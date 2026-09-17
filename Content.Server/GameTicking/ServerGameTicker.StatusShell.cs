using System.Linq;
using System.Text.Json.Nodes;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server.ServerStatus;
using Content.Corvax.Interfaces.Server;

namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    /// <summary>
    ///     Used for thread safety, given <see cref="IStatusHost.OnStatusRequest"/> is called from another thread.
    /// </summary>
    private readonly object _statusShellLock = new();

    /// <summary>
    ///     Round start time in UTC, for status shell purposes.
    /// </summary>
    [ViewVariables]
    private DateTime _roundStartDateTime;

    private void InitializeStatusShell()
    {
        IoCManager.Resolve<IStatusHost>().OnStatusRequest += GetStatusResponse;
    }

    private void GetStatusResponse(JsonNode jObject)
    {
        var preset = CurrentPreset ?? Preset;

        // This method is raised from another thread, so this better be thread safe!
        lock (_statusShellLock)
        {
            // Corvax-Queue-Start
            var players = IoCManager.Instance?.TryResolveType<IServerJoinQueueManager>(out var joinQueueManager) ?? false
                ? joinQueueManager.ActualPlayersCount
                : _playerManager.PlayerCount;

            players = Cfg.GetCVar(CCVars.AdminsCountInReportedPlayerCount)
                    ? players
                    : players - _adminManager.ActiveAdmins.Count();
            // Corvax-Queue-End

            jObject["name"] = _baseServer.ServerName;
            jObject["map"] = _gameMapManager.GetSelectedMap()?.MapName;
            jObject["round_id"] = RoundId;
            // Corvax-Queue-Start
            //jObject["players"] = Cfg.GetCVar(CCVars.AdminsCountInReportedPlayerCount)
            //    ? _playerManager.PlayerCount
            //    : _playerManager.PlayerCount - _adminManager.ActiveAdmins.Count();
            jObject["players"] = players;
            // Corvax-Queue-End
            jObject["soft_max_players"] = Cfg.GetCVar(CCVars.SoftMaxPlayers);
            jObject["panic_bunker"] = Cfg.GetCVar(CCVars.PanicBunkerEnabled);
            jObject["run_level"] = (int) RunLevel;
            if (preset != null)
                jObject["preset"] = (Decoy == null) ? Loc.GetString(preset.ModeTitle) : Loc.GetString(Decoy.ModeTitle);
            if (RunLevel >= GameRunLevel.InRound)
            {
                jObject["round_start_time"] = _roundStartDateTime.ToString("o");
            }
        }
    }
}
