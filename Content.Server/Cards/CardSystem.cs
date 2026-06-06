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
        List<ProtoId<CardPrototype>> selected
    )
    {
        var ev = new CardAnimationEvent(GetNetEntity(merger.Owner), GetNetEntity(mergee.Owner), selected);
        var filter = Filter.Pvs(merger.Owner);
        RaiseNetworkEvent(ev, filter);
    }
}
