using System.Globalization;
using System.Numerics;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// One measurement run of the research economy (D-064 evidence; test code, never sim code). The canonical world (the
/// PacingReferenceMeasurement setup: shipped worldgen, one AI Empire, polity 2), the shipped content with only the stated
/// research switches changed. Two optional MEASUREMENT MODELS sit outside the engine and are stated in every output:
/// <list type="bullet">
/// <item><b>Human proxy</b> (<see cref="RunOptions.HumanDelay"/>): the AI's own research orders, except that after a lane
/// loses its destination (a completion clears it, a repeatable level included) the proxy re-targets that lane only after
/// <c>d</c> turns — a player who does not re-order at once. Every other order is the AI's.</item>
/// <item><b>RP reserve</b> (<see cref="RunOptions.ReserveCapTurns"/> &gt; 0): a technology-lane RP reserve. On a step whose
/// technology lane funds nothing (no destination and nothing to assist, or a destination waiting on the other tree) the
/// step's RP goes into the reserve up to a cap of <c>k</c> × the current RP pool instead of being discarded. On a funded
/// step the reserve tops up the funded node: up to its remaining cost after this step's pool, at most
/// <see cref="RunOptions.DrawLimitTurns"/> × the pool. The top-up is written into the node's progress row before the step,
/// so the engine pays the rest exactly as it would. The engine is unchanged; CP is untouched (the civics lane's bank
/// already exists).</item>
/// </list>
/// The per-step funding is reconstructed with the engine's own planner (ResearchRoutePlanner.Fund over PREV plus this
/// step's orders, as ResearchSystem applies them), and every funded step is checked against the outcome (mismatches are
/// reported). RP accounting per step: spent = technology progress added + RP that assisted a civic (civic progress
/// added − CP spent − civic credits), credits excluded; lost = pool − spent − reserve deposit, split into SURPLUS (lane
/// funded a node) and EMPTY (lane funded nothing).
/// </summary>
internal static class ResearchPacingRunner
{
    internal static readonly PolityId Ai = new(2);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    internal sealed record RunOptions(
        ulong Seed, bool Overflow, string Label, int Turns = 1630, int? HumanDelay = null,
        double ReserveCapTurns = 0.0, double DrawLimitTurns = double.PositiveInfinity,
        bool Replay = false, long SaveLoadAt = -1, bool DrawLosesSurplus = false, bool GateAware = false);

    internal sealed record RunResult(string Summary, string? Hash, string? ReplayHash, string? SaveLoadHash);

    /// <summary>The shipped research.json with tuning.researchOverflow set to <paramref name="overflow"/> and, when
    /// <paramref name="gateAware"/>, tuning.aiGateAwareGoals set to true (ADR-052; otherwise left absent); nothing else.</summary>
    internal static SimConfig Config(bool overflow, bool gateAware = false)
    {
        System.Text.Json.Nodes.JsonObject doc;
        using (var s = Sim.Data.DataFiles.OpenResearch()) doc = System.Text.Json.Nodes.JsonNode.Parse(s)!.AsObject();
        doc["tuning"]!.AsObject()["researchOverflow"] = overflow;
        if (gateAware) doc["tuning"]!.AsObject()["aiGateAwareGoals"] = true;
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return TestConfigs.Sim() with { Research = ResearchContentLoader.Load(doc.ToJsonString(), GoodsConfigLoader.Load(goods)) };
    }

    private static TurnExecutor Executor(SimConfig cfg, WorldgenConfig shipped, OrderLog log)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, shipped)), log);
    }

    internal static RunResult Run(RunOptions o, string? outDir)
    {
        SimConfig cfg = Config(o.Overflow, o.GateAware);
        WorldgenConfig shipped = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec = Executor(cfg, shipped, log);
        WorldState w = WorldFounding.Found(shipped with { AiEmpires = 1 }, cfg, o.Seed);
        ResearchContent rc = cfg.Research!;
        AgeContent ages = cfg.Ages!;
        int n = rc.Nodes.Count;
        bool reserveOn = o.ReserveCapTurns > 0.0;

        var finite = new int[2, 10];
        var doneTurn = new long[2, 10];
        for (int a = 0; a < 10; a++) { doneTurn[0, a] = -1; doneTurn[1, a] = -1; }
        foreach (ResearchNode node in rc.Nodes)
            if (!node.HasRepeatableDescriptor) finite[node.Tree == ResearchTree.Technology ? 0 : 1, node.AgeNumber]++;
        var doneAt = new long[n];
        Array.Fill(doneAt, -1L);
        var milestoneMet = new SortedDictionary<string, long>(StringComparer.Ordinal);   // test bookkeeping, sorted keys
        var turns = new StringBuilder("turn,age,pool,spent,lostSurplus,lostEmpty,deposit,draw,reserve,techClass,civClass," +
                                      "techDone,civDone,levels,cpGenMilli,cpSpentMilli,cpBalanceMilli\n");
        var rpGen = new double[10];
        var rpSpent = new double[10];
        var lostSurplus = new double[10];
        var lostEmpty = new double[10];
        var turnsAt = new int[10];
        var classSteps = new int[2, 5];
        var techClassByAge = new int[10, 5];   // the technology lane's step classes by polity Age
        double reserve = 0.0, deposits = 0.0, draws = 0.0, reserveMax = 0.0, drawMaxRatio = 0.0;
        int mismatches = 0, maxTechPerTurn = 0, maxCivPerTurn = 0, maxLevelsPerTurn = 0;
        long firstLevelTurn = -1;
        var emptySince = new long[] { -1, -1 };
        var waitDetail = new SortedDictionary<string, int>(StringComparer.Ordinal);   // test bookkeeping, sorted keys

        for (int t = 0; t < o.Turns; t++)
        {
            OrderRecord[] orders = AiOrders.For(w, cfg, shipped);
            bool[] completed = ResearchQuery.CompletedMask(w, rc, Ai);
            int[] dest = ResearchQuery.Destinations(w, rc, Ai, completed);
            if (o.HumanDelay is int d)
                for (int lane = 0; lane < 2; lane++)
                    emptySince[lane] = dest[lane] < 0 ? (emptySince[lane] < 0 ? t : emptySince[lane]) : -1;
            foreach (OrderRecord rec in orders)
            {
                if (o.HumanDelay is int delay && rec.Kind == OrderKind.SetResearchTarget && rec.ActorId == Ai.Value)
                {
                    int lane = rec.TargetId == ResearchQuery.ClearTechnologyTargetId ? 0
                        : rec.TargetId == ResearchQuery.ClearCivicsTargetId ? 1
                        : ResearchRoutePlanner.LaneOf(rc, rc.IndexOf(new ResearchNodeId(rec.TargetId)), true);
                    if (emptySince[lane] >= 0 && t - emptySince[lane] < delay) continue;   // the proxy has not re-ordered yet
                }
                log.Append(rec);
                if (rec.Kind != OrderKind.SetResearchTarget || rec.ActorId != Ai.Value) continue;
                if (rec.TargetId == ResearchQuery.ClearTechnologyTargetId) { dest[0] = -1; continue; }
                if (rec.TargetId == ResearchQuery.ClearCivicsTargetId) { dest[1] = -1; continue; }
                int idx = rc.IndexOf(new ResearchNodeId(rec.TargetId));
                if (ResearchQuery.IsDestinationAdmissible(rc, idx, completed)) dest[ResearchRoutePlanner.LaneOf(rc, idx, true)] = idx;
            }
            bool[] available = ResearchQuery.AvailableMask(rc, completed, ResearchQuery.LevelMask(w, rc, Ai, completed));
            double[] remaining = ResearchQuery.RemainingCosts(w, rc, Ai);
            LaneFunding[] lanes = ResearchRoutePlanner.Fund(rc, completed, available, remaining, dest, true);
            var fundable = new bool[2];
            for (int i = 0; i < n; i++) if (available[i]) fundable[rc.Nodes[i].Tree == ResearchTree.Technology ? 0 : 1] = true;
            var cls = new int[2];
            for (int lane = 0; lane < 2; lane++)
            {
                cls[lane] = dest[lane] < 0
                    ? (lanes[lane].Current >= 0 ? 3 : fundable[lane] ? 0 : 1)
                    : (lanes[lane].Current < 0 ? 2 : 4);
                classSteps[lane, cls[lane]]++;
            }

            int age = AgeQuery.CurrentAge(w, ages, Ai);
            techClassByAge[age, cls[0]]++;
            if (cls[0] == 2 && lanes[0].Plan is { } plan)
            {
                // Why the technology lane waits: the outstanding route nodes of the OTHER tree, by structural cause.
                string why = "none-of-the-other-tree";
                for (int k = 0; k < plan.Route.Length; k++)
                    if (rc.Nodes[plan.Route[k]].Tree == ResearchTree.Civics)
                    { why = plan.Cause[k] == RouteReason.Sweep ? "civic-sweep-cohort" : plan.Cause[k] == RouteReason.Stage ? "civic-stage" : "civic-prerequisite"; break; }
                string key = string.Create(Inv, $"A{age} {rc.Nodes[plan.Destination].Id} (A{rc.Nodes[plan.Destination].AgeNumber}) {why}");
                waitDetail[key] = waitDetail.TryGetValue(key, out int c0) ? c0 + 1 : 1;
            }
            double pool = ResearchQuery.ResearchPointPool(w, rc, Ai);
            int cur = lanes[0].Current;
            double deposit = 0.0, draw = 0.0, drawLost = 0.0;
            if (reserveOn)
            {
                if (cur < 0)
                {
                    double cap = o.ReserveCapTurns * pool;
                    deposit = Math.Min(pool, Math.Max(0.0, cap - reserve));
                    reserve += deposit;
                    deposits += deposit;
                }
                else if (o.DrawLosesSurplus && reserve > 0.0)
                {
                    // LOSSY draw: up to the draw limit joins this step's points and obeys the same one-node rule — what the
                    // funded node cannot absorb after the pool is discarded, exactly as surplus RP is (overflow OFF only).
                    draw = Math.Min(reserve, o.DrawLimitTurns * pool);
                    double applied = Math.Min(draw, Math.Max(0.0, remaining[cur] - pool));
                    if (applied > 0.0) AddProgress(w, rc.Nodes[cur].Key, applied);
                    reserve -= draw;
                    draws += draw;
                    drawLost = draw - applied;
                    if (pool > 0.0) drawMaxRatio = Math.Max(drawMaxRatio, draw / pool);
                }
                else if (reserve > 0.0 && remaining[cur] - pool > 0.0)
                {
                    draw = Math.Min(Math.Min(reserve, remaining[cur] - pool), o.DrawLimitTurns * pool);
                    if (draw > 0.0)
                    {
                        AddProgress(w, rc.Nodes[cur].Key, draw);
                        reserve -= draw;
                        draws += draw;
                        if (pool > 0.0) drawMaxRatio = Math.Max(drawMaxRatio, draw / pool);
                    }
                }
                reserveMax = Math.Max(reserveMax, reserve);
            }

            WorldState prev = w;
            double[] before = ProgressOf(prev, rc, n);
            var cost = new double[n];
            for (int i = 0; i < n; i++) cost[i] = ResearchQuery.EffectiveCost(prev, rc, Ai, i);
            (double techCr0, double civCr0) = Credits(prev, rc);
            long cpSpent0 = ResearchQuery.CivicsPointsAccount(prev, Ai).SpentMilli;
            var levelBefore = new int[n];
            for (int i = 0; i < n; i++) if (rc.Nodes[i].HasRepeatableDescriptor) levelBefore[i] = ResearchQuery.Level(prev, Ai, rc.Nodes[i].Key);

            w = exec.Step(prev);

            bool[] after = ResearchQuery.CompletedMask(w, rc, Ai);
            double[] progressAfter = ProgressOf(w, rc, n);
            double techAdded = 0.0, civAdded = 0.0;
            int techDone = 0, civDone = 0, levelsGained = 0;
            for (int i = 0; i < n; i++)
            {
                bool levelled = rc.Nodes[i].HasRepeatableDescriptor && ResearchQuery.Level(w, Ai, rc.Nodes[i].Key) > levelBefore[i];
                bool newly = after[i] && !completed[i];
                double reached = newly || levelled ? cost[i] : progressAfter[i];
                if (rc.Nodes[i].Tree == ResearchTree.Technology) techAdded += reached - before[i];
                else civAdded += reached - before[i];
                if (levelled) levelsGained++;
                if (newly && !rc.Nodes[i].HasRepeatableDescriptor)
                {
                    doneAt[i] = w.Clock.Turn;
                    if (rc.Nodes[i].Tree == ResearchTree.Technology) techDone++; else civDone++;
                }
            }
            if (levelsGained > 0 && firstLevelTurn < 0) firstLevelTurn = w.Clock.Turn;
            maxTechPerTurn = Math.Max(maxTechPerTurn, techDone);
            maxCivPerTurn = Math.Max(maxCivPerTurn, civDone);
            maxLevelsPerTurn = Math.Max(maxLevelsPerTurn, levelsGained);
            (double techCr1, double civCr1) = Credits(w, rc);
            CivicsPointsRow cp = ResearchQuery.CivicsPointsAccount(w, Ai);
            double rpToCivic = cur >= 0 && rc.Nodes[cur].Tree == ResearchTree.Civics
                ? Math.Max(0.0, civAdded - (civCr1 - civCr0) - (cp.SpentMilli - cpSpent0) / 1000.0)
                : 0.0;
            double spent = Math.Min(pool, Math.Max(0.0, techAdded - (techCr1 - techCr0) + rpToCivic));
            double lost = Math.Max(0.0, pool - spent - deposit);
            rpGen[age] += pool;
            rpSpent[age] += spent;
            if (cur >= 0) lostSurplus[age] += lost + drawLost; else lostEmpty[age] += lost;
            turnsAt[age]++;
            if (cur >= 0 && pool > 0.0 && !(progressAfter[cur] > before[cur] || (after[cur] && !completed[cur])
                || (rc.Nodes[cur].HasRepeatableDescriptor && ResearchQuery.Level(w, Ai, rc.Nodes[cur].Key) > levelBefore[cur])))
                mismatches++;

            var count = new int[2, 10];
            for (int i = 0; i < n; i++)
                if (after[i] && !rc.Nodes[i].HasRepeatableDescriptor)
                    count[rc.Nodes[i].Tree == ResearchTree.Technology ? 0 : 1, rc.Nodes[i].AgeNumber]++;
            for (int tr = 0; tr < 2; tr++)
                for (int a = 1; a < 10; a++)
                    if (doneTurn[tr, a] < 0 && finite[tr, a] > 0 && count[tr, a] == finite[tr, a]) doneTurn[tr, a] = w.Clock.Turn;

            AgeDefinition? next = AgeQuery.NextAge(w, ages, Ai);
            if (next?.Entry is { } entry)
            {
                foreach (AgeMilestone m in entry.Core) Note(m, true);
                foreach (AgeMilestone m in entry.Supporting) Note(m, false);
            }
            void Note(AgeMilestone m, bool core)
            {
                string key = string.Create(Inv, $"A{next!.Key}|{(core ? "core" : "supporting")}|{m.Id}");
                if (!milestoneMet.ContainsKey(key) && AgeQuery.Milestone(w, Ai, m).Met) milestoneMet[key] = w.Clock.Turn;
            }

            int levelsNow = 0;
            for (int r = 0; r < w.ResearchLevels.Count; r++) if (w.ResearchLevels[r].Polity.Value == Ai.Value) levelsNow += w.ResearchLevels[r].Level;
            turns.Append(Inv, $"{w.Clock.Turn},{age},{pool:0.###},{spent:0.###},{(cur >= 0 ? lost + drawLost : 0):0.###},{(cur < 0 ? lost : 0):0.###},")
                 .Append(Inv, $"{deposit:0.###},{draw:0.###},{reserve:0.###},{cls[0]},{cls[1]},{techDone},{civDone},{levelsNow},")
                 .Append(Inv, $"{cp.GeneratedMilli},{cp.SpentMilli},{cp.BalanceMilli}\n");
        }

        string hash = WorldHash.ComputeHex(w);
        string? replayHash = null, saveLoadHash = null;
        if (o.Replay)
        {
            // Replay: a fresh executor over the recorded order log only (no AI, no harness) from the founding world.
            TurnExecutor replay = Executor(cfg, shipped, log);
            WorldState r = WorldFounding.Found(shipped with { AiEmpires = 1 }, cfg, o.Seed);
            for (int t = 0; t < o.Turns; t++)
            {
                if (o.SaveLoadAt >= 0 && r.Clock.Turn == o.SaveLoadAt)
                {
                    using var ms = new MemoryStream();
                    Snapshot.Save(r, ms);
                    ms.Position = 0;
                    WorldState loaded = Snapshot.Load(ms, r.Terrain);
                    saveLoadHash = WorldHash.ComputeHex(loaded) == WorldHash.ComputeHex(r) ? "equal" : "DIFFERENT";
                    r = loaded;
                }
                r = replay.Step(r);
            }
            replayHash = WorldHash.ComputeHex(r);
        }

        var sb = new StringBuilder();
        sb.AppendLine(Inv, $"seed {o.Seed} overflow {(o.Overflow ? "ON" : "OFF")}{(o.GateAware ? " gate-aware" : "")} [{o.Label}] proxy-delay {(o.HumanDelay is int hd ? hd.ToString(Inv) : "none")} " +
                           $"reserve-cap {(reserveOn ? o.ReserveCapTurns.ToString("0.##", Inv) : "none")} draw-limit {(double.IsPositiveInfinity(o.DrawLimitTurns) ? "none" : o.DrawLimitTurns.ToString("0.##", Inv))}{(o.DrawLosesSurplus ? " draw-lossy" : "")}");
        sb.Append("  Age entered (effective turn):");
        for (int r = 0; r < w.AgeTransitions.Count; r++)
            if (w.AgeTransitions[r].Polity.Value == Ai.Value) sb.Append(Inv, $" A{w.AgeTransitions[r].ToAge}={w.AgeTransitions[r].EffectiveTurn}");
        sb.AppendLine();
        sb.Append("  Age complete (turn) technology:");
        for (int a = 1; a < 10; a++) sb.Append(Inv, $" A{a}={doneTurn[0, a]}");
        sb.AppendLine();
        sb.Append("  Age complete (turn) civics:    ");
        for (int a = 1; a < 10; a++) sb.Append(Inv, $" A{a}={doneTurn[1, a]}");
        sb.AppendLine();
        sb.Append("  RP by polity Age (turns, generated, spent, lost %):");
        for (int a = 1; a < 10; a++)
            if (turnsAt[a] > 0)
                sb.Append(Inv, $" A{a}[{turnsAt[a]}t {rpGen[a]:0} {rpSpent[a]:0} {(rpGen[a] > 0 ? 100.0 * (rpGen[a] - rpSpent[a]) / rpGen[a] : 0):0.0}%]");
        sb.AppendLine();
        sb.Append("  RP lost by Age (surplus past a funded node / lane funded nothing):");
        for (int a = 1; a < 10; a++)
            if (turnsAt[a] > 0) sb.Append(Inv, $" A{a}[{lostSurplus[a]:0} {lostEmpty[a]:0}]");
        sb.AppendLine();
        string[] names = ["nodest+fundable", "nodest+nothing", "waiting", "assisting", "funded"];
        for (int lane = 0; lane < 2; lane++)
        {
            sb.Append(lane == 0 ? "  technology lane steps:" : "  civics lane steps:    ");
            for (int k = 0; k < 5; k++) sb.Append(Inv, $" {names[k]}={classSteps[lane, k]}");
            sb.AppendLine();
        }
        sb.Append("  technology lane by polity Age (waiting / no destination, nothing available / no destination, fundable):");
        for (int a = 1; a < 10; a++)
            if (turnsAt[a] > 0) sb.Append(Inv, $" A{a}[{techClassByAge[a, 2]} {techClassByAge[a, 1]} {techClassByAge[a, 0]}]");
        sb.AppendLine();
        if (waitDetail.Count > 0)
        {
            sb.Append("  technology waiting steps (polity Age, destination (its Age), first blocking node of the other tree):");
            foreach (KeyValuePair<string, int> kv in waitDetail) sb.Append(Inv, $" [{kv.Key}: {kv.Value}]");
            sb.AppendLine();
        }
        if (reserveOn)
            sb.AppendLine(Inv, $"  reserve: deposited {deposits:0} drawn {draws:0} left {reserve:0} max {reserveMax:0}; largest draw {drawMaxRatio:0.##}x the turn's pool");
        CivicsPointsRow end = ResearchQuery.CivicsPointsAccount(w, Ai);
        sb.AppendLine(Inv, $"  CP generated {end.GeneratedMilli / 1000.0:0} spent {end.SpentMilli / 1000.0:0} banked {end.BalanceMilli / 1000.0:0}; identity {(end.GeneratedMilli - end.SpentMilli == end.BalanceMilli ? "holds" : "BROKEN")}");
        sb.AppendLine(Inv, $"  bursts: most technologies completed in one turn {maxTechPerTurn}, civics {maxCivPerTurn}, repeatable levels {maxLevelsPerTurn}; first level turn {firstLevelTurn}");
        int tech = ResearchQuery.CompletedNodes(w, rc, Ai, ResearchTree.Technology).Length;
        int civ = ResearchQuery.CompletedNodes(w, rc, Ai, ResearchTree.Civics).Length;
        long levels = 0;
        for (int r = 0; r < w.ResearchLevels.Count; r++) if (w.ResearchLevels[r].Polity.Value == Ai.Value) levels += w.ResearchLevels[r].Level;
        var mine = new HashSet<int>();   // test bookkeeping, not sim logic
        for (int r = 0; r < w.Controls.Count; r++) if (w.Controls[r].Polity.Value == Ai.Value) mine.Add(w.Controls[r].Place.Value);
        long structures = 0;
        for (int r = 0; r < w.Structures.Count; r++) if (mine.Contains(w.Structures[r].Settlement.Value)) structures += w.Structures[r].Count;
        int roads = 0, surveys = 0, explorations = 0, institutions = 0;
        long known = 0;
        for (int r = 0; r < w.RoadDevelopments.Count; r++) if (w.RoadDevelopments[r].Polity.Value == Ai.Value) roads++;
        for (int r = 0; r < w.SettlementSurveys.Count; r++) if (w.SettlementSurveys[r].Polity.Value == Ai.Value) surveys++;
        for (int r = 0; r < w.Explorations.Count; r++) if (w.Explorations[r].Polity.Value == Ai.Value) explorations++;
        for (int r = 0; r < w.KnownExtents.Count; r++) if (w.KnownExtents[r].Polity.Value == Ai.Value) known += BitOperations.PopCount(w.KnownExtents[r].Bits);
        for (int r = 0; r < w.Institutions.Count; r++) if (mine.Contains(w.Institutions[r].Settlement.Value)) institutions++;
        sb.AppendLine(Inv, $"  end: Age {AgeQuery.CurrentAge(w, ages, Ai)} tech {tech} civics {civ} levels {levels} population {ResearchQuery.Population(w, Ai):0} settlements {mine.Count}");
        sb.AppendLine(Inv, $"  infrastructure: structures {structures} road developments {roads} institutions {institutions}; discovery: surveys {surveys} explorations {explorations} known cells {known}");
        string replayText = replayHash is null ? "" : "; replay " + (replayHash == hash ? "EQUAL" : "DIFFERENT " + replayHash);
        string saveText = saveLoadHash is null ? "" : string.Create(Inv, $"; save/load at turn {o.SaveLoadAt} {saveLoadHash}");
        sb.AppendLine(Inv, $"  reconstruction mismatches {mismatches}; final hash {hash}{replayText}{saveText}");

        if (outDir is not null)
        {
            Directory.CreateDirectory(outDir);
            string stem = Path.Combine(outDir, string.Create(Inv, $"{o.Label}-s{o.Seed}"));
            File.WriteAllText(stem + "-turns.csv", turns.ToString());
            var nodes = new StringBuilder("id,tree,age,repeatable,completedTurn\n");
            for (int i = 0; i < n; i++)
                nodes.Append(Inv, $"{rc.Nodes[i].Id},{(rc.Nodes[i].Tree == ResearchTree.Technology ? "tech" : "civics")},{rc.Nodes[i].AgeNumber},{(rc.Nodes[i].HasRepeatableDescriptor ? 1 : 0)},{doneAt[i]}\n");
            File.WriteAllText(stem + "-nodes.csv", nodes.ToString());
            var ms = new StringBuilder("key,firstMetTurn\n");
            foreach (KeyValuePair<string, long> kv in milestoneMet) ms.Append(Inv, $"{kv.Key},{kv.Value}\n");
            File.WriteAllText(stem + "-milestones.csv", ms.ToString());
            File.WriteAllText(stem + "-summary.txt", sb.ToString());
        }
        return new RunResult(sb.ToString(), hash, replayHash, saveLoadHash);
    }

    private static void AddProgress(WorldState w, ResearchNodeId node, double amount)
    {
        for (int r = 0; r < w.ResearchProgress.Count; r++)
        {
            ResearchProgressRow row = w.ResearchProgress[r];
            if (row.Polity.Value == Ai.Value && row.Node.Value == node.Value)
            {
                w.ResearchProgress[r] = row with { Progress = row.Progress + amount };
                return;
            }
        }
        w.ResearchProgress.Add(new ResearchProgressRow(Ai, node, amount));
    }

    private static double[] ProgressOf(IReadOnlyWorldState w, ResearchContent rc, int n)
    {
        var p = new double[n];
        for (int r = 0; r < w.ResearchProgress.Count; r++)
        {
            ResearchProgressRow row = w.ResearchProgress[r];
            if (row.Polity.Value != Ai.Value) continue;
            int i = rc.IndexOf(row.Node);
            if (i >= 0) p[i] = row.Progress;
        }
        return p;
    }

    private static (double Tech, double Civic) Credits(IReadOnlyWorldState w, ResearchContent rc)
    {
        double tech = 0.0, civic = 0.0;
        for (int r = 0; r < w.ResearchCredits.Count; r++)
        {
            ResearchCreditRow row = w.ResearchCredits[r];
            if (row.Polity.Value != Ai.Value) continue;
            int i = rc.IndexOf(row.Node);
            if (i < 0) continue;
            if (rc.Nodes[i].Tree == ResearchTree.Technology) tech += row.Amount;
            else civic += row.Amount;
        }
        return (tech, civic);
    }
}
