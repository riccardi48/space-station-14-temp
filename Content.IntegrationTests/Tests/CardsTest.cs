using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Cards;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests;

public sealed class CardsTest : GameTest
{
    [SidedDependency(Side.Server)]
    private readonly SharedCardSystem _sCards = null!;

    [SidedDependency(Side.Server)]
    private readonly SharedStackSystem _sStacks = null!;

    private const string CardsProtoId = "cardDeck";

    /// <summary>
    /// Helper to spawn a card deck and verify it has the expected components.
    /// </summary>
    private (EntityUid uid, CardsComponent cards, StackComponent stack) SpawnDeck(EntityCoordinates coords)
    {
        var uid = SSpawnAtPosition(CardsProtoId, coords);

        var cards = SComp<CardsComponent>(uid);
        var stack = SComp<StackComponent>(uid);

        return (uid, cards, stack);
    }

    [Test]
    public async Task CardsShufflePreservesCount()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            var beforeCount = cards.Cards.Count;
            var stackBefore = stack.Count;

            _sCards.TryShuffleCards((uid, cards));

            Assert.Multiple(() =>
            {
                Assert.That(cards.Cards.Count, Is.EqualTo(beforeCount), "Card list count changed after shuffle.");
                Assert.That(stack.Count, Is.EqualTo(stackBefore), "StackCount changed after shuffle.");
            });
        });
    }

    [Test]
    public async Task CardsFlipPreservesCount()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            var beforeCount = cards.Cards.Count;
            var stackBefore = stack.Count;

            // Flip over
            _sCards.TryFlipCards((uid, cards));
            Assert.Multiple(() =>
            {
                Assert.That(cards.Cards.Count, Is.EqualTo(beforeCount), "Count changed after flipping.");
                Assert.That(stack.Count, Is.EqualTo(stackBefore), "Stack count changed after flipping.");
            });

            // Flip back
            _sCards.TryFlipCards((uid, cards));
            Assert.Multiple(() =>
            {
                Assert.That(cards.Cards.Count, Is.EqualTo(beforeCount), "Count changed after flipping back.");
                Assert.That(stack.Count, Is.EqualTo(stackBefore), "Stack count changed after flipping back.");
            });
        });
    }

    [Test]
    public async Task CardsFanPreservesCount()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            var beforeCount = cards.Cards.Count;
            var stackBefore = stack.Count;

            // Fan cards
            _sCards.TryFanCards((uid, cards));
            Assert.Multiple(() =>
            {
                Assert.That(cards.Cards.Count, Is.EqualTo(beforeCount), "Count changed after fanning.");
                Assert.That(stack.Count, Is.EqualTo(stackBefore), "Stack count changed after fanning.");
            });

            // Unfan cards
            _sCards.TryFanCards((uid, cards));
            Assert.Multiple(() =>
            {
                Assert.That(cards.Cards.Count, Is.EqualTo(beforeCount), "Count changed after unfanning.");
                Assert.That(stack.Count, Is.EqualTo(stackBefore), "Stack count changed after unfanning.");
            });
        });
    }

    [Test]
    public async Task CardsFlipTogglesFlippedFlag()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, _) = SpawnDeck(coords);
            Assert.That(cards.Flipped, Is.False, "Deck should start face down (unflipped).");

            _sCards.TryFlipCards((uid, cards));
            Assert.That(cards.Flipped, Is.True, "Deck should be marked flipped after flip.");

            _sCards.TryFlipCards((uid, cards));
            Assert.That(cards.Flipped, Is.False, "Deck should be marked unflipped after another flip.");
        });
    }

    [Test]
    public async Task CardsFanTogglesFannedFlag()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, _) = SpawnDeck(coords);
            Assert.That(cards.Fanned, Is.False, "Deck should start unfanned.");

            _sCards.TryFanCards((uid, cards));
            Assert.That(cards.Fanned, Is.True, "Deck should be marked fanned.");

            _sCards.TryFanCards((uid, cards));
            Assert.That(cards.Fanned, Is.False, "Deck should be marked unfanned again.");
        });
    }

    [Test]
    public async Task CardsVisualStateUnflippedShowsFirstCard()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (_, cards, _) = SpawnDeck(coords);
            Assert.That(cards.Flipped, Is.False);

            var state = _sCards.GetCardListVisualState(cards);

            Assert.Multiple(() =>
            {
                Assert.That(state.Start, Is.EqualTo(0), "Unflipped deck should start rendering at index 0.");
                Assert.That(state.Count, Is.EqualTo(1), "Unflipped, unfanned deck should show exactly 1 card.");
            });
        });
    }

    [Test]
    public async Task CardsVisualStateFlippedShowsLastCard()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, _) = SpawnDeck(coords);
            var total = cards.Cards.Count;
            _sCards.TryFlipCards((uid, cards));

            var state = _sCards.GetCardListVisualState(cards);

            Assert.Multiple(() =>
            {
                Assert.That(state.Start, Is.EqualTo(total - 1), "Flipped unfanned deck should start at the last card.");
                Assert.That(state.Count, Is.EqualTo(1), "Flipped unfanned deck should show exactly 1 card.");
            });
        });
    }

    [Test]
    public async Task CardsVisualStateFannedCapsAtMaxFanned()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, _) = SpawnDeck(coords);
            _sCards.TryFanCards((uid, cards));

            var state = _sCards.GetCardListVisualState(cards);
            var expectedVisible = Math.Min(cards.MaxFanned, cards.Cards.Count);

            Assert.Multiple(() =>
            {
                Assert.That(
                    state.Count,
                    Is.EqualTo(expectedVisible),
                    $"Fanned state count should be capped at {nameof(CardsComponent.MaxFanned)}."
                );
                Assert.That(state.Start, Is.GreaterThanOrEqualTo(0), "Start index must be non-negative.");
                Assert.That(
                    state.Start + state.Count,
                    Is.LessThanOrEqualTo(cards.Cards.Count),
                    "Indices out of range."
                );
            });
        });
    }

    [Test]
    public async Task CardsVisualStateFannedFlippedStartsCorrectly()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, _) = SpawnDeck(coords);
            _sCards.TryFanCards((uid, cards));

            var total = cards.Cards.Count;
            var expectedVisible = Math.Min(cards.MaxFanned, total);
            var state = _sCards.GetCardListVisualState(cards);

            Assert.Multiple(() =>
            {
                Assert.That(state.Start, Is.EqualTo(0), "Unflipped fanned deck should start at index 0.");
                Assert.That(state.Count, Is.EqualTo(expectedVisible));
                Assert.That(state.Start + state.Count, Is.LessThanOrEqualTo(total));
            });

            _sCards.TryFlipCards((uid, cards));
            state = _sCards.GetCardListVisualState(cards);

            Assert.Multiple(() =>
            {
                Assert.That(
                    state.Start,
                    Is.EqualTo(total - expectedVisible),
                    "Flipped fanned deck should start at (Total - VisibleCount)."
                );
                Assert.That(state.Count, Is.EqualTo(expectedVisible));
                Assert.That(state.Start + state.Count, Is.LessThanOrEqualTo(total));
            });
        });
    }

    [Test]
    public async Task CardsSplitAndMergePreservesTotal()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            var cardsBefore = cards.Cards.Count;
            var stackBefore = stack.Count;

            var splitResult = _sStacks.Split((uid, stack), 20, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(splitResult, Is.Not.Null, "Split operation returned null.");

            var splitUid = splitResult!.Value;
            var splitCards = SComp<CardsComponent>(splitUid);
            var splitStack = SComp<StackComponent>(splitUid);

            Assert.Multiple(() =>
            {
                Assert.That(
                    cards.Cards.Count + splitCards.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards across both halves must match original."
                );
                Assert.That(
                    stack.Count + splitStack.Count,
                    Is.EqualTo(stackBefore),
                    "Total stack counts must match original."
                );
            });

            // Merge them back together
            var merged = _sStacks.TryMergeStacks((splitUid, splitStack), (uid, stack), out _);
            Assert.That(merged, Is.True, "Merging the split stacks back failed.");

            Assert.Multiple(() =>
            {
                Assert.That(cards.Cards.Count, Is.EqualTo(cardsBefore), "Post-merge cards mismatch.");
                Assert.That(stack.Count, Is.EqualTo(stackBefore), "Post-merge stack count mismatch.");
            });
        });
    }

    [Test]
    public async Task CardsSplitAmountIsRespected()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            const int splitAmount = 13;

            var splitResult = _sStacks.Split((uid, stack), splitAmount, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(splitResult, Is.Not.Null, "Split operation returned null.");

            var splitUid = splitResult!.Value;
            var splitCards = SComp<CardsComponent>(splitUid);
            var splitStack = SComp<StackComponent>(splitUid);

            Assert.Multiple(() =>
            {
                Assert.That(
                    splitCards.Cards.Count,
                    Is.EqualTo(splitAmount),
                    "Split deck does not contain requested card count."
                );
                Assert.That(
                    splitStack.Count,
                    Is.EqualTo(splitAmount),
                    "Split stack does not match requested card count."
                );
            });
        });
    }

    [Test]
    public async Task CardsSplitPreservesFlippedState()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            _sCards.TryFlipCards((uid, cards));
            Assert.That(cards.Flipped, Is.True);

            var splitResult = _sStacks.Split((uid, stack), 10, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(splitResult, Is.Not.Null);

            var splitCards = SComp<CardsComponent>(splitResult.Value);
            Assert.That(splitCards.Flipped, Is.True, "Split deck should inherit Flipped state from parent.");
        });
    }

    [Test]
    public async Task CardsSplitPreservesFannedState()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            _sCards.TryFanCards((uid, cards));
            Assert.That(cards.Fanned, Is.True);

            var splitResult = _sStacks.Split((uid, stack), 10, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(splitResult, Is.Not.Null);

            var splitCards = SComp<CardsComponent>(splitResult.Value);
            Assert.That(splitCards.Fanned, Is.True, "Split deck should inherit Fanned state from parent.");
        });
    }

    [Test]
    public async Task CardsTryTakeCardReducesDeckByOne()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var player = SSpawnAtPosition("MobHuman", coords);
            var (uid, cards, stack) = SpawnDeck(coords);
            var cardsBefore = cards.Cards.Count;
            var stackBefore = stack.Count;

            var playerXform = SComp<TransformComponent>(player);

            // Take a specific card by index (e.g. index 20)
            var targetInx = cards.Cards[20].CardInx;
            var taken = _sCards.TryTakeCard((uid, cards), (player, playerXform), targetInx, out var split);

            Assert.Multiple(() =>
            {
                Assert.That(taken, Is.True, "TryTakeCard returned false for a valid card index.");
                Assert.That(split, Is.Not.Null, "Split entity should be produced upon taking card.");
            });

            var splitCards = SComp<CardsComponent>(split!.Value);
            var splitStack = SComp<StackComponent>(split.Value);

            Assert.Multiple(() =>
            {
                Assert.That(
                    cards.Cards.Count + splitCards.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards sum changed after card draw."
                );
                Assert.That(
                    stack.Count + splitStack.Count,
                    Is.EqualTo(stackBefore),
                    "Total stack size sum changed after card draw."
                );
                Assert.That(splitCards.Cards.Count, Is.EqualTo(1), "Taken card stack should have exactly 1 card.");
                Assert.That(splitCards.Cards[0].CardInx, Is.EqualTo(targetInx), "Drawn card is not the expected one.");
            });
        });
    }

    [Test]
    public async Task CardsTryTakeCardWithInvalidInxReturnsFalse()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var player = SSpawnAtPosition("MobHuman", coords);
            var (uid, cards, _) = SpawnDeck(coords);
            var cardsBefore = cards.Cards.Count;

            var playerXform = SComp<TransformComponent>(player);

            var result = _sCards.TryTakeCard((uid, cards), (player, playerXform), int.MaxValue, out var split);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "TryTakeCard with a non-existent index should fail.");
                Assert.That(split, Is.Null, "No split entity should be generated on failure.");
                Assert.That(
                    cards.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards leaked or modified during failed draw."
                );
            });
        });
    }

    [Test]
    public async Task CardsUnflippedSplitTakesFromFront()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            Assert.That(cards.Flipped, Is.False);

            var originalFirstCards = cards.Cards.Take(5).Select(c => c.CardInx).ToList();

            var splitResult = _sStacks.Split((uid, stack), 5, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(splitResult, Is.Not.Null);

            var splitCards = SComp<CardsComponent>(splitResult.Value);
            var splitInxes = splitCards.Cards.Select(c => c.CardInx).ToList();

            Assert.That(
                splitInxes,
                Is.EquivalentTo(originalFirstCards),
                "Unflipped splits must pick from the front of the list."
            );
        });
    }

    [Test]
    public async Task CardsFlippedSplitTakesFromBack()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            var originalLastCards = cards.Cards.TakeLast(5).Select(c => c.CardInx).ToList();

            _sCards.TryFlipCards((uid, cards));

            var splitResult = _sStacks.Split((uid, stack), 5, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(splitResult, Is.Not.Null);

            var splitCards = SComp<CardsComponent>(splitResult.Value);
            var splitInxes = splitCards.Cards.Select(c => c.CardInx).ToList();

            Assert.That(
                splitInxes,
                Is.EquivalentTo(originalLastCards),
                "Flipped splits must pick from the back of the list."
            );
        });
    }

    [Test]
    public async Task CardsMergeRecipientGainsCardsFromDonor()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uidA, cardsA, stackA) = SpawnDeck(coords);
            var splitA = _sStacks.Split((uidA, stackA), 40, new EntityCoordinates(uidA, Vector2.Zero));
            SQueueDel(splitA!.Value);

            var (uidB, cardsB, stackB) = SpawnDeck(coords);
            var splitB = _sStacks.Split((uidB, stackB), 40, new EntityCoordinates(uidB, Vector2.Zero));
            SQueueDel(splitB!.Value);

            var countA = cardsA.Cards.Count;
            var countB = cardsB.Cards.Count;

            // Merge B into A
            var ok = _sStacks.TryMergeStacks((uidB, stackB), (uidA, stackA), out var transferred);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True, "Merge operation failed.");
                Assert.That(transferred, Is.GreaterThan(0));
                Assert.That(
                    cardsA.Cards.Count,
                    Is.EqualTo(countA + transferred),
                    "Recipient didn't receive the donor cards."
                );
                Assert.That(cardsB.Cards.Count, Is.EqualTo(countB - transferred), "Donor didn't lose the cards.");
            });
        });
    }

    [Test]
    public async Task CardsSingleCardDeckDoesNotFan()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);

            // Keep only 1 card on the stack
            var leaveOne = stack.Count - 1;
            var splitResult = _sStacks.Split((uid, stack), leaveOne, new EntityCoordinates(uid, Vector2.Zero));

            Assert.Multiple(() =>
            {
                Assert.That(splitResult, Is.Not.Null);
                Assert.That(stack.Count, Is.EqualTo(1));
            });

            _sCards.TryFanCards((uid, cards));

            var state = _sCards.GetCardListVisualState(cards);
            Assert.That(
                state.Count,
                Is.EqualTo(1),
                "A single-card fanned deck visual state should still be exactly 1."
            );
        });
    }

    [Test]
    public async Task CardsInxesAreUniqueAfterSpawn()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (_, cards, _) = SpawnDeck(coords);
            var inxes = cards.Cards.Select(c => c.CardInx).ToList();

            Assert.That(inxes, Is.Unique, "Card indexes are not completely unique upon spawning.");
        });
    }

    [Test]
    public async Task CardsInxesRemainingAfterSplitAreStillUnique()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            var splitResult = _sStacks.Split((uid, stack), 13, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(splitResult, Is.Not.Null);

            var splitCards = SComp<CardsComponent>(splitResult.Value);
            var allInxes = cards.Cards.Select(c => c.CardInx).Concat(splitCards.Cards.Select(c => c.CardInx)).ToList();

            Assert.That(allInxes, Is.Unique, "Split leaves duplicates among both card sets.");
        });
    }

    [Test]
    public async Task CardsShuffleRetainsAllCardInxes()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, _) = SpawnDeck(coords);
            var before = cards.Cards.Select(c => c.CardInx).OrderBy(x => x).ToList();

            _sCards.TryShuffleCards((uid, cards));

            var after = cards.Cards.Select(c => c.CardInx).OrderBy(x => x).ToList();
            Assert.That(after, Is.EqualTo(before), "Shuffling changed or corrupted card indices.");
        });
    }
}
