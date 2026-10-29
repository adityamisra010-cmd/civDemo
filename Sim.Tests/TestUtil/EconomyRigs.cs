using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;

namespace Sim.Tests.TestUtil;

/// <summary>
/// ADR-056 (the M7 economy packets) — shared rigs: a one-settlement hand-built world UNDER A CONTROLLER (polity 1), so
/// the packets' goods.json knowledge gates have knowledge to read (they are met only by knowledge — a world with no
/// control relation never meets one), and a knowledge closure helper.
/// </summary>
internal static class EconomyRigs
{
    public static readonly SettlementId S0 = new(0);
    public static readonly PolityId Ruler = new(1);

    public static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>One settlement, <paramref name="adults"/> adults (cohort 5), a stock row for every good of
    /// <paramref name="cfg"/>, the given sector allocation, deposits and stocks; controlled by <see cref="Ruler"/> when
    /// <paramref name="controlled"/>; artisan_share published at 0.5 so the artisan latch is open.</summary>
    public static WorldState World(
        SimConfig cfg, long adults, SectorAllocationRow allocation, bool controlled = true,
        (string Good, long Amount)[]? stocks = null, (string Good, double Abundance)[]? deposits = null,
        double arableKm2 = 0.0)
    {
        var counts = new long[Cohorts.Count];
        counts[5] = adults;
        var world = new WorldState(7);
        world.Settlements.Add(new SettlementRow(S0, SiteCell: 0, FoundedTurn: 0));
        var ledger = new Ledger(world.LedgerFlows);
        for (int c = 0; c < Cohorts.Count; c++)
        {
            int row = world.Buckets.Add(new BucketRow(
                S0, new CultureId(1), new ReligionId(1), new ClassId(1), c, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (counts[c] > 0)
                ledger.Flow(ref world.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                    ReasonIds.InitialEndowment, counts[c], FlowDirection.Source, OverdrawPolicy.Throw);
        }
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            S0, NodeCount: 1, EffectiveArableKm2: arableKm2, NetworkRevision: 0, LastRecomputeTurn: 0));
        world.SectorAllocations.Add(allocation);
        world.Variables.Add(new VariableRow(S0, Variables.ArtisanShare, 0.5));
        if (controlled) world.Controls.Add(new ControlRow(Ruler, S0, 1.0));
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            int row = world.GoodStocks.Add(new GoodStockRow(S0, new GoodId(g.Id), Conserved.Zero, 0.0, 0.0));
            long amount = 0;
            foreach ((string good, long qty) in stocks ?? []) if (good == g.Name) amount = qty;
            if (amount > 0)
                ledger.Flow(ref world.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(new GoodId(g.Id)),
                    ReasonIds.InitialEndowment, amount, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        foreach ((string good, double abundance) in deposits ?? [])
        {
            int id = cfg.Goods!.IdOf(good);
            if (id >= 0) world.Deposits.Add(new DepositRow(S0, new GoodId(id), abundance));
        }
        return world;
    }

    /// <summary>Completes, for <paramref name="polity"/> (every polity of the world when null) and for the local holder of
    /// every settlement under no controller, the knowledge closure of <paramref name="nodes"/> (each node and all its
    /// prerequisite ancestors, every OR alternative — the KnowTrade precedent). Data rows only; returns the world.</summary>
    public static WorldState Know(WorldState w, SimConfig cfg, PolityId? polity, params string[] nodes)
    {
        ResearchContent research = cfg.Research!;
        var seen = new bool[research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in nodes)
        {
            int i = research.IndexOfId(id);
            if (i < 0) throw new ArgumentException($"no research node '{id}'");
            stack.Push(i);
        }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        var holders = new List<PolityId>();
        if (polity is { } only) holders.Add(only);
        else
        {
            for (int p = 0; p < w.Polities.Count; p++) holders.Add(w.Polities[p].Id);
            for (int s = 0; s < w.Settlements.Count; s++)
                if (!EmpireQuery.TryGetController(w, w.Settlements[s].Id, out _))
                    holders.Add(SettlementKnowledge.LocalHolder(w.Settlements[s].Id));
        }
        foreach (PolityId h in holders)
            for (int i = 0; i < seen.Length; i++)
                if (seen[i] && !ResearchQuery.IsCompleted(w, h, research.Nodes[i].Key))
                    w.ResearchCompleted.Add(new ResearchCompletedRow(h, research.Nodes[i].Key));
        return w;
    }

    public static long Stock(IReadOnlyWorldState w, SimConfig cfg, string good, SettlementId? at = null)
    {
        int id = cfg.Goods!.IdOf(good);
        long sum = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == id && (at is null || w.GoodStocks[i].Settlement == at.Value))
                sum += w.GoodStocks[i].Amount.Value;
        return sum;
    }

    public static GoodStockRow Row(IReadOnlyWorldState w, SimConfig cfg, string good, SettlementId at)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == id && w.GoodStocks[i].Settlement == at) return w.GoodStocks[i];
        throw new InvalidOperationException($"no {good} row at settlement {at.Value}");
    }

    /// <summary>The ledger's running total for (good, reason), sourced or sunk.</summary>
    public static long Flow(IReadOnlyWorldState w, SimConfig cfg, string good, ReasonId reason, bool sunk)
    {
        var q = ConservedQuantityIds.OfGood(new GoodId(cfg.Goods!.IdOf(good)));
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == q && w.LedgerFlows[i].Reason == reason)
                return sunk ? w.LedgerFlows[i].TotalSunk : w.LedgerFlows[i].TotalSourced;
        return 0;
    }

    /// <summary>ADR-056: the manifest key a session recorded on <paramref name="founded"/> carries — the shipped packets when the
    /// world holds their stock rows (founded with them on), null when it does not (a pre-packet world).</summary>
    public static string? RecordedPackets(IReadOnlyWorldState founded)
    {
        SimConfig shipped = TestConfigs.Sim();
        for (int i = 0; i < founded.GoodStocks.Count; i++)
        {
            int g = founded.GoodStocks[i].Good.Value;
            if (shipped.Goods!.Contains(g) && shipped.Goods.ById(g).Packet is not null)
                return SessionManifest.EnabledEconomyPackets(shipped.Goods);
        }
        return null;
    }

    /// <summary>The canonical production pipeline over <paramref name="cfg"/>.</summary>
    public static TurnExecutor Canonical(SimConfig cfg, WorldgenConfig? wg = null)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg ?? TestConfigs.Worldgen())));
    }

    /// <summary>Every settlement of <paramref name="w"/> set to the given sector weights (raw; normalised by the consumer).</summary>
    public static WorldState Allocate(WorldState w, double farm, double herd, double extract, double craft, double build)
    {
        w.SectorAllocations.Clear();
        for (int s = 0; s < w.Settlements.Count; s++)
            w.SectorAllocations.Add(new SectorAllocationRow(w.Settlements[s].Id, farm, herd, extract, craft, build));
        return w;
    }
}
