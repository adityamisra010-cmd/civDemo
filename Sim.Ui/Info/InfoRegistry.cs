using Sim.Core.State;
using Sim.Ui.Render;

namespace Sim.Ui.Info;

/// <summary>One inspectable region painted this frame: where, what it designates, the text painted there, and
/// whether the host must route a Shift+click on it itself (<see cref="Passive"/>: a text line or a map token with no
/// control of its own) or the control that painted it answers (a button, the action surface, the trees, the Age panel).</summary>
public readonly record struct InfoHit(RectD Rect, InfoSubject Subject, string Label, bool Passive = false);

/// <summary>
/// M5 polish (directive §9) — THE FRAME'S INSPECTABLE REGIONS, the <see cref="UiControls"/> pattern for Shift+click:
/// every painter that draws a nameable object (a research card, a milestone, a labour row, a good in a production line,
/// a project, a formation token…) adds (rect, subject) here as it paints; the host hit-tests the LAST frame's regions
/// (what the player saw) when Shift is held at a click. The top-most region wins: the last added that contains the
/// point. UI state only — nothing in the simulation reads it, and a hit never issues an order.
/// </summary>
public sealed class InfoRegistry
{
    private readonly List<InfoHit> _hits = [];

    /// <summary>The regions registered this frame, in paint order.</summary>
    public IReadOnlyList<InfoHit> Hits => _hits;

    public void Clear() => _hits.Clear();

    /// <summary>Registers a region (empty rects are ignored).</summary>
    public void Add(RectD rect, InfoSubject subject, string label, bool passive = false)
    {
        if (rect.W > 0.5 && rect.H > 0.5) _hits.Add(new InfoHit(rect, subject, label, passive));
    }

    /// <summary>Registers a painter's regions, each cut to <paramref name="clip"/> (a scrolled body's visible rect)
    /// and dropped when nothing of it is visible or when it lies under <paramref name="covered"/> (a panel painted
    /// over it).</summary>
    public void AddRange(IReadOnlyList<InfoHit> hits, RectD? clip = null, RectD? covered = null)
    {
        foreach (InfoHit h in hits)
        {
            RectD r = h.Rect;
            if (clip is { } c)
            {
                if (!r.Intersects(c)) continue;
                r = Intersect(r, c);
            }
            if (covered is { } cov && cov.Contains(r.CenterX, r.CenterY)) continue;
            Add(r, h.Subject, h.Label, h.Passive);
        }
    }

    /// <summary>The top-most region containing the point, or null.</summary>
    public InfoHit? HitTest(double x, double y)
    {
        for (int i = _hits.Count - 1; i >= 0; i--) if (_hits[i].Rect.Contains(x, y)) return _hits[i];
        return null;
    }

    /// <summary>The first region of a kind (paint order), or null.</summary>
    public InfoHit? First(InfoKind kind)
    {
        foreach (InfoHit h in _hits) if (h.Subject.Kind == kind) return h;
        return null;
    }

    public static RectD Intersect(RectD a, RectD b)
    {
        double x0 = Math.Max(a.X, b.X), y0 = Math.Max(a.Y, b.Y), x1 = Math.Min(a.Right, b.Right), y1 = Math.Min(a.Bottom, b.Bottom);
        return new RectD(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
    }

    /// <summary>Hit test over a painter's own list (the action surface, the Age panel, the trees): the last match.</summary>
    public static InfoHit? At(IReadOnlyList<InfoHit> hits, double x, double y)
    {
        for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].Rect.Contains(x, y)) return hits[i];
        return null;
    }
}
