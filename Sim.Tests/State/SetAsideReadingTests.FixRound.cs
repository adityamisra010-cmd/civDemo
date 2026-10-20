using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.State;

/// <summary>
/// D-055 (2026-10-17; batch 4, track T1b, FIX ROUND — the adjudicated review findings T1b-L1-F2 and T1b-L1-F4): the queries
/// as the corrected displays read them, on <see cref="SetAsideReadingTests"/>' rigs, each with its control and its named
/// semantic mutant.
/// </summary>
public partial class SetAsideReadingTests
{
    /// <summary>
    /// FIX ROUND F4 — A QUEUED HEAD BEHIND EARLIER DRAWS: its card's Met AND its wait line count what the heads ahead of it
    /// draw this step as well as what they set aside, and name the two apart — so Met is false and the card says it will wait
    /// EXACTLY when HeadStatus says HeldForEarlier. 15 stone and 500 timber at S3. S0's granary (turn 0, p = 750,000, builders)
    /// completes, drawing 5 stone; S1's (turn 1, not started, no builders) sets aside the other 10; S2's (turn 2, p = 791,300,
    /// builders) has 4 stone to go and is HeldForEarlier naming S1. Its card: "20 stone - 16 in, 4 to go" not Met, "empire has
    /// 15; 5 used this turn by S0's Granary and 10 set aside for S1's Granary, ahead in the building order", Blocked, "It will
    /// wait for 4 stone (empire has 15; 5 used this turn by earlier projects, 10 set aside for earlier projects)." (before the
    /// fix: Met false but Available, no wait line — 15 − 10 covers 4). S1's cancel pending (its set-aside released in the same
    /// step) and the rule off: not HeldForEarlier, Met, Available. Mutants: the earlier heads' draws dropped from Met
    /// (withDraws: false); the draws not passed to the blocker.
    /// </summary>
    [Theory]
    [InlineData("draws and a set-aside", true)]
    [InlineData("holder cancelled", false)]
    [InlineData("batch 3", false)]
    public void AQueuedHeadBehindEarlierDraws_ItsMetAndItsWaitLineCountThem_ExactlyWhenItIsHeldForEarlier(string arm, bool held)
    {
        WorldState w = Solo();
        Head(w, S0, 0, Granary, 750_000);
        AllBuilders(w, S0);
        Head(w, S1, 1);
        NoBuilders(w, S1);
        Head(w, S2, 2, Granary, 791_300);
        AllBuilders(w, S2);
        Set(w, S3, Timber, 500);
        Set(w, S3, Stone, 15);
        SimConfig cfg = arm == "batch 3" ? Pre : Cfg;
        OrderRecord[] pending = arm == "holder cancelled" ? [Cancel(0, Player, S1, 0)] : [];

        HoldsWalkResult walk = SetAsideQuery.Read(w, cfg, pending, Dt).Walk;
        int at = SetAsideQuery.HeadIndexOf(walk, S2);
        Assert.Equal(5L, SetAsideQuery.At(SetAsideQuery.DrawnAhead(walk, at), Stone));                            // PREMISE: S0 draws 5
        Assert.Equal(held ? 10L : 0L, SetAsideQuery.At(SetAsideQuery.Ahead(walk, at, withDraws: false), Stone));  // S1 sets aside 10
        HeadStatusReport s2 = ConstructionQuery.HeadStatus(w, cfg, S2, pending, Dt)!;
        Assert.Equal(held, s2.Cause == MaterialCause.HeldForEarlier);

        InfoCard card = InfoQuery.Card(w, cfg, Player, InfoSubject.OfProject(Granary, S2.Value), Next(pending), SName);
        (string _, bool? met, string? note) = Line(card, "20 stone - 16 in, 4 to go");
        Assert.Equal(!held, met);
        Assert.Equal(held ? InfoStatus.Blocked : InfoStatus.Available, card.Status);
        Assert.Equal(held, card.StatusLine.Contains("It will wait for", StringComparison.Ordinal));
        if (held)
        {
            Assert.Equal("empire has 15; 5 used this turn by S0's Granary and 10 set aside for S1's Granary, ahead in the building order", note);
            Assert.EndsWith(" It will wait for 4 stone (empire has 15; 5 used this turn by earlier projects, 10 set aside for earlier projects).",
                card.StatusLine, StringComparison.Ordinal);
        }
    }

    private static string? RoadBlockerFor(IReadOnlyWorldState w, SimConfig cfg, PolityId polity, IReadOnlyList<OrderRecord>? pending)
    {
        var list = new List<ActionDescriptor>();
        AvailableActionsQuery.Roads(w, cfg, polity, Next(pending), list);
        foreach (ActionDescriptor a in list) if (a.Domain == ActionDomain.Roads) return a.Blocker;
        throw new Xunit.Sdk.XunitException("road development is not listed");
    }

    /// <summary>
    /// FIX ROUND F2 — THE ROAD BLOCKER READS THE COMING STEP'S SET-ASIDES (the walk with the pending orders; the step runs
    /// construction before road development). The rival's waiting granary at S1 sets aside S3's 40 timber, published by a
    /// construction step: the blocker names it; with the rival's cancel of that granary pending (released in the same step)
    /// there is no blocker, though the published table still holds the row. A world with nothing published and nothing queued:
    /// no blocker; with a granary enqueue at S1 pending (no builders: it will set aside the 40) the blocker names it. The
    /// order-less listing (AvailableActionsQuery.For's four-argument contributor) is the walk with no pending order. CONTROL
    /// (the rule off): no blocker in any arm. Mutant: the blocker back on the published table.
    /// </summary>
    [Fact]
    public void TheRoadBlocker_ReadsTheComingStepsSetAsides_APendingCancelReleases_APendingEnqueueAdds()
    {
        const string named = "needs 60 timber for the first route (set aside for building projects: 40 timber)";
        WorldState w = RivalRoadRig();
        Head(w, S1, 0);
        NoBuilders(w, S1);
        Set(w, S3, Timber, 40);
        Set(w, S3, Stone, 20);
        WorldState on = ConstructionOnly(Cfg, null).Step(w);
        Assert.Contains(new ConstructionHoldRow(Reservations.PolityPurse(Rival), Timber, 40), Rows(on.ConstructionHolds));   // PREMISE
        Assert.Equal(named, RoadBlockerFor(on, Cfg, Rival, null));
        Assert.Equal(named, RoadBlocker(on, Cfg, Rival));                                                          // the 4-argument listing
        OrderRecord[] cancel = [Cancel(0, Rival, S1, 0)];
        Assert.Null(RoadBlockerFor(on, Cfg, Rival, cancel));
        Assert.Contains(new ConstructionHoldRow(Reservations.PolityPurse(Rival), Timber, 40), Rows(on.ConstructionHolds));   // still published

        WorldState fresh = RivalRoadRig();
        NoBuilders(fresh, S1);
        Set(fresh, S3, Timber, 40);
        Set(fresh, S3, Stone, 20);
        Assert.Empty(Rows(fresh.ConstructionHolds));                                                               // PREMISE
        Assert.Null(RoadBlockerFor(fresh, Cfg, Rival, null));
        OrderRecord[] enqueue = [Enqueue(0, Rival, S1)];
        Assert.Equal(named, RoadBlockerFor(fresh, Cfg, Rival, enqueue));

        Assert.Null(RoadBlockerFor(on, Pre, Rival, null));                                                         // CONTROL: batch 3
        Assert.Null(RoadBlockerFor(fresh, Pre, Rival, enqueue));
    }
}
