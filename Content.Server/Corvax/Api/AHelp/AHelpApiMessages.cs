namespace Content.Server.Corvax.Api.AHelp;

public static class AHelpApiInbound
{
    public sealed record Base(string Type, string? RequestId);

    public sealed record SendAHelpMessage(
        string Type,
        string? RequestId,
        string? ConversationId,
        string? UserId,
        string? AuthorExternalId,
        string? AuthorName,
        string Text);

    public sealed record OpenAHelp(
        string Type,
        string? RequestId,
        string Ckey,
        string? AuthorExternalId,
        string? AuthorName,
        string Text);
}

public static class AHelpApiOutbound
{
    public sealed record Hello(
        string Type,
        string ProtocolVersion,
        string ServerName,
        int RoundId,
        string RunLevel)
    {
        public Hello(string protocolVersion, string serverName, int roundId, string runLevel)
            : this("hello", protocolVersion, serverName, roundId, runLevel)
        {
        }
    }

    public sealed record ConversationUpsert(
        string Type,
        string ConversationId,
        string UserId,
        string Ckey,
        string? CharacterName,
        ulong? RootMessageId,
        ulong? WebhookChannelId,
        string ServerName,
        int RoundId,
        string RunLevel)
    {
        public ConversationUpsert(
            string conversationId,
            string userId,
            string ckey,
            string? characterName,
            ulong? rootMessageId,
            ulong? webhookChannelId,
            string serverName,
            int roundId,
            string runLevel)
            : this("conversation_upsert", conversationId, userId, ckey, characterName, rootMessageId, webhookChannelId, serverName, roundId, runLevel)
        {
        }
    }

    public sealed record AHelpMessage(
        string Type,
        string ConversationId,
        string UserId,
        string Source,
        string Text,
        DateTimeOffset SentAt)
    {
        public AHelpMessage(string conversationId, string userId, string source, string text, DateTimeOffset sentAt)
            : this("ahelp_message", conversationId, userId, source, text, sentAt)
        {
        }
    }

    public sealed record PlayerStatus(
        string Type,
        string ConversationId,
        string UserId,
        string Ckey,
        string Status,
        DateTimeOffset SentAt)
    {
        public PlayerStatus(string conversationId, string userId, string ckey, string status, DateTimeOffset sentAt)
            : this("player_status", conversationId, userId, ckey, status, sentAt)
        {
        }
    }

    public sealed record RoundChanged(string Type, int RoundId, string RunLevel)
    {
        public RoundChanged(int roundId, string runLevel)
            : this("round_changed", roundId, runLevel)
        {
        }
    }

    public sealed record PlayersResponse(string Type, string? RequestId, bool Ok, string? Error, PlayerInfo[] Players)
    {
        public PlayersResponse(string? requestId, PlayerInfo[] players)
            : this("response", requestId, true, null, players)
        {
        }
    }

    public sealed record PlayerInfo(
        string UserId,
        string Ckey,
        string Status,
        string? CharacterName,
        string Job,
        string[] Roles,
        bool Antagonist);

    public sealed record Response(string Type, string? RequestId, bool Ok, string? Error)
    {
        public Response(string? requestId, bool ok, string? error)
            : this("response", requestId, ok, error)
        {
        }
    }

    public sealed record Pong(string Type, string? RequestId)
    {
        public Pong(string? requestId)
            : this("pong", requestId)
        {
        }
    }
}
