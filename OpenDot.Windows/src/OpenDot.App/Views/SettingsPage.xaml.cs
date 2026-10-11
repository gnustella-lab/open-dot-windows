using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenDot.App.Services;
using OpenDot.App.ViewModels;

namespace OpenDot.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Services.GetRequiredService<SettingsViewModel>();
        DataContext = ViewModel;

        Loaded += (_, _) =>
        {
            ViewModel.LoadSettings();
            OpenAiKeyInput.Password = ViewModel.OpenAiApiKey;
            OpenRouterKeyInput.Password = ViewModel.OpenRouterApiKey;
            ComposioKeyInput.Password = ViewModel.ComposioApiKey;
            DefaultModelInput.Text = ViewModel.DefaultModel;
            ComputerModeInput.Text = ViewModel.ComputerMode;
            PasswordsListView.ItemsSource = ViewModel.Passwords;
            UpdateCodexControls();
        };
        Unloaded += (_, _) => ViewModel.CancelCodexSignIn();

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.StatusMessage))
            {
                StatusMessageText.Text = ViewModel.StatusMessage;
            }
            else if (e.PropertyName == nameof(SettingsViewModel.Passwords))
            {
                PasswordsListView.ItemsSource = ViewModel.Passwords;
            }
            else if (e.PropertyName == nameof(SettingsViewModel.CodexStatus) ||
                     e.PropertyName == nameof(SettingsViewModel.IsSigningIn) ||
                     e.PropertyName == nameof(SettingsViewModel.OpenAiAuthMode))
            {
                UpdateCodexControls();
            }
            else if (e.PropertyName == nameof(SettingsViewModel.DefaultModel))
            {
                DefaultModelInput.Text = ViewModel.DefaultModel;
            }
        };
    }

    private void UpdateCodexControls()
    {
        CodexStatusText.Text = ViewModel.CodexStatus;
        OpenAiAuthModeInput.SelectedIndex = ViewModel.OpenAiAuthMode == "codex" ? 1 : 0;
        OpenAiAuthModeInput.IsEnabled = !ViewModel.IsSigningIn;
        SaveAuthenticationButton.IsEnabled = !ViewModel.IsSigningIn;
        CodexSignInButton.IsEnabled = !ViewModel.IsSigningIn;
        CodexSignOutButton.IsEnabled = !ViewModel.IsSigningIn;
        CodexCancelButton.IsEnabled = ViewModel.IsSigningIn;
    }

    private void OnAuthModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && OpenAiAuthModeInput.SelectedItem is ComboBoxItem item)
            ViewModel.OpenAiAuthMode = (string)item.Tag;
    }

    private async void OnCodexSignInClicked(object sender, RoutedEventArgs e)
        => await ViewModel.SignInCodexAsync();

    private void OnCodexCancelClicked(object sender, RoutedEventArgs e) => ViewModel.CancelCodexSignIn();

    private void OnCodexSignOutClicked(object sender, RoutedEventArgs e) => ViewModel.SignOutCodex();

    private void OnSaveSettingsClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenAiApiKey = OpenAiKeyInput.Password;
        ViewModel.OpenRouterApiKey = OpenRouterKeyInput.Password;
        ViewModel.ComposioApiKey = ComposioKeyInput.Password;
        ViewModel.SaveSettings();
    }

    private void OnSaveModelPrefsClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.DefaultModel = DefaultModelInput.Text;
        ViewModel.ComputerMode = ComputerModeInput.Text;
        ViewModel.SaveSettings();
    }

    private void OnAddPasswordClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.NewSite = SiteInput.Text;
        ViewModel.NewUsername = UsernameInput.Text;
        ViewModel.NewPassword = PasswordInput.Password;
        ViewModel.AddPassword();

        SiteInput.Text = string.Empty;
        UsernameInput.Text = string.Empty;
        PasswordInput.Password = string.Empty;
    }

    private void OnDeletePasswordClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            ViewModel.DeletePassword(id);
        }
    }
}
