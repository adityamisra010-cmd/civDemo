using System.Globalization;
using System.Text;

namespace Sim.Ui.Art;

/// <summary>
/// THE DROP AUDIT: what the art folder actually contains versus what the
/// manifest promises. Run after every art drop (`sim-ui --audit-assets`) —
/// it answers three questions the eye cannot answer reliably:
///   1. which manifest keys resolve to REAL art;
///   2. which are still programmatic stand-ins (byte-identical to what
///      PlaceholderArt would generate — the only honest definition);
///   3. which FILES are orphaned: present in assets/ but referenced by no
///      manifest key, so the renderer silently ignores them. A misnamed drop
///      (deep.png → deepsea.png) looks like a delivered asset and behaves like
///      a missing one; this is what catches that.
/// </summary>
public static class AssetAudit
{
    public sealed record KeyStatus(
        string Key, string RelativePath, bool FileExists, bool IsPlaceholder,
        int Width, int Height, string? Note, bool Optional = false,
        string? AlphaFault = null);

    public sealed record Report(
        string Root,
        IReadOnlyList<KeyStatus> Keys,
        IReadOnlyList<string> OrphanFiles)
    {
        /// <summary>Keys whose art cannot carry the transparency the renderer
        /// composites it with — a JPEG re-exported under a .png name, or an
        /// RGBA file that is actually an opaque rectangle. Either way the
        /// device draws as a BOX over the map, which is the failure the eye
        /// notices last and the audit must notice first.</summary>
        public IEnumerable<KeyStatus> AlphaFaults => Keys.Where(k => k.AlphaFault is not null);

        /// <summary>Keys that MUST be satisfied — optional parchment variants
        /// beyond the primary sheet are excluded, so "one sheet for every seed"
        /// is a complete drop rather than a two-thirds-missing one.</summary>
        public IEnumerable<KeyStatus> Required => Keys.Where(k => !k.Optional);
        public int RealCount => Required.Count(k => k.FileExists && !k.IsPlaceholder);
        public int PlaceholderCount => Required.Count() - RealCount;

        public string Render()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"assets root: {Root}");
            int required = Required.Count();
            int optionalProvided = Keys.Count(k => k.Optional && k.FileExists && !k.IsPlaceholder);
            int optionalMissing = Keys.Count(k => k.Optional && !k.FileExists);
            string optionalNote =
                (optionalProvided > 0 ? $" (+{optionalProvided} optional variant(s) PROVIDED" : "")
                + (optionalProvided > 0 && optionalMissing > 0 ? ", " : "")
                + (optionalMissing > 0
                    ? (optionalProvided > 0 ? $"{optionalMissing} not provided)"
                                            : $" (+{optionalMissing} optional variant(s) not provided)")
                    : (optionalProvided > 0 ? ")" : ""));
            sb.AppendLine(
                $"art: {PlaceholderCount.ToString(CultureInfo.InvariantCulture)}/" +
                $"{required.ToString(CultureInfo.InvariantCulture)} PLACEHOLDER, " +
                $"{RealCount.ToString(CultureInfo.InvariantCulture)} real{optionalNote}");
            sb.AppendLine();
            sb.AppendLine("key                       state        size        note");
            foreach (KeyStatus k in Keys)
            {
                string state = !k.FileExists ? (k.Optional ? "-" : "MISSING")
                    : k.IsPlaceholder ? "placeholder" : "REAL";
                string size = k.FileExists
                    ? string.Create(CultureInfo.InvariantCulture, $"{k.Width}x{k.Height}")
                    : "-";
                string note = k.AlphaFault is null ? (k.Note ?? "") : $"!! {k.AlphaFault}";
                sb.AppendLine($"{k.Key,-25} {state,-12} {size,-11} {note}");
            }
            if (AlphaFaults.Any())
            {
                sb.AppendLine();
                sb.AppendLine("TRANSPARENCY FAULTS (these keys composite over the map or a panel and");
                sb.AppendLine("MUST carry an alpha channel — as delivered they draw as opaque boxes):");
                foreach (KeyStatus k in AlphaFaults) sb.AppendLine($"  {k.Key}: {k.AlphaFault}");
            }
            if (OrphanFiles.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ORPHANED FILES (present in assets/, referenced by NO manifest key —");
                sb.AppendLine("the renderer NEVER loads these; a drop that lands here looks delivered");
                sb.AppendLine("and behaves missing. Check for a doubled extension (foo.png.jpg), a");
                sb.AppendLine("misspelling, or a name the manifest does not alias):");
                foreach (string o in OrphanFiles) sb.AppendLine($"  {o}");
            }
            return sb.ToString();
        }
    }

    public static Report Run(string? root = null)
    {
        root ??= AssetManifest.DefaultRoot();
        var keys = new List<KeyStatus>();
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (AssetManifest.Entry entry in AssetManifest.All)
        {
            string? resolved = AssetManifest.Resolve(root, entry);
            if (resolved is not null) referenced.Add(Path.GetFullPath(resolved));

            // A parchment variant beyond the primary is OPTIONAL: the bible
            // allows 2–3 sheets but does not require them, and the director
            // generated one.
            bool optional = entry.Kind == AssetManifest.AssetKind.ParchmentBase && entry.Variant > 0;
            if (resolved is null)
            {
                keys.Add(new KeyStatus(entry.Key, entry.RelativePath, false, true, 0, 0,
                    optional ? "optional variant — not provided; every seed uses the primary sheet"
                             : "no file at any accepted name",
                    optional));
                continue;
            }
            try
            {
                ArtImage img = PngCodec.Read(resolved);
                ArtImage stand = PlaceholderArt.Generate(entry);
                bool isPlaceholder = img.Width == stand.Width && img.Height == stand.Height
                                     && img.Rgba.AsSpan().SequenceEqual(stand.Rgba);
                string? note = Path.GetFileName(resolved) == Path.GetFileName(entry.RelativePath)
                    ? null
                    : $"resolved via alias '{Path.GetFileName(resolved)}'";
                // TRANSPARENCY CHECK — only meaningful on REAL art (a
                // placeholder is a stand-in and reports as such already).
                string? alphaFault = null;
                if (entry.RequiresAlpha && !isPlaceholder)
                {
                    if (!img.SourceHadAlpha)
                        alphaFault = "source file has NO alpha channel (8-bit RGB PNG, or a JPEG " +
                                     "re-exported under a .png name) — it cannot be transparent";
                    else if (!img.CornersTransparent)
                        alphaFault = "has an alpha channel but its corners are OPAQUE (" +
                            img.TransparentFraction.ToString("P1", CultureInfo.InvariantCulture) +
                            " of pixels transparent) — draws as a box";
                    else if (img.TransparentFraction < 0.02)
                        alphaFault = "only " +
                            img.TransparentFraction.ToString("P2", CultureInfo.InvariantCulture) +
                            " of pixels are transparent — effectively an opaque plate";
                }
                keys.Add(new KeyStatus(entry.Key, entry.RelativePath, true, isPlaceholder,
                    img.Width, img.Height, note, optional, alphaFault));
            }
            catch (Exception e)
            {
                keys.Add(new KeyStatus(entry.Key, entry.RelativePath, true, true, 0, 0,
                    $"UNREADABLE: {e.Message}"));
            }
        }

        // ALL files, not *.png: a mis-EXTENSIONED drop (header-rule.png.jpg —
        // a JPEG export beside the placeholder it meant to replace) is exactly
        // the "looks delivered, behaves missing" failure this audit exists to
        // catch, and a .png glob is blind to it. Fonts (loaded by UiTheme, not
        // the manifest) and drop-point docs are the only legitimate
        // non-manifest files.
        var orphans = new List<string>();
        if (Directory.Exists(root))
        {
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (rel.StartsWith("fonts/", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                if (!referenced.Contains(Path.GetFullPath(file))) orphans.Add(rel);
            }
            orphans.Sort(StringComparer.Ordinal);
        }
        return new Report(root, keys, orphans);
    }
}
