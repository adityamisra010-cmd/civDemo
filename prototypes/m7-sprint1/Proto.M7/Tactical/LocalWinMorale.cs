namespace Proto.M7.Tactical;

/// <summary>[MOR] One "fight it won" morale rise (inspection and tests): at <paramref name="AtMs"/> formation
/// <paramref name="FormationId"/> gained <paramref name="Rise"/> because enemy formation <paramref name="BrokenFormationId"/>, which it
/// had dealt damage to within the last pulse length, broke. Several rows of one formation at one instant add up (capped once).</summary>
public readonly record struct LocalWinRise(long AtMs, long FormationId, long BrokenFormationId, long Rise);

/// <summary>
/// [MOR] The in-battle "fight it won" rule (docs/m7-plan/morale-proposal.md §3.2; <see cref="TacticalRules.LocalWinMoraleRise"/>).
/// ONE implementation that both resolvers call at the same point of their resolution (after every casualty and rout of an instant
/// or step), so the rule is the same in both by construction (D-079 D11: a one-sided term opens a parity gap). Integer only.
/// A coefficient inside the morale equation (Law 2): the rise is a change of the formation's morale stock at the instant its enemy
/// breaks, which the casualty-driven loss and the recovery then work on as before; nothing standing.
/// </summary>
public static class LocalWinMorale
{
    /// <summary>The rise of a winner of <paramref name="startWinner"/> entities over a broken formation of <paramref name="startBroken"/>
    /// entities: floor(coefficient × startBroken ÷ (startBroken + startWinner)). The larger the beaten formation against one's own,
    /// the larger the lift; 0 at coefficient 0.</summary>
    public static long Rise(long coefficient, long startBroken, long startWinner)
    {
        if (coefficient < 0 || startBroken < 0 || startWinner < 0) throw new ArgumentOutOfRangeException(nameof(coefficient));
        if (coefficient == 0 || startBroken == 0) return 0;
        return (long)((Int128)coefficient * startBroken / ((Int128)startBroken + startWinner));
    }

    /// <summary>The winner fought the broken formation: it dealt it damage at most <paramref name="window"/> before
    /// <paramref name="now"/> (inclusive), in the caller's time unit (ms in the tactical engine, steps in the AutoResolver).
    /// <paramref name="lastHit"/> = long.MinValue: never.</summary>
    public static bool Fought(long lastHit, long now, long window)
        => lastHit != long.MinValue && lastHit <= now && now - lastHit <= window;

    /// <summary>
    /// Applies the rule at one instant. Formation i (index order of the caller) BROKE now iff it was in the fight before this
    /// instant's casualties (<paramref name="inFightBefore"/>) and is not now (<paramref name="inFightNow"/>: routed, or no one left
    /// to fight). Each formation w still in the fight, of the other side, that <see cref="Fought"/> a broken formation g gains
    /// <see cref="Rise"/>(coefficient, start g, start w); the gains of one winner add up and its morale is capped at
    /// <paramref name="moraleMax"/> once. A formation that broke at the same instant gains nothing. The result does not depend on
    /// the index order (a sum and one cap per winner); rows are logged broken-major, winner-minor, in index order.
    /// <paramref name="lastHit"/>[w × n + g] = when w last dealt damage to g. Returns true when some morale changed.
    /// </summary>
    public static bool Apply(long coefficient, long moraleMax, long window, long now, long atMs,
                             ReadOnlySpan<long> ids, ReadOnlySpan<int> sides, ReadOnlySpan<long> starts,
                             ReadOnlySpan<bool> inFightBefore, ReadOnlySpan<bool> inFightNow, ReadOnlySpan<long> lastHit,
                             Span<long> morale, List<LocalWinRise> log)
    {
        int n = ids.Length;
        if (sides.Length != n || starts.Length != n || inFightBefore.Length != n || inFightNow.Length != n || morale.Length != n
            || lastHit.Length != checked(n * n) || moraleMax <= 0)
            throw new ArgumentException("one entry per formation, an n × n hit table and a positive scale");
        if (coefficient == 0) return false;
        Span<long> gain = n <= 64 ? stackalloc long[n] : new long[n];
        gain.Clear();
        bool any = false;
        for (int g = 0; g < n; g++)
        {
            if (!inFightBefore[g] || inFightNow[g]) continue;
            for (int w = 0; w < n; w++)
            {
                if (!inFightNow[w] || sides[w] == sides[g] || !Fought(lastHit[w * n + g], now, window)) continue;
                long rise = Rise(coefficient, starts[g], starts[w]);
                if (rise == 0) continue;
                gain[w] = checked(gain[w] + rise);
                log.Add(new LocalWinRise(atMs, ids[w], ids[g], rise));
                any = true;
            }
        }
        if (!any) return false;
        for (int w = 0; w < n; w++)
            if (gain[w] > 0) morale[w] = Math.Min(moraleMax, checked(morale[w] + gain[w]));
        return true;
    }
}
