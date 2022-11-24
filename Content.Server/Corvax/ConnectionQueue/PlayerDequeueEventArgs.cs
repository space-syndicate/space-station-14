using Robust.Server.Player;

namespace Content.Server.Corvax.ConnectionQueue;

public sealed class PlayerDequeueEventArgs : EventArgs
{
    public PlayerDequeueEventArgs(IPlayerSession session)
    {
        Session = session;
    }
    
    public IPlayerSession Session { get; }
}