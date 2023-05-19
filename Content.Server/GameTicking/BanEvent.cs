namespace Content.Shared.GameTicking;

public sealed class BanEvent : EntityEventArgs
{
	public string AdminNick { get; }
    public string Username { get; }
    public DateTimeOffset? Expires { get; }
    public string Reason { get; }


    public BanEvent(string admin, string username, DateTimeOffset? expires, string reason)
    {
		AdminNick = admin;
        Username = username;
        Expires = expires;
        Reason = reason;
    }
}
