using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 integration R2b — D-021 UNREST-LITE, re-founded by H2 (Director 2026-10-05 §4–§6, §18; RATIFIED in
/// docs/d049-taxation-and-revolt-model.md) on the POPULATION SEGMENT: the levy → felt burden (offset by provision and
/// services) → Dignity → the segment's own LEVY GRIEVANCE stock T (TaxGrievances, v32) → protest → the segment's tipping
/// point → a growing portion of it in revolt → the settlement's uprising only when the rebels carry it. These are the
/// MECHANISM tests (laws, rows, edges); the temporal A–H behaviour on the full pipeline is TaxPressureTests.
///
/// 2026-10-05 (H2): the R2b/R4a tests of the ATTRIBUTION (the levy's grievance as a share of the needs-grievance stock)
/// and of the settlement-wide uprising at G_tax ≥ 50 were removed with the attribution they pinned; their history is
/// in git and in docs/r4a-m5-closure-record.md §3. The R2b "no offset" control arms remain (NoOffset).
/// </summary>
public class UnrestTests
{
    private static SimConfig Cfg => GovernanceRigs.Cfg();
    private static UnrestTuning Tuning => Cfg.Needs!.Unrest!;
    private static readonly ClassId Peasants = new(1);
    private static readonly ClassId Artisans = new(2);

    private static void Levy(WorldState w, PolityId p, double rate)
    {
        for (int i = 0; i < w.TaxPolicies.Count; i++)
            if (w.TaxPolicies[i].Polity == p) { w.TaxPolicies[i] = new TaxPolicyRow(p, rate); return; }
        w.TaxPolicies.Add(new TaxPolicyRow(p, rate));
    }

    /// <summary>Upserts the (settlement, class) LEVY GRIEVANCE row — the rig standing in for NeedsGrievanceSystem's
    /// accrual, used where a test isolates the READERS of the stock.</summary>
    private static void SetLevyGrievance(WorldState w, SettlementId s, ClassId c, double value)
    {
        for (int i = 0; i < w.TaxGrievances.Count; i++)
            if (w.TaxGrievances[i].Settlement == s && w.TaxGrievances[i].Class == c) { w.TaxGrievances[i] = new TaxGrievanceRow(s, c, value); return; }
        w.TaxGrievances.Add(new TaxGrievanceRow(s, c, value));
    }

    /// <summary>Every class of <paramref name="s"/> with members at levy grievance <paramref name="value"/>.</summary>
    private static void SetLevyGrievance(WorldState w, SettlementId s, double value)
    {
        for (int g = 0; g < w.Grievances.Count; g++)
            if (w.Grievances[g].Settlement == s && Unrest.Members(w, s, w.Grievances[g].Class) > 0)
                SetLevyGrievance(w, s, w.Grievances[g].Class, value);
    }

    /// <summary>Moves <paramref name="fraction"/> of every peasant cohort of <paramref name="s"/> into the artisan
    /// buckets through the Ledger (a sanctioned rig transfer — the ClassSystemTests precedent): a minority segment.</summary>
    private static void SeedArtisans(WorldState w, SettlementId s, double fraction)
    {
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            if (w.Buckets[i].Settlement != s || w.Buckets[i].Class != Peasants) continue;
            int dst = -1;
            for (int j = 0; j < w.Buckets.Count; j++)
                if (w.Buckets[j].Settlement == s && w.Buckets[j].Class == Artisans && w.Buckets[j].CohortIdx == w.Buckets[i].CohortIdx) { dst = j; break; }
            long move = (long)Math.Floor(w.Buckets[i].Count.Value * fraction);
            if (dst < 0 || move <= 0) continue;
            ledger.Transfer(ref w.Buckets.Ref(i).Count, ref w.Buckets.Ref(dst).Count, move, OverdrawPolicy.Throw);
        }
    }

    private static double DignityRow(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
            if (w.NeedSatisfactions[i].Settlement == s && w.NeedSatisfactions[i].NeedId == 7) return w.NeedSatisfactions[i].Value;
        return double.NaN;
    }

    private static SimConfig NoOffset(SimConfig cfg) =>
        cfg with { Needs = cfg.Needs! with { Unrest = cfg.Needs!.Unrest! with { TaxBurdenOffsetMax = 0.0, TaxServiceOffsetMax = 0.0 } } };

    // ------------------------------------------------------------------ the content

    [Fact]
    public void TheShippedContent_BindsDignityToTheTaxBurden_AndShipsTheSegmentModel()
    {
        NeedEntry dignity = Cfg.Needs!.Needs.Single(n => n.Id == 7);
        Assert.True(dignity.Bound);
        Assert.True(dignity.FromTaxBurden);
        Assert.NotNull(Cfg.Needs.Unrest);
        Assert.True(Tuning.UprisingGrievance > Tuning.ProtestOnsetGrievance);
        // H2: re-derived on the levy-grievance scale (d049 §5), the service offset and the majority rule.
        Assert.Equal((12.0, 20.0, 0.5, 0.25, 0.5),
            (Tuning.ProtestOnsetGrievance, Tuning.UprisingGrievance, Tuning.TaxBurdenOffsetMax, Tuning.TaxServiceOffsetMax, Tuning.UprisingPopulationShare));
    }

    [Fact]
    public void ALoaderRefuses_ABasketForTheTaxBurdenNeed_AnUprisingBelowTheOnset_AndOutOfRangeSegmentTuning()
    {
        string json;
        using (var reader = new StreamReader(Sim.Data.DataFiles.OpenNeeds())) json = reader.ReadToEnd();
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            System.Text.RegularExpressions.Regex.Replace(json, "\"uprisingGrievance\": [0-9.]+", "\"uprisingGrievance\": 1.0")));
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            json.Replace("\"source\": \"taxBurden\"", "\"source\": \"taxBurdn\"", StringComparison.Ordinal)));
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            json.Replace("\"taxServiceOffsetMax\": 0.25", "\"taxServiceOffsetMax\": 1.0", StringComparison.Ordinal)));
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            json.Replace("\"uprisingPopulationShare\": 0.5", "\"uprisingPopulationShare\": 1.0", StringComparison.Ordinal)));
        // F1: the state-capacity offset is a share in [0, 1].
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            json.Replace("\"taxCapacityOffsetMax\": 0.25", "\"taxCapacityOffsetMax\": -0.1", StringComparison.Ordinal)));
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            json.Replace("\"taxCapacityOffsetMax\": 0.25", "\"taxCapacityOffsetMax\": 1.5", StringComparison.Ordinal)));
    }

    // ------------------------------------------------------------------ the felt burden and Dignity

    [Fact]
    public void HighTax_InjuresDignityDirectly_AndGrievanceRisesAboveTheUntaxedTwin()
    {
        (WorldState taxed, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(taxed, player);
        WorldState untaxed = taxed.Clone();
        Levy(taxed, player, 0.9);

        // Asserted on the R2b reading (no offsets), where the satisfaction is exactly 1 − r.
        TurnExecutor ex = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(NoOffset(Cfg)));
        WorldState t1 = ex.Step(taxed), u1 = ex.Step(untaxed);
        Assert.Equal(1.0 - Governance.EffectiveTaxRate(taxed, seat, Cfg), DignityRow(t1, seat));
        Assert.Equal(1.0 - 0.9, DignityRow(t1, seat), 12);
        Assert.Equal(1.0, DignityRow(u1, seat));
        Assert.True(Unrest.Grievance(t1, seat) > Unrest.Grievance(u1, seat));
        // H2: the levy's OWN stock accrues for the taxed world and does not exist in the untaxed one.
        Assert.True(Unrest.TaxGrievance(t1, seat, Cfg) > 0.0);
        Assert.Equal(0, u1.TaxGrievances.Count);

        WorldState t = t1, u = u1;
        for (int i = 0; i < 6; i++) { t = ex.Step(t); u = ex.Step(u); }
        Assert.True(Unrest.TaxGrievance(t, seat, Cfg) > Unrest.TaxGrievance(t1, seat, Cfg));
        Assert.Equal(0, u.TaxGrievances.Count);
    }

    [Fact]
    public void WithoutAGovernanceSection_DignityHasNoCarrier_PublishesNothing_AndNoLevyGrievanceExists()
    {
        SimConfig bare = Cfg with { Governance = null };
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        WorldState next = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(bare)).Step(w);
        Assert.True(double.IsNaN(DignityRow(next, seat)));
        Assert.Equal(0, next.TaxGrievances.Count);
    }

    /// <summary>R4a + H2: Dignity = 1 − felt, felt = r × (1 − m_P × P) × (1 − m_V × V). Exactly the R2b 1 − r untaxed,
    /// at zero provision with nothing built, or with both offsets 0; a fully provided population feels (1 − m_P).</summary>
    [Fact]
    public void Dignity_IsTheBurdenOffsetByProvision_AndServices_AndExactlyTheR2bReadingAtItsEdges()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        Levy(w, player, 0.99);
        double r = Governance.EffectiveTaxRate(w, seat, Cfg);
        Assert.True(r > 0.9);
        double m = Tuning.TaxBurdenOffsetMax;
        Assert.Equal(0.0, Unrest.ServiceOffset(w, seat, Cfg));   // nothing built at founding
        Assert.Equal(1.0 - r, NeedsGrievanceSystem.DignitySatisfaction(w, seat, Cfg, 0.0));
        Assert.Equal(1.0 - r * (1.0 - m), NeedsGrievanceSystem.DignitySatisfaction(w, seat, Cfg, 1.0));
        Assert.Equal(1.0 - r, NeedsGrievanceSystem.DignitySatisfaction(w, seat, NoOffset(Cfg), 1.0));
        Assert.True(NeedsGrievanceSystem.DignitySatisfaction(w, seat, Cfg, 0.8) > NeedsGrievanceSystem.DignitySatisfaction(w, seat, Cfg, 0.3));
        // Services: a granary at the seat (reach 1.0) lightens the felt levy by m_V × (1 − e^-1).
        w.Structures.Add(new StructureRow(seat, 1, 1));
        double v = Unrest.ServiceOffset(w, seat, Cfg);
        Assert.Equal(1.0 - Math.Exp(-1.0), v, 12);
        Assert.Equal(1.0 - r * (1.0 - m * 0.5) * (1.0 - Tuning.TaxServiceOffsetMax * v),
            NeedsGrievanceSystem.DignitySatisfaction(w, seat, Cfg, 0.5), 12);
        Levy(w, player, 0.0);
        Assert.Equal(1.0, NeedsGrievanceSystem.DignitySatisfaction(w, seat, Cfg, 0.7));
    }

    /// <summary>The service offset is real state only, delivered as far as the state reaches: structures that found no
    /// institution count one each, institutions their maturity, a university's building is not counted twice, an
    /// uncontrolled place gets nothing, and the reach scales it.</summary>
    [Fact]
    public void ServiceOffset_ReadsPublicWorksAndInstitutions_ScaledByReach()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SettlementId other = w.Settlements[0].Id == seat ? w.Settlements[1].Id : w.Settlements[0].Id;
        w.Structures.Add(new StructureRow(seat, 1, 1));                       // granary
        w.Structures.Add(new StructureRow(seat, 2, 1));                       // workshop
        w.Structures.Add(new StructureRow(seat, 12, 1));                      // a university building: counted via its institution
        w.Institutions.Add(new InstitutionRow(1, player, seat, 2, 0, 0.5));   // half-mature university
        Assert.Equal(1.0 - Math.Exp(-2.5), Unrest.ServiceOffset(w, seat, Cfg), 12);
        w.Structures.Add(new StructureRow(other, 1, 1));
        GovernanceRigs.SetStrength(w, player, other, 0.4);
        Assert.Equal(0.4 * (1.0 - Math.Exp(-1.0)), Unrest.ServiceOffset(w, other, Cfg), 12);
        GovernanceRigs.SetStrength(w, player, other, 0.0);
        Assert.Equal(0.0, Unrest.ServiceOffset(w, other, Cfg));
    }

    // ------------------------------------------------------------------ the levy's accrual: undiluted, poorer → more

    /// <summary>
    /// H2 §6 — THE ATTRIBUTION FIX: the levy's accrual is what the aggregation charges for the levy ALONE (every other
    /// need at 1), so a destitute segment's other shortfalls neither dilute nor zero it (under R2b's share they did:
    /// a homeless seat's tax grievance read 0) — and it grows as the felt levy grows. Monotone in the felt burden.
    /// </summary>
    [Fact]
    public void TheLevysAccrual_IsTheLevyAlone_UndilutedByOtherShortfalls_AndGrowsWithTheFeltBurden()
    {
        AggregationTuning agg = Cfg.Needs!.Aggregation;
        double[] w = [1.0, 0.9, 0.3, 0.4];
        bool[] gate = [true, true, false, false];
        double W = 2.6;
        double Accrual(double[] sat) => NeedsGrievanceSystem.LevyAccrualPerYear(sat, gate, w, 3, W, agg, new double[4], new double[4]);
        // A homeless, unfed segment and a well-provided one, same felt Dignity: the SAME levy accrual.
        Assert.Equal(Accrual([1.0, 1.0, 1.0, 0.4]), Accrual([0.0, 0.0, 0.0, 0.4]));
        // No levy felt: exactly 0.
        Assert.Equal(0.0, Accrual([0.2, 0.0, 0.1, 1.0]));
        // Monotone in the felt burden.
        double previous = 0.0;
        foreach (double dignity in new[] { 0.9, 0.7, 0.5, 0.3, 0.1, 0.0 })
        {
            double a = Accrual([1.0, 1.0, 1.0, dignity]);
            Assert.True(a > previous, $"dignity {dignity}: accrual {a} not above {previous}");
            previous = a;
        }
    }

    /// <summary>The same rate on a poorly provided segment is FELT more (lower P → lower Dignity), so it accrues more —
    /// and under R2b's attribution the destitute seat's tax grievance was zero.</summary>
    [Fact]
    public void ThePoorerSegment_FeelsTheSameLevyMore_AndAccruesMore_InTheSystem()
    {
        (WorldState rich, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(rich, player);
        SettlementId seat = GovernanceRigs.Seat(rich, player);
        Levy(rich, player, 0.7);
        WorldState poor = rich.Clone();
        GovernanceRigs.WellProvided(rich, seat);
        GovernanceRigs.Destitute(poor, seat);
        TurnExecutor ex = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(Cfg));
        WorldState r1 = ex.Step(rich), p1 = ex.Step(poor);
        Assert.True(DignityRow(p1, seat) < DignityRow(r1, seat), "the destitute seat felt the levy less");
        Assert.True(Unrest.TaxGrievance(p1, seat, Cfg) > Unrest.TaxGrievance(r1, seat, Cfg));
        Assert.True(Unrest.TaxGrievance(p1, seat, Cfg) > 0.0, "the R2b defect: a destitute seat accrued no tax grievance");
    }

    // ------------------------------------------------------------------ the segment laws

    [Fact]
    public void Protest_IsZeroAtOrBelowTheOnset_LinearBetween_OneAtTheTippingPoint()
    {
        UnrestTuning u = Tuning;
        Assert.Equal(0.0, Unrest.ProtestOf(0.0, u));
        Assert.Equal(0.0, Unrest.ProtestOf(u.ProtestOnsetGrievance, u));
        Assert.Equal(0.5, Unrest.ProtestOf((u.ProtestOnsetGrievance + u.UprisingGrievance) / 2.0, u), 12);
        Assert.Equal(1.0, Unrest.ProtestOf(u.UprisingGrievance, u));
        Assert.Equal(1.0, Unrest.ProtestOf(u.UprisingGrievance * 3.0, u));
        Assert.Equal(0.0, Unrest.ProtestOf(double.NaN, u));
    }

    /// <summary>THE SEGMENT'S TIPPING POINT: risen exactly from uprisingGrievance; past it the rebel fraction grows
    /// linearly over the protest span and saturates at the whole segment.</summary>
    [Fact]
    public void TheTippingPoint_RisenFromU_TheRebelFractionGrowsOverTheProtestSpan()
    {
        UnrestTuning u = Tuning;
        double span = u.UprisingGrievance - u.ProtestOnsetGrievance;
        Assert.Equal(0.0, Unrest.RebelFractionOf(Math.BitDecrement(u.UprisingGrievance), u));
        Assert.Equal(0.0, Unrest.RebelFractionOf(u.UprisingGrievance, u));
        Assert.Equal(0.25, Unrest.RebelFractionOf(u.UprisingGrievance + 0.25 * span, u), 12);
        Assert.Equal(1.0, Unrest.RebelFractionOf(u.UprisingGrievance + span, u));
        Assert.Equal(1.0, Unrest.RebelFractionOf(u.UprisingGrievance + 5 * span, u));

        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SetLevyGrievance(w, seat, Peasants, Math.BitDecrement(u.UprisingGrievance));
        Assert.False(Unrest.IsSegmentRisen(w, seat, Peasants, Cfg));
        SetLevyGrievance(w, seat, Peasants, u.UprisingGrievance);
        Assert.True(Unrest.IsSegmentRisen(w, seat, Peasants, Cfg));
        Assert.False(Unrest.IsSegmentRisen(w, seat, Artisans, Cfg));   // a class with no members never rises
    }

    /// <summary>
    /// E (mechanism) — A RISING AFFECTS ONLY ITS SEGMENT. A minority segment (artisans, a fifth of the capital) past its
    /// tipping point with half of it in revolt, the peasants calm: only the artisans are risen; the output factor is
    /// exactly 1 − r × share × (drag·p·(1 − q) + q) — the peasants withhold nothing; the settlement stays its ruler's
    /// through the real RevoltSystem. The same rebels as the MAJORITY take the settlement.
    /// </summary>
    [Fact]
    public void ARisenMinority_WithholdsOnlyItsOwnLevy_AndDoesNotTakeTheSettlement_AMajorityDoes()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SeedArtisans(w, seat, 0.2);
        Levy(w, player, 0.8);
        UnrestTuning u = Tuning;
        double t = u.UprisingGrievance + 0.5 * (u.UprisingGrievance - u.ProtestOnsetGrievance);   // q = 0.5
        SetLevyGrievance(w, seat, Artisans, t);
        SetLevyGrievance(w, seat, Peasants, 0.0);

        double share = Unrest.Members(w, seat, Artisans) / (double)Unrest.Population(w, seat);
        Assert.InRange(share, 0.15, 0.25);
        Assert.True(Unrest.IsSegmentRisen(w, seat, Artisans, Cfg));
        Assert.False(Unrest.IsSegmentRisen(w, seat, Peasants, Cfg));
        Assert.Equal(0.5 * share, Unrest.RebelShare(w, seat, Cfg), 12);
        double r = Governance.EffectiveTaxRate(w, seat, Cfg);
        double withheld = share * (u.ProtestOutputDragMax * 1.0 * 0.5 + 0.5);
        Assert.Equal(1.0 - r * withheld, Unrest.OutputFactor(w, seat, Cfg), 12);
        Assert.False(Unrest.IsUprising(w, seat, Cfg));
        TurnExecutor revolt = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.Revolt(Cfg));
        Assert.True(EmpireQuery.ControlsSettlement(revolt.Step(w), player, seat), "a rebel minority took the settlement");

        // The peasants (the majority) past the point where their rebels outnumber half the people: the settlement falls.
        SetLevyGrievance(w, seat, Peasants, u.UprisingGrievance + (u.UprisingGrievance - u.ProtestOnsetGrievance));   // q = 1
        Assert.True(Unrest.IsUprising(w, seat, Cfg));
        Assert.False(EmpireQuery.ControlsSettlement(revolt.Step(w), player, seat));
    }

    /// <summary>The settlement uprising boundary, exactly: rebels at the share → held; one ulp of grievance more → lost.</summary>
    [Fact]
    public void TheSettlementUprising_IsRebelsAboveTheShare_Strictly()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        UnrestTuning u = Tuning;
        Levy(w, player, 0.5);   // F1: an uprising against the levy needs the current ruler's levy
        // One class (peasants) holds everyone at founding: rebels = q. q = 0.5 exactly at T = U + 0.5 × span.
        double atShare = u.UprisingGrievance + u.UprisingPopulationShare * (u.UprisingGrievance - u.ProtestOnsetGrievance);
        SetLevyGrievance(w, seat, atShare);
        Assert.Equal(u.UprisingPopulationShare, Unrest.RebelShare(w, seat, Cfg), 12);
        Assert.False(Unrest.IsUprising(w, seat, Cfg));
        SetLevyGrievance(w, seat, atShare + 1e-6);
        Assert.True(Unrest.IsUprising(w, seat, Cfg));
    }

    /// <summary>
    /// F1 (2026-10-05, the H2 verifier's probe P3; d049 §13): an uprising AGAINST THE LEVY needs the CURRENT ruler's
    /// levy. Levy grievance survives a change of hands (d049 §11.5) and keeps decaying, but a ruler that takes nothing
    /// here — e.g. a revolt-born polity that never declared a tax — cannot be thrown off by grievance inherited from the
    /// ruler before it. The same stock under a levy the current ruler collects does carry the settlement.
    /// </summary>
    [Fact]
    public void InheritedLevyGrievance_CannotThrowOffARulerThatLeviesNothing()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SettlementId other = w.Settlements[0].Id == seat ? w.Settlements[1].Id : w.Settlements[0].Id;
        UnrestTuning u = Tuning;
        SetLevyGrievance(w, other, u.UprisingGrievance + (u.UprisingGrievance - u.ProtestOnsetGrievance));   // q = 1
        Assert.Equal(0.0, Governance.EffectiveTaxRate(w, other, Cfg));
        Assert.Equal(1.0, Unrest.RebelShare(w, other, Cfg), 12);          // the inherited stock is still there
        Assert.False(Unrest.IsUprising(w, other, Cfg));
        TurnExecutor revolt = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.Revolt(Cfg));
        Assert.True(EmpireQuery.ControlsSettlement(revolt.Step(w), player, other), "an untaxing ruler was thrown off by inherited levy grievance");

        Levy(w, player, 0.3);
        Assert.True(Unrest.IsUprising(w, other, Cfg));
        Assert.False(EmpireQuery.ControlsSettlement(revolt.Step(w), player, other));
    }

    /// <summary>
    /// F1 (2026-10-05, the H2 verifier's surviving mutant V6) — THE GHOST-GRIEVANCE RULE FOR LEVY ROWS (d049 §2.1, the
    /// T2.13 / T3.5b rule): a class with no members in a living settlement holds NO levy grievance — NeedsGrievanceSystem
    /// zeroes its row — so a class that refills starts from 0 rather than inheriting a pressure nobody now present felt.
    /// A class WITH members keeps its stock (decayed, not zeroed).
    /// </summary>
    [Fact]
    public void AnEmptyClass_HoldsNoLevyGrievance_TheGhostRuleZeroesItsRow()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        Levy(w, player, 0.5);
        Assert.Equal(0L, Unrest.Members(w, seat, Artisans));
        Assert.True(Unrest.Members(w, seat, Peasants) > 0);
        bool hasRow = false;
        for (int i = 0; i < w.Grievances.Count; i++) hasRow |= w.Grievances[i].Settlement == seat && w.Grievances[i].Class == Artisans;
        Assert.True(hasRow, "the seat holds no artisan grievance row");
        SetLevyGrievance(w, seat, Artisans, 15.0);
        SetLevyGrievance(w, seat, Peasants, 15.0);
        WorldState next = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(Cfg)).Step(w);
        Assert.Equal(0.0, Unrest.SegmentTaxGrievance(next, seat, Artisans));
        Assert.True(Unrest.SegmentTaxGrievance(next, seat, Peasants) > 0.0, "a populated segment's levy grievance was zeroed");
    }

    [Fact]
    public void Protest_DragsRealisedOutput_OnlyUnderALevy_AndTheDragGrowsWithItsAmplitude()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        UnrestTuning u = Tuning;

        // Untaxed: EXACTLY 1.0 whatever the levy grievance (the carrier is the levy: no doom loop).
        SetLevyGrievance(w, seat, u.UprisingGrievance * 2.0);
        Assert.Equal(1.0, Governance.OutputMultiplier(w, seat, Cfg));

        Levy(w, player, 0.5);
        double extraction = Governance.ExtractionMultiplier(w, seat, Cfg);
        SetLevyGrievance(w, seat, u.ProtestOnsetGrievance);
        Assert.Equal(extraction, Governance.OutputMultiplier(w, seat, Cfg));   // quiet: the extraction alone

        double previous = extraction;
        foreach (double fraction in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            SetLevyGrievance(w, seat, u.ProtestOnsetGrievance + fraction * (u.UprisingGrievance - u.ProtestOnsetGrievance));
            double expected = extraction * (1.0 - u.ProtestOutputDragMax * fraction * Governance.EffectiveTaxRate(w, seat, Cfg));
            Assert.Equal(expected, Governance.OutputMultiplier(w, seat, Cfg), 12);
            Assert.True(Governance.OutputMultiplier(w, seat, Cfg) < previous);
            previous = Governance.OutputMultiplier(w, seat, Cfg);
        }
        // Past the tipping point the rebels withhold all their levied work: the drag keeps growing.
        SetLevyGrievance(w, seat, u.UprisingGrievance + 0.5 * (u.UprisingGrievance - u.ProtestOnsetGrievance));
        Assert.True(Governance.OutputMultiplier(w, seat, Cfg) < previous);
    }

    [Fact]
    public void NeedsGrievanceTheLevyDidNotCause_IgnitesNothing_SoHungerOrMissingPotteryCannotRaiseARuler()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        for (int i = 0; i < w.Grievances.Count; i++)
            if (w.Grievances[i].Settlement == seat) w.Grievances[i] = w.Grievances[i] with { Value = 1000.0 };
        Assert.Equal(0.0, Unrest.TaxGrievance(w, seat, Cfg));
        Assert.Equal(0.0, Unrest.Protest(w, seat, Cfg));
        Assert.Equal(1.0, Unrest.OutputFactor(w, seat, Cfg));
        Assert.Equal(0.0, Unrest.DischargePerYear(w, seat, Cfg));
        Assert.Equal(0.0, Unrest.LevyPressure(w, seat, Cfg));
        Assert.False(Unrest.IsUprising(w, seat, Cfg));
    }

    [Fact]
    public void Protest_Discharges_PerSegmentAndForTheSettlement_AndQuietIsBitIdentical()
    {
        GrievanceTuning g = Cfg.Needs!.Grievance;
        Assert.Equal(NeedsGrievanceSystem.DecayRatePerYear(g, 0.07), NeedsGrievanceSystem.DecayRatePerYear(g, 0.07, 0.0));
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SetLevyGrievance(w, seat, Tuning.UprisingGrievance);
        Assert.Equal(Tuning.ProtestDischargePerYear, Unrest.SegmentDischargePerYear(w, seat, Peasants, Cfg));
        Assert.Equal(Tuning.ProtestDischargePerYear, Unrest.DischargePerYear(w, seat, Cfg), 12);
        SetLevyGrievance(w, seat, Tuning.ProtestOnsetGrievance - 1.0);
        Assert.Equal(0.0, Unrest.SegmentDischargePerYear(w, seat, Peasants, Cfg));
        Assert.Equal(0.0, Unrest.DischargePerYear(w, seat, Cfg));
    }

    [Fact]
    public void WithoutAnUnrestSection_EveryReaderIsNeutral()
    {
        SimConfig inert = Cfg with { Needs = Cfg.Needs! with { Unrest = null } };
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        Levy(w, player, 0.9);
        SetLevyGrievance(w, seat, 1000.0);
        Assert.Equal(0.0, Unrest.Protest(w, seat, inert));
        Assert.Equal(1.0, Unrest.OutputFactor(w, seat, inert));
        Assert.Equal(0.0, Unrest.DischargePerYear(w, seat, inert));
        Assert.Equal(0.0, Unrest.LevyPressure(w, seat, inert));
        Assert.False(Unrest.IsUprising(w, seat, inert));
        Assert.Equal(1.0, SettlementHappiness.TaxSufficiency(w, seat, inert));
    }

    // ------------------------------------------------------------------ happiness reads the accumulated pressure

    /// <summary>
    /// H2: happiness is scaled by 1 − the ACCUMULATED levy pressure, not by 1 − the edict's rate. A freshly declared
    /// levy changes nothing until pressure has accrued; the pressure is the population-weighted min(1, T / U); untaxed
    /// worlds read exactly 1.0; and revolt reads the PROVISION reading, which no levy touches.
    /// </summary>
    [Fact]
    public void Happiness_FallsWithTheAccumulatedPressure_NotWithTheEdict_AndRevoltReadsProvisionOnly()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        GovernanceRigs.WellProvided(w, seat);
        double before = SettlementHappiness.Of(w, seat, Cfg);
        Levy(w, player, 1.0);
        Assert.Equal(before, SettlementHappiness.Of(w, seat, Cfg));   // the edict alone: no instant effect
        Assert.False(SettlementHappiness.IsRevoltReady(w, seat, Cfg));
        SetLevyGrievance(w, seat, Tuning.UprisingGrievance / 4.0);
        Assert.Equal(0.25, Unrest.LevyPressure(w, seat, Cfg), 12);
        Assert.Equal(before * 0.75, SettlementHappiness.Of(w, seat, Cfg), 9);
        SetLevyGrievance(w, seat, Tuning.UprisingGrievance * 3.0);
        Assert.Equal(0.0, SettlementHappiness.Of(w, seat, Cfg));
        Assert.Equal(before, SettlementHappiness.Provision(w, seat, Cfg));
        Assert.False(SettlementHappiness.IsRevoltReady(w, seat, Cfg));   // consumed welfare is not deprivation
    }

    // ------------------------------------------------------------------ the integrated loop

    private static (WorldState World, int RevoltTurn, double PeakCapitalProtest, int QuietAgainTurn) RunLevy(double percent, int turns, SimConfig? cfg = null)
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        var orders = new OrderLog();
        if (percent > 0.0) orders.Append(Governance.TaxOrder(0, player, percent));
        cfg ??= Cfg;
        TurnExecutor ex = UniversityRigs.Production(cfg, orders);
        int revoltTurn = -1, quietTurn = -1;
        double peak = 0.0;
        for (int t = 1; t <= turns; t++)
        {
            w = ex.Step(w);
            double p = Unrest.Protest(w, seat, cfg);
            peak = Math.Max(peak, p);
            if (revoltTurn < 0 && !EmpireQuery.ControlsSettlement(w, player, seat)) revoltTurn = t;
            if (revoltTurn > 0 && quietTurn < 0 && p == 0.0) quietTurn = t;
        }
        return (w, revoltTurn, peak, quietTurn);
    }

    [Fact]
    public void NormalTax_AtTheAiCeiling_RemainsViable_NoProtestNoRevolt_AndOutputGains()
    {
        const int Turns = 40;
        (WorldState taxed, int revolt, double peak, _) = RunLevy(40.0, Turns);
        (WorldState untaxed, _, _, _) = RunLevy(0.0, Turns);
        Assert.Equal(-1, revolt);
        Assert.Equal(0.0, peak);
        for (int s = 0; s < taxed.Settlements.Count; s++)
            Assert.Equal(0.0, Unrest.Protest(taxed, taxed.Settlements[s].Id, Cfg));
        Assert.Equal(untaxed.Controls.Count, taxed.Controls.Count);
        long Grain(WorldState w)
        {
            long total = 0;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Good.Value == Cfg.Goods!.GrainId) total += w.GoodStocks[i].LastProducedUnits;
            return total;
        }
        Assert.True(Grain(taxed) > Grain(untaxed));
    }

    /// <summary>R2b's integrated loop, kept as the NO-OFFSET control: a 99 % levy at full reach on a seat that feels it
    /// unoffset raises its segments, the rebels carry the seat, and the episode burns out once the levy falls nowhere —
    /// deterministically. With the shipped offsets the same founded seat is TaxPressureTests' subject.</summary>
    [Fact]
    public void ExtremeTax_WithoutTheOffsets_IgnitesProtest_RaisesTheSeat_AndTheEpisodeBurnsOut_Deterministically()
    {
        const int Turns = 40;
        SimConfig r2b = NoOffset(Cfg);
        (WorldState a, int revoltA, double peakA, int quietA) = RunLevy(99.0, Turns, r2b);
        (WorldState b, int revoltB, double peakB, int quietB) = RunLevy(99.0, Turns, r2b);
        Assert.True(revoltA > 0, "a 99 % levy felt unoffset at full reach must raise the seat within the horizon");
        Assert.True(revoltA >= 4, "the seat fell before the accrual bound allows (TaxPressureTests C)");
        Assert.True(peakA > 0.0);
        Assert.True(quietA > revoltA, "the episode must end: protest returns to zero after the uprising");
        Assert.Equal((revoltA, peakA, quietA), (revoltB, peakB, quietB));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    /// <summary>Runs a levy on the founded seat; the seat's every stock and its dwellings are topped up before each
    /// turn (a population with food, amenities and housing to spare).</summary>
    private static (int RevoltTurn, double PeakTaxGrievance, double PeakProtest) RunProvided(double percent, int turns, SimConfig cfg)
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        var orders = new OrderLog();
        orders.Append(Governance.TaxOrder(0, player, percent));
        TurnExecutor ex = UniversityRigs.Production(cfg, orders);
        int revolt = -1;
        double peakG = 0.0, peakP = 0.0;
        for (int t = 1; t <= turns && revolt < 0; t++)
        {
            long pop = GovernanceRigs.Population(w, seat);
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Settlement == seat) GovernanceRigs.AddStock(w, seat, w.GoodStocks[i].Good, 2 * pop + 10);
            GovernanceRigs.SetDwellings(w, seat, pop + 100);
            w = ex.Step(w);
            if (!EmpireQuery.ControlsSettlement(w, player, seat)) revolt = t;
            peakG = Math.Max(peakG, Unrest.TaxGrievance(w, seat, cfg));
            peakP = Math.Max(peakP, Unrest.Protest(w, seat, cfg));
        }
        return (revolt, peakG, peakP);
    }

    /// <summary>R4a's verdict, on the H2 model: 99 % on a well-provided seat is borne in protest, without a rising, over
    /// 80 turns; the same seat felt unoffset (the control) rises.</summary>
    [Fact]
    public void ExtremeTax_OnAWellProvidedSeat_IsBorneInProtest_NotARevolt()
    {
        (int revoltControl, _, _) = RunProvided(99.0, 80, NoOffset(Cfg));
        Assert.True(revoltControl > 0, "control: without the offsets the well-provided seat rises");
        (int revolt, double peakG, double peakP) = RunProvided(99.0, 80, Cfg);
        Assert.Equal(-1, revolt);
        Assert.True(peakP > 0.0, "99 % must still be felt: the seat protests");
        Assert.True(peakG < Tuning.UprisingGrievance);
    }

    // ------------------------------------------------------------------ the uprising meets D-048

    /// <summary>R3 / D-048: a seat thrown off by an UPRISING becomes a NEW AI-controlled polity at once and holds a
    /// COMPLETE copy of its former ruler's knowledge at the instant of separation; the ruler keeps every node.</summary>
    [Fact]
    public void ARevoltedSeat_BecomesANewAiPolity_HoldingTheCompleteParentKnowledge()
    {
        (WorldState w, int revolt, _, _) = RunLevy(99.0, 40, NoOffset(Cfg));
        Assert.True(revolt > 0);
        (WorldState fresh, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(fresh, player);
        Assert.True(EmpireQuery.TryGetController(w, seat, out PolityId founded));
        Assert.NotEqual(player.Value, founded.Value);
        Assert.True(EmpireQuery.TryGetCommandSource(w, founded, out CommandSource source));
        Assert.Equal(CommandSource.Ai, source);
        bool[] child = ResearchQuery.CompletedMask(w, TestConfigs.Research(), founded);
        bool[] ruler = ResearchQuery.CompletedMask(w, TestConfigs.Research(), player);
        int taxation = TestConfigs.Research().IndexOfId(GovernanceRigs.TaxationNode);
        Assert.True(ruler[taxation], "the ruler keeps its knowledge");
        Assert.True(child[taxation], "the new polity inherits the ruler's knowledge");
        for (int i = 0; i < ruler.Length; i++)
            Assert.True(!ruler[i] || child[i], $"node {i}: the ruler knew it at separation, so the new polity must");
        // H2 §8: and its Age — the ruler was in A3 (the tax Age), so is the new polity.
        Assert.Equal(GovernanceRigs.TaxAge, AgeQuery.CurrentAge(w, TestConfigs.Ages(), founded));
    }

    // ------------------------------------------------------------------ R3 §5: capital loss corrupts nothing

    private static (List<WorldState> Worlds, int RevoltTurn, PolityId Player, SettlementId Seat) RunCapitalLoss(int turns)
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        Sim.Core.Systems.Research.ResearchContent content = TestConfigs.Research();
        bool[] done = ResearchQuery.CompletedMask(w, content, player);
        int target = Sim.Core.Systems.Research.AiResearchPolicy.Cheapest(
            w, content, player, ResearchQuery.AvailableMask(content, done), null);
        var orders = new OrderLog();
        orders.Append(Governance.TaxOrder(0, player, 99.0));
        orders.Append(OrderRecord.From(0, player, OrderKind.SetResearchTarget, content.Nodes[target].Key.Value, 0.0));
        // Raised on the no-offset control — the subject is capital loss, not its cause.
        TurnExecutor ex = UniversityRigs.Production(NoOffset(Cfg), orders);
        var worlds = new List<WorldState> { w };
        int revoltTurn = -1;
        for (int t = 1; t <= turns; t++)
        {
            w = ex.Step(w);
            worlds.Add(w);
            if (revoltTurn < 0 && !EmpireQuery.ControlsSettlement(w, player, seat)) revoltTurn = t;
        }
        return (worlds, revoltTurn, player, seat);
    }

    [Fact]
    public void CapitalLoss_ErasesNoKnowledge_ResetsNoResearch_AndLeavesNoTaxSource()
    {
        (List<WorldState> worlds, int revolt, PolityId player, SettlementId seat) = RunCapitalLoss(40);
        Assert.True(revolt > 0, "the capital must be lost within the horizon");
        Sim.Core.Systems.Research.ResearchContent content = TestConfigs.Research();
        for (int t = 1; t < worlds.Count; t++)
        {
            bool[] before = ResearchQuery.CompletedMask(worlds[t - 1], content, player);
            bool[] after = ResearchQuery.CompletedMask(worlds[t], content, player);
            for (int i = 0; i < before.Length; i++)
                Assert.True(!before[i] || after[i], $"turn {t}: node {i} was known and is gone — knowledge decayed");
            for (int r = 0; r < worlds[t - 1].ResearchProgress.Count; r++)
            {
                ResearchProgressRow row = worlds[t - 1].ResearchProgress[r];
                if (row.Polity != player) continue;
                double now = ResearchQuery.Progress(worlds[t], player, row.Node);
                Assert.True(now >= row.Progress || ResearchQuery.IsCompleted(worlds[t], player, row.Node),
                    $"turn {t}: progress on node {row.Node.Value} fell {row.Progress} -> {now}");
            }
        }
        WorldState last = worlds[^1];
        Assert.False(EmpireQuery.IsExtinct(last, player));
        Assert.True(!EmpireQuery.TryGetCapital(last, player, out SettlementId capital) || capital == seat);
        for (int s = 0; s < last.Settlements.Count; s++)
        {
            SettlementId place = last.Settlements[s].Id;
            if (EmpireQuery.ControlsSettlement(last, player, place))
                Assert.Equal(0.0, Governance.EffectiveTaxRate(last, place, Cfg));
        }
    }

    [Fact]
    public void CapitalLoss_SaveLoadAndReplay_AreExact()
    {
        (List<WorldState> a, int revolt, _, _) = RunCapitalLoss(40);
        (List<WorldState> b, _, _, _) = RunCapitalLoss(40);
        Assert.Equal(WorldHash.ComputeHex(a[^1]), WorldHash.ComputeHex(b[^1]));
        WorldState atLoss = a[revolt];
        Assert.True(atLoss.TaxGrievances.Count > 0, "the save must carry levy-grievance rows (a populated v32 table)");
        using var ms = new MemoryStream();
        Snapshot.Save(atLoss, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms, atLoss.Terrain);
        Assert.Equal(WorldHash.ComputeHex(atLoss), WorldHash.ComputeHex(loaded));
    }
}
