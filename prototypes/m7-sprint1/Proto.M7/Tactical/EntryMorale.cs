namespace Proto.M7.Tactical;

/// <summary>[MOR2] One in-battle entry charge (inspection and tests): at <paramref name="AtMs"/>, the instant formation
/// <paramref name="FormationId"/> was placed on the battlefield, its morale fell from <paramref name="Before"/> by
/// <paramref name="Charged"/> (the entry cost, or all of the formation's morale when it had less). While
/// <see cref="TacticalRules.EntryMoraleCost"/> is above 0, exactly one row per formation that entered the battle; none at 0.</summary>
public readonly record struct EntryCharge(long AtMs, long FormationId, long Before, long Charged);

/// <summary>
/// [MOR2] D-083 DB1 ("Entering a battle costs morale, with or without casualties"), the in-battle part (docs/m7-plan/morale-proposal.md
/// §11; <see cref="TacticalRules.EntryMoraleCost"/>). ONE implementation that both resolvers call, so the rule is the same in both by
/// construction (D-079 D11: a one-sided term opens a parity gap).
/// <list type="bullet">
/// <item>READING DB1-R1 (a), applied: a formation enters a battle when it is placed on the battlefield — at the battle's start in its
///   deployment third, or later at the instant it arrives (in the prototype the only later arrival is lane N's landing of a land
///   formation that starts aboard a transport: the instant its first entity is put ashore). A formation that never reaches the field
///   pays nothing.</item>
/// <item>READING DB1-R2 (a), applied: once per formation per battle, at its entry instant. The first call for a formation records its
///   entry instant; every later call (a formation that left the field and is placed on it again) is a no-op.</item>
/// <item>Law 2: an event change of the formation's morale stock at the instant it enters, which the casualty-driven loss and the
///   recovery then work on as before; nothing standing. Law 3: an event, so no rate. Law 5: integer, clamped at 0.</item>
/// </list>
/// The campaign part (the army's campaign morale pays the cost exactly, whatever the battle does) is the settlement term of
/// <see cref="Proto.M7.Morale.CampaignMorale.EntryPpm"/>; the battle reports each formation's entry instant and what it charged
/// (<see cref="FormationResult.EnteredAtMs"/>, <see cref="FormationResult.EntryCharged"/>) so that the settlement measures DR29's
/// net loss from the morale after the entry charge and counts nothing twice.
/// </summary>
public static class EntryMorale
{
    /// <summary>What the entry takes from a formation of morale <paramref name="morale"/>: the cost, or all of its morale when it has
    /// less (the clamp at 0). 0 at cost 0.</summary>
    public static long Charge(long cost, long morale)
    {
        if (cost < 0 || morale < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        return Math.Min(cost, morale);
    }

    /// <summary>
    /// The formation is placed on the field at <paramref name="atMs"/>. The first call for a formation records the entry instant in
    /// <paramref name="enteredAtMs"/>; when the cost is above 0 it also takes <see cref="Charge"/> from <paramref name="morale"/>,
    /// records it in <paramref name="charged"/> and logs one row (at cost 0 nothing else happens: the exact identity). A later call is
    /// a no-op (once per formation per battle). Returns true when this call was the formation's entry.
    /// </summary>
    public static bool Enter(long cost, long atMs, long formationId, ref long? enteredAtMs, ref long morale, ref long charged, List<EntryCharge> log)
    {
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        if (enteredAtMs is not null) return false;
        enteredAtMs = atMs;
        if (cost == 0) return true;
        long c = Charge(cost, morale);
        log.Add(new EntryCharge(atMs, formationId, morale, c));
        morale -= c;
        charged = c;
        return true;
    }
}
