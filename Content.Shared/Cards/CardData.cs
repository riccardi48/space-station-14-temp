using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards
{
    [DataDefinition, NetSerializable, Serializable]
    public partial struct CardData
    {
        [DataField]
        public ProtoId<CardPrototype> CardId { get; private set; }

        public CardData(ProtoId<CardPrototype> cardId)
        {
            CardId = cardId;
        }
    }
}
