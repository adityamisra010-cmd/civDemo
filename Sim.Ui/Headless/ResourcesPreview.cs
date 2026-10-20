using System.Globalization;
using System.Security.Cryptography;
using Sim.Core.Kernel;
using Sim.Ui.Art;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Headless;

/// <summary>
/// HEADLESS EVIDENCE of the M5 resources view (sim-ui --resources-preview [dir]): REAL frames of the game's UI, driven by the
/// headless harness on the one-AI canonical world (seed 42) after three End Turns, rasterized to PNG by
/// <see cref="FrameRaster"/> (the chrome and its text as ImGui draws them; the map and parchment textures as a flat
/// stand-in). Writes: the status band with the resource strip and a group's hover card (1280×800, and the band alone at
/// the smallest window 1080×640), and the RESOURCES view (the stores table, then scrolled to the trend graph). A log
/// records the world hash and every card and RESOURCES line drawn. Never a player surface.
/// </summary>
public static class ResourcesPreview
{
    /// <summary>Writes the previews into <paramref name="outDir"/>; returns the files written.</summary>
    public static IReadOnlyList<string> Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        string assets = AssetManifest.DefaultRoot();
        string work = Path.Combine(Path.GetTempPath(), "sim-ui-resources-preview-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        var written = new List<string>();
        var log = new List<string> { "resources preview: the real GameUi frame, one-AI canonical world (seed 42), three End Turns" };
        try
        {
            UiSession session = UiSession.Start(42, aiEmpiresOverride: 1);
            for (int t = 0; t < 3; t++) session.EndTurn();
            log.Add("turn " + PlayerTurn.Current(session.World.Clock.Turn).ToString(CultureInfo.InvariantCulture)
                + ", world hash " + WorldHash.ComputeHex(session.World));

            using (UiFrameHarness h = UiFrameHarness.Start(session, Path.Combine(work, "wide"), assets))
            {
                h.Idle(3);
                StoreGroup group = h.Ui.Stores!.Group(StoreGroup.Materials) is not null ? StoreGroup.Materials : StoreGroup.Food;
                UiControl c = h.Ui.Controls.Find(GameUi.StripControl(group))!.Value;
                h.MoveTo((c.X0 + c.X1) / 2.0, (c.Y0 + c.Y1) / 2.0);
                log.Add("== strip hover card (" + group + ")");
                log.AddRange(h.Ui.StripCardLines);
                ArtImage frame = FrameRaster.Rasterize(h.Width, h.Height);
                int bottom = (int)Math.Min(h.Height, Math.Ceiling(h.Ui.StripCardRect.Y + h.Ui.StripCardRect.H + 16));
                written.Add(Save(outDir, "resources-strip-hover.png", Crop(frame, 0, 0, h.Width, bottom), log));

                h.MoveTo(h.Width / 2.0, h.Height - 120);
                h.ClickControl("nav:Resources");
                StoreGood? timber = null;
                foreach (StoreGood g in h.Ui.Stores!.Shown) if (g.Name == "timber") timber = g;
                if (timber is not null) h.ClickInContext(GameUi.StoreRowControl(timber.Good));
                h.MoveTo(h.Width / 2.0, h.Height - 120);
                h.Idle(3);
                log.Add("== RESOURCES");
                log.AddRange(h.Ui.FrameText.Texts(FrameSurface.Context));
                written.Add(Save(outDir, "resources-view.png", FrameRaster.Rasterize(h.Width, h.Height), log));
                h.ScrollContextTo("trend-empire");
                PanelRect ctx = h.Ui.ContextRect;
                for (int k = 0; k < 12; k++) h.Wheel(ctx.X + ctx.Width / 2, ctx.Y + ctx.Height / 2, -3);   // to the graph
                h.MoveTo(h.Width / 2.0, h.Height - 120);
                h.Idle(3);
                written.Add(Save(outDir, "resources-view-trends.png", FrameRaster.Rasterize(h.Width, h.Height), log));
            }

            using (UiFrameHarness n = UiFrameHarness.Start(session, Path.Combine(work, "narrow"), assets, developer: false,
                PanelLayout.MinWindowWidth, PanelLayout.MinWindowHeight))
            {
                n.Idle(3);
                log.Add("== the smallest window: status band text");
                log.AddRange(n.Ui.FrameText.Texts(FrameSurface.Status));
                ArtImage frame = FrameRaster.Rasterize(n.Width, n.Height);
                written.Add(Save(outDir, "resources-strip-narrow.png", Crop(frame, 0, 0, n.Width, 70), log));
            }
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        File.WriteAllLines(Path.Combine(outDir, "preview-log.txt"), log);
        return written;
    }

    private static string Save(string dir, string name, ArtImage image, List<string> log)
    {
        string path = Path.Combine(dir, name);
        PngCodec.Write(path, image);
        log.Add("  " + name + "  " + image.Width.ToString(CultureInfo.InvariantCulture) + "x" + image.Height.ToString(CultureInfo.InvariantCulture)
            + "  sha256 " + Convert.ToHexStringLower(SHA256.HashData(image.Rgba)));
        return path;
    }

    private static ArtImage Crop(ArtImage src, int x, int y, int w, int h)
    {
        w = Math.Min(w, src.Width - x);
        h = Math.Min(h, src.Height - y);
        var rgba = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
            Array.Copy(src.Rgba, ((y + row) * src.Width + x) * 4, rgba, row * w * 4, w * 4);
        return new ArtImage(w, h, rgba);
    }
}
