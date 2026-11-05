using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>Snipers (DIR5 §2.15): independent small formations, cap 2, personal concealment, long range, low fire rate.</summary>
public class SniperTests
{
    private static BattleSetup Duel(UnitProfile sniper, int n, ulong seed, TacticalRules? rules = null)
        => new(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
               DefaultSides(obj1: new[] { Cell(10, 16) }),                                // the infantry advances on the snipers
               new[]
               {
                   new FormationSpec(1, 0, sniper, n, Cell(10, 16)),
                   new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 20, Cell(36, 16)),   // 520 m away: beyond its 240 m sight
               }, seed) { Rules = rules ?? new TacticalRules() };

    [Fact]
    public void SniperFormationIsCappedAtTwo()
    {
        var ex = Assert.Throws<ArgumentException>(() => new TacticalBattle(Duel(DomainProfiles.Sniper, 3, 1), PassiveController.Instance, PassiveController.Instance));
        Assert.Contains("Snipers", ex.Message);
        _ = new TacticalBattle(Duel(DomainProfiles.Sniper, 2, 1), PassiveController.Instance, PassiveController.Instance);
    }

    [Fact]
    public void ShotsAreSpacedByTheFireIntervalAndReachBeyondArcheryRange()
    {
        foreach (int interval in new[] { 5, 1 })
        {
            var p = DomainProfiles.Sniper with { FireIntervalSteps = interval };
            var b = new TacticalBattle(Duel(p, 2, 3), PassiveController.Instance, new KnowledgeAi());
            b.RunUntilPulse(6);
            var shots = b.Scheduler.Trace.Entries.Where(e => e.Kind == TacticalKinds.Fire && e.Outcome == Proto.M7.Events.TraceOutcome.Executed && e.A == 2 && e.Source.Id == 1000).ToArray();
            Assert.True(shots.Length >= 2, $"interval {interval}: {shots.Length} shots");
            long minGap = long.MaxValue;
            for (int i = 1; i < shots.Length; i++) minGap = Math.Min(minGap, shots[i].Key.TimeMs - shots[i - 1].Key.TimeMs);
            if (interval == 5) Assert.True(minGap >= 5 * 2_000, $"gap {minGap}");
            else Assert.Equal(2_000, minGap);                                       // teeth: without the interval it fires every step
        }
        // Reach: first hit lands at a range archers (2 cells) and riflemen (6 cells) cannot touch.
        var b2 = new TacticalBattle(Duel(DomainProfiles.Sniper, 2, 3), PassiveController.Instance, new KnowledgeAi());
        RunUntil(b2, () => b2.Entities().Any(e => e.FormationId == 2 && e.Hp < UnitProfiles.HeavyInfantry.MaxHp), 20);
        var shooter = L(b2, 1000);
        var victim = b2.Entities().Where(e => e.FormationId == 2 && e.Hp < UnitProfiles.HeavyInfantry.MaxHp).First();
        Assert.True(Vec2L.Distance(shooter.Pos, victim.Pos) > 6 * CellMm);
    }

    /// <summary>
    /// Personal concealment only lowers the enemy's acquisition chance. With the same seed every draw is shared (counter RNG),
    /// so until the first detection both battles are identical and the camouflaged sniper can never be seen EARLIER:
    /// first-detection(concealed) &gt;= first-detection(unconcealed) for every seed, strictly later for some.
    /// </summary>
    [Fact]
    public void PersonalConcealmentDelaysDetectionUnderCommonRandomNumbers()
    {
        long First(UnitProfile p, ulong seed)
        {
            var b = new TacticalBattle(Duel(p, 2, seed), PassiveController.Instance, new KnowledgeAi());
            long t = -1;
            RunUntil(b, () => { if (b.EverDetected(1, 1000) || b.EverDetected(1, 1001)) { t = b.Scheduler.NowMs; return true; } return b.CurrentPulse >= 12; }, 13);
            return t < 0 ? long.MaxValue : t;
        }
        int later = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            long plain = First(DomainProfiles.Sniper with { PersonalConcealment = Q32.One }, seed);
            long hid = First(DomainProfiles.Sniper, seed);
            Assert.True(hid >= plain, $"seed {seed}: concealed seen at {hid} before plain at {plain}");
            if (hid > plain) later++;
        }
        Assert.True(later > 0);
    }

    [Fact]
    public void ConcealedSniperInTheOpenCountsAsActivelyHiding()
    {
        // A sniper standing in OPEN ground with personal concealment is "concealed" for the active-concealment clause: while an
        // enemy that is making progress (searching) passes within its sight, the sniper's formation stays active; the same
        // entity without camouflage is plainly inactive. (The sniper does not shoot here: Ranged 0, and its camouflage is a
        // TEST INPUT of 2^-20 so it is not found during the test.)
        long Counter(Q32 pc)
        {
            var s = new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
                DefaultSides(obj1: new[] { Cell(20, 28) }),
                new[]
                {
                    new FormationSpec(1, 0, DomainProfiles.Sniper with { PersonalConcealment = pc, Ranged = 0, RangeCells = 0 }, 1, Cell(10, 16)),
                    new FormationSpec(2, 1, UnitProfiles.HeavyInfantry, 5, Cell(20, 8)),
                }, 9);
            var b = new TacticalBattle(s, PassiveController.Instance, Script((0, new OrderRequest(2, OrderKind.MoveTo, 0, -1, Cell(20, 28)))));
            b.RunUntilPulse(3);
            Assert.False(b.EverDetected(1, 1000) && pc != Q32.One);
            return b.InactivityCounter(1);
        }
        Assert.Equal(0, Counter(Q32.FromRatio(1, 1 << 20)));
        Assert.True(Counter(Q32.One) > 0);
    }
}
