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
        // MERGE T4.21-4 x T4.21-5: reachable only on the MERGED tree. T4.21-5's
        // CausalChain cites Disasters for DisasterMultiplierApplied, but with the
        // hazard disarmed that link is always a GAP, which never reaches this
        // switch. T4.21-4 armed it (sim.json disaster.hazardPerYear 0.0 -> 0.01),
        // the row existed, the link became a READ, and the name had no entry.
        // Neither branch could see this alone.
        // T4.21-7 disarms the shipped hazard again (CR-016 — the mechanism ships
        // inert and the rate is the director's), so on the SHIPPED config this
        // arm is a GAP once more and this entry is unreached by a shipped-config
        // run. It STAYS: it is a name-resolution table, not a measurement, the
        // link is exercised the moment any rig arms λ (FamineScenarioTests,
        // DisasterSystemTests), and it is one data edit from being reached on
        // the shipped config too. Keeping it ADDS coverage — the cited index is
        // bounds-checked like every other table's — and deleting it would
        // re-introduce the exact defect the merge found.
        "Disasters" => w.Disasters.Count,
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
                    {
                        AssertSourcesExist(f.Chain, next, next, $"{name} t{t} happiness {f.Name} s{next.Settlements[i].Id.Value}");
                        // A2-LABEL: happiness asks about ONE world, so no link
                        // of its chains may claim Prev — the (next, next)
                        // resolution above cannot tell the two labels apart.
                        foreach (Link l in f.Chain)
                            Assert.True(l.World != SourceWorld.Prev, $"{name} t{t} happiness {f.Name}: {l.Node} claims Prev");
                    }
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

    /// <summary>
    /// A2-FIX D3. A colony founded THIS step is in Next and not in Prev, has
    /// people, and has no HousingRow until HousingSystem's first step
    /// materialises one. The first cut emitted its housing sufficiency as a
    /// RECOMPUTED link citing Housing[−1] with a note that did not say so —
    /// the one shape the checker above rejects — and no rig ever founded a
    /// colony, so the checker never saw it. Now the same checker walks every
    /// chain of the colony's every need and class, and both happiness chains.
    /// The sufficiency link stays RECOMPUTED, because
    /// SettlementHappiness.HousingSufficiency is a public function that
    /// DEFINES people-and-no-row as 0.0 (SettlementHappiness.cs:158) — the
    /// value is a simulation quantity, the missing row is named in the note,
    /// and the Dwellings link beneath it is the GAP.
    /// </summary>
    [Fact]
    public void Colony_WithoutAHousingRow_EveryLinkStillCitesHonestly()
    {
        (SimConfig cfg, WorldState prev, WorldState next, SettlementId colony) = ExplainRigs.Colony(prefixTurns: 3);
        Assert.Equal(-1, HousingIndex(next, colony));
        Assert.Equal(-1, HousingIndex(prev, colony));

        int chains = 0, sufficiencyLinks = 0;
        foreach ((SettlementId s, ClassId c) in ExplainRigs.GrievanceKeys(next))
        {
            if (s != colony) continue;
            GrievanceExplanation g = GrievanceExplanation.For(prev, next, cfg, s, c);
            Assert.False(g.SteppedBySystem);
            Assert.Equal(g.Total, g.Recomputed);          // not stepped: the row is what founding left (0)
            Assert.Equal(-1, g.PrimaryNeedId);            // nothing published, nothing to rank
            for (int i = 0; i < g.Needs.Length; i++)             // registry order, registry ids
                if (cfg.Needs!.Needs[i].Bound) Assert.Contains("founded this step", g.Needs[i].Note);

            foreach (NeedEntry need in cfg.Needs!.Needs)
            {
                CausalChain chain = CausalChain.ForNeed(prev, next, cfg, s, c, need.Id);
                AssertSourcesExist(chain.Links, prev, next, $"colony s{s.Value} c{c.Value} need {need.Id}");
                chains++;
                foreach (Link l in chain.Links)
                {
                    if (l.Node != ChainNode.HousingSufficiency) continue;
                    sufficiencyLinks++;
                    Assert.Equal(LinkKind.Recomputed, l.Kind);
                    Assert.Equal(-1, l.SourceIndex);
                    Assert.Contains("No row", l.Note);
                    // The Shelter chain sits on PREV (what the system read), and
                    // the colony is not there: nobody to house reads 1.0
                    // (SettlementHappiness.cs:149). The other branch — people
                    // and no row, 0.0 — is the happiness chain on Next, below.
                    Assert.Equal(1.0, l.Value);
                    Assert.Equal(SettlementHappiness.HousingSufficiency(prev, colony, cfg), l.Value);
                }
                if (need.FromHousingStock)
                    Assert.Equal(LinkKind.Gap, ExplainGrievanceTests.Single(chain.Links, ChainNode.Dwellings).Kind);
            }
        }
        Assert.True(chains > 0, "rig vacuous: the colony has no grievance rows");
        Assert.True(sufficiencyLinks > 0, "rig vacuous: no Shelter chain was built for the colony");

        HappinessExplanation h = HappinessExplanation.For(next, cfg, colony);
        foreach (HappinessFactor f in h.Factors)
            AssertSourcesExist(f.Chain, next, next, $"colony happiness {f.Name}");
        Link housing = ExplainGrievanceTests.Single(h.Factors[(int)SettlementHappiness.Factor.Housing].Chain, ChainNode.HousingSufficiency);
        Assert.Equal(LinkKind.Recomputed, housing.Kind);
        Assert.Equal(-1, housing.SourceIndex);
        Assert.Contains("No row", housing.Note);
        Assert.Equal(0.0, housing.Value);                 // people, no row: SettlementHappiness.cs:158
        Assert.Equal(h.Factors[(int)SettlementHappiness.Factor.Housing].Value, housing.Value);
        Assert.Equal(LinkKind.Gap, ExplainGrievanceTests.Single(h.Factors[(int)SettlementHappiness.Factor.Housing].Chain, ChainNode.Dwellings).Kind);
    }

    private static int HousingIndex(WorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Housing.Count; i++) if (w.Housing[i].Settlement == s) return i;
        return -1;
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
