using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-054 / ADR-039 — ORDER KINDS 11 (CancelConstruction, WS1) and 12 (SetResourceAllocation, WS2): the
/// load-time payload rules (OrderLog.ValidateRecord), the world rules (OrderValidation.ValidateAgainstWorld: kind 11 as
/// EnqueueConstruction — the settlement exists and the issuer controls it; kind 12 as SetTaxRate — an Empire sets its own),
/// and the observability mapping (TurnRecord.OrderApplied.Settlement). Kind 10 stays reserved (MoveFormation, M7).
/// </summary>
public class ConstructionOrderKindsTests
{
    private static OrderLog RoundTrip(params OrderRecord[] records)
    {
        var log = new OrderLog();
        foreach (OrderRecord r in records) log.Append(r);
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        return OrderLog.Load(ms);
    }

    [Theory]
    [InlineData(-1.0, "building share must be a whole number in [0,100]")]
    [InlineData(101.0, "building share must be a whole number in [0,100]")]
    [InlineData(50.5, "building share must be a whole number in [0,100]")]
    [InlineData(double.NaN, "building share must be a whole number in [0,100]")]
    public void Kind12_LoadRejectsANonWholeOrOutOfRangeShare(double amount, string fragment)
    {
        var e = Assert.Throws<SnapshotFormatException>(() => RoundTrip(new OrderRecord(0, 1, OrderKind.SetResourceAllocation, 1, amount)));
        Assert.Contains(fragment, e.Message);
        var neg = Assert.Throws<SnapshotFormatException>(() => RoundTrip(new OrderRecord(0, 1, OrderKind.SetResourceAllocation, -2, 50.0)));
        Assert.Contains("SetResourceAllocation target polity id must be >= 0", neg.Message);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(100.0)]
    [InlineData(70.0)]
    public void Kind12_LoadAcceptsWholeSharesIncludingBothEnds(double amount) =>
        Assert.Equal(amount, RoundTrip(new OrderRecord(0, 1, OrderKind.SetResourceAllocation, 1, amount))[0].Amount);

    [Fact]
    public void Kind11_PayloadRule_IsWholeFiniteNonNegativeIntRange()
    {
        Assert.True(OrderLog.ConstructionSlotValid(0.0));
        Assert.True(OrderLog.ConstructionSlotValid(int.MaxValue));
        Assert.False(OrderLog.ConstructionSlotValid(-1.0));
        Assert.False(OrderLog.ConstructionSlotValid(2.5));
        Assert.False(OrderLog.ConstructionSlotValid(double.NaN));
        Assert.False(OrderLog.ConstructionSlotValid(double.PositiveInfinity));
        Assert.False(OrderLog.ConstructionSlotValid((double)int.MaxValue + 1.0));
        Assert.True(OrderLog.AllocationPercentValid(0.0) && OrderLog.AllocationPercentValid(100.0));
        Assert.False(OrderLog.AllocationPercentValid(100.0000001) || OrderLog.AllocationPercentValid(-0.5));
    }

    private static readonly Lazy<WorldState> Duo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.Sim(), 42));

    [Fact]
    public void OrderValidation_Kind11_NeedsControl_AndAnExistingSettlement_LikeEnqueueConstruction()
    {
        WorldState w = Duo.Value;
        var player = new PolityId(1);
        var log = new OrderLog();
        log.Append(new OrderRecord(0, 1, OrderKind.CancelConstruction, 0, 0.0));   // S0 is the player's
        OrderValidation.ValidateAgainstWorld(log, w);
        var foreign = new OrderLog();
        foreign.Append(new OrderRecord(0, player.Value, OrderKind.CancelConstruction, 1, 0.0));   // S1 is the AI's
        var e = Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(foreign, w));
        Assert.Contains("CancelConstruction targets settlement 1, which polity 1 does not control", e.Message);
        var missing = new OrderLog();
        missing.Append(new OrderRecord(0, 2, OrderKind.CancelConstruction, 77, 0.0));
        Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(missing, w));
    }

    [Fact]
    public void OrderValidation_Kind12_AnEmpireSetsOnlyItsOwnSplit()
    {
        WorldState w = Duo.Value;
        var own = new OrderLog();
        own.Append(new OrderRecord(0, 1, OrderKind.SetResourceAllocation, 1, 60.0));
        OrderValidation.ValidateAgainstWorld(own, w);
        var other = new OrderLog();
        other.Append(new OrderRecord(0, 1, OrderKind.SetResourceAllocation, 2, 60.0));
        Assert.Contains("SetResourceAllocation targets polity 2 but was issued by polity 1",
            Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(other, w)).Message);
    }

    [Fact]
    public void TurnRecord_MapsAKind11ToItsSettlement_AndAKind12ToNone()
    {
        Assert.Equal(4, new OrderApplied(0, 1, 1, OrderKind.CancelConstruction, 4, 2.0).Settlement);
        Assert.Equal(-1, new OrderApplied(0, 1, 1, OrderKind.SetResourceAllocation, 1, 60.0).Settlement);
    }

    [Fact]
    public void Kind10_StaysReserved_NoMemberOfTheEnumHoldsIt()
    {
        foreach (OrderKind k in Enum.GetValues<OrderKind>()) Assert.NotEqual(10, (int)k);
        Assert.Equal(11, (int)OrderKind.CancelConstruction);
        Assert.Equal(12, (int)OrderKind.SetResourceAllocation);
    }
}
