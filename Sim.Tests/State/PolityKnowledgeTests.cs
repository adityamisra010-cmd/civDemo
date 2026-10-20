using System.Collections.Immutable;
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
/// (Spine principle 7; Director: "the AI cannot use information it has not legitimately discovered"). The seam is
/// all-known today and answers from explicit masks only through its test stub; the AI founding policy ranks only the
/// cells the seam says its polity knows (a stub that hides the unrestricted pick moves it to the oracle's best KNOWN
/// legal cell, perturbing every unknown cell's own score moves nothing, and whatever the terrain under unknown nodes the
/// pick is the oracle's best known legal cell — while a known cell's VALUATION still integrates that terrain, the channel
/// ADR-037 §6 records, quarantined here until S2b decides it); the AI producer's orders do not depend on any
/// other polity's books (a metamorphic test perturbing every foreign row); AiConstructionPolicy asks the seam before
/// it reads a queue; the formation card of a formation the player does not know reads as an id that names nothing; and
/// the good card classifies deposit sectors from content, never from another polity's settlements.
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
        PolityKnowledge k = PolityKnowledge.Of(w, Player);
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
        Assert.True(PolityKnowledge.Of(toy, Rival).ReadsInternals(toy.Settlements[0].Id));
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
        PolityKnowledge k = PolityKnowledge.Of(w, Player);
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
        PolityKnowledge any = PolityKnowledge.Of(w, Player);
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

    private static int Oracle(IReadOnlyWorldState w, SimConfig cfg, SettlementSiting.FrontierSiting f, SettlementId source, PolityKnowledge k)
    {
        int[] keyed = Enumerable.Range(0, f.Scores.Length).Where(c => f.Scores[c] > 0.0 && k.KnowsCell(c))
            .OrderByDescending(c => f.Scores[c]).ThenBy(c => c).ToArray();
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
        PolityKnowledge any = PolityKnowledge.Of(w, Rival);
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
        Assert.Equal(free, AiFoundingPolicy.Decide(w, cfg, Rival, new AiFoundingPolicy.Sites(w, cfg, Wg), FoundingQuery.Check, PolityKnowledge.Of(w, Rival)));
        // The knowledge must be the issuer's.
        Assert.Throws<ArgumentException>(() => AiFoundingPolicy.Decide(w, cfg, Rival, new AiFoundingPolicy.Sites(w, cfg, Wg), FoundingQuery.Check, PolityKnowledge.Of(w, Player)));
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
        };
        int before = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, k);
        int after = AiFoundingPolicy.BestSiteFor(w, cfg, g, w.Terrain!, new AiFoundingPolicy.Sites(w, g), Rival, source, FoundingQuery.Check, k);
        Assert.True(before >= 0, "no known legal cell - the rig is void");
        Assert.Equal(before, after);
        PolityKnowledge all = PolityKnowledge.Of(w, Rival);
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

    /// <summary>The rival's knowledge of the half of the map (by lattice column) holding <paramref name="sourceCell"/> —
    /// the mask <see cref="AnUnknownCellsOwnScore_CannotMoveThePick_HoweverHighItIs"/> uses.</summary>
    private static bool[] HalfMapKnown(IReadOnlyWorldState w, int sourceCell)
    {
        PolityKnowledge any = PolityKnowledge.Of(w, Rival);
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

            int control = AiFoundingPolicy.BestSiteFor(w, cfg, f, w.Terrain!, new AiFoundingPolicy.Sites(w, f), Rival, source, FoundingQuery.Check, PolityKnowledge.Of(w, Rival));
            if (control >= 0 && !k.KnowsCell(control)) controlMovedToUnknown = true;
        }
        Assert.True(controlMovedToUnknown, "all-known, no terrain perturbation put the pick on an unknown cell - the restricted scan was never tested against one");
    }

    [Fact]
    public void KnownCellsValuations_StillIntegrateTerrainUnderUnknownNodes_TheRecordedChannelQuarantined()
    {
        // THE RECORDED CHANNEL (P7 review, finding 1; ADR-037 §3 and §6, ADR-036 §8.3): the pre-filter's KnowsCell arm
        // stops an UNKNOWN cell being ranked, but a KNOWN cell's valuation is the frontier raster's — region fertility
        // (a box sum over ±fertilityRadiusPx, which reaches the neighbouring 4-px nodes), water access (a BFS over the
        // whole map) and the candidate floor (the median over every cell) — so terrain under unknown nodes moves a known
        // cell's score, the floor, and the pick between KNOWN cells. Which valuation a polity may hold of a known cell
        // whose neighbourhood it has not discovered is S2b's (ADR-036 §8 item (h)); it is not decided here.
        //
        // THE PROPERTY THE SYSTEM OUGHT TO HAVE is invariance — Assert.Equal(before, after) below. It does not hold
        // today, so it is QUARANTINED in the Cr003Quarantine pattern, not skipped: the test asserts the channel is still
        // OPEN, every other assertion stays live, and the moment S2b closes it this test fails, telling the reader to
        // restore the invariance tooth.
        SimConfig cfg = Lax;
        WorldState w0 = Duo1();
        OrderRecord free = AiFoundingPolicy.Decide(w0, cfg, Wg, Rival) ?? throw new InvalidOperationException("no AI order - the rig is void");
        SettlementId source = new(free.TargetId);
        int freeCell = FoundingQuery.SiteCellOf(free);

        // Hide ONLY the eight lattice nodes around the free pick's node; the pick's own node stays known.
        PolityKnowledge any = PolityKnowledge.Of(w0, Rival);
        (int l, _) = PolityKnowledge.Grid(w0.Terrain!.Size);
        int pickNode = any.NodeOfCell(freeCell);
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

        PolityKnowledge k0 = PolityKnowledge.Restricted(w0, Rival, known, known);
        SettlementSiting.FrontierSiting f0 = FoundingRigs.Frontier(w0);
        int before = AiFoundingPolicy.BestSiteFor(w0, cfg, f0, w0.Terrain!, new AiFoundingPolicy.Sites(w0, f0), Rival, source, FoundingQuery.Check, k0);
        Assert.Equal(freeCell, before);   // the free pick is known, so the restricted scan keeps it

        WorldState w1 = Duo1();
        w1.Terrain = FertilityUnderUnknownNodes(w1.Terrain!, k0, 0.0, out int changed, out int changedKnown);
        Assert.True(changed > 0);
        Assert.Equal(0, changedKnown);   // not one KNOWN cell's terrain changed
        PolityKnowledge k1 = PolityKnowledge.Restricted(w1, Rival, known, known);
        SettlementSiting.FrontierSiting f1 = FoundingRigs.Frontier(w1);
        int after = AiFoundingPolicy.BestSiteFor(w1, cfg, f1, w1.Terrain!, new AiFoundingPolicy.Sites(w1, f1), Rival, source, FoundingQuery.Check, k1);
        Assert.True(after >= 0 && k1.KnowsCell(after), "the pick left the known cells - the seam's guarantee is broken, not just the channel");
        Assert.NotEqual(f0.Scores[freeCell], f1.Scores[freeCell]);   // the known pick's own valuation moved

        Assert.False(before == after,
            "P7 CHANNEL QUARANTINE RESOLVED - terrain under unknown nodes no longer moves the AI's pick between KNOWN cells "
            + "(cell " + before.ToString(System.Globalization.CultureInfo.InvariantCulture) + " both ways). The valuation is "
            + "now a function of known terrain: DELETE this Assert.False and restore the invariance tooth, "
            + "Assert.Equal(before, after), and strike the channel from ADR-037 section 6 and ADR-036 section 8.3.");
    }

    // ================================================================== the AI producer: no other polity's books

    /// <summary>Perturbs every row of a settlement <paramref name="ai"/> does not control (people and stocks through the
    /// Ledger; deposits, dwellings, structures, a queued project with its WS1 progress, the labour split, deficits), every
    /// formation it does not own, and every other polity's WS2 material split row (D-054, schema v33).</summary>
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

    // ================================================================== InfoQuery

    [Fact]
    public void TheFormationCard_OfAFormationThePlayerDoesNotKnow_ReadsAsAnIdThatNamesNothing()
    {
        WorldState w = Duo1();
        SimConfig cfg = Cfg;
        int n = NodeCount(w);
        bool[] known = AllTrue(n);
        PolityKnowledge any = PolityKnowledge.Of(w, Player);
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
        Assert.Throws<ArgumentException>(() => InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(rival.Id), knowledge: PolityKnowledge.Of(w, Rival)));
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
