using Moq;
using OpenDot.Core.Agents;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_IncludesDotIdentityAndRules()
    {
        var dot = new Dot
        {
            Id = "dot_test",
            Name = "Lumina",
            Purpose = "Analyze reports",
            Instructions = "Always output structured bullets"
        };

        var mockRepo = new Mock<IDotRepository>();
        mockRepo.Setup(r => r.RulesFor(dot.Id)).Returns([
            new Rule { Id = "r1", Action = "delete files", Decision = "never" }
        ]);
        mockRepo.Setup(r => r.ListMemories(dot.Id)).Returns([
            new Memory { Id = "m1", Text = "User prefers concise answers" }
        ]);
        mockRepo.Setup(r => r.ListSkills(dot.Id)).Returns([]);
        mockRepo.Setup(r => r.ListRoutinesForDot(dot.Id)).Returns([]);
        mockRepo.Setup(r => r.ListDots()).Returns([dot]);
        mockRepo.Setup(r => r.ListPasswords()).Returns([]);

        var mockComputer = new Mock<IComputerManager>();
        mockComputer.Setup(c => c.Describe(dot.Id)).Returns("a persistent Windows workspace");

        var prompt = PromptBuilder.BuildSystemPrompt(
            dot,
            new TriggerContext("chat"),
            mockRepo.Object,
            mockComputer.Object,
            false,
            []
        );

        Assert.Contains("You are Lumina", prompt);
        Assert.Contains("Your job: Analyze reports", prompt);
        Assert.Contains("Always output structured bullets", prompt);
        Assert.Contains("delete files: never do it", prompt);
        Assert.Contains("User prefers concise answers", prompt);
    }
}
