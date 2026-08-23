using System.Collections.Generic;
using Content.Server.Imperial.DayTime;
using Content.Server.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Calendar;
using Content.Shared.Imperial.Medieval.Factions;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Imperial.Medieval.Calendar.Board;

public sealed class CalendarBoardSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly CalendarSystem _calendar = default!;
    [Dependency] private readonly MedievalFactionsSystem _factions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CalendarBoardComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<DayCycleFinishedEvent>(OnDayCycleFinished);
    }

    private void OnUIOpened(EntityUid uid, CalendarBoardComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is not CalendarBoardUiKey.Key)
            return;

        UpdateUIState(uid);
    }

    private void OnDayCycleFinished(ref DayCycleFinishedEvent args)
    {
        UpdateAllBoards();
    }

    public void UpdateAllBoards()
    {
        var query = EntityQueryEnumerator<CalendarBoardComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            UpdateUIState(uid);
        }
    }

    public void UpdateUIState(EntityUid uid)
    {
        var deck = _calendar.CalendarDeck;
        var currentCycle = _calendar.CurrentCycle;

        var stringDeck = new List<string>(deck.Count);
        foreach (var protoId in deck)
        {
            stringDeck.Add((string) protoId);
        }

        var wantedData = _factions.WantedList;

        var state = new CalendarBoardBoundUserInterfaceState(wantedData, stringDeck, currentCycle);
        _ui.SetUiState(uid, CalendarBoardUiKey.Key, state);

        _appearance.SetData(uid, WantedDeskVisuals.Appearance, wantedData.Count switch
        {
            <= 0 => WantedDeskVisualState.None,
            < 3 => WantedDeskVisualState.Min,
            < 6 => WantedDeskVisualState.Medium,
            > 6 => WantedDeskVisualState.Full,
            _ => WantedDeskVisualState.None
        });
    }
}
