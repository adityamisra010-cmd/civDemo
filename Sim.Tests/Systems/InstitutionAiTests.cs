using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Construction;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.UniversityRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D6 — the AI founds universities through the SAME order, predicate and systems as the player
/// (AiConstructionPolicy, called by AiOrders). The rule: at most one founding per AI polity per turn, the
/// AVAILABLE specialty with the fewest PROSPECTIVE instances n (founded rows + built-but-unfounded buildings +
/// queued projects), then the lowest maturity-weighted count X, then the lower type key — never a type whose
/// prospective count is already SATURATED — at the eligible site (empty queue, AVAILABLE, founding-viable,
/// materials in full, capacity covering the labour) with the largest spare market (ties to the lower
/// settlement id). Composite keys with integer tie-breaks; tie-dense.
/// </summary>
public class InstitutionAiTests
{
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Human = new(1);
    private static readonly PolityId Rival = new(2);

    private static readonly Lazy<WorldState> DuoAt3 = new(() =>
        Production(Cfg()).Run(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg(), 42), 3));

    /// <summary>The dev duo after three turns (a published surplus, a turn length), the rival granted the
    /// university knowledge, and both rival settlements given materials and a construction share that covers
    /// the 240 adult-year project.</summary>
    private static (WorldState World, SettlementId[] RivalSites) Rig(SimConfig cfg)
    {
        WorldState w = DuoAt3.Value.Clone();
        Grant(w, Research, Rival);
        SettlementId[] sites = LabourActivities.ControlledSettlements(w, Rival);
        Assert.Equal(2, sites.Length);
        int granary = -1;
        foreach (ConstructionProjectEntry p in cfg.Goods!.Projects!) if (p.Name == "granary") granary = p.Id;
        foreach (SettlementId s in sites)
        {
            Materials(w, cfg, s);
            Construction(w, s, 0.5);
            Surplus(w, s, 2.0);
            // The granary already stands (the materials would otherwise buy one first — ordinary projects are
            // decided before universities, at most one order per settlement per turn); the workshop needs tools.
            w.Structures.Add(new StructureRow(s, granary, 1));
        }
        return (w, sites);
    }

    [Fact]
    public void AiFoundsAUniversity_WhereAvailableViableAndAffordable_ThroughTheOrderAndTheSystems()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 100);
        (WorldState w, SettlementId[] sites) = Rig(cfg);

        // Before the knowledge: nothing (LOCKED).
        WorldState locked = DuoAt3.Value.Clone();
        Assert.DoesNotContain(AiOrders.For(locked, cfg), o => o.Kind == OrderKind.EnqueueConstruction
            && cfg.Goods!.ProjectById((int)o.Amount) is { Founds: not null });

        OrderRecord[] orders = AiOrders.For(w, cfg);
        OrderRecord university = Assert.Single(orders, o => o.Kind == OrderKind.EnqueueConstruction
            && cfg.Goods!.ProjectById((int)o.Amount) is { Founds: not null });
        Assert.Equal(Rival.Value, university.ActorId);
        // Specialty: every n and X is 0 → the lowest type key (military). Site: the larger spare market (nothing
        // hosted yet, so the larger adult population), ties to the lower id.
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 1)!.Id, (int)university.Amount);
        long a0 = BandViews.Adults(w.Buckets, sites[0]), a1 = BandViews.Adults(w.Buckets, sites[1]);
        SettlementId expected = a0 > a1 ? sites[0] : a1 > a0 ? sites[1] : Min(sites);
        Assert.Equal(expected.Value, university.TargetId);
        Assert.Equal(ConstructionQuery.EnqueueOrder(w, Rival, expected, (int)university.Amount), university);   // the player's constructor
        Assert.All(orders, o => Assert.Equal(Rival.Value, o.ActorId));                                         // the human is never touched

        // The systems do the rest: built next step, founded the step after, owned by the rival.
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        TurnExecutor exec = Production(cfg, log);
        WorldState built = exec.Step(w);
        Assert.Equal(1L, ConstructionQuery.Built(built, expected, (int)university.Amount));
        WorldState founded = exec.Step(built);
        InstitutionRow row = Assert.Single(Rows(founded.Institutions));
        Assert.Equal((Rival, expected, 1, 0.0), (row.Polity, row.Settlement, row.Type, row.Maturity));
    }

    [Fact]
    public void AiUniversity_NotFounded_WhereNotViable_NotAffordable_OrTheQueueIsBusy()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 100);
        (WorldState w, SettlementId[] sites) = Rig(cfg);
        double dt = w.Clock.DtYears;

        // Not viable (food) at both sites → nothing.
        WorldState hungry = w.Clone();
        foreach (SettlementId s in sites) Surplus(hungry, s, 1.0);
        Assert.Null(AiConstructionPolicy.University(hungry, cfg, Rival, dt));
        // Not viable (market) → nothing.
        Assert.Null(AiConstructionPolicy.University(w, Cfg(adultsPerUniversity: 1_000_000), Rival, dt));
        // Not affordable: no capacity for the labour at the default share → nothing.
        WorldState weak = w.Clone();
        foreach (SettlementId s in sites) Construction(weak, s, 0.01);
        Assert.Null(AiConstructionPolicy.University(weak, cfg, Rival, dt));
        // A busy queue, or a settlement already given an order this turn, is skipped.
        Assert.NotNull(AiConstructionPolicy.University(w, cfg, Rival, dt));
        Assert.Null(AiConstructionPolicy.University(w, cfg, Rival, dt, [sites[0].Value, sites[1].Value]));
        // Without a known turn length the AI never judges a university affordable (founding state).
        Assert.DoesNotContain(AiConstructionPolicy.Decide(w, cfg, Rival), o => cfg.Goods!.ProjectById((int)o.Amount) is { Founds: not null });
        // The human polity never gets one.
        Assert.Null(AiConstructionPolicy.University(w, cfg, Human, dt));
    }

    [Fact]
    public void AiUniversity_SpecialtyAndSite_AreCompositeKeys_WithIntegerTieBreaks_TieDense()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 10);   // a market small enough that a host of one still has room
        (WorldState w, SettlementId[] sites) = Rig(cfg);
        double dt = w.Clock.DtYears;

        // SITE TIE: equalize the two sites' adults (a conserving Ledger transfer between same-key buckets) —
        // equal spare markets → the LOWER settlement id wins.
        Equalize(w, sites[0], sites[1]);
        Assert.Equal(BandViews.Adults(w.Buckets, sites[0]), BandViews.Adults(w.Buckets, sites[1]));
        OrderRecord first = AiConstructionPolicy.University(w, cfg, Rival, dt)!.Value;
        Assert.Equal(Min(sites).Value, first.TargetId);

        // SPECIALTY: the fewest prospective instances first, then the lowest X, ties (bit-equal) to the lower key.
        // All n = X = 0 → key 1.
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 1)!.Id, (int)first.Amount);
        // A military university → n(1) = 1 > 0 = n(2..5) → medical (key 2).
        WorldState one = w.Clone();
        Found(one, Rival, sites[0], 1, 0.5);
        one.Structures.Add(new StructureRow(sites[0], InstitutionContent.ProjectOfType(cfg, 1)!.Id, 1));
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 2)!.Id, (int)AiConstructionPolicy.University(one, cfg, Rival, dt)!.Value.Amount);
        // One of every type (n ties at 1): types 1 and 2 BOTH at X = 0.5 (a dense tie), the rest higher → the
        // lower key, 1.
        Found(one, Rival, sites[1], 2, 0.5);
        for (int t = 3; t <= 5; t++) Found(one, Rival, sites[1], t, 0.75);
        one.Structures.Add(new StructureRow(sites[1], InstitutionContent.ProjectOfType(cfg, 2)!.Id, 1));
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 1)!.Id, (int)AiConstructionPolicy.University(one, cfg, Rival, dt)!.Value.Amount);
        // ...each site now hosts one university building (Structures): equal spare markets → the lower id again.
        OrderRecord third = AiConstructionPolicy.University(one, cfg, Rival, dt)!.Value;
        Assert.Equal(Min(sites).Value, third.TargetId);
    }

    [Fact]
    public void AiUniversity_CountsBuiltAndQueuedInstances_AndNeverFoundsASaturatedSpecialty()
    {
        SimConfig cfg = Cfg(adultsPerUniversity: 10);
        (WorldState w, SettlementId[] sites) = Rig(cfg);
        double dt = w.Clock.DtYears;
        ConstructionProjectEntry military = InstitutionContent.ProjectOfType(cfg, 1)!, medical = InstitutionContent.ProjectOfType(cfg, 2)!;

        // THE FOUNDING LAG: a military university BUILT at sites[0] and not yet founded (no row: InstitutionsSystem
        // founds it next step) and a medical one QUEUED at sites[1] both count — so the AI does not order either
        // specialty again while they are in flight (it did, measured, when only founded maturity counted).
        WorldState inFlight = w.Clone();
        inFlight.Structures.Add(new StructureRow(sites[0], military.Id, 1));
        inFlight.ConstructionQueue.Add(new ConstructionQueueRow(sites[1], 0, medical.Id));
        Assert.Equal(1L, AiConstructionPolicy.ProspectiveCount(inFlight, Rival, 1, military));
        Assert.Equal(1L, AiConstructionPolicy.ProspectiveCount(inFlight, Rival, 2, medical));
        Assert.Equal(0L, AiConstructionPolicy.ProspectiveCount(inFlight, Human, 1, military));   // not the human's
        OrderRecord next = AiConstructionPolicy.University(inFlight, cfg, Rival, dt)!.Value;
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 3)!.Id, (int)next.Amount);   // engineering: n = 0
        Assert.Equal(sites[0].Value, next.TargetId);                                     // sites[1]'s queue is busy
        // Once founded the row replaces the building in the count (never both).
        WorldState founded = inFlight.Clone();
        Found(founded, Rival, sites[0], 1, 0.0);
        Assert.Equal(1L, AiConstructionPolicy.ProspectiveCount(founded, Rival, 1, military));

        // SATURATION: three instances of a type put S(3) = 1 − e^(−3) ≥ 0.95 — the next would add < 5 % of the
        // first — so a type the polity holds three of is not founded again; with every type at three, nothing is.
        Assert.True(InstitutionEffects.Saturation(3) >= cfg.Institutions!.Universities.SaturatedAt);
        Assert.True(InstitutionEffects.Saturation(2) < cfg.Institutions.Universities.SaturatedAt);
        Assert.Null(AiConstructionPolicy.University(Holding(w, sites, engineering: 3), cfg, Rival, dt));
        // ...engineering held twice (n = 2 < 3), every other type three times: engineering is founded again.
        Assert.Equal(InstitutionContent.ProjectOfType(cfg, 3)!.Id,
            (int)AiConstructionPolicy.University(Holding(w, sites, engineering: 2), cfg, Rival, dt)!.Value.Amount);
    }

    /// <summary>The rig with three mature founded rows of every university type, except engineering (key 3) held
    /// <paramref name="engineering"/> times.</summary>
    private static WorldState Holding(WorldState w, SettlementId[] sites, int engineering)
    {
        WorldState held = w.Clone();
        for (int t = 1; t <= 5; t++)
            for (int k = 0; k < (t == 3 ? engineering : 3); k++) Found(held, Rival, sites[k % 2], t, 1.0);
        return held;
    }

    private static void Equalize(WorldState w, SettlementId a, SettlementId b)
    {
        long ga = BandViews.Adults(w.Buckets, a), gb = BandViews.Adults(w.Buckets, b);
        SettlementId from = ga > gb ? a : b, to = ga > gb ? b : a;
        // Move whole adults between the same (culture, religion, class, cohort) key, cohort by cohort; if the
        // difference is odd, one more person moves into an elder cohort of the receiver to keep adults equal.
        var ledger = new Ledger(w.LedgerFlows);
        long remaining = Math.Abs(ga - gb) / 2;
        for (int i = 0; i < w.Buckets.Count && remaining > 0; i++)
        {
            BucketRow src = w.Buckets[i];
            if (src.Settlement != from || !BandViews.IsAdult(src.CohortIdx) || src.Count.Value == 0) continue;
            int dst = -1;
            for (int j = 0; j < w.Buckets.Count; j++)
                if (w.Buckets[j].Settlement == to && w.Buckets[j].Culture == src.Culture && w.Buckets[j].Religion == src.Religion
                    && w.Buckets[j].Class == src.Class && w.Buckets[j].CohortIdx == src.CohortIdx) { dst = j; break; }
            if (dst < 0) continue;
            long n = Math.Min(remaining, src.Count.Value);
            ledger.Transfer(ref w.Buckets.Ref(i).Count, ref w.Buckets.Ref(dst).Count, n, OverdrawPolicy.Throw);
            remaining -= n;
        }
        if ((Math.Abs(ga - gb) % 2) == 1)
        {
            // Odd difference: one adult of the larger ages out of the adult band (a conserving transfer to the
            // same key's first elder cohort), so both hold the same number of adults.
            for (int i = 0; i < w.Buckets.Count; i++)
            {
                BucketRow src = w.Buckets[i];
                if (src.Settlement != from || src.CohortIdx != Cohorts.FirstElder - 1 || src.Count.Value == 0) continue;
                for (int j = 0; j < w.Buckets.Count; j++)
                {
                    BucketRow d = w.Buckets[j];
                    if (d.Settlement != from || d.CohortIdx != Cohorts.FirstElder || d.Culture != src.Culture
                        || d.Religion != src.Religion || d.Class != src.Class) continue;
                    ledger.Transfer(ref w.Buckets.Ref(i).Count, ref w.Buckets.Ref(j).Count, 1, OverdrawPolicy.Throw);
                    return;
                }
            }
        }
    }

    private static SettlementId Min(SettlementId[] sites) => sites[0].Value <= sites[1].Value ? sites[0] : sites[1];

    private static T[] Rows<T>(Table<T> t) where T : unmanaged
    {
        var rows = new T[t.Count];
        for (int i = 0; i < t.Count; i++) rows[i] = t[i];
        return rows;
    }
}
