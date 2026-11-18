using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Imperial.DayTime;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System;
using System.Collections.Generic;

namespace Content.Shared.Imperial.Medieval.Calendar;

public sealed class CalendarSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public const int DayStageNumber = 1;
    public const int NightStageNumber = 6;

    public const string DayTag = "Day";
    public const string NightTag = "Night";

    public const int TargetDaysCount = 12;

    private int _curCycle;
    private bool _decksInitialized;

    private readonly List<ProtoId<CalendarEventPrototype>> _dayDeck = new();
    private readonly List<ProtoId<CalendarEventPrototype>> _nightDeck = new();

    /// <summary>
    /// Все теги, зарегистрированные в правилах игры (отделяет теги режимов от общих тегов вроде TradeEvent).
    /// </summary>
    private readonly HashSet<string> _knownModeTags = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Теги игровых режимов, активные в текущем раунде.
    /// </summary>
    private readonly HashSet<string> _activeModeTags = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ProtoId<CalendarEventPrototype>> DayDeck => _dayDeck;
    public IReadOnlyList<ProtoId<CalendarEventPrototype>> NightDeck => _nightDeck;
    public IReadOnlySet<string> ActiveModeTags => _activeModeTags;
    public int CurrentCycle => _curCycle;

    public override void Initialize()
    {
        base.Initialize();

        RebuildKnownModeTags();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<DayCycleStageChangedEvent>(OnDayCycleChanged);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
        SubscribeLocalEvent<CalendarEventStartedEvent>(OnCalendarEventStarted);
        SubscribeLocalEvent<DayCycleFinishedEvent>(OnNewDayCycle);
        SubscribeLocalEvent<CalendarRuleTagsComponent, GameRuleStartedEvent>(OnCalendarRuleStarted);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            RebuildKnownModeTags();
    }

    private void RebuildKnownModeTags()
    {
        _knownModeTags.Clear();

        foreach (var entProto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (!entProto.TryGetComponent<CalendarRuleTagsComponent>(out var ruleTags, EntityManager.ComponentFactory))
                continue;

            foreach (var tag in ruleTags.Tags)
            {
                _knownModeTags.Add(tag);
            }
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _curCycle = 0;
        _decksInitialized = false;
        _activeModeTags.Clear();
        _dayDeck.Clear();
        _nightDeck.Clear();
    }

    private void OnCalendarRuleStarted(EntityUid uid, CalendarRuleTagsComponent comp, ref GameRuleStartedEvent args)
    {
        if (comp.Tags.Count == 0)
            return;

        var addedAny = false;
        foreach (var tag in comp.Tags)
        {
            _knownModeTags.Add(tag);
            if (_activeModeTags.Add(tag))
                addedAny = true;
        }

        if (addedAny && _decksInitialized)
            RegenerateRemainingDecks();
    }

    public bool HasActiveModeTag(string tag)
    {
        return _activeModeTags.Contains(tag);
    }

    public void AddModeTag(string tag, bool regenerateDecks = true)
    {
        _knownModeTags.Add(tag);
        if (_activeModeTags.Add(tag) && regenerateDecks && _decksInitialized)
            RegenerateRemainingDecks();
    }

    public void RemoveModeTag(string tag, bool regenerateDecks = true)
    {
        if (_activeModeTags.Remove(tag) && regenerateDecks && _decksInitialized)
            RegenerateRemainingDecks();
    }

    private void OnCalendarEventStarted(CalendarEventStartedEvent args)
    {
        if (args.Prototype.Spawns == null || args.Prototype.Spawns.Count == 0)
            return;

        var markerToSpawns = new Dictionary<string, List<(EntProtoId Proto, int Amount)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (entProto, entries) in args.Prototype.Spawns)
        {
            foreach (var entry in entries)
            {
                if (entry.Amount <= 0)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.Marker) || entry.Marker.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    for (var i = 0; i < entry.Amount; i++)
                    {
                        Spawn(entProto, MapCoordinates.Nullspace);
                    }
                    continue;
                }

                if (!markerToSpawns.TryGetValue(entry.Marker, out var list))
                {
                    list = new List<(EntProtoId, int)>();
                    markerToSpawns[entry.Marker] = list;
                }

                list.Add((entProto, entry.Amount));
            }
        }

        if (markerToSpawns.Count == 0)
            return;

        var query = EntityQueryEnumerator<CalendarSpawnMarkerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var marker, out var xform))
        {
            if (!markerToSpawns.TryGetValue(marker.MarkerId, out var spawns))
                continue;

            foreach (var (proto, amount) in spawns)
            {
                for (var i = 0; i < amount; i++)
                {
                    Spawn(proto, xform.Coordinates);
                }
            }
        }
    }

    private void OnRoundStart(RoundStartedEvent args)
    {
        _curCycle = 0;
        InitializeCalendarDecks();

        if (_dayDeck.Count > 0)
            TriggerDayStageNotification(_dayDeck[0]);
        else
            TriggerDayStageNotification("DefaultCalendarEvent");
    }

    private void CollectActiveModeTags()
    {
        _activeModeTags.Clear();

        var ruleQuery = EntityQueryEnumerator<CalendarRuleTagsComponent, GameRuleComponent>();
        while (ruleQuery.MoveNext(out var uid, out var ruleTags, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            foreach (var tag in ruleTags.Tags)
            {
                _knownModeTags.Add(tag);
                _activeModeTags.Add(tag);
            }
        }

        var collectEv = new CollectCalendarModeTagsEvent(_activeModeTags);
        RaiseLocalEvent(collectEv);

        foreach (var tag in collectEv.Tags)
        {
            _knownModeTags.Add(tag);
        }
    }

    private void InitializeCalendarDecks()
    {
        CollectActiveModeTags();

        GenerateDeck(_dayDeck, 0, DayTag, "DefaultCalendarEvent");
        GenerateDeck(_nightDeck, 0, NightTag, "DefaultCalendarNightEvent");
        _decksInitialized = true;

        // Модифицировать колоду календаря можно после этого ивента
        RaiseLocalEvent(new CalendarDecksGeneratedEvent());
    }

    private void RegenerateRemainingDecks()
    {
        // Текущий день уже объявлен при старте цикла, поэтому перегенерируем со следующего дня,
        // а ночи — начиная с текущего цикла (если ночь ещё не наступила) или следующего.
        var dayStartIndex = Math.Clamp(_curCycle + 1, 0, TargetDaysCount);
        var nightStartIndex = Math.Clamp(_curCycle, 0, TargetDaysCount);

        GenerateDeck(_dayDeck, dayStartIndex, DayTag, "DefaultCalendarEvent");
        GenerateDeck(_nightDeck, nightStartIndex, NightTag, "DefaultCalendarNightEvent");

        RaiseLocalEvent(new CalendarDecksGeneratedEvent());
    }

    /// <summary>
    /// Позволяет внешним системам (например, игровым режимам) перезаписать конкретный день в колоде.
    /// </summary>
    /// <param name="dayNumber">Номер дня (начиная с 1)</param>
    /// <param name="eventId">ID прототипа события</param>
    /// <param name="isNight">Заменить событие в колоде ночей (true) или дней (false)</param>
    public void SetOverrideEvent(int dayNumber, ProtoId<CalendarEventPrototype> eventId, bool isNight = false)
    {
        var index = dayNumber - 1;

        if (isNight)
        {
            if (index >= 0 && index < _nightDeck.Count)
                _nightDeck[index] = eventId;
        }
        else
        {
            if (index >= 0 && index < _dayDeck.Count)
                _dayDeck[index] = eventId;
        }
    }

    private bool IsEventAllowedForCurrentModes(CalendarEventPrototype proto)
    {
        if (proto.ExcludedTags.Count > 0 && _activeModeTags.Overlaps(proto.ExcludedTags))
            return false;

        var hasModeTag = false;
        var matchesActiveMode = false;

        foreach (var tag in proto.Tags)
        {
            if (!_knownModeTags.Contains(tag))
                continue;

            hasModeTag = true;
            if (_activeModeTags.Contains(tag))
            {
                matchesActiveMode = true;
                break;
            }
        }

        // Общие события (без тегов режимов) доступны всегда; события с тегом режима — только при его активности.
        return !hasModeTag || matchesActiveMode;
    }

    private void GenerateDeck(List<ProtoId<CalendarEventPrototype>> deck, int startIndex, string filterTag, string fallbackEventId)
    {
        if (startIndex <= 0)
        {
            deck.Clear();
            startIndex = 0;
        }
        else if (deck.Count > startIndex)
        {
            deck.RemoveRange(startIndex, deck.Count - startIndex);
        }

        var pool = new List<CalendarEventPrototype>();
        var playerCount = _playerManager.PlayerCount;

        foreach (var proto in _prototype.EnumeratePrototypes<CalendarEventPrototype>())
        {
            if (proto.Tags.Contains(filterTag) &&
                proto.Weight > 0f &&
                playerCount >= proto.MinPlayers &&
                playerCount <= proto.MaxPlayers &&
                IsEventAllowedForCurrentModes(proto))
            {
                pool.Add(proto);
            }
        }

        if (pool.Count == 0)
        {
            for (var day = startIndex + 1; day <= TargetDaysCount; day++)
            {
                deck.Add(fallbackEventId);
            }
            return;
        }

        var counts = new Dictionary<string, int>(pool.Count);
        var lastOccurrence = new Dictionary<string, int>(pool.Count);

        // Учитываем уже зафиксированные дни (если идёт перегенерация середины колоды)
        for (var i = 0; i < deck.Count; i++)
        {
            var existingId = deck[i].Id;
            counts[existingId] = counts.GetValueOrDefault(existingId) + 1;
            lastOccurrence[existingId] = i + 1;
        }

        for (var day = startIndex + 1; day <= TargetDaysCount; day++)
        {
            var candidates = pool.FindAll(p =>
                p.MinDay <= day &&
                counts.GetValueOrDefault(p.ID) < p.MaxOccurrences &&
                (day - lastOccurrence.GetValueOrDefault(p.ID, -999)) >= p.MinOffset
            );

            if (candidates.Count == 0)
            {
                candidates = pool.FindAll(p =>
                    p.MaxOccurrences >= TargetDaysCount &&
                    (day - lastOccurrence.GetValueOrDefault(p.ID, -999)) >= p.MinOffset
                );
            }

            if (candidates.Count == 0)
            {
                deck.Add(fallbackEventId);
                continue;
            }

            var totalWeight = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].GetWeight(day);
            }

            var roll = _random.NextFloat() * totalWeight;
            var acc = 0f;
            var selected = candidates[0];

            for (var i = 0; i < candidates.Count; i++)
            {
                acc += candidates[i].GetWeight(day);
                if (roll <= acc)
                {
                    selected = candidates[i];
                    break;
                }
            }

            deck.Add(selected.ID);
            counts[selected.ID] = counts.GetValueOrDefault(selected.ID) + 1;
            lastOccurrence[selected.ID] = day;
        }
    }

    private void OnDayCycleChanged(ref DayCycleStageChangedEvent args)
    {
        TriggerNextDayStageNotification(args.NextStage);
    }

    private void OnNewDayCycle(ref DayCycleFinishedEvent args)
    {
        _curCycle++;
    }

    public void TriggerNextDayStageNotification(int stageNumber)
    {
        switch (stageNumber)
        {
            case DayStageNumber:
                {
                    if (_dayDeck.Count == 0)
                    {
                        TriggerDayStageNotification("DefaultCalendarEvent");
                        return;
                    }

                    var index = _curCycle % _dayDeck.Count;
                    TriggerDayStageNotification(_dayDeck[index]);
                    break;
                }

            case NightStageNumber:
                {
                    if (_nightDeck.Count == 0)
                    {
                        TriggerDayStageNotification("DefaultCalendarNightEvent");
                        return;
                    }

                    var index = _curCycle % _nightDeck.Count;
                    TriggerDayStageNotification(_nightDeck[index]);
                    break;
                }
        }
    }

    public void TriggerDayStageNotification(ProtoId<CalendarEventPrototype>? protoId = null)
    {
        var id = protoId ?? "DefaultCalendarEvent";

        if (!_prototype.TryIndex(id, out var proto))
            return;

        RaiseNetworkEvent(new CalendarBroadcastNotificationEvent(id), Filter.Broadcast());
        RaiseLocalEvent(new CalendarEventStartedEvent(_curCycle, id, proto));
    }
}

[AdminCommand(AdminFlags.VarEdit)]
public sealed class TriggerNextDayCycleCommand : IConsoleCommand
{
    public string Command => "triggernextdaycycle";
    public string Description => Loc.GetString("cmd-trigger-next-day-cycle-desc");
    public string Help => Loc.GetString("cmd-trigger-next-day-cycle-help", ("command", Command));

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-need-exactly-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var dayCount))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
            return;
        }

        var entSys = IoCManager.Resolve<IEntitySystemManager>();
        var daySystem = entSys.GetEntitySystem<CalendarSystem>();

        daySystem.TriggerNextDayStageNotification(dayCount);
        shell.WriteLine(Loc.GetString("cmd-trigger-next-day-cycle-success", ("days", dayCount)));
    }
}
