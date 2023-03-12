namespace Content.Server.ADT.RabbitMq;

public sealed class QueueMode
{
    public const string Default = "default";
    /// <summary>
    ///     Ленивый режим. Ленивый режим заставит сохранять
    ///     как можно больше сообщений на диске, чтобы уменьшить
    ///     использование оперативной памяти
    /// </summary>
    public const string Lazy = "lazy";
}
