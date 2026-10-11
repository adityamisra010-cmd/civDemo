using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Chronicle;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.Actions;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 map workstream P7 — THE PLAYER-KNOWLEDGE SEAM'S LEAK GATE. The gate of record is METAMORPHIC and on VALUES: the
/// one-AI world after six played turns is seen through a knowledge stub that hides the AI polity whole
/// (<see cref="KnowledgeStubs"/>), and everything that polity is — its settlements' people, stocks, deposits, dwellings,
/// structures, queues, labour, prices, grievances and needs, its formations, its tax and Age, the terrain under the
/// nodes it hides — is perturbed. Every player-facing output must then be IDENTICAL: the world projection and the lens
/// paint (SVG bytes and LensFrame), the HUD (world totals included), the player views, the market and needs panels, Tab,
/// the unit and formation cards, the good cards, the annals (both chronicles, with hidden EVENTS planted), the Trends
/// world series and the history buffer's, and FrameText of every player section of the real UI. Each comparison has a
/// POSITIVE CONTROL: with the all-known view the same perturbation does change the same output, so equality under the
/// stub means the readers filtered, not that the perturbation missed. The read-isolation grep is advisory; the source
/// guard here reads the same allowlist and is exact.
/// </summary>
[Collection("ImGui context")]
public class KnowledgeLeakTests : IDisposable
{
    private static readonly PolityId Player = UiPlayer.Empire;
    private static readonly PolityId Hidden = new(2);

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-knowledge-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    // ================================================================== the rig

    /// <summary>The one-AI canonical world, six turns played by the real session: the step 5 → 6 and its names.</summary>
    private sealed class Rig
    {
        public required WorldState Prev { get; init; }
        public required WorldState Next { get; init; }
        public required SimConfig Config { get; init; }
        public required Func<int, string> Name { get; init; }
        public required bool[] Mask { get; init; }
    }

    private static readonly Lazy<Rig> Played = new(() =>
    {
        UiSession s = UiSession.Start(42, aiEmpiresOverride: 1);
        for (int t = 0; t < 6; t++) s.EndTurn();
        WorldState next = s.World.Clone();
        return new Rig
        {
            Prev = s.PreviousWorld!.Clone(), Next = next, Config = s.Config, Name = s.Names.Name,
            Mask = KnowledgeStubs.HiddenMask(next, Player, Hidden),
        };
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>What the stub lets the player know of <paramref name="w"/> (the rig's fixed mask).</summary>
    private static PlayerKnowledge Stub(IReadOnlyWorldState w) => KnowledgeStubs.Over(w, Player, Played.Value.Mask);

    /// <summary>The all-known production view (the positive control).</summary>
    private static PlayerKnowledge All(IReadOnlyWorldState w) => PlayerKnowledge.Of(w);

    [Fact]
    public void TheRig_HidesExactlyTheAiPolity_AndLeavesTheRestKnown()
    {
        Rig r = Played.Value;
        PlayerKnowledge k = Stub(r.Next);
        Assert.False(k.AllKnown);
        Assert.True(PlayerKnowledge.Of(r.Next).AllKnown);
        IReadOnlyList<int> hidden = KnowledgeStubs.SettlementsOf(r.Next, Hidden);
        Assert.True(hidden.Count >= 2, "the rig needs at least two hidden settlements");
        int known = 0;
        for (int i = 0; i < r.Next.Settlements.Count; i++)
        {
            int id = r.Next.Settlements[i].Id.Value;
            bool isHidden = hidden.Contains(id);
            Assert.Equal(!isHidden, k.KnowsSettlement(id));
            Assert.Equal(isHidden, k.IsHiddenSettlement(id));
            if (!isHidden) known++;
        }
        Assert.True(known >= 2, "the rig needs at least two known settlements");
        Assert.False(k.KnowsPolity(Hidden.Value));
        Assert.True(k.KnowsPolity(Player.Value));
        int hiddenUnits = 0;
        for (int i = 0; i < r.Next.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = r.Next.MilitaryUnits[i];
            Assert.Equal(u.Owner.Value != Hidden.Value, k.KnowsUnit(u));
            if (u.Owner.Value == Hidden.Value) hiddenUnits++;
        }
        Assert.True(hiddenUnits >= 1, "the rig needs a formation of the hidden polity");
    }

    // ================================================================== the perturbation

    /// <summary>
    /// Perturbs, in place, everything of <paramref name="w"/> that <paramref name="k"/> hides: every row keyed by a hidden
    /// settlement (people and stocks through the Ledger, so the world stays conserved; deposits, dwellings, structures, a
    /// queued project, the labour split, deficits, grievances, needs, prices, class states), the hidden polity's formations
    /// (experience, plus one new formation where one of them stands), its tax, and the terrain under every unknown node.
    /// <paramref name="salt"/> varies the amounts, so two calls perturb differently.
    /// </summary>
    private static void Perturb(WorldState w, PlayerKnowledge k, SimConfig cfg, int salt)
    {
        var ledger = new Ledger(w.LedgerFlows);
        bool H(SettlementId s) => k.IsHiddenSettlement(s.Value);
        for (int i = 0; i < w.Buckets.Count; i++)
            if (H(w.Buckets[i].Settlement))
                ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment,
                    7 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (!H(w.GoodStocks[i].Settlement)) continue;
            ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(w.GoodStocks[i].Good), ReasonIds.InitialEndowment,
                1000 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
            w.GoodStocks.Ref(i).LastProducedUnits += 333 + salt;
        }
        for (int i = 0; i < w.Housing.Count; i++)
            if (H(w.Housing[i].Settlement))
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings, ReasonIds.InitialEndowment,
                    5 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
        for (int i = 0; i < w.Deposits.Count; i++)
            if (H(w.Deposits[i].Settlement)) w.Deposits[i] = w.Deposits[i] with { Abundance = w.Deposits[i].Abundance * (1.7 + 0.1 * salt) + 0.05 };
        int project = cfg.Goods!.Projects![0].Id;
        foreach (int id in KnowledgeStubs.SettlementsOf(w, Hidden))
        {
            var s = new SettlementId(id);
            int at = -1;
            for (int i = 0; i < w.Structures.Count; i++) if (w.Structures[i].Settlement == s && w.Structures[i].ProjectId == project) at = i;
            if (at >= 0) w.Structures[at] = w.Structures[at] with { Count = w.Structures[at].Count + 2 + salt };
            else w.Structures.Add(new StructureRow(s, project, 2 + salt));
            w.ConstructionQueue.Add(new ConstructionQueueRow(s, 7 + salt, project));
        }
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (H(w.SectorAllocations[i].Settlement))
                w.SectorAllocations[i] = new SectorAllocationRow(w.SectorAllocations[i].Settlement, 0.1, 0.2, 0.3, 0.2, 0.2 + 0.01 * salt);
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (H(w.ConsumptionDeficits[i].Settlement))
                w.ConsumptionDeficits[i] = w.ConsumptionDeficits[i] with { DeficitRatio = salt > 0 ? 0.9 : 0.0, DemandUnits = 4321 + salt };
        for (int i = 0; i < w.Grievances.Count; i++)
            if (H(w.Grievances[i].Settlement)) w.Grievances[i] = w.Grievances[i] with { Value = w.Grievances[i].Value + 0.5 + salt };
        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
            if (H(w.NeedSatisfactions[i].Settlement)) w.NeedSatisfactions[i] = w.NeedSatisfactions[i] with { Value = w.NeedSatisfactions[i].Value * 0.5 };
        for (int i = 0; i < w.Prices.Count; i++)
            if (H(w.Prices[i].Settlement)) w.Prices[i] = w.Prices[i] with { Price = w.Prices[i].Price * (1.3 + 0.1 * salt) };
        for (int i = 0; i < w.ClassStates.Count; i++)
            if (H(w.ClassStates[i].Settlement)) w.ClassStates[i] = w.ClassStates[i] with { Active = 1 - w.ClassStates[i].Active };

        int maxUnit = -1, template = -1;
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
        {
            maxUnit = Math.Max(maxUnit, w.MilitaryUnits[i].Id);
            if (w.MilitaryUnits[i].Owner.Value != Hidden.Value) continue;
            Assert.False(k.KnowsUnit(w.MilitaryUnits[i]));
            w.MilitaryUnits[i] = w.MilitaryUnits[i] with { Experience = w.MilitaryUnits[i].Experience + 3.5 + salt };
            template = i;
        }
        Assert.True(template >= 0);
        MilitaryUnitRow t = w.MilitaryUnits[template];
        w.MilitaryUnits.Add(t with { Id = maxUnit + 1, Experience = 9.0 + salt });
        Assert.False(k.KnowsUnit(w.MilitaryUnits[w.MilitaryUnits.Count - 1]));

        int tax = -1;
        for (int i = 0; i < w.TaxPolicies.Count; i++) if (w.TaxPolicies[i].Polity.Value == Hidden.Value) tax = i;
        if (tax >= 0) w.TaxPolicies[tax] = new TaxPolicyRow(Hidden, 0.31 + 0.01 * salt);
        else w.TaxPolicies.Add(new TaxPolicyRow(Hidden, 0.31 + 0.01 * salt));

        w.Terrain = UnderUnknownNodes(w.Terrain!, k, salt);
    }

    /// <summary>A copy of <paramref name="terrain"/> whose elevation, temperature, moisture and fertility are changed in
    /// every cell of a node <paramref name="k"/> does not know (water, movement cost and rivers — the geography the
    /// systems path over — are kept, so the copy is still the same map to every system).</summary>
    private static TerrainSet UnderUnknownNodes(TerrainSet terrain, PlayerKnowledge k, int salt)
    {
        double[] elevation = terrain.Elevation.ToArray(), temperature = terrain.Temperature.ToArray();
        double[] moisture = terrain.Moisture.ToArray(), fertility = terrain.Fertility.ToArray();
        int changed = 0;
        for (int cell = 0; cell < elevation.Length; cell++)
        {
            if (k.KnowsCell(cell)) continue;
            elevation[cell] += 0.013 * (1 + salt);
            temperature[cell] += 1.5 + salt;
            moisture[cell] = Math.Clamp(moisture[cell] * 0.7, 0.0, 1.0);
            fertility[cell] = Math.Clamp(fertility[cell] * 0.6 + 0.01, 0.0, 1.0);
            changed++;
        }
        Assert.True(changed > 0);
        var polylines = new int[terrain.RiverPolylineCount][];
        for (int i = 0; i < polylines.Length; i++) polylines[i] = terrain.RiverPolyline(i).ToArray();
        ConstructorInfo ctor = typeof(TerrainSet).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
        return (TerrainSet)ctor.Invoke([terrain.Size, terrain.KmPerPx, terrain.SeaLevel, elevation, terrain.Water.ToArray(),
            temperature, moisture, fertility, terrain.MovementCost.ToArray(), terrain.Rivers.ToArray(), polylines]);
    }

    /// <summary>The rig's world after the step, perturbed (salt 1).</summary>
    private static WorldState PerturbedNext()
    {
        Rig r = Played.Value;
        WorldState w = r.Next.Clone();
        Perturb(w, Stub(w), r.Config, 1);
        return w;
    }

    // ================================================================== the readers, as text

    private static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    private static string View(PlayerView v)
    {
        var sb = new StringBuilder(v.Title).Append(" / ").Append(v.Subtitle).Append('\n');
        foreach (ViewBlock b in v.Blocks) sb.Append(b.Heading).Append(": ").Append(string.Join(" ; ", b.Lines)).Append('\n');
        return sb.ToString();
    }

    private static string Hud(HudModel h) => string.Join("|", h.TitleLine, h.PopulationLine, h.FoodLine, h.SplitLine, h.WorldLine,
        h.WorldFoodFigure, h.SettlementCountDetail, h.HappinessLine, h.GrievanceLine, string.Join(";", h.NeedLines ?? []));

    /// <summary>Every static reader of one world through <paramref name="k"/>, by category.</summary>
    private static SortedDictionary<string, string> Readers(IReadOnlyWorldState w, PlayerKnowledge k, Rig r, int maxSettlement, int maxUnit)
    {
        SimConfig cfg = r.Config;
        var cat = new SortedDictionary<string, string>(StringComparer.Ordinal);
        WorldProjection p = WorldProjection.Build(w, cfg, r.Name, Player, knowledge: k);
        cat["projection+lens"] = string.Join("\n", ProjectionDigest.Lines("w", p)) + "\n" + ProjectionDigest.Of(p);

        var hud = new StringBuilder();
        var views = new StringBuilder();
        var market = new StringBuilder();
        var needs = new StringBuilder();
        for (int id = -1; id <= maxSettlement; id++)
        {
            hud.Append(Hud(HudModel.From(w, id, cfg.Needs, id >= 0 ? r.Name(id) : null, cfg, k))).Append('\n');
            if (id < 0) continue;
            views.Append(View(PlayerViews.Settlement(w, cfg, Player, null, id, r.Name, 3, k)));
            foreach (MarketGoodRow m in MarketModel.Rows(w, id, cfg.Goods!, k))
                market.Append(m.Line).Append('\n');
            foreach (GoodEntry g in cfg.Goods!.Goods)
                market.Append(MarketModel.Breakdown(w, id, g.Id, cfg.Goods!, k)?.HeaderLine ?? "-").Append('\n');
            foreach (NeedsClassBlock b in NeedsPanelModel.Blocks(w, id, cfg.Needs!, cfg.Registries.Classes, k))
                needs.Append(b.HeaderLine).Append(':').Append(string.Join(";", b.NeedLines)).Append('\n');
        }
        views.Append(View(PlayerViews.Empire(w, cfg, Player, r.Name, 3, k)));
        views.Append(View(PlayerViews.Institutions(w, cfg, Player, r.Name, 3)));
        cat["hud"] = hud.ToString();
        cat["player views"] = views.ToString();
        cat["market"] = market.ToString();
        cat["needs"] = needs.ToString();

        var tab = new List<int>();
        int cur = -1;
        for (int i = 0; i <= w.Settlements.Count; i++) { cur = SettlementSelection.CycleNext(w, cur, k); tab.Add(cur); }
        cat["tab"] = string.Join(",", tab);

        var units = new StringBuilder();
        var cards = new StringBuilder();
        for (int u = 0; u <= maxUnit; u++)
        {
            units.Append(UnitCardModel.Build(w, cfg, u, Player, r.Name, k)?.ToString() ?? "none").Append('\n');
            InfoCard card = InfoQuery.Card(w, cfg, Player, InfoSubject.OfFormation(u), settlementName: r.Name, knowledge: k.Core);
            cards.Append(card.Title).Append('|').Append(card.Kind).Append('|').Append(card.What).Append('|').Append(card.StatusLine).Append('\n');
        }
        foreach (GoodEntry g in cfg.Goods!.Goods)
        {
            InfoCard card = InfoQuery.Card(w, cfg, Player, InfoSubject.OfGood(g.Id), settlementName: r.Name, knowledge: k.Core);
            cards.Append(card.Title).Append('|').Append(card.StatusLine).Append('|');
            foreach (InfoLink l in card.EnabledBy) cards.Append(l.Label).Append('/').Append(l.Met).Append('/').Append(l.Note).Append(';');
            cards.Append('\n');
        }
        cat["unit cards"] = units.ToString();
        cat["info cards"] = cards.ToString();
        return cat;
    }

    private static (int MaxSettlement, int MaxUnit) Bounds(params IReadOnlyWorldState[] worlds)
    {
        int s = -1, u = -1;
        foreach (IReadOnlyWorldState w in worlds)
        {
            for (int i = 0; i < w.Settlements.Count; i++) s = Math.Max(s, w.Settlements[i].Id.Value);
            for (int i = 0; i < w.MilitaryUnits.Count; i++) u = Math.Max(u, w.MilitaryUnits[i].Id);
        }
        return (s, u);
    }

    // ================================================================== metamorphic: every static reader

    [Fact]
    public void PerturbingWhatTheStubHides_LeavesEveryStaticReaderIdentical_AndTheAllKnownControlSeesIt()
    {
        Rig r = Played.Value;
        WorldState a = r.Next.Clone();
        WorldState b = PerturbedNext();
        (int maxS, int maxU) = Bounds(a, b);

        SortedDictionary<string, string> ka = Readers(a, Stub(a), r, maxS, maxU), kb = Readers(b, Stub(b), r, maxS, maxU);
        foreach (string category in ka.Keys)
            Assert.True(ka[category] == kb[category], "the stubbed player sees the hidden polity through: " + category);

        // POSITIVE CONTROL: all-known, the same perturbation reaches every category it can reach (Tab cycles ids, which
        // the perturbation does not change).
        SortedDictionary<string, string> ca = Readers(a, All(a), r, maxS, maxU), cb = Readers(b, All(b), r, maxS, maxU);
        foreach (string category in ca.Keys)
        {
            if (category == "tab") { Assert.Equal(ca[category], cb[category]); continue; }
            Assert.True(ca[category] != cb[category], "the perturbation does not reach " + category + " - the comparison above proves nothing for it");
        }
        // And the stub is not all-known's twin: it differs from the control on the unperturbed world.
        Assert.NotEqual(ca["projection+lens"], ka["projection+lens"]);
        Assert.NotEqual(ca["tab"], ka["tab"]);
    }

    [Fact]
    public void TheHistoryBuffersWorldSeries_SumOnlyKnownSettlements()
    {
        Rig r = Played.Value;
        WorldState a0 = r.Prev.Clone(), a1 = r.Next.Clone();
        WorldState b0 = r.Prev.Clone(), b1 = PerturbedNext();
        Perturb(b0, Stub(b0), r.Config, 0);
        string Series(HistoryBuffer h)
        {
            var sb = new StringBuilder();
            foreach (HistoryBuffer.Metric m in new[] { HistoryBuffer.Metric.Population, HistoryBuffer.Metric.Food, HistoryBuffer.Metric.Grievance })
                foreach (float x in h.World(m)) sb.Append(x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            return sb.ToString();
        }
        HistoryBuffer Capture(WorldState p, WorldState n, Func<IReadOnlyWorldState, PlayerKnowledge> k)
        {
            var h = new HistoryBuffer();
            h.Capture(p, k(p));
            h.Capture(n, k(n));
            return h;
        }
        Assert.Equal(Series(Capture(a0, a1, Stub)), Series(Capture(b0, b1, Stub)));
        Assert.NotEqual(Series(Capture(a0, a1, All)), Series(Capture(b0, b1, All)));   // control

        // P7 review (finding 2): a hidden settlement's own PRICE series — the Market and Trends price plots — reads as
        // never captured through the stub, whatever its prices do; all-known it is read, and moves (the control).
        HistoryBuffer ha = Capture(a0, a1, Stub), hb = Capture(b0, b1, Stub);
        bool priceMoved = false;
        foreach (int id in KnowledgeStubs.SettlementsOf(a1, Hidden))
            foreach (GoodEntry g in r.Config.Goods!.Goods)
            {
                Assert.Empty(ha.Price(id, g.Id, Stub(a1)));
                Assert.Empty(hb.Price(id, g.Id, Stub(b1)));
                priceMoved |= !ha.Price(id, g.Id, All(a1)).SequenceEqual(hb.Price(id, g.Id, All(b1)));
            }
        Assert.True(priceMoved, "no hidden settlement's price series moved all-known - the control is void");
        foreach (int id in KnowledgeStubs.SettlementsOf(a1, Player))
            foreach (GoodEntry g in r.Config.Goods!.Goods)
                Assert.Equal(ha.Price(id, g.Id, Stub(a1)), hb.Price(id, g.Id, Stub(b1)));
    }

    // ================================================================== metamorphic: Trends (the plotted values)

    [Fact]
    public void TheTrendsWorldSeries_AreIdentical_WhateverTheHiddenPolityDoes()
    {
        Rig r = Played.Value;
        WorldState a0 = r.Prev.Clone(), a1 = r.Next.Clone();
        WorldState b0 = r.Prev.Clone(), b1 = PerturbedNext();
        Perturb(b0, Stub(b0), r.Config, 0);
        var la = new ObservationLog();
        la.Observe(a0, a1, r.Config, []);
        var lb = new ObservationLog();
        lb.Observe(b0, b1, r.Config, []);

        int reached = 0;
        foreach (TrendMetric m in TrendsModel.Metrics(r.Config.Registries.Classes))
        {
            if (m.IsPrice) continue;
            double[] sa = TrendsModel.World(la, m.Key, m.ClassId, Stub(a1)), sb = TrendsModel.World(lb, m.Key, m.ClassId, Stub(b1));
            Assert.Equal(sa.Length, sb.Length);
            for (int i = 0; i < sa.Length; i++)
                Assert.True(sa[i].Equals(sb[i]), m.Label + " [" + i.ToString(CultureInfo.InvariantCulture) + "]: " + R(sa[i]) + " vs " + R(sb[i]));
            // What PlotLines is handed is the same, value for value.
            Assert.Equal(TrendsModel.ForPlot(sa), TrendsModel.ForPlot(sb));
            double[] ca = TrendsModel.World(la, m.Key, m.ClassId, All(a1)), cb = TrendsModel.World(lb, m.Key, m.ClassId, All(b1));
            bool differs = false;
            for (int i = 0; i < ca.Length; i++) differs |= !ca[i].Equals(cb[i]);
            if (differs) reached++;
        }
        Assert.True(reached >= 3, "the perturbation reaches only " + reached.ToString(CultureInfo.InvariantCulture) + " all-known world series - the control is void");
        // Every known settlement's own series is untouched too.
        PlayerKnowledge k = Stub(a1);
        for (int i = 0; i < a1.Settlements.Count; i++)
        {
            int id = a1.Settlements[i].Id.Value;
            if (!k.KnowsSettlement(id)) continue;
            Assert.Equal(TrendsModel.Settlement(la, id, SeriesKey.Population, -1), TrendsModel.Settlement(lb, id, SeriesKey.Population, -1));
        }
        // P7 review (finding 2): a HIDDEN settlement's own series — the settlement scope — reads through the stub exactly
        // as no selection, whatever it does; all-known it is read, and the perturbation moves it (the control).
        bool settlementMoved = false;
        foreach (int id in KnowledgeStubs.SettlementsOf(a1, Hidden))
            foreach (TrendMetric m in TrendsModel.Metrics(r.Config.Registries.Classes))
            {
                if (m.IsPrice) continue;
                double[] ha = TrendsModel.Settlement(la, id, m.Key, m.ClassId, Stub(a1)), hb = TrendsModel.Settlement(lb, id, m.Key, m.ClassId, Stub(b1));
                Assert.Equal(TrendsModel.Settlement(la, -1, m.Key, m.ClassId), ha);
                Assert.Equal(ha, hb);
                settlementMoved |= !TrendsModel.Settlement(la, id, m.Key, m.ClassId, All(a1)).SequenceEqual(TrendsModel.Settlement(lb, id, m.Key, m.ClassId, All(b1)));
            }
        Assert.True(settlementMoved, "no hidden settlement's own series moved all-known - the control is void");
    }

    // ================================================================== metamorphic: the annals (hidden EVENTS planted)

    /// <summary>Plants, in <paramref name="next"/>, an event of every kind the state chronicle detects — each about the
    /// hidden polity or one of its settlements: a construction completed, an institution founded, a merchant class (people
    /// in every class bucket), the tax changed, an Age entered, a settlement passing to another polity, re-equipped
    /// formations.</summary>
    private static void PlantHiddenEvents(WorldState next, SimConfig cfg)
    {
        IReadOnlyList<int> hidden = KnowledgeStubs.SettlementsOf(next, Hidden);
        var s0 = new SettlementId(hidden[0]);
        int project = cfg.Goods!.Projects![0].Id;
        next.Structures.Add(new StructureRow(s0, project + 1000, 1));
        int instId = 0;
        for (int i = 0; i < next.Institutions.Count; i++) instId = Math.Max(instId, next.Institutions[i].Id + 1);
        next.Institutions.Add(new InstitutionRow(instId, Hidden, s0, cfg.Research!.UniversityTypes[0].Key, next.Clock.Turn, 0.0));
        int age = -1;
        for (int i = 0; i < next.AgeStates.Count; i++) if (next.AgeStates[i].Polity.Value == Hidden.Value) age = i;
        if (age >= 0) next.AgeStates[age] = next.AgeStates[age] with { Age = next.AgeStates[age].Age + 1, EnteredTurn = next.Clock.Turn };
        // The second hidden settlement passes to a polity outside the roster (as a revolt would make one).
        var s1 = new SettlementId(hidden[1]);
        for (int i = 0; i < next.Controls.Count; i++)
            if (next.Controls[i].Place == s1) next.Controls[i] = new ControlRow(new PolityId(99), s1, 1.0);
        for (int i = 0; i < next.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = next.MilitaryUnits[i];
            if (u.Owner.Value != Hidden.Value) continue;
            next.UnitConversions.Add(new UnitConversionRow(next.Clock.Turn, u.Id, Hidden, u.Family, u.Identity, u.Family, u.Identity + 1, 1, 2, 1));
        }
    }

    [Fact]
    public void TheAnnals_TellNothingOfWhatTheStubHides_EvenItsEvents()
    {
        Rig r = Played.Value;
        WorldState a0 = r.Prev.Clone(), a1 = r.Next.Clone();
        WorldState b0 = r.Prev.Clone(), b1 = PerturbedNext();
        Perturb(b0, Stub(b0), r.Config, 0);
        PlantHiddenEvents(b1, r.Config);

        List<string> StateEvents(WorldState p, WorldState n, PlayerKnowledge? k)
        {
            var lines = new List<string>();
            new StateChronicle().Observe(p, n, r.Config, r.Name, lines, k);
            return lines;
        }
        List<string> CoreEvents(WorldState p, WorldState n, PlayerKnowledge? k)
        {
            ChronicleConfig ccfg;
            using (Stream stream = Sim.Data.DataFiles.OpenChronicle()) ccfg = ChronicleConfigLoader.Load(stream);
            var collector = new ChronicleCollector(ccfg, r.Config);
            collector.Observe(p);
            collector.Observe(n);
            NameRegistry names = NameRegistry.Build(ccfg, n.Seed, n);
            var lines = new List<string>();
            foreach (ChronicleEvent e in collector.Events)
                if (k is null || UiSession.AdmitsAnnal(k, e)) lines.Add(ChronicleProse.Render(e, ccfg, names));
            return lines;
        }

        Assert.Equal(StateEvents(a0, a1, Stub(a1)), StateEvents(b0, b1, Stub(b1)));
        Assert.Equal(CoreEvents(a0, a1, Stub(a1)), CoreEvents(b0, b1, Stub(b1)));
        // POSITIVE CONTROLS: unfiltered, the planted events are told (and the core collector sees the hidden famine).
        List<string> told = StateEvents(b0, b1, null);
        Assert.NotEqual(StateEvents(a0, a1, null), told);
        Assert.True(told.Count >= StateEvents(b0, b1, Stub(b1)).Count + 5, "fewer than five hidden events were planted: " + string.Join(" | ", told));
        Assert.NotEqual(CoreEvents(a0, a1, null), CoreEvents(b0, b1, null));
        // And the filter keeps every event about what the player knows (all-known: exactly the unfiltered list).
        Assert.Equal(StateEvents(a0, a1, null), StateEvents(a0, a1, All(a1)));
    }

    [Fact]
    public void ACoreChronicleEvent_ReachesTheAnnals_OnlyWhenItsSettlementIsNotHidden()
    {
        Rig r = Played.Value;
        PlayerKnowledge k = Stub(r.Next);
        foreach (int id in KnowledgeStubs.SettlementsOf(r.Next, Hidden))
            Assert.False(UiSession.AdmitsAnnal(k, new ChronicleEvent(ChronicleEventType.FamineOnset, 6, 0.0, id, 0, 0)));
        foreach (int id in KnowledgeStubs.SettlementsOf(r.Next, Player))
            Assert.True(UiSession.AdmitsAnnal(k, new ChronicleEvent(ChronicleEventType.FamineOnset, 6, 0.0, id, 0, 0)));
        Assert.True(UiSession.AdmitsAnnal(k, new ChronicleEvent(ChronicleEventType.Extinction, 6, 0.0, 9999, 0, 0)));   // no such row: not hidden
        foreach (int id in KnowledgeStubs.SettlementsOf(r.Next, Hidden))
            Assert.True(UiSession.AdmitsAnnal(All(r.Next), new ChronicleEvent(ChronicleEventType.FamineOnset, 6, 0.0, id, 0, 0)));
    }

    // ================================================================== metamorphic: FrameText of the real UI

    private static List<string> Frames(UiSession s, string dir)
    {
        using UiFrameHarness h = UiFrameHarness.Start(s, dir, Assets());
        h.Idle(3);
        var all = new List<string> { "== map" };
        all.AddRange(h.Ui.FrameText.Texts());
        foreach (Section section in GameSections.PlayerOrder)
        {
            Assert.True(h.ClickControl("nav:" + section), "no nav button for " + section);
            h.Idle(3);
            all.Add("== " + section);
            all.AddRange(h.Ui.FrameText.Texts());
        }
        Assert.Empty(h.Problems);
        return all;
    }

    [Fact]
    public void FrameText_OfEveryPlayerSection_IsIdentical_WhateverTheHiddenPolityIs()
    {
        Rig r = Played.Value;
        WorldState a = r.Next.Clone(), b = PerturbedNext();
        List<string> fa = Frames(UiSession.StartFrom(a, 42, null, null, Stub), Path.Combine(_root, "a"));
        List<string> fb = Frames(UiSession.StartFrom(b, 42, null, null, Stub), Path.Combine(_root, "b"));
        Assert.Equal(fa, fb);
        // No name of a hidden settlement is drawn anywhere.
        foreach (int id in KnowledgeStubs.SettlementsOf(a, Hidden))
            foreach (string text in fa) Assert.False(HasWord(text, r.Name(id)), r.Name(id) + " drawn: " + text);
        // POSITIVE CONTROL: the all-known UI over the same two worlds draws a difference.
        List<string> ca = Frames(UiSession.StartFrom(r.Next.Clone(), 42), Path.Combine(_root, "ca"));
        List<string> cb = Frames(UiSession.StartFrom(PerturbedNext(), 42), Path.Combine(_root, "cb"));
        Assert.NotEqual(ca, cb);
    }

    [Fact]
    public void RawSettlementIds_AreNotInPlayerText()
    {
        Rig r = Played.Value;
        Assert.Equal(HudModel.NoSelectionTitle, HudModel.From(r.Next, -1).TitleLine);
        Assert.Equal(HudModel.UnnamedTitle, HudModel.From(r.Next, r.Next.Settlements[1].Id.Value).TitleLine);
        Assert.Equal(r.Name(r.Next.Settlements[1].Id.Value), HudModel.From(r.Next, r.Next.Settlements[1].Id.Value, settlementName: r.Name(r.Next.Settlements[1].Id.Value)).TitleLine);
        // No player surface of the real UI prints "settlement N" (the dense id would count the world's settlements).
        foreach (string text in Frames(UiSession.StartFrom(r.Next.Clone(), 42), Path.Combine(_root, "ids")))
            Assert.False(Regex.IsMatch(text, @"(?i)\bsettlement\s+\d+"), "a raw settlement id in player text: " + text);
    }

    private static bool HasWord(string text, string word) =>
        Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(word) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void TheSettlementView_ListsOnlyTheFormationsThePlayerKnows_WhereverTheyStand()
    {
        // The view and the seam agree formation by formation: a rival formation stationed in the player's own capital is
        // listed while the seam knows it, and not when the seam does not (here: the capital's node out of sight, the one
        // case where a known settlement can hold an unknown formation). The control: all-known lists it.
        Rig r = Played.Value;
        WorldState w = r.Next.Clone();
        Assert.True(EmpireQuery.TryGetCapital(w, Player, out SettlementId capital));
        int capitalCell = -1;
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == capital) capitalCell = w.Settlements[i].SiteCell;
        int rival = -1;
        for (int i = 0; i < w.MilitaryUnits.Count && rival < 0; i++) if (w.MilitaryUnits[i].Owner.Value == Hidden.Value) rival = i;
        Assert.True(rival >= 0);
        int size = w.Terrain!.Size;
        w.MilitaryUnits[rival] = w.MilitaryUnits[rival] with { Location = capital, X = capitalCell % size, Y = capitalCell / size };
        int unitId = w.MilitaryUnits[rival].Id;

        bool[] mask = (bool[])r.Mask.Clone();
        mask[PlayerKnowledge.Of(w).Core.NodeOfCell(capitalCell)] = false;
        PlayerKnowledge unseen = KnowledgeStubs.Over(w, Player, mask);
        Assert.True(unseen.KnowsSettlement(capital.Value));             // its own: always known
        Assert.False(unseen.KnowsUnit(w.MilitaryUnits[rival]));          // the rival formation in it: not seen

        string Units(PlayerKnowledge k) =>
            string.Join(" ; ", PlayerViews.Settlement(w, r.Config, Player, null, capital.Value, r.Name, 3, k).Blocks[^1].Lines);
        string all = Units(All(w)), hidden = Units(unseen);
        Assert.Contains("The empire of " + Hidden.Value.ToString(CultureInfo.InvariantCulture), all, StringComparison.Ordinal);
        Assert.DoesNotContain("The empire of " + Hidden.Value.ToString(CultureInfo.InvariantCulture), hidden, StringComparison.Ordinal);
        Assert.Null(UnitCardModel.Build(w, r.Config, unitId, Player, r.Name, unseen));
        Assert.NotNull(UnitCardModel.Build(w, r.Config, unitId, Player, r.Name, All(w)));
    }

    // ================================================================== a selection that a step hides (P7 review, finding 2)

    private static string PolicyText(PolicyView v)
    {
        var sb = new StringBuilder();
        foreach (string l in v.Current) sb.Append("C ").Append(l).Append('\n');
        foreach (PolicyChangeView c in v.History) sb.Append("H ").Append(c.Turn).Append(' ').Append(c.Line).Append(" / ").Append(string.Join(";", c.Consequences)).Append('\n');
        foreach (string l in v.States) sb.Append("S ").Append(l).Append('\n');
        return sb.ToString();
    }

    [Fact]
    public void EverySelectionKeyedReader_ReadsAHiddenSettlementAsNoSelection_AndTheAllKnownControlReadsIt()
    {
        // Reader by reader (GameUi drops a hidden selection before any of them is asked, so the frame test below cannot
        // see a reader that ignores knowledge): the settlement records, the labour record (POLICY), the action surface's
        // labour note, the Trends settlement series and the price series, keyed on a settlement the stub hides, read
        // exactly as no selection (id -1). The control: the all-known twin session (the same world and orders, one step)
        // reads each of them for the same id.
        UiSession s = PlayabilityGate.HiddenRival();
        UiSession all = UiSession.StartFrom(UiSession.Start(42, aiEmpiresOverride: 1).World.Clone(), 42);
        all.EndTurn();
        Assert.Equal(s.World.Clock.Turn, all.World.Clock.Turn);
        PlayerKnowledge k = s.Knowledge;
        Assert.False(k.AllKnown);
        IReadOnlyList<int> hidden = KnowledgeStubs.SettlementsOf(s.World, Hidden);
        Assert.NotEmpty(hidden);
        EraTheme theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Player));
        EraTable era = UiSession.ProductionEra();
        string? NoteOf(UiSession x, int selected) => ActionSurface.ForSession(x, era, selected, theme).Labour?.Note;

        bool policyRead = false, noteRead = false, priceRead = false;
        foreach (int id in hidden)
        {
            Assert.True(k.IsHiddenSettlement(id));
            Assert.Null(ScreenModels.Settlement(s, id));
            Assert.NotNull(ScreenModels.Settlement(all, id));                                       // control
            Assert.Equal(PolicyText(ScreenModels.Policy(s, -1)), PolicyText(ScreenModels.Policy(s, id)));
            policyRead |= PolicyText(ScreenModels.Policy(all, id)) != PolicyText(ScreenModels.Policy(all, -1));
            Assert.Equal(NoteOf(s, -1), NoteOf(s, id));
            noteRead |= NoteOf(all, id) is { } note && note.Contains(all.Names.Name(id), StringComparison.Ordinal);
            foreach (TrendMetric m in TrendsModel.Metrics(s.Config.Registries.Classes))
            {
                if (m.IsPrice) continue;
                Assert.Equal(TrendsModel.Settlement(s.Observations, -1, m.Key, m.ClassId, k), TrendsModel.Settlement(s.Observations, id, m.Key, m.ClassId, k));
            }
            Assert.NotEqual(TrendsModel.Settlement(all.Observations, -1, SeriesKey.Population, -1),
                TrendsModel.Settlement(all.Observations, id, SeriesKey.Population, -1, all.Knowledge));   // control
            foreach (GoodEntry g in s.Config.Goods!.Goods)
            {
                Assert.Empty(s.History.Price(id, g.Id, k));
                priceRead |= all.History.Price(id, g.Id, all.Knowledge).Length > 0;
            }
        }
        Assert.True(policyRead, "the all-known labour record of a rival settlement reads as no selection - the control is void");
        Assert.True(noteRead, "the all-known action surface never named a rival selection - the control is void");
        Assert.True(priceRead, "no rival settlement has a price series all-known - the control is void");
    }

    /// <summary>
    /// The one-AI world from turn 0, played through the real UI: a settlement of the rival is selected by Tab (the first
    /// one Tab reaches, or the <paramref name="rivalsOn"/>-th rival settlement after it) while the rival is KNOWN,
    /// then End Turn is clicked; when <paramref name="hideOnStep"/>, the step hides the rival (S2a's ordinary case: a
    /// foreign settlement goes out of sight). Returns the selected id, the selection after the step and every text the
    /// player sections draw then — Trends in settlement scope (and on each price metric), POLICY's labour record open.
    /// </summary>
    private (int Target, int After, List<string> Texts, HashSet<string> ToldWhileKnown) SelectRivalThenStep(bool hideOnStep, int rivalsOn, string dir)
    {
        bool hide = false;
        UiSession s = UiSession.StartFrom(UiSession.Start(42, aiEmpiresOverride: 1).World.Clone(), 42, null, null,
            knowledgeOf: w => hide ? KnowledgeStubs.HidePolity(w, Player, Hidden) : PlayerKnowledge.Of(w));
        using UiFrameHarness h = UiFrameHarness.Start(s, dir, Assets());
        h.Idle(3);
        IReadOnlyList<int> rival = KnowledgeStubs.SettlementsOf(s.World, Hidden);
        Assert.True(rival.Count >= 2, "the rig needs two rival settlements");
        for (int i = 0; i <= s.World.Settlements.Count && !rival.Contains(h.Ui.SelectedSettlement); i++) h.Key(Keys.Tab);
        for (int n = 0; n < rivalsOn; n++)
        {
            int from = h.Ui.SelectedSettlement;
            h.Key(Keys.Tab);
            for (int i = 0; i <= s.World.Settlements.Count && !(rival.Contains(h.Ui.SelectedSettlement) && h.Ui.SelectedSettlement != from); i++) h.Key(Keys.Tab);
        }
        int target = h.Ui.SelectedSettlement;
        Assert.Contains(target, rival);
        var toldWhileKnown = new HashSet<string>(s.AnnalLines, StringComparer.Ordinal);
        hide = hideOnStep;
        Assert.True(h.ClickControl("end-turn"));
        h.Idle(3);
        Assert.Equal(hideOnStep, s.Knowledge.IsHiddenSettlement(target));
        int after = h.Ui.SelectedSettlement;
        var texts = new List<string> { "== map" };
        texts.AddRange(h.Ui.FrameText.Texts());
        foreach (Section section in GameSections.PlayerOrder)
        {
            Assert.True(h.ClickControl("nav:" + section), "no nav button for " + section);
            h.Idle(3);
            texts.Add("== " + section);
            texts.AddRange(h.Ui.FrameText.Texts());
        }
        Assert.True(h.ClickControl("nav:Policy"));
        h.ClickControl("labour-record");
        h.Idle(3);
        texts.Add("== Policy, labour record");
        texts.AddRange(h.Ui.FrameText.Texts());
        Assert.True(h.ClickControl("nav:Trends"));
        Assert.True(h.ClickControl("trend-place"));
        h.Idle(3);
        texts.Add("== Trends, settlement scope");
        texts.AddRange(h.Ui.FrameText.Texts());
        for (int i = 0; i < h.Ui.TrendMetrics.Count; i++)
        {
            if (!h.Ui.TrendMetrics[i].IsPrice) continue;
            Assert.True(h.ClickControl("trend-" + i.ToString(CultureInfo.InvariantCulture)));
            h.Idle(3);
            texts.Add("== Trends, price");
            texts.AddRange(h.Ui.FrameText.Texts());
        }
        Assert.Empty(h.Problems);
        return (target, after, texts, toldWhileKnown);
    }

    [Fact]
    public void ASelectionThatAStepHides_IsDropped_AndWhichHiddenSettlementItWasLeavesNoTrace()
    {
        // METAMORPHIC on the hidden subject: two plays differ ONLY in which rival settlement was selected before the step
        // hid the rival (a UI choice: the world, the orders and every hash are the same). After the step the selection is
        // dropped, and every player section draws the same text, naming no hidden settlement. The control: when the step
        // hides nothing, the selection survives and the two plays draw different text (each names its own selection).
        Rig r = Played.Value;
        (int t1, int after1, List<string> f1, HashSet<string> told) = SelectRivalThenStep(hideOnStep: true, rivalsOn: 0, Path.Combine(_root, "sel-a"));
        (int t2, int after2, List<string> f2, _) = SelectRivalThenStep(hideOnStep: true, rivalsOn: 1, Path.Combine(_root, "sel-b"));
        Assert.NotEqual(t1, t2);
        Assert.Equal(-1, after1);
        Assert.Equal(-1, after2);
        Assert.Equal(f1, f2);
        // No hidden settlement is named — except in the annal lines told while it was still known (its turn-0 founding:
        // history the player legitimately learnt; the annals filter what is APPENDED, P7 section 2).
        foreach (int id in new[] { t1, t2 })
            foreach (string text in f1)
                if (!told.Contains(text)) Assert.False(HasWord(text, r.Name(id)), r.Name(id) + " drawn after it was hidden: " + text);

        (int c1, int cafter1, List<string> g1, _) = SelectRivalThenStep(hideOnStep: false, rivalsOn: 0, Path.Combine(_root, "sel-ca"));
        (int c2, int cafter2, List<string> g2, _) = SelectRivalThenStep(hideOnStep: false, rivalsOn: 1, Path.Combine(_root, "sel-cb"));
        Assert.Equal(c1, cafter1);
        Assert.Equal(c2, cafter2);
        Assert.NotEqual(g1, g2);
        Assert.Contains(g1, text => HasWord(text, r.Name(c1)) && !told.Contains(text));
    }

    // ================================================================== selection: Tab and the click

    [Fact]
    public void Tab_NeverReachesAHiddenSettlement_AndReachesEveryKnownOne()
    {
        WorldState w = Played.Value.Next;
        PlayerKnowledge k = Stub(w);
        var reached = new List<int>();
        int cur = -1;
        for (int i = 0; i <= 2 * w.Settlements.Count; i++)
        {
            cur = SettlementSelection.CycleNext(w, cur, k);
            Assert.True(k.KnowsSettlement(cur), "Tab selected " + cur.ToString(CultureInfo.InvariantCulture));
            if (!reached.Contains(cur)) reached.Add(cur);
        }
        int known = 0;
        for (int i = 0; i < w.Settlements.Count; i++) if (k.KnowsSettlement(w.Settlements[i].Id.Value)) known++;
        Assert.Equal(known, reached.Count);
        Assert.True(k.KnowsSettlement(SettlementSelection.Opening(w, k)));
        // The control: all-known, Tab does reach the settlements the stub hides.
        var all = new List<int>();
        cur = -1;
        for (int i = 0; i <= w.Settlements.Count; i++) { cur = SettlementSelection.CycleNext(w, cur, All(w)); all.Add(cur); }
        Assert.Contains(all, id => k.IsHiddenSettlement(id));
        // Nothing known: Tab selects nothing.
        bool[] none = new bool[Played.Value.Mask.Length];
        Assert.Equal(-1, SettlementSelection.CycleNext(w, -1, KnowledgeStubs.Over(w, new PolityId(77), none)));
    }

    [Fact]
    public void AClickOnAHiddenSite_SelectsNothing_WhereTheAllKnownMapSelectsIt()
    {
        Rig r = Played.Value;
        WorldState w = r.Next;
        PlayerKnowledge k = Stub(w);
        WorldProjection stub = WorldProjection.Build(w, r.Config, r.Name, Player, knowledge: k);
        WorldProjection all = WorldProjection.Build(w, r.Config, r.Name, Player);
        foreach (int id in KnowledgeStubs.SettlementsOf(w, Hidden))
        {
            SettlementLensView site = all.Settlements.First(s => s.Id == id);
            Assert.DoesNotContain(stub.Settlements, s => s.Id == id);
            var cam = new Camera(stub.WorldSize);
            cam.Clamp(1280, 800);
            cam.ZoomAt(640, 400, 10.0, 1280, 800);
            cam.Pan((cam.CenterX - site.X) * cam.Zoom, (cam.CenterY - site.Y) * cam.Zoom, 1280, 800);
            (double sx, double sy) = cam.WorldToScreen(site.X, site.Y, 1280, 800);
            LensFrame Paint(WorldProjection p) => WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p,
                MapDetail.At(cam.Zoom, 1280, 800, p.KmPerPx), (x, y) => cam.WorldToScreen(x, y, 1280, 800), cam.Zoom, new RectD(0, 0, 1280, 800));
            LensFrame hiddenFrame = Paint(stub);
            int hit = SettlementSelection.HitTest(hiddenFrame, sx, sy);
            Assert.False(k.IsHiddenSettlement(hit), "a click on hidden settlement " + id.ToString(CultureInfo.InvariantCulture) + " selected it");
            Assert.Equal(-1, hit);
            Assert.Equal(-1, UnitSelection.HitTest(hiddenFrame, sx + 20, sy));
            Assert.Equal(id, SettlementSelection.HitTest(Paint(all), sx, sy));   // the control: drawn, it is picked
        }
    }

    [Fact]
    public void TheGateRows_PassOnTheHiddenRivalRig_AndAreNotDrawnWhenNothingIsHidden()
    {
        using (UiFrameHarness h = UiFrameHarness.Start(PlayabilityGate.HiddenRival(), Path.Combine(_root, "gate"), Assets()))
        {
            IReadOnlyList<GateRow> rows = PlayabilityGate.KnowledgeRows(h, "hidden rival");
            Assert.Equal(["Tab never reaches a hidden settlement", "a click on a hidden position selects nothing"],
                rows.Select(x => x.Control).ToArray());
            foreach (GateRow row in rows) Assert.True(row.Result == GateResult.Pass, row.Control + ": " + row.Detail);
            Assert.Empty(h.Problems);
        }
        using (UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42, aiEmpiresOverride: 1), Path.Combine(_root, "gate-all"), Assets()))
            Assert.Empty(PlayabilityGate.KnowledgeRows(h, "one AI"));
    }

    // ================================================================== the seam: all-known is byte-identical

    [Fact]
    public void TheProductionView_IsAllKnown_AndEveryReaderGivesWhatItGaveWithoutKnowledge()
    {
        Rig r = Played.Value;
        WorldState w = r.Next;
        PlayerKnowledge k = PlayerKnowledge.Of(w);
        Assert.True(k.AllKnown);
        for (int i = 0; i < w.Settlements.Count; i++)
        {
            Assert.True(k.KnowsSettlement(w.Settlements[i].Id.Value));
            Assert.False(k.IsHiddenSettlement(w.Settlements[i].Id.Value));
        }
        for (int i = 0; i < w.MilitaryUnits.Count; i++) Assert.True(k.KnowsUnit(w.MilitaryUnits[i]));
        for (int p = 0; p < 4; p++) Assert.True(k.KnowsPolity(p));
        // The default (no knowledge passed) is the production view.
        Assert.Equal(ProjectionDigest.Of(WorldProjection.Build(w, r.Config, r.Name, Player)),
            ProjectionDigest.Of(WorldProjection.Build(w, r.Config, r.Name, Player, knowledge: k)));
    }

    // ================================================================== the source guard

    /// <summary>A raw read of a foreign-sensitive world table through a world receiver (the advisory script's pattern).</summary>
    public static readonly Regex RawRead = new(@"\b(world|w|_world|World|prev|next|W)\.(Settlements|MilitaryUnits|Buckets|Deposits)\b",
        RegexOptions.CultureInvariant);

    /// <summary>The allowlist (scripts/ui-raw-read-allowlist.txt): path → class.</summary>
    private static SortedDictionary<string, string> Allowlist()
    {
        var list = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string raw in File.ReadAllLines(Path.Combine(RepoRoot(), "scripts", "ui-raw-read-allowlist.txt")))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            string[] parts = line.Split('|');
            Assert.True(parts.Length == 3 && parts[2].Trim().Length > 0, "an allowlist line needs a path, a class and a reason: " + line);
            list[parts[0].Trim()] = parts[1].Trim();
        }
        return list;
    }

    /// <summary>The Sim.Ui files (repo-relative, '/') that read raw, from (path, text) pairs.</summary>
    public static SortedSet<string> RawReaders(IEnumerable<(string Path, string Text)> files)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach ((string path, string text) in files) if (RawRead.IsMatch(text)) found.Add(path);
        return found;
    }

    private static IEnumerable<(string Path, string Text)> Sources(string project)
    {
        string root = RepoRoot();
        foreach (string f in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            if (rel.Contains("/bin/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal)) continue;
            yield return (rel, File.ReadAllText(f));
        }
    }

    [Fact]
    public void SourceGuard_RawReadsOfForeignSensitiveTables_OnlyInAllowlistedFiles_AndTheRoutedOnesAskTheSeam()
    {
        SortedDictionary<string, string> allow = Allowlist();
        SortedSet<string> readers = RawReaders(Sources("Sim.Ui"));
        foreach (string f in readers) Assert.True(allow.ContainsKey(f), f + " reads a foreign-sensitive table raw and is not on scripts/ui-raw-read-allowlist.txt");
        foreach ((string path, string cls) in allow)
        {
            Assert.True(readers.Contains(path), "stale allowlist entry (the file no longer reads raw, or is gone): " + path);
            Assert.Contains(cls, new[] { "routed", "seam", "own", "developer", "geometry", "harness" });
            if (cls == "routed")
                Assert.Contains("PlayerKnowledge", File.ReadAllText(Path.Combine(RepoRoot(), path)), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SourceGuard_HasTeeth_AnInjectedRawReadInAnUnlistedFileIsCaught()
    {
        SortedDictionary<string, string> allow = Allowlist();
        var files = new List<(string, string)>(Sources("Sim.Ui"))
        {
            ("Sim.Ui/ViewModel/TradeModel.cs", "static long N(IReadOnlyWorldState world) => world.Buckets.Count;"),
            ("Sim.Ui/Leak.cs", "foreach (var u in next.MilitaryUnits) {}"),
        };
        SortedSet<string> readers = RawReaders(files);
        var unlisted = new List<string>();
        foreach (string f in readers) if (!allow.ContainsKey(f)) unlisted.Add(f);
        Assert.Equal(["Sim.Ui/Leak.cs", "Sim.Ui/ViewModel/TradeModel.cs"], unlisted);
        // A projection's or a record's own Settlements list is not a world read.
        Assert.Empty(RawReaders([("x.cs", "p.Settlements.Count + latest.Settlements.Length + manifest.Settlements")]));
    }

    [Fact]
    public void SourceGuard_TheKnowledgeStub_IsBuiltOnlyByTheHarness_AndNoReaderBuildsASecondView()
    {
        var restricted = new List<string>();
        var from = new List<string>();
        var knowledgeOf = new List<string>();
        foreach (string project in new[] { "Sim.Core", "Sim.Ui", "Sim.Cli", "Sim.Data" })
            foreach ((string path, string text) in Sources(project))
            {
                if (text.Contains("PolityKnowledge.Restricted(", StringComparison.Ordinal)) restricted.Add(path);
                if (text.Contains("PlayerKnowledge.From(", StringComparison.Ordinal)) from.Add(path);
                if (text.Contains("knowledgeOf: w", StringComparison.Ordinal)) knowledgeOf.Add(path);
            }
        Assert.Equal(["Sim.Ui/Headless/KnowledgeStubs.cs"], restricted);
        Assert.Equal(["Sim.Ui/Headless/KnowledgeStubs.cs"], from);
        Assert.Equal(["Sim.Ui/Headless/PlayabilityGate.cs"], knowledgeOf);
    }
}
