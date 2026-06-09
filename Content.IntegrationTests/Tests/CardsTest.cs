using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Hands.Systems;
using Content.Shared.Cards;
using Content.Shared.Hands.Components;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class CardsTest : GameTest
{
    [SidedDependency(Side.Server)]
    private readonly SharedCardSystem _sCards = null!;

    [SidedDependency(Side.Server)]
    private readonly SharedStackSystem _sStacks = null!;

    [SidedDependency(Side.Server)]
    private readonly HandsSystem _sHands = null!;
    [SidedDependency(Side.Server)]
    private readonly TransformSystem _sTransform = null!;
    private const string CardsProtoId = "cardDeck";

    [Test]
    public async Task CardsCountIntegrity()
    {
        await Pair.CreateTestMap();
        var coordinates = Pair.TestMap!.GridCoords;

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                var player = SSpawnAtPosition("MobHuman", coordinates);

                var cards = SSpawnAtPosition(CardsProtoId, coordinates);

                int cardCountBefore;
                int stackCountBefore;

                if (!SEntMan.TryGetComponent<CardsComponent>(cards, out var cardsComp))
                    Assert.Fail($"Missing Comp");
                if (!SEntMan.TryGetComponent<StackComponent>(cards, out var stackComp))
                    Assert.Fail();

                cardCountBefore = cardsComp.Cards.Count;
                stackCountBefore = stackComp.Count;
                _sCards.TryShuffleCards((cards, cardsComp));

                Assert.That(cardsComp.Cards.Count, Is.EqualTo(cardCountBefore));
                Assert.That(stackComp.Count,Is.EqualTo(stackCountBefore));

                _sCards.TryFlipCards((cards, cardsComp));

                Assert.That(cardsComp.Cards.Count, Is.EqualTo(cardCountBefore));
                Assert.That(stackComp.Count, Is.EqualTo(stackCountBefore));

                _sCards.TryFanCards((cards, cardsComp));

                Assert.That(cardsComp.Cards.Count, Is.EqualTo(cardCountBefore));
                Assert.That(stackComp.Count, Is.EqualTo(stackCountBefore));

                if (!SEntMan.TryGetComponent<TransformComponent>(player, out var playerTransformComp))
                    Assert.Fail($"No Cards Prototype with name {cards}");

                _sCards.TryTakeCard((cards, cardsComp), (player, playerTransformComp), 20, out var split);
                if (split == null)
                    Assert.Fail();
                if (!SEntMan.TryGetComponent<CardsComponent>(split, out var splitCardsComp))
                    Assert.Fail();
                if (!SEntMan.TryGetComponent<StackComponent>(split, out var splitStackComp))
                    Assert.Fail();
                if (_sHands.GetActiveItem(player) != split)
                    Assert.Fail();

                Assert.That(cardsComp.Cards.Count + splitCardsComp.Cards.Count, Is.EqualTo(cardCountBefore));
                Assert.That(stackComp.Count + splitStackComp.Count, Is.EqualTo(stackCountBefore));

                if (!_sStacks.TryMergeStacks((cards, stackComp), (split.Value, splitStackComp), out var transferred))
                    Assert.Fail();

                Assert.That(splitCardsComp.Cards.Count, Is.EqualTo(cardCountBefore));
                Assert.That(splitStackComp.Count, Is.EqualTo(stackCountBefore));

                cards = split.Value;
                cardsComp = splitCardsComp;
                stackComp = splitStackComp;

                if (!SEntMan.TryGetComponent<HandsComponent>(player, out var handsComp))
                    Assert.Fail();
                if (!SEntMan.TryGetComponent<TransformComponent>(player, out var playerTransComp))
                    Assert.Fail();
                _sHands.SwapHands((player, handsComp));

                _sStacks.UserSplit((cards, stackComp), (player, playerTransComp), 20);
                split = _sHands.GetActiveItem(player);
                if (!SEntMan.TryGetComponent<CardsComponent>(split, out splitCardsComp))
                    Assert.Fail();
                if (!SEntMan.TryGetComponent<StackComponent>(split, out splitStackComp))
                    Assert.Fail();

                Assert.That(cardsComp.Cards.Count + splitCardsComp.Cards.Count, Is.EqualTo(cardCountBefore));
                Assert.That(stackComp.Count + splitStackComp.Count, Is.EqualTo(stackCountBefore));

                _sStacks.UserSplit((cards, stackComp), (player, playerTransComp), 20);

                Assert.That(cardsComp.Cards.Count + splitCardsComp.Cards.Count, Is.EqualTo(cardCountBefore));
                Assert.That(stackComp.Count + splitStackComp.Count, Is.EqualTo(stackCountBefore));

                if (!_sStacks.TryMergeStacks((cards, stackComp), (split.Value, splitStackComp), out transferred))
                    Assert.Fail();

                cards = split.Value;
                cardsComp = splitCardsComp;
                stackComp = splitStackComp;
                var coords = _sTransform.GetMapCoordinates(player).Offset(new Vector2(0, 10));
                // _sHands.ThrowHeldItem(player, _sTransform.ToCoordinates(player, coords));
            }
        });
    }
}

[TestFixture]
public sealed class CardsInteractionTest : InteractionTest
{
    private const string CardsProtoId = "cardDeck";

    [Test]
    public async Task CardsInteraction()
    {
        var cards = await SpawnTarget(CardsProtoId);

        if (!SEntMan.TryGetComponent<CardsComponent>(SEntMan.GetEntity(cards), out var sCardsComp))
            Assert.Fail($"Missing Comp");
        if (!SEntMan.TryGetComponent<StackComponent>(SEntMan.GetEntity(cards), out var sStackComp))
            Assert.Fail($"Missing Comp");

        int cardCountBefore = sCardsComp.Cards.Count;
        int stackCountBefore = sStackComp.Count;

        await Pickup(deleteHeld: false);

        // Flip the deck of cards
        await UseInHand();

        Assert.That(sCardsComp.Flipped, Is.EqualTo(true));

        // Fans the deck of cards
        await UseInHand();

        Assert.That(sCardsComp.Flipped, Is.EqualTo(true));
        Assert.That(sCardsComp.Fanned, Is.EqualTo(true));

        // Throw One Card
        var coords = Transform.GetMapCoordinates(CPlayer).Offset(new Vector2(0, 10));
        await ThrowItem(SEntMan.GetNetCoordinates(Transform.ToCoordinates(CPlayer, coords)));

        Assert.That(sCardsComp.Cards.Count, Is.EqualTo(cardCountBefore - 1));
        Assert.That(sStackComp.Count, Is.EqualTo(stackCountBefore - 1));
        Assert.That(sCardsComp.Flipped, Is.EqualTo(true));
        Assert.That(sCardsComp.Fanned, Is.EqualTo(true));
    }
}
