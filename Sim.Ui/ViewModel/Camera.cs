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
///
/// P2 (M5 map workstream) — EASED ZOOM AND PAN. The camera holds two views: the SHOWN view (<see cref="Zoom"/>,
/// <see cref="CenterX"/>, <see cref="CenterY"/> — what is drawn, hit-tested and framed by the GPU pass) and the TARGET
/// view (<see cref="TargetZoom"/>, <see cref="TargetCenterX"/>, <see cref="TargetCenterY"/> — where the player asked to
/// look). The wheel (<see cref="ZoomToward"/>) and the keys (<see cref="GlidePan"/>) move the target; once per frame
/// <see cref="Advance"/> eases the shown view toward it by frame-rate-independent exponential smoothing — the
/// <see cref="Sim.Ui.Progression.ProgressionCamera.Advance"/> pattern, at its rate. What is eased is the VISIBLE WORLD
/// RECT: the centre and the inverse zoom (world px per screen px) move by the same fraction, so every edge of the
/// rect moves linearly, and a world point the shown view and the target both have under a screen point stays under it
/// on EVERY frame of the ease. The wheel anchors on the shown view (the ground the player sees under the cursor), so
/// that ground is under the cursor throughout and once the ease settles — for a run of notches at one cursor, and for
/// a notch at a cursor that moved while the view was still easing. A drag (<see cref="Pan"/>) moves both views by the
/// same screen delta (a drag tracks the hand). The immediate operations every preview, test and measurement uses — <see cref="ZoomAt(double, double, double, RectD)"/>,
/// <see cref="Clamp(RectD)"/>, <see cref="Stop"/> — act on the shown view and leave the camera SETTLED (target =
/// shown), so a camera never eased computes exactly what it computed before P2. The camera is UI state only: nothing
/// here reads or writes the simulation.
/// </summary>
public sealed class Camera(double worldSize)
{
    /// <summary>One view of the world: the world point at the map rect's centre and the zoom.</summary>
    private struct View
    {
        public double X, Y, Zoom;
    }

    private View _shown = new() { X = worldSize / 2.0, Y = worldSize / 2.0, Zoom = 1.0 };
    private View _target = new() { X = worldSize / 2.0, Y = worldSize / 2.0, Zoom = 1.0 };

    /// <summary>World-space point at the map rect's center (the SHOWN view).</summary>
    public double CenterX => _shown.X;
    public double CenterY => _shown.Y;

    /// <summary>Screen pixels per world pixel (the SHOWN view).</summary>
    public double Zoom => _shown.Zoom;

    /// <summary>P2: the view the shown one eases toward.</summary>
    public double TargetCenterX => _target.X;
    public double TargetCenterY => _target.Y;
    public double TargetZoom => _target.Zoom;

    /// <summary>P2: the shown view IS the target view (no ease in progress).</summary>
    public bool Settled => _shown.X == _target.X && _shown.Y == _target.Y && _shown.Zoom == _target.Zoom;

    public double WorldSize { get; } = worldSize;

    public const double MaxZoom = 32.0;

    /// <summary>P2: the ease rate, per second — ProgressionCamera's (k = 1 − exp(−14 dt)): 90 % of the way in 0.16 s.</summary>
    public const double EaseRate = 14.0;

    /// <summary>P2: the ease snaps to its target once the zoom is within this fraction of it …</summary>
    public const double SettleZoomFraction = 1e-4;

    /// <summary>… and the centre within this many screen px of it (at the target zoom).</summary>
    public const double SettleScreenPx = 0.05;

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

    /// <summary>P2: screen → world through the map rect in the TARGET view (where the point will be once the ease settles).</summary>
    public (double X, double Y) TargetScreenToWorld(double sx, double sy, RectD view) =>
        (_target.X + (sx - view.CenterX) / _target.Zoom,
         _target.Y + (sy - view.CenterY) / _target.Zoom);

    /// <summary>P1: the affine map world → screen as (scale, offset x, offset y): screen = world × scale + offset. The
    /// render matrix (SimUiGame) is built from THIS, so the GPU pass and <see cref="WorldToScreen(double, double, RectD)"/>
    /// cannot disagree.</summary>
    public (double Scale, double OffsetX, double OffsetY) Transform(RectD view) =>
        (Zoom, view.CenterX - CenterX * Zoom, view.CenterY - CenterY * Zoom);

    /// <summary>Pan by a SCREEN-space delta (mouse drag): the world moves
    /// with the cursor, so the center moves opposite, scaled by zoom.</summary>
    public void Pan(double dxScreen, double dyScreen, int viewportW, int viewportH) =>
        Pan(dxScreen, dyScreen, Window(viewportW, viewportH));

    /// <summary>P1: pan, clamped to the map rect. P2: a DRAG — it moves the shown view and the target view by the same
    /// screen delta (each at its own zoom), so the ground under the hand follows the hand while an ease goes on.</summary>
    public void Pan(double dxScreen, double dyScreen, RectD view)
    {
        _shown.X -= dxScreen / _shown.Zoom;
        _shown.Y -= dyScreen / _shown.Zoom;
        _target.X -= dxScreen / _target.Zoom;
        _target.Y -= dyScreen / _target.Zoom;
        Clamp(view);
    }

    /// <summary>P2: an EASED pan by a screen-space delta (W A S D): the target moves, the shown view glides after it.</summary>
    public void GlidePan(double dxScreen, double dyScreen, RectD view)
    {
        _target.X -= dxScreen / _target.Zoom;
        _target.Y -= dyScreen / _target.Zoom;
        ClampView(ref _target, view);
    }

    /// <summary>
    /// Zoom by <paramref name="factor"/> keeping the world point under the
    /// CURSOR fixed on screen (the invariant the camera tests pin): solve the
    /// new center from the fixed point after scaling. IMMEDIATE (P2): the camera is left settled.
    /// </summary>
    public void ZoomAt(double cursorSx, double cursorSy, double factor, int viewportW, int viewportH) =>
        ZoomAt(cursorSx, cursorSy, factor, Window(viewportW, viewportH));

    /// <summary>P1: zoom at the cursor through the map rect. Immediate (P2): the camera is left settled.</summary>
    public void ZoomAt(double cursorSx, double cursorSy, double factor, RectD view)
    {
        (double wx, double wy) = ScreenToWorld(cursorSx, cursorSy, view);
        _shown.Zoom = Math.Clamp(Zoom * factor, MinZoom(view), MaxZoom);
        _shown.X = wx - (cursorSx - view.CenterX) / _shown.Zoom;
        _shown.Y = wy - (cursorSy - view.CenterY) / _shown.Zoom;
        _target = _shown;
        Clamp(view);
    }

    /// <summary>
    /// P2: THE WHEEL — an EASED zoom by <paramref name="factor"/> about the cursor. The target zoom is multiplied (notches
    /// accumulate) and the target centre is solved so the world point the player SEES under the cursor — in the SHOWN
    /// view — is under it in the target view too. Both views then have that point under the cursor, so the eased
    /// visible rect keeps it there on every frame and once the ease settles (see the class remarks), wherever the cursor
    /// was at earlier notches. (P2 review finding: anchored on the TARGET view, as ProgressionCamera.ZoomAt is, a notch
    /// arriving mid-ease at a cursor that had moved since the last notch anchored on ground not under the cursor; the
    /// ground seen there drifted (cursor moved) × (1/shown zoom − 1/target zoom) — MEASURED 324.7 / 203.6 / 101.1 screen
    /// px at 1 / 3 / 6 frames of lag.) At a still cursor the two anchors are the same point. The target's pending
    /// W A S D glide lead is not carried across a notch: the target restarts from what is on screen. The clamp applies
    /// to the target, as it applied to the immediate zoom.
    /// </summary>
    public void ZoomToward(double cursorSx, double cursorSy, double factor, RectD view)
    {
        (double wx, double wy) = ScreenToWorld(cursorSx, cursorSy, view);
        _target.Zoom = Math.Clamp(_target.Zoom * factor, MinZoom(view), MaxZoom);
        _target.X = wx - (cursorSx - view.CenterX) / _target.Zoom;
        _target.Y = wy - (cursorSy - view.CenterY) / _target.Zoom;
        ClampView(ref _target, view);
    }

    /// <summary>
    /// P2: eases the shown view toward the target over <paramref name="dt"/> seconds. The remaining distance of the centre
    /// and of the inverse zoom shrinks by exp(−<see cref="EaseRate"/> dt), so N frames of dt/N reach the same view as one
    /// frame of dt (frame-rate independent). Snaps to the target once within <see cref="SettleZoomFraction"/> and
    /// <see cref="SettleScreenPx"/>. A settled camera is left exactly as it is. Returns true while it is still moving.
    /// </summary>
    public bool Advance(double dt)
    {
        if (Settled) return false;
        double k = 1.0 - Math.Exp(-Math.Max(0.0, dt) * EaseRate);
        double inv = 1.0 / _shown.Zoom;
        inv += (1.0 / _target.Zoom - inv) * k;
        _shown.Zoom = 1.0 / inv;
        _shown.X += (_target.X - _shown.X) * k;
        _shown.Y += (_target.Y - _shown.Y) * k;
        bool moving = Math.Abs(_target.Zoom - _shown.Zoom) > SettleZoomFraction * _target.Zoom
            || Math.Abs(_target.X - _shown.X) * _target.Zoom > SettleScreenPx
            || Math.Abs(_target.Y - _shown.Y) * _target.Zoom > SettleScreenPx;
        if (!moving) _shown = _target;
        return moving;
    }

    /// <summary>P2: ends the ease where it is — the target becomes the shown view (an immediate operation such as
    /// <see cref="CameraFocus.CenterOn(Camera, Sim.Core.State.IReadOnlyWorldState, int, RectD)"/> starts from what is on screen).</summary>
    public void Stop() => _target = _shown;

    /// <summary>
    /// Keep the view inside the world: when the world is wider than the
    /// viewport (in world units) the center stays at least half a viewport
    /// from each edge; when the whole world fits, lock to the world center on
    /// that axis (no drifting off into the void).
    /// </summary>
    public void Clamp(int viewportW, int viewportH) => Clamp(Window(viewportW, viewportH));

    /// <summary>P1: the clamp against the map rect — the rect, not the window, stays over the world. P2: both views are
    /// clamped, each at its own zoom (a settled camera stays settled).</summary>
    public void Clamp(RectD view)
    {
        ClampView(ref _shown, view);
        ClampView(ref _target, view);
    }

    /// <summary>
    /// P1: THE MAP RECT CHANGED (a context panel opened, folded or closed; the window was resized). The world point
    /// under (<paramref name="anchorSx"/>, <paramref name="anchorSy"/>) — the cursor — stays where it is on screen,
    /// and the view is then clamped to the new rect. At the same zoom that keeps the whole picture still (a pan of
    /// zero); a view that was at the old rect's fit stays at fit — it RE-FITS to the new rect (the whole world, framed
    /// in it) — and a zoom below the new rect's fit is raised to it. Where the clamp moves the view (the world's edge
    /// would leave the new rect), the clamp wins: that is what "inside the world" means. P2: the shown view and the
    /// target view are reframed alike, each about the same anchor, so an ease in progress carries on in the new rect.
    /// </summary>
    public void Reframe(RectD from, RectD to, double anchorSx, double anchorSy)
    {
        bool settled = Settled;
        ReframeView(ref _shown, from, to, anchorSx, anchorSy);
        if (settled) _target = _shown;
        else ReframeView(ref _target, from, to, anchorSx, anchorSy);
    }

    private void ReframeView(ref View v, RectD from, RectD to, double anchorSx, double anchorSy)
    {
        bool atFit = v.Zoom <= MinZoom(from) * (1.0 + 1e-12);
        double wx = v.X + (anchorSx - from.CenterX) / v.Zoom, wy = v.Y + (anchorSy - from.CenterY) / v.Zoom;
        v.Zoom = atFit ? Math.Min(MinZoom(to), MaxZoom) : Math.Clamp(v.Zoom, MinZoom(to), MaxZoom);
        v.X = wx - (anchorSx - to.CenterX) / v.Zoom;
        v.Y = wy - (anchorSy - to.CenterY) / v.Zoom;
        ClampView(ref v, to);
    }

    private void ClampView(ref View v, RectD view)
    {
        v.Zoom = Math.Clamp(v.Zoom, MinZoom(view), MaxZoom);
        v.X = ClampAxis(v.X, view.W, v.Zoom);
        v.Y = ClampAxis(v.Y, view.H, v.Zoom);
    }

    private double ClampAxis(double center, double viewportPx, double zoom)
    {
        double halfView = viewportPx / 2.0 / zoom;
        if (halfView * 2.0 >= WorldSize) return WorldSize / 2.0;
        return Math.Clamp(center, halfView, WorldSize - halfView);
    }
}
