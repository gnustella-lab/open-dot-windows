using System.Text.Json;
using OpenDot.Core.Models;

namespace OpenDot.Core.Agents;

public static class ToolRegistry
{
    private static string Str(Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var val) && val != null ? val.ToString() ?? "" : "";

    private static bool Bool(Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var val) && val is bool b && b;

    public static List<ToolDefinition> GetDefaultTools()
    {
        return
        [
            new ToolDefinition
            {
                Name = "run_command",
                Label = "Running commands",
                Description = "Run a command on your own computer (the working directory is your persistent workspace). Use it for scripts, data work, downloads, installing packages, etc.",
                ParametersJson = """{"type":"object","properties":{"command":{"type":"string","description":"The command to run"}},"required":["command"],"additionalProperties":false}""",
                Describe = (args, dot) => $"run `{Str(args, "command")}` on its own computer",
                DefaultDecision = (args, dot) => "allow",
                ExecuteAsync = async (args, ctx, ct) => await ctx.Computer.RunCommandAsync(ctx.Dot.Id, Str(args, "command"), ct)
            },

            new ToolDefinition
            {
                Name = "read_file",
                Label = "Reading a file",
                Description = "Read a text file from your workspace.",
                ParametersJson = """{"type":"object","properties":{"path":{"type":"string","description":"Path relative to your workspace"}},"required":["path"],"additionalProperties":false}""",
                ExecuteAsync = async (args, ctx, ct) =>
                {
                    try
                    {
                        var bytes = await ctx.Computer.ReadFileAsync(ctx.Dot.Id, Str(args, "path"), ct);
                        var text = System.Text.Encoding.UTF8.GetString(bytes);
                        return text.Length > 30000 ? text[..30000] : text;
                    }
                    catch (Exception ex)
                    {
                        return $"No such file: {Str(args, "path")} ({ex.Message})";
                    }
                }
            },

            new ToolDefinition
            {
                Name = "write_file",
                Label = "Writing a file",
                Description = "Create or overwrite a text file in your workspace (reports, notes, code). Use share_file to hand a finished file to the user.",
                ParametersJson = """{"type":"object","properties":{"path":{"type":"string","description":"Path relative to your workspace"},"content":{"type":"string","description":"Full file contents"}},"required":["path","content"],"additionalProperties":false}""",
                ExecuteAsync = async (args, ctx, ct) =>
                {
                    var content = Str(args, "content");
                    var path = await ctx.Computer.WriteFileAsync(ctx.Dot.Id, Str(args, "path"), content, ct);
                    return $"Wrote {content.Length} chars to {path}";
                }
            },

            new ToolDefinition
            {
                Name = "share_file",
                Label = "Sharing a file",
                Description = "Send a file from your computer to the user in chat (reports, spreadsheets, images, exports, code). They get a notification and can preview or download it. Write the file first, then share it.",
                ParametersJson = """{"type":"object","properties":{"path":{"type":"string","description":"Path of the file in your workspace"},"note":{"type":"string","description":"A short message to go with it"}},"required":["path","note"],"additionalProperties":false}""",
                ExecuteAsync = async (args, ctx, ct) =>
                {
                    var path = Str(args, "path");
                    var note = Str(args, "note");
                    var bytes = await ctx.Computer.ReadFileAsync(ctx.Dot.Id, path, ct);
                    var fileName = System.IO.Path.GetFileName(path);
                    var att = ctx.Repository.SaveFile(ctx.Dot.Id, fileName, "application/octet-stream", bytes.Length, "chat", path);

                    var msgText = !string.IsNullOrWhiteSpace(note) ? note : $"Here's {fileName}.";
                    ctx.Repository.AddMessage(new Message
                    {
                        DotId = ctx.Dot.Id,
                        Role = "dot",
                        Text = msgText,
                        Attachments = [att]
                    });

                    return $"Shared {att.Name} ({att.Size} bytes) with the user. Don't repeat its contents unless asked.";
                }
            },

            new ToolDefinition
            {
                Name = "open_url",
                Label = "Browsing the web",
                Description = "Open a URL in your browser (you'll see it via the computer tool / read_page). Your browser keeps its logins.",
                ParametersJson = """{"type":"object","properties":{"url":{"type":"string","description":"URL to open"}},"required":["url"],"additionalProperties":false}""",
                Describe = (args, dot) => $"open {Str(args, "url")} in its browser",
                DefaultDecision = (args, dot) => "allow",
                ExecuteAsync = async (args, ctx, ct) => await ctx.Computer.OpenUrlAsync(ctx.Dot.Id, Str(args, "url"), ct)
            },

            new ToolDefinition
            {
                Name = "read_page",
                Label = "Reading the web",
                Description = "Get the visible text of the page currently open in your browser.",
                ParametersJson = """{"type":"object","properties":{},"required":[],"additionalProperties":false}""",
                ExecuteAsync = async (args, ctx, ct) => await ctx.Computer.ReadPageAsync(ctx.Dot.Id, ct)
            },

            new ToolDefinition
            {
                Name = "click",
                Label = "Using its computer",
                Description = "Click something on the page open in your browser by its visible text (a button, link, tab, option, checkbox or label). Read the page first so you use the exact text.",
                ParametersJson = """{"type":"object","properties":{"text":{"type":"string","description":"The visible text of what to click"}},"required":["text"],"additionalProperties":false}""",
                Describe = (args, dot) => $"click \"{Str(args, "text")}\" in its browser",
                DefaultDecision = (args, dot) =>
                {
                    var text = Str(args, "text").ToLowerInvariant();
                    var risky = text.Contains("buy") || text.Contains("pay") || text.Contains("delete") || text.Contains("confirm") || text.Contains("submit") || text.Contains("order");
                    return risky ? "ask" : "allow";
                },
                ExecuteAsync = async (args, ctx, ct) => await ctx.Computer.ClickTextAsync(ctx.Dot.Id, Str(args, "text"), ct)
            },

            new ToolDefinition
            {
                Name = "type_text",
                Label = "Using its computer",
                Description = "Type into a field on the page open in your browser, found by its label, placeholder or name. Set submit to press Enter afterwards. Never use it for passwords (use sign_in).",
                ParametersJson = """{"type":"object","properties":{"field":{"type":"string","description":"Label, placeholder or name of the field"},"text":{"type":"string","description":"What to type"},"submit":{"type":"boolean","description":"Press Enter after typing"}},"required":["field","text","submit"],"additionalProperties":false}""",
                Describe = (args, dot) => $"type \"{(Str(args, "text").Length > 60 ? Str(args, "text")[..60] + "..." : Str(args, "text"))}\" into \"{Str(args, "field")}\" in its browser",
                DefaultDecision = (args, dot) => "allow",
                ExecuteAsync = async (args, ctx, ct) => await ctx.Computer.TypeTextAsync(ctx.Dot.Id, Str(args, "field"), Str(args, "text"), Bool(args, "submit"), ct)
            },

            new ToolDefinition
            {
                Name = "sign_in",
                Label = "Signing in",
                Description = "Fill the login form on the current browser page using the user's saved password for a site. You never see the password. Open the site's sign-in page first, then submit the form yourself afterwards.",
                ParametersJson = """{"type":"object","properties":{"site":{"type":"string","description":"Site/domain of the login, e.g. github.com"}},"required":["site"],"additionalProperties":false}""",
                Describe = (args, dot) => $"sign in to {Str(args, "site")} with the user's saved password",
                DefaultDecision = (args, dot) => "ask",
                ExecuteAsync = async (args, ctx, ct) =>
                {
                    var site = Str(args, "site");
                    var cred = ctx.Vault.CredentialFor(site);
                    if (cred == null)
                        return $"No saved password for {site}. Ask the user to add one under Passwords (never ask them to paste it in chat), or to take over your computer and log in themselves.";
                    return await ctx.Computer.FillLoginAsync(ctx.Dot.Id, cred.Value.Username, cred.Value.Password, ct);
                }
            },

            new ToolDefinition
            {
                Name = "run_on_my_computer",
                Label = "On your computer",
                Description = "Run a command on the USER's own computer (Windows). Only use when the task truly needs their machine; prefer your own computer.",
                ParametersJson = """{"type":"object","properties":{"command":{"type":"string","description":"The command to run on the user's computer"}},"required":["command"],"additionalProperties":false}""",
                Describe = (args, dot) => $"run `{Str(args, "command")}` on the user's personal computer",
                DefaultDecision = (args, dot) => "ask",
                ExecuteAsync = async (args, ctx, ct) =>
                {
                    if (!ctx.Dot.LocalAccess)
                        return "You no longer have access to the user's computer. They can allow access again from this dot's settings on that computer.";
                    return await ctx.Computer.RunOnUserComputerAsync(Str(args, "command"), ct);
                }
            },

            new ToolDefinition
            {
                Name = "remember",
                Label = "Remembering",
                Description = "Save a durable fact or preference about the user or their work to your memory, so you know it in future conversations.",
                ParametersJson = """{"type":"object","properties":{"fact":{"type":"string","description":"The fact, written as a short standalone sentence"}},"required":["fact"],"additionalProperties":false}""",
                ExecuteAsync = (args, ctx, ct) =>
                {
                    ctx.Repository.AddMemory(ctx.Dot.Id, Str(args, "fact"));
                    return Task.FromResult("Saved to memory.");
                }
            },

            new ToolDefinition
            {
                Name = "forget",
                Label = "Updating memory",
                Description = "Delete a memory that is wrong or outdated.",
                ParametersJson = """{"type":"object","properties":{"memory_id":{"type":"string","description":"The id shown in your memory list"}},"required":["memory_id"],"additionalProperties":false}""",
                ExecuteAsync = (args, ctx, ct) =>
                {
                    ctx.Repository.DeleteMemory(Str(args, "memory_id"));
                    return Task.FromResult("Forgotten.");
                }
            },

            new ToolDefinition
            {
                Name = "save_skill",
                Label = "Learning a skill",
                Description = "Save a reusable skill: step-by-step markdown instructions for a task you'll repeat. Updates the skill if the name exists.",
                ParametersJson = """{"type":"object","properties":{"name":{"type":"string","description":"Short skill name"},"description":{"type":"string","description":"One line: when to use it"},"instructions":{"type":"string","description":"Markdown instructions"}},"required":["name","description","instructions"],"additionalProperties":false}""",
                ExecuteAsync = (args, ctx, ct) =>
                {
                    var name = Str(args, "name");
                    ctx.Repository.UpsertSkill(ctx.Dot.Id, name, Str(args, "description"), Str(args, "instructions"));
                    return Task.FromResult($"Skill \"{name}\" saved.");
                }
            },

            new ToolDefinition
            {
                Name = "use_skill",
                Label = "Using a skill",
                Description = "Load the full instructions of one of your saved skills.",
                ParametersJson = """{"type":"object","properties":{"name":{"type":"string","description":"Skill name"}},"required":["name"],"additionalProperties":false}""",
                ExecuteAsync = (args, ctx, ct) =>
                {
                    var name = Str(args, "name");
                    var skill = ctx.Repository.ListSkills(ctx.Dot.Id).FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    return Task.FromResult(skill != null ? skill.Body : $"No skill named \"{name}\".");
                }
            },

            new ToolDefinition
            {
                Name = "create_routine",
                Label = "Setting up a routine",
                Description = "Create a recurring task you'll run on a schedule on your own, e.g. a morning briefing. Results reach the user via send_update.",
                ParametersJson = """{"type":"object","properties":{"name":{"type":"string","description":"Short name"},"instruction":{"type":"string","description":"What to do each time, written as a full instruction to yourself"},"schedule":{"type":"string","description":"5-field cron expression in user's timezone, e.g. '0 8 * * 1-5' for weekdays at 8am"}},"required":["name","instruction","schedule"],"additionalProperties":false}""",
                Describe = (args, dot) => $"set up a recurring routine \"{Str(args, "name")}\" ({Str(args, "schedule")})",
                DefaultDecision = (args, dot) => "allow",
                ExecuteAsync = (args, ctx, ct) =>
                {
                    var schedule = Str(args, "schedule");
                    var r = ctx.Repository.AddRoutine(ctx.Dot.Id, Str(args, "name"), Str(args, "instruction"), schedule);
                    return Task.FromResult($"Routine created (id {r.Id}). Next run: {(r.NextRunAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(r.NextRunAt.Value).ToString("G") : "unknown")}.");
                }
            },

            new ToolDefinition
            {
                Name = "delete_routine",
                Label = "Updating routines",
                Description = "Delete one of your routines.",
                ParametersJson = """{"type":"object","properties":{"routine_id":{"type":"string","description":"Routine id"}},"required":["routine_id"],"additionalProperties":false}""",
                Describe = (args, dot) => $"delete routine {Str(args, "routine_id")}",
                DefaultDecision = (args, dot) => "allow",
                ExecuteAsync = (args, ctx, ct) =>
                {
                    ctx.Repository.DeleteRoutine(Str(args, "routine_id"));
                    return Task.FromResult("Routine deleted.");
                }
            },

            new ToolDefinition
            {
                Name = "send_update",
                Label = "Messaging you",
                Description = "Proactively message the user with a notification — for progress on long work, or to deliver results of background/routine work. Give finished deliverables a short title like 'Your research is ready'.",
                ParametersJson = """{"type":"object","properties":{"title":{"type":["string","null"],"description":"Short notification title, or null"},"text":{"type":"string","description":"The message (markdown)"}},"required":["title","text"],"additionalProperties":false}""",
                ExecuteAsync = (args, ctx, ct) =>
                {
                    var title = Str(args, "title");
                    var text = Str(args, "text");
                    ctx.Repository.AddMessage(new Message
                    {
                        DotId = ctx.Dot.Id,
                        Role = "dot",
                        Text = text,
                        Title = string.IsNullOrWhiteSpace(title) ? null : title
                    });
                    return Task.FromResult("Delivered to the user.");
                }
            },

            new ToolDefinition
            {
                Name = "message_dot",
                Label = "Messaging another dot",
                Description = "Ask another of the user's dots for help or hand off a sub-task. Returns their reply.",
                ParametersJson = """{"type":"object","properties":{"dot_name":{"type":"string","description":"The other dot's name"},"message":{"type":"string","description":"Your message to them, with all needed context"}},"required":["dot_name","message"],"additionalProperties":false}""",
                Describe = (args, dot) => $"message the dot \"{Str(args, "dot_name")}\"",
                DefaultDecision = (args, dot) => "allow",
                ExecuteAsync = async (args, ctx, ct) =>
                {
                    var targetName = Str(args, "dot_name");
                    var target = ctx.Repository.FindDotByName(targetName);
                    if (target == null)
                    {
                        var available = string.Join(", ", ctx.Repository.ListDots().Select(d => d.Name));
                        return $"No dot named \"{targetName}\". Available: {available}";
                    }
                    if (target.Id == ctx.Dot.Id) return "That's you.";
                    if (target.Status == "paused") return $"{target.Name} is paused.";
                    if (ctx.Depth >= 2 || ctx.ConsultDelegate == null) return "Too many nested hand-offs; do it yourself.";

                    return await ctx.ConsultDelegate(target, Str(args, "message"), ctx.Dot, ctx.Depth + 1, ct);
                }
            },

            new ToolDefinition
            {
                Name = "ask_user",
                Label = "Waiting for you",
                Description = "Ask the user a question and wait for the answer. Offer 2-4 likely answers as options when possible.",
                ParametersJson = """{"type":"object","properties":{"question":{"type":"string","description":"The question"},"options":{"type":"array","items":{"type":"string"},"description":"Suggested answers (may be empty)"}},"required":["question","options"],"additionalProperties":false}""",
                PauseKind = "question"
            },

            new ToolDefinition
            {
                Name = "request_approval",
                Label = "Waiting for approval",
                Description = "Ask the user to approve an action before you take it (sending messages on their behalf, purchases, deleting things, submitting forms, anything irreversible or public). Wait for their decision.",
                ParametersJson = """{"type":"object","properties":{"action":{"type":"string","description":"What you want to do, one line"},"details":{"type":"string","description":"Exactly what will happen: recipients, amounts, content, etc."}},"required":["action","details"],"additionalProperties":false}""",
                PauseKind = "approval"
            },

            new ToolDefinition
            {
                Name = "app_connect",
                Label = "Connecting an app",
                Description = "Ask the user to connect one of their apps to Composio (shows a Connect card with a sign-in link) and wait until they finish. Use the exact toolkit slug from COMPOSIO_SEARCH_TOOLS.",
                ParametersJson = """{"type":"object","properties":{"toolkit":{"type":"string","description":"Toolkit slug, e.g. gmail, googlecalendar, slack, notion, github"}},"required":["toolkit"],"additionalProperties":false}""",
                PauseKind = "connect"
            }
        ];
    }
}
