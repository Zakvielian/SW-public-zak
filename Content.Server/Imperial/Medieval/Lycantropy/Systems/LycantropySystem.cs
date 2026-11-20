using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Actions;
using Content.Server.Administration;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Server.Imperial.Medieval.Tracker;
using Content.Server.Jittering;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.Stunnable;
using Content.Shared.Administration;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Imperial.Medieval.Weapons;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Lycantropy;

public sealed partial class LycantropySystem : SharedLycantropySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InnerWeaponSystem _inner = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly TargetTrackerSystem _targetTracker = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MedievalLycantropyRuleSystem _rule = default!;

    private const int PointsPerNight = 0;
    private const int PointsPerInfect = 4;
    private const int PointsPerCrit = 1;
    private const float PointsRewardRadius = 15f;

    private readonly FixedPoint2 _regenAmount = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LycantropyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LycantropyComponent, PolymorphWerewolfActionEvent>(OnWerewolfPolymorph);

        InitializeAbilities();
        InitializeInfection();
        InitializeBloom();

        _console.RegisterCommand("setlycantropynight",
            Loc.GetString("cmd-weather-desc"),
            Loc.GetString("cmd-weather-help"),
            SetNightCommand,
            CommandCompletion);
    }

    private void OnMapInit(EntityUid uid, LycantropyComponent comp, MapInitEvent args)
    {
        RemComp<LycantropyRevealSightComponent>(uid);

        EntityUid? formEntity = null;
        if (_actions.AddAction(uid, ref formEntity, "SelectWerewolfFormAction"))
            comp.Actions.Add("Form", formEntity.Value);

        EntityUid? shopEntity = null;
        if (_actions.AddAction(uid, ref shopEntity, "LycantropyProgressMenuAction"))
            comp.Actions.Add("Menu", shopEntity.Value);
    }

    private void OnWerewolfPolymorph(EntityUid uid, LycantropyComponent comp, PolymorphWerewolfActionEvent args)
    {
        Morph(uid, comp);
    }

    public void OnNightStarted()
    {
        var ents = EntityManager.AllEntities<LycantropyComponent>();
        _random.Shuffle(ents);

        var count = GetWerewolfTransformCount(ents.Count());
        for (var i = 0; i < ents.Count(); i++)
        {
            var entity = ents[i];

            if (i >= count && entity.Comp.NightsSpent < 2)
            {
                entity.Comp.NightsSpent++;
                continue;
            }

            Morph(entity.Owner, entity.Comp);
        }
    }

    public void OnNightEnded()
    {
        var ents = EntityManager.AllEntities<WerewolfComponent>();
        foreach (var item in ents)
        {
            if (HasComp<LycantropyCrimsonTreeAuraComponent>(item.Owner))
                continue;

            item.Comp.RevertTime = _timing.CurTime + TimeSpan.FromSeconds(30);
            item.Comp.NextRevertPopup = _timing.CurTime;
            _jitter.DoJitter(item.Owner, TimeSpan.FromSeconds(30), true);
        }
    }

    public void Morph(EntityUid uid, LycantropyComponent comp)
    {
        var abilities = comp.Abilities;
        var requiresTreeAura = !_rule.IsNight && HasComp<LycantropyCrimsonTreeAuraComponent>(uid);

        _audio.PlayGlobal(new SoundCollectionSpecifier("WerewolfTransform"), uid);
        _stun.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(3));
        _jitter.DoJitter(uid, TimeSpan.FromSeconds(3), true);

        Timer.Spawn(TimeSpan.FromSeconds(3), () =>
        {
            if (TerminatingOrDeleted(uid) || !_mobState.IsAlive(uid))
                return;

            if (requiresTreeAura && !_rule.IsNight && !HasComp<LycantropyCrimsonTreeAuraComponent>(uid))
                return;

            var mob = _polymorph.PolymorphEntity(uid, comp.SelectedForm ?? comp.AllowedPolymorphs.First().Value);

            if (!mob.HasValue)
                return;

            foreach (var item in abilities)
            {
                var proto = _proto.Index(item);

                if (proto.Event != null)
                    RaiseLocalEvent(mob.Value, proto.Event);

                foreach (var action in proto.WerewolfActions)
                {
                    EntityUid? ent = null;
                    _actions.AddAction(mob.Value, ref ent, action);
                    if (!ent.HasValue || !TryComp<WerewolfComponent>(mob, out var werewolf))
                        continue;

                    werewolf.Actions.Add(action, ent.Value);
                }
            }
        });
    }

    private float GetWerewolfTransformCount(int count)
    {
        if (MedievalLycantropyRuleSystem.IsBloodMoon)
            return count;

        if (count <= 2 && count > 0)
            return Math.Clamp(count - 1, 1, 2);

        return count / 2;
    }

    public void HealTotalDamage(EntityUid uid, FixedPoint2 healAmount, DamageableComponent? damageable = null)
    {
        if (healAmount <= FixedPoint2.Zero || !Resolve(uid, ref damageable))
            return;

        var currentDamage = damageable.Damage;
        var totalCurrentDamage = currentDamage.GetTotal();

        if (totalCurrentDamage <= FixedPoint2.Zero)
            return;

        var healSpec = new DamageSpecifier();

        if (healAmount >= totalCurrentDamage)
        {
            foreach (var (type, amount) in currentDamage.DamageDict)
            {
                if (amount > FixedPoint2.Zero)
                    healSpec.DamageDict.Add(type, -amount);
            }
        }
        else
        {
            var remainingHeal = healAmount;
            var remainingDamage = totalCurrentDamage;

            foreach (var (type, amount) in currentDamage.DamageDict)
            {
                if (amount <= FixedPoint2.Zero)
                    continue;

                var healForType = FixedPoint2.Min(amount, remainingHeal * amount / remainingDamage);

                if (healForType > FixedPoint2.Zero)
                {
                    healSpec.DamageDict.Add(type, -healForType);
                    remainingHeal -= healForType;
                    remainingDamage -= amount;
                }
            }
        }

        if (healSpec.DamageDict.Count > 0)
            _damage.TryChangeDamage(uid, healSpec);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateRegen();
        UpdateWerewolves();
        UpdateInfected();
        UpdateBloom();
    }

    private void UpdateRegen()
    {
        var query = EntityQueryEnumerator<WerewolfRegenComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUpdate > _timing.CurTime)
                continue;

            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
            HealTotalDamage(uid, _regenAmount);
        }
    }

    private void UpdateWerewolves()
    {
        var wolfQuery = EntityQueryEnumerator<WerewolfComponent>();
        while (wolfQuery.MoveNext(out var uid, out var comp))
        {
            foreach (var item in new Dictionary<EntityUid, TimeSpan>(comp.Critted))
            {
                if (item.Value <= _timing.CurTime)
                    comp.Critted.Remove(item.Key);
            }

            if (HasComp<LycantropyCrimsonTreeAuraComponent>(uid))
            {
                comp.RevertTime = null;
                comp.NextRevertPopup = null;
                continue;
            }

            if (comp.RevertTime.HasValue && comp.RevertTime <= _timing.CurTime)
            {
                comp.RevertTime = null;
                var ent = _polymorph.Revert(uid);
                if (TryComp<LycantropyComponent>(ent, out var lycantropy) && _mobState.IsAlive(ent.Value))
                {
                    lycantropy.Points += PointsPerNight;
                    Dirty(ent.Value, lycantropy);
                }

                continue;
            }

            if (comp.NextRevertPopup.HasValue && comp.NextRevertPopup <= _timing.CurTime)
            {
                comp.NextRevertPopup = _timing.CurTime + TimeSpan.FromSeconds(10);
                _popup.PopupEntity(Loc.GetString("popup-werewolf-revert-soon"), uid, uid);
            }
        }
    }

    [AdminCommand(AdminFlags.Fun)]
    private void SetNightCommand(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("cmd-weather-error-no-arguments"));
            return;
        }

        if (!bool.TryParse(args[0], out var night))
            return;

        if (night)
            OnNightStarted();
        else
            OnNightEnded();
    }

    private CompletionResult CommandCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.FromHintOptions(new List<string> { "true", "false" }, Loc.GetString("night state"));
    }
}
