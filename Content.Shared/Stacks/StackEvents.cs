namespace Content.Shared.Stacks;

[ByRefEvent]
public readonly record struct MergeEvent(EntityUid Merger, EntityUid Mergee, int Delta);

[ByRefEvent]
public readonly record struct MergeeEvent(EntityUid Merger, EntityUid Mergee, int Delta);
