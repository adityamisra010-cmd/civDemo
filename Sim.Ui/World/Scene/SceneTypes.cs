using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;

namespace Sim.Ui.World.Scene;

/// <summary>A camera as a plain value: world point at the viewport centre and screen pixels
/// per world unit. The game builds one from its map Camera; the demo overlay and the preview
/// build their own.</summary>
public readonly record struct WorldProjection(double CenterX, double CenterY, double PxPerWorldUnit, double ViewW, double ViewH)
{
    public (double X, double Y) ToScreen(WorldPoint p) =>
        ((p.X - CenterX) * PxPerWorldUnit + ViewW / 2.0, (p.Y - CenterY) * PxPerWorldUnit + ViewH / 2.0);

    public WorldPoint ToWorld(double sx, double sy) =>
        new(CenterX + (sx - ViewW / 2.0) / PxPerWorldUnit, CenterY + (sy - ViewH / 2.0) / PxPerWorldUnit);

    /// <summary>Fit a world rectangle into a screen region (e.g. the part of the view the legend
    /// does not cover), centred in it.</summary>
    public static WorldProjection FitInto(double worldW, double worldH, double rx, double ry, double rw, double rh, double viewW, double viewH)
    {
        double z = Math.Min(rw / worldW, rh / worldH);
        // Screen centre of the region must show the world centre: solve for the camera centre.
        double cx = worldW / 2.0 - (rx + rw / 2.0 - viewW / 2.0) / z;
        double cy = worldH / 2.0 - (ry + rh / 2.0 - viewH / 2.0) / z;
        return new WorldProjection(cx, cy, z, viewW, viewH);
    }

    /// <summary>Fit a world rectangle into the viewport with a margin.</summary>
    public static WorldProjection Fit(double worldW, double worldH, double viewW, double viewH, double marginPx = 24) =>
        new(worldW / 2.0, worldH / 2.0,
            Math.Min((viewW - 2 * marginPx) / worldW, (viewH - 2 * marginPx) / worldH), viewW, viewH);
}

/// <summary>A rectangle in world units.</summary>
public readonly record struct WorldRect(double X, double Y, double W, double H);

/// <summary>The paper a scene is drawn on when the host has no map of its own (the demo overlay,
/// the preview): its extent and decorative water. Not an entity, not a report.</summary>
public sealed record WorldBackdrop(double Width, double Height, IReadOnlyList<WorldRect> Water);

/// <summary>THE LAYER ORDER (task Part 11), fixed: each layer is painted into its own list and
/// the lists are concatenated in this order, so nothing in a later layer can be covered by an
/// earlier one whatever the entity order.</summary>
public enum WorldLayer
{
    Background = 0,
    SettlementFootprint = 1,
    Infrastructure = 2,
    Buildings = 3,
    Resources = 4,
    MobileAgents = 5,
    Labels = 6,
    Selection = 7,
    TransientUi = 8,
}

/// <summary>Where one layer's commands sit in the frame's draw list.</summary>
public readonly record struct LayerSpan(WorldLayer Layer, int Start, int Count);

/// <summary>What the host wants drawn. The demo overlay and the preview draw everything; the
/// live map leaves the terrain, the network, the settlement markers' names and the settlement
/// click to the game, which already draws and handles them.</summary>
public sealed record WorldSceneOptions(
    bool DrawBackground = true,
    bool DrawInfrastructure = true,
    WorldLod FootprintMinLod = WorldLod.Far,
    bool DrawSettlementLabels = true,
    bool HitSettlements = true,
    bool DrawPanels = true,
    double CoreMinPx = 6.0,
    string? BannerExtra = null)
{
    public static readonly WorldSceneOptions Full = new();

    /// <summary>On the live map: over the game's terrain and network, under its name labels.</summary>
    public static readonly WorldSceneOptions LiveOverlay = new(
        DrawBackground: false, DrawInfrastructure: false, FootprintMinLod: WorldLod.Mid, DrawSettlementLabels: false,
        HitSettlements: false, DrawPanels: false, CoreMinPx: 11.0);
}

/// <summary>The view's UI state: selection and hover are view-model state only — never written
/// to the simulation, never to the game's own settlement selection (the game may pass its
/// selected settlement IN, as <see cref="MirrorSettlementKey"/>, to be highlighted).</summary>
public sealed class WorldUiState
{
    public WorldEntityId? Selected { get; set; }
    public WorldEntityId? Hovered { get; set; }
    public string? MirrorSettlementKey { get; set; }
}

/// <summary>One painted frame: the draw list, where each layer sits in it, what can be clicked,
/// and the level of detail it was drawn at.</summary>
public sealed record WorldFrame(DrawList Draw, IReadOnlyList<LayerSpan> Layers, HitIndex Hits, WorldLod Lod, double PxPerUnit)
{
    public LayerSpan Span(WorldLayer layer)
    {
        foreach (LayerSpan s in Layers) if (s.Layer == layer) return s;
        return new LayerSpan(layer, 0, 0);
    }
}

/// <summary>Everything a painter reads, bundled — no painter reaches anything else.</summary>
public sealed record SceneContext(
    WorldView View, WorldMorphology Morph, WorldProjection Proj, WorldLod Lod, double PxPerUnit, WorldUiState Ui,
    WorldSceneOptions Options, ITextMeasure Measure, SceneGeometry Geometry, string? StepLabel);
