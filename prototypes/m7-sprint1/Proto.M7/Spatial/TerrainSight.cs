using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>[TR] Result of a terrain-aware sight query. Clarity = product of concealment multipliers of the cells whose layer
/// the line is inside (Zero when not visible); Cover = product of missile transmissions over the same cells.</summary>
public readonly record struct SightResult(bool Visible, Q32 Clarity, Q32 Cover, LosBlock Block, CellCoord BlockCell);

/// <summary>[TR] Result of a terrain-aware fire-path check for a shot that geometrically struck an entity.</summary>
public readonly record struct FirePathResult(bool Blocked, Q32 Transmission, LosBlock Block, CellCoord BlockCell);

/// <summary>
/// [TR] CONTINUOUS line of sight and fire over the shared grid with ELEVATION, VEGETATION and STRUCTURE layers (DIR5 §2.7).
/// It walks the same exact supercover as Slice C (<see cref="Supercover"/>) and keeps every Slice C rule; the only
/// difference is the per-cell LAYER of <see cref="TerrainEffects"/>:
///
///   line height at walk parameter n/D: z(n) = z0 + floor((z1 - z0) * n / D), z0 = ground(observer cell) + eye,
///   z1 = ground(target cell) + target height (Slice C interpolation).
///   For each supercover cell c, in walk order:
///     off-grid -> blocked (OffGrid);  Wall -> blocked (Wall, infinitely tall);
///     c is neither the observer's nor the target's cell:
///        ground(c) &gt; min(z(TIn), z(TOut))                       -> blocked (Height)      [Slice C]
///        inside := min(z(TIn), z(TOut)) &lt; ground(c) + FeatureHeight(class c)   (the line passes through the layer)
///        inside and Opaque(class c)                              -> blocked (Structure)   [buildings occlude]
///        inside and not corner-side -> Clarity *= Concealment(class c); Cover *= MissileTransmission(class c)
///     c is the target's cell (not corner-side): Clarity *= Concealment(class c); Cover *= MissileTransmission(class c)
///        (the target stands INSIDE its own layer — seen from above, a unit under canopy is still under canopy).
///   The observer's own cell never attenuates (Slice C), unless it is also the target's cell.
/// Consequences (each tested): an observer on a hill whose line clears the canopy sees across a forest unattenuated; the
/// same line at eye level through the forest is attenuated cell by cell; a building blocks a line below its roof and not a
/// line above it; under <see cref="TerrainEffects.SliceCEquivalent"/> the query equals <see cref="LineOfSight.Query"/>.
/// </summary>
public static class TerrainSight
{
    private struct Visitor : ISupercoverVisitor
    {
        public TerrainGrid Grid;
        public ConcealmentTable Conceal;
        public TerrainEffects Fx;
        public CellCoord Obs, Tgt;
        public long Z0, Z1, D;
        public Q32 Clarity, Cover;
        public LosBlock Block;
        public CellCoord BlockCell;

        private long ZAt(long n) => Z0 + FixedMath.MulDivFloor(Z1 - Z0, n, D);

        public bool Visit(in CoverCell c)
        {
            if (!Grid.InBounds(c.X, c.Y)) { Block = LosBlock.OffGrid; BlockCell = new(c.X, c.Y); return false; }
            TerrainClass k = Grid.ClassAt(c.X, c.Y);
            if (k == TerrainClass.Wall) { Block = LosBlock.Wall; BlockCell = new(c.X, c.Y); return false; }
            bool isObs = c.X == Obs.X && c.Y == Obs.Y, isTgt = c.X == Tgt.X && c.Y == Tgt.Y;
            if (!isObs && !isTgt)
            {
                long ground = Grid.HeightAt(c.X, c.Y);
                long zMin = Math.Min(ZAt(c.TIn), ZAt(c.TOut));
                if (ground > zMin) { Block = LosBlock.Height; BlockCell = new(c.X, c.Y); return false; }
                if (Fx.InsideLayer(k, ground, zMin))
                {
                    if (Fx.Opaque(k)) { Block = LosBlock.Structure; BlockCell = new(c.X, c.Y); return false; }
                    if (!c.CornerSide) { Clarity = Clarity.Mul(Conceal.Multiplier(k)); Cover = Cover.Mul(Fx.MissileTransmission(k)); }
                }
                return true;
            }
            if (isTgt && !c.CornerSide) { Clarity = Clarity.Mul(Conceal.Multiplier(k)); Cover = Cover.Mul(Fx.MissileTransmission(k)); }
            return true;
        }
    }

    public static SightResult Query(TerrainGrid grid, ConcealmentTable conceal, TerrainEffects fx, Vec2L observer, long eyeHeightMm,
                                    Vec2L target, long targetHeightMm)
    {
        CellCoord o = grid.CellOf(observer), t = grid.CellOf(target);
        if (!grid.InBounds(o.X, o.Y)) return new SightResult(false, Q32.Zero, Q32.Zero, LosBlock.OffGrid, o);
        if (!grid.InBounds(t.X, t.Y)) return new SightResult(false, Q32.Zero, Q32.Zero, LosBlock.OffGrid, t);
        var v = new Visitor
        {
            Grid = grid, Conceal = conceal, Fx = fx, Obs = o, Tgt = t,
            Z0 = grid.HeightAt(o.X, o.Y) + eyeHeightMm,
            Z1 = grid.HeightAt(t.X, t.Y) + targetHeightMm,
            D = Supercover.Denominator(observer, target),
            Clarity = Q32.One, Cover = Q32.One,
        };
        bool done = Supercover.Walk(grid.CellSizeMm, observer, target, ref v);
        return done ? new SightResult(true, v.Clarity, v.Cover, LosBlock.None, default)
                    : new SightResult(false, Q32.Zero, Q32.Zero, v.Block, v.BlockCell);
    }

    private struct FireVisitor : ISupercoverVisitor
    {
        public TerrainGrid Grid;
        public TerrainEffects Fx;
        public CellCoord Shooter, Struck;
        public long Z0, Z1, D, HitNum, HitDen;
        public Q32 Transmission;
        public LosBlock Block;
        public CellCoord BlockCell;

        private long ZAt(long n) => Z0 + FixedMath.MulDivFloor(Z1 - Z0, n, D);

        public bool Visit(in CoverCell c)
        {
            // Only cells ENTERED STRICTLY BEFORE the hit parameter lie between the shooter and the struck entity.
            if ((Int128)c.TIn * HitDen >= (Int128)HitNum * D) return false;
            if (!Grid.InBounds(c.X, c.Y)) return true;                    // Slice C DirectFire already judged off-grid / Wall
            bool isShooter = c.X == Shooter.X && c.Y == Shooter.Y, isStruck = c.X == Struck.X && c.Y == Struck.Y;
            if (isShooter || isStruck || c.CornerSide) return true;
            TerrainClass k = Grid.ClassAt(c.X, c.Y);
            long ground = Grid.HeightAt(c.X, c.Y);
            long zMin = Math.Min(ZAt(c.TIn), ZAt(c.TOut));
            if (!Fx.InsideLayer(k, ground, zMin)) return true;
            if (Fx.Opaque(k)) { Block = LosBlock.Structure; BlockCell = new(c.X, c.Y); return false; }
            Transmission = Transmission.Mul(Fx.MissileTransmission(k));
            return true;
        }
    }

    /// <summary>
    /// The layer part of a direct-fire shot that Slice C <see cref="Projectiles.DirectFire"/> scored as a geometric HIT on
    /// the entity at <paramref name="struckPos"/> with segment parameter hitNum/hitDen along from-&gt;aim (heights exactly as
    /// DirectFire: muzzle above the shooter's ground, aim height above the aim cell's ground):
    ///   - an OPAQUE layer cell (not the shooter's or the struck entity's cell) entered strictly before the hit, with the
    ///     shot inside its layer, BLOCKS the shot (a building between shooter and target) — Slice C only knows Wall;
    ///   - otherwise Transmission = product of MissileTransmission over the non-corner layer cells entered strictly before
    ///     the hit whose layer the shot is inside, times MissileTransmission of the struck entity's own cell (it stands in
    ///     its layer, even when that is also the shooter's cell). The shooter's own cell never intercepts as an
    ///     intermediate cell.
    /// </summary>
    public static FirePathResult FirePath(TerrainGrid grid, TerrainEffects fx, Vec2L from, long muzzleHeightMm, Vec2L aim, long aimHeightMm,
                                          Vec2L struckPos, long hitNum, long hitDen)
    {
        if (hitDen <= 0 || hitNum < 0) throw new ArgumentOutOfRangeException(nameof(hitDen));
        CellCoord sc = grid.CellOf(from), ac = grid.CellOf(aim), kc = grid.CellOf(struckPos);
        long aimGround = grid.InBounds(ac.X, ac.Y) ? grid.HeightAt(ac.X, ac.Y) : 0;
        var v = new FireVisitor
        {
            Grid = grid, Fx = fx, Shooter = sc, Struck = kc,
            Z0 = grid.HeightAt(sc.X, sc.Y) + muzzleHeightMm, Z1 = aimGround + aimHeightMm,
            D = Supercover.Denominator(from, aim), HitNum = hitNum, HitDen = hitDen,
            Transmission = Q32.One,
        };
        Supercover.Walk(grid.CellSizeMm, from, aim, ref v);
        if (v.Block != LosBlock.None) return new FirePathResult(true, Q32.Zero, v.Block, v.BlockCell);
        Q32 t = v.Transmission;
        if (grid.InBounds(kc.X, kc.Y)) t = t.Mul(fx.MissileTransmission(grid.ClassAt(kc.X, kc.Y)));
        return new FirePathResult(false, t, LosBlock.None, default);
    }
}
