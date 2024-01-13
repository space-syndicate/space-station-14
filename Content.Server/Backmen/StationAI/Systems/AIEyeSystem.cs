using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Server.Speech.Components;
using Content.Shared.Actions;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Eye;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.StationAI;

public sealed class AIEyePowerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    [Dependency] private readonly MobStateSystem _mobState = default!;

    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedEyeSystem _sharedEyeSystem = default!;

    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AIEyePowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AIEyePowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AIEyePowerComponent, AIEyePowerActionEvent>(OnPowerUsed);

        SubscribeLocalEvent<AIEyeComponent, AIEyePowerReturnActionEvent>(OnPowerReturnUsed);

        SubscribeLocalEvent<AIEyeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AIEyeComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<AIEyeComponent, MindUnvisitedMessage>(OnMindRemoved2);

        SubscribeLocalEvent<StationAIComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<StationAIComponent, GetSiliconLawsEvent>(OnGetLaws);

        SubscribeLocalEvent<StationAIComponent, PowerChangedEvent>(OnPowerChange);


    }

    private void OnPowerChange(EntityUid uid, StationAIComponent component, ref PowerChangedEvent args)
    {
        if (HasComp<AIEyeComponent>(uid) || TerminatingOrDeleted(uid))
        {
            return;
        }

        foreach (var (actionId,action) in _actions.GetActions(uid))
        {
            _actions.SetEnabled(actionId, args.Powered);
        }

        if (!args.Powered && component.ActiveEye.IsValid())
        {
            QueueDel(component.ActiveEye);
            component.ActiveEye = EntityUid.Invalid;
        }

        if (!args.Powered)
        {
            EnsureComp<ReplacementAccentComponent>(uid).Accent = "dwarf";
            _uiSystem.TryCloseAll(uid);
        }
        else
        {
            RemCompDeferred<ReplacementAccentComponent>(uid);
        }
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
        if (!HasComp<StationAIComponent>(uid))
            return;

        if (component.EyePowerAction != null)
            _actions.RemoveAction(uid, component.EyePowerAction);
    }

    private void OnPowerReturnUsed(EntityUid uid, AIEyeComponent component, AIEyePowerReturnActionEvent args)
    {
        if (
            !TryComp<VisitingMindComponent>(args.Performer, out var mindId) ||
            mindId!.MindId == null ||
            !TryComp<MindComponent>(mindId.MindId.Value, out var mind)
        )
            return;

        _mindSystem.UnVisit(mindId.MindId.Value, mind);
        QueueDel(args.Performer);
        args.Handled = true;
    }

    private void OnPowerUsed(EntityUid uid, AIEyePowerComponent component, AIEyePowerActionEvent args)
    {
        if (!_mindSystem.TryGetMind(args.Performer, out var mindId, out var mind))
            return;

        if (!TryComp<StationAIComponent>(uid, out var ai))
            return;

        var coords = Transform(uid).Coordinates;
        var projection = EntityManager.CreateEntityUninitialized(component.Prototype, coords);
        ai.ActiveEye = projection;
        EnsureComp<AIEyeComponent>(projection).AiCore = (uid, ai);
        EnsureComp<StationAIComponent>(projection).SelectedLaw = ai.SelectedLaw;
        EnsureComp<SiliconLawBoundComponent>(projection);
        var core = MetaData(uid);
        // Consistent name
        _metaDataSystem.SetEntityName(projection, core.EntityName != "" ? core.EntityName : "Invalid AI");
        EntityManager.InitializeAndStartEntity(projection, coords.GetMapId(EntityManager));

        _transformSystem.AttachToGridOrMap(projection);
        _mindSystem.Visit(mindId, projection, mind); // Mind swap

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
        _actions.AddAction(uid, ref component.ReturnActionUid, component.ReturnAction);
    }

    private void OnMindRemoved(EntityUid uid, AIEyeComponent component, MindRemovedMessage args)
    {
        QueueDel(uid);
        if (component.AiCore?.Comp != null)
            component.AiCore.Value.Comp.ActiveEye = EntityUid.Invalid;
    }
    private void OnMindRemoved2(EntityUid uid, AIEyeComponent component, MindUnvisitedMessage args)
    {
        QueueDel(uid);
        if (component.AiCore?.Comp != null)
            component.AiCore.Value.Comp.ActiveEye = EntityUid.Invalid;
    }

    private void ClearState(EntityUid uid, AIEyeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return;
        }

        QueueDel(uid);
        if (!component.AiCore.HasValue)
            return;

        if (_mindSystem.TryGetMind(component.AiCore.Value, out var mindId, out var mind))
        {
            _mindSystem.UnVisit(mindId, mind);
        }

        component.AiCore.Value.Comp.ActiveEye = EntityUid.Invalid;
    }

    private static readonly SoundSpecifier AIDeath =
        new SoundPathSpecifier("/Audio/Backmen/Machines/AI/borg_death.ogg");

    private void OnMobStateChanged(EntityUid uid, StationAIComponent component, MobStateChangedEvent args)
    {
        if (!_mobState.IsDead(uid))
            return;

        if (component.ActiveEye.IsValid() && _mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            ClearState(component.ActiveEye);
        }

        _audioSystem.PlayPvs(AIDeath, uid);
    }
}
