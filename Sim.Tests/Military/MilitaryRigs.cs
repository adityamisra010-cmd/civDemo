using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Military;

/// <summary>ADR-055 — rigs for the recruitment slice: a founded DEV world (256², 4 settlements) under the SHIPPED config
/// (military on), knowledge granted by ResearchCompleted rows, goods endowed by Ledger sources (never by a bare write), and
/// executors of MilitarySystem alone or of the Age systems with it.</summary>
internal static class MilitaryRigs
{
    internal static readonly SimConfig Cfg = TestConfigs.Sim();
    internal static readonly PolityId Player = new(1);
    internal static UnitFamilyContent Families => Cfg.UnitFamilies!;
    internal static ResearchContent Research => Cfg.Research!;

    internal static UnitIdentity Id(string id) => Families.IdentityById(id) ?? throw new ArgumentException(id);

    /// <summary>A founded dev world (the warband of each Empire manned at founding).</summary>
    internal static WorldState World(SimConfig? cfg = null, int aiEmpires = 0, int settlements = 4) =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = aiEmpires }, cfg ?? Cfg, 42, settlements);

    internal static SettlementId Capital(IReadOnlyWorldState w, PolityId? p = null)
    {
        Assert.True(EmpireQuery.TryGetCapital(w, p ?? Player, out SettlementId c));
        return c;
    }

    /// <summary>A controlled settlement of <paramref name="p"/> other than its capital, or the capital when it has one only.</summary>
    internal static SettlementId Other(IReadOnlyWorldState w, PolityId? p = null)
    {
        SettlementId cap = Capital(w, p);
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].Id != cap && EmpireQuery.ControlsSettlement(w, p ?? Player, w.Settlements[i].Id)) return w.Settlements[i].Id;
        return cap;
    }

    internal static void Know(WorldState w, params string[] nodeIds) => KnowFor(w, Player, nodeIds);

    internal static void KnowFor(WorldState w, PolityId p, params string[] nodeIds)
    {
        foreach (string id in nodeIds)
        {
            int index = Research.IndexOfId(id);
            Assert.True(index >= 0, id);
            ResearchNodeId key = Research.Nodes[index].Key;
            if (!ResearchQuery.IsCompleted(w, p, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(p, key));
        }
    }

    internal static int Good(string name) => Cfg.Goods!.IdOf(name) is int id and >= 0 ? id : throw new ArgumentException(name);

    /// <summary>Adds <paramref name="qty"/> of <paramref name="good"/> to <paramref name="s"/>'s stock through a Ledger source.</summary>
    internal static void Endow(WorldState w, SettlementId s, string good, long qty)
    {
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(Good(good)));
        Assert.True(idx >= 0);
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(new GoodId(Good(good))),
            ReasonIds.InitialEndowment, qty, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    /// <summary>Removes every unit of <paramref name="good"/> held anywhere (a Ledger sink).</summary>
    internal static void Drain(WorldState w, string good)
    {
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == Good(good) && w.GoodStocks[i].Amount.Value > 0)
                ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(new GoodId(Good(good))), ReasonIds.Eaten,
                    w.GoodStocks[i].Amount.Value, FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    internal static long Stock(IReadOnlyWorldState w, SettlementId s, string good)
    {
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(Good(good)));
        return idx < 0 ? 0 : w.GoodStocks[idx].Amount.Value;
    }

    internal static long StockAll(IReadOnlyWorldState w, string good)
    {
        long total = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) if (w.GoodStocks[i].Good.Value == Good(good)) total += w.GoodStocks[i].Amount.Value;
        return total;
    }

    internal static long Sunk(IReadOnlyWorldState w, string good, ReasonId reason)
    {
        for (int i = 0; i < w.LedgerFlows.Count; i++)
            if (w.LedgerFlows[i].Quantity == ConservedQuantityIds.OfGood(new GoodId(Good(good))) && w.LedgerFlows[i].Reason == reason)
                return w.LedgerFlows[i].TotalSunk;
        return 0;
    }

    internal static long People(IReadOnlyWorldState w)
    {
        long total = 0;
        for (int i = 0; i < w.Buckets.Count; i++) total += w.Buckets[i].Count.Value;
        for (int i = 0; i < w.Notables.Count; i++) total += w.Notables[i].Count.Value;
        for (int i = 0; i < w.FormationPersonnel.Count; i++) total += w.FormationPersonnel[i].Count.Value;
        return total;
    }

    internal static T[] Rows<T>(IReadOnlyTable<T> table) where T : unmanaged
    {
        var rows = new T[table.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = table[i];
        return rows;
    }

    internal static long Personnel(IReadOnlyWorldState w, int unit) => RecruitmentQuery.PersonnelOf(w.FormationPersonnel, unit);

    internal static MilitaryUnitRow Unit(IReadOnlyWorldState w, int id) =>
        w.MilitaryUnits[RecruitmentQuery.UnitIndex(w.MilitaryUnits, id)];

    internal static int MaxUnitId(IReadOnlyWorldState w)
    {
        int max = 0;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) max = Math.Max(max, w.MilitaryUnits[i].Id);
        return max;
    }

    /// <summary>The player's founding warband id.</summary>
    internal static int Warband(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
            if (w.MilitaryUnits[i].Owner == Player) return w.MilitaryUnits[i].Id;
        throw new InvalidOperationException("no warband");
    }

    internal static TurnExecutor MilitaryOnly(OrderLog? orders, double dtYears = 10.0, SimConfig? cfg = null) =>
        new(ResearchRigs.FlatEra(dtYears), [SystemCatalog.Military(cfg ?? Cfg)], orders);

    internal static TurnExecutor AgeAndMilitary(OrderLog? orders, double dtYears = 10.0, SimConfig? cfg = null) =>
        new(ResearchRigs.FlatEra(dtYears),
            [SystemCatalog.AgeEligibility(cfg ?? Cfg), SystemCatalog.AgeTransition(cfg ?? Cfg), SystemCatalog.Military(cfg ?? Cfg)], orders);

    internal static OrderRecord Recruit(long turn, SettlementId s, string identity, PolityId? p = null) =>
        OrderRecord.From(turn, p ?? Player, OrderKind.RecruitFormation, s.Value, Id(identity).Key);

    internal static OrderRecord Reequip(long turn, int unit, string identity, PolityId? p = null) =>
        OrderRecord.From(turn, p ?? Player, OrderKind.ReequipFormation, unit, Id(identity).Key);

    internal static OrderRecord CancelReequip(long turn, int unit) =>
        OrderRecord.From(turn, Player, OrderKind.ReequipFormation, unit, 0.0);

    internal static OrderRecord Disband(long turn, int unit) =>
        OrderRecord.From(turn, Player, OrderKind.DisbandFormation, unit, 0.0);

    internal static RecruitCheck Check(IReadOnlyWorldState w, SettlementId s, string identity, PolityId? p = null, SimConfig? cfg = null) =>
        RecruitmentQuery.CheckRecruit(w, cfg ?? Cfg, RecruitmentQuery.ViewOf(w), p ?? Player, s, Id(identity).Key);

    internal static void Conserved(IReadOnlyWorldState w)
    {
        Assert.True(ConservationAuditor.IsConserved(w, Cfg.Goods, out string report), report);
    }

    /// <summary>A rig world ready to raise early formations at the capital: hafting, the sling, the simple bow and arsenical
    /// bronze known; timber, stone, hides, fiber and bronze endowed at the capital.</summary>
    internal static WorldState Ready(SimConfig? cfg = null)
    {
        WorldState w = World(cfg);
        Know(w, "hafting", "sling", "bow_simple", "arsenical_bronze");
        SettlementId cap = Capital(w);
        foreach (string g in new[] { "timber", "stone", "hides", "fiber", "bronze" }) Endow(w, cap, g, 500);
        return w;
    }
}
