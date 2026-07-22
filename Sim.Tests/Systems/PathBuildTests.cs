using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.PathBuild;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// T1.6 acceptance: the labor order end-to-end (log → allocation row → dt-correct
// yield shift → path bank), the build loop through gameplay (edge → revision →
// next-turn catchment growth), boundary orders, load-time rejection, ordered
// twin determinism, and the no-order behavior proof.
public class PathBuildTests
{
    private const ulong Seed = 42;

    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static TurnExecutor ProductionExecutor(SimConfig cfg, OrderLog? orders = null)
    {
        using var stream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            CanonicalEra(), PipelineLoader.Load(stream, SystemCatalog.All(cfg)), orders);
    }

    private static WorldState Founded(SimConfig cfg) =>
        WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed);

    private static OrderLog LaborOrder(long turn, double farmPct)
    {
        var log = new OrderLog();
        log.Append(new OrderRecord(turn, ActorId: 1, OrderKind.LaborAllocation, TargetId: 0, farmPct));
        return log;
    }

    private static long HarvestSourced(WorldState world)
    {
        for (int i = 0; i < world.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = world.LedgerFlows[i];
            if (row.Quantity == ConservedQuantityIds.Food && row.Reason == ReasonIds.Harvest)
                return row.TotalSourced;
        }
        return 0;
    }

    // --- end-to-end order pipe ----------------------------------------------

    [Fact]
    public void Order_SetsAllocationRowOnItsTurn_YieldShiftsDtCorrectlyNextTurn_BankAccrues()
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg, LaborOrder(turn: 2, farmPct: 50.0));
        WorldState world = Founded(cfg);

        // Turns 1 and 2: no order delivered yet; allocations stay empty.
        world = exec.Step(world);
        world = exec.Step(world);
        Assert.Equal(0, world.LaborAllocations.Count);

        // The order (Turn = 2) is delivered to the step FROM turn-2 state: the
        // allocation row exists in turn-3 state, exactly 0.5.
        world = exec.Step(world);
        Assert.Equal(1, world.LaborAllocations.Count);
        Assert.Equal(new SettlementId(0), world.LaborAllocations[0].Settlement);
        Assert.Equal(0.5, world.LaborAllocations[0].FarmShare);

        // Yield shifts NEXT turn (Farming reads Prev): the step from turn-3
        // state harvests farmland × 0.5 × yield × dtYears — hand-computed exact
        // from turn-3 state (law 3: the rate integrates dtYears = 10).
        double farmland = world.CatchmentSummaries[0].EffectiveFarmland;
        double remainder = world.FoodStores[0].HarvestRemainder;
        long adultsT3 = world.PopBands[1].Count.Value;
        long harvestBefore = HarvestSourced(world);

        world = exec.Step(world);
        long expected = (long)Math.Floor(
            farmland * 0.5 * cfg.Farming.YieldPerFarmlandPerYear * 10.0 + remainder);
        Assert.Equal(expected, HarvestSourced(world) - harvestBefore);

        // And the path bank accrued from the same Prev allocation, dt-correctly.
        Assert.Equal(1, world.PathProgress.Count);
        Assert.Equal(cfg.PathBuild.LaborPerAdultPerYear * 0.5 * adultsT3 * 10.0,
            world.PathProgress[0].Banked);
    }

    [Fact]
    public void BuildLoop_EdgeAppends_RevisionIncrements_CatchmentStrictlyGrowsNextTurn()
    {
        // Sustained 50% path allocation: N turns later an edge appends, the
        // revision increments, and the NEXT turn's catchment strictly grows
        // with effective farmland strictly increasing — T1.4's hand-test now
        // happens through gameplay.
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg, LaborOrder(turn: 2, farmPct: 50.0));
        WorldState world = Founded(cfg);

        int firstEdgeTurn = -1;
        int nodesBefore = 0;
        double farmlandBefore = 0.0;
        for (int t = 1; t <= 30 && firstEdgeTurn < 0; t++)
        {
            nodesBefore = world.CatchmentSummaries.Count > 0 ? world.CatchmentSummaries[0].NodeCount : 0;
            farmlandBefore = world.CatchmentSummaries.Count > 0 ? world.CatchmentSummaries[0].EffectiveFarmland : 0.0;
            world = exec.Step(world);
            if (world.NetworkEdges.Count > 0) firstEdgeTurn = t;
        }

        Assert.True(firstEdgeTurn > 0, "no edge appended in 30 turns of sustained 50% path labor");
        Assert.Equal(world.NetworkEdges.Count, world.NetworkMeta[0].Revision); // one bump per edge
        Assert.True(world.NetworkNodes.Count >= 2, "edge appended without anchor nodes");
        Console.WriteLine($"turns-to-first-edge @ seed {Seed} (order t2, 50/50, default rates): {firstEdgeTurn}");

        // The D-016 lag, driven by gameplay: NEXT turn the catchment recomputes
        // strictly larger, farmland strictly higher.
        world = exec.Step(world);
        Assert.True(world.CatchmentSummaries[0].NodeCount > nodesBefore,
            $"catchment did not grow: {world.CatchmentSummaries[0].NodeCount} <= {nodesBefore}");
        Assert.True(world.CatchmentSummaries[0].EffectiveFarmland > farmlandBefore,
            $"farmland did not grow: {world.CatchmentSummaries[0].EffectiveFarmland} <= {farmlandBefore}");
    }

    // --- boundary orders -----------------------------------------------------

    [Fact]
    public void ZeroPercentFarm_IsLegal_AndTheFamineActuallyArrives()
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg, LaborOrder(turn: 2, farmPct: 0.0));
        WorldState world = Founded(cfg);

        bool famine = false;
        long starved = 0;
        for (int t = 1; t <= 40; t++)
        {
            world = exec.Step(world);
            if (world.ConsumptionDeficits.Count > 0 && world.ConsumptionDeficits[0].DeficitRatio > 0.0)
                famine = true;
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity == ConservedQuantityIds.Population && row.Reason == ReasonIds.Starvation)
                    starved = row.TotalSunk;
            }
        }

        Assert.Equal(0.0, world.LaborAllocations[0].FarmShare);
        Assert.True(famine, "0% farm never produced a consumption deficit in 40 turns");
        Assert.True(starved > 0, "0% farm never starved anyone in 40 turns");
        Assert.True(world.PathProgress[0].Banked > 0.0 || world.NetworkEdges.Count > 0,
            "full path allocation banked nothing");
    }

    [Fact]
    public void HundredPercentFarm_IsLegal_BanksNothing_BuildsNothing()
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg, LaborOrder(turn: 2, farmPct: 100.0));
        WorldState world = Founded(cfg);

        for (int t = 1; t <= 30; t++) world = exec.Step(world);

        Assert.Equal(1.0, world.LaborAllocations[0].FarmShare); // explicit row == default
        Assert.Equal(0, world.PathProgress.Count);              // zero accrual → row never created
        Assert.Equal(0, world.NetworkEdges.Count);
        Assert.Equal(0, world.NetworkMeta[0].Revision);
    }

    // --- load-time rejection -------------------------------------------------

    private static byte[] RawOrderLog(params (long Turn, int Kind, int Target, double Amount)[] records)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("CIVORDR\0"u8);
            writer.Write(OrderLog.IoVersion);
            writer.Write(records.Length);
            foreach ((long turn, int kind, int target, double amount) in records)
            {
                writer.Write(turn);
                writer.Write(1); // actor
                writer.Write(kind);
                writer.Write(target);
                writer.Write(BitConverter.DoubleToInt64Bits(amount));
            }
        }
        return buffer.ToArray();
    }

    [Fact]
    public void OutOfRangeFarmPct_RejectedAtLoad_Actionably()
    {
        using var stream = new MemoryStream(RawOrderLog((5, 2, 0, 150.0)));
        var e = Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(stream));
        Assert.Contains("[0,100]", e.Message);
        Assert.Contains("turn 5", e.Message);

        using var nan = new MemoryStream(RawOrderLog((3, 2, 0, double.NaN)));
        Assert.Contains("[0,100]",
            Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(nan)).Message);
    }

    [Fact]
    public void UnknownOrderKind_RejectedAtLoad_Actionably()
    {
        using var stream = new MemoryStream(RawOrderLog((7, 99, 0, 1.0)));
        var e = Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(stream));
        Assert.Contains("unknown order kind 99", e.Message);
    }

    [Fact]
    public void UnknownSettlement_RejectedBeforeTurnOne_Actionably()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState founded = Founded(cfg);
        var bad = new OrderLog();
        bad.Append(new OrderRecord(1, ActorId: 1, OrderKind.LaborAllocation, TargetId: 7, Amount: 50.0));
        var e = Assert.Throws<OrderValidationException>(() =>
            OrderValidation.ValidateAgainstWorld(bad, founded));
        Assert.Contains("settlement 7", e.Message);
        Assert.Contains("does not exist", e.Message);

        // Toy worlds have no settlements at all — same up-front rejection.
        var toy = new WorldState(1);
        Assert.Throws<OrderValidationException>(() =>
            OrderValidation.ValidateAgainstWorld(LaborOrder(turn: 1, farmPct: 50.0), toy));
    }

    // --- ordered determinism -------------------------------------------------

    private static OrderLog SweepLog()
    {
        var log = new OrderLog();
        double[] pcts = [30.0, 50.0, 70.0, 0.0, 100.0, 40.0];
        for (int i = 0; i < pcts.Length; i++)
            log.Append(new OrderRecord(2 + i * 15, ActorId: 1, OrderKind.LaborAllocation, 0, pcts[i]));
        return log;
    }

    [Fact]
    public void OrderedRun_TwinDeterministic_AndReplayHashIdentical_200Turns()
    {
        // Twin A and twin B are constructed completely independently from the
        // same (seed, order log) — which is also exactly what replay(seed, log)
        // does (T0.7 contract). Hash-identical at EVERY turn, 200 turns.
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor execA = ProductionExecutor(cfg, SweepLog());
        TurnExecutor execB = ProductionExecutor(TestConfigs.Sim(), SweepLog());
        WorldState a = Founded(cfg);
        WorldState b = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), Seed);

        for (int t = 1; t <= 200; t++)
        {
            a = execA.Step(a);
            b = execB.Step(b);
            Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        }
        Assert.True(a.NetworkEdges.Count > 0, "ordered twin run built no path — vacuous determinism");
    }

    // --- no-order behavior ---------------------------------------------------

    [Fact]
    public void NoOrders_T15ShapePreserved_NothingAllocatesOrBuilds()
    {
        // Without orders the allocation default (1.0 farm) applies and PathBuild
        // is inert: no rows, no edges, revision stays 0 — the T1.5 world shape.
        // (The pinned golden covers the toy world; this covers production.)
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = ProductionExecutor(cfg);
        WorldState world = Founded(cfg);
        for (int t = 1; t <= 30; t++) world = exec.Step(world);

        Assert.Equal(0, world.LaborAllocations.Count);
        Assert.Equal(0, world.PathProgress.Count);
        Assert.Equal(0, world.NetworkEdges.Count);
        Assert.Equal(0, world.NetworkNodes.Count);
        Assert.Equal(0, world.NetworkMeta[0].Revision);
        Assert.True(world.FoodStores[0].Store.Value > 0 || world.PopBands[1].Count.Value > 0);
    }

    // --- tie-dense target choice (constitution rule) -------------------------

    [Fact]
    public void ChooseTarget_TieDense_LowestNodeIdWins()
    {
        var lattice = TraversalLattice.FromCosts(4,
            nodeCost: [.. Enumerable.Repeat(1.0, 16)],
            passable: [.. Enumerable.Repeat(true, 16)]);
        var none = new bool[16];
        var all = new bool[16];
        Array.Fill(all, true);

        // All 16 fertilities EXACTLY equal → lowest id wins.
        double[] flat = [.. Enumerable.Repeat(0.5, 16)];
        Assert.Equal(0, PathBuildSystem.ChooseTarget(lattice, flat, none, all));

        // Exclude the winner → next id.
        var exclude0 = new bool[16];
        exclude0[0] = true;
        Assert.Equal(1, PathBuildSystem.ChooseTarget(lattice, flat, exclude0, all));

        // Two tied plateaus, higher one starting mid-array → ITS lowest id.
        double[] plateaus = [.. Enumerable.Repeat(0.5, 16)];
        plateaus[9] = plateaus[10] = plateaus[13] = 0.75;
        Assert.Equal(9, PathBuildSystem.ChooseTarget(lattice, plateaus, none, all));

        // Impassable and ineligible nodes never win, even at max fertility.
        double[] spiked = [.. Enumerable.Repeat(0.5, 16)];
        spiked[3] = 1.0;
        var noThree = new bool[16];
        Array.Fill(noThree, true);
        noThree[3] = false;
        Assert.Equal(0, PathBuildSystem.ChooseTarget(lattice, spiked, none, noThree));
    }
}
