using System.Collections.Immutable;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Ages;
using Sim.Ui.Headless;
using Sim.Ui.Info;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 POLISH — SHIFT + LEFT CLICK UNIVERSAL INFO (directive §9), RESEARCH DISCOVERABILITY (§10) and "VISIBLY DISABLED"
/// (§4), through the live UI (<see cref="UiFrameHarness"/>, a real ImGui frame per tick) and the painters' models:
/// every painted subject kind is registered where it is drawn; the Director's "Cultivated cereals" example end to end
/// (Age panel → card → Show in research tree); Shift+click NEVER issues an order nor changes the world while a plain
/// click is unchanged; Farming's provenance at Age I; the hover hint is painted (never an ImGui tooltip); Escape
/// closes the card first; the card fits the minimum window; the locked POLICY lines read Governance.GateOf; the
/// granary and the workshop say what they really do; and the M7 limitation is named where military is shown.
/// </summary>
[Collection("ImGui context")]
public class InfoPanelTests : IDisposable
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-info-tests-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    private string WorkDir(string name) => Path.Combine(_root, name);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ rigs

    private static UiSession Small() => UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);

    /// <summary>A session over a copy of a fresh small world with <paramref name="rig"/> applied.</summary>
    private static UiSession Rigged(Action<WorldState, SimConfig> rig)
    {
        UiSession s0 = Small();
        WorldState w = s0.World.Clone();
        rig(w, s0.Config);
        return UiSession.StartFrom(w, 42, 256, 4);
    }

    private static void Know(WorldState w, SimConfig cfg, params string[] ids)
    {
        ResearchContent r = cfg.Research!;
        foreach (string id in ids)
            w.ResearchCompleted.Add(new ResearchCompletedRow(Me, r.Nodes[r.IndexOfId(id)].Key));
    }

    private static int Capital(IReadOnlyWorldState w) => EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c.Value : -1;

    private static ActionSurfaceModel Model(UiSession s) =>
        ActionSurface.ForSession(s, UiSession.ProductionEra(), Capital(s.World), EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)));

    /// <summary>The surface painted at a width where nothing wraps: the model, the screen, and every painted text.</summary>
    private static (ActionSurfaceModel Model, ActionSurfaceScreen Screen, string Text) Painted(UiSession s, double width = 1400)
    {
        ActionSurfaceModel m = Model(s);
        var screen = new ActionSurfaceScreen { Theme = EraThemes.For(m.Era) };
        screen.Refresh(m, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, width);
        var texts = new List<string>();
        foreach (DrawCmd c in d.Commands) if (c is TextCmd t) texts.Add(t.Text);
        return (m, screen, string.Join("\n", texts));
    }

    private static bool Has(IReadOnlyList<InfoHit> hits, InfoSubject subject)
    {
        foreach (InfoHit h in hits) if (h.Subject == subject) return true;
        return false;
    }

    /// <summary>Scrolls POLICY until <paramref name="find"/>'s rect sits fully inside the visible body (clear of a scrollbar).</summary>
    private static RectD? Reveal(UiFrameHarness h, Func<RectD?> find)
    {
        PanelRect p = h.Ui.ContextRect;
        double top = p.Y + 48, bottom = p.Y + p.Height - 32;
        for (int k = 0; k < 60; k++)
        {
            if (find() is not { } r) return null;
            if (r.Y >= top && r.Bottom <= bottom) return r;
            h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, r.Y < top ? 1 : -1);
        }
        return null;
    }

    private static RectD? ActionRect(UiFrameHarness h, ActionHitKind kind, int a = int.MinValue)
    {
        foreach (ActionHit x in h.Ui.Actions.Hits) if (x.Kind == kind && (a == int.MinValue || x.A == a)) return x.Rect;
        return null;
    }

    private static RectD? InfoRect(UiFrameHarness h, Func<InfoHit, bool> pick)
    {
        foreach (InfoHit x in h.Ui.Actions.InfoHits) if (pick(x)) return x.Rect;
        return null;
    }

    private static string Hash(UiSession s) => WorldHash.ComputeHex(s.World);

    // ------------------------------------------------------------------ U01 registration completeness

    [Fact]
    public void Registration_EveryRowTheActionSurfacePaints_IsRegisteredWithItsSubject()
    {
        UiSession s = Rigged((w, cfg) => { Know(w, cfg, "root_crop"); });
        // A research target chosen this turn: the Learning line names it (the gate's sweep clears the target with its
        // "Stop" check before its info area runs, so this registration is pinned here).
        ResearchContent rc = s.Config.Research!;
        ResearchNodeId chosen = rc.Nodes[rc.IndexOfId("grinding_stone")].Key;
        Assert.True(s.EmitResearchOrder(chosen));
        (ActionSurfaceModel m, ActionSurfaceScreen screen, _) = Painted(s);
        IReadOnlyList<InfoHit> hits = screen.InfoHits;
        Assert.Equal(chosen.Value, m.Research!.Effective!.Key);
        Assert.True(Has(hits, InfoSubject.Node(chosen)), "the research target");
        LabourBlock labour = m.Labour!;
        Assert.NotEmpty(labour.Entries);
        foreach (LabourEntry e in labour.Entries)
            Assert.True(Has(hits, InfoSubject.Sector(e.Sector, labour.Settlement.Value)), "labour row " + e.Label);
        ConstructionBlock construction = m.Construction!;
        Assert.NotEmpty(construction.Projects);
        foreach (ProjectEntry p in construction.Projects)
            Assert.True(Has(hits, InfoSubject.OfProject(p.ProjectId, construction.Settlement.Value)), "project " + p.Name);
        Assert.NotNull(m.Production);
        foreach (ProductionEntry e in m.Production!.Entries)
            Assert.Contains(hits, x => x.Subject.Kind == InfoKind.Recipe && x.Label == e.Name);
        Assert.NotNull(m.Military);
        Assert.NotEmpty(m.Military!.Formations);
        foreach (FormationEntry f in m.Military.Formations) Assert.True(Has(hits, InfoSubject.OfFormation((int)f.Id)), "formation " + f.Identity);
        Assert.False(m.Locked.IsDefaultOrEmpty);
        foreach (LockedDomain l in m.Locked) Assert.True(Has(hits, l.Subject), "locked line " + l.Label);
        Assert.NotNull(m.Standing);
        foreach (InfoSubject sub in m.Standing!.Subjects) Assert.True(Has(hits, sub), "standing " + sub);
        Assert.True(Has(hits, InfoSubject.Research), "the Learning heading");
        // Every good named in a painted line is a token: at least one Good hit, each a goods.json id.
        Assert.Contains(hits, x => x.Subject.Kind == InfoKind.Good);
        foreach (InfoHit x in hits)
            if (x.Subject.Kind == InfoKind.Good) Assert.NotNull(s.Config.Goods!.Goods.FirstOrDefault(g => g.Id == x.Subject.Id && g.Name == x.Label));
        // Every registered subject has a card with a title (nothing registered that the query cannot describe).
        foreach (InfoHit x in hits) Assert.False(string.IsNullOrEmpty(InfoQuery.Card(s.World, s.Config, Me, x.Subject).Title), x.Label);
    }

    [Fact]
    public void Registration_TheAgePanel_RegistersEveryMilestoneLineItPaints_AndTheTreeEveryCardInView()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("reg"), Assets(), width: 1920, height: 1080);
        Assert.True(h.ClickControl("band-age"));
        AgePanelModel panel = h.Ui.Age.Panel;
        int painted = 0;
        foreach (MilestoneLine l in panel.Core)
        {
            Assert.True(Has(h.Ui.Age.InfoHits, InfoSubject.Milestone(l.AgeKey, l.Id)), "core milestone " + l.Name);
            painted++;
        }
        Assert.True(painted > 0);
        Assert.True(Has(h.Ui.Age.InfoHits, InfoSubject.OfAge(panel.CurrentAge)), "the banner names the current Age");
        Assert.Contains(h.Ui.InfoRegistry.Hits, x => x.Subject.Kind == InfoKind.AgeMilestone);

        h.Key(Keys.Escape);
        Assert.True(h.ClickControl("band-research"));
        h.Idle(30);
        ProgressionScreen screen = h.Ui.Progression!;
        int cards = 0;
        foreach (int v in screen.VisibleVertices())
        {
            if (screen.Graph.Vertices[v].External || screen.Layout.Placed[v].Hidden) continue;
            PlacedVertex pv = screen.Layout.Placed[v];
            double x = screen.Camera.ToScreenX(pv.CenterX, screen.Canvas.X), y = screen.Camera.ToScreenY(pv.CenterY, screen.Canvas.Y);
            if (!screen.Canvas.Contains(x, y) || (screen.MinimapVisible && screen.MinimapRect().Contains(x, y))) continue;
            ResearchNodeId key = screen.Content.Nodes[screen.Graph.Vertices[v].ContentIndex].Key;
            Assert.True(Has(screen.InfoHits, InfoSubject.Node(key)), "tree card " + screen.Content.Nodes[screen.Graph.Vertices[v].ContentIndex].Id);
            cards++;
        }
        Assert.True(cards > 5, "cards in view " + cards);
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ U02 the Director's example

    [Fact]
    public void TheDirectorsExample_CultivatedCereals_FromTheAgePanel_ToTheCard_ToTheResearchTree()
    {
        UiSession s = Small();
        ResearchContent c = s.Config.Research!;
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("cereals"), Assets());
        Assert.True(h.ClickControl("band-age"));
        InfoSubject cereals = InfoSubject.Milestone(2, "a2_cultivation");
        // UR-5 (merge of m5p-ui): the Age panel SCROLLS and leads with STILL REQUIRED and the progress chips, so at
        // 1280 x 800 the core milestones sit below the fold; a region is registered only where it is visible, so the
        // player scrolls the panel (the wheel over it) until the milestone is on screen.
        RectD over = h.Ui.Age.InfoHits[0].Rect;
        for (int i = 0; i < 20 && !h.Ui.InfoRegistry.Hits.Any(x => x.Subject == cereals); i++) h.Wheel(over.CenterX, over.CenterY, -1);
        InfoHit hit = h.Ui.InfoRegistry.Hits.First(x => x.Subject == cereals);
        int queued = s.QueuedOrders().Count;
        string hash = Hash(s);

        h.ShiftClick(hit.Rect.CenterX, hit.Rect.CenterY);
        Assert.True(h.Ui.Inspector.IsOpen);
        InfoCard card = h.Ui.Inspector.Card!;
        Assert.Equal("Cultivated cereals", card.Title);
        Assert.Equal(c.Nodes[c.IndexOfId("cereal_cultivation")].Key, card.ResearchNode);
        Assert.Contains(card.EnabledBy, l => l.Label == "Cereal cultivation");
        Assert.Contains(card.Prerequisites, l => l.Label == "Grinding stone");
        Assert.True(h.Ui.AgePanelOpen, "the Age panel stays open under the card");
        Assert.Equal(queued, s.QueuedOrders().Count);
        Assert.Equal(hash, Hash(s));

        InfoPanelHit show = h.Ui.Inspector.Hits.Single(x => x.Kind == InfoHitKind.ShowInTree);
        h.Click(show.Rect.CenterX, show.Rect.CenterY);
        h.Idle(10);
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(c.IndexOfId("cereal_cultivation"), h.Ui.Progression!.Selected);
        Assert.Equal(queued, s.QueuedOrders().Count);
        Assert.Equal(hash, Hash(s));
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ U03 Shift+click never orders

    [Fact]
    public void ShiftClick_OnBuild_OpensTheProjectsCard_AndQueuesNothing_APlainClickStillBuilds()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("build"), Assets());
        Assert.True(h.ClickControl("nav:Policy"));
        ActionHit build = h.Ui.Actions.Hits.First(x => x.Kind == ActionHitKind.Build);
        RectD r = Reveal(h, () => ActionRect(h, ActionHitKind.Build, build.A))!.Value;
        int queued = s.QueuedOrders().Count;
        string hash = Hash(s);

        h.ShiftClick(r.CenterX, r.CenterY);
        Assert.True(h.Ui.Inspector.IsOpen);
        Assert.Equal(InfoKind.Project, h.Ui.Inspector.Subject!.Value.Kind);
        Assert.Equal(queued, s.QueuedOrders().Count);
        Assert.Equal(hash, Hash(s));

        h.Key(Keys.Escape);
        Assert.False(h.Ui.Inspector.IsOpen);
        Assert.Equal(Section.Policy, h.Ui.OpenSection);
        r = Reveal(h, () => ActionRect(h, ActionHitKind.Build, build.A))!.Value;
        h.Click(r.CenterX, r.CenterY);
        Assert.Equal(queued + 1, s.QueuedOrders().Count);
        Assert.Equal(OrderKind.EnqueueConstruction, s.QueuedOrders()[^1].Kind);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void ShiftClick_OnLabourApply_QueuesNothing_APlainClickStillSetsThemToWork()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("labour"), Assets());
        Assert.True(h.ClickControl("nav:Policy"));
        LabourBlock labour = h.Ui.Actions.Model!.Labour!;
        int s0 = labour.Entries[0].Sector;
        int before = h.Ui.Actions.Draft[s0];
        RectD slot = Reveal(h, () =>
        {
            foreach (ActionHit x in h.Ui.Actions.Hits)
                if (x.Kind is ActionHitKind.LabourSlot && x.A == s0 && x.B == Math.Max(0, before - 1)) return x.Rect;
            return null;
        })!.Value;
        h.Click(slot.CenterX, slot.CenterY);
        Assert.True(h.Ui.Actions.LabourChanged);
        int queued = s.QueuedOrders().Count;
        string hash = Hash(s);

        RectD apply = Reveal(h, () => ActionRect(h, ActionHitKind.LabourApply))!.Value;
        h.ShiftClick(apply.CenterX, apply.CenterY);
        Assert.Equal(queued, s.QueuedOrders().Count);
        Assert.Equal(hash, Hash(s));

        // The labour row's name, Shift+clicked, is the activity's card (no draft or order change).
        int draft = h.Ui.Actions.Draft[s0];
        RectD name = Reveal(h, () => InfoRect(h, x => x.Subject == InfoSubject.Sector(s0, labour.Settlement.Value)))!.Value;
        h.ShiftClick(name.CenterX, name.CenterY);
        Assert.Equal(InfoSubject.Sector(s0, labour.Settlement.Value), h.Ui.Inspector.Subject);
        Assert.Equal(draft, h.Ui.Actions.Draft[s0]);
        Assert.Equal(queued, s.QueuedOrders().Count);
        h.Key(Keys.Escape);

        apply = Reveal(h, () => ActionRect(h, ActionHitKind.LabourApply))!.Value;
        h.Click(apply.CenterX, apply.CenterY);
        Assert.True(s.QueuedOrders().Count > queued);
        Assert.Equal(OrderKind.SectorAllocation, s.QueuedOrders()[^1].Kind);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void ShiftClick_OnAnAvailableResearchCard_ShowsItsCard_AndTargetsNothing_APlainClickStillTargetsIt()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("node"), Assets());
        Assert.True(h.ClickControl("band-research"));
        ProgressionScreen screen = h.Ui.Progression!;
        screen.ResetView();
        h.Idle(30);
        (double X, double Y, int Node)? pick = null;
        foreach (int v in screen.VisibleVertices())
        {
            if (screen.Graph.Vertices[v].External || screen.Layout.Placed[v].Hidden) continue;
            int ci = screen.Graph.Vertices[v].ContentIndex;
            PlacedVertex pv = screen.Layout.Placed[v];
            double x = screen.Camera.ToScreenX(pv.CenterX, screen.Canvas.X), y = screen.Camera.ToScreenY(pv.CenterY, screen.Canvas.Y);
            if (!screen.Canvas.Contains(x, y) || (screen.MinimapVisible && screen.MinimapRect().Contains(x, y))) continue;
            if (screen.Snapshot!.Nodes[ci].Available && !screen.Snapshot.Nodes[ci].IsTarget) { pick = (x, y, ci); break; }
        }
        Assert.NotNull(pick);
        (double px, double py, int node) = pick!.Value;
        int? choice = s.QueuedResearchChoice();
        int queued = s.QueuedOrders().Count;
        string hash = Hash(s);

        h.ShiftClick(px, py);
        Assert.Equal(InfoSubject.Node(screen.Content.Nodes[node].Key), h.Ui.Inspector.Subject);
        Assert.Equal(choice, s.QueuedResearchChoice());
        Assert.Equal(queued, s.QueuedOrders().Count);
        Assert.Equal(hash, Hash(s));

        h.Key(Keys.Escape);
        Assert.False(h.Ui.Inspector.IsOpen);
        Assert.True(h.Ui.ProgressionOpen, "Escape closed the card, not the tree");
        h.Click(px, py);
        Assert.Equal(node, screen.Selected);
        Assert.Equal(screen.Content.Nodes[node].Key.Value, s.QueuedResearchChoice());
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void APlainClick_OnAPassiveRegion_OpensNoCard_TheFormationIsSelected_AndAShiftClickOpensIt()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("passive"), Assets());
        MilitaryUnitRow unit = MilitaryQuery.Units(h.Ui.World, Me)[0];
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, unit.Location.Value, h.Width, h.Height);
        h.Idle(2);
        InfoHit token = h.Ui.InfoRegistry.Hits.First(x => x.Passive && x.Subject == InfoSubject.OfFormation((int)unit.Id));
        // The token's plain click is the map's: it selects the formation; no card.
        h.Click(token.Rect.CenterX, token.Rect.CenterY);
        Assert.False(h.Ui.Inspector.IsOpen);
        Assert.Equal((int)unit.Id, h.Ui.SelectedUnit);
        h.Key(Keys.Escape);

        // A text line of a player view: a plain click does nothing; Shift+click opens the line's card.
        Assert.True(h.ClickControl("nav:Empire"));
        h.Idle(3);
        PanelRect p = h.Ui.ContextRect;
        InfoHit? line = null;
        for (int k = 0; k < 40 && line is null; k++)
        {
            foreach (InfoHit x in h.Ui.InfoRegistry.Hits)
                if (x.Passive && x.Subject.Kind == InfoKind.TaxEdict && x.Rect.Bottom < p.Y + p.Height - 32) { line = x; break; }
            if (line is null) h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, -1);
        }
        Assert.NotNull(line);
        h.Click(line!.Value.Rect.CenterX, line.Value.Rect.CenterY);
        Assert.False(h.Ui.Inspector.IsOpen);
        h.ShiftClick(line.Value.Rect.CenterX, line.Value.Rect.CenterY);
        Assert.Equal(InfoSubject.Tax, h.Ui.Inspector.Subject);
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ U04 Farming at Age I

    [Fact]
    public void Farming_AtAgeI_SaysWhereItCameFrom_AndItsCardNamesRootAndTuberCultivation()
    {
        UiSession s = Rigged((w, cfg) => Know(w, cfg, "root_crop"));
        Assert.Equal(1, AgeQuery.CurrentAge(s.World, s.Config.Ages!, Me));
        (ActionSurfaceModel m, _, string text) = Painted(s);
        Assert.True(m.Layout == SurfaceLayout.Flat, "Age I is the flat density the provenance was once hidden at");
        LabourEntry farming = m.Labour!.Entries.Single(e => e.Sector == Sim.Core.State.Sectors.Farming);
        Assert.Equal("Farming", farming.Label);
        Assert.Equal("Root and tuber cultivation", farming.LearnedFrom);
        Assert.Contains("from Root and tuber cultivation", text);

        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("farming"), Assets());
        Assert.True(h.ClickControl("nav:Policy"));
        InfoSubject subject = InfoSubject.Sector(Sim.Core.State.Sectors.Farming, m.Labour.Settlement.Value);
        RectD r = Reveal(h, () => InfoRect(h, x => x.Subject == subject))!.Value;
        h.ShiftClick(r.CenterX, r.CenterY);
        InfoCard card = h.Ui.Inspector.Card!;
        Assert.Equal("Farming", card.Title);
        ResearchContent c = s.Config.Research!;
        Assert.Equal(c.Nodes[c.IndexOfId("root_crop")].Key, card.ResearchNode);
        Assert.Contains(card.EnabledBy, l => l.Label == "Root and tuber cultivation");
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ U05/U06 hint and Escape

    [Fact]
    public void TheHoverHint_IsPaintedBesideThePointer_NeverAnImGuiTooltip_AndNotOverTheCard()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("hint"), Assets());
        Assert.True(h.ClickControl("nav:Policy"));
        RectD r = Reveal(h, () => InfoRect(h, x => x.Subject.Kind == InfoKind.LabourSector))!.Value;
        h.MoveTo(r.CenterX, r.CenterY);
        Assert.Equal(InfoPanel.HintText, h.Ui.InfoHint);
        Assert.NotNull(h.Ui.InfoHintRect);
        Assert.Empty(h.Gui.LastTooltipWindows);
        // Off any inspectable region: no hint.
        h.MoveTo(h.Ui.ContextRect.X - 300, h.Ui.ContextRect.Y + 400);
        Assert.Null(h.Ui.InfoHint);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void Escape_ClosesTheCardFirst_ThenTheSectionBeneathIt_AndNeverAsksToExit()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("escape"), Assets());
        Assert.True(h.ClickControl("nav:Empire"));
        h.Ui.OpenInfo(InfoSubject.Tax);
        h.Frame();
        Assert.True(h.Ui.Inspector.IsOpen);
        Assert.NotNull(h.Ui.Controls.Find("info-card"));
        h.Key(Keys.Escape);
        Assert.False(h.Ui.Inspector.IsOpen);
        Assert.Equal(Section.Empire, h.Ui.OpenSection);
        Assert.Equal(0, h.ExitRequests);
        h.Key(Keys.Escape);
        Assert.Equal(Section.None, h.Ui.OpenSection);
        Assert.Equal(0, h.ExitRequests);
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ U07 minimum window

    [Fact]
    public void AtTheMinimumWindow_TheCardFitsTheRightColumn_AndItsCloseIsReachable()
    {
        UiSession s = Small();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("min"), Assets(), width: 1080, height: 640);
        h.Ui.OpenInfo(InfoSubject.Milestone(2, "a2_cultivation"));
        h.Frame();
        PanelRect p = h.Ui.ContextRect;
        // The card's body (a scrolled child, taller than the window when the card is long) is as wide as the column
        // and starts inside the window; the header's close button is wholly inside both.
        UiControl body = h.Ui.Controls.Find("info-card")!.Value;
        Assert.True(body.X0 >= p.X - 1 && body.X1 <= p.X + p.Width + 1 && body.Y0 >= p.Y && body.Y0 < 640,
            "info-card at " + body.X0 + ".." + body.X1 + " x " + body.Y0 + ".." + body.Y1);
        UiControl close = h.Ui.Controls.Find("info-close")!.Value;
        Assert.True(close.X0 >= p.X - 1 && close.X1 <= p.X + p.Width + 1 && close.Y0 >= p.Y && close.Y1 <= p.Y + p.Height,
            "info-close at " + close.X0 + ".." + close.X1 + " x " + close.Y0 + ".." + close.Y1);
        Assert.True(h.ClickControl("info-close"));
        Assert.False(h.Ui.Inspector.IsOpen);
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ visibly disabled (§4, verify G2)

    [Fact]
    public void TheTaxEdict_InItsThreeGateStates_IsALockedLineWithGovernancesReason_OrTheControl_NeverBoth()
    {
        // Needs knowledge (A1, Taxation unknown).
        UiSession a1 = Small();
        (ActionSurfaceModel m1, _, string t1) = Painted(a1);
        Assert.Equal(TaxGate.NeedsKnowledge, Governance.GateOf(a1.World, a1.Config, Me));
        Assert.Null(m1.Governance);
        LockedDomain tax1 = m1.Locked.Single(l => l.Subject == InfoSubject.Tax);
        Assert.Equal("needs Taxation (Civics) and the Bronze Age (Age III)", tax1.Reason);
        Assert.Contains("Tax edict", t1);
        Assert.Contains("needs Taxation (Civics) and the Bronze Age (Age III)", t1);

        // Needs the Age (Taxation known at A1).
        UiSession known = Rigged((w, cfg) => Know(w, cfg, "taxation"));
        Assert.Equal(TaxGate.NeedsAge, Governance.GateOf(known.World, known.Config, Me));
        (ActionSurfaceModel m2, _, _) = Painted(known);
        Assert.Null(m2.Governance);
        Assert.Equal("Taxation (Civics) is known; needs the Bronze Age (Age III)", m2.Locked.Single(l => l.Subject == InfoSubject.Tax).Reason);

        // Open (Taxation and Age III): the control, and no locked line.
        UiSession open = Rigged((w, cfg) => { Know(w, cfg, "taxation"); TaxAgeRig.EnterTaxAge(w, cfg, Me); });
        Assert.Equal(TaxGate.Open, Governance.GateOf(open.World, open.Config, Me));
        (ActionSurfaceModel m3, _, _) = Painted(open);
        Assert.NotNull(m3.Governance);
        Assert.DoesNotContain(m3.Locked, l => l.Subject == InfoSubject.Tax);

        // Road development and the Age advance are locked lines exactly when the query does not list them.
        foreach (ActionSurfaceModel m in new[] { m1, m2, m3 })
        {
            Assert.Equal(m.Roads is null, m.Locked.Any(l => l.Subject.Kind == InfoKind.RoadClass));
            foreach (LockedDomain l in m.Locked) Assert.False(string.IsNullOrWhiteSpace(l.Reason), l.Label);
        }
        LockedDomain age = m1.Locked.Single(l => l.Subject.Kind == InfoKind.Age);
        Assert.Null(m1.Age);
        Assert.Equal(InfoQuery.Card(a1.World, a1.Config, Me, age.Subject).Summary, age.Reason);
    }

    [Fact]
    public void TheGranary_SaysWhatItReallyDoes_AndTheWorkshop_NamesTheChainItWaitsOn()
    {
        UiSession s = Small();
        (ActionSurfaceModel m, _, string text) = Painted(s);
        ConstructionBlock construction = m.Construction!;
        ProjectEntry granary = construction.Projects.Single(p => p.Name.Contains("ranary", StringComparison.Ordinal));
        Assert.NotNull(granary.Effects);
        Assert.Contains("counts toward Age II", granary.Effects);
        Assert.Contains("once a levy exists, eases how heavily it is felt here", granary.Effects);
        Assert.DoesNotContain("food", granary.Effects!, StringComparison.OrdinalIgnoreCase);   // a granary stores nothing in this build
        Assert.Contains("does: " + granary.Effects, text);
        InfoCard card = InfoQuery.Card(s.World, s.Config, Me, InfoSubject.OfProject(granary.ProjectId, construction.Settlement.Value));
        Assert.Contains("holds no goods", card.RealizedBy);
        Assert.Contains(card.Enables, l => l.Subject is { Kind: InfoKind.AgeMilestone });

        ProjectEntry workshop = construction.Projects.Single(p => p.Name.Contains("orkshop", StringComparison.Ordinal));
        Assert.NotNull(workshop.Blocker);
        Assert.Contains("tools", workshop.Blocker);
        Assert.Equal(InfoQuery.SourceChain(s.World, s.Config, Me, "tools"), workshop.Chain);
        Assert.StartsWith("tools <- Toolmaking <- bronze <- Bronze casting <- Tin bronze", workshop.Chain);
        Assert.Contains("(research, Age III)", workshop.Chain);
        Assert.Contains("comes from: " + workshop.Chain, text);
    }

    [Fact]
    public void AKnownCraftThatRunsNowhere_SaysItsConditionWithTheLiveValue_OrWhatItCannotObtain()
    {
        UiSession s = Small();
        (ActionSurfaceModel m, _, string text) = Painted(s);
        Assert.NotNull(m.Production);
        int explained = 0;
        foreach (ProductionEntry e in m.Production!.Entries)
        {
            if (e.Settlements > 0) continue;
            Assert.True(e.Condition is not null || e.Missing is not null || e.Blocker is not null, e.Name + " runs nowhere and says nothing");
            if (e.Condition is { } c) { Assert.Contains(" - ", c); Assert.Contains("runs where " + c, text); explained++; }
            if (e.Missing is { } miss) { Assert.Contains("cannot produce: " + miss, text); explained++; }
        }
        Assert.True(explained > 0, "a known craft that runs nowhere is explained");
    }

    // ------------------------------------------------------------------ P-INFO-4: the Age panel points at the tree

    [Fact]
    public void TheAgePanel_NamesTheResearchToDoNext_AndK_OpensTheTreeOnIt()
    {
        UiSession s = Small();
        ResearchContent c = s.Config.Research!;
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("k"), Assets());
        Assert.True(h.ClickControl("band-age"));
        AgePanelModel panel = h.Ui.Age.Panel;
        MilestoneLine cereals = panel.Core.Single(l => l.Id == "a2_cultivation");
        Assert.Equal(c.Nodes[c.IndexOfId("grinding_stone")].Key.Value, cereals.NextNodeKey);
        Assert.Equal("Grinding stone", cereals.NextNodeName);
        Assert.Equal("Cereal cultivation", cereals.TargetNodeName);
        int key = panel.SuggestedNodeKey;
        Assert.Equal(cereals.NextNodeKey, key);   // the first unmet core research milestone's next node
        int queued = s.QueuedOrders().Count;
        h.Key(Keys.K);
        h.Idle(5);
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Equal(c.IndexOf(new ResearchNodeId(key)), h.Ui.Progression!.Selected);
        Assert.Equal(queued, s.QueuedOrders().Count);
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ P-INFO-4: the node detail

    [Fact]
    public void TheNodeDetail_NamesItsAgeMilestone_WhatItOpens_AndSaysKnowledgeOnlyWhenNothingReadsIt()
    {
        UiSession s = Small();
        ResearchContent c = s.Config.Research!;
        string Detail(string id)
        {
            var screen = new ProgressionScreen(c, Me) { Config = s.Config };
            screen.Refresh(s.World);
            int i = c.IndexOfId(id);
            screen.Tab = c.Nodes[i].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
            screen.Selected = i;
            DrawList d = screen.Paint(1600, 1000, ApproxTextMeasure.Instance);
            var texts = new List<string>();
            foreach (DrawCmd cmd in d.Commands) if (cmd is TextCmd t) texts.Add(t.Text);
            return string.Join("\n", texts);
        }
        string cereal = Detail("cereal_cultivation");
        Assert.Contains("OPENS THE WAY TO", cereal);
        Assert.Contains("Cultivated cereals", cereal);
        string flaking = Detail("pressure_flaking");
        Assert.Contains("DESCRIBED, NOT SIMULATED", flaking);
        Assert.Contains("Knowledge only - no simulated effect in this build.", flaking);
    }

    // ------------------------------------------------------------------ the M7 limitation is named

    [Fact]
    public void TheBattleLayer_IsNamed_WhereverMilitaryIsShown()
    {
        UiSession s = Small();
        ActionSurfaceModel m = Model(s);
        Assert.Contains("Battle Layer (M7)", m.Military!.Note);
        LensPage military = Lenses.Page(Lens.Military, s.World, s.Config.Research!, Me);
        Assert.Contains("Battle Layer (M7)", military.StatusNote);
        WorldProjection p = WorldProjection.Build(s.World, s.Config, id => s.Names.Name(id), Me);
        Assert.Contains(p.Absent, a => a.Contains("Battle Layer (M7)", StringComparison.Ordinal));
    }
}
