using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 §8 — THE FOUR-SEED MEASUREMENT of the AI's recruitment policy (military.aiRecruitment), the ADR-052 / ADR-053
/// precedent: the canonical world (shipped worldgen, one AI Empire = polity 2, the PacingReferenceMeasurement setup), the
/// shipped content, seeds 1, 2, 42 and 7, the AI's Age-entry turns with the policy ON (shipped) and OFF
/// (TestConfigs.PreAiRecruitment — the attribution control), the AI's recruitment orders and the first turn the two worlds
/// differ. A MEASUREMENT, opt-in: it runs only with the environment variable SIM_MEASURE_AI_RECRUITMENT set (to the turn cap,
/// e.g. 1500), and writes its table to the test output and to SIM_MEASURE_OUT when set. Without the variable it does nothing.
/// </summary>
public class AiRecruitmentPacingMeasurement(ITestOutputHelper output)
{
    private static readonly PolityId Ai = new(2);

    [Fact]
    public void FourSeeds_AgeEntryTurns_PolicyOnVsOff()
    {
        string? cap = Environment.GetEnvironmentVariable("SIM_MEASURE_AI_RECRUITMENT");
        if (string.IsNullOrEmpty(cap)) return;
        int turns = int.Parse(cap, CultureInfo.InvariantCulture);
        string? seedList = Environment.GetEnvironmentVariable("SIM_MEASURE_SEEDS");
        ulong[] seeds = seedList is { Length: > 0 }
            ? Array.ConvertAll(seedList.Split(','), s => ulong.Parse(s, CultureInfo.InvariantCulture))
            : [1UL, 2UL, 42UL, 7UL];
        var sb = new StringBuilder();
        sb.AppendLine("seed | policy | A2 | A3 | A4 | A5 | A6 | A7 | A8 | A9 | recruit orders (turn:identity) | formations at end | last turn | first differing turn");
        foreach (ulong seed in seeds)
        {
            (int[] onAges, string onRecruits, int onUnits, long onLast, string[] onHashes) = Run(seed, TestConfigs.Sim(), turns);
            (int[] offAges, string offRecruits, int offUnits, long offLast, string[] offHashes) = Run(seed, TestConfigs.PreAiRecruitment(TestConfigs.Sim()), turns);
            long diverge = -1;
            for (int t = 0; t < Math.Min(onHashes.Length, offHashes.Length); t++)
                if (onHashes[t] != offHashes[t]) { diverge = t + 1; break; }
            sb.AppendLine(Row(seed, "on", onAges, onRecruits, onUnits, onLast, diverge));
            sb.AppendLine(Row(seed, "off", offAges, offRecruits, offUnits, offLast, diverge));
            output.WriteLine(sb.ToString());
            if (Environment.GetEnvironmentVariable("SIM_MEASURE_OUT") is { Length: > 0 } path) File.WriteAllText(path, sb.ToString());
        }
    }

    private static string Row(ulong seed, string policy, int[] ages, string recruits, int units, long last, long diverge) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{seed} | {policy} | {string.Join(" | ", Array.ConvertAll(ages, a => a < 0 ? "-" : a.ToString(CultureInfo.InvariantCulture)))} | {recruits} | {units} | {last} | {diverge}");

    private static (int[] Ages, string Recruits, int Units, long Last, string[] Hashes) Run(ulong seed, SimConfig cfg, int turns)
    {
        WorldgenConfig shipped = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, shipped)), log);
        WorldState w = WorldFounding.Found(shipped with { AiEmpires = 1 }, cfg, seed);
        var ages = new int[8];
        Array.Fill(ages, -1);
        var recruits = new List<string>();
        var hashes = new List<string>();
        for (int t = 0; t < turns; t++)
        {
            foreach (OrderRecord o in AiOrders.For(w, cfg, shipped))
            {
                log.Append(o);
                if (o.Kind == OrderKind.RecruitFormation && o.ActorId == Ai.Value)
                    recruits.Add(string.Create(CultureInfo.InvariantCulture, $"{o.Turn}:{(int)o.Amount}"));
            }
            w = exec.Step(w);
            hashes.Add(t % 10 == 9 ? WorldHash.ComputeHex(w) : w.Clock.Turn.ToString(CultureInfo.InvariantCulture) + ":" + Digest(w));
            for (int i = 0; i < w.AgeTransitions.Count; i++)
            {
                AgeTransitionRow r = w.AgeTransitions[i];
                if (r.Polity == Ai && r.ToAge >= 2 && ages[r.ToAge - 2] < 0) ages[r.ToAge - 2] = (int)r.EffectiveTurn;
            }
            if (ages[7] >= 0) break;
        }
        int units = 0;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Owner == Ai) units++;
        return (ages, string.Join(" ", recruits), units, w.Clock.Turn, [.. hashes]);
    }

    /// <summary>A cheap per-turn fingerprint (the full hash every tenth turn): the AI's formations, its completed research and
    /// its total population — enough to find the first turn the worlds differ.</summary>
    private static string Digest(IReadOnlyWorldState w)
    {
        long pop = 0;
        for (int i = 0; i < w.Buckets.Count; i++) pop += w.Buckets[i].Count.Value;
        return string.Create(CultureInfo.InvariantCulture, $"{w.MilitaryUnits.Count}/{w.ResearchCompleted.Count}/{pop}/{w.FormationPersonnel.Count}");
    }
}
