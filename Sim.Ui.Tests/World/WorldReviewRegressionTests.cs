using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Sim.Ui.Art.Glyphs;
using Sim.Ui.Render;
using Sim.Ui.World;
using Sim.Ui.World.Content;
using Sim.Ui.World.Scene;
using Sim.Ui.World.View;
using Xunit;
using static Sim.Ui.Tests.World.WorldTestKit;

namespace Sim.Ui.Tests.World;

/// <summary>
/// One pin per CONFIRMED finding of the adversarial review of 4093910 (docs/architecture/
/// world-visualization.md §12). Ids in the names are the review's. Which pins kill which
/// reverted fix was measured by a mutation battery, recorded in the doc (§12.1).
/// </summary>
public class WorldReviewRegressionTests
{
    private static readonly WorldMorphology M = Morph();
    private static WorldProjection At(double x, double y, double zoom) => new(x, y, zoom, 1280, 800);

    // ------------------------------------------------ det-2 / place-2: attached people never jump

    private static AgentReport AtSettlement(string key, string settlement, long? est = null) =>
        new(key, key, "person", null, PolityRelation.None, null, null, settlement, null, null, null, [], ReportedVisibility.Visible, "t", est);

    [Fact]
    public void Det2_AttachedPeople_KeepTheirLot_WhenAnotherArrivesOrLeaves()
    {
        SettlementReport s = SettlementAt("s", 50, 50, ("population", 5000));
        string[] keys = ["p-10", "p-20", "p-30", "p-40", "p-50"];
        long Est(string k) => k == "p-05" ? 99 : long.Parse(k[2..], CultureInfo.InvariantCulture);
        WorldView all = WorldViewBuilder.Build(Sources([s], agents: keys.Select(k => AtSettlement(k, "s", Est(k))).ToArray()), M);
        // A LATER arrival with a LOWER key (p-05) moves nobody: arrival order, not key, ranks first.
        WorldView plus = WorldViewBuilder.Build(Sources([s], agents: keys.Append("p-05").Select(k => AtSettlement(k, "s", Est(k))).ToArray()), M);
        foreach (string k in keys)
            Assert.Equal(all.FindAgent(Agent(k))!.AttachIndex, plus.FindAgent(Agent(k))!.AttachIndex);
        // A departure moves only agents ranked after it, and only into a freed lot.
        WorldView minus = WorldViewBuilder.Build(Sources([s], agents: keys.Where(k => k != "p-20").Select(k => AtSettlement(k, "s", Est(k))).ToArray()), M);
        Assert.Equal(all.FindAgent(Agent("p-10"))!.AttachIndex, minus.FindAgent(Agent("p-10"))!.AttachIndex);
        Assert.Equal(keys.Length, all.Agents.Select(a => a.AttachIndex).Distinct().Count());
        // With no arrival order reported (the live notables), key order decides: a higher-key arrival moves nobody.
        WorldView bare = WorldViewBuilder.Build(Sources([s], agents: keys.Select(k => AtSettlement(k, "s")).ToArray()), M);
        WorldView barePlus = WorldViewBuilder.Build(Sources([s], agents: keys.Append("p-99").Select(k => AtSettlement(k, "s")).ToArray()), M);
        foreach (string k in keys) Assert.Equal(bare.FindAgent(Agent(k))!.AttachIndex, barePlus.FindAgent(Agent(k))!.AttachIndex);
    }

    [Fact]
    public void Det2_AttachedPeople_KeepTheirLot_AcrossTheDemoTimeline()
    {
        Dictionary<string, int>? prev = null;
        for (int step = 0; step < 6; step++)
        {
            var now = DemoView(M, step).Agents.Where(a => a.Anchor == AgentAnchor.Settlement).ToDictionary(a => a.Report.Key, a => a.AttachIndex);
            if (prev is not null)
                foreach ((string k, int lot) in prev) if (now.TryGetValue(k, out int n)) Assert.Equal(lot, n);
            prev = now;
        }
    }

    // ------------------------------------------------ det-3: what is clicked is what is seen

    [Fact]
    public void Det3_PartlyOverlappingTokens_TheOneDrawnOnTopIsHit()
    {
        // a01 (lowest id: drawn last, on top) and a02, 0.4 world units apart at 20 px/unit.
        AgentReport a1 = AgentAt("a01", "scientist", 10.0, 10.0), a2 = AgentAt("a02", "engineer", 10.4, 10.0);
        WorldView v = WorldViewBuilder.Build(Sources(agents: [a2, a1]), M);
        var proj = At(10, 10, 20);
        WorldFrame f = Paint(v, M, proj);
        (double x2, double y2) = proj.ToScreen(new WorldPoint(10.4, 10.0));
        // A point inside both circles but NEARER a02's centre: a01 is drawn over it there.
        double px = x2 - 4, py = y2;
        Assert.True(f.Hits.Regions.Count(r => r.Contains(px, py)) >= 2);
        Assert.Equal(Agent("a01"), f.Hits.HitTest(px, py));
    }

    [Fact]
    public void Det3_AStackedLabel_SelectsItsOwnAgent()
    {
        WorldView v = DemoView(M, 4);
        var proj = At(228, 198, 3.0);
        WorldFrame f = Paint(v, M, proj);
        var labels = f.Hits.Regions.Where(r => r.Priority == HitIndex.LabelPriority).ToList();
        Assert.True(labels.Count >= 2);
        foreach (HitRegion r in labels) Assert.Equal(r.Id, f.Hits.HitTest(r.X + r.W / 2, r.Y + r.H / 2));
    }

    // ------------------------------------------------ det-4: a hidden resource leaves a gap, not a shift

    [Fact]
    public void Det4_AHiddenResource_DoesNotShiftTheOthers()
    {
        SettlementReport s = SettlementAt("s", 50, 50, ("population", 5000));
        ResourceReport R(string k, ReportedVisibility v) => new(k, k, "grain", "s", [], v, "t");
        WorldView shown = WorldViewBuilder.Build(Sources([s], resources: [R("r1", ReportedVisibility.Visible), R("r2", ReportedVisibility.Visible), R("r3", ReportedVisibility.Visible)]), M);
        WorldView hidden = WorldViewBuilder.Build(Sources([s], resources: [R("r1", ReportedVisibility.Visible), R("r2", ReportedVisibility.Hidden), R("r3", ReportedVisibility.Visible)]), M);
        var proj = At(50, 50, 20);
        HitRegion Hit(WorldView v, string k) => Paint(v, M, proj).Hits.Regions.Single(r => r.Id == new WorldEntityId(WorldEntityKind.Resource, k));
        Assert.Equal(Hit(shown, "r1"), Hit(hidden, "r1"));
        Assert.Equal(Hit(shown, "r3"), Hit(hidden, "r3"));
    }

    // ------------------------------------------------ det-5 / place-3: removal under aggregation

    [Fact]
    public void Det5_ARemovalWithAggregation_NeverMovesWhatRanksBeforeIt()
    {
        int n = M.Aggregation.IndividualUpTo;
        var reports = new List<StructureReport> { StructureOf("h-0", "hospital", "s", 0) };
        for (int i = 1; i <= n + 2; i++) reports.Add(StructureOf($"u-{i}", "university", "s", i));
        reports.Add(StructureOf("h-9", "hospital", "s", 1));
        SettlementReport s = SettlementAt("s", 0, 0);
        WorldView before = WorldViewBuilder.Build(Sources([s], reports), M);
        for (int removed = 1; removed < reports.Count; removed++)
        {
            StructureReport gone = reports[removed];
            WorldView after = WorldViewBuilder.Build(Sources([s], reports.Where((_, i) => i != removed).ToArray()), M);
            foreach (StructureReport r in reports)
            {
                if (r == gone || WorldViewBuilder.ComparePriority(r, gone) > 0) continue;
                Assert.Equal(before.FindStructure(Structure(r.Key))!.Slot, after.FindStructure(Structure(r.Key))!.Slot);
            }
        }
    }

    // ------------------------------------------------ det-6: headings

    [Theory]
    [InlineData(-360.0, 0.0)]
    [InlineData(-0.04, 359.96)]
    [InlineData(720.0, 0.0)]
    public void Det6_Headings_NormaliseWithoutNegativeZero(double deg, double expected)
    {
        double h = WorldViewBuilder.NormalizeHeading(deg);
        Assert.Equal(expected, h, 10);
        Assert.False(double.IsNegative(h));
    }

    [Fact]
    public void Det6_AHeadingNear360_IsShownAsZero()
    {
        AgentReport a = AgentAt("a", "army", 5, 5) with { HeadingDeg = 359.96 };
        WorldView v = WorldViewBuilder.Build(Sources(agents: [a]), M);
        DetailLine line = WorldInspector.Details(v, M, Agent("a"))!.Lines.Single(l => l.Label == "Heading");
        Assert.StartsWith("0 deg", line.Value, StringComparison.Ordinal);
    }

    // ------------------------------------------------ semantics-5: unreported count is the base size

    [Fact]
    public void Semantics5_AnUnreportedCount_IsDrawnAtTheBaseBand()
    {
        Assert.Equal(M.SizeBands[0].Scale, M.SizeScale(null));
        Assert.True(M.SizeScale(null) <= M.SizeScale(1));
    }

    // ------------------------------------------------ load-1 / load-2 / blocks-1: loader contracts

    private static IReadOnlyList<WorldDiagnostic> MorphWith(Action<JsonObject> edit)
    {
        JsonObject o = JsonNode.Parse(MorphologyJson)!.AsObject();
        edit(o);
        (WorldMorphology? m, IReadOnlyList<WorldDiagnostic> d) = WorldContentLoader.LoadMorphology(o.ToJsonString());
        Assert.Null(m);
        return d;
    }

    [Fact]
    public void Load1_HugeLotCounts_AreDiagnostics_NotAnOverflowOrAFreeze()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();   // test-side timing only
        IReadOnlyList<WorldDiagnostic> d = MorphWith(o =>
        {
            o["layout"]!["slotRings"]![2]!["lots"] = int.MaxValue;
            o["layout"]!["blockRings"]![0]!["lots"] = 2_000_000_000;
        });
        Assert.Contains(d, x => x.Message.Contains("at most", StringComparison.Ordinal));
        Assert.True(watch.Elapsed.TotalSeconds < 10, $"the loader took {watch.Elapsed.TotalSeconds:F1} s");
    }

    [Fact]
    public void Load2_NoInfrastructureType_IsADiagnostic() =>
        Assert.Contains(MorphWith(o => o["infrastructureTypes"] = new JsonArray()), d => d.Path == "infrastructureTypes");

    [Fact]
    public void Blocks1_AStageLargerThanTheLattice_IsADiagnostic() =>
        Assert.Contains(MorphWith(o => o["settlementStages"]!["stages"]![4]!["blocks"] = 100000), d => d.Message.Contains("block lattice", StringComparison.Ordinal));

    /// <summary>Every demo settlement at every step draws exactly its stage's block count: the
    /// lattice never runs out, so a growing city never shows fewer blocks.</summary>
    [Fact]
    public void Blocks1_EverySettlement_DrawsItsStagesBlockCount()
    {
        for (int step = 0; step < 6; step++)
            foreach (SettlementView s in DemoView(M, step).Settlements)
                Assert.Equal(M.Settlements.Stages[s.Stage.Index].Blocks, s.Blocks.Count);
        // And the worst case: the largest stage with every slot of one settlement taken.
        StructureReport[] full = Enumerable.Range(0, M.Layout.SlotCount).Select(i => StructureOf($"k{i:D3}", "structure", "s", i)).ToArray();
        WorldMorphology wide = WorldContentLoader.LoadMorphology(MorphologyJson.Replace("\"individualUpTo\": 4", "\"individualUpTo\": 99", StringComparison.Ordinal)).Morphology!;
        WorldView v = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0, ("population", 1e7))], full), wide);
        Assert.Equal(wide.Settlements.Stages[^1].Blocks, v.Settlements[0].Blocks.Count);
    }

    // ------------------------------------------------ arch-3: provenance cannot be mislabelled

    [Fact]
    public void Arch3_AMixedBundle_IsLabelledPlaceholder_AndNeverDrawnOnTheLiveMap()
    {
        var demo = Demo(M, 4);
        WorldSources mixed = demo.Sources() with { IsPlaceholder = false, Label = "claims to be live" };
        WorldView v = WorldViewBuilder.Build(mixed, M);
        Assert.True(v.IsPlaceholder);
        Assert.Contains(v.Notes, n => n.Contains("mixes placeholder", StringComparison.Ordinal));
        var host = new WorldOverlayHost(Dir, mixed);
        Assert.Null(host.LiveFrame(At(150, 190, 10), null, ApproxTextMeasure.Instance));
    }

    // ------------------------------------------------ arch-5: the composed sprite at Mid, not a glyph ring

    [Fact]
    public void Arch5_AtMidDetail_BuildingsAreParts_AndOnlyNonOperationalStatesGetAGlyph()
    {
        WorldView v = DemoView(M, 5);
        WorldFrame f = Paint(v, M, At(150, 190, 9));
        Assert.Equal(WorldLod.Mid, f.Lod);
        LayerSpan span = f.Span(WorldLayer.Buildings);
        var cmds = f.Draw.Commands.Skip(span.Start).Take(span.Count).ToList();
        var glyphs = cmds.OfType<GlyphCmd>().ToList();
        Assert.All(glyphs, g => Assert.NotEqual(GlyphState.Complete, g.Spec.State));   // only 'what is happening'
        Assert.Contains(glyphs, g => g.Spec.State == GlyphState.InProgress);            // University #155
        Assert.True(cmds.OfType<RectCmd>().Count() > 3 * v.Structures.Count(s => s.Slot is not null)); // parts, not icons
    }

    // ------------------------------------------------ arch-6: a live-project remap takes effect

    [Fact]
    public void Arch6_TheLiveProjectMap_IsConsultedFirst()
    {
        WorldMorphology remapped = WorldContentLoader.LoadMorphology(MorphologyJson.Replace(
            "\"project\": \"granary\",\n      \"visualType\": \"granary\"", "\"project\": \"granary\",\n      \"visualType\": \"reservoir\"", StringComparison.Ordinal)).Morphology!;
        Assert.Equal("reservoir", remapped.LiveVisualType("granary"));
        Assert.Equal("reservoir", WorldViewBuilder.ResolveType("granary", remapped).Id);
    }

    // ------------------------------------------------ arch-7: one light

    [Fact]
    public void Arch7_PartShadows_UseObjectLightsRule_AtEverySizeClass()
    {
        foreach (SizeClass z in new[] { SizeClass.Px16, SizeClass.Px24, SizeClass.Px32, SizeClass.Px48 })
            Assert.Equal(ObjectLight.ShadowOffset(z), ObjectLight.ShadowOffsetPx((int)z));
        (int dx, int dy) = ObjectLight.ShadowOffsetPx(40);
        Assert.Equal(Math.Sign(ObjectLight.ShadowDirection.X), Math.Sign(dx));
        Assert.Equal(Math.Sign(ObjectLight.ShadowDirection.Y), Math.Sign(dy));
    }

    // ------------------------------------------------ arch-8: the contact sheet grows with the alphabet

    [Fact]
    public void Arch8_TheContactSheet_HasARowForEveryMark()
    {
        int marks = Enum.GetValues<GlyphDomain>().Length - 1;
        Assert.True(GlyphSheet.MarkRows * GlyphSheet.Columns >= marks);
        Assert.Equal(21 + GlyphSheet.MarkRows + 2, GlyphSheet.Rows);
    }

    // ------------------------------------------------ vis-1 / vis-2: hidden and remembered

    [Fact]
    public void Vis1_AHiddenSettlement_AnchorsNobody_CountsNothing_AndRevealsNothing()
    {
        SettlementReport hidden = SettlementAt("s", 50, 50, ("population", 5000)) with { Visibility = ReportedVisibility.Hidden };
        WorldView v = WorldViewBuilder.Build(Sources([hidden], [StructureOf("u", "university", "s", 0)], [AtSettlement("p", "s")]), M);
        Assert.False(v.FindAgent(Agent("p"))!.Drawable);
        Assert.False(v.FindStructure(Structure("u"))!.Drawable);
        Assert.Empty(v.Summary);
        InspectorDetails d = WorldInspector.Details(v, M, Settlement("s"))!;
        Assert.Equal("Not visible", d.Title);
        Assert.Single(d.Lines);
        Assert.Equal("Not visible", WorldInspector.Details(v, M, Structure("u"))!.Title);
    }

    [Fact]
    public void Vis2_ARememberedStructure_IsNotSelectableThroughItsCluster()
    {
        int n = M.Aggregation.IndividualUpTo;
        var reports = Enumerable.Range(0, n).Select(i => StructureOf($"u{i}", "university", "s", i)).ToList();
        reports.Add(StructureOf("u8", "university", "s", 8, 1, ReportedVisibility.Remembered));
        WorldView v = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], reports), M);
        ClusterView k = v.Clusters.Single();
        Assert.True(k.Ghost);
        Assert.Null(k.HitTarget);
        WorldFrame f = Paint(v, M, At(0, 0, 20));
        Assert.DoesNotContain(f.Hits.Regions, r => r.Id == Structure("u8"));
        reports.Add(StructureOf("u9", "university", "s", 9));
        WorldView v2 = WorldViewBuilder.Build(Sources([SettlementAt("s", 0, 0)], reports), M);
        Assert.Equal(Structure("u9"), v2.Clusters.Single().HitTarget);   // the first member that is not merely remembered
    }

    // ------------------------------------------------ demo-1: the demo camera never degenerates

    [Theory]
    [InlineData(300, 600)]
    [InlineData(1280, 60)]
    [InlineData(1, 1)]
    public void Demo1_TheCameraFit_IsPositiveAndFinite_InAnyViewport(double w, double h)
    {
        var host = new WorldOverlayHost(Dir, null);
        WorldProjection p = host.DemoProjection(w, h);
        Assert.True(p.PxPerWorldUnit > 0 && double.IsFinite(p.PxPerWorldUnit));
        Assert.True(double.IsFinite(p.CenterX) && double.IsFinite(p.CenterY));
    }

    // ------------------------------------------------ auth-3 / game-1: the game's settlement click wins

    [Fact]
    public void Auth3_ASettlementClick_BelongsToTheSettlement()
    {
        WorldEntityId structure = Structure("u");
        Assert.Null(WorldOverlayHost.LiveClick(3, structure));      // marker or label admitted: the settlement's
        Assert.Equal(structure, WorldOverlayHost.LiveClick(-1, structure));
        Assert.Null(WorldOverlayHost.LiveClick(-1, null));
    }

    // ------------------------------------------------ doc-1: labels draw lowest id on top

    [Fact]
    public void Doc1_TheLabelsLayer_DrawsInDescendingIdOrder()
    {
        WorldView v = DemoView(M, 4);
        WorldFrame f = Paint(v, M, WorldProjection.FitInto(480, 300, 352, 74, 916, 714, 1280, 800));
        LayerSpan span = f.Span(WorldLayer.Labels);
        var names = v.Settlements.Where(s => s.Drawable).Select(s => s.Report.DisplayName).ToHashSet();
        var order = f.Draw.Commands.Skip(span.Start).Take(span.Count).OfType<TextCmd>().Where(t => names.Contains(t.Text))
            .Select(t => v.Settlements.Single(s => s.Report.DisplayName == t.Text).Key).ToList();
        Assert.Equal(order.OrderByDescending(k => k, StringComparer.Ordinal), order);
    }

    // ------------------------------------------------ teeth-1 / teeth-2: tests that can fail

    /// <summary>A deep canonical dump of every report kind, doubles as bits, lists expanded.</summary>
    internal static string DeepDump(WorldSources b)
    {
        var sb = new StringBuilder();
        static string B(double v) => BitConverter.DoubleToInt64Bits(v).ToString("X16", CultureInfo.InvariantCulture);
        void In(IReadOnlyList<ReportedInput> xs) { foreach (ReportedInput i in xs) sb.Append(i.Name).Append('=').Append(B(i.Value)).Append(';'); }
        foreach (SettlementReport r in b.Settlements.Current.Items) { sb.Append(r with { Inputs = [] }).Append('|'); In(r.Inputs); sb.Append('\n'); }
        foreach (StructureReport r in b.Structures.Current.Items)
        {
            sb.Append(r with { Inputs = [], Labels = [] }).Append('|'); In(r.Inputs);
            foreach (ReportedLabel l in r.Labels) sb.Append(l.Name).Append('=').Append(l.Value).Append(';');
            sb.Append('\n');
        }
        foreach (AgentReport r in b.Agents.Current.Items) { sb.Append(r with { Members = [] }).Append('|').Append(string.Join(",", r.Members)).Append('\n'); }
        foreach (InfraEdgeReport r in b.Infrastructure.Edges.Items) { sb.Append(r with { Inputs = [] }).Append('|'); In(r.Inputs); sb.Append('\n'); }
        foreach (ResourceReport r in b.Resources.Current.Items) { sb.Append(r with { Inputs = [] }).Append('|'); In(r.Inputs); sb.Append('\n'); }
        return sb.ToString();
    }

    [Fact]
    public void Teeth1_PaintingLeavesEveryReportUnchanged_Deeply()
    {
        var demo = Demo(M, 4);
        WorldSources b = demo.Sources();
        string before = DeepDump(b);
        Assert.Contains("capacity=", before, StringComparison.Ordinal);          // the dump does see list contents
        Assert.Contains("Ada Frey", before, StringComparison.Ordinal);
        WorldView v = WorldViewBuilder.Build(b, M);
        foreach (WorldScenario s in WorldPreview.Scenarios)
        {
            WorldFrame f = Paint(v, M, At(s.CenterX, s.CenterY, Math.Max(1, s.Zoom)), new WorldUiState { Selected = WorldPreview.Parse(s.Selected) });
            f.Hits.HitTest(640, 400);
            if (WorldPreview.Parse(s.Selected) is WorldEntityId id) WorldInspector.Details(v, M, id);
        }
        Assert.Equal(before, DeepDump(b));
    }

    [Fact]
    public void Teeth2_Declutter_IsInvariantUnderLargePans()
    {
        WorldView v = DemoView(M, 4);
        (bool Stacked, double Dx, double Dy)[] Placements(WorldProjection p)
        {
            double ppu = M.Sprite.PxPerUnit(p.PxPerWorldUnit);
            var g = new SceneGeometry(v, M, p, M.Lod.For(ppu), ppu);
            return v.Agents.Where(a => a.Drawable && a.Anchor is AgentAnchor.Position or AgentAnchor.Graph).Select(a =>
            {
                g.TryAgent(a.Id, out AgentPlacement ap);
                return ap.Stacked ? (true, ap.Label.X - ap.X, ap.Label.Y - ap.Y) : (false, 0.0, 0.0);   // label offset relative to its own agent
            }).ToArray();
        }
        var baseline = Placements(At(228, 198, 3.0));
        Assert.Contains(baseline, x => x.Stacked);
        // Pans of hundreds of pixels push the cluster to and past the viewport edge.
        foreach (double dx in new[] { -300.0, 150.0, 400.0 })
        {
            var panned = Placements(At(228 + dx / 3.0, 198 - dx / 6.0, 3.0));
            Assert.Equal(baseline.Select(x => x.Stacked), panned.Select(x => x.Stacked));   // membership: exactly invariant
            for (int i = 0; i < baseline.Length; i++)
            {
                // Offsets are differences of absolute screen coordinates taken under two camera
                // centres, so they agree to rounding, not to the bit.
                Assert.Equal(baseline[i].Dx, panned[i].Dx, 1e-9);
                Assert.Equal(baseline[i].Dy, panned[i].Dy, 1e-9);
            }
        }
    }
}
