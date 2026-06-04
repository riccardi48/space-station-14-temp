using System.Linq;
using Content.Shared.Stacks;

namespace Content.Shared.Cards;

public sealed partial class SharedCardSystem : EntitySystem
{
    [Dependency] private SharedStackSystem _stacks = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CardsComponent, MergeEvent>(OnMergeEvent);
        SubscribeLocalEvent<CardsComponent, StackSplitEvent>(OnSplitEvent);
    }

    private void OnMergeEvent(Entity<CardsComponent> ent, ref MergeEvent args)
    {
        if (!TryComp<CardsComponent>(args.Mergee, out var mergeeComp))
            return;

        if (args.Delta <= 0)
            return;
        MoveCards(ent.Comp, mergeeComp, args.Delta);
        Dirty(ent.Owner, ent.Comp);
        Dirty(args.Mergee, mergeeComp);
    }

    private void OnSplitEvent(Entity<CardsComponent> ent, ref StackSplitEvent args)
    {
        if (
            !TryComp<CardsComponent>(args.NewId, out var splitComp)
            || !TryComp<StackComponent>(args.NewId, out var splitStackComp)
        )
            return;

        var delta = splitStackComp.Count;
        MoveCards(splitComp, ent.Comp, delta);
        Dirty(ent.Owner, ent.Comp);
        Dirty(args.NewId, splitComp);
    }

    private void MoveCards(CardsComponent comp1, CardsComponent comp2, int delta)
    {
        var selected = comp2.Cards.Take(delta).ToList();
        selected.ForEach(item => comp2.Cards.Remove(item));
        selected.ForEach(item => Log.Error($"{item}"));
        comp1.Cards = selected.Concat(comp1.Cards).ToList();

    }

}
