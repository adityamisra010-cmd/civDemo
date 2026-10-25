using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Migration;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-047 (tranche-2 contract §1.1) — THE TEETH OF EVERY OPERAND. For each registry row: a fixture world (the dev map,
/// four settlements, the player empire P holding s0 and s2, the AI empire holding s1 and s3, three production turns) whose
/// holder P has completed the operand's PROBE node (a late technology carrying the one realized effect
/// {operand, 0.5}, KnowledgeEffectsRigs). The OWNING system (or the State query that is the read site) is run on the same
/// world under the ON content and its OFF twin, which differ only in tuning.knowledgeEffects, so the difference is the
/// realized effect alone. Each case asserts:
/// <list type="number">
/// <item>the holder's settlement moves in the operand's stated direction (strictly);</item>
/// <item>without the completion, ON equals OFF exactly (no knowledge, no effect);</item>
/// <item>where the read is per holder and nothing couples the two empires within the step, the RIVAL's settlement reads
///   exactly the same ON and OFF (effects are per holder).</item>
/// </list>
/// The coverage guard proves every registry row has a case.
/// </summary>
public class KnowledgeEffectsTeethTests
{
    private static readonly SimConfig On = KnowledgeEffectsRigs.Cfg(KnowledgeEffectsRigs.ProbeOn);
    private static readonly SimConfig Off = KnowledgeEffectsRigs.Cfg(KnowledgeEffectsRigs.ProbeOff);

    private static readonly Lazy<WorldState> Fixture = new(() =>
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Off, 42);
        w = UniversityRigs.Production(Off).Run(w, 3);
        // No fixture row may carry a probe effect (the probes are late technologies; this makes it certain).
        var kept = new List<ResearchCompletedRow>();
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (!IsProbe(w.ResearchCompleted[i].Node)) kept.Add(w.ResearchCompleted[i]);
        w.ResearchCompleted.Clear();
        foreach (ResearchCompletedRow row in kept) w.ResearchCompleted.Add(row);
        Assert.Equal(4, w.Settlements.Count);
        Assert.NotEqual(Holder(w, S(0)).Value, Holder(w, S(1)).Value);
        Assert.Equal(Holder(w, S(0)).Value, Holder(w, S(2)).Value);
        Assert.Equal(Holder(w, S(1)).Value, Holder(w, S(3)).Value);
        return w;
    });

    private static bool IsProbe(ResearchNodeId key)
    {
        ResearchContent c = KnowledgeEffectsRigs.ProbeOn;
        int i = c.IndexOf(key);
        return i >= 0 && c.Nodes[i].RealizedEffects.Count > 0;
    }

    private static SettlementId S(int i) => new(i);
    private static PolityId Holder(IReadOnlyWorldState w, SettlementId s) => SettlementKnowledge.HolderOf(w, s);
    private static SettlementId Partner(SettlementId s) => S(s.Value < 2 ? s.Value + 2 : s.Value - 2);
    private static GoodId Good(SimConfig cfg, string name) => new(cfg.Goods!.IdOf(name));

    private static WorldState Step(WorldState w, SimConfig cfg, Func<SimConfig, SystemRegistration> system) =>
        UniversityRigs.Only(new OrderLog(), 10.0, system(cfg)).Step(w);

    // ================================================================== the cases

    /// <summary>One operand's teeth: <c>Measure</c> rigs a fresh clone, runs the read site under the config given and
    /// reads the metric at the settlement given; <c>Direction</c> +1 means the improvement raises the metric.</summary>
    private sealed record Case(KnowledgeOperand Op, int Direction, Func<WorldState, SimConfig, SettlementId, double> Measure,
        bool RivalUnaffected = true, Func<SimConfig, SimConfig>? Tune = null);

    private static readonly Lazy<Case[]> Cases = new(BuildCases);

    public static TheoryData<string> OperandIds()
    {
        var data = new TheoryData<string>();
        foreach (KnowledgeOperandInfo info in KnowledgeOperands.All) data.Add(info.Id);
        return data;
    }

    [Fact]
    public void EveryRegistryOperand_HasATeethCase()
    {
        Case[] cases = Cases.Value;
        Assert.Equal(KnowledgeOperands.Count, cases.Length);
        for (int i = 0; i < cases.Length; i++) Assert.Equal((KnowledgeOperand)i, cases[i].Op);
    }

    [Theory]
    [MemberData(nameof(OperandIds))]
    public void TheOperand_MovesItsSystemsOutput_ForTheHolderOnly(string operandId)
    {
        KnowledgeOperand op = KnowledgeOperands.FromId(operandId)!.Value;
        Case c = Cases.Value[(int)op];
        SimConfig on = c.Tune is null ? On : c.Tune(On);
        SimConfig off = c.Tune is null ? Off : c.Tune(Off);

        WorldState granted = Fixture.Value.Clone();
        KnowledgeEffectsRigs.Grant(granted, KnowledgeEffectsRigs.ProbeOn, Holder(granted, S(0)), KnowledgeEffectsRigs.ProbeNode(op));

        double vOn = c.Measure(granted.Clone(), on, S(0));
        double vOff = c.Measure(granted.Clone(), off, S(0));
        Assert.True(double.IsFinite(vOn) && double.IsFinite(vOff), $"{operandId}: non-finite metric (on {vOn}, off {vOff})");
        if (c.Direction > 0) Assert.True(vOn > vOff, $"{operandId}: expected ON > OFF, got on {vOn} off {vOff}");
        else Assert.True(vOn < vOff, $"{operandId}: expected ON < OFF, got on {vOn} off {vOff}");

        // No completion: no effect, exactly.
        WorldState plain = Fixture.Value.Clone();
        Assert.Equal(c.Measure(plain.Clone(), off, S(0)), c.Measure(plain.Clone(), on, S(0)));

        // The rival empire's settlement does not see P's knowledge.
        if (c.RivalUnaffected)
            Assert.Equal(c.Measure(granted.Clone(), off, S(1)), c.Measure(granted.Clone(), on, S(1)));
    }

    private static Case[] BuildCases()
    {
        var list = new List<Case>
        {
            // ---------------------------------------------------------------- ProductionSystem
            new(KnowledgeOperand.FarmingYieldPerArableKm2PerYear, +1, (w, cfg, s) => FarmGrain(w, cfg, s, arableKm2: 0.2)),
            new(KnowledgeOperand.FarmingOutputPerFarmerPerYear, +1, (w, cfg, s) => FarmGrain(w, cfg, s, arableKm2: 1e6)),
            new(KnowledgeOperand.ProductionOutputPerHerderPerYear, +1, (w, cfg, s) => DepositOutput(w, cfg, s, food: true)),
            new(KnowledgeOperand.ProductionOutputPerExtractorPerYear, +1, (w, cfg, s) => DepositOutput(w, cfg, s, food: false)),
            new(KnowledgeOperand.ProductionToolYieldBonusMax, +1, (w, cfg, s) => FarmGrain(w, cfg, s, arableKm2: 1e6, tools: 1_000_000)),
            new(KnowledgeOperand.ProductionToolWearPerEquippedFarmerPerYear, +1, (w, cfg, s) =>
            {
                Each(w, x => UniversityRigs.TopUp(w, cfg, x, "tools", 1_000_000));
                return Stock(Step(w, cfg, SystemCatalog.Production), s, Good(cfg, "tools"));
            }),
            // ---------------------------------------------------------------- ConsumptionSystem
            new(KnowledgeOperand.ConsumptionGrainSpoilagePerYear, +1, (w, cfg, s) =>
            {
                // Below the granary bound (raised so it never binds): the store after spoilage is the metric.
                Each(w, x => UniversityRigs.TopUp(w, cfg, x, "grain", 100_000));
                return Stock(Step(w, cfg, SystemCatalog.Consumption), s, Good(cfg, "grain"));
            }, Tune: c => c with { Consumption = c.Consumption with { GranaryYearsOfDemand = 1000.0 } }),
            new(KnowledgeOperand.ConsumptionGranaryYearsOfDemand, +1, (w, cfg, s) =>
            {
                Each(w, x => UniversityRigs.TopUp(w, cfg, x, "grain", 100_000_000));
                return Stock(Step(w, cfg, SystemCatalog.Consumption), s, Good(cfg, "grain"));
            }),
            // ---------------------------------------------------------------- HarvestWeatherSystem
            new(KnowledgeOperand.HarvestVarianceSigmaLogYield, -1, HarvestLogSpread),
            // ---------------------------------------------------------------- GranaryEffects
            new(KnowledgeOperand.GranariesFamineReductionMax, -1, (w, cfg, s) =>
            {
                Granaries(w, cfg);
                return GranaryEffects.FamineIntensityFactor(w, cfg, s);
            }),
            new(KnowledgeOperand.GranariesFarmYieldBonusMax, +1, (w, cfg, s) =>
            {
                Granaries(w, cfg);
                return FarmGrain(w, cfg, s, arableKm2: 1e6);
            }),
            // ---------------------------------------------------------------- FoodState
            new(KnowledgeOperand.FoodStateAdaptationAbsorbableShortfall, -1, (w, cfg, s) =>
                FoodState.EffectiveDeficit(w, s, 0.3, FoodStateKind.Severe, cfg)),
            // ---------------------------------------------------------------- HousingSystem
            new(KnowledgeOperand.HousingBuildLaborAdultYearsPerDwelling, +1, (w, cfg, s) =>
            {
                Each(w, x =>
                {
                    GovernanceRigs.SetDwellings(w, x, 0);
                    UniversityRigs.TopUp(w, cfg, x, "timber", 1_000_000);
                    UniversityRigs.TopUp(w, cfg, x, "clay", 1_000_000);
                    UniversityRigs.Construction(w, x, 0.001);
                });
                return Dwellings(Step(w, cfg, SystemCatalog.Housing), s);
            }),
            new(KnowledgeOperand.HousingBuildTimberPerDwelling, +1, (w, cfg, s) =>
            {
                Each(w, x =>
                {
                    GovernanceRigs.SetDwellings(w, x, 0);
                    SetStock(w, cfg, x, "timber", 20);
                    UniversityRigs.TopUp(w, cfg, x, "clay", 1_000_000);
                    UniversityRigs.Construction(w, x, 0.5);
                });
                return Dwellings(Step(w, cfg, SystemCatalog.Housing), s);
            }),
            new(KnowledgeOperand.HousingUpkeepTimberPerDwellingYear, +1, (w, cfg, s) =>
            {
                Each(w, x => { GovernanceRigs.SetDwellings(w, x, 200); SetStock(w, cfg, x, "timber", 20); });
                WorldState n = Step(w, cfg, SystemCatalog.Housing);
                for (int i = 0; i < n.Housing.Count; i++) if (n.Housing[i].Settlement == s) return n.Housing[i].LastMaintenanceFraction;
                throw new InvalidOperationException("no housing row");
            }),
            new(KnowledgeOperand.HousingTauYears, +1, (w, cfg, s) =>
            {
                Each(w, x => { GovernanceRigs.SetDwellings(w, x, 200); SetStock(w, cfg, x, "timber", 0); });
                return Dwellings(Step(w, cfg, SystemCatalog.Housing), s);
            }),
            new(KnowledgeOperand.HousingPersonsPerDwelling, +1, (w, cfg, s) =>
            {
                Each(w, x => GovernanceRigs.SetDwellings(w, x, Math.Max(1, GovernanceRigs.Population(w, x) / 12)));
                return SettlementHappiness.HousingSufficiency(w, s, cfg);
            }),
            // ---------------------------------------------------------------- PathBuildSystem (+ the river factor)
            new(KnowledgeOperand.PathBuildLaborPerAdultPerYear, +1, (w, cfg, s) =>
            {
                Each(w, x => UniversityRigs.Construction(w, x, 0.5));
                WorldState n = Step(w, cfg, SystemCatalog.PathBuild);
                for (int i = 0; i < n.PathProgress.Count; i++) if (n.PathProgress[i].Settlement == s) return n.PathProgress[i].Banked;
                return 0.0;
            }, Tune: c => c with { PathBuild = c.PathBuild with { BuildCostMultiplier = 1e15 } }),
            new(KnowledgeOperand.PathBuildBuildCostMultiplier, +1, (w, cfg, s) =>
            {
                Bank(w, 100.0);
                return Step(w, cfg, SystemCatalog.PathBuild).NetworkEdges.Count - w.NetworkEdges.Count;
            }, RivalUnaffected: false, Tune: NoAccrual),
            new(KnowledgeOperand.PathBuildDirtPathSpeedFactor, -1, (w, cfg, s) =>
            {
                Bank(w, 400.0);
                return FirstNewEdgeCost(w, Step(w, cfg, SystemCatalog.PathBuild));
            }, RivalUnaffected: false, Tune: NoAccrual),
            new(KnowledgeOperand.TransportRiverCostFactor, -1, (w, cfg, s) =>
            {
                Bank(w, 1e6);
                SetFrontierOnRiver(w, cfg, S(0));
                return FirstNewEdgeCost(w, Step(w, cfg, SystemCatalog.PathBuild));
            }, RivalUnaffected: false, Tune: NoAccrual),
            // ---------------------------------------------------------------- TransportQuery / RoadDevelopmentQuery
            // CR-025: this case measures the travel-time QUERY, which no system calls (ADR-032 §7) — teeth, not aim. It
            // proves the read site responds, not that play changes; UnconsumedOperandTests pins the latter as measured.
            new(KnowledgeOperand.RoadsBaselineKmPerDay, -1, (w, cfg, s) =>
                TransportQuery.EstimateFreight(w, cfg.Roads!, s, Partner(s), 1000.0, cfg.Research, Holder(w, s)).TransitDays),
            new(KnowledgeOperand.RoadsMaxRouteKm, +1, (w, cfg, s) =>
            {
                RoadsConfig roads = cfg.Roads! with { MaxRouteKm = ShortestRouteKm(w, cfg) * 0.8 };
                return RoadDevelopmentQuery.Routes(w, cfg.Research!, roads, Holder(w, s)).Length;
            }),
            // ---------------------------------------------------------------- CatchmentSystem
            new(KnowledgeOperand.CatchmentHinterlandRadiusKm, +1, (w, cfg, s) =>
            {
                WorldState n = Step(w, cfg, SystemCatalog.Catchment);
                int count = 0;
                for (int i = 0; i < n.CatchmentNodes.Count; i++) if (n.CatchmentNodes[i].Settlement == s) count++;
                return count;
            }, RivalUnaffected: false),
            // ---------------------------------------------------------------- TradeArbitrageSystem / PriceSystem
            new(KnowledgeOperand.TradeGapClosingFraction, +1, TradeVolume, RivalUnaffected: false),
            new(KnowledgeOperand.TradeCostPerBulkCostUnit, +1, TradeVolume, RivalUnaffected: false),
            new(KnowledgeOperand.PriceStockReleaseRatePerYear, -1, (w, cfg, s) =>
            {
                // A demanded, stocked good: the un-railed price change (the sum of the published terms) falls further
                // the faster the stock is released to the market.
                GoodId timber = Good(cfg, "timber");
                Each(w, x =>
                {
                    SetStock(w, cfg, x, "timber", 1_000);
                    int r = GoodStockIndex.IndexOf(w.GoodStocks, x, timber);
                    w.GoodStocks.Ref(r).LastConsumptionDemandUnits = 4_000;
                    w.GoodStocks.Ref(r).LastProducedUnits = 0;
                    w.GoodStocks.Ref(r).LastInputDemandUnits = 0;
                });
                WorldState n = Step(w, cfg, SystemCatalog.Price);
                for (int i = 0; i < n.PriceTerms.Count; i++)
                    if (n.PriceTerms[i].Settlement == s && n.PriceTerms[i].Good == timber)
                        return n.PriceTerms[i].Consumption + n.PriceTerms[i].InputDemand + n.PriceTerms[i].Production + n.PriceTerms[i].StockRelease;
                throw new InvalidOperationException("no price term row");
            }),
            // ---------------------------------------------------------------- DemographicsSystem
            new(KnowledgeOperand.DemographicsMortalityPerYear, -1, (w, cfg, s) => Deaths(Step(w, cfg, SystemCatalog.Demographics), s)),
            new(KnowledgeOperand.DemographicsStarvationMortalityMaxPerYear, -1, (w, cfg, s) =>
            {
                Each(w, x => GovernanceRigs.SetDeficit(w, x, 0.9));
                return Deaths(Step(w, cfg, SystemCatalog.Demographics), s);
            }),
            new(KnowledgeOperand.DemographicsStarvationChildMultiplier, -1, (w, cfg, s) =>
            {
                Each(w, x => GovernanceRigs.SetDeficit(w, x, 0.9));
                return Deaths(Step(w, cfg, SystemCatalog.Demographics), s);
            }),
            // ---------------------------------------------------------------- Institutions (universities)
            new(KnowledgeOperand.InstitutionsUniversitiesMaxMortalityReduction, -1, (w, cfg, s) =>
            {
                Each(w, x => UniversityRigs.Found(w, Holder(w, x), x, InstitutionContent.HealthTypeKey(cfg), 1.0));
                return InstitutionEffects.MortalityMultiplier(w, cfg, s);
            }),
            new(KnowledgeOperand.InstitutionsUniversitiesMaxResearchCostReduction, -1, (w, cfg, s) =>
            {
                int type = cfg.Research!.UniversityTypes[0].Key;
                Each(w, x => UniversityRigs.Found(w, Holder(w, x), x, type, 1.0));
                WorldState n = Step(w, cfg, SystemCatalog.Institutions);
                for (int i = 0; i < n.ResearchCostModifiers.Count; i++)
                    if (n.ResearchCostModifiers[i].Polity.Value == Holder(w, s).Value && n.ResearchCostModifiers[i].UniversityType == type)
                        return n.ResearchCostModifiers[i].Factor;
                throw new InvalidOperationException("no cost modifier row");
            }),
            new(KnowledgeOperand.InstitutionsUniversitiesMaturityTauYears, +1, (w, cfg, s) =>
            {
                int type = cfg.Research!.UniversityTypes[0].Key;
                Each(w, x => { UniversityRigs.Found(w, Holder(w, x), x, type, 0.1); UniversityRigs.Surplus(w, x, 3.0); });
                WorldState n = Step(w, cfg, SystemCatalog.Institutions);
                for (int i = 0; i < n.Institutions.Count; i++) if (n.Institutions[i].Settlement == s) return n.Institutions[i].Maturity;
                throw new InvalidOperationException("no institution row");
            }, Tune: c => c with { Institutions = c.Institutions! with { Universities = c.Institutions.Universities with { AdultsPerUniversity = 100 } } }),
            // ---------------------------------------------------------------- Governance / Unrest
            new(KnowledgeOperand.GovernanceAuthorityDecayCostUnits, +1, (w, cfg, s) =>
                Sim.Core.State.Governance.AdministrativeReach(w, Holder(w, s), Partner(s), cfg.Governance!, cfg.Research)),
            new(KnowledgeOperand.GovernanceTaxExtractionResponseMax, +1, (w, cfg, s) =>
            {
                Taxed(w);
                return Sim.Core.State.Governance.ExtractionMultiplier(w, s, cfg);
            }),
            new(KnowledgeOperand.SocietyUnrestOutputDrag, +1, (w, cfg, s) =>
            {
                Taxed(w);
                double t = cfg.Needs!.Unrest!.UprisingGrievance * 0.8;   // past the protest onset, short of rising
                Each(w, x =>
                {
                    foreach (ClassEntry c in cfg.Registries.Classes)
                        w.TaxGrievances.Add(new TaxGrievanceRow(x, new ClassId(c.Id), t));
                });
                return Unrest.OutputFactor(w, s, cfg);
            }),
            // ---------------------------------------------------------------- MigrationSystem
            new(KnowledgeOperand.MigrationBaseRatePerYear, +1, (w, cfg, s) =>
            {
                // A land gap: s0, s1 nearly landless, s2, s3 rich — the gap channel's (pre-cap) desire out of s.
                for (int i = 0; i < w.CatchmentSummaries.Count; i++)
                    w.CatchmentSummaries[i] = w.CatchmentSummaries[i] with
                    { EffectiveArableKm2 = w.CatchmentSummaries[i].Settlement.Value < 2 ? 0.001 : 10_000.0 };
                MigrationPlan plan = MigrationSystem.Plan(w, cfg, 10.0);
                int src = Row(w, s);
                double sum = 0.0;
                for (int dst = 0; dst < w.Settlements.Count; dst++) if (dst != src) sum += plan.GapPairDesire[src, dst];
                return sum;
            }, RivalUnaffected: false),
            new(KnowledgeOperand.MigrationDampingDecayCostUnits, +1, (w, cfg, s) =>
                MigrationSystem.Plan(w, cfg, 10.0).Damping[Row(w, s), Row(w, Partner(s))]),
            // ---------------------------------------------------------------- ClassMobilitySystem
            new(KnowledgeOperand.MobilityPromoteRatePerYear, +1, Promoted),
            new(KnowledgeOperand.MobilityTargetShareCap, +1, Promoted),
            // ---------------------------------------------------------------- FoundingQuery
            new(KnowledgeOperand.ColonizationOrderPartyFraction, +1, (w, cfg, s) =>
            {
                FoundingQuery.Departure(w.Buckets, w.GoodStocks, cfg, s, null, out long party, FoundingQuery.PartyFraction(w, cfg, s));
                return party;
            }),
            // ---------------------------------------------------------------- DiscoveryQuery (integer operands)
            new(KnowledgeOperand.FogExploreStepNodes, +1, (w, cfg, s) =>
                DiscoveryQuery.NextRadius(w, cfg.Fog!, Holder(w, s), s, cfg.Research)),
            new(KnowledgeOperand.FogExploreMaxRadiusNodes, +1, (w, cfg, s) =>
            {
                Each(w, x => w.Explorations.Add(new ExplorationRow(Holder(w, x), x, cfg.Fog!.ExploreMaxRadiusNodes)));
                return DiscoveryQuery.NextRadius(w, cfg.Fog!, Holder(w, s), s, cfg.Research);
            }),
            new(KnowledgeOperand.FogFormationSightNodes, +1, (w, cfg, s) =>
            {
                // The integer floor: ONE probe (Σ 0.5) leaves 1 × 1.5 → 1; the second probe makes Σ 1.0 → 2.
                KnowledgeEffectsRigs.Grant(w, KnowledgeEffectsRigs.ProbeOn, Holder(w, S(0)), KnowledgeEffectsRigs.ExtraSightProbe);
                return VisibleNodes(w, cfg, Holder(w, s));
            }),
            // ---------------------------------------------------------------- ResearchQuery (CP, RP)
            new(KnowledgeOperand.ResearchCivicsPointsCoefficient, +1, (w, cfg, s) =>
                ResearchQuery.CivicsPointsMilli(w, cfg.Research!, Holder(w, s), 10.0)),
            new(KnowledgeOperand.ResearchResearchPointsCoefficient, +1, (w, cfg, s) =>
                ResearchQuery.ResearchPointPool(w, cfg.Research!, Holder(w, s))),
            // ---------------------------------------------------------------- NeedsGrievanceSystem
            new(KnowledgeOperand.SocietyGrievanceRelief, -1, (w, cfg, s) =>
            {
                Each(w, x => GovernanceRigs.Destitute(w, x));
                WorldState n = Step(w, cfg, SystemCatalog.NeedsGrievance);
                double g = 0.0;
                for (int i = 0; i < n.Grievances.Count; i++) if (n.Grievances[i].Settlement == s) g += n.Grievances[i].Value;
                return g;
            }),
        };
        list.Sort((a, b) => ((int)a.Op).CompareTo((int)b.Op));
        return [.. list];
    }

    // ================================================================== rigs and metrics

    private static void Each(WorldState w, Action<SettlementId> rig)
    {
        for (int i = 0; i < w.Settlements.Count; i++) rig(w.Settlements[i].Id);
    }

    private static int Row(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == s) return i;
        throw new InvalidOperationException("no settlement");
    }

    private static long Stock(IReadOnlyWorldState w, SettlementId s, GoodId good)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, good);
        return i < 0 ? 0 : w.GoodStocks[i].Amount.Value;
    }

    private static void SetStock(WorldState w, SimConfig cfg, SettlementId s, string good, long target)
    {
        var id = Good(cfg, good);
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long delta = target - w.GoodStocks[idx].Amount.Value;
        if (delta == 0) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id), ReasonIds.InitialEndowment,
            Math.Abs(delta), delta > 0 ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    private static double FarmGrain(WorldState w, SimConfig cfg, SettlementId s, double arableKm2, long tools = -1)
    {
        for (int i = 0; i < w.CatchmentSummaries.Count; i++)
            w.CatchmentSummaries[i] = w.CatchmentSummaries[i] with { EffectiveArableKm2 = arableKm2 };
        if (tools >= 0) Each(w, x => UniversityRigs.TopUp(w, cfg, x, "tools", tools));
        WorldState n = Step(w, cfg, SystemCatalog.Production);
        int g = GoodStockIndex.IndexOf(n.GoodStocks, s, Good(cfg, "grain"));
        return n.GoodStocks[g].LastProducedUnits;
    }

    private static double DepositOutput(WorldState w, SimConfig cfg, SettlementId s, bool food)
    {
        WorldState n = Step(w, cfg, SystemCatalog.Production);
        GoodId livestock = Good(cfg, "livestock"), fish = Good(cfg, "fish");
        long sum = 0;
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow d = w.Deposits[i];
            if (d.Settlement != s) continue;
            bool isFood = d.Good == livestock || d.Good == fish;
            if (isFood != food) continue;
            int r = GoodStockIndex.IndexOf(n.GoodStocks, s, d.Good);
            if (r >= 0) sum += n.GoodStocks[r].LastProducedUnits;
        }
        return sum;
    }

    private static void Granaries(WorldState w, SimConfig cfg)
    {
        int project = GranaryEffects.ProjectId(cfg);
        Assert.True(project >= 0);
        Each(w, x => w.Structures.Add(new StructureRow(x, project, 3)));
    }

    private static double HarvestLogSpread(WorldState w, SimConfig cfg, SettlementId s)
    {
        var logs = new List<double>();
        TurnExecutor ex = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.HarvestWeather(cfg));
        for (int t = 0; t < 12; t++)
        {
            w = ex.Step(w);
            for (int i = 0; i < w.HarvestWeather.Count; i++)
                if (w.HarvestWeather[i].Settlement == s) logs.Add(Math.Log(w.HarvestWeather[i].Multiplier));
        }
        double mean = 0.0;
        foreach (double x in logs) mean += x;
        mean /= logs.Count;
        double ss = 0.0;
        foreach (double x in logs) ss += (x - mean) * (x - mean);
        return ss;
    }

    private static double Dwellings(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Housing.Count; i++) if (w.Housing[i].Settlement == s) return w.Housing[i].Dwellings.Value;
        return 0.0;
    }

    private static SimConfig NoAccrual(SimConfig c) => c with { PathBuild = c.PathBuild with { LaborPerAdultPerYear = 0.0 } };

    /// <summary>Every settlement holds a progress row with <paramref name="banked"/> (frontier at its origin).</summary>
    private static void Bank(WorldState w, double banked)
    {
        Each(w, x =>
        {
            for (int i = 0; i < w.PathProgress.Count; i++)
                if (w.PathProgress[i].Settlement == x) { w.PathProgress[i] = w.PathProgress[i] with { Banked = banked }; return; }
            w.PathProgress.Add(new PathProgressRow(x, banked, -1));
        });
    }

    private static double FirstNewEdgeCost(IReadOnlyWorldState before, IReadOnlyWorldState after)
    {
        Assert.True(after.NetworkEdges.Count > before.NetworkEdges.Count, "no segment was built");
        return after.NetworkEdges[before.NetworkEdges.Count].Cost;
    }

    /// <summary>Moves s's path frontier onto the passable RIVER lattice node nearest its origin (a node whose cost
    /// depends on transport.riverCostFactor), so its first segment's cost carries the river term.</summary>
    private static void SetFrontierOnRiver(WorldState w, SimConfig cfg, SettlementId s)
    {
        TraversalLattice a = TraversalLattice.Build(w.Terrain!, cfg.Transport.RiverCostFactor);
        TraversalLattice b = TraversalLattice.Build(w.Terrain!, cfg.Transport.RiverCostFactor * 0.5);
        int origin = LatticeMap.OriginLatticeNode(a, w.Terrain!.Size, w.Settlements[Row(w, s)].SiteCell);
        (int ox, int oy) = a.Coords(origin);
        int best = -1;
        long bestD = long.MaxValue;
        for (int node = 0; node < a.NodeCount; node++)
        {
            if (!a.IsPassable(node) || a.NodeCost[node] == b.NodeCost[node]) continue;
            (int x, int y) = a.Coords(node);
            long d = (long)(x - ox) * (x - ox) + (long)(y - oy) * (y - oy);
            if (d < bestD) { bestD = d; best = node; }
        }
        Assert.True(best >= 0, "the dev map has no passable river node");
        for (int i = 0; i < w.PathProgress.Count; i++)
            if (w.PathProgress[i].Settlement == s) { w.PathProgress[i] = w.PathProgress[i] with { FrontierNode = best }; return; }
        throw new InvalidOperationException("no progress row");
    }

    private static double ShortestRouteKm(IReadOnlyWorldState w, SimConfig cfg)
    {
        double min = double.PositiveInfinity;
        RoadsConfig open = cfg.Roads! with { MaxRouteKm = 1e12 };
        for (int p = 0; p < w.Polities.Count; p++)
            foreach (RouteStatus r in RoadDevelopmentQuery.Routes(w, cfg.Research!, open, w.Polities[p].Id))
                min = Math.Min(min, r.LengthKm);
        Assert.True(double.IsFinite(min), "no candidate route");
        return min;
    }

    private static double TradeVolume(WorldState w, SimConfig cfg, SettlementId s)
    {
        TestConfigs.KnowTrade(w, cfg);
        Each(w, x => Each(w, y => { if (x != y) GovernanceRigs.SetDistance(w, x, y, 2.0); }));
        GoodId pottery = Good(cfg, "timber");
        Each(w, x =>
        {
            double price = x.Value < 2 ? 0.2 : 5.0;   // s0, s1 cheap; s2, s3 dear
            SetPrice(w, x, pottery, price);
            if (x.Value < 2) UniversityRigs.TopUp(w, cfg, x, "timber", 100_000);
        });
        WorldState n = Step(w, cfg, SystemCatalog.TradeArbitrage);
        long q = 0;
        for (int i = 0; i < n.TradeFlows.Count; i++)
            if (n.TradeFlows[i].From == s || n.TradeFlows[i].To == s) q += n.TradeFlows[i].Quantity;
        return q;
    }

    private static void SetPrice(WorldState w, SettlementId s, GoodId good, double price)
    {
        for (int i = 0; i < w.Prices.Count; i++)
            if (w.Prices[i].Settlement == s && w.Prices[i].Good == good) { w.Prices[i] = new PriceRow(s, good, price); return; }
        w.Prices.Add(new PriceRow(s, good, price));
    }

    private static double Price(IReadOnlyWorldState w, SettlementId s, GoodId good)
    {
        for (int i = 0; i < w.Prices.Count; i++) if (w.Prices[i].Settlement == s && w.Prices[i].Good == good) return w.Prices[i].Price;
        throw new InvalidOperationException("no price row");
    }

    private static double Deaths(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.SettlementVitals.Count; i++) if (w.SettlementVitals[i].Settlement == s) return w.SettlementVitals[i].Deaths;
        throw new InvalidOperationException("no vitals row");
    }

    /// <summary>Both empires levy 50 % at full reach.</summary>
    private static void Taxed(WorldState w)
    {
        for (int p = 0; p < w.Polities.Count; p++) w.TaxPolicies.Add(new TaxPolicyRow(w.Polities[p].Id, 0.5));
        for (int i = 0; i < w.Controls.Count; i++) w.Controls[i] = w.Controls[i] with { Strength = 1.0 };
    }

    /// <summary>The managed (artisan) class's members at s after one ClassMobility step from a well-fed, latched rig.</summary>
    private static double Promoted(WorldState w, SimConfig cfg, SettlementId s)
    {
        var managed = new ClassId(cfg.Registries.Classes[1].Id);
        GoodId grain = Good(cfg, "grain");
        Each(w, x =>
        {
            GovernanceRigs.SetDeficit(w, x, 0.0);
            UniversityRigs.Surplus(w, x, 5.0);
            int g = GoodStockIndex.IndexOf(w.GoodStocks, x, grain);
            w.GoodStocks.Ref(g).LastProducedUnits = 1_000_000;
            for (int i = 0; i < w.ClassStates.Count; i++)
                if (w.ClassStates[i].Settlement == x && w.ClassStates[i].Class == managed)
                    w.ClassStates[i] = w.ClassStates[i] with { Active = 1 };
        });
        WorldState n = Step(w, cfg, SystemCatalog.ClassMobility);
        long members = 0;
        for (int i = 0; i < n.Buckets.Count; i++)
            if (n.Buckets[i].Settlement == s && n.Buckets[i].Class == managed) members += n.Buckets[i].Count.Value;
        return members;
    }

    private static double VisibleNodes(WorldState w, SimConfig cfg, PolityId polity)
    {
        bool owns = false;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) owns |= w.MilitaryUnits[i].Owner.Value == polity.Value;
        Assert.True(owns, "the fixture holder owns no formation");
        ulong[] bits = DiscoveryQuery.VisibleBits(w, cfg.Fog!, polity, cfg.Research)!;
        long count = 0;
        foreach (ulong word in bits) count += System.Numerics.BitOperations.PopCount(word);
        return count;
    }
}
