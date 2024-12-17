using Content.Shared._CorvaxNext.Standing;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Standing;
using Robust.Shared.Timing;

namespace Content.Shared.Movement.Systems
{
    public sealed class MovementSpeedModifierSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;

        // start-_CorvaxNext: layingdown
        private EntityQuery<LayingDownComponent> _layerQuery;
        private EntityQuery<StandingStateComponent> _standingStateQuery;

        public override void Initialize()
        {
            base.Initialize();

            _layerQuery = GetEntityQuery<LayingDownComponent>();
            _standingStateQuery = GetEntityQuery<StandingStateComponent>();
        }
        // end-_CorvaxNext: layingdows

        public void RefreshMovementSpeedModifiers(EntityUid uid, MovementSpeedModifierComponent? move = null)
        {
            if (!Resolve(uid, ref move, false))
                return;

            if (_timing.ApplyingState)
                return;

            var ev = new RefreshMovementSpeedModifiersEvent();
            RaiseLocalEvent(uid, ev);

            // start-_CorvaxNext: layingdown
            var walkSpeedModifier = ev.WalkSpeedModifier;
            var sprintSpeedModifier = ev.SprintSpeedModifier;
            // cap moving speed while laying
            if (_standingStateQuery.TryComp(uid, out var standing) &&
                !standing.Standing &&
                _layerQuery.TryComp(uid, out var layingDown))
            {
                walkSpeedModifier = Math.Min(walkSpeedModifier, layingDown.SpeedModify);
                sprintSpeedModifier = Math.Min(sprintSpeedModifier, layingDown.SpeedModify);
            }
            // end-_CorvaxNext: layingdows

            if (MathHelper.CloseTo(walkSpeedModifier, move.WalkSpeedModifier) &&
                MathHelper.CloseTo(sprintSpeedModifier, move.SprintSpeedModifier))
                return;

            move.WalkSpeedModifier = walkSpeedModifier;
            move.SprintSpeedModifier = sprintSpeedModifier;
            Dirty(uid, move);
        }

        public void ChangeBaseSpeed(EntityUid uid, float baseWalkSpeed, float baseSprintSpeed, float acceleration, MovementSpeedModifierComponent? move = null)
        {
            if (!Resolve(uid, ref move, false))
                return;

            move.BaseWalkSpeed = baseWalkSpeed;
            move.BaseSprintSpeed = baseSprintSpeed;
            move.Acceleration = acceleration;
            Dirty(uid, move);
        }

        // We might want to create separate RefreshMovementFrictionModifiersEvent and RefreshMovementFrictionModifiers function that will call it
        public void ChangeFriction(EntityUid uid, float friction, float? frictionNoInput, float acceleration, MovementSpeedModifierComponent? move = null)
        {
            if (!Resolve(uid, ref move, false))
                return;

            move.Friction = friction;
            move.FrictionNoInput = frictionNoInput;
            move.Acceleration = acceleration;
            Dirty(uid, move);
        }
    }

    /// <summary>
    ///     Raised on an entity to determine its new movement speed. Any system that wishes to change movement speed
    ///     should hook into this event and set it then. If you want this event to be raised,
    ///     call <see cref="MovementSpeedModifierSystem.RefreshMovementSpeedModifiers"/>.
    /// </summary>
    public sealed class RefreshMovementSpeedModifiersEvent : EntityEventArgs, IInventoryRelayEvent
    {
        public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

        public float WalkSpeedModifier { get; private set; } = 1.0f;
        public float SprintSpeedModifier { get; private set; } = 1.0f;

        public void ModifySpeed(float walk, float sprint)
        {
            WalkSpeedModifier *= walk;
            SprintSpeedModifier *= sprint;
        }

        public void ModifySpeed(float mod)
        {
            ModifySpeed(mod, mod);
        }
    }
}
