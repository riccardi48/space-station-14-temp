using System.Linq;
using System.Numerics;
using Content.Client.Gameplay;
using Content.Shared.Cards;
using Content.Shared.Interaction.Events;
using Content.Shared.Stacks;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

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

    [Dependency]
    private IStateManager _stateManager = default!;

    [Dependency]
    private IEyeManager _eyeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeNetworkEvent<CardAnimationEvent>(HandleCardAnimation);
        SubscribeLocalEvent<CardsComponent, DroppedEvent>(OnCardsDropped);
    }

    private void OnCardsDropped(Entity<CardsComponent> ent, ref DroppedEvent args)
    {
        if (args.User != EntityUid.Invalid)
            return;
        var currentState = _stateManager.CurrentState;
        if (currentState is not GameplayStateBase screen)
            return;
        var uid = screen
            .GetClickableEntities(Transform(ent).Coordinates)
            .FirstOrDefault(e => e != ent.Owner && TryComp<CardsComponent>(e, out _));
        if (
            uid != ent.Owner
            && TryComp<CardsComponent>(uid, out var cardComp)
            && TryComp<StackComponent>(ent.Owner, out var donorStack)
            && TryComp<StackComponent>(uid, out var recipientStack)
            && Stacks.TryMergeStacks((ent.Owner, donorStack), (uid, recipientStack), out _)
        )
        {
            if (Timing.IsFirstTimePredicted)
                RaisePredictiveEvent(new CardDropMergeEvent(GetNetEntity(uid), GetNetEntity(ent.Owner)));
        }
    }

    private void HandleCardAnimation(CardAnimationEvent args)
    {
        PlayCardAnimation(
            GetCoordinates(args.MergerCoords),
            args.MergeeFlipped,
            GetCoordinates(args.MergeeCoords),
            args.MergeeRotation,
            args.StackId,
            args.Selected
        );
    }

    protected override void PlayCardAnimation(
        EntityCoordinates mergerCoords,
        bool mergeeFlipped,
        EntityCoordinates mergeeCoords,
        Angle mergeeRotation,
        ProtoId<StackPrototype> newStackId,
        List<CardData> selected,
        bool playOnUser = false
    )
    {
        if (!Timing.IsFirstTimePredicted)
            return;
        Log.Info("A");
        var ent = SpawnTempClone(mergeeFlipped, mergeeCoords, newStackId, selected);
        if (ent == EntityUid.Invalid)
            return;
        Log.Info("B");
        _storage.PlayPickupAnimation(ent, mergeeCoords, mergerCoords, mergeeRotation);
        QueueDel(ent);
    }

    private EntityUid SpawnTempClone(
        bool mergeeFlipped,
        EntityCoordinates mergeeCoords,
        ProtoId<StackPrototype> newStackId,
        List<CardData> selected
    )
    {
        if (!_prototypeManager.TryIndex(newStackId, out var newStack))
            return EntityUid.Invalid;

        var ent = Spawn(newStack.Spawn, mergeeCoords);

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
        cardsComp.Flipped = mergeeFlipped;
        Stacks.SetCount((ent, stackComp), cardsComp.Cards.Count);
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetDrawDepth((ent, sprite), (int)DrawDepth.BelowMobs);
        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            Appearance.SetData(ent, CardVisuals.CardList, GetCardListVisualState(cardsComp), appearance);
            Appearance.SetData(ent, CardVisuals.IsFlipped, cardsComp.Flipped, appearance);

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
        new((float)Math.Sin(angle) * radius, (float)Math.Cos(angle) * radius - radius * (3f / 4f));

    private static float FanRadius(int count) => count <= 1 ? 0f : (float)Math.Sqrt(count / 20f);

    private static (string Base, string LayerOne, string LayerTwo) CardLayers(int i) =>
        ($"card_{i}_base", $"card_{i}_layerOne", $"card_{i}_layerTwo");

    private void OnAppearanceChanged(EntityUid uid, CardsComponent component, ref AppearanceChangeEvent args)
    {
        Appearance.TryGetData<bool>(uid, CardVisuals.IsFlipped, out var flipped, args.Component);

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

            if (flipped)
            {
                BuildCard(prototype, baseLayer, card.BaseState, layerOne, layerTwo, (uid, sprite));
                TransformLayer(layerOne, position, rotation, (uid, sprite));
                TransformLayer(layerTwo, position, rotation, (uid, sprite));
            }
            else
                BuildLayer(baseLayer, card.CardBack, null, (uid, sprite));

            TransformLayer(baseLayer, position, rotation, (uid, sprite));

            if (i == 0)
                TransformLayer("base", position, rotation, (uid, sprite));
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
        _sprite.LayerSetVisible(sprite, layer, true);
        _sprite.LayerSetRsiState(sprite, layer, layerState);
        if (layerColor != null)
            _sprite.LayerSetColor(sprite, layer, layerColor.Value);
    }

    public void TransformLayer(string layer, Vector2 movement, Angle rotation, Entity<SpriteComponent?> sprite)
    {
        _sprite.LayerSetOffset(sprite, layer, movement);
        _sprite.LayerSetRotation(sprite, layer, rotation);
    }
}
