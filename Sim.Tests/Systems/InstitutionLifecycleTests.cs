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
/// ADR-033 D6 — universities as real institutions on ADR-028's lifecycle (docs/institutions-universities.md):
/// LOCKED → AVAILABLE → UNDER_CONSTRUCTION → ACTIVE → MATURE through the real order, queue and systems, the
/// turn-exact founding timing, the viability gate, the dt-correct maturation, diminishing returns and the
/// derived saturation reading, staffing withdrawn from labour with people conserved, the research-cost seam
/// (matching-branch Technology only, floor and Eureka ceiling untouched), and save/load mid-maturation.
/// </summary>
public class InstitutionLifecycleTests
{
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42));
    private static WorldState Solo() => DevSolo.Value.Clone();

    private static int Engineering => TypeKey(Research, "engineering_university");
    private static int Medical => TypeKey(Research, "medical_university");
    private static int Military => TypeKey(Research, "military_university");

    private static SystemRegistration[] FoundingPipeline(SimConfig cfg) =>
        [SystemCatalog.Construction(cfg), SystemCatalog.Institutions(cfg)];

    // ------------------------------------------------------------------ lifecycle + turn-exact founding

    [Fact]
    public void Lifecycle_LockedAvailableUnderConstructionActiveMature_ThroughTheQueue_TurnExact()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 100);
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        ConstructionProjectEntry project = InstitutionContent.ProjectOfType(cfg, Engineering)!;
        string key = $"institutions.{s.Value}.engineering_university";

        // LOCKED: neither entity is knowledge-eligible — not listed, the order is refused.
        Assert.Equal(InstitutionLifecycle.Locked, InstitutionsQuery.Lifecycle(w, cfg, Player, s, Engineering));
        Assert.DoesNotContain(AvailableActionsQuery.For(w, cfg, Player), a => a.Key == key);

        // Only ONE of the two entities eligible is still LOCKED (both gate — the research stage's own reading).
        Grant(w, Research, Player, "cuneiform", "stamp_seal", "legal_code_roman");     // inst.university only
        Assert.Equal(ProjectAvailability.NotKnowledgeEligible, ConstructionQuery.Availability(w, cfg, Player, s, project.Id));
        Grant(w, Research, Player);                                                   // + building.university
        Assert.Equal(InstitutionLifecycle.Available, InstitutionsQuery.Lifecycle(w, cfg, Player, s, Engineering));
        ActionDescriptor found = Assert.Single(AvailableActionsQuery.For(w, cfg, Player, new ActionQueryContext(NextDtYears: 10.0)), a => a.Key == key);
        Assert.Equal("Found Engineering University", found.Label);
        Assert.NotNull(found.Blocker);                         // no surplus published, no stone: legal, not affordable

        Surplus(w, s, 2.0);
        Construction(w, s, 0.5);
        var log = new OrderLog();
        log.Append(ConstructionQuery.EnqueueOrder(w, Player, s, project.Id));        // stamped turn 0
        TurnExecutor exec = Only(log, 10.0, FoundingPipeline(cfg));

        // t1 — UNDER_CONSTRUCTION: queued; the head waits (materials short).
        WorldState t1 = exec.Step(w);
        Assert.Equal(1, t1.ConstructionQueue.Count);
        Assert.Equal(InstitutionLifecycle.UnderConstruction, InstitutionsQuery.Lifecycle(t1, cfg, Player, s, Engineering));
        Assert.Equal(0, t1.Institutions.Count);

        // t2 — built (Structures), labour published, NOT yet founded: still UNDER_CONSTRUCTION.
        Materials(t1, cfg, s);
        WorldState t2 = exec.Step(t1);
        Assert.Equal(0, t2.ConstructionQueue.Count);
        Assert.Equal(1L, ConstructionQuery.Built(t2, s, project.Id));
        Assert.Equal(new ConstructionLaborRow(s, project.LaborRequired), Assert.Single(Rows(t2.ConstructionLabor)));
        Assert.Equal(0, t2.Institutions.Count);
        Assert.Equal(InstitutionLifecycle.UnderConstruction, InstitutionsQuery.Lifecycle(t2, cfg, Player, s, Engineering));

        // t3 — FOUNDED by InstitutionsSystem: ACTIVE, maturity exactly 0, owner = the controller, no effect yet.
        WorldState t3 = exec.Step(t2);
        InstitutionRow row = Assert.Single(Rows(t3.Institutions));
        Assert.Equal(new InstitutionRow(1, Player, s, Engineering, 3, 0.0), row);
        Assert.Equal(InstitutionLifecycle.Active, InstitutionsQuery.Lifecycle(t3, cfg, Player, s, Engineering));
        Assert.Equal(0, t3.ResearchCostModifiers.Count);        // X = 0: no factor (no instant magic)

        // t4 — first maturation: the closed form, and the factor it implies is published.
        WorldState t4 = exec.Step(t3);
        double m4 = 1.0 - Math.Exp(-10.0 / 16.0);
        Assert.Equal(m4, t4.Institutions[0].Maturity);
        Assert.Equal(new ResearchCostModifierRow(Player, Engineering, 1.0 - cfg.Institutions!.Universities.MaxResearchCostReduction * (1.0 - Math.Exp(-m4))),
            Assert.Single(Rows(t4.ResearchCostModifiers)));

        // MATURE once M ≥ 0.95: 1 − e^(−10k/16) ≥ 0.95 first at k = 5 maturations (state t8).
        WorldState t7 = exec.Run(t4, 3), t8 = exec.Step(t7);
        Assert.True(t7.Institutions[0].Maturity < 0.95 && t8.Institutions[0].Maturity >= 0.95);
        Assert.Equal(InstitutionLifecycle.Active, InstitutionsQuery.Lifecycle(t7, cfg, Player, s, Engineering));
        Assert.Equal(InstitutionLifecycle.Mature, InstitutionsQuery.Lifecycle(t8, cfg, Player, s, Engineering));
    }

    [Fact]
    public void Founding_IsGatedByTheHostsFoundingViability_AtResolution_AndTheBlockerSaysWhy()
    {
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        long adults = BandViews.Adults(w.Buckets, s);
        Grant(w, Research, Player);
        Construction(w, s, 0.5);
        Materials(w, TestConfigs.Sim(), s);

        // MARKET: one university needs adultsPerUniversity adults; the host has fewer → the head WAITS.
        SimConfig big = Cfg(adultsPerUniversity: adults + 1);
        Surplus(w, s, 2.0);
        ConstructionProjectEntry project = InstitutionContent.ProjectOfType(big, Engineering)!;
        var log = new OrderLog();
        log.Append(ConstructionQuery.EnqueueOrder(w, Player, s, project.Id));
        WorldState waited = Only(log, 10.0, FoundingPipeline(big)).Step(w);
        Assert.Equal(1, waited.ConstructionQueue.Count);
        Assert.Equal(0L, ConstructionQuery.Built(waited, s, project.Id));
        string? blocker = ConstructionQuery.Blocker(w, big, s, project, 10.0);
        Assert.Equal($"needs {adults + 1} adults (has {adults})", blocker);

        // FOOD: the host's published surplus is below the founding threshold (1.3) → the head waits.
        SimConfig ok = Cfg(adultsPerUniversity: 100);
        Surplus(w, s, 1.29);
        WorldState hungry = Only(log, 10.0, FoundingPipeline(ok)).Step(w);
        Assert.Equal(0L, ConstructionQuery.Built(hungry, s, project.Id));
        Assert.Equal("needs a food surplus ratio of 1.30 (has 1.29)", ConstructionQuery.Blocker(w, ok, s, project, 10.0));

        // Viable → built.
        Surplus(w, s, 1.3);
        Assert.Null(ConstructionQuery.Blocker(w, ok, s, project, 10.0));
        Assert.Equal(1L, ConstructionQuery.Built(Only(log, 10.0, FoundingPipeline(ok)).Step(w), s, project.Id));
    }

    // ------------------------------------------------------------------ maturation, dt-correct

    [Fact]
    public void Maturation_IsTheClosedForm_TwoHalfStepsMatchOneFullStep_FixedPointsExact()
    {
        const double tau = 16.0, decay = 21.7;
        foreach (double m in new[] { 0.0, 0.1, 0.37, 0.5, 0.9 })
        {
            double full = InstitutionEffects.Grow(m, 10.0, tau);
            double halves = InstitutionEffects.Grow(InstitutionEffects.Grow(m, 5.0, tau), 5.0, tau);
            Assert.Equal(full, halves, 15);
            double dFull = InstitutionEffects.Decay(m, 10.0, decay);
            double dHalves = InstitutionEffects.Decay(InstitutionEffects.Decay(m, 5.0, decay), 5.0, decay);
            Assert.Equal(dFull, dHalves, 15);
        }
        // Exact where the math is exact.
        Assert.Equal(1.0, InstitutionEffects.Grow(1.0, 10.0, tau));          // a mature institution stays at 1
        Assert.Equal(0.0, InstitutionEffects.Decay(0.0, 10.0, decay));        // nothing decays from nothing
        Assert.Equal(1.0 - Math.Exp(-10.0 / tau), InstitutionEffects.Grow(0.0, 10.0, tau));
        Assert.Equal(0.5 * Math.Exp(-10.0 / decay), InstitutionEffects.Decay(0.5, 10.0, decay));
    }

    [Fact]
    public void Maturation_ThroughTheSystem_IsDtCorrect_AndDecaysWhenTheHostCannotSustainIt()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 100);
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        Grant(w, Research, Player);
        Surplus(w, s, 2.0);
        // A built-and-founded university (Structures + the row), mid-maturation.
        w.Structures.Add(new StructureRow(s, InstitutionContent.ProjectOfType(cfg, Engineering)!.Id, 1));
        Found(w, Player, s, Engineering, 0.37);

        SystemRegistration[] only = [SystemCatalog.Institutions(cfg)];
        WorldState ten = Only(new OrderLog(), 10.0, only).Step(w);
        WorldState fives = Only(new OrderLog(), 5.0, only).Run(w, 2);
        Assert.Equal(InstitutionEffects.Grow(0.37, 10.0, 16.0), ten.Institutions[0].Maturity);
        Assert.Equal(ten.Institutions[0].Maturity, fives.Institutions[0].Maturity, 15);   // dt-invariant (closed form)

        // Not viable to SUSTAIN (surplus below 1.1: hunger) → maturity DECAYS with its own τ.
        Surplus(w, s, 1.09);
        WorldState hungry = Only(new OrderLog(), 10.0, only).Step(w);
        Assert.Equal(0.37 * Math.Exp(-10.0 / 21.7), hungry.Institutions[0].Maturity);
        // ...and between 1.1 and 1.3 an established university keeps maturing (the hysteresis), though none
        // could be founded there.
        Surplus(w, s, 1.2);
        Assert.Equal(InstitutionEffects.Grow(0.37, 10.0, 16.0), Only(new OrderLog(), 10.0, only).Step(w).Institutions[0].Maturity);
        Assert.False(InstitutionViability.ToFound(w, cfg, s).Viable);
        // Population loss below the market (adults < 100 × hosted) → decays.
        SimConfig dear = Cfg(adultsPerUniversity: BandViews.Adults(w.Buckets, s) + 1);
        Assert.Equal(0.37 * Math.Exp(-10.0 / 21.7),
            Only(new OrderLog(), 10.0, [SystemCatalog.Institutions(dear)]).Step(w).Institutions[0].Maturity);
    }

    // ------------------------------------------------------------------ diminishing returns, saturation

    [Fact]
    public void ResearchEffect_HasDiminishingReturns_AndTheSaturationReadingIsDerived()
    {
        SimConfig cfg = Cfg();
        UniversitiesConfig u = cfg.Institutions!.Universities;
        double prevGain = double.PositiveInfinity, prevFactor = 1.0;
        for (int n = 1; n <= 6; n++)
        {
            double gain = InstitutionEffects.Saturation(n) - InstitutionEffects.Saturation(n - 1);
            Assert.True(gain > 0.0 && gain < prevGain, $"university {n} must add less than university {n - 1}");
            double factor = InstitutionEffects.ResearchFactor(u, n);
            Assert.True(factor < prevFactor && factor > 1.0 - u.MaxResearchCostReduction);
            Assert.Equal(u.MaxResearchCostReduction * InstitutionEffects.NextMarginal(n - 1), prevFactor - factor, 15);
            (prevGain, prevFactor) = (gain, factor);
        }

        // Through the system: three mature engineering universities of the player — the published factor is the
        // curve at X = 3, and the reading is derived from it (never a flag).
        WorldState w = Solo();
        SettlementId[] sites = [w.Settlements[0].Id, w.Settlements[1].Id, w.Settlements[2].Id];
        UniversitySpecialtyView none = Assert.Single(InstitutionsQuery.Specialties(w, cfg, Player), v => v.TypeKey == Engineering);
        Assert.Equal((0, SaturationReading.None, 1.0), (none.Count, none.Reading, none.ResearchFactor));
        foreach (SettlementId site in sites)
        {
            Found(w, Player, site, Engineering, 1.0);
            Surplus(w, site, 2.0);                // viable: a mature institution stays exactly at 1
        }
        WorldState next = Only(new OrderLog(), 10.0, SystemCatalog.Institutions(cfg)).Step(w);
        Assert.Equal(new ResearchCostModifierRow(Player, Engineering, InstitutionEffects.ResearchFactor(u, 3.0)),
            Assert.Single(Rows(next.ResearchCostModifiers)));
        UniversitySpecialtyView three = Assert.Single(InstitutionsQuery.Specialties(next, cfg, Player), v => v.TypeKey == Engineering);
        Assert.Equal(3, three.Count);
        Assert.Equal(SaturationReading.Saturated, three.Reading);             // S(3) = 0.9502 ≥ 0.95
        Assert.Equal(InstitutionEffects.ResearchFactor(u, 3.0), three.ResearchFactor);
        Assert.Equal(SaturationReading.Diminishing,
            Assert.Single(InstitutionsQuery.Specialties(Only(new OrderLog(), 10.0, SystemCatalog.Institutions(cfg)).Step(Two(cfg)), cfg, Player),
                v => v.TypeKey == Engineering).Reading);                    // S(2) = 0.8647 < 0.95
    }

    private static WorldState Two(SimConfig cfg)
    {
        _ = cfg;
        WorldState w = Solo();
        foreach (SettlementId site in new[] { w.Settlements[0].Id, w.Settlements[1].Id })
        {
            Found(w, Player, site, Engineering, 1.0);
            Surplus(w, site, 2.0);
        }
        return w;
    }

    // ------------------------------------------------------------------ staffing, people conserved

    [Fact]
    public void Staffing_IsWithdrawnFromEveryLabourPool_AndNoPersonIsCreatedOrDestroyed()
    {
        SimConfig cfg = Cfg();
        WorldState w = Production(cfg).Run(Solo(), 3);            // published surplus, catchments, stocks
        SettlementId s = w.Settlements[0].Id;
        long adults = BandViews.Adults(w.Buckets, s);
        Assert.Equal((double)adults, InstitutionStaffing.LabourAdults(w, cfg, s));   // no institution: the raw count

        // Two NON-medical specialties: a medical university would (correctly) change the step's deaths through
        // the health seam, which is not what this test isolates.
        WorldState staffed = w.Clone();
        Found(staffed, Player, s, Engineering, 1.0);
        Found(staffed, Player, s, Military, 0.5);
        // Per INSTANCE: σ × adultsPerUniversity × M each (σ = 0.05 of the 100-adult rig market: 5 adults at
        // maturity), never a share of the host — so the host's size does not scale the staff.
        double sigma = cfg.Institutions!.Universities.StaffShareAtMaturity;
        long market = cfg.Institutions.Universities.AdultsPerUniversity;
        double staff = InstitutionStaffing.Staff(staffed, cfg, s);
        Assert.Equal(sigma * market * 1.0 + sigma * market * 0.5, staff, 12);
        Assert.True(staff > 0.0 && staff < adults);
        Assert.Equal(adults - staff, InstitutionStaffing.LabourAdults(staffed, cfg, s));
        Assert.Equal((double)adults, InstitutionStaffing.LabourAdults(staffed, cfg, s) + staff, 9);   // staff + labour = adults
        // Other settlements are untouched.
        Assert.Equal((double)BandViews.Adults(w.Buckets, w.Settlements[1].Id), InstitutionStaffing.LabourAdults(staffed, cfg, w.Settlements[1].Id));

        // One full production-pipeline step from each: the staffed settlement's labour-bound output falls, and the
        // population — every bucket — is identical in both worlds: scholars are withdrawn from work, not from life.
        WorldState a = Production(cfg).Step(w), b = Production(cfg).Step(staffed);
        Assert.Equal(Total(a), Total(b));
        Assert.True(WorldStates.TableEquals(a.Buckets, b.Buckets));
        long outA = Output(a, s, cfg, "timber") + Output(a, s, cfg, "stone") + Output(a, s, cfg, "fiber") + Output(a, s, cfg, "hides");
        long outB = Output(b, s, cfg, "timber") + Output(b, s, cfg, "stone") + Output(b, s, cfg, "fiber") + Output(b, s, cfg, "hides");
        Assert.True(outB < outA, $"extraction output must fall with staff withdrawn ({outB} vs {outA})");
        Assert.InRange(outB, (long)Math.Floor(outA * (1.0 - staff / adults)) - 4, (long)Math.Ceiling(outA * (1.0 - staff / adults)) + 4);
        // The other settlements produce exactly as before.
        Assert.Equal(Output(a, w.Settlements[1].Id, cfg, "timber"), Output(b, w.Settlements[1].Id, cfg, "timber"));
    }

    // ------------------------------------------------------------------ the research seam

    [Fact]
    public void ResearchCost_FallsOnlyForTechnologyNodesOfTheMatchingBranch_NeverBelowTheFloor_EurekaCeilingUntouched()
    {
        SimConfig cfg = Cfg();
        WorldState w = Solo();
        Found(w, Player, w.Settlements[0].Id, Engineering, 1.0);
        Surplus(w, w.Settlements[0].Id, 2.0);     // sustained: maturity stays exactly 1
        WorldState next = Only(new OrderLog(), 10.0, SystemCatalog.Institutions(cfg)).Step(w);
        double f = InstitutionEffects.ResearchFactor(cfg.Institutions!.Universities, 1.0);
        int engineeringBranch = Research.UniversityTypes[Research.UniversityTypeIndexOf(Engineering)].Branch;
        int checkedOn = 0, untouched = 0;
        for (int i = 0; i < Research.Nodes.Count; i++)
        {
            ResearchNode n = Research.Nodes[i];
            double cost = ResearchQuery.EffectiveCost(next, Research, Player, i);
            if (n.Tree == ResearchTree.Technology && n.Branch == engineeringBranch)
            {
                Assert.Equal(n.BaseCost * f, cost);
                checkedOn++;
            }
            else
            {
                Assert.Equal(n.BaseCost, cost);       // trunk, other subtrees and Civics: no university term
                untouched++;
            }
            Assert.Equal(n.BaseCost, ResearchQuery.EffectiveCost(next, Research, Rival, i));   // another polity: none
        }
        Assert.True(checkedOn > 0 && untouched > 0);

        // The floor: an absurd stack (maxReduction 0.99, fifty mature universities) still costs 20 % of BASE.
        SimConfig absurd = Cfg(tune: u => u with { MaxResearchCostReduction = 0.99 });
        WorldState stack = Solo();
        for (int k = 0; k < 50; k++) Found(stack, Player, stack.Settlements[k % 4].Id, Engineering, 1.0);
        stack = Only(new OrderLog(), 10.0, SystemCatalog.Institutions(absurd)).Step(stack);
        int engineeringNode = -1;
        for (int i = 0; i < Research.Nodes.Count && engineeringNode < 0; i++)
            if (Research.Nodes[i].Tree == ResearchTree.Technology && Research.Nodes[i].Branch == engineeringBranch) engineeringNode = i;
        ResearchQuery.CostBreakdown breakdown = ResearchQuery.EffectiveCostBreakdown(stack, Research, Player, Research.Nodes[engineeringNode].Key);
        Assert.True(breakdown.FloorBinds);
        Assert.Equal(0.2 * Research.Nodes[engineeringNode].BaseCost, breakdown.EffectiveCost);
        Assert.True(breakdown.Terms.Single().Factor is > 0.0 and <= 1.0);   // the ADR-029 input contract holds
        // The Eureka ceiling is 40 % of BASE whatever the factor (the acceleration pool never reads it).
        Assert.Equal(0.4 * Research.Nodes[engineeringNode].BaseCost,
            ResearchQuery.AccelerationPoolOf(stack, Research, Player, Research.Nodes[engineeringNode].Key).Ceiling);
    }

    // ------------------------------------------------------------------ save/load mid-maturation

    [Fact]
    public void SaveLoad_MidMaturation_ContinuesHashIdentically()
    {
        SimConfig cfg = Cfg();
        WorldState w = Production(cfg).Run(Solo(), 3);
        SettlementId s = w.Settlements[0].Id;
        Grant(w, Research, Player);
        w.Structures.Add(new StructureRow(s, InstitutionContent.ProjectOfType(cfg, Medical)!.Id, 1));   // founded next step
        w.Structures.Add(new StructureRow(w.Settlements[2].Id, InstitutionContent.ProjectOfType(cfg, Engineering)!.Id, 1));

        TurnExecutor exec = Production(cfg);
        WorldState mid = exec.Run(w, 4);
        Assert.Equal(2, mid.Institutions.Count);
        Assert.True(mid.Institutions[0].Maturity is > 0.0 and < 0.95, "the save must be MID-maturation");
        using var ms = new MemoryStream();
        Snapshot.Save(mid, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms, mid.Terrain);
        Assert.Equal(WorldHash.ComputeHex(mid), WorldHash.ComputeHex(loaded));
        WorldState straight = mid, resumed = loaded;
        for (int t = 0; t < 6; t++)
        {
            straight = exec.Step(straight);
            resumed = Production(cfg).Step(resumed);
            Assert.Equal(WorldHash.ComputeHex(straight), WorldHash.ComputeHex(resumed));
        }
        Assert.True(straight.Institutions[0].Maturity > mid.Institutions[0].Maturity);
    }

    // ------------------------------------------------------------------ helpers

    private static T[] Rows<T>(Table<T> t) where T : unmanaged
    {
        var rows = new T[t.Count];
        for (int i = 0; i < t.Count; i++) rows[i] = t[i];
        return rows;
    }

    private static long Total(WorldState w)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) n += w.Buckets[i].Count.Value;
        return n;
    }

    private static long Output(WorldState w, SettlementId s, SimConfig cfg, string good)
    {
        int id = cfg.Goods!.IdOf(good);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good.Value == id) return w.GoodStocks[i].LastProducedUnits;
        return 0;
    }
}
