namespace OpenDot.Core.Models;

public class Conversation
{
    public string Id { get; set; } = string.Empty;
    public string DotId { get; set; } = string.Empty;
    public string Title { get; set; } = "New Chat";
    public string Kind { get; set; } = "chat"; // "chat" | "routine" | "trigger" | "channel"
    public string? Ref { get; set; }
    public string? Thread { get; set; } // Server-side OpenAI response ID
    public string? Pending { get; set; } // JSON of Pending tool calls
    public string? History { get; set; } // JSON of conversation history for stateless models
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
