using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cards;

/// <summary>
/// Stores the data for an individual card
/// </summary>
[DataDefinition, NetSerializable, Serializable]
public partial struct CardData
{
    /// <summary>
    /// Prototype for the card with layerOne and layerTwo sprites
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CardPrototype> CardId = string.Empty;

    /// <summary>
    /// The sprite for the base layer of the cards in the deck
    /// </summary>
    [DataField, AutoNetworkedField]
    public string BaseState = string.Empty;

    /// <summary>
    /// The sprite for the back layer of the cards
    /// </summary>
    [DataField, AutoNetworkedField]
    public string CardBack = string.Empty;

    public CardData(ProtoId<CardPrototype> cardId, string baseState, string cardBack)
    {
        CardId = cardId;
        BaseState = baseState;
        CardBack = cardBack;
    }
}
