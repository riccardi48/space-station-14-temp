using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards;

public abstract partial class SharedCardSystem
{
    private void OnCardsStarted(Entity<CardsComponent> ent, ref ComponentStartup args)
    {
        UpdateVisualState(ent);
        UpdateStackCount(ent);
    }

    private void OnCardsExamined(Entity<CardsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Flipped)
        {
            var cards = GetCardListVisualState(ent.Comp);
            var cardName = (string)cards.CardList.Last().CardId;
            args.PushMarkup(
                Loc.GetString("comp-cards-examine-detail", ("card", Loc.GetString(cardName.Replace('_', '-'))))
            );
        }
    }

    private void OnStackCountChanged(Entity<CardsComponent> ent, ref StackCountChangedEvent args)
    {
        if (!ent.Comp.Fanned || args.NewCount == 0)
            return;
        UpdateStackCount(ent);
    }

    private void UpdateStackCount(Entity<CardsComponent> ent)
    {
        var cardsCount = ent.Comp.Cards.Count;
        var dummyCount = cardsCount >= ent.Comp.MaxFanned ? cardsCount - ent.Comp.MaxFanned + 1 : 1;
        Appearance.SetData(ent.Owner, StackVisuals.Actual, dummyCount);
    }

    private void UpdateVisualState(Entity<CardsComponent> ent)
    {
        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(ent.Comp), appearance);
            Appearance.SetData(ent, CardVisuals.IsFlipped, ent.Comp.Flipped, appearance);
        }
    }

    protected CardListVisualState GetCardListVisualState(CardsComponent cards)
    {
        var count = cards.Fanned ? cards.MaxFanned : 1;
        var selected = cards.Flipped ? cards.Cards.TakeLast(count) : cards.Cards.Take(count);
        return new CardListVisualState(selected.ToList());
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

    public CardListVisualState(List<CardData> cardList)
    {
        CardList = cardList;
    }

    public object Clone()
    {
        return new CardListVisualState(CardList);
    }
}

[Serializable, NetSerializable]
public sealed class CardAnimationEvent : EntityEventArgs
{
    public readonly NetEntity Mergee;
    public readonly NetEntity Merger;
    public readonly List<CardData> Selected;

    public CardAnimationEvent(NetEntity merger, NetEntity mergee, List<CardData> selected)
    {
        Mergee = mergee;
        Merger = merger;
        Selected = selected;
    }
}

[Serializable, NetSerializable]
public sealed class CardDropMergeEvent : EntityEventArgs
{
    public readonly NetEntity Mergee;
    public readonly NetEntity Merger;

    public CardDropMergeEvent(NetEntity merger, NetEntity mergee)
    {
        Mergee = mergee;
        Merger = merger;
    }
}
