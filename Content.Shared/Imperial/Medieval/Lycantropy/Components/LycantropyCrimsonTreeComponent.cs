using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent]
public sealed partial class LycantropyCrimsonTreeComponent : Component
{
    [DataField]
    public TimeSpan LifeTime = TimeSpan.FromMinutes(5);

    [DataField]
    public float Radius = 15f;

    [DataField]
    public float PointsPerSecond = 0.125f;

    [DataField]
    public string AuraFixtureId = "aura";

    [DataField]
    public EntProtoId? WitheredPrototype = "MedievalLycantropyWitheredCrimsonTree";

    [ViewVariables]
    public TimeSpan WitherTime;

    [ViewVariables]
    public readonly HashSet<EntityUid> TrackedEntities = new();
}

[RegisterComponent]
public sealed partial class LycantropyCrimsonTreeAuraComponent : Component
{
    [ViewVariables]
    public readonly HashSet<EntityUid> Sources = new();

    [ViewVariables]
    public float AccumulatedPoints;
}