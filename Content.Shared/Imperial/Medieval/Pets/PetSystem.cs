using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.Dogs;

public sealed partial class PetSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TameTargetComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TameTargetComponent, PetCollarEvent>(OnPetCollarEvent);
        SubscribeLocalEvent<TameTargetComponent, PetOfferMessage>(OnOfferResponse);
    }

    private void OnInteractUsing(Entity<TameTargetComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<PetCollarComponent>(args.Used))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            TimeSpan.FromSeconds(2f),
            new PetCollarEvent(),
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

    private void OnPetCollarEvent(Entity<TameTargetComponent> ent, ref PetCollarEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Args.Used is not { } collarUid || !Exists(collarUid))
            return;

        if (_net.IsServer)
        {
            // if (!HasComp<ActorComponent>(ent.Owner))
            //     return;

            ent.Comp.PetOwner = args.User;
            _ui.TryOpenUi(ent.Owner, PetOfferUiKey.Key, ent.Owner);
        }

        args.Handled = true;
    }

    private void OnOfferResponse(Entity<TameTargetComponent> ent, ref PetOfferMessage args)
    {
        if (!_net.IsServer)
            return;

        if (ent.Comp.PetOwner is not { } petOwner)
            return;

        _ui.CloseUi(ent.Owner, PetOfferUiKey.Key, args.Actor);

        if (args.Accepted)
            OnAccepted(ent, petOwner);
        else
            OnDeny(ent, petOwner);
    }

    private void OnAccepted(Entity<TameTargetComponent> ent, EntityUid petOwner)
    {
        var selectedProto = _random.Pick(ent.Comp.PetsProtoId);

        var dog = SpawnAtPosition(selectedProto, Transform(ent).Coordinates);

        if (_mindSystem.TryGetMind(dog, out var oldMindId, out var oldMind))
            _mindSystem.TransferTo(oldMindId, null, mind: oldMind);

        if (_mindSystem.TryGetMind(ent, out var targetMindId, out var targetMind))
            _mindSystem.TransferTo(targetMindId, dog, mind: targetMind);

        QueueDel(ent);

        _popup.PopupClient(Loc.GetString("popup-pet-offer-accepted"), petOwner, PopupType.Medium);
    }

    private void OnDeny(Entity<TameTargetComponent> wolf, EntityUid petOwner)
    {
        _popup.PopupCursor(Loc.GetString("popup-pet-offer-deny"), petOwner, PopupType.Medium);
    }
}
