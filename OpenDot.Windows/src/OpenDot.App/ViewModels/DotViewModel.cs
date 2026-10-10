using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;
using Windows.Storage.Streams;

namespace OpenDot.App.ViewModels;

public partial class DotViewModel : ObservableObject
{
    private readonly IDotRepository _repo;
    private readonly IAgentRuntime _runtime;
    private readonly IComputerManager _computer;

    [ObservableProperty]
    private Dot? _dot;

    [ObservableProperty]
    private ObservableCollection<Message> _messages = [];

    [ObservableProperty]
    private ObservableCollection<Conversation> _conversations = [];

    [ObservableProperty]
    private Conversation? _currentConversation;

    [ObservableProperty]
    private string _inputMessage = string.Empty;

    [ObservableProperty]
    private bool _isWorking;

    [ObservableProperty]
    private string _activityText = string.Empty;

    // Computer tab properties
    [ObservableProperty]
    private BitmapImage? _screenBitmap;

    [ObservableProperty]
    private string _browserUrl = "https://www.bing.com";

    [ObservableProperty]
    private string _computerStatus = "Headless browser ready";

    // Setup tab properties
    [ObservableProperty]
    private ObservableCollection<Rule> _rules = [];

    [ObservableProperty]
    private ObservableCollection<Memory> _memories = [];

    [ObservableProperty]
    private ObservableCollection<Skill> _skills = [];

    [ObservableProperty]
    private ObservableCollection<Routine> _routines = [];

    [ObservableProperty]
    private ObservableCollection<FileEntry> _workspaceFiles = [];

    public DotViewModel(IDotRepository repo, IAgentRuntime runtime, IComputerManager computer)
    {
        _repo = repo;
        _runtime = runtime;
        _computer = computer;

        _runtime.OnMessageDelta += ev =>
        {
            if (Dot != null && ev.DotId == Dot.Id)
            {
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
                {
                    var msg = Messages.FirstOrDefault(m => m.Id == ev.MessageId);
                    if (msg != null)
                    {
                        var index = Messages.IndexOf(msg);
                        Messages[index] = _repo.GetMessage(msg.Id) ?? msg;
                    }
                    else
                    {
                        var fresh = _repo.GetMessage(ev.MessageId);
                        if (fresh != null) Messages.Add(fresh);
                    }
                });
            }
        };

        _runtime.OnActivityChanged += dotId =>
        {
            if (Dot != null && dotId == Dot.Id)
            {
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
                {
                    var fresh = _repo.GetDot(dotId);
                    if (fresh != null)
                    {
                        Dot = fresh;
                        IsWorking = fresh.Status == "working";
                        ActivityText = fresh.Activity ?? (IsWorking ? "Working..." : "Idle");
                    }
                });
            }
        };
    }

    public void SetDot(Dot dot)
    {
        Dot = dot;
        IsWorking = dot.Status == "working";
        ActivityText = dot.Activity ?? (IsWorking ? "Working..." : "Idle");

        LoadConversations();
        LoadSetupData();
        _ = RefreshFilesAsync();
    }

    public void LoadConversations()
    {
        if (Dot == null) return;
        var list = _repo.ListConversations(Dot.Id);
        Conversations.Clear();
        foreach (var c in list) Conversations.Add(c);

        CurrentConversation = Conversations.FirstOrDefault();
        LoadMessages();
    }

    public void LoadMessages()
    {
        if (CurrentConversation == null) return;
        var msgs = _repo.ConversationMessages(CurrentConversation.Id, 100);
        Messages.Clear();
        foreach (var m in msgs) Messages.Add(m);
    }

    public void LoadSetupData()
    {
        if (Dot == null) return;

        Rules.Clear();
        foreach (var r in _repo.RulesFor(Dot.Id)) Rules.Add(r);

        Memories.Clear();
        foreach (var m in _repo.ListMemories(Dot.Id)) Memories.Add(m);

        Skills.Clear();
        foreach (var s in _repo.ListSkills(Dot.Id)) Skills.Add(s);

        Routines.Clear();
        foreach (var rt in _repo.ListRoutinesForDot(Dot.Id)) Routines.Add(rt);
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (Dot == null || string.IsNullOrWhiteSpace(InputMessage)) return;

        var text = InputMessage.Trim();
        InputMessage = string.Empty;

        var convId = CurrentConversation?.Id ?? _repo.LatestConversationId(Dot.Id);
        await _runtime.SendMessageAsync(Dot.Id, text, null, convId);
        LoadMessages();
    }

    [RelayCommand]
    public void Stop()
    {
        if (Dot != null) _runtime.Stop(Dot.Id);
    }

    [RelayCommand]
    public void TogglePause()
    {
        if (Dot == null) return;
        if (Dot.Status == "paused") _runtime.Resume(Dot.Id);
        else _runtime.Pause(Dot.Id);
    }

    [RelayCommand]
    public async Task ResolveCardAsync(CardResolutionArgs args)
    {
        await _runtime.ResolveCardAsync(args.MessageId, args.Choice, args.Answer);
        LoadMessages();
    }

    // ---------- Computer Automation ----------
    [RelayCommand]
    public async Task NavigateBrowserAsync()
    {
        if (Dot == null || string.IsNullOrWhiteSpace(BrowserUrl)) return;
        ComputerStatus = $"Navigating to {BrowserUrl}...";
        try
        {
            var result = await _computer.OpenUrlAsync(Dot.Id, BrowserUrl);
            ComputerStatus = result;
            await CaptureScreenshotAsync();
        }
        catch (Exception ex)
        {
            ComputerStatus = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CaptureScreenshotAsync()
    {
        if (Dot == null) return;
        try
        {
            var bytes = await _computer.ScreenshotAsync(Dot.Id);
            if (bytes != null && bytes.Length > 0)
            {
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync();
                }
                stream.Seek(0);

                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(stream);
                ScreenBitmap = bmp;
                ComputerStatus = "Screenshot updated";
            }
        }
        catch (Exception ex)
        {
            ComputerStatus = $"Screenshot failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RefreshFilesAsync()
    {
        if (Dot == null) return;
        try
        {
            var files = await _computer.ListFilesAsync(Dot.Id);
            WorkspaceFiles.Clear();
            foreach (var f in files) WorkspaceFiles.Add(f);
        }
        catch { }
    }

    // ---------- Setup CRUD ----------
    [RelayCommand]
    public void SaveDotSetup()
    {
        if (Dot != null)
        {
            _repo.UpdateDot(Dot);
        }
    }

    [RelayCommand]
    public void AddRule(string action)
    {
        if (Dot != null && !string.IsNullOrWhiteSpace(action))
        {
            _repo.AddRule(Dot.Id, action, "ask");
            LoadSetupData();
        }
    }

    [RelayCommand]
    public void DeleteRule(string ruleId)
    {
        _repo.DeleteRule(ruleId);
        LoadSetupData();
    }

    [RelayCommand]
    public void AddMemory(string text)
    {
        if (Dot != null && !string.IsNullOrWhiteSpace(text))
        {
            _repo.AddMemory(Dot.Id, text);
            LoadSetupData();
        }
    }

    [RelayCommand]
    public void DeleteMemory(string memId)
    {
        _repo.DeleteMemory(memId);
        LoadSetupData();
    }
}

public record CardResolutionArgs(string MessageId, string Choice, string? Answer = null);
