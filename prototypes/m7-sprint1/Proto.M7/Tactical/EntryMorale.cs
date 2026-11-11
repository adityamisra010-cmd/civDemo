namespace Proto.M7.Tactical;

/// <summary>[MOR2] One in-battle entry charge (inspection and tests): at <paramref name="AtMs"/> formation
/// <paramref name="FormationId"/>'s morale fell from <paramref name="Before"/> by <paramref name="Charged"/> (the entry cost, or all of
/// the formation's morale when it had less). ~~At the instant the formation was placed on the battlefield.~~ [MOR3, D-085 DD1,
/// 2026-11-11] AtMs is the instant the charge FELL: the battle's first fighting instant for a formation that entered before it, or the
/// formation's own entry instant for one that enters after it (<see cref="EntryMorale"/>). While
/// <see cref="TacticalRules.EntryMoraleCost"/> is above 0, exactly one row per formation that entered a battle that had fighting; none
/// at 0, and none in a battle without fighting.</summary>
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
/// <item>READING DB1-R2 (a), applied: once per formation per battle~~, at its entry instant~~. The first call for a formation records its
///   entry instant; every later call (a formation that left the field and is placed on it again) is a no-op.</item>
/// <item>Law 2: an event change of the formation's morale stock ~~at the instant it enters~~, which the casualty-driven loss and the
///   recovery then work on as before; nothing standing. Law 3: an event, so no rate. Law 5: integer, clamped at 0.</item>
/// </list>
/// [MOR3, D-085 DD1, 2026-11-11] "A battle without fighting does not affect morale at all" (DD1), and D-085 §4: "If it is switched on,
/// it must not change morale in a battle that turns out to have no fighting." The charge is therefore no longer taken at the entry
/// instant (struck above): a formation's entry is RECORDED when it is placed on the field (<see cref="Enter"/>), and the charge FALLS
/// (PROPOSAL C1, morale proposal §12.4):
/// <list type="bullet">
/// <item>at the battle's first fighting instant (<see cref="BattleResult.FirstFightMs"/>: the first attack resolved against an enemy
///   formation, DD1-R2 (a)) for every formation that has entered by then, wherever it is at that instant
///   (<see cref="ChargeOnFighting"/>, called by each resolver at the start of that instant's resolution, after the instant's attacks and
///   before their casualties, morale losses and routs);</item>
/// <item>at its own entry instant for a formation that enters after the first fighting instant (<see cref="Enter"/> with
///   <c>fightingBegun</c> true);</item>
/// <item>never in a battle without fighting: no attack, no charge, so the battle runs exactly as with the part off.</item>
/// </list>
/// Still once per formation per battle, clamped at 0; at cost 0 nothing is charged, logged or hashed (the exact identity).
/// The campaign part (the army's campaign morale pays the cost exactly, whatever the battle does) is the settlement term of
/// <see cref="Proto.M7.Morale.CampaignMorale.EntryPpm"/> [MOR3: in a battle that had fighting only, DD1]; the battle reports each
/// formation's entry instant and what it charged (<see cref="FormationResult.EnteredAtMs"/>, <see cref="FormationResult.EntryCharged"/>)
/// so that the settlement measures DR29's net loss from the morale after the entry charge and counts nothing twice.
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
    /// <paramref name="enteredAtMs"/> (a later call is a no-op: once per formation per battle). [MOR3, D-085 DD1] The charge falls now
    /// only when the battle's fighting has already begun (<paramref name="fightingBegun"/>: a formation entering after the first fighting
    /// instant); otherwise it waits for that instant (<see cref="ChargeOnFighting"/>) and never falls if the battle has no fighting.
    /// Returns true when this call was the formation's entry.
    /// </summary>
    public static bool Enter(long cost, long atMs, long formationId, bool fightingBegun, ref long? enteredAtMs, ref long morale, ref long charged,
                             ref long? chargedAtMs, List<EntryCharge> log)
    {
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        if (enteredAtMs is not null) return false;
        enteredAtMs = atMs;
        if (fightingBegun) ChargeOnFighting(cost, atMs, formationId, enteredAtMs, ref morale, ref charged, ref chargedAtMs, log);
        return true;
    }

    /// <summary>
    /// [MOR3, D-085 DD1] The battle has fighting at <paramref name="atMs"/>: a formation that has entered and has not been charged pays
    /// the entry cost now — <see cref="Charge"/> taken from <paramref name="morale"/>, recorded in <paramref name="charged"/> and
    /// <paramref name="chargedAtMs"/>, one row logged. A formation that never entered, or was charged already, is untouched; at cost 0
    /// nothing happens at all (no row, no field set: the exact identity). Returns true when it charged.
    /// </summary>
    public static bool ChargeOnFighting(long cost, long atMs, long formationId, long? enteredAtMs, ref long morale, ref long charged,
                                        ref long? chargedAtMs, List<EntryCharge> log)
    {
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        if (cost == 0 || enteredAtMs is null || chargedAtMs is not null) return false;
        long c = Charge(cost, morale);
        log.Add(new EntryCharge(atMs, formationId, morale, c));
        morale -= c;
        charged = c;
        chargedAtMs = atMs;
        return true;
    }
}
