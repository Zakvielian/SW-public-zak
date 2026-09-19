using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Dogs;

[Serializable, NetSerializable]
public enum PetOfferUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class PetOfferMessage : BoundUserInterfaceMessage
{
    public bool Accepted { get; }

    public PetOfferMessage(bool accepted)
    {
        Accepted = accepted;
    }
}
