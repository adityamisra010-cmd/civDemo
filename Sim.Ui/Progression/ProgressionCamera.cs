namespace Sim.Ui.Progression;

/// <summary>
/// The tree canvas camera: a world point (PanX, PanY) at the canvas's top-left and a zoom.
/// Pan and zoom requests move a TARGET; <see cref="Advance"/> eases the shown view toward it
/// (frame-rate independent exponential smoothing), so scrolling and zooming glide. Pure.
/// </summary>
public sealed class ProgressionCamera
{
    public const double MinZoom = 0.12, MaxZoom = 1.6;
    public double Zoom { get; private set; } = 0.8;
    public double PanX { get; private set; }
    public double PanY { get; private set; }
    public double TargetZoom { get; private set; } = 0.8;
    public double TargetPanX { get; private set; }
    public double TargetPanY { get; private set; }

    public double ToScreenX(double wx, double canvasX) => canvasX + (wx - PanX) * Zoom;
    public double ToScreenY(double wy, double canvasY) => canvasY + (wy - PanY) * Zoom;
    public double ToWorldX(double sx, double canvasX) => PanX + (sx - canvasX) / Zoom;
    public double ToWorldY(double sy, double canvasY) => PanY + (sy - canvasY) / Zoom;

    /// <summary>Jump (no easing) — used on open and by previews.</summary>
    public void Set(double zoom, double panX, double panY)
    {
        Zoom = TargetZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        PanX = TargetPanX = panX;
        PanY = TargetPanY = panY;
    }

    /// <summary>Pan by a screen-pixel drag; applied immediately (drags must track the hand).</summary>
    public void Drag(double dxPx, double dyPx)
    {
        PanX -= dxPx / Zoom; PanY -= dyPx / Zoom;
        TargetPanX -= dxPx / TargetZoom; TargetPanY -= dyPx / TargetZoom;
    }

    /// <summary>Smooth scroll by screen pixels (keyboard / trackpad).</summary>
    public void ScrollBy(double dxPx, double dyPx)
    {
        TargetPanX += dxPx / TargetZoom; TargetPanY += dyPx / TargetZoom;
    }

    /// <summary>Zoom about a screen point (relative to the canvas origin), keeping the world
    /// point under it fixed once the easing settles.</summary>
    public void ZoomAt(double localX, double localY, double factor)
    {
        double wx = TargetPanX + localX / TargetZoom, wy = TargetPanY + localY / TargetZoom;
        TargetZoom = Math.Clamp(TargetZoom * factor, MinZoom, MaxZoom);
        TargetPanX = wx - localX / TargetZoom;
        TargetPanY = wy - localY / TargetZoom;
    }

    /// <summary>Glide so world point (wx, wy) sits at the canvas centre.</summary>
    public void CenterOn(double wx, double wy, double canvasW, double canvasH)
    {
        TargetPanX = wx - canvasW / 2.0 / TargetZoom;
        TargetPanY = wy - canvasH / 2.0 / TargetZoom;
    }

    /// <summary>Keep the view over the drawing (a margin of half the canvas may show beyond it).</summary>
    public void Clamp(double worldW, double worldH, double canvasW, double canvasH)
    {
        static double C(double v, double lo, double hi) => hi < lo ? (lo + hi) / 2.0 : Math.Clamp(v, lo, hi);
        TargetPanX = C(TargetPanX, -canvasW * 0.5 / TargetZoom, worldW - canvasW * 0.5 / TargetZoom);
        TargetPanY = C(TargetPanY, -canvasH * 0.5 / TargetZoom, worldH - canvasH * 0.5 / TargetZoom);
    }

    /// <summary>Ease toward the target over <paramref name="dt"/> seconds. Returns true while moving.</summary>
    public bool Advance(double dt)
    {
        double k = 1.0 - Math.Exp(-Math.Max(0.0, dt) * 14.0);
        Zoom += (TargetZoom - Zoom) * k;
        PanX += (TargetPanX - PanX) * k;
        PanY += (TargetPanY - PanY) * k;
        bool moving = Math.Abs(TargetZoom - Zoom) > 1e-4 || Math.Abs(TargetPanX - PanX) > 0.05 || Math.Abs(TargetPanY - PanY) > 0.05;
        if (!moving) { Zoom = TargetZoom; PanX = TargetPanX; PanY = TargetPanY; }
        return moving;
    }
}
