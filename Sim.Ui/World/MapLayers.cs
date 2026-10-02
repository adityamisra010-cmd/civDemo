namespace Sim.Ui.World;

/// <summary>The visual layers of the map, as the player sees them. Each has exactly ONE owner
/// (<see cref="MapLayerOwnership"/>) so nothing is drawn twice.</summary>
public enum MapLayer
{
    Terrain = 0,
    Rivers = 1,
    Territory = 2,
    Paths = 3,
    SettlementMarkers = 4,
    UnitTokens = 5,
    InstitutionMarkers = 6,
    AgeBanners = 7,
}

/// <summary>Who draws a map layer: the legacy GPU map pass in <c>SimUiGame.Draw</c>, or the
/// zoom-dependent <see cref="WorldLens"/>.</summary>
public enum LayerOwner { MapRenderer = 0, WorldLens = 1 }

/// <summary>
/// THE LAYER REGISTRY — the single authority for which renderer owns each map layer. The world
/// lens owns everything that is zoom-dependent (D-047 Part 5): territory (polity-tinted), paths
/// (dashed network at world zoom, cased roads below) and the built roads on them (by class, one owner
/// per piece of ground — <see cref="RoadLens"/>), settlement markers and names (sized marks at
/// world zoom, aggregated morphology below), formations, institutions and Age banners. The legacy
/// GPU pass keeps only the substrate the lens draws over: the terrain bake and the vector rivers.
/// <c>SimUiGame</c> draws a layer only when this table names <see cref="LayerOwner.MapRenderer"/>;
/// <see cref="WorldLens.Paint"/> only ever draws the others.
/// </summary>
public static class MapLayerOwnership
{
    public static readonly MapLayer[] All =
    [
        MapLayer.Terrain, MapLayer.Rivers, MapLayer.Territory, MapLayer.Paths,
        MapLayer.SettlementMarkers, MapLayer.UnitTokens, MapLayer.InstitutionMarkers, MapLayer.AgeBanners,
    ];

    public static LayerOwner OwnerOf(MapLayer layer) => layer switch
    {
        MapLayer.Terrain => LayerOwner.MapRenderer,
        MapLayer.Rivers => LayerOwner.MapRenderer,
        _ => LayerOwner.WorldLens,
    };

    /// <summary>The layers the legacy GPU map pass draws, in paint order (what <c>SimUiGame.Draw</c> does).</summary>
    public static IReadOnlyList<MapLayer> MapRendererLayers()
    {
        var r = new List<MapLayer>();
        foreach (MapLayer l in All) if (OwnerOf(l) == LayerOwner.MapRenderer) r.Add(l);
        return r;
    }

    /// <summary>The map layer a lens information layer belongs to.</summary>
    public static MapLayer MapLayerOf(WorldLayer l) => l switch
    {
        WorldLayer.Territories => MapLayer.Territory,
        WorldLayer.MajorInfrastructure or WorldLayer.Roads => MapLayer.Paths,
        WorldLayer.MilitaryFormations => MapLayer.UnitTokens,
        WorldLayer.InstitutionalPresence or WorldLayer.InstitutionTypes => MapLayer.InstitutionMarkers,
        WorldLayer.AgeBanners => MapLayer.AgeBanners,
        _ => MapLayer.SettlementMarkers,   // major settlements, morphology, density, population, production
    };
}
