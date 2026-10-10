using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenDot.Core.Models;
using Windows.UI;

namespace OpenDot.App.Controls;

public sealed partial class DotAvatarControl : UserControl
{
    public static readonly DependencyProperty DotLookProperty =
        DependencyProperty.Register(nameof(DotLook), typeof(Look), typeof(DotAvatarControl), new PropertyMetadata(null, OnLookChanged));

    public static readonly DependencyProperty AvatarSizeProperty =
        DependencyProperty.Register(nameof(AvatarSize), typeof(double), typeof(DotAvatarControl), new PropertyMetadata(48.0));

    public static readonly DependencyProperty IsWorkingProperty =
        DependencyProperty.Register(nameof(IsWorking), typeof(bool), typeof(DotAvatarControl), new PropertyMetadata(false));

    public Look? DotLook
    {
        get => (Look?)GetValue(DotLookProperty);
        set => SetValue(DotLookProperty, value);
    }

    public double AvatarSize
    {
        get => (double)GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    public bool IsWorking
    {
        get => (bool)GetValue(IsWorkingProperty);
        set => SetValue(IsWorkingProperty, value);
    }

    public double ScaleFactor => AvatarSize / 64.0;

    public Brush BodyBrush => CreateBrush(DotLook?.Color ?? "#9fcbff");
    public Brush AccentBrush => CreateBrush(DotLook?.Accent ?? "#2a6fdb");
    public Brush EyeBrush => CreateBrush(DotLook?.EyeColor ?? "#3b3842");

    public Visibility AccessoryVisibility =>
        DotLook?.Accessory != null && DotLook.Accessory != "none" ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkingPulseVisibility =>
        IsWorking ? Visibility.Visible : Visibility.Collapsed;

    public DotAvatarControl()
    {
        InitializeComponent();
    }

    private static void OnLookChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        try
        {
            var cleaned = hex.Trim().TrimStart('#');
            if (cleaned.Length == 6)
            {
                byte r = Convert.ToByte(cleaned[..2], 16);
                byte g = Convert.ToByte(cleaned[2..4], 16);
                byte b = Convert.ToByte(cleaned[4..6], 16);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            if (cleaned.Length == 8)
            {
                byte a = Convert.ToByte(cleaned[..2], 16);
                byte r = Convert.ToByte(cleaned[2..4], 16);
                byte g = Convert.ToByte(cleaned[4..6], 16);
                byte b = Convert.ToByte(cleaned[6..8], 16);
                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
        }
        catch { }
        return new SolidColorBrush(Color.FromArgb(255, 159, 203, 255));
    }
}
