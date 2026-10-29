using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.State;

/// <summary>
/// M5 map workstream P7 — THE INFORMATION-CLASS READ SEAM (<see cref="PolityKnowledge"/>) and the AI's information class
/// (Spine principle 7; Director: "the AI cannot use information it has not legitimately discovered").
///
/// <para><b>Which fog these tests run under (corrected 2026-10-28, M6 closure track FOG; this header first said "the seam is
/// all-known today", which stopped being true when M6 Track EX shipped the fog — ADR-044).</b> The PRODUCTION seam
/// (<see cref="PolityKnowledge.Of"/> under the shipped config) restricts: it answers from the stored known extent, the seed
/// discs, formation sight and sightings. The rigs of the P7 tests below (<c>FoundingRigs.Cfg()</c>) switch the fog OFF
/// (<c>TestConfigs.PreDiscovery</c>) and hide part of the world with the TEST stub (<see cref="PolityKnowledge.Restricted"/>)
/// — the stub's masks make a metamorphic rig of any shape; with the fog off <see cref="PolityKnowledge.Of"/> is all-known
/// (the attribution control). The FOG-ON legs (<c>…_UnderTheProductionFog</c>) run the same AI properties through the
/// production seam with no stub at all.</para>
///
/// The AI founding policy ranks only the cells the seam says its polity knows (a stub that hides the unrestricted pick
/// moves it to the oracle's best KNOWN legal cell, perturbing every unknown cell's own score moves nothing, and whatever the
/// terrain under unknown nodes the pick is the oracle's best known legal cell), and — D-066 A18 (closed in M6; the channel
/// ADR-037 §6 and ADR-036 §8.3 recorded, quarantined here until then) — VALUES a known cell only by the terrain it knows
/// (<see cref="KnownSiting"/>: region fertility, water access, the candidate floor and the reachability flood), so
/// perturbing terrain under unknown nodes leaves its order identical; the switch-off twin
/// (<c>TestConfigs.PreAiKnownTerrain</c>) reopens the channel (the attribution control). The AI producer's orders do not
/// depend on any other polity's books (a metamorphic test perturbing every foreign row, civics included);
/// AiConstructionPolicy asks the seam before it reads a queue; the formation card of a formation the player does not know
/// reads as an id that names nothing; and the good card classifies deposit sectors from content, never from another
/// polity's settlements.
/// </summary>
public class PolityKnowledgeTests
{
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private static readonly WorldgenConfig Wg = TestConfigs.Worldgen();
    private static SimConfig Cfg => FoundingRigs.Cfg();

    /// <summary>The canonical config with the AI founding thresholds opened (as AiFoundingPolicyTests' Lax).</summary>
    private static SimConfig Lax
    {
        get
        {
            SimConfig cfg = Cfg;
            return cfg with { Colonization = cfg.Colonization! with { AiFounding = new AiFoundingConfig(Enabled: true, MinSourcePopulation: 1, MinYearsBetweenFoundings: 0.0) } };
        }
    }

    private static readonly Lazy<WorldState> DuoTurn1 = new(
        () => FoundingRigs.Pipeline(FoundingRigs.Cfg(), null).Step(FoundingRigs.Dev(42UL, aiEmpires: 1)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static WorldState Duo1() => DuoTurn1.Value.Clone();

    private static int NodeCount(IReadOnlyWorldState w)
    {
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        return l * l;
    }

    private static bool[] AllTrue(int n)
    {
        var a = new bool[n];
        Array.Fill(a, true);
        return a;
    }

    // ================================================================== the seam

    [Fact]
    public void Of_IsAllKnown_EveryAnswerIsYes_AndTheBooksAreTheControllers()
    {
        WorldState w = Duo1();
        PolityKnowledge k = PolityKnowledge.Of(w, Player, null);
        Assert.True(k.AllKnown);
        Assert.Equal(Player, k.Polity);
        for (int n = 0; n < NodeCount(w); n += 97) { Assert.True(k.Known(n)); Assert.True(k.Visible(n)); }
        for (int cell = 0; cell < w.Terrain!.Size * w.Terrain.Size; cell += 1013) { Assert.True(k.KnowsCell(cell)); Assert.True(k.SeesCell(cell)); }
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            Assert.True(k.KnowsSettlement(s));
            Assert.Equal(EmpireQuery.ControlsSettlement(w, Player, s), k.ReadsInternals(s));
        }
        Assert.False(k.KnowsSettlement(new SettlementId(9999)));
        for (int i = 0; i < w.MilitaryUnits.Count; i++) Assert.True(k.KnowsUnit(w.MilitaryUnits[i]));
        Assert.True(k.KnowsPolity(Rival));
        // A world with no control relation at all (a hand-built toy) treats every settlement as the polity's — the guard
        // ConstructionQuery's availability applies.
        WorldState toy = Duo1();
        toy.Controls.Clear();
        Assert.True(PolityKnowledge.Of(toy, Rival, null).ReadsInternals(toy.Settlements[0].Id));
    }

    [Fact]
    public void Grid_IsTheTraversalLatticesGeometry_AndNodeOfCellIsItsBlock()
    {
        WorldState w = Duo1();
        TraversalLattice lattice = TraversalLattice.Build(w.Terrain!, Cfg.Transport.RiverCostFactor);
        (int l, int block) = PolityKnowledge.Grid(w.Terrain!.Size);
        Assert.Equal(lattice.Size, l);
        Assert.Equal(lattice.Stride, block);
        Assert.Equal(TraversalLattice.DefaultStride, lattice.Stride);
        foreach (int size in new[] { 250, 256, 1024 })
            Assert.Equal((size / 4, size / (size / 4)), PolityKnowledge.Grid(size));
        PolityKnowledge k = PolityKnowledge.Of(w, Player, null);
        int s = w.Terrain.Size;
        for (int cell = 0; cell < s * s; cell += 517)
        {
            int expected = Math.Min(cell / s / block, l - 1) * l + Math.Min(cell % s / block, l - 1);
            Assert.Equal(expected, k.NodeOfCell(cell));
            Assert.Equal(expected, k.NodeAt(cell % s + 0.5, cell / s + 0.25));
        }
        Assert.Equal(-1, k.NodeOfCell(-1));
        Assert.Equal(-1, k.NodeOfCell(s * s));
        Assert.Equal(-1, k.NodeAt(double.NaN, 0));
    }

    [Fact]
    public void Restricted_AnswersFromItsMasks_OwnThingsAreAlwaysKnown_AndContactIsAKnownSettlement()
    {
        WorldState w = Duo1();
        int n = NodeCount(w);
        PolityKnowledge any = PolityKnowledge.Of(w, Player, null);
        // Hide every node holding a site of the rival; see nothing else but keep it known.
        bool[] known = AllTrue(n), visible = AllTrue(n);
        var rivalSites = new List<SettlementId>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            if (!EmpireQuery.ControlsSettlement(w, Rival, w.Settlements[i].Id)) continue;
            rivalSites.Add(w.Settlements[i].Id);
            int node = any.NodeOfCell(w.Settlements[i].SiteCell);
            known[node] = false;
            visible[node] = false;
        }
        Assert.NotEmpty(rivalSites);
        PolityKnowledge k = PolityKnowledge.Restricted(w, Player, known, visible);
        Assert.False(k.AllKnown);
        foreach (SettlementId s in rivalSites)
        {
            Assert.False(k.KnowsSettlement(s));
            Assert.True(PolityKnowledge.Restricted(w, Rival, known, visible).KnowsSettlement(s));   // its own: always
        }
        Assert.False(k.KnowsPolity(Rival));
        Assert.True(k.KnowsPolity(Player));
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
            Assert.Equal(w.MilitaryUnits[i].Owner != Rival, k.KnowsUnit(w.MilitaryUnits[i]));

        // Known but unseen: the node is in the extent, the live settlement is not shown (no ghost memory before M7).
        bool[] seenNone = new bool[n];
        PolityKnowledge blind = PolityKnowledge.Restricted(w, Player, AllTrue(n), seenNone);
        Assert.True(blind.Known(0));
        Assert.False(blind.Visible(0));
        foreach (SettlementId s in rivalSites) Assert.False(blind.KnowsSettlement(s));
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, Player, w.Settlements[i].Id)) Assert.True(blind.KnowsSettlement(w.Settlements[i].Id));

        // The stub's contract: Visible ⊆ Known, one entry per node.
        Assert.Throws<ArgumentException>(() => PolityKnowledge.Restricted(w, Player, new bool[n], AllTrue(n)));
        Assert.Throws<ArgumentException>(() => PolityKnowledge.Restricted(w, Player, new bool[n - 1], new bool[n - 1]));
    }

    // ================================================================== the AI: the site scan ranks only what is known

    /// <summary>THE ORACLE: the best cell, by the composite key (score DESC, cell ASC), among the KNOWN cells the predicate
    /// answers Legal for — scored by the valuation the scan must use (D-066 A18): the issuer's known-terrain valuation
    /// (<see cref="KnownSiting"/>: its candidate arm and its reach over known nodes are admission arms too) whenever the
    /// knowledge restricts and the switch is on; else the frontier's raster.</summary>
    private static int Oracle(IReadOnlyWorldState w, SimConfig cfg, SettlementSiting.FrontierSiting f, SettlementId source, PolityKnowledge k)
    {
        KnownSiting? v = AiFoundingPolicy.ValuationFor(cfg, f, w.Terrain!, k);
        double[] scores = v?.Scores ?? f.Scores;
        bool[]? reach = v is null ? null
            : Pathfinder.ReachableWithin(f.Lattice, w, LatticeMap.OriginLatticeNode(f.Lattice, f.Size, FoundingRigs.SiteCellOf(w, source)), v.KnownNodes);
        int[] keyed = Enumerable.Range(0, scores.Length).Where(c => scores[c] > 0.0 && k.KnowsCell(c)
                && (v is null || (v.IsCandidate(w.Terrain!, c) && reach![LatticeMap.OriginLatticeNode(f.Lattice, f.Size, c)])))
            .OrderByDescending(c => scores[c]).ThenBy(c => c).ToArray();
        foreach (int cell in keyed)
            if (FoundingQuery.Check(w, cfg, f, Rival, source, cell) == FoundingVerdict.Legal) return cell;
        return -1;
    }

    [Fact]
    public void TheAiSiteScan_RanksOnlyTheCellsItsPolityKnows()
    {
        WorldState w = Duo1();
        SimConfig cfg = Lax;
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        OrderRecord free = AiFoundingPolicy.Decide(w, cfg, Wg, Rival) ?? throw new InvalidOperationException("no AI order - the rig is void");
        SettlementId source = new(free.TargetId);
        int freeCell = FoundingQuery.SiteCellOf(free);

        // The rival does not know the node of its unrestricted pick.
        PolityKnowledge any = PolityKnowledge.Of(w, Rival, null);
        bool[] known = AllTrue(NodeCount(w));
        known[any.NodeOfCell(freeCell)] = false;
        PolityKnowledge k = PolityKnowledge.Restricted(w, Rival, known, known);
        Assert.False(k.KnowsCell(freeCell));

        OrderRecord restricted = AiFoundingPolicy.Decide(w, cfg, Rival, new AiFoundingPolicy.Sites(w, cfg, Wg), FoundingQuery.Check, k)
            ?? throw new Xunit.Sdk.XunitException("no AI order with one node unknown");
        int oracle = Oracle(w, cfg, f, source, k);
        Assert.True(oracle >= 0 && oracle != freeCell);
        Assert.Equal(FoundingQuery.FoundOrder(w, Rival, source, oracle), restricted);
        Assert.True(k.KnowsCell(FoundingQuery.SiteCellOf(restricted)));
        // The production entry is the all-known seam: the same order as before the seam existed.
        Assert.Equal(free, AiFoundingPolicy.Decide(w, cfg, Rival, new AiFoundingPolicy.Sites(w, cfg, Wg), FoundingQuery.Check, PolityKnowledge.Of(w, Rival, null)));
        // The knowledge must be the issuer's.
        Assert.Throws<ArgumentException>(() => AiFoundingPolicy.Decide(w, cfg, Rival, new AiFoundingPolicy.Sites(w, cfg, Wg), FoundingQuery.Check, PolityKnowledge.Of(w, Player, null)));
    }

    [Fact]
    public void AnUnknownCellsOwnScore_CannotMoveThePick_HoweverHighItIs()
    {
        // METAMORPHIC ON THE SCORE RASTER: the rival knows only the half of the map holding its source. Every unknown
        // cell's OWN score is raised above every known one; the pick is the same cell. The control: all-known, the same
        // raised scores DO move the pick. What this does NOT cover (P7 review, finding 1): a known cell's score is itself
        // computed from terrain under unknown nodes — the TERRAIN tests below perturb that, and record the channel.
        WorldState w = Duo1();
        SimConfig cfg = Lax;
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        SettlementId source = AiFoundingPolicy.RankedSources(w, cfg, cfg.Colonization!.AiFounding!, Rival)[0];
        int sourceCell = FoundingRigs.SiteCellOf(w, source);
        bool[] known = HalfMapKnown(w, sourceCell);   // keep the source's half known
        PolityKnowledge k = PolityKnowledge.Restricted(w, Rival, known, known);
        Assert.True(k.KnowsCell(sourceCell));

        double top = 0.0;
        foreach (double v in f.Scores) top = Math.Max(top, v);
        var raised = (double[])f.Scores.Clone();
        int moved = 0;
        for (int c = 0; c < raised.Length; c++)
            if (!k.KnowsCell(c) && raised[c] > 0.0) { raised[c] = top * 10.0 + c; moved++; }
        Assert.True(moved > 1000);
        var g = new SettlementSiting.FrontierSiting
        {
            Lattice = f.Lattice, Scores = raised, Floor = f.Floor, MinSpacingCostUnits = f.MinSpacingCostUnits, Size = f.Size,
            Siting = f.Siting,
        };
        int before = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, k);
        int after = AiFoundingPolicy.BestSiteFor(w, cfg, g, w.Terrain!, new AiFoundingPolicy.Sites(w, g), Rival, source, FoundingQuery.Check, k);
        Assert.True(before >= 0, "no known legal cell - the rig is void");
        Assert.Equal(before, after);
        PolityKnowledge all = PolityKnowledge.Of(w, Rival, null);
        int controlBefore = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, all);
        int controlAfter = AiFoundingPolicy.BestSiteFor(w, cfg, g, w.Terrain!, new AiFoundingPolicy.Sites(w, g), Rival, source, FoundingQuery.Check, all);
        Assert.NotEqual(controlBefore, controlAfter);
        Assert.False(k.KnowsCell(controlAfter));
    }

    // ================================================================== the AI: terrain under unknown nodes (P7 review)

    /// <summary>A copy of <paramref name="terrain"/> whose FERTILITY is <paramref name="value"/> in every cell
    /// <paramref name="k"/> does not know, every other layer kept (water, movement cost and rivers — the geography the
    /// lattice, the reachability flood and the catchments are built over — so the copy is the same map to every path).
    /// <paramref name="changedKnown"/> counts KNOWN cells whose fertility changed: always 0, the rig's own check.</summary>
    private static TerrainSet FertilityUnderUnknownNodes(TerrainSet terrain, PolityKnowledge k, double value, out int changedUnknown, out int changedKnown)
    {
        double[] fertility = terrain.Fertility.ToArray();
        changedUnknown = 0;
        changedKnown = 0;
        for (int cell = 0; cell < fertility.Length; cell++)
        {
            if (k.KnowsCell(cell)) continue;
            if (fertility[cell] != value) changedUnknown++;
            fertility[cell] = value;
        }
        for (int cell = 0; cell < fertility.Length; cell++)
            if (k.KnowsCell(cell) && fertility[cell] != terrain.Fertility[cell]) changedKnown++;
        var polylines = new int[terrain.RiverPolylineCount][];
        for (int i = 0; i < polylines.Length; i++) polylines[i] = terrain.RiverPolyline(i).ToArray();
        // Batch 3: the public test/tool factory replaces the reflection into the internal constructor (whose signature
        // gained the topology flag); the copy keeps the world's topology, so it is the same map to every path.
        return TerrainSet.FromLayers(terrain.Size, terrain.KmPerPx, terrain.SeaLevel, terrain.Elevation, terrain.Water,
            terrain.Temperature, terrain.Moisture, fertility, terrain.MovementCost, terrain.Rivers, polylines, terrain.WrapX);
    }

    /// <summary>A copy of <paramref name="terrain"/> with a river in every cell <paramref name="k"/> does not know (every other
    /// layer kept; the river polylines too).</summary>
    private static TerrainSet RiversUnderUnknownNodes(TerrainSet terrain, PolityKnowledge k)
    {
        double[] rivers = terrain.Rivers.ToArray();
        Assert.Equal(terrain.Size * terrain.Size, rivers.Length);
        for (int cell = 0; cell < rivers.Length; cell++) if (!k.KnowsCell(cell)) rivers[cell] = 1.0;
        var polylines = new int[terrain.RiverPolylineCount][];
        for (int i = 0; i < polylines.Length; i++) polylines[i] = terrain.RiverPolyline(i).ToArray();
        return TerrainSet.FromLayers(terrain.Size, terrain.KmPerPx, terrain.SeaLevel, terrain.Elevation, terrain.Water,
            terrain.Temperature, terrain.Moisture, terrain.Fertility, terrain.MovementCost, rivers, polylines, terrain.WrapX);
    }

    /// <summary>The rival's knowledge of the half of the map (by lattice column) holding <paramref name="sourceCell"/> —
    /// the mask <see cref="AnUnknownCellsOwnScore_CannotMoveThePick_HoweverHighItIs"/> uses.</summary>
    private static bool[] HalfMapKnown(IReadOnlyWorldState w, int sourceCell)
    {
        PolityKnowledge any = PolityKnowledge.Of(w, Rival, null);
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        int half = any.NodeOfCell(sourceCell) % l <= l / 2 ? 1 : 0;
        bool[] known = new bool[l * l];
        for (int node = 0; node < known.Length; node++) known[node] = (node % l <= l / 2 ? 1 : 0) == half;
        return known;
    }

    [Fact]
    public void TerrainUnderUnknownNodes_NeverPutsAnUnknownCellInThePick_ThePickIsTheOraclesBestKnownLegalCell()
    {
        // THE GUARANTEE THE SEAM GIVES (P7 review, finding 1): whatever the terrain under the nodes the rival does not
        // know, it founds on a cell it KNOWS, and that cell is the oracle's best KNOWN legal cell of the world as it is.
        // The perturbation is TERRAIN (fertility under every unknown node, 0 and 1), as the UI leak gate perturbs it —
        // not the score raster. POSITIVE CONTROL: all-known, fertility 1 under those nodes puts the pick on one of them,
        // so the restricted scan is tested on a world where an unknown cell outranks every known one.
        SimConfig cfg = Lax;
        WorldState w0 = Duo1();
        SettlementId source = AiFoundingPolicy.RankedSources(w0, cfg, cfg.Colonization!.AiFounding!, Rival)[0];
        bool[] known = HalfMapKnown(w0, FoundingRigs.SiteCellOf(w0, source));
        bool controlMovedToUnknown = false;
        foreach (double value in new[] { 0.0, 1.0 })
        {
            WorldState w = Duo1();
            w.Terrain = FertilityUnderUnknownNodes(w.Terrain!, PolityKnowledge.Restricted(w, Rival, known, known), value, out int changed, out int changedKnown);
            PolityKnowledge k = PolityKnowledge.Restricted(w, Rival, known, known);
            Assert.True(changed > 1000, "the perturbation must reach the unknown half");
            Assert.Equal(0, changedKnown);
            SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
            int pick = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, k);
            Assert.True(pick >= 0, "no known legal cell - the rig is void");
            Assert.True(k.KnowsCell(pick), "fertility " + value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " under unknown nodes put an UNKNOWN cell in the pick");
            Assert.Equal(Oracle(w, cfg, f, source, k), pick);
            OrderRecord order = AiFoundingPolicy.Decide(w, cfg, Rival, new AiFoundingPolicy.Sites(w, f), FoundingQuery.Check, k)
                ?? throw new Xunit.Sdk.XunitException("no AI order");
            Assert.True(k.KnowsCell(FoundingQuery.SiteCellOf(order)));

            int control = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, PolityKnowledge.Of(w, Rival, null));
            if (control >= 0 && !k.KnowsCell(control)) controlMovedToUnknown = true;
        }
        Assert.True(controlMovedToUnknown, "all-known, no terrain perturbation put the pick on an unknown cell - the restricted scan was never tested against one");
    }

    /// <summary>The rival's knowledge with ONLY the eight lattice nodes around <paramref name="cell"/>'s node hidden.</summary>
    private static bool[] EightAround(IReadOnlyWorldState w, int cell)
    {
        PolityKnowledge any = PolityKnowledge.Of(w, Rival, null);
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        int pickNode = any.NodeOfCell(cell);
        bool[] known = AllTrue(l * l);
        int hidden = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = pickNode % l + dx, y = pickNode / l + dy;
                if ((dx == 0 && dy == 0) || x < 0 || y < 0 || x >= l || y >= l) continue;
                known[y * l + x] = false;
                hidden++;
            }
        Assert.Equal(8, hidden);
        return known;
    }

    [Fact]
    public void KnownCellsValuations_ReadOnlyKnownTerrain_TerrainUnderUnknownNodesLeavesThePickIdentical_D066A18()
    {
        // THE CHANNEL ADR-037 §6 / ADR-036 §8.3 RECORDED, CLOSED (D-066 A18; quarantined here, in the Cr003Quarantine pattern,
        // until then): the pre-filter's KnowsCell arm stopped an UNKNOWN cell being ranked, but a KNOWN cell was VALUED by the
        // frontier raster — region fertility (a box sum over ±fertilityRadiusPx, reaching the neighbouring 4-px nodes), water
        // access (a BFS over the whole map) and the candidate floor (the median over every cell) — so terrain under unknown
        // nodes moved a known cell's score, the floor, and the pick between KNOWN cells. Now the scan ranks by the issuer's
        // KnownSiting. THE INVARIANCE TOOTH (restored): the eight nodes around the unrestricted pick hidden, zeroing their
        // fertility leaves the valuation (every score, the floor, the known nodes) and the pick identical. ATTRIBUTION
        // CONTROL: the switch-off twin (TestConfigs.PreAiKnownTerrain) values by the whole-map raster again, and the same
        // perturbation moves its pick (P7's measurement: 36463 → 42559, both known).
        SimConfig cfg = Lax;
        Assert.True(cfg.Colonization!.AiFounding!.KnownTerrainOnly);
        WorldState w0 = Duo1();
        OrderRecord free = AiFoundingPolicy.Decide(w0, cfg, Wg, Rival) ?? throw new InvalidOperationException("no AI order - the rig is void");
        SettlementId source = new(free.TargetId);
        int freeCell = FoundingQuery.SiteCellOf(free);
        bool[] known = EightAround(w0, freeCell);

        PolityKnowledge k0 = PolityKnowledge.Restricted(w0, Rival, known, known);
        SettlementSiting.FrontierSiting f0 = FoundingRigs.Frontier(w0);
        int before = AiFoundingPolicy.BestSiteFor(w0, cfg, f0, w0.Terrain!, new AiFoundingPolicy.Sites(w0, f0), Rival, source, FoundingQuery.Check, k0);
        Assert.True(before >= 0 && k0.KnowsCell(before), "no known legal cell - the rig is void");

        WorldState w1 = Duo1();
        w1.Terrain = FertilityUnderUnknownNodes(w1.Terrain!, k0, 0.0, out int changed, out int changedKnown);
        Assert.True(changed > 0);
        Assert.Equal(0, changedKnown);   // not one KNOWN cell's terrain changed
        PolityKnowledge k1 = PolityKnowledge.Restricted(w1, Rival, known, known);
        SettlementSiting.FrontierSiting f1 = FoundingRigs.Frontier(w1);
        Assert.NotEqual(f0.Scores[freeCell], f1.Scores[freeCell]);   // the perturbation reaches a known cell's WHOLE-MAP score

        // The valuation the scan ranks by: identical, cell for cell.
        KnownSiting v0 = AiFoundingPolicy.ValuationFor(cfg, f0, w0.Terrain!, k0)!, v1 = AiFoundingPolicy.ValuationFor(cfg, f1, w1.Terrain!, k1)!;
        Assert.NotNull(v0);
        Assert.Equal(v0.Floor, v1.Floor);
        Assert.Equal(v0.KnownNodes, v1.KnownNodes);
        Assert.True(v0.Scores.AsSpan().SequenceEqual(v1.Scores), "a known cell's known-terrain score moved with terrain under unknown nodes");
        Assert.Equal(0.0, v0.Scores[freeCell] - v1.Scores[freeCell]);

        int after = AiFoundingPolicy.BestSiteFor(w1, cfg, f1, w1.Terrain!, new AiFoundingPolicy.Sites(w1, f1), Rival, source, FoundingQuery.Check, k1);
        Assert.Equal(before, after);
        Assert.Equal(AiFoundingPolicy.Decide(w0, cfg, Rival, new AiFoundingPolicy.Sites(w0, f0), FoundingQuery.Check, k0),
            AiFoundingPolicy.Decide(w1, cfg, Rival, new AiFoundingPolicy.Sites(w1, f1), FoundingQuery.Check, k1));

        // ATTRIBUTION CONTROL: the switch off reopens the channel — the whole-map valuation keeps the known free pick, and
        // terrain under unknown nodes moves it to another KNOWN cell.
        SimConfig off = TestConfigs.PreAiKnownTerrain(cfg);
        Assert.Null(AiFoundingPolicy.ValuationFor(off, f0, w0.Terrain!, k0));
        int offBefore = AiFoundingPolicy.BestSiteFor(w0, off, f0, w0.Terrain!, new AiFoundingPolicy.Sites(w0, f0), Rival, source, FoundingQuery.Check, k0);
        int offAfter = AiFoundingPolicy.BestSiteFor(w1, off, f1, w1.Terrain!, new AiFoundingPolicy.Sites(w1, f1), Rival, source, FoundingQuery.Check, k1);
        Assert.Equal(freeCell, offBefore);
        Assert.True(offAfter >= 0 && k1.KnowsCell(offAfter));
        Assert.NotEqual(offBefore, offAfter);
    }

    [Fact]
    public void TheKnownTerrainValuation_IsAFunctionOfKnownTerrainOnly_HalfTheMapsFertilityWaterAndRiversPerturbed()
    {
        // The valuation alone (no predicate): the rival knows half the map; under the other half the fertility is set to 0
        // and to 1, every cell is made water, and every cell a river. Its scores, floor, known nodes and its reach over the
        // known nodes are identical in all four. CONTROL: the whole-map raster moves at known cells in each.
        SimConfig cfg = Lax;
        WorldState w0 = Duo1();
        SettlementId source = AiFoundingPolicy.RankedSources(w0, cfg, cfg.Colonization!.AiFounding!, Rival)[0];
        int sourceCell = FoundingRigs.SiteCellOf(w0, source);
        bool[] known = HalfMapKnown(w0, sourceCell);
        PolityKnowledge k0 = PolityKnowledge.Restricted(w0, Rival, known, known);
        SettlementSiting.FrontierSiting f0 = FoundingRigs.Frontier(w0);
        KnownSiting v0 = AiFoundingPolicy.ValuationFor(cfg, f0, w0.Terrain!, k0)!;
        Assert.NotNull(v0);
        int origin = LatticeMap.OriginLatticeNode(f0.Lattice, f0.Size, sourceCell);
        bool[] reach0 = Pathfinder.ReachableWithin(f0.Lattice, w0, origin, v0.KnownNodes);
        int positive = 0;
        foreach (double v in v0.Scores) if (v > 0.0) positive++;
        Assert.True(positive > 1000, "the known half must hold candidates - the rig is void");

        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            WorldState w = Duo1();
            TerrainSet t = w.Terrain!;
            double[] fertility = t.Fertility.ToArray(), water = t.Water.ToArray(), rivers = t.Rivers.ToArray();
            for (int cell = 0; cell < fertility.Length; cell++)
            {
                if (k0.KnowsCell(cell)) continue;
                if (variant <= 1) fertility[cell] = variant;
                if (variant == 2) water[cell] = 1.0;
                if (variant == 3 && rivers.Length == fertility.Length) rivers[cell] = 1.0;
            }
            var polylines = new int[t.RiverPolylineCount][];
            for (int i = 0; i < polylines.Length; i++) polylines[i] = t.RiverPolyline(i).ToArray();
            w.Terrain = TerrainSet.FromLayers(t.Size, t.KmPerPx, t.SeaLevel, t.Elevation, water, t.Temperature, t.Moisture, fertility,
                t.MovementCost, rivers, polylines, t.WrapX);
            PolityKnowledge k = PolityKnowledge.Restricted(w, Rival, known, known);
            SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
            KnownSiting v = AiFoundingPolicy.ValuationFor(cfg, f, w.Terrain!, k)!;
            string what = "variant " + variant.ToString(CultureInfo.InvariantCulture);
            Assert.True(v0.Scores.AsSpan().SequenceEqual(v.Scores), what + ": a known-terrain score moved");
            Assert.Equal(v0.Floor, v.Floor);
            Assert.Equal(v0.KnownNodes, v.KnownNodes);
            int movedRaster = 0;
            for (int cell = 0; cell < f.Scores.Length; cell++) if (k.KnowsCell(cell) && !f0.Scores[cell].Equals(f.Scores[cell])) movedRaster++;
            Assert.True(movedRaster > 0, what + ": the perturbation never reached a known cell's whole-map score - the control is void");
            if (variant != 2)   // water under the unknown half re-cuts the lattice there; the known nodes' reach is asked only of known nodes
                Assert.Equal(reach0, Pathfinder.ReachableWithin(f.Lattice, w, origin, v.KnownNodes));
        }
    }

    [Fact]
    public void TheAiSiteScan_ReachesOnlyThroughKnownLand_AKnownIslandIsNotASite_D066A18()
    {
        // D-066 A18's reachability arm: the scan's flood crosses only KNOWN lattice nodes. The rival knows a square of nodes
        // around its source and, separately, a 5 × 5-node ISLAND around the cell it would pick with the whole map known —
        // the island joined to the square only through unknown nodes. Its pick is NOT on the island (it cannot know a way
        // there). CONTROL: the same knowledge with a known corridor joining the two (a straight band of nodes), and the
        // island is reachable again — the pick is an island cell.
        SimConfig cfg = Lax;
        WorldState w = Duo1();
        OrderRecord free = AiFoundingPolicy.Decide(w, cfg, Wg, Rival) ?? throw new InvalidOperationException("no AI order - the rig is void");
        SettlementId source = new(free.TargetId);
        PolityKnowledge any = PolityKnowledge.Of(w, Rival, null);
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        int sn = any.NodeOfCell(FoundingRigs.SiteCellOf(w, source)), pn = any.NodeOfCell(FoundingQuery.SiteCellOf(free));
        int sx = sn % l, sy = sn / l, px = pn % l, py = pn / l;
        Assert.True(Math.Max(Math.Abs(px - sx), Math.Abs(py - sy)) >= 9, "the free pick must lie away from the source - the rig is void");
        bool[] Mask(bool corridor)
        {
            bool[] known = new bool[l * l];
            for (int n = 0; n < known.Length; n++)
            {
                int x = n % l, y = n / l;
                bool square = Math.Abs(x - sx) <= 3 && Math.Abs(y - sy) <= 3;
                bool island = Math.Abs(x - px) <= 2 && Math.Abs(y - py) <= 2;
                known[n] = square || island;
            }
            if (corridor)
            {
                // A band three nodes wide along the straight line from the source to the pick.
                int steps = Math.Max(Math.Abs(px - sx), Math.Abs(py - sy));
                for (int i = 0; i <= steps; i++)
                {
                    int cx = sx + (px - sx) * i / steps, cy = sy + (py - sy) * i / steps;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = cx + dx, y = cy + dy;
                            if (x >= 0 && y >= 0 && x < l && y < l) known[y * l + x] = true;
                        }
                }
            }
            return known;
        }
        bool OnIsland(int cell)
        {
            int n = any.NodeOfCell(cell);
            return Math.Abs(n % l - px) <= 2 && Math.Abs(n / l - py) <= 2;
        }
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        bool[] islandOnly = Mask(false), joined = Mask(true);
        PolityKnowledge k = PolityKnowledge.Restricted(w, Rival, islandOnly, islandOnly);
        int pick = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, k);
        Assert.False(pick >= 0 && OnIsland(pick), "the scan picked a known cell it can reach only through unknown land");
        Assert.Equal(Oracle(w, cfg, f, source, k), pick);
        PolityKnowledge kj = PolityKnowledge.Restricted(w, Rival, joined, joined);
        int control = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, kj);
        Assert.True(control >= 0 && OnIsland(control), "joined by known land, the island's best cell is not picked - the control is void");
    }

    [Fact]
    public void ReachableWithin_EveryNodeAdmitted_IsTheUncappedIsochrone_AndItNeverEntersANonAdmittedNode()
    {
        WorldState w = Duo1();
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        int origin = LatticeMap.OriginLatticeNode(f.Lattice, f.Size, w.Settlements[0].SiteCell);
        bool[] all = AllTrue(f.Lattice.NodeCount);
        Assert.Equal(AiFoundingPolicy.ReachableNodes(w, f, w.Settlements[0].SiteCell), Pathfinder.ReachableWithin(f.Lattice, w, origin, all));
        // Half the lattice (by column) barred: nothing barred is reached, and a reached node is connected to the origin
        // through admitted nodes only (BFS over the 8-neighbourhood of admitted passable nodes — the dev rig has no lane).
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        bool[] half = new bool[l * l];
        for (int n = 0; n < half.Length; n++) half[n] = n % l <= l / 2 == (origin % l <= l / 2);
        bool[] reached = Pathfinder.ReachableWithin(f.Lattice, w, origin, half);
        var bfs = new bool[half.Length];
        var queue = new Queue<int>();
        bfs[origin] = true;
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = MapTopology.NeighbourX(c % l, dx, l, f.Lattice.WrapX), y = c / l + dy;
                    if (x < 0 || y < 0 || y >= l) continue;
                    int nb = y * l + x;
                    if (bfs[nb] || !half[nb] || !f.Lattice.IsPassable(nb)) continue;
                    bfs[nb] = true;
                    queue.Enqueue(nb);
                }
        }
        Assert.Equal(0, w.TransportEdges.Count);
        Assert.Equal(bfs, reached);
        for (int n = 0; n < half.Length; n++) if (!half[n]) Assert.False(reached[n]);
        bool[] none = new bool[half.Length];
        Assert.DoesNotContain(true, Pathfinder.ReachableWithin(f.Lattice, w, origin, none));
    }

    // ================================================================== the AI producer: no other polity's books

    /// <summary>Perturbs every row of a settlement <paramref name="ai"/> does not control (people and stocks through the
    /// Ledger; deposits, dwellings, structures, a queued project with its WS1 progress, the labour split, deficits), every
    /// formation it does not own, every other polity's WS2 material split row (D-054, schema v33), and (M6 closure, track
    /// FOG) every other polity's CIVICS state: its Civics Points purse (balance, generated, spent — a row added where it has
    /// none) and its civic research (civics-tree nodes completed, progress on one, and a civics target).</summary>
    private static void PerturbForeignBooks(WorldState w, PolityId ai, SimConfig cfg, int salt)
    {
        var ledger = new Ledger(w.LedgerFlows);
        bool Foreign(SettlementId s) => !EmpireQuery.ControlsSettlement(w, ai, s);
        for (int i = 0; i < w.Buckets.Count; i++)
            if (Foreign(w.Buckets[i].Settlement))
                ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment, 11 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (Foreign(w.GoodStocks[i].Settlement))
                ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(w.GoodStocks[i].Good), ReasonIds.InitialEndowment, 5000 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
        for (int i = 0; i < w.Housing.Count; i++)
            if (Foreign(w.Housing[i].Settlement))
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings, ReasonIds.InitialEndowment, 9 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
        for (int i = 0; i < w.Deposits.Count; i++)
            if (Foreign(w.Deposits[i].Settlement)) w.Deposits[i] = w.Deposits[i] with { Abundance = w.Deposits[i].Abundance * 2.5 + 0.1 };
        int project = cfg.Goods!.Projects![0].Id;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            if (!Foreign(s)) continue;
            w.Structures.Add(new StructureRow(s, project + 500, 3 + salt));
            w.ConstructionQueue.Add(new ConstructionQueueRow(s, 50 + salt, project));
            w.ConstructionProgress.Add(new ConstructionProgressRow(s, 50 + salt, project, 2 + salt, 300_000 + 1_000 * salt));
        }
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Id.Value != ai.Value)
                w.ResourceAllocations.Add(new ResourceAllocationRow(w.Polities[i].Id, 35 + salt));
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (Foreign(w.SectorAllocations[i].Settlement))
                w.SectorAllocations[i] = new SectorAllocationRow(w.SectorAllocations[i].Settlement, 0.05, 0.05, 0.6, 0.2, 0.1);
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (Foreign(w.ConsumptionDeficits[i].Settlement)) w.ConsumptionDeficits[i] = w.ConsumptionDeficits[i] with { DeficitRatio = 0.8 };
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
            if (w.MilitaryUnits[i].Owner.Value != ai.Value)
                w.MilitaryUnits[i] = w.MilitaryUnits[i] with { Experience = w.MilitaryUnits[i].Experience + 4.0 + salt };

        // Foreign CIVICS state (M6 closure, track FOG).
        Sim.Core.Systems.Research.ResearchContent research = cfg.Research!;
        var civics = new List<ResearchNodeId>();
        foreach (Sim.Core.Systems.Research.ResearchNode node in research.Nodes)
            if (node.Tree == Sim.Core.Systems.Research.ResearchTree.Civics) civics.Add(node.Key);
        Assert.True(civics.Count >= 4, "the content has a civics tree");
        for (int i = 0; i < w.Polities.Count; i++)
        {
            PolityId other = w.Polities[i].Id;
            if (other.Value == ai.Value) continue;
            int purse = -1;
            for (int j = 0; j < w.CivicsPoints.Count; j++) if (w.CivicsPoints[j].Polity == other) purse = j;
            if (purse >= 0)
                w.CivicsPoints[purse] = w.CivicsPoints[purse] with
                {
                    BalanceMilli = w.CivicsPoints[purse].BalanceMilli + 7_000_000 + salt,
                    GeneratedMilli = w.CivicsPoints[purse].GeneratedMilli + 9_000_000 + salt,
                    SpentMilli = w.CivicsPoints[purse].SpentMilli + 2_000_000,
                };
            else w.CivicsPoints.Add(new CivicsPointsRow(other, 7_000_000 + salt, 9_000_000 + salt, 2_000_000));
            int added = 0;
            foreach (ResearchNodeId node in civics)
            {
                if (added == 2) break;
                bool has = false;
                for (int j = 0; j < w.ResearchCompleted.Count; j++) has |= w.ResearchCompleted[j].Polity == other && w.ResearchCompleted[j].Node == node;
                if (has) continue;
                w.ResearchCompleted.Add(new ResearchCompletedRow(other, node));
                added++;
            }
            w.ResearchProgress.Add(new ResearchProgressRow(other, civics[^1], 123.0 + salt));
            w.ResearchTargets.Add(new ResearchTargetRow(other, civics[^2]));
        }
    }

    [Fact]
    public void TheAiProducersOrders_DoNotDependOnAnyOtherPolitysBooks()
    {
        // METAMORPHIC (the fog audit's AI non-interference test, unblocked from S2b by the Director's P7 addition): the
        // dev world with one AI Empire is played by the real producer for twelve turns with the founding thresholds open
        // and the AI's settlements stocked with every good (so construction, roads and founding all fire). On every
        // turn, perturbing every row of every settlement the AI does not control — and every formation it does not own —
        // leaves AiOrders.For identical. The control: perturbing the AI's OWN books changes its orders.
        SimConfig cfg = Lax;
        var log = new OrderLog();
        TurnExecutor exec = FoundingRigs.Pipeline(cfg, log);
        WorldState w = Duo1();
        var kinds = new SortedSet<OrderKind>();
        bool ownChanges = false;
        for (int t = 0; t < 12; t++)
        {
            for (int i = 0; i < w.Settlements.Count; i++)
            {
                SettlementId s = w.Settlements[i].Id;
                if (!EmpireQuery.ControlsSettlement(w, Rival, s)) continue;
                foreach (GoodEntry g in cfg.Goods!.Goods) TopUp(w, cfg, s, g.Id, 4000);
            }
            OrderRecord[] orders = AiOrders.For(w, cfg, Wg);
            foreach (OrderRecord o in orders) kinds.Add(o.Kind);
            WorldState foreign = w.Clone();
            PerturbForeignBooks(foreign, Rival, cfg, t);
            Assert.Equal(orders, AiOrders.For(foreign, cfg, Wg));
            if (!ownChanges)
            {
                WorldState own = w.Clone();
                for (int i = 0; i < own.GoodStocks.Count; i++)
                    if (EmpireQuery.ControlsSettlement(own, Rival, own.GoodStocks[i].Settlement))
                        new Ledger(own.LedgerFlows).Flow(ref own.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(own.GoodStocks[i].Good),
                            ReasonIds.InitialEndowment, own.GoodStocks[i].Amount.Value, FlowDirection.Sink, OverdrawPolicy.Throw);
                ownChanges = !AiOrders.For(own, cfg, Wg).SequenceEqual(orders);
            }
            foreach (OrderRecord o in orders) log.Append(o);
            w = exec.Step(w);
        }
        Assert.True(ownChanges, "emptying the AI's own stocks never changed its orders - the control is void");
        Assert.Contains(OrderKind.FoundSettlement, kinds);
        Assert.Contains(OrderKind.EnqueueConstruction, kinds);
        Assert.Contains(OrderKind.SetResearchTarget, kinds);
    }

    private static void TopUp(WorldState w, SimConfig cfg, SettlementId s, int good, long target)
    {
        var id = new GoodId(good);
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have >= target) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id),
            ReasonIds.InitialEndowment, target - have, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    [Fact]
    public void AiConstructionPolicy_AsksTheSeamBeforeItReadsAnyQueue()
    {
        // STRUCTURAL PIN (the reorder is information hygiene, not behaviour: a foreign queue could never change the output,
        // so no output test can see the order of the two reads). In every settlement loop of the policy that reads a
        // queue, the seam's ReadsInternals test comes first.
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Sim.slnx"))) root = Path.GetDirectoryName(root)!;
        string src = File.ReadAllText(Path.Combine(root, "Sim.Core", "Systems", "Construction", "AiConstructionPolicy.cs"));
        MatchCollection loops = Regex.Matches(src, @"for \(int s = 0; s < world\.Settlements\.Count; s\+\+\)\s*\{(?<body>.*?)\n        \}", RegexOptions.Singleline);
        int queueLoops = 0;
        foreach (Match m in loops)
        {
            string body = m.Groups["body"].Value;
            int queue = body.IndexOf("ConstructionQuery.Queue(", StringComparison.Ordinal);
            if (queue < 0) continue;
            queueLoops++;
            int seam = body.IndexOf("ReadsInternals(settlement)", StringComparison.Ordinal);
            Assert.True(seam >= 0 && seam < queue, "a settlement loop reads a queue before asking the seam:\n" + body);
        }
        Assert.Equal(2, queueLoops);
    }

    // ================================================================== FOG-ON legs: the PRODUCTION seam, no stub (M6 closure, track FOG)

    /// <summary>The SHIPPED config (fog and discovery ON) with the AI founding thresholds opened.</summary>
    private static SimConfig FogLax
    {
        get
        {
            SimConfig cfg = TestConfigs.Sim();
            Assert.True(DiscoveryQuery.FogOn(cfg));
            return cfg with { Colonization = cfg.Colonization! with { AiFounding = cfg.Colonization.AiFounding! with { MinSourcePopulation = 1, MinYearsBetweenFoundings = 0.0 } } };
        }
    }

    private static readonly Lazy<WorldState> FogDuoTurn1 = new(
        () => FoundingRigs.Pipeline(TestConfigs.Sim(), null).Step(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.Sim(), 42UL)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The dev world with one AI Empire under the SHIPPED fog, after one played step (extents and sightings stored).</summary>
    private static WorldState FogDuo1() => FogDuoTurn1.Value.Clone();

    /// <summary>Replaces <paramref name="polity"/>'s stored extent by <paramref name="bits"/> (non-zero words), every table
    /// row kept in (polity, word) order — the state an Explore of that reach leaves (ADR-044 §7).</summary>
    private static void StoreExtent(WorldState w, PolityId polity, ulong[] bits)
    {
        var rows = new List<KnownExtentRow>();
        for (int i = 0; i < w.KnownExtents.Count; i++) if (w.KnownExtents[i].Polity != polity) rows.Add(w.KnownExtents[i]);
        for (int word = 0; word < bits.Length; word++) if (bits[word] != 0) rows.Add(new KnownExtentRow(polity, word, bits[word]));
        rows.Sort((a, b) => a.Polity.Value != b.Polity.Value ? a.Polity.Value.CompareTo(b.Polity.Value) : a.Word.CompareTo(b.Word));
        w.KnownExtents.Clear();
        foreach (KnownExtentRow row in rows) w.KnownExtents.Add(row);
    }

    [Fact]
    public void Of_UnderTheProductionFog_IsTheStoredExtentAndTheSight_AndHidesTheUnseenRival()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = FogDuo1();
        PolityKnowledge k = PolityKnowledge.Of(w, Player, cfg);
        Assert.False(k.AllKnown);
        ulong[] known = DiscoveryQuery.KnownBits(w, cfg.Fog!, Player, cfg.Research)!, visible = DiscoveryQuery.VisibleBits(w, cfg.Fog!, Player, cfg.Research)!;
        int knownNodes = 0;
        for (int n = 0; n < k.LatticeSize * k.LatticeSize; n++)
        {
            Assert.Equal(DiscoveryQuery.Has(known, n), k.Known(n));
            Assert.Equal(DiscoveryQuery.Has(visible, n), k.Visible(n));
            if (k.Known(n)) knownNodes++;
        }
        Assert.True(knownNodes > 0 && knownNodes < k.LatticeSize * k.LatticeSize / 4, "the production fog restricts");
        int rivalSettlements = 0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            bool own = EmpireQuery.ControlsSettlement(w, Player, s);
            Assert.Equal(own, k.ReadsInternals(s));
            if (own) { Assert.True(k.KnowsSettlement(s)); Assert.True(k.Visible(k.NodeOfCell(w.Settlements[i].SiteCell))); }
            else if (EmpireQuery.ControlsSettlement(w, Rival, s)) { rivalSettlements++; Assert.False(k.KnowsSettlement(s), "the rival is unsighted on this rig"); }
        }
        Assert.True(rivalSettlements >= 1);
        Assert.False(k.KnowsPolity(Rival));
        for (int i = 0; i < w.MilitaryUnits.Count; i++) Assert.Equal(w.MilitaryUnits[i].Owner == Player, k.KnowsUnit(w.MilitaryUnits[i]));
        // The control: the fog off, the same world is all-known.
        Assert.True(PolityKnowledge.Of(w, Player, TestConfigs.PreDiscovery(cfg)).AllKnown);
    }

    /// <summary>The fog-on founding rig: the dev world with one AI Empire under the SHIPPED fog after one step, the rival
    /// having explored to radius <paramref name="radius"/> around its first source (the state an Explore of that reach
    /// stores — ADR-044 §7), so known legal sites exist. Radius 7: MEASURED 2026-10-28 on this tree as the reach whose
    /// best known site lies near the edge of the known land (radii 8–12 put the AI's pick and the whole-map pick on the
    /// same interior cell, 15774, which no perturbation under unknown nodes moves either way — a void control).</summary>
    private static (WorldState World, SettlementId Source, PolityKnowledge Knowledge) FogFoundingRig(SimConfig cfg, int radius = 7)
    {
        WorldState w = FogDuo1();
        SettlementId source = AiFoundingPolicy.RankedSources(w, cfg, cfg.Colonization!.AiFounding!, Rival)[0];
        (int l, _, _, bool wrap) = DiscoveryQuery.Lattice(w);
        ulong[] bits = DiscoveryQuery.StoredExtent(w, Rival, DiscoveryQuery.WordCount(l));
        DiscoveryQuery.OrDisc(bits, l, wrap, DiscoveryQuery.NodeOfSettlement(w, source), radius);
        StoreExtent(w, Rival, bits);
        PolityKnowledge k = PolityKnowledge.Of(w, Rival, cfg);
        Assert.False(k.AllKnown);
        return (w, source, k);
    }

    [Fact]
    public void TheAiSiteScan_UnderTheProductionFog_IsTheOraclesBestKnownLegalCell_AndTerrainUnderUnknownNodesLeavesEveryAiOrderIdentical()
    {
        // The production seam (no stub). The AI's founding order is the oracle's best KNOWN legal cell by its known-terrain
        // valuation, and perturbing the FERTILITY under every node it does not know (0 or 1 — the valuation's channel of
        // ADR-037 §6) leaves EVERY order of the AI producer identical (D-066 A18). ATTRIBUTION CONTROL: with the switch off
        // (TestConfigs.PreAiKnownTerrain) the same perturbation moves its founding order (MEASURED: 15774 → 15516 at
        // fertility 0). What the predicate itself reads of unknown land is the recorded residual below.
        SimConfig cfg = FogLax;
        (WorldState w, _, PolityKnowledge k) = FogFoundingRig(cfg);
        OrderRecord found = AiFoundingPolicy.Decide(w, cfg, Wg, Rival) ?? throw new Xunit.Sdk.XunitException("no AI founding order under the fog - the rig is void");
        int site = FoundingQuery.SiteCellOf(found);
        Assert.True(k.KnowsCell(site));
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w, cfg);
        Assert.Equal(FoundingQuery.FoundOrder(w, Rival, new SettlementId(found.TargetId), Oracle(w, cfg, f, new SettlementId(found.TargetId), k)), found);
        OrderRecord[] orders = AiOrders.For(w, cfg, Wg);
        Assert.Contains(found, orders);

        SimConfig off = TestConfigs.PreAiKnownTerrain(cfg);
        OrderRecord? offFound = AiFoundingPolicy.Decide(w, off, Wg, Rival);
        bool controlMoved = false;
        foreach (double value in new[] { 0.0, 1.0 })
        {
            WorldState p = w.Clone();
            p.Terrain = FertilityUnderUnknownNodes(p.Terrain!, k, value, out int changed, out int changedKnown);
            Assert.True(changed > 1000);
            Assert.Equal(0, changedKnown);
            Assert.Equal(orders, AiOrders.For(p, cfg, Wg));
            controlMoved |= !Equals(offFound, AiFoundingPolicy.Decide(p, off, Wg, Rival));
        }
        Assert.True(controlMoved, "with the switch off, terrain under unknown nodes never moved the founding order - the control is void");
    }

    [Fact]
    public void RecordedResidual_ThePredicatesOwnArms_ReadLandTheIssuerDoesNotKnow_SharedByThePlayerAndTheAi()
    {
        // RECORDED, NOT CLOSED (ADR-044 dated note 2026-10-28; D-066 A19's M7 owner). A18 closes the AI's VALUATION; THE
        // FOUNDING PREDICATE (FoundingQuery.Check — the rule ColonizationSystem applies, which the player's placement mode and
        // the AI both ask) still judges its candidate floor over the whole map, and spacing and the route over the whole
        // lattice. A river under every unknown cell (the lattice's river cost and the water access change there) leaves the
        // AI's valuation identical, yet the predicate now REFUSES the AI's former pick — so the order moves through the
        // rule. The player is told the same verdict for the same cell (one predicate), so this is not an AI-only channel;
        // restricting the rule's arms to the known extent is a rule change shared with the player, owned with the M7
        // movement/route graph (A19). This test asserts the residual is still OPEN: when the predicate stops reading unknown
        // land it fails — then delete it and record the closure.
        SimConfig cfg = FogLax;
        (WorldState w, SettlementId source, PolityKnowledge k) = FogFoundingRig(cfg);
        OrderRecord found = AiFoundingPolicy.Decide(w, cfg, Wg, Rival) ?? throw new Xunit.Sdk.XunitException("no AI founding order - the rig is void");
        int site = FoundingQuery.SiteCellOf(found);
        WorldState p = w.Clone();
        p.Terrain = RiversUnderUnknownNodes(p.Terrain!, k);
        PolityKnowledge kp = PolityKnowledge.Of(p, Rival, cfg);
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w, cfg), fp = FoundingRigs.Frontier(p, cfg);
        KnownSiting v = AiFoundingPolicy.ValuationFor(cfg, f, w.Terrain!, k)!, vp = AiFoundingPolicy.ValuationFor(cfg, fp, p.Terrain!, kp)!;
        Assert.True(v.Scores.AsSpan().SequenceEqual(vp.Scores));   // the valuation: closed
        Assert.Equal(v.Floor, vp.Floor);
        Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(w, cfg, f, Rival, new SettlementId(found.TargetId), site));
        FoundingVerdict now = FoundingQuery.Check(p, cfg, fp, Rival, new SettlementId(found.TargetId), site);
        Assert.False(now == FoundingVerdict.Legal,
            "RESIDUAL CLOSED - the founding predicate no longer reads unknown land at the AI's pick; delete this test and strike the residual from the ADR-044 note of 2026-10-28.");
        Assert.NotEqual(found, AiFoundingPolicy.Decide(p, cfg, Wg, Rival));
        _ = source;
    }

    [Fact]
    public void TheAiProducersOrders_UnderTheProductionFog_DoNotDependOnAnyOtherPolitysBooks()
    {
        // The fog-on leg of TheAiProducersOrders_DoNotDependOnAnyOtherPolitysBooks: the SHIPPED fog, twelve played turns,
        // every foreign row perturbed (civics included) — AiOrders.For identical every turn. Control: the AI's own stocks.
        SimConfig cfg = FogLax;
        var log = new OrderLog();
        TurnExecutor exec = FoundingRigs.Pipeline(cfg, log);
        WorldState w = FogDuo1();
        var kinds = new SortedSet<OrderKind>();
        bool ownChanges = false;
        for (int t = 0; t < 12; t++)
        {
            for (int i = 0; i < w.Settlements.Count; i++)
            {
                SettlementId s = w.Settlements[i].Id;
                if (!EmpireQuery.ControlsSettlement(w, Rival, s)) continue;
                foreach (GoodEntry g in cfg.Goods!.Goods) TopUp(w, cfg, s, g.Id, 4000);
            }
            OrderRecord[] orders = AiOrders.For(w, cfg, Wg);
            foreach (OrderRecord o in orders) kinds.Add(o.Kind);
            WorldState foreign = w.Clone();
            PerturbForeignBooks(foreign, Rival, cfg, t);
            Assert.Equal(orders, AiOrders.For(foreign, cfg, Wg));
            if (!ownChanges)
            {
                WorldState own = w.Clone();
                for (int i = 0; i < own.GoodStocks.Count; i++)
                    if (EmpireQuery.ControlsSettlement(own, Rival, own.GoodStocks[i].Settlement))
                        new Ledger(own.LedgerFlows).Flow(ref own.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(own.GoodStocks[i].Good),
                            ReasonIds.InitialEndowment, own.GoodStocks[i].Amount.Value, FlowDirection.Sink, OverdrawPolicy.Throw);
                ownChanges = !AiOrders.For(own, cfg, Wg).SequenceEqual(orders);
            }
            foreach (OrderRecord o in orders) log.Append(o);
            w = exec.Step(w);
        }
        Assert.True(ownChanges, "emptying the AI's own stocks never changed its orders - the control is void");
        Assert.Contains(OrderKind.EnqueueConstruction, kinds);
        Assert.Contains(OrderKind.SetResearchTarget, kinds);
        Assert.True(kinds.Contains(OrderKind.Explore) || kinds.Contains(OrderKind.FoundSettlement), "the AI neither explored nor founded");
    }

    [Fact]
    public void TheFormationCard_UnderTheProductionFog_OfTheUnseenRivalsFormation_ReadsAsAnIdThatNamesNothing()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = FogDuo1();
        PolityKnowledge k = PolityKnowledge.Of(w, Player, cfg);
        MilitaryUnitRow rival = default;
        bool found = false;
        for (int i = 0; i < w.MilitaryUnits.Count && !found; i++)
            if (w.MilitaryUnits[i].Owner == Rival) { rival = w.MilitaryUnits[i]; found = true; }
        Assert.True(found);
        Assert.False(k.KnowsUnit(rival));
        InfoCard hidden = InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(rival.Id), knowledge: k);
        InfoCard nothing = InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(987654), knowledge: k);
        Assert.Equal(InfoStatus.Unknown, hidden.Status);
        Assert.Equal(nothing with { Subject = hidden.Subject }, hidden);
        Assert.Equal(InfoStatus.Known, InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(rival.Id), knowledge: PolityKnowledge.Of(w, Player, TestConfigs.PreDiscovery(cfg))).Status);
    }

    // ================================================================== InfoQuery

    [Fact]
    public void TheFormationCard_OfAFormationThePlayerDoesNotKnow_ReadsAsAnIdThatNamesNothing()
    {
        WorldState w = Duo1();
        SimConfig cfg = Cfg;
        int n = NodeCount(w);
        bool[] known = AllTrue(n);
        PolityKnowledge any = PolityKnowledge.Of(w, Player, null);
        MilitaryUnitRow rival = default;
        bool found = false;
        for (int i = 0; i < w.MilitaryUnits.Count && !found; i++)
            if (w.MilitaryUnits[i].Owner == Rival) { rival = w.MilitaryUnits[i]; found = true; }
        Assert.True(found);
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].Id == rival.Location) known[any.NodeOfCell(w.Settlements[i].SiteCell)] = false;
        known[any.NodeAt(rival.X, rival.Y)] = false;
        PolityKnowledge k = PolityKnowledge.Restricted(w, Player, known, known);
        Assert.False(k.KnowsUnit(rival));

        InfoCard hidden = InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(rival.Id), knowledge: k);
        InfoCard nothing = InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(987654), knowledge: k);
        Assert.Equal(InfoStatus.Unknown, hidden.Status);
        Assert.Equal(nothing with { Subject = hidden.Subject }, hidden);
        // All-known (the default), the same id is a rival's formation card.
        InfoCard seen = InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(rival.Id));
        Assert.Equal(InfoStatus.Known, seen.Status);
        Assert.Contains("rival", seen.Kind, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(rival.Id), knowledge: PolityKnowledge.Of(w, Rival, null)));
    }

    [Fact]
    public void TheGoodCardsDepositSector_IsProductionsContentRule_NeverAnotherPolitysSettlement()
    {
        SimConfig cfg = Cfg;
        GoodsConfig goods = cfg.Goods!;
        WorldState w = Duo1();
        // IsHerded is the rule GoodsOf files deposits under: on every settlement, a deposit good with abundance is listed
        // under Herding iff IsHerded, under Extraction otherwise.
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            ImmutableArray<GoodId> herd = LabourActivities.GoodsOf(w, goods, s, Sectors.Herding).Goods;
            ImmutableArray<GoodId> extract = LabourActivities.GoodsOf(w, goods, s, Sectors.Extraction).Goods;
            foreach (GoodId g in herd) Assert.True(LabourActivities.IsHerded(goods, g.Value));
            foreach (GoodId g in extract) Assert.False(LabourActivities.IsHerded(goods, g.Value));
        }
        // The card names the sector by that rule — also for a good NO settlement holds a deposit of (it used to scan the
        // world's settlements, foreign ones included, and fall back to Extraction when none held it).
        int fish = goods.IdOf("fish");
        Assert.True(fish >= 0 && LabourActivities.IsHerded(goods, fish));
        WorldState none = Duo1();
        var keep = new List<DepositRow>();
        for (int i = 0; i < none.Deposits.Count; i++) if (none.Deposits[i].Good.Value != fish) keep.Add(none.Deposits[i]);
        none.Deposits.Clear();
        foreach (DepositRow d in keep) none.Deposits.Add(d);
        foreach (WorldState world in new[] { w, none })
        {
            InfoCard card = InfoQuery.Card(world, cfg, Player, InfoSubject.OfGood(fish));
            Assert.Contains(card.EnabledBy, l => l.Subject == InfoSubject.Sector(Sectors.Herding));
            Assert.DoesNotContain(card.EnabledBy, l => l.Subject == InfoSubject.Sector(Sectors.Extraction));
        }
        // And it does not move with another polity's deposits: the rival's fish deposits removed, the card is unchanged.
        WorldState noRivalFish = Duo1();
        var kept = new List<DepositRow>();
        for (int i = 0; i < noRivalFish.Deposits.Count; i++)
        {
            DepositRow d = noRivalFish.Deposits[i];
            if (d.Good.Value == fish && EmpireQuery.ControlsSettlement(noRivalFish, Rival, d.Settlement)) continue;
            kept.Add(d);
        }
        noRivalFish.Deposits.Clear();
        foreach (DepositRow d in kept) noRivalFish.Deposits.Add(d);
        Assert.Equal(InfoQuery.Card(w, cfg, Player, InfoSubject.OfGood(fish)), InfoQuery.Card(noRivalFish, cfg, Player, InfoSubject.OfGood(fish)));
    }
}
