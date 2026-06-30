using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Corvax.Discord;

public sealed class AHelpDiscordRelayService
{
    private readonly IAdminManager _adminManager;
    private readonly IPlayerManager _playerManager;
    private readonly GameTicker _gameTicker;
    private readonly AHelpBwoinkReflectionAdapter _bwoinkAdapter;
    private readonly Action<SharedBwoinkSystem.BwoinkTextMessage, INetChannel> _raiseNetworkEvent;

    public AHelpDiscordRelayService(
        IAdminManager adminManager,
        IPlayerManager playerManager,
        GameTicker gameTicker,
        AHelpBwoinkReflectionAdapter bwoinkAdapter,
        Action<SharedBwoinkSystem.BwoinkTextMessage, INetChannel> raiseNetworkEvent)
    {
        _adminManager = adminManager;
        _playerManager = playerManager;
        _gameTicker = gameTicker;
        _bwoinkAdapter = bwoinkAdapter;
        _raiseNetworkEvent = raiseNetworkEvent;
    }

    public void RelayDiscordMessageToAHelp(NetUserId userId, string authorName, string text)
    {
        SendAHelpToGame(userId, AHelpDiscordRelayHelper.BuildDiscordBwoinkText(authorName, text));
        QueueWebhookMessage(userId, AHelpDiscordRelayHelper.GetDiscordRelayName(authorName), text, isAdmin: true);
    }

    public void SendAHelpToGame(NetUserId userId, string text)
    {
        var admins = GetTargetAdmins();
        var bwoinkMessage = new SharedBwoinkSystem.BwoinkTextMessage(
            userId,
            SharedBwoinkSystem.SystemUserId,
            text,
            sentAt: DateTime.Now,
            playSound: true);

        foreach (var admin in admins)
        {
            _raiseNetworkEvent(bwoinkMessage, admin);
        }

        if (_playerManager.TryGetSessionById(userId, out var session) && !admins.Contains(session.Channel))
            _raiseNetworkEvent(bwoinkMessage, session.Channel);
    }

    public void QueueWebhookMessage(NetUserId userId, string username, string text, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var messageParams = AHelpDiscordRelayHelper.BuildWebhookMessageParams(
            username,
            text,
            isAdmin,
            _gameTicker.RunLevel == GameRunLevel.InRound
                ? _gameTicker.RoundDuration().ToString("hh\\:mm\\:ss")
                : string.Empty,
            _gameTicker.RunLevel);

        _bwoinkAdapter.QueueWebhookMessage(userId, messageParams);
    }

    private IList<INetChannel> GetTargetAdmins()
    {
        return _adminManager.ActiveAdmins
            .Where(p => _adminManager.GetAdminData(p)?.HasFlag(AdminFlags.Adminhelp) ?? false)
            .Select(p => p.Channel)
            .ToList();
    }
}
