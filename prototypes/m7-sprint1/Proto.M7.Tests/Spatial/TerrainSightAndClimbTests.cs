using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.Spatial.SpatialTestSupport;

namespace Proto.M7.Tests.Spatial;

/// <summary>
/// [TR] Lane TR spatial terrain: continuous line of sight with ELEVATION, VEGETATION (canopy) and STRUCTURE layers, the
/// missile fire-path cover, and Naismith climb cost in the planner and the exit field. Expected values are derived in the
/// comments from the equations in TerrainSight / TerrainEffects / GridPathfinder, not captured from a run.
/// </summary>
public class TerrainSightAndClimbTests
{
    private static readonly TerrainEffects Fx = TerrainEffects.PlaceholderTestTable();
    private static readonly Q32 Half = Q32.FromRatio(1, 2), ThreeQ = Q32.FromRatio(3, 4);

    private static SightResult Sight(TerrainGrid g, Vec2L a, Vec2L b, long eye = 1700, long th = 1700, TerrainEffects? fx = null)
        => TerrainSight.Query(g, Conceal, fx ?? Fx, a, eye, b, th);

    // ------------------------------------------------------------------ equivalence with Slice C (control)

    /// <summary>Under the Slice-C-equivalent table the lane-TR query equals LineOfSight.Query exactly (visibility, clarity,
    /// block kind, block cell) on 600 random segments over random grids WITH random heights and every class incl. the new
    /// ones — so the new code changes nothing until a real layer table is used.</summary>
    [Fact]
    public void SliceCEquivalentTableReproducesLineOfSightExactly()
    {
        var fx = TerrainEffects.SliceCEquivalent();
        int checkedCount = 0, visible = 0, attenuated = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var g = new TerrainGrid(24, 18);
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    g.Set(x, y, (TerrainClass)R(seed, x, y, TerrainCostTable.ClassCount), R(seed + 50, x, y, 9) * 1_000);
            for (long k = 0; k < 100; k++)
            {
                var a = new Vec2L(R(seed + 100, k, 0, g.Width * g.CellSizeMm), R(seed + 100, k, 1, g.Height * g.CellSizeMm));
                var b = new Vec2L(R(seed + 100, k, 2, g.Width * g.CellSizeMm), R(seed + 100, k, 3, g.Height * g.CellSizeMm));
                long eye = R(seed + 100, k, 4, 12_000), th = R(seed + 100, k, 5, 12_000);
                var l = LineOfSight.Query(g, Conceal, a, eye, b, th);
                var t = TerrainSight.Query(g, Conceal, fx, a, eye, b, th);
                Assert.Equal(l.Visible, t.Visible);
                Assert.Equal(l.Clarity, t.Clarity);
                Assert.Equal(l.Block, t.Block);
                if (!l.Visible) Assert.Equal(l.BlockCell, t.BlockCell);
                checkedCount++;
                if (l.Visible) visible++;
                if (l.Visible && l.Clarity < Q32.One) attenuated++;
            }
        }
        Assert.Equal(600, checkedCount);
        Assert.True(visible > 30 && attenuated > 10, $"the sample must exercise visible and attenuated lines (visible {visible}, attenuated {attenuated})");
    }

    // ------------------------------------------------------------------ vegetation layer + elevation

    /// <summary>
    /// A 3-cell forest strip (x = 4..6) between an observer at x = 1 and a target at x = 9 on row 5 of a 20 m grid.
    /// Eye level on flat ground (line at 1.7 m &lt; 15 m canopy): every forest cell attenuates: clarity = (1/2)^3 = 1/8, cover
    /// (3/4)^3 = 27/64. Observer and target on 30 m hills (cells x &lt;= 2 and x &gt;= 8 at 30,000 mm): the line runs at 31.7 m,
    /// above the 15 m canopy: clarity 1 and cover 1. Forest raised onto a 20 m ridge (canopy top 35 m): the 31.7 m line is
    /// inside the layer again -> 1/8 (elevation and vegetation compose).
    /// </summary>
    [Fact]
    public void ForestAttenuatesBelowTheCanopyAndNotAboveIt()
    {
        var g = new TerrainGrid(12, 10, 20_000);
        for (int x = 4; x <= 6; x++) g.SetClass(x, 5, TerrainClass.Forest);
        Vec2L a = g.CellCenter(1, 5), b = g.CellCenter(9, 5);
        var low = Sight(g, a, b);
        Assert.True(low.Visible);
        Assert.Equal(Half.Mul(Half).Mul(Half), low.Clarity);
        Assert.Equal(ThreeQ.Mul(ThreeQ).Mul(ThreeQ), low.Cover);

        for (int x = 0; x <= 2; x++) g.Set(x, 5, TerrainClass.Hills, 30_000);
        for (int x = 8; x <= 11; x++) g.Set(x, 5, TerrainClass.Hills, 30_000);
        var high = Sight(g, a, b);
        Assert.True(high.Visible);
        Assert.Equal(Q32.One, high.Clarity);
        Assert.Equal(Q32.One, high.Cover);

        for (int x = 4; x <= 6; x++) g.Set(x, 5, TerrainClass.Forest, 20_000);
        var ridge = Sight(g, a, b);
        Assert.True(ridge.Visible);
        Assert.Equal(Half.Mul(Half).Mul(Half), ridge.Clarity);

        // Control (Slice C): its LineOfSight attenuates over the canopy too — the difference IS the layer rule.
        for (int x = 4; x <= 6; x++) g.Set(x, 5, TerrainClass.Forest, 0);
        Assert.Equal(Half.Mul(Half).Mul(Half), LineOfSight.Query(g, Conceal, a, 1700, b, 1700).Clarity);
    }

    /// <summary>The canopy boundary is exact: with the line at height z everywhere (eye = target height = z over flat ground),
    /// a forest cell attenuates iff z &lt; 0 + 15,000 (inside the layer): z = 14,999 -> 1/2; z = 15,000 -> 1.</summary>
    [Fact]
    public void CanopyBoundaryIsStrict()
    {
        var g = new TerrainGrid(10, 3, 20_000);
        g.SetClass(5, 1, TerrainClass.Forest);
        Vec2L a = g.CellCenter(1, 1), b = g.CellCenter(8, 1);
        Assert.Equal(Half, Sight(g, a, b, 14_999, 14_999).Clarity);
        Assert.Equal(Q32.One, Sight(g, a, b, 15_000, 15_000).Clarity);
    }

    /// <summary>A unit standing IN a forest is concealed even from an observer above the canopy: the target's own cell
    /// always applies (Jungle 1/4 here), while the open cells in between do not.</summary>
    [Fact]
    public void TargetUnderCanopyStaysConcealedFromAbove()
    {
        var g = new TerrainGrid(12, 3, 20_000);
        g.Set(1, 1, TerrainClass.Hills, 60_000);
        g.SetClass(9, 1, TerrainClass.Jungle);
        var r = Sight(g, g.CellCenter(1, 1), g.CellCenter(9, 1));
        Assert.True(r.Visible);
        Assert.Equal(Q32.FromRatio(1, 4), r.Clarity);
        Assert.Equal(Half, r.Cover);                                   // jungle missile transmission 1/2 on the target's cell
    }

    // ------------------------------------------------------------------ structures

    /// <summary>
    /// A building (8 m) between observer and target on flat ground blocks the eye-level line (Structure, at the building's
    /// cell). An observer on a 20 m hill sees over it (line from 21.7 m down to 1.7 m crosses the building cell well above 8 m);
    /// lowered to a 5 m knoll the line is below the roof inside the cell and is blocked again. Slice C would see through
    /// (control): its LineOfSight knows only Wall.
    /// </summary>
    [Fact]
    public void BuildingBlocksBelowItsRoofAndNotAbove()
    {
        var g = new TerrainGrid(12, 3, 20_000);
        g.SetClass(5, 1, TerrainClass.Building);
        Vec2L a = g.CellCenter(1, 1), b = g.CellCenter(8, 1);
        var r = Sight(g, a, b);
        Assert.False(r.Visible);
        Assert.Equal(LosBlock.Structure, r.Block);
        Assert.Equal(new CellCoord(5, 1), r.BlockCell);
        Assert.True(LineOfSight.Query(g, Conceal, a, 1700, b, 1700).Visible);           // control: Slice C has no structures

        g.Set(1, 1, TerrainClass.Hills, 20_000);
        // Line z(x) from 21,700 at x = 30,000 to 1,700 at x = 170,000; building cell spans x in [100,000, 120,000):
        // z leaves the cell at 21,700 + floor(-20,000 * 90/140) = 8,842 > 8,000 -> above the roof everywhere in the cell.
        Assert.True(Sight(g, a, b).Visible);
        g.Set(1, 1, TerrainClass.Hills, 5_000);
        // z at the cell exit: 6,700 + floor(-5,000 * 90/140) = 3,485 < 8,000 -> inside the structure layer -> blocked.
        Assert.Equal(LosBlock.Structure, Sight(g, a, b).Block);
    }

    // ------------------------------------------------------------------ fire path (cover)

    /// <summary>
    /// Fire path of a shot from x = 1 at a target at x = 8 (row 1, 20 m cells, muzzle 1.5 m, aim 1.0 m), the target's disc
    /// struck at t = (140,000 - 300)/140,000:
    ///  - two forest cells between (x = 4, 5) and the target in open ground: transmission (3/4)^2 = 9/16;
    ///  - the target in forest too: (3/4)^3;
    ///  - a building between instead: Blocked (Structure);
    ///  - the shooter's own forest cell never intercepts (only as the struck entity's cell would it count).
    /// </summary>
    [Fact]
    public void FirePathCoverAndStructureBlocking()
    {
        var g = new TerrainGrid(12, 3, 20_000);
        Vec2L from = g.CellCenter(1, 1), aim = g.CellCenter(8, 1);
        var disc = new[] { new Disc(7, aim, 300) };
        var hit = Projectiles.DirectFire(g, from, 1500, aim, 1000, disc, 1);
        Assert.Equal(FireOutcome.Hit, hit.Outcome);
        FirePathResult FP() => TerrainSight.FirePath(g, Fx, from, 1500, aim, 1000, aim, hit.TNum, hit.TDen);

        Assert.Equal(Q32.One, FP().Transmission);
        g.SetClass(4, 1, TerrainClass.Forest); g.SetClass(5, 1, TerrainClass.Forest);
        Assert.Equal(ThreeQ.Mul(ThreeQ), FP().Transmission);
        g.SetClass(8, 1, TerrainClass.Forest);
        Assert.Equal(ThreeQ.Mul(ThreeQ).Mul(ThreeQ), FP().Transmission);
        g.SetClass(1, 1, TerrainClass.Forest);                                      // shooter's own cell: no extra factor
        Assert.Equal(ThreeQ.Mul(ThreeQ).Mul(ThreeQ), FP().Transmission);
        g.SetClass(4, 1, TerrainClass.Building);
        var blocked = FP();
        Assert.True(blocked.Blocked);
        Assert.Equal(LosBlock.Structure, blocked.Block);
        Assert.Equal(new CellCoord(4, 1), blocked.BlockCell);
    }

    // ------------------------------------------------------------------ Naismith climb

    /// <summary>Naismith (1892): 600 m of ascent costs as much as 5 km on the flat. ClimbCost(600,000 mm) in the A* unit
    /// (cost x1000 per mm) = 5,000,000 mm x 1000; descent is free; the per-mm form spreads the same cost over the run.</summary>
    [Fact]
    public void NaismithClimbCostIsExact()
    {
        Assert.Equal(5_000_000L * 1000, Fx.ClimbCost(600_000));
        Assert.Equal(0, Fx.ClimbCost(-600_000));
        Assert.Equal(0, Fx.ClimbCost(0));
        Assert.Equal(25 * 1000 / 3, Fx.ClimbCost(1));                              // floor(25000/3) = 8333
        // 5 m over a 20 m run: floor(5000 * 25000 / (3 * 20000)) = floor(2083.33) = 2083 per mm of horizontal travel.
        Assert.Equal(2_083, Fx.ClimbCostPerMm(5_000, 20_000));
        Assert.Equal(0, Fx.ClimbCostPerMm(-5_000, 20_000));
        Assert.Equal(0, TerrainEffects.SliceCEquivalent().ClimbCost(600_000));
    }

    /// <summary>
    /// A* with climb: a straight 9-cell route over a 10 m plateau (cells x = 3..5) climbs 10 m once (descent free):
    /// cost = 8 orthogonal open steps (8 * 1000 * S) + ClimbCost(10,000) = 8 * 4,000,000 + 83,333,333 (S = 4,000 mm), while a
    /// full-height wall of plateau forces it; with a gap row it detours around when that is cheaper. Without the climb table
    /// the cost is exactly Slice C's (heights ignored). The climb-aware cost equals an independent Dijkstra with the same rule.
    /// </summary>
    [Fact]
    public void PlannerPaysTheClimbAndDetoursWhenCheaper()
    {
        var g = new TerrainGrid(9, 5);                       // S = 4,000 mm
        for (int y = 0; y < 5; y++) for (int x = 3; x <= 5; x++) g.Set(x, y, TerrainClass.Open, 10_000);
        var plain = new GridPathfinder(g, Costs);
        var climb = new GridPathfinder(g, Costs, default, Fx);
        CellCoord s = new(0, 2), t = new(8, 2);
        long S = g.CellSizeMm;
        Assert.Equal(8 * 1000 * S, plain.FindPath(s, t).Cost);
        var over = climb.FindPath(s, t);
        Assert.Equal(8 * 1000 * S + Fx.ClimbCost(10_000), over.Cost);
        Assert.Equal(8 * 1000 * S + 83_333_333, over.Cost);

        // Open a detour: row 4 of the plateau back at ground level. Going around costs 2 extra diagonals' worth at most,
        // far below the 83 M climb, so the planner now routes through row 4 and pays no climb.
        for (int x = 3; x <= 5; x++) g.Set(x, 4, TerrainClass.Open, 0);
        climb.RefreshCosts();
        var around = climb.FindPath(s, t);
        Assert.True(around.Cost < 8 * 1000 * S + Fx.ClimbCost(10_000));
        foreach (int c in around.Cells) Assert.Equal(0, g.HeightAtIndex(c) > 0 ? 1 : 0);
        Assert.Equal(ClimbDijkstra(g, s, t), around.Cost);
        g.Set(4, 4, TerrainClass.Open, 10_000);              // close it again: back over the top
        climb.RefreshCosts();
        Assert.Equal(ClimbDijkstra(g, s, t), climb.FindPath(s, t).Cost);
    }

    /// <summary>Fleeing uphill is slow: the exit field to the West edge from behind a 10 m ridge costs the climb once more
    /// than the same field on the flat; walking DOWN to the edge is free of climb.</summary>
    [Fact]
    public void ExitFieldPaysClimbTowardTheEdgeOnly()
    {
        var g = new TerrainGrid(8, 3);
        long S = g.CellSizeMm;
        for (int y = 0; y < 3; y++) g.Set(2, y, TerrainClass.Open, 10_000);   // ridge column x = 2
        var flat = new EdgeField(g, Costs, ExitEdge.West);
        var hill = new EdgeField(g, Costs, ExitEdge.West, Fx);
        int c = g.Index(5, 1);
        Assert.Equal(5 * 1000 * S, flat.DistanceAt(c));
        Assert.Equal(5 * 1000 * S + Fx.ClimbCost(10_000), hill.DistanceAt(c));   // up onto the ridge once, down for free
        int top = g.Index(2, 1);
        Assert.Equal(2 * 1000 * S, hill.DistanceAt(top));                      // from the ridge top: all downhill
    }

    /// <summary>Independent O(N^2) Dijkstra with Slice C step costs plus ascent-only Naismith climb.</summary>
    private static long ClimbDijkstra(TerrainGrid g, CellCoord s, CellCoord t)
    {
        var costs = Costs;
        int n = g.CellCount, W = g.Width;
        var dist = new long[n]; var done = new bool[n];
        Array.Fill(dist, long.MaxValue);
        int si = g.Index(s.X, s.Y), ti = g.Index(t.X, t.Y);
        dist[si] = 0;
        long S = g.CellSizeMm;
        while (true)
        {
            int u = -1;
            for (int i = 0; i < n; i++) if (!done[i] && dist[i] != long.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0) return -1;
            if (u == ti) return dist[u];
            done[u] = true;
            int x = u % W, y = u / W;
            for (int ddy = -1; ddy <= 1; ddy++)
                for (int ddx = -1; ddx <= 1; ddx++)
                {
                    if (ddx == 0 && ddy == 0) continue;
                    int nx = x + ddx, ny = y + ddy;
                    if (!g.IsPassable(costs, nx, ny)) continue;
                    long ca = costs.Cost(g.ClassAt(x, y)), cb = costs.Cost(g.ClassAt(nx, ny));
                    long step;
                    if (ddx != 0 && ddy != 0)
                    {
                        if (!g.IsPassable(costs, nx, y) || !g.IsPassable(costs, x, ny)) continue;
                        step = (ca + cb) * S * 1414 / 2000;
                    }
                    else step = (ca + cb) * S / 2;
                    step += Fx.ClimbCost(g.HeightAt(nx, ny) - g.HeightAt(x, y));
                    int v = ny * W + nx;
                    if (dist[u] + step < dist[v]) dist[v] = dist[u] + step;
                }
        }
    }

    // ------------------------------------------------------------------ table validation

    [Fact]
    public void EffectsTableValidationRefusesNonIdentityOpenGroundAndZeroCoefficients()
    {
        int n = TerrainCostTable.ClassCount;
        var fh = new long[n]; var op = new bool[n]; var one = new Q32[n];
        Array.Fill(one, Q32.One);
        Assert.Null(TerrainEffects.Validate(fh, op, one, one, one, 25, 3, Half, Half, Q32.One, Half));
        var bad = (Q32[])one.Clone(); bad[(int)TerrainClass.Open] = Half;
        Assert.Contains("identity", TerrainEffects.Validate(fh, op, bad, one, one, 25, 3, Half, Half, Q32.One, Half));
        var zero = (Q32[])one.Clone(); zero[(int)TerrainClass.Forest] = Q32.Zero;
        Assert.Contains("transmission", TerrainEffects.Validate(fh, op, zero, one, one, 25, 3, Half, Half, Q32.One, Half));
        Assert.Contains("footing", TerrainEffects.Validate(fh, op, one, zero, one, 25, 3, Half, Half, Q32.One, Half));
        Assert.Contains("order cap", TerrainEffects.Validate(fh, op, one, one, zero, 25, 3, Half, Half, Q32.One, Half));
        // beta * cap must stay below 1 (else 1 - beta*g reaches 0 and an uphill attack could never hit).
        Assert.Contains("gradient cap", TerrainEffects.Validate(fh, op, one, one, one, 25, 3, Q32.One, Half, Q32.One, Half));
        var opq = (bool[])op.Clone(); opq[(int)TerrainClass.Road] = true;
        Assert.Contains("identity", TerrainEffects.Validate(fh, opq, one, one, one, 25, 3, Half, Half, Q32.One, Half));
        Assert.Contains("climb", TerrainEffects.Validate(fh, op, one, one, one, -1, 3, Half, Half, Q32.One, Half));
    }
}
