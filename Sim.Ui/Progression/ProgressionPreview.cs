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
        new("01-technology-default-open", "Technology tree as it OPENS: zoom 1, width fitted, scrolled to the least-developed branch's frontier.",
            (s, c) => { }),
        new("02-technology-hover-highlight", "Hover on a subtree node: prerequisites and their routed edges in orange, dependents in blue, the rest dimmed.",
            (s, c) => Hover(s, MostConnected(s, c, ResearchTree.Technology))),
        new("03-technology-target", "JUMP TO TARGET: the current research target (partial progress) selected, its links highlighted.",
            (s, c) => { if (s.Snapshot!.TargetIndex is int t) s.Focus(t, jump: true); }),
        new("04-technology-locked-subtree", "A subtree node locked by the research stage, with its Eureka list and university relevance.",
            (s, c) => s.Focus(FirstWithEurekaInSubtree(s, c), jump: true)),
        new("05-technology-overview", "FIT: the whole Technology tree as one vertical strip of tiers; lanes across the width.",
            (s, c) => { s.Tab = TreeTab.Technology; s.FitAll(); }),
        new("06-technology-trunk-collapsed", "Main Trunk lane collapsed via its header chip: its width goes to the subtrees.",
            (s, c) => { s.Tab = TreeTab.Technology; s.ToggleLane(LaneIndex(s, "main")); s.Paint(Width, Height, ApproxTextMeasure.Instance); s.ResetView(); }),
        new("07-civics-default-open", "The Civics tree as it opens; Technology prerequisites sit in their own lane as anchor tokens.",
            (s, c) => { s.Tab = TreeTab.Civics; }),
        new("08-civics-hover-highlight", "Civics hover highlight, including a Technology prerequisite anchor.",
            (s, c) => { s.Tab = TreeTab.Civics; Hover(s, MostConnected(s, c, ResearchTree.Civics)); }),
        new("09-lens-institutions", "INSTITUTIONS lens: adopted civics and knowledge-eligible institutions (real data).",
            (s, c) => s.SetLens(Lens.Institutions)),
        new("10-lens-industry", "INDUSTRY lens: honestly not yet simulated.",
            (s, c) => s.SetLens(Lens.Industry)),
    ];

    /// <summary>Scroll a node into view and put the pointer on it (hover).</summary>
    private static void Hover(ProgressionScreen s, int contentIndex)
    {
        s.Tab = s.Content.Nodes[contentIndex].Tree == ResearchTree.Civics ? TreeTab.Civics : TreeTab.Technology;
        s.Paint(Width, Height, ApproxTextMeasure.Instance);
        s.Focus(contentIndex, jump: true);
        s.Selected = -1;
        PlacedVertex p = s.Layout.Placed[s.Graph.VertexOf(contentIndex)];
        RectD c = s.Canvas;
        s.PointerMove(s.Camera.ToScreenX(p.CenterX, c.X), s.Camera.ToScreenY(p.CenterY, c.Y));
    }

    /// <summary>The node with the most cross-lane prerequisites, then most links (ties: lowest index).</summary>
    private static int MostConnected(ProgressionScreen s, ResearchContent c, ResearchTree tree)
    {
        ResearchGraph g = s.Graphs[tree == ResearchTree.Civics ? 1 : 0];
        TreeLayout L = s.Layouts[tree == ResearchTree.Civics ? 1 : 0];
        int best = 0; (int, int) key = (-1, -1);
        for (int v = 0; v < g.OwnCount; v++)
        {
            (int[] pre, int[] dep) = ResearchTreeLayout.Neighbours(g, v);
            int cross = 0;
            foreach (int u in pre) if (L.Placed[u].Lane != L.Placed[v].Lane) cross++;
            (int, int) k = (Math.Min(cross, 3), Math.Min(pre.Length + dep.Length, 8));
            if (k.Item1 > key.Item1 || (k.Item1 == key.Item1 && k.Item2 > key.Item2)) { key = k; best = g.Vertices[v].ContentIndex; }
        }
        return best;
    }

    private static int LaneIndex(ProgressionScreen s, string id)
    {
        foreach (LaneBox l in s.Layout.Lanes) if (l.Id == id) return l.Index;
        return 0;
    }

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
            var screen = new ProgressionScreen(content, LaborOrderFactory.PlayerEmpire)
            {
                // The era is derived from the stepped world's Age, exactly as the game derives it.
                Theme = Sim.Ui.Theme.EraThemes.For(Sim.Ui.Theme.UiEras.Of(session.World, session.Config.Ages, LaborOrderFactory.PlayerEmpire)),
            };
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
