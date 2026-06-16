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

    private (EntityUid uid, CardsComponent cards, StackComponent stack) SpawnDeck(EntityCoordinates coords)
    {
        var uid = SSpawnAtPosition(CardsProtoId, coords);
        if (!SEntMan.TryGetComponent<CardsComponent>(uid, out var cards))
            Assert.Fail($"Spawned {CardsProtoId} is missing {nameof(CardsComponent)}");
        if (!SEntMan.TryGetComponent<StackComponent>(uid, out var stack))
            Assert.Fail($"Spawned {CardsProtoId} is missing {nameof(StackComponent)}");
        return (uid, cards!, stack!);
    }

    [Test]
    public async Task CardsShufflePreservesCount()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            var (uid, cards, stack) = SpawnDeck(coords);
            var before = cards.Cards.Count;
            var stackBefore = stack.Count;

            _sCards.TryShuffleCards((uid, cards));

            Assert.That(cards.Cards.Count, Is.EqualTo(before), "Card list count changed after shuffle");
            Assert.That(stack.Count, Is.EqualTo(stackBefore), $"{nameof(StackComponent.Count)} changed after shuffle");
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
            var before = cards.Cards.Count;
            var stackBefore = stack.Count;

            _sCards.TryFlipCards((uid, cards));

            Assert.That(cards.Cards.Count, Is.EqualTo(before));
            Assert.That(stack.Count, Is.EqualTo(stackBefore));

            // Flip back — still same count
            _sCards.TryFlipCards((uid, cards));

            Assert.That(cards.Cards.Count, Is.EqualTo(before));
            Assert.That(stack.Count, Is.EqualTo(stackBefore));
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
            var before = cards.Cards.Count;
            var stackBefore = stack.Count;

            _sCards.TryFanCards((uid, cards));

            Assert.That(cards.Cards.Count, Is.EqualTo(before));
            Assert.That(stack.Count, Is.EqualTo(stackBefore));

            // Unfan — still the same
            _sCards.TryFanCards((uid, cards));

            Assert.That(cards.Cards.Count, Is.EqualTo(before));
            Assert.That(stack.Count, Is.EqualTo(stackBefore));
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
            Assert.That(cards.Flipped, Is.False, "Deck should start un-flipped");

            _sCards.TryFlipCards((uid, cards));
            Assert.That(cards.Flipped, Is.True);

            _sCards.TryFlipCards((uid, cards));
            Assert.That(cards.Flipped, Is.False);
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
            Assert.That(cards.Fanned, Is.False);

            _sCards.TryFanCards((uid, cards));
            Assert.That(cards.Fanned, Is.True);

            _sCards.TryFanCards((uid, cards));
            Assert.That(cards.Fanned, Is.False);
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

            Assert.That(state.Start, Is.EqualTo(0), "Unflipped deck should start at index 0");
            Assert.That(state.Count, Is.EqualTo(1), "Unflipped un-fanned deck should show exactly 1 card");
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

            Assert.That(state.Start, Is.EqualTo(total - 1), "Flipped un-fanned deck should start at last card");
            Assert.That(state.Count, Is.EqualTo(1));
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
            var expected = Math.Min(cards.MaxFanned, cards.Cards.Count);

            Assert.That(
                state.Count,
                Is.EqualTo(expected),
                $"Fanned state.Count should be capped at {nameof(CardsComponent.MaxFanned)}"
            );
            // Start should not go negative or out of range
            Assert.That(state.Start, Is.GreaterThanOrEqualTo(0));
            Assert.That(state.Start + state.Count, Is.LessThanOrEqualTo(cards.Cards.Count));
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
            var state = _sCards.GetCardListVisualState(cards);
            var expected = Math.Min(cards.MaxFanned, total);

            Assert.That(state.Start, Is.EqualTo(0), "Flipped fanned deck should start at Cards.Count - visibleCount");
            Assert.That(state.Count, Is.EqualTo(expected));
            Assert.That(state.Start + state.Count, Is.LessThanOrEqualTo(total));

            _sCards.TryFlipCards((uid, cards));

            state = _sCards.GetCardListVisualState(cards);

            // Flipped+fanned: start = Cards.Count - count
            Assert.That(
                state.Start,
                Is.EqualTo(total - expected),
                "Flipped fanned deck should start at Cards.Count - visibleCount"
            );
            Assert.That(state.Count, Is.EqualTo(expected));
            Assert.That(state.Start + state.Count, Is.LessThanOrEqualTo(total));
        });
    }

    [Test]
    public async Task CardsSplitAndMergePreservesTotal()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                var (uid, cards, stack) = SpawnDeck(coords);
                var cardsBefore = cards.Cards.Count;
                var stackBefore = stack.Count;

                var split = _sStacks.Split((uid, stack), 20, new EntityCoordinates(uid, Vector2.Zero));
                Assert.That(split, Is.Not.Null, "Split returned null");

                if (!SEntMan.TryGetComponent<CardsComponent>(split!.Value, out var splitCards))
                    Assert.Fail($"Split entity missing {nameof(CardsComponent)}");
                if (!SEntMan.TryGetComponent<StackComponent>(split.Value, out var splitStack))
                    Assert.Fail($"Split entity missing {nameof(StackComponent)}");

                // Total cards across both stacks must be preserved
                Assert.That(
                    cards.Cards.Count + splitCards!.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards after split does not equal cards before split"
                );
                Assert.That(
                    stack.Count + splitStack!.Count,
                    Is.EqualTo(stackBefore),
                    $"{nameof(StackComponent)} counts after split do not add up to original"
                );

                // Now merge back
                if (!_sStacks.TryMergeStacks((split.Value, splitStack), (uid, stack), out _))
                    Assert.Fail("TryMergeStacks failed");

                // After merge, the recipient (uid) should have all the cards
                Assert.That(
                    cards.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Cards count after re-merge should equal original"
                );
                Assert.That(
                    stack.Count,
                    Is.EqualTo(stackBefore),
                    $"{nameof(StackComponent.Count)} after re-merge should equal original"
                );
            }
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
            var splitAmount = 13;

            var split = _sStacks.Split((uid, stack), splitAmount, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(split, Is.Not.Null, "Split returned null");

            if (!SEntMan.TryGetComponent<CardsComponent>(split!.Value, out var splitCards))
                Assert.Fail($"Split entity missing {nameof(CardsComponent)}");
            if (!SEntMan.TryGetComponent<StackComponent>(split.Value, out var splitStack))
                Assert.Fail($"Split entity missing {nameof(StackComponent)}");

            Assert.That(
                splitCards!.Cards.Count,
                Is.EqualTo(splitAmount),
                "Split deck card list should have exactly splitAmount cards"
            );
            Assert.That(
                splitStack!.Count,
                Is.EqualTo(splitAmount),
                $"Split {nameof(StackComponent.Count)} should equal splitAmount"
            );
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

            var split = _sStacks.Split((uid, stack), 10, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(split, Is.Not.Null);

            if (!SEntMan.TryGetComponent<CardsComponent>(split!.Value, out var splitCards))
                Assert.Fail("Split entity missing CardsComponent");

            Assert.That(splitCards!.Flipped, Is.True, "Split deck should inherit Flipped state from parent");
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

            var split = _sStacks.Split((uid, stack), 10, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(split, Is.Not.Null);

            if (!SEntMan.TryGetComponent<CardsComponent>(split!.Value, out var splitCards))
                Assert.Fail("Split entity missing CardsComponent");

            Assert.That(splitCards!.Fanned, Is.True, "Split deck should inherit Fanned state from parent");
        });
    }

    [Test]
    public async Task CardsTryTakeCardReducesDeckByOne()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                var player = SSpawnAtPosition("MobHuman", coords);
                var (uid, cards, stack) = SpawnDeck(coords);
                var cardsBefore = cards.Cards.Count;
                var stackBefore = stack.Count;

                if (!SEntMan.TryGetComponent<TransformComponent>(player, out var playerXform))
                    Assert.Fail($"Missing player {nameof(TransformComponent)}");

                // Take a specific card by index
                var targetInx = cards.Cards[20].CardInx;
                var result = _sCards.TryTakeCard((uid, cards), (player, playerXform), targetInx, out var split);

                Assert.That(result, Is.True, "TryTakeCard should return true");
                Assert.That(split, Is.Not.Null, "TryTakeCard should produce a split entity");

                if (!SEntMan.TryGetComponent<CardsComponent>(split, out var splitCards))
                    Assert.Fail($"Split entity missing {nameof(CardsComponent)}");
                if (!SEntMan.TryGetComponent<StackComponent>(split!.Value, out var splitStack))
                    Assert.Fail($"Split entity missing {nameof(StackComponent)}");

                // Total must be preserved
                Assert.That(
                    cards.Cards.Count + splitCards!.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards must be preserved after TryTakeCard"
                );
                Assert.That(
                    stack.Count + splitStack!.Count,
                    Is.EqualTo(stackBefore),
                    $"Total {nameof(StackComponent.Count)} must be preserved after TryTakeCard"
                );

                // The split should have exactly 1 card
                Assert.That(splitCards.Cards.Count, Is.EqualTo(1), "TryTakeCard split should have exactly one card");

                // That one card should have the correct CardInx
                Assert.That(
                    splitCards.Cards[0].CardInx,
                    Is.EqualTo(targetInx),
                    "The taken card should be the one with the requested CardInx"
                );

                result = _sCards.TryTakeCard((uid, cards), (player, playerXform), targetInx, out split);

                // Total must be preserved
                Assert.That(
                    cards.Cards.Count + splitCards!.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards must be preserved after TryTakeCard"
                );
                Assert.That(
                    stack.Count + splitStack!.Count,
                    Is.EqualTo(stackBefore),
                    $"Total {nameof(StackComponent.Count)} must be preserved after TryTakeCard"
                );

                var ok = _sStacks.TryMergeStacks((uid, stack), (split.Value, splitStack!), out var transferred, amount: cards.NumberOfCards - 1);

                // Total must be preserved
                Assert.That(
                    cards.Cards.Count + splitCards!.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards must be preserved after TryTakeCard"
                );
                Assert.That(
                    stack.Count + splitStack!.Count,
                    Is.EqualTo(stackBefore),
                    $"Total {nameof(StackComponent.Count)} must be preserved after TryTakeCard"
                );

                result = _sCards.TryTakeCard((uid, cards), (player, playerXform), targetInx, out split);

                Assert.That(
                    splitCards!.Cards.Count,
                    Is.EqualTo(cardsBefore),
                    "Total cards must be preserved after TryTakeCard"
                );
                Assert.That(
                    splitStack!.Count,
                    Is.EqualTo(stackBefore),
                    $"Total {nameof(StackComponent.Count)} must be preserved after TryTakeCard"
                );
            }
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
            var (uid, cards, stack) = SpawnDeck(coords);
            var cardsBefore = cards.Cards.Count;

            if (!SEntMan.TryGetComponent<TransformComponent>(player, out var playerXform))
                Assert.Fail($"Missing player {nameof(TransformComponent)}");

            // Use an index that definitely doesn't exist
            var result = _sCards.TryTakeCard((uid, cards), (player, playerXform), int.MaxValue, out var split);

            Assert.That(result, Is.False, "TryTakeCard with a non-existent CardInx should return false");

            // Total cards should be unchanged (no leak)
            // If split was created it should hold 0 cards net
            var actualCards = cards.Cards.Count;
            if (split != null && SEntMan.TryGetComponent<CardsComponent>(split.Value, out var splitCards))
                actualCards += splitCards.Cards.Count;

            Assert.That(actualCards, Is.EqualTo(cardsBefore), "Card total must not change when TryTakeCard fails");
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

            var split = _sStacks.Split((uid, stack), 5, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(split, Is.Not.Null);

            if (!SEntMan.TryGetComponent<CardsComponent>(split!.Value, out var splitCards))
                Assert.Fail($"Missing {nameof(CardsComponent)} on split");

            var splitInxes = splitCards!.Cards.Select(c => c.CardInx).ToList();
            Assert.That(
                splitInxes,
                Is.EquivalentTo(originalFirstCards),
                "Unflipped deck: split should take the first N cards (by CardInx)"
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
            var total = cards.Cards.Count;
            var originalLastCards = cards.Cards.TakeLast(5).Select(c => c.CardInx).ToList();

            _sCards.TryFlipCards((uid, cards));

            var split = _sStacks.Split((uid, stack), 5, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(split, Is.Not.Null);

            if (!SEntMan.TryGetComponent<CardsComponent>(split!.Value, out var splitCards))
                Assert.Fail($"Missing {nameof(CardsComponent)} on split");

            var splitInxes = splitCards!.Cards.Select(c => c.CardInx).ToList();
            Assert.That(
                splitInxes,
                Is.EquivalentTo(originalLastCards),
                "Flipped deck: split should take the last N cards (by CardInx)"
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
            using (Assert.EnterMultipleScope())
            {
                var (uidA, cardsA, stackA) = SpawnDeck(coords);
                var splitA = _sStacks.Split((uidA, stackA), 40, new EntityCoordinates(uidA, Vector2.Zero));
                SQueueDel(splitA.Value);
                var (uidB, cardsB, stackB) = SpawnDeck(coords);
                var splitB = _sStacks.Split((uidB, stackB), 40, new EntityCoordinates(uidB, Vector2.Zero));
                SQueueDel(splitB.Value);

                var countA = cardsA.Cards.Count;
                var countB = cardsB.Cards.Count;

                // Merge B into A (A is the recipient)
                var ok = _sStacks.TryMergeStacks((uidB, stackB), (uidA, stackA), out var transferred);
                Assert.That(ok, Is.True, "TryMergeStacks should succeed for two compatible decks");
                Assert.That(transferred, Is.GreaterThan(0));

                // A (recipient) should have more cards, B (donor) fewer
                Assert.That(
                    cardsA.Cards.Count,
                    Is.EqualTo(countA + transferred),
                    "Recipient should gain the transferred cards"
                );
                Assert.That(
                    cardsB.Cards.Count,
                    Is.EqualTo(countB - transferred),
                    "Donor should lose the transferred cards"
                );
            }
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

            // Split off everything except one card
            var leaveOne = stack.Count - 1;
            var split = _sStacks.Split((uid, stack), leaveOne, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(split, Is.Not.Null);
            Assert.That(stack.Count, Is.EqualTo(1));

            // Fan a single card
            _sCards.TryFanCards((uid, cards));

            // FanRadius returns 0 for count <= 1, which is fine visually,
            // but the Fanned flag itself may still toggle — what matters is
            // the VisualState only shows 1 card.
            var state = _sCards.GetCardListVisualState(cards);
            Assert.That(
                state.Count,
                Is.EqualTo(1),
                "Visual state of a single-card deck should show exactly 1 card even when Fanned"
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
            var distinct = inxes.Distinct().ToList();

            Assert.That(distinct.Count, Is.EqualTo(inxes.Count), "Every CardInx should be unique across the deck");
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
            var split = _sStacks.Split((uid, stack), 13, new EntityCoordinates(uid, Vector2.Zero));
            Assert.That(split, Is.Not.Null);

            if (!SEntMan.TryGetComponent<CardsComponent>(split!.Value, out var splitCards))
                Assert.Fail($"Missing {nameof(CardsComponent)} on split");

            var allInxes = cards.Cards.Select(c => c.CardInx).Concat(splitCards!.Cards.Select(c => c.CardInx)).ToList();
            var distinct = allInxes.Distinct().ToList();

            Assert.That(
                distinct.Count,
                Is.EqualTo(allInxes.Count),
                "CardInxes must remain unique across both halves after split"
            );
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
            Assert.That(after, Is.EqualTo(before), "After shuffle the same CardInxes must all be present");
        });
    }
}
