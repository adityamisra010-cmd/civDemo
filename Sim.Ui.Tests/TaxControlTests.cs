using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Governance;
using Sim.Core.Worldgen;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-033 D4 (ported from <c>m5-full-build</c>'s TaxControlTests, 8 cases): the tax edict at the
/// view-model and session seam (headless — no Game, no window), and the AI's use of the SAME order
/// pathway. The mechanism is pinned in Sim.Tests (GovernanceTests, GovernanceTimingTests). What these
/// cover is the UI's ability to ISSUE the edict and to REPORT it honestly: the order is the only route
/// in, it is refused when the research gate is closed, the panel shows what is COLLECTED beside what is
/// declared, and the AI reaches the world through the same log the director writes to.
///
/// WHAT CHANGED IN THE PORT. Taxation is research-gated by the content (ADR-033 D4), so a fresh
/// session cannot tax; the cases that legislate first COMPLETE ONE OF THE TAXATION NODES in the
/// session's world (the constructed-knowledge rig the Age tests use). The AI valve is a pure policy
/// that this port deliberately does not wire into EndTurn (the single AI order producer is stream
/// S2's), so the last case appends the valve's orders to the session log exactly as that producer
/// will, and proves they take effect through the ordinary step.
/// </summary>
public class TaxControlTests
{
    /// <summary>Completes the Taxation civic for <paramref name="polity"/> in a
    /// session world — the research gate's single node (R5), named in sim.json taxationRequires.</summary>
    private static void GrantTaxation(WorldState world, SimConfig cfg, PolityId polity)
    {
        int index = cfg.Research!.IndexOfId("taxation");
        Assert.True(index >= 0);
        world.ResearchCompleted.Add(new ResearchCompletedRow(polity, cfg.Research.Nodes[index].Key));
        Assert.True(Governance.CanLevyTax(world, cfg, polity));
    }

    [Fact]
    public void TheEdictIsSelfTargetedAndCarriesThePercentAsTyped()
    {
        OrderRecord o = TaxOrderFactory.Create(currentTurn: 17, percent: 35);

        Assert.Equal(17, o.Turn);
        Assert.Equal(OrderKind.SetTaxRate, o.Kind);
        Assert.Equal(TaxOrderFactory.PlayerEmpire.Value, o.ActorId);
        Assert.Equal(o.ActorId, o.TargetId);
        Assert.Equal(35.0, o.Amount);
        // ONE constructor for player and AI alike.
        Assert.Equal(Governance.TaxOrder(17, TaxOrderFactory.PlayerEmpire, 35), o);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ARateOutsideTheLegislativeRangeIsRefusedAtTheSource(int percent)
    {
        Assert.False(TaxOrderFactory.CanSubmit(percent));
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxOrderFactory.Create(currentTurn: 0, percent));
    }

    [Fact]
    public void TheSessionRefusesAnIllegalRateOrAClosedGate_AndWritesNOTHING()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int before = session.Orders.Count;

        // Turn 1 is a primitive civilization: no taxation knowledge, so even a legal rate is refused.
        Assert.False(Governance.CanLevyTax(session.World, session.Config, TaxOrderFactory.PlayerEmpire));
        Assert.False(session.EmitTaxOrder(20));
        Assert.Equal(before, session.Orders.Count);

        GrantTaxation(session.World, session.Config, TaxOrderFactory.PlayerEmpire);
        Assert.False(session.EmitTaxOrder(101));
        Assert.Equal(before, session.Orders.Count);

        Assert.True(session.EmitTaxOrder(20));
        Assert.Equal(before + 1, session.Orders.Count);
    }

    [Fact]
    public void AnEdictTakesEffectThroughTheORDINARYStepAndTheLogRecordsIt()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        PolityId player = TaxOrderFactory.PlayerEmpire;
        GrantTaxation(session.World, session.Config, player);

        Assert.Equal(0.0, Governance.NominalTaxRate(session.World, player));
        Assert.True(session.EmitTaxOrder(30));
        session.EndTurn();

        Assert.Equal(0.30, Governance.NominalTaxRate(session.World, player), 9);
        Assert.Equal(30, TaxOrderFactory.DeclaredPercent(session.World, player));
        OrderRecord logged = session.Orders[session.Orders.Count - 1];
        Assert.Equal(OrderKind.SetTaxRate, logged.Kind);
        Assert.Equal(0, logged.Turn);
    }

    [Fact]
    public void ThePanelReportsWhatIsCOLLECTEDNotOnlyWhatIsDeclared()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        GrantTaxation(session.World, session.Config, TaxOrderFactory.PlayerEmpire);
        Assert.True(session.EmitTaxOrder(50));
        session.EndTurn();
        session.EndTurn();   // reach is computed from the previous turn's distances: let it settle

        IReadOnlyList<string> lines = TaxOrderFactory.BurdenLines(
            session.World, TaxOrderFactory.PlayerEmpire, session.Config);

        Assert.NotEmpty(lines);
        foreach (string line in lines)
        {
            Assert.Contains("reach", line);
            Assert.Contains("collects", line);
        }
        // The capital collects the full declared rate; the reach shown is the STORED Strength.
        Assert.True(EmpireQuery.TryGetCapital(session.World, TaxOrderFactory.PlayerEmpire, out SettlementId seat));
        Assert.Contains($"#{seat.Value}: reach 100% -> collects 50%", lines);

        Assert.Contains("legitimacy", TaxOrderFactory.LegitimacyLine(
            session.World, TaxOrderFactory.PlayerEmpire, session.Config));
    }

    [Fact]
    public void TheBurdenLinesAreOrderedByAStableIntegerKey()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        session.EndTurn();

        IReadOnlyList<string> a = TaxOrderFactory.BurdenLines(session.World, TaxOrderFactory.PlayerEmpire, session.Config);
        IReadOnlyList<string> b = TaxOrderFactory.BurdenLines(session.World, TaxOrderFactory.PlayerEmpire, session.Config);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
        // Ascending settlement id, never table order.
        int last = -1;
        foreach (string line in a)
        {
            int id = int.Parse(line[1..line.IndexOf(':')], System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(id > last);
            last = id;
        }
    }

    [Fact]
    public void TheAiLegislatesThroughTheSAMELogAndStepTheDirectorUses()
    {
        // Law 7 / ADR-033 D5: the AI uses player-identical verbs. The valve's orders are appended to
        // the session log BEFORE the step — where the single AI producer (stream S2) will append them —
        // and they take effect through OrderValidation and GovernanceSystem like the director's.
        SimConfig cfg = UiFounding.ProductionConfig();
        WorldgenConfig wg;
        using (var st = global::Sim.Data.DataFiles.OpenWorldgen()) wg = WorldgenConfigLoader.Load(st);
        WorldState w = WorldFounding.Found(wg with { SizePx = 256, AiEmpires = 1 }, cfg, 42, 4);
        PolityId ai = default;
        bool found = false;
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Ai) { ai = w.Polities[i].Id; found = true; }
        Assert.True(found, "the founded world has no AI Empire — the test would be vacuous");
        GrantTaxation(w, cfg, ai);

        UiSession session = UiSession.StartFrom(w, 42, 256, 4);
        int before = session.Orders.Count;
        OrderRecord[] edicts = AiGovernance.OrdersFor(session.World, session.Config, ai, session.World.Clock.Turn);
        Assert.Single(edicts);   // a content founding realm is comfortable: the valve raises the levy
        foreach (OrderRecord o in edicts) session.Orders.Append(o);
        OrderValidation.ValidateAgainstWorld(session.Orders, session.World);   // the same validator
        session.EndTurn();

        Assert.True(session.Orders.Count > before);
        for (int i = before; i < session.Orders.Count; i++)
        {
            OrderRecord o = session.Orders[i];
            if (o.Kind != OrderKind.SetTaxRate) continue;
            Assert.NotEqual(TaxOrderFactory.PlayerEmpire.Value, o.ActorId);
            Assert.Equal(o.ActorId, o.TargetId);   // an AI Empire legislates its own
        }
        Assert.True(Governance.NominalTaxRate(session.World, ai) > 0.0, "the AI edict did not take effect through the ordinary step");
        Assert.Equal(0.0, Governance.NominalTaxRate(session.World, TaxOrderFactory.PlayerEmpire));   // the player is never taxed by the valve
    }
}
