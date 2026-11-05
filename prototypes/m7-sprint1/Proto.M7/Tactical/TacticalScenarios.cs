using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// Synthetic battle scenarios shared by tests and the bench. Field = 48 x 32 cells (the "~48×32" coarse grid of
/// docs/d011-battle-layer-addendum.md:35, D-012 recommendation — the grid itself is OPEN as O-7) of 20,000 mm
/// (cell size PLACEHOLDER: the charter's range bands are in "battle-grid cells of D-012's coarse grid"
/// (unit-catalogue…md:222) and no physical size is given anywhere). Side 0 exits West, side 1 exits East.
/// </summary>
public static class TacticalScenarios
{
    public const int FieldW = 48, FieldH = 32;
    /// <summary>Battle-grid cell, mm. PLACEHOLDER.</summary>
    public const long CellMm = 20_000;

    public static TerrainGrid OpenField() => new(FieldW, FieldH, CellMm, TerrainClass.Open);

    public static Vec2L Cell(long cx, long cy) => new(cx * CellMm + CellMm / 2, cy * CellMm + CellMm / 2);

    public static SideSpec[] DefaultSides(Vec2L[]? obj0 = null, Vec2L[]? obj1 = null) => new[]
    {
        new SideSpec(ExitEdge.West) { Objectives = obj0 ?? Array.Empty<Vec2L>() },
        new SideSpec(ExitEdge.East) { Objectives = obj1 ?? Array.Empty<Vec2L>() },
    };

    /// <summary>Column of the cell that mirrors column cx about the field's vertical centre line (x = FieldW*CellMm/2).</summary>
    public static long MirrorColumn(long cx) => FieldW - 1 - cx;

    /// <summary>
    /// A meeting engagement on open ground: one formation per side, 19 cells apart (beyond sight), each side's objective
    /// is the other's start point, so the knowledge-only AIs advance, detect, and attack. The two anchors are exact mirror
    /// images about the field centre (each 290,000 mm from its own exit edge). Verify-D F1: the earlier pair (14, 34)
    /// put side 1 20,000 mm closer to its exit than side 0, which accounted for most of the East/West bias of §9.
    /// [TR, verify-TR-r1 V10] STATUS: this mirrored geometry is the F1 CORRECTION, whose outcome-equalizing rationale DIR5
    /// §2.14 / D-079 DR32 supersedes. It is kept ONLY as the mirrored ENGINE-BIAS CONTROL (and because ~20 tests, the
    /// AutoResolver parity tests of lane P and the Slice D bench pin it); it is not the realistic scenario. The uncorrected
    /// geometry is <see cref="MeetingAsymmetric"/>. Re-pointing the default is an integrator / Director choice (H-TR P-TR6).
    /// </summary>
    public static BattleSetup Meeting(UnitProfile p0, int n0, UnitProfile p1, int n1, ulong seed, TacticalRules? rules = null)
    {
        Vec2L a0 = Cell(14, 16), a1 = Cell(MirrorColumn(14), 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { a1 }, new[] { a0 }),
            new[] { new FormationSpec(1, 0, p0, n0, a0), new FormationSpec(2, 1, p1, n1, a1) },
            seed) { Rules = rules ?? new TacticalRules() };
    }

    /// <summary>
    /// [TR] The meeting engagement with the ORIGINAL (pre-verify-D-F1) anchors Cell(14,16) and Cell(34,16): side 1 starts
    /// 20,000 mm closer to its exit edge (270,000 vs 290,000 mm). DIR5 §2.14 / D-079 DR32: this realistic-or-accidental
    /// asymmetry must NOT be corrected merely to equalize outcomes, so it is PRESERVED here as its own scenario (both anchors
    /// are inside their own deployment thirds, so the thirds rule accepts it unchanged — tested), while <see cref="Meeting"/>
    /// stays as the mirrored CONTROL. Per-side outcomes of both are reported (H-TR.md §3, D-079 D13 classification).
    /// </summary>
    public static BattleSetup MeetingAsymmetric(UnitProfile p0, int n0, UnitProfile p1, int n1, ulong seed, TacticalRules? rules = null)
    {
        Vec2L a0 = Cell(14, 16), a1 = Cell(34, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { a1 }, new[] { a0 }),
            new[] { new FormationSpec(1, 0, p0, n0, a0), new FormationSpec(2, 1, p1, n1, a1) },
            seed) { Rules = rules ?? new TacticalRules() };
    }

    /// <summary>Combined arms: per side heavy infantry (cap 50) in front and archers (cap 60) behind — 110 v 110.</summary>
    public static BattleSetup CombinedArms(ulong seed, TacticalRules? rules = null)
    {
        Vec2L h0 = Cell(14, 16), r0 = Cell(12, 16), h1 = Cell(34, 16), r1 = Cell(36, 16);
        return new BattleSetup(OpenField(), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            DefaultSides(new[] { h1 }, new[] { h0 }),
            new[]
            {
                new FormationSpec(1, 0, UnitProfiles.HeavyInfantry, 50, h0),
                new FormationSpec(2, 0, UnitProfiles.Archers, 60, r0),
                new FormationSpec(3, 1, UnitProfiles.HeavyInfantry, 50, h1),
                new FormationSpec(4, 1, UnitProfiles.Archers, 60, r1),
            },
            seed) { Rules = rules ?? new TacticalRules() };
    }
}
