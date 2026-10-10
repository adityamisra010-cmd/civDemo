using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Worldgen;

/// <summary>
/// M5 S1a — THE SITING AND FOUNDING READ SEAMS, a pure refactor. Four claims, each measured:
///   1. <see cref="SettlementSiting.ChooseFrontierSite"/> returns the SAME site as the T4.4 inline
///      scan it replaced (kept below verbatim as a witness) over a 20-seed sweep and every
///      spacing/occupancy state the sweep drives, and that site is the (score DESC, cell id ASC)
///      argmax over <see cref="SettlementSiting.IsLegalFrontierCell"/> cells.
///   2. <see cref="SettlementSiting.IsLegalFrontierCell"/> is exactly the four T4.4 rules, cell for
///      cell, with the strict spacing boundary the T4.4 tests rely on.
///   3. <see cref="SettlementSiting.CandidateScores"/> is region fertility (<see cref="SettlementSiting.BoxSum"/>)
///      × water access (<see cref="SettlementSiting.WaterAccess"/>), bit for bit, so a read-only
///      consumer can show the two factors and the product they make.
///   4. The public hinterland field (<see cref="WorldFounding.HinterlandMeans"/>), evaluated AT EACH
///      SETTLEMENT'S SITE CELL (exact — never through a raster), reproduces that settlement's
///      DepositRow bit for bit for every spread-0 good, for turn-zero AND colony-founded settlements.
/// The 1024² canonical 20-seed before/after sweep (720 picks, identical output files) is recorded
/// in the S1a commit; the in-test sweep runs the dev preset so it is a CI cost of seconds.
/// </summary>
public class SitingReadSeamTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();

    private static int[] SiteCells(WorldState w)
    {
        var cells = new int[w.Settlements.Count];
        for (int i = 0; i < cells.Length; i++) cells[i] = w.Settlements[i].SiteCell;
        return cells;
    }

    /// <summary>THE WITNESS: ChooseFrontierSite's body at 82bde01, before S1a, verbatim except
    /// that the private IsOccupied is spelled out. Kept so the refactor is compared against the
    /// code it replaced, not against itself.</summary>
    private static int T44InlineScan(
        SettlementSiting.FrontierSiting f, TerrainSet terrain, double[] spacing,
        ReadOnlySpan<int> occupiedSiteCells, double scoreJitter, ulong seed)
    {
        ReadOnlySpan<double> water = terrain.Water;
        int bestCell = -1;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < f.Scores.Length; i++)
        {
            if (water[i] >= 0.5) continue;                       // land only
            double unjittered = f.Scores[i];
            if (unjittered <= 0.0 || unjittered < f.Floor) continue;
            double score = unjittered * SettlementSiting.Jitter(seed, i, scoreJitter);
            if (score <= bestScore) continue;
            bool occupied = false;
            for (int k = 0; k < occupiedSiteCells.Length; k++) if (occupiedSiteCells[k] == i) occupied = true;
            if (occupied) continue;                              // exact site cell
            int node = LatticeMap.OriginLatticeNode(f.Lattice, f.Size, i);
            if (spacing[node] < f.MinSpacingCostUnits) continue; // travel-cost spacing
            bestScore = score;
            bestCell = i;
        }
        return bestCell;
    }

    /// <summary>The argmax over IsLegalFrontierCell cells by an EXPLICIT composite key
    /// (score DESC, cell id ASC) — the oracle the scan must agree with.</summary>
    private static int LegalArgmax(
        SettlementSiting.FrontierSiting f, TerrainSet terrain, double[] spacing,
        ReadOnlySpan<int> occupied, double scoreJitter, ulong seed)
    {
        int bestCell = -1;
        double bestScore = 0.0;
        for (int i = 0; i < f.Scores.Length; i++)
        {
            if (!SettlementSiting.IsLegalFrontierCell(f, terrain, spacing, occupied, i)) continue;
            double score = f.Scores[i] * SettlementSiting.Jitter(seed, i, scoreJitter);
            if (bestCell < 0 || score > bestScore || (score == bestScore && i < bestCell))
            {
                bestScore = score;
                bestCell = i;
            }
        }
        return bestCell;
    }

    [Fact]
    public void ChooseFrontierSite_EqualsTheT44InlineScan_AndTheLegalArgmax_Over20Seeds()
    {
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        int picks = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            WorldState w = WorldFounding.Found(wg, Cfg, seed);
            TerrainSet t = w.Terrain!;
            int[] sites = SiteCells(w);

            // Three spacing regimes (canonical, none — occupancy is then the only obstruction —
            // and a short one) and two jitters (shipped, none).
            foreach (double km in new[] { wg.Siting.MinSpacingKm, 0.0, 40.0 })
            foreach (double jitter in new[] { wg.Siting.ScoreJitter, 0.0 })
            {
                SitingConfig siting = wg.Siting with { MinSpacingKm = km };
                var f = SettlementSiting.PrepareFrontier(t, siting, Cfg.Transport.RiverCostFactor);

                // Nothing standing yet.
                double[] empty = SettlementSiting.SeedSpacing(f, []);
                Check(f, t, empty, [], jitter, w.Seed);

                // Every founded site standing, then six accepted picks in a row.
                double[] spacing = SettlementSiting.SeedSpacing(f, sites);
                var occupied = new List<int>(sites);
                for (int k = 0; k < 6; k++)
                {
                    int pick = Check(f, t, spacing, occupied.ToArray(), jitter, w.Seed);
                    if (pick < 0) break;
                    SettlementSiting.AcceptFrontierSite(f, spacing, pick);
                    occupied.Add(pick);
                }
            }
        }
        Console.WriteLine($"S1a frontier sweep: {picks} legal picks compared (20 dev seeds × 6 regimes).");
        Assert.True(picks >= 500, $"only {picks} legal picks in the sweep — too few to compare anything");

        int Check(SettlementSiting.FrontierSiting f, TerrainSet t, double[] spacing, int[] occupied,
            double jitter, ulong seed)
        {
            int now = SettlementSiting.ChooseFrontierSite(f, t, spacing, occupied, jitter, seed);
            Assert.Equal(T44InlineScan(f, t, spacing, occupied, jitter, seed), now);
            Assert.Equal(LegalArgmax(f, t, spacing, occupied, jitter, seed), now);
            if (now >= 0)
            {
                Assert.True(SettlementSiting.IsLegalFrontierCell(f, t, spacing, occupied, now));
                picks++;
            }
            return now;
        }
    }

    [Fact]
    public void IsLegalFrontierCell_IsExactlyTheFourT44Rules_CellForCell()
    {
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        WorldState w = WorldFounding.Found(wg, Cfg, 3UL);
        TerrainSet t = w.Terrain!;
        int[] sites = SiteCells(w);
        var f = SettlementSiting.PrepareFrontier(t, wg.Siting, Cfg.Transport.RiverCostFactor);
        double[] spacing = SettlementSiting.SeedSpacing(f, sites);

        int water = 0, score = 0, occupied = 0, spaced = 0, legal = 0;
        for (int i = 0; i < f.Scores.Length; i++)
        {
            // The rules restated from their definitions, in rejection order.
            bool expected;
            if (t.Water[i] >= 0.5) { expected = false; water++; }
            else if (f.Scores[i] <= 0.0 || f.Scores[i] < f.Floor) { expected = false; score++; }
            else if (Array.IndexOf(sites, i) >= 0) { expected = false; occupied++; }
            else if (spacing[LatticeMap.OriginLatticeNode(f.Lattice, t.Size, i)] < f.MinSpacingCostUnits)
            { expected = false; spaced++; }
            else { expected = true; legal++; }

            Assert.Equal(expected, SettlementSiting.IsLegalFrontierCell(f, t, spacing, sites, i));
            Assert.Equal(SettlementSiting.IsFrontierCandidate(f, t, i)
                && SettlementSiting.IsUnobstructedFrontierCell(f, spacing, sites, i),
                SettlementSiting.IsLegalFrontierCell(f, t, spacing, sites, i));
        }
        // Every rule rejects something on this world, or the comparison above is partly vacuous.
        Assert.True(water > 0 && score > 0 && occupied == sites.Length && spaced > 0 && legal > 0,
            $"water {water}, score/floor {score}, occupied {occupied}, spacing {spaced}, legal {legal}");

        // Off the map is never legal.
        Assert.False(SettlementSiting.IsLegalFrontierCell(f, t, spacing, sites, -1));
        Assert.False(SettlementSiting.IsLegalFrontierCell(f, t, spacing, sites, f.Scores.Length));
    }

    [Fact]
    public void SpacingRule_IsStrictlyLess_AtTheExactBoundary()
    {
        // T4.4's rejection is `spacing[node] < MinSpacingCostUnits`, which the zero-spacing
        // occupancy test relies on. A node exactly AT the floor is legal; one ULP under is not.
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        WorldState w = WorldFounding.Found(wg, Cfg, 3UL);
        TerrainSet t = w.Terrain!;
        var f = SettlementSiting.PrepareFrontier(t, wg.Siting, Cfg.Transport.RiverCostFactor);
        int cell = -1;
        for (int i = 0; i < f.Scores.Length && cell < 0; i++)
            if (SettlementSiting.IsFrontierCandidate(f, t, i)) cell = i;
        Assert.True(cell >= 0);
        int node = LatticeMap.OriginLatticeNode(f.Lattice, t.Size, cell);

        var spacing = new double[f.Lattice.NodeCount];
        Array.Fill(spacing, double.PositiveInfinity);
        spacing[node] = f.MinSpacingCostUnits;
        Assert.True(SettlementSiting.IsLegalFrontierCell(f, t, spacing, [], cell));
        spacing[node] = Math.BitDecrement(f.MinSpacingCostUnits);
        Assert.False(SettlementSiting.IsLegalFrontierCell(f, t, spacing, [], cell));
        // And occupancy rejects regardless of spacing.
        spacing[node] = double.PositiveInfinity;
        Assert.False(SettlementSiting.IsLegalFrontierCell(f, t, spacing, [cell], cell));
        Assert.True(SettlementSiting.IsOccupied([5, cell, 9], cell));
        Assert.False(SettlementSiting.IsOccupied([5, 9], cell));
    }

    [Fact]
    public void CandidateScores_IsRegionFertilityTimesWaterAccess_BitForBit()
    {
        // The shipped cutoff, and a short one: on the 256² dev map no land lies 24 px from water
        // or river (measured), so only the short cutoff exercises the floored-access branch.
        WorldgenConfig dev = TestConfigs.DevWorldgen();
        TerrainSet t = Sim.Core.Worldgen.Worldgen.Generate(dev, 5UL);
        int[] distance = SettlementSiting.WaterDistanceBfs(t.Water, t.Rivers, t.Size);
        int positive = 0, cutOff = 0;
        foreach (int cutoffPx in new[] { dev.Siting.WaterAccessCutoffPx, 4 })
        {
            SitingConfig siting = dev.Siting with { WaterAccessCutoffPx = cutoffPx };
            double[] scores = SettlementSiting.CandidateScores(t, siting);
            double[] region = SettlementSiting.BoxSum(t.Fertility, t.Size, siting.FertilityRadiusPx);
            for (int i = 0; i < scores.Length; i++)
            {
                double access = SettlementSiting.WaterAccess(distance[i], cutoffPx);
                double expected = t.Water[i] >= 0.5 || access <= 0.0 ? 0.0 : region[i] * access;
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(scores[i]));
                if (scores[i] > 0.0) positive++;
                else if (t.Water[i] < 0.5 && access <= 0.0) cutOff++;
            }
        }
        Assert.True(positive > 0 && cutOff > 0, $"positive {positive}, land beyond the cutoff {cutOff}");
    }

    [Fact]
    public void WaterAccessAndBoxSum_PinnedValues()
    {
        // 1 at the shore, linear to 0 at the cutoff, floored at 0 beyond it (BFS-unreachable too).
        Assert.Equal(1.0, SettlementSiting.WaterAccess(0, 24));
        Assert.Equal(0.5, SettlementSiting.WaterAccess(12, 24));
        Assert.Equal(1.0 - 23 / 24.0, SettlementSiting.WaterAccess(23, 24));
        Assert.Equal(0.0, SettlementSiting.WaterAccess(24, 24));
        Assert.Equal(0.0, SettlementSiting.WaterAccess(25, 24));
        Assert.Equal(0.0, SettlementSiting.WaterAccess(int.MaxValue, 24));

        // 3×3 field 1..9, radius 1, edge-clamped neighbourhoods.
        double[] field = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        Assert.Equal([12.0, 21.0, 16.0, 27.0, 45.0, 33.0, 24.0, 39.0, 28.0], SettlementSiting.BoxSum(field, 3, 1));
        Assert.Equal(field, SettlementSiting.BoxSum(field, 3, 0));
    }

    // ------------------------------------------------------------------ the deposit field

    /// <summary>The channel value spelled from goods.json's documentation, independently of the
    /// code under test: moisture → moisture mean; elevation → elevation mean; water →
    /// moisture mean SQUARED.</summary>
    private static double Channel(string channel, double m, double e) => channel switch
    {
        "moisture" => m,
        "elevation" => e,
        "water" => m * m,
        _ => throw new InvalidOperationException($"unknown deposit channel '{channel}'"),
    };

    /// <summary>Every settlement standing in <paramref name="w"/> — founded at turn zero or by
    /// colonization — has DepositRows equal to the field at its site cell.</summary>
    private static (int Settlements, int Exact, int Bounded) AssertFieldMatchesDeposits(
        WorldState w, WorldgenConfig wg)
    {
        GoodsConfig goods = Cfg.Goods!;
        double radiusPx = WorldFounding.HinterlandRadiusPx(wg, Cfg);
        Assert.Equal(Cfg.Catchment.HinterlandRadiusKm / wg.KmPerPx, radiusPx);
        int exact = 0, bounded = 0;
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementRow row = w.Settlements[s];
            (double m, double e) = WorldFounding.HinterlandMeans(w.Terrain!, wg, row.SiteCell, radiusPx);
            foreach (GoodEntry g in goods.Goods)
            {
                if (g.DepositChannel is null) continue;
                double field = Channel(g.DepositChannel, m, e);
                Assert.Equal(field, WorldFounding.DepositChannelValue(g.DepositChannel, m, e));
                double abundance = double.NaN;
                int found = 0;
                for (int i = 0; i < w.Deposits.Count; i++)
                {
                    DepositRow d = w.Deposits[i];
                    if (d.Settlement != row.Id || d.Good.Value != g.Id) continue;
                    abundance = d.Abundance;
                    found++;
                }
                Assert.Equal(1, found);
                if (g.DepositSpread == 0.0)
                {
                    Assert.True(BitConverter.DoubleToInt64Bits(field) == BitConverter.DoubleToInt64Bits(abundance),
                        $"settlement {row.Id.Value} (founded turn {row.FoundedTurn}) {g.Name}: field {field:R} != deposit {abundance:R}");
                    exact++;
                }
                else
                {
                    // An ore differs from the field only by its seeded spread factor in [1−s, 1+s].
                    Assert.True(abundance >= Math.Max(0.0, field * (1.0 - g.DepositSpread))
                        && abundance <= field * (1.0 + g.DepositSpread),
                        $"settlement {row.Id.Value} {g.Name}: {abundance:R} outside field {field:R} × [1 ∓ {g.DepositSpread}]");
                    bounded++;
                }
            }
        }
        return (w.Settlements.Count, exact, bounded);
    }

    /// <summary>Found, seed unplaced-departure demand at every settlement and run ONE colonization
    /// step (the ColonizationTests rig), so the world holds colony-founded settlements too.</summary>
    private static WorldState FoundedWithColonies(WorldgenConfig wg, ulong seed)
    {
        WorldState w = WorldFounding.Found(wg, Cfg, seed);
        for (int i = 0; i < w.Buckets.Count; i++) w.Buckets.Ref(i).UnplacedDeparture = 12.0;
        var exec = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.Colonization(Cfg, wg)]);
        return exec.Step(w);
    }

    [Fact]
    public void HinterlandField_AtEachSiteCell_ReproducesDepositRows_TurnZeroAndColonies_DevPreset20Seeds()
    {
        WorldgenConfig wg = TestConfigs.DevWorldgen();
        int colonies = 0, exact = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            WorldState w = FoundedWithColonies(wg, seed);
            for (int s = 0; s < w.Settlements.Count; s++)
                if (w.Settlements[s].FoundedTurn > 0) colonies++;
            exact += AssertFieldMatchesDeposits(w, wg).Exact;
        }
        Console.WriteLine($"S1a deposit field (dev, 20 seeds): {colonies} colonies, {exact} spread-0 rows exact.");
        Assert.True(colonies >= 20, $"only {colonies} colony-founded settlements across 20 seeds");
        Assert.True(exact > 0);
    }

    [Fact]
    public void HinterlandField_AtEachSiteCell_ReproducesDepositRows_TurnZeroAndColonies_Canonical()
    {
        WorldgenConfig wg = TestConfigs.Worldgen();
        foreach (ulong seed in new ulong[] { 42, 1 })
        {
            WorldState w = FoundedWithColonies(wg, seed);
            int colonies = 0;
            for (int s = 0; s < w.Settlements.Count; s++)
                if (w.Settlements[s].FoundedTurn > 0) colonies++;
            Assert.True(colonies > 0, $"seed {seed}: no colony was founded — the colony half is vacuous");
            (int settlements, int exact, int bounded) = AssertFieldMatchesDeposits(w, wg);
            Console.WriteLine($"S1a deposit field (canonical seed {seed}): {settlements} settlements "
                + $"({colonies} colonies), {exact} spread-0 rows exact, {bounded} ore rows in band.");
            Assert.True(exact > 0 && bounded > 0, $"seed {seed}: {settlements} settlements, {exact} exact, {bounded} ore");
        }
    }
}
