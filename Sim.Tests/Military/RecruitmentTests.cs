using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 §3–§4 — THE RECRUITMENT PREDICATE (unit charter §6.5) AND THE RECRUITFORMATION ORDER (kind 16): the bill and the
/// people move exactly (Ledger.Flow sinks, Ledger.Transfer of adults), the latest realizable identity in line order is the
/// recruitable one (material blockers fall back, never fail), a planned good blocks with its packet named, a family with no
/// live consumer is refused, the Age is never read, every refusal is atomic, and the formation appears in the state of the
/// turn after the order's stamp.
/// </summary>
public class RecruitmentTests
{
    [Fact]
    public void Recruit_SinksTheBill_TransfersTheAdults_AndAppendsTheFormation_Exactly()
    {
        WorldState w0 = Ready();
        SettlementId cap = Capital(w0);
        var log = new OrderLog();
        log.Append(Recruit(0, cap, "bronze_swordsmen"));
        WorldState w1 = MilitaryOnly(log).Step(w0);

        int id = MaxUnitId(w0) + 1;
        MilitaryUnitRow unit = Unit(w1, id);
        Assert.Equal((Player, Id("bronze_swordsmen").FamilyKey, Id("bronze_swordsmen").Key, cap, 0.0, 0),
            (unit.Owner, unit.Family, unit.Identity, unit.Location, unit.Experience, unit.Army));
        Assert.Equal(20L, Personnel(w1, id));
        Assert.Equal(BandViews.Adults(w0.Buckets, cap) - 20, BandViews.Adults(w1.Buckets, cap));
        Assert.Equal(People(w0), People(w1));                              // people moved, none created or lost
        Assert.Equal((10L, 5L, 5L), (Sunk(w1, "bronze", ReasonIds.Recruitment), Sunk(w1, "timber", ReasonIds.Recruitment), Sunk(w1, "hides", ReasonIds.Recruitment)));
        Assert.Equal(StockAll(w0, "bronze") - 10, StockAll(w1, "bronze"));
        Conserved(w1);
    }

    [Fact]
    public void Recruit_TurnExactDelivery_StampedTurnT_FirstInTheStateOfTPlusOne()
    {
        WorldState w0 = Ready();
        var log = new OrderLog();
        log.Append(Recruit(2, Capital(w0), "spearmen"));
        TurnExecutor ex = MilitaryOnly(log);
        WorldState w1 = ex.Step(w0), w2 = ex.Step(w1), w3 = ex.Step(w2);
        Assert.Equal(w0.MilitaryUnits.Count, w1.MilitaryUnits.Count);
        Assert.Equal(w0.MilitaryUnits.Count, w2.MilitaryUnits.Count);
        Assert.Equal(w0.MilitaryUnits.Count + 1, w3.MilitaryUnits.Count);
        Assert.Equal(Id("spearmen").Key, w3.MilitaryUnits[^1].Identity);
        Assert.Equal(3L, w3.Clock.Turn);
    }

    [Fact]
    public void LatestRealizable_IsTheRecruitableMainline_AndAMaterialBlockerFallsBack()
    {
        WorldState w = Ready();
        SettlementId cap = Capital(w);
        Assert.True(Check(w, cap, "bronze_swordsmen").Ok);
        RecruitCheck axe = Check(w, cap, "axe_warriors");
        Assert.Equal(RecruitVerdict.Superseded, axe.Verdict);
        Assert.Contains("Bronze-armed infantry", axe.Reason);
        Assert.Equal(RecruitVerdict.Superseded, Check(w, cap, "warband").Verdict);
        Assert.Equal(RecruitVerdict.Superseded, Check(w, cap, "slingers").Verdict);   // archers are later in the ranged line
        Assert.True(Check(w, cap, "archers").Ok);

        // No bronze anywhere: the bronze identity is short, so the axe warriors are the latest realizable again.
        Drain(w, "bronze");
        Assert.Equal(RecruitVerdict.MaterialsShort, Check(w, cap, "bronze_swordsmen").Verdict);
        Assert.True(Check(w, cap, "axe_warriors").Ok);
        Assert.Equal(RecruitVerdict.Superseded, Check(w, cap, "warband").Verdict);
    }

    [Fact]
    public void APlannedGood_BlocksWithItsPacketNamed_AndTheLineFallsBackToWhatExistingGoodsMake()
    {
        // ADR-056 §13 (the M7 merge): with EC-1 on, iron is a real good (EconomyRecruitmentTests); the planned-good path is
        // the world with the economy packets off, where the bill falls back to planned.
        SimConfig off = TestConfigs.PreEconomyTrack(Cfg);
        WorldState w = Ready(off);
        SettlementId cap = Capital(w);
        Know(w, "iron_bloomery", "standing_army");
        RecruitCheck iron = Check(w, cap, "iron_infantry", cfg: off);
        Assert.Equal(RecruitVerdict.BlockedByMissingGood, iron.Verdict);
        Assert.Equal("needs iron, which does not exist yet (EC-1)", iron.Reason);
        Assert.Equal(RecruitVerdict.BlockedByMissingGood, Check(w, cap, "legion", cfg: off).Verdict);
        Assert.True(Check(w, cap, "bronze_swordsmen", cfg: off).Ok);    // A4 knowledge, no iron: bronze-armed infantry (charter §6.5 item 2)
    }

    [Fact]
    public void Aviation_IsBlockedByEnginesAndFuel_EC4a_AndAMobileAgentFamilyHasNoLiveConsumer()
    {
        WorldState w = Ready();
        SettlementId cap = Capital(w);
        Know(w, "aircraft", "naval_ram", "rifled_artillery");
        Assert.Equal("needs engines, which does not exist yet (EC-4a)", Check(w, cap, "military_aircraft").Reason);
        Assert.Equal("needs engines, which does not exist yet (EC-4a)", Check(w, cap, "light_bombers").Reason);
        // ADR-056 §13: steel (EC-1) and explosives (EC-3) are real goods with the packets on — none held, so short of materials;
        // with the packets off they are planned again.
        Assert.Equal(RecruitVerdict.MaterialsShort, Check(w, cap, "anti_aircraft_artillery").Verdict);
        Assert.Equal(RecruitVerdict.BlockedByMissingGood, Check(w, cap, "anti_aircraft_artillery", cfg: TestConfigs.PreEconomyTrack(Cfg)).Verdict);
        Assert.Equal(RecruitVerdict.NoRuleSupport, Check(w, cap, "war_galleys").Verdict);                     // goods exist; no water edges
        Assert.Equal(RecruitVerdict.NotKnown, Check(w, cap, "jet_fighters").Verdict);
        Know(w, "rocket");
        Assert.Equal(RecruitVerdict.BlockedBySystem, Check(w, cap, "ballistic_missiles").Verdict);            // launch facility + M10 governance
    }

    [Fact]
    public void TheAgeIsNeverRead_EveryVerdictIsTheSameAtEveryAge()
    {
        WorldState w = Ready();
        Know(w, "iron_bloomery", "aircraft", "naval_ram", "crossbow");
        SettlementId cap = Capital(w);
        var baseline = new List<(int, RecruitVerdict, string)>();
        foreach (Sim.Core.Systems.Ages.UnitFamily f in Families.Families)
            foreach (Sim.Core.Systems.Ages.UnitIdentity i in f.Line)
            {
                RecruitCheck c = RecruitmentQuery.CheckRecruit(w, Cfg, RecruitmentQuery.ViewOf(w), Player, cap, i.Key);
                baseline.Add((i.Key, c.Verdict, c.Reason));
            }
        for (int age = 1; age <= 9; age++)
        {
            WorldState aged = w.Clone();
            aged.AgeStates.Clear();
            aged.AgeStates.Add(new AgeStateRow(Player, age, 0, 1, 0));
            int k = 0;
            foreach (Sim.Core.Systems.Ages.UnitFamily f in Families.Families)
                foreach (Sim.Core.Systems.Ages.UnitIdentity i in f.Line)
                {
                    RecruitCheck c = RecruitmentQuery.CheckRecruit(aged, Cfg, RecruitmentQuery.ViewOf(aged), Player, cap, i.Key);
                    Assert.Equal(baseline[k++], (i.Key, c.Verdict, c.Reason));
                }
        }
    }

    [Fact]
    public void EveryRefusal_IsAtomic_TheStepIsTheStepWithoutTheOrder()
    {
        WorldState ready = Ready();
        SettlementId cap = Capital(ready);
        SettlementId other = Other(ready);
        // (world mutation, order, expected verdict)
        var cases = new List<(Action<WorldState> Setup, OrderRecord Order, RecruitVerdict Verdict)>
        {
            (_ => { }, Recruit(0, cap, "axe_warriors"), RecruitVerdict.Superseded),
            (_ => { }, Recruit(0, cap, "phalanx"), RecruitVerdict.NotKnown),
            // ADR-056 §13: iron is a real good with EC-1 on (none held: short); engines (EC-4a) are still planned.
            (w => Know(w, "iron_bloomery"), Recruit(0, cap, "iron_infantry"), RecruitVerdict.MaterialsShort),
            (w => Know(w, "aircraft"), Recruit(0, cap, "military_aircraft"), RecruitVerdict.BlockedByMissingGood),
            (w => Know(w, "naval_ram"), Recruit(0, cap, "war_galleys"), RecruitVerdict.NoRuleSupport),
            (w => Drain(w, "bronze"), Recruit(0, cap, "bronze_swordsmen"), RecruitVerdict.MaterialsShort),
            (w => Drain(w, "grain"), Recruit(0, cap, "bronze_swordsmen"), RecruitVerdict.Unsupported),
            (w => LeaveAdults(w, other, 20), Recruit(0, other, "bronze_swordsmen"), RecruitVerdict.PersonnelShort),
            (_ => { }, OrderRecord.From(0, new PolityId(7), OrderKind.RecruitFormation, cap.Value, Id("spearmen").Key), RecruitVerdict.NotEmpire),
        };
        foreach ((Action<WorldState> setup, OrderRecord order, RecruitVerdict verdict) in cases)
        {
            WorldState w0 = ready.Clone();
            setup(w0);
            Assert.Equal(verdict, RecruitmentQuery.CheckRecruit(w0, Cfg, RecruitmentQuery.ViewOf(w0), order.Actor,
                new SettlementId(order.TargetId), (int)order.Amount).Verdict);
            var log = new OrderLog();
            log.Append(order);
            WorldState with = MilitaryOnly(log).Step(w0);
            WorldState without = MilitaryOnly(new OrderLog()).Step(w0);
            Assert.True(WorldStates.StateEquals(with, without), verdict.ToString());
            Conserved(with);
        }
    }

    [Fact]
    public void OrdersApplyInLogOrder_EachAgainstWhatEarlierOnesLeft()
    {
        WorldState w0 = Ready();
        SettlementId cap = Capital(w0);
        Drain(w0, "bronze");
        Endow(w0, cap, "bronze", 15);                        // enough for one bronze formation (10), not two
        var log = new OrderLog();
        log.Append(Recruit(0, cap, "bronze_swordsmen"));
        log.Append(Recruit(0, cap, "bronze_swordsmen"));
        WorldState w1 = MilitaryOnly(log).Step(w0);
        Assert.Equal(w0.MilitaryUnits.Count + 1, w1.MilitaryUnits.Count);
        Assert.Equal(5L, StockAll(w1, "bronze"));
        Conserved(w1);
    }

    [Fact]
    public void TheMilitarySwitchOff_RefusesEveryOrder_AndTheSystemWritesNothing()
    {
        SimConfig off = TestConfigs.PreMilitary(Cfg);
        WorldState w0 = World(off);
        Assert.Equal(0, w0.FormationPersonnel.Count);         // the founding warband stays the ADR-031 token
        Know(w0, "hafting");
        SettlementId cap = Capital(w0);
        foreach (string g in new[] { "timber", "stone" }) Endow(w0, cap, g, 100);
        Assert.Equal(RecruitVerdict.Disabled, RecruitmentQuery.CheckRecruit(w0, off, RecruitmentQuery.ViewOf(w0), Player, cap, Id("spearmen").Key).Verdict);
        var log = new OrderLog();
        log.Append(Recruit(0, cap, "spearmen"));
        log.Append(Disband(0, Warband(w0)));
        WorldState w1 = MilitaryOnly(log, cfg: off).Step(w0);
        WorldState w1Bare = MilitaryOnly(null, cfg: off).Step(w0);
        Assert.True(WorldStates.StateEquals(w1, w1Bare));
        Assert.True(WorldStates.TableEquals(w0.MilitaryUnits, w1.MilitaryUnits));
        Assert.True(WorldStates.TableEquals(w0.GoodStocks, w1.GoodStocks));
    }

    [Fact]
    public void AllocateDraw_IsProportional_Exact_AndTieBrokenByRowIndex()
    {
        WorldState w = World();
        SettlementId cap = Capital(w);
        long adults = BandViews.Adults(w.Buckets, cap);
        for (long qty = 0; qty <= Math.Min(adults, 97); qty++)
        {
            long[] draw = RecruitmentQuery.AllocateDraw(w.Buckets, cap, qty)!;
            long sum = 0;
            for (int i = 0; i < draw.Length; i++)
            {
                sum += draw[i];
                if (draw[i] == 0) continue;
                Assert.Equal(cap, w.Buckets[i].Settlement);
                Assert.True(BandViews.IsAdult(w.Buckets[i].CohortIdx));
                Assert.True(draw[i] <= w.Buckets[i].Count.Value);
            }
            Assert.Equal(qty, sum);
        }
        Assert.Null(RecruitmentQuery.AllocateDraw(w.Buckets, cap, adults + 1));
        // Deterministic: the same call twice is the same array.
        Assert.Equal(RecruitmentQuery.AllocateDraw(w.Buckets, cap, 13), RecruitmentQuery.AllocateDraw(w.Buckets, cap, 13));
    }

    /// <summary>Leaves exactly <paramref name="keep"/> adults at <paramref name="s"/> (the rest sink as Deaths through the Ledger).</summary>
    internal static void LeaveAdults(WorldState w, SettlementId s, long keep)
    {
        var ledger = new Ledger(w.LedgerFlows);
        long adults = BandViews.Adults(w.Buckets, s);
        long remove = adults - keep;
        for (int i = 0; i < w.Buckets.Count && remove > 0; i++)
        {
            if (w.Buckets[i].Settlement != s || !BandViews.IsAdult(w.Buckets[i].CohortIdx)) continue;
            long take = Math.Min(remove, w.Buckets[i].Count.Value);
            ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.Deaths, take, FlowDirection.Sink, OverdrawPolicy.Throw);
            remove -= take;
        }
        Assert.Equal(keep, BandViews.Adults(w.Buckets, s));
    }
}
