namespace Sim.Core.State;

/// <summary>What <see cref="UnitFamilyMigration.Remap"/> did to one (Family, Identity) pair.</summary>
public enum UnitFamilyRemap
{
    /// <summary>Not a rehomed identity, or already under its new family: kept as it is.</summary>
    Unchanged = 0,
    /// <summary>A rehomed identity under its old family: moved to the new family (the key is unchanged).</summary>
    Remapped = 1,
    /// <summary>A rehomed identity under a family that is neither its old nor its new one: no rule maps it.</summary>
    Refused = 2,
}

/// <summary>
/// ADR-055 (unit charter §5.3; Q2 resolved, §16.4) — THE SAVE MIGRATION FOR THE TAXONOMY SPLITS. Five identities moved family
/// when the M7 taxonomy (twenty families) landed; their KEYS did not change (keys are append-only, never reused). A MilitaryUnits row
/// written under the 12-family taxonomy (any stream older than schema v39) is rewritten at load by this PURE, DETERMINISTIC
/// map of (Family, Identity) — no content is read, so the rule cannot drift with a later content edit (a test pins the map
/// against unit-families.json):
/// <list type="bullet">
/// <item>101 scouts: light_infantry (1) → reconnaissance (22)</item>
/// <item>907 nuclear submarines: naval_combat (9) → submarines (18)</item>
/// <item>908 aircraft carriers: naval_combat (9) → carriers (19)</item>
/// <item>1102 helicopters: air (11) → rotary_wing (20)</item>
/// <item>1104 combat drones: air (11) → unmanned_aircraft (21)</item>
/// </list>
/// A row naming a rehomed identity under any OTHER family is refused (no rule maps it — a corrupt or foreign save), never
/// guessed at. Every other row is untouched, so an unknown identity is preserved (ADR-031 rule 5). The founding warband
/// (301, heavy_infantry) is never rehomed; no shipped save holds a rehomed row (measured, ADR-055 §5), so the migration is a
/// no-op on every existing save.
/// </summary>
public static class UnitFamilyMigration
{
    /// <summary>The map, in identity-key order: (identity, old family, new family).</summary>
    public static readonly IReadOnlyList<(int Identity, int OldFamily, int NewFamily)> Map =
    [
        (101, 1, 22),
        (907, 9, 18),
        (908, 9, 19),
        (1102, 11, 20),
        (1104, 11, 21),
    ];

    /// <summary>The migrated family of (<paramref name="family"/>, <paramref name="identity"/>).</summary>
    public static UnitFamilyRemap Remap(int family, int identity, out int newFamily)
    {
        newFamily = family;
        for (int i = 0; i < Map.Count; i++)
        {
            if (Map[i].Identity != identity) continue;
            if (family == Map[i].NewFamily) return UnitFamilyRemap.Unchanged;
            if (family != Map[i].OldFamily) return UnitFamilyRemap.Refused;
            newFamily = Map[i].NewFamily;
            return UnitFamilyRemap.Remapped;
        }
        return UnitFamilyRemap.Unchanged;
    }

    /// <summary>Rewrites every rehomed row of <paramref name="units"/> in place, in table order, and returns how many
    /// changed. Throws <see cref="InvalidDataException"/> (Snapshot.Load's corrupt-save diagnostic) on a refused pair, before
    /// any row is rewritten.</summary>
    public static int Apply(Table<MilitaryUnitRow> units)
    {
        for (int i = 0; i < units.Count; i++)
            if (Remap(units[i].Family, units[i].Identity, out _) == UnitFamilyRemap.Refused)
                throw new InvalidDataException(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"military unit {units[i].Id} names identity {units[i].Identity} under family {units[i].Family}; that identity was rehomed and no rule maps it from that family (ADR-055 unit-family migration)."));
        int changed = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (Remap(units[i].Family, units[i].Identity, out int to) != UnitFamilyRemap.Remapped) continue;
            units[i] = units[i] with { Family = to };
            changed++;
        }
        return changed;
    }
}
