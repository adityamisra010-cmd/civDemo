using Xunit;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>F3 item 4 (M5 hardening): one player-facing turn number everywhere. The status band shows the turn being
/// played (Clock.Turn + 1, so the founded world reads "turn 1" as the annals, previews, gate and baseline call it), and
/// the record of the turn just played (TurnRecord.Turn, the turn-audit header) carries that same number. The simulation
/// clock itself is untouched.</summary>
public class PlayerTurnNumberingTests
{
    [Fact]
    public void StatusBand_and_turn_audit_agree_on_the_turn_number_across_an_End_Turn()
    {
        UiSession s = UiSession.Start(42);
        Assert.Equal(0, s.World.Clock.Turn); // the clock is not renumbered
        Assert.StartsWith("turn 1 ", HudModel.From(s.World, selectedSettlementId: 0).ClockLine);
        s.EndTurn();
        Assert.Equal(1, s.World.Clock.Turn);
        // The turn just played is turn 1: its record and the audit header say so; the band has moved on to turn 2.
        Assert.Equal(1, s.Observations.LastTurn);
        Assert.StartsWith("turn 1 ", TurnAuditModel.HeaderLine(s.Observations.Observations[^1].Turn));
        Assert.StartsWith("turn 2 ", HudModel.From(s.World, selectedSettlementId: 0).ClockLine);
    }
}
