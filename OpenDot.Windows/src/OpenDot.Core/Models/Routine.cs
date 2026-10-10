namespace OpenDot.Core.Models;

public class Routine
{
    public string Id { get; set; } = string.Empty;
    public string DotId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty; // Cron expression
    public bool Enabled { get; set; } = true;
    public long? LastRunAt { get; set; }
    public long? NextRunAt { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
