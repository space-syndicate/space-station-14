namespace Content.Corvax.Interfaces.Server;

public interface IServerJoinQueueManager
{
    public int PlayerInQueueCount { get; }
    public int ActualPlayersCount { get; }
    public void Initialize();
}
