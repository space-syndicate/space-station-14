using Content.Server.Actions;
using Content.Server.Bed.Sleep;
using Content.Server.Body.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.Speech.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Mobs.Components;
using Content.Shared.Timing;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.ADT.Lizard
{
    public sealed class LizardBloodSystem : EntitySystem
    {
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly ActionsSystem _actionsSystem = default!;
        [Dependency] private readonly SleepingSystem _sleepingSystem = default!;
        [Dependency] private readonly ChemistrySystem _chemistry = default!;
        [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainer = default!;
        [Dependency] private readonly ReactiveSystem _reactiveSystem = default!;
        [Dependency] private readonly ReactiveSystem _reactive = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<LizardBloodComponent, OnTemperatureChangeEvent>(ChangeTemperature);

        }

        private void ChangeTemperature(EntityUid uid, LizardBloodComponent lizard, OnTemperatureChangeEvent temperature)
        {
            //DEBUG field
            lizard.ColdCheck(temperature);

            // when lizzard is cold, he falls asleep
            if (temperature.CurrentTemperature <= lizard.SleepyTemperature)
            {

                lizard.IsSleep = true;
                _sleepingSystem.TrySleeping(uid);
                TryWakeCooldown(uid);
            }
            else if (temperature.CurrentTemperature > lizard.SleepyTemperature)
            {
                lizard.IsSleep = false;
                _sleepingSystem.TryWaking(uid);
            }

            if (temperature.CurrentTemperature > lizard.ComfortableTemperature)
            {
                lizard.LizardIsComfortable();
            }
            else
            {
                lizard.LizardIsFine();
            }

        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            //var healingSolutions = new Solution();
            //healingSolutions.AddReagent("ADTZessulsBlood", 1.0f);

            // When lizzard is awake, it produces  a reagent
            foreach (var comp in EntityQuery<LizardBloodComponent>())
            {
                if (comp.ZessulBloodCount < comp.MaxZessulBloodCount && !comp.IsTickDelay && !comp.IsSleep)
                {

                    comp.ProduceReagent();
                    comp.IsTickDelay = true;
                }

                if (comp.IsTickDelay && comp.CurrentTickDelay < comp.BloodTickDelay)
                {
                    comp.CurrentTickDelay++;
                }
                else
                {
                    comp.IsTickDelay = false;
                    comp.ResetTickDelay();
                }

                // _bloodstream.TryAddToChemicals(comp.Uid, healingSolutions);


            }
        }
        private bool TryWakeCooldown(EntityUid uid, SleepingComponent? component = null)
        {
            if (!Resolve(uid, ref component, false))
                return false;

            var curTime = _gameTiming.CurTime;

            if (curTime < component.CoolDownEnd)
            {
                return false;
            }

            component.CoolDownEnd = curTime + component.Cooldown;
            return true;
        }

    }
}
