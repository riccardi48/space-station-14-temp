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
        List<ProtoId<CardPrototype>> selected
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
        List<ProtoId<CardPrototype>> selected
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
    )
    {
        var (baseLayer, layerOne, layerTwo) = CardLayers(i);
        _sprite.LayerMapReserve(sprite, baseLayer);
        _sprite.LayerMapReserve(sprite, layerOne);
        _sprite.LayerMapReserve(sprite, layerTwo);
        TransformCard(baseLayer, layerOne, layerTwo, offset, rotation, sprite);
        BuildCard(prototype, baseLayer, baseSprite, layerOne, layerTwo, sprite);
    }

    private void OnAppearanceChanged(EntityUid uid, CardsComponent component, ref AppearanceChangeEvent args)
    {
        Appearance.TryGetData<bool>(uid, CardVisuals.IsFlipped, out var flipped, args.Component);
        Appearance.TryGetData<bool>(uid, CardVisuals.IsFanned, out var fanned, args.Component);

        if (!Appearance.TryGetData<CardListVisualState>(uid, CardVisuals.CardList, out var visualState, args.Component))
            visualState = new CardListVisualState(new List<ProtoId<CardPrototype>>());

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

        if (!flipped)
            return;

        Appearance.SetData(uid, StackVisuals.Hide, false, args.Component);
        _sprite.LayerSetVisible((uid, sprite), "base", false);

        if (fanned)
        {
            var count = visualState.CardList.Count;
            var radius = FanRadius(count);
            for (var i = 0; i < count; i++)
            {
                if (!_prototypeManager.TryIndex<CardPrototype>(visualState.CardList[i].Id, out var prototype))
                    continue;
                var angle = (i - count / 2.0 + 0.5) / count * Math.PI;
                PlaceCard(
                    i,
                    prototype,
                    component.BaseState,
                    (uid, sprite),
                    FanPosition(angle, radius),
                    new Angle(-angle)
                );
            }
            return;
        }
        var id = visualState.CardList.FirstOrDefault().Id;
        if (id == null || !_prototypeManager.TryIndex<CardPrototype>(id, out var topCard))
            return;
        PlaceCard(0, topCard, component.BaseState, (uid, sprite), Vector2.Zero, Angle.Zero);
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
        _sprite.LayerSetVisible(sprite, baseLayer, true);
        _sprite.LayerSetRsiState(sprite, baseLayer, baseSprite);

        if (prototype.LayerOneState != null)
        {
            _sprite.LayerSetVisible(sprite, layerOne, true);
            _sprite.LayerSetRsiState(sprite, layerOne, prototype.LayerOneState);
            if (prototype.LayerOneColor != null)
                _sprite.LayerSetColor(sprite, layerOne, prototype.LayerOneColor.Value);
        }

        if (prototype.LayerTwoState != null)
        {
            _sprite.LayerSetVisible(sprite, layerTwo, true);
            _sprite.LayerSetRsiState(sprite, layerTwo, prototype.LayerTwoState);
            if (prototype.LayerTwoColor != null)
                _sprite.LayerSetColor(sprite, layerTwo, prototype.LayerTwoColor.Value);
        }
    }

    public void TransformCard(
        string baseLayer,
        string layerOne,
        string layerTwo,
        Vector2 movement,
        Angle rotation,
        Entity<SpriteComponent?> sprite
    )
    {
        _sprite.LayerSetOffset(sprite, baseLayer, movement);
        _sprite.LayerSetRotation(sprite, baseLayer, rotation);

        _sprite.LayerSetOffset(sprite, layerOne, movement);
        _sprite.LayerSetRotation(sprite, layerOne, rotation);

        _sprite.LayerSetOffset(sprite, layerTwo, movement);
        _sprite.LayerSetRotation(sprite, layerTwo, rotation);
    }
}
