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
