using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Stacks;
using Content.Shared.Verbs;

namespace Content.Shared.Cards;

public abstract partial class SharedCardSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CardsComponent, MergeEvent>(OnMergeEvent);
        SubscribeLocalEvent<CardsComponent, StackSplitEvent>(OnSplitEvent);
        SubscribeLocalEvent<CardsComponent, GetVerbsEvent<AlternativeVerb>>(OnCardsAlternativeInteract);

        SubscribeLocalEvent<CardsComponent, ActivateInWorldEvent>(OnCardsActivate);
        SubscribeLocalEvent<CardsComponent, UseInHandEvent>(OnCardsUse);
    }

    private void OnMergeEvent(Entity<CardsComponent> ent, ref MergeEvent args)
    {
        if (!TryComp<CardsComponent>(args.Mergee, out var mergeeComp))
            return;

        if (args.Delta <= 0)
            return;
        if (args.TargetDelta != null)
            PlayCardDrawAnimation(ent, (args.Mergee, mergeeComp), args.Delta);
        MoveCards(ent.Comp, mergeeComp, args.Delta);

        Dirty(ent.Owner, ent.Comp);
        Dirty(args.Mergee, mergeeComp);
    }

    protected virtual void PlayCardDrawAnimation(Entity<CardsComponent> merger, Entity<CardsComponent> mergee, int delta){}

    private void OnSplitEvent(Entity<CardsComponent> ent, ref StackSplitEvent args)
    {
        if (
            !TryComp<CardsComponent>(args.NewId, out var splitComp)
            || !TryComp<StackComponent>(args.NewId, out var splitStackComp)
        )
            return;

        var delta = splitStackComp.Count;
        PlayCardDrawAnimation((args.NewId, splitComp), ent, delta);
        MoveCards(splitComp, ent.Comp, delta);
        splitComp.Flipped = ent.Comp.Flipped;
        splitComp.Fanned = ent.Comp.Fanned;
        Dirty(ent.Owner, ent.Comp);
        Dirty(args.NewId, splitComp);
    }

    private void MoveCards(CardsComponent comp1, CardsComponent comp2, int delta)
    {
        var selected = MovedCards(comp2, delta);
        selected.ForEach(item => comp2.Cards.Remove(item));
        comp1.Cards = selected.Concat(comp1.Cards).ToList();

        var logString = "movedCards ";
        selected.ForEach(item => logString += $"{item}");
        Log.Info(logString);
    }

    protected List<int> MovedCards(CardsComponent comp, int delta)
    {
        if (comp.Flipped)
            return comp.Cards.Skip(Math.Max(0, comp.Cards.Count() - delta)).ToList();
        return comp.Cards.Take(delta).ToList();
    }

    /// <summary>
    /// Called when user "Activated In World" (E) with the gun as the target
    /// </summary>
    private void OnCardsActivate(Entity<CardsComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        TryFlipCards(ent);
    }

    /// <summary>
    /// Called when gun was "Activated In Hand" (Z)
    /// </summary>
    private void OnCardsUse(Entity<CardsComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Flipped && !ent.Comp.Fanned)
            TryFanCards(ent);
        else
        {
            TryFlipCards(ent);
        }
    }

    private void OnCardsAlternativeInteract(Entity<CardsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract || args.Hands == null)
            return;

        AlternativeVerb flip = new()
        {
            Text = Loc.GetString("comp-cards-flip"),
            Act = () => TryFlipCards(ent),
            Priority = -98,
        };
        args.Verbs.Add(flip);


        AlternativeVerb shuffle = new()
        {
            Text = Loc.GetString("comp-cards-shuffle"),
            Act = () => TryShuffleCards(ent),
            Priority = -99,
            CloseMenu = false,
        };

        args.Verbs.Add(shuffle);

        if (ent.Comp.Flipped)
        {
            AlternativeVerb fan = new()
            {
                Text = Loc.GetString("comp-cards-fan"),
                Act = () => TryFanCards(ent),
                Priority = -100,
            };

            args.Verbs.Add(fan);
        }
    }

    private bool TryShuffleCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Cards = cards.Comp.Cards.Shuffle().ToList();
        Log.Info("Shuffled");
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    private bool TryFlipCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Flipped = cards.Comp.Flipped ^ true;
        cards.Comp.Fanned = false;
        Log.Info("Flipped");
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    private bool TryFanCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Fanned = cards.Comp.Fanned ^ true;
        Log.Info("Fanned");
        Dirty(cards.Owner, cards.Comp);
        return true;
    }
}
