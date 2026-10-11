namespace Sim.Ui.World;

/// <summary>
/// P2 (M5 map workstream) — THE CONTINUOUS LEVEL OF DETAIL, keyed on how much ground the player SEES: the visible km
/// across the map rect's shorter side (screen px ÷ zoom × the terrain's km per px — <c>TerrainSet.KmPerPx</c>, read
/// from state, never assumed). It replaces the three hard-switched levels with two cross-fades:
/// <list type="bullet">
/// <item><see cref="Regional"/> rises 0 → 1 as the view closes from above <see cref="WorldRegionalKm"/> to below it,
///   over <see cref="FadeOctaves"/> octaves either side of it (km × 2^±0.25);</item>
/// <item><see cref="Settlement"/> rises 0 → 1 the same way about <see cref="RegionalSettlementKm"/>.</item>
/// </list>
/// Each ramp is a smoothstep in log2 km: exactly 0 and 1 outside its window (the PLATEAUS), continuous and monotone
/// inside it. The windows do not overlap, so at most one cross-fade is under way at any scale. Every information
/// layer's opacity is a function of the two (<see cref="Alpha"/>): continuous everywhere and monotone across each
/// window, so no layer pops in or out; on a plateau it is exactly 1 for the layers of that band
/// (<see cref="WorldLens.LayersFor"/>) and 0 for the rest, and the lens paints exactly what the banded lens painted.
///
/// THE DERIVED BAND (<see cref="Band"/>, <see cref="BandFor"/>) — kept for the gate, the tests, the legend title and
/// the hit tests that read a level: World above <see cref="WorldRegionalKm"/> km, Regional above
/// <see cref="RegionalSettlementKm"/> km, Settlement at or below it — the switch is at the middle of each cross-fade.
/// The two edges are the banded lens's switch points on the canonical world (55 % and 16 % of its 1024 px × 4 km =
/// 4096 km: 2252.8 and 655.4 km, rounded to the km), so the game's canonical map changes band at the same zooms as
/// before; a smaller world (the 256-px test and dev worlds, 1024 km) is now banded by the ground it shows, not by its
/// share of itself.
/// </summary>
public readonly record struct MapDetail(WorldZoom Band, double Regional, double Settlement, double VisibleKm)
{
    /// <summary>The World ↔ Regional edge, in visible km (the canonical world's 55 % of 4096 km, to the km).</summary>
    public const double WorldRegionalKm = 2253;

    /// <summary>The Regional ↔ Settlement edge, in visible km (the canonical world's 16 % of 4096 km, to the km).</summary>
    public const double RegionalSettlementKm = 655;

    /// <summary>Half-width of each cross-fade window, in octaves of visible km (2^0.25 ≈ 1.19: just over one wheel
    /// notch of ×1.25 across the whole window).</summary>
    public const double FadeOctaves = 0.25;

    /// <summary>The visible km across the shorter side of a <paramref name="viewW"/> × <paramref name="viewH"/> map rect at
    /// <paramref name="zoom"/> screen px per world px, with <paramref name="kmPerPx"/> km per world px.</summary>
    public static double VisibleKmOf(double zoom, double viewW, double viewH, double kmPerPx) =>
        Math.Min(viewW, viewH) / Math.Max(1e-9, zoom) * kmPerPx;

    /// <summary>The derived band for a visible span: World above <see cref="WorldRegionalKm"/>, Regional above
    /// <see cref="RegionalSettlementKm"/>, Settlement otherwise.</summary>
    public static WorldZoom BandFor(double visibleKm) =>
        visibleKm > WorldRegionalKm ? WorldZoom.World : visibleKm > RegionalSettlementKm ? WorldZoom.Regional : WorldZoom.Settlement;

    /// <summary>The detail at a visible span.</summary>
    public static MapDetail At(double visibleKm) =>
        new(BandFor(visibleKm), Ramp(visibleKm, WorldRegionalKm), Ramp(visibleKm, RegionalSettlementKm), visibleKm);

    /// <summary>The detail of a camera: <paramref name="zoom"/> over a map rect of <paramref name="viewW"/> ×
    /// <paramref name="viewH"/>, <paramref name="kmPerPx"/> km per world px.</summary>
    public static MapDetail At(double zoom, double viewW, double viewH, double kmPerPx) =>
        At(VisibleKmOf(zoom, viewW, viewH, kmPerPx));

    /// <summary>The PLATEAU of a band — the detail the banded lens painted at it (no visible span: NaN).</summary>
    public static MapDetail Of(WorldZoom band) => band switch
    {
        WorldZoom.World => new(WorldZoom.World, 0, 0, double.NaN),
        WorldZoom.Regional => new(WorldZoom.Regional, 1, 0, double.NaN),
        _ => new(WorldZoom.Settlement, 1, 1, double.NaN),
    };

    /// <summary>On a plateau (no cross-fade under way): both blends are exactly 0 or 1.</summary>
    public bool IsPlateau => Regional is 0.0 or 1.0 && Settlement is 0.0 or 1.0;

    /// <summary>The cross-fade ramp about <paramref name="edgeKm"/>: 0 above the window, 1 below it, a smoothstep in log2 km
    /// between (exactly 0.5 at the edge).</summary>
    public static double Ramp(double visibleKm, double edgeKm)
    {
        double t = (Math.Log2(edgeKm) + FadeOctaves - Math.Log2(Math.Max(1e-12, visibleKm))) / (2 * FadeOctaves);
        if (!(t > 0)) return 0.0;
        if (t >= 1) return 1.0;
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// The opacity of an information layer at this detail, R = <see cref="Regional"/>, S = <see cref="Settlement"/>:
    /// territories 1 − S; the World band's marks and network 1 − R; roads, morphology and production signals R;
    /// institutional presence R(1 − S); the Settlement band's institution types, infrastructure density and population
    /// scale S; formations and Age banners 1 (every band).
    /// </summary>
    public double Alpha(WorldLayer layer) => layer switch
    {
        WorldLayer.Territories => 1 - Settlement,
        WorldLayer.MajorSettlements or WorldLayer.MajorInfrastructure => 1 - Regional,
        WorldLayer.Roads or WorldLayer.SettlementMorphology or WorldLayer.ProductionSignals => Regional,
        WorldLayer.InstitutionalPresence => Regional * (1 - Settlement),
        WorldLayer.InstitutionTypes or WorldLayer.InfrastructureDensity or WorldLayer.PopulationScale => Settlement,
        _ => 1.0,   // MilitaryFormations, AgeBanners
    };

    /// <summary>The layers drawn at this detail (opacity above 0), in paint order: on a plateau exactly
    /// <see cref="WorldLens.LayersFor"/> of its band (the same array); in a cross-fade the farther band's layers, then
    /// the nearer band's that are not already listed.</summary>
    public IReadOnlyList<WorldLayer> Layers()
    {
        if (IsPlateau) return WorldLens.LayersFor(Band);
        (WorldZoom far, WorldZoom near) = Settlement > 0 ? (WorldZoom.Regional, WorldZoom.Settlement) : (WorldZoom.World, WorldZoom.Regional);
        var layers = new List<WorldLayer>();
        foreach (WorldLayer l in WorldLens.LayersFor(far)) if (Alpha(l) > 0) layers.Add(l);
        foreach (WorldLayer l in WorldLens.LayersFor(near)) if (Alpha(l) > 0 && !layers.Contains(l)) layers.Add(l);
        return layers;
    }

    /// <summary>The endpoint-exact blend: <paramref name="a"/> at t ≤ 0 and <paramref name="b"/> at t ≥ 1 EXACTLY (so a
    /// plateau computes exactly the banded value), a(1 − t) + bt between.</summary>
    public static double Mix(double a, double b, double t) => t <= 0 ? a : t >= 1 ? b : a * (1 - t) + b * t;
}
