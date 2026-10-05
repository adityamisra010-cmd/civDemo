using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// F1 (2026-10-05, Director §5/§6; docs/d049 §13) — STATE CAPACITY OFFSETS THE LEVY. The M5 port's collection rule is
/// kept: a weakly reached settlement YIELDS less (effective rate = declared × reach). But the burden FELT per unit
/// collected rises as capacity falls — arbitrary, unpredictable collection by agents the centre does not control — so
/// at the same declared rate, and at the same effective rate, a weakly administered settlement accumulates MORE
/// pressure than a well administered one. Before F1 the opposite held (felt ∝ declared × reach).
/// </summary>
public class TaxStateCapacityTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();

    private static (WorldState W, PolityId P, SettlementId Other) Rig()
    {
        (WorldState w, PolityId p) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, p);
        SettlementId seat = GovernanceRigs.Seat(w, p);
        SettlementId other = w.Settlements[0].Id == seat ? w.Settlements[1].Id : w.Settlements[0].Id;
        return (w, p, other);
    }

    private static void Levy(WorldState w, PolityId p, double rate)
    {
        for (int i = 0; i < w.TaxPolicies.Count; i++)
            if (w.TaxPolicies[i].Polity == p) { w.TaxPolicies[i] = new TaxPolicyRow(p, rate); return; }
        w.TaxPolicies.Add(new TaxPolicyRow(p, rate));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AtTheSameDECLAREDRate_WeakerReach_FeelsMore_WhileItYieldsLess(bool granary)
    {
        (WorldState w, PolityId p, SettlementId other) = Rig();
        if (granary) w.Structures.Add(new StructureRow(other, 1, 1));
        Levy(w, p, 1.0);
        double prevFelt = -1.0, prevEffective = 2.0;
        foreach (double reach in new[] { 1.0, 0.75, 0.5, 0.25 })
        {
            GovernanceRigs.SetStrength(w, p, other, reach);
            double felt = Unrest.FeltBurden(w, other, Cfg, 0.5);
            double effective = Governance.EffectiveTaxRate(w, other, Cfg);
            Assert.True(felt > prevFelt, $"reach {reach}: felt {felt} not above {prevFelt}");
            Assert.True(effective < prevEffective, $"reach {reach}: the collection rule changed ({effective})");
            prevFelt = felt; prevEffective = effective;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AtTheSameEFFECTIVERate_WeakerReach_FeelsMore(bool granary)
    {
        (WorldState w, PolityId p, SettlementId other) = Rig();
        if (granary) w.Structures.Add(new StructureRow(other, 1, 1));
        Levy(w, p, 0.5);
        GovernanceRigs.SetStrength(w, p, other, 1.0);
        double strongEffective = Governance.EffectiveTaxRate(w, other, Cfg);
        double strong = Unrest.FeltBurden(w, other, Cfg, 0.5);
        Levy(w, p, 1.0);
        GovernanceRigs.SetStrength(w, p, other, 0.5);
        Assert.Equal(strongEffective, Governance.EffectiveTaxRate(w, other, Cfg));
        double weak = Unrest.FeltBurden(w, other, Cfg, 0.5);
        Assert.True(weak > strong, $"equal effective rate: weak capacity felt {weak}, strong {strong}");
    }

    /// <summary>At full reach (the capital) the capacity term is EXACTLY inert: felt = declared × the provision and
    /// service offsets, bit for bit the H2 reading, so a capital's numbers do not move.</summary>
    [Fact]
    public void AtFullReach_TheCapacityTermIsExactlyInert()
    {
        (WorldState w, PolityId p, _) = Rig();
        SettlementId seat = GovernanceRigs.Seat(w, p);
        Levy(w, p, 0.7);
        double m = Cfg.Needs!.Unrest!.TaxBurdenOffsetMax;
        Assert.Equal(1.0, Governance.ControlStrength(w, p, seat));
        Assert.Equal(0.7 * (1.0 - m * 0.4), Unrest.FeltBurden(w, seat, Cfg, 0.4));
    }

    /// <summary>On the full pipeline: the same declared levy accumulates MORE levy grievance in a weakly reached
    /// settlement with the capacity offset than without it (taxCapacityOffsetMax = 0, the H2 reading).</summary>
    [Fact]
    public void OnThePipeline_TheWeaklyReachedSettlement_AccumulatesMorePressure_WithTheCapacityOffset()
    {
        SimConfig without = Cfg with { Needs = Cfg.Needs! with { Unrest = Cfg.Needs!.Unrest! with { TaxCapacityOffsetMax = 0.0 } } };
        Assert.True(Cfg.Needs!.Unrest!.TaxCapacityOffsetMax > 0.0);
        (double withT, double reach) = Run(Cfg);
        (double withoutT, _) = Run(without);
        Assert.True(reach < 1.0, $"the rig's settlement is fully reached ({reach})");
        Assert.True(withT > withoutT, $"capacity offset did not add pressure: {withT} vs {withoutT}");
    }

    private static (double T, double Reach) Run(SimConfig cfg)
    {
        (WorldState w, PolityId p) = GovernanceRigs.Founded();
        TestConfigs.KnowRecipes(w, cfg);
        GovernanceRigs.Grant(w, p);
        var orders = new OrderLog();
        orders.Append(Governance.TaxOrder(0, p, 70.0));
        TurnExecutor ex = UniversityRigs.Production(cfg, orders);
        for (int t = 0; t < 6; t++) w = ex.Step(w);
        // The least-reached settlement the player still holds.
        SettlementId weakest = default; double reach = 2.0;
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            SettlementId s = w.Settlements[i].Id;
            if (!EmpireQuery.ControlsSettlement(w, p, s)) continue;
            double r = Governance.ControlStrength(w, p, s);
            if (r < reach || (r == reach && s.Value < weakest.Value)) { reach = r; weakest = s; }
        }
        return (Unrest.TaxGrievance(w, weakest, cfg), reach);
    }
}
