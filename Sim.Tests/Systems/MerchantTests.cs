using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T4.11 — MERCHANTS, the class that emerges on trade volume.
///
/// Merchants are PEOPLE, not a bookkeeping actor: they are a registry class with
/// a D-020 emergence predicate, so they occupy buckets, eat, age, migrate and
/// carry needs exactly as peasants and artisans do. That is what makes them real
/// simulation actors rather than a façade, and it is why this file tests the
/// emergence CHAIN — trade moves goods, the movement is published, the predicate
/// reads the publication — instead of testing a merchant object in isolation.
/// </summary>
public class MerchantTests
{
    private static ClassId MerchantClass()
    {
        ClassEntry[] classes = TestConfigs.Sim().Registries!.Classes;
        for (int i = 0; i < classes.Length; i++)
            if (classes[i].Name == "Merchants") return new ClassId(classes[i].Id);
        throw new InvalidOperationException("no Merchants class in the registry");
    }

    [Fact]
    public void TheRegistryCarriesMerchantsWithATradeVolumePredicate()
    {
        ClassEntry[] classes = TestConfigs.Sim().Registries!.Classes;
        ClassEntry merchants = Array.Find(classes, c => c.Name == "Merchants")
            ?? throw new InvalidOperationException("no Merchants class");

        Assert.Contains("trade_volume", merchants.Emerge);
        Assert.False(string.IsNullOrWhiteSpace(merchants.Recede),
            "merchants need a recede predicate — the latch is what survives episodic volume");
    }

    [Fact]
    public void TradeVolumeIsPublishedFromRealisedFlows()
    {
        // The link in the chain that makes emergence possible at all: what the
        // predicate reads must be the movement that actually happened.
        var w = new WorldState(3);
        var a = new SettlementId(0);
        var b = new SettlementId(1);
        w.Settlements.Add(new SettlementRow(a, 0, 0));
        w.Settlements.Add(new SettlementRow(b, 1, 0));
        w.TradeFlows.Add(new TradeFlowRow(a, b, new GoodId(2), 700));
        w.TradeFlows.Add(new TradeFlowRow(b, a, new GoodId(3), 40));

        WorldState next = new TurnExecutor(
            EraTableLoader.Load("""{ "bands": [ { "name": "f", "startYear": 0, "endYear": 100000, "dtYears": 10 } ] }"""),
            [SystemCatalog.ClassMobility(TestConfigs.Sim())]).Step(w);

        // Both settlements are endpoints of both flows, so both see 740.
        Assert.Equal(740.0, VarOf(next, a, Variables.TradeVolume));
        Assert.Equal(740.0, VarOf(next, b, Variables.TradeVolume));
    }

    [Fact]
    public void ASettlementWithNoTradeHasZeroVolume_SoNoMerchantCanEmergeThere()
    {
        var w = new WorldState(3);
        var a = new SettlementId(0);
        w.Settlements.Add(new SettlementRow(a, 0, 0));

        WorldState next = new TurnExecutor(
            EraTableLoader.Load("""{ "bands": [ { "name": "f", "startYear": 0, "endYear": 100000, "dtYears": 10 } ] }"""),
            [SystemCatalog.ClassMobility(TestConfigs.Sim())]).Step(w);

        Assert.Equal(0.0, VarOf(next, a, Variables.TradeVolume));
    }

    [Fact]
    public void MerchantsAreCarriedByEveryFoundedSettlement_AsAnInactiveClass()
    {
        // Class-A state: the row must exist from founding, at zero and inactive,
        // or the emergence latch has nothing to flip and merchants could never
        // appear however much trade there is.
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), 42);
        ClassId merchant = MerchantClass();

        int rows = 0;
        for (int i = 0; i < w.ClassStates.Count; i++)
            if (w.ClassStates[i].Class == merchant) { rows++; Assert.Equal(0, w.ClassStates[i].Active); }

        Assert.Equal(w.Settlements.Count, rows);
    }

    [Fact]
    public void MerchantsEmergeInAFoundedWorldOnceTradeVolumeCrossesTheThreshold()
    {
        // THE INTEGRATION TEST, and the one that would catch a merchant class
        // that is configured but unreachable. It runs the REAL pipeline on the
        // canonical world and asserts the latch flips somewhere — the measured
        // series reaches 3042 units on a settlement by turn 650, well past the
        // threshold of 200.
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(TestConfigs.Sim(), TestConfigs.Worldgen())));

        WorldState w = exec.Run(
            WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), 42), 650);
        ClassId merchant = MerchantClass();

        int active = 0;
        for (int i = 0; i < w.ClassStates.Count; i++)
            if (w.ClassStates[i].Class == merchant && w.ClassStates[i].Active != 0) active++;

        Assert.True(active > 0,
            "no settlement ever became a merchant town in 650 turns — either the predicate "
            + "threshold is above anything the world produces, or trade_volume is not reaching "
            + "the predicate. Merchants would be configured but unreachable.");
    }

    private static double VarOf(WorldState w, SettlementId s, int varId)
    {
        for (int i = 0; i < w.Variables.Count; i++)
            if (w.Variables[i].Settlement == s && w.Variables[i].VarId == varId)
                return w.Variables[i].Value;
        return double.NaN;
    }
}
