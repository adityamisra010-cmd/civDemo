using System.Globalization;
using System.Text;
using Sim.Ui.Render;
using Sim.Ui.World;
using Sim.Ui.World.Content;
using Sim.Ui.World.Demo;
using Sim.Ui.World.Scene;
using Sim.Ui.World.View;
using Xunit;

namespace Sim.Ui.Tests.World;

/// <summary>Shared fixtures: the SHIPPED content (the same files the game loads) and small
/// hand-built sources for the pure functions.</summary>
internal static class WorldTestKit
{
    public static string Dir => WorldContentLoader.DefaultDirectory;

    public static WorldMorphology Morph()
    {
        (WorldMorphology? m, IReadOnlyList<WorldDiagnostic> d) = WorldContentLoader.LoadMorphologyFile(Path.Combine(Dir, WorldContentLoader.MorphologyFile));
        Assert.True(m is not null, string.Join("\n", d));
        return m!;
    }

    public static DemoWorldSource Demo(WorldMorphology morph, int step = 0)
    {
        (DemoWorldSource? s, IReadOnlyList<WorldDiagnostic> d) = DemoWorldSource.LoadFile(Path.Combine(Dir, WorldContentLoader.DemoFile), morph, step);
        Assert.True(s is not null, string.Join("\n", d));
        return s!;
    }

    public static string MorphologyJson => File.ReadAllText(Path.Combine(Dir, WorldContentLoader.MorphologyFile));
    public static string DemoJson => File.ReadAllText(Path.Combine(Dir, WorldContentLoader.DemoFile));

    public static WorldView DemoView(WorldMorphology morph, int step) => WorldViewBuilder.Build(Demo(morph, step).Sources(), morph);

    /// <summary>A canonical dump of a draw list with every double written as its exact bits, so
    /// "identical" means bit-identical (SVG rounds to 2 dp and would hide drift).</summary>
    public static string Dump(DrawList dl)
    {
        var sb = new StringBuilder();
        static string B(double v) => BitConverter.DoubleToInt64Bits(v).ToString("X16", CultureInfo.InvariantCulture);
        foreach (DrawCmd c in dl.Commands)
        {
            switch (c)
            {
                case RectCmd r: sb.Append("R ").Append(B(r.Rect.X)).Append(B(r.Rect.Y)).Append(B(r.Rect.W)).Append(B(r.Rect.H)).Append(r.Fill).Append(r.Stroke).Append(B(r.StrokeWidth)).Append(B(r.Radius)); break;
                case LineCmd l: sb.Append("L ").Append(B(l.X0)).Append(B(l.Y0)).Append(B(l.X1)).Append(B(l.Y1)).Append(l.Color).Append(B(l.Width)).Append(l.Dash is (double a, double b) ? B(a) + B(b) : "-"); break;
                case PolygonCmd p: sb.Append("P "); foreach ((double x, double y) in p.Points) sb.Append(B(x)).Append(B(y)); sb.Append(p.Fill); break;
                case CircleCmd ci: sb.Append("C ").Append(B(ci.Cx)).Append(B(ci.Cy)).Append(B(ci.R)).Append(ci.Fill).Append(ci.Stroke).Append(B(ci.StrokeWidth)); break;
                case TextCmd t: sb.Append("T ").Append(B(t.X)).Append(B(t.Y)).Append(t.Text).Append('|').Append(B(t.Size)).Append(t.Color).Append(t.Align).Append(t.Role); break;
                case GlyphCmd g: sb.Append("G ").Append(B(g.X)).Append(B(g.Y)).Append(B(g.Size)).Append(g.Spec).Append(B(g.Alpha)); break;
                default: sb.Append(c); break;
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public static WorldFrame Paint(WorldView view, WorldMorphology morph, WorldProjection proj, WorldUiState? ui = null, WorldSceneOptions? options = null) =>
        WorldScene.Paint(view, morph, proj, ui ?? new WorldUiState(), options ?? WorldSceneOptions.Full, ApproxTextMeasure.Instance);

    public static WorldEntityId Agent(string key) => new(WorldEntityKind.Agent, key);
    public static WorldEntityId Structure(string key) => new(WorldEntityKind.Structure, key);
    public static WorldEntityId Settlement(string key) => new(WorldEntityKind.Settlement, key);

    // ---------------------------------------------------------- hand-built sources

    public static SettlementReport SettlementAt(string key, double x, double y, params (string Name, double Value)[] inputs) =>
        new(key, key, null, PolityRelation.None, new WorldPoint(x, y), inputs.Select(i => new ReportedInput(i.Name, i.Value)).ToArray(),
            ReportedVisibility.Visible, "test");

    public static StructureReport StructureOf(string key, string type, string settlement, long? established = null, long multiplicity = 1,
        ReportedVisibility visibility = ReportedVisibility.Visible, params (string Name, double Value)[] inputs) =>
        new(key, key, type, settlement, null, PolityRelation.None, established, multiplicity,
            inputs.Select(i => new ReportedInput(i.Name, i.Value)).ToArray(), [], null, null, visibility, "test");

    public static AgentReport AgentAt(string key, string type, double x, double y, long? count = null,
        ReportedVisibility visibility = ReportedVisibility.Visible) =>
        new(key, key, type, null, PolityRelation.None, new WorldPoint(x, y), null, null, null, count, null, [], visibility, "test");

    public static WorldSources Sources(IEnumerable<SettlementReport>? settlements = null, IEnumerable<StructureReport>? structures = null,
        IEnumerable<AgentReport>? agents = null, IEnumerable<InfraNodeReport>? nodes = null, IEnumerable<InfraEdgeReport>? edges = null,
        IEnumerable<ResourceReport>? resources = null, IEnumerable<PolityReport>? polities = null)
    {
        var s = new FixedSource(settlements ?? [], structures ?? [], agents ?? [], nodes ?? [], edges ?? [], resources ?? [], polities ?? []);
        return new WorldSources("TEST", false, "test observer", s, s, s, s, s, s);
    }

    /// <summary>A fixed, test-only source bundle (reports supplied directly).</summary>
    internal sealed class FixedSource(
        IEnumerable<SettlementReport> settlements, IEnumerable<StructureReport> structures, IEnumerable<AgentReport> agents,
        IEnumerable<InfraNodeReport> nodes, IEnumerable<InfraEdgeReport> edges, IEnumerable<ResourceReport> resources,
        IEnumerable<PolityReport> polities)
        : ISettlementViewSource, IPolityViewSource, IStructureViewSource, IInfrastructureViewSource, IResourceViewSource, IMobileAgentViewSource
    {
        private readonly Snapshot<SettlementReport> _s = new(1, "TEST", false, settlements, r => r.Key);
        private readonly Snapshot<StructureReport> _st = new(1, "TEST", false, structures, r => r.Key);
        private readonly Snapshot<AgentReport> _a = new(1, "TEST", false, agents, r => r.Key);
        private readonly Snapshot<InfraNodeReport> _n = new(1, "TEST", false, nodes, r => r.Key);
        private readonly Snapshot<InfraEdgeReport> _e = new(1, "TEST", false, edges, r => r.Key);
        private readonly Snapshot<ResourceReport> _r = new(1, "TEST", false, resources, r => r.Key);
        private readonly Snapshot<PolityReport> _p = new(1, "TEST", false, polities, r => r.Key);
        Snapshot<SettlementReport> ISettlementViewSource.Current => _s;
        Snapshot<PolityReport> IPolityViewSource.Current => _p;
        Snapshot<StructureReport> IStructureViewSource.Current => _st;
        public Snapshot<InfraNodeReport> Nodes => _n;
        public Snapshot<InfraEdgeReport> Edges => _e;
        Snapshot<ResourceReport> IResourceViewSource.Current => _r;
        Snapshot<AgentReport> IMobileAgentViewSource.Current => _a;
    }
}
