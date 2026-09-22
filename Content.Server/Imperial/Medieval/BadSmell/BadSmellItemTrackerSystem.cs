using Content.Server.Actions;
using Content.Server.BadSmell.Components;
using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.BadSmell;
using Content.Shared.Imperial.Medieval.Perfume;
using Content.Shared.Popups;
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

        if (smellItem.Toucher is not { } targetEnt || !Exists(targetEnt) || TryComp<PerfumeTargetComponent>(targetEnt, out _))
        {
            _popup.PopupEntity(Loc.GetString("bad-smell-track-target-lost"), performer, performer, PopupType.SmallCaution);
            return;
        }

        var performerXform = Transform(performer);
        var targetXform = Transform(targetEnt);

        if (performerXform.GridUid == null || performerXform.GridUid != targetXform.GridUid)
        {
            _popup.PopupEntity(Loc.GetString("bad-smell-track-different-grid"), performer, performer, PopupType.SmallCaution);
            return;
        }

        var performerPos = _transform.GetWorldPosition(performerXform);
        var targetPos = _transform.GetWorldPosition(targetXform);
        var diff = targetPos - performerPos;
        var distance = MathF.Round(diff.Length());

        var direction = diff.GetDir();
        var dirKey = $"bad-smell-dir-{direction.ToString().ToLowerInvariant()}";

        _popup.PopupEntity(Loc.GetString("bad-smell-track-success",
            ("dir", Loc.GetString(dirKey)),
            ("dist", distance)),
            performer, performer, PopupType.Medium);

        args.Handled = true;
    }
}
