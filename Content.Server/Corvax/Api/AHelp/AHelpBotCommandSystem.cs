using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.Api.AHelp;

public sealed partial class AHelpBotCommandSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private BwoinkSystem _bwoinkSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    internal AHelpApiCommandResponse ExecuteCommand(AHelpApiCommand command)
    {
        try
        {
            return command.Type switch
            {
                "list_players" => new AHelpApiCommandResponse(
                    command.CommandId,
                    true,
                    null,
                    Players: _playerManager.Sessions.OrderBy(session => session.Name).Select(BuildPlayerInfo).ToArray()),
                "list_objectives" => HandleListObjectives(command),
                "open_ahelp" => HandleOpenAHelp(command),
                "send_ahelp_message" => HandleSendAHelpMessage(command),
                _ => new AHelpApiCommandResponse(command.CommandId, false, $"Unsupported command type '{command.Type}'"),
            };
        }
        catch (Exception e)
        {
            return new AHelpApiCommandResponse(command.CommandId, false, e.Message);
        }
    }

    private AHelpApiCommandResponse HandleOpenAHelp(AHelpApiCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Ckey) || string.IsNullOrWhiteSpace(command.Text))
            return new AHelpApiCommandResponse(command.CommandId, false, "ckey and text are required");

        if (!TryGetSessionByCkey(command.Ckey, out var target))
            return new AHelpApiCommandResponse(command.CommandId, false, $"Player '{command.Ckey}' not found");

        RelayExternalMessageToAHelp(target.UserId, GetAuthorName(command), command.Text);
        return new AHelpApiCommandResponse(
            command.CommandId,
            true,
            UserId: target.UserId.ToString(),
            Ckey: target.Name,
            CharacterName: _minds.GetCharacterName(target.UserId));
    }

    private AHelpApiCommandResponse HandleSendAHelpMessage(AHelpApiCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
            return new AHelpApiCommandResponse(command.CommandId, false, "text is required");

        if (!Guid.TryParse(command.ConversationId ?? command.UserId, out var userGuid))
            return new AHelpApiCommandResponse(command.CommandId, false, "conversationId or userId must be a NetUserId guid");

        var userId = new NetUserId(userGuid);
        if (!_bwoinkSystem.CorvaxHasActiveAHelpConversation(userId) &&
            !_playerManager.TryGetSessionById(userId, out _))
        {
            return new AHelpApiCommandResponse(command.CommandId, false, "AHelp conversation is not active");
        }

        RelayExternalMessageToAHelp(userId, GetAuthorName(command), command.Text);
        return new AHelpApiCommandResponse(command.CommandId, true);
    }

    private AHelpApiCommandResponse HandleListObjectives(AHelpApiCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Ckey))
            return new AHelpApiCommandResponse(command.CommandId, false, "ckey is required");

        if (!TryGetSessionByCkey(command.Ckey, out var target))
            return new AHelpApiCommandResponse(command.CommandId, false, $"Player '{command.Ckey}' not found");

        if (!_minds.TryGetMind(target, out var mindId, out var mind))
            return new AHelpApiCommandResponse(command.CommandId, false, $"Player '{target.Name}' does not have a mind");

        var objectives = mind.Objectives
            .Select((objective, index) =>
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                return info == null
                    ? new AHelpApiObjectiveInfo(index, objective.ToString(), null, null, 0, false)
                    : new AHelpApiObjectiveInfo(
                        index,
                        objective.ToString(),
                        info.Value.Title,
                        info.Value.Description,
                        (int) (info.Value.Progress * 100f),
                        true);
            })
            .ToArray();

        return new AHelpApiCommandResponse(
            command.CommandId,
            true,
            null,
            UserId: target.UserId.ToString(),
            Ckey: target.Name,
            CharacterName: _minds.GetCharacterName(target.UserId),
            Objectives: objectives);
    }

    private AHelpApiPlayerInfo BuildPlayerInfo(ICommonSession session)
    {
        string? characterName = null;
        var job = "-";
        var roleNames = new List<string>();
        var antagonist = false;
        var foundJob = false;

        if (_minds.TryGetMind(session.UserId, out var mind))
        {
            characterName = mind.Value.Comp.CharacterName;

            foreach (var role in _roles.MindGetAllRoleInfo((mind.Value.Owner, mind.Value.Comp)))
            {
                var roleName = Loc.GetString(role.Name);
                if (!string.IsNullOrWhiteSpace(roleName))
                    roleNames.Add(roleName);

                if (role.Antagonist)
                {
                    antagonist = true;
                }
                else if (!foundJob && !string.IsNullOrWhiteSpace(roleName))
                {
                    foundJob = true;
                    job = roleName;
                }
            }
        }

        return new AHelpApiPlayerInfo(
            session.UserId.ToString(),
            session.Name,
            session.Status.ToString(),
            characterName,
            job,
            roleNames.ToArray(),
            antagonist);
    }

    private bool TryGetSessionByCkey(string ckey, [NotNullWhen(true)] out ICommonSession? session)
    {
        if (_playerManager.TryGetSessionByUsername(ckey, out session))
            return true;

        session = _playerManager.Sessions.FirstOrDefault(player =>
            string.Equals(player.Name, ckey, StringComparison.OrdinalIgnoreCase));

        return session != null;
    }

    private void RelayExternalMessageToAHelp(NetUserId userId, string authorName, string text)
    {
        var plainText = text.ReplaceLineEndings(" ");
        _bwoinkSystem.CorvaxSendAHelpToGame(userId, BuildExternalBwoinkText(authorName, plainText));
        _bwoinkSystem.CorvaxQueueAHelpWebhookMessage(userId, new AHelpMessageParams(
            $"{authorName}[D]",
            plainText,
            true,
            _gameTicker.RunLevel == GameRunLevel.InRound
                ? _gameTicker.RoundDuration().ToString("hh\\:mm\\:ss")
                : string.Empty,
            _gameTicker.RunLevel,
            playedSound: true));
    }

    private static string GetAuthorName(AHelpApiCommand command)
    {
        return string.IsNullOrWhiteSpace(command.AuthorName)
            ? command.AuthorExternalId ?? "External"
            : command.AuthorName;
    }

    private static string BuildExternalBwoinkText(string authorName, string text)
    {
        return $"[color=red]{FormattedMessage.EscapeText(authorName)} \\[D\\][/color]: {FormattedMessage.EscapeText(text)}";
    }
}
