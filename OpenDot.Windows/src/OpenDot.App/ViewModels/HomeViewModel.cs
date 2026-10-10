using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IDotRepository _repo;

    [ObservableProperty]
    private int _totalDots;

    [ObservableProperty]
    private int _activeRoutines;

    [ObservableProperty]
    private int _totalMemories;

    [ObservableProperty]
    private ObservableCollection<Dot> _recentDots = [];

    public HomeViewModel(IDotRepository repo)
    {
        _repo = repo;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        var dots = _repo.ListDots();
        TotalDots = dots.Count;

        var routines = _repo.ListRoutines();
        ActiveRoutines = routines.Count(r => r.Enabled);

        RecentDots.Clear();
        foreach (var d in dots) RecentDots.Add(d);
    }
}
