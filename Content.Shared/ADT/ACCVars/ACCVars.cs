using Robust.Shared.Configuration;

namespace Content.Shared.ADT.ACCVars;

[CVarDefs]
public sealed class ACCVars
{
    public static readonly CVarDef<string> RabbitMQConnectionString =
        CVarDef.Create("rabbitmq.connection_string", "", CVar.SERVERONLY);
}
