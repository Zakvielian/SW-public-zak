using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Dogs;

[RegisterComponent, NetworkedComponent]
public sealed partial class PetCollarComponent : Component
{
    [DataField] public float CollarUseDuration = 5f;
}
