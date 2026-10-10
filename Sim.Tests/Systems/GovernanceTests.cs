using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Governance;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.GovernanceRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D4 — THE GOVERNING LOOP (ported from <c>m5-full-build</c>'s GovernanceTests, 25 cases,
/// re-anchored on the integrated tree, plus the port's own pins).
///
/// Organised around the property the loop is accepted on — that it is CAUSALLY EXECUTABLE — so each
/// block pins a real arrow: policy enters only through the order and the research gate; reach is ONE
/// stored fact (ControlRow.Strength) that every consumer reads; policy raises output and costs
/// happiness; legitimacy reads it; the AI valve answers it through the same pathway; and without its
/// config section the whole loop is inert. Timing, save/load and the production sites are in
/// GovernanceTimingTests.
/// </summary>
public class GovernanceTests
{
    private static GovernanceConfig G() => Cfg().Governance!;

    private static TurnExecutor Full(SimConfig cfg, OrderLog? orders = null)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.DevWorldgen())), orders);
    }

    // ---- 1. POLICY IS ENACTED THROUGH THE COMMON ORDER PATHWAY ---------------------------

    [Fact]
    public void ASetTaxRateOrderBecomesStandingPolicy()
    {
        (WorldState w, PolityId p) = Founded();
        Grant(w, p);
        Assert.Equal(0.0, Governance.NominalTaxRate(w, p));   // never legislated
        Assert.False(Governance.HasPolicy(w, p));

        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 20.0));
        w = GovernanceOnly(orders).Step(w);

        Assert.Equal(1, w.TaxPolicies.Count);
        Assert.Equal(0.20, Governance.NominalTaxRate(w, p), 9);
        Assert.True(Governance.HasPolicy(w, p));
    }

    [Fact]
    public void PolicyPersistsAcrossTurnsWithoutBeingReissued()
    {
        (WorldState w, PolityId p) = Founded();
        Grant(w, p);
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 20.0));

        TurnExecutor exec = GovernanceOnly(orders);
        w = exec.Step(w);   // turn 0: enacted
        w = exec.Step(w);   // turn 1: no order at all
        w = exec.Step(w);

        Assert.Equal(1, w.TaxPolicies.Count);
        Assert.Equal(0.20, Governance.NominalTaxRate(w, p), 9);
    }

    [Fact]
    public void ALaterOrderReplacesTheRateRatherThanAppendingASecondRow()
    {
        (WorldState w, PolityId p) = Founded();
        Grant(w, p);
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
    public void TheLastValidOrderOfATurnWins_AndARepealIsAStandingZero()
    {
        (WorldState w, PolityId p) = Founded();
        Grant(w, p);
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 20.0));
        orders.Append(SetTax(0, p, 35.0));   // same turn, later in the log
        orders.Append(SetTax(1, p, 0.0));    // repeal

        TurnExecutor exec = GovernanceOnly(orders);
        w = exec.Step(w);
        Assert.Equal(0.35, Governance.NominalTaxRate(w, p), 9);
        w = exec.Step(w);
        Assert.Equal(1, w.TaxPolicies.Count);   // the row stays: "levies 0 %", not "never legislated"
        Assert.True(Governance.HasPolicy(w, p));
        Assert.Equal(0.0, Governance.NominalTaxRate(w, p));
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

    [Fact]
    public void ATrespassThatReachesTheSystemUnvalidated_ChangesNothing()
    {
        // Defence in depth: an order log handed straight to the executor (no world validation)
        // still cannot set another Empire's rate — the system enacts only self-targeted edicts.
        (WorldState w, PolityId p) = Founded();
        var rival = new PolityId(2);
        w.Polities.Add(new PolityRow(rival, CommandSource.Ai));
        Grant(w, p);
        Grant(w, rival);
        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, p, OrderKind.SetTaxRate, rival.Value, 40.0));
        w = GovernanceOnly(orders).Step(w);
        Assert.Equal(0, w.TaxPolicies.Count);
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

    [Theory]
    [InlineData(0.0)]
    [InlineData(100.0)]
    [InlineData(37.5)]
    public void TheLegislativeBoundsAndFractionsRoundTripTheOrderLog(double percent)
    {
        var log = new OrderLog();
        log.Append(Governance.TaxOrder(4, new PolityId(1), percent));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        OrderLog back = OrderLog.Load(ms);
        Assert.Equal(log[0], back[0]);
        Assert.Equal(OrderKind.SetTaxRate, back[0].Kind);
        Assert.Equal(5, (int)back[0].Kind);   // the kind ADR-029 §4 reserved, now real
    }

    // ---- 2. TAXATION IS RESEARCH-GATED BY THE CONTENT --------------------------------------

    [Fact]
    public void WithoutTaxationKnowledge_TheEdictChangesNothing()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.False(Governance.CanLevyTax(w, Cfg(), p));   // a founding civilization cannot tax
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 50.0));
        w = GovernanceOnly(orders).Step(w);
        Assert.Equal(0, w.TaxPolicies.Count);
        Assert.Equal(0.0, Governance.NominalTaxRate(w, p));
    }

    [Fact]
    public void TheTaxationCivicOpensTheGate()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.False(Governance.CanLevyTax(w, Cfg(), p));
        Grant(w, p, "taxation");
        Assert.True(Governance.CanLevyTax(w, Cfg(), p));
        var content = TestConfigs.Research();
        var node = content.Nodes[content.IndexOfId("taxation")];
        Assert.Equal(global::Sim.Core.Systems.Research.ResearchTree.Civics, node.Tree);
    }

    /// <summary>R5 (Director 2026-10-04): the four refinement nodes keep their taxation capability TEXT but
    /// no longer open the gate — alone or all together.</summary>
    [Theory]
    [InlineData("arithmetic_babylonian")]   // "tax assessment"
    [InlineData("surveying")]               // "taxation by area"
    [InlineData("standard_weights")]        // "taxation by weight"
    [InlineData("coinage_electrum")]        // "taxation in coin"
    public void TheFourTaxationRefinementNodes_NoLongerOpenTheGate(string node)
    {
        (WorldState w, PolityId p) = Founded();
        Grant(w, p, node);
        Assert.False(Governance.CanLevyTax(w, Cfg(), p));
        foreach (string other in new[] { "arithmetic_babylonian", "surveying", "standard_weights", "coinage_electrum" })
            Grant(w, p, other);
        Assert.False(Governance.CanLevyTax(w, Cfg(), p));
        Grant(w, p, "taxation");
        Assert.True(Governance.CanLevyTax(w, Cfg(), p));
        var content = TestConfigs.Research();
        bool namesTax = false;
        foreach (string cap in content.Nodes[content.IndexOfId(node)].Capabilities)
            namesTax |= cap.Contains("tax", StringComparison.OrdinalIgnoreCase);
        Assert.True(namesTax, $"{node} does not name a taxation capability");
    }

    [Fact]
    public void ANonTaxationNodeDoesNotOpenTheGate()
    {
        (WorldState w, PolityId p) = Founded();
        Grant(w, p, "cereal_cultivation");
        Grant(w, p, "numeral_sexagesimal");   // arithmetic_babylonian's prerequisite, not taxation itself
        Assert.False(Governance.CanLevyTax(w, Cfg(), p));
    }

    [Fact]
    public void TheGateIsTheCONFIGUREDExpression_NoNodeIsNamedInCode()
    {
        // Rewire the content expression: the same predicate now follows the new data.
        SimConfig cfg = Cfg() with { Governance = G() with { TaxationRequires = "cereal_cultivation" } };
        (WorldState w, PolityId p) = Founded();
        Grant(w, p, "taxation");
        Assert.False(Governance.CanLevyTax(w, cfg, p));
        Grant(w, p, "cereal_cultivation");
        Assert.True(Governance.CanLevyTax(w, cfg, p));
    }

    [Fact]
    public void OneEmpiresClosedGateDoesNotBlockAnothersEdict()
    {
        (WorldState w, PolityId p) = Founded(aiEmpires: 1);
        var ai = new PolityId(2);
        Grant(w, ai);                         // only the AI Empire can tax
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 30.0));
        orders.Append(SetTax(0, ai, 10.0));
        w = GovernanceOnly(orders).Step(w);
        Assert.Equal(0.0, Governance.NominalTaxRate(w, p));
        Assert.False(Governance.HasPolicy(w, p));
        Assert.Equal(0.10, Governance.NominalTaxRate(w, ai), 9);
    }

    [Fact]
    public void CanLevyTax_IsFalseWithoutAGovernanceSectionOrResearchContent()
    {
        (WorldState w, PolityId p) = Founded();
        Grant(w, p);
        Assert.True(Governance.CanLevyTax(w, Cfg(), p));
        Assert.False(Governance.CanLevyTax(w, Cfg() with { Governance = null }, p));
        Assert.False(Governance.CanLevyTax(w, Cfg() with { Research = null }, p));
    }

    // ---- 3. AUTHORITY: REACH IS ONE STORED FACT ---------------------------------------------

    [Fact]
    public void TheCapitalAdministersItselfInFull()
    {
        (WorldState w, PolityId p) = Founded();
        Assert.Equal(1.0, Governance.AdministrativeReach(w, p, Seat(w, p), G()));
    }

    [Fact]
    public void ReachFallsWithTravelCostFromTheCapital()
    {
        // THE AUTHORITY ARROW: the same declared rate collects less where the capital's reach is weaker.
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        var place = new SettlementId(1);
        Assert.NotEqual(seat, place);

        SetDistance(w, seat, place, 10.0);
        double close = Governance.AdministrativeReach(w, p, place, G());
        SetDistance(w, seat, place, 60.0);
        double far = Governance.AdministrativeReach(w, p, place, G());

        Assert.Equal(Math.Exp(-10.0 / 25.0), close);   // exp(−cost / authorityDecayCostUnits), bit for bit
        Assert.Equal(Math.Exp(-60.0 / 25.0), far);
        Assert.True(close > far);
    }

    [Fact]
    public void AnEmpireWithNoCapitalReachesNothing()
    {
        (WorldState w, PolityId p) = Founded();
        w.Capitals.Clear();
        for (int s = 0; s < w.Settlements.Count; s++)
            Assert.Equal(0.0, Governance.AdministrativeReach(w, p, w.Settlements[s].Id, G()));
    }

    [Fact]
    public void AnEmpireThatNoLongerControlsItsCapital_ReachesNothing_SoItsTaxFallsNowhere()
    {
        // THE ORCHESTRATOR'S CASE (revolt drops the capital's control row but never the CapitalRow,
        // RevoltSystem.cs / WorldFounding.cs). L6 RULE: a seat the Empire no longer holds administers
        // nothing — reach 0 at every settlement it still controls, so its levy has NO effect anywhere
        // (no extraction, no burden) until it holds a capital again. Total: no throw, no NaN.
        (WorldState w, PolityId p) = Founded();
        Grant(w, p);
        SettlementId seat = Seat(w, p);
        for (int s = 0; s < w.Settlements.Count; s++)
            if (w.Settlements[s].Id != seat) SetDistance(w, seat, w.Settlements[s].Id, 5.0);

        // Lose the capital the way revolt loses it: the control row goes, the CapitalRow stays.
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != seat) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow r in kept) w.Controls.Add(r);
        Assert.True(EmpireQuery.TryGetCapital(w, p, out _));          // the designation survives
        Assert.False(EmpireQuery.ControlsSettlement(w, p, seat));     // ...but the seat is not held

        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 80.0));
        WorldState next = GovernanceOnly(orders).Step(w);

        // G10 (M5 polish): a lost seat fails the ONE tax predicate, so the edict is refused outright (it could never
        // be collected) — before G10 it was recorded as a nominal 80 % that reached nobody.
        Assert.Equal(TaxGate.NeedsSeat, Governance.GateOf(w, Cfg(), p));
        Assert.False(Governance.HasPolicy(next, p));                  // the edict is refused...
        Assert.Equal(0.0, Governance.NominalTaxRate(next, p));
        for (int i = 0; i < next.Controls.Count; i++)
        {
            ControlRow row = next.Controls[i];
            Assert.Equal(0.0, row.Strength);                          // ...and nobody is reached
            Assert.Equal(0.0, Governance.EffectiveTaxRate(next, row.Place, Cfg()));
            Assert.Equal(1.0, Governance.ExtractionMultiplier(next, row.Place, Cfg()));
            Assert.Equal(1.0, SettlementHappiness.TaxSufficiency(next, row.Place, Cfg()));
            Assert.False(double.IsNaN(SettlementHappiness.Of(next, row.Place, Cfg())));
        }
    }

    [Fact]
    public void UnreachableCorruptAndMissingRoutesReachZero_NeverNaN()
    {
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        var a = new SettlementId(1);
        var b = new SettlementId(2);
        var c = new SettlementId(3);
        SetDistance(w, seat, a, double.PositiveInfinity);
        SetDistance(w, seat, b, double.NaN);
        Assert.Equal(0.0, Governance.AdministrativeReach(w, p, a, G()));   // unreachable
        Assert.Equal(0.0, Governance.AdministrativeReach(w, p, b, G()));   // corrupt
        Assert.Equal(0.0, Governance.AdministrativeReach(w, p, c, G()));   // no route on record
        Assert.Equal(0.0, Governance.AdministrativeReach(w, p, a, G() with { AuthorityDecayCostUnits = 0.0 }));
        // A corrupt stored Strength reads 0, never NaN.
        SetStrength(w, p, a, double.NaN);
        Assert.Equal(0.0, Governance.ControlStrength(w, p, a));
    }

    [Fact]
    public void GovernanceComputesControlStrengthAsReach_FromThePreviousTurnsDistances()
    {
        // ControlRow.Strength shipped as a written 1.0 nobody computed; GovernanceSystem is its ONE
        // computer, evaluating the reach on PREV.
        (WorldState w0, _) = Founded();
        TurnExecutor full = Full(Cfg());
        WorldState prev = full.Run(w0, 2);
        WorldState next = full.Step(prev);

        bool anyBelowOne = false;
        for (int i = 0; i < next.Controls.Count; i++)
        {
            ControlRow row = next.Controls[i];
            double expected = Governance.AdministrativeReach(prev, row.Polity, row.Place, G());
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(row.Strength));
            anyBelowOne |= row.Strength < 1.0;
        }
        Assert.True(anyBelowOne, "no Strength differed from the founded 1.0 — the computation is invisible");
    }

    [Fact]
    public void TheEffectiveRateReadsTheSTOREDStrength_NothingRecomputesReachBesideIt()
    {
        // ONE FACT, ONE PLACE (the M5B defect: Strength written, never read, while production and
        // happiness recomputed reach). Move the road-aware distance: the stored Strength — and so
        // every consumer — does NOT move until GovernanceSystem rewrites it. Move the Strength: every
        // consumer moves with it.
        (WorldState w, PolityId p) = Founded();
        var place = new SettlementId(1);
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.5));
        Assert.Equal(1.0, Governance.ControlStrength(w, p, place));   // turn-0 founding value
        Assert.Equal(0.5, Governance.EffectiveTaxRate(w, place, Cfg()));

        SetDistance(w, Seat(w, p), place, 500.0);   // a distance that would make reach ~0 if recomputed
        Assert.Equal(0.5, Governance.EffectiveTaxRate(w, place, Cfg()));
        Assert.Equal(1.0 + 0.3 * 0.5, Governance.ExtractionMultiplier(w, place, Cfg()));
        // H2: the burden on welfare starts from the FELT levy (unoffset here: provision 0, nothing built).
        Assert.Equal(0.5, Unrest.FeltBurden(w, place, Cfg(), 0.0));

        SetStrength(w, p, place, 0.25);
        Assert.Equal(0.125, Governance.EffectiveTaxRate(w, place, Cfg()));
        Assert.Equal(1.0 + 0.3 * 0.125, Governance.ExtractionMultiplier(w, place, Cfg()));
        // F1 (d049 §15): the felt burden reads the SAME stored Strength — as state capacity aggravating the declared
        // levy (0.5 × (1 + k × 0.75)), no longer as the collected share.
        Assert.Equal(0.5 * (1.0 + Cfg().Needs!.Unrest!.TaxCapacityOffsetMax * 0.75), Unrest.FeltBurden(w, place, Cfg(), 0.0));
    }

    // ---- 4. THE ECONOMIC ARM ------------------------------------------------------------------

    [Fact]
    public void TaxRaisesEffectiveExtractionAndUntaxedIsExactlyNeutral()
    {
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        Assert.Equal(1.0, Governance.ExtractionMultiplier(w, seat, Cfg()));   // untaxed: exactly 1

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.50));
        double taxed = Governance.ExtractionMultiplier(w, seat, Cfg());
        Assert.Equal(1.0 + 0.3 * 0.5, taxed);
    }

    [Fact]
    public void HigherTaxExtractsMoreThanLowerTax()
    {
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.20));
        double low = Governance.ExtractionMultiplier(w, seat, Cfg());
        w.TaxPolicies[0] = new TaxPolicyRow(p, 0.60);
        double high = Governance.ExtractionMultiplier(w, seat, Cfg());
        Assert.True(high > low, $"extraction did not rise with the rate ({low} -> {high})");
    }

    [Fact]
    public void WithTheResponseAtZeroTaxationChangesNoOutput()
    {
        // THE CONTROL ARM: without it the cases above would not be measuring the extraction term.
        SimConfig off = Cfg() with { Governance = G() with { TaxExtractionResponseMax = 0.0 } };
        (WorldState w, PolityId p) = Founded();
        w.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));
        Assert.Equal(1.0, Governance.ExtractionMultiplier(w, Seat(w, p), off));
    }

    // ---- 5. THE SOCIAL ARM, THROUGH THE EXISTING HAPPINESS ARCHITECTURE --------------------
    // H2 (Director 2026-10-05 §4, docs/d049-taxation-and-revolt-model.md): the levy reaches happiness only through
    // the population segments' ACCUMULATED levy grievance (SettlementHappiness.TaxSufficiency = 1 − LevyPressure),
    // which NeedsGrievanceSystem integrates turn by turn. These cases therefore step that system (Accrue) where the
    // ADR-033 D4 versions read the declared rate directly; each still asserts the property it was written for.

    /// <summary>Steps ONLY NeedsGrievanceSystem <paramref name="turns"/> times (10-year turns): the levy's grievance
    /// accrues from the policy rows the rig wrote.</summary>
    private static WorldState Accrue(WorldState w, int turns)
    {
        TurnExecutor ex = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(Cfg()));
        for (int t = 0; t < turns; t++) w = ex.Step(w);
        return w;
    }

    [Fact]
    public void TaxationLowersHappinessThroughTheExistingDerivedCalculation()
    {
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        WellProvided(w, seat);
        double untaxed = SettlementHappiness.Of(w, seat, Cfg());
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.60));
        Assert.Equal(untaxed, SettlementHappiness.Of(w, seat, Cfg()));   // H2: the edict alone costs nothing yet
        WorldState later = Accrue(w, 3);
        double taxed = SettlementHappiness.Of(later, seat, Cfg());
        Assert.True(taxed < SettlementHappiness.Provision(later, seat, Cfg()), $"tax did not cost happiness ({untaxed} -> {taxed})");
        Assert.InRange(taxed, 0.0, SettlementHappiness.Max);
    }

    [Fact]
    public void AnUntaxedWorldsHappinessIsUNCHANGEDFromThePrePortCalculation()
    {
        // EXACT, not approximate: no policy row means the burden reads 1.0 to the bit, the identity
        // of the multiplication it enters — and so does a config with no governance section.
        (WorldState w, _) = Founded();
        Assert.Equal(0, w.TaxPolicies.Count);
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            Assert.Equal(BitConverter.DoubleToInt64Bits(1.0),
                BitConverter.DoubleToInt64Bits(SettlementHappiness.TaxSufficiency(w, id, Cfg())));
            Assert.Equal(BitConverter.DoubleToInt64Bits(1.0),
                BitConverter.DoubleToInt64Bits(SettlementHappiness.TaxSufficiency(w, id, Cfg() with { Governance = null })));
        }
    }

    [Fact]
    public void TheFrontierYieldsTheEFFECTIVERate_ButFeelsTheDeclaredOne_HeavierForWeakCapacity()
    {
        // F1 (2026-10-05, Director §5/§6; d049 §15) SUPERSEDES the H2 pin "TheBurdenFeltIsTheEFFECTIVERateNotTheDeclaredOne"
        // ("the frontier accumulates less pressure"), which ran opposite to the Director: weak state capacity means
        // GREATER grievance from the same tax rate. The frontier still YIELDS little (collection = declared × reach),
        // but it feels the declared demand, aggravated by the weak administration that collects it.
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        var far = new SettlementId(1);
        SetDistance(w, seat, far, 200.0);
        w = GovernanceOnly(new OrderLog()).Step(w);   // the ONE computer writes the reach
        double reach = Math.Exp(-200.0 / 25.0);
        Assert.Equal(reach, Governance.ControlStrength(w, p, far));

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.6));
        Assert.Equal(0.6 * reach, Governance.EffectiveTaxRate(w, far, Cfg()));   // the collection rule
        Assert.Equal(0.6, Unrest.FeltBurden(w, seat, Cfg(), 0.0));
        double k = Cfg().Needs!.Unrest!.TaxCapacityOffsetMax;
        Assert.Equal(0.6 * (1.0 + k * (1.0 - reach)), Unrest.FeltBurden(w, far, Cfg(), 0.0), 12);
        // And so the frontier accumulates MORE pressure and keeps LESS of its welfare than the capital.
        WorldState later = Accrue(w, 3);
        double atSeat = SettlementHappiness.TaxSufficiency(later, seat, Cfg());
        double atFrontier = SettlementHappiness.TaxSufficiency(later, far, Cfg());
        Assert.True(atSeat < 1.0);
        Assert.True(atFrontier < atSeat,
            $"the weakly administered frontier felt the levy no harder than the capital ({atFrontier} vs {atSeat})");
    }

    [Fact]
    public void TheTaxBurdenCannotDisarmTheRuledRevoltCondition()
    {
        // THE REGRESSION THIS PINS ACTUALLY HAPPENED on m5-full-build: tax as a third CES factor
        // lifted total deprivation to 2.31 and made happiness == 0 unreachable. As a multiplier,
        // total deprivation lands on exactly 0 at every rate.
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        Destitute(w, seat);
        Assert.Equal(0.0, SettlementHappiness.Of(w, seat, Cfg()));
        Assert.True(SettlementHappiness.IsRevoltReady(w, seat, Cfg()));

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.75));
        Assert.Equal(0.0, SettlementHappiness.Of(w, seat, Cfg()));
        Assert.True(SettlementHappiness.IsRevoltReady(w, seat, Cfg()));
    }

    [Fact]
    public void TheBurdenIsFeltAtEveryLevelOfProvisionNotTradedOffAgainstIt()
    {
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        WellProvided(w, seat);
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.50));
        WorldState later = Accrue(w, 3);
        double before = SettlementHappiness.Provision(later, seat, Cfg());
        double after = SettlementHappiness.Of(later, seat, Cfg());
        Assert.True(before > 0.0, "the rig is not providing anything — the test would be vacuous");
        Assert.True(after < before, $"the levy cost nothing ({before} -> {after})");
        double burden = SettlementHappiness.TaxSufficiency(later, seat, Cfg());
        Assert.Equal(before * burden, after, 9);   // proportional: the burden scales the reading
    }

    /// <summary>
    /// 2026-10-05 (H2) — REPLACES <c>AFullLevyAtFullReachIsTotalExtraction_TheSecondRevoltCorner</c>, which pinned
    /// "a declared 100 % levy at full reach reads happiness 0 and is revolt-ready". The Director REJECTED that corner
    /// (M5 hardening §4: "Do NOT implement an instant revolt at 100 % tax. Do NOT create a direct tax >= X → revolt
    /// rule"; RATIFIED in docs/d049-taxation-and-revolt-model.md). Now: a 100 % levy at full reach is permitted, it
    /// is NOT revolt-ready (revolt reads the provision reading), happiness is untouched until pressure accrues, and
    /// even after accrual a well-provided seat is never revolt-ready from the levy. The revolt path of a levy is the
    /// segment rising (UnrestTests, TaxPressureTests).
    /// </summary>
    [Fact]
    public void AFullLevyAtFullReach_IsNoLongerARevoltCorner_H2()
    {
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        WellProvided(w, seat);
        double before = SettlementHappiness.Of(w, seat, Cfg());
        Assert.True(before > 0.0);
        w.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));
        Assert.Equal(1.0, Governance.EffectiveTaxRate(w, seat, Cfg()));
        Assert.Equal(before, SettlementHappiness.Of(w, seat, Cfg()));
        Assert.False(SettlementHappiness.IsRevoltReady(w, seat, Cfg()));
        // One 10-year turn of total levy: felt, but nothing like the old zero.
        WorldState one = Accrue(w, 1);
        Assert.InRange(SettlementHappiness.Of(one, seat, Cfg()), double.Epsilon, before - 1e-9);
        // Thirty years of it on a seat with no public works may exhaust the levy's share of welfare (severe) — but
        // the PROVISION reading revolt reads is untouched: the levy alone never makes a place revolt-ready.
        WorldState later = Accrue(one, 2);
        Assert.True(SettlementHappiness.Of(later, seat, Cfg()) <= SettlementHappiness.Of(one, seat, Cfg()));
        Assert.False(SettlementHappiness.IsRevoltReady(later, seat, Cfg()));
        Assert.Equal(SettlementHappiness.Provision(w, seat, Cfg()), SettlementHappiness.Provision(later, seat, Cfg()));
    }

    // ---- 6. LEGITIMACY HAS A CONSUMER ------------------------------------------------------------

    [Fact]
    public void LegitimacyFallsWhenTheRealmIsTaxedHarder()
    {
        (WorldState w, PolityId p) = Founded();
        for (int s = 0; s < w.Settlements.Count; s++) WellProvided(w, w.Settlements[s].Id);
        double before = Governance.Legitimacy(w, p, Cfg());
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.80));
        Assert.Equal(before, Governance.Legitimacy(w, p, Cfg()));   // H2: no instant effect
        double after = Governance.Legitimacy(Accrue(w, 3), p, Cfg());
        Assert.True(after < before, $"legitimacy ignored the levy ({before} -> {after})");
        Assert.InRange(after, 0.0, SettlementHappiness.Max);
    }

    [Fact]
    public void LegitimacyIsThePopulationWeightedMeanHappinessOfWhatTheEmpireHolds()
    {
        (WorldState w, PolityId p) = Founded();
        SetDeficit(w, w.Settlements[0].Id, 0.5);   // make the readings differ
        double weighted = 0.0;
        long people = 0;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            long pop = Population(w, id);
            weighted += SettlementHappiness.Of(w, id, Cfg()) * pop;
            people += pop;
        }
        Assert.Equal(weighted / people, Governance.Legitimacy(w, p, Cfg()), 12);
    }

    [Fact]
    public void AnEmpireHoldingNothingHasNoStanding()
    {
        (WorldState w, PolityId p) = Founded();
        w.Controls.Clear();
        Assert.True(EmpireQuery.IsExtinct(w, p));
        Assert.Equal(0.0, Governance.Legitimacy(w, p, Cfg()));
    }

    // ---- 7. D-021 VALVE 6 — THE STATE ACTS BY DEFAULT -----------------------------------------

    private static (WorldState World, PolityId Ai) AiRealm()
    {
        (WorldState w, _) = Founded(aiEmpires: 1);
        var ai = new PolityId(2);
        Assert.True(EmpireQuery.TryGetCommandSource(w, ai, out CommandSource source) && source == CommandSource.Ai);
        Assert.False(EmpireQuery.IsExtinct(w, ai));
        Grant(w, ai);
        return (w, ai);
    }

    [Fact]
    public void AnAiEmpireEasesTheLevyWhenLegitimacyIsLow()
    {
        (WorldState w, PolityId ai) = AiRealm();
        // A realm in real trouble: hungry AND taxed.
        for (int s = 0; s < w.Settlements.Count; s++) SetDeficit(w, w.Settlements[s].Id, 0.9);
        w.TaxPolicies.Add(new TaxPolicyRow(ai, 0.40));
        double legitimacy = Governance.Legitimacy(w, ai, Cfg());
        Assert.True(legitimacy < G().Ai.TroubledLegitimacy, $"rig is not troubled enough (legitimacy {legitimacy})");

        OrderRecord order = Assert.Single(AiGovernance.OrdersFor(w, Cfg(), ai, turn: 3));
        Assert.Equal(OrderKind.SetTaxRate, order.Kind);
        Assert.Equal(3, order.Turn);
        Assert.Equal(40.0 - G().Ai.StepPercent, order.Amount);
    }

    [Fact]
    public void TheValveRule_UpWhenComfortable_DownWhenTroubled_HoldsInTheDeadBand_AndClamps()
    {
        GovernanceAiConfig ai = G().Ai;
        Assert.Equal(25.0, AiGovernance.TargetRatePercent(ai.ComfortableLegitimacy, 20.0, ai));       // ≥ comfortable: up
        Assert.Equal(15.0, AiGovernance.TargetRatePercent(ai.TroubledLegitimacy - 1e-9, 20.0, ai));   // < troubled: down
        Assert.Equal(20.0, AiGovernance.TargetRatePercent(ai.TroubledLegitimacy, 20.0, ai));          // dead band: hold
        Assert.Equal(20.0, AiGovernance.TargetRatePercent(ai.ComfortableLegitimacy - 1e-9, 20.0, ai));
        Assert.Equal(ai.MaxRatePercent, AiGovernance.TargetRatePercent(100.0, ai.MaxRatePercent, ai)); // capped
        Assert.Equal(0.0, AiGovernance.TargetRatePercent(0.0, 0.0, ai));                               // floored
        // The constants are DATA: a different config, a different valve.
        var other = new GovernanceAiConfig(80.0, 10.0, 7.0, 12.0);
        Assert.Equal(12.0, AiGovernance.TargetRatePercent(90.0, 7.0, other));
        Assert.Equal(7.0, AiGovernance.TargetRatePercent(60.0, 7.0, other));
    }

    [Fact]
    public void TheAiActsOnlyWhenItCanLevyTax()
    {
        (WorldState w, _) = Founded(aiEmpires: 1);
        var ai = new PolityId(2);
        Assert.False(Governance.CanLevyTax(w, Cfg(), ai));
        Assert.Empty(AiGovernance.OrdersFor(w, Cfg(), ai, 0));
        Grant(w, ai);
        Assert.NotEmpty(AiGovernance.OrdersFor(w, Cfg(), ai, 0));   // a content founding realm raises the levy
        Assert.Empty(AiGovernance.OrdersFor(w, Cfg() with { Governance = null }, ai, 0));
    }

    [Fact]
    public void TheAiNeverLegislatesForTheHumanEmpire()
    {
        (WorldState w, PolityId player) = Founded();
        Grant(w, player);
        Assert.Empty(AiGovernance.OrdersFor(w, Cfg(), player, turn: 1));
    }

    [Fact]
    public void AiOrdersAreDeterministic()
    {
        (WorldState w, PolityId ai) = AiRealm();
        OrderRecord[] a = AiGovernance.OrdersFor(w, Cfg(), ai, 5);
        OrderRecord[] b = AiGovernance.OrdersFor(w.Clone(), Cfg(), ai, 5);
        Assert.Equal(a, b);
    }

    [Fact]
    public void AiOrdersGoThroughTheSameValidationAndSystemAsThePlayers()
    {
        (WorldState w, PolityId ai) = AiRealm();
        var log = new OrderLog();
        foreach (OrderRecord o in AiGovernance.OrdersFor(w, Cfg(), ai, 0)) log.Append(o);
        Assert.True(log.Count > 0, "the AI proposed nothing — the test would be vacuous");

        OrderValidation.ValidateAgainstWorld(log, w);   // must not throw
        WorldState next = GovernanceOnly(log).Step(w);
        Assert.True(Governance.NominalTaxRate(next, ai) > 0.0, "an AI order did not take effect through the ordinary system");
    }

    // ---- 8. INERT WITHOUT A GOVERNANCE SECTION -------------------------------------------------

    [Fact]
    public void WithoutAGovernanceSection_TheFullCatalogRuns_AndTheLoopIsInert()
    {
        // A rig on a hand-written config runs every system; the governing loop then applies no tax
        // row, leaves Strength as founding wrote it, and every reader is neutral. M5B's reach THREW
        // here for any world with two or more controlled settlements.
        SimConfig bare = Cfg() with { Governance = null };
        (WorldState w, PolityId p) = Founded();
        Grant(w, p);
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 60.0));
        orders.Append(SetTax(2, p, 90.0));
        WorldState end = Full(bare, orders).Run(w, 4);

        Assert.Equal(0, end.TaxPolicies.Count);
        for (int i = 0; i < end.Controls.Count; i++)
        {
            Assert.Equal(1.0, end.Controls[i].Strength);
            Assert.Equal(0.0, Governance.EffectiveTaxRate(end, end.Controls[i].Place, bare));
            Assert.Equal(1.0, Governance.ExtractionMultiplier(end, end.Controls[i].Place, bare));
        }
        Assert.False(Governance.CanLevyTax(end, bare, p));
    }

    // ---- 9. THE CONFIG -------------------------------------------------------------------------

    [Fact]
    public void TheCanonicalConfigCarriesTheGoverningLoop()
    {
        GovernanceConfig g = G();
        Assert.Equal(25.0, g.AuthorityDecayCostUnits);
        Assert.Equal(Cfg().Migration.DampingDecayCostUnits, g.AuthorityDecayCostUnits);   // the reference class
        Assert.Equal(0.3, g.TaxExtractionResponseMax);
        Assert.Equal(Cfg().Production.ToolYieldBonusMax, g.TaxExtractionResponseMax);    // the measured frame
        Assert.Equal("taxation", g.TaxationRequires);
        Assert.Equal(new GovernanceAiConfig(60.0, 35.0, 5.0, 40.0), g.Ai);
    }

    private static SimConfig LoadFourStream(string simJson)
    {
        using var sim = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(simJson));
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        using var research = Sim.Data.DataFiles.OpenResearch();
        return SimConfigLoader.Load(sim, needs, goods, research);
    }

    private const string ShippedRequires = "\"taxationRequires\": \"taxation\"";

    [Fact]
    public void TheTaxationRequirementIsValidatedAgainstResearchWhereTheFilesMeet()
    {
        Assert.Contains(ShippedRequires, TestConfigs.SimJson());
        Assert.NotNull(LoadFourStream(TestConfigs.SimJson()).Governance);   // the shipped expression resolves

        string typo = TestConfigs.SimJson().Replace(ShippedRequires,
            "\"taxationRequires\": \"arithmetic_babylonian OR no_such_node\"", StringComparison.Ordinal);
        SimConfigException e = Assert.Throws<SimConfigException>(() => LoadFourStream(typo));
        Assert.Contains("governance.taxationRequires", e.Message);
        Assert.Contains("no_such_node", e.Message);
        // The sim-only load cannot see research.json, so it accepts the text; the gate then never
        // opens on a config without research content (CanLevyTax is false), never silently true.
        Assert.NotNull(SimConfigLoader.Load(typo).Governance);
    }

    [Theory]
    [InlineData("\"taxationRequires\": \"NOT arithmetic_babylonian\"", "NOT")]
    [InlineData("\"taxationRequires\": \"artisan_share > 0\"", "node ids only")]
    public void AKnowledgeRequirementThatIsNotMonotoneOrNotOverNodesFailsTheLoad(string replacement, string fragment)
    {
        string json = TestConfigs.SimJson().Replace(ShippedRequires, replacement, StringComparison.Ordinal);
        SimConfigException e = Assert.Throws<SimConfigException>(() => LoadFourStream(json));
        Assert.Contains(fragment, e.Message);
    }

    [Theory]
    [InlineData("\"authorityDecayCostUnits\": 25.0", "\"authorityDecayCostUnits\": 0.0")]
    [InlineData("\"taxExtractionResponseMax\": 0.3", "\"taxExtractionResponseMax\": -0.1")]
    [InlineData(ShippedRequires, "\"taxationRequires\": \"  \"")]
    [InlineData("\"troubledLegitimacy\": 35.0", "\"troubledLegitimacy\": 60.0")]
    [InlineData("\"stepPercent\": 5.0", "\"stepPercent\": 0.0")]
    [InlineData("\"maxRatePercent\": 40.0", "\"maxRatePercent\": 101.0")]
    public void AMalformedGovernanceSectionFailsTheLoad(string shipped, string broken)
    {
        string json = TestConfigs.SimJson().Replace(shipped, broken, StringComparison.Ordinal);
        Assert.NotEqual(TestConfigs.SimJson(), json);
        Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
    }

    [Fact]
    public void AConfigWithoutAGovernanceSectionLoads_WithTheLoopAbsent()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(TestConfigs.SimJson())!.AsObject();
        Assert.True(node.Remove("governance"));
        SimConfig cfg = LoadFourStream(node.ToJsonString());
        Assert.Null(cfg.Governance);
    }
}
