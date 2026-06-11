using System.Linq;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cards;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CardsComponent : Component
{
    // Cards imported from the prototype
    // Do not use after initial spawning
    [DataField("cards", required: true)]
    public List<ProtoId<CardPrototype>> _cards { get; set; }

    // List of current cards in the deck
    [ViewVariables, AutoNetworkedField]
    public List<CardData> Cards { get; set; } = new();

    // Number of current cards in the deck
    [ViewVariables]
    public int NumberOfCards => Cards.Count;

    // List of prototype names
    [ViewVariables]
    public List<string> CardPrototypes => Cards.Select(c => (string)c.CardId).ToList();

    // If the deck is flipped or not
    // If flipped then card is face side up
    [DataField, AutoNetworkedField]
    public bool Flipped;

    // If the deck is fanned
    [DataField, AutoNetworkedField]
    public bool Fanned;

    // Max number of cards which will be shown while fanned
    // Big numbers will use a lot of sprite layers
    [DataField, AutoNetworkedField]
    public int MaxFanned = 10;

    [AutoNetworkedField]
    internal bool BeingCherryPicked;

    [DataField, AutoNetworkedField]
    public SoundSpecifier ShuffleSound = new SoundPathSpecifier("/Audio/Effects/cardshuffle.ogg");

    // Sets the base sprite for the whole deck
    // Can be overwritten by card prototypes
    [DataField, AutoNetworkedField]
    public string BaseState = "sc_base";

    // Sets the back sprite for the whole deck
    // Can NOT be overwritten by card prototypes
    [DataField, AutoNetworkedField]
    public string CardBack = "sc_backside";
}
