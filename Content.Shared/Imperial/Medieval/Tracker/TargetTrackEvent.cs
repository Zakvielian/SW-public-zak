using System;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Tracker;

[Serializable, NetSerializable]
public sealed class TargetTrackEvent : EntityEventArgs
{
    public MapCoordinates TargetMapCoordinates;
    public NetEntity? TargetEntity;
    public Color ArrowColor;
    public TimeSpan Duration;
    public bool ResetTimer;

    public TargetTrackEvent(
        MapCoordinates targetMapCoordinates,
        Color arrowColor,
        TimeSpan duration,
        NetEntity? targetEntity = null,
        bool resetTimer = true)
    {
        TargetMapCoordinates = targetMapCoordinates;
        ArrowColor = arrowColor;
        Duration = duration;
        TargetEntity = targetEntity;
        ResetTimer = resetTimer;
    }
}
