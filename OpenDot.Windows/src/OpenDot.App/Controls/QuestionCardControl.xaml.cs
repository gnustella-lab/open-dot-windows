using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenDot.Core.Models;
using Windows.System;

namespace OpenDot.App.Controls;

public sealed partial class QuestionCardControl : UserControl
{
    public static readonly DependencyProperty CardProperty =
        DependencyProperty.Register(nameof(Card), typeof(CardData), typeof(QuestionCardControl), new PropertyMetadata(null, OnCardChanged));

    public static readonly DependencyProperty MessageIdProperty =
        DependencyProperty.Register(nameof(MessageId), typeof(string), typeof(QuestionCardControl), new PropertyMetadata(string.Empty));

    public event Action<string, string>? OnAnswer;

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

    public string QuestionText => Card?.Title ?? "Question";
    public List<string> Options => Card?.Options ?? [];
    public Visibility PendingVisibility => Card?.Status == "pending" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AnsweredVisibility => Card?.Status != "pending" ? Visibility.Visible : Visibility.Collapsed;

    public string AnsweredText => Card?.Status == "answered"
        ? $"Answered: {Card?.Answer ?? ""}"
        : Card?.Status ?? "";

    public QuestionCardControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateState();
    }

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuestionCardControl control)
        {
            control.UpdateState();
        }
    }

    private void UpdateState()
    {
        if (QuestionTextBlock == null) return;

        QuestionTextBlock.Text = Card?.Title ?? "Question";
        var isPending = Card?.Status == "pending";

        OptionsList.ItemsSource = Card?.Options ?? [];
        OptionsList.Visibility = (isPending && (Card?.Options?.Count ?? 0) > 0) ? Visibility.Visible : Visibility.Collapsed;
        CustomAnswerGrid.Visibility = isPending ? Visibility.Visible : Visibility.Collapsed;
        AnsweredBanner.Visibility = !isPending ? Visibility.Visible : Visibility.Collapsed;

        AnsweredTextBlock.Text = Card?.Status == "answered"
            ? $"Answered: {Card?.Answer ?? ""}"
            : Card?.Status ?? "";
    }

    private void OnOptionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string text)
        {
            OnAnswer?.Invoke(MessageId, text);
        }
    }

    private void OnCustomAnswerClicked(object sender, RoutedEventArgs e)
    {
        var text = AnswerInput.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            OnAnswer?.Invoke(MessageId, text);
        }
    }

    private void OnAnswerKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            OnCustomAnswerClicked(sender, e);
        }
    }
}
