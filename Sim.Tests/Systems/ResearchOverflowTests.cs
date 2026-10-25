using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-050 (D-064) — IN-STEP RESEARCH OVERFLOW (tuning.researchOverflow). Before: a lane's points funded ONE node per step and
/// RP past that node's remaining cost was discarded (D-044 R20-D), so a cheap node wasted most of a large turn's RP. Now the
/// remainder funds the lane's next fundable node — the destination's route, then the free-lane assist — read from PREV, each
/// node at most once per step; RP with nothing fundable is still lost, CP stays banked. Off: the one-node engine exactly.
/// Every test measures the rig's own RP (ResearchQuery.ResearchPointPool) and sizes the costs from it, so the arithmetic in
/// each comment is the arithmetic the engine does.
/// </summary>
public class ResearchOverflowTests
{
    private static readonly PolityId P = new(Player);

    /// <summary>The rig needs a Civics tree (D-044 R12); this civic is never funded (no Civics destination).</summary>
    private static readonly Node Filler = new(2001, "cv");

    /// <summary>The rig's RP per step for <see cref="PlayerWorld"/> (population-driven, content-independent).</summary>
    private static readonly Lazy<double> Rp = new(() =>
    {
        ResearchContent probe = new Spec { Stage = "x", Technologies = { new Node(1, "x") }, Civics = { Filler } }.Load();
        return ResearchQuery.ResearchPointPool(PlayerWorld(), probe, P);
    });

    /// <summary>Three roots x, y, z (0.3 R each) and a destination d needing all three (1.0 R): one step of R RP can pay
    /// x, y and z (0.9 R) but not d — d is not available until x, y and z are complete on PREV.</summary>
    private static ResearchContent Fan(bool overflow)
    {
        double r = Rp.Value;
        var s = new Spec { Stage = "x", DestinationRouting = true, ResearchOverflow = overflow };
        s.Civics.Add(Filler);
        s.Technologies.AddRange([
            new Node(1, "x", Cost: 0.3 * r), new Node(2, "y", Cost: 0.3 * r), new Node(3, "z", Cost: 0.3 * r),
            new Node(4, "d", "x AND y AND z", Cost: 1.0 * r),
        ]);
        return s.Load();
    }

    private static WorldState Toward(ResearchContent c, string dest)
    {
        WorldState w = PlayerWorld();
        w.ResearchTargets.Add(new ResearchTargetRow(P, c.Nodes[c.IndexOfId(dest)].Key));
        return w;
    }

    [Fact]
    public void Overflow_PaysEveryAvailableRouteNode_InOneStep_AndOffPaysOne()
    {
        Assert.True(Rp.Value > 0.0);
        // ON: R RP → x (0.3 R) complete, 0.7 R left → y (0.3 R) complete, 0.4 R left → z (0.3 R) complete, 0.1 R left →
        // d not available on PREV, nothing else on the route, no other lane: the 0.1 R is lost (no RP bank, D-044 R20-D).
        ResearchContent on = Fan(overflow: true);
        WorldState next = Executor(on).Step(Toward(on, "d"));
        Assert.True(Done(next, 1) && Done(next, 2) && Done(next, 3));
        Assert.False(Done(next, 4));
        Assert.Equal(0.0, Progress(next, 4));
        // OFF (the twin): R RP → x only (0.3 R); 0.7 R discarded; y and z untouched.
        ResearchContent off = Fan(overflow: false);
        WorldState twin = Executor(off).Step(Toward(off, "d"));
        Assert.True(Done(twin, 1));
        Assert.False(Done(twin, 2) || Done(twin, 3));
        Assert.Equal(0.0, Progress(twin, 2));
        // and the destination itself completes on the NEXT step under overflow (its prerequisites now on PREV).
        Assert.True(Done(Executor(on).Step(next), 4));
    }

    [Fact]
    public void Overflow_PartiallyPaysTheLastNodeItReaches_AndTheProgressIsKept()
    {
        double r = Rp.Value;
        var s = new Spec { Stage = "x", DestinationRouting = true, ResearchOverflow = true };
        // x 0.6 R then y 1.0 R: x completes, 0.4 R lands on y (y is the next-cheapest remaining route node).
        s.Civics.Add(Filler);
        s.Technologies.AddRange([new Node(1, "x", Cost: 0.6 * r), new Node(2, "y", Cost: 1.0 * r), new Node(3, "d", "x AND y", Cost: r)]);
        ResearchContent c = s.Load();
        WorldState next = Executor(c).Step(Toward(c, "d"));
        Assert.True(Done(next, 1));
        Assert.Equal(r - 0.6 * r, Progress(next, 2), 9);   // double arithmetic of R − 0.6 R, not a tolerance on the rule
        Assert.False(Done(next, 2));
    }

    [Fact]
    public void Overflow_WithNothingFundable_TheRemainderIsLost_NotBanked()
    {
        // A lone available destination x (0.2 R): paid, then nothing else is fundable — 0.8 R lost, and nothing is
        // stored for the next step (the next step with no destination funds nothing).
        double r = Rp.Value;
        var s = new Spec { Stage = "x", DestinationRouting = true, ResearchOverflow = true };
        s.Civics.Add(Filler);
        s.Technologies.AddRange([new Node(1, "x", Cost: 0.2 * r), new Node(2, "w", "x", Cost: 5.0 * r)]);
        ResearchContent c = s.Load();
        WorldState next = Executor(c).Step(Toward(c, "x"));
        Assert.True(Done(next, 1));
        Assert.Equal(0.0, Progress(next, 2));
        WorldState after = Executor(c).Step(next);   // no destination now: w is available but nobody chose it
        Assert.Equal(0.0, Progress(after, 2));
    }

    [Fact]
    public void Overflow_CivicsSpendTheBankAcrossSeveralCivics_AndTheIdentityHolds()
    {
        // Civics lane: CP accrues into the bank; with overflow the bank pays c1 then c2 in one step; the rest stays banked.
        var s = new Spec { Stage = "t", DestinationRouting = true, ResearchOverflow = true, CivicsPoints = (true, 1.0, 0.5) };
        s.Technologies.Add(new Node(1, "t", Cost: 400_000.0));
        s.Civics.AddRange([new Node(1001, "c1", Cost: 100.0), new Node(1002, "c2", Cost: 100.0), new Node(1003, "cd", "c1 AND c2", Cost: 400_000.0)]);
        ResearchContent c = s.Load();
        WorldState w = Toward(c, "cd");
        long generated = ResearchQuery.CivicsPointsMilli(w, c, P, 10.0);
        Assert.True(generated > 200_000L);   // more than both civics: overflow has something to carry
        WorldState next = Executor(c).Step(w);
        Assert.True(Done(next, 1001) && Done(next, 1002));
        Assert.Equal(1, next.CivicsPoints.Count);
        CivicsPointsRow account = next.CivicsPoints[0];
        Assert.Equal(account.GeneratedMilli - account.SpentMilli, account.BalanceMilli);   // ADR-043 §5 identity
        Assert.Equal(200_000L, account.SpentMilli);   // exactly the two costs (100 CP each), in milli-CP
        Assert.Equal(generated - 200_000L, account.BalanceMilli);
    }

    [Fact]
    public void Overflow_ARepeatableGainsAtMostOneLevelPerStep()
    {
        double r = Rp.Value;
        var s = new Spec { Stage = "x", DestinationRouting = true, ResearchOverflow = true, RepeatableLevels = true };
        s.Civics.Add(Filler);
        s.Technologies.Add(new Node(1, "x", Cost: 0.1 * r, Age: "A9",
            Levels: new LevelRule(1.2, [("research.researchPointsCoefficient", 0.1)])));
        s.Recursive.Add("x");   // a levelled repeatable (ADR-049), not a complete-once node
        ResearchContent c = s.Load();
        Assert.True(c.Nodes[0].Levels is not null);
        // 0.1 R per level and R RP: a looping engine would buy several levels in this step. Each node is paid at most once.
        WorldState next = Executor(c).Step(Toward(c, "x"));
        Assert.Equal(1, ResearchQuery.Level(next, P, c.Nodes[0].Key));
        // The node really is levelled: aimed at again, the next step buys level 2 (complete-once would stay at 1).
        next.ResearchTargets.Add(new ResearchTargetRow(P, c.Nodes[0].Key));
        Assert.Equal(2, ResearchQuery.Level(Executor(c).Step(next), P, c.Nodes[0].Key));
    }

    [Fact]
    public void Overflow_TwoCheapRepeatables_EachGainOneLevel_AndTheStepTerminates()
    {
        // The lane variant is where a loop could live: after the destination x its fallback takes the lane's cheapest available
        // node, and a just-paid repeatable is available again at its next level. 0.1 R per level and R RP: a looping engine would
        // buy several levels of each. Each node is paid at most once per step: x then y, one level each, the rest of the step's
        // RP is lost (nothing else fundable) — and the step returns.
        double r = Rp.Value;
        var s = new Spec { Stage = "x", DestinationRouting = true, ResearchOverflow = true, ResearchOverflowLane = true, RepeatableLevels = true };
        s.Civics.Add(Filler);
        var rule = new LevelRule(1.2, [("research.researchPointsCoefficient", 0.1)]);
        s.Technologies.AddRange([new Node(1, "x", Cost: 0.1 * r, Age: "A9", Levels: rule),
            new Node(2, "y", Cost: 0.1 * r, Age: "A9", Levels: rule)]);
        s.Recursive.AddRange(["x", "y"]);
        ResearchContent c = s.Load();
        Assert.True(c.Nodes[0].Levels is not null && c.Nodes[1].Levels is not null);
        WorldState next = Executor(c).Step(Toward(c, "x"));
        Assert.Equal((1, 1), (ResearchQuery.Level(next, P, c.Nodes[0].Key), ResearchQuery.Level(next, P, c.Nodes[1].Key)));
        Assert.Equal(0.0, Progress(next, 1));   // no progress carried into x's level 2 within the step
        Assert.Equal(0.0, Progress(next, 2));
    }

    [Fact]
    public void Overflow_ADependentUnlockedThisStep_IsNotPaidUntilTheNextStep()
    {
        // a (0.1 R) → b (0.1 R), destination b. Step 1 pays a; b is not available on PREV, so the 0.9 R left is lost, not
        // spent on b. Step 2 pays b. The prerequisite semantics (availability read from PREV) are unchanged by overflow.
        double r = Rp.Value;
        var s = new Spec { Stage = "a", DestinationRouting = true, ResearchOverflow = true };
        s.Civics.Add(Filler);
        s.Technologies.AddRange([new Node(1, "a", Cost: 0.1 * r), new Node(2, "b", "a", Cost: 0.1 * r)]);
        ResearchContent c = s.Load();
        WorldState w = Toward(c, "b");
        WorldState one = Executor(c).Step(w);
        Assert.True(Done(one, 1));
        Assert.False(Done(one, 2));
        Assert.Equal(0.0, Progress(one, 2));
        Assert.True(Done(Executor(c).Step(one), 2));
    }

    [Fact]
    public void Overflow_KeepsTheBanksSeparate_RpNeverPaysACivic_AndCivicProgressIsExactlyTheCpSpent()
    {
        // The technology destination td needs tech x (0.1 R) AND civic cv (huge). The tech lane overflows past x but may
        // fund only technologies: RP lands on nothing else. The civics lane (no destination) assists td's route: cv is paid
        // from the CP bank only, so cv's progress equals the CP spent, to the milli-CP.
        double r = Rp.Value;
        var s = new Spec { Stage = "x", DestinationRouting = true, ResearchOverflow = true, CivicsPoints = (true, 1.0, 0.5) };
        s.Technologies.AddRange([new Node(1, "x", Cost: 0.1 * r), new Node(3, "td", "x AND cv", Cost: r)]);
        s.Civics.Add(new Node(1001, "cv", Cost: 400_000.0));
        ResearchContent c = s.Load();
        WorldState next = Executor(c).Step(Toward(c, "td"));
        Assert.True(Done(next, 1));
        Assert.Equal(0.0, Progress(next, 3));
        CivicsPointsRow account = next.CivicsPoints[0];
        Assert.Equal(account.SpentMilli / 1000.0, Progress(next, 1001));
        Assert.Equal(account.GeneratedMilli - account.SpentMilli, account.BalanceMilli);
    }

    [Fact]
    public void LaneVariant_AfterTheRouteIsExhausted_PaysTheCheapestAvailableNodeOfTheLane_RouteOnlyDoesNot()
    {
        // Destination d needs x (0.3 R); an off-route root o (0.3 R) is also available. Route-only overflow pays x and
        // stops (0.7 R lost); the lane variant then pays o, the cheapest available technology (0.4 R lost).
        double r = Rp.Value;
        ResearchContent Load(bool lane)
        {
            var s = new Spec { Stage = "x", DestinationRouting = true, ResearchOverflow = true, ResearchOverflowLane = lane };
            s.Civics.Add(Filler);
            s.Technologies.AddRange([new Node(1, "x", Cost: 0.3 * r), new Node(2, "o", Cost: 0.3 * r), new Node(3, "d", "x", Cost: r)]);
            return s.Load();
        }
        ResearchContent route = Load(lane: false), lane = Load(lane: true);
        WorldState a = Executor(route).Step(Toward(route, "d"));
        WorldState b = Executor(lane).Step(Toward(lane, "d"));
        Assert.True(Done(a, 1) && Done(b, 1));
        Assert.False(Done(a, 2));
        Assert.True(Done(b, 2));
        Assert.Equal(0.0, Progress(b, 3));   // d is still next-step only
    }
}
