using System.Globalization;
using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Catchment;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-047 (tranche-2 contract §1, §1.1) — REALIZED RESEARCH EFFECTS: the operand registry against the contract table,
/// the loader's schema and refusals, the combining rule (sum, clamp, direction, result bounds, integer floor), the
/// per-step snapshot against the point query (bit for bit, independent of row order), per-holder isolation, the switch
/// (off = base exactly), a populated content's load round-trip, deterministic replay with effects active, and the
/// switch-off byte identity of a long AI run. The per-operand teeth are KnowledgeEffectsTeethTests.
/// </summary>
public class KnowledgeEffectsTests
{
    // ================================================================== the registry is the contract table

    /// <summary>The contract's §1.1 rows, parsed from the document itself: (id, reader, direction, cap, meaning).</summary>
    private static List<(string Id, string Reader, string Dir, double Cap, string Meaning)> ContractRows()
    {
        string path = Path.Combine(RepoPaths.Root(), "docs", "m6-plan", "knowledge-redesign", "tranche2-contract.md");
        var rows = new List<(string, string, string, double, string)>();
        bool inTable = false;
        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith("### 1.1", StringComparison.Ordinal)) { inTable = true; continue; }
            if (!inTable) continue;
            if (line.StartsWith("## ", StringComparison.Ordinal)) break;
            if (!line.StartsWith("| ", StringComparison.Ordinal) || line.StartsWith("| Operand id", StringComparison.Ordinal)
                || line.StartsWith("|---", StringComparison.Ordinal)) continue;
            string[] cells = line.Split('|');
            rows.Add((cells[1].Trim(), cells[2].Trim(), cells[3].Trim(), double.Parse(cells[4].Trim(), CultureInfo.InvariantCulture),
                cells[5].Trim()));
        }
        return rows;
    }

    [Fact]
    public void TheRegistry_IsTheContractTable_RowForRow()
    {
        var rows = ContractRows();
        Assert.Equal(47, rows.Count);
        Assert.Equal(rows.Count, KnowledgeOperands.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            KnowledgeOperandInfo info = KnowledgeOperands.All[i];
            Assert.Equal((KnowledgeOperand)i, info.Operand);
            Assert.Equal(rows[i].Id, info.Id);
            Assert.Equal(rows[i].Reader, info.ReadBy);
            Assert.Equal(rows[i].Dir == "higher" ? OperandDirection.Higher : OperandDirection.Lower, info.Direction);
            Assert.Equal(rows[i].Cap, info.Cap);
            bool integer = rows[i].Meaning.Contains("(integer)", StringComparison.Ordinal);
            Assert.Equal(integer, info.Integer);
            Assert.Equal(rows[i].Meaning.Replace(" (integer)", "", StringComparison.Ordinal), info.Meaning);
            Assert.Equal(info.Operand, KnowledgeOperands.FromId(info.Id));
            if (info.Direction == OperandDirection.Lower) Assert.True(info.Cap < 1.0, info.Id);   // contract §1 step 4
        }
        Assert.Null(KnowledgeOperands.FromId("farming.notAnOperand"));
    }

    // ================================================================== the loader

    private static string WithRealized(params (string Operand, object Delta)[] effects)
    {
        JsonObject doc = KnowledgeEffectsRigs.ShippedDocument();
        var list = new JsonArray();
        foreach ((string operand, object delta) in effects)
            list.Add(new JsonObject { ["operand"] = operand, ["delta"] = JsonValue.Create(delta) });
        doc["technologies"]![0]!["effects"]!["realized"] = list;
        return doc.ToJsonString();
    }

    [Theory]
    [InlineData("farming.notAnOperand", 0.04, "is not a registered operand")]
    [InlineData("farming.outputPerFarmerPerYear", 0.0, "must be finite and in (0, 0.5]")]
    [InlineData("farming.outputPerFarmerPerYear", -0.1, "must be finite and in (0, 0.5]")]
    [InlineData("farming.outputPerFarmerPerYear", 0.5000001, "must be finite and in (0, 0.5]")]
    public void TheLoader_RefusesABadRealizedEffect(string operand, double delta, string fragment)
    {
        var e = Assert.Throws<ResearchContentException>(() => KnowledgeEffectsRigs.Load(WithRealized((operand, delta))));
        Assert.Contains(fragment, e.Message);
        Assert.Contains("effects.realized[0]", e.Message);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void TheLoader_RefusesANonFiniteDelta(string literal)
    {
        var e = Assert.Throws<ResearchContentException>(() =>
            KnowledgeEffectsRigs.Load(WithRealized(("farming.outputPerFarmerPerYear", literal))));
        Assert.Contains("must be finite and in (0, 0.5]", e.Message);
    }

    [Fact]
    public void TheLoader_RefusesAnOperandTwiceInOneNode()
    {
        var e = Assert.Throws<ResearchContentException>(() => KnowledgeEffectsRigs.Load(WithRealized(
            ("farming.outputPerFarmerPerYear", 0.04), ("housing.tauYears", 0.1), ("farming.outputPerFarmerPerYear", 0.02))));
        Assert.Contains("appears twice in one node", e.Message);
        Assert.Contains("effects.realized[2]", e.Message);
    }

    [Fact]
    public void TheLoader_StillRefusesAnImmediateEffect()
    {
        JsonObject doc = KnowledgeEffectsRigs.ShippedDocument();
        doc["technologies"]![0]!["effects"]!["immediate"] = new JsonArray(new JsonObject { ["x"] = 1 });
        var e = Assert.Throws<ResearchContentException>(() => KnowledgeEffectsRigs.Load(doc.ToJsonString()));
        Assert.Contains("effects.immediate is not empty", e.Message);
    }

    [Fact]
    public void TheSwitch_DefaultsOffWhenAbsent_AndTheShippedContentShipsItOnWithNoEffect()
    {
        Assert.True(TestConfigs.Research().Tuning.KnowledgeEffects);
        Assert.False(TestConfigs.Research().HasRealizedEffects);   // tranche 2: the switch, not the content
        foreach (ResearchNode n in TestConfigs.Research().Nodes) Assert.Empty(n.RealizedEffects);
        ResearchContent twin = TestConfigs.PreKnowledgeEffects(TestConfigs.Sim()).Research!;
        Assert.False(twin.Tuning.KnowledgeEffects);
        SimConfig once = TestConfigs.PreKnowledgeEffects(TestConfigs.Sim());
        Assert.Same(once, TestConfigs.PreKnowledgeEffects(once));   // idempotent: the twin has no switch to remove
    }

    [Fact]
    public void APopulatedContent_LoadsExposesItsEffects_AndRoundTrips()
    {
        string json = KnowledgeEffectsRigs.EarlyJson(on: true);
        ResearchContent a = KnowledgeEffectsRigs.Load(json);
        Assert.True(a.HasRealizedEffects);
        JsonObject doc = JsonNode.Parse(json)!.AsObject();

        // Every node's parsed effects are its JSON list, in order, bit for bit.
        int declared = 0;
        foreach (string tree in new[] { "technologies", "civics" })
            foreach (JsonNode? node in doc[tree]!.AsArray())
            {
                ResearchNode parsed = a.Nodes[a.IndexOfId(node!["id"]!.GetValue<string>())];
                JsonArray? list = node["effects"]!["realized"] as JsonArray;
                Assert.Equal(list?.Count ?? 0, parsed.RealizedEffects.Count);
                for (int k = 0; k < parsed.RealizedEffects.Count; k++)
                {
                    Assert.Equal(list![k]!["operand"]!.GetValue<string>(), KnowledgeOperands.Of(parsed.RealizedEffects[k].Operand).Id);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(list[k]!["delta"]!.GetValue<double>()),
                        BitConverter.DoubleToInt64Bits(parsed.RealizedEffects[k].Delta));
                    declared++;
                }
            }
        Assert.Equal(2 * KnowledgeOperands.Count, declared);

        // The operand index: every effect once, ascending node index per operand.
        int indexed = 0;
        for (int op = 0; op < KnowledgeOperands.Count; op++)
        {
            IReadOnlyList<int> nodes = a.NodesWith((KnowledgeOperand)op);
            for (int k = 1; k < nodes.Count; k++) Assert.True(nodes[k - 1] < nodes[k]);
            foreach (int n in nodes)
            {
                int hits = 0;
                foreach (RealizedEffect e in a.Nodes[n].RealizedEffects) if ((int)e.Operand == op) hits++;
                Assert.Equal(1, hits);
            }
            indexed += nodes.Count;
        }
        Assert.Equal(declared, indexed);

        // Round trip: write the PARSED effects back over the document and reload — the same model.
        foreach (string tree in new[] { "technologies", "civics" })
            foreach (JsonNode? node in doc[tree]!.AsArray())
            {
                ResearchNode parsed = a.Nodes[a.IndexOfId(node!["id"]!.GetValue<string>())];
                var effects = new (string, double)[parsed.RealizedEffects.Count];
                for (int k = 0; k < effects.Length; k++)
                    effects[k] = (KnowledgeOperands.Of(parsed.RealizedEffects[k].Operand).Id, parsed.RealizedEffects[k].Delta);
                KnowledgeEffectsRigs.SetRealized(node.AsObject(), effects);
            }
        ResearchContent b = KnowledgeEffectsRigs.Load(doc.ToJsonString());
        for (int i = 0; i < a.Nodes.Count; i++)
            Assert.Equal(a.Nodes[i].RealizedEffects, b.Nodes[i].RealizedEffects);
        for (int op = 0; op < KnowledgeOperands.Count; op++)
            Assert.Equal(a.NodesWith((KnowledgeOperand)op), b.NodesWith((KnowledgeOperand)op));
    }

    // ================================================================== the combining rule

    [Fact]
    public void Value_ScalesByDirection_ReturnsTheBaseExactlyAtZero()
    {
        Assert.Equal(26.0 * (1.0 + 0.3), KnowledgeEffects.Value(KnowledgeOperand.FarmingYieldPerArableKm2PerYear, 26.0, 0.3));
        Assert.Equal(0.08 * (1.0 - 0.3), KnowledgeEffects.Value(KnowledgeOperand.ConsumptionGrainSpoilagePerYear, 0.08, 0.3));
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
        {
            double b = 0.1 + 0.37 * (int)info.Operand;
            Assert.Equal(BitConverter.DoubleToInt64Bits(b), BitConverter.DoubleToInt64Bits(KnowledgeEffects.Value(info.Operand, b, 0.0)));
        }
    }

    [Fact]
    public void Clamp_HoldsSigmaInsideZeroAndTheCap()
    {
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
        {
            Assert.Equal(info.Cap, KnowledgeEffects.Clamp(info.Operand, info.Cap + 10.0));
            Assert.Equal(0.0, KnowledgeEffects.Clamp(info.Operand, -1.0));
            Assert.Equal(0.0, KnowledgeEffects.Clamp(info.Operand, double.NaN));
            Assert.Equal(info.Cap * 0.5, KnowledgeEffects.Clamp(info.Operand, info.Cap * 0.5));
        }
    }

    [Fact]
    public void TheResultBounds_AreTheContractsAndNeverPushBelowTheBase()
    {
        Assert.Equal(0.35, KnowledgeEffects.Value(KnowledgeOperand.ColonizationOrderPartyFraction, 0.3, 0.5));
        Assert.Equal(0.95, KnowledgeEffects.Value(KnowledgeOperand.TradeGapClosingFraction, 0.5, 1.5));
        Assert.Equal(0.75, KnowledgeEffects.Value(KnowledgeOperand.InstitutionsUniversitiesMaxResearchCostReduction, 0.6, 0.5));
        Assert.Equal(1.0, KnowledgeEffects.Value(KnowledgeOperand.MobilityTargetShareCap, 0.8, 1.0));
        Assert.Equal(0.95, KnowledgeEffects.Value(KnowledgeOperand.FoodStateAdaptationAbsorbableShortfall, 0.6, 1.0));
        // A base already past the bound is kept, never lowered by an IMPROVEMENT.
        Assert.Equal(0.4, KnowledgeEffects.Value(KnowledgeOperand.ColonizationOrderPartyFraction, 0.4, 0.5));
    }

    [Fact]
    public void IntegerOperands_RoundDownAfterScaling()
    {
        Assert.Equal(4.0, KnowledgeEffects.Value(KnowledgeOperand.FogExploreStepNodes, 3, 0.5));        // 4.5 → 4
        Assert.Equal(1.0, KnowledgeEffects.Value(KnowledgeOperand.FogFormationSightNodes, 1, 0.5));     // 1.5 → 1
        Assert.Equal(2.0, KnowledgeEffects.Value(KnowledgeOperand.FogFormationSightNodes, 1, 1.0));
        Assert.Equal(22.0, KnowledgeEffects.Value(KnowledgeOperand.FogExploreMaxRadiusNodes, 15, 0.5)); // 22.5 → 22
        // The shipped fog section through the query: a step of 3 improved by 0.5 explores 4 nodes further, not 4.5.
        FogConfig fog = TestConfigs.Sim().Fog!;
        Assert.Equal(3, fog.ExploreStepNodes);
    }

    // ================================================================== the query, the snapshot, the holders

    /// <summary>The dev world with EARLY effect nodes completed for the two empires, a city-state key and a stale key, in
    /// a deliberately scrambled row order.</summary>
    private static (WorldState World, ResearchContent Content, PolityId[] Holders) Populated()
    {
        ResearchContent content = KnowledgeEffectsRigs.Load(KnowledgeEffectsRigs.EarlyJson(on: true));
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, TestConfigs.Sim(), 42);
        PolityId p = SettlementKnowledge.HolderOf(w, new SettlementId(0));
        PolityId q = SettlementKnowledge.HolderOf(w, new SettlementId(1));
        PolityId local = SettlementKnowledge.LocalHolder(new SettlementId(3));
        PolityId[] holders = [p, q, local, new PolityId(99)];
        int g = 0;
        for (int i = 0; i < 30; i++)
        {
            ResearchNode tech = content.Nodes[i];
            ResearchNode civic = content.Nodes[content.TechnologyCount + i];
            PolityId h = holders[g++ % 3];
            if (!ResearchQuery.IsCompleted(w, h, tech.Key)) w.ResearchCompleted.Add(new ResearchCompletedRow(h, tech.Key));
            if (i % 2 == 0 && !ResearchQuery.IsCompleted(w, p, civic.Key)) w.ResearchCompleted.Add(new ResearchCompletedRow(p, civic.Key));
            if (i % 3 == 0 && !ResearchQuery.IsCompleted(w, local, civic.Key)) w.ResearchCompleted.Add(new ResearchCompletedRow(local, civic.Key));
        }
        // A row twice (a hand-built duplicate) counts once.
        w.ResearchCompleted.Add(w.ResearchCompleted[0]);
        return (w, content, holders);
    }

    [Fact]
    public void TheSnapshot_EqualsThePointQuery_BitForBit_ForEveryHolderAndOperand()
    {
        (WorldState w, ResearchContent content, PolityId[] holders) = Populated();
        KnowledgeEffectsSnapshot fx = KnowledgeEffects.Snapshot(w, content);
        Assert.True(fx.IsActive);
        int moved = 0;
        foreach (PolityId h in holders)
            foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
            {
                double b = info.Integer ? 7 : 1.2345;
                double point = KnowledgeEffects.Apply(w, content, h, info.Operand, b);
                Assert.Equal(BitConverter.DoubleToInt64Bits(point), BitConverter.DoubleToInt64Bits(fx.Apply(h, info.Operand, b)));
                Assert.Equal(KnowledgeEffects.Improvement(w, content, h, info.Operand), fx.Improvement(h, info.Operand));
                if (point != b) moved++;
            }
        Assert.True(moved > 20, $"the populated world moved only {moved} (holder, operand) values");
        // The unknown key reads the base for every operand.
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
            Assert.Equal(1.5, fx.Apply(new PolityId(99), info.Operand, 1.5));
    }

    [Fact]
    public void Sigma_IsAFunctionOfTheCompletedSet_NotOfRowOrder()
    {
        (WorldState w, ResearchContent content, PolityId[] holders) = Populated();
        WorldState reversed = w.Clone();
        reversed.ResearchCompleted.Clear();
        for (int i = w.ResearchCompleted.Count - 1; i >= 0; i--) reversed.ResearchCompleted.Add(w.ResearchCompleted[i]);
        KnowledgeEffectsSnapshot a = KnowledgeEffects.Snapshot(w, content), b = KnowledgeEffects.Snapshot(reversed, content);
        foreach (PolityId h in holders)
            foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(a.Improvement(h, info.Operand)), BitConverter.DoubleToInt64Bits(b.Improvement(h, info.Operand)));
                Assert.Equal(BitConverter.DoubleToInt64Bits(KnowledgeEffects.Improvement(w, content, h, info.Operand)),
                    BitConverter.DoubleToInt64Bits(KnowledgeEffects.Improvement(reversed, content, h, info.Operand)));
            }
    }

    [Fact]
    public void EffectsArePerHolder_AndASettlementReadsItsHolder()
    {
        (WorldState w, ResearchContent content, PolityId[] holders) = Populated();
        // Settlement-scoped reads resolve the holder: s0/s2 the player's, s1 the rival's, s3 — once uncontrolled — its own key.
        Assert.Equal(KnowledgeEffects.Apply(w, content, holders[0], KnowledgeOperand.HousingTauYears, 40.0),
            KnowledgeEffects.ApplyAt(w, content, new SettlementId(0), KnowledgeOperand.HousingTauYears, 40.0));
        Assert.Equal(KnowledgeEffects.Apply(w, content, holders[1], KnowledgeOperand.HousingTauYears, 40.0),
            KnowledgeEffects.ApplyAt(w, content, new SettlementId(1), KnowledgeOperand.HousingTauYears, 40.0));
        var controls = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place.Value != 3) controls.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow row in controls) w.Controls.Add(row);
        Assert.Equal(holders[2], SettlementKnowledge.HolderOf(w, new SettlementId(3)));
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
            Assert.Equal(KnowledgeEffects.Apply(w, content, holders[2], info.Operand, 2.0),
                KnowledgeEffects.ApplyAt(w, content, new SettlementId(3), info.Operand, 2.0));
        // Each holder's Σ counts only ITS rows: completing a node for the rival never moves the player.
        WorldState more = w.Clone();
        foreach (ResearchNode n in content.Nodes)
            if (n.RealizedEffects.Count > 0 && !ResearchQuery.IsCompleted(more, holders[1], n.Key))
                more.ResearchCompleted.Add(new ResearchCompletedRow(holders[1], n.Key));
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
        {
            Assert.Equal(KnowledgeEffects.Improvement(w, content, holders[0], info.Operand),
                KnowledgeEffects.Improvement(more, content, holders[0], info.Operand));
            Assert.Equal(KnowledgeEffects.Improvement(w, content, holders[2], info.Operand),
                KnowledgeEffects.Improvement(more, content, holders[2], info.Operand));
        }
    }

    [Fact]
    public void ACappedSum_ClampsAtTheCap()
    {
        JsonObject doc = KnowledgeEffectsRigs.ShippedDocument();
        for (int k = 0; k < 3; k++)
            KnowledgeEffectsRigs.SetRealized(doc["technologies"]![k]!.AsObject(), ("society.unrestOutputDrag", 0.5), ("roads.maxRouteKm", 0.5));
        doc["tuning"]!["knowledgeEffects"] = true;
        ResearchContent content = KnowledgeEffectsRigs.Load(doc.ToJsonString());
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        var p = new PolityId(7);
        for (int k = 0; k < 3; k++) w.ResearchCompleted.Add(new ResearchCompletedRow(p, content.Nodes[k].Key));
        Assert.Equal(0.6, KnowledgeEffects.Improvement(w, content, p, KnowledgeOperand.SocietyUnrestOutputDrag));   // 1.5 → cap 0.6
        Assert.Equal(1.0 * (1.0 - 0.6), KnowledgeEffects.Apply(w, content, p, KnowledgeOperand.SocietyUnrestOutputDrag, 1.0));
        Assert.Equal(1.5, KnowledgeEffects.Improvement(w, content, p, KnowledgeOperand.RoadsMaxRouteKm));           // below its cap 3.0
        Assert.Equal(400.0 * (1.0 + 1.5), KnowledgeEffects.Apply(w, content, p, KnowledgeOperand.RoadsMaxRouteKm, 400.0));
    }

    // ================================================================== the switch

    [Fact]
    public void SwitchOff_OrNoContent_ReturnsEveryBaseExactly_EvenWithEveryEffectCompleted()
    {
        ResearchContent off = KnowledgeEffectsRigs.Load(KnowledgeEffectsRigs.EarlyJson(on: false));
        ResearchContent on = KnowledgeEffectsRigs.Load(KnowledgeEffectsRigs.EarlyJson(on: true));
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        var p = new PolityId(1);
        foreach (ResearchNode n in off.Nodes) w.ResearchCompleted.Add(new ResearchCompletedRow(p, n.Key));
        Assert.Same(KnowledgeEffectsSnapshot.Inert, KnowledgeEffects.Snapshot(w, off));
        Assert.Same(KnowledgeEffectsSnapshot.Inert, KnowledgeEffects.Snapshot(w, null));
        int differs = 0;
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All)
        {
            // Inside every result bound (an improvement can move it), integer-valued for the integer operands.
            double b = info.Integer ? 3.0 + (int)info.Operand : 0.1 + 0.001 * (int)info.Operand;
            long bits = BitConverter.DoubleToInt64Bits(b);
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(KnowledgeEffects.Apply(w, off, p, info.Operand, b)));
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(KnowledgeEffects.Apply(w, null, p, info.Operand, b)));
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(KnowledgeEffects.ApplyAt(w, off, new SettlementId(0), info.Operand, b)));
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(KnowledgeEffectsSnapshot.Inert.Apply(p, info.Operand, b)));
            Assert.Equal(0.0, KnowledgeEffects.Improvement(w, off, p, info.Operand));
            if (KnowledgeEffects.Apply(w, on, p, info.Operand, b) != b) differs++;
        }
        Assert.Equal(KnowledgeOperands.Count, differs);   // the same rows DO move every operand with the switch on
    }

    [Fact]
    public void TheCatchmentRevision_CountsHinterlandCompletions_OnlyWhenSuchAnEffectExists()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), TestConfigs.Sim(), 42);
        SimConfig shipped = TestConfigs.Sim();
        Assert.Equal(RoadPerformance.NetworkRevision(w), CatchmentSystem.Revision(w, shipped));
        SimConfig probe = KnowledgeEffectsRigs.Cfg(KnowledgeEffectsRigs.ProbeOn);
        SimConfig probeOff = KnowledgeEffectsRigs.Cfg(KnowledgeEffectsRigs.ProbeOff);
        KnowledgeEffectsRigs.Grant(w, probe.Research!, new PolityId(1), KnowledgeEffectsRigs.ProbeNode(KnowledgeOperand.CatchmentHinterlandRadiusKm));
        KnowledgeEffectsRigs.Grant(w, probe.Research!, new PolityId(1), KnowledgeEffectsRigs.ProbeNode(KnowledgeOperand.HousingTauYears));
        Assert.Equal(RoadPerformance.NetworkRevision(w) + 1, CatchmentSystem.Revision(w, probe));
        Assert.Equal(RoadPerformance.NetworkRevision(w), CatchmentSystem.Revision(w, probeOff));
    }

    // ================================================================== runs: identity and replay

    private static WorldState RunAi(SimConfig cfg, int turns)
    {
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, TestConfigs.Worldgen())), log);
        WorldState w = WorldFounding.Found(TestConfigs.Worldgen() with { AiEmpires = 1 }, cfg, 42);
        for (int t = 1; t <= turns; t++)
        {
            AiOrders.Append(log, w, cfg, TestConfigs.Worldgen());
            w = exec.Step(w);
        }
        return w;
    }

    /// <summary>THE SWITCH-OFF BYTE IDENTITY on the long canonical AI run (300 turns, seed 42, one AI empire): the shipped
    /// world (switch on, no effect declared), its twin TestConfigs.PreKnowledgeEffects (switch absent), and a content that
    /// DECLARES effects on early nodes the AI completes but has the switch OFF are the same world, hash for hash.</summary>
    [Fact]
    public void SwitchOff_IsByteIdentical_OnALongAiRun()
    {
        SimConfig shipped = TestConfigs.Sim();
        string live = WorldHash.ComputeHex(RunAi(shipped, 300));
        Assert.Equal(live, WorldHash.ComputeHex(RunAi(TestConfigs.PreKnowledgeEffects(shipped), 300)));
        SimConfig declaredOff = shipped with { Research = KnowledgeEffectsRigs.Load(KnowledgeEffectsRigs.EarlyJson(on: false)) };
        Assert.Equal(live, WorldHash.ComputeHex(RunAi(declaredOff, 300)));
    }

    /// <summary>With effects ACTIVE (the switch on and early nodes the AI completes), the run is deterministic — two runs
    /// are one world — and the effects bite: the world differs from the switch-off twin.</summary>
    [Fact]
    public void WithEffectsActive_TheRunIsDeterministic_AndTheEffectsBite()
    {
        SimConfig on = TestConfigs.Sim() with { Research = KnowledgeEffectsRigs.Load(KnowledgeEffectsRigs.EarlyJson(on: true)) };
        SimConfig off = TestConfigs.Sim() with { Research = KnowledgeEffectsRigs.Load(KnowledgeEffectsRigs.EarlyJson(on: false)) };
        WorldState a = RunAi(on, 120);
        WorldState b = RunAi(on, 120);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.True(WorldStates.StateEquals(a, b));
        // The AI did complete effect-bearing nodes, so the switch is not vacuous here.
        int effectRows = 0;
        for (int i = 0; i < a.ResearchCompleted.Count; i++)
        {
            int n = on.Research!.IndexOf(a.ResearchCompleted[i].Node);
            if (n >= 0 && on.Research.Nodes[n].RealizedEffects.Count > 0) effectRows++;
        }
        Assert.True(effectRows > 0, "the AI completed no effect-bearing node in 120 turns");
        Assert.NotEqual(WorldHash.ComputeHex(a), WorldHash.ComputeHex(RunAi(off, 120)));
    }
}
