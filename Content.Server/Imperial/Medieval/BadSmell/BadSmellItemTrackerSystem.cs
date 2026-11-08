using System.Numerics;
using Content.Server.Actions;
using Content.Server.BadSmell.Components;
using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.BadSmell;
using Content.Shared.Imperial.Medieval.Perfume;
using Content.Shared.Popups;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.BadSmell;

public sealed class BadSmellItemTrackerSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BadSmellItemTrackerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BadSmellItemTrackerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BadSmellItemTrackerComponent, BadSmellItemTrackerActionEvent>(OnTrackAction);
    }

    private void OnMapInit(Entity<BadSmellItemTrackerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.ActionProto);
    }

    private void OnShutdown(Entity<BadSmellItemTrackerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionEntity is not { } action)
            return;

        _actionContainer.RemoveAction(action);
        QueueDel(action);
        ent.Comp.ActionEntity = null;
    }

    private void OnTrackAction(Entity<BadSmellItemTrackerComponent> ent, ref BadSmellItemTrackerActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        var performer = args.Performer;

        if (!TryComp<BadSmellItemComponent>(target, out var smellItem))
        {
            _popup.PopupEntity(Loc.GetString("bad-smell-track-no-scent"), performer, performer, PopupType.SmallCaution);
            return;
        }

        if (smellItem.SmellTime < _timing.CurTime)
        {
            RemComp<BadSmellItemComponent>(target);
            _popup.PopupEntity(Loc.GetString("bad-smell-track-scent-expired"), performer, performer, PopupType.SmallCaution);
            return;
        }

        var targetProfile = smellItem.SmellProfile;
        if (targetProfile == null && smellItem.Toucher is { } toucher && TryComp<BadSmellComponent>(toucher, out var toucherSmell))
        {
            targetProfile = toucherSmell.Profile;
        }

        if (targetProfile == null)
        {
            _popup.PopupEntity(Loc.GetString("bad-smell-track-target-lost"), performer, performer, PopupType.SmallCaution);
            return;
        }

        var performerXform = Transform(performer);
        if (performerXform.GridUid is not { } gridUid)
        {
            _popup.PopupEntity(Loc.GetString("bad-smell-track-no-grid"), performer, performer, PopupType.SmallCaution);
            return;
        }

        var performerPos = _transform.GetWorldPosition(performerXform);
        EntityUid? closestTarget = null;
        var closestDistSq = float.MaxValue;
        var closestDiff = Vector2.Zero;
        var sourceProfile = targetProfile.Value;

        var query = EntityQueryEnumerator<BadSmellComponent, TransformComponent>();
        while (query.MoveNext(out var candidateUid, out var candidateSmell, out var candidateXform))
        {
            if (candidateUid == performer)
                continue;

            if (candidateXform.GridUid != gridUid)
                continue;

            if (!IsMatchingSmell(sourceProfile, candidateSmell.Profile))
                continue;

            if (HasComp<PerfumeTargetComponent>(candidateUid))
                continue;

            var candidatePos = _transform.GetWorldPosition(candidateXform);
            var diff = candidatePos - performerPos;
            var distSq = diff.LengthSquared();

            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestTarget = candidateUid;
                closestDiff = diff;
            }
        }

        if (closestTarget == null)
        {
            _popup.PopupEntity(Loc.GetString("bad-smell-track-no-target-found"), performer, performer, PopupType.SmallCaution);
            return;
        }

        // Send network event with NetCoordinates and scent color to the client
        var targetCoords = GetNetCoordinates(Transform(closestTarget.Value).Coordinates);
        var scentColor = sourceProfile.GetColor();
        RaiseNetworkEvent(new BadSmellTrackEvent(targetCoords, scentColor), performer);

        var distance = (int) MathF.Round(MathF.Sqrt(closestDistSq));
        _popup.PopupEntity(Loc.GetString("bad-smell-track-success-scent", ("dist", distance)),
            performer, performer, PopupType.Medium);

        args.Handled = true;

        args.Handled = true;
    }

    /// <summary>
    /// Checks if candidate smell matches source scent profile.
    /// </summary>
    private static bool IsMatchingSmell(SmellProfile source, SmellProfile candidate)
    {
        return source.Base == candidate.Base && source.Modifier == candidate.Modifier;
    }
}
