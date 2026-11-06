using Proto.M7.Events;
using Proto.M7.Spatial;
using Proto.M7.WarEcon;
using Proto.M7.Time;
using Proto.M7.War.Siege;
using Xunit.Abstractions;
using static Proto.M7.Tests.Siege.SiegeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Siege;

/// <summary>POPULATED-table round trips for the lane S row types (CLAUDE.md), and the pinned calibration record.</summary>
public class SiegeCodecAndCalibrationTests
{
    private readonly ITestOutputHelper _out;
    public SiegeCodecAndCalibrationTests(ITestOutputHelper output) => _out = output;

    /// <summary>A world mid-siege with every kind of state populated: a relief battle, a pending ration order, carries, disease, a
    /// lane E supply line with shipments, pay arrears with their clock (the treasury runs dry), missiles and munitions drawn.</summary>
    private static SiegeWorld Populated()
    {
        // The campaign-turn start at 1502/0 falls inside the siege pulse [start + 19 d, start + 26 d): the world stops one day after it,
        // so the per-pulse accumulators hold the flows settled up to the turn start (a pulse start would show them reset).
        long turn = At(1502, 0), start = turn - 23 * Day, b1 = start + 5 * Day;
        var road = new Route(new[] { new RouteSegment(60_000, TerrainClass.Open, RoadClass.Built, false, false, -1) });
        var b = Build(start, stops: true, probes: new[] { b1 + 1 }, homeTroops: 50_000, money: 500,
            inst: i => i with { Guns = 4, Munitions = 50_000 },
            siege: s => s with
            {
                Relief = new ReliefSpec(start + 10 * Day, new long[] { 500, 200, 300, 0 }, RangedKind.Bow), CorridorFoodPerYear = 1_000_000,
                ReinforcementPerYear = 1_000, AttackerReinforcementPerYear = 2_000, EvacuationPerYear = 500,
                SupplyLine = new SiegeSupplyLine(road, 300, 600, 0),
            });
        while (true)
        {
            var r = b.R.RunUntil(turn + Day);
            if (r.Status == RunStatus.ReachedLimit) break;
            if (r.AtMs == b1 + 1) b.R.IssueOrder(SiegeKinds.OrderRation, Pack(0, 0), 600);
        }
        // leave a pending (not yet latched) order in the row: issue at a window just after a pulse start
        return b.W;
    }

    private static void RoundTrip<T>(IReadOnlyList<T> rows, int rowBytes, SiegeCodec.SpanEncoder<T> enc, SiegeCodec.SpanDecoder<T> dec)
    {
        Assert.NotEmpty(rows);
        byte[] a = SiegeCodec.EncodeTable(rows, rowBytes, enc);
        Assert.Equal(SiegeCodec.HeaderBytes + rows.Count * rowBytes, a.Length);                 // exact expected length
        var back = SiegeCodec.DecodeTable(a, rowBytes, dec);
        byte[] b = SiegeCodec.EncodeTable(back, rowBytes, enc);
        Assert.Equal(a, b);                                                                    // bit-exact
        Assert.Equal(SiegeCodec.HashHex(a), SiegeCodec.HashHex(b));                            // hash equality
        Assert.Throws<FormatException>(() => SiegeCodec.DecodeTable(a.AsSpan(0, a.Length - 1), rowBytes, dec));
        var bad = (byte[])a.Clone(); bad[0] = 99;
        Assert.Throws<FormatException>(() => SiegeCodec.DecodeTable(bad, rowBytes, dec));
    }

    [Fact]
    public void PopulatedTables_RoundTripBitExact_WithExactLengths()
    {
        var w = Populated();
        var inst = SiegeCodec.Installations(w);
        var forms = SiegeCodec.Formations(w);
        var sieges = SiegeCodec.Sieges(w);
        // populated, not empty or default: non-zero carries, works, disease, accumulators and a relief state
        Assert.Contains(forms, f => f.Carry.Any(c => c != 0));
        Assert.True(inst[0].WorksRaw > 0 && inst[0].UpkeepCarryDen > 1);
        Assert.True(inst[0].UpkeepArrears > 0 && inst[0].ArrearsSinceMs > 0 && inst[0].MissileStore > 0);
        var s = sieges[0];
        Assert.True(s.Active);
        Assert.True(s.ReliefResolved && s.ReliefFailed);
        Assert.True(s.QRaw.Count(q => q != 0) >= 13, string.Join(",", s.QRaw));
        Assert.True(s.Refs.All(v => v > 0));
        Assert.Contains(s.FractionCarries, v => v > 1);
        Assert.True(s.HasLine && s.LineLossDen > 1 && s.LineLossNum > 0 && s.LineCapacityPerYear > 0 && s.Shipped > s.Delivered && s.Delivered > 0);
        Assert.True(s.Accumulators.Count(v => v != 0) >= 10, string.Join(",", s.Accumulators));
        Assert.True(s.RationHeld, "the human ration order stands (fix r1, S-V6)");
        Assert.Contains(s.ReinfArmCarry, v => v != 0);
        Assert.Contains(s.AttCarry, v => v > 1_000_000_000_000L);                                 // head-loss carries in 10⁻¹⁸ head (S-V1)
        Assert.Equal(6, forms.Count);
        Assert.True(inst[0].TheatreLossSettledMs > 0 && inst[0].TheatreLossSettledMs == w.TheatrePulseStart);   // version 4 (fix r2, S2-V2)
        RoundTrip(inst, SiegeCodec.InstallationRowBytes, SiegeCodec.Encode, SiegeCodec.DecodeInstallation);
        RoundTrip(forms, SiegeCodec.FormationRowBytes, SiegeCodec.Encode, SiegeCodec.DecodeFormation);
        RoundTrip(sieges, SiegeCodec.SiegeRowBytes, SiegeCodec.Encode, SiegeCodec.DecodeSiege);
        Assert.Equal(223, SiegeCodec.InstallationRowBytes);                                      // version 4 (fix r2): + theatre settle instant
        Assert.Equal(99, SiegeCodec.FormationRowBytes);
        Assert.Equal(1_292, SiegeCodec.SiegeRowBytes);                                            // version 3 (fix r1): + ration flag + 4 arm carries
    }

    [Fact]
    public void PendingOrders_AreInTheSiegeRow()
    {
        long start = At(1500, 100), b1 = start + 5 * Day;
        var b = Build(start, stops: true, probes: new[] { b1 + 1 });
        while (true)
        {
            var r = b.R.RunUntil(b1 + 2);
            if (r.Status == RunStatus.ReachedLimit) break;
            b.R.IssueOrder(SiegeKinds.OrderRation, Pack(0, 0), 400);
            b.R.IssueOrder(SiegeKinds.OrderAssault, Pack(1, 0), 700);
        }
        var row = SiegeCodec.Sieges(b.W)[0];
        Assert.Equal(new[] { 400L, 700L, -1L }, row.Pending);
        Assert.Equal(new[] { 0, 1, -1, -1 }, row.PendingLogs);
        Assert.Equal(b1 + 1, row.PendingIssued[0]);
        var back = SiegeCodec.DecodeSiege(Encode(row));
        Assert.Equal(row.Pending, back.Pending);
        Assert.Equal(row.PendingLogs, back.PendingLogs);
    }

    private static byte[] Encode(SiegeRow r) { var buf = new byte[SiegeCodec.SiegeRowBytes]; SiegeCodec.Encode(r, buf); return buf; }

    // ------------------------------------------------------------------ the calibration record (pinned; reports/H-S.md §6)

    /// <summary>War Pulse clock (T2's PROPOSED lengths), ProRata, harness seed. A change here is a moved golden: attribute it.
    /// Moved at the lane S resume (2026-11-06; lane E reuse: food in gm at lane E's rations, lane E's desertion and disease rates,
    /// HeadcountLoss, munitions, the pay table, and the lines' containment of an invested garrison's deserters): Vicksburg
    /// (Capitulated, 90 → 87), Sevastopol ((Lifted, 384) → (CapLifted, 1080): the besiegers' health settles at 0.26-0.29, above the
    /// doctrine's lift threshold, and the cap adjudicates), Leningrad (Capitulated, 133 → 109). The other six are unchanged
    /// (measured).</summary>
    [Theory]
    [InlineData("alesia", SiegeOutcome.Relieved, 39)]
    [InlineData("constantinople", SiegeOutcome.Stormed, 46)]
    [InlineData("vienna", SiegeOutcome.Relieved, 60)]
    [InlineData("vicksburg", SiegeOutcome.Capitulated, 87)]
    [InlineData("sevastopol", SiegeOutcome.CapLifted, 1080)]
    [InlineData("masada", SiegeOutcome.Stormed, 95)]
    [InlineData("malta", SiegeOutcome.Capitulated, 123)]
    [InlineData("rhodes", SiegeOutcome.Stormed, 25)]
    [InlineData("leningrad", SiegeOutcome.Capitulated, 109)]
    public void CalibrationRecord_IsPinned(string key, SiegeOutcome outcome, long days)
    {
        var h = SiegeScenarios.Get(key);
        var r = SiegeHarness.Run(h, Cal, ProposedSchedule(), SiegeClock.WarPulse, ShortfallPolicy.ProRata);
        _out.WriteLine($"{key}: {r.Outcome} {r.DurationMs / (double)Day:F2} d (history {h.HistoryDaysMin}-{h.HistoryDaysMax}, {string.Join("/", h.HistoryOutcomes)})");
        Assert.Equal((outcome, days), (r.Outcome, r.DurationMs / Day));
        Assert.True(r.DurationMs <= r.CapMs - InstantOf(h));
    }

    private static long InstantOf(HistoricalSiege h) => SiegeHarness.InstantOf(Cal, h.Year, h.DayOfYear);

    /// <summary>The historical maximum duration of every calibration siege fits under the model's maximum duration (three
    /// campaign-turn equivalents from its start), except Leningrad's (872 days against a 540-day cap). It compares history with the
    /// cap, not the model with history (that is <see cref="CalibrationRecord_IsPinned"/> and reports/H-S.md §6). Renamed in fix r1
    /// (S-V10): it was called "...AreAllInsideTheirBands...", which it never checked.</summary>
    [Fact]
    public void HistoricalDurations_FitUnderTheMaximumDuration_ExceptLeningrads()
    {
        foreach (var h in SiegeScenarios.All)
        {
            long start = InstantOf(h);
            long cap = 3 * Cal.TurnLengthMs(Cal.TurnOf(new SimInstant(start)));
            bool binds = h.HistoryDaysMax * Day > cap;
            Assert.Equal(h.Key == "leningrad", binds);
        }
    }
}
