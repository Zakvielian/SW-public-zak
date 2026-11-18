using System.Collections.Generic;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Calendar;

public sealed class CalendarEventStartedEvent : EntityEventArgs
{
    public int DayNumber { get; }
    public ProtoId<CalendarEventPrototype> EventId { get; }
    public CalendarEventPrototype Prototype { get; }

    public CalendarEventStartedEvent(int dayNumber, ProtoId<CalendarEventPrototype> eventId, CalendarEventPrototype prototype)
    {
        DayNumber = dayNumber;
        EventId = eventId;
        Prototype = prototype;
    }
}

/// <summary>
/// Вызывается перед генерацией колод календаря. Позволяет системам режимов добавить активные теги пула.
/// </summary>
public sealed class CollectCalendarModeTagsEvent : EntityEventArgs
{
    public HashSet<string> Tags { get; }

    public CollectCalendarModeTagsEvent(HashSet<string> tags)
    {
        Tags = tags;
    }
}

/// <summary>
/// Вызывается сразу после того, как CalendarSystem сгенерировал базовые колоды дней и ночей на раунд.
/// Идеальное место для игровых режимов, чтобы перезаписать нужные дни.
/// </summary>
public sealed class CalendarDecksGeneratedEvent : EntityEventArgs
{
}
