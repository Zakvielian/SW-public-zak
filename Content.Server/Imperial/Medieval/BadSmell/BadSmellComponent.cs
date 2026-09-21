using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Imperial.Medieval.Perfume;

namespace Content.Server.BadSmell.Components
{
    [RegisterComponent]
    public sealed partial class BadSmellComponent : Component
    {
        [DataField]
        public int WorstSmell = 0;

        [DataField]
        public int BestSmell = 0;

        [DataField]
        public float SmellLevel = 0f;

        [DataField]
        public float MaxSmellLevel = 100f;

        [DataField]
        public float GrowTemp = 0.31f;

        [DataField]
        public float WashTemp = 42f;

        [DataField("startTime")]
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        [DataField("endTime")]
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

        [DataField("reloadTime")]
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(30f);

        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public string EffectSound = "/Audio/Imperial/Medieval/bad_smell_effect.ogg";
        public ProtoId<AlertPrototype> SmellAlert = "BadSmell";

        [DataField]
        public bool IsDirtyVisible = true;

        [DataField]
        public float BadSmellItemMod = 1f;

        [DataField]
        public SmellProfile Profile;

    }
}

[DataDefinition, Serializable]
public partial record struct SmellProfile()
{
    [DataField("strength")]
    public SmellStrength Strength { get; set; } = SmellStrength.Moderate;

    [DataField("base")]
    public SmellBase Base { get; set; } = SmellBase.Sweet;

    [DataField("modifier")]
    public SmellModifier Modifier { get; set; } = SmellModifier.Tart;

    public SmellProfile(SmellStrength strength, SmellBase smellBase, SmellModifier modifier) : this()
    {
        Strength = strength;
        Base = smellBase;
        Modifier = modifier;
    }

    public readonly string ToFormattedString()
    {
        var strengthKey = $"smell-strength-{Strength.ToString().ToLowerInvariant()}";
        var baseKey = $"smell-base-{Base.ToString().ToLowerInvariant()}";
        var modKey = $"smell-modifier-{Modifier.ToString().ToLowerInvariant()}";

        return Loc.GetString("smell-profile-format",
            ("strength", Loc.GetString(strengthKey)),
            ("base", Loc.GetString(baseKey)),
            ("modifier", Loc.GetString(modKey)));
    }

    public readonly Color GetColor()
    {
        var baseColor = Base switch
        {
            SmellBase.Sweet    => Color.FromHex("#FF69B4"),
            SmellBase.Sour     => Color.FromHex("#7FFF00"),
            SmellBase.Salty    => Color.FromHex("#00FFFF"),
            SmellBase.Bitter   => Color.FromHex("#8A2BE2"),
            SmellBase.Rotten   => Color.FromHex("#556B2F"),
            SmellBase.Metallic => Color.FromHex("#B0C4DE"),
            _                  => Color.White
        };

        var modTint = Modifier switch
        {
            SmellModifier.Tart     => Color.FromHex("#FFD700"),
            SmellModifier.Musty    => Color.FromHex("#8B4513"),
            SmellModifier.Pungent  => Color.FromHex("#FF4500"),
            SmellModifier.Floral   => Color.FromHex("#E6E6FA"),
            SmellModifier.Spicy    => Color.FromHex("#DC143C"),
            SmellModifier.Chemical => Color.FromHex("#39FF14"),
            _                      => Color.White
        };

        var blendedR = (baseColor.R * 0.75f) + (modTint.R * 0.25f);
        var blendedG = (baseColor.G * 0.75f) + (modTint.G * 0.25f);
        var blendedB = (baseColor.B * 0.75f) + (modTint.B * 0.25f);

        var alpha = Strength switch
        {
            SmellStrength.Faint        => 0.60f,
            SmellStrength.Moderate     => 0.75f,
            SmellStrength.Strong       => 0.90f,
            SmellStrength.Overwhelming => 1.00f,
            _                          => 1.0f
        };

        return new Color(blendedR, blendedG, blendedB, alpha);
    }
}

public enum SmellStrength : byte
{
    Faint,
    Moderate,
    Strong,
    Overwhelming
}

public enum SmellBase : byte
{
    Sweet,
    Sour,
    Salty,
    Bitter,
    Rotten,
    Metallic
}

public enum SmellModifier : byte
{
    Tart,
    Musty,
    Pungent,
    Floral,
    Spicy,
    Chemical
}

