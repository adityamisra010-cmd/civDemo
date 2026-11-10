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

    public static MoraleRates Proposal() => new();

    public void Validate()
    {
        if (VictoryRiseMaxPpm < 0 || VictoryRiseMaxPpm > CampaignMorale.ScalePpm) throw new ArgumentOutOfRangeException(nameof(VictoryRiseMaxPpm));
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
/// </summary>
public static class CampaignMorale
{
    public const long ScalePpm = 1_000_000;

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

    /// <summary>The campaign morale after the battle: one clamp to the scale over the sum of both terms.</summary>
    public static long After(long beforePpm, long decreasePpm, long risePpm)
    {
        if (beforePpm < 0 || beforePpm > ScalePpm || decreasePpm < 0 || risePpm < 0) throw new ArgumentOutOfRangeException(nameof(beforePpm));
        return Math.Clamp(beforePpm - decreasePpm + risePpm, 0, ScalePpm);
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
/// <item>BothEliminated, BothDisengaged, Standoff: today's resolvers separate neither side → no winner is reported (−1). DA6 gives
///   such a battle to the defender and DA7 an ambush to the ambusher (DA7-R (a)); lane AP implements both in the AutoResolver (and the
///   Standoff extension is a PROPOSAL, DA6-R). Until a resolver reports that winner, no rise is given; once it does, the hand-off
///   passes it and the rule is unchanged.</item>
/// </list>
/// </summary>
public static class BattleWinner
{
    public static int Reported(BattleResult r) => r.Reason switch
    {
        BattleEndReason.SideEliminated or BattleEndReason.SideDisengaged when r.Side is 0 or 1 => 1 - r.Side,
        _ => -1,
    };
}
