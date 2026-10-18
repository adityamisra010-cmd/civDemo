using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// P12a (M5 map workstream, part 8 within D-043 D1–D3 and D-038 H2) — STABLE PLACEMENT AND SIZE LEGIBILITY FOR THE EXISTING
/// SETTLEMENT GLYPHS. A settlement's composition (<see cref="SettlementComposer"/>) is a pure function of its projected
/// state and the content: a later arrival never moves an earlier part; a change of count, zoom or pan moves nothing
/// already placed; any table input order gives the same layout; the marks grow strictly with population up to a stated
/// saturation (the compressive class scale of <c>settlement-classes.json</c>); dwellings densify on extra lattice rings
/// beyond the old cap of 48; structures and institutions appear only once BUILT (founded) and persist; the parts' hit
/// rects follow where they were drawn. Institutions stay aggregated per type (D-043 D2).
/// </summary>
public class SettlementCompositionTests(InstitutionWorldFixture fx) : IClassFixture<InstitutionWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    /// <summary>The measured range of a settlement's population (P12a measurement: order-free canonical worlds, seeds 42 and
    /// 7, 1,500 turns, every 100 turns): 219 (seed 7, turn 0) to 61,078 (seed 7, turn 1,500).</summary>
    private const long MeasuredMin = 219, MeasuredMax = 61_078;

    private WorldProjection Seam() =>
        WorldProjection.Build(fx.Session.World, fx.Session.Config, id => fx.Session.Names.Name(id), Me, fx.SeamUniversities);

    private static WorldProjection Only(WorldProjection p, params SettlementLensView[] settlements) => new()
    {
        WorldSize = p.WorldSize, KmPerPx = p.KmPerPx, Settlements = settlements, Units = [], Roads = [], Routes = [],
        Territory = [], PlayerPolity = p.PlayerPolity, PlayerAge = p.PlayerAge, PlayerAgeName = p.PlayerAgeName,
        Absent = p.Absent, Universe = p.Universe, Classes = p.Classes,
    };

    private static Dictionary<string, CompositionLot> LotsByKey(SettlementComposition c)
    {
        var r = new Dictionary<string, CompositionLot>(StringComparer.Ordinal);
        foreach (PlacedPart part in c.Parts) r[part.Key] = part.Lot;
        return r;
    }

    // ------------------------------------------------------------------ the hash

    /// <summary>The hash is pinned by vectors computed by an INDEPENDENT implementation (Python, from the FNV-1a and
    /// MurmurHash3 fmix64 definitions): the published FNV-1a 64 vectors, two fmix64 values, and two composed keys — one of
    /// them the b1z2y4 branch's own pin, which the independent implementation reproduces. A process or platform that hashed
    /// differently would place every part differently, and fails here.</summary>
    [Fact]
    public void StableHash_IsPinnedByIndependentVectors()
    {
        Assert.Equal(0xcbf29ce484222325UL, StableHash.Fnv1a64([]));
        Assert.Equal(0xaf63dc4c8601ec8cUL, StableHash.Fnv1a64(Encoding.UTF8.GetBytes("a")));
        Assert.Equal(0x85944171f73967e8UL, StableHash.Fnv1a64(Encoding.UTF8.GetBytes("foobar")));
        Assert.Equal(0UL, StableHash.Fmix64(0));
        Assert.Equal(12994781566227106604UL, StableHash.Fmix64(1));
        Assert.Equal(9785191686031420650UL, StableHash.Fmix64(0x0123456789abcdefUL));
        Assert.Equal(15447159657008429857UL, StableHash.Of("s-veyra", "u-123"));
        Assert.Equal(18137235018649413122UL, StableHash.Of("settlement:0", "structure:1"));
        Assert.NotEqual(StableHash.Of("ab", "c"), StableHash.Of("a", "bc"));   // the 0x00 separator
        Assert.Equal(2, StableHash.Index(18137235018649413122UL, 5));   // unsigned modulo: ...122 mod 5
    }

    // ------------------------------------------------------------------ size: population-primary, compressive, monotone

    /// <summary>
    /// THE MONOTONE LEGIBILITY PIN (acceptance 3). Every integer population from 0 to the stated saturation (4,096,000 — the
    /// last class threshold, 67× the measured maximum) draws a STRICTLY larger World mark and footprint than the one below
    /// it, and the size index and class never fall; at and above the saturation both are constant. The measured range
    /// (219–61,078) lies wholly below the saturation. Before P12a the World mark saturated at 23,716 people (min(5 +
    /// √p/14, 16)), the footprint at 150 px from about 3,500 people at maximum zoom, and dwellings at 48 blocks.
    /// </summary>
    [Fact]
    public void MarkSize_IsStrictlyMonotoneInPopulation_WithNoSaturationBelowTheStatedPopulation()
    {
        SettlementClasses c = SettlementClasses.Default;
        long sat = c.SaturationPopulation;
        Assert.Equal(4_096_000L, sat);
        Assert.True(MeasuredMin > 0 && MeasuredMax < sat, "the measured range must lie below the stated saturation");
        double mark = c.WorldMarkRadiusPx(0), foot = c.FootprintRadiusUnits(0), index = c.SizeIndex(0);
        int cls = c.ClassOf(0);
        for (long p = 1; p <= sat; p++)
        {
            double m = c.WorldMarkRadiusPx(p), f = c.FootprintRadiusUnits(p), i = c.SizeIndex(p);
            int k = c.ClassOf(p);
            if (!(m > mark) || !(f > foot) || !(i > index) || k < cls)
                Assert.Fail($"not strictly increasing at {p}: mark {mark} -> {m}, footprint {foot} -> {f}, index {index} -> {i}, class {cls} -> {k}");
            mark = m; foot = f; index = i; cls = k;
        }
        // Saturated: constant at and above the stated population.
        foreach (long p in new[] { sat, sat + 1, sat * 10, long.MaxValue / 4 })
        {
            Assert.Equal(c.Classes[^1].WorldMarkRadiusPx, c.WorldMarkRadiusPx(p));
            Assert.Equal(c.Classes[^1].FootprintRadiusUnits, c.FootprintRadiusUnits(p));
            Assert.Equal(c.Classes.Count - 1, c.ClassOf(p));
        }
        // At a threshold the index is the class exactly (the step a class spans is one visual step).
        for (int k = 0; k < c.Classes.Count; k++)
        {
            Assert.Equal(k, c.ClassOf(c.Classes[k].MinPopulation));
            Assert.Equal((double)k, c.SizeIndex(c.Classes[k].MinPopulation));
            if (k > 0) Assert.Equal(k - 1, c.ClassOf(c.Classes[k].MinPopulation - 1));
        }
    }

    /// <summary>The same, AS DRAWN: one settlement painted at a ladder of populations spanning the measured range and beyond,
    /// at the World, Regional and Settlement plateaus and at the maximum zoom — the drawn mark (the halo at World zoom, the
    /// footprint closer; P3's hit footprint) strictly grows at every rung, including above the old saturations.</summary>
    [Fact]
    public void DrawnMarkSize_GrowsWithEveryRungOfPopulation_AtEveryBandAndZoom()
    {
        WorldProjection p = Seam();
        SettlementLensView cap = p.Settlements.Single(s => s.Id == fx.Capital);
        long[] ladder = [0, MeasuredMin, 250, 1_000, 3_500, 4_903, 10_000, 23_716, 30_000, MeasuredMax, 250_000, 1_000_000, 4_096_000];
        foreach ((WorldZoom zoom, double scale) in new[] { (WorldZoom.World, 1.0), (WorldZoom.Regional, 3.0), (WorldZoom.Settlement, 8.0), (WorldZoom.Settlement, 32.0) })
        {
            double last = 0;
            foreach (long pop in ladder)
            {
                SettlementLensView v = cap with { Population = pop };
                LensFrame f = WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, Only(p, v), zoom,
                    (x, y) => ((x - v.X) * scale + 2000, (y - v.Y) * scale + 2000), scale, new RectD(0, 0, 4000, 4000));
                double r = f.SettlementHits.Single().Footprint.W / 2;
                Assert.True(r > last, $"{zoom} x{scale}: population {pop} draws {r} px, not more than {last}");
                last = r;
            }
        }
    }

    /// <summary>The thresholds are DATA: the table parses from JSON, a changed threshold changes the class, and a malformed
    /// table is refused with the field named (never a silent default) — including one that would let a glyph cross the
    /// smallest footprint or cap the blocks below the saturation.</summary>
    [Fact]
    public void Classes_AreDataDefined_AndAMalformedTableIsRefused()
    {
        string json;
        using (Stream s = typeof(SettlementClasses).Assembly.GetManifestResourceStream(SettlementClasses.ResourceName)!)
        using (var r = new StreamReader(s)) json = r.ReadToEnd();
        SettlementClasses shipped = SettlementClasses.Parse(json);
        Assert.Equal(SettlementClasses.Default.Classes, shipped.Classes);
        Assert.Equal(2, shipped.ClassOf(1_000));
        SettlementClasses moved = SettlementClasses.Parse(json.Replace("\"minPopulation\": 1000,", "\"minPopulation\": 2000,", StringComparison.Ordinal));
        Assert.Equal(1, moved.ClassOf(1_000));
        Assert.Equal(2, moved.ClassOf(2_000));

        void Refused(string from, string to, string field)
        {
            Assert.Contains(from, json, StringComparison.Ordinal);
            var e = Assert.Throws<InvalidDataException>(() => SettlementClasses.Parse(json.Replace(from, to, StringComparison.Ordinal)));
            Assert.Contains(field, e.Message, StringComparison.Ordinal);
        }
        Refused("\"minPopulation\": 0,", "\"minPopulation\": 5,", "sizeClasses[0].minPopulation");
        Refused("\"minPopulation\": 4000,", "\"minPopulation\": 900,", "sizeClasses[3].minPopulation");
        Refused("\"worldMarkRadiusPx\": 8.5,", "\"worldMarkRadiusPx\": 6.0,", "sizeClasses[3].worldMarkRadiusPx");
        Refused("\"footprintRadiusUnits\": 7.75", "\"footprintRadiusUnits\": 6.75", "sizeClasses[3].footprintRadiusUnits");
        Refused("{ \"radius\": 3.6, \"lots\": 9 }", "{ \"radius\": 3.7, \"lots\": 9 }", "SMALLEST footprint");
        Refused("\"blockRings\": 16,", "\"blockRings\": 15,", "lattice.blockRings");
        Refused("\"dwellingsPerBlock\": 25", "\"dwellingsPerBlock\": 0", "dwellingsPerBlock");
        Refused("\"glyphUnits\": 1.0,", "\"glyphUnitz\": 1.0,", "glyphUnits is missing");
    }

    // ------------------------------------------------------------------ slot ordering: integer composite key, tie-dense

    /// <summary>
    /// TIE-DENSE SLOT ORDERING. The priority is the integer composite (tier, category, key) — unique for distinct claims —
    /// so the claim → lot map is the same for every input order. Dense: 32 claims (both tiers, both categories, keys 1–8)
    /// for 13 lots, so every lot is contested and 19 claims spill; and a one-lot lattice where EVERY claim prefers the same
    /// lot, which the lowest key must win. 200 orders each (a deterministic shuffle). Tier-1 claims carry keys 9–16
    /// (Compose adds a held kind to tier 1 only when the universe does not name it, so no claim is in both tiers).
    /// </summary>
    [Fact]
    public void SlotOrder_IsTheIntegerCompositeKey_TieDense_WhateverTheInputOrder()
    {
        CompositionLattice lattice = SettlementClasses.Default.Lattice;
        var claims = new List<(int Tier, PartClaim Claim)>();
        for (int tier = 0; tier <= 1; tier++)
            for (int cat = 0; cat <= 1; cat++)
                for (int key = 1; key <= 8; key++) claims.Add((tier, new PartClaim(cat, key + 8 * tier)));
        Dictionary<(int, PartClaim), (CompositionLot Lot, bool Spilled)> Map(List<(int Tier, PartClaim Claim)> cs, CompositionLattice l)
        {
            CompositionLot[] lots = SettlementComposer.AssignSlots(7, cs, l, out bool[] spilled);
            var m = new Dictionary<(int, PartClaim), (CompositionLot, bool)>();
            for (int i = 0; i < cs.Count; i++) m[(cs[i].Tier, cs[i].Claim)] = (lots[i], spilled[i]);
            return m;
        }
        var baseline = Map(claims, lattice);
        Assert.Equal(lattice.SlotCount, baseline.Values.Count(v => !v.Spilled));
        Assert.Equal(claims.Count - lattice.SlotCount, baseline.Values.Count(v => v.Spilled));
        // Exactly the first SlotCount claims in (tier, category, key) order hold lots; distinct lots.
        var ordered = claims.OrderBy(c => c.Tier).ThenBy(c => c.Claim.Category).ThenBy(c => c.Claim.Key).ToList();
        for (int i = 0; i < ordered.Count; i++) Assert.Equal(i >= lattice.SlotCount, baseline[(ordered[i].Tier, ordered[i].Claim)].Spilled);
        Assert.Equal(lattice.SlotCount, baseline.Values.Where(v => !v.Spilled).Select(v => (v.Lot.Ring, v.Lot.Index)).Distinct().Count());

        var single = lattice with { SlotRings = [new SlotRing(1.6, 1)] };
        var one = Map(claims, single);
        Assert.False(one[(0, new PartClaim(PartClaim.Structure, 1))].Spilled);   // the lowest composite key wins the only lot
        Assert.Equal(claims.Count - 1, one.Values.Count(v => v.Spilled));

        ulong state = 0x9E3779B97F4A7C15UL;
        for (int round = 0; round < 200; round++)
        {
            var shuffled = new List<(int Tier, PartClaim Claim)>(claims);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
                int j = (int)((state >> 33) % (ulong)(i + 1));
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            Assert.Equal(baseline, Map(shuffled, lattice));
            Assert.Equal(one, Map(shuffled, single));
            int[] order = SettlementComposer.PriorityOrder(shuffled);
            Assert.Equal(ordered, order.Select(i => shuffled[i]).ToList());
        }
    }

    // ------------------------------------------------------------------ stability: arrivals, counts, zoom, pan

    /// <summary>
    /// A LATER ARRIVAL NEVER MOVES AN EARLIER PART (acceptance 1), on the populated institution fixture's capital (its two
    /// structure kinds and the five university types through the seam): in EVERY one of the 7! = 5,040 arrival orders,
    /// each arrival leaves every part already held on its lot, and every block already drawn either stays on its lot or is
    /// hidden where it stands under the newcomer's glyph. A removal moves nothing either (the universe reserves every lot).
    /// </summary>
    [Fact]
    public void ALaterArrival_NeverMovesAnEarlierPart_InEveryArrivalOrder()
    {
        WorldProjection p = Seam();
        SettlementLensView cap = p.Settlements.Single(s => s.Id == fx.Capital);
        Assert.Equal(2, cap.Structures.Count);
        Assert.Equal(5, cap.Institutions.Count);
        var kinds = new List<object>();
        kinds.AddRange(cap.Structures);
        kinds.AddRange(cap.Institutions);
        CompositionLattice lattice = p.Classes.Lattice;
        SettlementComposition Of(IEnumerable<object> held) => SettlementComposer.Compose(cap with
        {
            Structures = held.OfType<StructureView>().OrderBy(x => x.ProjectId).ToList(),
            Institutions = held.OfType<InstitutionView>().OrderBy(x => x.TypeKey).ToList(),
        }, p.Universe, p.Classes);
        SettlementComposition all = Of(kinds);
        Assert.All(all.Parts, part => Assert.False(part.Spilled));
        int orders = 0;
        foreach (int[] perm in Permutations(kinds.Count))
        {
            orders++;
            var held = new List<object>();
            SettlementComposition before = Of(held);
            foreach (int k in perm)
            {
                held.Add(kinds[k]);
                SettlementComposition after = Of(held);
                Dictionary<string, CompositionLot> was = LotsByKey(before), now = LotsByKey(after);
                foreach ((string key, CompositionLot lot) in was) Assert.Equal(lot, now[key]);
                PlacedPart newcomer = after.Parts.Single(x => !was.ContainsKey(x.Key));
                Assert.Equal(LotsByKey(all)[newcomer.Key], newcomer.Lot);   // the newcomer takes its own reserved lot
                foreach (CompositionLot b in before.Blocks)
                    Assert.True(after.Blocks.Contains(b) || SettlementComposer.Covers(lattice, newcomer.Lot, b),
                        $"order {string.Join(",", perm)}: block {b} moved when {newcomer.Key} arrived");
                before = after;
            }
            // Removals, in the same order: nothing that stays moves.
            foreach (int k in perm)
            {
                held.Remove(kinds[k]);
                SettlementComposition after = Of(held);
                foreach ((string key, CompositionLot lot) in LotsByKey(after)) Assert.Equal(LotsByKey(all)[key], lot);
            }
        }
        Assert.Equal(5040, orders);
    }

    private static IEnumerable<int[]> Permutations(int n)
    {
        int[] a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Go(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (int[] r in Go(k + 1)) yield return r;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Go(0);
    }

    /// <summary>
    /// A CHANGE OF COUNT MOVES NOTHING ALREADY PLACED (acceptance 1): more of a kind (granary ×2 → ×9, Engineering ×2 →
    /// ×30) changes no lot; more dwellings only ADD blocks after the ones drawn (a prefix grows); more people grow the
    /// footprint, opening rings outward, and move no part and no block. Past the old cap: a settlement at the measured
    /// maximum draws far more than 48 blocks, on rings the old ring never had.
    /// </summary>
    [Fact]
    public void ACountChange_MovesNothingAlreadyPlaced_AndDwellingsDensifyBeyondTheOldCap()
    {
        WorldProjection p = Seam();
        SettlementLensView cap = p.Settlements.Single(s => s.Id == fx.Capital);
        SettlementComposition c0 = SettlementComposer.Compose(cap, p.Universe, p.Classes);
        SettlementLensView more = cap with
        {
            Structures = cap.Structures.Select(st => st with { Count = st.Count + 7 }).ToList(),
            Institutions = cap.Institutions.Select(iv => iv with { Count = iv.Count + 28 }).ToList(),
        };
        Assert.Equal(LotsByKey(c0), LotsByKey(SettlementComposer.Compose(more, p.Universe, p.Classes)));
        Assert.Equal(c0.Blocks, SettlementComposer.Compose(more, p.Universe, p.Classes).Blocks);

        // Dwellings and people grow together along the measured range: every step keeps every part and every block.
        SettlementComposition prev = SettlementComposer.Compose(cap with { Population = MeasuredMin, Dwellings = 0 }, p.Universe, p.Classes);
        Assert.Empty(prev.Blocks);   // fewer than 25 dwellings: no block is drawn (none is invented)
        int rings = 0;
        for (long pop = MeasuredMin; pop <= MeasuredMax; pop = pop * 21 / 20 + 1)
        {
            long dwellings = pop / 5;
            SettlementComposition next = SettlementComposer.Compose(cap with { Population = pop, Dwellings = dwellings }, p.Universe, p.Classes);
            Assert.Equal(LotsByKey(c0), LotsByKey(next));
            Assert.Equal(prev.Blocks, next.Blocks.Take(prev.Blocks.Count).ToList());   // only appended
            Assert.True(next.OpenBlockLots >= prev.OpenBlockLots);
            Assert.Equal(Math.Min(next.BlocksWanted, next.Blocks.Count), next.Blocks.Count);
            foreach (CompositionLot b in next.Blocks)   // every block inside the footprint (a ring opens only when it fits)
                Assert.True(Math.Sqrt(b.X * b.X + b.Y * b.Y) + p.Classes.Lattice.BlockReach <= next.FootprintUnits + 1e-12, $"block {b} outside {next.FootprintUnits}");
            rings = next.Blocks.Count > 0 ? Math.Max(rings, next.Blocks.Max(b => b.Ring) + 1) : rings;
            prev = next;
        }
        Assert.True(prev.Blocks.Count > 48, $"{prev.Blocks.Count} blocks at the measured maximum (the old cap was 48)");
        Assert.True(rings >= 6, $"{rings} rings opened");
        Assert.True(prev.BlocksWanted > prev.Blocks.Count, "the measured maximum fills its footprint (dense)");
        // Under the block lattice no two blocks share a lot, and none lies under a held part.
        Assert.Equal(prev.Blocks.Count, prev.Blocks.Select(b => (b.Ring, b.Index)).Distinct().Count());
        Assert.DoesNotContain(prev.Blocks, b => prev.Parts.Any(x => SettlementComposer.Covers(p.Classes.Lattice, x.Lot, b)));
    }

    /// <summary>
    /// ZOOM AND PAN MOVE NOTHING (acceptance 1) and THE HIT RECTS FOLLOW THE PLACED PARTS (acceptance 5): the institution
    /// fixture with the seam (7 parts at the capital — the most crowded composition) at the smallest footprint (population
    /// 0) and at the measured maximum, painted at 15 zooms from the World band to the maximum, panned three ways, in three
    /// eras' hands. Every drawn part is reported once (<see cref="LensFrame.Parts"/>, one per institution marker), on the
    /// SAME lot at every zoom and pan; its rect holds its lot's centre as drawn (the hit centre + lot × px per unit) and
    /// lies inside its settlement's hit footprint, so a click on the part selects the settlement.
    /// </summary>
    [Fact]
    public void ZoomAndPan_MoveNothing_AndThePartsHitRectsFollowWhereTheyWereDrawn()
    {
        string hash = WorldHash.ComputeHex(fx.Session.World);
        WorldProjection seam = Seam();
        SettlementLensView capital = seam.Settlements.Single(s => s.Id == fx.Capital);
        int checkedParts = 0;
        foreach (long pop in new[] { 0L, MeasuredMax })
        {
            SettlementLensView cap = capital with { Population = pop, Dwellings = pop / 5 };
            WorldProjection p = Only(seam, cap);
            var lotOf = new Dictionary<string, (int Ring, int Lot)>(StringComparer.Ordinal);
            foreach (EraTheme? theme in new[] { null, EraThemes.For(UiEra.Prehistoric), EraThemes.For(UiEra.Modern) })
            {
                MapInk ink = theme is null ? MapInk.Default : MapInk.For(theme);
                for (int z = 0; z <= 14; z++)
                {
                    double scale = 0.8 * Math.Pow(32 / 0.8, z / 14.0);
                    MapDetail detail = MapDetail.At(scale, 1280, 800, p.KmPerPx);
                    foreach ((double ox, double oy) in new[] { (640.0, 400.0), (300.0, 650.0), (1000.0, 150.0) })
                    {
                        var d = new DrawList();
                        LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, detail,
                            (x, y) => ((x - cap.X) * scale + ox, (y - cap.Y) * scale + oy), scale, new RectD(0, 0, 1280, 800), ink: ink, theme: theme);
                        Assert.Equal(f.Institutions.Count, f.Parts.Count);
                        if (detail.Alpha(WorldLayer.InstitutionalPresence) + detail.Alpha(WorldLayer.InstitutionTypes) <= 0) { Assert.Empty(f.Parts); continue; }
                        Assert.Equal(7, f.Parts.Count);
                        SettlementHit hit = f.SettlementHits.Single();
                        double unit = p.Classes.UnitPx(scale, detail.Settlement);
                        SettlementComposition comp = p.CompositionOf(cap);
                        for (int i = 0; i < f.Parts.Count; i++)
                        {
                            PartPlacement part = f.Parts[i];
                            Assert.Equal(f.Institutions[i].Key, part.Key);
                            if (lotOf.TryGetValue(part.Key, out (int Ring, int Lot) was)) Assert.Equal(was, (part.Ring, part.Lot));
                            else lotOf[part.Key] = (part.Ring, part.Lot);
                            CompositionLot lot = comp.Parts.Single(x => x.Key == part.Key).Lot;
                            double lx = hit.X + lot.X * unit, ly = hit.Y + lot.Y * unit;
                            Assert.True(part.Rect.Contains(lx, ly), $"{part.Key}: rect {part.Rect} does not hold its lot ({lx}, {ly})");
                            Assert.True(LensHitGeometryTests.Holds(hit.Footprint, part.Rect),
                                $"pop {pop} {theme?.Era} x{scale:0.###}: {part.Key} rect {part.Rect} outside the footprint {hit.Footprint}");
                            Assert.Equal(cap.Id, SettlementSelection.HitTest([hit], part.Rect.CenterX, part.Rect.CenterY));
                            checkedParts++;
                        }
                    }
                }
            }
        }
        Assert.True(checkedParts > 1000, $"only {checkedParts} parts checked");
        Assert.Equal(hash, WorldHash.ComputeHex(fx.Session.World));   // composing and painting read state only
    }

    /// <summary>
    /// A PAN NEVER POPS A SETTLEMENT IN OR OUT WHILE ANY OF IT CAN BE ON SCREEN (fix to the P12a review finding: with the
    /// 34–150 px footprint clamp gone, the fixed 200 px centre margin of the settlement cull dropped a large settlement
    /// whose footprint, blocks, glyphs and sector ring still reached the viewport — at ×32 the measured maximum, 61,078
    /// people, paints a 209 px footprint and the stated saturation, 4,096,000, a 330 px one). The capital of the
    /// institution fixture (its 7 parts or 20, notables, a sector ring or the default-split caption, a banner or none,
    /// selected or not) at the measured minimum and maximum and the saturation, at 8 zooms from the World band to ×32 and in
    /// three eras' hands, is panned across every edge and corner of a 1280×800 viewport: wherever ANY command it paints
    /// with nothing culled has bounds (<see cref="DrawListCull.Bounds"/>, which the replay keeps) that touch the viewport,
    /// the culled paint draws it — probed in 6 px steps and a quarter pixel either side of the exact position where its painted box first touches.
    /// The cull still culls: clear probes are culled, and 1,000 px clear of the viewport the settlement is not drawn. First,
    /// the finding's own cases: the fixture's capital at ×32 with its footprint's edge 8 px on screen is drawn.
    /// </summary>
    [Fact]
    public void APan_NeverCullsASettlementWhileAnyOfItsCommandsCanTouchTheViewport()
    {
        string hash = WorldHash.ComputeHex(fx.Session.World);
        WorldProjection seam = Seam();
        SettlementLensView capital = seam.Settlements.Single(s => s.Id == fx.Capital);
        var viewport = new RectD(0, 0, 1280, 800);
        var everywhere = new RectD(-1e6, -1e6, 2e6, 2e6);

        // The finding's own cases: the capital as the fixture has it, at ×32, its footprint's right edge 8 px inside the
        // viewport's left edge — its centre 201 px out at the measured maximum (209 px footprint) and 322 px out at the
        // saturation (330 px). The 200 px centre margin culled both, though blocks, glyphs and the footprint were on screen.
        foreach (long pop in new[] { MeasuredMax, SettlementClasses.Default.SaturationPopulation })
        {
            SettlementLensView v = capital with { Population = pop, Dwellings = pop / 5 };
            WorldProjection p = Only(seam, v);
            double scale = Camera.MaxZoom;
            MapDetail detail = MapDetail.At(scale, 1280, 800, p.KmPerPx);
            double rFoot = WorldLens.FootprintRadiusPx(p, v, scale, detail.Settlement);
            Assert.True(rFoot > 200, $"pop {pop}: footprint {rFoot} px");
            var d = new DrawList();
            LensFrame f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, detail,
                (x, y) => ((x - v.X) * scale + 8 - rFoot, (y - v.Y) * scale + 400), scale, viewport);
            Assert.Equal([v.Id], f.SettlementsDrawn);
            Assert.Equal(7, f.Parts.Count);
            Assert.Contains(d.Commands, c => DrawListCull.Bounds(c) is RectD b && DrawListCull.Touches(b, viewport));
        }

        int touching = 0, beyondOldMargin = 0, culled = 0, configs = 0;
        // 13 more structure kinds (synthetic project ids content does not name — they claim after content's and spill): a
        // key of 20 entries.
        var crowded = capital.Structures.Concat(Enumerable.Range(1, 13)
            .Select(i => new StructureView(9000 + i, "Works " + i.ToString(CultureInfo.InvariantCulture), StructureKind.Other, 1))).ToList();
        foreach (long pop in new[] { MeasuredMin, MeasuredMax, SettlementClasses.Default.SaturationPopulation })
        foreach (int variant in new[] { 0, 1, 2, 3 })
        {
            // 0: unselected, a sector ring, notables, a banner; 1: selected, the default-split caption, no notables, a
            // banner; 2: as 0 with no banner (Age 0); 3: as 2 with the 20-entry key.
            SettlementLensView v = capital with
            {
                Population = pop, Dwellings = pop / 5, Age = variant >= 2 ? 0 : Math.Max(1, capital.Age),
                Notables = variant == 1 ? 0 : 3, Sectors = variant == 1 ? null : [0.4, 0.3, 0.15, 0.1, 0.05],
                Structures = variant == 3 ? crowded : capital.Structures,
            };
            Assert.True(v.Controller >= 0, "the banner needs a controller");
            WorldProjection p = Only(seam, v);
            int selected = variant == 1 ? v.Id : -1;
            foreach (EraTheme? theme in new[] { null, EraThemes.For(UiEra.Prehistoric), EraThemes.For(UiEra.Modern) })
            {
                MapInk ink = theme is null ? MapInk.Default : MapInk.For(theme);
                for (int z = 0; z <= 14; z += 2)
                {
                    double scale = 0.8 * Math.Pow(Camera.MaxZoom / 0.8, z / 14.0);
                    MapDetail detail = MapDetail.At(scale, 1280, 800, p.KmPerPx);
                    DrawList Paint(double ox, double oy, RectD within, out LensFrame f)
                    {
                        var d = new DrawList();
                        f = WorldLens.Paint(d, ApproxTextMeasure.Instance, p, detail,
                            (x, y) => ((x - v.X) * scale + ox, (y - v.Y) * scale + oy), scale, within, selected, ink: ink, theme: theme);
                        return d;
                    }
                    // The settlement's painted box about its centre, with nothing culled.
                    RectD box = Union(Paint(0, 0, everywhere, out _).Commands);
                    configs++;
                    var probes = new List<(double X, double Y)>();
                    for (double clear = -36; clear <= 36; clear += 6)
                    {
                        probes.Add((-box.Right - clear, 400));                    // across the left edge
                        probes.Add((1280 - box.X + clear, 400));                  // the right
                        probes.Add((640, -box.Bottom - clear));                   // the top
                        probes.Add((640, 800 - box.Y + clear));                   // the bottom
                        probes.Add((-box.Right - clear, -box.Bottom - clear));    // the corners
                        probes.Add((1280 - box.X + clear, 800 - box.Y + clear));
                        probes.Add((-box.Right - clear, 800 - box.Y + clear));
                        probes.Add((1280 - box.X + clear, -box.Bottom - clear));
                    }
                    foreach (double e in new[] { -0.25, 0.25 })
                    {
                        probes.Add((-box.Right - e, 400)); probes.Add((1280 - box.X + e, 400));
                        probes.Add((640, -box.Bottom - e)); probes.Add((640, 800 - box.Y + e));
                    }
                    foreach ((double ox, double oy) in probes)
                    {
                        bool touches = Paint(ox, oy, everywhere, out _).Commands
                            .Any(c => DrawListCull.Bounds(c) is RectD b && DrawListCull.Touches(b, viewport));
                        Paint(ox, oy, viewport, out LensFrame f);
                        bool drawn = f.SettlementsDrawn.Contains(v.Id);
                        if (touches)
                        {
                            touching++;
                            Assert.True(drawn, $"pop {pop} {theme?.Era} x{scale:0.###} selected {selected}: centre ({ox:0.##}, {oy:0.##}) "
                                + $"culled while its painted box {box} touches the viewport");
                            if (ox < -200 || ox > 1480 || oy < -200 || oy > 1000) beyondOldMargin++;
                        }
                        else if (!drawn) culled++;
                    }
                    Paint(-box.Right - 1000, 400, viewport, out LensFrame far);
                    Assert.DoesNotContain(v.Id, far.SettlementsDrawn);
                }
            }
        }
        Assert.Equal(3 * 4 * 3 * 8, configs);
        // Measured on this fixture: 12,428 touching probes (3,656 of them with the centre beyond the old 200 px margin), and
        // every one of the 19,828 clear probes culled.
        Assert.True(touching > 12_000, $"only {touching} touching probes");
        Assert.True(beyondOldMargin > 3_500, $"only {beyondOldMargin} touching probes lay beyond the old 200 px centre margin");
        Assert.True(culled > 19_500, $"only {culled} clear probes were culled");
        Assert.Equal(hash, WorldHash.ComputeHex(fx.Session.World));

        static RectD Union(IReadOnlyList<DrawCmd> commands)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (DrawCmd c in commands)
                if (DrawListCull.Bounds(c) is RectD b)
                {
                    x0 = Math.Min(x0, b.X); y0 = Math.Min(y0, b.Y); x1 = Math.Max(x1, b.Right); y1 = Math.Max(y1, b.Bottom);
                }
            return new RectD(x0, y0, x1 - x0, y1 - y0);
        }
    }

    // ------------------------------------------------------------------ table order

    /// <summary>
    /// ANY TABLE INPUT ORDER GIVES THE SAME LAYOUT (acceptance 2). The fixture world with founded institution rows of
    /// several types in two settlements, rebuilt with its Settlements, Structures, Institutions, Buckets and Housing rows
    /// REVERSED and then rotated: every settlement's composition and every drawn part's lot and rect are identical, and
    /// the projection's structure list is in project-id order either way.
    /// </summary>
    [Fact]
    public void AnyTableInputOrder_GivesTheSameLayout()
    {
        WorldState w = fx.Session.World.Clone();
        SimConfig cfg = fx.Session.Config;
        // Founded institutions (the real table's row type): several types, two settlements, out of key order.
        int id = 1;
        foreach ((int settlement, int type) in new[] { (fx.Capital, 4), (fx.Second, 2), (fx.Capital, 1), (fx.Capital, 4), (fx.Second, 5) })
            w.Institutions.Add(new InstitutionRow(id++, Me, new SettlementId(settlement), type, 3 + id, 0.25));
        WorldState Reordered(WorldState src, Func<int, int, int> at)
        {
            WorldState c = src.Clone();
            static void Permute<T>(Table<T> t, Func<int, int, int> at) where T : unmanaged
            {
                var rows = new T[t.Count];
                for (int i = 0; i < rows.Length; i++) rows[i] = t[i];
                t.Clear();
                for (int i = 0; i < rows.Length; i++) t.Add(rows[at(i, rows.Length)]);
            }
            Permute(c.Settlements, at);
            Permute(c.Structures, at);
            Permute(c.Institutions, at);
            Permute(c.Buckets, at);
            Permute(c.Housing, at);
            return c;
        }
        WorldProjection Build(WorldState x) => WorldProjection.Build(x, cfg, i => fx.Session.Names.Name(i), Me);
        WorldProjection a = Build(w);
        Assert.Contains(a.Settlements, s => s.Institutions.Count >= 2 && s.Structures.Count == 2);
        foreach (Func<int, int, int> order in new Func<int, int, int>[] { (i, n) => n - 1 - i, (i, n) => (i + n / 3 + 1) % n })
        {
            WorldState permuted = Reordered(w, order);
            WorldProjection b = Build(permuted);
            Assert.Equal(a.Settlements.Count, b.Settlements.Count);
            foreach (SettlementLensView sa in a.Settlements)
            {
                SettlementLensView sb = b.Settlements.Single(s => s.Id == sa.Id);
                Assert.Equal(sa.Structures, sb.Structures);
                Assert.Equal(sa.Institutions, sb.Institutions);
                SettlementComposition ca = a.CompositionOf(sa), cb = b.CompositionOf(sb);
                Assert.Equal(ca.Parts, cb.Parts);
                Assert.Equal(ca.Blocks, cb.Blocks);
                Assert.Equal((ca.SizeClass, ca.SizeIndex, ca.FootprintUnits), (cb.SizeClass, cb.SizeIndex, cb.FootprintUnits));
                Assert.Equal(sa.Structures.Select(x => x.ProjectId).OrderBy(x => x), sa.Structures.Select(x => x.ProjectId));
            }
            foreach (WorldZoom z in new[] { WorldZoom.Regional, WorldZoom.Settlement })
            {
                LensFrame fa = WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, a, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024));
                LensFrame fb = WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, b, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024));
                Assert.Equal(fa.Parts.OrderBy(x => x.Settlement).ThenBy(x => x.Key, StringComparer.Ordinal),
                    fb.Parts.OrderBy(x => x.Settlement).ThenBy(x => x.Key, StringComparer.Ordinal));
            }
        }
    }

    // ------------------------------------------------------------------ built only, and persistent

    /// <summary>
    /// BUILT ONLY, AND PERSISTENT — read from real state. A university QUEUED at the capital is UNDER_CONSTRUCTION
    /// (<see cref="InstitutionsQuery.Lifecycle"/>) and draws nothing; its building STANDING but not yet founded is still
    /// UNDER_CONSTRUCTION and draws nothing — neither a hall nor a generic structure glyph (ADR-033 D6). The real
    /// InstitutionsSystem founds it on the next End Turn: it is ACTIVE and its hall appears on its lot; four more turns of
    /// the real simulation and it is still drawn, on the same lot, and so is every structure.
    /// </summary>
    [Fact]
    public void OnlyWhatIsBuiltIsDrawn_UnderConstructionIsNot_AndAFoundedInstitutionPersistsOnItsLot()
    {
        SimConfig cfg = fx.Session.Config;
        const int Type = 1;
        ConstructionProjectEntry project = InstitutionContent.ProjectOfType(cfg, Type)!;
        var cap = new SettlementId(fx.Capital);
        WorldProjection Build(IReadOnlyWorldState w) => WorldProjection.Build(w, cfg, i => fx.Session.Names.Name(i), Me);
        static bool Drawn(WorldProjection p, int settlement, string key)
        {
            foreach (WorldZoom z in new[] { WorldZoom.Regional, WorldZoom.Settlement })
                if (WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p, z, (x, y) => (x * 4, y * 4), 4, new RectD(0, 0, 1024, 1024))
                        .Parts.Any(x => x.Settlement == settlement && x.Key == key)) return true;
            return false;
        }
        string hall = "institution:" + Type.ToString(CultureInfo.InvariantCulture);
        string building = "structure:" + project.Id.ToString(CultureInfo.InvariantCulture);

        // Queued.
        WorldState queued = fx.Session.World.Clone();
        queued.ConstructionQueue.Add(new ConstructionQueueRow(cap, 1, project.Id));
        Assert.Equal(InstitutionLifecycle.UnderConstruction, InstitutionsQuery.Lifecycle(queued, cfg, Me, cap, Type));
        Assert.False(Drawn(Build(queued), fx.Capital, hall));
        Assert.False(Drawn(Build(queued), fx.Capital, building));

        // Built, not yet founded.
        WorldState built = fx.Session.World.Clone();
        built.Structures.Add(new StructureRow(cap, project.Id, 1));
        Assert.Equal(InstitutionLifecycle.UnderConstruction, InstitutionsQuery.Lifecycle(built, cfg, Me, cap, Type));
        Assert.Empty(InstitutionsQuery.At(built, cfg, cap));
        Assert.False(Drawn(Build(built), fx.Capital, hall));
        Assert.False(Drawn(Build(built), fx.Capital, building));

        // Founded by the real system on the next End Turn; then four more turns.
        UiSession s = UiSession.StartFrom(built, 42, 256, 4);
        s.EndTurn();
        InstitutionLifecycle stage = InstitutionsQuery.Lifecycle(s.World, cfg, Me, cap, Type);
        Assert.True(stage is InstitutionLifecycle.Active or InstitutionLifecycle.Mature, $"founded: {stage}");
        WorldProjection first = Build(s.World);
        Assert.True(Drawn(first, fx.Capital, hall));
        Assert.False(Drawn(first, fx.Capital, building));
        Dictionary<string, CompositionLot> lots = LotsByKey(first.CompositionOf(first.Settlements.Single(x => x.Id == fx.Capital)));
        Assert.Equal(3, lots.Count);   // granary, workshop, the hall
        for (int t = 0; t < 4; t++)
        {
            s.EndTurn();
            WorldProjection p = Build(s.World);
            Assert.True(Drawn(p, fx.Capital, hall), $"turn {s.World.Clock.Turn}: the founded hall is gone");
            Assert.Equal(lots, LotsByKey(p.CompositionOf(p.Settlements.Single(x => x.Id == fx.Capital))));
        }
    }

    // ------------------------------------------------------------------ a pure function of state

    /// <summary>
    /// THE COMPOSITION IS A PURE FUNCTION OF STATE — identical after a snapshot round trip, after a session RESUME, and
    /// across processes. Snapshot: the fixture world (structures and founded institutions) saved and loaded composes
    /// identically. Resume: a canonical seed-42 session played 4 turns, saved the way the game saves it and resumed by
    /// replay, composes every settlement identically. Across processes: the literal lots below were computed by this
    /// code in another process; the hash beneath them is pinned by an independent implementation
    /// (<see cref="StableHash_IsPinnedByIndependentVectors"/>), so a process that placed anything differently fails here.
    /// </summary>
    [Fact]
    public void Composition_IsIdentical_AfterSnapshotRoundTrip_AfterResume_AndAcrossProcesses()
    {
        SimConfig cfg = fx.Session.Config;
        WorldState w = fx.Session.World.Clone();
        w.Institutions.Add(new InstitutionRow(1, Me, new SettlementId(fx.Capital), 3, 5, 0.5));
        WorldState loaded;
        using (var ms = new MemoryStream())
        {
            Snapshot.Save(w, ms);
            ms.Position = 0;
            loaded = Snapshot.Load(ms, w.Terrain);
        }
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(loaded));
        AssertSameCompositions(WorldProjection.Build(w, cfg, i => fx.Session.Names.Name(i), Me),
            WorldProjection.Build(loaded, cfg, i => fx.Session.Names.Name(i), Me));

        string dir = Path.Combine(Path.GetTempPath(), "p12a-compose-resume-"
            + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        string log = Path.Combine(dir, "orders-20261011-120000.bin");
        try
        {
            UiSession live = UiSession.Start(42);
            live.ExportManifest("2026-10-11 12:00:00", log);
            for (int t = 0; t < 4; t++) live.EndTurn();
            live.Save(log);
            live.ExportTrace(UiSession.TracePath(log));
            UiSession resumed = UiSession.Resume(UiSession.ManifestPath(log), out _);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            WorldProjection pl = WorldProjection.Build(live.World, live.Config, i => live.Names.Name(i), Me);
            WorldProjection pr = WorldProjection.Build(resumed.World, resumed.Config, i => resumed.Names.Name(i), Me);
            AssertSameCompositions(pl, pr);
            Assert.Equal(PinnedCanonicalDigest, Digest(pl));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }

        // The institution fixture's capital holding every kind of the content (structures through the table, the five
        // university types through the seam): each kind's lot, literally.
        WorldProjection seam = Seam();
        SettlementComposition c = seam.CompositionOf(seam.Settlements.Single(x => x.Id == fx.Capital));
        string lots = string.Join(" ", c.Parts.Select(x => x.Key + "@" + x.Lot.Ring.ToString(CultureInfo.InvariantCulture) + "." + x.Lot.Index.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(PinnedCapitalLots, lots);
    }

    /// <summary>The canonical seed-42 world after 4 End Turns, every settlement's composition (<see cref="Digest"/>).
    /// BATCH 3 / D-054 RE-PIN (integration R1, 2026-10-14; MEASURED on this tree by the agent writing this line):
    /// d8fbf5692a457c71… -> 31a160dc6f1d36d9…. CAUSE: the CYLINDER alone (worldgen.json topology.wrapX — the canonical world
    /// regenerates x-periodic, so its settlements' sizes, footprints and parts after 4 turns differ). This digest is a
    /// composition digest, not a WorldHash, so the v33 layout cannot reach it; WS1/WS2 do not act in 4 order-less turns.
    /// ATTRIBUTION: Composition_WithTheCylinderOff_IsTheBatch2Digest (UiSession.Start(42, wrapXOverride: false), the flat
    /// world, 4 End Turns) returns <see cref="Batch2CanonicalDigest"/> byte for byte.</summary>
    private const string PinnedCanonicalDigest = "31a160dc6f1d36d9349f0ce0538ccb33aa6c0087aae1e10cc0c13528e6e8a33a";

    /// <summary>The batch-2 value of <see cref="PinnedCanonicalDigest"/> — the flat (pre-cylinder) canonical world.</summary>
    private const string Batch2CanonicalDigest = "d8fbf5692a457c716fc5b2b001b4eb80b6ba740f5b2cc786946bc6e5531f6769";

    /// <summary>BATCH 3 / D-054 ATTRIBUTION CONTROL (integration R1, 2026-10-14) for <see cref="PinnedCanonicalDigest"/>: the
    /// same 4 End Turns on the FLAT canonical world (UiSession.Start's wrapXOverride: false; the sim half as shipped) compose
    /// every settlement exactly as the batch-2 build did, so the cylinder is the whole of the digest's move.</summary>
    [Fact]
    public void Composition_WithTheCylinderOff_IsTheBatch2Digest()
    {
        UiSession flat = UiSession.Start(42, wrapXOverride: false);
        Assert.False(flat.World.Terrain!.WrapX);
        for (int t = 0; t < 4; t++) flat.EndTurn();
        WorldProjection p = WorldProjection.Build(flat.World, flat.Config, i => flat.Names.Name(i), Me);
        Assert.Equal(Batch2CanonicalDigest, Digest(p));
    }

    /// <summary>The institution fixture capital's parts and their lots (ring.index).</summary>
    private const string PinnedCapitalLots =
        "structure:1@0.2 structure:2@0.1 institution:1@0.3 institution:2@0.4 institution:3@0.0 institution:4@1.5 institution:5@1.4";

    private static void AssertSameCompositions(WorldProjection a, WorldProjection b)
    {
        Assert.Equal(a.Settlements.Count, b.Settlements.Count);
        foreach (SettlementLensView sa in a.Settlements)
        {
            SettlementComposition ca = a.CompositionOf(sa), cb = b.CompositionOf(b.Settlements.Single(s => s.Id == sa.Id));
            Assert.Equal(ca.Parts, cb.Parts);
            Assert.Equal(ca.Blocks, cb.Blocks);
            Assert.Equal((ca.SizeClass, ca.SizeIndex, ca.FootprintUnits, ca.BlocksWanted, ca.OpenBlockLots),
                (cb.SizeClass, cb.SizeIndex, cb.FootprintUnits, cb.BlocksWanted, cb.OpenBlockLots));
        }
    }

    /// <summary>A SHA-256 of every settlement's composition in id order: size class, size index (R), footprint (R), parts
    /// (key, ring, index) and blocks (ring, index) — the lots' identities, not their trig.</summary>
    internal static string Digest(WorldProjection p)
    {
        var sb = new StringBuilder();
        foreach (SettlementLensView s in p.Settlements.OrderBy(x => x.Id))
        {
            SettlementComposition c = p.CompositionOf(s);
            sb.Append(s.Id.ToString(CultureInfo.InvariantCulture)).Append(':').Append(c.SizeClass.ToString(CultureInfo.InvariantCulture))
              .Append(' ').Append(c.SizeIndex.ToString("R", CultureInfo.InvariantCulture))
              .Append(' ').Append(c.FootprintUnits.ToString("R", CultureInfo.InvariantCulture));
            foreach (PlacedPart part in c.Parts)
                sb.Append(' ').Append(part.Key).Append('@').Append(part.Lot.Ring.ToString(CultureInfo.InvariantCulture)).Append('.').Append(part.Lot.Index.ToString(CultureInfo.InvariantCulture));
            sb.Append(" |");
            foreach (CompositionLot b in c.Blocks)
                sb.Append(' ').Append(b.Ring.ToString(CultureInfo.InvariantCulture)).Append('.').Append(b.Index.ToString(CultureInfo.InvariantCulture));
            sb.Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>The stable hash VARIES the composition between settlements — the point of hashing (settlements are not
    /// stamped from one die) — while each settlement keeps its own for ever: across 64 settlement ids holding every kind of
    /// the content, the kinds' arrangement and the block rings' rotation take many values, and each id gives the same
    /// arrangement every time it is composed.</summary>
    [Fact]
    public void TheHashVariesTheArrangementBetweenSettlements_AndEachKeepsItsOwn()
    {
        WorldProjection p = Seam();
        SettlementLensView cap = p.Settlements.Single(s => s.Id == fx.Capital);
        var arrangements = new HashSet<string>(StringComparer.Ordinal);
        var rotations = new HashSet<double>();
        for (int id = 0; id < 64; id++)
        {
            SettlementLensView v = cap with { Id = id, Population = 5_000, Dwellings = 1_000 };
            SettlementComposition a = SettlementComposer.Compose(v, p.Universe, p.Classes), b = SettlementComposer.Compose(v, p.Universe, p.Classes);
            Assert.Equal(a.Parts, b.Parts);
            Assert.Equal(a.Blocks, b.Blocks);
            arrangements.Add(string.Join(" ", a.Parts.Select(x => x.Key + "@" + x.Lot.Ring.ToString(CultureInfo.InvariantCulture) + "." + x.Lot.Index.ToString(CultureInfo.InvariantCulture))));
            rotations.Add(SettlementComposer.RingRotationDeg(id, 0));
        }
        Assert.True(arrangements.Count >= 32, $"only {arrangements.Count} distinct arrangements over 64 settlements");
        Assert.True(rotations.Count >= 32, $"only {rotations.Count} distinct block-ring rotations over 64 settlements");
    }

    // ------------------------------------------------------------------ content guard

    /// <summary>Every kind the shipped content can build has its own lot inside the smallest footprint: no part spills, no
    /// two kinds share a lot, and every glyph lot clears the smallest footprint's edge by its glyph's reach (the loader's
    /// rule, restated against the content). Institutions stay aggregated: one part per TYPE whatever its count.</summary>
    [Fact]
    public void EveryContentKind_HasItsOwnLot_InsideTheSmallestFootprint()
    {
        WorldProjection p = Seam();
        PartUniverse u = p.Universe;
        Assert.Equal(PartUniverse.Of(fx.Session.Config).StructureProjects, u.StructureProjects);
        Assert.Equal(PartUniverse.Of(fx.Session.Config).InstitutionTypes, u.InstitutionTypes);
        Assert.Equal([fx.Granary, fx.Workshop], u.StructureProjects);
        Assert.Equal([1, 2, 3, 4, 5], u.InstitutionTypes);
        CompositionLattice lattice = p.Classes.Lattice;
        Assert.True(u.StructureProjects.Count + u.InstitutionTypes.Count <= lattice.SlotCount);
        SettlementLensView cap = p.Settlements.Single(s => s.Id == fx.Capital);
        SettlementComposition c = SettlementComposer.Compose(cap with { Population = 0, Dwellings = 0 }, u, p.Classes);
        Assert.Equal(cap.Structures.Count + cap.Institutions.Count, c.Parts.Count);
        Assert.All(c.Parts, x => Assert.False(x.Spilled));
        Assert.Equal(c.Parts.Count, c.Parts.Select(x => (x.Lot.Ring, x.Lot.Index)).Distinct().Count());
        foreach (PlacedPart x in c.Parts)
            Assert.True(Math.Sqrt(x.Lot.X * x.Lot.X + x.Lot.Y * x.Lot.Y) + lattice.GlyphReach <= c.FootprintUnits + 1e-12);
        Assert.Single(cap.Institutions, iv => iv.TypeKey == 3 && iv.Count == 2);   // Engineering ×2: one part
        Assert.Single(c.Parts, x => x.Key == "institution:3");
    }
}
