using Microsoft.Xna.Framework.Input;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Ui.Actions;
using Sim.Ui.Headless;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 S1d (D-051 §5, ADR-036) — THE FOUND SETTLEMENT UI, end to end through the live UI (<see cref="UiFrameHarness"/>: the
/// game's real per-frame UI in a real ImGui context). The action surface lists "Found a settlement" for an owned source
/// whose source-side arms hold; "Choose a site..." enters the placement mode; the hovered place is judged by THE predicate
/// (the hint says why, the route and its cost are drawn, the P8 place card is docked); a click selects it, Confirm asks the
/// ONE guarded emitter (<see cref="UiSession.EmitFoundSettlement"/>), and End Turn founds the colony in the next state.
/// An illegal place queues nothing and says why; Escape cancels; undiscovered ground is not offered; looking (hover,
/// camera, the card) changes no hash and queues nothing; the order the UI queued moves exactly the people and grain the
/// predicate's own functions name, and it replays hash for hash.
/// </summary>
[Collection("ImGui context")]
public class FoundSettlementUiTests : IDisposable
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-found-tests-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    private string WorkDir(string name) => Path.Combine(_root, name);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ rigs and helpers

    /// <summary>The canonical founded world (seed 42): the founding path's worldgen config describes its map. (A non-canonical
    /// --size map founds on its own cells too since D-052 — NonCanonicalSizeFoundingUiTests.)</summary>
    private static readonly Lazy<WorldState> Canonical = new(() => UiFounding.Found(42));

    private static UiSession Fresh() => UiSession.StartFrom(Canonical.Value.Clone(), 42);

    private static string Hash(UiSession s) => WorldHash.ComputeHex(s.World);

    /// <summary>The best LEGAL site for a Found Settlement order of the player's from <paramref name="source"/> (the AI's own
    /// ranking over the predicate's Legal cells — any legal cell would do; this one is deterministic).</summary>
    private static int LegalSite(UiSession s, int source)
    {
        SettlementSiting.FrontierSiting f = s.Sites!.Frontier;
        int cell = AiFoundingPolicy.BestSiteFor(s.World, s.Config, f, s.World.Terrain!, new AiFoundingPolicy.Sites(s.World, f), Me, new SettlementId(source));
        Assert.True(cell >= 0, "no legal site - the rig is void");
        return cell;
    }

    /// <summary>As <see cref="LegalSite"/>, among the cells the session's player KNOWS (the AI's ranking with that knowledge).</summary>
    private static int LegalSiteKnown(UiSession s, int source)
    {
        SettlementSiting.FrontierSiting f = s.Sites!.Frontier;
        int cell = AiFoundingPolicy.BestSiteFor(s.World, s.Config, f, s.World.Terrain!, new AiFoundingPolicy.Sites(s.World, f), Me,
            new SettlementId(source), FoundingQuery.Check, s.Knowledge.Core);
        Assert.True(cell >= 0, "no legal known site - the rig is void");
        return cell;
    }

    private static int SiteOf(IReadOnlyWorldState w, int settlement)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id.Value == settlement) return w.Settlements[i].SiteCell;
        return -1;
    }

    private static int SettlementAt(IReadOnlyWorldState w, int cell)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].SiteCell == cell) return w.Settlements[i].Id.Value;
        return -1;
    }

    /// <summary>Frames <paramref name="cell"/> at the map rect's centre, 8 screen px a cell, and returns where to point.</summary>
    private static (double X, double Y) Aim(UiFrameHarness h, int cell)
    {
        int size = h.Ui.World.Terrain!.Size;
        CameraFocus.CenterOnCell(h.Ui.Camera, cell, size, h.Ui.MapRect, zoom: 8.0);
        h.Idle(2);
        (double x, double y) = h.Ui.Camera.WorldToScreen(cell % size + 0.5, cell / size + 0.5, h.Ui.MapRect);
        Assert.Equal(cell, MapCells.At(h.Ui.Camera, h.Ui.MapRect, x, y, size));
        return (x, y);
    }

    /// <summary>Opens POLICY (scrolled home) and clicks its "Choose a site..." for the block's source; returns the source.</summary>
    private static int ChooseASite(UiFrameHarness h)
    {
        if (h.Ui.OpenSection != Section.Policy) Assert.True(h.ClickControl("nav:Policy"));
        for (int k = 0; k < 30; k++) h.Wheel(h.Ui.ContextRect.X + 100, h.Ui.ContextRect.Y + 300, 3);
        FoundingBlock block = h.Ui.Actions.Model!.Founding ?? throw new InvalidOperationException("the surface lists no founding");
        PanelRect p = h.Ui.ContextRect;
        double top = ChromeGeometry.ContentTop(ChromeGeometry.Context, ImGuiNET.ImGui.GetFrameHeight()),
            bottom = p.Y + p.Height - Sim.Ui.Art.UiTheme.WindowPaddingPx.Y * h.Ui.Scale - 6;
        for (int k = 0; k < 60; k++)
        {
            ActionHit? hit = null;
            foreach (ActionHit x in h.Ui.Actions.Hits) if (x.Kind == ActionHitKind.FoundSettlement) hit = x;
            Assert.NotNull(hit);
            if (hit.Value.Rect.CenterY >= top + 2 && hit.Value.Rect.CenterY <= bottom)
            {
                h.Click(hit.Value.Rect.CenterX, hit.Value.Rect.CenterY);
                return block.Source.Value;
            }
            h.Wheel(p.X + p.Width / 2, p.Y + p.Height / 2, hit.Value.Rect.CenterY < top + 2 ? 1 : -1);
        }
        throw new InvalidOperationException("the founding button never scrolled into view");
    }

    /// <summary>The first cell, by Chebyshev rings about <paramref name="center"/> (row-major within a ring), that
    /// <paramref name="want"/> accepts; -1 within 60 rings.</summary>
    private static int NearestCell(int size, int center, Func<int, bool> want)
    {
        for (int r = 1; r < 60; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int x = center % size + dx, y = center / size + dy;
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    if (want(y * size + x)) return y * size + x;
                }
        return -1;
    }

    private static long People(IReadOnlyWorldState w, SettlementId s)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) if (w.Buckets[i].Settlement == s) n += w.Buckets[i].Count.Value;
        return n;
    }

    private static long People(IReadOnlyWorldState w)
    {
        long n = 0;
        for (int i = 0; i < w.Buckets.Count; i++) n += w.Buckets[i].Count.Value;
        return n;
    }

    private static long Grain(IReadOnlyWorldState w, SettlementId s, GoodId grain)
    {
        int at = GoodStockIndex.IndexOf(w.GoodStocks, s, grain);
        return at < 0 ? 0 : w.GoodStocks[at].Amount.Value;
    }

    private static long Grain(IReadOnlyWorldState w, GoodId grain)
    {
        long n = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++) if (w.GoodStocks[i].Good == grain) n += w.GoodStocks[i].Amount.Value;
        return n;
    }

    // ------------------------------------------------------------------ the flow

    [Fact]
    public void SelectSource_Hover_Click_Confirm_EndTurn_TheColonyStandsInTheNextState_AndLookingChangedNothing()
    {
        UiSession s = Fresh();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("flow"), Assets());
        long turn = s.World.Clock.Turn;

        // THE ACTION: the surface lists it for the selection (an owned source whose source-side arms hold).
        int source = ChooseASite(h);
        Assert.True(h.Ui.Placement.Active);
        Assert.Equal(source, h.Ui.Placement.Source);
        Assert.True(EmpireQuery.ControlsSettlement(s.World, Me, new SettlementId(source)));
        Assert.Empty(s.QueuedOrders());
        string hash = Hash(s);

        // HOVER: the place is judged by THE predicate; the painted hint says so (never a tooltip); after a rest the route
        // (FoundingQuery.RouteFrom — the route arm 7 judges) is shown and the P8 place card judged from the source is docked.
        int site = LegalSite(s, source);
        (double x, double y) = Aim(h, site);
        h.MoveTo(x, y);
        h.Idle(PlacementMode.CardRestFrames + 1);
        Assert.Equal(site, h.Ui.Placement.Hovered);
        Assert.Equal(FoundingVerdict.Legal, h.Ui.Placement.HoveredVerdict);
        Assert.NotNull(h.Ui.PlacementHint);
        Assert.StartsWith("Found here: " + InfoQuery.FoundingLine(FoundingVerdict.Legal, s.Names.Name(source)), h.Ui.PlacementHint);
        Assert.Empty(h.Gui.LastTooltipWindows);
        RouteQueryAssert(s, h, source, site);
        Assert.Equal(InfoSubject.OfCell(site, source), h.Ui.Inspector.Subject);
        Assert.True(h.Ui.FrameText.Contains("Found a settlement from " + s.Names.Name(source), FrameSurface.Placement));

        // LOOKING CHANGES NOTHING: hover, wheel, drag and the docked card left the world and the queue as they were.
        h.Wheel(x, y, 1);
        h.Wheel(x, y, -1);
        h.Drag(x, y, x + 30, y + 10);
        (x, y) = Aim(h, site);
        h.MoveTo(x, y);
        Assert.Equal(hash, Hash(s));
        Assert.Empty(s.QueuedOrders());
        Assert.True(h.Ui.Placement.Active);

        // STEP ONE: the click chooses the place (nothing is queued yet).
        h.Click(x, y);
        Assert.Equal(site, h.Ui.Placement.Selected);
        Assert.True(h.Ui.Placement.CanConfirm);
        Assert.Empty(s.QueuedOrders());
        Assert.Equal(hash, Hash(s));

        // STEP TWO: Confirm appends exactly ONE order — FoundingQuery.FoundOrder, the AI's constructor — and the mode ends.
        Assert.True(h.ClickControl("found-confirm"));
        OrderRecord order = Assert.Single(s.QueuedOrders());
        Assert.Equal(FoundingQuery.FoundOrder(s.World, Me, new SettlementId(source), site), order);
        Assert.Equal(turn, order.Turn);
        Assert.False(h.Ui.Placement.Active);
        Assert.Equal(hash, Hash(s));   // queued, not applied
        // The source is not offered again this turn (the intake founds only the first legal order per source per turn).
        Assert.DoesNotContain(h.Ui.Actions.Model!.Actions, a => a.Domain == ActionDomain.Colonization && a.Id == source);
        Assert.False(h.Ui.BeginPlacement(source));

        // END TURN: the colony stands in the state of turn + 1, at the site, the player's, founded that turn.
        Assert.True(h.ClickControl("end-turn"));
        Assert.Equal(turn + 1, s.World.Clock.Turn);
        int colony = SettlementAt(s.World, site);
        Assert.True(colony >= 0, "the UI-queued order founded nothing");
        Assert.True(EmpireQuery.ControlsSettlement(s.World, Me, new SettlementId(colony)));
        SettlementRow row = default;
        for (int i = 0; i < s.World.Settlements.Count; i++) if (s.World.Settlements[i].Id.Value == colony) row = s.World.Settlements[i];
        Assert.Equal(turn + 1, row.FoundedTurn);
        Assert.True(ConservationAuditor.IsConserved(s.World, s.Config.Goods, out string report), report);
        Assert.Empty(h.Problems);
    }

    private static void RouteQueryAssert(UiSession s, UiFrameHarness h, int source, int site)
    {
        Assert.Equal(site, h.Ui.Placement.RouteCell);
        Sim.Core.State.RouteQuery.Route route = h.Ui.Placement.Route ?? throw new InvalidOperationException("no route shown");
        Sim.Core.State.RouteQuery.Route expected = FoundingQuery.RouteFrom(s.World, s.Sites!.Frontier, new SettlementId(source), site)!;
        Assert.True(route.Found);
        Assert.Equal(expected.Nodes, route.Nodes);
        Assert.Equal(expected.TotalCost, route.TotalCost);
        Assert.Contains(h.Ui.Placement.RouteLine(s.Sites!.Frontier.Lattice)!, h.Ui.PlacementHint);
    }

    // ------------------------------------------------------------------ an illegal place

    [Fact]
    public void AnIllegalPlace_QueuesNothing_AndShowsWhy_ConfirmIsDisabled_EnterRefuses_EscapeCancels()
    {
        UiSession s = Fresh();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("illegal"), Assets());
        int source = ChooseASite(h);
        string hash = Hash(s);
        string from = s.Names.Name(source);

        // The source's own site: Occupied. A water cell: Water. Each is SELECTED with its reason on the bar.
        int own = SiteOf(s.World, source);
        int water = NearestCell(s.World.Terrain!.Size, own, c => s.World.Terrain.Water[c] >= 0.5);
        Assert.True(water >= 0);
        foreach ((int cell, FoundingVerdict want) in new[] { (own, FoundingVerdict.Occupied), (water, FoundingVerdict.Water) })
        {
            (double x, double y) = Aim(h, cell);
            h.Click(x, y);
            Assert.Equal(cell, h.Ui.Placement.Selected);
            Assert.Equal(want, h.Ui.Placement.SelectedVerdict);
            Assert.False(h.Ui.Placement.CanConfirm);
            string reason = InfoQuery.FoundingLine(want, from);
            Assert.True(h.Ui.FrameText.Contains(reason, FrameSurface.Placement), "the bar does not say why: " + reason);

            // Confirm is drawn DISABLED: a click on it queues nothing. Enter asks anyway: refused, and the bar says why.
            Assert.True(h.ClickControl("found-confirm"));
            Assert.Empty(s.QueuedOrders());
            h.Key(Keys.Enter);
            Assert.Empty(s.QueuedOrders());
            Assert.Equal("Not here: " + reason + ".", h.Ui.Placement.Refusal);
            Assert.True(h.Ui.FrameText.Contains("Not here: " + reason, FrameSurface.Placement));
            Assert.True(h.Ui.Placement.Active);
        }

        // A place inside the spacing exclusion of a standing settlement: TooClose (found by the predicate, ring order).
        int tooClose = NearestCell(s.World.Terrain.Size, own, c => h.Ui.Placement.Judge(s, c) == FoundingVerdict.TooClose);
        Assert.True(tooClose >= 0);
        (double tx, double ty) = Aim(h, tooClose);
        h.Click(tx, ty);
        Assert.Equal(FoundingVerdict.TooClose, h.Ui.Placement.SelectedVerdict);
        Assert.False(s.EmitFoundSettlement(source, tooClose));   // the emitter's own guard refuses it too
        Assert.Empty(s.QueuedOrders());

        // In the mode a click on ANOTHER settlement chooses its place (Occupied); it does not select the settlement.
        int other = -1;
        for (int i = 0; i < s.World.Settlements.Count && other < 0; i++)
            if (s.World.Settlements[i].Id.Value != source && s.World.Settlements[i].Id.Value != h.Ui.SelectedSettlement) other = s.World.Settlements[i].Id.Value;
        int selectedBefore = h.Ui.SelectedSettlement;
        (double ox, double oy) = Aim(h, SiteOf(s.World, other));
        h.Click(ox, oy);
        Assert.Equal(SiteOf(s.World, other), h.Ui.Placement.Selected);
        Assert.Equal(FoundingVerdict.Occupied, h.Ui.Placement.SelectedVerdict);
        Assert.Equal(selectedBefore, h.Ui.SelectedSettlement);
        Assert.Equal(InfoSubject.OfCell(SiteOf(s.World, other), source), h.Ui.Inspector.Subject);   // the chosen place's card is docked

        // Escape cancels the mode (and puts its card away); nothing was queued, nothing changed.
        h.Key(Keys.Escape);
        Assert.False(h.Ui.Placement.Active);
        Assert.Empty(s.QueuedOrders());
        Assert.Equal(hash, Hash(s));
        Assert.Equal(0, h.ExitRequests);   // Escape left the mode; it did not ask to exit
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ a founding already queued this turn (S1d review)

    [Fact]
    public void AFoundingQueuedThisTurn_InsideItsColonysExclusion_TheHintTheBarConfirmAndTheDockedCardGiveOneVerdict()
    {
        // S1d review finding: the mode judged each place against this turn's batch, the P8 card it docked judged the bare
        // world — so once a founding was queued, the card said a refused place "would be accepted" and ticked every arm,
        // while the hint said "too close to a standing settlement" where none stands. Now the card is judged against the
        // mode's own batch and a refusal only an ordered colony causes is told in its own words, everywhere alike.
        UiSession s = Fresh();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("batch"), Assets());

        // One founding queued through the UI: the block's source to its best legal place.
        int first = ChooseASite(h);
        int ordered = LegalSite(s, first);
        (double ox, double oy) = Aim(h, ordered);
        h.Click(ox, oy);
        Assert.True(h.ClickControl("found-confirm"));
        OrderRecord order = Assert.Single(s.QueuedOrders());
        string hash = Hash(s);

        // ANOTHER source the surface lists, and a known place the bare world accepts from it that only the ordered colony's
        // spacing refuses (the emitter's own judgement against this turn's batch).
        int other = -1;
        foreach (ActionDescriptor a in h.Ui.Actions.Model!.Actions)
            if (a.Domain == ActionDomain.Colonization && a.Id != first) { other = checked((int)a.Id); break; }
        Assert.True(other >= 0, "no second source listed - the rig is void");
        string from = s.Names.Name(other);
        SettlementSiting.FrontierSiting f = s.Sites!.Frontier;
        FoundSettlementOrderFactory.Pending pending = FoundSettlementOrderFactory.Standing(s.World, s.Config, f, s.QueuedOrders());
        int size = s.World.Terrain!.Size;
        int cell = NearestCell(size, ordered, c => s.Knowledge.KnowsCell(c)
            && FoundingQuery.Check(s.World, s.Config, f, Me, new SettlementId(other), c) == FoundingVerdict.Legal
            && FoundSettlementOrderFactory.Judge(s.World, s.Config, f, s.Knowledge.Core, Me, new SettlementId(other), c, pending) == FoundingVerdict.TooClose);
        Assert.True(cell >= 0, "no place inside the ordered colony's exclusion only - the rig is void");
        string reason = InfoQuery.FoundingLine(FoundingVerdict.TooClose, from, byOrder: true);
        Assert.Equal("too close to a settlement you ordered this turn", reason);

        // The place's card is already open (built before the mode, without its batch); entering the mode and resting on the
        // place docks the same subject, so the open card must be RE-JUDGED against the mode's batch, not kept.
        h.Ui.OpenInfo(InfoSubject.OfCell(cell, other));
        h.Idle(1);
        Assert.NotNull(h.Ui.Inspector.Card);
        Assert.True(h.Ui.BeginPlacement(other));
        Assert.Equal(FoundingVerdict.TooClose, h.Ui.Placement.Judge(s, cell));

        // HOVER, then rest: the hint and the docked card say the same thing.
        (double x, double y) = Aim(h, cell);
        h.MoveTo(x, y);
        h.Idle(PlacementMode.CardRestFrames + 1);
        Assert.Equal(cell, h.Ui.Placement.Hovered);
        Assert.Equal(FoundingVerdict.TooClose, h.Ui.Placement.HoveredVerdict);
        Assert.NotNull(h.Ui.PlacementHint);
        Assert.Equal("Not here: " + reason, h.Ui.PlacementHint!.Split('\n')[0]);
        Assert.DoesNotContain("standing settlement", h.Ui.PlacementHint);
        Assert.Equal(InfoSubject.OfCell(cell, other), h.Ui.Inspector.Subject);
        InfoCard card = h.Ui.Inspector.Card ?? throw new InvalidOperationException("no card docked");
        AssertCardRefuses(card, reason);

        // CLICK: chosen with the same reason on the bar; Confirm disabled; the card unchanged; Enter refuses in those words.
        h.Click(x, y);
        Assert.Equal(cell, h.Ui.Placement.Selected);
        Assert.Equal(FoundingVerdict.TooClose, h.Ui.Placement.SelectedVerdict);
        Assert.False(h.Ui.Placement.CanConfirm);
        Assert.True(h.Ui.FrameText.Contains(" - " + reason + ".", FrameSurface.Placement), "the bar does not say why: " + reason);
        Assert.Equal(card, h.Ui.Inspector.Card);
        Assert.True(h.ClickControl("found-confirm"));   // drawn disabled: nothing
        h.Key(Keys.Enter);
        Assert.Equal("Not here: " + reason + ".", h.Ui.Placement.Refusal);
        Assert.False(s.EmitFoundSettlement(other, cell));   // the guard agrees
        Assert.Equal(order, Assert.Single(s.QueuedOrders()));
        Assert.Equal(hash, Hash(s));

        // The ordered site itself: occupied by the order, told as such by the hint, the bar and the card alike.
        string here = InfoQuery.FoundingLine(FoundingVerdict.Occupied, from, byOrder: true);
        (double sx, double sy) = Aim(h, ordered);
        h.MoveTo(sx, sy);
        h.Idle(PlacementMode.CardRestFrames + 1);
        Assert.Equal(FoundingVerdict.Occupied, h.Ui.Placement.HoveredVerdict);
        Assert.Equal("Not here: " + here, h.Ui.PlacementHint!.Split('\n')[0]);
        h.Click(sx, sy);
        Assert.True(h.Ui.FrameText.Contains(" - " + here + ".", FrameSurface.Placement), "the bar does not say why: " + here);
        Assert.Equal(InfoSubject.OfCell(ordered, other), h.Ui.Inspector.Subject);
        AssertCardRefuses(h.Ui.Inspector.Card!, here);

        // CONTROL (the defect's own reproduction): the bare card — no batch — for the first place says it would be accepted.
        InfoCard bare = InfoQuery.Card(s.World, s.Config, Me, InfoSubject.OfCell(cell, other), settlementName: s.Names.Name,
            knowledge: s.Knowledge.Core, site: s.Sites);
        Assert.Equal(InfoQuery.FoundingLine(FoundingVerdict.Legal, from) + ".", bare.StatusLine);

        h.Key(Keys.Escape);
        Assert.False(h.Ui.Placement.Active);
        Assert.Equal(order, Assert.Single(s.QueuedOrders()));
        Assert.Equal(hash, Hash(s));
        Assert.Empty(h.Problems);
    }

    /// <summary>The docked place card refuses with <paramref name="reason"/>: its status line, its FoundingQuery line (not
    /// ticked), and an arm that fails because of the order queued this turn.</summary>
    private static void AssertCardRefuses(InfoCard card, string reason)
    {
        Assert.Equal(reason + ".", card.StatusLine);
        InfoSection arms = card.More.Single(sec => sec.Heading.StartsWith("Founding here", StringComparison.Ordinal));
        Assert.Equal("FoundingQuery: " + reason, arms.Lines[0].Label);
        Assert.False(arms.Lines[0].Met);
        Assert.Contains(arms.Lines.Skip(1), l => l.Met == false && (l.Note ?? "").StartsWith("A settlement you ordered this turn will stand", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ F, Cancel, and undiscovered ground

    [Fact]
    public void FKey_EntersAndLeavesTheMode_TheCancelButtonLeavesIt_NothingIsQueued()
    {
        UiSession s = Fresh();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("keys"), Assets());
        string hash = Hash(s);
        FoundingBlock block = h.Ui.Actions.Model!.Founding!;
        h.Key(Keys.F);
        Assert.True(h.Ui.Placement.Active);
        Assert.Equal(block.Source.Value, h.Ui.Placement.Source);
        Assert.NotNull(h.Ui.Controls.Find("found-cancel"));
        h.Key(Keys.F);
        Assert.False(h.Ui.Placement.Active);
        h.Key(Keys.F);
        Assert.True(h.ClickControl("found-cancel"));
        Assert.False(h.Ui.Placement.Active);
        Assert.Null(h.Ui.Controls.Find("found-cancel"));   // the bar is drawn only in the mode
        Assert.Empty(s.QueuedOrders());
        Assert.Equal(hash, Hash(s));

        // End Turn leaves the mode: its verdicts were for the world just stepped.
        h.Key(Keys.F);
        Assert.True(h.Ui.Placement.Active);
        h.Key(Keys.Space);
        Assert.Equal(1L, s.World.Clock.Turn);
        Assert.False(h.Ui.Placement.Active);
        Assert.Equal(0, h.ExitRequests);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void UndiscoveredGround_IsNeitherHoverableNorSelectable_AndTheEmitterRefusesIt_AllKnownControlAcceptsIt()
    {
        // A knowledge stub that hides exactly the lattice node holding the best legal site: the predicate (no known-extent
        // arm yet) answers Legal for it, but the player does not know it, so the UI never offers it.
        UiSession all = Fresh();
        int source = all.Actions().Founding!.Source.Value;
        int site = LegalSite(all, source);
        PolityKnowledge core = all.Knowledge.Core;
        var known = new bool[core.LatticeSize * core.LatticeSize];
        for (int n = 0; n < known.Length; n++) known[n] = true;
        known[core.NodeOfCell(site)] = false;
        UiSession s = UiSession.StartFrom(Canonical.Value.Clone(), 42, null, null, knowledgeOf: w => KnowledgeStubs.Over(w, Me, known));
        Assert.False(s.Knowledge.KnowsCell(site));
        Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(s.World, s.Config, s.Sites!.Frontier, Me, new SettlementId(source), site));

        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("undiscovered"), Assets());
        h.Ui.BeginPlacement(source);
        Assert.True(h.Ui.Placement.Active);
        string hash = Hash(s);
        (double x, double y) = Aim(h, site);
        h.MoveTo(x, y);
        h.Idle(PlacementMode.CardRestFrames + 1);
        Assert.Equal(-1, h.Ui.Placement.Hovered);   // not hoverable
        Assert.Null(h.Ui.PlacementHint);
        Assert.Null(h.Ui.Placement.Judge(s, site));
        h.Click(x, y);
        Assert.Equal(-1, h.Ui.Placement.Selected);  // not selectable
        h.Key(Keys.Enter);
        Assert.False(s.EmitFoundSettlement(source, site));   // the guard: an unknown cell is never ordered
        Assert.Empty(s.QueuedOrders());
        Assert.Equal(hash, Hash(s));

        // A KNOWN legal place under the same stub is offered and judged, but the player does not see the whole world
        // (ADR-037 §7): no route is drawn or told — the route runs over ground the player has not seen.
        int knownSite = LegalSiteKnown(s, source);
        (double kx, double ky) = Aim(h, knownSite);
        h.MoveTo(kx, ky);
        h.Idle(PlacementMode.CardRestFrames + 1);
        Assert.Equal(knownSite, h.Ui.Placement.Hovered);
        Assert.Equal(FoundingVerdict.Legal, h.Ui.Placement.HoveredVerdict);
        Assert.False(h.Ui.Placement.SeesAll);
        Assert.Null(h.Ui.Placement.Route);
        Assert.DoesNotContain("Route from", h.Ui.PlacementHint);
        Assert.Equal(hash, Hash(s));

        // Control: the same cell, all-known, is offered and accepted.
        Assert.Equal(FoundingVerdict.Legal, FoundSettlementOrderFactory.Judge(all.World, all.Config, all.Sites!.Frontier, all.Knowledge.Core, Me,
            new SettlementId(source), site, FoundSettlementOrderFactory.Standing(all.World, all.Config, all.Sites.Frontier, [])));
        Assert.True(all.EmitFoundSettlement(source, site));
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void TheReason_IsThePlaceCardsOwnLine_AndUnderAPartialViewARefusalDecidedByUnseenGroundIsToldOnlyAsSuch()
    {
        foreach (FoundingVerdict v in Enum.GetValues<FoundingVerdict>())
            Assert.Equal(InfoQuery.FoundingLine(v, "Ur"), PlacementMode.ReasonText(v, "Ur", seesAll: true));
        // Decided by what the player knows: told as is under a partial view too.
        foreach (FoundingVerdict v in new[] { FoundingVerdict.Legal, FoundingVerdict.UnknownSource, FoundingVerdict.NotControlled,
                     FoundingVerdict.OffMap, FoundingVerdict.Water, FoundingVerdict.NoParty, FoundingVerdict.NoProvisions })
            Assert.Equal(InfoQuery.FoundingLine(v, "Ur"), PlacementMode.ReasonText(v, "Ur", seesAll: false));
        // Decided by the floor over every cell, occupancy, spacing, the claim or the route: never narrated.
        foreach (FoundingVerdict v in new[] { FoundingVerdict.PoorSite, FoundingVerdict.Occupied, FoundingVerdict.TooClose,
                     FoundingVerdict.Claimed, FoundingVerdict.Unreachable })
        {
            string text = PlacementMode.ReasonText(v, "Ur", seesAll: false);
            Assert.NotEqual(InfoQuery.FoundingLine(v, "Ur"), text);
            Assert.Contains("depends on what you have not seen", text);
        }
        // S1d review: a refusal only an order queued this turn causes is told in its own words (the card's); under a partial
        // view the player's own ordered site is the player's to know, the spacing of one is not (it reads the lattice).
        foreach (FoundingVerdict v in Enum.GetValues<FoundingVerdict>())
            Assert.Equal(InfoQuery.FoundingLine(v, "Ur", byOrder: true), PlacementMode.ReasonText(v, "Ur", seesAll: true, byOrder: true));
        Assert.Equal("a settlement you ordered this turn will stand here", PlacementMode.ReasonText(FoundingVerdict.Occupied, "Ur", seesAll: false, byOrder: true));
        Assert.Contains("depends on what you have not seen", PlacementMode.ReasonText(FoundingVerdict.TooClose, "Ur", seesAll: false, byOrder: true));
        Assert.Equal("too close to a settlement you ordered this turn", PlacementMode.ReasonText(FoundingVerdict.TooClose, "Ur", seesAll: true, byOrder: true));
        Assert.Equal("too close to a standing settlement", PlacementMode.ReasonText(FoundingVerdict.TooClose, "Ur", seesAll: true, byOrder: false));
    }

    // ------------------------------------------------------------------ the ledger and the replay

    [Fact]
    public void TheOrderTheUiQueued_MovesExactlyThePeopleAndGrainThePredicatesFunctionsName_KeyForKey()
    {
        UiSession s = Fresh();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("ledger"), Assets());
        int source = ChooseASite(h);
        int site = LegalSite(s, source);
        (double x, double y) = Aim(h, site);
        h.Click(x, y);
        Assert.True(h.ClickControl("found-confirm"));
        OrderRecord order = Assert.Single(s.QueuedOrders());

        // The UI's order, delivered by ColonizationSystem ALONE (nothing else can move a person or a grain), from the world
        // the player saw: the party and the provisions are FoundingQuery's AllocateParty / ProvisionsShare on PREV (departure
        // is live, and with nothing else in the step the live tables are PREV's), moved key for key by the Ledger.
        WorldState prev = s.World.Clone();
        var src = new SettlementId(source);
        var grain = new GoodId(s.Config.Goods!.GrainId);
        var plan = new long[prev.Buckets.Count];
        long party = FoundingQuery.AllocateParty(prev.Buckets, src, FoundingQuery.PartyFraction(s.Config), plan);
        long provisions = FoundingQuery.ProvisionsShare(prev.GoodStocks, prev.Buckets, src, grain, party);
        Assert.True(party >= 1 && provisions >= 1, "vacuous rig");
        string detail = AvailableActionsQuery.For(prev, s.Config, Me).Single(a => a.Domain == ActionDomain.Colonization && a.Id == source).Detail!;
        Assert.StartsWith(party.ToString(System.Globalization.CultureInfo.InvariantCulture) + " people and "
            + provisions.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ", detail);   // the numbers the player was shown

        var log = new OrderLog();
        log.Append(order);
        var alone = new TurnExecutor(UiSession.ProductionEra(), [SystemCatalog.Colonization(s.Config, UiSession.ProductionWorldgen())], log);
        WorldState next = alone.Step(prev.Clone());
        int colony = SettlementAt(next, site);
        Assert.True(colony >= 0);
        var colonyId = new SettlementId(colony);
        Assert.Equal(People(prev), People(next));
        Assert.Equal(Grain(prev, grain), Grain(next, grain));
        Assert.Equal(party, People(next, colonyId));
        Assert.Equal(People(prev, src) - party, People(next, src));
        Assert.Equal(provisions, Grain(next, colonyId, grain));
        Assert.Equal(Grain(prev, src, grain) - provisions, Grain(next, src, grain));
        Assert.True(ConservationAuditor.IsConserved(next, s.Config.Goods, out string report), report);
        int moved = 0;
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            BucketRow b = prev.Buckets[i];
            if (b.Settlement != src) continue;
            Assert.Equal(b.Count.Value - plan[i], next.Buckets[i].Count.Value);
            long arrived = -1;
            for (int j = prev.Buckets.Count; j < next.Buckets.Count; j++)
            {
                BucketRow c = next.Buckets[j];
                if (c.Settlement == colonyId && c.Culture == b.Culture && c.Religion == b.Religion && c.Class == b.Class && c.CohortIdx == b.CohortIdx)
                { arrived = c.Count.Value; break; }
            }
            Assert.Equal(plan[i], arrived);
            if (plan[i] > 0) moved++;
        }
        Assert.True(moved > 1, "vacuous: the party came from one bucket");
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void AUiQueuedOrder_ReplaysHashForHash_FromTheFoundedWorld()
    {
        UiSession s = Fresh();
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("replay"), Assets());
        Assert.True(h.ClickControl("end-turn"));   // turn 1: the order is stamped with a turn other than 0
        int source = ChooseASite(h);
        int site = LegalSite(s, source);
        (double x, double y) = Aim(h, site);
        h.Click(x, y);
        Assert.True(h.ClickControl("found-confirm"));
        Assert.Equal(1L, Assert.Single(s.QueuedOrders()).Turn);
        var hashes = new List<string> { Hash(s) };
        for (int t = 0; t < 3; t++) { Assert.True(h.ClickControl("end-turn")); hashes.Add(Hash(s)); }
        Assert.True(SettlementAt(s.World, site) >= 0);

        // The log the UI wrote passes replay validation and reproduces every turn's hash from the founded world.
        WorldState world = Canonical.Value.Clone();
        OrderValidation.ValidateAgainstWorld(s.Orders, world);
        TurnExecutor exec = UiSession.BuildProductionExecutor(s.Orders);
        world = exec.Step(world);   // turn 0 -> 1 (the first End Turn, no orders)
        Assert.Equal(hashes[0], WorldHash.ComputeHex(world));
        Assert.True(SettlementAt(world, site) < 0);   // not before its delivery
        for (int t = 1; t < hashes.Count; t++)
        {
            world = exec.Step(world);
            Assert.Equal(hashes[t], WorldHash.ComputeHex(world));
            Assert.True(SettlementAt(world, site) >= 0);   // founded in the state of turn 2 = stamp 1 + 1, and after
        }
        Assert.Empty(h.Problems);
    }

    // ------------------------------------------------------------------ m5w integration: saved by the game, resumed

    /// <summary>
    /// M5 workstream integration (S1d x S0 x P10): Found Settlement orders PLACED THROUGH THE LIVE UI — the action surface's
    /// "Choose a site...", a click on the map, Confirm — are saved the way the GAME saves them (GameUi writes the order log,
    /// trace and chronicle after every Confirm and End Turn; the manifest once at launch, as Program.cs does) and the
    /// session is resumed by <see cref="UiSession.Resume"/>. The resumed session must be hash-identical to the live one,
    /// carry the same log, queue, trace, annals, colony names and P10 'now usable' history, and continue identically —
    /// including the second UI-placed order, confirmed at the save turn and not yet played (re-queued by the resume and
    /// delivered by the next End Turn of both sessions, the live one through its End Turn button).
    /// </summary>
    [Fact]
    public void UiPlacedFoundSettlementOrders_SavedByTheGame_ResumeHashIdentically_AndTheQueuedOneFoundsInBoth()
    {
        UiSession live = UiSession.Start(42);
        using UiFrameHarness h = UiFrameHarness.Start(live, WorkDir("resume"), Assets());
        live.ExportManifest("2026-10-11 12:00:00", h.SessionLogPath);   // as Program.cs does, once, at launch
        Assert.True(h.ClickControl("end-turn"));   // turn 1: the first order is stamped with a turn other than 0

        // ORDER 1, placed in the UI at turn 1 and played.
        int source = ChooseASite(h);
        int site = LegalSiteKnown(live, source);
        (double x, double y) = Aim(h, site);
        h.Click(x, y);
        Assert.True(h.ClickControl("found-confirm"));
        Assert.Equal(1L, Assert.Single(live.QueuedOrders()).Turn);
        for (int t = 0; t < 3; t++) Assert.True(h.ClickControl("end-turn"));
        Assert.True(SettlementAt(live.World, site) >= 0, "the UI-placed order founded nothing");

        // ORDER 2, placed in the UI at the save turn and NOT played: the game's own save after Confirm holds it.
        int source2 = ChooseASite(h);
        int site2 = LegalSiteKnown(live, source2);
        (x, y) = Aim(h, site2);
        h.Click(x, y);
        Assert.True(h.ClickControl("found-confirm"));
        OrderRecord queued = Assert.Single(live.QueuedOrders());
        Assert.Equal(OrderKind.FoundSettlement, queued.Kind);
        Assert.Equal(live.World.Clock.Turn, queued.Turn);
        Assert.True(SettlementAt(live.World, site2) < 0);
        int founds = 0;
        for (int i = 0; i < live.Orders.Count; i++) if (live.Orders[i].Kind == OrderKind.FoundSettlement) founds++;
        Assert.Equal(2, founds);

        // RESUME from the files the game wrote (no save call of the test's own).
        UiSession resumed = UiSession.Resume(UiSession.ManifestPath(h.SessionLogPath), out string resumedLog);
        Assert.Equal(Path.GetFullPath(h.SessionLogPath), Path.GetFullPath(resumedLog));
        Assert.Empty(resumed.ResumeNotice);   // same schema: every replayed turn was checked against the saved trace
        Assert.Equal(live.World.Clock.Turn, resumed.World.Clock.Turn);
        Assert.Equal(Hash(live), Hash(resumed));
        Assert.Equal(live.Orders.Count, resumed.Orders.Count);
        for (int i = 0; i < live.Orders.Count; i++) Assert.Equal(live.Orders[i], resumed.Orders[i]);
        Assert.Equal(live.QueuedOrders().ToArray(), resumed.QueuedOrders().ToArray());
        Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
        Assert.Equal(live.AnnalLines.ToArray(), resumed.AnnalLines.ToArray());
        Assert.Equal(live.NowUsableHistory.CanonicalLines().ToArray(), resumed.NowUsableHistory.CanonicalLines().ToArray());
        Assert.Equal(live.World.Settlements.Count, resumed.World.Settlements.Count);
        for (int i = 0; i < live.World.Settlements.Count; i++)
        {
            int id = live.World.Settlements[i].Id.Value;
            Assert.Equal(live.Names.Name(id), resumed.Names.Name(id));
        }

        // CONTINUED PLAY: the re-queued UI-placed order founds in BOTH sessions on the next End Turn, identically.
        for (int t = 0; t < 2; t++)
        {
            Assert.True(h.ClickControl("end-turn"));
            resumed.EndTurn();
            Assert.Equal(Hash(live), Hash(resumed));
            if (t == 0)
            {
                Assert.True(SettlementAt(live.World, site2) >= 0, "the queued UI-placed order founded nothing (live)");
                Assert.True(SettlementAt(resumed.World, site2) >= 0, "the re-queued UI-placed order founded nothing (resumed)");
            }
        }
        Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
        Assert.Equal(live.NowUsableHistory.CanonicalLines().ToArray(), resumed.NowUsableHistory.CanonicalLines().ToArray());
        Assert.Empty(h.Problems);
    }
}

internal static class FoundingSessionExtensions
{
    /// <summary>The action surface for a session at its capital (A1 theme), as the game builds it.</summary>
    public static ActionSurfaceModel Actions(this UiSession s) =>
        ActionSurface.ForSession(s, UiSession.ProductionEra(),
            EmpireQuery.TryGetCapital(s.World, UiPlayer.Empire, out SettlementId c) ? c.Value : -1,
            Sim.Ui.Theme.EraThemes.For(Sim.Ui.Theme.UiEras.Of(s.World, s.Config.Ages, UiPlayer.Empire)));
}
