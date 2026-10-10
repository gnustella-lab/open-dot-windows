using OpenDot.Core.Models;

namespace OpenDot.Core.Interfaces;

public record NotificationEvent(string DotId, string Title, string Body);
public record MessageDeltaEvent(string MessageId, string DotId, string Delta);

public interface IAgentRuntime
{
    event Action<NotificationEvent>? OnNotification;
    event Action<MessageDeltaEvent>? OnMessageDelta;
    event Action<string>? OnActivityChanged;

    Task SendMessageAsync(string dotId, string text, List<Attachment>? attachments = null, string? conversationId = null, CancellationToken ct = default);
    Task QueueTaskAsync(string dotId, string text, string conversationId, CancellationToken ct = default);
    Task SendToChannelAsync(string channelId, string text, CancellationToken ct = default);
    Task RunRoutineAsync(Routine routine, CancellationToken ct = default);
    Task RunTriggerAsync(AppTrigger trigger, string eventJson, CancellationToken ct = default);
    Task ResolveCardAsync(string messageId, string choice, string? answer = null, CancellationToken ct = default);
    void Stop(string dotId);
    void Pause(string dotId);
    void Resume(string dotId);
    bool IsRunning(string dotId);
}
