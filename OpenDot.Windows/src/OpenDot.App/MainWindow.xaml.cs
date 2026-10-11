using System.IO;
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
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(AppContext.BaseDirectory, "AppIcon.ico");
            }
            if (File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
                SetWin32Icon(iconPath);
            }
        }
        catch
        {
            // Silently continue if icon cannot be set
        }

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

    private void SetWin32Icon(string iconPath)
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hWnd != IntPtr.Zero)
            {
                IntPtr hIconBig = LoadImage(IntPtr.Zero, iconPath, 1 /* IMAGE_ICON */, 32, 32, 0x0010 /* LR_LOADFROMFILE */);
                IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, 1 /* IMAGE_ICON */, 16, 16, 0x0010 /* LR_LOADFROMFILE */);
                if (hIconBig != IntPtr.Zero)
                {
                    SendMessage(hWnd, 0x0080 /* WM_SETICON */, (IntPtr)1 /* ICON_BIG */, hIconBig);
                    SetClassLongPtr(hWnd, -14 /* GCLP_HICON */, hIconBig);
                }
                if (hIconSmall != IntPtr.Zero)
                {
                    SendMessage(hWnd, 0x0080 /* WM_SETICON */, (IntPtr)0 /* ICON_SMALL */, hIconSmall);
                    SetClassLongPtr(hWnd, -34 /* GCLP_HICONSM */, hIconSmall);
                }
            }
        }
        catch
        {
            // Fallback gracefully
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLong")]
    private static extern IntPtr SetClassLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size > 4)
            return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
        else
            return SetClassLong32(hWnd, nIndex, dwNewLong);
    }
}
