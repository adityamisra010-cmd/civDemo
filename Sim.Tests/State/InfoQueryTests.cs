using System.Collections.Immutable;
using System.Text.RegularExpressions;
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
/// M5 POLISH — SHIFT + LEFT CLICK UNIVERSAL INFO (directive §9) and RESEARCH DISCOVERABILITY (§10): the read-only
/// <see cref="InfoQuery"/>. First the Director's named chains, each pinned to the content and to the predicate the
/// simulation itself calls (Cultivated cereals ← Cereal cultivation ← Grinding stone; Farming ← Root and tuber
/// cultivation; Pressure flaking is knowledge only; Toolmaking ← bronze ← Bronze casting ← Tin bronze; the granary and
/// the workshop against ConstructionQuery; the tax edict in its three gate states against Governance.GateOf). Then
/// PROPERTY tests over every node, entity and action descriptor: a card never disagrees with the legality the
/// simulation applies on the same world. Then determinism (twice, and across a save/load), the composite-key argmax of
/// a condition's live value (tie-dense), and the no-second-database rule (no content id literal in InfoQuery.cs).
/// </summary>
public class InfoQueryTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly ResearchContent R = Cfg.Research!;
    private static readonly AgeContent Ages = Cfg.Ages!;

    private static readonly Lazy<(WorldState World, PolityId Player)> Founded = new(() =>
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42);
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return (w, w.Polities[i].Id);
        throw new InvalidOperationException("no player Empire");
    });

    private static WorldState World() => Founded.Value.World.Clone();
    private static PolityId Player => Founded.Value.Player;

    private static int Node(string id) { int i = R.IndexOfId(id); Assert.True(i >= 0, id); return i; }
    private static ResearchNodeId Key(string id) => R.Nodes[Node(id)].Key;

    private static WorldState Know(WorldState w, params string[] ids)
    {
        var seen = new bool[R.Nodes.Count];
        var stack = new Stack<int>();
        foreach (string id in ids) stack.Push(Node(id));
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            if (seen[i]) continue;
            seen[i] = true;
            foreach (int p in R.Nodes[i].PrerequisiteNodes) stack.Push(p);
        }
        for (int i = 0; i < seen.Length; i++)
            if (seen[i] && !ResearchQuery.IsCompleted(w, Player, R.Nodes[i].Key)) w.ResearchCompleted.Add(new ResearchCompletedRow(Player, R.Nodes[i].Key));
        return w;
    }

    private static WorldState AtAge(WorldState w, int age) { GovernanceRigs.EnterAge(w, Player, age); return w; }

    /// <summary>Sets a published variable of a settlement (replacing the row the founding published, if any).</summary>
    private static void SetVariable(WorldState w, SettlementId s, int varId, double value)
    {
        for (int i = 0; i < w.Variables.Count; i++)
            if (w.Variables[i].Settlement == s && w.Variables[i].VarId == varId) { w.Variables[i] = new VariableRow(s, varId, value); return; }
        w.Variables.Add(new VariableRow(s, varId, value));
    }

    private static SettlementId Capital(WorldState w) { Assert.True(EmpireQuery.TryGetCapital(w, Player, out SettlementId c)); return c; }

    private static InfoCard Card(WorldState w, InfoSubject s, ActionQueryContext? q = null) => InfoQuery.Card(w, Cfg, Player, s, q);

    private static bool Links(ImmutableArray<InfoLink> links, InfoSubject subject)
    {
        foreach (InfoLink l in links) if (l.Subject == subject) return true;
        return false;
    }

    private static List<InfoSubject> Subjects(ImmutableArray<InfoLink> links, InfoKind kind)
    {
        var r = new List<InfoSubject>();
        foreach (InfoLink l in links) if (l.Subject is { } s && s.Kind == kind) r.Add(s);
        return r;
    }

    // ================================================================== the named chains

    [Fact]
    public void CultivatedCereals_NamesItsNode_ThePrerequisite_AndTheResearchToDoNext()
    {
        WorldState w = World();
        InfoCard card = Card(w, InfoSubject.Milestone(2, "a2_cultivation"));
        Assert.Equal("Cultivated cereals", card.Title);
        Assert.Equal(InfoStatus.NotMet, card.Status);
        Assert.Equal(Key("cereal_cultivation"), card.ResearchNode);
        Assert.Equal([InfoSubject.Node(Key("cereal_cultivation"))], Subjects(card.EnabledBy, InfoKind.ResearchNode));
        Assert.Equal(false, card.EnabledBy[0].Met);
        Assert.True(Links(card.Prerequisites, InfoSubject.Node(Key("grinding_stone"))), "the node's prerequisite is named");
        Assert.True(Links(card.WhyLocked, InfoSubject.Node(Key("cereal_cultivation"))));
        InfoSection next = Assert.Single(card.More, s => s.Heading == "Research next");
        Assert.Equal(InfoSubject.Node(Key("grinding_stone")), next.Lines[0].Subject);
        Assert.Contains("Cereal cultivation", card.Summary);
        Assert.Contains("Grinding stone", card.Summary);
        Assert.Equal(Node("grinding_stone"), InfoQuery.NextResearchToward(R, ResearchQuery.CompletedMask(w, R, Player), Node("cereal_cultivation")));

        // The node's own card says it is the Age II core milestone (the reverse link the tree used to omit).
        InfoCard node = Card(w, InfoSubject.Node(Key("cereal_cultivation")));
        Assert.True(Links(node.Enables, InfoSubject.Milestone(2, "a2_cultivation")));
        Assert.Equal(InfoStatus.Locked, node.Status);

        // Once Grinding stone is known the milestone's next research is Cereal cultivation itself, open now.
        Know(w, "grinding_stone");
        InfoCard after = Card(w, InfoSubject.Milestone(2, "a2_cultivation"));
        Assert.Equal(InfoSubject.Node(Key("cereal_cultivation")), Assert.Single(after.More).Lines[0].Subject);
        InfoCard open = Card(w, InfoSubject.Node(Key("cereal_cultivation")));
        Assert.Equal(InfoStatus.Researchable, open.Status);
        Assert.Empty(open.WhyLocked);
    }

    [Fact]
    public void Farming_IsKnownFromRootAndTuberCultivation_AndItsProvenanceIsTheLabourDescriptors()
    {
        WorldState w = World();
        SettlementId cap = Capital(w);
        InfoSubject sector = InfoSubject.Sector(Sectors.Farming, cap.Value);
        InfoCard before = Card(w, sector);
        Assert.Equal(LabourActivities.Of(w, Cfg, Player, cap, Sectors.Farming)!.Label, before.Title);
        Assert.True(LabourActivities.HarvestsWildFood(w, Cfg, cap));
        Assert.Contains("WILD", before.StatusLine);
        Assert.Equal(Key("root_crop"), before.ResearchNode);   // the first atom open to research now
        InfoLink could = Assert.Single(Assert.Single(before.More, s => s.Heading == "Research can change it").Lines);
        Assert.Equal(InfoSubject.OfEntity("activity.farming"), could.Subject);
        Assert.Contains("Root and tuber cultivation", could.Note);

        Know(w, "root_crop");
        InfoCard after = Card(w, sector);
        ActionDescriptor labour = AvailableActionsQuery.For(w, Cfg, Player).Single(a => a.Domain == ActionDomain.Labour
            && a.Targets[0].Id == cap.Value && a.Targets[1].Id == Sectors.Farming);
        Assert.Equal("Farming", after.Title);
        Assert.Equal(labour, after.Action);
        Assert.Equal(labour.Provenance.NodeNames.ToArray(),
            after.EnabledBy.Where(l => l.Subject is { Kind: InfoKind.ResearchNode }).Select(l => l.Label).ToArray());
        Assert.Equal(Key("root_crop"), after.ResearchNode);    // a known activity points at the research it came from
        Assert.False(LabourActivities.HarvestsWildFood(w, Cfg, cap));
        Assert.Contains("Cultivated yields apply", after.StatusLine);
        Assert.Equal(Key("root_crop"), Card(w, InfoSubject.OfEntity("activity.farming")).ResearchNode);
    }

    [Fact]
    public void PressureFlaking_IsKnowledgeOnly_AndTheCardSaysSo()
    {
        WorldState w = World();
        ResearchNode n = R.Nodes[Node("pressure_flaking")];
        InfoCard card = Card(w, InfoSubject.Node(n.Key));
        Assert.Equal(InfoEffect.KnowledgeOnly, card.Effect);
        Assert.Empty(card.Enables);
        Assert.Equal([.. n.Capabilities, .. n.Techniques, .. n.Applications], card.KnowledgeOnly.ToArray());
        Assert.Contains("no simulated effect in this build", card.RealizedBy);
    }

    [Fact]
    public void EveryNodesEffect_IsKnowledgeOnly_ExactlyWhenNothingReadsIt()
    {
        WorldState w = World();
        Sim.Core.Systems.ClassMobility.Predicate tax = ResearchContentLoader.ParseRequirement(R, Cfg.Governance!.TaxationRequires, "test");
        for (int i = 0; i < R.Nodes.Count; i++)
        {
            ResearchNode n = R.Nodes[i];
            bool named = false;
            for (int a = 1; a <= AgeContent.AgeCount; a++)
                if (Ages.Age(a).Entry is { } e)
                    foreach (AgeMilestone m in e.Core.Concat(e.Supporting))
                        if (m.Fact.Kind == MilestoneFactKind.Research && m.Fact.NodeKeys.Contains(n.Key.Value)) named = true;
            InfoCard card = Card(w, InfoSubject.Node(n.Key));
            bool realizedEntity = card.Enables.Any(l => l.Subject is { Kind: InfoKind.Entity } && l.Note is { } note
                && !note.Contains("not built in this build", StringComparison.Ordinal) && !note.Contains("Battle Layer (M7)", StringComparison.Ordinal));
            bool reads = realizedEntity || named || tax.AtomIds.Contains(i) || R.Stage.AtomIds.Contains(i);
            InfoEffect expect = reads ? InfoEffect.Simulated : n.Dependents.Count > 0 ? InfoEffect.ResearchOnly : InfoEffect.KnowledgeOnly;
            Assert.True(expect == card.Effect, n.Id + ": expected " + expect + ", card says " + card.Effect);
        }
    }

    [Fact]
    public void Toolmaking_IsKnownFromTheStart_ButCannotProduce_AndTheChainEndsAtTinBronze()
    {
        WorldState w = World();
        InfoCard card = Card(w, InfoSubject.OfRecipe("toolmaking"));
        ActionDescriptor production = AvailableActionsQuery.For(w, Cfg, Player).Single(a => a.Key == "production.toolmaking");
        Assert.Equal("Toolmaking", card.Title);
        Assert.Equal(InfoStatus.Known, card.Status);
        Assert.Equal(production, card.Action);
        Assert.Contains(card.EnabledBy, l => l.Subject is { Kind: InfoKind.Baseline });
        Assert.Contains(card.Requirements, l => l.Subject == InfoSubject.OfGood(Cfg.Goods!.IdOf("bronze")) && l.Met == false);
        InfoLink condition = Assert.Single(card.Requirements, l => l.Label.StartsWith("Condition:", StringComparison.Ordinal));
        Assert.Contains("artisan_share > 0.05", condition.Label);
        Assert.Contains(Variables.Describe(Variables.ArtisanShare), condition.Label);   // never the raw predicate alone
        Assert.Equal(Key("tin_bronze"), card.ResearchNode);
        Assert.Contains(card.WhyLocked, l => l.Label.Contains("Bronze casting <- Tin bronze", StringComparison.Ordinal));
        Assert.Equal("tools <- Toolmaking <- bronze <- Bronze casting <- Tin bronze (research, Age III)",
            InfoQuery.SourceChain(w, Cfg, Player, "tools"));
        Assert.Equal(Node("tin_bronze"), InfoQuery.SourceChainNode(w, Cfg, Player, "tools"));

        // "Runs in N settlements" is exactly the Production descriptor's targets (CraftingQuery.IsRecipeAvailable).
        int runs = production.Targets.Length;
        Assert.Contains(runs == 0 ? "runs in none" : "holds in " + runs.ToString(System.Globalization.CultureInfo.InvariantCulture), card.StatusLine);
        foreach (SettlementId s in LabourActivities.ControlledSettlements(w, Player))
            SetVariable(w, s, Variables.ArtisanShare, 0.2);
        ActionDescriptor running = AvailableActionsQuery.For(w, Cfg, Player).Single(a => a.Key == "production.toolmaking");
        Assert.True(running.Targets.Length > 0);
        Assert.Contains("holds in " + running.Targets.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Card(w, InfoSubject.OfRecipe("toolmaking")).StatusLine);
    }

    [Fact]
    public void Weaving_IsABaselineCraftWithAnObtainableInput()
    {
        WorldState w = World();
        InfoCard card = Card(w, InfoSubject.OfRecipe("weaving"));
        Assert.Equal(InfoStatus.Known, card.Status);
        Assert.Empty(card.WhyLocked);
        Assert.Null(card.ResearchNode);
        Assert.Contains(card.Requirements, l => l.Subject == InfoSubject.OfGood(Cfg.Goods!.IdOf("fiber")) && l.Met == true);
    }

    [Fact]
    public void GranaryAndWorkshop_AgreeWithConstructionQuery_InEverySettlement()
    {
        WorldState w = World();
        foreach (SettlementId cap in LabourActivities.ControlledSettlements(w, Player))
            foreach (ConstructionProjectEntry p in Cfg.Goods!.Projects!)
            {
                ActionQueryContext q = new(null, 10.0);
                InfoCard card = Card(w, InfoSubject.OfProject(p.Id, cap.Value), q);
                ProjectAvailability av = ConstructionQuery.Availability(w, Cfg, Player, cap, p.Id);
                string? blocker = av == ProjectAvailability.Available ? ConstructionQuery.Blocker(w, Cfg, cap, p, 10.0) : null;
                InfoStatus expect = av == ProjectAvailability.Available ? (blocker is null ? InfoStatus.Available : InfoStatus.Blocked)
                    : av == ProjectAvailability.NotKnowledgeEligible ? InfoStatus.Locked : InfoStatus.Known;
                Assert.Equal(expect, card.Status);
                if (blocker is not null) Assert.Contains(blocker, card.StatusLine);
                ActionDescriptor? listed = AvailableActionsQuery.For(w, Cfg, Player, q).SingleOrDefault(a =>
                    a.Domain is ActionDomain.Construction or ActionDomain.Institutions && a.Id == (((long)cap.Value << 32) | (uint)p.Id));
                Assert.Equal(listed, card.Action);
            }
        SettlementId capital = Capital(w);
        InfoCard granary = Card(w, InfoSubject.OfProject(1, capital.Value));
        Assert.True(Links(granary.Enables, InfoSubject.Milestone(2, "a2_granary")));
        Assert.Contains(granary.Enables, l => l.Subject is null && l.Label.Contains("levy", StringComparison.Ordinal));
        Assert.Contains("holds no goods", granary.RealizedBy);
        Assert.DoesNotContain("store", granary.What + granary.StatusLine, StringComparison.OrdinalIgnoreCase);   // never implies food storage

        InfoCard workshop = Card(w, InfoSubject.OfProject(2, capital.Value));
        Assert.Equal(Key("tin_bronze"), workshop.ResearchNode);
        Assert.Contains(workshop.WhyLocked, l => l.Label.Contains("tools <- Toolmaking <- bronze <- Bronze casting <- Tin bronze (research, Age III)", StringComparison.Ordinal));
    }

    [Fact]
    public void TaxEdict_InItsThreeGateStates_ReadsGovernanceGateOf()
    {
        // NeedsKnowledge: the founded world.
        WorldState w = World();
        Assert.Equal(TaxGate.NeedsKnowledge, Governance.GateOf(w, Cfg, Player));
        InfoCard locked = Card(w, InfoSubject.Tax);
        Assert.Equal(InfoStatus.Locked, locked.Status);
        Assert.Equal("needs Taxation (Civics) and the " + Ages.Age(3).Name + " (Age III)", locked.Summary);
        Assert.Equal(Key("taxation"), locked.ResearchNode);
        Assert.Null(locked.Action);

        // NeedsAge: the knowledge, not the Age.
        GovernanceRigs.GrantKnowledgeOnly(w, Player);
        Assert.Equal(TaxGate.NeedsAge, Governance.GateOf(w, Cfg, Player));
        InfoCard age = Card(w, InfoSubject.Tax);
        Assert.Equal(InfoStatus.NeedsAge, age.Status);
        Assert.Contains(age.WhyLocked, l => l.Subject == InfoSubject.OfAge(3));
        Assert.Null(age.Action);

        // Open: knowledge and Age.
        GovernanceRigs.EnterAge(w, Player, 3);
        Assert.Equal(TaxGate.Open, Governance.GateOf(w, Cfg, Player));
        InfoCard open = Card(w, InfoSubject.Tax);
        Assert.Equal(InfoStatus.Available, open.Status);
        Assert.Equal(AvailableActionsQuery.For(w, Cfg, Player).Single(a => a.Domain == ActionDomain.Governance), open.Action);
        Assert.Empty(open.WhyLocked);
    }

    [Fact]
    public void ASubtreeNodeWithItsPrerequisitesMet_ButTheStageClosed_SaysTheStageIsWhy()
    {
        WorldState w = World();
        bool[] done = ResearchQuery.CompletedMask(w, R, Player);
        int pick = -1;
        for (int i = 0; i < R.Nodes.Count && pick < 0; i++)
            if (R.Nodes[i].Branch >= 0 && R.Nodes[i].PrerequisiteNodes.Count > 0) pick = i;
        Assert.True(pick >= 0);
        foreach (int p in R.Nodes[pick].PrerequisiteNodes) Know(w, R.Nodes[p].Id);
        Assert.False(ResearchQuery.IsStageReached(w, R, Player));
        Assert.True(ResearchQuery.PrerequisitesMet(R, pick, ResearchQuery.CompletedMask(w, R, Player)));
        InfoCard card = Card(w, InfoSubject.Node(R.Nodes[pick].Key));
        Assert.Equal(InfoStatus.Locked, card.Status);
        Assert.Contains(card.WhyLocked, l => l.Label.Contains("research stage", StringComparison.Ordinal));
        Assert.Empty(Subjects(card.WhyLocked, InfoKind.ResearchNode));
        _ = done;
    }

    // ================================================================== properties: the card never disagrees with legality

    private static IEnumerable<WorldState> Worlds()
    {
        yield return World();
        yield return Know(World(), "taxation", "track_road", "tin_bronze", "cereal_cultivation", "pottery_open_fired");
    }

    [Fact]
    public void EveryNode_AgreesWithResearchQuery_OnTwoWorlds()
    {
        foreach (WorldState w in Worlds())
        {
            bool[] done = ResearchQuery.CompletedMask(w, R, Player);
            bool stage = ResearchQuery.StageReached(R, done);
            ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, Player);
            for (int i = 0; i < R.Nodes.Count; i++)
            {
                ResearchNode n = R.Nodes[i];
                InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.Node(n.Key), actions: actions);
                bool available = ResearchQuery.IsAvailable(R, i, done, stage);
                Assert.True(available == card.Status is InfoStatus.Researchable or InfoStatus.Researching, n.Id + " availability");
                Assert.True(done[i] == (card.Status == InfoStatus.Known), n.Id + " completion");
                Assert.True(available == (card.Action is not null), n.Id + " action");
                Assert.Equal(n.Key, card.ResearchNode);

                var unmet = new List<InfoSubject>();
                if (!done[i] && !available && !ResearchQuery.PrerequisitesMet(R, i, done))
                    foreach (ResearchQuery.PrerequisiteAtom a in ResearchQuery.Prerequisites(w, R, Player, n.Key).Atoms)
                        if (!a.Completed) unmet.Add(InfoSubject.Node(a.Node));
                Assert.Equal(unmet, Subjects(card.WhyLocked, InfoKind.ResearchNode));

                var entities = new List<InfoSubject>();
                foreach (int e in n.UnlockedEntities) entities.Add(InfoSubject.OfEntity(R.Entities[e].Id));
                Assert.Equal(entities, Subjects(card.Enables, InfoKind.Entity));

                var milestones = new List<InfoSubject>();
                for (int a = 1; a <= AgeContent.AgeCount; a++)
                    if (Ages.Age(a).Entry is { } entry)
                        foreach (AgeMilestone m in entry.Core.Concat(entry.Supporting))
                            if (m.Fact.Kind == MilestoneFactKind.Research && m.Fact.NodeKeys.Contains(n.Key.Value)) milestones.Add(InfoSubject.Milestone(a, m.Id));
                Assert.Equal(milestones, Subjects(card.Enables, InfoKind.AgeMilestone));

                var dependents = new List<InfoSubject>();
                foreach (int d in n.Dependents) dependents.Add(InfoSubject.Node(R.Nodes[d].Key));
                Assert.Equal(dependents, Subjects(card.Enables, InfoKind.ResearchNode));
            }
        }
    }

    [Fact]
    public void EveryEntity_AgreesWithTheKnowledgeEvaluator_AndNamesEveryAtom()
    {
        foreach (WorldState w in Worlds())
        {
            bool[] done = ResearchQuery.CompletedMask(w, R, Player);
            for (int e = 0; e < R.Entities.Count; e++)
            {
                ResearchEntity ent = R.Entities[e];
                InfoCard card = InfoQuery.Card(w, Cfg, Player, InfoSubject.OfEntity(ent.Id), actions: ImmutableArray<ActionDescriptor>.Empty);
                bool eligible = ResearchQuery.IsKnowledgeEligible(R, e, done);
                Assert.True(eligible == (card.Status == InfoStatus.Known), ent.Id + " eligibility");
                List<InfoSubject> nodes = Subjects(card.EnabledBy, InfoKind.ResearchNode);
                foreach (int a in ent.NodeAtoms) Assert.Contains(InfoSubject.Node(R.Nodes[a].Key), nodes);
                List<InfoSubject> institutions = Subjects(card.EnabledBy, InfoKind.Entity);
                foreach (int inst in ent.InstitutionAtoms)
                {
                    Assert.Contains(InfoSubject.OfEntity(R.Entities[inst].Id), institutions);
                    foreach (int a in R.Entities[inst].NodeAtoms) Assert.Contains(InfoSubject.Node(R.Nodes[a].Key), nodes);   // expanded
                }
                if (!eligible) Assert.NotEmpty(card.WhyLocked);
                if (card.ResearchNode is { } target)
                {
                    var candidates = new List<int>();
                    foreach (InfoSubject s in nodes) candidates.Add(R.IndexOf(new ResearchNodeId((int)s.Id)));
                    Assert.Contains(R.IndexOf(target), candidates);
                }
            }
        }
    }

    /// <summary>The subject each available action is about — the mapping the UI uses when it paints a descriptor.</summary>
    private static List<InfoSubject> SubjectsOf(ActionDescriptor a)
    {
        switch (a.Domain)
        {
            case ActionDomain.Labour: return [InfoSubject.Sector((int)a.Targets[1].Id, (int)a.Targets[0].Id)];
            case ActionDomain.Research when a.Id == 1:
            {
                var r = new List<InfoSubject>();
                foreach (ActionTarget t in a.Targets) r.Add(InfoSubject.Node(new ResearchNodeId((int)t.Id)));
                return r;
            }
            case ActionDomain.Research: return [InfoSubject.Research];
            case ActionDomain.Age: return [InfoSubject.OfAge((int)a.Id)];
            case ActionDomain.Construction or ActionDomain.Institutions: return [InfoSubject.OfProject((int)a.Targets[1].Id, (int)a.Targets[0].Id)];
            case ActionDomain.Roads: return [InfoSubject.OfRoadClass((int)a.Id)];
            case ActionDomain.Military:
            {
                var r = new List<InfoSubject>();
                foreach (ActionTarget t in a.Targets) r.Add(InfoSubject.OfFormation((int)t.Id));
                return r;
            }
            case ActionDomain.Governance: return [InfoSubject.Tax];
            case ActionDomain.Standing: return [InfoSubject.OfBaseline(R.Baseline[(int)a.Id].Id)];
            case ActionDomain.Production: return [InfoSubject.OfRecipe(Cfg.Goods!.Recipes[(int)a.Id - 1].Name)];
            case ActionDomain.Trade: return [InfoSubject.OfEntity(Cfg.Trade.Entity!)];
            default: throw new InvalidOperationException("unmapped domain " + a.Domain);
        }
    }

    [Fact]
    public void EveryAvailableAction_HasACardCarryingExactlyThatDescriptor_AndNoCardClaimsAnUnlistedOrder()
    {
        foreach (WorldState w in new[] { World(), Know(AtAge(World(), 3), "taxation", "track_road", "tin_bronze") })
        {
            var q = new ActionQueryContext(null, 10.0);
            ImmutableArray<ActionDescriptor> actions = AvailableActionsQuery.For(w, Cfg, Player, q);
            // Anti-vacuity: every domain the mapping covers is exercised across the two worlds.
            Assert.Contains(actions, a => a.Domain == ActionDomain.Construction);
            Assert.Contains(actions, a => a.Domain == ActionDomain.Production);
            Assert.Contains(actions, a => a.Domain == ActionDomain.Military);
            if (Governance.GateOf(w, Cfg, Player) == TaxGate.Open)
            {
                Assert.Contains(actions, a => a.Domain == ActionDomain.Governance);
            }
            foreach (ActionDescriptor a in actions)
                foreach (InfoSubject s in SubjectsOf(a))
                {
                    InfoCard card = InfoQuery.Card(w, Cfg, Player, s, q, actions: actions);
                    Assert.True(a.Equals(card.Action), a.Key + " -> " + s + ": card carries " + card.Action?.Key);
                    InfoStatus expect = a.Kind == ActionKind.Standing ? InfoStatus.Known
                        : a.Domain == ActionDomain.Research ? card.Status : a.Blocker is null ? InfoStatus.Available : InfoStatus.Blocked;
                    Assert.True(card.Status == expect || (a.Domain == ActionDomain.Research && card.Status is InfoStatus.Researchable or InfoStatus.Researching or InfoStatus.Available),
                        a.Key + ": status " + card.Status);
                }

            // Conversely: every card that claims an orderable status carries a descriptor the query lists.
            var subjects = new List<InfoSubject> { InfoSubject.Tax, InfoSubject.Research };
            foreach (SettlementId s in LabourActivities.ControlledSettlements(w, Player))
            {
                for (int sector = 0; sector < Sectors.Count; sector++) subjects.Add(InfoSubject.Sector(sector, s.Value));
                foreach (ConstructionProjectEntry p in Cfg.Goods!.Projects!) subjects.Add(InfoSubject.OfProject(p.Id, s.Value));
            }
            foreach (RecipeEntry rec in Cfg.Goods!.Recipes) subjects.Add(InfoSubject.OfRecipe(rec.Name));
            foreach (RoadClassConfig rc in Cfg.Roads!.Classes) subjects.Add(InfoSubject.OfRoadClass(rc.EdgeType));
            foreach (ResearchBaselineCapability b in R.Baseline) subjects.Add(InfoSubject.OfBaseline(b.Id));
            for (int a = 1; a <= AgeContent.AgeCount; a++) subjects.Add(InfoSubject.OfAge(a));
            foreach (UniversityType ut in R.UniversityTypes) subjects.Add(InfoSubject.OfUniversity(ut.Key));
            for (int i = 0; i < w.MilitaryUnits.Count; i++) subjects.Add(InfoSubject.OfFormation(w.MilitaryUnits[i].Id));
            foreach (InfoSubject s in subjects)
            {
                InfoCard card = InfoQuery.Card(w, Cfg, Player, s, q, actions: actions);
                if (card.Status is InfoStatus.Available or InfoStatus.Blocked && s.Kind != InfoKind.Learning)
                    Assert.True(card.Action is not null && actions.Contains(card.Action), s + " claims " + card.Status + " without a listed order");
                if (card.Action is { } act) Assert.Contains(act, actions);
            }
        }
    }

    [Fact]
    public void EveryResearchMilestone_PointsTheTreeAtOneOfItsListedNodes()
    {
        WorldState w = World();
        for (int a = 1; a <= AgeContent.AgeCount; a++)
        {
            if (Ages.Age(a).Entry is not { } entry) continue;
            foreach (AgeMilestone m in entry.Core.Concat(entry.Supporting))
            {
                InfoCard card = Card(w, InfoSubject.Milestone(a, m.Id));
                Assert.Equal(m.Name, card.Title);
                Assert.Equal(AgeQuery.Milestone(w, Player, m).Met, card.Status == InfoStatus.Met);
                if (m.Fact.Kind != MilestoneFactKind.Research) continue;
                Assert.NotNull(card.ResearchNode);
                Assert.Contains(card.ResearchNode!.Value.Value, m.Fact.NodeKeys);
            }
        }
    }

    // ================================================================== determinism, the argmax, no second database

    [Fact]
    public void Cards_AreDeterministic_AndIdenticalAcrossASaveAndLoad()
    {
        WorldState w = Know(World(), "root_crop", "grinding_stone");
        var subjects = new List<InfoSubject>
        {
            InfoSubject.Milestone(2, "a2_cultivation"), InfoSubject.Sector(Sectors.Farming, Capital(w).Value), InfoSubject.OfRecipe("toolmaking"),
            InfoSubject.OfProject(2, Capital(w).Value), InfoSubject.Tax, InfoSubject.OfEntity("inst.university"), InfoSubject.OfAge(2),
            InfoSubject.OfGood(Cfg.Goods!.IdOf("bronze")), InfoSubject.OfRoadClass(2), InfoSubject.OfFormation(w.MilitaryUnits[0].Id),
            InfoSubject.Node(Key("cereal_cultivation")), InfoSubject.OfUniversity(R.UniversityTypes[0].Key), InfoSubject.Research,
        };
        using var buffer = new MemoryStream();
        Snapshot.Save(w, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer, w.Terrain);
        foreach (InfoSubject s in subjects)
        {
            InfoCard a = Card(w, s), b = Card(w, s), c = Card(loaded, s);
            Assert.Equal(a, b);
            Assert.Equal(a, c);
        }
    }

    [Fact]
    public void AConditionsLiveValue_IsTheBestSettlement_WithTheLowerIdWinningATie_TieDense()
    {
        WorldState w = World();
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, Player);
        Assert.True(mine.Length >= 3);
        // Every settlement ties: the lowest id wins, whatever the table order.
        foreach (SettlementId s in mine.Reverse()) SetVariable(w, s, Variables.ArtisanShare, 0.03);
        int lowest = mine.Min(s => s.Value);
        Assert.Equal((0.03, lowest), InfoQuery.BestVariable(w, mine.Reverse().ToArray(), Variables.ArtisanShare));
        Assert.Equal((0.03, lowest), InfoQuery.BestVariable(w, mine, Variables.ArtisanShare));
        // A strictly higher reading wins over the tie.
        SettlementId top = mine.OrderBy(s => s.Value).Last();
        WorldState w2 = World();
        foreach (SettlementId s in mine) SetVariable(w2, s, Variables.ArtisanShare, s == top ? 0.07 : 0.03);
        Assert.Equal((0.07, top.Value), InfoQuery.BestVariable(w2, mine, Variables.ArtisanShare));
        Assert.Contains("0.07 at best", InfoQuery.ConditionReading(w2, Cfg, Player, "artisan_share > 0.05"));
    }

    private static string GoodNamed(GoodId id)
    {
        foreach (GoodEntry g in Cfg.Goods!.Goods) if (g.Id == id.Value) return g.Name;
        throw new InvalidOperationException("no good " + id.Value);
    }

    [Fact]
    public void AStockCondition_ReadsTheDialectsQuantityName_AsTheLargestHoldingInOneSettlement()
    {
        // The research dialect names a quantity "stock_" + the good's name with '-' as '_' (copper-ore ->
        // stock_copper_ore); the reading is that good's largest holding in one of the issuer's settlements.
        WorldState w = World();
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, Player);
        Assert.True(mine.Length >= 2);
        Assert.NotEmpty(R.QuantityGoods);
        int hyphenated = 0;
        var expected = new long[R.QuantityGoods.Count];
        for (int q = 0; q < R.QuantityGoods.Count; q++)
        {
            string name = GoodNamed(R.QuantityGoods[q]);
            if (name.Contains('-')) hyphenated++;
            // Above anything any settlement holds already, so the second settlement's holding is the largest.
            long before = 0;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Good == R.QuantityGoods[q]) before = Math.Max(before, w.GoodStocks[i].Amount.Value);
            expected[q] = before + 40 + q;
            UniversityRigs.TopUp(w, Cfg, mine[0], name, before + 3 + q);
            UniversityRigs.TopUp(w, Cfg, mine[1], name, expected[q]);
        }
        Assert.True(hyphenated > 0, "a hyphenated good is among the dialect's quantities (the case the token must translate)");
        for (int q = 0; q < R.QuantityGoods.Count; q++)
        {
            string name = GoodNamed(R.QuantityGoods[q]);
            string reading = InfoQuery.ConditionReading(w, Cfg, Player, "stock_" + name.Replace('-', '_') + " > 0");
            Assert.Contains(name + " held: " + expected[q].ToString(System.Globalization.CultureInfo.InvariantCulture) + " at most in one settlement", reading);
        }
        Assert.Equal("no_such_token > 0", InfoQuery.ConditionReading(w, Cfg, Player, "no_such_token > 0"));
    }

    [Fact]
    public void InfoQuerySource_NamesNoContentId()
    {
        string source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Sim.Core", "State", "InfoQuery.cs"));
        var ids = new List<string>();
        foreach (ResearchNode n in R.Nodes) ids.Add(n.Id);
        foreach (ResearchEntity e in R.Entities) ids.Add(e.Id);
        foreach (ResearchBaselineCapability b in R.Baseline) ids.Add(b.Id);
        foreach (UniversityType u in R.UniversityTypes) ids.Add(u.Id);
        foreach (GoodEntry g in Cfg.Goods!.Goods) ids.Add(g.Name);
        foreach (RecipeEntry r in Cfg.Goods.Recipes) ids.Add(r.Name);
        foreach (ConstructionProjectEntry p in Cfg.Goods.Projects!) ids.Add(p.Name);
        for (int a = 1; a <= AgeContent.AgeCount; a++)
        {
            ids.Add(Ages.Age(a).Id);
            if (Ages.Age(a).Entry is { } e) foreach (AgeMilestone m in e.Core.Concat(e.Supporting)) ids.Add(m.Id);
        }
        var literals = new List<string>();
        foreach (Match m in Regex.Matches(source, "\"((?:[^\"\\\\]|\\\\.)*)\"")) literals.Add(m.Groups[1].Value);
        Assert.True(literals.Count > 50, "the scan found the file's literals");
        foreach (string lit in literals)
        {
            Assert.DoesNotContain(lit, ids);
            foreach (string id in ids)
                if (id.Contains('_') || id.Contains('.'))
                    Assert.False(Regex.IsMatch(lit, "(^|[^A-Za-z0-9_.])" + Regex.Escape(id) + "($|[^A-Za-z0-9_])"), "literal \"" + lit + "\" names " + id);
        }
    }
}
