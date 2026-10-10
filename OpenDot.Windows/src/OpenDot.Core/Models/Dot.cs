namespace OpenDot.Core.Models;

public class Dot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public Look Look { get; set; } = new();
    public string Status { get; set; } = "idle"; // "idle" | "working" | "waiting" | "paused"
    public string? Activity { get; set; } // e.g. "Searching the web", "Using computer"
    public bool LocalAccess { get; set; } = false; // allow running on user's host machine
    public string? Model { get; set; } // null = use system default
    public string? BoxId { get; set; } // cloud sandbox identifier
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
