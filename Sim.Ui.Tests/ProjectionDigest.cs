using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.State;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// Exact, culture-free digests of what the map shows for one world: the <see cref="WorldProjection"/> itself (every
/// field the lens reads, doubles round-tripped with "R") and, for each of a fixed set of cameras, the SVG bytes of the
/// lens paint and its <see cref="LensFrame"/>. Two projections whose digests are equal draw the same map, byte for
/// byte. Used by <see cref="WorldProjectionStrideTests"/> (the per-turn lattice build replaced by the stride, P4).
/// Only the public lens API is used, so the same digests can be computed on any commit that has it.
/// </summary>
public static class ProjectionDigest
{
    private static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    private static string I(long v) => v.ToString(CultureInfo.InvariantCulture);

    public static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    /// <summary>Every field of the projection, in table order.</summary>
    public static string Of(WorldProjection p)
    {
        var sb = new StringBuilder();
        sb.Append("size ").Append(R(p.WorldSize)).Append(" player ").Append(I(p.PlayerPolity)).Append(" age ").Append(I(p.PlayerAge))
          .Append(' ').Append(p.PlayerAgeName).Append('\n');
        foreach (SettlementLensView s in p.Settlements)
        {
            sb.Append("S ").Append(I(s.Id)).Append(' ').Append(s.Name).Append(' ').Append(R(s.X)).Append(' ').Append(R(s.Y))
              .Append(' ').Append(I(s.Controller)).Append(' ').Append(s.IsCapital).Append(' ').Append(s.IsPlayer)
              .Append(' ').Append(I(s.Population)).Append(' ').Append(I(s.Dwellings)).Append(' ').Append(I(s.SizeTier))
              .Append(' ').Append(I(s.Age)).Append(' ').Append(I(s.RoadsInCatchment)).Append(' ').Append(I(s.Notables))
              .Append(' ').Append(I(s.ClassesActive)).Append(' ').Append(s.TopSectorLabel ?? "-");
            foreach (StructureView st in s.Structures)
                sb.Append(" st:").Append(I(st.ProjectId)).Append('/').Append(st.Name).Append('/').Append(st.Kind).Append('/').Append(I(st.Count));
            foreach (InstitutionView iv in s.Institutions)
                sb.Append(" in:").Append(I(iv.TypeKey)).Append('/').Append(iv.Name).Append('/').Append(I(iv.Count));
            if (s.Sectors is { } sec) foreach (double x in sec) sb.Append(" sec:").Append(R(x));
            sb.Append('\n');
        }
        foreach (UnitLensView u in p.Units)
            sb.Append("U ").Append(I(u.Id)).Append(' ').Append(I(u.Owner)).Append(' ').Append(R(u.X)).Append(' ').Append(R(u.Y))
              .Append(' ').Append(I(u.FamilyKey)).Append(' ').Append(u.FamilyName).Append(' ').Append(u.IdentityName)
              .Append(' ').Append(I(u.Location)).Append('\n');
        foreach (RoadSegment r in p.Roads)
            sb.Append("P ").Append(R(r.X0)).Append(' ').Append(R(r.Y0)).Append(' ').Append(R(r.X1)).Append(' ').Append(R(r.Y1))
              .Append(' ').Append(I(r.EdgeType)).Append(' ').Append(I(r.Edge)).Append(' ').Append(I(r.CoveredBy)).Append('\n');
        foreach (RouteLensView r in p.Routes)
            sb.Append("R ").Append(I(r.Id)).Append(' ').Append(I(r.A)).Append(' ').Append(I(r.B)).Append(' ').Append(R(r.X0))
              .Append(' ').Append(R(r.Y0)).Append(' ').Append(R(r.X1)).Append(' ').Append(R(r.Y1)).Append(' ').Append(I(r.EdgeType))
              .Append(' ').Append(I(r.TargetClass)).Append(' ').Append(R(r.Modernization)).Append(' ').Append(R(r.LengthKm))
              .Append(' ').Append(r.ClassName).Append(' ').Append(r.TargetName).Append('\n');
        foreach (TerritoryBlock t in p.Territory)
            sb.Append("T ").Append(R(t.X)).Append(' ').Append(R(t.Y)).Append(' ').Append(R(t.Size)).Append(' ').Append(I(t.Polity)).Append('\n');
        foreach (string a in p.Absent) sb.Append("A ").Append(a).Append('\n');
        return sb.ToString();
    }

    /// <summary>Every field of a lens frame, in emission order.</summary>
    public static string Of(LensFrame f)
    {
        var sb = new StringBuilder();
        sb.Append("zoom ").Append(f.Zoom).Append('\n');
        foreach (WorldLayer l in f.Layers) sb.Append("layer ").Append(l).Append('\n');
        foreach (int u in f.UnitTokens) sb.Append("token ").Append(I(u)).Append('\n');
        foreach (int s in f.SettlementsDrawn) sb.Append("drawn ").Append(I(s)).Append('\n');
        foreach (int n in f.LayerDraws) sb.Append("draws ").Append(I(n)).Append('\n');
        foreach (InstitutionMarker m in f.Institutions)
            sb.Append("inst ").Append(I(m.Settlement)).Append(' ').Append(m.Key).Append(' ').Append(I(m.Count)).Append('\n');
        foreach (PathPiece pp in f.Paths)
            sb.Append("path ").Append(I(pp.NetworkEdge)).Append(' ').Append(I(pp.Route)).Append(' ').Append(I(pp.EdgeType))
              .Append(' ').Append(R(pp.From)).Append(' ').Append(R(pp.To)).Append(' ').Append(I(pp.Commands)).Append('\n');
        foreach (UnitPlacement up in f.UnitPlacements)
            sb.Append("place ").Append(I(up.Id)).Append(' ').Append(R(up.X)).Append(' ').Append(R(up.Y)).Append(' ').Append(I(up.Slot)).Append('\n');
        return sb.ToString();
    }

    /// <summary>One camera: the window, the zoom as a multiple of the fit zoom (<c>0</c> = the camera's maximum zoom;
    /// the real camera clamps both) and the settlement row to centre on (<c>-1</c> = the fit view's centre; past the
    /// last row = the last settlement).</summary>
    public sealed record View(string Name, int W, int H, double FitTimes, int CentreOnSettlementRow);

    /// <summary>The cameras every digest paints at the 1280x800 design size: the fit (World band), 3x the fit
    /// (Regional), 10x the fit (Settlement) on the first settlement, and the maximum zoom on the last one.</summary>
    public static readonly View[] Views =
    [
        new("fit", 1280, 800, 1.0, -1),
        new("regional-first", 1280, 800, 3.0, 0),
        new("settlement-first", 1280, 800, 10.0, 0),
        new("max-last", 1280, 800, 0.0, int.MaxValue),
    ];

    /// <summary>The paint of <paramref name="p"/> through <paramref name="v"/>: (SVG bytes' SHA-256, LensFrame text).</summary>
    public static (string SvgSha, string Frame) Paint(WorldProjection p, View v)
    {
        var cam = new Camera(p.WorldSize);
        cam.Clamp(v.W, v.H);
        double target = v.FitTimes > 0 ? v.FitTimes * cam.Zoom : Camera.MaxZoom;
        cam.ZoomAt(v.W / 2.0, v.H / 2.0, target / cam.Zoom, v.W, v.H);
        if (v.CentreOnSettlementRow >= 0 && p.Settlements.Count > 0)
        {
            SettlementLensView s = p.Settlements[Math.Min(v.CentreOnSettlementRow, p.Settlements.Count - 1)];
            cam.Pan((cam.CenterX - s.X) * cam.Zoom, (cam.CenterY - s.Y) * cam.Zoom, v.W, v.H);
        }
        MapDetail level = MapDetail.At(cam.Zoom, v.W, v.H, p.KmPerPx);   // P2: the game's continuous detail (km-keyed)
        var d = new DrawList();
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, level, (x, y) => cam.WorldToScreen(x, y, v.W, v.H), cam.Zoom,
            new RectD(0, 0, v.W, v.H), selected: p.Settlements.Count > 0 ? p.Settlements[0].Id : -1);
        return (Sha(SvgWriter.Write(d, v.W, v.H)), Of(f));
    }

    /// <summary>One line per view ("label view zoom BAND svg SHA frame SHA"), after one line for the projection.</summary>
    public static IEnumerable<string> Lines(string label, WorldProjection p)
    {
        yield return label + " projection " + Sha(Of(p));
        foreach (View v in Views)
        {
            (string svg, string frame) = Paint(p, v);
            yield return label + " " + v.Name + " " + frame[..frame.IndexOf('\n')] + " svg " + svg + " frame " + Sha(frame);
        }
    }
}
