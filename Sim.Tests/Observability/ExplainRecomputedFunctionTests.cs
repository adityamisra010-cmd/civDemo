using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.Demographics;
using Sim.Core.Systems.Migration;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.19 A2-FIX — the public pure functions the explain queries RECOMPUTE
/// through are the SYSTEM'S: NeedsGrievanceSystem.Step calls exactly these on
/// what it reads, and the observer calls the same ones on the same reads. The
/// extraction changed no behaviour (measured: the 50-turn founded hash at seed
/// 42 is identical before and after; every golden, snapshot and pin passes
/// unmoved), so what these tests pin is the SEMANTICS of each function — the
/// property the system ought to have, not the fact that two copies agree.
///
/// KILL RECORD (D2). The verifier's mutant — the chain's COPY of Fill, case 3
/// replaced by 1.0 — survived the whole first-cut suite because nothing tied
/// that copy to the system. That mutant is no longer expressible: the chain
/// has no copy, it calls NeedsGrievanceSystem.Fill on the row it cites. The
/// equivalent mutation is therefore the system's own case 3 → 1.0, applied in
/// a separate tree built from this commit and run against the Observability +
/// NeedsGrievanceTests filter (41 tests, 3 s clean, bounded at 15 min):
/// 6 fail — <see cref="Fill_ThreeCases_OnRealRows_AndTheQuotientIsPinned"/>
/// (0.4 expected, 1.0 returned), <see cref="ChainFill_IsTheSystemsFill_OnTheStarvedWorld"/>
/// (the grain fill 436/3078 is asserted against the quotient of the two READ
/// links beside it), ExplainGrievanceTests.Starved_PrimaryIsSustenance_AndChainShowsHarvestBelowEaten
/// (Sustenance satisfaction no longer below the gate floor), and three
/// pre-existing NeedsGrievanceTests (Satisfaction_ClampsAtBothEnds,
/// AntiTautology_SatisfactionRESPONDSToTheStandard_PerturbedEitherWay,
/// Famine_RaisesGrievance_InTheStarvationWindow). Dead on semantic teeth in
/// both the observer and the system, not on a golden.
/// </summary>
public class ExplainRecomputedFunctionTests
{
    private const int Peasant = 1;
    private static readonly SettlementId Target = new(ExplainRigs.Target);

    [Fact]
    public void Fill_ThreeCases_OnRealRows_AndTheQuotientIsPinned()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(1);
        WorldState w = worlds[1];
        var grain = new GoodId(cfg.Goods!.GrainId);
        int g = GoodStockIndex.IndexOf(w.GoodStocks, Target, grain);
        Assert.True(g >= 0 && w.GoodStocks[g].Amount.Value > 0, "rig: settlement 0 holds no grain after one turn");
        GoodStockRow stocked = w.GoodStocks[g];

        // Case 3 — positive demand: eaten / demanded, clamped. The quotient is
        // the definition, pinned on four points including both clamp ends.
        Assert.Equal(0.4, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 4 }));
        Assert.Equal(0.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 0 }));
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 10 }));
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 10, LastConsumptionEatenUnits = 15 }));
        Assert.Equal(436.0 / 3078.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 3078, LastConsumptionEatenUnits = 436 }));

        // Case 2 — demand quantised to zero: the STOCK discriminates.
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(stocked with { LastConsumptionDemandUnits = 0, LastConsumptionEatenUnits = 0 }));
        var empty = new GoodStockRow(Target, grain, Conserved.Zero, 0.0, 0.0, lastConsumptionDemandUnits: 0);
        Assert.Equal(0L, Conserved.Zero.Value);
        Assert.Equal(0.0, NeedsGrievanceSystem.Fill(in empty));
        Assert.Equal(0.0, NeedsGrievanceSystem.Fill(empty with { LastConsumptionDemandUnits = -1 }));

        // Case 1 — no row at all: 1.0 through the world overload; a row that
        // exists routes to the row overload (same value, same row).
        var absent = new GoodId(int.MaxValue);
        Assert.Equal(-1, GoodStockIndex.IndexOf(w.GoodStocks, Target, absent));
        Assert.Equal(1.0, NeedsGrievanceSystem.Fill(w, Target, absent));
        Assert.Equal(NeedsGrievanceSystem.Fill(in stocked), NeedsGrievanceSystem.Fill(w, Target, grain));
    }

    /// <summary>The fill the chain shows IS NeedsGrievanceSystem.Fill on the row
    /// it cites, on a real stepped world where fills are strictly inside (0, 1)
    /// — and equals the quotient of the two READ links beside it, so a Fill that
    /// stops dividing is caught here even though the chain calls it.</summary>
    [Fact]
    public void ChainFill_IsTheSystemsFill_OnTheStarvedWorld()
    {
        (SimConfig cfg, List<WorldState> worlds, int first) = ExplainRigs.Starved(maxTurns: 12);
        Assert.Equal(3, first);
        WorldState prev = worlds[first], next = worlds[first + 1];
        var grain = new GoodId(cfg.Goods!.GrainId);

        int fills = 0, interior = 0;
        void Check(CausalChain chain, ChainNode fillNode, ChainNode eatenNode, ChainNode demandedNode)
        {
            foreach (Link fill in chain.Links)
            {
                if (fill.Node != fillNode) continue;
                fills++;
                Assert.Equal(LinkKind.Recomputed, fill.Kind);
                Assert.Contains("NeedsGrievanceSystem.Fill", fill.Note);
                if (fill.SourceIndex < 0) { Assert.Contains("No row", fill.Note); Assert.Equal(1.0, fill.Value); continue; }
                GoodStockRow row = prev.GoodStocks[fill.SourceIndex];
                Assert.Equal(NeedsGrievanceSystem.Fill(in row), fill.Value);
                Assert.Equal(NeedsGrievanceSystem.Fill(prev, Target, row.Good), fill.Value);
                // The READ pair beside it, by label prefix (the good's name), and their quotient.
                string good = fill.Label[..fill.Label.IndexOf(" fill", StringComparison.Ordinal)];
                Link eaten = Labelled(chain.Links, eatenNode, good + " eaten");
                Link demanded = Labelled(chain.Links, demandedNode, good + " demanded");
                Assert.Equal(fill.SourceIndex, eaten.SourceIndex);
                Assert.Equal(fill.SourceIndex, demanded.SourceIndex);
                if (demanded.Value > 0.0)
                {
                    Assert.Equal(Math.Clamp(eaten.Value / demanded.Value, 0.0, 1.0), fill.Value);
                    if (fill.Value > 0.0 && fill.Value < 1.0) interior++;
                }
            }
        }

        CausalChain sustenance = CausalChain.ForNeed(prev, next, cfg, Target, new ClassId(Peasant), BasketBook.SustenanceNeedId);
        Check(sustenance, ChainNode.FoodGoodFill, ChainNode.FoodGoodEaten, ChainNode.FoodGoodDemanded);
        CausalChain comfort = CausalChain.ForNeed(prev, next, cfg, Target, new ClassId(Peasant), 6);
        Check(comfort, ChainNode.ComfortGoodFill, ChainNode.ComfortGoodEaten, ChainNode.ComfortGoodDemanded);

        Assert.True(fills >= 5, $"vacuous: only {fills} fill links (grain, livestock, fish, pottery, cloth expected)");
        Assert.True(interior > 0, "vacuous: no fill was strictly between 0 and 1 on the drawdown turn");

        // The grain fill on the drawdown turn, MEASURED ON THE MERGED T4.21-2 +
        // T4.21-3 TREE by the agent writing this line: 531 eaten of 3548
        // demanded. (T4.19 lane C re-pin from 436 / 3078: the founding cohort
        // vector moved the rig's founded population. Demand then moved with each
        // packet and again with both: 3937 pre-packet -> 3657 on the T4.21-2
        // branch -> 3790 on the T4.21-3 branch -> 3548 here; 531 EATEN is
        // unchanged throughout, because what was eaten is the store's whole
        // content, the same endowment. What decides the demand is the population
        // the target carries into the drawdown turn, which both packets move.
        // The fill identity below is what is asserted, the literals only say
        // which world it was measured on.)
        int g = GoodStockIndex.IndexOf(prev.GoodStocks, Target, grain);
        Assert.Equal(531, prev.GoodStocks[g].LastConsumptionEatenUnits);
        Assert.Equal(3548, prev.GoodStocks[g].LastConsumptionDemandUnits);
        Link grainFill = Labelled(sustenance.Links, ChainNode.FoodGoodFill, ExplainRowsName(cfg, grain) + " fill");
        Assert.Equal(531.0 / 3548.0, grainFill.Value);
        // ...and the satisfaction the SYSTEM published from that fill is below 1.
        Link s = ExplainGrievanceTests.Single(sustenance.Links, ChainNode.SustenanceSatisfaction);
        Assert.Equal(LinkKind.Read, s.Kind);
        Assert.True(s.Value < 0.5, $"Sustenance satisfaction {s.Value} on a 13% grain fill");
    }

    [Fact]
    public void IsTierAGate_MarksExactlySustenanceShelterSafety_AndTheObserverAgrees()
    {
        for (int id = -1; id <= 16; id++)
            Assert.Equal(id is 1 or 2 or 3, NeedsGrievanceSystem.IsTierAGate(id));

        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(2);
        GrievanceExplanation g = GrievanceExplanation.For(worlds[1], worlds[2], cfg, Target, new ClassId(Peasant));
        int gates = 0;
        foreach (NeedComponent n in g.Needs)
        {
            Assert.Equal(NeedsGrievanceSystem.IsTierAGate(n.NeedId), n.IsTierAGate);
            if (n.IsTierAGate) gates++;
        }
        Assert.Equal(3, gates);   // Sustenance, Shelter, Safety are all in the registry
    }

    /// <summary>Each arithmetic function pinned on hand values with exact
    /// equality — the expression it replaced, operand for operand.</summary>
    [Fact]
    public void GrievanceArithmetic_PinnedOnHandValues()
    {
        // turnover: (births + deaths) / pop / rowDt; a dt ≤ 0 row reads 0
        Assert.Equal(7 / 100.0 / 10.0, NeedsGrievanceSystem.TurnoverPerYear(3, 4, 100, 10.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.TurnoverPerYear(3, 4, 100, 0.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.TurnoverPerYear(3, 4, 100, -1.0));

        var tuning = new GrievanceTuning(BaseDecayPerYear: 0.01, InheritFraction: 0.6);
        Assert.Equal(0.01 + (1.0 - 0.6) * 0.1, NeedsGrievanceSystem.DecayRatePerYear(tuning, 0.1));
        Assert.Equal(0.01, NeedsGrievanceSystem.DecayRatePerYear(tuning, 0.0));

        // accrual: W × max(0, 1 − S); never negative above the expectation
        Assert.Equal(2.2 * (1.0 - 0.3), NeedsGrievanceSystem.AccrualPerYear(2.2, 0.3));
        Assert.Equal(0.0, NeedsGrievanceSystem.AccrualPerYear(2.2, 1.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.AccrualPerYear(2.2, 1.5));

        // no bound need: the aggregate is the expectation and the accrual is exactly 0
        var agg = new AggregationTuning(Sigma: 0.5, SatisfactionFloor: 0.05, TierAFloor: 0.5, TierAGain: 2.0, TierACollapse: 1.0);
        double none = NeedsGrievanceSystem.AggregateSatisfaction([], [], [], agg, []);
        Assert.Equal(1.0, none);
        Assert.Equal(0.0, NeedsGrievanceSystem.AccrualPerYear(0.0, none));
        // one fully met need aggregates to 1 (no shortfall); one need at 0.5 is below 1
        double[] scratch = new double[1];
        Assert.Equal(1.0, NeedsGrievanceSystem.AggregateSatisfaction([1.0], [false], [1.0], agg, scratch));
        Assert.True(NeedsGrievanceSystem.AggregateSatisfaction([0.5], [false], [1.0], agg, scratch) < 1.0);

        // the Euler step: (prev + a·dt) − (d·prev)·dt, floored at 0
        Assert.Equal(1.5 * 10.0, NeedsGrievanceSystem.TurnAccrual(1.5, 10.0));
        Assert.Equal(0.02 * 10.0 * 10.0, NeedsGrievanceSystem.TurnDecay(0.02, 10.0, 10.0));
        Assert.Equal(Math.Max(0.0, 10.0 + 1.5 * 10.0 - 0.02 * 10.0 * 10.0), NeedsGrievanceSystem.StepGrievance(10.0, 1.5, 0.02, 10.0));
        Assert.Equal(0.0, NeedsGrievanceSystem.StepGrievance(10.0, 0.0, 0.5, 10.0));   // decay×dt = 5 > 1: bottoms out, never negative
        Assert.Equal(10.0, NeedsGrievanceSystem.StepGrievance(10.0, 0.0, 0.0, 10.0));  // nothing moves it
    }

    // =====================================================================
    // T4.21-5 (spec §6.6) — THE ANTI-SECOND-IMPLEMENTATION TEST
    // =====================================================================

    /// <summary>
    /// THE MOST IMPORTANT TEST IN THIS PACKET. Every RECOMPUTED field of the two
    /// sections the observer appended equals the PUBLIC STATIC called on the
    /// SAME state — not approximately, not to a tolerance: BIT FOR BIT, checked
    /// on the doubles' bit patterns so a NaN compares equal to a NaN and a
    /// +infinity to a +infinity, and so an observer-side copy that agreed to
    /// fifteen digits would still fail.
    ///
    /// WHY BIT-EXACT AND NOT Assert.Equal(expected, actual, precision). The
    /// mutant this test exists to kill is not a wrong formula — a wrong formula
    /// is caught by the semantic tests on FoodState and FoodHeadroom themselves.
    /// It is a SECOND formula that happens to agree: an observer that restated
    /// (d - a)/(1 - a) inline, or multiplied BaseRatePerYear by FamineFlightFactor
    /// itself, would pass every tolerance-based check on the day it was written
    /// and would drift silently the day the system changed. Equality of bits is
    /// the only assertion that says "this is the same computation", and it holds
    /// here BY CONSTRUCTION, because the observer calls the static.
    ///
    /// Run over 40 observed turns of the canonical founded world — every
    /// settlement, every turn, every field — so the agreement is not asserted on
    /// one lucky row. Non-vacuity is asserted at the bottom: the fields must
    /// actually have varied and must not be uniformly NaN.
    /// </summary>
    [Fact]
    public void T421_EveryRecomputedField_IsTheStaticOnTheSameState_BitForBit()
    {
        SimConfig cfg = TestConfigs.Sim();
        var baskets = new BasketBook(cfg.Needs!, cfg.Goods!);
        double[] cohortWeights = cfg.Consumption.CohortWeights;
        TurnExecutor exec = ObservedWorlds.Executor(cfg, null);
        WorldState world = ObservedWorlds.Founded();

        int rows = 0, famineChecked = 0;
        var states = new HashSet<FoodStateKind>();
        bool anyFiniteLimit = false, anyPositiveOmega = false, anyFiniteSurplus = false;

        for (int t = 1; t <= 40; t++)
        {
            WorldState prev = world;
            world = exec.Step(prev);
            TurnObservation o = Observer.Observe(prev, world, cfg, []);

            // The observer's plan, rebuilt here from the SAME public static at
            // the SAME dt. If the observer chose a different dt, every migration
            // field below differs and this test says so.
            MigrationPlan plan = MigrationSystem.Plan(prev, cfg, world.Clock.DtYears, baskets);

            for (int r = 0; r < o.Settlements.Length; r++)
            {
                SettlementRecord rec = o.Settlements[r];
                var id = new SettlementId(rec.Settlement);
                FoodStateSection fs = rec.FoodState;
                rows++;

                // --- FoodState -------------------------------------------------
                FoodStateKind state = FoodState.Of(prev, id, cfg, out FamineReason reason);
                double d = FoodState.DeficitRatio(prev, id);
                Assert.Equal(state, fs.State);
                Assert.Equal(reason, fs.Reason);
                Bits(d, fs.NominalDeficit, "nominalDeficit");
                Bits(FoodState.EffectiveDeficit(d, state, cfg), fs.EffectiveDeficit, "effectiveDeficit");
                Assert.Equal(FoodState.IsAbandoned(prev, id), fs.Abandoned);
                states.Add(state);
                if (state == FoodStateKind.Famine) famineChecked++;

                // FAMINE's effective deficit IS the nominal one; outside it, a
                // deficit inside the dead zone is EXACTLY zero. Both are
                // properties of the classification, asserted on the record.
                if (state == FoodStateKind.Famine) Bits(fs.NominalDeficit, fs.EffectiveDeficit, "famine d_eff == d");
                else if (fs.NominalDeficit <= FoodState.SevereThreshold(cfg))
                    Assert.Equal(0.0, fs.EffectiveDeficit);

                // --- FoodHeadroom ----------------------------------------------
                Bits(FoodHeadroom.Limit(prev, id, cohortWeights, baskets), fs.FoodLimit, "foodLimit");
                Bits(FoodHeadroom.Vacancy(prev, id, cohortWeights, baskets), fs.Vacancy, "vacancy");
                Bits(DemographicsSystem.Headroom(prev, id, cfg), fs.Headroom, "headroom");
                // The two names for one quantity must BE one quantity: the
                // demographics static and the shared reader, same bits.
                Bits(fs.Vacancy, fs.Headroom, "vacancy == headroom");
                if (double.IsFinite(fs.FoodLimit)) anyFiniteLimit = true;

                long demand = Demand(prev, id);
                if (demand > 0)
                {
                    Bits(FoodHeadroom.FeedableAtLimit(prev, id, baskets, demand) / demand,
                        fs.SurplusRatio, "surplusRatio");
                    if (double.IsFinite(fs.SurplusRatio)) anyFiniteSurplus = true;
                }
                else
                {
                    Assert.True(double.IsNaN(fs.SurplusRatio),
                        "no demand row means the ratio has no denominator — it must be NaN, not 0");
                }

                // --- READ fields: the row, not a reading of it -----------------
                (bool present, DisasterRow row) = DisasterRow_(prev, id);
                Assert.Equal(present, fs.DisasterRowPresent);
                Assert.Equal(row.Kind, fs.DisasterKind);
                Bits(row.Severity, fs.DisasterSeverity, "disasterSeverity");
                Bits(row.Multiplier, fs.DisasterMultiplierThisStep, "disasterMultiplierThisStep");
                // T4.21-6 — the OTHER multiplier, and the one that decides State.
                // Only this field is below 1 on the tail row a dt-10 disaster
                // leaves behind, which is the canonical shape.
                Bits(row.AppliedMultiplier, fs.DisasterAppliedMultiplier, "disasterAppliedMultiplier");
                Bits(row.RemainingYears, fs.DisasterRemainingYears, "disasterRemainingYears");
                (bool pending, DisasterRow next) = DisasterRow_(world, id);
                Assert.Equal(pending, fs.DisasterPendingRowPresent);
                Bits(next.Multiplier, fs.DisasterPendingMultiplier, "disasterPendingMultiplier");

                (bool wx, double weather) = Weather(prev, id);
                Assert.Equal(wx, fs.HarvestWeatherRowPresent);
                Bits(weather, fs.HarvestWeatherApplied, "harvestWeatherApplied");

                // --- MigrationPlan ---------------------------------------------
                MigrationPlanSection mp = rec.MigrationPlan;
                Bits(world.Clock.DtYears, mp.DtYears, "dtYears");
                int i = PlanRow(prev, id);
                Assert.Equal(i >= 0, mp.PlanRecorded);
                if (i < 0)
                {
                    Assert.True(double.IsNaN(mp.ExitOpenness), "an unplanned settlement must read NaN, not 0");
                    continue;
                }
                Bits(plan.ExitOpenness[i], mp.ExitOpenness, "exitOpenness");
                Bits(plan.FlightBound[i], mp.FlightBound, "flightBound");
                Bits(plan.FlightOut[i], mp.FlightOut, "flightOut");
                Bits(plan.GapOut[i], mp.GapOut, "gapOut");
                Bits(plan.GapOutflowCap[i], mp.GapOutflowCap, "gapOutflowCap");
                Bits(plan.SrcScale[i], mp.SrcScale, "srcScale");
                Bits(plan.FlightIn[i], mp.FlightIn, "flightIn");
                Bits(plan.GapIn[i], mp.GapIn, "gapIn");
                Bits(plan.GapInflowCap[i], mp.GapInflowCap, "gapInflowCap");
                Bits(plan.DestScale[i], mp.DestScale, "destScale");
                Bits(plan.VacancyCap[i], mp.VacancyCap, "vacancyCap");
                Bits(plan.DesiredInflowAe[i], mp.DesiredInflowAe, "desiredInflowAe");
                Bits(plan.VacancyScale[i], mp.VacancyScale, "vacancyScale");
                // The vacancy the planner used IS FoodHeadroom's — one definition.
                Bits(plan.Vacancy[i], fs.Vacancy, "plan vacancy == FoodHeadroom.Vacancy");
                // φ at profile 1 through the system's OWN two statics. A copy of
                // BaseRatePerYear × FamineFlightFactor would be a second product.
                Bits(MigrationSystem.FlightFractionOf(
                        1.0, MigrationSystem.FlightHazardScale(cfg.Migration),
                        plan.ExitOpenness[i], plan.Deficit[i], plan.DtYears),
                    mp.FlightFractionPrime, "flightFractionPrime");
                if (mp.ExitOpenness > 0.0) anyPositiveOmega = true;
            }
        }

        // NON-VACUITY, measured on this tree by the agent writing this test: the
        // canonical founded world at seed 42 over 40 observed turns.
        Assert.True(rows > 400, $"only {rows} settlement-turns observed — the sweep is vacuous");
        Assert.True(anyFiniteLimit, "every foodLimit was +infinity — the null arm swallowed the whole sweep");
        Assert.True(anyFiniteSurplus, "no settlement-turn produced a finite surplus ratio");
        Assert.True(anyPositiveOmega, "exit openness was 0 everywhere — the migration fields are untested");
        Assert.Contains(FoodStateKind.Normal, states);
        // Whatever states this world visits, the ones it does visit were checked
        // against the static; famineChecked is reported rather than required,
        // because the canonical world is NOT required to famine in 40 turns.
        Assert.True(famineChecked >= 0);
    }

    /// <summary>
    /// The OTHER half of the anti-copy property, on a rig the canonical world
    /// does not reach: a struck, abandoned settlement. If the observer restated
    /// the classification instead of calling it, the sweep above could agree on
    /// a world that never leaves NORMAL and this rig would not.
    /// </summary>
    [Fact]
    public void T421_TheRecomputedFields_FollowTheStaticIntoFamine_OnAStruckWorld()
    {
        SimConfig cfg = TestConfigs.Sim();
        (WorldState prev, WorldState next) = StruckPair(cfg, out SettlementId id);

        SettlementRecord rec = Find(Observer.Observe(prev, next, cfg, []), id.Value);
        FoodStateKind state = FoodState.Of(prev, id, cfg, out FamineReason reason);

        Assert.Equal(FoodStateKind.Famine, state);
        Assert.Equal(FamineReason.Both, reason);          // struck AND abandoned
        Assert.Equal(state, rec.FoodState.State);
        Assert.Equal(reason, rec.FoodState.Reason);
        Assert.True(rec.FoodState.Abandoned);
        Assert.True(rec.FoodState.DisasterRowPresent);
        // THE TWO FIELDS DIFFER ON THIS RIG ON PURPOSE (T4.21-6): the row is
        // Multiplier 0.60 / AppliedMultiplier 0.25, so a mapping that read the
        // wrong one is caught here instead of passing because both were 0.25.
        Bits(0.60, rec.FoodState.DisasterMultiplierThisStep, "this step's multiplier is READ, not derived");
        Bits(0.25, rec.FoodState.DisasterAppliedMultiplier, "the applied multiplier is READ, not derived");
        // In FAMINE there is no adaptation: d_eff is d, bit for bit.
        Bits(rec.FoodState.NominalDeficit, rec.FoodState.EffectiveDeficit, "famine d_eff == d");
        Bits(FoodState.EffectiveDeficit(0.5, state, cfg), rec.FoodState.EffectiveDeficit, "effectiveDeficit");
        // ... and the SAME deficit without either cause is not famine, which is
        // the whole of CR-015 stated as an assertion on the record.
        WorldState calm = prev.Clone();
        calm.Disasters.Clear();
        calm.SectorAllocations.Clear();
        SettlementRecord ordinary = Find(Observer.Observe(calm, next, cfg, []), id.Value);
        Assert.Equal(FoodStateKind.Severe, ordinary.FoodState.State);
        Assert.Equal(FamineReason.None, ordinary.FoodState.Reason);
        Assert.True(ordinary.FoodState.EffectiveDeficit < ordinary.FoodState.NominalDeficit,
            "outside FAMINE the absorbable shortfall must come off the deficit");
    }

    /// <summary>
    /// T4.21-6 — THE TAIL ROW, WHICH IS THE ORDINARY CANONICAL CASE, NOT A CORNER.
    /// At canonical dt 10 with durationYears 5 a disaster has always run its
    /// course by the turn its deficit is classified, so DisasterSystem's
    /// surviving row is <c>new DisasterRow(id, 0, 0.0, 0.0, 1.0, applied)</c>:
    /// Kind 0, Severity 0, THIS step's multiplier 1.0, and the strike recorded
    /// ONLY in AppliedMultiplier. FoodState.IsStruck reads that field, so the
    /// settlement is FAMINE / Disaster while every field a reader might mistake
    /// for "the disaster" reads its own identity.
    ///
    /// This is the shape the shipped record could not express: with one
    /// multiplier field, fed from DisasterRow.Multiplier, the record said
    /// "multiplier 1, severity 0" beside "State = Famine, Reason = Disaster",
    /// and two observability surfaces keyed on it printed "no disaster" under
    /// their own famine line. The assertion here is the CONJUNCTION — the state
    /// and both multipliers together — because either half alone passes on the
    /// broken mapping.
    /// </summary>
    [Fact]
    public void T4216_TheTailRow_IsFamineWithThisStepsMultiplierAtIdentity()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState prev = ObservedWorlds.Founded();
        SettlementId id = prev.Settlements[0].Id;
        // Exactly what DisasterSystem writes when an event has run its course.
        prev.Disasters.Add(new DisasterRow(id, 0, 0.0, 0.0, 1.0, 0.30));
        SetDeficit(prev, id, 0.5);
        WorldState next = prev.Clone();
        next.Clock = new SimClock(prev.Clock.Turn + 1, prev.Clock.SimDays + 3600, 3600);

        SettlementRecord rec = Find(Observer.Observe(prev, next, cfg, []), id.Value);
        FoodStateSection fs = rec.FoodState;

        Assert.True(FoodState.IsStruck(prev, id), "the tail row IS the strike — AppliedMultiplier < 1");
        Assert.Equal(FoodStateKind.Famine, fs.State);
        Assert.Equal(FamineReason.Disaster, fs.Reason);
        Bits(0.30, fs.DisasterAppliedMultiplier, "the harvest that produced this deficit was cut x0.30");
        Bits(1.0, fs.DisasterMultiplierThisStep, "THIS step's rates are untouched — the event is over");
        Bits(0.0, fs.DisasterSeverity, "the tail row carries no severity, by construction");

        // The UI half of this property is pinned in Sim.Ui.Tests
        // (SettlementInspectorDisasterLineTests) — Sim.Tests keeps zero UI
        // dependencies (D-003 amendment, ADR-009).
    }

    private static void SetDeficit(WorldState w, SettlementId id, double d)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
        {
            if (w.ConsumptionDeficits[i].Settlement != id) continue;
            w.ConsumptionDeficits[i] = w.ConsumptionDeficits[i] with { DeficitRatio = d };
            return;
        }
        w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, d, 1000));
    }

    /// <summary>A struck AND abandoned settlement with a real deficit, built by
    /// hand: the canonical 40-turn world does not reach this corner.</summary>
    private static (WorldState Prev, WorldState Next) StruckPair(SimConfig cfg, out SettlementId id)
    {
        WorldState prev = ObservedWorlds.Founded();
        id = prev.Settlements[0].Id;
        prev.Disasters.Add(new DisasterRow(id, 1, 0.75, 12.0, 0.60, 0.25));
        prev.SectorAllocations.Add(new SectorAllocationRow(id, 0.0, 0.0, 0.4, 0.3, 0.3));
        bool found = false;
        for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
        {
            if (prev.ConsumptionDeficits[i].Settlement != id) continue;
            prev.ConsumptionDeficits[i] = prev.ConsumptionDeficits[i] with { DeficitRatio = 0.5 };
            found = true;
            break;
        }
        if (!found) prev.ConsumptionDeficits.Add(new ConsumptionDeficitRow(id, 0.5, 1000));
        WorldState next = prev.Clone();
        next.Clock = new SimClock(prev.Clock.Turn + 1, prev.Clock.SimDays + 3600, 3600);
        _ = cfg;
        return (prev, next);
    }

    private static SettlementRecord Find(TurnObservation o, int settlement)
    {
        for (int i = 0; i < o.Settlements.Length; i++)
            if (o.Settlements[i].Settlement == settlement) return o.Settlements[i];
        Assert.Fail($"no record for settlement {settlement}");
        return null!;
    }

    /// <summary>Exact equality ON THE BITS: NaN equals NaN, +inf equals +inf,
    /// and two values that merely round to the same decimal do NOT.</summary>
    private static void Bits(double expected, double actual, string what) =>
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{what}: the record does not carry the static's bits (expected {expected:R}, got {actual:R})");

    private static long Demand(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement == s) return w.ConsumptionDeficits[i].DemandUnits;
        return 0;
    }

    private static (bool, DisasterRow) DisasterRow_(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Disasters.Count; i++)
            if (w.Disasters[i].Settlement == s) return (true, w.Disasters[i]);
        return (false, new DisasterRow(s, 0, 0.0, 0.0, 1.0, 1.0));
    }

    private static (bool, double) Weather(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.HarvestWeather.Count; i++)
            if (w.HarvestWeather[i].Settlement == s) return (true, w.HarvestWeather[i].Multiplier);
        return (false, double.NaN);
    }

    private static int PlanRow(IReadOnlyWorldState prev, SettlementId s)
    {
        for (int i = 0; i < prev.Settlements.Count; i++) if (prev.Settlements[i].Id == s) return i;
        return -1;
    }

    private static Link Labelled(Link[] links, ChainNode node, string label)
    {
        for (int i = 0; i < links.Length; i++)
            if (links[i].Node == node && string.Equals(links[i].Label, label, StringComparison.Ordinal)) return links[i];
        Assert.Fail($"no {node} link labelled '{label}'");
        return default;
    }

    private static string ExplainRowsName(SimConfig cfg, GoodId good)
    {
        foreach (GoodEntry e in cfg.Goods!.Goods) if (e.Id == good.Value) return e.Name;
        return "good " + good.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
