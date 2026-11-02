using System.Text;
using System.Text.RegularExpressions;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Conventions;

/// <summary>
/// D-069 R2 (Director, 2026-11-01; docs/ip-originality.md §2, §3 item 1): SHIPPED CONTENT NAMES NO OTHER GAME. Shipped
/// content is every file under <c>Sim.Data/content/</c> (descriptions, <c>_doc</c> strings and data fields alike) and every
/// player-facing string in <c>Sim.Ui</c>. Design references to other games live in <c>docs/</c>
/// (docs/m7-plan/unit-design-references.md), labelled as reference.
///
/// WHAT IT SCANS:
/// <list type="bullet">
/// <item>every file under <c>Sim.Data/content/</c>, whole text (all keys and values), including
///   <c>research-pre-tranche2.json</c>: it is embedded in Sim.Data and reachable through the CLI's
///   <c>--without-tranche2</c>, so it ships;</item>
/// <item>every file under <c>Sim.Ui/UiContent/</c>, whole text;</item>
/// <item>every <c>Sim.Ui/**/*.cs</c> source file with its comments stripped (<c>//</c>, <c>///</c> and <c>/* */</c>),
///   so string literals and identifiers are scanned and design notes in comments are not.</item>
/// </list>
/// WHAT IT CANNOT DO, stated plainly:
/// <list type="bullet">
/// <item>The deny-list is a list. A game it does not name is not caught, and the release-gate review
///   (ip-originality.md §3) stays a human step.</item>
/// <item>Names that are also ordinary English or history are matched only in a distinctive form, or not at all:
///   "civilization" (our own common noun), "humankind", "old world", "millennia", "total war", "endless",
///   "anno", "Victoria" and "Alpha Centauri" occur legitimately in our historical text. The deny-list matches the
///   title forms instead ("Civilization VI", "Total War:", "Endless Legend", "Anno 1800", "Victoria 3"…) and the
///   studio names. A bare "Civ" with no numeral ("the Civ series") is not matched.</item>
/// <item>The C# comment stripper is lexical: it does not parse raw (<c>"""</c>) or verbatim string literals, so a
///   <c>//</c> inside such a literal could hide the rest of that line. Sim.Ui text is overwhelmingly regular
///   literals; the UiContent JSON is scanned whole.</item>
/// </list>
/// </summary>
public class ThirdPartyReferenceGuardTests
{
    /// <summary>The deny-list: one entry per third-party game, studio or franchise form, each a word-boundary,
    /// case-insensitive pattern, with a sample the teeth test proves it flags.</summary>
    internal static readonly (string Name, string Pattern, string Sample)[] DenyList =
    [
        ("Civilization numbered title",  @"\bCivilization\s*(?:VII|VI|IV|V|III|II|[1-7])\b", "as in Civilization VI"),
        ("Civ numbered abbreviation",    @"\bCiv\s*(?:VII|VI|IV|V|III|II|[1-7])\b",          "Civ 6's laser station; Civ6; CivVI"),
        ("Civilization: Beyond Earth",   @"\bCivilization\s*:\s*Beyond\s+Earth\b",           "Civilization: Beyond Earth"),
        ("Civilization Revolution",      @"\bCivilization\s+Revolution\b",                   "Civilization Revolution"),
        ("Civilopedia",                  @"\bCivilopedia\b",                                 "see the Civilopedia"),
        ("CivFanatics",                  @"\bCivFanatics\b",                                 "CivFanatics forum"),
        ("Freeciv",                      @"\bFreeciv\b",                                     "Freeciv rules"),
        ("FreeCol",                      @"\bFreeCol\b",                                     "FreeCol"),
        ("Sid Meier",                    @"\bSid\s+Meier",                                   "Sid Meier's"),
        ("Firaxis",                      @"\bFiraxis\b",                                     "Firaxis Games"),
        ("MicroProse",                   @"\bMicroProse\b",                                  "MicroProse"),
        ("2K Games",                     @"\b2K\s+Games\b",                                  "2K Games"),
        ("Age of Empires",               @"\bAge\s+of\s+Empires\b",                          "Age of Empires II"),
        ("AoE abbreviation",             @"\bAoE\s*(?:IV|III|II|I|[1-4])?\b",             "AoE II Pikeman"),
        ("Age of Mythology",             @"\bAge\s+of\s+Mythology\b",                        "Age of Mythology"),
        ("Age of Wonders",               @"\bAge\s+of\s+Wonders\b",                          "Age of Wonders"),
        ("Ensemble Studios",             @"\bEnsemble\s+Studios\b",                          "Ensemble Studios"),
        ("Rise of Nations",              @"\bRise\s+of\s+Nations\b",                         "Rise of Nations"),
        ("Big Huge Games",               @"\bBig\s+Huge\s+Games\b",                          "Big Huge Games"),
        ("Europa Universalis",           @"\bEuropa\s+Universalis\b",                        "Europa Universalis IV"),
        ("EU numbered abbreviation",     @"\bEU\s*(?:IV|V|4|5)\b",                           "EU4"),
        ("Crusader Kings",               @"\bCrusader\s+Kings\b",                            "Crusader Kings III"),
        ("Hearts of Iron",               @"\bHearts\s+of\s+Iron\b",                          "Hearts of Iron IV"),
        ("Stellaris",                    @"\bStellaris\b",                                   "Stellaris"),
        ("Victoria numbered title",      @"\bVictoria\s+(?:III|II|2|3)\b",                   "Victoria 3"),
        ("Imperator: Rome",              @"\bImperator\s*:\s*Rome\b",                        "Imperator: Rome"),
        ("Paradox studio",               @"\bParadox\s+(?:Interactive|Development|Development\s+Studio|Tinto)\b", "Paradox Interactive"),
        // the common noun "humankind" is ordinary English: only the all-capitals title form is matched, case-SENSITIVELY
        ("Humankind (title form)",       @"(?-i:\bHUMANKIND\b)",                             "HUMANKIND"),
        ("Amplitude Studios",            @"\bAmplitude\s+Studios\b",                         "Amplitude Studios"),
        ("Endless titles",               @"\bEndless\s+(?:Legend|Space|Dungeon)\b",          "Endless Legend"),
        ("Old World studio",             @"\bMohawk\s+Games\b",                              "Mohawk Games"),
        ("Ara: History Untold",          @"\bAra\s*:\s*History\s+Untold\b",                  "Ara: History Untold"),
        ("Oxide Games",                  @"\bOxide\s+Games\b",                               "Oxide Games"),
        ("Millennia studio",             @"\bC\s+Prompt\s+Games\b",                          "C Prompt Games"),
        ("Anno numbered title",          @"\bAnno\s+(?:1602|1503|1701|1404|2070|2205|1800|117)\b", "Anno 1800"),
        ("Total War title",              @"\bTotal\s+War\s*:|\b(?:Rome|Medieval|Shogun|Empire|Attila|Napoleon)(?:\s+II)?\s*:\s*Total\s+War\b", "Total War: Rome"),
        ("Creative Assembly",            @"\bCreative\s+Assembly\b",                         "Creative Assembly"),
        ("Galactic Civilizations",       @"\bGalactic\s+Civilizations\b",                    "Galactic Civilizations"),
        ("Stardock",                     @"\bStardock\b",                                    "Stardock"),
        ("Polytopia",                    @"\bPolytopia\b",                                   "The Battle of Polytopia"),
    ];

    private static readonly Regex[] Compiled =
        Array.ConvertAll(DenyList, e => new Regex(e.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    /// <summary>Every deny-list match in <paramref name="text"/>, as (entry name, matched text, 1-based line).</summary>
    internal static List<(string Entry, string Match, int Line)> Scan(string text)
    {
        var hits = new List<(string, string, int)>();
        for (int i = 0; i < Compiled.Length; i++)
            foreach (Match m in Compiled[i].Matches(text))
                hits.Add((DenyList[i].Name, m.Value, LineOf(text, m.Index)));
        return hits;
    }

    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++) if (text[i] == '\n') line++;
        return line;
    }

    /// <summary>Blanks C# comments (keeping newlines, so line numbers stay true) and keeps everything else, including
    /// the contents of regular and interpolated string literals and char literals.</summary>
    internal static string StripCSharpComments(string src)
    {
        var sb = new StringBuilder(src.Length);
        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];
            if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
            {
                while (i < src.Length && src[i] != '\n') i++;
            }
            else if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
            {
                i += 2;
                while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/'))
                {
                    if (src[i] == '\n') sb.Append('\n');
                    i++;
                }
                i += 2;
            }
            else if (c == '"' || c == '\'')
            {
                sb.Append(c);
                i++;
                while (i < src.Length && src[i] != c && src[i] != '\n')
                {
                    if (src[i] == '\\' && i + 1 < src.Length) { sb.Append(src[i]); i++; }
                    sb.Append(src[i]);
                    i++;
                }
                if (i < src.Length) { sb.Append(src[i]); i++; }
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string[] FilesUnder(string dir, string pattern) =>
        Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

    private static string Describe(string root, string file, List<(string Entry, string Match, int Line)> hits) =>
        string.Join("\n", hits.Select(h => $"{Path.GetRelativePath(root, file)}:{h.Line}: \"{h.Match}\" ({h.Entry})"));

    [Fact]
    public void ShippedContent_NamesNoThirdPartyGameOrStudio()
    {
        string root = RepoPaths.Root();
        string content = Path.Combine(root, "Sim.Data", "content");
        string[] files = FilesUnder(content, "*");
        // non-vacuous: the shipped content set is really here (15 embedded resources at D-069; the two biggest named)
        Assert.True(files.Length >= 15, $"expected the shipped content files under {content}, found {files.Length}");
        Assert.Contains(files, f => Path.GetFileName(f) == "research.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "unit-families.json");
        var report = new List<string>();
        foreach (string f in files)
        {
            var hits = Scan(File.ReadAllText(f, Encoding.UTF8));
            if (hits.Count > 0) report.Add(Describe(root, f, hits));
        }
        Assert.True(report.Count == 0,
            "D-069 R2: shipped content names another game, studio or franchise (move design notes to docs/):\n" + string.Join("\n", report));
    }

    [Fact]
    public void PlayerFacingUiText_NamesNoThirdPartyGameOrStudio()
    {
        string root = RepoPaths.Root();
        string ui = Path.Combine(root, "Sim.Ui");
        string[] json = FilesUnder(Path.Combine(ui, "UiContent"), "*");
        string[] cs = FilesUnder(ui, "*.cs");
        Assert.NotEmpty(json);
        Assert.True(cs.Length >= 20, $"expected the Sim.Ui sources under {ui}, found {cs.Length}");
        var report = new List<string>();
        foreach (string f in json)
        {
            var hits = Scan(File.ReadAllText(f, Encoding.UTF8));
            if (hits.Count > 0) report.Add(Describe(root, f, hits));
        }
        foreach (string f in cs)
        {
            var hits = Scan(StripCSharpComments(File.ReadAllText(f, Encoding.UTF8)));
            if (hits.Count > 0) report.Add(Describe(root, f, hits));
        }
        Assert.True(report.Count == 0,
            "D-069 R2: player-facing Sim.Ui text names another game, studio or franchise:\n" + string.Join("\n", report));
    }

    // ------------------------------------------------------------------ teeth

    [Fact]
    public void Teeth_EveryDenyListEntry_FlagsItsSample_InsideOrdinaryText()
    {
        foreach (var (name, _, sample) in DenyList)
        {
            string text = "{\"desc\": \"Our own text, then " + sample + ", then more.\"}";
            Assert.True(Scan(text).Any(h => h.Entry == name), $"deny-list entry '{name}' did not flag its sample \"{sample}\"");
        }
    }

    [Theory]
    [InlineData("Each level raises the transit velocity of probes and expeditions \\u2014 the research analogue of Civ 6's laser station, which is a project.")]
    [InlineData("Placement: Civ VI unlocks trade routes and the Trader with the Ancient-Era Foreign Trade civic")]
    [InlineData("D-051 section 5 names Civilization VI settlers as the style")]
    [InlineData("\"reference\": \"Civ VI Spearman; AoE II Spearman\"")]
    [InlineData("CIV VI")]
    [InlineData("civ vii")]
    [InlineData("Civ7")]
    [InlineData("AOE2")]
    public void Teeth_TheRemovedShippedForms_AreFlagged_CaseInsensitively(string text)
    {
        Assert.NotEmpty(Scan(text));
    }

    [Theory]
    [InlineData("civics")]
    [InlineData("Civics")]
    [InlineData("civil law and civil engineering")]
    [InlineData("civic")]
    [InlineData("a civilization of the same population")]
    [InlineData("Civilization-wide generic UNIT FAMILIES")]
    [InlineData("civilizations rise")]
    [InlineData("civ-sim/research@2")]
    [InlineData("civ-sim M6 (dev, 2026-11-01)")]
    [InlineData("the Roman empire")]
    [InlineData("Age III, the Bronze Age")]
    [InlineData("Age of Bronze")]
    [InlineData("anno domini; Anno Domini 1066")]
    [InlineData("took millennia to become a staple")]
    [InlineData("Potato, maize, cassava, tomato to the Old World")]
    [InlineData("all humankind; Humankind")]
    [InlineData("the total war of 1914")]
    [InlineData("Science, the Endless Frontier")]
    [InlineData("Lake Victoria; Queen Victoria")]
    [InlineData("an uncrewed spacecraft sent beyond Earth's orbit")]
    [InlineData("amplitude modulation")]
    [InlineData("a paradox of thrift")]
    [InlineData("Alpha Centauri")]
    [InlineData("the European Union (EU) treaty")]
    [InlineData("area of effect; aorta; Aoede")]
    [InlineData("Sidon and Sidonian glass")]
    [InlineData("the Imperator title")]
    public void Teeth_OrdinaryWords_AreNotFlagged(string text)
    {
        var hits = Scan(text);
        Assert.True(hits.Count == 0, $"false positive on \"{text}\": " + string.Join(", ", hits.Select(h => $"{h.Entry} '{h.Match}'")));
    }

    [Fact]
    public void Teeth_TheCommentStripper_HidesCommentsButNotStringLiterals()
    {
        const string src = "// Civ VI in a comment\n/* Firaxis\n in a block */ var s = \"Age of Empires\"; /// <summary>Civ 6</summary>\nvar url = \"http://x\"; // Sid Meier\nchar q = '\"';";
        string stripped = StripCSharpComments(src);
        var hits = Scan(stripped);
        Assert.Single(hits);
        Assert.Equal("Age of Empires", hits[0].Match);
        Assert.Equal(3, hits[0].Line);   // line numbers survive the stripping
        Assert.Contains("\"http://x\"", stripped, StringComparison.Ordinal);   // a // inside a literal is not a comment
    }
}
