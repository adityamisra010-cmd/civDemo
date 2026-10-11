using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Colonization;
using Sim.Core.Worldgen;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// TEST-ONLY FIXTURE — the canonical seed-42 world (1024 px, the founding path's own worldgen config, so the deposit
/// channels are evaluated) played to turn 3, with a COLONY the player founded through OrderKind 9 at turn 2 (the
/// determinism-xproc Found Settlement leg's first literal: settlement 0 to cell 330985, re-derived Legal by the predicate
/// here). Its DepositRows were written by ColonizationSystem — the colony-founded half of the field-equals-DepositRow test.
/// </summary>
public sealed class CanonicalOverlayFixture
{
    public const int Source = 0, Site = 330985;
    public UiSession Session { get; }
    public int Colony { get; }

    public CanonicalOverlayFixture()
    {
        UiSession s = UiSession.Start(42);
        for (int t = 0; t < 3; t++)
        {
            if (t == 2)
            {
                SettlementSiting.FrontierSiting f = FoundingQuery.Frontier(s.World, s.Config, UiSession.ProductionWorldgen())!;
                Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(s.World, s.Config, f, UiPlayer.Empire, new SettlementId(Source), Site));
                s.Orders.Append(FoundingQuery.FoundOrder(s.World, UiPlayer.Empire, new SettlementId(Source), Site));
            }
            s.EndTurn();
        }
        Colony = -1;
        for (int i = 0; i < s.World.Settlements.Count; i++) if (s.World.Settlements[i].SiteCell == Site) Colony = s.World.Settlements[i].Id.Value;
        Assert.True(Colony >= 0, "the player's colony was not founded");
        Session = s;
    }
}

/// <summary>
/// M5 map workstream P9 — THE RESOURCE AND SUITABILITY OVERLAY, pinned on values, not pixels:
/// <list type="bullet">
/// <item>FIELD = DEPOSITROW: the channel field the overlay draws IS the founding's deposit function — at every founded
///   site (turn-zero and a colony) it equals the site's DepositRow bit for bit for every good with no spread, and every
///   node it draws is that function at the node's centre cell;</item>
/// <item>the suitability layers ARE the simulation's unjittered siting score and its two factors, and agree with the place
///   card (P8);</item>
/// <item>NO LAYER PER GOOD: one layer per deposit channel, every deposit good in exactly one, and the simulation itself
///   draws no difference between the spread-free goods of one channel;</item>
/// <item>NON-INTERFERENCE (the P7 stub): perturbing terrain and deposits under what the stub hides leaves the bake, the
///   status, the key and every painted command identical — and the all-known control sees the perturbation;</item>
/// <item>the bake is byte-deterministic; the paint's accounting, toggles, opacity and run coverage are exact; nothing
///   changes a hash.</item>
/// </list>
/// </summary>
public class ResourceOverlayTests(CanonicalOverlayFixture fx) : IClassFixture<CanonicalOverlayFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static WorldgenConfig Wg => UiSession.ProductionWorldgen();

    /// <summary>The 256-px dev world's founding config (the production worldgen at that size): its channels evaluate.</summary>
    private static WorldgenConfig Wg256 => UiSession.ProductionWorldgen() with { SizePx = 256 };

    private static GoodEntry GoodOf(SimConfig cfg, int id)
    {
        foreach (GoodEntry g in cfg.Goods!.Goods) if (g.Id == id) return g;
        throw new InvalidOperationException("no good " + id.ToString(CultureInfo.InvariantCulture));
    }

    private static long Bits(double v) => BitConverter.DoubleToInt64Bits(v);

    private static List<T> Rows<T>(IReadOnlyTable<T> t) where T : unmanaged
    {
        var r = new List<T>(t.Count);
        for (int i = 0; i < t.Count; i++) r.Add(t[i]);
        return r;
    }

    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    private static string Sha(string s) => Sha(Encoding.UTF8.GetBytes(s));

    private static bool[] AllOn(ResourceOverlay b)
    {
        var on = new bool[b.Layers.Count];
        Array.Fill(on, true);
        return on;
    }

    /// <summary>A whole-map lens paint (scale <paramref name="scale"/> world px → screen px, nothing culled) with the overlay.</summary>
    private static (DrawList D, LensFrame F) PaintOverlay(WorldProjection p, OverlayPaint? ov, double scale, bool legend = false)
    {
        var d = new DrawList();
        double side = p.WorldSize * scale;
        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, WorldZoom.World, (x, y) => (x * scale, y * scale), scale,
            new RectD(0, 0, side, side), legend: legend ? new LegendAnchor(side - 12, side - 12, 340) : null, overlay: ov);
        return (d, f);
    }

    // ================================================================== FIELD = DEPOSITROW

    [Fact]
    public void TheChannelField_AtEveryFoundedSite_IsThatSitesDepositRow_BitExact_ForEveryGoodWithoutSpread_TheColonyIncluded()
    {
        UiSession s = fx.Session;
        WorldState w = s.World;
        TerrainSet t = w.Terrain!;
        int exact = 0, rolled = 0, colonyRows = 0;
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow d = w.Deposits[i];
            int site = -1;
            for (int k = 0; k < w.Settlements.Count; k++) if (w.Settlements[k].Id == d.Settlement) site = w.Settlements[k].SiteCell;
            Assert.True(site >= 0);
            GoodEntry g = GoodOf(s.Config, d.Good.Value);
            double field = ResourceOverlay.ChannelAt(t, Wg, s.Config, g.DepositChannel!, site);
            if (d.Settlement.Value == fx.Colony) colonyRows++;
            if (g.DepositSpread == 0.0)
            {
                Assert.Equal(Bits(d.Abundance), Bits(field));   // the founding's own value, bit for bit
                exact++;
            }
            else
            {
                // A seeded roll keyed to the SETTLEMENT id: within the good's spread of the field, never claimed per place.
                Assert.InRange(d.Abundance, Math.Max(0.0, field * (1.0 - g.DepositSpread)), field * (1.0 + g.DepositSpread));
                rolled++;
            }
        }
        Assert.True(colonyRows > 0, "the colony has no deposit rows - its half of the test is void");
        Assert.True(exact > 50 && rolled > 10, "exact " + exact + ", rolled " + rolled);
    }

    [Fact]
    public void EveryDrawnChannelNode_IsTheFoundingsFunctionAtItsCentreCell_AndWaterAndNothingElseIsLeftOut()
    {
        UiSession s = fx.Session;
        ResourceOverlay b = s.Overlay!;
        Assert.True(b.AllKnown && b.SeesAll);
        Assert.Null(b.ChannelsUnavailable);
        TerrainSet t = b.Terrain;
        double radius = WorldFounding.HinterlandRadiusPx(Wg, s.Config);
        int land = 0;
        for (int n = 0; n < b.NodeCount; n++)
        {
            int cell = b.SampleCell(n);
            bool water = t.Water[cell] >= 0.5;
            if (!water) land++;
            (double m, double e) = WorldFounding.HinterlandMeans(t, Wg, cell, radius);
            foreach (OverlayLayer l in b.Layers)
            {
                if (l.Kind != OverlayKind.Channel) continue;
                if (water) { Assert.Equal(ResourceOverlay.NotDrawn, b.Level(l.Index, n)); continue; }
                Assert.Equal(Bits(WorldFounding.DepositChannelValue(l.Channel!, m, e)), Bits(b.Value(l.Index, n)));
                Assert.NotEqual(ResourceOverlay.NotDrawn, b.Level(l.Index, n));
            }
        }
        Assert.Equal(land, b.KnownLandNodes);
        foreach (OverlayLayer l in b.Layers) Assert.Equal(land, b.Drawn(l.Index));   // all-known: every land node, every layer
        // The sample cell is the block's centre, on the map.
        Assert.Equal(2 * t.Size + 2, ResourceOverlay.SampleCell(0, t.Size, b.LatticeSize, b.Block));
        Assert.Equal(4, b.Block);
    }

    // ================================================================== THE SCORE AND ITS FACTORS

    [Fact]
    public void TheSuitabilityLayers_AreTheSimulationsUnjitteredScoreAndItsTwoFactors_BitExact_AndThePlaceCardAgrees()
    {
        UiSession s = fx.Session;
        ResourceOverlay b = s.Overlay!;
        SiteAssessment.Prepared site = s.Sites!;
        int cutoff = Wg.Siting.WaterAccessCutoffPx;
        OverlayLayer score = b.Layers.Single(l => l.Kind == OverlayKind.Suitability);
        OverlayLayer region = b.Layers.Single(l => l.Kind == OverlayKind.RegionFertility);
        OverlayLayer access = b.Layers.Single(l => l.Kind == OverlayKind.WaterAccess);
        double scoreMax = 0;
        for (int n = 0; n < b.NodeCount; n++)
        {
            int cell = b.SampleCell(n);
            if (b.Terrain.Water[cell] >= 0.5) { Assert.True(double.IsNaN(b.Value(score.Index, n))); continue; }
            Assert.Equal(Bits(site.Frontier.Scores[cell]), Bits(b.Value(score.Index, n)));
            Assert.Equal(Bits(site.RegionFertility[cell]), Bits(b.Value(region.Index, n)));
            Assert.Equal(Bits(SettlementSiting.WaterAccess(site.WaterDistancePx[cell], cutoff)), Bits(b.Value(access.Index, n)));
            scoreMax = Math.Max(scoreMax, site.Frontier.Scores[cell]);
        }
        Assert.Equal(Bits(site.Frontier.Floor), Bits(b.Floor));
        Assert.Equal(Bits(scoreMax), Bits(b.Max(score.Index)));
        // Stronger value, higher level: the ramp is monotone and its top level is the largest value drawn.
        for (int n = 0; n < b.NodeCount; n++)
            if (b.Level(score.Index, n) != ResourceOverlay.NotDrawn)
                Assert.Equal(Math.Min(15, (int)(b.Value(score.Index, n) / scoreMax * 16)), b.Level(score.Index, n));

        // THE PLACE CARD (P8) agrees at the centre cells: the same score, factors and expected channel values.
        PolityKnowledge k = PolityKnowledge.Of(s.World, Me);
        int checkedNodes = 0;
        for (int n = 7; n < b.NodeCount && checkedNodes < 40; n += 1637)
        {
            int cell = b.SampleCell(n);
            if (b.Terrain.Water[cell] >= 0.5) continue;
            SiteAssessment.Report r = SiteAssessment.Assess(s.World, s.Config, site, k, Me, cell);
            Assert.Equal(Bits(r.Score), Bits(b.Value(score.Index, n)));
            Assert.Equal(Bits(r.RegionFertility), Bits(b.Value(region.Index, n)));
            Assert.Equal(Bits(r.WaterAccess), Bits(b.Value(access.Index, n)));
            foreach (SiteAssessment.DepositPreview d in r.Deposits)
            {
                OverlayLayer ch = b.Layers.Single(l => l.Channel == d.Channel);
                Assert.Equal(Bits(d.ChannelValue), Bits(b.Value(ch.Index, n)));
            }
            checkedNodes++;
        }
        Assert.True(checkedNodes >= 10);
    }

    // ================================================================== NO LAYER PER GOOD

    [Fact]
    public void ThereIsNoLayerPerGood_OneLayerPerChannel_EveryDepositGoodInExactlyOne_AndTheSimulationDrawsNoDifferenceWithinOne()
    {
        UiSession s = fx.Session;
        SimConfig cfg = s.Config;
        IReadOnlyList<OverlayLayer> layers = ResourceOverlay.LayersOf(cfg, Wg, 4.0);
        var channels = new List<string>();
        foreach (GoodEntry g in cfg.Goods!.Goods) if (g.DepositChannel is { } c && !channels.Contains(c)) channels.Add(c);
        Assert.Equal(channels, layers.Where(l => l.Kind == OverlayKind.Channel).Select(l => l.Channel!).ToList());
        Assert.Equal(3, channels.Count);   // nine deposit goods, three channels (goods.json today)
        Assert.Equal(channels.Count + 3, layers.Count);   // + the score and its two factors; nothing else
        foreach (GoodEntry g in cfg.Goods.Goods)
        {
            int holders = layers.Count(l => l.Goods.Any(x => x.Id == g.Id));
            Assert.Equal(g.DepositChannel is null ? 0 : 1, holders);
            Assert.DoesNotContain(layers, l => l.Key == g.Name || l.Name.Contains(g.Name, StringComparison.Ordinal));   // no layer is named for a good
        }
        // The claim behind it, measured on the world: every spread-free good of one channel has the same abundance at
        // every settlement — the simulation holds no per-good difference beyond the channel.
        WorldState w = s.World;
        foreach (OverlayLayer l in layers.Where(l => l.Kind == OverlayKind.Channel))
        {
            List<OverlayGood> flat = l.Goods.Where(g => g.Spread == 0.0).ToList();
            if (flat.Count < 2) continue;
            for (int i = 0; i < w.Settlements.Count; i++)
            {
                SettlementId id = w.Settlements[i].Id;
                var values = flat.Select(g => Bits(Rows(w.Deposits).Single(d => d.Settlement == id && d.Good.Value == g.Id).Abundance)).Distinct().ToList();
                Assert.Single(values);
            }
        }
        // The key names each channel's goods in that channel's row only, each with its spread.
        ResourceOverlay b = s.Overlay!;
        bool[] on = AllOn(b);
        IReadOnlyList<OverlayKeyRow> rows = b.KeyRows(on, s.OverlayStatus, s.Names.Name);
        foreach (OverlayLayer l in b.Layers.Where(l => l.Kind == OverlayKind.Channel))
        {
            OverlayKeyRow drives = Assert.Single(rows, r => r.Text == ResourceOverlay.DrivesText(l));
            bool spread = l.Goods.Any(g => g.Spread > 0);
            foreach (OverlayGood g in l.Goods)
            {
                Assert.Contains(g.Name, drives.Text);
                if (spread) Assert.Contains(g.Name + (g.Spread > 0 ? " (\u00B1" + (g.Spread * 100).ToString("0", CultureInfo.InvariantCulture) + "%)" : " (exact)"), drives.Text);
                foreach (OverlayLayer other in b.Layers.Where(o => o != l && o.Kind == OverlayKind.Channel))
                    Assert.DoesNotContain(g.Name, ResourceOverlay.DrivesText(other));   // a good is named in its own channel's row only
            }
            if (!spread) Assert.EndsWith(l.Goods.Count > 1 ? "all exact, one field - no difference between them" : "exact", drives.Text);
        }
        Assert.Contains(rows, r => r.Text.StartsWith(ResourceOverlay.AbundanceStatement, StringComparison.Ordinal));
    }

    // ================================================================== NON-INTERFERENCE (the P7 stub)

    /// <summary>A copy of <paramref name="t"/> with every layer changed in every cell of a node <paramref name="k"/> does not
    /// know: elevation, temperature, moisture, fertility, and rivers and water flipped on a salted pattern — or, for salt 3,
    /// all of it flooded (a sea where the undiscovered ground was).</summary>
    private static TerrainSet PerturbedUnderUnknown(TerrainSet t, PolityKnowledge k, int salt)
    {
        double[] elevation = t.Elevation.ToArray(), water = t.Water.ToArray(), temperature = t.Temperature.ToArray(),
            moisture = t.Moisture.ToArray(), fertility = t.Fertility.ToArray(), rivers = t.Rivers.ToArray();
        int changed = 0;
        for (int cell = 0; cell < fertility.Length; cell++)
        {
            if (k.KnowsCell(cell)) continue;
            changed++;
            if (salt == 3)
            {
                // A sea where the undiscovered ground was: what lies there decides routes across it.
                water[cell] = 1.0;
                rivers[cell] = 0.0;
                elevation[cell] = t.SeaLevel - 0.05;
                fertility[cell] = 0.0;
                continue;
            }
            double f = 0.1 * salt;
            fertility[cell] = Math.Min(1.0, fertility[cell] * 0.5 + f);
            moisture[cell] = Math.Min(1.0, moisture[cell] * 0.5 + f);
            temperature[cell] += 7.0 * salt;
            elevation[cell] += 0.013 * salt;
            if ((cell * 31 + salt) % 13 == 0) rivers[cell] = 1.0 - rivers[cell];
            if ((cell * 17 + salt) % 29 == 0) water[cell] = 1.0 - water[cell];
        }
        Assert.True(changed > 1000);
        var polylines = new int[t.RiverPolylineCount][];
        for (int i = 0; i < polylines.Length; i++) polylines[i] = t.RiverPolyline(i).ToArray();
        System.Reflection.ConstructorInfo ctor = typeof(TerrainSet)
            .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        return (TerrainSet)ctor.Invoke([t.Size, t.KmPerPx, t.SeaLevel, elevation, water, temperature, moisture, fertility,
            t.MovementCost.ToArray(), rivers, polylines]);
    }

    private static string StatusText(OverlayStatus st) =>
        string.Join("|", st.Reachable?.ToString(CultureInfo.InvariantCulture) ?? "-", st.InCatchments.ToString(CultureInfo.InvariantCulture),
            st.Source.ToString(CultureInfo.InvariantCulture), st.Workable?.ToString() ?? "-",
            string.Join(";", st.Rolls.Select(r => r.Settlement + "/" + r.Good + "/" + Bits(r.Abundance) + "/" + Bits(r.Expected))));

    /// <summary>Everything the overlay outputs for one world seen through <paramref name="mask"/> (null: all-known): the
    /// bake's bytes, the status, the key, and the SVG of a whole-map paint of every layer with its key and the markers.</summary>
    private static (string Bake, string Status, string Key, string Paint, ResourceOverlay B) Outputs(WorldState w, SimConfig cfg, bool[]? mask, Func<int, string> name)
    {
        PlayerKnowledge k = mask is null ? PlayerKnowledge.Of(w, Me) : KnowledgeStubs.Over(w, Me, mask);
        SiteAssessment.Prepared site = SiteAssessment.Prepare(w, cfg, Wg)!;
        ResourceOverlay b = ResourceOverlay.Bake(cfg, site, k.Core);
        OverlayStatus st = b.Status(w, cfg, k);
        bool[] on = AllOn(b);
        string key = string.Join("\n", b.KeyRows(on, st, name).Select(r => r.Text + "#" + r.Ramp.ToString(CultureInfo.InvariantCulture)));
        WorldProjection p = WorldProjection.Build(w, cfg, name, Me, knowledge: k);
        (DrawList d, LensFrame f) = PaintOverlay(p, new OverlayPaint(b, on, 0.5, st, new OverlayKeyAnchor(12, 1012, 340), name), 1.0);
        Assert.NotNull(f.Overlay);
        return (Sha(b.CanonicalBytes()), StatusText(st), key, Sha(SvgWriter.Write(d, 1024, 1024)), b);
    }

    [Fact]
    public void PerturbingTerrainAndDepositsUnderWhatTheStubHides_LeavesTheBakeTheStatusTheKeyAndEveryPaintedCommandIdentical_TheAllKnownControlSeesIt()
    {
        UiSession rival = PlayabilityGate.HiddenRival();
        WorldState w0 = rival.World;
        SimConfig cfg = rival.Config;
        Func<int, string> name = rival.Names.Name;
        // What the stub hides: the rival polity whole (its sites, catchments, formations) AND a 17 x 17-node block round
        // its first settlement, so the unknown ground is wide and the known nodes at its edge have reaches into it.
        bool[] mask = KnowledgeStubs.HiddenMask(w0, Me, PlayabilityGate.HiddenPolity);
        PolityKnowledge grid = PolityKnowledge.Of(w0, Me);
        int hiddenSite = -1;
        foreach (int id in KnowledgeStubs.SettlementsOf(w0, PlayabilityGate.HiddenPolity))
            for (int i = 0; i < w0.Settlements.Count && hiddenSite < 0; i++) if (w0.Settlements[i].Id.Value == id) hiddenSite = w0.Settlements[i].SiteCell;
        Assert.True(hiddenSite >= 0);
        int l = grid.LatticeSize, hn = grid.NodeOfCell(hiddenSite);
        for (int n = 0; n < mask.Length; n++)
            if (Math.Max(Math.Abs(n % l - hn % l), Math.Abs(n / l - hn / l)) <= 8) mask[n] = false;
        IReadOnlyList<int> hidden = KnowledgeStubs.SettlementsOf(w0, PlayabilityGate.HiddenPolity);

        var stub = new List<(string Bake, string Status, string Key, string Paint, ResourceOverlay B)>();
        var control = new List<(string Bake, string Status, string Key, string Paint, ResourceOverlay B)>();
        var statuses = new List<OverlayStatus>();
        var controlStatuses = new List<OverlayStatus>();
        foreach (int salt in new[] { 0, 1, 2, 3 })
        {
            WorldState w = w0.Clone();
            if (salt > 0)
            {
                w.Terrain = PerturbedUnderUnknown(w.Terrain!, PolityKnowledge.Restricted(w, Me, mask, mask), salt);
                int moved = 0;
                for (int i = 0; i < w.Deposits.Count; i++)
                    if (hidden.Contains(w.Deposits[i].Settlement.Value))
                    {
                        w.Deposits[i] = w.Deposits[i] with { Abundance = w.Deposits[i].Abundance * (1.5 + 0.2 * salt) + 0.07 };
                        moved++;
                    }
                Assert.True(moved > 0, "no hidden deposit row was perturbed");
            }
            stub.Add(Outputs(w, cfg, mask, name));
            control.Add(Outputs(w, cfg, null, name));
            statuses.Add(stub[^1].B.Status(w, cfg, KnowledgeStubs.Over(w, Me, mask)));
            controlStatuses.Add(control[^1].B.Status(w, cfg, PlayerKnowledge.Of(w, Me)));
        }
        // Routes run over the whole lattice: under the restricted view they are not judged, and the key says so.
        foreach (OverlayStatus st in statuses) Assert.Null(st.Reachable);
        Assert.Contains("Accessible: routes not judged (undiscovered ground)", stub[0].Key);
        // CONTROL: all-known, the flooded ground moves what is reachable — the status would leak it if it were judged.
        Assert.NotEqual(controlStatuses[0].Reachable, controlStatuses[3].Reachable);
        ResourceOverlay b0 = stub[0].B;
        Assert.False(b0.AllKnown || b0.SeesAll);
        Assert.True(b0.KnownNodes < b0.NodeCount - 200, "the stub hides too little: " + (b0.NodeCount - b0.KnownNodes));
        int withheld = 0;
        foreach (OverlayLayer layer in b0.Layers) { withheld += b0.Withheld(layer.Index); Assert.True(b0.Drawn(layer.Index) > 5000, layer.Name); }
        Assert.True(withheld > 50, "no known value has a reach into the unknown ground - the withholding is untested");
        Assert.True(double.IsNaN(b0.Floor));
        for (int i = 1; i < stub.Count; i++)
        {
            Assert.Equal(stub[0].Bake, stub[i].Bake);
            Assert.Equal(stub[0].Status, stub[i].Status);
            Assert.Equal(stub[0].Key, stub[i].Key);
            Assert.Equal(stub[0].Paint, stub[i].Paint);
        }
        // POSITIVE CONTROL: all-known, the same perturbations move the bake and the paint.
        Assert.Equal(4, control.Select(c => c.Bake).Distinct().Count());
        Assert.Equal(4, control.Select(c => c.Paint).Distinct().Count());
        // And nothing the stub's bake draws lies on an undiscovered node.
        for (int n = 0; n < b0.NodeCount; n++)
            foreach (OverlayLayer layer in b0.Layers)
                if (b0.Level(layer.Index, n) != ResourceOverlay.NotDrawn) Assert.True(mask[n]);
    }

    [Fact]
    public void UnderAStubThatHidesNothing_TheBakeIsTheAllKnownBake_AndARestrictedRegionSumAgreesWithTheSimulationsToRounding()
    {
        // A stub hiding nothing sees the whole world: the simulation's own values, bit for bit.
        UiSession s = fx.Session;
        (int l, _) = PolityKnowledge.Grid(s.World.Terrain!.Size);
        var every = new bool[l * l];
        Array.Fill(every, true);
        ResourceOverlay all = s.Overlay!;
        ResourceOverlay none = ResourceOverlay.Bake(s.Config, s.Sites!, PolityKnowledge.Restricted(s.World, Me, every, every));
        Assert.True(none.SeesAll);
        Assert.False(none.AllKnown);
        for (int i = 0; i < all.Layers.Count; i++)
            for (int n = 0; n < all.NodeCount; n++) Assert.Equal(Bits(all.Value(i, n)), Bits(none.Value(i, n)));
        // One node out of sight: the window sum stands in for the integral image — the same quantity to rounding.
        var visible = (bool[])every.Clone();
        visible[0] = false;
        ResourceOverlay oneOut = ResourceOverlay.Bake(s.Config, s.Sites!, PolityKnowledge.Restricted(s.World, Me, every, visible));
        Assert.False(oneOut.SeesAll);
        OverlayLayer region = all.Layers.Single(x => x.Kind == OverlayKind.RegionFertility);
        int compared = 0;
        for (int n = 0; n < all.NodeCount; n++)
        {
            double a = all.Value(region.Index, n), r = oneOut.Value(region.Index, n);
            if (double.IsNaN(a)) { Assert.True(double.IsNaN(r)); continue; }
            Assert.True(Math.Abs(a - r) <= 1e-9 * Math.Max(1.0, Math.Abs(a)), n + ": " + a + " vs " + r);
            compared++;
        }
        Assert.True(compared > 10000);
    }

    // ================================================================== DETERMINISM

    [Fact]
    public void TheBake_IsByteDeterministic_TwoIndependentSessionsAndPreparations_AndADifferentSeedDiffers()
    {
        static ResourceOverlay DevBake(ulong seed)
        {
            UiSession s = UiSession.Start(seed, sizeOverridePx: 256, settlementsOverride: 4);
            SiteAssessment.Prepared site = SiteAssessment.Prepare(s.World, s.Config, Wg256)!;
            return ResourceOverlay.Bake(s.Config, site, PolityKnowledge.Of(s.World, Me));
        }
        ResourceOverlay a = DevBake(42), b = DevBake(42), c = DevBake(43);
        Assert.Null(a.ChannelsUnavailable);
        Assert.Equal(Sha(a.CanonicalBytes()), Sha(b.CanonicalBytes()));
        Assert.NotEqual(Sha(a.CanonicalBytes()), Sha(c.CanonicalBytes()));
        // The canonical world: a second, independent preparation of its terrain bakes the same bytes as the session's.
        UiSession s = fx.Session;
        ResourceOverlay again = ResourceOverlay.Bake(s.Config, SiteAssessment.Prepare(s.World, s.Config, Wg)!, PolityKnowledge.Of(s.World, Me));
        Assert.Equal(Sha(s.Overlay!.CanonicalBytes()), Sha(again.CanonicalBytes()));
        // The session keeps its bake while the terrain and the knowledge masks are the same.
        Assert.Same(s.Overlay, s.Overlay);
    }

    [Fact]
    public void TheBakeIsKept_ForTheSameSiteAndKnowledgeMask_AndRemadeForAnotherMask()
    {
        UiSession s = fx.Session;
        ResourceOverlay all = s.Overlay!;
        Assert.True(all.IsFor(s.Sites!, PolityKnowledge.Of(s.World, Me)));
        (int l, _) = PolityKnowledge.Grid(s.World.Terrain!.Size);
        var mask = new bool[l * l];
        Array.Fill(mask, true);
        mask[l * l / 2] = false;
        PolityKnowledge one = PolityKnowledge.Restricted(s.World, Me, mask, mask);
        Assert.False(all.IsFor(s.Sites!, one));
        ResourceOverlay restricted = ResourceOverlay.Bake(s.Config, s.Sites!, one);
        Assert.True(restricted.IsFor(s.Sites!, PolityKnowledge.Restricted(s.World, Me, mask, mask)));
        var other = (bool[])mask.Clone();
        other[l * l / 2 + 1] = false;
        Assert.False(restricted.IsFor(s.Sites!, PolityKnowledge.Restricted(s.World, Me, other, other)));
        Assert.False(restricted.IsFor(s.Sites!, PolityKnowledge.Of(s.World, Me)));
        // Another preparation of the same terrain is another site half: the bake is not claimed for it.
        Assert.False(all.IsFor(SiteAssessment.Prepare(s.World, s.Config, Wg)!, PolityKnowledge.Of(s.World, Me)));
        Assert.Equal(all.NodeCount - 1, restricted.KnownNodes);
    }

    // ================================================================== THE PAINT

    private sealed class DevWorld
    {
        public readonly UiSession S = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        public readonly ResourceOverlay B;
        public readonly OverlayStatus St;
        public readonly WorldProjection P;

        public DevWorld()
        {
            B = ResourceOverlay.Bake(S.Config, SiteAssessment.Prepare(S.World, S.Config, Wg256)!, PolityKnowledge.Of(S.World, Me));
            St = B.Status(S.World, S.Config, S.Knowledge);
            P = WorldProjection.Build(S.World, S.Config, S.Names.Name, Me);
        }
    }

    private static readonly Lazy<DevWorld> Dev = new(() => new DevWorld());

    [Fact]
    public void ThePaint_ChargesEveryCommandToTheOverlayLayer_DrawsOnlyTheShownLayers_AndItsRunsCoverExactlyTheDrawnNodes()
    {
        DevWorld dw = Dev.Value;
        ResourceOverlay b = dw.B;
        const double scale = 4.0;
        (DrawList d0, LensFrame f0) = PaintOverlay(dw.P, null, scale, legend: true);
        Assert.Null(f0.Overlay);
        Assert.Equal(0, f0.LayerDraws[(int)MapLayer.ResourceOverlay]);
        for (int i = 0; i < b.Layers.Count; i++)
        {
            var on = new bool[b.Layers.Count];
            on[i] = true;
            (DrawList d, LensFrame f) = PaintOverlay(dw.P, new OverlayPaint(b, on, 0.5, dw.St, new OverlayKeyAnchor(12, 1012, 340), dw.S.Names.Name), scale, legend: true);
            OverlayFrame o = f.Overlay!;
            Assert.Equal([i], o.Layers);
            for (int j = 0; j < b.Layers.Count; j++) Assert.Equal(j == i ? b.Drawn(i) : 0, o.Nodes[j]);   // the whole map in view
            Assert.True(o.Rects[i] > 0 && o.Rects[i] <= b.Drawn(i));
            // Every command charged once; the overlay's are its rects, its markers and its key; every other layer draws
            // exactly what it drew without the overlay, and the legend is still the last thing drawn.
            Assert.Equal(d.Commands.Count, f.LayerDraws.Sum());
            foreach (MapLayer m in MapLayerOwnership.All)
                if (m != MapLayer.ResourceOverlay) Assert.Equal(f0.LayerDraws[(int)m], f.LayerDraws[(int)m]);
            int legend = f.LayerDraws[(int)MapLayer.Legend];
            Assert.Equal(SvgOf(d0.Commands.Skip(d0.Commands.Count - legend)), SvgOf(d.Commands.Skip(d.Commands.Count - legend)));
            Assert.Empty(o.Markers);   // World zoom: the settlements stand too close for a roll line each
            // Closer in, every settlement of the player's with a roll on a shown channel carries its line (and no other).
            var dr = new DrawList();
            LensFrame fr = WorldLens.Paint(dr, ApproxTextMeasure.Instance, dw.P, WorldZoom.Regional, (x, y) => (x * 12, y * 12), 12,
                new RectD(-1e6, -1e6, 2e6, 2e6), overlay: new OverlayPaint(b, on, 0.5, dw.St));
            var wantMarkers = dw.P.Settlements.Where(s => b.MarkerText(on, dw.St, s.Id).Length > 0).Select(s => (s.Id, b.MarkerText(on, dw.St, s.Id))).ToList();
            Assert.Equal(wantMarkers, fr.Overlay!.Markers);
            Assert.Equal(b.Layers[i].Kind == OverlayKind.Channel && b.Layers[i].Goods.Any(g => g.Spread > 0), wantMarkers.Count > 0);
            foreach ((int id, string text) in wantMarkers)
                Assert.Single(dr.Commands.OfType<TextCmd>(), t => t.Text == text);

            // The runs (the first commands) tile exactly the drawn nodes of the layer: each node once, no other.
            var covered = new int[b.NodeCount];
            for (int c = 0; c < o.Rects[i]; c++)
            {
                RectCmd r = Assert.IsType<RectCmd>(d.Commands[c]);
                int bs = b.Block;
                int ny = (int)Math.Round(r.Rect.Y / scale / bs), nx0 = (int)Math.Round(r.Rect.X / scale / bs), nx1 = (int)Math.Round(r.Rect.Right / scale / bs);
                Assert.Equal(r.Rect.Y + r.Rect.H, (ny + 1) * bs * scale);
                for (int nx = nx0; nx < nx1; nx++)
                {
                    int node = ny * b.LatticeSize + nx;
                    covered[node]++;
                    Assert.Equal(ResourceOverlay.Fill(MapInk.Default, b.Layers[i], b.Level(i, node), 0.5), r.Fill);
                }
            }
            for (int n = 0; n < b.NodeCount; n++) Assert.Equal(b.Level(i, n) == ResourceOverlay.NotDrawn ? 0 : 1, covered[n]);
        }
    }

    private static string SvgOf(IEnumerable<DrawCmd> cmds)
    {
        var d = new DrawList();
        foreach (DrawCmd c in cmds) d.Add(c);
        return SvgWriter.Write(d, 1024, 1024);
    }

    [Fact]
    public void Opacity_ScalesOnlyTheOverlaysAlpha_StrongerValueIsDeeperAndMoreOpaque_AndTheTerrainAlwaysShowsThrough()
    {
        DevWorld dw = Dev.Value;
        ResourceOverlay b = dw.B;
        OverlayLayer score = b.Layers.Single(l => l.Kind == OverlayKind.Suitability);
        var on = new bool[b.Layers.Count];
        on[score.Index] = true;
        (DrawList lo, LensFrame flo) = PaintOverlay(dw.P, new OverlayPaint(b, on, 0.3, dw.St), 4.0);
        (DrawList hi, LensFrame fhi) = PaintOverlay(dw.P, new OverlayPaint(b, on, 0.8, dw.St), 4.0);
        Assert.Equal(flo.Overlay!.Rects, fhi.Overlay!.Rects);
        Assert.Equal(lo.Commands.Count, hi.Commands.Count);
        int top = 0;
        for (int c = 0; c < flo.Overlay.Rects[score.Index]; c++)
        {
            RectCmd a = (RectCmd)lo.Commands[c], z = (RectCmd)hi.Commands[c];
            Assert.Equal(a.Rect, z.Rect);   // geometry unchanged
            Assert.Equal((a.Fill!.Value.R, a.Fill.Value.G, a.Fill.Value.B), (z.Fill!.Value.R, z.Fill.Value.G, z.Fill.Value.B));
            Assert.True(z.Fill.Value.A > a.Fill.Value.A);
            Assert.True(z.Fill.Value.A <= (byte)Math.Round(255 * 0.8));   // never opaque: the terrain shows through
            if (z.Fill.Value.A == (byte)Math.Round(255 * 0.8)) top++;
        }
        Assert.True(top > 0, "the deepest level is drawn at the opacity's alpha");
        // Every command after the overlay's own is the same at both opacities.
        for (int c = flo.Overlay.Rects[score.Index]; c < lo.Commands.Count; c++) Assert.Equal(SvgOf([lo.Commands[c]]), SvgOf([hi.Commands[c]]));
        // The ramp: each level deeper (further from the pale end) and more opaque than the one below.
        for (int v = 1; v < ResourceOverlay.LevelCount; v++)
        {
            var a = ResourceOverlay.Fill(MapInk.Default, score, v - 1, 0.5);
            var z = ResourceOverlay.Fill(MapInk.Default, score, v, 0.5);
            Assert.True(z.A > a.A);
            int da = Dist(a, MapInk.Default.OverlayLow), dz = Dist(z, MapInk.Default.OverlayLow);
            Assert.True(dz >= da);
        }
    }

    private static int Dist(Sim.Ui.Art.ParchmentPalette.Rgba a, Sim.Ui.Art.ParchmentPalette.Rgba b) =>
        Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    // ================================================================== THE STATUS (P8's predicates)

    [Fact]
    public void TheKeysAccessAndExtractionRows_AreThePlaceCardsOwnPredicates_AndTheMarkersAreTheDepositRows()
    {
        UiSession s = fx.Session;
        ResourceOverlay b = s.Overlay!;
        OverlayStatus st = s.OverlayStatus!;
        Assert.NotNull(st.Reachable);
        SiteAssessment.Prepared site = s.Sites!;
        PolityKnowledge k = PolityKnowledge.Of(s.World, Me);
        // ACCESSIBLE: the union of unbounded isochrones is the card's "reachable" (FindPath found) at every node sampled.
        var reach = new bool[site.Frontier.Lattice.NodeCount];
        foreach (SettlementId mine in LabourActivities.ControlledSettlements(s.World, Me))
        {
            int cell = Rows(s.World.Settlements).Single(r => r.Id == mine).SiteCell;
            foreach (int n in Sim.Core.Pathing.Pathfinder.Isochrone(site.Frontier.Lattice, s.World,
                Sim.Core.Pathing.LatticeMap.OriginLatticeNode(site.Frontier.Lattice, b.Size, cell), double.PositiveInfinity).Reached) reach[n] = true;
        }
        int sampled = 0, yes = 0;
        for (int n = 3; n < b.NodeCount; n += 997)
        {
            int cell = b.SampleCell(n);
            if (b.Terrain.Water[cell] >= 0.5) continue;
            SiteAssessment.Report r = SiteAssessment.Assess(s.World, s.Config, site, k, Me, cell);
            bool card = r.AccessibleFrom >= 0;
            Assert.Equal(card, reach[Sim.Core.Pathing.LatticeMap.OriginLatticeNode(site.Frontier.Lattice, b.Size, cell)]);
            if (card) yes++;
            sampled++;
            // EXTRACTABLE: the card's "workable" for the same (default) source.
            Assert.Equal(r.ColonyWorkable, st.Workable == true);
        }
        Assert.True(sampled > 20 && yes > 0 && yes < sampled, sampled + " sampled, " + yes + " reachable");
        Assert.Equal(SiteAssessment.DefaultSource(s.World, Me), st.Source);
        Assert.InRange(st.Reachable!.Value, 1, b.KnownLandNodes - 1);
        Assert.True(st.InCatchments > 0);

        // The realized rolls: exactly the DepositRows of the player's settlements for the goods with a spread.
        var want = new List<string>();
        for (int i = 0; i < s.World.Deposits.Count; i++)
        {
            DepositRow d = s.World.Deposits[i];
            if (!EmpireQuery.ControlsSettlement(s.World, Me, d.Settlement) || !(GoodOf(s.Config, d.Good.Value).DepositSpread > 0)) continue;
            want.Add(d.Settlement.Value + "/" + d.Good.Value + "/" + Bits(d.Abundance));
        }
        Assert.NotEmpty(want);
        Assert.Equal(want.Order(StringComparer.Ordinal), st.Rolls.Select(r => r.Settlement + "/" + r.Good + "/" + Bits(r.Abundance)).Order(StringComparer.Ordinal));
        foreach (OreRoll r in st.Rolls)
            Assert.Equal(Bits(ResourceOverlay.ChannelAt(b.Terrain, Wg, s.Config, r.Channel, Rows(s.World.Settlements).Single(x => x.Id.Value == r.Settlement).SiteCell)), Bits(r.Expected));
        // The four rows are in the key, each from its predicate.
        IReadOnlyList<OverlayKeyRow> rows = b.KeyRows(AllOn(b), st, s.Names.Name);
        Assert.Contains(rows, r => r.Text == "Discovered: every place known (no extent is stored yet)");
        Assert.Contains(rows, r => r.Text == "Present: every good above 0 at all " + b.JudgedForPresence.ToString("#,0", CultureInfo.InvariantCulture)
            + " judged places (no scarcity until D-051 s.2)");
        Assert.Contains(rows, r => r.Text == "Accessible: " + st.Reachable.Value.ToString("#,0", CultureInfo.InvariantCulture) + " of "
            + b.KnownLandNodes.ToString("#,0", CultureInfo.InvariantCulture) + " by land from your settlements; "
            + st.InCatchments.ToString("#,0", CultureInfo.InvariantCulture) + " in your catchments");
        Assert.Contains(rows, r => r.Text == "Extractable: herding works livestock, fish, extraction the rest - no research gate; a colony from "
            + s.Names.Name(st.Source) + (st.Workable == true ? " would be yours" : " would not be yours"));
        Assert.Equal(b.JudgedForPresence, b.PresentNodes);   // presence is universal until D-051 section 2
    }

    // ================================================================== NOTHING CHANGES A HASH

    [Fact]
    public void BakingStatusAndPainting_ChangeNoHash()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        string before = WorldHash.ComputeHex(s.World);
        ResourceOverlay b = s.Overlay!;
        OverlayStatus st = s.OverlayStatus!;
        PaintOverlay(WorldProjection.Build(s.World, s.Config, s.Names.Name, Me), new OverlayPaint(b, AllOn(b), 0.8, st, new OverlayKeyAnchor(12, 1012, 340), s.Names.Name), 4.0);
        Assert.Equal(before, WorldHash.ComputeHex(s.World));
        Assert.Empty(s.QueuedOrders());
        // The dev world's founding config is the production one at 1024 px: its channels are not evaluated, and say so.
        Assert.Contains("1024-px map", b.ChannelsUnavailable, StringComparison.Ordinal);
        Assert.Contains(b.KeyRows(AllOn(b), st, s.Names.Name), r => r.Text.StartsWith("Not evaluated: the founding path's worldgen config", StringComparison.Ordinal));
        foreach (OverlayLayer l in b.Layers) if (l.Kind == OverlayKind.Channel) Assert.Equal(0, b.Drawn(l.Index));
    }

    // ================================================================== m5w integration: the markers beside P12a's footprint

    /// <summary>
    /// M5 workstream integration (P9 x P12a): a realized-roll marker stands WEST of its settlement's footprint (P9: the name
    /// below, the banner above, the formations east) — the footprint as P12a sizes it (the population class scale,
    /// <see cref="WorldLens.FootprintRadiusPx"/>), which the merge made the marker read when P12a replaced the old footprint
    /// function. At the Regional and Settlement plateaus and between them, every marker's plate lies wholly left of its
    /// settlement's hit footprint (the footprint as painted, <see cref="SettlementHit.Footprint"/>), close to it, and level
    /// with the settlement's centre. Teeth: at the Settlement plateau the footprint is wider than the World mark by more than
    /// the marker's gap, so a marker placed off the mark (not the footprint) would overlap it.
    /// </summary>
    [Fact]
    public void TheRealizedRollMarkers_StandWestOfTheFootprintAsP12aSizesIt_NeverOverIt()
    {
        UiSession s = fx.Session;
        ResourceOverlay b = s.Overlay!;
        OverlayStatus st = s.OverlayStatus!;
        WorldProjection p = WorldProjection.Build(s.World, s.Config, s.Names.Name, Me, knowledge: s.Knowledge);
        var paint = new OverlayPaint(b, AllOn(b), 0.5, st, null, s.Names.Name);
        int checkedMarkers = 0, teeth = 0;
        foreach (SettlementLensView sv in p.Settlements)
        {
            if (!sv.IsPlayer || b.MarkerText(paint.Shown, st, sv.Id).Length == 0) continue;
            foreach ((MapDetail detail, double scale) in new[] { (MapDetail.Of(WorldZoom.Regional), 2.0), (MapDetail.At(5.0, 1280, 800, p.KmPerPx), 5.0),
                         (MapDetail.Of(WorldZoom.Settlement), 8.0), (MapDetail.Of(WorldZoom.Settlement), 14.0) })
            {
                var d = new DrawList();
                LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, detail,
                    (x, y) => ((x - sv.X) * scale + 640, (y - sv.Y) * scale + 400), scale, new RectD(0, 0, 1280, 800), overlay: paint);
                (int id, string text) = f.Overlay!.Markers.Single(m => m.Settlement == sv.Id);
                SettlementHit hit = f.SettlementHits.Single(h => h.Id == sv.Id);
                int at = -1;
                for (int i = 0; i < d.Commands.Count; i++) if (d.Commands[i] is TextCmd t && t.Text == text) at = i;
                Assert.True(at > 0 && d.Commands[at - 1] is RectCmd, "no plate before the marker's text");
                RectD plate = ((RectCmd)d.Commands[at - 1]).Rect;
                Assert.True(plate.Right <= hit.Footprint.X, "the marker of " + sv.Name + " at scale " + scale.ToString(CultureInfo.InvariantCulture)
                    + " overlaps its footprint: plate right " + plate.Right.ToString("R", CultureInfo.InvariantCulture)
                    + ", footprint left " + hit.Footprint.X.ToString("R", CultureInfo.InvariantCulture));
                Assert.InRange(400.0, plate.Y, plate.Bottom);
                double gap = hit.Footprint.X - plate.Right;
                Assert.True(gap < 12, "the marker floats " + gap.ToString("R", CultureInfo.InvariantCulture) + " px off its footprint");
                if (detail.Settlement == 1.0 && WorldLens.FootprintRadiusPx(p, sv, scale, 1.0) > WorldLens.MarkRadiusPx(p, sv) + 12) teeth++;
                checkedMarkers++;
            }
        }
        Assert.True(checkedMarkers >= 4, "too few markers checked (" + checkedMarkers + ")");
        Assert.True(teeth > 0, "no footprint is wider than its mark plus the gap - the placement is untested");
    }
}

/// <summary>P9 through the real per-frame UI: the overlay controls, the gate's overlay rows and hash invariance across an
/// End Turn with every layer shown.</summary>
[Collection("ImGui context")]
public class ResourceOverlayUiTests : IDisposable
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-p9-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    private string WorkDir(string name) => Path.Combine(_root, name);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheGatesOverlayRows_PassOnTheCanonicalWorld_AndPressEveryOverlayControl()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("rows"), Assets());
        IReadOnlyList<GateRow> rows = PlayabilityGate.OverlayRows(h, "test");
        foreach (GateRow r in rows) Assert.True(r.Result == GateResult.Pass, r.Control + ": " + r.Detail);
        var pressed = new SortedSet<string>(rows.SelectMany(r => r.Covers), StringComparer.Ordinal);
        Assert.Contains(UiCoverage.ControlKey(GameUi.OverlaysControl), pressed);
        Assert.Contains(UiCoverage.ControlKey(GameUi.OverlayOpacityDown), pressed);
        Assert.Contains(UiCoverage.ControlKey(GameUi.OverlayOpacityUp), pressed);
        foreach (OverlayLayer l in h.Ui.OverlayLayers) Assert.Contains(UiCoverage.ControlKey(l.Control), pressed);
        Assert.Equal(2 + 2 * h.Ui.OverlayLayers.Count + 2, rows.Count);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void InTheHiddenRivalRig_TheOverlayRowsPass_IncludingNothingDrawnOnUndiscoveredGround()
    {
        using UiFrameHarness h = UiFrameHarness.Start(PlayabilityGate.HiddenRival(), WorkDir("hidden"), Assets());
        IReadOnlyList<GateRow> rows = PlayabilityGate.OverlayRows(h, "hidden rival");
        foreach (GateRow r in rows) Assert.True(r.Result == GateResult.Pass, r.Control + ": " + r.Detail);
        Assert.Contains(rows, r => r.Control == "the overlay draws nothing on undiscovered ground");
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void ShowingEveryLayerAndPlaying_IsHashIdenticalToNotShowingThem()
    {
        UiSession plain = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4), WorkDir("hash"), Assets());
        Assert.True(h.ClickControl(GameUi.OverlaysControl));
        foreach (OverlayLayer l in h.Ui.OverlayLayers) Assert.True(h.ClickControl(l.Control));
        Assert.True(h.ClickControl(GameUi.OverlayOpacityUp));
        h.Idle(2);
        Assert.NotNull(h.Ui.LensFrame!.Overlay);
        foreach (OverlayLayer l in h.Ui.OverlayLayers) Assert.True(h.Ui.OverlayShown(l.Index));
        Assert.Empty(h.Ui.Session.QueuedOrders());
        Assert.Equal(WorldHash.ComputeHex(plain.World), WorldHash.ComputeHex(h.Ui.Session.World));
        plain.EndTurn();
        h.Ui.Session.EndTurn();
        h.Idle(2);
        Assert.NotNull(h.Ui.LensFrame!.Overlay);
        Assert.Equal(WorldHash.ComputeHex(plain.World), WorldHash.ComputeHex(h.Ui.Session.World));
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void WithALayerShown_ShiftClickOnKnownGround_OpensP8sPlaceCard_AndTheKeyOffersNoPlaceBeneathIt()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        using UiFrameHarness h = UiFrameHarness.Start(s, WorkDir("place"), Assets());
        Assert.True(h.ClickControl(GameUi.OverlaysControl));
        OverlayLayer score = h.Ui.OverlayLayers.Single(l => l.Kind == OverlayKind.Suitability);
        Assert.True(h.ClickControl(score.Control));
        Assert.True(h.ClickControl(GameUi.OverlaysControl));   // fold the controls: the layer stays shown
        Assert.True(EmpireQuery.TryGetCapital(s.World, UiPlayer.Empire, out SettlementId cap));
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, cap.Value, h.Ui.MapRect);
        h.Idle(2);
        OverlayKey key = h.Ui.LensFrame!.Overlay!.Key!;
        Assert.True(h.Ui.OverlayShown(score.Index));

        // Known land off the key, near the capital: the ground is P8's passive place region, and Shift+click opens its card.
        Sim.Core.Worldgen.TerrainSet t = h.Ui.World.Terrain!;
        RectD map = h.Ui.MapRect;
        (double X, double Y, int Cell)? at = null;
        for (int ring = 40; ring <= 200 && at is null; ring += 40)
            for (int a = 0; a < 8 && at is null; a++)
            {
                double x = Math.Round(map.CenterX + ring * Math.Cos(a * Math.PI / 4)), y = Math.Round(map.CenterY + ring * Math.Sin(a * Math.PI / 4));
                int cell = MapCells.At(h.Ui.Camera, map, x, y, t.Size);
                if (cell < 0 || t.Water[cell] >= 0.5 || key.Rect.Contains(x, y)) continue;
                h.MoveTo(x, y);
                if (h.Ui.InfoRegistry.HitTest(x, y) is { Subject.Kind: Sim.Core.State.InfoKind.Cell }) at = (x, y, cell);
            }
        Assert.NotNull(at);
        string hash = WorldHash.ComputeHex(s.World);
        h.ShiftClick(at.Value.X, at.Value.Y);
        Assert.True(h.Ui.Inspector.IsOpen);
        Assert.Equal(Sim.Core.State.InfoKind.Cell, h.Ui.Inspector.Subject!.Value.Kind);
        Assert.Equal(at.Value.Cell, h.Ui.Inspector.Subject!.Value.Id);
        Assert.Equal(hash, WorldHash.ComputeHex(s.World));
        Assert.Empty(s.QueuedOrders());
        h.Key(Microsoft.Xna.Framework.Input.Keys.Escape);
        Assert.False(h.Ui.Inspector.IsOpen);

        // The key lies over the map: no place is offered beneath it, and a click on it selects nothing.
        h.MoveTo(key.Rect.CenterX, key.Rect.CenterY);
        Assert.Null(h.Ui.InfoRegistry.HitTest(key.Rect.CenterX, key.Rect.CenterY));
        int selected = h.Ui.SelectedSettlement;
        h.Click(key.Rect.CenterX, key.Rect.CenterY);
        Assert.Equal(selected, h.Ui.SelectedSettlement);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void TheOpacity_StepsInTenths_From10To80Percent_NeverOpaque_SoTheTerrainAlwaysShowsThrough()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4), WorkDir("opacity"), Assets());
        Assert.Equal(0.5, h.Ui.OverlayOpacity);
        Assert.True(h.ClickControl(GameUi.OverlaysControl));
        OverlayLayer score = h.Ui.OverlayLayers.Single(l => l.Kind == OverlayKind.Suitability);
        Assert.True(h.ClickControl(score.Control));
        for (int i = 0; i < 12; i++) h.ClickControl(GameUi.OverlayOpacityUp);
        h.Idle(1);
        Assert.Equal(0.8, h.Ui.OverlayOpacity, 12);
        int deepest = 0;
        OverlayFrame f = h.Ui.LensFrame!.Overlay!;
        for (int i = 0; i < f.Rects[score.Index]; i++)
            if (h.Ui.LastLensCommands[i] is RectCmd { Fill: { } fill }) deepest = Math.Max(deepest, fill.A);
        Assert.Equal((int)Math.Round(255 * 0.8), deepest);   // the deepest level at the top opacity: never opaque
        for (int i = 0; i < 12; i++) h.ClickControl(GameUi.OverlayOpacityDown);
        h.Idle(1);
        Assert.Equal(0.1, h.Ui.OverlayOpacity, 12);
        Assert.Empty(h.Problems);
    }
}
