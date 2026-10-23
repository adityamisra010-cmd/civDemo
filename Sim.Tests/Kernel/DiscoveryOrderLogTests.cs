using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M6 Track EX (D-062; ADR-044) — ORDER KINDS 14 (Explore) AND 15 (Prospect) ON THE WIRE: the unchanged five-field record
/// (IoVersion 1), TargetId = a settlement, Amount reserved = 0. Load refuses a negative target and any non-zero Amount
/// (NaN included); the world-dependent pass refuses a settlement the issuer does not control at turn 0, exactly as it does
/// construction; the settlement-addressed readers (TurnRecord, ReplayProof) name the target.
/// </summary>
public class DiscoveryOrderLogTests
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

    [Theory]
    [InlineData(OrderKind.Explore, 14)]
    [InlineData(OrderKind.Prospect, 15)]
    public void TheKinds_RoundTrip_AndTheirPayloadIsRangeChecked(OrderKind kind, int value)
    {
        Assert.Equal(value, (int)kind);
        Assert.Equal(1, OrderLog.IoVersion);
        var ok = new OrderRecord(4, 1, kind, 3, 0.0);
        Assert.Equal(ok, RoundTrip(ok)[0]);
        // ADR-046 §2.4: Prospect's Amount is the method — exactly 0 (survey) or 1 (assay); Explore's stays exactly 0.
        bool prospect = kind == OrderKind.Prospect;
        if (prospect)
        {
            var assay = new OrderRecord(4, 1, kind, 3, 1.0);
            Assert.Equal(assay, RoundTrip(assay)[0]);
        }
        string message = prospect ? "Amount is the method and must be 0 (survey) or 1 (assay)" : "Amount is reserved and must be 0";
        double[] bads = prospect ? [2.0, -1.0, double.NaN, double.PositiveInfinity, 0.5, -0.0 + 1e-300]
                                 : [1.0, -1.0, double.NaN, double.PositiveInfinity, 0.5];
        foreach (double bad in bads)
            Assert.Contains(message,
                Assert.Throws<SnapshotFormatException>(() => RoundTrip(new OrderRecord(4, 1, kind, 3, bad))).Message);
        Assert.Contains("settlement id must be >= 0",
            Assert.Throws<SnapshotFormatException>(() => RoundTrip(new OrderRecord(4, 1, kind, -1, 0.0))).Message);
    }

    [Fact]
    public void TheSettlementAddressedReaders_NameTheTarget()
    {
        Assert.Equal(7, new OrderApplied(0, 1, 1, OrderKind.Explore, 7, 0.0).Settlement);
        Assert.Equal(7, new OrderApplied(0, 1, 1, OrderKind.Prospect, 7, 0.0).Settlement);
        Assert.Equal(7, ReplayProof.AddressedSettlement(new OrderRecord(0, 1, OrderKind.Explore, 7, 0.0)));
        Assert.Equal(7, ReplayProof.AddressedSettlement(new OrderRecord(0, 1, OrderKind.Prospect, 7, 0.0)));
    }

    [Fact]
    public void ValidateAgainstWorld_RefusesASettlementTheIssuerDoesNotControl_AndAcceptsItsOwn()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.Sim(), 42);
        int mine = -1, theirs = -1;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId id = w.Settlements[i].Id;
            if (EmpireQuery.ControlsSettlement(w, new PolityId(1), id)) mine = id.Value; else theirs = id.Value;
        }
        foreach (OrderKind kind in new[] { OrderKind.Explore, OrderKind.Prospect })
        {
            var good = new OrderLog();
            good.Append(new OrderRecord(0, 1, kind, mine, 0.0));
            OrderValidation.ValidateAgainstWorld(good, w);
            var bad = new OrderLog();
            bad.Append(new OrderRecord(0, 1, kind, theirs, 0.0));
            Assert.Contains("does not control", Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(bad, w)).Message);
            var missing = new OrderLog();
            missing.Append(new OrderRecord(0, 1, kind, 999, 0.0));
            Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(missing, w));
        }
    }
}
