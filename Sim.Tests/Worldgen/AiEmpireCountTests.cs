using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Worldgen;

/// <summary>
/// M4 §11 — ONE human Empire, N AI Empires, and N is configuration.
///
/// The seam is only real if a non-default value actually produces Empires, so
/// every test here founds a world at a DIFFERENT count and reads the roster the
/// world actually has. The default arm is equally load-bearing: it pins that the
/// shipped world is unchanged, which is what lets every golden stay put.
/// </summary>
public class AiEmpireCountTests
{
    private static WorldState Found(int aiEmpires, ulong seed = 42) =>
        WorldFounding.Found(
            TestConfigs.Worldgen() with { AiEmpires = aiEmpires }, TestConfigs.Sim(), seed);

    private static int PlayerCount(WorldState w)
    {
        int n = 0;
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) n++;
        return n;
    }

    [Fact]
    public void TheShippedDefaultIsUnchanged_OnePolityHoldingEverything()
    {
        // The arm that protects every golden: with no rivals configured, the
        // world is exactly what it was before this seam existed.
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), 42);

        Assert.Equal(1, w.Polities.Count);
        Assert.Equal(CommandSource.Player, w.Polities[0].Source);
        Assert.Equal(w.Settlements.Count, w.Controls.Count);
        Assert.Equal(1, w.Capitals.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(11)]
    public void ExactlyOneEmpireIsHumanCommanded_WhateverTheAiCount(int ai)
    {
        WorldState w = Found(ai);

        Assert.Equal(ai + 1, w.Polities.Count);
        Assert.Equal(1, PlayerCount(w));       // the invariant, at every count
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void EverySettlementIsHeldByExactlyOneEmpire(int ai)
    {
        WorldState w = Found(ai);

        Assert.Equal(w.Settlements.Count, w.Controls.Count);
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            Assert.True(EmpireQuery.TryGetController(w, w.Settlements[s].Id, out PolityId held));
            Assert.True(held.Value >= 1 && held.Value <= ai + 1, $"polity {held.Value} is off-roster");
        }
    }

    [Fact]
    public void TheCountIsNotHardCoded_DifferentValuesGiveDifferentRosters()
    {
        // The anti-hard-coding assertion: three distinct counts, three distinct
        // rosters. A constant buried in the founding code would fail this.
        Assert.Equal(2, Found(1).Polities.Count);
        Assert.Equal(5, Found(4).Polities.Count);
        Assert.Equal(9, Found(8).Polities.Count);
    }

    [Fact]
    public void EachEmpireThatHoldsGroundHasACapitalInsideIt()
    {
        WorldState w = Found(3);

        for (int p = 0; p < w.Polities.Count; p++)
        {
            PolityId id = w.Polities[p].Id;
            if (EmpireQuery.IsExtinct(w, id))
            {
                Assert.False(EmpireQuery.TryGetCapital(w, id, out _),
                    "an Empire holding nothing must not be given a seat");
                continue;
            }

            Assert.True(EmpireQuery.TryGetCapital(w, id, out SettlementId seat));
            Assert.True(EmpireQuery.ControlsSettlement(w, id, seat),
                "a capital must be a place its own Empire actually holds");
        }
    }

    [Fact]
    public void MoreEmpiresThanSettlementsLeavesTheSurplusExtinctRatherThanFailing()
    {
        // A representable state (M4-A), not an error — and worth pinning because
        // the natural implementation throws or invents a settlement here.
        WorldState w = Found(50);

        Assert.Equal(51, w.Polities.Count);
        Assert.Equal(w.Settlements.Count, w.Controls.Count);
        Assert.True(w.Capitals.Count <= w.Settlements.Count);

        int extinct = 0;
        for (int p = 0; p < w.Polities.Count; p++)
            if (EmpireQuery.IsExtinct(w, w.Polities[p].Id)) extinct++;
        Assert.True(extinct > 0, "with 51 Empires and 12 settlements some must hold nothing");
    }
}
