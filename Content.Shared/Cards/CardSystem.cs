using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cards;

public abstract partial class SharedCardSystem : EntitySystem
{
    [Dependency]
    protected SharedStackSystem Stacks = default!;

    [Dependency]
    protected SharedHandsSystem Hands = default!;

    [Dependency]
    protected SharedPopupSystem Popup = default!;

    [Dependency]
    protected SharedAppearanceSystem Appearance = default!;

    [Dependency]
    private SharedAudioSystem _audio = default!;

    [Dependency]
    private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CardsComponent, MergeEvent>(OnMergeEvent);
        SubscribeLocalEvent<CardsComponent, StackSplitEvent>(OnSplitEvent);
        SubscribeLocalEvent<CardsComponent, GetVerbsEvent<AlternativeVerb>>(OnCardsAlternativeInteract);
        SubscribeLocalEvent<CardsComponent, ComponentStartup>(OnCardsStarted);

        SubscribeLocalEvent<CardsComponent, ActivateInWorldEvent>(OnCardsActivate);
        SubscribeLocalEvent<CardsComponent, UseInHandEvent>(OnCardsUse);
        SubscribeLocalEvent<CardsComponent, EntGotInsertedIntoContainerMessage>(OnCardsContainerInserted);
    }

    private void OnCardsStarted(Entity<CardsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent.Owner, out AppearanceComponent? appearance))
            return;

        Appearance.SetData(ent.Owner, CardVisuals.CardList, ent.Comp.Cards, appearance);
        Appearance.SetData(ent.Owner, CardVisuals.IsFlipped, ent.Comp.Flipped, appearance);
        Appearance.SetData(ent.Owner, CardVisuals.IsFanned, ent.Comp.Fanned, appearance);
    }

    private void OnMergeEvent(Entity<CardsComponent> ent, ref MergeEvent args)
    {
        if (ent.Comp.BeingCherryPicked)
            return;

        if (!TryComp<CardsComponent>(args.Mergee, out var mergeeComp))
            return;

        if (args.Delta <= 0)
            return;
        if (args.TargetDelta != null)
            PlayCardDrawAnimation(ent, (args.Mergee, mergeeComp), args.Delta);
        TakeFromDeck(ent.Comp, mergeeComp, args.Delta);

        Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(ent.Comp));
        Appearance.SetData(args.Mergee, CardVisuals.CardList, GetCardListVisualState(mergeeComp));

        Dirty(ent.Owner, ent.Comp);
        Dirty(args.Mergee, mergeeComp);
    }

    protected virtual void PlayCardDrawAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int delta
    ) { }

    protected virtual void PlayCardTakeAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int cardInx
    ) { }

    private void OnSplitEvent(Entity<CardsComponent> ent, ref StackSplitEvent args)
    {
        if (ent.Comp.BeingCherryPicked)
            return;
        if (
            !TryComp<CardsComponent>(args.NewId, out var splitComp)
            || !TryComp<StackComponent>(args.NewId, out var splitStackComp)
        )
            return;

        var delta = splitStackComp.Count;
        PlayCardDrawAnimation((args.NewId, splitComp), ent, delta);
        TakeFromDeck(splitComp, ent.Comp, delta);
        splitComp.Flipped = ent.Comp.Flipped;
        splitComp.Fanned = ent.Comp.Fanned;
        Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(ent.Comp));
        if (TryComp<AppearanceComponent>(args.NewId, out var appearance))
        {
            Appearance.SetData(args.NewId, CardVisuals.CardList, GetCardListVisualState(splitComp), appearance);
            Appearance.SetData(args.NewId, CardVisuals.IsFlipped, splitComp.Flipped, appearance);
            Appearance.SetData(args.NewId, CardVisuals.IsFanned, splitComp.Fanned, appearance);
        }
        Dirty(ent.Owner, ent.Comp);
        Dirty(args.NewId, splitComp);
    }

    private void OnCardsContainerInserted(Entity<CardsComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        Log.Info($"{args.Container.ID}");
        if (ent.Comp.Fanned && !Hands.EnumerateHands(args.Container.Owner).ToList().Contains(args.Container.ID))
        {
            TryFanCards(ent);
        }
    }

    private void TakeFromDeck(CardsComponent comp1, CardsComponent comp2, int delta)
    {
        var selected = MovedCards(comp2, delta);
        MoveCards(comp1, comp2, selected);
    }

    private void MoveCards(CardsComponent comp1, CardsComponent comp2, List<ProtoId<CardPrototype>> selected)
    {
        selected.ForEach(item => comp2.Cards.Remove(item));
        if (comp1.Flipped)
            comp1.Cards = comp1.Cards.Concat(selected).ToList();
        else
        {
            comp1.Cards = selected.Concat(comp1.Cards).ToList();
        }

        var logString = "movedCards ";
        selected.ForEach(item => logString += $"{item}");
        Log.Info(logString);
    }

    protected List<ProtoId<CardPrototype>> MovedCards(CardsComponent comp, int delta)
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

        var user = args.User;

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
        };

        args.Verbs.Add(shuffle);

        if (
            ent.Comp.Flipped
            && (
                !_container.TryGetContainingContainer(ent.Owner, out var container)
                || Hands.EnumerateHands(container.Owner).ToList().Contains(container.ID)
            )
        )
        {
            AlternativeVerb fan = new()
            {
                Text = Loc.GetString("comp-cards-fan"),
                Act = () => TryFanCards(ent),
                Priority = -100,
            };

            args.Verbs.Add(fan);
        }

        if (ent.Comp.Fanned && Hands.GetActiveItem(user) != ent.Owner)
        {
            var priority = -200;
            for (var i = 0; i < ent.Comp.Cards.Count; i++)
            {
                var index = ent.Comp.Cards.Count - i - 1;
                var card = ent.Comp.Cards[index];
                var cardName = $"{card}";

                Log.Info($"{i} {ent.Comp.Cards.Count}");
                AlternativeVerb take = new()
                {
                    Text = Loc.GetString(cardName.Replace('_', '-')),
                    Act = () => TryTakeCard(ent, user, index),
                    Category = VerbCategory.TakeCard,
                    Priority = priority,
                };

                priority--;

                args.Verbs.Add(take);
            }
        }
    }

    private bool TryShuffleCards(Entity<CardsComponent> cards)
    {
        // This should probably be predicted but it kinda plays a shuffle animation during catchup frames because it isn't predicted.
        // Maybe fine to not predict this then.
        cards.Comp.Cards = cards.Comp.Cards.Shuffle().ToList();
        Log.Info("Shuffled");
        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        _audio.PlayPredicted(cards.Comp.ShuffleSound, cards, null);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    private bool TryFlipCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Flipped = cards.Comp.Flipped ^ true;
        cards.Comp.Fanned = false;
        Log.Info("Flipped");
        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        Appearance.SetData(cards, CardVisuals.IsFlipped, cards.Comp.Flipped);
        Appearance.SetData(cards, CardVisuals.IsFanned, cards.Comp.Fanned);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    private bool TryFanCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Fanned = cards.Comp.Fanned ^ true;
        Log.Info("Fanned");
        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        Appearance.SetData(cards, CardVisuals.IsFanned, cards.Comp.Fanned);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    private bool TryTakeCard(Entity<CardsComponent> cards, Entity<TransformComponent?> user, int cardInx)
    {
        if (!cards.Comp.Fanned || !cards.Comp.Flipped)
            return false;
        if (!Resolve(user.Owner, ref user.Comp, false) || !TryComp<StackComponent>(cards.Owner, out var stackComp))
            return false;

        cards.Comp.BeingCherryPicked = true;

        bool Abort()
        {
            cards.Comp.BeingCherryPicked = false;
            return false;
        }

        if (
            Hands.TryGetActiveItem(user.Owner, out var recipient)
            && TryComp<StackComponent>(recipient, out var recipientStack)
            && Stacks.TryMergeStacks((cards.Owner, stackComp), (recipient.Value, recipientStack), out _, amount: 1)
        )
            return Abort();

        if (Stacks.Split((cards.Owner, stackComp), 1, user.Comp.Coordinates) is not { } split)
            return Abort();

        if (!TryComp<CardsComponent>(split, out var newCardsComp))
            return Abort();

        cards.Comp.BeingCherryPicked = false;

        PlayCardTakeAnimation((split, newCardsComp), cards, cardInx);
        MoveCards(newCardsComp, cards.Comp, new List<ProtoId<CardPrototype>> { cards.Comp.Cards[cardInx] });

        if (newCardsComp.Cards.Count == 1)
        {
            newCardsComp.Flipped = cards.Comp.Flipped;
            newCardsComp.Fanned = cards.Comp.Fanned;
        }

        Hands.PickupOrDrop(user.Owner, split);
        Popup.PopupCursor(Loc.GetString("comp-stack-split"), user.Owner);

        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        if (TryComp<AppearanceComponent>(split, out var appearance))
        {
            Appearance.SetData(split, CardVisuals.CardList, GetCardListVisualState(newCardsComp), appearance);
            Appearance.SetData(split, CardVisuals.IsFlipped, newCardsComp.Flipped, appearance);
            Appearance.SetData(split, CardVisuals.IsFanned, newCardsComp.Fanned, appearance);
        }

        Dirty(cards.Owner, cards.Comp);
        Dirty(split, newCardsComp);

        return true;
    }

    protected CardListVisualState GetCardListVisualState(CardsComponent cards)
    {
        if (!cards.Flipped)
            return new CardListVisualState(new List<ProtoId<CardPrototype>>());
        if (cards.Fanned)
            return new CardListVisualState(cards.Cards);
        return new CardListVisualState(cards.Cards.TakeLast(1).ToList());
    }
}
