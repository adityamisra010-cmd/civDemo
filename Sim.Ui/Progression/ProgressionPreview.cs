using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Progression;

/// <summary>
/// HEADLESS PREVIEW of the progression screen (sim-ui --research-preview [dir]) — a REAL founded
/// world, played forward through the real session and order pathway (no placeholder state):
/// each time the player has no target, the screen-equivalent order for the cheapest available
/// Technology node is logged and End Turn is pressed, until <see cref="CompletedGoal"/> nodes are
/// complete; then one more target is set and played part-way. The screens are painted with the
/// same <see cref="ProgressionScreen"/> the game runs and written as SVG through
/// <see cref="SvgWriter"/>. Deterministic for a fixed seed.
/// </summary>
public static class ProgressionPreview
{
    public const double Width = 1600, Height = 1000;
    public const int CompletedGoal = 5;

    /// <summary>The stepped world: some completed nodes, one target with partial progress.</summary>
    public static UiSession SteppedSession(ulong seed = 42, int maxTurns = 400)
    {
        var session = UiSession.Start(seed, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent content = session.Config.Research!;
        PolityId me = LaborOrderFactory.PlayerEmpire;
        for (int turn = 0; turn < maxTurns; turn++)
        {
            int done = ResearchQuery.CompletedNodes(session.World, content, me).Length;
            bool hasTarget = ResearchQuery.TryGetTarget(session.World, me, out _);
            if (done >= CompletedGoal && hasTarget && ResearchQuery.PartialProgress(session.World, me).Length > 0) break;
            if (!hasTarget && ResearchQuery.CheapestAvailable(session.World, content, me, ResearchTree.Technology) is ResearchNodeId next)
                session.EmitResearchOrder(next);
            session.EndTurn();
        }
        return session;
    }

    public sealed record Shot(string Name, string Description, Action<ProgressionScreen, ResearchContent> Arrange);

    public static IReadOnlyList<Shot> Shots { get; } =
    [
        new("01-technology-frontier", "Technology tree opened on the frontier; the current target (partial progress) selected.",
            (s, c) => { if (s.Snapshot!.TargetIndex is int t) s.Focus(t, jump: true); }),
        new("02-technology-overview", "The whole Technology tree fitted to the canvas: Main trunk + five subtree lanes, Age bands.",
            (s, c) => { s.Tab = TreeTab.Technology; s.FitAll(); }),
        new("03-technology-locked-subtree", "A subtree node locked by the research stage, with its Eureka list and university relevance.",
            (s, c) => s.Focus(FirstWithEurekaInSubtree(s, c), jump: true)),
        new("04-civics-tree", "The Civics tree: a separate graph; Technology prerequisites appear as anchor tokens.",
            (s, c) => { s.Tab = TreeTab.Civics; s.FitAll(); s.Selected = c.TechnologyCount; }),
        new("05-lens-institutions", "INSTITUTIONS lens: adopted civics and knowledge-eligible institutions (real data).",
            (s, c) => s.SetLens(Lens.Institutions)),
        new("06-lens-industry", "INDUSTRY lens: honestly not yet simulated.",
            (s, c) => s.SetLens(Lens.Industry)),
    ];

    private static int FirstWithEurekaInSubtree(ProgressionScreen s, ResearchContent c)
    {
        int best = -1, bestDepth = int.MaxValue;
        for (int i = 0; i < c.TechnologyCount; i++)
        {
            ResearchNode n = c.Nodes[i];
            if (n.Branch >= 0 && n.Eurekas.Count > 0 && n.UniversityRelevance.Count > 0 && n.Depth < bestDepth) { best = i; bestDepth = n.Depth; }
        }
        return best >= 0 ? best : 0;
    }

    public static IReadOnlyList<string> Run(string outDir, string? fontDir)
    {
        Directory.CreateDirectory(outDir);
        UiSession session = SteppedSession();
        ResearchContent content = session.Config.Research!;
        var written = new List<string>();
        foreach (Shot shot in Shots)
        {
            var screen = new ProgressionScreen(content, LaborOrderFactory.PlayerEmpire);
            screen.Refresh(session.World);
            screen.Paint(Width, Height, ApproxTextMeasure.Instance);   // first frame: frames the frontier
            shot.Arrange(screen, content);
            DrawList list = screen.Paint(Width, Height, ApproxTextMeasure.Instance);
            string path = Path.Combine(outDir, shot.Name + ".svg");
            File.WriteAllText(path, SvgWriter.Write(list, Width, Height, fontDir));
            written.Add(path);
        }
        return written;
    }
}
