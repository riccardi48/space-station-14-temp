using System.Linq;
using System.Numerics;
using Content.Shared.Cards;
using Content.Shared.Stacks;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Control;

namespace Content.Client.Cards;

public sealed partial class CardSystem
{
    protected override void OpenInspectUI(EntityUid player, Entity<CardsComponent> cards)
    {
        CardInspect menu = new CardInspect { VisualState = GetCardListVisualState(cards) };

        var prototype = Prototype(cards.Owner);
        if (prototype == null)
            return;

        var textures = _sprite.GetPrototypeTextures(prototype).ToList();

        var icon = _sprite.GetPrototypeIcon(prototype);


        TextureRect texture = new TextureRect
        {
            Texture = icon.GetFrame(RsiDirection.South, 0),
            TextureScale = new Vector2(10, 10),
        };
        menu.Box.AddChild(new EntityPrototypeView(prototype, entMan));

        var resourcePath = new ResPath(RsiPath);

        var count = menu.VisualState.CardList.Count;

        for (var i = 0; i < count; i++)
        {
            var card = menu.VisualState.CardList[i];

            var (baseLayer, layerOne, layerTwo) = CardLayers(i);

            if (!_prototypeManager.TryIndex<CardPrototype>(card.CardId, out var cardPrototype))
                continue;

            BuildUICard(cardPrototype, baseLayer, card.BaseState, layerOne, layerTwo, menu, resourcePath);
        }
        _examineSystem.SendExamineControl(player, cards.Owner, menu, false);
    }

    public void BuildUICard(
        CardPrototype prototype,
        string baseLayer,
        string baseSprite,
        string layerOne,
        string layerTwo,
        CardInspect menu,
        ResPath resourcePath
    )
    {
        BuildUILayer(baseLayer, new SpriteSpecifier.Rsi(resourcePath, prototype.LayerBaseState ?? baseSprite), menu);
        if (prototype.LayerOneState != null)
            BuildUILayer(layerOne, new SpriteSpecifier.Rsi(resourcePath, prototype.LayerOneState), menu);
        if (prototype.LayerTwoState != null)
            BuildUILayer(layerTwo, new SpriteSpecifier.Rsi(resourcePath, prototype.LayerTwoState), menu);
    }

    public void BuildUILayer(string layer, SpriteSpecifier.Rsi? specifier, CardInspect menu)
    {
        if (specifier == null)
            return;
        TextureRect texture = new TextureRect
        {
            Name = layer,
            Texture = _sprite.Frame0(specifier),
            TextureScale = new Vector2(10, 10),
        };
        menu.Box.AddChild(texture);
    }

    public void TransformUILayer(int idx, Vector2 movement, Angle rotation, CardInspect menu)
    {
        var texture = menu.Box.GetChild(idx);
        menu.Box.GetChild(idx)
            .Arrange(new UIBox2(movement - texture.DesiredSize / 2, movement + texture.DesiredSize / 2));
    }
}
