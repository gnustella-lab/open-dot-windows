namespace OpenDot.Core.Models;

public class Memory
{
    public string Id { get; set; } = string.Empty;
    public string DotId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
