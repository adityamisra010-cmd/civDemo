using System.Collections.Immutable;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.State;

/// <summary>
/// M5 map workstream P8 — THE SITE ASSESSMENT (<see cref="SiteAssessment"/>) and the map cell's info card. Every value is
/// the simulation's own function's value (exact equality, never an epsilon): the ONE composite (the unjittered siting
/// score) decomposed into its two factors with its rank against the floor; the terrain fields; occupancy, spacing and
/// claim; the hinterland means and expected deposits; the route; the party and provisions; and FoundingQuery's verdict,
/// whose first failing arm the per-arm breakdown reproduces over a dense sweep. A settlement founded at the cell with the
/// previewed id gets EXACTLY the previewed deposit rows, and the catchment recompute gives it exactly the previewed arable
/// land. Assessing and carding change no hash. An unknown cell is refused (the P7 stub): nothing of it is read, whatever
/// lies there (metamorphic, with an all-known control). The nearest-settlement order is the composite integer key
/// (squared distance, id), tie-dense. The card shows the composite first and every other factor in its own section
/// labelled "not part of the score".
///
/// The worlds are the dev preset (256², 4 settlements, seed 42) after one orderless production step. The site half is
/// prepared with the DEV worldgen config and the founding pipelines are built with the same config, so the deposit
/// preview is evaluated with the config founding uses (see <see cref="AWorldgenConfigForAnotherMapSize_LeavesTheDepositsUnevaluated"/>).
/// </summary>
public class SiteAssessmentTests
{
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private static readonly WorldgenConfig DevWg = TestConfigs.DevWorldgen();
    private static SimConfig Cfg => FoundingRigs.Cfg();

    private static readonly Lazy<WorldState> Turn1 = new(
        () => FoundingRigs.Pipeline(FoundingRigs.Cfg(), null, TestConfigs.DevWorldgen()).Step(FoundingRigs.Dev(42UL)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<WorldState> DuoTurn1 = new(
        () => FoundingRigs.Pipeline(FoundingRigs.Cfg(), null, TestConfigs.DevWorldgen()).Step(FoundingRigs.Dev(42UL, aiEmpires: 1)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static WorldState W1() => Turn1.Value.Clone();
    private static WorldState Duo1() => DuoTurn1.Value.Clone();

    private static SiteAssessment.Prepared Site(IReadOnlyWorldState w, WorldgenConfig? wg = null) =>
        SiteAssessment.Prepare(w, Cfg, wg ?? DevWg) ?? throw new InvalidOperationException("no terrain");

    private static SiteAssessment.Report Assess(IReadOnlyWorldState w, SiteAssessment.Prepared site, int cell, int source = -1, PolityKnowledge? k = null) =>
        SiteAssessment.Assess(w, Cfg, site, k ?? PolityKnowledge.Of(w, Player), Player, cell, source);

    private static OrderLog Log(params OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return log;
    }

    private static int[] Occupied(IReadOnlyWorldState w)
    {
        var o = new int[w.Settlements.Count];
        for (int i = 0; i < o.Length; i++) o[i] = w.Settlements[i].SiteCell;
        return o;
    }

    /// <summary>A stable integer sample of cells: every ring cell within <paramref name="radius"/> of each settlement's site,
    /// and every <paramref name="stride"/>-th cell of the map.</summary>
    private static List<int> Sample(IReadOnlyWorldState w, int radius, int stride)
    {
        int size = w.Terrain!.Size;
        var cells = new List<int>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            cells.Add(w.Settlements[i].SiteCell);
            for (int r = 1; r <= radius; r++) cells.AddRange(FoundingRigs.Ring(size, w.Settlements[i].SiteCell, r));
        }
        for (int cell = 0; cell < size * size; cell += stride) cells.Add(cell);
        return cells;
    }

    // ================================================================== THE COMPOSITE

    [Fact]
    public void TheComposite_IsTheSimulationsUnjitteredSitingScore_ExactlyItsTwoFactorsMultiplied_RankedAgainstTheFloor()
    {
        WorldState w = W1();
        TerrainSet t = w.Terrain!;
        SitingConfig siting = DevWg.Siting;
        SiteAssessment.Prepared site = Site(w);
        // Independent recomputation, through the public functions only.
        double[] scores = SettlementSiting.CandidateScores(t, siting);
        double[] region = SettlementSiting.BoxSum(t.Fertility, t.Size, siting.FertilityRadiusPx);
        int[] bfs = SettlementSiting.WaterDistanceBfs(t.Water, t.Rivers, t.Size);
        double floor = SettlementSiting.ScoreFloor(scores, siting.ScoreFloorPercentile);
        double[] positive = [.. scores.Where(s => s > 0.0)];
        Array.Sort(positive);
        int land = 0, water = 0, scored = 0;
        foreach (int cell in Sample(w, 6, 211))
        {
            SiteAssessment.Report r = Assess(w, site, cell);
            Assert.True(r.Known);
            Assert.Equal(scores[cell], r.Score);
            Assert.Equal(region[cell], r.RegionFertility);
            Assert.Equal(bfs[cell], r.WaterDistancePx);
            Assert.Equal(SettlementSiting.WaterAccess(bfs[cell], siting.WaterAccessCutoffPx), r.WaterAccess);
            if (t.Water[cell] < 0.5 && r.WaterAccess > 0.0)
            {
                Assert.Equal(r.RegionFertility * r.WaterAccess, r.Score);   // the score IS the two factors' product, bit for bit
                land++;
            }
            else
            {
                Assert.Equal(0.0, r.Score);
                water++;
            }
            Assert.Equal(floor, r.Floor);
            Assert.Equal(siting.ScoreFloorPercentile, r.FloorPercentile);
            Assert.Equal(positive.Length, r.PositiveScores);
            int below = 0;
            while (below < positive.Length && positive[below] < r.Score) below++;
            Assert.Equal(below, r.ScoresBelow);
            Assert.Equal(SettlementSiting.IsFrontierCandidate(site.Frontier, t, cell), r.Candidate);
            if (r.Score > 0.0) scored++;
        }
        Assert.True(land > 50 && water > 50 && scored > 50, "the sample covers land, water and scored cells");
    }

    // ================================================================== EVERY OTHER VALUE

    [Fact]
    public void EveryOtherValue_IsItsOwnFunctionsValue_TerrainObstructionClaimHinterlandDepositsPartyRouteVerdict()
    {
        WorldState w = Duo1();
        TerrainSet t = w.Terrain!;
        SiteAssessment.Prepared site = Site(w);
        SettlementSiting.FrontierSiting f = site.Frontier;
        int[] occupied = Occupied(w);
        double[] spacing = SettlementSiting.SeedSpacing(f, occupied);
        double radiusPx = WorldFounding.HinterlandRadiusPx(DevWg, Cfg);
        SettlementId source = new(SiteAssessment.DefaultSource(w, Player));
        Assert.True(source.Value >= 0);
        int sourceCell = FoundingRigs.SiteCellOf(w, source);
        long party = FoundingQuery.AllocateParty(w.Buckets, source, FoundingQuery.PartyFraction(Cfg), null);
        long provisions = FoundingQuery.ProvisionsShare(w.GoodStocks, w.Buckets, source, new GoodId(Cfg.Goods!.GrainId), party);
        int claimedSeen = 0, occupiedSeen = 0, routes = 0, land = 0, water = 0;
        foreach (int cell in Sample(w, 4, 503))
        {
            SiteAssessment.Report r = Assess(w, site, cell);
            Assert.Equal((cell % t.Size, cell / t.Size), (r.X, r.Y));
            Assert.Equal(t.Elevation[cell], r.Elevation);
            Assert.Equal(t.SeaLevel, r.SeaLevel);
            Assert.Equal(t.Water[cell], r.Water);
            Assert.Equal(t.Temperature[cell], r.TemperatureC);
            Assert.Equal(t.Moisture[cell], r.Moisture);
            Assert.Equal(t.Fertility[cell], r.Fertility);
            Assert.Equal(t.MovementCost[cell], r.MovementCost);
            Assert.Equal(t.Rivers[cell] >= 0.5, r.River);

            int node = LatticeMap.OriginLatticeNode(f.Lattice, t.Size, cell);
            Assert.Equal(node, r.SiteNode);
            Assert.Equal(spacing[node], r.SpacingCost);
            Assert.Equal(f.MinSpacingCostUnits, r.MinSpacingCost);
            int occ = -1;
            for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].SiteCell == cell) occ = w.Settlements[i].Id.Value;
            Assert.Equal(occ, r.OccupiedBy);
            if (occ >= 0) occupiedSeen++;
            int claim = -1;
            for (int i = 0; i < w.CatchmentNodes.Count; i++) if (w.CatchmentNodes[i].LatticeNode == node) { claim = w.CatchmentNodes[i].Settlement.Value; break; }
            Assert.Equal(claim, r.ClaimedBy);
            Assert.Equal(claim >= 0, FoundingQuery.IsClaimed(w, node));
            if (claim >= 0) claimedSeen++;

            Assert.Equal(radiusPx, r.HinterlandRadiusPx);
            if (t.Water[cell] >= 0.5)
            {
                // No settlement can stand on water: neither its deposits nor its catchment are evaluated.
                Assert.Empty(r.Deposits);
                Assert.Equal("no settlement can stand on water", r.DepositsUnavailable);
                Assert.Null(r.WouldBeArableKm2);
                Assert.Equal("no settlement can stand on water", r.CatchmentUnavailable);
                water++;
            }
            else
            {
                (double m, double e) = WorldFounding.HinterlandMeans(t, DevWg, cell, radiusPx);
                Assert.Equal((m, e), (r.MeanMoisture, r.MeanElevation));
                int k = 0;
                foreach (GoodEntry g in Cfg.Goods!.Goods)
                {
                    if (g.DepositChannel is null) continue;
                    SiteAssessment.DepositPreview d = r.Deposits[k++];
                    Assert.Equal(g.Id, d.Good.Value);
                    Assert.Equal(WorldFounding.DepositChannelValue(g.DepositChannel, m, e), d.ChannelValue);
                    Assert.Equal(g.DepositSpread, d.Spread);
                    Assert.Equal(LabourActivities.IsHerded(Cfg.Goods, g.Id), d.Herded);
                    Assert.True(d.Low <= d.ChannelValue && d.ChannelValue <= d.High);
                }
                Assert.Equal(k, r.Deposits.Length);
                Assert.Null(r.DepositsUnavailable);
                Assert.NotNull(r.WouldBeArableKm2);
                land++;
            }

            Assert.Equal(source.Value, r.Source);
            Assert.Equal(party, r.Party);
            Assert.Equal(provisions, r.Provisions);
            Pathfinder.PathResult path = Pathfinder.FindPath(f.Lattice, w, LatticeMap.OriginLatticeNode(f.Lattice, t.Size, sourceCell), node);
            Assert.Equal(path.Found, r.Route!.Found);
            if (path.Found)
            {
                Assert.Equal(path.Nodes, r.Route.Nodes);
                Assert.Equal(path.TotalCost, r.Route.TotalCost);
                Assert.Equal(LatticeGeometry.IdealGroundKmForCostUnits(f.Lattice, path.TotalCost), r.RouteKm);
                routes++;
            }
            Assert.Equal(FoundingQuery.Check(w, Cfg, f, Player, source, cell), r.Verdict);

            // EACH ARM, against its own public predicate (independent of which one fails first): the parity test below
            // sees only the first failure, and an arm that never fails first (Claimed: a catchment is narrower than the
            // spacing exclusion) would otherwise go unchecked.
            bool isWater = t.Water[cell] >= 0.5, isOccupied = SettlementSiting.IsOccupied(occupied, cell);
            bool?[] want =
            [
                true,                                                        // UnknownSource: the source stands
                EmpireQuery.ControlsSettlement(w, Player, source),           // NotControlled
                true,                                                        // OffMap
                !isWater,                                                    // Water
                isWater ? null : SettlementSiting.IsFrontierCandidate(f, t, cell),   // PoorSite
                !isOccupied,                                                 // Occupied
                isOccupied ? null : !(spacing[node] < f.MinSpacingCostUnits),        // TooClose
                !FoundingQuery.IsClaimed(w, node),                           // Claimed
                party >= 1,                                                  // NoParty
                party >= 1 ? provisions >= 1 : null,                         // NoProvisions
                path.Found,                                                  // Unreachable
            ];
            FoundingVerdict[] order = [FoundingVerdict.UnknownSource, FoundingVerdict.NotControlled, FoundingVerdict.OffMap, FoundingVerdict.Water,
                FoundingVerdict.PoorSite, FoundingVerdict.Occupied, FoundingVerdict.TooClose, FoundingVerdict.Claimed, FoundingVerdict.NoParty,
                FoundingVerdict.NoProvisions, FoundingVerdict.Unreachable];
            Assert.Equal(order.Length, r.Arms.Length);
            for (int a = 0; a < order.Length; a++)
            {
                Assert.Equal(order[a], r.Arms[a].Arm);
                Assert.Equal(want[a], r.Arms[a].Holds);
            }
        }
        Assert.True(claimedSeen > 0 && occupiedSeen > 0 && routes > 0 && land > 50 && water > 50,
            "the sample covers claimed, occupied, routed, land and water cells");
    }

    [Fact]
    public void EachArm_IsJudgedSeparately_AndItsFirstFailure_IsFoundingQueryChecksVerdict_OverADenseSweep()
    {
        // The per-arm breakdown is built from the predicate's own public parts; its first failing arm in the predicate's
        // order must be exactly what FoundingQuery.Check answers — for every verdict the sweep reaches, from the player's
        // sources, a rival's source (NotControlled), the default source and an id that names nothing (UnknownSource).
        WorldState w = Duo1();
        SiteAssessment.Prepared site = Site(w);
        var sources = new List<int> { -1, 9999 };
        for (int i = 0; i < w.Settlements.Count; i++) sources.Add(w.Settlements[i].Id.Value);
        var seen = new HashSet<FoundingVerdict>();
        foreach (int src in sources)
            foreach (int cell in Sample(w, 5, 1499))
            {
                SiteAssessment.Report r = Assess(w, site, cell, src);
                FoundingVerdict check = FoundingQuery.Check(w, Cfg, site.Frontier, Player,
                    new SettlementId(src >= 0 ? src : SiteAssessment.DefaultSource(w, Player)), cell);
                Assert.Equal(check, r.Verdict);
                Assert.Equal(check, SiteAssessment.FirstFailing(r.Arms));
                seen.Add(check);
            }
        foreach (FoundingVerdict v in new[] { FoundingVerdict.Legal, FoundingVerdict.UnknownSource, FoundingVerdict.NotControlled,
                     FoundingVerdict.Water, FoundingVerdict.PoorSite, FoundingVerdict.Occupied, FoundingVerdict.TooClose })
            Assert.Contains(v, seen);
    }

    // ================================================================== AGAINST THIS TURN'S QUEUED FOUNDINGS (M5 S1d review)

    /// <summary>The batch one queued founding makes: the world's settlements, then <paramref name="ordered"/> (AiFoundingPolicy.Sites
    /// grown by it — the intake's own growth, the arrays FoundingQuery.Check's explicit overload takes).</summary>
    private static SiteAssessment.Batch BatchWith(IReadOnlyWorldState w, SettlementSiting.FrontierSiting f, int ordered)
    {
        var sites = new Sim.Core.Systems.Colonization.AiFoundingPolicy.Sites(w, f);
        int standing = sites.Occupied.Length;
        double[] standingSpacing = sites.Spacing.ToArray();
        sites.Accept(ordered);
        return new SiteAssessment.Batch(f, sites.Occupied, standing, sites.Spacing, standingSpacing);
    }

    [Fact]
    public void WithABatch_EachArmAndTheVerdict_AreTheExplicitOverloadsOnTheBatch_AndARefusalOnlyAnOrderedSiteCauses_IsToldAsSuch()
    {
        // S1d review finding: the placement mode judges a place against this turn's batch, and the P8 card it docked judged
        // the bare world — so with a founding queued, the card said "would be accepted" and ticked every arm where the mode
        // refused. With the batch the card's verdict is FoundingQuery.Check's explicit overload on the batch's sites, its first
        // failing arm reproduces it, and a refusal is flagged "by order" exactly when the bare world does not refuse it on
        // the same arm (an independent oracle: Check on the bare world).
        WorldState w = Duo1();
        SiteAssessment.Prepared site = Site(w);
        SettlementSiting.FrontierSiting f = site.Frontier;
        var src = w.Settlements[0].Id;
        int ordered = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, src);
        SiteAssessment.Batch batch = BatchWith(w, f, ordered);
        Assert.Equal(1, batch.OrderedCount);
        int size = w.Terrain!.Size;
        var cells = Sample(w, 3, 1499);
        cells.Add(ordered);
        for (int r = 1; r <= 14; r++) cells.AddRange(FoundingRigs.Ring(size, ordered, r));
        var seen = new HashSet<(FoundingVerdict, bool)>();
        foreach (int cell in cells)
        {
            SiteAssessment.Report r = SiteAssessment.Assess(w, Cfg, site, PolityKnowledge.Of(w, Player), Player, cell, src.Value, batch: batch);
            FoundingVerdict check = FoundingQuery.Check(w, Cfg, f, batch.Spacing, batch.Occupied, Player, src, cell);
            FoundingVerdict bare = FoundingQuery.Check(w, Cfg, f, Player, src, cell);
            Assert.Equal(check, r.Verdict);
            Assert.Equal(check, SiteAssessment.FirstFailing(r.Arms));
            bool byOrder = check is FoundingVerdict.Occupied or FoundingVerdict.TooClose && bare != check;
            Assert.Equal(byOrder, r.RefusedByOrder);
            Assert.Equal(byOrder, batch.RefusesByOrder(check, cell));
            Assert.Equal(1, r.OrderedSites);
            seen.Add((check, byOrder));
        }
        // The sweep reaches each case: the ordered site itself, its exclusion, a standing settlement's site and exclusion.
        Assert.Contains((FoundingVerdict.Occupied, true), seen);
        Assert.Contains((FoundingVerdict.TooClose, true), seen);
        Assert.Contains((FoundingVerdict.Occupied, false), seen);
        Assert.Contains((FoundingVerdict.TooClose, false), seen);
        Assert.Contains((FoundingVerdict.Legal, false), seen);

        // CONTROL: without a batch the same report is the bare world's (P8 unchanged).
        foreach (int cell in cells)
            Assert.Equal(FoundingQuery.Check(w, Cfg, f, Player, src, cell), Assess(w, site, cell, src.Value).Verdict);
    }

    [Fact]
    public void ThePlaceCard_InsideAnOrderedColonysExclusion_SaysSoInItsOwnWords_AndTheBareCardIsTheDefectsControl()
    {
        WorldState w = Duo1();
        SiteAssessment.Prepared site = Site(w);
        SettlementSiting.FrontierSiting f = site.Frontier;
        var src = w.Settlements[0].Id;
        int ordered = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, src);
        SiteAssessment.Batch batch = BatchWith(w, f, ordered);
        int size = w.Terrain!.Size;
        // A place the bare world accepts that only the ordered colony's spacing refuses.
        int cell = -1;
        for (int r = 1; r <= 14 && cell < 0; r++)
            foreach (int c in FoundingRigs.Ring(size, ordered, r))
                if (FoundingQuery.Check(w, Cfg, f, Player, src, c) == FoundingVerdict.Legal
                    && FoundingQuery.Check(w, Cfg, f, batch.Spacing, batch.Occupied, Player, src, c) == FoundingVerdict.TooClose) { cell = c; break; }
        Assert.True(cell >= 0, "no place inside the ordered colony's exclusion only - the rig is void");
        PolityKnowledge k = PolityKnowledge.Of(w, Player);
        InfoCard Card(SiteAssessment.Batch? b, int at) =>
            InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(at, src.Value), settlementName: Named, knowledge: k, site: site, batch: b);

        InfoCard card = Card(batch, cell);
        Assert.Equal("too close to a settlement you ordered this turn.", card.StatusLine);
        Assert.Equal(InfoQuery.FoundingLine(FoundingVerdict.TooClose, Named(src.Value), byOrder: true) + ".", card.StatusLine);
        InfoSection arms = card.More.Single(s => s.Heading.StartsWith("Founding here", StringComparison.Ordinal));
        Assert.Equal("FoundingQuery: too close to a settlement you ordered this turn", arms.Lines[0].Label);
        Assert.False(arms.Lines[0].Met);
        InfoLink spacing = arms.Lines.Single(l => l.Label.StartsWith("Outside every settlement's spacing", StringComparison.Ordinal));
        Assert.False(spacing.Met);
        Assert.StartsWith("A settlement you ordered this turn will stand ", spacing.Note, StringComparison.Ordinal);
        Assert.True(arms.Lines.Single(l => l.Label.StartsWith("No settlement stands", StringComparison.Ordinal)).Met);
        Assert.DoesNotContain("standing settlement", card.StatusLine, StringComparison.Ordinal);

        // The ordered site itself: occupied by the order, no settlement named.
        InfoCard at = Card(batch, ordered);
        Assert.Equal("a settlement you ordered this turn will stand here.", at.StatusLine);
        InfoSection atArms = at.More.Single(s => s.Heading.StartsWith("Founding here", StringComparison.Ordinal));
        InfoLink occupied = atArms.Lines.Single(l => l.Label.StartsWith("No settlement stands", StringComparison.Ordinal));
        Assert.False(occupied.Met);
        Assert.StartsWith("A settlement you ordered this turn will stand here", occupied.Note, StringComparison.Ordinal);

        // A standing settlement's exclusion keeps its own words under the same batch.
        int standing = -1;
        for (int r = 1; r <= 14 && standing < 0; r++)
            foreach (int c in FoundingRigs.Ring(size, w.Settlements[1].SiteCell, r))
                if (FoundingQuery.Check(w, Cfg, f, Player, src, c) == FoundingVerdict.TooClose) { standing = c; break; }
        Assert.True(standing >= 0);
        Assert.Equal("too close to a standing settlement.", Card(batch, standing).StatusLine);

        // CONTROL (the defect): the bare card for the same place says the order would be accepted and ticks every arm.
        InfoCard bare = Card(null, cell);
        Assert.Equal(InfoQuery.FoundingLine(FoundingVerdict.Legal, Named(src.Value)) + ".", bare.StatusLine);
        Assert.True(bare.More.Single(s => s.Heading.StartsWith("Founding here", StringComparison.Ordinal))
            .Lines.Single(l => l.Label.StartsWith("Outside every settlement's spacing", StringComparison.Ordinal)).Met);
    }

    [Fact]
    public void UnderAPartialView_ThePlayersOwnOrderedSite_IsOccupiedByOrder_SpacingStaysNotJudged_TheVerdictStaysNotJudged()
    {
        // The player's class (ADR-037 §7): with one far node out of sight the player does not see the whole world. The batch's
        // ORDERED site is the player's own order, so the occupancy arm says so there; spacing reads the lattice, so it stays
        // not judged; and the verdict stays not judged (the floor arm comes first).
        WorldState w = W1();
        SiteAssessment.Prepared site = Site(w);
        SettlementSiting.FrontierSiting f = site.Frontier;
        var src = w.Settlements[0].Id;
        int ordered = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, src);
        SiteAssessment.Batch batch = BatchWith(w, f, ordered);
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        PolityKnowledge any = PolityKnowledge.Of(w, Player);
        int node = any.NodeOfCell(ordered);
        int far = (node % l < l / 2 ? l - 1 : 0) + (node / l < l / 2 ? l - 1 : 0) * l;
        var known = new bool[l * l];
        var visible = new bool[l * l];
        for (int v = 0; v < known.Length; v++) { known[v] = true; visible[v] = v != far; }
        PolityKnowledge stub = PolityKnowledge.Restricted(w, Player, known, visible);
        Assert.False(SiteAssessment.SeesAll(stub));
        Assert.True(stub.SeesCell(ordered));

        SiteAssessment.Report withBatch = SiteAssessment.Assess(w, Cfg, site, stub, Player, ordered, src.Value, batch: batch);
        SiteAssessment.Report without = SiteAssessment.Assess(w, Cfg, site, stub, Player, ordered, src.Value);
        Assert.True(withBatch.OccupiedByOrder);
        Assert.Equal(false, ArmOf(withBatch, FoundingVerdict.Occupied).Holds);
        Assert.Equal(new SiteAssessment.FoundingArm(FoundingVerdict.TooClose, null, false), ArmOf(withBatch, FoundingVerdict.TooClose));
        Assert.Null(withBatch.Verdict);
        Assert.False(withBatch.TooCloseByOrder);
        Assert.True(double.IsNaN(withBatch.BatchSpacingCost));
        // CONTROL: without the batch the place is in sight and nothing stands there.
        Assert.Equal(true, ArmOf(without, FoundingVerdict.Occupied).Holds);
        Assert.False(without.OccupiedByOrder);
    }

    // ================================================================== AGAINST AN ACTUAL FOUNDING

    [Fact]
    public void TheIfFoundedNextDeposits_AreTheDepositRowsOfAColonyActuallyFoundedThere_BitExactly()
    {
        WorldState w = W1();
        SettlementId src = w.Settlements[0].Id;
        SiteAssessment.Prepared site = Site(w);
        int cell = FoundingRigs.NearestLegalSite(w, Cfg, site.Frontier, Player, src);
        Assert.True(cell >= 0, "no legal site - the rig is void");
        SiteAssessment.Report r = Assess(w, site, cell, src.Value);
        Assert.Equal(FoundingVerdict.Legal, r.Verdict);
        Assert.Null(r.RollWithheld);
        Assert.Equal(FoundingRigs.MaxSettlementId(w) + 1, r.NextSettlementId);

        // Founded through the real production pipeline (its ColonizationSystem holds the same worldgen config).
        WorldState s1 = FoundingRigs.Pipeline(Cfg, Log(FoundingQuery.FoundOrder(w, Player, src, cell)), DevWg).Step(w);
        Assert.True(FoundingRigs.TryGetSettlement(s1, r.NextSettlementId, out SettlementRow colony), "the order did not found");
        Assert.Equal(cell, colony.SiteCell);
        var written = new List<DepositRow>();
        for (int i = 0; i < s1.Deposits.Count; i++) if (s1.Deposits[i].Settlement.Value == r.NextSettlementId) written.Add(s1.Deposits[i]);
        Assert.Equal(r.Deposits.Length, written.Count);
        for (int i = 0; i < written.Count; i++)
        {
            SiteAssessment.DepositPreview p = r.Deposits[i];
            Assert.Equal(written[i].Good, p.Good);
            Assert.Equal(BitConverter.DoubleToInt64Bits(written[i].Abundance), BitConverter.DoubleToInt64Bits(p.IfFoundedNext!.Value));
            // The roll lies in the previewed range: [low, high) — exactly the value when the good has no spread.
            Assert.True(p.Low <= written[i].Abundance && (p.Spread == 0.0 ? written[i].Abundance == p.High : written[i].Abundance < p.High),
                "good " + p.Good.Value + ": " + written[i].Abundance.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " outside its range");
        }
        Assert.Contains(r.Deposits, d => d.Spread > 0.0 && d.IfFoundedNext != d.ChannelValue);   // the spread is really rolled
    }

    [Fact]
    public void TheWouldBeArableLand_IsWhatTheCatchmentRecomputeGivesTheColony_BitExactly()
    {
        // Colonization alone founds (nothing else moves: tiers, network and every other site are PREV's), then the
        // catchment system alone recomputes — so its partition is exactly the one the preview computed from PREV.
        WorldState w = W1();
        SettlementId src = w.Settlements[0].Id;
        SiteAssessment.Prepared site = Site(w);
        int cell = FoundingRigs.NearestLegalSite(w, Cfg, site.Frontier, Player, src, skip: 3);
        Assert.True(cell >= 0);
        SiteAssessment.Report r = Assess(w, site, cell, src.Value);
        Assert.NotNull(r.WouldBeArableKm2);
        Assert.True(r.WouldBeNodes > 0);

        WorldState s1 = FoundingRigs.ColonizationOnly(Cfg, Log(FoundingQuery.FoundOrder(w, Player, src, cell)), DevWg).Step(w);
        Assert.True(FoundingRigs.TryGetSettlement(s1, r.NextSettlementId, out _));
        WorldState s2 = new TurnExecutor(GovernanceRigs.FlatEra(), [SystemCatalog.Catchment(Cfg)], null).Step(s1);
        CatchmentSummaryRow? summary = null;
        for (int i = 0; i < s2.CatchmentSummaries.Count; i++) if (s2.CatchmentSummaries[i].Settlement.Value == r.NextSettlementId) summary = s2.CatchmentSummaries[i];
        Assert.NotNull(summary);
        Assert.Equal(r.WouldBeNodes, summary!.Value.NodeCount);
        Assert.Equal(BitConverter.DoubleToInt64Bits(r.WouldBeArableKm2!.Value), BitConverter.DoubleToInt64Bits(summary.Value.EffectiveArableKm2));
    }

    [Fact]
    public void AWorldgenConfigForAnotherMapSize_LeavesTheDepositsUnevaluated_NeverReadsTheWrongCells()
    {
        // WorldFounding.HinterlandMeans indexes by the CONFIG's sizePx; evaluated with the canonical 1024-px config on a
        // 256-px map it would read other cells (or past the raster). The assessment says so instead of showing them.
        WorldState w = W1();
        SiteAssessment.Report r = Assess(w, Site(w, TestConfigs.Worldgen()), w.Settlements[0].SiteCell);
        Assert.True(r.Known);
        Assert.Empty(r.Deposits);
        Assert.Contains("1024-px", r.DepositsUnavailable);
        Assert.Equal(Assess(w, Site(w), w.Settlements[0].SiteCell).Score, r.Score);   // the score half does not depend on it
    }

    // ================================================================== READ-ONLY

    [Fact]
    public void PreparingAssessingAndCarding_ChangeNoHash()
    {
        foreach (WorldState w in new[] { W1(), Duo1() })
        {
            string before = WorldHash.ComputeHex(w);
            long turn = w.Clock.Turn;
            int flows = w.LedgerFlows.Count;
            SiteAssessment.Prepared site = Site(w);
            foreach (int cell in Sample(w, 2, 4099))
            {
                Assess(w, site, cell);
                InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(cell), site: site);
            }
            Assert.Equal(before, WorldHash.ComputeHex(w));
            Assert.Equal(turn, w.Clock.Turn);
            Assert.Equal(flows, w.LedgerFlows.Count);
        }
    }

    // ================================================================== THE REFUSAL (P7 stub)

    /// <summary>Known everywhere except a 5 × 5-node block around <paramref name="cell"/>'s node.</summary>
    private static bool[] HideAround(IReadOnlyWorldState w, int cell)
    {
        PolityKnowledge any = PolityKnowledge.Of(w, Player);
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        int c = any.NodeOfCell(cell), cx = c % l, cy = c / l;
        var known = new bool[l * l];
        for (int n = 0; n < known.Length; n++) known[n] = Math.Max(Math.Abs(n % l - cx), Math.Abs(n / l - cy)) > 2;
        return known;
    }

    /// <summary>A copy of <paramref name="t"/> with every land layer changed in every cell <paramref name="k"/> does not know.</summary>
    private static TerrainSet PerturbedUnderUnknown(TerrainSet t, PolityKnowledge k, double salt)
    {
        double[] elevation = t.Elevation.ToArray(), temperature = t.Temperature.ToArray(), moisture = t.Moisture.ToArray(), fertility = t.Fertility.ToArray();
        int changed = 0;
        for (int cell = 0; cell < fertility.Length; cell++)
        {
            if (k.KnowsCell(cell)) continue;
            fertility[cell] = Math.Min(1.0, fertility[cell] * 0.5 + salt);
            moisture[cell] = Math.Min(1.0, moisture[cell] * 0.5 + salt);
            temperature[cell] += 10.0 * salt;
            elevation[cell] += 0.01 * salt;
            changed++;
        }
        Assert.True(changed > 100);
        var polylines = new int[t.RiverPolylineCount][];
        for (int i = 0; i < polylines.Length; i++) polylines[i] = t.RiverPolyline(i).ToArray();
        System.Reflection.ConstructorInfo ctor = typeof(TerrainSet)
            .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        return (TerrainSet)ctor.Invoke([t.Size, t.KmPerPx, t.SeaLevel, elevation, t.Water.ToArray(), temperature, moisture, fertility,
            t.MovementCost.ToArray(), t.Rivers.ToArray(), polylines]);
    }

    [Fact]
    public void AnUnknownCell_IsRefused_NothingOfItIsRead_WhateverLiesThere_AndTheAllKnownControlSeesIt()
    {
        WorldState w0 = W1();
        int target = -1;   // a land cell with a positive score, far from every settlement
        TerrainSet t0 = w0.Terrain!;
        double[] scores = SettlementSiting.CandidateScores(t0, DevWg.Siting);
        for (int cell = 0; cell < scores.Length && target < 0; cell += 7)
        {
            if (!(scores[cell] > 0.0)) continue;
            bool far = true;
            for (int i = 0; i < w0.Settlements.Count; i++)
            {
                int s = w0.Settlements[i].SiteCell;
                far &= Math.Max(Math.Abs(s % t0.Size - cell % t0.Size), Math.Abs(s / t0.Size - cell / t0.Size)) > 40;
            }
            if (far) target = cell;
        }
        Assert.True(target >= 0, "no remote scored land cell - the rig is void");
        bool[] known = HideAround(w0, target);

        SiteAssessment.Report? firstRefused = null;
        InfoCard? firstCard = null;
        var allKnownScores = new List<double>();
        var allKnownCards = new List<InfoCard>();
        foreach (double salt in new[] { 0.0, 0.3, 0.6 })
        {
            WorldState w = W1();
            if (salt > 0.0) w.Terrain = PerturbedUnderUnknown(w.Terrain!, PolityKnowledge.Restricted(w, Player, known, known), salt);
            PolityKnowledge stub = PolityKnowledge.Restricted(w, Player, known, known);
            Assert.False(stub.KnowsCell(target));
            SiteAssessment.Prepared site = Site(w);
            SiteAssessment.Report r = Assess(w, site, target, k: stub);
            Assert.False(r.Known);
            Assert.Equal((target, target % w.Terrain!.Size, target / w.Terrain.Size), (r.Cell, r.X, r.Y));
            Assert.Equal(0.0, r.Score);
            Assert.Empty(r.Deposits);
            Assert.Empty(r.Arms);
            Assert.Null(r.Route);
            InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(target), knowledge: stub, site: site);
            Assert.Equal("Undiscovered ground", card.Title);
            Assert.Equal(InfoStatus.Unknown, card.Status);
            Assert.True(card.More.IsDefaultOrEmpty);
            if (firstRefused is null) { firstRefused = r; firstCard = card; }
            else
            {
                Assert.Equal(firstCard, card);   // nothing that lies there reaches the card
                Assert.Equal((firstRefused.Known, firstRefused.Score, firstRefused.Elevation, firstRefused.Fertility, firstRefused.Source),
                    (r.Known, r.Score, r.Elevation, r.Fertility, r.Source));
            }

            // POSITIVE CONTROL: all-known, the same cell is read — and the perturbation moves what it reads.
            SiteAssessment.Report all = Assess(w, site, target);
            Assert.True(all.Known);
            allKnownScores.Add(all.Score);
            allKnownCards.Add(InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(target), site: site));
        }
        Assert.Equal(3, allKnownScores.Distinct().Count());
        Assert.NotEqual(allKnownCards[0], allKnownCards[1]);
        Assert.StartsWith("Land at ", allKnownCards[0].Title);
    }

    [Fact]
    public void UnderAStub_TheNextFoundingRollIsWithheld_AndTheNearestListNamesOnlyKnownSettlements()
    {
        // The next settlement id counts every settlement in the world, so the exact roll is shown only while the viewer
        // knows them all; the nearest list never names a settlement the viewer does not know (KnowledgeStubs' rule:
        // the rival's site and catchment nodes hidden).
        WorldState w = Duo1();
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        PolityKnowledge all = PolityKnowledge.Of(w, Player);
        var known = new bool[l * l];
        Array.Fill(known, true);
        var rival = new List<int>();
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            if (EmpireQuery.ControlsSettlement(w, Player, w.Settlements[i].Id)) continue;
            rival.Add(w.Settlements[i].Id.Value);
            known[all.NodeOfCell(w.Settlements[i].SiteCell)] = false;
        }
        Assert.NotEmpty(rival);
        PolityKnowledge stub = PolityKnowledge.Restricted(w, Player, known, known);
        SiteAssessment.Prepared site = Site(w);
        int cell = FoundingRigs.SiteCellOf(w, new SettlementId(SiteAssessment.DefaultSource(w, Player)));   // the player's own
        SiteAssessment.Report r = SiteAssessment.Assess(w, Cfg, site, stub, Player, cell, -1, nearest: 99);
        Assert.True(r.Known);
        Assert.NotNull(r.RollWithheld);
        Assert.All(r.Deposits, d => Assert.Null(d.IfFoundedNext));
        Assert.All(r.Nearest, n => Assert.DoesNotContain(n.Id.Value, rival));
        Assert.Equal(w.Settlements.Count - rival.Count, r.Nearest.Length);

        SiteAssessment.Report control = SiteAssessment.Assess(w, Cfg, site, all, Player, cell, -1, nearest: 99);
        Assert.Null(control.RollWithheld);
        Assert.All(control.Deposits, d => Assert.NotNull(d.IfFoundedNext));
        Assert.Equal(w.Settlements.Count, control.Nearest.Length);
    }

    // ================================================================== THE PLAYER'S CLASS for a KNOWN cell (P8 review; ADR-037 §7)

    /// <summary>Every word a card shows, for the "never narrated" checks.</summary>
    private static string Text(InfoCard card)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(card.Title).Append('|').Append(card.What).Append('|').Append(card.StatusLine).Append('\n');
        foreach (InfoSection sec in card.More)
        {
            sb.Append(sec.Heading).Append('\n');
            foreach (InfoLink l in sec.Lines) sb.Append(l.Label).Append('|').Append(l.Met).Append('|').Append(l.Note).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>A settlement's name on these cards: one no other text can contain by accident.</summary>
    private static string Named(int id) => "<S" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ">";

    private static InfoCard CardOf(IReadOnlyWorldState w, SiteAssessment.Prepared site, PolityKnowledge k, int cell, int source = -1) =>
        InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(cell, source), settlementName: Named, knowledge: k, site: site);

    private static SiteAssessment.FoundingArm ArmOf(SiteAssessment.Report r, FoundingVerdict arm)
    {
        foreach (SiteAssessment.FoundingArm a in r.Arms) if (a.Arm == arm) return a;
        throw new InvalidOperationException("no arm " + arm);
    }

    /// <summary>The rival's settlements, in table order.</summary>
    private static List<SettlementRow> RivalSettlements(IReadOnlyWorldState w)
    {
        var rows = new List<SettlementRow>();
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.TryGetController(w, w.Settlements[i].Id, out PolityId p) && p.Value == Rival.Value) rows.Add(w.Settlements[i]);
        return rows;
    }

    /// <summary>KnowledgeStubs.HidePolity's rule (Sim.Ui) on the duo world: the rival's site and catchment nodes are
    /// unknown, every other node known; a node holding a player site is never hidden.</summary>
    private static bool[] RivalHiddenMask(IReadOnlyWorldState w)
    {
        PolityKnowledge any = PolityKnowledge.Of(w, Player);
        (int l, _) = PolityKnowledge.Grid(w.Terrain!.Size);
        var known = new bool[l * l];
        Array.Fill(known, true);
        var rivals = new HashSet<int>();
        foreach (SettlementRow row in RivalSettlements(w)) { rivals.Add(row.Id.Value); known[any.NodeOfCell(row.SiteCell)] = false; }
        for (int i = 0; i < w.CatchmentNodes.Count; i++)
            if (rivals.Contains(w.CatchmentNodes[i].Settlement.Value)) known[w.CatchmentNodes[i].LatticeNode] = false;
        for (int i = 0; i < w.Settlements.Count; i++)
            if (!rivals.Contains(w.Settlements[i].Id.Value)) known[any.NodeOfCell(w.Settlements[i].SiteCell)] = true;
        return known;
    }

    /// <summary>The centre cell of lattice node <paramref name="node"/> on a <paramref name="size"/>-px map.</summary>
    private static int CentreOf(int size, int node)
    {
        (int l, int block) = PolityKnowledge.Grid(size);
        return (node / l * block + block / 2) * size + node % l * block + block / 2;
    }

    /// <summary>A copy of <paramref name="t"/> with, in every cell <paramref name="k"/> does not know, fertility set to
    /// <paramref name="fertility"/>, a river, rough ground (movement cost × 3) and every other land layer changed.</summary>
    private static TerrainSet RewrittenUnderUnknown(TerrainSet t, PolityKnowledge k, double fertility)
    {
        double[] elevation = t.Elevation.ToArray(), temperature = t.Temperature.ToArray(), moisture = t.Moisture.ToArray();
        double[] fert = t.Fertility.ToArray(), rivers = t.Rivers.ToArray(), movement = t.MovementCost.ToArray();
        int changed = 0;
        for (int cell = 0; cell < fert.Length; cell++)
        {
            if (k.KnowsCell(cell)) continue;
            fert[cell] = fertility;
            moisture[cell] = 1.0 - moisture[cell];
            temperature[cell] += 7.0;
            elevation[cell] += 0.03;
            if (rivers.Length == fert.Length) rivers[cell] = 1.0;
            movement[cell] *= 3.0;
            changed++;
        }
        Assert.True(changed > 0);
        var polylines = new int[t.RiverPolylineCount][];
        for (int i = 0; i < polylines.Length; i++) polylines[i] = t.RiverPolyline(i).ToArray();
        System.Reflection.ConstructorInfo ctor = typeof(TerrainSet)
            .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        return (TerrainSet)ctor.Invoke([t.Size, t.KmPerPx, t.SeaLevel, elevation, t.Water.ToArray(), temperature, moisture, fert,
            movement, rivers, polylines]);
    }

    [Fact]
    public void ThroughAStubHidingTheRival_AKnownCellsCard_IsTheSameWhereverTheRivalStands_AndNeverNamesIt_TheAllKnownControlSeesIt()
    {
        // P8 review (finding 1, first case): through a stub hiding the rival whole (its site and catchment nodes), known cells
        // failed the spacing arm only because of the rival and the card said how far away its site was. Spacing reads every
        // settlement and the lattice, so through such a stub it is NOT JUDGED, and WHERE the rival stands — every rival site
        // moved to the hidden node farthest from it — leaves every known cell's card identical and nameless. A source the
        // player does not know names nothing (its card is a nonexistent id's). THE CONTROL: all-known, the rival's spacing is
        // read, and the move moves it.
        WorldState w = Duo1(), moved = Duo1();
        int size = w.Terrain!.Size;
        bool[] mask = RivalHiddenMask(w);
        PolityKnowledge any = PolityKnowledge.Of(w, Player);
        List<SettlementRow> rivals = RivalSettlements(w);
        Assert.NotEmpty(rivals);
        (int l, _) = PolityKnowledge.Grid(size);
        for (int i = 0; i < moved.Settlements.Count; i++)
        {
            if (!rivals.Exists(r => r.Id == moved.Settlements[i].Id)) continue;
            int from = any.NodeOfCell(moved.Settlements[i].SiteCell), best = -1;
            long bestD = -1;
            for (int v = 0; v < mask.Length; v++)
            {
                if (mask[v]) continue;
                long dx = v % l - from % l, dy = v / l - from / l, d = dx * dx + dy * dy;
                if (d > bestD) { bestD = d; best = v; }
            }
            Assert.True(bestD > 0, "the rival hides only its own site node - the rig is void");
            moved.Settlements[i] = moved.Settlements[i] with { SiteCell = CentreOf(size, best) };
        }
        SiteAssessment.Prepared site = Site(w);   // clones share the terrain
        PolityKnowledge stub = PolityKnowledge.Restricted(w, Player, mask, mask), stubMoved = PolityKnowledge.Restricted(moved, Player, mask, mask);
        Assert.False(SiteAssessment.SeesAll(stub));
        foreach (SettlementRow r in rivals) Assert.False(stub.KnowsSettlement(r.Id));

        var cells = new List<int>();
        foreach (SettlementRow r in rivals)
            for (int radius = 4; radius <= 48; radius += 4)
                foreach (int c in FoundingRigs.Ring(size, r.SiteCell, radius))
                    if (c % 3 == 0 && stub.KnowsCell(c) && w.Terrain.Water[c] < 0.5) cells.Add(c);
        Assert.True(cells.Count > 20, "too few known land cells near the rival - the rig is void");

        int spacingMoved = 0, cardsMoved = 0;
        foreach (int cell in cells)
        {
            SiteAssessment.Report r = Assess(w, site, cell, k: stub);
            Assert.True(r.Known);
            Assert.False(r.SeesAll);
            Assert.True(double.IsNaN(r.SpacingCost));
            SiteAssessment.FoundingArm tooClose = ArmOf(r, FoundingVerdict.TooClose);
            Assert.Null(tooClose.Holds);
            Assert.True(tooClose.Unseen || ArmOf(r, FoundingVerdict.Occupied).Holds == false);
            InfoCard a = CardOf(w, site, stub, cell), b = CardOf(moved, site, stubMoved, cell);
            Assert.True(a == b, "the card of known cell " + cell + " moved with the hidden rival:\n" + Text(a) + "\n---\n" + Text(b));
            string text = Text(a);
            foreach (SettlementRow rv in rivals) Assert.DoesNotContain(Named(rv.Id.Value), text, StringComparison.Ordinal);
            Assert.DoesNotContain("travel-cost units away", text, StringComparison.Ordinal);

            // CONTROL: all-known, the rival's spacing is read and the move changes it.
            SiteAssessment.Report ca = Assess(w, site, cell), cb = Assess(moved, site, cell);
            if (!ca.SpacingCost.Equals(cb.SpacingCost)) spacingMoved++;
            if (CardOf(w, site, any, cell) != CardOf(moved, site, PolityKnowledge.Of(moved, Player), cell)) cardsMoved++;
        }
        Assert.True(spacingMoved > 0, "moving the rival moves no all-known spacing - the comparison proves nothing");
        Assert.True(cardsMoved > 0, "moving the rival moves no all-known card - the comparison proves nothing");

        // A source the player does not know names nothing: judged from the rival's own settlement, the card is a
        // nonexistent id's, and does not name it.
        InfoCard fromRival = CardOf(w, site, stub, cells[0], rivals[0].Id.Value), fromNothing = CardOf(w, site, stub, cells[0], 9999);
        Assert.Equal(Text(fromNothing), Text(fromRival));
        Assert.Equal(-1, Assess(w, site, cells[0], rivals[0].Id.Value, stub).Source);
        Assert.DoesNotContain(Named(rivals[0].Id.Value), Text(fromRival), StringComparison.Ordinal);
        Assert.Contains(Named(rivals[0].Id.Value), Text(CardOf(w, site, any, cells[0], rivals[0].Id.Value)), StringComparison.Ordinal);   // control
    }

    [Fact]
    public void ASettlementOnKnownGroundOutOfSight_IsNeverNarrated_ItsSiteCardIsTheSameWhereverItStands_TheAllKnownControlNamesIt()
    {
        // P8 review (finding 1, second case): the rival's site node KNOWN but not in sight, so the player does not know the
        // settlement. Its site cell's card said "a settlement already stands here", "The site of a settlement." and "In the
        // catchment of a settlement you do not know". Now occupancy and claim are not judged there unless a settlement the
        // player knows decides them, the predicate's answer is not judged, nothing narrates the settlement, and moving it to
        // the next node out of sight leaves the card identical. CONTROL: all-known, the card names it standing there, and
        // the move changes the card.
        WorldState w = Duo1(), moved = Duo1();
        int size = w.Terrain!.Size;
        (int l, _) = PolityKnowledge.Grid(size);
        PolityKnowledge any = PolityKnowledge.Of(w, Player);
        SettlementRow rival = RivalSettlements(w)[0];
        int cell = rival.SiteCell, n0 = any.NodeOfCell(cell);
        var known = new bool[l * l];
        var visible = new bool[l * l];
        Array.Fill(known, true);
        Array.Fill(visible, true);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = n0 % l + dx, ny = n0 / l + dy;
                if (nx >= 0 && nx < l && ny >= 0 && ny < l) visible[ny * l + nx] = false;
            }
        int to = -1;   // the next node out of sight that holds no settlement
        foreach (int v in new[] { n0 + 1, n0 - 1, n0 + l, n0 - l })
        {
            if (v < 0 || v >= l * l || visible[v]) continue;
            bool empty = true;
            for (int i = 0; i < w.Settlements.Count; i++) empty &= any.NodeOfCell(w.Settlements[i].SiteCell) != v;
            if (empty) { to = v; break; }
        }
        Assert.True(to >= 0, "no empty node out of sight beside the rival - the rig is void");
        for (int i = 0; i < moved.Settlements.Count; i++)
            if (moved.Settlements[i].Id == rival.Id) moved.Settlements[i] = moved.Settlements[i] with { SiteCell = CentreOf(size, to) };

        SiteAssessment.Prepared site = Site(w);
        PolityKnowledge stub = PolityKnowledge.Restricted(w, Player, known, visible), stubMoved = PolityKnowledge.Restricted(moved, Player, known, visible);
        Assert.True(stub.KnowsCell(cell));
        Assert.False(stub.KnowsSettlement(rival.Id));

        SiteAssessment.Report r = Assess(w, site, cell, k: stub);
        Assert.True(r.Known);
        Assert.Equal((-1, -1), (r.OccupiedBy, r.ClaimedBy));
        Assert.Equal((null, true), (ArmOf(r, FoundingVerdict.Occupied).Holds, ArmOf(r, FoundingVerdict.Occupied).Unseen));
        Assert.Equal((null, true), (ArmOf(r, FoundingVerdict.Claimed).Holds, ArmOf(r, FoundingVerdict.Claimed).Unseen));
        Assert.Null(r.Verdict);
        Assert.Null(SiteAssessment.FirstFailing(r.Arms));
        Assert.DoesNotContain(r.Nearest, x => x.Id == rival.Id);

        InfoCard a = CardOf(w, site, stub, cell), b = CardOf(moved, site, stubMoved, cell);
        Assert.True(a == b, "the card moved with the unseen settlement:\n" + Text(a) + "\n---\n" + Text(b));
        string text = Text(a);
        foreach (string told in new[] { "a settlement already stands here", "The site of a settlement", "a settlement you do not know", Named(rival.Id.Value) })
            Assert.DoesNotContain(told, text, StringComparison.Ordinal);

        // CONTROL: all-known, the card names the settlement standing there, and the move changes the card.
        InfoCard ca = CardOf(w, site, any, cell), cb = CardOf(moved, site, PolityKnowledge.Of(moved, Player), cell);
        Assert.NotEqual(ca, cb);
        Assert.Contains("The site of " + Named(rival.Id.Value) + ".", Text(ca), StringComparison.Ordinal);
        Assert.False(ArmOf(Assess(w, site, cell), FoundingVerdict.Occupied).Holds);   // all-known, the occupancy arm is judged and fails
    }

    [Fact]
    public void AKnownPlaceAmidUnknownGround_ShowsNothingThatGroundDecides_WhateverLiesThere_TheAllKnownControlSeesIt()
    {
        // P8 review (finding 1, third case): with the eight nodes around a known cell hidden and only their fertility
        // changed, the card's first line moved (the score's box reaches them). Now each factor whose reach touches unknown
        // ground is not judged — the fertility box, the cells the water BFS reads, the hinterland disc — and so are the
        // floor and the rank (every cell), and the card is identical whatever lies under those nodes: fertility 0 or 1,
        // rivers everywhere, rough ground. CONTROL: all-known, each variant moves the score and the card.
        WorldState w0 = W1();
        int size = w0.Terrain!.Size;
        SiteAssessment.Prepared site0 = Site(w0);
        PolityKnowledge any = PolityKnowledge.Of(w0, Player);
        (int l, _) = PolityKnowledge.Grid(size);
        int target = -1;
        for (int cell = 0; cell < size * size && target < 0; cell += 13)
        {
            int x = cell % size, y = cell / size;
            if (x < 12 || y < 12 || x >= size - 12 || y >= size - 12) continue;
            if (w0.Terrain.Water[cell] >= 0.5 || w0.Terrain.Rivers[cell] >= 0.5 || !(site0.Frontier.Scores[cell] > 0.0)) continue;
            if (site0.WaterDistancePx[cell] < 6) continue;
            bool far = true;
            for (int i = 0; i < w0.Settlements.Count; i++)
            {
                int s = w0.Settlements[i].SiteCell;
                far &= Math.Max(Math.Abs(s % size - x), Math.Abs(s / size - y)) > 24;
            }
            if (far) target = cell;
        }
        Assert.True(target >= 0, "no remote dry scored land cell - the rig is void");
        int node = any.NodeOfCell(target);
        var known = new bool[l * l];
        for (int v = 0; v < known.Length; v++) known[v] = Math.Max(Math.Abs(v % l - node % l), Math.Abs(v / l - node / l)) != 1;

        var cards = new List<InfoCard>();
        var controlScores = new List<double>();
        var controlCards = new List<InfoCard>();
        foreach (double fertility in new[] { -1.0, 0.0, 1.0 })
        {
            WorldState w = W1();
            if (fertility >= 0.0) w.Terrain = RewrittenUnderUnknown(w.Terrain!, PolityKnowledge.Restricted(w, Player, known, known), fertility);
            SiteAssessment.Prepared site = Site(w);
            PolityKnowledge stub = PolityKnowledge.Restricted(w, Player, known, known);
            Assert.True(stub.KnowsCell(target));
            SiteAssessment.Report r = Assess(w, site, target, k: stub);
            Assert.True(r.Known);
            Assert.True(r.RegionFertilityUnseen && r.WaterAccessUnseen && r.ScoreUnseen);
            Assert.True(double.IsNaN(r.Score) && double.IsNaN(r.RegionFertility) && double.IsNaN(r.WaterAccess) && double.IsNaN(r.Floor));
            Assert.Equal(-1, r.WaterDistancePx);
            Assert.Equal(0L, r.PositiveScores);
            Assert.Empty(r.Deposits);
            Assert.Contains("not discovered", r.DepositsUnavailable, StringComparison.Ordinal);
            Assert.Null(r.WouldBeArableKm2);
            Assert.Null(r.Route);
            cards.Add(CardOf(w, site, stub, target));

            SiteAssessment.Report all = Assess(w, site, target);
            controlScores.Add(all.Score);
            controlCards.Add(CardOf(w, site, PolityKnowledge.Of(w, Player), target));
        }
        Assert.True(cards[0] == cards[1] && cards[0] == cards[2], "what lies under the unknown nodes reaches the card:\n" + Text(cards[0]) + "\n---\n" + Text(cards[1]));
        Assert.Equal("Siting score: not judged", cards[0].More[0].Lines[0].Label);
        Assert.Equal(3, controlScores.Distinct().Count());   // CONTROL: all-known, each variant moves the score
        Assert.NotEqual(controlCards[0], controlCards[1]);
        Assert.NotEqual(controlCards[1], controlCards[2]);
    }

    [Fact]
    public void AStubThatHidesNothing_WithholdsNothing_AndOneThatHidesOnlyFarGround_StillShowsTheKnownReachesExactly()
    {
        // The "withheld or shown" choice is a function of the knowledge masks. A stub hiding nothing (every node known and in
        // sight) sees the whole world: every card is the all-known card. A stub with one far node out of sight does not:
        // the whole-map values (floor, rank, spacing, catchment, route) are not judged — but a cell whose own reaches are
        // known still shows its score, both factors and its expected deposits exactly.
        WorldState w = Duo1();
        int size = w.Terrain!.Size;
        SiteAssessment.Prepared site = Site(w);
        (int l, _) = PolityKnowledge.Grid(size);
        var every = new bool[l * l];
        Array.Fill(every, true);
        PolityKnowledge all = PolityKnowledge.Of(w, Player), none = PolityKnowledge.Restricted(w, Player, every, every);
        Assert.False(none.AllKnown);
        Assert.True(SiteAssessment.SeesAll(none));
        // Every line is the all-known card's except the "Discovered" note, which says which knowledge state this is.
        static string Shown(InfoCard c) => string.Join("\n", Text(c).Split('\n').Where(x => !x.StartsWith("Discovered|", StringComparison.Ordinal)));
        foreach (int cell in Sample(w, 1, 1999)) Assert.Equal(Shown(CardOf(w, site, all, cell)), Shown(CardOf(w, site, none, cell)));

        int capital = FoundingRigs.SiteCellOf(w, new SettlementId(SiteAssessment.DefaultSource(w, Player)));
        PolityKnowledge any = all;
        int farthest = -1;
        long bestD = -1;
        int cn = any.NodeOfCell(capital);
        for (int v = 0; v < l * l; v++)
        {
            long dx = v % l - cn % l, dy = v / l - cn / l, d = dx * dx + dy * dy;
            if (d > bestD) { bestD = d; farthest = v; }
        }
        var visible = (bool[])every.Clone();
        visible[farthest] = false;
        PolityKnowledge oneOut = PolityKnowledge.Restricted(w, Player, every, visible);
        Assert.False(SiteAssessment.SeesAll(oneOut));
        int probe = -1;   // a dry land cell beside the capital
        foreach (int c in FoundingRigs.Ring(size, capital, 6)) if (w.Terrain.Water[c] < 0.5 && site.Frontier.Scores[c] > 0.0) { probe = c; break; }
        Assert.True(probe >= 0);
        SiteAssessment.Report r = Assess(w, site, probe, k: oneOut), a = Assess(w, site, probe);
        Assert.False(r.ScoreUnseen);
        Assert.Equal((a.Score, a.RegionFertility, a.WaterAccess), (r.Score, r.RegionFertility, r.WaterAccess));
        Assert.Equal((a.MeanMoisture, a.MeanElevation), (r.MeanMoisture, r.MeanElevation));
        Assert.Equal(a.Deposits.Length, r.Deposits.Length);
        for (int i = 0; i < a.Deposits.Length; i++) Assert.Equal(a.Deposits[i] with { IfFoundedNext = null }, r.Deposits[i]);
        Assert.True(double.IsNaN(r.Floor) && double.IsNaN(r.SpacingCost));
        Assert.Null(r.WouldBeArableKm2);
        Assert.Null(r.Route);
        Assert.Equal(-1, r.AccessibleFrom);
        InfoCard card = CardOf(w, site, oneOut, probe);
        Assert.StartsWith("Siting score " , card.More[0].Lines[0].Label, StringComparison.Ordinal);
        Assert.Equal(CardOf(w, site, all, probe).More[0].Lines[0].Label, card.More[0].Lines[0].Label);
        Assert.StartsWith("Score floor: not judged", card.More[0].Lines[4].Label, StringComparison.Ordinal);
    }

    // ================================================================== THE NEAREST ORDER (tie-dense)

    [Fact]
    public void TheNearestOrder_IsSquaredDistanceThenId_TieDense_AndIndependentOfInputOrder()
    {
        // 240 entries over only 4 distances: almost every comparison is a distance tie, decided by the id.
        var items = new SiteAssessment.Nearby[240];
        for (int i = 0; i < items.Length; i++)
            items[i] = new SiteAssessment.Nearby(new SettlementId((i * 97) % 240), (i * 7) % 4, Math.Sqrt((i * 7) % 4));
        SiteAssessment.Nearby[] a = [.. items], b = [.. items];
        Array.Reverse(b);
        Array.Sort(a, SiteAssessment.CompareNearby);
        Array.Sort(b, SiteAssessment.CompareNearby);
        Assert.Equal(a, b);
        for (int i = 1; i < a.Length; i++)
            Assert.True(a[i - 1].DistanceSquaredPx < a[i].DistanceSquaredPx
                || (a[i - 1].DistanceSquaredPx == a[i].DistanceSquaredPx && a[i - 1].Id.Value < a[i].Id.Value),
                "out of order at " + i);
    }

    [Fact]
    public void AtAPlaceEquidistantFromTwoSettlements_TheLowerIdIsListedFirst()
    {
        // A world-level tie: a cell whose two nearest settlements are at exactly the same squared distance.
        WorldState w = W1();
        int size = w.Terrain!.Size;
        SiteAssessment.Prepared site = Site(w);
        int found = 0;
        for (int cell = 0; cell < size * size && found < 3; cell++)
        {
            var d = new List<(long D2, int Id)>();
            for (int i = 0; i < w.Settlements.Count; i++)
            {
                long dx = w.Settlements[i].SiteCell % size - cell % size, dy = w.Settlements[i].SiteCell / size - cell / size;
                d.Add((dx * dx + dy * dy, w.Settlements[i].Id.Value));
            }
            d.Sort();
            if (d.Count < 2 || d[0].D2 != d[1].D2) continue;
            SiteAssessment.Report r = Assess(w, site, cell);
            Assert.Equal(d[0].D2, r.Nearest[0].DistanceSquaredPx);
            Assert.Equal(d[1].D2, r.Nearest[1].DistanceSquaredPx);
            Assert.True(r.Nearest[0].Id.Value < r.Nearest[1].Id.Value, "the tie went to the higher id at cell " + cell);
            Assert.Equal(Math.Sqrt(d[0].D2) * w.Terrain.KmPerPx, r.Nearest[0].Km);
            found++;
        }
        Assert.True(found > 0, "no tied cell on the map - the rig is void");
    }

    // ================================================================== THE CARD

    [Fact]
    public void TheCellCard_ShowsTheOneCompositeDecomposed_ThenEveryOtherFactorSeparatelyLabelled_NoWeightedComposite()
    {
        WorldState w = W1();
        SiteAssessment.Prepared site = Site(w);
        int cell = FoundingRigs.NearestLegalSite(w, Cfg, site.Frontier, Player, w.Settlements[0].Id);
        SiteAssessment.Report r = Assess(w, site, cell);
        InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(cell), site: site);
        Assert.Equal(InfoKind.Cell, card.Subject.Kind);
        Assert.Equal("Land at " + (cell % w.Terrain!.Size) + ", " + (cell / w.Terrain.Size), card.Title);
        Assert.Equal(InfoStatus.Known, card.Status);

        // The composite: first, alone, exactly the simulation's score and its two factors.
        InfoSection composite = card.More[0];
        Assert.Equal("The simulation's siting score", composite.Heading);
        string F(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Siting score " + F(r.Score) + " = region fertility " + F(r.RegionFertility) + " x water access " + F(r.WaterAccess),
            composite.Lines[0].Label);
        // Every other section is labelled as outside the score, except the closing "Not simulated" list.
        for (int i = 1; i < card.More.Length; i++)
        {
            string h = card.More[i].Heading;
            Assert.True(h.EndsWith(" - not part of the score", StringComparison.Ordinal) || h == "Not simulated", "section '" + h + "' is not labelled");
            Assert.DoesNotContain("Siting score", h, StringComparison.Ordinal);
        }
        Assert.Equal("Not simulated", card.More[^1].Heading);
        // No invented weight anywhere on the card.
        foreach (InfoSection s in card.More)
            foreach (InfoLink line in s.Lines)
            {
                Assert.DoesNotContain("weight", (line.Label + " " + line.Note).Replace("fertility-weighted", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("suitability", line.Label, StringComparison.OrdinalIgnoreCase);
            }
        // Each founding arm is shown with its own tick.
        InfoSection arms = card.More.Single(s => s.Heading.StartsWith("Founding here", StringComparison.Ordinal));
        Assert.Equal(r.Arms.Length + 1, arms.Lines.Length);
        for (int i = 0; i < r.Arms.Length; i++) Assert.Equal(r.Arms[i].Holds, arms.Lines[i + 1].Met);
        // Every deposit good links to its card and is labelled expected.
        InfoSection deposits = card.More.Single(s => s.Heading.StartsWith("Deposits", StringComparison.Ordinal));
        Assert.Contains("(expected)", deposits.Heading, StringComparison.Ordinal);
        Assert.Equal(r.Deposits.Length, deposits.Lines.Count(l => l.Subject is { Kind: InfoKind.Good }));
        // Discovery, presence, access and extraction are four separate lines.
        InfoSection four = card.More.Single(s => s.Heading.StartsWith("Discovered, present, reachable, workable", StringComparison.Ordinal));
        Assert.Equal(new[] { "Discovered", "Present", "Reachable", "Workable" }, four.Lines.Select(l => l.Label).ToArray());
    }

    [Fact]
    public void TheCellCard_WithoutTheSiteHalf_IsTitledAndInert_AndAnOffMapIndexNamesNothing()
    {
        WorldState w = W1();
        int cell = w.Settlements[0].SiteCell;
        InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(cell));
        Assert.StartsWith("Land at ", card.Title);
        Assert.Equal(InfoStatus.Inert, card.Status);
        // The title does not depend on the site half (the playability gate's expected-title reading has none).
        Assert.Equal(card.Title, InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(cell), site: Site(w)).Title);
        Assert.Equal(InfoStatus.Unknown, InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(w.Terrain!.Size * w.Terrain.Size)).Status);
        Assert.Equal(InfoStatus.Unknown, InfoQuery.Card(w, Cfg, Player, InfoSubject.OfCell(-1)).Status);
    }
}
