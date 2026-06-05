using System.Linq;
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

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(SharedStackSystem));
        SubscribeLocalEvent<CardsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    protected override void PlayCardDrawAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int delta
    )
    {
        var selected = MovedCards(mergee.Comp, delta);
        PlayCardAnimation(merger.Owner, mergee.Owner, selected);
    }

    protected override void PlayCardTakeAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        int cardInx
    )
    {
        Log.Info($"{cardInx} {mergee.Comp.Cards.Count}");
        List<ProtoId<CardPrototype>> selected = new List<ProtoId<CardPrototype>> { mergee.Comp.Cards[cardInx] };
        PlayCardAnimation(merger.Owner, mergee.Owner, selected);
    }

    private void PlayCardAnimation(EntityUid merger, EntityUid mergee, List<ProtoId<CardPrototype>> selected)
    {
        var mergeeCoords = Transform(mergee).Coordinates;
        var mergerCoords = Transform(merger).Coordinates;
        var localRotation = Transform(mergee).LocalRotation;
        var ent = SpawnTempClone(mergerCoords, selected);
        if (ent == EntityUid.Invalid)
            return;
        _storage.PlayPickupAnimation(ent, mergeeCoords, mergerCoords, localRotation);
        PredictedQueueDel(ent);
    }

    private EntityUid SpawnTempClone(EntityCoordinates mergerCoords, List<ProtoId<CardPrototype>> selected)
    {
        var ent = Spawn("BaseCards", mergerCoords);
        if (!TryComp<CardsComponent>(ent, out var cardsComp) || !TryComp<StackComponent>(ent, out var stackComp))
        {
            PredictedQueueDel(ent);
            return EntityUid.Invalid;
        }

        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            Appearance.SetData(ent, CardVisuals.CardList, new CardListVisualState(cardsComp.Cards), appearance);
            Appearance.SetData(ent, CardVisuals.IsFlipped, cardsComp.Flipped, appearance);
            Appearance.SetData(ent, CardVisuals.IsFanned, cardsComp.Fanned, appearance);
        }
        cardsComp.Cards = selected;
        Stacks.SetCount((ent, stackComp), cardsComp.Cards.Count);
        return ent;
    }

    private void OnAppearanceChanged(EntityUid uid, CardsComponent component, ref AppearanceChangeEvent args)
    {
        if (!Appearance.TryGetData<bool>(uid, CardVisuals.IsFlipped, out var flipped, args.Component))
            flipped = false;

        if (!flipped)
            return;

        if (!Appearance.TryGetData<bool>(uid, CardVisuals.IsFanned, out var fanned, args.Component))
            fanned = false;

        if (!Appearance.TryGetData<CardListVisualState>(uid, CardVisuals.CardList, out var visualState, args.Component))
            visualState = new CardListVisualState(new List<ProtoId<CardPrototype>>([]));

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        List<ProtoId<CardPrototype>> cardList = visualState.CardList;
        int cardCount = cardList.Count;
    }
}
