using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// TEST-ONLY FIXTURE — the playability gate's "research done: now usable" state (the canonical seed-42 world; the cheapest
/// node available at turn 1 whose completion alone fires a 'now usable' shortcut — derived from content, no node id —
/// targeted and played until the shortcut fires), and the founded turn-0 world with its production config.
/// </summary>
public sealed class NowUsableFixture
{
    public UiSession Session { get; }
    public WorldState Founded { get; }
    public SimConfig Config { get; }
    public ResearchNodeId Node { get; }

    public NowUsableFixture()
    {
        Founded = UiFounding.Found(42);
        Config = UiFounding.ProductionConfig();
        Node = PlayabilityGate.NowUsableNode(Founded, Config, DiscoveryShortcuts.Content)
            ?? throw new InvalidOperationException("no node available at turn 1 fires a shortcut");
        Session = PlayabilityGate.States(null).First(s => s.Name == PlayabilityGate.NowUsableStateName).Build();
    }
}

/// <summary>
/// M5 map workstream P10 — THE RESOURCE 'NOW USABLE' SHORTCUT (the Director's default for workstream part 6), pinned on
/// values:
/// <list type="bullet">
/// <item>TRANSITION-ONLY: a shortcut fires on the step that makes a content-declared entity knowledge-eligible for the
///   player AND whose effect the simulation reads — never on a step without new knowledge, never again once eligible,
///   never for a completion that makes nothing usable, never for a label-only identity, never for farming while the yield
///   does not read it; a recipe names only the deposit goods no known recipe consumed before;</item>
/// <item>TURN-EXACT EXPIRY at T + N (N the UI content's TUNE value), in the history and in the game; the layer stays;</item>
/// <item>RESUME rebuilds the history identically; NO HASH moves;</item>
/// <item>the OUTLINE is exactly the boundary of the known places above 0 (never an unknown one); the lens draws it only on
///   a shown layer;</item>
/// <item>the text says 'now usable', never 'discovered', and keeps permission to use apart from knowledge of the resource;
///   no node or entity id is named in the code; the gate rows pass in a research-done state.</item>
/// </list>
/// </summary>
public class DiscoveryShortcutTests(NowUsableFixture fx) : IClassFixture<NowUsableFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private static string WorkDir(string name) => Path.Combine(Path.GetTempPath(),
        "sim-ui-p10-" + name + "-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    private ResearchContent Research => fx.Config.Research!;

    /// <summary>A clone of <paramref name="w"/> with the player's completed rows for <paramref name="nodes"/> added.</summary>
    private static WorldState With(WorldState w, ResearchContent c, IEnumerable<int> nodes)
    {
        WorldState n = w.Clone();
        foreach (int i in nodes)
            if (!ResearchQuery.IsCompleted(n, Me, c.Nodes[i].Key)) n.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[i].Key));
        return n;
    }

    private int EntityOf(string id)
    {
        int e = Research.EntityIndexOf(id);
        Assert.True(e >= 0, id + " is not a research entity");
        return e;
    }

    /// <summary>The founded world with every node in <paramref name="entity"/>'s requirement completed.</summary>
    private WorldState Knowing(string entity, WorldState? from = null) => With(from ?? fx.Founded, Research, Research.Entities[EntityOf(entity)].NodeAtoms);

    private static string Hash(WorldState w) => WorldHash.ComputeHex(w);

    // ================================================================== the gate state: one shortcut, on its step

    [Fact]
    public void TheResearchDoneState_FiresExactlyOneShortcut_OnTheStepItsNodeCompletes_StampedWithThatStepsClock()
    {
        UiSession s = fx.Session;
        Assert.Single(s.NowUsableHistory.Entries);
        NowUsable e = s.NowUsableHistory.Entries[0];
        WorldState prev = s.PreviousWorld!;
        Assert.Equal(s.World.Clock.Turn, e.Turn);                 // the clock of the state the completing step wrote
        Assert.Equal(prev.Clock.Turn + 1, e.Turn);
        Assert.Contains(fx.Node, ResearchQuery.CompletedBetween(prev, s.World, Me));
        Assert.False(ResearchQuery.IsKnowledgeEligible(Research, e.Entity, ResearchQuery.CompletedMask(prev, Research, Me)));
        Assert.True(ResearchQuery.IsKnowledgeEligible(Research, e.Entity, ResearchQuery.CompletedMask(s.World, Research, Me)));
        IReadOnlyList<NowUsable> again = DiscoveryShortcuts.Transitions(prev, s.World, s.Config, s.NowUsableHistory.Content, Me);
        Assert.Equal(e.Canonical(), Assert.Single(again).Canonical());
        // The farming sector's researched identity, on the layer the UI content maps it to, its yield predicate flipped.
        Assert.Equal(NowUsableKind.Activity, e.Kind);
        SectorLayer map = Assert.Single(DiscoveryShortcuts.Content.SectorLayers);
        Assert.Equal([map.Layer], e.Layers.ToArray());
        Assert.Contains(Research.SectorActivities[Sectors.Farming].Researched, r => r.Entity == e.Entity);
        Assert.True(DiscoveryShortcuts.YieldReadsKnowledge(prev, s.World, s.Config, Me, Sectors.Farming));
        Assert.Empty(e.Goods);
    }

    // ================================================================== TRANSITION-ONLY

    [Fact]
    public void NoNewKnowledge_FiresNothing_AndAnEntityAlreadyEligibleNeverFiresAgain()
    {
        UiSession s = fx.Session;
        Assert.Empty(DiscoveryShortcuts.Transitions(s.World, s.World, s.Config, DiscoveryShortcuts.Content, Me));
        // A later completion of a node no shortcut source names: knowledge changed, nothing transitioned.
        var sourceNodes = new HashSet<int>();
        foreach (int e in DiscoveryShortcuts.SourceEntities(s.Config, DiscoveryShortcuts.Content))
            foreach (int n in Research.Entities[e].NodeAtoms) sourceNodes.Add(n);
        int other = -1;
        for (int i = 0; i < Research.Nodes.Count && other < 0; i++)
            if (!sourceNodes.Contains(i) && !ResearchQuery.IsCompleted(s.World, Me, Research.Nodes[i].Key)) other = i;
        WorldState later = With(s.World, Research, [other]);
        Assert.NotEmpty(ResearchQuery.CompletedBetween(s.World, later, Me));
        Assert.Empty(DiscoveryShortcuts.Transitions(s.World, later, s.Config, DiscoveryShortcuts.Content, Me));
        // Completing the fired node's requirement AGAIN on top (a second node of the same OR): eligible before, so nothing.
        NowUsable fired = s.NowUsableHistory.Entries[0];
        var more = new List<int>();
        foreach (int n in Research.Entities[fired.Entity].NodeAtoms) if (!ResearchQuery.IsCompleted(s.World, Me, Research.Nodes[n].Key)) more.Add(n);
        Assert.NotEmpty(more);
        Assert.Empty(DiscoveryShortcuts.Transitions(s.World, With(s.World, Research, more), s.Config, DiscoveryShortcuts.Content, Me));
    }

    [Fact]
    public void ASessionStartedWithTheEntityAlreadyEligible_NeverFiresIt_StateIsNotATransition()
    {
        UiSession s = UiSession.StartFrom(With(fx.Founded, Research, [Research.IndexOf(fx.Node)]), 42);
        s.EndTurn();
        s.EndTurn();
        Assert.Empty(s.NowUsableHistory.Entries);
    }

    [Fact]
    public void ASettlementJoiningAPlayerThatAlreadyKnowsIt_FiresNothing_ThePlayersKnowledgeDidNotTransition()
    {
        // A step in which the player, already farming and already knowing bronze casting, learns something else and gains a
        // settlement (a colony founded, a place taken): that settlement's own predicates flip from wild food / unknown
        // recipe to the player's, but the PLAYER's eligibility did not transition, so nothing is newly usable.
        UiSession s = fx.Session;
        WorldState knows = Knowing("recipe.bronze_casting", s.World);
        WorldState next = knows.Clone();
        int newId = 0;
        for (int i = 0; i < next.Settlements.Count; i++) newId = Math.Max(newId, next.Settlements[i].Id.Value + 1);
        next.Settlements.Add(next.Settlements[0] with { Id = new SettlementId(newId) });
        next.Controls.Add(new ControlRow(Me, new SettlementId(newId), 1.0));
        ResearchNodeId cheapest = ResearchQuery.CheapestAvailable(knows, Research, Me, ResearchTree.Technology)!.Value;
        next.ResearchCompleted.Add(new ResearchCompletedRow(Me, cheapest));
        Assert.NotEmpty(ResearchQuery.CompletedBetween(knows, next, Me));
        Assert.True(LabourActivities.HarvestsWildFood(knows, s.Config, new SettlementId(newId)));    // the predicates DO flip
        Assert.False(LabourActivities.HarvestsWildFood(next, s.Config, new SettlementId(newId)));    // for the new settlement
        Assert.Empty(DiscoveryShortcuts.Transitions(knows, next, s.Config, DiscoveryShortcuts.Content, Me));
    }

    [Fact]
    public void APlayerTransitionTheEconomyAlreadyHad_FiresNothing_EveryHeldSettlementPractisedItAsItsOwnTradition()
    {
        // Every settlement the player holds already holds the knowledge itself (SettlementKnowledge's own holder: a craft
        // tradition that survived annexation), so the settlement predicates the economy reads — CraftingQuery.IsKnown over
        // CraftingQuery.KnowledgeOf, LabourActivities.HarvestsWildFood — were already true: the player's eligibility
        // transitions, the economy changes nowhere, and nothing fires. Control: without the traditions, both fire.
        WorldState traditions = fx.Founded.Clone();
        var nodes = new List<int>(Research.Entities[EntityOf("recipe.bronze_casting")].NodeAtoms);
        nodes.Add(Research.IndexOf(fx.Node));
        foreach (SettlementId s in LabourActivities.ControlledSettlements(fx.Founded, Me))
            foreach (int n in nodes)
                traditions.ResearchCompleted.Add(new ResearchCompletedRow(SettlementKnowledge.LocalHolder(s), Research.Nodes[n].Key));
        IReadOnlyList<NowUsable> none = DiscoveryShortcuts.Transitions(traditions, With(traditions, Research, nodes), fx.Config, DiscoveryShortcuts.Content, Me);
        Assert.Empty(none);
        IReadOnlyList<NowUsable> both = DiscoveryShortcuts.Transitions(fx.Founded, With(fx.Founded, Research, nodes), fx.Config, DiscoveryShortcuts.Content, Me);
        Assert.Equal(2, both.Count);
    }

    [Fact]
    public void ACompletionThatMakesNothingUsable_FiresNothing_TheShortcutsOwnNodeDoes()
    {
        ResearchNodeId cheapest = ResearchQuery.CheapestAvailable(fx.Founded, Research, Me, ResearchTree.Technology)!.Value;
        Assert.NotEqual(fx.Node, cheapest);
        WorldState learned = With(fx.Founded, Research, [Research.IndexOf(cheapest)]);
        Assert.NotEmpty(ResearchQuery.CompletedBetween(fx.Founded, learned, Me));
        Assert.Empty(DiscoveryShortcuts.Transitions(fx.Founded, learned, fx.Config, DiscoveryShortcuts.Content, Me));
        // Control: the rig's node, on the same world, fires.
        Assert.Single(DiscoveryShortcuts.Transitions(fx.Founded, With(fx.Founded, Research, [Research.IndexOf(fx.Node)]), fx.Config,
            DiscoveryShortcuts.Content, Me));
    }

    [Fact]
    public void ALabelOnlyIdentity_NeverFires_EvenWhenTheUiContentMapsItsSector()
    {
        int checkedIdentities = 0;
        foreach (SectorActivityContent sector in Research.SectorActivities)
        {
            if (sector.Sector == Sectors.Farming) continue;
            var ui = new DiscoveryShortcutContent(5, [new SectorLayer(sector.SectorId, "elevation")]);
            foreach (SectorResearchedIdentity id in sector.Researched)
            {
                WorldState next = With(fx.Founded, Research, Research.Entities[id.Entity].NodeAtoms);
                Assert.False(ResearchQuery.IsKnowledgeEligible(Research, id.Entity, ResearchQuery.CompletedMask(fx.Founded, Research, Me)));
                Assert.True(ResearchQuery.IsKnowledgeEligible(Research, id.Entity, ResearchQuery.CompletedMask(next, Research, Me)));
                foreach (NowUsable e in DiscoveryShortcuts.Transitions(fx.Founded, next, fx.Config, ui, Me))
                    Assert.NotEqual(id.Entity, e.Entity);
                checkedIdentities++;
            }
        }
        Assert.True(checkedIdentities >= 3, "herding, logging and mining are label-only identities in the shipped content");
    }

    [Fact]
    public void TheFarmingShortcut_FiresOnlyWhileTheYieldReadsTheKnowledge()
    {
        WorldState next = With(fx.Founded, Research, [Research.IndexOf(fx.Node)]);
        Assert.Single(DiscoveryShortcuts.Transitions(fx.Founded, next, fx.Config, DiscoveryShortcuts.Content, Me));
        PreCultivationConfig pre = fx.Config.Farming.PreCultivation!;
        SimConfig off = fx.Config with { Farming = fx.Config.Farming with { PreCultivation = pre with { Enabled = false } } };
        Assert.Empty(DiscoveryShortcuts.Transitions(fx.Founded, next, off, DiscoveryShortcuts.Content, Me));
    }

    [Fact]
    public void ARecipeShortcut_NamesOnlyTheDepositGoodsNoKnownRecipeConsumedBefore_OnTheirChannels()
    {
        GoodsConfig goods = fx.Config.Goods!;
        NowUsable bronze = Assert.Single(DiscoveryShortcuts.Transitions(fx.Founded, Knowing("recipe.bronze_casting"), fx.Config, DiscoveryShortcuts.Content, Me));
        Assert.Equal(NowUsableKind.Recipe, bronze.Kind);
        Assert.Equal(["copper-ore", "tin-ore"], bronze.GoodNames.ToArray());   // timber: toolmaking (baseline) consumed it already
        Assert.Equal(["elevation"], bronze.Layers.ToArray());
        Assert.Contains("artisan_share > 0.05", bronze.Permission, StringComparison.Ordinal);   // the recipe's own D-020 gate
        Assert.NotNull(bronze.Extraction);
        NowUsable pottery = Assert.Single(DiscoveryShortcuts.Transitions(fx.Founded, Knowing("recipe.pottery_firing"), fx.Config, DiscoveryShortcuts.Content, Me));
        Assert.Equal(["clay"], pottery.GoodNames.ToArray());
        Assert.Equal(["water"], pottery.Layers.ToArray());
        // Both in one step: two shortcuts, in research entity order.
        IReadOnlyList<NowUsable> both = DiscoveryShortcuts.Transitions(fx.Founded, Knowing("recipe.pottery_firing", Knowing("recipe.bronze_casting")),
            fx.Config, DiscoveryShortcuts.Content, Me);
        Assert.Equal(2, both.Count);
        Assert.True(both[0].Entity < both[1].Entity);
        // A recipe ALL of whose deposit inputs a recipe known before already consumed fires nothing: pottery made to consume
        // the ores, and known first; bronze casting then makes nothing newly usable.
        var recipes = new List<RecipeEntry>();
        foreach (RecipeEntry r in goods.Recipes)
            recipes.Add(r.Entity == "recipe.pottery_firing" ? r with { Inputs = [new RecipeInput("copper-ore", 1.0), new RecipeInput("tin-ore", 1.0)] } : r);
        SimConfig variant = fx.Config with { Goods = goods with { Recipes = [.. recipes] } };
        WorldState potters = Knowing("recipe.pottery_firing");
        Assert.Empty(DiscoveryShortcuts.Transitions(potters, Knowing("recipe.bronze_casting", potters), variant, DiscoveryShortcuts.Content, Me));
        Assert.Single(DiscoveryShortcuts.Transitions(potters, Knowing("recipe.bronze_casting", potters), fx.Config, DiscoveryShortcuts.Content, Me));
    }

    // ================================================================== the words

    [Fact]
    public void TheText_SaysNowUsable_NeverDiscovered_AndKeepsPermissionToUseApartFromKnowledgeOfTheResource()
    {
        var all = new List<NowUsable>(fx.Session.NowUsableHistory.Entries);
        all.AddRange(DiscoveryShortcuts.Transitions(fx.Founded, Knowing("recipe.pottery_firing", Knowing("recipe.bronze_casting")),
            fx.Config, DiscoveryShortcuts.Content, Me));
        Assert.Equal(3, all.Count);
        foreach (NowUsable e in all)
        {
            Assert.Contains(DiscoveryShortcuts.Label, e.Headline, StringComparison.Ordinal);
            Assert.StartsWith("Permission to use:", e.Permission, StringComparison.Ordinal);
            Assert.Equal(DiscoveryShortcuts.KnowledgeStatement, e.Knowledge);
            Assert.Equal(e.Kind == NowUsableKind.Recipe, e.Extraction is not null);
            foreach (string t in new[] { e.Headline, e.Permission, e.Extraction ?? "", e.Knowledge })
                Assert.DoesNotContain("discover", t, StringComparison.OrdinalIgnoreCase);
        }
        foreach (string t in new[] { GameUi.NowUsableTitle, GameUi.NowUsablePermissionShort, GameUi.NowUsableKnowledgeShort, DiscoveryShortcuts.KnowledgeStatement })
            Assert.DoesNotContain("discover", t, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Knowledge of the resource: not simulated", DiscoveryShortcuts.KnowledgeStatement, StringComparison.Ordinal);
    }

    /// <summary>
    /// Review finding (P10, major): ProductionSystem.Farm harvests min(land, labour) — arable km² × the per-km² yield
    /// against farmers × the per-farmer output × tools. Cultivation lifts the LAND limit 2 → 26 (x13) on the shipped content
    /// but the LABOUR limit only 4.3 → 5 (about x1.16), and a labour-bound farm's harvest follows the labour arm. So the
    /// farming shortcut names BOTH arms, read from the same config fields the yield reads, and says the harvest is the
    /// lower of the two; it never shows a multiplier — least of all the bare land ratio — as the food change.
    /// </summary>
    [Fact]
    public void TheFarmingShortcut_NamesBothLeontiefArms_TheLabourArmToo_AndNeverShowsTheBareLandRatioAsTheFoodChange()
    {
        static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static void AssertNamesBothArms(NowUsable e, FarmingConfig f)
        {
            PreCultivationConfig pre = f.PreCultivation!;
            Assert.Equal(NowUsableKind.Activity, e.Kind);
            Assert.Contains("the lower of two limits", e.Permission, StringComparison.Ordinal);
            Assert.Contains("land " + N(pre.YieldPerArableKm2PerYear) + " -> " + N(f.YieldPerArableKm2PerYear) + " food per arable km2 a year",
                e.Permission, StringComparison.Ordinal);
            Assert.Contains("labour " + N(pre.OutputPerGathererPerYear) + " -> " + N(f.OutputPerFarmerPerYear) + " food per farmer a year",
                e.Permission, StringComparison.Ordinal);
            string landRatio = N(f.YieldPerArableKm2PerYear / pre.YieldPerArableKm2PerYear);
            foreach (string t in new[] { e.Headline, e.Permission, e.Knowledge })
            {
                Assert.DoesNotMatch(@"(^|[^A-Za-z0-9])x ?\d", t);                                      // no multiplier of any kind
                Assert.DoesNotMatch(@"(^|[^0-9.])" + Regex.Escape(landRatio) + @"($|[^0-9.])", t);    // nor the land ratio bare
                Assert.DoesNotContain("times", t, StringComparison.OrdinalIgnoreCase);
            }
        }

        // The shipped content, as the gate state fired it: land 2 -> 26, labour 4.3 -> 5.
        NowUsable shipped = fx.Session.NowUsableHistory.Entries[0];
        AssertNamesBothArms(shipped, fx.Config.Farming);
        Assert.Contains("land 2 -> 26 ", shipped.Permission, StringComparison.Ordinal);
        Assert.Contains("labour 4.3 -> 5 ", shipped.Permission, StringComparison.Ordinal);

        // Every figure is the config's, none written into the text: a variant whose four coefficients all differ (land
        // ratio 21, labour ratio 1.6) fires with exactly its own arms.
        PreCultivationConfig shippedPre = fx.Config.Farming.PreCultivation!;
        SimConfig variant = fx.Config with
        {
            Farming = fx.Config.Farming with
            {
                YieldPerArableKm2PerYear = 31.5, OutputPerFarmerPerYear = 6.4,
                PreCultivation = shippedPre with { YieldPerArableKm2PerYear = 1.5, OutputPerGathererPerYear = 4 },
            },
        };
        WorldState next = With(fx.Founded, Research, [Research.IndexOf(fx.Node)]);
        NowUsable varied = Assert.Single(DiscoveryShortcuts.Transitions(fx.Founded, next, variant, DiscoveryShortcuts.Content, Me));
        AssertNamesBothArms(varied, variant.Farming);
        Assert.Contains("land 1.5 -> 31.5 ", varied.Permission, StringComparison.Ordinal);
        Assert.Contains("labour 4 -> 6.4 ", varied.Permission, StringComparison.Ordinal);
    }

    [Fact]
    public void NoResearchNodeOrEntityId_IsNamedInTheShortcutsCode()
    {
        string root = RepoRoot();
        string[] files = ["Sim.Ui/ViewModel/DiscoveryShortcuts.cs", "Sim.Ui/GameUi.NowUsable.cs", "Sim.Ui/Headless/PlayabilityGate.NowUsable.cs"];
        var ids = new List<string>();
        foreach (ResearchNode n in Research.Nodes) ids.Add(n.Id);
        foreach (ResearchEntity e in Research.Entities) ids.Add(e.Id);
        foreach (string f in files)
        {
            string src = File.ReadAllText(Path.Combine(root, f));
            var literals = new List<string>();
            foreach (Match m in Regex.Matches(src, "\"([^\"\\\\]|\\\\.)*\"")) literals.Add(m.Value.Trim('"'));
            Assert.NotEmpty(literals);
            foreach (string lit in literals)
                foreach (string id in ids)
                    Assert.False(Regex.IsMatch(lit, @"(?<![\w.])" + Regex.Escape(id) + @"(?![\w.])"),
                        f + " names the research id '" + id + "' in the literal \"" + lit + "\"");
        }
    }

    // ================================================================== TURN-EXACT EXPIRY

    [Fact]
    public void TheOffer_StartsOnItsStampedTurn_AndExpiresTurnExactly_AtTPlusN()
    {
        NowUsable e = fx.Session.NowUsableHistory.Entries[0];
        int n = DiscoveryShortcuts.Content.ExpiryTurns;
        Assert.True(n >= 1);
        long t = e.Turn;
        Assert.Equal(t + n, e.ExpiresAt);
        NowUsableHistory h = fx.Session.NowUsableHistory;
        Assert.Empty(h.Active(t - 1));
        Assert.Equal(e, Assert.Single(h.Active(t)).Entry);
        Assert.Equal(e, Assert.Single(h.Active(t + n - 1)).Entry);
        Assert.Empty(h.Active(t + n));
        Assert.Empty(h.Active(t + n + 1));
    }

    [Fact]
    public void InTheGame_TheShortcutIsOfferedAndOutlinedThroughClockTurnTPlusNMinus1_AndGoneAtTPlusN_TheLayerStays()
    {
        NowUsable fired = fx.Session.NowUsableHistory.Entries[0];
        long t = fired.Turn;
        int n = DiscoveryShortcuts.Content.ExpiryTurns;
        UiSession s = Replayed(t);
        Assert.Equal(fired.Canonical(), Assert.Single(s.NowUsableHistory.Entries).Canonical());
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("expiry"), Assets());
        h.Idle(2);
        int layer = -1;
        foreach (OverlayLayer l in h.Ui.OverlayLayers) if (l.Key == fired.Layers[0]) layer = l.Index;
        Assert.True(layer >= 0);
        Assert.True(h.ClickControl(GameUi.NowUsableControl(0)), "the Show on map button is not drawn at turn T");
        h.Idle(1);
        Assert.True(h.Ui.OverlayShown(layer));
        for (int k = 0; k < n; k++)
        {
            Assert.Equal(t + k, s.World.Clock.Turn);
            Assert.NotNull(h.Ui.Controls.Find(GameUi.NowUsableControl(0)));
            Assert.Equal(0, h.Ui.NowUsableFocus);
            OverlayFrame f = h.Ui.LensFrame!.Overlay!;
            Assert.Equal([layer], f.OutlinedLayers.ToArray());
            Assert.True(f.OutlineSegments > 0);
            Assert.Equal(fired.Headline, f.Key!.Rows[0]);
            Assert.True(h.ClickControl("end-turn"));
            h.Idle(1);
        }
        Assert.Equal(t + n, s.World.Clock.Turn);
        Assert.Null(h.Ui.Controls.Find(GameUi.NowUsableControl(0)));
        Assert.Empty(h.Ui.NowUsableOffered);
        Assert.Equal(-1, h.Ui.NowUsableFocus);
        Assert.False(h.Ui.FrameText.Contains(GameUi.NowUsableTitle, FrameSurface.Overlay));
        OverlayFrame after = h.Ui.LensFrame!.Overlay!;
        Assert.Empty(after.OutlinedLayers);
        Assert.Equal(0, after.OutlineSegments);
        Assert.NotEqual(fired.Headline, after.Key!.Rows[0]);
        Assert.True(h.Ui.OverlayShown(layer), "the layer is the player's now: it stays shown (permanent manual access)");
        // ... and OVERLAYS still toggles it.
        if (!h.Ui.OverlayControlsOpen) Assert.True(h.ClickControl(GameUi.OverlaysControl));
        h.Idle(1);
        Assert.True(h.ClickControl(h.Ui.OverlayLayers[layer].Control));
        h.Idle(1);
        Assert.False(h.Ui.OverlayShown(layer));
        Assert.Single(s.NowUsableHistory.Entries);   // the record stays; only the offer expired
    }

    /// <summary>A fresh session replayed (UiSession.ReplayTo, Resume's loop) from the fixture's order log to clock turn
    /// <paramref name="turn"/>.</summary>
    private UiSession Replayed(long turn)
    {
        using var ms = new MemoryStream();
        fx.Session.Orders.Save(ms);
        ms.Position = 0;
        OrderLog saved = OrderLog.Load(ms);
        UiSession s = UiSession.Start(42);
        s.ReplayTo(saved, turn);
        return s;
    }

    // ================================================================== RESUME; NO HASH

    [Fact]
    public void Resume_RebuildsTheNowUsableHistoryIdentically()
    {
        UiSession live = fx.Session;
        string dir = WorkDir("resume");
        string log = Path.Combine(dir, "orders-20261011-120000-s1024-p10.bin");
        Directory.CreateDirectory(dir);
        live.ExportManifest("2026-10-11 12:00:00", log);
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));
        UiSession resumed = UiSession.Resume(UiArgs.ResolveManifest(dir), out _);
        Assert.Equal(live.World.Clock.Turn, resumed.World.Clock.Turn);
        Assert.Equal(Hash(live.World), Hash(resumed.World));
        Assert.NotEmpty(live.NowUsableHistory.CanonicalLines());
        Assert.Equal(live.NowUsableHistory.CanonicalLines().ToArray(), resumed.NowUsableHistory.CanonicalLines().ToArray());
        Assert.Equal(live.NowUsableHistory.Active(live.World.Clock.Turn).Count, resumed.NowUsableHistory.Active(resumed.World.Clock.Turn).Count);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void TheShortcut_ChangesNoHash_TheSessionsWorldIsTheBareExecutorsAndTheTriggerWritesNothing()
    {
        // The session (every UI observer, the P10 history among them) steps to the same world the bare production executor
        // steps to from the same order log.
        using var ms = new MemoryStream();
        fx.Session.Orders.Save(ms);
        ms.Position = 0;
        OrderLog orders = OrderLog.Load(ms);
        TurnExecutor executor = UiSession.BuildProductionExecutor(orders);
        WorldState w = UiFounding.Found(42);
        while (w.Clock.Turn < fx.Session.World.Clock.Turn) w = executor.Step(w);
        Assert.Equal(Hash(w), Hash(fx.Session.World));
        // The trigger reads only.
        WorldState prev = fx.Session.PreviousWorld!, next = fx.Session.World;
        string hp = Hash(prev), hn = Hash(next);
        _ = DiscoveryShortcuts.Transitions(prev, next, fx.Config, DiscoveryShortcuts.Content, Me);
        _ = DiscoveryShortcuts.Transitions(fx.Founded, Knowing("recipe.bronze_casting"), fx.Config, DiscoveryShortcuts.Content, Me);
        Assert.Equal(hp, Hash(prev));
        Assert.Equal(hn, Hash(next));
    }

    // ================================================================== THE OUTLINE

    /// <summary>The outline of every layer of <paramref name="b"/> is exactly the boundary of the places it qualifies —
    /// counted independently (known, drawn, above 0): the node count, every segment on a node boundary between a
    /// qualifying and a non-qualifying side, and the total length equal to the number of unit edges between them.</summary>
    private static void AssertOutlinesExact(ResourceOverlay b, PolityKnowledge k)
    {
        int l = b.LatticeSize, blk = b.Block;
        foreach (OverlayLayer layer in b.Layers)
        {
            var q = new bool[b.NodeCount];
            int count = 0;
            for (int n = 0; n < q.Length; n++)
            {
                double v = b.Value(layer.Index, n);
                q[n] = k.Known(n) && !double.IsNaN(v) && v > 0.0;
                if (q[n]) count++;
            }
            bool In(int x, int y) => x >= 0 && y >= 0 && x < l && y < l && q[y * l + x];
            long unitEdges = 0;
            for (int y = 0; y <= l; y++) for (int x = 0; x < l; x++) if (In(x, y - 1) != In(x, y)) unitEdges++;
            for (int x = 0; x <= l; x++) for (int y = 0; y < l; y++) if (In(x - 1, y) != In(x, y)) unitEdges++;
            OverlayOutline o = b.Outline(layer.Index);
            Assert.Equal(count, o.Nodes);
            long length = 0;
            int[] s = o.Segments;
            for (int i = 0; i < s.Length; i += 4)
            {
                Assert.True(s[i] % blk == 0 && s[i + 1] % blk == 0 && s[i + 2] % blk == 0 && s[i + 3] % blk == 0);
                int x0 = s[i] / blk, y0 = s[i + 1] / blk, x1 = s[i + 2] / blk, y1 = s[i + 3] / blk;
                if (y0 == y1)
                {
                    Assert.True(x1 > x0);
                    for (int x = x0; x < x1; x++) Assert.NotEqual(In(x, y0 - 1), In(x, y0));
                    length += x1 - x0;
                }
                else
                {
                    Assert.True(x0 == x1 && y1 > y0);
                    for (int y = y0; y < y1; y++) Assert.NotEqual(In(x0 - 1, y), In(x0, y));
                    length += y1 - y0;
                }
            }
            Assert.Equal(unitEdges, length);
            for (int n = 0; n < q.Length; n++) Assert.Equal(q[n], b.Qualifies(layer.Index, n));
        }
    }

    [Fact]
    public void TheOutline_IsExactlyTheBoundaryOfTheKnownPlacesAboveZero_AllKnownAndRestricted_NeverAnUnknownPlace()
    {
        UiSession s = fx.Session;
        AssertOutlinesExact(s.Overlay!, s.Knowledge.Core);   // the session's own (fogged, M6 EX) view
        // M6 EX (2026-10-22): the session's view is fogged now, so the all-known comparison bakes the all-known view itself.
        PolityKnowledge everything = PolityKnowledge.Of(s.World, Me, null);
        ResourceOverlay all = ResourceOverlay.Bake(s.Config, s.Sites!, everything);
        AssertOutlinesExact(all, everything);
        // A restricted view (the east half unknown): no unknown node is ever inside an outline, and the outlines shrink.
        int l = all.LatticeSize;
        var known = new bool[all.NodeCount];
        for (int n = 0; n < known.Length; n++) known[n] = n % l < l / 2;
        PlayerKnowledge half = KnowledgeStubs.Over(s.World, Me, known);
        ResourceOverlay restricted = ResourceOverlay.Bake(s.Config, s.Sites!, half.Core);
        AssertOutlinesExact(restricted, half.Core);
        foreach (OverlayLayer layer in restricted.Layers)
        {
            for (int n = 0; n < known.Length; n++) if (!known[n]) Assert.False(restricted.Qualifies(layer.Index, n));
            if (all.Outline(layer.Index).Nodes > 0) Assert.True(restricted.Outline(layer.Index).Nodes < all.Outline(layer.Index).Nodes);
        }
    }

    [Fact]
    public void TheLens_OutlinesOnlyAShownLayer_EverySegmentInView_AndTheKeyOpensWithTheShortcutsRows()
    {
        UiSession s = fx.Session;
        ResourceOverlay b = s.Overlay!;
        int li = -1;
        foreach (OverlayLayer l in b.Layers) if (l.Key == s.NowUsableHistory.Entries[0].Layers[0]) li = l.Index;
        WorldProjection p = WorldProjection.Build(s.World, s.Config, id => s.Names.Name(id), Me, knowledge: s.Knowledge);
        var hl = new OverlayHighlight([li], ["headline row", "second row"]);
        double scale = 0.5, side = p.WorldSize * scale;
        (DrawList D, LensFrame F) Paint(bool[] shown)
        {
            var d = new DrawList();
            LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.World, (x, y) => (x * scale, y * scale), scale,
                new RectD(0, 0, side, side), overlay: new OverlayPaint(b, shown, 0.5, Key: new OverlayKeyAnchor(12, side - 12, 400)) { Highlight = hl });
            return (d, f);
        }
        var on = new bool[b.Layers.Count];
        on[li] = true;
        (DrawList d1, LensFrame f1) = Paint(on);
        Assert.Equal([li], f1.Overlay!.OutlinedLayers.ToArray());
        Assert.Equal(b.Outline(li).SegmentCount, f1.Overlay.OutlineSegments);   // the whole map in view
        Assert.Equal(b.Outline(li).Nodes, f1.Overlay.OutlineNodes);
        Assert.Equal(["headline row", "second row"], f1.Overlay.Key!.Rows.Take(2).ToArray());
        int lines1 = d1.Commands.Count(c => c is LineCmd);
        // Another layer shown instead: no outline, no shortcut rows.
        var other = new bool[b.Layers.Count];
        other[li == 0 ? 1 : 0] = true;
        (DrawList d2, LensFrame f2) = Paint(other);
        Assert.Empty(f2.Overlay!.OutlinedLayers);
        Assert.Equal(0, f2.Overlay.OutlineSegments);
        Assert.DoesNotContain("headline row", f2.Overlay.Key!.Rows);
        Assert.Equal(lines1 - b.Outline(li).SegmentCount, d2.Commands.Count(c => c is LineCmd));
    }

    // ================================================================== the rig's pick; the gate rows

    [Fact]
    public void Cheapest_BreaksCostTiesByTheLowerIndex_WhateverOrderTheNodesComeIn()
    {
        ulong x = 20261011UL;
        int Next(int n) { x = x * 6364136223846793005UL + 1442695040888963407UL; return (int)((x >> 33) % (ulong)n); }   // a fixed LCG
        for (int trial = 0; trial < 200; trial++)
        {
            int count = 1 + Next(12);
            var nodes = new List<int>();
            while (nodes.Count < count) { int v = Next(40); if (!nodes.Contains(v)) nodes.Add(v); }
            var cost = new double[40];
            foreach (int v in nodes) cost[v] = Next(3) * 0.5;   // tie-dense: three distinct costs
            double min = nodes.Min(v => cost[v]);
            int want = nodes.Where(v => cost[v] == min).Min();
            Assert.Equal(want, PlayabilityGate.Cheapest(nodes, v => cost[v]));
            nodes.Reverse();
            Assert.Equal(want, PlayabilityGate.Cheapest(nodes, v => cost[v]));
        }
        Assert.Equal(-1, PlayabilityGate.Cheapest([], _ => 0.0));
    }

    [Fact]
    public void TheGateRows_PassInTheResearchDoneStates_OfferedAndShownWhereAShortcutFired_NothingWhereNoneDid()
    {
        using (UiFrameHarness h = UiFrameHarness.Start(fx.Session, WorkDir("gate"), Assets()))
        {
            h.Idle(2);
            IReadOnlyList<GateRow> rows = PlayabilityGate.NowUsableRows(h, PlayabilityGate.NowUsableStateName);
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.True(r.Result == GateResult.Pass, r.Control + ": " + r.Detail));
            Assert.Contains(rows, r => r.Control.Contains("offered", StringComparison.Ordinal));
            GateRow show = Assert.Single(rows, r => r.Control.Contains("Show on map", StringComparison.Ordinal));
            Assert.Contains(UiCoverage.ControlKey(GameUi.NowUsableControl(0)), show.Covers);
        }
        UiSession plain = PlayabilityGate.States(null).First(st => st.Name == "research done").Build();
        Assert.Empty(plain.NowUsableHistory.Entries);
        using UiFrameHarness h2 = UiFrameHarness.Start(plain, WorkDir("gate-plain"), Assets());
        h2.Idle(2);
        GateRow none = Assert.Single(PlayabilityGate.NowUsableRows(h2, "research done"));
        Assert.Equal(GateResult.Pass, none.Result);
        Assert.Contains("nothing offered", none.Control, StringComparison.Ordinal);
    }

    // ================================================================== the UI content

    [Fact]
    public void TheUiContent_IsValidated_AndItsSectorsAndLayersExist()
    {
        DiscoveryShortcutContent c = DiscoveryShortcuts.Content;
        Assert.True(c.ExpiryTurns >= 1);
        foreach (SectorLayer m in c.SectorLayers)
        {
            Assert.Contains(Research.SectorActivities, s => s.SectorId == m.Sector);
            Assert.Contains(fx.Session.OverlayLayers, l => l.Key == m.Layer);
        }
        static DiscoveryShortcutContent Parse(string json) => DiscoveryShortcuts.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        Assert.Throws<InvalidDataException>(() => Parse("{\"expiryTurns\": 0}"));
        Assert.Throws<InvalidDataException>(() => Parse("{\"sectorLayers\": []}"));
        Assert.Throws<InvalidDataException>(() => Parse("{\"expiryTurns\": 3, \"sectorLayers\": [{\"sector\": \"farming\", \"layer\": \"a\"}, {\"sector\": \"farming\", \"layer\": \"b\"}]}"));
        Assert.Throws<InvalidDataException>(() => Parse("{\"expiryTurns\": 3, \"sectorLayers\": [{\"sector\": \"farming\"}]}"));
        Assert.Equal(7, Parse("{\"expiryTurns\": 7}").ExpiryTurns);
    }
}
