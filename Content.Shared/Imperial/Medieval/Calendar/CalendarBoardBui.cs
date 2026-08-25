using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;
using Content.Shared.Imperial.Medieval.Factions;

namespace Content.Shared.Imperial.Medieval.Calendar;

[Serializable, NetSerializable]
public enum CalendarBoardUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CalendarBoardBoundUserInterfaceState : BoundUserInterfaceState
{
    public Dictionary<int, WantedData> Wanted;
    public List<string> CalendarDeck;
    public int CurrentCycle;
    public List<AnnouncementData> Announcements;

    public CalendarBoardBoundUserInterfaceState(
        Dictionary<int, WantedData> wanted,
        List<string> calendarDeck,
        int currentCycle,
        List<AnnouncementData> announcements)
    {
        Wanted = wanted;
        CalendarDeck = calendarDeck;
        CurrentCycle = currentCycle;
        Announcements = announcements;
    }
}
