namespace Sim.Ui.ViewModel;

/// <summary>
/// The map camera as PURE double math (view-model — no MonoGame types, fully
/// unit-testable headless). World space is terrain pixels; screen space is
/// window pixels. Zoom is screen px per world px. The Game converts this to a
/// float transform matrix at the render boundary only.
/// </summary>
public sealed class Camera(double worldSize)
{
    /// <summary>World-space point at the viewport center.</summary>
    public double CenterX { get; private set; } = worldSize / 2.0;
    public double CenterY { get; private set; } = worldSize / 2.0;

    /// <summary>Screen pixels per world pixel.</summary>
    public double Zoom { get; private set; } = 1.0;

    public double WorldSize { get; } = worldSize;

    public const double MaxZoom = 32.0;

    /// <summary>Fit-the-world zoom for a viewport — the minimum zoom (never
    /// smaller than an 1/8 of it so degenerate viewports cannot zero out).</summary>
    public double MinZoom(int viewportW, int viewportH) =>
        Math.Max(Math.Min(viewportW, viewportH) / WorldSize / 8.0,
                 Math.Min(viewportW, viewportH) / WorldSize);

    public (double X, double Y) ScreenToWorld(double sx, double sy, int viewportW, int viewportH) =>
        (CenterX + (sx - viewportW / 2.0) / Zoom,
         CenterY + (sy - viewportH / 2.0) / Zoom);

    public (double X, double Y) WorldToScreen(double wx, double wy, int viewportW, int viewportH) =>
        ((wx - CenterX) * Zoom + viewportW / 2.0,
         (wy - CenterY) * Zoom + viewportH / 2.0);

    /// <summary>Pan by a SCREEN-space delta (mouse drag / WASD): the world moves
    /// with the cursor, so the center moves opposite, scaled by zoom.</summary>
    public void Pan(double dxScreen, double dyScreen, int viewportW, int viewportH)
    {
        CenterX -= dxScreen / Zoom;
        CenterY -= dyScreen / Zoom;
        Clamp(viewportW, viewportH);
    }

    /// <summary>
    /// Zoom by <paramref name="factor"/> keeping the world point under the
    /// CURSOR fixed on screen (the invariant the camera tests pin): solve the
    /// new center from the fixed point after scaling.
    /// </summary>
    public void ZoomAt(double cursorSx, double cursorSy, double factor, int viewportW, int viewportH)
    {
        (double wx, double wy) = ScreenToWorld(cursorSx, cursorSy, viewportW, viewportH);
        Zoom = Math.Clamp(Zoom * factor, MinZoom(viewportW, viewportH), MaxZoom);
        CenterX = wx - (cursorSx - viewportW / 2.0) / Zoom;
        CenterY = wy - (cursorSy - viewportH / 2.0) / Zoom;
        Clamp(viewportW, viewportH);
    }

    /// <summary>
    /// Keep the view inside the world: when the world is wider than the
    /// viewport (in world units) the center stays at least half a viewport
    /// from each edge; when the whole world fits, lock to the world center on
    /// that axis (no drifting off into the void).
    /// </summary>
    public void Clamp(int viewportW, int viewportH)
    {
        Zoom = Math.Clamp(Zoom, MinZoom(viewportW, viewportH), MaxZoom);
        CenterX = ClampAxis(CenterX, viewportW);
        CenterY = ClampAxis(CenterY, viewportH);
    }

    private double ClampAxis(double center, int viewportPx)
    {
        double halfView = viewportPx / 2.0 / Zoom;
        if (halfView * 2.0 >= WorldSize) return WorldSize / 2.0;
        return Math.Clamp(center, halfView, WorldSize - halfView);
    }
}
