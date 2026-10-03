using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Tests.TestUtil;

/// <summary>
/// Rigs for ADR-033 D6 (universities). A rig FORCES a precondition through the very rows the simulation
/// publishes — completed research, a published food surplus, topped-up stocks via the Ledger, a sector
/// allocation — and never bypasses a gate: the systems and queries under test still decide everything.
/// The dev world's settlements hold ~150–350 adults, so rigs that need a viable host lower the market
/// (adultsPerUniversity, TUNE data) through <see cref="Cfg"/> rather than inventing people.
/// </summary>
internal static class UniversityRigs
{
    /// <summary>The knowledge that makes BOTH university entities eligible: building.university
    /// (medicine_hippocratic AND (geometry_axiomatic OR algebra)) and inst.university (library AND
    /// legal_code_roman; library = archive AND scribal_school; archive = a script AND stamp_seal).</summary>
    public static readonly string[] Knowledge = ["cuneiform", "stamp_seal", "legal_code_roman", "medicine_hippocratic", "geometry_axiomatic"];

    /// <summary>The canonical config with the university market lowered to <paramref name="adultsPerUniversity"/>
    /// (and, optionally, other tuning changed by <paramref name="tune"/>).</summary>
    public static SimConfig Cfg(long adultsPerUniversity = 100, Func<UniversitiesConfig, UniversitiesConfig>? tune = null)
    {
        SimConfig cfg = TestConfigs.Sim();
        UniversitiesConfig u = cfg.Institutions!.Universities with { AdultsPerUniversity = adultsPerUniversity };
        if (tune is not null) u = tune(u);
        return cfg with { Institutions = cfg.Institutions with { Universities = u } };
    }

    public static void Grant(WorldState w, ResearchContent research, PolityId polity, params string[] nodeIds)
    {
        foreach (string id in nodeIds.Length == 0 ? Knowledge : nodeIds)
        {
            var key = research.Nodes[research.IndexOfId(id)].Key;
            if (!ResearchQuery.IsCompleted(w, polity, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(polity, key));
        }
    }

    /// <summary>Publishes a food_surplus_ratio for the settlement (upsert; ClassMobilitySystem's row shape).</summary>
    public static void Surplus(WorldState w, SettlementId s, double ratio)
    {
        for (int i = 0; i < w.Variables.Count; i++)
        {
            if (w.Variables[i].Settlement != s || w.Variables[i].VarId != Variables.FoodSurplusRatio) continue;
            w.Variables[i] = w.Variables[i] with { Value = ratio };
            return;
        }
        w.Variables.Add(new VariableRow(s, Variables.FoodSurplusRatio, ratio));
    }

    /// <summary>Raises a stock to <paramref name="target"/> through the Ledger (reason InitialEndowment).</summary>
    public static void TopUp(WorldState w, SimConfig cfg, SettlementId s, string good, long target)
    {
        var id = new GoodId(cfg.Goods!.IdOf(good));
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have >= target) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id),
            ReasonIds.InitialEndowment, target - have, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    /// <summary>The materials of the university project, in full.</summary>
    public static void Materials(WorldState w, SimConfig cfg, SettlementId s)
    {
        ConstructionProjectEntry project = InstitutionContent.ProjectOfType(cfg, 1)!;
        foreach (ProjectInput input in project.Inputs) TopUp(w, cfg, s, input.Good, input.Qty);
    }

    /// <summary>A sector allocation giving construction the share <paramref name="construction"/> (the rest
    /// split as the default mix's other four sectors in their default proportions).</summary>
    public static void Construction(WorldState w, SettlementId s, double construction)
    {
        SectorAllocationRow d = Sectors.Default(s);
        double other = d.Farming + d.Herding + d.Extraction + d.Crafting;
        double scale = (1.0 - construction) / other;
        var row = new SectorAllocationRow(s, d.Farming * scale, d.Herding * scale, d.Extraction * scale, d.Crafting * scale, construction);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) { w.SectorAllocations[i] = row; return; }
        w.SectorAllocations.Add(row);
    }

    /// <summary>A founded institution row of type <paramref name="typeKey"/> (a rig shortcut for EFFECT tests:
    /// the founding path itself is pinned through ConstructionSystem → InstitutionsSystem elsewhere).</summary>
    public static void Found(WorldState w, PolityId owner, SettlementId s, int typeKey, double maturity)
    {
        int next = 1;
        for (int i = 0; i < w.Institutions.Count; i++) next = Math.Max(next, w.Institutions[i].Id + 1);
        w.Institutions.Add(new InstitutionRow(next, owner, s, typeKey, w.Clock.Turn, maturity));
    }

    public static int TypeKey(ResearchContent research, string typeId)
    {
        foreach (UniversityType u in research.UniversityTypes) if (u.Id == typeId) return u.Key;
        throw new ArgumentException(typeId);
    }

    public static TurnExecutor Production(SimConfig cfg, OrderLog? orders = null)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())), orders);
    }

    public static TurnExecutor Only(OrderLog orders, double dtYears, params SystemRegistration[] systems) =>
        new(ResearchRigs.FlatEra(dtYears), systems, orders);
}
