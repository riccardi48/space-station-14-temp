using Content.IntegrationTests.Fixtures;
using Content.Shared.Humanoid.Prototypes;

namespace Content.IntegrationTests.Dwarf;

[TestFixture]
public sealed class DwarfTest : GameTest
{
    private const string Dwarf = "Dwarf";

    [Test]
    [Description("Checks that dwarfs are still in the game")]
    public async Task DwarfsNotRemovedTest()
    {
        await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            if (!SProtoMan.TryIndex<SpeciesPrototype>(Dwarf, out var dwarfPrototype))
            {
                Assert.Fail(
                    "DWARFS REMOVED!!! Freak the fuck out and panic sell everything right now. It's fucking over."
                );
            }
        });
    }
}
