using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 workstream P6 — HEADLESS OBSERVABILITY. <see cref="GameUi.FrameText"/> is every string the last frame drew (the
/// chrome's ImGui text and widget labels through GameUi's recording helpers, the runs added straight to a draw list,
/// every TextCmd the DrawList backend replayed), by surface; <see cref="GameUi.LastLensCommands"/> is the world lens as
/// the last frame painted it. These tests pin what they show, that they only observe, and that they are COMPLETE — twice
/// over: a source guard (no ImGui text or widget call in Sim.Ui outside the helpers, or an allowlisted, counted, reasoned
/// site; each bypass shape a mutant the guard catches) and ImGui's own text log of real frames on every surface
/// (whatever ImGui rendered as text must be in FrameText; a bypassing call in a real frame is caught).
/// </summary>
[Collection("ImGui context")]
public class FrameTextTests : IDisposable
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-frametext-tests-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    private string WorkDir(string name) => Path.Combine(_root, name);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    /// <summary>Whether <paramref name="text"/> carries <paramref name="word"/> as a whole word (case-insensitive: an era
    /// style may set a name in capitals; whole-word: a short name must not match inside "Turn").</summary>
    private static bool HasWord(string text, string word) =>
        Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(word) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool AnyHasWord(IEnumerable<string> texts, string word)
    {
        foreach (string t in texts) if (HasWord(t, word)) return true;
        return false;
    }

    // ================================================================== the log itself

    [Fact]
    public void FrameTextLog_KeepsTheLastCompletedFrame_InDrawOrder_AndIgnoresEmptyStrings()
    {
        var log = new FrameTextLog();
        log.BeginFrame();
        log.Add(FrameSurface.Status, FrameTextSource.Replayed, "Turn");
        log.Add(FrameSurface.Selection, FrameTextSource.Replayed, "Ashkelon");
        log.Add(FrameSurface.Context, FrameTextSource.ImGuiText, "");
        Assert.Empty(log.Last);                                   // nothing is published before the frame completes
        log.EndFrame();
        Assert.Equal(["Turn", "Ashkelon"], log.Texts());
        Assert.Equal(["Ashkelon"], log.Texts(FrameSurface.Selection));
        Assert.True(log.Contains("ASHKELON", FrameSurface.Selection));   // case-insensitive by default
        Assert.False(log.Contains("Ashkelon", FrameSurface.Status));
        Assert.False(log.Contains("ASHKELON", null, StringComparison.Ordinal));
        log.BeginFrame();
        log.Add(FrameSurface.Command, FrameTextSource.DrawListText, "End Turn");
        Assert.Equal(["Turn", "Ashkelon"], log.Texts());         // the building frame does not leak into Last
        log.EndFrame();
        Assert.Equal([new FrameTextEntry(FrameSurface.Command, FrameTextSource.DrawListText, "End Turn")], log.Last);
    }

    [Fact]
    public void VisibleLabel_IsWhatImGuiDraws_TheTextBeforeTheIdSeparator()
    {
        Assert.Equal("open POLICY", GameUi.VisibleLabel("open POLICY##lever-need-1-2"));
        Assert.Equal("", GameUi.VisibleLabel("##end-turn"));
        Assert.Equal("a", GameUi.VisibleLabel("a###b"));
        Assert.Equal("territory", GameUi.VisibleLabel("territory"));
        Assert.Equal("x # y", GameUi.VisibleLabel("x # y##id"));
    }

    // ================================================================== what the live UI drew

    [Fact]
    public void SelectionCard_FrameTextNamesTheSelectedSettlement_AndFollowsTheSelection()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("sel"), Assets());
        int first = h.Ui.SelectedSettlement;
        Assert.True(first >= 0);
        string firstName = h.Ui.Session.Names.Name(first);
        Assert.Contains(h.Ui.FrameText.Last, e => e.Surface == FrameSurface.Selection && e.Source == FrameTextSource.Replayed && e.Text == firstName);

        h.Key(Keys.Tab);
        int second = h.Ui.SelectedSettlement;
        Assert.NotEqual(first, second);
        string secondName = h.Ui.Session.Names.Name(second);
        Assert.False(HasWord(secondName, firstName) || HasWord(firstName, secondName), "the fixture needs two distinct names");
        IReadOnlyList<string> card = h.Ui.FrameText.Texts(FrameSurface.Selection);
        Assert.True(AnyHasWord(card, secondName), "the card does not name the new selection: " + string.Join(" | ", card));
        Assert.False(AnyHasWord(card, firstName), "the card still names the old selection: " + string.Join(" | ", card));
        Assert.Empty(h.Problems);
    }

    /// <summary>Nothing is selected only in a world without settlements (selection is never cleared otherwise): the
    /// rig takes the canonical world's settlement rows away. No surface then draws any of their names, and the
    /// selection card still draws (its title is the HUD's unnamed fallback).</summary>
    [Fact]
    public void NothingSelected_NoSurfaceDrawsASettlementName()
    {
        UiSession canonical = UiSession.Start(42);
        var names = new List<string>();
        for (int i = 0; i < canonical.World.Settlements.Count; i++) names.Add(canonical.Names.Name(canonical.World.Settlements[i].Id.Value));
        Assert.True(names.Count > 1);
        WorldState w = canonical.World.Clone();
        w.Settlements.Clear();

        using UiFrameHarness h = UiFrameHarness.Start(UiSession.StartFrom(w, 42), WorkDir("none"), Assets());
        h.Idle(3);
        Assert.Equal(-1, h.Ui.SelectedSettlement);
        Assert.NotEmpty(h.Ui.FrameText.Texts(FrameSurface.Selection));
        Assert.NotEmpty(h.Ui.FrameText.Texts(FrameSurface.Status));
        foreach (string name in names)
            Assert.False(AnyHasWord(h.Ui.FrameText.Texts(), name), name + " drawn with nothing selected");
        // The control: the same names ARE found by the same search on the canonical world's frame.
        using UiFrameHarness c = UiFrameHarness.Start(canonical, WorkDir("none-control"), Assets());
        Assert.True(AnyHasWord(c.Ui.FrameText.Texts(FrameSurface.Selection), canonical.Names.Name(c.Ui.SelectedSettlement)));
    }

    [Fact]
    public void FrameText_RecordsEverySource_EachUnderTheSurfaceThatDrewIt()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("sources"), Assets());
        IReadOnlyList<FrameTextEntry> map = h.Ui.FrameText.Last;
        // The command bar: End Turn's label is added straight to the draw list; the territory toggle is a widget label.
        Assert.Contains(map, e => e.Surface == FrameSurface.Command && e.Source == FrameTextSource.DrawListText && e.Text == "End Turn");
        Assert.Contains(map, e => e.Surface == FrameSurface.Command && e.Source == FrameTextSource.WidgetLabel && e.Text == "territory");
        // The status band's figures are era-styled runs replayed by the backend.
        foreach (string label in new[] { "Turn", "Year", "Population", "Food" })
            Assert.Contains(map, e => e.Surface == FrameSurface.Status && e.Source == FrameTextSource.Replayed && e.Text == label);
        // The lens replays the map's labels.
        Assert.Contains(map, e => e.Surface == FrameSurface.Lens && e.Source == FrameTextSource.Replayed);

        h.ClickControl("nav:Empire");
        Assert.Contains(h.Ui.FrameText.Last, e => e.Surface == FrameSurface.Context && e.Source == FrameTextSource.ImGuiText);
        h.ClickControl("nav:Trends");
        Assert.Contains(h.Ui.FrameText.Last, e => e.Surface == FrameSurface.Context && e.Source == FrameTextSource.WidgetLabel && e.Text == "world");
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void LastLensCommands_AreTheLensPaintedLastFrame_AndEmptyUnderTheResearchScreen()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("lens"), Assets());
        foreach (int notches in new[] { 0, 6, 12 })
        {
            if (notches > 0) h.Wheel(h.Width / 2.0, h.Height / 2.0 + 30, 6);
            IReadOnlyList<DrawCmd> lens = h.Ui.LastLensCommands;
            Assert.NotEmpty(lens);
            // Every lens string the backend replayed is one of the lens's TextCmds, in paint order (the cull drops only
            // off-screen commands): the two read-only views agree.
            var painted = new List<string>();
            foreach (DrawCmd c in lens) if (c is TextCmd t && t.Text.Length > 0) painted.Add(t.Text);
            int at = 0;
            foreach (string s in h.Ui.FrameText.Texts(FrameSurface.Lens))
            {
                while (at < painted.Count && painted[at] != s) at++;
                Assert.True(at < painted.Count, "'" + s + "' replayed under the lens but not among its commands");
                at++;
            }
        }
        // At the closest zoom the selected settlement's name is in the lens's own text.
        string selected = h.Ui.Session.Names.Name(h.Ui.SelectedSettlement);
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, h.Ui.SelectedSettlement, h.Width, h.Height);
        h.Idle(2);
        Assert.Contains(h.Ui.LastLensCommands, c => c is TextCmd t && t.Text == selected);

        h.ClickControl("band-research");
        Assert.True(h.Ui.ProgressionOpen);
        Assert.Empty(h.Ui.LastLensCommands);
        Assert.Empty(h.Ui.FrameText.Texts(FrameSurface.Lens));
        Assert.NotEmpty(h.Ui.FrameText.Texts(FrameSurface.Research));
    }

    /// <summary>THE RECORDERS ONLY OBSERVE: a live UI that drew (and recorded) every section, the developer surfaces and the
    /// research screen, then ended the turn through its button, holds exactly the world a bare session holds after the
    /// same End Turn — hash for hash, before and after.</summary>
    [Fact]
    public void Recorders_OnlyObserve_TheWorldHashIsTheBareSessions_BeforeAndAfterEndTurn()
    {
        UiSession bare = UiSession.Start(42);
        string before = WorldHash.ComputeHex(bare.World);
        bare.EndTurn();
        string after = WorldHash.ComputeHex(bare.World);

        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("observe"), Assets(), developer: true);
        int recorded = 0;
        foreach (Section s in GameSections.Roster(true))
        {
            h.ClickControl("nav:" + s);
            recorded += h.Ui.FrameText.Last.Count;
        }
        h.ClickControl("close");
        h.ClickControl("band-research");
        recorded += h.Ui.FrameText.Last.Count;
        h.Key(Keys.Escape);
        Assert.True(recorded > 100, "recorded " + recorded);
        Assert.NotEmpty(h.Ui.LastLensCommands);
        Assert.Equal(before, WorldHash.ComputeHex(h.Ui.World));
        Assert.Empty(h.Ui.Session.QueuedOrders());
        Assert.True(h.ClickControl("end-turn"));
        Assert.Equal(after, WorldHash.ComputeHex(h.Ui.World));
        Assert.Empty(h.Problems);
    }

    // ================================================================== completeness: ImGui's own text log

    /// <summary>One harness tick (as <see cref="UiFrameHarness.Frame"/>) with ImGui's text log on for the frame: ImGui
    /// writes every string it renders as text — a text line, a widget label, a plot label or overlay — to the file
    /// (LogToFile, closed when the frame's root window ends in Render). <paramref name="bypass"/> runs after the UI's
    /// own drawing, inside the same frame.</summary>
    private static string LoggedFrame(UiFrameHarness h, string path, Action? bypass = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) File.Delete(path);
        MouseState mouse = h.Mouse;
        KeyboardState keyboard = h.Keyboard;
        h.Ui.SetViewport(h.Width, h.Height);
        h.Ui.Update(mouse, keyboard, 1.0 / 60.0);
        h.Ui.PrepareFrame();
        h.Gui.BeginFrame(mouse, keyboard, 1.0 / 60.0);
        ImGui.LogToFile(-1, path);
        h.Ui.Draw();
        bypass?.Invoke();
        h.Gui.EndFrame();
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal) : "";
    }

    /// <summary>What ImGui logged that the frame's FrameText (its ImGui text and widget labels) does not account for:
    /// each recorded string removes one occurrence (longest first), then ImGui's own log furniture (button brackets,
    /// checkbox and radio markers, separator dashes) and whitespace go. Empty = complete.</summary>
    private static string Residue(string log, IReadOnlyList<FrameTextEntry> recorded)
    {
        var texts = new List<string>();
        foreach (FrameTextEntry e in recorded)
            if (e.Source is FrameTextSource.ImGuiText or FrameTextSource.WidgetLabel) texts.Add(e.Text);
        texts.Sort((a, b) => a.Length != b.Length ? b.Length.CompareTo(a.Length) : string.CompareOrdinal(a, b));
        string r = log;
        foreach (string t in texts)
        {
            int at = r.IndexOf(t, StringComparison.Ordinal);
            if (at >= 0) r = r[..at] + "\n" + r[(at + t.Length)..];
        }
        // What ImGui adds of its own: a button's "[" "]" decoration (LogSetNextTextDecoration), a checkbox's "[x]" / "[ ]",
        // a radio button's "(x)" / "( )", a separator's 32 dashes.
        r = Regex.Replace(r, @"\[\s*\]|\[[x~]\]|\([ x]\)|-{32}", " ");
        return Regex.Replace(r, @"\s+", " ").Trim();
    }

    [Fact]
    public void ImGuisOwnTextLog_HoldsNothingFrameTextMissed_OnEverySurface()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("oracle"), Assets(), developer: true);
        string path = Path.Combine(WorkDir("oracle"), "imgui-log.txt");
        var misses = new List<string>();
        int logged = 0;
        void Look(string view, bool chrome = true)
        {
            h.Idle(2);
            string log = LoggedFrame(h, path);
            if (chrome) Assert.Contains("territory", log, StringComparison.Ordinal);   // the oracle is live (not vacuous)
            logged += log.Length;
            string residue = Residue(log, h.Ui.FrameText.Last);
            if (residue.Length > 0) misses.Add(view + ": " + residue);
        }

        Look("map, turn 1");
        Assert.True(h.ClickControl("end-turn"));   // a played turn: the TURN audit, the grievance account, the annals
        Look("map, turn 2");
        foreach (Section s in GameSections.Roster(true))
        {
            h.ClickControl("nav:" + s);
            Look(s.ToString());
        }
        h.ClickControl("nav:Policy");
        h.ClickControl("labour-record");
        for (int k = 0; k < 20; k++) h.Wheel(h.Ui.ContextRect.X + 100, h.Ui.ContextRect.Y + 300, -3);
        Look("POLICY, labour record open, scrolled");
        h.ClickControl("nav:Trends");
        h.ClickControl("trend-place");
        Look("TRENDS, settlement scope");
        for (int i = 0; i < h.Ui.TrendMetrics.Count; i++)
            if (h.Ui.TrendMetrics[i].IsPrice) { h.ClickControl("trend-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)); Look("TRENDS, price"); }
        h.ClickControl("nav:Developer");
        foreach (Section tab in GameSections.DeveloperTabs)
        {
            h.ClickControl("dev-" + tab);
            Look("DEV " + tab);
            if (tab != Section.Settlement) continue;
            foreach (SettlementTab st in GameSections.Tabs)
            {
                h.ClickControl("tab-" + st);
                if (st == SettlementTab.Grievance)
                {
                    h.ClickControl("happiness");
                    foreach (UiControl c in h.Ui.Controls.Last)
                        if (c.Name.StartsWith("factor-", StringComparison.Ordinal)) { (double x, double y) = h.Aim(c); h.Click(x, y); break; }
                }
                Look("DEV Settlement / " + st);
            }
        }
        h.Key(Keys.F12);
        h.ClickControl("close");
        // The formation card, the docked Age panel, a pinned info card over the map, the research screen.
        MilitaryUnitRow[] mine = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire);
        if (mine.Length > 0)
        {
            if (mine[0].Location.Value >= 0) CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, mine[0].Location.Value, h.Width, h.Height);
            h.Idle(2);
            foreach (UnitPlacement p in h.Ui.LensFrame!.UnitPlacements) if (p.Id == mine[0].Id) { h.Click(p.X, p.Y); break; }
            Assert.NotNull(h.Ui.UnitCard);
            Look("formation card");
            h.Key(Keys.Escape);
        }
        h.ClickControl("band-age");
        Look("Age panel");
        h.ClickControl("band-age");
        h.ClickControl("nav:Policy");
        Sim.Ui.Info.InfoHit? row = h.Ui.InfoRegistry.First(InfoKind.LabourSector);
        Assert.NotNull(row);
        h.ShiftClick(row.Value.Rect.CenterX, row.Value.Rect.CenterY);
        Assert.True(h.Ui.Inspector.IsOpen);
        Look("info card");
        h.Key(Keys.Escape);
        h.ClickControl("band-research");
        Look("research screen", chrome: false);

        Assert.True(logged > 2000, "ImGui logged only " + logged + " characters");
        Assert.True(misses.Count == 0, "ImGui rendered text FrameText did not record:\n" + string.Join("\n", misses));
        Assert.Empty(h.Problems);
    }

    /// <summary>THE MUTANT (a bypassing call): the same frame plus one direct <c>ImGui.TextUnformatted</c> outside the
    /// helpers — ImGui renders it, FrameText never hears of it, and the oracle names it. Without the bypass the residue
    /// of the same frame is empty, so the kill is the bypass's.</summary>
    [Fact]
    public void ImGuisOwnTextLog_CatchesABypassingCall()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("bypass"), Assets());
        h.ClickControl("nav:Empire");
        string path = Path.Combine(WorkDir("bypass"), "imgui-log.txt");
        Assert.Equal("", Residue(LoggedFrame(h, path), h.Ui.FrameText.Last));
        string residue = Residue(LoggedFrame(h, path, () =>
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(400, 300));
            ImGui.Begin("##p6-bypass", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);
            ImGui.TextUnformatted("P6 BYPASS MUTANT");
            ImGui.Button("P6 BYPASS BUTTON##p6");
            ImGui.End();
        }), h.Ui.FrameText.Last);
        Assert.Equal("P6 BYPASS MUTANT [ P6 BYPASS BUTTON ]", residue);   // ImGui logs a button as "[ label ]"
        Assert.False(h.Ui.FrameText.Contains("P6 BYPASS"));
    }

    // ================================================================== completeness: the source guard

    [Fact]
    public void SourceGuard_NoImGuiTextOrWidgetCallInSimUi_BypassesTheRecordingHelpers()
    {
        IReadOnlyList<(string Path, string Source)> sources = FrameTextGuard.Sources(RepoRoot());
        Assert.True(sources.Count > 100, "scanned " + sources.Count + " files");
        IReadOnlyList<string> violations = FrameTextGuard.Violations(sources);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void SourceGuard_Allowlist_EveryEntryHasAReason_AndPinsTheExactCount()
    {
        var seen = new HashSet<(string, string)>();
        foreach (FrameTextGuard.Allowance a in FrameTextGuard.Allowed)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Reason), a.File + " " + a.Call);
            Assert.True(a.Count > 0, a.File + " " + a.Call);
            Assert.True(seen.Add((a.File, a.Call)), "duplicate entry " + a.File + " " + a.Call);
            Assert.True(File.Exists(Path.Combine(RepoRoot(), a.File)), a.File);
        }
        // The helpers' file holds exactly one call per helper, nothing else.
        int helpers = 0;
        foreach (FrameTextGuard.Allowance a in FrameTextGuard.Allowed) if (a.File == "Sim.Ui/GameUi.Text.cs") helpers += a.Count;
        Assert.Equal(9, helpers);
    }

    /// <summary>The bypass shapes, each a MUTANT of the real tree (one file's real source, changed in memory): the
    /// guard must report every one, at its line.</summary>
    [Theory]
    [InlineData("Sim.Ui/GameUi.cs", "Text(view.ScopeNote);", "ImGui.TextUnformatted(view.ScopeNote);", "ImGui.TextUnformatted")]
    [InlineData("Sim.Ui/GameUi.cs", "bool clicked = Button(label + \"##\" + id);", "bool clicked = ImGui.Button(label + \"##\" + id);", "ImGui.Button")]
    [InlineData("Sim.Ui/GameUi.cs", "Text(view.ScopeNote);", "ImGuiNET.ImGui.Selectable(view.ScopeNote, false);", "ImGui.Selectable")]
    [InlineData("Sim.Ui/GameUi.cs", "AddText(dl, df, df.FontSize,", "dl.AddText(df, df.FontSize,", "AddText")]
    [InlineData("Sim.Ui/GameUi.cs", "Text(view.ScopeNote);", "ImGui.TextWrapped(view.ScopeNote);", "ImGui.TextWrapped")]
    [InlineData("Sim.Ui/GameUi.cs", "Text(view.ScopeNote);", "ImGui.SetTooltip(view.ScopeNote);", "ImGui.SetTooltip")]
    [InlineData("Sim.Ui/GameUi.cs", "Text(view.ScopeNote);", "ImGuiNative.igTextUnformatted(null, null);", "ImGuiNative.igTextUnformatted")]
    [InlineData("Sim.Ui/GameUi.cs", "Text(view.ScopeNote);", "string u = \"http://x\"; ImGui.LabelText(u, view.ScopeNote);", "ImGui.LabelText")]
    [InlineData("Sim.Ui/ImGuiIntegration/DrawListImGuiBackend.cs", "dl.AddText(font, (float)t.Size, V(x, t.Y), Col(t.Color), t.Text);",
        "dl.AddText(font, (float)t.Size, V(x, t.Y), Col(t.Color), t.Text); dl.AddText(font, (float)t.Size, V(x, t.Y), Col(t.Color), t.Text);", "AddText")]
    [InlineData("Sim.Ui/GameUi.Text.cs", "        ImGui.TextUnformatted(text);", "        ImGui.TextUnformatted(text);\n        ImGui.TextUnformatted(text);", "ImGui.TextUnformatted")]
    public void SourceGuard_EachBypassShape_IsCaught(string file, string find, string replace, string call)
    {
        IReadOnlyList<(string Path, string Source)> sources = FrameTextGuard.Sources(RepoRoot());
        var mutated = new List<(string, string)>();
        int line = -1;
        foreach ((string path, string source) in sources)
        {
            if (path != file) { mutated.Add((path, source)); continue; }
            int at = source.IndexOf(find, StringComparison.Ordinal);
            Assert.True(at >= 0, "the mutant's anchor is gone from " + file + ": " + find);
            line = 1 + source[..at].Count(c => c == '\n');
            mutated.Add((path, source[..at] + replace + source[(at + find.Length)..]));
        }
        Assert.True(line > 0, file + " not scanned");
        Assert.Empty(FrameTextGuard.Violations(sources));                          // the clean tree passes...
        IReadOnlyList<string> found = FrameTextGuard.Violations(mutated);          // ...the mutant does not
        Assert.Contains(found, v => v.Contains(file, StringComparison.Ordinal) && v.Contains(call, StringComparison.Ordinal));
        if (!found.Any(v => v.Contains("count", StringComparison.Ordinal)))
            Assert.Contains(found, v => v.StartsWith(file + ":" + line.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceGuard_AnAliasOfImGui_IsCaught_AndACallInACommentIsNot()
    {
        var aliasStatic = new List<(string, string)> { ("Sim.Ui/Mutant.cs", "using static ImGuiNET.ImGui;\nclass M { void F() { TextUnformatted(\"x\"); } }\n") };
        Assert.Contains(FrameTextGuard.Violations(aliasStatic), v => v.Contains("alias", StringComparison.Ordinal));
        var aliasType = new List<(string, string)> { ("Sim.Ui/Mutant.cs", "using G = ImGuiNET.ImGui;\nclass M { void F() { G.Button(\"x\"); } }\n") };
        Assert.Contains(FrameTextGuard.Violations(aliasType), v => v.Contains("alias", StringComparison.Ordinal));
        var commented = new List<(string, string)>
        {
            ("Sim.Ui/Mutant.cs", "// ImGui.TextUnformatted(\"x\");\n/* ImGui.Button(\"y\"); */\n/// <c>ImGui.PlotLines</c>\nclass M { }\n"),
        };
        Assert.Empty(FrameTextGuard.Violations(commented));
    }
}

/// <summary>
/// P6 — THE FRAMETEXT COMPLETENESS GUARD (source level). Every ImGui call in Sim.Ui that can put text on screen or
/// submit a widget — <c>ImGui.Text*</c>, <c>LabelText</c>, <c>BulletText</c>, every button, checkbox, radio, selectable,
/// tree, tab, menu, combo, list box, slider, drag, input, plot, value, colour widget, progress bar, tooltip, the window
/// <c>Begin</c> (its title), any draw list's <c>AddText</c>, any native <c>ImGuiNative.ig*</c> entry point — must sit in
/// an allowlisted file, as many times as the allowlist says, for the stated reason. An alias of ImGui (<c>using
/// static</c>, <c>using X = ImGuiNET.ImGui</c>) is itself a violation: it would hide a call from the pattern.
/// Comments are blanked before scanning (a call named in a doc comment is not a call); string literals are kept (a
/// pattern inside a string is reported — conservative).
/// </summary>
internal static class FrameTextGuard
{
    public sealed record Allowance(string File, string Call, int Count, string Reason);

    private const string Helper = "GameUi's recording helper: records the string in FrameText, then makes exactly this call";

    public static IReadOnlyList<Allowance> Allowed { get; } =
    [
        new("Sim.Ui/GameUi.Text.cs", "ImGui.TextUnformatted", 1, Helper + " (Text)"),
        new("Sim.Ui/GameUi.Text.cs", "ImGui.Button", 1, Helper + " (Button)"),
        new("Sim.Ui/GameUi.Text.cs", "ImGui.Selectable", 1, Helper + " (Selectable)"),
        new("Sim.Ui/GameUi.Text.cs", "ImGui.Checkbox", 1, Helper + " (Checkbox)"),
        new("Sim.Ui/GameUi.Text.cs", "ImGui.RadioButton", 1, Helper + " (RadioButton)"),
        new("Sim.Ui/GameUi.Text.cs", "ImGui.InvisibleButton", 1, "GameUi.InvisibleButton: draws nothing (its label is an id), routed through the helpers' file so it stays the one place widgets are submitted"),
        new("Sim.Ui/GameUi.Text.cs", "ImGui.PlotLines", 1, Helper + " (PlotLines: the label and the overlay)"),
        new("Sim.Ui/GameUi.Text.cs", "AddText", 1, Helper + " (AddText)"),
        new("Sim.Ui/GameUi.Text.cs", "ImGui.Begin", 1, Helper + " (BeginWindow: the title, when a title bar draws it)"),
        new("Sim.Ui/ImGuiIntegration/DrawListImGuiBackend.cs", "AddText", 3,
            "the DrawList replay: Render's TextCmd case reports the command through Replayed (GameUi.FrameText) before its glyphs are added — plainly, or by Styled (cased, tracked, double-struck)"),
    ];

    private static readonly Regex Calls = new(
        @"\bImGui\.(?<n>Text\w*|LabelText\w*|BulletText\w*|SeparatorText|Button|SmallButton|InvisibleButton|ArrowButton|ImageButton|RadioButton|Checkbox\w*|Selectable|CollapsingHeader|TreeNode\w*|BeginTabItem|TabItemButton|BeginMenu|MenuItem|BeginCombo|Combo|ListBox|ProgressBar|Slider\w*|VSlider\w*|Drag\w*|Input\w*|Plot\w*|Value|LogText|SetTooltip\w*|SetItemTooltip\w*|BeginTooltip|BeginItemTooltip|Begin|Color\w*)\s*\("
        + @"|(?<add>\.AddText)\s*\("
        + @"|\bImGuiNative\.(?<native>ig\w+|ImDrawList_AddText\w*)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Alias = new(@"\busing\s+static\s+ImGuiNET\.ImGui\s*;|\busing\s+\w+\s*=\s*(global::)?ImGuiNET\.(ImGui|ImGuiNative|ImDrawListPtr)\s*;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Every .cs file of Sim.Ui (bin and obj excluded), repo-relative with '/', in ordinal order.</summary>
    public static IReadOnlyList<(string Path, string Source)> Sources(string root)
    {
        string ui = Path.Combine(root, "Sim.Ui");
        var files = new List<string>();
        foreach (string f in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            if (rel.StartsWith("Sim.Ui/bin/", StringComparison.Ordinal) || rel.StartsWith("Sim.Ui/obj/", StringComparison.Ordinal)) continue;
            files.Add(rel);
        }
        files.Sort(StringComparer.Ordinal);
        var r = new List<(string, string)>();
        foreach (string rel in files) r.Add((rel, File.ReadAllText(Path.Combine(root, rel))));
        return r;
    }

    /// <summary>"file:line: call (why)" for every call outside the allowlist, "file: call count N, allowlisted M" for
    /// an allowlisted file whose count differs, and every alias of ImGui.</summary>
    public static IReadOnlyList<string> Violations(IReadOnlyList<(string Path, string Source)> sources)
    {
        var v = new List<string>();
        var counts = new Dictionary<(string, string), int>();
        foreach ((string path, string source) in sources)
        {
            string code = BlankComments(source);
            foreach (Match m in Alias.Matches(code))
                v.Add(path + ":" + LineOf(code, m.Index) + ": alias of ImGui ('" + m.Value + "') hides its calls from the guard");
            foreach (Match m in Calls.Matches(code))
            {
                string call = m.Groups["n"].Success ? "ImGui." + m.Groups["n"].Value
                    : m.Groups["add"].Success ? "AddText" : "ImGuiNative." + m.Groups["native"].Value;
                Allowance? allowed = null;
                foreach (Allowance a in Allowed) if (a.File == path && a.Call == call) allowed = a;
                if (allowed is null) { v.Add(path + ":" + LineOf(code, m.Index) + ": " + call + " outside GameUi's recording helpers (FrameText would not see it)"); continue; }
                counts[(path, call)] = counts.GetValueOrDefault((path, call)) + 1;
            }
        }
        var files = new HashSet<string>();
        foreach ((string path, _) in sources) files.Add(path);
        foreach (Allowance a in Allowed)
        {
            if (!files.Contains(a.File)) continue;   // a partial scan (a mutant set) checks only the files it holds
            int n = counts.GetValueOrDefault((a.File, a.Call));
            if (n != a.Count)
                v.Add(a.File + ": " + a.Call + " count " + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", allowlisted "
                    + a.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (a new direct call in an allowlisted file, or a stale entry)");
        }
        return v;
    }

    private static int LineOf(string s, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++) if (s[i] == '\n') line++;
        return line;
    }

    /// <summary>The source with every comment's characters blanked (newlines kept, so lines still count), string and
    /// char literals left as they are.</summary>
    private static string BlankComments(string s)
    {
        var sb = new StringBuilder(s);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            char n = i + 1 < s.Length ? s[i + 1] : '\0';
            if (c == '/' && n == '/')
            {
                while (i < s.Length && s[i] != '\n') sb[i++] = ' ';
            }
            else if (c == '/' && n == '*')
            {
                sb[i] = ' '; sb[i + 1] = ' ';
                i += 2;
                while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/')) { if (s[i] != '\n') sb[i] = ' '; i++; }
                if (i < s.Length) { sb[i] = ' '; if (i + 1 < s.Length) sb[i + 1] = ' '; i += 2; }
            }
            else if (c == '@' && n == '"')
            {
                i += 2;
                while (i < s.Length && !(s[i] == '"' && (i + 1 >= s.Length || s[i + 1] != '"'))) i += s[i] == '"' ? 2 : 1;
                i++;
            }
            else if (c == '"')
            {
                i++;
                while (i < s.Length && s[i] != '"' && s[i] != '\n') i += s[i] == '\\' ? 2 : 1;
                i++;
            }
            else if (c == '\'')
            {
                i++;
                while (i < s.Length && s[i] != '\'' && s[i] != '\n') i += s[i] == '\\' ? 2 : 1;
                i++;
            }
            else i++;
        }
        return sb.ToString();
    }
}
