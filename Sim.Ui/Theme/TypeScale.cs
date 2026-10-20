using Sim.Ui.Render;

namespace Sim.Ui.Theme;

/// <summary>
/// The ROLE a run of text plays on the screen (M5 polish, UI readability UR-1). Sizes are set by role, never by a
/// literal at the call site: a screen asks for "the body of a row" or "a caption under it", and
/// <see cref="TypeScale"/> answers with the pixel size for the face it is set in. Ordered from the largest to the
/// smallest; <see cref="Caption"/> is the FLOOR — nothing that carries information is set below it.
/// </summary>
public enum TypeRole
{
    /// <summary>Screen title, the Age banner, a modal's title.</summary>
    Display = 0,
    /// <summary>A panel's subject: the settlement's name, the node's name, the Age's name.</summary>
    Title = 1,
    /// <summary>A block heading inside a panel or a detail section.</summary>
    Heading = 2,
    /// <summary>Sentences, rows, control labels.</summary>
    Body = 3,
    /// <summary>Figures in rows (the lining-figure face).</summary>
    Data = 4,
    /// <summary>The one to four primary figures a panel or the status band exists for.</summary>
    Kpi = 5,
    /// <summary>Explanations, units, "makes …" lines.</summary>
    Secondary = 6,
    /// <summary>Meta, chips, legends, timestamps — the floor.</summary>
    Caption = 7,
}

/// <summary>
/// THE TYPOGRAPHIC SCALE (docs: the M5 polish UI-readability spec §6.2) — the pixel size of every
/// <see cref="TypeRole"/> at the 1080p reference (UI scale s = 1), per face. Sizes are set by X-HEIGHT, so a
/// role reads the same size in EB Garamond (x-height 0.400 em) and in IBM Plex (0.516 em): Body is 20 px of
/// Garamond and 16 px of Plex, both an 8 px x-height. The era theme's <see cref="TypographyTokens.SizeScale"/>
/// (never below 1.0) and the UI scale multiply these; nothing multiplies them DOWN.
///
/// Pure and constant: the table is the tuning surface. The rasterised atlas holds every size of this table at the
/// current UI scale (<see cref="Sim.Ui.Art.UiTheme.LoadFonts"/>), so a role's run is never a minified raster.
/// </summary>
public static class TypeScale
{
    /// <summary>EB Garamond's x-height per em (OS/2 sxHeight / unitsPerEm, measured from the shipped TTF).</summary>
    public const double GaramondXHeight = 0.400;

    /// <summary>IBM Plex Serif / Sans x-height per em (OS/2, measured).</summary>
    public const double PlexXHeight = 0.516;

    /// <summary>The smallest size any information-carrying run may be set at in Garamond (the Caption floor).</summary>
    public const double FloorGaramondPx = 15.0;

    /// <summary>The smallest size any information-carrying run may be set at in Plex (the Caption floor).</summary>
    public const double FloorPlexPx = 13.0;

    // Columns: Garamond (A1–A7 labels and prose), Plex (numerals in every era; A8–A9 labels). Rows in TypeRole order.
    private static readonly double[] Garamond = [34, 27, 21, 20, 22, 31, 17, 15];
    private static readonly double[] Plex = [26, 21, 16, 16, 17, 24, 14, 13];
    // Capitals are set smaller than mixed case of the same role (they have no ascenders to carry them): a caps
    // heading is 16/13, a caps caption 15/14 (the floor for caps in Plex is 14, not 13).
    private static readonly double[] GaramondCaps = [30, 24, 16, 17, 22, 31, 15, 15];
    private static readonly double[] PlexCaps = [24, 19, 13, 14, 17, 24, 14, 14];

    /// <summary>The size of <paramref name="role"/> set in <paramref name="face"/> at UI scale 1 (and era scale 1);
    /// <paramref name="caps"/> for a run the era sets in capitals.</summary>
    public static double Px(TypeRole role, TypeFace face, bool caps = false)
    {
        int i = (int)role;
        if (i < 0 || i >= Garamond.Length) throw new ArgumentOutOfRangeException(nameof(role), role, "no such type role");
        bool plex = face != TypeFace.Garamond;
        return caps ? (plex ? PlexCaps[i] : GaramondCaps[i]) : (plex ? Plex[i] : Garamond[i]);
    }

    /// <summary>The size of <paramref name="role"/> in the face <paramref name="theme"/> sets <paramref name="font"/>
    /// in (its caps flag included), at UI scale 1 — the design size a DrawList painter hands
    /// <see cref="ThemeText.Write"/>, which applies the era's size scale.</summary>
    public static double Px(EraTheme theme, TypeRole role, FontRole font = FontRole.Body)
    {
        TextStyle style = theme.Type.For(font);
        return Px(role, style.Face, style.Case == TextCase.Upper);
    }

    /// <summary>The x-height in px of a run of <paramref name="px"/> in <paramref name="face"/>.</summary>
    public static double XHeight(TypeFace face, double px) => px * (face == TypeFace.Garamond ? GaramondXHeight : PlexXHeight);

    /// <summary>The Caption floor for <paramref name="face"/>: no information-carrying run is set below it.</summary>
    public static double Floor(TypeFace face) => face == TypeFace.Garamond ? FloorGaramondPx : FloorPlexPx;

    /// <summary>Every size the table names for <paramref name="face"/> (mixed case and caps), ascending, distinct.</summary>
    public static double[] SizesFor(TypeFace face)
    {
        double[] a = face == TypeFace.Garamond ? Garamond : Plex, b = face == TypeFace.Garamond ? GaramondCaps : PlexCaps;
        var all = new List<double>(a.Length + b.Length);
        foreach (double v in a) if (!all.Contains(v)) all.Add(v);
        foreach (double v in b) if (!all.Contains(v)) all.Add(v);
        all.Sort();
        return [.. all];
    }

    /// <summary>The roles in table order (largest first).</summary>
    public static IReadOnlyList<TypeRole> Roles { get; } =
        [TypeRole.Display, TypeRole.Title, TypeRole.Heading, TypeRole.Body, TypeRole.Data, TypeRole.Kpi, TypeRole.Secondary, TypeRole.Caption];
}
