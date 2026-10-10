namespace OpenDot.Core.Models;

public class CardData
{
    public string Kind { get; set; } = "approval"; // "approval" | "question" | "connect"
    public string Status { get; set; } = "pending"; // "pending" | "approved" | "denied" | "answered" | "expired"
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Tool { get; set; }
    public List<string>? Options { get; set; }
    public string? Answer { get; set; }
    public string? RuleAction { get; set; } // Natural-language action used if user picks "Always allow"
    public string? Toolkit { get; set; } // Composio toolkit slug
    public string? Url { get; set; } // OAuth link
}
