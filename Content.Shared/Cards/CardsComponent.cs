using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cards;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CardsComponent : Component
{
    [DataField("cards", required: true), AutoNetworkedField]
    public List<ProtoId<CardPrototype>> Cards = new();

    [DataField, AutoNetworkedField]
    public bool Flipped = false;

    [DataField, AutoNetworkedField]
    public bool Fanned = false;

    [DataField, AutoNetworkedField]
    public int MaxFanned = 10;

    [AutoNetworkedField]
    public bool BeingCherryPicked = false;

    [DataField, AutoNetworkedField]
    public SoundSpecifier ShuffleSound = new SoundPathSpecifier("/Audio/Effects/cardshuffle.ogg");

    [DataField, AutoNetworkedField]
    public string BaseState = "sc_base";

}
