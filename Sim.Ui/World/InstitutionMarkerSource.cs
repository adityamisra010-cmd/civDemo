using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.World;

/// <summary>
/// One institution TYPE a settlement HAS, aggregated — never one building (D-047 Part 4 G; districts
/// are abstracted, D-038 H2: an institution is a capability the settlement has, not an object at
/// coordinates inside it). <see cref="TypeKey"/> selects the hall glyph: for the five specialized
/// universities (D-047 ruling 9; the only institutions ADR-033 D6 founds) it is the research.json
/// <c>universityTypes[].key</c> (1 military … 5 agricultural), which draws that type's emblem; any other
/// key draws the generic hall. <see cref="Count"/> is how many of that type the settlement has.
/// </summary>
public sealed record InstitutionView(int TypeKey, string Name, long Count);

/// <summary>
/// THE INSTITUTION SEAM — the ONE function through which the world lens reads institution state
/// (D-038 H5: a settlement shows a university because it HAS one; ADR-033 Appendix A, stream U3).
///
/// <para><b>Today it returns nothing, because no institution state exists on this branch.</b> The
/// institution table and system are ADR-033 D6 (schema v31), a later stream. The lens therefore shows no
/// institution it cannot read from state: the polity-level <c>ResearchCostModifiers</c> rows are research
/// cost EFFECTS (ADR-029 §9), not institutions, and are never drawn as universities.</para>
///
/// <para><b>How the institutions stream plugs in:</b> implement <see cref="InstitutionsAt"/> over its
/// table — the settlement's institution rows in a state the settlement HAS (founded, not merely
/// available), grouped by type key, ascending, counted — and change nothing else: the projection calls
/// it once per settlement and the lens draws exactly what it returns (aggregated glyphs embedded in the
/// settlement footprint, named with counts at Settlement zoom). Pure, read-only, deterministic order.</para>
/// </summary>
public static class InstitutionMarkerSource
{
    /// <summary>The institutions <paramref name="settlement"/> has, one entry per type, ascending
    /// <see cref="InstitutionView.TypeKey"/>. No institution state exists yet: none.</summary>
    public static IReadOnlyList<InstitutionView> InstitutionsAt(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement) => [];
}
