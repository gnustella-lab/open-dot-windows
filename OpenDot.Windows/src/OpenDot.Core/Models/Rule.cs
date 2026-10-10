namespace OpenDot.Core.Models;

public class Rule
{
    public string Id { get; set; } = string.Empty;
    public string? DotId { get; set; } // null = global rule for all dots
    public string Action { get; set; } = string.Empty; // e.g. "send an email", "delete files"
    public string Decision { get; set; } = "ask"; // "allow" | "ask" | "never"
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
