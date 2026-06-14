using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Cards;

public abstract partial class SharedCardSystem
{
    private void InitializeVisuals()
    {
        SubscribeLocalEvent<CardsComponent, ComponentStartup>(OnCardsStarted);
        SubscribeLocalEvent<CardsComponent, ExaminedEvent>(OnCardsExamined);
        SubscribeLocalEvent<CardsComponent, GetVerbsEvent<ExamineVerb>>(OnCardsExaminableVerb);
        SubscribeLocalEvent<CardsComponent, StackCountChangedEvent>(OnStackCountChanged);
    }

    private void OnCardsStarted(Entity<CardsComponent> ent, ref ComponentStartup args)
    {
        UpdateVisualState(ent);
    }

    private void OnCardsExamined(Entity<CardsComponent> ent, ref ExaminedEvent args)
    {
        // Can only see top card if the deck is flipped
        if (!args.IsInDetailsRange || !ent.Comp.Flipped)
            return;

        var cards = GetCardListVisualState(ent.Comp);
        var cardName = (string)cards.CardList.Last().CardId;
        args.PushMarkup(
            Loc.GetString("comp-cards-examine-detail", ("card", Loc.GetString(cardName.Replace('_', '-'))))
        );
    }

    /// <summary>
    ///     Full reagent scan, such as with chemical analysis goggles.
    /// </summary>
    private void OnCardsExaminableVerb(Entity<CardsComponent> cards, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var target = args.Target;
        var user = args.User;
        var verb = new ExamineVerb()
        {
            Act = () => OpenInspectUI(user, cards),
            Text = "Inspect",
            Message = "Inspect",
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),
        };
        args.Verbs.Add(verb);
    }

    protected abstract void OpenInspectUI(EntityUid player, Entity<CardsComponent> cards);

    private void OnStackCountChanged(Entity<CardsComponent> ent, ref StackCountChangedEvent args)
    {
        // Makes sure the sudo stack count for visuals is up to date
        if (args.NewCount == 0)
            return;
        UpdateStackCount(ent);
    }

    private void UpdateStackCount(Entity<CardsComponent> ent)
    {
        // If the deck is fanned it changes the visual count to what ever number is below the fanned cards
        // This means that a deck with the same number of cards as the MaxFanned will not have a stack extending of the cards
        var visualState = GetCardListVisualState(ent.Comp);
        Appearance.SetData(
            ent.Owner,
            StackVisuals.Actual,
            ent.Comp.Cards.Count - visualState.Count - ent.Comp.AmountCycled + 1
        );
    }

    private void UpdateVisualState(Entity<CardsComponent> ent)
    {
        UpdateStackCount(ent);
        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(ent.Comp), appearance);
            Appearance.SetData(ent, CardVisuals.IsFlipped, ent.Comp.Flipped, appearance);
        }
    }

    public CardListVisualState GetCardListVisualState(CardsComponent cards)
    {
        // This gets the cards the player could see
        // This function controls a lot of the client side sprite
        // Very important this is correct
        var count = Math.Min(cards.Fanned ? cards.MaxFanned : 1, cards.Cards.Count);
        var start = cards.Flipped ? Math.Max(cards.Cards.Count - count - cards.AmountCycled, 0) : cards.AmountCycled;
        return new CardListVisualState(cards.Cards, start, count);
    }

    protected void PlayCardDrawAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int delta,
        bool playOnUser = false
    )
    {
        // Plays animation for a split or merge where the cards taken are from the top or bottom
        var selected = MovedCards(mergee.Comp, delta);
        PlayCardAnimation(merger, mergee, selected, playOnUser: playOnUser);
    }

    protected void PlayCardTakeAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int cardInx,
        bool playOnUser = false
    )
    {
        // Plays animation for a split or merge where the cards taken are from somewhere in the deck
        var card = GetCardFromInx(mergee.Comp.Cards, cardInx);
        List<CardData> selected = new List<CardData> { card };
        PlayCardAnimation(merger, mergee, selected, playOnUser: playOnUser);
    }

    private void PlayCardAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        List<CardData> selected,
        bool playOnUser = false
    )
    {
        // Animation function needs to not send any entityUid information as the entity may have been merged and deleted on other clients when played
        if (!TryComp<StackComponent>(mergee.Owner, out var originalStackComp))
            return;

        var xform = Transform(mergee.Owner);
        PlayCardAnimation(
            Transform(merger).Coordinates,
            mergee.Comp.Flipped,
            xform.Coordinates,
            xform.LocalRotation,
            originalStackComp.StackTypeId,
            selected,
            playOnUser: playOnUser
        );
    }

    protected abstract void PlayCardAnimation(
        EntityCoordinates mergerCoords,
        bool mergeeFlipped,
        EntityCoordinates mergeeCoords,
        Angle mergeeRotation,
        ProtoId<StackPrototype> stackId,
        List<CardData> selected,
        bool playOnUser = false
    );
}

[Serializable, NetSerializable]
public enum CardVisuals : byte
{
    IsFlipped,
    CardList,
}

[Serializable, NetSerializable]
public sealed class CardListVisualState : ICloneable
{
    public readonly List<CardData> CardList;
    public readonly int Start;
    public readonly int Count;

    public CardListVisualState(List<CardData> cardList, int start, int count)
    {
        CardList = cardList;
        Start = start;
        Count = count;
    }

    public object Clone() => new CardListVisualState(CardList, Start, Count);
}
