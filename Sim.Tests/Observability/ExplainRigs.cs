using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.19 lane A2 — the worlds the explain-query tests are asked about. Three
/// rigs, each a REAL stepped world through the production pipeline on the
/// D-025 dev preset (256², N = 4), never a hand-tuned fixture where the
/// explanation could agree with a wrong system:
///
///   FED       — founded, stepped under the subsistence default. Comfort is
///               unmet from turn 1 (no pottery or cloth at founding), so
///               grievance is > 0 without any rig: the non-vacuity world.
///   STARVED   — settlement 0 ordered to ZERO farming and ZERO herding at
///               turn 1 (D-032 SectorAllocation orders through the executor —
///               the packet's stated construction). Delivery: orders with
///               Turn 1 land in the step 1→2 (PathBuild writes the row into
///               world 2); Production reads it in step 2→3; world 3 carries
///               the first zero harvest and eats the store down. The rig
///               returns every world so a test can pick the FIRST world whose
///               deficit is positive as Prev and the one after as Next.
///   HOMELESS  — the fed prefix, then every dwelling of settlement 0 sunk
///               through the Ledger (HousingDecayed, clamp) on the world that
///               will be Prev, then one more step: "housed-but-homeless".
///   COLONY    — the fed prefix, then settlement 0's buckets seeded with
///               unplaced-departure demand (the T4.4 test construction,
///               ColonizationTests.SeedDemand) and ONE step of the Colonization
///               system alone: Next carries a settlement that is not in Prev,
///               has people and provisions, and has NO HousingRow — the
///               A2-FIX D3 case, in which every explanation must still cite
///               honestly. Colonization alone because the full pipeline's
///               Migration rewrites UnplacedDeparture every turn
///               (MigrationSystem.cs:319) before Colonization consumes it.
/// </summary>
internal static class ExplainRigs
{
    public const ulong Seed = 42;
    public const int Target = 0;

    public static TurnExecutor Executor(SimConfig cfg, OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())), orders);
    }

    public static WorldState Found(SimConfig cfg) =>
        WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, Seed);

    /// <summary>Every world from founding (index 0) through <paramref name="turns"/> steps.</summary>
    public static (SimConfig Cfg, List<WorldState> Worlds) Fed(int turns)
    {
        SimConfig cfg = TestConfigs.Sim();
        TurnExecutor exec = Executor(cfg);
        var worlds = new List<WorldState>(turns + 1) { Found(cfg) };
        for (int t = 0; t < turns; t++) worlds.Add(exec.Step(worlds[^1]));
        return (cfg, worlds);
    }

    /// <summary>The batch that starves one settlement: no farming, no herding;
    /// construction kept at 10 so housing is maintained and Shelter cannot
    /// compete with Sustenance for the primary.</summary>
    public static OrderLog ZeroFoodOrders(int settlement)
    {
        double[] mix = [0.0, 0.0, 45.0, 45.0, 10.0];   // Farming..Construction
        var log = new OrderLog();
        for (int sector = 0; sector < Sectors.Count; sector++)
        {
            log.Append(new OrderRecord(
                Turn: 1, ActorId: 1, OrderKind.SectorAllocation,
                TargetId: settlement * 8 + sector, Amount: mix[sector]));
        }
        return log;
    }

    /// <summary>Steps until settlement <see cref="Target"/> shows a positive
    /// deficit, then TWO more turns (so the drawdown turn has a Next), or
    /// <paramref name="maxTurns"/>. Returns the index of the first world with a
    /// positive deficit, −1 if none appeared.</summary>
    public static (SimConfig Cfg, List<WorldState> Worlds, int FirstDeficit) Starved(int maxTurns)
    {
        SimConfig cfg = TestConfigs.Sim();
        OrderLog orders = ZeroFoodOrders(Target);
        WorldState founded = Found(cfg);
        OrderValidation.ValidateAgainstWorld(orders, founded);
        TurnExecutor exec = Executor(cfg, orders);
        var worlds = new List<WorldState>(maxTurns + 1) { founded };
        int first = -1;
        for (int t = 0; t < maxTurns; t++)
        {
            worlds.Add(exec.Step(worlds[^1]));
            if (first < 0 && Deficit(worlds[^1], Target) > 0.0) first = worlds.Count - 1;
            if (first >= 0 && worlds.Count - 1 >= first + 2) break;
        }
        return (cfg, worlds, first);
    }

    /// <summary>Fed prefix, then settlement 0's dwellings sunk to zero on the
    /// world that becomes Prev, then one step.</summary>
    public static (SimConfig Cfg, WorldState Prev, WorldState Next) Homeless(int prefixTurns)
    {
        (SimConfig cfg, List<WorldState> worlds) = Fed(prefixTurns);
        WorldState prev = worlds[^1];
        var ledger = new Ledger(prev.LedgerFlows);
        int h = -1;
        for (int i = 0; i < prev.Housing.Count; i++)
            if (prev.Housing[i].Settlement.Value == Target) { h = i; break; }
        Assert.True(h >= 0, "rig: settlement 0 has no housing row after the fed prefix");
        long dwellings = prev.Housing[h].Dwellings.Value;
        Assert.True(dwellings > 0, "rig vacuous: settlement 0 had no dwellings to remove");
        ledger.Flow(ref prev.Housing.Ref(h).Dwellings, ConservedQuantityIds.Dwellings,
            ReasonIds.HousingDecayed, dwellings, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
        Assert.Equal(0, prev.Housing[h].Dwellings.Value);
        WorldState next = Executor(cfg).Step(prev);
        return (cfg, prev, next);
    }

    /// <summary>Fed prefix, then a colony founded from settlement 0 by the
    /// Colonization system alone. Asserts the construction: exactly one new
    /// settlement, absent from Prev, populated, with no housing row on Next.</summary>
    public static (SimConfig Cfg, WorldState Prev, WorldState Next, SettlementId Colony) Colony(int prefixTurns)
    {
        (SimConfig cfg, List<WorldState> worlds) = Fed(prefixTurns);
        WorldState prev = worlds[^1];
        for (int i = 0; i < prev.Buckets.Count; i++)
            if (prev.Buckets[i].Settlement.Value == Target) prev.Buckets.Ref(i).UnplacedDeparture = 12.0;

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        var colonizationOnly = new TurnExecutor(
            EraTableLoader.Load(eraStream), [SystemCatalog.Colonization(cfg, TestConfigs.DevWorldgen())]);
        WorldState next = colonizationOnly.Step(prev);

        Assert.Equal(prev.Settlements.Count + 1, next.Settlements.Count);
        SettlementId colony = next.Settlements[^1].Id;
        for (int i = 0; i < prev.Settlements.Count; i++)
            Assert.NotEqual(colony, prev.Settlements[i].Id);
        long party = 0;
        for (int i = 0; i < next.Buckets.Count; i++)
            if (next.Buckets[i].Settlement == colony) party += next.Buckets[i].Count.Value;
        Assert.True(party > 0, "rig vacuous: the colony has nobody");
        for (int i = 0; i < next.Housing.Count; i++)
            Assert.NotEqual(colony, next.Housing[i].Settlement);   // ColonizationTests.NewSettlementGetsNoFreeHousing
        return (cfg, prev, next, colony);
    }

    public static bool HasSectorRow(WorldState w, int settlement)
    {
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement.Value == settlement) return true;
        return false;
    }

    public static double Deficit(WorldState w, int settlement)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement.Value == settlement) return w.ConsumptionDeficits[i].DeficitRatio;
        return 0.0;
    }

    public static double Grievance(WorldState w, int settlement, int cls)
    {
        for (int i = 0; i < w.Grievances.Count; i++)
            if (w.Grievances[i].Settlement.Value == settlement && w.Grievances[i].Class.Value == cls)
                return w.Grievances[i].Value;
        return double.NaN;
    }

    /// <summary>Every (settlement, class) that has a grievance row in <paramref name="w"/>, in table order.</summary>
    public static List<(SettlementId Settlement, ClassId Class)> GrievanceKeys(WorldState w)
    {
        var keys = new List<(SettlementId, ClassId)>();
        for (int i = 0; i < w.Grievances.Count; i++) keys.Add((w.Grievances[i].Settlement, w.Grievances[i].Class));
        return keys;
    }
}
