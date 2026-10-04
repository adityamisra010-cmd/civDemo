using System.Collections.Immutable;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Ages;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>The canonical founded world at turn 1 (seed 42, 1024 px, 12 settlements) — shared READ-ONLY;
/// a test that issues orders builds its own session from a clone.</summary>
public sealed class CanonicalTurnOneFixture
{
    public UiSession Session { get; } = UiSession.Start(42);
}

/// <summary>
/// ADR-033 D1/D2 at the UI — THE ACTION SURFACE ACCEPTANCE (directive "NO MODERN DASHBOARD AT TURN 1"): on the
/// canonical founded world at turn 1 the surface holds exactly the civilization's baseline set and no Farming %,
/// Construction %, Industry %, tax, road, institution or advanced-production control; completing root_crop makes
/// Farming appear (the same sector order), completing a taxation node makes the tax edict appear, and knowing a
/// road class makes road development appear. Every block comes from AvailableActionsQuery / LabourActivities.
/// </summary>
public class ActionSurfaceTests(CanonicalTurnOneFixture fx) : IClassFixture<CanonicalTurnOneFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static readonly EraTable Era = UiSession.ProductionEra();

    private static int Capital(IReadOnlyWorldState w) => EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c.Value : -1;

    private static EraTheme ThemeOf(UiSession s, IReadOnlyWorldState w) => EraThemes.For(UiEras.Of(w, s.Config.Ages, Me));

    private static ActionSurfaceModel Surface(UiSession s, int selected, EraTheme? theme = null) =>
        ActionSurface.ForSession(s, Era, selected, theme ?? ThemeOf(s, s.World));

    private static (ActionSurfaceScreen Screen, DrawList List) Painted(UiSession s, ActionSurfaceModel model, EraTheme theme)
    {
        var screen = new ActionSurfaceScreen { Theme = theme };
        screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 360);
        return (screen, d);
    }

    private static List<string> Texts(DrawList d)
    {
        var texts = new List<string>();
        foreach (DrawCmd c in d.Commands) if (c is TextCmd t) texts.Add(t.Text);
        return texts;
    }

    private static UiSession Knowing(UiSession from, params string[] nodeIds)
    {
        ResearchContent research = from.Config.Research!;
        WorldState w = from.World.Clone();
        foreach (string id in nodeIds) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId(id)].Key));
        return UiSession.StartFrom(w, from.World.Seed);
    }

    private static ActionHit Hit(ActionSurfaceScreen screen, ActionHitKind kind, int a = -1, int b = -1)
    {
        foreach (ActionHit h in screen.Hits)
            if (h.Kind == kind && (a < 0 || h.A == a) && (b < 0 || h.B == b)) return h;
        throw new InvalidOperationException("no hit region " + kind);
    }

    private static ActionCommand ClickOn(ActionSurfaceScreen screen, ActionHit h) => screen.Click(h.Rect.CenterX, h.Rect.CenterY);

    // ------------------------------------------------------------------ turn 1

    [Fact]
    public void TurnOne_TheSurfaceIsExactlyTheBaselineSet()
    {
        UiSession s = fx.Session;
        int capital = Capital(s.World);
        Assert.Equal(12, s.World.Settlements.Count);
        ActionSurfaceModel m = Surface(s, capital);

        Assert.Equal(UiEra.Prehistoric, m.Era);
        Assert.Equal(SurfaceLayout.Flat, m.Layout);   // A1: a short flat list
        Assert.Equal(new[] { ActionDomain.Labour, ActionDomain.Research, ActionDomain.Construction, ActionDomain.Military, ActionDomain.Standing, ActionDomain.Production },
            m.Domains.ToArray());
        // R1: the crafts known at founding are exactly the content's BASELINE recipes (null requirement) — no
        // pottery firing (pottery_open_fired) and no bronze casting (tin_bronze) before their research.
        Assert.Equal(new[] { "Weaving", "Toolmaking" }, m.Production!.Entries.Select(e => e.Name).ToArray());
        Assert.All(m.Production.Entries, e => Assert.Null(e.LearnedFrom));
        Assert.Null(m.Age);          // not eligible: no advance
        Assert.Null(m.Roads);        // no road class known
        Assert.Null(m.Governance);   // no taxation node known
        Assert.Empty(m.Notices);

        // LABOUR: the capital's five sectors under their BASELINE identities, as SectorAllocation orders.
        LabourBlock labour = m.Labour!;
        Assert.Equal(capital, labour.Settlement.Value);
        Assert.Equal(new[] { "Gathering", "Hunting & fishing", "Gathering wood & stone", "Crafts & toolmaking", "Building" },
            labour.Entries.Select(e => e.Label).ToArray());
        Assert.Equal(new[] { "gathering", "hunting_fishing", "gathering_wood_stone", "crafts_toolmaking", "building" },
            labour.Entries.Select(e => e.Identities.Single().Id).ToArray());
        for (int sector = 0; sector < Sectors.Count; sector++)
        {
            LabourEntry e = labour.Entries[sector];
            Assert.Equal(sector, e.Sector);
            Assert.False(e.Researched);
            Assert.Null(e.NewMarker);
            Assert.Equal(OrderKind.SectorAllocation, e.Action.Order);
            Assert.Equal(LabourActivities.PackTarget(new SettlementId(capital), sector), e.Action.Id);
        }
        Assert.Equal(12, labour.Controlled.Length);   // the Empire-wide summary covers every settlement it commands
        Assert.Equal(12, labour.Summary.Settlements);

        // RESEARCH: idle — choose; the ten roots are open; nothing to clear.
        Assert.True(m.Research!.Idle);
        Assert.False(m.Research.CanClear);
        Assert.Equal(10, m.Research.Available);

        // CONSTRUCTION: the two zero-node projects of the capital, legal and blocked by materials.
        Assert.Equal(new[] { "Granary", "Workshop" }, m.Construction!.Projects.Select(p => p.Name).ToArray());
        Assert.All(m.Construction.Projects, p => Assert.StartsWith("needs ", p.Blocker));
        Assert.Contains(m.Construction.Projects, p => p.Blocker == "needs 40 timber (has 0), 20 stone (has 0)");

        // MILITARY: basic fighting — the founding warband — information only.
        Assert.Equal((ActionKind.Standing, (OrderKind?)null), (m.Military!.Capability.Kind, m.Military.Capability.Order));
        Assert.Equal("Warband", Assert.Single(m.Military.Formations).Identity);

        // STANDING: what the people do on their own, a compact list (the query's simulated baselines).
        Assert.Equal(8, m.Standing!.Items.Length);
        Assert.Contains("Migration and resettlement", m.Standing.Items);
        Assert.Contains("Basic fishing", m.Standing.Items);   // parenthetical gloss dropped from the compact list
    }

    [Fact]
    public void TurnOne_NoModernDashboard_NoPercent_NoFutureControl()
    {
        UiSession s = fx.Session;
        EraTheme a1 = ThemeOf(s, s.World);
        ActionSurfaceModel m = Surface(s, Capital(s.World), a1);

        // The A1 control is a tally of ten pebbles with no numerals: no numeric % dashboard.
        Assert.Equal(new LabourControlSpec(LabourControlKind.Pebbles, 10, false), m.Control);
        (ActionSurfaceScreen screen, DrawList d) = Painted(s, m, a1);
        List<string> texts = Texts(d);
        Assert.NotEmpty(texts);
        Assert.DoesNotContain(texts, t => t.Contains('%'));
        foreach (string banned in new[] { "Farming", "Industry", "tax", "Tax", "levy", "Levy", "road", "Road", "nstitution", "niversit", "Advance", "Pottery", "Bronze casting" })
            Assert.DoesNotContain(texts, t => t.Contains(banned, StringComparison.Ordinal));

        // The ONLY interactive regions: the pebbles, the settlement cycle, the trees link and the baseline
        // projects' build buttons — no slider track, no tax, road, Age or apply control (nothing changed yet).
        var kinds = screen.Hits.Select(h => h.Kind).Distinct().OrderBy(k => k).ToArray();
        Assert.Equal(new[] { ActionHitKind.LabourSlot, ActionHitKind.ResearchOpen, ActionHitKind.Build, ActionHitKind.PrevSettlement, ActionHitKind.NextSettlement }
            .OrderBy(k => k).ToArray(), kinds);
        Assert.Equal(Sectors.Count * 10, screen.Hits.Count(h => h.Kind == ActionHitKind.LabourSlot));
        Assert.Equal(2, screen.Hits.Count(h => h.Kind == ActionHitKind.Build));

        // Every control is backed by a descriptor the query returned — the UI decides no availability.
        ImmutableArray<ActionDescriptor> query = AvailableActionsQuery.For(s.World, s.Config, Me,
            ActionQueryContext.ForNextStep(s.World, Era, s.QueuedOrders()));
        Assert.True(AvailableActionsQuery.Same(query, m.Actions));
        Assert.Equal(12 * 5 + 1 + 24 + 1 + 8 + 2, query.Length);   // R1: + the two baseline recipes
        foreach (ActionHit build in screen.Hits.Where(h => h.Kind == ActionHitKind.Build))
            Assert.Contains(query, a => a.Domain == ActionDomain.Construction && a.Targets[0].Id == build.A && a.Targets[1].Id == build.B);
    }

    [Fact]
    public void TurnOne_TheStatusBand_ShowsResearchIdle_AndTheAgeAsACompactIndicator()
    {
        UiSession s = fx.Session;
        ActionSurfaceModel m = Surface(s, Capital(s.World));
        ResearchFigure r = StatusFigures.Research(m.Research);
        Assert.True(r.Idle);
        Assert.Equal("research idle [K]", r.Text);
        AgeFigure a = StatusFigures.Age(AgePanelModel.Build(s.World, s.Config.Ages, s.QueuedOrders(), Me));
        Assert.Equal(AgePanelState.NotEligible, a.State);
        Assert.StartsWith("Age I " + s.Config.Ages!.Age(1).Name, a.Text);   // the FULL Age name
        Assert.EndsWith(" - not yet", a.Text);
    }

    // ------------------------------------------------------------------ what appears with knowledge

    [Fact]
    public void RootCrop_MakesFarmingAppear_AsTheSameSectorOrder()
    {
        UiSession s0 = fx.Session;
        int capital = Capital(s0.World);
        UiSession s1 = Knowing(s0, "root_crop");
        LabourEntry before = Surface(s0, capital).Labour!.Entries[Sectors.Farming];
        LabourEntry after = Surface(s1, capital).Labour!.Entries[Sectors.Farming];

        Assert.Equal("Gathering", before.Label);
        Assert.Equal("Farming", after.Label);
        Assert.True(after.Researched);
        Assert.Equal("Root and tuber cultivation", after.LearnedFrom);
        // The SAME sector order: the same packed target, the same kind, the same share and goods.
        Assert.Equal(before.Action.Id, after.Action.Id);
        Assert.Equal((OrderKind?)OrderKind.SectorAllocation, after.Action.Order);
        // R4: the same share; the goods text follows the capability — gathered "wild food (grain)" before the crop,
        // cultivated "grain" after (LabourActivities.HarvestsWildFood, the predicate production's yield reads).
        Assert.Equal(before.Share, after.Share);
        Assert.Equal("wild food (grain)", before.Produces);
        Assert.Equal("grain", after.Produces);

        // ...and the control emits exactly the batch it emitted before the crop was known.
        var screen = new ActionSurfaceScreen { Theme = ThemeOf(s1, s1.World) };
        screen.Refresh(Surface(s1, capital), s1.World, s1.Config, Me, id => s1.Names.Name(id));
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ClickOn(screen, Hit(screen, ActionHitKind.LabourSlot, Sectors.Farming, 3));   // four pebbles to farming
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ActionCommand apply = ClickOn(screen, Hit(screen, ActionHitKind.LabourApply));
        Assert.Equal(ActionCommandKind.ApplyLabour, apply.Kind);
        int n = s1.Orders.Count;
        Assert.True(ActionDispatch.Apply(s1, apply));
        Assert.Equal(n + Sectors.Count, s1.Orders.Count);
        for (int sector = 0; sector < Sectors.Count; sector++)
        {
            OrderRecord o = s1.Orders[n + sector];
            Assert.Equal((OrderKind.SectorAllocation, LabourActivities.PackTarget(new SettlementId(capital), sector)), (o.Kind, o.TargetId));
        }
        Assert.Equal(40.0, s1.Orders[n + Sectors.Farming].Amount);   // four pebbles of ten points

        // Nothing else appeared: a crop opens no tax, road or Age action.
        ActionSurfaceModel m1 = Surface(s1, capital);
        Assert.Null(m1.Governance);
        Assert.Null(m1.Roads);
    }

    [Fact]
    public void ATaxationNode_MakesTheTaxEdictAppear_AndItEmitsTheGovernanceOrder()
    {
        UiSession s = Knowing(fx.Session, "taxation");
        int capital = Capital(s.World);
        ActionSurfaceModel m = Surface(s, capital);
        GovernanceBlock g = m.Governance!;
        Assert.Equal("Taxation", g.LearnedFrom);
        Assert.False(g.HasPolicy);
        Assert.StartsWith("legitimacy ", g.Legitimacy);
        Assert.Equal(12, g.Collection.Length);   // one collection/reach line per settlement the Empire controls

        var screen = new ActionSurfaceScreen { Theme = ThemeOf(s, s.World) };
        screen.Refresh(m, s.World, s.Config, Me, id => s.Names.Name(id));
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ClickOn(screen, Hit(screen, ActionHitKind.TaxSlot, -1, 1));   // the second pebble: a 20 % levy at A1
        Assert.Equal(20, screen.TaxDraft);
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ActionCommand declare = ClickOn(screen, Hit(screen, ActionHitKind.TaxApply));
        Assert.Equal((ActionCommandKind.SetTax, 20.0), (declare.Kind, declare.Percent));
        Assert.True(ActionDispatch.Apply(s, declare));
        Assert.Equal(Governance.TaxOrder(s.World.Clock.Turn, Me, 20), s.Orders[^1]);
        Assert.Equal(20, Surface(s, capital).Governance!.QueuedPercent);
    }

    [Fact]
    public void KnowingARoadClass_MakesRoadDevelopmentAppear_AndItEmitsTheRoadOrder()
    {
        ResearchContent research = fx.Session.Config.Research!;
        UiSession s = Knowing(fx.Session, ActionSurfacePreview.WithAncestors(research, "track_road"));
        s.EndTurn();   // founding writes no SettlementDistances; the routes exist from turn 1
        int capital = Capital(s.World);
        ActionSurfaceModel m = Surface(s, capital);
        RoadsBlock roads = m.Roads!;
        Assert.Equal("Trackway", roads.ClassName);
        Assert.Equal("Trackway", roads.LearnedFrom);
        Assert.NotEmpty(roads.Routes);
        Assert.Null(roads.OrderedPercent);

        var screen = new ActionSurfaceScreen { Theme = ThemeOf(s, s.World) };
        screen.Refresh(m, s.World, s.Config, Me, id => s.Names.Name(id));
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ActionCommand develop = ClickOn(screen, Hit(screen, ActionHitKind.RoadApply));
        Assert.Equal(ActionCommandKind.DevelopRoads, develop.Kind);
        Assert.True(ActionDispatch.Apply(s, develop));
        Assert.Equal(RoadDevelopmentQuery.DevelopOrder(s.World, Me, develop.Percent), s.Orders[^1]);
        Assert.Equal(develop.Percent, Surface(s, capital).Roads!.OrderedPercent);
        Assert.False(ActionDispatch.Apply(s, develop));   // one development per Empire per turn

        // The same played turn WITHOUT the class lists no road action.
        UiSession plain = UiSession.StartFrom(fx.Session.World.Clone(), 42);
        plain.EndTurn();
        Assert.Null(Surface(plain, capital).Roads);
    }

    [Fact]
    public void LearningACrop_ThroughTheOrderPath_MarksTheNewActivity_OnlyOnTheTurnItAppears()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int capital = Capital(s.World);
        ResearchContent research = s.Config.Research!;
        ResearchNodeId crop = research.Nodes[research.IndexOfId("root_crop")].Key;
        Assert.True(s.EmitResearchOrder(crop));
        Assert.Equal("research: Root and tuber cultiv... next turn", StatusFigures.Research(Surface(s, capital).Research).Text);
        for (int t = 0; t < 300 && !ResearchQuery.IsCompleted(s.World, Me, crop); t++)
        {
            s.EndTurn();
            if (!ResearchQuery.IsCompleted(s.World, Me, crop))
            {
                ResearchBlock r = Surface(s, capital).Research!;
                Assert.Equal("Root and tuber cultivation", r.Target!.Name);
                Assert.Equal(ResearchQuery.ResearchPointPool(s.World, research, Me), r.PointsPerTurn);
                Assert.Equal("Gathering", Surface(s, capital).Labour!.Entries[0].Label);
            }
        }
        Assert.True(ResearchQuery.IsCompleted(s.World, Me, crop));

        // The turn it appears: the entry changes with the knowledge and carries the marker; the step is announced.
        ActionSurfaceModel now = Surface(s, capital);
        Assert.Equal("Farming", now.Labour!.Entries[0].Label);
        Assert.Equal("new: Farming (Root and tuber cultivation)", now.Labour.Entries[0].NewMarker);
        Assert.Contains("learned: Root and tuber cultivation", now.Notices);
        Assert.Contains("new: Farming (Root and tuber cultivation)", now.Notices);
        Assert.True(now.Research!.Idle);   // the target completed: research is idle until a new choice

        // The next turn the marker is gone; the identity stays.
        s.EndTurn();
        ActionSurfaceModel later = Surface(s, capital);
        Assert.Equal("Farming", later.Labour!.Entries[0].Label);
        Assert.Null(later.Labour.Entries[0].NewMarker);
        Assert.Empty(later.Notices);
    }

    // ------------------------------------------------------------------ the era decides HOW

    [Fact]
    public void ControlGranularity_ComesFromTheEraToken()
    {
        Assert.Equal(new LabourControlSpec(LabourControlKind.Pebbles, 10, false), LabourControlSpec.For(ControlGranularity.Coarse));
        Assert.Equal(new LabourControlSpec(LabourControlKind.Counters, 10, false), LabourControlSpec.For(ControlGranularity.Simple));
        Assert.Equal(new LabourControlSpec(LabourControlKind.Notches, 20, true), LabourControlSpec.For(ControlGranularity.Standard));
        Assert.Equal(new LabourControlSpec(LabourControlKind.Slider, 100, true), LabourControlSpec.For(ControlGranularity.Fine));
        Assert.Equal(new LabourControlSpec(LabourControlKind.PreciseSlider, 100, true), LabourControlSpec.For(ControlGranularity.Precise));
        foreach (UiEra era in UiEras.All)
        {
            EraTheme t = EraThemes.For(era);
            Assert.Equal(LabourControlSpec.For(t.Controls.Granularity), LabourControlSpec.For(t));
            Assert.Equal(100, LabourControlSpec.For(t).Units * LabourControlSpec.For(t).PercentPerUnit);
        }
        // A1–A2: primitive tallies in coarse steps with no numerals; A8–A9: sliders with numbers.
        Assert.False(LabourControlSpec.For(EraThemes.For(UiEra.Prehistoric)).Numerals);
        Assert.False(LabourControlSpec.For(EraThemes.For(UiEra.Neolithic)).Numerals);
        Assert.True(LabourControlSpec.For(EraThemes.For(UiEra.Industrial)).IsSlider);
        Assert.Equal(LabourControlKind.PreciseSlider, LabourControlSpec.For(EraThemes.For(UiEra.Modern)).Kind);
    }

    [Fact]
    public void LaterEras_GroupByDomain_WithNumbers_AndTheSameActions()
    {
        UiSession s = fx.Session;
        WorldState a8 = EraPreview.WorldAt(s.World, s.Config.Ages!, Me, 8);
        UiSession s8 = UiSession.StartFrom(a8, 42);
        EraTheme t8 = ThemeOf(s8, s8.World);
        ActionSurfaceModel m8 = Surface(s8, Capital(a8), t8);
        ActionSurfaceModel m1 = Surface(s, Capital(s.World));
        Assert.Equal(SurfaceLayout.Detailed, m8.Layout);
        Assert.Equal(LabourControlKind.Slider, m8.Control.Kind);
        // The Age changes HOW, never WHAT: the same actions are listed.
        Assert.Equal(m1.Domains.ToArray(), m8.Domains.ToArray());
        Assert.Equal(m1.Labour!.Entries.Select(e => e.Label), m8.Labour!.Entries.Select(e => e.Label));
        (ActionSurfaceScreen screen, DrawList d) = Painted(s8, m8, t8);
        List<string> texts = Texts(d);
        Assert.Contains(texts, t => t.EndsWith('%'));                                    // numerals
        Assert.Contains(texts, t => t.StartsWith("Work - ", StringComparison.Ordinal));   // a domain header (set in capitals by the era)
        Assert.Contains(screen.Hits, h => h.Kind == ActionHitKind.LabourTrack);
        Assert.DoesNotContain(screen.Hits, h => h.Kind == ActionHitKind.LabourSlot);
    }

    [Fact]
    public void TheSlider_SetsTheValueUnderThePointer_AndADragKeepsTheSum()
    {
        // A8: the Fine control — a slider in whole points. A press on the track sets the value UNDER THE POINTER:
        // repainted, the knob sits where the pointer is (within half a point, the rounding to whole points) — near
        // the ends too, where a hit region wider than the drawn track would put the knob points away from it.
        UiSession s8 = UiSession.StartFrom(EraPreview.WorldAt(fx.Session.World, fx.Session.Config.Ages!, Me, 8), 42);
        EraTheme t8 = ThemeOf(s8, s8.World);
        (ActionSurfaceScreen screen, _) = Painted(s8, Surface(s8, Capital(s8.World), t8), t8);
        Assert.Equal(LabourControlKind.Slider, screen.Model!.Control.Kind);
        int units = screen.Model.Control.Units;
        ActionHit track = Hit(screen, ActionHitKind.LabourTrack, Sectors.Herding);
        double halfPoint = track.Rect.W / units / 2;

        double press = track.Rect.X + track.Rect.W * 0.052;                       // near the left end: 5 points
        screen.Click(press, track.Rect.CenterY);
        Assert.Equal(5, screen.Draft[Sectors.Herding]);
        Assert.Equal(100, screen.Draft.ToArray().Sum());
        Assert.InRange(KnobX(screen, ActionHitKind.LabourTrack, Sectors.Herding) - press, -halfPoint, halfPoint);

        double drag = track.Rect.X + track.Rect.W * 0.948;                        // dragged near the right end: 95
        Assert.True(screen.Drag(drag));
        Assert.Equal(95, screen.Draft[Sectors.Herding]);
        Assert.Equal(100, screen.Draft.ToArray().Sum());
        Assert.InRange(KnobX(screen, ActionHitKind.LabourTrack, Sectors.Herding) - drag, -halfPoint, halfPoint);

        screen.Release();
        Assert.False(screen.Drag(track.Rect.X));                                  // released: no drag continues
        Assert.Equal(95, screen.Draft[Sectors.Herding]);
        ActionHit plus = Hit(screen, ActionHitKind.LabourPlus, Sectors.Herding);
        screen.Click(plus.Rect.CenterX, plus.Rect.CenterY);
        Assert.Equal(96, screen.Draft[Sectors.Herding]);
        Assert.Equal(100, screen.Draft.ToArray().Sum());
    }

    [Fact]
    public void TheLevySlider_SetsTheRateUnderThePointer()
    {
        // The same contract for the levy's slider (the governance block at A8, a taxation node known).
        UiSession known = Knowing(fx.Session, "taxation");
        UiSession s8 = UiSession.StartFrom(EraPreview.WorldAt(known.World, known.Config.Ages!, Me, 8), 42);
        EraTheme t8 = ThemeOf(s8, s8.World);
        (ActionSurfaceScreen screen, _) = Painted(s8, Surface(s8, Capital(s8.World), t8), t8);
        ActionHit track = Hit(screen, ActionHitKind.TaxTrack);
        double halfPoint = track.Rect.W / 100 / 2;

        double press = track.Rect.X + track.Rect.W * 0.052;
        screen.Click(press, track.Rect.CenterY);
        Assert.Equal(5, screen.TaxDraft);
        Assert.InRange(KnobX(screen, ActionHitKind.TaxTrack, 0) - press, -halfPoint, halfPoint);

        double drag = track.Rect.X + track.Rect.W * 0.948;
        Assert.True(screen.Drag(drag));
        Assert.Equal(95, screen.TaxDraft);
        Assert.InRange(KnobX(screen, ActionHitKind.TaxTrack, 0) - drag, -halfPoint, halfPoint);
        screen.Release();
        Assert.False(screen.Drag(track.Rect.X));
        Assert.Equal(95, screen.TaxDraft);
    }

    /// <summary>Repaints the surface and returns the x of the knob drawn on a slider track (the one circle centred
    /// on the track's line, within its span).</summary>
    private static double KnobX(ActionSurfaceScreen screen, ActionHitKind kind, int a)
    {
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 360);
        RectD tr = Hit(screen, kind, a).Rect;
        return d.Commands.OfType<CircleCmd>()
            .Single(c => Math.Abs(c.Cy - tr.CenterY) < 1e-9 && c.Cx >= tr.X - 8 && c.Cx <= tr.Right + 8).Cx;
    }

    // ------------------------------------------------------------------ the labour control

    [Fact]
    public void ThePebbleControl_KeepsTenPebbles_AndAppliesAtTheNextEndTurn()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int capital = Capital(s.World);
        var screen = new ActionSurfaceScreen { Theme = ThemeOf(s, s.World) };
        screen.Refresh(Surface(s, capital), s.World, s.Config, Me, id => s.Names.Name(id));
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        Assert.Equal(new[] { 6, 1, 1, 1, 1 }, screen.Draft.ToArray());   // 55/15/10/12/8 to the nearest ten pebbles
        Assert.False(screen.LabourChanged);

        ClickOn(screen, Hit(screen, ActionHitKind.LabourSlot, Sectors.Herding, 2));   // three pebbles to hunting & fishing
        Assert.Equal(10, screen.Draft.ToArray().Sum());
        Assert.Equal(3, screen.Draft[Sectors.Herding]);
        Assert.True(screen.LabourChanged);
        ClickOn(screen, Hit(screen, ActionHitKind.LabourSlot, Sectors.Herding, 2));   // clicking the last laid pebble lifts it
        Assert.Equal(2, screen.Draft[Sectors.Herding]);
        Assert.Equal(10, screen.Draft.ToArray().Sum());

        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        ActionCommand apply = ClickOn(screen, Hit(screen, ActionHitKind.LabourApply));
        int[] weights = apply.Weights.ToArray();
        Assert.Equal(100, weights.Sum());
        Assert.All(weights, w => Assert.Equal(0, w % 10));
        long turn = s.World.Clock.Turn;
        Assert.True(ActionDispatch.Apply(s, apply));
        Assert.All(s.QueuedOrders(), o => Assert.Equal(turn, o.Turn));   // stamped with the CURRENT turn

        // Queued, not yet applied: the world still runs the default; the surface says it is set.
        Assert.Equal(Sectors.Default(new SettlementId(capital)), LabourActivities.AllocationOf(s.World, new SettlementId(capital)));
        ActionSurfaceModel queued = Surface(s, capital);
        Assert.True(queued.Labour!.HasQueued);
        Assert.Equal(weights.Select(w => w / 10).ToArray(), queued.Labour.Queued.ToArray());

        // TURN-EXACT: the next End Turn applies it.
        s.EndTurn();
        SectorAllocationRow row = LabourActivities.AllocationOf(s.World, new SettlementId(capital));
        for (int sector = 0; sector < Sectors.Count; sector++) Assert.Equal(weights[sector] / 100.0, Sectors.Raw(row, sector));
        ActionSurfaceModel after = Surface(s, capital);
        Assert.False(after.Labour!.HasQueued);
        Assert.Equal(weights.Select(w => w / 10).ToArray(), after.Labour.Current.ToArray());
    }

    [Fact]
    public void UnitRounding_AndTheSummaryRanking_BreakTiesByTheLowestSector()
    {
        // TIE-DENSE: equal remainders go to the lowest sector index (largest remainder, stable integer tie-break).
        int[] units = new int[Sectors.Count];
        SectorAllocationModel.FromShares(new SectorAllocationRow(new SettlementId(0), 0.2, 0.2, 0.2, 0.2, 0.2), units, 10);
        Assert.Equal(new[] { 2, 2, 2, 2, 2 }, units);
        SectorAllocationModel.FromShares(new SectorAllocationRow(new SettlementId(0), 0.15, 0.15, 0.30, 0.25, 0.15), units, 10);
        Assert.Equal(new[] { 2, 2, 3, 2, 1 }, units);
        SectorAllocationModel.FromShares(new SectorAllocationRow(new SettlementId(0), 1, 1, 1, 1, 1), units, 20);
        Assert.Equal(new[] { 4, 4, 4, 4, 4 }, units);
        SectorAllocationModel.FromShares(new SectorAllocationRow(new SettlementId(0), 1, 1, 1, 0, 0), units, 10);
        Assert.Equal(new[] { 4, 3, 3, 0, 0 }, units);

        // The summary ranks sectors by (share DESC, sector ASC) — equal shares keep sector order.
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, ActionSurface.RankedSectors([0.2, 0.2, 0.2, 0.2, 0.2]));
        Assert.Equal(new[] { 2, 0, 3, 1, 4 }, ActionSurface.RankedSectors([0.25, 0.1, 0.3, 0.25, 0.1]));

        // A rebalance over ten units keeps the sum.
        int[] w = [6, 1, 1, 1, 1];
        SectorAllocationModel.Rebalance(w, Sectors.Construction, 5, 10);
        Assert.True(SectorAllocationModel.IsBalanced(w, 10));
        Assert.Equal(5, w[Sectors.Construction]);
    }

    // ------------------------------------------------------------------ only what the player commands

    [Fact]
    public void ASettlementThePlayerDoesNotCommand_IsNeverListed_TheControlShowsTheCapital()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        int capital = Capital(s.World);
        int foreign = -1;
        for (int i = 0; i < s.World.Settlements.Count; i++)
            if (!EmpireQuery.ControlsSettlement(s.World, Me, s.World.Settlements[i].Id)) foreign = s.World.Settlements[i].Id.Value;
        Assert.True(foreign >= 0);
        ActionSurfaceModel m = Surface(s, foreign);
        Assert.Equal(capital, m.Labour!.Settlement.Value);
        Assert.Contains("is not ours to command", m.Labour.Note);
        Assert.DoesNotContain(m.Labour.Controlled, c => c.Value == foreign);
        Assert.Equal(capital, m.Construction!.Settlement.Value);
        // And the labour dispatch for the foreign settlement is refused by the session's predicate.
        var command = new ActionCommand(ActionCommandKind.ApplyLabour, foreign, [20, 20, 20, 20, 20]);
        Assert.False(ActionDispatch.Apply(s, command));
        Assert.Equal(0, s.Orders.Count);
    }

    // ------------------------------------------------------------------ research

    [Fact]
    public void Research_ChooseThenClear_ThroughTheSurface()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int capital = Capital(s.World);
        ResearchContent research = s.Config.Research!;
        ResearchNodeId cordage = research.Nodes[research.IndexOfId("cordage")].Key;
        Assert.True(s.EmitResearchOrder(cordage));
        ResearchBlock chosen = Surface(s, capital).Research!;
        Assert.Equal("Cordage", chosen.Chosen!.Name);
        Assert.False(chosen.Idle);
        Assert.True(chosen.CanClear);
        s.EndTurn();
        ResearchBlock running = Surface(s, capital).Research!;
        Assert.Equal(cordage.Value, running.Target!.Key);
        Assert.True(running.Target.Progress > 0.0);
        ResearchFigure f = StatusFigures.Research(running);
        Assert.StartsWith("research: Cordage ", f.Text);
        Assert.EndsWith("/turn", f.Text);
        Assert.Contains("% +", f.Text);

        var screen = new ActionSurfaceScreen { Theme = ThemeOf(s, s.World) };
        screen.Refresh(Surface(s, capital), s.World, s.Config, Me, id => s.Names.Name(id));
        screen.Paint(new DrawList(), ApproxTextMeasure.Instance, 0, 0, 360);
        Assert.Equal(ActionCommandKind.OpenResearch, ClickOn(screen, Hit(screen, ActionHitKind.ResearchOpen)).Kind);
        ActionCommand stop = ClickOn(screen, Hit(screen, ActionHitKind.ResearchClear));
        Assert.Equal(ActionCommandKind.ClearResearch, stop.Kind);
        Assert.True(ActionDispatch.Apply(s, stop));
        Assert.Equal(ResearchQuery.ClearTargetOrder(s.World, Me), s.Orders[^1]);
        Assert.True(Surface(s, capital).Research!.ClearQueued);
        Assert.Equal("research stops at End Turn", StatusFigures.Research(Surface(s, capital).Research).Text);
        s.EndTurn();
        ResearchBlock idle = Surface(s, capital).Research!;
        Assert.True(idle.Idle);
        Assert.Contains(idle.Retained, r => r.Key == cordage.Value);   // retained progress is shown
    }

    // ------------------------------------------------------------------ read-only and deterministic

    [Fact]
    public void BuildingAndPaintingTheSurfaceInEveryEra_ChangesNothing()
    {
        UiSession s = fx.Session;
        string hash = WorldHash.ComputeHex(s.World);
        int orders = s.Orders.Count;
        foreach (UiEra era in UiEras.All)
        {
            EraTheme t = EraThemes.For(era);
            ActionSurfaceModel m = Surface(s, Capital(s.World), t);
            Painted(s, m, t);
        }
        Assert.Equal(hash, WorldHash.ComputeHex(s.World));
        Assert.Equal(orders, s.Orders.Count);
        // And the same inputs give the same model and the same painted surface, byte for byte.
        ActionSurfaceModel a = Surface(s, Capital(s.World)), b = Surface(s, Capital(s.World));
        Assert.True(AvailableActionsQuery.Same(a.Actions, b.Actions));
        EraTheme a1 = ThemeOf(s, s.World);
        Assert.Equal(SvgWriter.Write(Painted(s, a, a1).List, 400, 900, null), SvgWriter.Write(Painted(s, b, a1).List, 400, 900, null));
    }

    [Fact]
    public void ThePreview_IsTheRealSurface_AndByteIdenticalAcrossRuns()
    {
        var one = new ActionSurfacePreview.State("t", "turn 1", fx.Session, fx.Session.World, Capital(fx.Session.World));
        string a = ActionSurfacePreview.PanelSvg(one, null), b = ActionSurfacePreview.PanelSvg(one, null);
        Assert.Equal(a, b);
        Assert.Contains("Gathering", a);
        Assert.DoesNotContain("SAMPLE", a);       // not the era theme's illustrative list
        Assert.DoesNotContain("Farming", a);
    }
}
