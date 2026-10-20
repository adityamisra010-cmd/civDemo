using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// THE EMPIRE THE HUMAN DIRECTOR COMMANDS — the one place the UI names it. It lived on the M1
/// single-slider <c>LaborOrderFactory</c>, which ADR-033 D9 retires; every order factory, screen and
/// preview now reads it here.
///
/// It is an Empire identity, not a "player" marker (M4-B §3 forbids that reading, and D-042 makes command
/// source a property of the Empire's roster row rather than of the actor id). The numeric value is the id
/// this UI has always written, so every existing order log and replay fixture keeps its meaning. It is a
/// STANDING DEFAULT, not a decision: when a session can command a different Empire it becomes a lookup of
/// the roster's player-commanded Empire, and the constant goes.
/// </summary>
public static class UiPlayer
{
    /// <summary>The player's Empire (polity 1, the founding roster's player-commanded Empire).</summary>
    public static readonly PolityId Empire = new(1);

    /// <summary>The same identity as the raw order actor id.</summary>
    public const int ActorId = 1;
}
