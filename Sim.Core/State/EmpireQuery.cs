namespace Sim.Core.State;

/// <summary>
/// M4 (D-042): pure read-only queries over the Empire foundation tables. Every
/// question an Empire answers structurally — how many settlements it commands,
/// whether it still exists, where its capital is, and whether a human or the AI
/// issues its orders — is DERIVED here rather than stored, so no denormalised
/// membership list can drift from <see cref="ControlRow"/>, the single source of
/// truth for what an Empire actually holds.
///
/// Iteration is over table indices in insertion order (never a dictionary or a
/// set), and there is no LINQ — Law 5. These are readers: nothing here mutates
/// state, and no system calls them yet.
/// </summary>
public static class EmpireQuery
{
    /// <summary>
    /// How many settlements obey <paramref name="polity"/>'s orders. Membership is
    /// the control relation, so an Empire that loses a settlement loses it here
    /// with no separate roster to update.
    /// </summary>
    public static int ControlledCount(IReadOnlyWorldState world, PolityId polity)
    {
        int count = 0;
        for (int i = 0; i < world.Controls.Count; i++)
        {
            if (world.Controls[i].Polity.Value == polity.Value)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// An Empire is extinct when it controls no settlement. Extinction is a
    /// DERIVED emptiness, not a stored flag — there is nothing to forget to set,
    /// and a polity that regains control is no longer extinct by the same rule.
    /// A row may remain in <see cref="IReadOnlyWorldState.Polities"/> for an
    /// extinct polity: the identity persists, the holdings do not.
    /// </summary>
    public static bool IsExtinct(IReadOnlyWorldState world, PolityId polity)
        => ControlledCount(world, polity) == 0;

    /// <summary>
    /// The capital of <paramref name="polity"/>, if it has one. Absence of a
    /// <see cref="CapitalRow"/> is a capital-less Empire — a representable state,
    /// not an error and not a sentinel id — so capital loss can be recorded
    /// without touching the polity's identity or its surviving holdings.
    /// </summary>
    public static bool TryGetCapital(IReadOnlyWorldState world, PolityId polity, out SettlementId place)
    {
        for (int i = 0; i < world.Capitals.Count; i++)
        {
            CapitalRow row = world.Capitals[i];
            if (row.Polity.Value == polity.Value)
            {
                place = row.Place;
                return true;
            }
        }

        place = default;
        return false;
    }

    /// <summary>
    /// Whether a human director or the AI commands <paramref name="polity"/>.
    /// False when no <see cref="PolityRow"/> exists for the id — an unregistered
    /// polity has no command source rather than a defaulted one.
    /// </summary>
    public static bool TryGetCommandSource(IReadOnlyWorldState world, PolityId polity, out CommandSource source)
    {
        for (int i = 0; i < world.Polities.Count; i++)
        {
            PolityRow row = world.Polities[i];
            if (row.Id.Value == polity.Value)
            {
                source = row.Source;
                return true;
            }
        }

        source = default;
        return false;
    }

    /// <summary>
    /// True when <paramref name="polity"/> is registered and player-commanded.
    /// The command source is a property of the Empire ROW, never of a settlement
    /// or of any simulation quantity, so removing the player changes who issues
    /// orders and nothing else about the world.
    /// </summary>
    public static bool IsPlayerCommanded(IReadOnlyWorldState world, PolityId polity)
        => TryGetCommandSource(world, polity, out CommandSource source) && source == CommandSource.Player;
}
