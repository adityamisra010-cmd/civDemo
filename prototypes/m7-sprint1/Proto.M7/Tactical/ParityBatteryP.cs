using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>[P] Scenario families of the lane-P parity battery (reporting groups; stable codes).</summary>
public enum ParityFamily : byte
{
    /// <summary>Open, flat ground; one formation per side; one condition factor differs (numbers, supply, morale, ...).</summary>
    OpenConditions = 1,
    /// <summary>Composition and combined arms on open ground: ranged vs melee, multi-formation sides, special abilities
    /// (sniper concealment and fire interval, charge impetus).</summary>
    Composition = 2,
    /// <summary>Concealment and ambush: forest / jungle hide a waiting or holding formation.</summary>
    Concealment = 3,
    /// <summary>Terrain-heavy: slope (crest), river ford, marsh, forest cover, defile / street frontage.</summary>
    Terrain = 4,
}

/// <summary>[P] Stable ids of the lane-P battery scenarios. Never renumber (they key reports, CSVs and the battery hash).
/// 1-10 reuse the Sprint 1 <see cref="ParityScenarioId"/> builders where they are deployment-legal; 8 (the Sprint 1 ambush,
/// whose ambushers stand in the middle third, H-TR P-TR13) is NOT used and has no entry here.</summary>
public enum PScenario : byte
{
    // ---- TUNING scenarios (the only ones the calibration may see, and only on the tuning seeds)
    EqualInfantry = 1,
    Numbers2To1 = 2,
    ArchersVsInfantryOpen = 3,
    ArchersVsInfantryForest = 4,
    LowSupply = 5,
    LowMorale = 6,
    LeadershipEdge = 7,
    ExperienceEdge = 9,
    CombinedArms = 10,
    RiflemenVsInfantry = 11,
    SniperScreen = 12,
    ShockInfantry = 13,
    JungleAmbush = 14,
    HillCrest = 15,
    FordCrossing = 16,
    MarshFront = 17,
    ForestHoldArchers = 18,
    Defile = 19,
    ArchersOnHill = 20,
    HoldLineOpen = 21,
    // ---- HELD-OUT scenarios (never seen by the calibration, ParityCalibrationP; decide acceptance).
    //      [P fix r1, P-V7] Correction: they were NOT all unseen by the AutoResolver before the acceptance run — the C37
    //      metamorphic test (AutoResolverPTests.FavourableDefensiveTerrainNeverRaisesTheDefendersLosses, committed in the freeze
    //      commit 03c07cc and executed during development) ran model 2 on high-ridge, seeds 1-20 (never acceptance seeds),
    //      asserting only a direction of the losses. Disclosed in H-P.md (verification round 1).
    Numbers5To4 = 101,
    MixedConditions = 102,
    MeetingAsymmetric = 103,
    CombinedArmsVsMass = 104,
    RiflemenVsArchers = 105,
    ForestAmbushArchers = 106,
    HighRidge = 107,
    NarrowFordArchers = 108,
    UrbanStreet = 109,
}

/// <summary>[P] One battery scenario. Role A = side 0, role B = side 1 in every scenario and both orientations.</summary>
public sealed record PScenarioDef(PScenario Id, string Name, ParityFamily Family, bool HeldOut, string RoleA, string RoleB, string Description);

/// <summary>[P] One battery case: (scenario, seed). The orientation is a function of the seed (see <see cref="ParityBatteryP.Mirrored"/>).</summary>
public readonly record struct PCase(PScenario Scenario, ulong Seed);

/// <summary>
/// [P] The comparable outcome of one battle under either resolver (integer only). Winner = the side holding the field
/// (BattleResult.HoldsField), -1 = none. CasA / CasB = (Killed + Surrendered) / Start of the role, permille, floor.
/// DurationMs = EndMs - StartMs (the end instant: a step instant for an elimination, a pulse end for an inactivity end).
/// </summary>
public readonly record struct POutcome(int Winner, long CasA, long CasB, long Pulses, long DurationMs, BattleEndReason Reason)
{
    public static POutcome Of(BattleResult r, long startMs)
    {
        long[] start = new long[2], loss = new long[2];
        foreach (var f in r.Formations) { start[f.Side] += f.Start; loss[f.Side] += f.PersonnelLoss; }
        return new POutcome(r.HoldsField, FixedMath.MulDivFloor(loss[0], 1000, start[0]), FixedMath.MulDivFloor(loss[1], 1000, start[1]),
                            r.Pulses, r.EndMs - startMs, r.Reason);
    }
}

/// <summary>
/// [P] Lane P's repeatable parity battery (DIR5 §3.1; D-079 Q4 PROPOSAL). Families x scenarios x seeds, land only.
///
/// FIELD: the Slice D 48 x 32 field of 20,000 mm cells (PLACEHOLDER), cost / concealment / terrain-effects PLACEHOLDER tables
/// of Slice C and lane TR. Role A = side 0 = the ATTACKER where there is one (KnowledgeAi, Normal posture, objective = role
/// B's position); role B = side 1. Every new scenario is DEPLOYMENT-LEGAL under DIR5 §2.14 (<see cref="DeploymentRule.Thirds"/>
/// is set and re-checked by the tactical engine): role A inside columns 0-15, role B inside columns 32-47, terrain features
/// may lie anywhere. The terrain-defence defenders use <see cref="Posture.Defend"/> (an explicit Hold at pulse 0) with their
/// front rank 400 mm inside the first cell of their own third (x = 640,400 mm), so an attacker in contact (1,200 mm) stands
/// in the cell in front of it — the crest edge, the river bank, the marsh edge (the lane-TR test geometry).
///
/// ORIENTATION (independent seeds): case (s, seed) is built MIRRORED iff seed is even (every column x -> 47 - x, every mm
/// coordinate x -> 960,000 - x, exits swapped). Each seed is therefore one independent battle, both orientations occur
/// equally often, and the effective sample size of n cases is n (the Sprint 1 battery ran both orientations of each seed,
/// which were strongly correlated, parity-report.md §1).
///
/// SEED SETS (pre-committed, H-P.md §2): TUNING seeds 1-80 (tuning scenarios only); ACCEPTANCE seeds 5001-5160 (every
/// scenario, n = 160); CONTROL seeds 9001-9160 (held-out scenarios, tactical only: the attainability control of the bands;
/// for a tuning scenario the control sample is its tuning-seed tactical sample, n = 80).
///
/// Every numeric scenario input is a TEST INPUT that exercises one mechanism; none is a gameplay value. Formation sizes are
/// within the PROPOSED caps (HI 50, archers / riflemen 60, snipers 2).
/// </summary>
public static class ParityBatteryP
{
    public const ulong FirstTuningSeed = 1, LastTuningSeed = 80;
    public const ulong FirstAcceptanceSeed = 5001, LastAcceptanceSeed = 5160;
    public const ulong FirstControlSeed = 9001, LastControlSeed = 9160;

    /// <summary>The orientation of a seed: mirrored iff the seed is even.</summary>
    public static bool Mirrored(ulong seed) => seed % 2 == 0;

    /// <summary>A TEST INPUT shock-infantry profile: the heavy-infantry profile with charge impetus 1/2 (lane TR's labelled
    /// test value; no shipped profile carries an impetus).</summary>
    public static UnitProfile ShockInfantry { get; } = UnitProfiles.HeavyInfantry with { Name = "heavy_infantry/shock(TEST INPUT impetus 1/2)", Impetus = Q32.FromRatio(1, 2) };

    public static IReadOnlyList<PScenarioDef> All { get; } = new[]
    {
        // ---- tuning: open conditions (Sprint 1 builders)
        new PScenarioDef(PScenario.EqualInfantry, "equal-infantry", ParityFamily.OpenConditions, false, "HI 50", "HI 50", "meeting engagement on open ground (Sprint 1 #1)"),
        new PScenarioDef(PScenario.Numbers2To1, "numbers-2to1", ParityFamily.OpenConditions, false, "HI 50", "HI 25", "meeting engagement (Sprint 1 #2)"),
        new PScenarioDef(PScenario.LowSupply, "low-supply", ParityFamily.OpenConditions, false, "HI 50 supply 1/4", "HI 50", "Sprint 1 #5"),
        new PScenarioDef(PScenario.LowMorale, "low-morale", ParityFamily.OpenConditions, false, "HI 50 morale 550", "HI 50", "Sprint 1 #6"),
        new PScenarioDef(PScenario.LeadershipEdge, "leadership-edge", ParityFamily.OpenConditions, false, "HI 50 leadership 50", "HI 50", "Sprint 1 #7"),
        new PScenarioDef(PScenario.ExperienceEdge, "experience-edge", ParityFamily.OpenConditions, false, "HI 50 experience 50", "HI 50", "Sprint 1 #9"),
        // ---- tuning: composition
        new PScenarioDef(PScenario.ArchersVsInfantryOpen, "archers-vs-infantry-open", ParityFamily.Composition, false, "archers 60", "HI 50", "Sprint 1 #3"),
        new PScenarioDef(PScenario.CombinedArms, "combined-arms", ParityFamily.Composition, false, "HI 50 + archers 60", "HI 50 + archers 60", "Sprint 1 #10 (mirror-exact anchors)"),
        new PScenarioDef(PScenario.RiflemenVsInfantry, "riflemen-vs-infantry", ParityFamily.Composition, false, "riflemen 60", "HI 50", "long-range fire (6 cells) against melee infantry, meeting engagement"),
        new PScenarioDef(PScenario.SniperScreen, "sniper-screen", ParityFamily.Composition, false, "HI 50 + snipers 2", "HI 50", "special abilities: sniper personal concealment 1/8 and one shot per 5 steps (lane N profile), meeting engagement"),
        new PScenarioDef(PScenario.ShockInfantry, "shock-infantry", ParityFamily.Composition, false, "shock HI 50 (impetus 1/2)", "HI 50", "special ability: charge impetus (lane TR mechanism, TEST INPUT profile), meeting engagement"),
        // ---- tuning: concealment
        new PScenarioDef(PScenario.ArchersVsInfantryForest, "archers-vs-infantry-forest", ParityFamily.Concealment, false, "archers 60", "HI 50 in forest", "Sprint 1 #4: the defender in a forest block, no objective, attacks once it knows the archers"),
        new PScenarioDef(PScenario.JungleAmbush, "jungle-ambush", ParityFamily.Concealment, false, "HI 50 advancing", "HI 50 ambush in jungle", "deployment-legal ambush: a full-height jungle belt in role B's third (columns 32-37), the ambusher in Ambush posture at column 34, role A marches to column 42"),
        // ---- tuning: terrain
        new PScenarioDef(PScenario.HillCrest, "hill-crest", ParityFamily.Terrain, false, "HI 50 assault", "HI 50 holding a 10 m crest", "slope: a 10 m terrace from column 32, the defender's front rank on the crest edge (Defend)"),
        new PScenarioDef(PScenario.FordCrossing, "ford-crossing", ParityFamily.Terrain, false, "HI 50 assault", "HI 50 holding the far bank", "river: deep water in columns 30-31 except a 3-cell ford (rows 15-17); the defender holds the bank at column 32 (Defend)"),
        new PScenarioDef(PScenario.MarshFront, "marsh-front", ParityFamily.Terrain, false, "HI 50 assault", "HI 50 behind a marsh", "marsh in columns 26-31 (footing and order 1/2, cost 3000); the defender holds firm ground at column 32 (Defend)"),
        new PScenarioDef(PScenario.ForestHoldArchers, "forest-hold-archers", ParityFamily.Terrain, false, "archers 60", "HI 50 holding in forest", "cover vs concealment: a forest block (columns 32-38, rows 10-22), the defender one cell inside it holds (Defend) under archery"),
        new PScenarioDef(PScenario.Defile, "defile", ParityFamily.Terrain, false, "HI 50 assault", "HI 50 holding the exit", "frontage: walls in columns 26-31 except a 1-cell gap (row 16); the defender holds the defile exit at column 32 (Defend)"),
        new PScenarioDef(PScenario.ArchersOnHill, "archers-on-hill", ParityFamily.Terrain, false, "HI 50 assault", "archers 60 on a 10 m crest", "missile elevation + melee slope: archers hold the crest edge of a 10 m terrace (Defend)"),
        new PScenarioDef(PScenario.HoldLineOpen, "hold-line-open", ParityFamily.Terrain, false, "HI 50 assault", "HI 50 holding on open ground", "the terrain-free CONTROL of the Defend scenarios: the same assault on a line holding at column 32 on flat open ground"),
        // ---- held-out
        new PScenarioDef(PScenario.Numbers5To4, "numbers-5to4", ParityFamily.OpenConditions, true, "HI 50", "HI 40", "meeting engagement, a smaller numerical edge than numbers-2to1"),
        new PScenarioDef(PScenario.MixedConditions, "mixed-conditions", ParityFamily.OpenConditions, true, "HI 50 experience 25, leadership 30", "HI 50 supply 3/4, morale 850", "two condition factors per side at once, meeting engagement"),
        new PScenarioDef(PScenario.MeetingAsymmetric, "meeting-asymmetric", ParityFamily.OpenConditions, true, "HI 50 at column 14", "HI 50 at column 34", "the preserved pre-F1 geometry (role B 20 m nearer its exit; DIR5 §2.14 realistic asymmetry, not normalized)"),
        new PScenarioDef(PScenario.CombinedArmsVsMass, "combined-arms-vs-mass", ParityFamily.Composition, true, "HI 50 + archers 60", "HI 50 + HI 50", "missile-supported line against a two-formation melee mass, meeting engagement"),
        new PScenarioDef(PScenario.RiflemenVsArchers, "riflemen-vs-archers", ParityFamily.Composition, true, "riflemen 60", "archers 60", "range edge (6 vs 2 cells) between two missile formations, meeting engagement"),
        new PScenarioDef(PScenario.ForestAmbushArchers, "forest-ambush-archers", ParityFamily.Concealment, true, "HI 50 advancing", "archers 60 ambush in forest", "a full-height forest belt (columns 32-36), archers in Ambush posture at column 33, role A marches to column 42"),
        new PScenarioDef(PScenario.HighRidge, "high-ridge", ParityFamily.Terrain, true, "HI 50 assault", "HI 50 holding a 20 m crest", "steeper slope (gradient capped at 1) than hill-crest"),
        new PScenarioDef(PScenario.NarrowFordArchers, "narrow-ford-archers", ParityFamily.Terrain, true, "HI 50 assault", "archers 60 holding the far bank", "deep water in columns 30-31 except a 2-cell ford (rows 15-16); archers hold the bank (Defend)"),
        new PScenarioDef(PScenario.UrbanStreet, "urban-street", ParityFamily.Terrain, true, "HI 50 assault", "HI 50 holding the street exit", "built-up blocks (opaque 8 m, impassable) in columns 24-31 cut by a 1-cell road (row 16); the defender holds the street exit at column 32 (Defend)"),
    };

    public static PScenarioDef Get(PScenario id)
    {
        foreach (var s in All) if (s.Id == id) return s;
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    public static PScenario[] TuningScenarios() => Select(false);
    public static PScenario[] HeldOutScenarios() => Select(true);
    public static PScenario[] AllScenarios()
    {
        var r = new PScenario[All.Count];
        for (int i = 0; i < r.Length; i++) r[i] = All[i].Id;
        return r;
    }

    private static PScenario[] Select(bool heldOut)
    {
        var l = new List<PScenario>();
        foreach (var s in All) if (s.HeldOut == heldOut) l.Add(s.Id);
        return l.ToArray();
    }

    /// <summary>Cases for the given scenarios over seeds [first, last], in (scenario order as given, seed ascending) order.</summary>
    public static PCase[] Cases(IReadOnlyList<PScenario> scenarios, ulong firstSeed, ulong lastSeed)
    {
        if (lastSeed < firstSeed) throw new ArgumentException("empty seed range");
        var l = new List<PCase>();
        foreach (var s in scenarios) for (ulong seed = firstSeed; seed <= lastSeed; seed++) l.Add(new PCase(s, seed));
        return l.ToArray();
    }

    /// <summary>The TUNING cases: tuning scenarios x tuning seeds. The ONLY input the calibration may see.</summary>
    public static PCase[] TuningCases() => Cases(TuningScenarios(), FirstTuningSeed, LastTuningSeed);

    /// <summary>True iff a case is admissible as calibration input (tuning scenario AND tuning seed).</summary>
    public static bool IsTuningCase(PCase c) => !Get(c.Scenario).HeldOut && c.Seed >= FirstTuningSeed && c.Seed <= LastTuningSeed;

    public static long RoleStart(PScenario id, int side)
    {
        long n = 0;
        foreach (var f in Build(id, 1).Formations) if (f.Side == side) n += f.Count;
        return n;
    }

    /// <summary>Runs the TacticalResolver (KnowledgeAi on both sides, default tactical rules: DIR5 hidden-stationary inactivity).</summary>
    public static BattleResult RunTacticalResult(PCase c)
    {
        var s = Build(c.Scenario, c.Seed);
        return new TacticalBattle(s, new KnowledgeAi(), new KnowledgeAi()).Run();
    }

    public static POutcome RunTactical(PCase c) => POutcome.Of(RunTacticalResult(c), 0);

    public static POutcome RunAuto(PCase c, AutoCoefficients coefficients)
        => POutcome.Of(new AutoBattle(Build(c.Scenario, c.Seed), coefficients).Run(), 0);

    // ------------------------------------------------------------------ builders

    private const long W = TacticalScenarios.FieldW * TacticalScenarios.CellMm;   // 960,000 mm
    /// <summary>Front rank of a role-B defender, 400 mm inside column 32 (the first cell of side 1's third, as built).</summary>
    private const long DefenderX = 640_400;
    private const long MidY = 16 * TacticalScenarios.CellMm + TacticalScenarios.CellMm / 2;   // row 16 centre, 330,000 mm

    /// <summary>Builds the battle setup of (scenario, seed); orientation = <see cref="Mirrored"/>(seed).</summary>
    public static BattleSetup Build(PScenario id, ulong seed)
    {
        bool mir = Mirrored(seed);
        // Sprint 1 builders (deployment-legal ones; H-TR V8 pins that only #8 violates the thirds).
        switch (id)
        {
            case PScenario.EqualInfantry: return ParityScenarios.Build(ParityScenarioId.EqualInfantry, seed, mir);
            case PScenario.Numbers2To1: return ParityScenarios.Build(ParityScenarioId.Numbers2To1, seed, mir);
            case PScenario.ArchersVsInfantryOpen: return ParityScenarios.Build(ParityScenarioId.ArchersVsInfantryOpen, seed, mir);
            case PScenario.ArchersVsInfantryForest: return ParityScenarios.Build(ParityScenarioId.ArchersVsInfantryForest, seed, mir);
            case PScenario.LowSupply: return ParityScenarios.Build(ParityScenarioId.LowSupply, seed, mir);
            case PScenario.LowMorale: return ParityScenarios.Build(ParityScenarioId.LowMorale, seed, mir);
            case PScenario.LeadershipEdge: return ParityScenarios.Build(ParityScenarioId.LeadershipEdge, seed, mir);
            case PScenario.ExperienceEdge: return ParityScenarios.Build(ParityScenarioId.ExperienceEdge, seed, mir);
            case PScenario.CombinedArms: return ParityScenarios.Build(ParityScenarioId.CombinedArms, seed, mir);
        }

        var grid = TacticalScenarios.OpenField();
        long Col(long c) => mir ? TacticalScenarios.MirrorColumn(c) : c;
        long Xmm(long x) => mir ? W - x : x;
        Vec2L C(long col, long row) => TacticalScenarios.Cell(Col(col), row);
        Vec2L P(long xMm, long yMm) => new(Xmm(xMm), yMm);
        void Fill(long c0, long r0, long c1, long r1, TerrainClass k, long h = 0)
        {
            for (long r = r0; r <= r1; r++) for (long c = c0; c <= c1; c++) grid.Set(Col(c), r, k, h);
        }
        var HI = UnitProfiles.HeavyInfantry;
        var AR = UnitProfiles.Archers;
        var RF = DomainProfiles.Riflemen;
        const long A0 = 14, B0 = 33;
        Vec2L a = C(A0, 16), b = C(B0, 16);
        Vec2L def = P(DefenderX, MidY);
        Vec2L[] none = Array.Empty<Vec2L>();
        FormationSpec[] forms;
        Vec2L[] obj0, obj1;

        switch (id)
        {
            case PScenario.RiflemenVsInfantry:
                forms = new[] { new FormationSpec(1, 0, RF, 60, a), new FormationSpec(2, 1, HI, 50, b) };
                obj0 = new[] { b }; obj1 = new[] { a };
                break;
            case PScenario.SniperScreen:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 0, DomainProfiles.Sniper, 2, C(A0 - 2, 16)), new FormationSpec(3, 1, HI, 50, b) };
                obj0 = new[] { b }; obj1 = new[] { a };
                break;
            case PScenario.ShockInfantry:
                forms = new[] { new FormationSpec(1, 0, ShockInfantry, 50, a), new FormationSpec(2, 1, HI, 50, b) };
                obj0 = new[] { b }; obj1 = new[] { a };
                break;
            case PScenario.JungleAmbush:
                Fill(32, 0, 37, TacticalScenarios.FieldH - 1, TerrainClass.Jungle);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, C(34, 16)) { Posture = Posture.Ambush } };
                obj0 = new[] { C(42, 16) }; obj1 = none;
                break;
            case PScenario.HillCrest:
                Fill(32, 0, 47, TacticalScenarios.FieldH - 1, TerrainClass.Open, 10_000);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            case PScenario.FordCrossing:
                Fill(30, 0, 31, TacticalScenarios.FieldH - 1, TerrainClass.Water);
                Fill(30, 15, 31, 17, TerrainClass.Ford);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            case PScenario.MarshFront:
                Fill(26, 0, 31, TacticalScenarios.FieldH - 1, TerrainClass.Marsh);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            case PScenario.ForestHoldArchers:
                Fill(32, 10, 38, 22, TerrainClass.Forest);
                forms = new[] { new FormationSpec(1, 0, AR, 60, a), new FormationSpec(2, 1, HI, 50, C(33, 16)) { Posture = Posture.Defend } };
                obj0 = new[] { C(33, 16) }; obj1 = none;
                break;
            case PScenario.Defile:
                Fill(26, 0, 31, TacticalScenarios.FieldH - 1, TerrainClass.Wall);
                Fill(26, 16, 31, 16, TerrainClass.Open);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            case PScenario.HoldLineOpen:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            case PScenario.ArchersOnHill:
                Fill(32, 0, 47, TacticalScenarios.FieldH - 1, TerrainClass.Open, 10_000);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, AR, 60, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            // ---- held-out
            case PScenario.Numbers5To4:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 40, b) };
                obj0 = new[] { b }; obj1 = new[] { a };
                break;
            case PScenario.MixedConditions:
                forms = new[]
                {
                    new FormationSpec(1, 0, HI, 50, a) { Experience = 25, Leadership = 30 },
                    new FormationSpec(2, 1, HI, 50, b) { Supply = Q32.FromRatio(3, 4), Morale = 850 },
                };
                obj0 = new[] { b }; obj1 = new[] { a };
                break;
            case PScenario.MeetingAsymmetric:
            {
                Vec2L b34 = C(34, 16);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, b34) };
                obj0 = new[] { b34 }; obj1 = new[] { a };
                break;
            }
            case PScenario.CombinedArmsVsMass:
            {
                Vec2L h0 = C(A0, 16), r0 = C(A0 - 2, 16), m1 = C(B0, 14), m2 = C(B0, 18);
                forms = new[]
                {
                    new FormationSpec(1, 0, HI, 50, h0), new FormationSpec(2, 0, AR, 60, r0),
                    new FormationSpec(3, 1, HI, 50, m1), new FormationSpec(4, 1, HI, 50, m2),
                };
                obj0 = new[] { b }; obj1 = new[] { h0 };
                break;
            }
            case PScenario.RiflemenVsArchers:
                forms = new[] { new FormationSpec(1, 0, RF, 60, a), new FormationSpec(2, 1, AR, 60, b) };
                obj0 = new[] { b }; obj1 = new[] { a };
                break;
            case PScenario.ForestAmbushArchers:
                Fill(32, 0, 36, TacticalScenarios.FieldH - 1, TerrainClass.Forest);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, AR, 60, C(33, 16)) { Posture = Posture.Ambush } };
                obj0 = new[] { C(42, 16) }; obj1 = none;
                break;
            case PScenario.HighRidge:
                Fill(32, 0, 47, TacticalScenarios.FieldH - 1, TerrainClass.Open, 20_000);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            case PScenario.NarrowFordArchers:
                Fill(30, 0, 31, TacticalScenarios.FieldH - 1, TerrainClass.Water);
                Fill(30, 15, 31, 16, TerrainClass.Ford);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, AR, 60, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            case PScenario.UrbanStreet:
                Fill(24, 0, 31, TacticalScenarios.FieldH - 1, TerrainClass.Building);
                Fill(24, 16, 31, 16, TerrainClass.Road);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, def) { Posture = Posture.Defend } };
                obj0 = new[] { def }; obj1 = none;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
        var sides = new[]
        {
            new SideSpec(mir ? ExitEdge.East : ExitEdge.West) { Objectives = obj0 },
            new SideSpec(mir ? ExitEdge.West : ExitEdge.East) { Objectives = obj1 },
        };
        return new BattleSetup(grid, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), sides, forms, seed)
        { Deployment = DeploymentRule.Thirds };
    }
}
