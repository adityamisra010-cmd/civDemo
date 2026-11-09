using System.Reflection;

namespace Proto.M7.Tests.LaneD;

/// <summary>
/// [D, verification round 1, D-R1-07] A populated-table round trip proves the field ORDER of an encoder only for fields whose
/// values differ in some row: if two same-typed fields are equal in every row (both zero, say), an encoder that writes them in
/// each other's slot round-trips unseen (verifier mutants V12 and V13 survived for that reason). This check makes a populated
/// table say so: for every pair of public same-typed scalar properties of the row type, some row must tell the two apart.
/// Test-only reflection (the order of the properties does not matter: the check is over all pairs).
/// </summary>
internal static class RowFieldCoverage
{
    private static readonly Type[] Scalars = { typeof(long), typeof(int), typeof(byte), typeof(short), typeof(bool) };

    public static void AssertEverySameTypedFieldPairDiffers<TRow>(IReadOnlyList<TRow> rows)
    {
        var props = new List<PropertyInfo>();
        foreach (var p in typeof(TRow).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.GetIndexParameters().Length == 0 && Array.IndexOf(Scalars, p.PropertyType) >= 0) props.Add(p);
        var missing = new List<string>();
        for (int i = 0; i < props.Count; i++)
            for (int j = i + 1; j < props.Count; j++)
            {
                if (props[i].PropertyType != props[j].PropertyType) continue;
                bool told = false;
                foreach (var r in rows)
                    if (!Equals(props[i].GetValue(r), props[j].GetValue(r))) { told = true; break; }
                if (!told) missing.Add(props[i].Name + "/" + props[j].Name);
            }
        missing.Sort(StringComparer.Ordinal);
        Assert.True(missing.Count == 0,
            typeof(TRow).Name + ": these same-typed field pairs are equal in every row, so a swap of the two in the encoder would round-trip: "
            + string.Join(", ", missing));
    }
}
