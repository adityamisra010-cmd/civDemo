using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T4.18 workstream B: the labour split is a FIXED-SUM allocation, and these
/// pin the three things that makes true — the invariant holds after any move,
/// redistribution is deterministic, and the snap from the world's own shares
/// cannot show the director a total he never typed.
/// </summary>
public class SectorAllocationModelTests
{
    private static int[] W(params int[] weights) => weights;
    private static int Sum(ReadOnlySpan<int> w)
    {
        int total = 0;
        for (int i = 0; i < w.Length; i++) total += w[i];
        return total;
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 100)] [InlineData(0, 37)]
    [InlineData(2, 0)] [InlineData(2, 55)] [InlineData(2, 100)]
    [InlineData(4, 13)] [InlineData(4, 99)]
    public void AnyMoveLeavesTheTotalAtEXACTLY100(int moved, int requested)
    {
        int[] weights = W(55, 15, 10, 12, 8);
        SectorAllocationModel.Rebalance(weights, moved, requested);

        Assert.Equal(SectorAllocationModel.Total, Sum(weights));
        Assert.Equal(requested, weights[moved]);
        Assert.True(SectorAllocationModel.IsBalanced(weights));
    }

    [Fact]
    public void ASEQUENCEOfMovesNeverDriftsOffTheInvariant()
    {
        // The defect this forbids is cumulative: totals like 97 or 104 arise
        // from a SERIES of independent adjustments, not from one. Drag every
        // slider in turn, repeatedly, and the sum must still be 100.
        int[] weights = W(20, 20, 20, 20, 20);
        int[] script = [80, 5, 45, 0, 33, 12, 60, 7, 100, 0, 25];

        for (int i = 0; i < script.Length; i++)
        {
            SectorAllocationModel.Rebalance(weights, i % Sectors.Count, script[i]);
            Assert.Equal(SectorAllocationModel.Total, Sum(weights));
        }
    }

    [Fact]
    public void TheOTHERSAbsorbTheDifferenceInPROPORTIONToTheirSize()
    {
        // The behaviour that reads as "the others make room" rather than "a
        // slider I never touched jumped". Farming 60 -> 20 frees 40; the other
        // four hold 20/10/6/4 of the remaining 40, so they take back in that
        // same ratio and land on 40/20/12/8.
        int[] weights = W(60, 20, 10, 6, 4);
        SectorAllocationModel.Rebalance(weights, Sectors.Farming, 20);

        Assert.Equal([20, 40, 20, 12, 8], weights);
        Assert.Equal(SectorAllocationModel.Total, Sum(weights));
    }

    [Fact]
    public void PushingOneSectorToTheLIMITEmptiesTheOthersRatherThanBreakingTheSum()
    {
        int[] weights = W(20, 20, 20, 20, 20);
        SectorAllocationModel.Rebalance(weights, Sectors.Crafting, 100);

        Assert.Equal([0, 0, 0, 100, 0], weights);
        Assert.Equal(SectorAllocationModel.Total, Sum(weights));
    }

    [Fact]
    public void ReducingASECTORThatHeldEverythingSpreadsTheFreedLabourEvenly()
    {
        // The degenerate case: with the other four at zero there is nothing to
        // scale up, so proportional absorption has no ratio to work from. 60
        // freed over four sectors is 15 each, exactly.
        int[] weights = W(0, 0, 0, 100, 0);
        SectorAllocationModel.Rebalance(weights, Sectors.Crafting, 40);

        Assert.Equal([15, 15, 15, 40, 15], weights);
        Assert.Equal(SectorAllocationModel.Total, Sum(weights));
    }

    [Fact]
    public void ATIEDenseSplitRedistributesDETERMINISTICALLYAndByLowestIndex()
    {
        // CLAUDE.md: an argmax over doubles ships a tie-dense test. An even
        // split is the shape a director reaches for first, and it makes EVERY
        // remainder identical — so the tie-break is the whole answer, not a
        // corner of it. 70 spread over four equal sectors is 17.5 each: two
        // sectors get 18 and two get 17, and it must be the two LOWEST indices
        // that round up, every time.
        int[] weights = W(20, 20, 20, 20, 20);
        SectorAllocationModel.Rebalance(weights, Sectors.Farming, 30);

        Assert.Equal([30, 18, 18, 17, 17], weights);

        // ...and identically on a repeat, from the same starting point.
        int[] again = W(20, 20, 20, 20, 20);
        SectorAllocationModel.Rebalance(again, Sectors.Farming, 30);
        Assert.Equal(weights, again);
    }

    [Fact]
    public void AnOutOfRangeRequestIsCLAMPEDRatherThanObeyed()
    {
        int[] low = W(20, 20, 20, 20, 20);
        SectorAllocationModel.Rebalance(low, Sectors.Herding, -40);
        Assert.Equal(0, low[Sectors.Herding]);
        Assert.Equal(SectorAllocationModel.Total, Sum(low));

        int[] high = W(20, 20, 20, 20, 20);
        SectorAllocationModel.Rebalance(high, Sectors.Herding, 250);
        Assert.Equal(100, high[Sectors.Herding]);
        Assert.Equal(SectorAllocationModel.Total, Sum(high));
    }

    [Fact]
    public void TheSnapFromTheWORLDSSharesAlsoSumsToEXACTLY100()
    {
        // THE LATENT DEFECT THIS CLOSES, and it needed no player at all.
        // Rounding each share independently loses units: five equal sectors are
        // 20 each and fine, but an uneven split rounds down in several places at
        // once and the panel opens reading 99. Largest remainder cannot.
        Span<int> into = stackalloc int[Sectors.Count];

        var even = new SectorAllocationRow(new SettlementId(0), 1.0, 1.0, 1.0, 1.0, 1.0);
        SectorAllocationModel.FromShares(even, into);
        Assert.Equal(SectorAllocationModel.Total, Sum(into));

        // Seven equal parts across five sectors: 14.28…% each, every one of
        // which floors to 14 for a total of 70 before the remainder is dealt.
        var awkward = new SectorAllocationRow(new SettlementId(0), 3.0, 1.0, 1.0, 1.0, 1.0);
        SectorAllocationModel.FromShares(awkward, into);
        Assert.Equal(SectorAllocationModel.Total, Sum(into));
        Assert.True(SectorAllocationModel.IsBalanced(into));
    }

    [Fact]
    public void ABalancedAllocationApplIESASTypedSoThePredictionIsAnIDENTITY()
    {
        // The reason the "applies as …" caption could go: with the weights
        // summing to 100, the share the sim runs IS the number on the slider.
        // Measured through the SIM's own normalization, not asserted.
        int[] weights = W(20, 20, 20, 20, 20);
        SectorAllocationModel.Rebalance(weights, Sectors.Farming, 44);

        var row = new SectorAllocationRow(new SettlementId(0), 0, 0, 0, 0, 0);
        for (int s = 0; s < Sectors.Count; s++) row = Sectors.With(row, s, weights[s] / 100.0);

        for (int s = 0; s < Sectors.Count; s++)
        {
            Assert.Equal(weights[s] / 100.0, Sectors.Share(row, s), 9);
        }
    }

    [Fact]
    public void IsBalancedREJECTSTheTotalsTheOldPanelCouldShow()
    {
        Assert.True(SectorAllocationModel.IsBalanced(W(20, 20, 20, 20, 20)));
        Assert.False(SectorAllocationModel.IsBalanced(W(20, 20, 20, 20, 17)));   // 97
        Assert.False(SectorAllocationModel.IsBalanced(W(24, 20, 20, 20, 20)));   // 104
        Assert.False(SectorAllocationModel.IsBalanced(W(0, 0, 0, 0, 0)));        // the sigma=0 order
        Assert.False(SectorAllocationModel.IsBalanced(W(50, 50)));               // wrong shape
    }
}
