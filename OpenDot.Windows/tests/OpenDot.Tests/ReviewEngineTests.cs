using Moq;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;
using OpenDot.Core.Policies;

namespace OpenDot.Tests;

public class ReviewEngineTests
{
    [Fact]
    public async Task ReviewAsync_ReturnsFallback_WhenNoRulesExist()
    {
        var mockRepo = new Mock<IDotRepository>();
        mockRepo.Setup(r => r.RulesFor(It.IsAny<string>())).Returns([]);

        var engine = new ReviewEngine(mockRepo.Object);
        var verdict = await engine.ReviewAsync("dot1", "open website", "allow");

        Assert.Equal("allow", verdict.Decision);
        Assert.Null(verdict.Rule);
    }

    [Fact]
    public async Task ReviewAsync_NeverOverridesAskAndAllow()
    {
        var mockRepo = new Mock<IDotRepository>();
        mockRepo.Setup(r => r.RulesFor("dot1")).Returns([
            new Rule { Id = "r1", DotId = "dot1", Action = "delete database files", Decision = "never" },
            new Rule { Id = "r2", DotId = "dot1", Action = "delete", Decision = "ask" }
        ]);

        var engine = new ReviewEngine(mockRepo.Object);
        var verdict = await engine.ReviewAsync("dot1", "delete database files now", "allow");

        Assert.Equal("never", verdict.Decision);
        Assert.NotNull(verdict.Rule);
        Assert.Equal("delete database files", verdict.Rule.Action);
    }

    [Fact]
    public async Task ReviewAsync_AskTriggers_WhenRuleMatches()
    {
        var mockRepo = new Mock<IDotRepository>();
        mockRepo.Setup(r => r.RulesFor("dot1")).Returns([
            new Rule { Id = "r1", DotId = "dot1", Action = "send email", Decision = "ask" }
        ]);

        var engine = new ReviewEngine(mockRepo.Object);
        var verdict = await engine.ReviewAsync("dot1", "send email to client", "allow");

        Assert.Equal("ask", verdict.Decision);
        Assert.NotNull(verdict.Rule);
    }
}
