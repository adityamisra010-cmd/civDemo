using Proto.M7.Econ;

namespace Proto.M7.Econ.Tests;

/// <summary>The automatic priority: growth first, then military movement, never civilian starvation. All exact longs.</summary>
public class LogisticsPriorityTests
{
    private static void AssertConserved(LogisticsDecision d) =>
        Assert.Equal(d.Free, d.CivilianGranted + d.GrowthGranted + d.MilitaryGranted + d.Unallocated);

    [Fact]
    public void Unconstrained_EverythingGranted_RestStaysInStore()
    {
        LogisticsDecision d = LogisticsPriority.Resolve(1_000, 600, 100, [new MoveRequest(1, 0, 50, 10)]);
        Assert.Equal(ConstraintStage.Unconstrained, d.Stage);
        Assert.Equal(600, d.CivilianGranted);
        Assert.Equal(100, d.GrowthGranted);
        Assert.Equal(50, d.MilitaryGranted);
        Assert.Equal(250, d.Unallocated);
        Assert.Equal(MoveOutcome.Granted, d.Moves[0].Outcome);
        AssertConserved(d);
    }

    [Fact]
    public void Stage1_GrowthIsCutBeforeAnyMoveIsTouched()
    {
        // Surplus 400 − 0 civilian slack: moves 300 fit whole; growth asks 200 and gets the 100 left.
        LogisticsDecision d = LogisticsPriority.Resolve(1_000, 600, 200, [new MoveRequest(1, 0, 300, 30)]);
        Assert.Equal(ConstraintStage.GrowthReduced, d.Stage);
        Assert.Equal(MoveOutcome.Granted, d.Moves[0].Outcome);
        Assert.Equal(300, d.MilitaryGranted);
        Assert.Equal(100, d.GrowthGranted);
        Assert.Equal(100, d.GrowthCut);
        Assert.Equal(600, d.CivilianGranted);
        AssertConserved(d);
    }

    [Fact]
    public void Stage2_GrowthAtZero_ThenTheMoveIsShortenedToWholeDays()
    {
        // Surplus 250 against a 300-unit, 30-day move: floor(250 × 30 / 300) = 25 days, ceil(25 × 300 / 30) = 250 units.
        LogisticsDecision d = LogisticsPriority.Resolve(850, 600, 200, [new MoveRequest(7, 0, 300, 30)]);
        Assert.Equal(ConstraintStage.MilitaryRestricted, d.Stage);
        Assert.Equal(0, d.GrowthGranted);
        Assert.Equal(200, d.GrowthCut);
        Assert.Equal(new MoveDecision(7, 0, 300, 30, 250, 25, MoveOutcome.Shortened), d.Moves[0]);
        Assert.Equal(600, d.CivilianGranted);
        Assert.Equal(0, d.Unallocated);
        AssertConserved(d);
    }

    [Fact]
    public void Shortened_GrantIsAtMostTheRemainder_AndTheDaysAreMaximal()
    {
        // Odd numbers: remaining 101, Draw 997 over 13 days → days = floor(101·13/997) = 1, granted = ceil(997/13) = 77.
        LogisticsDecision d = LogisticsPriority.Resolve(101, 0, 0, [new MoveRequest(1, 0, 997, 13)]);
        MoveDecision m = d.Moves[0];
        Assert.Equal(1, m.GrantedDays);
        Assert.Equal(77, m.Granted);
        Assert.True(m.Granted <= 101);
        Assert.True((m.GrantedDays + 1) * 997 > 101L * 13);   // one more day would not be paid for
        AssertConserved(d);
    }

    [Fact]
    public void NotOneDayFits_TheMoveIsDenied()
    {
        LogisticsDecision d = LogisticsPriority.Resolve(610, 600, 0, [new MoveRequest(1, 0, 1_000, 30)]);   // 10 < 1000/30
        Assert.Equal(MoveOutcome.Denied, d.Moves[0].Outcome);
        Assert.Equal(0, d.MilitaryGranted);
        Assert.Equal(ConstraintStage.MilitaryRestricted, d.Stage);
        AssertConserved(d);
    }

    [Fact]
    public void CivilianShortfall_MilitaryGetsNothing_AndTheShortfallIsNotMilitaryCaused()
    {
        LogisticsDecision d = LogisticsPriority.Resolve(500, 600, 100, [new MoveRequest(1, 0, 1, 1), new MoveRequest(2, 0, 50, 5)]);
        Assert.Equal(ConstraintStage.CivilianShortfall, d.Stage);
        Assert.Equal(500, d.CivilianGranted);
        Assert.Equal(100, d.CivilianShortfall);
        Assert.Equal(0, d.MilitaryGranted);
        Assert.Equal(0, d.GrowthGranted);
        Assert.All(d.Moves, m => Assert.Equal(MoveOutcome.Denied, m.Outcome));
        Assert.Equal(0, d.MilitaryCausedStarvation);
        AssertConserved(d);
    }

    [Fact]
    public void AfterARestriction_LaterMovesAreDenied_EvenWhenSmaller()
    {
        // Move 1 (priority 0) is shortened; move 2 (priority 1) would fit the 0 left anyway, but a tiny third move with
        // priority 2 must NOT overtake: no lower-priority move is served after a higher one was restricted.
        LogisticsDecision d = LogisticsPriority.Resolve(100, 0, 0,
            [new MoveRequest(3, 2, 0, 1), new MoveRequest(1, 0, 150, 3), new MoveRequest(2, 1, 1, 1)]);
        Assert.Equal([1L, 2L, 3L], d.Moves.Select(m => m.Id).ToArray());
        Assert.Equal(MoveOutcome.Shortened, d.Moves[0].Outcome);
        Assert.Equal(MoveOutcome.Denied, d.Moves[1].Outcome);
        Assert.Equal(MoveOutcome.Denied, d.Moves[2].Outcome);
        AssertConserved(d);
    }

    [Fact]
    public void TieDense_EqualPrioritiesAndDraws_OrderIsByIdRegardlessOfInputOrder()
    {
        // 40 identical requests (priority 0, draw 10, 1 day) against 155 surplus units: exactly ids 1..15 granted, 16 shortened? —
        // a 1-day move cannot be shortened (0 days fit), so 16 is DENIED and every later id denied too.
        var reqs = new MoveRequest[40];
        for (int i = 0; i < reqs.Length; i++) reqs[i] = new MoveRequest(i + 1, 0, 10, 1);
        LogisticsDecision baseline = LogisticsPriority.Resolve(155, 0, 0, reqs);
        for (int perm = 0; perm < 25; perm++)
        {
            MoveRequest[] shuffled = Permute(reqs, perm);
            LogisticsDecision d = LogisticsPriority.Resolve(155, 0, 0, shuffled);
            Assert.Equal(baseline.Moves, d.Moves);
            Assert.Equal(baseline.Unallocated, d.Unallocated);
        }
        for (int i = 0; i < 15; i++) Assert.Equal(MoveOutcome.Granted, baseline.Moves[i].Outcome);
        for (int i = 15; i < 40; i++) Assert.Equal(MoveOutcome.Denied, baseline.Moves[i].Outcome);
        Assert.Equal(150, baseline.MilitaryGranted);
        Assert.Equal(5, baseline.Unallocated);
    }

    [Fact]
    public void PriorityBeatsId()
    {
        LogisticsDecision d = LogisticsPriority.Resolve(10, 0, 0, [new MoveRequest(1, 5, 10, 1), new MoveRequest(9, 0, 10, 1)]);
        Assert.Equal(9, d.Moves[0].Id);
        Assert.Equal(MoveOutcome.Granted, d.Moves[0].Outcome);
        Assert.Equal(MoveOutcome.Denied, d.Moves[1].Outcome);
    }

    [Fact]
    public void Property_Sweep_ConservationCivilianFirstAndMonotoneInFreeGrain()
    {
        // A deterministic grid (no RNG): every combination is checked exactly.
        long[] frees = [0, 1, 7, 99, 100, 101, 250, 599, 600, 601, 999, 5_000];
        long[] reqs = [0, 1, 100, 600];
        long[] growths = [0, 1, 50, 400];
        MoveRequest[][] moveSets =
        [
            [],
            [new MoveRequest(1, 0, 100, 10)],
            [new MoveRequest(1, 0, 100, 10), new MoveRequest(2, 0, 333, 7), new MoveRequest(3, 1, 0, 2)],
        ];
        foreach (long r in reqs)
        foreach (long g in growths)
        foreach (MoveRequest[] ms in moveSets)
        {
            LogisticsDecision? previous = null;
            foreach (long f in frees)
            {
                LogisticsDecision d = LogisticsPriority.Resolve(f, r, g, ms);
                AssertConserved(d);
                Assert.Equal(Math.Min(f, r), d.CivilianGranted);
                if (d.MilitaryGranted > 0) Assert.Equal(r, d.CivilianGranted);   // military only from beyond the requirement
                if (d.GrowthGranted > 0)                                         // growth only after every move is whole
                    Assert.All(d.Moves, m => Assert.Equal(MoveOutcome.Granted, m.Outcome));
                Assert.True(d.MilitaryGranted <= d.MilitaryRequested);
                Assert.True(d.GrowthGranted <= g);
                if (previous is not null)
                {
                    Assert.True(d.CivilianGranted >= previous.CivilianGranted);
                    Assert.True(d.MilitaryGranted >= previous.MilitaryGranted);
                    Assert.True(d.GrowthGranted >= previous.GrowthGranted);
                }
                previous = d;
            }
        }
    }

    [Fact]
    public void InvalidInputs_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LogisticsPriority.Resolve(-1, 0, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogisticsPriority.Resolve(0, -1, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogisticsPriority.Resolve(0, 0, -1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogisticsPriority.Resolve(0, 0, 0, [new MoveRequest(1, 0, 5, 0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogisticsPriority.Resolve(0, 0, 0, [new MoveRequest(1, 0, -5, 1)]));
        Assert.Throws<ArgumentException>(() => LogisticsPriority.Resolve(0, 0, 0, [new MoveRequest(1, 0, 5, 1), new MoveRequest(1, 1, 5, 1)]));
    }

    /// <summary>A deterministic permutation (rotation + interleave by a multiplier coprime to 40-ish lengths).</summary>
    private static MoveRequest[] Permute(MoveRequest[] src, int k)
    {
        int n = src.Length;
        int mul = 1 + 2 * (k % 7);   // odd multipliers 1..13
        while (Gcd(mul, n) != 1) mul += 2;
        var r = new MoveRequest[n];
        for (int i = 0; i < n; i++) r[i] = src[(int)(((long)i * mul + k) % n)];
        return r;
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}
