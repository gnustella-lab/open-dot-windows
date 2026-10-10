using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenDot.App.Services;
using OpenDot.App.ViewModels;
using OpenDot.Core.Models;

namespace OpenDot.App.Views;

public sealed partial class AppsPage : Page
{
    public AppsViewModel ViewModel { get; }

    public AppsPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Services.GetRequiredService<AppsViewModel>();
        DataContext = ViewModel;

        Loaded += async (_, _) =>
        {
            await ViewModel.LoadAppsAsync();
            AppsGridView.ItemsSource = ViewModel.Apps;
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppsViewModel.StatusMessage))
            {
                StatusMessageText.Text = ViewModel.StatusMessage;
            }
        };
    }

    private async void OnConnectAppClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TriggerApp app)
        {
            await ViewModel.ConnectAppAsync(app);
            StatusMessageText.Text = ViewModel.StatusMessage;
        }
    }
}
