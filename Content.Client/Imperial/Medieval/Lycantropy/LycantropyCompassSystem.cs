using System;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Imperial.Medieval.Ships.Compass;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.Imperial.Medieval.Lycantropy;

public sealed class LycantropyCompassSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<LycantropyCompassComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            Angle angle;
            if (comp.TargetPosition is { } targetPos)
            {
                var compassPos = _transform.GetWorldPosition(uid);
                var delta = targetPos - compassPos;
                angle = delta.LengthSquared() > 0.001f
                    ? delta.ToAngle() + eyeRotation
                    : new Angle(Math.PI / 2.0) + eyeRotation;
            }
            else
            {
                angle = new Angle(Math.PI / 2.0) + eyeRotation;
            }

            var snappedIndex = (int) Math.Round(angle.Reduced().Theta / (Math.PI / 4));
            snappedIndex = ((snappedIndex % 8) + 8) % 8;
            var state = MedievalCompassComponent.DirectionStates[snappedIndex];

            _sprite.LayerSetRsiState((uid, sprite), MedievalCompassLayers.Base, state);
        }
    }
}
