using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Dogs;

[Serializable, NetSerializable]
public enum WolfToDogOfferUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class WolfToDogOfferMessage : BoundUserInterfaceMessage
{
    public bool Accepted { get; }

    public WolfToDogOfferMessage(bool accepted)
    {
        Accepted = accepted;
    }
}
