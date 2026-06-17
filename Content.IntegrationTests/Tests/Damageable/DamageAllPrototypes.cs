using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.IntegrationTests.Tests.Damageable;

[TestOf(typeof(DamageableComponent))]
[TestOf(typeof(DamageableSystem))]
public sealed class DamageAllPrototypesTest : GameTest
{
    [SidedDependency(Side.Server)]
    private readonly DamageableSystem _damageableSystem = default!;

    [Test]
    [TestOf(typeof(DamageableSystem))]
    [Description("Ensures all Entity Prototypes with damageable can be damaged.")]
    public async Task TestDamageableComponents()
    {
        await Pair.CreateTestMap();
        var coords = Pair.TestMap!.GridCoords;
        var protoIds = Pair.GetPrototypesWithComponent<DamageableComponent>();

        foreach (var (damageable, comp) in protoIds)
        {
            var entity = await SpawnAtPosition(damageable.ID, coords);

            // Intentionally cannot take damage, ignore it.
            if (SEntMan.HasComponent<GodmodeComponent>(entity))
                return;

            var canBeDamaged = false;

            foreach (var type in SProtoMan.EnumeratePrototypes<DamageTypePrototype>())
            {
                if (!_damageableSystem.CanBeDamagedBy(entity, type))
                    continue;

                canBeDamaged = true;

                await Server.WaitPost(() =>
                {
                    var damage = new DamageSpecifier(type, FixedPoint2.Epsilon);
                    var previousDamage = _damageableSystem.GetTotalDamage(entity);
                    _damageableSystem.ChangeDamage(entity, damage, ignoreResistances: true);
                    Assert.That(_damageableSystem.GetTotalDamage(entity) == FixedPoint2.Epsilon + previousDamage);
                    _damageableSystem.ClearAllDamage(entity);
                });
            }
            // Ensure that this entity can actually be damaged.
            Assert.That(canBeDamaged);
        }
    }
}
