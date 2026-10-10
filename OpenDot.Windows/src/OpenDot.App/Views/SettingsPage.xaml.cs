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
        };

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
        };
    }

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
