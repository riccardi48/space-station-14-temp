using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards;

[Serializable, NetSerializable]
public enum CardVisuals : byte
{
    IsFlipped,
    IsFanned,
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
