using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>Stable ids of the parity-battery scenarios (spec §5). Never renumber (they key reports and goldens).</summary>
public enum ParityScenarioId : byte
{
    EqualInfantry = 1,
    Numbers2To1 = 2,
    ArchersVsInfantryOpen = 3,
    ArchersVsInfantryForest = 4,
    LowSupply = 5,
    LowMorale = 6,
    LeadershipEdge = 7,
    Ambush = 8,
    ExperienceEdge = 9,
    CombinedArms = 10,
}

/// <summary>One battery scenario: role A is always side 0, role B side 1.</summary>
public sealed record ParityScenario(ParityScenarioId Id, string Name, string RoleA, string RoleB, string Description);

/// <summary>
/// The repeatable scenario battery of Slice E (spec §5: "≥ 8 scenarios (equal infantry; 2:1 numbers; archers vs infantry open;
/// archers vs infantry with defender in forest; low supply; low morale; leadership edge; ambush/concealment)"; two more are
/// added because the AutoResolver consumes them as factors: experience edge and combined arms (composition)).
///
/// Field: <see cref="TacticalScenarios"/> 48 x 32 open field, 20,000 mm cells (PLACEHOLDER, Slice D). Role A = side 0,
/// role B = side 1. ORIENTATION: in the as-built orientation side 0 starts west and exits West; in the MIRRORED orientation
/// every column x becomes FieldW-1-x (anchors, objectives and terrain) and the exits swap. Slice D §9/§11.3 records a residual
/// East/West bias of the tactical engine whose cause is not isolated, so the battery runs every seed in BOTH orientations and
/// reports per ROLE, never per compass side.
///
/// Every numeric scenario input is a TEST INPUT chosen to exercise one factor; none is a gameplay value. Formation sizes are
/// within the PROPOSED caps (heavy infantry 50, archers 60: docs/m7-spec.md:395-396).
/// </summary>
public static class ParityScenarios
{
    public static IReadOnlyList<ParityScenario> All { get; } = new[]
    {
        new ParityScenario(ParityScenarioId.EqualInfantry, "equal-infantry", "HI 50", "HI 50",
            "heavy infantry 50 v 50 on open ground; meeting engagement (both sides advance on the other's start)"),
        new ParityScenario(ParityScenarioId.Numbers2To1, "numbers-2to1", "HI 50", "HI 25",
            "heavy infantry 50 v 25 on open ground; meeting engagement"),
        new ParityScenario(ParityScenarioId.ArchersVsInfantryOpen, "archers-vs-infantry-open", "archers 60", "HI 50",
            "archers 60 v heavy infantry 50 on open ground; meeting engagement"),
        new ParityScenario(ParityScenarioId.ArchersVsInfantryForest, "archers-vs-infantry-forest", "archers 60", "HI 50 (forest)",
            "archers 60 advance on heavy infantry 50 holding inside a forest block (defender has no objective, holds until it knows an enemy)"),
        new ParityScenario(ParityScenarioId.LowSupply, "low-supply", "HI 50 supply 1/4", "HI 50",
            "heavy infantry 50 v 50, role A at supply 1/4 (attack x 5/8 via ConditionMath)"),
        new ParityScenario(ParityScenarioId.LowMorale, "low-morale", "HI 50 morale 550", "HI 50",
            "heavy infantry 50 v 50, role A starts at morale 550 of 1000"),
        new ParityScenario(ParityScenarioId.LeadershipEdge, "leadership-edge", "HI 50 leadership 50", "HI 50",
            "heavy infantry 50 v 50, role A leadership 50 (morale loss per casualty x 100/150)"),
        new ParityScenario(ParityScenarioId.Ambush, "ambush", "HI 50 (advancing)", "HI 50 ambush (jungle)",
            "role A advances through a full-height jungle belt where role B waits in Ambush posture"),
        new ParityScenario(ParityScenarioId.ExperienceEdge, "experience-edge", "HI 50 experience 50", "HI 50",
            "heavy infantry 50 v 50, role A experience 50 (melee/defence x 1.5 via ConditionMath)"),
        new ParityScenario(ParityScenarioId.CombinedArms, "combined-arms", "HI 50 + archers 60", "HI 50 + archers 60",
            "combined arms 110 v 110, anchors exact mirror images (the Slice D CombinedArms asymmetry, D-tactical.md §11.4, is not used)"),
    };

    public static ParityScenario Get(ParityScenarioId id)
    {
        foreach (var s in All) if (s.Id == id) return s;
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    /// <summary>Builds the battle setup for one (scenario, seed, orientation).</summary>
    public static BattleSetup Build(ParityScenarioId id, ulong seed, bool mirrored, TacticalRules? rules = null)
    {
        var grid = TacticalScenarios.OpenField();
        long X(long col) => mirrored ? TacticalScenarios.MirrorColumn(col) : col;
        Vec2L C(long col, long row) => TacticalScenarios.Cell(X(col), row);
        void Fill(long c0, long r0, long c1, long r1, TerrainClass k)
        {
            for (long r = r0; r <= r1; r++) for (long c = c0; c <= c1; c++) grid.Set(X(c), r, k);
        }
        SideSpec[] Sides(Vec2L[] obj0, Vec2L[] obj1) => new[]
        {
            new SideSpec(mirrored ? ExitEdge.East : ExitEdge.West) { Objectives = obj0 },
            new SideSpec(mirrored ? ExitEdge.West : ExitEdge.East) { Objectives = obj1 },
        };
        var HI = UnitProfiles.HeavyInfantry;
        var AR = UnitProfiles.Archers;
        const long A0 = 14, B0 = 33;                       // B0 = MirrorColumn(A0): mirror-image start columns
        Vec2L a = C(A0, 16), b = C(B0, 16);
        Vec2L[] none = Array.Empty<Vec2L>();
        FormationSpec[] forms;
        SideSpec[] sides;
        switch (id)
        {
            case ParityScenarioId.EqualInfantry:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, b) };
                sides = Sides(new[] { b }, new[] { a });
                break;
            case ParityScenarioId.Numbers2To1:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 25, b) };
                sides = Sides(new[] { b }, new[] { a });
                break;
            case ParityScenarioId.ArchersVsInfantryOpen:
                forms = new[] { new FormationSpec(1, 0, AR, 60, a), new FormationSpec(2, 1, HI, 50, b) };
                sides = Sides(new[] { b }, new[] { a });
                break;
            case ParityScenarioId.ArchersVsInfantryForest:
                Fill(30, 12, 36, 20, TerrainClass.Forest);          // forest block around the defender (TEST INPUT geometry)
                forms = new[] { new FormationSpec(1, 0, AR, 60, a), new FormationSpec(2, 1, HI, 50, b) };
                sides = Sides(new[] { b }, none);                    // defender: no objective -> holds until it knows an enemy
                break;
            case ParityScenarioId.LowSupply:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a) { Supply = Q32.FromRatio(1, 4) }, new FormationSpec(2, 1, HI, 50, b) };
                sides = Sides(new[] { b }, new[] { a });
                break;
            case ParityScenarioId.LowMorale:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a) { Morale = 550 }, new FormationSpec(2, 1, HI, 50, b) };
                sides = Sides(new[] { b }, new[] { a });
                break;
            case ParityScenarioId.LeadershipEdge:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a) { Leadership = 50 }, new FormationSpec(2, 1, HI, 50, b) };
                sides = Sides(new[] { b }, new[] { a });
                break;
            case ParityScenarioId.Ambush:
            {
                Fill(23, 0, 29, TacticalScenarios.FieldH - 1, TerrainClass.Jungle);   // full-height belt: no detour (TEST INPUT)
                Vec2L amb = C(27, 16), goal = C(40, 16);
                forms = new[] { new FormationSpec(1, 0, HI, 50, a), new FormationSpec(2, 1, HI, 50, amb) { Posture = Posture.Ambush } };
                sides = Sides(new[] { goal }, none);
                break;
            }
            case ParityScenarioId.ExperienceEdge:
                forms = new[] { new FormationSpec(1, 0, HI, 50, a) { Experience = 50 }, new FormationSpec(2, 1, HI, 50, b) };
                sides = Sides(new[] { b }, new[] { a });
                break;
            case ParityScenarioId.CombinedArms:
            {
                Vec2L h0 = C(A0, 16), r0 = C(A0 - 2, 16), h1 = C(B0, 16), r1 = C(B0 + 2, 16);
                forms = new[]
                {
                    new FormationSpec(1, 0, HI, 50, h0), new FormationSpec(2, 0, AR, 60, r0),
                    new FormationSpec(3, 1, HI, 50, h1), new FormationSpec(4, 1, AR, 60, r1),
                };
                sides = Sides(new[] { h1 }, new[] { h0 });
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
        return new BattleSetup(grid, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), sides, forms, seed)
        { Rules = rules ?? new TacticalRules() };
    }
}
