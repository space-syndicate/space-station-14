using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Paper;

namespace Content.Server.Corvax.SecretStationGoal
{
    /// <summary>
    ///     This System adds Secret Station Goal to any Entity with <see cref="PaperComponent"></see>.
    /// </summary>
    public sealed class SecretStationGoalPaperSystem : EntitySystem
    {
        [Dependency] private readonly PaperSystem _paperSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SecretStationGoalPaperComponent, ComponentInit>(OnComponentInit);
        }

        private void OnComponentInit(EntityUid uid, SecretStationGoalPaperComponent component, ComponentInit args)
        {
            AddSecretGoal(uid);
        }

        private void AddSecretGoal(EntityUid uid)
        {
            if (!TryComp<PaperComponent>(uid, out var paper))
            {
                return;
            }

            var secretGoal = PickSecretGoal();
            _paperSystem.SetContent(uid, Loc.GetString(secretGoal.Text), paper);
        }

        private SecretStationGoalPrototype PickSecretGoal()
        {
            var secretGoals = _prototypeManager.EnumeratePrototypes<SecretStationGoalPrototype>().ToList();
            return secretGoals.Any() ? _random.Pick(secretGoals) : new SecretStationGoalPrototype();
        }
    }
}
