using System.Text;
using System.Text.Json;
using OpenDot.Core.Agents;

namespace OpenDot.Integrations.OpenAI;

internal sealed class CodexResponseStream
{
    private readonly Action<string>? _onDelta;
    private readonly Action<string>? _onActivity;
    private readonly StringBuilder _text = new();
    private readonly SortedDictionary<int, JsonElement> _completedItems = new();
    private readonly Dictionary<string, ToolCallBuilder> _tools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _itemIndexes = new(StringComparer.Ordinal);

    private CodexResponseStream(Action<string>? onDelta, Action<string>? onActivity)
    {
        _onDelta = onDelta;
        _onActivity = onActivity;
    }

    internal static async Task<AiModelResponse> ReadAsync(Stream stream, Action<string>? onDelta,
        Action<string>? onActivity, CancellationToken ct)
    {
        var parser = new CodexResponseStream(onDelta, onActivity);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var data = new StringBuilder();
        string? eventName = null;
        try
        {
            while (true)
            {
                idle.CancelAfter(TimeSpan.FromMinutes(5));
                var line = await reader.ReadLineAsync(idle.Token).ConfigureAwait(false);
                if (line == null || line.Length == 0)
                {
                    if (data.Length > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        var result = parser.ProcessEvent(data.ToString(), eventName);
                        if (result != null) return result;
                        data.Clear();
                    }
                    eventName = null;
                    if (line == null) break;
                    continue;
                }
                if (line[0] == ':') continue;
                var separator = line.IndexOf(':');
                var field = separator < 0 ? line : line[..separator];
                var value = separator < 0 ? "" : line[(separator + 1)..];
                if (value.StartsWith(' ')) value = value[1..];
                if (field == "event") eventName = value;
                else if (field == "data")
                {
                    if (data.Length > 0) data.Append('\n');
                    data.Append(value);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The Codex response stopped arriving. The partial response was not completed; try again.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Codex returned malformed response data. The partial response was not completed; try again.");
        }
        catch (IOException)
        {
            ct.ThrowIfCancellationRequested();
            throw new IOException("The Codex connection ended unexpectedly. The partial response was not completed; try again.");
        }
        ct.ThrowIfCancellationRequested();
        throw new InvalidOperationException("The Codex stream ended before response.completed. The partial response was not completed; try again.");
    }

    private AiModelResponse? ProcessEvent(string data, string? eventName)
    {
        if (data.Trim() == "[DONE]")
            throw new InvalidOperationException("Codex ended its stream without confirming completion. Try again.");
        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;
        var type = StringProperty(root, "type") ?? eventName;
        switch (type)
        {
            case "response.output_text.delta":
            case "response.refusal.delta":
                var delta = RequiredString(root, "delta");
                _text.Append(delta);
                _onActivity?.Invoke("Writing");
                _onDelta?.Invoke(delta);
                break;
            case "response.output_item.added":
            case "response.output_item.done":
                if (!root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object)
                    throw InvalidStream();
                var index = ItemIndex(root, item);
                if (type == "response.output_item.done") _completedItems[index] = item.Clone();
                if (StringProperty(item, "type") == "function_call")
                {
                    _onActivity?.Invoke("Calling tools");
                    var id = RequiredString(item, "id");
                    if (!_tools.TryGetValue(id, out var builder))
                    {
                        builder = new ToolCallBuilder { Index = index };
                        _tools.Add(id, builder);
                    }
                    builder.CallId = StringProperty(item, "call_id") ?? builder.CallId;
                    builder.Name = StringProperty(item, "name") ?? builder.Name;
                    var arguments = StringProperty(item, "arguments");
                    if (arguments != null && (type == "response.output_item.done" || builder.Arguments.Length == 0))
                    {
                        builder.Arguments.Clear();
                        builder.Arguments.Append(arguments);
                    }
                    if (type == "response.output_item.done") builder.Complete = true;
                }
                break;
            case "response.function_call_arguments.delta":
            case "response.function_call_arguments.done":
                var itemId = RequiredString(root, "item_id");
                if (!_tools.TryGetValue(itemId, out var tool)) throw InvalidStream();
                if (type == "response.function_call_arguments.delta")
                    tool.Arguments.Append(RequiredString(root, "delta"));
                else
                {
                    tool.Arguments.Clear();
                    tool.Arguments.Append(RequiredString(root, "arguments"));
                    tool.Complete = true;
                }
                break;
            case "response.completed":
            case "response.done":
                if (!root.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object)
                    throw InvalidStream();
                var status = StringProperty(response, "status");
                if (status != null && status != "completed") throw StreamFailure(response);
                return Complete(response);
            case "response.failed":
            case "response.incomplete":
            case "response.cancelled":
                throw StreamFailure(root.TryGetProperty("response", out var failed) ? failed : root);
            case "error":
                throw StreamFailure(root);
            case null:
                throw InvalidStream();
            // Created, reasoning, content-part and usage events do not carry visible text or executable tool calls.
        }
        return null;
    }

    private int ItemIndex(JsonElement root, JsonElement item)
    {
        var id = StringProperty(item, "id");
        if (id != null && _itemIndexes.TryGetValue(id, out var existing)) return existing;
        var index = root.TryGetProperty("output_index", out var value) && value.TryGetInt32(out var supplied)
            ? supplied : _itemIndexes.Count;
        if (id != null) _itemIndexes[id] = index;
        return index;
    }

    private AiModelResponse Complete(JsonElement response)
    {
        var id = RequiredString(response, "id");
        var calls = new List<AiToolCall>();
        var finalText = new StringBuilder();
        var hasMessage = false;
        var hasFinalOutput = response.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array;
        IEnumerable<JsonElement> items = hasFinalOutput ? output.EnumerateArray() : _completedItems.Values;
        foreach (var item in items)
        {
            var status = StringProperty(item, "status");
            if (status is "incomplete" or "failed" or "in_progress") throw InvalidStream();
            switch (StringProperty(item, "type"))
            {
                case "message":
                    hasMessage = true;
                    if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                        throw InvalidStream();
                    foreach (var part in content.EnumerateArray())
                    {
                        var partType = StringProperty(part, "type");
                        if (partType == "output_text") finalText.Append(RequiredString(part, "text"));
                        else if (partType == "refusal") finalText.Append(RequiredString(part, "refusal"));
                    }
                    break;
                case "function_call":
                    calls.Add(CreateToolCall(RequiredString(item, "call_id"), RequiredString(item, "name"), RequiredString(item, "arguments")));
                    break;
            }
        }
        if (!hasFinalOutput)
        {
            var included = calls.Select(call => call.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var builder in _tools.Values.OrderBy(tool => tool.Index))
            {
                if (!builder.Complete) throw InvalidStream();
                if (included.Add(builder.CallId))
                    calls.Add(CreateToolCall(builder.CallId, builder.Name, builder.Arguments.ToString()));
            }
        }
        var streamed = _text.ToString();
        var text = hasMessage || hasFinalOutput ? finalText.ToString() : streamed;
        if (!text.StartsWith(streamed, StringComparison.Ordinal)) throw InvalidStream();
        if (text.Length > streamed.Length)
        {
            _onActivity?.Invoke("Writing");
            _onDelta?.Invoke(text[streamed.Length..]);
        }
        if (calls.Count > 0) _onActivity?.Invoke("Calling tools");
        return new AiModelResponse { Id = id, Content = text, ToolCalls = calls, FinishReason = calls.Count > 0 ? "tool_calls" : "stop" };
    }

    private static AiToolCall CreateToolCall(string callId, string name, string arguments)
    {
        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(name)) throw InvalidStream();
        using var json = JsonDocument.Parse(arguments);
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw InvalidStream();
        return new AiToolCall { Id = callId, Name = name, ArgumentsJson = arguments };
    }

    private static InvalidOperationException StreamFailure(JsonElement response)
    {
        var error = response.ValueKind == JsonValueKind.Object && response.TryGetProperty("error", out var nested) ? nested : response;
        var code = StringProperty(error, "code") ?? StringProperty(error, "type");
        var guidance = code switch
        {
            "context_length_exceeded" => "The conversation exceeds the Codex context window. Start a new conversation.",
            "rate_limit_exceeded" or "usage_limit_reached" or "insufficient_quota" => "Your Codex usage limit has been reached. Wait for your ChatGPT allowance to reset.",
            "model_not_found" => "This model is not available to your Codex account. Refresh the model list in Settings.",
            "invalid_api_key" or "token_expired" => "The Codex session is no longer valid. Disconnect and sign in again in Settings.",
            "content_filter" or "content_policy_violation" => "Codex could not complete this response because of its content policy.",
            _ => "Codex did not complete the response. Try again; if it persists, check the model and account access in Settings."
        };
        return new InvalidOperationException(guidance);
    }

    private static InvalidOperationException InvalidStream() => new("Codex returned an incomplete or inconsistent response. No tool calls were executed; try again.");

    internal static string? StringProperty(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static string RequiredString(JsonElement value, string name) => StringProperty(value, name) ?? throw InvalidStream();

    private sealed class ToolCallBuilder
    {
        internal int Index { get; init; }
        internal string CallId { get; set; } = "";
        internal string Name { get; set; } = "";
        internal StringBuilder Arguments { get; } = new();
        internal bool Complete { get; set; }
    }
}
