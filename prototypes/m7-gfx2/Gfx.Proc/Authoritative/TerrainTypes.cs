namespace Gfx.Proc.Authoritative;

/// <summary>
/// Battle terrain classes (GAMEPLAY-RELEVANT, authoritative). Byte codes 0..7 deliberately match the battle
/// prototype's <c>Proto.M7.Spatial.TerrainClass</c> (read, not referenced); 8+ are additions this generator emits.
/// Stable codes (hashed): never reorder.
/// </summary>
public enum TerrainClass : byte
{
    Road = 0, Open = 1, Forest = 2, Jungle = 3, Hills = 4, Mountain = 5, Water = 6,
    /// <summary>Reserved for structures (prototype parity); this generator never emits it.</summary>
    Wall = 7,
    Beach = 8, Desert = 9,
}

/// <summary>Climate family of the battlefield (authoritative: it decides Forest vs Jungle vs Desert).</summary>
public enum Biome : byte { Temperate = 0, Arid = 1, Tropical = 2, Cold = 3 }

/// <summary>
/// Integer inputs of the battlefield downscale (the campaign→battle BOUNDARY). In the real game these come from the
/// campaign cell (recon-sim-seams §1): world seed, cell index, elevation, movement cost, temperature, moisture,
/// coast side, river flag. Here they are labelled fixtures (see reports/terrain.md for the conversion rule).
/// </summary>
/// <param name="CoastEdge">-1 none; 0 north (grid y = 0), 1 east, 2 south, 3 west.</param>
/// <param name="MoveCostPermille">Campaign movement cost, 1000 = open baseline; drives relief amplitude.</param>
/// <param name="TemperatureDeciC">Mean temperature in tenths of a degree C.</param>
/// <param name="MoisturePermille">Moisture 0..1000.</param>
public readonly record struct BattleTerrainInputs(
    ulong WorldSeed, int CampaignCell, int ElevationM, int MoveCostPermille,
    int TemperatureDeciC, int MoisturePermille, int CoastEdge, bool River,
    int Width = 256, int Height = 256, int CellMm = 4000)
{
    /// <summary>Integer climate rule (prototype thresholds — a Director/spec decision, see report).</summary>
    public Biome Biome =>
        TemperatureDeciC <= 20 ? Biome.Cold :
        MoisturePermille < 300 ? Biome.Arid :
        TemperatureDeciC >= 220 && MoisturePermille >= 600 ? Biome.Tropical : Biome.Temperate;

    public void Validate()
    {
        if (Width < 16 || Height < 16 || Width > 4096 || Height > 4096) throw new ArgumentOutOfRangeException(nameof(Width), "grid 16..4096 per side");
        if (CellMm < 500 || CellMm > 50_000) throw new ArgumentOutOfRangeException(nameof(CellMm));
        if (CoastEdge < -1 || CoastEdge > 3) throw new ArgumentOutOfRangeException(nameof(CoastEdge));
        if (MoisturePermille < 0 || MoisturePermille > 1000) throw new ArgumentOutOfRangeException(nameof(MoisturePermille));
        if (MoveCostPermille < 1 || MoveCostPermille > 10_000) throw new ArgumentOutOfRangeException(nameof(MoveCostPermille));
    }
}
