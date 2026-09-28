using Sim.Ui.Render;
using Sim.Ui.World;
using Sim.Ui.World.Content;
using Sim.Ui.World.Demo;
using Sim.Ui.World.Scene;
using Sim.Ui.World.View;
using Xunit;
using static Sim.Ui.Tests.World.WorldTestKit;

namespace Sim.Ui.Tests.World;

/// <summary>Task Parts 7–12 and 15: one entity per report, continuous coordinates, counts,
/// layering, selection at every zoom, and a renderer that paints the same thing twice and
/// changes nothing.</summary>
public class WorldSceneTests
{
    private static readonly WorldMorphology M = Morph();
    private static WorldProjection At(double x, double y, double zoom) => new(x, y, zoom, 1280, 800);

    private static List<T> InSpan<T>(WorldFrame f, WorldLayer layer) where T : DrawCmd
    {
        LayerSpan s = f.Span(layer);
        return f.Draw.Commands.Skip(s.Start).Take(s.Count).OfType<T>().ToList();
    }

    // ------------------------------------------------------------ Part 8: one army, one entity

    [Theory]
    [InlineData(50L, "50")]
    [InlineData(500L, "500")]
    [InlineData(5000L, "5,000")]
    public void AnArmy_IsOneToken_OneCountLabel_OneHitRegion(long personnel, string label)
    {
        WorldView v = WorldViewBuilder.Build(Sources(agents: [AgentAt("army-184", "army", 100.25, 50.5, personnel)]), M);
        WorldFrame f = Paint(v, M, At(100, 50, 12));
        Assert.Single(v.Agents);
        Assert.Single(InSpan<GlyphCmd>(f, WorldLayer.MobileAgents));                         // one token, not `personnel` marks
        Assert.Single(f.Draw.Commands.OfType<TextCmd>(), t => t.Text == label);              // one count label
        Assert.Single(f.Hits.Regions, r => r.Id == Agent("army-184"));                       // one hit region
        Assert.Equal(personnel, v.Agents[0].Report.Count);                                   // personnel ≠ render entities
    }

    [Fact]
    public void TheDemoArmy184_ShowsFiveHundred_AndItsDetailsAreReportedStateOnly()
    {
        WorldView v = DemoView(M, 4);
        AgentView a = v.FindAgent(Agent("army-184"))!;
        Assert.Equal("500", a.CountLabel);
        InspectorDetails d = WorldInspector.Details(v, M, a.Id)!;
        Assert.Contains(d.Lines, l => l.Label == "Personnel" && l.Value == "500" && l.Tag == DetailTag.Reported);
        Assert.Contains(d.Lines, l => l.Label == "Owner" && l.Value.Contains("Aurel", StringComparison.Ordinal));
        Assert.Contains(d.Lines, l => l.Label == "Position" && l.Value == "on Veyra - Oskar road at 20%");   // D-009: on the graph
        Assert.Contains(d.Lines, l => l.Label == "Map point (projected)" && l.Tag == DetailTag.View);
        foreach (string banned in new[] { "strength", "speed", "morale", "attack", "defen", "movement" })
            Assert.DoesNotContain(d.Lines, l => l.Label.Contains(banned, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("DEMO", d.Provenance, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ Part 10: people and groups

    [Fact]
    public void TheBand_IsOneEntity_WithFourMembers()
    {
        WorldView v = DemoView(M, 4);
        AgentView band = v.FindAgent(Agent("band-1"))!;
        Assert.Equal("band", band.Type.Id);
        Assert.Equal("group", band.Type.Category);
        Assert.Equal(4, band.Report.Count);
        Assert.Equal(4, band.Report.Members.Count);
        WorldFrame f = Paint(v, M, At(band.World.X, band.World.Y, 16));
        Assert.Single(f.Hits.Regions.Select(r => r.Id).Distinct(), id => id == band.Id);
        Assert.Single(InSpan<TextCmd>(f, WorldLayer.MobileAgents), t => t.Text == "4");
    }

    [Fact]
    public void PeopleCategories_ReadApart_ByTheirSilhouette()
    {
        Assert.Equal(Sim.Ui.Art.Glyphs.GlyphBase.Standard, M.Agent("army")!.Glyph.Base);
        Assert.Equal(Sim.Ui.Art.Glyphs.GlyphBase.Emblem, M.Agent("scientist")!.Glyph.Base);
        Assert.Equal(Sim.Ui.Art.Glyphs.GlyphBase.Node, M.Agent("band")!.Glyph.Base);
        var marks = new[] { "scientist", "engineer", "artist", "musician", "hero", "leader" }.Select(t => M.Agent(t)!.Glyph.Mark).ToList();
        Assert.Equal(marks.Count, marks.Distinct().Count());
    }

    // ------------------------------------------------------------ Part 9: continuous coordinates

    [Fact]
    public void Positions_AreContinuous_AndProjectLinearly()
    {
        WorldView v = WorldViewBuilder.Build(Sources(agents: [AgentAt("a", "army", 10.123456789, 20.987654321)]), M);
        Assert.Equal(new WorldPoint(10.123456789, 20.987654321), v.Agents[0].World);
        var proj = At(0, 0, 3.7);
        WorldFrame f = Paint(v, M, proj);
        HitRegion r = f.Hits.Regions.Single(h => h.Id == Agent("a") && h.R > 0);
        Assert.Equal((10.123456789 - 0) * 3.7 + 640, r.X);
        Assert.Equal((20.987654321 - 0) * 3.7 + 400, r.Y);
    }

    [Fact]
    public void AGraphLocation_ProjectsAlongItsEdge()
    {
        WorldView v = DemoView(M, 3);
        AgentView f = v.FindAgent(Agent("formation-21"))!;
        EdgeView e = v.FindEdge(new WorldEntityId(WorldEntityKind.InfraEdge, "e-veyra-oskar"))!;
        Assert.Equal(AgentAnchor.Graph, f.Anchor);
        Assert.Equal(e.A.X + (e.B.X - e.A.X) * 0.35, f.World.X);
        Assert.Equal(e.A.Y + (e.B.Y - e.A.Y) * 0.35, f.World.Y);
    }

    [Fact]
    public void AnUnresolvableAgent_IsNotDrawn_NeverPlacedAtADefault()
    {
        var lost = new AgentReport("lost", "Lost", "army", null, PolityRelation.None, null, new AgentGraphLocation("no-such-edge", 0.5), null,
            null, 10, null, [], ReportedVisibility.Visible, "t");
        WorldView v = WorldViewBuilder.Build(Sources(agents: [lost]), M);
        Assert.False(v.Agents[0].Drawable);
        Assert.Equal(AgentAnchor.Unresolved, v.Agents[0].Anchor);
        Assert.Contains(v.Notes, n => n.Contains("no resolvable location", StringComparison.Ordinal));
        Assert.Empty(Paint(v, M, At(0, 0, 10)).Hits.Regions);
    }

    [Fact]
    public void SettlementAttachedPeople_FanOutsideTheSettlementsClickTarget()
    {
        WorldView v = DemoView(M, 4);
        foreach (double zoom in new[] { 2.5, 9.0, 25.0 })
        {
            WorldFrame f = Paint(v, M, At(150, 190, zoom));
            HitRegion s = f.Hits.Regions.Single(r => r.Id == Settlement("s-veyra"));
            foreach (string key in new[] { "lead-1", "person-1" })
            {
                HitRegion a = f.Hits.Regions.First(r => r.Id == Agent(key));
                double d = Math.Sqrt((a.X - s.X) * (a.X - s.X) + (a.Y - s.Y) * (a.Y - s.Y));
                Assert.True(d > s.R + a.R, $"{key} overlaps the settlement's target at zoom {zoom}");
            }
            Assert.Equal(Settlement("s-veyra"), f.Hits.HitTest(s.X, s.Y));   // the settlement stays clickable
        }
    }

    // ------------------------------------------------------------ Part 11: layering

    [Fact]
    public void Layers_AreContiguous_InTheFixedOrder()
    {
        WorldView v = DemoView(M, 4);
        WorldFrame f = Paint(v, M, At(157, 196, 25), new WorldUiState { Selected = Structure("u-123") });
        int at = 0;
        for (int i = 0; i < f.Layers.Count; i++)
        {
            Assert.Equal((WorldLayer)i, f.Layers[i].Layer);
            Assert.Equal(at, f.Layers[i].Start);
            at += f.Layers[i].Count;
        }
        Assert.Equal(f.Draw.Commands.Count, at);
        Assert.NotEmpty(InSpan<GlyphCmd>(f, WorldLayer.Buildings));
        Assert.NotEmpty(InSpan<RectCmd>(f, WorldLayer.SettlementFootprint));
        Assert.NotEmpty(InSpan<DrawCmd>(f, WorldLayer.Selection));
        Assert.Contains(InSpan<TextCmd>(f, WorldLayer.TransientUi), t => t.Text.StartsWith("DEMO / PLACEHOLDER", StringComparison.Ordinal));
        Assert.Contains(InSpan<TextCmd>(f, WorldLayer.Labels), t => t.Text == "Veyra");
        // Every building glyph comes after every footprint command and before every label.
        Assert.True(f.Span(WorldLayer.Buildings).Start >= f.Span(WorldLayer.SettlementFootprint).Start + f.Span(WorldLayer.SettlementFootprint).Count);
    }

    // ------------------------------------------------------------ Part 12: selection

    [Fact]
    public void Selection_HitsTheSameEntity_AtEveryZoom()
    {
        WorldView v = DemoView(M, 4);
        AgentView army = v.FindAgent(Agent("army-184"))!;
        foreach (double zoom in new[] { 2.5, 7.6, 12, 25 })
        {
            var proj = At(army.World.X + 3, army.World.Y - 2, zoom);
            WorldFrame f = Paint(v, M, proj);
            (double sx, double sy) = proj.ToScreen(army.World);
            Assert.Equal(army.Id, f.Hits.HitTest(sx, sy));
        }
    }

    [Fact]
    public void Selection_OfABuilding_UsesItsSlot_AndAMissSelectsNothing()
    {
        WorldView v = DemoView(M, 4);
        var proj = At(157, 196, 25);
        WorldFrame f = Paint(v, M, proj);
        StructureView u = v.FindStructure(Structure("u-123"))!;
        (double sx, double sy) = proj.ToScreen(v.Settlement("s-veyra")!.Report.Position);
        double px = sx + u.Slot!.Value.X * f.PxPerUnit, py = sy + u.Slot.Value.Y * f.PxPerUnit;
        Assert.Equal(u.Id, f.Hits.HitTest(px, py));
        Assert.Null(f.Hits.HitTest(5, 400));
    }

    /// <summary>CLAUDE.md tie-dense rule: k tokens at bit-identical positions — the hit is the
    /// lowest id, and the lowest id is the one drawn LAST (on top) in the agent layer.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(9)]
    public void CoincidentTokens_HitAndDrawTheLowestIdOnTop(int k)
    {
        // The lowest id is the only engineer, so its glyph is recognisable in the draw order.
        var agents = Enumerable.Range(1, k).Select(i => AgentAt($"a{i:D2}", i == 1 ? "engineer" : "scientist", 42.5, 17.25))
            .OrderByDescending(r => r.Key, StringComparer.Ordinal).ToArray();
        WorldView v = WorldViewBuilder.Build(Sources(agents: agents), M);
        WorldFrame f = Paint(v, M, At(42.5, 17.25, 10));
        Assert.Equal(Agent("a01"), f.Hits.HitTest(640, 400));
        List<GlyphCmd> tokens = InSpan<GlyphCmd>(f, WorldLayer.MobileAgents);
        Assert.Equal(k, tokens.Count);
        Assert.Equal(M.Agent("engineer")!.Glyph.Mark, tokens[^1].Spec.Domain);   // drawn last = on top
        Assert.All(tokens.Take(k - 1), t => Assert.Equal(M.Agent("scientist")!.Glyph.Mark, t.Spec.Domain));
    }

    [Fact]
    public void RememberedAndHidden_AreNotSelectable_AndHiddenIsNotDrawn()
    {
        WorldView v = DemoView(M, 4);
        WorldFrame f = Paint(v, M, WorldProjection.Fit(480, 300, 1280, 800));
        Assert.DoesNotContain(f.Hits.Regions, r => r.Id == Agent("army-9"));    // remembered: drawn ghosted
        Assert.DoesNotContain(f.Hits.Regions, r => r.Id == Agent("army-31"));   // hidden
        Assert.DoesNotContain(f.Draw.Commands.OfType<TextCmd>(), t => t.Text.Contains("Army #31", StringComparison.Ordinal));
        Assert.False(v.FindAgent(Agent("army-31"))!.Drawable);
    }

    [Fact]
    public void Declutter_IsPanInvariant()
    {
        WorldView v = DemoView(M, 4);
        (double X, double Y, bool Stacked, RectD Label)[] Placements(WorldProjection p)
        {
            var g = new SceneGeometry(v, M, p, M.Lod.For(M.Sprite.PxPerUnit(p.PxPerWorldUnit)), M.Sprite.PxPerUnit(p.PxPerWorldUnit));
            return v.Agents.Where(a => a.Drawable).Select(a => g.TryAgent(a.Id, out AgentPlacement ap)
                ? (ap.X - p.ToScreen(a.World).X, ap.Y - p.ToScreen(a.World).Y, ap.Stacked, ap.Label with { X = ap.Label.X - p.ToScreen(a.World).X, Y = ap.Label.Y - p.ToScreen(a.World).Y })
                : (0, 0, false, default)).ToArray();
        }
        var a = Placements(At(228, 198, 3.0));
        var b = Placements(At(228.0001, 198.0003, 3.0));
        Assert.Equal(a.Select(x => x.Stacked), b.Select(x => x.Stacked));
        Assert.Contains(a, x => x.Stacked);   // the overlapping pair (and friends) stack at this zoom
    }

    // ------------------------------------------------------------ Part 15: determinism, no mutation

    [Fact]
    public void RenderingTwice_IsBitIdentical_AndSvgIdentical()
    {
        foreach (WorldScenario s in WorldPreview.Scenarios)
        {
            DemoWorldSource demo = Demo(M);
            (WorldFrame a, _) = WorldPreview.Paint(s, M, demo, ApproxTextMeasure.Instance);
            (WorldFrame b, _) = WorldPreview.Paint(s, M, Demo(M), ApproxTextMeasure.Instance);
            Assert.Equal(Dump(a.Draw), Dump(b.Draw));
            Assert.Equal(SvgWriter.Write(a.Draw, 1280, 800), SvgWriter.Write(b.Draw, 1280, 800));
        }
    }

    [Fact]
    public void ChangingOnlyTheDemoStep_ChangesThePicture_AndNothingElse()
    {
        DemoWorldSource demo = Demo(M, 3);
        var ui = new WorldUiState { Selected = Structure("u-123") };
        var proj = At(157, 196, 25);
        string a = Dump(Paint(WorldViewBuilder.Build(demo.Sources(), M), M, proj, ui).Draw);
        demo.SetStep(4);
        string b = Dump(Paint(WorldViewBuilder.Build(demo.Sources(), M), M, proj, ui).Draw);
        Assert.NotEqual(a, b);
        Assert.Equal(Structure("u-123"), ui.Selected);   // painting never touched the UI state
        Assert.Null(ui.Hovered);
        Assert.Equal(4, demo.Step);                        // nor the source
    }

    // Painting leaves the reports unchanged: pinned DEEPLY (list contents, doubles as bits) by
    // WorldReviewRegressionTests.Teeth1_PaintingLeavesEveryReportUnchanged_Deeply — the record
    // ToString comparison that used to stand here could not see a list changing (review teeth-1).

    [Fact]
    public void AMissingSelection_IsKept_DrawsNoRing_AndSaysNotReported()
    {
        WorldView v = DemoView(M, 0);   // University #123 does not exist yet at step A
        var ui = new WorldUiState { Selected = Structure("u-123") };
        WorldFrame f = Paint(v, M, At(157, 196, 25), ui);
        Assert.Empty(InSpan<DrawCmd>(f, WorldLayer.Selection));
        Assert.Contains(InSpan<TextCmd>(f, WorldLayer.TransientUi), t => t.Text == "not reported in the current state");
        Assert.Equal(Structure("u-123"), ui.Selected);
    }

    [Fact]
    public void Lod_SwitchesAtTheContentThresholds_AndNeverMovesABuilding()
    {
        WorldView v = DemoView(M, 4);
        var seen = new HashSet<WorldLod>();
        foreach (double zoom in new[] { 2.0, M.Lod.MidFromPxPerUnit, M.Lod.NearFromPxPerUnit - 0.01, M.Lod.NearFromPxPerUnit, 28 })
        {
            WorldFrame f = Paint(v, M, At(150, 190, zoom));
            seen.Add(f.Lod);
            if (f.Lod == WorldLod.Far) Assert.Empty(InSpan<GlyphCmd>(f, WorldLayer.Buildings));   // detail is added with size
        }
        Assert.Equal(3, seen.Count);
        // Slots are view state, computed once per view: no projection is an input to them.
        Assert.Equal(v.Structures.Select(s => s.Slot), DemoView(M, 4).Structures.Select(s => s.Slot));
    }

    [Fact]
    public void EveryPaintedString_IsLatin1_WithNothingReplaced()
    {
        // DrawList maps anything outside Latin-1 to '?': a '?' in a painted string means a
        // report or content string was not Latin-1 clean (the demo and the content use none).
        foreach (WorldScenario s in WorldPreview.Scenarios)
        {
            (WorldFrame f, _) = WorldPreview.Paint(s, M, Demo(M), ApproxTextMeasure.Instance);
            foreach (TextCmd t in f.Draw.Commands.OfType<TextCmd>())
            {
                Assert.All(t.Text, ch => Assert.True(ch <= '\u00FF'));
                Assert.DoesNotContain("?", t.Text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void EveryScenario_CarriesTheDemoBanner_AndItsExpectedSelection()
    {
        foreach (WorldScenario s in WorldPreview.Scenarios)
        {
            (WorldFrame f, WorldView v) = WorldPreview.Paint(s, M, Demo(M), ApproxTextMeasure.Instance);
            Assert.Contains(f.Draw.Commands.OfType<TextCmd>(), t => t.Text == "DEMO / PLACEHOLDER WORLD - not simulation output");
            if (WorldPreview.Parse(s.Selected) is WorldEntityId id) Assert.True(v.Contains(id), $"{s.File}: {id} not in step {s.Step}");
        }
    }
}
