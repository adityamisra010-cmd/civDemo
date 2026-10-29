using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// §3 — THE STORE-LOSS RESIDUAL, cross-checked against the ledger. StoreLosses
/// = GrainOpening + Harvest − Eaten − GrainClosing absorbs spoilage + granary
/// overflow (recorded per settlement by nothing, §8 gap 2) AND every unrecorded
/// grain transfer (provisions, raids). Because a transfer is zero-sum, the SUM
/// over all records equals Δledger(Spoilage) + Δledger(GranaryOverflow) on every
/// turn — founding turns included once the founding record is counted — and
/// that is what is asserted, to the unit, for 300 turns on two worlds.
///
/// APPROPRIATION is detected HONESTLY: a raid moves grain victim → raider by
/// Ledger.Transfer and no row records it (§8 gap 4), so the raider's residual
/// goes NEGATIVE while the world sum still closes. The turn is then LABELLED as
/// carrying an unrecorded transfer (FlowsSummary.UnattributedGrainTransfer);
/// nothing re-derives the raid rule to assert it did not fire.
///
/// D-053 §2 (intra-polity grain relief in a shortage; sim.json
/// consumption.empireFoodAllocation) ADDS ONE TERM, stated: a settlement whose grain
/// row is empty and whose requirement is unmet is fed from its own polity's spare
/// grain by Ledger.Transfer and eats it at once, and no row records the grant, so
/// the residual absorbs it — positive at the donor, NEGATIVE at the recipient — and
/// the world sum still closes because a transfer is zero-sum. Where the old
/// assertion said "never negative", it now admits a negative residual ONLY with the
/// relief signature, checked rather than trusted: the recipient's granary closes
/// EMPTY (it ate everything it received, after its own store ran out), it ate
/// something, it has a controller, and its polity's residuals sum to ≥ 0 (a transfer
/// inside one polity nets to zero within it, leaving that polity's spoilage +
/// overflow). A raid — grain crossing a polity boundary — or any other unrecorded
/// inbound transfer still fails. The APPROPRIATION LABEL keeps its meaning: the
/// detector excludes exactly the negatives that carry the relief signature
/// (Observer.AppropriationDetected), so on these worlds — which carry no raid — it is
/// never set, relief turns included; its teeth are pinned below (a relief-shaped
/// negative IS labelled when stateless, crossing a polity, leaving grain in the
/// store, or with the switch off).
/// The founded world carries NO relief (MEASURED: its only deficits are one-unit
/// remainder shortfalls beside a stocked granary), so it keeps the strict form.
/// </summary>
public class StoreLossTests
{
    /// <summary>
    /// The controller of <paramref name="settlement"/> on observation <paramref name="i"/>'s PREV — the grouping the
    /// production detector uses (Observer.AppropriationDetected reads EmpireQuery.TryGetController on PREV, the control
    /// relief grouped the settlement by). Observation i's PREV is observation i−1's NEXT, whose controllers that
    /// observation's records carry (SettlementRecord.Controller is read on NEXT); the first observation's PREV is the run's
    /// <paramref name="start"/>. −1 when the settlement had no controller on PREV, or no row there (founded this step).
    /// </summary>
    private static int PrevController(ObservationLog log, int i, IReadOnlyWorldState start, int settlement)
    {
        if (i == 0) return EmpireQuery.TryGetController(start, new SettlementId(settlement), out PolityId p) ? p.Value : -1;
        foreach (SettlementRecord r in log.Observations[i - 1].Settlements) if (r.Settlement == settlement) return r.Controller;
        return -1;
    }

    private static void AssertSumClosesEveryTurn(
        ObservationLog log, IReadOnlyWorldState start, string world, bool foundingExpected, bool reliefAdmitted = false)
    {
        int spoiling = 0, relieved = 0;
        for (int i = 0; i < log.Observations.Count; i++)
        {
            TurnObservation o = log.Observations[i];
            long losses = 0, harvest = 0, eaten = 0, opening = 0, closing = 0;
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                SettlementRecord r = o.Settlements[s];
                Assert.Equal(Observer.StoreLossesIdentity, r.Food.StoreLossesIdentity);
                losses += r.Food.StoreLosses;
                harvest += r.Food.Harvest;
                eaten += r.Food.Eaten;
                opening += r.Food.GrainOpening;
                closing += r.Food.GrainClosing;
                if (r.Founded) Assert.True(foundingExpected, $"{world}: unexpected founding on turn {o.Turn.Turn}");
                else if (r.Food.StoreLosses < 0)
                {
                    Assert.True(reliefAdmitted,
                        $"{world} turn {o.Turn.Turn} settlement {r.Settlement}: negative store loss {r.Food.StoreLosses} with no founding — an unrecorded inbound transfer");
                    // The ONE admitted term: intra-polity grain relief (D-053 §2), by its signature — grouped, as production
                    // groups it, by the controller on PREV (the control relief allocated by), never NEXT's.
                    int controller = PrevController(log, i, start, r.Settlement);
                    Assert.True(r.Food.GrainClosing == 0 && controller >= 0 && r.Food.Eaten > 0,
                        $"{world} turn {o.Turn.Turn} settlement {r.Settlement}: negative store loss {r.Food.StoreLosses} "
                        + $"without the relief signature (closing {r.Food.GrainClosing}, PREV controller {controller}, eaten {r.Food.Eaten})");
                    long polity = 0;
                    for (int k = 0; k < o.Settlements.Length; k++)
                        if (PrevController(log, i, start, o.Settlements[k].Settlement) == controller) polity += o.Settlements[k].Food.StoreLosses;
                    Assert.True(polity >= 0,
                        $"{world} turn {o.Turn.Turn}: polity {controller}'s residuals sum to {polity} — grain arrived from OUTSIDE it");
                    relieved++;
                }
                if (r.Food.StoreLosses > 0) spoiling++;
            }
            Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow + o.Turn.Grain.Military, losses);
            // No raid on these worlds, and relief is not one: the appropriation label is never set (relief turns included).
            Assert.False(o.Turn.Flows.UnattributedGrainTransfer);
            // The READ terms the residual leans on sum to the ledger's legs.
            Assert.Equal(o.Turn.Grain.Harvest, harvest);
            Assert.Equal(o.Turn.Grain.Eaten, eaten);
            Assert.Equal(o.Turn.Grain.Opening, opening);
            Assert.Equal(o.Turn.Grain.Closing, closing);
            // Sustenance is a basket: FoodObtained counts livestock and fish too,
            // and never less than the grain alone.
            for (int s = 0; s < o.Settlements.Length; s++)
            {
                Assert.Equal(3, o.Settlements[s].Food.FoodGoods.Length);
                Assert.True(o.Settlements[s].Food.FoodObtained >= o.Settlements[s].Food.Eaten);
            }
        }
        // MEASURED: 2,895 of 3,600 settlement-turns on the driven world lose
        // grain to the store (the quarry/workshop groups run leaner granaries);
        // the founded world loses on more. The bound guards vacuity, no more.
        // R4 (forager layer): the driven world MEASURED 2,463 (leaner wild-food stores); the guard is a vacuity
        // floor, re-set below the measured value.
        Assert.True(spoiling > 2000, $"{world}: only {spoiling} settlement-turns lost anything to the store");
        // The admitted term is exercised where it is admitted (non-vacuity of the relief branch).
        if (reliefAdmitted) Assert.True(relieved > 0, $"{world}: no relief settlement-turn — the relief branch is vacuous");
    }

    [Fact]
    public void Founded_SumOfStoreLosses_EqualsLedgerSpoilagePlusOverflow_EveryTurn()
        => AssertSumClosesEveryTurn(ObservedWorlds.Founded300.Value.Log, ObservedWorlds.Founded(), "founded", foundingExpected: false);

    [Fact]
    public void Driven_SumOfStoreLosses_EqualsLedgerSpoilagePlusOverflow_EveryTurn()
        => AssertSumClosesEveryTurn(ObservedWorlds.Driven300.Value.Log, ObservedWorlds.Founded(), "driven", foundingExpected: false, reliefAdmitted: true);

    [Fact]
    public void ARelievedSettlement_ResidualIsMinusWhatItReceived_DonorsCarryIt_TheWorldSumCloses()
    {
        // The turn-level relief rig (EmpireFoodAllocationTests): settlement 0 of the founded world has abandoned
        // its food labour and its stores are empty; its siblings feed it in full. Its record is EXACT: opening 0,
        // harvest 0, eaten = its whole requirement, closing 0 → StoreLosses = −requirement. The turn is NOT labelled
        // as an appropriation (the negative carries the relief signature), and the world sum still equals the
        // ledger's spoilage + overflow to the unit.
        SimConfig cfg = TestConfigs.Sim();
        WorldState prev = Sim.Tests.Systems.EmpireFoodAllocationTests.AbandonedSettlementRig(cfg, out SettlementId s0);
        WorldState next = Sim.Tests.Systems.EmpireFoodAllocationTests.Pipeline(cfg, TestConfigs.Worldgen()).Step(prev);
        TurnObservation o = Observer.Observe(prev, next, cfg, []);

        SettlementRecord r = o.Settlements[0];
        Assert.Equal(s0.Value, r.Settlement);
        long required = r.Food.DemandUnits;
        Assert.True(required > 0);
        Assert.Equal(0.0, r.Food.DeficitRatio);
        Assert.Equal(0L, r.Food.GrainOpening);
        Assert.Equal(0L, r.Food.Harvest);
        Assert.Equal(required, r.Food.Eaten);
        Assert.Equal(0L, r.Food.GrainClosing);
        Assert.Equal(-required, r.Food.StoreLosses);
        Assert.Contains("intra-polity grain relief", r.Food.StoreLossesIdentity, StringComparison.Ordinal);
        Assert.False(o.Turn.Flows.UnattributedGrainTransfer);
        Assert.True(o.Turn.Grain.Reconciles);

        long sum = 0, siblings = 0;
        for (int s = 0; s < o.Settlements.Length; s++)
        {
            sum += o.Settlements[s].Food.StoreLosses;
            if (s > 0) { siblings += o.Settlements[s].Food.StoreLosses; Assert.True(o.Settlements[s].Food.StoreLosses >= 0); }
        }
        Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow + o.Turn.Grain.Military, sum);
        Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow + o.Turn.Grain.Military + required, siblings);   // the donors carry the grant
    }

    /// <summary>
    /// THE DETECTOR'S TEETH AGAINST THE RELIEF EXCLUSION (B2). The relieved turn above is not labelled; the SAME
    /// negative residual IS labelled the moment any arm of the relief signature fails — and only the observation
    /// changes, never the step: (1) the switch is off (relief cannot have happened); (2) the recipient had no
    /// controller (a stateless raider's shape); (3) it is credited to a polity of its own, whose residuals then sum
    /// below 0 (grain crossed a polity boundary); (4) one more unit arrives by an unrecorded transfer from a sibling
    /// and stays in its store (the granary does not close empty).
    /// </summary>
    [Fact]
    public void TheDetector_LabelsAReliefShapedNegative_WhenAnyArmOfTheSignatureFails()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState prev = Sim.Tests.Systems.EmpireFoodAllocationTests.AbandonedSettlementRig(cfg, out SettlementId s0);
        WorldState next = Sim.Tests.Systems.EmpireFoodAllocationTests.Pipeline(cfg, TestConfigs.Worldgen()).Step(prev);
        TurnObservation baseline = Observer.Observe(prev, next, cfg, []);
        Assert.True(baseline.Settlements[0].Food.StoreLosses < 0);
        Assert.False(baseline.Turn.Flows.UnattributedGrainTransfer);

        // (1) switch off.
        TurnObservation off = Observer.Observe(prev, next, TestConfigs.PreEmpireFoodAllocation(cfg), []);
        Assert.Equal(baseline.Settlements[0].Food.StoreLosses, off.Settlements[0].Food.StoreLosses);
        Assert.True(off.Turn.Flows.UnattributedGrainTransfer);

        // (2) stateless: the recipient's control rows removed from both worlds.
        static WorldState Recontrol(WorldState w, SettlementId place, int? polity)
        {
            WorldState c = w.Clone();
            var kept = new List<ControlRow>();
            for (int i = 0; i < c.Controls.Count; i++) if (c.Controls[i].Place != place) kept.Add(c.Controls[i]);
            c.Controls.Clear();
            foreach (ControlRow row in kept) c.Controls.Add(row);
            if (polity is { } p) c.Controls.Add(new ControlRow(new PolityId(p), place, 1.0));
            return c;
        }
        TurnObservation stateless = Observer.Observe(Recontrol(prev, s0, null), Recontrol(next, s0, null), cfg, []);
        Assert.Equal(-1, stateless.Settlements[0].Controller);
        Assert.True(stateless.Settlements[0].Food.StoreLosses < 0);
        Assert.True(stateless.Turn.Flows.UnattributedGrainTransfer);

        // (3) another polity: polity 99 holds only the recipient, so its residuals sum to the negative alone.
        TurnObservation foreign = Observer.Observe(Recontrol(prev, s0, 99), Recontrol(next, s0, 99), cfg, []);
        Assert.Equal(99, foreign.Settlements[0].Controller);
        Assert.True(foreign.Turn.Flows.UnattributedGrainTransfer);

        // (4) the granary does not close empty: one unit moved in from settlement 1 by an unrecorded transfer.
        WorldState kept1 = next.Clone();
        var grain = new GoodId(cfg.Goods!.GrainId);
        int from = GoodStockIndex.IndexOf(kept1.GoodStocks, kept1.Settlements[1].Id, grain);
        int to = GoodStockIndex.IndexOf(kept1.GoodStocks, s0, grain);
        Assert.True(kept1.GoodStocks[from].Amount.Value > 0);
        new Ledger(kept1.LedgerFlows).Transfer(
            ref kept1.GoodStocks.Ref(from).Amount, ref kept1.GoodStocks.Ref(to).Amount, 1, OverdrawPolicy.Throw);
        TurnObservation stocked = Observer.Observe(prev, kept1, cfg, []);
        Assert.Equal(1L, stocked.Settlements[0].Food.GrainClosing);
        Assert.Equal(baseline.Settlements[0].Food.StoreLosses - 1, stocked.Settlements[0].Food.StoreLosses);
        Assert.True(stocked.Turn.Flows.UnattributedGrainTransfer);
    }

    [Fact]
    public void OnAFoundingTurn_TheSumStillCloses_OnceTheColonyCarriesItsProvisions()
    {
        ObservationLog log = ObservedWorlds.Founding5.Value.Log;
        TurnObservation o = log.At(2)!;
        long losses = 0, prevOnly = 0;
        for (int s = 0; s < o.Settlements.Length; s++)
        {
            losses += o.Settlements[s].Food.StoreLosses;
            if (!o.Settlements[s].Founded) prevOnly += o.Settlements[s].Food.StoreLosses;
        }
        long provisions = o.Settlements[12].Food.GrainClosing;
        Assert.True(provisions > 0);
        Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow + o.Turn.Grain.Military, losses);
        // Without the founding record the prev settlements over-count by exactly
        // the provisions that left the source — which is why the founding record
        // carries them as a negative loss.
        Assert.Equal(o.Turn.Grain.Spoilage + o.Turn.Grain.Overflow + o.Turn.Grain.Military + provisions, prevOnly);
        Assert.True(o.Turn.Grain.Spoilage > 0, "no spoilage on the founding turn");
        Assert.False(o.Turn.Flows.UnattributedGrainTransfer);
        for (long turn = 3; turn <= 6; turn++)
        {
            TurnObservation later = log.At(turn)!;
            long sum = 0;
            for (int s = 0; s < later.Settlements.Length; s++) sum += later.Settlements[s].Food.StoreLosses;
            Assert.Equal(later.Turn.Grain.Spoilage + later.Turn.Grain.Overflow + later.Turn.Grain.Military, sum);
        }
    }

    [Fact]
    public void AnUnrecordedGrainTransfer_LabelsTheTurn_WhileTheWorldSumStillCloses()
    {
        // TEETH for the appropriation detector: move 50 grain from settlement 1
        // to settlement 0 by Ledger.Transfer — exactly what AppropriationSystem
        // does, and what no row records. The recipient's residual goes negative,
        // the turn is labelled, and the world-level sum is unmoved.
        WorldState prev = ObservedWorlds.Founded();
        WorldState next = prev.Clone();
        next.Clock = new SimClock(prev.Clock.Turn + 1, prev.Clock.SimDays + 3600, 3600);
        var grain = new GoodId(TestConfigs.Sim().Goods!.GrainId);
        int from = GoodStockIndex.IndexOf(next.GoodStocks, next.Settlements[1].Id, grain);
        int to = GoodStockIndex.IndexOf(next.GoodStocks, next.Settlements[0].Id, grain);
        new Ledger(next.LedgerFlows).Transfer(
            ref next.GoodStocks.Ref(from).Amount, ref next.GoodStocks.Ref(to).Amount, 50, OverdrawPolicy.Throw);

        TurnObservation o = Observer.Observe(prev, next, TestConfigs.Sim(), []);

        Assert.Equal(-50, o.Settlements[0].Food.StoreLosses);
        Assert.Equal(50, o.Settlements[1].Food.StoreLosses);
        Assert.True(o.Turn.Flows.UnattributedGrainTransfer);
        Assert.True(o.Turn.Grain.Reconciles);
        long sum = 0;
        for (int s = 0; s < o.Settlements.Length; s++) sum += o.Settlements[s].Food.StoreLosses;
        Assert.Equal(0, sum);   // no spoilage, no overflow, no rows: the transfer is zero-sum
    }
}
