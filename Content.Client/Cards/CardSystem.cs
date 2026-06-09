using System.Linq;
using System.Numerics;
using Content.Shared.Cards;
using Content.Shared.Stacks;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Cards;

/// <inheritdoc />
[UsedImplicitly]
public sealed partial class CardSystem : SharedCardSystem
{
    [Dependency]
    private SharedStorageSystem _storage = default!;

    [Dependency]
    private SharedStackSystem _stack = default!;

    [Dependency]
    private SpriteSystem _sprite = default!;

    [Dependency]
    private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeNetworkEvent<CardAnimationEvent>(HandleCardAnimation);
    }

    private void HandleCardAnimation(CardAnimationEvent args)
    {
        var merger = GetEntity(args.Merger);
        var mergee = GetEntity(args.Mergee);
        if (
            !TryComp<CardsComponent>(merger, out var mergerComp) || !TryComp<CardsComponent>(mergee, out var mergeeComp)
        )
            return;
        PlayCardAnimation((merger, mergerComp), (mergee, mergeeComp), args.Selected);
    }

    protected override void PlayCardAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        List<CardData> selected
    )
    {
        if (_timing.ApplyingState)
            return;
        var mergeeCoords = Transform(mergee.Owner).Coordinates;
        var mergerCoords = Transform(merger.Owner).Coordinates;
        var localRotation = Transform(mergee.Owner).LocalRotation;
        var ent = SpawnTempClone(mergee, mergeeCoords, selected);
        if (ent == EntityUid.Invalid)
            return;
        _storage.PlayPickupAnimation(ent, mergeeCoords, mergerCoords, localRotation);
        QueueDel(ent);
    }

    private EntityUid SpawnTempClone(
        Entity<CardsComponent> mergee,
        EntityCoordinates mergeeCoords,
        List<CardData> selected
    )
    {
        var ent = Spawn("BaseCards", mergeeCoords);
        if (
            !TryComp<CardsComponent>(ent, out var cardsComp)
            || !TryComp<StackComponent>(ent, out var stackComp)
            || !TryComp(ent, out SpriteComponent? spriteComp)
        )
        {
            QueueDel(ent);
            return EntityUid.Invalid;
        }
        cardsComp.Cards = selected;
        cardsComp.Flipped = mergee.Comp.Flipped;
        Stacks.SetCount((ent, stackComp), cardsComp.Cards.Count);
        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(cardsComp), appearance);
            Appearance.SetData(ent, CardVisuals.IsFlipped, cardsComp.Flipped, appearance);
            Appearance.SetData(ent, CardVisuals.IsFanned, cardsComp.Fanned, appearance);

            var ev = new AppearanceChangeEvent
            {
                Component = appearance,
                AppearanceData = new Dictionary<Enum, object>(),
                Sprite = spriteComp,
            };
            RaiseLocalEvent(ent, ref ev);
        }
        _sprite.SetVisible((ent, spriteComp), false);
        return ent;
    }

    private static Vector2 FanPosition(double angle, float radius) =>
        new((float)Math.Sin(angle) * radius, (float)Math.Cos(angle) * radius - radius * (3 / 4f));

    private static float FanRadius(int count) => count <= 1 ? 0f : (float)Math.Sqrt(count / 20f);

    private static (string Base, string LayerOne, string LayerTwo) CardLayers(int i) =>
        ($"card_{i}_base", $"card_{i}_layerOne", $"card_{i}_layerTwo");

    private void PlaceCard(
        int i,
        CardPrototype prototype,
        string baseSprite,
        Entity<SpriteComponent?> sprite,
        Vector2 offset,
        Angle rotation
    ) { }

    private void OnAppearanceChanged(EntityUid uid, CardsComponent component, ref AppearanceChangeEvent args)
    {
        Appearance.TryGetData<bool>(uid, CardVisuals.IsFlipped, out var flipped, args.Component);
        Appearance.TryGetData<bool>(uid, CardVisuals.IsFanned, out var fanned, args.Component);

        if (!Appearance.TryGetData<CardListVisualState>(uid, CardVisuals.CardList, out var visualState, args.Component))
            visualState = new CardListVisualState(new List<CardData>());

        if (!TryComp<SpriteComponent>(uid, out var sprite) || !TryComp<CardsComponent>(uid, out var cards))
            return;

        for (var i = 0; i < cards.MaxFanned; i++)
        {
            var (baseLayer, layerOne, layerTwo) = CardLayers(i);
            if (!_sprite.LayerExists((uid, sprite), baseLayer))
                break;
            _sprite.RemoveLayer((uid, sprite), baseLayer, false);
            _sprite.RemoveLayer((uid, sprite), layerOne, false);
            _sprite.RemoveLayer((uid, sprite), layerTwo, false);
        }


        var count = visualState.CardList.Count;
        var radius = FanRadius(count);
        for (var i = 0; i < count; i++)
        {
            var card = visualState.CardList[i];
            var (baseLayer, layerOne, layerTwo) = CardLayers(i);
            if (!_prototypeManager.TryIndex<CardPrototype>(card.CardId, out var prototype))
                continue;
            var angle = (i - count / 2.0 + 0.5) / count * Math.PI;
            var position = FanPosition(angle, radius);
            var rotation = new Angle(-angle);

            _sprite.LayerMapReserve((uid, sprite), baseLayer);
            _sprite.LayerMapReserve((uid, sprite), layerOne);
            _sprite.LayerMapReserve((uid, sprite), layerTwo);
            if (!flipped)
            {
                BuildLayer(baseLayer, card.CardBack, null, (uid, sprite));
                TransformLayer(baseLayer, position, rotation, (uid, sprite));
                _sprite.LayerSetVisible((uid, sprite), layerOne, false);
                _sprite.LayerSetVisible((uid, sprite), layerTwo, false);
            }
            else
            {
                BuildCard(prototype, baseLayer, card.BaseState, layerOne, layerTwo, (uid, sprite));
                TransformLayer(baseLayer, position, rotation, (uid, sprite));
                TransformLayer(layerOne, position, rotation, (uid, sprite));
                TransformLayer(layerTwo, position, rotation, (uid, sprite));
            }
            if (i == 0)
            {
                TransformLayer("base", position, rotation, (uid, sprite));
            }
        }
    }

    public void BuildCard(
        CardPrototype prototype,
        string baseLayer,
        string baseSprite,
        string layerOne,
        string layerTwo,
        Entity<SpriteComponent?> sprite
    )
    {
        BuildLayer(baseLayer, baseSprite, null, sprite);

        BuildLayer(layerOne, prototype.LayerOneState, prototype.LayerOneColor, sprite);

        BuildLayer(layerTwo, prototype.LayerTwoState, prototype.LayerTwoColor, sprite);
    }

    public void BuildLayer(string layer, string? layerState, Color? layerColor, Entity<SpriteComponent?> sprite)
    {
        if (layer != null)
        {
            _sprite.LayerSetVisible(sprite, layer, true);
            _sprite.LayerSetRsiState(sprite, layer, layerState);
            if (layerColor != null)
                _sprite.LayerSetColor(sprite, layer, layerColor.Value);
        }
    }

    public void TransformLayer(string layer, Vector2 movement, Angle rotation, Entity<SpriteComponent?> sprite)
    {
        _sprite.LayerSetOffset(sprite, layer, movement);
        _sprite.LayerSetRotation(sprite, layer, rotation);
    }
}
