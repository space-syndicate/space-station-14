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

    public sealed record ListObjectives(
        string Type,
        string? RequestId,
        string Ckey);
}

public static class AHelpApiOutbound
{
    public sealed record Hello(
        string ProtocolVersion,
        string ServerName,
        int RoundId,
        string RunLevel)
    {
        public string Type { get; init; } = "hello";
    }

    public sealed record ConversationUpsert(
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
        public string Type { get; init; } = "conversation_upsert";
    }

    public sealed record AHelpMessage(
        string ConversationId,
        string UserId,
        string Source,
        string Text,
        DateTimeOffset SentAt)
    {
        public string Type { get; init; } = "ahelp_message";
    }

    public sealed record PlayerStatus(
        string ConversationId,
        string UserId,
        string Ckey,
        string Status,
        DateTimeOffset SentAt)
    {
        public string Type { get; init; } = "player_status";
    }

    public sealed record RoundChanged(int RoundId, string RunLevel)
    {
        public string Type { get; init; } = "round_changed";
    }

    public sealed record PlayersResponse(string? RequestId, bool Ok, string? Error, PlayerInfo[] Players)
    {
        public string Type { get; init; } = "response";
    }

    public sealed record PlayerInfo(
        string UserId,
        string Ckey,
        string Status,
        string? CharacterName,
        string Job,
        string[] Roles,
        bool Antagonist);

    public sealed record ObjectivesResponse(
        string? RequestId,
        bool Ok,
        string? Error,
        string? UserId,
        string? Ckey,
        string? CharacterName,
        ObjectiveInfo[] Objectives)
    {
        public string Type { get; init; } = "response";
    }

    public sealed record ObjectiveInfo(
        int Index,
        string Entity,
        string? Title,
        string? Description,
        int Progress,
        bool Valid);

    public sealed record Response(string? RequestId, bool Ok, string? Error)
    {
        public string Type { get; init; } = "response";
    }

    public sealed record Pong(string? RequestId)
    {
        public string Type { get; init; } = "pong";
    }
}
