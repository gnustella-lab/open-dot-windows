using System.Net.Http.Headers;
using System.Text.Json;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Integrations.Composio;

public class ComposioClient : ITriggerService
{
    private readonly HttpClient _http;
    private readonly Func<string?> _apiKeyProvider;
    private const string BaseUrl = "https://backend.composio.dev/api/v1";

    public ComposioClient(HttpClient? http = null, Func<string?>? apiKeyProvider = null)
    {
        _http = http ?? new HttpClient();
        _apiKeyProvider = apiKeyProvider ?? (() => Environment.GetEnvironmentVariable("COMPOSIO_API_KEY"));
    }

    private string? GetApiKey() => _apiKeyProvider();

    public async Task<List<TriggerApp>> GetTriggerAppsAsync(CancellationToken ct = default)
    {
        var key = GetApiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            return
            [
                new TriggerApp { Slug = "gmail", Name = "Gmail", Connected = false },
                new TriggerApp { Slug = "googlecalendar", Name = "Google Calendar", Connected = false },
                new TriggerApp { Slug = "slack", Name = "Slack", Connected = false },
                new TriggerApp { Slug = "github", Name = "GitHub", Connected = false },
                new TriggerApp { Slug = "notion", Name = "Notion", Connected = false },
                new TriggerApp { Slug = "linear", Name = "Linear", Connected = false },
                new TriggerApp { Slug = "discord", Name = "Discord", Connected = false }
            ];
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/apps");
            req.Headers.Add("X-API-KEY", key);
            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return [];

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var list = new List<TriggerApp>();
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var el in items.EnumerateArray())
                {
                    var slug = el.GetProperty("key").GetString() ?? "";
                    var name = el.GetProperty("name").GetString() ?? slug;
                    list.Add(new TriggerApp { Slug = slug, Name = name, Connected = false });
                }
            }
            return list;
        }
        catch
        {
            return [];
        }
    }

    public async Task<string?> ConnectAppUrlAsync(string slug, CancellationToken ct = default)
    {
        var key = GetApiKey();
        if (string.IsNullOrWhiteSpace(key)) return null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/connectedAccounts");
            req.Headers.Add("X-API-KEY", key);
            var payload = new { appName = slug, redirectUrl = "http://localhost:3100/api/composio/oauth" };
            req.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("connectionUrl", out var urlProp))
            {
                return urlProp.GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public Task<List<TriggerType>> GetTriggerTypesAsync(string slug, CancellationToken ct = default)
    {
        return Task.FromResult<List<TriggerType>>([
            new TriggerType
            {
                Slug = $"{slug.ToUpperInvariant()}_NEW_EVENT",
                Name = $"New {slug} event",
                Description = $"Fires when a new event occurs in {slug}",
                Fields = [new TriggerField { Name = "query", Title = "Filter query", Description = "Optional filter", Type = "string", Required = false }]
            }
        ]);
    }

    public Task StartListeningAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task StopListeningAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
