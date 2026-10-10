using OpenDot.Automation.Browser;
using OpenDot.Automation.Workspaces;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Automation.Computer;

public class ComputerManager : IComputerManager
{
    private readonly PlaywrightBrowserManager _browser;
    private readonly string _baseDir;

    public string DefaultMode => "local";

    public ComputerManager(PlaywrightBrowserManager? browser = null, string? baseDir = null)
    {
        _baseDir = baseDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDot");
        _browser = browser ?? new PlaywrightBrowserManager(_baseDir);
    }

    public string ModeFor(string dotId) => "local";

    public string Describe(string dotId) =>
        "a persistent workspace folder on your Windows computer, plus a dedicated Chromium browser";

    private string WorkspaceDir(string dotId)
    {
        var dir = Path.Combine(_baseDir, "dots", dotId, "workspace");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---------- Browser Automation ----------
    public Task<string> OpenUrlAsync(string dotId, string url, CancellationToken ct = default) =>
        _browser.OpenUrlAsync(dotId, url, ct);

    public Task<string> ReadPageAsync(string dotId, CancellationToken ct = default) =>
        _browser.ReadPageAsync(dotId, ct);

    public Task<string> ClickTextAsync(string dotId, string text, CancellationToken ct = default) =>
        _browser.ClickTextAsync(dotId, text, ct);

    public Task<string> TypeTextAsync(string dotId, string field, string text, bool submit, CancellationToken ct = default) =>
        _browser.TypeTextAsync(dotId, field, text, submit, ct);

    public Task<string> FillLoginAsync(string dotId, string username, string password, CancellationToken ct = default) =>
        _browser.FillLoginAsync(dotId, username, password, ct);

    public Task<byte[]> ScreenshotAsync(string dotId, CancellationToken ct = default) =>
        _browser.ScreenshotAsync(dotId, ct);

    public byte[]? GetLastScreenshot(string dotId) =>
        _browser.GetLastScreenshot(dotId);

    public Task WakeAsync(string dotId, CancellationToken ct = default) =>
        _browser.OpenUrlAsync(dotId, "https://www.bing.com", ct);

    public Task SleepAsync(string dotId, CancellationToken ct = default) =>
        _browser.SleepAsync(dotId);

    public Task<string?> TakeOverAsync(string dotId, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    public Task HandBackAsync(string dotId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public async Task ResetAsync(string dotId, CancellationToken ct = default)
    {
        await _browser.SleepAsync(dotId);
        var ws = WorkspaceDir(dotId);
        try
        {
            if (Directory.Exists(ws)) Directory.Delete(ws, true);
        }
        catch { }
    }

    public async Task DestroyAsync(string dotId, CancellationToken ct = default)
    {
        await _browser.DestroyAsync(dotId);
        var ws = WorkspaceDir(dotId);
        try
        {
            if (Directory.Exists(ws)) Directory.Delete(ws, true);
        }
        catch { }
    }

    // ---------- Workspace & Commands ----------
    public Task<string> RunCommandAsync(string dotId, string command, CancellationToken ct = default)
    {
        var ws = WorkspaceDir(dotId);
        return CommandRunner.RunAsync(command, ws, ct);
    }

    public Task<string> RunOnUserComputerAsync(string command, CancellationToken ct = default)
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return CommandRunner.RunAsync(command, homeDir, ct);
    }

    public async Task<byte[]> ReadFileAsync(string dotId, string relativePath, CancellationToken ct = default)
    {
        var ws = WorkspaceDir(dotId);
        var fullPath = Path.GetFullPath(Path.Combine(ws, relativePath));
        if (!fullPath.StartsWith(ws, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path traversal outside workspace is denied.");

        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    public async Task<string> WriteFileAsync(string dotId, string relativePath, string content, CancellationToken ct = default)
    {
        var ws = WorkspaceDir(dotId);
        var fullPath = Path.GetFullPath(Path.Combine(ws, relativePath));
        if (!fullPath.StartsWith(ws, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path traversal outside workspace is denied.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, ct);
        return Path.GetRelativePath(ws, fullPath).Replace("\\", "/");
    }

    public Task<List<FileEntry>> ListFilesAsync(string dotId, CancellationToken ct = default)
    {
        var ws = WorkspaceDir(dotId);
        var list = new List<FileEntry>();
        var di = new DirectoryInfo(ws);
        if (!di.Exists) return Task.FromResult(list);

        foreach (var f in di.GetFiles("*", SearchOption.AllDirectories))
        {
            list.Add(new FileEntry(Path.GetRelativePath(ws, f.FullName).Replace("\\", "/"), f.Length, false));
        }

        foreach (var d in di.GetDirectories("*", SearchOption.AllDirectories))
        {
            list.Add(new FileEntry(Path.GetRelativePath(ws, d.FullName).Replace("\\", "/"), 0, true));
        }

        return Task.FromResult(list);
    }
}
