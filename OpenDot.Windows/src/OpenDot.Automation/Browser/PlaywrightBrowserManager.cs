using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Playwright;

namespace OpenDot.Automation.Browser;

public class PlaywrightBrowserManager : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private readonly ConcurrentDictionary<string, DotBrowserSession> _sessions = new();
    private readonly string _baseDataDir;
    private readonly object _initLock = new();

    public record DotBrowserSession(
        IBrowserContext Context,
        IPage Page,
        byte[]? LastScreenshot,
        long ShotAt
    )
    {
        public byte[]? LastScreenshot { get; set; } = LastScreenshot;
        public long ShotAt { get; set; } = ShotAt;
    }

    public PlaywrightBrowserManager(string? baseDataDir = null)
    {
        _baseDataDir = baseDataDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDot");
    }

    private async Task<IPlaywright> GetPlaywrightAsync()
    {
        if (_playwright != null) return _playwright;
        _playwright = await Playwright.CreateAsync();
        return _playwright;
    }

    private string ProfileDir(string dotId)
    {
        var dir = Path.Combine(_baseDataDir, "dots", dotId, "browser");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void FreeProfileIfLocked(string profileDir)
    {
        // Safe Windows Chrome profile lock release:
        // Only kill chrome.exe processes that have THIS exact profile directory in their arguments!
        try
        {
            var normalizedDir = profileDir.Replace("/", "\\").TrimEnd('\\');
            var chromeProcesses = Process.GetProcessesByName("chrome");
            foreach (var proc in chromeProcesses)
            {
                try
                {
                    // Check process command line safely
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {proc.Id}");
                    foreach (var obj in searcher.Get())
                    {
                        var cmdLine = obj["CommandLine"]?.ToString() ?? "";
                        if (cmdLine.Contains(normalizedDir, StringComparison.OrdinalIgnoreCase))
                        {
                            proc.Kill();
                            proc.WaitForExit(2000);
                        }
                    }
                }
                catch
                {
                    // Ignore processes we can't inspect
                }
            }
        }
        catch
        {
            // ManagementObjectSearcher fallback if WMI is unavailable
        }
    }

    private async Task<DotBrowserSession> GetOrCreateSessionAsync(string dotId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(dotId, out var existing))
        {
            return existing;
        }

        var pw = await GetPlaywrightAsync();
        var dir = ProfileDir(dotId);
        FreeProfileIfLocked(dir);

        var launchOptions = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            Locale = "en-US",
            TimezoneId = TimeZoneInfo.Local.Id,
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--no-default-browser-check",
                "--no-first-run",
                "--disable-infobars"
            ]
        };

        IBrowserContext context;
        try
        {
            launchOptions.Channel = "chrome";
            context = await pw.Chromium.LaunchPersistentContextAsync(dir, launchOptions);
        }
        catch
        {
            // Fallback to bundled chromium if system chrome is not installed
            launchOptions.Channel = null;
            context = await pw.Chromium.LaunchPersistentContextAsync(dir, launchOptions);
        }

        var pages = context.Pages;
        var page = pages.Count > 0 ? pages[^1] : await context.NewPageAsync();

        var session = new DotBrowserSession(context, page, null, 0);
        _sessions[dotId] = session;

        context.Close += (_, _) => _sessions.TryRemove(dotId, out _);

        return session;
    }

    public async Task<string> OpenUrlAsync(string dotId, string url, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(dotId, ct);
        var targetUrl = url.StartsWith("http://") || url.StartsWith("https://") ? url : $"https://{url}";
        await session.Page.GotoAsync(targetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        return $"Opened {targetUrl}. Page title: {await session.Page.TitleAsync()}";
    }

    public async Task<string> ReadPageAsync(string dotId, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(dotId, ct);
        try
        {
            var text = await session.Page.EvaluateAsync<string>("""
            () => {
                const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                let out = '';
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    if (node.parentElement && !['SCRIPT','STYLE','NOSCRIPT'].includes(node.parentElement.tagName)) {
                        const trimmed = node.nodeValue.trim();
                        if (trimmed) out += trimmed + ' ';
                    }
                }
                return out.slice(0, 15000);
            }
            """);
            return string.IsNullOrWhiteSpace(text) ? "(Empty page)" : text;
        }
        catch (Exception ex)
        {
            return $"Error reading page: {ex.Message}";
        }
    }

    public async Task<string> ClickTextAsync(string dotId, string text, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(dotId, ct);
        try
        {
            // Try precise text click
            var locator = session.Page.GetByText(text, new PageGetByTextOptions { Exact = false });
            if (await locator.CountAsync() > 0)
            {
                await locator.First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
                return $"Clicked \"{text}\".";
            }

            // Fallback: evaluate click via JS query
            var clicked = await session.Page.EvaluateAsync<bool>("""
            (query) => {
                const elements = Array.from(document.querySelectorAll('button, a, input[type=button], input[type=submit], [role=button], label'));
                const match = elements.find(el => el.innerText && el.innerText.toLowerCase().includes(query.toLowerCase()));
                if (match) {
                    match.click();
                    return true;
                }
                return false;
            }
            """, text);

            return clicked ? $"Clicked \"{text}\"." : $"Could not find clickable element with text \"{text}\".";
        }
        catch (Exception ex)
        {
            return $"Failed to click \"{text}\": {ex.Message}";
        }
    }

    public async Task<string> TypeTextAsync(string dotId, string field, string text, bool submit, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(dotId, ct);
        try
        {
            // Try by placeholder
            var byPlaceholder = session.Page.GetByPlaceholder(field, new PageGetByPlaceholderOptions { Exact = false });
            if (await byPlaceholder.CountAsync() > 0)
            {
                await byPlaceholder.First.FillAsync(text);
                if (submit) await byPlaceholder.First.PressAsync("Enter");
                return $"Typed into \"{field}\".";
            }

            // Try by label
            var byLabel = session.Page.GetByLabel(field, new PageGetByLabelOptions { Exact = false });
            if (await byLabel.CountAsync() > 0)
            {
                await byLabel.First.FillAsync(text);
                if (submit) await byLabel.First.PressAsync("Enter");
                return $"Typed into \"{field}\".";
            }

            // Fallback query selector
            var typed = await session.Page.EvaluateAsync<bool>("""
            ([f, val, sub]) => {
                const inputs = Array.from(document.querySelectorAll('input, textarea'));
                const match = inputs.find(i => (i.name && i.name.toLowerCase().includes(f.toLowerCase())) ||
                                               (i.placeholder && i.placeholder.toLowerCase().includes(f.toLowerCase())) ||
                                               (i.id && i.id.toLowerCase().includes(f.toLowerCase())));
                if (match) {
                    match.value = val;
                    match.dispatchEvent(new Event('input', { bubbles: true }));
                    match.dispatchEvent(new Event('change', { bubbles: true }));
                    if (sub && match.form) match.form.submit();
                    return true;
                }
                return false;
            }
            """, new object[] { field, text, submit });

            return typed ? $"Typed into \"{field}\"." : $"Could not find input field \"{field}\".";
        }
        catch (Exception ex)
        {
            return $"Failed to type into \"{field}\": {ex.Message}";
        }
    }

    public async Task<string> FillLoginAsync(string dotId, string username, string password, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(dotId, ct);
        try
        {
            var filled = await session.Page.EvaluateAsync<bool>("""
            ([user, pass]) => {
                const userField = document.querySelector('input[type=email], input[type=text][name*=user], input[type=text][name*=email], input[name*=login], input[id*=user], input[id*=email]');
                const passField = document.querySelector('input[type=password]');
                if (userField && passField) {
                    userField.value = user;
                    userField.dispatchEvent(new Event('input', { bubbles: true }));
                    userField.dispatchEvent(new Event('change', { bubbles: true }));
                    passField.value = pass;
                    passField.dispatchEvent(new Event('input', { bubbles: true }));
                    passField.dispatchEvent(new Event('change', { bubbles: true }));
                    return true;
                }
                return false;
            }
            """, new object[] { username, password });

            return filled
                ? "Filled login form with saved credentials. Submit when ready."
                : "Could not locate both username and password fields on this page.";
        }
        catch (Exception ex)
        {
            return $"Error filling login: {ex.Message}";
        }
    }

    public async Task<byte[]> ScreenshotAsync(string dotId, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(dotId, ct);
        var bytes = await session.Page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png });
        session.LastScreenshot = bytes;
        session.ShotAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return bytes;
    }

    public byte[]? GetLastScreenshot(string dotId)
    {
        return _sessions.TryGetValue(dotId, out var s) ? s.LastScreenshot : null;
    }

    public async Task SleepAsync(string dotId)
    {
        if (_sessions.TryRemove(dotId, out var session))
        {
            await session.Context.CloseAsync();
        }
    }

    public async Task DestroyAsync(string dotId)
    {
        await SleepAsync(dotId);
        try
        {
            var dir = ProfileDir(dotId);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var key in _sessions.Keys)
        {
            await SleepAsync(key);
        }
        _playwright?.Dispose();
    }
}
