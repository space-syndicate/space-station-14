using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Linq;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpExternalApiSystem
{
    private AHelpApiOutbound.PlayerInfo BuildPlayerInfo(ICommonSession session)
    {
        var characterName = _minds.GetCharacterName(session.UserId);
        var job = "-";
        var roleNames = Array.Empty<string>();
        var antagonist = false;

        if (_minds.TryGetMind(session.UserId, out var mind))
        {
            var roles = _roles.MindGetAllRoleInfo((mind.Value.Owner, mind.Value.Comp)).ToArray();
            var jobRole = roles.FirstOrDefault(role => !role.Antagonist);
            roleNames = roles
                .Select(role => Loc.GetString(role.Name))
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(jobRole.Name))
                job = Loc.GetString(jobRole.Name);

            antagonist = roles.Any(role => role.Antagonist);
        }

        return new AHelpApiOutbound.PlayerInfo(
            session.UserId.ToString(),
            session.Name,
            session.Status.ToString(),
            characterName,
            job,
            roleNames,
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

    private void RelayDiscordMessageToAHelp(NetUserId userId, string authorName, string plainText)
    {
        _relayService.RelayDiscordMessageToAHelp(userId, authorName, plainText);
    }

    private async Task SendOkAsync(string? requestId)
    {
        await SendAsync(new AHelpApiOutbound.Response(requestId, true, null));
    }

    private async Task SendErrorAsync(string? requestId, string error)
    {
        await SendAsync(new AHelpApiOutbound.Response(requestId, false, error));
    }

    private async Task SendAsync<T>(T payload)
    {
        await _apiClient.SendAsync(payload);
    }

    private async Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        return await tcs.Task;
    }

    private async Task RunOnMainThread(Action action)
    {
        var tcs = new TaskCompletionSource();
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        await tcs.Task;
    }
}
