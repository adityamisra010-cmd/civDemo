using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D10 — "construction capacity is spent once". The decision was conditional on a measurement (the
/// labour a project consumed was also banked by PathBuild in the same turn); schema v31 lands the fix, and the
/// measurement is FLIPPED to pin the single spend and its null arm.
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

    // ------------------------------------------------------------------ ADR-033 D10 — the single spend (schema v31)

    [Fact]
    public void D10_TheLabourAProjectConsumes_IsPublished_AndPathBuildBanksItOnlyOnce()
    {
        // FLIPPED (ADR-033 D10, schema v31). Before v31 this test MEASURED the defect: twin worlds identical but for
        // one EnqueueConstruction order that ConstructionSystem resolved the same turn banked EXACTLY the same
        // PathBuild labour, although the granary consumed laborRequired adult-years of the very construction pool
        // PathBuild accrues from — the labour was spent twice. Now ConstructionSystem PUBLISHES what it consumed
        // (ConstructionLaborRow, rebuilt every step) and PathBuild subtracts it at the standard one-turn lag, exactly
        // as it subtracts HousingRow.LastLaborUsed: in the step after the build the ordered twin banks
        // LaborPerAdultPerYear × laborRequired LESS than its order-free twin, and nothing else differs.
        WorldState w = Solo();
        SettlementId s0 = w.Settlements[0].Id;
        foreach (string good in new[] { "timber", "stone" }) TopUp(w, s0, good, 1_000);
        var ordered = new OrderLog();
        ordered.Append(ConstructionQuery.EnqueueOrder(w, Player, s0, 1));
        SystemRegistration[] pipeline = [SystemCatalog.Construction(Cfg), SystemCatalog.PathBuild(Cfg)];
        double laborRequired = Cfg.Goods!.ProjectById(1)!.LaborRequired;
        Assert.True(ConstructionQuery.CapacityAdultYears(w, Cfg, s0, 10.0) >= laborRequired);   // affordable from the pool

        // Step 1: the granary is built and its labour PUBLISHED; PathBuild's bank is unchanged this step (the lag).
        WorldState built = Only(ordered, pipeline).Step(w);
        WorldState twin = Only(new OrderLog(), pipeline).Step(w);
        Assert.Equal(1L, ConstructionQuery.Built(built, s0, 1));
        Assert.Equal(0L, ConstructionQuery.Built(twin, s0, 1));
        Assert.Equal([new ConstructionLaborRow(s0, laborRequired)], Rows(built.ConstructionLabor));
        Assert.Equal(0, twin.ConstructionLabor.Count);                            // no build, no row
        Assert.True(WorldStates.TableEquals(built.PathProgress, twin.PathProgress));
        Assert.True(Bank(built, s0) > 0.0, "PathBuild banked nothing — the measurement would be vacuous");

        // Step 2: PathBuild subtracts the published labour once; the table is rebuilt (empty: nothing built).
        WorldState after = Only(new OrderLog(), pipeline).Step(built);
        WorldState twinAfter = Only(new OrderLog(), pipeline).Step(twin);
        Assert.Equal(0, after.ConstructionLabor.Count);
        Assert.True(WorldStates.TableEquals(after.NetworkEdges, twinAfter.NetworkEdges));   // no segment laid differently
        double spentOnce = Cfg.PathBuild.LaborPerAdultPerYear * laborRequired;
        Assert.Equal(Bank(twinAfter, s0) - spentOnce, Bank(after, s0), 12);
        Assert.True(Bank(after, s0) < Bank(twinAfter, s0));
        // Every other settlement banks identically (the subtraction is per settlement).
        for (int i = 1; i < w.Settlements.Count; i++)
            Assert.Equal(Bank(twinAfter, w.Settlements[i].Id), Bank(after, w.Settlements[i].Id));
    }

    [Fact]
    public void D10_AWorldThatBuildsNothing_PublishesNoRow_AndPathBuildBanksExactlyAsBefore()
    {
        // The null arm, bit for bit: with no completed project the table stays empty and PathBuild skips the
        // subtraction entirely, so every golden world (none issues EnqueueConstruction) is unmoved by D10.
        WorldState w = Solo();
        SystemRegistration[] pipeline = [SystemCatalog.Construction(Cfg), SystemCatalog.PathBuild(Cfg)];
        WorldState next = Only(new OrderLog(), pipeline).Step(w);
        Assert.Equal(0, next.ConstructionLabor.Count);
        WorldState control = Only(new OrderLog(), [SystemCatalog.PathBuild(Cfg)]).Step(w);
        Assert.True(WorldStates.TableEquals(next.PathProgress, control.PathProgress));
    }

    private static ConstructionLaborRow[] Rows(Table<ConstructionLaborRow> t)
    {
        var rows = new ConstructionLaborRow[t.Count];
        for (int i = 0; i < t.Count; i++) rows[i] = t[i];
        return rows;
    }

    private static double Bank(WorldState w, SettlementId s)
    {
        for (int i = 0; i < w.PathProgress.Count; i++) if (w.PathProgress[i].Settlement == s) return w.PathProgress[i].Banked;
        return 0.0;
    }
}
