using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Api.AHelp;

public static class AHelpPlayerInfoHelper
{
    public static AHelpPlayerInfo BuildPlayerInfo(
        ICommonSession session,
        SharedMindSystem minds,
        SharedRoleSystem roles)
    {
        var characterName = minds.GetCharacterName(session.UserId);
        var job = "-";
        var roleNames = Array.Empty<string>();
        var antagonist = false;

        if (minds.TryGetMind(session.UserId, out var mind))
        {
            var allRoles = roles.MindGetAllRoleInfo((mind.Value.Owner, mind.Value.Comp)).ToArray();
            var jobRole = allRoles.FirstOrDefault(role => !role.Antagonist);
            roleNames = allRoles
                .Select(role => Loc.GetString(role.Name))
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(jobRole.Name))
                job = Loc.GetString(jobRole.Name);

            antagonist = allRoles.Any(role => role.Antagonist);
        }

        return new AHelpPlayerInfo(
            session.UserId.ToString(),
            session.Name,
            session.Status,
            characterName,
            job,
            roleNames,
            antagonist);
    }

    public static bool TryGetSessionByCkey(
        IPlayerManager playerManager,
        string ckey,
        [NotNullWhen(true)] out ICommonSession? session)
    {
        if (playerManager.TryGetSessionByUsername(ckey, out session))
            return true;

        session = playerManager.Sessions.FirstOrDefault(player =>
            string.Equals(player.Name, ckey, StringComparison.OrdinalIgnoreCase));

        return session != null;
    }

}

public sealed record AHelpPlayerInfo(
    string UserId,
    string Ckey,
    SessionStatus Status,
    string? CharacterName,
    string Job,
    string[] Roles,
    bool Antagonist);
