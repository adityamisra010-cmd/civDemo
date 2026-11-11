using System.Security.Cryptography;
using Proto.M7.Events;
using Proto.M7.Morale;
using Proto.M7.Spatial;
using Proto.M7.Support;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.WarEcon;
using Proto.M7.Tests.LaneD;
using static Proto.M7.Tests.TestSupport;
using static Proto.M7.Tests.WarEconE.WarEconRigs;

namespace Proto.M7.Tests.Morale;

/// <summary>[MOR] Rigs for the lane MOR campaign-morale tests (lane E's calendar and PROPOSED pulse table, lane D's support world;
/// every number is a TEST INPUT).</summary>
internal static class MoraleRig
{
    public static readonly long Week = 7 * Day;

    /// <summary>n halted foot armies of 2,000 men, army i owned by polity i % 2, all in war 0, fed (a 50,000,000 gm train) and paid
    /// (both treasuries 50,000,000): lane E's fed, paid, loyal army, so its pulse-end morale update is the recovery or decay alone.</summary>
    public static WarEconSetup Armies(int n, LogisticsRates? rates = null, long men = 2_000)
    {
        var route = new Route(new[] { Seg(400, TerrainClass.Open, RoadClass.Track, -1) });
        var armies = new ArmySetup[n];
        for (int i = 0; i < n; i++)
            armies[i] = new ArmySetup(i, i % 2, 0, 0, new ArmyComposition(men, 0, 0, 0, 0, 0, 0, 0, false), route, 60_000, false, 50_000_000, 0, 0,
                                      Array.Empty<(long, long)>());
        return new WarEconSetup(FoodOrdering.CivilianMilitaryGrowth, rates ?? LogisticsRates.Proposal(), TerrainLogistics.Proposal(),
            new[] { Polity(0, WarTech.Coinage, 300_000, 2_000_000_000, treasury: 50_000_000), Polity(1, WarTech.Coinage, 300_000, 2_000_000_000, treasury: 50_000_000) },
            Array.Empty<LocalitySetup>(), armies);
    }

    /// <summary>The support world over lane E's economy (lane D's SupportRig.Build, with the DA1 coefficients as an input).</summary>
    public static SupRig Build(WarEconSetup setup, long start, MoraleRates? morale = null, SupportPool[]? pools = null, long weeks = 8,
                               long[]? probes = null)
    {
        var cal = ShippedCalendar();
        var table = new long[cal.Bands.Count];
        for (int i = 0; i < table.Length; i++) table[i] = ProposedPulseDays[i] * Day;
        var s = new EventScheduler(start - Day);
        var wars = new[] { WarOf(0, start, start + weeks * Week, 0, 1) };
        var inner = new WarTheaterWorld(cal, new WarPulseSchedule(cal, table), 2, wars, WarFog.None, SignificantKinds.None, new long[2], 0, 0);
        var w = new SupportWorld(inner, cal, setup, pools ?? Array.Empty<SupportPool>(), SupportRates.Proposal(), morale);
        w.Begin(s);
        foreach (long p in probes ?? Array.Empty<long>()) WarTheaterWorld.ScheduleProbe(s, p, 0);
        return new SupRig(s, inner, w, new Runner(s, w, SupportRig.Sig, stopOnSignificant: true));
    }

    public static void Run(SupRig rig, long limit) => SupportRig.Drive(rig, limit, _ => Array.Empty<(int, long, long)>());

    /// <summary>Lane W's collapse of 50 entities (fit 30, wounded 5, routed 3, dead 10, surrendered 2) of a formation on one side.</summary>
    public static CollapseInput Collapse(int side, long moraleEnd) => new(side, side, new long[] { 30, 5, 3, 10, 2 }, moraleEnd, 1000);

    /// <summary>One army's part in a battle that ends at <paramref name="at"/>: it started at 800 on the battle scale (lane E's
    /// opening 800,000), ended at <paramref name="end"/>; the report names <paramref name="winner"/>; the strengths in persons.</summary>
    public static SupportBattle Battle(int army, int side, long at, long end, int winner, long beaten, long winnerSide, bool held = false)
        => new(army, at, Collapse(side, end), held, 800, 2_000, 0) { WinnerSide = winner, BeatenPersons = beaten, WinnerSidePersons = winnerSide };

    /// <summary>[MOR2] D-083 DB1-R1 (a): a battle the army's formation never reached the field of (a reserve not committed; a
    /// formation that stayed aboard): its 50 entities are all fit and its morale ends where it started (800).</summary>
    public static SupportBattle NeverEntered(int army, int side, long at, int winner, long beaten, long winnerSide)
        => new(army, at, new CollapseInput(side, side, new long[] { 50, 0, 0, 0, 0 }, 800, 1000), false, 800, 2_000, 0)
           { WinnerSide = winner, BeatenPersons = beaten, WinnerSidePersons = winnerSide, Entered = false };

    public static long WarStart() => SupportRig.WarStart();         // band 3: 7-day proposed pulses

    /// <summary>floor(perYear‰ × 1000 × w ÷ year): lane E's Scale over a pulse of w ms (by hand).</summary>
    public static long OverPulse(long perMilleYear, long w) => (long)((Int128)perMilleYear * 1000 * w / Proto.M7.Time.SimInstant.MsPerYear);
}

/// <summary>
/// [MOR] D-082 DA1 with D-079 DR29 at the campaign level (docs/m7-plan/morale-proposal.md §4): every participant's campaign morale
/// falls by the battle's net combat loss (lane D's rule), and the army of the side the battle report names the winner also gains
/// the DA1 rise floor(VictoryRiseMaxPpm × B ÷ (B + W)); one clamp. Then lane E's pulse-end dynamics move the stock toward the baseline.
/// </summary>
public sealed class CampaignMoraleTests
{
    private static readonly long Week = MoraleRig.Week;

    // ------------------------------------------------------------------ the equations, by hand

    [Fact]
    public void Equations_ByHand_DecreaseRiseOneClampAndTheBattleStart()
    {
        // DR29 decrease (lane D's net loss): floor(max(0, start − end − restore) × 1,000,000 ÷ MoraleMax)
        Assert.Equal(160_000, CampaignMorale.CombatDecreasePpm(800, 600, 40, 1000));
        Assert.Equal(0, CampaignMorale.CombatDecreasePpm(800, 900, 0, 1000));          // an in-battle gain is never carried
        Assert.Equal(0, CampaignMorale.CombatDecreasePpm(800, 790, 40, 1000));         // a restore above the net loss gives no rise
        Assert.Equal(1_000_000, CampaignMorale.CombatDecreasePpm(1000, 0, 0, 1000));
        Assert.Equal(333_333, CampaignMorale.CombatDecreasePpm(750, 500, 0, 750));     // floor(250 × 1,000,000 ÷ 750)
        // DA1 rise: floor(400,000 × B ÷ (B + W)) for the winner only
        var r = MoraleRates.Proposal();
        Assert.Equal(400_000, r.VictoryRiseMaxPpm);
        Assert.Equal(200_000, CampaignMorale.VictoryRisePpm(true, 2_000, 2_000, r));   // an equal force: half
        Assert.Equal(266_666, CampaignMorale.VictoryRisePpm(true, 4_000, 2_000, r));   // twice one's own: two thirds (floor)
        Assert.Equal(133_333, CampaignMorale.VictoryRisePpm(true, 1_000, 2_000, r));   // half one's own: one third (floor)
        Assert.Equal(0, CampaignMorale.VictoryRisePpm(false, 2_000, 2_000, r));        // the side that did not win
        Assert.Equal(0, CampaignMorale.VictoryRisePpm(true, 0, 2_000, r));             // a "win" over nobody
        Assert.Equal(0, CampaignMorale.VictoryRisePpm(true, 2_000, 2_000, r with { VictoryRiseMaxPpm = 0 }));
        // one clamp over both terms (the order of the two terms does not exist)
        Assert.Equal(900_000, CampaignMorale.After(800_000, 100_000, 200_000));
        Assert.Equal(1_000_000, CampaignMorale.After(950_000, 0, 200_000));            // the top of the scale
        Assert.Equal(0, CampaignMorale.After(100_000, 300_000, 150_000));              // not 150,000 (decrease clamped first)
        Assert.Equal(700_000, CampaignMorale.After(800_000, 100_000, 0));
        // the campaign → battle hand-off (M7.15): floor(campaign × MoraleMax ÷ 1,000,000)
        Assert.Equal(800, CampaignMorale.BattleStartMorale(800_000, 1000));
        Assert.Equal(999, CampaignMorale.BattleStartMorale(999_999, 1000));
        Assert.Equal(1000, CampaignMorale.BattleStartMorale(1_000_000, 1000));
        Assert.Equal(249, CampaignMorale.BattleStartMorale(333_333, 750));
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMorale.After(1_000_001, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMorale.VictoryRisePpm(true, -1, 2_000, r));
        Assert.Throws<ArgumentOutOfRangeException>(() => (MoraleRates.Proposal() with { VictoryRiseMaxPpm = 1_000_001 }).Validate());
    }

    /// <summary>What counts as a win (DA1-R (a)): the side the battle report names. Today's resolvers name one for a SideEliminated end
    /// (the other side routed, surrendered, died or withdrew) and a SideDisengaged end (the other side withdrew), and none for the ends
    /// that separate neither side (DA6 and DA7 decide those once lane AP's resolvers report them).</summary>
    [Fact]
    public void BattleWinner_IsTheSideTheReportNames()
    {
        BattleResult R(BattleEndReason reason, int side, int holds) =>
            new(ResolverKind.Tactical, reason, side, holds, 3, 180_000, 1, 0, "", "", Array.Empty<FormationResult>());
        Assert.Equal(0, BattleWinner.Reported(R(BattleEndReason.SideEliminated, 1, 0)));
        Assert.Equal(1, BattleWinner.Reported(R(BattleEndReason.SideEliminated, 0, 1)));
        Assert.Equal(1, BattleWinner.Reported(R(BattleEndReason.SideDisengaged, 0, 1)));
        Assert.Equal(0, BattleWinner.Reported(R(BattleEndReason.SideDisengaged, 1, 0)));
        Assert.Equal(-1, BattleWinner.Reported(R(BattleEndReason.BothEliminated, -1, -1)));
        Assert.Equal(-1, BattleWinner.Reported(R(BattleEndReason.BothDisengaged, -1, -1)));
        Assert.Equal(-1, BattleWinner.Reported(R(BattleEndReason.Standoff, -1, -1)));
        Assert.Equal(-1, BattleWinner.Reported(R(BattleEndReason.None, -1, -1)));
    }

    // ------------------------------------------------------------------ DA1 in the world: the winner rises, the loser falls

    /// <summary>One battle, two armies. Army 0 (side 0) ends at 700 of 1,000 from a start of 800 (decrease 100,000); army 1 (side 1)
    /// routs to 250 (decrease 550,000). The report names side 0 the winner, so army 0 gains floor(400,000 × B ÷ (B + W)) and army 1
    /// gains nothing. Both entered at lane E's opening 800,000 (the battle ends on day 3, before the first pulse end on day 7).
    /// [MOR2, D-083 DB1: both armies entered the battle, so each also pays the 50,000 entry cost (MoraleRates.EntryCostPpm), its own
    /// term in the same clamp. Every expected after below is the lane-MOR value less 50,000 (MEASURED: lane MOR's values 900,000,
    /// 966,666, 833,333 and 250,000 failed by exactly 50,000 before this update); the two top-of-scale rows of lane MOR (6,000 and 12,000
    /// beaten) no longer reach the top, and two rows (14,000: exactly the top; 30,000: clamped) keep the top-of-scale case.]</summary>
    [Theory]
    [InlineData(2_000L, 2_000L, 200_000L, 850_000L)]
    [InlineData(4_000L, 2_000L, 266_666L, 916_666L)]
    [InlineData(1_000L, 2_000L, 133_333L, 783_333L)]
    [InlineData(6_000L, 2_000L, 300_000L, 950_000L)]       // [MOR2] lane MOR's "exactly the top": now 1,000,000 − 50,000
    [InlineData(12_000L, 2_000L, 342_857L, 992_857L)]      // [MOR2] lane MOR's clamped case: 1,042,857 − 50,000, below the top
    [InlineData(14_000L, 2_000L, 350_000L, 1_000_000L)]    // [MOR2] exactly the top: 800,000 − 50,000 − 100,000 + 350,000
    [InlineData(30_000L, 2_000L, 375_000L, 1_000_000L)]    // [MOR2] 1,025,000 clamped to the top
    public void DA1_TheWinnersArmyRises_TheLosersFalls_ByHand(long beaten, long winnerSide, long rise, long winnerAfter)
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 0, beaten, winnerSide, held: true));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 250, 0, beaten, winnerSide));
        MoraleRig.Run(rig, at + 1);
        Assert.Equal(winnerAfter, rig.W.Economy.MoraleOf(0));
        Assert.Equal(200_000, rig.W.Economy.MoraleOf(1));                       // [MOR2] 800,000 − 50,000 − 550,000: no rise for the loser
        var m0 = rig.W.MoraleRows.Single(x => x.ArmyId == 0);
        var m1 = rig.W.MoraleRows.Single(x => x.ArmyId == 1);
        Assert.Equal((100_000L, rise, 800_000L, winnerAfter), (m0.DecreasePpm, m0.RisePpm, m0.BeforePpm, m0.AfterPpm));
        Assert.Equal((550_000L, 0L, 800_000L, 200_000L), (m1.DecreasePpm, m1.RisePpm, m1.BeforePpm, m1.AfterPpm));
        Assert.All(new[] { m0, m1 }, m => Assert.Equal(((byte)1, 0L, 50_000L), (m.Entered, m.EntryInBattle, m.EntryPpm)));   // [MOR2]
        Assert.Equal(winnerAfter, rig.W.BattleRows.Single(x => x.ArmyId == 0).MoraleAfter);   // lane D's row carries the same after
    }

    /// <summary>D-082 DA6 ("If two sides are equal, the defending side wins"): a battle that separates neither side on the field (here
    /// neither side holds it alone, and both lost the same: each ended at 750 from 800) is given to the defender, and the morale layer
    /// credits exactly the side the report names: the defender (side 1) rises above its 800,000, the attacker (side 0) falls below it.
    /// A rule that read the field instead of the report would credit nobody. (Lane AP implements DA6 in the AutoResolver; the stand-in
    /// passes the winner its report will name.)</summary>
    [Fact]
    public void DA1_EqualSides_TheDefenderTheReportNames_Rises_ThoughNeitherSideHeldTheField()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        var attacker = MoraleRig.Battle(0, 0, at, 750, winner: 1, beaten: 2_000, winnerSide: 2_000);
        var defender = MoraleRig.Battle(1, 1, at, 750, winner: 1, beaten: 2_000, winnerSide: 2_000);
        Assert.False(attacker.HeldField || defender.HeldField);
        rig.W.ScheduleBattle(rig.S, attacker);
        rig.W.ScheduleBattle(rig.S, defender);
        MoraleRig.Run(rig, at + 1);
        // [MOR2, D-083 DB1] each army also pays the 50,000 entry cost: lane MOR's 750,000 and 950,000 less 50,000 (MEASURED before this update)
        Assert.Equal(700_000, rig.W.Economy.MoraleOf(0));                        // 800,000 − 50,000 (entry) − 50,000: the attacker falls
        Assert.Equal(900_000, rig.W.Economy.MoraleOf(1));                        // 800,000 − 50,000 (entry) − 50,000 + 200,000: the defender rises
        Assert.Equal(new[] { 0L, 200_000L }, rig.W.MoraleRows.Select(x => x.RisePpm).ToArray());
    }

    /// <summary>D-082 DA7 with DA7-R (a): "Unless it's an ambush like from a hidden unit, then the winning side wins", read as the
    /// ambusher. Equal sides (equal strengths, equal losses: each ended at 750 from 800) in an ambush that side 0 launched from
    /// concealment against the defender, side 1: the report names the ambusher, so side 0 rises above its 800,000 and the defender
    /// falls below it. A rule that gave every equal battle to the defender (DA6 without its exception) would credit side 1 here.</summary>
    [Fact]
    public void DA1_AnAmbushBetweenEqualSides_TheAmbusherTheReportNames_Rises_NotTheDefender()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 750, winner: 0, beaten: 2_000, winnerSide: 2_000));   // the ambusher
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 750, winner: 0, beaten: 2_000, winnerSide: 2_000));   // the defender
        MoraleRig.Run(rig, at + 1);
        // [MOR2, D-083 DB1] each army also pays the 50,000 entry cost: lane MOR's 950,000 and 750,000 less 50,000 (MEASURED before this update)
        Assert.Equal(900_000, rig.W.Economy.MoraleOf(0));                        // 800,000 − 50,000 (entry) − 50,000 + 200,000: the ambusher rises
        Assert.Equal(700_000, rig.W.Economy.MoraleOf(1));                        // 800,000 − 50,000 (entry) − 50,000: the defender falls
        Assert.Equal(new[] { 200_000L, 0L }, rig.W.MoraleRows.Select(x => x.RisePpm).ToArray());
    }

    /// <summary>A battle whose report names no winner (BothEliminated, BothDisengaged or Standoff from today's resolvers, before DA6 and
    /// DA7 are reported; or a stand-in that reports none): DR29's decrease only, for both, and nobody rises.</summary>
    [Fact]
    public void DA1_NoWinnerReported_BothFallByTheirLossOnly()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 650, winner: -1, beaten: 2_000, winnerSide: 2_000, held: true));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 700, winner: -1, beaten: 2_000, winnerSide: 2_000, held: true));
        MoraleRig.Run(rig, at + 1);
        Assert.Equal(600_000, rig.W.Economy.MoraleOf(0));                        // [MOR2, DB1] lane MOR's 650,000 − 50,000 entry
        Assert.Equal(650_000, rig.W.Economy.MoraleOf(1));                        // [MOR2, DB1] lane MOR's 700,000 − 50,000 entry
        Assert.All(rig.W.MoraleRows, x => Assert.Equal(0, x.RisePpm));
    }

    /// <summary>The stand-in refuses a winner outside −1..1 and negative strengths (it is replayed input). [verification round 1,
    /// MOR-V1-11] It also refuses a reported winner without the beaten side's strength, for the winner's and the loser's record alike:
    /// before the fix such a battle settled as a win with a rise of 0 (MEASURED by the fix round's probe: RisePpm 0, morale 800,000
    /// unchanged), so a caller that named the winner but forgot BeatenPersons silently lost the rise. A battle that names no winner
    /// (lane D's stand-in battles) still needs no strengths.</summary>
    [Fact]
    public void DA1_TheHandOffRefusesABadWinnerOrStrength()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 2, 2_000, 2_000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, -2, 2_000, 2_000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 0, -1, 2_000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 0, 2_000, -1)));
        // [MOR-V1-11] a reported winner, no beaten strength: refused for the winning army's record and for the losing army's
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, new SupportBattle(0, at, MoraleRig.Collapse(0, 800), true, 800, 2_000, 0) { WinnerSide = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 700, 0, 0, 2_000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 1, 0, 0)));
        // nothing was scheduled by the refused calls; a battle that names no winner needs no strengths and settles DR29 alone
        // [MOR2, D-083 DB1] ... and the entry cost: 800,000 − 50,000 − 100,000 = 650,000 (lane MOR's 700,000 less 50,000)
        rig.W.ScheduleBattle(rig.S, new SupportBattle(0, at, MoraleRig.Collapse(0, 700), true, 800, 2_000, 0));
        MoraleRig.Run(rig, at + 1);
        var m = rig.W.MoraleRows.Single();
        Assert.Equal((-1, 0L, 0L, 100_000L, 650_000L), (m.WinnerSide, m.BeatenPersons, m.RisePpm, m.DecreasePpm, m.AfterPpm));
        Assert.Equal(50_000, m.EntryPpm);
    }

    /// <summary>[verification round 1, MOR-V1-03] The field is not the winner, also when the LOSING army is the one that held the
    /// field (lane W's desertion input): the report names side 0, the side-1 army held the field; both ended at 700 from 800. The
    /// winner rises (800,000 − 100,000 + 200,000), the loser falls by its loss alone (700,000) and its row records no rise. A rule that
    /// gave the rise to "the reported winner OR a field holder" (MEASURED before this test: it survived the whole suite, the loser
    /// ending at 900,000) or that read the field instead of the report would credit the loser here.</summary>
    [Fact]
    public void DA1_ALoserThatHeldTheField_DoesNotRise_TheReportDecides()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        var winner = MoraleRig.Battle(0, 0, at, 700, winner: 0, beaten: 2_000, winnerSide: 2_000, held: false);
        var loser = MoraleRig.Battle(1, 1, at, 700, winner: 0, beaten: 2_000, winnerSide: 2_000, held: true);
        rig.W.ScheduleBattle(rig.S, winner);
        rig.W.ScheduleBattle(rig.S, loser);
        MoraleRig.Run(rig, at + 1);
        // [MOR2, D-083 DB1] both entered: each value is lane MOR's less the 50,000 entry cost (900,000, 700,000 before this update)
        Assert.Equal(850_000, rig.W.Economy.MoraleOf(0));                        // the reported winner, though it did not hold the field
        Assert.Equal(650_000, rig.W.Economy.MoraleOf(1));                        // the field holder that lost: its loss (and the entry cost) only
        var m1 = rig.W.MoraleRows.Single(x => x.ArmyId == 1);
        Assert.Equal((0, 0L, 100_000L, 650_000L), (m1.WinnerSide, m1.RisePpm, m1.DecreasePpm, m1.AfterPpm));
        Assert.Equal(200_000, rig.W.MoraleRows.Single(x => x.ArmyId == 0).RisePpm);
    }

    /// <summary>The one-army stand-in (WinnerSidePersons 0): the winner's side is this army's own men at the battle instant, before
    /// the collapse moves its casualties (lane E's men account). 2,000 men less lane E's attrition over three days.</summary>
    [Fact]
    public void DA1_WithoutASideStrength_TheWinnersSideIsTheArmysOwnMenAtTheBattle()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 800, winner: 0, beaten: 1_000, winnerSide: 0));
        MoraleRig.Run(rig, at + 1);
        var b = rig.W.BattleRows.Single();
        var m = rig.W.MoraleRows.Single();
        Assert.Equal(b.Persons, m.WinnerSidePersons);
        Assert.InRange(b.Persons, 1_990, 2_000);
        Assert.Equal(400_000L * 1_000 / (1_000 + b.Persons), m.RisePpm);         // about 133,400: below the top of the scale
        Assert.Equal(800_000 - 50_000 + m.RisePpm, rig.W.Economy.MoraleOf(0));   // [MOR2, DB1] less the 50,000 entry cost
    }

    // ------------------------------------------------------------------ the instant (turn-exact pins)

    /// <summary>TURN-EXACT PIN: the settlement applies at the battle's end instant (class CampaignProcess) and not before: 1 ms
    /// earlier the army still has lane E's opening 800,000; at the instant it has the DA1 result.</summary>
    [Fact]
    public void DA1_TheRiseAppliesAtTheBattlesEndInstant_NotBefore()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day + 12_345;
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 0, 2_000, 2_000));
        MoraleRig.Run(rig, at - 1);
        Assert.Equal(800_000, rig.W.Economy.MoraleOf(0));
        Assert.Empty(rig.W.MoraleRows);
        MoraleRig.Run(rig, at);
        Assert.Equal(850_000, rig.W.Economy.MoraleOf(0));                        // [MOR2, DB1] lane MOR's 900,000 less the 50,000 entry cost
        Assert.Equal(at, rig.W.MoraleRows.Single().AtMs);
    }

    /// <summary>TURN-EXACT PIN: a battle that ends AT a pulse-end instant is settled after that pulse's morale update (System before
    /// CampaignProcess at one instant), and one that ends 1 ms earlier before it. The win lifts army 0 by 200,000 (equal sides, no
    /// loss: it ended at its start). The pulse-end update over the 7-day pulse w: recovery or decay floor(600 × 1000 × w ÷ year) =
    /// 11,666 micro units.
    /// Baseline 800 (option (b)): 1 ms before, the excess decays at that pulse end (1,000,000 → 988,334); at the instant, the army sits
    /// at the baseline when the pulse ends (no recovery past it) and then rises (1,000,000).
    /// Baseline at the cap (the default): 1 ms before, 1,000,000 (nothing above the cap to recover); at the instant, 811,666 + 200,000
    /// = 1,011,666 clamped to 1,000,000. A smaller win (133,333) gives 944,999 either way: at the default the two orders differ only
    /// where the top of the scale binds.
    /// [MOR2, D-083 DB1: the battle now also costs the 50,000 entry cost, so the lossless win lifts army 0 by 200,000 − 50,000 = 150,000
    /// (133,333 − 50,000 = 83,333 for the smaller one). Baseline 800: 1 ms before, 950,000 decays to 938,334 at the pulse end; at the
    /// instant, 800,000 then 950,000 — the pin still tells the two orders apart. Baseline at the cap: 1 ms before, 950,000 + 11,666 =
    /// 961,666; at the instant, 811,666 + 150,000 = 961,666 (the cap no longer binds, so the orders agree); the smaller win 894,999 either
    /// way. Lane MOR's values (988,334; 1,000,000 ×3; 944,999 ×2) failed before this update (MEASURED).]</summary>
    [Theory]
    [InlineData(800L, 2_000L, -1L, 938_334L)]
    [InlineData(800L, 2_000L, 0L, 950_000L)]
    [InlineData(1000L, 2_000L, -1L, 961_666L)]
    [InlineData(1000L, 2_000L, 0L, 961_666L)]
    [InlineData(1000L, 1_000L, -1L, 894_999L)]
    [InlineData(1000L, 1_000L, 0L, 894_999L)]
    public void DA1_ABattleAtThePulseEndInstant_IsSettledAfterThatPulsesUpdate(long baseline, long beaten, long offsetMs, long expected)
    {
        long start = MoraleRig.WarStart(), pulseEnd = start + Week;
        var rates = LogisticsRates.Proposal() with { MoraleBaselinePerMille = baseline };
        var rig = MoraleRig.Build(MoraleRig.Armies(2, rates), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, pulseEnd + offsetMs, 800, 0, beaten, 2_000));
        MoraleRig.Run(rig, pulseEnd);
        var first = rig.W.Economy.Rows.First(x => x.ArmyId == 0);
        Assert.Equal((start, pulseEnd), (first.StartMs, first.EndMs));             // the pulse the pin is about
        Assert.Equal(11_666, MoraleRig.OverPulse(600, Week));
        Assert.Equal(expected, rig.W.Economy.MoraleOf(0));
    }

    // ------------------------------------------------------------------ Law 2 / Law 3: toward a baseline at per-sim-year rates

    /// <summary>Baseline 800 (option (b)): a victory lifts army 0 to 1,000,000 on day 3; at every pulse end after it the excess decays
    /// by exactly min(excess, floor(600 × 1000 × w ÷ year)) over that pulse's exact length w, with no recovery above the baseline, until
    /// it is back at 800,000, where it stays (a fed, paid army neither decays below nor recovers above the baseline). The first pulse
    /// end decays the excess over the whole 7-day pulse although it arose on day 3: lane E's pulse-end granularity (first order in the
    /// pulse length; morale proposal §4.4 limitation).
    /// [MOR2, D-083 DB1: the victory now lifts army 0 to 800,000 − 50,000 + 200,000 = 950,000 (the entry cost); the first pulse end
    /// decays it to 938,334 (lane MOR's 1,000,000 and 988,334 failed before this update, MEASURED); the closed form is unchanged.]</summary>
    [Fact]
    public void Baseline_AnExcessDecaysTowardIt_OverTheExactPulseLength_ClosedForm()
    {
        long start = MoraleRig.WarStart();
        var rates = LogisticsRates.Proposal() with { MoraleBaselinePerMille = 800 };
        var rig = MoraleRig.Build(MoraleRig.Armies(2, rates), start, weeks: 30);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, start + 3 * Day, 800, 0, 2_000, 2_000));
        MoraleRig.Run(rig, start + 25 * Week);
        long m = 950_000;                                                           // [MOR2] the win less the entry cost
        int pulses = 0, atBaseline = 0;
        foreach (var row in rig.W.Economy.Rows.Where(x => x.ArmyId == 0).OrderBy(x => x.PulseIndex))
        {
            Assert.Equal(0, row.HungerPpm);
            Assert.Equal(0, row.ArrearsEnd);
            Assert.Equal(ArmyCondition.Loyal, row.ConditionEnd);
            long decay = MoraleRig.OverPulse(rates.MoraleElationDecayPerYear, row.EndMs - row.StartMs);
            m = m > 800_000 ? Math.Max(800_000, m - decay) : m;
            Assert.Equal(m, row.MoraleEnd);
            pulses++;
            if (row.MoraleEnd == 800_000) atBaseline++;
        }
        Assert.Equal(938_334, rig.W.Economy.Rows.First(x => x.ArmyId == 0).MoraleEnd);   // [MOR2] 950,000 − 11,666
        Assert.True(pulses >= 20 && atBaseline >= 3, $"{pulses} pulses, {atBaseline} at the baseline");
        Assert.Equal(800_000, rig.W.Economy.MoraleOf(0));
        Assert.Equal(800_000, rig.W.Economy.MoraleOf(1));                         // army 1 fought no battle: it never left the baseline
    }

    /// <summary>Baseline 800 (option (b)): below the baseline a fed, paid army recovers by floor(600 × 1000 × w ÷ year) per pulse and
    /// stops AT the baseline (750,000 → 761,666 → 773,332 → 784,998 → 796,664 → 800,000 → 800,000 …). At the default baseline (the cap)
    /// the same army recovers past 800,000 toward 1,000,000: lane E's ramp, unchanged.
    /// [MOR2, D-083 DB1: the battle that puts army 0 at 750,000 is now a LOSSLESS one (it ends at its start, 800) and the 50,000 is its
    /// entry cost; lane MOR's battle (a 50-point loss) would now leave 700,000 (MEASURED: the test failed before this input changed).
    /// The recovery sequence it pins is unchanged.]</summary>
    [Theory]
    [InlineData(800L)]
    [InlineData(1000L)]
    public void Baseline_BelowIt_TheRecoveryStopsAtTheBaseline(long baseline)
    {
        long start = MoraleRig.WarStart();
        var rates = LogisticsRates.Proposal() with { MoraleBaselinePerMille = baseline };
        var rig = MoraleRig.Build(MoraleRig.Armies(2, rates), start, weeks: 30);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, start + 3 * Day, 800, -1, 2_000, 2_000));         // [MOR2] 800,000 − 50,000 entry
        MoraleRig.Run(rig, start + 25 * Week);
        long m = 750_000, cap = baseline * 1000;
        var seen = new List<long>();
        foreach (var row in rig.W.Economy.Rows.Where(x => x.ArmyId == 0).OrderBy(x => x.PulseIndex))
        {
            long rec = MoraleRig.OverPulse(rates.MoraleRecoveryPerYear, row.EndMs - row.StartMs);
            m = Math.Min(cap, m + rec);
            Assert.Equal(m, row.MoraleEnd);
            seen.Add(row.MoraleEnd);
        }
        Assert.Equal(new[] { 761_666L, 773_332L, 784_998L, 796_664L, Math.Min(cap, 808_330L) }, seen.Take(5).ToArray());
        Assert.Equal(m, rig.W.Economy.MoraleOf(0));
        Assert.Equal(baseline == 800 ? 800_000 : 1_000_000, m);                  // 25 pulses: the default ramp reaches the cap
    }

    /// <summary>LAW 3: the decay of an excess is a per-sim-year rate integrated over the exact pulse length. The same victory in an era
    /// band with 14-day pulses (band 1; T2's PROPOSED table) decays by floor(600 × 1000 × 14 d ÷ 360 d) = 23,333 at the first pulse end,
    /// against 11,666 for a 7-day pulse (band 3). [MOR2, D-083 DB1: from 950,000, the lossless win less the 50,000 entry cost, instead of
    /// lane MOR's 1,000,000.]</summary>
    [Theory]
    [InlineData(1, 14L, 23_333L)]
    [InlineData(3, 7L, 11_666L)]
    public void Baseline_TheDecayFollowsThePulseLength(int band, long pulseDays, long decay)
    {
        var cal = ShippedCalendar();
        long start = cal.TurnStart(cal.Bands[band].FirstTurn + 1).Ms;
        var rates = LogisticsRates.Proposal() with { MoraleBaselinePerMille = 800 };
        var rig = MoraleRig.Build(MoraleRig.Armies(2, rates), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, start + 3 * Day, 800, 0, 2_000, 2_000));
        MoraleRig.Run(rig, start + pulseDays * Day);
        var first = rig.W.Economy.Rows.First(x => x.ArmyId == 0);
        Assert.Equal(pulseDays * Day, first.EndMs - first.StartMs);
        Assert.Equal(decay, MoraleRig.OverPulse(600, first.EndMs - first.StartMs));
        Assert.Equal(950_000 - decay, first.MoraleEnd);                             // [MOR2] the win less the entry cost, then the decay
    }

    /// <summary>[verification round 1, MOR-V1-04] Option (b), ABOVE the baseline, the hunger penalty still applies (morale proposal
    /// §4.4: "the hunger and arrears penalties apply as before"). Baseline 800; army 0 has no grain (train 0), so its hunger share over
    /// the first pulse is 1,000,000 ppm, and a win on day 3 lifts it to 1,000,000. At the pulse end, over the exact 7-day pulse w:
    /// the excess decays by min(200,000, floor(600 × 1000 × w ÷ year)) = 11,666, and the hunger penalty takes
    /// floor(2,400 × 1000 × 1,000,000 ppm × w ÷ (1,000,000 × year)) = 46,666, so 1,000,000 − 11,666 − 46,666 = 941,668 by hand.
    /// (A precedence slip that bound both penalties to the below-baseline branch gave 988,334 and survived the whole suite before
    /// this test: MEASURED.) Army 1, fed and paid, fought no battle and stays at the baseline.
    /// [MOR2, D-083 DB1: the win now lifts army 0 to 800,000 − 50,000 + 200,000 = 950,000 (the entry cost), so by hand 950,000 − 11,666
    /// − 46,666 = 891,668 (lane MOR's 1,000,000 and 941,668 failed before this update, MEASURED); the excess, 150,000, still exceeds a
    /// pulse's decay, so the decay term is the same 11,666.]</summary>
    [Fact]
    public void Baseline_AboveIt_TheHungerPenaltyStillApplies_ByHand()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rates = LogisticsRates.Proposal() with { MoraleBaselinePerMille = 800 };
        var setup = MoraleRig.Armies(2, rates);
        setup = setup with { Armies = new[] { setup.Armies[0] with { TrainGrain = 0 }, setup.Armies[1] } };
        var rig = MoraleRig.Build(setup, start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 800, 0, 2_000, 2_000));
        MoraleRig.Run(rig, start + Week);
        Assert.Equal(950_000, rig.W.MoraleRows.Single().AfterPpm);                  // [MOR2] the win (less the entry cost) lifted it above the baseline
        var row = rig.W.Economy.Rows.First(x => x.ArmyId == 0);
        Assert.Equal((start, start + Week), (row.StartMs, row.EndMs));
        Assert.Equal(1_000_000, row.HungerPpm);                                     // unfed over the whole pulse
        Assert.Equal(0, row.ArrearsEnd);
        long decay = MoraleRig.OverPulse(rates.MoraleElationDecayPerYear, Week), hunger = MoraleRig.OverPulse(rates.MoraleHungerPenaltyPerYear, Week);
        Assert.Equal((11_666L, 46_666L), (decay, hunger));
        Assert.Equal(950_000 - decay - hunger, row.MoraleEnd);                      // [MOR2]
        Assert.Equal(891_668, row.MoraleEnd);                                       // [MOR2] lane MOR's 941,668 less the 50,000 entry cost
        Assert.Equal(800_000, rig.W.Economy.Rows.First(x => x.ArmyId == 1).MoraleEnd);
    }

    /// <summary>[verification round 1, MOR-V1-04] The decay of an excess and the recovery toward the baseline each read their own
    /// per-year rate. Baseline 800, decay 1,200 ‰/yr (twice the recovery's 600). One battle on day 3: army 0 wins without loss (→
    /// 1,000,000), army 1 loses 50 (→ 750,000). At the 7-day pulse end, by hand: army 0 decays by floor(1,200 × 1000 × w ÷ year) =
    /// 23,333 to 976,667; army 1, fed, paid and loyal below the baseline, recovers by floor(600 × 1000 × w ÷ year) = 11,666 to
    /// 761,666. (The decay reading the recovery rate gave 988,334 and survived the whole suite before this test: MEASURED; the
    /// recovery reading the decay rate would give 773,332.)
    /// [MOR2, D-083 DB1: both armies also pay the 50,000 entry cost: army 0 → 950,000 and decays to 926,667; army 1 → 700,000 and
    /// recovers to 711,666 (lane MOR's 1,000,000 / 750,000 and 976,667 / 761,666 failed before this update, MEASURED). The two rates are
    /// still told apart: the decay read as the recovery would give 938,334; the recovery read as the decay 723,333.]</summary>
    [Fact]
    public void Baseline_TheDecayAndTheRecoveryEachReadTheirOwnRate_ByHand()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var rates = LogisticsRates.Proposal() with { MoraleBaselinePerMille = 800, MoraleElationDecayPerYear = 1_200 };
        Assert.Equal(600, rates.MoraleRecoveryPerYear);
        var rig = MoraleRig.Build(MoraleRig.Armies(2, rates), start);
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 800, 0, 2_000, 2_000));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 750, 0, 2_000, 2_000));
        MoraleRig.Run(rig, start + Week);
        Assert.Equal(new[] { 950_000L, 700_000L }, rig.W.MoraleRows.Select(x => x.AfterPpm).ToArray());   // [MOR2] less the entry cost
        var r0 = rig.W.Economy.Rows.First(x => x.ArmyId == 0);
        var r1 = rig.W.Economy.Rows.First(x => x.ArmyId == 1);
        foreach (var r in new[] { r0, r1 })
        {
            Assert.Equal((start, start + Week), (r.StartMs, r.EndMs));
            Assert.Equal((0L, 0L, ArmyCondition.Loyal), (r.HungerPpm, r.ArrearsEnd, r.ConditionEnd));   // fed, paid, loyal
        }
        Assert.Equal(23_333, MoraleRig.OverPulse(1_200, Week));
        Assert.Equal(11_666, MoraleRig.OverPulse(600, Week));
        Assert.Equal(926_667, r0.MoraleEnd);                                        // [MOR2] 950,000 − 23,333
        Assert.Equal(711_666, r1.MoraleEnd);                                        // [MOR2] 700,000 + 11,666
    }

    /// <summary>Lane E's world refuses a baseline off the scale or a negative decay rate.</summary>
    [Fact]
    public void Baseline_OffTheScale_IsRefused()
    {
        long start = MoraleRig.WarStart();
        foreach (var bad in new[] { LogisticsRates.Proposal() with { MoraleBaselinePerMille = 1001 }, LogisticsRates.Proposal() with { MoraleBaselinePerMille = -1 },
                                    LogisticsRates.Proposal() with { MoraleElationDecayPerYear = -1 } })
            Assert.Throws<ArgumentOutOfRangeException>(() => MoraleRig.Build(MoraleRig.Armies(2, bad), start));
    }

    // ------------------------------------------------------------------ ordering at one instant (tie-dense)

    /// <summary>TIE-DENSE: four armies' battles all end at one instant. Whatever order they are scheduled in (six permutations), they
    /// settle in army-id order (the event key's SourceId, before the scheduling sequence), each army's morale is its own battle's result,
    /// and the state hash is the same.</summary>
    [Fact]
    public void ManyArmiesBattlesAtOneInstant_SettleInArmyIdOrder_WhateverTheSchedulingOrder()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var battles = new[]
        {
            MoraleRig.Battle(0, 0, at, 700, 0, 4_000, 4_000),
            MoraleRig.Battle(1, 1, at, 300, 0, 4_000, 4_000),
            MoraleRig.Battle(2, 0, at, 650, 0, 4_000, 4_000),
            MoraleRig.Battle(3, 1, at, 450, 0, 4_000, 4_000),
        };
        string? hash = null;
        long[]? morale = null;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var rig = MoraleRig.Build(MoraleRig.Armies(4), start);
            foreach (int i in Permutation(4, seed)) rig.W.ScheduleBattle(rig.S, battles[i]);
            MoraleRig.Run(rig, at + 1);
            Assert.Equal(new[] { 0, 1, 2, 3 }, rig.W.MoraleRows.Select(x => x.ArmyId).ToArray());
            var m = new long[4];
            for (int a = 0; a < 4; a++) m[a] = rig.W.Economy.MoraleOf(a);
            // [MOR2, D-083 DB1] every army entered: lane MOR's 900,000, 300,000, 850,000, 450,000 less the 50,000 entry cost each
            Assert.Equal(new[] { 850_000L, 250_000L, 800_000L, 400_000L }, m);
            morale ??= m;
            hash ??= rig.W.StateHashHex();
            Assert.Equal(hash, rig.W.StateHashHex());
        }
    }

    /// <summary>TIE-DENSE: two battles of ONE army at one instant settle in the order they were scheduled (the event key's sequence: a
    /// stable integer tie-break), each from the morale the one before left. Order matters only where the scale's top or bottom binds:
    /// a win (rise 300,000, no loss) then a defeat (decrease 400,000) gives 800,000 → 1,000,000 (not 1,100,000) → 600,000; the reverse
    /// order 800,000 → 400,000 → 700,000. (Morale proposal §5.3: the M7.15 hand-off should settle one army's battles of one instant
    /// together; the stand-in settles them one by one.)
    /// [MOR2, D-083 DB1: each battle also costs the 50,000 entry cost (two battles, two entries: DB1-R2 (a) is once per battle). Win
    /// first: 800,000 − 50,000 + 300,000 = 1,050,000 → 1,000,000 (the top binds) → 1,000,000 − 50,000 − 400,000 = 550,000. Defeat
    /// first: 800,000 − 50,000 − 400,000 = 350,000 → 350,000 − 50,000 + 300,000 = 600,000. Lane MOR's 600,000 and 700,000 failed
    /// before this update (MEASURED); the order still matters only through the top of the scale.]</summary>
    [Theory]
    [InlineData(true, 550_000L)]
    [InlineData(false, 600_000L)]
    public void TwoBattlesOfOneArmyAtOneInstant_SettleInTheOrderTheyWereScheduled(bool winFirst, long expected)
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        var win = MoraleRig.Battle(0, 0, at, 800, 0, 6_000, 2_000);
        var loss = MoraleRig.Battle(0, 0, at, 400, 1, 2_000, 2_000);
        var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
        foreach (var b in winFirst ? new[] { win, loss } : new[] { loss, win }) rig.W.ScheduleBattle(rig.S, b);
        MoraleRig.Run(rig, at + 1);
        Assert.Equal(expected, rig.W.Economy.MoraleOf(0));
        var rows = rig.W.MoraleRows;
        Assert.Equal(2, rows.Count);
        Assert.Equal(winFirst ? new[] { 300_000L, 0L } : new[] { 0L, 300_000L }, rows.Select(x => x.RisePpm).ToArray());
        Assert.Equal(rows[0].AfterPpm, rows[1].BeforePpm);
    }

    // ------------------------------------------------------------------ the new serialized row (POPULATED table)

    /// <summary>POPULATED-TABLE TEST of <see cref="MoraleSettlementRow"/> from a real run (winners, losers, no winner, a medical restore,
    /// varied strengths and instants): exact ExpectedLength (5 + 93 per row), bit-exact round trip, the same bytes and hash again, the
    /// world's encoded table equal to it; and every pair of same-typed fields is told apart by some row (lane D's field-swap guard).
    /// [MOR2, D-083 DB1: the row is codec version 2 (Entered, EntryInBattle, EntryPpm appended: 110 bytes). The run adds a battle that
    /// army 3 never entered (Entered 0, EntryPpm 0) and one whose battle charged 50 at the entry (EntryInBattle 50), so every new field
    /// is told apart from every same-typed field; a version-1 table, a version byte of 3 and an Entered byte of 2 are refused.]
    /// [MOR3, D-085 DD1, 2026-11-11: codec version 3 (Fought appended: 111 bytes). MEASURED move: the length 5 + 110 × rows → 5 + 111 ×
    /// rows; the refused versions { 1, 3 } → { 1, 2, 4 } (3 is now the current version). Every stand-in battle here is fought (Fought 1);
    /// the unfought rows, and a Fought byte other than 0 or 1, are pinned in NoFightingTests.DD1_SettlementRows_Version3_….]</summary>
    [Fact]
    public void MoraleRows_PopulatedTable_RoundTripBitExact_WithExactLength()
    {
        long start = MoraleRig.WarStart();
        var rig = MoraleRig.Build(MoraleRig.Armies(4), start, pools: new[] { new SupportPool(0, 40, 0, 0, 0) }, probes: new[] { start - 1 });
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, start + 2 * Day, 400, 1, 1_500, 2_000));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(3, 1, start + 3 * Day, 650, 0, 2_000, 4_000));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, start + 3 * Day, 600, 0, 2_000, 4_000) with { EntryChargedInBattle = 50 });   // [MOR2]
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(2, 0, start + 9 * Day + 7, 720, -1, 3_000, 0));
        rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(2, 0, start + 12 * Day, 790, 0, 5_000, 0));
        rig.W.ScheduleBattle(rig.S, MoraleRig.NeverEntered(3, 1, start + 5 * Day, 0, 2_000, 4_000));                                  // [MOR2]
        SupportRig.Drive(rig, start + 13 * Day, t => t == start - 1 ? new[] { SupportRig.Allocate(0, 0, SupportKind.FieldMedical, 40) } : Array.Empty<(int, long, long)>());
        var rows = rig.W.MoraleRows.ToArray();
        Assert.Equal(6, rows.Length);
        Assert.Contains(rows, x => x.Restore > 0);
        Assert.Contains(rows, x => x.RisePpm > 0);
        Assert.Contains(rows, x => x.WinnerSide == -1);
        Assert.Contains(rows, x => x.Entered == 0 && x.EntryPpm == 0 && x.AfterPpm == x.BeforePpm);                                     // [MOR2]
        Assert.Contains(rows, x => x.EntryInBattle == 50);                                                                            // [MOR2]
        var bytes = MoraleCodec.Encode(rows);
        Assert.Equal(MoraleCodec.ExpectedLength(rows.Length), bytes.Length);
        Assert.Equal(5 + 111 * rows.Length, bytes.Length);       // [MOR2] v2 (110); [MOR3, D-085 DD1] v3: Fought appended, 111 bytes a row
        var back = MoraleCodec.Decode(bytes);
        Assert.Equal(rows, back);
        var again = MoraleCodec.Encode(back);
        Assert.Equal(bytes, again);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(again));
        Assert.Equal(bytes, rig.W.EncodedTables()[4]);
        RowFieldCoverage.AssertEverySameTypedFieldPairDiffers(rows);
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(bytes[..^1]));
        foreach (byte v in new byte[] { 1, 2, 4 })               // [MOR2] v1 refused; [MOR3] v2 refused too, and the next version (was { 1, 3 })
        {
            var badVersion = (byte[])bytes.Clone(); badVersion[0] = v;
            Assert.Throws<FormatException>(() => MoraleCodec.Decode(badVersion));
        }
        var badEntered = (byte[])bytes.Clone(); badEntered[MoraleCodec.HeaderBytes + 93] = 2;                                         // row 0's Entered
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(badEntered));
        Assert.Throws<ArgumentOutOfRangeException>(() => MoraleCodec.Encode(new[] { rows[0] with { Entered = 2 } }));
    }

    /// <summary>[verification round 1, MOR-V1-10] A table whose count field is negative or claims more rows than its bytes hold is a
    /// FORMAT error, refused with FormatException like every other malformed table: before the fix a 5-byte table counting int.MaxValue
    /// rows threw OverflowException from the checked length arithmetic (MEASURED). Counts just above and below what the bytes hold
    /// are refused too, and the exact count decodes.</summary>
    [Fact]
    public void MoraleCodec_RefusesAHugeOrNegativeCount_AsAFormatError()
    {
        byte[] Header(int count, int extraBytes)
        {
            var b = new byte[MoraleCodec.HeaderBytes + extraBytes];
            b[0] = MoraleCodec.Version;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(1), count);
            return b;
        }
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(Header(int.MaxValue, 0)));
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(Header(int.MaxValue / MoraleCodec.SettlementRowBytes + 1, 0)));   // ExpectedLength would overflow
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(Header(-1, 0)));
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(Header(int.MinValue, 0)));
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(Header(2, MoraleCodec.SettlementRowBytes)));     // one row's bytes, two counted
        Assert.Throws<FormatException>(() => MoraleCodec.Decode(Header(0, MoraleCodec.SettlementRowBytes)));     // trailing bytes
        Assert.Single(MoraleCodec.Decode(Header(1, MoraleCodec.SettlementRowBytes)));
        Assert.Empty(MoraleCodec.Decode(Header(0, 0)));
    }

    /// <summary>The support world's state hash carries the morale table. Two identical runs give the same hash. [verification round 1,
    /// MOR-V1-05] Two runs that differ ONLY in a field that the morale table alone records — the losing army's BeatenPersons (its rise
    /// is 0 either way, so its morale, lane D's battle row and lane E's economy are the same) — give equal lane D tables, a different
    /// morale table and a different state hash. (The earlier version compared winner 0 with winner 1, which also changes lane D's
    /// battle row, so it passed with the morale table left out of the hash: MEASURED, that mutant survived the whole suite.)</summary>
    [Fact]
    public void TheStateHash_CarriesTheMoraleTable()
    {
        long start = MoraleRig.WarStart(), at = start + 3 * Day;
        (string Hash, List<byte[]> Tables, long Loser) Run(long loserBeaten)
        {
            var rig = MoraleRig.Build(MoraleRig.Armies(2), start);
            rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(0, 0, at, 700, 0, 2_000, 2_000, held: true));
            rig.W.ScheduleBattle(rig.S, MoraleRig.Battle(1, 1, at, 600, 0, loserBeaten, 2_000));
            MoraleRig.Run(rig, at + 1);
            return (rig.W.StateHashHex(), rig.W.EncodedTables(), rig.W.Economy.MoraleOf(1));
        }
        var a = Run(2_000); var a2 = Run(2_000); var b = Run(3_000);
        Assert.Equal(a.Hash, a2.Hash);
        Assert.Equal((550_000L, 550_000L), (a.Loser, b.Loser));                          // no rise for the loser either way ([MOR2] − 50,000 entry)
        Assert.Equal(5, a.Tables.Count);
        for (int i = 0; i < 4; i++) Assert.Equal(a.Tables[i], b.Tables[i]);             // lane D's tables are the same ...
        Assert.NotEqual(a.Tables[4], b.Tables[4]);                                       // ... the morale table is not ...
        Assert.NotEqual(a.Hash, b.Hash);                                                 // ... and the state hash tells them apart
    }
}
