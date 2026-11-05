using Proto.M7.Fixed;

namespace Proto.M7.Spatial;

/// <summary>
/// Terrain classes of the TACTICAL prototype (spec §3). The campaign terrain has NO forest/jungle/hills/mountain classes
/// (recon-critic A4, recon-military §1.4: mountains are slope only there), so these classes exist only inside this prototype;
/// deriving them from the campaign raster is an open dependency (no thresholds are invented here).
/// Values are stable byte codes (used as table indices and in hashes) — never reorder.
/// </summary>
public enum TerrainClass : byte
{
    Road = 0,
    Open = 1,
    Forest = 2,
    Jungle = 3,
    Hills = 4,
    Mountain = 5,
    /// <summary>Impassable for land movement. Does not block LOS.</summary>
    Water = 6,
    /// <summary>Impassable; blocks LOS and direct fire (treated as infinitely tall).</summary>
    Wall = 7,
}

/// <summary>
/// Movement cost per terrain class: an INPUT table (integer, cost per mm-equivalent x1000, i.e. 1000 = open-ground baseline).
/// A cost of <see cref="Impassable"/> marks a class as impassable; Water and Wall MUST be impassable.
/// Validation (Director's binding ordering, brief §2 Logistics): Road &lt; Open &lt; Jungle &lt; Mountain, strictly.
/// Nothing else about the ordering is binding (Forest/Hills position is NOT stated), so nothing else is validated
/// beyond 1 &lt;= cost &lt;= MaxCost for every passable class (MaxCost bounds every integer intermediate).
/// </summary>
public sealed class TerrainCostTable
{
    public const int ClassCount = 8;
    public const long Impassable = 0;
    /// <summary>Upper bound per class (bounds A* step costs: (2*MaxCost)*CellSize*1414 fits long for CellSize &lt;= MaxCellSizeMm).</summary>
    public const long MaxCost = 1_000_000;

    private readonly long[] _cost;

    private TerrainCostTable(long[] cost) { _cost = cost; }

    /// <summary>Cost x1000 of a class; 0 = impassable.</summary>
    public long Cost(TerrainClass c) => _cost[(int)c];
    public bool IsPassable(TerrainClass c) => _cost[(int)c] != Impassable;

    /// <summary>Smallest cost over passable classes (heuristic scale; admissibility does not depend on which classes a grid uses).</summary>
    public long MinPassableCost { get; private init; }

    /// <summary>Builds a validated table from per-class costs indexed by TerrainClass byte value. Throws on any violation.</summary>
    public static TerrainCostTable Create(ReadOnlySpan<long> costByClass)
    {
        string? err = Validate(costByClass);
        if (err is not null) throw new ArgumentException(err, nameof(costByClass));
        long[] c = costByClass.ToArray();
        long min = long.MaxValue;
        for (int i = 0; i < ClassCount; i++) if (c[i] != Impassable) min = Math.Min(min, c[i]);
        return new TerrainCostTable(c) { MinPassableCost = min };
    }

    /// <summary>Returns null when valid, else a description of the first violation found.</summary>
    public static string? Validate(ReadOnlySpan<long> c)
    {
        if (c.Length != ClassCount) return $"expected {ClassCount} classes, got {c.Length}";
        for (int i = 0; i < ClassCount; i++)
        {
            if (c[i] < 0 || c[i] > MaxCost) return $"class {(TerrainClass)i}: cost {c[i]} outside [0, {MaxCost}]";
        }
        if (c[(int)TerrainClass.Water] != Impassable) return "Water must be impassable for land movement";
        if (c[(int)TerrainClass.Wall] != Impassable) return "Wall must be impassable";
        foreach (var k in new[] { TerrainClass.Road, TerrainClass.Open, TerrainClass.Forest, TerrainClass.Jungle, TerrainClass.Hills, TerrainClass.Mountain })
            if (c[(int)k] == Impassable) return $"class {k} must be passable (cost >= 1)";
        // Director's binding ordering (brief §2 Logistics): roads cheaper than open; jungle dearer than open; mountains dearer still.
        if (!(c[(int)TerrainClass.Road] < c[(int)TerrainClass.Open])) return "ordering violated: Road must be cheaper than Open";
        if (!(c[(int)TerrainClass.Open] < c[(int)TerrainClass.Jungle])) return "ordering violated: Jungle must be dearer than Open";
        if (!(c[(int)TerrainClass.Jungle] < c[(int)TerrainClass.Mountain])) return "ordering violated: Mountain must be dearer than Jungle";
        return null;
    }

    /// <summary>
    /// PLACEHOLDER test table (spec §3), NOT a shipped default:
    /// Road 700 = campaign `roads.classes` road_built speedFactor 0.7 x1000 (Sim.Data sim.json, recon-military §1.4) — CITED;
    /// Open 1000 = baseline by definition; Forest 1500, Hills 1800, Jungle 2500, Mountain 4000 — PLACEHOLDER (spec §3 list;
    /// the campaign has no such classes, recon-critic A4). Water/Wall impassable.
    /// </summary>
    public static TerrainCostTable PlaceholderTestTable() => Create(new long[]
    {
        700,   // Road     (cited: road_built 0.7)
        1000,  // Open     (baseline)
        1500,  // Forest   PLACEHOLDER
        2500,  // Jungle   PLACEHOLDER
        1800,  // Hills    PLACEHOLDER
        4000,  // Mountain PLACEHOLDER
        Impassable, // Water
        Impassable, // Wall
    });
}

/// <summary>
/// Concealment per terrain class: a Q32 DETECTION MULTIPLIER in [0, 1] (1 = no concealment). INPUT table.
/// Validation: every entry a probability; Road and Open must be exactly 1 (spec §3: Open/Road do not conceal);
/// Forest and Jungle must be &lt; 1 (spec §3: they conceal).
/// </summary>
public sealed class ConcealmentTable
{
    private readonly Q32[] _m;
    private ConcealmentTable(Q32[] m) { _m = m; }

    public Q32 Multiplier(TerrainClass c) => _m[(int)c];

    public static ConcealmentTable Create(ReadOnlySpan<Q32> byClass)
    {
        string? err = Validate(byClass);
        if (err is not null) throw new ArgumentException(err, nameof(byClass));
        return new ConcealmentTable(byClass.ToArray());
    }

    public static string? Validate(ReadOnlySpan<Q32> m)
    {
        if (m.Length != TerrainCostTable.ClassCount) return $"expected {TerrainCostTable.ClassCount} classes, got {m.Length}";
        for (int i = 0; i < m.Length; i++) if (!m[i].IsProbability) return $"class {(TerrainClass)i}: multiplier not in [0,1]";
        if (m[(int)TerrainClass.Road] != Q32.One || m[(int)TerrainClass.Open] != Q32.One) return "Road and Open must not conceal (multiplier 1)";
        if (m[(int)TerrainClass.Forest] >= Q32.One || m[(int)TerrainClass.Jungle] >= Q32.One) return "Forest and Jungle must conceal (multiplier < 1)";
        return null;
    }

    /// <summary>
    /// PLACEHOLDER test table (spec §3: "placeholder input table"): Forest 1/2, Jungle 1/4 — PLACEHOLDER values;
    /// Road/Open/Hills/Mountain/Water 1 (no concealment; Hills/Mountain/Water = 1 is also PLACEHOLDER: nothing decides them);
    /// Wall 0 (irrelevant: Wall blocks LOS outright).
    /// </summary>
    public static ConcealmentTable PlaceholderTestTable() => Create(new[]
    {
        Q32.One,                // Road
        Q32.One,                // Open
        Q32.FromRatio(1, 2),    // Forest   PLACEHOLDER
        Q32.FromRatio(1, 4),    // Jungle   PLACEHOLDER
        Q32.One,                // Hills    PLACEHOLDER
        Q32.One,                // Mountain PLACEHOLDER
        Q32.One,                // Water    PLACEHOLDER
        Q32.Zero,               // Wall (opaque; LOS is blocked before this matters)
    });
}
