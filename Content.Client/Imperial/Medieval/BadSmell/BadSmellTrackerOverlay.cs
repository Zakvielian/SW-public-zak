using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.Imperial.Medieval.BadSmell;

public sealed class BadSmellTrackerOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _playerMan;
    private readonly SharedTransformSystem _transform;
    private readonly BadSmellItemTrackerClientSystem _system;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BadSmellTrackerOverlay(
        BadSmellItemTrackerClientSystem system,
        IEntityManager entMan,
        IPlayerManager playerMan,
        IResourceCache resCache)
    {
        _system = system;
        _entMan = entMan;
        _playerMan = playerMan;
        _transform = _entMan.System<SharedTransformSystem>();
        _arrowTexture = resCache.GetResource<TextureResource>("/Textures/Imperial/Medieval/BadSmell/actions.rsi/arrowBROWN.png").Texture;
    }

    private readonly Texture _arrowTexture;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;

        if (!_system.TryGetTrackingData(out var targetCoords, out var scentColor, out var alpha))
            return;

        if (_playerMan.LocalEntity is not { } player || !_entMan.EntityExists(player))
            return;

        var playerXform = _entMan.GetComponent<TransformComponent>(player);
        var targetMapCoords = _transform.ToMapCoordinates(targetCoords);

        if (playerXform.MapID != targetMapCoords.MapId || playerXform.MapID == MapId.Nullspace)
            return;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var diff = targetMapCoords.Position - playerPos;
        var dist = diff.Length();

        // Target reached: hide arrow when in melee range (adjacent tile)
        const float minDistance = 1.2f;
        if (dist <= minDistance)
            return;

        var dir = diff / dist;

        // Keep arrow between player and target; never overshoot past target
        var arrowOffset = MathF.Min(1.25f, dist * 0.5f);
        var arrowCenter = playerPos + dir * arrowOffset;

        // Smooth proximity fade-out between 2.0m and 1.2m
        var proximityAlpha = Math.Clamp((dist - minDistance) / 0.8f, 0f, 1f);
        var finalAlpha = alpha * proximityAlpha;

        if (finalAlpha <= 0.01f)
            return;

        var angle = (float) diff.ToAngle();
        var matrix = Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(arrowCenter);
        worldHandle.SetTransform(matrix);

        var size = new Vector2(_arrowTexture.Width, _arrowTexture.Height) / EyeManager.PixelsPerMeter;
        var drawBox = Box2.CenteredAround(Vector2.Zero, size);

        worldHandle.DrawTextureRect(_arrowTexture, drawBox, Color.White.WithAlpha(finalAlpha));
        worldHandle.SetTransform(Matrix3x2.Identity);
    }
}

