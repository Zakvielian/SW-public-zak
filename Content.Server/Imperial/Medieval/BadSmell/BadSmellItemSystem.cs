using System.ComponentModel;
using Content.Server.BadSmell;
using Content.Server.BadSmell.Components;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Imperial.Medieval.BadSmell;
using Content.Shared.Imperial.Medieval.Perfume;
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

        SubscribeLocalEvent<BadSmellComponent, DidEquipHandEvent>(OnItemEquipped);
        SubscribeLocalEvent<BadSmellItemComponent, ExaminedEvent>(OnExamineItem);
    }

    private void OnItemEquipped(Entity<BadSmellComponent> ent, ref DidEquipHandEvent args)
    {
        if (!TryComp<ItemComponent>(args.Equipped, out _))
            return;

        if (ent.Comp.SmellLevel < 30)
            return;

        var badSmellItem = EnsureComp<BadSmellItemComponent>(args.Equipped);

        var newTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SmellLevel * ent.Comp.BadSmellItemMod);

        badSmellItem.SmellProfile = ent.Comp.Profile;

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

        if (ent.Comp.Toucher is not { } toucher || !Exists(toucher) || ent.Comp.SmellProfile is not { } profile)
            return;

        var hex = profile.GetColor().ToHex();
        var smellText = profile.ToFormattedString();

        var msg = Loc.GetString("smell-examined",
            ("color", hex),
            ("smell", smellText));

        args.PushMarkup(msg);
    }
}
