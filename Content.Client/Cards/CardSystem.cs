using System.Linq;
using Content.Shared.Cards;
using Content.Shared.Stacks;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Client.Cards;

/// <inheritdoc />
[UsedImplicitly]
public sealed partial class CardSystem : SharedCardSystem
{
    [Dependency]
    private SharedStorageSystem _storage = default!;

    [Dependency]
    private SharedStackSystem _stack = default!;

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
        List<int> selected = new List<int> { mergee.Comp.Cards[cardInx] };
        PlayCardAnimation(merger.Owner, mergee.Owner, selected);
    }

    private void PlayCardAnimation(EntityUid merger, EntityUid mergee, List<int> selected)
    {
        var mergeeCoords = Transform(mergee).Coordinates;
        var mergerCoords = Transform(merger).Coordinates;
        var localRotation = Transform(mergee).LocalRotation;
        var ent = SpawnTempClone(mergerCoords, selected);
        _storage.PlayPickupAnimation(ent, mergeeCoords, mergerCoords, localRotation);
        PredictedQueueDel(ent);
    }

    private EntityUid SpawnTempClone(EntityCoordinates mergerCoords, List<int> selected)
    {
        var ent = Spawn("BaseCards", mergerCoords);
        if (!TryComp<CardsComponent>(ent, out var cardsComp) || !TryComp<StackComponent>(ent, out var stackComp))
            return EntityUid.Invalid;
        cardsComp.Cards = selected;
        Stacks.SetCount((ent, stackComp), cardsComp.Cards.Count);
        return ent;
    }
}
