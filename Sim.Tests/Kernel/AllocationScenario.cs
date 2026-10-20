using System.Security.Cryptography;
using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — THE SCENARIO S42-ALLOC-H of the ci.yml step "Allocation split, binding,
/// cross-process (D-055)", as data: the player's orders `scripts/gen-sample-orders.py --allocation` writes (this table and
/// the script's literals are pinned against each other by <see cref="AllocationCiLegTests"/>, and the sha256 of the logs
/// serialized from this table are the ones the ci.yml step checks the generated files against), and the scenario's
/// MEASURED turns. Seed 42, founded, no AI Empire (worldgen.json aiEmpires 0: the player, polity 1, rules all twelve
/// settlements, so its Empire purse is the whole world's).
/// <list type="bullet">
/// <item>WINDOW 1 — research: the player researches the built road (track_road AND stone_dry: road class 3 costs stone);
///   stone_dry completes in step 115 → 116.</item>
/// <item>WINDOW 2 — the competing-heads binding window (117–125): b = 30 stamped <see cref="P"/>, DevelopRoads every turn
///   from 116 (100 % to 125), three granaries each at settlements 0 and 2 stamped 117. The split first binds at
///   <see cref="X"/>; building work's unused share goes to road works at <see cref="Y"/>.</item>
/// <item>WINDOW 3 — the hold window, in the 1 % road phase: settlement 4 (<see cref="Third"/>) gets few builders (kind 3,
///   construction weight 0.01, stamped 125), a granary stamped 126 that its builders pace (part-built to a progress that
///   is not a round number), and NO builders (weight 0, stamped 127): an OLDER part-built head that sets aside what it
///   still needs. The granaries stamped 128 at settlements 0 and 2 wait for it — HeldForEarlier, from <see cref="W"/> —
///   while the set-aside sits inside the 30 % share. Road works' unused share goes to building work at <see cref="Z"/>. At
///   <see cref="V"/> the part-built head is cancelled (kind 13, D-056) and the same project re-enqueued (kind 4) in one batch;
///   settlement 4's builders return at 134; b = 70 is stamped <see cref="Seventy"/> in the batch that queues granaries at
///   settlements 0, 2 and 4 (T3 round 2, 2026-10-17): the new share binds in its own delivery step, where its twin without
///   the order draws the old share.</item>
/// </list>
/// <see cref="K"/> is the save point inside the hold window and <see cref="H"/> the horizon.
/// </summary>
internal static class AllocationScenario
{
    internal const ulong Seed = 42;

    /// <summary>The pre-split world (A0 saves it; A, A' and the control continue from it). b = 30 is stamped at it.</summary>
    internal const long P = 116;

    /// <summary>The first binding step (the step from turn X): stone build + holds = G_b &lt; D_b.</summary>
    internal const long X = 117;

    /// <summary>Building work's unused share used by road works, with heads in the queue (D_b &lt; E_b, road &gt; E_r). D-058
    /// (2026-10-19): 123 — was 120, where settlement 2's head now claims more than the grant (the Director's "build one at a
    /// time" ruling; 120 is reproduced on the PreContinueWithinTurn twin, AllocationScenarioD058AttributionTests).</summary>
    internal const long Y = 123;

    /// <summary>The first head HELD FOR AN EARLIER HEAD (the step from turn W).</summary>
    internal const long W = 128;

    /// <summary>The save point (world K): inside the hold window.</summary>
    internal const long K = 130;

    /// <summary>Road works' unused share used by building work (D_b &gt; E_b, D_r &lt; E_r, build + holds &gt; E_b).</summary>
    internal const long Z = 131;

    /// <summary>The cancel (kind 13, D-056) of the part-built head and its re-enqueue (kind 4), one batch.</summary>
    internal const long V = 132;

    /// <summary>The kind-12 order that changes b to 70 — with three granaries in the same batch, so building work draws what
    /// the new share grants in the order's own delivery step (T3 round 2).</summary>
    internal const long Seventy = 136;

    /// <summary>The horizon: the leg runs worlds 1..H.</summary>
    internal const long H = 140;

    /// <summary>The third settlement, whose older head loses its builders.</summary>
    internal const int Third = 4;

    internal const int Player = 1;
    internal const int Granary = 1;
    internal const int Stone = 5;
    internal const int Timber = 4;

    /// <summary>Kind 3's packed target for settlement 4's construction sector: (4 &lt;&lt; 3) | 4.</summary>
    internal const int ThirdConstruction = (Third << 3) | Sectors.Construction;

    /// <summary>
    /// The records, in the generator's order: the fixed orders as the script lists them, then DevelopRoads for every turn
    /// 116 ≤ t &lt; <paramref name="maxTurn"/>, then a STABLE sort by turn (same-turn rows keep their order) — so the log
    /// this serializes to is byte for byte the script's. <paramref name="split"/> false drops every kind 12 (`--no-split`,
    /// the control); <paramref name="drop"/> drops that turn's orders of that kind (`--drop TURN:KIND`, the twins).
    /// </summary>
    internal static OrderRecord[] Orders(bool split = true, (long Turn, OrderKind Kind)? drop = null, long maxTurn = H)
    {
        var r = new List<OrderRecord>
        {
            R(0, OrderKind.SetResearchTarget, 12, 0.0), R(32, OrderKind.SetResearchTarget, 17, 0.0),
            R(39, OrderKind.SetResearchTarget, 2, 0.0), R(53, OrderKind.SetResearchTarget, 8, 0.0),
            R(66, OrderKind.SetResearchTarget, 52, 0.0), R(86, OrderKind.SetResearchTarget, 74, 0.0),
            R(99, OrderKind.SetResearchTarget, 53, 0.0), R(116, OrderKind.SetResourceAllocation, Player, 30.0),
        };
        for (int i = 0; i < 3; i++) r.Add(R(117, OrderKind.EnqueueConstruction, 0, Granary));
        for (int i = 0; i < 3; i++) r.Add(R(117, OrderKind.EnqueueConstruction, 2, Granary));
        r.Add(R(125, OrderKind.SectorAllocation, ThirdConstruction, 0.01));
        r.Add(R(126, OrderKind.EnqueueConstruction, Third, Granary));
        r.Add(R(127, OrderKind.SectorAllocation, ThirdConstruction, 0.0));
        for (int i = 0; i < 2; i++) r.Add(R(128, OrderKind.EnqueueConstruction, 0, Granary));
        for (int i = 0; i < 2; i++) r.Add(R(128, OrderKind.EnqueueConstruction, 2, Granary));
        r.Add(R(V, OrderKind.CancelConstructionEntry, Third, OrderLog.PackCancelTarget(new CancelTarget(0, Granary, 126))));
        r.Add(R(V, OrderKind.EnqueueConstruction, Third, Granary));
        r.Add(R(134, OrderKind.SectorAllocation, ThirdConstruction, 10.0));
        r.Add(R(Seventy, OrderKind.SetResourceAllocation, Player, 70.0));
        foreach (int s in new[] { 0, 2, Third }) r.Add(R(Seventy, OrderKind.EnqueueConstruction, s, Granary));   // T3 round 2
        for (long t = 116; t < maxTurn; t++) r.Add(R(t, OrderKind.DevelopRoads, 0, t < 126 ? 100.0 : 1.0));

        var kept = new List<OrderRecord>();
        foreach (OrderRecord o in r.Where(o => o.Turn < maxTurn).OrderBy(o => o.Turn))   // OrderBy is stable
        {
            if (!split && o.Kind == OrderKind.SetResourceAllocation) continue;
            if (drop is { } d && o.Turn == d.Turn && o.Kind == d.Kind) continue;
            kept.Add(o);
        }
        return [.. kept];
    }

    private static OrderRecord R(long turn, OrderKind kind, int target, double amount) => new(turn, Player, kind, target, amount);

    internal static OrderLog Log(OrderRecord[] records)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in records) log.Append(o);
        return log;
    }

    /// <summary>The sha256 (lower-case hex) of <paramref name="records"/> serialized as an order log (OrderLog.Save: the
    /// format the generator writes).</summary>
    internal static string Sha256(OrderRecord[] records)
    {
        using var ms = new MemoryStream();
        Log(records).Save(ms);
        return Convert.ToHexStringLower(SHA256.HashData(ms.ToArray()));
    }

    /// <summary>The simulation configuration the CLI runs (Sim.Cli's: sim.json with research, Ages and unit families).</summary>
    internal static SimConfig CliConfig()
    {
        using var sim = global::Sim.Data.DataFiles.OpenSim();
        using var needs = global::Sim.Data.DataFiles.OpenNeeds();
        using var goods = global::Sim.Data.DataFiles.OpenGoods();
        using var research = global::Sim.Data.DataFiles.OpenResearch();
        using var ages = global::Sim.Data.DataFiles.OpenAges();
        using var families = global::Sim.Data.DataFiles.OpenUnitFamilies();
        return SimConfigLoader.Load(sim, needs, goods, research, ages, families);
    }

    /// <summary>The founded production executor exactly as `sim run --founded` builds it (era table, pipeline.json, the
    /// catalogue with the worldgen config attached).</summary>
    internal static TurnExecutor Executor(SimConfig cfg, OrderLog log)
    {
        using var era = global::Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = global::Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, CliRecipes.Worldgen())), log);
    }

    /// <summary>A table's rows, in index order.</summary>
    internal static T[] All<T>(IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }

    /// <summary>A batch's orders, in delivery order.</summary>
    internal static OrderRecord[] All(OrderBatch batch)
    {
        var orders = new OrderRecord[batch.Count];
        for (int i = 0; i < orders.Length; i++) orders[i] = batch[i];
        return orders;
    }

    /// <summary>The repository root (the directory holding .github).</summary>
    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}

/// <summary>
/// The scenario RUN, made once for both test classes (the slow part: 140 founded steps and a 24-step control), exactly as
/// the ci.yml step runs it but in process: the world `sim run --founded --seed 42` founds (<see cref="HeadlessFounding"/>),
/// the CLI's executor and configuration, the generated log validated against turn zero and at every delivery (ADR-034, as
/// `sim run` does), stepped unobserved to <see cref="AllocationScenario.P"/> and then through the SAME
/// <see cref="SplitProbe"/> `--split-report` uses — so the rows asserted here are the rows the CLI writes. The control (no
/// kind 12) continues from world P, as the ci.yml control continues from the pre-split save.
/// </summary>
public sealed class AllocationScenarioRun
{
    internal SimConfig Cfg { get; }
    internal OrderLog Log { get; } = AllocationScenario.Log(AllocationScenario.Orders());
    internal OrderLog ControlLog { get; } = AllocationScenario.Log(AllocationScenario.Orders(split: false));

    /// <summary>The founding world (turn zero, ADR-034's validation world).</summary>
    internal WorldState TurnZero { get; }

    /// <summary>Worlds P..H of the run (index = turn − P).</summary>
    internal WorldState[] Worlds { get; }

    /// <summary>The world hash of every world 0..H (index = turn).</summary>
    internal string[] Hashes { get; }

    /// <summary>The first world (turn) whose player knows each of <see cref="Watched"/> (−1: none by H).</summary>
    private readonly long[] _firstKnowing = [-1, -1];
    private static readonly string[] Watched = ["track_road", "stone_dry"];

    /// <summary>The first world whose player knows research node <paramref name="id"/> (track_road or stone_dry).</summary>
    internal long FirstWorldKnowing(string id) => _firstKnowing[Array.IndexOf(Watched, id)];

    /// <summary>Polity and settlement counts of every world 0..H (index = turn).</summary>
    internal int[] PolityCounts { get; }
    internal int[] SettlementCounts { get; }

    /// <summary>The split report of steps P..H−1, as the probe wrote it (bytes) and parsed.</summary>
    internal byte[] Report { get; }
    internal SplitReportRow[] Rows { get; }

    /// <summary>The control's report rows and world hashes P+1..H (index = turn − P; index 0 unused).</summary>
    internal SplitReportRow[] ControlRows { get; }
    internal string[] ControlHashes { get; }
    internal int[] ControlPolityCounts { get; }
    internal int[] ControlSettlementCounts { get; }

    public AllocationScenarioRun() : this(AllocationScenario.CliConfig()) { }

    /// <summary>D-058 (2026-10-19): the same run under another configuration — the attribution twin
    /// (TestConfigs.PreContinueWithinTurn of the CLI's configuration, <see cref="AllocationScenarioPreD058Run"/>).</summary>
    internal AllocationScenarioRun(SimConfig cfg)
    {
        Cfg = cfg;
        const long p = AllocationScenario.P, h = AllocationScenario.H;
        TurnZero = HeadlessFounding.Found(AllocationScenario.Seed);
        OrderValidation.ValidateAgainstWorld(Log, TurnZero);
        OrderValidation.ValidateAgainstWorld(ControlLog, TurnZero);
        Hashes = new string[h + 1];
        PolityCounts = new int[h + 1];
        SettlementCounts = new int[h + 1];
        Worlds = new WorldState[h - p + 1];
        Record(TurnZero.Clone());

        TurnExecutor exec = AllocationScenario.Executor(Cfg, Log);
        WorldState w = TurnZero.Clone();
        while (w.Clock.Turn < p)
        {
            OrderValidation.ValidateAtDelivery(Log.BatchFor(w.Clock.Turn), TurnZero, w);
            w = exec.Step(w);
            Record(w);
        }
        Worlds[0] = w;
        using (var report = new MemoryStream())
        {
            var probe = new SplitProbe(report, Cfg);
            while (w.Clock.Turn < h)
            {
                OrderBatch batch = Log.BatchFor(w.Clock.Turn);
                OrderValidation.ValidateAtDelivery(batch, TurnZero, w);
                w = probe.Step(exec, w, batch);
                Record(w);
                Worlds[w.Clock.Turn - p] = w;
            }
            Report = report.ToArray();
        }
        Rows = SplitReport.Parse(System.Text.Encoding.UTF8.GetString(Report));

        TurnExecutor control = AllocationScenario.Executor(Cfg, ControlLog);
        ControlHashes = new string[h - p + 1];
        ControlPolityCounts = new int[h - p + 1];
        ControlSettlementCounts = new int[h - p + 1];
        using (var report = new MemoryStream())
        {
            var probe = new SplitProbe(report, Cfg);
            WorldState c = Worlds[0].Clone();
            while (c.Clock.Turn < h)
            {
                OrderBatch batch = ControlLog.BatchFor(c.Clock.Turn);
                OrderValidation.ValidateAtDelivery(batch, TurnZero, c);
                c = probe.Step(control, c, batch);
                ControlHashes[c.Clock.Turn - p] = WorldHash.ComputeHex(c);
                ControlPolityCounts[c.Clock.Turn - p] = c.Polities.Count;
                ControlSettlementCounts[c.Clock.Turn - p] = c.Settlements.Count;
            }
            ControlRows = SplitReport.Parse(System.Text.Encoding.UTF8.GetString(report.ToArray()));
        }
    }

    private void Record(WorldState w)
    {
        long t = w.Clock.Turn;
        Hashes[t] = WorldHash.ComputeHex(w);
        PolityCounts[t] = w.Polities.Count;
        SettlementCounts[t] = w.Settlements.Count;
        Sim.Core.Systems.Research.ResearchContent research = Cfg.Research!;
        for (int i = 0; i < Watched.Length; i++)
            if (_firstKnowing[i] < 0
                && ResearchQuery.IsCompleted(w, new PolityId(AllocationScenario.Player), research.Nodes[research.IndexOfId(Watched[i])].Key))
                _firstKnowing[i] = t;
    }

    /// <summary>World <paramref name="turn"/> (P ≤ turn ≤ H).</summary>
    internal WorldState World(long turn) => Worlds[turn - AllocationScenario.P];

    /// <summary>The report row of the step from <paramref name="turn"/> (the player's; the only Empire).</summary>
    internal SplitReportRow Row(long turn) => Assert.Single(Rows, r => r.Turn == turn);

    internal SplitReportRow ControlRow(long turn) => Assert.Single(ControlRows, r => r.Turn == turn);

    /// <summary>The report's lines (each with its newline) for the steps from <paramref name="fromTurn"/> on.</summary>
    internal string ReportFrom(long fromTurn)
    {
        string[] lines = System.Text.Encoding.UTF8.GetString(Report).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
            if (Rows[i].Turn >= fromTurn) text.Append(lines[i]).Append('\n');
        return text.ToString();
    }
}

/// <summary>The scenario run shared by <see cref="AllocationCiLegTests"/> and <see cref="AllocationScenarioTests"/>.</summary>
[CollectionDefinition(Name)]
public sealed class AllocationScenarioCollection : ICollectionFixture<AllocationScenarioRun>
{
    public const string Name = "S42-ALLOC-H";
}

/// <summary>
/// D-058 (2026-10-19) — THE SCENARIO UNDER THE BATCH-4 CONSTRUCTION RULE: the same founding, orders and executor with
/// sim.json construction.continueWithinTurn switched off (TestConfigs.PreContinueWithinTurn, a single-layer twin), so a
/// completing head's leftover capacity never reaches its queue's next entry in the same step. Every scenario literal the
/// Director's "build one at a time" ruling moved is reproduced on it (AllocationScenarioD058AttributionTests).
/// </summary>
public sealed class AllocationScenarioPreD058Run() : AllocationScenarioRunHolder(new AllocationScenarioRun(
    Sim.Tests.TestUtil.TestConfigs.PreContinueWithinTurn(AllocationScenario.CliConfig())));

/// <summary>Holds one <see cref="AllocationScenarioRun"/> built with a non-default configuration (an xUnit fixture needs a
/// public parameterless constructor).</summary>
public abstract class AllocationScenarioRunHolder(AllocationScenarioRun run)
{
    internal AllocationScenarioRun Run { get; } = run;
}
