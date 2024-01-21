using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.StationAI.Systems;

/// <summary>
/// This handles...
/// </summary>
public sealed class AIEntrySystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AIEntryComponent, MindAddedMessage>(OnStartup);
    }

    private void OnStartup(EntityUid uid, AIEntryComponent component, MindAddedMessage args)
    {
        if (!TryComp<ActorComponent>(uid, out var actorComponent))
        {
            return;
        }

        var playerSession = actorComponent.PlayerSession;

        var queue = EntityQueryEnumerator<StationAIComponent>();

        if (queue.MoveNext(out var core, out _))
        {
            _mindSystem.ControlMob(playerSession.UserId, core);
            _gameTicker.AddGameRule("BrokenAi");
        }
        else
        {
            _chatManager.DispatchServerMessage(playerSession, "Ядро ИИ не было найдено, вероятно, оно было уничтожено, вы отправляетесь обратно в лобби.");

            _gameTicker.Respawn(playerSession);
        }

        QueueDel(uid);
    }
}
