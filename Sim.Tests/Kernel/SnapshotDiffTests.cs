using Sim.Cli;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// T4.19 lane E — `sim diff` (SnapshotDiff), the instrument CR-013 §6 asked
/// for before any ruling: name the first divergent table/row/field between
/// two saves, with doubles bit-exact.
///
/// The diff walks the canonical byte stream with its OWN copy of the schema
/// layout (Sim.Cli may not name every table — see SnapshotDiff's header), so
/// the copy is the thing that can rot. Every test here therefore runs on a
/// POPULATED founded world at turn 2 (the first turn CatchmentSummaries is
/// consumed, per CR-013 §2) — 928 catchment nodes, 576 buckets, 168 price
/// terms — and the alignment test pins the walk to CanonicalSchema.ExpectedLength
/// exactly. Empty-table coverage would prove nothing (T1.1/T1.3 precedent).
/// </summary>
public class SnapshotDiffTests
{
    private const ulong Seed = 42;

    private static readonly Lazy<WorldState> Turn2 = new(() =>
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(TestConfigs.Sim())),
            orders: null);
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), Seed);
        return executor.Run(world, 2);
    });

    private static SnapshotDiff.TableSummary Summary(SnapshotDiff.Result r, string table)
    {
        for (int i = 0; i < r.Tables.Count; i++)
            if (r.Tables[i].Table == table) return r.Tables[i];
        throw new Xunit.Sdk.XunitException($"no summary row for table {table}");
    }

    private static int TotalDifferingRows(SnapshotDiff.Result r)
    {
        int n = 0;
        for (int i = 0; i < r.Tables.Count; i++) n += r.Tables[i].RowsDiffering;
        return n;
    }

    [Fact]
    public void IdenticalWorlds_DiffClean_AndTheWalkConsumesExactlyTheSchemaLength()
    {
        WorldState a = Turn2.Value;
        WorldState b = a.Clone();

        SnapshotDiff.Result r = SnapshotDiff.Compare(a, b);

        Assert.True(r.Identical);
        Assert.Null(r.First);
        Assert.Equal(0, TotalDifferingRows(r));

        // The layout-staleness guard: the walk must land on the schema's own
        // anti-padding length, on BOTH streams, with no bytes left over. A
        // field of the wrong width anywhere in SnapshotDiff.Layout shifts every
        // later count prefix and this equality cannot hold on a populated world.
        long expected = CanonicalSchema.ExpectedLength(a);
        Assert.Equal(expected, r.BytesA);
        Assert.Equal(expected, r.BytesWalkedA);
        Assert.Equal(expected, r.BytesWalkedB);

        // Row counts were read from the RIGHT prefixes (populated tables only —
        // an empty table's zero says nothing about alignment).
        Assert.Equal(a.CatchmentNodes.Count, Summary(r, "CatchmentNodes").RowsA);
        Assert.Equal(a.Buckets.Count, Summary(r, "Buckets").RowsA);
        Assert.Equal(a.PriceTerms.Count, Summary(r, "PriceTerms").RowsA);
        Assert.Equal(a.Housing.Count, Summary(r, "Housing").RowsA);
        Assert.True(a.CatchmentNodes.Count > 0 && a.Buckets.Count > 0 && a.PriceTerms.Count > 0 && a.Housing.Count > 0,
            "fixture must be populated for the alignment proof to mean anything");
        Assert.Equal(SnapshotDiff.Layout.Length + 1, r.Tables.Count); // header + every block
    }

    [Fact]
    public void OneUlpFlip_IsReportedAtTheExactTableRowAndField_WithBothBitPatterns()
    {
        WorldState a = Turn2.Value;
        WorldState b = a.Clone();
        const int row = 7;
        ref CatchmentSummaryRow target = ref b.CatchmentSummaries.Ref(row);
        long bitsBefore = BitConverter.DoubleToInt64Bits(target.EffectiveArableKm2);
        long bitsAfter = bitsBefore + 1;
        target.EffectiveArableKm2 = BitConverter.Int64BitsToDouble(bitsAfter);

        // The flip must be invisible to a short print and visible to the hash —
        // that is the CR-013 signature the tool exists for.
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));

        SnapshotDiff.Result r = SnapshotDiff.Compare(a, b);

        Assert.False(r.Identical);
        SnapshotDiff.FieldDifference d = Assert.IsType<SnapshotDiff.FieldDifference>(r.First);
        Assert.Equal("CatchmentSummaries", d.Table);
        Assert.Equal(row, d.Row);
        Assert.Equal("EffectiveArableKm2", d.Field);
        Assert.Equal(SnapshotDiff.Kind.Double, d.Kind);
        Assert.Equal(SnapshotDiff.DoubleText(bitsBefore), d.ValueA);
        Assert.Equal(SnapshotDiff.DoubleText(bitsAfter), d.ValueB);
        Assert.Contains("0x" + bitsBefore.ToString("X16", System.Globalization.CultureInfo.InvariantCulture), d.ValueA);
        Assert.Contains("0x" + bitsAfter.ToString("X16", System.Globalization.CultureInfo.InvariantCulture), d.ValueB);
        Assert.Equal("ulp distance B-A = 1", d.Detail);

        // Extent: exactly one row in exactly one table; the walk stays aligned
        // through every later block (a misalignment would show as differences).
        SnapshotDiff.TableSummary s = Summary(r, "CatchmentSummaries");
        Assert.Equal(1, s.RowsDiffering);
        Assert.Equal(row, s.FirstDifferingRow);
        Assert.Equal(1, TotalDifferingRows(r));
        Assert.Equal(r.BytesA, r.BytesWalkedA);
        Assert.Equal(r.BytesB, r.BytesWalkedB);
    }

    [Fact]
    public void LongAndDeepStreamFlips_PrintPlainly_AndFirstHitIsStreamOrderNotSeverity()
    {
        WorldState a = Turn2.Value;
        WorldState b = a.Clone();
        // Two edits: a long in GoodStocks (block 16) and a double in
        // HarvestWeather (block 29). The FIRST reported must be the earlier
        // block in stream order, and both must appear in the summary.
        const int stockRow = 5, weatherRow = 11;
        long produced = b.GoodStocks[stockRow].LastProducedUnits;
        b.GoodStocks.Ref(stockRow).LastProducedUnits = produced + 3;
        ref HarvestWeatherRow w = ref b.HarvestWeather.Ref(weatherRow);
        long wBits = BitConverter.DoubleToInt64Bits(w.Multiplier);
        w.Multiplier = BitConverter.Int64BitsToDouble(wBits - 2);

        SnapshotDiff.Result r = SnapshotDiff.Compare(a, b);

        SnapshotDiff.FieldDifference d = Assert.IsType<SnapshotDiff.FieldDifference>(r.First);
        Assert.Equal("GoodStocks", d.Table);
        Assert.Equal(stockRow, d.Row);
        Assert.Equal("LastProducedUnits", d.Field);
        Assert.Equal(SnapshotDiff.Kind.Int64, d.Kind);
        Assert.Equal(produced.ToString(System.Globalization.CultureInfo.InvariantCulture), d.ValueA);
        Assert.Equal((produced + 3).ToString(System.Globalization.CultureInfo.InvariantCulture), d.ValueB);

        Assert.Equal(1, Summary(r, "GoodStocks").RowsDiffering);
        SnapshotDiff.TableSummary hw = Summary(r, "HarvestWeather");
        Assert.Equal(1, hw.RowsDiffering);
        Assert.Equal(weatherRow, hw.FirstDifferingRow);
        Assert.Equal(2, TotalDifferingRows(r));
        Assert.Equal("ulp distance B-A = -2", SnapshotDiff.UlpNote(wBits, wBits - 2));
    }

    [Fact]
    public void RowCountMismatch_IsReportedBlockLevel_AndLaterBlocksStayAligned()
    {
        WorldState a = Turn2.Value;
        WorldState b = a.Clone();
        Assert.Equal(0, a.TradeFlows.Count); // nothing traded by turn 2 on this seed — measured
        b.TradeFlows.Add(new TradeFlowRow(new SettlementId(0), new SettlementId(1), new GoodId(0), 5));

        SnapshotDiff.Result r = SnapshotDiff.Compare(a, b);

        SnapshotDiff.FieldDifference d = Assert.IsType<SnapshotDiff.FieldDifference>(r.First);
        Assert.Equal("TradeFlows", d.Table);
        Assert.Equal(-1, d.Row);
        Assert.Equal("RowCount", d.Field);
        Assert.Equal("0", d.ValueA);
        Assert.Equal("1", d.ValueB);

        // The extra row is skipped on B so Housing (the next block) is still
        // read from its own count prefix on both sides.
        SnapshotDiff.TableSummary housing = Summary(r, "Housing");
        Assert.Equal(a.Housing.Count, housing.RowsA);
        Assert.Equal(a.Housing.Count, housing.RowsB);
        Assert.Equal(0, housing.RowsDiffering);
        Assert.Equal(0, TotalDifferingRows(r));
        Assert.Equal(r.BytesA, r.BytesWalkedA);
        Assert.Equal(r.BytesB, r.BytesWalkedB);
        Assert.Equal(CanonicalSchema.ExpectedLength(b), r.BytesB);
    }
}
