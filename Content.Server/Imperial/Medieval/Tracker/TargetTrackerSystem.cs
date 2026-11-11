using System;
using System.Collections.Generic;
using Content.Shared.Imperial.Medieval.Tracker;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Tracker;

public sealed class TargetTrackerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, TrackData> _activeTracks = new();
    private readonly List<EntityUid> _toRemove = new();

    private TimeSpan _nextSyncTime = TimeSpan.Zero;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(0.2);

    private record struct TrackData(EntityUid Target, TimeSpan EndTime, TimeSpan Duration, Color Color);

    public void StartTrackingEntity(EntityUid follower, EntityUid target, TimeSpan duration, Color color)
    {
        if (Deleted(follower) || Deleted(target))
            return;

        _activeTracks[follower] = new TrackData(target, _timing.CurTime + duration, duration, color);

        var mapCoords = _transform.GetMapCoordinates(target);
        var netTarget = GetNetEntity(target);

        RaiseNetworkEvent(new TargetTrackEvent(mapCoords, color, duration, netTarget, resetTimer: true), follower);
    }

    public void StartTrackingPosition(EntityUid follower, MapCoordinates mapCoords, TimeSpan duration, Color color)
    {
        if (Deleted(follower))
            return;

        _activeTracks.Remove(follower);
        RaiseNetworkEvent(new TargetTrackEvent(mapCoords, color, duration, null, resetTimer: true), follower);
    }

    public void StopTracking(EntityUid follower)
    {
        _activeTracks.Remove(follower);
        RaiseNetworkEvent(new TargetTrackEvent(MapCoordinates.Nullspace, Color.White, TimeSpan.Zero, null, resetTimer: true), follower);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeTracks.Count == 0)
            return;

        var curTime = _timing.CurTime;
        if (curTime < _nextSyncTime)
            return;

        _nextSyncTime = curTime + SyncInterval;
        _toRemove.Clear();

        foreach (var (follower, track) in _activeTracks)
        {
            if (curTime >= track.EndTime || Deleted(follower) || Deleted(track.Target))
            {
                _toRemove.Add(follower);
                continue;
            }

            var followerXform = Transform(follower);
            var targetXform = Transform(track.Target);

            if (followerXform.MapID != targetXform.MapID || targetXform.MapID == MapId.Nullspace)
            {
                _toRemove.Add(follower);
                continue;
            }

            var mapCoords = _transform.GetMapCoordinates(track.Target, targetXform);
            var netTarget = GetNetEntity(track.Target);

            RaiseNetworkEvent(new TargetTrackEvent(mapCoords, track.Color, track.Duration, netTarget, resetTimer: false), follower);
        }

        foreach (var uid in _toRemove)
        {
            _activeTracks.Remove(uid);
        }
    }
}
