using Content.Server.Backmen.Abilities.Psionics;
using Content.Shared.Actions;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Eye;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Player;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.StationAI;

public sealed class AIEyePowerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MindSwapPowerSystem _mindSwap = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    [Dependency] private readonly MobStateSystem _mobState = default!;

    //[Dependency] private readonly LawsSystem _laws = default!;
    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedEyeSystem _sharedEyeSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AIEyePowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AIEyePowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AIEyePowerComponent, AIEyePowerActionEvent>(OnPowerUsed);

        SubscribeLocalEvent<AIEyeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AIEyeComponent, MindRemovedMessage>(OnMindRemoved);

        SubscribeLocalEvent<StationAIComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<StationAIComponent, GetSiliconLawsEvent>(OnGetLaws);
    }

    [ValidatePrototypeId<SiliconLawsetPrototype>]
    private const string defaultAIRule = "Asimovpp";
    private void OnGetLaws(Entity<StationAIComponent> ent, ref GetSiliconLawsEvent args)
    {
        if (ent.Comp.SelectedLaw == null)
        {
            var selectedLaw = _prototypeManager.Index(ent.Comp.LawsId).Pick();
            if (_prototypeManager.TryIndex<SiliconLawsetPrototype>(selectedLaw, out var newLaw))
            {
                ent.Comp.SelectedLaw = newLaw;
            }
            else
            {
                ent.Comp.SelectedLaw = _prototypeManager.Index<SiliconLawsetPrototype>(defaultAIRule);
            }
        }

        foreach (var law in ent.Comp.SelectedLaw.Laws)
        {
            args.Laws.Laws.Add(_prototypeManager.Index<SiliconLawPrototype>(law));
        }

        args.Handled = true;
    }

    private void OnInit(EntityUid uid, AIEyePowerComponent component, ComponentInit args)
    {
        if (!_entityManager.HasComponent<StationAIComponent>(uid))
            return;

        _actions.AddAction(uid, ref component.EyePowerAction, component.PrototypeAction);
    }

    private void OnShutdown(EntityUid uid, AIEyePowerComponent component, ComponentShutdown args)
    {
        if (!_entityManager.HasComponent<StationAIComponent>(uid))
            return;

        if (component.EyePowerAction != null)
            _actions.RemoveAction(uid, component.EyePowerAction);
    }

    private void OnPowerUsed(EntityUid uid, AIEyePowerComponent component, AIEyePowerActionEvent args)
    {
        // var ai = _entityManager.EnsureComponent<StationAIComponent>(uid);
        if (!_entityManager.TryGetComponent<StationAIComponent>(uid, out var ai))
            return;

        // Mind swap
        var projection = Spawn(component.Prototype, Transform(uid).Coordinates);
        ai.ActiveEye = projection;
        var core = _entityManager.GetComponent<MetaDataComponent>(uid);

        Transform(projection).AttachToGridOrMap();
        _mindSwap.Swap(uid, projection);

        // Consistent name
        _metaDataSystem.SetEntityName(projection, core.EntityName != "" ? core.EntityName : "Invalid AI");

        EnsureComp<SiliconLawBoundComponent>(projection);

        args.Handled = true;
    }


    private void OnStartup(EntityUid uid, AIEyeComponent component, ComponentStartup args)
    {
        if (!_entityManager.HasComponent<StationAIComponent>(uid) ||
            !_entityManager.TryGetComponent<VisibilityComponent>(uid, out var visibility) ||
            !_entityManager.TryGetComponent<EyeComponent>(uid, out var eye))
            return;

        _sharedEyeSystem.SetVisibilityMask(uid,  eye.VisibilityMask | (int) VisibilityFlags.AIEye, eye);
        _visibilitySystem.AddLayer(uid, visibility, (int) VisibilityFlags.AIEye);
    }

    private void OnMindRemoved(EntityUid uid, AIEyeComponent component, MindRemovedMessage args)
    {
        QueueDel(uid);
    }


    private void OnMobStateChanged(EntityUid uid, StationAIComponent component, MobStateChangedEvent args)
    {
        if (!_mobState.IsDead(uid))
            return;

        if (component.ActiveEye != EntityUid.Invalid)
            _mindSwap.Swap(component.ActiveEye, uid);

        var sound = new SoundPathSpecifier("/Audio/SimpleStation14/Machines/AI/borg_death.ogg");

        _audio.PlayEntity(sound, Filter.Pvs(uid), uid, true);
    }
}
