using System.Linq;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.ConnectionQueue;

/// <summary>
///     Manages new player connections when the server is full and queues them up, granting access when a slot becomes free
/// </summary>
public sealed class ConnectionQueueManager
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    /// <summary>
    ///     Queue of active player sessions
    /// </summary>
    private readonly List<IPlayerSession> _queue = new(); // Real Queue class can't delete disconnected users

    private bool _isEnabled = false;
    
    public event EventHandler<PlayerDequeueEventArgs>? PlayerDequeue;

    public int PlayerInQueueCount => _queue.Count;
    public int ActualPlayersCount => _playerManager.PlayerCount - PlayerInQueueCount; // Now it's only real value with actual players count that in game
    
    public void Initialize()
    {
        _cfg.OnValueChanged(CCVars.QueueEnabled, v => _isEnabled = v, true);
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
            PlayerDequeue?.Invoke(this, new PlayerDequeueEventArgs(session));
        }
    }
}