using Robust.Shared.Serialization;

namespace Content.Shared.Cards
{
    [DataDefinition, NetSerializable, Serializable]
    public sealed partial class CardData
    {
        [DataField]
        public int CardId;

        CardData(int cardId)
        {
            CardId = cardId;
        }
    }
}
