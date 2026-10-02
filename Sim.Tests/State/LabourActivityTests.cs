using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// ADR-033 D1 — the labour surface reports what each sector PRODUCES, and that report must be what
/// ProductionSystem actually does with the sector's labour (LabourActivities.GoodsOf mirrors production's
/// classification; production reads no research). Measured here against a real step of the founded world.
/// </summary>
public class LabourActivityTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly PolityId Player = new(1);

    private static TurnExecutor Production()
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(Cfg, TestConfigs.Worldgen())), null);
    }

    [Fact]
    public void TheGoodsEachSectorIsSaidToProduce_AreExactlyWhereProductionPutsItsOutput()
    {
        // Two steps: the first harvest needs the previous turn's catchment, and crafting needs inputs in stock.
        WorldState w = Production().Run(WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42), 2);
        WorldState next = Production().Step(w);
        GoodsConfig goods = Cfg.Goods!;
        int checkedGoods = 0;
        foreach (SettlementId s in LabourActivities.ControlledSettlements(w, Player))
        {
            // Which sector lists each good (a good is listed by at most one sector).
            var sectorOf = new int[goods.Goods.Max(g => g.Id) + 1];
            Array.Fill(sectorOf, -1);
            for (int sector = 0; sector < Sectors.Count; sector++)
                foreach (GoodId g in LabourActivities.GoodsOf(w, goods, s, sector).Goods)
                {
                    Assert.Equal(-1, sectorOf[g.Value]);
                    sectorOf[g.Value] = sector;
                }
            // Every good production credited this step is listed under SOME sector of the settlement...
            for (int i = 0; i < next.GoodStocks.Count; i++)
            {
                GoodStockRow row = next.GoodStocks[i];
                if (row.Settlement != s || row.LastProducedUnits <= 0) continue;
                Assert.True(sectorOf[row.Good.Value] >= 0, $"settlement {s.Value} produced good {row.Good.Value}, which no sector lists");
                checkedGoods++;
            }
            // ...and the deposit sectors list exactly the deposits with abundance > 0 (production's split).
            foreach (int sector in new[] { Sectors.Herding, Sectors.Extraction })
                foreach (GoodId g in LabourActivities.GoodsOf(w, goods, s, sector).Goods)
                    Assert.Contains(w.Deposits.Count > 0 ? Enumerable.Range(0, w.Deposits.Count).Select(i => w.Deposits[i]) : [],
                        d => d.Settlement == s && d.Good == g && d.Abundance > 0.0);
            Assert.Equal([new GoodId(goods.GrainId)], LabourActivities.GoodsOf(w, goods, s, Sectors.Farming).Goods.ToArray());
            Assert.Empty(LabourActivities.GoodsOf(w, goods, s, Sectors.Construction).Goods);
        }
        Assert.True(checkedGoods > 0, "no production observed — the agreement check would be vacuous");
    }

    [Fact]
    public void ShareAndAllocation_AreTheRowProductionReads_TheDefaultUntilAnOrderLands()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42);
        SettlementId s0 = w.Settlements[0].Id;
        Assert.Equal(Sectors.Default(s0), LabourActivities.AllocationOf(w, s0));
        Assert.Equal(0.55, LabourActivities.Of(w, Cfg, Player, s0, Sectors.Farming)!.Share, 12);
        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, Player, OrderKind.SectorAllocation, LabourActivities.PackTarget(s0, Sectors.Crafting), 100.0));
        WorldState next = new TurnExecutor(ResearchRigs.FlatEra(10.0), [SystemCatalog.PathBuild(Cfg)], orders).Step(w);
        Assert.Equal(Sectors.Share(LabourActivities.AllocationOf(next, s0), Sectors.Crafting),
            LabourActivities.Of(next, Cfg, Player, s0, Sectors.Crafting)!.Share);
        Assert.True(LabourActivities.Of(next, Cfg, Player, s0, Sectors.Crafting)!.Share > 0.5);
        Assert.Equal(s0.Value * 8 + Sectors.Crafting, LabourActivities.Of(next, Cfg, Player, s0, Sectors.Crafting)!.ActionId);
    }
}
