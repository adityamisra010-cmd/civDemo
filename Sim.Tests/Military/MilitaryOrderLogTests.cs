using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>ADR-055 — ORDER KINDS 16 (RecruitFormation), 17 (ReequipFormation) and 18 (DisbandFormation) ON THE WIRE: the
/// unchanged five-field record (IoVersion 1); the load-time range checks; the world-dependent pass (control of the settlement,
/// ownership of the formation, the deferral of an id a later recruitment can create); and the settlement-addressed readers.</summary>
public class MilitaryOrderLogTests
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
    public void TheKinds_AreAppended_AndRoundTrip()
    {
        Assert.Equal((16, 17, 18), ((int)OrderKind.RecruitFormation, (int)OrderKind.ReequipFormation, (int)OrderKind.DisbandFormation));
        Assert.Equal(1, OrderLog.IoVersion);
        foreach (OrderRecord ok in new[]
        {
            new OrderRecord(3, 1, OrderKind.RecruitFormation, 2, 302.0),
            new OrderRecord(3, 1, OrderKind.ReequipFormation, 5, 201.0),
            new OrderRecord(3, 1, OrderKind.ReequipFormation, 5, 0.0),    // cancel
            new OrderRecord(3, 1, OrderKind.DisbandFormation, 5, 0.0),
        })
            Assert.Equal(ok, RoundTrip(ok)[0]);
    }

    [Theory]
    [InlineData(OrderKind.RecruitFormation, -1, 302.0, "settlement id must be >= 0")]
    [InlineData(OrderKind.RecruitFormation, 2, 0.0, "identity key must be a whole number >= 1")]
    [InlineData(OrderKind.RecruitFormation, 2, 1.5, "identity key must be a whole number >= 1")]
    [InlineData(OrderKind.RecruitFormation, 2, double.NaN, "identity key must be a whole number >= 1")]
    [InlineData(OrderKind.ReequipFormation, 0, 201.0, "formation id must be >= 1")]
    [InlineData(OrderKind.ReequipFormation, 5, -1.0, "target identity key must be a whole number")]
    [InlineData(OrderKind.ReequipFormation, 5, double.PositiveInfinity, "target identity key must be a whole number")]
    [InlineData(OrderKind.DisbandFormation, 0, 0.0, "formation id must be >= 1")]
    [InlineData(OrderKind.DisbandFormation, 5, 1.0, "Amount is reserved and must be 0")]
    public void Load_RefusesAMalformedPayload(OrderKind kind, int target, double amount, string message)
    {
        Assert.Contains(message, Assert.Throws<SnapshotFormatException>(() => RoundTrip(new OrderRecord(3, 1, kind, target, amount))).Message);
    }

    [Fact]
    public void ValidateAgainstWorld_RefusesAnotherEmpiresSettlementOrFormation_AndDefersALaterFormationId()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42);
        int mine = -1, theirs = -1, myUnit = -1, theirUnit = -1;
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, Player, w.Settlements[i].Id)) mine = w.Settlements[i].Id.Value; else theirs = w.Settlements[i].Id.Value;
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
            if (w.MilitaryUnits[i].Owner == Player) myUnit = w.MilitaryUnits[i].Id; else theirUnit = w.MilitaryUnits[i].Id;

        void Ok(OrderRecord r) { var l = new OrderLog(); l.Append(r); OrderValidation.ValidateAgainstWorld(l, w); }
        string Bad(OrderRecord r) { var l = new OrderLog(); l.Append(r); return Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAgainstWorld(l, w)).Message; }

        Ok(new OrderRecord(0, 1, OrderKind.RecruitFormation, mine, 201));
        Assert.Contains("does not control", Bad(new OrderRecord(0, 1, OrderKind.RecruitFormation, theirs, 201)));
        Assert.Contains("does not control", Bad(new OrderRecord(0, 1, OrderKind.RecruitFormation, 999, 201)));
        Ok(new OrderRecord(0, 1, OrderKind.ReequipFormation, myUnit, 201));
        Ok(new OrderRecord(0, 1, OrderKind.DisbandFormation, myUnit, 0));
        Assert.Contains("does not own", Bad(new OrderRecord(0, 1, OrderKind.ReequipFormation, theirUnit, 201)));
        Assert.Contains("does not own", Bad(new OrderRecord(0, 1, OrderKind.DisbandFormation, theirUnit, 0)));
        Assert.Contains("does not exist", Bad(new OrderRecord(0, 1, OrderKind.DisbandFormation, 99, 0)));   // turn 0: no deferral
        Ok(new OrderRecord(4, 1, OrderKind.DisbandFormation, 99, 0));                                       // a later recruitment may create it
    }

    [Fact]
    public void TheSettlementAddressedReaders_NameARecruitmentsSettlement()
    {
        Assert.Equal(7, new OrderApplied(0, 1, 1, OrderKind.RecruitFormation, 7, 201.0).Settlement);
        Assert.Equal(7, ReplayProof.AddressedSettlement(new OrderRecord(0, 1, OrderKind.RecruitFormation, 7, 201.0)));
        Assert.Equal(-1, ReplayProof.AddressedSettlement(new OrderRecord(0, 1, OrderKind.ReequipFormation, 7, 201.0)));
    }
}
