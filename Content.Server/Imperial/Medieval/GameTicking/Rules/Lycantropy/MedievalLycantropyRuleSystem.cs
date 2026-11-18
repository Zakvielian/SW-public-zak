using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.DayTime;
using Content.Server.Imperial.Medieval.Lycantropy;
using Content.Server.Polymorph.Systems;
using Content.Shared.Alert;
using Content.Shared.DayTime;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.DayTime;
using Content.Shared.Imperial.Medieval.Calendar;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class MedievalLycantropyRuleSystem : GameRuleSystem<MedievalLycantropyRuleComponent>
{
    [Dependency] private readonly LycantropySystem _lycantropy = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly ProtoId<AlertPrototype> AlertMoon = "MedievalWerewolfTransformation";
    private static readonly ProtoId<CalendarEventPrototype> BloomEventId = "CalendarEventLycantropyBloom";
    private static readonly EntProtoId BloomFlowerProto = "MedievalLycantropyBloomFlower";
    private const string DungeonFlowerMarkerId = "LycantropyFlowerDungeon";

    public const int NightStartStage = 5;
    public const int NightEndStage = 10;
    public const string DefaultDayTimeGroup = "0";
    public const string BloodMoonPresetId = "bloody";

    private const short AlertSeverityDay = 0;
    private const short AlertSeverityNight = 1;

    public static bool IsBloodMoon = false;

    private int _curCycle = 0;
    private int _currentStage = 0;
    private TimeSpan? _endTime = null;

    private TimeSpan _phaseStartTime = TimeSpan.Zero;
    private TimeSpan _phaseEndTime = TimeSpan.Zero;

    private bool IsNight => IsBloodMoon || (_currentStage >= NightStartStage && _currentStage < NightEndStage);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DayCycleFinishedEvent>(OnDayCycleFinished);
        SubscribeLocalEvent<DayCycleStageChangedEvent>(OnDayStageChanged);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnd);

        SubscribeLocalEvent<LycantropyComponent, ComponentStartup>(OnHumanStartup);
        SubscribeLocalEvent<LycantropyComponent, ComponentShutdown>(OnHumanShutdown);
        SubscribeLocalEvent<LycantropyComponent, WerewolfTransformAlertEvent>(OnHumanAlertClicked);

        SubscribeLocalEvent<WerewolfComponent, ComponentStartup>(OnWolfStartup);
        SubscribeLocalEvent<WerewolfComponent, ComponentShutdown>(OnWolfShutdown);
        SubscribeLocalEvent<WerewolfComponent, WerewolfTransformAlertEvent>(OnWolfAlertClicked);

        SubscribeLocalEvent<CalendarEventStartedEvent>(OnCalendarEventStarted);
    }

    private void OnCalendarEventStarted(CalendarEventStartedEvent args)
    {
        if (args.EventId != BloomEventId)
            return;

        var spawners = new List<EntityCoordinates>();

        var spawnerQuery = EntityQueryEnumerator<LycantropyFlowerSpawnerComponent, TransformComponent>();
        while (spawnerQuery.MoveNext(out _, out _, out var xform))
        {
            spawners.Add(xform.Coordinates);
        }

        var markerQuery = EntityQueryEnumerator<CalendarSpawnMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out _, out var marker, out var xform))
        {
            if (string.Equals(marker.MarkerId, DungeonFlowerMarkerId, StringComparison.OrdinalIgnoreCase))
                spawners.Add(xform.Coordinates);
        }

        if (spawners.Count == 0)
            return;

        var chosenCoords = _random.Pick(spawners);
        Spawn(BloomFlowerProto, chosenCoords);
    }

    private void OnRoundStart(RoundStartedEvent args)
    {
        _curCycle = 0;
        _currentStage = 0;
        _endTime = null;
        _phaseStartTime = TimeSpan.Zero;
        _phaseEndTime = TimeSpan.Zero;

        SyncInternalTimer(isStageChangeEvent: false);
    }

    private void OnRoundEnd(RoundEndedEvent args)
    {
        IsBloodMoon = false;
        _endTime = null;
        ClearAllTransformationAlerts();
    }

    #region Human Handlers (LycantropyComponent)

    private void OnHumanStartup(EntityUid uid, LycantropyComponent component, ComponentStartup args)
    {
        RemComp<LycantropyRevealSightComponent>(uid);

        if (_phaseEndTime <= _timing.CurTime)
            SyncInternalTimer(isStageChangeEvent: false);

        UpdateHumanAlert(uid);
    }

    private void OnHumanShutdown(EntityUid uid, LycantropyComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, AlertMoon);
    }

    private void OnHumanAlertClicked(EntityUid uid, LycantropyComponent component, WerewolfTransformAlertEvent args)
    {
        if (args.Handled || !IsNight || !_mobState.IsAlive(uid))
            return;

        args.Handled = true;
        _lycantropy.Morph(uid, component);
    }

    #endregion

    #region Werewolf Handlers (WerewolfComponent)

    private void OnWolfStartup(EntityUid uid, WerewolfComponent component, ComponentStartup args)
    {
        RemComp<LycantropyRevealSightComponent>(uid);

        if (_phaseEndTime <= _timing.CurTime)
            SyncInternalTimer(isStageChangeEvent: false);

        UpdateWolfAlert(uid);
    }

    private void OnWolfShutdown(EntityUid uid, WerewolfComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, AlertMoon);
    }

    private void OnWolfAlertClicked(EntityUid uid, WerewolfComponent component, WerewolfTransformAlertEvent args)
    {
        if (args.Handled || IsBloodMoon || IsNight || !_mobState.IsAlive(uid))
            return;

        args.Handled = true;

        _polymorph.Revert(uid);
        UpdateAllAlerts();
    }

    #endregion

    private void OnDayCycleFinished(ref DayCycleFinishedEvent args)
    {
        _curCycle++;

        var livingWerewolves = 0;
        var query = EntityQueryEnumerator<LycantropyComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_mobState.IsAlive(uid))
                livingWerewolves++;
        }

        if (livingWerewolves < _config.GetCVar(MedievalCCVars.BloodMoonWerewolves))
            return;

        if (_curCycle >= _config.GetCVar(MedievalCCVars.BloodMoonPeriod))
        {
            SetDayTimePreset(DefaultDayTimeGroup, BloodMoonPresetId);

            var duration = TimeSpan.FromMinutes(10);
            _phaseStartTime = _timing.CurTime;
            _endTime = _phaseStartTime + duration;
            _phaseEndTime = _endTime.Value;

            IsBloodMoon = true;
            _lycantropy.OnNightStarted();
            UpdateAllAlerts();
        }
    }

    private void OnDayStageChanged(ref DayCycleStageChangedEvent args)
    {
        if (IsBloodMoon)
            return;

        _currentStage = args.NextStage;

        if (_currentStage == NightEndStage)
        {
            _lycantropy.OnNightEnded();
        }

        SyncInternalTimer(isStageChangeEvent: true);
        UpdateAllAlerts();
    }

    private void SetDayTimePreset(string groupId, string presetId)
    {
        if (!_proto.TryIndex<DayTimePrototype>(presetId, out var proto))
            return;

        if (proto.ColorStages.Count != proto.TimeStages.Count || proto.TimeStages.Count == 0)
            return;

        var query = EntityQueryEnumerator<DayTimeComponent>();
        while (query.MoveNext(out _, out var dayTime))
        {
            if (dayTime.GroupID != groupId)
                continue;

            dayTime.PresetID = presetId;
            dayTime.StageColorTimeData.Clear();

            for (var i = 0; i < proto.TimeStages.Count; i++)
            {
                dayTime.StageColorTimeData.Add((Color.FromHex(proto.ColorStages[i]), TimeSpan.FromSeconds(proto.TimeStages[i])));
            }

            dayTime.TotalLocalTime = dayTime.StageColorTimeData.Sum(x => (float)x.time.TotalSeconds);
            dayTime.CurrentStage = 0;
            dayTime.StageUpdateTime = _timing.CurTime + dayTime.StageColorTimeData[0].time;
            dayTime.ColorTo = dayTime.StageColorTimeData[0].color;
        }
    }

    private void SyncInternalTimer(bool isStageChangeEvent)
    {
        DayTimeComponent? targetDayTime = null;
        var query = EntityQueryEnumerator<DayTimeComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.GroupID == DefaultDayTimeGroup)
            {
                targetDayTime = comp;
                break;
            }
        }

        if (targetDayTime == null)
            return;

        var stageDurations = GetStageDurations(targetDayTime);
        if (stageDurations.Count == 0)
            return;

        _currentStage = Math.Clamp(targetDayTime.CurrentStage, 0, stageDurations.Count - 1);
        var curTime = _timing.CurTime;

        var currentStageDuration = stageDurations[_currentStage];
        var elapsedInCurrentStage = TimeSpan.Zero;

        if (!isStageChangeEvent && targetDayTime.StageUpdateTime > curTime)
        {
            var remainingInCurrent = targetDayTime.StageUpdateTime - curTime;
            if (remainingInCurrent < currentStageDuration)
                elapsedInCurrentStage = currentStageDuration - remainingInCurrent;
        }

        int phaseStartStage;
        int phaseTargetStage;

        if (_currentStage >= NightStartStage && _currentStage < NightEndStage)
        {
            phaseStartStage = NightStartStage;
            phaseTargetStage = NightEndStage;
        }
        else
        {
            phaseStartStage = NightEndStage;
            phaseTargetStage = NightStartStage;
        }

        var elapsedBeforeCurrentStage = GetDurationBetweenStages(stageDurations, phaseStartStage, _currentStage);
        var totalPhaseDuration = GetDurationBetweenStages(stageDurations, phaseStartStage, phaseTargetStage);

        _phaseStartTime = curTime - (elapsedBeforeCurrentStage + elapsedInCurrentStage);
        _phaseEndTime = _phaseStartTime + totalPhaseDuration;
    }

    private List<TimeSpan> GetStageDurations(DayTimeComponent comp)
    {
        var list = new List<TimeSpan>();

        if (comp.StageColorTimeData.Count > 0)
        {
            foreach (var item in comp.StageColorTimeData)
            {
                list.Add(item.time);
            }
            return list;
        }

        if (_proto.TryIndex<DayTimePrototype>(comp.PresetID, out var proto))
        {
            foreach (var sec in proto.TimeStages)
            {
                list.Add(TimeSpan.FromSeconds(sec));
            }
        }

        return list;
    }

    private TimeSpan GetDurationBetweenStages(List<TimeSpan> stages, int fromStage, int toStage)
    {
        var total = TimeSpan.Zero;
        var count = stages.Count;
        if (count == 0)
            return total;

        var idx = fromStage % count;
        var target = toStage % count;
        var safety = 0;

        while (idx != target && safety < count)
        {
            total += stages[idx];
            idx = (idx + 1) % count;
            safety++;
        }

        return total;
    }

    public void UpdateAllAlerts()
    {
        var humanQuery = EntityQueryEnumerator<LycantropyComponent>();
        while (humanQuery.MoveNext(out var uid, out _))
        {
            UpdateHumanAlert(uid);
        }

        var wolfQuery = EntityQueryEnumerator<WerewolfComponent>();
        while (wolfQuery.MoveNext(out var uid, out _))
        {
            UpdateWolfAlert(uid);
        }
    }

    private void UpdateHumanAlert(EntityUid uid)
    {
        var severity = IsNight ? AlertSeverityNight : AlertSeverityDay;

        if (IsNight)
        {
            _alerts.ShowAlert(uid, AlertMoon, severity, cooldown: null);
        }
        else
        {
            _alerts.ShowAlert(uid, AlertMoon, severity, cooldown: (_phaseStartTime, _phaseEndTime));
        }
    }

    private void UpdateWolfAlert(EntityUid uid)
    {
        var severity = IsNight ? AlertSeverityNight : AlertSeverityDay;

        if (IsNight)
        {
            _alerts.ShowAlert(uid, AlertMoon, severity, cooldown: (_phaseStartTime, _phaseEndTime));
        }
        else
        {
            _alerts.ShowAlert(uid, AlertMoon, severity, cooldown: null);
        }
    }

    private void ClearAllTransformationAlerts()
    {
        var humanQuery = EntityQueryEnumerator<LycantropyComponent>();
        while (humanQuery.MoveNext(out var uid, out _))
        {
            _alerts.ClearAlert(uid, AlertMoon);
        }

        var wolfQuery = EntityQueryEnumerator<WerewolfComponent>();
        while (wolfQuery.MoveNext(out var uid, out _))
        {
            _alerts.ClearAlert(uid, AlertMoon);
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, MedievalLycantropyRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var werewolves = new List<EntityUid>();
        var query = EntityQueryEnumerator<LycantropyComponent>();
        while (query.MoveNext(out var ent, out _))
        {
            werewolves.Add(ent);
        }

        if (werewolves.Count == 0)
            return;

        args.AddLine(Loc.GetString("round-end-lycantropy-werewolf-count-summary",
            ("count", werewolves.Count)));

        foreach (var item in werewolves)
        {
            args.AddLine(Loc.GetString("round-end-lycantropy-werewolf-summary",
                ("name", Name(item))));
        }
    }

    protected override void ActiveTick(EntityUid uid, MedievalLycantropyRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (_endTime != null && _endTime <= _timing.CurTime)
        {
            _endTime = null;
            GameTicker.EndRound();
            GameTicker.RestartRound();
            return;
        }
    }
}
