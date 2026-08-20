using Content.Server.Administration;
using Content.Server.Imperial.DayTime;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.Calendar;

public sealed class DaySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    int _curCycle = 0;

    private readonly List<ProtoId<CalendarEventPrototype>> _calendarDeck = new();

    public const int TargetDaysCount = 30;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DayCycleFinishedEvent>(OnDayCycleFinished);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
    }

    private void OnRoundStart(RoundStartedEvent args)
    {
        _curCycle = 0;

        InitializeCalendarDeck();
    }

    private void InitializeCalendarDeck()
    {
        _calendarDeck.Clear();

        var pool = new List<ProtoId<CalendarEventPrototype>>();
        foreach (var proto in _prototype.EnumeratePrototypes<CalendarEventPrototype>())
        {
            if (proto.Tags.Contains("RandomDay"))
                pool.Add(proto.ID);
        }

        if (pool.Count == 0)
            return;

        _random.Shuffle(pool);

        _calendarDeck.EnsureCapacity(TargetDaysCount);

        for (var i = 0; i < TargetDaysCount; i++)
        {
            _calendarDeck.Add(pool[i % pool.Count]);
        }
    }

    private void OnDayCycleFinished(ref DayCycleFinishedEvent args)
    {
        TriggerNextDayNotification();
    }

    public void TriggerNextDayNotification()
    {
        _curCycle++;
        var index = _curCycle % _calendarDeck.Count;
        TriggerDayNotification(_calendarDeck[index]);
    }

    public void TriggerDayNotification(ProtoId<CalendarEventPrototype>? protoId = null)
    {
        var id = protoId ?? "DefaultCalendarEvent";

        if (!_prototype.TryIndex(id, out var proto))
            return;

        RaiseNetworkEvent(new CalendarBroadcastNotificationEvent(id), Filter.Broadcast());
    }
}


[AdminCommand(AdminFlags.VarEdit)]
public sealed class TriggerDayNotificationCommand : IConsoleCommand
{
    public string Command => "triggerdaynotification";
    public string Description => "Triggers the calendar day notification event for testing.";
    public string Help => "Usage: triggerdaynotification <texturePath>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entSys = IoCManager.Resolve<IEntitySystemManager>();
        var daySystem = entSys.GetEntitySystem<DaySystem>();

        if (args.Length == 0)
        {
            daySystem.TriggerDayNotification();
            shell.WriteLine("Triggered default calendar notification.");
            return;
        }

        var path = args[0];
        daySystem.TriggerDayNotification(path);
        shell.WriteLine($"Triggered calendar notification with path: {path}");
    }
}


[AdminCommand(AdminFlags.VarEdit)]
public sealed class TriggerNextDayCycleCommand : IConsoleCommand
{
    public string Command => "triggernextdaycycle";
    public string Description => "Advances the calendar to the next day notification.";
    public string Help => "Usage: triggernextdaycycle";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entSys = IoCManager.Resolve<IEntitySystemManager>();
        var daySystem = entSys.GetEntitySystem<DaySystem>();

        daySystem.TriggerNextDayNotification();
        shell.WriteLine("Triggered next day notification.");
    }
}
