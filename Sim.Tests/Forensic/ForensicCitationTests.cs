using System.Text.RegularExpressions;
using Sim.Core.Observability.Forensic;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Forensic;

/// <summary>
/// ADR-033 D4 — THE HAPPINESS BOUNDARY'S file:line CITATIONS ARE CHECKED, NOT TRUSTED.
///
/// <see cref="ForensicSchema.HappinessDecompositionWhy"/> states the boundary "as file:line so it can
/// be checked rather than believed", and InspectionTests pins one of those citations as a literal.
/// A literal pin cannot see the SOURCE move: the M5 port inserted TaxSufficiency into
/// SettlementHappiness.cs, every cited line below it shifted, and the old citation (:234) would have
/// stayed green while pointing at the wrong line. This test resolves every "Name (:N)" and
/// "(:A-B)" citation against the file as it stands, so the next insertion fails here instead.
/// </summary>
public class ForensicCitationTests
{
    private static string[] SettlementHappinessSource() =>
        File.ReadAllLines(Path.Combine(RepoPaths.Root(), "Sim.Core", "State", "SettlementHappiness.cs"));

    /// <summary>The 1-based line <paramref name="n"/>, or a failure naming it.</summary>
    private static string Line(string[] source, int n)
    {
        Assert.InRange(n, 1, source.Length);
        return source[n - 1];
    }

    private static int Cited(string text, string pattern)
    {
        Match m = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
        Assert.True(m.Success, $"citation not found in HappinessDecompositionWhy: {pattern}");
        return int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void EveryHappinessBoundaryCitation_PointsAtTheSymbolItNames()
    {
        string why = ForensicSchema.HappinessDecompositionWhy;
        string[] src = SettlementHappinessSource();

        int weightOf = Cited(why, @"WeightOf is `private static` \(:(\d+)\)");
        Assert.Contains("private static double WeightOf(", Line(src, weightOf), StringComparison.Ordinal);

        int sustenance = Cited(why, @"SustenanceNeedId` \(:(\d+)\)");
        Assert.Contains("private const int SustenanceNeedId", Line(src, sustenance), StringComparison.Ordinal);

        int shelter = Cited(why, @"ShelterNeedId` \(:(\d+)\)");
        Assert.Contains("private const int ShelterNeedId", Line(src, shelter), StringComparison.Ordinal);

        // "inside Of (:A-B)": A is Of's signature line, B its closing brace.
        Match of = Regex.Match(why, @"inside Of \(:(\d+)-(\d+)\)", RegexOptions.CultureInvariant);
        Assert.True(of.Success, "the Of range citation is missing");
        int ofStart = int.Parse(of.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        int ofEnd = int.Parse(of.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("public static double Of(", Line(src, ofStart), StringComparison.Ordinal);
        Assert.Equal("    }", Line(src, ofEnd));
        // The floor, the span and the normalisation really are inside the cited range.
        bool floor = false, span = false, normalized = false, burden = false;
        for (int n = ofStart; n <= ofEnd; n++)
        {
            string l = Line(src, n);
            floor |= l.Contains("double floor =", StringComparison.Ordinal);
            span |= l.Contains("double span =", StringComparison.Ordinal);
            normalized |= l.Contains("double normalized =", StringComparison.Ordinal);
            burden |= l.Contains("TaxSufficiency(world, settlement, cfg)", StringComparison.Ordinal);
        }
        Assert.True(floor && span && normalized, "the floor/span/normalisation locals are not inside the cited Of range");
        Assert.True(burden, "Of does not apply the tax burden inside the cited range");

        // "TaxSufficiency (:A-B)": the public multiplier's signature and body.
        Match tax = Regex.Match(why, @"TaxSufficiency \(:(\d+)-(\d+)\)", RegexOptions.CultureInvariant);
        Assert.True(tax.Success, "the TaxSufficiency citation is missing");
        Assert.Contains("public static double TaxSufficiency(",
            Line(src, int.Parse(tax.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)), StringComparison.Ordinal);
        Assert.Contains("Governance.EffectiveTaxRate",
            Line(src, int.Parse(tax.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)), StringComparison.Ordinal);
    }
}
