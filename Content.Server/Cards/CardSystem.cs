using Content.Shared.Cards;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Cards;

/// <inheritdoc />
[UsedImplicitly]
public sealed partial class CardSystem : SharedCardSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CardDropMergeEvent>(HandleDropMerge);
    }

    private void HandleDropMerge(CardDropMergeEvent args)
    {
        var mergee = GetEntity(args.Mergee);
        var merger = GetEntity(args.Merger);
        if (
            TryComp<StackComponent>(mergee, out var donorStack)
            && TryComp<StackComponent>(merger, out var recipientStack)
            && Stacks.TryMergeStacks((mergee, donorStack), (merger, recipientStack), out var transferred)
        )
        {
            return;
        }
    }

    protected override void PlayCardAnimation(
        Entity<CardsComponent> merger,
        Entity<CardsComponent> mergee,
        List<CardData> selected,
        bool playOnUser = false
    )
    {
        var ev = new CardAnimationEvent(GetNetEntity(merger.Owner), GetNetEntity(mergee.Owner), selected);
        var filter = Filter
            .Pvs(merger.Owner)
            .RemoveWhereAttachedEntity(e =>
                (e == Transform(merger.Owner).ParentUid || e == Transform(mergee.Owner).ParentUid) && !playOnUser
            );
        RaiseNetworkEvent(ev, filter);
    }
}
