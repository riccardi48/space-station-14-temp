using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

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
    protected SharedAudioSystem _audio = default!;

    [Dependency]
    protected SharedContainerSystem _container = default!;

    [Dependency]
    protected IGameTiming _timing = default!;

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

        Appearance.SetData(ent.Owner, CardVisuals.CardList, GetCardListVisualState(ent.Comp), appearance);
        Appearance.SetData(ent.Owner, CardVisuals.IsFlipped, ent.Comp.Flipped, appearance);
        Appearance.SetData(ent.Owner, CardVisuals.IsFanned, ent.Comp.Fanned, appearance);

        ent.Comp.Cards = ent
            .Comp._cards.Select(protoId => new CardData(protoId, ent.Comp.BaseState, ent.Comp.CardBack))
            .ToList();
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

        if (ent.Comp.Fanned == true && ent.Comp.Cards.Count > ent.Comp.MaxFanned)
        {
            ent.Comp.Fanned = false;
            Appearance.SetData(ent, CardVisuals.IsFanned, ent.Comp.Fanned);
        }
        Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(ent.Comp));
        Appearance.SetData(args.Mergee, CardVisuals.CardList, GetCardListVisualState(mergeeComp));

        Dirty(ent.Owner, ent.Comp);
        Dirty(args.Mergee, mergeeComp);
    }

    protected void PlayCardDrawAnimation(Entity<CardsComponent> merger, Entity<CardsComponent> mergee, int delta)
    {
        var selected = MovedCards(mergee.Comp, delta);
        PlayCardAnimation(merger, mergee, selected);
    }

    protected void PlayCardTakeAnimation(Entity<CardsComponent> merger, Entity<CardsComponent> mergee, int cardInx)
    {
        List<CardData> selected = new List<CardData> { mergee.Comp.Cards[cardInx] };
        PlayCardAnimation(merger, mergee, selected);
    }

    protected abstract void PlayCardAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        List<CardData> selected
    );

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

    private void MoveCards(CardsComponent comp1, CardsComponent comp2, List<CardData> selected)
    {
        selected.ForEach(item => comp2.Cards.Remove(item));
        if (comp1.Flipped)
            comp1.Cards = comp1.Cards.Concat(selected).ToList();
        else
        {
            comp1.Cards = selected.Concat(comp1.Cards).ToList();
        }
    }

    protected List<CardData> MovedCards(CardsComponent comp, int delta)
    {
        if (comp.Flipped)
            return comp.Cards.Skip(Math.Max(0, comp.Cards.Count() - delta)).ToList();
        return comp.Cards.Take(delta).ToList();
    }

    private void OnCardsActivate(Entity<CardsComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        TryFlipCards(ent);
    }

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
            (
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
                var cardName = $"{card.CardId}";

                // Want this to have icon of the card
                // Not sure is possible
                AlternativeVerb take = new()
                {
                    Text = Loc.GetString(cardName.Replace('_', '-')),
                    Act = () => TryTakeCard(ent, user, index, out var _),
                    Category = VerbCategory.TakeCard,
                    Priority = priority,
                };

                priority--;

                args.Verbs.Add(take);
            }
        }
    }

    public bool TryShuffleCards(Entity<CardsComponent> cards)
    {
        // This should probably be predicted but it kinda plays a shuffle animation during catchup frames because it isn't predicted.
        // Maybe fine to not predict this then.
        cards.Comp.Cards = cards.Comp.Cards.Shuffle().ToList();
        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        _audio.PlayPredicted(cards.Comp.ShuffleSound, cards, null);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    public bool TryFlipCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Flipped = cards.Comp.Flipped ^ true;
        // cards.Comp.Fanned = false;
        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        Appearance.SetData(cards, CardVisuals.IsFlipped, cards.Comp.Flipped);
        Appearance.SetData(cards, CardVisuals.IsFanned, cards.Comp.Fanned);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    public bool TryFanCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Fanned = cards.Comp.Fanned ^ true;
        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        Appearance.SetData(cards, CardVisuals.IsFanned, cards.Comp.Fanned);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    public bool TryTakeCard(
        Entity<CardsComponent> cards,
        Entity<TransformComponent?> user,
        int cardInx,
        out EntityUid? split
    )
    {
        split = null;
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

        split = Stacks.Split((cards.Owner, stackComp), 1, user.Comp.Coordinates);
        if (split == null)
            return Abort();

        if (!TryComp<CardsComponent>(split, out var newCardsComp))
            return Abort();

        cards.Comp.BeingCherryPicked = false;

        PlayCardTakeAnimation((split.Value, newCardsComp), cards, cardInx);
        MoveCards(newCardsComp, cards.Comp, new List<CardData> { cards.Comp.Cards[cardInx] });

        if (newCardsComp.Cards.Count == 1)
        {
            newCardsComp.Flipped = cards.Comp.Flipped;
            newCardsComp.Fanned = cards.Comp.Fanned;
        }

        Hands.PickupOrDrop(user.Owner, split.Value);
        Popup.PopupCursor(Loc.GetString("comp-stack-split"), user.Owner);

        Appearance.SetData(cards, CardVisuals.CardList, GetCardListVisualState(cards.Comp));
        if (TryComp<AppearanceComponent>(split, out var appearance))
        {
            Appearance.SetData(split.Value, CardVisuals.CardList, GetCardListVisualState(newCardsComp), appearance);
            Appearance.SetData(split.Value, CardVisuals.IsFlipped, newCardsComp.Flipped, appearance);
            Appearance.SetData(split.Value, CardVisuals.IsFanned, newCardsComp.Fanned, appearance);
        }

        Dirty(cards.Owner, cards.Comp);
        Dirty(split.Value, newCardsComp);

        return true;
    }

    protected CardListVisualState GetCardListVisualState(CardsComponent cards)
    {
        if (!cards.Flipped)
        {
            if (cards.Fanned)
                return new CardListVisualState(cards.Cards.Take(cards.MaxFanned).ToList());
            return new CardListVisualState(cards.Cards.Take(1).ToList());
        }
        if (cards.Fanned)
            return new CardListVisualState(cards.Cards.TakeLast(cards.MaxFanned).ToList());
        return new CardListVisualState(cards.Cards.TakeLast(1).ToList());
    }

}
