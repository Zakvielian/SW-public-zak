using System;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LycantropyCompassComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public EntProtoId? BreakPrototype;

    [ViewVariables]
    public TimeSpan ExpireTime;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [AutoNetworkedField]
    public Vector2? TargetPosition;
}
