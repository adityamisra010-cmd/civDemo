using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// TEMPORARY measurement harness for T4.21-4 step 2. Not a test of the system;
// deleted before the packet's final commit.
public class ZzMeasureT4214
{
    private sealed class Tally
    {
        public int Onsets, Famine, Severe, Stress, Normal, FamineNone, FamineDisaster,
                   FamineAbandon, FamineBoth;
        public double SettlementYears;
        public long StarvTurnsMaxFamine, StarvTurnsMaxSevere, StarvTurnsMaxStress, StarvTurnsMaxNormal;
        public int TurnsMaxFamine, TurnsMaxSevere, TurnsMaxStress, TurnsMaxNormal;
        public long StarvTotal;
        public int V2Violations;
        public int Turns;
        public long FinalPop;
    }

    private static Tally Run(ulong seed, int turns, bool dev, double hazard)
    {
        SimConfig baseCfg = TestConfigs.Sim();
        SimConfig cfg = baseCfg with { Disaster = baseCfg.Disaster with { HazardPerYear = hazard } };
        WorldgenConfig wg = dev ? TestConfigs.DevWorldgen() : TestConfigs.Worldgen();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, wg)));
        WorldState world = WorldFounding.Found(wg, cfg, seed, null);
        var t = new Tally();
        WorldState prev = world;
        long prevStarvCum = 0;
        for (int turn = 1; turn <= turns; turn++)
        {
            WorldState next = exec.Step(world);

            // onsets, measured on `next` against `world`
            for (int r = 0; r < next.Disasters.Count; r++)
            {
                DisasterRow row = next.Disasters[r];
                if (row.Kind != 1) continue;
                bool wasActive = false;
                for (int q = 0; q < world.Disasters.Count; q++)
                    if (world.Disasters[q].Settlement == row.Settlement)
                    { wasActive = world.Disasters[q].RemainingYears > 0.0; break; }
                if (!wasActive) t.Onsets++;
            }

            // starvation delta of THIS step, driven by PREV (= `world`)
            long cum = 0;
            for (int i = 0; i < next.LedgerFlows.Count; i++)
                if (next.LedgerFlows[i].Quantity == ConservedQuantityIds.Population
                    && next.LedgerFlows[i].Reason == ReasonIds.Starvation)
                    cum = next.LedgerFlows[i].TotalSunk;
            long starvThisTurn = cum - prevStarvCum;
            prevStarvCum = cum;
            t.StarvTotal += starvThisTurn;

            // classify every settlement on `world` (what Demographics read)
            int maxKind = 0;
            for (int i = 0; i < world.Settlements.Count; i++)
            {
                SettlementId sid = world.Settlements[i].Id;
                FoodStateKind k = FoodState.Of(world, sid, cfg, out FamineReason reason);
                if ((int)k > maxKind) maxKind = (int)k;
                switch (k)
                {
                    case FoodStateKind.Famine:
                        t.Famine++;
                        if (reason == FamineReason.None) t.FamineNone++;
                        else if (reason == FamineReason.Disaster) t.FamineDisaster++;
                        else if (reason == FamineReason.Abandonment) t.FamineAbandon++;
                        else t.FamineBoth++;
                        break;
                    case FoodStateKind.Severe: t.Severe++; break;
                    case FoodStateKind.Stress: t.Stress++; break;
                    default: t.Normal++; break;
                }
            }
            switch (maxKind)
            {
                case 3: t.TurnsMaxFamine++; t.StarvTurnsMaxFamine += starvThisTurn; break;
                case 2: t.TurnsMaxSevere++; t.StarvTurnsMaxSevere += starvThisTurn; break;
                case 1: t.TurnsMaxStress++; t.StarvTurnsMaxStress += starvThisTurn;
                        if (starvThisTurn != 0) t.V2Violations++; break;
                default: t.TurnsMaxNormal++; t.StarvTurnsMaxNormal += starvThisTurn;
                        if (starvThisTurn != 0) t.V2Violations++; break;
            }

            t.SettlementYears += world.Settlements.Count * next.Clock.DtYears;
            t.Turns++;
            prev = world;
            world = next;
        }
        long pop = 0;
        for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
        t.FinalPop = pop;
        return t;
    }

    private static string Inv(double v) => v.ToString("G9", CultureInfo.InvariantCulture);

    [Fact]
    public void Measure()
    {
        string outPath = Environment.GetEnvironmentVariable("T4214_OUT") ?? "/tmp/t4214.txt";
        double hazard = double.Parse(Environment.GetEnvironmentVariable("T4214_HAZARD") ?? "0.01",
            CultureInfo.InvariantCulture);
        int turns = int.Parse(Environment.GetEnvironmentVariable("T4214_TURNS") ?? "300",
            CultureInfo.InvariantCulture);
        bool dev = (Environment.GetEnvironmentVariable("T4214_DEV") ?? "0") == "1";
        string seedSpec = Environment.GetEnvironmentVariable("T4214_SEEDS") ?? "42";
        var seeds = new List<ulong>();
        foreach (string part in seedSpec.Split(','))
        {
            if (part.Contains('-'))
            {
                string[] ab = part.Split('-');
                for (ulong s = ulong.Parse(ab[0]); s <= ulong.Parse(ab[1]); s++) seeds.Add(s);
            }
            else seeds.Add(ulong.Parse(part));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# hazard={Inv(hazard)} turns={turns} dev={dev} seeds={seedSpec}");
        sb.AppendLine("seed,onsets,settlementYears,onsetsPerSettlementCentury,famine,severe,stress,normal," +
                      "fReasonNone,fDisaster,fAbandon,fBoth,starvTotal,starvMaxFamineTurns,starvMaxSevereTurns," +
                      "starvMaxStressTurns,starvMaxNormalTurns,turnsMaxFamine,turnsMaxSevere,turnsMaxStress," +
                      "turnsMaxNormal,v2Violations,finalPop");
        int totalOnsets = 0; double totalSy = 0; int totFam = 0, totSev = 0, totStr = 0, totNone = 0, totV2 = 0;
        foreach (ulong seed in seeds)
        {
            Tally t = Run(seed, turns, dev, hazard);
            totalOnsets += t.Onsets; totalSy += t.SettlementYears;
            totFam += t.Famine; totSev += t.Severe; totStr += t.Stress; totNone += t.FamineNone;
            totV2 += t.V2Violations;
            sb.AppendLine(string.Join(",", seed, t.Onsets, Inv(t.SettlementYears),
                Inv(t.Onsets / (t.SettlementYears / 100.0)), t.Famine, t.Severe, t.Stress, t.Normal,
                t.FamineNone, t.FamineDisaster, t.FamineAbandon, t.FamineBoth, t.StarvTotal,
                t.StarvTurnsMaxFamine, t.StarvTurnsMaxSevere, t.StarvTurnsMaxStress, t.StarvTurnsMaxNormal,
                t.TurnsMaxFamine, t.TurnsMaxSevere, t.TurnsMaxStress, t.TurnsMaxNormal,
                t.V2Violations, t.FinalPop));
            Console.WriteLine($"seed {seed} done onsets={t.Onsets} famine={t.Famine}");
        }
        sb.AppendLine($"# TOTAL onsets={totalOnsets} settlementYears={Inv(totalSy)} " +
                      $"perSettlementCentury={Inv(totalOnsets / (totalSy / 100.0))} famine={totFam} " +
                      $"severe={totSev} stress={totStr} famineReasonNone={totNone} v2Violations={totV2}");
        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine(sb.ToString());
    }
}
