using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Tests.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Morale;

/// <summary>
/// [MOR] The in-battle "fight it won" rule (docs/m7-plan/morale-proposal.md §3.2; TacticalRules.LocalWinMoraleRise, default 0): when
/// an enemy land formation breaks, each formation still in the fight that dealt it damage within the last pulse length gains
/// floor(c × Start(broken) ÷ (Start(broken) + Start(winner))), capped at MoraleMax once. One shared implementation
/// (<see cref="LocalWinMorale.Apply"/>) called by both resolvers at the same point of their resolution.
/// </summary>
public sealed class LocalWinMoraleTests
{
    // ------------------------------------------------------------------ the rule, by hand

    [Fact]
    public void Rise_ByHand_TheLargerTheBeatenFormationAgainstOnesOwn()
    {
        Assert.Equal(100, LocalWinMorale.Rise(200, 50, 50));     // equal: half
        Assert.Equal(50, LocalWinMorale.Rise(200, 20, 60));      // floor(200 × 20 ÷ 80)
        Assert.Equal(133, LocalWinMorale.Rise(200, 100, 50));    // floor(200 × 100 ÷ 150)
        Assert.Equal(57, LocalWinMorale.Rise(200, 20, 50));      // floor(4,000 ÷ 70)
        Assert.Equal(0, LocalWinMorale.Rise(0, 50, 50));         // the default: off
        Assert.Equal(0, LocalWinMorale.Rise(200, 0, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocalWinMorale.Rise(-1, 50, 50));
        Assert.Throws<ArgumentException>(() => new TacticalRules { LocalWinMoraleRise = -1 }.Validate());
    }

    /// <summary>TURN-EXACT PIN of the window: damage dealt exactly one pulse length before the break counts, 1 unit more does not, a
    /// hit "after" the instant never does, and never-hit never does.</summary>
    [Fact]
    public void Fought_TheWindowIsOnePulseInclusive()
    {
        Assert.True(LocalWinMorale.Fought(1_000, 61_000, 60_000));
        Assert.True(LocalWinMorale.Fought(61_000, 61_000, 60_000));
        Assert.False(LocalWinMorale.Fought(999, 61_000, 60_000));
        Assert.False(LocalWinMorale.Fought(61_001, 61_000, 60_000));
        Assert.False(LocalWinMorale.Fought(long.MinValue, 61_000, 60_000));
        Assert.True(LocalWinMorale.Fought(7, 37, 30));            // the AutoResolver's unit: steps (30 per 60 s pulse)
        Assert.False(LocalWinMorale.Fought(6, 37, 30));
    }

    /// <summary>The table the tie-dense tests share: A (side 0, 50, morale 900) fought both G1 and G2; B (side 0, 50, morale 500)
    /// fought nobody; C (side 0, 40, morale 700) fought G1 but broke at the same instant; G1 (side 1, 50) and G2 (side 1, 30) broke
    /// now; H (side 1, 20) fought C, stays in the fight. A gains Rise(200, 50, 50) + Rise(200, 30, 50) = 100 + 75 = 175 → 1,075, capped
    /// at 1,000 once; H gains Rise(200, 40, 20) = 133 for C's break (800 → 933); B, C, G1, G2 nothing.</summary>
    private static (long[] Ids, int[] Sides, long[] Starts, bool[] Before, bool[] Now, long[] Hit, long[] Morale) Table(int[] order, long now)
    {
        long[] ids = { 1, 2, 3, 11, 12, 13 };
        int[] sides = { 0, 0, 0, 1, 1, 1 };
        long[] starts = { 50, 50, 40, 50, 30, 20 };
        bool[] before = { true, true, true, true, true, true };
        bool[] after = { true, true, false, false, false, true };
        long[] morale = { 900, 500, 700, 250, 100, 800 };
        // A hit G1 (2 s ago) and G2 (now); C hit G1 (now) but broke itself; H hit C exactly one pulse ago (in the window); G1 hit A;
        // B hit G2 1 ms outside the window; A "hit" C (friendly: a friendly break gives nothing).
        var hits = new (int W, int G, long At)[] { (0, 3, now - 2_000), (0, 4, now), (2, 3, now), (5, 2, now - 60_000), (3, 0, now), (1, 4, now - 60_001), (0, 2, now) };
        int n = ids.Length;
        // index i of the permuted table holds formation order[i]
        var pIds = new long[n]; var pSides = new int[n]; var pStarts = new long[n]; var pBefore = new bool[n]; var pNow = new bool[n]; var pMorale = new long[n];
        var at = new int[n];
        for (int i = 0; i < n; i++)
        {
            int f = order[i];
            pIds[i] = ids[f]; pSides[i] = sides[f]; pStarts[i] = starts[f]; pBefore[i] = before[f]; pNow[i] = after[f]; pMorale[i] = morale[f];
            at[f] = i;
        }
        var hit = new long[n * n];
        Array.Fill(hit, long.MinValue);
        foreach (var (w, g, t) in hits) hit[at[w] * n + at[g]] = t;
        return (pIds, pSides, pStarts, pBefore, pNow, hit, pMorale);
    }

    /// <summary>TIE-DENSE: several formations break at ONE instant; the gains of one winner add up and are capped once; a formation
    /// that broke at the same instant gains nothing; a friendly break gives nothing; a hit 1 ms outside the window gives nothing. The
    /// result per formation id is the same for all 720 orders of the six formations.</summary>
    [Fact]
    public void Apply_TieDense_SimultaneousBreaks_SumAndCapOnce_WhateverTheOrder()
    {
        const long now = 600_000;
        var expected = new Dictionary<long, long> { [1] = 1_000, [2] = 500, [3] = 700, [11] = 250, [12] = 100, [13] = 933 };
        int count = 0;
        foreach (var order in Permutations(6))
        {
            var t = Table(order, now);
            var log = new List<LocalWinRise>();
            Assert.True(LocalWinMorale.Apply(200, 1_000, 60_000, now, now, t.Ids, t.Sides, t.Starts, t.Before, t.Now, t.Hit, t.Morale, log));
            for (int i = 0; i < t.Ids.Length; i++) Assert.Equal(expected[t.Ids[i]], t.Morale[i]);
            var rows = log.Select(x => (x.FormationId, x.BrokenFormationId, x.Rise)).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { (1L, 11L, 100L), (1L, 12L, 75L), (13L, 3L, 133L) }, rows);
            Assert.All(log, x => Assert.Equal(now, x.AtMs));
            count++;
        }
        Assert.Equal(720, count);
    }

    /// <summary>[verification round 1, MOR-V1-02] TIE-DENSE: the two rises of one winner ADD UP. The table above, but A starts at 500,
    /// so the cap does not bind: A gains Rise(200, 50, 50) + Rise(200, 30, 50) = 100 + 75 = 175 and ends at 675 for all 720 orders (the
    /// larger rise alone would give 600; taking it instead of the sum survived the whole suite before this test, because in the table
    /// above both reach the cap: MEASURED). Every other formation as above.</summary>
    [Fact]
    public void Apply_TieDense_TheRisesOfOneWinnerAddUp_BelowTheCap_WhateverTheOrder()
    {
        const long now = 600_000;
        var expected = new Dictionary<long, long> { [1] = 675, [2] = 500, [3] = 700, [11] = 250, [12] = 100, [13] = 933 };
        int count = 0;
        foreach (var order in Permutations(6))
        {
            var t = Table(order, now);
            t.Morale[Array.IndexOf(t.Ids, 1L)] = 500;
            var log = new List<LocalWinRise>();
            Assert.True(LocalWinMorale.Apply(200, 1_000, 60_000, now, now, t.Ids, t.Sides, t.Starts, t.Before, t.Now, t.Hit, t.Morale, log));
            for (int i = 0; i < t.Ids.Length; i++) Assert.Equal(expected[t.Ids[i]], t.Morale[i]);
            Assert.Equal(175, log.Where(x => x.FormationId == 1).Sum(x => x.Rise));
            count++;
        }
        Assert.Equal(720, count);
    }

    [Fact]
    public void Apply_OffOrNoBreak_ChangesNothing()
    {
        var t = Table(new[] { 0, 1, 2, 3, 4, 5 }, 600_000);
        var before = (long[])t.Morale.Clone();
        var log = new List<LocalWinRise>();
        Assert.False(LocalWinMorale.Apply(0, 1_000, 60_000, 600_000, 600_000, t.Ids, t.Sides, t.Starts, t.Before, t.Now, t.Hit, t.Morale, log));
        Assert.Equal(before, t.Morale);
        Assert.False(LocalWinMorale.Apply(200, 1_000, 60_000, 600_000, 600_000, t.Ids, t.Sides, t.Starts, t.Before, t.Before, t.Hit, t.Morale, log));
        Assert.Equal(before, t.Morale);
        Assert.Empty(log);
        Assert.Throws<ArgumentException>(() => LocalWinMorale.Apply(200, 1_000, 60_000, 0, 0, t.Ids, t.Sides, t.Starts, t.Before, t.Now, new long[3], t.Morale, log));
    }

    private static IEnumerable<int[]> Permutations(int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = i;
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var p in Rec(k + 1)) yield return p;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
    }

    // ------------------------------------------------------------------ both resolvers, differentially

    /// <summary>A (side 0, heavy infantry 50, morale 900) and G (side 1, heavy infantry 10) advance on each other's start; B (side 0,
    /// heavy infantry 10, morale 800) holds far behind A (Defend: it never advances) and never fights. No side withdraws (retreat morale
    /// 0: the knowledge-only AI and the auto doctrine), so G, five times outnumbered, ends in a rout under A's blows and the battle with
    /// it. The winner's rise is floor(200 × 10 ÷ 60) = 33. TEST INPUT geometry. (A first version had A hold a Defend line against 20
    /// attackers: the TacticalResolver broke A first, the AutoResolver broke G — a resolver disagreement for lane P's battery, not a
    /// property of this rule.)</summary>
    private static BattleSetup Scenario(ulong seed, long rise)
    {
        Vec2L a = Cell(14, 16), b = Cell(4, 16), g = Cell(34, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { g }, new[] { a }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, a) { Morale = 900 },
                new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 10, g),
                new FormationSpec(3, 0, UnitProfiles.HeavyInfantry, 10, b) { Posture = Posture.Defend, Morale = 800 },
            },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = rise } };
    }

    private const long ExpectedRise = 33;                                // floor(200 × 10 ÷ (10 + 50))

    /// <summary>The two results are equal in everything but the winner's end morale, which differs by exactly the rise.</summary>
    private static void AssertOnlyTheWinnersMoraleDiffers(BattleResult off, BattleResult on, long winnerId, long rise)
    {
        Assert.Equal((off.Reason, off.Side, off.HoldsField, off.Pulses, off.EndMs), (on.Reason, on.Side, on.HoldsField, on.Pulses, on.EndMs));
        Assert.Equal(off.Formations.Length, on.Formations.Length);
        for (int i = 0; i < off.Formations.Length; i++)
        {
            var x = off.Formations[i]; var y = on.Formations[i];
            long d = y.FormationId == winnerId ? rise : 0;
            Assert.Equal(x with { MoraleEnd = x.MoraleEnd + d }, y);
        }
        Assert.NotEqual(off.ResultHash(), on.ResultHash());
    }

    /// <summary>TACTICAL, TURN-EXACT: the rule changes nothing until the instant G breaks (the same events, the same result in every
    /// other field); at that instant, the battle's end, A's morale is exactly floor(200 × 10 ÷ 60) = 33 higher, and B's (which never
    /// fought G) is unchanged. One rise row, at the end instant. With the rule off the log is empty.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Tactical_TheFormationThatBrokeItsEnemyRisesAtThatInstant_OthersDoNot(ulong seed)
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var offB = new TacticalBattle(Scenario(seed, 0), ai, ai);
        var off = TT.RunGuarded(offB);
        var onB = new TacticalBattle(Scenario(seed, 200), ai, ai);
        var on = TT.RunGuarded(onB);
        Assert.Equal(BattleEndReason.SideEliminated, on.Reason);
        Assert.Equal(0, on.HoldsField);
        Assert.Empty(offB.LocalWinRises);
        Assert.Equal(new[] { new LocalWinRise(on.EndMs, 1, 2, ExpectedRise) }, onB.LocalWinRises.ToArray());
        Assert.True(off.Formations.Single(f => f.FormationId == 1).MoraleEnd + ExpectedRise <= 1_000, "the rise is not hidden by the cap");
        Assert.True(on.Formations.Single(f => f.FormationId == 2).RoutedEscaped + on.Formations.Single(f => f.FormationId == 2).Surrendered > 0
                    || on.Formations.Single(f => f.FormationId == 2).ActiveAtEnd == 0);
        AssertOnlyTheWinnersMoraleDiffers(off, on, 1, ExpectedRise);                     // B (id 3) never fought G: unchanged
        Assert.Equal(0, on.Formations.Single(f => f.FormationId == 3).DamageDealt);
        Assert.Equal(offB.Entities().ToArray(), onB.Entities().ToArray());
    }

    /// <summary>AUTORESOLVER, TURN-EXACT: the same rule through the same shared implementation, with time in steps: the result differs
    /// only in A's end morale, by exactly 33, and the one rise row stands at the end instant (StartMs + step × StepMs).</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Auto_TheFormationThatBrokeItsEnemyRisesAtThatStep_OthersDoNot(ulong seed)
    {
        var c = new AutoCoefficients { DoctrineRetreatMorale = 0 };
        var offB = new AutoBattle(Scenario(seed, 0), c);
        var off = offB.Run();
        var onB = new AutoBattle(Scenario(seed, 200), c);
        var on = onB.Run();
        Assert.Equal(BattleEndReason.SideEliminated, on.Reason);
        Assert.Equal(0, on.HoldsField);
        Assert.Empty(offB.LocalWinRises);
        Assert.Equal(new[] { new LocalWinRise(on.EndMs, 1, 2, ExpectedRise) }, onB.LocalWinRises.ToArray());
        Assert.True(off.Formations.Single(f => f.FormationId == 1).MoraleEnd + ExpectedRise <= 1_000, "the rise is not hidden by the cap");
        AssertOnlyTheWinnersMoraleDiffers(off, on, 1, ExpectedRise);                     // B (id 3) never fought G: unchanged
        Assert.Equal(0, on.Formations.Single(f => f.FormationId == 3).DamageDealt);
    }

    /// <summary>The window is about the WINNER's blows, not the loser's: 60 archers (morale 800) holding their ground shoot 10 heavy
    /// infantry to a rout before it reaches them, so the infantry never dealt them damage; the archers rise by floor(200 × 10 ÷ 70) = 28
    /// at the instant the infantry breaks, in both resolvers. (A table that recorded who was hit instead of who hit would give no rise.)
    /// Lane D's archers scenario (MeetingAsymmetric); the infantry never withdraws (retreat morale 0).</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    public void BothResolvers_ArchersWhoseVolleysBrokeTheInfantry_RiseWithoutHavingBeenHit(ulong seed)
    {
        var setup = TacticalScenarios.MeetingAsymmetric(UnitProfiles.Archers, 60, UnitProfiles.HeavyInfantry, 10, seed);
        setup = setup with { Formations = new[] { setup.Formations[0] with { Morale = 800 }, setup.Formations[1] } };
        BattleSetup S(long c) => setup with { Rules = setup.Rules with { LocalWinMoraleRise = c } };
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var offB = new TacticalBattle(S(0), PassiveController.Instance, ai);
        var off = TT.RunGuarded(offB);
        var onB = new TacticalBattle(S(200), PassiveController.Instance, ai);
        var on = TT.RunGuarded(onB);
        Assert.Equal(0, on.Formations.Single(f => f.Side == 0).DamageTaken);           // the infantry never hit the archers
        Assert.Equal(BattleEndReason.SideEliminated, on.Reason);
        Assert.Equal(new[] { new LocalWinRise(on.EndMs, 1, 2, 28) }, onB.LocalWinRises.ToArray());
        AssertOnlyTheWinnersMoraleDiffers(off, on, 1, 28);
        var c = new AutoCoefficients { DoctrineRetreatMorale = 0 };
        var aOffB = new AutoBattle(S(0), c); var aOff = aOffB.Run();
        var aOnB = new AutoBattle(S(200), c); var aOn = aOnB.Run();
        Assert.Equal(0, aOn.Formations.Single(f => f.Side == 0).DamageTaken);
        Assert.Equal(BattleEndReason.SideEliminated, aOn.Reason);
        Assert.Equal(new[] { new LocalWinRise(aOn.EndMs, 1, 2, 28) }, aOnB.LocalWinRises.ToArray());
        AssertOnlyTheWinnersMoraleDiffers(aOff, aOn, 1, 28);
    }

    /// <summary>With the rule on, the tactical state hash carries the rule's own state (the hit table and the rise log), appended
    /// after the existing encoding: before any break, the rule-on and rule-off battles are in the same state in every other field yet
    /// hash differently, and two rule-on battles hash alike (determinism). With the rule off nothing is appended (the goldens).</summary>
    [Fact]
    public void Tactical_StateHash_CarriesTheRulesState_WhenOn()
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var off = new TacticalBattle(Scenario(1, 0), ai, ai);
        var on1 = new TacticalBattle(Scenario(1, 200), ai, ai);
        var on2 = new TacticalBattle(Scenario(1, 200), ai, ai);
        off.RunUntilPulse(2); on1.RunUntilPulse(2); on2.RunUntilPulse(2);
        Assert.False(off.IsOver || on1.IsOver);
        Assert.Empty(on1.LocalWinRises);                                                 // no break yet: the same battle so far
        Assert.Equal(off.Entities().ToArray(), on1.Entities().ToArray());
        Assert.Equal(off.Morale(1), on1.Morale(1));
        Assert.NotEqual(off.StateHashHex(), on1.StateHashHex());
        Assert.Equal(on1.StateHashHex(), on2.StateHashHex());
    }

    /// <summary>The rule at its default (0) is the exact identity in both resolvers: the same result hash as a setup that never names
    /// it, and no rows (the golden tests of both resolvers pin the rest: unchanged).</summary>
    [Fact]
    public void Default_IsTheIdentity_InBothResolvers()
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var named = Scenario(1, 0);
        var unnamed = named with { Rules = new TacticalRules() };
        var t1 = new TacticalBattle(named, ai, ai); var r1 = TT.RunGuarded(t1);
        var t2 = new TacticalBattle(unnamed, ai, ai); var r2 = TT.RunGuarded(t2);
        Assert.Equal(r2.ResultHash(), r1.ResultHash());
        Assert.Equal(r2.StateHash, r1.StateHash);
        Assert.Empty(t1.LocalWinRises);
        var c = new AutoCoefficients { DoctrineRetreatMorale = 0 };
        var a1 = new AutoBattle(named, c).Run();
        var a2 = new AutoBattle(unnamed, c).Run();
        Assert.Equal(a2.ResultHash(), a1.ResultHash());
        Assert.Equal(a2.StateHash, a1.StateHash);
    }

    /// <summary>[verification round 1, MOR-V1-06] The state hash carries the rule's own log in BOTH resolvers, at the battle's end too:
    /// the scenario above at c = 2,000 and c = 3,000 gives the winner A rises of floor(c × 10 ÷ 60) = 333 and 500, which the cap hides
    /// (A ends at 1,000 either way), so the two results are equal in every field and the two battles differ only in their rise logs;
    /// their state hashes differ. (Leaving the rule's state out of the AutoResolver's end state hash, lane mutant M24, had been judged
    /// equivalent; it changes the reported StateHash — MEASURED in the fix round — and survived the whole suite before this test.)</summary>
    [Fact]
    public void BothResolvers_TheStateHashCarriesTheRiseLog_WhenTheResultsAreEqual()
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var t2 = new TacticalBattle(Scenario(1, 2_000), ai, ai); var tr2 = TT.RunGuarded(t2);
        var t3 = new TacticalBattle(Scenario(1, 3_000), ai, ai); var tr3 = TT.RunGuarded(t3);
        Assert.Equal(new[] { new LocalWinRise(tr2.EndMs, 1, 2, 333) }, t2.LocalWinRises.ToArray());
        Assert.Equal(new[] { new LocalWinRise(tr3.EndMs, 1, 2, 500) }, t3.LocalWinRises.ToArray());
        Assert.Equal(1_000, tr2.Formations.Single(f => f.FormationId == 1).MoraleEnd);    // the cap hides the difference ...
        Assert.Equal(tr2.ResultHash(), tr3.ResultHash());                                // ... in every result field
        Assert.NotEqual(tr2.StateHash, tr3.StateHash);                                   // the state hash does not hide it
        var c = new AutoCoefficients { DoctrineRetreatMorale = 0 };
        var a2 = new AutoBattle(Scenario(1, 2_000), c); var ar2 = a2.Run();
        var a3 = new AutoBattle(Scenario(1, 3_000), c); var ar3 = a3.Run();
        Assert.Equal(new[] { new LocalWinRise(ar2.EndMs, 1, 2, 333) }, a2.LocalWinRises.ToArray());
        Assert.Equal(new[] { new LocalWinRise(ar3.EndMs, 1, 2, 500) }, a3.LocalWinRises.ToArray());
        Assert.Equal(1_000, ar2.Formations.Single(f => f.FormationId == 1).MoraleEnd);
        Assert.Equal(ar2.ResultHash(), ar3.ResultHash());
        Assert.NotEqual(ar2.StateHash, ar3.StateHash);
    }

    // ------------------------------------------------------------------ [verification round 1, MOR-V1-01] the window and the
    // withdrawal at the resolver level, in both resolvers, observed from outside the rule's code

    private const long PulseMs = 60_000;                                  // TacticalRules default: the window is one pulse
    private const long StepsPerPulse = 30;                                // PulseMs ÷ StepMs (2,000 ms): the AutoResolver's unit

    /// <summary>Every landed hit of a TACTICAL battle as (instant ms, attacker formation, target formation): each executed Damage event
    /// that lowered its target's hit points. The test drives the battle event by event through its public scheduler (what Advance
    /// does) and compares the struck entity's hit points before and after each Damage event; it never reads the engine's hit table, so
    /// the tests below check the window and the breaks independently of the rule's implementation.</summary>
    private static (BattleResult Result, List<(long T, long From, long To)> Hits) RunObservingHits(TacticalBattle b)
    {
        var hits = new List<(long, long, long)>();
        var initial = b.Entities();
        var ids = initial.Select(e => e.Id).ToArray();                       // ascending: the engine's entity order
        var forms = initial.Select(e => e.FormationId).ToArray();
        b.Start();
        while (!b.IsOver)
        {
            if (b.CurrentPulse > TT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD: battle still running — non-termination is a FAILURE");
            Assert.True(b.Scheduler.TryPeekKey(out var key), "event queue empty before termination");
            var before = key.Class == (byte)EventClass.Damage ? b.Entities() : null;
            var outcome = b.Scheduler.ExecuteNext(b, out var ev);
            if (before is null || outcome != TraceOutcome.Executed || ev.Kind != TacticalKinds.Damage) continue;
            int tg = Array.BinarySearch(ids, ev.Target.Id), src = Array.BinarySearch(ids, ev.Source.Id);
            if (b.Entities()[tg].Hp < before[tg].Hp) hits.Add((ev.Key.TimeMs, forms[src], forms[tg]));
        }
        return (b.Result!, hits);
    }

    /// <summary>Every landed hit of an AUTORESOLVER battle (RecordLog on) as (step, attacker formation, target formation): a step at which
    /// the attacker's dealt damage grew. All of a formation's damage in one step goes to its one recorded target. Read from the engine's
    /// per-step decision log, not from the rule's hit table.</summary>
    private static List<(long K, long From, long To)> AutoHits(AutoBattle b)
    {
        var hits = new List<(long, long, long)>();
        var dealt = new Dictionary<long, long>();                           // lookups only
        foreach (var x in b.Log)                                            // step order; formations in id order within a step
        {
            long prev = dealt.TryGetValue(x.FormationId, out long p) ? p : 0;
            if (x.DamageDealtMilli > prev) hits.Add((x.Step, x.FormationId, x.Target));
            dealt[x.FormationId] = x.DamageDealtMilli;
        }
        return hits;
    }

    /// <summary>TEST INPUT profile: archers that loose ONE volley (a fire interval of a million steps) at long range (12 cells).</summary>
    private static readonly UnitProfile OneVolleyArchers = UnitProfiles.Archers with { FireIntervalSteps = 1_000_000, RangeCells = 12 };

    /// <summary>The window scenario (TEST INPUT geometry). Side 0 holds a line (Defend): B, heavy infantry 50 (leadership 100), in front;
    /// A, ten one-volley archers, just behind; C, two snipers (lane N's profile: one shot per 5 steps), behind them. Side 1: G, heavy
    /// infantry 45, attacks the line and breaks under B's blows. A looses its one volley at G at the start, more than a pulse before G
    /// breaks, and stays in the fight; C keeps shooting G until shortly before the break.</summary>
    private static BattleSetup WindowScenario(ulong seed, long rise)
    {
        Vec2L b = Cell(20, 16), a = Cell(19, 16), c = Cell(17, 16), g = Cell(26, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { g }, new[] { b }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, b) { Posture = Posture.Defend, Leadership = 100 },
                new FormationSpec(2, 0, OneVolleyArchers, 10, a) { Posture = Posture.Defend },
                new FormationSpec(3, 0, DomainProfiles.Sniper, 2, c) { Posture = Posture.Defend },
                new FormationSpec(4, 1, UnitProfiles.HeavyInfantry, 45, g),
            },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = rise } };
    }

    // Rises at c = 200 when G (45) breaks: B (50) floor(9,000 ÷ 95) = 94; C (2) floor(9,000 ÷ 47) = 191; A (10) would get floor(9,000 ÷ 55) = 163.

    /// <summary>TACTICAL, TURN-EXACT, the window at the resolver level. Of the three side-0 formations still in the fight when G breaks,
    /// B struck G at that instant, C last struck it strictly before that instant but within one pulse (60,000 ms), and A's one volley
    /// lies more than a pulse back — all observed from outside the engine. Exactly B and C rise, by 94 and 191; A, whose rise would be
    /// 163, does not. The test fails if the window shrinks to the breaking instant (C loses its rise), grows (A gains one) or is read in
    /// another time unit. (A window of 0 ms or of 1,000 pulses each survived the whole suite before this test: MEASURED.)
    /// [Verification round 2, MOR-V2-02: "grows (A gains one)" overstated what this pins. A's volley lies about 1.8 pulses back
    /// (112,000 and 108,000 ms before the break for seeds 1 and 4: MEASURED), so this test accepts any window from 8,000 to 107,999 ms
    /// (a window of 58,000 or 90,000 ms survived the whole suite: MEASURED). The boundary itself, one pulse inclusive, is pinned by
    /// <see cref="Tactical_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot"/>.]</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(4UL)]
    public void Tactical_TheWindowIsOnePulseOfTheWinnersOwnBlows_InMilliseconds(ulong seed)
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var b = new TacticalBattle(WindowScenario(seed, 200), ai, ai);
        var (r, hits) = RunObservingHits(b);
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));               // G broke: its side has no one left in the fight
        long tb = r.EndMs;
        long[] Times(long from) => hits.Where(h => h.From == from && h.To == 4).Select(h => h.T).ToArray();
        Assert.Equal(tb, Times(1).Max());                                                     // B struck G at the breaking instant
        Assert.InRange(tb - Times(3).Max(), 1, PulseMs);                                      // C: strictly before it, within one pulse
        Assert.NotEmpty(Times(2));
        Assert.True(Times(2).All(t => tb - t > PulseMs), "A's one volley lies more than a pulse before the break");
        Assert.Equal(10, r.Formations.Single(f => f.FormationId == 2).ActiveAtEnd);         // A is in the fight at the break
        Assert.Equal(new[] { new LocalWinRise(tb, 1, 4, 94), new LocalWinRise(tb, 3, 4, 191) }, b.LocalWinRises.ToArray());
    }

    /// <summary>AUTORESOLVER, TURN-EXACT, the same window through the shared rule with time in STEPS (one pulse = 30 steps): B struck G
    /// at the breaking step, C last struck it 1 to 30 steps before, A's one volley lies more than 30 steps back (observed from the
    /// per-step log). Exactly B and C rise, by 94 and 191. (The window passed in milliseconds, 60,000 "steps", or as 0 steps each
    /// survived the whole suite before this test: MEASURED.)
    /// [Verification round 2, MOR-V2-02: A's volley lies 57 and 51 steps back for seeds 1 and 4 (MEASURED), so this test accepts any
    /// window from 2 to 50 steps (windows of 15, 29 and 45 steps survived the whole suite: MEASURED). The boundary itself, 30 steps
    /// inclusive, is pinned by <see cref="Auto_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot"/>.]</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(4UL)]
    public void Auto_TheWindowIsOnePulseOfTheWinnersOwnBlows_InSteps(ulong seed)
    {
        var b = new AutoBattle(WindowScenario(seed, 200), new AutoCoefficients { DoctrineRetreatMorale = 0 }) { RecordLog = true };
        var r = b.Run();
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));
        long kb = r.EndMs / 2_000;                                                            // StartMs 0: the break ends the battle at that step
        var hits = AutoHits(b);
        long[] Steps(long from) => hits.Where(h => h.From == from && h.To == 4).Select(h => h.K).ToArray();
        Assert.Equal(kb, Steps(1).Max());
        Assert.InRange(kb - Steps(3).Max(), 1, StepsPerPulse);
        Assert.NotEmpty(Steps(2));
        Assert.True(Steps(2).All(k => kb - k > StepsPerPulse), "A's one volley lies more than a pulse before the break");
        Assert.Equal(10, r.Formations.Single(f => f.FormationId == 2).ActiveAtEnd);
        Assert.Equal(new[] { new LocalWinRise(r.EndMs, 1, 4, 94), new LocalWinRise(r.EndMs, 3, 4, 191) }, b.LocalWinRises.ToArray());
    }

    /// <summary>The edge scenario (TEST INPUT geometry): A, heavy infantry 30 of side 0, two cells west of G, heavy infantry 20 of side 1
    /// standing in its own exit-edge column (East, column 47: both its ranks are on the edge). A attacks (the knowledge-only AI) and
    /// strikes G from 28 s on.</summary>
    private static BattleSetup EdgeScenario(ulong seed, long rise)
    {
        Vec2L a = Cell(45, 16), g = Cell(47, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { g }, new[] { a }),
            new[] { new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 30, a), new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, g) },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = rise } };
    }

    /// <summary>TACTICAL: an orderly withdrawal off the field is NOT a break; a rout is. (1) G, struck by A from 28 s on, is ordered to
    /// Retreat at 32 s (pulse 0, step 16): its men, all on their exit edge, leave the field in good order at that instant, A having
    /// struck G within the pulse before (observed). The battle ends there (G's side has no one left in the fight), no rise is given, and
    /// the rule-on result equals the rule-off result in every field. (2) The same battle without the order: G fights on and breaks under
    /// A's blows, and A rises by floor(200 × 20 ÷ (20 + 30)) = 80 at that instant — so the absence of a rise in (1) is the escape not
    /// counting as a break. (The rule judged after the instant's escapes counted (1) as a break and survived the whole suite before this
    /// test: MEASURED.)</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Tactical_AnOrderlyWithdrawalOffTheEdgeIsNotABreak_ARoutIs(ulong seed)
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        TacticalBattle Escaping(long c) => new(EdgeScenario(seed, c), ai, new ScriptedController().Add(0, new OrderRequest(2, OrderKind.Retreat, 16)));
        var off = TT.RunGuarded(Escaping(0));
        var onB = Escaping(200);
        var (on, hits) = RunObservingHits(onB);
        Assert.Equal((BattleEndReason.SideEliminated, 1, 32_000L), (on.Reason, on.Side, on.EndMs));
        var g = on.Formations.Single(f => f.FormationId == 2);
        Assert.Equal((0L, 0L, 0L), (g.ActiveAtEnd, g.RoutedEscaped, g.DisengagedEscaped));   // withdrawn in good order, not routed ...
        Assert.True(g.Survivors > 0);
        Assert.Equal(g.Survivors, g.Escaped);                                                 // ... every survivor left the field
        Assert.InRange(on.EndMs - hits.Where(h => h.From == 1 && h.To == 2).Max(h => h.T), 0, PulseMs);   // A struck G within the pulse
        Assert.Empty(onB.LocalWinRises);
        Assert.Equal(off.ResultHash(), on.ResultHash());
        Assert.Equal(off.Formations, on.Formations);
        var standB = new TacticalBattle(EdgeScenario(seed, 200), ai, PassiveController.Instance);
        var stand = TT.RunGuarded(standB);
        Assert.Equal((BattleEndReason.SideEliminated, 1), (stand.Reason, stand.Side));      // without the order G breaks ...
        Assert.Equal(new[] { new LocalWinRise(stand.EndMs, 1, 2, 80) }, standB.LocalWinRises.ToArray());   // ... and A rises
    }

    /// <summary>TEST INPUT profile: heavy infantry with four times the hit points (it dies slowly enough to break only in the pulse
    /// after F stops striking it; here it also outlasts the blows of the pulse before its withdrawal).</summary>
    private static readonly UnitProfile Stalwart = UnitProfiles.HeavyInfantry with { Integrity = 200 };

    /// <summary>The AutoResolver's edge scenario (TEST INPUT geometry): A, heavy infantry 20 of side 0, three cells west of G; G, twenty
    /// stalwart heavy infantry of side 1 at morale 450 (the doctrine's withdrawal threshold) with leadership 100 (50 morale a death),
    /// holding (Defend) on its own exit edge (East, column 47); H, heavy infantry 10 of side 1, holding far along the battle axis, so
    /// that the battle goes on after G has left.</summary>
    private static BattleSetup AutoEdgeScenario(ulong seed, long rise)
        => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { Cell(47, 16) }, new[] { Cell(44, 16) }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 20, Cell(44, 16)),
                new FormationSpec(2, 1, Stalwart, 20, Cell(47, 16)) { Morale = 450, Leadership = 100, Posture = Posture.Defend },
                new FormationSpec(3, 1, UnitProfiles.HeavyInfantry, 10, Cell(47, 6)) { Posture = Posture.Defend },
            },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = rise } };

    /// <summary>AUTORESOLVER: an orderly withdrawal off the field is not a break, as in the tactical engine. A strikes G up to step 29
    /// (observed); G, below 450 after 1 to 3 deaths but above the rout threshold, withdraws at the start of pulse 1 (step 30) and,
    /// standing on its exit edge, is off the field one step later (DERIVED: AutoBattle.ExitSteps = max(1, ⌈0 ÷ pace⌉) = 1 on an edge
    /// cell) — within one pulse of A's last blow, with A still in the fight. No rise is ever logged for G; the battle goes on, and A's
    /// one rise is for H's break at the end, floor(200 × 10 ÷ 30) = 66. (Judging the breaks of a step against the previous step's field
    /// counts G's escape as a break: a fix-round mutant, killed by this test.)</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void Auto_AnOrderlyWithdrawalOffTheFieldIsNotABreak(ulong seed)
    {
        var coefficients = new AutoCoefficients();
        Assert.Equal(450, coefficients.DoctrineRetreatMorale);
        var b = new AutoBattle(AutoEdgeScenario(seed, 200), coefficients) { RecordLog = true };
        var r = b.Run();
        var hits = AutoHits(b);
        Assert.Equal(StepsPerPulse - 1, hits.Where(h => h.From == 1 && h.To == 2).Max(h => h.K));   // A's last blow on G: step 29
        var g = r.Formations.Single(x => x.FormationId == 2);
        Assert.Equal((0L, 0L, 0L), (g.ActiveAtEnd, g.RoutedEscaped, g.DisengagedEscaped));         // withdrawn in good order, not routed
        Assert.Equal(g.Survivors, g.Escaped);
        Assert.InRange(g.Killed, 1, 3);
        Assert.True(r.EndMs / 2_000 > StepsPerPulse + 1, "the battle goes on after G has left the field");
        Assert.Contains(hits, h => h.From == 1 && h.To == 3);                                       // A fights H afterwards
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));
        Assert.DoesNotContain(b.LocalWinRises, x => x.BrokenFormationId == 2);
        Assert.Equal(new[] { new LocalWinRise(r.EndMs, 1, 3, 66) }, b.LocalWinRises.ToArray());
    }

    /// <summary>The withdrawal scenario (TEST INPUT geometry): F and B, heavy infantry 50 each of side 0, side by side; G, ten stalwart
    /// heavy infantry of side 1 at morale 650 (its second death routs it), seven cells east. F's start morale is an input.</summary>
    private static BattleSetup WithdrawalScenario(ulong seed, long rise, long fMorale)
    {
        Vec2L f = Cell(24, 14), b = Cell(24, 18), g = Cell(31, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { g }, new[] { Cell(24, 16) }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, f) { Morale = fMorale },
                new FormationSpec(2, 0, UnitProfiles.HeavyInfantry, 50, b),
                new FormationSpec(3, 1, Stalwart, 10, g) { Morale = 650 },
            },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = rise } };
    }

    /// <summary>AUTORESOLVER: a formation WITHDRAWING in good order is still in the fight (on the field, not routed), as the tactical
    /// engine's retreating men are (Active). F starts at 450, the doctrine's withdrawal threshold (DoctrineRetreatMorale 450): it strikes
    /// G and loses men (40 morale each), so at the start of pulse 1 (step 30) it is below 450 and withdraws; its last blow on G landed at
    /// step 29 and it strikes no more (observed). G breaks under B's blows 1 to 30 steps after F's last blow, while F is still on its way
    /// to its exit edge (about 120 steps from column 24 at catch-up pace: DERIVED). F and B both rise by floor(200 × 10 ÷ 60) = 33, and F
    /// ends at exactly 450 − 40 × its dead + 33. (Treating a withdrawing formation as out of the fight, or a window of 0 steps, each drop
    /// F's rise; each survived the whole suite before this test: MEASURED.)</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void Auto_AFormationWithdrawingInGoodOrderIsStillInTheFight_ItRisesWhenTheEnemyItStruckBreaks(ulong seed)
    {
        var coefficients = new AutoCoefficients();
        Assert.Equal(450, coefficients.DoctrineRetreatMorale);
        var b = new AutoBattle(WithdrawalScenario(seed, 200, 450), coefficients) { RecordLog = true };
        var r = b.Run();
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));
        long kb = r.EndMs / 2_000;
        long kF = AutoHits(b).Where(h => h.From == 1 && h.To == 3).Max(h => h.K);
        Assert.Equal(StepsPerPulse - 1, kF);                                                  // F's last blow: the last step of pulse 0
        Assert.True(b.Log.Where(x => x.FormationId == 1 && x.Step >= StepsPerPulse).All(x => x.Mode == 0), "F withdraws: it strikes no more");
        Assert.InRange(kb - kF, 1, StepsPerPulse);                                            // G breaks within one pulse of it
        var f = r.Formations.Single(x => x.FormationId == 1);
        Assert.Equal((0L, 0L, 0L), (f.ActiveAtEnd, f.RoutedEscaped, f.DisengagedEscaped));   // withdrawn in good order, not routed
        Assert.Equal(f.Survivors, f.Escaped);
        Assert.InRange(f.Killed, 1, 3);                                                        // below 450, never below the rout threshold
        Assert.Equal(450 - 40 * f.Killed + 33, f.MoraleEnd);
        Assert.Equal(new[] { new LocalWinRise(r.EndMs, 1, 3, 33), new LocalWinRise(r.EndMs, 2, 3, 33) }, b.LocalWinRises.ToArray());
    }

    /// <summary>TACTICAL, the same property: a formation RETREATING in good order is still in the fight (its men are Active) and rises when
    /// the enemy it struck within the last pulse breaks. F (morale 1000) and B attack G; F is ordered to Retreat at 62 s (pulse 1, step
    /// 1), its last blow on G having landed at 60 s (observed); G breaks under B's blows within the pulse after that blow, while F's men
    /// are still on the field, retreating. F and B both rise by 33.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void Tactical_AFormationRetreatingInGoodOrderIsStillInTheFight_ItRisesWhenTheEnemyItStruckBreaks(ulong seed)
    {
        var side0 = new CompositeController(new KnowledgeAi { RetreatMorale = 0 }, new ScriptedController().Add(1, new OrderRequest(1, OrderKind.Retreat, 1)));
        var b = new TacticalBattle(WithdrawalScenario(seed, 200, 1_000), side0, new KnowledgeAi { RetreatMorale = 0 });
        var (r, hits) = RunObservingHits(b);
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));
        long tF = hits.Where(h => h.From == 1 && h.To == 3).Max(h => h.T);
        Assert.Equal(60_000, tF);                                                             // F's last blow, before its Retreat at 62 s
        Assert.InRange(r.EndMs - tF, 1, PulseMs);
        Assert.Equal(OrderKind.Retreat, b.Directive(1));
        var f = r.Formations.Single(x => x.FormationId == 1);
        Assert.True(f.ActiveAtEnd > 0, "F's men are on the field, retreating");
        Assert.Equal(0, f.RoutedEscaped);
        Assert.Equal(new[] { new LocalWinRise(r.EndMs, 1, 3, 33), new LocalWinRise(r.EndMs, 2, 3, 33) }, b.LocalWinRises.ToArray());
    }

    // ------------------------------------------------------------------ [verification round 2, MOR-V2-01] a break needs the formation
    // to have been in the fight BEFORE the instant: a formation already out of it does not break again

    /// <summary>TIE-DENSE, the "before" half of a break. Six formations at one instant: W (side 0, 50, morale 500) and X (side 0, 40,
    /// morale 600) are in the fight before and after; R (side 1, 40) routed at an earlier instant and E (side 1, 30) left the field in
    /// good order earlier — both out of the fight before this instant and after it; Y (side 1, 20) enters the fight at this instant
    /// (out before, in after: a landing, say); G (side 1, 50) breaks now (in before, out after). W struck R 10 s ago, E exactly one
    /// pulse ago, Y 5 s ago and G now; X struck R now (routing men can still be struck: lane P's combined-arms battle, below). Only G
    /// broke, so W gains Rise(200, 50, 50) = 100 → 600 and nothing else changes, for all 720 orders. (Judging a break on "out of the
    /// fight now" alone gave W 100 + 88 + 75 = 263 and X 100 here, and survived the whole suite before this test: MEASURED, mutant B1.)</summary>
    [Fact]
    public void Apply_TieDense_AFormationAlreadyOutOfTheFight_DoesNotBreakAgain_NorDoesOneEnteringIt_WhateverTheOrder()
    {
        const long now = 600_000;
        long[] ids = { 1, 2, 11, 12, 13, 14 };
        int[] sides = { 0, 0, 1, 1, 1, 1 };
        long[] starts = { 50, 40, 40, 30, 50, 20 };
        bool[] before = { true, true, false, false, true, false };
        bool[] after = { true, true, false, false, false, true };
        long[] morale = { 500, 600, 150, 200, 280, 700 };
        // W hit R (10 s ago), E (one pulse ago: in the window), G (now), Y (5 s ago); X hit R (now).
        var hits = new (int W, int G, long At)[] { (0, 2, now - 10_000), (0, 3, now - 60_000), (0, 4, now), (0, 5, now - 5_000), (1, 2, now) };
        var expected = new Dictionary<long, long> { [1] = 600, [2] = 600, [11] = 150, [12] = 200, [13] = 280, [14] = 700 };
        int count = 0;
        foreach (var order in Permutations(6))
        {
            int n = ids.Length;
            var pIds = new long[n]; var pSides = new int[n]; var pStarts = new long[n]; var pBefore = new bool[n]; var pNow = new bool[n]; var pMorale = new long[n];
            var at = new int[n];
            for (int i = 0; i < n; i++)
            {
                int f = order[i];
                pIds[i] = ids[f]; pSides[i] = sides[f]; pStarts[i] = starts[f]; pBefore[i] = before[f]; pNow[i] = after[f]; pMorale[i] = morale[f];
                at[f] = i;
            }
            var hit = new long[n * n];
            Array.Fill(hit, long.MinValue);
            foreach (var (w, g, t) in hits) hit[at[w] * n + at[g]] = t;
            var log = new List<LocalWinRise>();
            Assert.True(LocalWinMorale.Apply(200, 1_000, 60_000, now, now, pIds, pSides, pStarts, pBefore, pNow, hit, pMorale, log));
            for (int i = 0; i < n; i++) Assert.Equal(expected[pIds[i]], pMorale[i]);
            Assert.Equal(new[] { new LocalWinRise(now, 1, 13, 100) }, log.ToArray());
            count++;
        }
        Assert.Equal(720, count);
    }

    /// <summary>Lane P's combined-arms battle at seed 1 (side 0: heavy infantry 50, id 1, and archers 60, id 2; side 1: heavy infantry
    /// 50, id 3, and archers 60, id 4), built and controlled as the morale-parity bench runs lane P's tuning battery, at c = 200.</summary>
    private static BattleSetup CombinedArmsSeed1()
    {
        var s = ParityBatteryP.Build(PScenario.CombinedArms, 1);
        return s with { Rules = s.Rules with { LocalWinMoraleRise = 200 } };
    }

    /// <summary>BOTH RESOLVERS, a formation that broke EARLIER is not credited again at a later break. In lane P's combined-arms battle
    /// (seed 1, c = 200) both heavy-infantry formations break at one instant t1 (1 under the volleys of side 1's archers, 4; 3 under
    /// side 0's archers, 2), and side 0's archers (2) break under 4's volleys at the later instant t2, which ends the battle. Between
    /// the two, 4 struck the routed formation 1 within one pulse before t2 (observed from outside the rule's code), 1 was out of the
    /// fight before t2 (observed), and 4 is in the fight at t2 (it is credited for 2 then). The rows are exactly one per (winner,
    /// broken) pair: 4 ← 1 (+90) and 2 ← 3 (+90) at t1, 4 ← 2 (+100) at t2. A break judged on "out of the fight now" alone re-credits
    /// 4 for 1 at t2 in both resolvers (mutant B1, which survived the whole suite before this test: MEASURED; on lane P's tuning battery
    /// it added 79 re-credits in the AutoResolver and 128 in the TacticalResolver's two multi-formation scenarios: MEASURED).</summary>
    [Fact]
    public void BothResolvers_AFormationThatBrokeEarlier_IsNotCreditedAgainAtALaterBreak_LanePCombinedArms()
    {
        // TacticalResolver (KnowledgeAi on both sides, as lane P's battery).
        var tb = new TacticalBattle(CombinedArmsSeed1(), new KnowledgeAi(), new KnowledgeAi());
        var (r, hits) = RunObservingHits(tb);
        const long t1 = 160_000, t2 = 202_000;
        Assert.Equal((BattleEndReason.SideEliminated, 0, t2), (r.Reason, r.Side, r.EndMs));
        Assert.Equal(new[] { new LocalWinRise(t1, 4, 1, 90), new LocalWinRise(t1, 2, 3, 90), new LocalWinRise(t2, 4, 2, 100) },
                     tb.LocalWinRises.ToArray());
        long last41 = hits.Where(h => h.From == 4 && h.To == 1 && h.T <= t2).Max(h => h.T);
        Assert.True(last41 > t1, "4 kept striking 1's routing men after 1 broke");
        Assert.InRange(t2 - last41, 0, PulseMs);                                              // ... within one pulse before t2
        var probe = new TacticalBattle(CombinedArmsSeed1(), new KnowledgeAi(), new KnowledgeAi());   // the same battle, stopped before t2
        probe.Start();
        while (probe.Scheduler.TryPeekKey(out var key) && key.TimeMs < t2) probe.Scheduler.ExecuteNext(probe, out _);
        Assert.False(probe.IsOver);
        Assert.DoesNotContain(probe.Entities(), e => e.FormationId == 1 && e.State == EntityState.Active);   // 1 out of the fight
        Assert.Contains(probe.Entities(), e => e.FormationId == 4 && e.State == EntityState.Active);
        AssertOneRowPerPair(tb.LocalWinRises);

        // AutoResolver (model 2 defaults, as lane P's battery); time in steps of 2,000 ms.
        var ab = new AutoBattle(CombinedArmsSeed1(), new AutoCoefficients()) { RecordLog = true };
        var ar = ab.Run();
        const long k1 = 75, k2 = 86;
        Assert.Equal((BattleEndReason.SideEliminated, 0, k2 * 2_000), (ar.Reason, ar.Side, ar.EndMs));
        Assert.Equal(new[] { new LocalWinRise(k1 * 2_000, 4, 1, 90), new LocalWinRise(k1 * 2_000, 2, 3, 90), new LocalWinRise(k2 * 2_000, 4, 2, 100) },
                     ab.LocalWinRises.ToArray());
        long lastK41 = AutoHits(ab).Where(h => h.From == 4 && h.To == 1).Max(h => h.K);
        Assert.InRange(k2 - lastK41, 0, StepsPerPulse);                                       // 4 struck 1 within one pulse before k2
        var rules = new TacticalRules();
        Assert.True(ab.Log.Single(x => x.Step == k1 && x.FormationId == 1).Morale < rules.RoutThreshold, "1 routed at k1 (a rout is final)");
        Assert.True(ab.Log.Where(x => x.FormationId == 1 && x.Step > k1).All(x => x.Mode == 0), "1 strikes no more after k1");
        AssertOneRowPerPair(ab.LocalWinRises);
    }

    private static void AssertOneRowPerPair(IReadOnlyList<LocalWinRise> rows)
        => Assert.Equal(rows.Count, rows.Select(x => (x.FormationId, x.BrokenFormationId)).Distinct().Count());

    // ------------------------------------------------------------------ [verification round 2, MOR-V2-02] the window's boundary at the
    // resolver level, in both resolvers: a blow exactly one pulse before the break counts, one step more does not

    /// <summary>The boundary scenario (TEST INPUT geometry and profile). Side 0 holds a line (Defend): B, heavy infantry 50 (leadership
    /// 100), in front; A, ten archers that loose a volley every <paramref name="interval"/> steps (TEST INPUT fire interval) with a range
    /// of <paramref name="range"/> cells, just behind. Side 1: G, heavy infantry <paramref name="gCount"/>, attacks the line and breaks
    /// under B's blows. A holds and stays in the fight throughout; its last volley lands some time before G breaks. The battle's pulse
    /// length is an input (the default 60,000 ms, or a TEST INPUT 30,000 ms: the window is one pulse of the battle's own length).</summary>
    private static BattleSetup VolleyScenario(ulong seed, long rise, int interval, int gCount, long range, long pulseMs = 60_000)
    {
        var archers = UnitProfiles.Archers with { FireIntervalSteps = interval, RangeCells = range };
        Vec2L b = Cell(20, 16), a = Cell(19, 16), g = Cell(26, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { g }, new[] { b }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, b) { Posture = Posture.Defend, Leadership = 100 },
                new FormationSpec(2, 0, archers, 10, a) { Posture = Posture.Defend },
                new FormationSpec(4, 1, UnitProfiles.HeavyInfantry, gCount, g),
            },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = rise, PulseMs = pulseMs } };
    }

    /// <summary>TACTICAL, TURN-EXACT, the window's boundary. Default pulse (60,000 ms): archers firing every 34 steps (68 s), range 8
    /// cells, against 46 heavy infantry. Seed 13: A's last volley lands EXACTLY one pulse (60,000 ms) before G breaks, and A rises with B
    /// (Rise(200, 46, 10) = 164; B Rise(200, 46, 50) = 95); seed 12: one step more (62,000 ms), and only B rises. A 30,000 ms pulse
    /// (TEST INPUT): archers every 18 steps, range 8, against 30; seed 2: exactly one pulse (30,000 ms) before the break, A rises
    /// (Rise(200, 30, 10) = 150; B 75); seed 4: 32,000 ms, only B. In every battle A is in the fight at the break (all ten Active, none
    /// routed) and B struck G at the breaking instant; every blow is observed from outside the rule's code. Hits land on the engine's
    /// 2,000 ms steps (MEASURED: every observed instant), so the pairs pin the window to one pulse of the battle's own length,
    /// inclusive, at step granularity. (Windows of 58,000 and 90,000 ms each survived the whole suite before this test: MEASURED,
    /// mutants W3 and W2; a window fixed at the default pulse instead of the battle's own is killed by the 30,000 ms pair.)</summary>
    [Theory]
    [InlineData(13UL, 60_000L, 34, 46, 60_000L, true, 95L, 164L)]
    [InlineData(12UL, 60_000L, 34, 46, 62_000L, false, 95L, 164L)]
    [InlineData(2UL, 30_000L, 18, 30, 30_000L, true, 75L, 150L)]
    [InlineData(4UL, 30_000L, 18, 30, 32_000L, false, 75L, 150L)]
    public void Tactical_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot(
        ulong seed, long pulseMs, int interval, int gCount, long gap, bool rises, long riseB, long riseA)
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var b = new TacticalBattle(VolleyScenario(seed, 200, interval, gCount, 8, pulseMs), ai, ai);
        var (r, hits) = RunObservingHits(b);
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));               // G broke and the battle ended there
        long tb = r.EndMs;
        Assert.Equal(tb, hits.Where(h => h.From == 1 && h.To == 4).Max(h => h.T));          // B struck G at the breaking instant
        Assert.Equal(gap, tb - hits.Where(h => h.From == 2 && h.To == 4).Max(h => h.T));    // A's last volley: exactly gap before it
        Assert.All(hits, h => Assert.Equal(0, h.T % 2_000));
        var a = r.Formations.Single(f => f.FormationId == 2);
        Assert.Equal((10L, 0L), (a.ActiveAtEnd, a.RoutedEscaped));                          // A in the fight at the break
        var bf = r.Formations.Single(f => f.FormationId == 1);
        Assert.True(bf.ActiveAtEnd > 0 && bf.RoutedEscaped == 0, "B in the fight at the break");
        var expected = rises
            ? new[] { new LocalWinRise(tb, 1, 4, riseB), new LocalWinRise(tb, 2, 4, riseA) }
            : new[] { new LocalWinRise(tb, 1, 4, riseB) };
        Assert.Equal(expected, b.LocalWinRises.ToArray());
    }

    /// <summary>AUTORESOLVER, TURN-EXACT, the window's boundary in STEPS. Default pulse (30 steps): archers firing every 32 steps, range 4
    /// cells (their one volley lands at step 23 in both battles), against 30 heavy infantry. Seed 5: the volley lies EXACTLY 30 steps
    /// before the break, and A rises with B (Rise(200, 30, 10) = 150; B Rise(200, 30, 50) = 75); seed 2: 31 steps, and only B rises. A
    /// 30,000 ms pulse (TEST INPUT; 15 steps): archers every 17 steps, range 8 (volleys at steps 0, 17 and 34); seed 5: the last volley
    /// lies exactly 15 steps before the break, A rises; seed 3: 16 steps, only B. A is in the fight at the break (all ten Active, none
    /// routed) and B struck G at the breaking step; the steps are read from the per-step log. (Windows of 15, 29 and 45 steps each
    /// survived the whole suite before this test: MEASURED, mutants W4, W6 and W5; a window fixed at the default pulse's 30 steps is
    /// killed by the 15-step pair.)</summary>
    [Theory]
    [InlineData(5UL, 60_000L, 32, 4L, 30L, true)]
    [InlineData(2UL, 60_000L, 32, 4L, 31L, false)]
    [InlineData(5UL, 30_000L, 17, 8L, 15L, true)]
    [InlineData(3UL, 30_000L, 17, 8L, 16L, false)]
    public void Auto_TheWindowsBoundary_ABlowExactlyOnePulseBeforeTheBreakCounts_OneStepMoreDoesNot(
        ulong seed, long pulseMs, int interval, long range, long gap, bool rises)
    {
        var b = new AutoBattle(VolleyScenario(seed, 200, interval, 30, range, pulseMs), new AutoCoefficients { DoctrineRetreatMorale = 0 }) { RecordLog = true };
        var r = b.Run();
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));
        long kb = r.EndMs / 2_000;                                                            // StartMs 0: the break ends the battle at that step
        var hits = AutoHits(b);
        Assert.Equal(kb, hits.Where(h => h.From == 1 && h.To == 4).Max(h => h.K));
        var volleys = hits.Where(h => h.From == 2 && h.To == 4).Select(h => h.K).ToArray();
        Assert.NotEmpty(volleys);
        Assert.Equal(gap, kb - volleys.Max());                                                // A's last volley: exactly gap steps before
        var a = r.Formations.Single(f => f.FormationId == 2);
        Assert.Equal((10L, 0L), (a.ActiveAtEnd, a.RoutedEscaped));
        var bf = r.Formations.Single(f => f.FormationId == 1);
        Assert.True(bf.ActiveAtEnd > 0 && bf.RoutedEscaped == 0, "B in the fight at the break");
        var expected = rises
            ? new[] { new LocalWinRise(r.EndMs, 1, 4, 75), new LocalWinRise(r.EndMs, 2, 4, 150) }
            : new[] { new LocalWinRise(r.EndMs, 1, 4, 75) };
        Assert.Equal(expected, b.LocalWinRises.ToArray());
    }

    // ------------------------------------------------------------------ [verification round 2, MOR-V2-03] a land formation's crew-served
    // (domain) weapons count as dealing damage (TacticalResolver; the AutoResolver refuses such battles)

    /// <summary>Every landed hit of a TACTICAL battle on a LAND entity, by either damage path: the Damage event (melee and missiles) and
    /// lane N's DomainHit of kind 0 (a crew-served weapon's effect on a land entity), as (instant ms, attacker formation, target
    /// formation, event kind). Observed as in <see cref="RunObservingHits"/>: hit points before and after each executed event.</summary>
    private static (BattleResult Result, List<(long T, long From, long To, int Kind)> Hits) RunObservingLandHitsByPath(TacticalBattle b)
    {
        var hits = new List<(long, long, long, int)>();
        var initial = b.Entities();
        var ids = initial.Select(e => e.Id).ToArray();
        var forms = initial.Select(e => e.FormationId).ToArray();
        b.Start();
        while (!b.IsOver)
        {
            if (b.CurrentPulse > TT.HarnessMaxPulses) Assert.Fail("HARNESS GUARD: battle still running — non-termination is a FAILURE");
            Assert.True(b.Scheduler.TryPeekKey(out var key), "event queue empty before termination");
            var before = key.Class == (byte)EventClass.Damage ? b.Entities() : null;
            var outcome = b.Scheduler.ExecuteNext(b, out var ev);
            if (before is null || outcome != TraceOutcome.Executed) continue;
            if (ev.Kind != TacticalKinds.Damage && !(ev.Kind == DomainKinds.DomainHit && ev.A == 0)) continue;
            int tg = Array.BinarySearch(ids, ev.Target.Id), src = Array.BinarySearch(ids, ev.Source.Id);
            if (tg < 0 || src < 0) continue;                                                // land target, land source only
            if (b.Entities()[tg].Hp < before[tg].Hp) hits.Add((ev.Key.TimeMs, forms[src], forms[tg], ev.Kind));
        }
        return (b.Result!, hits);
    }

    /// <summary>The guns scenario (TEST INPUT geometry): side 0, <paramref name="guns"/> gun teams (lane N's gun crew with field guns,
    /// Defend, morale 700) at column 4 and, when <paramref name="infantry"/> &gt; 0, heavy infantry holding (Defend) at column 20; side 1,
    /// <paramref name="enemy"/> heavy infantry attacking from column <paramref name="enemyCol"/>.</summary>
    private static BattleSetup GunsScenario(ulong seed, long rise, int guns, int infantry, int enemy, long enemyCol)
    {
        var forms = new List<FormationSpec>
        {
            new(1, 0, DomainProfiles.GunCrew, guns, Cell(4, 16)) { Posture = Posture.Defend, Morale = 700, Weapons = new[] { DomainProfiles.FieldGun } },
        };
        if (infantry > 0) forms.Add(new FormationSpec(2, 0, UnitProfiles.HeavyInfantry, infantry, Cell(20, 16)) { Posture = Posture.Defend });
        forms.Add(new FormationSpec(3, 1, UnitProfiles.HeavyInfantry, enemy, Cell(enemyCol, 16)));
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { Cell(enemyCol, 16) }, new[] { Cell(infantry > 0 ? 20 : 4, 16) }), forms.ToArray(), seed)
            { Rules = new TacticalRules { LocalWinMoraleRise = rise } };
    }

    /// <summary>TACTICAL: six gun teams shell ten attacking heavy infantry into breaking before they reach the guns. Every blow on the
    /// infantry comes through the crew-served path (lane N's DomainHit kind 0; none through the Damage event) and the infantry dealt no
    /// damage at all (observed). The guns, still in the fight, rise by Rise(200, 10, 6) = 125 at the breaking instant: the rule-on
    /// result equals the rule-off result in every field but the guns' end morale, +125 exactly. Before the fix the guns got no rise
    /// (the hit table was written by the Damage event only: MEASURED, 12 of 12 such battles with no row). At c = 0 the guns' battle is
    /// the exact identity (nothing recorded). The AutoResolver refuses the battle (no auto model of crew-served weapons), so the
    /// tactical-only path opens no parity gap (D-079 D11).</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Tactical_GunsThatBreakTheInfantry_Rise_TheirCrewServedWeaponsDealtTheDamage(ulong seed)
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var offB = new TacticalBattle(GunsScenario(seed, 0, 6, 0, 10, 40), ai, ai);
        var off = TT.RunGuarded(offB);
        var onB = new TacticalBattle(GunsScenario(seed, 200, 6, 0, 10, 40), ai, ai);
        var (on, hits) = RunObservingLandHitsByPath(onB);
        Assert.Equal((BattleEndReason.SideEliminated, 1), (on.Reason, on.Side));
        var onGuns = hits.Where(h => h.From == 1 && h.To == 3).ToArray();
        Assert.NotEmpty(onGuns);
        Assert.All(onGuns, h => Assert.Equal(DomainKinds.DomainHit, h.Kind));               // the crew-served path only
        Assert.InRange(on.EndMs - onGuns.Max(h => h.T), 0, PulseMs);
        Assert.Equal(0, on.Formations.Single(f => f.FormationId == 3).DamageDealt);         // the infantry never reached the guns
        Assert.Equal(6, on.Formations.Single(f => f.FormationId == 1).ActiveAtEnd);         // the guns are in the fight at the break
        Assert.Empty(offB.LocalWinRises);
        Assert.Equal(new[] { new LocalWinRise(on.EndMs, 1, 3, 125) }, onB.LocalWinRises.ToArray());
        Assert.True(off.Formations.Single(f => f.FormationId == 1).MoraleEnd + 125 <= 1_000, "the rise is not hidden by the cap");
        AssertOnlyTheWinnersMoraleDiffers(off, on, 1, 125);
        var unnamed = GunsScenario(seed, 0, 6, 0, 10, 40) with { Rules = new TacticalRules() };
        var r0 = TT.RunGuarded(new TacticalBattle(unnamed, ai, ai));
        Assert.Equal((r0.ResultHash(), r0.StateHash), (off.ResultHash(), off.StateHash));   // c = 0: the identity
        Assert.Throws<NotSupportedException>(() => new AutoBattle(GunsScenario(seed, 200, 6, 0, 10, 40), new AutoCoefficients()).Run());
    }

    /// <summary>TACTICAL: one gun team and thirty heavy infantry break fifty attacking heavy infantry TOGETHER. The guns' last blow on
    /// the enemy (crew-served path) lies 46 s (seed 3) or 44 s (seed 4) before the break, within the pulse, and the infantry struck at
    /// the breaking instant (observed); both are in the fight at the break. Both rise: the guns by Rise(200, 50, 1) = 196, the infantry
    /// by Rise(200, 50, 30) = 125. (Before the fix only the infantry rose: the shared break credited the Damage event's blows only.)</summary>
    [Theory]
    [InlineData(3UL, 46_000L)]
    [InlineData(4UL, 44_000L)]
    public void Tactical_GunsAndInfantryThatBreakTheEnemyTogether_BothRise(ulong seed, long gunsGap)
    {
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        var b = new TacticalBattle(GunsScenario(seed, 200, 1, 30, 50, 23), ai, ai);
        var (r, hits) = RunObservingLandHitsByPath(b);
        Assert.Equal((BattleEndReason.SideEliminated, 1), (r.Reason, r.Side));
        long tb = r.EndMs;
        var guns = hits.Where(h => h.From == 1 && h.To == 3).ToArray();
        Assert.All(guns, h => Assert.Equal(DomainKinds.DomainHit, h.Kind));
        Assert.Equal(gunsGap, tb - guns.Max(h => h.T));                                      // within the pulse, not at the instant
        var inf = hits.Where(h => h.From == 2 && h.To == 3).ToArray();
        Assert.All(inf, h => Assert.Equal(TacticalKinds.Damage, h.Kind));
        Assert.Equal(tb, inf.Max(h => h.T));                                                  // the infantry struck at the breaking instant
        Assert.Equal(1, r.Formations.Single(f => f.FormationId == 1).ActiveAtEnd);
        var fi = r.Formations.Single(f => f.FormationId == 2);
        Assert.True(fi.ActiveAtEnd > 0 && fi.RoutedEscaped == 0, "the infantry is in the fight at the break");
        Assert.Equal(new[] { new LocalWinRise(tb, 1, 3, 196), new LocalWinRise(tb, 2, 3, 125) }, b.LocalWinRises.ToArray());
    }
}
