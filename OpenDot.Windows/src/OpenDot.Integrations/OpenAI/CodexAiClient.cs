using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenDot.Core.Agents;

namespace OpenDot.Integrations.OpenAI;

public sealed class CodexAiClient : IAiClient
{
    // Listed in openai/codex's models-manager/models.json with ordinary function-tool support.
    public const string DefaultModel = "codex:gpt-5.5";
    private const string BaseUrl = "https://chatgpt.com/backend-api/codex";
    private const string CatalogClientVersion = "0.124.0";
    private readonly HttpClient _http;
    private readonly CodexAuthService _auth;

    public CodexAiClient(HttpClient http, CodexAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public string ProviderName => "OpenAI (Codex)";

    public Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(false); // This provider accepts an explicit ChatGPT sign-in, never an API key.
    }

    public async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
    {
        var token = await _auth.GetAccessTokenAsync(ct).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token.Lifetime);
        using var request = CreateRequest(HttpMethod.Get, $"{BaseUrl}/models?client_version={CatalogClientVersion}", token);
        using var response = await _http.SendAsync(request, linked.Token).ConfigureAwait(false);
        EnsureSuccess(response);
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: linked.Token).ConfigureAwait(false);
            if (!json.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Codex returned an invalid model catalog. Try refreshing the model list later.");
            var result = new List<string>();
            foreach (var model in models.EnumerateArray())
            {
                var slug = CodexResponseStream.StringProperty(model, "slug");
                if (string.IsNullOrWhiteSpace(slug) || CodexResponseStream.StringProperty(model, "visibility") != "list") continue;
                if (CodexResponseStream.StringProperty(model, "tool_mode") == "code_mode_only"
                    || (model.TryGetProperty("use_responses_lite", out var lite) && lite.ValueKind == JsonValueKind.True)) continue;
                result.Add("codex:" + slug);
            }
            if (result.Count == 0)
                throw new InvalidOperationException("No Codex models compatible with OpenDot's function tools are available for this account.");
            return result.Distinct(StringComparer.Ordinal).ToList();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Codex returned an invalid model catalog. Try refreshing the model list later.");
        }
    }

    public async Task<AiModelResponse> StreamAsync(
        AiModelRequest request,
        Action<string>? onDelta = null,
        Action<string>? onActivity = null,
        CancellationToken ct = default)
    {
        string model;
        if (request.Model.StartsWith("codex:", StringComparison.Ordinal) && request.Model.Length > "codex:".Length)
            model = request.Model["codex:".Length..];
        else if (request.Model.StartsWith("chatgpt:", StringComparison.Ordinal) && request.Model.Length > "chatgpt:".Length)
            model = request.Model["chatgpt:".Length..];
        else
            throw new InvalidOperationException("This dot does not have a Codex model selected. Choose a codex: model in its settings, or switch OpenAI authentication back to API key. Existing GPT-4o models cannot use ChatGPT sign-in.");
        var input = new List<object>();
        var instructions = new StringBuilder(request.SystemPrompt);
        foreach (var message in request.Messages)
        {
            if (message.Role == "system" || message.Role == "developer")
            {
                instructions.AppendLine().Append(message.Content);
                continue;
            }
            if (message.Role == "tool")
            {
                if (string.IsNullOrWhiteSpace(message.ToolCallId))
                    throw new InvalidOperationException("The conversation contains a tool result without its call ID. Start a new conversation.");
                input.Add(new { type = "function_call_output", call_id = message.ToolCallId, output = message.Content });
                continue;
            }
            if (message.Role != "user" && message.Role != "assistant")
                throw new InvalidOperationException("The conversation contains a message role that Codex cannot accept.");
            if (!string.IsNullOrEmpty(message.Content))
                input.Add(new
                {
                    type = "message",
                    role = message.Role,
                    content = new[] { new { type = message.Role == "assistant" ? "output_text" : "input_text", text = message.Content } }
                });
            if (message.ToolCalls == null) continue;
            foreach (var call in message.ToolCalls)
            {
                if (string.IsNullOrWhiteSpace(call.Id) || string.IsNullOrWhiteSpace(call.Name))
                    throw new InvalidOperationException("The conversation contains an incomplete tool call. Start a new conversation.");
                input.Add(new { type = "function_call", call_id = call.Id, name = call.Name, arguments = call.ArgumentsJson });
            }
        }
        var tools = new List<object>(request.Tools.Count);
        foreach (var tool in request.Tools)
        {
            using var schema = JsonDocument.Parse(tool.ParametersJson);
            tools.Add(new { type = "function", name = tool.Name, description = tool.Description, parameters = schema.RootElement.Clone(), strict = tool.Strict });
        }
        var body = new
        {
            model,
            instructions = instructions.Length > 0 ? instructions.ToString() : "You are a helpful assistant.",
            input,
            tools,
            tool_choice = "auto",
            parallel_tool_calls = true,
            store = false,
            stream = true
        };

        // store:false requires replaying history; previous_response_id is deliberately not sent.
        var token = await _auth.GetAccessTokenAsync(ct).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token.Lifetime);
        using var httpRequest = CreateRequest(HttpMethod.Post, BaseUrl + "/responses", token);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Content = JsonContent.Create(body);
        onActivity?.Invoke("Thinking");
        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        EnsureSuccess(response);
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Codex did not return an event stream. Check your connection and account access.");
        using var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
        return await CodexResponseStream.ReadAsync(stream, onDelta, onActivity, linked.Token).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, CodexAuthService.AccessToken token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        request.Headers.Add("ChatGPT-Account-Id", token.AccountId);
        request.Headers.Add("originator", CodexAuthService.Originator);
        request.Headers.UserAgent.ParseAdd("OpenDot.Windows/1.0");
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var guidance = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "The Codex session was rejected. Disconnect and sign in again in Settings.",
            HttpStatusCode.Forbidden => "This ChatGPT account cannot access Codex or the selected model. Check your plan and workspace permissions.",
            HttpStatusCode.TooManyRequests => "Your Codex usage limit has been reached. Wait for your ChatGPT usage allowance to reset.",
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound => "Codex rejected the model or request. Refresh the Codex model list and choose an available codex: model; for an old conversation, start a new one.",
            _ => "Codex is unavailable. Check your connection and try again later."
        };
        // Server error bodies may contain echoed credentials or input, so never put them in chat/logs.
        throw new HttpRequestException($"{guidance} (HTTP {(int)response.StatusCode})", null, response.StatusCode);
    }
}
