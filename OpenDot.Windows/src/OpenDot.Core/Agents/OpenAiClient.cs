using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenDot.Core.Agents;

namespace OpenDot.Integrations.OpenAI;

public class OpenAiClient : IAiClient
{
    private readonly HttpClient _http;
    private readonly Func<string?> _apiKeyProvider;

    public string ProviderName => "OpenAI";

    public OpenAiClient(HttpClient? http = null, Func<string?>? apiKeyProvider = null)
    {
        _http = http ?? new HttpClient();
        _apiKeyProvider = apiKeyProvider ?? (() => Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    private string GetApiKey()
    {
        var key = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("No OpenAI API key found. Please add your API key in Settings.");
        return key;
    }

    public async Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var res = await _http.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetApiKey());
            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo"];

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var list = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var el in data.EnumerateArray())
                {
                    var id = el.GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(id) && (id.StartsWith("gpt-") || id.StartsWith("o1") || id.StartsWith("o3")))
                    {
                        if (!id.Contains("audio") && !id.Contains("realtime") && !id.Contains("transcribe") && !id.Contains("moderation"))
                        {
                            list.Add(id);
                        }
                    }
                }
            }
            return list.Count > 0 ? list : ["gpt-4o", "gpt-4o-mini", "gpt-4.5-preview"];
        }
        catch
        {
            return ["gpt-4o", "gpt-4o-mini"];
        }
    }

    public async Task<AiModelResponse> StreamAsync(
        AiModelRequest request,
        Action<string>? onDelta = null,
        Action<string>? onActivity = null,
        CancellationToken ct = default)
    {
        var apiKey = GetApiKey();
        var model = !string.IsNullOrWhiteSpace(request.Model) ? request.Model : "gpt-4o";

        // Build messages payload
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }

        foreach (var m in request.Messages)
        {
            if (m.Role == "tool")
            {
                messages.Add(new { role = "tool", tool_call_id = m.ToolCallId, content = m.Content });
            }
            else if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                var toolCallsPayload = m.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new { name = tc.Name, arguments = tc.ArgumentsJson }
                }).ToList();
                messages.Add(new { role = "assistant", content = m.Content, tool_calls = toolCallsPayload });
            }
            else
            {
                messages.Add(new { role = m.Role, content = m.Content });
            }
        }

        // Build tools payload
        var toolsPayload = request.Tools.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = JsonDocument.Parse(t.ParametersJson).RootElement
            }
        }).ToList();

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["stream"] = true
        };

        if (toolsPayload.Count > 0)
        {
            body["tools"] = toolsPayload;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        onActivity?.Invoke("Thinking");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"OpenAI request failed ({res.StatusCode}): {err}");
        }

        using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var responseContent = new StringBuilder();
        var toolCallBuilders = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        string finishReason = "stop";
        string responseId = $"resp_{Guid.NewGuid():N}";

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..].Trim();
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    responseId = idProp.GetString() ?? responseId;
                }

                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind == JsonValueKind.String)
                    {
                        finishReason = fr.GetString() ?? finishReason;
                    }

                    if (choice.TryGetProperty("delta", out var delta))
                    {
                        if (delta.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                        {
                            var textDelta = contentProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(textDelta))
                            {
                                responseContent.Append(textDelta);
                                onActivity?.Invoke("Writing");
                                onDelta?.Invoke(textDelta);
                            }
                        }

                        if (delta.TryGetProperty("tool_calls", out var toolCalls))
                        {
                            onActivity?.Invoke("Calling tools");
                            foreach (var tc in toolCalls.EnumerateArray())
                            {
                                var index = tc.GetProperty("index").GetInt32();
                                if (!toolCallBuilders.ContainsKey(index))
                                {
                                    var id = tc.TryGetProperty("id", out var idP) ? idP.GetString() ?? $"call_{Guid.NewGuid():N}" : $"call_{Guid.NewGuid():N}";
                                    var name = tc.TryGetProperty("function", out var f) && f.TryGetProperty("name", out var nP) ? nP.GetString() ?? "" : "";
                                    toolCallBuilders[index] = (id, name, new StringBuilder());
                                }

                                if (tc.TryGetProperty("function", out var fn) && fn.TryGetProperty("arguments", out var argP))
                                {
                                    var argDelta = argP.GetString();
                                    if (!string.IsNullOrEmpty(argDelta))
                                    {
                                        toolCallBuilders[index].Args.Append(argDelta);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore SSE framing / JSON chunk anomalies
            }
        }

        var finalToolCalls = toolCallBuilders.OrderBy(k => k.Key).Select(kv => new AiToolCall
        {
            Id = kv.Value.Id,
            Name = kv.Value.Name,
            ArgumentsJson = kv.Value.Args.ToString()
        }).ToList();

        return new AiModelResponse
        {
            Id = responseId,
            Content = responseContent.ToString(),
            ToolCalls = finalToolCalls,
            FinishReason = finishReason
        };
    }
}
