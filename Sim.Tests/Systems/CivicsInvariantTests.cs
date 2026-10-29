using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// M6 CIV track (ADR-057; D-066 A2, A6, A10) — THE CIVICS INVARIANT PINS the m6-spec §3 names and no test held before:
/// <list type="bullet">
/// <item>P1 — the civic-state vocabulary (D-D: Available and Adopted distinguished; "Adopted" reserved for M8): a civic is
///   Unresearched, Available or Researched, read from completion and the one eligibility predicate.</item>
/// <item>I-3 — gate on completion, never on a currency: a METAMORPHIC pin — two worlds that differ only in their RP/CP
///   balances (the CP bank, the RP reserve, every partial progress) answer every gate identically.</item>
/// <item>I-6 — Age entry grants nothing, in particular no government form: two worlds that differ only in the polity's
///   stored Age answer every civic state, form line, regime module and label identically.</item>
/// <item>I-7 under D-066 A6 — "optional" = not needed for the next Age: the loader's validator holds on the shipped content
///   and has teeth (a form made a next-Age entry fact is refused).</item>
/// </list>
/// </summary>
public sealed class CivicsInvariantTests
{
    private static readonly PolityId P = new(Player);

    // ================================================================== P1 — the vocabulary

    [Fact]
    public void CivicState_IsUnresearchedAvailableResearched_FromCompletionAndTheEligibilityPredicate()
    {
        ResearchContent c = Standard().Load();
        int law = c.IndexOf(Key(1001)), code = c.IndexOf(Key(1002));
        WorldState w = PlayerWorld();
        // nothing complete: law needs a, code needs law AND c — both Unresearched
        Assert.Equal(CivicState.Unresearched, ResearchQuery.CivicStateOf(w, c, P, Key(1001)));
        Assert.Equal(CivicState.Unresearched, ResearchQuery.CivicStateOf(w, c, P, Key(1002)));
        WithCompleted(w, 1);   // a
        Assert.Equal(CivicState.Available, ResearchQuery.CivicStateOf(w, c, P, Key(1001)));
        Assert.Equal(CivicState.Unresearched, ResearchQuery.CivicStateOf(w, c, P, Key(1002)));
        WithCompleted(w, 1001);   // law complete: RESEARCHED — never "adopted"
        Assert.Equal(CivicState.Researched, ResearchQuery.CivicStateOf(w, c, P, Key(1001)));
        // the mask form is the same function
        bool[] done = ResearchQuery.CompletedMask(w, c, P);
        bool[] avail = ResearchQuery.AvailableMask(c, done);
        Assert.Equal(CivicState.Researched, ResearchQuery.CivicStateOf(done, avail, law));
        Assert.Equal(CivicState.Unresearched, ResearchQuery.CivicStateOf(done, avail, code));
    }

    /// <summary>D-D: "Adopted" is reserved for M8 — the vocabulary has no such member, so nothing in M6 can return or show it.</summary>
    [Fact]
    public void CivicState_HasNoAdoptedMember_AdoptionIsReservedForM8()
    {
        Assert.Equal(["Unresearched", "Available", "Researched"], Enum.GetNames<CivicState>());
        Assert.DoesNotContain(Enum.GetNames<InstitutionLifecycle>(), n => n.Contains("Adopt", StringComparison.Ordinal));
    }

    // ================================================================== I-3 — no currency gates anything

    /// <summary>
    /// m6-spec I-3 (exit criterion 2: "no currency gates anything"). METAMORPHIC: the shipped AI world at turn 24, and its twin
    /// with every currency balance of every polity moved — the CP bank raised by 10^9 milli-CP (generated raised with it, so
    /// the CP identity still holds), an RP reserve row of 10^9 milli-RP, and every incomplete node given progress up to just
    /// under its effective cost. Completion is untouched. Every gate must answer identically: research availability and every
    /// civic state, every construction project's availability at every settlement, the Age eligibility report, the regime
    /// modules, label and forms, and the set of legal actions (domain, id, key) on the action surface.
    /// </summary>
    [Fact]
    public void NoCurrencyGatesAnything_Metamorphic_BalancesMoveNoGate()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = ShippedAiWorld(cfg, 42, turns: 24);
        WorldState twin = w.Clone();
        ResearchContent rc = cfg.Research!;
        for (int p = 0; p < twin.Polities.Count; p++)
        {
            PolityId polity = twin.Polities[p].Id;
            bool cpRow = false;
            for (int i = 0; i < twin.CivicsPoints.Count; i++)
            {
                if (twin.CivicsPoints[i].Polity.Value != polity.Value) continue;
                ref CivicsPointsRow row = ref twin.CivicsPoints.Ref(i);
                row.BalanceMilli += 1_000_000_000L;
                row.GeneratedMilli += 1_000_000_000L;
                cpRow = true;
            }
            if (!cpRow) twin.CivicsPoints.Add(new CivicsPointsRow(polity, 1_000_000_000L, 1_000_000_000L, 0L));
            twin.ResearchReserves.Add(new ResearchReserveRow(polity, 1_000_000_000L));
            bool[] done = ResearchQuery.CompletedMask(twin, rc, polity);
            for (int i = 0; i < rc.Nodes.Count; i++)
            {
                if (done[i]) continue;
                double almost = ResearchQuery.EffectiveCost(twin, rc, polity, i) * 0.999;
                SetProgress(twin, polity, rc.Nodes[i].Key, almost);
            }
        }
        Assert.Null(ResearchQuery.CivicsPointsInvariantViolation(twin.CivicsPoints));
        Assert.NotEqual(WorldHash.ComputeHex(w), WorldHash.ComputeHex(twin));   // the twin really differs

        for (int p = 0; p < w.Polities.Count; p++)
        {
            PolityId polity = w.Polities[p].Id;
            Assert.Equal(ResearchQuery.AvailableMask(w, rc, polity), ResearchQuery.AvailableMask(twin, rc, polity));
            Assert.Equal(ResearchQuery.CompletedMask(w, rc, polity), ResearchQuery.CompletedMask(twin, rc, polity));
            for (int i = 0; i < rc.Nodes.Count; i++)
                if (rc.Nodes[i].Tree == ResearchTree.Civics)
                    Assert.Equal(ResearchQuery.CivicStateOf(w, rc, polity, rc.Nodes[i].Key), ResearchQuery.CivicStateOf(twin, rc, polity, rc.Nodes[i].Key));
            for (int s = 0; s < w.Settlements.Count; s++)
                foreach (ConstructionProjectEntry project in cfg.Goods!.Projects!)
                    Assert.Equal(ConstructionQuery.IsProjectAvailable(w, cfg, polity, w.Settlements[s].Id, project.Id),
                        ConstructionQuery.IsProjectAvailable(twin, cfg, polity, w.Settlements[s].Id, project.Id));
            Assert.Equal(AgeFlags(w, cfg, polity), AgeFlags(twin, cfg, polity));
            Assert.Equal(RegimeQuery.Label(w, cfg, polity), RegimeQuery.Label(twin, cfg, polity));
            Assert.Equal(RegimeQuery.Modules(w, cfg, polity), RegimeQuery.Modules(twin, cfg, polity));
            Assert.Equal(RegimeQuery.Forms(w, cfg, polity), RegimeQuery.Forms(twin, cfg, polity));
            Assert.Equal(Legal(w, cfg, polity), Legal(twin, cfg, polity));
        }
    }

    // ================================================================== I-6 — Age entry grants no form

    /// <summary>
    /// m6-spec I-6 ("Completing an Age grants nothing by itself; in particular no government form"; exit criterion 3: "Age
    /// entry grants no form"). METAMORPHIC over the stored Age: the shipped AI world at turn 24 and its twins whose polities
    /// are stored at every Age 1..9 (AgeStates) answer every civic state, every government form's state and line, the regime
    /// modules and the derived label identically — and no form is ever anything but Unresearched / Available / Researched (a
    /// researched one reads "Available - adoption in M8").
    /// </summary>
    [Fact]
    public void AgeEntryGrantsNoForm_Metamorphic_TheStoredAgeMovesNoCivicNoFormNoModule()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = ShippedAiWorld(cfg, 42, turns: 24);
        ResearchContent rc = cfg.Research!;
        Assert.NotEmpty(cfg.Regime!.Forms);
        for (int age = 1; age <= AgeContent.AgeCount; age++)
        {
            WorldState twin = w.Clone();
            twin.AgeStates.Clear();
            for (int p = 0; p < twin.Polities.Count; p++) twin.AgeStates.Add(new AgeStateRow(twin.Polities[p].Id, age, 0, 0, 0));
            for (int p = 0; p < w.Polities.Count; p++)
            {
                PolityId polity = w.Polities[p].Id;
                Assert.Equal(age, AgeQuery.CurrentAge(twin, cfg.Ages!, polity));
                Assert.Equal(ResearchQuery.CompletedMask(w, rc, polity), ResearchQuery.CompletedMask(twin, rc, polity));
                Assert.Equal(ResearchQuery.AvailableMask(w, rc, polity), ResearchQuery.AvailableMask(twin, rc, polity));
                GovernmentFormView[] forms = RegimeQuery.Forms(twin, cfg, polity);
                Assert.Equal(RegimeQuery.Forms(w, cfg, polity), forms);
                foreach (GovernmentFormView f in forms)
                    Assert.Equal(f.State == CivicState.Researched, f.Line == "Available - adoption in M8");
                Assert.Equal(RegimeQuery.Modules(w, cfg, polity), RegimeQuery.Modules(twin, cfg, polity));
                Assert.Equal(RegimeQuery.Label(w, cfg, polity), RegimeQuery.Label(twin, cfg, polity));
            }
        }
    }

    // ================================================================== I-7 under D-066 A6 — optional = not needed for the next Age

    /// <summary>D-066 A6: on the shipped content no government form is necessary for entering the Age after its own (or an
    /// earlier one) — the validator the loader runs returns nothing, and every one of the 33 forms is a civic.</summary>
    [Fact]
    public void Optionality_ShippedContent_NoFormIsNeededForTheNextAge()
    {
        SimConfig cfg = TestConfigs.Sim();
        Assert.Equal(33, cfg.Regime!.Forms.Length);
        foreach (string id in cfg.Regime.Forms)
            Assert.Equal(ResearchTree.Civics, cfg.Research!.Nodes[cfg.Research.IndexOfId(id)].Tree);
        Assert.Empty(RegimeContent.OptionalityViolations(cfg));
    }

    /// <summary>
    /// The validator's TEETH and AIM. (a) Each Age's necessary set really contains what its CORE research facts name, and —
    /// with the Age sweep on — every sweep-cohort node of two Ages below a core node's Age ("nothing is optional across two
    /// Ages", D-063 R2): a form of Age x is necessary for entering Age x + 2 whenever a core node of Age x + 2 exists, and the
    /// validator does NOT flag that (only y ≤ x + 1 is a violation). (b) A form made a CORE research fact of the next Age's
    /// entry is flagged — and the six-file load refuses such content.
    /// </summary>
    [Fact]
    public void Optionality_Validator_HasTeeth_AFormMadeANextAgeCoreFactIsRefused()
    {
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent rc = cfg.Research!;
        AgeContent ages = cfg.Ages!;
        // (a) aim: the core facts are necessary; the sweep closure two Ages down is necessary.
        for (int age = 2; age <= AgeContent.AgeCount; age++)
        {
            if (ages.Age(age).Entry is not { } entry) continue;
            bool[] needed = RegimeContent.NecessaryForEntry(rc, cfg.Goods, entry);
            foreach (AgeMilestone m in entry.Core)
                if (m.Fact.Kind == MilestoneFactKind.Research && m.Fact.NodeKeys.Count == 1)
                {
                    int core = rc.IndexOf(new ResearchNodeId(m.Fact.NodeKeys[0]));
                    Assert.True(needed[core], $"Age {age} core node {rc.Nodes[core].Id} must be necessary");
                    int cohort = rc.Nodes[core].AgeNumber - 2;
                    if (cohort >= 1)
                        for (int i = 0; i < rc.Nodes.Count; i++)
                            if (rc.Nodes[i].AgeNumber == cohort && ResearchQuery.IsSweepCohortMember(rc.Nodes[i]))
                                Assert.True(needed[i], $"the Age {cohort} sweep node {rc.Nodes[i].Id} must be necessary for Age {age}");
                }
        }
        // (b) teeth: put a form of Age x into the CORE facts of Age x + 1's entry.
        string form = cfg.Regime!.Forms[0];
        int f = rc.IndexOfId(form);
        int next = rc.Nodes[f].AgeNumber + 1;
        AgeEntryRequirements e = ages.Age(next).Entry!;
        var fact = new AgeMilestoneFact(MilestoneFactKind.Research, [rc.Nodes[f].Key.Value], [form], null, [], 1, -1, null, "research");
        var forced = new AgeMilestone("test_form", "A form", "", AgeMilestoneCategory.InstitutionalSocial, true, fact);
        var ageList = new List<AgeDefinition>(ages.Ages);
        ageList[next - 1] = ages.Age(next) with { Entry = e with { Core = [.. e.Core, forced] } };
        var bent = new AgeContent
        {
            Ages = ageList, Categories = ages.Categories, Surges = ages.Surges, FoundingAge = ages.FoundingAge,
            FoundingAgeBasis = ages.FoundingAgeBasis, SurgeShape = ages.SurgeShape, Status = ages.Status,
        };
        IReadOnlyList<string> violations = RegimeContent.OptionalityViolations(cfg with { Ages = bent });
        Assert.Single(violations);
        Assert.Contains($"'{form}'", violations[0]);
        Assert.Contains($"entering Age {next}", violations[0]);
    }

    // ================================================================== helpers

    /// <summary>The shipped canonical world (1 AI Empire) stepped <paramref name="turns"/> turns under the AI's own orders.</summary>
    internal static WorldState ShippedAiWorld(SimConfig cfg, ulong seed, int turns)
    {
        WorldgenConfig wg = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, wg)), log);
        WorldState w = WorldFounding.Found(wg with { AiEmpires = 1 }, cfg, seed);
        for (int t = 0; t < turns; t++)
        {
            AiOrders.Append(log, w, cfg, wg);
            w = exec.Step(w);
        }
        return w;
    }

    private static void SetProgress(WorldState w, PolityId polity, ResearchNodeId node, double progress)
    {
        for (int i = 0; i < w.ResearchProgress.Count; i++)
            if (w.ResearchProgress[i].Polity.Value == polity.Value && w.ResearchProgress[i].Node.Value == node.Value)
            {
                w.ResearchProgress.Ref(i).Progress = progress;
                return;
            }
        w.ResearchProgress.Add(new ResearchProgressRow(polity, node, progress));
    }

    private static string AgeFlags(IReadOnlyWorldState w, SimConfig cfg, PolityId polity)
    {
        if (AgeQuery.Evaluate(w, cfg.Ages!, polity) is not { } r) return "none";
        var sb = new System.Text.StringBuilder();
        sb.Append(r.Eligible).Append('|').Append(r.CoreMet).Append('|').Append(r.SupportingMet).Append('|').Append(r.CategoryMask).Append('|');
        foreach (MilestoneStatus m in r.Core) sb.Append(m.Met ? '1' : '0');
        sb.Append('|');
        foreach (MilestoneStatus m in r.Supporting) sb.Append(m.Met ? '1' : '0');
        sb.Append('|').Append(string.Join(",", r.Remaining));
        return sb.ToString();
    }

    private static string[] Legal(IReadOnlyWorldState w, SimConfig cfg, PolityId polity)
    {
        var keys = new List<string>();
        foreach (ActionDescriptor a in AvailableActionsQuery.For(w, cfg, polity))
            keys.Add(((int)a.Domain).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
                     + a.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + a.Key);
        return [.. keys];
    }
}
