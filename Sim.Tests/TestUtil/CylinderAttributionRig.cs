using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>
/// W1 F7 (batch 4; D-055, 2026-10-16; design §2) — THE CYLINDER FEATURE-ATTRIBUTION HARNESS shared by the
/// CylinderAttribution*Tests rigs. One rig proves that ONE feature, and nothing else, moves the SHIPPED cylindrical world,
/// turn-exactly:
/// <list type="number">
/// <item><b>The world.</b> Both arms found and step the SAME shipped worldgen (worldgen.json as shipped, topology.wrapX on)
///   with the same seed, pipeline and order source; <see cref="Run"/> asserts <c>Terrain.WrapX</c> on every arm, so no arm
///   can silently be flat. The terrain is folded into every world hash, and it is held constant across the arms.</item>
/// <item><b>The arms.</b> ON = the shipped config (<see cref="TestConfigs.Sim"/>); OFF = a NON-chained single-switch twin
///   (<see cref="SingleSwitchTwins"/>, or a single-layer helper of <see cref="TestConfigs"/>). The arms differ by one
///   switch.</item>
/// <item><b>World 0 identical</b> (<see cref="AssertWorld0Identical"/>).</item>
/// <item><b>First action.</b> The first differing turn (<see cref="FirstDifferent"/>, from the hash series) equals the
///   feature's first-action turn read INDEPENDENTLY of the hash — from the books, a state predicate (a <see cref="Probe"/>
///   on the run) or the order log — and both are pinned as MEASURED constants, so a feature that acts one turn late (or
///   early) fails.</item>
/// <item><b>The write set</b> (<see cref="WriteSet"/>): the tables that differ at the divergence world, MEASURED and
///   pinned per rig. <see cref="AssertNoTopologyTable"/> refuses a topology table (catchments, the network, path progress)
///   unless the rig names it as the feature's own write. Terrain held constant plus the pinned write set is what separates
///   the feature's effect from topology's.</item>
/// <item><b>AI rigs:</b> the order logs are equal before the action turn (<see cref="AssertLogsEqualBefore"/>).</item>
/// <item><b>Vacuity:</b> the feature acts within the horizon (each rig asserts its predicate fired).</item>
/// <item><b>Determinism:</b> the ON arm's hash series equals a second ON run, or an existing pinned run of the same world.</item>
/// <item><b>Seam exposure</b> (<see cref="SeamCrossingPairs"/>, <see cref="WrappedDonorOrders"/>): how much of the feature's
///   distance reading the seam can reach on the rig's world — recorded (pinned, MEASURED) per rig. These are STATIC counts
///   over the settlement sites (seam-crossing settlement pairs, wrapped donor orders) at the worlds a rig names, not a
///   count of the wrapped-dx answers a run computes: the shared pathfinding heuristic and the colonization water search
///   cross the seam on every cylinder world, a 0/0 rig included (T2 fix round, nit N1; recorded in D-055).</item>
/// </list>
/// Pure test code: no RNG, no floats, table-order scans; every run goes through the production pipeline
/// (pipeline.json, era-pacing.json), exactly as `sim run` does.
/// </summary>
public static class CylinderAttributionRig
{
    /// <summary>The topology tables (design §2 / review L3-17: Catchment*, Network*, PathProgress). A rig's write set may
    /// contain one only when the rig names it as the feature's own write.</summary>
    public static readonly string[] TopologyTables =
        ["CatchmentNodes", "CatchmentSummaries", "NetworkEdges", "NetworkMeta", "NetworkNodes", "PathProgress"];

    /// <summary>A rig world: how both arms found it (<paramref name="Founding"/>, with aiEmpires where the rig has an AI
    /// Empire), the worldgen the pipeline and the AI producer are built with (<paramref name="Pipeline"/>, exactly as the
    /// existing AI harnesses and `sim run` do), the seed, whether <see cref="AiOrders"/> appends before every step, and the
    /// scripted player orders (made from the founded world; null = none).</summary>
    public sealed record RigWorld(
        string Name, WorldgenConfig Founding, WorldgenConfig Pipeline, ulong Seed, bool Ai, Func<WorldState, OrderLog>? Script);

    /// <summary>The canonical 1024-px founded world (seed 42, twelve settlements, no orders): `sim run --founded --seed 42`.</summary>
    public static RigWorld Founded1024() => new("founded-1024", TestConfigs.Worldgen(), TestConfigs.Worldgen(), 42, Ai: false, Script: null);

    /// <summary>The canonical driven world (seed 42, 1024 px, the driven golden's turn-2 SectorAllocation batch).</summary>
    public static RigWorld Driven1024() => new("driven-1024", TestConfigs.Worldgen(), TestConfigs.Worldgen(), 42, Ai: false,
        Script: w => Sim.Tests.Kernel.DrivenGoldenTests.DrivingOrders(w.Settlements.Count));

    /// <summary>The 1024-px AI world: `sim run --founded --seed 42 --ai-empires 1` (the AiFoundingAttributionTests live arm).</summary>
    public static RigWorld Ai1024() => new("ai-1024", TestConfigs.Worldgen() with { AiEmpires = 1 }, TestConfigs.Worldgen(), 42, Ai: true, Script: null);

    /// <summary>The 256-px dev AI world (the D-025 dev preset, four settlements, one AI Empire).</summary>
    public static RigWorld DevAi() => new("dev-ai", TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.DevWorldgen(), 42, Ai: true, Script: null);

    /// <summary>The dev starved rig (ExplainRigs' STARVED orders: settlement 0 ordered to zero farming and herding at turn 1)
    /// on the dev CYLINDER (the explain rig itself is kept flat; this is its cylinder twin for relief).</summary>
    public static RigWorld DevStarved() => new("dev-starved", TestConfigs.DevWorldgen(), TestConfigs.DevWorldgen(),
        Sim.Tests.Observability.ExplainRigs.Seed, Ai: false,
        Script: _ => Sim.Tests.Observability.ExplainRigs.ZeroFoodOrders(Sim.Tests.Observability.ExplainRigs.Target));

    /// <summary>The revolt rig (design §2 rig #13; T2 step 2): the canonical founded world (seed 42, 1024 px) driven exactly as
    /// `sim run --founded --seed 42 --orders <player log>` drives it — the CLI player log of CliRevoltPolityProducerTests
    /// (FoundedHarness's labour swings on settlement 0, every sibling's food labour abandoned with its 0 % swing, so the
    /// polity has no spare grain to relieve it with) and the AI producer appended after the player's batch every turn, so the
    /// polity the revolt founds is driven as the CLI and the UI drive it.</summary>
    public static RigWorld Revolt1024() => new("revolt-1024", TestConfigs.Worldgen(), TestConfigs.Worldgen(), 42, Ai: true,
        Script: Sim.Tests.Kernel.CliRevoltPolityProducerTests.PlayerLog);

    /// <summary>The dev Q17 rig (design §2 rig #16; X1's v33 fixture recipe, T2 step 2): `sim run --founded --seed 42 --size 256
    /// --settlements 4 --ai-empires 1 --orders q.bin` — the player sets a 30/70 building/road split and orders granaries at
    /// settlements 0 and 2 (turn 0), then takes settlement 0's builders away (turn 1), so s0's granary waits part-built while
    /// s2's, younger in the building order, progresses. Founded on the dev preset with one AI Empire (the same world the
    /// fixture's `--size 256 --settlements 4` founds — asserted by the rig), stepped by the CLI's pipeline (the shipped
    /// worldgen, as `sim run --founded` builds it), the AI producer appended after the player's batch every turn.</summary>
    public static RigWorld DevQ17() => new("dev-q17", TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.Worldgen(), 42,
        Ai: true, Script: _ => Q17Orders());

    /// <summary>The four player records of the Q17 rig (Fixtures/README-v33-fixture.md: `v33-fixture-orders.bin`).</summary>
    public static OrderLog Q17Orders()
    {
        var q = new OrderLog();
        q.Append(new OrderRecord(0, 1, OrderKind.SetResourceAllocation, 1, 30.0));
        q.Append(new OrderRecord(0, 1, OrderKind.EnqueueConstruction, 0, 1.0));
        q.Append(new OrderRecord(0, 1, OrderKind.EnqueueConstruction, 2, 1.0));
        q.Append(new OrderRecord(1, 1, OrderKind.SectorAllocation, (0 << 3) | 4, 0.0));
        return q;
    }

    /// <summary>A state predicate evaluated on every step of a run (prev, next, the arm's config); the run records the first
    /// turn (next's) it holds and how many steps it holds on.</summary>
    public sealed record Probe(string Name, Func<IReadOnlyWorldState, IReadOnlyWorldState, SimConfig, bool> Holds);

    /// <summary>One arm of a rig: every turn's hash (index = turn, 0 = the founded world), the order log it was stepped with
    /// (the AI's appended orders included), the worlds kept at the requested turns, and each probe's first turn.</summary>
    public sealed class Arm
    {
        private readonly int[] _keptTurns;
        private readonly WorldState[] _kept;
        private readonly Probe[] _probes;
        private readonly long[] _first;
        private readonly long[] _count;

        internal Arm(string label, SimConfig cfg, RigWorld rig, int horizon, string[] hashes, OrderLog log,
            int[] keptTurns, WorldState[] kept, Probe[] probes, long[] first, long[] count)
        {
            Label = label;
            Cfg = cfg;
            Rig = rig;
            Horizon = horizon;
            Hashes = hashes;
            Log = log;
            _keptTurns = keptTurns;
            _kept = kept;
            _probes = probes;
            _first = first;
            _count = count;
        }

        public string Label { get; }
        public SimConfig Cfg { get; }
        public RigWorld Rig { get; }
        public int Horizon { get; }
        public string[] Hashes { get; }
        public OrderLog Log { get; }

        /// <summary>The world at <paramref name="turn"/> (it must have been kept).</summary>
        public WorldState At(int turn)
        {
            for (int i = 0; i < _keptTurns.Length; i++) if (_keptTurns[i] == turn) return _kept[i];
            throw new InvalidOperationException($"arm {Label}: world {turn} was not kept");
        }

        /// <summary>The first turn (the NEXT world's) the probe held on, or −1.</summary>
        public long FirstHeld(string probe) => _first[IndexOf(probe)];

        /// <summary>The number of steps the probe held on.</summary>
        public long TimesHeld(string probe) => _count[IndexOf(probe)];

        private int IndexOf(string probe)
        {
            for (int i = 0; i < _probes.Length; i++) if (_probes[i].Name == probe) return i;
            throw new InvalidOperationException($"arm {Label}: no probe {probe}");
        }
    }

    /// <summary>The production pipeline for <paramref name="cfg"/> on <paramref name="worldgen"/>, delivering
    /// <paramref name="orders"/>.</summary>
    public static TurnExecutor Pipeline(SimConfig cfg, WorldgenConfig worldgen, OrderLog? orders)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, worldgen)), orders);
    }

    /// <summary>The era table the pipeline steps with (the dt of a step is read from it, never from a hash).</summary>
    public static EraTable Eras()
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(era);
    }

    /// <summary>
    /// Founds <paramref name="rig"/> under <paramref name="cfg"/> and steps it <paramref name="horizon"/> turns. Asserts the
    /// founded world is the cylinder. Keeps the worlds at <paramref name="keep"/> (turns, any order) and evaluates every
    /// probe on every step.
    /// </summary>
    public static Arm Run(RigWorld rig, string label, SimConfig cfg, int horizon, int[]? keep = null, Probe[]? probes = null,
        Action<WorldState, WorldState, OrderLog>? observe = null)
    {
        keep ??= [];
        probes ??= [];
        WorldState w = WorldFounding.Found(rig.Founding, cfg, rig.Seed);
        Assert.True(w.Terrain!.WrapX, $"rig {rig.Name}, arm {label}: the founded world is not the shipped cylinder");
        OrderLog? script = rig.Script?.Invoke(w);
        if (script is not null) OrderValidation.ValidateAgainstWorld(script, w);
        // A scripted AI rig steps exactly as `sim run --orders … --ai-empires …` and the UI's end-turn loop do: each turn
        // the player's batch for the turn is appended first, then the AI producer's orders (CliRevoltPolityProducerTests'
        // replica of the loop); a scripted rig with no AI delivers its log as given.
        bool interleave = rig.Ai && script is not null;
        OrderLog log = interleave ? new OrderLog() : script ?? new OrderLog();
        TurnExecutor exec = Pipeline(cfg, rig.Pipeline, log);

        var hashes = new string[horizon + 1];
        var keptTurns = new int[keep.Length];
        var kept = new WorldState[keep.Length];
        var first = new long[probes.Length];
        var count = new long[probes.Length];
        for (int p = 0; p < first.Length; p++) first[p] = -1;
        hashes[0] = WorldHash.ComputeHex(w);
        Keep(0, w);
        for (int t = 1; t <= horizon; t++)
        {
            if (interleave)
            {
                OrderBatch batch = script!.BatchFor(w.Clock.Turn);
                for (int b = 0; b < batch.Count; b++) log.Append(batch[b]);
            }
            if (rig.Ai) AiOrders.Append(log, w, cfg, rig.Pipeline);
            WorldState prev = w;
            w = exec.Step(prev);
            Assert.Equal(t, w.Clock.Turn);
            hashes[t] = WorldHash.ComputeHex(w);
            observe?.Invoke(prev, w, log);
            for (int p = 0; p < probes.Length; p++)
            {
                if (!probes[p].Holds(prev, w, cfg)) continue;
                if (first[p] < 0) first[p] = t;
                count[p]++;
            }
            Keep(t, w);
        }
        for (int i = 0; i < keep.Length; i++)
            Assert.True(kept[i] is not null, $"rig {rig.Name}, arm {label}: world {keep[i]} is beyond the horizon {horizon}");
        return new Arm(label, cfg, rig, horizon, hashes, log, keptTurns, kept, probes, first, count);

        void Keep(int turn, WorldState world)
        {
            for (int i = 0; i < keep.Length; i++)
            {
                if (keep[i] != turn) continue;
                keptTurns[i] = turn;
                kept[i] = world;
            }
        }
    }

    /// <summary>The first turn whose hashes differ, over the turns both arms reached; −1 when none does.</summary>
    public static int FirstDifferent(Arm on, Arm off)
    {
        int n = Math.Min(on.Horizon, off.Horizon);
        for (int t = 0; t <= n; t++) if (on.Hashes[t] != off.Hashes[t]) return t;
        return -1;
    }

    /// <summary>World 0 is the same world in both arms (the switch changes nothing at founding).</summary>
    public static void AssertWorld0Identical(Arm on, Arm off) =>
        Assert.True(on.Hashes[0] == off.Hashes[0], $"{on.Rig.Name}: world 0 differs between {on.Label} and {off.Label}");

    /// <summary>The ON arm's hash series equals <paramref name="again"/>'s over the turns both reached (determinism by a
    /// second run).</summary>
    public static void AssertSameSeries(Arm on, Arm again)
    {
        int n = Math.Min(on.Horizon, again.Horizon);
        for (int t = 0; t <= n; t++)
            Assert.True(on.Hashes[t] == again.Hashes[t], $"{on.Rig.Name}: a second ON run differs at turn {t}");
    }

    /// <summary>THE MEASURED WRITE SET: the canonical-layout tables (and the header, should it differ) in which
    /// <paramref name="a"/> and <paramref name="b"/> differ — a differing row count or any differing row
    /// (Sim.Cli.SnapshotDiff, the `sim diff` walk) — sorted ordinally.</summary>
    public static string[] WriteSet(WorldState a, WorldState b)
    {
        Sim.Cli.SnapshotDiff.Result r = Sim.Cli.SnapshotDiff.Compare(a, b);
        var names = new List<string>();
        foreach (Sim.Cli.SnapshotDiff.TableSummary t in r.Tables)
            if (t.RowsDiffering > 0 || t.RowsA != t.RowsB) names.Add(t.Table);
        names.Sort(StringComparer.Ordinal);
        return names.ToArray();
    }

    /// <summary>The write set contains no topology table except those in <paramref name="featureWrites"/>.</summary>
    public static void AssertNoTopologyTable(string[] writeSet, params string[] featureWrites)
    {
        foreach (string table in writeSet)
        {
            if (Array.IndexOf(TopologyTables, table) < 0 || Array.IndexOf(featureWrites, table) >= 0) continue;
            Assert.Fail($"the write set [{string.Join(", ", writeSet)}] contains the topology table {table}, which the feature does not write");
        }
    }

    /// <summary>Every order stamped before <paramref name="turn"/> is the same order, in the same place, in both logs.</summary>
    public static void AssertLogsEqualBefore(OrderLog a, OrderLog b, long turn)
    {
        OrderRecord[] x = Before(a, turn), y = Before(b, turn);
        Assert.Equal(x.Length, y.Length);
        for (int i = 0; i < x.Length; i++) Assert.Equal(x[i], y[i]);
    }

    /// <summary>The orders stamped exactly <paramref name="turn"/> agree in both logs, except the orders of
    /// <paramref name="kind"/> (the feature's own), which only <paramref name="withKind"/> holds.</summary>
    public static void AssertLogsEqualAtExcept(OrderLog withKind, OrderLog without, long turn, OrderKind kind)
    {
        var x = new List<OrderRecord>();
        var y = new List<OrderRecord>();
        for (int i = 0; i < withKind.Count; i++) if (withKind[i].Turn == turn && withKind[i].Kind != kind) x.Add(withKind[i]);
        for (int i = 0; i < without.Count; i++)
        {
            if (without[i].Turn != turn) continue;
            Assert.NotEqual(kind, without[i].Kind);
            y.Add(without[i]);
        }
        Assert.Equal(x, y);
    }

    private static OrderRecord[] Before(OrderLog log, long turn)
    {
        var list = new List<OrderRecord>();
        for (int i = 0; i < log.Count; i++) if (log[i].Turn < turn) list.Add(log[i]);
        return list.ToArray();
    }

    /// <summary>The stamp of the first order of <paramref name="kind"/> in <paramref name="log"/>, or −1.</summary>
    public static long FirstStamp(OrderLog log, OrderKind kind)
    {
        for (int i = 0; i < log.Count; i++) if (log[i].Kind == kind) return log[i].Turn;
        return -1;
    }

    // ------------------------------------------------------------------ seam exposure (review F7-WRAP-NEVER-EXERCISED)

    /// <summary>The number of unordered pairs of settlements one polity controls on <paramref name="w"/> whose shortest
    /// east-west separation on the cylinder crosses the seam (<see cref="MapTopology.Dx"/> wrapped differs from the raw
    /// x2 − x1): the pairs whose distance a topology-blind (raw dx) reading would get wrong.</summary>
    public static int SeamCrossingPairs(IReadOnlyWorldState w)
    {
        int size = w.Terrain!.Size, n = 0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            if (!EmpireQuery.TryGetController(w, w.Settlements[i].Id, out PolityId pi)) continue;
            for (int j = i + 1; j < w.Settlements.Count; j++)
            {
                if (!EmpireQuery.TryGetController(w, w.Settlements[j].Id, out PolityId pj) || pj.Value != pi.Value) continue;
                int xi = w.Settlements[i].SiteCell % size, xj = w.Settlements[j].SiteCell % size;
                if (MapTopology.Dx(xi, xj, size, wrap: true) != xj - xi) n++;
            }
        }
        return n;
    }

    /// <summary>The number of unordered pairs of ALL settlements of <paramref name="w"/> (whoever controls them) whose shortest
    /// east-west separation crosses the seam — the pairs a reading of every settlement pair (the weather kernel) would get
    /// wrong with raw dx.</summary>
    public static int SeamCrossingPairsAll(IReadOnlyWorldState w)
    {
        int size = w.Terrain!.Size, n = 0;
        for (int i = 0; i < w.Settlements.Count; i++)
            for (int j = i + 1; j < w.Settlements.Count; j++)
            {
                int xi = w.Settlements[i].SiteCell % size, xj = w.Settlements[j].SiteCell % size;
                if (MapTopology.Dx(xi, xj, size, wrap: true) != xj - xi) n++;
            }
        return n;
    }

    /// <summary>The number of controlled settlements of <paramref name="w"/> whose NEAREST-FIRST draw order over their
    /// polity's settlements (<see cref="RoadDevelopmentQuery.PayingSettlements(IReadOnlyWorldState, PolityId, SettlementId, SettlementId)"/>
    /// with the settlement as both ends — relief's and the purse's donor order) differs from the order a flat (raw dx)
    /// distance would give: where a topology-blind donor order is observable at all.</summary>
    public static int WrappedDonorOrders(IReadOnlyWorldState w)
    {
        int size = w.Terrain!.Size, n = 0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            if (!EmpireQuery.TryGetController(w, s, out PolityId polity)) continue;
            SettlementId[] wrapped = RoadDevelopmentQuery.PayingSettlements(w, polity, s, s);
            var flat = new (long D, int Id)[wrapped.Length];
            for (int k = 0; k < wrapped.Length; k++)
                flat[k] = (MapTopology.SquaredPx(SiteOf(w, wrapped[k]), w.Settlements[i].SiteCell, size, wrap: false), wrapped[k].Value);
            Array.Sort(flat, static (x, y) => x.D != y.D ? x.D.CompareTo(y.D) : x.Id.CompareTo(y.Id));
            for (int k = 0; k < flat.Length; k++)
            {
                if (flat[k].Id == wrapped[k].Value) continue;
                n++;
                break;
            }
        }
        return n;
    }

    private static int SiteOf(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == s) return w.Settlements[i].SiteCell;
        throw new InvalidOperationException($"no settlement {s.Value}");
    }

    // ------------------------------------------------------------------ first-action predicates (shared, never the hash)

    /// <summary>Whether intra-polity grain relief moved grain in the step <paramref name="prev"/> → <paramref name="next"/>,
    /// read off the grain BOOKS alone (EmpireFoodAllocationTests' predicate): some settlement's grain residual opening +
    /// harvest − eaten − closing is NEGATIVE — it ate grain it neither held nor harvested. Relief books its moves through
    /// Ledger.Transfer, which carries no reason (only Ledger.Flow rows are published), so the books are the independent
    /// reading; on a world with no raid, trade or founding in the step, relief is the only inbound grain transfer.</summary>
    public static readonly Probe ReliefMovedGrain = new("relief", static (prev, next, cfg) =>
    {
        for (int s = 0; s < next.Settlements.Count; s++)
            if (GrainResidual(prev, next, next.Settlements[s].Id, cfg) < 0) return true;
        return false;
    });

    /// <summary>A settlement's grain books over the step <paramref name="prev"/> → <paramref name="next"/>: opening +
    /// harvested − eaten − closing (0 when it has no grain row on either side). Negative = it ate grain it neither held nor
    /// harvested (relief received); positive = grain left it other than by eating (relief given, spoilage, the bound).</summary>
    public static long GrainResidual(IReadOnlyWorldState prev, IReadOnlyWorldState next, SettlementId settlement, SimConfig cfg)
    {
        var grain = new GoodId(cfg.Goods!.IdOf("grain"));
        int p = GoodStockIndex.IndexOf(prev.GoodStocks, settlement, grain);
        int n = GoodStockIndex.IndexOf(next.GoodStocks, settlement, grain);
        if (p < 0 || n < 0) return 0;
        GoodStockRow r = next.GoodStocks[n];
        return prev.GoodStocks[p].Amount.Value + r.LastProducedUnits - r.LastConsumptionEatenUnits - r.Amount.Value;
    }

    /// <summary>Whether a granary stands anywhere on NEXT.</summary>
    public static readonly Probe GranaryStands = new("granary-stands", static (_, next, cfg) =>
    {
        int project = GranaryEffects.ProjectId(cfg);
        for (int i = 0; i < next.Structures.Count; i++)
            if (next.Structures[i].ProjectId == project && next.Structures[i].Count > 0) return true;
        return false;
    });

    /// <summary>Whether a granary can ACT on the step that reads NEXT as its PREV: its local farm factor
    /// (<see cref="GranaryEffects.FarmYieldFactor(IReadOnlyWorldState, SimConfig, SettlementId)"/>, the literal 1.0 at a
    /// wild-food settlement) differs from 1 at some settlement, or its civilization's famine factor differs from 1 where
    /// the dead-zone deficit is positive (GranaryAttributionTests' predicate, through the public factor seams).</summary>
    public static readonly Probe GranaryActs = new("granary-acts", static (prev, next, cfg) =>
    {
        for (int i = 0; i < next.Settlements.Count; i++)
        {
            SettlementId s = next.Settlements[i].Id;
            if (GranaryEffects.FarmYieldFactor(next, cfg, s) != 1.0) return true;
            if (GranaryEffects.FamineIntensityFactor(next, cfg, s) == 1.0) continue;
            double d = FoodState.DeficitRatio(next, s);
            if (FoodState.EffectiveDeficit(d, FoodState.Of(next, s, cfg, out _), cfg) > 0.0) return true;
        }
        return false;
    });

    /// <summary>Whether NEXT carries a trade flow.</summary>
    public static readonly Probe Trades = new("trades", static (_, next, _) => next.TradeFlows.Count > 0);

    /// <summary>Whether a construction queue row survives the step (NEXT ends with a queued entry): the only state in which
    /// incremental construction (WS1) can act — a head that completes in its delivery step never carries progress.</summary>
    public static readonly Probe QueueRowSurvives = new("queue-row", static (_, next, _) => next.ConstructionQueue.Count > 0);

    /// <summary>Whether NEXT carries a construction progress row (WS1's own table).</summary>
    public static readonly Probe ProgressRow = new("progress-row", static (_, next, _) => next.ConstructionProgress.Count > 0);

    /// <summary>Whether NEXT carries a published hold (D-055's ConstructionHolds).</summary>
    public static readonly Probe HoldRow = new("hold-row", static (_, next, _) => next.ConstructionHolds.Count > 0);
}
