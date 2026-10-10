using OpenDot.Core.Models;
using OpenDot.Infrastructure.Persistence;

namespace OpenDot.Tests;

public class SqliteDotRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteDotRepository _repo;

    public SqliteDotRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OpenDotTests_" + Guid.NewGuid().ToString("N"));
        _repo = new SqliteDotRepository(_tempDir);
    }

    [Fact]
    public void CreateDot_PersistsAndLoadsCorrectly()
    {
        var dot = _repo.CreateDot("Aura", "Personal Assistant", "Be polite and helpful", new Look { Color = "#ffa7c4" }, true, "gpt-4o");

        Assert.NotNull(dot);
        Assert.NotEmpty(dot.Id);

        var loaded = _repo.GetDot(dot.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Aura", loaded.Name);
        Assert.Equal("Personal Assistant", loaded.Purpose);
        Assert.Equal("#ffa7c4", loaded.Look.Color);
        Assert.True(loaded.LocalAccess);
    }

    [Fact]
    public void AddMessage_StoresAndRetrievesInConversation()
    {
        var dot = _repo.CreateDot("Nova", "Researcher", "Find answers", new Look(), false);
        var conv = _repo.CreateConversation(dot.Id, "Research Conv");

        var msg1 = _repo.AddMessage(new Message
        {
            DotId = dot.Id,
            ConversationId = conv.Id,
            Role = "user",
            Text = "Hello Nova"
        });

        var msg2 = _repo.AddMessage(new Message
        {
            DotId = dot.Id,
            ConversationId = conv.Id,
            Role = "dot",
            Text = "Hello! How can I assist you today?"
        });

        var messages = _repo.ConversationMessages(conv.Id);
        Assert.Equal(2, messages.Count);
        Assert.Equal("Hello Nova", messages[0].Text);
        Assert.Equal("Hello! How can I assist you today?", messages[1].Text);
    }

    [Fact]
    public void AddRule_EnforcesDotOrGlobalScope()
    {
        var dot = _repo.CreateDot("Echo", "Coder", "Code", new Look(), false);
        var rule1 = _repo.AddRule(dot.Id, "delete git repositories", "never");
        var rule2 = _repo.AddRule(null, "send automated payments", "ask");

        var rules = _repo.RulesFor(dot.Id);
        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, r => r.Action == "delete git repositories" && r.Decision == "never");
        Assert.Contains(rules, r => r.Action == "send automated payments" && r.Decision == "ask");
    }

    [Fact]
    public void Routines_AddAndListWorks()
    {
        var dot = _repo.CreateDot("Atlas", "Scheduler", "Plan tasks", new Look(), false);
        var routine = _repo.AddRoutine(dot.Id, "Daily Briefing", "Summarize news", "0 9 * * 1-5");

        Assert.NotNull(routine);
        var list = _repo.ListRoutinesForDot(dot.Id);
        Assert.Single(list);
        Assert.Equal("Daily Briefing", list[0].Name);
        Assert.Equal("0 9 * * 1-5", list[0].Schedule);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }
}
