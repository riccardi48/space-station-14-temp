using Robust.Shared.Prototypes;

namespace Content.Shared.Cards;

[Prototype]
public sealed partial class CardPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField("layerOneState")]
    public string? LayerOneState { get; private set; }

    [DataField("layerOneColor")]
    public Color? LayerOneColor { get; private set; }

    [DataField("layerTwoState")]
    public string? LayerTwoState { get; private set; }

    [DataField("layerTwoColor")]
    public Color? LayerTwoColor { get; private set; }
}
