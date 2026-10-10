using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenDot.App.Services;
using OpenDot.App.ViewModels;
using OpenDot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace OpenDot.App.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        InitializeComponent();
        ViewModel = AppServices.Services.GetRequiredService<HomeViewModel>();
        DataContext = ViewModel;

        Loaded += (_, _) =>
        {
            ViewModel.Refresh();
            TotalDotsText.Text = ViewModel.TotalDots.ToString();
            ActiveRoutinesText.Text = ViewModel.ActiveRoutines.ToString();
            DotsGridView.ItemsSource = ViewModel.RecentDots;
        };
    }

    private void OnDotClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Dot dot)
        {
            var mainVm = AppServices.Services.GetRequiredService<MainViewModel>();
            mainVm.SelectDot(dot);

            // Navigate to DotPage
            Frame.Navigate(typeof(DotPage), dot);
        }
    }

    private async void OnCreateDotClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new NewDotDialog { XamlRoot = XamlRoot };
        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary && dialog.CreatedDot != null)
        {
            ViewModel.Refresh();
            DotsGridView.ItemsSource = ViewModel.RecentDots;
            Frame.Navigate(typeof(DotPage), dialog.CreatedDot);
        }
    }
}
