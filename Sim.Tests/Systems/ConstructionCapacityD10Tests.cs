using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D10 — "construction capacity is spent once": the MEASUREMENT the decision is conditional on.
/// </summary>
public class ConstructionCapacityD10Tests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42));
    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private static WorldState Solo() => DevSolo.Value.Clone();
    private static WorldState Duo() => DevDuo.Value.Clone();

    private static void Complete(WorldState w, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in nodeIds)
            w.ResearchCompleted.Add(new ResearchCompletedRow(polity, Research.Nodes[Research.IndexOfId(id)].Key));
    }

    private static void TopUp(WorldState w, SettlementId s, string good, long target)
    {
        var id = new GoodId(Cfg.Goods!.IdOf(good));
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have >= target) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id),
            ReasonIds.InitialEndowment, target - have, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    private static TurnExecutor Only(OrderLog orders, params SystemRegistration[] systems) =>
        new(ResearchRigs.FlatEra(10.0), systems, orders);

    // ------------------------------------------------------------------ ADR-033 D10 — measured, blocked on the schema

    [Fact]
    public void D10_Measured_TheLabourAProjectConsumes_IsAlsoBankedByPathBuild_InTheSameTurn()
    {
        // MEASUREMENT (ADR-033 D10, "if measurement confirms"): twin worlds, identical but for one
        // EnqueueConstruction order that ConstructionSystem resolves the same turn. PathBuild's bank grows by
        // EXACTLY the same amount in both, although the granary consumed laborRequired adult-years of the very
        // construction pool PathBuild accrues from: the labour is spent twice. The fix — ConstructionSystem
        // publishing the labour it used, which PathBuild subtracts as it subtracts HousingRow.LastLaborUsed —
        // needs a new serialized field (a CanonicalSchema change, outside this stream; reported). When it lands,
        // this test must FLIP: the ordered twin's bank must be lower by LaborPerAdultPerYear × laborRequired.
        WorldState w = Solo();
        SettlementId s0 = w.Settlements[0].Id;
        foreach (string good in new[] { "timber", "stone" }) TopUp(w, s0, good, 1_000);
        var ordered = new OrderLog();
        ordered.Append(ConstructionQuery.EnqueueOrder(w, Player, s0, 1));
        SystemRegistration[] pipeline = [SystemCatalog.Construction(Cfg), SystemCatalog.PathBuild(Cfg)];
        WorldState built = Only(ordered, pipeline).Step(w);
        WorldState twin = Only(new OrderLog(), pipeline).Step(w);

        Assert.Equal(1L, ConstructionQuery.Built(built, s0, 1));                 // the project WAS built this turn
        Assert.Equal(0L, ConstructionQuery.Built(twin, s0, 1));
        double capacity = ConstructionQuery.CapacityAdultYears(w, s0, 10.0);
        Assert.True(capacity >= Cfg.Goods!.ProjectById(1)!.LaborRequired);    // from the same construction pool
        Assert.True(WorldStates.TableEquals(built.PathProgress, twin.PathProgress)); // ...which PathBuild banked in full
        int row = -1;
        for (int i = 0; i < built.PathProgress.Count; i++) if (built.PathProgress[i].Settlement == s0) row = i;
        Assert.True(row >= 0, "PathBuild banked nothing — the measurement would be vacuous");
    }
}
