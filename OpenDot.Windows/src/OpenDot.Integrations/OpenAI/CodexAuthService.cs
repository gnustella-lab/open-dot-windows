using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenDot.Core.Interfaces;

namespace OpenDot.Integrations.OpenAI;

public sealed class CodexAuthService
{
    // Public native-app client and PKCE flow used by openai/codex's login crate.
    private const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    private const string TokenEndpoint = "https://auth.openai.com/oauth/token";
    private const int PrimaryPort = 1455;
    private const int FallbackPort = 1457;
    private const string SessionSetting = "openai_codex_session";
    internal const string Originator = "opendot_windows";
    private readonly HttpClient _http;
    private readonly IDotRepository _repo;
    private readonly ISecureVault _vault;
    private readonly SemaphoreSlim _authGate = new(1, 1);
    private readonly object _stateLock = new();
    private Session? _session;
    private string? _sessionError;
    private long _generation;
    private CancellationTokenSource? _activeLogin;
    private CancellationTokenSource _sessionLifetime = new();

    public CodexAuthService(HttpClient http, IDotRepository repo, ISecureVault vault)
    {
        _http = http;
        _repo = repo;
        _vault = vault;
        var encrypted = repo.GetSetting(SessionSetting) ?? repo.GetSetting("chatgpt_oauth_session");
        if (string.IsNullOrWhiteSpace(encrypted)) return;
        try
        {
            _session = JsonSerializer.Deserialize<Session>(vault.Unseal(encrypted));
            if (_session == null || string.IsNullOrWhiteSpace(_session.AccessToken)
                || string.IsNullOrWhiteSpace(_session.RefreshToken) || string.IsNullOrWhiteSpace(_session.AccountId))
                throw new FormatException();
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException)
        {
            _session = null;
            _sessionError = "The saved Codex session could not be decrypted. Sign in again in Settings.";
        }
    }

    public bool IsSignedIn { get { lock (_stateLock) return _session != null; } }
    public string? AccountLabel { get { lock (_stateLock) return _session?.Email ?? _session?.AccountId; } }
    public string? SessionError { get { lock (_stateLock) return _sessionError; } }

    public async Task SignInAsync(CancellationToken ct = default)
    {
        if (!await _authGate.WaitAsync(0, ct).ConfigureAwait(false))
            throw new InvalidOperationException("A Codex sign-in or token refresh is already in progress. Try again when it finishes.");

        using var login = CancellationTokenSource.CreateLinkedTokenSource(ct);
        login.CancelAfter(TimeSpan.FromMinutes(5));
        long generation;
        lock (_stateLock)
        {
            generation = _generation;
            _activeLogin = login;
        }
        try
        {
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var state = Base64Url(RandomNumberGenerator.GetBytes(32));
            TcpListener listener;
            int port = PrimaryPort;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, PrimaryPort);
                listener.Server.ExclusiveAddressUse = true;
                listener.Start();
            }
            catch (SocketException)
            {
                try
                {
                    port = FallbackPort;
                    listener = new TcpListener(IPAddress.Loopback, FallbackPort);
                    listener.Server.ExclusiveAddressUse = true;
                    listener.Start();
                }
                catch (SocketException)
                {
                    throw new InvalidOperationException("Codex sign-in needs local port 1455 or 1457. Close another Codex login window or the app using that port, then try again.");
                }
            }

            using (listener)
            {
                var redirectUri = $"http://127.0.0.1:{port}/auth/callback";
                var parameters = new Dictionary<string, string>
                {
                    ["response_type"] = "code",
                    ["client_id"] = ClientId,
                    ["redirect_uri"] = redirectUri,
                    ["scope"] = "openid profile email offline_access",
                    ["code_challenge"] = challenge,
                    ["code_challenge_method"] = "S256",
                    ["state"] = state,
                    ["id_token_add_organizations"] = "true",
                    ["codex_cli_simplified_flow"] = "true",
                    ["originator"] = Originator
                };
                var url = "https://auth.openai.com/oauth/authorize?" + string.Join("&", parameters.Select(p =>
                    $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
                login.Token.ThrowIfCancellationRequested();
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    throw new InvalidOperationException("The default browser could not be opened. Configure a browser in Windows and try Codex sign-in again.");
                }

                var code = await WaitForCodeAsync(listener, state, redirectUri, port, login.Token).ConfigureAwait(false);
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = ClientId,
                    ["code"] = code,
                    ["code_verifier"] = verifier,
                    ["redirect_uri"] = redirectUri
                });
                var session = await ExchangeAsync(content, null, login.Token).ConfigureAwait(false);
                SaveSession(session, generation, login.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !IsGenerationChanged(generation))
        {
            throw new TimeoutException("Codex sign-in timed out after five minutes. Try signing in again.");
        }
        finally
        {
            lock (_stateLock) _activeLogin = null;
            _authGate.Release();
        }
    }

    public void SignOut()
    {
        lock (_stateLock)
        {
            // Persist deletion before reporting success; never touch the Codex CLI's own credentials.
            _repo.SetSetting(SessionSetting, null);
            _generation++;
            _session = null;
            _sessionError = null;
            _activeLogin?.Cancel();
            _sessionLifetime.Cancel();
            _sessionLifetime.Dispose();
            _sessionLifetime = new CancellationTokenSource();
        }
    }

    internal async Task<AccessToken> GetAccessTokenAsync(CancellationToken ct)
    {
        await _authGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Session session;
            long generation;
            CancellationToken lifetime;
            lock (_stateLock)
            {
                session = _session ?? throw new InvalidOperationException(_sessionError ?? "Sign in with ChatGPT (Codex) in Settings first.");
                generation = _generation;
                lifetime = _sessionLifetime.Token;
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime);
            if (session.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
            {
                using var content = JsonContent.Create(new
                {
                    grant_type = "refresh_token",
                    client_id = ClientId,
                    refresh_token = session.RefreshToken
                });
                session = await ExchangeAsync(content, session, linked.Token).ConfigureAwait(false);
                SaveSession(session, generation, linked.Token);
            }
            linked.Token.ThrowIfCancellationRequested();
            return new AccessToken(session.AccessToken, session.AccountId, lifetime);
        }
        finally { _authGate.Release(); }
    }

    private bool IsGenerationChanged(long generation)
    {
        lock (_stateLock) return generation != _generation;
    }

    private void SaveSession(Session session, long generation, CancellationToken ct)
    {
        lock (_stateLock)
        {
            ct.ThrowIfCancellationRequested();
            if (generation != _generation) throw new OperationCanceledException("Codex sign-in was disconnected.");
            try { _repo.SetSetting(SessionSetting, _vault.Seal(JsonSerializer.Serialize(session))); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException("The Codex session could not be saved securely. Check local storage and sign in again.");
            }
            _session = session;
            _sessionError = null;
        }
    }

    private async Task<Session> ExchangeAsync(HttpContent content, Session? previous, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) { Content = content };
            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var action = previous == null ? "sign-in" : "session refresh";
                throw new InvalidOperationException($"Codex {action} failed (HTTP {(int)response.StatusCode}). Sign in again in Settings; if it persists, check your connection and ChatGPT account access.");
            }
            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            var root = json.RootElement;
            var access = StringProperty(root, "access_token") ?? throw new FormatException();
            var refresh = StringProperty(root, "refresh_token") ?? previous?.RefreshToken ?? throw new FormatException();
            using var accessClaims = ReadJwt(access);
            var idToken = StringProperty(root, "id_token");
            using var idClaims = idToken == null ? null : ReadJwt(idToken);
            var account = AccountId(accessClaims.RootElement) ?? (idClaims == null ? null : AccountId(idClaims.RootElement))
                ?? previous?.AccountId ?? throw new FormatException();
            if (previous != null && account != previous.AccountId)
                throw new InvalidOperationException("The refreshed Codex account changed. Disconnect and sign in again in Settings.");
            var email = idClaims == null ? Email(accessClaims.RootElement) : Email(idClaims.RootElement);
            DateTimeOffset expiresAt;
            if (root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt64(out var seconds) && seconds > 0)
                expiresAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
            else if (accessClaims.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var unixTime))
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixTime);
            else throw new FormatException();
            if (expiresAt <= DateTimeOffset.UtcNow) throw new FormatException();
            return new Session { AccessToken = access, RefreshToken = refresh, AccountId = account, Email = email ?? previous?.Email, ExpiresAt = expiresAt };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The Codex authentication server did not respond in time. Try again.");
        }
        catch (HttpRequestException)
        {
            throw new HttpRequestException("Could not reach the Codex authentication server. Check your connection and try again.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentOutOfRangeException)
        {
            throw new InvalidOperationException("The Codex authentication server returned an invalid session. Sign in again.");
        }
    }

    private static async Task<string> WaitForCodeAsync(TcpListener listener, string expectedState, string redirectUri, int expectedPort, CancellationToken ct)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connection.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                using var stream = client.GetStream();
                var buffer = new byte[8192];
                var length = 0;
                var headersEnd = -1;
                while (length < buffer.Length && headersEnd < 0)
                {
                    var count = await stream.ReadAsync(buffer.AsMemory(length), connection.Token).ConfigureAwait(false);
                    if (count == 0) break;
                    length += count;
                    headersEnd = Encoding.ASCII.GetString(buffer, 0, length).IndexOf("\r\n\r\n", StringComparison.Ordinal);
                }
                var request = Encoding.ASCII.GetString(buffer, 0, length);
                var requestLineEnd = request.IndexOf("\r\n", StringComparison.Ordinal);
                var parts = requestLineEnd < 0 ? [] : request[..requestLineEnd].Split(' ');
                if (headersEnd < 0 || parts.Length != 3 || parts[0] != "GET" || !parts[1].StartsWith('/'))
                {
                    await ReplyAsync(stream, 400, "Invalid callback request.", connection.Token).ConfigureAwait(false);
                    continue;
                }
                if (!Uri.TryCreate(new Uri(redirectUri), parts[1], out var uri)
                    || uri.Host != "127.0.0.1" || uri.Port != expectedPort || uri.AbsolutePath != "/auth/callback")
                {
                    await ReplyAsync(stream, 404, "Not found.", connection.Token).ConfigureAwait(false);
                    continue;
                }
                var query = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var pair = part.Split('=', 2);
                    var key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
                    var value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "";
                    if (!query.TryAdd(key, value)) throw new InvalidOperationException("Codex returned duplicate login parameters. Sign in again.");
                }
                if (!query.TryGetValue("state", out var state) || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(expectedState)))
                {
                    await ReplyAsync(stream, 400, "Invalid login state. Return to OpenDot and sign in again.", connection.Token).ConfigureAwait(false);
                    throw new InvalidOperationException("Codex login state did not match. Sign in again in Settings.");
                }
                if (query.ContainsKey("error"))
                {
                    await ReplyAsync(stream, 400, "Authorization was not granted. Return to OpenDot.", connection.Token).ConfigureAwait(false);
                    throw new InvalidOperationException("ChatGPT authorization was declined or failed. Sign in again if you want to connect Codex.");
                }
                if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
                    throw new InvalidOperationException("Codex returned no authorization code. Sign in again.");
                await ReplyAsync(stream, 200, "Authorization received. Return to OpenDot to finish signing in. You can close this window.", connection.Token).ConfigureAwait(false);
                return code;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // An idle local connection must not consume the whole five-minute login window.
            }
            catch (IOException) when (!ct.IsCancellationRequested)
            {
                // A disconnected browser connection is not a valid callback; keep waiting.
            }
        }
    }

    private static async Task ReplyAsync(NetworkStream stream, int status, string message, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes($"<!doctype html><html><head><meta charset=\"utf-8\"><title>OpenDot Codex sign-in</title></head><body><p>{message}</p></body></html>");
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(body, ct).ConfigureAwait(false);
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static JsonDocument ReadJwt(string token)
    {
        // Claims are metadata from the HTTPS token exchange, not a local authentication decision.
        var parts = token.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace)) throw new FormatException();
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        return JsonDocument.Parse(Convert.FromBase64String(payload));
    }

    private static string? AccountId(JsonElement claims) => claims.TryGetProperty("https://api.openai.com/auth", out var auth)
        ? StringProperty(auth, "chatgpt_account_id") : null;

    private static string? Email(JsonElement claims) => StringProperty(claims, "email")
        ?? (claims.TryGetProperty("https://api.openai.com/profile", out var profile) ? StringProperty(profile, "email") : null);

    private static string? StringProperty(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(property.GetString()) ? property.GetString() : null;

    private sealed class Session
    {
        public Session() { }
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string AccountId { get; set; } = "";
        public string? Email { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    internal sealed class AccessToken(string value, string accountId, CancellationToken lifetime)
    {
        internal string Value { get; } = value;
        internal string AccountId { get; } = accountId;
        internal CancellationToken Lifetime { get; } = lifetime;
    }
}
