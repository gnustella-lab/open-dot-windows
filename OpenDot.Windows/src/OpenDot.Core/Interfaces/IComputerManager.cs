namespace OpenDot.Core.Interfaces;

using OpenDot.Core.Models;

public interface IComputerManager
{
    string DefaultMode { get; }
    string ModeFor(string dotId);
    string Describe(string dotId);

    // Browser automation
    Task<string> OpenUrlAsync(string dotId, string url, CancellationToken ct = default);
    Task<string> ReadPageAsync(string dotId, CancellationToken ct = default);
    Task<string> ClickTextAsync(string dotId, string text, CancellationToken ct = default);
    Task<string> TypeTextAsync(string dotId, string field, string text, bool submit, CancellationToken ct = default);
    Task<string> FillLoginAsync(string dotId, string username, string password, CancellationToken ct = default);
    Task<byte[]> ScreenshotAsync(string dotId, CancellationToken ct = default);
    byte[]? GetLastScreenshot(string dotId);
    Task WakeAsync(string dotId, CancellationToken ct = default);
    Task SleepAsync(string dotId, CancellationToken ct = default);
    Task<string?> TakeOverAsync(string dotId, CancellationToken ct = default);
    Task HandBackAsync(string dotId, CancellationToken ct = default);
    Task ResetAsync(string dotId, CancellationToken ct = default);
    Task DestroyAsync(string dotId, CancellationToken ct = default);

    // Workspace & commands
    Task<string> RunCommandAsync(string dotId, string command, CancellationToken ct = default);
    Task<string> RunOnUserComputerAsync(string command, CancellationToken ct = default);
    Task<byte[]> ReadFileAsync(string dotId, string relativePath, CancellationToken ct = default);
    Task<string> WriteFileAsync(string dotId, string relativePath, string content, CancellationToken ct = default);
    Task<List<FileEntry>> ListFilesAsync(string dotId, CancellationToken ct = default);
}
