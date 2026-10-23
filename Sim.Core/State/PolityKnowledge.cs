using Sim.Core.Pathing;

namespace Sim.Core.State;

/// <summary>
/// M5 map workstream P7 — THE INFORMATION-CLASS READ SEAM: what one polity KNOWS of the world, the one place every
/// player-facing reader (Sim.Ui's PlayerKnowledge is built from it) and every AI policy that ranks or reads the world
/// (<c>AiFoundingPolicy</c>'s site scan, <c>AiConstructionPolicy</c>'s settlement reads, <c>InfoQuery</c>'s formation
/// card) asks. Spine principle 7 (frozen): AI actors use the player's verbs AND the player's information class —
/// "the AI cannot use information it has not legitimately discovered" (Director). A read that does not come through
/// here is a read of something the polity may not know.
///
/// <para><b>THE FOG (M6 Track EX, D-062, ADR-044).</b> <see cref="Of"/> is THE production entry. Its inputs are the
/// stored known extent (<see cref="KnownExtentRow"/>, section 63 — monotone), the polity's sightings
/// (<see cref="SettlementSightingRow"/>, section 65) and the sight it derives each call from the world it is handed (the
/// nodes of the settlements it controls, its formations' sight discs — <see cref="DiscoveryQuery"/>; never stored), under
/// sim.json <c>fog</c>. Nothing else may ever feed it: no UI flag, no difficulty setting, no second table. With the fog
/// off (sim.json fog absent or disabled, or no config) every node is known and visible — the pre-M6 answer, byte for
/// byte, the attribution control.</para>
///
/// <para><b>THE QUESTIONS</b> (granularity: the traversal lattice's 16 km node — D-040 B5 names it; a cell is known iff
/// the stride block that contains it is):</para>
/// <list type="bullet">
/// <item><see cref="Known"/>(node): the node is in the polity's extent (permanent once gained, D-040 B2).</item>
/// <item><see cref="Visible"/>(node): the node is in sight now. Visible ⊆ Known.</item>
/// <item><see cref="KnowsSettlement"/>: a settlement the polity controls, one whose site is VISIBLE, or (fog on) one it
///   has SIGHTED (rule E5: presence as last seen — a settlement founded in remembered land stays unknown until seen again).
///   Its internals stay its controller's (<see cref="ReadsInternals"/>).</item>
/// <item><see cref="KnowsUnit"/>: a formation the polity owns, or one whose position (its station's site when stationed)
///   is visible (D-039 C1: a formation not observed is UNKNOWN; rule E6: no ghost memory before M7 — formations cannot move
///   before then, so a last-known marker would equal the live position).</item>
/// <item><see cref="KnowsPolity"/>: the polity itself, or one with a settlement the polity knows (contact).</item>
/// <item><see cref="ReadsInternals"/>: a settlement's BOOKS (its queue, stocks, people, deposits) are its controller's
///   information — the class every AI policy reads under. Not an extent question: it holds under all-known too.</item>
/// </list>
///
/// <para>Read-only and deterministic: no state is written, nothing is cached across worlds (an instance answers for
/// the world it was built from), no dictionary, no LINQ (law 5); a position is floored to its cell and nothing is computed in floating point.
/// <see cref="Restricted"/> is the TEST SEAM —
/// a knowledge stub with explicit node masks, so the metamorphic non-interference tests can hide part of the world;
/// production code never calls it (pinned by the P7 source guard).</para>
/// </summary>
public sealed class PolityKnowledge
{
    private readonly IReadOnlyWorldState _world;
    private readonly ulong[]? _known;    // null: every node (the fog off)
    private readonly ulong[]? _visible;  // null: every known node
    private readonly bool _sightings;    // the fog's settlement memory (rule E5) is read
    private readonly int _terrainSize, _latticeSize, _block;
    private readonly bool _wrapX;

    /// <summary>The polity whose knowledge this is.</summary>
    public PolityId Polity { get; }

    /// <summary>True while the seam restricts nothing (the fog off).</summary>
    public bool AllKnown => _known is null && _visible is null;

    /// <summary>Lattice nodes per side of the knowledge grid (0 for a world without terrain).</summary>
    public int LatticeSize => _latticeSize;

    /// <summary>WS3: the world's east-west topology (<see cref="Worldgen.TerrainSet.WrapX"/>; false without terrain) —
    /// read by the reach helpers that mirror the wrapped siting windows (SiteAssessment.KnowsSquare/Diamond/Disc).</summary>
    public bool WrapX => _wrapX;

    private PolityKnowledge(IReadOnlyWorldState world, PolityId polity, ulong[]? known, ulong[]? visible, bool sightings = false)
    {
        _world = world;
        Polity = polity;
        _known = known;
        _visible = visible;
        _sightings = sightings;
        if (world.Terrain is { } terrain)
        {
            _terrainSize = terrain.Size;
            (_latticeSize, _block) = Grid(terrain.Size);
            _wrapX = terrain.WrapX;
        }
    }

    /// <summary>
    /// THE PRODUCTION ENTRY: what <paramref name="polity"/> knows of <paramref name="world"/> under <paramref name="cfg"/>'s
    /// fog (see the type's remarks): all-known with the fog off or no config; else the stored extent ∪ the seed discs of
    /// its controlled settlements ∪ its formations' sight (known), the settlement nodes and formation discs (visible), and
    /// its sightings.
    /// </summary>
    public static PolityKnowledge Of(IReadOnlyWorldState world, PolityId polity, Systems.SimConfig? cfg)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (cfg?.Fog is not { Enabled: true } fog || world.Terrain is null)
            return new PolityKnowledge(world, polity, null, null);
        return new PolityKnowledge(world, polity,
            DiscoveryQuery.KnownBits(world, fog, polity), DiscoveryQuery.VisibleBits(world, fog, polity), sightings: true);
    }

    /// <summary>
    /// TEST SEAM — a knowledge stub over explicit node masks (<paramref name="known"/>, <paramref name="visible"/>; one
    /// entry per lattice node of <see cref="Grid"/>). The metamorphic leak tests and the headless gate's hidden-rival
    /// rig use it to hide part of a world; no production reader calls it (the P7 source guard pins the callers). A
    /// visible node must be known (Visible ⊆ Known).
    /// </summary>
    public static PolityKnowledge Restricted(IReadOnlyWorldState world, PolityId polity, ReadOnlySpan<bool> known, ReadOnlySpan<bool> visible)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Terrain is not { } terrain) throw new ArgumentException("a knowledge stub needs a world with terrain", nameof(world));
        int nodes = Grid(terrain.Size).LatticeSize * Grid(terrain.Size).LatticeSize;
        if (known.Length != nodes || visible.Length != nodes)
            throw new ArgumentException("a knowledge mask has one entry per lattice node (" + nodes.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
        var k = new ulong[DiscoveryQuery.WordCount(Grid(terrain.Size).LatticeSize)];
        var v = new ulong[k.Length];
        for (int n = 0; n < nodes; n++)
        {
            if (visible[n] && !known[n]) throw new ArgumentException("a visible node must be known (node " + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
            if (known[n]) DiscoveryQuery.Set(k, n);
            if (visible[n]) DiscoveryQuery.Set(v, n);
        }
        return new PolityKnowledge(world, polity, k, v);
    }

    /// <summary>The knowledge grid of a terrain of <paramref name="terrainSize"/> px — the traversal lattice's geometry
    /// (<see cref="TraversalLattice.DefaultStride"/>): nodes per side and the block width in terrain px.</summary>
    public static (int LatticeSize, int BlockPx) Grid(int terrainSize) =>
        MapTopology.LatticeGrid(terrainSize, TraversalLattice.DefaultStride);

    // ------------------------------------------------------------------ nodes and cells

    /// <summary>The lattice node whose stride block contains terrain cell <paramref name="cell"/> (cells past the last
    /// whole block belong to the last node), or −1 for a world without terrain or a cell off the map.</summary>
    public int NodeOfCell(int cell)
    {
        if (_latticeSize == 0 || cell < 0 || cell >= _terrainSize * _terrainSize) return -1;
        return MapTopology.NodeOfCell(cell, _terrainSize, _latticeSize, _block);
    }

    /// <summary>The lattice node under world position (<paramref name="x"/>, <paramref name="y"/>) in terrain px, or −1.
    /// WS3: on a wrapped world an x outside [0, size) is the same place one circumference on — it is floored to its
    /// column and the column normalised (integer modulo), never refused; y keeps its hard edges.</summary>
    public int NodeAt(double x, double y)
    {
        if (_latticeSize == 0 || !(y >= 0.0) || y >= _terrainSize) return -1;
        if (_wrapX)
        {
            if (!double.IsFinite(x)) return -1;
            double column = Math.Floor(x);
            if (column < long.MinValue / 2.0 || column > long.MaxValue / 2.0) return -1;
            return NodeOfCell((int)y * _terrainSize + MapTopology.WrapX((long)column, _terrainSize));
        }
        if (!(x >= 0.0) || x >= _terrainSize) return -1;
        return NodeOfCell((int)y * _terrainSize + (int)x);
    }

    /// <summary>Whether lattice node <paramref name="node"/> is in the polity's known extent.</summary>
    public bool Known(int node) =>
        _known is null || ((uint)node < (uint)(_latticeSize * _latticeSize) && DiscoveryQuery.Has(_known, node));

    /// <summary>Whether lattice node <paramref name="node"/> is in the polity's sight now (Visible ⊆ Known).</summary>
    public bool Visible(int node) => _visible is null
        ? Known(node)
        : (uint)node < (uint)(_latticeSize * _latticeSize) && DiscoveryQuery.Has(_visible, node);

    /// <summary>Whether lattice node <paramref name="node"/> is REMEMBERED: known but not in sight now (the map's darker
    /// overlay). Always false with the fog off.</summary>
    public bool Remembered(int node) => !AllKnown && Known(node) && !Visible(node);

    /// <summary>Whether terrain cell <paramref name="cell"/> lies in a known node.</summary>
    public bool KnowsCell(int cell) => _known is null || Known(NodeOfCell(cell));

    /// <summary>Whether terrain cell <paramref name="cell"/> lies in a visible node.</summary>
    public bool SeesCell(int cell) => AllKnown || Visible(NodeOfCell(cell));

    // ------------------------------------------------------------------ objects

    /// <summary>Whether the polity knows settlement <paramref name="settlement"/>: it controls it, its site is visible, or
    /// (fog on) it has sighted it. False for an id no settlement has.</summary>
    public bool KnowsSettlement(SettlementId settlement)
    {
        for (int i = 0; i < _world.Settlements.Count; i++)
        {
            SettlementRow row = _world.Settlements[i];
            if (row.Id.Value != settlement.Value) continue;
            return AllKnown || EmpireQuery.ControlsSettlement(_world, Polity, settlement) || SeesCell(row.SiteCell)
                || (_sightings && DiscoveryQuery.HasSighting(_world, Polity, settlement));
        }
        return false;
    }

    /// <summary>Whether the polity knows formation <paramref name="unit"/>: it owns it, or where it stands is visible —
    /// its station's site when stationed in a settlement, else its own position.</summary>
    public bool KnowsUnit(in MilitaryUnitRow unit)
    {
        if (AllKnown || unit.Owner.Value == Polity.Value) return true;
        if (unit.Location.Value >= 0)
            for (int i = 0; i < _world.Settlements.Count; i++)
                if (_world.Settlements[i].Id.Value == unit.Location.Value) return SeesCell(_world.Settlements[i].SiteCell);
        return Visible(NodeAt(unit.X, unit.Y));
    }

    /// <summary>Whether the polity knows polity <paramref name="other"/>: itself, or a polity with a settlement it knows
    /// (contact).</summary>
    public bool KnowsPolity(PolityId other)
    {
        if (AllKnown || other.Value == Polity.Value) return true;
        for (int i = 0; i < _world.Controls.Count; i++)
        {
            ControlRow row = _world.Controls[i];
            if (row.Polity.Value == other.Value && KnowsSettlement(row.Place)) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the polity may read settlement <paramref name="settlement"/>'s BOOKS — its construction queue, stocks,
    /// people and deposits: only a settlement it controls. A world with no control relation at all (a hand-built toy)
    /// has nothing to check against and treats every settlement as the polity's — the guard ConstructionQuery's
    /// availability and LabourActivities.CanAllocate apply, so the information class and the legality arm never
    /// disagree.
    /// </summary>
    public bool ReadsInternals(SettlementId settlement) =>
        _world.Controls.Count == 0 || EmpireQuery.ControlsSettlement(_world, Polity, settlement);
}
