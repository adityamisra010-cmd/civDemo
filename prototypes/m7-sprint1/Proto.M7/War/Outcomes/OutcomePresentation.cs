using Proto.M7.Tactical;
using Proto.M7.War.Siege;

namespace Proto.M7.War.Outcomes.Presentation;

/// <summary>
/// The execution and death content setting (D-079 Q12 OPEN: its DEFAULT is the Director's). PRESENTATION ONLY: the simulation
/// never reads it (WarOutcomeWorld takes no such input; a source test pins that), so it cannot change a fate, a record or a hash.
/// There is deliberately no default: 0 is <see cref="Unset"/>, and the selector refuses it, so whoever ships the cinematics must
/// choose (a) Show (the setting cuts away on request) or (b) CutAway (the setting shows on request).
/// </summary>
public enum ExecutionContent : byte
{
    Unset = 0,
    Show = 1,
    CutAway = 2,
}

/// <summary>What a cinematic may depict. Stable codes.</summary>
public enum CinematicCue : byte
{
    None = 0,
    ExecutionShown = 1,
    /// <summary>The fate is Executed and the content setting cuts away: the cinematic cuts away, the fate stays Executed.</summary>
    ExecutionCutAway = 2,
    LeaderTakenCaptive = 3,
    LeaderFreed = 4,
    SettlementOccupied = 5,
    SettlementSacked = 6,
    SettlementRazed = 7,
    Surrender = 8,
    Stormed = 9,
    SiegeRaised = 10,
    Victory = 11,
    Defeat = 12,
    Inconclusive = 13,
    Peace = 14,
}

/// <summary>
/// The outcome → cinematic selector (C35): a PURE function of the authoritative records and the content setting. It never shows an
/// execution unless the record says Executed, never a victory for an inconclusive end, and keeps the existing distinctions (surrender,
/// execution, peace, defeat, inconclusive, siege). Naval outcomes reach it as field battles (their kind is not distinguished here).
/// </summary>
public static class CinematicSelector
{
    public static CinematicCue ForLeaderFate(LeaderFateRow fate, ExecutionContent setting)
    {
        if (setting == ExecutionContent.Unset) throw new InvalidOperationException("the execution content setting has no default (D-079 Q12 is open): choose one");
        return (LeaderFate)fate.To switch
        {
            LeaderFate.Executed => setting == ExecutionContent.Show ? CinematicCue.ExecutionShown : CinematicCue.ExecutionCutAway,
            LeaderFate.Captive => CinematicCue.LeaderTakenCaptive,
            LeaderFate.Reigning => CinematicCue.LeaderFreed,
            _ => CinematicCue.None,
        };
    }

    public static CinematicCue ForSettlement(SettlementOutcomeRow row) => (Disposition)row.Disposition switch
    {
        Disposition.Sack => CinematicCue.SettlementSacked,
        Disposition.Raze => CinematicCue.SettlementRazed,
        _ => CinematicCue.SettlementOccupied,
    };

    /// <summary>A battle as seen by one of its sides (<paramref name="viewerSide"/> 0 or 1).</summary>
    public static CinematicCue ForBattle(BattleRow b, int viewerSide)
    {
        switch ((BattleKind)b.Kind)
        {
            case BattleKind.Siege:
                return (SiegeOutcome)b.EndCode switch
                {
                    SiegeOutcome.Capitulated or SiegeOutcome.CapCapitulation => CinematicCue.Surrender,
                    SiegeOutcome.Stormed or SiegeOutcome.GarrisonDestroyed => CinematicCue.Stormed,
                    SiegeOutcome.Relieved or SiegeOutcome.Lifted or SiegeOutcome.CapLifted => CinematicCue.SiegeRaised,
                    _ => CinematicCue.None,
                };
            case BattleKind.Storming:
                return CinematicCue.Stormed;
            default:
                var end = (BattleEndReason)b.EndCode;
                if (b.WinnerSide < 0 || end == BattleEndReason.BothEliminated || end == BattleEndReason.BothDisengaged || end == BattleEndReason.Standoff)
                    return CinematicCue.Inconclusive;
                return b.WinnerSide == viewerSide ? CinematicCue.Victory : CinematicCue.Defeat;
        }
    }

    public static CinematicCue ForWar(WarRow w, int viewerSide) => (WarOutcome)w.Outcome switch
    {
        WarOutcome.Conquest => w.WinnerSide == viewerSide ? CinematicCue.Victory : CinematicCue.Defeat,
        WarOutcome.NegotiatedPeace => CinematicCue.Peace,
        WarOutcome.MutualCollapse => CinematicCue.Inconclusive,
        _ => CinematicCue.None,
    };
}
