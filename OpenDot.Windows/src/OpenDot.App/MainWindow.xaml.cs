using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenDot.App.Services;
using OpenDot.App.ViewModels;
using OpenDot.App.Views;
using OpenDot.Core.Models;

namespace OpenDot.App;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Open Dot for Windows";

        _viewModel = AppServices.Services.GetRequiredService<MainViewModel>();

        // Set default window size
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1320, 880));

        // Initial navigation
        ContentFrame.Navigate(typeof(HomePage));

        _viewModel.Dots.CollectionChanged += (_, _) => RebuildDotMenuItems();
        RebuildDotMenuItems();
    }

    private void RebuildDotMenuItems()
    {
        // Keep standard items and inject Dot items
        var toRemove = MainNavView.MenuItems.OfType<NavigationViewItem>().Where(i => i.Tag is Dot).ToList();
        foreach (var item in toRemove) MainNavView.MenuItems.Remove(item);

        var insertIndex = 3; // Right after "+ New Dot"
        foreach (var dot in _viewModel.Dots)
        {
            var item = new NavigationViewItem
            {
                Content = dot.Name,
                Tag = dot,
                Icon = new FontIcon { Glyph = "\uE77B" }
            };
            MainNavView.MenuItems.Insert(insertIndex++, item);
        }
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag == "home")
            {
                ContentFrame.Navigate(typeof(HomePage));
            }
            else if (tag == "channels")
            {
                ContentFrame.Navigate(typeof(ChannelsPage));
            }
            else if (tag == "apps")
            {
                ContentFrame.Navigate(typeof(AppsPage));
            }
            else if (item.Tag is Dot dot)
            {
                ContentFrame.Navigate(typeof(DotPage), dot);
            }
        }
    }

    private async void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag?.ToString() == "new_dot")
        {
            var dialog = new NewDotDialog { XamlRoot = Content.XamlRoot };
            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary && dialog.CreatedDot != null)
            {
                ContentFrame.Navigate(typeof(DotPage), dialog.CreatedDot);
            }
        }
    }
}
