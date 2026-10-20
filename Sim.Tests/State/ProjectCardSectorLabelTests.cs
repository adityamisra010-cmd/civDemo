using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.Systems.IncrementalConstructionTests;

namespace Sim.Tests.State;

/// <summary>
/// D-054 WS1 (batch-3 integration, 2026-10-14; verifier 2 F10(b), the C1 fixer's open F9(c)) — THE PROJECT CARD NAMES
/// THE CONSTRUCTION SECTOR AS THE PLAYER'S LABOUR BLOCK DOES. The card's no-builders pace line ("give X a share in
/// Labour") takes X from LabourActivities.CapabilityLabel — the knowledge-derived label the Labour block and the queue's
/// status line show — falling back to "Building" when nothing names it. Shipped content gives the construction sector
/// no researched identity and names its baseline "Building", so the shipped card cannot tell the derived label from a
/// literal (the C1 fixer: "no test can distinguish"). This test renames the sector's baseline identity in a COPY of
/// research.json (content data, not code), so the derived label and the literal differ, and pins that the card follows
/// the content — the same words the Labour block shows.
/// </summary>
public class ProjectCardSectorLabelTests
{
    private const string Renamed = "Raising Walls";

    /// <summary>research.json with the construction sector's baseline identity named <see cref="Renamed"/>.</summary>
    private static ResearchContent RenamedConstruction()
    {
        JsonObject doc = JsonNode.Parse(TestConfigs.ResearchJson())!.AsObject();
        int renamed = 0;
        foreach (JsonNode? sector in doc["sectorActivities"]!.AsArray())
        {
            if ((string?)sector!["sector"] != "construction") continue;
            Assert.Equal("Building", (string?)sector["baseline"]!["name"]);
            sector["baseline"]!["name"] = Renamed;
            renamed++;
        }
        Assert.Equal(1, renamed);
        return ResearchContentLoader.Load(doc.ToJsonString(), Cfg.Goods);
    }

    private static string PaceNote(SimConfig cfg)
    {
        WorldState w = Solo();
        NoBuilders(w, S0);                                     // construction share 0: capacity 0, the no-builders line
        Assert.Equal(0.0, ConstructionQuery.CapacityAdultYears(w, cfg, S0, 10.0));
        InfoCard card = InfoQuery.Card(w, cfg, Player, InfoSubject.OfProject(Granary, S0.Value), new ActionQueryContext(null, 10.0));
        InfoLink labour = Assert.Single(card.Requirements, r => r.Label.EndsWith("adult-years of construction labour", StringComparison.Ordinal));
        Assert.Equal(false, labour.Met);
        return labour.Note!;
    }

    [Fact]
    public void TheNoBuildersLine_NamesTheSectorByTheContentsLabel_TheSameWordsTheLabourBlockShows()
    {
        Assert.Equal("no builders here this turn - give Building a share in Labour", PaceNote(Cfg));   // shipped

        SimConfig cfg = Cfg with { Research = RenamedConstruction() };
        WorldState w = Solo();
        LabourActivity? block = LabourActivities.Of(w, cfg, Player, S0, Sectors.Construction);
        Assert.NotNull(block);
        Assert.Equal(Renamed, block.Label);                                                          // the Labour block
        Assert.Equal("no builders here this turn - give " + Renamed + " a share in Labour", PaceNote(cfg));   // the card
    }
}
