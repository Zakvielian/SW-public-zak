namespace Content.Server.BadSmell.Components;

[RegisterComponent]
public sealed partial class BadSmellItemFeelComponent : Component
{
    [DataField]
    public float Sensivity = 25;
}

