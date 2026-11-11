using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Tests.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Morale;

/// <summary>
/// [MOR2] Lane MOR's open round-2 item (H-MOR §11.6, §11.8): the ZERO-DAMAGE GUARD of "the fight it won" (morale proposal §3.2:
/// a formation rises when an enemy formation it "dealt damage to within the last pulse length" breaks) was unpinned in both damage
/// paths of the TacticalResolver — the Damage event (melee and missiles; `dmg &gt; 0`, mutant L1) and lane N's crew-served DomainHit
/// (`amount &gt; 0`, mutant G3). A blow deals nothing when its target was brought to 0 HP by another blow at the same instant (all
/// damage of an instant lands before any death is resolved; events of one struck entity run in scheduling order, and the fire of
/// the lower attacker id is scheduled first). The battles below were found by a probe search (scratchpad mor2/probe-zero-*.txt):
/// a formation's ONLY blow on the enemy formation within the pulse before that formation breaks is such a zero-damage blow. It dealt
/// no damage, so it must not rise; with the guard dropped it would. Every blow is observed from outside the rule's code (hit points
/// before each executed damage event). TEST INPUT geometry and profiles.
/// </summary>
public sealed class ZeroDamageGuardTests
{
    private const long PulseMs = 60_000;

    /// <summary>B: heavy infantry 50 (leadership 100) holding a line at column 20; A: the probe's formation at column 19 or 18; G:
    /// heavy infantry 20 attacking from column 26 and breaking under B's blows.</summary>
    private static BattleSetup Line(ulong seed, FormationSpec a)
    {
        Vec2L b = Cell(20, 16), g = Cell(26, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { g }, new[] { b }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, b) { Posture = Posture.Defend, Leadership = 100 },
                a,
                new FormationSpec(4, 1, UnitProfiles.HeavyInfantry, 20, g),
            },
            seed) { Rules = new TacticalRules { LocalWinMoraleRise = 200 } };
    }

    /// <summary>Every executed blow on a land entity by either path, as (instant, attacker formation, target formation, the target's
    /// hit points before, event kind) — zero-damage blows included.</summary>
    private static (BattleResult Result, List<(long T, long From, long To, long HpBefore, int Kind)> Blows) RunObservingBlows(TacticalBattle b)
    {
        var blows = new List<(long, long, long, long, int)>();
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
            if (tg < 0 || src < 0) continue;
            blows.Add((ev.Key.TimeMs, forms[src], forms[tg], before[tg].Hp, ev.Kind));
        }
        return (b.Result!, blows);
    }

    private static void AssertTheLoneBlowIsZero_AndItsFormationDoesNotRise(TacticalBattle b, int kind, long blowAt, long breakAt)
    {
        var (r, blows) = RunObservingBlows(b);
        var mine = blows.Where(x => x.From == 2 && x.To == 4).ToArray();
        Assert.Equal(new[] { (blowAt, 2L, 4L, 0L, kind) }, mine);                           // its one blow on G: the target already at 0 HP
        Assert.Contains(blows, x => x.T == blowAt && x.From == 1 && x.To == 4 && x.HpBefore > 0);   // B's blows at that instant
        var rows = b.LocalWinRises.ToArray();
        Assert.Contains(rows, x => x.AtMs == breakAt && x.FormationId == 1 && x.BrokenFormationId == 4);   // G broke under B's blows
        Assert.InRange(breakAt - blowAt, 0, PulseMs);                                        // within the window: only "dealt damage" decides
        var a = r.Formations.Single(f => f.FormationId == 2);
        Assert.True(a.ActiveAtEnd > 0 && a.RoutedEscaped == 0, "the formation is in the fight at the break");
        Assert.Equal(0, a.DamageDealt);                                                      // it dealt nothing at all
        Assert.DoesNotContain(rows, x => x.FormationId == 2);                               // so it does not rise (L1 / G3 would credit it)
    }

    /// <summary>THE DAMAGE EVENT'S GUARD (mutant L1). A: ONE archer that looses one arrow in the whole battle (fire interval of a
    /// million steps, range 1 cell), just behind the line. Its arrow strikes, at 90,000 ms (seed 3) or 92,000 ms (seed 24), a man of
    /// G whom B's blows of the same instant had already brought to 0 HP: it deals no damage. G breaks 20,000 ms later, within the
    /// pulse; A is in the fight; A dealt G no damage, so A does not rise, while B does.</summary>
    [Theory]
    [InlineData(3UL, 90_000L, 110_000L)]
    [InlineData(24UL, 92_000L, 112_000L)]
    public void Tactical_AZeroDamageArrow_IsNoBlow_TheArcherDoesNotRise(ulong seed, long blowAt, long breakAt)
    {
        var oneArrow = UnitProfiles.Archers with { FireIntervalSteps = 1_000_000, RangeCells = 1 };
        var a = new FormationSpec(2, 0, oneArrow, 1, Cell(19, 16)) { Posture = Posture.Defend };
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        AssertTheLoneBlowIsZero_AndItsFormationDoesNotRise(new TacticalBattle(Line(seed, a), ai, ai), TacticalKinds.Damage, blowAt, breakAt);
    }

    /// <summary>THE CREW-SERVED GUARD (mutant G3). A: one gun team (lane N's gun crew) at column 18 with a direct-fire field gun of
    /// 40 m range that fires once (a reload of 100,000 s). Its shot hits, at 90,000 ms, a man of G whom B's blows of the same instant
    /// had already brought to 0 HP (lane N's DomainHit of kind 0 with amount 0). G breaks within the pulse (seed 3: 110,000 ms; seed
    /// 28: 116,000 ms); the guns are in the fight and dealt nothing, so they do not rise.</summary>
    [Theory]
    [InlineData(3UL, 90_000L, 110_000L)]
    [InlineData(28UL, 90_000L, 116_000L)]
    public void Tactical_AZeroDamageShell_IsNoBlow_TheGunsDoNotRise(ulong seed, long blowAt, long breakAt)
    {
        var gun = DomainProfiles.FieldGun with { MaxRangeMm = 40_000, BlastRadiusMm = 0, ReloadMs = 100_000_000, Accuracy = 1000, DispersionPermille = 0 };
        var a = new FormationSpec(2, 0, DomainProfiles.GunCrew, 1, Cell(18, 16)) { Posture = Posture.Defend, Weapons = new[] { gun } };
        var ai = new KnowledgeAi { RetreatMorale = 0 };
        AssertTheLoneBlowIsZero_AndItsFormationDoesNotRise(new TacticalBattle(Line(seed, a), ai, ai), DomainKinds.DomainHit, blowAt, breakAt);
    }
}
