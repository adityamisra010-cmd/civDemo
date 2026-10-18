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
///
/// BATCH 3 WS3 — THE CYLINDER (<see cref="WrapX"/>; the Director's 2026-10-14 instruction: "rendering and camera movement
/// must display the same world consistently across the boundary"). A camera built with <c>wrapX: true</c> (only the game,
/// from <c>TerrainSet.WrapX</c>, and the frame-cost measurement pass it; every existing flat camera and test is unchanged):
/// <list type="bullet">
/// <item>X is NORMALISED into [0, <see cref="WorldSize"/>) after every operation (<see cref="Sim.Core.Pathing.MapTopology.WrapCoord"/>) —
///   panning east past the eastern edge continues from the western edge, and the reverse; there is no x clamp;</item>
/// <item><see cref="Advance"/>, the glide and <see cref="Settled"/> take the SHORTEST wrapped delta, so an ease never goes the
///   long way round;</item>
/// <item>Y keeps its clamp (the poles are hard edges);</item>
/// <item>THE ZOOM FLOOR is the WINDOW's width over the world's (<see cref="WindowWidth"/>; not the map rect's), so the window —
///   the map is drawn full-window beneath the chrome — is never wider than one circumference: no world point is ever on
///   screen twice, and opening or closing a context panel neither re-zooms nor shows repeated terrain. (A window taller
///   than wide floors on its height instead, so the poles' clamp stays meaningful — the same rule for any landscape window.)</item>
/// </list>
/// What is drawn where is the caller's: <see cref="WorldToScreen(double, double, RectD)"/> stays the plain affine map of
/// copy 0 (world x as given), <see cref="VisibleCopies"/> says which copies k (world x + k·WorldSize) of the world the window
/// shows, and <see cref="WorldToScreenNearest"/> places a point at its copy nearest the view centre.
/// </summary>
public sealed class Camera(double worldSize, bool wrapX = false)
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

    /// <summary>WS3: the world is a cylinder east-west (see the class remarks). False: the flat camera, exactly as before.</summary>
    public bool WrapX { get; } = wrapX;

    /// <summary>WS3: the width in screen px of the WINDOW the map is drawn across (the game sets it from its viewport) — a
    /// wrapped camera's zoom floor is this over <see cref="WorldSize"/>. 0 (unset): the map rect's right edge. Unused by a
    /// flat camera.</summary>
    public double WindowWidth { get; set; }

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

    /// <summary>P1: the fit zoom of a MAP RECT — the whole world inside it, on its shorter side. WS3: a wrapped camera's
    /// floor is one circumference across the WINDOW — max(<see cref="WindowWidth"/> (else the rect's right edge), the rect's
    /// height) / <see cref="WorldSize"/> — so the window never shows a world point twice, whatever panel is open.</summary>
    public double MinZoom(RectD view)
    {
        if (WrapX)
        {
            double across = Math.Max(Math.Max(WindowWidth, view.Right), view.H);
            return Math.Max(1e-9, across / WorldSize);
        }
        return Math.Max(Math.Min(view.W, view.H) / WorldSize / 8.0,
                        Math.Min(view.W, view.H) / WorldSize);
    }

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

    /// <summary>WS3: world → screen at the copy of <paramref name="wx"/> NEAREST the view centre (<see cref="CenterX"/>): on a
    /// wrapped camera the point is placed where the player sees it (a settlement just west of the seam, viewed from just
    /// east of it, is to the left of the centre, not a circumference away); flat, exactly <see cref="WorldToScreen(double, double, RectD)"/>.</summary>
    public (double X, double Y) WorldToScreenNearest(double wx, double wy, RectD view) =>
        WorldToScreen(WrapX ? Sim.Core.Pathing.MapTopology.NearestCopy(wx, CenterX, WorldSize) : wx, wy, view);

    /// <summary>WS3: the world rect (world px) a screen rect <paramref name="screen"/> shows through the map rect
    /// <paramref name="view"/> in the SHOWN view — x NOT normalised (it may run below 0 or past <see cref="WorldSize"/>, which
    /// is exactly the information <see cref="VisibleCopies"/> needs).</summary>
    public RectD ScreenRectToWorld(RectD screen, RectD view)
    {
        (double x0, double y0) = ScreenToWorld(screen.X, screen.Y, view);
        (double x1, double y1) = ScreenToWorld(screen.Right, screen.Bottom, view);
        return new RectD(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>
    /// WS3: WHICH COPIES OF THE WORLD a world-space window shows — the k ∈ {−1, 0, 1} (ascending) for which the copy
    /// [k·<see cref="WorldSize"/>, (k + 1)·<see cref="WorldSize"/>) intersects [<paramref name="windowWorld"/>.X,
    /// <paramref name="windowWorld"/>.Right). A pure function. A flat camera always answers {0} (there is one world, drawn
    /// where it is). On a wrapped camera the centre is in [0, WorldSize) and the floor keeps the window at most one
    /// circumference wide, so {−1, 0, 1} is always enough: a window straddling the seam answers two copies (the two pieces of
    /// one world across the cut, never a duplicate), one inside the world answers {0}. The GPU passes (the terrain sprite's
    /// source rect, the river buffer's translations) draw exactly these copies.
    /// </summary>
    public int[] VisibleCopies(RectD windowWorld)
    {
        if (!WrapX) return [0];
        var ks = new List<int>(3);
        for (int k = -1; k <= 1; k++)
        {
            double lo = k * WorldSize, hi = (k + 1) * WorldSize;
            if (windowWorld.X < hi && windowWorld.Right > lo) ks.Add(k);
        }
        return [.. ks];
    }

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
        _shown.Zoom = Math.Clamp(Zoom * factor, MinZoom(view), CeilingFor(MinZoom(view)));
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
        _target.Zoom = Math.Clamp(_target.Zoom * factor, MinZoom(view), CeilingFor(MinZoom(view)));
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
        // WS3: on a cylinder the centre eases along the SHORTEST wrapped delta (never the long way round), and is normalised.
        _shown.X += DeltaX(_shown.X, _target.X) * k;
        if (WrapX) _shown.X = Wrap(_shown.X);
        _shown.Y += (_target.Y - _shown.Y) * k;
        bool moving = Math.Abs(_target.Zoom - _shown.Zoom) > SettleZoomFraction * _target.Zoom
            || Math.Abs(DeltaX(_shown.X, _target.X)) * _target.Zoom > SettleScreenPx
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
    public void Reframe(RectD from, RectD to, double anchorSx, double anchorSy) =>
        Reframe(from, to, anchorSx, anchorSy, WindowWidth);

    /// <summary>WS3: <see cref="Reframe(RectD, RectD, double, double)"/> when the WINDOW's width changed too (a resize): a
    /// wrapped camera's floor before the change is judged at the old <see cref="WindowWidth"/>, after it at
    /// <paramref name="toWindowWidth"/> (which becomes the camera's). A flat camera ignores the window width.</summary>
    public void Reframe(RectD from, RectD to, double anchorSx, double anchorSy, double toWindowWidth)
    {
        bool settled = Settled;
        double fromFloor = MinZoom(from);
        WindowWidth = toWindowWidth;
        double toFloor = MinZoom(to);
        ReframeView(ref _shown, from, to, anchorSx, anchorSy, fromFloor, toFloor);
        if (settled) _target = _shown;
        else ReframeView(ref _target, from, to, anchorSx, anchorSy, fromFloor, toFloor);
    }

    private void ReframeView(ref View v, RectD from, RectD to, double anchorSx, double anchorSy, double fromFloor, double toFloor)
    {
        bool atFit = v.Zoom <= fromFloor * (1.0 + 1e-12);
        double wx = v.X + (anchorSx - from.CenterX) / v.Zoom, wy = v.Y + (anchorSy - from.CenterY) / v.Zoom;
        v.Zoom = atFit ? Math.Min(toFloor, CeilingFor(toFloor)) : Math.Clamp(v.Zoom, toFloor, CeilingFor(toFloor));
        v.X = wx - (anchorSx - to.CenterX) / v.Zoom;
        v.Y = wy - (anchorSy - to.CenterY) / v.Zoom;
        ClampView(ref v, to);
    }

    private void ClampView(ref View v, RectD view)
    {
        v.Zoom = Math.Clamp(v.Zoom, MinZoom(view), CeilingFor(MinZoom(view)));
        v.X = WrapX ? Wrap(v.X) : ClampAxis(v.X, view.W, v.Zoom);   // WS3: no x edge on a cylinder — normalised instead
        v.Y = ClampAxis(v.Y, view.H, v.Zoom);
    }

    /// <summary>The zoom ceiling over a floor of <paramref name="floor"/>: <see cref="MaxZoom"/>. WS3: a wrapped camera's
    /// floor (one circumference across the window) can exceed it on a tiny world (a 12-px --size world in a 1280-px window:
    /// 107×); its ceiling is then the floor — the window is never wider than one circumference, and the clamp never inverts.
    /// A flat camera keeps <see cref="MaxZoom"/> exactly.</summary>
    private double CeilingFor(double floor) => WrapX ? Math.Max(floor, MaxZoom) : MaxZoom;

    /// <summary>WS3: a world x normalised into [0, <see cref="WorldSize"/>) (<see cref="Sim.Core.Pathing.MapTopology.WrapCoord"/>).</summary>
    private double Wrap(double x) => Sim.Core.Pathing.MapTopology.WrapCoord(x, WorldSize);

    /// <summary>WS3: the x delta from <paramref name="from"/> to <paramref name="to"/> — on a cylinder the SHORTEST way round
    /// (in (−WorldSize/2, WorldSize/2]; east at the exact half-world tie, MapTopology's convention), flat the plain difference.</summary>
    public double DeltaX(double from, double to)
    {
        double d = to - from;
        if (!WrapX || !double.IsFinite(d)) return d;
        double size = WorldSize;
        d -= size * Math.Floor(d / size + 0.5);
        if (d <= -size / 2.0) d += size;
        else if (d > size / 2.0) d -= size;
        return d;
    }

    private double ClampAxis(double center, double viewportPx, double zoom)
    {
        double halfView = viewportPx / 2.0 / zoom;
        if (halfView * 2.0 >= WorldSize) return WorldSize / 2.0;
        return Math.Clamp(center, halfView, WorldSize - halfView);
    }
}
