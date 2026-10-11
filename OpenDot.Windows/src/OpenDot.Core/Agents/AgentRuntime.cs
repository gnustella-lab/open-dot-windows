using System.Collections.Concurrent;
using System.Text.Json;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;
using OpenDot.Core.Policies;

namespace OpenDot.Core.Agents;

public class AgentRuntime : IAgentRuntime
{
    private readonly IDotRepository _repo;
    private readonly IComputerManager _computer;
    private readonly ISecureVault _vault;
    private readonly Func<Dot, IAiClient> _clientResolver;
    private readonly ReviewEngine _reviewEngine;
    private readonly List<ToolDefinition> _tools;

    private readonly ConcurrentDictionary<string, DotRunState> _states = new();
    private const int MaxSteps = 60;

    public event Action<NotificationEvent>? OnNotification;
    public event Action<MessageDeltaEvent>? OnMessageDelta;
    public event Action<string>? OnActivityChanged;

    private class InboxItem
    {
        public string Text { get; set; } = string.Empty;
        public TriggerContext Trigger { get; set; } = new("chat");
        public string ConversationId { get; set; } = string.Empty;
        public List<Attachment>? Attachments { get; set; }
    }

    private class DotRunState
    {
        public bool Running;
        public CancellationTokenSource? Cts;
        public readonly List<InboxItem> Inbox = [];
        public readonly List<Func<Task>> After = [];
    }

    public AgentRuntime(
        IDotRepository repo,
        IComputerManager computer,
        ISecureVault vault,
        Func<Dot, IAiClient> clientResolver)
    {
        _repo = repo;
        _computer = computer;
        _vault = vault;
        _clientResolver = clientResolver;
        _reviewEngine = new ReviewEngine(repo);
        _tools = ToolRegistry.GetDefaultTools();
    }

    private DotRunState State(string dotId) => _states.GetOrAdd(dotId, _ => new DotRunState());

    public bool IsRunning(string dotId) => State(dotId).Running;

    public void Stop(string dotId)
    {
        var s = State(dotId);
        lock (s)
        {
            s.Inbox.Clear();
            s.Cts?.Cancel();
        }
    }

    public void Pause(string dotId)
    {
        var dot = _repo.GetDot(dotId);
        if (dot == null || dot.Status == "paused") return;

        _repo.UpdateDotStatus(dotId, "paused");
        var s = State(dotId);
        lock (s)
        {
            s.Cts?.Cancel();
        }

        _repo.AddMessage(new Message
        {
            DotId = dotId,
            Role = "system",
            Text = $"Paused. Ongoing work was stopped, and {dot.Name} won't message you until resumed."
        });

        _ = _computer.SleepAsync(dotId);
    }

    public void Resume(string dotId)
    {
        var dot = _repo.GetDot(dotId);
        if (dot == null || dot.Status != "paused") return;

        var waiting = _repo.PendingCards(dotId).Count > 0;
        _repo.UpdateDotStatus(dotId, waiting ? "waiting" : "idle");
        _repo.AddMessage(new Message
        {
            DotId = dotId,
            Role = "system",
            Text = $"{dot.Name} resumed."
        });

        _ = PumpAsync(dotId);
    }

    public Task SendMessageAsync(string dotId, string text, List<Attachment>? attachments = null, string? conversationId = null, CancellationToken ct = default)
    {
        var dot = _repo.GetDot(dotId) ?? throw new InvalidOperationException("Dot not found.");
        var conv = conversationId ?? _repo.LatestConversationId(dotId);

        _repo.AddMessage(new Message
        {
            DotId = dotId,
            Role = "user",
            Text = text,
            Attachments = attachments,
            ConversationId = conv
        });

        if (dot.Status == "paused")
        {
            _repo.AddMessage(new Message
            {
                DotId = dotId,
                Role = "system",
                Text = $"{dot.Name} is paused. Resume it to pick this up.",
                ConversationId = conv
            });
        }

        var s = State(dotId);
        lock (s)
        {
            s.Inbox.Add(new InboxItem
            {
                Text = text,
                Trigger = new TriggerContext("chat"),
                Attachments = attachments,
                ConversationId = conv
            });
        }

        _ = PumpAsync(dotId);
        return Task.CompletedTask;
    }

    public Task QueueTaskAsync(string dotId, string text, string conversationId, CancellationToken ct = default)
    {
        var dot = _repo.GetDot(dotId) ?? throw new InvalidOperationException("Dot not found.");
        _repo.AddMessage(new Message
        {
            DotId = dotId,
            Role = "activity",
            Text = $"Voice task · {text}",
            ConversationId = conversationId
        });

        var s = State(dotId);
        lock (s)
        {
            s.Inbox.Add(new InboxItem
            {
                Text = $"{text}\n\n(Asked on a voice call.)",
                Trigger = new TriggerContext("chat"),
                ConversationId = conversationId
            });
        }

        _ = PumpAsync(dotId);
        return Task.CompletedTask;
    }

    public Task SendToChannelAsync(string channelId, string text, CancellationToken ct = default)
    {
        var ch = _repo.GetChannel(channelId) ?? throw new InvalidOperationException("Channel not found.");
        _repo.AddMessage(new Message
        {
            DotId = ch.LeadId,
            Role = "user",
            Text = text,
            ChannelId = channelId
        });

        var members = ch.MemberIds.Select(id => _repo.GetDot(id)).Where(d => d != null).Cast<Dot>().ToList();
        var mentioned = members.Where(d => text.Contains($"@{d.Name}", StringComparison.OrdinalIgnoreCase)).ToList();
        var responders = mentioned.Count > 0 ? mentioned : members.Where(d => d.Id == ch.LeadId).ToList();

        foreach (var d in responders)
        {
            if (d.Status == "paused")
            {
                _repo.AddMessage(new Message { DotId = d.Id, Role = "system", Text = $"{d.Name} is paused.", ChannelId = channelId });
                continue;
            }

            var conv = _repo.GetOrCreateWorkConversation(d.Id, "channel", channelId, $"#{ch.Name}");
            var s = State(d.Id);
            lock (s)
            {
                s.Inbox.Add(new InboxItem
                {
                    Text = $"[#{ch.Name}] {text}",
                    Trigger = new TriggerContext("channel", ChannelId: channelId, Name: ch.Name),
                    ConversationId = conv.Id
                });
            }
            _ = PumpAsync(d.Id);
        }

        return Task.CompletedTask;
    }

    public Task RunRoutineAsync(Routine routine, CancellationToken ct = default)
    {
        var dot = _repo.GetDot(routine.DotId);
        if (dot == null || dot.Status == "paused" || !routine.Enabled) return Task.CompletedTask;

        var conv = _repo.GetOrCreateWorkConversation(dot.Id, "chat", $"routine:{routine.Id}", $"Routine · {routine.Name}");
        _repo.AddMessage(new Message
        {
            DotId = dot.Id,
            Role = "system",
            Text = $"Routine \"{routine.Name}\" started",
            From = $"routine:{routine.Name}",
            ConversationId = conv.Id
        });

        var s = State(dot.Id);
        lock (s)
        {
            s.Inbox.Add(new InboxItem
            {
                Text = $"[Routine: {routine.Name}] {routine.Instruction}",
                Trigger = new TriggerContext("routine", Name: routine.Name),
                ConversationId = conv.Id
            });
        }

        _ = PumpAsync(dot.Id);
        return Task.CompletedTask;
    }

    public Task RunTriggerAsync(AppTrigger trigger, string eventJson, CancellationToken ct = default)
    {
        var dot = _repo.GetDot(trigger.DotId);
        if (dot == null || dot.Status == "paused" || !trigger.Enabled) return Task.CompletedTask;

        var conv = _repo.GetOrCreateWorkConversation(dot.Id, "chat", $"trigger:{trigger.Id}", $"Trigger · {trigger.Name}");
        _repo.AddMessage(new Message
        {
            DotId = dot.Id,
            Role = "system",
            Text = $"Trigger \"{trigger.Name}\" fired",
            From = $"trigger:{trigger.Name}",
            ConversationId = conv.Id
        });

        var s = State(dot.Id);
        lock (s)
        {
            s.Inbox.Add(new InboxItem
            {
                Text = $"[Trigger: {trigger.Name}] {trigger.Instruction}\n\nWhat happened ({trigger.Toolkit} event):\n{eventJson}",
                Trigger = new TriggerContext("trigger", Name: trigger.Name),
                ConversationId = conv.Id
            });
        }

        _ = PumpAsync(dot.Id);
        return Task.CompletedTask;
    }

    public async Task ResolveCardAsync(string messageId, string choice, string? answer = null, CancellationToken ct = default)
    {
        var msg = _repo.GetMessage(messageId);
        if (msg == null || msg.Card == null || msg.Card.Status != "pending") return;

        var dot = _repo.GetDot(msg.DotId);
        if (dot == null || dot.Status == "paused") return;

        var approved = choice == "approve" || choice == "always";
        var newStatus = choice == "answer" ? "answered" : approved ? "approved" : "denied";

        msg.Card.Status = newStatus;
        msg.Card.Answer = answer;
        _repo.UpdateCard(messageId, msg.Card);

        if (choice == "always" && !string.IsNullOrWhiteSpace(msg.Card.RuleAction))
        {
            _repo.AddRule(dot.Id, msg.Card.RuleAction, "allow");
        }

        var convId = msg.ConversationId ?? _repo.LatestConversationId(dot.Id);
        var conv = _repo.GetConversation(convId);
        if (conv == null || string.IsNullOrWhiteSpace(conv.Pending)) return;

        PendingRunState? pending;
        try
        {
            pending = JsonSerializer.Deserialize<PendingRunState>(conv.Pending);
        }
        catch
        {
            pending = null;
        }

        if (pending == null || pending.CardId != messageId) return;

        // Clear card wait
        pending.CardId = null;
        var call = pending.Calls[pending.Index];
        var def = _tools.FirstOrDefault(t => t.Name == call.Name);

        string output;
        if (def?.PauseKind == "question")
            output = $"The user answered: {answer ?? ""}";
        else if (def?.PauseKind == "approval")
            output = approved ? "The user approved. Go ahead." : "The user denied this. Do not do it; tell them briefly what you'll do instead, if anything.";
        else if (def?.PauseKind == "connect")
            output = approved ? "Connected. Continue with the task." : "The user chose not to connect this app right now.";
        else if (approved && def?.ExecuteAsync != null)
        {
            var args = SafeParse(call.ArgumentsJson);
            var toolCtx = new ToolContext(dot, _repo, _computer, _vault, 0, ConsultDotAsync);
            output = await def.ExecuteAsync(args, toolCtx, ct);
        }
        else
        {
            output = "The user denied this action. Don't retry it; continue without it or ask what they'd prefer.";
        }

        pending.Outputs.Add(new AiChatMessage
        {
            Role = "tool",
            ToolCallId = call.Id,
            Content = output
        });

        pending.Index++;
        _repo.SetThread(convId, pending.ResponseId, JsonSerializer.Serialize(pending));

        if (await ProcessToolCallsAsync(dot, convId, pending, ct)) return;

        await RunDriveLoopAsync(dot, convId, pending, ct);
    }

    private class PendingRunState
    {
        public string ResponseId { get; set; } = string.Empty;
        public List<AiToolCall> Calls { get; set; } = [];
        public List<AiChatMessage> Outputs { get; set; } = [];
        public int Index { get; set; }
        public string? CardId { get; set; }
        public TriggerContext Trigger { get; set; } = new("chat");
    }

    private async Task PumpAsync(string dotId)
    {
        var s = State(dotId);
        InboxItem? item = null;

        lock (s)
        {
            if (s.Running) return;
            if (s.Inbox.Count == 0) return;
            item = s.Inbox[0];
            s.Inbox.RemoveAt(0);
            s.Running = true;
            s.Cts = new CancellationTokenSource();
        }

        var dot = _repo.GetDot(dotId);
        if (dot == null || dot.Status == "paused" || item == null)
        {
            lock (s) { s.Running = false; s.Cts = null; }
            return;
        }

        _repo.UpdateDotStatus(dotId, "working");

        try
        {
            await TurnAsync(dot, item.Text, item.Trigger, item.ConversationId, s.Cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Aborted normally
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AgentRuntime] Run failed for {dot.Name}: {ex}");
            _repo.AddMessage(new Message
            {
                DotId = dotId,
                Role = "system",
                Text = $"Something went wrong: {ex.Message}",
                ConversationId = item.ConversationId
            });
        }
        finally
        {
            lock (s)
            {
                s.Running = false;
                s.Cts = null;
            }

            var freshDot = _repo.GetDot(dotId);
            if (freshDot != null && freshDot.Status != "paused")
            {
                var waiting = _repo.PendingCards(dotId).Count > 0;
                _repo.UpdateDotStatus(dotId, waiting ? "waiting" : "idle", null);
            }

            OnActivityChanged?.Invoke(dotId);

            // Process next queued turn
            if (s.Inbox.Count > 0 && freshDot?.Status != "paused")
            {
                _ = PumpAsync(dotId);
            }
        }
    }

    private async Task TurnAsync(Dot dot, string text, TriggerContext trigger, string conversationId, CancellationToken ct)
    {
        var conv = _repo.GetConversation(conversationId) ?? _repo.CreateConversation(dot.Id, "Chat");

        // Build conversation messages
        var messages = new List<AiChatMessage>();
        var history = _repo.ConversationMessages(conversationId, 30)
            .Where(m => (m.Role == "user" || m.Role == "dot") && !string.IsNullOrWhiteSpace(m.Text))
            .ToList();

        foreach (var h in history)
        {
            messages.Add(new AiChatMessage
            {
                Role = h.Role == "user" ? "user" : "assistant",
                Content = h.Text
            });
        }

        if (messages.Count == 0 || messages.Last().Role != "user")
        {
            messages.Add(new AiChatMessage { Role = "user", Content = text });
        }

        var pending = new PendingRunState
        {
            Trigger = trigger,
            Calls = [],
            Outputs = messages,
            Index = 0
        };

        await RunDriveLoopAsync(dot, conversationId, pending, ct);
    }

    private async Task RunDriveLoopAsync(Dot dot, string conversationId, PendingRunState pending, CancellationToken ct)
    {
        var client = _clientResolver(dot);

        for (int step = 0; step < MaxSteps; step++)
        {
            ct.ThrowIfCancellationRequested();

            var sysPrompt = PromptBuilder.BuildSystemPrompt(dot, pending.Trigger, _repo, _computer, false, []);

            var req = new AiModelRequest
            {
                Model = dot.Model ?? _repo.GetSetting("default_model") ?? "gpt-4o",
                SystemPrompt = sysPrompt,
                Messages = pending.Outputs,
                Tools = _tools,
                PreviousResponseId = string.IsNullOrEmpty(pending.ResponseId) ? null : pending.ResponseId
            };

            Message? draftMessage = null;

            var response = await client.StreamAsync(
                req,
                onDelta: delta =>
                {
                    if (draftMessage == null)
                    {
                        draftMessage = _repo.AddMessage(new Message
                        {
                            DotId = dot.Id,
                            Role = "dot",
                            Text = delta,
                            ConversationId = conversationId
                        });
                    }
                    else
                    {
                        draftMessage.Text += delta;
                        _repo.UpdateMessageText(draftMessage.Id, draftMessage.Text);
                    }

                    OnMessageDelta?.Invoke(new MessageDeltaEvent(draftMessage.Id, dot.Id, delta));
                },
                onActivity: activity =>
                {
                    _repo.UpdateDotStatus(dot.Id, "working", activity);
                    OnActivityChanged?.Invoke(dot.Id);
                },
                ct: ct
            );

            pending.ResponseId = response.Id;
            _repo.SetThread(conversationId, response.Id, null);

            if (response.ToolCalls.Count == 0)
            {
                // Finished response
                if (draftMessage != null)
                {
                    _repo.UpdateMessageText(draftMessage.Id, draftMessage.Text);
                }
                return;
            }

            // Process tool calls
            pending.Calls = response.ToolCalls;
            pending.Index = 0;
            pending.Outputs.Add(new AiChatMessage
            {
                Role = "assistant",
                Content = response.Content,
                ToolCalls = response.ToolCalls
            });

            var paused = await ProcessToolCallsAsync(dot, conversationId, pending, ct);
            if (paused)
            {
                // Paused waiting for user response on a Card
                return;
            }
        }

        _repo.AddMessage(new Message
        {
            DotId = dot.Id,
            Role = "system",
            Text = $"Stopped after {MaxSteps} steps. Say \"continue\" to keep going.",
            ConversationId = conversationId
        });
    }

    private async Task<bool> ProcessToolCallsAsync(Dot dot, string conversationId, PendingRunState pending, CancellationToken ct)
    {
        for (; pending.Index < pending.Calls.Count; pending.Index++)
        {
            ct.ThrowIfCancellationRequested();
            var call = pending.Calls[pending.Index];
            var def = _tools.FirstOrDefault(t => t.Name == call.Name);
            var args = SafeParse(call.ArgumentsJson);

            if (def == null)
            {
                pending.Outputs.Add(new AiChatMessage { Role = "tool", ToolCallId = call.Id, Content = $"Unknown tool {call.Name}" });
                continue;
            }

            // Check pause tools
            if (def.PauseKind == "question")
            {
                var options = new List<string>();
                if (args.TryGetValue("options", out var optVal) && optVal is JsonElement je && je.ValueKind == JsonValueKind.Array)
                {
                    options = je.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                }

                PauseFor(dot, conversationId, pending, new CardData
                {
                    Kind = "question",
                    Status = "pending",
                    Title = args.TryGetValue("question", out var q) ? q?.ToString() ?? "" : "",
                    Options = options
                });
                return true;
            }

            if (def.PauseKind == "approval")
            {
                PauseFor(dot, conversationId, pending, new CardData
                {
                    Kind = "approval",
                    Status = "pending",
                    Title = args.TryGetValue("action", out var a) ? a?.ToString() ?? "" : "Approval required",
                    Detail = args.TryGetValue("details", out var d) ? d?.ToString() : null,
                    Tool = def.Name
                });
                return true;
            }

            if (def.PauseKind == "connect")
            {
                var toolkit = args.TryGetValue("toolkit", out var tk) ? tk?.ToString() ?? "" : "";
                PauseFor(dot, conversationId, pending, new CardData
                {
                    Kind = "connect",
                    Status = "pending",
                    Title = $"Connect {toolkit}",
                    Toolkit = toolkit,
                    Detail = $"{dot.Name} needs access to your {toolkit} to continue."
                });
                return true;
            }

            // Safety rules review
            if (def.Describe != null)
            {
                var actionDesc = def.Describe(args, dot);
                var fallbackDecision = def.DefaultDecision?.Invoke(args, dot) ?? "allow";
                var verdict = await _reviewEngine.ReviewAsync(dot.Id, actionDesc, fallbackDecision, ct);

                if (verdict.Decision == "never")
                {
                    _repo.AddMessage(new Message
                    {
                        DotId = dot.Id,
                        Role = "activity",
                        Text = $"Blocked by your rule · {verdict.Rule?.Action ?? actionDesc}",
                        ConversationId = conversationId
                    });
                    pending.Outputs.Add(new AiChatMessage
                    {
                        Role = "tool",
                        ToolCallId = call.Id,
                        Content = $"Not allowed: the user's rule says never {verdict.Rule?.Action ?? "do this"}."
                    });
                    continue;
                }

                if (verdict.Decision == "ask")
                {
                    PauseFor(dot, conversationId, pending, new CardData
                    {
                        Kind = "approval",
                        Status = "pending",
                        Title = actionDesc,
                        Detail = verdict.Rule != null ? $"Your rule: ask first when it wants to {verdict.Rule.Action}." : null,
                        Tool = def.Name,
                        RuleAction = verdict.Rule?.Action ?? actionDesc
                    });
                    return true;
                }
            }

            // Execute tool
            _repo.UpdateDotStatus(dot.Id, "working", def.Label);
            OnActivityChanged?.Invoke(dot.Id);

            string output;
            try
            {
                var toolCtx = new ToolContext(dot, _repo, _computer, _vault, 0, ConsultDotAsync);
                output = def.ExecuteAsync != null ? await def.ExecuteAsync(args, toolCtx, ct) : "Tool execution not supported.";
            }
            catch (Exception ex)
            {
                output = $"Error: {ex.Message}";
            }

            pending.Outputs.Add(new AiChatMessage
            {
                Role = "tool",
                ToolCallId = call.Id,
                Content = output
            });
        }

        return false;
    }

    private void PauseFor(Dot dot, string conversationId, PendingRunState pending, CardData card)
    {
        var msg = _repo.AddMessage(new Message
        {
            DotId = dot.Id,
            Role = "card",
            Text = card.Title,
            Card = card,
            ConversationId = conversationId
        });

        pending.CardId = msg.Id;
        _repo.SetThread(conversationId, pending.ResponseId, JsonSerializer.Serialize(pending));
        _repo.UpdateDotStatus(dot.Id, "waiting");

        OnNotification?.Invoke(new NotificationEvent(dot.Id, $"{dot.Name} needs your approval", card.Title));
    }

    private async Task<string> ConsultDotAsync(Dot target, string message, Dot from, int depth, CancellationToken ct)
    {
        _repo.AddMessage(new Message
        {
            DotId = target.Id,
            Role = "user",
            Text = message,
            From = $"dot:{from.Name}"
        });

        var client = _clientResolver(target);
        var sysPrompt = PromptBuilder.BuildSystemPrompt(target, new TriggerContext("dot", From: from.Name), _repo, _computer, false, []);
        var req = new AiModelRequest
        {
            Model = target.Model ?? _repo.GetSetting("default_model") ?? "gpt-4o",
            SystemPrompt = sysPrompt,
            Messages = [new AiChatMessage { Role = "user", Content = $"{from.Name} asks: {message}" }]
        };

        var res = await client.StreamAsync(req, ct: ct);
        var reply = !string.IsNullOrWhiteSpace(res.Content) ? res.Content : "(no reply)";

        _repo.AddMessage(new Message
        {
            DotId = target.Id,
            Role = "dot",
            Text = reply,
            From = $"dot:{from.Name}"
        });

        return $"{target.Name} replied: {reply}";
    }

    private static Dictionary<string, object?> SafeParse(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
