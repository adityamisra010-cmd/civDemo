using System.Reflection;
using System.Text.RegularExpressions;
using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.World;
using Xunit;

namespace Sim.Ui.Tests.World;

/// <summary>
/// The read-only boundary, as structure rather than promise: only the live adapters see a
/// simulation type; every source interface is get-only; the world view shares no model type
/// with the Trees; and the world code uses none of the constructs the determinism rules ban
/// (Sim.Ui is outside the banned-constructs gate, so this scan is the only thing enforcing it).
/// </summary>
public class WorldBoundaryTests
{
    private static readonly Assembly Ui = typeof(WorldEntityId).Assembly;
    private static readonly Assembly Core = typeof(Sim.Core.State.WorldState).Assembly;

    private static IEnumerable<Type> WorldTypes() =>
        Ui.GetTypes().Where(t => t.Namespace is string ns && (ns == "Sim.Ui.World" || ns.StartsWith("Sim.Ui.World.", StringComparison.Ordinal)));

    private static IEnumerable<Type> Mentioned(Type t)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (FieldInfo f in t.GetFields(all)) yield return f.FieldType;
        foreach (PropertyInfo p in t.GetProperties(all)) yield return p.PropertyType;
        foreach (MethodInfo m in t.GetMethods(all))
        {
            yield return m.ReturnType;
            foreach (ParameterInfo p in m.GetParameters()) yield return p.ParameterType;
        }
        foreach (ConstructorInfo c in t.GetConstructors(all))
            foreach (ParameterInfo p in c.GetParameters()) yield return p.ParameterType;
        if (t.BaseType is Type b) yield return b;
        foreach (Type i in t.GetInterfaces()) yield return i;
    }

    private static IEnumerable<Type> Expand(Type t)
    {
        yield return t;
        if (t.HasElementType) foreach (Type e in Expand(t.GetElementType()!)) yield return e;
        if (t.IsGenericType) foreach (Type a in t.GetGenericArguments()) foreach (Type e in Expand(a)) yield return e;
    }

    [Fact]
    public void OnlyTheLiveAdapters_SeeASimulationType()
    {
        foreach (Type t in WorldTypes())
        {
            if (t.Namespace == "Sim.Ui.World.Live") continue;
            foreach (Type m in Mentioned(t).SelectMany(Expand))
                Assert.False(m.Assembly == Core, $"{t.FullName} mentions the simulation type {m.FullName}");
        }
    }

    [Fact]
    public void TheWorldView_SharesNoModelTypeWithTheTrees()
    {
        foreach (Type t in WorldTypes())
            foreach (Type m in Mentioned(t).SelectMany(Expand))
                Assert.False(m.Namespace?.StartsWith("Sim.Ui.Trees", StringComparison.Ordinal) == true, $"{t.FullName} mentions {m.FullName}");
    }

    [Fact]
    public void EverySourceInterface_IsGetOnly()
    {
        Type[] sources = [typeof(ISettlementViewSource), typeof(IPolityViewSource), typeof(IStructureViewSource),
            typeof(IInfrastructureViewSource), typeof(IResourceViewSource), typeof(IMobileAgentViewSource)];
        foreach (Type s in sources)
        {
            foreach (PropertyInfo p in s.GetProperties()) Assert.False(p.CanWrite, $"{s.Name}.{p.Name} has a setter");
            foreach (MethodInfo m in s.GetMethods()) Assert.True(m.IsSpecialName && m.Name.StartsWith("get_", StringComparison.Ordinal), $"{s.Name}.{m.Name} is a command");
        }
        // Reports are records of values: no settable property.
        foreach (Type r in new[] { typeof(SettlementReport), typeof(StructureReport), typeof(AgentReport), typeof(InfraNodeReport),
                     typeof(InfraEdgeReport), typeof(ResourceReport), typeof(PolityReport) })
            foreach (PropertyInfo p in r.GetProperties())
                Assert.True(p.SetMethod is null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"),
                    $"{r.Name}.{p.Name} is mutable");
    }

    private static string WorldSourceDir()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "Sim.Ui", "World");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Sim.Ui/World not found above " + AppContext.BaseDirectory);
    }

    /// <summary>CLAUDE.md law 5's banned constructs, applied to the world view by choice (docs §6).</summary>
    [Fact]
    public void TheWorldCode_UsesNoBannedConstruct()
    {
        var banned = new (string Name, Regex Pattern)[]
        {
            ("System.Random", new Regex(@"\bnew\s+Random\s*\(|System\.Random")),
            ("wall clock", new Regex(@"DateTime\.(Now|UtcNow)|Environment\.TickCount|Stopwatch")),
            ("GetHashCode as logic", new Regex(@"GetHashCode\s*\(")),
            ("HashCode.Combine", new Regex(@"HashCode\.Combine")),
            ("parallelism", new Regex(@"AsParallel|Parallel\.(For|ForEach|Invoke)")),
            ("float", new Regex(@"\bfloat\b")),
            ("dictionary iteration", new Regex(@"foreach\s*\([^)]*\bin\s+[\w\.]+\.(Keys|Values)\s*\)|foreach\s*\(\s*KeyValuePair")),
            ("culture-sensitive format", new Regex(@"\.ToString\(\s*""[^""]*""\s*\)")),
        };
        var failures = new List<string>();
        foreach (string file in Directory.EnumerateFiles(WorldSourceDir(), "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string code = lines[i].Split("//")[0];
                foreach ((string name, Regex p) in banned)
                    if (p.IsMatch(code)) failures.Add($"{Path.GetFileName(file)}:{i + 1} {name}: {lines[i].Trim()}");
            }
        }
        Assert.Empty(failures);
    }

    /// <summary>The four marks the world foundation appended draw four different things, none
    /// of them the empty mark, and none is named for a person (the anatomy fence, D-038 C1).</summary>
    [Fact]
    public void TheNewObjectMarks_AreDistinct_AndNamedForObjects()
    {
        GlyphDomain[] added = [GlyphDomain.Lyre, GlyphDomain.Compass, GlyphDomain.Anchor, GlyphDomain.Drop];
        Assert.Equal([33, 34, 35, 36], added.Select(d => (int)d));
        var bakes = new List<byte[]> { GlyphBaker.Bake(new GlyphSpec(GlyphBase.Emblem, GlyphState.Complete, SizeClass.Px32)).Rgba };
        foreach (GlyphDomain d in added) bakes.Add(GlyphBaker.Bake(new GlyphSpec(GlyphBase.Emblem, GlyphState.Complete, SizeClass.Px32, d)).Rgba);
        for (int i = 0; i < bakes.Count; i++)
            for (int j = i + 1; j < bakes.Count; j++)
                Assert.False(bakes[i].AsSpan().SequenceEqual(bakes[j]), $"marks {i} and {j} bake identically");
        foreach (GlyphDomain d in added)
            foreach (string person in new[] { "Musician", "Explorer", "Sailor", "Person", "Man", "Woman", "Figure" })
                Assert.DoesNotContain(person, d.ToString(), StringComparison.Ordinal);
    }
}
