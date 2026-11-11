using Proto.M7.War.Outcomes;

namespace Proto.M7.Support;

/// <summary>
/// [D] The implemented support capabilities (DIR5 §2.12 examples, D-079 DR29: "medics and supply wagons"; category B abstract
/// military support, charter §7.11 military supply and §7.12 field medical, D-065 R10). Any further capability (combat
/// engineers, charter §7.9) is a labelled PROPOSAL in docs/m7-plan/support-capabilities-proposal.md and is NOT implemented.
/// Stable codes.
/// </summary>
public enum SupportKind : byte
{
    /// <summary>Medical staff (surgeons, orderlies): coverage acts on the casualty split and on the care of the wounded.</summary>
    FieldMedical = 1,
    /// <summary>A supply train (wagons, their draft animals and drivers): joins the army's own train in lane E's logistics.</summary>
    SupplyTrain = 2,
}

/// <summary>[D] Why an allocation order is refused (it is recorded and changes nothing). Stable codes.</summary>
public enum SupportRefusal : byte
{
    None = 0,
    /// <summary>No such army, or an A outside 0..<see cref="SupportKinds.MaxPacked"/> (verification round 2, D-R2-04).</summary>
    UnknownArmy = 1,
    /// <summary>The issuing polity does not own the army (a panel allocates only its own support to its own armies).</summary>
    NotOwner = 2,
    /// <summary>B ≥ 0 and ⌊B ÷ 2^32⌋ is neither 1 nor 2.</summary>
    UnknownKind = 3,
    /// <summary>B &lt; 0: its target part is negative under the documented decoding; checked before the kind (verification round 2,
    /// D-R2-04: before, a negative B decoded to kind 0 and was refused UnknownKind first, so this code could not occur).</summary>
    NegativeTarget = 4,
    /// <summary>Naval forces: no naval support capability is designed (ships' surgeons and colliers are lane E's own terms).</summary>
    NavalForce = 5,
    ArmyDisbanded = 6,
}

/// <summary>[D] Event and order kinds of the support layer. Disjoint from T2 (40-43, 140-141), T3 (150-153), lane S (60-70,
/// 180-187), lane W (80-87, 200-205) and the tactical world (101-108).</summary>
public static class SupportKinds
{
    /// <summary>World event: a battle the army fought ended; its entities collapse (A = index into the scheduled battles).</summary>
    public const int BattleEnded = 90;
    /// <summary>Order: A = Pack(polity, army), B = kind × <see cref="TargetStride"/> + target amount (wagons or medical staff).</summary>
    public const int OrderAllocate = 230;
    public const long TargetStride = 1L << 32;
    /// <summary>[D, verification round 2, D-R2-04] The largest A that <see cref="Unpack"/> decodes without overflow: polity
    /// int.MaxValue, army 65,535 (= 2^47 − 1). The support world refuses an A outside 0..MaxPacked as UnknownArmy.</summary>
    public const long MaxPacked = (long)int.MaxValue * 65_536 + 65_535;

    public static long Pack(int polity, int army) => checked((long)polity * 65_536 + army);
    public static (int Polity, int Army) Unpack(long a) => ((int)(a / 65_536), (int)(a % 65_536));
    public static long PackTarget(SupportKind kind, long target)
    {
        if (target < 0 || target >= TargetStride) throw new ArgumentOutOfRangeException(nameof(target));
        return checked((long)kind * TargetStride + target);
    }
}

/// <summary>
/// [D] Every coefficient of the support layer. ALL are PROPOSALS (TUNE), placeholders for the M7.9 effects and M7.14 costs
/// (D-079 §4 D8) — DIR5: "These figures are examples, not approved formulas". Rates are per sim-year (Law 3) and are integrated
/// with the exact elapsed time. The PROPOSAL values and the reasoning behind each are in
/// docs/m7-plan/support-capabilities-proposal.md §5.
/// </summary>
public sealed record SupportRates
{
    /// <summary>Draft animals per support wagon: lane E's hitch rule ("two per wagon", ArmyLogistics.Evaluate). CITED.</summary>
    public long DraftPerWagon { get; init; } = 2;
    /// <summary>Drivers (support staff) per support wagon. PROPOSAL.</summary>
    public long DriversPerWagon { get; init; } = 1;
    /// <summary>LIMIT: medical staff at most this many per 1000 men of the army at the latch. PROPOSAL.</summary>
    public long MedicsPerThousandMax { get; init; } = 50;
    /// <summary>LIMIT: support wagons at most this many per 1000 men of the army at the latch. PROPOSAL.</summary>
    public long WagonsPerThousandMax { get; init; } = 100;
    /// <summary>Coverage (medical staff per 1000 men) at which a medical effect reaches its midpoint; the effect saturates as
    /// u(c) = c / (c + this) — diminishing returns, so stacking more staff on one army never multiplies the effect. PROPOSAL.</summary>
    public long SaturationCoverage { get; init; } = 10;
    /// <summary>The share (ppm) of men put out of action (the battle's dead) that full coverage would save as wounded; the save
    /// at coverage c is this × u(c). Without staff it is 0, so the casualty split is lane W's exactly. PROPOSAL.</summary>
    public long SaveMaxPpm { get; init; } = 400_000;
    /// <summary>Wounded returning to duty, ‰ per sim-year, without care. PROPOSAL.</summary>
    public long RecoveryBasePermilleYear { get; init; } = 3_000;
    /// <summary>Extra return-to-duty rate at full coverage (× u(c)), ‰ per sim-year. PROPOSAL.</summary>
    public long RecoveryCarePermilleYear { get; init; } = 6_000;
    /// <summary>Deaths from wounds among the wounded, ‰ per sim-year, without care. PROPOSAL.</summary>
    public long DiedOfWoundsBasePermilleYear { get; init; } = 1_500;
    /// <summary>The share (‰) of deaths from wounds that full coverage prevents (× u(c)). PROPOSAL.</summary>
    public long DiedOfWoundsCareCutPermille { get; init; } = 800;
    /// <summary>The share (‰) of the casualty-driven morale loss of a man who is saved (not dead) that is restored after the
    /// battle (the TacticalResolver's own loss formula, ConditionMath.MoraleLoss). PROPOSAL.</summary>
    public long MoraleRestorePermille { get; init; } = 500;

    public static SupportRates Proposal() => new();

    public void Validate()
    {
        if (DraftPerWagon < 0 || DriversPerWagon < 0 || MedicsPerThousandMax < 0 || WagonsPerThousandMax < 0) throw new ArgumentOutOfRangeException(nameof(DraftPerWagon));
        if (SaturationCoverage <= 0) throw new ArgumentOutOfRangeException(nameof(SaturationCoverage));
        if (SaveMaxPpm < 0 || SaveMaxPpm > 1_000_000) throw new ArgumentOutOfRangeException(nameof(SaveMaxPpm));
        if (RecoveryBasePermilleYear < 0 || RecoveryCarePermilleYear < 0 || DiedOfWoundsBasePermilleYear < 0) throw new ArgumentOutOfRangeException(nameof(RecoveryBasePermilleYear));
        if (DiedOfWoundsCareCutPermille < 0 || DiedOfWoundsCareCutPermille > 1000) throw new ArgumentOutOfRangeException(nameof(DiedOfWoundsCareCutPermille));
        if (MoraleRestorePermille < 0 || MoraleRestorePermille > 1000) throw new ArgumentOutOfRangeException(nameof(MoraleRestorePermille));
    }
}

/// <summary>[D] A polity's support pool at the start: medical staff (people), wagons, draft animals, drivers (people). People
/// and animals here are in addition to lane E's polity accounts (the stand-in for staff already raised).</summary>
public sealed record SupportPool(int Polity, long Medics, long Wagons, long DraftAnimals, long Drivers);

/// <summary>[D] A finished battle of one army (the stand-in for the M7.15 battle → campaign hand-off): lane W's collapse input
/// (entity counts by end category, end morale on the tactical scale), whether its side held the field, the tactical start
/// morale (the army's campaign morale on the tactical scale at the battle start) and the TacticalResolver's morale terms.
/// [D, verification round 2, D-R2-01] The campaign morale falls by MoraleStart − Collapse.MoraleEnd (less the medical restore) and
/// never rises, so MoraleStart must be the battle's real start morale; that it is so is the stand-in's assumption.
/// [MOR, D-082 DA1] Superseded in part: "never rises" holds only for an army whose side did not win. The battle now also carries the
/// winner the battle report names (<see cref="WinnerSide"/>, −1 none) and the two sides' strengths in persons at the battle start;
/// an army of the winning side (Collapse.Side == WinnerSide) gains the DA1 rise on top of DR29's decrease
/// (<see cref="Proto.M7.Morale.CampaignMorale"/>). HeldField stays lane W's desertion input only: the morale layer reads the winner
/// the report names (D-082 DA1-R (a)), never the field. [MOR, verification round 1, MOR-V1-09: the earlier sentence "under D-082 DA6
/// the defender of an equal battle wins without holding the field alone" is withdrawn: D-082 does not say it. Its only statement on
/// the field is DA6-R's PROPOSAL for the TacticalResolver's Standoff end, "the defender holds the field and wins, unless the attacker
/// ambushed from concealment". The rule does not depend on whether a DA6 or DA7 winner holds the field.]</summary>
public sealed record SupportBattle(int ArmyId, long AtMs, CollapseInput Collapse, bool HeldField, long MoraleStart,
                                   long MoraleLossFullStrength, long Leadership)
{
    /// <summary>[MOR] The side the battle report names the winner (0, 1), or −1 when it names none (a stand-in that reports no
    /// winner, or a battle today's resolvers do not decide: <see cref="Proto.M7.Morale.BattleWinner.Reported"/>).</summary>
    public int WinnerSide { get; init; } = -1;
    /// <summary>[MOR] Persons of the side that lost, at the battle start (the force the winner beat).</summary>
    public long BeatenPersons { get; init; }
    /// <summary>[MOR] Persons of the winning side at the battle start, every army of it that took part; 0 = this army's own men at
    /// the battle (the one-army stand-in).</summary>
    public long WinnerSidePersons { get; init; }
    /// <summary>[MOR2] D-083 DB1-R1 (a): the army's formation was placed on the battlefield (the battle report's
    /// FormationResult.EnteredAtMs is set). Default true: every stand-in battle so far is one its army fought. An army whose formation
    /// never reached the field (a reserve not committed; a formation that stayed aboard; an aircraft formation that never left its base)
    /// pays no entry cost ~~, takes no DR29 decrease and no DA1 rise (it did not take part: DA1-R (a)); the stand-in then requires its end
    /// morale to equal its start, no casualty and no in-battle charge~~. [MOR2, verification round 1, MOR2-R1-01, MOR2-R1-04: such a
    /// formation can still lose men and morale in the battle (troops killed aboard their transport, lane N), so its army takes DR29's
    /// decrease of what the battle cost it, and the stand-in accepts its losses and casualties; it takes no DA1 rise under READING
    /// MOR2-R1 (a) ("took part" = entered the field; morale proposal §11.2), and the stand-in requires only that the battle charged it
    /// nothing at an entry it never made.]</summary>
    public bool Entered { get; init; } = true;
    /// <summary>[MOR2] What the battle itself charged at the entry, on the battle scale (FormationResult.EntryCharged; 0 while the
    /// in-battle part, TacticalRules.EntryMoraleCost, is off): DR29's decrease is measured from MoraleStart less this, so the entry
    /// cost is counted once, by the settlement's own term (CampaignMorale.EntryPpm).</summary>
    public long EntryChargedInBattle { get; init; }
}

// ------------------------------------------------------------------ serialized rows (SupportCodec; populated-table tests)

/// <summary>One panel allocation order as executed (Refusal 0 = accepted: it becomes the army's target for that kind).</summary>
public readonly record struct SupportOrderRow(int Index, long AtMs, int Polity, int ArmyId, byte Kind, long Target, byte Refusal);

/// <summary>One latch: at an army's pulse start, the target in force, the army's limit, what was granted (the army holds Granted
/// afterwards) and what the pool held afterwards (wagons for a supply train, staff for medical).</summary>
public readonly record struct SupportAllocationRow(int ArmyId, int WarId, long PulseIndex, long AtMs, byte Kind, long Requested, long Limit,
                                                   long Granted, long PoolAfter);

/// <summary>One settlement of the care of an army's wounded over [FromMs, ToMs): the coverage in force, the wounded at the start,
/// the recovered (back to the ranks) and the dead of wounds.</summary>
public readonly record struct SupportCareRow(int ArmyId, long FromMs, long ToMs, long Coverage, long WoundedBefore, long Recovered, long Died);

/// <summary>One battle collapse with the medical save: persons before, the battle's dead before the save, the saved, then the
/// final dead, wounded, captured, deserted, the coverage, and the campaign morale before and after (micro units).</summary>
public readonly record struct SupportBattleRow(int Seq, int ArmyId, long AtMs, long Persons, long DeadBefore, long Saved, long Dead,
                                               long Wounded, long Captured, long Deserted, long Coverage, long MoraleBefore, long MoraleAfter);
