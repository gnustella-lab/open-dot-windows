using OpenDot.Core.Models;

namespace OpenDot.Core.Interfaces;

public interface IDotRepository
{
    // Dots
    List<Dot> ListDots();
    Dot? GetDot(string id);
    Dot? FindDotByName(string name);
    Dot CreateDot(string name, string purpose, string instructions, Look look, bool localAccess, string? model = null);
    void UpdateDot(Dot dot);
    void UpdateDotStatus(string id, string status, string? activity = null);
    void DeleteDot(string id);

    // Messages
    List<Message> ListMessages(string dotId, int limit = 50);
    List<Message> ConversationMessages(string conversationId, int limit = 50);
    List<Message> ChannelMessages(int limit = 100);
    Message? GetMessage(string id);
    Message AddMessage(Message message);
    void UpdateMessage(Message message);
    void UpdateMessageText(string id, string text);
    void UpdateCard(string messageId, CardData card);
    List<Message> PendingCards(string dotId);

    // Conversations
    List<Conversation> ListConversations(string dotId);
    Conversation? GetConversation(string id);
    Conversation CreateConversation(string dotId, string title, string kind = "chat", string? reference = null);
    Conversation GetOrCreateWorkConversation(string dotId, string kind, string reference, string defaultTitle);
    string LatestConversationId(string dotId);
    void UpdateConversation(Conversation conversation);
    void SetThread(string conversationId, string? threadId, string? pendingJson);
    void SetConversationHistory(string conversationId, string? historyJson);
    void DeleteConversation(string id);

    // Channels
    List<Channel> ListChannels();
    Channel? GetChannel(string id);
    Channel CreateChannel(string name, string leadId, List<string> memberIds);
    void UpdateChannel(Channel channel);
    void DeleteChannel(string id);

    // Rules
    List<Rule> RulesFor(string dotId);
    Rule AddRule(string? dotId, string action, string decision);
    void DeleteRule(string id);

    // Memories
    List<Memory> ListMemories(string dotId);
    Memory AddMemory(string dotId, string text);
    void DeleteMemory(string id);

    // Skills
    List<Skill> ListSkills(string dotId);
    Skill UpsertSkill(string dotId, string name, string description, string body);
    void DeleteSkill(string id);

    // Routines
    List<Routine> ListRoutines();
    List<Routine> ListRoutinesForDot(string dotId);
    Routine? GetRoutine(string id);
    Routine AddRoutine(string dotId, string name, string instruction, string schedule);
    void UpdateRoutine(Routine routine);
    void DeleteRoutine(string id);

    // Triggers
    List<AppTrigger> ListTriggers();
    List<AppTrigger> ListTriggersForDot(string dotId);
    AppTrigger? GetTrigger(string id);
    AppTrigger AddTrigger(string dotId, string composioId, string slug, string toolkit, string name, string configJson, string instruction);
    void UpdateTrigger(AppTrigger trigger);
    void DeleteTrigger(string id);

    // Passwords & Vault
    List<PasswordEntry> ListPasswords();
    void InsertPassword(string site, string username, string sealedSecret);
    (string Site, string Username, string SealedSecret)? SealedPasswordFor(string site);
    void DeletePassword(string id);

    // Settings
    string? GetSetting(string key);
    void SetSetting(string key, string? value);

    // Files
    List<Attachment> ListFiles(string dotId);
    Attachment? GetFile(string id);
    Attachment SaveFile(string dotId, string name, string mime, long size, string source, string? boxPath = null);
}
