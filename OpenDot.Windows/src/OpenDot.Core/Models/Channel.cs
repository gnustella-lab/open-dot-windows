namespace OpenDot.Core.Models;

public class Channel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LeadId { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = [];
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
