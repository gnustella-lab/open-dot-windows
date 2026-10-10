namespace OpenDot.Core.Models;

public class PasswordEntry
{
    public string Id { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
