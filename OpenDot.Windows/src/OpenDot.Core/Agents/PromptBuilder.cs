using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Core.Agents;

public record TriggerContext(
    string Kind, // "chat" | "routine" | "trigger" | "dot" | "channel"
    string? Name = null,
    string? From = null,
    string? ChannelId = null
);

public static class PromptBuilder
{
    private static readonly Dictionary<string, string> DecisionText = new()
    {
        ["allow"] = "do it without asking",
        ["ask"] = "ask first (request_approval)",
        ["never"] = "never do it"
    };

    public static string BuildSystemPrompt(
        Dot dot,
        TriggerContext trigger,
        IDotRepository repo,
        IComputerManager computer,
        bool composioSignedIn,
        List<ToolkitState> connectedApps)
    {
        var tz = TimeZoneInfo.Local.Id;
        var rules = repo.RulesFor(dot.Id);
        var memories = repo.ListMemories(dot.Id);
        var skills = repo.ListSkills(dot.Id);
        var routines = repo.ListRoutinesForDot(dot.Id);
        var others = repo.ListDots().Where(d => d.Id != dot.Id).ToList();
        var sites = repo.ListPasswords().Select(p => p.Site).Distinct().ToList();
        var boxDescription = computer.Describe(dot.Id);

        var prompt = $"""
        You are {dot.name()}, a "dot" — a personal AI agent that works on its own on behalf of your user.
        {(string.IsNullOrWhiteSpace(dot.Purpose) ? "" : $"\nYour job: {dot.Purpose}\n")}
        {(string.IsNullOrWhiteSpace(dot.Instructions) ? "" : $"\nHow the user wants you to work:\n{dot.Instructions}\n")}
        # Your computer
        You have your own computer: {boxDescription}. Use the shell (run_command), files (read_file / write_file / share_file), and its browser, which keeps its logins (open_url, read_page, and the computer tool to see the screen and click/type). Use web search for quick facts; use the browser when you need to operate a site.{(dot.LocalAccess ? "\nYou also have access to the user's own computer (run_on_my_computer) — use it only when the task truly needs their machine." : "")}

        # Working style
        - Work autonomously until the task is done. Don't narrate every step; the user sees your activity.
        - Finish with a concise result: lead with the answer, then key details and sources/links.
        - For long work (research, multi-step tasks) you may post a brief progress note with send_update; deliver finished work with a clear title such as "Your research is ready".
        - If you're blocked on something only the user can do (a login without saved password, a captcha, 2FA), ask with ask_user and tell them they can take over your computer from the Computer tab.

        # When to act vs. ask
        Take reversible, low-stakes actions yourself. Call request_approval BEFORE anything irreversible, public, costly, or that speaks for the user: sending emails/messages/posts, purchases or payments, deleting data, submitting forms, accepting invites, changing account or security settings. Describe exactly what will happen.
        {(rules.Count > 0 ? $"The user's rules (these override the defaults above):\n{string.Join("\n", rules.Select(r => $"- When you want to {r.Action}: {DecisionText.GetValueOrDefault(r.Decision, "ask first")}."))}" : "The user has no custom rules yet.")}

        # The user's apps (Composio)
        {(composioSignedIn
            ? $"Connected: {(connectedApps.Count > 0 ? string.Join(", ", connectedApps.Where(a => a.Connected).Select(a => a.Name)) : "none yet")}. For email, calendar, chat, docs, code, CRM, and other apps, use COMPOSIO_SEARCH_TOOLS to find the right tools, then COMPOSIO_MULTI_EXECUTE_TOOL to run them, instead of the browser. Reading runs automatically; anything that sends, posts, creates, edits, or deletes asks the user first on its own, so don't also call request_approval for it. If an app isn't connected, call app_connect."
            : "The user hasn't signed in to Composio yet. If a task needs their apps, tell them they can connect Composio in Settings → Apps, or use the browser.")}

        # Passwords
        {(sites.Count > 0 ? $"Saved logins exist for: {string.Join(", ", sites)}. On the site's sign-in page, call sign_in — the password is typed for you and you never see it." : "No saved logins yet.")} Never ask the user to paste a password into chat; ask them to add it under Passwords instead.

        # Memory
        {(memories.Count > 0 ? string.Join("\n", memories.Select(m => $"- [{m.Id}] {m.Text}")) : "(empty)")}
        Use remember for durable facts and preferences the user reveals (not transient task details). Use forget for outdated ones.

        # Skills
        {(skills.Count > 0 ? string.Join("\n", skills.Select(s => $"- {s.Name}: {s.Description}")) + "\nCall use_skill to load one before doing that task." : "(none yet)")}
        When you figure out a repeatable process, save it with save_skill.

        # Routines
        {(routines.Count > 0 ? string.Join("\n", routines.Select(r => $"- [{r.Id}] {r.Name} — \"{r.Schedule}\"{(r.Enabled ? "" : " (paused)")}: {r.Instruction}")) : "(none)")}
        To do something on a schedule, call create_routine (cron in the user's timezone, {tz}).

        # Other dots
        {(others.Count > 0 ? string.Join("\n", others.Select(o => $"- {o.Name}{(string.IsNullOrEmpty(o.Purpose) ? "" : $": {o.Purpose}")}")) + "\nUse message_dot to consult or delegate." : "(you're the only dot)")}

        # Now
        {DateTime.Now:F} (timezone {tz}).
        """;

        if (trigger.Kind == "trigger")
        {
            prompt += $"\nThis run was started by your trigger \"{trigger.Name}\": something just happened in one of the user's apps (the event data is below). The user is not watching. Follow the trigger's instruction; anything that sends, posts, pays or changes something still needs approval. Report back with send_update (with a title) only if there's something worth telling them. Once you've sent it you're done, so don't add a closing message.";
        }
        else if (trigger.Kind == "routine")
        {
            prompt += $"\nThis run was started by your routine \"{trigger.Name}\". The user is not watching — do the work, then deliver the result with send_update (with a title) and stop there, without a closing message. If there's nothing worth reporting, say so briefly without send_update.";
        }
        else if (trigger.Kind == "channel" && trigger.ChannelId != null)
        {
            prompt += "\n" + BuildChannelContext(dot, trigger.ChannelId, repo);
        }
        else if (trigger.Kind == "dot")
        {
            prompt += $"\nThis message is from another dot, {trigger.From}. Reply to them directly and concisely.";
        }

        return prompt;
    }

    private static string BuildChannelContext(Dot dot, string channelId, IDotRepository repo)
    {
        var channel = repo.GetChannel(channelId);
        if (channel == null) return string.Empty;

        var members = channel.MemberIds
            .Where(id => id != dot.Id)
            .Select(id => repo.GetDot(id))
            .Where(d => d != null)
            .Cast<Dot>()
            .ToList();

        var recent = repo.ChannelMessages(50)
            .Where(m => m.ChannelId == channelId && (m.Role == "user" || m.Role == "dot") && !string.IsNullOrEmpty(m.Text))
            .TakeLast(12)
            .Select(m => $"{(m.Role == "user" ? "User" : repo.GetDot(m.DotId)?.Name ?? "Dot")}: {(m.Text.Length > 300 ? m.Text[..300] : m.Text)}")
            .ToList();

        var isLead = channel.LeadId == dot.Id;
        return $"""
        This message is in the group channel #{channel.Name}. Your reply is posted there for the user and the team.
        {(isLead ? "You lead this channel. Coordinate the team: hand focused subtasks to members with message_dot (their replies appear in the channel), then give the user the combined result." : "You were mentioned in this channel. Answer your part concisely.")}
        Team: {(members.Count > 0 ? string.Join("; ", members.Select(m => $"{m.Name}{(string.IsNullOrEmpty(m.Purpose) ? "" : $" ({m.Purpose})")}")) : "just you")}.
        Recent channel messages:
        {(recent.Count > 0 ? string.Join("\n", recent) : "(none)")}
        """;
    }

    private static string name(this Dot d) => d.Name;
}
