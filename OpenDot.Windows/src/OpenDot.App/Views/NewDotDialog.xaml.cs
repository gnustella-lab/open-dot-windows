using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using OpenDot.App.Services;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.App.Views;

public sealed partial class NewDotDialog : ContentDialog
{
    private readonly Look _look = new();
    public Dot? CreatedDot { get; private set; }

    public NewDotDialog()
    {
        InitializeComponent();
        AvatarPreview.DotLook = _look;
        ColorsGridView.ItemsSource = Look.Colors;
        ColorsGridView.SelectedIndex = 6; // Default pleasant pastel blue
    }

    private void OnColorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ColorsGridView.SelectedItem is string colorHex)
        {
            _look.Color = colorHex;
            AvatarPreview.DotLook = _look;
        }
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var name = DotNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            args.Cancel = true;
            return;
        }

        var repo = AppServices.Services.GetRequiredService<IDotRepository>();
        CreatedDot = repo.CreateDot(
            name,
            DotPurposeInput.Text.Trim(),
            DotInstructionsInput.Text.Trim(),
            _look,
            LocalAccessCheckBox.IsChecked ?? false
        );

        var mainVm = AppServices.Services.GetRequiredService<ViewModels.MainViewModel>();
        mainVm.LoadData();
    }
}
