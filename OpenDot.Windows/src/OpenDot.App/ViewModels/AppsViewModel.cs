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

    [ObservableProperty]
    private bool _isLoading;

    public AppsViewModel(ITriggerService triggerService)
    {
        _triggerService = triggerService;
        _ = LoadAppsAsync();
    }

    [RelayCommand]
    public async Task LoadAppsAsync()
    {
        try
        {
            IsLoading = true;
            var list = await _triggerService.GetTriggerAppsAsync();
            Apps.Clear();
            foreach (var a in list) Apps.Add(a);

            var connectedCount = list.Count(a => a.Connected);
            if (connectedCount > 0)
            {
                StatusMessage = $"{connectedCount} app(s) connected in Composio.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading apps: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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
            StatusMessage = $"Opening {app.Name} sign-in in your browser... After authorizing, click Refresh.";
        }
        else
        {
            StatusMessage = "Add a Composio API key in Settings to connect apps.";
        }
    }
}
