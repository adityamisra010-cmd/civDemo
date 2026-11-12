using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Core.Observability;

/// <summary>
/// D-086 DE1 (the Director, 2026-11-11: "do the runs record the researches? If not, it should record which node was
/// researched and how much and which turn also") — THE RESEARCH ROWS OF ONE STEP, observed from the two worlds the step
/// joined: every node of every knowledge holder whose stored progress moved, every node that completed, and every Civics
/// Points bank that changed (readings DE1-R1..R3 (a): every holder; per node and turn the points received, the progress and
/// the cost, the banked Civics Points drawn; a completion row says it completed).
///
/// AN OBSERVATION OF ROWS THAT ALREADY EXIST (D-086 DE-R (a)). Everything is READ or DIFFERENCED from the research tables
/// ResearchSystem owns (ResearchProgress, ResearchCompleted, ResearchLevels, ResearchCredits, CivicsPoints), or RECOMPUTED
/// through the public static the system itself calls (<see cref="ResearchQuery.EffectiveCost"/>, on PREV, with the same
/// arguments). Nothing re-derives the funding rule (which node a lane funds, or how many points a holder makes): the points a
/// node received are the change in its stored progress, so the record cannot disagree with the world. No world, hash or
/// table is written; nothing in Sim.Core/Systems references this namespace.
///
/// DETERMINISTIC: tables are walked in index order; every lookup is a binary search over sorted integer keys (no Dictionary,
/// no hash), and the rows are ordered by an integer composite key — the roster polities first (ascending polity id), then the
/// city-states (ascending settlement id), each by node key — a total order, since each (holder, node) pair appears once.
/// </summary>
public static class ResearchObservation
{
    /// <summary>The research rows and Civics bank rows of the step <paramref name="prev"/> → <paramref name="next"/>; both
    /// empty when <paramref name="content"/> is null (a world without research content, where ResearchSystem is inert).</summary>
    public static (ResearchStep[] Steps, CivicsBankStep[] Bank) Observe(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, ResearchContent? content)
    {
        if (content is null) return ([], []);

        Keyed<double> progBefore = Progress(prev), progAfter = Progress(next);
        long[] doneBefore = Completed(prev), doneAfter = Completed(next);
        Keyed<int> levelBefore = Levels(prev), levelAfter = Levels(next);
        Keyed<double> eurekaBefore = Credits(prev, AccelerationSource.Eureka), eurekaAfter = Credits(next, AccelerationSource.Eureka);
        Keyed<double> exposureBefore = Credits(prev, AccelerationSource.ForeignExposure),
            exposureAfter = Credits(next, AccelerationSource.ForeignExposure);

        // The candidates: a progress row that moved (or appeared), a completion that appeared, a level that rose.
        var candidates = new List<long>();
        for (int i = 0; i < progAfter.Keys.Length; i++)
        {
            long key = progAfter.Keys[i];
            int at = Array.BinarySearch(progBefore.Keys, key);
            if (at < 0 || BitConverter.DoubleToInt64Bits(progBefore.Values[at]) != BitConverter.DoubleToInt64Bits(progAfter.Values[i]))
                candidates.Add(RowOrder(key));
        }
        for (int i = 0; i < doneAfter.Length; i++)
            if (Array.BinarySearch(doneBefore, doneAfter[i]) < 0) candidates.Add(RowOrder(doneAfter[i]));
        for (int i = 0; i < levelAfter.Keys.Length; i++)
            if (levelAfter.Values[i] > levelBefore.Get(levelAfter.Keys[i], 0)) candidates.Add(RowOrder(levelAfter.Keys[i]));

        long[] order = [.. candidates];
        Array.Sort(order);   // distinct composite integer keys after de-duplication below: a total order
        var steps = new List<ResearchStep>(order.Length);
        for (int i = 0; i < order.Length; i++)
        {
            if (i > 0 && order[i] == order[i - 1]) continue;   // a pair named by two candidates (moved and completed) is one row
            long key = KeyOfRowOrder(order[i]);
            int holder = (int)(key >> 32);
            int node = (int)(key & 0xFFFFFFFFL);
            int index = content.IndexOf(new ResearchNodeId(node));
            if (index < 0) continue;   // ResearchSystem writes only content keys (one content per session), so no row is skipped
            bool completed = (Array.BinarySearch(doneAfter, key) >= 0 && Array.BinarySearch(doneBefore, key) < 0)
                || levelAfter.Get(key, 0) > levelBefore.Get(key, 0);
            double cost = ResearchQuery.EffectiveCost(prev, content, new PolityId(holder), index);
            double before = progBefore.Get(key, 0.0);
            // On completion the row is gone: the system set the progress to EXACTLY the cost when a credit reached it
            // (ResearchSystem.Credit / SpendCivics), or completed a node already at its cost with what it had.
            double after = completed ? Math.Max(before, cost) : progAfter.Get(key, 0.0);
            double credit = Delta(eurekaAfter, eurekaBefore, key) + Delta(exposureAfter, exposureBefore, key);
            steps.Add(new ResearchStep(holder, node, content.Nodes[index].Tree == ResearchTree.Civics ? "Civics" : "Technology",
                after - before, credit, after, cost, levelAfter.Get(key, 0), completed));
        }

        // The Civics Points banks that changed, in the same holder order.
        var bankOrder = new List<long>();
        var bankRows = new List<CivicsBankStep>();
        for (int r = 0; r < next.CivicsPoints.Count; r++)
        {
            CivicsPointsRow now = next.CivicsPoints[r];
            CivicsPointsRow then = new(now.Polity, 0L, 0L, 0L);
            for (int p = 0; p < prev.CivicsPoints.Count; p++)
                if (prev.CivicsPoints[p].Polity.Value == now.Polity.Value) { then = prev.CivicsPoints[p]; break; }
            long generated = now.GeneratedMilli - then.GeneratedMilli;
            long drawn = now.SpentMilli - then.SpentMilli;
            if (generated == 0L && drawn == 0L && now.BalanceMilli == then.BalanceMilli) continue;
            bankOrder.Add(RowOrder(Key(now.Polity.Value, 0)));
            bankRows.Add(new CivicsBankStep(now.Polity.Value, generated, drawn, now.BalanceMilli));
        }
        long[] bankKeys = [.. bankOrder];
        CivicsBankStep[] bank = [.. bankRows];
        Array.Sort(bankKeys, bank);   // at most one account per holder (ADR-043): distinct keys
        return (steps.ToArray(), bank);
    }

    /// <summary>The (holder, node) key: the holder in the high 32 bits, the node in the low 32.</summary>
    internal static long Key(int holder, int node) => ((long)holder << 32) | (uint)node;

    /// <summary>The row-order key of a (holder, node) key: a roster polity (holder ≥ 0) before a city-state; a roster polity
    /// by its id, a city-state by its settlement id (−1 − holder); then the node. Every part is a non-negative 31-bit
    /// integer, so the composite is a total order on distinct pairs and maps back exactly (<see cref="KeyOfRowOrder"/>).</summary>
    internal static long RowOrder(long key)
    {
        int holder = (int)(key >> 32);
        int node = (int)(key & 0xFFFFFFFFL);
        long group = holder >= 0 ? 0L : 1L;
        long ordinal = holder >= 0 ? holder : -1L - holder;
        return (group << 62) | (ordinal << 31) | (uint)node;
    }

    internal static long KeyOfRowOrder(long order)
    {
        bool local = (order >> 62) != 0;
        long ordinal = (order >> 31) & 0x7FFFFFFFL;
        int node = (int)(order & 0x7FFFFFFFL);
        return Key(local ? (int)(-1L - ordinal) : (int)ordinal, node);
    }

    /// <summary>A table's values by sorted (holder, node) key — each pair at most once (the tables' invariants).</summary>
    private readonly record struct Keyed<T>(long[] Keys, T[] Values)
    {
        public T Get(long key, T absent)
        {
            int at = Array.BinarySearch(Keys, key);
            return at >= 0 ? Values[at] : absent;
        }
    }

    private static Keyed<double> Progress(IReadOnlyWorldState w)
    {
        var keys = new long[w.ResearchProgress.Count];
        var values = new double[keys.Length];
        for (int r = 0; r < keys.Length; r++)
        {
            ResearchProgressRow row = w.ResearchProgress[r];
            keys[r] = Key(row.Polity.Value, row.Node.Value);
            values[r] = row.Progress;
        }
        Array.Sort(keys, values);
        return new Keyed<double>(keys, values);
    }

    private static long[] Completed(IReadOnlyWorldState w)
    {
        var keys = new long[w.ResearchCompleted.Count];
        for (int r = 0; r < keys.Length; r++) keys[r] = Key(w.ResearchCompleted[r].Polity.Value, w.ResearchCompleted[r].Node.Value);
        Array.Sort(keys);
        return keys;
    }

    private static Keyed<int> Levels(IReadOnlyWorldState w)
    {
        var keys = new long[w.ResearchLevels.Count];
        var values = new int[keys.Length];
        for (int r = 0; r < keys.Length; r++)
        {
            keys[r] = Key(w.ResearchLevels[r].Polity.Value, w.ResearchLevels[r].Node.Value);
            values[r] = w.ResearchLevels[r].Level;
        }
        Array.Sort(keys, values);
        return new Keyed<int>(keys, values);
    }

    /// <summary>One acceleration source's cumulative credit per (holder, node) — at most one row per (holder, node, source).</summary>
    private static Keyed<double> Credits(IReadOnlyWorldState w, AccelerationSource source)
    {
        var keys = new List<long>();
        var values = new List<double>();
        for (int r = 0; r < w.ResearchCredits.Count; r++)
        {
            ResearchCreditRow row = w.ResearchCredits[r];
            if (row.Source != (int)source) continue;
            keys.Add(Key(row.Polity.Value, row.Node.Value));
            values.Add(row.Amount);
        }
        long[] k = [.. keys];
        double[] v = [.. values];
        Array.Sort(k, v);
        return new Keyed<double>(k, v);
    }

    /// <summary>A source's credit to the pair in this step: after − before (0 before a source's first row).</summary>
    private static double Delta(Keyed<double> after, Keyed<double> before, long key)
    {
        double now = after.Get(key, 0.0), then = before.Get(key, 0.0);
        return BitConverter.DoubleToInt64Bits(now) == BitConverter.DoubleToInt64Bits(then) ? 0.0 : now - then;
    }
}
