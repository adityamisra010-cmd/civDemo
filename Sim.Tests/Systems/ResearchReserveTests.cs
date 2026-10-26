using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-051 (PROPOSED; research.json tuning.researchReserve, absent = off) — THE CAPPED TECHNOLOGY-LANE RP RESERVE. A step whose
/// technology lane funds nothing deposits the step's RP pool, up to capTurns × the current pool (the rest is lost); a step
/// whose technology lane funds its OWN technology node draws min(reserve, drawTurns × pool), spent by the pool's rules, LOSSY
/// (what the funding cannot absorb is lost, never refunded). Never for an assist or a civic; the CP bank is untouched.
///
/// Rig (ResearchRigs, the ResearchPointLifecycleTests rig): one player polity of 10 000 people, two lanes (CP 1.0 × P^0.5 a
/// sim-year: 1 000 CP a dt-10 step), destination routing on, overflow off unless stated, reserve {capTurns 3, drawTurns 1}
/// unless stated. R = ResearchQuery.ResearchPointPool (1 000 RP, measured: Rm = 1 000 000 milli-RP). Every expected value
/// is the arithmetic in its comment; costs are sized from R.
/// </summary>
public class ResearchReserveTests
{
    private static readonly PolityId P = new(Player);

    private static readonly Lazy<double> Rp = new(() =>
    {
        ResearchContent probe = new Spec { Stage = "x", Technologies = { new Node(1, "x") }, Civics = { new Node(2001, "cv") } }.Load();
        return ResearchQuery.ResearchPointPool(PlayerWorld(), probe, P);
    });

    private static double R => Rp.Value;

    /// <summary>One turn of RP in milli-RP — measured through the engine's own conversion.</summary>
    private static long Rm => ResearchQuery.ToMilliRp(R);

    /// <summary>The rig: two lanes, destination routing, t0 (key 1, 5 R) the stage node nobody names unless a test does.</summary>
    private static ResearchContent Rig(Action<Spec> add, bool reserve = true, bool overflow = false, double capTurns = 3.0,
        double drawTurns = 1.0, bool twoLanes = true)
    {
        var s = new Spec
        {
            Stage = "t0", DestinationRouting = true, CivicsPoints = twoLanes ? (true, 1.0, 0.5) : null, ResearchOverflow = overflow,
            ResearchReserve = reserve ? (capTurns, drawTurns) : null,
        };
        s.Technologies.Add(new Node(1, "t0", Cost: 5.0 * R));
        add(s);
        if (s.Civics.Count == 0) s.Civics.Add(new Node(2001, "cv0", Cost: 400_000.0));
        ResearchContent c = s.Load();
        Assert.Equal(reserve, c.Tuning.Reserve is not null);
        return c;
    }

    private static WorldState Toward(params int[] keys)
    {
        WorldState w = PlayerWorld();
        foreach (int k in keys) w.ResearchTargets.Add(new ResearchTargetRow(P, Key(k)));
        return w;
    }

    private static WorldState WithReserve(WorldState w, long milli)
    {
        w.ResearchReserves.Add(new ResearchReserveRow(P, milli));
        return w;
    }

    private static long Reserve(IReadOnlyWorldState w) => ResearchQuery.ReserveMilli(w, P);

    private static void Identity(IReadOnlyWorldState w)
    {
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));
        Assert.Null(ResearchQuery.ReservesInvariantViolation(w.ResearchReserves));
    }

    [Fact]
    public void TheRig_MakesExactlyOneThousandRp()
    {
        Assert.Equal(1000.0, R);
        Assert.Equal(1_000_000L, Rm);
    }

    // ================================================================== deposit

    [Fact]
    public void Deposit_ALaneFundingNothing_PutsTheStepsPoolIntoTheReserve_AndChangesNothingElse()
    {
        // No technology destination; t0 and x available. ON: fund[0] = −1 → the step's R goes to the reserve (Rm milli-RP).
        // OFF (the twin): R is lost and no reserve row is written. Every other table is identical between the two.
        ResearchContent on = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 2.0 * R)));
        ResearchContent off = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 2.0 * R)), reserve: false);
        WorldState w = PlayerWorld();
        WorldState a = Executor(on).Step(w);
        WorldState b = Executor(off).Step(w);
        Assert.Equal(1, a.ResearchReserves.Count);
        Assert.Equal(new ResearchReserveRow(P, Rm), a.ResearchReserves[0]);
        Assert.Equal(0, b.ResearchReserves.Count);
        Assert.Empty(ResearchQuery.PartialProgress(a, P));
        WorldState stripped = a.Clone();
        stripped.ResearchReserves.Clear();
        Assert.True(WorldStates.StateEquals(stripped, b), "the reserve changed something besides its own table");
        // CP is untouched: no civics destination, the whole accrual is banked, exactly as with the switch off.
        Assert.Equal(ResearchQuery.CivicsPointsMilli(w, on, P, 10.0), ResearchQuery.CivicsPointsAccount(a, P).BalanceMilli);
        Identity(a);
    }

    [Fact]
    public void Deposit_AWaitingDestination_Deposits_AndTheCivicIsPaidOnlyFromCp()
    {
        // td needs the civic cv (huge): the technology lane holds a destination but funds nothing (waiting on the Civics tree),
        // so it deposits; the civics lane (no destination) assists td's route, paying cv from the CP bank only.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(2, "td", "cv", Cost: 10.0 * R));
            s.Civics.Add(new Node(1001, "cv", Cost: 400_000.0));
        });
        WorldState w = Toward(2);
        Assert.Equal(LaneActivity.Waiting, ResearchQuery.Activity(ResearchQuery.Route(w, c, P, ResearchTree.Technology)));
        WorldState next = Executor(c).Step(w);
        Assert.Equal(Rm, Reserve(next));
        CivicsPointsRow cp = ResearchQuery.CivicsPointsAccount(next, P);
        Assert.Equal(cp.SpentMilli / 1000.0, Progress(next, 1001));   // civic progress is exactly the CP spent
        Identity(next);
    }

    [Fact]
    public void LongWait_TheReserveSaturatesAtTheCap_AndTheExcessIsLostEveryStep()
    {
        // Twelve steps with no destination. After step k the reserve is min(k, 3) × Rm; from step 4 on each step's R is lost.
        ResearchContent c = Rig(s => { });
        Assert.Equal(3 * Rm, ResearchQuery.ReserveCapMilli(PlayerWorld(), c, P));   // floor(1000 × 3 × R)
        TurnExecutor ex = Executor(c);
        WorldState w = PlayerWorld();
        for (int k = 1; k <= 12; k++)
        {
            long expectedDeposit = Math.Min(Rm, 3 * Rm - Reserve(w));
            Assert.Equal(expectedDeposit, ResearchQuery.ReserveDepositMilli(w, c, P));
            w = ex.Step(w);
            Assert.Equal(Math.Min(k, 3) * Rm, Reserve(w));
            Assert.Empty(ResearchQuery.PartialProgress(w, P));
            Identity(w);
        }
        Assert.Equal(0L, ResearchQuery.ReserveDepositMilli(w, c, P));   // saturated: nothing more goes in
    }

    [Fact]
    public void Saturation_APartialRoomTakesOnlyTheRoom()
    {
        // capTurns 2.5: deposits R, R, then 0.5 R (the room), then nothing: 1.0, 2.0, 2.5, 2.5 turns.
        ResearchContent c = Rig(s => { }, capTurns: 2.5);
        TurnExecutor ex = Executor(c);
        WorldState w = PlayerWorld();
        long[] expected = [Rm, 2 * Rm, 5 * Rm / 2, 5 * Rm / 2];
        foreach (long e in expected)
        {
            w = ex.Step(w);
            Assert.Equal(e, Reserve(w));
        }
    }

    // ================================================================== draw

    [Fact]
    public void Draw_IsAtMostOnePoolATurn_AndTheReserveFallsByTheWholeDraw()
    {
        // A full reserve (3 Rm) and a destination x of 10 R: each step funds R + R (pool + one turn's draw) until the reserve
        // is empty, then R alone: progress 2, 4, 6, 7, 8 R; reserve 2, 1, 0, 0, 0 Rm (the row stays, at 0).
        ResearchContent c = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 10.0 * R)));
        TurnExecutor ex = Executor(c);
        WorldState w = WithReserve(Toward(2), 3 * Rm);
        (double Progress, long Reserve)[] expected = [(2 * R, 2 * Rm), (4 * R, Rm), (6 * R, 0), (7 * R, 0), (8 * R, 0)];
        foreach ((double progress, long reserve) in expected)
        {
            Assert.True(ResearchQuery.ReserveDrawMilli(w, c, P) <= Rm);
            w = ex.Step(w);
            Assert.Equal(progress, Progress(w, 2));
            Assert.Equal(reserve, Reserve(w));
            Assert.Equal(1, w.ResearchReserves.Count);
            Identity(w);
        }
    }

    [Fact]
    public void Draw_IsLossy_WhatTheNodeCannotAbsorbIsLost_NotReturned()
    {
        // Reserve 3 Rm; x costs 1.5 R. The step holds R + R = 2 R; x absorbs 1.5 R and completes; 0.5 R is lost. The reserve
        // falls by the whole draw (to 2 Rm), never by only the part used (2.5 Rm would be the EXACT rule that rewards idling).
        ResearchContent c = Rig(s => s.Technologies.AddRange([new Node(2, "x", Cost: 1.5 * R), new Node(3, "y", Cost: 5.0 * R)]));
        WorldState next = Executor(c).Step(WithReserve(Toward(2), 3 * Rm));
        Assert.True(Done(next, 2));
        Assert.Equal(2 * Rm, Reserve(next));
        Assert.Empty(ResearchQuery.PartialProgress(next, P));   // the surplus went nowhere
        // Even a node the pool alone pays: x' 0.5 R — the draw still leaves the reserve whole and is lost.
        ResearchContent c2 = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 0.5 * R)));
        WorldState n2 = Executor(c2).Step(WithReserve(Toward(2), 3 * Rm));
        Assert.True(Done(n2, 2));
        Assert.Equal(2 * Rm, Reserve(n2));
    }

    [Fact]
    public void NoDraw_WhenTheLaneAssistsACivic_OrFundsACivicInTheOnePoolModel()
    {
        // Two lanes: no technology destination; the civics destination cd needs technology t1, so the technology lane ASSISTS
        // the Civics tree with t1. It is funded by the pool only (R), and the reserve neither pays nor receives (3 Rm stays).
        ResearchContent c = Rig(s =>
        {
            s.Technologies.Add(new Node(2, "t1", Cost: 5.0 * R));
            s.Civics.Add(new Node(1001, "cd", "t1", Cost: 400_000.0));
        });
        WorldState w = WithReserve(Toward(1001), 3 * Rm);
        Assert.Equal(LaneActivity.Assisting, ResearchQuery.Activity(ResearchQuery.Route(w, c, P, ResearchTree.Technology)));
        WorldState next = Executor(c).Step(w);
        Assert.Equal(R, Progress(next, 2));
        Assert.Equal(3 * Rm, Reserve(next));
        Identity(next);

        // One-pool model (civicsPoints absent): the one target is the civic law; RP pays it, the reserve does not.
        ResearchContent one = Rig(s => s.Civics.Add(new Node(1001, "law", Cost: 5.0 * R)), twoLanes: false);
        WorldState o = Executor(one).Step(WithReserve(Toward(1001), 3 * Rm));
        Assert.Equal(R, Progress(o, 1001));
        Assert.Equal(3 * Rm, Reserve(o));
    }

    // ================================================================== gates

    /// <summary>td (10 R) needs the civic cv (3 500 CP): the technology lane waits until cv completes, then draws.</summary>
    private static ResearchContent Gate(bool reserve = true) => Rig(s =>
    {
        s.Technologies.Add(new Node(2, "td", "cv", Cost: 10.0 * R));
        s.Civics.Add(new Node(1001, "cv", Cost: 3_500.0));
    }, reserve);

    [Fact]
    public void FirstTurnsAfterTheGateOpens_DrawOnePoolATurnUntilEmpty_AndCpIsUntouched()
    {
        // CP: 1 000 a step, all to cv (the civics lane assists td's route): cv completes in step 4 (500 CP stay banked).
        // RP: steps 1–3 deposit R each (3 Rm, the cap); step 4 still waits (cv completes IN the step): R lost, reserve 3 Rm.
        // Step 5, td available on PREV: R + R → 2 R, reserve 2 Rm; step 6: 4 R, 1 Rm; step 7: 6 R, 0; step 8: 7 R; step 9: 8 R.
        ResearchContent on = Gate(), off = Gate(reserve: false);
        TurnExecutor exOn = Executor(on), exOff = Executor(off);
        WorldState a = Toward(2), b = Toward(2);
        (double Td, long Reserve)[] expected =
            [(0, Rm), (0, 2 * Rm), (0, 3 * Rm), (0, 3 * Rm), (2 * R, 2 * Rm), (4 * R, Rm), (6 * R, 0), (7 * R, 0), (8 * R, 0)];
        for (int k = 0; k < expected.Length; k++)
        {
            a = exOn.Step(a);
            b = exOff.Step(b);
            Assert.Equal(expected[k].Td, Progress(a, 2));
            Assert.Equal(expected[k].Reserve, Reserve(a));
            // The CP bank and civic progress are byte-identical to the no-reserve twin, every step; the identity holds.
            Assert.True(WorldStates.StateEquals(Only(a.CivicsPoints), Only(b.CivicsPoints)));
            Assert.Equal(Progress(b, 1001), Progress(a, 1001));
            Assert.Equal(Done(b, 1001), Done(a, 1001));
            Identity(a);
        }
        Assert.True(Done(a, 1001));
        Assert.Equal(5_500_000L, ResearchQuery.CivicsPointsAccount(a, P).BalanceMilli);   // 500 CP left at step 4 + steps 5–9 banked
        // Without the reserve the same steps give td 1, 2, 3, 4, 5 R: the reserve recovered exactly the 3 R of the wait.
        Assert.Equal(5 * R, Progress(b, 2));
    }

    private static WorldState Only(Table<CivicsPointsRow> rows)
    {
        var w = new WorldState(0);
        for (int i = 0; i < rows.Count; i++) w.CivicsPoints.Add(rows[i]);
        return w;
    }

    [Fact]
    public void RepeatedGates_TheReserveRefillsAfterBeingDrawnDown()
    {
        // Destination td2; route cv1 (2 500 CP) → td1 (2.5 R) → cv2 (4 500 CP) → td2 (10 R): two waits on the Civics tree.
        // RP per step:  1–3 wait: R, 2R, 3R | 4 td1: 2R, res 2R | 5 td1 done (needs 0.5 R; 1.5 R lost), res R |
        //               6–7 wait on cv2: 2R, 3R | 8 td2: 2R, res 2R | 9: 4R, res R | 10: 6R, 0 | 11: 7R, 0.
        // CP per step:  cv1 1 000 / 2 000 / done at 3 (500 left) | 4–5 banked (cv2 needs td1) | 6 cv2 3 500 | 7 cv2 done.
        ResearchContent c = Rig(s =>
        {
            s.Technologies.AddRange([new Node(2, "td1", "cv1", Cost: 2.5 * R), new Node(3, "td2", "cv2", Cost: 10.0 * R)]);
            s.Civics.AddRange([new Node(1001, "cv1", Cost: 2_500.0), new Node(1002, "cv2", "td1", Cost: 4_500.0)]);
        });
        TurnExecutor ex = Executor(c);
        WorldState w = Toward(3);
        long[] reserve = [Rm, 2 * Rm, 3 * Rm, 2 * Rm, Rm, 2 * Rm, 3 * Rm, 2 * Rm, Rm, 0, 0];
        double[] td2 = [0, 0, 0, 0, 0, 0, 0, 2 * R, 4 * R, 6 * R, 7 * R];
        for (int k = 0; k < reserve.Length; k++)
        {
            w = ex.Step(w);
            Assert.Equal(reserve[k], Reserve(w));
            Assert.Equal(td2[k], Progress(w, 3));
            if (k == 3) Assert.Equal(2 * R, Progress(w, 2));
            if (k == 4) Assert.True(Done(w, 2));
            if (k == 6) Assert.True(Done(w, 1002));
            Identity(w);
        }
    }

    [Fact]
    public void AgeSweepGate_TheTechnologyLaneDepositsWhileTheCohortWaits_AndDrawsWhenTheSweepOpens()
    {
        // The real Age sweep (ADR-048): td is Age A3, so it is eligible only once every A1 node of both trees is complete — t0
        // (already complete here) and the civic cv (2 500 CP). The technology lane waits (deposits R, 2 R, 3 R); cv completes
        // in step 3 on CP alone; from step 4 td draws one pool a turn.
        var s = new Spec
        {
            Stage = "t0", DestinationRouting = true, AgeSweep = true, CivicsPoints = (true, 1.0, 0.5), ResearchReserve = (3.0, 1.0),
        };
        s.Technologies.AddRange([new Node(1, "t0", Cost: 5.0 * R), new Node(2, "td", Cost: 10.0 * R, Age: "A3")]);
        s.Civics.Add(new Node(1001, "cv", Cost: 2_500.0));
        ResearchContent c = s.Load();
        TurnExecutor ex = Executor(c);
        WorldState w = WithCompleted(Toward(2), 1);
        Assert.Equal(LaneActivity.Waiting, ResearchQuery.Activity(ResearchQuery.Route(w, c, P, ResearchTree.Technology)));
        long[] reserve = [Rm, 2 * Rm, 3 * Rm, 2 * Rm, Rm, 0, 0];
        double[] td = [0, 0, 0, 2 * R, 4 * R, 6 * R, 7 * R];
        for (int k = 0; k < reserve.Length; k++)
        {
            w = ex.Step(w);
            Assert.Equal(reserve[k], Reserve(w));
            Assert.Equal(td[k], Progress(w, 2));
        }
    }

    // ================================================================== overflow (ADR-050) with the reserve

    private static ResearchContent Fan(bool reserve) => Rig(s => s.Technologies.AddRange([
        new Node(2, "x", Cost: 0.6 * R), new Node(3, "y", Cost: 0.6 * R), new Node(4, "z", Cost: 0.6 * R),
        new Node(5, "d", "x AND y AND z", Cost: 1.0 * R),
    ]), reserve, overflow: true);

    [Fact]
    public void WithOverflow_TheDrawFlowsAlongTheRoute()
    {
        // Reserve 3 Rm, destination d (needs x, y, z, 0.6 R each). Overflow ON: R + R = 2 R pays x, y and z (1.8 R); 0.2 R is
        // lost (d is not available on PREV); the reserve falls to 2 Rm. Without the reserve: x, then 0.4 R on y.
        ResearchContent on = Fan(reserve: true);
        WorldState a = Executor(on).Step(WithReserve(Toward(5), 3 * Rm));
        Assert.True(Done(a, 2) && Done(a, 3) && Done(a, 4));
        Assert.Equal(0.0, Progress(a, 5));
        Assert.Equal(2 * Rm, Reserve(a));
        ResearchContent off = Fan(reserve: false);
        WorldState b = Executor(off).Step(Toward(5));
        Assert.True(Done(b, 2));
        Assert.False(Done(b, 3));
        Assert.Equal(R - 0.6 * R, Progress(b, 3));
        // Overflow OFF with the reserve: one node only — x (0.6 R) of the 2 R; 1.4 R lost; reserve 2 Rm all the same.
        ResearchContent one = Rig(s => s.Technologies.AddRange([
            new Node(2, "x", Cost: 0.6 * R), new Node(3, "y", Cost: 0.6 * R), new Node(4, "z", Cost: 0.6 * R),
            new Node(5, "d", "x AND y AND z", Cost: 1.0 * R)]));
        WorldState o = Executor(one).Step(WithReserve(Toward(5), 3 * Rm));
        Assert.True(Done(o, 2));
        Assert.Equal(0.0, Progress(o, 3));
        Assert.Equal(2 * Rm, Reserve(o));
    }

    [Fact]
    public void WithOverflow_TheDrawNeverPaysAnAssist_TheUnspentDrawIsDroppedThere()
    {
        // Technology destination td needs x (0.3 R); the civics destination cd needs technology t1 (5 R). Overflow ON, reserve
        // 3 Rm: R + R → x takes 0.3 R (from the pool), then overflow reaches t1 — an ASSIST on the Civics route: the unspent draw
        // (R) is dropped and only the pool's remaining 0.7 R pays t1 — exactly what t1 gets with the reserve off.
        ResearchContent Load(bool reserve) => Rig(s =>
        {
            s.Technologies.AddRange([new Node(2, "x", Cost: 0.3 * R), new Node(3, "td", "x", Cost: R), new Node(4, "t1", Cost: 5.0 * R)]);
            s.Civics.Add(new Node(1001, "cd", "t1", Cost: 400_000.0));
        }, reserve, overflow: true);
        ResearchContent on = Load(true), off = Load(false);
        WorldState a = Executor(on).Step(WithReserve(Toward(3, 1001), 3 * Rm));
        WorldState b = Executor(off).Step(Toward(3, 1001));
        Assert.True(Done(a, 2) && Done(b, 2));
        Assert.Equal(R - 0.3 * R, Progress(b, 4));
        Assert.Equal(Progress(b, 4), Progress(a, 4));
        Assert.Equal(2 * Rm, Reserve(a));
    }

    // ================================================================== order timing, save/load, replay

    [Fact]
    public void TurnExactPin_DepositsLandInTheStepsWorld_AndAnOrderStampedTurnTDrawsInTheStepFromT()
    {
        // No destination until an order stamped turn 2 names x (10 R). Step 0→1 deposits R (world of turn 1: Rm); 1→2 deposits
        // R (turn 2: 2 Rm, x untouched); the order applies in the step 2→3, which draws: turn 3 shows x 2 R and Rm.
        ResearchContent c = Rig(s => s.Technologies.Add(new Node(2, "x", Cost: 10.0 * R)));
        var log = new OrderLog();
        log.Append(Target(2, 2));
        TurnExecutor ex = Executor(c, log);
        WorldState w = PlayerWorld();
        Assert.Equal(0L, w.Clock.Turn);
        w = ex.Step(w);
        Assert.Equal((1L, Rm, 0.0), (w.Clock.Turn, Reserve(w), Progress(w, 2)));
        w = ex.Step(w);
        Assert.Equal((2L, 2 * Rm, 0.0), (w.Clock.Turn, Reserve(w), Progress(w, 2)));
        w = ex.Step(w);
        Assert.Equal((3L, Rm, 2 * R), (w.Clock.Turn, Reserve(w), Progress(w, 2)));
        w = ex.Step(w);
        Assert.Equal((4L, 0L, 4 * R), (w.Clock.Turn, Reserve(w), Progress(w, 2)));
    }

    [Fact]
    public void SaveLoad_MidRunWithANonZeroReserve_ContinuesIdentically()
    {
        ResearchContent c = Gate();
        TurnExecutor ex = Executor(c);
        WorldState w = Run(ex, Toward(2), 5);            // reserve 2 Rm, td 2 R (FirstTurnsAfterTheGateOpens' table)
        Assert.Equal(2 * Rm, Reserve(w));
        using var ms = new MemoryStream();
        Snapshot.Save(w, ms);
        WorldState loaded = Snapshot.Load(new MemoryStream(ms.ToArray()));
        Assert.True(WorldStates.StateEquals(w, loaded));
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(loaded));
        WorldState a = Run(ex, w, 4), b = Run(Executor(c), loaded, 4);
        Assert.True(WorldStates.StateEquals(a, b));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.Equal(0L, Reserve(b));
        Assert.Equal(8 * R, Progress(b, 2));
    }

    [Fact]
    public void Replay_WithTheSwitchOn_ReproducesTheRunFromItsOrderLog()
    {
        // A run with orders (a destination at turn 4, a clear at turn 9, a new destination at turn 13): live, then replayed from
        // the same genesis and log by a fresh executor — every step's hash equal, and the reserve moved both ways.
        ResearchContent c = Rig(s => s.Technologies.AddRange([new Node(2, "x", Cost: 3.0 * R), new Node(3, "y", Cost: 10.0 * R)]),
            overflow: true);
        var log = new OrderLog();
        log.Append(Target(4, 2));
        log.Append(Target(9, ResearchQuery.ClearTechnologyTargetId));
        log.Append(Target(13, 3));
        var live = new List<string>();
        WorldState w = PlayerWorld();
        TurnExecutor ex = Executor(c, log);
        bool deposited = false, drew = false;
        for (int t = 0; t < 20; t++)
        {
            WorldState next = ex.Step(w);
            if (Reserve(next) > Reserve(w)) deposited = true;
            if (Reserve(next) < Reserve(w)) drew = true;
            w = next;
            live.Add(WorldHash.ComputeHex(w));
        }
        Assert.True(deposited && drew);
        WorldState r = PlayerWorld();
        TurnExecutor replay = Executor(c, log);
        for (int t = 0; t < 20; t++)
        {
            r = replay.Step(r);
            Assert.Equal(live[t], WorldHash.ComputeHex(r));
        }
    }

    // ================================================================== content validation

    [Theory]
    [InlineData(0.0, 1.0, "capTurns")]
    [InlineData(-1.0, 1.0, "capTurns")]
    [InlineData(3.0, 0.0, "drawTurns")]
    [InlineData(3.0, -0.5, "drawTurns")]
    public void TheLoader_RefusesAReserveThatIsNotFiniteAndPositive(double cap, double draw, string field)
    {
        var s = new Spec
        {
            Stage = "x", Technologies = { new Node(1, "x") }, Civics = { new Node(2001, "cv") }, ResearchReserve = (cap, draw),
        };
        Exception e = Assert.ThrowsAny<Exception>(() => s.Load());
        Assert.Contains("tuning.researchReserve." + field, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShippedContent_HasNoReserve_TheSwitchIsOff()
    {
        Assert.Null(TestConfigs.Sim().Research!.Tuning.Reserve);
    }
}
