using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M5 resources view — WHICH SETTLEMENTS WERE THE PLAYER'S, turn by turn: UI-side history, never sim state (the D-028
/// shape of <see cref="HistoryBuffer"/>). One sample per observed world — the founding world at session start and the
/// world after every step — of the places the player's Empire controlled then (<see cref="EmpireStores.OwnSettlements"/>,
/// the control relation). The RESOURCES view's trends sum a turn's records over the settlements that were the player's
/// ON THAT TURN only, so a settlement taken from a rival never brings its foreign past into the player's charts, and one
/// lost to a rival stops counting from the turn it was lost. Replay rebuilds it (UiSession's one Step path); a resumed
/// session replays, so it is rebuilt there too. Lookup by turn in a list; no dictionary is iterated.
/// </summary>
public sealed class OwnershipHistory
{
    private readonly List<long> _turns = [];
    private readonly List<int[]> _own = [];

    /// <summary>Samples captured so far.</summary>
    public int Count => _turns.Count;

    /// <summary>Records the player's settlements in <paramref name="world"/> under its turn; a repeated capture of the same
    /// turn is ignored (a redraw must not double-sample).</summary>
    public void Capture(IReadOnlyWorldState world, PolityId player)
    {
        ArgumentNullException.ThrowIfNull(world);
        long turn = world.Clock.Turn;
        if (_turns.Count > 0 && _turns[^1] == turn) return;
        _turns.Add(turn);
        _own.Add(EmpireStores.OwnSettlements(world, player));
    }

    /// <summary>The player's settlements at <paramref name="turn"/> (ascending ids), or null when that turn was never
    /// captured.</summary>
    public int[]? At(long turn)
    {
        for (int i = _turns.Count - 1; i >= 0; i--) if (_turns[i] == turn) return _own[i];
        return null;
    }

    /// <summary>Whether <paramref name="settlement"/> was the player's at <paramref name="turn"/> (false for a turn never
    /// captured).</summary>
    public bool Owned(long turn, int settlement) => At(turn) is { } own && Array.BinarySearch(own, settlement) >= 0;

    /// <summary>A history built from explicit samples (tests): <paramref name="samples"/> in turn order.</summary>
    public static OwnershipHistory Of(params (long Turn, int[] Own)[] samples)
    {
        var h = new OwnershipHistory();
        foreach ((long turn, int[] own) in samples)
        {
            int[] sorted = (int[])own.Clone();
            Array.Sort(sorted);
            h._turns.Add(turn);
            h._own.Add(sorted);
        }
        return h;
    }
}
