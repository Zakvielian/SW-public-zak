using System;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Server.Polymorph.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Mobs;
using Content.Shared.Rejuvenate;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.Medieval.Lycantropy;

public sealed partial class LycantropySystem
{
    private void InitializeInfection()
    {
        SubscribeLocalEvent<LycantropyInfectedComponent, MobStateChangedEvent>(OnInfectedMobStateChanged);
        SubscribeLocalEvent<WerewolfComponent, ToggleLycantropyInfectActionEvent>(OnWerewolfInfect);
        SubscribeLocalEvent<WerewolfComponent, MeleeHitEvent>(OnWerewolfHit);
        SubscribeLocalEvent<WerewolfComponent, MeleeDamageDealtEvent>(OnWerewolfDamageDealt);
    }

    private void OnWerewolfInfect(EntityUid uid, WerewolfComponent comp, ToggleLycantropyInfectActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayGlobal(args.Sound, uid);
        comp.InfectOn = !comp.InfectOn;
        _actions.SetToggled(comp.InfectAction, comp.InfectOn);
    }

    private void OnWerewolfHit(EntityUid uid, WerewolfComponent comp, MeleeHitEvent args)
    {
        if (!comp.InfectOn)
            return;

        foreach (var item in args.HitEntities)
        {
            if (!HasComp<HumanoidAppearanceComponent>(item) || !_mobState.IsCritical(item))
                continue;

            _blood.TryModifyBleedAmount(item, -10);
            _jitter.DoJitter(item, TimeSpan.FromSeconds(3), true);

            RemComp<LycantropyRevealSightComponent>(item);
            EnsureComp<LycantropyComponent>(item);

            var infect = EnsureComp<LycantropyInfectedComponent>(item);
            infect.Werewolf = uid;
        }
    }

    private void OnWerewolfDamageDealt(EntityUid uid, WerewolfComponent comp, MeleeDamageDealtEvent args)
    {
        if (!_mobState.IsCritical(args.Target) || comp.Critted.ContainsKey(args.Target))
            return;

        comp.Critted.Add(args.Target, _timing.CurTime + TimeSpan.FromSeconds(60));
        if (TryComp<PolymorphedEntityComponent>(uid, out var polymorphed) &&
            TryComp<LycantropyComponent>(polymorphed.Parent, out var lycantropy))
        {
            lycantropy.Points += PointsPerCrit;
            Dirty(polymorphed.Parent.Value, lycantropy);
        }
    }

    private void OnInfectedMobStateChanged(EntityUid uid, LycantropyInfectedComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
        {
            var xform = Transform(uid);
            var nearbyEntities = _lookup.GetEntitiesInRange(xform.Coordinates, PointsRewardRadius);

            foreach (var entity in nearbyEntities)
            {
                if (TryComp<LycantropyComponent>(entity, out var lycantropy) && uid != entity)
                {
                    lycantropy.Points += PointsPerInfect;
                    Dirty(entity, lycantropy);
                    continue;
                }

                if (TryComp<PolymorphedEntityComponent>(entity, out var polymorphed) &&
                    polymorphed.Parent != null &&
                    TryComp<LycantropyComponent>(polymorphed.Parent.Value, out var parentLycantropy))
                {
                    parentLycantropy.Points += PointsPerInfect;
                    Dirty(polymorphed.Parent.Value, parentLycantropy);
                }
            }

            if (_mind.TryGetMind(uid, out var mindId, out _))
                _role.MindAddRole(mindId, "MindRoleWerewolf");

            RemComp(uid, comp);

            if (MedievalLycantropyRuleSystem.IsBloodMoon)
            {
                RaiseLocalEvent(uid, new RejuvenateEvent());
                Morph(uid, EnsureComp<LycantropyComponent>(uid));
            }
        }
        else if (args.NewMobState == MobState.Dead)
        {
            RemComp(uid, comp);
            RemComp<LycantropyComponent>(uid);
        }
    }

    private void UpdateInfected()
    {
        var infectedQuery = EntityQueryEnumerator<LycantropyInfectedComponent>();
        while (infectedQuery.MoveNext(out var uid, out _))
        {
            HealTotalDamage(uid, _regenAmount);
            _blood.TryModifyBleedAmount(uid, -1);
        }
    }
}
