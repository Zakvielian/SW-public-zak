using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Medieval.Dogs;

public partial class WolfToDogSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WolfToDogComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WolfToDogComponent, DogCollarEvent>(OnDogCollarEvent);
        SubscribeLocalEvent<WolfToDogComponent, WolfToDogOfferMessage>(OnOfferResponse);
    }

    private void OnInteractUsing(Entity<WolfToDogComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<DogCollarComponent>(args.Used))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            TimeSpan.FromSeconds(2f),
            new DogCollarEvent(),
            ent.Owner,
            target: ent.Owner,
            used: args.Used)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            BreakOnDamage = true,
            RequireCanInteract = true,
            BreakOnDropItem = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnDogCollarEvent(Entity<WolfToDogComponent> ent, ref DogCollarEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Args.Used is not { } collarUid || !Exists(collarUid))
            return;

        if (_net.IsServer)
        {
            if (!HasComp<ActorComponent>(ent.Owner))
                return;

            ent.Comp.PendingCollar = collarUid;
            _ui.TryOpenUi(ent.Owner, WolfToDogOfferUiKey.Key, ent.Owner);
        }

        args.Handled = true;
    }

    private void OnOfferResponse(Entity<WolfToDogComponent> ent, ref WolfToDogOfferMessage args)
    {
        if (!_net.IsServer)
            return;

        _ui.CloseUi(ent.Owner, WolfToDogOfferUiKey.Key, args.Actor);

        var collar = ent.Comp.PendingCollar;
        ent.Comp.PendingCollar = null;

        if (args.Accepted && collar != null && Exists(collar.Value))
            OnAccepted(ent, collar.Value);
        else
            OnRejected(ent, collar);
    }

    protected virtual void OnAccepted(Entity<WolfToDogComponent> ent, EntityUid collar)
    {
        var dog = SpawnAtPosition(ent.Comp.DogProtoId, Transform(ent).Coordinates);

        if (_mindSystem.TryGetMind(dog, out var oldMindId, out var oldMind))
            _mindSystem.TransferTo(oldMindId, null, mind: oldMind);

        if (_mindSystem.TryGetMind(ent, out var targetMindId, out var targetMind))
            _mindSystem.TransferTo(targetMindId, dog, mind: targetMind);

        QueueDel(ent);
    }

    protected virtual void OnRejected(Entity<WolfToDogComponent> wolf, EntityUid? collar)
    {
    }
}
