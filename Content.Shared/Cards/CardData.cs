using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards;

[DataDefinition, NetSerializable, Serializable]
public partial struct CardData
{
    [DataField, AutoNetworkedField]
    public ProtoId<CardPrototype> CardId = string.Empty;

    [DataField, AutoNetworkedField]
    public string BaseState = string.Empty;

    [DataField, AutoNetworkedField]
    public string CardBack = string.Empty;
    public CardData(ProtoId<CardPrototype> cardId, string baseState, string cardBack)
    {
        CardId = cardId;
        BaseState = baseState;
        CardBack = cardBack;
    }
}
