using Content.Server.BadSmell;
using Content.Server.BadSmell.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.BadSmell;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.BadSmell;

/// <summary>
/// A system that marks items if BadSmellComponent entity hold them
/// </summary>
public sealed class BadSmellItemSystem : EntitySystem
{
    [Dependency] private readonly BadSmellSystem _badSmell = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BadSmellComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<BadSmellItemComponent, ExaminedEvent>(OnExamineItem);
    }

    private void OnInteractHand(Entity<BadSmellComponent> ent, ref InteractHandEvent args)
    {
        if (!TryComp<ItemComponent>(args.Target, out _))
            return;

        if (ent.Comp.SmellLevel < 60)
            return;

        var badSmellItem = EnsureComp<BadSmellItemComponent>(args.Target);

        var newTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SmellLevel * ent.Comp.BadSmellItemMod);

        if (args.User != badSmellItem.Toucher)
        {
            badSmellItem.Toucher = args.User;

            badSmellItem.SmellTime = newTime;
        }
        else
        {
            badSmellItem.SmellTime = newTime > badSmellItem.SmellTime ? newTime : badSmellItem.SmellTime;
        }
    }

    private void OnExamineItem(Entity<BadSmellItemComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<BadSmellItemFeelComponent>(args.Examiner, out var badSmellItemFeel))
            return;

        if (ent.Comp.SmellTime < _timing.CurTime)
        {
            RemComp<BadSmellItemComponent>(ent);
            return;
        }


    }
}
