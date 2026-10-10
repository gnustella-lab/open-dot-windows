using OpenDot.Core.Models;

namespace OpenDot.Core.Interfaces;

public interface ITriggerService
{
    Task<List<TriggerApp>> GetTriggerAppsAsync(CancellationToken ct = default);
    Task<string?> ConnectAppUrlAsync(string slug, CancellationToken ct = default);
    Task<List<TriggerType>> GetTriggerTypesAsync(string slug, CancellationToken ct = default);
    Task StartListeningAsync(CancellationToken ct = default);
    Task StopListeningAsync(CancellationToken ct = default);
}
