using System.Text;
using Sim.Ui.World;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;
using Xunit;
using static Sim.Ui.Tests.World.WorldTestKit;

namespace Sim.Ui.Tests.World;

/// <summary>
/// Task Part 6: placement inside a settlement's composed sprite is deterministic, stable and
/// reproducible — the same reports give the same slots in any process and any input order,
/// and nothing moves unless the reports change in the one documented way (a removal).
/// Placement and blocks are tested as pure functions fed permuted arrays directly, bypassing
/// the snapshot's sort, so a permutation test can actually fail.
/// </summary>
public class WorldPlacementTests
{
    // ---------------------------------------------------------------- the hash

    [Fact]
    public void Fnv1a64_MatchesThePublishedVectors()
    {
        Assert.Equal(0xcbf29ce484222325UL, StableHash.Fnv1a64([]));
        Assert.Equal(0xaf63dc4c8601ec8cUL, StableHash.Fnv1a64(Encoding.UTF8.GetBytes("a")));
        Assert.Equal(0x85944171f73967e8UL, StableHash.Fnv1a64(Encoding.UTF8.GetBytes("foobar")));
    }

    [Fact]
    public void StableHash_IsPinnedAcrossProcesses()
    {
        // Literal values: a per-process hash (string.GetHashCode) could never pass this.
        Assert.Equal(0UL, StableHash.Fmix64(0));
        Assert.Equal(StableHash.Fmix64(StableHash.Fnv1a64(Encoding.UTF8.GetBytes("s-veyra\0u-123"))), StableHash.Of("s-veyra", "u-123"));
        Assert.Equal(PinnedVeyraU123, StableHash.Of("s-veyra", "u-123"));
        Assert.NotEqual(StableHash.Of("ab", "c"), StableHash.Of("a", "bc"));   // the 0x00 separator
    }

    // Computed independently (a Python FNV-1a 64 + fmix64 over b"s-veyra\x00u-123") and pinned.
    private const ulong PinnedVeyraU123 = 15447159657008429857UL;

    [Fact]
    public void FinalMix_SeparatesKeysThatDifferInOneDigit()
    {
        // Plain FNV-1a's low bits track the low bits of each byte: 'univ-1' / 'univ-9' (0x31/0x39)
        // would share a slot modulo 2 and 4. With fmix64 they need not; check the mod-8 spread.
        var slots = new HashSet<int>();
        for (int i = 0; i < 10; i++) slots.Add(StableHash.Index(StableHash.Of("s", "univ-" + i), 8));
        Assert.True(slots.Count >= 5, $"only {slots.Count} distinct slots of 8 for ten keys");
    }

    // ---------------------------------------------------------------- slots

    private static readonly CompositionLayout Layout = Morph().Layout;

    private static Composition.Claim C(string key, long? est) => new(key, est, key);

    private static Dictionary<string, LotGeometry> Assign(string settlement, IReadOnlyList<Composition.Claim> claims)
    {
        LotGeometry[] slots = Composition.AssignSlots(settlement, claims, Layout);
        var map = new Dictionary<string, LotGeometry>(StringComparer.Ordinal);
        for (int i = 0; i < claims.Count; i++) map[claims[i].HashKey] = slots[i];
        return map;
    }

    private static List<Composition.Claim> Claims(int n) =>
        Enumerable.Range(0, n).Select(i => C($"s-{i:D3}", i / 3)).ToList();   // tie-dense: three per established

    [Fact]
    public void Slots_AreIdentical_OnRepeat_AndUnderEveryPermutation()
    {
        List<Composition.Claim> claims = Claims(14);
        Dictionary<string, LotGeometry> a = Assign("s-veyra", claims);
        var rng = new System.Random(7);   // test-side shuffling only
        for (int round = 0; round < 25; round++)
        {
            List<Composition.Claim> shuffled = claims.OrderBy(_ => rng.Next()).ToList();
            Dictionary<string, LotGeometry> b = Assign("s-veyra", shuffled);
            foreach (Composition.Claim c in claims) Assert.Equal(a[c.HashKey], b[c.HashKey]);
        }
    }

    [Fact]
    public void Slots_TieDense_EqualEstablishedResolveByKey()
    {
        var claims = new List<Composition.Claim> { C("b", 1), C("a", 1), C("c", 1) };
        int[] order = Composition.PriorityOrder(claims);
        Assert.Equal(["a", "b", "c"], order.Select(i => claims[i].HashKey));
    }

    [Fact]
    public void Slots_ALaterArrival_NeverMovesAnEarlierOne()
    {
        List<Composition.Claim> claims = Claims(20);
        Dictionary<string, LotGeometry> before = Assign("s-veyra", claims);
        for (int extra = 0; extra < 10; extra++)
        {
            claims.Add(C($"z-{extra}", 100 + extra));
            Dictionary<string, LotGeometry> after = Assign("s-veyra", claims);
            foreach ((string key, LotGeometry g) in before) Assert.Equal(g, after[key]);
        }
    }

    /// <summary>The one accepted relocation: removing a claim frees its slot, and only claims
    /// whose probe sequence PASSED that slot may move — each into an earlier lot of its own
    /// probe sequence. Everything else is bit-identical.</summary>
    [Fact]
    public void Slots_ARemoval_MovesOnlyClaimsWhoseProbeCrossedTheFreedSlot()
    {
        List<Composition.Claim> claims = Claims(24);
        Dictionary<string, LotGeometry> before = Assign("s-veyra", claims);
        for (int removed = 0; removed < claims.Count; removed += 5)
        {
            var less = claims.Where((_, i) => i != removed).ToList();
            Dictionary<string, LotGeometry> after = Assign("s-veyra", less);
            int moved = 0;
            foreach (Composition.Claim c in less)
            {
                if (after[c.HashKey] == before[c.HashKey]) continue;
                moved++;
                // A move is only ever INTO a slot that the removal (directly, or through the
                // chain of moves it starts) freed EARLIER in the claim's own probe sequence.
                List<LotGeometry> probe = ProbeSequence("s-veyra", c.HashKey);
                int was = probe.IndexOf(before[c.HashKey]), now = probe.IndexOf(after[c.HashKey]);
                Assert.True(now >= 0 && now < was, $"{c.HashKey} must move EARLIER in its own probe sequence");
                Assert.True(string.CompareOrdinal(c.HashKey, claims[removed].HashKey) > 0 || c.Established > claims[removed].Established,
                    $"{c.HashKey} ranks before the removed claim and cannot have been displaced by it");
            }
            Assert.True(moved <= less.Count);
        }
    }

    private static List<LotGeometry> ProbeSequence(string settlement, string key)
    {
        LotGeometry[] lattice = CompositionGeometry.SlotCentres(Layout);
        ulong h = StableHash.Of(settlement, key);
        var seq = new List<LotGeometry>();
        int start = 0;
        foreach (LotRing ring in Layout.SlotRings)
        {
            int s = StableHash.Index(h, ring.Lots);
            for (int p = 0; p < ring.Lots; p++) seq.Add(lattice[start + (s + p) % ring.Lots]);
            start += ring.Lots;
        }
        return seq;
    }

    [Fact]
    public void Slots_AreBounded_AndSpillIsDistinct_WhenEverySlotIsTaken()
    {
        int total = Layout.SlotCount + 30;
        List<Composition.Claim> claims = Enumerable.Range(0, total).Select(i => C($"k{i}", i)).ToList();
        LotGeometry[] slots = Composition.AssignSlots("s", claims, Layout);
        Assert.Equal(total, slots.Distinct().Count());
        Assert.Equal(30, slots.Count(s => s.Ring >= Layout.SlotRings.Count));
    }

    // ---------------------------------------------------------------- through the builder

    private static Dictionary<string, LotGeometry?> SlotsOf(WorldView v) =>
        v.Structures.ToDictionary(s => s.Report.Key, s => s.Slot, StringComparer.Ordinal);

    [Fact]
    public void SettlementStageChange_MovesNoStructure_AndNoBlock()
    {
        WorldMorphology m = Morph();
        StructureReport[] st = Enumerable.Range(0, 6).Select(i => StructureOf($"u{i}", "university", "s", i)).ToArray();
        WorldView small = WorldViewBuilder.Build(Sources([SettlementAt("s", 10, 10, ("population", 500))], st), m);
        WorldView large = WorldViewBuilder.Build(Sources([SettlementAt("s", 10, 10, ("population", 900000))], st), m);
        Assert.NotEqual(small.Settlements[0].Stage.Index, large.Settlements[0].Stage.Index);
        Assert.Equal(SlotsOf(small), SlotsOf(large));
        // The larger stage draws MORE blocks; every block the smaller one drew is still there.
        Assert.Subset(large.Settlements[0].Blocks.ToHashSet(), small.Settlements[0].Blocks.ToHashSet());
    }

    [Fact]
    public void StructureStageChange_MovesNothing()
    {
        WorldMorphology m = Morph();
        StructureReport[] a = [StructureOf("u0", "university", "s", 0, 1, ReportedVisibility.Visible, ("capacity", 100)), StructureOf("u1", "university", "s", 1)];
        StructureReport[] b = [StructureOf("u0", "university", "s", 0, 1, ReportedVisibility.Visible, ("capacity", 9000)), StructureOf("u1", "university", "s", 1)];
        WorldView va = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], a), m);
        WorldView vb = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], b), m);
        Assert.NotEqual(va.Structures[0].Stage.Index, vb.Structures[0].Stage.Index);
        Assert.Equal(SlotsOf(va), SlotsOf(vb));
    }

    [Fact]
    public void AnotherStructuresVisibility_MovesNothing()
    {
        WorldMorphology m = Morph();
        StructureReport Hidden(ReportedVisibility v) => StructureOf("h0", "hospital", "s", 0, 1, v);
        StructureReport[] rest = Enumerable.Range(1, 5).Select(i => StructureOf($"u{i}", "university", "s", i)).ToArray();
        WorldView shown = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], [Hidden(ReportedVisibility.Visible), .. rest]), m);
        WorldView hidden = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], [Hidden(ReportedVisibility.Hidden), .. rest]), m);
        foreach (StructureReport r in rest)
            Assert.Equal(shown.FindStructure(Structure(r.Key))!.Slot, hidden.FindStructure(Structure(r.Key))!.Slot);
        Assert.False(hidden.FindStructure(Structure("h0"))!.Drawable);
    }

    [Fact]
    public void CrossingTheAggregationLimit_WithALaterReport_MovesNoIndividual()
    {
        WorldMorphology m = Morph();
        int n = m.Aggregation.IndividualUpTo;
        StructureReport[] first = Enumerable.Range(0, n).Select(i => StructureOf($"u{i}", "university", "s", i)).ToArray();
        WorldView before = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], first), m);
        WorldView after = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], [.. first, StructureOf("u9", "university", "s", 99)]), m);
        foreach (StructureReport r in first) Assert.Equal(before.FindStructure(Structure(r.Key))!.Slot, after.FindStructure(Structure(r.Key))!.Slot);
        Assert.Single(after.Clusters);
        Assert.Null(after.FindStructure(Structure("u9"))!.Slot);
    }

    [Fact]
    public void Blocks_AreHiddenWhereTheyStand_NeverMoved()
    {
        WorldMorphology m = Morph();
        WorldView bare = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0, ("population", 50000))]), m);
        WorldView built = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0, ("population", 50000))],
            Enumerable.Range(0, 5).Select(i => StructureOf($"u{i}", "university", "s", i)).ToArray()), m);
        HashSet<LotGeometry> all = CompositionGeometry.BlockCentres(m.Layout, "s").ToHashSet();
        // Every drawn block is a lattice lot at its fixed position, and the count is the stage's.
        Assert.All(built.Settlements[0].Blocks, b => Assert.Contains(b, all));
        Assert.Equal(bare.Settlements[0].Blocks.Count, built.Settlements[0].Blocks.Count);
        // No drawn block lies under an occupied slot.
        var occupied = built.Structures.Where(s => s.Slot is not null).Select(s => s.Slot!.Value).Concat(built.Clusters.Select(k => k.Slot)).ToList();
        Assert.Equal(5, occupied.Count);   // four individual + one cluster token
        foreach (LotGeometry o in occupied)
            foreach (LotGeometry b in built.Settlements[0].Blocks)
                Assert.False(CompositionGeometry.BoxesOverlap(b.X, b.Y, m.Layout.BlockSize, o.X, o.Y, m.Layout.SlotSize));
    }

    [Fact]
    public void FillStep_IsCoprimeWithTheRing()
    {
        for (int n = 1; n <= 64; n++)
        {
            int s = Composition.FillStep(n);
            var seen = new HashSet<int>();
            for (int j = 0; j < n; j++) seen.Add((int)((long)j * s % n));
            Assert.Equal(n, seen.Count);
        }
    }

    /// <summary>Across consecutive demo steps, every structure present in both keeps a
    /// bit-identical slot (the demo derives Established from the first step, so arrivals rank
    /// after what already stands).</summary>
    [Fact]
    public void DemoTimeline_NoStructureEverJumps()
    {
        WorldMorphology m = Morph();
        var demo = Demo(m);
        Dictionary<string, LotGeometry?>? prev = null;
        for (int s = 0; s < demo.StepCount; s++)
        {
            demo.SetStep(s);
            WorldView v = WorldViewBuilder.Build(demo.Sources(), m);
            Dictionary<string, LotGeometry?> now = SlotsOf(v);
            if (prev is not null)
                foreach ((string key, LotGeometry? slot) in prev)
                    if (slot is not null && now.TryGetValue(key, out LotGeometry? n) && n is not null) Assert.Equal(slot, n);
            prev = now;
        }
    }
}
