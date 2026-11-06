using System.Diagnostics;
using Proto.M7.Demo;
using Proto.M7.Events;
using Proto.M7.Time;

// Proto.M7.Bench — scheduler throughput (spec §8). Stopwatch/double are used for REPORTING ONLY.
// Usage: dotnet Proto.M7.Bench.dll [events=2000000] [reps=5]
// Slice C: `spatial` mode (A* 250x250, LOS, separation) — see SpatialBench.cs.
if (args.Length > 0 && args[0] == "spatial") { SpatialBench.Run(args); return; }
if (args.Length > 0 && args[0] == "tactical") { TacticalBench.Run(args); return; }   // Slice D
if (args.Length > 0 && args[0] == "parity") { ParityBench.Run(args); return; }       // Slice E
if (args.Length > 0 && args[0] == "terrain") { TerrainBench.Run(args); return; }     // [TR] lane TR
if (args.Length > 0 && args[0] == "domain") { DomainBench.Run(args); return; }       // lane N (naval / air / combined arms)
if (args.Length > 0 && args[0] == "parityp") { ParityPBench.Run(args); return; }     // [P] lane P parity battery
if (args.Length > 0 && args[0] == "wareconE") { WarEconBench.Run(args); return; }   // lane E per-pulse war economy
if (args.Length > 0 && args[0] == "siege") { SiegeBench.Run(args); return; }         // Phase C lane S
if (args.Length > 0 && args[0] == "siegetrace") { SiegeBench.Trace(args); return; } // Phase C lane S
if (args.Length > 0 && args[0] == "siegeseeds") { SiegeBench.Seeds(args); return; } // Phase C lane S
long n = args.Length > 0 ? long.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 2_000_000;
int reps = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 5;
Console.WriteLine($"Proto.M7.Bench scheduler: events={n} reps={reps} cpu={Environment.ProcessorCount} os={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

static double Median(List<double> xs) { xs.Sort(); return xs[xs.Count / 2]; }

// 1. Raw heap: push n tie-dense keys then pop all.
var heapMs = new List<double>();
for (int r = 0; r < reps; r++)
{
    var h = new EventHeap(1024);
    var sw = Stopwatch.StartNew();
    for (long i = 0; i < n; i++)
    {
        ulong d = Proto.M7.Rng.CounterRng.Draw(1, 1, i, 0, 0, 0);
        h.Push(new ScheduledEvent(new EventKey((long)(d % 100_000), (byte)(d >> 60), 0, (long)((d >> 20) % 64), i), 0, EntityRef.None, EntityRef.None, 0, 0, 0));
    }
    while (h.Count > 0) h.Pop();
    sw.Stop();
    heapMs.Add(sw.Elapsed.TotalMilliseconds);
}
double hm = Median(heapMs);
Console.WriteLine($"heap push+pop        : median {hm:F1} ms  => {2 * n / (hm / 1000.0) / 1e6:F2} M heap-ops/s ({n / (hm / 1000.0) / 1e6:F2} M events/s)");

// 2. Steady-state scheduler: a self-rescheduling population of K "ticking" sources, trace on and off.
foreach (bool trace in new[] { false, true })
{
    var ms = new List<double>();
    long executed = 0;
    for (int r = 0; r < reps; r++)
    {
        var s = new EventScheduler();
        s.Trace.Enabled = trace;
        var w = new TickWorld();
        const int K = 4096;
        for (int k = 0; k < K; k++) s.Schedule(k % 97, EventClass.Arrival, 0, k, 0, a: 1 + k % 13);
        var runner = new Runner(s, w, SignificantKinds.None);
        var sw = Stopwatch.StartNew();
        while (s.ExecutedCount < n && s.PendingCount > 0) s.ExecuteNext(w, out _);
        sw.Stop();
        executed = s.ExecutedCount;
        ms.Add(sw.Elapsed.TotalMilliseconds);
        GC.KeepAlive(runner);
    }
    double m = Median(ms);
    Console.WriteLine($"scheduler steady (trace={(trace ? "on " : "off")}): median {m:F1} ms for {executed} events => {executed / (m / 1000.0) / 1e6:F2} M events/s");
}

// 3. Campaign demo: 1,000 construction processes over all 1,630 turns (interrupts on; no orders).
{
    var cal = CampaignCalendar.FromJsonFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "era-pacing.json"));
    var s = new EventScheduler();
    var w = new CampaignDemoWorld(cal);
    for (int i = 0; i < 1000; i++) w.AddProcess(s, 1000 + i * 37, 1 + (i % 50), 0);
    var runner = new Runner(s, w, new SignificantKinds(DemoKinds.ConstructionComplete));
    var sw = Stopwatch.StartNew();
    long interrupts = 0;
    for (long t = 0; t < cal.TotalTurns; t++)
        while (runner.RunUntil(cal.TurnEnd(t).Ms - 1).Status == RunStatus.Interrupted) interrupts++;
    sw.Stop();
    Console.WriteLine($"campaign demo 1630 turns x 1000 processes: {sw.Elapsed.TotalMilliseconds:F1} ms, events={s.ExecutedCount}, interrupts={interrupts}, trace={s.Trace.HashHex()[..16]}");
}

sealed class TickWorld : IEventWorld
{
    public bool IsLive(EntityRef e) => true;
    public void Execute(in ScheduledEvent ev, EventScheduler s)
        => s.Schedule(ev.Key.TimeMs + ev.A, EventClass.Arrival, 0, ev.Key.SourceId, 0, a: ev.A);
}
