using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenDot.Core.Models;

namespace OpenDot.App.Controls;

public sealed partial class ApprovalCardControl : UserControl
{
    public static readonly DependencyProperty CardProperty =
        DependencyProperty.Register(nameof(Card), typeof(CardData), typeof(ApprovalCardControl), new PropertyMetadata(null, OnCardChanged));

    public static readonly DependencyProperty MessageIdProperty =
        DependencyProperty.Register(nameof(MessageId), typeof(string), typeof(ApprovalCardControl), new PropertyMetadata(string.Empty));

    public event Action<string, string>? OnChoice;

    public CardData? Card
    {
        get => (CardData?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    public string MessageId
    {
        get => (string)GetValue(MessageIdProperty);
        set => SetValue(MessageIdProperty, value);
    }

    public string ActionTitle => Card?.Title ?? "Pending Action";
    public string Detail => Card?.Detail ?? string.Empty;
    public Visibility DetailVisibility => !string.IsNullOrWhiteSpace(Detail) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PendingVisibility => Card?.Status == "pending" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ResolvedVisibility => Card?.Status != "pending" ? Visibility.Visible : Visibility.Collapsed;

    public string StatusText => Card?.Status switch
    {
        "approved" => "Approved by you",
        "denied" => "Denied by you",
        "expired" => "Expired (new message received)",
        _ => Card?.Status ?? ""
    };

    public ApprovalCardControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateState();
    }

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ApprovalCardControl control)
        {
            control.UpdateState();
        }
    }

    private void UpdateState()
    {
        if (ActionTitleBlock == null) return;

        ActionTitleBlock.Text = Card?.Title ?? "Pending Action";
        var detail = Card?.Detail ?? string.Empty;
        DetailBlock.Text = detail;
        DetailBlock.Visibility = !string.IsNullOrWhiteSpace(detail) ? Visibility.Visible : Visibility.Collapsed;

        var isPending = Card?.Status == "pending";
        PendingActionsPanel.Visibility = isPending ? Visibility.Visible : Visibility.Collapsed;
        ResolvedBanner.Visibility = !isPending ? Visibility.Visible : Visibility.Collapsed;

        StatusTextBlock.Text = Card?.Status switch
        {
            "approved" => "Approved by you",
            "denied" => "Denied by you",
            "expired" => "Expired (new message received)",
            _ => Card?.Status ?? ""
        };
    }

    private void OnAllowClicked(object sender, RoutedEventArgs e) => OnChoice?.Invoke(MessageId, "approve");
    private void OnAlwaysClicked(object sender, RoutedEventArgs e) => OnChoice?.Invoke(MessageId, "always");
    private void OnDenyClicked(object sender, RoutedEventArgs e) => OnChoice?.Invoke(MessageId, "deny");
}
