using Content.Shared.Cards;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Map;
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
        )
            Stacks.TryMergeStacks((mergee, donorStack), (merger, recipientStack), out _);
    }

    protected override void PlayCardAnimation(
        EntityCoordinates mergerCoords,
        bool mergeeFlipped,
        EntityCoordinates mergeeCoords,
        Angle mergeeRotation,
        ProtoId<StackPrototype> stackId,
        List<CardData> selected,
        bool playOnUser = false
    )
    {
        var ev = new CardAnimationEvent(
            GetNetCoordinates(mergerCoords),
            mergeeFlipped,
            GetNetCoordinates(mergeeCoords),
            mergeeRotation,
            stackId,
            selected
        );
        var filter = Filter
            .Pvs(mergerCoords)
            .RemoveWhereAttachedEntity(e => !playOnUser && (e == mergerCoords.EntityId || e == mergeeCoords.EntityId));
        RaiseNetworkEvent(ev, filter);
    }
}
