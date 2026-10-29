using System.Reflection;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Military;

/// <summary>
/// M6.8–M6.10 (D-E and D-F RULED option 1: "No serialized per-unit progression state in M6"; "No executable training state,
/// training order or AP-resolution change in M6"; D-066 A11: "M6 packets add none") — THE CONTENT IS INERT, AND THE CONTRACT
/// DOCUMENTS AND THE LOADER'S VOCABULARY ARE ONE.
/// <list type="bullet">
/// <item>No simulation, CLI or kernel source names the unit-progression types or file except the loader itself.</item>
/// <item>SimConfig carries no progression member (no system can be handed it); WorldState holds no promotion or training table;
/// MilitaryUnitRow keeps its nine fields; no order kind trains, promotes, builds or moves; kind 10 stays unassigned.</item>
/// <item>No Builder / worker unit type exists (task register M6.8: construction stays city-level).</item>
/// <item>The (field, term) table of docs/m6-plan/unit-progression-contract.md and the condition table of
/// docs/m6-plan/training-ap-contract.md equal <see cref="UnitProgressionContract"/> (and the shipped rejections).</item>
/// </list>
/// </summary>
public class UnitProgressionInertTests
{
    private static readonly string[] ProgressionNames =
    [
        "UnitProgression", "PromotionTree", "PromotionEffect", "TrainingProfile", "TrainingContract", "TrainingRejection",
        "unit-progression.json",
    ];

    private static IEnumerable<string> Sources(string project)
    {
        string root = Path.Combine(RepoPaths.Root(), project);
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(RepoPaths.Root(), file).Replace('\\', '/');
            if (rel.Contains("/obj/", StringComparison.Ordinal) || rel.Contains("/bin/", StringComparison.Ordinal)) continue;
            yield return rel;
        }
    }

    [Fact]
    public void NoSimulationKernelOrCliSource_NamesTheProgressionContent_ButItsLoader()
    {
        const string loader = "Sim.Core/Systems/Ages/UnitProgressionContent.cs";
        var readers = new List<string>();
        bool loaderSeen = false;
        foreach (string rel in Sources("Sim.Core").Concat(Sources("Sim.Cli")).OrderBy(r => r, StringComparer.Ordinal))
        {
            string text = File.ReadAllText(Path.Combine(RepoPaths.Root(), rel));
            bool names = ProgressionNames.Any(n => text.Contains(n, StringComparison.Ordinal));
            if (rel == loader) { loaderSeen = names; continue; }
            if (names) readers.Add(rel);
        }
        Assert.True(loaderSeen, "the scan must see the loader itself (a scan that finds nothing proves nothing)");
        Assert.Empty(readers);
    }

    [Fact]
    public void NoConfigMember_NoWorldTable_NoNewUnitField_HoldsProgression()
    {
        static bool Progression(Type t) =>
            t == typeof(UnitProgressionContent) || t == typeof(TrainingContract) || t == typeof(PromotionTree)
            || (t.IsGenericType && t.GetGenericArguments().Any(Progression)) || (t.IsArray && Progression(t.GetElementType()!));
        foreach (PropertyInfo p in typeof(SimConfig).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Assert.False(Progression(p.PropertyType), "SimConfig." + p.Name);
        foreach (FieldInfo f in typeof(SimConfig).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Assert.False(Progression(f.FieldType), "SimConfig." + f.Name);
        foreach (PropertyInfo p in typeof(WorldState).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain("Promotion", p.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("Training", p.Name, StringComparison.Ordinal);
        }
        // D-066 A11: no per-unit state is added by M6 — the formation row keeps the nine fields ADR-031 gave it.
        Assert.Equal(["Id", "Owner", "Family", "Identity", "Location", "X", "Y", "Experience", "Army"],
            typeof(MilitaryUnitRow).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name));
    }

    [Fact]
    public void NoOrderKind_TrainsPromotesBuildsOrMoves_AndKind10StaysUnassigned()
    {
        foreach (string name in Enum.GetNames<OrderKind>())
            foreach (string verb in new[] { "Train", "Promot", "Builder", "Worker", "Move" })
                Assert.DoesNotContain(verb, name, StringComparison.Ordinal);
        Assert.False(Enum.IsDefined(typeof(OrderKind), 10));
    }

    [Fact]
    public void NoBuilderOrWorkerUnitType_ConstructionStaysCityLevel()
    {
        Type[] types = typeof(SimConfig).Assembly.GetTypes();
        Assert.NotEmpty(types);
        foreach (Type t in types)
        {
            Assert.DoesNotContain("Builder", t.Name, StringComparison.Ordinal);
            Assert.False(t.Name.EndsWith("Worker", StringComparison.Ordinal) || t.Name.StartsWith("Worker", StringComparison.Ordinal), t.FullName);
        }
    }

    // ------------------------------------------------------------------ the documents and the vocabulary are one

    /// <summary>The rows of the Markdown table that follows <paramref name="marker"/> (an HTML comment line), as cells with
    /// backticks stripped; the header and separator rows are skipped.</summary>
    private static List<string[]> Table(string doc, string marker)
    {
        string[] lines = File.ReadAllLines(Path.Combine(RepoPaths.Root(), "docs", "m6-plan", doc));
        int at = Array.FindIndex(lines, l => l.Trim() == marker);
        Assert.True(at >= 0, doc + " has no " + marker);
        var rows = new List<string[]>();
        int i = at + 1;
        while (i < lines.Length && lines[i].Trim().Length == 0) i++;
        for (int row = 0; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++, row++)
        {
            if (row < 2) continue;   // header, separator
            string[] cells = lines[i].Trim().Trim('|').Split('|');
            rows.Add([.. cells.Select(c => c.Trim().Trim('`'))]);
        }
        Assert.NotEmpty(rows);
        return rows;
    }

    [Fact]
    public void TheContractDocument_NamesExactlyTheLoadersFieldsTermsAndRefusals()
    {
        List<string[]> terms = Table("unit-progression-contract.md", "<!-- contract:terms -->");
        Assert.Equal(UnitProgressionContract.Terms.Select(t => t.Field + "." + t.Term), terms.Select(r => r[0] + "." + r[1]));
        Assert.Equal(UnitProgressionContract.Fields, terms.Select(r => r[0]).Distinct());
        List<string[]> refused = Table("unit-progression-contract.md", "<!-- contract:refused -->");
        Assert.Equal(UnitProgressionContract.Refused.Select(r => r.Name), refused.Select(r => r[0]));
    }

    [Fact]
    public void TheTrainingDocument_NamesExactlyTheLoadersConditions_AndTheShippedRejectionCodes()
    {
        List<string[]> conditions = Table("training-ap-contract.md", "<!-- contract:conditions -->");
        Assert.Equal(UnitProgressionContract.TrainingConditions, conditions.Select(r => r[0]));
        using Stream s = Sim.Data.DataFiles.OpenUnitProgression();
        UnitProgressionContent shipped = UnitProgressionContentLoader.Load(s, TestConfigs.UnitFamilies());
        Assert.Equal(shipped.Training.Rejections.Select(r => r.Code), conditions.Select(r => r[1]));
    }
}
