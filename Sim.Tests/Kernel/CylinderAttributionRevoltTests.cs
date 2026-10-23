using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using R = Sim.Tests.TestUtil.CylinderAttributionRig;

namespace Sim.Tests.Kernel;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2 rig #13 and the DECAY observability sample) — REVOLT AND ORDER REFUSAL
/// (ADR-033 D2; R3) turn-exactly on the SHIPPED CYLINDER, and the dwelling-decay flow reconciled on it.
///
/// THE RIG. The canonical founded world (seed 42, 1024 px, worldgen.json as shipped: topology.wrapX), the shipped config,
/// stepped exactly as `sim run --founded --seed 42 --orders <player log>` steps it: CliRevoltPolityProducerTests' CLI player
/// log (FoundedHarness's labour swings on settlement 0 — 60, 0, 80, 30, 100, 45 % issued at 3, 33, 63, 93, 123, 153 — with
/// every sibling's food labour abandoned with the 0 % swing, so the polity has no spare grain and relief cannot rescue
/// settlement 0), the AI producer appended after the player's batch every turn (<see cref="R.Revolt1024"/>). Revolt and
/// refusal have NO SWITCH, so the rig is a TWIN PAIR (two fresh runs, identical every turn) whose turn-exact pins a CODE
/// mutant must break (design §2 / review F7-PER-RIG-UNDERSPECIFIED): one that skips the refusal (the 80 % order would land
/// at 64), and one that delivers the orders a turn late (the 60 % order would not be in force at 4).
/// The flat leg (FoundedHarnessTests.FoundedOrderedTwin_HashIdentical_EveryTurn, the per-settlement-store twin on the flat
/// world) is unchanged; it is one of the four D-054 §7 "kept flat" rigs this class gives a cylinder twin.
///
/// THE DECAY SAMPLE. WorldReconciliationTests' positive dwelling-decay sample runs on the flat per-settlement-store driven
/// world (ObservedWorlds.DrivenPerSettlementStores300: on the cylinder no driven run decays in 300 turns, MEASURED). This rig
/// does decay on the cylinder — the polity's abandoned food labour empties homes — so its ON run is observed exactly as
/// UiSession.EndTurn and `sim inspect --telemetry` observe a step, and every turn's accounts are reconciled with the decay
/// flow positive from its measured first turn. The flat sample stays; this is its additive cylinder twin.
///
/// SEAM EXPOSURE (MEASURED): 0 seam-crossing pairs and 0 wrapped donor orders at world 0; revolt and refusal read no
/// distance (refusal is control, not geography).
/// </summary>
public class CylinderAttributionRevoltTests : IClassFixture<CylinderAttributionRevoltTests.Arms>
{
    // ---------------------------------------------------------------------------------------------------------------
    // MEASURED (2026-10-16, D-055; batch 4 track T2 step 2) on base-b3 + the frozen T1a patch (pinned tree 48e7c13b…,
    // patch sha256 01179819…), Release, by the agent writing this block (probe log t2-out/s2/probe-a.txt, C1; the same with
    // and without the AI producer): settlement 0 is ruled by the player in world 36 and by a new AI polity (2) in world 37;
    // the farming share in force at the six landings (issue + 1) is 0.6 at 4, 0 at 34, and 0 at 64, 94, 124 and 154 — the
    // 80, 30, 100 and 45 % orders land after the revolt and are refused; the new polity's first order is stamped 37; two fresh
    // runs are hash-identical on every turn; the first dwelling decays in world 15. The CLI's own pin of the revolt turn is
    // CliRevoltPolityProducerTests.RevoltTurn = 37 (the same log, the same loop).
    // ---------------------------------------------------------------------------------------------------------------
    private const int RevoltTurn = 37;
    private const int RevoltPolity = 2;
    private const int LastLanding = 154;
    private const int Horizon = LastLanding + 2;
    private const int FirstDecay = 15;
    private static readonly int[] Landings = [4, 34, 64, 94, 124, 154];
    private static readonly double[] FarmingInForce = [0.6, 0.0, 0.0, 0.0, 0.0, 0.0];

    /// <summary>The ON arm (observed every step) and its twin.</summary>
    public sealed class Arms
    {
        public SimConfig Cfg { get; } = TestConfigs.PreM6(TestConfigs.Sim());
        public R.Arm Live { get; }
        public R.Arm Twin { get; }
        public ObservationLog Observed { get; } = new();

        public Arms()
        {
            int[] keep = [0, RevoltTurn - 1, RevoltTurn, 3, 33, 63, 93, 123, 153, .. Landings];
            Live = R.Run(R.Revolt1024(), "on", Cfg, Horizon, keep: keep,
                observe: (prev, next, log) => Observed.Observe(prev, next, Cfg, OrderApplied.For(log, prev.Clock.Turn)));
            Twin = R.Run(R.Revolt1024(), "twin", TestConfigs.PreM6(TestConfigs.Sim()), Horizon);
        }
    }

    private readonly Arms _arms;

    public CylinderAttributionRevoltTests(Arms arms) => _arms = arms;

    private static double Farming0(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement.Value == 0) return w.SectorAllocations[i].Farming;
        return -1.0;
    }

    /// <summary>#13 — the revolt and the refusal of the former ruler's later orders, turn-exact, on the twin pair.</summary>
    [Fact]
    public void Revolt_OnTheShippedCylinder_TheRevoltedSettlementRefusesItsFormerRulersLaterOrders_TurnExact()
    {
        R.Arm live = _arms.Live;
        var s0 = new SettlementId(0);
        var player = new PolityId(1);
        WorldState w0 = live.At(0);
        Assert.True(w0.Terrain!.WrapX);
        Assert.Equal((0, 0), (R.SeamCrossingPairs(w0), R.WrappedDonorOrders(w0)));
        Assert.True(_arms.Cfg.Consumption.EmpireFoodAllocation);                      // relief is ON: the log starves the polity

        // The revolt, at its measured turn (read off Controls, never a hash): the player rules s0 in world 36, a NEW AI
        // polity rules it in world 37.
        Assert.True(EmpireQuery.ControlsSettlement(live.At(RevoltTurn - 1), player, s0));
        Assert.True(EmpireQuery.TryGetController(live.At(RevoltTurn), s0, out PolityId ruler));
        Assert.Equal(RevoltPolity, ruler.Value);
        Assert.True(EmpireQuery.TryGetCommandSource(live.At(RevoltTurn), ruler, out CommandSource source) && source == CommandSource.Ai);
        Assert.Equal((long)RevoltTurn, FirstOrderOf(live.Log, RevoltPolity));        // the CLI loop drives the new polity at once

        // The orders: each swing is in the log at its issue turn, and lands on issue + 1 (the T1.9 delivery semantic): the
        // two before the revolt are OBEYED on their landing turn, the four after it are REFUSED — the share in force stays
        // the last obeyed one, and the issuer no longer controls the settlement on the PREV of their delivery.
        double[] pcts = FoundedHarnessTests.SessionPcts;
        for (int k = 0; k < Landings.Length; k++)
        {
            int issued = Landings[k] - 1;
            Assert.True(HasLabourOrder(live.Log, issued, pcts[k]), $"the {pcts[k]} % swing is not in the log at turn {issued}");
            Assert.Equal(FarmingInForce[k], Farming0(live.At(Landings[k])));
            bool afterRevolt = Landings[k] > RevoltTurn;
            Assert.Equal(!afterRevolt, LabourActivities.CanAllocate(live.At(issued), player, s0));   // the delivery's PREV
            if (afterRevolt) Assert.NotEqual(pcts[k] / 100.0, Farming0(live.At(Landings[k])));   // vacuity: obeying would show
        }

        // Turn-exact the other way too: before an obeyed order's landing the share it replaces is still in force.
        Assert.NotEqual(FarmingInForce[0], Farming0(live.At(Landings[0] - 1)));   // the 60 % order is not in force at 3
        Assert.Equal(FarmingInForce[0], Farming0(live.At(Landings[1] - 1)));      // the 0 % order is not in force at 33

        // THE TWIN PAIR: a second fresh run is the same world on every turn.
        R.AssertSameSeries(live, _arms.Twin);
    }

    /// <summary>The decay observability sample on the cylinder: every turn's accounts close exactly through their named
    /// reasons, and the dwelling-decay flow is zero before its measured first turn and positive on it.</summary>
    [Fact]
    public void TheDecaySample_OnTheRevoltCylinder_ReconcilesEveryTurn_AndDecayFirstAppearsAtItsMeasuredTurn()
    {
        ObservationLog log = _arms.Observed;
        Assert.Equal(Horizon, log.Observations.Count);
        long decayTurns = 0, decayed = 0;
        for (int i = 0; i < log.Observations.Count; i++)
        {
            TurnRecord t = log.Observations[i].Turn;
            Assert.Equal(i + 1, t.Turn);
            for (int s = 0; s < t.Stocks.Length; s++)
            {
                StockRecord st = t.Stocks[s];
                Assert.True(st.Reconciles, $"turn {t.Turn}: {st.Name} discrepancy {st.Discrepancy}");
                long sourced = 0, sunk = 0;
                for (int k = 0; k < st.Sources.Length; k++) sourced += st.Sources[k].Units;
                for (int k = 0; k < st.Sinks.Length; k++) sunk += st.Sinks[k].Units;
                Assert.Equal(st.Closing, st.Opening + sourced - sunk);
            }
            Assert.True(t.Population.Reconciles, $"turn {t.Turn}: population discrepancy {t.Population.Discrepancy}");
            Assert.True(t.Grain.Reconciles, $"turn {t.Turn}: grain discrepancy {t.Grain.Discrepancy}");
            Assert.True(t.Dwellings.Reconciles, $"turn {t.Turn}: dwellings discrepancy {t.Dwellings.Discrepancy}");
            for (int g = 0; g < t.Goods.Length; g++)
                Assert.True(t.Goods[g].Reconciles, $"turn {t.Turn}: {t.Goods[g].Name} discrepancy {t.Goods[g].Discrepancy}");
            Assert.Equal(t.Population.Closing,
                t.Population.Opening + t.Population.Births - t.Population.NaturalDeaths - t.Population.Starvation);
            Assert.Equal(t.Grain.Closing,
                t.Grain.Opening + t.Grain.Endowment + t.Grain.Harvest - t.Grain.Eaten - t.Grain.Spoilage - t.Grain.Overflow);
            Assert.Equal(t.Dwellings.Closing, t.Dwellings.Opening + t.Dwellings.Built - t.Dwellings.Decayed);
            if (t.Turn < FirstDecay) Assert.Equal(0, t.Dwellings.Decayed);
            if (t.Dwellings.Decayed > 0) { decayTurns++; decayed += t.Dwellings.Decayed; }
        }
        Assert.True(log.At(FirstDecay)!.Turn.Dwellings.Decayed > 0, $"no dwelling decay on turn {FirstDecay}");
        Assert.Equal((DecayTurns, DwellingsDecayed), (decayTurns, decayed));
    }

    // MEASURED (2026-10-16, D-055; T2 step 2, this rig's 156 turns): decay in 19 of the 156 turns, 133 dwellings in all.
    private const long DecayTurns = 19;
    private const long DwellingsDecayed = 133;

    private static long FirstOrderOf(OrderLog log, int actor)
    {
        for (int i = 0; i < log.Count; i++) if (log[i].ActorId == actor) return log[i].Turn;
        return -1;
    }

    private static bool HasLabourOrder(OrderLog log, long turn, double pct)
    {
        for (int i = 0; i < log.Count; i++)
            if (log[i].Turn == turn && log[i].ActorId == 1 && log[i].Kind == OrderKind.LaborAllocation && log[i].TargetId == 0
                && log[i].Amount == pct) return true;
        return false;
    }
}
