using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Alert;
using Content.Shared._CorvaxNext.Alert.Click;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared._CorvaxNext.Mood;
using Content.Shared._CorvaxNext.Overlays;
using Content.Shared.Popups;
using Content.Server._CorvaxNext.Traits.Assorted;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Content.Shared._CorvaxNext.NextVars;
using Content.Shared.Examine;
using Content.Shared.Humanoid;

namespace Content.Server._CorvaxNext.Mood;

public sealed class MoodSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    [ValidatePrototypeId<AlertCategoryPrototype>]
    private const string MoodCategory = "Mood";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<MoodComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MoodComponent, MoodEffectEvent>(OnMoodEffect);
        SubscribeLocalEvent<MoodComponent, DamageChangedEvent>(OnDamageChange);
        SubscribeLocalEvent<MoodComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<MoodComponent, MoodRemoveEffectEvent>(OnRemoveEffect);

        SubscribeLocalEvent<MoodModifyTraitComponent, ComponentStartup>(OnTraitStartup);

        SubscribeLocalEvent<MoodComponent, MoodCheckAlertEvent>(OnAlertClicked);
        SubscribeLocalEvent<MoodComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, MoodComponent component, ExaminedEvent args)
    {
        var mood = GetMoodName(component.CurrentMoodThreshold);
        var color = GetMoodColor(component.CurrentMoodThreshold);
        if (mood == string.Empty)
            return;

        args.PushText(Loc.GetString("mood-component-examine",
            ("color", color),
            ("mood", mood),
            ("user", uid)));
    }

    private void OnAlertClicked(EntityUid uid, MoodComponent component, MoodCheckAlertEvent args)
    {
        if (component.CurrentMoodThreshold == MoodThreshold.Dead ||
            !TryComp<ActorComponent>(uid, out var actor))
            return;

        var session = actor.PlayerSession;
        var msgStart = Loc.GetString("mood-show-effects-start");
        _chat.ChatMessageToOne(ChatChannel.Emotes,
            msgStart,
            msgStart,
            EntityUid.Invalid,
            false,
            session.Channel);

        foreach (var (_, protoId) in component.CategorisedEffects)
        {
            if (!_prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var proto)
                || proto.Hidden)
                continue;

            SendDescToChat(proto, session);
        }

        foreach (var (protoId, _) in component.UncategorisedEffects)
        {
            if (!_prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var proto)
                || proto.Hidden)
                continue;

            SendDescToChat(proto, session);
        }
    }

    private void SendDescToChat(MoodEffectPrototype proto, ICommonSession session)
    {
        var color = (proto.MoodChange > 0) ? "#008000" : "#BA0000";
        var msg = $"[font size=10][color={color}]{proto.Description}[/color][/font]";

        _chat.ChatMessageToOne(ChatChannel.Emotes,
            msg,
            msg,
            EntityUid.Invalid,
            false,
            session.Channel);
    }

    private void OnRemoveEffect(EntityUid uid, MoodComponent component, MoodRemoveEffectEvent args)
    {
        if (component.UncategorisedEffects.TryGetValue(args.EffectId, out _))
            RemoveTimedOutEffect(uid, args.EffectId);
        else
        {
            foreach (var (category, id) in component.CategorisedEffects)
            {
                if (id == args.EffectId)
                {
                    RemoveTimedOutEffect(uid, args.EffectId, category);
                    return;
                }
            }
        }
    }

    private void OnRefreshMoveSpeed(EntityUid uid, MoodComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.CurrentMoodThreshold is > MoodThreshold.Meh and < MoodThreshold.Good or MoodThreshold.Dead
            || _jetpack.IsUserFlying(uid))
            return;

        // This ridiculous math serves a purpose making high mood less impactful on movement speed than low mood
        var modifier =
            Math.Clamp(
                (component.CurrentMoodLevel >= component.MoodThresholds[MoodThreshold.Neutral])
                    ? _config.GetCVar(NextVars.MoodIncreasesSpeed)
                        ? MathF.Pow(1.003f, component.CurrentMoodLevel - component.MoodThresholds[MoodThreshold.Neutral])
                        : 1
                    : _config.GetCVar(NextVars.MoodDecreasesSpeed)
                        ? 2 - component.MoodThresholds[MoodThreshold.Neutral] / component.CurrentMoodLevel
                        : 1,
                component.MinimumSpeedModifier,
                component.MaximumSpeedModifier);

        args.ModifySpeed(1, modifier);
    }

    private void OnTraitStartup(EntityUid uid, MoodModifyTraitComponent component, ComponentStartup args)
    {
        if (!TryComp<MoodComponent>(uid, out var mood))
            return;

        mood.GoodMoodMultiplier = component.GoodMoodMultiplier;
        mood.BadMoodMultiplier = component.BadMoodMultiplier;
        RaiseLocalEvent(uid, new MoodEffectEvent($"{component.MoodId}"));
    }

    private void OnMoodEffect(EntityUid uid, MoodComponent component, MoodEffectEvent args)
    {
        if (!_config.GetCVar(NextVars.MoodEnabled)
            || !_prototypeManager.TryIndex<MoodEffectPrototype>(args.EffectId, out var prototype))
            return;

        var ev = new OnMoodEffect(uid, args.EffectId, args.EffectModifier, args.EffectOffset);
        RaiseLocalEvent(uid, ref ev);

        ApplyEffect(uid, component, prototype, ev.EffectModifier, ev.EffectOffset);
    }

    private void ApplyEffect(EntityUid uid, MoodComponent component, MoodEffectPrototype prototype, float eventModifier = 1, float eventOffset = 0)
    {
        // Apply categorised effect
        if (prototype.Category != null)
        {
            if (component.CategorisedEffects.TryGetValue(prototype.Category, out var oldPrototypeId))
            {
                if (!_prototypeManager.TryIndex<MoodEffectPrototype>(oldPrototypeId, out var oldPrototype))
                    return;

                if (prototype.ID != oldPrototype.ID)
                {
                    SendEffectText(uid, prototype);
                    component.CategorisedEffects[prototype.Category] = prototype.ID;
                }
            }
            else
            {
                component.CategorisedEffects.Add(prototype.Category, prototype.ID);
            }

            if (prototype.Timeout != 0)
                Timer.Spawn(TimeSpan.FromSeconds(prototype.Timeout), () => RemoveTimedOutEffect(uid, prototype.ID, prototype.Category));
        }
        // Apply uncategorised effect
        else
        {
            if (component.UncategorisedEffects.TryGetValue(prototype.ID, out _))
                return;

            var moodChange = prototype.MoodChange * eventModifier + eventOffset;
            if (moodChange == 0)
                return;

            SendEffectText(uid, prototype);
            component.UncategorisedEffects.Add(prototype.ID, moodChange);

            if (prototype.Timeout != 0)
                Timer.Spawn(TimeSpan.FromSeconds(prototype.Timeout), () => RemoveTimedOutEffect(uid, prototype.ID));
        }

        RefreshMood(uid, component);
    }

    private void SendEffectText(EntityUid uid, MoodEffectPrototype prototype)
    {
        if (!prototype.Hidden)
            _popup.PopupEntity(prototype.Description, uid, uid, (prototype.MoodChange > 0) ? PopupType.Medium : PopupType.MediumCaution);
    }

    private void RemoveTimedOutEffect(EntityUid uid, string prototypeId, string? category = null)
    {
        if (!TryComp<MoodComponent>(uid, out var comp))
            return;

        if (category == null)
        {
            if (!comp.UncategorisedEffects.ContainsKey(prototypeId))
                return;
            comp.UncategorisedEffects.Remove(prototypeId);
        }
        else
        {
            if (!comp.CategorisedEffects.TryGetValue(category, out var currentProtoId)
                || currentProtoId != prototypeId
                || !_prototypeManager.HasIndex<MoodEffectPrototype>(currentProtoId))
                return;
            comp.CategorisedEffects.Remove(category);
        }

        RefreshMood(uid, comp);
    }

    private void OnMobStateChanged(EntityUid uid, MoodComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && args.OldMobState != MobState.Dead)
        {
            var ev = new MoodEffectEvent("Dead");
            RaiseLocalEvent(uid, ev);
        }
        else if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
        {
            var ev = new MoodRemoveEffectEvent("Dead");
            RaiseLocalEvent(uid, ev);
        }
        RefreshMood(uid, component);

        if (args.Origin == null ||
            args.NewMobState != MobState.Alive ||
            !HasComp<HumanoidAppearanceComponent>(uid) ||
            !HasComp<MoodComponent>(args.Origin))
            return;

        // Finally players won't miss any crit bodies, because of the sweet mood bonus!
        switch (args.NewMobState)
        {
            case MobState.Alive:
                RaiseLocalEvent(uid, new MoodEffectEvent("GotSavedLife"));
                RaiseLocalEvent(args.Origin.Value, new MoodEffectEvent("SavedLife"));
                break;
            default:
                RaiseLocalEvent(uid, new MoodRemoveEffectEvent("GotSavedLife"));
                break;
        }
    }

    // <summary>
    //      Recalculate the mood level of an entity by summing up all moodlets.
    // </summary>
    private void RefreshMood(EntityUid uid, MoodComponent component)
    {
        var amount = 0f;

        foreach (var (_, protoId) in component.CategorisedEffects)
        {
            if (!_prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var prototype))
                continue;

            if (prototype.MoodChange > 0)
                amount += prototype.MoodChange * component.GoodMoodMultiplier;
            else
                amount += prototype.MoodChange * component.BadMoodMultiplier;
        }

        foreach (var (_, value) in component.UncategorisedEffects)
        {
            if (value > 0)
                amount += value * component.GoodMoodMultiplier;
            else
                amount += value * component.BadMoodMultiplier;
        }

        SetMood(uid, amount, component, refresh: true);
    }

    private void OnInit(EntityUid uid, MoodComponent component, ComponentStartup args)
    {
        if (_config.GetCVar(NextVars.MoodModifiesThresholds)
            && TryComp<MobThresholdsComponent>(uid, out var mobThresholdsComponent)
            && _mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold, mobThresholdsComponent))
            component.CritThresholdBeforeModify = critThreshold.Value;

        RefreshMood(uid, component);
    }

    private void SetMood(EntityUid uid, float amount, MoodComponent? component = null, bool force = false, bool refresh = false)
    {
        if (!_config.GetCVar(NextVars.MoodEnabled)
            || !Resolve(uid, ref component)
            || component.CurrentMoodThreshold == MoodThreshold.Dead && !refresh)
            return;

        var neutral = component.MoodThresholds[MoodThreshold.Neutral];
        var ev = new OnSetMoodEvent(uid, amount, false);
        RaiseLocalEvent(uid, ref ev);

        if (ev.Cancelled)
            return;
        else
        {
            uid = ev.Receiver;
            amount = ev.MoodChangedAmount;
        }

        var newMoodLevel = amount + neutral;
        if (!force)
        {
            newMoodLevel = Math.Clamp(amount + neutral,
                component.MoodThresholds[MoodThreshold.Dead],
                component.MoodThresholds[MoodThreshold.Perfect]);
        }

        component.CurrentMoodLevel = newMoodLevel;

        component.NeutralMoodThreshold = component.MoodThresholds.GetValueOrDefault(MoodThreshold.Neutral);
        Dirty(uid, component);
        UpdateCurrentThreshold(uid, component);
    }

    private void UpdateCurrentThreshold(EntityUid uid, MoodComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var calculatedThreshold = GetMoodThreshold(component);
        if (calculatedThreshold == component.CurrentMoodThreshold)
            return;

        component.CurrentMoodThreshold = calculatedThreshold;

        DoMoodThresholdsEffects(uid, component);
    }

    private void DoMoodThresholdsEffects(EntityUid uid, MoodComponent? component = null, bool force = false)
    {
        if (!Resolve(uid, ref component)
            || component.CurrentMoodThreshold == component.LastThreshold && !force)
            return;

        var modifier = GetMovementThreshold(component.CurrentMoodThreshold, component);

        // Modify mob stats
        if (modifier != GetMovementThreshold(component.LastThreshold, component))
        {
            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            SetCritThreshold(uid, component, modifier);
            RefreshShaders(uid, modifier);
        }

        // Modify interface
        if (component.MoodThresholdsAlerts.TryGetValue(component.CurrentMoodThreshold, out var alertId))
            _alerts.ShowAlert(uid, alertId);
        else
            _alerts.ClearAlertCategory(uid, MoodCategory);

        component.LastThreshold = component.CurrentMoodThreshold;
    }

    private void RefreshShaders(EntityUid uid, int modifier)
    {
        if (modifier == -1)
            EnsureComp<SaturationScaleOverlayComponent>(uid);
        else
            RemComp<SaturationScaleOverlayComponent>(uid);
    }

    private void SetCritThreshold(EntityUid uid, MoodComponent component, int modifier)
    {
        if (!_config.GetCVar(NextVars.MoodModifiesThresholds)
            || !TryComp<MobThresholdsComponent>(uid, out var mobThresholds)
            || !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var key))
            return;

        var newKey = modifier switch
        {
            1 => FixedPoint2.New(key.Value.Float() * component.IncreaseCritThreshold),
            -1 => FixedPoint2.New(key.Value.Float() * component.DecreaseCritThreshold),
            _ => component.CritThresholdBeforeModify
        };

        component.CritThresholdBeforeModify = key.Value;
        _mobThreshold.SetMobStateThreshold(uid, newKey, MobState.Critical, mobThresholds);
    }

    private MoodThreshold GetMoodThreshold(MoodComponent component, float? moodLevel = null)
    {
        moodLevel ??= component.CurrentMoodLevel;
        var result = MoodThreshold.Dead;
        var value = component.MoodThresholds[MoodThreshold.Perfect];

        foreach (var threshold in component.MoodThresholds)
        {
            if (threshold.Value <= value && threshold.Value >= moodLevel)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }

        return result;
    }

    private int GetMovementThreshold(MoodThreshold threshold, MoodComponent component)
    {
        if (threshold >= component.BuffsMoodThreshold)
            return 1;

        if (threshold <= component.ConsMoodThreshold)
            return -1;

        return 0;
    }

    private string GetMoodName(MoodThreshold threshold)
    {
        return threshold switch
        {
            MoodThreshold.Insane or MoodThreshold.Horrible or MoodThreshold.Terrible => Loc.GetString("mood-examine-horrible"),
            MoodThreshold.Bad or MoodThreshold.Meh => Loc.GetString("mood-examine-bad"),
            MoodThreshold.Neutral => Loc.GetString("mood-examine-neutral"),
            MoodThreshold.Good or MoodThreshold.Great => Loc.GetString("mood-examine-good"),
            MoodThreshold.Exceptional or MoodThreshold.Perfect => Loc.GetString("mood-examine-perfect"),
            _ => Loc.GetString(""),
        };
    }

    private static Color GetMoodColor(MoodThreshold threshold)
    {
        return threshold switch
        {
            MoodThreshold.Insane or MoodThreshold.Horrible or MoodThreshold.Terrible => Color.Red,
            MoodThreshold.Bad or MoodThreshold.Meh => Color.Orange,
            MoodThreshold.Neutral => Color.Blue,
            MoodThreshold.Good or MoodThreshold.Great => Color.Green,
            MoodThreshold.Exceptional or MoodThreshold.Perfect => Color.Aquamarine,
            _ => Color.Gray,
        };
    }

    private void OnDamageChange(EntityUid uid, MoodComponent component, DamageChangedEvent args)
    {
        if (!_mobThreshold.TryGetPercentageForState(uid, MobState.Critical, args.Damageable.TotalDamage, out var damage))
            return;

        var protoId = "HealthNoDamage";
        var value = component.HealthMoodEffectsThresholds["HealthNoDamage"];

        foreach (var threshold in component.HealthMoodEffectsThresholds)
        {
            if (threshold.Value <= damage && threshold.Value >= value)
            {
                protoId = threshold.Key;
                value = threshold.Value;
            }
        }

        var ev = new MoodEffectEvent(protoId);
        RaiseLocalEvent(uid, ev);
    }
}
