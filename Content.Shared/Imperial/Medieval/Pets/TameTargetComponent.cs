using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Dogs;

[RegisterComponent, NetworkedComponent]
public sealed partial class TameTargetComponent : Component
{
    [DataField]
    public EntityUid? PetOwnerUid;

    [DataField]
    public EntityUid? CollarUid;

    [DataField]
    public List<string> PetsProtoId = new();
}
