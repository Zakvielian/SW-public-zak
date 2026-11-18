using System;
using Content.Shared.Chat;
using Content.Shared.Imperial.Dash;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Mobs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Lycantropy;

public sealed partial class LycantropySystem
{
    private void InitializeAbilities()
    {
        SubscribeLocalEvent<WerewolfComponent, MapInitEvent>(OnWerewolfMapInit);
        SubscribeLocalEvent<WerewolfComponent, WerewolfHowlActionEvent>(OnWerewolfHowl);
        SubscribeLocalEvent<WerewolfComponent, MeleeAttackEvent>(OnWerewolfAttack);
        SubscribeLocalEvent<WerewolfComponent, WerewolfHealAlliesActionEvent>(OnHealAllies);
        SubscribeLocalEvent<WerewolfComponent, WerewolfRegenActionEvent>(OnRegen);
        SubscribeLocalEvent<WerewolfComponent, WerewolfStatusEffectActionEvent>(OnStatusEffect);
        SubscribeLocalEvent<WerewolfComponent, WerewolfTearingActionEvent>(OnTearing);
        SubscribeLocalEvent<WerewolfComponent, WerewolfShadowDashActionEvent>(OnShadowDash);
        SubscribeLocalEvent<WerewolfComponent, WerewolfBloodFeelActionEvent>(OnBloodFeel);

        SubscribeLocalEvent<WerewolfComponent, SetWerewolfDamageEvent>(OnSetDamage);
        SubscribeLocalEvent<WerewolfComponent, SetWerewolfDashStrengthEvent>(OnSetDashStrength);
        SubscribeLocalEvent<WerewolfComponent, SetWerewolfMobThresholdsEvent>(OnSetThresholds);

        SubscribeNetworkEvent<SelectWerewolfFormEvent>(OnSelectForm);
        SubscribeNetworkEvent<BuyLycantropyAbilityEvent>(OnBuyAbility);
    }

    private void OnWerewolfMapInit(EntityUid uid, WerewolfComponent comp, MapInitEvent args)
    {
        RemComp<LycantropyRevealSightComponent>(uid);

        _actions.AddAction(uid, ref comp.InfectAction, "WerewolfInfectAction");
        _actions.AddAction(uid, "LycantropyHowlAction");
    }

    private void OnWerewolfHowl(EntityUid uid, WerewolfComponent comp, WerewolfHowlActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _status.TryAddStatusEffect<WerewolfRegenComponent>(uid, "MedievalWerewolfRegen", TimeSpan.FromSeconds(20), true);

        _chat.TryEmoteWithChat(uid, "Howl", ChatTransmitRange.Normal, true);
        var xform = Transform(uid);
        var filter = Filter.BroadcastMap(xform.MapID).RemoveInRange(xform.MapPosition, 16f, EntityManager);
        _audio.PlayGlobal(_audio.ResolveSound(new SoundPathSpecifier("/Audio/Imperial/Medieval/Werewolf/howl-far.ogg")), filter, false);

        if (xform.GridUid is not { } gridUid)
            return;

        var arrowColor = Color.Crimson;
        var trackDuration = TimeSpan.FromSeconds(5);

        var wolfQuery = EntityQueryEnumerator<WerewolfComponent, TransformComponent>();
        while (wolfQuery.MoveNext(out var targetUid, out _, out var targetXform))
        {
            if (targetUid == uid || targetXform.GridUid != gridUid)
                continue;

            _targetTracker.StartTrackingEntity(targetUid, uid, trackDuration, arrowColor);
        }

        var humanQuery = EntityQueryEnumerator<LycantropyComponent, TransformComponent>();
        while (humanQuery.MoveNext(out var targetUid, out _, out var targetXform))
        {
            if (targetUid == uid || targetXform.GridUid != gridUid)
                continue;

            _targetTracker.StartTrackingEntity(targetUid, uid, trackDuration, arrowColor);
        }
    }

    private void OnWerewolfAttack(EntityUid uid, WerewolfComponent comp, MeleeAttackEvent args)
    {
        if (_inner.TryGetInnerWeapon(uid, out _, out var id) && id == "tearing_weapon")
        {
            _inner.SetWeapon(uid, "");
            _actions.SetToggled(comp.Actions["WerewolfTearingAction"], false);
        }
    }

    private void OnHealAllies(EntityUid uid, WerewolfComponent comp, WerewolfHealAlliesActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(args.Sound, uid);

        foreach (var item in _lookup.GetEntitiesInRange<WerewolfComponent>(Transform(uid).Coordinates, 7f))
        {
            if (item.Owner == uid)
                continue;

            _status.TryAddStatusEffect<WerewolfRegenComponent>(item.Owner, "MedievalWerewolfRegen", TimeSpan.FromSeconds(20), true);
        }
    }

    private void OnRegen(EntityUid uid, WerewolfComponent comp, WerewolfRegenActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(args.Sound, uid);
        HealTotalDamage(uid, 50);
        _jitter.DoJitter(uid, TimeSpan.FromSeconds(3), true);
    }

    private void OnStatusEffect(EntityUid uid, WerewolfComponent comp, WerewolfStatusEffectActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _status.TryAddStatusEffect(uid, args.Key, TimeSpan.FromSeconds(args.Time), args.Refresh, args.Component);

        if (args.Global)
            _audio.PlayGlobal(args.Sound, uid);
        else
            _audio.PlayPvs(args.Sound, uid);
    }

    private void OnTearing(EntityUid uid, WerewolfComponent comp, WerewolfTearingActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_inner.TryGetInnerWeapon(uid, out _, out var id) && id == "tearing_weapon")
        {
            _inner.SetWeapon(uid, "");
            _actions.SetToggled(args.Action.Owner, false);
        }
        else if (_inner.TryGetInnerWeapon(uid, out _, out _))
        {
            return;
        }
        else
        {
            _audio.PlayGlobal(args.Sound, uid);
            _inner.SetWeapon(uid, "tearing_weapon");
            _actions.SetToggled(args.Action.Owner, true);
        }
    }

    private void OnShadowDash(EntityUid uid, WerewolfComponent comp, WerewolfShadowDashActionEvent args)
    {
        if (args.Handled || !HasComp<MedievalDashComponent>(uid))
            return;

        args.Handled = true;
        EnsureComp<WerewolfShadowDashComponent>(uid);
    }

    private void OnBloodFeel(EntityUid uid, WerewolfComponent comp, WerewolfBloodFeelActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _appearance.SetData(uid, WerewolfBloodFeelVisuals.Active, !HasComp<WerewolfBloodFeelComponent>(uid));

        if (HasComp<WerewolfBloodFeelComponent>(uid))
            RemComp<WerewolfBloodFeelComponent>(uid);
        else
            EnsureComp<WerewolfBloodFeelComponent>(uid);
    }

    private void OnSetDamage(EntityUid uid, WerewolfComponent comp, SetWerewolfDamageEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var weapon))
            return;

        foreach (var item in args.Replacements)
        {
            weapon.Damage.DamageDict[item.Key] = item.Value;
        }

        Dirty(uid, weapon);
    }

    private void OnSetDashStrength(EntityUid uid, WerewolfComponent comp, SetWerewolfDashStrengthEvent args)
    {
        if (!TryComp<MedievalDashComponent>(uid, out var dash))
            return;

        dash.Force *= args.Modifier;
        Dirty(uid, dash);
    }

    private void OnSetThresholds(EntityUid uid, WerewolfComponent comp, SetWerewolfMobThresholdsEvent args)
    {
        _mobThreshold.SetMobStateThreshold(uid, args.Death, MobState.Dead);
        _mobThreshold.SetMobStateThreshold(uid, args.Crit, MobState.Critical);
    }

    private void OnSelectForm(SelectWerewolfFormEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } uid || GetEntity(args.Ent) != uid)
            return;

        if (!TryComp<LycantropyComponent>(uid, out var comp)
            || comp.SelectedForm != null
            || !comp.AllowedPolymorphs.ContainsValue(args.Proto))
            return;

        comp.SelectedForm = args.Proto;

        if (comp.Actions.TryGetValue("Form", out var ent))
            _actions.RemoveAction(uid, ent);
    }

    private void OnBuyAbility(BuyLycantropyAbilityEvent args)
    {
        var uid = GetEntity(args.Ent);
        if (!TryComp<LycantropyComponent>(uid, out var comp))
            return;

        var proto = _proto.Index(args.Proto);

        if (comp.Points < proto.Cost)
            return;

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Werewolf/skill.ogg"), uid);

        comp.Points -= proto.Cost;
        comp.Abilities.Add(args.Proto);
        Dirty(uid, comp);

        foreach (var item in proto.HumanActions)
            _actions.AddAction(uid, item);
    }
}
