using Sim.Core.Kernel;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 "Observability samples") — THE EXPLAIN STARVED RIG ON THE DEV CYLINDER, an
/// ADDITIVE twin of <see cref="ExplainRigs.Starved"/>. That rig is one of the four D-054 §7 "kept flat" rigs: it is a
/// batch-2 layer twin (the per-settlement store, TestConfigs.PreEmpireFoodAllocation), and by the R4 rule it founds flat,
/// so every explanation pin of ExplainGrievanceTests / ExplainRecomputedFunctionTests / ExplainHappinessMigrationTests is a
/// flat measurement and stays one. This class runs the SAME rig — the same config, the same STARVED orders (settlement 0 to
/// zero farming and herding at turn 1), the same seed — on the dev preset AS SHIPPED (worldgen.json topology.wrapX, 256 px),
/// with its OWN measured pins: the T1.9 delivery turn of the order (the row lands in world 2; the first zero harvest and the
/// first positive deficit are world 3 — the cylinder keeps the flat rig's turn), the drawdown deficit bit-exact, the grain
/// fill the chain reports (the cylinder's target carries a different population into the drawdown: 472 eaten where the flat
/// rig eats 456), and the explanation's shape (Sustenance primary, harvest 0 below eaten, the food factor 1 − deficit, the
/// deficit read off Prev for the grievance chain and off Next for happiness).
/// </summary>
public class CylinderExplainStarvedSampleTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/s2/probe-a.txt, D2): the cylinder
    // starved rig's batch is not in force in world 1 and is in world 2; the first positive deficit is world 3 =
    // 0.7842608482471194 (the batch-3 recon measured the same value, f7-flat-only §5 P5a; the flat rig's is
    // 0.78818337827898999); world 4's deficit is 0.9177539608574091; on the drawdown turn the target ate 472 grain of 3,671
    // demanded; the peasants' grievance names Sustenance primary.
    // ---------------------------------------------------------------------------------------------------------------
    private const int FirstDeficit = 3;
    private const double DeficitAtFirst = 0.7842608482471194;
    private const double DeficitNext = 0.9177539608574091;
    private const long Eaten = 472, Demanded = 3_671;
    private const int Peasant = 1;
    private static readonly SettlementId Target = new(ExplainRigs.Target);

    /// <summary><see cref="ExplainRigs.Starved"/> founded on the shipped dev CYLINDER: every world from founding to the first
    /// positive deficit + 2.</summary>
    private static (SimConfig Cfg, List<WorldState> Worlds, int First) StarvedOnTheCylinder(int maxTurns)
    {
        SimConfig cfg = TestConfigs.PreEmpireFoodAllocation(TestConfigs.Sim());
        OrderLog orders = ExplainRigs.ZeroFoodOrders(ExplainRigs.Target);
        WorldState founded = ExplainRigs.Found(cfg, TestConfigs.DevWorldgen());
        Assert.True(founded.Terrain!.WrapX, "the dev preset as shipped is the cylinder");
        OrderValidation.ValidateAgainstWorld(orders, founded);
        TurnExecutor exec = ExplainRigs.Executor(cfg, orders);
        var worlds = new List<WorldState>(maxTurns + 1) { founded };
        int first = -1;
        for (int t = 0; t < maxTurns; t++)
        {
            worlds.Add(exec.Step(worlds[^1]));
            if (first < 0 && ExplainRigs.Deficit(worlds[^1], ExplainRigs.Target) > 0.0) first = worlds.Count - 1;
            if (first >= 0 && worlds.Count - 1 >= first + 2) break;
        }
        return (cfg, worlds, first);
    }

    [Fact]
    public void StarvedOnTheCylinder_TheOrderLandsTurnExact_AndTheDrawdownIsExplainedAsOnTheFlatRig_WithItsOwnValues()
    {
        (SimConfig cfg, List<WorldState> worlds, int first) = StarvedOnTheCylinder(12);

        // The T1.9 delivery semantic, on the cylinder: stamped turn 1, in force from world 2, read by production in 2 → 3.
        Assert.False(ExplainRigs.HasSectorRow(worlds[1], ExplainRigs.Target), "the Turn-1 batch must not be in force in world 1");
        Assert.True(ExplainRigs.HasSectorRow(worlds[2], ExplainRigs.Target), "the Turn-1 batch must land in world 2");
        Assert.Equal(FirstDeficit, first);
        WorldState prev = worlds[first], next = worlds[first + 1];
        Assert.Equal(DeficitAtFirst, ExplainRigs.Deficit(prev, ExplainRigs.Target));
        Assert.Equal(DeficitNext, ExplainRigs.Deficit(next, ExplainRigs.Target));

        // The grievance explanation: Sustenance primary, the chain reading the zero harvest below what was eaten.
        GrievanceExplanation g = GrievanceExplanation.For(prev, next, cfg, Target, new ClassId(Peasant));
        Assert.True(g.Total > 0.0 && g.Delta > 0.0, $"grievance {g.Total} (Δ {g.Delta}) — nothing to explain");
        Assert.Equal(BasketBook.SustenanceNeedId, g.PrimaryNeedId);
        Link harvest = ExplainGrievanceTests.Single(g.PrimaryChain!.Links, ChainNode.GrainHarvest);
        Link eaten = ExplainGrievanceTests.Single(g.PrimaryChain.Links, ChainNode.GrainEaten);
        Assert.Equal(0.0, harvest.Value);
        Assert.True(harvest.Value < eaten.Value);

        // The fill the system read, and the chain's quotient of its two READ links.
        var grain = new GoodId(cfg.Goods!.GrainId);
        int row = GoodStockIndex.IndexOf(prev.GoodStocks, Target, grain);
        Assert.Equal((Eaten, Demanded), (prev.GoodStocks[row].LastConsumptionEatenUnits, prev.GoodStocks[row].LastConsumptionDemandUnits));
        GoodStockRow stock = prev.GoodStocks[row];
        Assert.Equal((double)Eaten / Demanded, NeedsGrievanceSystem.Fill(in stock));

        // Happiness reads the deficit off Next; the grievance chain off Prev — each its own world's value.
        HappinessExplanation h = HappinessExplanation.For(next, cfg, Target);
        Assert.Equal(1.0 - DeficitNext, h.Factors[(int)SettlementHappiness.Factor.Food].Value);
        CausalChain chain = CausalChain.ForNeed(prev, next, cfg, Target, new ClassId(Peasant), BasketBook.SustenanceNeedId);
        Link gd = ExplainGrievanceTests.Single(chain.Links, ChainNode.DeficitRatio);
        Assert.Equal(SourceWorld.Prev, gd.World);
        Assert.Equal(DeficitAtFirst, gd.Value);
    }
}
