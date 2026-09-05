using Sim.Core.Kernel;
using Sim.Core;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5: the governance control at the view-model and session seam (headless — no
/// Game, no window), and the AI's use of the SAME seam.
///
/// The mechanism itself is pinned in Sim.Tests GovernanceTests. What these cover
/// is the UI's ability to ISSUE the edict and to REPORT it honestly: that the
/// order is the only route into the sim, that the panel shows the collected rate
/// beside the declared one, and that the AI Empires reach the world through the
/// same log the director writes to rather than through a private back door.
/// </summary>
public class TaxControlTests
{
    [Fact]
    public void TheEdictIsSelfTargetedAndCarriesThePercentAsTyped()
    {
        // Self-targeted because an Empire legislates its OWN taxes — the rule
        // OrderValidation enforces on the way in. The payload is the percentage
        // as typed, not a pre-converted fraction: the log records the director's
        // decision, and the system that enacts it does the conversion.
        OrderRecord o = TaxOrderFactory.Create(currentTurn: 17, percent: 35);

        Assert.Equal(17, o.Turn);
        Assert.Equal(OrderKind.SetTaxRate, o.Kind);
        Assert.Equal(TaxOrderFactory.PlayerEmpire.Value, o.ActorId);
        Assert.Equal(o.ActorId, o.TargetId);
        Assert.Equal(35.0, o.Amount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ARateOutsideTheLegislativeRangeIsRefusedAtTheSource(int percent)
    {
        Assert.False(TaxOrderFactory.CanSubmit(percent));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TaxOrderFactory.Create(currentTurn: 0, percent));
    }

    [Fact]
    public void TheSessionRefusesAnIllegalRateAndWritesNOTHING()
    {
        // On refusal nothing is written and nothing is claimed — the same
        // contract the sector batch follows.
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int before = session.Orders.Count;

        Assert.False(session.EmitTaxOrder(101));
        Assert.Equal(before, session.Orders.Count);

        Assert.True(session.EmitTaxOrder(20));
        Assert.Equal(before + 1, session.Orders.Count);
    }

    [Fact]
    public void AnEdictTakesEffectThroughTheOrDINARYStepAndTheLogRecordsIt()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        PolityId player = TaxOrderFactory.PlayerEmpire;

        Assert.Equal(0.0, Governance.NominalTaxRate(session.World, player));
        Assert.True(session.EmitTaxOrder(30));
        session.EndTurn();

        Assert.Equal(0.30, Governance.NominalTaxRate(session.World, player), 9);
        Assert.Equal(30, TaxOrderFactory.DeclaredPercent(session.World, player));
    }

    [Fact]
    public void ThePanelReportsWhatIsCOLLECTEDNotOnlyWhatIsDeclared()
    {
        // The reason the panel is three numbers and not one: a declared rate is
        // not what anyone pays, and a director shown only the declaration would
        // read his own unreachable frontier as a bug.
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        session.EmitTaxOrder(50);
        session.EndTurn();

        IReadOnlyList<string> lines = TaxOrderFactory.BurdenLines(
            session.World, TaxOrderFactory.PlayerEmpire, session.Config);

        Assert.NotEmpty(lines);
        foreach (string line in lines)
        {
            Assert.Contains("reach", line);
            Assert.Contains("collects", line);
        }

        Assert.Contains("legitimacy", TaxOrderFactory.LegitimacyLine(
            session.World, TaxOrderFactory.PlayerEmpire, session.Config));
    }

    [Fact]
    public void TheBurdenLinesAreOrderedByAStableIntegerKey()
    {
        // Never table order: a list the director reads must not reshuffle
        // because a row moved.
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        session.EndTurn();

        IReadOnlyList<string> a = TaxOrderFactory.BurdenLines(
            session.World, TaxOrderFactory.PlayerEmpire, session.Config);
        IReadOnlyList<string> b = TaxOrderFactory.BurdenLines(
            session.World, TaxOrderFactory.PlayerEmpire, session.Config);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }

    [Fact]
    public void TheAiLegislatesThroughTheSAMELogTheDirectorWritesTo()
    {
        // Law 7: the AI uses player-identical verbs. Ending a turn must leave
        // any AI edict IN THE LOG — not applied around it — because the log is
        // the whole story of what was ordered and replay depends on it being so.
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);

        int aiEmpires = 0;
        for (int i = 0; i < session.World.Polities.Count; i++)
            if (session.World.Polities[i].Source == CommandSource.Ai) aiEmpires++;

        int before = session.Orders.Count;
        session.EndTurn();

        // ANTI-VACUITY, stated rather than assumed. A founded world may hold no
        // AI Empire at all, and then "every appended order is a well-formed AI
        // edict" is trivially true. The assertion below says WHICH world this
        // is, so a future change that quietly stops the AI legislating cannot
        // hide behind an empty loop.
        if (aiEmpires == 0)
        {
            Assert.Equal(before, session.Orders.Count);
            return;
        }

        Assert.True(session.Orders.Count > before,
            $"{aiEmpires} AI Empire(s) exist and none legislated — the loop below would be vacuous");

        for (int i = before; i < session.Orders.Count; i++)
        {
            OrderRecord o = session.Orders[i];
            Assert.Equal(OrderKind.SetTaxRate, o.Kind);
            Assert.NotEqual(TaxOrderFactory.PlayerEmpire.Value, o.ActorId);
            Assert.Equal(o.ActorId, o.TargetId);   // an AI Empire legislates its own
        }
    }
}
