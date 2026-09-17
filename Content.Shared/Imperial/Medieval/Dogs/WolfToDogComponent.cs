using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Dogs;

[RegisterComponent, NetworkedComponent]
public sealed partial class WolfToDogComponent : Component
{
    [DataField] public EntityUid? PendingCollar;

    [DataField] public string DogProtoId = "MedievalMobDog";
}
