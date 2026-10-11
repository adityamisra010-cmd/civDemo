using System.Globalization;
using System.Text;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.World;

/// <summary>What an overlay layer shows: one deposit CHANNEL (the deposit function's own channel switch), or the
/// simulation's one composite siting score, or one of that score's two factors.</summary>
public enum OverlayKind { Channel = 0, Suitability = 1, RegionFertility = 2, WaterAccess = 3 }

/// <summary>A deposit good a channel layer drives (goods.json: its id, name and seeded spread; whether herding/fishing
/// labour works it, else extraction — <see cref="LabourActivities.IsHerded"/>).</summary>
public readonly record struct OverlayGood(int Id, string Name, double Spread, bool Herded);

/// <summary>
/// One toggleable overlay layer. A CHANNEL layer is one value of goods.json's <c>depositChannel</c> and lists EVERY good
/// that channel drives (<see cref="Goods"/>): the simulation draws no difference between the goods of one channel beyond
/// each one's seeded spread, which is rolled per SETTLEMENT, so there is no layer per good. The other three are the
/// simulation's siting score and its two factors.
/// </summary>
public sealed record OverlayLayer(int Index, string Key, OverlayKind Kind, string? Channel, string Name, string Measure,
    IReadOnlyList<OverlayGood> Goods)
{
    /// <summary>The control that toggles this layer (UiControls; a gate row presses it).</summary>
    public string Control => ResourceOverlay.ControlPrefix + Key;
}

/// <summary>One row of the overlay's key: its text, and the layer whose ramp is drawn under it (-1: none) — a layer's
/// name row carries its ramp.</summary>
public readonly record struct OverlayKeyRow(string Text, int Ramp = -1);

/// <summary>A REALIZED ore roll at a settlement the player holds: its DepositRow abundance for a good with a seeded spread,
/// and the channel's expected value at its site (NaN when not judged).</summary>
public readonly record struct OreRoll(int Settlement, int Good, string GoodName, string Channel, double Abundance, double Expected);

/// <summary>
/// The per-turn half of the overlay's key — DISCOVERY is the bake's (<see cref="ResourceOverlay.KnownNodes"/>); the rest
/// here, each from the predicate the place card (P8) uses:
/// <list type="bullet">
/// <item>ACCESSIBLE: <see cref="Reachable"/> known land places with a land route from a settlement the player controls —
///   P8's "reachable" (<see cref="Pathfinder.FindPath"/> found), taken for every place at once as the union of
///   <see cref="Pathfinder.Isochrone"/> reached sets at an unbounded budget (the same expansion: pinned against the card's
///   own answer by the tests); null unless the player sees the whole world (routes run over the whole lattice and every
///   road). <see cref="InCatchments"/>: known land places in the catchments of the player's settlements (worked now).</item>
/// <item>EXTRACTABLE: <see cref="Workable"/> — P8's "workable" (<see cref="LabourActivities.CanAllocate"/> for the default
///   source, <see cref="SiteAssessment.DefaultSource"/>): a colony from it takes its ruler, so its herding and extraction
///   labour — the sectors that work deposits, with no research gate in production — would be the player's to set.</item>
/// <item>The realized ore rolls at the player's own known settlements (<see cref="Rolls"/>; their deposits are the
///   player's books — <see cref="PolityKnowledge.ReadsInternals"/>).</item>
/// </list>
/// PRESENCE is the bake's (<see cref="ResourceOverlay.PresentNodes"/>): the channel value above 0 — P8's "present".
/// </summary>
public sealed record OverlayStatus(int? Reachable, int InCatchments, int Source, bool? Workable, IReadOnlyList<OreRoll> Rolls);

/// <summary>
/// M5 map workstream P9 — THE RESOURCE AND SUITABILITY OVERLAY: per traversal-lattice node (the knowledge grid's 16 km
/// node; D-040 B5), the value a settlement founded at the node's CENTRE CELL (<see cref="SampleCell"/>) would be judged
/// by, through the simulation's own public functions — never restated, never read from a rendered colour:
/// <list type="bullet">
/// <item>a CHANNEL layer per goods.json <c>depositChannel</c>: <see cref="WorldFounding.DepositChannelValue"/> of
///   <see cref="WorldFounding.HinterlandMeans"/> over the founding's own disc (<see cref="WorldFounding.HinterlandRadiusPx"/>)
///   — exactly the value <see cref="WorldFounding.AddDepositsForSite"/> writes for a good with no spread, the EXPECTED
///   value (the spread's centre) for one with a spread. It is an abundance — a RATE MULTIPLIER on extraction — never a
///   quantity: nothing depletes, and the actual value varies ±spread, rolled per settlement, not per place;</item>
/// <item>SUITABILITY: the unjittered siting score <see cref="SettlementSiting.CandidateScores"/> (the session's prepared
///   <see cref="SiteAssessment.Prepared.Frontier"/> raster), and its two factors — REGION FERTILITY
///   (<see cref="SettlementSiting.BoxSum"/>) and WATER ACCESS (<see cref="SettlementSiting.WaterAccess"/> of
///   <see cref="SettlementSiting.WaterDistanceBfs"/>). No weight is invented and nothing else is folded in (D-043 C2).</item>
/// </list>
///
/// <para><b>THE PLAYER'S INFORMATION CLASS</b> (P7 seam, ADR-037 §7 — the place card's rule): a node the player does not
/// know is not evaluated at all — nothing of it is drawn, scored, counted into a range or shown. A known node's value is
/// WITHHELD (not drawn; counted as "not judged") when its own reach touches undiscovered ground — the hinterland disc,
/// the fertility box, the cells the water BFS reads to settle min(distance, cutoff) (<see cref="SiteAssessment.KnowsDisc"/>,
/// <see cref="SiteAssessment.KnowsSquare"/>, <see cref="SiteAssessment.KnowsDiamond"/> — the card's own reach rules). While
/// the player sees the whole world (<see cref="SiteAssessment.SeesAll"/>: production until S2a stores an extent) every
/// value is the simulation's own, bit for bit, and the score's founding floor is shown. Otherwise the region-fertility
/// sum is taken over its own window alone (<see cref="WindowSum"/>): the simulation's integral image reads every cell
/// above and left of the window, so the low bits of ITS sum depend on ground the player has not discovered; the window
/// sum is the same quantity summed from known cells only, and the floor (taken over every cell) is not shown. The colour
/// ramp of a layer runs from 0 to the largest value DRAWN, so it too is a function of known ground alone.</para>
///
/// <para>Pure and deterministic: arrays in node order, no dictionary, no LINQ, invariant formatting; a bake is a function of
/// (terrain, configs, knowledge masks) and its <see cref="CanonicalBytes"/> are byte-identical for equal inputs. UI-side
/// only, like the terrain bake: nothing in the simulation reads it, and baking changes no hash.</para>
///
/// <para><b>DATA GAPS (stated, not filled).</b> There is no per-cell resource quantity: deposits are written per settlement
/// at founding. The nine deposit goods collapse onto three channels. D-051 §2 (availability classes, regional richness;
/// ratified, not built) will replace the deposit function; this overlay keeps calling that one function. Presence is
/// universally true until §2 introduces scarcity.</para>
/// </summary>
public sealed class ResourceOverlay
{
    /// <summary>The prefix of every overlay layer's control name.</summary>
    public const string ControlPrefix = "overlay:";

    /// <summary>A node's level when its value is not drawn (unknown, water, withheld or not evaluated).</summary>
    public const byte NotDrawn = 255;

    /// <summary>The ramp's levels: a drawn value v is level floor(v / max × 16), the top one closed.</summary>
    public const int LevelCount = 16;

    private readonly SiteAssessment.Prepared _site;
    private readonly bool[] _known;
    private readonly double[][] _values;
    private readonly byte[][] _levels;
    private readonly double[] _max;
    private readonly int[] _drawn, _withheld, _positive;

    /// <summary>The terrain this was baked from.</summary>
    public TerrainSet Terrain => _site.Terrain;

    /// <summary>Terrain px per side.</summary>
    public int Size { get; }

    /// <summary>Lattice nodes per side (<see cref="PolityKnowledge.Grid"/>).</summary>
    public int LatticeSize { get; }

    /// <summary>A node's block width in terrain px.</summary>
    public int Block { get; }

    public int NodeCount => LatticeSize * LatticeSize;

    /// <summary>The layers, in paint and key order: the channels in goods.json order, then the score and its factors.</summary>
    public IReadOnlyList<OverlayLayer> Layers { get; }

    /// <summary>The knowledge restricted nothing (production until S2a).</summary>
    public bool AllKnown { get; }

    /// <summary>The player sees the whole world (<see cref="SiteAssessment.SeesAll"/>): every value is the simulation's own.</summary>
    public bool SeesAll { get; }

    /// <summary>Lattice nodes the player knows, and of them the ones whose centre cell is land.</summary>
    public int KnownNodes { get; }
    public int KnownLandNodes { get; }

    /// <summary>Known land nodes whose every channel value is judged, and of them the ones where every channel is above 0
    /// (every deposit good is expected present).</summary>
    public int JudgedForPresence { get; }
    public int PresentNodes { get; }

    /// <summary>Why the channel layers are not evaluated (no goods content; a worldgen config for another map size, so
    /// the founding's deposit function cannot be evaluated here — the place card's rule), or null.</summary>
    public string? ChannelsUnavailable { get; }

    /// <summary>The score's founding floor (SettlementSiting.ScoreFloor's percentile of the positive scores) — NaN unless
    /// <see cref="SeesAll"/> (it is taken over every cell).</summary>
    public double Floor { get; }

    /// <summary>The founding's hinterland radius (km), the fertility box radius (px) and the water cutoff (px).</summary>
    public double HinterlandKm { get; }
    public int FertilityRadiusPx { get; }
    public int WaterCutoffPx { get; }

    private ResourceOverlay(SiteAssessment.Prepared site, IReadOnlyList<OverlayLayer> layers, int lsize, int block, bool allKnown,
        bool seesAll, bool[] known, double[][] values, byte[][] levels, double[] max, int[] drawn, int[] withheld, int[] positive,
        int knownNodes, int knownLand, int judged, int present, string? channelsUnavailable, double floor, double hinterlandKm)
    {
        _site = site;
        Size = site.Terrain.Size;
        Layers = layers;
        LatticeSize = lsize;
        Block = block;
        AllKnown = allKnown;
        SeesAll = seesAll;
        _known = known;
        _values = values;
        _levels = levels;
        _max = max;
        _drawn = drawn;
        _withheld = withheld;
        _positive = positive;
        KnownNodes = knownNodes;
        KnownLandNodes = knownLand;
        JudgedForPresence = judged;
        PresentNodes = present;
        ChannelsUnavailable = channelsUnavailable;
        Floor = floor;
        HinterlandKm = hinterlandKm;
        FertilityRadiusPx = site.Worldgen.Siting.FertilityRadiusPx;
        WaterCutoffPx = site.Worldgen.Siting.WaterAccessCutoffPx;
    }

    /// <summary>The value of layer <paramref name="layer"/> at node <paramref name="node"/>, or NaN when it is not drawn.</summary>
    public double Value(int layer, int node) => _values[layer][node];

    /// <summary>The ramp level of layer <paramref name="layer"/> at node <paramref name="node"/>, or <see cref="NotDrawn"/>.</summary>
    public byte Level(int layer, int node) => _levels[layer][node];

    /// <summary>The largest value drawn on a layer (the deep end of its ramp; 0 when nothing is drawn).</summary>
    public double Max(int layer) => _max[layer];

    /// <summary>How many nodes a layer draws; how many known land nodes it withholds (reach undiscovered); how many drawn
    /// values are above 0.</summary>
    public int Drawn(int layer) => _drawn[layer];
    public int Withheld(int layer) => _withheld[layer];
    public int Positive(int layer) => _positive[layer];

    /// <summary>Whether the player knows lattice node <paramref name="node"/> (the mask this was baked under).</summary>
    public bool Known(int node) => _known[node];

    // ------------------------------------------------------------------ geometry

    /// <summary>The cell a node's values are taken at: its block's centre cell (block x·b + b/2, y·b + b/2), on the map.</summary>
    public static int SampleCell(int node, int size, int latticeSize, int block)
    {
        int x = Math.Min(size - 1, node % latticeSize * block + block / 2);
        int y = Math.Min(size - 1, node / latticeSize * block + block / 2);
        return y * size + x;
    }

    /// <summary>This bake's <see cref="SampleCell(int, int, int, int)"/>.</summary>
    public int SampleCell(int node) => SampleCell(node, Size, LatticeSize, Block);

    // ------------------------------------------------------------------ the layers

    /// <summary>The overlay's layers for a session's content: one per distinct goods.json <c>depositChannel</c> in registry
    /// order (each listing every good it drives), then the siting score, region fertility and water access. A pure
    /// function of the configs and the map's km per px.</summary>
    public static IReadOnlyList<OverlayLayer> LayersOf(SimConfig cfg, WorldgenConfig worldgen, double kmPerPx)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(worldgen);
        var layers = new List<OverlayLayer>();
        var channels = new List<string>();
        if (cfg.Goods is { } goods)
            foreach (GoodEntry g in goods.Goods)
                if (g.DepositChannel is { } ch && !channels.Contains(ch)) channels.Add(ch);
        double hinterlandKm = cfg.Catchment.HinterlandRadiusKm;
        foreach (string ch in channels)
        {
            var list = new List<OverlayGood>();
            foreach (GoodEntry g in cfg.Goods!.Goods)
                if (g.DepositChannel == ch) list.Add(new OverlayGood(g.Id, g.Name, g.DepositSpread, LabourActivities.IsHerded(cfg.Goods, g.Id)));
            layers.Add(new OverlayLayer(layers.Count, ch, OverlayKind.Channel, ch, ChannelName(ch) + " channel",
                ChannelMeasure(ch, hinterlandKm), list));
        }
        int r = worldgen.Siting.FertilityRadiusPx, side = 2 * r + 1, cutoff = worldgen.Siting.WaterAccessCutoffPx;
        layers.Add(new OverlayLayer(layers.Count, "suitability", OverlayKind.Suitability, null, "Settlement suitability",
            "The siting score the simulation founds settlements by: region fertility x water access, unjittered", []));
        layers.Add(new OverlayLayer(layers.Count, "fertility", OverlayKind.RegionFertility, null, "Region fertility",
            "A score factor: fertility summed over the " + Inv(side) + " x " + Inv(side) + " cells around the place ("
            + Num(side * kmPerPx) + " km square)", []));
        layers.Add(new OverlayLayer(layers.Count, "access", OverlayKind.WaterAccess, null, "Water access",
            "A score factor: 1 at a shore or river bank, falling to 0 at " + Inv(cutoff) + " cells (" + Num(cutoff * kmPerPx) + " km)", []));
        return layers;
    }

    /// <summary>A channel's display name: the deposit function reads "moisture" and "elevation" as such and every other
    /// channel as the squared moisture mean (water proximity).</summary>
    public static string ChannelName(string channel) => channel switch
    {
        "moisture" => "Moisture",
        "elevation" => "Elevation",
        "water" => "Water-proximity",
        _ => channel.Length == 0 ? channel : char.ToUpperInvariant(channel[0]) + channel[1..],
    };

    private static string ChannelMeasure(string channel, double hinterlandKm) => channel switch
    {
        "moisture" => "Mean moisture of the land within " + Num(hinterlandKm) + " km",
        "elevation" => "Mean height above sea of the land within " + Num(hinterlandKm) + " km",
        _ => "Mean moisture of the land within " + Num(hinterlandKm) + " km, squared",
    };

    /// <summary>The deposit channel <paramref name="channel"/>'s value for a settlement founded at <paramref name="cell"/>:
    /// THE founding's function (<see cref="WorldFounding.HinterlandMeans"/> over <see cref="WorldFounding.HinterlandRadiusPx"/>,
    /// then <see cref="WorldFounding.DepositChannelValue"/>) — exactly the abundance <see cref="WorldFounding.AddDepositsForSite"/>
    /// writes for a good of this channel with no spread. <paramref name="worldgen"/> must describe this map's size.</summary>
    public static double ChannelAt(TerrainSet terrain, WorldgenConfig worldgen, SimConfig cfg, string channel, int cell)
    {
        (double m, double e) = WorldFounding.HinterlandMeans(terrain, worldgen, cell, WorldFounding.HinterlandRadiusPx(worldgen, cfg));
        return WorldFounding.DepositChannelValue(channel, m, e);
    }

    /// <summary>The sum of <paramref name="field"/> over the window ±<paramref name="r"/> about (<paramref name="x"/>,
    /// <paramref name="y"/>), clamped to the map — BoxSum's window — summed row by row from that window's cells alone (the
    /// restricted view's region fertility; see the type's remarks).</summary>
    public static double WindowSum(ReadOnlySpan<double> field, int size, int x, int y, int r)
    {
        r = Math.Max(0, r);
        int y0 = Math.Max(0, y - r), y1 = Math.Min(size - 1, y + r), x0 = Math.Max(0, x - r), x1 = Math.Min(size - 1, x + r);
        double sum = 0.0;
        for (int yy = y0; yy <= y1; yy++)
            for (int xx = x0; xx <= x1; xx++) sum += field[yy * size + xx];
        return sum;
    }

    // ------------------------------------------------------------------ the bake

    /// <summary>Whether this bake answers for <paramref name="site"/> under <paramref name="knowledge"/>: the same prepared
    /// terrain half, and the same known-node mask and whole-world sight.</summary>
    public bool IsFor(SiteAssessment.Prepared site, PolityKnowledge knowledge)
    {
        if (!ReferenceEquals(_site, site)) return false;
        if (knowledge.AllKnown) return AllKnown;
        if (AllKnown || SiteAssessment.SeesAll(knowledge) != SeesAll) return false;
        for (int n = 0; n < _known.Length; n++) if (knowledge.Known(n) != _known[n]) return false;
        return true;
    }

    /// <summary>
    /// Bakes every layer for <paramref name="knowledge"/>'s player over <paramref name="site"/>'s terrain (the session's
    /// prepared site half, built with the worldgen config the founding path holds). Reads only; writes nothing.
    /// </summary>
    public static ResourceOverlay Bake(SimConfig cfg, SiteAssessment.Prepared site, PolityKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(knowledge);
        TerrainSet t = site.Terrain;
        int size = t.Size;
        (int lsize, int block) = PolityKnowledge.Grid(size);
        if (knowledge.LatticeSize != lsize) throw new ArgumentException("the knowledge is for another map", nameof(knowledge));
        IReadOnlyList<OverlayLayer> layers = LayersOf(cfg, site.Worldgen, t.KmPerPx);
        int nodes = lsize * lsize, count = layers.Count;
        bool all = SiteAssessment.SeesAll(knowledge);
        var known = new bool[nodes];
        var values = new double[count][];
        var levels = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            values[i] = new double[nodes];
            Array.Fill(values[i], double.NaN);
            levels[i] = new byte[nodes];
            Array.Fill(levels[i], NotDrawn);
        }
        var withheld = new int[count];

        string? unavailable = cfg.Goods is null ? "no goods content is loaded"
            : site.Worldgen.SizePx != size
                ? "the founding path's worldgen config describes a " + Inv(site.Worldgen.SizePx) + "-px map and this map is " + Inv(size)
                    + " px, so its deposit function cannot be evaluated here"
                : null;
        double radiusPx = WorldFounding.HinterlandRadiusPx(site.Worldgen, cfg);
        SitingConfig siting = site.Worldgen.Siting;
        int r = siting.FertilityRadiusPx, cutoff = siting.WaterAccessCutoffPx;
        int knownNodes = 0, knownLand = 0, judged = 0, present = 0;
        bool anyChannel = false;
        foreach (OverlayLayer l in layers) anyChannel |= l.Kind == OverlayKind.Channel;

        for (int node = 0; node < nodes; node++)
        {
            if (!knowledge.Known(node)) continue;   // THE REFUSAL: nothing of an unknown node is read
            known[node] = true;
            knownNodes++;
            int cell = SampleCell(node, size, lsize, block);
            if (t.Water[cell] >= 0.5) continue;      // no settlement stands on water
            knownLand++;
            int x = cell % size, y = cell / size;

            // The deposit channels: the founding's hinterland disc.
            if (anyChannel && unavailable is null)
            {
                if (all || SiteAssessment.KnowsDisc(knowledge, size, x, y, radiusPx))
                {
                    (double m, double e) = WorldFounding.HinterlandMeans(t, site.Worldgen, cell, radiusPx);
                    bool allPositive = true;
                    foreach (OverlayLayer l in layers)
                    {
                        if (l.Kind != OverlayKind.Channel) continue;
                        double v = WorldFounding.DepositChannelValue(l.Channel!, m, e);
                        values[l.Index][node] = v;
                        allPositive &= v > 0.0;
                    }
                    judged++;
                    if (allPositive) present++;
                }
                else
                    foreach (OverlayLayer l in layers) if (l.Kind == OverlayKind.Channel) withheld[l.Index]++;
            }

            // The score's two factors, each by its own reach; the score when both are judged.
            bool boxKnown = all || SiteAssessment.KnowsSquare(knowledge, size, x, y, r);
            int distance = site.WaterDistancePx[cell];
            bool diamondKnown = all || SiteAssessment.KnowsDiamond(knowledge, size, x, y, Math.Min(distance, cutoff));
            double region = !boxKnown ? double.NaN : all ? site.RegionFertility[cell] : WindowSum(t.Fertility, size, x, y, r);
            double access = diamondKnown ? SettlementSiting.WaterAccess(distance, cutoff) : double.NaN;
            double score = all ? site.Frontier.Scores[cell]
                : boxKnown && diamondKnown ? (access <= 0.0 ? 0.0 : region * access) : double.NaN;
            foreach (OverlayLayer l in layers)
            {
                double v = l.Kind switch
                {
                    OverlayKind.Suitability => score,
                    OverlayKind.RegionFertility => region,
                    OverlayKind.WaterAccess => access,
                    _ => 0.0,
                };
                if (l.Kind == OverlayKind.Channel) continue;
                if (double.IsNaN(v)) withheld[l.Index]++;
                else values[l.Index][node] = v;
            }
        }

        // The ramps: 0 to the largest value DRAWN (a function of known ground alone).
        var max = new double[count];
        var drawn = new int[count];
        var positive = new int[count];
        for (int i = 0; i < count; i++)
        {
            double[] v = values[i];
            for (int n = 0; n < nodes; n++)
            {
                if (double.IsNaN(v[n])) continue;
                drawn[i]++;
                if (v[n] > 0.0) positive[i]++;
                if (v[n] > max[i]) max[i] = v[n];
            }
            for (int n = 0; n < nodes; n++)
            {
                if (double.IsNaN(v[n])) continue;
                levels[i][n] = max[i] > 0.0 ? (byte)Math.Min(LevelCount - 1, (int)(v[n] / max[i] * LevelCount)) : (byte)0;
            }
        }
        return new ResourceOverlay(site, layers, lsize, block, knowledge.AllKnown, all, known, values, levels, max, drawn, withheld,
            positive, knownNodes, knownLand, judged, present, anyChannel ? unavailable : null, all ? site.Frontier.Floor : double.NaN,
            cfg.Catchment.HinterlandRadiusKm);
    }

    /// <summary>The bake as canonical bytes (little-endian; every layer's key, range and counts, then its levels and its
    /// values' IEEE-754 bits in node order) — what the determinism and non-interference tests compare.</summary>
    public byte[] CanonicalBytes()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Size);
            w.Write(LatticeSize);
            w.Write(Block);
            w.Write(AllKnown);
            w.Write(SeesAll);
            w.Write(KnownNodes);
            w.Write(KnownLandNodes);
            w.Write(JudgedForPresence);
            w.Write(PresentNodes);
            w.Write(ChannelsUnavailable ?? "");
            w.Write(BitConverter.DoubleToInt64Bits(Floor));
            w.Write(Layers.Count);
            for (int i = 0; i < Layers.Count; i++)
            {
                w.Write(Layers[i].Key);
                w.Write(BitConverter.DoubleToInt64Bits(_max[i]));
                w.Write(_drawn[i]);
                w.Write(_withheld[i]);
                w.Write(_positive[i]);
                w.Write(_levels[i]);
                foreach (double v in _values[i]) w.Write(BitConverter.DoubleToInt64Bits(v));
            }
        }
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ the per-turn status

    /// <summary>
    /// The per-turn half of the key for <paramref name="knowledge"/>'s player (see <see cref="OverlayStatus"/>): access,
    /// extraction and the realized ore rolls, each from the place card's own predicate. Reads only; writes nothing.
    /// </summary>
    public OverlayStatus Status(IReadOnlyWorldState world, SimConfig cfg, PlayerKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(knowledge);
        if (!ReferenceEquals(world.Terrain, Terrain)) throw new ArgumentException("the world is on another terrain", nameof(world));
        PolityId player = knowledge.Player;
        TraversalLattice lattice = _site.Frontier.Lattice;
        int size = Size;

        // ACCESSIBLE — the card's "reachable": a land route from a settlement the player controls. Only while the player sees
        // the whole world (routes run over the whole lattice and every road).
        SettlementId[] mine = LabourActivities.ControlledSettlements(world, player);
        int? reachable = null;
        if (SeesAll)
        {
            var reached = new bool[lattice.NodeCount];
            foreach (SettlementId s in mine)
            {
                int site = -1;
                for (int i = 0; i < world.Settlements.Count; i++) if (world.Settlements[i].Id == s) { site = world.Settlements[i].SiteCell; break; }
                if (site < 0) continue;
                int origin = LatticeMap.OriginLatticeNode(lattice, size, site);
                if (reached[origin]) continue;   // the same land: its isochrone is the one already taken
                foreach (int n in Pathfinder.Isochrone(lattice, world, origin, double.PositiveInfinity).Reached) reached[n] = true;
            }
            int count = 0;
            for (int node = 0; node < NodeCount; node++)
            {
                if (!_known[node]) continue;
                int cell = SampleCell(node);
                if (Terrain.Water[cell] >= 0.5) continue;
                if (reached[LatticeMap.OriginLatticeNode(lattice, size, cell)]) count++;
            }
            reachable = count;
        }

        // Worked now: the known land nodes in the player's own settlements' catchments (the player's own books).
        var claimed = new bool[NodeCount];
        for (int i = 0; i < world.CatchmentNodes.Count; i++)
        {
            CatchmentNodeRow row = world.CatchmentNodes[i];
            if ((uint)row.LatticeNode < (uint)claimed.Length && EmpireQuery.ControlsSettlement(world, player, row.Settlement)) claimed[row.LatticeNode] = true;
        }
        int inCatchments = 0;
        for (int node = 0; node < NodeCount; node++)
            if (claimed[node] && _known[node] && Terrain.Water[SampleCell(node)] < 0.5) inCatchments++;

        // EXTRACTABLE — the card's "workable", for the default source.
        int source = SiteAssessment.DefaultSource(world, player);
        bool? workable = source >= 0 ? LabourActivities.CanAllocate(world, player, new SettlementId(source)) : null;

        // The realized ore rolls at the player's own known settlements (its books).
        var rolls = new List<OreRoll>();
        if (cfg.Goods is { } goods)
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                SettlementRow row = world.Settlements[s];
                if (!knowledge.KnowsSettlement(row.Id.Value) || !knowledge.Core.ReadsInternals(row.Id)
                    || !EmpireQuery.ControlsSettlement(world, player, row.Id)) continue;
                for (int i = 0; i < world.Deposits.Count; i++)
                {
                    DepositRow d = world.Deposits[i];
                    if (d.Settlement != row.Id) continue;
                    GoodEntry? g = null;
                    foreach (GoodEntry e in goods.Goods) if (e.Id == d.Good.Value) { g = e; break; }
                    if (g is not { DepositChannel: { } ch } || !(g.DepositSpread > 0.0)) continue;
                    int x = row.SiteCell % size, y = row.SiteCell / size;
                    bool judgedHere = ChannelsUnavailable is null
                        && (SeesAll || SiteAssessment.KnowsDisc(knowledge.Core, size, x, y, WorldFounding.HinterlandRadiusPx(_site.Worldgen, cfg)));
                    double expected = judgedHere ? ChannelAt(Terrain, _site.Worldgen, cfg, ch, row.SiteCell) : double.NaN;
                    rolls.Add(new OreRoll(row.Id.Value, g.Id, g.Name, ch, d.Abundance, expected));
                }
            }
        return new OverlayStatus(reachable, inCatchments, source, workable, rolls);
    }

    // ------------------------------------------------------------------ the paint (the lens calls it)

    /// <summary>The deep ink of a layer (<see cref="MapInk"/>'s overlay tokens).</summary>
    public static Rgba DeepInk(MapInk k, OverlayLayer layer) => layer.Kind switch
    {
        OverlayKind.Suitability => k.OverlaySuitability,
        OverlayKind.RegionFertility => k.OverlayFertility,
        OverlayKind.WaterAccess => k.OverlayAccess,
        _ => layer.Channel switch
        {
            "moisture" => k.OverlayMoisture,
            "elevation" => k.OverlayElevation,
            _ => k.OverlayWater,
        },
    };

    /// <summary>The ramp position of a level (0 for the lowest, 1 for the highest).</summary>
    public static double RampOf(int level) => level / (double)(LevelCount - 1);

    /// <summary>The fill of level <paramref name="level"/> of <paramref name="layer"/> at overlay opacity
    /// <paramref name="opacity"/>: stronger value, deeper ink and more opaque — opacity × (0.25 + 0.75 × ramp), so the
    /// terrain always shows through (the opacity control's top step is below 1).</summary>
    public static Rgba Fill(MapInk k, OverlayLayer layer, int level, double opacity) =>
        k.OverlayRamp(DeepInk(k, layer), RampOf(level), opacity * (0.25 + 0.75 * RampOf(level)));

    /// <summary>
    /// Paints the shown layers (in layer order) as one rect per RUN of equal levels along each lattice row, edges snapped
    /// to whole screen px so neighbouring runs tile without a seam or an overlap; rows and runs off the viewport are not
    /// emitted. <paramref name="rects"/> and <paramref name="nodes"/> count, per layer, the rects emitted and the nodes
    /// they cover.
    /// </summary>
    internal void PaintLayers(DrawList d, IReadOnlyList<bool> shown, double opacity, Func<double, double, (double X, double Y)> toScreen,
        RectD viewport, MapInk k, int[] rects, int[] nodes)
    {
        int l = LatticeSize, b = Block;
        for (int i = 0; i < Layers.Count; i++)
        {
            if (i >= shown.Count || !shown[i]) continue;
            var fill = new Rgba[LevelCount];
            for (int v = 0; v < LevelCount; v++) fill[v] = Fill(k, Layers[i], v, opacity);
            byte[] lv = _levels[i];
            for (int ny = 0; ny < l; ny++)
            {
                double y0 = Math.Round(toScreen(0, ny * b).Y), y1 = Math.Round(toScreen(0, (ny + 1) * b).Y);
                if (y1 <= y0 || y1 <= viewport.Y || y0 >= viewport.Bottom) continue;
                int row = ny * l, nx = 0;
                while (nx < l)
                {
                    byte level = lv[row + nx];
                    if (level == NotDrawn) { nx++; continue; }
                    int start = nx;
                    while (nx < l && lv[row + nx] == level) nx++;
                    double x0 = Math.Round(toScreen(start * b, ny * b).X), x1 = Math.Round(toScreen(nx * b, ny * b).X);
                    if (x1 <= x0 || x1 <= viewport.X || x0 >= viewport.Right) continue;
                    d.Rect(new RectD(x0, y0, x1 - x0, y1 - y0), fill[level]);
                    rects[i]++;
                    nodes[i] += nx - start;
                }
            }
        }
    }

    // ------------------------------------------------------------------ the key's text (the lens sets it)

    /// <summary>The verbatim statement every channel layer is labelled with.</summary>
    public const string AbundanceStatement = "Expected abundance (rate multiplier); actual varies ±spread";

    /// <summary>The key's title.</summary>
    public const string KeyTitle = "Resource overlay";

    /// <summary>
    /// The key's rows for the shown layers, in order (unwrapped): the abundance statement when a channel is shown; per shown
    /// layer its name and range (its ramp is drawn under that row), the goods a channel drives (each with its spread), what it
    /// measures with its counts, the score's founding floor; then DISCOVERED, PRESENT, ACCESSIBLE and EXTRACTABLE, each from
    /// its predicate; then where a place's own card is and what is not simulated. <paramref name="name"/> names a settlement.
    /// </summary>
    public IReadOnlyList<OverlayKeyRow> KeyRows(IReadOnlyList<bool> shown, OverlayStatus? status, Func<int, string> name)
    {
        var rows = new List<OverlayKeyRow>();
        bool anyChannel = false;
        for (int i = 0; i < Layers.Count; i++) anyChannel |= i < shown.Count && shown[i] && Layers[i].Kind == OverlayKind.Channel;
        if (anyChannel) rows.Add(new OverlayKeyRow(AbundanceStatement));
        for (int i = 0; i < Layers.Count; i++)
        {
            if (i >= shown.Count || !shown[i]) continue;
            OverlayLayer layer = Layers[i];
            bool unevaluated = layer.Kind == OverlayKind.Channel && ChannelsUnavailable is not null;
            rows.Add(new OverlayKeyRow(layer.Name + (unevaluated ? ": not evaluated" : " " + RangeText(i)), i));
            if (layer.Kind == OverlayKind.Channel) rows.Add(new OverlayKeyRow(DrivesText(layer)));
            if (unevaluated) rows.Add(new OverlayKeyRow("Not evaluated: " + ChannelsUnavailable));
            else
                rows.Add(new OverlayKeyRow(layer.Measure + "; " + N0(Drawn(i)) + " places"
                    + (Withheld(i) > 0 ? ", " + N0(Withheld(i)) + " not judged (undiscovered ground in reach)" : "")));
            if (layer.Kind == OverlayKind.Suitability && SeesAll)
                rows.Add(new OverlayKeyRow("Founding floor " + Num3(Floor) + ": below it, never a site"));
            if (layer.Kind == OverlayKind.Channel && status is { } st)
            {
                bool rolled = false;
                foreach (OreRoll roll in st.Rolls) rolled |= roll.Channel == layer.Channel;
                if (rolled) rows.Add(new OverlayKeyRow("Rolled at your settlements (x the expected): marked beside them, closer in"));
            }
        }

        // DISCOVERED (PolityKnowledge.Known).
        rows.Add(new OverlayKeyRow(AllKnown
            ? "Discovered: every place known (no extent is stored yet)"
            : "Discovered: " + N0(KnownNodes) + " of " + N0(NodeCount) + " places; the rest is not drawn"));
        // PRESENT (the card's: a channel value above 0).
        rows.Add(new OverlayKeyRow(ChannelsUnavailable is not null ? "Present: not evaluated"
            : JudgedForPresence == 0 ? "Present: no place judged"
            : PresentNodes == JudgedForPresence
                ? "Present: every good above 0 at all " + N0(JudgedForPresence) + " judged places (no scarcity until D-051 s.2)"
                : "Present: every good above 0 at " + N0(PresentNodes) + " of " + N0(JudgedForPresence) + " judged places"));
        if (status is { } s)
        {
            // ACCESSIBLE (the card's "reachable"; and the catchments worked now).
            rows.Add(new OverlayKeyRow((s.Reachable is { } reach
                    ? "Accessible: " + N0(reach) + " of " + N0(KnownLandNodes) + " by land from your settlements"
                    : "Accessible: routes not judged (undiscovered ground)")
                + "; " + N0(s.InCatchments) + " in your catchments"));
            // EXTRACTABLE (the card's "workable").
            string herded = "";
            foreach (OverlayLayer layer in Layers)
                foreach (OverlayGood g in layer.Goods)
                    if (g.Herded) herded += (herded.Length > 0 ? ", " : "") + g.Name;
            string who = s.Source < 0 ? "you hold no settlement to set out from"
                : "a colony from " + name(s.Source) + (s.Workable == true ? " would be yours" : " would not be yours");
            rows.Add(new OverlayKeyRow("Extractable: herding works " + (herded.Length > 0 ? herded : "none") + ", extraction the rest"
                + " - no research gate; " + who));
        }
        rows.Add(new OverlayKeyRow("Shift+click a place for its card. Not simulated: a quantity per place"));
        return rows;
    }

    /// <summary>A layer's drawn range, "0 - max".</summary>
    private string RangeText(int layer) => "0 - " + Num3(Max(layer));

    /// <summary>The goods a channel drives, each with its spread — or, when none has one, "all exact": one field, the
    /// simulation draws no difference between them.</summary>
    public static string DrivesText(OverlayLayer layer)
    {
        bool anySpread = false;
        foreach (OverlayGood g in layer.Goods) anySpread |= g.Spread > 0.0;
        var sb = new StringBuilder("Drives ");
        for (int g = 0; g < layer.Goods.Count; g++)
        {
            if (g > 0) sb.Append(", ");
            sb.Append(layer.Goods[g].Name);
            if (anySpread) sb.Append(layer.Goods[g].Spread > 0.0 ? " (\u00B1" + Pct(layer.Goods[g].Spread) + ")" : " (exact)");
        }
        if (!anySpread) sb.Append(layer.Goods.Count > 1 ? ": all exact, one field - no difference between them" : ": exact");
        return sb.ToString();
    }

    /// <summary>The marker text of the realized rolls at one settlement for the shown channels (empty when none).</summary>
    public string MarkerText(IReadOnlyList<bool> shown, OverlayStatus status, int settlement)
    {
        var sb = new StringBuilder();
        foreach (OreRoll roll in status.Rolls)
        {
            if (roll.Settlement != settlement || !ChannelShown(shown, roll.Channel)) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(roll.GoodName).Append(' ').Append(Num3(roll.Abundance));
            if (!double.IsNaN(roll.Expected) && roll.Expected > 0.0) sb.Append(" (x").Append((roll.Abundance / roll.Expected).ToString("0.00", CultureInfo.InvariantCulture)).Append(')');
        }
        return sb.ToString();
    }

    /// <summary>Whether the channel layer of <paramref name="channel"/> is shown.</summary>
    public bool ChannelShown(IReadOnlyList<bool> shown, string channel)
    {
        for (int i = 0; i < Layers.Count; i++)
            if (i < shown.Count && shown[i] && Layers[i].Kind == OverlayKind.Channel && Layers[i].Channel == channel) return true;
        return false;
    }

    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);
    private static string N0(double v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string Num3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    private static string Pct(double v) => (v * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";
}
