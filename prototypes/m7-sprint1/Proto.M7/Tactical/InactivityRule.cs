namespace Proto.M7.Tactical;

/// <summary>[TR] Which inactivity rule the tactical battle applies.</summary>
public enum InactivityModel : byte
{
    /// <summary>
    /// DIR5 §2.6 (D-079 DR20, R17; delegated design D6): per FORMATION, hidden + stationary for at least five pulses =&gt;
    /// ROUTED, unless concealed with an enemy in reach, holding a defensive order/position, or under an ambush (wait) order;
    /// plus the whole-field STANDOFF end when nothing at all progresses. See <see cref="HiddenStationaryRule"/>. DEFAULT.
    /// </summary>
    HiddenStationary = 0,
    /// <summary>
    /// The Sprint 1 rule (D-tactical.md §4): a SIDE whose every formation is inactive for five pulses disengages and its
    /// entities ESCAPE intact. Superseded for the tactical resolver by DIR5 §2.6 / D-079 R17 ("escaped" is not a permitted
    /// consequence). Retained ONLY because the AutoResolver (lane P) still implements it, so resolver-parity tests can pin
    /// both resolvers to the same rule until the AutoResolver is ported.
    /// </summary>
    LegacySideDisengage = 1,
}

/// <summary>[TR] The three DIR5 §2.6 exemptions ("distinguish genuine inactivity from active concealment, defence or
/// deliberate waiting"). Flags; None = genuine inactivity.</summary>
[Flags]
public enum InactivityExemption : byte
{
    None = 0,
    /// <summary>(a) Active concealment: a member is concealed — by its own cell, personal concealment, or the terrain on the
    /// enemy's sight line (verify-TR-r1 V4) — from an Active enemy within its engagement reach.</summary>
    ConcealedEnemyInReach = 1,
    /// <summary>(b) Defence: an EXPLICIT Hold order is in force, or the formation stands on one of its side's objectives.</summary>
    DefensiveHold = 2,
    /// <summary>(c) Deliberate waiting: the formation was deployed under an Ambush (wait) order that is still in force (its
    /// directive has not changed from the standing Hold).</summary>
    AmbushWait = 4,
}

/// <summary>[TR] One pulse-end judgement of a formation whose idle counter reached the threshold (inspection and tests).</summary>
public readonly record struct InactivityVerdict(long Pulse, long FormationId, int Side, long IdlePulses, InactivityExemption Exemptions, bool Routed);

/// <summary>
/// [TR] The DIR5 §2.6 hidden-stationary rule as a PURE predicate (the battle evaluates its inputs; this class decides).
///
/// PER FORMATION f AT THE END OF PULSE p (f has at least one Active member and has not routed):
///   Hidden_p(f)   := no on-field member of f is TRACKED by the enemy side at the end of p (hidden from ALL enemies).
///   Progress_p(f) := some member had PRIMARY activity during p (dealt/received damage, attacked a valid target, first
///                    detection credit, a new running minimum of its distance to a tracked enemy / own objective / exit by
///                    &gt;= one cell — D-tactical.md §4) or some member changed state, OR (verify-TR-r1 V1) the formation's
///                    anchor set a new running minimum (by &gt;= one cell) of its REMAINING PATH LENGTH to the GOAL CELL of the
///                    movement order it executes (MoveTo destination, or the cell an Attack marches on):
///                    Rem = |Anchor − P[i]| + Σ_{k=i}^{n−2} |P[k+1] − P[k]| along the planned path P from its next waypoint
///                    i (verify-TR-r2 V1: r1 used |Anchor − centre(goal)|, which GROWS on a detour around a wall or along a
///                    river to a ford, so such marches were judged stationary); one minimum per goal cell, never reset
///                    (GoalMinima).
///   Stationary_p(f) := NOT Progress_p(f)  — "stationary" is read as "goes nowhere": a formation marching toward its goal
///                    is not stationary; one that oscillates between goals it has already come as close to sets no new
///                    minimum, so it is stationary in effect (a stricter reading than "did not move", reported: P-TR3).
///   idle_p(f)     := Hidden_p(f) AND Stationary_p(f)
///   c_p(f)        := idle_p(f) ? min(c_{p-1}(f) + 1, N) : 0        (c_{-1} = 0; saturating at N: no counter overflow, so no
///                    implicit pulse ceiling)
///   Exempt(f)     := (a) OR (b) OR (c) evaluated at the end of p (see <see cref="InactivityExemption"/>):
///     (a) EXISTS Active member e and Active enemy x with |x - e| &lt;= R(e) such that e is CONCEALED FROM x: e's cell has
///         Concealment &lt; 1, or e has personal concealment &lt; 1, or TerrainSight.Query(x -&gt; e, eye heights) is not visible
///         or has Clarity &lt; 1 (a building, a crest, vegetation on the line) [verify-TR-r1 V4]; for an enemy vessel /
///         aircraft only the first two terms count;
///         R(e) = W(e) + v_f * PulseMs / 1000, W(e) = RangeCells*S for a ranged entity else the melee contact distance
///         (2r + reach), v_f = the formation's open-ground pace (mm/s): the distance it can close and strike within one pulse;
///     (b) Directive(f) = Hold AND that Hold was ORDERED (an accepted explicit Hold order, not the default standing state)
///         OR |Anchor(f) - o| &lt;= S for some objective o of f's side;
///     (c) Posture(f) = Ambush AND Directive(f) = Hold (the deployment-time ambush order is still in force).
///   ROUT_p(f)    := c_p(f) &gt;= N AND NOT Exempt(f),   N = TacticalRules.InactivityPulses = 5 (Director value).
/// CONSEQUENCE (D-079 R17: "routed or lost"; escaped is not used): ROUTED — every Active member becomes Routing and leaves by
///   the Slice D rout mechanics (escape at the exit edge, or surrender when cut off). Routed was chosen over "lost" because
///   it reuses the existing, conserved rout path (no new casualty mapping is invented); "lost" is the reported alternative.
/// MUTUAL GUARD (OPTION TacticalRules.MutualStandoffGuard, default OFF since verify-TR-r1 V3 — D-079 S304 "routed or lost,
///   per unit; not a draw ... by default"): when ON and the routs of a pulse end would leave neither side an Active entity,
///   nobody is routed and the battle ends as a STANDOFF. OFF (default): both sides' inactive formations rout (BothEliminated).
/// STANDOFF: if no formation on the field made progress in each of the last N pulses (whole-field idle counter G &gt;= N), the
///   battle ends as <see cref="BattleEndReason.Standoff"/>: nobody is routed or removed; Active entities stay Active
///   (survivors). This is what ends battles in which every remaining formation is exempt (both sides holding or waiting) or
///   VISIBLE but idle — cases DIR5 §2.6 does not cover (its rule is for HIDDEN units; its end list is dead / retreated /
///   routed / surrendered). It is a state condition, not a pulse count, and an UNRATIFIED end condition introduced by lane
///   TR (PROPOSAL P-TR1): it is the shipped default only because without it such battles never end (a measured hang).
/// </summary>
public static class HiddenStationaryRule
{
    /// <summary>Next counter value: idle ? min(c + 1, n) : 0.</summary>
    public static long NextCounter(long counter, bool hidden, bool progress, int n)
        => hidden && !progress ? Math.Min(counter + 1, n) : 0;

    /// <summary>ROUT predicate: counter &gt;= n and no exemption.</summary>
    public static bool Routs(long counter, int n, InactivityExemption exemptions) => counter >= n && exemptions == InactivityExemption.None;
}
