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
        int Width, int Height, string? Note, bool Optional = false);

    public sealed record Report(
        string Root,
        IReadOnlyList<KeyStatus> Keys,
        IReadOnlyList<string> OrphanFiles)
    {
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
            string optionalNote = Keys.Count > required
                ? $" (+{Keys.Count - required} optional variant(s) not provided)"
                : "";
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
                sb.AppendLine($"{k.Key,-25} {state,-12} {size,-11} {k.Note ?? ""}");
            }
            if (OrphanFiles.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ORPHANED FILES (present in assets/, referenced by NO manifest key —");
                sb.AppendLine("the renderer never loads these):");
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
                keys.Add(new KeyStatus(entry.Key, entry.RelativePath, true, isPlaceholder,
                    img.Width, img.Height, note, optional));
            }
            catch (Exception e)
            {
                keys.Add(new KeyStatus(entry.Key, entry.RelativePath, true, true, 0, 0,
                    $"UNREADABLE: {e.Message}"));
            }
        }

        var orphans = new List<string>();
        if (Directory.Exists(root))
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories))
                if (!referenced.Contains(Path.GetFullPath(file)))
                    orphans.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
            orphans.Sort(StringComparer.Ordinal);
        }
        return new Report(root, keys, orphans);
    }
}
