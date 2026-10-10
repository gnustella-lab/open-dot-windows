namespace OpenDot.Core.Models;

public class Attachment
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Mime { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? BoxPath { get; set; }
}
