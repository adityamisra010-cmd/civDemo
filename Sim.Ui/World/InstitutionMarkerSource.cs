using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

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
/// <para><b>Implemented over the real table (ADR-033 D6, schema v31).</b> A settlement HAS an institution
/// when an <see cref="InstitutionRow"/> founded there exists — founded, not merely available, queued or
/// built-but-not-yet-founded (InstitutionsSystem founds a completed university building the step after it
/// stands). The rows are grouped by type key, ascending, and counted; names come from research.json
/// <c>universityTypes</c>. The polity-level <c>ResearchCostModifiers</c> rows are research cost EFFECTS
/// (ADR-029 §9), not institutions, and are still never drawn as universities. A university building is
/// drawn ONCE, here — the lens does not also draw its <c>Structures</c> count as a generic structure.</para>
///
/// <para>Pure, read-only, deterministic order (ascending key; table scan, no dictionary).</para>
/// </summary>
public static class InstitutionMarkerSource
{
    /// <summary>The institutions <paramref name="settlement"/> has, one entry per type, ascending
    /// <see cref="InstitutionView.TypeKey"/>, counted.</summary>
    public static IReadOnlyList<InstitutionView> InstitutionsAt(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        if (world.Institutions.Count == 0) return [];
        var keys = new List<int>();
        var counts = new List<long>();
        for (int i = 0; i < world.Institutions.Count; i++)
        {
            InstitutionRow row = world.Institutions[i];
            if (row.Settlement != settlement) continue;
            int at = keys.IndexOf(row.Type);
            if (at < 0) { keys.Add(row.Type); counts.Add(1); }
            else counts[at]++;
        }
        if (keys.Count == 0) return [];
        var views = new InstitutionView[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            UniversityType? type = InstitutionContent.TypeOf(cfg.Research, keys[i]);
            views[i] = new InstitutionView(keys[i], type?.Name ?? "Institution", counts[i]);
        }
        Array.Sort(views, static (a, b) => a.TypeKey.CompareTo(b.TypeKey));   // keys are unique
        return views;
    }
}
