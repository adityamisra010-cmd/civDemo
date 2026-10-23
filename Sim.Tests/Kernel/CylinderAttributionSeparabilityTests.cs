using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rig #15b) — THE BEHAVIOURAL SEPARABILITY FACTS proven only on the flat world
/// until now, as TWO-RUN STRIP EQUALITIES on the SHIPPED CYLINDER (review F7-INVENTORY-INCOMPLETE). IntegratedPinAttributionTests
/// proves three layers write only their own rows by stripping those rows from a FLAT world and comparing against a historical
/// literal (FoundedGoldenSeed42Turn300_MovedForTheResearchLayerAlone :1050, the governance layer :1248/:1268, the Age layer
/// :1148/:1167). A literal is a flat measurement and cannot be re-measured on the cylinder, so the cylinder twin compares two
/// LIVE runs instead: the same shipped worldgen (topology.wrapX), the same seed and orders, one with the layer acting and one
/// without; strip the layer's own footprint from BOTH worlds (a Clone each) and the two must hash equal on EVERY turn, and the
/// tables that differ must be the layer's own. Both runs use IntegratedPinAttributionTests' Unarmed() config (the PreR1 chain
/// with the disaster hazard pinned inert) — the config under which the flat facts are stated, because the R1/R2 unlock layers
/// make research a gameplay input by design.
/// <list type="bullet">
/// <item><b>Research non-leak</b>: a research-driven arm (a SetResearchTarget for the cheapest available node whenever the
///   player has none — the IntegratedPinAttributionTests driver) against the order-less (founded) or driven arm. Vacuity:
///   research completes a node inside the horizon (MEASURED: first completion in world 11).</item>
/// <item><b>The governance reach</b>: sim.json governance present against absent (GovernanceSystem is inert without it).
///   Its footprint is the TaxPolicies rows and the reach it writes into ControlRow.Strength (restored to the 1.0 founding
///   wrote). Vacuity: a computed reach below 1 (MEASURED: 11 of 12 control rows in world 1 — the reach reads the road-aware
///   distance from the capital).</item>
/// <item><b>The Age layer</b>: ages.json present against absent (the Age systems are inert without it). Its footprint is
///   the five Age/military tables. Vacuity: the eligibility row it publishes (MEASURED: one row in world 1, none without).</item>
/// </list>
/// The flat facts are unchanged. Horizons: the first action + 2 (design §2 budget). SEAM EXPOSURE (MEASURED): 0 seam-crossing
/// pairs on both worlds; of the three only the governance reach reads a distance (the capital's travel cost, through the
/// catchment's wrapped Pathfinder), and it is not seam-exposed here — recorded in D-055 per feature.
/// </summary>
public class CylinderAttributionSeparabilityTests : IClassFixture<CylinderAttributionSeparabilityTests.Worlds>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/s2/probe-a.txt, B1/B2), on both
    // the founded and the driven cylinder: the driver's first target is set at turn 2 and the first node completes in
    // world 11; through world 14 the two arms differ only in ResearchCompleted, ResearchProgress and ResearchTargets.
    // Governance absent: the arms differ only in Controls (worlds 1–4); 11 control rows carry a reach below 1 in world 1
    // ON, none OFF. Ages absent: the arms differ only in AgeEligibility (worlds 1–4): one row ON, none OFF.
    // ---------------------------------------------------------------------------------------------------------------
    private const int ResearchFrom = 2;
    private const int FirstCompletion = 11;
    private const int ResearchHorizon = FirstCompletion + 2;
    private const int LayerFirstAction = 1;
    private const int LayerHorizon = LayerFirstAction + 2;
    private const int ReachBelowOne = 11;

    private static readonly string[] ResearchTables =
    [
        "ResearchCompleted", "ResearchCostModifiers", "ResearchCredits", "ResearchEurekas", "ResearchExposures",
        "ResearchProgress", "ResearchTargets",
    ];
    private static readonly string[] GovernanceTables = ["Controls", "TaxPolicies"];
    private static readonly string[] AgeTables = ["AgeEligibility", "AgeStates", "AgeTransitions", "MilitaryUnits", "UnitConversions"];

    /// <summary>IntegratedPinAttributionTests.Unarmed(): the PreR1 chain (recipe and trade knowledge, R2b and every newer
    /// layer stripped) with the disaster hazard inert.</summary>
    internal static SimConfig Unarmed()
    {
        SimConfig s = TestConfigs.PreRecipeKnowledge(TestConfigs.PreTradeKnowledge(TestConfigs.PreR2b(TestConfigs.PreM6(TestConfigs.Sim()))));
        Assert.Equal(0.0, s.Disaster.HazardPerYear);
        return s with { Disaster = s.Disaster with { HazardPerYear = 0.0 } };
    }

    /// <summary>The shared plain arms: the founded and the driven cylinder under Unarmed(), no research order, to the
    /// research horizon (their prefixes are the ON arms of the governance and Age facts).</summary>
    public sealed class Worlds
    {
        public SimConfig Cfg { get; } = Unarmed();
        public WorldState[] Founded { get; }
        public WorldState[] Driven { get; }

        public Worlds()
        {
            Founded = Run(Cfg, driven: false, research: false, ResearchHorizon);
            Driven = Run(Cfg, driven: true, research: false, ResearchHorizon);
        }

        public WorldState[] Plain(bool driven) => driven ? Driven : Founded;
    }

    private readonly Worlds _worlds;

    public CylinderAttributionSeparabilityTests(Worlds worlds) => _worlds = worlds;

    /// <summary>Founds the canonical world on the shipped cylinder under <paramref name="cfg"/> and steps it
    /// <paramref name="horizon"/> turns through the production pipeline, with the driven golden's orders when
    /// <paramref name="driven"/>, and the research driver when <paramref name="research"/> (after the turn's player batch).</summary>
    internal static WorldState[] Run(SimConfig cfg, bool driven, bool research, int horizon)
    {
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        Assert.True(w.Terrain!.WrapX);
        OrderLog player = driven ? DrivenGoldenTests.DrivingOrders(w.Settlements.Count) : new OrderLog();
        var log = new OrderLog();
        TurnExecutor exec = R.Pipeline(cfg, TestConfigs.Worldgen(), log);
        var polity = new PolityId(1);
        var worlds = new WorldState[horizon + 1];
        worlds[0] = w;
        for (int t = 1; t <= horizon; t++)
        {
            OrderBatch batch = player.BatchFor(w.Clock.Turn);
            for (int b = 0; b < batch.Count; b++) log.Append(batch[b]);
            if (research && w.Clock.Turn >= ResearchFrom && !ResearchQuery.TryGetTarget(w, polity, out _)
                && ResearchQuery.CheapestAvailable(w, cfg.Research!, polity) is { } pick)
                log.Append(OrderRecord.From(w.Clock.Turn, polity, OrderKind.SetResearchTarget, pick.Value, 0.0));
            w = exec.Step(w);
            worlds[t] = w;
        }
        return worlds;
    }

    /// <summary>The world's hash with <paramref name="strip"/> applied to a Clone (the world is untouched).</summary>
    private static string StrippedHash(WorldState world, Func<WorldState, int> strip, out int removed)
    {
        WorldState c = world.Clone();
        removed = strip(c);
        return WorldHash.ComputeHex(c);
    }

    private static int StripResearch(WorldState w)
    {
        int n = w.ResearchTargets.Count + w.ResearchProgress.Count + w.ResearchCompleted.Count + w.ResearchEurekas.Count
            + w.ResearchCostModifiers.Count + w.ResearchCredits.Count + w.ResearchExposures.Count;
        w.ResearchTargets.Clear();
        w.ResearchProgress.Clear();
        w.ResearchCompleted.Clear();
        w.ResearchEurekas.Clear();
        w.ResearchCostModifiers.Clear();
        w.ResearchCredits.Clear();
        w.ResearchExposures.Clear();
        return n;
    }

    private static int StripGovernance(WorldState w)
    {
        int n = w.TaxPolicies.Count;
        w.TaxPolicies.Clear();
        for (int i = 0; i < w.Controls.Count; i++)
        {
            ControlRow row = w.Controls[i];
            if (BitConverter.DoubleToInt64Bits(row.Strength) == BitConverter.DoubleToInt64Bits(1.0)) continue;
            w.Controls[i] = row with { Strength = 1.0 };
            n++;
        }
        return n;
    }

    private static int StripAges(WorldState w)
    {
        int n = w.AgeStates.Count + w.AgeEligibility.Count + w.AgeTransitions.Count + w.MilitaryUnits.Count + w.UnitConversions.Count;
        w.AgeStates.Clear();
        w.AgeEligibility.Clear();
        w.AgeTransitions.Clear();
        w.MilitaryUnits.Clear();
        w.UnitConversions.Clear();
        return n;
    }

    /// <summary>The template: on every turn to <paramref name="horizon"/> the two arms differ only in the layer's own tables,
    /// and with the layer's footprint stripped from both they are the same world.</summary>
    private static void AssertSeparable(WorldState[] withLayer, WorldState[] without, int horizon, string[] layerTables,
        Func<WorldState, int> strip, string layer)
    {
        for (int t = 0; t <= horizon; t++)
        {
            foreach (string table in R.WriteSet(withLayer[t], without[t]))
                Assert.True(Array.IndexOf(layerTables, table) >= 0, $"{layer}: world {t} differs in {table}, which the layer does not own");
            Assert.True(StrippedHash(withLayer[t], strip, out _) == StrippedHash(without[t], strip, out _),
                $"{layer}: world {t} differs once the layer's own rows are stripped from both arms");
        }
    }

    /// <summary>Determinism: the shared plain arms are reproduced by fresh runs, turn by turn; and the worlds have no
    /// seam exposure.</summary>
    [Fact]
    public void ThePlainArms_AreDeterministic_AndSeamFree()
    {
        foreach (bool driven in new[] { false, true })
        {
            WorldState[] plain = _worlds.Plain(driven), again = Run(Unarmed(), driven, research: false, ResearchHorizon);
            for (int t = 0; t <= ResearchHorizon; t++)
                Assert.True(WorldHash.ComputeHex(plain[t]) == WorldHash.ComputeHex(again[t]), $"driven={driven}: a second run differs at turn {t}");
            Assert.Equal((0, 0), (R.SeamCrossingPairs(plain[0]), R.WrappedDonorOrders(plain[0])));
        }
    }

    /// <summary>#15b — RESEARCH NON-LEAK on the cylinder (the flat fact: IntegratedPinAttributionTests :1050 / :1114).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResearchNonLeak_OnTheShippedCylinder_ResearchWritesOnlyItsOwnTables(bool driven)
    {
        SimConfig cfg = _worlds.Cfg;
        WorldState[] plain = _worlds.Plain(driven);
        WorldState[] researched = Run(cfg, driven, research: true, ResearchHorizon);
        int first = -1;
        for (int t = 0; t <= ResearchHorizon && first < 0; t++) if (researched[t].ResearchCompleted.Count > 0) first = t;
        Assert.Equal(FirstCompletion, first);                                     // vacuity: research completes a node
        Assert.Equal(0, plain[ResearchHorizon].ResearchCompleted.Count);
        StrippedHash(plain[ResearchHorizon], StripResearch, out int plainRows);
        StrippedHash(researched[ResearchHorizon], StripResearch, out int researchRows);
        Assert.Equal(0, plainRows);
        Assert.True(researchRows > 0, "no research row to strip — the fact would be vacuous");
        AssertSeparable(researched, plain, ResearchHorizon, ResearchTables, StripResearch, "research");
        Assert.Equal((0, 0), (R.SeamCrossingPairs(plain[0]), R.WrappedDonorOrders(plain[0])));
    }

    /// <summary>#15b — THE GOVERNANCE REACH on the cylinder (the flat facts: IntegratedPinAttributionTests :1248 / :1268).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GovernanceReach_OnTheShippedCylinder_TheGoverningLoopWritesOnlyReachAndPolicy(bool driven)
    {
        SimConfig cfg = _worlds.Cfg;
        Assert.NotNull(cfg.Governance);
        WorldState[] on = _worlds.Plain(driven);
        WorldState[] off = Run(cfg with { Governance = null }, driven, research: false, LayerHorizon);
        Assert.Equal(ReachBelowOne, ReachBelow1(on[LayerFirstAction]));          // vacuity: a computed reach below 1
        Assert.Equal(0, ReachBelow1(off[LayerFirstAction]));
        Assert.Equal(0, on[LayerHorizon].TaxPolicies.Count);                    // no levy: reach is the whole footprint
        AssertSeparable(on, off, LayerHorizon, GovernanceTables, StripGovernance, "governance");
    }

    /// <summary>#15b — THE AGE LAYER on the cylinder (the flat facts: IntegratedPinAttributionTests :1148 / :1167).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AgeLayer_OnTheShippedCylinder_TheAgeSystemsWriteOnlyTheirOwnRows(bool driven)
    {
        SimConfig cfg = _worlds.Cfg;
        Assert.NotNull(cfg.Ages);
        WorldState[] on = _worlds.Plain(driven);
        WorldState[] off = Run(cfg with { Ages = null }, driven, research: false, LayerHorizon);
        Assert.Equal(1, on[LayerFirstAction].AgeEligibility.Count);              // vacuity: the layer publishes its row
        Assert.Equal(0, off[LayerFirstAction].AgeEligibility.Count);
        AssertSeparable(on, off, LayerHorizon, AgeTables, StripAges, "ages");
    }

    private static int ReachBelow1(IReadOnlyWorldState w)
    {
        int n = 0;
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Strength < 1.0) n++;
        return n;
    }
}
