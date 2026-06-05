using Robust.Shared.GameStates;

namespace Content.Shared.Cards;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CardsComponent : Component
{
    [DataField("cards", required: true), AutoNetworkedField]
    public List<CardData> Cards = new();

    [DataField, AutoNetworkedField]
    public bool Flipped = false;

    [DataField, AutoNetworkedField]
    public bool Fanned = false;

    [AutoNetworkedField]
    public bool BeingCherryPicked = false;
}
