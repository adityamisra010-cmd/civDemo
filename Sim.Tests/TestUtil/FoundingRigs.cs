using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>
/// M5 S1b (ADR-036) — rigs for the Found Settlement order. Emergent B1 founding is near-dormant on the shipped
/// weather (CR-020 §B.2), so every rig here is ORDER-driven: a founded world, a site found by asking THE predicate
/// (FoundingQuery.Check) in a fixed scan order, and an order stamped for the turn being played.
///
/// The site scan is a stable INTEGER order — Chebyshev rings around the source's site cell by ascending radius,
/// row-major within a ring (the LatticeMap.OriginLatticeNode ring order) — so a rig's site is a pure function of
/// the world and never of a double-valued ranking. It is a TEST convenience, not a site policy: the AI's ranking
/// is S1c's, the player's choice S1d's.
/// </summary>
internal static class FoundingRigs
{
    internal static SimConfig Cfg() => TestConfigs.Sim();

    /// <summary>The production pipeline over <paramref name="cfg"/> (worldgen siting config attached, so
    /// colonization is live), reading <paramref name="orders"/>.</summary>
    internal static TurnExecutor Pipeline(SimConfig cfg, OrderLog? orders, WorldgenConfig? worldgen = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, worldgen ?? TestConfigs.Worldgen())), orders);
    }

    /// <summary>ColonizationSystem alone: nothing else can move a person or a grain, so the step's changes are
    /// the founding's and nothing else's.</summary>
    internal static TurnExecutor ColonizationOnly(SimConfig cfg, OrderLog? orders, WorldgenConfig? worldgen = null) =>
        new(GovernanceRigs.FlatEra(), [SystemCatalog.Colonization(cfg, worldgen ?? TestConfigs.Worldgen())], orders);

    /// <summary>A founded DEV world (256², 4 settlements; the D-015 preset).</summary>
    internal static WorldState Dev(ulong seed = 42UL, int aiEmpires = 0) =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = aiEmpires }, Cfg(), seed);

    internal static SettlementSiting.FrontierSiting Frontier(IReadOnlyWorldState w, SimConfig? cfg = null, WorldgenConfig? worldgen = null) =>
        FoundingQuery.Frontier(w, cfg ?? Cfg(), worldgen ?? TestConfigs.Worldgen())
        ?? throw new InvalidOperationException("the rig world has no terrain");

    internal static int SiteCellOf(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == s) return w.Settlements[i].SiteCell;
        throw new InvalidOperationException($"no settlement {s.Value}");
    }

    internal static int MaxSettlementId(IReadOnlyWorldState w)
    {
        int max = -1;
        for (int i = 0; i < w.Settlements.Count; i++) max = Math.Max(max, w.Settlements[i].Id.Value);
        return max;
    }

    internal static bool TryGetSettlement(IReadOnlyWorldState w, int id, out SettlementRow row)
    {
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].Id.Value == id) { row = w.Settlements[i]; return true; }
        row = default;
        return false;
    }

    /// <summary>The cells of the Chebyshev ring of radius <paramref name="r"/> around <paramref name="center"/>,
    /// row-major — a stable integer scan order.</summary>
    internal static IEnumerable<int> Ring(int size, int center, int r)
    {
        int cx = center % size, cy = center / size;
        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= size) continue;
            for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                int x = cx + dx;
                if (x < 0 || x >= size) continue;
                yield return y * size + x;
            }
        }
    }

    /// <summary>The first cell (ring scan from the source, skipping the first <paramref name="skip"/> matches) for
    /// which THE predicate answers <paramref name="want"/> on <paramref name="w"/>; −1 when none within
    /// <paramref name="maxRadius"/>.</summary>
    internal static int FirstCell(
        IReadOnlyWorldState w, SimConfig cfg, SettlementSiting.FrontierSiting f, PolityId issuer, SettlementId source,
        FoundingVerdict want, int skip = 0, int maxRadius = int.MaxValue)
    {
        var occupied = new int[w.Settlements.Count];
        for (int i = 0; i < occupied.Length; i++) occupied[i] = w.Settlements[i].SiteCell;
        double[] spacing = SettlementSiting.SeedSpacing(f, occupied);
        int center = SiteCellOf(w, source);
        int limit = Math.Min(maxRadius, f.Size);
        for (int r = 1; r < limit; r++)
            foreach (int cell in Ring(f.Size, center, r))
            {
                if (FoundingQuery.Check(w, cfg, f, spacing, occupied, issuer, source, cell) != want) continue;
                if (skip-- > 0) continue;
                return cell;
            }
        return -1;
    }

    /// <summary>The nearest LEGAL site for a Found Settlement order from <paramref name="source"/>.</summary>
    internal static int NearestLegalSite(
        IReadOnlyWorldState w, SimConfig cfg, SettlementSiting.FrontierSiting f, PolityId issuer, SettlementId source,
        int skip = 0) =>
        FirstCell(w, cfg, f, issuer, source, FoundingVerdict.Legal, skip);

    /// <summary>A LAND candidate the source CANNOT reach: a frontier candidate outside every spacing exclusion and
    /// every catchment whose origin node lies outside the source's connected component (one uncapped Dijkstra
    /// from the source, then a cell scan). −1 when the map has none.</summary>
    internal static int UnreachableCandidate(IReadOnlyWorldState w, SettlementSiting.FrontierSiting f, SettlementId source)
    {
        int sourceNode = LatticeMap.OriginLatticeNode(f.Lattice, f.Size, SiteCellOf(w, source));
        Pathfinder.IsochroneResult all = Pathfinder.Isochrone(f.Lattice, w, sourceNode, double.MaxValue);
        var reached = new bool[f.Lattice.NodeCount];
        foreach (int n in all.Reached) reached[n] = true;
        var occupied = new int[w.Settlements.Count];
        for (int i = 0; i < occupied.Length; i++) occupied[i] = w.Settlements[i].SiteCell;
        double[] spacing = SettlementSiting.SeedSpacing(f, occupied);
        for (int cell = 0; cell < f.Scores.Length; cell++)
        {
            if (!SettlementSiting.IsLegalFrontierCell(f, w.Terrain!, spacing, occupied, cell)) continue;
            int node = LatticeMap.OriginLatticeNode(f.Lattice, f.Size, cell);
            if (reached[node] || FoundingQuery.IsClaimed(w, node)) continue;
            return cell;
        }
        return -1;
    }

    /// <summary>Sinks <paramref name="amount"/> of <paramref name="good"/> from a settlement's stock through the
    /// Ledger (reason InitialEndowment, the battery's rig precedent), so the conservation audit still balances.</summary>
    internal static void SinkGood(WorldState w, SettlementId s, GoodId good, long amount)
    {
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Settlement != s || w.GoodStocks[i].Good != good) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(good),
                ReasonIds.InitialEndowment, amount, FlowDirection.Sink, OverdrawPolicy.Throw);
            return;
        }
        throw new InvalidOperationException($"settlement {s.Value} has no stock of good {good.Value}");
    }

    internal static long Grain(IReadOnlyWorldState w, SettlementId s)
    {
        var grain = new GoodId(Cfg().Goods!.GrainId);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good == grain) return w.GoodStocks[i].Amount.Value;
        return 0;
    }

    internal static long People(IReadOnlyWorldState w, SettlementId s)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) if (w.Buckets[i].Settlement == s) n += w.Buckets[i].Count.Value;
        return n;
    }

    internal static long People(IReadOnlyWorldState w)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) n += w.Buckets[i].Count.Value;
        for (int i = 0; i < w.Notables.Count; i++) n += w.Notables[i].Count.Value;
        return n;
    }

    internal static long Grain(IReadOnlyWorldState w)
    {
        var grain = new GoodId(Cfg().Goods!.GrainId);
        long n = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) if (w.GoodStocks[i].Good == grain) n += w.GoodStocks[i].Amount.Value;
        return n;
    }
}
