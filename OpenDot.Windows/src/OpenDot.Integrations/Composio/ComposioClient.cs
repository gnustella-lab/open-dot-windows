using System.Net.Http.Headers;
using System.Text.Json;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Integrations.Composio;

public class ComposioClient : ITriggerService
{
    private readonly HttpClient _http;
    private readonly Func<string?> _apiKeyProvider;
    private readonly Func<string>? _userIdProvider;
    private const string BaseUrl = "https://backend.composio.dev/api/v3.1";
    private const string BaseUrlV3 = "https://backend.composio.dev/api/v3";

    public static readonly Dictionary<string, string> StandardTriggerApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gmail"] = "Gmail",
        ["googlecalendar"] = "Google Calendar",
        ["slack"] = "Slack",
        ["github"] = "GitHub",
        ["notion"] = "Notion",
        ["outlook"] = "Outlook",
        ["linear"] = "Linear",
        ["jira"] = "Jira",
        ["hubspot"] = "HubSpot",
        ["discord"] = "Discord",
        ["asana"] = "Asana",
        ["googledrive"] = "Google Drive"
    };

    public ComposioClient(HttpClient? http = null, Func<string?>? apiKeyProvider = null, Func<string>? userIdProvider = null)
    {
        _http = http ?? new HttpClient();
        _apiKeyProvider = apiKeyProvider ?? (() => Environment.GetEnvironmentVariable("COMPOSIO_API_KEY"));
        _userIdProvider = userIdProvider;
    }

    private string? GetApiKey() => _apiKeyProvider();
    private string GetUserId() => _userIdProvider?.Invoke() ?? "default";

    public async Task<List<TriggerApp>> GetTriggerAppsAsync(CancellationToken ct = default)
    {
        var key = GetApiKey();
        var connectedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(key))
        {
            var userId = GetUserId();
            try
            {
                // 1. Query connected accounts for this install's user
                var queryUrl = $"{BaseUrl}/connected_accounts?user_ids={Uri.EscapeDataString(userId)}&statuses=ACTIVE&limit=100";
                using var req = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                req.Headers.Add("x-api-key", key);
                var res = await _http.SendAsync(req, ct);

                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    ExtractConnectedSlugs(json, connectedSlugs);
                }

                // 2. If no user-specific accounts found, also check project-wide active connected accounts
                if (connectedSlugs.Count == 0)
                {
                    using var reqAll = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/connected_accounts?statuses=ACTIVE&limit=100");
                    reqAll.Headers.Add("x-api-key", key);
                    var resAll = await _http.SendAsync(reqAll, ct);
                    if (resAll.IsSuccessStatusCode)
                    {
                        var jsonAll = await resAll.Content.ReadAsStringAsync(ct);
                        ExtractConnectedSlugs(jsonAll, connectedSlugs);
                    }
                }
            }
            catch
            {
                // Fall back gracefully so apps are never empty
            }
        }

        // Build list: baseline standard apps + any extra connected toolkits discovered in Composio
        var list = new List<TriggerApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (slug, name) in StandardTriggerApps)
        {
            list.Add(new TriggerApp
            {
                Slug = slug,
                Name = name,
                Connected = connectedSlugs.Contains(slug)
            });
            seen.Add(slug);
        }

        foreach (var slug in connectedSlugs)
        {
            if (!seen.Contains(slug))
            {
                string displayName = char.ToUpperInvariant(slug[0]) + slug[1..];
                list.Add(new TriggerApp
                {
                    Slug = slug,
                    Name = displayName,
                    Connected = true
                });
                seen.Add(slug);
            }
        }

        // Sort: connected apps first, then alphabetically by name
        list.Sort((a, b) =>
        {
            if (a.Connected != b.Connected)
                return b.Connected.CompareTo(a.Connected);
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }

    private static void ExtractConnectedSlugs(string json, HashSet<string> target)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement itemsElement = default;

            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                itemsElement = items;
            }
            else if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                itemsElement = data;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                itemsElement = doc.RootElement;
            }

            if (itemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in itemsElement.EnumerateArray())
                {
                    string? slug = null;

                    if (el.TryGetProperty("toolkit", out var toolkitProp))
                    {
                        if (toolkitProp.ValueKind == JsonValueKind.Object && toolkitProp.TryGetProperty("slug", out var sProp))
                            slug = sProp.GetString();
                        else if (toolkitProp.ValueKind == JsonValueKind.String)
                            slug = toolkitProp.GetString();
                    }

                    if (string.IsNullOrWhiteSpace(slug) && el.TryGetProperty("toolkit_slug", out var tsProp))
                        slug = tsProp.GetString();
                    if (string.IsNullOrWhiteSpace(slug) && el.TryGetProperty("app_name", out var anProp))
                        slug = anProp.GetString();
                    if (string.IsNullOrWhiteSpace(slug) && el.TryGetProperty("appName", out var an2Prop))
                        slug = an2Prop.GetString();
                    if (string.IsNullOrWhiteSpace(slug) && el.TryGetProperty("slug", out var slugProp))
                        slug = slugProp.GetString();
                    if (string.IsNullOrWhiteSpace(slug) && el.TryGetProperty("key", out var keyProp))
                        slug = keyProp.GetString();

                    if (!string.IsNullOrWhiteSpace(slug))
                    {
                        target.Add(slug.Trim().ToLowerInvariant());
                    }
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }
    }

    public async Task<string?> ConnectAppUrlAsync(string slug, CancellationToken ct = default)
    {
        var key = GetApiKey();
        if (string.IsNullOrWhiteSpace(key)) return null;

        var userId = GetUserId();

        try
        {
            // 1. Try to find existing auth config for this toolkit
            string? authConfigId = null;
            using (var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrlV3}/auth_configs?toolkit={Uri.EscapeDataString(slug)}"))
            {
                req.Headers.Add("x-api-key", key);
                var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
                    {
                        authConfigId = items[0].GetProperty("id").GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                    {
                        authConfigId = data[0].GetProperty("id").GetString();
                    }
                }
            }

            // 2. If no auth config found, try to create one with managed auth
            if (string.IsNullOrEmpty(authConfigId))
            {
                using var createReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrlV3}/auth_configs");
                createReq.Headers.Add("x-api-key", key);
                var payload = new
                {
                    toolkit = slug,
                    type = "use_composio_managed_auth",
                    name = $"{slug} (Open Dot)"
                };
                createReq.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var res = await _http.SendAsync(createReq, ct);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        authConfigId = idProp.GetString();
                    }
                }
            }

            // 3. Initiate link session
            if (!string.IsNullOrEmpty(authConfigId))
            {
                using var linkReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/connected_accounts/link");
                linkReq.Headers.Add("x-api-key", key);
                var linkPayload = new
                {
                    auth_config_id = authConfigId,
                    user_id = userId
                };
                linkReq.Content = new StringContent(JsonSerializer.Serialize(linkPayload), System.Text.Encoding.UTF8, "application/json");
                var res = await _http.SendAsync(linkReq, ct);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("redirect_url", out var rUrl) ||
                        doc.RootElement.TryGetProperty("redirectUrl", out rUrl))
                    {
                        var url = rUrl.GetString();
                        if (!string.IsNullOrEmpty(url)) return url;
                    }
                }
            }
        }
        catch
        {
            // Fall through to dashboard fallback
        }

        // Fallback: direct user to Composio platform page to connect the toolkit
        return $"https://platform.composio.dev/apps/{slug}";
    }

    public async Task<List<TriggerType>> GetTriggerTypesAsync(string slug, CancellationToken ct = default)
    {
        var key = GetApiKey();
        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/triggers_types?toolkits={Uri.EscapeDataString(slug)}");
                req.Headers.Add("x-api-key", key);
                var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var types = new List<TriggerType>();
                    JsonElement items = default;
                    if (doc.RootElement.TryGetProperty("items", out var i) && i.ValueKind == JsonValueKind.Array)
                        items = i;
                    else if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array)
                        items = d;

                    if (items.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in items.EnumerateArray())
                        {
                            var tSlug = el.TryGetProperty("slug", out var sp) ? sp.GetString() ?? "" : "";
                            var tName = el.TryGetProperty("name", out var np) ? np.GetString() ?? tSlug : tSlug;
                            var tDesc = el.TryGetProperty("description", out var dp) ? dp.GetString() ?? "" : "";
                            types.Add(new TriggerType { Slug = tSlug, Name = tName, Description = tDesc });
                        }
                        if (types.Count > 0) return types;
                    }
                }
            }
            catch
            {
                // Fallback to default
            }
        }

        return
        [
            new TriggerType
            {
                Slug = $"{slug.ToUpperInvariant()}_NEW_EVENT",
                Name = $"New {slug} event",
                Description = $"Fires when a new event occurs in {slug}",
                Fields = [new TriggerField { Name = "query", Title = "Filter query", Description = "Optional filter", Type = "string", Required = false }]
            }
        ];
    }

    public Task StartListeningAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopListeningAsync(CancellationToken ct = default) => Task.CompletedTask;
}
