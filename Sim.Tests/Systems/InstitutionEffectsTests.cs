using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.UniversityRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D6 — the medical effect (one per-settlement mortality seam, bounded, saturating, diffusing by travel
/// cost), its null arm (no medical university: demographics bit-identical), the action-surface contributor
/// (listed only when AVAILABLE, one predicate with ConstructionSystem, blockers for viability / materials /
/// capacity, never also listed as a generic construction project) and the InstitutionsQuery read surface.
/// </summary>
public class InstitutionEffectsTests
{
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> SoloAt3 = new(() =>
        Production(Cfg()).Run(WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg(), 42), 3));

    private static int Medical => TypeKey(Research, "medical_university");
    private static int Engineering => TypeKey(Research, "engineering_university");

    private static double Distance(IReadOnlyWorldState w, SettlementId from, SettlementId to)
    {
        for (int i = 0; i < w.SettlementDistances.Count; i++)
            if (w.SettlementDistances[i].From == from && w.SettlementDistances[i].To == to) return w.SettlementDistances[i].TravelCost;
        return double.PositiveInfinity;
    }

    // ------------------------------------------------------------------ health

    [Fact]
    public void Medical_MortalityMultiplier_IsBoundedSaturating_StrongestAtTheSite_AndDecaysWithTravelCost()
    {
        SimConfig cfg = Cfg();
        UniversitiesConfig u = cfg.Institutions!.Universities;
        WorldState w = SoloAt3.Value.Clone();
        SettlementId site = w.Settlements[0].Id;
        Assert.True(w.SettlementDistances.Count > 0, "the rig needs travel costs");
        foreach (SettlementId s in Ids(w)) Assert.Equal(1.0, InstitutionEffects.MortalityMultiplier(w, cfg, s));   // none: exactly 1

        Found(w, Player, site, Medical, 1.0);
        Assert.Equal(1.0 - u.MaxMortalityReduction * (1.0 - Math.Exp(-1.0)), InstitutionEffects.MortalityMultiplier(w, cfg, site));
        // Diffusion: every other settlement gets M × e^(−d/25), so its multiplier is nearer 1, in travel-cost order.
        var byDistance = new List<(double D, double Mu)>();
        foreach (SettlementId s in Ids(w))
        {
            if (s == site) continue;
            double d = Distance(w, site, s);
            double expected = 1.0 - u.MaxMortalityReduction * (1.0 - Math.Exp(-Math.Exp(-d / u.HealthDecayCostUnits)));
            Assert.Equal(expected, InstitutionEffects.MortalityMultiplier(w, cfg, s));
            byDistance.Add((d, InstitutionEffects.MortalityMultiplier(w, cfg, s)));
        }
        byDistance.Sort((a, b) => a.D.CompareTo(b.D));
        for (int i = 1; i < byDistance.Count; i++) Assert.True(byDistance[i].Mu >= byDistance[i - 1].Mu);
        Assert.True(InstitutionEffects.MortalityMultiplier(w, cfg, site) < byDistance[0].Mu);

        // Bounded and saturating: fifty mature medical universities at the site never cut mortality by more than
        // maxMortalityReduction; an ENGINEERING university heals no one.
        WorldState many = SoloAt3.Value.Clone();
        for (int k = 0; k < 50; k++) Found(many, Player, site, Medical, 1.0);
        double mu = InstitutionEffects.MortalityMultiplier(many, cfg, site);
        Assert.True(mu >= 1.0 - u.MaxMortalityReduction && mu < 1.0 - u.MaxMortalityReduction * 0.999);
        WorldState engineering = SoloAt3.Value.Clone();
        Found(engineering, Player, site, Engineering, 1.0);
        Assert.Equal(1.0, InstitutionEffects.MortalityMultiplier(engineering, cfg, site));
    }

    [Fact]
    public void Medical_LowersDeathsThroughDemographics_AndWithoutOne_DemographicsAreBitIdentical()
    {
        SimConfig cfg = Cfg();
        WorldState w = SoloAt3.Value.Clone();
        SettlementId site = w.Settlements[0].Id;
        SystemRegistration[] demographics = [SystemCatalog.Demographics(cfg)];

        // Null arm: a NON-medical university (staff and all) leaves demographics bit for bit unchanged — the seam
        // returns the literal 1.0 and m × 1.0 == m.
        WorldState plain = w.Clone(), engineering = w.Clone();
        Found(engineering, Player, site, Engineering, 1.0);
        WorldState a = Only(new OrderLog(), 10.0, demographics).Step(plain);
        WorldState b = Only(new OrderLog(), 10.0, demographics).Step(engineering);
        Assert.True(WorldStates.TableEquals(a.Buckets, b.Buckets));
        Assert.True(WorldStates.TableEquals(a.SettlementVitals, b.SettlementVitals));
        Assert.True(WorldStates.TableEquals(a.LedgerFlows, b.LedgerFlows));

        // A mature medical university: fewer deaths at its site (and the people are all accounted for by the
        // Ledger — births and deaths, nothing else); the farthest settlement gains least.
        WorldState healed = w.Clone();
        Found(healed, Player, site, Medical, 1.0);
        WorldState c = Only(new OrderLog(), 10.0, demographics).Step(healed);
        Assert.True(Deaths(c, site) < Deaths(a, site), $"deaths must fall at the site ({Deaths(c, site)} vs {Deaths(a, site)})");
        Assert.True(Population(c, site) > Population(a, site));
        foreach (SettlementId s in Ids(w)) Assert.True(Deaths(c, s) <= Deaths(a, s));
    }

    // ------------------------------------------------------------------ the action surface

    [Fact]
    public void AvailableActions_ListsAFoundingOnlyWhereAvailable_WithItsBlockers_AndNeverAsAGenericProject()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 100);
        WorldState w = Production(cfg).Run(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42), 3);
        var context = new ActionQueryContext(NextDtYears: 10.0);
        // LOCKED: no institution action at all.
        Assert.DoesNotContain(AvailableActionsQuery.For(w, cfg, Player, context), a => a.Domain == ActionDomain.Institutions);

        Grant(w, Research, Player);
        var actions = AvailableActionsQuery.For(w, cfg, Player, context);
        ActionDescriptor[] found = actions.Where(a => a.Domain == ActionDomain.Institutions).ToArray();
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, Player);
        Assert.Equal(mine.Length * Research.UniversityTypes.Count, found.Length);       // every (own settlement, type)
        foreach (ActionDescriptor a in found)
        {
            var settlement = new SettlementId((int)a.Targets[0].Id);
            int project = (int)a.Targets[1].Id;
            Assert.Equal(OrderKind.EnqueueConstruction, a.Order);
            Assert.True(ConstructionQuery.IsProjectAvailable(w, cfg, Player, settlement, project));    // one predicate
            Assert.Contains(settlement, mine);
            Assert.Equal(((long)settlement.Value << 32) | (uint)project, a.Id);
            Assert.Equal(["building.university", "inst.university"], a.Provenance.Entities.ToArray());
            Assert.True(a.Provenance.Researched);
            Assert.Equal(ConstructionQuery.Blocker(w, cfg, settlement, cfg.Goods!.ProjectById(project)!, 10.0), a.Blocker);
        }
        // The rival's settlements are never offered to the player, and no university is a generic construction.
        Assert.DoesNotContain(actions, a => a.Domain == ActionDomain.Construction && cfg.Goods!.ProjectById((int)a.Targets[1].Id) is { Founds: not null });

        // The blockers name the failing term: materials and capacity at the default share, viability first.
        ActionDescriptor engineering = found.First(a => a.Key.EndsWith(".engineering_university", StringComparison.Ordinal));
        Assert.Contains("1800 timber", engineering.Blocker);
        SettlementId at = new((int)engineering.Targets[0].Id);
        Surplus(w, at, 1.0);
        Assert.StartsWith("needs a food surplus ratio of 1.30 (has 1.00)",
            AvailableActionsQuery.For(w, cfg, Player, context).First(a => a.Key == engineering.Key).Blocker);
        Surplus(w, at, 2.0);
        Materials(w, cfg, at);
        Construction(w, at, 0.5);
        Assert.Null(AvailableActionsQuery.For(w, cfg, Player, context).First(a => a.Key == engineering.Key).Blocker);
        Assert.Contains("Engineering", engineering.Detail);
    }

    // ------------------------------------------------------------------ InstitutionsQuery

    [Fact]
    public void InstitutionsQuery_ReportsInstancesSpecialtiesHealthAndLifecycle_FromTheSystemsOwnFunctions()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 100);
        WorldState w = SoloAt3.Value.Clone();
        SettlementId site = w.Settlements[0].Id, other = w.Settlements[1].Id;
        Grant(w, Research, Player);
        Surplus(w, site, 2.0);
        w.Structures.Add(new StructureRow(site, InstitutionContent.ProjectOfType(cfg, Medical)!.Id, 1));
        Found(w, Player, site, Medical, 0.6);
        WorldState next = Only(new OrderLog(), 10.0, SystemCatalog.Institutions(cfg)).Step(w);

        InstitutionInstanceView view = Assert.Single(InstitutionsQuery.Instances(next, cfg, Player));
        Assert.Equal((Medical, "medical_university", "Medical University", site), (view.TypeKey, view.TypeId, view.TypeName, view.Settlement));
        Assert.Equal(InstitutionEffects.Grow(0.6, 10.0, 16.0), view.Maturity);
        Assert.Equal(InstitutionLifecycle.Active, view.Stage);
        Assert.True(view.Viable && view.ViabilityBlocker is null && view.Heals);
        Assert.Equal(cfg.Institutions!.Universities.StaffShareAtMaturity * cfg.Institutions.Universities.AdultsPerUniversity * view.Maturity, view.Staff, 12);
        Assert.Equal(view.Staff, InstitutionsQuery.StaffAt(next, cfg, site), 9);
        Assert.Empty(InstitutionsQuery.Instances(next, cfg, Rival));
        Assert.Single(InstitutionsQuery.At(next, cfg, site));
        Assert.Empty(InstitutionsQuery.At(next, cfg, other));

        UniversitySpecialtyView medical = Assert.Single(InstitutionsQuery.Specialties(next, cfg, Player), v => v.TypeKey == Medical);
        Assert.Equal((1, SaturationReading.Diminishing, true), (medical.Count, medical.Reading, medical.Heals));
        Assert.Equal(next.ResearchCostModifiers[0].Factor, medical.ResearchFactor);     // the stored factor research uses
        Assert.Equal(InstitutionEffects.ResearchFactor(cfg.Institutions.Universities, view.Maturity), medical.ResearchFactor);

        MedicalCoverageView health = InstitutionsQuery.Health(next, cfg, site);
        Assert.Equal(view.Maturity, health.Coverage);
        Assert.Equal(InstitutionEffects.MortalityMultiplier(next, cfg, site), health.MortalityMultiplier);

        Assert.Equal(InstitutionLifecycle.Active, InstitutionsQuery.Lifecycle(next, cfg, Player, site, Medical));
        Assert.Equal(InstitutionLifecycle.Available, InstitutionsQuery.Lifecycle(next, cfg, Player, other, Medical));
        Assert.Equal(InstitutionLifecycle.Locked, InstitutionsQuery.Lifecycle(next, cfg, Rival, other, Medical));

        // The sustaining reading and its reason (a famine at the host).
        Surplus(next, site, 0.5);
        InstitutionInstanceView starving = Assert.Single(InstitutionsQuery.Instances(next, cfg, Player));
        Assert.False(starving.Viable);
        Assert.Equal("needs a food surplus ratio of 1.10 (has 0.50)", starving.ViabilityBlocker);
    }

    private static SettlementId[] Ids(WorldState w)
    {
        var ids = new SettlementId[w.Settlements.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = w.Settlements[i].Id;
        return ids;
    }

    private static long Deaths(WorldState w, SettlementId s)
    {
        for (int i = 0; i < w.SettlementVitals.Count; i++) if (w.SettlementVitals[i].Settlement == s) return w.SettlementVitals[i].Deaths;
        return 0;
    }

    private static long Population(WorldState w, SettlementId s)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) if (w.Buckets[i].Settlement == s) n += w.Buckets[i].Count.Value;
        return n;
    }
}
