using Content.Server.Chat.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
namespace Content.Server.Sunrise.Paws
{
    public sealed class PawsSystem : EntitySystem
    {
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        public override void Initialize()
        {
            base.Initialize();
            // SubscribeLocalEvent<PawsComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<PawsComponent, DamageChangedEvent>(OnDamaged);
        }
        // private void OnMobStateChanged(EntityUid uid, PawsComponent component, MobStateChangedEvent args)
        // {
        //     if (args.NewMobState == MobState.Dead)
        //         _audioSystem.PlayPvs(component.DeadSound, uid, component.DeadSound.Params);
        // }
        private void OnDamaged(EntityUid uid, PawsComponent component, DamageChangedEvent args)
        {
            if (!_mobStateSystem.IsAlive(uid))
                return;
            if (!args.DamageIncreased)
                return;
            var curTime = _timing.CurTime;
            if (curTime < component.NextScreamTime)
                return;
            if (args.DamageDelta!.GetTotal() < component.ThresholdDamage)
                return;
            component.NextScreamTime = curTime + TimeSpan.FromSeconds(component.ScreamInterval);
            _chatSystem.TryEmoteWithChat(uid, _random.Pick(component.EmotesTakeDamage));
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var curTime = _timing.CurTime;
            var query = EntityQueryEnumerator<PawsComponent, MobStateComponent>();
            while (query.MoveNext(out var uid, out var comp, out var state))
            {
                if (state.CurrentState != MobState.Critical)
                    continue;
                if (curTime < comp.NextCoughTime)
                    return;
                comp.NextCoughTime = curTime + TimeSpan.FromSeconds(comp.CoughInterval);
                _chatSystem.TryEmoteWithChat(uid, "Cough", ignoreActionBlocker: true);
            }
        }
    }
}