using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.BadSmell;

[Serializable, NetSerializable]
public sealed class BadSmellTrackEvent : EntityEventArgs
{
    public NetCoordinates TargetCoordinates;
    public Color ScentColor;

    public BadSmellTrackEvent(NetCoordinates targetCoordinates, Color scentColor)
    {
        TargetCoordinates = targetCoordinates;
        ScentColor = scentColor;
    }
}
