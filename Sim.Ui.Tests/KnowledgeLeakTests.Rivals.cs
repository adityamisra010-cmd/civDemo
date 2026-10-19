using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Chronicle;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Headless;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-060 — RIVAL INTERNALS ISOLATION (the Director, 2026-10-19: "i should not be able to see other empire's data";
/// docs/d060-rival-internals-isolation.md). P7's tests hide a polity WHOLE through a knowledge stub; production knowledge is
/// all-known, so a KNOWN rival's figures went through every surface that only asked <see cref="PlayerKnowledge.IsHiddenSettlement"/>.
/// These tests run under THE PRODUCTION VIEW (<see cref="PlayerKnowledge.Of(IReadOnlyWorldState)"/>, all-known) of the
/// one-AI world after six played turns, and per surface: the rival's BOOKS are perturbed — its people, grain and goods and
/// their last harvest, dwellings, labour split, food deficit, grievance, needs, prices and price terms, class states, a
/// queued and part-built project, its formations' experience and army — while its PUBLIC identity (name, site, owner) is
/// held; the rival-facing output must be byte-identical and still name the rival and its owner. POSITIVE CONTROL: the same
/// perturbation of the player's OWN selected settlement changes the same output.
/// </summary>
public partial class KnowledgeLeakTests
{
    private static SettlementId RivalOf(IReadOnlyWorldState w) => new(KnowledgeStubs.SettlementsOf(w, Hidden)[0]);

    private static SettlementId OwnOf(IReadOnlyWorldState w) => new(KnowledgeStubs.SettlementsOf(w, Player)[0]);

    private static int SiteOf(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.Settlements.Count; i++) if (w.Settlements[i].Id == s) return w.Settlements[i].SiteCell;
        throw new InvalidOperationException("no settlement " + s.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Perturbs settlement <paramref name="s"/>'s BOOKS in <paramref name="w"/> (salt varies the amounts) and nothing of its
    /// public identity: its name (the registry is built from the seed and ids), site, controller and the control relation
    /// are untouched. People and goods move only through the Ledger (Law 1).
    /// </summary>
    private static void PerturbBooks(WorldState w, SettlementId s, SimConfig cfg, int salt)
    {
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == s)
                ledger.Flow(ref w.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment,
                    11 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Settlement != s) continue;
            ledger.Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(w.GoodStocks[i].Good), ReasonIds.InitialEndowment,
                1000 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
            w.GoodStocks.Ref(i).LastProducedUnits += 333 + salt;
        }
        for (int i = 0; i < w.Housing.Count; i++)
            if (w.Housing[i].Settlement == s)
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings, ReasonIds.InitialEndowment,
                    5 + salt, FlowDirection.Source, OverdrawPolicy.Throw);
        bool allocated = false;
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s)
            { w.SectorAllocations[i] = new SectorAllocationRow(s, 0.1, 0.2, 0.3, 0.2, 0.2 + 0.01 * salt); allocated = true; }
        if (!allocated) w.SectorAllocations.Add(new SectorAllocationRow(s, 0.1, 0.2, 0.3, 0.2, 0.2 + 0.01 * salt));
        for (int i = 0; i < w.ConsumptionDeficits.Count; i++)
            if (w.ConsumptionDeficits[i].Settlement == s)
                w.ConsumptionDeficits[i] = w.ConsumptionDeficits[i] with { DeficitRatio = 0.4 + 0.05 * salt, DemandUnits = 4321 + salt };
        for (int i = 0; i < w.Grievances.Count; i++)
            if (w.Grievances[i].Settlement == s) w.Grievances[i] = w.Grievances[i] with { Value = w.Grievances[i].Value + 0.5 + salt };
        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
            if (w.NeedSatisfactions[i].Settlement == s) w.NeedSatisfactions[i] = w.NeedSatisfactions[i] with { Value = w.NeedSatisfactions[i].Value * (0.5 - 0.05 * salt) };
        for (int i = 0; i < w.Prices.Count; i++)
            if (w.Prices[i].Settlement == s) w.Prices[i] = w.Prices[i] with { Price = w.Prices[i].Price * (1.3 + 0.1 * salt) };
        for (int i = 0; i < w.PriceTerms.Count; i++)
            if (w.PriceTerms[i].Settlement == s)
                w.PriceTerms[i] = w.PriceTerms[i] with { PrevPrice = w.PriceTerms[i].PrevPrice + 0.25 + salt, Delta = w.PriceTerms[i].Delta + 0.125, Consumption = w.PriceTerms[i].Consumption + 0.5 };
        for (int i = 0; i < w.ClassStates.Count; i++)
            if (w.ClassStates[i].Settlement == s) w.ClassStates[i] = w.ClassStates[i] with { Active = 1 - w.ClassStates[i].Active };
        int project = cfg.Goods!.Projects![0].Id;
        w.ConstructionQueue.Add(new ConstructionQueueRow(s, 7 + salt, project));
        w.ConstructionProgress.Add(new ConstructionProgressRow(s, 7 + salt, project, 3 + salt, 250_000 + 1_000 * salt));
        for (int i = 0; i < w.MilitaryUnits.Count; i++)
            if (w.MilitaryUnits[i].Location == s)
                w.MilitaryUnits[i] = w.MilitaryUnits[i] with { Experience = w.MilitaryUnits[i].Experience + 3.5 + salt, Army = 7 + salt };
    }

    /// <summary>The played world with <paramref name="s"/>'s books perturbed (salt 1); unperturbed for salt 0.</summary>
    private static WorldState Books(SettlementId s, int salt)
    {
        Rig r = Played.Value;
        WorldState w = r.Next.Clone();
        if (salt > 0) PerturbBooks(w, s, r.Config, salt);
        return w;
    }

    private static string Card(InfoCard c)
    {
        var sb = new StringBuilder(c.Title).Append('|').Append(c.Kind).Append('|').Append(c.What).Append('|').Append(c.Status)
            .Append('|').Append(c.StatusLine).Append('|').Append(c.Summary).Append('\n');
        foreach (InfoLink l in c.Requirements) sb.Append(l.Label).Append('/').Append(l.Met).Append('/').Append(l.Note).Append('\n');
        return sb.ToString();
    }

    /// <summary>
    /// One player-facing surface's text for settlement <paramref name="id"/> of <paramref name="w"/>, through the PRODUCTION
    /// view (<see cref="PlayerKnowledge.Of(IReadOnlyWorldState)"/>, all-known) — keyed L1..L7 as the D-060 record numbers them.
    /// </summary>
    private static string Surface(string key, WorldState w, SettlementId id)
    {
        Rig r = Played.Value;
        SimConfig cfg = r.Config;
        PlayerKnowledge k = All(w);
        switch (key)
        {
            case "L1 selection card":
            {
                HudModel h = HudModel.From(w, id.Value, cfg.Needs, r.Name(id.Value), cfg, k);
                return Hud(h) + "|" + h.ReadsInternals + "|" + h.OwnerLine + "|" + string.Join(";", h.ClosedBooksLines);
            }
            case "L2 settlement view":
                return View(PlayerViews.Settlement(w, cfg, Player, null, id.Value, r.Name, 3, k));
            case "L3 needs panel":
            {
                var sb = new StringBuilder();
                foreach (NeedsClassBlock b in NeedsPanelModel.Blocks(w, id.Value, cfg.Needs!, cfg.Registries.Classes, k))
                    sb.Append(b.HeaderLine).Append(':').Append(string.Join(";", b.NeedLines)).Append('\n');
                return sb.ToString();
            }
            case "L4 market panel":
            {
                var sb = new StringBuilder();
                foreach (MarketGoodRow m in MarketModel.Rows(w, id.Value, cfg.Goods!, k)) sb.Append(m.Line).Append('\n');
                foreach (GoodEntry g in cfg.Goods!.Goods)
                {
                    PriceBreakdown? b = MarketModel.Breakdown(w, id.Value, g.Id, cfg.Goods!, k);
                    sb.Append(b is null ? "-" : b.HeaderLine + ";" + string.Join(";", b.TermLines) + ";" + b.DriverLine).Append('\n');
                }
                return sb.ToString();
            }
            case "L6 founding card":
                return Card(InfoQuery.Card(w, cfg, Player, InfoSubject.OfFounding(id.Value), settlementName: r.Name, knowledge: k.Core));
            case "L7 unit card":
            {
                var sb = new StringBuilder();
                for (int i = 0; i < w.MilitaryUnits.Count; i++)
                {
                    if (w.MilitaryUnits[i].Location != id) continue;
                    UnitCard? c = UnitCardModel.Build(w, cfg, w.MilitaryUnits[i].Id, Player, r.Name, k);
                    Assert.NotNull(c);
                    sb.Append(c.Title).Append('|').Append(c.Owner).Append('|').Append(c.Family).Append('|').Append(c.Line).Append('|')
                        .Append(c.AgeForm).Append('|').Append(c.Station).Append('|').Append(c.NextAge).Append('|').Append(c.Position)
                        .Append('|').Append(c.Experience).Append('|').Append(c.Army).Append('\n');
                }
                Assert.True(sb.Length > 0, "no formation stationed at " + r.Name(id.Value) + " - the unit card comparison is vacuous");
                return sb.ToString();
            }
            case "L7 map projection":
            {
                // What the lens carries for the settlement BEYOND its token's geometry (Population and Dwellings size the
                // drawn footprint and blocks — the map as drawn, public by the D-060 split — so they are not compared here;
                // the lens TEXT is compared in the real-UI test at Settlement zoom).
                WorldProjection p = WorldProjection.Build(w, cfg, r.Name, Player, knowledge: k);
                SettlementLensView v = p.Settlements.Single(x => x.Id == id.Value);
                return v.Name + "|" + R(v.X) + "|" + R(v.Y) + "|" + v.Controller + "|" + v.IsCapital + "|" + v.ReadsInternals + "|"
                    + v.SizeTier + "|" + v.Notables + "|" + v.ClassesActive + "|" + (v.TopSectorLabel ?? "-") + "|"
                    + (v.Sectors is { } sec ? string.Join(",", sec.Select(R)) : "-");
            }
            default: throw new ArgumentOutOfRangeException(nameof(key), key, "no such surface");
        }
    }

    /// <summary>The public identity each surface must still show for the rival (null: the surface shows no text of it).</summary>
    private static string[] PublicIdentity(string key, IReadOnlyWorldState w, SettlementId rival)
    {
        string name = Played.Value.Name(rival.Value);
        return key switch
        {
            "L1 selection card" => [name, "Held by the empire of 2", PlayerKnowledge.NotYoursLine],
            "L2 settlement view" => [name, "held by empire 2", PlayerKnowledge.NotYoursLine],
            "L6 founding card" => [name, "not known to you", "is not yours"],
            "L7 unit card" => ["A rival Empire's formation (polity 2)", "Stationed at " + name, UnitCardModel.RivalRecordText],
            "L7 map projection" => [name],
            _ => [],
        };
    }

    [Theory]
    [InlineData("L1 selection card")]
    [InlineData("L2 settlement view")]
    [InlineData("L3 needs panel")]
    [InlineData("L4 market panel")]
    [InlineData("L6 founding card")]
    [InlineData("L7 unit card")]
    [InlineData("L7 map projection")]
    public void D060_AKnownRivalsBooks_DoNotReachTheSurface_UnderTheProductionView_WhileThePlayersOwnDo(string surface)
    {
        Rig r = Played.Value;
        SettlementId rival = RivalOf(r.Next), own = OwnOf(r.Next);
        Assert.True(All(r.Next).KnowsSettlement(rival.Value) && !All(r.Next).ReadsInternals(rival.Value), "rig: the rival is known, its books closed");
        Assert.True(All(r.Next).ReadsInternals(own.Value), "rig: the player's own books are open");

        // The rival's books perturbed, its identity held: byte-identical output, and its public identity still shown.
        WorldState a = Books(rival, 0), b = Books(rival, 1);
        Assert.Equal(r.Name(rival.Value), Played.Value.Name(rival.Value));
        Assert.Equal(SiteOf(a, rival), SiteOf(b, rival));
        string ra = Surface(surface, a, rival), rb = Surface(surface, b, rival);
        Assert.True(ra == rb, surface + " shows a known rival's books:\n" + ra + "\n---\n" + rb);
        foreach (string token in PublicIdentity(surface, a, rival))
            Assert.True(ra.Contains(token, StringComparison.Ordinal), surface + " lost the rival's public identity '" + token + "':\n" + ra);

        // POSITIVE CONTROL: the same perturbation of the player's OWN settlement reaches the same surface.
        string oa = Surface(surface, Books(own, 0), own), ob = Surface(surface, Books(own, 1), own);
        Assert.True(oa != ob, surface + ": the perturbation does not reach the player's own settlement - the equality above proves nothing");
    }

    [Fact]
    public void D060_TheDeveloperView_StillReadsEveryKnownEmpiresBooks_AndOnlyGameUiAsksForIt()
    {
        // Scope decision (D-060 §5): the F12 DEV panels stay world-wide. The developer view reads the rival's books — the
        // perturbation reaches its HUD lines, market and needs — and a hidden settlement stays hidden to it.
        Rig r = Played.Value;
        SettlementId rival = RivalOf(r.Next);
        WorldState a = Books(rival, 0), b = Books(rival, 1);
        string Dev(WorldState w)
        {
            PlayerKnowledge d = All(w).ForDeveloper();
            Assert.True(d.IsDeveloperView && d.ReadsInternals(rival.Value));
            HudModel h = HudModel.From(w, rival.Value, r.Config.Needs, r.Name(rival.Value), r.Config, d);
            var sb = new StringBuilder(Hud(h)).Append('\n');
            foreach (MarketGoodRow m in MarketModel.Rows(w, rival.Value, r.Config.Goods!, d)) sb.Append(m.Line).Append('\n');
            return sb.ToString();
        }
        Assert.NotEqual(Dev(a), Dev(b));
        Assert.False(Stub(r.Next).ForDeveloper().ReadsInternals(rival.Value), "the developer view must not open a HIDDEN settlement");

        // Source guard: ForDeveloper is called by GameUi (the DEV panels) and nothing else in the shipped projects.
        var callers = new List<string>();
        foreach (string project in new[] { "Sim.Core", "Sim.Ui", "Sim.Cli", "Sim.Data" })
            foreach ((string path, string text) in Sources(project))
                if (text.Contains(".ForDeveloper()", StringComparison.Ordinal)) callers.Add(path);
        Assert.Equal(["Sim.Ui/GameUi.cs"], callers);
    }

    // ================================================================== L5: the annals

    [Fact]
    public void D060_TheAnnalsView_TellsARivalsFoundingWithoutItsFigure_AndNothingOfItsBooks_WhileTheSessionRecordKeepsThem()
    {
        Rig r = Played.Value;
        SettlementId rival = RivalOf(r.Next), own = OwnOf(r.Next);
        // A fresh session over each world fires every settlement's founding on first sight, its people the event's figure.
        UiSession a = UiSession.StartFrom(Books(rival, 0), 42), b = UiSession.StartFrom(Books(rival, 1), 42);
        Assert.Equal(a.AnnalLines, b.AnnalLines);
        string rivalName = r.Name(rival.Value);
        string told = Assert.Single(a.AnnalLines, l => l.Contains(rivalName, StringComparison.Ordinal));
        Assert.Matches(@"^In the year -?\d+, " + System.Text.RegularExpressions.Regex.Escape(rivalName) + @" was founded\.$", told);
        // The session record (chronicle-*.txt) stays world-wide (scope decision, D-060 §6): there the figure moves.
        Assert.NotEqual(a.RecordAnnalLines, b.RecordAnnalLines);
        // POSITIVE CONTROL: the player's own founding carries its figure, which the same perturbation moves.
        UiSession c = UiSession.StartFrom(Books(own, 1), 42);
        Assert.NotEqual(a.AnnalLines, c.AnnalLines);
        Assert.Contains(a.AnnalLines, l => l.Contains(r.Name(own.Value), StringComparison.Ordinal) && l.Contains("strong", StringComparison.Ordinal));

        // A core event of the rival's books is not told at all; the same event at the player's own settlement is.
        PlayerKnowledge k = All(r.Next);
        foreach (ChronicleEventType t in new[] { ChronicleEventType.FamineOnset, ChronicleEventType.FamineEnd, ChronicleEventType.Extinction,
                     ChronicleEventType.FirstArtisans, ChronicleEventType.MigrationSurge, ChronicleEventType.Disaster,
                     ChronicleEventType.FoodShortfallOnset, ChronicleEventType.FoodShortfallEnd })
        {
            Assert.Null(UiSession.PlayerAnnal(k, new ChronicleEvent(t, 6, 70.0, rival.Value, 0.4, 3), "x", r.Name));
            Assert.Equal("x", UiSession.PlayerAnnal(k, new ChronicleEvent(t, 6, 70.0, own.Value, 0.4, 3), "x", r.Name));
        }
    }

    /// <summary>Plants, in <paramref name="next"/>, the state-chronicle events that tell a settlement's or a polity's
    /// books at <paramref name="s"/> (owned by <paramref name="owner"/>): its owner's tax levy changed, a merchant class
    /// emerging and an institution maturing there.</summary>
    private static void PlantBookEvents(WorldState prev, WorldState next, SimConfig cfg, SettlementId s, PolityId owner)
    {
        int tax = -1;
        for (int i = 0; i < next.TaxPolicies.Count; i++) if (next.TaxPolicies[i].Polity.Value == owner.Value) tax = i;
        double rate = tax >= 0 ? next.TaxPolicies[tax].Rate + 0.07 : 0.07;
        if (tax >= 0) next.TaxPolicies[tax] = new TaxPolicyRow(owner, rate); else next.TaxPolicies.Add(new TaxPolicyRow(owner, rate));
        int merchant = StateChronicle.MerchantClass(cfg);
        Assert.True(merchant >= 0);
        for (int i = 0; i < prev.Buckets.Count; i++)
            if (prev.Buckets[i].Settlement == s && prev.Buckets[i].Class.Value == merchant && prev.Buckets[i].Count.Value > 0)
                new Ledger(prev.LedgerFlows).Flow(ref prev.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment,
                    prev.Buckets[i].Count.Value, FlowDirection.Sink, OverdrawPolicy.Throw);
        bool any = false;
        for (int i = 0; i < next.Buckets.Count; i++)
            if (next.Buckets[i].Settlement == s && next.Buckets[i].Class.Value == merchant)
            {
                new Ledger(next.LedgerFlows).Flow(ref next.Buckets.Ref(i).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment,
                    3, FlowDirection.Source, OverdrawPolicy.Throw);
                any = true;
            }
        Assert.True(any, "no merchant bucket at the rig's settlement");
        int instId = 0;
        for (int i = 0; i < next.Institutions.Count; i++) instId = Math.Max(instId, next.Institutions[i].Id + 1);
        int type = cfg.Research!.UniversityTypes[0].Key;
        prev.Institutions.Add(new InstitutionRow(instId, owner, s, type, 0, 0.0));
        next.Institutions.Add(new InstitutionRow(instId, owner, s, type, 0, 1.0));
    }

    [Fact]
    public void D060_TheStateChronicle_InThePlayersView_LeavesOutARivalsBookEvents_AndTellsThePlayersOwn()
    {
        Rig r = Played.Value;
        SettlementId rival = RivalOf(r.Next), own = OwnOf(r.Next);
        List<string> Told(WorldState p, WorldState n, bool playerBooks)
        {
            var lines = new List<string>();
            new StateChronicle().Observe(p, n, r.Config, r.Name, lines, All(n), playerBooks);
            return lines;
        }
        WorldState p0 = r.Prev.Clone(), n0 = r.Next.Clone();
        WorldState p1 = r.Prev.Clone(), n1 = r.Next.Clone();
        PlantBookEvents(p1, n1, r.Config, rival, Hidden);
        Assert.Equal(Told(p0, n0, true), Told(p1, n1, true));
        // The session record's chronicle (playerBooks false) tells them — the planting is real.
        Assert.True(Told(p1, n1, false).Count >= Told(p0, n0, false).Count + 3, "fewer than three rival book events were planted");
        // POSITIVE CONTROL: the same events at the player's own settlement are told in the player's view.
        WorldState p2 = r.Prev.Clone(), n2 = r.Next.Clone();
        PlantBookEvents(p2, n2, r.Config, own, Player);
        Assert.True(Told(p2, n2, true).Count >= Told(p0, n0, true).Count + 3, "the player's own book events are not told");
    }

    // ================================================================== the real UI: FrameText with a known rival selected

    /// <summary>The real UI over <paramref name="w"/> (production knowledge), <paramref name="select"/> selected with Tab:
    /// the map (the selection card), every player section, POLICY's labour record, and the lens at Settlement zoom over the
    /// selected settlement — every string drawn, in order.</summary>
    private List<string> SelectedUi(WorldState w, SettlementId select, string dir)
    {
        UiSession s = UiSession.StartFrom(w, 42);
        using UiFrameHarness h = UiFrameHarness.Start(s, Path.Combine(_root, dir), Assets());
        h.Idle(3);
        for (int i = 0; i <= s.World.Settlements.Count && h.Ui.SelectedSettlement != select.Value; i++) h.Key(Keys.Tab);
        Assert.Equal(select.Value, h.Ui.SelectedSettlement);
        h.Idle(2);
        var all = new List<string> { "== map" };
        all.AddRange(h.Ui.FrameText.Texts());
        foreach (Section section in GameSections.PlayerOrder)
        {
            Assert.True(h.ClickControl("nav:" + section), "no nav button for " + section);
            h.Idle(3);
            all.Add("== " + section);
            all.AddRange(h.Ui.FrameText.Texts());
            if (section != Section.Policy) continue;
            // The gate's recipe: wheel the panel until the checkbox is inside it, then click it (and check it flipped).
            for (int k = 0; k < 40 && h.Ui.Controls.Find("labour-record") is { } c && c.Y1 > h.Ui.ContextRect.Y + h.Ui.ContextRect.Height - 10; k++)
                h.Wheel(h.Ui.ContextRect.X + 100, h.Ui.ContextRect.Y + 300, -2);
            Assert.True(h.ClickControl("labour-record"), "no labour record control");
            h.Idle(1);
            Assert.True(h.Ui.PolicyRecordOpen, "the labour record did not open");
            h.Idle(3);
            IReadOnlyList<string> record = h.Ui.FrameText.Texts(FrameSurface.Context);
            Assert.True(record.Any(t => t.StartsWith("CURRENT", StringComparison.Ordinal) || t == PlayerKnowledge.NotYoursLine),
                "the labour record did not open: " + string.Join(" | ", record));
            all.Add("== labour record");
            all.AddRange(record);
        }
        Focus(h, SiteOf(s.World, select), s.World.Terrain!.Size, 8.0);
        all.Add("== lens at settlement zoom");
        all.AddRange(h.Ui.FrameText.Texts(FrameSurface.Lens));
        all.AddRange(h.Ui.FrameText.Texts(FrameSurface.Selection));
        Assert.Empty(h.Problems);
        return all;
    }

    [Fact]
    public void D060_TheRealUi_WithAKnownRivalSelected_DrawsItsNameAndOwner_AndNothingOfItsBooks()
    {
        Rig r = Played.Value;
        SettlementId rival = RivalOf(r.Next), own = OwnOf(r.Next);
        List<string> a = SelectedUi(Books(rival, 0), rival, "ra"), b = SelectedUi(Books(rival, 1), rival, "rb");
        for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            Assert.True(a[i] == b[i], "line " + i + " differs: '" + a[i] + "' vs '" + b[i] + "'");
        Assert.Equal(a.Count, b.Count);
        // Public identity: the name, who holds it, the closed-books line — on the selection card and the SETTLEMENT view.
        Assert.Contains(r.Name(rival.Value), a);
        Assert.Contains("Held by the empire of 2", a);
        Assert.Contains(PlayerKnowledge.NotYoursLine, a);
        // And no figure of the rival's people is drawn at Settlement zoom (the lens's people line is absent).
        int lens = a.IndexOf("== lens at settlement zoom");
        Assert.DoesNotContain(a.Skip(lens), t => t.Contains(" people  -  ", StringComparison.Ordinal) && t.Contains(r.Name(rival.Value), StringComparison.Ordinal));

        // POSITIVE CONTROL: the player's own settlement selected, its books perturbed — the real UI draws a difference
        // (and its people line at Settlement zoom).
        List<string> oa = SelectedUi(Books(own, 0), own, "oa"), ob = SelectedUi(Books(own, 1), own, "ob");
        Assert.NotEqual(oa, ob);
        Assert.Contains(oa.Skip(oa.IndexOf("== lens at settlement zoom")), t => t.Contains(" people  -  ", StringComparison.Ordinal));
    }
}
