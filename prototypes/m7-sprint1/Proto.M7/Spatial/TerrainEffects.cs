using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>
/// [TR] Lane TR (M7 Phase C, DIR5 §2.7 "Terrain, concealment, forest cover, elevation and line of sight must materially
/// influence combat"): the per-class PHYSICAL terrain properties and the global terrain coefficients that enter the
/// tactical resolution equations. INPUT table, validated. Every value is a coefficient INSIDE an equation (movement cost,
/// line of sight, hit chance, cohesion) — never a free-floating buff (CLAUDE.md law 2). Integer / Q32 only.
///
/// PER CLASS
///  - FeatureHeightMm: height of the vegetation or structure layer above the cell's ground (canopy, roofs). A sight or fire
///    line is INSIDE the layer of an intermediate cell iff it passes below ground + FeatureHeightMm somewhere in that cell.
///    Concealment (ConcealmentTable) and missile interception (MissileTransmission) apply only to cells whose layer the line
///    is inside — an observer on a hill looking OVER a forest is not attenuated by it. The target's own cell always applies
///    (the target stands inside its own layer).
///  - Opaque: the layer blocks sight and missiles outright where the line is inside it (Building). Wall stays infinitely tall
///    (Slice C semantics) regardless of this table.
///  - MissileTransmission (COVER, Q32 in (0, 1]): fraction of missiles NOT intercepted by one cell of the layer the missile
///    passes through, and by the struck entity's own cell. Cover protects from fire; concealment protects from observation —
///    two different coefficients (US Army FM 3-21.8 (2007), "cover and concealment").
///  - Footing (Q32 in (0, 1]): melee effectiveness of an entity standing in the class, multiplying both its attack (as
///    attacker) and its defence (as defender): mud and water deny footing (Agincourt 1415 — Keegan, The Face of Battle, 1976,
///    ch. 2; attacks across rivers: Granicus 334 BC, Arrian, Anabasis I.13-16).
///  - OrderCap (Q32 in (0, 1]): the closest order a formation can keep in the class; formation COHESION is the mean OrderCap
///    over its Active members' cells (Polybius, Histories XVIII.31: the phalanx needs level, clear ground; ditches, clumps of
///    trees, ridges and water courses break it).
/// GLOBAL
///  - Climb (ClimbNum/ClimbDen): Naismith's rule (W. W. Naismith, Scottish Mountaineering Club Journal, 1892): one hour per
///    5 km on the flat plus one hour per 600 m of ascent, i.e. 1 mm of ascent costs 5000/600 = 25/3 mm of open-ground travel.
///    Descent is free (Naismith). CITED rule; its use for formed bodies of troops is a PLACEHOLDER transfer.
///  - MeleeSlope (beta), MissileElevation (gamma), GradientCap: hit-chance coefficients on the attacker's effective attack,
///    (1 - beta*g) for a melee blow up a gradient g and (1 + gamma*g) for a missile shot DOWN a gradient g, g clamped to
///    [-cap, cap]. Direction CITED (Vegetius, Epitoma rei militaris III.13: the higher ground favours both the charge and the
///    missile); magnitudes PLACEHOLDER.
///  - CohesionFloor (c0): melee effectiveness of a fully disordered formation relative to close order; the cohesion factor is
///    c0 + (1 - c0)*cohesion. PLACEHOLDER.
///
/// IDENTITY GUARANTEE (validated): Road and Open are not opaque and have transmission 1, footing 1, order cap 1 (and
/// ConcealmentTable validates their concealment to 1),
/// and flat ground has gradient 0, so on a flat Road/Open field EVERY terrain coefficient is exactly One and every Slice D
/// equation is reproduced bit for bit (the combined-arms trace golden does not move; tested).
/// </summary>
public sealed class TerrainEffects
{
    private readonly long[] _featureHeight;
    private readonly bool[] _opaque;
    private readonly Q32[] _transmission;
    private readonly Q32[] _footing;
    private readonly Q32[] _orderCap;

    public long ClimbNum { get; }
    public long ClimbDen { get; }
    public Q32 MeleeSlope { get; }
    public Q32 MissileElevation { get; }
    public Q32 GradientCap { get; }
    public Q32 CohesionFloor { get; }

    /// <summary>Upper bound on the climb ratio (keeps climb * height products far inside long).</summary>
    public const long MaxClimbRatio = 1_000;

    /// <summary>Sentinel feature height: a layer with no top (every line through the cell is inside it). Used by
    /// <see cref="SliceCEquivalent"/> to reproduce the Slice C rule "every intermediate concealing cell attenuates".</summary>
    public const long UnboundedLayerMm = long.MaxValue;

    /// <summary>True iff a line whose lowest height inside the cell is <paramref name="lineMinZ"/> is inside the cell's layer.</summary>
    public bool InsideLayer(TerrainClass c, long groundMm, long lineMinZ)
    {
        long fh = _featureHeight[(int)c];
        if (fh == UnboundedLayerMm) return true;
        return lineMinZ < groundMm + fh;
    }

    private TerrainEffects(long[] fh, bool[] op, Q32[] tr, Q32[] ft, Q32[] oc, long climbNum, long climbDen, Q32 meleeSlope,
                           Q32 missileElevation, Q32 gradientCap, Q32 cohesionFloor)
    {
        _featureHeight = fh; _opaque = op; _transmission = tr; _footing = ft; _orderCap = oc;
        ClimbNum = climbNum; ClimbDen = climbDen; MeleeSlope = meleeSlope; MissileElevation = missileElevation;
        GradientCap = gradientCap; CohesionFloor = cohesionFloor;
    }

    public long FeatureHeightMm(TerrainClass c) => _featureHeight[(int)c];
    public bool Opaque(TerrainClass c) => _opaque[(int)c];
    public Q32 MissileTransmission(TerrainClass c) => _transmission[(int)c];
    public Q32 Footing(TerrainClass c) => _footing[(int)c];
    public Q32 OrderCap(TerrainClass c) => _orderCap[(int)c];

    /// <summary>
    /// Naismith climb cost of an ascent of dh mm, in the A* unit (cost x1000 per mm times mm): 0 for dh &lt;= 0 (descent free),
    /// else floor(dh * 1000 * ClimbNum / ClimbDen). Independent of the horizontal length (Naismith: time per metre of ascent).
    /// </summary>
    public long ClimbCost(long dhMm) => dhMm <= 0 ? 0 : FixedMath.MulDivFloor(dhMm, checked(1000 * ClimbNum), ClimbDen);

    /// <summary>
    /// Climb cost per mm of horizontal travel on a gradient dh over d mm (the same Naismith rule spread over the run, cost
    /// x1000 per mm): 0 for dh &lt;= 0 or d &lt;= 0, else floor(dh * 1000 * ClimbNum / (ClimbDen * d)).
    /// </summary>
    public long ClimbCostPerMm(long dhMm, long runMm)
        => dhMm <= 0 || runMm <= 0 ? 0 : FixedMath.MulDivFloor(dhMm, checked(1000 * ClimbNum), checked(ClimbDen * runMm));

    public static TerrainEffects Create(ReadOnlySpan<long> featureHeightMm, ReadOnlySpan<bool> opaque, ReadOnlySpan<Q32> transmission,
                                        ReadOnlySpan<Q32> footing, ReadOnlySpan<Q32> orderCap, long climbNum, long climbDen,
                                        Q32 meleeSlope, Q32 missileElevation, Q32 gradientCap, Q32 cohesionFloor)
    {
        string? err = Validate(featureHeightMm, opaque, transmission, footing, orderCap, climbNum, climbDen, meleeSlope, missileElevation,
                               gradientCap, cohesionFloor);
        if (err is not null) throw new ArgumentException(err);
        return new TerrainEffects(featureHeightMm.ToArray(), opaque.ToArray(), transmission.ToArray(), footing.ToArray(), orderCap.ToArray(),
                                  climbNum, climbDen, meleeSlope, missileElevation, gradientCap, cohesionFloor);
    }

    /// <summary>Null when valid, else the first violation.</summary>
    public static string? Validate(ReadOnlySpan<long> fh, ReadOnlySpan<bool> op, ReadOnlySpan<Q32> tr, ReadOnlySpan<Q32> ft, ReadOnlySpan<Q32> oc,
                                   long climbNum, long climbDen, Q32 meleeSlope, Q32 missileElevation, Q32 gradientCap, Q32 cohesionFloor)
    {
        int n = TerrainCostTable.ClassCount;
        if (fh.Length != n || op.Length != n || tr.Length != n || ft.Length != n || oc.Length != n) return $"expected {n} classes per table";
        for (int i = 0; i < n; i++)
        {
            var k = (TerrainClass)i;
            if ((fh[i] < 0 || fh[i] > TerrainGrid.MaxAbsHeightMm) && fh[i] != UnboundedLayerMm)
                return $"class {k}: feature height outside [0, {TerrainGrid.MaxAbsHeightMm}] (or the unbounded sentinel)";
            // Termination (D-tactical.md §4): every attack must keep a positive hit chance, so no coefficient may be zero.
            if (tr[i].Raw <= 0 || tr[i] > Q32.One) return $"class {k}: missile transmission must be in (0, 1]";
            if (ft[i].Raw <= 0 || ft[i] > Q32.One) return $"class {k}: footing must be in (0, 1]";
            if (oc[i].Raw <= 0 || oc[i] > Q32.One) return $"class {k}: order cap must be in (0, 1]";
        }
        foreach (var k in new[] { TerrainClass.Road, TerrainClass.Open })
        {
            int i = (int)k;
            // (A layer height is harmless here: Road/Open concealment is validated to 1 and transmission to 1 below.)
            if (op[i] || tr[i] != Q32.One || ft[i] != Q32.One || oc[i] != Q32.One)
                return $"class {k} must be the identity (not opaque, transmission/footing/order 1): flat open ground reproduces Slice D exactly";
        }
        if (climbNum < 0 || climbDen <= 0 || climbNum > MaxClimbRatio * climbDen) return "climb ratio must be in [0, 1000]";
        if (!meleeSlope.IsProbability || !missileElevation.IsProbability) return "slope coefficients must be in [0, 1]";
        if (gradientCap.Raw <= 0 || gradientCap > Q32.One) return "gradient cap must be in (0, 1]";
        // (1 - beta*g) and (1 + gamma*g) must stay positive for |g| <= cap: beta*cap < 1 and gamma*cap < 1.
        if (meleeSlope.Mul(gradientCap) >= Q32.One || missileElevation.Mul(gradientCap) >= Q32.One) return "slope coefficient times gradient cap must be < 1";
        if (cohesionFloor.Raw <= 0 || cohesionFloor > Q32.One) return "cohesion floor must be in (0, 1]";
        return null;
    }

    private static readonly Q32 OneHalf = Q32.FromRatio(1, 2), ThreeQuarters = Q32.FromRatio(3, 4), OneQuarter = Q32.FromRatio(1, 4);

    private static TerrainEffects? _placeholder;
    /// <summary>The shared immutable instance of <see cref="PlaceholderTestTable"/> (idempotent lazy creation).</summary>
    public static TerrainEffects Placeholder => _placeholder ??= PlaceholderTestTable();

    /// <summary>
    /// PLACEHOLDER test table (lane TR; [AP] two field-works classes appended). Values, by class (Road, Open, Forest, Jungle,
    /// Hills, Mountain, Water, Wall, Marsh, Ford, Building, [AP] Earthwork, Ditch):
    ///  - FeatureHeightMm: 0, 0, 15,000, 25,000, 0, 0, 0, 0, 0, 0, 8,000 — PLACEHOLDER (order of magnitude: mature temperate
    ///    canopy 15-30 m, tropical canopy 25-45 m, the low ends used; two-storey building ~8 m).
    ///  - Opaque: Building only.
    ///  - MissileTransmission: Forest 3/4, Jungle 1/2, else 1 — PLACEHOLDER.
    ///  - Footing: Marsh 1/2, Ford 1/2, else 1 — PLACEHOLDER.
    ///  - OrderCap: Forest 1/2, Jungle 1/4, Mountain 1/2, Marsh 1/2, Ford 1/2, else 1 — PLACEHOLDER.
    ///  - Climb 25/3 — CITED (Naismith 1892); beta = gamma = 1/2 at a cap of 1 (45 degrees) — PLACEHOLDER; c0 = 1/2 — PLACEHOLDER.
    ///  - [AP] Earthwork: a 1,500 mm parapet layer, not opaque, missile transmission 1/4 (cover), footing 1, order cap 1 (the
    ///    occupant keeps its order); Ditch: no layer, transmission 1, footing 1/2, order cap 1/2 (an obstacle breaks close order:
    ///    Polybius, Histories XVIII.31 names ditches among the ground that breaks the phalanx — direction CITED, magnitudes
    ///    PLACEHOLDER, the same as lane TR's Ford). docs/m7-plan/concealment-actions-proposal.md §4.3.
    /// </summary>
    public static TerrainEffects PlaceholderTestTable()
    {
        var one = Q32.One;
        return Create(
            featureHeightMm: new long[] { 0, 0, 15_000, 25_000, 0, 0, 0, 0, 0, 0, 8_000, 1_500, 0 },
            opaque: new[] { false, false, false, false, false, false, false, false, false, false, true, false, false },
            transmission: new[] { one, one, ThreeQuarters, OneHalf, one, one, one, one, one, one, one, OneQuarter, one },
            footing: new[] { one, one, one, one, one, one, one, one, OneHalf, OneHalf, one, one, OneHalf },
            orderCap: new[] { one, one, OneHalf, OneQuarter, one, OneHalf, one, one, OneHalf, OneHalf, one, one, OneHalf },
            climbNum: 25, climbDen: 3,
            meleeSlope: OneHalf, missileElevation: OneHalf, gradientCap: one, cohesionFloor: OneHalf);
    }

    /// <summary>
    /// The SLICE-C-EQUIVALENT table (control for tests: "terrain off"): every layer UNBOUNDED (so concealment applies on every
    /// intermediate cell, exactly the Slice C LineOfSight rule), nothing opaque, every coefficient One, no climb cost. Under it
    /// the lane-TR sight query equals <see cref="LineOfSight.Query"/> on every grid (tested) and every lane-TR combat and
    /// movement coefficient is the identity.
    /// </summary>
    public static TerrainEffects SliceCEquivalent()
    {
        int n = TerrainCostTable.ClassCount;
        var fh = new long[n]; var op = new bool[n]; var q = new Q32[n];
        Array.Fill(fh, UnboundedLayerMm);
        Array.Fill(q, Q32.One);
        return Create(fh, op, q, q, q, 0, 1, Q32.Zero, Q32.Zero, Q32.One, Q32.One);
    }
}
