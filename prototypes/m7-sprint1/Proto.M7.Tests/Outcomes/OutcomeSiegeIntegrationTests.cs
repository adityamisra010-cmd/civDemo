using Proto.M7.Events;
using Proto.M7.Fixed;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.War.Siege;
using Proto.M7.WarEcon;
using Proto.M7.Tests.Siege;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// Lane S reused, not rebuilt: a siege's capture (lane S's EndSiege, the only path by which control passes) is the capture of the
/// outcome world; the garrison lane S takes prisoner enters custody; a sack moves lane S's civilians and granary; the grievance a sack
/// derives is the input of lane S's occupation unrest.
/// </summary>
public class OutcomeSiegeIntegrationTests
{
    private static readonly long Start = SiegeRig.At(1500, 100);

    private static SiegeOutcome CaptureOutcome(BoundBuilt b) => b.Siege.OutcomeOf(0);

    [Fact]
    public void ASiegeCapitulation_IsTheCapture_AndTheLeaderIsExecutedAtItsInstant()
    {
        var b = BuildBound(Start, foodDays: 5);
        var before = Totals(b.W.Ledger);
        b.R.RunUntil(Start + 200 * Day);
        Assert.Equal(SiegeOutcome.Capitulated, CaptureOutcome(b));
        var c = Assert.Single(b.Siege.Captures);
        var f = Assert.Single(b.W.Fates);
        Assert.Equal((c.AtMs, (byte)LeaderFate.Executed, (byte)FateCause.LastCityLost, 1, 0), (f.AtMs, f.To, f.Cause, f.ByPolity, f.SettlementId));
        Assert.Equal(((c.AtMs - b.WarStart) / (7 * Day), Cal.TurnOf(new SimInstant(c.AtMs))), (f.Pulse, f.Turn));
        // the garrison lane S took prisoner is in this world's custody, exactly that many, out of lane S's per-captor account
        Assert.Equal((c.Prisoners, 0L), (b.W.Ledger.Balance(b.W.CustodyOf(1, 0)), b.W.Ledger.Balance(b.Siege.PrisonersOf(1))));
        Assert.True(c.Prisoners > 0);
        var battle = Assert.Single(b.W.Battles);
        Assert.Equal(((byte)BattleKind.Siege, (byte)BattleMechanism.SiegeHealth, (byte)SiegeOutcome.Capitulated, Start, c.AtMs, 0),
                     (battle.Kind, battle.Mechanism, battle.EndCode, battle.StartMs, battle.EndMs, battle.SettlementId));
        Assert.Contains(b.W.Casualties, x => x.Cause == (byte)CasualtyCause.Captured && x.Count == c.Prisoners);
        Assert.Equal((c.AtMs, (byte)WarOutcome.Conquest, 0), (b.W.WarRows()[0].EndMs, b.W.WarRows()[0].Outcome, b.W.WarRows()[0].WinnerSide));
        Assert.Equal(before, Totals(b.W.Ledger));                                    // one shared ledger, every commodity exact
        // one millisecond earlier the leader still reigns
        var twin = BuildBound(Start, foodDays: 5);
        twin.R.RunUntil(c.AtMs - 1);
        Assert.Equal((LeaderFate.Reigning, 0), (twin.W.FateOf(0), twin.W.ControllerOf(0)));
    }

    private sealed class Storms : IAssaultResolver
    {
        public AssaultResult Resolve(in AssaultInputs input, SiegeCoefficients k) => new(true, Q32.One, Q32.One, Q32.Zero, Q32.Zero);
    }

    [Fact]
    public void AnAssaultOrderThatStorms_IsSettledWhenTheOrderCompletes_AtThatInstant()
    {
        // the siege's first pulse starts at Start; an assault issued at a window at that instant acts at once (lane S, WA1)
        var b = BuildBound(Start, foodDays: 120, stops: true, probes: new[] { Start }, resolver: new Storms());
        Drive(b, Start + Day, at => at == Start ? new[] { (SiegeKinds.OrderAssault, Pack(1, 0), 1_000L) } : None);
        var c = Assert.Single(b.Siege.Captures);
        Assert.Equal((Start, SiegeOutcome.Stormed), (c.AtMs, c.Cause));
        var f = Assert.Single(b.W.Fates);
        Assert.Equal((Start, (byte)LeaderFate.Executed), (f.AtMs, f.To));
        Assert.Equal(3_000L, b.W.Ledger.Balance(b.W.CustodyOf(1, 0)));
        // the window at Start had closed before the order: no fate-changed hit could be raised there, but the record is complete
        Assert.DoesNotContain(b.R.Windows, w => w.InstantMs == Start && w.Hits.Any(h => h.Kind == OutcomeKinds.LeaderFateChanged));
    }

    private static void Drive(BoundBuilt b2, long limit, Func<long, (int Kind, long A, long B)[]> ordersAt)
    {
        while (true)
        {
            var r = b2.R.RunUntil(limit);
            if (r.Status == RunStatus.ReachedLimit) return;
            foreach (var o in ordersAt(r.AtMs)) b2.R.IssueOrder(o.Kind, o.A, o.B);
        }
    }

    private static BoundBuilt Sacked(SubTurnOrderLog? replay = null)
    {
        var b = BuildBound(Start, foodDays: 5, stops: replay is null, replay: replay);
        if (replay is not null) { b.R.RunUntil(Start + 200 * Day); return b; }
        Drive(b, Start + 200 * Day, at => b.Siege.Captures.Count > 0 && b.Siege.Captures[0].AtMs == at
            ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), (long)Disposition.Sack) } : None);
        return b;
    }

    [Fact]
    public void ASackOfABoundCity_MovesLaneSsCiviliansAndGranary_ThroughTheSharedLedger()
    {
        var plain = BuildBound(Start, foodDays: 5);
        plain.R.RunUntil(Start + 200 * Day);
        var b = Sacked();
        var row = b.W.SettlementOutcomes[^1];
        Assert.Equal((byte)Disposition.Sack, row.Disposition);
        Assert.True(row.Killed > 0 && row.Fled > 0 && row.LootMoney > 0 && row.Burnt > 0);
        Assert.Equal(plain.Siege.Civilians(0) - row.Killed - row.Fled, b.Siege.Civilians(0));
        Assert.Equal(plain.W.Ledger.Balance(plain.Siege.ElsewhereOf(0)) + row.Fled, b.W.Ledger.Balance(b.Siege.ElsewhereOf(0)));
        Assert.Equal(row.Killed, b.W.Ledger.Balance(b.W.CiviliansKilledSink));
        Assert.Equal(plain.Siege.FoodOf(0) - row.LootGrain, b.Siege.FoodOf(0));
        Assert.Equal(plain.W.Ledger.Balance(plain.Siege.HomeFoodOf(1)) + row.LootGrain, b.W.Ledger.Balance(b.Siege.HomeFoodOf(1)));
        Assert.Equal(plain.W.Ledger.Balance(plain.Siege.TreasuryOf(1)) + row.LootMoney, b.W.Ledger.Balance(b.Siege.TreasuryOf(1)));
        Assert.Equal(Totals(plain.W.Ledger), Totals(b.W.Ledger));
        Assert.True(row.GrievancePermilleYear > 1_000);
        var replay = Sacked(b.R.Orders);
        Assert.Equal(b.W.StateHashHex(), replay.W.StateHashHex());
    }

    [Fact]
    public void TheGrievanceASackDerives_DrivesLaneSsOccupationUnrestToRevoltSooner()
    {
        var b = Sacked();
        long derived = b.W.SettlementOutcomes[^1].GrievancePermilleYear;
        long Revolt(long grievance)
        {
            // lane S's own occupation mechanism, unchanged: polity 1 holds polity 0's city with no garrison to police it
            var forms = new[] { new FormationSpec(0, 0, FormationFamily.Infantry, 100, 50, 50) };
            var x = SiegeRig.Build(Start, noSiege: true, formations: forms,
                inst: i => i with { Controller = 1, PopulationPolity = 0, GrievancePermillePerYear = grievance });
            x.R.RunUntil(Start + 6 * SimInstant.MsPerYear);
            return Assert.Single(x.W.Revolts).AtMs;
        }
        Assert.True(derived > 1_000);
        Assert.True(Revolt(derived) < Revolt(1_000), "a sacked population revolts sooner than one merely occupied");
    }
}
