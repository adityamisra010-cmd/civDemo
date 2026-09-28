using Sim.Ui.World.Content;

namespace Sim.Ui.World.View;

/// <summary>
/// DETERMINISTIC COMPOSITION of a settlement's sprite (docs/architecture/world-visualization.md §6).
///
/// A structure is a capability of its settlement (D-038 H2), so it has no world position;
/// what is assigned here is a SLOT in the settlement's composed sprite (H3/H4), in sprite
/// units, used only to draw. The rule:
/// <list type="number">
/// <item>Claims are ordered by (established ascending — unreported last —, key ordinal).</item>
/// <item>Each claim's preferred lot on every ring is StableHash(settlementKey, claimKey) mod
/// the ring's lot count; rings are tried inner to outer, and on each ring lots are probed
/// from the preferred one upward (wrapping). At most every lot is probed once: bounded.</item>
/// <item>If every slot is taken, the claim goes to a fixed spill position outside the
/// lattice, in spill order — never a loop, never an overlap with a slot.</item>
/// </list>
/// Consequences, pinned by tests: identical reports give identical slots in any process;
/// input order is irrelevant; a later-established arrival never moves an earlier one; a
/// stage change, a visibility change or a zoom never moves anything (none is an input).
/// The one accepted relocation is a REMOVAL: a later claim whose probe passed the freed slot
/// may move into it — documented, and tested to move only those claims.
/// </summary>
public static class Composition
{
    /// <summary>One thing that wants a slot: the key its preferred lot hashes from, and its priority.</summary>
    public readonly record struct Claim(string HashKey, long? Established, string OrderKey);

    public static int[] PriorityOrder(IReadOnlyList<Claim> claims)
    {
        int[] order = new int[claims.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            long ea = claims[a].Established ?? long.MaxValue, eb = claims[b].Established ?? long.MaxValue;
            int c = ea.CompareTo(eb);
            if (c != 0) return c;
            c = string.CompareOrdinal(claims[a].OrderKey, claims[b].OrderKey);
            return c != 0 ? c : string.CompareOrdinal(claims[a].HashKey, claims[b].HashKey);
        });
        return order;
    }

    /// <summary>The slot of every claim, index-aligned with <paramref name="claims"/>.</summary>
    public static LotGeometry[] AssignSlots(string settlementKey, IReadOnlyList<Claim> claims, CompositionLayout layout)
    {
        LotGeometry[] lattice = CompositionGeometry.SlotCentres(layout);
        var ringStart = new int[layout.SlotRings.Count];
        for (int r = 1; r < ringStart.Length; r++) ringStart[r] = ringStart[r - 1] + layout.SlotRings[r - 1].Lots;
        var used = new bool[lattice.Length];
        var result = new LotGeometry[claims.Count];
        int spill = 0;
        foreach (int i in PriorityOrder(claims))
        {
            ulong h = StableHash.Of(settlementKey, claims[i].HashKey);
            int found = -1;
            for (int r = 0; r < layout.SlotRings.Count && found < 0; r++)
            {
                int n = layout.SlotRings[r].Lots;
                int start = StableHash.Index(h, n);
                for (int p = 0; p < n; p++)
                {
                    int idx = ringStart[r] + (start + p) % n;
                    if (!used[idx]) { found = idx; break; }
                }
            }
            if (found >= 0) { used[found] = true; result[i] = lattice[found]; }
            else result[i] = Spill(layout, spill++);
        }
        return result;
    }

    /// <summary>The k-th spill position: fixed rings of 24 outside the slot lattice.</summary>
    public static LotGeometry Spill(CompositionLayout layout, int k)
    {
        const int PerRing = 24;
        double pitch = layout.SlotSize * 1.2;
        double outer = layout.SlotRings.Count > 0 ? layout.SlotRings[^1].Radius : layout.PlazaRadius;
        int ring = k / PerRing, index = k % PerRing;
        double radius = Math.Max(outer, layout.BlockRings.Count > 0 ? layout.BlockRings[^1].Radius : 0) + pitch * (ring + 1);
        double deg = (index + 0.5) * 360.0 / PerRing;
        double a = deg * Math.PI / 180.0;
        return new LotGeometry(layout.SlotRings.Count + ring, index, deg, radius * Math.Sin(a), -radius * Math.Cos(a));
    }

    /// <summary>The fill order step for a ring of n lots: the smallest step ≥ round(0.382·n) that
    /// is coprime with n, so a partly filled ring is filled evenly around, not one side first.</summary>
    public static int FillStep(int n)
    {
        if (n <= 2) return 1;
        int s = Math.Max(1, (int)Math.Round(n * 0.382, MidpointRounding.AwayFromZero));
        while (Gcd(s, n) != 1) s++;
        return s;
    }

    private static int Gcd(int a, int b) { while (b != 0) (a, b) = (b, a % b); return a; }

    /// <summary>
    /// The residential blocks a settlement draws: the first <paramref name="count"/> block lots,
    /// in fixed order (rings inner to outer, each ring in its even fill order), that do not lie
    /// under an occupied slot. A block under a slot is hidden WHERE IT STANDS — nothing is
    /// re-indexed or moved; the next lot in the fixed order is shown instead.
    /// </summary>
    public static LotGeometry[] VisibleBlocks(string settlementKey, int count, IReadOnlyList<LotGeometry> occupied, CompositionLayout layout)
    {
        LotGeometry[] lots = CompositionGeometry.BlockCentres(layout, settlementKey);
        var shown = new List<LotGeometry>(Math.Min(count, lots.Length));
        int ringStart = 0;
        for (int r = 0; r < layout.BlockRings.Count && shown.Count < count; r++)
        {
            int n = layout.BlockRings[r].Lots;
            int step = FillStep(n);
            for (int j = 0; j < n && shown.Count < count; j++)
            {
                LotGeometry lot = lots[ringStart + (int)((long)j * step % n)];
                bool covered = false;
                foreach (LotGeometry s in occupied)
                    if (CompositionGeometry.BoxesOverlap(lot.X, lot.Y, layout.BlockSize, s.X, s.Y, layout.SlotSize)) { covered = true; break; }
                if (!covered) shown.Add(lot);
            }
            ringStart += n;
        }
        return [.. shown];
    }
}
