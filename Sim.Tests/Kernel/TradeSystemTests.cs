using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.Kernel;

// T0.6: the toy TradeSystem shuffles goods between two regions through the
// Ledger; world totals stay exact at every turn (the auditor runs per turn here,
// as T0.8's harness will).
public class TradeSystemTests
{
    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 10 } ] }""");

    private static WorldState NewWorld(ulong seed)
    {
        var world = new WorldState(seed);
        world.Regions.Add(new RegionRow(new RegionId(0)));
        world.Regions.Add(new RegionRow(new RegionId(1)));
        return world;
    }

    [Fact]
    public void FullPipeline_ConservesAllQuantities_EveryTurn()
    {
        // All registered systems (M1 production ones no-op on this toy world).
        var executor = new TurnExecutor(FlatEra(), SystemCatalog.All(TestUtil.TestConfigs.Sim()));
        var world = NewWorld(seed: 42);

        for (int turn = 0; turn < 40; turn++)   // even+odd turns → both policies exercised
        {
            world = executor.Step(world);
            Assert.True(ConservationAuditor.IsConserved(world, out string report),
                $"turn {turn + 1}: {report}");
        }

        // The endowment arrived exactly once, through the Ledger.
        var audit = ConservationAuditor.AuditQuantity(world, ConservedQuantityIds.ToyGood);
        Assert.Equal(2 * Sim.Core.Systems.Trade.TradeSystem.InitialEndowment, audit.StockTotal);
        Assert.Equal(2 * Sim.Core.Systems.Trade.TradeSystem.InitialEndowment, audit.TotalSourced);
        Assert.Equal(0, audit.TotalSunk);
    }

    [Fact]
    public void Trade_MovesGoods_BetweenTheTwoRegions()
    {
        var executor = new TurnExecutor(FlatEra(), [SystemCatalog.Trade()]);
        var world = executor.Run(NewWorld(seed: 7), 20);

        // Stocks have diverged from the symmetric endowment (some trade happened)…
        Assert.NotEqual(world.Goods[0].Amount.Value, world.Goods[1].Amount.Value);
        // …while the pair total is untouched.
        Assert.Equal(2 * Sim.Core.Systems.Trade.TradeSystem.InitialEndowment,
            world.Goods[0].Amount.Value + world.Goods[1].Amount.Value);
    }

    [Fact]
    public void GrowthMigration_BiomassSourcedViaLedger_AuditsExactly()
    {
        var executor = new TurnExecutor(FlatEra(), [SystemCatalog.Weather(), SystemCatalog.Growth()]);
        var world = executor.Run(NewWorld(seed: 42), 10);

        var audit = ConservationAuditor.AuditQuantity(world, ConservedQuantityIds.Biomass);
        Assert.True(audit.IsConserved);
        Assert.True(audit.StockTotal > 0);              // growth actually happened
        Assert.Equal(audit.StockTotal, audit.TotalSourced); // all of it born via Flow(Growth)
    }
}
