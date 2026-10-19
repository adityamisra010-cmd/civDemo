using System.Globalization;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Headless;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 resources view (the Director, 2026-10-19: "i should not be able to see other empire's data") — THE LEAK ROWS OF THE
/// RESOURCE STRIP, ITS HOVER CARDS AND THE RESOURCES VIEW. Stronger than the P7 rows above: those readers leave out what the
/// player does not KNOW; these must leave out everything the player does not OWN, so every row here reads through the
/// ALL-KNOWN production view (no stub). METAMORPHIC: the rival polity's settlements are perturbed whole — their stocks,
/// people, dwellings, prices, grievances, queues (<see cref="Perturb"/>) and set-asides planted in its purse and in its
/// places' own purses — on both sides of the step; every output must be IDENTICAL. POSITIVE CONTROLS: the same
/// perturbation moves the all-known world series (so it reached the data), and perturbing the PLAYER's own stocks moves
/// the outputs (so they read something).
/// </summary>
public partial class KnowledgeLeakTests
{
    /// <summary>Every field of the stores, its card lines and its strip values, as text.</summary>
    private static string StoresText(EmpireStores st, Func<int, string> name)
    {
        var sb = new StringBuilder();
        sb.Append("settlements ").Append(string.Join(",", st.Settlements)).Append('\n');
        for (int g = 0; g < st.AllGoods.Length; g++)
        {
            StoreGood x = st.AllGoods[g];
            sb.Append(x.Good).Append(' ').Append(x.Name).Append(' ').Append(x.Group).Append(' ').Append(x.Total).Append(' ')
                .Append(x.Previous).Append(' ').Append(x.HasPrevious).Append(' ').Append(x.SetAside).Append(" [");
            foreach (StoreHolder h in x.Holders) sb.Append(h.Settlement).Append(':').Append(h.Amount).Append(' ');
            sb.Append("] ").Append(string.Join(",", st.Amounts[g])).Append(" | ").Append(EmpireStores.GoodLine(x, name)).Append('\n');
        }
        foreach (StoreGroupSummary g in st.Groups)
            sb.Append(EmpireStores.Label(g.Group)).Append(' ').Append(EmpireStores.Value(g)).Append(' ')
                .Append(EmpireStores.TrendOf(g.HasPrevious, g.Change)).Append('\n')
                .Append(string.Join("\n", EmpireStores.CardLines(g, name))).Append('\n');
        return sb.ToString();
    }

    /// <summary>Plants set-asides in the rival's Empire purse and in each of its places' own purses (P7: never the player's).</summary>
    private static void PlantForeignHolds(WorldState w, SimConfig cfg)
    {
        int timber = cfg.Goods!.IdOf("timber"), stone = cfg.Goods!.IdOf("stone");
        w.ConstructionHolds.Add(new ConstructionHoldRow(Reservations.PolityPurse(Hidden), timber, 77));
        foreach (int id in KnowledgeStubs.SettlementsOf(w, Hidden)) w.ConstructionHolds.Add(new ConstructionHoldRow(-1 - id, stone, 13));
    }

    /// <summary>Adds <paramref name="units"/> of every good to each of the PLAYER's own settlements (the positive control).</summary>
    private static void PerturbOwn(WorldState w, int units)
    {
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, Player, w.GoodStocks[i].Settlement))
                ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(w.GoodStocks[i].Good), ReasonIds.InitialEndowment,
                    units, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    [Fact]
    public void TheStoresStripAndCards_AreIdentical_WhateverAForeignEmpireHolds_EvenAllKnown()
    {
        Rig r = Played.Value;
        WorldState a0 = r.Prev.Clone(), a1 = r.Next.Clone();
        WorldState b0 = r.Prev.Clone(), b1 = PerturbedNext();
        Perturb(b0, Stub(b0), r.Config, 0);
        PlantForeignHolds(b1, r.Config);
        PlantForeignHolds(b0, r.Config);
        Assert.NotEmpty(KnowledgeStubs.SettlementsOf(a1, Hidden));
        // The rival is KNOWN to the player in both worlds (the production view): the filter is ownership, not knowledge.
        foreach (int id in KnowledgeStubs.SettlementsOf(a1, Hidden)) Assert.True(All(a1).KnowsSettlement(id));

        string sa = StoresText(EmpireStores.Build(a1, a0, r.Config, Player), r.Name);
        string sb = StoresText(EmpireStores.Build(b1, b0, r.Config, Player), r.Name);
        Assert.Equal(sa, sb);
        foreach (int id in KnowledgeStubs.SettlementsOf(a1, Hidden))
            Assert.False(HasWord(sa, r.Name(id)), "a foreign settlement named in the stores: " + r.Name(id));

        // CONTROL 1: the perturbation reached the rival's stores (the sum the rival's own books would show moved).
        long ra = 0, rb = 0;
        foreach (int id in KnowledgeStubs.SettlementsOf(a1, Hidden))
            for (int i = 0; i < a1.GoodStocks.Count; i++)
                if (a1.GoodStocks[i].Settlement.Value == id) ra += a1.GoodStocks[i].Amount.Value;
        foreach (int id in KnowledgeStubs.SettlementsOf(b1, Hidden))
            for (int i = 0; i < b1.GoodStocks.Count; i++)
                if (b1.GoodStocks[i].Settlement.Value == id) rb += b1.GoodStocks[i].Amount.Value;
        Assert.NotEqual(ra, rb);
        Assert.NotEqual(StoresText(EmpireStores.Build(a1, a0, r.Config, Hidden), r.Name), StoresText(EmpireStores.Build(b1, b0, r.Config, Hidden), r.Name));
        // CONTROL 2: the player's own stores DO move the outputs (now and at the start of the step).
        WorldState c1 = r.Next.Clone(), c0 = r.Prev.Clone();
        PerturbOwn(c1, 50);
        Assert.NotEqual(sa, StoresText(EmpireStores.Build(c1, a0, r.Config, Player), r.Name));
        PerturbOwn(c0, 50);
        Assert.NotEqual(sa, StoresText(EmpireStores.Build(a1, c0, r.Config, Player), r.Name));
    }

    [Fact]
    public void TheResourcesTrends_AreIdentical_WhateverAForeignEmpireDoes_EvenAllKnown()
    {
        Rig r = Played.Value;
        WorldState a0 = r.Prev.Clone(), a1 = r.Next.Clone();
        WorldState b0 = r.Prev.Clone(), b1 = PerturbedNext();
        Perturb(b0, Stub(b0), r.Config, 0);
        var la = new ObservationLog();
        la.Observe(a0, a1, r.Config, []);
        var lb = new ObservationLog();
        lb.Observe(b0, b1, r.Config, []);
        OwnershipHistory oa = OwnershipHistory.Of((a0.Clock.Turn, EmpireStores.OwnSettlements(a0, Player)), (a1.Clock.Turn, EmpireStores.OwnSettlements(a1, Player)));
        OwnershipHistory ob = OwnershipHistory.Of((b0.Clock.Turn, EmpireStores.OwnSettlements(b0, Player)), (b1.Clock.Turn, EmpireStores.OwnSettlements(b1, Player)));

        int reached = 0;
        foreach (TrendMetric m in TrendsModel.ResourceMetrics(r.Config.Registries.Classes))
        {
            if (m.IsPrice || m.IsStock) continue;
            double[] ea = TrendsModel.Empire(la, m.Key, m.ClassId, oa), eb = TrendsModel.Empire(lb, m.Key, m.ClassId, ob);
            Assert.Equal(ea.Length, eb.Length);
            for (int i = 0; i < ea.Length; i++)
                Assert.True(ea[i].Equals(eb[i]), m.Label + " [" + i.ToString(CultureInfo.InvariantCulture) + "]: " + R(ea[i]) + " vs " + R(eb[i]));
            Assert.Equal(TrendsModel.ForPlot(ea), TrendsModel.ForPlot(eb));
            // CONTROL: the all-known WORLD series (the old TRENDS world scope) moves with the same perturbation.
            double[] ca = TrendsModel.World(la, m.Key, m.ClassId, All(a1)), cb = TrendsModel.World(lb, m.Key, m.ClassId, All(b1));
            bool differs = false;
            for (int i = 0; i < ca.Length; i++) differs |= !ca[i].Equals(cb[i]);
            if (differs) reached++;
        }
        Assert.True(reached >= 3, "the perturbation reaches only " + reached.ToString(CultureInfo.InvariantCulture) + " all-known world series - the control is void");

        // The stores metric: the empire's stores of every good, and each of the player's settlements'.
        bool stockMoved = false;
        foreach (GoodEntry g in r.Config.Goods!.Goods)
        {
            Assert.Equal(TrendsModel.EmpireStock(la, g.Id, oa), TrendsModel.EmpireStock(lb, g.Id, ob));
            foreach (int id in EmpireStores.OwnSettlements(a1, Player))
                Assert.Equal(TrendsModel.OwnSettlementStock(la, id, g.Id, oa), TrendsModel.OwnSettlementStock(lb, id, g.Id, ob));
            // A foreign settlement has no series at all in this view, whatever it holds.
            foreach (int id in KnowledgeStubs.SettlementsOf(a1, Hidden))
            {
                Assert.All(TrendsModel.OwnSettlementStock(la, id, g.Id, oa), v => Assert.True(double.IsNaN(v)));
                Assert.All(TrendsModel.OwnSettlement(la, id, SeriesKey.Population, -1, oa), v => Assert.True(double.IsNaN(v)));
                // CONTROL: the rival's own stock series moved (read as if it were the rival's empire).
                OwnershipHistory rival = OwnershipHistory.Of((a1.Clock.Turn, [id]));
                stockMoved |= !TrendsModel.OwnSettlementStock(la, id, g.Id, rival).SequenceEqual(TrendsModel.OwnSettlementStock(lb, id, g.Id, rival));
            }
        }
        Assert.True(stockMoved, "no foreign stock series moved - the control is void");

        // CONTROL: the player's own stocks move the empire stores series.
        WorldState c1 = r.Next.Clone();
        PerturbOwn(c1, 50);
        var lc = new ObservationLog();
        lc.Observe(r.Prev.Clone(), c1, r.Config, []);
        Assert.NotEqual(TrendsModel.EmpireStock(la, r.Config.Goods!.GrainId, oa), TrendsModel.EmpireStock(lc, r.Config.Goods!.GrainId, oa));
    }

    /// <summary>The RESOURCES view's text through the real UI: the section with each stores row and each metric in empire
    /// scope, and each strip group's hover card.</summary>
    private static List<string> ResourcesFrames(UiSession s, string dir)
    {
        using UiFrameHarness h = UiFrameHarness.Start(s, dir, Assets());
        h.Idle(3);
        var texts = new List<string>();
        foreach (StoreGroupSummary g in h.Ui.Stores!.Groups)
        {
            UiControl c = h.Ui.Controls.Find(GameUi.StripControl(g.Group)) ?? throw new InvalidOperationException("strip group not drawn");
            h.MoveTo((c.X0 + c.X1) / 2.0, (c.Y0 + c.Y1) / 2.0);
            texts.Add("== card " + g.Group);
            texts.AddRange(h.Ui.StripCardLines);
        }
        h.MoveTo(5, h.Height - 5);
        Assert.True(h.ClickControl("nav:Resources"));
        h.Idle(3);
        texts.Add("== resources");
        texts.AddRange(h.Ui.FrameText.Texts(FrameSurface.Context));
        for (int i = 0; i < h.Ui.TrendMetrics.Count; i++)
        {
            if (h.Ui.TrendMetrics[i].IsPrice) continue;
            Assert.True(h.ClickInContext("trend-" + i.ToString(CultureInfo.InvariantCulture)));
            h.Idle(2);
            texts.Add("== metric " + h.Ui.TrendMetrics[i].Label);
            texts.AddRange(h.Ui.FrameText.Texts(FrameSurface.Context));
        }
        Assert.Empty(h.Problems);
        return texts;
    }

    [Fact]
    public void FrameText_OfTheStripCardsAndResources_IsIdentical_WhateverAForeignEmpireHolds_EvenAllKnown()
    {
        Rig r = Played.Value;
        WorldState a = r.Next.Clone(), b = PerturbedNext();
        PlantForeignHolds(b, r.Config);
        // The production (all-known) view on both: the rival is known, and still nothing of it is drawn here.
        List<string> fa = ResourcesFrames(UiSession.StartFrom(a, 42), Path.Combine(_root, "res-a"));
        List<string> fb = ResourcesFrames(UiSession.StartFrom(b, 42), Path.Combine(_root, "res-b"));
        Assert.Equal(fa, fb);
        Assert.Contains(fa, t => t.Contains("Stores of your empire", StringComparison.Ordinal));
        foreach (int id in KnowledgeStubs.SettlementsOf(a, Hidden))
            foreach (string text in fa) Assert.False(HasWord(text, r.Name(id)), r.Name(id) + " drawn: " + text);
        // CONTROL: the player's own stores change what is drawn.
        WorldState c = r.Next.Clone();
        PerturbOwn(c, 50);
        Assert.NotEqual(fa, ResourcesFrames(UiSession.StartFrom(c, 42), Path.Combine(_root, "res-c")));
    }
}
