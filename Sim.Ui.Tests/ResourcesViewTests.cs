using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Actions;
using Sim.Ui.Headless;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 resources view (the Director, 2026-10-19; docs/m5-resources-view-proposal.md) — THE STRIP, ITS CARDS AND RESOURCES.
/// The totals are the exact long sums of GoodStocks over the player's OWN settlements (the control relation) on a populated
/// multi-empire world; the change since last turn is exact across a step; no '%' in Ages I–III; RESOURCES (the former
/// TRENDS: the same slot, the same key 6) renders in every state of the playability gate; the strip fits the smallest
/// window beside the research and Age chips. The leak rows (nothing foreign anywhere, even all-known) are in
/// KnowledgeLeakTests.Resources.cs.
/// </summary>
[Collection("ImGui context")]
public sealed class ResourcesViewTests : IDisposable
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-resources-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    /// <summary>The one-AI canonical world, four turns played by the real session (AI orders included).</summary>
    private static readonly Lazy<UiSession> Played = new(() =>
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: 1);
        for (int t = 0; t < 4; t++) s.EndTurn();
        return s;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The independent oracle: the sum of EVERY GoodStocks row of <paramref name="good"/> whose settlement the
    /// player controls (a different loop from the model's per-settlement first-row sum).</summary>
    private static long Oracle(IReadOnlyWorldState w, int good, PolityId player, bool own = true)
    {
        long sum = 0;
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            GoodStockRow row = w.GoodStocks[i];
            if (row.Good.Value != good) continue;
            if (EmpireQuery.ControlsSettlement(w, player, row.Settlement) == own) sum = checked(sum + row.Amount.Value);
        }
        return sum;
    }

    [Fact]
    public void EmpireTotals_EqualTheSumOfGoodStocks_OverOwnSettlementsOnly_OnAPopulatedMultiEmpireWorld()
    {
        UiSession s = Played.Value;
        IReadOnlyWorldState w = s.World;
        EmpireStores st = EmpireStores.Build(w, s.PreviousWorld, s.Config, Me);
        // PREMISE: a populated multi-empire world — the player and a rival each hold settlements, and the rival holds goods.
        Assert.NotEmpty(st.Settlements);
        Assert.NotEmpty(KnowledgeStubs.SettlementsOf(w, PlayabilityGate.HiddenPolity));
        long foreign = 0;
        foreach (GoodEntry g in s.Config.Goods!.Goods) foreign += Oracle(w, g.Id, Me, own: false);
        Assert.True(foreign > 0, "the rival holds nothing - the premise is void");

        int nonZero = 0;
        foreach (GoodEntry g in s.Config.Goods!.Goods)
        {
            StoreGood good = st.Good(g.Id)!;
            Assert.Equal(Oracle(w, g.Id, Me), good.Total);                                    // exact, long
            long columns = 0;
            int gi = Array.IndexOf(st.AllGoods, good);
            foreach (long v in st.Amounts[gi]) columns += v;
            Assert.Equal(good.Total, columns);                                                // the table's columns add up
            long holders = 0;
            foreach (StoreHolder h in good.Holders) { holders += h.Amount; Assert.Contains(h.Settlement, st.Settlements); }
            Assert.Equal(good.Total, holders);
            if (good.Total > 0) nonZero++;
        }
        Assert.True(nonZero >= 3, "anti-vacuity: the player holds several goods");
        // Each group is the sum of its shown goods; every good held is shown, in its category's group.
        foreach (StoreGroupSummary g in st.Groups)
        {
            long sum = 0;
            foreach (StoreGood x in g.Goods) { sum += x.Total; Assert.Equal(g.Group, x.Group); }
            Assert.Equal(sum, g.Total);
        }
        foreach (StoreGood x in st.AllGoods)
            if (x.Total > 0) Assert.Contains(st.Group(x.Group)!.Goods, y => y.Good == x.Good);
        // Own settlements are exactly the controlled ones.
        foreach (int id in st.Settlements) Assert.True(EmpireQuery.ControlsSettlement(w, Me, new SettlementId(id)));
        Assert.Equal(EmpireQuery.ControlledCount(w, Me), st.Settlements.Length);
    }

    /// <summary>M5 resources view follow-up: the status band's POPULATION, FOOD and settlement count are exact sums over the
    /// player's OWN settlements on the populated one-AI world — never the known world (the rival holds people and grain) —
    /// and FOOD equals the strip's grain.</summary>
    [Fact]
    public void TheBandTotals_AreExactOwnSums_OnAPopulatedMultiEmpireWorld()
    {
        UiSession s = Played.Value;
        IReadOnlyWorldState w = s.World;
        long pop = 0, allPop = 0;
        for (int i = 0; i < w.Buckets.Count; i++)
        {
            allPop += w.Buckets[i].Count.Value;
            if (EmpireQuery.ControlsSettlement(w, Me, w.Buckets[i].Settlement)) pop += w.Buckets[i].Count.Value;
        }
        HudModel hud = HudModel.From(w, -1, null, null, s.Config, s.Knowledge);
        Assert.Equal(pop, hud.WorldPopulation);
        Assert.Equal(Oracle(w, s.Config.Goods!.GrainId, Me), hud.WorldFood);
        Assert.Equal(EmpireQuery.ControlledCount(w, Me), hud.SettlementCount);
        Assert.Equal(EmpireStores.Build(w, s.PreviousWorld, s.Config, Me).Good(s.Config.Goods!.GrainId)!.Total, hud.WorldFood);
        // PREMISE: the known world is larger (the rival is known and populated) — the old reading would differ.
        Assert.True(allPop > pop);
        Assert.True(Oracle(w, s.Config.Goods!.GrainId, Me, own: false) > 0);
        Assert.True(w.Settlements.Count > hud.SettlementCount);
        Assert.Equal(hud.SettlementCount == 1 ? "1 settlement" : hud.SettlementCount.ToString(CultureInfo.InvariantCulture) + " settlements", hud.SettlementCountDetail);
    }

    [Fact]
    public void ChangeSinceLastTurn_IsExact_AcrossAStep_AndAbsentBeforeTheFirst()
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: 1);
        EmpireStores before = EmpireStores.Build(s.World, s.PreviousWorld, s.Config, Me);
        Assert.Null(s.PreviousWorld);
        foreach (StoreGood g in before.AllGoods) { Assert.False(g.HasPrevious); Assert.Equal(0, g.Change); }
        foreach (StoreGroupSummary g in before.Groups)
        {
            Assert.Equal(StoreTrend.None, EmpireStores.TrendOf(g.HasPrevious, g.Change));
            Assert.Equal("-", EmpireStores.ChangeText(g.HasPrevious, g.Change));
        }
        var totals = new Dictionary<int, long>();
        foreach (GoodEntry g in s.Config.Goods!.Goods) totals[g.Id] = Oracle(s.World, g.Id, Me);

        s.EndTurn();
        EmpireStores after = EmpireStores.Build(s.World, s.PreviousWorld, s.Config, Me);
        int moved = 0;
        foreach (GoodEntry g in s.Config.Goods!.Goods)
        {
            StoreGood x = after.Good(g.Id)!;
            Assert.True(x.HasPrevious);
            Assert.Equal(totals[g.Id], x.Previous);
            Assert.Equal(Oracle(s.World, g.Id, Me) - totals[g.Id], x.Change);
            if (x.Change != 0) moved++;
        }
        Assert.True(moved >= 1, "anti-vacuity: a step moved some store");
        foreach (StoreGroupSummary g in after.Groups)
        {
            long change = 0;
            foreach (StoreGood x in g.Goods) change += x.Change;
            Assert.Equal(change, g.Change);
            StoreTrend expected = change > 0 ? StoreTrend.Rising : change < 0 ? StoreTrend.Falling : StoreTrend.Steady;
            Assert.Equal(expected, EmpireStores.TrendOf(g.HasPrevious, g.Change));
        }
    }

    [Fact]
    public void Holders_AreLargestFirst_TiesToTheLowerSettlementId_OnATieDenseWorld()
    {
        // Tie-dense: every own settlement holds exactly the same amount of every good.
        UiSession s = Played.Value;
        WorldState w = s.World.Clone();
        int[] own = EmpireStores.OwnSettlements(w, Me);
        Assert.True(own.Length >= 1);
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (Array.IndexOf(own, w.GoodStocks[i].Settlement.Value) < 0) continue;
            long now = w.GoodStocks[i].Amount.Value;
            if (now > 0) ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(w.GoodStocks[i].Good), ReasonIds.InitialEndowment,
                now, FlowDirection.Sink, OverdrawPolicy.Throw);
            ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(w.GoodStocks[i].Good), ReasonIds.InitialEndowment,
                10, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        EmpireStores st = EmpireStores.Build(w, null, s.Config, Me);
        foreach (StoreGood g in st.AllGoods)
        {
            if (g.Holders.Length < 2) continue;
            for (int i = 1; i < g.Holders.Length; i++)
            {
                Assert.Equal(g.Holders[i - 1].Amount, g.Holders[i].Amount);
                Assert.True(g.Holders[i - 1].Settlement < g.Holders[i].Settlement);
            }
        }
        // And a strict order by amount where they differ (the composite key's first component).
        var line = new StoreGood(1, "grain", StoreGroup.Food, 30, 0, false, 0,
            [new StoreHolder(5, 20), new StoreHolder(2, 5), new StoreHolder(9, 5)]);
        Assert.Equal("grain: 30; most at s5 20, s2 5, s9 5", EmpireStores.GoodLine(line, id => "s" + id.ToString(CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void TheCardWords_SetAside_NeverHeldOrReserved_AndCountsOnly()
    {
        var good = new StoreGood(4, "timber", StoreGroup.Materials, 120, 108, true, 40,
            [new StoreHolder(0, 80), new StoreHolder(3, 30), new StoreHolder(1, 6), new StoreHolder(2, 4)]);
        string line = EmpireStores.GoodLine(good, id => "s" + id.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("timber: 120 (+12 since last turn); 40 set aside for projects; most at s0 80, s3 30, s1 6 and one other", line);
        var group = new StoreGroupSummary(StoreGroup.Materials, 120, 108, true, [good]);
        IReadOnlyList<string> card = EmpireStores.CardLines(group, id => "s" + id.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("Your empire holds 120. 12 more than at the start of last turn.", card[0]);
        foreach (string l in card)
        {
            Assert.DoesNotContain("%", l, StringComparison.Ordinal);
            Assert.DoesNotContain("reserved", l, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" held", l, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal("-5", EmpireStores.ChangeText(true, -5));
        Assert.Equal("no change", EmpireStores.ChangeText(true, 0));
        Assert.Equal("+1,234", EmpireStores.ChangeText(true, 1234));
    }

    /// <summary>A good the empire never held is not shown; one that just ran out is (0, falling); a set-aside alone shows it.</summary>
    [Fact]
    public void WhichGoodsAreShown_HeldNowOrLastTurnOrSetAside()
    {
        Assert.False(EmpireStores.IsShown(new StoreGood(11, "bronze", StoreGroup.Goods, 0, 0, true, 0, [])));
        Assert.False(EmpireStores.IsShown(new StoreGood(11, "bronze", StoreGroup.Goods, 0, 0, false, 0, [])));
        Assert.True(EmpireStores.IsShown(new StoreGood(11, "bronze", StoreGroup.Goods, 0, 3, true, 0, [])));
        Assert.True(EmpireStores.IsShown(new StoreGood(11, "bronze", StoreGroup.Goods, 0, 0, true, 2, [])));
        Assert.True(EmpireStores.IsShown(new StoreGood(11, "bronze", StoreGroup.Goods, 5, 0, false, 0, [])));
        // The turn-1 Stone Age strip is short: the founding world's player holds no made goods.
        UiSession s = UiSession.Start(42);
        EmpireStores st = EmpireStores.Build(s.World, null, s.Config, Me);
        Assert.Null(st.Group(StoreGroup.Goods));
        Assert.NotNull(st.Group(StoreGroup.Food));
    }

    /// <summary>The OWN-EMPIRE series sum only settlements that were the player's on the turn read (a settlement taken
    /// from a rival brings no foreign past; one lost stops counting the turn it is lost).</summary>
    [Fact]
    public void TheEmpireSeries_SumOnlyTheSettlementsThatWereThePlayersOnEachTurn()
    {
        UiSession s = Played.Value;
        Sim.Core.Observability.IObservationHistory h = s.Observations;
        int[] rival = [.. KnowledgeStubs.SettlementsOf(s.World, PlayabilityGate.HiddenPolity)];
        long first = h.Observations[0].Turn.Turn, last = h.Observations[^1].Turn.Turn;
        // The session's own history: the player's settlements on every turn.
        double[] pop = TrendsModel.Empire(h, Sim.Core.Observability.SeriesKey.Population, -1, s.Ownership);
        for (int t = 0; t < h.Observations.Count; t++)
        {
            long sum = 0;
            foreach (Sim.Core.Observability.SettlementRecord r in h.Observations[t].Settlements)
                if (s.Ownership.Owned(h.Observations[t].Turn.Turn, r.Settlement)) sum += r.Population.Closing;
            Assert.Equal((double)sum, pop[t + 1]);   // +1: the founding sample leads
        }
        // A constructed history: a rival settlement "taken" on the last turn counts only on that turn.
        var taken = new List<(long Turn, int[] Own)>();
        for (long turn = first - 1; turn <= last; turn++)
        {
            int[] then = s.Ownership.At(turn)!;
            if (turn == last)
            {
                int[] plus = new int[then.Length + 1];
                then.CopyTo(plus, 0);
                plus[^1] = rival[0];
                then = plus;
            }
            taken.Add((turn, then));
        }
        OwnershipHistory o = OwnershipHistory.Of(taken.ToArray());
        double[] stock = TrendsModel.EmpireStock(h, s.Config.Goods!.GrainId, o);
        double[] mine = TrendsModel.EmpireStock(h, s.Config.Goods!.GrainId, s.Ownership);
        for (int t = 0; t < stock.Length - 1; t++) Assert.Equal(mine[t], stock[t]);
        Assert.Equal(mine[^1] + TrendsModel.StockOf(h.Settlement(last, rival[0])!, s.Config.Goods!.GrainId), stock[^1]);
        double[] one = TrendsModel.OwnSettlementStock(h, rival[0], s.Config.Goods!.GrainId, o);
        for (int t = 0; t < one.Length - 1; t++) Assert.True(double.IsNaN(one[t]));
        Assert.False(double.IsNaN(one[^1]));
    }

    // ================================================================== the real UI

    /// <summary>The ids of the gate's states (every one is rendered by the test below).</summary>
    public static TheoryData<string> GateStates()
    {
        var data = new TheoryData<string>();
        foreach (PlayabilityGate.GateState st in PlayabilityGate.States(null)) data.Add(st.Name);
        return data;
    }

    /// <summary>RESOURCES renders in EVERY state of the playability gate: the section opens from its button (the former
    /// TRENDS slot) and from key 6, draws the stores and the trends without a frame defect, and each strip group's card
    /// paints.</summary>
    [Theory]
    [MemberData(nameof(GateStates))]
    public void Resources_Renders_InEveryGateState(string state)
    {
        PlayabilityGate.GateState st = PlayabilityGate.States(null).First(x => x.Name == state);
        UiSession s = st.Build();
        using UiFrameHarness h = UiFrameHarness.Start(s, Path.Combine(_root, "s"), Assets(), developer: false, st.Width, st.Height);
        h.Idle(3);
        Assert.Equal("RESOURCES", GameSections.Label(Section.Resources));
        Assert.Equal(Section.Resources, GameSections.ForDigit(6, developer: false));
        Assert.True(h.ClickControl("nav:Resources"), "no RESOURCES button");
        h.Idle(3);
        Assert.Equal(Section.Resources, h.Ui.OpenSection);
        IReadOnlyList<string> texts = h.Ui.FrameText.Texts(FrameSurface.Context);
        Assert.Contains("Stores of your empire", texts);
        Assert.Contains("Trends of your empire", texts);
        h.Key(Microsoft.Xna.Framework.Input.Keys.Escape);
        h.Key(Microsoft.Xna.Framework.Input.Keys.D6);
        Assert.Equal(Section.Resources, h.Ui.OpenSection);
        foreach (StoreGroupSummary g in h.Ui.Stores!.Groups)
        {
            UiControl c = h.Ui.Controls.Find(GameUi.StripControl(g.Group)) ?? throw new InvalidOperationException("strip group not drawn: " + g.Group);
            h.MoveTo((c.X0 + c.X1) / 2.0, (c.Y0 + c.Y1) / 2.0);
            Assert.Equal(g.Group, h.Ui.StripHover);
            Assert.Equal(1 + g.Goods.Length, h.Ui.StripCardLines.Count);
        }
        Assert.Empty(h.Problems);
    }

    /// <summary>The strip fits the smallest window the game allows: every group inside the status band, left of the research
    /// chip, which (with the Age chip) stays inside the window — after turns have filled more than one group.</summary>
    [Fact]
    public void TheStrip_FitsTheSmallestWindow_BesideTheChips()
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: 1);
        for (int t = 0; t < 2; t++) s.EndTurn();
        using UiFrameHarness h = UiFrameHarness.Start(s, Path.Combine(_root, "narrow"), Assets(), developer: false,
            PanelLayout.MinWindowWidth, PanelLayout.MinWindowHeight);
        h.Idle(3);
        Assert.True(h.Ui.Stores!.Groups.Length >= 2, "anti-vacuity: two strip groups");
        UiControl research = h.Ui.Controls.Find("band-research")!.Value;
        UiControl? age = h.Ui.Controls.Find("band-age");
        double right = 0;
        foreach (StoreGroupSummary g in h.Ui.Stores.Groups)
        {
            UiControl c = h.Ui.Controls.Find(GameUi.StripControl(g.Group))!.Value;
            Assert.True(c.X0 >= 0 && c.X1 > c.X0, g.Group + " drawn");
            right = Math.Max(right, c.X1);
        }
        Assert.True(right <= research.X0, "the strip runs into the research chip: " + right + " > " + research.X0);
        Assert.True(research.X1 <= h.Width, "the research chip leaves the window");
        // Director playtest 2026-10-29: the Civics chip ([C]) sits after the Tech chip, inside the window, before the Age chip.
        UiControl civics = h.Ui.Controls.Find("band-civics") ?? throw new InvalidOperationException("no Civics chip (two-lane content)");
        Assert.True(civics.X0 >= research.X1 && civics.X1 <= h.Width, "the Civics chip leaves the window or overlaps: " + civics.X0 + ".." + civics.X1);
        if (age is { } a) Assert.True(a.X1 <= h.Width && a.X0 >= civics.X1, "the Age chip leaves the window or overlaps");
        Assert.Empty(h.Problems);
    }

    /// <summary>
    /// NO '%' IN THE NUMERAL-FREE ERAS (Ages I–III): the strip's cards and every text RESOURCES draws — the stores table and
    /// the graph on every metric — carry no percent sign, the played one-AI world put in Age 1, 2 and 3. Premise: the era
    /// is numeral-free; anti-vacuity: the table and a card are drawn.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InTheNumeralFreeEras_NoResourcesTextCarriesAPercentSign(int age)
    {
        UiSession played = Played.Value;
        WorldState w = played.World.Clone();
        bool found = false;
        for (int i = 0; i < w.AgeStates.Count; i++)
            if (w.AgeStates[i].Polity.Value == Me.Value) { w.AgeStates[i] = new AgeStateRow(Me, age, w.Clock.Turn, 1, w.Clock.Turn); found = true; }
        if (!found && age != played.Config.Ages!.FoundingAge) w.AgeStates.Add(new AgeStateRow(Me, age, w.Clock.Turn, 1, w.Clock.Turn));
        Assert.False(LabourControlSpec.For(EraThemes.For(UiEras.Of(w, played.Config.Ages, Me))).Numerals, "PREMISE: Age " + age + " is numeral-free");
        UiSession s = UiSession.StartFrom(w, 42);
        s.EndTurn();   // a step, so the change marks and the graphs have a reading
        using UiFrameHarness h = UiFrameHarness.Start(s, Path.Combine(_root, "age" + age.ToString(CultureInfo.InvariantCulture)), Assets());
        h.Idle(3);
        var texts = new List<string>();
        foreach (StoreGroupSummary g in h.Ui.Stores!.Groups)
        {
            texts.Add(EmpireStores.Label(g.Group));
            texts.Add(EmpireStores.Value(g));
            UiControl c = h.Ui.Controls.Find(GameUi.StripControl(g.Group))!.Value;
            h.MoveTo((c.X0 + c.X1) / 2.0, (c.Y0 + c.Y1) / 2.0);
            texts.AddRange(h.Ui.StripCardLines);
        }
        Assert.True(h.ClickControl("nav:Resources"));
        h.Idle(2);
        texts.AddRange(h.Ui.FrameText.Texts(FrameSurface.Context));
        for (int i = 0; i < h.Ui.TrendMetrics.Count; i++)
        {
            Assert.True(h.ClickInContext("trend-" + i.ToString(CultureInfo.InvariantCulture)));
            h.Idle(2);
            texts.AddRange(h.Ui.FrameText.Texts(FrameSurface.Context));
        }
        Assert.Contains(texts, t => t.Contains("Stores of your empire", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("Your empire holds ", StringComparison.Ordinal));
        Assert.All(texts, t => Assert.DoesNotContain("%", t, StringComparison.Ordinal));
        Assert.Empty(h.Problems);
    }
}
