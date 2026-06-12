using System.Numerics;
using Content.Shared.Cards;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Cards;

public sealed partial class CardSystem
{
    protected override void OpenInspectUI(EntityUid player, Entity<CardsComponent> cards)
    {
        CardInspect menu = new CardInspect { VisualState = GetCardListVisualState(cards) };

        var spriteView = new SpriteView
        {
            OverrideDirection = Direction.South,
            SetSize = new Vector2(250, 250),
            Stretch = SpriteView.StretchMode.Fill,
        };
        spriteView.SetEntity(cards);
        menu.SpriteHolder.AddChild(spriteView);

        _examineSystem.SendExamineControl(player, cards.Owner, menu, false);
    }
}
