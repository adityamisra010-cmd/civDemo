using Sim.Ui.Art;
using Sim.Ui.Render;

namespace Sim.Ui.Trees.View;

/// <summary>
/// THE HOST — owns one Trees/Ages screen: the loaded content, graph and layout, the
/// read-only state sources, the UI state, the animator and the forward-only Age guard.
/// The game (SimUiGame) and the headless preview both drive it the same way: Frame() to
/// paint, Handle() to route a clicked action.
///
/// It is the composition root for the screen and deliberately small: content loading is
/// TreesContentLoader's, state is the sources', painting is TreesScreen's. It never
/// references the simulation; the one live value it shows (the session clock) arrives as
/// a plain <see cref="SessionContext"/> from the caller.
/// </summary>
public sealed class TreesHost
{
    private readonly string _contentDir;
    private readonly ITreesStateSource? _treesOverride;
    private readonly IAgeStateSource? _agesOverride;
    private readonly IGalleryStateSource? _galleryOverride;

    public TreesHost(string contentDirectory, ITreesStateSource? trees = null, IAgeStateSource? ages = null, IGalleryStateSource? gallery = null)
    {
        _contentDir = contentDirectory;
        _treesOverride = trees;
        _agesOverride = ages;
        _galleryOverride = gallery;
        Reload();
    }

    public TreesContentSet? Content { get; private set; }
    public TreeGraph? Graph { get; private set; }
    public TreeLayoutResult? Layout { get; private set; }
    public DemoStateSource? Demo { get; private set; }
    public ITreesStateSource? TreesSource { get; private set; }
    public IAgeStateSource? AgeSource { get; private set; }
    public IGalleryStateSource? GallerySource { get; private set; }
    public IReadOnlyList<ContentDiagnostic> Diagnostics { get; private set; } = [];
    public TreesUiState Ui { get; private set; } = new();
    public StateAnimator? Animator { get; private set; }
    public AgeForwardGuard AgeGuard { get; private set; } = new();

    /// <summary>Set when the user asked to close; the caller clears it.</summary>
    public bool CloseRequested { get; set; }
    /// <summary>Set when the user clicked the search box; the caller gives it keyboard focus.</summary>
    public bool SearchFocusRequested { get; set; }

    public bool Loaded => Content is not null;

    /// <summary>Re-read ui-content from disk (the Director edits JSON; Reload shows it).
    /// Keeps the tab and lens; drops a selection that no longer exists.</summary>
    public void Reload()
    {
        ContentLoadResult r = TreesContentLoader.LoadDirectory(_contentDir);
        var diags = new List<ContentDiagnostic>(r.Diagnostics);
        TreesUiState old = Ui;
        Content = r.Content;
        Graph = null; Layout = null; Demo = null; Animator = null;
        AgeGuard = new AgeForwardGuard();
        if (Content is not null)
        {
            Graph = new TreeGraph(Content.Trees);
            Layout = TreeLayout.Compute(Graph);
            Animator = new StateAnimator(Content.Animations);
            ITreesStateSource? t = _treesOverride;
            IAgeStateSource? a = _agesOverride;
            IGalleryStateSource? g = _galleryOverride;
            if (t is null || a is null || g is null)
            {
                string demoPath = Path.Combine(_contentDir, TreesContentLoader.DemoStateFile);
                if (File.Exists(demoPath))
                {
                    (DemoStateSource? demo, IReadOnlyList<ContentDiagnostic> dd) = DemoStateSource.LoadFile(demoPath, Content);
                    diags.AddRange(dd);
                    Demo = demo;
                }
                var none = new NoStateSource(Content);
                t ??= (ITreesStateSource?)Demo ?? none;
                a ??= (IAgeStateSource?)Demo ?? none;
                g ??= (IGalleryStateSource?)Demo ?? none;
            }
            TreesSource = t; AgeSource = a; GallerySource = g;
        }
        Diagnostics = diags;

        Ui = new TreesUiState { Tab = old.Tab, Lens = old.Lens, LegendOpen = old.LegendOpen };
        if (Graph is not null && old.Selected is int s && s < Graph.Count) { Ui.Selected = s; Ui.LegendOpen = old.LegendOpen; }
        if (Content is not null && old.Lens is not null && !Content.Trees.Lenses.Any(l => l.Id == old.Lens)) Ui.Lens = null;
    }

    /// <summary>Paint one frame for a viewport of <paramref name="width"/> × <paramref name="height"/>
    /// design pixels at UI time <paramref name="now"/> (seconds).</summary>
    public TreesFrame Frame(double width, double height, double now, ITextMeasure measure, SessionContext? session = null)
    {
        if (Content is null || Graph is null || Layout is null || Animator is null || TreesSource is null || AgeSource is null || GallerySource is null)
            return ErrorFrame(width, height, measure);

        TreesStateSnapshot t = TreesSource.Current;
        AgeStateSnapshot a = AgeSource.Current;
        GallerySnapshot g = GallerySource.Current;
        Animator.Observe(Content.Trees, t, a, g, Content.Ages, Content.Gallery, now);
        AgeGuard.Observe(a, Content.Ages);
        TreesScreenLayout lay = TreesScreenLayout.For(width, height);
        if (!Ui.CameraInitialised) Ui.InitialView(Layout, lay.Canvas);

        var input = new TreesFrameInput(Content, Graph, Layout, t, a, g, Ui, Animator, now, width, height, measure, session,
            Demo?.StepLabel, Demo?.Step ?? 0, Demo?.StepCount ?? 0, Diagnostics, AgeGuard);
        return TreesScreen.Paint(input);
    }

    /// <summary>Route an action: UI actions change UI state; host actions are handled here.</summary>
    public void Handle(TreesAction action, TreesFrame frame)
    {
        switch (action)
        {
            case CloseAction: CloseRequested = true; return;
            case ReloadContentAction: Reload(); return;
            case FocusSearchAction: SearchFocusRequested = true; return;
            case PreviewStepAction p:
                if (Demo is not null && ReferenceEquals(TreesSource, Demo)) { if (p.Delta > 0) Demo.Next(); else Demo.Previous(); }
                return;
        }
        if (Graph is not null && Layout is not null) Ui.Apply(action, Graph, Layout, frame.Layout.Canvas);
    }

    /// <summary>Scroll the details panel, clamped to its content.</summary>
    public void ScrollDetails(double delta, TreesFrame frame)
    {
        double max = System.Math.Max(0, frame.DetailContentHeight - (frame.Layout.Detail.H - 50));
        Ui.DetailScroll = System.Math.Clamp(Ui.DetailScroll + delta, 0, max);
    }

    private TreesFrame ErrorFrame(double width, double height, ITextMeasure m)
    {
        var dl = new DrawList();
        var hits = new List<Hit>();
        TreesScreenLayout lay = TreesScreenLayout.For(width, height);
        dl.Rect(lay.Screen, ParchmentPalette.PaperMid);
        dl.Rect(lay.Header, ParchmentPalette.InkPrimary);
        dl.Text(20, 16, "THE TREES — CONTENT DID NOT LOAD", 20, ParchmentPalette.GoldLeaf, TextAlign.Left, FontRole.Caps);
        double y = 80;
        dl.Text(20, y, $"Content directory: {_contentDir}", 13, ParchmentPalette.InkPrimary);
        y += 26;
        foreach (ContentDiagnostic d in Diagnostics.Take(40))
        {
            var color = d.Severity == DiagnosticSeverity.Error ? ParchmentPalette.IronRed : ParchmentPalette.InkSoft;
            foreach (string line in Ink.Wrap(m, d.ToString(), 12.5, width - 40))
            {
                dl.Text(20, y, line, 12.5, color, TextAlign.Left, FontRole.Numeric);
                y += 17;
            }
        }
        var reload = new RectD(20, height - 50, 140, 30);
        Chrome.Button(dl, hits, reload, "Reload", false, new ReloadContentAction());
        var close = new RectD(170, height - 50, 140, 30);
        Chrome.Button(dl, hits, close, "Close", false, new CloseAction());
        return new TreesFrame(dl, hits, lay, default, null);
    }
}
