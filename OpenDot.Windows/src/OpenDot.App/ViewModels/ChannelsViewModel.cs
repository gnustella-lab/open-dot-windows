using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.App.ViewModels;

public partial class ChannelsViewModel : ObservableObject
{
    private readonly IDotRepository _repo;
    private readonly IAgentRuntime _runtime;

    [ObservableProperty]
    private ObservableCollection<Channel> _channels = [];

    [ObservableProperty]
    private Channel? _selectedChannel;

    [ObservableProperty]
    private ObservableCollection<Message> _channelMessages = [];

    [ObservableProperty]
    private string _messageInput = string.Empty;

    public ChannelsViewModel(IDotRepository repo, IAgentRuntime runtime)
    {
        _repo = repo;
        _runtime = runtime;
        LoadChannels();
    }

    [RelayCommand]
    public void LoadChannels()
    {
        var list = _repo.ListChannels();
        Channels.Clear();
        foreach (var c in list) Channels.Add(c);
        SelectedChannel = Channels.FirstOrDefault();
        LoadMessages();
    }

    public void LoadMessages()
    {
        if (SelectedChannel == null) return;
        var msgs = _repo.ChannelMessages(100).Where(m => m.ChannelId == SelectedChannel.Id).ToList();
        ChannelMessages.Clear();
        foreach (var m in msgs) ChannelMessages.Add(m);
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (SelectedChannel == null || string.IsNullOrWhiteSpace(MessageInput)) return;

        var text = MessageInput.Trim();
        MessageInput = string.Empty;

        await _runtime.SendToChannelAsync(SelectedChannel.Id, text);
        LoadMessages();
    }
}
