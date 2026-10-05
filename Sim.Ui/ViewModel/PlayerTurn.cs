namespace Sim.Ui.ViewModel;

/// <summary>
/// F3 (M5 hardening): THE PLAYER-FACING TURN NUMBER. The simulation clock counts turns RESOLVED (Clock.Turn = 0 on
/// the founded world, before any End Turn); the player is always playing the NEXT one. The annals, the previews, the
/// playability gate and the baseline all call the first turn "Turn 1", and every observability stamp (TurnRecord.Turn,
/// PolicyChange.Turn, FoundedTurn) is the clock of the state the resolved turn WROTE — i.e. the number of the turn
/// just played. So a screen that shows the turn being played shows <c>Clock.Turn + 1</c>; a stamp of a resolved turn
/// is shown as stored. UI text only — the simulation clock is never changed.
/// </summary>
public static class PlayerTurn
{
    /// <summary>The number of the turn being played on a world whose clock reads <paramref name="clockTurn"/>.</summary>
    public static long Current(long clockTurn) => clockTurn + 1;
}
