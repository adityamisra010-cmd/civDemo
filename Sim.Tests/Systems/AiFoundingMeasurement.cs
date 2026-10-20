using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 S1c (ADR-036 §8) — THE AI FOUNDING MEASUREMENT. A diagnostic rig, not a gate (skipped unless SIM_MEASURE=1).
/// Per seed, the canonical founded world (1024², twelve settlements) with ONE AI Empire is stepped through the
/// production pipeline with the AI producer before every step — the `sim run --founded --ai-empires 1` loop — once
/// with the shipped content (policy ON) and once with <see cref="TestConfigs.PreAiFounding"/> (OFF), and records:
/// <list type="bullet">
/// <item>AI Found Settlement orders issued and colonies actually founded by turn 300 and by the horizon;</item>
/// <item>the conservation audit (ConservationAuditor) on EVERY turn of the ON run;</item>
/// <item>a REPLAY of the ON run's log with a fresh executor and no producer, hash for hash;</item>
/// <item>the AI's controlled settlements at 300 / horizon and the first turn a4_expansion / a6_towns hold for it, ON and
///   OFF; its Age at 300 / horizon and the turns it entered each Age, ON and OFF;</item>
/// <item>each founding's straight-line distance from its source (GeographicDistance.Km).</item>
/// </list>
/// Environment: S1C_SEEDS (comma list, default 42,1,2,3,7), S1C_TURNS (default 600), S1C_OUT (report path, default
/// the system temp directory).
/// </summary>
public class AiFoundingMeasurement
{
    private static readonly PolityId Rival = new(2);

    private static TurnExecutor Production(SimConfig cfg, OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())), orders);
    }

    private static AgeMilestone Find(AgeContent ages, string id)
    {
        foreach (AgeDefinition a in ages.Ages)
            if (a.Entry is { } e)
            {
                foreach (AgeMilestone m in e.Supporting) if (m.Id == id) return m;
                foreach (AgeMilestone m in e.Core) if (m.Id == id) return m;
            }
        throw new InvalidOperationException($"no milestone {id}");
    }

    private sealed record Arm(OrderLog Log, List<string> Hashes, long[] AgeEnteredAt, long A4ExpansionAt, long A6TownsAt,
        int Controlled300, int ControlledEnd, int Age300, int AgeEnd, int NotConservedTurns, List<(long Turn, int Source, int Cell, bool Founded, double Km)> Foundings);

    private static Arm Run(SimConfig cfg, ulong seed, int turns, bool audit)
    {
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 1 };
        AgeContent ages = cfg.Ages!;
        AgeMilestone a4 = Find(ages, "a4_expansion"), a6 = Find(ages, "a6_towns");
        var log = new OrderLog();
        TurnExecutor exec = Production(cfg, log);
        WorldState w = WorldFounding.Found(wg, cfg, seed);
        var hashes = new List<string>(turns);
        var enteredAt = new long[10];
        Array.Fill(enteredAt, -1);
        long a4At = -1, a6At = -1;
        int c300 = -1, age300 = -1, notConserved = 0;
        var foundings = new List<(long, int, int, bool, double)>();
        for (int t = 1; t <= turns; t++)
        {
            int before = log.Count;
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            WorldState prev = w;
            w = exec.Step(prev);
            hashes.Add(WorldHash.ComputeHex(w));
            for (int i = before; i < log.Count; i++)
            {
                OrderRecord o = log[i];
                if (o.Kind != OrderKind.FoundSettlement) continue;
                int cell = FoundingQuery.SiteCellOf(o);
                bool founded = false;
                for (int s = 0; s < w.Settlements.Count; s++)
                    if (w.Settlements[s].SiteCell == cell && w.Settlements[s].FoundedTurn == w.Clock.Turn) founded = true;
                int sourceCell = -1;
                for (int s = 0; s < prev.Settlements.Count; s++)
                    if (prev.Settlements[s].Id.Value == o.TargetId) sourceCell = prev.Settlements[s].SiteCell;
                foundings.Add((o.Turn, o.TargetId, cell, founded, GeographicDistance.Km(w.Terrain!, sourceCell, cell)));
            }
            if (audit && !ConservationAuditor.IsConserved(w, cfg.Goods, out _)) notConserved++;
            int age = AgeQuery.CurrentAge(w, ages, Rival);
            if (enteredAt[age] < 0) enteredAt[age] = w.Clock.Turn;
            if (a4At < 0 && AgeQuery.Milestone(w, Rival, a4).Met) a4At = w.Clock.Turn;
            if (a6At < 0 && AgeQuery.Milestone(w, Rival, a6).Met) a6At = w.Clock.Turn;
            if (t == 300) { c300 = EmpireQuery.ControlledCount(w, Rival); age300 = age; }
        }
        return new Arm(log, hashes, enteredAt, a4At, a6At, c300, EmpireQuery.ControlledCount(w, Rival),
            age300, AgeQuery.CurrentAge(w, ages, Rival), notConserved, foundings);
    }

    [MeasurementFact("S1c AI founding")]
    public void AiFoundings_AcrossSeeds_ByTurn300AndTheHorizon_ConservedAndReplayed()
    {
        string seedsVar = Environment.GetEnvironmentVariable("S1C_SEEDS") ?? "42,1,2,3,7";
        int turns = int.Parse(Environment.GetEnvironmentVariable("S1C_TURNS") ?? "600", CultureInfo.InvariantCulture);
        string outPath = Environment.GetEnvironmentVariable("S1C_OUT") ?? Path.Combine(Path.GetTempPath(), "s1c-measure.txt");
        SimConfig on = TestConfigs.Sim(), off = TestConfigs.PreAiFounding(on);
        var sb = new StringBuilder();
        foreach (string token in seedsVar.Split(','))
        {
            ulong seed = ulong.Parse(token, CultureInfo.InvariantCulture);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Arm a = Run(on, seed, turns, audit: true);
            long onMs = clock.ElapsedMilliseconds;

            // REPLAY: a fresh founding and executor, the ON log, no producer.
            WorldState r = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, on, seed);
            TurnExecutor replay = Production(on, a.Log);
            OrderValidation.ValidateAgainstWorld(a.Log, r);
            int firstReplayDiff = -1;
            for (int t = 0; t < a.Hashes.Count; t++)
            {
                r = replay.Step(r);
                if (firstReplayDiff < 0 && WorldHash.ComputeHex(r) != a.Hashes[t]) firstReplayDiff = t + 1;
            }

            clock.Restart();
            Arm b = Run(off, seed, turns, audit: false);
            long offMs = clock.ElapsedMilliseconds;

            int issued300 = a.Foundings.Count(f => f.Turn < 300), founded300 = a.Foundings.Count(f => f.Turn < 300 && f.Founded);
            int issued = a.Foundings.Count, founded = a.Foundings.Count(f => f.Founded);
            string km = a.Foundings.Count == 0 ? "-" : string.Join(",", a.Foundings.Select(f => f.Km.ToString("F0", CultureInfo.InvariantCulture)));
            string stamps = string.Join(",", a.Foundings.Select(f => f.Turn.ToString(CultureInfo.InvariantCulture) + (f.Founded ? "" : "x")));
            string ages(Arm x) => string.Join(" ", Enumerable.Range(2, 8).Where(k => x.AgeEnteredAt[k] >= 0).Select(k => $"A{k}@{x.AgeEnteredAt[k]}"));
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"seed {seed}: ON issued/founded by300 {issued300}/{founded300}, by{turns} {issued}/{founded}; stamps [{stamps}]; km [{km}]; " +
                $"notConservedTurns {a.NotConservedTurns}; replay {(firstReplayDiff < 0 ? "IDENTICAL" : "DIVERGES at " + firstReplayDiff)}; " +
                $"AI controls 300/{turns}: ON {a.Controlled300}/{a.ControlledEnd} OFF {b.Controlled300}/{b.ControlledEnd}; " +
                $"a4_expansion ON {a.A4ExpansionAt} OFF {b.A4ExpansionAt}; a6_towns ON {a.A6TownsAt} OFF {b.A6TownsAt}; " +
                $"Age 300/{turns} ON {a.Age300}/{a.AgeEnd} OFF {b.Age300}/{b.AgeEnd}; entered ON [{ages(a)}] OFF [{ages(b)}]; " +
                $"final ON {a.Hashes[^1]} OFF {b.Hashes[^1]}; ms ON {onMs} OFF {offMs}"));
            File.WriteAllText(outPath, sb.ToString());
        }
    }
}
