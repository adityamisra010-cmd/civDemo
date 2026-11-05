using Proto.M7.Econ;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Worldgen;

namespace Proto.M7.Econ.Tests;

/// <summary>
/// Tests added for the slice-F verification findings (F1, F2) and the surviving mutants (V3, V4, V6). Each test asserts a
/// property the prototype ought to have, not just a value the code happens to produce.
/// </summary>
public class EconVerificationFixTests
{
    // ---------- F2: rations and haul are the EXACT ceiling of the decimal-valued bill ----------

    /// <summary>With the shipped parameters (1.0 grain/person-year, 16 km per cost unit, 40 km/day, 360-day year) the
    /// ration of an integer route cost c is exactly ceil(personnel × c × 16 / (40 × 360)) = ceil(personnel × c × 2 / 1800).
    /// Before the fix, 46 of these 180,000 cases came out one unit high from double rounding (e.g. 225 people, cost 44:
    /// 225 × 17.6 / 360 = 11.000000000000002 → 12).</summary>
    [Fact]
    public void F2_Rations_AreTheExactCeiling_OverAnIntegerGrid()
    {
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        int mismatches = 0;
        string first = "";
        for (long personnel = 1; personnel <= 3000; personnel++)
            for (int cost = 1; cost <= 60; cost++)
            {
                MovementEstimate e = MovementFood.FromRouteCost(p, 2, cost, 1.0, personnel, HaulBasis.FullLoad);
                long num = personnel * cost * 2, den = 1800;
                long exact = (num + den - 1) / den;
                if (e.RationUnits != exact)
                {
                    mismatches++;
                    if (first.Length == 0) first = $"personnel={personnel} cost={cost} got={e.RationUnits} exact={exact}";
                }
            }
        Assert.True(mismatches == 0, mismatches + " mismatches; first " + first);
    }

    [Fact]
    public void F2_TheReportedCase_225PeopleOver44CostUnits_Eat11Rations()
    {
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        MovementEstimate e = MovementFood.FromRouteCost(p, 2, 44.0, 1.0, 225, HaulBasis.FullLoad);
        Assert.Equal(11, e.RationUnits);                       // 225 × 44 × 16 / 14,400 = 11 exactly
        Assert.Equal(78, e.HaulUnits);                         // ceil(11 × 44 × 0.16) = ceil(77.44)
    }

    /// <summary>FULL haul = ceil(rations × 1.0 × cost × 0.16) = ceil(rations × cost × 16 / 100); MEAN = ceil(rations × cost × 8 / 100).
    /// A guard that the exact-decimal path keeps the haul exact too (0.16 is not a dyadic double).</summary>
    [Fact]
    public void F2_Haul_IsTheExactCeiling_BothBases_OverAnIntegerGrid()
    {
        MovementParams p = MovementParams.From(EconWorld.SimCfg, Worlds.Turn1Seed42);
        int mismatches = 0;
        string first = "";
        for (long personnel = 1; personnel <= 3000; personnel += 7)
            for (int cost = 1; cost <= 60; cost++)
            {
                MovementEstimate f = MovementFood.FromRouteCost(p, 2, cost, 1.0, personnel, HaulBasis.FullLoad);
                MovementEstimate m = MovementFood.FromRouteCost(p, 2, cost, 1.0, personnel, HaulBasis.MeanLoad);
                long full = (f.RationUnits * cost * 16 + 99) / 100;
                long mean = (f.RationUnits * cost * 8 + 99) / 100;
                if (f.HaulUnits != full || m.HaulUnits != mean)
                {
                    mismatches++;
                    if (first.Length == 0) first = $"personnel={personnel} cost={cost} full={f.HaulUnits}/{full} mean={m.HaulUnits}/{mean}";
                }
            }
        Assert.True(mismatches == 0, mismatches + " mismatches; first " + first);
    }

    // ---------- F1: the allocator's measured inputs and their readings ----------

    private static PolitySnapshot Snap(long stock, long harvest, long requirement, double headroomPerYear, bool unbounded = false) =>
        new(Turn: 25, Polity: 1, Player: true, Settlements: 12, Population: 6_000, Adults: 3_000, Soldiers: 20,
            GrainStock: stock, FoodStock: stock, GrainProducedLastStep: harvest, FoodProducedLastStep: harvest,
            FoodEatenLastStep: requirement, CivilianRequirementLastStep: requirement, MaxDeficitRatio: 0.0,
            GrowthHeadroomPersonsPerYear: headroomPerYear, GrowthHeadroomUnbounded: unbounded, DtYears: 10.0, UnfedUnits: 0);

    private static LedgerTotals Step(long grainEaten, long spoilage) => new(0, grainEaten, spoilage, 0, 0, 0, 0, 0, 0);

    /// <summary>The as-shipped reading: free = grain stock + last grain harvest; R = the all-food requirement; G = vacancy × dt.
    /// (Kills V4: "free = grain stock only".)</summary>
    [Fact]
    public void F1_AsShipped_FreeIsStockPlusLastHarvest_RIsTheRequirement_GIsHeadroomTimesDt()
    {
        AllocatorInputs x = MeasuredAllocation.Inputs(Snap(6_899, 98_278, 53_308, 5_694.1), Step(47_364, 31_800), AllocatorReading.AsShipped);
        Assert.Equal(105_177, x.Free);
        Assert.Equal(53_308, x.CivilianRequirement);
        Assert.Equal(56_941, x.GrowthDemand);   // ceil(5,694.1 × 10)
    }

    [Fact]
    public void F1_Readings_ChangeExactlyTheirOwnInput()
    {
        PolitySnapshot s = Snap(6_899, 98_278, 53_308, 5_694.1);
        LedgerTotals d = Step(47_364, 31_800);
        AllocatorInputs grainR = MeasuredAllocation.Inputs(s, d, new AllocatorReading(false, true, false));
        Assert.Equal(new AllocatorInputs(105_177, 47_364, 56_941), grainR);
        AllocatorInputs net = MeasuredAllocation.Inputs(s, d, new AllocatorReading(true, false, false));
        Assert.Equal(new AllocatorInputs(105_177 - 31_800, 53_308, 56_941), net);
        // Spoilage larger than the store never makes free negative.
        Assert.Equal(0, MeasuredAllocation.Inputs(Snap(10, 5, 0, 0.0), Step(0, 100), new AllocatorReading(true, false, false)).Free);

        MovementEstimate bill = new(true, 9, 107.5, 1_720.0, 900.0, 43.0, 1_000, 120, 2_057);
        Assert.Equal(new MoveRequest(1, 0, 2_177, 43), MeasuredAllocation.Move(1, 0, bill, AllocatorReading.AsShipped));
        Assert.Equal(new MoveRequest(1, 0, 2_057, 43), MeasuredAllocation.Move(1, 0, bill, new AllocatorReading(false, false, true)));
        // Days round UP to whole days and are at least 1.
        Assert.Equal(18, MeasuredAllocation.Move(1, 0, bill with { TravelDays = 17.2 }, AllocatorReading.AsShipped).Days);
        Assert.Equal(1, MeasuredAllocation.Move(1, 0, bill with { TravelDays = 0.0 }, AllocatorReading.AsShipped).Days);
    }

    /// <summary>F1 as a pinned fact: on the measured turn-25 numbers (raw calibration file, seeds 2, 42, 7) the allocator's verdict
    /// flips with the reading. A report that states one verdict must name its reading.</summary>
    [Theory]
    // seed, stock, harvest, R(all food), grain eaten, spoilage, G/yr·10 (as G), 1k draw, 1k days, 50k draw, 50k days, shipped, grain-R, net-of-spoilage
    [InlineData(7, 6_551, 82_179, 50_950, 45_045, 24_022, 41_660, 858, 27, 42_632, 27, ConstraintStage.MilitaryRestricted, ConstraintStage.GrowthReduced, ConstraintStage.MilitaryRestricted)]
    [InlineData(2, 6_893, 97_152, 53_036, 47_327, 31_188, 55_375, 713, 25, 35_529, 25, ConstraintStage.GrowthReduced, ConstraintStage.GrowthReduced, ConstraintStage.MilitaryRestricted)]
    [InlineData(42, 6_452, 103_873, 50_045, 44_391, 36_255, 66_791, 925, 28, 45_842, 28, ConstraintStage.GrowthReduced, ConstraintStage.GrowthReduced, ConstraintStage.MilitaryRestricted)]
    public void F1_TheVerdictOnMeasuredNumbers_DependsOnTheReading(
        int seed, long stock, long harvest, long r, long grainEaten, long spoilage, long g, long d1, long n1, long d2, long n2,
        ConstraintStage shipped, ConstraintStage grainR, ConstraintStage netOfSpoilage)
    {
        _ = seed;   // free = stock + harvest reproduces the raw file's free column (88,730 / 104,045 / 110,325)
        MoveRequest[] moves = [new(1, 0, d1, n1), new(2, 1, d2, n2)];
        Assert.Equal(shipped, LogisticsPriority.Resolve(stock + harvest, r, g, moves).Stage);
        Assert.Equal(grainR, LogisticsPriority.Resolve(stock + harvest, grainEaten, g, moves).Stage);
        Assert.Equal(netOfSpoilage, LogisticsPriority.Resolve(stock + harvest - spoilage, r, g, moves).Stage);
    }

    // ---------- V3 / V6: PolityMeasure on a MULTI-polity world and on the unbounded-headroom arm ----------

    private static readonly Lazy<WorldState> TwoPolitiesTurn0 = new(() =>
        WorldFounding.Found(EconWorld.WorldgenCfg with { AiEmpires = 1 }, EconWorld.SimCfg, 42));

    /// <summary>Each polity counts only ITS formations (kills V3: owner filter dropped). The founding gives every polity a
    /// 20-person warband (ADR-055), so with two polities each must report 20, not 40, and the per-polity sums must equal the
    /// world's tables.</summary>
    [Fact]
    public void V3_SoldiersArePerPolity_OnATwoPolityWorld()
    {
        WorldState w = TwoPolitiesTurn0.Value;
        PolitySnapshot[] all = PolityMeasure.All(w, EconWorld.SimCfg);
        Assert.Equal(2, all.Length);
        Assert.All(all, p => Assert.Equal(20, p.Soldiers));
        long personnel = 0;
        for (int i = 0; i < w.FormationPersonnel.Count; i++) personnel += w.FormationPersonnel[i].Count.Value;
        Assert.Equal(personnel, all.Sum(p => p.Soldiers));
        long people = 0;
        for (int i = 0; i < w.Buckets.Count; i++) people += w.Buckets[i].Count.Value;
        Assert.Equal(people, all.Sum(p => p.Population));
        Assert.Equal(w.Settlements.Count, all.Sum(p => p.Settlements));
    }

    /// <summary>On the turn-0 world no settlement has a catchment row yet, so FoodHeadroom.Vacancy is +∞ (its null arm): the
    /// snapshot must flag the headroom UNBOUNDED and keep the finite sum finite (kills V6: +∞ folded into the sum).</summary>
    [Fact]
    public void V6_AnInfiniteVacancy_IsFlaggedUnbounded_NotAddedToTheHeadroom()
    {
        WorldState w = TwoPolitiesTurn0.Value;
        var baskets = new BasketBook(EconWorld.SimCfg.Needs!, EconWorld.SimCfg.Goods!);
        Assert.True(double.IsPositiveInfinity(FoodHeadroom.Vacancy(w, w.Settlements[0].Id, EconWorld.SimCfg.Consumption.CohortWeights, baskets)));
        PolitySnapshot[] all = PolityMeasure.All(w, EconWorld.SimCfg);
        Assert.All(all, p =>
        {
            Assert.True(p.GrowthHeadroomUnbounded);
            Assert.True(double.IsFinite(p.GrowthHeadroomPersonsPerYear));
        });
        // ...and on the stepped world (every settlement has its catchment row) the headroom is bounded and finite.
        PolitySnapshot t1 = PolityMeasure.All(Worlds.Turn1Seed42, EconWorld.SimCfg)[0];
        Assert.False(t1.GrowthHeadroomUnbounded);
        Assert.True(double.IsFinite(t1.GrowthHeadroomPersonsPerYear) && t1.GrowthHeadroomPersonsPerYear >= 0.0);
    }

    // ---------- V4: Program.Run's allocator row is the as-shipped reading of its own measurements ----------

    /// <summary>Program.Run had no test. Run it on the smallest real input (seed 42, checkpoint and twin at turn 1, 1-turn
    /// horizon) and check that the allocator row's free/R/G are the as-shipped reading of the turn-1 snapshot, and that the
    /// sensitivity table is printed for the single-polity calibration world.</summary>
    [Fact]
    public void V4_ProgramRun_AllocatorRow_IsTheAsShippedReadingOfTheMeasuredSnapshot()
    {
        string report = Program.Run([42], [1], [1], 1);
        PolitySnapshot pl = PolityMeasure.All(Worlds.Turn1Seed42, EconWorld.SimCfg)[0];
        Assert.True(pl.Player);
        AllocatorInputs x = MeasuredAllocation.Inputs(pl, default, AllocatorReading.AsShipped);
        Assert.Equal(pl.GrainStock + pl.GrainProducedLastStep, x.Free);
        string[] lines = report.Split('\n');
        int header = Array.FindIndex(lines, l => l.StartsWith("| free | R | G |", StringComparison.Ordinal));
        Assert.True(header >= 0, "allocator table missing");
        string[] cells = lines[header + 2].Split('|', StringSplitOptions.TrimEntries);
        Assert.Equal(x.Free.ToString(System.Globalization.CultureInfo.InvariantCulture), cells[1]);
        Assert.Equal(x.CivilianRequirement.ToString(System.Globalization.CultureInfo.InvariantCulture), cells[2]);
        Assert.Equal(x.GrowthDemand.ToString(System.Globalization.CultureInfo.InvariantCulture), cells[3]);
        Assert.Contains("Allocator input sensitivity at turn 1", report, StringComparison.Ordinal);
        Assert.Contains("| as shipped", report, StringComparison.Ordinal);
    }
}
