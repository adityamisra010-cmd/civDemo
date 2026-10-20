using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Headless;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// Director playtest 2026-10-20 — A RESOURCE ONCE UNLOCKED STAYS LISTED, AND READS 0 AT ZERO; THE NEXT-TURN FIGURE IS THE
/// SIMULATION'S OWN. The defect: <see cref="EmpireStores.IsShown"/> showed a good only while it was held now, held at the
/// start of the last turn, or set aside, so a good that ran out vanished one turn later. The fix: the game's
/// <see cref="ResourceCatalogue"/> (UI-side, rebuilt by replay, empty in a new game) keeps every good the player's empire
/// has held. The next-turn figure is <see cref="UiSession.PreviewNextWorld"/> — the production step on the current world
/// with this turn's queued orders, without the AI's — so it equals the next turn's stores exactly, construction charges
/// counted once. The rig is real play: pottery, cloth and tools seeded at the founding capital are eaten by the people in
/// the first turn (needs.json) and the Stone Age player cannot make more.
/// </summary>
[Collection("ImGui context")]
public sealed class ResourcesPersistenceTests : IDisposable
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const int Timber = 4, Stone = 5, Fiber = 9, Bronze = 11, Tools = 12, Pottery = 13, Cloth = 14;
    private const int Granary = 1;

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-resources-persist-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static readonly Lazy<WorldState> Founding = new(() => UiSession.Start(42).World, LazyThreadSafetyMode.ExecutionAndPublication);

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

    private static SettlementId Capital(IReadOnlyWorldState w)
    {
        Assert.True(EmpireQuery.TryGetCapital(w, Me, out SettlementId capital));
        return capital;
    }

    /// <summary>The canonical founding world with 5 pottery, cloth and tools at the player's capital.</summary>
    private static UiSession Seeded()
    {
        WorldState w = Founding.Value.Clone();
        SettlementId capital = Capital(w);
        foreach (int g in new[] { Pottery, Cloth, Tools }) Set(w, capital, g, 5);
        return UiSession.StartFrom(w, 42);
    }

    /// <summary>The independent oracle: every GoodStocks row of <paramref name="good"/> whose settlement the player controls.</summary>
    private static long Own(IReadOnlyWorldState w, int good)
    {
        long sum = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == good && EmpireQuery.ControlsSettlement(w, Me, w.GoodStocks[i].Settlement))
                sum = checked(sum + w.GoodStocks[i].Amount.Value);
        return sum;
    }

    private static EmpireStores Stores(UiSession s, bool next = false) =>
        EmpireStores.Build(s.World, s.PreviousWorld, s.Config, Me, s.Unlocked, next ? s.PreviewNextWorld() : null);

    // ================================================================== persistence

    [Fact]
    public void AGoodUnlockedEarlier_StaysListed_AtZero_OnEveryLaterTurn_WhereTheOldRuleDroppedIt()
    {
        UiSession s = Seeded();
        foreach (int g in new[] { Pottery, Cloth, Tools }) Assert.Equal(5, Own(s.World, g));   // PREMISE: held at the founding
        for (int t = 1; t <= 6; t++)
        {
            s.EndTurn();
            EmpireStores st = Stores(s);
            foreach (int id in new[] { Pottery, Cloth, Tools })
            {
                StoreGood g = st.Good(id)!;
                Assert.Equal(0, Own(s.World, id));                       // PREMISE: real play ate it all
                Assert.Equal(0, g.Total);                                // exactly 0, a figure
                Assert.True(g.Unlocked);
                Assert.True(EmpireStores.IsShown(g));
                Assert.Contains(st.Shown, x => x.Good == id);
                Assert.Contains(st.Group(StoreGroup.Goods)!.Goods, x => x.Good == id);
                // The old rule (held now / at the start of last turn / set aside) drops it from the second turn on.
                if (t >= 2) Assert.False(EmpireStores.IsShown(g with { Unlocked = false }), "turn " + t + ": premise void");
            }
        }
    }

    [Fact]
    public void AGoodListedAtZero_StaysListed_AfterFurtherStockChanges()
    {
        UiSession s = Seeded();
        for (int t = 0; t < 3; t++) s.EndTurn();
        WorldState w = s.World.Clone();
        SettlementId capital = Capital(w);
        Set(w, capital, Pottery, 7);
        StoreGood up = EmpireStores.Build(w, s.World, s.Config, Me, s.Unlocked).Good(Pottery)!;
        Assert.Equal((7L, 7L), (up.Total, up.Change));
        Assert.True(EmpireStores.IsShown(up));
        WorldState back = w.Clone();
        Set(back, capital, Pottery, 0);
        StoreGood down = EmpireStores.Build(back, back, s.Config, Me, s.Unlocked).Good(Pottery)!;   // 0 now and "last turn"
        Assert.Equal((0L, 0L), (down.Total, down.Previous));
        Assert.True(EmpireStores.IsShown(down));
        // Play on: other stores keep changing, pottery stays listed at 0.
        for (int t = 0; t < 3; t++)
        {
            long grainBefore = Own(s.World, s.Config.Goods!.GrainId);
            s.EndTurn();
            Assert.NotEqual(grainBefore, Own(s.World, s.Config.Goods!.GrainId));
            Assert.Contains(Stores(s).Shown, x => x.Good == Pottery && x.Total == 0);
        }
    }

    /// <summary>The catalogue is exactly the goods the player's empire held at the founding or after some step (an oracle
    /// tracked turn by turn over the raw rows) — so a good never held (fiber, bronze here) is never listed.</summary>
    [Fact]
    public void AGoodNeverUnlocked_IsNotListed_AndTheCatalogueIsExactlyTheGoodsEverHeld()
    {
        UiSession s = Seeded();
        var ever = new SortedSet<int>();
        void Track() { foreach (GoodEntry g in s.Config.Goods!.Goods) if (Own(s.World, g.Id) > 0) ever.Add(g.Id); }
        Track();
        for (int t = 0; t < 5; t++)
        {
            s.EndTurn();
            Track();
            Assert.Equal(ever.ToArray(), s.Unlocked.Unlocked.ToArray());
            EmpireStores st = Stores(s);
            foreach (int id in new[] { Fiber, Bronze })
            {
                Assert.DoesNotContain(id, ever);                         // PREMISE: never held
                Assert.False(st.Good(id)!.Unlocked);
                Assert.DoesNotContain(st.Shown, x => x.Good == id);
            }
        }
        Assert.Contains(Pottery, ever);
    }

    /// <summary>A rival's stores never unlock a good for the player: a catalogue capture reads the player's own
    /// settlements only, however much bronze the rival holds.</summary>
    [Fact]
    public void ARivalsStores_NeverUnlockAGood()
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: 1);
        s.EndTurn();
        WorldState w = s.World.Clone();
        IReadOnlyList<int> rival = KnowledgeStubs.SettlementsOf(w, PlayabilityGate.HiddenPolity);
        Assert.NotEmpty(rival);
        foreach (int id in rival) Set(w, new SettlementId(id), Bronze, 500);
        var catalogue = new ResourceCatalogue();
        catalogue.Capture(w, s.Config, Me);
        Assert.False(catalogue.IsUnlocked(Bronze));
        // CONTROL: the same capture unlocks bronze held by the player.
        Set(w, Capital(w), Bronze, 1);
        catalogue.Capture(w, s.Config, Me);
        Assert.True(catalogue.IsUnlocked(Bronze));
    }

    /// <summary>Game-scoped: a new game starts with only its own founding goods; a resumed game's replay rebuilds the
    /// catalogue it had.</summary>
    [Fact]
    public void TheCatalogue_IsGameScoped_EmptyOfAnotherGamesGoods_AndRebuiltByResume()
    {
        UiSession seeded = Seeded();
        seeded.EndTurn();
        Assert.True(seeded.Unlocked.IsUnlocked(Pottery));
        UiSession fresh = UiSession.Start(42);
        Assert.False(fresh.Unlocked.IsUnlocked(Pottery));               // nothing carries over from another game
        Assert.NotEmpty(fresh.Unlocked.Unlocked);

        string dir = Path.Combine(_root, "resume");
        Directory.CreateDirectory(dir);
        string log = Path.Combine(dir, "orders-20261020-120000-a1.bin");
        UiSession live = UiSession.Start(42, aiEmpiresOverride: 1);
        live.ExportManifest("2026-10-20 12:00:00", log);
        for (int t = 0; t < 5; t++) live.EndTurn();
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));
        UiSession resumed = UiSession.Resume(UiArgs.ResolveManifest(dir), out _);
        Assert.Equal(live.World.Clock.Turn, resumed.World.Clock.Turn);
        Assert.Equal(live.Unlocked.Unlocked.ToArray(), resumed.Unlocked.Unlocked.ToArray());
        Assert.NotEmpty(resumed.Unlocked.Unlocked);
    }

    // ================================================================== the real UI: 0 is drawn

    [Fact]
    public void AZeroStockGood_IsDrawnAs0_InTheStoresTable_AndItsCard()
    {
        UiSession s = Seeded();
        for (int t = 0; t < 3; t++) s.EndTurn();
        using UiFrameHarness h = UiFrameHarness.Start(s, Path.Combine(_root, "ui"), Path.Combine(AppContext.BaseDirectory, "assets"),
            developer: false, 1600, 1000);
        h.Idle(3);
        Assert.True(h.ClickControl("nav:Resources"));
        h.Idle(3);
        Assert.NotNull(h.Ui.Controls.Find(GameUi.StoreRowControl(Pottery)));   // the row is drawn
        IReadOnlyList<string> texts = h.Ui.FrameText.Texts(FrameSurface.Context);
        Assert.Contains("pottery", texts);
        Assert.Contains("Next turn", texts);
        StoreGood pottery = h.Ui.Stores!.Good(Pottery)!;
        Assert.Equal((0L, 0L, true), (pottery.Total, pottery.Next, pottery.HasNext));

        UiControl c = h.Ui.Controls.Find(GameUi.StripControl(StoreGroup.Goods)) ?? throw new InvalidOperationException("Goods group not drawn");
        h.MoveTo((c.X0 + c.X1) / 2.0, (c.Y0 + c.Y1) / 2.0);
        Assert.Equal(StoreGroup.Goods, h.Ui.StripHover);
        Assert.Contains("pottery: 0 (no change since last turn); 0 next turn; none in store", h.Ui.StripCardLines);
        Assert.Contains("cloth: 0 (no change since last turn); 0 next turn; none in store", h.Ui.StripCardLines);
        Assert.Empty(h.Problems);
    }

    // ================================================================== the next-turn figure

    /// <summary>A good at 0 NOW (stone, emptied at every own settlement) shows what the next turn opens with — the stone the
    /// step extracts — and that is exactly the next turn's figure.</summary>
    [Fact]
    public void AGoodAtZeroNow_ShowsTheNextTurnsStores_ExactlyAsTheStepMakesThem()
    {
        WorldState w = Founding.Value.Clone();
        foreach (int id in EmpireStores.OwnSettlements(w, Me)) Set(w, new SettlementId(id), Stone, 0);
        UiSession s = UiSession.StartFrom(w, 42);
        Assert.False(s.Unlocked.IsUnlocked(Stone));   // never held in THIS game: not listed until the step brings some
        EmpireStores before = Stores(s, next: true);
        StoreGood stone = before.Good(Stone)!;
        Assert.Equal(0, stone.Total);
        Assert.True(stone.HasNext);
        s.EndTurn();
        Assert.Equal(Own(s.World, Stone), stone.Next);
        Assert.True(stone.Next > 0, "premise: the step extracts stone");
        foreach (StoreGood g in before.AllGoods) Assert.Equal(Own(s.World, g.Good), g.Next);   // every good, exactly
    }

    /// <summary>
    /// Construction expenditure, counted once: two granaries queued at the capital this turn. The figure equals the next
    /// turn's stores for every good (so the materials the step charges — and any it sets aside — are taken exactly once);
    /// the charge is real (the twin without the orders opens the next turn with more timber), and it is NOT the whole queue's
    /// cost (one-at-a-time with carry-over, D-058, charges only what the turn's capacity reaches) — so subtracting every queued
    /// project's materials would be wrong.
    /// </summary>
    [Fact]
    public void TheNextTurnFigure_ChargesConstructionAsTheStepDoes_OnceAndNotTheWholeQueue()
    {
        UiSession s = UiSession.StartFrom(Founding.Value.Clone(), 42);
        UiSession twin = UiSession.StartFrom(Founding.Value.Clone(), 42);
        int capital = Capital(s.World).Value;
        EmpireStores idle = Stores(s, next: true);
        Assert.True(s.EmitConstructionOrder(capital, Granary));
        Assert.True(s.EmitConstructionOrder(capital, Granary));
        EmpireStores ordered = Stores(s, next: true);                    // the cache follows the order count
        EmpireStores twinNext = Stores(twin, next: true);
        Assert.Equal(idle.Good(Timber)!.Next, twinNext.Good(Timber)!.Next);

        long charged = twinNext.Good(Timber)!.Next - ordered.Good(Timber)!.Next;
        long stoneCharged = twinNext.Good(Stone)!.Next - ordered.Good(Stone)!.Next;
        Assert.True(charged > 0, "premise: the step charges timber for the head");
        Assert.True(charged < 2 * 40, "premise: the turn does not charge the whole queue (" + charged + ")");
        long setAside = ordered.Good(Timber)!.SetAside + ordered.Good(Stone)!.SetAside;

        s.EndTurn();
        twin.EndTurn();
        foreach (StoreGood g in ordered.AllGoods) Assert.Equal(Own(s.World, g.Good), g.Next);   // once, every good
        foreach (StoreGood g in twinNext.AllGoods) Assert.Equal(Own(twin.World, g.Good), g.Next);
        // The naive readings are wrong here: the whole queue's timber, or the set-aside taken a second time.
        Assert.NotEqual(twinNext.Good(Timber)!.Next - 2 * 40, ordered.Good(Timber)!.Next);
        if (setAside > 0) Assert.NotEqual(Own(s.World, Timber) - ordered.Good(Timber)!.SetAside, ordered.Good(Timber)!.Next);
        Assert.True(stoneCharged >= 0);
    }

    /// <summary>
    /// Set-asides counted once: materials a waiting project keeps back are still in the stores (the table's "Set aside"
    /// column is a part of the total, not a deduction), so the next-turn figure must not take them off again. Rig: a
    /// granary queued at each of the player's settlements with stone short, so a waiting entry holds what it can; the
    /// figure equals the next turn's stores for every good, through several turns, while something is set aside.
    /// </summary>
    [Fact]
    public void TheNextTurnFigure_TakesSetAsideMaterialsOnce()
    {
        WorldState w = Founding.Value.Clone();
        int[] own = EmpireStores.OwnSettlements(w, Me);
        foreach (int id in own) Set(w, new SettlementId(id), Stone, 6);
        UiSession s = UiSession.StartFrom(w, 42);
        foreach (int id in own) { Assert.True(s.EmitConstructionOrder(id, Granary)); Assert.True(s.EmitConstructionOrder(id, Granary)); }
        int withAside = 0;
        for (int t = 0; t < 6; t++)
        {
            EmpireStores st = Stores(s, next: true);
            foreach (StoreGood g in st.AllGoods) if (g.SetAside > 0) withAside++;
            s.EndTurn();
            foreach (StoreGood g in st.AllGoods) Assert.Equal(Own(s.World, g.Good), g.Next);
        }
        Assert.True(withAside > 0, "premise: some turn had materials set aside");
    }

    /// <summary>The preview is read-only: a session that previewed after every order and every turn plays to the same world,
    /// hash for hash, as one that never previewed — and its order log is untouched.</summary>
    [Fact]
    public void ThePreview_ChangesNoWorld_NoOrder_AndNoHash()
    {
        UiSession a = UiSession.Start(42, aiEmpiresOverride: 1);
        UiSession b = UiSession.Start(42, aiEmpiresOverride: 1);
        int capital = Capital(a.World).Value;
        for (int t = 0; t < 4; t++)
        {
            string before = WorldHash.ComputeHex(a.World);
            int orders = a.Orders.Count;
            a.PreviewNextWorld();
            if (t == 1) { Assert.True(a.EmitConstructionOrder(capital, Granary)); Assert.True(b.EmitConstructionOrder(capital, Granary)); }
            a.PreviewNextWorld();
            a.PreviewNextWorld();
            Assert.Equal(before, WorldHash.ComputeHex(a.World));
            Assert.Equal(orders + (t == 1 ? 1 : 0), a.Orders.Count);
            a.EndTurn();
            b.EndTurn();
            Assert.Equal(WorldHash.ComputeHex(b.World), WorldHash.ComputeHex(a.World));
            Assert.Equal(b.Orders.Count, a.Orders.Count);
        }
    }

    /// <summary>Rival isolation (D-060) holds for the new figures: next-turn worlds that differ only in a rival's stores give
    /// the same stores, strip and card text; the player's own next-turn stores do move it (the control).</summary>
    [Fact]
    public void TheNextTurnFigure_ReadsNoRivalsStores()
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: 1);
        s.EndTurn();
        IReadOnlyWorldState next = s.PreviewNextWorld();
        WorldState a = ((WorldState)next).Clone(), b = ((WorldState)next).Clone();
        IReadOnlyList<int> rival = KnowledgeStubs.SettlementsOf(b, PlayabilityGate.HiddenPolity);
        Assert.NotEmpty(rival);
        foreach (int id in rival) foreach (GoodEntry g in s.Config.Goods!.Goods) Set(b, new SettlementId(id), g.Id, 999);
        string Text(IReadOnlyWorldState n)
        {
            EmpireStores st = EmpireStores.Build(s.World, s.PreviousWorld, s.Config, Me, s.Unlocked, n);
            var lines = new List<string>();
            foreach (StoreGroupSummary g in st.Groups) lines.AddRange(EmpireStores.CardLines(g, id => "s" + id.ToString(CultureInfo.InvariantCulture)));
            foreach (StoreGood g in st.AllGoods) lines.Add(g.Good.ToString(CultureInfo.InvariantCulture) + ":" + g.Next.ToString(CultureInfo.InvariantCulture));
            return string.Join("\n", lines);
        }
        Assert.Equal(Text(a), Text(b));
        WorldState c = ((WorldState)next).Clone();
        Set(c, Capital(c), Timber, Own(c, Timber) + 50);
        Assert.NotEqual(Text(a), Text(c));
    }
}
