using System.Text.Json.Serialization;

namespace Content.Server.Corvax.Api.AHelp;

public sealed record AHelpApiEventRequest(
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("serverName")]
    string ServerName,
    [property: JsonPropertyName("roundId")]
    int RoundId,
    [property: JsonPropertyName("runLevel")]
    string RunLevel,
    [property: JsonPropertyName("conversations")]
    AHelpApiConversation[] Conversations);

public sealed record AHelpApiConversation(
    [property: JsonPropertyName("conversationId")]
    string ConversationId,
    [property: JsonPropertyName("userId")]
    string UserId,
    [property: JsonPropertyName("ckey")]
    string Ckey,
    [property: JsonPropertyName("characterName")]
    string? CharacterName,
    [property: JsonPropertyName("rootMessageId")]
    string? RootMessageId,
    [property: JsonPropertyName("webhookChannelId")]
    string? WebhookChannelId,
    [property: JsonPropertyName("serverName")]
    string ServerName,
    [property: JsonPropertyName("roundId")]
    int RoundId,
    [property: JsonPropertyName("runLevel")]
    string RunLevel,
    [property: JsonPropertyName("transcript")]
    string? Transcript);

public sealed record AHelpApiCommand(
    [property: JsonPropertyName("commandId")]
    string CommandId,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("conversationId")]
    string? ConversationId,
    [property: JsonPropertyName("userId")]
    string? UserId,
    [property: JsonPropertyName("ckey")]
    string? Ckey,
    [property: JsonPropertyName("authorExternalId")]
    string? AuthorExternalId,
    [property: JsonPropertyName("authorName")]
    string? AuthorName,
    [property: JsonPropertyName("text")]
    string? Text);

public sealed record AHelpApiCommandResponse(
    [property: JsonPropertyName("commandId")]
    string CommandId,
    [property: JsonPropertyName("ok")]
    bool Ok,
    [property: JsonPropertyName("error")]
    string? Error = null,
    [property: JsonPropertyName("players")]
    AHelpApiPlayerInfo[]? Players = null,
    [property: JsonPropertyName("userId")]
    string? UserId = null,
    [property: JsonPropertyName("ckey")]
    string? Ckey = null,
    [property: JsonPropertyName("characterName")]
    string? CharacterName = null,
    [property: JsonPropertyName("objectives")]
    AHelpApiObjectiveInfo[]? Objectives = null);

public sealed record AHelpApiPlayerInfo(
    [property: JsonPropertyName("userId")]
    string UserId,
    [property: JsonPropertyName("ckey")]
    string Ckey,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("characterName")]
    string? CharacterName,
    [property: JsonPropertyName("job")]
    string Job,
    [property: JsonPropertyName("roles")]
    string[] Roles,
    [property: JsonPropertyName("antagonist")]
    bool Antagonist);

public sealed record AHelpApiObjectiveInfo(
    [property: JsonPropertyName("index")]
    int Index,
    [property: JsonPropertyName("entity")]
    string Entity,
    [property: JsonPropertyName("title")]
    string? Title,
    [property: JsonPropertyName("description")]
    string? Description,
    [property: JsonPropertyName("progress")]
    int Progress,
    [property: JsonPropertyName("valid")]
    bool Valid);
