using Content.Shared.Cards;
using JetBrains.Annotations;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Map;
using System.Linq;
using Content.Shared.Stacks;

namespace Content.Client.Cards;

/// <inheritdoc />
[UsedImplicitly]
public sealed partial class CardSystem : SharedCardSystem
{
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    protected override void PlayCardDrawAnimation(Entity<CardsComponent> merger, Entity<CardsComponent> mergee, int delta)
    {
        var mergeeCoords = Transform(mergee.Owner).Coordinates;
        var mergerCoords = Transform(merger.Owner).Coordinates;
        var localRotation = Transform(mergee.Owner).LocalRotation;

        var ent = SpawnTempClone(mergee, mergerCoords, delta);
        _storage.PlayPickupAnimation(ent, mergeeCoords, mergerCoords, localRotation);
        QueueDel(ent);
    }

    private EntityUid SpawnTempClone(Entity<CardsComponent> mergee, EntityCoordinates mergerCoords, int delta)
    {
        var ent = Spawn("BaseCards", mergerCoords);
        if (!TryComp<CardsComponent>(ent, out var cardsComp) || !TryComp<StackComponent>(ent, out var stackComp))
            return EntityUid.Invalid;
        cardsComp.Cards = MovedCards(mergee.Comp, delta);
        _stack.SetCount((ent, stackComp), cardsComp.Cards.Count);
        return ent;
    }
}
