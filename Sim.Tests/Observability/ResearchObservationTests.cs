using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.Observability.Forensic;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Observability;

/// <summary>
/// D-086 DE1 (the Director, 2026-11-11: "it should record which node was researched and how much and which turn also";
/// readings DE1-R1..R3 (a)) — THE RESEARCH ROWS OF THE RUN RECORD (telemetry/v6). Exact values on the research rigs (a
/// population that makes exactly 1 000 RP and 1 000 CP a turn), the completion row on the turn the node completes, the
/// turn-exact delivery of a research order, acceleration credit on a node that is not the target, the city-states after the
/// roster polities, the production world (the player and the AI empire) checked turn by turn against a linear-scan reading
/// of the research tables, and the populated JSON round trip through the writer and the reader. Verification round 1
/// (RR-R1-01) added the two paths the shipped content uses that no rig reached — a repeatable's every level, and
/// foreign-exposure credit paid over several steps — and a v6 line whose step researched nothing, each pinned with the
/// rig's own numbers (the linear-scan reading shares the observer's definitions, so it cannot check them). Verification
/// round 2 added the bank rows of city-states, which the accounts table keeps first (RR-R2-01), and a completion whose cost
/// fell below the progress the node already held (RR-R2-02).
/// </summary>
public class ResearchObservationTests
{
    private static ResearchStep[] Rows(IReadOnlyWorldState prev, IReadOnlyWorldState next, ResearchContent content) =>
        ResearchObservation.Observe(prev, next, content).Steps;

    private static ResearchStep? Row(ResearchStep[] rows, int holder, int node)
    {
        foreach (ResearchStep r in rows)
            if (r.Holder == holder && r.Node == node) return r;
        return null;
    }

    // ------------------------------------------------------------------------------------- exact, on the rigs --

    /// <summary>One-pool model, 1 000 RP a turn, node a (2 500 RP) ordered on turn 0: the rows of turns 1, 2 and 3 are
    /// 1 000 / 1 000 / 500 received, progress 1 000 / 2 000 / 2 500, cost 2 500; the third is the COMPLETION row, on the turn
    /// the node completes (ResearchCompleted gains it in exactly that step); turn 4 has no row (no target: the RP reaches no
    /// node). Exact integers.</summary>
    [Fact]
    public void TheRowsAreThePointsEachTurn_AndTheCompletionRowIsOnTheTurnTheNodeCompletes()
    {
        ResearchContent content = Standard().Load();
        var log = new OrderLog();
        log.Append(Target(0, 1));
        TurnExecutor ex = Executor(content, log);
        WorldState w = PlayerWorld();
        var rowsByTurn = new List<ResearchStep[]>();
        var doneByTurn = new List<bool>();
        for (int t = 0; t < 4; t++)
        {
            WorldState prev = w;
            w = ex.Step(prev);
            rowsByTurn.Add(Rows(prev, w, content));
            doneByTurn.Add(Done(w, 1));
        }
        double[] received = [1000.0, 1000.0, 500.0];
        double[] progress = [1000.0, 2000.0, 2500.0];
        for (int t = 0; t < 3; t++)
        {
            ResearchStep r = Assert.Single(rowsByTurn[t]);
            Assert.Equal((Player, 1, "Technology"), (r.Holder, r.Node, r.Tree));
            Assert.Equal(received[t], r.Received);
            Assert.Equal(0.0, r.Credit);
            Assert.Equal(progress[t], r.Progress);
            Assert.Equal(2500.0, r.Cost);
            Assert.Equal(0, r.Level);
            Assert.Equal(t == 2, r.Completed);
            Assert.Equal(t == 2, doneByTurn[t] && (t == 0 || !doneByTurn[t - 1]));   // completed in exactly this step
        }
        Assert.Empty(rowsByTurn[3]);
    }

    /// <summary>Two-lane model (ADR-043), 1 000 CP a turn banked, civic law (2 500) the Civics target: the node rows carry the
    /// Civics lane's points and the BANK rows carry the milli-CP generated, drawn and left — 1 000 000 / 1 000 000 / 0 twice,
    /// then 1 000 000 / 500 000 / 500 000 on the completion turn — exactly the CivicsPoints account's differences.</summary>
    [Fact]
    public void TheCivicsBankRows_AreTheGeneratedDrawnAndLeftMilliCp_OfEachTurn()
    {
        Spec s = Standard();
        s.CivicsPoints = (true, 1.0, 0.5);
        ResearchContent content = s.Load();
        var log = new OrderLog();
        log.Append(Target(0, 1001));
        TurnExecutor ex = Executor(content, log);
        WorldState w = WithCompleted(PlayerWorld(), 1);   // law requires a
        long[] drawn = [1_000_000, 1_000_000, 500_000];
        long[] left = [0, 0, 500_000];
        double[] lawReceived = [1000.0, 1000.0, 500.0];
        for (int t = 0; t < 3; t++)
        {
            WorldState prev = w;
            w = ex.Step(prev);
            (ResearchStep[] rows, CivicsBankStep[] bank) = ResearchObservation.Observe(prev, w, content);
            ResearchStep law = Row(rows, Player, 1001) ?? throw new InvalidOperationException("no row for law on turn " + (t + 1));
            Assert.Equal("Civics", law.Tree);
            Assert.Equal(lawReceived[t], law.Received);
            Assert.Equal(t == 2, law.Completed);
            CivicsBankStep b = Assert.Single(bank);
            Assert.Equal(Player, b.Holder);
            Assert.Equal(1_000_000L, b.GeneratedMilli);
            Assert.Equal(drawn[t], b.DrawnMilli);
            Assert.Equal(left[t], b.BalanceMilli);
            CivicsPointsRow account = w.CivicsPoints[0];
            Assert.Equal(account.BalanceMilli, b.BalanceMilli);
        }
    }

    /// <summary>TURN-EXACT DELIVERY (the T1.9 rule): a target order stamped turn t is delivered to the step FROM turn t, so
    /// the node's first row from the lane's points is on turn t + 1 — never on turn t.</summary>
    [Fact]
    public void AResearchOrderStampedTurnT_GivesItsFirstRowOnTurnTPlusOne()
    {
        ResearchContent content = Standard().Load();
        var log = new OrderLog();
        log.Append(Target(2, 2));   // b, ordered on turn 2
        TurnExecutor ex = Executor(content, log);
        WorldState w = PlayerWorld();
        var turns = new List<(long Turn, ResearchStep[] Rows)>();
        for (int t = 0; t < 4; t++)
        {
            WorldState prev = w;
            w = ex.Step(prev);
            turns.Add((w.Clock.Turn, Rows(prev, w, content)));
        }
        foreach ((long turn, ResearchStep[] rows) in turns)
        {
            ResearchStep? b = Row(rows, Player, 2);
            if (turn <= 2) Assert.Null(b);
            else Assert.Equal(1000.0, b!.Value.Received - b.Value.Credit);
        }
        Assert.Equal(3L, turns[2].Turn);
    }

    /// <summary>ACCELERATION CREDIT lands whatever the target is: node w's first Eureka ("stock_timber &gt; 0") fires the
    /// first step w is available, crediting 0.20 × 2 500 = 500; w's row says received 500, all of it credit, while the RP goes
    /// to the target's row.</summary>
    [Fact]
    public void ACreditOnANodeThatIsNotTheTarget_IsItsOwnRow_AllCredit()
    {
        ResearchContent content = Standard().Load();
        var log = new OrderLog();
        log.Append(Target(0, 2));   // b is the target
        TurnExecutor ex = Executor(content, log);
        WorldState w = WithCompleted(PlayerWorld([(4, 50)]), 1);   // a complete: w is available; timber (good 4) in stock
        WorldState prev = w;
        w = ex.Step(prev);
        ResearchStep[] rows = Rows(prev, w, content);
        ResearchStep credit = Row(rows, Player, 11) ?? throw new InvalidOperationException("no row for w");
        Assert.Equal(500.0, credit.Received);
        Assert.Equal(500.0, credit.Credit);
        Assert.Equal(500.0, credit.Progress);
        Assert.False(credit.Completed);
        ResearchStep target = Row(rows, Player, 2) ?? throw new InvalidOperationException("no row for b");
        Assert.Equal(1000.0, target.Received);
        Assert.Equal(0.0, target.Credit);
    }

    /// <summary>EVERY HOLDER, IN A FIXED ORDER: the roster polities by id, then the city-states (a settlement no Empire
    /// controls, researching under its local key −1 − s) by settlement id, each holder's nodes by key — an integer
    /// composite order, so the rows of a step are the same in any run.</summary>
    [Fact]
    public void TheRowsAreTheRosterPolitiesThenTheCityStates_EachByNodeKey()
    {
        Spec s = Standard();
        s.CityStatePace = 0.25;
        ResearchContent content = s.Load();
        var log = new OrderLog();
        log.Append(Target(0, 2, polity: 2));
        log.Append(Target(0, 1, polity: 1));
        TurnExecutor ex = Executor(content, log);
        WorldState w = World([2, 1], [2, 1], 10_000);   // roster order 2, 1: the rows still come out 1, 2
        AddUncontrolled(w, 2, 10_000);
        AddUncontrolled(w, 3, 10_000);
        WorldState prev = w;
        w = ex.Step(prev);
        ResearchStep[] rows = Rows(prev, w, content);
        var holders = new List<int>();
        foreach (ResearchStep r in rows) holders.Add(r.Holder);
        Assert.Equal([1, 2, -3, -4], holders.Distinct().ToArray());   // ST2 = −3, ST3 = −4
        for (int i = 1; i < rows.Length; i++)
            if (rows[i].Holder == rows[i - 1].Holder) Assert.True(rows[i].Node > rows[i - 1].Node);
        foreach (ResearchStep r in rows) Assert.True(r.Received > 0.0, "a row is a node that received points or completed");
    }

    private static void AddUncontrolled(WorldState w, int settlement, long adults)
    {
        var id = new SettlementId(settlement);
        w.Settlements.Add(new SettlementRow(id, SiteCell: settlement, FoundedTurn: 0));
        int row = w.Buckets.Add(new BucketRow(id, new CultureId(1), new ReligionId(1), new ClassId(1),
            cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        new Ledger(w.LedgerFlows).Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, adults, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    // ------------------------------------------------------------ the production world, against the tables --

    /// <summary>
    /// THE ROWS ARE THE STATE'S RESEARCH, TURN BY TURN, EVERY HOLDER. On the production content (two lanes, destination
    /// routing, Eurekas, foreign exposure, city-states) with one AI empire and the player researching as the AI does, for 60
    /// turns: each step's rows are compared, exactly (bit for bit), with a linear-scan reading of the research tables
    /// before and after the step — the progress rows that moved, the completions and levels that appeared, each source's
    /// credit, the effective cost — and each bank row with the CivicsPoints account's differences. Non-vacuity: rows for the
    /// player (1) and the AI empire (2), completions of both, and bank rows of both. (A repeatable's later levels and
    /// exposure credit paid over several steps are pinned on rigs below, with the rig's own numbers.)
    /// </summary>
    [Fact]
    public void OnTheProductionWorld_TheRowsAreTheResearchTablesTurnByTurn_ThePlayerAndTheAiIncluded()
    {
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent content = cfg.Research!;
        WorldgenConfig wg = TestConfigs.Worldgen() with { AiEmpires = 1 };
        var log = new OrderLog();
        TurnExecutor exec = ObservedWorlds.Executor(cfg, log);
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        var seenHolders = new List<int>();
        var completedHolders = new List<int>();
        var bankHolders = new List<int>();
        int rows = 0;
        for (int t = 0; t < 60; t++)
        {
            foreach (OrderRecord o in PlayerTargets(w, content)) log.Append(o);   // the player researches: the cheapest node per lane
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            WorldState prev = w;
            w = exec.Step(prev);
            (ResearchStep[] steps, CivicsBankStep[] bank) = ResearchObservation.Observe(prev, w, content);
            List<ResearchStep> expected = Expected(prev, w, content);
            Assert.Equal(expected.Count, steps.Length);
            foreach (ResearchStep e in expected)
            {
                ResearchStep a = Row(steps, e.Holder, e.Node) ?? throw new InvalidOperationException($"turn {w.Clock.Turn}: no row for {e.Holder}/{e.Node}");
                Assert.True(Same(e, a), $"turn {w.Clock.Turn}: {a} is not {e}");
                if (!seenHolders.Contains(a.Holder)) seenHolders.Add(a.Holder);
                if (a.Completed && !completedHolders.Contains(a.Holder)) completedHolders.Add(a.Holder);
            }
            rows += steps.Length;
            Assert.Equal(ExpectedBank(prev, w), bank);
            foreach (CivicsBankStep b in bank) if (!bankHolders.Contains(b.Holder)) bankHolders.Add(b.Holder);
        }
        Assert.True(rows > 100, "rows observed: " + rows);
        foreach (int holder in new[] { 1, 2 })
        {
            Assert.Contains(holder, seenHolders);
            Assert.Contains(holder, completedHolders);
            Assert.Contains(holder, bankHolders);
        }
    }

    /// <summary>The player's research orders of a turn: in each lane without a live target, the cheapest available node of
    /// that tree, by the composite key (effective cost, node key) — a stable integer tie-break.</summary>
    private static List<OrderRecord> PlayerTargets(IReadOnlyWorldState w, ResearchContent content)
    {
        var orders = new List<OrderRecord>();
        var player = new PolityId(Player);
        bool[] available = ResearchQuery.AvailableMask(w, content, player);
        foreach (ResearchTree tree in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            if (ResearchQuery.TryGetTarget(w, content, player, tree, out _)) continue;
            int best = -1;
            double bestCost = 0.0;
            for (int i = 0; i < available.Length; i++)
            {
                if (!available[i] || content.Nodes[i].Tree != tree) continue;
                double cost = ResearchQuery.EffectiveCost(w, content, player, i);
                if (best < 0 || cost < bestCost || (cost == bestCost && content.Nodes[i].Key.Value < content.Nodes[best].Key.Value))
                {
                    best = i;
                    bestCost = cost;
                }
            }
            if (best >= 0 && ResearchQuery.TargetOrder(w, content, player, content.Nodes[best].Key) is { } order) orders.Add(order);
        }
        return orders;
    }

    private static bool Same(ResearchStep e, ResearchStep a) =>
        e.Holder == a.Holder && e.Node == a.Node && e.Tree == a.Tree && Bits(e.Received) == Bits(a.Received)
        && Bits(e.Credit) == Bits(a.Credit) && Bits(e.Progress) == Bits(a.Progress) && Bits(e.Cost) == Bits(a.Cost)
        && e.Level == a.Level && e.Completed == a.Completed;

    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    /// <summary>The linear-scan reading: linear scans of the tables (the observer uses sorted keys and binary search). It
    /// shares the observer's DEFINITIONS — max(before, cost) on a completion, the sum of the sources' credit deltas, the cost
    /// on PREV — so it checks the candidate set and the lookups, not the definitions; the rig tests pin those with the
    /// rig's own numbers (verification round 1, RR-R1-01).</summary>
    private static List<ResearchStep> Expected(IReadOnlyWorldState prev, IReadOnlyWorldState next, ResearchContent content)
    {
        var pairs = new List<(int Holder, int Node)>();
        for (int i = 0; i < next.ResearchProgress.Count; i++)
        {
            ResearchProgressRow r = next.ResearchProgress[i];
            double? before = ProgressOf(prev, r.Polity.Value, r.Node.Value);
            if (before is null || Bits(before.Value) != Bits(r.Progress)) pairs.Add((r.Polity.Value, r.Node.Value));
        }
        for (int i = 0; i < next.ResearchCompleted.Count; i++)
        {
            ResearchCompletedRow r = next.ResearchCompleted[i];
            if (!IsDone(prev, r.Polity.Value, r.Node.Value) && !pairs.Contains((r.Polity.Value, r.Node.Value))) pairs.Add((r.Polity.Value, r.Node.Value));
        }
        for (int i = 0; i < next.ResearchLevels.Count; i++)
        {
            ResearchLevelRow r = next.ResearchLevels[i];
            if (r.Level > LevelOf(prev, r.Polity.Value, r.Node.Value) && !pairs.Contains((r.Polity.Value, r.Node.Value)))
                pairs.Add((r.Polity.Value, r.Node.Value));
        }
        var result = new List<ResearchStep>();
        foreach ((int holder, int node) in pairs)
        {
            int index = content.IndexOf(new ResearchNodeId(node));
            bool completed = (IsDone(next, holder, node) && !IsDone(prev, holder, node)) || LevelOf(next, holder, node) > LevelOf(prev, holder, node);
            double cost = ResearchQuery.EffectiveCost(prev, content, new PolityId(holder), index);
            double before = ProgressOf(prev, holder, node) ?? 0.0;
            double after = completed ? (before >= cost ? before : cost) : ProgressOf(next, holder, node) ?? 0.0;
            double credit = 0.0;
            for (int source = 1; source <= 2; source++)
            {
                double now = CreditOf(next, holder, node, source), then = CreditOf(prev, holder, node, source);
                if (Bits(now) != Bits(then)) credit += now - then;
            }
            result.Add(new ResearchStep(holder, node, content.Nodes[index].Tree == ResearchTree.Civics ? "Civics" : "Technology",
                after - before, credit, after, cost, LevelOf(next, holder, node), completed));
        }
        return result;
    }

    private static double? ProgressOf(IReadOnlyWorldState w, int holder, int node)
    {
        for (int i = 0; i < w.ResearchProgress.Count; i++)
            if (w.ResearchProgress[i].Polity.Value == holder && w.ResearchProgress[i].Node.Value == node) return w.ResearchProgress[i].Progress;
        return null;
    }

    private static bool IsDone(IReadOnlyWorldState w, int holder, int node)
    {
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (w.ResearchCompleted[i].Polity.Value == holder && w.ResearchCompleted[i].Node.Value == node) return true;
        return false;
    }

    private static int LevelOf(IReadOnlyWorldState w, int holder, int node)
    {
        for (int i = 0; i < w.ResearchLevels.Count; i++)
            if (w.ResearchLevels[i].Polity.Value == holder && w.ResearchLevels[i].Node.Value == node) return w.ResearchLevels[i].Level;
        return 0;
    }

    private static double CreditOf(IReadOnlyWorldState w, int holder, int node, int source)
    {
        for (int i = 0; i < w.ResearchCredits.Count; i++)
        {
            ResearchCreditRow r = w.ResearchCredits[i];
            if (r.Polity.Value == holder && r.Node.Value == node && r.Source == source) return r.Amount;
        }
        return 0.0;
    }

    private static CivicsBankStep[] ExpectedBank(IReadOnlyWorldState prev, IReadOnlyWorldState next)
    {
        var rows = new List<CivicsBankStep>();
        for (int i = 0; i < next.CivicsPoints.Count; i++)
        {
            CivicsPointsRow now = next.CivicsPoints[i];
            CivicsPointsRow then = new(now.Polity, 0, 0, 0);
            for (int j = 0; j < prev.CivicsPoints.Count; j++) if (prev.CivicsPoints[j].Polity == now.Polity) then = prev.CivicsPoints[j];
            if (now.GeneratedMilli == then.GeneratedMilli && now.SpentMilli == then.SpentMilli && now.BalanceMilli == then.BalanceMilli) continue;
            rows.Add(new CivicsBankStep(now.Polity.Value, now.GeneratedMilli - then.GeneratedMilli, now.SpentMilli - then.SpentMilli, now.BalanceMilli));
        }
        // the record's order: the roster polities by id, then the city-states by settlement id (a stable sort on that key)
        return [.. rows.OrderBy(r => r.Holder >= 0 ? 0 : 1).ThenBy(r => r.Holder >= 0 ? r.Holder : -1 - r.Holder)];
    }

    // ------------------------------------------------------------------- the writer, the reader, the tag --

    /// <summary>A POPULATED round trip (the T1.1/T1.3 rule for a new serialized row): a turn record carrying research and
    /// bank rows — a roster polity and a city-state, fractional, large and repeating-decimal values, a completion with a
    /// level — is written by the writer as the exact JSON pinned here, read back by the reader bit for bit, and written again
    /// to the same bytes (sha256 equal). A telemetry/v5 line reads as research NOT RECORDED (Recorded false), never as
    /// "nothing researched".</summary>
    [Fact]
    public void ThePopulatedResearchRows_RoundTripThroughTheWriterAndTheReader_BitForBit()
    {
        ResearchStep[] steps =
        [
            new(1, 196, "Technology", 17.383314761717482, 0.0, 34.69492761826018, 102.92412, 0, false),
            new(1, 1069, "Civics", 182.73579999999998 - 160.0, 2.5, 182.73579999999998, 182.73579999999998, 3, true),
            new(-13, 9, "Technology", 1e-05, 1e-05, 1e-05, 1234567.890625, 0, false),
        ];
        CivicsBankStep[] bank = [new(1, 17_383, 17_383, 0), new(-13, 4_346, 0, 9_000_000_000_001)];
        TurnObservation observation = ObservedWorlds.Observed(ObservedWorlds.Founded(), null, 1).Log.Observations[0];
        TurnObservation populated = observation with { Turn = observation.Turn with { Research = steps, CivicsBank = bank } };

        byte[] first = Line(populated);
        string text = Encoding.UTF8.GetString(first);
        Assert.Contains("\"schema\":\"telemetry/v6\"", text, StringComparison.Ordinal);
        Assert.Contains("\"research\":[{\"holder\":1,\"node\":196,\"tree\":\"Technology\",\"received\":17.383314761717482,\"credit\":0,"
            + "\"progress\":34.69492761826018,\"cost\":102.92412,\"level\":0,\"completed\":false},", text, StringComparison.Ordinal);
        Assert.Contains("{\"holder\":-13,\"node\":9,\"tree\":\"Technology\",\"received\":1E-05,\"credit\":1E-05,\"progress\":1E-05,"
            + "\"cost\":1234567.890625,\"level\":0,\"completed\":false}],\"civicsBank\":[{\"holder\":1,\"generatedMilli\":17383,"
            + "\"drawnMilli\":17383,\"balanceMilli\":0},{\"holder\":-13,\"generatedMilli\":4346,\"drawnMilli\":0,"
            + "\"balanceMilli\":9000000000001}]}", text, StringComparison.Ordinal);

        TelemetryTurn read = TelemetryRecordFile.Parse([text.TrimEnd('\n')], "populated").Turns[0];
        Assert.NotNull(read.Research);
        Assert.True(read.Research!.Recorded);
        Assert.Equal(steps.Length, read.Research.Steps.Length);
        for (int i = 0; i < steps.Length; i++) Assert.True(Same(steps[i], read.Research.Steps[i]), read.Research.Steps[i].ToString());
        Assert.Equal(bank, read.Research.Bank);

        TurnObservation again = observation with { Turn = observation.Turn with { Research = read.Research.Steps, CivicsBank = read.Research.Bank } };
        Assert.Equal(ForensicIdentity.Sha256Hex(first), ForensicIdentity.Sha256Hex(Line(again)));

        string v5 = text.TrimEnd('\n').Replace("\"telemetry/v6\"", "\"telemetry/v5\"", StringComparison.Ordinal);
        int at = v5.IndexOf(",\"research\":", StringComparison.Ordinal);
        int end = v5.IndexOf("]}", v5.IndexOf("\"civicsBank\":", StringComparison.Ordinal), StringComparison.Ordinal) + 1;
        v5 = v5[..at] + v5[end..];
        TelemetryTurn old = TelemetryRecordFile.Parse([v5], "v5").Turns[0];
        Assert.False(old.Research!.Recorded);
        Assert.Empty(old.Research.Steps);
    }

    private static byte[] Line(TurnObservation o)
    {
        using var ms = new MemoryStream();
        TelemetryWriter.WriteTurn(ms, o);
        return ms.ToArray();
    }

    /// <summary>The tag moved with the field set (T4.20): the writer writes telemetry/v6 and the reader accepts v6 and every
    /// earlier vintage it accepted before.</summary>
    [Fact]
    public void TheWriterTagIsV6_AndTheReaderStillReadsV2ToV5()
    {
        Assert.Equal("telemetry/v6", TelemetryWriter.Schema);
        Assert.Equal(["telemetry/v6", "telemetry/v5", "telemetry/v4", "telemetry/v3", "telemetry/v2"], TelemetryWriter.ReadableSchemas);
        foreach (string tag in TelemetryWriter.ReadableSchemas) Assert.True(TelemetryRecordFile.IsReadable(tag));
        Assert.False(TelemetryRecordFile.IsReadable("telemetry/v7"));
    }

    // ------------------------------- verification round 1 (RR-R1-01): levels, exposure credit, an empty step --

    /// <summary>
    /// EVERY LEVEL OF A REPEATABLE IS A COMPLETION ROW, ON THE TURN THAT LEVEL COMPLETES (verification round 1, RR-R1-01:
    /// the shipped content runs with research.json tuning.repeatableLevels on and 13 levelled nodes). ResearchLevelTests'
    /// rig: rep (key 12, requires eng, BaseCost 1 000, costGrowth 1.25), whose levels 1, 2 and 3 cost 1 000, 1 250 and
    /// 1 562.5 (exact binary fractions); 10 000 people make 1 000 RP a turn, and the target is set again every turn (a level
    /// clears it). Level 1 completes on turn 1; level 2 takes turns 2 and 3 (1 000, then the 250 it lacks); level 3 turns 4
    /// and 5 (1 000, then 562.5). So rep's completion rows are on turns 1, 3 and 5 and no other, each with the level it
    /// reached and the cost of THAT level, while the world appends ONE completed row (the first level's): a later level
    /// only raises the level and removes the progress row. The numbers are the rig's, not the observer's formulas: a
    /// level's last row received what it lacked, because a credit stops at the cost (the rest of the turn's RP reaches no
    /// node: the rig has no overflow).
    /// </summary>
    [Fact]
    public void EveryLevelOfARepeatable_IsACompletionRow_OnTheTurnThatLevelCompletes_WithThatLevelsCost()
    {
        const int rep = 12;
        Spec s = Standard();
        s.RepeatableLevels = true;
        s.Technologies.Add(new Node(rep, "rep", "eng", Cost: 1000.0, Branch: "engineering", Age: "A9",
            Levels: new LevelRule(1.25, [("farming.yieldPerArableKm2PerYear", 0.5)])));
        s.Recursive.Add("rep");
        ResearchContent content = s.Load();
        var log = new OrderLog();
        for (int t = 0; t < 5; t++) log.Append(Target(t, rep));
        TurnExecutor ex = Executor(content, log);
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3, 8);   // a, b, c and eng complete: rep is open
        double[] received = [1000.0, 1000.0, 250.0, 1000.0, 562.5];
        double[] progress = [1000.0, 1000.0, 1250.0, 1000.0, 1562.5];
        double[] cost = [1000.0, 1250.0, 1250.0, 1562.5, 1562.5];
        int[] level = [1, 1, 2, 2, 3];
        bool[] completes = [true, false, true, false, true];
        for (int t = 0; t < 5; t++)
        {
            WorldState prev = w;
            w = ex.Step(prev);
            Assert.Equal(level[t], ResearchQuery.Level(w, new PolityId(Player), new ResearchNodeId(rep)));   // the world's level
            ResearchStep r = Row(Rows(prev, w, content), Player, rep)
                ?? throw new InvalidOperationException("no row for rep on turn " + (t + 1).ToString(CultureInfo.InvariantCulture));
            Assert.Equal("Technology", r.Tree);
            Assert.Equal(received[t], r.Received);
            Assert.Equal(0.0, r.Credit);
            Assert.Equal(progress[t], r.Progress);
            Assert.Equal(cost[t], r.Cost);
            Assert.Equal(level[t], r.Level);
            Assert.Equal(completes[t], r.Completed);
        }
        int completedRows = 0;
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (w.ResearchCompleted[i].Polity.Value == Player && w.ResearchCompleted[i].Node.Value == rep) completedRows++;
        Assert.Equal(1, completedRows);   // three completion rows on the record, one completed row in the world
    }

    /// <summary>
    /// FOREIGN-EXPOSURE CREDIT PAID OVER SEVERAL STEPS: EACH ROW'S CREDIT IS THAT STEP'S CREDIT (verification round 1,
    /// RR-R1-01: the shipped content runs with tuning.foreignExposure enabled, which credits a node every step while its
    /// holder trades with a polity that knows it). What a node is offered is cumulative (ResearchExposures, which
    /// DiffusionSystem writes; set here as it would be): 100 before turn 1, then 300. Node x (key 12, BaseCost 2 500) is also
    /// the RP target (1 000 a turn), so its credit is part of what it received:
    /// turn 1 — credit 100, received 1 100 (progress 1 100);
    /// turn 2 — credit 200 (300 offered less the 100 already paid; not the 300 paid so far), received 1 200 (2 300);
    /// turn 3 — nothing new offered: credit 0, and the 200 RP it lacked: the completion row (received 200, progress 2 500).
    /// The world's own provenance row for the source holds the whole 300.
    /// </summary>
    [Fact]
    public void ForeignExposureCredit_PaidOverSeveralSteps_IsEachStepsCredit_WithinWhatTheNodeReceived()
    {
        const int x = 12;
        Spec s = Standard();
        s.Technologies.Add(new Node(x, "x", "a", Cost: 2500.0));
        ResearchContent content = s.Load();
        var log = new OrderLog();
        log.Append(Target(0, x));
        TurnExecutor ex = Executor(content, log);
        WorldState w = WithCompleted(PlayerWorld(), 1);   // a complete: x is open
        double[] offered = [100.0, 300.0, 300.0];
        double[] received = [1100.0, 1200.0, 200.0];
        double[] credit = [100.0, 200.0, 0.0];
        double[] progress = [1100.0, 2300.0, 2500.0];
        for (int t = 0; t < 3; t++)
        {
            WorldState prev = w.Clone();
            Offer(prev, x, offered[t]);
            w = ex.Step(prev);
            ResearchStep r = Row(Rows(prev, w, content), Player, x)
                ?? throw new InvalidOperationException("no row for x on turn " + (t + 1).ToString(CultureInfo.InvariantCulture));
            Assert.Equal(received[t], r.Received);
            Assert.Equal(credit[t], r.Credit);
            Assert.Equal(progress[t], r.Progress);
            Assert.Equal(2500.0, r.Cost);
            Assert.Equal(t == 2, r.Completed);
        }
        double paid = 0.0;
        for (int i = 0; i < w.ResearchCredits.Count; i++)
        {
            ResearchCreditRow c = w.ResearchCredits[i];
            if (c.Polity.Value == Player && c.Node.Value == x && c.Source == (int)AccelerationSource.ForeignExposure) paid += c.Amount;
        }
        Assert.Equal(300.0, paid);
    }

    /// <summary>Sets what <paramref name="node"/> is offered to the player by foreign exposure (the DiffusionSystem seam).</summary>
    private static void Offer(WorldState w, int node, double offered)
    {
        var row = new ResearchExposureRow(new PolityId(Player), new ResearchNodeId(node), offered);
        for (int i = 0; i < w.ResearchExposures.Count; i++)
            if (w.ResearchExposures[i].Polity.Value == Player && w.ResearchExposures[i].Node.Value == node)
            {
                w.ResearchExposures[i] = row;
                return;
            }
        w.ResearchExposures.Add(row);
    }

    /// <summary>A telemetry/v6 line whose step researched nothing — empty research and civicsBank arrays, which the writer
    /// writes for such a step — reads as RECORDED with no rows ("nothing this step"), never as research not recorded: the
    /// array's presence decides, not its length (verification round 1, RR-R1-01). A line without the arrays (v5) is the
    /// not-recorded case, pinned in the populated round trip above.</summary>
    [Fact]
    public void AV6LineWhoseStepResearchedNothing_ReadsAsRecorded_WithNoRows()
    {
        TurnObservation observation = ObservedWorlds.Observed(ObservedWorlds.Founded(), null, 1).Log.Observations[0];
        TurnObservation empty = observation with { Turn = observation.Turn with { Research = [], CivicsBank = [] } };
        string text = Encoding.UTF8.GetString(Line(empty)).TrimEnd('\n');
        Assert.Contains("\"schema\":\"telemetry/v6\"", text, StringComparison.Ordinal);
        Assert.Contains("\"research\":[],\"civicsBank\":[]", text, StringComparison.Ordinal);
        TelemetryTurn read = TelemetryRecordFile.Parse([text], "empty step").Turns[0];
        Assert.NotNull(read.Research);
        Assert.True(read.Research!.Recorded);
        Assert.Empty(read.Research.Steps);
        Assert.Empty(read.Research.Bank);
    }

    // ------------------------------- verification round 2 (RR-R2-01, RR-R2-02): the bank order, a fallen cost --

    /// <summary>
    /// THE BANK ROWS ARE THE ROSTER POLITIES, THEN THE CITY-STATES — NOT THE ACCOUNTS TABLE'S ORDER (verification round 2,
    /// RR-R2-01). The two-lane model (CP 1.0 × P^0.5 a sim-year, a city-state at a quarter of that pace), the roster in
    /// the order 2, 1 and two city-states (settlements 2 and 3, local keys −3 and −4), 10 000 people each. The CivicsPoints
    /// table keeps its accounts in ascending holder key (ADR-043), so after the step it holds −4, −3, 1, 2: the city-states'
    /// accounts FIRST. The record's bank rows are the documented order — 1, 2, then ST2 (−3), ST3 (−4) — each its account's
    /// change exactly: 1 000 000 milli-CP generated by an Empire of 10 000 (100 CP a sim-year, a dt-10 turn), 250 000 by a
    /// city-state, nothing drawn (no civic is open: law requires a), all of it left.
    /// </summary>
    [Fact]
    public void TheBankRows_AreTheRosterPolitiesThenTheCityStates_NotTheAccountsTablesOrder()
    {
        Spec s = Standard();
        s.CityStatePace = 0.25;
        s.CivicsPoints = (true, 1.0, 0.5);
        ResearchContent content = s.Load();
        TurnExecutor ex = Executor(content);
        WorldState w = World([2, 1], [2, 1], 10_000);
        AddUncontrolled(w, 2, 10_000);
        AddUncontrolled(w, 3, 10_000);
        WorldState prev = w;
        w = ex.Step(prev);
        var accounts = new List<int>();
        for (int i = 0; i < w.CivicsPoints.Count; i++) accounts.Add(w.CivicsPoints[i].Polity.Value);
        Assert.Equal([-4, -3, 1, 2], accounts);   // the premise: the table's order is not the record's
        CivicsBankStep[] bank = ResearchObservation.Observe(prev, w, content).Bank;
        Assert.Equal(
        [
            new CivicsBankStep(1, 1_000_000, 0, 1_000_000), new CivicsBankStep(2, 1_000_000, 0, 1_000_000),
            new CivicsBankStep(-3, 250_000, 0, 250_000), new CivicsBankStep(-4, 250_000, 0, 250_000),
        ], bank);
    }

    /// <summary>
    /// A NODE WHOSE COST FELL BELOW ITS STORED PROGRESS COMPLETES WITH THE PROGRESS IT HAD, RECEIVING NOTHING (verification
    /// round 2, RR-R2-02). The effective cost applies the holder's university modifiers (ResearchCostModifiers, which
    /// InstitutionsSystem rebuilds every step from university maturity; set here as it would write it), so it can fall
    /// below what a node already holds; ResearchSystem then pays the node nothing (a credit stops at the cost) and completes
    /// it with what it had. Node eng (engineering, BaseCost 2 500; a, b and c complete, so the research stage is reached) is
    /// the RP target: turn 1 pays 1 000 (progress 1 000).
    /// Before turn 2 an engineering university's modifier 0.3 brings the cost to 750 (above the floor 500). Turn 2's row is
    /// the completion row, on that turn: progress 1 000 — the world's stored progress, not the 750 cost — received 0, never
    /// a negative amount, cost 750 (the cost on PREV, the one the system compared).
    /// </summary>
    [Fact]
    public void ANodeWhoseCostFellBelowItsStoredProgress_CompletesWithTheProgressItHad_ReceivingNothing()
    {
        const int eng = 8;
        ResearchContent content = Standard().Load();
        var log = new OrderLog();
        log.Append(Target(0, eng));
        TurnExecutor ex = Executor(content, log);
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3);   // a, b and c complete: the research stage (c) is reached, eng is open
        WorldState prev = w;
        w = ex.Step(prev);
        ResearchStep paid = Row(Rows(prev, w, content), Player, eng) ?? throw new InvalidOperationException("no row for eng on turn 1");
        Assert.Equal((Player, eng, 1000.0, 1000.0, 2500.0, false), (paid.Holder, paid.Node, paid.Received, paid.Progress, paid.Cost, paid.Completed));

        prev = w.Clone();
        prev.ResearchCostModifiers.Add(new ResearchCostModifierRow(new PolityId(Player), 3, 0.3));   // engineering_university
        Assert.Equal(750.0, ResearchQuery.EffectiveCost(prev, content, new PolityId(Player), content.IndexOf(Key(eng))));
        w = ex.Step(prev);
        Assert.True(Done(w, eng) && !Done(prev, eng), "eng completed in this step");
        Assert.Equal(1000.0, Progress(prev, eng));   // what the world stored when the cost fell below it
        ResearchStep r = Row(Rows(prev, w, content), Player, eng) ?? throw new InvalidOperationException("no row for eng on turn 2");
        Assert.Equal("Technology", r.Tree);
        Assert.True(r.Completed);
        Assert.Equal(1000.0, r.Progress);
        Assert.Equal(0.0, r.Received);
        Assert.Equal(0.0, r.Credit);
        Assert.Equal(750.0, r.Cost);
        Assert.Equal(0, r.Level);
    }
}
