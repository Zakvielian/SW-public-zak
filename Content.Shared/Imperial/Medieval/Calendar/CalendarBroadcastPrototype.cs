using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Calendar;

[Prototype("calendarEvent")]
public sealed partial class CalendarEventPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("texture")]
    public string Texture { get; set; } = string.Empty;

    [DataField("text")]
    public string Text { get; set; } = string.Empty;

    [DataField("sound")]
    public SoundSpecifier? Sound { get; set; }

    [DataField("tags")]
    public HashSet<string> Tags { get; private set; } = new();
}
