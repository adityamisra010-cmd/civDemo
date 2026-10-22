using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-029 §4 — OrderKind 6, SetResearchTarget, on the fixed five-field record. The
/// wire format is untouched: IoVersion stays 1 and the kind rides the existing Kind
/// int. A malformed payload is rejected at LOAD, actionably, and never mid-turn.
/// </summary>
public class ResearchOrderTests
{
    private static OrderLog RoundTrip(OrderRecord record)
    {
        var log = new OrderLog();
        log.Append(record);
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        return OrderLog.Load(ms);
    }

    [Fact]
    public void SetResearchTarget_IsKindSix_AndRoundTripsThroughTheUnchangedWireFormat()
    {
        Assert.Equal(6, (int)OrderKind.SetResearchTarget);
        Assert.Equal(1, OrderLog.IoVersion);
        OrderRecord set = OrderRecord.From(12, new PolityId(1), OrderKind.SetResearchTarget, 1001, 0.0);
        Assert.Equal(set, RoundTrip(set)[0]);
        OrderRecord clear = OrderRecord.From(13, new PolityId(1), OrderKind.SetResearchTarget, -1, 0.0);
        Assert.Equal(clear, RoundTrip(clear)[0]);
    }

    [Theory]
    [InlineData(0, 0.0, "node key must be a research.json key")]
    [InlineData(-2, 0.0, "node key must be a research.json key")]
    [InlineData(5, 1.0, "Amount is reserved and must be 0")]
    [InlineData(5, double.NaN, "Amount is reserved and must be 0")]
    public void SetResearchTarget_AMalformedPayload_IsRejectedAtLoad(int target, double amount, string fragment)
    {
        var e = Assert.Throws<SnapshotFormatException>(() =>
            RoundTrip(OrderRecord.From(3, new PolityId(1), OrderKind.SetResearchTarget, target, amount)));
        Assert.Contains(fragment, e.Message);
    }

    [Fact]
    public void AnUnknownKind_NamesEveryKindThisBuildUnderstands()
    {
        // ADR-033 D4: kind 5 is SetTaxRate now (the reservation became the real kind).
        // M5 S1b (ADR-036): kind 9 is FoundSettlement now, so the first kind this build does NOT understand
        // moved 9 -> 10 (cause: the new kind, not a behaviour change; kind 9 round-trips in OrderLogFoundSettlementTests).
        var e = Assert.Throws<SnapshotFormatException>(() =>
            RoundTrip(new OrderRecord(3, 1, (OrderKind)10, 0, 0.0)));
        Assert.Contains("unknown order kind 10", e.Message);
        Assert.Contains("5 (SetTaxRate)", e.Message);
        Assert.Contains("6 (SetResearchTarget)", e.Message);
        Assert.Contains("9 (FoundSettlement)", e.Message);
        // D-054 (ADR-039): kinds 11 (CancelConstruction) and 12 (SetResourceAllocation) are new; 10 STAYS unknown — it is
        // reserved for MoveFormation (M7), and the message says so.
        Assert.Contains("11 (CancelConstruction)", e.Message);
        Assert.Contains("12 (SetResourceAllocation)", e.Message);
        Assert.Contains("kind 10 is reserved for MoveFormation (M7)", e.Message);
        // D-056: kind 13 is CancelConstructionEntry now (the identity cancel), so the first kind past the end moved 13 -> 14
        // (cause: the new kind; kind 13 round-trips in LegacyCancelGuardTests).
        Assert.Contains("13 (CancelConstructionEntry)", e.Message);
        Assert.Contains("kind 11 is legacy (read, no longer written: D-056)", e.Message);
        // M6 Track EX (D-062, ADR-044): kinds 14 (Explore) and 15 (Prospect) are new, so the first kind past the end moved
        // 14 -> 16 (cause: the two new kinds; both round-trip in DiscoveryOrderLogTests).
        Assert.Contains("14 (Explore)", e.Message);
        Assert.Contains("15 (Prospect)", e.Message);
        var sixteen = Assert.Throws<SnapshotFormatException>(() => RoundTrip(new OrderRecord(3, 1, (OrderKind)16, 0, 0.0)));
        Assert.Contains("unknown order kind 16", sixteen.Message);
    }
}
