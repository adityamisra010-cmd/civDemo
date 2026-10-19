using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.Systems;

/// <summary>
/// D-054 WS1 — THE BUILDING ORDER'S COMPOSITE KEY IS TIE-DENSE-PROVEN (batch-3 integration, 2026-10-14; verifier 2 F2).
/// ConstructionQuery.HeadsInBuildingOrder orders the queue heads by (EnqueuedTurn ascending, −1 for an entry with no
/// progress row; SettlementId ascending; Slot ascending) — all integers, one head per settlement, so the order is total.
/// CLAUDE.md requires a tie-dense test for every composite key: MEASURED before these tests, a mutant that drops the
/// SettlementId tie-break survived every suite (the focused WS1 filter, FullyQualifiedName~Construction, the UI filter),
/// because List.Sort is an insertion sort — stable — up to 16 elements and every rig held at most four heads in id
/// order. With 17 or more heads tied on EnqueuedTurn the sort is an introsort and the tie-break is all that orders them.
/// Both reachable shapes are pinned: many settlements ordering on one turn, and migrated v32 entries (no progress row,
/// every head reads −1). Each head's slot runs AGAINST its id (settlement s heads at slot n − 1 − s), so a key that
/// falls through to Slot comes out in descending id, never by accident in the right order; the queue rows are inserted
/// out of id order. The ConstructionSystem rig makes the order observable as the simulation acts on it: tied heads
/// compete for one scarce good, and which settlements build is the draw split the order dictates.
/// </summary>
public class BuildingOrderTieDenseTests
{
    private const int Heads = 24;

    /// <summary>A bare world of <paramref name="n"/> settlements (ids 0..n−1, table in id order — the order every
    /// founding and colonisation appends in), each with its head at slot n − 1 − s and one later entry, the rows
    /// inserted in the order s = (7k) mod n. <paramref name="turn"/> ≥ 0 gives every entry a progress row (the head's
    /// EnqueuedTurn = turn, the later entry's turn + 1: slots are taken in order, so a later slot is never older); −1
    /// gives none (migrated v32 entries).</summary>
    private static WorldState TiedQueue(int n, Func<int, long> turnOf)
    {
        var w = new WorldState(1);
        for (int s = 0; s < n; s++) w.Settlements.Add(new SettlementRow(new SettlementId(s), s, 0));
        for (int k = 0; k < n; k++)
        {
            int s = (k * 7) % n;
            int head = n - 1 - s;
            long turn = turnOf(s);
            // The later entry first in the table, so the head is found by its slot, not by its table position.
            w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(s), head + 1, Granary));
            w.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(s), head, Granary));
            if (turn < 0) continue;
            w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(s), head + 1, Granary, turn + 1, 0));
            w.ConstructionProgress.Add(new ConstructionProgressRow(new SettlementId(s), head, Granary, turn, 1_000L * s));
        }
        Assert.Null(ConstructionQuery.ProgressInvariantViolation(w.ConstructionQueue, w.ConstructionProgress));
        return w;
    }

    [Theory]
    [InlineData(5L)]    // every settlement ordered on the same turn
    [InlineData(-1L)]   // every head a migrated v32 entry: no progress row, the key reads −1
    public void TwentyFourHeadsTiedOnEnqueuedTurn_InsertedOutOfIdOrder_ComeOutInAscendingSettlementId(long turn)
    {
        WorldState w = TiedQueue(Heads, _ => turn);
        Assert.True(Heads > 16, "the tie must be denser than List.Sort's insertion-sort threshold");
        for (int s = 0; s < Heads; s++)
            Assert.Equal(turn, ConstructionQuery.EnqueuedTurnOf(w.ConstructionProgress,
                new ConstructionQueueRow(new SettlementId(s), Heads - 1 - s, Granary)));   // a full tie on the first key

        ConstructionQueueRow[] order = ConstructionQuery.HeadsInBuildingOrder(w, w.ConstructionQueue, w.ConstructionProgress);

        Assert.Equal(Heads, order.Length);
        for (int i = 0; i < Heads; i++)
            Assert.Equal(new ConstructionQueueRow(new SettlementId(i), Heads - 1 - i, Granary), order[i]);
    }

    [Fact]
    public void ThreeTiedTurnGroups_IncludingMigratedEntries_TheTurnDecidesFirst_ThenTheId_WithinEachGroup()
    {
        // 40 heads in three tied groups: −1 (no progress row, migrated), turn 0 and turn 7; group by s·11 mod 3, so each
        // group's ids interleave with the others'. Expected: every −1 head (ascending id), then turn 0, then turn 7.
        const int n = 40;
        long[] turns = [-1, 0, 7];
        WorldState w = TiedQueue(n, s => turns[(s * 11) % 3]);

        ConstructionQueueRow[] order = ConstructionQuery.HeadsInBuildingOrder(w, w.ConstructionQueue, w.ConstructionProgress);

        var expected = new List<int>();
        foreach (long t in turns)
            for (int s = 0; s < n; s++)
                if (turns[(s * 11) % 3] == t) expected.Add(s);
        Assert.Equal(n, order.Length);
        Assert.Equal(expected, order.Select(o => o.Settlement.Value).ToArray());
        for (int i = 0; i < n; i++) Assert.Equal(n - 1 - order[i].Settlement.Value, order[i].Slot);   // each one its head
    }

    // ================================================================== the system acts on the order

    private const int Settlements = 20;
    private const int Completing = 9;                 // the stone pays for nine whole granaries ...
    private const long Left = 7;                      // ... and 7 stone of the tenth

    private static readonly Lazy<WorldState> Twenty = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42, settlementsOverride: Settlements));

    [Theory]
    [InlineData(3L)]
    [InlineData(-1L)]
    public void TwentyTiedHeads_CompetingForOneScarceGood_BuildInAscendingSettlementId_TheExactDrawSplit(long turn)
    {
        // The founded dev world at 20 settlements, all the player's (one Empire, one purse); every head a granary
        // (timber 40, stone 20) tied on EnqueuedTurn, builders everywhere and labour never binding; timber for all,
        // stone for 9 granaries and 7 over. Served in ascending id, settlements 0..8 complete, settlement 9 reaches
        // p = floor(7 × N / 20) = 350,000 and the other ten draw nothing. Any other order builds other settlements.
        WorldState w = Twenty.Value.Clone();
        Assert.Equal(Settlements, w.Settlements.Count);
        Assert.Equal(Settlements, LabourActivities.ControlledSettlements(w, Player).Length);
        for (int s = 0; s < Settlements; s++)
        {
            Assert.Equal(s, w.Settlements[s].Id.Value);
            Set(w, w.Settlements[s].Id, Stone, 0);
            Set(w, w.Settlements[s].Id, Timber, 0);
            AllBuilders(w, w.Settlements[s].Id);
        }
        for (int i = 0; i < w.Housing.Count; i++) w.Housing.Ref(i).LastLaborUsed = 0.0;
        double labour = Cfg.Goods!.ProjectById(Granary)!.LaborRequired;
        for (int s = 0; s < Settlements; s++)
            Assert.True(C(w, new SettlementId(s), 10.0) >= labour, $"settlement {s}: labour must not bind the rig");

        WorldState tied = TiedQueue(Settlements, _ => turn);
        foreach (ConstructionQueueRow row in Rows(tied.ConstructionQueue)) w.ConstructionQueue.Add(row);
        foreach (ConstructionProgressRow row in Rows(tied.ConstructionProgress)) w.ConstructionProgress.Add(row with { Progress = 0 });
        // Every head is at p = 0 here, so the only input to the order is the key.
        Set(w, new SettlementId(Settlements - 1), Timber, 40L * Settlements);   // the HIGHEST id holds the purse
        Set(w, new SettlementId(Settlements - 1), Stone, 20L * Completing + Left);

        // D-058 (2026-10-19) CAUSE BLOCK. The Director's ruling "build one at a time … 7 built, 1 under construction, 1 not
        // started" carries a completing head's leftover capacity to its queue's next entry IN THE SAME STEP, at that
        // entry's own place in the building order. With turn = 3 every later entry is keyed turn + 1 = 4, behind all twenty
        // heads, and the stone is gone by then: nothing below moves. With turn = −1 (migrated v32 entries, no progress
        // row) every entry of every queue keys −1, so settlement s's later entry (−1, s, slot + 1) outranks settlement
        // s + 1's head and is served before it: MEASURED (this tree, before this block) settlement 0 built 2, not 1. The
        // −1 arm's batch-4 pins are kept under TestConfigs.PreContinueWithinTurn (the head key's tie-break, this test's
        // subject); the shipped arm is checked against the oracle below.
        if (turn < 0) ChainedTieArm(w);
        WorldState next = ConstructionOnly(turn < 0 ? TestConfigs.PreContinueWithinTurn(Cfg) : Cfg, null).Step(w);

        for (int s = 0; s < Settlements; s++)
        {
            var id = new SettlementId(s);
            int head = Settlements - 1 - s;
            ConstructionQueueRow[] q = ConstructionQuery.Queue(next, id);
            if (s < Completing)
            {
                Assert.Equal(1L, Built(next, id));
                Assert.Equal(new ConstructionQueueRow(id, head + 1, Granary), Assert.Single(q));   // the later entry heads now
                Assert.Equal(0, ConstructionQuery.ProgressOf(next, q[0]));
            }
            else
            {
                Assert.Equal(0L, Built(next, id));
                Assert.Equal(2, q.Length);
                long p = ConstructionQuery.ProgressOf(next, q[0]);
                Assert.Equal(s == Completing ? 350_000L : 0L, p);
                // The partly built head keeps its order key: its row's EnqueuedTurn is the tied turn (−1 for a migrated
                // entry, whose first progress appends a row keeping −1).
                if (s == Completing) Assert.Equal(turn, ConstructionQuery.EnqueuedTurnOf(next.ConstructionProgress, q[0]));
            }
        }
        Assert.Equal(0L, TotalOf(next, Stone));
        Assert.Equal(20L * Completing + Left, Sunk(next, Stone) - Sunk(w, Stone));
        Assert.Equal(40L * Settlements - (40L * Completing + 14), TotalOf(next, Timber));   // ceil(40 × 0.35) = 14
        AssertConserved(next);
    }

    /// <summary>
    /// D-058, the shipped rule on the −1 arm (every entry keyed −1): an INDEPENDENT oracle of the walk on the one scarce good
    /// (stone; timber is ample and never limits a draw here). In the building order — settlement ascending, then slot —
    /// each settlement's head is served from the stone left; a head that completes passes C − L (its settlement's capacity
    /// less the granary's labour) to the later entry, which then advances min(floor((C − L) / L × N), the stone bound) and
    /// sets aside what it still needs of the stone left; a head that does not complete passes nothing. The step must build
    /// and part-build exactly what the oracle serves, settlement by settlement, and draw exactly the stone it serves.
    /// </summary>
    private static void ChainedTieArm(WorldState w)
    {
        double labour = Cfg.Goods!.ProjectById(Granary)!.LaborRequired;
        long stone = 20L * Completing + Left, drawn = 0;
        var built = new long[Settlements];
        var part = new long[Settlements];          // the progress of the entry the queue keeps as its head
        for (int s = 0; s < Settlements; s++)
        {
            long head = stone >= 20 ? N : stone * N / 20;
            long take = ConstructionQuery.Delivered(20, head);
            stone -= take;
            drawn += take;
            if (head < N)
            {
                part[s] = head;
                stone -= Math.Min(20 - take, stone);            // its set-aside
                continue;
            }
            built[s] = 1;
            double left = C(w, new SettlementId(s), 10.0) - ConstructionQuery.LabourBetween(labour, 0, N);
            long dp = ConstructionQuery.LabourBound(left, labour, 0);
            if (dp <= 0) continue;                               // not started: the head next step
            long second = Math.Min(dp, stone >= 20 ? N : stone * N / 20);
            long take2 = ConstructionQuery.Delivered(20, second);
            stone -= take2;
            drawn += take2;
            if (second == N) { built[s] = 2; continue; }
            part[s] = second;
            stone -= Math.Min(20 - take2, stone);
        }
        Assert.True(built[0] == 2, "anti-vacuity: the oracle continues settlement 0's later entry in the same step");

        WorldState next = ConstructionOnly(Cfg, null).Step(w);
        for (int s = 0; s < Settlements; s++)
        {
            var id = new SettlementId(s);
            Assert.Equal(built[s], Built(next, id));
            ConstructionQueueRow[] q = ConstructionQuery.Queue(next, id);
            Assert.Equal(2 - (int)built[s], q.Length);
            if (q.Length > 0) Assert.Equal(part[s], ConstructionQuery.ProgressOf(next, q[0]));
        }
        Assert.Equal(drawn, Sunk(next, Stone) - Sunk(w, Stone));
        AssertConserved(next);
    }

    private static T[] Rows<T>(Table<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }
}
