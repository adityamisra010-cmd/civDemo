using Sim.Core.Observability;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;


/// <summary>
/// T4.20 — THE FOOD FLOW BLOCK as rendered. It is ADDITIVE: the existing
/// <see cref="SettlementInspectorModel.FoodLines"/> store account and its pins
/// are untouched, and these tests assert that too. Everything here is exact
/// text, InvariantCulture, including the three quantities that must render as
/// "not recorded" rather than as a number and the one-turn lag label on the
/// published surplus ratio.
/// </summary>
public class FoodFlowUiTests
{
    private static Sim.Ui.UiSession Played(int turns)
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < turns; t++) session.EndTurn();
        return session;
    }

    [Fact]
    public void FoodFlow_OnThePlayedSession_RendersTheRecordExactly()
    {
        Sim.Ui.UiSession session = Played(3);
        int first = session.World.Settlements[0].Id.Value;
        SettlementView view = ScreenModels.Settlement(session, first)!;
        SettlementRecord r = session.Observations.Settlement(3, first)!;
        FoodSection f = r.Food;
        IReadOnlyList<string> lines = view.FoodFlow;

        // Header says WHOLE-TURN, and the turn length is READ from the turn
        // record's DtYears - never a hardcoded number of years.
        double dt = session.Observations.Observations[^1].Turn.DtYears;
        Assert.Equal(
            string.Create(C, $"FOOD THIS TURN ({dt:F1} sim-years) - whole-turn totals, not per-year"),
            lines[0]);
        Assert.Equal(string.Create(C, $"  produced   {f.FoodProduced:+#,0;-#,0;0}"), lines[1]);
        Assert.Equal(string.Create(C, $"  required   {-f.DemandUnits:+#,0;-#,0;0}"), lines[2]);

        // The verdict word is the SIGN of the balance and nothing else.
        string verdict = f.FoodBalance > 0 ? "surplus" : f.FoodBalance < 0 ? "deficit" : "balanced";
        Assert.Equal(string.Create(C, $"  balance    {f.FoodBalance:+#,0;-#,0;0}        {verdict}"), lines[3]);
        Assert.Equal(f.FoodProduced - f.DemandUnits, f.FoodBalance);

        // One produced/eaten line and one reserve line per food good, in
        // registry order, with the reserve READ from the economy section.
        for (int g = 0; g < f.FoodGoods.Length; g++)
        {
            FoodGood good = f.FoodGoods[g];
            Assert.Equal(
                string.Create(C, $"  {good.Name,-10} produced {good.Produced:#,0}  eaten {good.Eaten:#,0}"),
                lines[4 + g]);
            long stock = 0;
            for (int i = 0; i < r.Economy.Goods.Length; i++)
                if (r.Economy.Goods[i].Good == good.Good) stock = r.Economy.Goods[i].Stock;
            Assert.Equal(
                string.Create(C, $"  reserve {good.Name,-10} {stock:#,0}"),
                lines[4 + f.FoodGoods.Length + g]);
        }
        Assert.Equal(3, f.FoodGoods.Length);   // grain, livestock, fish

        // The store-losses RESIDUAL points at the EXISTING identity, verbatim,
        // and is not renamed "spoilage".
        Assert.Contains(lines, l => l == "  " + f.StoreLossesIdentity);
        Assert.Contains(lines, l => l.StartsWith("  store losses ", StringComparison.Ordinal)
            && l.EndsWith("  (residual, grain only)", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("spoilage", StringComparison.Ordinal)
            && l.StartsWith("  store losses ", StringComparison.Ordinal));

        // The published ratio, marked as LAST TURN'S on its own line.
        Assert.Contains(lines, l => l == string.Create(C,
            $"  food surplus ratio: {(double.IsNaN(r.Economy.FoodSurplusRatio) ? "not recorded" : r.Economy.FoodSurplusRatio.ToString("F3", C))}"));
        Assert.Contains(lines, l => l == SettlementInspectorModel.SurplusRatioLagNote);
        Assert.Contains("LAST TURN'S", SettlementInspectorModel.SurplusRatioLagNote, StringComparison.Ordinal);

        // The three not-derivable quantities: worded, never numbered.
        Assert.Equal(
            "  granary capacity / spoilage / overflow, per settlement: " + SettlementInspectorModel.StoreGapReason,
            lines[^1]);
        Assert.StartsWith("not recorded:", SettlementInspectorModel.StoreGapReason, StringComparison.Ordinal);

        // ADDITIVE: the existing store tab is byte-identical to the model call.
        Assert.Equal(SettlementInspectorModel.FoodLines(r), view.Food);
        Assert.DoesNotContain(view.Food, l => l.StartsWith("FOOD THIS TURN", StringComparison.Ordinal));
    }

    [Fact]
    public void FoodFlow_NoRecordForTheSettlement_YieldsNoViewAtAll()
    {
        Sim.Ui.UiSession session = Played(2);
        Assert.Null(ScreenModels.Settlement(session, 999));
        Assert.Equal(
            "no turn observed yet for this settlement - end a turn to see its record",
            SettlementInspectorModel.NoRecord);
    }

    /// <summary>Zero population, zero production, zero requirement, and a single
    /// food good: the balance is 0 and the word is "balanced" BY DEFINITION, the
    /// three not-recorded lines are still present, and nothing renders NaN.</summary>
    [Fact]
    public void FoodFlow_ZeroPopulationSingleGood_RendersBalancedAndNoNaN()
    {
        var food = new FoodSection(0, 0, 0, 0, 0, "identity", 0, 0.0,
            [new FoodGood(1, "grain", 0, 0, 0)], 0, 0, 0);
        var economy = new EconomySection(
            [new GoodReading(1, "grain", 0, 0, 0, 0, 0, double.NaN)], [], [],
            [0.2, 0.2, 0.2, 0.2, 0.2], false, double.NaN, double.NaN, double.NaN, []);
        SettlementRecord r = Empty(food, economy);

        IReadOnlyList<string> lines = SettlementInspectorModel.FoodFlowLines(r, 10.0);
        Assert.Equal("FOOD THIS TURN (10.0 sim-years) - whole-turn totals, not per-year", lines[0]);
        Assert.Equal("  produced   0", lines[1]);
        Assert.Equal("  required   0", lines[2]);
        Assert.Equal("  balance    0        balanced", lines[3]);
        Assert.Equal("  grain      produced 0  eaten 0", lines[4]);
        Assert.Equal("  reserve grain      0", lines[5]);
        Assert.Contains(lines, l => l == "  food surplus ratio: not recorded");
        Assert.Contains(lines, l => l == SettlementInspectorModel.SurplusRatioLagNote);
        Assert.Equal(
            "  granary capacity / spoilage / overflow, per settlement: " + SettlementInspectorModel.StoreGapReason,
            lines[^1]);
        Assert.DoesNotContain(lines, l => l.Contains("NaN", StringComparison.Ordinal));
    }

    /// <summary>No food good has a stock row at all: one worded line, not an
    /// empty block and not a zero pretending to be a measurement.</summary>
    [Fact]
    public void FoodFlow_NoFoodGoodsAtAll_SaysSo()
    {
        var food = new FoodSection(0, 0, 0, 0, 0, "identity", 0, 0.0, [], 0, 0, 0);
        var economy = new EconomySection([], [], [], [0.2, 0.2, 0.2, 0.2, 0.2], false,
            double.NaN, double.NaN, double.NaN, []);
        IReadOnlyList<string> lines = SettlementInspectorModel.FoodFlowLines(Empty(food, economy), 10.0);
        Assert.Equal("  no food good has a stock row for this settlement yet", lines[4]);
    }

    /// <summary>Number formatting is InvariantCulture at every call site: comma
    /// group separators, a dot decimal point, and the signed format on the three
    /// flow lines. The test suite runs in globalization-invariant mode, so the
    /// ambient culture cannot be switched to prove it by contrast; what is pinned
    /// instead is the exact invariant text, which is what every
    /// string.Create(CultureInfo.InvariantCulture, ...) in the builder produces
    /// regardless of ambient culture.</summary>
    [Fact]
    public void FoodFlow_IsInvariantCulture()
    {
        var food = new FoodSection(0, 0, 0, 0, 0, "identity", 1000, 0.0,
            [new FoodGood(1, "grain", 1234567, 0, 9)], 0, 1234567, 1233567);
        var economy = new EconomySection(
            [new GoodReading(1, "grain", 7654321, 0, 0, 0, 0, double.NaN)], [], [],
            [0.2, 0.2, 0.2, 0.2, 0.2], false, 1.5, double.NaN, double.NaN, []);
        IReadOnlyList<string> lines = SettlementInspectorModel.FoodFlowLines(Empty(food, economy), 10.0);
        Assert.Equal("  produced   +1,234,567", lines[1]);
        Assert.Equal("  required   -1,000", lines[2]);
        Assert.Equal("  balance    +1,233,567        surplus", lines[3]);
        Assert.Equal("  grain      produced 1,234,567  eaten 9", lines[4]);
        Assert.Equal("  reserve grain      7,654,321", lines[5]);
        Assert.Contains(lines, l => l == "  food surplus ratio: 1.500");
    }

    private static readonly System.Globalization.CultureInfo C =
        System.Globalization.CultureInfo.InvariantCulture;

    private static SettlementRecord Empty(FoodSection food, EconomySection economy) =>
        new(0, 0, -1, false,
            new PopulationSection(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "identity", []),
            food,
            new HousingSection(false, 0, 0, 0, 0, 0, 0, 0, "GAP: none"),
            economy,
            new SocialSection(0, [0.0, 0.0], [], []),
            new MigrationSection(0.0, double.NaN, [], 0, 0, 0.0, 0.0, "GAP: none"),
            new PolicySection([0.2, 0.2, 0.2, 0.2, 0.2], false, [0.2, 0.2, 0.2, 0.2, 0.2]),
            []);
}
