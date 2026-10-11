using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.World;

/// <summary>
/// M5 map workstream P8 — PICKING A PLACE ON THE MAP: the terrain cell under a screen point, through the camera's SHOWN
/// view and the map rect (<see cref="Camera.ScreenToWorld(double, double, RectD)"/> — the transform the lens and the GPU
/// pass draw with, so the cell picked is the cell drawn under the pointer), and the screen region a cell is registered
/// under for Shift+click. World space is terrain px and a cell (x, y) covers [x, x+1) × [y, y+1) — a site cell's
/// settlement is drawn at its centre (x + 0.5, y + 0.5). Pure UI math: nothing here reads or writes the simulation.
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
        if (!(wx >= 0.0) || !(wy >= 0.0) || wx >= terrainSize || wy >= terrainSize) return -1;
        return (int)wy * terrainSize + (int)wx;
    }

    /// <summary>The cell's screen footprint (its four corners through the camera).</summary>
    public static RectD Footprint(Camera camera, RectD map, int cell, int terrainSize)
    {
        ArgumentNullException.ThrowIfNull(camera);
        int x = cell % terrainSize, y = cell / terrainSize;
        (double x0, double y0) = camera.WorldToScreen(x, y, map);
        (double x1, double y1) = camera.WorldToScreen(x + 1, y + 1, map);
        return new RectD(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>The region the cell under the pointer (<paramref name="px"/>, <paramref name="py"/>) is registered under:
    /// its footprint when that is at least <see cref="MinRegionPx"/> a side, else a <see cref="MinRegionPx"/> square
    /// centred on the pointer. Either way it contains the pointer, and its centre lies in the same cell.</summary>
    public static RectD Region(Camera camera, RectD map, int cell, int terrainSize, double px, double py)
    {
        RectD f = Footprint(camera, map, cell, terrainSize);
        if (f.W >= MinRegionPx && f.H >= MinRegionPx) return f;
        return new RectD(px - MinRegionPx / 2.0, py - MinRegionPx / 2.0, MinRegionPx, MinRegionPx);
    }
}
