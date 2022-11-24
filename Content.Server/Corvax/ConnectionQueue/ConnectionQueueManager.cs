using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Corvax.ConnectionQueue;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server.Corvax.ConnectionQueue;

/// <summary>
///     Manages new player connections when the server is full and queues them up, granting access when a slot becomes free
/// </summary>
public sealed class ConnectionQueueManager
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;

    /// <summary>
    ///     Queue of active player sessions
    /// </summary>
    private readonly List<IPlayerSession> _queue = new(); // Real Queue class can't delete disconnected users

    private bool _isEnabled = false;

    public int PlayerInQueueCount => _queue.Count;
    public int ActualPlayersCount => _playerManager.PlayerCount - PlayerInQueueCount; // Now it's only real value with actual players count that in game
    
    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgQueueUpdate>();
        
        _cfg.OnValueChanged(CCVars.QueueEnabled, v => _isEnabled = v, true); // TODO: It probably need to kick all in queue if changes from true to false during game
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Connected:
            {
                _queue.Add(e.Session);
                ProcessQueue(false);
                break;
            }
            case SessionStatus.Disconnected:
            {
                _queue.Remove(e.Session);
                ProcessQueue(true);
                break;
            }
        }
    }

    private void ProcessQueue(bool isDisconnect)
    {
        var players = ActualPlayersCount;
        if (isDisconnect)
            players--; // Decrease currently disconnected session but that has not yet been deleted
        
        var haveFreeSlot = players < _cfg.GetCVar(CCVars.SoftMaxPlayers);
        if ((haveFreeSlot && _queue.Count > 0) || !_isEnabled)
        {
            var session = _queue.First();
            _queue.Remove(session);
            Timer.Spawn(0, session.JoinGame);
        }

        SendUpdateMessages();
    }

    private void SendUpdateMessages()
    {
        for (var i = 0; i < _queue.Count; i++)
        {
            _queue[i].ConnectedClient.SendMessage(new MsgQueueUpdate
            {
                Total = _queue.Count,
                Position = i + 1,
            });
        }
    }
}