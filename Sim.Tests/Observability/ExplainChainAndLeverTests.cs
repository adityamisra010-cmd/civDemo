using Sim.Core.Kernel;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Tests.Observability;

/// <summary>
/// T4.19 lane A2 — every link points at a row that exists in the world it
/// claims to have read, every GAP is labelled, and every lever names an
/// existing order kind or none.
/// </summary>
public class ExplainChainAndLeverTests
{
    /// <summary>The tables a link may cite, resolved by name in the named world.
    /// A name not listed here fails the test — a chain cannot cite a table this
    /// checker cannot open.</summary>
    private static int Count(IReadOnlyWorldState w, string table) => table switch
    {
        "NeedSatisfactions" => w.NeedSatisfactions.Count,
        "GoodStocks" => w.GoodStocks.Count,
        "ConsumptionDeficits" => w.ConsumptionDeficits.Count,
        "SectorAllocations" => w.SectorAllocations.Count,
        "CatchmentSummaries" => w.CatchmentSummaries.Count,
        "HarvestWeather" => w.HarvestWeather.Count,
        "Deposits" => w.Deposits.Count,
        "Housing" => w.Housing.Count,
        "Buckets" => w.Buckets.Count,
        _ => throw new InvalidOperationException($"link cites unknown table '{table}'"),
    };

    private static void AssertSourcesExist(Link[] links, IReadOnlyWorldState prev, IReadOnlyWorldState next, string where)
    {
        Assert.NotEmpty(links);
        foreach (Link link in links)
        {
            string at = $"{where}: {link.Node} '{link.Label}'";
            switch (link.Kind)
            {
                case LinkKind.Gap:
                    Assert.Equal(-1, link.SourceIndex);
                    Assert.False(string.IsNullOrWhiteSpace(link.Note), at + " — a GAP must say what is missing");
                    break;
                case LinkKind.Read when link.World == SourceWorld.Config:
                    Assert.Equal("SimConfig", link.SourceTable);
                    break;
                case LinkKind.Read:
                case LinkKind.Differenced:
                {
                    IReadOnlyWorldState w = link.World == SourceWorld.Next ? next : prev;
                    Assert.True(link.World is SourceWorld.Prev or SourceWorld.Next, at + " — a READ names a world");
                    int count = Count(w, link.SourceTable);
                    Assert.True(link.SourceIndex >= 0 && link.SourceIndex < count,
                        at + $" — cites {link.SourceTable}[{link.SourceIndex}] but the table holds {count} rows");
                    break;
                }
                case LinkKind.Summed:
                    Assert.True(Count(link.World == SourceWorld.Next ? next : prev, link.SourceTable) > 0,
                        at + " — sums over an empty table");
                    break;
                case LinkKind.Recomputed:
                    if (link.SourceIndex >= 0)
                    {
                        IReadOnlyWorldState w = link.World == SourceWorld.Next ? next : prev;
                        Assert.True(link.SourceIndex < Count(w, link.SourceTable), at + " — cites a row past the table");
                    }
                    else Assert.Contains("No row", link.Note);
                    break;
            }
            Assert.False(string.IsNullOrWhiteSpace(link.Note), at + " — every link cites where it was read");
        }
    }

    [Fact]
    public void EveryLink_CitesARowThatExists_InTheWorldItNames()
    {
        int chains = 0, reads = 0, gaps = 0;
        void CheckRun(SimConfig cfg, List<WorldState> worlds, string name)
        {
            for (int t = 1; t < worlds.Count; t++)
            {
                WorldState prev = worlds[t - 1], next = worlds[t];
                foreach ((SettlementId s, ClassId c) in ExplainRigs.GrievanceKeys(next))
                {
                    foreach (NeedEntry need in cfg.Needs!.Needs)
                    {
                        CausalChain chain = CausalChain.ForNeed(prev, next, cfg, s, c, need.Id);
                        AssertSourcesExist(chain.Links, prev, next, $"{name} t{t} s{s.Value} c{c.Value} need {need.Id}");
                        chains++;
                        foreach (Link l in chain.Links) { if (l.Kind == LinkKind.Read) reads++; if (l.Kind == LinkKind.Gap) gaps++; }
                    }
                }
                for (int i = 0; i < next.Settlements.Count; i++)
                {
                    HappinessExplanation h = HappinessExplanation.For(next, cfg, next.Settlements[i].Id);
                    foreach (HappinessFactor f in h.Factors)
                        AssertSourcesExist(f.Chain, next, next, $"{name} t{t} happiness {f.Name} s{next.Settlements[i].Id.Value}");
                }
            }
        }

        (SimConfig fedCfg, List<WorldState> fed) = ExplainRigs.Fed(4);
        CheckRun(fedCfg, fed, "fed");
        (SimConfig starvedCfg, List<WorldState> starved, int first) = ExplainRigs.Starved(12);
        Assert.True(first > 0);
        CheckRun(starvedCfg, starved, "starved");

        Assert.True(chains > 0);
        Assert.True(reads > 0, "vacuous: no READ link was checked");
        Assert.True(gaps > 0, "vacuous: no GAP link was checked");
    }

    [Fact]
    public void SustenanceChain_CarriesTheStoredFarmInputs_AndTheDocumentedGaps()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(3);
        CausalChain chain = CausalChain.ForNeed(worlds[2], worlds[3], cfg, new SettlementId(0), new ClassId(1), 1);
        ChainNode[] expected =
        [
            ChainNode.SustenanceSatisfaction, ChainNode.FoodGoodFill, ChainNode.DeficitRatio, ChainNode.GrainHarvest,
            ChainNode.GrainEaten, ChainNode.FarmingShare, ChainNode.ArableLand, ChainNode.HarvestWeather,
            ChainNode.ToolsStock, ChainNode.ToolFactor, ChainNode.LandVsLabourBinding, ChainNode.HerdingShare,
            ChainNode.GrainImports,
        ];
        foreach (ChainNode node in expected)
            Assert.Contains(chain.Links, l => l.Node == node);
        Assert.Equal(LinkKind.Gap, ExplainGrievanceTests.Single(chain.Links, ChainNode.ToolFactor).Kind);
        Assert.Equal(LinkKind.Gap, ExplainGrievanceTests.Single(chain.Links, ChainNode.LandVsLabourBinding).Kind);
        Assert.Equal(LinkKind.Gap, ExplainGrievanceTests.Single(chain.Links, ChainNode.GrainImports).Kind);
        Assert.Equal(LinkKind.Recomputed, ExplainGrievanceTests.Single(chain.Links, ChainNode.FarmingShare).Kind);
        // The share shown is the one IN FORCE, through the public accessor — and
        // the note says which turn's harvest it drives.
        Link farming = ExplainGrievanceTests.Single(chain.Links, ChainNode.FarmingShare);
        Assert.Contains("one-turn lag", farming.Note);
        // The weather row exists on a founded world with the production pipeline: READ, not GAP.
        Assert.Equal(LinkKind.Read, ExplainGrievanceTests.Single(chain.Links, ChainNode.HarvestWeather).Kind);
    }

    [Fact]
    public void ComfortChain_ListsRecipeInputs_StocksAndInputDemand()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(3);
        CausalChain chain = CausalChain.ForNeed(worlds[2], worlds[3], cfg, new SettlementId(0), new ClassId(1), 6);
        Assert.Contains(chain.Links, l => l.Node == ChainNode.ComfortSatisfaction);
        Assert.Contains(chain.Links, l => l.Node == ChainNode.ComfortGoodFill && l.Label.StartsWith("pottery", StringComparison.Ordinal));
        Assert.Contains(chain.Links, l => l.Node == ChainNode.ComfortGoodFill && l.Label.StartsWith("cloth", StringComparison.Ordinal));
        // pottery-firing: clay + timber; weaving: fiber — from goods.json, read through the registry.
        Assert.Contains(chain.Links, l => l.Node == ChainNode.CraftInputStock && l.Label.StartsWith("clay", StringComparison.Ordinal));
        Assert.Contains(chain.Links, l => l.Node == ChainNode.CraftInputStock && l.Label.StartsWith("timber", StringComparison.Ordinal));
        Assert.Contains(chain.Links, l => l.Node == ChainNode.CraftInputStock && l.Label.StartsWith("fiber", StringComparison.Ordinal));
        Assert.Contains(chain.Links, l => l.Node == ChainNode.CraftInputDemand);
        Assert.Contains(chain.Links, l => l.Node == ChainNode.CraftingShare && l.Kind == LinkKind.Recomputed);
        Assert.Contains(chain.Links, l => l.Node == ChainNode.RecipeLabourCap && l.Kind == LinkKind.Gap);
    }

    [Fact]
    public void ShelterChain_ClayAppearsOnlyWhenTheDataDrawsIt()
    {
        (SimConfig cfg, List<WorldState> worlds) = ExplainRigs.Fed(2);
        // Shipped data: buildClay 0, upkeepClay 0 — structural earth is a non-good.
        Assert.Equal(0.0, cfg.Housing.BuildClayPerDwelling);
        Assert.Equal(0.0, cfg.Housing.UpkeepClayPerDwellingYear);
        CausalChain shipped = CausalChain.ForNeed(worlds[1], worlds[2], cfg, new SettlementId(0), new ClassId(1), 2);
        Assert.DoesNotContain(shipped.Links, l => l.Node == ChainNode.ClayStock);
        Assert.Contains(shipped.Links, l => l.Node == ChainNode.TimberStock && l.Kind == LinkKind.Read);
        Assert.Contains(shipped.Links, l => l.Node == ChainNode.DwellingsDelta && l.Kind == LinkKind.Differenced);
        Assert.Contains(shipped.Links, l => l.Node == ChainNode.BuiltDecayedSplit && l.Kind == LinkKind.Gap);

        SimConfig clay = cfg with { Housing = cfg.Housing with { BuildClayPerDwelling = 0.5 } };
        CausalChain withClay = CausalChain.ForNeed(worlds[1], worlds[2], clay, new SettlementId(0), new ClassId(1), 2);
        Assert.Contains(withClay.Links, l => l.Node == ChainNode.ClayStock && l.Kind == LinkKind.Read);
    }

    [Fact]
    public void Levers_CoverEveryNode_AndNameOnlyTheExistingSectorOrder()
    {
        int none = 0, sliders = 0;
        foreach (ChainNode node in Enum.GetValues<ChainNode>())
        {
            Lever lever = Levers.For(node);
            Assert.False(string.IsNullOrWhiteSpace(lever.Reason), $"{node}: a lever carries a reason");
            if (lever.IsNone)
            {
                none++;
                Assert.Empty(lever.Sectors);
                continue;
            }
            sliders++;
            // M4's one policy: the five-sector allocation. Never another kind,
            // never an invented one.
            Assert.Equal(OrderKind.SectorAllocation, lever.Kind);
            Assert.True(Enum.IsDefined(lever.Kind!.Value));
            Assert.NotEmpty(lever.Sectors);
            foreach (int sector in lever.Sectors)
                Assert.True(sector >= 0 && sector < Sectors.Count, $"{node}: sector {sector} is not one of the five");
        }
        Assert.True(none > 0, "vacuous: no node is honestly lever-less");
        Assert.True(sliders > 0, "vacuous: no node maps to the slider");

        // The conditions the doc names as lever-less are lever-less here.
        Assert.True(Levers.For(ChainNode.HarvestWeather).IsNone);
        Assert.True(Levers.For(ChainNode.ArableLand).IsNone);
        Assert.True(Levers.For(ChainNode.DepositAbundance).IsNone);
        Assert.True(Levers.For(ChainNode.GrainImports).IsNone);
        Assert.Equal([Sectors.Farming], Levers.For(ChainNode.FarmingShare).Sectors);
        Assert.Equal([Sectors.Construction], Levers.For(ChainNode.ConstructionShare).Sectors);
    }
}
