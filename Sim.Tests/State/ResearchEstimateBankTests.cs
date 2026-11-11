using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.State;

/// <summary>
/// D-084 (lane RT), research-tree audit §5: the inspector's (InfoQuery) "about N turns" of a civic against what the engine
/// does with the banked Civics Points. The rig (ResearchRigs) is the shipped engine's switches — two lanes, destination
/// routing, overflow off — with one player polity of 10 000 people: G = 1 000 CP a dt-10 turn exactly, so every expected
/// value is exact. ResearchSystem 3b: each End Turn ACCRUES G into the bank, then the civic the lane funds DRAWS
/// min(bank, remaining) — so a bank that covers the remaining cost completes the civic at the first End Turn.
/// </summary>
public class ResearchEstimateBankTests
{
    private static readonly PolityId P = new(Player);
    private const double G = 1_000.0;
    private const int Civic = 2001;

    /// <summary>The shipped switches on the rig with one civic of <paramref name="cost"/> CP (CP a sim-year =
    /// <paramref name="coefficient"/> × P^0.5).</summary>
    private static ResearchContent Rig(double cost, double coefficient = 1.0)
    {
        var s = new Spec { Stage = "t0", DestinationRouting = true, CivicsPoints = (true, coefficient, 0.5) };
        s.Technologies.Add(new Node(1, "t0", Cost: 5_000.0));
        s.Technologies.Add(new Node(2, "t1", Cost: 5_000.0));
        s.Civics.Add(new Node(Civic, "cv", Cost: cost));
        ResearchContent c = s.Load();
        Assert.False(c.Tuning.ResearchOverflow);
        Assert.True(c.Tuning.TwoLanes && c.Tuning.DestinationRouting);
        return c;
    }

    /// <summary>The player world (10 000 people; with <paramref name="empty"/>, the player holds no settlement, so its RP and
    /// CP a turn are 0) with <paramref name="bankCp"/> banked (Balance = Generated − Spent) and <paramref name="progress"/> on
    /// the civic.</summary>
    private static WorldState World(double bankCp, double progress = 0.0, bool empty = false)
    {
        WorldState w = empty ? ResearchRigs.World([Player], [], 0) : PlayerWorld();
        long milli = (long)Math.Round(bankCp * 1000.0);
        if (milli > 0) w.CivicsPoints.Add(new CivicsPointsRow(P, milli, milli, 0L));
        if (progress > 0) w.ResearchProgress.Add(new ResearchProgressRow(P, Key(Civic), progress));
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(w.CivicsPoints));
        return w;
    }

    private static SimConfig Cfg(ResearchContent c) =>
        TestConfigs.Sim() with { Research = c, Ages = null, Roads = null, Governance = null, Institutions = null };

    private static string NodeLine(IReadOnlyWorldState w, ResearchContent c, int key) =>
        InfoQuery.Card(w, Cfg(c), P, InfoSubject.Node(Key(key)), ActionQueryContext.ForNextStep(w, FlatEra(10.0)),
            actions: ImmutableArray<ActionDescriptor>.Empty).StatusLine;

    private static readonly Regex About = new(@"\(about (\d+) turns? at the current rate\)", RegexOptions.CultureInvariant);

    /// <summary>The inspector's turns, or null when the line gives none.</summary>
    private static int? Turns(string line) =>
        About.Match(line) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;

    /// <summary>End Turns until the civic is complete once it is ordered (research-only steps), or null within the limit.</summary>
    private static int? EngineTurns(ResearchContent c, WorldState w, int key, int limit = 40)
    {
        WorldState x = w.Clone();
        x.ResearchTargets.Add(new ResearchTargetRow(P, Key(key)));
        TurnExecutor ex = Executor(c);
        for (int t = 1; t <= limit; t++)
        {
            x = ex.Step(x);
            if (Done(x, key)) return t;
        }
        return null;
    }

    // ------------------------------------------------------------------ the shared function (D-084): exact numbers

    [Theory]
    [InlineData(500.0, 0.0, 50.0, 600.0, 1L)]    // the bank covers the cost: ONE End Turn (the audit's formula gives 0; see below)
    [InlineData(500.0, 0.0, 50.0, 450.0, 1L)]    // 450 banked + 50 accrued in the step = 500
    [InlineData(500.0, 0.0, 50.0, 449.0, 2L)]    // one short: a second End Turn
    [InlineData(500.0, 0.0, 50.0, 0.0, 10L)]     // no bank: ceil(500 / 50), the estimate as it was
    [InlineData(500.0, 300.0, 50.0, 300.0, 1L)]  // progress plus bank above the cost
    [InlineData(500.0, 0.0, 0.0, 600.0, 1L)]     // a zero rate: the bank alone pays
    [InlineData(500.0, 500.0, 50.0, 0.0, 1L)]    // paid in full but not complete: it completes at the next End Turn
    [InlineData(500.0, 0.0, 50.0, -5.0, 10L)]    // a negative bank counts as none
    public void ResearchTurns_IsTheNumberOfEndTurnsTheEnginePaysIn(double cost, double progress, double rate, double bank, long turns) =>
        // ResearchSystem 3b accrues the turn's CP into the bank and then the funded civic draws min(bank, remaining) in the SAME
        // step, so the civic completes at the first End Turn when the bank covers it — never in 0 End Turns (the audit's
        // max(0, cost − progress − bank) / rate gives 0 there; the engine, stepped in TheInspectorsEstimate_… below, takes 1).
        Assert.Equal(turns, InfoQuery.ResearchTurns(cost, progress, rate, bank));

    [Fact]
    public void ResearchTurns_AtAZeroOrUnknownRate_WithABankShortOfTheCost_GivesNoEstimate()
    {
        Assert.Null(InfoQuery.ResearchTurns(500.0, 0.0, 0.0, 100.0));
        Assert.Null(InfoQuery.ResearchTurns(500.0, 0.0, -1.0, 0.0));
        Assert.Null(InfoQuery.ResearchTurns(500.0, 0.0, double.NaN, 0.0));
    }

    [Fact]
    public void ResearchBank_IsTheCivicsBank_ForACivic_AndNone_ForATechnology()
    {
        ResearchContent c = Rig(10_000.0);
        WorldState w = World(3_000.0);
        Assert.Equal(3_000.0, InfoQuery.ResearchBank(w, c, P, c.IndexOf(Key(Civic))));
        Assert.Equal(0.0, InfoQuery.ResearchBank(w, c, P, c.IndexOf(Key(2))));
        // The one-pool model (no civicsPoints tuning): civics are paid in RP and there is no bank to count.
        var onePool = new Spec { Stage = "t0" };
        onePool.Technologies.Add(new Node(1, "t0", Cost: 5_000.0));
        onePool.Civics.Add(new Node(Civic, "cv", Cost: 10_000.0));
        ResearchContent one = onePool.Load();
        Assert.False(one.Tuning.TwoLanes);
        Assert.Equal(0.0, InfoQuery.ResearchBank(w, one, P, one.IndexOf(Key(Civic))));
    }

    // ------------------------------------------------------------------ the inspector against the engine (rig)

    [Theory]
    [InlineData(10_000.0, 12_000.0, 0.0, 1)]   // the bank covers the cost: one End Turn (the audit's formula said 0)
    [InlineData(10_000.0, 9_000.0, 0.0, 1)]    // bank + one turn's CP = the cost exactly
    [InlineData(10_000.0, 8_999.0, 0.0, 2)]    // one CP short: a second End Turn
    [InlineData(10_000.0, 4_500.0, 0.0, 6)]    // ceil(5 500 / 1 000)
    [InlineData(10_000.0, 0.0, 0.0, 10)]       // no bank: the estimate as before
    [InlineData(10_000.0, 5_000.0, 6_000.0, 1)] // progress plus bank above the cost
    public void TheInspectorsEstimate_IsTheEndTurnsTheEngineTakes(double cost, double bank, double progress, int turns)
    {
        ResearchContent c = Rig(cost);
        WorldState w = World(bank, progress);
        Assert.Equal(G * 1000.0, ResearchQuery.CivicsPointsMilli(w, c, P, 10.0));   // rig calibration, measured
        Assert.Equal(turns, EngineTurns(c, w, Civic));
        Assert.Equal(turns, Turns(NodeLine(w, c, Civic)));
    }

    [Fact]
    public void AtAZeroRate_ACoveringBankStillCompletesInOneTurn_AndAShortOneGivesNoEstimate()
    {
        // A zero CP rate (a polity holding no settlement: no population, so no RP or CP a turn — the loader refuses a zero
        // coefficient). The bank alone pays: a covering bank completes the civic at the first End Turn; a short one never does.
        ResearchContent c = Rig(10_000.0);
        WorldState covered = World(12_000.0, empty: true), shortOf = World(9_000.0, empty: true);
        Assert.Equal(0L, ResearchQuery.CivicsPointsMilli(covered, c, P, 10.0));
        Assert.Equal(1, EngineTurns(c, covered, Civic));
        Assert.Equal(1, Turns(NodeLine(covered, c, Civic)));
        Assert.Null(EngineTurns(c, shortOf, Civic));
        Assert.Null(Turns(NodeLine(shortOf, c, Civic)));
        Assert.Equal("Open to research now: 10,000 civics points.", NodeLine(shortOf, c, Civic));
    }

    [Fact]
    public void TheInspectorsLine_SaysOneTurnInTheSingular_AndMoreInThePlural()
    {
        // D-084 verification round 1 (RT-R1-06 b): once the bank counts, one End Turn is a common estimate, and the line read
        // "about 1 turns". InfoQuery.TurnsPhrase writes the words of the card's, the pane's and this line's estimate.
        ResearchContent c = Rig(10_000.0);
        Assert.Equal("Open to research now: 10,000 civics points (about 1 turn at the current rate).", NodeLine(World(12_000.0), c, Civic));
        Assert.Equal("Open to research now: 10,000 civics points (about 6 turns at the current rate).", NodeLine(World(4_500.0), c, Civic));
        Assert.Equal("1 turn", InfoQuery.TurnsPhrase(1L));
        Assert.Equal("2 turns", InfoQuery.TurnsPhrase(2L));
        Assert.Equal("1000 turns", InfoQuery.TurnsPhrase(1_000L));
    }

    [Fact]
    public void ATechnologysEstimate_IsNotShortenedByTheCivicsBank()
    {
        // Technology has no general RP bank (D-044 R20-D; the rig has no ADR-051 reserve): 5 000 RP at 1 000 RP a turn is
        // 5 turns whatever the CP bank holds.
        ResearchContent c = Rig(10_000.0);
        WorldState w = World(1_000_000.0);
        Assert.Equal(5, Turns(NodeLine(w, c, 2)));
        Assert.Equal(5, EngineTurns(c, w, 2));
    }
}
