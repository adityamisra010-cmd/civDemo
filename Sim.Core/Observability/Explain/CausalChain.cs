using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Consumption;
using Sim.Core.Systems.NeedsGrievance;

namespace Sim.Core.Observability.Explain;

/// <summary>The §0 field kinds, plus GAP. A link is exactly one of these and
/// says which; there is no sixth kind, because a sixth kind would be an
/// observer-side formula.</summary>
public enum LinkKind
{
    /// <summary>Copied from a row a system wrote (a config constant is also READ,
    /// with <see cref="SourceWorld.Config"/>).</summary>
    Read,
    /// <summary>An integer sum over READ rows.</summary>
    Summed,
    /// <summary>next − prev of a READ or SUMMED quantity.</summary>
    Differenced,
    /// <summary>A call to a PUBLIC static simulation function on stored state.</summary>
    Recomputed,
    /// <summary>Not recorded by the simulation. The Note says what is missing and
    /// where it is computed and discarded (docs/observability-architecture.md §8).</summary>
    Gap,
}

/// <summary>Which world a link's row was read from. Next is the post-step
/// world (what the systems just wrote); Prev is the world they READ (§3.2 —
/// every system reads Prev), so a link below a satisfaction sits on Prev.</summary>
public enum SourceWorld { Prev, Next, Config, None }

/// <summary>
/// Every node a chain can contain, one per distinct modelled dependency. The
/// lever table (<see cref="Levers"/>) is keyed on this, so adding a node here
/// without a lever entry fails the lever-completeness test rather than
/// silently rendering an unexplained cause.
/// </summary>
public enum ChainNode
{
    // --- Sustenance (NeedsGrievanceSystem.Satisfaction, Fill; ProductionSystem.Farm/FromDeposits; ConsumptionSystem)
    SustenanceSatisfaction,
    FoodGoodEaten,
    FoodGoodDemanded,
    FoodGoodFill,
    DeficitRatio,
    NutritionalDemand,
    GrainStore,
    GrainHarvest,
    GrainEaten,
    FarmingShare,
    ArableLand,
    HarvestWeather,
    ToolsStock,
    ToolFactor,
    LandVsLabourBinding,
    DepositFoodProduced,
    HerdingShare,
    DepositAbundance,
    GrainImports,

    // --- Shelter (NeedsGrievanceSystem.HousingSatisfaction; HousingSystem)
    ShelterSatisfaction,
    HousingSufficiency,
    Dwellings,
    DwellingsDelta,
    Population,
    PersonsPerDwelling,
    MaintenanceFraction,
    ConstructionLabourUsed,
    ConstructionShare,
    TimberStock,
    ClayStock,
    ExtractionShare,
    BuiltDecayedSplit,
    BuildBindingConstraint,

    // --- Comfort, and any other basket-bound need (ProductionSystem.Craft)
    ComfortSatisfaction,
    ComfortGoodEaten,
    ComfortGoodDemanded,
    ComfortGoodFill,
    CraftOutputProduced,
    CraftingShare,
    CraftInputStock,
    CraftInputDemand,
    RecipeLabourCap,

    // --- an unbound registry need
    NotSimulated,
}

/// <summary>
/// One link of a causal chain: a named node, its value, how the value was
/// obtained (<see cref="Kind"/>), and the row it came from — table name and
/// row index in the named world — so a reader (or a test) can go and look.
/// <see cref="SourceIndex"/> is −1 when there is no row: for a GAP always, for
/// a RECOMPUTED value whose function took a default (the Note says which), and
/// for a config constant.
/// </summary>
public readonly record struct Link(
    ChainNode Node, string Label, double Value, LinkKind Kind,
    SourceWorld World, string SourceTable, int SourceIndex, string Note = "");

/// <summary>
/// T4.19 — THE CAUSE BEHIND A NEED, ONE LINK AT A TIME (docs/observability-
/// architecture.md §5). From a stored satisfaction down to the deepest STORED
/// input, then a GAP where the next link is transient. Every link here was
/// verified against the owning system's source before it was encoded, and each
/// carries the file:line it was read from in its Label or Note; a dependency
/// that is not in source does not appear.
///
/// NO SECOND IMPLEMENTATION. A chain never computes a harvest, a fill, a
/// dwelling count or a share of its own: it READS the fields the systems
/// published, calls the PUBLIC simulation functions that already exist
/// (<see cref="SettlementHappiness.HousingSufficiency"/>,
/// <see cref="Sectors.Share"/>, <see cref="NeedsGrievanceSystem.Fill(in GoodStockRow)"/>
/// — the fill ratio is the system's own three-case function, called on the
/// row it cites, not a quotient this file takes), and stops. Where a system computes a quantity
/// and throws it away — the tool factor, the land-versus-labour Leontief
/// branch, the per-recipe labour cap, the built/decayed split — the chain says
/// "not recorded" rather than recomputing it, because a recomputation is a
/// copy that will drift the day the system changes.
///
/// THE ONE-TURN LAG, stated because a reader will otherwise misattribute. A
/// satisfaction in Next was computed from fill ratios on Prev
/// (NeedsGrievanceSystem.Fill reads Prev). Those fills were written by the
/// consumption step that PRODUCED Prev, from the harvest recorded on Prev.
/// That harvest was produced under the inputs of the turn BEFORE Prev. The
/// inputs the chain reads off Prev (shares, arable land, weather, tool stock)
/// are the ones IN FORCE for the step Prev→Next — they drive Next's harvest,
/// not the harvest the chain shows. Each such link says so in its Note.
///
/// A chain is a pure function of (prev, next, cfg, ids). It allocates a list
/// and returns an array; it writes nothing.
/// </summary>
public sealed class CausalChain
{
    public int NeedId { get; }
    public string NeedName { get; }
    public Link[] Links { get; }

    private CausalChain(int needId, string needName, Link[] links)
    {
        NeedId = needId;
        NeedName = needName;
        Links = links;
    }

    /// <summary>The chain for one registry need, dispatched on the need's DATA
    /// binding exactly as NeedsGrievanceSystem.Step dispatches it
    /// (NeedsGrievanceSystem.cs:243-260): unbound → not simulated;
    /// source=housingStock → the Shelter chain; the Sustenance id → the
    /// Sustenance chain (staple substitution, farming); any other basket-bound
    /// need → the crafted-goods chain.</summary>
    public static CausalChain ForNeed(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId settlement, ClassId cls, int needId)
    {
        NeedEntry? need = ExplainRows.Need(cfg, needId)
            ?? throw new ArgumentException($"need id {needId} is not in the registry.", nameof(needId));
        if (!need.Bound)
        {
            return new CausalChain(needId, need.Name,
            [
                new Link(ChainNode.NotSimulated, need.Name + ": not yet simulated", double.NaN, LinkKind.Gap,
                    SourceWorld.None, "", -1,
                    "Registered but UNBOUND (needs.json bound=false): skipped before any weight is read "
                    + "(NeedsGrievanceSystem.cs:243), contributes exactly nothing, has no satisfaction row."),
            ]);
        }
        if (need.FromHousingStock) return Shelter(prev, next, cfg, settlement, cls, need);
        if (needId == BasketBook.SustenanceNeedId) return Sustenance(prev, next, cfg, settlement, cls, need);
        return Basket(prev, next, cfg, settlement, cls, need);
    }

    // =====================================================================
    // SUSTENANCE
    // =====================================================================

    private static CausalChain Sustenance(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId s, ClassId cls, NeedEntry need)
    {
        var links = new List<Link>();
        SatisfactionLink(links, next, s, cls, need, ChainNode.SustenanceSatisfaction,
            "s = clamp(obtained/wanted) × varietyFactor over the class's food basket, staple substitution "
            + "included (NeedsGrievanceSystem.cs:303-351); every input is a Prev fill ratio.");

        // Per food good in THIS class's basket — the lines the system iterates
        // (NeedsGrievanceSystem.cs:310-334), in the book's (class, need, good) order.
        BasketBook book = Book(cfg);
        ReadOnlySpan<BasketLine> basket = book.Basket(cls, need.Id);
        for (int i = 0; i < basket.Length; i++)
            FillLinks(links, prev, cfg, s, basket[i].Good,
                ChainNode.FoodGoodEaten, ChainNode.FoodGoodDemanded, ChainNode.FoodGoodFill);

        FoodSupply(prev, cfg, s, links);
        return new CausalChain(need.Id, need.Name, [.. links]);
    }

    /// <summary>
    /// The food-supply block, reading ONE world: the deficit and what produced
    /// it. Shared by the Sustenance chain (on Prev) and the happiness Food
    /// factor (on the world happiness was asked about), so the two cannot name
    /// different causes for the same shortfall.
    /// </summary>
    public static void FoodSupply(IReadOnlyWorldState w, SimConfig cfg, SettlementId s, List<Link> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("SimConfig.Goods is not loaded.", nameof(cfg));
        var grain = new GoodId(goods.GrainId);

        int d = ExplainRows.Deficit(w, s);
        if (d >= 0)
        {
            into.Add(new Link(ChainNode.DeficitRatio, "DeficitRatio", w.ConsumptionDeficits[d].DeficitRatio,
                LinkKind.Read, SourceWorld.Prev, "ConsumptionDeficits", d,
                "Unmet fraction of the nutritional requirement, substitution counted once "
                + "(ConsumptionSystem.cs:183-190). The famine-flight push and the happiness Food factor read this."));
            into.Add(new Link(ChainNode.NutritionalDemand, "DemandUnits", w.ConsumptionDeficits[d].DemandUnits,
                LinkKind.Read, SourceWorld.Prev, "ConsumptionDeficits", d,
                "Person-year-equivalents required this turn (ConsumptionSystem.cs:183-184): a population fact, no lever."));
        }
        else
        {
            into.Add(new Link(ChainNode.DeficitRatio, "DeficitRatio", double.NaN, LinkKind.Gap,
                SourceWorld.Prev, "ConsumptionDeficits", -1,
                "No deficit row: consumption has not run for this settlement (a founding turn). "
                + "SettlementHappiness.FoodSufficiency reads this absence as 1.0 (SettlementHappiness.cs:118-129)."));
        }

        // GRAIN: store, harvest, eaten — the store-drawdown reading is Harvest < Eaten.
        int g = GoodStockIndex.IndexOf(w.GoodStocks, s, grain);
        if (g >= 0)
        {
            GoodStockRow row = w.GoodStocks[g];
            into.Add(new Link(ChainNode.GrainStore, "grain store (post-step)", row.Amount.Value,
                LinkKind.Read, SourceWorld.Prev, "GoodStocks", g,
                "GoodStockRow.Amount after harvest, eating, spoilage and granary overflow (ConsumptionSystem.cs:192-203)."));
            into.Add(new Link(ChainNode.GrainHarvest, "grain harvest", row.LastProducedUnits,
                LinkKind.Read, SourceWorld.Prev, "GoodStocks", g,
                "LastProducedUnits — units credited by Farm this turn (ProductionSystem.cs:243-247), "
                + "= min(arable × yield, farm labour × output × toolFactor) × weather × dt, whole units."));
            into.Add(new Link(ChainNode.GrainEaten, "grain eaten", row.LastConsumptionEatenUnits,
                LinkKind.Read, SourceWorld.Prev, "GoodStocks", g,
                "LastConsumptionEatenUnits, post-clamp (ConsumptionSystem.cs:224-229). Eaten > Harvest means the "
                + "store was drawn down; in a standing famine Eaten == Harvest < demand."));
        }
        else
        {
            into.Add(new Link(ChainNode.GrainHarvest, "grain harvest", double.NaN, LinkKind.Gap,
                SourceWorld.Prev, "GoodStocks", -1,
                "No grain stock row: Farm credits nothing (ProductionSystem.cs:191-192) and Fill reads 1.0 (case 1)."));
        }

        // The farming Leontief's STORED inputs (ProductionSystem.cs:202-225).
        int sectorIdx;
        SectorAllocationRow shares = ExplainRows.SectorRow(w, s, out sectorIdx);
        const string Lag = " IN FORCE for the step reading this world as Prev (§3.2 one-turn lag): it drives the "
            + "NEXT harvest, not the harvest shown above, which was produced under the previous turn's allocation.";
        into.Add(ShareLink(ChainNode.FarmingShare, "farming share", shares, Sectors.Farming, sectorIdx,
            "Sectors.Share(row, Farming) × adults = farm labour (ProductionSystem.cs:157-158).", Lag));

        int c = ExplainRows.Catchment(w, s);
        into.Add(c >= 0
            ? new Link(ChainNode.ArableLand, "EffectiveArableKm2", w.CatchmentSummaries[c].EffectiveArableKm2,
                LinkKind.Read, SourceWorld.Prev, "CatchmentSummaries", c,
                "Land side of the Leontief: arable × YieldPerArableKm2PerYear (ProductionSystem.cs:202-207, 223). "
                + "A condition of the site's catchment; no M4 lever.")
            : new Link(ChainNode.ArableLand, "EffectiveArableKm2", double.NaN, LinkKind.Gap,
                SourceWorld.Prev, "CatchmentSummaries", -1,
                "No catchment summary yet: Farm reads arable 0.0 (ProductionSystem.cs:202)."));

        int wx = ExplainRows.Weather(w, s);
        into.Add(wx >= 0
            ? new Link(ChainNode.HarvestWeather, "harvest weather multiplier", w.HarvestWeather[wx].Multiplier,
                LinkKind.Read, SourceWorld.Prev, "HarvestWeather", wx,
                "Multiplies realised farm AND herding output after the Leontief minimum (ProductionSystem.cs:239, "
                + "171-175, 306). Mean-one AR(1) weather (HarvestWeatherSystem.cs:30-33); a condition, no lever.")
            : new Link(ChainNode.HarvestWeather, "harvest weather multiplier", double.NaN, LinkKind.Gap,
                SourceWorld.Prev, "HarvestWeather", -1,
                "No weather row for this settlement: Farm uses 1.0 by design (ProductionSystem.cs:329-337)."));

        int toolsId = goods.IdOf("tools");
        int t = toolsId > 0 ? GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(toolsId)) : -1;
        into.Add(t >= 0
            ? new Link(ChainNode.ToolsStock, "tools stock", w.GoodStocks[t].Amount.Value,
                LinkKind.Read, SourceWorld.Prev, "GoodStocks", t,
                "Prev tool stock equips farmers: equipRatio = min(1, stock / (farmLabour × ToolsPerFarmerToEquip)) "
                + "(ProductionSystem.cs:211-221)." + Lag)
            : new Link(ChainNode.ToolsStock, "tools stock", double.NaN, LinkKind.Gap,
                SourceWorld.Prev, "GoodStocks", -1, "No tools stock row: equipRatio reads 0 (ProductionSystem.cs:213-220)."));
        into.Add(new Link(ChainNode.ToolFactor, "tool factor", double.NaN, LinkKind.Gap, SourceWorld.None, "", -1,
            "toolFactor = 1 + ToolYieldBonusMax × equipRatio is computed inside Farm and discarded "
            + "(ProductionSystem.cs:221) — not recorded (§8 item 9)."));
        into.Add(new Link(ChainNode.LandVsLabourBinding, "binding constraint (land vs labour)", double.NaN,
            LinkKind.Gap, SourceWorld.None, "", -1,
            "Which side of min(landSide, labourSide) bound (ProductionSystem.cs:223-225) is computed and discarded "
            + "— not recorded (§8 item 9). The inputs of both sides are the links above."));

        // HERDING/FISHING: every food deposit this settlement holds
        // (ProductionSystem.cs:273-316 with foodSector: true).
        into.Add(ShareLink(ChainNode.HerdingShare, "herding share", shares, Sectors.Herding, sectorIdx,
            "Sectors.Share(row, Herding) × adults = the pool split across food deposits ∝ abundance "
            + "(ProductionSystem.cs:171-175, 300).", Lag));
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow dep = w.Deposits[i];
            if (dep.Settlement != s) continue;
            GoodEntry? entry = null;
            for (int k = 0; k < goods.Goods.Length; k++) if (goods.Goods[k].Id == dep.Good.Value) { entry = goods.Goods[k]; break; }
            if (entry is null || !string.Equals(entry.Category, "food", StringComparison.Ordinal)) continue;
            string name = entry.Name;
            int st = GoodStockIndex.IndexOf(w.GoodStocks, s, dep.Good);
            if (st >= 0)
            {
                into.Add(new Link(ChainNode.DepositFoodProduced, name + " produced", w.GoodStocks[st].LastProducedUnits,
                    LinkKind.Read, SourceWorld.Prev, "GoodStocks", st,
                    "LastProducedUnits = workers × OutputPerHerderPerYear × abundance × weather × dt (ProductionSystem.cs:300-315)."));
            }
            into.Add(new Link(ChainNode.DepositAbundance, name + " deposit abundance", dep.Abundance,
                LinkKind.Read, SourceWorld.Prev, "Deposits", i,
                "Founding endowment (DepositRow.Abundance): sets both the labour split and the per-worker rate "
                + "(ProductionSystem.cs:300-306). A condition, no lever."));
        }

        into.Add(new Link(ChainNode.GrainImports, "grain imports", double.NaN, LinkKind.Gap, SourceWorld.None, "", -1,
            "None: grain is the numeraire, pinned at 1.0 both sides, so its price gap is structurally zero and it "
            + "NEVER moves through trade (TradeArbitrageSystem.cs:63-67, 133-137). Trade is autonomous — no player "
            + "trade lever in M4 (§8 item 12)."));
    }

    // =====================================================================
    // SHELTER
    // =====================================================================

    private static CausalChain Shelter(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId s, ClassId cls, NeedEntry need)
    {
        var links = new List<Link>();
        SatisfactionLink(links, next, s, cls, need, ChainNode.ShelterSatisfaction,
            "s = min(1, dwellings × PersonsPerDwelling / population) on Prev (NeedsGrievanceSystem.cs:133-146); "
            + "settlement-level, so every class carries the same value.");
        HousingSupply(prev, next, cfg, s, links);
        return new CausalChain(need.Id, need.Name, [.. links]);
    }

    /// <summary>
    /// The housing-supply block: sufficiency (the public reader), the dwelling
    /// stock and what moves it. <paramref name="next"/> may equal
    /// <paramref name="w"/> (happiness asks about one world), in which case the
    /// Δdwellings link is omitted rather than reported as zero.
    /// </summary>
    public static void HousingSupply(
        IReadOnlyWorldState w, IReadOnlyWorldState next, SimConfig cfg, SettlementId s, List<Link> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        HousingConfig housing = cfg.Housing ?? throw new ArgumentException("SimConfig.Housing is not loaded.", nameof(cfg));
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("SimConfig.Goods is not loaded.", nameof(cfg));

        int h = ExplainRows.Housing(w, s);
        into.Add(new Link(ChainNode.HousingSufficiency, "housing sufficiency",
            SettlementHappiness.HousingSufficiency(w, s, cfg), LinkKind.Recomputed, SourceWorld.Prev, "Housing", h,
            "SettlementHappiness.HousingSufficiency: clamp(dwellings × PersonsPerDwelling / population, 0, 1) "
            + "(SettlementHappiness.cs:138-159) — the same expression the Shelter need evaluates."
            + (h < 0
                ? " No row: the function reads people-and-no-housing-row as 0.0, nobody-to-house as 1.0 "
                  + "(SettlementHappiness.cs:149, 158) — a colony before HousingSystem's first step materialises "
                  + "its row (HousingSystem.cs:96-101)."
                : "")));

        long pop = ExplainRows.Population(w, s);
        into.Add(new Link(ChainNode.Population, "population", pop, LinkKind.Summed, SourceWorld.Prev, "Buckets", -1,
            "Σ BucketRow.Count over the settlement (NeedsGrievanceSystem.cs:135-137). Demographics and migration "
            + "move it; no lever."));
        into.Add(new Link(ChainNode.PersonsPerDwelling, "PersonsPerDwelling", housing.PersonsPerDwelling,
            LinkKind.Read, SourceWorld.Config, "SimConfig", -1, "sim.json housing.personsPerDwelling (TUNE)."));

        if (h >= 0)
        {
            HousingRow row = w.Housing[h];
            into.Add(new Link(ChainNode.Dwellings, "dwellings", row.Dwellings.Value, LinkKind.Read,
                SourceWorld.Prev, "Housing", h, "HousingRow.Dwellings, the conserved stock (HousingSystem.cs:107)."));
            if (!ReferenceEquals(w, next))
            {
                int hn = ExplainRows.Housing(next, s);
                if (hn >= 0)
                {
                    into.Add(new Link(ChainNode.DwellingsDelta, "Δdwellings this step",
                        next.Housing[hn].Dwellings.Value - row.Dwellings.Value, LinkKind.Differenced,
                        SourceWorld.Next, "Housing", hn,
                        "next − prev dwellings = built − decayed; the split is not recorded (§8 item 6)."));
                }
            }
            into.Add(new Link(ChainNode.MaintenanceFraction, "LastMaintenanceFraction", row.LastMaintenanceFraction,
                LinkKind.Read, SourceWorld.Prev, "Housing", h,
                "m = min over materials of available/upkeep-demand, Leontief (HousingSystem.cs:109-117); the "
                + "unmaintained share decays as exp(−(1−m)·dt/τ) (HousingSystem.cs:127-140)."));
            into.Add(new Link(ChainNode.ConstructionLabourUsed, "LastLaborUsed (adult-years)", row.LastLaborUsed,
                LinkKind.Read, SourceWorld.Prev, "Housing", h,
                "Adult-years spent building last turn (HousingSystem.cs:183, 192); PathBuild and Construction subtract "
                + "it from the same pool at the one-turn lag."));
        }
        else
        {
            into.Add(new Link(ChainNode.Dwellings, "dwellings", double.NaN, LinkKind.Gap, SourceWorld.Prev, "Housing", -1,
                "No housing row: people and no row read Shelter 0.0 (NeedsGrievanceSystem.cs:145); HousingSystem "
                + "creates the row on its first step (HousingSystem.cs:96-101)."));
        }

        int sectorIdx;
        SectorAllocationRow shares = ExplainRows.SectorRow(w, s, out sectorIdx);
        const string Lag = " In force for the step reading this world as Prev (§3.2 one-turn lag).";
        into.Add(ShareLink(ChainNode.ConstructionShare, "construction share", shares, Sectors.Construction, sectorIdx,
            "builderYears = Sectors.Share(row, Construction) × adults × dt; laborCap = builderYears / "
            + "BuildLaborAdultYearsPerDwelling (HousingSystem.cs:145-150, 160-161).", Lag));

        int timberId = goods.IdOf("timber");
        int t = timberId > 0 ? GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(timberId)) : -1;
        into.Add(t >= 0
            ? new Link(ChainNode.TimberStock, "timber stock", w.GoodStocks[t].Amount.Value, LinkKind.Read,
                SourceWorld.Prev, "GoodStocks", t,
                "Upkeep demand dwellings × UpkeepTimberPerDwellingYear × dt against this stock (HousingSystem.cs:111-115); "
                + "timberCap = stock / BuildTimberPerDwelling (HousingSystem.cs:162-164). Housing reads its OWN shared "
                + "stock table live, so the value shown is the post-step Prev stock.")
            : new Link(ChainNode.TimberStock, "timber stock", double.NaN, LinkKind.Gap, SourceWorld.Prev, "GoodStocks", -1,
                "No timber row: available reads 0.0 and nothing can be built or maintained (HousingSystem.cs:113, 163)."));
        // Clay is wired in HousingSystem (HousingSystem.cs:114-117, 165-167) but
        // the shipped coefficients are 0.0 (sim.json housing: structural earth is
        // a non-good, dug on site). A link with a zero coefficient binds nothing,
        // so it appears only when the data makes it a dependency.
        if (housing.BuildClayPerDwelling > 0.0 || housing.UpkeepClayPerDwellingYear > 0.0)
        {
            int clayId = goods.IdOf("clay");
            int cl = clayId > 0 ? GoodStockIndex.IndexOf(w.GoodStocks, s, new GoodId(clayId)) : -1;
            into.Add(cl >= 0
                ? new Link(ChainNode.ClayStock, "clay stock", w.GoodStocks[cl].Amount.Value, LinkKind.Read,
                    SourceWorld.Prev, "GoodStocks", cl,
                    "Clay upkeep and build caps (HousingSystem.cs:112-117, 165-167) — a dependency because the "
                    + "config coefficients are non-zero.")
                : new Link(ChainNode.ClayStock, "clay stock", double.NaN, LinkKind.Gap, SourceWorld.Prev, "GoodStocks", -1,
                    "No clay row while the config demands clay: available reads 0.0."));
        }
        into.Add(ShareLink(ChainNode.ExtractionShare, "extraction share", shares, Sectors.Extraction, sectorIdx,
            "Timber is a deposit good produced by the extraction pool (ProductionSystem.cs:176-180, 273-316).", Lag));
        into.Add(new Link(ChainNode.BuiltDecayedSplit, "built / decayed split", double.NaN, LinkKind.Gap,
            SourceWorld.None, "", -1,
            "Built (HousingSystem.cs:170-179) and decayed (HousingSystem.cs:130-140) are two Ledger flows recorded "
            + "only as WORLD totals; per settlement only Δdwellings and LastMaintenanceFraction survive (§8 item 6)."));
        into.Add(new Link(ChainNode.BuildBindingConstraint, "binding build cap (deficit / labour / timber / clay)",
            double.NaN, LinkKind.Gap, SourceWorld.None, "", -1,
            "min(min(deficit, laborCap), min(timberCap, clayCap)) (HousingSystem.cs:168) — which cap bound is "
            + "computed and discarded; not recorded (§8 item 9)."));
    }

    // =====================================================================
    // BASKET-BOUND (Comfort)
    // =====================================================================

    private static CausalChain Basket(
        IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg,
        SettlementId s, ClassId cls, NeedEntry need)
    {
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("SimConfig.Goods is not loaded.", nameof(cfg));
        var links = new List<Link>();
        SatisfactionLink(links, next, s, cls, need, ChainNode.ComfortSatisfaction,
            "s = clamp(obtained/wanted) × varietyFactor over the class's basket, no substitution "
            + "(NeedsGrievanceSystem.cs:335-344); every input is a Prev fill ratio.");

        BasketBook book = Book(cfg);
        ReadOnlySpan<BasketLine> basket = book.Basket(cls, need.Id);
        int sectorIdx;
        SectorAllocationRow shares = ExplainRows.SectorRow(prev, s, out sectorIdx);
        const string Lag = " In force for the step reading this world as Prev (§3.2 one-turn lag).";
        bool anyRecipe = false, anyDeposit = false;

        for (int i = 0; i < basket.Length; i++)
        {
            GoodId good = basket[i].Good;
            string name = ExplainRows.GoodName(cfg, good);
            FillLinks(links, prev, cfg, s, good,
                ChainNode.ComfortGoodEaten, ChainNode.ComfortGoodDemanded, ChainNode.ComfortGoodFill);

            int st = GoodStockIndex.IndexOf(prev.GoodStocks, s, good);
            if (st >= 0)
            {
                links.Add(new Link(ChainNode.CraftOutputProduced, name + " produced", prev.GoodStocks[st].LastProducedUnits,
                    LinkKind.Read, SourceWorld.Prev, "GoodStocks", st,
                    "LastProducedUnits, zeroed then credited by the producing sector this turn (ProductionSystem.cs:134-141)."));
            }

            // The recipes that OUTPUT this good, and their inputs (ProductionSystem.Craft, 347-443).
            for (int r = 0; r < goods.Recipes.Length; r++)
            {
                RecipeEntry recipe = goods.Recipes[r];
                if (goods.IdOf(recipe.Output.Good) != good.Value) continue;
                anyRecipe = true;
                for (int k = 0; k < recipe.Inputs.Length; k++)
                {
                    var input = new GoodId(goods.IdOf(recipe.Inputs[k].Good));
                    string inName = ExplainRows.GoodName(cfg, input);
                    int inRow = GoodStockIndex.IndexOf(prev.GoodStocks, s, input);
                    if (inRow < 0)
                    {
                        links.Add(new Link(ChainNode.CraftInputStock, inName + " stock (input of " + recipe.Name + ")",
                            double.NaN, LinkKind.Gap, SourceWorld.Prev, "GoodStocks", -1,
                            "No stock row: the input cap reads stock 0 and the recipe makes nothing (ProductionSystem.cs:410-415)."));
                        continue;
                    }
                    GoodStockRow inStock = prev.GoodStocks[inRow];
                    links.Add(new Link(ChainNode.CraftInputStock, inName + " stock (input of " + recipe.Name + ")",
                        inStock.Amount.Value, LinkKind.Read, SourceWorld.Prev, "GoodStocks", inRow,
                        "Leontief cap: output ≤ stock / (PerOutput / Output.Qty) (ProductionSystem.cs:406-414). "
                        + "Production reads its own shared stock live; this is the post-step Prev stock."));
                    links.Add(new Link(ChainNode.CraftInputDemand, inName + " LastInputDemandUnits",
                        inStock.LastInputDemandUnits, LinkKind.Read, SourceWorld.Prev, "GoodStocks", inRow,
                        "Units recipes WANTED from labour alone, before any input cap (ProductionSystem.cs:386-404); "
                        + "demand above the stock link is the shortage."));
                }
            }

            // A basket good that is a DEPOSIT good instead (none in shipped data;
            // wired so a re-basketed need cannot render an empty chain).
            int dep = ExplainRows.Deposit(prev, s, good);
            if (dep >= 0)
            {
                anyDeposit = true;
                links.Add(new Link(ChainNode.DepositAbundance, name + " deposit abundance", prev.Deposits[dep].Abundance,
                    LinkKind.Read, SourceWorld.Prev, "Deposits", dep,
                    "Founding endowment; sets the labour split and per-worker rate (ProductionSystem.cs:300-306)."));
            }
        }

        if (anyRecipe)
        {
            links.Add(ShareLink(ChainNode.CraftingShare, "crafting share", shares, Sectors.Crafting, sectorIdx,
                "pool = Sectors.Share(row, Crafting) × adults, split EQUALLY across available recipes "
                + "(ProductionSystem.cs:181-182, 366-369).", Lag));
            links.Add(ShareLink(ChainNode.ExtractionShare, "extraction share", shares, Sectors.Extraction, sectorIdx,
                "Raw inputs (clay, timber, fiber) are deposit goods produced by the extraction pool "
                + "(ProductionSystem.cs:176-180, 273-316).", Lag));
            links.Add(new Link(ChainNode.RecipeLabourCap, "per-recipe labour cap", double.NaN, LinkKind.Gap,
                SourceWorld.None, "", -1,
                "laborPerRecipe / LaborPerOutput × dt × Qty, and which of labour or input bound "
                + "(ProductionSystem.cs:369, 382-384, 412-413) — computed inside Craft, not recorded (§8 item 9)."));
        }
        else if (anyDeposit)
        {
            links.Add(ShareLink(ChainNode.ExtractionShare, "extraction share", shares, Sectors.Extraction, sectorIdx,
                "The deposit pool (ProductionSystem.cs:176-180).", Lag));
        }

        return new CausalChain(need.Id, need.Name, [.. links]);
    }

    // =====================================================================
    // shared pieces
    // =====================================================================

    /// <summary>A sector share IN FORCE, through the public accessor. When the
    /// settlement was never ordered there is no row and <see cref="Sectors.Default"/>
    /// is what every consumer applies — the note says so, because a founded
    /// world that was never ordered has NO SectorAllocations row at all.</summary>
    private static Link ShareLink(
        ChainNode node, string label, in SectorAllocationRow shares, int sector, int sectorIdx,
        string note, string lag) =>
        new(node, label, Sectors.Share(shares, sector), LinkKind.Recomputed, SourceWorld.Prev,
            "SectorAllocations", sectorIdx,
            note + (sectorIdx < 0 ? " No row: Sectors.Default in force." : "") + lag);

    private static BasketBook Book(SimConfig cfg)
    {
        NeedsConfig needs = cfg.Needs ?? throw new ArgumentException("SimConfig.Needs is not loaded.", nameof(cfg));
        GoodsConfig goods = cfg.Goods ?? throw new ArgumentException("SimConfig.Goods is not loaded.", nameof(cfg));
        return new BasketBook(needs, goods);
    }

    private static void SatisfactionLink(
        List<Link> links, IReadOnlyWorldState next, SettlementId s, ClassId cls, NeedEntry need,
        ChainNode node, string note)
    {
        int i = ExplainRows.Satisfaction(next, s, cls, need.Id);
        links.Add(i >= 0
            ? new Link(node, need.Name + " satisfaction", next.NeedSatisfactions[i].Value, LinkKind.Read,
                SourceWorld.Next, "NeedSatisfactions", i, note)
            : new Link(node, need.Name + " satisfaction", double.NaN, LinkKind.Gap, SourceWorld.Next,
                "NeedSatisfactions", -1,
                "No satisfaction row published: the class had no members, the settlement was extinct, or it was "
                + "not in Prev (NeedsGrievanceSystem.cs:187-193, 231-235)."));
    }

    /// <summary>
    /// The fill ratio for one good: the two READ integers the system reads, then
    /// the fill as a CALL to <see cref="NeedsGrievanceSystem.Fill(in GoodStockRow)"/>
    /// on that same row — RECOMPUTED in the §0 sense. The note says which of the
    /// system's three cases applied (NeedsGrievanceSystem.cs:378-396), read off
    /// the row's demand sign; the VALUE never comes from this file. The first
    /// cut divided the two longs here and called it READ; the verifier's mutant
    /// (case 3 → 1.0 in THAT copy) survived because nothing tied the copy to
    /// the system — now there is no copy, the equivalent mutant in the system
    /// moves this link and the published satisfaction together, and the
    /// kill-record test pins the quotient (ExplainRecomputedFunctionTests).
    /// </summary>
    private static void FillLinks(
        List<Link> links, IReadOnlyWorldState prev, SimConfig cfg, SettlementId s, GoodId good,
        ChainNode eatenNode, ChainNode demandedNode, ChainNode fillNode)
    {
        string name = ExplainRows.GoodName(cfg, good);
        int i = GoodStockIndex.IndexOf(prev.GoodStocks, s, good);
        if (i < 0)
        {
            links.Add(new Link(fillNode, name + " fill", NeedsGrievanceSystem.Fill(prev, s, good), LinkKind.Recomputed,
                SourceWorld.Prev, "GoodStocks", -1,
                "No row: no stock row for this good here — nothing wanted from a market that is not there; "
                + "NeedsGrievanceSystem.Fill reads 1.0 (case 1, NeedsGrievanceSystem.cs:380-381)."));
            return;
        }
        GoodStockRow row = prev.GoodStocks[i];
        links.Add(new Link(eatenNode, name + " eaten", row.LastConsumptionEatenUnits, LinkKind.Read,
            SourceWorld.Prev, "GoodStocks", i, "LastConsumptionEatenUnits, post-clamp (ConsumptionSystem.cs:224-229)."));
        links.Add(new Link(demandedNode, name + " demanded", row.LastConsumptionDemandUnits, LinkKind.Read,
            SourceWorld.Prev, "GoodStocks", i, "LastConsumptionDemandUnits, pre-clamp (ConsumptionSystem.cs:222-228)."));
        links.Add(new Link(fillNode, name + " fill", NeedsGrievanceSystem.Fill(in row), LinkKind.Recomputed,
            SourceWorld.Prev, "GoodStocks", i,
            row.LastConsumptionDemandUnits <= 0
                ? "NeedsGrievanceSystem.Fill on this row: demand quantised to zero units this turn, so the STOCK "
                  + "discriminates — empty store 0.0, else 1.0 (case 2, NeedsGrievanceSystem.cs:392-393)."
                : "NeedsGrievanceSystem.Fill on this row: eaten / demanded, clamped to [0,1] "
                  + "(case 3, NeedsGrievanceSystem.cs:394-395)."));
    }
}
