using Sim.Ui.Render;

namespace Sim.Ui.ViewModel;

/// <summary>
/// The map camera as PURE double math (view-model — no MonoGame types, fully
/// unit-testable headless). World space is terrain pixels; screen space is
/// window pixels. Zoom is screen px per world px. The Game converts this to a
/// float transform matrix at the render boundary only.
///
/// P1 (M5 map workstream) — THE MAP RECT. Every operation takes the rectangle of the window the map is LOOKED AT
/// through: the clear area the chrome leaves (GameUi.MapRect — between the status band and the command bar, left of
/// an open context panel). <see cref="CenterX"/>/<see cref="CenterY"/> is the world point at that rect's centre, the
/// fit zoom fits the world into that rect, and the clamp keeps that rect over the world — so at fit no world sits
/// under the bars, and every edge of the world can be brought into the clear map. The map is still DRAWN full-window
/// beneath the chrome; only what the camera frames moved. The (width, height) overloads are the rect at the window's
/// origin — the full-window camera every headless paint and preview uses — and compute exactly what they computed
/// before (the origin adds 0).
/// </summary>
public sealed class Camera(double worldSize)
{
    /// <summary>World-space point at the map rect's center.</summary>
    public double CenterX { get; private set; } = worldSize / 2.0;
    public double CenterY { get; private set; } = worldSize / 2.0;

    /// <summary>Screen pixels per world pixel.</summary>
    public double Zoom { get; private set; } = 1.0;

    public double WorldSize { get; } = worldSize;

    public const double MaxZoom = 32.0;

    /// <summary>A full-window map rect: the window's origin, its size.</summary>
    public static RectD Window(int viewportW, int viewportH) => new(0, 0, viewportW, viewportH);

    /// <summary>Fit-the-world zoom for a viewport — the minimum zoom (never
    /// smaller than an 1/8 of it so degenerate viewports cannot zero out).</summary>
    public double MinZoom(int viewportW, int viewportH) => MinZoom(Window(viewportW, viewportH));

    /// <summary>P1: the fit zoom of a MAP RECT — the whole world inside it, on its shorter side.</summary>
    public double MinZoom(RectD view) =>
        Math.Max(Math.Min(view.W, view.H) / WorldSize / 8.0,
                 Math.Min(view.W, view.H) / WorldSize);

    public (double X, double Y) ScreenToWorld(double sx, double sy, int viewportW, int viewportH) =>
        ScreenToWorld(sx, sy, Window(viewportW, viewportH));

    public (double X, double Y) WorldToScreen(double wx, double wy, int viewportW, int viewportH) =>
        WorldToScreen(wx, wy, Window(viewportW, viewportH));

    /// <summary>P1: screen → world through the map rect (its centre shows <see cref="CenterX"/>, <see cref="CenterY"/>).</summary>
    public (double X, double Y) ScreenToWorld(double sx, double sy, RectD view) =>
        (CenterX + (sx - view.CenterX) / Zoom,
         CenterY + (sy - view.CenterY) / Zoom);

    /// <summary>P1: world → screen through the map rect.</summary>
    public (double X, double Y) WorldToScreen(double wx, double wy, RectD view) =>
        ((wx - CenterX) * Zoom + view.CenterX,
         (wy - CenterY) * Zoom + view.CenterY);

    /// <summary>P1: the affine map world → screen as (scale, offset x, offset y): screen = world × scale + offset. The
    /// render matrix (SimUiGame) is built from THIS, so the GPU pass and <see cref="WorldToScreen(double, double, RectD)"/>
    /// cannot disagree.</summary>
    public (double Scale, double OffsetX, double OffsetY) Transform(RectD view) =>
        (Zoom, view.CenterX - CenterX * Zoom, view.CenterY - CenterY * Zoom);

    /// <summary>Pan by a SCREEN-space delta (mouse drag / WASD): the world moves
    /// with the cursor, so the center moves opposite, scaled by zoom.</summary>
    public void Pan(double dxScreen, double dyScreen, int viewportW, int viewportH) =>
        Pan(dxScreen, dyScreen, Window(viewportW, viewportH));

    /// <summary>P1: pan, clamped to the map rect.</summary>
    public void Pan(double dxScreen, double dyScreen, RectD view)
    {
        CenterX -= dxScreen / Zoom;
        CenterY -= dyScreen / Zoom;
        Clamp(view);
    }

    /// <summary>
    /// Zoom by <paramref name="factor"/> keeping the world point under the
    /// CURSOR fixed on screen (the invariant the camera tests pin): solve the
    /// new center from the fixed point after scaling.
    /// </summary>
    public void ZoomAt(double cursorSx, double cursorSy, double factor, int viewportW, int viewportH) =>
        ZoomAt(cursorSx, cursorSy, factor, Window(viewportW, viewportH));

    /// <summary>P1: zoom at the cursor through the map rect.</summary>
    public void ZoomAt(double cursorSx, double cursorSy, double factor, RectD view)
    {
        (double wx, double wy) = ScreenToWorld(cursorSx, cursorSy, view);
        Zoom = Math.Clamp(Zoom * factor, MinZoom(view), MaxZoom);
        CenterX = wx - (cursorSx - view.CenterX) / Zoom;
        CenterY = wy - (cursorSy - view.CenterY) / Zoom;
        Clamp(view);
    }

    /// <summary>
    /// Keep the view inside the world: when the world is wider than the
    /// viewport (in world units) the center stays at least half a viewport
    /// from each edge; when the whole world fits, lock to the world center on
    /// that axis (no drifting off into the void).
    /// </summary>
    public void Clamp(int viewportW, int viewportH) => Clamp(Window(viewportW, viewportH));

    /// <summary>P1: the clamp against the map rect — the rect, not the window, stays over the world.</summary>
    public void Clamp(RectD view)
    {
        Zoom = Math.Clamp(Zoom, MinZoom(view), MaxZoom);
        CenterX = ClampAxis(CenterX, view.W);
        CenterY = ClampAxis(CenterY, view.H);
    }

    /// <summary>
    /// P1: THE MAP RECT CHANGED (a context panel opened, folded or closed; the window was resized). The world point
    /// under (<paramref name="anchorSx"/>, <paramref name="anchorSy"/>) — the cursor — stays where it is on screen,
    /// and the view is then clamped to the new rect. At the same zoom that keeps the whole picture still (a pan of
    /// zero); a view that was at the old rect's fit stays at fit — it RE-FITS to the new rect (the whole world, framed
    /// in it) — and a zoom below the new rect's fit is raised to it. Where the clamp moves the view (the world's edge
    /// would leave the new rect), the clamp wins: that is what "inside the world" means.
    /// </summary>
    public void Reframe(RectD from, RectD to, double anchorSx, double anchorSy)
    {
        bool atFit = Zoom <= MinZoom(from) * (1.0 + 1e-12);
        (double wx, double wy) = ScreenToWorld(anchorSx, anchorSy, from);
        Zoom = atFit ? Math.Min(MinZoom(to), MaxZoom) : Math.Clamp(Zoom, MinZoom(to), MaxZoom);
        CenterX = wx - (anchorSx - to.CenterX) / Zoom;
        CenterY = wy - (anchorSy - to.CenterY) / Zoom;
        Clamp(to);
    }

    private double ClampAxis(double center, double viewportPx)
    {
        double halfView = viewportPx / 2.0 / Zoom;
        if (halfView * 2.0 >= WorldSize) return WorldSize / 2.0;
        return Math.Clamp(center, halfView, WorldSize - halfView);
    }
}
