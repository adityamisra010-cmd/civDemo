using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-033 D9 / audit E37 — the player-facing views that replace the record dumps as the player's surface,
/// and the developer toggle that keeps the dumps for developers.
/// </summary>
public class PlayerViewsTests
{
    private static UiSession Played(int turns)
    {
        var session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < turns; t++) session.EndTurn();
        return session;
    }

    private static SettlementRecord? Record(UiSession s, int id) =>
        s.Observations.Observations.Count == 0 ? null : s.Observations.Settlement(s.Observations.LastTurn, id);

    private static string[] Headings(PlayerView v)
    {
        var h = new string[v.Blocks.Count];
        for (int i = 0; i < h.Length; i++) h[i] = v.Blocks[i].Heading;
        return h;
    }

    private static IEnumerable<string> AllLines(PlayerView v)
    {
        yield return v.Title;
        foreach (ViewBlock b in v.Blocks) foreach (string l in b.Lines) yield return l;
    }

    [Fact]
    public void TheSettlementView_CoversEveryTopicTheDumpsWereTheOnlyHomeOf()
    {
        UiSession s = Played(3);
        int id = s.World.Settlements[0].Id.Value;
        PlayerView v = PlayerViews.Settlement(s.World, s.Config, UiPlayer.Empire, Record(s, id), id, s.Names.Name, 3);
        Assert.Equal(
            new[] { "People", "Food", "Housing", "Happiness", "Migration", "Work", "Structures", "Institutions", "Units" },
            Headings(v));
        foreach (ViewBlock b in v.Blocks) Assert.NotEmpty(b.Lines);
        Assert.StartsWith(s.Names.Name(id), v.Title);
        // Plain language: none of the record vocabulary leaks into the player surface.
        foreach (string line in AllLines(v))
        {
            Assert.DoesNotContain("GAP", line);
            Assert.DoesNotContain("RESIDUAL", line);
            Assert.DoesNotContain("not recorded", line);
        }
        // Happiness names its causes: food, shelter and the tax burden (or its absence).
        IReadOnlyList<string> happy = v.Blocks[3].Lines;
        Assert.Contains(happy, l => l.StartsWith("Food: ", StringComparison.Ordinal));
        Assert.Contains(happy, l => l.StartsWith("Shelter: ", StringComparison.Ordinal));
        Assert.Contains(happy, l => l.Contains("tax", StringComparison.Ordinal));
        // The player's own settlement lists its labour activities (from LabourActivities).
        Assert.DoesNotContain("You do not direct the labour here.", v.Blocks[5].Lines);
    }

    [Fact]
    public void BeforeTheFirstTurn_TheViewStillReadsTheLiveStocks_AndSaysTheFlowsComeLater()
    {
        UiSession s = Played(0);
        int id = s.World.Settlements[0].Id.Value;
        PlayerView v = PlayerViews.Settlement(s.World, s.Config, UiPlayer.Empire, null, id, s.Names.Name, 2);
        Assert.Contains(v.Blocks[0].Lines, l => l.StartsWith("People live here (", StringComparison.Ordinal));
        Assert.Contains("The trend shows after the first turn.", v.Blocks[0].Lines);
        Assert.Contains("Movement shows after the first turn.", v.Blocks[4].Lines);
    }

    [Fact]
    public void DensityFollowsTheEra_WordsAlwaysTheSame_FiguresAndBreakdownAddedWithTheLevel()
    {
        UiSession s = Played(3);
        int id = s.World.Settlements[0].Id.Value;
        SettlementRecord? r = Record(s, id);
        PlayerView sparse = PlayerViews.Settlement(s.World, s.Config, UiPlayer.Empire, r, id, s.Names.Name, 1);
        PlayerView dense = PlayerViews.Settlement(s.World, s.Config, UiPlayer.Empire, r, id, s.Names.Name, 5);
        // Level 1 speaks in words: the population line carries no figure.
        Assert.Equal("People live here", sparse.Blocks[0].Lines[0]);
        Assert.StartsWith("People live here (", dense.Blocks[0].Lines[0], StringComparison.Ordinal);
        // The dense view says at least as much in every block, and strictly more overall.
        int sparseCount = 0, denseCount = 0;
        for (int b = 0; b < sparse.Blocks.Count; b++)
        {
            Assert.True(dense.Blocks[b].Lines.Count >= sparse.Blocks[b].Lines.Count, sparse.Blocks[b].Heading);
            sparseCount += sparse.Blocks[b].Lines.Count;
            denseCount += dense.Blocks[b].Lines.Count;
        }
        Assert.True(denseCount > sparseCount);
        // The words never change with the level: every sparse line is the prefix of a dense line.
        for (int b = 0; b < sparse.Blocks.Count; b++)
            foreach (string line in sparse.Blocks[b].Lines)
                Assert.Contains(dense.Blocks[b].Lines, d => d.StartsWith(line, StringComparison.Ordinal));
    }

    [Fact]
    public void TheEmpireView_SpeaksOfPeopleGrainTradeRuleRoadsAndArms()
    {
        UiSession s = Played(3);
        PlayerView v = PlayerViews.Empire(s.World, s.Config, UiPlayer.Empire, s.Names.Name, 3);
        Assert.Equal(new[] { "Realm", "Grain", "Trade", "Rule", "Roads", "Arms" }, Headings(v));
        foreach (ViewBlock b in v.Blocks) Assert.NotEmpty(b.Lines);
        // Taxation is research-gated: at turn 3 the founding Empire cannot levy, and the view says so in words.
        Assert.False(Governance.CanLevyTax(s.World, s.Config, UiPlayer.Empire));
        Assert.Contains("Your people do not yet know how to levy a tax.", v.Blocks[3].Lines);
        // Legitimacy is the public reader's reading.
        double legit = Governance.Legitimacy(s.World, UiPlayer.Empire, s.Config);
        Assert.Contains(v.Blocks[3].Lines, l => l.Contains(PlayerViews.LegitimacyWord(legit), StringComparison.Ordinal));
    }

    [Fact]
    public void Trade_IsListedLargestFirst_TiesBrokenByTableOrder()
    {
        // Tie-dense: four flows of the SAME quantity and one larger — the larger leads, the equal ones keep
        // table order (the composite key (quantity desc, index asc)).
        UiSession s = Played(1);
        WorldState w = s.World.Clone();
        w.TradeFlows.Clear();
        SettlementId a = w.Settlements[0].Id, b = w.Settlements[1].Id, c = w.Settlements[2].Id;
        w.TradeFlows.Add(new TradeFlowRow(a, b, UiGoods.Grain, 10));
        w.TradeFlows.Add(new TradeFlowRow(b, a, UiGoods.Grain, 10));
        w.TradeFlows.Add(new TradeFlowRow(a, c, UiGoods.Grain, 10));
        w.TradeFlows.Add(new TradeFlowRow(c, a, UiGoods.Grain, 25));
        w.TradeFlows.Add(new TradeFlowRow(b, c, UiGoods.Grain, 10));
        Func<int, string> n = s.Names.Name;
        PlayerView v = PlayerViews.Empire(w, s.Config, UiPlayer.Empire, n, 3);
        IReadOnlyList<string> trade = v.Blocks[2].Lines;
        string Line(SettlementId f, SettlementId t, long q) => $"Grain went from {n(f.Value)} to {n(t.Value)} ({q})";
        Assert.Equal(Line(c, a, 25), trade[0]);
        Assert.Equal(Line(a, b, 10), trade[1]);
        Assert.Equal(Line(b, a, 10), trade[2]);
        Assert.Equal(Line(a, c, 10), trade[3]);
        Assert.Equal(Line(b, c, 10), trade[4]);
    }

    [Fact]
    public void TheInstitutionsPanel_SaysNoneFounded_ThenShowsTypeSiteStageStaffAndEffect()
    {
        UiSession s = Played(1);
        PlayerView empty = PlayerViews.Institutions(s.World, s.Config, UiPlayer.Empire, s.Names.Name, 3);
        Assert.Equal(new[] { "Founded", "Specialties" }, Headings(empty));
        Assert.StartsWith("Your empire has founded no institution yet.", empty.Blocks[0].Lines[0], StringComparison.Ordinal);

        WorldState w = s.World.Clone();
        SettlementId site = w.Settlements[0].Id;
        int health = InstitutionContent.HealthTypeKey(s.Config);
        int other = -1;
        foreach (Sim.Core.Systems.Research.UniversityType t in s.Config.Research!.UniversityTypes)
            if (t.Key != health) { other = t.Key; break; }
        Assert.True(other >= 0);
        w.Institutions.Add(new InstitutionRow(1, UiPlayer.Empire, site, other, w.Clock.Turn, 0.95));
        w.Institutions.Add(new InstitutionRow(2, UiPlayer.Empire, site, health, w.Clock.Turn, 0.30));

        PlayerView v = PlayerViews.Institutions(w, s.Config, UiPlayer.Empire, s.Names.Name, 3);
        InstitutionInstanceView[] views = InstitutionsQuery.Instances(w, s.Config, UiPlayer.Empire);
        Assert.Equal(2, views.Length);
        IReadOnlyList<string> lines = v.Blocks[0].Lines;
        foreach (InstitutionInstanceView iv in views)
        {
            string stage = iv.Stage == InstitutionLifecycle.Mature ? "mature" : "growing";
            Assert.Contains(lines, l => l.StartsWith($"{iv.TypeName} at {s.Names.Name(site.Value)} - {stage}", StringComparison.Ordinal));
            Assert.Contains("  " + PlayerViews.EffectNow(w, s.Config, iv, 3), lines);
        }
        Assert.Contains(lines, l => l.StartsWith("  employs about ", StringComparison.Ordinal));
        // The healing institution's effect is the query's mortality reading.
        InstitutionInstanceView healer = Array.Find(views, x => x.Heals)!;
        MedicalCoverageView cover = InstitutionsQuery.Health(w, s.Config, site);
        Assert.Equal(cover.MortalityMultiplier < 1.0, PlayerViews.EffectNow(w, s.Config, healer, 3).StartsWith("Fewer people die here", StringComparison.Ordinal));
        // The settlement view lists the same institutions in its Institutions block.
        PlayerView place = PlayerViews.Settlement(w, s.Config, UiPlayer.Empire, null, site.Value, s.Names.Name, 3);
        Assert.Contains(place.Blocks[7].Lines, l => l.StartsWith(healer.TypeName + " - ", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildingThePlayerViewsRepeatedly_MutatesNoSimulationState()
    {
        UiSession s = Played(3);
        string before = WorldHash.ComputeHex(s.World);
        int orders = s.Orders.Count;
        for (int frame = 0; frame < 3; frame++)
        {
            for (int i = 0; i < s.World.Settlements.Count; i++)
            {
                int id = s.World.Settlements[i].Id.Value;
                _ = PlayerViews.Settlement(s.World, s.Config, UiPlayer.Empire, Record(s, id), id, s.Names.Name, 5);
            }
            _ = PlayerViews.Empire(s.World, s.Config, UiPlayer.Empire, s.Names.Name, 5);
            _ = PlayerViews.Institutions(s.World, s.Config, UiPlayer.Empire, s.Names.Name, 5);
        }
        Assert.Equal(before, WorldHash.ComputeHex(s.World));
        Assert.Equal(orders, s.Orders.Count);
    }

    // --- the developer toggle ---------------------------------------------------------------

    [Fact]
    public void ThePlayerBar_ShowsOnlyPlayerSections_TheDeveloperBarAddsDEV()
    {
        foreach (Section section in GameSections.Roster(developer: false))
            Assert.False(GameSections.IsDeveloper(section), section.ToString());
        Assert.Equal(GameSections.PlayerOrder.ToArray(), GameSections.Roster(false).ToArray());
        Assert.Equal(GameSections.PlayerOrder.Concat(new[] { Section.Developer }).ToArray(), GameSections.Roster(true).ToArray());
        // Every audit/debug surface is a DEV tab, intact.
        Assert.Equal(new[] { Section.Turn, Section.Settlement, Section.Economy, Section.More }, GameSections.DeveloperTabs.ToArray());
        foreach (Section tab in GameSections.DeveloperTabs) Assert.True(GameSections.IsDeveloper(tab));
    }

    [Fact]
    public void Routes_LandOnThePlayerHomeOfTheFigure_OrOnTheDEVTabWhenTheToggleIsOn()
    {
        Assert.Equal((Section.Empire, Section.More), GameSections.Resolve(Section.Turn, developer: false, Section.More));
        Assert.Equal((Section.Place, Section.More), GameSections.Resolve(Section.Settlement, developer: false, Section.More));
        Assert.Equal((Section.Developer, Section.Turn), GameSections.Resolve(Section.Turn, developer: true, Section.More));
        Assert.Equal((Section.Developer, Section.Settlement), GameSections.Resolve(Section.Settlement, developer: true, Section.More));
        Assert.Equal((Section.Policy, Section.More), GameSections.Resolve(Section.Policy, developer: false, Section.More));
        // Every shipped explain route resolves to a section the player bar has.
        foreach (ExplainFigure f in Enum.GetValues<ExplainFigure>())
            Assert.Contains(GameSections.Resolve(ExplainRouting.For(f).Section, false, Section.Turn).Open, GameSections.Roster(false));
    }

    [Fact]
    public void TurningTheToggleOff_ClosesADeveloperSurface_AndNothingElse()
    {
        Assert.Equal(Section.None, GameSections.OnDeveloperToggle(Section.Developer, developerNow: false));
        Assert.Equal(Section.None, GameSections.OnDeveloperToggle(Section.Turn, developerNow: false));
        Assert.Equal(Section.Place, GameSections.OnDeveloperToggle(Section.Place, developerNow: false));
        Assert.Equal(Section.Developer, GameSections.OnDeveloperToggle(Section.Developer, developerNow: true));
    }

    [Fact]
    public void TheDevFlag_IsOffByDefault_AndDoesNotChangeTheWorldsIdentity()
    {
        Assert.False(UiArgs.Developer([]));
        Assert.False(UiArgs.Developer(["--seed", "7"]));
        Assert.True(UiArgs.Developer(["--seed", "7", "--dev"]));
        Assert.Equal(UiArgs.Parse(["--seed", "7"]), UiArgs.Parse(["--seed", "7", "--dev"]));
        Assert.Contains("--dev", UiArgs.Usage);
    }
}
