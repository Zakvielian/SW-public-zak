namespace Content.Server.BadSmell.Components;

[RegisterComponent]
public sealed partial class BadSmellItemComponent : Component
{
    [DataField] public EntityUid? Toucher;

    [DataField] public TimeSpan SmellTime;

    [DataField] public SmellProfile? SmellProfile;
}

