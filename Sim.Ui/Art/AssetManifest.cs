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
    /// <param name="AlternateFileNames">Other file names this key ACCEPTS, in
    /// preference order after the primary. Real art arrives named however the
    /// generating session named it; an alias costs nothing and turns a silent
    /// "asset missing" (a misnamed drop looks delivered but renders as a
    /// stand-in) into a normal load. The audit reports which name resolved.</param>
    public sealed record Entry(
        string Key, string RelativePath, AssetKind Kind, bool Tileable,
        int Variant = 0, ParchmentPalette.TerrainClass? TerrainClass = null,
        string[]? AlternateFileNames = null)
    {
        /// <summary>
        /// Does this key's art have to CARRY TRANSPARENCY? True only for DRAWN
        /// DEVICES — a shape composited over other content, where an opaque
        /// rectangle would be visibly wrong: the compass rose and settlement
        /// marker (over the map), the header rule (over a panel), the coast
        /// hairline (over the washes).
        ///
        /// Deliberately FALSE for everything that is a SHEET rather than a
        /// device: the parchment, the grain, the terrain washes, the Annals
        /// background — and also ui/panel and ui/button-plate, which are
        /// parchment PLATES drawn behind a window and a button. Those are
        /// opaque on purpose (the current placeholders measure 0% transparent),
        /// so requiring alpha of them would raise a false fault the first time
        /// the director drops real plate art.
        ///
        /// Derived from the KIND, not listed per entry, so a new asset of an
        /// existing kind inherits the requirement automatically.
        /// </summary>
        public bool RequiresAlpha => Kind is
            AssetKind.CoastHairline or AssetKind.UiHeaderRule or
            AssetKind.UiCompassRose or AssetKind.SettlementMarker;
    }

    /// <summary>How many parchment variants the bible allows ("2–3 variants;
    /// renderer picks one per world seed"). VARIANTS ARE OPTIONAL: a single
    /// sheet at parchment/parchment.png serves every seed (see
    /// <see cref="ParchmentPrimary"/>).</summary>
    public const int ParchmentVariants = 3;

    /// <summary>THE single-sheet paper. When this file exists it is the paper
    /// for EVERY world seed and the numbered variants are ignored entirely —
    /// the director generated one sheet, and "all three identical" is the
    /// intent, so one file expresses it without three copies of a 2.6 MB PNG
    /// (and without 2/3 of seeds silently drawing on placeholder paper because
    /// a base-N stand-in still sat beside the real sheet).</summary>
    public const string ParchmentPrimary = "parchment/parchment.png";

    public static readonly IReadOnlyList<Entry> All = Build();

    private static Entry[] Build()
    {
        var entries = new List<Entry>();
        // The primary sheet first: variant 0 accepts parchment.png OR base-0.png.
        entries.Add(new Entry("parchment/base-0", ParchmentPrimary,
            AssetKind.ParchmentBase, Tileable: true, Variant: 0,
            AlternateFileNames: ["base-0.png", "base0.png"]));
        // UNHYPHENATED ALIASES (third occurrence of this failure class, after
        // deepsea.png and header-rule.png.jpg): the director's variant sheets
        // arrived as base1.png / base2.png while the manifest asked for
        // base-1.png / base-2.png, so 4.9 MB of real paper sat orphaned and
        // every seed kept drawing on the primary sheet. The audit named them;
        // an alias costs nothing and closes the class for good.
        for (int v = 1; v < ParchmentVariants; v++)
            entries.Add(new Entry($"parchment/base-{v}", $"parchment/base-{v}.png",
                AssetKind.ParchmentBase, Tileable: true, Variant: v,
                AlternateFileNames: [$"base{v}.png"]));
        entries.Add(new Entry("parchment/grain", "parchment/grain.png", AssetKind.Grain, Tileable: true));

        for (int c = 0; c < ParchmentPalette.TerrainClassCount; c++)
        {
            var cls = (ParchmentPalette.TerrainClass)c;
            string name = ParchmentPalette.TileName(cls);
            entries.Add(new Entry($"terrain/{name}", $"terrain/{name}.png",
                AssetKind.TerrainWash, Tileable: true, TerrainClass: cls,
                AlternateFileNames: TileAliases(cls)));
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

    /// <summary>Alternate names a terrain wash accepts. 'deepsea.png' is the
    /// name the director's generation session produced for the deep-sea wash;
    /// both spellings resolve so a regenerated batch drops in unchanged.</summary>
    private static string[]? TileAliases(ParchmentPalette.TerrainClass cls) => cls switch
    {
        ParchmentPalette.TerrainClass.Deep => ["deepsea.png", "deep-sea.png"],
        ParchmentPalette.TerrainClass.Shallows => ["shallow.png"],
        ParchmentPalette.TerrainClass.Lowland => ["lowland-green.png"],
        ParchmentPalette.TerrainClass.Fertile => ["fertile-green.png"],
        ParchmentPalette.TerrainClass.Upland => ["upland-umber.png"],
        ParchmentPalette.TerrainClass.Peak => ["peak-pale.png"],
        ParchmentPalette.TerrainClass.Plain => ["plain-tan.png"],
        _ => null,
    };

    /// <summary>The path this entry loads from, or null if no accepted name
    /// exists on disk. Primary first, then each alias in order.</summary>
    public static string? Resolve(string root, Entry entry)
    {
        string primary = Path.Combine(root, entry.RelativePath);
        if (File.Exists(primary)) return primary;
        if (entry.AlternateFileNames is { } alternates)
        {
            string dir = Path.GetDirectoryName(Path.Combine(root, entry.RelativePath))!;
            foreach (string name in alternates)
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    /// <summary>Parchment variants that ACTUALLY resolved, ascending. When the
    /// single primary sheet is the only one present, every seed gets it.</summary>
    public static IReadOnlyList<Entry> ResolvedParchmentVariants(string root)
    {
        var found = new List<Entry>();
        foreach (Entry e in All)
            if (e.Kind == AssetKind.ParchmentBase && Resolve(root, e) is not null) found.Add(e);
        return found;
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
