using Proto.M7.Fixed;

namespace Proto.M7.Tactical;

/// <summary>
/// Cap families of the Director-revision PROPOSED per-formation caps (docs/m7-spec.md:391-404, table rows :395-402).
/// Stable codes.
/// </summary>
public enum CapFamily : byte
{
    HeavyInfantry = 0,          // "Heavy/aggregated infantry" (m7-spec.md:395)
    ArchersLightInfantry = 1,   // "Archers and lighter infantry" (m7-spec.md:396)
    Tanks = 2,                  // :397
    Helicopters = 3,            // :398
    Ships = 4,                  // :399
    FixedWing = 5,              // :400
    Snipers = 6,                // :401
    Mortars = 7,                // :402
    /// <summary>
    /// Lane N: a family with NO per-formation cap. D-076 §5 CAP-5 lists families without a cap ("Any cap for them would be
    /// invented"); D-079 R6 keeps cavalry, siege (rams to field artillery), air defence, missiles and reconnaissance
    /// uncapped (economy-governed). <see cref="ProposedCaps.Check"/> never refuses a positive count for it.
    /// </summary>
    Uncapped = 8,
}

/// <summary>
/// Per-formation entity caps — the Director revision's INITIAL PROPOSED caps, cited verbatim from docs/m7-spec.md:391-404:
/// "Initial proposed per-formation caps: Heavy/aggregated infantry 50 soldiers; Archers and lighter infantry 60 soldiers;
/// Tanks 5 vehicles; Helicopters 3; Ships 5; Fixed-wing aircraft 1; Snipers 2 personnel; Mortars 5 weapon systems/crews"
/// and ":404 These are proposed limits, not measured performance guarantees."
/// They are PROPOSALS (not ratified): the table is CONFIGURABLE (<see cref="WithOverride"/>), changes no existing data,
/// and the prototype refuses a formation larger than its family cap.
/// </summary>
public sealed class ProposedCaps
{
    public const string Citation = "docs/m7-spec.md:391-404 (rows :395-402), Director revision, PROPOSED caps";
    private readonly int[] _cap;

    private ProposedCaps(int[] cap) { _cap = cap; }

    /// <summary>The proposed caps exactly as listed at docs/m7-spec.md:395-402.</summary>
    public static ProposedCaps Proposed { get; } = new(new[] { 50, 60, 5, 3, 5, 1, 2, 5 });

    /// <summary>The cap of a family; <see cref="int.MaxValue"/> for <see cref="CapFamily.Uncapped"/> (no cap — lane N).</summary>
    public int CapOf(CapFamily f) => f == CapFamily.Uncapped ? int.MaxValue : _cap[(int)f];

    /// <summary>A copy with one family's cap replaced (configurability; the shipped proposal is untouched).</summary>
    public ProposedCaps WithOverride(CapFamily f, int cap)
    {
        if (cap <= 0) throw new ArgumentOutOfRangeException(nameof(cap));
        if (f == CapFamily.Uncapped) throw new ArgumentException("an uncapped family has no cap to override (lane N: no cap is invented)", nameof(f));
        var c = (int[])_cap.Clone();
        c[(int)f] = cap;
        return new ProposedCaps(c);
    }

    /// <summary>Null when a formation of this size is allowed, else the refusal text.</summary>
    public string? Check(CapFamily f, long count)
    {
        if (count <= 0) return "formation must have at least one entity";
        if (f == CapFamily.Uncapped) return null;          // lane N: D-076 CAP-5 / D-079 R6
        if (count > _cap[(int)f]) return $"formation of {count} exceeds the proposed {f} cap {_cap[(int)f]} ({Citation})";
        return null;
    }
}

/// <summary>
/// Battle stats of one unit identity (spec §4: NO ratified numbers exist; recon-critic A13). Values are taken from the
/// PROPOSED unit charter's normalized bands (docs/m7-plan/unit-catalogue-and-progression.md:199-231 and the per-family
/// rows :664-1029) as LABELLED PLACEHOLDERS. Where the charter gives a band rather than a number, the value chosen inside
/// (or on the boundary of) the band is a PLACEHOLDER. Fields marked "PLACEHOLDER (invented)" have no source at all.
/// All integer. Band scale: VL 1-15, L 15-35, M 35-55, H 55-75, VH 75-95 (unit-catalogue…md:209-213), "relative to the
/// same-Age cohort" (:201).
/// </summary>
public sealed record UnitProfile(
    int FamilyKey,
    int IdentityKey,
    string Name,
    CapFamily CapFamily,
    long Integrity,
    long Melee,
    long Ranged,
    long Defence,
    long RangeCells,
    long SightCells,
    long SpeedNum,
    long SpeedDen,
    string Citation)
{
    /// <summary>Per-entity hit points. Mapping Integrity band value -&gt; entity HP 1:1 is PLACEHOLDER (invented).</summary>
    public long MaxHp => Integrity;
    public bool IsRanged => Ranged > 0 && RangeCells > 0;
    /// <summary>[TR] Charge impetus coefficient chi (Q32 in [0, 1]): a melee blow struck at the end of a run-up gets the factor
    /// 1 + chi * min(1, distance covered this step / open-ground sprint per step) (TerrainCombat). Default 0 = no shock
    /// charge (the shipped Sprint 1 profiles carry none; no value is invented for them — tests use a labelled TEST INPUT).</summary>
    public Fixed.Q32 Impetus { get; init; }

    // ---- lane N (snipers, M7 Phase C): both default to the identity, so every existing profile behaves exactly as before.
    /// <summary>Legacy ranged fire: at most one shot per this many internal steps (low fire rate). Default 1 = every step.</summary>
    public int FireIntervalSteps { get; init; } = 1;
    /// <summary>Personal concealment (camouflage): multiplies the enemy's acquisition chance unless the entity fired within the
    /// last step. Default One = none. Also makes the entity "concealed" for the active-concealment clause.</summary>
    public Q32 PersonalConcealment { get; init; } = Q32.One;
}

/// <summary>The two profiles the vertical slice needs (spec §4: "do not invent other stats beyond what is needed").</summary>
public static class UnitProfiles
{
    private const string Doc = "docs/m7-plan/unit-catalogue-and-progression.md";

    /// <summary>
    /// heavy_infantry (family key 3), A1 identity warband (301) — unit-catalogue…md:655-672.
    /// Integrity 50: CITED, ":202 the heavy-infantry realization is the reference formation, with Integrity 50 and Melee 50"
    ///   and ":664 Integrity M (anchor 50)".
    /// Melee 50: CITED anchor :202 (the family row :664 says "Melee M–H"; the anchor value is used).
    /// Ranged 0: CITED ":664 Ranged — until A7".
    /// Defence 45: PLACEHOLDER inside band M 35-55 (":664 Defence M", band :211).
    /// RangeCells 0 (no ranged weapon). SightCells 12: PLACEHOLDER (invented; ":664 Sight L" has no cell mapping).
    /// Speed x1/1: ":663 Foot (×0.9–1.1)" — PLACEHOLDER midpoint of the cited band (movement multiplier :217).
    /// </summary>
    public static UnitProfile HeavyInfantry { get; } = new(
        FamilyKey: 3, IdentityKey: 301, Name: "heavy_infantry/warband", CapFamily: CapFamily.HeavyInfantry,
        Integrity: 50, Melee: 50, Ranged: 0, Defence: 45, RangeCells: 0, SightCells: 12, SpeedNum: 1, SpeedDen: 1,
        Citation: Doc + ":202, :211, :217, :663-664 (bands; Defence/Sight/speed point values PLACEHOLDER)");

    /// <summary>
    /// ranged_infantry (family key 4), A2 identity archers (402) — unit-catalogue…md:712-725.
    /// Integrity 35: PLACEHOLDER at the L/M band boundary (":721 Integrity L–M", bands :210-211).
    /// Melee 25: PLACEHOLDER midpoint of band L 15-35 (":721 Melee L").
    /// Ranged 75: PLACEHOLDER at the H/VH boundary (":721 Ranged H–VH", bands :212-213).
    /// Defence 25: PLACEHOLDER midpoint of band L (":721 Defence L").
    /// RangeCells 2: CITED upper end of "short 1–2" (:223) for ":721 Range short (A1–A6)".
    /// SightCells 12: PLACEHOLDER (invented). Speed x1/1: ":720 Foot (×0.9–1.1)" midpoint PLACEHOLDER.
    /// </summary>
    public static UnitProfile Archers { get; } = new(
        FamilyKey: 4, IdentityKey: 402, Name: "ranged_infantry/archers", CapFamily: CapFamily.ArchersLightInfantry,
        Integrity: 35, Melee: 25, Ranged: 75, Defence: 25, RangeCells: 2, SightCells: 12, SpeedNum: 1, SpeedDen: 1,
        Citation: Doc + ":210-213, :217, :223, :720-721 (bands; point values PLACEHOLDER)");
}
