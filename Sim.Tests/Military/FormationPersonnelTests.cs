using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 §4 (Q7) — FORMATION PERSONNEL: the founding warband draws its people from the capital's adult buckets by
/// Ledger.Transfer (the switch-off twin keeps the ADR-031 token byte for byte); the auditor counts formation people in the
/// population total and escrowed goods in their good's total; a formation's people eat a per-person-year ration × dtYears
/// from the purse (exact at three turn lengths, the sub-unit carry banked, the unfed recorded); and the full pipeline with
/// recruitments, re-equipments, cancels and disbandments conserves exactly every turn, replays and resumes from a save.
/// </summary>
public class FormationPersonnelTests
{
    [Fact]
    public void TheFoundingWarband_DrawsItsPeopleFromTheCapital_AndTheSwitchOffTwinIsTheToken()
    {
        WorldState on = World(aiEmpires: 1);
        WorldState off = World(TestConfigs.PreMilitary(Cfg), aiEmpires: 1);
        Assert.Equal(0, off.FormationPersonnel.Count);
        Assert.Equal(on.MilitaryUnits.Count, on.FormationPersonnel.Count);
        Assert.True(WorldStates.TableEquals(on.MilitaryUnits, off.MilitaryUnits));
        for (int i = 0; i < on.FormationPersonnel.Count; i++)
        {
            FormationPersonnelRow p = on.FormationPersonnel[i];
            MilitaryUnitRow u = Unit(on, p.Unit);
            Assert.Equal(Families.FoundingIdentity.Recruitment!.Personnel, p.Count.Value);
            Assert.Equal(BandViews.Adults(off.Buckets, u.Location) - 20, BandViews.Adults(on.Buckets, u.Location));
        }
        Assert.Equal(People(off), People(on));                      // moved, not created: the founding endowment is unchanged
        Assert.True(WorldStates.TableEquals(on.LedgerFlows, off.LedgerFlows));   // a transfer records no flow
        Conserved(on);
        // The twin's world is the pre-ADR-055 world plus four empty count prefixes.
        // (ADR-056 §13: the twin keeps the economy packets' inert rows; SchemaProjection.V37 strips them first, so the direct
        // route reads the same stripped world.)
        Assert.Equal(SchemaProjection.V37(off), WorldHash.ComputeHexAsSchema(EconomyProjection.StripShipped(off), 37));
    }

    [Fact]
    public void TheAuditor_CountsFormationPeopleAndEscrowedGoods_AndCatchesALeak()
    {
        WorldState w = Ready();
        Conserved(w);
        // A personnel row's people are in the population audit: sinking them without the Ledger's flow is caught.
        WorldState leak = w.Clone();
        new Ledger(new Table<LedgerFlowRow>()).Flow(ref leak.FormationPersonnel.Ref(0).Count, ConservedQuantityIds.Population,
            ReasonIds.Deaths, 1, FlowDirection.Sink, OverdrawPolicy.Throw);   // the flow is recorded on a throwaway table
        Assert.False(ConservationAuditor.IsConserved(leak, Cfg.Goods, out string report));
        Assert.Contains("population", report);
        Assert.Equal(People(w), ConservationAuditor.AuditQuantity(w, ConservedQuantityIds.Population).StockTotal);
    }

    [Theory]
    [InlineData(10.0, 200)]   // 20 people × 1 grain per person-year × 10 years
    [InlineData(0.5, 10)]
    [InlineData(0.25, 5)]
    public void Upkeep_IsPersonnelTimesRateTimesDtYears_Exactly(double dtYears, long perStep)
    {
        WorldState w = World();
        TurnExecutor ex = MilitaryOnly(null, dtYears);
        long sunk = 0;
        for (int t = 1; t <= 4; t++)
        {
            w = ex.Step(w);
            long now = Sunk(w, "grain", ReasonIds.FormationUpkeep);
            Assert.Equal(perStep, now - sunk);
            sunk = now;
        }
        Assert.Equal(0.0, w.FormationPersonnel[0].UpkeepRemainder);
        Conserved(w);
    }

    [Fact]
    public void Upkeep_BanksTheSubUnitRemainder_AndRecordsWhatThePurseCannotSupply()
    {
        WorldState w = World();
        TurnExecutor ex = MilitaryOnly(null, dtYears: 0.025);   // 9 days: 20 × 0.025 = 0.5 grain a step
        w = ex.Step(w);
        Assert.Equal(0L, Sunk(w, "grain", ReasonIds.FormationUpkeep));
        Assert.Equal(0.5, w.FormationPersonnel[0].UpkeepRemainder);
        w = ex.Step(w);
        Assert.Equal(1L, Sunk(w, "grain", ReasonIds.FormationUpkeep));
        Assert.Equal(0.0, w.FormationPersonnel[0].UpkeepRemainder);

        WorldState hungry = World();
        Drain(hungry, "grain");
        hungry = MilitaryOnly(null, 10.0).Step(hungry);
        Assert.Equal(200L, hungry.FormationPersonnel[0].UnfedUnits);
        Assert.Equal(20L, hungry.FormationPersonnel[0].Count.Value);   // no consumer yet: nobody starves or deserts (M7.4)
        Conserved(hungry);
    }

    /// <summary>The production pipeline over the shipped config, with a fixed order script for the player — recruit, re-equip,
    /// cancel, re-equip again, disband — on the dev world.</summary>
    private static (WorldState Final, string[] Hashes) Scripted(int turns, OrderLog log, WorldState? from = null)
    {
        WorldState w = from ?? ScriptStart();
        TurnExecutor ex = FoundingRigs.Pipeline(Cfg, log, TestConfigs.DevWorldgen());
        var hashes = new string[turns];
        for (int t = 0; t < turns; t++)
        {
            w = ex.Step(w);
            Conserved(w);
            hashes[t] = WorldHash.ComputeHex(w);
        }
        return (w, hashes);
    }

    private static WorldState ScriptStart()
    {
        WorldState w = World();
        Know(w, "hafting", "sling", "bow_simple", "arsenical_bronze", "crossbow");
        foreach (string g in new[] { "timber", "stone", "hides", "fiber", "bronze" }) Endow(w, Capital(w), g, 400);
        return w;
    }

    private static OrderLog Script(WorldState w)
    {
        SettlementId cap = Capital(w), other = Other(w);
        int warband = Warband(w), next = MaxUnitId(w) + 1;
        var log = new OrderLog();
        log.Append(Recruit(1, cap, "bronze_swordsmen"));
        log.Append(Recruit(1, other, "crossbowmen"));     // crossbows known: the latest realizable ranged identity
        log.Append(Reequip(3, warband, "crossbowmen"));
        log.Append(CancelReequip(3, warband));                // same step as the accept: the first re-equip order of a formation wins, so it is ignored
        log.Append(CancelReequip(4, warband));
        log.Append(Reequip(6, next, "spearmen"));
        log.Append(Disband(9, next + 1));
        log.Append(Recruit(10, cap, "spearmen"));
        return log;
    }

    [Fact]
    public void TheFullPipeline_ConservesEveryTurn_Replays_AndResumesFromASave()
    {
        WorldState start = ScriptStart();
        OrderLog log = Script(start);
        (WorldState a, string[] ha) = Scripted(16, log);
        (WorldState b, string[] hb) = Scripted(16, log);
        Assert.Equal(ha, hb);                                   // replay: the same log, the same world every turn
        string trace = string.Join(" | ", Rows(a.ReequipLog).Select(r => $"t{r.Turn} u{r.Unit} {r.FromIdentity}->{r.ToIdentity} c{r.Cause}"))
            + " || units " + string.Join(",", Rows(a.MilitaryUnits).Select(u => $"{u.Id}:{u.Identity}@{u.Location.Value}"));
        Assert.True(a.ReequipLog.Count >= 3, trace);            // the cancel, the completed role change, the disbandment
        Assert.Equal([2, 1, 3], Rows(a.ReequipLog).Select(r => r.Cause).Take(3));

        // Resume from a save at turn 8 (a world holding formations recruited by order): bit-identical thereafter.
        (WorldState mid, _) = Scripted(8, log);
        WorldState loaded = ReequipTests.RoundTrip(mid);
        (WorldState resumed, string[] hr) = Scripted(8, log, loaded);
        Assert.Equal(ha[^1], hr[^1]);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(resumed));
    }
}
