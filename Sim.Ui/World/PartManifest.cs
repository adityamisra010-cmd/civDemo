using System.Globalization;
using System.Text.Json;

namespace Sim.Ui.World;

/// <summary>
/// M6 E1.2 — THE PART CONVENTIONS of a composed settlement (<c>settlement-classes.json</c> <c>parts</c>; style bible §8):
/// <list type="bullet">
/// <item><see cref="CountStages"/> — D-043 D2's count staging, data-defined: a part of an aggregated count c is drawn at
///   stage i, the last whose threshold c reaches (1 → small, 10 → expanded, 20 → campus, 30 → complex);</item>
/// <item><see cref="MaxParts"/> — D-066 A4 (D-038 H8): the most parts one settlement's composite shows. A settlement holding
///   more kinds shows its <see cref="MaxParts"/> − 1 most significant (<see cref="PartFold.Order"/>) and folds the surplus
///   into ONE overflow part whose number, at high zoom, is the count of the kinds folded.</item>
/// </list>
/// View data only.
/// </summary>
public sealed record PartConventions(IReadOnlyList<long> CountStages, int MaxParts)
{
    /// <summary>The stage names, one per threshold (D-043 D2's university example).</summary>
    public static readonly string[] StageNames = ["small", "expanded", "campus", "complex"];

    /// <summary>The stage of a part of aggregated count <paramref name="count"/>: the last threshold it reaches (0-based);
    /// −1 below the first (nothing is drawn of a count the first stage does not reach). Integer comparison only.</summary>
    public int StageOf(long count)
    {
        int stage = -1;
        for (int i = 0; i < CountStages.Count; i++) if (count >= CountStages[i]) stage = i;
        return stage;
    }

    /// <summary>Parses the <c>parts</c> section; a malformed one throws naming the field.</summary>
    internal static PartConventions Parse(JsonElement parts)
    {
        if (parts.ValueKind != JsonValueKind.Object) throw Bad("parts must be an object");
        if (!parts.TryGetProperty("countStages", out JsonElement st) || st.ValueKind != JsonValueKind.Array) throw Bad("parts.countStages is missing");
        var stages = new List<long>();
        foreach (JsonElement e in st.EnumerateArray()) stages.Add(e.GetInt64());
        if (stages.Count != StageNames.Length) throw Bad("parts.countStages must name " + StageNames.Length.ToString(CultureInfo.InvariantCulture) + " thresholds (small, expanded, campus, complex)");
        if (stages[0] != 1) throw Bad("parts.countStages[0] must be 1 (one of a kind draws the small stage)");
        for (int i = 1; i < stages.Count; i++)
            if (stages[i] <= stages[i - 1]) throw Bad("parts.countStages must ascend strictly");
        if (!parts.TryGetProperty("maxParts", out JsonElement mp)) throw Bad("parts.maxParts is missing");
        int max = mp.GetInt32();
        if (max < 2) throw Bad("parts.maxParts must be at least 2 (one part and the overflow)");
        return new PartConventions(stages, max);
    }

    private static InvalidDataException Bad(string what) => new("settlement-classes.json: " + what);
}

/// <summary>One part a settlement's composite DRAWS: its key (the <see cref="InstitutionMarker"/> key), its aggregated count
/// and whether it is an institution (else a structure kind); <see cref="Index"/> is its position in the settlement's own
/// list (structures, then institutions, the projection's order).</summary>
public readonly record struct ShownPart(string Key, long Count, bool Institution, int Index);

/// <summary>
/// M6 E1.2 — THE OVERFLOW FOLD (D-066 A4; D-038 H8). Pure: which of a settlement's parts its composite draws, and what is
/// folded. SIGNIFICANCE is the integer composite key (institution before structure, count DESC, key ASC — a structure's
/// project id, an institution's type key), unique per kind, so the order is total and independent of any table's order
/// (CLAUDE.md: an ordering is a composite key with a stable integer tie-break; the tie-dense test is
/// <c>ObjectLayerTests.TheFold_KeepsTheMostSignificant_TieDense_WhateverTheOrder</c>).
/// </summary>
public static class PartFold
{
    /// <summary>The settlement's parts in SIGNIFICANCE order (indices into structures-then-institutions).</summary>
    public static List<ShownPart> Order(SettlementLensView s)
    {
        var all = new List<(ShownPart Part, int Key)>();
        int i = 0;
        foreach (StructureView st in s.Structures)
            all.Add((new ShownPart("structure:" + st.ProjectId.ToString(CultureInfo.InvariantCulture), st.Count, false, i++), st.ProjectId));
        foreach (InstitutionView iv in s.Institutions)
            all.Add((new ShownPart("institution:" + iv.TypeKey.ToString(CultureInfo.InvariantCulture), iv.Count, true, i++), iv.TypeKey));
        all.Sort(static (a, b) =>
        {
            int c = b.Part.Institution.CompareTo(a.Part.Institution);   // institutions first
            if (c != 0) return c;
            c = b.Part.Count.CompareTo(a.Part.Count);                     // larger count first
            if (c != 0) return c;
            c = a.Key.CompareTo(b.Key);                                   // the integer key
            return c != 0 ? c : a.Part.Index.CompareTo(b.Part.Index);     // equal kinds (never in a projection): position
        });
        var r = new List<ShownPart>(all.Count);
        foreach ((ShownPart p, int _) in all) r.Add(p);
        return r;
    }

    /// <summary>
    /// The parts the composite draws, in the settlement's own order (structures, then institutions), and the fold: when it
    /// holds at most <see cref="PartConventions.MaxParts"/> kinds, every one is shown and nothing is folded; otherwise the
    /// <c>MaxParts − 1</c> most significant are shown and the rest are folded — <paramref name="foldedKinds"/> kinds of
    /// <paramref name="foldedCount"/> in all (the overflow part's numbers).
    /// </summary>
    public static List<ShownPart> Shown(SettlementLensView s, PartConventions c, out int foldedKinds, out long foldedCount)
    {
        List<ShownPart> order = Order(s);
        foldedKinds = 0;
        foldedCount = 0;
        int keep = order.Count <= c.MaxParts ? order.Count : c.MaxParts - 1;
        var kept = new List<ShownPart>(keep);
        for (int i = 0; i < order.Count; i++)
        {
            if (i < keep) { kept.Add(order[i]); continue; }
            foldedKinds++;
            foldedCount += order[i].Count;
        }
        kept.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        return kept;
    }
}

/// <summary>What a part painter draws for an institution TYPE, chosen from content (<see cref="PartManifest.InstitutionStyleOf"/>).</summary>
public enum InstitutionEmblem { Generic = 0, Blades = 1, Cross = 2, Gear = 3, Orbit = 4, Sheaf = 5, Scales = 6 }

/// <summary>A row of the parts manifest (<see cref="PartManifest.Entries"/>): the part's id, the state that drives it, its
/// painter and its stages.</summary>
public sealed record PartEntry(string Id, string DrivenBy, string Painter, bool Staged, bool Shaded);

/// <summary>
/// M6 E1.1 — THE PARTS MANIFEST: every object part the lens can draw, authored ONCE, procedurally in code (D-038 D1), and
/// composited at draw time from the parts the settlement's state says it has (D-038 H4). The table is the manifest the
/// style bible §8 points to; <c>ObjectLayerTests</c> pins that every entry is drawn by its painter under the one light.
/// INSTITUTIONS ARE DRAWN GENERICALLY FROM CONTENT: a type's emblem is looked up by its key (the five research.json
/// university types) and otherwise by a word of its content NAME (<see cref="NamedEmblems"/>: a law court draws the
/// scales), with the generic hall as the fallback — so a new institution type gets a hall the day its content exists, with
/// no change here.
/// </summary>
public static class PartManifest
{
    public static readonly PartEntry[] Entries =
    [
        new("dwelling", "HousingRow dwellings: one block per dwellingsPerBlock (P12a lattice)", "PartPainters.Dwelling (register roof)", false, true),
        new("structure.storage", "StructureRow of a storage project (granary)", "PartPainters.Structure (silo, dome roof)", true, true),
        new("structure.production", "StructureRow of a production project (workshop)", "PartPainters.Structure (shed and chimney)", true, true),
        new("structure.other", "StructureRow of any other non-founding project", "PartPainters.Structure (pitched building)", true, true),
        new("institution.university", "InstitutionRow of a research.json university type (keys 1-5: emblem by key)", "PartPainters.Institution (pedimented hall)", true, true),
        new("institution.named", "InstitutionRow of another type whose content name carries a known word (court/law: scales)", "PartPainters.Institution (pedimented hall)", true, true),
        new("institution.generic", "InstitutionRow of any other type (fallback)", "PartPainters.Institution (pedimented hall, generic emblem)", true, true),
        new("scaffold", "ConstructionQueueRow at the settlement, or a founding project built and not yet founded", "PartPainters.Scaffold", false, true),
        new("overflow", "more kinds than maxParts (D-066 A4): the folded surplus", "PartPainters.Overflow", false, true),
        new("relief.peak", "terrain elevation (immutable worldgen raster), highest band", "TerrainObjects (peak with a pale cap)", false, true),
        new("relief.mountain", "terrain elevation, upland band", "TerrainObjects (mountain)", false, true),
        new("relief.hill", "terrain elevation, hill band", "TerrainObjects (mound)", false, true),
        new("forest", "terrain moisture, temperature and fertility below the hills", "TerrainObjects (tree crowns)", false, true),
        new("field", "a cultivating settlement's catchment (farming share > 0, cultivation known: LabourActivities.HarvestsWildFood false)", "TerrainObjects (strips)", false, false),
        new("unit.silhouette", "MilitaryUnitRow current identity (family role shape, form variant)", "UnitSilhouette (convex solids on the plate)", false, true),
    ];

    /// <summary>Words of an institution type's content name that choose an emblem when its key is not a university type's
    /// (matched case-insensitively, as whole words or prefixes; the first match in this order wins).</summary>
    public static readonly (string Word, InstitutionEmblem Emblem)[] NamedEmblems =
    [
        ("court", InstitutionEmblem.Scales), ("law", InstitutionEmblem.Scales), ("justice", InstitutionEmblem.Scales),
        ("military", InstitutionEmblem.Blades), ("medic", InstitutionEmblem.Cross), ("engineer", InstitutionEmblem.Gear),
        ("science", InstitutionEmblem.Orbit), ("agricultur", InstitutionEmblem.Sheaf),
    ];

    /// <summary>The emblem an institution type draws: the university types by key (1 military blades, 2 medical cross, 3
    /// engineering gear, 4 natural-science orbit, 5 agricultural sheaf — the halls' emblems since H1); any other key by a
    /// word of its content name (<see cref="NamedEmblems"/>); otherwise the generic emblem.</summary>
    public static InstitutionEmblem InstitutionStyleOf(int typeKey, string name)
    {
        if (typeKey is >= 1 and <= 5) return (InstitutionEmblem)typeKey;
        string lower = (name ?? "").ToLowerInvariant();
        foreach ((string word, InstitutionEmblem e) in NamedEmblems)
            if (ContainsWord(lower, word)) return e;
        return InstitutionEmblem.Generic;
    }

    private static bool ContainsWord(string text, string word)
    {
        for (int at = 0; at + word.Length <= text.Length; at++)
        {
            if (!text.AsSpan(at, word.Length).SequenceEqual(word.AsSpan())) continue;
            if (at == 0 || !char.IsLetter(text[at - 1])) return true;
        }
        return false;
    }
}
