using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Actions;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-054 WS1 AT THE UI — the queue as rows: one painted row per entry, the head's progress in marks and "N of M" figures
/// (never a percentage), its status from ConstructionQuery.HeadStatus (the resolver ConstructionSystem uses — an estimate),
/// a Cancel on every COMMITTED entry (an entry ordered this turn has none), a two-step "Lose it" confirm for an entry
/// something has gone into (the forfeit stated beside it; no refund), and the pending book (QueueWithPending / PendingBook)
/// the order factory and the listing both judge against — so a pending cancel frees its entry's claims for both.
/// </summary>
public class IncrementalConstructionUiTests(CanonicalTurnOneFixture fx) : IClassFixture<CanonicalTurnOneFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const int Granary = 1, Workshop = 2;
    private const long Part = 400_000;   // 16 of 40 timber, 8 of 20 stone, 0.8 of 2 adult-years

    private static void Set(WorldState w, SettlementId s, int good, long qty)
    {
        var id = new GoodId(good);
        int idx = GoodStockIndex.IndexOf(w.GoodStocks, s, id);
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));
        long have = w.GoodStocks[idx].Amount.Value;
        if (have == qty) return;
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id), ReasonIds.InitialEndowment,
            Math.Abs(qty - have), qty > have ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
    }

    /// <summary>A committed entry at <paramref name="s"/>'s next slot with its WS1 progress row (as the intake writes it).</summary>
    private static int Queue(WorldState w, SettlementId s, int project, long progress)
    {
        int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, project));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, project, Math.Max(0, w.Clock.Turn - 1), progress));
        return slot;
    }

    private (UiSession S, SettlementId Capital) From(Action<WorldState, SettlementId> rig)
    {
        WorldState w = fx.Session.World.Clone();
        Assert.True(EmpireQuery.TryGetCapital(w, Me, out SettlementId capital));
        rig(w, capital);
        return (UiSession.StartFrom(w, fx.Session.World.Seed), capital);
    }

    private static (ActionSurfaceScreen Screen, ActionSurfaceModel Model, List<TextCmd> Texts, string All) Paint(
        UiSession s, SettlementId at, ActionSurfaceScreen? screen = null)
    {
        EraTheme th = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
        ActionSurfaceModel model = ActionSurface.ForSession(s, UiSession.ProductionEra(), at.Value, th);
        screen ??= new ActionSurfaceScreen { Theme = th };
        screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
        List<TextCmd> texts = d.Commands.OfType<TextCmd>().ToList();
        return (screen, model, texts, string.Join(" ", texts.Select(t => t.Text)));
    }

    [Fact]
    public void APartBuiltGranary_OnTheA1Surface_IsOneRowWithFiguresAndACancel_AndNoPercentAnywhere()
    {
        (UiSession s, SettlementId capital) = From((w, c) => Queue(w, c, Granary, Part));
        Assert.Equal(UiEra.Prehistoric, UiEras.Of(s.World, s.Config.Ages, Me));   // A1
        (ActionSurfaceScreen screen, ActionSurfaceModel model, List<TextCmd> texts, string all) = Paint(s, capital);

        ConstructionBlock c = model.Construction!;
        Assert.True(c.Incremental);
        QueueEntry head = Assert.Single(c.Queue);
        Assert.Equal((Part, true, true, false, 1), (head.Progress, head.Head, head.Committed, head.CancelPending, head.Position));
        Assert.Equal("16 of 40 timber, 8 of 20 stone and 0.8 of 2 adult-years have gone in", head.GoneIn);
        Assert.Equal("16 timber, 8 stone, 0.8 adult-years", head.Forfeit);
        Assert.NotNull(head.Cancel);
        Assert.Equal(OrderKind.CancelConstruction, head.Cancel!.Order);
        Assert.Contains("16 of 40 timber, 8 of 20 stone and 0.8 of 2 adult-years have gone in", all, StringComparison.Ordinal);

        // No percentage in any figure on the surface.
        Assert.DoesNotContain(texts, t => t.Text.Contains('%', StringComparison.Ordinal));
        // Only the A1 hit kinds: no slider track (sliders come at A7), nothing armed yet — and the entry's Cancel.
        ActionHitKind[] allowed =
        [
            ActionHitKind.LabourSlot, ActionHitKind.LabourMinus, ActionHitKind.LabourPlus, ActionHitKind.LabourApply,
            ActionHitKind.LabourReset, ActionHitKind.ResearchOpen, ActionHitKind.ResearchClear, ActionHitKind.AgeAdvance,
            ActionHitKind.Build, ActionHitKind.RoadSlot, ActionHitKind.RoadApply, ActionHitKind.TaxSlot, ActionHitKind.TaxApply,
            ActionHitKind.PrevSettlement, ActionHitKind.NextSettlement, ActionHitKind.FoundSettlement, ActionHitKind.CancelConstruction,
        ];
        foreach (ActionHit h in screen.Hits) Assert.Contains(h.Kind, allowed);
        ActionHit cancel = Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.CancelConstruction);
        Assert.Equal((capital.Value, 0), (cancel.A, cancel.B));
    }

    /// <summary>
    /// C1 review F9(a) — "floor it and cap it below full until completion": the head's progress marks never all fill before
    /// the project is complete. The rule (EraMarks.MarksFilled with fullOnlyAtOne), and on the A1 surface itself (ten carved
    /// tally strokes): a head one progress unit short of N paints nine filled strokes, eight more than a head one unit in.
    /// Rounded (the general mark rule, kept for research and the rest), 95 % of ten notches would paint all ten.
    /// </summary>
    [Fact]
    public void TheHeadsProgressMarks_AreFlooredAndNeverAllFilledBeforeCompletion()
    {
        Assert.Equal(10, EraMarks.MarksFilled(0.95, 10));
        Assert.Equal(9, EraMarks.MarksFilled(0.95, 10, fullOnlyAtOne: true));
        Assert.Equal(9, EraMarks.MarksFilled(0.999999, 10, fullOnlyAtOne: true));
        Assert.Equal(10, EraMarks.MarksFilled(1.0, 10, fullOnlyAtOne: true));
        Assert.Equal(4, EraMarks.MarksFilled(0.49, 10, fullOnlyAtOne: true));
        Assert.Equal(1, EraMarks.MarksFilled(0.000001, 10, fullOnlyAtOne: true));   // started: one mark shows it
        Assert.Equal(0, EraMarks.MarksFilled(0.0, 10, fullOnlyAtOne: true));

        int FilledStrokes(long progress)
        {
            (UiSession s, SettlementId capital) = From((w, c) => Queue(w, c, Granary, progress));
            EraTheme th = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
            Assert.Equal(ProgressStyle.Notches, th.Controls.Progress);
            Assert.Equal(10, th.Controls.ProgressSegments);
            ActionSurfaceModel model = ActionSurface.ForSession(s, UiSession.ProductionEra(), capital.Value, th);
            var screen = new ActionSurfaceScreen { Theme = th };
            screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
            var d = new DrawList();
            screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
            return d.Commands.OfType<LineCmd>().Count(l => l.Width == 1.8 && l.Color.Equals(th.Semantic.Progress));
        }
        Assert.Equal(9 - 1, FilledStrokes(ConstructionQuery.ProgressUnits - 1) - FilledStrokes(1));
    }

    [Fact]
    public void Cancel_IsTwoStepForAnEntrySomethingHasGoneInto_OneClickOtherwise_AndAPendingCancelHasNoButton()
    {
        int behind = -1;
        (UiSession s, SettlementId capital) = From((w, c) =>
        {
            Queue(w, c, Granary, Part);
            behind = Queue(w, c, Granary, 0);
        });
        (ActionSurfaceScreen screen, _, _, _) = Paint(s, capital);
        ActionHit first = screen.Hits.Single(h => h.Kind == ActionHitKind.CancelConstruction && h.B == 0);

        // Click 1 on the part-built head ARMS the confirm and emits nothing.
        Assert.Equal(ActionCommandKind.None, screen.Click(first.Rect.CenterX, first.Rect.CenterY).Kind);
        Assert.Equal((capital.Value, 0), screen.CancelArmed);
        (_, _, _, string armed) = Paint(s, capital, screen);
        ActionHit confirm = Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.CancelConfirm);
        Assert.Equal((capital.Value, 0), (confirm.A, confirm.B));
        Assert.Contains("Lose it", armed, StringComparison.Ordinal);
        Assert.Contains("Cancelling loses what has gone in: 16 timber, 8 stone, 0.8 adult-years. Nothing is refunded.", armed, StringComparison.Ordinal);

        // Click 2 emits kind 11 through the guarded emitter.
        ActionCommand cmd = screen.Click(confirm.Rect.CenterX, confirm.Rect.CenterY);
        Assert.Equal((ActionCommandKind.CancelConstruction, capital.Value, 0), (cmd.Kind, cmd.Settlement, cmd.Project));
        Assert.Null(screen.CancelArmed);
        Assert.True(ActionDispatch.Apply(s, cmd));
        OrderRecord order = s.QueuedOrders()[^1];
        Assert.Equal(ConstructionQuery.CancelOrder(s.World, Me, capital, 0), order);
        Assert.False(s.EmitCancelConstruction(capital.Value, 0));   // already pending: nothing appended twice

        // The pending cancel: shown as cancelled this turn, with no button; the entry behind is now first in line.
        (screen, ActionSurfaceModel model, _, string after) = Paint(s, capital, screen);
        QueueEntry gone = model.Construction!.Queue.Single(q => q.Slot == 0);
        Assert.True(gone.CancelPending);
        Assert.Null(gone.Cancel);
        QueueEntry next = model.Construction.Queue.Single(q => q.Slot == behind);
        Assert.Equal((true, 1), (next.Head, next.Position));
        Assert.DoesNotContain(screen.Hits, h => h.Kind is ActionHitKind.CancelConstruction or ActionHitKind.CancelConfirm && h.B == 0);
        Assert.Contains("cancelled this turn: leaves the queue at End Turn, before building work; 16 timber, 8 stone, 0.8 adult-years are lost",
            after, StringComparison.Ordinal);

        // An entry nothing has gone into is cancelled with ONE click.
        ActionHit second = screen.Hits.Single(h => h.Kind == ActionHitKind.CancelConstruction && h.B == behind);
        ActionCommand now = screen.Click(second.Rect.CenterX, second.Rect.CenterY);
        Assert.Equal((ActionCommandKind.CancelConstruction, capital.Value, behind), (now.Kind, now.Settlement, now.Project));
        Assert.Null(screen.CancelArmed);
    }

    /// <summary>The fills of the frame commands (rects and polygons) wholly inside <paramref name="r"/> — a DrawList button's
    /// plate (PanelFrame: a Button frame has no texture).</summary>
    private static List<Sim.Ui.Art.ParchmentPalette.Rgba> FillsIn(DrawList d, RectD r)
    {
        RectD box = r.Inset(-1);
        var fills = new List<Sim.Ui.Art.ParchmentPalette.Rgba>();
        foreach (DrawCmd c in d.Commands)
        {
            if (c is RectCmd { Fill: { } f } rc && box.Contains(rc.Rect.X, rc.Rect.Y) && box.Contains(rc.Rect.Right, rc.Rect.Bottom)) fills.Add(f);
            if (c is PolygonCmd pc && pc.Points.All(q => box.Contains(q.X, q.Y))) fills.Add(pc.Fill);
        }
        return fills;
    }

    /// <summary>
    /// Batch 3 R3 (C1 review F3) — THE QUEUE'S READABILITY ROLES (era-ui §9a, UR-7; the WS1 UI rule): the status, waiting
    /// reason, forfeit warning and priority line at the Body/Second role in the Progress or Danger ink, explanations at
    /// Caption; Cancel Secondary, "Lose it" Danger. Pinned on the A1 surface with a waiting part-built head armed for its
    /// two-step cancel, a second committed entry behind it and a head at another settlement (so the priority line is
    /// drawn): the forfeit warning at the Second role (the Body size on this surface) in the Danger text ink; the status
    /// "waiting for …" and the priority line at the Body size in the Progress text ink; "Lose it" on the Danger plate
    /// (its label in the body role's heading weight), "Cancel" on the Secondary plate (the raised surface, the label in the
    /// body weight); after the confirm, the pending cancel's "… are lost" line in the Danger text ink.
    /// </summary>
    [Fact]
    public void TheQueuesRoles_ForfeitDanger_StatusAndPriorityProgress_LoseItDanger_CancelSecondary()
    {
        int stone = fx.Session.Config.Goods!.IdOf("stone");
        (UiSession s, SettlementId capital) = From((w, c) =>
        {
            SettlementId[] mine = LabourActivities.ControlledSettlements(w, Me);
            foreach (SettlementId x in mine) Set(w, x, stone, 0);
            Queue(w, c, Granary, Part);                        // the head: part-built, waits for materials
            Queue(w, c, Granary, 0);                           // committed behind it: a one-click Cancel
            Queue(w, mine[0] == c ? mine[1] : mine[0], Granary, 0);   // a second settlement's head: the priority line
        });
        (ActionSurfaceScreen screen, _, _, _) = Paint(s, capital);
        ActionHit head = screen.Hits.Single(h => h.Kind == ActionHitKind.CancelConstruction && h.B == 0);
        screen.Click(head.Rect.CenterX, head.Rect.CenterY);   // arm "Lose it"
        EraTheme th = screen.Theme;
        ActionSurfaceModel model = ActionSurface.ForSession(s, UiSession.ProductionEra(), capital.Value, th);
        screen.Refresh(model, s.World, s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
        List<TextCmd> texts = d.Commands.OfType<TextCmd>().ToList();
        double body = th.Type.Size(TypeScale.Px(th, TypeRole.Body)), caption = th.Type.Size(TypeScale.Px(th, TypeRole.Caption));
        Assert.True(caption < body, "anti-vacuity: the Caption role is smaller than the Body role");

        // The forfeit warning: the Second role (the Body size), the Danger text ink.
        TextCmd forfeit = Assert.Single(texts, t => t.Text.StartsWith("Cancelling loses what has gone in:", StringComparison.Ordinal));
        Assert.Equal((body, th.TextInk.Danger), (forfeit.Size, forfeit.Color));
        // The head's status (a waiting reason): the Body size, the Progress text ink.
        QueueEntry h0 = model.Construction!.Queue.Single(q => q.Slot == 0);
        Assert.StartsWith("waiting for ", h0.Status, StringComparison.Ordinal);
        TextCmd status = texts.First(t => t.Text.Length > 8 && h0.Status!.StartsWith(t.Text, StringComparison.Ordinal));
        Assert.Equal((body, th.TextInk.Progress), (status.Size, status.Color));
        // The priority line: the Body size, the Progress text ink.
        Assert.NotNull(model.Construction.Priority);
        TextCmd priority = Assert.Single(texts, t => t.Text.StartsWith("Building work is served oldest order first:", StringComparison.Ordinal));
        Assert.Equal((body, th.TextInk.Progress), (priority.Size, priority.Color));
        // An explanation stays at Caption: the one-click Cancel's note.
        TextCmd note = Assert.Single(texts, t => t.Text == "Cancel - nothing has gone into it yet");
        Assert.Equal((caption, th.Ink.TextSoft), (note.Size, note.Color));

        // "Lose it": the Danger plate; "Cancel": the Secondary plate.
        Sim.Ui.Art.ParchmentPalette.Rgba primary = ThemeColor.Mix(th.Material.PanelRaised, th.Material.Accent, 0.28),
            danger = ThemeColor.Mix(th.Material.PanelRaised, th.Semantic.Danger, 0.28), secondary = th.Material.PanelRaised;
        Assert.NotEqual(danger, primary);
        ActionHit confirm = Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.CancelConfirm);
        TextCmd loseIt = Assert.Single(texts, t => t.Text == "Lose it" && confirm.Rect.Contains(t.X, t.Y + 2));
        Assert.Equal((FontRole.Heading, body), (loseIt.Role, loseIt.Size));
        List<Sim.Ui.Art.ParchmentPalette.Rgba> loseFills = FillsIn(d, confirm.Rect);
        Assert.Contains(danger, loseFills);
        Assert.DoesNotContain(secondary, loseFills);
        Assert.DoesNotContain(primary, loseFills);
        ActionHit behind = Assert.Single(screen.Hits, h => h.Kind == ActionHitKind.CancelConstruction && h.A == capital.Value);
        TextCmd cancel = Assert.Single(texts, t => t.Text == "Cancel" && behind.Rect.Contains(t.X, t.Y + 2));
        Assert.Equal(FontRole.Body, cancel.Role);
        List<Sim.Ui.Art.ParchmentPalette.Rgba> cancelFills = FillsIn(d, behind.Rect);
        Assert.Contains(secondary, cancelFills);
        Assert.DoesNotContain(danger, cancelFills);
        Assert.DoesNotContain(primary, cancelFills);

        // Confirmed: the pending cancel's line, with what is lost, in the Danger text ink at the Second role.
        Assert.True(ActionDispatch.Apply(s, screen.Click(confirm.Rect.CenterX, confirm.Rect.CenterY)));
        (_, _, List<TextCmd> after, _) = Paint(s, capital, screen);
        TextCmd pending = Assert.Single(after, t => t.Text.StartsWith("granary - cancelled this turn", StringComparison.OrdinalIgnoreCase));
        Assert.Equal((body, th.TextInk.Danger), (pending.Size, pending.Color));
    }

    [Fact]
    public void AnEntryOrderedThisTurn_HasNoCancel_AndSaysWhatEndTurnDoes()
    {
        (UiSession s, SettlementId capital) = From((_, _) => { });
        Assert.True(s.EmitConstructionOrder(capital.Value, Granary));
        Assert.True(s.EmitConstructionOrder(capital.Value, Granary));
        (ActionSurfaceScreen screen, ActionSurfaceModel model, _, string all) = Paint(s, capital);
        ConstructionBlock c = model.Construction!;
        Assert.Equal(2, c.Queue.Length);
        Assert.All(c.Queue, q => Assert.False(q.Committed));
        Assert.All(c.Queue, q => Assert.Null(q.Cancel));
        Assert.DoesNotContain(screen.Hits, h => h.Kind is ActionHitKind.CancelConstruction or ActionHitKind.CancelConfirm);
        Assert.Equal(2, c.Ordered.Length);
        Assert.Contains("ordered - it is first in line, so at End Turn it starts and may use up to", c.Ordered[0], StringComparison.Ordinal);
        Assert.EndsWith("; later, cancelling loses what went in", c.Ordered[0], StringComparison.Ordinal);
        Assert.EndsWith("ordered - joins the queue 2nd; nothing is used until it is first in line", c.Ordered[1], StringComparison.Ordinal);
        Assert.Contains("can be cancelled from next turn", all, StringComparison.Ordinal);
        Assert.False(s.EmitCancelConstruction(capital.Value, 0));   // not committed: the intake would not find it
    }

    [Fact]
    public void TheHeadsStatus_NamesWhyItWaits_OnTheSurfaceAndInTheSettlementView()
    {
        (UiSession s, SettlementId capital) = From((w, c) =>
        {
            int stone = fx.Session.Config.Goods!.IdOf("stone"), timber = fx.Session.Config.Goods!.IdOf("timber");
            foreach (SettlementId mine in LabourActivities.ControlledSettlements(w, Me)) Set(w, mine, stone, 0);
            Set(w, c, timber, 100);
            Queue(w, c, Granary, 0);
        });
        (_, ActionSurfaceModel model, _, string all) = Paint(s, capital);
        QueueEntry head = Assert.Single(model.Construction!.Queue);
        Assert.Equal("waiting for stone: the empire has none", head.Status);
        Assert.Equal("waiting for stone: the empire has none", model.Construction.HeadBlocker);
        Assert.Contains("waiting for stone: the empire has none", all, StringComparison.Ordinal);

        // The settlement view judges the head with the coming step's length, as the surface does (before the first End
        // Turn the last step's is 0, which would read "waiting for builders").
        double? next = ActionQueryContext.ForNextStep(s.World, UiSession.ProductionEra()).NextDtYears;
        PlayerView v = PlayerViews.Settlement(s.World, s.Config, Me, null, capital.Value, s.Names.Name, 3, nextDtYears: next);
        string lines = string.Join(" | ", v.Blocks.Single(b => b.Heading == "Structures").Lines);
        Assert.Contains("Next to build: granary - not started; waiting for stone", lines, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fix round 1 (verifier WS1-TERM, 2026-10-15) — the two content-shaped stalls on the surface. MEASURED before this
    /// test: no UI test read "waiting: this project is no longer known" or the unobtainable-input line. A head whose
    /// project goods.json no longer defines, and a part-built workshop whose tools nothing in the Empire can make at turn 1
    /// (toolmaking needs bronze, an Age III chain), each say why they wait — the player's cue to cancel (the AI cancels its
    /// own; ConstructionTerminationTests) — and each keeps its Cancel.
    /// </summary>
    [Fact]
    public void AHeadWhoseProjectLeftTheContent_AndOneStalledOnAnUnobtainableCraftedInput_SayWhyTheyWait()
    {
        (UiSession unknown, SettlementId at) = From((w, c) => Queue(w, c, 77, 250_000));
        Assert.Null(unknown.Config.Goods!.ProjectById(77));
        (_, ActionSurfaceModel model, _, string all) = Paint(unknown, at);
        QueueEntry head = Assert.Single(model.Construction!.Queue);
        Assert.Equal("waiting: this project is no longer known", head.Status);
        Assert.Contains("waiting: this project is no longer known", all, StringComparison.Ordinal);
        Assert.NotNull(head.Cancel);

        (UiSession tools, SettlementId capital) = From((w, c) =>
        {
            int timber = fx.Session.Config.Goods!.IdOf("timber"), stone = fx.Session.Config.Goods!.IdOf("stone"),
                tool = fx.Session.Config.Goods!.IdOf("tools");
            foreach (SettlementId mine in LabourActivities.ControlledSettlements(w, Me)) Set(w, mine, tool, 0);
            Set(w, c, timber, 200);
            Set(w, c, stone, 200);
            Queue(w, c, Workshop, 300_000);
        });
        (_, ActionSurfaceModel m2, _, string all2) = Paint(tools, capital);
        QueueEntry stalled = Assert.Single(m2.Construction!.Queue);
        Assert.Equal("waiting for tools: the empire has none and nothing here can make it yet", stalled.Status);
        Assert.Contains("waiting for tools: the empire has none and nothing here can make it yet", all2, StringComparison.Ordinal);
        Assert.NotNull(stalled.Cancel);
    }

    [Fact]
    public void ASessionWithAPartBuiltHead_ACancelAndAnAllocationOrder_ResumesHashIdentical_AndContinuesToCompletion()
    {
        // The order-log RESUME round trip (D-054 WS1): the dev-size one-AI world, the player's capital ordering two granaries
        // at turn 0 (MEASURED on this tree: the empire holds no stone at turn 0, so the head is built a little each turn as
        // stone is quarried — part-built at turns 1 and 2, complete at 3), a kind-11 cancel of the second entry at turn 1
        // and a kind-12 SetResourceAllocation order (WS2: the player's split, building 60 shares in 100) through the Materials
        // block's emitter at turn 1. Saved at turn 2 with the head part-built and resumed by UiSession.Resume (re-found,
        // replay, every replayed hash checked against the saved trace): no divergence, the same log, trace, queue line and
        // split; then both sessions finish the head on the same turn.
        string dir = Path.Combine(Path.GetTempPath(), "d054-ws1-resume-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        string log = Path.Combine(dir, "orders-20261014-120000.bin");
        try
        {
            UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
            live.ExportManifest("2026-10-14 12:00:00", log);
            Assert.True(EmpireQuery.TryGetCapital(live.World, Me, out SettlementId capital));
            Assert.True(live.EmitConstructionOrder(capital.Value, Granary));
            Assert.True(live.EmitConstructionOrder(capital.Value, Granary));
            live.EndTurn();                                                                   // turn 1
            ConstructionQueueRow head = ConstructionQuery.Queue(live.World, capital)[0];
            long p1 = ConstructionQuery.ProgressOf(live.World, head);
            Assert.InRange(p1, 1, ConstructionQuery.ProgressUnits - 1);                        // part-built
            Assert.True(live.EmitCancelConstruction(capital.Value, 1));                        // the entry behind it
            Assert.True(live.EmitAllocationOrder(60));                                         // kind 12 (WS2)
            live.EndTurn();                                                                   // turn 2
            Assert.Single(ConstructionQuery.Queue(live.World, capital));                       // the cancel applied
            long p2 = ConstructionQuery.ProgressOf(live.World, head);
            Assert.InRange(p2, p1 + 1, ConstructionQuery.ProgressUnits - 1);                   // still part-built at the save
            Assert.Equal(new ResourceAllocationRow(Me, 60), Assert.Single(Enumerable.Range(0, live.World.ResourceAllocations.Count).Select(i => live.World.ResourceAllocations[i])));   // applied (WS2)

            live.Save(log);
            live.ExportTrace(UiSession.TracePath(log));
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _);
            Assert.Empty(resumed.ResumeNotice);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(live.Orders.Count, resumed.Orders.Count);
            for (int i = 0; i < live.Orders.Count; i++) Assert.Equal(live.Orders[i], resumed.Orders[i]);
            Assert.Contains(Enumerable.Range(0, resumed.Orders.Count).Select(i => resumed.Orders[i]), o => o.Kind == OrderKind.CancelConstruction);
            Assert.Contains(Enumerable.Range(0, resumed.Orders.Count).Select(i => resumed.Orders[i]), o => o.Kind == OrderKind.SetResourceAllocation);
            Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
            EraTheme th = EraThemes.For(UiEras.Of(live.World, live.Config.Ages, Me));
            static string Line(QueueEntry q) => string.Join("|", q.Slot, q.ProjectId, q.Name, q.Progress, q.Head, q.Committed, q.CancelPending,
                q.GoneIn, q.Status, q.Forfeit, q.Cancel?.Key, q.Cancel?.Detail, q.Position);
            string[] liveQueue = [.. ActionSurface.ForSession(live, UiSession.ProductionEra(), capital.Value, th).Construction!.Queue.Select(Line)];
            Assert.Single(liveQueue);
            string[] resumedQueue = [.. ActionSurface.ForSession(resumed, UiSession.ProductionEra(), capital.Value, th).Construction!.Queue.Select(Line)];
            Assert.Equal(liveQueue, resumedQueue);
            // The split as the player reads it (the Materials figures and the EMPIRE line's source) is the same after resume.
            double dt = ActionQueryContext.ForNextStep(live.World, UiSession.ProductionEra()).NextDtYears ?? 0.0;
            AllocationView? liveSplit = ResourceAllocationQuery.ForEmpire(live.World, live.Config, Me, live.QueuedOrders(), dt);
            AllocationView? resumedSplit = ResourceAllocationQuery.ForEmpire(resumed.World, resumed.Config, Me, resumed.QueuedOrders(), dt);
            Assert.Equal((60, 60, true), (liveSplit!.BuildingsPercent, liveSplit.CommittedPercent, liveSplit.HasRow));
            Assert.Equal((liveSplit.BuildingsPercent, liveSplit.CommittedPercent, liveSplit.HasRow, liveSplit.QueuedPercent),
                (resumedSplit!.BuildingsPercent, resumedSplit.CommittedPercent, resumedSplit.HasRow, resumedSplit.QueuedPercent));
            Assert.Equal(AllocationText.EmpireLine(liveSplit.CommittedPercent), AllocationText.EmpireLine(resumedSplit.CommittedPercent));

            int finished = -1;
            for (int t = 0; t < 6 && finished < 0; t++)
            {
                live.EndTurn();
                resumed.EndTurn();
                Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
                if (ConstructionQuery.Built(live.World, capital, Granary) > 0) finished = t;
            }
            Assert.True(finished >= 0, "the part-built granary never finished");
            Assert.Equal(ConstructionQuery.Built(live.World, capital, Granary), ConstructionQuery.Built(resumed.World, capital, Granary));
            Assert.Empty(ConstructionQuery.Queue(resumed.World, capital));
            Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// C1 review F10 — the per-good audit as the player reads it (TurnAuditModel.GoodAccountLines, the ECONOMY table) on a
    /// turn whose head was part-built (the dev-size one-AI world of the resume test: no stone at turn 0, so the capital's
    /// granary is built a little each turn as stone is quarried): every good closes — each line ends "ok", none says
    /// DISCREPANCY — and the stone and timber lines' construction column carries at least what the head's progress drew.
    /// </summary>
    [Fact]
    public void ATurnThatPartBuildsAHead_ReadsAsReconciledInTheTurnAudit_ItsConstructionColumnCarryingTheDraws()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        Assert.True(EmpireQuery.TryGetCapital(s.World, Me, out SettlementId capital));
        Assert.True(s.EmitConstructionOrder(capital.Value, Granary));
        s.EndTurn();
        ConstructionQueueRow head = ConstructionQuery.Queue(s.World, capital)[0];
        long p = ConstructionQuery.ProgressOf(s.World, head);
        Assert.InRange(p, 1, ConstructionQuery.ProgressUnits - 1);                           // part-built this turn

        Sim.Core.Observability.TurnRecord turn = s.Observations.Observations[^1].Turn;
        IReadOnlyList<string> lines = TurnAuditModel.GoodAccountLines(turn);
        Assert.True(lines.Count > 2);
        foreach (string line in lines.Skip(1)) Assert.EndsWith("  ok", line, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, l => l.Contains("DISCREPANCY", StringComparison.Ordinal));
        foreach ((string good, long cost) in new[] { ("timber", 40L), ("stone", 20L) })
        {
            Sim.Core.Observability.GoodAccount a = turn.Goods.Single(g => g.Name == good);
            Assert.True(a.ConstructionMaterials >= ConstructionQuery.Delivered(cost, p),
                $"{good}: the construction column ({a.ConstructionMaterials}) carries the head's draw ({ConstructionQuery.Delivered(cost, p)})");
            string row = lines.Single(l => l.StartsWith(good + " ", StringComparison.Ordinal));
            Assert.Contains(a.ConstructionMaterials.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(8) + "  = ", row, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void APendingCancel_FreesItsClaims_ForTheListingAndTheOrderFactoryAlike()
    {
        // A committed workshop at another settlement claims the empire's ten tools: the capital's workshop is locked, and
        // the factory refuses it. A PENDING cancel of that workshop (applied before enqueues, as the intake will) frees the
        // claim — the listing offers the capital's workshop and the factory builds its order, judged against the same book.
        SettlementId other = default;
        (UiSession s, SettlementId capital) = From((w, c) =>
        {
            int tools = fx.Session.Config.Goods!.IdOf("tools");
            SettlementId[] mine = LabourActivities.ControlledSettlements(w, Me);
            other = mine[0] == c ? mine[1] : mine[0];
            foreach (SettlementId x in mine) Set(w, x, tools, 0);
            Set(w, other, tools, 10);
            Queue(w, other, Workshop, 0);
        });
        string key = "construction." + capital.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".workshop";
        bool Listed() => AvailableActionsQuery.For(s.World, s.Config, Me, new ActionQueryContext(s.QueuedOrders(), null)).Any(a => a.Key == key);
        bool Factory() => ConstructionOrderFactory.Create(s.World, s.Config, Me, capital, Workshop, s.QueuedOrders()) is not null;

        Assert.False(Listed());
        Assert.False(Factory());
        Assert.False(s.EmitConstructionOrder(capital.Value, Workshop));

        Assert.True(s.EmitCancelConstruction(other.Value, 0));
        Assert.True(Listed());
        Assert.True(Factory());
        Assert.True(s.EmitConstructionOrder(capital.Value, Workshop));
        // ...and the new pending workshop claims the ten tools in turn: a second one is neither listed nor built.
        Assert.False(Listed());
        Assert.False(Factory());
    }
}
