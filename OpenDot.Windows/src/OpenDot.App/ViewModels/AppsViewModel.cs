using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.App.ViewModels;

public partial class AppsViewModel : ObservableObject
{
    private readonly ITriggerService _triggerService;

    [ObservableProperty]
    private ObservableCollection<TriggerApp> _apps = [];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public AppsViewModel(ITriggerService triggerService)
    {
        _triggerService = triggerService;
        _ = LoadAppsAsync();
    }

    [RelayCommand]
    public async Task LoadAppsAsync()
    {
        var list = await _triggerService.GetTriggerAppsAsync();
        Apps.Clear();
        foreach (var a in list) Apps.Add(a);
    }

    [RelayCommand]
    public async Task ConnectAppAsync(TriggerApp app)
    {
        var url = await _triggerService.ConnectAppUrlAsync(app.Slug);
        if (!string.IsNullOrEmpty(url))
        {
            // Open in default Windows browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusMessage = $"Connecting {app.Name}...";
        }
        else
        {
            StatusMessage = "Add a Composio API key in Settings to connect apps.";
        }
    }
}
