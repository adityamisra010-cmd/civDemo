using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>Why a line is blocked. [TR] Structure = an opaque terrain layer (Building) the line passes through (lane TR,
/// <see cref="TerrainSight"/>; Slice C queries never return it).</summary>
public enum LosBlock : byte { None = 0, Wall = 1, Height = 2, OffGrid = 3, Structure = 4 }

/// <summary>Visible = no wall/height/off-grid block; Clarity = product of concealment multipliers (Zero when not visible).</summary>
public readonly record struct LosResult(bool Visible, Q32 Clarity, LosBlock Block, CellCoord BlockCell);

/// <summary>
/// Line of sight over the shared grid (spec §3).
/// Walks the supercover from observer to target (so the same cell set as movement/fire), and:
/// - any off-grid cell or Wall cell blocks (Wall = infinitely tall);
/// - HEIGHT: the sight line runs from z0 = ground(observer cell) + eye to z1 = ground(target cell) + target height; its
///   height at parameter n/D is z0 + floor((z1 - z0) * n / D) (integer interpolation). A cell other than the observer's and
///   the target's blocks iff its flat top is above the line somewhere inside it: HeightMm &gt; min(z(TIn), z(TOut)).
///   (The endpoint cells are excluded: the observer and target stand ON them.) Corner-side cells are touched at one point.
/// - CONCEALMENT: every membership cell except the observer's own cell multiplies Clarity by its concealment multiplier
///   (the target's cell always counts, even when it is also the observer's). Corner-side cells (zero length) do not.
/// </summary>
public static class LineOfSight
{
    private struct Visitor : ISupercoverVisitor
    {
        public TerrainGrid Grid;
        public ConcealmentTable Conceal;
        public CellCoord Obs, Tgt;
        public long Z0, Z1, D;
        public Q32 Clarity;
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
                long top = Grid.HeightAt(c.X, c.Y);
                if (top > Math.Min(ZAt(c.TIn), ZAt(c.TOut))) { Block = LosBlock.Height; BlockCell = new(c.X, c.Y); return false; }
            }
            if (!c.CornerSide && (!isObs || isTgt)) Clarity = Clarity.Mul(Conceal.Multiplier(k));
            return true;
        }
    }

    public static LosResult Query(TerrainGrid grid, ConcealmentTable conceal, Vec2L observer, long eyeHeightMm, Vec2L target, long targetHeightMm)
    {
        CellCoord o = grid.CellOf(observer), t = grid.CellOf(target);
        if (!grid.InBounds(o.X, o.Y)) return new LosResult(false, Q32.Zero, LosBlock.OffGrid, o);
        if (!grid.InBounds(t.X, t.Y)) return new LosResult(false, Q32.Zero, LosBlock.OffGrid, t);
        var v = new Visitor
        {
            Grid = grid, Conceal = conceal, Obs = o, Tgt = t,
            Z0 = grid.HeightAt(o.X, o.Y) + eyeHeightMm,
            Z1 = grid.HeightAt(t.X, t.Y) + targetHeightMm,
            D = Supercover.Denominator(observer, target),
            Clarity = Q32.One,
        };
        bool done = Supercover.Walk(grid.CellSizeMm, observer, target, ref v);
        return done ? new LosResult(true, v.Clarity, LosBlock.None, default) : new LosResult(false, Q32.Zero, v.Block, v.BlockCell);
    }
}
