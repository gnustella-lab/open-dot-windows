using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IDotRepository _repo;
    private readonly ISecureVault _vault;

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

    public SettingsViewModel(IDotRepository repo, ISecureVault vault)
    {
        _repo = repo;
        _vault = vault;
        LoadSettings();
    }

    [RelayCommand]
    public void LoadSettings()
    {
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
