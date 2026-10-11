using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;
using OpenDot.Integrations.OpenAI;

namespace OpenDot.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IDotRepository _repo;
    private readonly ISecureVault _vault;
    private readonly CodexAuthService _codexAuth;
    private CancellationTokenSource? _loginCancellation;

    [ObservableProperty]
    private string _openAiAuthMode = "api_key";

    [ObservableProperty]
    private string _codexStatus = "Not signed in.";

    [ObservableProperty]
    private bool _isSigningIn;

    [ObservableProperty]
    private string _openAiApiKey = string.Empty;

    [ObservableProperty]
    private string _openRouterApiKey = string.Empty;

    [ObservableProperty]
    private string _composioApiKey = string.Empty;

    [ObservableProperty]
    private string _defaultModel = "gpt-4o";

    [ObservableProperty]
    private string _computerMode = "local";

    [ObservableProperty]
    private ObservableCollection<PasswordEntry> _passwords = [];

    [ObservableProperty]
    private string _newSite = string.Empty;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(IDotRepository repo, ISecureVault vault, CodexAuthService codexAuth)
    {
        _repo = repo;
        _vault = vault;
        _codexAuth = codexAuth;
        LoadSettings();
    }

    [RelayCommand]
    public void LoadSettings()
    {
        OpenAiAuthMode = _repo.GetSetting("openai_auth_mode") == "codex" ? "codex" : "api_key";
        RefreshCodexStatus();

        var sealedOpenAi = _repo.GetSetting("openai_key");
        OpenAiApiKey = !string.IsNullOrEmpty(sealedOpenAi) ? _vault.Unseal(sealedOpenAi) : "";

        var sealedOpenRouter = _repo.GetSetting("openrouter_key");
        OpenRouterApiKey = !string.IsNullOrEmpty(sealedOpenRouter) ? _vault.Unseal(sealedOpenRouter) : "";

        var sealedComposio = _repo.GetSetting("composio_api_key");
        ComposioApiKey = !string.IsNullOrEmpty(sealedComposio) ? _vault.Unseal(sealedComposio) : "";

        DefaultModel = _repo.GetSetting("default_model") ?? "gpt-4o";
        ComputerMode = _repo.GetSetting("computer_mode") ?? "local";

        LoadPasswords();
    }

    public void LoadPasswords()
    {
        var list = _vault.ListPasswords();
        Passwords.Clear();
        foreach (var p in list) Passwords.Add(p);
    }

    [RelayCommand]
    public void SaveSettings()
    {
        if (IsSigningIn) return;
        if (OpenAiAuthMode == "codex" && !_codexAuth.IsSignedIn)
        {
            StatusMessage = "Sign in with ChatGPT / Codex before selecting this authentication method.";
            return;
        }

        var previousMode = _repo.GetSetting("openai_auth_mode") ?? "api_key";
        if (OpenAiAuthMode != previousMode)
        {
            DefaultModel = OpenAiAuthMode == "codex" ? CodexAiClient.DefaultModel : "gpt-4o";
        }
        _repo.SetSetting("openai_auth_mode", OpenAiAuthMode);

        if (!string.IsNullOrWhiteSpace(OpenAiApiKey))
            _repo.SetSetting("openai_key", _vault.Seal(OpenAiApiKey.Trim()));
        else
            _repo.SetSetting("openai_key", null);

        if (!string.IsNullOrWhiteSpace(OpenRouterApiKey))
            _repo.SetSetting("openrouter_key", _vault.Seal(OpenRouterApiKey.Trim()));
        else
            _repo.SetSetting("openrouter_key", null);

        if (!string.IsNullOrWhiteSpace(ComposioApiKey))
            _repo.SetSetting("composio_api_key", _vault.Seal(ComposioApiKey.Trim()));
        else
            _repo.SetSetting("composio_api_key", null);

        _repo.SetSetting("default_model", DefaultModel);
        _repo.SetSetting("computer_mode", ComputerMode);

        StatusMessage = "Settings saved securely to Windows DPAPI.";
    }

    public async Task SignInCodexAsync()
    {
        if (_loginCancellation != null) return;
        using var cancellation = new CancellationTokenSource();
        _loginCancellation = cancellation;
        IsSigningIn = true;
        CodexStatus = "Complete the ChatGPT / Codex sign-in in your browser.";
        try
        {
            await _codexAuth.SignInAsync(cancellation.Token);
            OpenAiAuthMode = "codex";
            DefaultModel = CodexAiClient.DefaultModel;
            _repo.SetSetting("openai_auth_mode", OpenAiAuthMode);
            _repo.SetSetting("default_model", DefaultModel);
            StatusMessage = "ChatGPT / Codex selected. Dots without a custom model now use the Codex default.";
            RefreshCodexStatus();
        }
        catch (OperationCanceledException)
        {
            RefreshCodexStatus();
            StatusMessage = "Codex sign-in cancelled.";
        }
        catch (Exception ex)
        {
            CodexStatus = $"Codex sign-in failed: {ex.Message}";
        }
        finally
        {
            _loginCancellation = null;
            IsSigningIn = false;
        }
    }

    public void CancelCodexSignIn() => _loginCancellation?.Cancel();

    public void SignOutCodex()
    {
        if (IsSigningIn) return;
        try
        {
            _codexAuth.SignOut();
            RefreshCodexStatus();
            StatusMessage = "Disconnected from OpenDot only. Select API key and save to use API billing; no automatic fallback.";
        }
        catch (Exception ex)
        {
            CodexStatus = $"Could not disconnect Codex: {ex.Message}";
        }
    }

    private void RefreshCodexStatus()
    {
        try
        {
            CodexStatus = _codexAuth.IsSignedIn
                ? $"Signed in: {_codexAuth.AccountLabel ?? "ChatGPT / Codex"}"
                : "Not signed in.";
        }
        catch (Exception)
        {
            CodexStatus = "Stored Codex credentials could not be read. Disconnect and sign in again.";
        }
    }

    [RelayCommand]
    public void AddPassword()
    {
        if (string.IsNullOrWhiteSpace(NewSite) || string.IsNullOrWhiteSpace(NewPassword)) return;

        _vault.SavePassword(NewSite.Trim(), NewUsername.Trim(), NewPassword.Trim());
        NewSite = string.Empty;
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        LoadPasswords();
        StatusMessage = "Password encrypted and stored in vault.";
    }

    [RelayCommand]
    public void DeletePassword(string id)
    {
        _vault.DeletePassword(id);
        LoadPasswords();
    }
}
