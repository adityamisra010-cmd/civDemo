using Proto.M7.Econ;

namespace Proto.M7.Econ.Tests;

/// <summary>
/// M7 lane D: the economic-scale probe (Proto.M7.Econ/ScaleProbe.cs; docs/m7-plan/economic-scale-proposal.md). Read-only use of
/// Sim.Core; these runs step the real founded world.
/// </summary>
public class ScaleProbeTests
{
    /// <summary>SEMANTIC: where adults bind, the probe's formation-granular predicate bound equals what the shipped predicate
    /// actually accepts when every adult-limited RecruitFormation order is issued (lane F's real twin), MEASURED for seed 1 at
    /// turn 25 on HEAD (3,500 = 175 warbands of 20); and the research driver issues research orders (its world differs from the
    /// order-less one).</summary>
    [Fact]
    public void PredicateBound_EqualsTheRealTwin_WhereAdultsBind()
    {
        var (points, twins) = ScaleProbe.Run(1, ScaleDriver.None, new long[] { 25 }, new long[] { 25 }, null);
        var p = points.Single();
        var t = twins.Single();
        Assert.Equal(20, p.PersonnelPerFormation);
        Assert.Equal(t.SoldiersAfter - t.SoldiersBefore, p.PredicateBoundExtra);
        Assert.Equal(3_500, p.PredicateBoundExtra);
        Assert.Contains("PersonnelShort", t.Binding);                  // adults bound the twin
        Assert.True(p.GrainBoundTotal - p.Soldiers > p.PredicateBoundExtra);   // grain did not bind
        var (research, _) = ScaleProbe.Run(1, ScaleDriver.Research, new long[] { 25 }, Array.Empty<long>(), null);
        Assert.NotEqual(p.WorldHashPrefix, research.Single().WorldHashPrefix);
    }

    /// <summary>[verification round 2, D-R2-05] SEMANTIC: the real twin runs on RESEARCH-driven worlds too (before round 2 the probe
    /// skipped it, so the economic-scale proposal's research row at turn 25 was the formula, never a twin). Seed 7 at turn 25,
    /// MEASURED on HEAD (Sim.* unchanged since 45c3313): the formula allows 3,380 extra soldiers (adults bind it); the shipped
    /// predicate accepts 2,260, because stone binds ("MaterialsShort"), which the formula does not model. Also a labelled pin of
    /// Sim.Core's state at that turn (like the HEAD pin below).</summary>
    [Fact]
    public void ResearchTwin_Runs_AndStoneBindsBelowTheFormula_Seed7Turn25()
    {
        var (points, twins) = ScaleProbe.Run(7, ScaleDriver.Research, new long[] { 25 }, new long[] { 25 }, null);
        var p = points.Single();
        var t = twins.Single();
        Assert.Equal((ScaleDriver.Research, 25L), (t.Driver, t.Turn));
        Assert.Equal(3_380, p.PredicateBoundExtra);
        Assert.Equal(2_260, t.SoldiersAfter - t.SoldiersBefore);
        Assert.Contains("MaterialsShort", t.Binding);
        Assert.DoesNotContain("PersonnelShort", t.Binding);
    }

    /// <summary>[CB] SEMANTIC: a capped twin issues at most the cap's orders per settlement and is a lower bound of the full twin;
    /// a cap at or above every settlement's adult-limited order count IS the full twin. Seed 1 at turn 25, no orders (the world of
    /// the first test, whose full twin accepts 3,500 = 175 warbands): cap 2 issues at most 2 orders in each of the 12 settlements,
    /// every one accepted (20 soldiers each: neither adults nor stone bind so few); cap 1,000,000 equals the full twin order for
    /// order; the twins come back in the order of the caps, each recording its cap; a non-positive cap is refused.</summary>
    [Fact]
    public void CappedTwin_IsALowerBound_AndACapAboveEverySettlementIsTheFullTwin()
    {
        var timing = new StringWriter();
        var (_, twins) = ScaleProbe.Run(1, ScaleDriver.None, new long[] { 25 }, new long[] { 25 }, new[] { 2L, 1_000_000L, long.MaxValue }, null, timing);
        Assert.Equal(new[] { 2L, 1_000_000L, long.MaxValue }, twins.Select(t => t.CapPerSettlement).ToArray());
        var (capped, wide, full) = (twins[0], twins[1], twins[2]);
        Assert.InRange(capped.OrdersIssued, 1, 2 * 12);
        Assert.Equal(20L * capped.OrdersIssued, capped.SoldiersAfter - capped.SoldiersBefore);   // every capped order accepted
        Assert.Equal(3_500, full.SoldiersAfter - full.SoldiersBefore);
        Assert.True(capped.SoldiersAfter - capped.SoldiersBefore < full.SoldiersAfter - full.SoldiersBefore);
        Assert.Equal((full.OrdersIssued, full.SoldiersBefore, full.SoldiersAfter, full.Binding), (wide.OrdersIssued, wide.SoldiersBefore, wide.SoldiersAfter, wide.Binding));
        Assert.Equal(3, timing.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => ScaleProbe.Run(1, ScaleDriver.None, new long[] { 1 }, new long[] { 1 }, new[] { 0L }, null, null));
    }

    /// <summary>SEMANTIC (the predicate's own rules at their boundaries): a settlement with exactly `per` adults takes no formation
    /// (one adult stays), with per + 1 it takes one; the purse binds when its grain persons less the soldiers already owned are
    /// fewer; no recruitable identity means no bound.</summary>
    [Theory]
    [InlineData(20L, 1_000_000L, 0L, new long[] { 20, 21, 40, 41 }, 80L)]   // 0 + 1 + 1 + 2 formations
    [InlineData(20L, 1_000_000L, 0L, new long[] { 19, 1, 0 }, 0L)]
    [InlineData(20L, 100L, 20L, new long[] { 20, 21, 40, 41 }, 80L)]        // grain: (100 - 20) / 20 = 4 = adults' 4
    [InlineData(20L, 99L, 20L, new long[] { 20, 21, 40, 41 }, 60L)]         // grain: 79 / 20 = 3 binds
    [InlineData(20L, 10L, 20L, new long[] { 400 }, 0L)]                     // owned soldiers exceed the purse
    [InlineData(40L, long.MaxValue, 0L, new long[] { 81, 80 }, 120L)]        // 2 + 1
    [InlineData(0L, 1_000_000L, 0L, new long[] { 400 }, 0L)]                // nothing recruitable
    public void PredicateBound_AtTheRulesBoundaries(long per, long grainPersons, long owned, long[] adults, long expected)
        => Assert.Equal(expected, ScaleProbe.PredicateBoundExtra(per, grainPersons, owned, adults));

    /// <summary>HEAD PIN (a golden, labelled: it pins Sim.Core's state, not a mechanism of this lane): the research driver's driver
    /// polity population for turns 1-25 of seed 42 equals the trace of `sim research --seed 42 --auto cheapest
    /// --trace-population` measured on HEAD 45c3313 (the whole 1,630-turn trace was compared byte for byte outside the tests).</summary>
    [Fact]
    public void ResearchDriverTrace_Seed42_First25Turns_EqualTheCliOnHead()
    {
        long[] cli =
        {
            5096, 5109, 5141, 5181, 5213, 5265, 5298, 5333, 5373, 5412, 5473, 5510, 5555, 5594, 5638, 5685, 5720, 5773, 5818, 5862,
            5908, 5934, 5993, 6038, 6115,
        };
        var sw = new StringWriter();
        ScaleProbe.Run(42, ScaleDriver.Research, new long[] { 25 }, Array.Empty<long>(), sw);
        var lines = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("turn,population", lines[0]);
        for (int t = 1; t <= 25; t++) Assert.Equal($"{t},{cli[t - 1]}", lines[t]);
    }
}
