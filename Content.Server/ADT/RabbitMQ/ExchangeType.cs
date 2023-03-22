namespace Content.Server.ADT.RabbitMq;

/// <summary>
///     Типы обменников
/// </summary>
public static class ExchangeType
{
    public const string Direct = "direct";
    public const string Topic = "topic";
    public const string Fanout = "fanout";
    public const string Header = "headers";
}
