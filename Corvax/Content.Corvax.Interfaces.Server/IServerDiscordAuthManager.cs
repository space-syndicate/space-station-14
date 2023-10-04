using Content.Corvax.Interfaces.Shared;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Corvax.Interfaces.Server;

public interface IServerDiscordAuthManager : ISharedDiscordAuthManager
{
    public event EventHandler<IPlayerSession>? PlayerVerified;
    public Task<string> GenerateAuthLink(NetUserId userId, CancellationToken cancel);
    public Task<bool> IsVerified(NetUserId userId, CancellationToken cancel);
}
