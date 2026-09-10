using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.Observability.Explain;
using Sim.Core.State;
using Sim.Core.Systems.Consumption;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T4.19 lane B — the player information UI at the view-model seam. Every
/// panel body is a pure function of records and queries, so what a frame
/// would draw is built here headless and checked against the records it was
/// built from; the rendering hop (ImGui, GPU) is the one thing not covered.
///
/// Rigs: the D-025 dev preset through UiSession (256², N = 4, seed 42) —
/// the same session code the game runs, so a view model that only works on
/// a hand-built fixture cannot pass. The STARVED rig orders settlement 0 to
/// zero farming and zero herding at turn 0 (the ExplainRigs construction,
/// through the UI's own EmitSectorOrders) and steps until its deficit is
/// positive, then once more so the drawdown turn has a Next.
/// </summary>
public class GlassBoxUiTests
{
    private static Sim.Ui.UiSession Played(int turns)
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        for (int t = 0; t < turns; t++) session.EndTurn();
        return session;
    }

    /// <summary>Settlement 0 starved through the UI's order path; returns the
    /// session on the turn AFTER the first positive deficit.</summary>
    private static Sim.Ui.UiSession Starved(out int firstDeficitTurn)
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int target = session.World.Settlements[0].Id.Value;
        int[] weights = [0, 0, 45, 45, 10];
        Assert.True(session.EmitSectorOrders(weights, target));
        firstDeficitTurn = -1;
        for (int t = 0; t < 12 && firstDeficitTurn < 0; t++)
        {
            session.EndTurn();
            SettlementRecord? r = session.Observations.Settlement(session.Observations.LastTurn, target);
            if (r is not null && r.Food.DeficitRatio > 0.0) firstDeficitTurn = (int)session.Observations.LastTurn;
        }
        Assert.True(firstDeficitTurn > 0, "settlement 0 never showed a deficit in 12 turns");
        session.EndTurn();
        return session;
    }

    // ------------------------------------------------------------------
    // B1 — click-to-explain routing
    // ------------------------------------------------------------------

    [Fact]
    public void ExplainRouting_MapsEveryClickableFigure_ToASurface()
    {
        foreach (ExplainFigure figure in Enum.GetValues<ExplainFigure>())
        {
            ExplainRoute route = ExplainRouting.For(figure);
            Assert.NotEqual(Section.None, route.Section);
        }
        Assert.Equal(new ExplainRoute(Section.Turn, SettlementTab.Overview, AuditExpand.Population, false),
            ExplainRouting.For(ExplainFigure.WorldPopulation));
        Assert.Equal(new ExplainRoute(Section.Turn, SettlementTab.Overview, AuditExpand.Grain, false),
            ExplainRouting.For(ExplainFigure.WorldFood));
        Assert.Equal(new ExplainRoute(Section.Settlement, SettlementTab.Population, AuditExpand.None, false),
            ExplainRouting.For(ExplainFigure.SettlementPopulation));
        Assert.Equal(new ExplainRoute(Section.Settlement, SettlementTab.Food, AuditExpand.None, false),
            ExplainRouting.For(ExplainFigure.SettlementFood));
        Assert.Equal(new ExplainRoute(Section.Settlement, SettlementTab.Grievance, AuditExpand.None, true),
            ExplainRouting.For(ExplainFigure.SettlementHappiness));
        Assert.Equal(new ExplainRoute(Section.Settlement, SettlementTab.Grievance, AuditExpand.None, false),
            ExplainRouting.For(ExplainFigure.SettlementGrievance));
    }

    // ------------------------------------------------------------------
    // B3 — the turn audit
    // ------------------------------------------------------------------

    private static TurnRecord Turn(
        long popOpen, long births, long natural, long starvation, long popClose, bool popReconciles, long popDisc,
        long grainOpen, long harvest, long eaten, long spoilage, long overflow, long grainClose)
    {
        var population = new PopulationAccount(popOpen, births, natural, starvation, popClose, popReconciles, popDisc);
        var grain = new GrainAccount(grainOpen, 0, harvest, eaten, spoilage, overflow, grainClose, true, 0);
        var dwellings = new DwellingsAccount(100, 7, 2, 105, true, 0);
        var flows = new FlowsSummary(37, 1, 0, 12, 3, false);
        var causes = new CausesRecord(popClose - popOpen, births - natural - starvation,
            grainClose - grainOpen, harvest - eaten - spoilage - overflow);
        return new TurnRecord(24, 230.0, 10.0, [], population, grain, dwellings,
            [new GoodAccount(5, "pottery", 120, 30, 0, 0, 0, 5, 0, 145, true, 0)],
            flows, [], [], causes);
    }

    private static SettlementRecord Record(int id, long popOpen, long popClose, double deficit, bool founded = false)
    {
        var population = new PopulationSection(popOpen, popClose, 0, popClose, 0, 0, 3, 1, 0, 0, 0, "identity", []);
        var food = new FoodSection(100, 90, 50, 60, 0, "identity", 70, deficit, [], 60);
        var housing = new HousingSection(true, 10, 10, 60, 10, 1.0, 1.0, 0.0, "GAP");
        var economy = new EconomySection([], [], [], [0.55, 0.15, 0.10, 0.12, 0.08], false, double.NaN, double.NaN, double.NaN, []);
        var social = new SocialSection(80.0, [0.9, 1.0], [], []);
        var migration = new MigrationSection(0.0, double.NaN, [], 100, 50, 0.0, 0.0, "GAP");
        var policy = new PolicySection([0.55, 0.15, 0.10, 0.12, 0.08], false, [0.55, 0.15, 0.10, 0.12, 0.08]);
        return new SettlementRecord(id, 0, -1, founded, population, food, housing, economy, social, migration, policy, []);
    }

    [Fact]
    public void TurnAudit_WhatChangedLines_ReproduceTheRecordNumerically()
    {
        TurnRecord turn = Turn(
            popOpen: 4048, births: 120, natural: 300, starvation: 109, popClose: 3759, popReconciles: true, popDisc: 0,
            grainOpen: 82041, harvest: 38391, eaten: 24029, spoilage: 13869, overflow: 0, grainClose: 82534);

        IReadOnlyList<AuditLine> lines = TurnAuditModel.ChangedLines(turn, [Record(0, 10, 12, 0.0)], id => "S" + id);
        Assert.Equal(7, lines.Count);

        AuditLine pop = lines[0];
        Assert.Equal(TurnAuditModel.PopulationKey, pop.Key);
        Assert.Equal("population 4,048 -> 3,759  (-289)", pop.Line);
        Assert.Equal(
        [
            "  births          +120",
            "  natural deaths  -300",
            "  starvation      -109",
            "  reconciles to the unit",
        ], pop.Detail);

        AuditLine grain = lines[1];
        Assert.Equal("grain 82,041 -> 82,534  (+493)", grain.Line);
        Assert.Equal(
        [
            "  harvest         +38,391",
            "  eaten           -24,029",
            "  spoilage        -13,869",
            "  overflow        0",
            "  reconciles to the unit",
        ], grain.Detail);

        Assert.Equal("dwellings 100 -> 105  (+5)", lines[2].Line);
        Assert.Equal("  built           +7", lines[2].Detail[0]);
        Assert.Equal("  decayed         -2", lines[2].Detail[1]);
        Assert.Equal("migrants moved 37", lines[3].Line);
        Assert.Equal("settlements founded 1", lines[4].Line);
        Assert.Equal("control lost 0", lines[5].Line);
        Assert.Equal("trade units 12", lines[6].Line);
        Assert.Equal("  3 flow row(s); grain never trades (numeraire)", lines[6].Detail[0]);

        // The digest is the same record, three figures.
        Assert.Equal("pop -289 · food +493 · 0 in deficit", TurnAuditModel.Digest(turn, [Record(0, 10, 12, 0.0)]));
        Assert.Equal("pop -289 · food +493 · 2 in deficit",
            TurnAuditModel.Digest(turn, [Record(0, 10, 12, 0.3), Record(1, 10, 12, 0.0), Record(2, 10, 12, 0.01)]));
    }

    [Fact]
    public void TurnAudit_WhyLines_StateTheIdentityExactly_AndTheReconcileFlagPlainly()
    {
        TurnRecord good = Turn(4048, 120, 300, 109, 3759, true, 0, 100, 10, 5, 1, 0, 104);
        IReadOnlyList<string> why = TurnAuditModel.WhyLines(good);
        Assert.Equal("population delta -289 = births 120 - natural deaths 300 - starvation 109 = -289", why[0]);
        Assert.Equal("  reconciles to the unit", why[1]);
        Assert.Equal("grain delta +4 = endowment 0 + harvest 10 - eaten 5 - spoilage 1 - overflow 0 = +4", why[2]);

        // A stock that moved without a ledger row: the flag says DEFECT with
        // the integer, never a rounded or softened word.
        TurnRecord bad = Turn(4048, 120, 300, 109, 3760, false, 1, 100, 10, 5, 1, 0, 104);
        IReadOnlyList<string> whyBad = TurnAuditModel.WhyLines(bad);
        Assert.Equal("  DISCREPANCY +1 - simulation defect", whyBad[1]);
        Assert.Contains("DISCREPANCY +1 - simulation defect", TurnAuditModel.ChangedLines(bad, [], _ => "")[0].Detail[3]);
    }

    [Fact]
    public void TurnAudit_WhereRows_SortByMagnitudeDescThenIdAsc_TieDense()
    {
        // Six settlements, THREE sharing |delta| = 40 (two losing, one gaining)
        // and two sharing |delta| = 5, inserted in DESCENDING id order so table
        // order and the required order disagree everywhere a tie exists.
        SettlementRecord[] rows =
        [
            Record(9, 100, 60, 0.0),     // -40
            Record(7, 100, 105, 0.0),    // +5
            Record(5, 100, 140, 0.0),    // +40
            Record(3, 100, 60, 0.0),     // -40
            Record(2, 100, 95, 0.0),     // -5
            Record(1, 100, 100, 0.0),    // 0
        ];
        IReadOnlyList<WhereRow> where = TurnAuditModel.LargestPopulationDelta(rows, id => "S" + id, top: 10);
        Assert.Equal([3, 5, 9, 2, 7, 1], where.Select(w => w.Settlement).ToArray());
        Assert.StartsWith("S3  pop -40", where[0].Line);
        Assert.StartsWith("S5  pop +40", where[1].Line);

        // The cap keeps the head of the SAME order.
        IReadOnlyList<WhereRow> top = TurnAuditModel.LargestPopulationDelta(rows, id => "S" + id, top: 2);
        Assert.Equal([3, 5], top.Select(w => w.Settlement).ToArray());

        // Deficit: bit-equal ratios tie, lowest id first; zero deficits last.
        SettlementRecord[] deficits =
        [
            Record(8, 1, 1, 0.25), Record(6, 1, 1, 0.5), Record(4, 1, 1, 0.5), Record(2, 1, 1, 0.0), Record(0, 1, 1, 0.5),
        ];
        IReadOnlyList<WhereRow> worst = TurnAuditModel.LargestDeficit(deficits, id => "S" + id, top: 10);
        Assert.Equal([0, 4, 6, 8, 2], worst.Select(w => w.Settlement).ToArray());
        Assert.Equal(4, TurnAuditModel.InDeficit(deficits));

        // The predicate itself, both halves.
        var a = new WhereRow(4, "", 1.0);
        var b = new WhereRow(2, "", 1.0);
        Assert.False(TurnAuditModel.Before(a, b));
        Assert.True(TurnAuditModel.Before(b, a));
        Assert.True(TurnAuditModel.Before(new WhereRow(9, "", 2.0), b));
    }

    [Fact]
    public void TurnAudit_OnThePlayedSession_DigestAndWhereReadTheLatestRecord()
    {
        Sim.Ui.UiSession session = Played(4);
        TurnAuditView? audit = ScreenModels.TurnAudit(session);
        Assert.NotNull(audit);
        TurnObservation latest = session.Observations.Observations[^1];
        Assert.Equal(TurnAuditModel.Digest(latest.Turn, latest.Settlements), audit.Digest);
        Assert.StartsWith("turn 4  year", audit.HeaderLine);
        Assert.Equal(4, audit.LargestPopulationDelta.Count);   // four settlements, all ranked
        Assert.Equal(4, audit.LargestDeficit.Count);
        Assert.Equal(latest.Turn.Goods.Length + 1, audit.GoodAccounts.Count);
        // A reconciling world says so on every account it audits.
        Assert.All(audit.Changed.Take(3), line => Assert.Contains("reconciles to the unit", line.Detail[^1]));
        Assert.All(audit.GoodAccounts.Skip(1), line => Assert.EndsWith("ok", line));
        // Every WHERE row names a settlement that exists in the world.
        foreach (WhereRow row in audit.LargestPopulationDelta)
        {
            bool exists = false;
            for (int i = 0; i < session.World.Settlements.Count; i++)
                if (session.World.Settlements[i].Id.Value == row.Settlement) exists = true;
            Assert.True(exists, $"WHERE row names settlement {row.Settlement}, not in the world");
        }
        // Before the first End Turn there is no audit and the digest is absent.
        Assert.Null(ScreenModels.TurnAudit(Sim.Ui.UiSession.Start(42, 256, 4)));
    }

    // ------------------------------------------------------------------
    // A6-A9, B6 — the grievance tab
    // ------------------------------------------------------------------

    private static NeedComponent Need(int id, double lift, bool bound = true) =>
        new(id, "need" + id, bound, false, 1.0, 0.5, 0.5, lift, bound ? "" : "not yet simulated");

    [Fact]
    public void Grievance_Rank_PutsTheHighestLiftFirst_AndBreaksTiesByLowestId_TieDense()
    {
        // Every bound need equally lifting, inserted in DESCENDING id order:
        // the ranking must come out ascending by id, and an unbound need must
        // not appear at all. Then a higher lift beats a lower id.
        NeedComponent[] tied = [Need(6, 0.25), Need(2, 0.25), Need(3, 0.0, bound: false), Need(1, 0.25)];
        Assert.Equal([1, 2, 6], GrievanceViewModel.Rank(tied).Select(n => n.NeedId).ToArray());

        NeedComponent[] mixed = [Need(1, 0.1), Need(2, 0.7), Need(6, 0.7)];
        Assert.Equal([2, 6, 1], GrievanceViewModel.Rank(mixed).Select(n => n.NeedId).ToArray());

        Assert.False(GrievanceViewModel.Before(Need(6, 0.25), Need(2, 0.25)));
        Assert.True(GrievanceViewModel.Before(Need(2, 0.25), Need(6, 0.25)));
        Assert.True(GrievanceViewModel.Before(Need(6, 0.3), Need(2, 0.25)));
    }

    [Fact]
    public void Grievance_AGapLinkRendersAsNotRecorded_AndAReadLinkCitesItsRow()
    {
        var gap = new Link(ChainNode.ToolFactor, "tool factor", double.NaN, LinkKind.Gap,
            SourceWorld.None, "", -1, "computed inside production (ProductionSystem.cs:221)");
        ChainLine gapLine = GrievanceViewModel.LinkLine(gap);
        Assert.True(gapLine.IsGap);
        Assert.Equal("  tool factor: not recorded: computed inside production (ProductionSystem.cs:221)", gapLine.Text);
        Assert.DoesNotContain("NaN", gapLine.Text);

        var read = new Link(ChainNode.GrainHarvest, "grain harvest", 436, LinkKind.Read, SourceWorld.Prev, "GoodStocks", 7);
        ChainLine readLine = GrievanceViewModel.LinkLine(read);
        Assert.False(readLine.IsGap);
        Assert.Equal("  grain harvest  436  (read, prev GoodStocks[7])", readLine.Text);

        // Levers: a sector lever names its sectors; a None lever names its reason and no button.
        LeverLine farming = GrievanceViewModel.LeverFor(ChainNode.FarmingShare);
        Assert.False(farming.IsNone);
        Assert.Equal([Sectors.Farming], farming.Sectors);
        Assert.StartsWith("lever: labour allocation - farming", farming.Text);
        LeverLine weather = GrievanceViewModel.LeverFor(ChainNode.HarvestWeather);
        Assert.True(weather.IsNone);
        Assert.Empty(weather.Sectors);
        Assert.StartsWith("lever: none - ", weather.Text);
    }

    [Fact]
    public void Grievance_OnTheStarvedSession_PrimaryIsSustenanceAndFirst_ChainCarriesGaps_LeverIsFarming()
    {
        Sim.Ui.UiSession session = Starved(out int firstDeficit);
        int target = session.World.Settlements[0].Id.Value;
        SettlementView? view = ScreenModels.Settlement(session, target);
        Assert.NotNull(view);
        GrievanceView? grievance = view.Grievance;
        Assert.NotNull(grievance);

        // Cross-check against the query the view was built from.
        var explanation = GrievanceExplanation.For(
            session.PreviousWorld!, session.World, session.Config, new SettlementId(target), new ClassId(grievance.Classes[0].ClassId));
        Assert.Equal(BasketBook.SustenanceNeedId, explanation.PrimaryNeedId);

        ClassGrievanceBlock peasants = grievance.Classes[0];
        Assert.NotEmpty(peasants.Contributors);
        Assert.True(peasants.Contributors[0].IsPrimary);
        Assert.Equal(explanation.PrimaryNeedId, peasants.Contributors[0].NeedId);
        Assert.StartsWith("PRIMARY Sustenance", peasants.Contributors[0].Line);
        Assert.Single(peasants.Contributors, c => c.IsPrimary);
        Assert.Contains("primary grievance: Sustenance", peasants.PrimaryLine);
        Assert.Contains("reproduces exactly", peasants.AccrualLine);
        Assert.Equal(3, peasants.Contributors.Count);          // the three bound needs
        Assert.Equal(5, peasants.NotSimulated.Count);          // the five unbound, listed not omitted
        Assert.All(peasants.NotSimulated, line => Assert.Contains("not yet simulated", line));

        // The Sustenance chain: the ordered zero farming read back, the tool
        // factor / binding / imports GAPs worded "not recorded", the lever farming.
        ContributorRow primary = peasants.Contributors[0];
        Assert.Contains(primary.Chain, l => l.IsGap && l.Text.Contains("not recorded:"));
        Assert.Contains(primary.Chain, l => l.Text.Contains("farming share") && l.Text.Contains("  0  "));
        Assert.False(primary.Lever.IsNone);
        Assert.Contains(Sectors.Farming, primary.Lever.Sectors);
        Assert.Contains(Sectors.Herding, primary.Lever.Sectors);

        // Happiness sits at the top with both factors and their chains.
        Assert.StartsWith("happiness ", grievance.HappinessLine);
        Assert.Equal(2, grievance.Factors.Count);
        Assert.StartsWith("Food ", grievance.Factors[0].Line);
        Assert.NotEmpty(grievance.Factors[0].Chain);
        Assert.Equal(HappinessExplanation.ScopeNote, grievance.ScopeNote);

        // Migration on the same turn: famine flight is visible as a push, and
        // every GAP field is worded, not numbered.
        Assert.Contains(view.Migration, l => l.StartsWith("push: source deficit 0.") && !l.Contains("0.000"));
        Assert.Contains(view.Migration, l => l.StartsWith("pairwise flows: not recorded: "));
        Assert.DoesNotContain(view.Migration, l => l.Contains("GAP:"));
        Assert.Contains(view.Food, l => l.StartsWith("  built vs decayed: not recorded: "));
        Assert.DoesNotContain(view.Food, l => l.Contains("GAP:"));
        Assert.Contains(view.Migration, l => l.StartsWith("other destinations"));
        Assert.True(firstDeficit >= 2);
    }

    // ------------------------------------------------------------------
    // B5 — policy history
    // ------------------------------------------------------------------

    [Fact]
    public void PolicyHistory_RendersAChangeWithOldNewActorAndOrderIndex()
    {
        var change = new PolicyChange(24, 230.0, 3, Sectors.Farming, 0.55, 0.73, LaborOrderFactory.UiActorId, 104);
        Assert.Equal("turn 24 · farming 55% -> 73% · player · order #104", PolicyHistoryModel.ChangeLine(change));
        var orphan = new PolicyChange(5, 40.0, 3, Sectors.Crafting, 0.12, 0.0, -1, -1);
        Assert.Equal("turn 5 · crafting 12% -> 0% · unattributed · no order", PolicyHistoryModel.ChangeLine(orphan));
        Assert.Equal("actor 7", PolicyHistoryModel.ActorName(7));
        Assert.Single(PolicyHistoryModel.Policies);
    }

    [Fact]
    public void PolicyHistory_OnThePlayedSession_ListsTheChangeNewestFirst_WithObservedConsequences()
    {
        var session = Sim.Ui.UiSession.Start(42, 256, 4);
        int target = session.World.Settlements[1].Id.Value;
        for (int t = 0; t < 2; t++) session.EndTurn();
        Assert.True(session.EmitSectorOrders([70, 10, 10, 5, 5], target));   // stamped turn 2 → row on turn 3
        for (int t = 0; t < 4; t++) session.EndTurn();

        PolicyView view = ScreenModels.Policy(session, target);
        Assert.NotEmpty(view.History);
        // Every change of this batch is on turn 3; the farming one reads exactly.
        Assert.All(view.History, h => Assert.Equal(3, h.Turn));
        Assert.Contains(view.History, h => h.Line.StartsWith("turn 3 · farming 55% -> 70% · player · order #"));
        // Consequences: turns 4, 5, 6 observed, labelled as observation.
        PolicyChangeView farming = view.History.First(h => h.Line.Contains("farming"));
        Assert.Contains("observed on the turns after, not attributed", farming.Consequences[0]);
        Assert.Equal(4, farming.Consequences.Count);   // label + turns 4, 5, 6
        Assert.StartsWith("    turn 4  pop ", farming.Consequences[1]);
        // CURRENT: declared 70 beside effective 70 (the split sums to 100).
        Assert.Contains(view.Current, l => l.Contains("farming") && l.Contains("declared  70%") && l.Contains("effective  70%"));
        Assert.Contains(view.Current, l => l.Contains("in force on turn 6"));
        // The per-turn table is newest first and covers every observed turn.
        Assert.StartsWith("   6   70/10/10/5/5", view.States[1]);
        Assert.Equal(1 + 6, view.States.Count);
        // A settlement never ordered has no history and the default current split.
        PolicyView untouched = ScreenModels.Policy(session, session.World.Settlements[0].Id.Value);
        Assert.Empty(untouched.History);
        Assert.Contains(untouched.Current, l => l.Contains("farming") && l.Contains("declared  55%"));
    }

    // ------------------------------------------------------------------
    // B2 — trends
    // ------------------------------------------------------------------

    [Fact]
    public void Trends_ReturnASeriesPerSeriesKey_WithOneValuePerObservedTurn()
    {
        Sim.Ui.UiSession session = Played(6);
        IObservationHistory history = session.Observations;
        int first = session.World.Settlements[0].Id.Value;
        Sim.Core.Systems.ClassEntry[] classes = session.Config.Registries.Classes;
        IReadOnlyList<TrendMetric> metrics = TrendsModel.Metrics(classes);

        foreach (SeriesKey key in Enum.GetValues<SeriesKey>())
        {
            Assert.Contains(metrics, m => !m.IsPrice && m.Key == key);
            int classId = key == SeriesKey.Grievance ? classes[0].Id : -1;
            double[] settlement = TrendsModel.Settlement(history, first, key, classId);
            double[] world = TrendsModel.World(history, key, classId);
            Assert.Equal(6, settlement.Length);
            Assert.Equal(6, world.Length);
            Assert.All(settlement, v => Assert.False(double.IsNaN(v)));
        }
        Assert.Contains(metrics, m => m.IsPrice);
        Assert.Equal(classes.Length, metrics.Count(m => m.Key == SeriesKey.Grievance && !m.IsPrice));

        // World population through the seam equals the TurnRecord's closing
        // population on every turn — the sum is the ledger carrier's sum.
        double[] pop = TrendsModel.World(history, SeriesKey.Population, -1);
        for (int t = 0; t < 6; t++)
            Assert.Equal(history.Observations[t].Turn.Population.Closing, pop[t]);
        // An intensive key at world scope is a mean, and says so.
        Assert.Contains("mean", TrendsModel.ScopeNote(SeriesKey.Happiness, true));
        Assert.Contains("sum", TrendsModel.ScopeNote(SeriesKey.Births, true));
        double[] happiness = TrendsModel.World(history, SeriesKey.Happiness, -1);
        Assert.All(happiness, h => Assert.InRange(h, 0.0, 100.0));
        // The plot boundary maps NaN to 0 and keeps finite values.
        Assert.Equal([1f, 0f, 2.5f], TrendsModel.ForPlot([1.0, double.NaN, 2.5]));
    }

    // ------------------------------------------------------------------
    // WHERE → click → centre
    // ------------------------------------------------------------------

    [Fact]
    public void CameraFocus_LandsTheSettlementAtTheViewportCentre_OrClampedAtTheWorldsEdge()
    {
        WorldState world = Sim.Ui.UiSession.Start(42, 256, 4).World;
        const int vw = 512, vh = 384;
        int size = world.Terrain!.Size;

        // Zoomed in far enough that the world is wider than the viewport on
        // both axes: an interior settlement lands EXACTLY at the centre; one
        // within half a viewport of the edge stops at the clamp bound.
        int centred = 0, clamped = 0;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            var cam = new Camera(size);
            cam.ZoomAt(0, 0, Camera.MaxZoom, vw, vh);
            Assert.Equal(Camera.MaxZoom, cam.Zoom);
            double halfW = vw / 2.0 / cam.Zoom, halfH = vh / 2.0 / cam.Zoom;

            int id = world.Settlements[i].Id.Value;
            Assert.True(CameraFocus.CenterOn(cam, world, id, vw, vh));
            LineGeometry.Vertex pos = OverlayMeshes.SettlementPosition(world.Settlements[i], size);
            (double sx, double sy) = cam.WorldToScreen(pos.X, pos.Y, vw, vh);

            bool interiorX = pos.X >= halfW && pos.X <= size - halfW;
            bool interiorY = pos.Y >= halfH && pos.Y <= size - halfH;
            if (interiorX) Assert.Equal(vw / 2.0, sx, 9); else Assert.True(cam.CenterX == halfW || cam.CenterX == size - halfW);
            if (interiorY) Assert.Equal(vh / 2.0, sy, 9); else Assert.True(cam.CenterY == halfH || cam.CenterY == size - halfH);
            if (interiorX && interiorY) centred++; else clamped++;
        }
        Assert.True(centred >= 1, "no settlement of the dev world is interior at max zoom");

        // At the world-fit zoom the whole world is on screen and the camera
        // locks to the world centre: centring cannot move it, and the
        // settlement's screen position is its world offset from the centre.
        var fit = new Camera(size);
        fit.Clamp(vw, vh);
        Assert.True(CameraFocus.CenterOn(fit, world, world.Settlements[0].Id.Value, vw, vh));
        Assert.Equal(size / 2.0, fit.CenterX);
        Assert.Equal(size / 2.0, fit.CenterY);
        clamped++;
        Assert.True(clamped >= 1);

        // An unknown id leaves the camera untouched and says so.
        var untouched = new Camera(size);
        untouched.ZoomAt(0, 0, 4.0, vw, vh);
        double cx = untouched.CenterX, cy = untouched.CenterY;
        Assert.False(CameraFocus.CenterOn(untouched, world, 999, vw, vh));
        Assert.Equal(cx, untouched.CenterX);
        Assert.Equal(cy, untouched.CenterY);
    }

    // ------------------------------------------------------------------
    // the fence — looking changes nothing
    // ------------------------------------------------------------------

    [Fact]
    public void T419_OpeningEverySectionAndTab_AndBuildingEveryViewModelFiveTimes_MutatesNoSimulationState()
    {
        Sim.Ui.UiSession session = Starved(out _);
        string before = WorldHash.ComputeHex(session.World);
        string prevBefore = WorldHash.ComputeHex(session.PreviousWorld!);
        int ordersBefore = session.Orders.Count;
        int observationsBefore = session.Observations.Observations.Count;
        int selected = session.World.Settlements[0].Id.Value;

        Section open = Section.None;
        for (int frame = 0; frame < 5; frame++)
        {
            foreach (Section section in GameSections.Order)
            {
                open = GameSections.Toggle(open, section);
                foreach (SettlementTab tab in GameSections.Tabs) _ = GameSections.TabLabel(tab);
                open = GameSections.Toggle(open, section);
            }
            foreach (ExplainFigure figure in Enum.GetValues<ExplainFigure>()) _ = ExplainRouting.For(figure);

            _ = HudModel.From(session.World, selected, null, session.Names.Name(selected), session.Config);
            _ = ScreenModels.TurnAudit(session);
            SettlementView? view = ScreenModels.Settlement(session, selected);
            Assert.NotNull(view?.Grievance);
            _ = ScreenModels.Policy(session, selected);
            foreach (TrendMetric metric in TrendsModel.Metrics(session.Config.Registries.Classes))
            {
                if (metric.IsPrice) continue;
                _ = TrendsModel.World(session.Observations, metric.Key, metric.ClassId);
                _ = TrendsModel.Settlement(session.Observations, selected, metric.Key, metric.ClassId);
            }
            _ = SessionFilesModel.Lines("runs/orders-x.bin");
        }

        Assert.Equal(Section.None, open);
        Assert.Equal(before, WorldHash.ComputeHex(session.World));
        Assert.Equal(prevBefore, WorldHash.ComputeHex(session.PreviousWorld!));
        Assert.Equal(ordersBefore, session.Orders.Count);
        Assert.Equal(observationsBefore, session.Observations.Observations.Count);
    }

    // ------------------------------------------------------------------
    // the chrome's new figures
    // ------------------------------------------------------------------

    [Fact]
    public void HudModel_WorldFoodIsTheGrainCarrierSum_AndHappinessIsTheReaderOrADash()
    {
        Sim.Ui.UiSession session = Played(2);
        WorldState world = session.World;
        int first = world.Settlements[0].Id.Value;

        long grain = 0;
        for (int i = 0; i < world.GoodStocks.Count; i++)
            if (world.GoodStocks[i].Good == UiGoods.Grain) grain += world.GoodStocks[i].Amount.Value;
        HudModel withCfg = HudModel.From(world, first, null, null, session.Config);
        Assert.Equal(grain, withCfg.WorldFood);
        Assert.Equal("food " + grain.ToString(System.Globalization.CultureInfo.InvariantCulture), withCfg.WorldFoodFigure);
        Assert.Equal(session.Observations.Observations[^1].Turn.Grain.Closing, withCfg.WorldFood);
        Assert.Equal(SettlementHappiness.Of(world, new SettlementId(first), session.Config), withCfg.Happiness);
        Assert.StartsWith("happiness ", withCfg.HappinessLine);
        Assert.NotEqual("happiness —", withCfg.HappinessLine);

        HudModel withoutCfg = HudModel.From(world, first);
        Assert.Equal("happiness —", withoutCfg.HappinessLine);
        Assert.Equal("world pop " + withCfg.WorldPopulation.ToString(System.Globalization.CultureInfo.InvariantCulture),
            withCfg.WorldPopulationFigure);
    }

    [Fact]
    public void SessionFiles_ListTheFiveFilesOfTheSession()
    {
        IReadOnlyList<string> lines = SessionFilesModel.Lines(Path.Combine("runs", "orders-20260909-120000-s256-n4.bin"));
        Assert.Contains(lines, l => l.Contains("session-20260909-120000-s256-n4.json"));
        Assert.Contains(lines, l => l.Contains("orders-20260909-120000-s256-n4.bin"));
        Assert.Contains(lines, l => l.Contains("chronicle-20260909-120000-s256-n4.txt"));
        Assert.Contains(lines, l => l.Contains("trace-20260909-120000-s256-n4.csv"));
        Assert.Contains(lines, l => l.Contains("telemetry-20260909-120000-s256-n4.jsonl"));
    }

    [Fact]
    public void SettlementTabs_OnThePlayedSession_CarryTheRecordsIdentities()
    {
        Sim.Ui.UiSession session = Played(3);
        int first = session.World.Settlements[0].Id.Value;
        SettlementView? view = ScreenModels.Settlement(session, first);
        Assert.NotNull(view);
        SettlementRecord r = session.Observations.Settlement(3, first)!;

        Assert.Contains(view.Population, l => l == "  " + r.Population.ColonistsDepartedIdentity);
        Assert.Contains(view.Population, l => l.Contains("UNSPLIT"));
        Assert.Contains(view.Food, l => l == "  " + r.Food.StoreLossesIdentity);
        Assert.StartsWith(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"grain {r.Food.GrainOpening:#,0} + harvest {r.Food.Harvest:#,0} - eaten {r.Food.Eaten:#,0} - store losses {r.Food.StoreLosses:#,0} = {r.Food.GrainClosing:#,0}"),
            view.Food[0]);
        Assert.Contains(view.Food, l => l.StartsWith("  built vs decayed: not recorded: "));
        Assert.Single(view.Orders);   // "no orders applied ..."
        Assert.StartsWith("no orders applied", view.Orders[0]);
        // A settlement id the world does not have has no record and no view.
        Assert.Null(ScreenModels.Settlement(session, 999));
    }
}
