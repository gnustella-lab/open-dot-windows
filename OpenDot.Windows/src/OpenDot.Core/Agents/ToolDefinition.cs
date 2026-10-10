using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Core.Agents;

public record ToolContext(
    Dot Dot,
    IDotRepository Repository,
    IComputerManager Computer,
    ISecureVault Vault,
    int Depth,
    Func<Dot, string, Dot, int, CancellationToken, Task<string>>? ConsultDelegate = null
);

public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{\"type\":\"object\",\"properties\":{},\"required\":[]}";
    public bool Strict { get; set; } = true;
    public string? PauseKind { get; set; } // "question" | "approval" | "connect"
    public Func<Dictionary<string, object?>, Dot, string>? Describe { get; set; }
    public Func<Dictionary<string, object?>, Dot, string>? DefaultDecision { get; set; }
    public Func<Dictionary<string, object?>, ToolContext, CancellationToken, Task<string?>>? PrecheckAsync { get; set; }
    public Func<Dictionary<string, object?>, ToolContext, CancellationToken, Task<string>>? ExecuteAsync { get; set; }
}
