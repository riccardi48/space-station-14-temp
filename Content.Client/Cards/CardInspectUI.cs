using System.Numerics;
using Content.Shared.Cards;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Cards;

public sealed partial class CardSystem
{
    public event Action<CardData>? OnCardButtonClicked;

    protected override void OpenInspectUI(EntityUid player, Entity<CardsComponent> cards)
    {
        CardInspect menu = new CardInspect();

        var spriteView = new CardSpriteView
        {
            OverrideDirection = Direction.South,
            SetSize = new Vector2(250, 250),
            Stretch = SpriteView.StretchMode.Fill,
        };
        spriteView.SetEntity(cards);
        spriteView.SetVisualStateFunc(GetCardListVisualState);
        menu.SpriteHolder.AddChild(spriteView);
        _examineSystem.SendExamineControl(player, cards.Owner, menu, false);
        spriteView.SetCards(cards, 12, 20, OnCardButtonClicked);
    }
}

public sealed class CardSpriteView : SpriteView
{
    private readonly Control _buttonOverlay;
    private CardsComponent? _cards;
    private float _cardWidth;
    private float _cardHeight;
    private Action<CardData>? _onCardClicked;

    private Func<CardsComponent, CardListVisualState>? _getVisualState;

    private CardListVisualState VisualState
    {
        get
        {
            if (_cards == null || _getVisualState == null)
                return new CardListVisualState(new List<CardData>(), 0, 0);
            return _getVisualState(_cards);
        }
    }

    public void SetVisualStateFunc(Func<CardsComponent, CardListVisualState> getVisualState)
    {
        _getVisualState = getVisualState;
        RebuildButtons();
    }

    public CardSpriteView()
    {
        _buttonOverlay = new Control { MouseFilter = MouseFilterMode.Ignore };
        AddChild(_buttonOverlay);
    }

    public void SetCards(Entity<CardsComponent> cards, float cardWidth, float cardHeight, Action<CardData>? onCardClicked)
    {
        SpriteSystem ??= EntMan.System<SpriteSystem>();
        SpriteSystem.ForceUpdate(cards.Owner);
        _cards = cards.Comp;
        _cardWidth = cardWidth;
        _cardHeight = cardHeight;
        _onCardClicked = onCardClicked;
        RebuildButtons();
    }

    protected override void Resized()
    {
        base.Resized();
        _buttonOverlay.SetSize = Size;
        RebuildButtons();
    }

    private void RebuildButtons()
    {
        _buttonOverlay.RemoveAllChildren();

        var visualState = VisualState;
        var count = visualState.Count;
        if (count == 0)
            return;

        var center = new Vector2(Size.X / 2f, Size.Y / 2f);
        var stretchVec = Stretch switch
        {
            StretchMode.Fit => Vector2.Min(Size / SetSize, Vector2.One),
            StretchMode.Fill => Size / SetSize,
            _ => Vector2.One,
        };
        var stretch = MathF.Min(stretchVec.X, stretchVec.Y);
        var scale = (Scale * stretch).X;

        for (var i = 0; i < count; i++)
        {
            var card = visualState.CardList[visualState.Start + i];
            var (offset, rotation) = CardSystem.GetCardPosRot(i, count);

            var button = new CardHoverButton(rotation, _cardWidth * scale, _cardHeight * scale);
            button.OnPressed += _ => _onCardClicked?.Invoke(card);

            var screenPos = center + offset * scale;
            LayoutContainer.SetPosition(button, screenPos - new Vector2(button.SetSize.X / 2f, button.SetSize.Y / 2f));
            _buttonOverlay.AddChild(button);
        }
    }
}

public sealed class CardHoverButton : ContainerButton
{
    private bool _hovered;
    private readonly Angle _rotation;

    public CardHoverButton(Angle rotation, float width, float height)
    {
        _rotation = rotation;
        SetSize = new Vector2(width, height);
        MouseFilter = MouseFilterMode.Stop;
        OnMouseEntered += _ =>
        {
            _hovered = true;
            UpdateDraw();
        };
        OnMouseExited += _ =>
        {
            _hovered = false;
            UpdateDraw();
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (!_hovered)
            return;

        var center = new Vector2(PixelSizeBox.Width / 2f, PixelSizeBox.Height / 2f);
        var halfW = PixelSizeBox.Width / 2f;
        var halfH = PixelSizeBox.Height / 2f;

        Vector2 RotatePoint(Vector2 point)
        {
            var cos = (float)Math.Cos(_rotation.Theta);
            var sin = (float)Math.Sin(_rotation.Theta);
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;
            return new Vector2(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
        }

        var tl = RotatePoint(new Vector2(center.X - halfW, center.Y - halfH));
        var tr = RotatePoint(new Vector2(center.X + halfW, center.Y - halfH));
        var br = RotatePoint(new Vector2(center.X + halfW, center.Y + halfH));
        var bl = RotatePoint(new Vector2(center.X - halfW, center.Y + halfH));

        handle.DrawLine(tl, tr, Color.White);
        handle.DrawLine(tr, br, Color.White);
        handle.DrawLine(br, bl, Color.White);
        handle.DrawLine(bl, tl, Color.White);
    }
}
