using Proto.M7.Econ;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Proto.M7.Econ.Tests;

/// <summary>Shared, read-only fixtures (each world is built once per test process and never mutated).</summary>
internal static class Worlds
{
    private static readonly Lazy<WorldState> T1 = new(() => EconWorld.Executor().Step(EconWorld.Found(42)));
    public static WorldState Turn1Seed42 => T1.Value;
}

/// <summary>
/// GOLDEN NEUTRALITY. The prototype reads Sim.* and never writes it: (1) its founding/stepping recipe reproduces the pinned
/// FOUNDED golden byte for byte (so the canonical trajectory is untouched by the prototype's presence), (2) every prototype
/// query leaves a world's hash unchanged, (3) its measurements are exact sums of the state tables.
/// </summary>
public class EconWorldTests
{
    [Fact]
    public void Recipe_ReproducesThePinnedFoundedGolden_Seed42Turn300()
    {
        WorldState w = EconWorld.Executor().Run(EconWorld.Found(42), 300);
        Assert.Equal(EconWorld.FoundedGoldenSeed42Turn300, WorldHash.ComputeHex(w));
    }

    [Fact]
    public void PrototypeQueries_DoNotChangeTheWorld()
    {
        WorldState w = Worlds.Turn1Seed42;
        string before = WorldHash.ComputeHex(w);
        SimConfig cfg = EconWorld.SimCfg;
        _ = PolityMeasure.All(w, cfg);
        _ = Calibration.MovementTable(w, 50_000);
        PolityId player = Calibration.Player(w);
        _ = Calibration.FirstRecruitable(w, cfg, player, Calibration.Controlled(w, player)[0], out _);
        _ = LedgerTotals.Of(w, cfg);
        Assert.Equal(before, WorldHash.ComputeHex(w));
    }

    [Fact]
    public void Snapshot_IsAnExactSumOfTheTables()
    {
        WorldState w = Worlds.Turn1Seed42;
        SimConfig cfg = EconWorld.SimCfg;
        PolitySnapshot[] all = PolityMeasure.All(w, cfg);
        long bucketPeople = 0, adults = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            bucketPeople += w.Buckets[i].Count.Value;
            if (BandViews.IsAdult(w.Buckets[i].CohortIdx)) adults += w.Buckets[i].Count.Value;
        }
        long personnel = 0;
        for (int i = 0; i < w.FormationPersonnel.Count; i++) personnel += w.FormationPersonnel[i].Count.Value;
        // Every settlement of an order-less turn-1 world is controlled by some polity, so the polity sums are the world sums.
        Assert.Equal(bucketPeople, all.Sum(x => x.Population));
        Assert.Equal(adults, all.Sum(x => x.Adults));
        Assert.Equal(personnel, all.Sum(x => x.Soldiers));
        Assert.Equal(w.Settlements.Count, all.Sum(x => x.Settlements));
        Assert.Single(all, x => x.Player);
        Assert.Equal(10.0, all[0].DtYears);   // era-pacing.json: the first band is 10-year turns
        Assert.True(ConservationAuditor.IsConserved(w, out string report), report);
    }

    [Fact]
    public void FoundingWarband_IsTwentyPeoplePerPolity_AndEatsAtTheShippedRate()
    {
        // ADR-055: the founding warband draws 20 adults; they eat upkeepGrainPerPersonYear x dtYears = 20 x 1.0 x 10 = 200
        // grain per 10-year step, sunk under FormationUpkeep (measured: turn-1 ledger).
        WorldState w0 = EconWorld.Found(42), w1 = Worlds.Turn1Seed42;
        SimConfig cfg = EconWorld.SimCfg;
        PolitySnapshot[] polities = PolityMeasure.All(w0, cfg);
        Assert.All(polities, p => Assert.Equal(20, p.Soldiers));
        long upkeep = LedgerTotals.Of(w1, cfg).Minus(LedgerTotals.Of(w0, cfg)).FormationUpkeep;
        Assert.Equal(20L * 10 * polities.Length, upkeep);
    }

    [Fact]
    public void RecruitmentTwin_ExistingRulesAcceptFormations_TheWorldStaysConserved_AndBranchesDiffer()
    {
        WorldState w = Worlds.Turn1Seed42;
        RecruitTwin tw = Calibration.MaxRecruitment(42, w, horizon: 2, maxFormationsPerSettlement: 1);
        Assert.Equal("Warband", tw.Identity);
        Assert.True(tw.OrdersIssued > 0);
        // The order stamped turn T is applied in step T -> T+1 (OrderKind 16 DELIVERY).
        Assert.True(tw.SoldiersAfter > tw.SoldiersBefore);
        Assert.Equal(0, (tw.SoldiersAfter - tw.SoldiersBefore) % tw.PersonnelPerFormation);
        Assert.Equal(tw.BaseAtT1.Soldiers, tw.SoldiersBefore);
        // Production in step T+1 runs before MilitarySystem (pipeline slot 4 vs 23): the labour loss first shows in step T+2.
        Assert.Equal(tw.BaseAtT1.FoodProducedLastStep, tw.TwinAtT1.FoodProducedLastStep);
        Assert.True(tw.TwinAtT1.Population < tw.BaseAtT1.Population);   // soldiers leave the buckets (they are not deleted)
        Assert.Equal(tw.BaseAtT1.Population + tw.BaseAtT1.Soldiers, tw.TwinAtT1.Population + tw.TwinAtT1.Soldiers);
        Assert.True(tw.TwinDelta.FormationUpkeep > tw.BaseDelta.FormationUpkeep);
    }
}
