using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenDot.App.Services;
using OpenDot.App.ViewModels;
using OpenDot.Core.Models;
using Windows.System;

namespace OpenDot.App.Views;

public sealed partial class ChannelsPage : Page
{
    public ChannelsViewModel ViewModel { get; }

    public ChannelsPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Services.GetRequiredService<ChannelsViewModel>();
        DataContext = ViewModel;

        Loaded += (_, _) =>
        {
            ViewModel.LoadChannels();
            ChannelsListView.ItemsSource = ViewModel.Channels;
            ChannelMessagesListView.ItemsSource = ViewModel.ChannelMessages;
            if (ViewModel.SelectedChannel != null)
            {
                ChannelTitleText.Text = $"#{ViewModel.SelectedChannel.Name}";
            }
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ChannelsViewModel.ChannelMessages))
            {
                ChannelMessagesListView.ItemsSource = ViewModel.ChannelMessages;
            }
        };
    }

    private void OnChannelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelsListView.SelectedItem is Channel ch)
        {
            ViewModel.SelectedChannel = ch;
            ChannelTitleText.Text = $"#{ch.Name}";
            ViewModel.LoadMessages();
            ChannelMessagesListView.ItemsSource = ViewModel.ChannelMessages;
        }
    }

    private async void OnSendChannelClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.MessageInput = ChannelInputBox.Text;
        ChannelInputBox.Text = string.Empty;
        await ViewModel.SendMessageAsync();
        ChannelMessagesListView.ItemsSource = ViewModel.ChannelMessages;
    }

    private void OnChannelInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            OnSendChannelClicked(sender, e);
        }
    }
}
