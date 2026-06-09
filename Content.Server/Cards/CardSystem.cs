using Content.Shared.Cards;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Cards;

/// <inheritdoc />
[UsedImplicitly]
public sealed partial class CardSystem : SharedCardSystem
{
    protected override void PlayCardAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        List<CardData> selected
    )
    {
        var ev = new CardAnimationEvent(GetNetEntity(merger.Owner), GetNetEntity(mergee.Owner), selected);
        var filter = Filter
            .Pvs(merger.Owner)
            .RemoveWhereAttachedEntity(e =>
                e == Transform(merger.Owner).ParentUid || e == Transform(mergee.Owner).ParentUid
            );
        RaiseNetworkEvent(ev, filter);
    }
}
