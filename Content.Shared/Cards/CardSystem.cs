using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
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
    protected SharedAudioSystem Audio = default!;

    [Dependency]
    protected SharedContainerSystem Container = default!;

    [Dependency]
    protected IGameTiming Timing = default!;

    [Dependency]
    protected SharedTransformSystem TransformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CardsComponent, ComponentInit>(OnCardsInit);
        SubscribeLocalEvent<CardsComponent, MergeEvent>(OnMergeEvent);
        SubscribeLocalEvent<CardsComponent, StackSplitEvent>(OnSplitEvent);
        SubscribeLocalEvent<CardsComponent, EntGotInsertedIntoContainerMessage>(OnCardsContainerInserted);

        SubscribeLocalEvent<CardsComponent, ComponentStartup>(OnCardsStarted);
        SubscribeLocalEvent<CardsComponent, ExaminedEvent>(OnCardsExamined);
        SubscribeLocalEvent<CardsComponent, StackCountChangedEvent>(OnStackCountChanged);

        SubscribeLocalEvent<CardsComponent, ActivateInWorldEvent>(OnCardsActivate);
        SubscribeLocalEvent<CardsComponent, UseInHandEvent>(OnCardsUse);
        SubscribeLocalEvent<CardsComponent, GetVerbsEvent<AlternativeVerb>>(OnCardsAlternativeInteract);
    }

    private void OnCardsInit(Entity<CardsComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.Cards.Count == 0)
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

        PlayCardDrawAnimation(ent, (args.Mergee, mergeeComp), args.Delta);
        TakeFromDeck(ent.Comp, mergeeComp, args.Delta);
        UpdateVisualState(ent);
        UpdateVisualState((args.Mergee, mergeeComp));

        Dirty(ent.Owner, ent.Comp);
        Dirty(args.Mergee, mergeeComp);
    }

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

        UpdateVisualState(ent);
        UpdateVisualState((args.NewId, splitComp));

        Dirty(ent.Owner, ent.Comp);
        Dirty(args.NewId, splitComp);
    }

    private void OnCardsContainerInserted(Entity<CardsComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (ent.Comp.Fanned && !Hands.EnumerateHands(args.Container.Owner).Contains(args.Container.ID))
            TryFanCards(ent);
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
            comp1.Cards = selected.Concat(comp1.Cards).ToList();
    }

    private List<CardData> MovedCards(CardsComponent comp, int delta)
    {
        if (comp.Flipped)
            return comp.Cards.TakeLast(delta).ToList();
        return comp.Cards.Take(delta).ToList();
    }

    public bool TryShuffleCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Cards = cards.Comp.Cards.Shuffle().ToList();
        UpdateVisualState(cards);
        Audio.PlayPredicted(cards.Comp.ShuffleSound, cards, null);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    public bool TryFlipCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Flipped = !cards.Comp.Flipped;
        UpdateVisualState(cards);
        Dirty(cards.Owner, cards.Comp);
        return true;
    }

    public bool TryFanCards(Entity<CardsComponent> cards)
    {
        cards.Comp.Fanned = !cards.Comp.Fanned;
        UpdateVisualState(cards);
        UpdateStackCount(cards);
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

        if (
            Hands.TryGetActiveItem(user.Owner, out var recipient)
            && TryComp<StackComponent>(recipient, out var recipientStack)
            && Stacks.TryMergeStacks((cards.Owner, stackComp), (recipient.Value, recipientStack), out _, amount: 1)
        )
        {
            cards.Comp.BeingCherryPicked = false;
            return false;
        }

        split = Stacks.Split((cards.Owner, stackComp), 1, user.Comp.Coordinates);
        if (split == null)
        {
            cards.Comp.BeingCherryPicked = false;
            return false;
        }

        if (!TryComp<CardsComponent>(split, out var newCardsComp))
        {
            cards.Comp.BeingCherryPicked = false;
            return false;
        }

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

        UpdateVisualState(cards);
        UpdateVisualState((split.Value, newCardsComp));

        Dirty(cards.Owner, cards.Comp);
        Dirty(split.Value, newCardsComp);

        return true;
    }
}
