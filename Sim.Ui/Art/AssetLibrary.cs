namespace Sim.Ui.Art;

/// <summary>
/// Loads the manifest's art from disk into memory, ONCE, with a hard promise:
/// <b>a missing or unreadable asset never crashes the game</b>. It is replaced
/// by a labeled placeholder (the programmatic stand-in, marked in
/// <see cref="Report"/>), so a half-delivered art drop degrades to "some tiles
/// are stand-ins" instead of a black screen or an exception on the director's
/// machine. The report is surfaced in the debug panel.
///
/// Headless: no MonoGame types here — the Game turns these ArtImages into
/// textures. That keeps every load/fallback path testable without a window.
/// </summary>
public sealed class AssetLibrary
{
    public sealed record Status(string Key, bool Loaded, string? Note);

    private readonly Dictionary<string, ArtImage> _images = [];
    private readonly List<Status> _report = [];

    public string Root { get; }
    public IReadOnlyList<Status> Report => _report;
    public int PlaceholderCount { get; private set; }

    private AssetLibrary(string root) => Root = root;

    public static AssetLibrary Load(string? root = null)
    {
        var library = new AssetLibrary(root ?? AssetManifest.DefaultRoot());
        foreach (AssetManifest.Entry entry in AssetManifest.All)
        {
            string path = Path.Combine(library.Root, entry.RelativePath);
            try
            {
                if (File.Exists(path))
                {
                    library._images[entry.Key] = PngCodec.Read(path);
                    library._report.Add(new Status(entry.Key, true, null));
                    continue;
                }
                library.Substitute(entry, "missing file");
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                // A corrupt or unreadable PNG is exactly the case a crash would
                // be unforgivable: the director dropped a file, it was wrong,
                // and the game must still open and SAY so.
                library.Substitute(entry, e.Message);
            }
        }
        return library;
    }

    private void Substitute(AssetManifest.Entry entry, string why)
    {
        _images[entry.Key] = PlaceholderArt.Generate(entry);
        _report.Add(new Status(entry.Key, false, why));
        PlaceholderCount++;
    }

    /// <summary>The image for a manifest key — always non-null (placeholder if
    /// the file was absent or bad).</summary>
    public ArtImage Get(string key) =>
        _images.TryGetValue(key, out ArtImage? img)
            ? img
            : _images[key] = PlaceholderArt.Generate(AssetManifest.Require(key));

    public ArtImage Terrain(ParchmentPalette.TerrainClass cls) =>
        Get(AssetManifest.Terrain(cls).Key);

    /// <summary>Parchment variant for a world seed — the bible's "renderer
    /// picks one per world seed" (§4 item 1). Deterministic, no RNG.</summary>
    public ArtImage ParchmentFor(ulong worldSeed) =>
        Get($"parchment/base-{(int)(worldSeed % AssetManifest.ParchmentVariants)}");

    /// <summary>One-line status for the debug panel: silence when the art is
    /// complete, a loud count when it is not.</summary>
    public string SummaryLine() => PlaceholderCount == 0
        ? $"art: {_report.Count} assets loaded from {Root}"
        : $"art: {PlaceholderCount}/{_report.Count} PLACEHOLDER (drop real assets into {Root})";
}
