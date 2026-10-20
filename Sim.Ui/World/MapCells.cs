using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.World;

/// <summary>
/// M5 map workstream P8 — PICKING A PLACE ON THE MAP: the terrain cell under a screen point, through the camera's SHOWN
/// view and the map rect (<see cref="Camera.ScreenToWorld(double, double, RectD)"/> — the transform the lens and the GPU
/// pass draw with, so the cell picked is the cell drawn under the pointer), and the screen region a cell is registered
/// under for Shift+click. World space is terrain px and a cell (x, y) covers [x, x+1) × [y, y+1) — a site cell's
/// settlement is drawn at its centre (x + 0.5, y + 0.5). Pure UI math: nothing here reads or writes the simulation.
///
/// WS3 (batch 3): on a wrapped camera (<see cref="Camera.WrapX"/>) the world point under the pointer may be any copy of the
/// world (x below 0 or past the size, either side of the seam): its column is floored and NORMALISED (integer modulo,
/// <see cref="Sim.Core.Pathing.MapTopology.WrapX(long, int)"/>), so the cell picked is the cell drawn under the pointer at
/// whatever copy it is drawn; y keeps its hard edges. A cell's footprint is placed at the copy nearest a reference x (the
/// pointer's world x for <see cref="Region"/>, else the view centre).
/// </summary>
public static class MapCells
{
    /// <summary>The smallest side, in screen px, of the region a cell is registered under: below it (a zoomed-out map
    /// shows a cell smaller than a pointer) the region is a square of this side centred on the pointer, so a click
    /// where the region's centre is lands on the same cell at any zoom.</summary>
    public const double MinRegionPx = 12.0;

    /// <summary>The terrain cell (y × size + x) under screen point (<paramref name="sx"/>, <paramref name="sy"/>), or −1
    /// when the point shows no part of the world.</summary>
    public static int At(Camera camera, RectD map, double sx, double sy, int terrainSize)
    {
        ArgumentNullException.ThrowIfNull(camera);
        (double wx, double wy) = camera.ScreenToWorld(sx, sy, map);
        if (!(wy >= 0.0) || wy >= terrainSize) return -1;
        if (camera.WrapX)
        {
            if (!double.IsFinite(wx)) return -1;
            double column = Math.Floor(wx);
            if (column < long.MinValue / 2.0 || column > long.MaxValue / 2.0) return -1;
            return (int)wy * terrainSize + Sim.Core.Pathing.MapTopology.WrapX((long)column, terrainSize);
        }
        if (!(wx >= 0.0) || wx >= terrainSize) return -1;
        return (int)wy * terrainSize + (int)wx;
    }

    /// <summary>The cell's screen footprint (its four corners through the camera). WS3: on a wrapped camera, at the cell's
    /// copy nearest <paramref name="nearWorldX"/> (world px; null: the view centre, <see cref="Camera.CenterX"/>).</summary>
    public static RectD Footprint(Camera camera, RectD map, int cell, int terrainSize, double? nearWorldX = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        int x = cell % terrainSize, y = cell / terrainSize;
        double left = x;
        if (camera.WrapX)
            left = Sim.Core.Pathing.MapTopology.NearestCopy(x + 0.5, nearWorldX ?? camera.CenterX, terrainSize) - 0.5;
        (double x0, double y0) = camera.WorldToScreen(left, y, map);
        (double x1, double y1) = camera.WorldToScreen(left + 1, y + 1, map);
        return new RectD(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>The region the cell under the pointer (<paramref name="px"/>, <paramref name="py"/>) is registered under:
    /// its footprint when that is at least <see cref="MinRegionPx"/> a side, else a <see cref="MinRegionPx"/> square
    /// centred on the pointer. Either way it contains the pointer, and its centre lies in the same cell.</summary>
    public static RectD Region(Camera camera, RectD map, int cell, int terrainSize, double px, double py)
    {
        ArgumentNullException.ThrowIfNull(camera);
        RectD f = Footprint(camera, map, cell, terrainSize, camera.WrapX ? camera.ScreenToWorld(px, py, map).X : null);
        if (f.W >= MinRegionPx && f.H >= MinRegionPx) return f;
        return new RectD(px - MinRegionPx / 2.0, py - MinRegionPx / 2.0, MinRegionPx, MinRegionPx);
    }
}
