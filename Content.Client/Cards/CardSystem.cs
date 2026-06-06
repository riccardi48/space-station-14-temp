using System.Linq;
using System.Numerics;
using Content.Client.Salvage.UI;
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

        // UpdatesBefore.Add(typeof(SharedStackSystem));
        SubscribeLocalEvent<CardsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    protected override void PlayCardDrawAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int delta
    )
    {
        var selected = MovedCards(mergee.Comp, delta);
        PlayCardAnimation(merger, mergee, selected);
    }

    protected override void PlayCardTakeAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int cardInx
    )
    {
        Log.Info($"{cardInx} {mergee.Comp.Cards.Count}");
        List<ProtoId<CardPrototype>> selected = new List<ProtoId<CardPrototype>> { mergee.Comp.Cards[cardInx] };
        PlayCardAnimation(merger, mergee, selected);
    }

    private void PlayCardAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        List<ProtoId<CardPrototype>> selected
    )
    {
        var mergeeCoords = Transform(mergee.Owner).Coordinates;
        var mergerCoords = Transform(merger.Owner).Coordinates;
        var localRotation = Transform(mergee.Owner).LocalRotation;
        var ent = SpawnTempClone(mergee, mergerCoords, selected);
        if (ent == EntityUid.Invalid)
            return;
        _storage.PlayPickupAnimation(ent, mergeeCoords, mergerCoords, localRotation);
        PredictedQueueDel(ent);
    }

    private EntityUid SpawnTempClone(
        Entity<CardsComponent> mergee,
        EntityCoordinates mergerCoords,
        List<ProtoId<CardPrototype>> selected
    )
    {
        var ent = Spawn("BaseCards", mergerCoords);
        if (
            !TryComp<CardsComponent>(ent, out var cardsComp)
            || !TryComp<StackComponent>(ent, out var stackComp)
            || !TryComp(ent, out SpriteComponent? spriteComp)
        )
        {
            PredictedQueueDel(ent);
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
        }
        _sprite.SetVisible((ent, spriteComp), false);
        return ent;
    }

    private void OnAppearanceChanged(EntityUid uid, CardsComponent component, ref AppearanceChangeEvent args)
    {
        if (!Appearance.TryGetData<bool>(uid, CardVisuals.IsFlipped, out var flipped, args.Component))
            flipped = false;

        if (!Appearance.TryGetData<CardListVisualState>(uid, CardVisuals.CardList, out var visualState, args.Component))
            visualState = new CardListVisualState(new List<ProtoId<CardPrototype>>());

        if (!Appearance.TryGetData<bool>(uid, CardVisuals.IsFanned, out var fanned, args.Component))
            fanned = false;

        if (
            !TryComp<SpriteComponent>(uid, out var sprite)
            || !TryComp<StackComponent>(uid, out var stack)
            || !_prototypeManager.TryIndex(stack.StackTypeId, out var stackPrototype)
        )
            return;

        for (var i = 0; i < stackPrototype.MaxCount * 3; i++)
        {
            if (!_sprite.LayerExists((uid, sprite), $"card_{i}"))
                break;
            _sprite.LayerSetVisible((uid, sprite), $"card_{i}", false);
        }

        if (!flipped)
            return;

        Appearance.SetData(uid, StackVisuals.Hide, false, args.Component);

        _sprite.LayerSetVisible((uid, sprite), "base", false);
        CardPrototype? prototype;
        if (fanned)
        {
            var count = visualState.CardList.Count;
            for (var i = 0; i < count; i++)
            {
                if (!_prototypeManager.TryIndex<CardPrototype>(visualState.CardList[i].Id, out prototype))
                    continue;
                _sprite.LayerMapReserve((uid, sprite), $"card_{i * 3}");
                _sprite.LayerMapReserve((uid, sprite), $"card_{i * 3 + 1}");
                _sprite.LayerMapReserve((uid, sprite), $"card_{i * 3 + 2}");
                var angle = (i - count / 2.0 + 0.5) / count * Math.PI;
                var radius = (float)Math.Sqrt(count / 20f);
                if (count == 1)
                    radius = 0;
                TransformCard(
                    $"card_{i * 3}",
                    $"card_{i * 3 + 1}",
                    $"card_{i * 3 + 2}",
                    new Vector2(
                        (float)Math.Sin(angle) * radius,
                        (float)Math.Cos(angle) * radius - (radius * (3f / 4f))
                    ),
                    new Angle(-angle),
                    (uid, sprite)
                );
                BuildCard(
                    prototype,
                    $"card_{i * 3}",
                    component.BaseState,
                    $"card_{i * 3 + 1}",
                    $"card_{i * 3 + 2}",
                    (uid, sprite)
                );
            }
            return;
        }
        var id = visualState.CardList.FirstOrDefault().Id;
        if (id == null || !_prototypeManager.TryIndex<CardPrototype>(id, out prototype))
            return;
        _sprite.LayerMapReserve((uid, sprite), "card_0");
        _sprite.LayerMapReserve((uid, sprite), "card_1");
        _sprite.LayerMapReserve((uid, sprite), "card_2");
        TransformCard("card_0", "card_1", "card_2", new Vector2(0, 0), new Angle(0), (uid, sprite));
        BuildCard(prototype, "card_0", component.BaseState, "card_1", "card_2", (uid, sprite));
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
