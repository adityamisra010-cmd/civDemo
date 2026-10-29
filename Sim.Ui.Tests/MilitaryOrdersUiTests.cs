using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-055 §9 — THE PLAYER'S MILITARY ORDERS THROUGH THE SESSION. UiSession.EmitRecruit / EmitReequip / EmitDisband build
/// their order with MilitaryOrderFactory, which asks THE predicates MilitarySystem applies (RecruitmentQuery.CheckRecruit /
/// CheckReequip), so the UI appends only an order the simulation would accept, at most one per target per turn; an order
/// stamped on turn t is in the world of turn t + 1 (turn-exact).
/// </summary>
public class MilitaryOrdersUiTests
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static SettlementId Capital(IReadOnlyWorldState w) =>
        EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c : throw new InvalidOperationException("no capital");

    private static int Warband(IReadOnlyWorldState w)
    {
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
            if (w.MilitaryUnits[i].Owner == Me) return w.MilitaryUnits[i].Id;
        throw new InvalidOperationException("no warband");
    }

    private static long PersonnelOf(IReadOnlyWorldState w, int unit)
    {
        for (int i = 0; i < w.FormationPersonnel.Count; i++)
            if (w.FormationPersonnel[i].Unit == unit) return w.FormationPersonnel[i].Count.Value;
        return 0;
    }

    /// <summary>The dev session's world with timber and stone at the capital (the founding store holds grain only).</summary>
    private static UiSession WithMaterials()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        WorldState w = s.World.Clone();
        SettlementId cap = Capital(w);
        foreach (string good in new[] { "timber", "stone" })
        {
            var g = new GoodId(s.Config.Goods!.IdOf(good));
            int idx = GoodStockIndex.IndexOf(w.GoodStocks, cap, g);
            Assert.True(idx >= 0);
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(g),
                ReasonIds.InitialEndowment, 100, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return UiSession.StartFrom(w, 42, 256, 4);
    }

    [Fact]
    public void Recruit_IsEmittedOnlyWhenThePredicateAdmitsIt_OncePerTurn_AndLandsInTheNextTurnsWorld()
    {
        UiSession bare = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int warbandKey = bare.Config.UnitFamilies!.FoundingIdentity.Key;
        Assert.False(bare.EmitRecruit(Capital(bare.World).Value, warbandKey));   // no timber or stone: the predicate refuses
        Assert.Empty(bare.QueuedOrders());

        UiSession s = WithMaterials();
        int cap = Capital(s.World).Value;
        long turn = s.World.Clock.Turn;
        int units = s.World.MilitaryUnits.Count;
        Assert.True(s.EmitRecruit(cap, warbandKey));
        Assert.False(s.EmitRecruit(cap, warbandKey));                            // already queued this turn
        OrderRecord order = Assert.Single(s.QueuedOrders());
        Assert.Equal((turn, OrderKind.RecruitFormation, cap, (double)warbandKey), (order.Turn, order.Kind, order.TargetId, order.Amount));
        Assert.Equal(units, s.World.MilitaryUnits.Count);                         // not before the step
        s.EndTurn();
        Assert.Equal(turn + 1, s.World.Clock.Turn);
        Assert.Equal(units + 1, s.World.MilitaryUnits.Count);                     // in the world of t + 1
        int recruited = s.World.MilitaryUnits[s.World.MilitaryUnits.Count - 1].Id;
        Assert.Equal(s.Config.UnitFamilies!.FoundingIdentity.Recruitment!.Personnel, PersonnelOf(s.World, recruited));   // its people, drawn
    }

    [Fact]
    public void Reequip_SameLineCatchUp_IsNeverEmitted_AndDisband_ReturnsThePeopleNextTurn()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int warband = Warband(s.World);
        int bronze = s.Config.UnitFamilies!.IdentityById("bronze_swordsmen")!.Key;
        Assert.False(s.EmitReequip(warband, bronze));                            // same family → mainline: OD-2, refused
        Assert.False(s.EmitCancelReequip(warband));                              // nothing pending
        Assert.Empty(s.QueuedOrders());

        Assert.True(s.EmitDisband(warband));
        Assert.False(s.EmitDisband(warband));                                    // once per turn
        Assert.True(PersonnelOf(s.World, warband) > 0);
        s.EndTurn();
        for (int i = 0; i < s.World.MilitaryUnits.Count; i++) Assert.NotEqual(warband, s.World.MilitaryUnits[i].Id);
        Assert.Equal(0, PersonnelOf(s.World, warband));
        bool logged = false;
        for (int i = 0; i < s.World.ReequipLog.Count; i++) logged |= s.World.ReequipLog[i].Unit == warband && s.World.ReequipLog[i].Cause == 3;
        Assert.True(logged, "the disbandment is not in the re-equipment log");
    }
}
