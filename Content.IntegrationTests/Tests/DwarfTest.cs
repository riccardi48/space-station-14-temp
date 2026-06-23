using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

public sealed class DwarfTest : GameTest
{
    private readonly ProtoId<SpeciesPrototype> _dwarf = "Dwarf";

    [Test]
    [Description("Checks that dwarfs are still in the game")]
    [RunOnSide(Side.Server)]
    public async Task DwarfsNotRemovedTest()
    {
        if (!SProtoMan.HasIndex(_dwarf))
        {
            Assert.Fail("DWARFS REMOVED!!! Freak the fuck out and panic sell everything right now. It's fucking over.");
        }
    }
}
