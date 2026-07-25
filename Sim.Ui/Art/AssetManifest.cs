namespace Sim.Ui.Art;

/// <summary>
/// THE ASSET CONTRACT (style-bible §4). Every drawable the substrate renderer
/// can ask for is declared here once: a logical key, the path it lives at
/// under assets/, and what kind of thing it is. The renderer addresses art by
/// KEY and never by path, so replacing a placeholder with the director's
/// generated art is a file drop — no code change, no rebuild of the manifest.
///
/// The manifest is CODE, not data, deliberately: it is the compile-time list
/// of what the bible promises, so a missing entry is a build error rather
/// than a silent blank. What is DATA is the art itself.
/// </summary>
public static class AssetManifest
{
    public enum AssetKind
    {
        ParchmentBase, Grain, TerrainWash, CoastHairline,
        UiPanel, UiHeaderRule, UiButtonPlate, UiAnnalsBackground, UiCompassRose,
        SettlementMarker,
    }

    /// <param name="Key">Logical name the renderer asks for.</param>
    /// <param name="RelativePath">Path under the assets root.</param>
    /// <param name="Tileable">Subject to the §4/§5 SEAMLESS CLAUSE — the
    /// 2×2 edge-wrap test applies (enforced by AssetSeamTests).</param>
    /// <param name="Variant">Parchment base variant index (renderer picks by seed).</param>
    /// <param name="TerrainClass">Set for TerrainWash entries only.</param>
    public sealed record Entry(
        string Key, string RelativePath, AssetKind Kind, bool Tileable,
        int Variant = 0, ParchmentPalette.TerrainClass? TerrainClass = null);

    /// <summary>How many parchment variants the bible asks for ("2–3 variants;
    /// renderer picks one per world seed").</summary>
    public const int ParchmentVariants = 3;

    public static readonly IReadOnlyList<Entry> All = Build();

    private static Entry[] Build()
    {
        var entries = new List<Entry>();
        for (int v = 0; v < ParchmentVariants; v++)
            entries.Add(new Entry($"parchment/base-{v}", $"parchment/base-{v}.png",
                AssetKind.ParchmentBase, Tileable: true, Variant: v));
        entries.Add(new Entry("parchment/grain", "parchment/grain.png", AssetKind.Grain, Tileable: true));

        for (int c = 0; c < ParchmentPalette.TerrainClassCount; c++)
        {
            var cls = (ParchmentPalette.TerrainClass)c;
            string name = ParchmentPalette.TileName(cls);
            entries.Add(new Entry($"terrain/{name}", $"terrain/{name}.png",
                AssetKind.TerrainWash, Tileable: true, TerrainClass: cls));
        }

        entries.Add(new Entry("ink/coast-hairline", "ink/coast-hairline.png", AssetKind.CoastHairline, Tileable: false));
        entries.Add(new Entry("ui/panel", "ui/panel.png", AssetKind.UiPanel, Tileable: false));
        entries.Add(new Entry("ui/header-rule", "ui/header-rule.png", AssetKind.UiHeaderRule, Tileable: false));
        entries.Add(new Entry("ui/button-plate", "ui/button-plate.png", AssetKind.UiButtonPlate, Tileable: false));
        entries.Add(new Entry("ui/annals-bg", "ui/annals-bg.png", AssetKind.UiAnnalsBackground, Tileable: false));
        entries.Add(new Entry("ui/compass-rose", "ui/compass-rose.png", AssetKind.UiCompassRose, Tileable: false));
        entries.Add(new Entry("ui/settlement-marker", "ui/settlement-marker.png", AssetKind.SettlementMarker, Tileable: false));
        return [.. entries];
    }

    public static Entry Require(string key)
    {
        foreach (Entry e in All) if (e.Key == key) return e;
        throw new KeyNotFoundException(
            $"asset key '{key}' is not in the manifest — add it to AssetManifest.Build().");
    }

    public static Entry Terrain(ParchmentPalette.TerrainClass cls) =>
        Require($"terrain/{ParchmentPalette.TileName(cls)}");

    /// <summary>The assets root: &lt;exe dir&gt;/assets, falling back to the repo
    /// checkout when running from a test binary (so headless tests read the
    /// same files the game ships).</summary>
    public static string DefaultRoot()
    {
        string beside = Path.Combine(AppContext.BaseDirectory, "assets");
        if (Directory.Exists(beside)) return beside;
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "assets");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return beside; // missing — AssetLibrary substitutes labeled placeholders
    }
}
