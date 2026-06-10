using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
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
        if (!args.IsInDetailsRange || !ent.Comp.Flipped)
            return;

        var cards = GetCardListVisualState(ent.Comp);
        var cardName = (string)cards.CardList.Last().CardId;
        args.PushMarkup(
            Loc.GetString("comp-cards-examine-detail", ("card", Loc.GetString(cardName.Replace('_', '-'))))
        );
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

    protected void PlayCardDrawAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int delta,
        bool playOnUser = false
    )
    {
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
        List<CardData> selected = new List<CardData> { mergee.Comp.Cards[cardInx] };
        PlayCardAnimation(merger, mergee, selected, playOnUser: playOnUser);
    }

    private void PlayCardAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        List<CardData> selected,
        bool playOnUser = false
    )
    {
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

    public CardListVisualState(List<CardData> cardList)
    {
        CardList = cardList;
    }

    public object Clone() => new CardListVisualState(CardList);
}

[Serializable, NetSerializable]
public sealed class CardAnimationEvent : EntityEventArgs
{
    public readonly NetCoordinates MergerCoords;
    public readonly bool MergeeFlipped;
    public readonly NetCoordinates MergeeCoords;
    public readonly Angle MergeeRotation;
    public readonly ProtoId<StackPrototype> StackId;
    public readonly List<CardData> Selected;

    public CardAnimationEvent(
        NetCoordinates mergerCoords,
        bool mergeeFlipped,
        NetCoordinates mergeeCoords,
        Angle mergeeRotation,
        ProtoId<StackPrototype> stackId,
        List<CardData> selected
    )
    {
        MergerCoords = mergerCoords;
        MergeeFlipped = mergeeFlipped;
        MergeeCoords = mergeeCoords;
        MergeeRotation = mergeeRotation;
        StackId = stackId;
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
