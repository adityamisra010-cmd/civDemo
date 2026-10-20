using System.Globalization;
using System.Text.RegularExpressions;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Ui.Actions;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-055 (2026-10-16; the Director's batch-4 instruction, item 1; batch-4 design §1.6 and the UI tests of §1.8; track T1b) —
/// MATERIAL RESERVATIONS AT THE UI. One term: a project's units are "SET ASIDE" for it ("held" keeps possession and the
/// building/roads split). A head waiting on an earlier head's set-aside names the holder, where it stands in the building
/// order ("ordered earlier (turn T)" only when its EnqueuedTurn is lower, T = EnqueuedTurn + 1, omitted for a migrated −1;
/// "ordered the same turn, first in the building order" on a tie), the holder's own reason and the remedy; the holder's
/// row says what it sets aside; the project card's Met lines and the Build plate's "will wait for:" count the set-asides
/// exactly when the head's status says HeldForEarlier; the roads preview counts what building work sets aside and names
/// the projects; the EMPIRE view sums the viewer's OWN set-asides; no new line carries '%' in the numeral-free eras; the
/// roles are Body for the waiting status, Caption for the roads line. Every arm has its control: the hold rule off
/// (<see cref="PreHolds"/> = Sim.Tests' TestConfigs.PreReservations, the same one-field change), batch 3's words exactly.
///
/// THE RIG: the dev-size founded world (seed 42, 256 px, four settlements, the player alone) with the trackway known and one
/// End Turn played (Clock.Turn 1), every timber and stone row of the player's at 0. Q17: settlement A (the lower id) holds a
/// part-built granary ordered at turn <c>holderTurn</c> (p = 437,500: 18 of 40 timber, 9 of 20 stone in — 22 and 11 to
/// go) with no builders, a valid wait that SETS ASIDE what it still needs; B (the next id) holds a granary ordered at
/// <c>youngerTurn</c> with every adult building.
/// </summary>
public partial class ConstructionHoldsUiTests
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const int Granary = 1;
    private const long Part = 437_500;
    private static readonly SimConfig Cfg = UiFounding.ProductionConfig();
    private static readonly SimConfig PreHolds = Cfg with { Construction = Cfg.Construction! with { Reservations = false } };
    private static readonly int Timber = Cfg.Goods!.IdOf("timber"), Stone = Cfg.Goods!.IdOf("stone"), Tools = Cfg.Goods!.IdOf("tools");
    private static readonly EraTable Era = UiSession.ProductionEra();

    private static string Inv(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static readonly Lazy<WorldState> RoadWorld = new(() =>
    {
        WorldState w = UiFounding.Found(42, 256, 4);
        ResearchContent research = Cfg.Research!;
        foreach (string id in ActionSurfacePreview.WithAncestors(research, "track_road"))
            w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId(id)].Key));
        UiSession s = UiSession.StartFrom(w, 42, 256, 4);
        s.EndTurn();
        return s.World.Clone();
    });

    // ================================================================== helpers

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

    private static void Share(WorldState w, SettlementId s, double construction)
    {
        var row = new SectorAllocationRow(s, Farming: 1.0 - construction, Herding: 0.0, Extraction: 0.0, Crafting: 0.0, Construction: construction);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) { w.SectorAllocations[i] = row; return; }
        w.SectorAllocations.Add(row);
    }

    /// <summary>A committed entry at <paramref name="s"/>'s next slot, with its progress row (EnqueuedTurn, p) unless
    /// <paramref name="row"/> is false (a migrated v32 entry: EnqueuedTurn −1, p = 0).</summary>
    private static void Queue(WorldState w, SettlementId s, long turn, long progress = 0, bool row = true, int project = Granary)
    {
        int slot = ConstructionQuery.NextSlot(w.ConstructionQueue, s);
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, slot, project));
        if (row) w.ConstructionProgress.Add(new ConstructionProgressRow(s, slot, project, turn, progress));
    }

    private static SettlementId[] Mine(IReadOnlyWorldState w, PolityId polity)
    {
        SettlementId[] mine = LabourActivities.ControlledSettlements(w, polity);
        var sorted = (SettlementId[])mine.Clone();
        Array.Sort(sorted, static (x, y) => x.Value.CompareTo(y.Value));
        return sorted;
    }

    private sealed record Rig(UiSession S, SettlementId A, SettlementId B);

    private static Rig Q17(long holderTurn, long youngerTurn, long timber, long stone, bool holderRow = true, long progress = Part,
        bool younger = true, int? age = null, Action<WorldState, SettlementId>? more = null, long youngerProgress = 0)
    {
        WorldState w = RoadWorld.Value.Clone();
        if (age is int a) w = EraPreview.WorldAt(w, Cfg.Ages!, Me, a);
        SettlementId[] mine = Mine(w, Me);
        Assert.True(mine.Length >= 3, "the rig needs three of the player's settlements");
        Assert.Equal(0, w.ConstructionQueue.Count);
        foreach (SettlementId s in mine) { Set(w, s, Timber, 0); Set(w, s, Stone, 0); }
        SettlementId holder = mine[0], waiting = mine[1];
        Queue(w, holder, holderTurn, progress, holderRow);
        Share(w, holder, 0.0);
        if (younger)
        {
            Queue(w, waiting, youngerTurn, youngerProgress);
            Share(w, waiting, 1.0);
        }
        Set(w, holder, Timber, timber);
        Set(w, holder, Stone, stone);
        more?.Invoke(w, holder);
        return new Rig(UiSession.StartFrom(w, 42, 256, 4), holder, waiting);
    }

    private static double Dt(IReadOnlyWorldState w) => ActionQueryContext.ForNextStep(w, Era).NextDtYears!.Value;

    private static EraTheme ThemeOf(IReadOnlyWorldState w) => EraThemes.For(UiEras.Of(w, Cfg.Ages, Me));

    private static ActionSurfaceModel Surface(UiSession s, SettlementId at, SimConfig? cfg = null) =>
        ActionSurface.Build(new ActionSurfaceInput(s.World, null, cfg ?? s.Config, Era, Me, at.Value, s.QueuedOrders(), s.Names.Name, ThemeOf(s.World)));

    private static (ActionSurfaceModel Model, List<TextCmd> Texts, EraTheme Theme) Paint(UiSession s, SettlementId at, SimConfig? cfg = null)
    {
        EraTheme th = ThemeOf(s.World);
        ActionSurfaceModel model = Surface(s, at, cfg);
        var screen = new ActionSurfaceScreen { Theme = th };
        screen.Refresh(model, s.World, cfg ?? s.Config, Me, id => s.Names.Name(id));
        var d = new DrawList();
        screen.Paint(d, ApproxTextMeasure.Instance, 0, 0, 452);
        return (model, d.Commands.OfType<TextCmd>().ToList(), th);
    }

    private static string All(List<TextCmd> texts) => string.Join(" ", texts.Select(t => t.Text));

    private static QueueEntry HeadOf(ActionSurfaceModel m) => m.Construction!.Queue.First(q => q.Head);

    /// <summary>The construction sector's label as the surface derives it (LabourActivities.CapabilityLabel, else "Building").</summary>
    private static string Label(IReadOnlyWorldState w)
    {
        ResearchContent research = Cfg.Research!;
        string label = LabourActivities.CapabilityLabel(research, ResearchQuery.CompletedMask(w, research, Me), Sectors.Construction);
        return label.Length == 0 ? "Building" : label;
    }

    private static HeadStatusReport Status(UiSession s, SettlementId at, SimConfig? cfg = null) =>
        ConstructionQuery.HeadStatus(s.World, cfg ?? s.Config, at, s.QueuedOrders(), Dt(s.World))!;

    private static InfoCard Card(UiSession s, SettlementId at, SimConfig? cfg = null) =>
        InfoQuery.Card(s.World, cfg ?? s.Config, Me, InfoSubject.OfProject(Granary, at.Value),
            ActionQueryContext.ForNextStep(s.World, Era, s.QueuedOrders()), s.Names.Name);

    private static InfoLink Requirement(InfoCard card, string label) => card.Requirements.First(l => l.Label == label);

    private static IEnumerable<string> EmpireLines(IReadOnlyWorldState w, SimConfig cfg, Func<int, string> name) =>
        PlayerViews.Empire(w, cfg, Me, name, 3).Blocks.SelectMany(b => b.Lines);

    // ================================================================== the waiting head's line

    /// <summary>
    /// THE WAITING HEAD NAMES THE HOLDER, ITS REASON AND THE REMEDY (review L4-09). A's granary (ordered at turn 0, shown as
    /// turn 1) has no builders and sets aside the empire's 22 timber and 11 stone; B's (ordered at turn 1) waits on it. B's
    /// row: "waiting for timber: the empire's 22 are set aside for A's Granary, ordered earlier (turn 1), ahead in the
    /// building order - it has no builders (give Building a share in Labour at A, or cancel it)" — the status, the head's
    /// blocker and the painted line. A's own row keeps its status and gains "sets aside 22 timber and 11 stone while it
    /// waits; projects ordered later wait for it". CONTROL (the hold rule off): B takes the units and works; no set-aside
    /// line. Mutants: the holder's reason dropped; "ordered earlier" for any holder (a tie included).
    /// </summary>
    [Fact]
    public void AHeadWaitingOnAnEarlierSetAside_NamesTheHolder_ItsReason_AndTheRemedy_OrderedEarlierOnItsTurn()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 11);
        string a = r.S.Names.Name(r.A.Value), label = Label(r.S.World);
        HeadStatusReport status = Status(r.S, r.B);
        Assert.Equal((MaterialCause.HeldForEarlier, (QueueEntryRef?)new QueueEntryRef(r.A, 0, Granary), 22L, 22L),
            (status.Cause, status.Holder, status.SetAsideAhead, status.HeldAtStart));                  // PREMISE

        (ActionSurfaceModel m, List<TextCmd> texts, _) = Paint(r.S, r.B);
        string expected = "waiting for timber: the empire's 22 are set aside for " + a + "'s Granary, ordered earlier (turn 1), ahead in "
            + "the building order - it has no builders (give " + label + " a share in Labour at " + a + ", or cancel it)";
        Assert.Equal(expected, HeadOf(m).Status);
        Assert.Equal(expected, m.Construction!.HeadBlocker);
        Assert.Contains(expected, All(texts), StringComparison.Ordinal);
        Assert.Null(HeadOf(m).SetAside);                                   // everything is set aside AHEAD of it

        (ActionSurfaceModel ma, List<TextCmd> ta, _) = Paint(r.S, r.A);
        Assert.Equal("waiting: no builders here - give " + label + " a share in Labour", HeadOf(ma).Status);
        Assert.Equal("sets aside 22 timber and 11 stone while it waits; projects ordered later wait for it", HeadOf(ma).SetAside);
        Assert.Contains(HeadOf(ma).SetAside!, All(ta), StringComparison.Ordinal);

        // CONTROL: the hold rule off (batch 3) — B builds from the units, A sets nothing aside.
        ActionSurfaceModel pre = Surface(r.S, r.B, PreHolds);
        Assert.StartsWith("work goes on at End Turn", HeadOf(pre).Status, StringComparison.Ordinal);
        Assert.Null(HeadOf(Surface(r.S, r.A, PreHolds)).SetAside);
    }

    /// <summary>
    /// A TIE (the holder ordered on the same turn, first in the building order by its lower settlement id) is not "ordered
    /// earlier": "…, ordered the same turn, first in the building order - …". A MIGRATED holder (no progress row:
    /// EnqueuedTurn −1, first in the building order, p = 0, its whole 40 and 20 set aside) is ordered earlier with no turn
    /// shown — never "turn 0". Mutants: T shown as EnqueuedTurn (not + 1); the −1 turn shown.
    /// </summary>
    [Fact]
    public void ATie_IsOrderedTheSameTurn_FirstInTheBuildingOrder_AMigratedHolderShowsNoTurn()
    {
        Rig tie = Q17(holderTurn: 1, youngerTurn: 1, timber: 22, stone: 11);
        string a = tie.S.Names.Name(tie.A.Value), label = Label(tie.S.World);
        string remedy = " - it has no builders (give " + label + " a share in Labour at " + a + ", or cancel it)";
        Assert.True(tie.A.Value < tie.B.Value, "PREMISE: the holder is first on the tie by its settlement id");
        Assert.Equal("waiting for timber: the empire's 22 are set aside for " + a + "'s Granary, ordered the same turn, first in the building order" + remedy,
            HeadOf(Surface(tie.S, tie.B)).Status);

        Rig migrated = Q17(holderTurn: 0, youngerTurn: 1, timber: 40, stone: 20, holderRow: false, progress: 0);
        Assert.Equal(-1L, ConstructionQuery.EnqueuedTurnOf(migrated.S.World.ConstructionProgress, migrated.S.World.ConstructionQueue[0]));
        Assert.Equal("waiting for timber: the empire's 40 are set aside for " + a + "'s Granary, ordered earlier, ahead in the building order" + remedy,
            HeadOf(Surface(migrated.S, migrated.B)).Status);
        Assert.Equal("sets aside 40 timber and 20 stone while it waits; projects ordered later wait for it", HeadOf(Surface(migrated.S, migrated.A)).SetAside);
    }

    /// <summary>
    /// A HOLDER THAT IS BEING BUILT at a labour pace (A's construction share set so its builders give exactly 0.5 adult-years:
    /// p 437,500 → 687,500, drawing 10 timber and 5 stone) keeps the rest of its need, 12 and 6: B's line says "12 of the
    /// empire's 22 are set aside" and (T1b fix round 2026-10-17, finding F7) that the other 10 are used this turn by the head
    /// ahead of it, and the remedy is A's labour ("give Building a larger share in Labour at A to finish it sooner, or cancel
    /// it"); A's own row "sets aside 12 timber and 6 stone for the rest of its work; …".
    /// </summary>
    [Fact]
    public void AHolderBeingBuilt_SetsAsideTheRestOfItsNeed_AndTheRemedyIsItsLabour()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 11, more: (w, holder) =>
        {
            for (int i = 0; i < w.Housing.Count; i++) if (w.Housing[i].Settlement == holder) w.Housing.Ref(i).LastLaborUsed = 0.0;
            double adults = InstitutionStaffing.LabourAdults(w, Cfg, holder);
            Share(w, holder, 0.5 / (adults * Dt(w)));
        });
        HeadStatusReport holder = Status(r.S, r.A);
        Assert.True(holder.Resolution.Progresses, "PREMISE: the holder is being built");
        Assert.Equal((12L, 6L), (holder.SetAside![Timber], holder.SetAside[Stone]));
        string a = r.S.Names.Name(r.A.Value), label = Label(r.S.World);
        Assert.Equal("waiting for timber: 12 of the empire's 22 are set aside for " + a + "'s Granary, ordered earlier (turn 1), ahead in the building "
            + "order, and 10 are used this turn by projects ahead of it - it is being built a little each turn (give " + label + " a larger share in "
            + "Labour at " + a + " to finish it sooner, or cancel it)",
            HeadOf(Surface(r.S, r.B)).Status);
        Assert.Equal("sets aside 12 timber and 6 stone for the rest of its work; projects ordered later wait for it", HeadOf(Surface(r.S, r.A)).SetAside);
    }

    /// <summary>
    /// THE HOLDER'S REASON AND ITS REMEDY, for every wait a holder can be in (HoldText.Reason on the holder's own status; a
    /// released head never holds, so "no one can work" and "no longer known" are defensive), and the waiting line's
    /// "and other projects" when the earliest holder sets aside fewer units than are set aside ahead. Mutant: one remedy
    /// swapped for another.
    /// </summary>
    [Fact]
    public void TheHoldersReason_NamesItsOwnWait_WithTheRemedyForIt()
    {
        ConstructionProjectEntry granary = Cfg.Goods!.ProjectById(Granary)!;
        var entry = new ConstructionQueueRow(new SettlementId(0), 0, Granary);
        HeadStatusReport Of(HeadLimitKind limit, bool noAdults = false, int good = -1, long after = 0) =>
            new(new HeadResolution(entry, granary, 0, after, [0, 0], 0.0, limit, good, 0, noAdults), MaterialCause.None, [], 0, 0, []);
        string? Reason(HeadStatusReport h) => HoldText.Reason(h, Cfg, "Alpha", "Building");
        Assert.Equal("it has no builders (give Building a share in Labour at Alpha, or cancel it)", Reason(Of(HeadLimitKind.Labour)));
        Assert.Equal("no one at Alpha can work (cancel it)", Reason(Of(HeadLimitKind.Labour, noAdults: true)));
        Assert.Equal("Alpha cannot yet sustain it (wait until it can, or cancel it)", Reason(Of(HeadLimitKind.Viability)));
        Assert.Equal("it is itself waiting for stone (cancel it to free them)", Reason(Of(HeadLimitKind.Materials, good: Stone)));
        Assert.Equal("it is itself waiting for tools (cancel it to free them)", Reason(Of(HeadLimitKind.Unobtainable, good: Tools)));
        Assert.Equal("this project is no longer known (cancel it)", Reason(Of(HeadLimitKind.UnknownProject)));
        Assert.Equal("it is being built a little each turn (give Building a larger share in Labour at Alpha to finish it sooner, or cancel it)",
            Reason(Of(HeadLimitKind.None, after: 1_000)));
        Assert.Equal("it is being built a little each turn (give Building a larger share in Labour at Alpha to finish it sooner, or cancel it)",
            Reason(Of(HeadLimitKind.Labour, after: 1_000)));
        // T1b fix round (2026-10-17, finding F1): an ADVANCING holder whose limit is a good is not offered labour.
        Assert.Equal("it is being built only as far as its stone allows (cancel it to free them)", Reason(Of(HeadLimitKind.Materials, good: Stone, after: 1_000)));
        Assert.Equal("it is being built only as far as its tools allows (cancel it to free them)", Reason(Of(HeadLimitKind.Unobtainable, good: Tools, after: 1_000)));
        Assert.Null(Reason(Of(HeadLimitKind.None)));

        // "and other projects": 30 set aside ahead, 12 of them by the earliest holder.
        var waiting = new HeadStatusReport(
            new HeadResolution(new ConstructionQueueRow(new SettlementId(1), 0, Granary), granary, 0, 0, [0, 0], 0.0, HeadLimitKind.Materials, Timber, 40, false),
            MaterialCause.HeldForEarlier, [], 30, 2, [], new QueueEntryRef(new SettlementId(0), 0, Granary), 30);
        var timberOnly = new long[Math.Max(Timber, Stone) + 1];
        timberOnly[Timber] = 12;
        HeadStatusReport holder = Of(HeadLimitKind.Labour) with { SetAside = timberOnly };
        Func<int, string> name = id => id == 0 ? "Alpha" : "Beta";
        Assert.Equal("waiting for timber: the empire's 30 are set aside for Alpha's Granary and other projects, ordered earlier (turn 3), ahead in the "
            + "building order - it has no builders (give Building a share in Labour at Alpha, or cancel it)",
            ConstructionStatusText.Status(waiting, Cfg, true, "Beta", name, "Building", new HoldContext(holder, 2, 5)));
    }

    // ================================================================== the card's Met and the Build plate (L4-01)

    /// <summary>
    /// THE CARD'S Met IS FALSE, AND THE BUILD PLATE SAYS "will wait for", EXACTLY WHEN THE HEAD IS HeldForEarlier (batch-4
    /// design §1.8 UI; review L4-01). The purse holds 50 timber and 25 stone: enough for B's granary alone, not with A's 22
    /// and 11 set aside ahead. Held: B's status is HeldForEarlier, the card's timber and stone lines are not Met (its note
    /// names A's granary), and the Build plate for another granary at B says "will wait for: 40 timber (empire has 50; 22 set
    /// aside for earlier projects), …". A's cancel pending (its set-aside released in the same step) and the hold rule off:
    /// none of the three. Mutant: the holds term dropped from the card's Met and the blocker.
    /// </summary>
    [Theory]
    [InlineData("holds", true)]
    [InlineData("holder cancelled", false)]
    [InlineData("batch 3", false)]
    public void TheCardsMetIsFalse_AndThePlateWillWait_ExactlyWhenTheHeadIsHeldForEarlier(string arm, bool held)
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 50, stone: 25);
        if (arm == "holder cancelled") Assert.True(r.S.EmitCancelConstruction(r.A.Value, 0));
        SimConfig cfg = arm == "batch 3" ? PreHolds : Cfg;
        Assert.Equal(held, Status(r.S, r.B, cfg).Cause == MaterialCause.HeldForEarlier);

        InfoCard card = Card(r.S, r.B, cfg);
        Assert.Equal(!held, Requirement(card, "40 timber").Met);
        Assert.Equal(!held, Requirement(card, "20 stone").Met);

        (ActionSurfaceModel m, List<TextCmd> texts, _) = Paint(r.S, r.B, cfg);
        ProjectEntry plate = m.Construction!.Projects.Single(p => p.ProjectId == Granary);
        Assert.Equal(held, plate.Blocker is not null);
        if (held)
        {
            string a = r.S.Names.Name(r.A.Value);
            Assert.Equal("empire has 50; 22 set aside for " + a + "'s Granary, ahead in the building order", Requirement(card, "40 timber").Note);
            Assert.Equal("needs 40 timber (empire has 50; 22 set aside for earlier projects), 20 stone (empire has 25; 11 set aside for earlier projects)",
                plate.Blocker);
            Assert.Contains("will wait for:", All(texts), StringComparison.Ordinal);
            Assert.Contains("22 set aside for earlier projects", All(texts), StringComparison.Ordinal);
        }
        else Assert.DoesNotContain("set aside for", All(texts) + Requirement(card, "40 timber").Note, StringComparison.Ordinal);
    }

    // ================================================================== the EMPIRE view

    /// <summary>
    /// THE EMPIRE VIEW SUMS THE VIEWER'S OWN SET-ASIDES (the published table: what the last step set aside). After one End
    /// Turn on the Q17 rig the table holds the player's rows, and the Realm block says "Set aside for projects: …" with
    /// exactly their sums (ascending good id); with the hold rule off nothing is said. A hand-written table: the player's
    /// Empire purse (22 timber, 11 stone) and the single-settlement purse of one of its places (5 timber) are summed; another
    /// Empire's purse (purse 7: 99 timber) is never read (P7). Mutant: the purse filter dropped.
    /// </summary>
    [Fact]
    public void TheEmpireView_SumsWhatTheViewersOwnPurseSetsAside_AndNothingElse()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 11);
        Assert.DoesNotContain(EmpireLines(r.S.World, Cfg, r.S.Names.Name), l => l.StartsWith("Set aside for projects", StringComparison.Ordinal));
        r.S.EndTurn();
        var sums = new SortedDictionary<int, long>();
        IReadOnlyTable<ConstructionHoldRow> table = r.S.World.ConstructionHolds;
        for (int i = 0; i < table.Count; i++)
            if (table[i].Purse == Me.Value) sums[table[i].Good] = sums.GetValueOrDefault(table[i].Good) + table[i].Units;
        Assert.True(sums.Count > 0, "anti-vacuity: the step published the player's set-asides");
        string expected = "Set aside for projects: " + string.Join(", ", sums.Select(kv => Inv(kv.Value) + " " + Cfg.Goods!.ById(kv.Key).Name));
        Assert.Contains(expected, EmpireLines(r.S.World, Cfg, r.S.Names.Name));
        Assert.DoesNotContain(EmpireLines(r.S.World, PreHolds, r.S.Names.Name), l => l.StartsWith("Set aside", StringComparison.Ordinal));

        WorldState hand = r.S.World.Clone();
        hand.ConstructionHolds.Clear();
        foreach (ConstructionHoldRow row in new ConstructionHoldRow[]
                 {
                     new(Reservations.SettlementPurse(r.B), Timber, 5), new(Me.Value, Timber, 22), new(Me.Value, Stone, 11), new(7, Timber, 99),
                 })
            hand.ConstructionHolds.Add(row);
        Assert.Null(Reservations.InvariantViolation(hand.ConstructionHolds));
        Assert.Contains("Set aside for projects: 27 timber, 11 stone", EmpireLines(hand, Cfg, r.S.Names.Name));
    }

    // ================================================================== the roads preview

    private static long RoadTimber(IReadOnlyWorldState w)
    {
        long c = 0;
        foreach (RoadDevelopmentStep step in RoadDevelopmentQuery.Plan(w, Cfg.Research!, Cfg.Roads!, Cfg.Goods!, Me, 100.0))
            foreach (RoadMaterialCost m in step.Cost) if (m.Good.Value == Timber) c += m.Units;
        return c;
    }

    /// <summary>
    /// THE ROADS PREVIEW COUNTS WHAT BUILDING WORK SETS ASIDE AND NAMES THE PROJECTS (review L4-10). A's waiting granary sets
    /// aside 22 timber; the purse holds the road's whole timber R plus 10. Under the default split building work takes 22
    /// first — its draws (0) and its set-aside (22) — so "after what building work uses or sets aside, about R − 12 of the R
    /// timber needed is left", the default's warning says "may take up to 22 timber first", and the hold line names A's
    /// granary: "22 timber is set aside for A's Granary; road works may not use it" (painted at the Caption role in the soft
    /// ink). The forecast's heads sum to the split's BuildingHolds. At b = 50 the hold line stands and the default's warning
    /// does not. CONTROL (the hold rule off): nothing is set aside — "after building work, everything this order needs is
    /// left", no warning, no hold line. Mutant: the set-aside not counted in what building work takes.
    /// </summary>
    [Fact]
    public void TheRoadsPreview_CountsWhatBuildingWorkSetsAside_AndNamesTheProjects()
    {
        long road = RoadTimber(RoadWorld.Value);
        Assert.True(road > 22, "anti-vacuity: the road order needs timber");
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: road + 10, stone: 11, younger: false);
        string a = r.S.Names.Name(r.A.Value);
        AllocationBlock block = Surface(r.S, r.A).Allocation!;
        AllocationView view = ResourceAllocationQuery.ForEmpire(r.S.World, Cfg, Me, r.S.QueuedOrders(), Dt(r.S.World), 100.0)!;
        Assert.Equal((0L, 22L), (view.Split.BuildingDrawOf(Timber), view.Split.BuildingHoldOf(Timber)));     // PREMISE
        HoldsWalkHead[] heads = SetAsideQuery.HeadsUnder(r.S.World, Cfg, Me, r.S.QueuedOrders(), Dt(r.S.World), SetAsideQuery.ForecastGrants(view.Split));
        foreach (int g in new[] { Timber, Stone })
            Assert.Equal(view.Split.BuildingHoldOf(g), heads.Sum(h => h.HoldOf(g)));

        RoadPlanPreview p = ActionSurface.RoadPlan(r.S.World, Cfg, Me, 100.0, r.S.Names.Name, block, numerals: false);
        Assert.StartsWith("after what building work uses or sets aside, about " + Inv(road - 12) + " of the " + Inv(road) + " timber needed is left: ",
            p.LeftLine, StringComparison.Ordinal);
        Assert.Equal("Building work may take up to 22 timber first under the current split - change it under Materials", p.Warning);
        Assert.Equal("22 timber is set aside for " + a + "'s Granary; road works may not use it", p.SetAside);

        (_, List<TextCmd> texts, EraTheme th) = Paint(r.S, r.A);
        TextCmd line = texts.First(t => t.Text.Length > 8 && (p.SetAside + ".").StartsWith(t.Text, StringComparison.Ordinal));
        double caption = th.Type.Size(TypeScale.Px(th, TypeRole.Caption));
        Assert.Equal((caption, th.Ink.TextSoft), (line.Size, line.Color));

        RoadPlanPreview pre = ActionSurface.RoadPlan(r.S.World, PreHolds, Me, 100.0, r.S.Names.Name, block, numerals: false);
        Assert.Equal("after building work, everything this order needs is left: all of it would be paid this turn", pre.LeftLine);
        Assert.Null(pre.Warning);
        Assert.Null(pre.SetAside);

        Assert.True(r.S.EmitAllocationOrder(50));
        AllocationBlock half = Surface(r.S, r.A).Allocation!;
        RoadPlanPreview split = ActionSurface.RoadPlan(r.S.World, Cfg, Me, 100.0, r.S.Names.Name, half, numerals: false);
        Assert.Equal("22 timber is set aside for " + a + "'s Granary; road works may not use it", split.SetAside);
        Assert.Null(split.Warning);
    }

    /// <summary>
    /// THE ROADS PREVIEW PAYS FROM THE ISSUER'S OWN PURSE (T1a round 2's open item: the payers road development itself draws
    /// on, ConstructionQuery.EmpirePursePlaces). The one-AI world with a second control row: one of the LOWER-id Empire's places
    /// is also the higher one's, and it alone holds timber (40). The higher Empire's road order on a route between two of its
    /// own places: with the hold rule on that place is the lower Empire's purse's alone — not among the payers, nothing
    /// affordable; with it off (batch 3) it pays first-listed by distance and 40 of the 60 is affordable. Mutant: the preview
    /// back on PayingSettlements.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheRoadsPreview_InADoubleControlWorld_PaysOnlyFromTheIssuersOwnPurse(bool rule)
    {
        WorldState w = UiFounding.Found(42, 256, 4, aiEmpiresOverride: 1);
        PolityId ai = default;
        for (int i = 0; i < w.Polities.Count; i++) if (w.Polities[i].Id.Value != Me.Value) ai = w.Polities[i].Id;
        (PolityId low, PolityId high) = Me.Value < ai.Value ? (Me, ai) : (ai, Me);
        SettlementId shared = Mine(w, low)[0];
        SettlementId[] highs = Mine(w, high);
        Assert.True(highs.Length >= 2, "the rig needs two places of the higher Empire");
        w.Controls.Add(new ControlRow(high, shared, 1.0));
        w.TransportEdges.Clear();
        w.TransportEdges.Add(new TransportEdgeRow(1, highs[0], highs[1], EdgeTypes.DirtPath, TransportModes.Road, TransportEdgeStates.Complete,
            100_000, 60, 0, 0, 0, EdgeTypes.DirtPath, 0.0, 1.0));
        w.SettlementDistances.Clear();
        w.SettlementDistances.Add(new SettlementDistanceRow(highs[0], highs[1], 100.0));
        w.SettlementDistances.Add(new SettlementDistanceRow(highs[1], highs[0], 100.0));
        w.TradeFlows.Clear();
        ResearchContent research = Cfg.Research!;
        foreach (string id in ActionSurfacePreview.WithAncestors(research, "track_road"))
            w.ResearchCompleted.Add(new ResearchCompletedRow(high, research.Nodes[research.IndexOfId(id)].Key));
        for (int s = 0; s < w.Settlements.Count; s++) Set(w, w.Settlements[s].Id, Timber, 0);
        Set(w, shared, Timber, 40);

        SimConfig cfg = rule ? Cfg : PreHolds;
        Func<int, string> name = id => "S" + Inv(id);
        Assert.Contains(shared, RoadDevelopmentQuery.PayingSettlements(w, high));                    // PREMISE: a row for it
        RoadPlanPreview p = ActionSurface.RoadPlan(w, cfg, high, 100.0, name);
        Assert.Equal(1, p.Routes);
        Assert.Equal(!rule, p.Payers.Contains(name(shared.Value)));
        Assert.Equal(rule ? 0.0 : 40.0 / 60.0, p.Affordable, 12);
    }

    // ================================================================== roles, eras, the verbs

    /// <summary>
    /// THE ROLES (review L4-11; IncrementalConstructionUiTests.TheQueuesRoles_… extended to the new lines): the HeldForEarlier
    /// status at the Body size in the Progress text ink (the Second role on this surface, like every waiting reason); the
    /// holder's set-aside line at the Body size in the soft ink; the card's hold note in the requirement's NOTE slot (the
    /// card's caption line); the roads hold line at Caption (pinned in the roads-preview test).
    /// </summary>
    [Fact]
    public void TheQueuesRoles_TheHeldForEarlierStatusIsBodyProgress_TheSetAsideLineBodySoft_TheCardNoteIsTheNote()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 11);
        (ActionSurfaceModel m, List<TextCmd> texts, EraTheme th) = Paint(r.S, r.B);
        double body = th.Type.Size(TypeScale.Px(th, TypeRole.Body)), caption = th.Type.Size(TypeScale.Px(th, TypeRole.Caption));
        Assert.True(caption < body, "anti-vacuity: the Caption role is smaller than the Body role");
        string status = HeadOf(m).Status!;
        Assert.Contains("set aside for", status, StringComparison.Ordinal);
        TextCmd s0 = texts.First(t => t.Text.Length > 8 && status.StartsWith(t.Text, StringComparison.Ordinal));
        Assert.Equal((body, th.TextInk.Progress), (s0.Size, s0.Color));

        (ActionSurfaceModel ma, List<TextCmd> ta, _) = Paint(r.S, r.A);
        string aside = HeadOf(ma).SetAside!;
        TextCmd a0 = ta.First(t => t.Text.Length > 8 && aside.StartsWith(t.Text, StringComparison.Ordinal));
        Assert.Equal((body, th.Ink.TextSoft), (a0.Size, a0.Color));

        InfoLink timber = Requirement(Card(r.S, r.B), "40 timber");
        Assert.DoesNotContain("set aside", timber.Label, StringComparison.Ordinal);
        Assert.Contains("set aside for", timber.Note!, StringComparison.Ordinal);
    }

    /// <summary>Every new hold line of a rig: the waiting status, the holder's row, the Build plate, the card notes, the roads
    /// preview's left, warning and hold lines (in the era's own figures), and the EMPIRE summary.</summary>
    private static List<string> HoldLines(Rig r, bool numerals)
    {
        var lines = new List<string>();
        void Add(string? s) { if (s is not null) lines.Add(s); }
        Add(HeadOf(Surface(r.S, r.B)).Status);
        Add(HeadOf(Surface(r.S, r.A)).SetAside);
        ActionSurfaceModel surface = Surface(r.S, r.B);
        Add(surface.Construction!.Projects.Single(p => p.ProjectId == Granary).Blocker);
        foreach (InfoLink l in Card(r.S, r.B).Requirements) Add(l.Note);
        RoadPlanPreview p = ActionSurface.RoadPlan(r.S.World, Cfg, Me, 100.0, r.S.Names.Name, surface.Allocation, numerals);
        Add(p.LeftLine);
        Add(p.Warning);
        Add(p.SetAside);
        WorldState published = r.S.World.Clone();
        published.ConstructionHolds.Clear();
        published.ConstructionHolds.Add(new ConstructionHoldRow(Me.Value, Timber, 22));
        Add(HoldText.EmpireSummary(published, Cfg, Me));
        return lines;
    }

    /// <summary>
    /// NO '%' IN THE NUMERAL-FREE ERAS (A1–A3; review L4-10): every new hold line, on the Q17 rig moved to Age 1, 2 and 3 (the
    /// surface's own numeral rule: the share control has no numerals there), carries no percent sign. The purse holds 22 +
    /// R/2 timber (R the road's): A sets aside 22 and B (short of stone, all of it set aside by A) 40, so the road order is
    /// paid IN PART — the share the numeral eras would print as a percentage is in words here. Anti-vacuity: the lines are
    /// drawn, one names a holder, and the preview's share is partial. Mutant: the preview's share ignores the era's numerals.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InTheNumeralFreeEras_NoHoldLineCarriesAPercentSign(int age)
    {
        long road = RoadTimber(RoadWorld.Value);
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22 + road / 2, stone: 11, age: age);
        bool numerals = LabourControlSpec.For(ThemeOf(r.S.World)).Numerals;
        Assert.False(numerals, "PREMISE: Age " + Inv(age) + " is numeral-free");
        List<string> lines = HoldLines(r, numerals);
        Assert.True(lines.Count >= 7, "anti-vacuity: the hold lines are drawn (" + Inv(lines.Count) + ")");
        Assert.Contains(lines, l => l.Contains("set aside for", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith(" of it would be paid this turn", StringComparison.Ordinal)
            && !l.Contains("all of it", StringComparison.Ordinal) && !l.Contains("none of it", StringComparison.Ordinal));
        Assert.All(lines, l => Assert.DoesNotContain("%", l, StringComparison.Ordinal));
    }

    private static readonly Regex HoldVerb = new(@"\b(hold|holds|held)\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// THE TWO CAUSES NEVER SHARE A VERB (review L4-08): every new line about a project's reservation says "set aside" and
    /// never "hold"/"held"; the building/roads split's lines keep "held"/"hold" ("road works hold the rest …", "… the rest is
    /// held for road works …") and never say "set aside".
    /// </summary>
    [Fact]
    public void TheTwoCauses_NeverShareAVerb_ProjectsSetAside_RoadWorksHold()
    {
        Rig r = Q17(holderTurn: 0, youngerTurn: 1, timber: 22, stone: 11);
        ActionSurfaceModel atB = Surface(r.S, r.B);
        RoadPlanPreview preview = ActionSurface.RoadPlan(r.S.World, Cfg, Me, 100.0, r.S.Names.Name, atB.Allocation, numerals: true);
        WorldState published = r.S.World.Clone();
        published.ConstructionHolds.Clear();
        published.ConstructionHolds.Add(new ConstructionHoldRow(Me.Value, Timber, 22));
        string?[] reservations =
        [
            HeadOf(atB).Status,                                                           // the waiting head
            HeadOf(Surface(r.S, r.A)).SetAside,                                           // the holder's row
            atB.Construction!.Projects.Single(p => p.ProjectId == Granary).Blocker,      // the Build plate
            Requirement(Card(r.S, r.B), "40 timber").Note,                                // the card's note
            preview.SetAside,                                                             // the roads line
            HoldText.EmpireSummary(published, Cfg, Me),                                   // the EMPIRE summary
            AllocationText.DefaultLine,
            AllocationText.EmpireLine(100, setsAside: true),
        ];
        foreach (string? l in reservations)
        {
            Assert.NotNull(l);
            Assert.True(l!.Contains("set aside", StringComparison.Ordinal) || l.Contains("sets aside", StringComparison.Ordinal)
                || l.Contains("Set aside", StringComparison.Ordinal), "a reservation line without the term: " + l);
            Assert.False(HoldVerb.IsMatch(l), "a set-aside line uses the split's verb: " + l);
        }

        ConstructionProjectEntry granary = Cfg.Goods!.ProjectById(Granary)!;
        var roads = new HeadStatusReport(
            new HeadResolution(new ConstructionQueueRow(r.B, 0, Granary), granary, 0, 0, [0, 0], 0.0, HeadLimitKind.Materials, Timber, 40, false, HeldForRoads: true),
            MaterialCause.HeldForRoads, [], 40, 0, []);
        string split = ConstructionStatusText.Status(roads, Cfg, true, "B", r.S.Names.Name, "Building");
        Assert.True(HoldVerb.IsMatch(split), split);
        Assert.DoesNotContain("set aside", split, StringComparison.Ordinal);
        OrderRecord road = OrderRecord.From(0, Me, OrderKind.DevelopRoads, 0, 100.0);
        var none = new long[Math.Max(Timber, Stone) + 1];
        var view = new AllocationView(Me, 75, 75, true, null, road, false,
            new EmpireAllocation(Me, 75, road, [Timber], [ResourceAllocation.Split(100, 80, 60, 75)], null, none, none));
        string capLine = AllocationText.ConstructionLine(view, Cfg)!;
        Assert.True(HoldVerb.IsMatch(capLine), capLine);
        Assert.DoesNotContain("set aside", capLine, StringComparison.Ordinal);
    }

    // ================================================================== the allocation words, both rules

    /// <summary>
    /// THE ALLOCATION TEXTS, EACH STRING ENUMERATED (batch-4 design §1.6; review L4-10) with its batch-3 words under the
    /// control: the default line, the EMPIRE line at the default, the construction block's no-road and default branches.
    /// The split's own lines (a cap, the starvation guard) are unchanged by the hold rule.
    /// </summary>
    [Fact]
    public void TheAllocationTexts_SayBuildingWorkUsesOrSetsAside_BatchThreeKeepsItsOwnWords()
    {
        Assert.Equal("Building first (default): road works get only what is left after building work uses or sets aside what it needs.",
            AllocationText.DefaultLineFor(Cfg));
        Assert.Equal("Building first (default): road works get only what building work leaves.", AllocationText.DefaultLineFor(PreHolds));
        Assert.Equal("Materials when roads are ordered: building first (default) - road works get only what is left after building work uses or sets aside what it needs",
            AllocationText.EmpireLine(100, setsAside: true));
        Assert.Equal("Materials when roads are ordered: building first (default) - road works get only what building work leaves",
            AllocationText.EmpireLine(100, setsAside: false));
        Assert.Equal(AllocationText.EmpireLine(70, true), AllocationText.EmpireLine(70, false));

        OrderRecord road = OrderRecord.From(0, Me, OrderKind.DevelopRoads, 0, 100.0);
        var none = new long[Math.Max(Timber, Stone) + 1];
        AllocationView View(int b, OrderRecord? order) => new(Me, b, b, true, null, order, false,
            new EmpireAllocation(Me, b, order, [Timber], [ResourceAllocation.Split(100, 80, order is null ? 0 : 60, b)], null, none, none));
        Assert.Equal("With no road works ordered this turn, building work may use or set aside everything it needs, oldest order first.",
            AllocationText.ConstructionLine(View(100, null), Cfg));
        Assert.Equal("With no road works ordered this turn, building work may use everything it needs.", AllocationText.ConstructionLine(View(100, null), PreHolds));
        Assert.Equal("Building first (default): this turn building work may use or set aside everything it needs, oldest order first, and road works get what is left.",
            AllocationText.ConstructionLine(View(100, road), Cfg));
        Assert.Equal("Building first (default): this turn building work may use everything it needs, and road works get what it leaves.",
            AllocationText.ConstructionLine(View(100, road), PreHolds));
        Assert.Equal(AllocationText.ConstructionLine(View(75, road), PreHolds), AllocationText.ConstructionLine(View(75, road), Cfg));
    }
}
