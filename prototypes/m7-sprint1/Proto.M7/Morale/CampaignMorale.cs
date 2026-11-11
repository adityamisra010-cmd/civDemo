using Proto.M7.Tactical;

namespace Proto.M7.Morale;

/// <summary>
/// [MOR] Coefficients of the army (campaign) morale layer that lane MOR adds. Every value is a PROPOSAL (D-082 §5: "the size of
/// the victory rise, decay and recovery ... Delegated (DA2); a PROPOSAL in its lane"); docs/m7-plan/morale-proposal.md §4.
/// The recovery toward the baseline, the decay above it and the hunger and arrears penalties are lane E's per-sim-year rates
/// (<see cref="Proto.M7.WarEcon.LogisticsRates"/>), integrated over the exact pulse length at every pulse end.
/// </summary>
public sealed record MoraleRates
{
    /// <summary>The DA1 rise of the winning side's army, micro units of the campaign scale (0..1,000,000), in the limit of a beaten
    /// force infinitely larger than the winner's own side: a win over B persons by a side of W persons raises the army's campaign
    /// morale by floor(VictoryRiseMaxPpm × B ÷ (B + W)), half of it for an equal force. PROPOSAL (morale proposal §4.2).</summary>
    public long VictoryRiseMaxPpm { get; init; } = 400_000;

    /// <summary>[MOR2] D-083 DB1 ("Entering a battle costs morale, with or without casualties"): what an army's campaign morale pays
    /// for each battle its formation entered (DB1-R1 (a): placed on the field; DB1-R2 (a): once per battle), micro units, as its own
    /// settlement term beside DR29's decrease and DA1's rise (DB1-R3 (a)). PROPOSAL (morale proposal §11.4): 50,000 = what a fed, paid,
    /// loyal army recovers in 30 days at lane E's 600 ‰ per sim-year; an eighth of the largest rise, a quarter of the rise for beating
    /// an equal force, so a lossless win over a force smaller than a seventh of the winning side nets a loss. On the battle scale
    /// (MoraleMax 1,000) it is 50, the in-battle part's PROPOSAL value (TacticalRules.EntryMoraleCost, 0 by default).</summary>
    public long EntryCostPpm { get; init; } = 50_000;

    public static MoraleRates Proposal() => new();

    public void Validate()
    {
        if (VictoryRiseMaxPpm < 0 || VictoryRiseMaxPpm > CampaignMorale.ScalePpm) throw new ArgumentOutOfRangeException(nameof(VictoryRiseMaxPpm));
        if (EntryCostPpm < 0 || EntryCostPpm > CampaignMorale.ScalePpm) throw new ArgumentOutOfRangeException(nameof(EntryCostPpm));   // [MOR2]
    }
}

/// <summary>
/// [MOR] The army's campaign morale after a battle (D-079 DR29 with D-082 DA1; docs/m7-plan/morale-proposal.md §4). Pure integer
/// functions; the campaign scale is micro units 0..1,000,000 (lane E's stock), the battle scale is 0..MoraleMax (the resolvers').
/// <code>
///   decrease = floor(max(0, start − end − restore) × 1,000,000 ÷ MoraleMax)      DR29, every participant (lane D's net loss)
///   rise     = won ? floor(VictoryRiseMaxPpm × B ÷ (B + W)) : 0                 DA1, the winning side's army only
///   after    = clamp(before − decrease + rise, 0, 1,000,000)                     one clamp: the two terms are not ordered
/// </code>
/// B = the persons of the beaten side and W = the persons of the winning side at the battle start (the M7.15 hand-off supplies
/// both; the stand-in passes them). The loser takes no extra defeat penalty (PROPOSAL (a), proposal §4.3): its defeat is already in
/// its own battle morale, which a rout leaves below the rout threshold, and in lane W's desertion of its routed men.
/// The rise is a change of the stock, never a standing modifier (Law 2): lane E's pulse-end dynamics then move the stock toward
/// its baseline at per-sim-year rates over the exact pulse length (Law 3) — the recovery from below, the decay of an excess above.
/// [MOR2, D-083 DB1] A third term, the ENTRY cost, for every army whose formation entered the battle (DB1-R1 (a)), once per battle
/// (DB1-R2 (a)), beside the other two (DB1-R3 (a)); DR29's decrease is then measured from the start less what the battle itself
/// charged at the entry (TacticalRules.EntryMoraleCost; 0 by default), so the cost is counted once and reaches the campaign exactly
/// whatever the in-battle recovery did (morale proposal §11.3):
/// <code>
///   entry    = entered ? EntryCostPpm : 0                                       DB1, every army that took part
///   decrease = floor(max(0, (start − entryInBattle) − end − restore) × 1,000,000 ÷ MoraleMax)
///   rise     = won ∧ entered ? floor(VictoryRiseMaxPpm × B ÷ (B + W)) : 0        DA1 (DA1-R (a): the formations that took part)
///   after    = clamp(before − entry − decrease + rise, 0, 1,000,000)            one clamp over the three terms
/// </code>
/// [MOR2, verification round 1, MOR2-R1-01, MOR2-R1-04] "won ∧ entered" applies READING MOR2-R1 (a) ("took part" in DA1-R (a) =
/// entered the field; alternative (b): entered, or engaged without reaching it). The decrease applies whether or not the formation
/// entered: a formation that never reached the field can still lose men and morale (troops killed aboard their transport).
/// [MOR3, D-085 DD1, 2026-11-11] "A battle without fighting does not affect morale at all": every one of the three terms applies only
/// where the battle had fighting (DD1-R1 (a): fighting anywhere in the battle — the formations of an army need not have struck a blow;
/// DD1-R2 (a): at least one attack resolved against an enemy formation, the battle report's FirstFightMs), so (morale proposal §12.3)
/// <code>
///   entry    = fought ∧ entered ? EntryCostPpm : 0
///   decrease = fought ? floor(max(0, (start − entryInBattle) − end − restore) × 1,000,000 ÷ MoraleMax) : 0
///   rise     = fought ∧ won ∧ entered ? floor(VictoryRiseMaxPpm × B ÷ (B + W)) : 0
///   after    = clamp(before − entry − decrease + rise, 0, 1,000,000)            = before when the battle had no fighting
/// </code>
/// (<see cref="IfFought"/>). The reported winner is unchanged and still recorded; it gains nothing from a battle without fighting.
/// </summary>
public static class CampaignMorale
{
    public const long ScalePpm = 1_000_000;

    /// <summary>[MOR3] D-085 DD1: a settlement term of a battle that had fighting, or 0 for a battle without fighting (no entry cost, no
    /// DR29 decrease, no DA1 rise: "No fighting doesn't affect morale at all"). The one place the settlement applies DD1.</summary>
    public static long IfFought(bool fought, long termPpm)
    {
        if (termPpm < 0) throw new ArgumentOutOfRangeException(nameof(termPpm));
        return fought ? termPpm : 0;
    }

    /// <summary>[MOR2] D-083 DB1: the entry term of an army's settlement — the campaign cost for a battle its formation entered
    /// (placed on the field, DB1-R1 (a)), 0 for one it never entered. Whatever the battle did: it does not depend on the casualties,
    /// the end morale or the in-battle recovery (D-083 §4: the cost must reach the army's campaign morale exactly).</summary>
    public static long EntryPpm(bool entered, MoraleRates r) => entered ? r.EntryCostPpm : 0;

    /// <summary>[MOR2] The battle-scale image of a campaign entry cost, floor(costPpm × MoraleMax ÷ 1,000,000) ~~: the value the M7.15
    /// hand-off would set as TacticalRules.EntryMoraleCost when the in-battle part is switched on~~ (50 for 50,000 at MoraleMax 1,000).
    /// [MOR2, verification round 1, MOR2-R1-11] It derives the value the in-battle part's switch-on writes into TacticalRules'
    /// DEFAULT EntryMoraleCost (one value for the campaign's battles and the War Sandbox, DR30); the hand-off does not set it per battle
    /// (it may check that a battle's rules carry this image of the campaign's EntryCostPpm). Morale proposal §11.7.</summary>
    public static long EntryBattleScale(long costPpm, long moraleMax)
    {
        if (costPpm < 0 || costPpm > ScalePpm || moraleMax <= 0) throw new ArgumentOutOfRangeException(nameof(costPpm));
        return (long)((Int128)costPpm * moraleMax / ScalePpm);
    }

    /// <summary>DR29: the battle's net morale loss on the battle scale (start − end), less the medical restore, never below zero,
    /// mapped to micro units (floor). Lane D's rule (verification round 2, D-R2-01), unchanged: the in-battle recovery can lift a
    /// formation above its start, and that gain is not carried.</summary>
    public static long CombatDecreasePpm(long start, long end, long restore, long moraleMax)
    {
        if (moraleMax <= 0 || start < 0 || start > moraleMax || end < 0 || end > moraleMax || restore < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        long net = Math.Max(0, start - end - restore);
        return (long)((Int128)net * ScalePpm / moraleMax);
    }

    /// <summary>DA1: the rise of an army whose side won (0 for a side that did not, and 0 for a "win" over nobody): the share
    /// B ÷ (B + W) of <see cref="MoraleRates.VictoryRiseMaxPpm"/>, floor. The larger the force beaten against one's own, the larger the
    /// lift (a win over an equal force gives half of the maximum, over one twice as large two thirds).</summary>
    public static long VictoryRisePpm(bool won, long beatenPersons, long winnerSidePersons, MoraleRates r)
    {
        if (beatenPersons < 0 || winnerSidePersons < 0) throw new ArgumentOutOfRangeException(nameof(beatenPersons));
        if (!won || beatenPersons == 0) return 0;
        return (long)((Int128)r.VictoryRiseMaxPpm * beatenPersons / ((Int128)beatenPersons + winnerSidePersons));
    }

    /// <summary>The campaign morale after the battle: one clamp to the scale over the sum of the terms. [MOR2] With D-083 DB1's entry
    /// term <paramref name="entryPpm"/> (0 = lane MOR's two-term settlement exactly): clamp(before − entry − decrease + rise, 0, 1,000,000),
    /// so the three terms have no order.</summary>
    public static long After(long beforePpm, long decreasePpm, long risePpm, long entryPpm = 0)
    {
        if (beforePpm < 0 || beforePpm > ScalePpm || decreasePpm < 0 || risePpm < 0 || entryPpm < 0) throw new ArgumentOutOfRangeException(nameof(beforePpm));
        return Math.Clamp(beforePpm - entryPpm - decreasePpm + risePpm, 0, ScalePpm);
    }

    /// <summary>The campaign → battle hand-off (M7.15; proposal §5): a formation enters a battle at its army's campaign morale on the
    /// battle scale, floor(campaign × MoraleMax ÷ 1,000,000). Nothing else is added to the start morale (no standing modifier).</summary>
    public static long BattleStartMorale(long campaignPpm, long moraleMax)
    {
        if (campaignPpm < 0 || campaignPpm > ScalePpm || moraleMax <= 0) throw new ArgumentOutOfRangeException(nameof(campaignPpm));
        return (long)((Int128)campaignPpm * moraleMax / ScalePpm);
    }
}

/// <summary>
/// [MOR] Who won a battle, as the battle report names it (D-082 DA1-R (a): "a win is the battle outcome the resolvers already
/// report (the other side routed, retreated, surrendered or eliminated; DA6 and DA7 for equal sides)"). The morale layer reads the
/// reported winner and decides nothing itself.
/// <list type="bullet">
/// <item>SideEliminated: the other side has no Active entity left (its men are dead, surrendered, routed or withdrawn) → the side
///   that still has some wins.</item>
/// <item>SideDisengaged (the legacy inactivity rule): the other side withdrew → the side that stayed wins.</item>
/// <item>BothEliminated, BothDisengaged (and a Standoff only with the DA6-R extension TacticalRules.EqualSidesAtStandoff): the
///   result separates neither side. [AP] Both resolvers now report DA6's and DA7's winner on the result itself
///   (<see cref="BattleResult.Winner"/> with <see cref="BattleResult.WinBasis"/> DefenderOnEqualSides or AmbusherOnEqualSides,
///   <see cref="EqualSides"/>): the designated defender, or in an ambush launched from concealment the ambusher (DA7-R (a)); with
///   no designated role (a meeting engagement), no winner (−1), as before. The DA1 rule is unchanged: it gives the rise to whatever
///   side this function names.</item>
/// </list>
/// </summary>
public static class BattleWinner
{
    public static int Reported(BattleResult r)
    {
        // [AP] D-082 DA6 / DA7, decided by the resolver at the battle end (EqualSides): the equal-sides winner the result carries.
        if ((r.WinBasis is WinBasis.DefenderOnEqualSides or WinBasis.AmbusherOnEqualSides) && (r.Winner is 0 or 1)) return r.Winner;
        return r.Reason switch
        {
            BattleEndReason.SideEliminated or BattleEndReason.SideDisengaged when r.Side is 0 or 1 => 1 - r.Side,
            _ => -1,
        };
    }
}

/// <summary>
/// [MOR3] D-085 DD1 ("A battle without fighting does not affect morale at all"): whether the battle → campaign settlement of the army
/// of one formation treats the battle as FOUGHT — the input <see cref="Proto.M7.Support.SupportBattle.Fought"/> of the hand-off, read
/// from the battle report as <see cref="BattleWinner.Reported"/> reads the winner (docs/m7-plan/morale-proposal.md §12).
/// <list type="bullet">
/// <item>READING DD1-R1 (a), APPLIED: fighting is read per BATTLE. If any attack was resolved against an enemy formation anywhere in
///   the battle (<see cref="BattleResult.FirstFightMs"/> is set, DD1-R2 (a)), every army whose formation entered settles as before —
///   including an army whose formation itself struck no blow and received none (D-083 DB1: "Simply entering battke should cost
///   morale"). Alternative (b), not applied: per formation (one that neither struck nor received a blow settles as unfought).</item>
/// <item>READING DD1-R2 (a), APPLIED: an attack counts whether or not it dealt damage (a missed shot is fighting); the resolvers record
///   the first one. Alternative (b), not applied: only an attack that dealt damage.</item>
/// </list>
/// The formation must be one of the battle's (else ArgumentException): the answer is the battle's, whichever formation asks.
/// </summary>
public static class BattleFighting
{
    public static bool SettlesAsFought(BattleResult r, long formationId)
    {
        bool found = false;
        foreach (var f in r.Formations) if (f.FormationId == formationId) { found = true; break; }
        if (!found) throw new ArgumentException("the formation is not one of the battle's", nameof(formationId));
        return r.FirstFightMs is not null;                     // DD1-R1 (a): the battle, not the formation
    }
}
