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
    public readonly List<ProtoId<CardPrototype>> CardList;

    public CardListVisualState(List<ProtoId<CardPrototype>> cardList)
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
    public readonly List<ProtoId<CardPrototype>> Selected;

    public CardAnimationEvent(NetEntity merger, NetEntity mergee, List<ProtoId<CardPrototype>> selected)
    {
        Mergee = mergee;
        Merger = merger;
        Selected = selected;
    }
}
