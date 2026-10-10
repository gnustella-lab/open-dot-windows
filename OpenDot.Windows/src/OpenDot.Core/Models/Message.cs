namespace OpenDot.Core.Models;

public class Message
{
    public string Id { get; set; } = string.Empty;
    public string DotId { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // "user" | "dot" | "activity" | "card" | "system"
    public string Text { get; set; } = string.Empty;
    public string? Title { get; set; }
    public CardData? Card { get; set; }
    public string? From { get; set; } // "routine:<name>" | "dot:<name>" | null
    public List<Attachment>? Attachments { get; set; }
    public string? ChannelId { get; set; }
    public string? ConversationId { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
