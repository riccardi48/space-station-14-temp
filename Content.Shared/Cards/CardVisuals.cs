using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards;

[Serializable, NetSerializable]
public enum CardVisuals : byte
{
    IsFlipped,
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
