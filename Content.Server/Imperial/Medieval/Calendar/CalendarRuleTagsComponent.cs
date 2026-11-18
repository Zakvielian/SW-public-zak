using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using System.Collections.Generic;

namespace Content.Shared.Imperial.Medieval.Calendar;

/// <summary>
/// Указывает теги событий календаря, которые активируются при запуске данного игрового правила.
/// </summary>
[RegisterComponent]
public sealed partial class CalendarRuleTagsComponent : Component
{
    [DataField("tags")]
    public HashSet<string> Tags { get; private set; } = new();
}
