using Sim.Ui.Render;
using Sim.Ui.World.Content;
using Sim.Ui.World.Demo;
using Sim.Ui.World.View;

namespace Sim.Ui.World.Scene;

/// <summary>One deterministic screenshot: a demo step, a camera and a selection.</summary>
/// <param name="Zoom">Screen pixels per world unit; 0 = fit the whole demo world.</param>
public sealed record WorldScenario(string File, string Title, int Step, double CenterX, double CenterY, double Zoom, string? Selected, string Expect);

/// <summary>
/// THE WORLD PREVIEW (docs/architecture/world-visualization.md §10) — paints the scenario
/// table to SVG with no window and no simulation: `sim-ui --world-preview [dir]`, then
/// scripts/world-preview.sh screenshots each SVG with headless Chromium. The scenarios are
/// DEMO / PLACEHOLDER content only and every frame carries the DEMO banner. Deterministic:
/// the same content paints byte-identical SVG.
/// </summary>
public static class WorldPreview
{
    public const double Width = 1280, Height = 800;

    public static readonly WorldScenario[] Scenarios =
    [
        new("01-small-settlement", "Small settlement", 0, 157, 196, 25, "settlement:s-veyra",
            "State A: 6 blocks, no institutions; the stage comes from a DEMONSTRATION population threshold"),
        new("02-growing-settlement", "Growing settlement", 1, 157, 196, 25, "settlement:s-veyra",
            "More blocks on the same fixed lattice; nothing that existed moved"),
        new("03-first-university", "City with its first university", 2, 157, 196, 25, "structure:u-123",
            "State B: University #123 at stage 1 (main building), Hospital #7, granaries and a workshop"),
        new("04-university-expanded", "University expanded", 3, 157, 196, 25, "structure:u-123",
            "University #123 at stage 2 (wings added, main building unchanged in place)"),
        new("05-universities-across-cities", "Multiple universities across cities", 4, 178, 168, 7.6, null,
            "Mid detail: university icons in several cities; the legend counts 20 reported universities"),
        new("06-developed-city", "Developed city, several institution types", 4, 157, 196, 25, "structure:h-7",
            "State D: campus, medical complex, factories, academy, institute, reservoir"),
        new("07-army-500", "Army with 500 personnel", 4, 184, 184, 12, "agent:army-184",
            "One token, one '500' label, one hit region; details show personnel, owner, position only"),
        new("08-mobile-agents", "Several mobile agents", 4, 228, 198, 16, "agent:band-1",
            "People and groups as single tokens; the overlapping pair's labels stack"),
        new("09-mixed-scene", "City, military and people", 4, 190, 192, 9.5, "agent:formation-21",
            "A formation on the road graph, armies with counts, persons, a band and the city"),
        new("10-civilization", "Zoomed-out civilization", 5, 0, 0, 0, null,
            "Far detail: every settlement as a footprint; 30 reported universities in the legend"),
    ];

    public static (WorldMorphology Morph, DemoWorldSource Demo) Load(string contentDir)
    {
        (WorldMorphology? morph, IReadOnlyList<WorldDiagnostic> md) = WorldContentLoader.LoadMorphologyFile(Path.Combine(contentDir, WorldContentLoader.MorphologyFile));
        if (morph is null) throw new InvalidDataException("morphology.json did not load: " + string.Join("; ", md));
        (DemoWorldSource? demo, IReadOnlyList<WorldDiagnostic> dd) = DemoWorldSource.LoadFile(Path.Combine(contentDir, WorldContentLoader.DemoFile), morph);
        if (demo is null) throw new InvalidDataException("demo-world.json did not load: " + string.Join("; ", dd));
        return (morph, demo);
    }

    /// <summary>Paint one scenario. The demo source's step is set to the scenario's.</summary>
    public static (WorldFrame Frame, WorldView View) Paint(WorldScenario s, WorldMorphology morph, DemoWorldSource demo, ITextMeasure measure)
    {
        demo.SetStep(s.Step);
        WorldView view = WorldViewBuilder.Build(demo.Sources(), morph);
        WorldProjection proj = s.Zoom <= 0
            ? WorldProjection.FitInto(demo.Width, demo.Height, 352, 74, Width - 364, Height - 86, Width, Height)
            : new WorldProjection(s.CenterX, s.CenterY, s.Zoom, Width, Height);
        var ui = new WorldUiState { Selected = Parse(s.Selected) };
        DemoStep step = demo.CurrentStep;
        WorldFrame frame = WorldScene.Paint(view, morph, proj, ui, WorldSceneOptions.Full, measure,
            $"step {step.Id} ({demo.Step + 1} of {demo.StepCount}): {step.Name}", demo.Backdrop);
        return (frame, view);
    }

    public static IReadOnlyList<string> Run(string outDir, string contentDir, string? fontDir)
    {
        Directory.CreateDirectory(outDir);
        (WorldMorphology morph, DemoWorldSource demo) = Load(contentDir);
        var written = new List<string>();
        foreach (WorldScenario s in Scenarios)
        {
            (WorldFrame frame, _) = Paint(s, morph, demo, ApproxTextMeasure.Instance);
            string path = Path.Combine(outDir, s.File + ".svg");
            File.WriteAllText(path, SvgWriter.Write(frame.Draw, Width, Height, fontDir));
            written.Add(path);
        }
        return written;
    }

    /// <summary>"kind:key" → an id (the scenario table's notation).</summary>
    public static WorldEntityId? Parse(string? text)
    {
        if (text is null) return null;
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        string kind = text[..colon], key = text[(colon + 1)..];
        return kind switch
        {
            "settlement" => new WorldEntityId(WorldEntityKind.Settlement, key),
            "structure" => new WorldEntityId(WorldEntityKind.Structure, key),
            "node" => new WorldEntityId(WorldEntityKind.InfraNode, key),
            "edge" => new WorldEntityId(WorldEntityKind.InfraEdge, key),
            "resource" => new WorldEntityId(WorldEntityKind.Resource, key),
            "agent" => new WorldEntityId(WorldEntityKind.Agent, key),
            _ => throw new ArgumentException($"unknown entity kind in '{text}'"),
        };
    }
}
