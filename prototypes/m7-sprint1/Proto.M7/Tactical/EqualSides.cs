namespace Proto.M7.Tactical;

/// <summary>[AP] How a battle's reported winner was decided (stable codes; part of the result hash when DA6 or DA7 decided).</summary>
public enum WinBasis : byte
{
    /// <summary>No winner is reported: the result separates neither side and no designated role takes it (or the end is a
    /// Standoff with the DA6-R extension OFF).</summary>
    None = 0,
    /// <summary>The result separates the sides: one side was eliminated (SideEliminated) or withdrew (SideDisengaged); the other
    /// side wins. Lane MOR's reading of the reported winner (DA1-R (a)).</summary>
    Decisive = 1,
    /// <summary>D-082 DA6: the result separates neither side and the battle has a designated defender, which wins.</summary>
    DefenderOnEqualSides = 2,
    /// <summary>D-082 DA7 with DA7-R (a): the result separates neither side and the battle was opened as an ambush from
    /// concealment; the ambushing side wins.</summary>
    AmbusherOnEqualSides = 3,
}

/// <summary>
/// [AP] D-082 DA6 ("If two sides are equal, the defending side wins.") and DA7 ("Unless it's an ambush like from a hidden unit,
/// then the winning side wins.", read as DA7-R (a): the ambusher wins), as ONE pure function that both resolvers call at their
/// battle end, so the rule is identical in both by construction (D-079 §4 D11: a one-sided addition opens a parity gap).
/// docs/m7-plan/concealment-actions-proposal.md §6.
///
/// "EQUAL" (DA6-R, delegated design; this lane's definition): the deciding quantity of both resolvers is the INSTANT at which a
/// side has no fighting entity left on the field (E_s: the step in the AutoResolver, the event instant in the TacticalResolver;
/// infinite while a side still fights). The battle ends at min(E_0, E_1). The result separates the sides iff E_0 != E_1, so
/// "equal" is the exact integer equality E_0 = E_1:
///   both finite and equal  -> BattleEndReason.BothEliminated (both sides' last fighting entities went in the same step/instant),
///                             or BothDisengaged (the legacy rule withdrew both at the same pulse end);
///   both infinite          -> BattleEndReason.Standoff (lane TR's UNRATIFIED whole-field idle end, P-TR1): equal only when the
///                             DA6-R extension (TacticalRules.EqualSidesAtStandoff, a PROPOSAL, default OFF) is on.
/// No band is used: a band on a secondary quantity (casualties, survivors) would override results the resolvers DID separate
/// and add an unratified victory criterion; exact equality changes only results that today name no winner.
///
/// PRECEDENCE: a decided result is never changed; on equal sides an ambush (the setup's Ambusher) comes before the defender
/// (DA7 is the exception to DA6); with neither designated, no winner is reported.
/// </summary>
public static class EqualSides
{
    /// <summary>True iff the battle end separates neither side (see the type summary). Standoff counts only with the extension.</summary>
    public static bool SeparatesNeither(BattleEndReason reason, bool standoffExtension)
        => reason is BattleEndReason.BothEliminated or BattleEndReason.BothDisengaged
           || (standoffExtension && reason == BattleEndReason.Standoff);

    /// <summary>The reported winner and its basis. <paramref name="side"/> is the result's eliminated / disengaged side;
    /// <paramref name="defender"/> and <paramref name="ambusher"/> are the setup's designated roles (-1 = none).</summary>
    public static (int Winner, WinBasis Basis) Decide(BattleEndReason reason, int side, int defender, int ambusher, bool standoffExtension)
    {
        if (defender < -1 || defender > 1) throw new ArgumentOutOfRangeException(nameof(defender), "defender must be -1, 0 or 1");
        if (ambusher < -1 || ambusher > 1) throw new ArgumentOutOfRangeException(nameof(ambusher), "ambusher must be -1, 0 or 1");
        if ((reason == BattleEndReason.SideEliminated || reason == BattleEndReason.SideDisengaged) && (side == 0 || side == 1))
            return (1 - side, WinBasis.Decisive);
        if (!SeparatesNeither(reason, standoffExtension)) return (-1, WinBasis.None);
        if (ambusher >= 0) return (ambusher, WinBasis.AmbusherOnEqualSides);
        if (defender >= 0) return (defender, WinBasis.DefenderOnEqualSides);
        return (-1, WinBasis.None);
    }

    /// <summary>The setup's roles are -1, 0 or 1 (refused at battle construction otherwise).</summary>
    public static void ValidateRoles(BattleSetup setup)
    {
        if (setup.Defender < -1 || setup.Defender > 1) throw new ArgumentOutOfRangeException(nameof(setup), "BattleSetup.Defender must be -1, 0 or 1");
        if (setup.Ambusher < -1 || setup.Ambusher > 1) throw new ArgumentOutOfRangeException(nameof(setup), "BattleSetup.Ambusher must be -1, 0 or 1");
    }
}
