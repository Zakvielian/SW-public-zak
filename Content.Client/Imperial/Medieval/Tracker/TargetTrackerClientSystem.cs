using System;
using Content.Shared.Imperial.Medieval.Tracker;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Tracker;

public sealed class TargetTrackerClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private TargetTrackerOverlay _overlay = default!;

    private MapCoordinates _lastMapCoordinates = MapCoordinates.Nullspace;
    private NetEntity? _targetNetEntity;
    private Color _arrowColor = Color.White;
    private TimeSpan _trackingEndTime;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new TargetTrackerOverlay(this, EntityManager, _playerMan, _resCache);
        _overlayMan.AddOverlay(_overlay);

        SubscribeNetworkEvent<TargetTrackEvent>(OnTrackTarget);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnTrackTarget(TargetTrackEvent ev)
    {
        _lastMapCoordinates = ev.TargetMapCoordinates;
        _targetNetEntity = ev.TargetEntity;
        _arrowColor = ev.ArrowColor;

        if (ev.ResetTimer || _timing.CurTime >= _trackingEndTime)
        {
            _trackingEndTime = _timing.CurTime + ev.Duration;
        }
    }

    public bool TryGetTrackingData(out MapCoordinates target, out Color color, out float alpha)
    {
        target = MapCoordinates.Nullspace;
        color = _arrowColor;
        alpha = 0f;

        if (_timing.CurTime >= _trackingEndTime)
            return false;

        if (_targetNetEntity != null &&
            TryGetEntity(_targetNetEntity.Value, out var localUid) &&
            TryComp<TransformComponent>(localUid, out var targetXform) &&
            targetXform.MapID != MapId.Nullspace)
        {
            target = _transform.GetMapCoordinates(localUid.Value, targetXform);
        }
        else if (_lastMapCoordinates.MapId != MapId.Nullspace)
        {
            target = _lastMapCoordinates;
        }
        else
        {
            return false;
        }

        var timeLeft = (_trackingEndTime - _timing.CurTime).TotalSeconds;
        alpha = Math.Clamp((float)(timeLeft / 1.5f), 0f, 1f);

        return true;
    }
}
