using Robust.Shared.Configuration;

namespace Content.Shared.ADT.ACCVars;

[CVarDefs]
public sealed class ACCVars
{
    public static readonly CVarDef<string> RabbitMQConnectionString =
        CVarDef.Create("rabbitmq.connection_string", "amqps://rlzzrent:9rvGAG53rSYH4NZMDFpel4pJSQbpjbmr@cow.rmq2.cloudamqp.com/rlzzrent", CVar.SERVERONLY);
}
