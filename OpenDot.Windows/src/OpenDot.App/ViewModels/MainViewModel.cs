using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;
using OpenDot.Infrastructure.Windows;

namespace OpenDot.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDotRepository _repo;
    private readonly IAgentRuntime _runtime;
    private readonly INotificationService _notifications;

    [ObservableProperty]
    private ObservableCollection<Dot> _dots = [];

    [ObservableProperty]
    private ObservableCollection<Channel> _channels = [];

    [ObservableProperty]
    private Dot? _selectedDot;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainViewModel(IDotRepository repo, IAgentRuntime runtime, INotificationService notifications)
    {
        _repo = repo;
        _runtime = runtime;
        _notifications = notifications;

        _runtime.OnNotification += ev =>
        {
            _notifications.ShowNotification(ev.Title, ev.Body, ev.DotId);
        };

        _runtime.OnActivityChanged += dotId =>
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(LoadDots);
        };

        LoadData();
    }

    [RelayCommand]
    public void LoadData()
    {
        LoadDots();
        LoadChannels();
    }

    public void LoadDots()
    {
        var list = _repo.ListDots();
        Dots.Clear();
        foreach (var d in list) Dots.Add(d);
    }

    public void LoadChannels()
    {
        var list = _repo.ListChannels();
        Channels.Clear();
        foreach (var c in list) Channels.Add(c);
    }

    [RelayCommand]
    public void SelectDot(Dot? dot)
    {
        SelectedDot = dot;
    }
}
