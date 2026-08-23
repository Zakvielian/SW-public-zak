using System.Collections.Generic;
using Robust.Shared.Serialization;
using Content.Shared.Imperial.Medieval.Factions; // Пространство имен для WantedData

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

    public CalendarBoardBoundUserInterfaceState(Dictionary<int, WantedData> wanted)
    {
        Wanted = wanted;
    }
}
