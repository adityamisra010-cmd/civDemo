using System.Collections.Immutable;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// M6 closure (D-066 R2, the content/consequence audit) — THE ACTION-SURFACE ACCEPTANCE SWEEP. ADR-033 D2 promises "one
/// predicate, two callers": the UI lists an order exactly when the simulation would apply it. The pipeline tests (T15, T16)
/// probe single orders; this sweep measures the whole surface on one seeded world, kind by kind: for EVERY action type it
/// enumerates the candidate orders the order format allows over the world's settlements, projects, nodes, Ages, identities and
/// formations, takes the UI-AVAILABLE set from <see cref="AvailableActionsQuery"/> (what the UI shows as issuable now) and the
/// SIMULATION-ACCEPTED set by stepping each candidate ALONE through the system that consumes it and observing its effect, and
/// asserts the two sets are equal.
/// <para><b>What "available" means per domain</b> (the descriptor's documented blocker semantics): Construction and
/// Institutions list a project whose materials or capacity are short with a TRANSIENT blocker — the order is legal and is
/// queued (it waits at the head), so a listed project is available with or without a blocker. Every other domain's blocker
/// is the refusal of the predicate the system applies (Discovery, Recruitment, Roads), so a blocked entry is NOT available.</para>
/// <para><b>Stepping</b>: each candidate runs through the CONSUMER SYSTEM alone (the pipeline filtered to it, in pipeline.json
/// order) on a clone of the seeded world — the authority on acceptance, judged on PREV as the action surface is. A full
/// pipeline would add the other systems' same-step changes (for Discovery the step's own stocks: ADR-044 §5's documented
/// preview-versus-delivery case, which DiscoveryQuery.RefusedAtDelivery reports to the player).</para>
/// </summary>
public class ActionSurfaceAcceptanceSweepTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent Research = Cfg.Research!;
    private static readonly PolityId Me = new(1);

    private static readonly Lazy<SystemRegistration[]> Pipeline = new(() =>
    {
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return PipelineLoader.Load(pipe, SystemCatalog.All(Cfg, TestConfigs.Worldgen()));
    });

    private static readonly Lazy<EraTable> Era = new(() =>
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(era);
    });

    /// <summary>THE SEEDED WORLD: the dev map (seed 42) with one AI Empire (foreign settlements to refuse), stepped once; the
    /// player knows taxation (in its tax Age), the first road class, ore prospecting and the bow, so every gated domain has
    /// members on both sides of its gate; a granary is queued and committed (a cancellable entry).</summary>
    private static readonly Lazy<WorldState> Seeded = new(() =>
    {
        WorldState w = Full(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));
        Know(w, "taxation", "track_road", "ore_prospecting", "bow_simple");
        GovernanceRigs.EnterTaxAge(w, Me);
        // three expeditions from every settlement the player rules widen its known extent past the founding spacing, so
        // the colonization sweep meets legal sites as well as refused ones
        for (int t = 0; t < 3; t++)
        {
            var explore = new List<OrderRecord>();
            foreach (int s in SettlementIds(w))
                if (DiscoveryQuery.CheckExplore(w, Cfg, Me, new SettlementId(s)) == ExploreVerdict.Ok)
                    explore.Add(OrderRecord.From(w.Clock.Turn, Me, OrderKind.Explore, s, 0.0));
            w = Full(w, [.. explore]);
        }
        // every project the capital may build, queued at once: the cheap ones may finish in the step, the rest stay queued
        // and committed (cancellable entries for the queue sweep)
        Assert.True(EmpireQuery.TryGetCapital(w, Me, out SettlementId cap));
        var builds = new List<OrderRecord>();
        foreach (ConstructionProjectEntry p in Cfg.Goods!.Projects!)
            if (ConstructionQuery.IsProjectAvailable(w, Cfg, Me, cap, p.Id))
                for (int copy = 0; copy < 3; copy++) builds.Add(ConstructionQuery.EnqueueOrder(w, Me, cap, p.Id));
        Assert.NotEmpty(builds);
        w = Full(w, [.. builds]);
        Assert.True(Rows(w.ConstructionQueue).Any(r => r.Settlement == cap),
            $"{builds.Count} enqueue orders at the capital left no committed entry (structures {w.Structures.Count})");
        return w;
    });

    private static WorldState Full(WorldState w, params OrderRecord[] orders) => Run(w, Pipeline.Value, orders);

    private static WorldState Only(WorldState w, string[] systems, params OrderRecord[] orders)
    {
        var subset = new List<SystemRegistration>();
        foreach (SystemRegistration r in Pipeline.Value) if (Array.IndexOf(systems, r.Name) >= 0) subset.Add(r);
        Assert.Equal(systems.Length, subset.Count);
        return Run(w, [.. subset], orders);
    }

    private static WorldState Run(WorldState w, SystemRegistration[] pipeline, OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return new TurnExecutor(Era.Value, pipeline, log).Step(w);
    }

    private static void Know(WorldState w, params string[] ids)
    {
        var seen = new bool[Research.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in ids) { int i = Research.IndexOfId(id); Assert.True(i >= 0, id); stack.Push(i); }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in Research.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        for (int i = 0; i < seen.Length; i++)
            if (seen[i] && !ResearchQuery.IsCompleted(w, Me, Research.Nodes[i].Key))
                w.ResearchCompleted.Add(new ResearchCompletedRow(Me, Research.Nodes[i].Key));
    }

    private static ImmutableArray<ActionDescriptor> Actions(WorldState w) =>
        AvailableActionsQuery.For(w, Cfg, Me, ActionQueryContext.ForNextStep(w, Era.Value));

    private static IEnumerable<ActionDescriptor> Of(WorldState w, params OrderKind[] kinds) =>
        Actions(w).Where(a => a.Kind == ActionKind.Order && a.Order is { } k && kinds.Contains(k));

    private static T[] Rows<T>(IReadOnlyTable<T> t) where T : unmanaged { var r = new T[t.Count]; for (int i = 0; i < r.Length; i++) r[i] = t[i]; return r; }

    private static int[] SettlementIds(WorldState w) => Rows(w.Settlements).Select(s => s.Id.Value).OrderBy(x => x).ToArray();

    /// <summary>The two sets, equal; neither may be vacuous on both sides when <paramref name="mustBeNonEmpty"/>.</summary>
    private void Equal(string kind, SortedSet<string> ui, SortedSet<string> sim, int probed, bool mustBeNonEmpty = true)
    {
        output.WriteLine($"{kind}: {probed} candidates probed; offered {ui.Count}, accepted {sim.Count}");
        Assert.True(probed > 0, $"{kind}: no candidate probed");
        if (mustBeNonEmpty) Assert.True(ui.Count > 0, $"{kind}: the seeded world offers nothing — the sweep would prove nothing");
        var onlyUi = new SortedSet<string>(ui); onlyUi.ExceptWith(sim);
        var onlySim = new SortedSet<string>(sim); onlySim.ExceptWith(ui);
        Assert.True(onlyUi.Count == 0 && onlySim.Count == 0,
            $"{kind}: {probed} candidates; offered by the UI but refused by the simulation [{string.Join(", ", onlyUi)}]; " +
            $"accepted by the simulation but not offered [{string.Join(", ", onlySim)}]");
    }

    // ------------------------------------------------------------------ 3 SectorAllocation (Labour)

    [Fact]
    public void Labour_SectorAllocation_OfferedExactlyWhereAccepted()
    {
        WorldState w = Seeded.Value;
        var ui = new SortedSet<string>(Of(w, OrderKind.SectorAllocation).Select(a => $"{a.Targets[0].Id}.{a.Targets[1].Id}"));
        var sim = new SortedSet<string>();
        int probed = 0;
        foreach (int s in SettlementIds(w))
            for (int sector = 0; sector < Sectors.Count; sector++)
            {
                probed++;
                WorldState next = Only(w, ["pathbuild"], new OrderRecord(w.Clock.Turn, Me.Value, OrderKind.SectorAllocation, s * 8 + sector, 37.0));
                SectorAllocationRow? before = Rows(w.SectorAllocations).Where(r => r.Settlement.Value == s).Select(r => (SectorAllocationRow?)r).FirstOrDefault();
                SectorAllocationRow? after = Rows(next.SectorAllocations).Where(r => r.Settlement.Value == s).Select(r => (SectorAllocationRow?)r).FirstOrDefault();
                if (!Equals(before, after)) sim.Add($"{s}.{sector}");
            }
        Equal("SectorAllocation", ui, sim, probed);
    }

    // ------------------------------------------------------------------ 4 EnqueueConstruction (Construction + Institutions)

    [Fact]
    public void Construction_AndInstitutionFounding_OfferedExactlyWhereAccepted()
    {
        WorldState w = Seeded.Value;
        var ui = new SortedSet<string>(Of(w, OrderKind.EnqueueConstruction).Select(a => $"{a.Targets[0].Id}.{a.Targets[1].Id}"));
        var sim = new SortedSet<string>();
        int probed = 0;
        foreach (int s in SettlementIds(w))
            foreach (ConstructionProjectEntry p in Cfg.Goods!.Projects!)
            {
                probed++;
                WorldState next = Only(w, ["construction"], ConstructionQuery.EnqueueOrder(w, Me, new SettlementId(s), p.Id));
                // accepted = queued, or queued and finished within the same step (a built structure)
                if (Placed(next, s, p.Id) > Placed(w, s, p.Id)) sim.Add($"{s}.{p.Id}");
            }
        Equal("EnqueueConstruction", ui, sim, probed);
    }

    private static long Placed(WorldState w, int settlement, int project) =>
        Rows(w.ConstructionQueue).Count(r => r.Settlement.Value == settlement && r.ProjectId == project)
        + Rows(w.Structures).Where(r => r.Settlement.Value == settlement && r.ProjectId == project).Sum(r => r.Count);

    // ------------------------------------------------------------------ 13 CancelConstructionEntry (the queue)

    [Fact]
    public void ConstructionQueueCancel_OfferedExactlyWhereAccepted()
    {
        WorldState w = Seeded.Value;
        var ui = new SortedSet<string>(Of(w, OrderKind.CancelConstructionEntry).Select(a => $"{a.Targets[0].Id}.{a.Id & 0xFFFF_FFFFL}"));
        var sim = new SortedSet<string>();
        int probed = 0;
        foreach (int s in SettlementIds(w))
            for (int slot = 0; slot < 3; slot++)
            {
                probed++;
                WorldState next = Only(w, ["construction"], ConstructionQuery.CancelOrder(w, Me, new SettlementId(s), slot));
                bool before = Rows(w.ConstructionQueue).Any(r => r.Settlement.Value == s && r.Slot == slot);
                bool after = Rows(next.ConstructionQueue).Any(r => r.Settlement.Value == s && r.Slot == slot);
                if (before && !after) sim.Add($"{s}.{slot}");
            }
        Equal("CancelConstructionEntry", ui, sim, probed);
    }

    // ------------------------------------------------------------------ 12 SetResourceAllocation

    [Fact]
    public void ResourceAllocation_OfferedExactlyWhereAccepted()
    {
        WorldState w = Seeded.Value;
        int inForce = ResourceAllocation.EffectivePercent(w, Me);
        bool listed = Of(w, OrderKind.SetResourceAllocation).Any();
        var ui = new SortedSet<string>();
        var sim = new SortedSet<string>();
        int probed = 0;
        foreach (int pct in new[] { 0, 37, 100 })
        {
            if (pct == inForce) continue;   // the same split changes nothing (the UI logs no such Set)
            probed++;
            if (listed) ui.Add(pct.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WorldState next = Only(w, ["construction"], ResourceAllocation.Order(w, Me, pct));
            if (ResourceAllocation.EffectivePercent(next, Me) == pct) sim.Add(pct.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Equal("SetResourceAllocation", ui, sim, probed, mustBeNonEmpty: ResourceAllocation.Enabled(Cfg));
    }

    // ------------------------------------------------------------------ 5 SetTaxRate (Governance)

    [Fact]
    public void TaxEdict_OfferedExactlyWhereAccepted_OnBothSidesOfItsGate()
    {
        foreach (bool knowsTaxation in new[] { true, false })
        {
            WorldState w = Seeded.Value.Clone();
            if (!knowsTaxation)
            {
                int tax = Research.IndexOfId("taxation");
                var keep = Rows(w.ResearchCompleted).Where(r => !(r.Polity == Me && r.Node == Research.Nodes[tax].Key)).ToArray();
                w.ResearchCompleted.Clear();
                foreach (ResearchCompletedRow r in keep) w.ResearchCompleted.Add(r);
            }
            var ui = new SortedSet<string>(Of(w, OrderKind.SetTaxRate).Select(_ => "30"));
            WorldState next = Only(w, ["governance"], Governance.TaxOrder(w.Clock.Turn, Me, 30));
            var sim = new SortedSet<string>(Rows(next.TaxPolicies).Where(r => r.Polity == Me && !Rows(w.TaxPolicies).Contains(r)).Select(_ => "30"));
            Equal($"SetTaxRate (knows taxation: {knowsTaxation})", ui, sim, 1, mustBeNonEmpty: knowsTaxation);
        }
    }

    // ------------------------------------------------------------------ 6 SetResearchTarget (both lanes; routing on)

    [Fact]
    public void ResearchTarget_EveryNode_OfferedExactlyWhereAccepted_BothLanes()
    {
        WorldState w = Seeded.Value;
        var ui = new SortedSet<string>(Of(w, OrderKind.SetResearchTarget)
            .Where(a => a.Id is 1 or 4)   // set a target (available) / a destination (routing); Ids 2, 3 clear a lane
            .SelectMany(a => a.Targets).Select(t => t.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var sim = new SortedSet<string>();
        int probed = 0;
        for (int i = 0; i < Research.Nodes.Count; i++)
        {
            probed++;
            ResearchNode n = Research.Nodes[i];
            WorldState next = Only(w, ["research"], OrderRecord.From(w.Clock.Turn, Me, OrderKind.SetResearchTarget, n.Key.Value, 0.0));
            if (ResearchQuery.TryGetTarget(next, Research, Me, n.Tree, out ResearchNodeId t) && t == n.Key
                && !(ResearchQuery.TryGetTarget(w, Research, Me, n.Tree, out ResearchNodeId was) && was == n.Key))
                sim.Add(n.Key.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Equal("SetResearchTarget", ui, sim, probed);
    }

    // ------------------------------------------------------------------ 7 AdvanceAge

    /// <summary>On its own world: the player made eligible for Age 2 exactly as AgeProgressionTests' rig does (core
    /// cereal_cultivation, supporting pottery and a built granary), so the sweep meets the one legal advance and every
    /// refused one (wrong Age, unknown surge) — and, on the seeded world (Age 3, not eligible), refuses them all.</summary>
    [Fact]
    public void AdvanceAge_OfferedExactlyWhereAccepted_EligibleAndNot()
    {
        AgeContent ages = Cfg.Ages!;
        WorldState eligible = Full(WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));
        Know(eligible, "cereal_cultivation", "pottery_open_fired");
        Assert.True(EmpireQuery.TryGetCapital(eligible, Me, out SettlementId capital));
        eligible.Structures.Add(new StructureRow(capital, Cfg.Goods!.Projects!.First(p => p.Name == "granary").Id, 1));
        eligible = Full(eligible);
        Assert.Equal(AdvanceRejection.None, AgeQuery.CheckAdvance(eligible, ages, Me, 2, ages.Surges[0].Key));
        foreach ((WorldState w, bool expectSome) in new[] { (eligible, true), (Seeded.Value, false) })
        {
            var ui = new SortedSet<string>(Of(w, OrderKind.AdvanceAge).SelectMany(a => a.Targets.Select(t => $"{a.Id}.{t.Id}")));
            var sim = new SortedSet<string>();
            int probed = 0;
            int age = AgeQuery.CurrentAge(w, ages, Me);
            for (int to = 2; to <= AgeContent.AgeCount; to++)
                foreach (int surge in ages.Surges.Select(x => x.Key).Append(ages.Surges.Max(x => x.Key) + 1))   // + an unknown surge
                {
                    probed++;
                    WorldState next = Only(w, ["ageeligibility", "agetransition"], AgeQuery.AdvanceOrder(w, Me, to, surge));
                    if (AgeQuery.CurrentAge(next, ages, Me) != age) sim.Add($"{to}.{surge}");
                }
            Equal($"AdvanceAge (from Age {age})", ui, sim, probed, mustBeNonEmpty: expectSome);
            if (!expectSome) Assert.Empty(sim);
        }
    }

    // ------------------------------------------------------------------ 8 DevelopRoads

    [Fact]
    public void DevelopRoads_OfferedExactlyWhereAccepted()
    {
        WorldState w = Seeded.Value;
        var ui = new SortedSet<string>(Of(w, OrderKind.DevelopRoads).Where(a => !a.IsBlocked).Select(_ => "develop"));
        WorldState next = Only(w, ["roaddevelopment"], RoadDevelopmentQuery.DevelopOrder(w, Me, 100));
        var sim = new SortedSet<string>(next.RoadDevelopments.Count > w.RoadDevelopments.Count ? ["develop"] : Array.Empty<string>());
        Equal("DevelopRoads", ui, sim, 1, mustBeNonEmpty: false);
    }

    // ------------------------------------------------------------------ 14 Explore, 15 Prospect (survey and assay)

    [Fact]
    public void ExploreAndProspect_OfferedExactlyWhereAccepted()
    {
        WorldState w = Seeded.Value;
        ImmutableArray<ActionDescriptor> discovery = [.. Of(w, OrderKind.Explore, OrderKind.Prospect)];
        string Label(ActionDescriptor a) => (a.Id % 4) switch { 0 => "explore", 1 => "survey", _ => "assay" } + "." + a.Targets[0].Id;
        var ui = new SortedSet<string>(discovery.Where(a => !a.IsBlocked).Select(Label));
        var sim = new SortedSet<string>();
        int probed = 0;
        foreach (int s in SettlementIds(w))
        {
            var id = new SettlementId(s);
            probed += 3;
            WorldState e = Only(w, ["discovery"], OrderRecord.From(w.Clock.Turn, Me, OrderKind.Explore, s, 0.0));
            if (DiscoveryQuery.ExploredRadius(e, Cfg.Fog!, Me, id) > DiscoveryQuery.ExploredRadius(w, Cfg.Fog!, Me, id)) sim.Add("explore." + s);
            foreach ((ProspectMethod m, string name) in new[] { (ProspectMethod.Survey, "survey"), (ProspectMethod.Assay, "assay") })
            {
                WorldState p = Only(w, ["discovery"], OrderRecord.From(w.Clock.Turn, Me, OrderKind.Prospect, s, (double)(int)m));
                if (DepositConcealment.HasSurveyed(p, Me, id) && !DepositConcealment.HasSurveyed(w, Me, id)) sim.Add(name + "." + s);
            }
        }
        // the assay is offered only once ore_prospecting is known; the seeded player knows it, so all three are exercised
        Assert.True(DiscoveryQuery.AssayUnlocked(w, Cfg, Me));
        Equal("Explore / Prospect", ui, sim, probed);
    }

    // ------------------------------------------------------------------ 16 Recruit, 17 Re-equip (and cancel), 18 Disband

    [Fact]
    public void Military_RecruitReequipDisband_OfferedExactlyWhereAccepted()
    {
        if (!RecruitmentQuery.Enabled(Cfg)) return;
        WorldState w = Seeded.Value;
        UnitFamilyContent families = Cfg.UnitFamilies!;
        var keys = families.Families.SelectMany(f => f.Line).Select(i => i.Key).OrderBy(k => k).ToArray();

        // Recruit: (settlement, identity) — a listed target with no blocker
        var ui = new SortedSet<string>(Of(w, OrderKind.RecruitFormation)
            .SelectMany(a => a.Targets.Where(t => t.Blocker is null).Select(t => $"{KeyId(a, "military.recruit.")}.{t.Id}")));
        var sim = new SortedSet<string>();
        int probed = 0;
        foreach (int s in SettlementIds(w))
            foreach (int k in keys)
            {
                probed++;
                WorldState next = Only(w, ["military"], OrderRecord.From(w.Clock.Turn, Me, OrderKind.RecruitFormation, s, k));
                if (next.MilitaryUnits.Count > w.MilitaryUnits.Count) sim.Add($"{s}.{k}");
            }
        Equal("RecruitFormation", ui, sim, probed, mustBeNonEmpty: false);

        // Re-equip: (formation, identity); Disband: formation
        MilitaryUnitRow[] all = Rows(w.MilitaryUnits);
        Assert.Contains(all, u => u.Owner == Me);
        var uiRe = new SortedSet<string>(Of(w, OrderKind.ReequipFormation)
            .SelectMany(a => a.Targets.Where(t => t.Kind == ActionTargetKind.UnitIdentity && t.Blocker is null).Select(t => $"{KeyId(a, "military.reequip.")}.{t.Id}")));
        var uiDis = new SortedSet<string>(Of(w, OrderKind.DisbandFormation).Select(a => a.Targets[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var simRe = new SortedSet<string>();
        var simDis = new SortedSet<string>();
        int probedRe = 0, probedDis = 0;
        foreach (MilitaryUnitRow u in all)
        {
            foreach (int k in keys)
            {
                probedRe++;
                WorldState next = Only(w, ["military"], OrderRecord.From(w.Clock.Turn, Me, OrderKind.ReequipFormation, u.Id, k));
                if (Rows(next.Reequips).Any(r => r.Unit == u.Id) && !Rows(w.Reequips).Any(r => r.Unit == u.Id)) simRe.Add($"{u.Id}.{k}");
            }
            probedDis++;
            WorldState d = Only(w, ["military"], OrderRecord.From(w.Clock.Turn, Me, OrderKind.DisbandFormation, u.Id, 0.0));
            if (!Rows(d.MilitaryUnits).Any(r => r.Id == u.Id)) simDis.Add(u.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Equal("ReequipFormation", uiRe, simRe, probedRe, mustBeNonEmpty: false);
        Equal("DisbandFormation", uiDis, simDis, probedDis);
    }

    /// <summary>The settlement or formation id a military descriptor's key carries ("military.recruit.{id}").</summary>
    private static long KeyId(ActionDescriptor a, string prefix)
    {
        Assert.StartsWith(prefix, a.Key, StringComparison.Ordinal);
        return long.Parse(a.Key[prefix.Length..], System.Globalization.CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------ 9 FoundSettlement (Colonization)

    [Fact]
    public void FoundSettlement_SourcesAndSites_OfferedExactlyWhereAccepted()
    {
        WorldState w = Seeded.Value;
        TerrainSet terrain = w.Terrain!;
        var frontier = FoundingQuery.Frontier(w, Cfg, TestConfigs.Worldgen())!;
        var sources = new SortedSet<long>(Of(w, OrderKind.FoundSettlement).Select(a => a.Targets[0].Id));
        PolityKnowledge knowledge = PolityKnowledge.Of(w, Me, Cfg);
        var ui = new SortedSet<string>();
        var sim = new SortedSet<string>();
        int probed = 0;
        int n = terrain.Size * terrain.Size;
        foreach (int s in SettlementIds(w))
        {
            var src = new SettlementId(s);
            // the candidate sites: every 97th known cell (a spread over the known extent: water, poor, claimed, too close, and
            // legal land), every 997th cell of the map (mostly unknown), and the first four cells the predicate admits
            var cells = new SortedSet<int>();
            int legalSeen = 0;
            for (int c = 0, k = 0; c < n; c++)
            {
                bool known = knowledge.KnowsCell(c);
                if (known && k++ % 97 == 0) cells.Add(c);
                if (c % 997 == 0) cells.Add(c);
                if (known && legalSeen < 4 && FoundingQuery.Check(w, Cfg, frontier, Me, src, c) == FoundingVerdict.Legal) { cells.Add(c); legalSeen++; }
            }
            foreach (int cell in cells)
            {
                probed++;
                bool offered = sources.Contains(s) && knowledge.KnowsCell(cell)
                    && FoundingQuery.Check(w, Cfg, frontier, Me, src, cell) == FoundingVerdict.Legal;
                if (offered) ui.Add($"{s}.{cell}");
                WorldState next = Only(w, ["colonization"], FoundingQuery.FoundOrder(w, Me, src, cell));
                if (Rows(next.Settlements).Any(r => r.SiteCell == cell) && !Rows(w.Settlements).Any(r => r.SiteCell == cell)) sim.Add($"{s}.{cell}");
            }
        }
        Equal("FoundSettlement", ui, sim, probed);
    }
}
