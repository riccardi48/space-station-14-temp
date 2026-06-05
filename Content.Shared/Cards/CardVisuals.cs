using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards;

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
