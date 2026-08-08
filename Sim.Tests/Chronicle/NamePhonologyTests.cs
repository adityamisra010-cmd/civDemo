using Sim.Core.Chronicle;

namespace Sim.Tests.Chronicle;

/// <summary>
/// T4.1c — NAME LEGIBILITY. The chronicle is the instrument the director reads
/// most; a name he cannot hold in his head degrades the project's primary
/// defect-finding tool.
///
/// THE TWO WORST CASES, DECOMPOSED AGAINST THE SHIPPED POOLS (measured, not
/// assumed — and one correction to the reported diagnosis):
///   Thiathiariath = th+ia | th+ia | r+ia+th   onset "th" TWICE, nucleus "ia" THRICE
///   Naethaehun    = n+ae  | th+ae | h+u+n     nucleus "ae" TWICE
/// The reported reading was "onset thi drawn twice"; "thi" is not an onset in
/// the pool — onsets are single "th" — and the repeated element in that name is
/// primarily the NUCLEUS. Both names are still repetition, so the fix shape
/// holds, but the detail differs (§7.12).
///
/// The final-coda-only rule and its comment are UNTOUCHED — a prior fix for the
/// adjacent mid-word-cluster problem.
/// </summary>
public class NamePhonologyTests
{
    private static PhonologyConfig Pools()
    {
        using Stream s = Sim.Data.DataFiles.OpenChronicle();
        return ChronicleConfigLoader.Load(s).Phonology;
    }

    [Fact]
    public void NoOnsetOrNucleusRepeatsWithinAName()
    {
        // THE GUARD, red-proven against the pre-T4.1c generator — see
        // docs/t4.1c-review-record.md for the failing counts.
        PhonologyConfig p = Pools();
        int n = 0;
        for (ulong seed = 1; seed <= 40; seed++)
            for (int id = 0; id < 50; id++)
                for (int salt = 0; salt < 4; salt++)
                { AssertNoRepeats(p, NameRegistry.Generate(p, seed, id, salt)); n++; }
        Assert.Equal(8000, n);
    }

    [Fact]
    public void TheTwoWorstNamesAreNowUnreachable()
    {
        PhonologyConfig p = Pools();
        for (ulong seed = 1; seed <= 60; seed++)
            for (int id = 0; id < 60; id++)
                for (int salt = 0; salt < 4; salt++)
                {
                    string nm = NameRegistry.Generate(p, seed, id, salt);
                    Assert.NotEqual("Thiathiariath", nm);
                    Assert.NotEqual("Naethaehun", nm);
                }

        // Structural, and the reason this holds for EVERY seed rather than only
        // the swept ones: the vowel clusters that produced both names are gone
        // from the pools. Sumerian has no diphthongs.
        Assert.DoesNotContain("ae", p.Nuclei);
        Assert.DoesNotContain("ia", p.Nuclei);
        Assert.DoesNotContain("ou", p.Nuclei);
    }

    [Fact]
    public void SameSeedIdSalt_AlwaysGivesTheSameName()
    {
        // Law 5: constraints applied WITHIN the splitmix64 stream by re-draw,
        // never by a filter carrying state across settlements.
        PhonologyConfig p = Pools();
        for (ulong seed = 7; seed <= 9; seed++)
            for (int id = 0; id < 12; id++)
                Assert.Equal(NameRegistry.Generate(p, seed, id, 0),
                             NameRegistry.Generate(p, seed, id, 0));
    }

    private static void AssertNoRepeats(PhonologyConfig p, string name)
    {
        List<(string O, string N, string C)>? d = Decompose(p, name);
        Assert.True(d is not null, $"'{name}' does not decompose against the pools");
        string[] on = d!.Select(x => x.O).ToArray();
        string[] nu = d.Select(x => x.N).ToArray();
        Assert.Equal(on.Length, on.Distinct().Count());
        Assert.Equal(nu.Length, nu.Distinct().Count());
    }

    /// <summary>Parses a rendered name back into syllables, longest-first so
    /// "sh" wins over "s" — the same ambiguity the pools contain.</summary>
    internal static List<(string O, string N, string C)>? Decompose(PhonologyConfig p, string name)
    {
        string t = name.ToLowerInvariant();
        string[] ons = [.. p.Onsets.OrderByDescending(x => x.Length)];
        string[] nuc = [.. p.Nuclei.OrderByDescending(x => x.Length)];
        string[] cod = [.. p.Codas.Where(c => c.Length > 0).OrderByDescending(x => x.Length)];

        List<(string, string, string)>? Rec(int i, List<(string, string, string)> acc)
        {
            if (i == t.Length) return acc.Count is >= 2 and <= 3 ? acc : null;
            foreach (string o in ons)
            {
                if (!t.AsSpan(i).StartsWith(o)) continue;
                foreach (string nn in nuc)
                {
                    if (!t.AsSpan(i + o.Length).StartsWith(nn)) continue;
                    int j = i + o.Length + nn.Length;
                    var r = Rec(j, [.. acc, (o, nn, "")]);
                    if (r is not null) return r;
                    foreach (string c in cod)
                        if (j + c.Length == t.Length && t.AsSpan(j).StartsWith(c)
                            && acc.Count + 1 is >= 2 and <= 3)
                            return [.. acc, (o, nn, c)];
                }
            }
            return null;
        }
        return Rec(0, []);
    }
}
