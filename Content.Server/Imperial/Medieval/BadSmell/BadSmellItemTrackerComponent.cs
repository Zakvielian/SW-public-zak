using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.BadSmell;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadSmellItemTrackerComponent : Component
{
    [DataField]
    public EntProtoId ActionProto = "ActionBadSmellItemTracker";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}
