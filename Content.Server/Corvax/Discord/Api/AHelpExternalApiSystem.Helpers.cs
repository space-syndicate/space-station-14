using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Discord;

public sealed partial class AHelpExternalApiSystem
{
    private AHelpApiOutbound.PlayerInfo BuildPlayerInfo(ICommonSession session)
    {
        var info = AHelpPlayerInfoHelper.BuildPlayerInfo(session, _minds, _roles);

        return new AHelpApiOutbound.PlayerInfo(
            info.UserId,
            info.Ckey,
            info.Status.ToString(),
            info.CharacterName,
            info.Job,
            info.Roles,
            info.Antagonist);
    }

    private bool TryGetSessionByCkey(string ckey, [NotNullWhen(true)] out ICommonSession? session)
    {
        return AHelpPlayerInfoHelper.TryGetSessionByCkey(_playerManager, ckey, out session);
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
        return await _taskManager.RunOnMainThreadAsync(func);
    }

    private async Task RunOnMainThread(Action action)
    {
        await _taskManager.RunOnMainThreadAsync(action);
    }
}
