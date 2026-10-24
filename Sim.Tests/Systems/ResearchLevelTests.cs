using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-049 (D-063 R4, CR-024; tranche-2 contract §4) — REPEATABLE RESEARCH LEVELS, engine semantics on a rig built from
/// <see cref="ResearchRigs.Standard"/> with three levelled repeatables:
/// <code>
///  rep        key 12, engineering subtree, requires eng, BaseCost 1 000, costGrowth 1.25, farming.yieldPerArableKm2PerYear k 0.5
///  trunkrep   key 13, trunk,              requires c,   BaseCost 1 000, costGrowth 1.2,  research.researchPointsCoefficient k 0.1
///  fin9, fin9b keys 1003, 1004: finite A9 civics (require law)
///  reform     key 1990, civic A9, requires law, BaseCost 1 000, costGrowth 1.2, governance.authorityDecayCostUnits k 0.1
/// </code>
/// costGrowth 1.25 and k 0.5 are exact binary fractions, so every expected cost and contribution below is exact (no epsilon).
/// The world makes 2 000 RP a turn (40 000 people at the rig's 10 × P^0.5), so each of the first three levels of rep
/// (1 000, 1 250, 1 562.5) completes in one turn.
/// </summary>
public class ResearchLevelTests
{
    private const int Rep = 12, TrunkRep = 13, Fin9 = 1003, Fin9b = 1004, Reform = 1990;
    private const string Farming = "farming.yieldPerArableKm2PerYear";
    private static readonly PolityId P1 = new(Player);

    private static Spec LevelSpec(bool? levelsOn = true)
    {
        Spec s = Standard();
        s.RepeatableLevels = levelsOn;
        s.Technologies.Add(new Node(Rep, "rep", "eng", Cost: 1000.0, Branch: "engineering", Age: "A9",
            Levels: new LevelRule(1.25, [(Farming, 0.5)])));
        s.Technologies.Add(new Node(TrunkRep, "trunkrep", "c", Cost: 1000.0, Age: "A9",
            Levels: new LevelRule(1.2, [("research.researchPointsCoefficient", 0.1)])));
        s.Civics.Add(new Node(Fin9, "fin9", "law", Age: "A9"));
        s.Civics.Add(new Node(Fin9b, "fin9b", "law", Age: "A9"));
        s.Civics.Add(new Node(Reform, "reform", "law", Cost: 1000.0, Age: "A9",
            Levels: new LevelRule(1.2, [("governance.authorityDecayCostUnits", 0.1)])));
        s.Recursive.AddRange(["rep", "trunkrep", "reform"]);
        return s;
    }

    private static readonly ResearchContent On = LevelSpec().Load();
    private static readonly ResearchContent Off = LevelSpec(levelsOn: false).Load();
    private static readonly ResearchContent Absent = LevelSpec(levelsOn: null).Load();

    /// <summary>40 000 people (2 000 RP a turn) with a, b, c (the stage) and eng complete: rep and trunkrep are open.</summary>
    private static WorldState Ready() => WithCompleted(World([Player], [Player], 40_000), 1, 2, 3, 8);

    private static OrderLog TargetEveryTurn(int key, int turns)
    {
        var log = new OrderLog();
        for (int t = 0; t < turns; t++) log.Append(Target(t, key));
        return log;
    }

    private static int CompletedRows(IReadOnlyWorldState w, int key)
    {
        int n = 0;
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (w.ResearchCompleted[i].Polity.Value == Player && w.ResearchCompleted[i].Node.Value == key) n++;
        return n;
    }

    private static int Index(ResearchContent c, int key) => c.IndexOf(new ResearchNodeId(key));

    private static bool Available(IReadOnlyWorldState w, ResearchContent c, int key) =>
        Array.IndexOf(ResearchQuery.AvailableNodes(w, c, P1), new ResearchNodeId(key)) >= 0;

    // ------------------------------------------------------------------ the loader

    [Fact]
    public void Loader_ReadsTheLevelRule_AndTheSwitch()
    {
        Assert.True(On.Tuning.RepeatableLevels);
        Assert.False(Off.Tuning.RepeatableLevels);
        Assert.False(Absent.Tuning.RepeatableLevels);
        RepeatableLevels rule = On.Nodes[Index(On, Rep)].Levels!;
        Assert.Equal(1.25, rule.CostGrowth);
        Assert.Equal(new[] { new RepeatableLevelEffect(Farming, 0.5) }, rule.Effects);
        Assert.True(ResearchQuery.IsLevelled(On, Index(On, Rep)));
        Assert.False(ResearchQuery.IsLevelled(Off, Index(Off, Rep)));   // the rule is data; the switch decides
        Assert.False(ResearchQuery.IsLevelled(On, Index(On, 1)));
        Assert.Equal(new[] { Index(On, Fin9), Index(On, Fin9b) }, On.FiniteFinalAgeCivics);
        Assert.Equal(47, RepeatableOperands.All.Count);   // the contract §1.1 registry, row for row
    }

    [Theory]
    [InlineData(1.09, 0.1, "costGrowth")]
    [InlineData(1.31, 0.1, "costGrowth")]
    [InlineData(1.2, 0.0, "gapFraction")]
    [InlineData(1.2, 0.51, "gapFraction")]
    public void Loader_RefusesALevelRuleOutOfRange(double growth, double k, string field)
    {
        Spec s = LevelSpec();
        s.Technologies[^2] = s.Technologies[^2] with { Levels = new LevelRule(growth, [(Farming, k)]) };
        var e = Assert.Throws<ResearchContentException>(() => s.Load());
        Assert.Contains(field, e.Message);
    }

    [Fact]
    public void Loader_RefusesAnUnknownOperand_ATwiceListedOperand_AnUnlock_AndANonFinalAgeCivic()
    {
        Spec unknown = LevelSpec();
        unknown.Technologies[^2] = unknown.Technologies[^2] with { Levels = new LevelRule(1.2, [("farming.magic", 0.1)]) };
        Assert.Contains("operand registry", Assert.Throws<ResearchContentException>(() => unknown.Load()).Message);

        Spec twice = LevelSpec();
        twice.Technologies[^2] = twice.Technologies[^2] with { Levels = new LevelRule(1.2, [(Farming, 0.1), (Farming, 0.2)]) };
        Assert.Contains("listed twice", Assert.Throws<ResearchContentException>(() => twice.Load()).Message);

        // A levelled node never unlocks an entity: a level would re-grant nothing, so a one-time unlock is refused outright.
        Spec unlock = LevelSpec();
        unlock.Entities.Add(new Entity("building.lab", "building", "rep"));
        Assert.Contains("operand effects only", Assert.Throws<ResearchContentException>(() => unlock.Load()).Message);

        Spec early = LevelSpec();
        early.Civics[^1] = early.Civics[^1] with { Age = "A8" };
        Assert.Contains("final Age", Assert.Throws<ResearchContentException>(() => early.Load()).Message);
    }

    // ------------------------------------------------------------------ completion: levels 1, 2, 3

    [Fact]
    public void RepeatedCompletion_RecordsLevels1To3_CostGrowsExactly_OneCompletedRow_StaysAvailable()
    {
        TurnExecutor ex = Executor(On, TargetEveryTurn(Rep, 3));
        WorldState w = Ready();
        int i = Index(On, Rep);
        Assert.Equal(0, ResearchQuery.Level(w, P1, Key(Rep)));
        Assert.Equal(1000.0, ResearchQuery.EffectiveCost(w, On, P1, i));
        double[] nextCost = [1250.0, 1562.5, 1953.125];   // 1 000 × 1.25^n — exact binary fractions
        for (int level = 1; level <= 3; level++)
        {
            WorldState prev = w;
            w = ex.Step(w);
            Assert.Equal(level, ResearchQuery.Level(w, P1, Key(Rep)));
            Assert.Equal(1, CompletedRows(w, Rep));                                   // never a second completed row
            Assert.Equal(nextCost[level - 1], ResearchQuery.EffectiveCost(w, On, P1, i));
            Assert.Equal(1000.0 * Math.Pow(1.25, level), ResearchQuery.LevelBaseCost(On, i, level + 1));
            Assert.True(Available(w, On, Rep), $"rep must stay available after level {level}");
            Assert.Equal(0.0, Progress(w, Rep));                                      // progress restarts every level
            Assert.False(ResearchQuery.TryGetTarget(w, P1, out _));                   // a level is a completion: target cleared
            // The completion EVENT is the first level only: later levels append nothing to the completed relation.
            Assert.Equal(level == 1 ? new[] { Key(Rep) } : Array.Empty<ResearchNodeId>(), ResearchQuery.CompletedBetween(prev, w, P1));
        }
        Assert.Equal(1, w.ResearchLevels.Count);                                       // one row, replaced in place
        Assert.Equal(new ResearchLevelRow(P1, Key(Rep), 3), w.ResearchLevels[0]);
        Assert.Equal(1000.0, ResearchQuery.LevelBaseCost(On, i, 1));
        // The breakdown prices the NEXT level (level 4).
        Assert.Equal(1953.125, ResearchQuery.EffectiveCostBreakdown(w, On, P1, Key(Rep)).BaseCost);
    }

    [Fact]
    public void ALevelTakesSeveralTurns_ProgressIsKeptWithinALevel_AndResetAfterIt()
    {
        // 1 000 RP a turn: level 1 (1 000) in one turn; level 2 (1 250) in two — 1 000 then the remaining 250.
        TurnExecutor ex = Executor(On, TargetEveryTurn(Rep, 4));
        WorldState w = WithCompleted(PlayerWorld(), 1, 2, 3, 8);
        w = ex.Step(w);
        Assert.Equal(1, ResearchQuery.Level(w, P1, Key(Rep)));
        w = ex.Step(w);
        Assert.Equal(1, ResearchQuery.Level(w, P1, Key(Rep)));
        Assert.Equal(1000.0, Progress(w, Rep));
        w = ex.Step(w);
        Assert.Equal(2, ResearchQuery.Level(w, P1, Key(Rep)));
        Assert.Equal(0.0, Progress(w, Rep));
    }

    [Fact]
    public void Levels_NeverReGrantAnything_TheCompletedKnowledgeIsTheFirstLevelOnly()
    {
        WorldState w = Ready();
        w = Run(Executor(On, TargetEveryTurn(Rep, 3)), w, 3);
        Assert.Equal(3, ResearchQuery.Level(w, P1, Key(Rep)));
        string[] caps = ResearchQuery.UnlockedCapabilities(w, On, P1);
        Assert.Single(caps, c => c == "rep capability");                              // declared once, however many levels
        Assert.Empty(On.Nodes[Index(On, Rep)].UnlockedEntities);                      // and it unlocks no entity at all
        Assert.Equal(5, w.ResearchCompleted.Count);                                   // a b c eng + rep's ONE row
    }

    [Fact]
    public void SwitchOff_ARepeatableCompletesOnce_NoLevelRow_NeverAvailableAgain()
    {
        foreach (ResearchContent c in new[] { Off, Absent })
        {
            WorldState w = Run(Executor(c, TargetEveryTurn(Rep, 3)), Ready(), 3);
            Assert.Equal(1, CompletedRows(w, Rep));
            Assert.Equal(0, w.ResearchLevels.Count);
            Assert.False(Available(w, c, Rep));
            Assert.Equal(1, ResearchQuery.Level(w, P1, Key(Rep)));                   // completed once = level 1 by the fallback
            Assert.Equal(1000.0, ResearchQuery.EffectiveCost(w, c, P1, Index(c, Rep)));
            Assert.Null(ResearchQuery.TargetOrder(w, c, P1, Key(Rep)));
            Assert.Equal(0.0, ResearchQuery.LevelContribution(w, c, P1, Farming, 3.0));
        }
    }

    [Fact]
    public void SwitchOff_IsByteIdenticalToTheContentWithoutAnySwitch()
    {
        // The explicit false and the absent switch are one engine: the same orders give the same bytes.
        OrderLog orders = TargetEveryTurn(Rep, 4);
        WorldState off = Run(Executor(Off, orders), Ready(), 4);
        WorldState absent = Run(Executor(Absent, orders), Ready(), 4);
        Assert.Equal(WorldHash.ComputeHex(absent), WorldHash.ComputeHex(off));
        Assert.Equal(0, off.ResearchLevels.Count);
    }

    [Fact]
    public void ALevelCompletedBeforeLevelsExisted_CountsAsLevel1()
    {
        // A v35 save (or the switch off) has rep completed with no level row: it reads level 1, its next level is level 2.
        WorldState w = WithCompleted(Ready(), Rep);
        Assert.Equal(1, ResearchQuery.Level(w, P1, Key(Rep)));
        Assert.Equal(1250.0, ResearchQuery.EffectiveCost(w, On, P1, Index(On, Rep)));
        w = Executor(On, TargetEveryTurn(Rep, 1)).Step(w);
        Assert.Equal(2, ResearchQuery.Level(w, P1, Key(Rep)));
        Assert.Equal(1, CompletedRows(w, Rep));
    }

    // ------------------------------------------------------------------ availability

    [Fact]
    public void TechnologyRepeatables_KeepRuling3_AndATrunkRepeatableNeedsItsOwnPrerequisitesOnly()
    {
        WorldState w = WithCompleted(World([Player], [Player], 40_000), 1, 2);
        Assert.False(Available(w, On, TrunkRep));                                       // needs c
        Assert.False(Available(w, On, Rep));
        WithCompleted(w, 3);
        Assert.True(Available(w, On, TrunkRep));                                        // c alone opens the trunk one
        Assert.False(Available(w, On, Rep));                                            // eng (its subtree) is not done
        WithCompleted(w, 8);
        Assert.True(Available(w, On, Rep));
    }

    [Fact]
    public void ACivicsRepeatable_WaitsForEveryFiniteA9Civic()
    {
        WorldState w = WithCompleted(World([Player], [Player], 40_000), 1, 1001);       // a, law: its own prerequisite
        Assert.False(Available(w, On, Reform));
        Assert.Equal(0, Executor(On, TargetEveryTurn(Reform, 1)).Step(w).ResearchTargets.Count);   // the order is ignored
        WithCompleted(w, Fin9);
        Assert.False(Available(w, On, Reform));
        Assert.Null(ResearchQuery.TargetOrder(w, On, P1, Key(Reform)));
        WithCompleted(w, Fin9b);
        Assert.True(Available(w, On, Reform));
        Assert.True(ResearchQuery.FinalAgeCivicsExhausted(On, ResearchQuery.CompletedMask(w, On, P1)));
        // ...and it levels like a technology repeatable (one-pool model in the rig: RP funds it).
        w = Run(Executor(On, TargetEveryTurn(Reform, 2)), w, 2);
        Assert.Equal(2, ResearchQuery.Level(w, P1, Key(Reform)));
        Assert.Equal(1, CompletedRows(w, Reform));
        Assert.True(Available(w, On, Reform));
    }

    [Fact]
    public void TheLevelCap_StopsAvailability_AndKeepsEveryCostFinite()
    {
        WorldState w = WithCompleted(Ready(), Rep);
        w.ResearchLevels.Add(new ResearchLevelRow(P1, Key(Rep), ResearchQuery.MaxRepeatableLevel - 1));
        Assert.True(Available(w, On, Rep));
        Assert.NotNull(ResearchQuery.TargetOrder(w, On, P1, Key(Rep)));
        w.ResearchLevels[0] = new ResearchLevelRow(P1, Key(Rep), ResearchQuery.MaxRepeatableLevel);
        Assert.False(Available(w, On, Rep));
        Assert.Null(ResearchQuery.TargetOrder(w, On, P1, Key(Rep)));
        Assert.Null(ResearchQuery.LevelsInvariantViolation(w.ResearchLevels));
        // The steepest legal growth (1.30) keeps the cap level's cost finite.
        Spec steep = LevelSpec();
        steep.Technologies[^2] = steep.Technologies[^2] with { Levels = new LevelRule(1.30, [(Farming, 0.5)]) };
        ResearchContent c = steep.Load();
        double top = ResearchQuery.LevelBaseCost(c, Index(c, Rep), ResearchQuery.MaxRepeatableLevel);
        Assert.True(double.IsFinite(top) && top > 1e100, $"cap-level cost {top}");
        Assert.True(double.IsFinite(ResearchQuery.EffectiveCost(w, c, P1, Index(c, Rep))));
    }

    // ------------------------------------------------------------------ the contribution formula

    [Fact]
    public void Contribution_IsCapTimesOneMinusOneMinusKToTheN_ExactForLevels1To3()
    {
        // k = 0.5, cap 2: levels add 1, 0.5, 0.25 — the closed form and the per-level sum agree exactly.
        Assert.Equal(0.0, ResearchQuery.RepeatableContribution(0, 0.5, 2.0));
        double[] expected = [1.0, 1.5, 1.75];
        double sum = 0.0;
        for (int n = 1; n <= 3; n++)
        {
            sum += ResearchQuery.RepeatableLevelBenefit(n, 0.5, 2.0);
            Assert.Equal(expected[n - 1], ResearchQuery.RepeatableContribution(n, 0.5, 2.0));
            Assert.Equal(expected[n - 1], sum);
        }
        // k = 0.1, cap 3 (the shipped gapFraction): the documented closed form, term for term.
        for (int n = 1; n <= 3; n++)
            Assert.Equal(3.0 * (1.0 - Math.Pow(0.9, n)), ResearchQuery.RepeatableContribution(n, 0.1, 3.0));
        Assert.Equal(3.0 * 0.1, ResearchQuery.RepeatableLevelBenefit(1, 0.1, 3.0));
        Assert.Equal(3.0 * 0.1 * Math.Pow(0.9, 2), ResearchQuery.RepeatableLevelBenefit(3, 0.1, 3.0));
    }

    [Fact]
    public void Contribution_IsMonotone_AndNeverExceedsTheCap_StrictlyBelowItWhileTheGapIsRepresentable()
    {
        double previous = 0.0;
        for (int n = 1; n <= ResearchQuery.MaxRepeatableLevel; n++)
        {
            double c = ResearchQuery.RepeatableContribution(n, 0.1, 3.0);
            Assert.True(c >= previous && c <= 3.0, $"level {n}: {c}");
            if (n <= 100) Assert.True(c < 3.0 && c > previous, $"level {n} must still close part of the gap");
            previous = c;
        }
        Assert.True(ResearchQuery.RepeatableContribution(ResearchQuery.MaxRepeatableLevel, 0.5, 1.0) <= 1.0);
    }

    [Fact]
    public void LevelContribution_SumsTheHoldersLevelsOfTheOperand()
    {
        WorldState w = Run(Executor(On, TargetEveryTurn(Rep, 3)), Ready(), 3);
        Assert.Equal(ResearchQuery.RepeatableContribution(3, 0.5, 3.0), ResearchQuery.LevelContribution(w, On, P1, Farming, 3.0));
        Assert.Equal(2.625, ResearchQuery.LevelContribution(w, On, P1, Farming, 3.0));   // 3 × (1 − 0.125)
        Assert.Equal(0.0, ResearchQuery.LevelContribution(w, On, P1, "research.researchPointsCoefficient", 0.3));
        Assert.Equal(0.0, ResearchQuery.LevelContribution(w, On, new PolityId(9), Farming, 3.0));
    }

    // ------------------------------------------------------------------ determinism

    [Fact]
    public void Replay_IsDeterministic_TheSameOrdersGiveTheSameLevelsAndBytes()
    {
        var orders = new OrderLog();
        for (int t = 0; t < 6; t++) orders.Append(Target(t, t == 3 ? TrunkRep : Rep));
        WorldState a = Run(Executor(On, orders), Ready(), 6);
        WorldState b = Run(Executor(On, orders), Ready(), 6);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.True(WorldStates.StateEquals(a, b));
        Assert.True(a.ResearchLevels.Count >= 1);
        Assert.True(ResearchQuery.Level(a, P1, Key(Rep)) >= 3);
    }

    // ------------------------------------------------------------------ the AI (composite key, tie-dense)

    [Fact]
    public void Ai_WithEveryFiniteNodeDone_TargetsTheCheapestNextLevel_TiesToTheLowerKey()
    {
        // Trunk repeatables r1, r2, r3 (keys 2, 3, 4) at BaseCost 1 000 and costGrowth 1.2 — a DENSE tie at every level.
        var spec = new Spec { Stage = "a", RepeatableLevels = true };
        spec.Technologies.AddRange([
            new Node(1, "a"),
            new Node(2, "r1", "a", Cost: 1000.0, Levels: new LevelRule(1.2, [(Farming, 0.1)])),
            new Node(3, "r2", "a", Cost: 1000.0, Levels: new LevelRule(1.2, [(Farming, 0.1)])),
            new Node(4, "r3", "a", Cost: 1000.0, Levels: new LevelRule(1.2, [(Farming, 0.1)])),
        ]);
        spec.Civics.Add(new Node(1001, "law", "a"));
        spec.Recursive.AddRange(["r1", "r2", "r3"]);
        ResearchContent content = spec.Load();
        SimConfig cfg = TestConfigs.Sim() with { Research = content, Ages = null, Roads = null, Governance = null, Institutions = null };
        var ai = new PolityId(2);
        WorldState w = World([Player, 2], [Player, 2], 10_000);
        w.ResearchCompleted.Add(new ResearchCompletedRow(ai, new ResearchNodeId(1)));
        w.ResearchCompleted.Add(new ResearchCompletedRow(ai, new ResearchNodeId(1001)));

        int Pick() => AiResearchPolicy.Decide(w, cfg, ai)!.Value.TargetId;
        void Level(int key, int level)
        {
            if (level == 1) w.ResearchCompleted.Add(new ResearchCompletedRow(ai, new ResearchNodeId(key)));
            for (int r = 0; r < w.ResearchLevels.Count; r++)
                if (w.ResearchLevels[r].Polity.Value == ai.Value && w.ResearchLevels[r].Node.Value == key)
                { w.ResearchLevels[r] = new ResearchLevelRow(ai, new ResearchNodeId(key), level); return; }
            w.ResearchLevels.Add(new ResearchLevelRow(ai, new ResearchNodeId(key), level));
        }

        Assert.Equal(2, Pick());               // three-way tie at 1 000: the lower key
        Level(2, 1);
        Assert.Equal(3, Pick());               // r1 now costs 1 200; r2 and r3 tie at 1 000: r2
        Level(3, 1);
        Assert.Equal(4, Pick());               // only r3 is still at 1 000
        Level(4, 1);
        Assert.Equal(2, Pick());               // all three at 1 200: back to the lowest key
        Level(2, 2);
        Level(3, 2);
        Assert.Equal(4, Pick());               // 1 440, 1 440, 1 200: the strictly cheaper higher key
        Assert.Equal(ResearchQuery.TargetOrder(w, content, ai, new ResearchNodeId(4)), AiResearchPolicy.Decide(w, cfg, ai));
        Assert.Null(AiResearchPolicy.Decide(w, cfg, new PolityId(Player)));   // the human is never touched

        // Switch off: with every finite node AND each repeatable complete once, nothing is available — the AI idles (the
        // complete-once behaviour), where with levels on it researches the next level.
        var offSpec = new Spec { Stage = "a", RepeatableLevels = false };
        offSpec.Technologies.AddRange(spec.Technologies);
        offSpec.Civics.AddRange(spec.Civics);
        offSpec.Recursive.AddRange(spec.Recursive);
        SimConfig offCfg = cfg with { Research = offSpec.Load() };
        Assert.Null(AiResearchPolicy.Decide(w, offCfg, ai));
    }

    [Fact]
    public void ACityState_PicksTheCheapestLevelToo_ThroughTheSameKey()
    {
        // The city-state's autonomous pick is AiResearchPolicy.Cheapest over the available set: a levelled node at its next
        // level competes by (EffectiveCost, key) like any node.
        WorldState w = WithCompleted(Ready(), Rep);
        w.ResearchLevels.Add(new ResearchLevelRow(P1, Key(Rep), 1));
        bool[] completed = ResearchQuery.CompletedMask(w, On, P1);
        bool[] available = ResearchQuery.AvailableMask(On, completed, ResearchQuery.LevelMask(w, On, P1, completed));
        var onlyRepeatables = new bool[available.Length];
        onlyRepeatables[Index(On, Rep)] = onlyRepeatables[Index(On, TrunkRep)] = true;
        // rep's next level costs 1 250, trunkrep's first 1 000: trunkrep wins on cost though its key is higher.
        Assert.Equal(Index(On, TrunkRep), AiResearchPolicy.Cheapest(w, On, P1, available, onlyRepeatables));
    }
}
