using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T2.10 acceptance: capture, rollover, selection switch, extinct settlement —
// and the D-028 doctrine (UI-side only: no sim-side state exists for the
// graphs, asserted at grep level against the Sim.Core sources).
public class HistoryBufferTests
{
    private static WorldState World(long turn, params (int Id, long Pop, long Food, double Grievance)[] settlements)
    {
        var world = new WorldState(7)
        {
            Clock = new SimClock(turn, turn * 3600, 3600),
        };
        var ledger = new Ledger(world.LedgerFlows);
        foreach ((int id, long pop, long food, double grievance) in settlements)
        {
            var sid = new SettlementId(id);
            world.Settlements.Add(new SettlementRow(sid, SiteCell: id, FoundedTurn: 0));
            int row = world.Buckets.Add(new BucketRow(
                sid, new CultureId(1), new ReligionId(1), new ClassId(1),
                5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (pop > 0)
                ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                    ReasonIds.InitialEndowment, pop, FlowDirection.Source, OverdrawPolicy.Throw);
            var storeRow = new GoodStockRow(sid, new GoodId(1), Conserved.Zero, 0.0, 0.0, 0);
            int fs = world.GoodStocks.Add(storeRow);
            if (food > 0)
                ledger.Flow(ref world.GoodStocks.Ref(fs).Amount, ConservedQuantityIds.OfGood(new GoodId(1)),
                    ReasonIds.InitialEndowment, food, FlowDirection.Source, OverdrawPolicy.Throw);
            world.Grievances.Add(new GrievanceRow(sid, new ClassId(1), grievance));
        }
        return world;
    }

    [Fact]
    public void Capture_RecordsWorldAndPerSettlement_IdempotentPerTurn()
    {
        var buffer = new HistoryBuffer();
        buffer.Capture(World(0, (0, 100, 500, 0.25), (1, 40, 200, 0.75)));
        buffer.Capture(World(0, (0, 999, 999, 9.9), (1, 999, 999, 9.9))); // same turn: ignored
        buffer.Capture(World(1, (0, 110, 480, 0.30), (1, 38, 210, 0.80)));

        Assert.Equal(2, buffer.SampleCount);
        Assert.Equal([140f, 148f], buffer.World(HistoryBuffer.Metric.Population));
        Assert.Equal([700f, 690f], buffer.World(HistoryBuffer.Metric.Food));
        Assert.Equal([1.0f, 1.1f], buffer.World(HistoryBuffer.Metric.Grievance));
        Assert.Equal([100f, 110f], buffer.Settlement(0, HistoryBuffer.Metric.Population));
        Assert.Equal([0.75f, 0.80f], buffer.Settlement(1, HistoryBuffer.Metric.Grievance));
    }

    [Fact]
    public void Rollover_AtCapacity_ForgetsOldestKeepsNewest()
    {
        var buffer = new HistoryBuffer();
        const int extra = 50;
        for (int t = 0; t < HistoryBuffer.Capacity + extra; t++)
            buffer.Capture(World(t, (0, t, 0, 0.0)));

        float[] series = buffer.Settlement(0, HistoryBuffer.Metric.Population);
        Assert.Equal(HistoryBuffer.Capacity, series.Length);
        Assert.Equal(extra, (int)series[0]);                            // oldest surviving sample
        Assert.Equal(HistoryBuffer.Capacity + extra - 1, (int)series[^1]); // newest
    }

    [Fact]
    public void SelectionSwitch_AnySettlementHasFullHistory_UnknownIdIsEmptyNotCrash()
    {
        // Capture is selection-independent: after watching only "settlement
        // 0" the user switches to 1 — the full series must already be there.
        var buffer = new HistoryBuffer();
        for (int t = 0; t < 10; t++)
            buffer.Capture(World(t, (0, 100 + t, 0, 0.0), (1, 200 + t, 0, 0.0)));
        float[] b = buffer.Settlement(1, HistoryBuffer.Metric.Population);
        Assert.Equal(10, b.Length);
        Assert.Equal(200f, b[0]);
        Assert.Equal(209f, b[^1]);
        Assert.Empty(buffer.Settlement(99, HistoryBuffer.Metric.Population)); // ghost id: no data, no throw
    }

    [Fact]
    public void ExtinctSettlement_PlotsZeros_AllFinite()
    {
        var buffer = new HistoryBuffer();
        buffer.Capture(World(0, (0, 300, 100, 0.5)));
        buffer.Capture(World(1, (0, 0, 0, 0.5)));   // extinct: rows persist, counts zero
        buffer.Capture(World(2, (0, 0, 0, 0.5)));
        float[] pop = buffer.Settlement(0, HistoryBuffer.Metric.Population);
        Assert.Equal([300f, 0f, 0f], pop);
        foreach (HistoryBuffer.Metric metric in Enum.GetValues<HistoryBuffer.Metric>())
        {
            foreach (float v in buffer.Settlement(0, metric)) Assert.True(float.IsFinite(v));
            foreach (float v in buffer.World(metric)) Assert.True(float.IsFinite(v));
        }
    }

    // --- T3.9a item 2: per-(settlement, good) price series ----------------

    private static WorldState PriceWorld(long turn, params (int Settlement, int Good, double Price)[] prices)
    {
        WorldState world = World(turn, (0, 100, 100, 0.0));
        foreach ((int settlement, int good, double price) in prices)
            world.Prices.Add(new PriceRow(new SettlementId(settlement), new GoodId(good), price));
        return world;
    }

    [Fact]
    public void PriceSeries_CapturedPerSettlementGood_ChronologicalAndExact()
    {
        var buffer = new HistoryBuffer();
        buffer.Capture(PriceWorld(1, (0, 4, 1.00), (0, 5, 2.00), (1, 4, 3.00)));
        buffer.Capture(PriceWorld(1, (0, 4, 9.99)));                      // same turn: ignored
        buffer.Capture(PriceWorld(2, (0, 4, 1.10), (0, 5, 1.90), (1, 4, 3.30)));
        buffer.Capture(PriceWorld(3, (0, 4, 1.25), (0, 5, 1.80), (1, 4, 3.60)));

        Assert.Equal([1.00f, 1.10f, 1.25f], buffer.Price(0, 4)); // a scarcity reads as a rising line
        Assert.Equal([2.00f, 1.90f, 1.80f], buffer.Price(0, 5));
        Assert.Equal([3.00f, 3.30f, 3.60f], buffer.Price(1, 4));
    }

    [Fact]
    public void PriceSeries_StartsAtFirstMeasuredTurn_NoFabricatedFoundingSample()
    {
        // Turn 0 has no price rows (PriceSystem writes from the first resolved
        // turn): the price series must be SHORTER than the population series,
        // never padded with an invented turn-0 price.
        var buffer = new HistoryBuffer();
        buffer.Capture(World(0, (0, 100, 100, 0.0)));         // founding: no prices
        buffer.Capture(PriceWorld(1, (0, 4, 1.05)));
        buffer.Capture(PriceWorld(2, (0, 4, 1.15)));
        Assert.Equal(3, buffer.Settlement(0, HistoryBuffer.Metric.Population).Length);
        Assert.Equal([1.05f, 1.15f], buffer.Price(0, 4));
    }

    [Fact]
    public void PriceSeries_UnknownPair_EmptyNotCrash()
    {
        var buffer = new HistoryBuffer();
        buffer.Capture(PriceWorld(1, (0, 4, 1.0)));
        Assert.Empty(buffer.Price(0, 99));
        Assert.Empty(buffer.Price(42, 4));
        Assert.Empty(new HistoryBuffer().Price(0, 4));
    }

    [Fact]
    public void NoSimSideState_GrepLevel_SimCoreNeverReferencesTheBuffer()
    {
        // D-028 teeth: the ring buffer lives in Sim.Ui only. Walk up to the
        // repo root and grep the Sim.Core sources — any reference to the
        // buffer from sim code is a doctrine breach, caught here.
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "Sim.Core")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "could not locate the repo root from the test base directory");
        foreach (string file in Directory.EnumerateFiles(
            Path.Combine(dir!, "Sim.Core"), "*.cs", SearchOption.AllDirectories))
        {
            Assert.False(File.ReadAllText(file).Contains("HistoryBuffer", StringComparison.Ordinal),
                $"{file} references HistoryBuffer — D-028 says history buffers are UI-side only");
        }
    }
}
