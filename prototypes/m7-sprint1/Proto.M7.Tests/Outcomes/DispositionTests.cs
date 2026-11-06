using Proto.M7.Events;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Proto.M7.WarEcon;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// The post-battle choice for a settlement taken in battle (DIR5 §4.2, PROPOSAL): occupy, sack, raze. Eligibility (only the taker, only at
/// the capture instant, once), the exact Ledger flows of each choice (loot, civilian deaths and flight, burnt buildings), the derived
/// occupation grievance, and the delivery pins at the capture instant and its neighbours.
/// </summary>
public class DispositionTests
{
    private static readonly long T = B(2) + 2 * Day;
    private const long Sack = (long)Disposition.Sack, Raze = (long)Disposition.Raze, Occupy = (long)Disposition.Occupy;

    private static Built Taken(long sackers, long[]? probes = null, Func<OutcomeSettlement[], OutcomeSettlement[]>? settlements = null)
        => Build((s, _) => Capture(s, T, 0, 1, sackers), stops: true, probes: probes, settlements: settlements);

    private static Built Disposed(long disposition, long sackers = 5_000, Func<OutcomeSettlement[], OutcomeSettlement[]>? settlements = null)
    {
        var b = Taken(sackers, settlements: settlements);
        Drive(b, T + Day, at => at == T ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), disposition) } : None);
        Assert.Equal(0, Assert.Single(b.W.OrderResults).Reason);
        return b;
    }

    [Fact]
    public void Sack_MovesExactlyWhatLaneEsDrawsAndHeadLossFlowsGive_AndConserves()
    {
        var plain = Taken(5_000);
        plain.R.RunUntil(T + Day);
        var before = Totals(plain.W.Ledger);
        var b = Disposed(Sack);
        var w = b.W;
        var k = OutcomeCoefficients.Proposal;
        long dt = k.SackDays * Day;
        // intensity q = min(5,000 sackers, 10,000 civilians) / 10,000 = 1/2 scales every rate
        long kill = k.SackKillPermilleYear / 2, flee = k.SackFleePermilleYear / 2;
        var survival = HeadcountLoss.Survival(kill + flee, dt);
        long killed = HeadcountLoss.LossMilli(10_000, kill, kill + flee, survival) / 1000;
        long fled = HeadcountLoss.LossMilli(10_000, flee, kill + flee, survival) / 1000;
        long loot = LogisticsRates.Proposal().ForageForcedPermilleYear / 2;
        long money = StockDraw.Share(1_000_000, loot, dt), grain = StockDraw.Share(5_000_000, loot, dt);
        long burnt = StockDraw.Share(2_000, k.SackBurnPermilleYear / 2, dt);
        var row = w.SettlementOutcomes[^1];
        Assert.Equal(((byte)Disposition.Sack, money, grain, killed, fled, burnt, 0L), (row.Disposition, row.LootMoney, row.LootGrain, row.Killed, row.Fled, row.Burnt, row.GrainBurnt));
        // golden (regression) values of the same quantities, so a change of lane E's integrators shows here too
        Assert.Equal((529L, 1_059L), (killed, fled));   // computed independently (integer re-derivation of the daily survival product)
        Assert.True(money > 300_000 && money < 400_000, $"three days at half the forced rate take about a third: {money}");
        // the flows, account by account
        Assert.Equal(10_000 - killed - fled, w.Ledger.Balance(w.CivilianAccount(0)));
        Assert.Equal(killed, w.Ledger.Balance(w.CiviliansKilledSink));
        Assert.Equal(fled, w.Ledger.Balance(w.RefugeesOf(0)));
        Assert.Equal(3_000_000 + money, w.Ledger.Balance(w.TreasuryOf(1)));
        Assert.Equal(50_000_000 + grain, w.Ledger.Balance(w.GranaryOf(1)));
        Assert.Equal(burnt, w.Ledger.Balance(w.RuinsSink));
        Assert.Equal(before, Totals(w.Ledger));
        // the occupation grievance is derived from the harm: base + 4,000 × (killed + fled) / 10,000 (‰ per sim-year)
        Assert.Equal(1_000 + 4_000 * (killed + fled) / 10_000, row.GrievancePermilleYear);
        Assert.Equal(row.GrievancePermilleYear, w.GrievanceOf(0));
        Assert.Equal(1_000, plain.W.GrievanceOf(0));    // occupied without a sack: the base grievance
        // the sacked city stays the victor's
        Assert.Equal((1, false), (w.ControllerOf(0), w.IsRazed(0)));
        var cas = w.Casualties.Where(c => c.BattleId == row.BattleId && c.Side == 2).ToArray();
        Assert.Equal(new[] { ((byte)CasualtyCause.CiviliansKilled, killed), ((byte)CasualtyCause.CiviliansFled, fled) }, cas.Select(c => (c.Cause, c.Count)).ToArray());
    }

    [Fact]
    public void Raze_TakesTheRest_BurnsTheRest_ExpelsEveryone_AndTheCityIsGone()
    {
        var sack = Disposed(Sack);
        var b = Disposed(Raze);
        var w = b.W;
        var s = sack.W.SettlementOutcomes[^1];
        var row = w.SettlementOutcomes[^1];
        Assert.Equal((byte)Disposition.Raze, row.Disposition);
        Assert.Equal(s.Killed, row.Killed);                                        // the plunder part is the sack's
        Assert.Equal(10_000, row.Killed + row.Fled);                              // nobody stays
        Assert.Equal(1_000_000, row.LootMoney);                                   // all movable wealth is carried off
        Assert.Equal(5_000_000, row.LootGrain + row.GrainBurnt);                  // grain: what the sack took, the rest burnt
        Assert.Equal(s.LootGrain, row.LootGrain);
        Assert.Equal(2_000, row.Burnt);                                           // every building
        Assert.Equal((0L, 0L, 0L, 0L), (w.Ledger.Balance(w.CivilianAccount(0)), w.Ledger.Balance(w.MoneyAccount(0)), w.Ledger.Balance(w.GrainAccount(0)),
                                        w.Ledger.Balance(w.BuildingAccount(0))));
        Assert.Equal(row.GrainBurnt, w.Ledger.Balance(w.GrainBurntSink));
        Assert.True(w.IsRazed(0));
        Assert.Equal(-1, w.ControllerOf(0));
        Assert.Equal(2L, w.CityCount(1));                                         // the victor's count is back to what it was before the capture
        Assert.Equal(0, row.GrievancePermilleYear);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(2_500L)]
    [InlineData(10_000L)]
    [InlineData(40_000L)]
    public void SackIntensity_ScalesWithSackersPerInhabitant_CappedAtOne(long sackers)
    {
        var b = Disposed(Sack, sackers);
        var row = b.W.SettlementOutcomes[^1];
        if (sackers == 0) Assert.Equal((0L, 0L, 0L, 0L), (row.Killed, row.Fled, row.LootMoney, row.Burnt));
        var full = Disposed(Sack, 10_000).W.SettlementOutcomes[^1];
        if (sackers >= 10_000) Assert.Equal(full, row with { SettlementId = full.SettlementId });   // q = 1 for ten thousand or more
        if (sackers == 2_500)
        {
            Assert.True(row.Killed < full.Killed && row.LootMoney < full.LootMoney && row.Burnt <= full.Burnt);
            Assert.True(row.Killed > 0);
        }
    }

    [Fact]
    public void AnEmptyTown_IsLootedAtTheFullRate_WhenSackersArePresent()
    {
        var b = Disposed(Sack, 100, settlements: x => { x[0] = x[0] with { Civilians = 0 }; return x; });
        var row = b.W.SettlementOutcomes[^1];
        long dt = OutcomeCoefficients.Proposal.SackDays * Day;
        Assert.Equal(StockDraw.Share(1_000_000, LogisticsRates.Proposal().ForageForcedPermilleYear, dt), row.LootMoney);
        Assert.Equal((0L, 0L), (row.Killed, row.Fled));
        Assert.Equal(1_000, row.GrievancePermilleYear);
    }

    [Fact]
    public void Occupy_IsTheDefault_AndMovesNothing()
    {
        var b = Taken(5_000);
        var before = b.W.Ledger.Snapshot();
        b.R.RunUntil(T + Day);   // the window at T is passed without a choice
        b.R.RunUntil(T + 2 * Day);
        var row = b.W.SettlementOutcomes[^1];
        Assert.Equal(((byte)Disposition.Occupy, 0L, 0L, 0L), (row.Disposition, row.LootMoney, row.Killed, row.Burnt));
        var after = b.W.Ledger.Snapshot();
        Assert.Equal(before[b.W.CivilianAccount(0)], after[b.W.CivilianAccount(0)]);
        Assert.Equal(before[b.W.MoneyAccount(0)], after[b.W.MoneyAccount(0)]);
    }

    /// <summary>Delivery pins of the post-battle choice: offered only at the capture instant T (not at T − 1, not at T + 1).</summary>
    [Theory]
    [InlineData(-1, OutcomeRefusal.NotEligible)]
    [InlineData(0, 0)]
    [InlineData(1, OutcomeRefusal.WindowClosed)]
    public void Dispose_IsOfferedOnlyAtTheCaptureInstant(long offset, int expected)
    {
        long at = T + offset;
        var b = Taken(5_000, probes: offset == 0 ? null : new[] { at });
        Drive(b, T + Day, x => x == at ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), Sack) } : None);
        var o = Assert.Single(b.W.OrderResults);
        Assert.Equal((at, expected), (o.AtMs, o.Reason));
        Assert.Equal(expected == 0 ? (byte)Disposition.Sack : (byte)Disposition.Occupy, b.W.SettlementOutcomes[^1].Disposition);
    }

    /// <summary>The eligibility matrix: only the taker, once, of a settlement it took at this instant, with a valid choice.</summary>
    [Theory]
    [InlineData(1, 0, Sack, 0)]                                   // the taker, its capture, at the window
    [InlineData(2, 0, Sack, OutcomeRefusal.NotEligible)]           // another victor of the same war
    [InlineData(0, 0, Sack, OutcomeRefusal.NotEligible)]           // the loser
    [InlineData(1, 2, Sack, OutcomeRefusal.NotEligible)]           // a city the issuer holds but did not take
    [InlineData(1, 3, Raze, OutcomeRefusal.NotEligible)]
    [InlineData(1, 0, 3L, OutcomeRefusal.BadValue)]                // not a disposition
    [InlineData(1, 9, Sack, OutcomeRefusal.BadTarget)]             // no such settlement
    public void Dispose_EligibilityMatrix(int issuer, int settlement, long choice, int expected)
    {
        var b = Taken(5_000);
        var before = b.W.Ledger.Snapshot();
        Drive(b, T + Day, at => at == T ? new[] { (OutcomeKinds.OrderDispose, Pack(issuer, settlement), choice) } : None);
        Assert.Equal(expected, Assert.Single(b.W.OrderResults).Reason);
        if (expected != 0) Assert.Equal(before, b.W.Ledger.Snapshot());     // a refusal changes nothing
    }

    [Fact]
    public void Dispose_Once_ASecondChoiceIsRefused()
    {
        var b = Taken(5_000);
        Drive(b, T + Day, at => at == T ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), Occupy), (OutcomeKinds.OrderDispose, Pack(1, 0), Sack) } : None);
        Assert.Equal(new[] { 0, OutcomeRefusal.AlreadyDisposed }, b.W.OrderResults.Select(o => o.Reason).ToArray());
        Assert.Equal((byte)Disposition.Occupy, b.W.SettlementOutcomes[^1].Disposition);
    }

    [Fact]
    public void ARecapture_OpensANewChoice_ButTheSackedStocksAreWhatIsLeft()
    {
        long t2 = B(4);
        var b = Build((s, _) => { Capture(s, T, 0, 1, 10_000); Capture(s, t2, 0, 0, 10_000); Capture(s, t2 + Day, 0, 1, 10_000); }, stops: true);
        Drive(b, t2 + 2 * Day, at => at == T || at == t2 + Day ? new[] { (OutcomeKinds.OrderDispose, Pack(1, 0), Sack) } : None);
        var rows = b.W.SettlementOutcomes;
        Assert.Equal(3, rows.Count);
        Assert.Equal(((byte)Disposition.Sack, (byte)Disposition.Occupy, (byte)Disposition.Sack), (rows[0].Disposition, rows[1].Disposition, rows[2].Disposition));
        long dt = OutcomeCoefficients.Proposal.SackDays * Day, rate = LogisticsRates.Proposal().ForageForcedPermilleYear;
        Assert.Equal(StockDraw.Share(1_000_000 - rows[0].LootMoney, rate, dt), rows[2].LootMoney);   // a second sack draws on what is left
        Assert.True(rows[2].LootMoney < rows[0].LootMoney);
        Assert.Equal(0, rows[1].GrievancePermilleYear);                // liberated by its own polity: not occupied
    }
}
