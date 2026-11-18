using System;
using Content.Server.Polymorph.Components;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Lycantropy;

public sealed partial class LycantropySystem
{
    private static readonly ProtoId<AlertPrototype> AlertRevealSight = "MedievalLycantropyRevealSight";
    private TimeSpan _defaultEffectDuration = TimeSpan.FromSeconds(60);

    private void InitializeBloom()
    {
        SubscribeLocalEvent<MetaDataComponent, LycantropyGrantRevealSightEvent>(OnGrantRevealSight);
        SubscribeLocalEvent<LycantropyRevealSightComponent, ComponentStartup>(OnRevealSightStartup);
        SubscribeLocalEvent<LycantropyRevealSightComponent, ComponentShutdown>(OnRevealSightShutdown);

        SubscribeLocalEvent<LycantropyComponent, LycantropyReagentPointsEvent>(OnLycanReagentPoints);
        SubscribeLocalEvent<WerewolfComponent, LycantropyReagentPointsEvent>(OnWerewolfReagentPoints);
    }

    private void OnGrantRevealSight(EntityUid uid, MetaDataComponent comp, LycantropyGrantRevealSightEvent args)
    {
        if (HasComp<LycantropyComponent>(uid) ||
            HasComp<WerewolfComponent>(uid) ||
            HasComp<LycantropyInfectedComponent>(uid))
        {
            return;
        }

        if (TryComp<LycantropyRevealSightComponent>(uid, out var sight))
        {
            var baseTime = sight.EndTime > _timing.CurTime ? sight.EndTime : _timing.CurTime;
            sight.EndTime = baseTime + args.Duration;
        }
        else
        {
            sight = AddComp<LycantropyRevealSightComponent>(uid);
            sight.StartTime = _timing.CurTime;
            sight.EndTime = _timing.CurTime + args.Duration;
        }

        Dirty(uid, sight);
        _alerts.ShowAlert(uid, AlertRevealSight, cooldown: (sight.StartTime, sight.EndTime));
    }

    private void OnRevealSightStartup(EntityUid uid, LycantropyRevealSightComponent comp, ComponentStartup args)
    {
        if (comp.EndTime <= comp.StartTime)
        {
            comp.StartTime = _timing.CurTime;
            comp.EndTime = comp.StartTime + _defaultEffectDuration;
            Dirty(uid, comp);
        }

        _alerts.ShowAlert(uid, AlertRevealSight, cooldown: (comp.StartTime, comp.EndTime));
    }

    private void OnRevealSightShutdown(EntityUid uid, LycantropyRevealSightComponent comp, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, AlertRevealSight);
    }

    private void OnLycanReagentPoints(EntityUid uid, LycantropyComponent comp, LycantropyReagentPointsEvent args)
    {
        AddFlowerPoints(uid, uid, comp, args.Amount);
    }

    private void OnWerewolfReagentPoints(EntityUid uid, WerewolfComponent comp, LycantropyReagentPointsEvent args)
    {
        if (!TryComp<PolymorphedEntityComponent>(uid, out var polymorphed) ||
            polymorphed.Parent is not { } parent ||
            !TryComp<LycantropyComponent>(parent, out var parentLycan))
        {
            return;
        }

        AddFlowerPoints(uid, parent, parentLycan, args.Amount);
    }

    private void AddFlowerPoints(EntityUid popupTarget, EntityUid lycanUid, LycantropyComponent lycanComp, FixedPoint2 amount)
    {
        lycanComp.PartialPoints += amount;
        var gainedPoints = lycanComp.PartialPoints.Int();

        if (gainedPoints <= 0)
            return;

        lycanComp.PartialPoints -= gainedPoints;
        lycanComp.Points += gainedPoints;
        Dirty(lycanUid, lycanComp);

        _popup.PopupEntity(Loc.GetString("popup-lycantropy-flower-eaten", ("points", gainedPoints)), popupTarget, popupTarget);
    }

    private void UpdateBloom()
    {
        var query = EntityQueryEnumerator<LycantropyRevealSightComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.EndTime <= _timing.CurTime)
                RemComp<LycantropyRevealSightComponent>(uid);
        }
    }
}
