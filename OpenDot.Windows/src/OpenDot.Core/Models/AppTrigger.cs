namespace OpenDot.Core.Models;

public class AppTrigger
{
    public string Id { get; set; } = string.Empty;
    public string DotId { get; set; } = string.Empty;
    public string ComposioId { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty; // e.g. GMAIL_NEW_GMAIL_MESSAGE
    public string Toolkit { get; set; } = string.Empty; // e.g. gmail
    public string Name { get; set; } = string.Empty; // e.g. "New Gmail message"
    public string ConfigJson { get; set; } = "{}";
    public string Instruction { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public long? LastFiredAt { get; set; }
    public string? LastError { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
