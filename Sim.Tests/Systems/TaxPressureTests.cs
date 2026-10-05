using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.TaxPressureMeasurement;

namespace Sim.Tests.Systems;

/// <summary>
/// H2 (Director 2026-10-05 §17, A–H; RATIFIED in docs/d049-taxation-and-revolt-model.md) — TAXATION AS CONTINUOUS
/// PRESSURE, MEASURED OVER TIME ON THE FULL PIPELINE. Every case runs the founded dev world (seed 42, one player
/// Empire holding four settlements, crafts known, in A3, the canonical era table — 10 years a turn at founding, the
/// fastest accumulation per turn the game has) with a levy declared at turn 0, and reads the CAPITAL (reach 1.0) each
/// turn through the public readers (<see cref="TaxPressureMeasurement.Run"/>, the rig whose tables d049 §6 records).
/// Local conditions: WELL-OFF (provided and served), SERVED (public works only), NATURAL (as founded), POOR (housed for
/// a quarter of its people). The assertions are RELATIONS — progressive, delayed, ordered by local welfare,
/// recovering — not tuned turn numbers; the one turn pin is the regression record of the measured natural capital.
/// </summary>
public class TaxPressureTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static UnrestTuning U => Cfg.Needs!.Unrest!;

    private static readonly Lazy<Dictionary<(Condition, double), List<Reading>>> Runs = new(() =>
    {
        var runs = new Dictionary<(Condition, double), List<Reading>>();
        foreach (Condition c in new[] { Condition.WellOff, Condition.Served, Condition.Natural, Condition.Poor })
            foreach (double rate in new[] { 40.0, 70.0, 99.0, 100.0 })
                runs[(c, rate)] = TaxPressureMeasurement.Run(c, rate, 60);
        return runs;
    });

    private static List<Reading> R(Condition c, double rate) => Runs.Value[(c, rate)];

    private static int First(List<Reading> r, Func<Reading, bool> f) { int i = r.FindIndex(x => f(x)); return i < 0 ? -1 : i + 1; }

    // ------------------------------------------------------------------ A. normal tax → manageable pressure

    [Fact]
    public void A_NormalTax_AtTheAiCeiling_IsManageable_NoProtestNoRisingAnywhere_HappinessDipsButHolds()
    {
        foreach (Condition c in new[] { Condition.WellOff, Condition.Served, Condition.Natural, Condition.Poor })
        {
            List<Reading> r = R(c, 40.0);
            Assert.All(r, x => Assert.Equal(0.0, x.Protest));
            Assert.All(r, x => Assert.Equal(0.0, x.Risen));
            Assert.All(r, x => Assert.True(x.Controlled));
            Assert.True(r[^1].MaxT < U.ProtestOnsetGrievance, $"{c}: a 40 % levy reached the protest onset ({r[^1].MaxT})");
            Assert.True(r[^1].Happiness > 40.0, $"{c}: happiness collapsed under a normal levy ({r[^1].Happiness})");
            Assert.True(r[^1].Happiness < 100.0, $"{c}: the levy was not felt at all");
        }
    }

    // ------------------------------------------------------------------ B. high tax → progressive deterioration

    [Fact]
    public void B_HighTax_DeterioratesWelfareProgressively_OverTurns_NotAtTheEdict()
    {
        List<Reading> r = R(Condition.Natural, 70.0);
        // The edict lands in the state of turn 1: nothing is felt yet (no instant effect).
        Assert.Equal(0.0, r[0].MeanT);
        Assert.Equal(100.0, r[0].Happiness, 6);
        // Then the pressure accumulates and welfare falls, turn after turn.
        for (int t = 1; t < 8; t++)
        {
            Assert.True(r[t].MeanT > r[t - 1].MeanT, $"turn {t + 1}: levy grievance did not accumulate");
            Assert.True(r[t].Happiness < r[t - 1].Happiness, $"turn {t + 1}: happiness did not deteriorate");
        }
        // Protest comes only after years of it, and a high (not extreme) levy raises no one on an ordinary place.
        Assert.True(First(r, x => x.Protest > 0.0) >= 5, "protest at a 70 % levy came within four turns");
        Assert.All(r, x => Assert.Equal(0.0, x.Risen));
        Assert.All(r, x => Assert.True(x.Controlled));
    }

    // ------------------------------------------------------------------ C. 100 % → no immediate revolt

    /// <summary>
    /// THE REJECTED CORNER IS GONE, BY CONSTRUCTION: a 100 % levy at full reach no longer zeroes the provision reading
    /// that revolt reads, and from a standing start no segment can reach its tipping point within one turn of felt levy
    /// at any dt ≤ 10 years — the largest accrual the aggregation can charge the levy alone (Dignity at the
    /// satisfaction floor, every other need met) times 10 years stays below uprisingGrievance. So the earliest a
    /// segment can rise is turn 3 and the earliest a settlement can change hands is turn 4. MEASURED on every
    /// condition: the poor capital rises first, at turn 3, and falls at turn 5.
    /// </summary>
    [Fact]
    public void C_FullLevy_NeverRevoltsImmediately_TheEarliestRisingIsBoundByTheAccrual()
    {
        // The bound, computed with the system's own functions.
        NeedEntry[] needs = Cfg.Needs!.Needs;
        var sat = new List<double>(); var gate = new List<bool>(); var weight = new List<double>();
        int dignity = -1; double w = 0.0;
        foreach (NeedEntry n in needs)
        {
            if (!n.Bound) continue;
            if (n.FromTaxBurden) dignity = sat.Count;
            sat.Add(n.FromTaxBurden ? 0.0 : 1.0); gate.Add(NeedsGrievanceSystem.IsTierAGate(n.Id)); weight.Add(n.Weight); w += n.Weight;
        }
        double maxAccrual = NeedsGrievanceSystem.LevyAccrualPerYear(
            sat.ToArray(), gate.ToArray(), weight.ToArray(), dignity, w, Cfg.Needs.Aggregation, new double[sat.Count], new double[sat.Count]);
        Assert.True(maxAccrual * 10.0 < U.UprisingGrievance, $"one 10-year turn of total levy could reach the tipping point ({maxAccrual * 10.0})");

        foreach (Condition c in new[] { Condition.WellOff, Condition.Served, Condition.Natural, Condition.Poor })
        {
            List<Reading> r = R(c, 100.0);
            Assert.True(r[0].Controlled && r[1].Controlled && r[2].Controlled, $"{c}: lost within three turns of a 100 % levy");
            Assert.True(First(r, x => x.Risen > 0.0) is -1 or >= 3, $"{c}: a segment rose before turn 3");
            Assert.True(First(r, x => !x.Controlled) is -1 or >= 4, $"{c}: the settlement fell before turn 4");
            Assert.True(r[1].Happiness > 0.0, $"{c}: happiness zeroed one turn after the edict (the rejected corner)");
        }

        // And the corner itself: a declared 100 % levy at full reach is NOT a deprivation, so it is not revolt-ready.
        (WorldState world, PolityId p) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(world, p);
        GovernanceRigs.WellProvided(world, seat);
        world.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));
        Assert.Equal(1.0, Governance.EffectiveTaxRate(world, seat, Cfg));
        Assert.False(SettlementHappiness.IsRevoltReady(world, seat, Cfg));
        Assert.True(SettlementHappiness.Of(world, seat, Cfg) > 0.0);
    }

    // ------------------------------------------------------------------ D. sustained extreme → eventual instability

    /// <summary>
    /// On an ordinary capital, sustained total exaction ESCALATES: protest, then the segment's tipping point, then a
    /// growing portion of it in revolt (its levied work withheld — output falls), and in the long run the rebels carry
    /// the settlement and it throws off its ruler. MEASURED (dev world, seed 42, dt 10; d049 §6): protest from turn 5,
    /// peasants and artisans past their tipping point at 14, the settlement lost at 22 — pinned here as the regression
    /// record of the measured behaviour, not as a target.
    /// </summary>
    [Fact]
    public void D_SustainedExtremeTaxation_EventuallyProducesSeriousInstability_ThroughStages()
    {
        List<Reading> r = R(Condition.Natural, 100.0);
        int protest = First(r, x => x.Protest > 0.0), risen = First(r, x => x.Risen > 0.0);
        int rebels = First(r, x => x.Rebels > 0.0), lost = First(r, x => !x.Controlled);
        Assert.True(protest > 0 && risen > protest && rebels >= risen && lost > rebels, $"stages out of order: {protest}/{risen}/{rebels}/{lost}");
        Assert.True(r[rebels - 1].Output < r[protest - 1].Output, "the rising withheld no work");
        Assert.Equal((5, 14, 22), (protest, risen, lost));   // MEASURED pin (d049 §6), not a target
    }

    // ------------------------------------------------------------------ E. a revolt affects a segment

    /// <summary>
    /// A POOR capital under a high levy (70 %): its segments pass their tipping point and a PORTION of them stays in
    /// open revolt for the rest of the run — their levied work withheld — while the settlement remains its ruler's,
    /// because the rebels never carry it (fewer than unrest.uprisingPopulationShare of its people). MEASURED: rebels
    /// 3–6 % of the people from turn 14, the capital held for all 60 turns.
    /// </summary>
    [Fact]
    public void E_ARising_AffectsOnlyThePortionOfTheSegmentInRevolt_TheSettlementStaysItsRulers()
    {
        List<Reading> r = R(Condition.Poor, 70.0);
        int risen = First(r, x => x.Risen > 0.0);
        Assert.True(risen > 0, "no segment of the poor capital rose under a 70 % levy");
        Assert.All(r, x => Assert.True(x.Controlled, $"turn {x.Turn}: the settlement fell"));
        var after = r.Skip(risen + 2).ToList();
        Assert.Contains(after, x => x.Rebels > 0.0);
        Assert.All(after, x => Assert.InRange(x.Rebels, 0.0, U.UprisingPopulationShare));
        Assert.All(after, x => Assert.True(x.Output < 1.0, $"turn {x.Turn}: the rebels' work was not withheld"));
    }

    // ------------------------------------------------------------------ F. prosperity mitigates

    [Fact]
    public void F_ProvisionAndServices_ReduceAndDelayThePressure_AWellOffPlaceBearsTheExtremeLevy()
    {
        foreach (double rate in new[] { 70.0, 99.0, 100.0 })
        {
            double well = R(Condition.WellOff, rate).Max(x => x.MaxT);
            double served = R(Condition.Served, rate).Max(x => x.MaxT);
            double natural = R(Condition.Natural, rate).Take(12).Max(x => x.MaxT);   // before any rising feeds back
            Assert.True(well < served && served < natural, $"{rate}%: well-off {well}, served {served}, natural {natural}");
        }
        // The well-off and the served capital never rise, even at 100 %; the ordinary one does.
        Assert.All(R(Condition.WellOff, 100.0), x => Assert.Equal(0.0, x.Risen));
        Assert.All(R(Condition.Served, 100.0), x => Assert.Equal(0.0, x.Risen));
        Assert.Contains(R(Condition.Natural, 100.0), x => x.Risen > 0.0);
        // ... and keep a working welfare reading, turn for turn at least the ordinary place's, where the ordinary place
        // is consumed. MEASURED (d049 §6): the well-off capital settles at happiness 32.2 with mild protest (p 0.19)
        // under a permanent 100 % levy; the ordinary one reads 0 by turn 20 and is lost at turn 22.
        List<Reading> wellOff = R(Condition.WellOff, 100.0), ordinary = R(Condition.Natural, 100.0);
        for (int t = 0; t < 20; t++)
            Assert.True(wellOff[t].Happiness >= ordinary[t].Happiness, $"turn {t + 1}: well-off {wellOff[t].Happiness} < ordinary {ordinary[t].Happiness}");
        Assert.True(wellOff[^1].Happiness > 25.0, $"the well-off capital's welfare collapsed ({wellOff[^1].Happiness})");
        Assert.True(wellOff[^1].Protest < 0.5, $"the well-off capital is in heavy protest ({wellOff[^1].Protest})");
    }

    // ------------------------------------------------------------------ G. poverty aggravates

    [Fact]
    public void G_ThePoorSettlement_AccumulatesMorePressure_FromTheSameRate_AndRisesSooner()
    {
        foreach (double rate in new[] { 40.0, 70.0, 99.0 })
        {
            List<Reading> poor = R(Condition.Poor, rate), natural = R(Condition.Natural, rate);
            // While both are still under the levy: once the poor capital has thrown off its ruler (99 %: turn 5) its
            // levy is gone and its pressure decays — the comparison is of the same levy on two places.
            int lost = First(poor, x => !x.Controlled);
            int until = lost < 0 ? 10 : Math.Min(10, lost - 1);
            for (int t = 1; t < until; t++)
                Assert.True(poor[t].MeanT > natural[t].MeanT, $"{rate}% turn {t + 1}: poor {poor[t].MeanT} vs natural {natural[t].MeanT}");
            Assert.True(poor[1].Felt > natural[1].Felt, $"{rate}%: the poor capital felt the levy less");
        }
        // Gradual even for the poor: no protest in the first two turns, no rising before turn 3.
        List<Reading> p99 = R(Condition.Poor, 99.0);
        Assert.Equal(0.0, p99[1].Protest);
        Assert.True(First(p99, x => x.Risen > 0.0) < First(R(Condition.Natural, 99.0), x => x.Risen > 0.0));
        Assert.True(First(p99, x => x.Risen > 0.0) >= 3);
    }

    // ------------------------------------------------------------------ H. cut the levy → recovery

    [Fact]
    public void H_CuttingTheLevy_LetsThePressureDecay_AndWelfareRecover_OverTurns()
    {
        const int cut = 10;
        List<Reading> r = TaxPressureMeasurement.Run(Condition.Natural, 99.0, 40, cutAt: cut, cutTo: 0.0);
        Assert.True(r[cut - 1].Protest > 0.0, "the run was not under pressure when the levy was cut");
        // The cut stamped turn 10 is in force from turn 11, so the stock falls from turn 12 — and keeps falling.
        for (int t = cut + 1; t < r.Count; t++)
            Assert.True(r[t].MeanT < r[t - 1].MeanT, $"turn {t + 1}: the levy grievance did not decay after the cut");
        // Recovery takes turns (memory), and it comes: below the protest onset, welfare back.
        Assert.True(r[cut + 1].Happiness < 50.0, "welfare recovered at once — no memory");
        Assert.True(r[cut + 9].MeanT < U.ProtestOnsetGrievance);
        Assert.True(r[^1].Happiness > 90.0);
        Assert.All(r, x => Assert.Equal(0.0, x.Risen));
        Assert.All(r, x => Assert.True(x.Controlled));
    }

    // ------------------------------------------------------------------ determinism

    [Fact]
    public void TheRunIsDeterministic_TwoRunsAgreeReadingForReading()
    {
        List<Reading> a = TaxPressureMeasurement.Run(Condition.Poor, 100.0, 12);
        List<Reading> b = TaxPressureMeasurement.Run(Condition.Poor, 100.0, 12);
        Assert.Equal(a, b);
    }
}
