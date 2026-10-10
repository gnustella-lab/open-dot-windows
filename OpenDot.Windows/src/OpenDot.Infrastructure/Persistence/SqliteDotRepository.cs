using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Infrastructure.Persistence;

public class SqliteDotRepository : IDotRepository
{
    private readonly string _dbPath;
    private readonly object _lock = new();

    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDot");

    public SqliteDotRepository(string? dataDir = null)
    {
        var dir = dataDir ?? DefaultDataDirectory;
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "dots.db");
        InitializeDatabase();
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private void InitializeDatabase()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS dots (
              id TEXT PRIMARY KEY, name TEXT NOT NULL, purpose TEXT NOT NULL DEFAULT '', instructions TEXT NOT NULL DEFAULT '',
              look TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'idle', local_access INTEGER NOT NULL DEFAULT 0,
              thread TEXT, pending TEXT, model TEXT, box_id TEXT, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS messages (
              id TEXT PRIMARY KEY, dot_id TEXT NOT NULL, role TEXT NOT NULL, text TEXT NOT NULL DEFAULT '', title TEXT,
              card TEXT, source TEXT, attachments TEXT, channel_id TEXT, conversation_id TEXT, created_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS messages_dot ON messages(dot_id, created_at);
            CREATE INDEX IF NOT EXISTS messages_conv ON messages(conversation_id, created_at);

            CREATE TABLE IF NOT EXISTS rules (
              id TEXT PRIMARY KEY, dot_id TEXT, action TEXT NOT NULL, decision TEXT NOT NULL, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS memories (
              id TEXT PRIMARY KEY, dot_id TEXT NOT NULL, text TEXT NOT NULL, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS skills (
              id TEXT PRIMARY KEY, dot_id TEXT NOT NULL, name TEXT NOT NULL, description TEXT NOT NULL, body TEXT NOT NULL, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS routines (
              id TEXT PRIMARY KEY, dot_id TEXT NOT NULL, name TEXT NOT NULL, instruction TEXT NOT NULL, schedule TEXT NOT NULL,
              enabled INTEGER NOT NULL DEFAULT 1, last_run_at INTEGER, next_run_at INTEGER, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS triggers (
              id TEXT PRIMARY KEY, dot_id TEXT NOT NULL, composio_id TEXT NOT NULL, slug TEXT NOT NULL, toolkit TEXT NOT NULL,
              name TEXT NOT NULL, config TEXT NOT NULL, instruction TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1,
              last_fired_at INTEGER, last_error TEXT, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS files (
              id TEXT PRIMARY KEY, dot_id TEXT NOT NULL, name TEXT NOT NULL, mime TEXT NOT NULL, size INTEGER NOT NULL,
              source TEXT NOT NULL, box_path TEXT, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS channels (
              id TEXT PRIMARY KEY, name TEXT NOT NULL, lead_id TEXT NOT NULL, members TEXT NOT NULL, created_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS conversations (
              id TEXT PRIMARY KEY, dot_id TEXT NOT NULL, title TEXT NOT NULL, kind TEXT NOT NULL DEFAULT 'chat', ref TEXT,
              thread TEXT, pending TEXT, history TEXT, created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS conversations_dot ON conversations(dot_id, updated_at);

            CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS passwords (id TEXT PRIMARY KEY, site TEXT NOT NULL, username TEXT NOT NULL, secret TEXT NOT NULL, created_at INTEGER NOT NULL);
            """;
            cmd.ExecuteNonQuery();
        }
    }

    private static string Id(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    // ---------- Dots ----------
    public List<Dot> ListDots()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, purpose, instructions, look, status, local_access, model, box_id, created_at FROM dots ORDER BY created_at ASC";
            using var reader = cmd.ExecuteReader();
            var list = new List<Dot>();
            while (reader.Read())
            {
                list.Add(new Dot
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Purpose = reader.GetString(2),
                    Instructions = reader.GetString(3),
                    Look = JsonSerializer.Deserialize<Look>(reader.GetString(4)) ?? new Look(),
                    Status = reader.GetString(5),
                    LocalAccess = reader.GetInt32(6) != 0,
                    Model = reader.IsDBNull(7) ? null : reader.GetString(7),
                    BoxId = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CreatedAt = reader.GetInt64(9)
                });
            }
            return list;
        }
    }

    public Dot? GetDot(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, purpose, instructions, look, status, local_access, model, box_id, created_at FROM dots WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new Dot
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Purpose = reader.GetString(2),
                Instructions = reader.GetString(3),
                Look = JsonSerializer.Deserialize<Look>(reader.GetString(4)) ?? new Look(),
                Status = reader.GetString(5),
                LocalAccess = reader.GetInt32(6) != 0,
                Model = reader.IsDBNull(7) ? null : reader.GetString(7),
                BoxId = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt = reader.GetInt64(9)
            };
        }
    }

    public Dot? FindDotByName(string name)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, purpose, instructions, look, status, local_access, model, box_id, created_at FROM dots WHERE lower(name) = lower(@name) LIMIT 1";
            cmd.Parameters.AddWithValue("@name", name);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new Dot
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Purpose = reader.GetString(2),
                Instructions = reader.GetString(3),
                Look = JsonSerializer.Deserialize<Look>(reader.GetString(4)) ?? new Look(),
                Status = reader.GetString(5),
                LocalAccess = reader.GetInt32(6) != 0,
                Model = reader.IsDBNull(7) ? null : reader.GetString(7),
                BoxId = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt = reader.GetInt64(9)
            };
        }
    }

    public Dot CreateDot(string name, string purpose, string instructions, Look look, bool localAccess, string? model = null)
    {
        var dot = new Dot
        {
            Id = Id("dot"),
            Name = name,
            Purpose = purpose,
            Instructions = instructions,
            Look = look,
            Status = "idle",
            LocalAccess = localAccess,
            Model = model,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            INSERT INTO dots (id, name, purpose, instructions, look, status, local_access, model, created_at)
            VALUES (@id, @name, @purpose, @instructions, @look, @status, @localAccess, @model, @createdAt)
            """;
            cmd.Parameters.AddWithValue("@id", dot.Id);
            cmd.Parameters.AddWithValue("@name", dot.Name);
            cmd.Parameters.AddWithValue("@purpose", dot.Purpose);
            cmd.Parameters.AddWithValue("@instructions", dot.Instructions);
            cmd.Parameters.AddWithValue("@look", JsonSerializer.Serialize(dot.Look));
            cmd.Parameters.AddWithValue("@status", dot.Status);
            cmd.Parameters.AddWithValue("@localAccess", dot.LocalAccess ? 1 : 0);
            cmd.Parameters.AddWithValue("@model", (object?)dot.Model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", dot.CreatedAt);
            cmd.ExecuteNonQuery();

            // Create initial conversation
            CreateConversation(dot.Id, "First chat");
        }

        return dot;
    }

    public void UpdateDot(Dot dot)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            UPDATE dots SET name = @name, purpose = @purpose, instructions = @instructions,
            look = @look, status = @status, local_access = @localAccess, model = @model, box_id = @boxId
            WHERE id = @id
            """;
            cmd.Parameters.AddWithValue("@id", dot.Id);
            cmd.Parameters.AddWithValue("@name", dot.Name);
            cmd.Parameters.AddWithValue("@purpose", dot.Purpose);
            cmd.Parameters.AddWithValue("@instructions", dot.Instructions);
            cmd.Parameters.AddWithValue("@look", JsonSerializer.Serialize(dot.Look));
            cmd.Parameters.AddWithValue("@status", dot.Status);
            cmd.Parameters.AddWithValue("@localAccess", dot.LocalAccess ? 1 : 0);
            cmd.Parameters.AddWithValue("@model", (object?)dot.Model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@boxId", (object?)dot.BoxId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void UpdateDotStatus(string id, string status, string? activity = null)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE dots SET status = @status WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteDot(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            DELETE FROM dots WHERE id = @id;
            DELETE FROM messages WHERE dot_id = @id;
            DELETE FROM conversations WHERE dot_id = @id;
            DELETE FROM rules WHERE dot_id = @id;
            DELETE FROM memories WHERE dot_id = @id;
            DELETE FROM skills WHERE dot_id = @id;
            DELETE FROM routines WHERE dot_id = @id;
            DELETE FROM triggers WHERE dot_id = @id;
            DELETE FROM files WHERE dot_id = @id;
            """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Messages ----------
    public List<Message> ListMessages(string dotId, int limit = 50)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, role, text, title, card, source, attachments, channel_id, conversation_id, created_at FROM messages WHERE dot_id = @dotId ORDER BY created_at ASC LIMIT @limit";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            cmd.Parameters.AddWithValue("@limit", limit);
            return ReadMessages(cmd);
        }
    }

    public List<Message> ConversationMessages(string conversationId, int limit = 50)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, role, text, title, card, source, attachments, channel_id, conversation_id, created_at FROM messages WHERE conversation_id = @convId ORDER BY created_at ASC LIMIT @limit";
            cmd.Parameters.AddWithValue("@convId", conversationId);
            cmd.Parameters.AddWithValue("@limit", limit);
            return ReadMessages(cmd);
        }
    }

    public List<Message> ChannelMessages(int limit = 100)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, role, text, title, card, source, attachments, channel_id, conversation_id, created_at FROM messages WHERE channel_id IS NOT NULL ORDER BY created_at ASC LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            return ReadMessages(cmd);
        }
    }

    private static List<Message> ReadMessages(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<Message>();
        while (reader.Read())
        {
            list.Add(new Message
            {
                Id = reader.GetString(0),
                DotId = reader.GetString(1),
                Role = reader.GetString(2),
                Text = reader.GetString(3),
                Title = reader.IsDBNull(4) ? null : reader.GetString(4),
                Card = reader.IsDBNull(5) ? null : JsonSerializer.Deserialize<CardData>(reader.GetString(5)),
                From = reader.IsDBNull(6) ? null : reader.GetString(6),
                Attachments = reader.IsDBNull(7) ? null : JsonSerializer.Deserialize<List<Attachment>>(reader.GetString(7)),
                ChannelId = reader.IsDBNull(8) ? null : reader.GetString(8),
                ConversationId = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.GetInt64(10)
            });
        }
        return list;
    }

    public Message? GetMessage(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, role, text, title, card, source, attachments, channel_id, conversation_id, created_at FROM messages WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            var list = ReadMessages(cmd);
            return list.FirstOrDefault();
        }
    }

    public Message AddMessage(Message message)
    {
        if (string.IsNullOrEmpty(message.Id)) message.Id = Id("msg");
        if (message.CreatedAt == 0) message.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            INSERT INTO messages (id, dot_id, role, text, title, card, source, attachments, channel_id, conversation_id, created_at)
            VALUES (@id, @dotId, @role, @text, @title, @card, @source, @attachments, @channelId, @conversationId, @createdAt)
            """;
            cmd.Parameters.AddWithValue("@id", message.Id);
            cmd.Parameters.AddWithValue("@dotId", message.DotId);
            cmd.Parameters.AddWithValue("@role", message.Role);
            cmd.Parameters.AddWithValue("@text", message.Text);
            cmd.Parameters.AddWithValue("@title", (object?)message.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@card", message.Card != null ? JsonSerializer.Serialize(message.Card) : DBNull.Value);
            cmd.Parameters.AddWithValue("@source", (object?)message.From ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@attachments", message.Attachments != null ? JsonSerializer.Serialize(message.Attachments) : DBNull.Value);
            cmd.Parameters.AddWithValue("@channelId", (object?)message.ChannelId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@conversationId", (object?)message.ConversationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", message.CreatedAt);
            cmd.ExecuteNonQuery();

            if (!string.IsNullOrEmpty(message.ConversationId))
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = "UPDATE conversations SET updated_at = @now WHERE id = @convId";
                updateCmd.Parameters.AddWithValue("@now", message.CreatedAt);
                updateCmd.Parameters.AddWithValue("@convId", message.ConversationId);
                updateCmd.ExecuteNonQuery();
            }
        }
        return message;
    }

    public void UpdateMessage(Message message)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE messages SET text = @text, title = @title, card = @card WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", message.Id);
            cmd.Parameters.AddWithValue("@text", message.Text);
            cmd.Parameters.AddWithValue("@title", (object?)message.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@card", message.Card != null ? JsonSerializer.Serialize(message.Card) : DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void UpdateMessageText(string id, string text)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE messages SET text = @text WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@text", text);
            cmd.ExecuteNonQuery();
        }
    }

    public void UpdateCard(string messageId, CardData card)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE messages SET card = @card WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", messageId);
            cmd.Parameters.AddWithValue("@card", JsonSerializer.Serialize(card));
            cmd.ExecuteNonQuery();
        }
    }

    public List<Message> PendingCards(string dotId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, role, text, title, card, source, attachments, channel_id, conversation_id, created_at FROM messages WHERE dot_id = @dotId AND role = 'card' ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            var msgs = ReadMessages(cmd);
            return msgs.Where(m => m.Card?.Status == "pending").ToList();
        }
    }

    // ---------- Conversations ----------
    public List<Conversation> ListConversations(string dotId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, title, kind, ref, thread, pending, history, created_at, updated_at FROM conversations WHERE dot_id = @dotId ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            using var reader = cmd.ExecuteReader();
            var list = new List<Conversation>();
            while (reader.Read())
            {
                list.Add(new Conversation
                {
                    Id = reader.GetString(0),
                    DotId = reader.GetString(1),
                    Title = reader.GetString(2),
                    Kind = reader.GetString(3),
                    Ref = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Thread = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Pending = reader.IsDBNull(6) ? null : reader.GetString(6),
                    History = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CreatedAt = reader.GetInt64(8),
                    UpdatedAt = reader.GetInt64(9)
                });
            }
            return list;
        }
    }

    public Conversation? GetConversation(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, title, kind, ref, thread, pending, history, created_at, updated_at FROM conversations WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new Conversation
            {
                Id = reader.GetString(0),
                DotId = reader.GetString(1),
                Title = reader.GetString(2),
                Kind = reader.GetString(3),
                Ref = reader.IsDBNull(4) ? null : reader.GetString(4),
                Thread = reader.IsDBNull(5) ? null : reader.GetString(5),
                Pending = reader.IsDBNull(6) ? null : reader.GetString(6),
                History = reader.IsDBNull(7) ? null : reader.GetString(7),
                CreatedAt = reader.GetInt64(8),
                UpdatedAt = reader.GetInt64(9)
            };
        }
    }

    public Conversation CreateConversation(string dotId, string title, string kind = "chat", string? reference = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var conv = new Conversation
        {
            Id = Id("conv"),
            DotId = dotId,
            Title = title,
            Kind = kind,
            Ref = reference,
            CreatedAt = now,
            UpdatedAt = now
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            INSERT INTO conversations (id, dot_id, title, kind, ref, created_at, updated_at)
            VALUES (@id, @dotId, @title, @kind, @ref, @now, @now)
            """;
            cmd.Parameters.AddWithValue("@id", conv.Id);
            cmd.Parameters.AddWithValue("@dotId", conv.DotId);
            cmd.Parameters.AddWithValue("@title", conv.Title);
            cmd.Parameters.AddWithValue("@kind", conv.Kind);
            cmd.Parameters.AddWithValue("@ref", (object?)conv.Ref ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.ExecuteNonQuery();
        }
        return conv;
    }

    public Conversation GetOrCreateWorkConversation(string dotId, string kind, string reference, string defaultTitle)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, title, kind, ref, thread, pending, history, created_at, updated_at FROM conversations WHERE dot_id = @dotId AND ref = @ref LIMIT 1";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            cmd.Parameters.AddWithValue("@ref", reference);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Conversation
                {
                    Id = reader.GetString(0),
                    DotId = reader.GetString(1),
                    Title = reader.GetString(2),
                    Kind = reader.GetString(3),
                    Ref = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Thread = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Pending = reader.IsDBNull(6) ? null : reader.GetString(6),
                    History = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CreatedAt = reader.GetInt64(8),
                    UpdatedAt = reader.GetInt64(9)
                };
            }
        }
        return CreateConversation(dotId, defaultTitle, kind, reference);
    }

    public string LatestConversationId(string dotId)
    {
        var list = ListConversations(dotId);
        if (list.Count > 0) return list[0].Id;
        return CreateConversation(dotId, "Chat").Id;
    }

    public void UpdateConversation(Conversation conversation)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE conversations SET title = @title, updated_at = @now WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", conversation.Id);
            cmd.Parameters.AddWithValue("@title", conversation.Title);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
        }
    }

    public void SetThread(string conversationId, string? threadId, string? pendingJson)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE conversations SET thread = @thread, pending = @pending WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", conversationId);
            cmd.Parameters.AddWithValue("@thread", (object?)threadId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pending", (object?)pendingJson ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void SetConversationHistory(string conversationId, string? historyJson)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE conversations SET history = @history WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", conversationId);
            cmd.Parameters.AddWithValue("@history", (object?)historyJson ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteConversation(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM conversations WHERE id = @id; DELETE FROM messages WHERE conversation_id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Channels ----------
    public List<Channel> ListChannels()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, lead_id, members, created_at FROM channels ORDER BY created_at ASC";
            using var reader = cmd.ExecuteReader();
            var list = new List<Channel>();
            while (reader.Read())
            {
                list.Add(new Channel
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    LeadId = reader.GetString(2),
                    MemberIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(3)) ?? [],
                    CreatedAt = reader.GetInt64(4)
                });
            }
            return list;
        }
    }

    public Channel? GetChannel(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, lead_id, members, created_at FROM channels WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new Channel
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                LeadId = reader.GetString(2),
                MemberIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(3)) ?? [],
                CreatedAt = reader.GetInt64(4)
            };
        }
    }

    public Channel CreateChannel(string name, string leadId, List<string> memberIds)
    {
        var channel = new Channel
        {
            Id = Id("chan"),
            Name = name,
            LeadId = leadId,
            MemberIds = memberIds,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO channels (id, name, lead_id, members, created_at) VALUES (@id, @name, @leadId, @members, @now)";
            cmd.Parameters.AddWithValue("@id", channel.Id);
            cmd.Parameters.AddWithValue("@name", channel.Name);
            cmd.Parameters.AddWithValue("@leadId", channel.LeadId);
            cmd.Parameters.AddWithValue("@members", JsonSerializer.Serialize(channel.MemberIds));
            cmd.Parameters.AddWithValue("@now", channel.CreatedAt);
            cmd.ExecuteNonQuery();
        }
        return channel;
    }

    public void UpdateChannel(Channel channel)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE channels SET name = @name, lead_id = @leadId, members = @members WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", channel.Id);
            cmd.Parameters.AddWithValue("@name", channel.Name);
            cmd.Parameters.AddWithValue("@leadId", channel.LeadId);
            cmd.Parameters.AddWithValue("@members", JsonSerializer.Serialize(channel.MemberIds));
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteChannel(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM channels WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Rules ----------
    public List<Rule> RulesFor(string dotId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, action, decision, created_at FROM rules WHERE dot_id IS NULL OR dot_id = @dotId ORDER BY created_at ASC";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            using var reader = cmd.ExecuteReader();
            var list = new List<Rule>();
            while (reader.Read())
            {
                list.Add(new Rule
                {
                    Id = reader.GetString(0),
                    DotId = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Action = reader.GetString(2),
                    Decision = reader.GetString(3),
                    CreatedAt = reader.GetInt64(4)
                });
            }
            return list;
        }
    }

    public Rule AddRule(string? dotId, string action, string decision)
    {
        var rule = new Rule
        {
            Id = Id("rule"),
            DotId = dotId,
            Action = action,
            Decision = decision,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO rules (id, dot_id, action, decision, created_at) VALUES (@id, @dotId, @action, @decision, @now)";
            cmd.Parameters.AddWithValue("@id", rule.Id);
            cmd.Parameters.AddWithValue("@dotId", (object?)rule.DotId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@action", rule.Action);
            cmd.Parameters.AddWithValue("@decision", rule.Decision);
            cmd.Parameters.AddWithValue("@now", rule.CreatedAt);
            cmd.ExecuteNonQuery();
        }
        return rule;
    }

    public void DeleteRule(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM rules WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Memories ----------
    public List<Memory> ListMemories(string dotId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, text, created_at FROM memories WHERE dot_id = @dotId ORDER BY created_at ASC";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            using var reader = cmd.ExecuteReader();
            var list = new List<Memory>();
            while (reader.Read())
            {
                list.Add(new Memory
                {
                    Id = reader.GetString(0),
                    DotId = reader.GetString(1),
                    Text = reader.GetString(2),
                    CreatedAt = reader.GetInt64(3)
                });
            }
            return list;
        }
    }

    public Memory AddMemory(string dotId, string text)
    {
        var memory = new Memory
        {
            Id = Id("mem"),
            DotId = dotId,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO memories (id, dot_id, text, created_at) VALUES (@id, @dotId, @text, @now)";
            cmd.Parameters.AddWithValue("@id", memory.Id);
            cmd.Parameters.AddWithValue("@dotId", memory.DotId);
            cmd.Parameters.AddWithValue("@text", memory.Text);
            cmd.Parameters.AddWithValue("@now", memory.CreatedAt);
            cmd.ExecuteNonQuery();
        }
        return memory;
    }

    public void DeleteMemory(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM memories WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Skills ----------
    public List<Skill> ListSkills(string dotId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, name, description, body, created_at FROM skills WHERE dot_id = @dotId ORDER BY created_at ASC";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            using var reader = cmd.ExecuteReader();
            var list = new List<Skill>();
            while (reader.Read())
            {
                list.Add(new Skill
                {
                    Id = reader.GetString(0),
                    DotId = reader.GetString(1),
                    Name = reader.GetString(2),
                    Description = reader.GetString(3),
                    Body = reader.GetString(4),
                    CreatedAt = reader.GetInt64(5)
                });
            }
            return list;
        }
    }

    public Skill UpsertSkill(string dotId, string name, string description, string body)
    {
        var existing = ListSkills(dotId).FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var skill = existing ?? new Skill { Id = Id("skill"), DotId = dotId, CreatedAt = now };
        skill.Name = name;
        skill.Description = description;
        skill.Body = body;

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            if (existing != null)
            {
                cmd.CommandText = "UPDATE skills SET description = @description, body = @body WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", skill.Id);
            }
            else
            {
                cmd.CommandText = "INSERT INTO skills (id, dot_id, name, description, body, created_at) VALUES (@id, @dotId, @name, @description, @body, @now)";
                cmd.Parameters.AddWithValue("@id", skill.Id);
                cmd.Parameters.AddWithValue("@dotId", dotId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@now", now);
            }
            cmd.Parameters.AddWithValue("@description", description);
            cmd.Parameters.AddWithValue("@body", body);
            cmd.ExecuteNonQuery();
        }
        return skill;
    }

    public void DeleteSkill(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM skills WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Routines ----------
    public List<Routine> ListRoutines()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, name, instruction, schedule, enabled, last_run_at, next_run_at, created_at FROM routines ORDER BY created_at ASC";
            using var reader = cmd.ExecuteReader();
            var list = new List<Routine>();
            while (reader.Read())
            {
                list.Add(new Routine
                {
                    Id = reader.GetString(0),
                    DotId = reader.GetString(1),
                    Name = reader.GetString(2),
                    Instruction = reader.GetString(3),
                    Schedule = reader.GetString(4),
                    Enabled = reader.GetInt32(5) != 0,
                    LastRunAt = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    NextRunAt = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                    CreatedAt = reader.GetInt64(8)
                });
            }
            return list;
        }
    }

    public List<Routine> ListRoutinesForDot(string dotId)
    {
        return ListRoutines().Where(r => r.DotId == dotId).ToList();
    }

    public Routine? GetRoutine(string id)
    {
        return ListRoutines().FirstOrDefault(r => r.Id == id);
    }

    public Routine AddRoutine(string dotId, string name, string instruction, string schedule)
    {
        var r = new Routine
        {
            Id = Id("rtn"),
            DotId = dotId,
            Name = name,
            Instruction = instruction,
            Schedule = schedule,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO routines (id, dot_id, name, instruction, schedule, enabled, created_at) VALUES (@id, @dotId, @name, @instruction, @schedule, 1, @now)";
            cmd.Parameters.AddWithValue("@id", r.Id);
            cmd.Parameters.AddWithValue("@dotId", r.DotId);
            cmd.Parameters.AddWithValue("@name", r.Name);
            cmd.Parameters.AddWithValue("@instruction", r.Instruction);
            cmd.Parameters.AddWithValue("@schedule", r.Schedule);
            cmd.Parameters.AddWithValue("@now", r.CreatedAt);
            cmd.ExecuteNonQuery();
        }
        return r;
    }

    public void UpdateRoutine(Routine routine)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE routines SET name = @name, instruction = @instruction, schedule = @schedule, enabled = @enabled, last_run_at = @lastRun, next_run_at = @nextRun WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", routine.Id);
            cmd.Parameters.AddWithValue("@name", routine.Name);
            cmd.Parameters.AddWithValue("@instruction", routine.Instruction);
            cmd.Parameters.AddWithValue("@schedule", routine.Schedule);
            cmd.Parameters.AddWithValue("@enabled", routine.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@lastRun", (object?)routine.LastRunAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nextRun", (object?)routine.NextRunAt ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteRoutine(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM routines WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Triggers ----------
    public List<AppTrigger> ListTriggers()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, dot_id, composio_id, slug, toolkit, name, config, instruction, enabled, last_fired_at, last_error, created_at FROM triggers ORDER BY created_at ASC";
            using var reader = cmd.ExecuteReader();
            var list = new List<AppTrigger>();
            while (reader.Read())
            {
                list.Add(new AppTrigger
                {
                    Id = reader.GetString(0),
                    DotId = reader.GetString(1),
                    ComposioId = reader.GetString(2),
                    Slug = reader.GetString(3),
                    Toolkit = reader.GetString(4),
                    Name = reader.GetString(5),
                    ConfigJson = reader.GetString(6),
                    Instruction = reader.GetString(7),
                    Enabled = reader.GetInt32(8) != 0,
                    LastFiredAt = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                    LastError = reader.IsDBNull(10) ? null : reader.GetString(10),
                    CreatedAt = reader.GetInt64(11)
                });
            }
            return list;
        }
    }

    public List<AppTrigger> ListTriggersForDot(string dotId)
    {
        return ListTriggers().Where(t => t.DotId == dotId).ToList();
    }

    public AppTrigger? GetTrigger(string id)
    {
        return ListTriggers().FirstOrDefault(t => t.Id == id);
    }

    public AppTrigger AddTrigger(string dotId, string composioId, string slug, string toolkit, string name, string configJson, string instruction)
    {
        var t = new AppTrigger
        {
            Id = Id("trig"),
            DotId = dotId,
            ComposioId = composioId,
            Slug = slug,
            Toolkit = toolkit,
            Name = name,
            ConfigJson = configJson,
            Instruction = instruction,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            INSERT INTO triggers (id, dot_id, composio_id, slug, toolkit, name, config, instruction, enabled, created_at)
            VALUES (@id, @dotId, @compId, @slug, @toolkit, @name, @config, @instruction, 1, @now)
            """;
            cmd.Parameters.AddWithValue("@id", t.Id);
            cmd.Parameters.AddWithValue("@dotId", t.DotId);
            cmd.Parameters.AddWithValue("@compId", t.ComposioId);
            cmd.Parameters.AddWithValue("@slug", t.Slug);
            cmd.Parameters.AddWithValue("@toolkit", t.Toolkit);
            cmd.Parameters.AddWithValue("@name", t.Name);
            cmd.Parameters.AddWithValue("@config", t.ConfigJson);
            cmd.Parameters.AddWithValue("@instruction", t.Instruction);
            cmd.Parameters.AddWithValue("@now", t.CreatedAt);
            cmd.ExecuteNonQuery();
        }
        return t;
    }

    public void UpdateTrigger(AppTrigger trigger)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE triggers SET name = @name, instruction = @instruction, enabled = @enabled, last_fired_at = @lastFired, last_error = @lastErr WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", trigger.Id);
            cmd.Parameters.AddWithValue("@name", trigger.Name);
            cmd.Parameters.AddWithValue("@instruction", trigger.Instruction);
            cmd.Parameters.AddWithValue("@enabled", trigger.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@lastFired", (object?)trigger.LastFiredAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lastErr", (object?)trigger.LastError ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteTrigger(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM triggers WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Passwords & Vault ----------
    public List<PasswordEntry> ListPasswords()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, site, username, created_at FROM passwords ORDER BY site ASC";
            using var reader = cmd.ExecuteReader();
            var list = new List<PasswordEntry>();
            while (reader.Read())
            {
                list.Add(new PasswordEntry
                {
                    Id = reader.GetString(0),
                    Site = reader.GetString(1),
                    Username = reader.GetString(2),
                    CreatedAt = reader.GetInt64(3)
                });
            }
            return list;
        }
    }

    public void InsertPassword(string site, string username, string sealedSecret)
    {
        var id = Id("pwd");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO passwords (id, site, username, secret, created_at) VALUES (@id, @site, @username, @secret, @now)";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@site", site);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@secret", sealedSecret);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.ExecuteNonQuery();
        }
    }

    public (string Site, string Username, string SealedSecret)? SealedPasswordFor(string site)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT site, username, secret FROM passwords WHERE lower(site) = lower(@site) LIMIT 1";
            cmd.Parameters.AddWithValue("@site", site);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }
    }

    public void DeletePassword(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM passwords WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Settings ----------
    public string? GetSetting(string key)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var res = cmd.ExecuteScalar();
            return res?.ToString();
        }
    }

    public void SetSetting(string key, string? value)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            if (value == null)
            {
                cmd.CommandText = "DELETE FROM settings WHERE key = @key";
                cmd.Parameters.AddWithValue("@key", key);
            }
            else
            {
                cmd.CommandText = "INSERT INTO settings (key, value) VALUES (@key, @value) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@value", value);
            }
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Files ----------
    public List<Attachment> ListFiles(string dotId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, mime, size, box_path FROM files WHERE dot_id = @dotId ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("@dotId", dotId);
            using var reader = cmd.ExecuteReader();
            var list = new List<Attachment>();
            while (reader.Read())
            {
                list.Add(new Attachment
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Mime = reader.GetString(2),
                    Size = reader.GetInt64(3),
                    BoxPath = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
            return list;
        }
    }

    public Attachment? GetFile(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, mime, size, box_path FROM files WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new Attachment
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Mime = reader.GetString(2),
                Size = reader.GetInt64(3),
                BoxPath = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
    }

    public Attachment SaveFile(string dotId, string name, string mime, long size, string source, string? boxPath = null)
    {
        var att = new Attachment
        {
            Id = Id("file"),
            Name = name,
            Mime = mime,
            Size = size,
            BoxPath = boxPath
        };

        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            INSERT INTO files (id, dot_id, name, mime, size, source, box_path, created_at)
            VALUES (@id, @dotId, @name, @mime, @size, @source, @boxPath, @now)
            """;
            cmd.Parameters.AddWithValue("@id", att.Id);
            cmd.Parameters.AddWithValue("@dotId", dotId);
            cmd.Parameters.AddWithValue("@name", att.Name);
            cmd.Parameters.AddWithValue("@mime", att.Mime);
            cmd.Parameters.AddWithValue("@size", att.Size);
            cmd.Parameters.AddWithValue("@source", source);
            cmd.Parameters.AddWithValue("@boxPath", (object?)att.BoxPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
        }
        return att;
    }
}
