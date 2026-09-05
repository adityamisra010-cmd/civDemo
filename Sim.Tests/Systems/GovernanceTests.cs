using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 — THE GOVERNING LOOP.
///
/// The tests are organised around the property the milestone is accepted on: that
/// the loop is CAUSALLY EXECUTABLE, not that the classes exist. So each block
/// pins a real arrow — policy raises output, policy costs happiness, distance
/// limits collection, and the whole thing feeds the valves that already exist —
/// and several are written so that deleting the mechanism makes them fail.
/// </summary>
public class GovernanceTests
{
    private static SimConfig Cfg() => TestConfigs.Sim();

    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "f", "startYear": 0, "endYear": 100000, "dtYears": 10 } ] }""");

    private static OrderRecord SetTax(long turn, PolityId p, double percent)
        => OrderRecord.From(turn, p, OrderKind.SetTaxRate, p.Value, percent);

    /// <summary>A founded world, its player Empire, and a capital — the shape every
    /// governance question is asked of.</summary>
    private static (WorldState World, PolityId Player) Founded(int settlements = 2)
    {
        WorldState w = WorldFounding.Found(
            TestConfigs.Worldgen(), TestConfigs.Sim(), 42, settlements);
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return (w, w.Polities[i].Id);
        throw new InvalidOperationException("founded world has no player Empire");
    }


    private static SettlementId Seat(WorldState w, PolityId p)
    {
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));
        return seat;
    }

    /// <summary>Unfed and unhoused: every provision factor at zero.</summary>
    private static void Destitute(WorldState w, SettlementId s)
    {
        SetDeficit(w, s, 1.0);
        SetDwellings(w, s, 0);
    }

    /// <summary>Fed, and housed to capacity — a settlement with something to lose.</summary>
    private static void WellProvided(WorldState w, SettlementId s)
    {
        SetDeficit(w, s, 0.0);
        SetDwellings(w, s, Population(w, s) + 100);
    }

    private static long Population(WorldState w, SettlementId s)
    {
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s) pop += w.Buckets[i].Count.Value;
        return pop;
    }

    /// <summary>
    /// Drives the settlement's dwelling stock to <paramref name="target"/>.
    /// Conserved stocks move ONLY through the Ledger (law 1), so this sources or
    /// sinks the difference rather than assigning it — a test world is no
    /// exception to the conservation identity.
    /// </summary>
    private static void SetDwellings(WorldState w, SettlementId s, long target)
    {
        int row = -1;
        for (int i = 0; i < w.Housing.Count; i++)
            if (w.Housing[i].Settlement == s) { row = i; break; }

        if (row < 0)
        {
            w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            row = w.Housing.Count - 1;
        }

        long delta = target - w.Housing[row].Dwellings.Value;
        if (delta == 0) return;

        new Ledger(w.LedgerFlows).Flow(
            ref w.Housing.Ref(row).Dwellings, ConservedQuantityIds.Dwellings,
            ReasonIds.InitialEndowment, Math.Abs(delta),
            delta > 0 ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    private static void SetDeficit(WorldState w, SettlementId s, double ratio)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
        {
            if (w.ConsumptionDeficits[i].Settlement != s) continue;
            w.ConsumptionDeficits[i] = w.ConsumptionDeficits[i] with { DeficitRatio = ratio };
            return;
        }

        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, ratio, 1000));
    }

    private static TurnExecutor GovernanceOnly(OrderLog orders, SimConfig? cfg = null) =>
        new(FlatEra(), [SystemCatalog.Governance(cfg ?? TestConfigs.Sim())], orders);

    // ---- 1. POLICY IS ENACTED THROUGH THE COMMON ORDER PATHWAY -------------

    [Fact]
    public void ASetTaxRateOrderBecomesStandingPolicy()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.Equal(0.0, Governance.NominalTaxRate(w, p));   // never legislated

        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 20.0));
        w = GovernanceOnly(orders).Step(w);

        Assert.Equal(1, w.TaxPolicies.Count);
        Assert.Equal(0.20, Governance.NominalTaxRate(w, p), 9);
    }

    [Fact]
    public void PolicyPersistsAcrossTurnsWithoutBeingReissued()
    {
        // A standing decision, not a per-turn instruction.
        (WorldState w, PolityId p) = Founded();
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 20.0));

        TurnExecutor exec = GovernanceOnly(orders);
        w = exec.Step(w);           // turn 0: enacted
        w = exec.Step(w);           // turn 1: no order at all
        w = exec.Step(w);

        Assert.Equal(1, w.TaxPolicies.Count);
        Assert.Equal(0.20, Governance.NominalTaxRate(w, p), 9);
    }

    [Fact]
    public void ALaterOrderReplacesTheRateRatherThanAppendingASecondRow()
    {
        (WorldState w, PolityId p) = Founded();
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 20.0));
        orders.Append(SetTax(1, p, 35.0));

        TurnExecutor exec = GovernanceOnly(orders);
        w = exec.Step(w);
        w = exec.Step(w);

        Assert.Equal(1, w.TaxPolicies.Count);
        Assert.Equal(0.35, Governance.NominalTaxRate(w, p), 9);
    }

    [Fact]
    public void AnEmpireMayNotSetAnotherEmpiresTaxes()
    {
        (WorldState w, PolityId p) = Founded();
        var rival = new PolityId(2);
        w.Polities.Add(new PolityRow(rival, CommandSource.Ai));

        var trespass = new OrderLog();
        trespass.Append(OrderRecord.From(0, p, OrderKind.SetTaxRate, rival.Value, 40.0));

        OrderValidationException ex = Assert.Throws<OrderValidationException>(
            () => OrderValidation.ValidateAgainstWorld(trespass, w));
        Assert.Contains("legislates its own taxes", ex.Message);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    [InlineData(double.NaN)]
    public void AnOutOfRangeRateIsRejectedAtLoad(double percent)
    {
        var log = new OrderLog();
        log.Append(OrderRecord.From(0, new PolityId(1), OrderKind.SetTaxRate, 1, percent));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(ms));
    }

    // ---- 2. AUTHORITY IS A REAL CONSTRAINT --------------------------------

    [Fact]
    public void TheCapitalAdministersItselfInFull()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));
        Assert.Equal(1.0, Governance.AdministrativeReach(w, seat, Cfg()), 9);
    }

    [Fact]
    public void ReachFallsWithTravelCostFromTheCapital()
    {
        // THE AUTHORITY ARROW. Two settlements at different distances from the
        // same seat collect differently from the SAME declared rate.
        (WorldState w, PolityId p) = Founded();
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));
        var near = new SettlementId(1);

        w.SettlementDistances.Add(new SettlementDistanceRow(seat, near, 10.0));
        double closeReach = Governance.AdministrativeReach(w, near, Cfg());

        // Same settlement, further away.
        for (int i = 0; i < w.SettlementDistances.Count; i++)
            if (w.SettlementDistances[i].To == near)
                w.SettlementDistances[i] = new SettlementDistanceRow(seat, near, 60.0);
        double farReach = Governance.AdministrativeReach(w, near, Cfg());

        Assert.True(closeReach > farReach,
            $"reach did not fall with distance: {closeReach} vs {farReach}");
        Assert.InRange(farReach, 0.0, closeReach);
    }

    [Fact]
    public void AnEmpireWithNoCapitalReachesNothing()
    {
        // Capital loss has a fiscal consequence — the seat is what administration
        // radiates from, so without one the state collects nothing anywhere.
        (WorldState w, PolityId p) = Founded();
        w.Capitals.Clear();

        for (int s = 0; s < w.Settlements.Count; s++)
            Assert.Equal(0.0, Governance.AdministrativeReach(w, w.Settlements[s].Id, Cfg()));
    }

    [Fact]
    public void GovernanceComputesControlStrengthAsReach()
    {
        // ControlRow.Strength shipped as a written 1.0 that nobody computed — a
        // §4.1 "chosen, never derived" failure. M5 is its first computer.
        (WorldState w, PolityId p) = Founded();
        Assert.All(Enumerable.Range(0, w.Controls.Count),
            i => Assert.Equal(1.0, w.Controls[i].Strength));   // as founded

        w = GovernanceOnly(new OrderLog()).Step(w);

        bool anyComputed = false;
        for (int i = 0; i < w.Controls.Count; i++)
        {
            double expected = 1.0;   // the capital
            if (w.Controls[i].Place != CapitalOf(w, p))
                expected = Governance.AdministrativeReach(w, w.Controls[i].Place, Cfg());
            Assert.Equal(expected, w.Controls[i].Strength, 9);
            if (w.Controls[i].Strength != 1.0) anyComputed = true;
        }

        Assert.True(anyComputed || w.Settlements.Count == 1,
            "no strength differed from the founded 1.0 — the computation is invisible");
    }

    private static SettlementId CapitalOf(WorldState w, PolityId p)
        => EmpireQuery.TryGetCapital(w, p, out SettlementId seat) ? seat : new SettlementId(-1);

    // ---- 3. THE ECONOMIC ARM ----------------------------------------------

    [Fact]
    public void TaxRaisesEffectiveExtractionAndUntaxedIsExactlyNeutral()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));

        Assert.Equal(1.0, Governance.ExtractionMultiplier(w, seat, Cfg()), 9);   // untaxed

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.50));
        double taxed = Governance.ExtractionMultiplier(w, seat, Cfg());

        Assert.True(taxed > 1.0, $"taxation did not raise extraction ({taxed})");
    }

    [Fact]
    public void HigherTaxExtractsMoreThanLowerTax()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.20));
        double low = Governance.ExtractionMultiplier(w, seat, Cfg());
        w.TaxPolicies[0] = new TaxPolicyRow(p, 0.60);
        double high = Governance.ExtractionMultiplier(w, seat, Cfg());

        Assert.True(high > low, $"extraction did not rise with the rate ({low} -> {high})");
    }

    [Fact]
    public void WithTheResponseAtZeroTaxationChangesNoOutput()
    {
        // THE CONTROL ARM. If this failed, the tests above would not be measuring
        // the extraction term.
        SimConfig off = Cfg() with
        { Governance = Cfg().Governance! with { TaxExtractionResponseMax = 0.0 } };

        (WorldState w, PolityId p) = Founded();
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));
        w.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));

        Assert.Equal(1.0, Governance.ExtractionMultiplier(w, seat, off), 9);
    }

    // ---- 4. THE SOCIAL ARM, THROUGH THE EXISTING HAPPINESS ARCHITECTURE ----

    [Fact]
    public void TaxationLowersHappinessThroughTheExistingDerivedCalculation()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));

        double untaxed = SettlementHappiness.Of(w, seat, Cfg());
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.60));
        double taxed = SettlementHappiness.Of(w, seat, Cfg());

        Assert.True(taxed < untaxed, $"tax did not cost happiness ({untaxed} -> {taxed})");
        Assert.InRange(taxed, 0.0, SettlementHappiness.Max);
    }

    [Fact]
    public void AnUntaxedWorldsHappinessIsUNCHANGEDFromTheM4Calculation()
    {
        // The compatibility guarantee, and it is EXACT rather than approximate:
        // no policy row means the burden reads 1.0 to the bit, and 1.0 is the
        // identity of the multiplication it enters, so every pre-M5 world's
        // happiness is the number M4 computed — not a number close to it. The
        // goldens depend on this: BitConverter, not a tolerance.
        (WorldState w, PolityId p) = Founded();
        Assert.Equal(0, w.TaxPolicies.Count);

        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(1.0),
                BitConverter.DoubleToInt64Bits(SettlementHappiness.TaxSufficiency(w, id, Cfg())));
        }
    }

    [Fact]
    public void TheBurdenFeltIsTheEFFECTIVERateNotTheDeclaredOne()
    {
        // A frontier the collectors cannot reach is not resented for a levy it
        // never paid — which is the whole reason authority and burden share one
        // number instead of two.
        (WorldState w, PolityId p) = Founded();
        Assert.True(EmpireQuery.TryGetCapital(w, p, out SettlementId seat));
        var far = new SettlementId(1);
        w.SettlementDistances.Add(new SettlementDistanceRow(seat, far, 200.0));

        w.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));

        double atSeat = SettlementHappiness.TaxSufficiency(w, seat, Cfg());
        double atFrontier = SettlementHappiness.TaxSufficiency(w, far, Cfg());

        Assert.True(atFrontier > atSeat,
            $"the unreachable frontier felt the levy as hard as the capital ({atFrontier} vs {atSeat})");
    }

    // ---- 5. LEGITIMACY HAS A CONSUMER -------------------------------------

    [Fact]
    public void LegitimacyFallsWhenTheRealmIsTaxedHarder()
    {
        (WorldState w, PolityId p) = Founded();
        double before = Governance.Legitimacy(w, p, Cfg());

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.80));
        double after = Governance.Legitimacy(w, p, Cfg());

        Assert.True(after < before, $"legitimacy ignored the levy ({before} -> {after})");
        Assert.InRange(after, 0.0, SettlementHappiness.Max);
    }

    [Fact]
    public void AnEmpireHoldingNothingHasNoStanding()
    {
        (WorldState w, PolityId p) = Founded();
        w.Controls.Clear();
        Assert.True(EmpireQuery.IsExtinct(w, p));
        Assert.Equal(0.0, Governance.Legitimacy(w, p, Cfg()));
    }

    // ---- 6. D-021 VALVE 6 — THE STATE ACTS BY DEFAULT ---------------------

    [Fact]
    public void AnAiEmpireEasesTheLevyWhenLegitimacyIsLow()
    {
        (WorldState w, _) = Founded();
        var ai = new PolityId(2);
        w.Polities.Add(new PolityRow(ai, CommandSource.Ai));
        for (int s = 0; s < w.Settlements.Count; s++)
            w.Controls.Add(new ControlRow(ai, w.Settlements[s].Id, 1.0));
        w.Capitals.Add(new CapitalRow(ai, w.Settlements[0].Id));

        // A realm in real trouble: hungry AND taxed. Hunger is the honest route to
        // low legitimacy — a levy alone on a fed, housed realm barely dents a
        // non-compensatory aggregate, which is itself the correct behaviour.
        for (int s = 0; s < w.Settlements.Count; s++)
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(w.Settlements[s].Id, 0.9, 1000));
        w.TaxPolicies.Add(new TaxPolicyRow(ai, 0.40));

        double legitimacy = Governance.Legitimacy(w, ai, Cfg());
        Assert.True(legitimacy < AiGovernance.TroubledLegitimacy,
            $"rig is not troubled enough to exercise the valve (legitimacy {legitimacy})");

        List<OrderRecord> orders = AiGovernance.ChooseOrders(w, Cfg(), turn: 3);

        OrderRecord order = Assert.Single(orders, o => o.ActorId == ai.Value);
        Assert.Equal(OrderKind.SetTaxRate, order.Kind);
        Assert.True(order.Amount < 40.0, $"the state did not ease the levy ({order.Amount})");
    }

    [Fact]
    public void TheAiNeverLegislatesForTheHumanEmpire()
    {
        (WorldState w, PolityId player) = Founded();
        List<OrderRecord> orders = AiGovernance.ChooseOrders(w, Cfg(), turn: 1);
        Assert.DoesNotContain(orders, o => o.ActorId == player.Value);
    }

    [Fact]
    public void AiOrdersAreDeterministic()
    {
        (WorldState w, _) = Founded();
        var ai = new PolityId(2);
        w.Polities.Add(new PolityRow(ai, CommandSource.Ai));
        w.Controls.Add(new ControlRow(ai, w.Settlements[0].Id, 1.0));
        w.Capitals.Add(new CapitalRow(ai, w.Settlements[0].Id));

        List<OrderRecord> a = AiGovernance.ChooseOrders(w, Cfg(), 5);
        List<OrderRecord> b = AiGovernance.ChooseOrders(w, Cfg(), 5);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }

    [Fact]
    public void AiOrdersGoThroughTheSameValidationAndSystemAsThePlayers()
    {
        // The "same downstream pathway" requirement, asserted end to end.
        (WorldState w, _) = Founded();
        var ai = new PolityId(2);
        w.Polities.Add(new PolityRow(ai, CommandSource.Ai));
        for (int s = 0; s < w.Settlements.Count; s++)
            w.Controls.Add(new ControlRow(ai, w.Settlements[s].Id, 1.0));
        w.Capitals.Add(new CapitalRow(ai, w.Settlements[0].Id));

        var log = new OrderLog();
        foreach (OrderRecord o in AiGovernance.ChooseOrders(w, Cfg(), 0)) log.Append(o);
        Assert.True(log.Count > 0, "the AI proposed nothing — the test would be vacuous");

        OrderValidation.ValidateAgainstWorld(log, w);       // must not throw
        WorldState next = GovernanceOnly(log).Step(w);

        Assert.True(Governance.NominalTaxRate(next, ai) > 0.0,
            "an AI order did not take effect through the ordinary system");
    }

    [Fact]
    public void TheTaxBurdenCannotDisarmTheRuledRevoltCondition()
    {
        // THE REGRESSION THIS PINS ACTUALLY HAPPENED. M5's first implementation
        // made taxation a THIRD CES factor. An untaxed realm scores 1.0 on it,
        // and a third factor sitting at 1.0 lifts the floor-anchored aggregate
        // off its floor — so a starving, unhoused, untaxed settlement scored
        // 2.31 instead of 0 and `happiness == 0` silently became unreachable.
        // The revolt valve D-021 requires M5 to ship WITH the unrest still read
        // as implemented while being permanently disarmed.
        //
        // Taxation multiplies the aggregate instead, so total deprivation lands
        // on exactly 0 at EVERY rate — untaxed, and taxed as well, which is the
        // half a factor-based model got backwards.
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        Destitute(w, seat);

        Assert.Equal(0.0, SettlementHappiness.Of(w, seat, Cfg()), 9);
        Assert.True(SettlementHappiness.IsRevoltReady(w, seat, Cfg()));

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.75));
        Assert.Equal(0.0, SettlementHappiness.Of(w, seat, Cfg()), 9);
        Assert.True(SettlementHappiness.IsRevoltReady(w, seat, Cfg()));
    }

    [Fact]
    public void TheBurdenIsFeltAtEveryLevelOfProvisionNotTradedOffAgainstIt()
    {
        // The other half of the multiplier's claim, and the reason it is not a
        // free-floating modifier: a well-provided settlement and a struggling one
        // are BOTH made worse by the same levy, in proportion to what they have.
        // A CES factor would have let a full granary buy the levy off.
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        WellProvided(w, seat);

        double before = SettlementHappiness.Of(w, seat, Cfg());
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.50));
        double after = SettlementHappiness.Of(w, seat, Cfg());

        Assert.True(before > 0.0, "the rig is not providing anything — the test would be vacuous");
        Assert.True(after < before, $"the levy cost nothing ({before} -> {after})");

        // Proportional, and exactly so: the burden scales the reading.
        double burden = SettlementHappiness.TaxSufficiency(w, seat, Cfg());
        Assert.Equal(before * burden, after, 9);
    }
}
