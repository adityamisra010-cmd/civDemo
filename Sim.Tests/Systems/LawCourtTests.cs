using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;
using static Sim.Tests.TestUtil.UniversityRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-057 (M6 CIV track; D-066 A1, A2) — THE LAW COURT, the slice's second institution type: law_code → the court
/// (inst.law_code, goods.json project 16) → Unrest.ServiceOffset. Pinned through the real order, queue and systems:
/// LOCKED while the civic is incomplete (refused — NotKnowledgeEligible — and an injected order changes nothing, the T15
/// pattern) → AVAILABLE once researched (listed exactly where the one predicate admits it) → built, then FOUNDED the step
/// after (turn-exact) → ACTIVE → MATURE by the court's own profile; its maturity enters ServiceOffset's X as a term of the
/// existing equation (weight 1 — a coefficient, not a bonus) and its staff are withdrawn from labour (the D-021 brake);
/// the switch-off twin (TestConfigs.PreLawCourt) does none of it; save/load mid-maturation and replay are hash-identical;
/// the AI founds it through the same predicate with an integer tie-break against the universities (tie-dense).
/// </summary>
public sealed class LawCourtTests
{
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);
    private static readonly Lazy<WorldState> DevSolo = new(() => WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42));
    private static WorldState Solo() => DevSolo.Value.Clone();

    /// <summary>The shipped config with the court's market lowered to <paramref name="market"/> adults (the dev world's
    /// settlements hold ~150–350 adults; tuning data, never invented people — the UniversityRigs precedent).</summary>
    internal static SimConfig CourtCfg(long market = 100)
    {
        SimConfig cfg = TestConfigs.Sim();
        InstitutionTypeConfig[] types = cfg.Institutions!.Types!;
        var tuned = new InstitutionTypeConfig[types.Length];
        for (int i = 0; i < types.Length; i++) tuned[i] = types[i].Id == "law_court" ? types[i] with { AdultsPerInstitution = market } : types[i];
        return cfg with { Institutions = cfg.Institutions with { Types = tuned } };
    }

    internal static ConstructionProjectEntry Court(SimConfig cfg)
    {
        foreach (ConstructionProjectEntry p in cfg.Goods!.Projects!) if (p.Founds?.InstitutionType == "law_court") return p;
        throw new InvalidOperationException("no law court project");
    }

    private static int CourtKey(SimConfig cfg) => InstitutionContent.TypeKeyOf(cfg, Court(cfg));

    private static SystemRegistration[] FoundingPipeline(SimConfig cfg) => [SystemCatalog.Construction(cfg), SystemCatalog.Institutions(cfg)];

    private static void Drain(WorldState w, SimConfig cfg, string good)
    {
        var id = new GoodId(cfg.Goods!.IdOf(good));
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good == id && w.GoodStocks[i].Amount.Value > 0)
                ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(id), ReasonIds.Eaten,
                    w.GoodStocks[i].Amount.Value, FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    private static void CourtMaterials(WorldState w, SimConfig cfg, SettlementId s)
    {
        foreach (ProjectInput input in Court(cfg).Inputs) TopUp(w, cfg, s, input.Good, input.Qty);
    }

    private static T[] Rows<T>(IReadOnlyTable<T> t) where T : unmanaged
    {
        var r = new T[t.Count];
        for (int i = 0; i < r.Length; i++) r[i] = t[i];
        return r;
    }

    // ================================================================== content

    [Fact]
    public void TheCourt_IsContentDriven_TypeSixOnItsOwnProfile_FoundedByProject16_GatedByTheCivic()
    {
        SimConfig cfg = TestConfigs.Sim();
        ConstructionProjectEntry court = Court(cfg);
        Assert.Equal(16, court.Id);
        Assert.Null(court.Entity);
        Assert.Equal("inst.law_code", court.Founds!.Entity);
        Assert.Equal(6, CourtKey(cfg));
        InstitutionTypeInfo info = InstitutionContent.Info(cfg, 6)!;
        Assert.Equal(("law_court", "Law Court", -1, false), (info.Id, info.Name, info.Branch, info.University));
        // inst.law_code requires the civic law_code — the knowledge half of the one availability predicate.
        ResearchContent rc = cfg.Research!;
        int e = rc.EntityIndexOf("inst.law_code");
        Assert.Equal([rc.IndexOfId("law_code")], rc.Entities[e].NodeAtoms);
        Assert.Equal(ResearchTree.Civics, rc.Nodes[rc.IndexOfId("law_code")].Tree);
        InstitutionProfile p = InstitutionContent.Profile(cfg.Institutions!, 6);
        Assert.Equal(new InstitutionProfile(16.0, 21.7, 0.95, 0.01, 1000, 1.3, 1.1, false), p);
        Assert.True(InstitutionContent.Profile(cfg.Institutions!, 1).University);
    }

    // ================================================================== lifecycle, turn-exact

    [Fact]
    public void Lifecycle_UnresearchedAvailableUnderConstructionActiveMature_ThroughTheQueue_TurnExact()
    {
        SimConfig cfg = CourtCfg(market: 100);
        int key = CourtKey(cfg);
        ConstructionProjectEntry court = Court(cfg);
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        string action = $"institutions.{s.Value}.law_court";

        // UNRESEARCHED: law_code incomplete — LOCKED, refused, not listed; the module absent, the label the base.
        Assert.NotEqual(CivicState.Researched, ResearchQuery.CivicStateOf(w, cfg.Research!, Player, cfg.Research!.Nodes[cfg.Research.IndexOfId("law_code")].Key));
        Assert.Equal(InstitutionLifecycle.Locked, InstitutionsQuery.Lifecycle(w, cfg, Player, s, key));
        Assert.Equal(ProjectAvailability.NotKnowledgeEligible, ConstructionQuery.Availability(w, cfg, Player, s, court.Id));
        Assert.DoesNotContain(AvailableActionsQuery.For(w, cfg, Player), a => a.Key == action);
        InstitutionTypeReading locked = InstitutionsQuery.TypeReading(w, cfg, Player, key)!;
        Assert.Equal((InstitutionLifecycle.Locked, "Unresearched - needs Written law"), (locked.Stage, locked.Line));
        Assert.Equal("chiefdom", RegimeQuery.Label(w, cfg, Player));
        Assert.False(Assert.Single(RegimeQuery.Modules(w, cfg, Player)).Present);

        // AVAILABLE: the civic researched — listed (the one predicate), still affordable only with materials and a surplus.
        Grant(w, cfg.Research!, Player, "law_code");
        Assert.Equal(InstitutionLifecycle.Available, InstitutionsQuery.Lifecycle(w, cfg, Player, s, key));
        Assert.Equal(InstitutionLifecycle.Available, InstitutionsQuery.TypeReading(w, cfg, Player, key)!.Stage);
        ActionDescriptor found = Assert.Single(AvailableActionsQuery.For(w, cfg, Player, new ActionQueryContext(NextDtYears: 10.0)), a => a.Key == action);
        Assert.Equal("Found Law Court", found.Label);
        Assert.Equal(OrderKind.EnqueueConstruction, found.Order);

        Drain(w, cfg, "timber");
        Drain(w, cfg, "stone");
        Surplus(w, s, 2.0);
        Construction(w, s, 0.5);
        var log = new OrderLog();
        log.Append(ConstructionQuery.EnqueueOrder(w, Player, s, court.Id));   // stamped turn 0
        TurnExecutor exec = Only(log, 10.0, FoundingPipeline(cfg));

        // t1 — UNDER CONSTRUCTION: queued; the head waits (no timber or stone anywhere).
        WorldState t1 = exec.Step(w);
        Assert.Equal(1, t1.ConstructionQueue.Count);
        Assert.Equal(InstitutionLifecycle.UnderConstruction, InstitutionsQuery.Lifecycle(t1, cfg, Player, s, key));
        Assert.Equal(InstitutionLifecycle.UnderConstruction, InstitutionsQuery.TypeReading(t1, cfg, Player, key)!.Stage);
        Assert.Equal(0, t1.Institutions.Count);

        // t2 — built (Structures), NOT yet founded.
        CourtMaterials(t1, cfg, s);
        WorldState t2 = exec.Step(t1);
        Assert.Equal(0, t2.ConstructionQueue.Count);
        Assert.Equal(1L, ConstructionQuery.Built(t2, s, court.Id));
        Assert.Equal(0, t2.Institutions.Count);
        Assert.Equal(InstitutionLifecycle.UnderConstruction, InstitutionsQuery.Lifecycle(t2, cfg, Player, s, key));

        // t3 — FOUNDED by InstitutionsSystem: ACTIVE, maturity exactly 0, type 6, owner the controller; no research factor
        // (the court has no branch); the regime module "codified law" is present and the derived label changes.
        WorldState t3 = exec.Step(t2);
        Assert.Equal(new InstitutionRow(1, Player, s, key, 3, 0.0), Assert.Single(Rows(t3.Institutions)));
        Assert.Equal(InstitutionLifecycle.Active, InstitutionsQuery.Lifecycle(t3, cfg, Player, s, key));
        Assert.Equal(0, t3.ResearchCostModifiers.Count);
        Assert.True(Assert.Single(RegimeQuery.Modules(t3, cfg, Player)).Present);
        Assert.Equal("law-giving chiefdom", RegimeQuery.Label(t3, cfg, Player));

        // t4 — the first maturation: the closed form on the COURT's τ (16 years), dt 10.
        WorldState t4 = exec.Step(t3);
        Assert.Equal(1.0 - Math.Exp(-10.0 / 16.0), t4.Institutions[0].Maturity);

        // MATURE at the court's matureAt (0.95): first at k = 5 maturations (state t8).
        WorldState t7 = exec.Run(t4, 3), t8 = exec.Step(t7);
        Assert.True(t7.Institutions[0].Maturity < 0.95 && t8.Institutions[0].Maturity >= 0.95);
        Assert.Equal(InstitutionLifecycle.Active, InstitutionsQuery.Lifecycle(t7, cfg, Player, s, key));
        Assert.Equal(InstitutionLifecycle.Mature, InstitutionsQuery.Lifecycle(t8, cfg, Player, s, key));
        Assert.StartsWith("Mature - 1 founded", InstitutionsQuery.TypeReading(t8, cfg, Player, key)!.Line);
    }

    // ================================================================== refusal and injection (the T15 pattern)

    /// <summary>With law_code incomplete the order is refused by authoritative acceptance (NotKnowledgeEligible): an order
    /// injected into the log leaves no queue entry, no structure and no institution — and the same injection once the civic
    /// is researched is accepted (the refusal is the civic's, not the order's).</summary>
    [Fact]
    public void Refused_WhileTheCivicIsIncomplete_AnInjectedOrderChangesNothing_AcceptedOnceResearched()
    {
        SimConfig cfg = CourtCfg(market: 100);
        ConstructionProjectEntry court = Court(cfg);
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        Surplus(w, s, 2.0);
        Construction(w, s, 0.5);
        CourtMaterials(w, cfg, s);
        Assert.False(ConstructionQuery.IsProjectAvailable(w, cfg, Player, s, court.Id));

        var log = new OrderLog();
        log.Append(ConstructionQuery.EnqueueOrder(w, Player, s, court.Id));   // injected: the UI never offered it
        TurnExecutor exec = Only(log, 10.0, FoundingPipeline(cfg));
        WorldState refused = exec.Run(w, 3);
        Assert.DoesNotContain(Rows(refused.ConstructionQueue), r => r.ProjectId == court.Id);
        Assert.Equal(0L, ConstructionQuery.Built(refused, s, court.Id));
        Assert.Equal(0, refused.Institutions.Count);

        WorldState knows = w.Clone();
        Grant(knows, cfg.Research!, Player, "law_code");
        Assert.True(ConstructionQuery.IsProjectAvailable(knows, cfg, Player, s, court.Id));
        WorldState accepted = Only(log, 10.0, FoundingPipeline(cfg)).Run(knows, 3);
        Assert.Equal(1L, ConstructionQuery.Built(accepted, s, court.Id));
        Assert.Equal(CourtKey(cfg), Assert.Single(Rows(accepted.Institutions)).Type);
    }

    // ================================================================== the effect: a term of ServiceOffset's X

    /// <summary>
    /// The court's maturity is a TERM of Unrest.ServiceOffset's X (V = reach × (1 − e^(−X))), weight 1, exactly as a
    /// university's: with one granary (X = 1) and a court of maturity m, V = reach × S(1 + m) bit for bit; without the court
    /// V = reach × S(1). Under a levy the felt burden is lower with the court (the offset's only reader), and with no levy it
    /// is 0 either way (the court is no free-floating bonus: it acts only inside the equation).
    /// </summary>
    [Fact]
    public void Effect_TheCourtsMaturityIsATermOfServiceOffset_AndLightensOnlyALevy()
    {
        SimConfig cfg = CourtCfg();
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        int granary = -1;
        foreach (ConstructionProjectEntry p in cfg.Goods!.Projects!) if (p.Name == "granary") granary = p.Id;
        w.Structures.Add(new StructureRow(s, granary, 1));
        double reach = Governance.ControlStrength(w, Player, s);
        Assert.True(reach > 0.0);
        double without = Unrest.ServiceOffset(w, s, cfg);
        Assert.Equal(reach * InstitutionEffects.Saturation(1.0), without);
        const double m = 0.6;
        WorldState with = w.Clone();
        Found(with, Player, s, CourtKey(cfg), m);
        double v = Unrest.ServiceOffset(with, s, cfg);
        Assert.Equal(reach * InstitutionEffects.Saturation(1.0 + m), v);
        Assert.True(v > without);

        Assert.Equal(0.0, Unrest.FeltBurden(w, s, cfg, 0.5));
        Assert.Equal(0.0, Unrest.FeltBurden(with, s, cfg, 0.5));
        w.TaxPolicies.Add(new TaxPolicyRow(Player, 0.3));
        with.TaxPolicies.Add(new TaxPolicyRow(Player, 0.3));
        Assert.True(Unrest.FeltBurden(with, s, cfg, 0.5) < Unrest.FeltBurden(w, s, cfg, 0.5));
    }

    /// <summary>The SWITCH-OFF TWIN (TestConfigs.PreLawCourt): the same world, knowledge and order. ON, the court is founded
    /// and matures and its settlement's ServiceOffset rises above the twin's; OFF, the order names no project (refused), no
    /// structure or institution exists, and the regime has no module and no label. Every difference is the court's.</summary>
    [Fact]
    public void SwitchOffTwin_NoCourt_NoTerm_AndTheServiceOffsetDifferenceIsTheCourtsAlone()
    {
        SimConfig on = CourtCfg(market: 100);
        SimConfig off = TestConfigs.PreLawCourt(on);
        Assert.Null(off.Goods!.ProjectById(16));
        Assert.Null(off.Institutions!.Types);
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        Grant(w, on.Research!, Player, "law_code");
        Surplus(w, s, 2.0);
        Construction(w, s, 0.5);
        CourtMaterials(w, on, s);
        var log = new OrderLog();
        log.Append(ConstructionQuery.EnqueueOrder(w, Player, s, 16));
        WorldState a = Only(log, 10.0, FoundingPipeline(on)).Run(w, 5);
        WorldState b = Only(log, 10.0, FoundingPipeline(off)).Run(w, 5);
        InstitutionRow row = Assert.Single(Rows(a.Institutions));
        Assert.True(row.Maturity > 0.0);
        Assert.Equal(0, b.Institutions.Count);
        Assert.Equal(0L, ConstructionQuery.Built(b, s, 16));
        Assert.Empty(RegimeQuery.Modules(b, off, Player));
        Assert.Equal("", RegimeQuery.Label(b, off, Player));
        double vOn = Unrest.ServiceOffset(a, s, on), vOff = Unrest.ServiceOffset(b, s, off);
        Assert.True(vOn > vOff);
        // The court's maturity is the whole difference: ON's offset without its row is OFF's (same reach, same works).
        WorldState stripped = a.Clone();
        stripped.Institutions.Clear();
        Assert.Equal(Governance.ControlStrength(b, Player, s), Governance.ControlStrength(a, Player, s));
        Assert.Equal(vOff, Unrest.ServiceOffset(stripped, s, on));
    }

    // ================================================================== D-021: the staffing brake

    [Fact]
    public void Staffing_AMatureCourtWithdrawsItsStaffFromLabour_OnItsOwnProfile()
    {
        SimConfig cfg = CourtCfg(market: 100);
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        long adults = BandViews.Adults(w.Buckets, s);
        Assert.Equal((double)adults, InstitutionStaffing.LabourAdults(w, cfg, s));
        Found(w, Player, s, CourtKey(cfg), 1.0);
        double staff = 0.01 * 100 * 1.0;   // staffShareAtMaturity × adultsPerInstitution × M
        Assert.Equal(staff, InstitutionStaffing.StaffOfInstitution(cfg, CourtKey(cfg), 1.0));
        Assert.Equal(staff, InstitutionStaffing.Staff(w, cfg, s));
        Assert.Equal(adults - staff, InstitutionStaffing.LabourAdults(w, cfg, s));
        Assert.Equal(adults, BandViews.Adults(w.Buckets, s));   // no person created or destroyed
        // a university of the same maturity withdraws the university profile's staff (σ × A_u) — per type.
        Assert.Equal(cfg.Institutions!.Universities.StaffShareAtMaturity * cfg.Institutions.Universities.AdultsPerUniversity,
            InstitutionStaffing.StaffOfInstitution(cfg, 1, 1.0));
    }

    /// <summary>The court's market is its own: universities in the host do not use up the court's market (and the court
    /// not theirs) — InstitutionContent.MarketOf: every university one market, the court its own.</summary>
    [Fact]
    public void Viability_TheCourtsMarketIsItsOwn_UniversitiesDoNotCountAgainstIt()
    {
        SimConfig cfg = CourtCfg(market: 100);
        WorldState w = Solo();
        SettlementId s = w.Settlements[0].Id;
        Surplus(w, s, 2.0);
        long adults = BandViews.Adults(w.Buckets, s);
        Assert.True(adults >= 200);
        ConstructionProjectEntry court = Court(cfg);
        ConstructionProjectEntry military = InstitutionContent.ProjectOfType(cfg, 1)!;
        w.Structures.Add(new StructureRow(s, military.Id, 5));   // five university buildings
        Assert.Equal(5L, InstitutionViability.Hosted(w, cfg, s));
        Assert.Equal(0L, InstitutionViability.HostedOfType(w, cfg, s, CourtKey(cfg)));
        UniversityViability v = InstitutionViability.ToFound(w, cfg, s, court);
        Assert.True(v.Viable);
        Assert.Equal(100L, v.AdultsNeeded);
        w.Structures.Add(new StructureRow(s, court.Id, 1));
        Assert.Equal(5L, InstitutionViability.Hosted(w, cfg, s));   // the court is not a university
        Assert.Equal(200L, InstitutionViability.ToFound(w, cfg, s, court).AdultsNeeded);
    }

    // ================================================================== persistence and replay

    [Fact]
    public void SaveLoad_MidMaturation_ContinuesHashIdentically_AndReplayIsTwinDeterministic()
    {
        SimConfig cfg = CourtCfg(market: 100);
        WorldState w = Production(cfg).Run(Solo(), 3);
        SettlementId s = w.Settlements[0].Id;
        Grant(w, cfg.Research!, Player, "law_code");
        w.Structures.Add(new StructureRow(s, 16, 1));   // the court's building stands: founded next step
        TurnExecutor exec = Production(cfg);
        WorldState mid = exec.Run(w, 3);
        InstitutionRow court = Assert.Single(Rows(mid.Institutions), r => r.Type == CourtKey(cfg));
        Assert.True(court.Maturity is > 0.0 and < 0.95, "the save must be MID-maturation");
        using var ms = new MemoryStream();
        Snapshot.Save(mid, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms, mid.Terrain);
        Assert.Equal(WorldHash.ComputeHex(mid), WorldHash.ComputeHex(loaded));
        WorldState straight = mid, resumed = loaded, twin = mid.Clone();
        TurnExecutor other = Production(cfg);
        for (int t = 0; t < 4; t++)
        {
            straight = exec.Step(straight);
            resumed = Production(cfg).Step(resumed);
            twin = other.Step(twin);
            Assert.Equal(WorldHash.ComputeHex(straight), WorldHash.ComputeHex(resumed));
            Assert.Equal(WorldHash.ComputeHex(straight), WorldHash.ComputeHex(twin));
        }
        Assert.True(Assert.Single(Rows(straight.Institutions), r => r.Type == CourtKey(cfg)).Maturity > court.Maturity);
    }

    // ================================================================== AI parity, tie-dense

    /// <summary>
    /// AI PARITY: the AI founds the court through the SAME predicate and order as the player (AiConstructionPolicy, the
    /// institution decision), choosing among EVERY content-driven type by the composite key (n ascending, X ascending, type
    /// key ascending). TIE-DENSE: with universities and the court all available at n = 0, X = 0 the lowest key (military,
    /// 1) wins; with every university at n = 1 the court (n = 0) wins; with the court also at n = 1 and BIT-EQUAL X against a
    /// university (both 0.5) the lower key — the university — wins; with the universities' X above the court's, the court.
    /// Before law_code nothing about the court is ordered.
    /// </summary>
    [Fact]
    public void Ai_FoundsTheCourtThroughTheSamePredicate_CompositeKeyWithIntegerTieBreak_TieDense()
    {
        SimConfig court10 = CourtCfg(market: 10);
        SimConfig cfg = court10 with { Institutions = court10.Institutions! with { Universities = court10.Institutions.Universities with { AdultsPerUniversity = 10 } } };
        WorldState w = Production(cfg).Run(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42), 3);
        Grant(w, cfg.Research!, Rival);   // the university knowledge
        SettlementId[] sites = LabourActivities.ControlledSettlements(w, Rival);
        int granary = -1;
        foreach (ConstructionProjectEntry p in cfg.Goods!.Projects!) if (p.Name == "granary") granary = p.Id;
        foreach (SettlementId s in sites)
        {
            Materials(w, cfg, s);
            CourtMaterials(w, cfg, s);
            Construction(w, s, 0.5);
            Surplus(w, s, 2.0);
            w.Structures.Add(new StructureRow(s, granary, 1));
        }
        double dt = w.Clock.DtYears;
        int courtKey = CourtKey(cfg), courtProject = Court(cfg).Id;

        // law_code unknown: the court is never chosen, whatever the universities' counts.
        WorldState noCivic = w.Clone();
        for (int t = 1; t <= 5; t++) Found(noCivic, Rival, sites[0], t, 0.5);
        Assert.NotEqual(courtProject, (int?)AiConstructionPolicy.University(noCivic, cfg, Rival, dt)?.Amount);

        Grant(w, cfg.Research!, Rival, "law_code");
        // all n = X = 0: the lowest key, 1 (a university), not the court (6).
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 1)!.Id, (int)AiConstructionPolicy.University(w, cfg, Rival, dt)!.Value.Amount);
        // every university at n = 1: the court (n = 0).
        WorldState unis = w.Clone();
        for (int t = 1; t <= 5; t++) Found(unis, Rival, sites[0], t, 0.5);
        OrderRecord courtOrder = AiConstructionPolicy.University(unis, cfg, Rival, dt)!.Value;
        Assert.Equal(courtProject, (int)courtOrder.Amount);
        Assert.Equal(ConstructionQuery.EnqueueOrder(unis, Rival, new SettlementId(courtOrder.TargetId), courtProject), courtOrder);   // the player's constructor
        Assert.True(ConstructionQuery.IsProjectAvailable(unis, cfg, Rival, new SettlementId(courtOrder.TargetId), courtProject));
        // the court at n = 1 with X BIT-EQUAL to the military university's (0.5) and every other type higher: key 1 wins.
        WorldState tie = w.Clone();
        Found(tie, Rival, sites[0], 1, 0.5);
        for (int t = 2; t <= 5; t++) Found(tie, Rival, sites[0], t, 0.75);
        Found(tie, Rival, sites[1], courtKey, 0.5);
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 1)!.Id, (int)AiConstructionPolicy.University(tie, cfg, Rival, dt)!.Value.Amount);
        // ...the court's X strictly lower (0.25): the court.
        WorldState lower = w.Clone();
        for (int t = 1; t <= 5; t++) Found(lower, Rival, sites[0], t, 0.5);
        Found(lower, Rival, sites[1], courtKey, 0.25);
        Assert.Equal(courtProject, (int)AiConstructionPolicy.University(lower, cfg, Rival, dt)!.Value.Amount);
        // the human is never touched.
        Assert.Null(AiConstructionPolicy.University(unis, cfg, new PolityId(1), dt));
    }
}
