using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Core.Policies;

public record Verdict(string Decision, Rule? Rule);

public class ReviewEngine
{
    private readonly IDotRepository _repository;

    public ReviewEngine(IDotRepository repository)
    {
        _repository = repository;
    }

    public Task<Verdict> ReviewAsync(string dotId, string action, string fallbackDecision, CancellationToken ct = default)
    {
        var rules = _repository.RulesFor(dotId);
        if (rules.Count == 0)
        {
            return Task.FromResult(new Verdict(fallbackDecision, null));
        }

        var normalizedAction = action.Trim().ToLowerInvariant();

        // 1. Check for matching rules
        var matchingRules = rules.Where(r =>
        {
            var ruleAction = r.Action.Trim().ToLowerInvariant();
            if (normalizedAction.Contains(ruleAction) || ruleAction.Contains(normalizedAction))
                return true;

            // Common keyword match
            var actionKeywords = ruleAction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return actionKeywords.Length > 0 && actionKeywords.All(k => normalizedAction.Contains(k));
        }).ToList();

        if (matchingRules.Count == 0)
        {
            return Task.FromResult(new Verdict(fallbackDecision, null));
        }

        // Rule conflict resolution: "never" > "ask" > "allow"
        var neverRule = matchingRules.FirstOrDefault(r => r.Decision == "never");
        if (neverRule != null) return Task.FromResult(new Verdict("never", neverRule));

        var askRule = matchingRules.FirstOrDefault(r => r.Decision == "ask");
        if (askRule != null) return Task.FromResult(new Verdict("ask", askRule));

        var allowRule = matchingRules.FirstOrDefault(r => r.Decision == "allow");
        return Task.FromResult(new Verdict("allow", allowRule));
    }
}
