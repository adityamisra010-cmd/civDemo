using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 × ADR-056 §13 (the M7 merge) — A RECRUITMENT BILL NAMING AN ECONOMY-PACKET GOOD, BOTH DIRECTIONS. unit-families.json
/// lists the EC-1..EC-3 goods as plannedGoods under the names goods.json gives them. With the packet ON the loader resolves
/// the bill to the real good id: the identity is recruitable once its knowledge is met and the good is stocked, and the good
/// is paid through the Ledger (a Recruitment sink, exact). With the packet OFF — at load (goods.json's switch) or after
/// loading (EconomyKnowledge.WithoutEconomy / WithGoods, every twin) — the input falls back to PLANNED and the identity is
/// blocked, naming the good and its packet.
/// </summary>
public class EconomyRecruitmentTests
{
    private static readonly SimConfig Off = TestConfigs.PreEconomyTrack(Cfg);

    private static RecruitCheck CheckUnder(SimConfig cfg, IReadOnlyWorldState w, SettlementId s, string identity) =>
        RecruitmentQuery.CheckRecruit(w, cfg, RecruitmentQuery.ViewOf(w), Player, s, Id(identity).Key);

    [Fact]
    public void TheIronBill_ResolvesToTheRealGood_WithThePacketOn_AndFallsBackToPlanned_WithItOff()
    {
        UnitRecruitInput on = Id("iron_infantry").Recruitment!.Inputs.Single(x => x.Good == "iron");
        Assert.Equal((Cfg.Goods!.IdOf("iron"), (string?)null, false), (on.GoodId, on.PlannedPacket, on.IsPlanned));
        Assert.True(on.GoodId >= 0);

        UnitRecruitInput off = Off.UnitFamilies!.IdentityById("iron_infantry")!.Recruitment!.Inputs.Single(x => x.Good == "iron");
        Assert.Equal((-1, "EC-1", true), (off.GoodId, off.PlannedPacket, off.IsPlanned));
        Assert.Equal(-1, Off.Goods!.IdOf("iron"));

        // The same fallback at LOAD: goods.json with ec1 disabled loads no iron, and the unit families loaded against it plan it.
        GoodsConfig loadedOff = GoodsConfigLoader.Load(TestConfigs.GoodsJson().Replace("\"id\": \"ec1\",\n      \"enabled\": true", "\"id\": \"ec1\",\n      \"enabled\": false", StringComparison.Ordinal));
        Assert.False(loadedOff.IsPacketEnabled("ec1"));
        UnitFamilyContent atLoad;
        using (var s = Sim.Data.DataFiles.OpenUnitFamilies()) atLoad = UnitFamilyContentLoader.Load(s, Research, loadedOff);
        UnitRecruitInput loaded = atLoad.IdentityById("iron_infantry")!.Recruitment!.Inputs.Single(x => x.Good == "iron");
        Assert.Equal((-1, "EC-1"), (loaded.GoodId, loaded.PlannedPacket));

        // Only the switched-off packet's goods move: a base good keeps its id, an untouched identity is the same instance, and
        // with EC-2 and EC-3 off (EC-1 on) iron stays resolved while gunpowder falls back to planned under EC-3.
        UnitRecruitInput hides = Off.UnitFamilies.IdentityById("iron_infantry")!.Recruitment!.Inputs.Single(x => x.Good == "hides");
        Assert.Equal(Cfg.Goods.IdOf("hides"), hides.GoodId);
        Assert.Same(Cfg.UnitFamilies!.IdentityById("bronze_swordsmen"), Off.UnitFamilies.IdentityById("bronze_swordsmen"));
        UnitFamilyContent ec1Only = TestConfigs.PreEc2(TestConfigs.PreEc3(Cfg)).UnitFamilies!;
        Assert.Equal(on.GoodId, ec1Only.IdentityById("iron_infantry")!.Recruitment!.Inputs.Single(x => x.Good == "iron").GoodId);
        UnitRecruitInput powder = ec1Only.IdentityById("rangers")!.Recruitment!.Inputs.Single(x => x.Good == "gunpowder");
        Assert.Equal((-1, "EC-3"), (powder.GoodId, powder.PlannedPacket));
    }

    [Fact]
    public void PacketOff_TheIronIdentityIsBlocked_NamingTheGoodAndItsPacket()
    {
        WorldState w = World(Off);
        SettlementId cap = Capital(w);
        Know(w, "iron_bloomery");
        foreach (string g in new[] { "hides", "timber", "bronze" }) EndowUnder(Off, w, cap, g, 500);

        RecruitCheck iron = CheckUnder(Off, w, cap, "iron_infantry");
        Assert.Equal(RecruitVerdict.BlockedByMissingGood, iron.Verdict);
        Assert.Equal("needs iron, which does not exist yet (EC-1)", iron.Reason);

        // The order is refused atomically: nothing is recruited, nothing sunk.
        var log = new OrderLog();
        log.Append(Recruit(0, cap, "iron_infantry"));
        WorldState w1 = MilitaryOnly(log, cfg: Off).Step(w);
        Assert.Equal(w.MilitaryUnits.Count, w1.MilitaryUnits.Count);
        Assert.True(ConservationAuditor.IsConserved(w1, Off.Goods, out string report), report);
    }

    [Fact]
    public void PacketOn_TheIronIdentity_IsRecruitableWhenKnownAndStocked_AndTheIronIsPaidThroughTheLedger()
    {
        WorldState w = World();
        SettlementId cap = Capital(w);
        Know(w, "iron_bloomery");
        Endow(w, cap, "hides", 500);

        // Known, the packet on, no iron held: short of materials — no longer a missing good.
        Assert.Equal(RecruitVerdict.MaterialsShort, Check(w, cap, "iron_infantry").Verdict);

        Endow(w, cap, "iron", 25);
        Assert.True(Check(w, cap, "iron_infantry").Ok, Check(w, cap, "iron_infantry").Reason);

        long people = People(w), adults = BandViews.Adults(w.Buckets, cap), ironBefore = StockAll(w, "iron"), hidesBefore = StockAll(w, "hides");
        var log = new OrderLog();
        log.Append(Recruit(0, cap, "iron_infantry"));
        WorldState w1 = MilitaryOnly(log).Step(w);

        int id = MaxUnitId(w) + 1;
        MilitaryUnitRow unit = Unit(w1, id);
        Assert.Equal((Player, Id("iron_infantry").Key, cap), (unit.Owner, unit.Identity, unit.Location));
        Assert.Equal(40L, Personnel(w1, id));
        Assert.Equal(adults - 40, BandViews.Adults(w1.Buckets, cap));
        Assert.Equal(people, People(w1));
        // The bill (iron 10, hides 5) is sunk through Ledger.Flow under Recruitment — exactly, and nowhere else.
        Assert.Equal((10L, 5L), (Sunk(w1, "iron", ReasonIds.Recruitment), Sunk(w1, "hides", ReasonIds.Recruitment)));
        Assert.Equal((ironBefore - 10, hidesBefore - 5), (StockAll(w1, "iron"), StockAll(w1, "hides")));
        Conserved(w1);
    }

    private static void EndowUnder(SimConfig cfg, WorldState w, SettlementId s, string good, long qty)
    {
        var id = new GoodId(cfg.Goods!.IdOf(good));
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        Assert.True(idx >= 0);
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id),
            ReasonIds.InitialEndowment, qty, FlowDirection.Source, OverdrawPolicy.Throw);
    }
}
