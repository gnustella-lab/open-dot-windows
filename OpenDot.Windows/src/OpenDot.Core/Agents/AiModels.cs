namespace OpenDot.Core.Agents;

public class AiToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
}

public class AiChatMessage
{
    public string Role { get; set; } = "user"; // "system", "user", "assistant", "tool"
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
    public List<AiToolCall>? ToolCalls { get; set; }
}

public class AiModelRequest
{
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<AiChatMessage> Messages { get; set; } = [];
    public List<ToolDefinition> Tools { get; set; } = [];
    public string? PreviousResponseId { get; set; }
}

public class AiModelResponse
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<AiToolCall> ToolCalls { get; set; } = [];
    public string FinishReason { get; set; } = "stop";
}

public interface IAiClient
{
    string ProviderName { get; }
    Task<AiModelResponse> StreamAsync(
        AiModelRequest request,
        Action<string>? onDelta = null,
        Action<string>? onActivity = null,
        CancellationToken ct = default);

    Task<List<string>> ListModelsAsync(CancellationToken ct = default);
    Task<bool> ValidateKeyAsync(string apiKey, CancellationToken ct = default);
}
