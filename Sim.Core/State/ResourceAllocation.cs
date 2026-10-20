using Sim.Core.Kernel;
using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// One good's BUILDING/ROADS SPLIT for one Empire in one step (D-054 WS2; <see cref="ResourceAllocation.Split"/>):
/// <c>Supply</c> S (the Empire purse's holding when building work starts), building work's guaranteed share
/// <c>BuildingShare</c> E_b and road works' <c>RoadShare</c> E_r = S − E_b, the two DEMANDS (<c>BuildingDemand</c> D_b —
/// what the Empire's queue heads would put in with no split; <c>RoadDemand</c> D_r — what its delivered road order would
/// be charged), the most building work may take <c>BuildingCap</c> = E_b + max(0, E_r − D_r), and its GRANT
/// <c>BuildingGrant</c> G_b = min(D_b, cap). Every figure is an exact long.
/// </summary>
public readonly record struct AllocationSplit(
    long Supply, long BuildingShare, long RoadShare, long BuildingDemand, long RoadDemand, long BuildingCap, long BuildingGrant)
{
    /// <summary>What road works are certain to find after building work: min(D_r, E_r) (availability; the road
    /// system's own min-ratio may still draw less of one good when another is short).</summary>
    public long RoadGuarantee => Math.Min(RoadDemand, RoadShare);

    /// <summary>Whether the split holds anything back from building work this step: its grant is below its demand.</summary>
    public bool HoldsBack => BuildingGrant < BuildingDemand;
}

/// <summary>
/// D-054 WS2 — EDITABLE RESOURCE-ALLOCATION RATIOS (the Director's 2026-10-14 instruction, workstream 2): THE RULE, as
/// pure functions (no state, no cache), read by ConstructionSystem (which applies it), ConstructionQuery.HeadStatus and
/// ResourceAllocationQuery (the UI's estimates).
///
/// <list type="number">
/// <item><b>The setting.</b> Each Empire's standing BuildingsPercent b, a whole number in [0, 100]; road works get
///   100 − b (ResourceAllocationRow, upserted by order kind 12 SetResourceAllocation, the last valid order of a step
///   winning). NO ROW MEANS b = 100 — "building first: road works get only what building work leaves", the batch-2
///   behaviour, and what every AI Empire keeps (the AI never sets it).</item>
/// <item><b>Eligible uses.</b> The two that draw the Empire's purse BY ORDER: building projects (the construction queue)
///   and road works (DevelopRoads). Housing is NOT eligible: dwellings are needs-driven upkeep drawn from the settlement's
///   OWN timber (and clay) before construction, never ordered and never from the purse. <b>Eligible goods</b> are the
///   union of every goods.json project input and every roads.classes material (<see cref="EligibleGoods"/>) — from
///   content, never a code list.</item>
/// <item><b>Supply and demand, per good, when building work starts</b> (after production, trade and housing): S, what
///   the Empire's settlements hold; D_b, what its queue heads would put in this step with no split (builders and the
///   materials-first lockstep, oldest order first); D_r, what its DELIVERED road order would be charged against S by
///   road development's own arithmetic (RoadDevelopmentQuery.SimulateCharges) — 0 with no road order.</item>
/// <item><b>The split</b> (<see cref="Split"/>): E_b = floor(S × b / 100), computed exactly as (S / 100) × b +
///   (S % 100) × b / 100 (no overflow for any long S); E_r = S − E_b; building work may take
///   G_b = min(D_b, E_b + max(0, E_r − D_r)) — the road share road works do not need flows to buildings — and road works,
///   later in the step, draw from what physically remains — so building share that building work did not use flows to
///   roads. Road works can therefore always find at least min(D_r, E_r).</item>
/// <item><b>Semantics against demand.</b> The percentages are GUARANTEED SHARES OF SUPPLY, never amounts consumed: a use
///   draws only what its own rule needs, nothing moves except through the Ledger draws of the two systems, and no stock
///   goes negative. With no road order D_r = 0 and building work may use everything; b = 100 never binds
///   (G_b = min(D_b, S)), so it is skipped and is byte-identical to the switch off.</item>
/// <item><b>The starvation guard: a road order that can pay nothing withholds nothing</b> — neither one that can pay
///   nothing from the whole supply (D_r = 0), nor one that, on what building work would leave it under the split, road
///   development would charge nothing at all (ADR-032's min-ratio can floor a partial step to nothing: two tools needed
///   and a quarter of the timber left pays neither). Then the split is taken with D_r = 0 (G_b = min(D_b, S)): building
///   work takes what it needs and road works, finding no more than before, still pay nothing — so no unit is ever held
///   idle for a road that cannot use it (ConstructionQuery.AllocationFor). A road order that can pay SOME of its share may
///   still use less than the share holds — the guarantee is availability, min(D_r, E_r), not consumption.</item>
/// </list>
/// The switch is sim.json construction.allocationRatios; the rule is inert unless incremental construction and the
/// Empire purse are on as well (<see cref="Enabled"/>; the loader refuses the switch without them).
/// </summary>
public static class ResourceAllocation
{
    /// <summary>The building share an Empire with no ResourceAllocationRow has: 100, building first.</summary>
    public const int DefaultBuildingsPercent = 100;

    /// <summary>Whether the building/roads split applies: sim.json construction.allocationRatios, with incremental
    /// construction and the Empire purse both on (any other combination built in code leaves it inert).</summary>
    public static bool Enabled(SimConfig cfg) =>
        cfg.Construction is { AllocationRatios: true, Incremental: true, EmpireStockpile: true };

    /// <summary>E_b — building work's guaranteed share of <paramref name="supply"/> at <paramref name="buildingsPercent"/>:
    /// floor(S × b / 100), exact for every S ≥ 0 and b in [0, 100] ((S / 100) × b ≤ S and (S % 100) × b ≤ 9,900).</summary>
    public static long BuildingShare(long supply, int buildingsPercent)
    {
        if (buildingsPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(buildingsPercent), buildingsPercent, "a building share is in [0, 100].");
        if (supply <= 0) return 0;
        return supply / 100 * buildingsPercent + supply % 100 * buildingsPercent / 100;
    }

    /// <summary>
    /// THE SPLIT of one good (the header): S = <paramref name="supply"/> (a negative holding counts as 0), D_b =
    /// <paramref name="buildingDemand"/>, D_r = <paramref name="roadDemand"/> (both ≥ 0), b =
    /// <paramref name="buildingsPercent"/>. E_b = floor(S × b / 100), E_r = S − E_b, cap = E_b + max(0, E_r − D_r) (≤ S),
    /// G_b = min(D_b, cap).
    /// </summary>
    public static AllocationSplit Split(long supply, long buildingDemand, long roadDemand, int buildingsPercent)
    {
        if (buildingDemand < 0) throw new ArgumentOutOfRangeException(nameof(buildingDemand), buildingDemand, "a demand is never negative.");
        if (roadDemand < 0) throw new ArgumentOutOfRangeException(nameof(roadDemand), roadDemand, "a demand is never negative.");
        long s = Math.Max(0, supply);
        long eb = BuildingShare(s, buildingsPercent);
        long er = s - eb;
        long cap = eb + Math.Max(0, er - roadDemand);
        return new AllocationSplit(s, eb, er, buildingDemand, roadDemand, cap, Math.Min(buildingDemand, cap));
    }

    /// <summary>The index of <paramref name="polity"/>'s row in <paramref name="rows"/>; −1 when it has none.</summary>
    public static int IndexOf(IReadOnlyTable<ResourceAllocationRow> rows, PolityId polity)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].Polity.Value == polity.Value) return i;
        return -1;
    }

    /// <summary>The Empire's EFFECTIVE building share: its row, or <see cref="DefaultBuildingsPercent"/> (100) when it has
    /// none.</summary>
    public static int EffectivePercent(IReadOnlyTable<ResourceAllocationRow> rows, PolityId polity)
    {
        int i = IndexOf(rows, polity);
        return i < 0 ? DefaultBuildingsPercent : rows[i].BuildingsPercent;
    }

    /// <summary>The Empire's effective building share in <paramref name="world"/>'s own table.</summary>
    public static int EffectivePercent(IReadOnlyWorldState world, PolityId polity) =>
        EffectivePercent(world.ResourceAllocations, polity);

    /// <summary>UPSERT (order kind 12): the Empire's row takes <paramref name="buildingsPercent"/> in place, or a new row
    /// is APPENDED — so rows stay in the order their Empires first set a split and the serialized stream never depends on
    /// anything else. An explicit 100 is stored as a row (the same rule as no row).</summary>
    public static void Upsert(Table<ResourceAllocationRow> rows, PolityId polity, int buildingsPercent)
    {
        var row = new ResourceAllocationRow(polity, buildingsPercent);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Polity.Value != polity.Value) continue;
            rows[i] = row;
            return;
        }
        rows.Add(row);
    }

    /// <summary>
    /// THE v33 SPLIT INVARIANT (ResourceAllocationRow), one statement for its two checkers — ConstructionSystem at the end of
    /// every step (a bug: it throws) and Snapshot.Load (a save no rule produces: SnapshotFormatException): every
    /// BuildingsPercent is a whole share in [0, 100] (the order's payload rule; a share outside it has no meaning and the
    /// split's arithmetic refuses it), and no Empire has two rows (an upsert keeps one; a second would make the split depend
    /// on table position). Null when it holds; otherwise what breaks it.
    /// </summary>
    public static string? InvariantViolation(IReadOnlyTable<ResourceAllocationRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            ResourceAllocationRow r = rows[i];
            if (r.BuildingsPercent is < 0 or > 100)
                return "the building/roads split of polity " + Inv(r.Polity.Value) + " is " + Inv(r.BuildingsPercent)
                    + ", outside [0, 100]";
            for (int j = i + 1; j < rows.Count; j++)
                if (rows[j].Polity.Value == r.Polity.Value)
                    return "two building/roads split rows for polity " + Inv(r.Polity.Value);
        }
        return null;
    }

    private static string Inv(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The ELIGIBLE GOODS (the header): <c>[g]</c> true iff good id g is an input of any goods.json project or a
    /// material of any roads.classes entry; indexed by good id (length max id + 1). Empty without goods content.</summary>
    public static bool[] EligibleGoods(SimConfig cfg)
    {
        if (cfg.Goods is not { } goods) return [];
        int max = -1;
        for (int i = 0; i < goods.Goods.Length; i++) if (goods.Goods[i].Id > max) max = goods.Goods[i].Id;
        var eligible = new bool[max + 1];
        foreach (ConstructionProjectEntry p in goods.Projects ?? [])
            foreach (ProjectInput input in p.Inputs) Mark(eligible, goods.IdOf(input.Good));
        if (cfg.Roads is { } roads)
            foreach (RoadClassConfig c in roads.Classes)
                foreach (RoadMaterialConfig m in c.MaterialsPerKm) Mark(eligible, goods.IdOf(m.Good));
        return eligible;
    }

    /// <summary>The eligible goods' ids, ascending.</summary>
    public static int[] EligibleGoodIds(SimConfig cfg)
    {
        bool[] eligible = EligibleGoods(cfg);
        var ids = new List<int>();
        for (int g = 0; g < eligible.Length; g++) if (eligible[g]) ids.Add(g);
        return [.. ids];
    }

    /// <summary>The order the player's Set issues: stamped with the CURRENT turn, TargetId = the issuing Empire,
    /// Amount = the building share (kind 12; the SetTaxRate convention).</summary>
    public static OrderRecord Order(IReadOnlyWorldState world, PolityId polity, int buildingsPercent) =>
        OrderRecord.From(world.Clock.Turn, polity, OrderKind.SetResourceAllocation, polity.Value, buildingsPercent);

    private static void Mark(bool[] eligible, int id)
    {
        if (id >= 0 && id < eligible.Length) eligible[id] = true;
    }
}
