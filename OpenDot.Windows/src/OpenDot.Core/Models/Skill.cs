namespace OpenDot.Core.Models;

public class Skill
{
    public string Id { get; set; } = string.Empty;
    public string DotId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty; // Markdown procedural instructions
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
