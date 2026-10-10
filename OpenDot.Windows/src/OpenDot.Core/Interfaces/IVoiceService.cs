namespace OpenDot.Core.Interfaces;

public record VoiceSessionInfo(string ClientSecret, string Model);

public interface IVoiceService
{
    Task<VoiceSessionInfo> CreateVoiceSessionAsync(string dotId, string conversationId, CancellationToken ct = default);
}
