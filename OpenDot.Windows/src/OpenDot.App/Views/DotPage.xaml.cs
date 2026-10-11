using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using OpenDot.App.Services;
using OpenDot.App.ViewModels;
using OpenDot.Core.Models;
using Windows.System;

namespace OpenDot.App.Views;

public sealed partial class DotPage : Page
{
    public DotViewModel ViewModel { get; }

    public DotPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Services.GetRequiredService<DotViewModel>();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DotViewModel.Messages))
            {
                MessagesListView.ItemsSource = ViewModel.Messages;
            }
            else if (e.PropertyName == nameof(DotViewModel.ScreenBitmap))
            {
                ScreenImage.Source = ViewModel.ScreenBitmap;
            }
            else if (e.PropertyName == nameof(DotViewModel.ComputerStatus))
            {
                ComputerStatusText.Text = ViewModel.ComputerStatus;
            }
            else if (e.PropertyName == nameof(DotViewModel.WorkspaceFiles))
            {
                WorkspaceFilesListView.ItemsSource = ViewModel.WorkspaceFiles;
            }
            else if (e.PropertyName == nameof(DotViewModel.Rules))
            {
                RulesListView.ItemsSource = ViewModel.Rules;
            }
            else if (e.PropertyName == nameof(DotViewModel.Dot))
            {
                UpdateHeader();
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Dot dot)
        {
            ViewModel.SetDot(dot);
            UpdateHeader();
            PopulateSetup();
            MessagesListView.ItemsSource = ViewModel.Messages;
            WorkspaceFilesListView.ItemsSource = ViewModel.WorkspaceFiles;
            RulesListView.ItemsSource = ViewModel.Rules;
        }
    }

    private void UpdateHeader()
    {
        if (ViewModel.Dot == null) return;
        DotNameText.Text = ViewModel.Dot.Name;
        DotPurposeText.Text = ViewModel.Dot.Purpose;
        HeaderAvatar.DotLook = ViewModel.Dot.Look;
        HeaderAvatar.IsWorking = ViewModel.Dot.Status == "working";

        StatusBadgeText.Text = ViewModel.Dot.Status;
        PauseButton.Content = ViewModel.Dot.Status == "paused" ? "Resume" : "Pause";
    }

    private void PopulateSetup()
    {
        if (ViewModel.Dot == null) return;
        SetupNameInput.Text = ViewModel.Dot.Name;
        SetupPurposeInput.Text = ViewModel.Dot.Purpose;
        SetupInstructionsInput.Text = ViewModel.Dot.Instructions;
        SetupModelInput.Text = ViewModel.Dot.Model ?? string.Empty;
        SetupLocalAccessSwitch.IsOn = ViewModel.Dot.LocalAccess;
    }

    public static Visibility IsUserVisibility(string role) => role == "user" ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility IsDotVisibility(string role) => role == "dot" ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility IsActivityVisibility(string role) => role == "activity" ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility IsApprovalCardVisibility(CardData? card) => card?.Kind == "approval" ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility IsQuestionCardVisibility(CardData? card) => card?.Kind == "question" ? Visibility.Visible : Visibility.Collapsed;

    private async void OnSendClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.InputMessage = ChatInputBox.Text;
        ChatInputBox.Text = string.Empty;
        await ViewModel.SendMessageAsync();
    }

    private void OnChatInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            OnSendClicked(sender, e);
        }
    }

    private void OnApprovalChoice(string messageId, string choice)
    {
        _ = ViewModel.ResolveCardAsync(new CardResolutionArgs(messageId, choice));
    }

    private void OnQuestionAnswer(string messageId, string answer)
    {
        _ = ViewModel.ResolveCardAsync(new CardResolutionArgs(messageId, "answer", answer));
    }

    private void OnPauseClicked(object sender, RoutedEventArgs e) => ViewModel.TogglePause();
    private void OnStopClicked(object sender, RoutedEventArgs e) => ViewModel.Stop();

    private async void OnVoiceCallClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new VoiceDialog(ViewModel.Dot?.Name ?? "Dot") { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    // ---------- Computer Tab ----------
    private async void OnGoUrlClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.BrowserUrl = BrowserUrlInput.Text;
        await ViewModel.NavigateBrowserAsync();
    }

    private void OnBrowserUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            OnGoUrlClicked(sender, e);
        }
    }

    private async void OnRefreshScreenshotClicked(object sender, RoutedEventArgs e) => await ViewModel.CaptureScreenshotAsync();
    private async void OnWakeClicked(object sender, RoutedEventArgs e) => await ViewModel.NavigateBrowserAsync();
    private async void OnSleepClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Dot != null)
        {
            var comp = AppServices.Services.GetRequiredService<OpenDot.Core.Interfaces.IComputerManager>();
            await comp.SleepAsync(ViewModel.Dot.Id);
            ComputerStatusText.Text = "Browser put to sleep.";
        }
    }

    private async void OnResetComputerClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Dot != null)
        {
            var comp = AppServices.Services.GetRequiredService<OpenDot.Core.Interfaces.IComputerManager>();
            await comp.ResetAsync(ViewModel.Dot.Id);
            ComputerStatusText.Text = "Browser reset.";
        }
    }

    // ---------- Files Tab ----------
    private async void OnRefreshFilesClicked(object sender, RoutedEventArgs e) => await ViewModel.RefreshFilesAsync();
    private void OnOpenWorkspaceFolderClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Dot == null) return;
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDot", "dots", ViewModel.Dot.Id, "workspace");
        Directory.CreateDirectory(baseDir);
        System.Diagnostics.Process.Start("explorer.exe", baseDir);
    }

    // ---------- Setup Tab ----------
    private void OnSaveIdentityClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Dot == null) return;
        ViewModel.Dot.Name = SetupNameInput.Text;
        ViewModel.Dot.Purpose = SetupPurposeInput.Text;
        ViewModel.Dot.Instructions = SetupInstructionsInput.Text;
        ViewModel.Dot.Model = string.IsNullOrWhiteSpace(SetupModelInput.Text) ? null : SetupModelInput.Text.Trim();
        ViewModel.Dot.LocalAccess = SetupLocalAccessSwitch.IsOn;
        ViewModel.SaveDotSetup();
        UpdateHeader();
    }

    private void OnAddRuleClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NewRuleInput.Text))
        {
            ViewModel.AddRule(NewRuleInput.Text.Trim());
            NewRuleInput.Text = string.Empty;
        }
    }

    private void OnDeleteRuleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ruleId)
        {
            ViewModel.DeleteRule(ruleId);
        }
    }

    private void OnDeleteDotClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Dot == null) return;
        var repo = AppServices.Services.GetRequiredService<OpenDot.Core.Interfaces.IDotRepository>();
        repo.DeleteDot(ViewModel.Dot.Id);

        var mainVm = AppServices.Services.GetRequiredService<MainViewModel>();
        mainVm.LoadData();

        Frame.Navigate(typeof(HomePage));
    }
}
