using Content.Shared.Imperial.Medieval.BadSmell;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.BadSmell;

public sealed class BadSmellItemTrackerClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private BadSmellTrackerOverlay _overlay = default!;

    private EntityCoordinates? _targetCoordinates;
    private Color _scentColor = Color.White;
    private TimeSpan _trackingEndTime;
    private readonly TimeSpan _trackingDuration = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new BadSmellTrackerOverlay(this, EntityManager, _playerMan, _resCache);
        _overlayMan.AddOverlay(_overlay);

        SubscribeNetworkEvent<BadSmellTrackEvent>(OnTrackScent);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnTrackScent(BadSmellTrackEvent ev)
    {
        _targetCoordinates = GetCoordinates(ev.TargetCoordinates);
        _scentColor = ev.ScentColor;
        _trackingEndTime = _timing.CurTime + _trackingDuration;
    }

    public bool TryGetTrackingData(out EntityCoordinates target, out Color color, out float alpha)
    {
        target = default;
        color = _scentColor;
        alpha = 0f;

        if (_targetCoordinates == null || _timing.CurTime >= _trackingEndTime)
            return false;

        target = _targetCoordinates.Value;

        var timeLeft = (_trackingEndTime - _timing.CurTime).TotalSeconds;
        alpha = Math.Clamp((float)(timeLeft / 1.5f), 0f, 1f);

        return true;
    }
}
