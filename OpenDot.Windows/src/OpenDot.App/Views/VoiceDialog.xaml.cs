using Microsoft.UI.Xaml.Controls;

namespace OpenDot.App.Views;

public sealed partial class VoiceDialog : ContentDialog
{
    public VoiceDialog(string dotName)
    {
        InitializeComponent();
        DotNameText.Text = $"Talking with {dotName}";
        CallStatusText.Text = "Microphone connected · Live call active";
    }
}
