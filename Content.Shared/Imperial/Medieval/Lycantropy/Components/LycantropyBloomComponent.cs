using System;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LycantropyRevealSightComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan StartTime;

    [AutoNetworkedField]
    public TimeSpan EndTime;
}

[RegisterComponent]
public sealed partial class LycantropyFlowerSpawnerComponent : Component
{
}

public sealed class LycantropyGrantRevealSightEvent : EntityEventArgs
{
    public readonly TimeSpan Duration;

    public LycantropyGrantRevealSightEvent(TimeSpan duration)
    {
        Duration = duration;
    }
}

public sealed class LycantropyReagentPointsEvent : EntityEventArgs
{
    public readonly FixedPoint2 Amount;

    public LycantropyReagentPointsEvent(FixedPoint2 amount)
    {
        Amount = amount;
    }
}

public sealed partial class GrantLycantropyRevealSight : EntityEffect
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(60);

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-lycantropy-reveal-sight");

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs || reagentArgs.Scale <= FixedPoint2.Zero)
            return;

        var addedDuration = Duration * (double)reagentArgs.Scale;
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, new LycantropyGrantRevealSightEvent(addedDuration));
    }
}

public sealed partial class GrantLycantropyPoints : EntityEffect
{
    [DataField]
    public FixedPoint2 PointsPerUnit = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-lycantropy-points", ("points", PointsPerUnit));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs || reagentArgs.Scale <= FixedPoint2.Zero)
            return;

        var totalPoints = PointsPerUnit * reagentArgs.Scale;
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, new LycantropyReagentPointsEvent(totalPoints));
    }
}
