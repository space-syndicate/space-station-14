using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Corvax.JoinQueue;
using Content.Server.Corvax.Sponsors;
using Content.Shared.Corvax.CCCVars;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;

namespace Content.Server.Corvax.PlayerSlots;

public sealed class PlayerSlotsManager
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly SponsorsManager _sponsorsManager = default!; // Corvax-Sponsors
    [Dependency] private readonly JoinQueueManager _queueManager = default!; // Corvax-Queue

    private bool _ignorePrivileged = false;
    private int _privilegedCount = 0;

    /// <summary>
    ///     Value that mostly must be used to determine total players
    ///     Ignores privileged if enabled
    /// </summary>
    public int PlayersCount
    {
        get
        {
            if (_ignorePrivileged)
                return _playerManager.PlayerCount - _privilegedCount;

            return _playerManager.PlayerCount;
        }
    }

    /// <summary>
    ///     The actual number of players currently playing
    ///     Ignore only that stay in queue
    /// </summary>
    public int InGamePlayersCount => _playerManager.PlayerCount - _queueManager.PlayerInQueueCount;

    /// <summary>
    ///     Online that must be visible publicly
    ///     Ignores privileged if enabled and in queue
    /// </summary>
    public int PublicPlayersCount => PlayersCount - _queueManager.PlayerInQueueCount;

    public void Initialize()
    {
        _cfg.OnValueChanged(CCCVars.IgnorePrivileged, val => _ignorePrivileged = val, true);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        _privilegedCount = _playerManager.Sessions
            .Where(s =>
            {
                // Skip session that raised event if it disconnect, because it is still present in the total enumeration of all sessions
                var isCurrentSession = s == e.Session;
                var isDisconnectEvent = e.NewStatus == SessionStatus.Disconnected;
                return !(isCurrentSession && isDisconnectEvent);
            })
            .Count(s =>
            {
                var isAdmin = _adminManager.IsAdmin((IPlayerSession)s);
                var havePriorityJoin =
                    _sponsorsManager.TryGetInfo(s.UserId, out var sponsor) && sponsor.HavePriorityJoin;
                return isAdmin || havePriorityJoin;
            });
    }
}
