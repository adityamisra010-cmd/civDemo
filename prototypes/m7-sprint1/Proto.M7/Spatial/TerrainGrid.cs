using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>
/// The ONE grid that serves both the grid planner (A*) and the continuous queries (supercover, LOS, fire, movement time)
/// — spec §3 "Grid&lt;-&gt;continuous consistency". Cell (cx, cy) covers the HALF-OPEN square
/// [cx*S, (cx+1)*S) x [cy*S, (cy+1)*S) in mm, S = CellSizeMm; the grid occupies cells 0..Width-1 x 0..Height-1, i.e. the
/// rectangle [0, Width*S) x [0, Height*S). Off-grid cells are impassable, block LOS and stop projectiles ("leaves the field").
/// Per cell: TerrainClass (byte) and HeightMm (long, ground height of the flat-topped cell).
/// </summary>
public sealed class TerrainGrid
{
    public const long DefaultCellSizeMm = 4_000;
    /// <summary>Upper bound on the cell size (keeps every step-cost product in long; 1 km cells are far beyond tactical needs).</summary>
    public const long MaxCellSizeMm = 1_000_000;
    /// <summary>Height bound: |HeightMm| &lt;= 10^9 mm (keeps line-height interpolation inside long).</summary>
    public const long MaxAbsHeightMm = 1_000_000_000;

    /// <summary>Grid extent bound per axis: 5*10^8 mm (half of Vec2L.MaxCoordMm).</summary>
    public const long MaxExtentMm = Vec2L.MaxCoordMm / 2;

    public int Width { get; }
    public int Height { get; }
    public long CellSizeMm { get; }
    public int CellCount => Width * Height;

    private readonly byte[] _class;
    private readonly long[] _height;

    /// <param name="cellSizeMm">Positive and EVEN, so a cell centre (cx*S + S/2) is an exact integer and never lies on a grid line.</param>
    public TerrainGrid(int width, int height, long cellSizeMm = DefaultCellSizeMm, TerrainClass fill = TerrainClass.Open)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "grid must be non-empty");
        if (cellSizeMm < 2 || cellSizeMm > MaxCellSizeMm || cellSizeMm % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(cellSizeMm), "cell size must be even and in [2, 10^6] mm");
        // The whole grid must lie inside HALF the documented coordinate range, so a query point one bounded step outside the
        // grid (e.g. a ballistic step, |v| <= 10^7 mm/s for <= 1 s) is still inside the range the exact arithmetic is proven for.
        if ((Int128)width * cellSizeMm > MaxExtentMm || (Int128)height * cellSizeMm > MaxExtentMm)
            throw new ArgumentOutOfRangeException(nameof(width), "grid extent exceeds MaxExtentMm (half the documented coordinate range)");
        Width = width; Height = height; CellSizeMm = cellSizeMm;
        _class = new byte[checked(width * height)];
        _height = new long[width * height];
        if (fill != 0) Array.Fill(_class, (byte)fill);
    }

    public bool InBounds(long cx, long cy) => cx >= 0 && cy >= 0 && cx < Width && cy < Height;
    /// <summary>Linear index of an IN-BOUNDS cell; throws for an off-grid cell (verify-C C-V-8: no silent aliasing). The
    /// public accessors ClassAt/HeightAt go through it, so they throw too; hot loops use the *AtIndex forms.</summary>
    public int Index(long cx, long cy)
    {
        if (!InBounds(cx, cy)) throw new ArgumentOutOfRangeException(nameof(cx), "cell is off the grid");
        return (int)(cy * Width + cx);
    }
    public int IndexX(int index) => index % Width;
    public int IndexY(int index) => index / Width;

    public TerrainClass ClassAt(long cx, long cy) => (TerrainClass)_class[Index(cx, cy)];
    public TerrainClass ClassAtIndex(int i) => (TerrainClass)_class[i];
    public long HeightAt(long cx, long cy) => _height[Index(cx, cy)];
    public long HeightAtIndex(int i) => _height[i];

    public void Set(long cx, long cy, TerrainClass c, long heightMm = 0)
    {
        if (!InBounds(cx, cy)) throw new ArgumentOutOfRangeException(nameof(cx));
        if (heightMm < -MaxAbsHeightMm || heightMm > MaxAbsHeightMm) throw new ArgumentOutOfRangeException(nameof(heightMm));
        if ((byte)c >= TerrainCostTable.ClassCount) throw new ArgumentOutOfRangeException(nameof(c));
        int i = Index(cx, cy);
        _class[i] = (byte)c;
        _height[i] = heightMm;
    }

    public void SetClass(long cx, long cy, TerrainClass c) => Set(cx, cy, c, HeightAt(cx, cy));

    /// <summary>Cell containing p: floor division on each axis (negative-safe; spec §3).</summary>
    public CellCoord CellOf(Vec2L p) => CellOf(p, CellSizeMm);

    public static CellCoord CellOf(Vec2L p, long cellSizeMm) => new(FixedMath.DivFloor(p.X, cellSizeMm), FixedMath.DivFloor(p.Y, cellSizeMm));

    /// <summary>Exact centre of a cell (S is even).</summary>
    public Vec2L CellCenter(long cx, long cy) => new(cx * CellSizeMm + CellSizeMm / 2, cy * CellSizeMm + CellSizeMm / 2);

    /// <summary>Passable for land movement: in bounds and the class has a non-zero cost.</summary>
    public bool IsPassable(TerrainCostTable costs, long cx, long cy) => InBounds(cx, cy) && costs.IsPassable(ClassAt(cx, cy));
}
