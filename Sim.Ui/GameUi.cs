using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Art;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// THE GAME'S PER-FRAME UI, WITHOUT A GRAPHICS DEVICE (M5 hardening H1). Everything the window does except
/// putting pixels on a GPU lives here: the chrome (status band, selection card, command bar, the contextual
/// sections), the research/Knowledge screen, the Age panel/flow/toast, the action surface, the world lens and
/// map interaction (pan, zoom, settlement and unit selection), and the keyboard. <see cref="SimUiGame"/> is a thin
/// MonoGame host that polls input, draws the terrain bake and rivers on the GPU, and calls <see cref="Update"/>
/// and <see cref="Draw"/>; the headless frame harness (<see cref="Headless.UiFrameHarness"/>) calls the SAME two
/// methods inside a real native ImGui context with scripted input — so the playability gate drives the live UI,
/// not a model of it.
///
/// The UI is a VIEW + ORDER SOURCE (T1.7/T1.8, D-023): it reads the session's world and feeds the simulation
/// exclusively through the session's guarded emitters (the order log). Nothing here is read by the simulation.
/// </summary>
public sealed class GameUi
{
    private readonly UiSession _session;
    private readonly string _sessionLogPath;
    private WorldState _world; // convenience alias of _session.World, refreshed each End Turn

    private Camera _camera;

    // --- art substrate: the textures are the host's; the UI holds only the ids it draws with ImGui.Image.
    private readonly AssetLibrary _art;
    private readonly IntPtr _annalsId, _compassId;
    private UiTheme.Fonts? _fonts;

    // M5 polish UR-1: the UI scale. The atlas is rasterised at the scale the host loaded (Scale); the UI asks for the
    // scale its window and the player want (WantedScale) and the host rebuilds the atlas when they differ (SetFonts).
    private double _userScale;
    private double _autoScale = 1.0;

    private bool _showCatchment = true; // T2.4: political geography on by default

    // ADR-033 D1/D2 — THE ACTION SURFACE (the POLICY section): what the civilization can do now, built from
    // AvailableActionsQuery / LabourActivities and painted in the era's hand. The screen owns only UI state
    // (the labour, tax and road drafts); a click answers with a command dispatched through the session's
    // guarded emitters. It replaces the five static % sliders and "Apply labour split" (T3.9b/T4.18).
    private readonly Sim.Ui.Actions.ActionSurfaceScreen _actions = new();
    private Sim.Ui.Actions.ActionSurfaceModel? _actionModel;
    private readonly EraTable _eraTable = UiSession.ProductionEra();
    private ResearchFigure _researchFigure = new("", false);
    private AgeFigure _ageFigure = new("", Sim.Ui.Ages.AgePanelState.NoContent);
    private readonly Sim.Ui.Actions.NoticeToast _notices = new();
    private bool _policyRecord;   // the labour record (declared vs effective, history): folded by default

    // T4.18: which contextual section is open, or None for a clean world. The
    // screen's only mode, and it is UI state — nothing in the simulation reads
    // it, and opening a panel emits no order.
    private Section _openSection = Section.None;
    private bool _trendWorldScope = true;
    private bool _annalsShowAll;
    private HudModel _hud = null!;

    // T4.19 lane B: the glass-box panels' UI-ONLY state — which SETTLEMENT tab
    // is open, which audit lines / contributors / factors are unfolded, which
    // trend metric is graphed, whether the per-turn policy table is shown.
    // None of it is read by the simulation and none of it emits an order; the
    // headless test walks every section and tab and checks the hash.
    private SettlementTab _settlementTab = SettlementTab.Overview;
    private readonly bool[] _auditExpanded = new bool[8];
    private bool _showHappinessFactors;
    private int _expandedFactor = -1;
    private int _expandedClass = -1, _expandedNeed = -1;
    private bool _policyShowStates;
    private int _trendIndex;
    private TurnAuditView? _turnAudit;
    private SettlementView? _settlementView;
    // ADR-033 D9: the player-facing views (plain language, era density), rebuilt on the HUD cadence, and the
    // developer toggle that reveals the audit/debug surfaces (F12, or --dev at launch).
    private PlayerView? _placeView;
    private PlayerView? _empireView;
    private PlayerView? _institutionsView;
    private bool _developer;
    private Section _developerTab = Section.Turn;
    private PolicyView? _policyView;
    private System.Collections.Generic.IReadOnlyList<TrendMetric> _trendMetrics = [];

    /// <summary>T2.6: the D-018 needs registry for the HUD needs block —
    /// display data only (names, bound flags); the sim's copy travels inside
    /// SimConfig and never routes through the UI.</summary>
    private readonly Sim.Core.Systems.NeedsConfig _needs = LoadNeeds();

    private static Sim.Core.Systems.NeedsConfig LoadNeeds()
    {
        using var stream = Sim.Data.DataFiles.OpenNeeds();
        return Sim.Core.Systems.NeedsConfigLoader.Load(stream);
    }

    /// <summary>T3.9a: display registries — the D-031 goods roster (names for
    /// the market panel) and the class registry (names for the per-class needs
    /// block). Same doctrine as the needs registry above: display data only;
    /// the sim's copy travels inside SimConfig and never routes through here.</summary>
    private readonly Sim.Core.Systems.SimConfig _displayCfg = LoadDisplayCfg();

    private static Sim.Core.Systems.SimConfig LoadDisplayCfg()
    {
        return UiFounding.ProductionConfig();
    }

    /// <summary>T3.9a: the market panel's selected good — PURE UI STATE like
    /// the settlement selection. Starts at the first non-numeraire good (the
    /// numeraire's breakdown is all zeros by definition — nothing to explain).</summary>
    private int _selectedGood;
    private System.Collections.Generic.IReadOnlyList<MarketGoodRow> _marketRows = [];
    private System.Collections.Generic.IReadOnlyList<NeedsClassBlock> _needsBlocks = [];
    private System.Collections.Generic.IReadOnlyList<TradeGoodRow> _tradeRows = [];
    private System.Collections.Generic.IReadOnlyList<TradeFlowLine> _tradeFlows = [];
    private string _tradeSummary = "";

    /// <summary>T2.4: the selected settlement id — PURE UI STATE (never in
    /// WorldState, never serialized). Starts at the first settlement.</summary>
    private int _selected;

    private MouseState _lastMouse;

    // KNOWLEDGE & TECHNOLOGY progression screen (docs/architecture/research-tree-ui.md):
    // full-screen, opened with K or the status-band button. Pure UI state; a click on an
    // available node returns the SetResearchTarget order, which the session logs.
    // ADR-031 / D-047 Part 4 D-G: the Age surfaces (capital panel, advance flow, transition toast)
    // and the zoom-dependent world lens. Both are read-only projections; the only write path is
    // the AdvanceAge order the session appends.
    private readonly Sim.Ui.Ages.AgeScreen _age = new(UiPlayer.Empire);

    // ADR-033 D8: the interface's era, DERIVED from the player's authoritative Age (UiEras.Of) — never
    // stored in the simulation, re-derived after every End Turn and at load. _eraFade is the short,
    // presentation-only cross-fade when the Age changes; _frameTheme is what this frame paints with.
    private Sim.Ui.Theme.EraTheme _theme = Sim.Ui.Theme.EraThemes.For(Sim.Ui.Theme.UiEra.Prehistoric);
    private Sim.Ui.Theme.EraTheme _frameTheme = Sim.Ui.Theme.EraThemes.For(Sim.Ui.Theme.UiEra.Prehistoric);
    private Sim.Ui.Theme.EraTransition? _eraFade;
    // The map ink derived from the frame's theme (MapInk.For), rebuilt only when that theme changes.
    private Sim.Ui.World.MapInk _mapInk = Sim.Ui.World.MapInk.Default;
    private Sim.Ui.Theme.EraTheme? _mapInkTheme;

    private Sim.Ui.Theme.EraTheme DeriveTheme() =>
        Sim.Ui.Theme.EraThemes.For(Sim.Ui.Theme.UiEras.Of(_world, _session.Config.Ages, UiPlayer.Empire));
    // The capital's Age panel opens ON DEMAND (the status band's Age indicator, the action surface's advance,
    // the trees' Age chip) — it no longer opens over the map whenever the capital is selected.
    private bool _agePanelOpen;
    private Sim.Ui.World.WorldProjection? _lens;
    private long _lensTurn = -1;
    // H1: the docked Age panel starts under the selection card (it used to begin at y = 202, 18 px over the card's
    // bottom edge), in the left column the formation card also uses.
    private const int AgePanelX = 12, AgePanelW = 440;

    private Sim.Ui.Progression.ProgressionScreen? _progression;
    private bool _progressionOpen;
    private DrawListImGuiBackend? _drawListBackend;
    private bool _progressionDrag;
    private int _progressionDownX, _progressionDownY;
    private KeyboardState _lastKeyboard;
    private bool _clickCandidate;   // press began on the map (not over ImGui)
    // D-A2: per-settlement label rects, measured each frame by DrawNameLabels
    // (row-aligned with world.Settlements) and consumed by the click hit-test.
    private readonly System.Collections.Generic.List<SettlementSelection.LabelRect> _labelRects = new();
    private int _clickDownX, _clickDownY;

    private int _viewportWidth, _viewportHeight;
    private bool _active = true;
    private (Section, Section, SettlementTab) _contextBodyKey = (Section.None, Section.None, SettlementTab.Overview);

    // M5 hardening H1 (Director §3): the selected military formation and its card — PURE UI STATE. A click on a
    // formation token selects it; the card explains it and states that movement and battle are not built yet.
    private int _selectedUnit = -1;
    private UnitCard? _unitCard;
    private Sim.Ui.World.LensFrame? _lensFrame;

    /// <summary>The unit card's width at UI scale 1 (UR-3: its height is its measured content).</summary>
    public const float UnitCardWidth = 320f;

    // UR-3: the measured chrome of the current frame (the selection card's and the unit card's content-sized rects).
    private PanelRect _selectionRect = PanelLayout.Selection;
    private PanelRect _unitCardRect = new("##unit-card", PanelLayout.Margin,
        PanelLayout.Selection.Y + PanelLayout.Selection.Height + PanelLayout.Margin, UnitCardWidth, 268);
    private readonly Dictionary<string, float> _labelWidths = new(StringComparer.Ordinal);

    /// <summary>
    /// The UI over an existing ImGui context (the caller created it and loaded <paramref name="fonts"/> into its
    /// atlas). <paramref name="textures"/> are the ids the UI draws ImGui images with (the host binds real
    /// textures; the headless harness passes stand-ins).
    /// </summary>
    public GameUi(UiSession session, string sessionLogPath, bool developer, AssetLibrary art, UiTheme.Fonts? fonts,
        UiTextureIds textures, int viewportWidth, int viewportHeight, double userScale = Sim.Ui.Theme.UiScale.DefaultUser)
    {
        _developer = developer;
        _userScale = Sim.Ui.Theme.UiScale.NearestStep(userScale);
        _autoScale = Sim.Ui.Theme.UiScale.Auto(Math.Max(1, viewportHeight));
        _session = session;
        _world = session.World;
        _sessionLogPath = sessionLogPath;
        _art = art;
        _fonts = fonts;
        _annalsId = textures.Annals;
        _compassId = textures.Compass;
        _viewportWidth = Math.Max(1, viewportWidth);
        _viewportHeight = Math.Max(1, viewportHeight);
        // ADR-033 D8: the interface's era is DERIVED from the player's authoritative Age.
        _theme = _frameTheme = DeriveTheme();
        UiTheme.Apply(_theme, Scale);
        _camera = new Camera(_world.Terrain!.Size);
        _camera.Clamp(_viewportWidth, _viewportHeight);
        _selected = _world.Settlements.Count > 0 ? _world.Settlements[0].Id.Value : -1;
        RefreshHud();
    }

    // --- what the host and the harness read -------------------------------------------------------------

    /// <summary>The map camera (the host draws the terrain bake and rivers through it).</summary>
    public Camera Camera => _camera;
    public UiSession Session => _session;
    public WorldState World => _world;
    /// <summary>Set when Escape is pressed with nothing open (the host exits; the harness records it).</summary>
    public bool ExitRequested { get; set; }
    /// <summary>The host's frame rate, shown on the developer BUILD tab.</summary>
    public double Fps { get; set; }
    /// <summary>The host's parchment-bake note, shown on the developer BUILD tab.</summary>
    public string BakeNote { get; set; } = "";
    public Section OpenSection => _openSection;
    public Section DeveloperTab => _developerTab;
    public SettlementTab SettlementTabOpen => _settlementTab;
    public bool Developer => _developer;
    public int SelectedSettlement => _selected;
    public bool ProgressionOpen => _progressionOpen;
    public Sim.Ui.Progression.ProgressionScreen? Progression => _progression;
    public bool AgePanelOpen => AgePanelVisible;
    public Sim.Ui.Ages.AgeScreen Age => _age;
    public Sim.Ui.Actions.ActionSurfaceScreen Actions => _actions;
    public Sim.Ui.Actions.NoticeToast Notices => _notices;
    public bool ShowTerritory => _showCatchment;
    public bool PolicyRecordOpen => _policyRecord;
    public bool AnnalsShowAll => _annalsShowAll;
    public bool TrendWorldScope => _trendWorldScope;
    public int TrendIndex => _trendIndex;
    public bool ShowHappinessFactors => _showHappinessFactors;
    /// <summary>The unfolded happiness factor (developer SETTLEMENT / Grievance), or -1.</summary>
    public int ExpandedFactor => _expandedFactor;
    /// <summary>The unfolded grievance contributor (class, need), or (-1, -1).</summary>
    public (int Class, int Need) ExpandedNeed => (_expandedClass, _expandedNeed);
    /// <summary>Whether the developer per-turn policy table is shown (POLICY, labour record).</summary>
    public bool PolicyShowStates => _policyShowStates;
    /// <summary>Which TURN audit lines are unfolded (developer TURN tab), by line index.</summary>
    public IReadOnlyList<bool> AuditExpanded => _auditExpanded;
    public int SelectedGood => _selectedGood;
    public System.Collections.Generic.IReadOnlyList<TrendMetric> TrendMetrics => _trendMetrics;
    /// <summary>Where the docked Age panel is drawn in the current window.</summary>
    public Sim.Ui.Render.RectD AgePanelBounds => AgePanelRect();
    /// <summary>The selected military formation's id, or -1.</summary>
    public int SelectedUnit => _selectedUnit;
    /// <summary>The selected formation's card (null when none is selected).</summary>
    public UnitCard? UnitCard => _unitCard;
    /// <summary>What the world lens drew last frame (zoom, layers, where each formation token went).</summary>
    public Sim.Ui.World.LensFrame? LensFrame => _lensFrame;
    /// <summary>The interactive controls drawn in the last frame, with their screen rects (<see cref="UiControls"/>).</summary>
    public UiControls Controls { get; } = new();

    /// <summary>The status band, command bar and context panel as placed in the current window (see
    /// <see cref="Placed(in PanelRect)"/>).</summary>
    public PanelRect StatusRect => Placed(PanelLayout.Status);
    public PanelRect CommandRect => Placed(PanelLayout.Command);
    public PanelRect ContextRect => Placed(PanelLayout.Context);

    /// <summary>The selection card as drawn last frame (UR-3: its height is its content).</summary>
    public PanelRect SelectionRect => _selectionRect;

    /// <summary>The formation card as drawn last frame (UR-3: its height is its content).</summary>
    public PanelRect UnitCardRect => _unitCardRect;

    /// <summary>The UI scale the loaded atlas was rasterised for (UR-1): every chrome metric is × this.</summary>
    public float Scale => _fonts?.Scale ?? 1f;

    /// <summary>The UI scale the window and the player ask for: the automatic factor (window height, with
    /// hysteresis) × the player's step. When it differs from <see cref="Scale"/> the host rebuilds the atlas and
    /// calls <see cref="SetFonts"/>.</summary>
    public double WantedScale => Sim.Ui.Theme.UiScale.Effective(_autoScale, _userScale);

    /// <summary>The player's interface size step (Ctrl+= / Ctrl+- / Ctrl+0; <c>--ui-scale</c>).</summary>
    public double UserScale => _userScale;

    /// <summary>Takes a rebuilt atlas (a UI-scale change): restyles ImGui at the new scale and drops everything
    /// measured with the old faces.</summary>
    public void SetFonts(UiTheme.Fonts fonts)
    {
        _fonts = fonts;
        _drawListBackend = null;
        _labelWidths.Clear();
        UiTheme.Apply(_frameTheme, Scale);
    }

    /// <summary>
    /// H1: THE CHROME FOLLOWS THE WINDOW. PanelLayout is the 1280×800 design at UI scale 1 and stays the tested
    /// geometry; in a window of any other size (or scale) the status band spans the width, the command bar sits on
    /// the bottom edge and the context panel on the right edge, its height following the window. M5 polish UR-1/UR-3:
    /// the bars' heights are × the UI scale, and the context panel's width follows the window
    /// (<see cref="PanelLayout.ContextWidth"/>: a quarter of it, 420–560 px × s).
    /// </summary>
    public PanelRect Placed(in PanelRect design)
    {
        float s = Scale, w = _viewportWidth, h = _viewportHeight, margin = PanelLayout.Margin * s;
        float statusH = PanelLayout.Status.Height * s, commandH = PanelLayout.Command.Height * s;
        switch (design.Title)
        {
            case "##status": return design with { Height = statusH, Width = Math.Max(1f, w) };
            case "##command": return design with { Y = h - commandH, Height = commandH, Width = Math.Max(1f, w) };
            case "##context":
            {
                float cw = PanelLayout.ContextWidth(w, s);
                return design with
                {
                    X = w - cw - margin, Y = statusH + margin, Width = cw,
                    Height = Math.Max(120f, h - statusH - commandH - 2f * margin),
                };
            }
            default: return design;
        }
    }

    private ChromeElement Placed(in ChromeElement element) => element with { Panel = Placed(element.Panel) };

    public void SetViewport(int width, int height)
    {
        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
        _autoScale = Sim.Ui.Theme.UiScale.Follow(_autoScale, _viewportHeight);
    }


    /// <summary>Rebuilds the cached HUD snapshot for the current selection, the action surface and the
    /// status band's progression figures (on selection change, End Turn and every dispatched order).</summary>
    private void RefreshHud()
    {
        _unitCard = _selectedUnit >= 0
            ? UnitCardModel.Build(_world, _session.Config, _selectedUnit, UiPlayer.Empire, id => _session.Names.Name(id)) : null;
        if (_unitCard is null) _selectedUnit = -1;
        _hud = HudModel.From(_world, _selected, _needs,
            _selected >= 0 ? _session.Names.Name(_selected) : null, _session.Config);

        // T4.19 lane B: the glass-box view models, rebuilt on the same cadence
        // (selection change / End Turn) — pure reads over the observation
        // history and the (prev, next) pair the session holds.
        _turnAudit = ScreenModels.TurnAudit(_session);
        _settlementView = ScreenModels.Settlement(_session, _selected);
        _policyView = ScreenModels.Policy(_session, _selected);
        int density = _theme.Density.Level;
        Func<int, string> names = _session.Names.Name;
        Sim.Core.Observability.SettlementRecord? record = _session.Observations.Observations.Count == 0 || _selected < 0
            ? null : _session.Observations.Settlement(_session.Observations.LastTurn, _selected);
        _placeView = _selected >= 0
            ? PlayerViews.Settlement(_world, _session.Config, UiPlayer.Empire, record, _selected, names, density) : null;
        _empireView = PlayerViews.Empire(_world, _session.Config, UiPlayer.Empire, names, density);
        _institutionsView = PlayerViews.Institutions(_world, _session.Config, UiPlayer.Empire, names, density);
        _trendMetrics = TrendsModel.Metrics(_displayCfg.Registries.Classes);
        if (_trendIndex >= _trendMetrics.Count) _trendIndex = 0;


        // T3.9a: the read-only market/needs displays, recomputed on the same
        // cadence as the HUD snapshot (selection change / End Turn).
        Sim.Core.Systems.GoodsConfig goods = _displayCfg.Goods!;
        if (_selectedGood == 0)
            foreach (Sim.Core.Systems.GoodEntry g in goods.Goods)
                if (!g.Numeraire) { _selectedGood = g.Id; break; }
        _marketRows = MarketModel.Rows(_world, _selected, goods);
        _needsBlocks = NeedsPanelModel.Blocks(_world, _selected, _needs, _displayCfg.Registries.Classes);

        // ADR-033 D1/D2: the action surface, from the query, in the era the interface presents (the target
        // theme: a new Age's controls take over at once; its colours fade in with the frame theme).
        _actionModel = Sim.Ui.Actions.ActionSurface.ForSession(_session, _eraTable, _selected, _theme);
        _actions.Refresh(_actionModel, _world, _session.Config, UiPlayer.Empire, id => _session.Names.Name(id));
        _researchFigure = StatusFigures.Research(_actionModel.Research);
        _ageFigure = StatusFigures.Age(Sim.Ui.Ages.AgePanelModel.Build(_world, _session.Config.Ages, _session.QueuedOrders(), UiPlayer.Empire));

        // T3.9b: the trade panel's rows — world-level, not per-settlement
        // (trade is a pairwise mechanism over every settlement).
        _tradeRows = TradeModel.Rows(_world, goods);
        _tradeFlows = TradeModel.Flows(_world, goods);
        _tradeSummary = TradeModel.SummaryLine(_tradeRows);
    }

    // --- the player's verbs ---------------------------------------------------

    // Stamping/stepping/persistence all live in UiSession (T1.9 adversarial
    // hardening): the replay-equivalence test drives the SAME code paths.
    // ADR-033 D2: every order the action surface asks for goes through ONE dispatch to the session's guarded
    // emitters (each refuses what the simulation would refuse); on refusal nothing is written or claimed.
    private void DispatchAction(Sim.Ui.Actions.ActionCommand cmd)
    {
        switch (cmd.Kind)
        {
            case Sim.Ui.Actions.ActionCommandKind.None:
                return;
            case Sim.Ui.Actions.ActionCommandKind.OpenResearch:
                if (!_progressionOpen) ToggleProgression();
                return;
            case Sim.Ui.Actions.ActionCommandKind.AdvanceAge:
                OpenAgePanel(flow: true);
                return;
            case Sim.Ui.Actions.ActionCommandKind.SelectSettlement:
                if (cmd.Settlement >= 0 && cmd.Settlement != _selected) { _selected = cmd.Settlement; RefreshHud(); }
                return;
        }
        if (!cmd.IsOrder || !Sim.Ui.Actions.ActionDispatch.Apply(_session, cmd)) return;
        SaveSession();
        RefreshHud();
    }

    /// <summary>Opens the capital's Age panel (and, when eligible, its ADVANCE AGE flow — the unchanged
    /// AdvanceAge order path).</summary>
    private void OpenAgePanel(bool flow)
    {
        if (_session.Config.Ages is null) return;
        _agePanelOpen = true;
        _selectedUnit = -1;   // one left-column surface at a time: the Age panel replaces the formation card
        _unitCard = null;
        _age.Refresh(_world, _session.Config.Ages, _session.Config.UnitFamilies, _session.QueuedOrders());
        if (flow) _age.OpenFlow();
    }

    private void EndTurn()
    {
        int ageBefore = _session.Config.Ages is { } a0 ? AgeQuery.CurrentAge(_world, a0, UiPlayer.Empire) : 0;
        _session.EndTurn();
        _world = _session.World;
        Sim.Ui.Theme.EraTheme derived = DeriveTheme();
        if (!ReferenceEquals(derived, _theme))
        {
            _eraFade = new Sim.Ui.Theme.EraTransition(_frameTheme, derived);   // the interface itself transitions
            _theme = derived;
        }
        if (_session.Config.Ages is { } a1)
        {
            // The civilization-state change is announced only when the SIMULATION moved the Age.
            int ageAfter = AgeQuery.CurrentAge(_world, a1, UiPlayer.Empire);
            if (ageAfter != ageBefore)
            {
                int converted = 0;
                foreach (UnitConversionRow c in MilitaryQuery.Conversions(_world, UiPlayer.Empire))
                    if (c.ToAge == ageAfter && c.FromIdentity != c.ToIdentity) converted++;
                _age.ShowTransition(ageAfter, a1.Age(ageAfter).Name,
                    AgeQuery.StateRow(_world, UiPlayer.Empire) is { } row ? a1.SurgeByKey(row.Surge)?.Name : null, converted);
            }
        }
        RefreshHud();
        // Audit E26: announce what the step changed in the action space — research learned, an activity it
        // changed, a domain that appeared — with the surface's own notices.
        _notices.Show(_actionModel?.Notices ?? []);
        SaveSession();
    }

    private void SaveSession()
    {
        _session.Save(_sessionLogPath);
        // T2.9: the chronicle exports beside the order log, same stamp.
        _session.ExportChronicle(UiSession.ChroniclePath(_sessionLogPath));
        // T4.17: and the turn trace, same stamp again — what the world actually
        // looked like on each turn, ending in the hash this machine computed.
        // The manifest is NOT rewritten here: it is written once at launch, so
        // that a session ending in a crash is still reproducible.
        _session.ExportTrace(UiSession.TracePath(_sessionLogPath));
        // T4.19: and the telemetry, same stamp — every turn's world record and
        // settlement records, the glass box as it was actually played. P0: this
        // APPENDS the turns observed since the last save; it no longer rewrites
        // the file, so the cost of a save is the size of the new records.
        _session.ExportTelemetry(UiSession.TelemetryPath(_sessionLogPath));
    }


    /// <summary>The host is exiting: a final save, the telemetry flushed past the OS buffers, then the forensic
    /// close record (which content-hashes every companion, so it is written last).</summary>
    public void SaveOnExit()
    {
        SaveSession();
        _session.FinalizeTelemetry(UiSession.TelemetryPath(_sessionLogPath));
        _session.ExportForensicClose(_sessionLogPath);
    }

    private Rectangle Viewport() => new(0, 0, _viewportWidth, _viewportHeight);
    private bool IsActive => _active;
    private void Exit() => ExitRequested = true;

    /// <summary>
    /// One frame of NON-ImGui input (MonoGame's Update): the K/Escape/Space/digit/F12/Tab keys, the research
    /// screen's pointer and wheel, the Age surfaces' clicks, and the map's pan, zoom and click-select.
    /// <paramref name="mouse"/> and <paramref name="keyboard"/> are this frame's polled state; ImGui's capture
    /// flags are those of the previous ImGui frame, exactly as in the window.
    /// </summary>
    public void Update(MouseState mouse, KeyboardState keyboard, double dtSeconds, bool active = true)
    {
        _active = active;
        _eraFade?.Advance(dtSeconds);
        Rectangle viewport = Viewport();
        ImGuiIOPtr io = ImGui.GetIO();

        // T4.19 lane B: Escape CLOSES the open panel; with nothing open it
        // exits, as T4.18 did. Pressed-edge on polled state (a held key fires
        // once), and only when ImGui does not want the keyboard — a text field
        // owns its own Escape. GameSections.OnEscape says which case applied,
        // so one press is never both "close" and "exit".
        if (IsActive && !io.WantCaptureKeyboard && keyboard.IsKeyDown(Keys.K) && !_lastKeyboard.IsKeyDown(Keys.K))
            ToggleProgression();
        if (_progressionOpen)
        {
            UpdateProgression(dtSeconds, mouse, keyboard, viewport);
            _lastMouse = mouse;
            _lastKeyboard = keyboard;
            return;
        }

        _age.Advance(dtSeconds);
        _notices.Advance(dtSeconds);
        if (UpdateAge(mouse, keyboard))
        {
            _lastMouse = mouse;
            _lastKeyboard = keyboard;
            return;
        }

        if (IsActive && !io.WantCaptureKeyboard
            && keyboard.IsKeyDown(Keys.Escape) && !_lastKeyboard.IsKeyDown(Keys.Escape))
        {
            if (_selectedUnit >= 0) SelectUnit(-1);   // the formation card closes first
            else if (AgePanelVisible) _agePanelOpen = false;   // then the docked Age panel (it is a panel too)
            else
            {
                (Section next, bool closed) = GameSections.OnEscape(_openSection);
                _openSection = next;
                if (!closed) Exit();
            }
        }

        if (IsActive && !io.WantCaptureMouse)
        {
            if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Pressed)
                _camera!.Pan(mouse.X - _lastMouse.X, mouse.Y - _lastMouse.Y, viewport.Width, viewport.Height);

            int wheel = mouse.ScrollWheelValue - _lastMouse.ScrollWheelValue;
            if (wheel != 0)
                _camera!.ZoomAt(mouse.X, mouse.Y, Math.Pow(1.25, wheel / 120.0), viewport.Width, viewport.Height);

            // T2.4 click-select: press→release edge with a small movement
            // threshold so a drag-pan never doubles as a click. A miss keeps
            // the current selection (empty ground is camera territory).
            if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released)
            {
                _clickCandidate = true;
                _clickDownX = mouse.X;
                _clickDownY = mouse.Y;
            }
            if (mouse.LeftButton == ButtonState.Released && _lastMouse.LeftButton == ButtonState.Pressed
                && _clickCandidate
                && Math.Abs(mouse.X - _clickDownX) <= 4 && Math.Abs(mouse.Y - _clickDownY) <= 4)
            {
                // H1 / Director §3: a formation token is picked before the settlement it stands beside.
                int unit = UnitSelection.HitTest(_lensFrame, mouse.X, mouse.Y);
                if (unit >= 0) SelectUnit(unit);
                else
                {
                    int hit = SettlementSelection.HitTest(
                        _world, _camera!, mouse.X, mouse.Y, viewport.Width, viewport.Height,
                        _labelRects);   // D-A2: labels are part of the click target
                    if (hit >= 0) SelectUnit(-1);   // choosing a settlement puts the formation card away
                    if (hit >= 0 && hit != _selected)
                    {
                        _selected = hit;
                        RefreshHud();
                    }
                }
            }
            if (mouse.LeftButton == ButtonState.Released) _clickCandidate = false;
        }
        else if (mouse.LeftButton == ButtonState.Released)
        {
            _clickCandidate = false; // press was over ImGui — never a map click
        }
        if (IsActive && !io.WantCaptureKeyboard)
        {
            double panPx = 600.0 * dtSeconds;
            double dx = 0, dy = 0;
            if (keyboard.IsKeyDown(Keys.W)) dy += panPx;
            if (keyboard.IsKeyDown(Keys.S)) dy -= panPx;
            if (keyboard.IsKeyDown(Keys.A)) dx += panPx;
            if (keyboard.IsKeyDown(Keys.D)) dx -= panPx;
            if (dx != 0 || dy != 0) _camera!.Pan(dx, dy, viewport.Width, viewport.Height);

            // T4.19 lane B: digits 1..7 OPEN sections in roster order (key
            // edge; the same GameSections.Order the command bar draws, so the
            // key and the button cannot disagree about which section is which).
            IReadOnlyList<Section> roster = GameSections.Roster(_developer);
            for (int digit = 1; digit <= roster.Count; digit++)
            {
                Keys key = Keys.D0 + digit;
                if (keyboard.IsKeyDown(key) && !_lastKeyboard.IsKeyDown(key))
                    _openSection = GameSections.OnDigit(_openSection, digit, _developer);
            }

            // M5 polish UR-1: Ctrl+= / Ctrl+- step the interface size through UiScale.UserSteps, Ctrl+0 resets it
            // (key edge); the host rebuilds the atlas at the new scale between frames (WantedScale).
            bool ctrl = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
            if (ctrl && (Edge(keyboard, Keys.OemPlus) || Edge(keyboard, Keys.Add))) _userScale = Sim.Ui.Theme.UiScale.StepUser(_userScale, +1);
            if (ctrl && (Edge(keyboard, Keys.OemMinus) || Edge(keyboard, Keys.Subtract))) _userScale = Sim.Ui.Theme.UiScale.StepUser(_userScale, -1);
            if (ctrl && Edge(keyboard, Keys.D0)) _userScale = Sim.Ui.Theme.UiScale.DefaultUser;

            // ADR-033 D9: F12 toggles the developer surfaces (key edge); turning them off closes one left open.
            if (keyboard.IsKeyDown(Keys.F12) && !_lastKeyboard.IsKeyDown(Keys.F12))
            {
                _developer = !_developer;
                _openSection = GameSections.OnDeveloperToggle(_openSection, _developer);
            }

            // T2.4: Tab cycles the selection in settlement-id order (key edge).
            if (keyboard.IsKeyDown(Keys.Tab) && !_lastKeyboard.IsKeyDown(Keys.Tab))
            {
                int next = SettlementSelection.CycleNext(_world, _selected);
                if (next >= 0 && next != _selected)
                {
                    _selected = next;
                    RefreshHud();
                }
            }
        }

        // T3.9a-b item 1: Space = End Turn, through the SAME EndTurn() the
        // button calls (no second end-turn implementation). Eligibility —
        // the focus rule (never while ImGui wants the keyboard or a text
        // field has focus) and the no-repeat rule (pressed edge on polled
        // state; key repeat never re-lowers polled key state, so a held
        // Space fires exactly once) — lives in the pure EndTurnKey
        // predicate, pinned headless by EndTurnKeyTests.
        if (IsActive && EndTurnKey.ShouldFire(
                keyboard.IsKeyDown(Keys.Space), _lastKeyboard.IsKeyDown(Keys.Space),
                io.WantCaptureKeyboard, io.WantTextInput))
        {
            EndTurn();
        }

        _lastMouse = mouse;
        _lastKeyboard = keyboard;
    }

    private bool Edge(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && !_lastKeyboard.IsKeyDown(key);

    /// <summary>The capital's Age panel is shown only when opened (the compact Age indicator on the status band,
    /// the action surface's advance, the trees' Age chip) — never by itself over the map at turn 1.</summary>
    private bool AgePanelVisible => _agePanelOpen && _session.Config.Ages is not null;

    /// <summary>The name of the player's capital (the Age panel's heading), or "Capital".</summary>
    private string CapitalName =>
        EmpireQuery.TryGetCapital(_world, UiPlayer.Empire, out SettlementId cap) ? _session.Names.Name(cap.Value) : "Capital";

    private Sim.Ui.Render.RectD AgePanelRect()
    {
        Rectangle v = Viewport();
        // UR-3: under the selection card as drawn (its height is its content), above the command bar as placed.
        double y = _selectionRect.Y + _selectionRect.Height + PanelLayout.Margin * Scale;
        return new Sim.Ui.Render.RectD(AgePanelX, y, AgePanelW, Math.Max(360, v.Height - y - CommandRect.Height - PanelLayout.Margin * Scale));
    }

    /// <summary>
    /// The Age surfaces' input. The advance flow is modal (it takes every click and Escape); the
    /// docked capital panel takes clicks inside its rect. Returns true when the frame's input was
    /// consumed. A confirm logs exactly the AdvanceAge order through the session — never a write.
    /// </summary>
    private bool UpdateAge(MouseState mouse, KeyboardState keyboard)
    {
        if (!IsActive || _session.Config.Ages is null) return false;
        _age.Refresh(_world, _session.Config.Ages, _session.Config.UnitFamilies, _session.QueuedOrders());
        bool released = mouse.LeftButton == ButtonState.Released && _lastMouse.LeftButton == ButtonState.Pressed;
        if (_age.FlowOpen)
        {
            if (keyboard.IsKeyDown(Keys.Escape) && !_lastKeyboard.IsKeyDown(Keys.Escape)) _age.CloseFlow();
            if (released) Dispatch(_age.Click(mouse.X, mouse.Y));
            return true;
        }
        if (!AgePanelVisible || !AgePanelRect().Contains(mouse.X, mouse.Y)) return false;
        if (released) Dispatch(_age.Click(mouse.X, mouse.Y));
        return mouse.LeftButton == ButtonState.Pressed || released;
    }

    private void Dispatch(Sim.Ui.Ages.AgeCommand cmd)
    {
        if (cmd.ClosePanel) _agePanelOpen = false;
        if (cmd.OpenKnowledge && !_progressionOpen) ToggleProgression();
        if (cmd.Order is { } order && _session.EmitAdvanceAge(order.TargetId, cmd.SurgeKey)) { SaveSession(); RefreshHud(); }
    }

    /// <summary>The world lens over the map (background list: under the chrome), and the Age panel,
    /// flow and toast (foreground list: over it).</summary>
    private void DrawWorldLensAndAge()
    {
        _drawListBackend ??= new DrawListImGuiBackend(_fonts);
        Rectangle v = Viewport();
        Camera cam = _camera!;
        if (_lens is null || _lensTurn != _world.Clock.Turn)
        {
            _lens = Sim.Ui.World.WorldProjection.Build(_world, _session.Config, id => _session.Names.Name(id), UiPlayer.Empire);
            _lensTurn = _world.Clock.Turn;
        }
        Sim.Ui.World.WorldZoom level = Sim.Ui.World.WorldLens.LevelFor(cam.Zoom, v.Width, v.Height, cam.WorldSize);
        var lens = new Sim.Ui.Render.DrawList();
        // ADR-033 D8: the map's ink is derived from the era the interface paints with (MapInk.For — identity
        // inks constant, neutral ink, casing and legend paper following the era; it fades with the theme).
        if (!ReferenceEquals(_mapInkTheme, _frameTheme)) { _mapInk = Sim.Ui.World.MapInk.For(_frameTheme); _mapInkTheme = _frameTheme; }
        _lensFrame = Sim.Ui.World.WorldLens.Paint(lens, _drawListBackend, _lens, level,
            (x, y) => cam.WorldToScreen(x, y, v.Width, v.Height), cam.Zoom,
            new Sim.Ui.Render.RectD(0, 0, v.Width, v.Height), _selected, showTerritory: _showCatchment, ink: _mapInk);
        if (_selectedUnit >= 0)
            foreach (Sim.Ui.World.UnitPlacement up in _lensFrame.UnitPlacements)
                if (up.Id == _selectedUnit)
                {
                    double ts = UnitSelection.TokenSize(_lensFrame.Zoom) + 4;
                    lens.Rect(new Sim.Ui.Render.RectD(up.X - ts, up.Y - ts * 0.8, 2 * ts, 1.6 * ts), null, _mapInk.Selection, 2.4, 3);
                }
        _drawListBackend.Render(ImGui.GetBackgroundDrawList(), lens);

        if (_session.Config.Ages is null) return;
        _age.Theme = _frameTheme;
        var top = new Sim.Ui.Render.DrawList();
        _age.Refresh(_world, _session.Config.Ages, _session.Config.UnitFamilies, _session.QueuedOrders());
        if (AgePanelVisible) _age.PaintPanel(top, _drawListBackend, AgePanelRect(), CapitalName);
        _age.PaintFlow(top, _drawListBackend, v.Width, v.Height);
        _age.PaintToast(top, _drawListBackend, v.Width, 60);
        _notices.Paint(top, _drawListBackend, _frameTheme, v.Width, _age.ToastVisible ? 196 : 60);
        _drawListBackend.Render(ImGui.GetForegroundDrawList(), top);
    }

    private void ToggleProgression()
    {
        if (_session.Config.Research is not { } content) return;
        _progression ??= new Sim.Ui.Progression.ProgressionScreen(content, UiPlayer.Empire);
        _progressionOpen = !_progressionOpen;
        _progressionDrag = false;
    }

    /// <summary>The progression screen owns the whole window while open: Escape (or K, or the
    /// close button) closes it, Space still ends the turn, and every click is routed through the
    /// pure screen, which answers with the order to log — never a state write.</summary>
    private void UpdateProgression(double dt, MouseState mouse, KeyboardState keyboard, Rectangle viewport)
    {
        var screen = _progression!;
        screen.Scale = Scale;   // UR-4: the tree's strips, cards and type follow the UI scale
        screen.Resize(viewport.Width, viewport.Height);
        screen.Refresh(_world);
        screen.Age = Sim.Ui.Ages.AgePanelModel.Build(_world, _session.Config.Ages, _session.QueuedOrders(), UiPlayer.Empire);
        if (!IsActive) return;
        // UR-4: Escape closes the detail drawer first (narrow windows), then the screen.
        if (keyboard.IsKeyDown(Keys.Escape) && !_lastKeyboard.IsKeyDown(Keys.Escape)) { if (!screen.CloseDrawer()) _progressionOpen = false; return; }
        if (EndTurnKey.ShouldFire(keyboard.IsKeyDown(Keys.Space), _lastKeyboard.IsKeyDown(Keys.Space), false, false))
            EndTurn();

        double scroll = 900.0 * dt;
        double sx = 0, sy = 0;
        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) sy -= scroll;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) sy += scroll;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) sx -= scroll;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) sx += scroll;
        if (sx != 0 || sy != 0) screen.ScrollBy(sx, sy);
        if (keyboard.IsKeyDown(Keys.D1) && !_lastKeyboard.IsKeyDown(Keys.D1)) screen.Tab = Sim.Ui.Progression.TreeTab.Technology;
        if (keyboard.IsKeyDown(Keys.D2) && !_lastKeyboard.IsKeyDown(Keys.D2)) screen.Tab = Sim.Ui.Progression.TreeTab.Civics;

        screen.PointerMove(mouse.X, mouse.Y);
        int wheel = mouse.ScrollWheelValue - _lastMouse.ScrollWheelValue;
        if (wheel != 0)
        {
            // The wheel scrolls the single (vertical) axis; Ctrl + wheel zooms.
            if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl)) screen.WheelZoom(mouse.X, mouse.Y, wheel / 120.0);
            else screen.Wheel(mouse.X, mouse.Y, wheel / 120.0);
        }

        if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Released)
        {
            _progressionDownX = mouse.X; _progressionDownY = mouse.Y; _progressionDrag = false;
        }
        if (mouse.LeftButton == ButtonState.Pressed && _lastMouse.LeftButton == ButtonState.Pressed)
        {
            if (Math.Abs(mouse.X - _progressionDownX) > 4 || Math.Abs(mouse.Y - _progressionDownY) > 4) _progressionDrag = true;
            if (_progressionDrag) screen.Drag(mouse.X - _lastMouse.X, mouse.Y - _lastMouse.Y);
        }
        if (mouse.LeftButton == ButtonState.Released && _lastMouse.LeftButton == ButtonState.Pressed && !_progressionDrag)
        {
            Sim.Ui.Progression.ProgressionCommand cmd = screen.Click(mouse.X, mouse.Y);
            if (cmd.Close) _progressionOpen = false;
            if (cmd.OpenAge)
            {
                // The Age chip: close the tree and open the Age surface on the capital.
                _progressionOpen = false;
                if (EmpireQuery.TryGetCapital(_world, UiPlayer.Empire, out SettlementId cap)) { _selected = cap.Value; RefreshHud(); }
                OpenAgePanel(flow: true);
            }
            // The screen built the order with ResearchOrderFactory; the session logs it through
            // the same factory (it refuses anything the simulation would ignore).
            if (cmd.Node is { } node && _session.EmitResearchOrder(node)) { SaveSession(); RefreshHud(); }
        }
        screen.Advance(dt);
    }

    private void DrawProgression()
    {
        _drawListBackend ??= new DrawListImGuiBackend(_fonts);
        var screen = _progression!;
        screen.Theme = _frameTheme;
        screen.Scale = Scale;
        System.Numerics.Vector2 size = ImGui.GetIO().DisplaySize;
        screen.Refresh(_world);
        Sim.Ui.Render.DrawList list = screen.Paint(size.X, size.Y, _drawListBackend);
        _drawListBackend.Render(ImGui.GetBackgroundDrawList(), list);
    }

    /// <summary>The corner compass rose (§4 item 5, decorative): drawn on the
    /// ImGui background list at a fixed screen corner, faint enough to sit
    /// under the panels.</summary>
    private void DrawCompassRose()
    {
        if (_compassId == IntPtr.Zero) return;
        Rectangle viewport = Viewport();
        const float px = 132f, margin = 22f;
        var min = new System.Numerics.Vector2(viewport.Width - px - margin, viewport.Height - px - margin);
        ImGui.GetBackgroundDrawList().AddImage(_compassId, min,
            min + new System.Numerics.Vector2(px, px),
            System.Numerics.Vector2.Zero, System.Numerics.Vector2.One,
            0xB4FFFFFFu);
    }

    /// <summary>T2.9: name labels beside every marker — drawn on the ImGui
    /// background drawlist (behind panels, above the map), constant screen
    /// size like the markers themselves. Must run between BeforeLayout and
    /// AfterLayout, so DrawHud calls it.</summary>
    private void DrawNameLabels()
    {
        Rectangle viewport = Viewport();
        const float markerPx = (float)SettlementSelection.MarkerScreenPx;
        _labelRects.Clear();
        for (int i = 0; i < _world.Settlements.Count; i++)
        {
            LineGeometry.Vertex position = OverlayMeshes.SettlementPosition(
                _world.Settlements[i], _world.Terrain!.Size);
            (double sx, double sy) = _camera!.WorldToScreen(
                position.X, position.Y, viewport.Width, viewport.Height);
            string name = _session.Names.Name(_world.Settlements[i].Id.Value);
            var pos = new System.Numerics.Vector2(
                (float)sx + markerPx / 2f + 4f, (float)sy - markerPx / 2f);
            // The name TEXT is the WorldLens's (MapLayer.SettlementMarkers owner); only the click
            // target is kept here, so a name is never drawn twice.
            // D-A2: the label is part of the click target. The rect is
            // measured HERE (the renderer owns font metrics) and handed to
            // the pure view-model — ImGui never crosses into SettlementSelection.
            System.Numerics.Vector2 extent = ImGui.CalcTextSize(name);
            _labelRects.Add(new SettlementSelection.LabelRect(
                pos.X, pos.Y, pos.X + extent.X, pos.Y + extent.Y));
        }
    }

    /// <summary>
    /// Panel furniture (§4 item 5; ADR-033 D8): the ERA'S frame behind the current ImGui window and the
    /// era's rule at the ELEMENT's rule rect, painted by the same <c>ChromeFurniture</c> the headless era
    /// preview paints and replayed into the WINDOW draw list at Begin time, so every widget added
    /// afterwards sits on top of it. The window background itself is transparent (UiTheme.StyleFor):
    /// the frame IS the panel, so the A1 slab shows its cut corners over the map. The full window
    /// rect is the clip while it is drawn (ImGui's inner clip would shave the frame's outer pixels).
    ///
    /// T4.19 lane D: the rule's placement is the ELEMENT's, from ChromeGeometry, not "under the title
    /// bar" for every window. The caller names its element; the rect the rule is drawn into is the
    /// rect the headless test checks. The Annals keep their parchment sheet, laid inside the frame
    /// and below the rule.
    /// </summary>
    private void DrawPanelFurniture(in ChromeElement element, IntPtr backgroundId = default)
    {
        _drawListBackend ??= new DrawListImGuiBackend(_fonts);
        ImDrawListPtr list = ImGui.GetWindowDrawList();
        System.Numerics.Vector2 min = ImGui.GetWindowPos();
        System.Numerics.Vector2 max = min + ImGui.GetWindowSize();
        float frameHeight = ImGui.GetFrameHeight();
        var furniture = new Sim.Ui.Render.DrawList();
        Sim.Ui.Theme.ChromeFurniture.Paint(furniture, _frameTheme, element, frameHeight, Scale);
        list.PushClipRect(min, max, false);
        _drawListBackend.Render(list, furniture);
        list.PopClipRect();

        if (backgroundId != default)
        {
            // Tiled parchment sheet: uv spans the sheet in texture multiples,
            // so the ruled lines keep a constant pitch at any panel size.
            float border = ChromeGeometry.FrameBorderPx * Scale;
            var sheetMin = new System.Numerics.Vector2(min.X + border,
                ChromeGeometry.ContentTop(element, frameHeight, Scale));
            var sheetMax = new System.Numerics.Vector2(max.X - border, max.Y - border);
            var uv = new System.Numerics.Vector2((sheetMax.X - sheetMin.X) / 128f, (sheetMax.Y - sheetMin.Y) / 128f);
            list.AddImage(backgroundId, sheetMin, sheetMax, System.Numerics.Vector2.Zero, uv, 0xFFFFFFFFu);
        }
    }

    /// <summary>T3.9a-b item 4: first-use defaults from the tested PanelLayout
    /// — ImGuiCond.FirstUseEver ONLY (never Always, never per-frame forcing),
    /// so the user's drags, resizes and title-bar collapses stick for the
    /// session. Every panel window Begins through this helper.</summary>

    // §3 TYPOGRAPHY RULE (T3.9a-b item 3 — THE font-selection site, stated
    // once, applied panel-wide): the companion face (IBM Plex Serif, lining
    // figures) carries every DATA line — a text line whose payload is
    // measurements (clock/world totals, population, food, the sector bars,
    // grievance, per-need values, market rows, the debug footer). The body
    // serif (EB Garamond) carries headers, prose and control labels (the
    // settlement title, class headers, annal prose, slider/checkbox/button).
    // The switch happens per BLOCK, never per line: the T3.9a gate found the
    // food line sized differently from its neighbours because the companion-
    // face block boundary was drawn mid-panel, ad hoc. All data-line blocks
    // route through this pair so the choice cannot drift per call site.
    private void PushDataFont() { if (_fonts is { } f) ImGui.PushFont(f.For(_frameTheme).Numeric); }
    private void PopDataFont() { if (_fonts is not null) ImGui.PopFont(); }

    /// <summary>The small price sparkline. The axis is PINNED THROUGH
    /// TrendAxisModel rather than passed float.MaxValue/float.MaxValue — those
    /// two sentinels mean "auto-scale to the series' own min and max", which is
    /// what made a series in [90, 100] draw its floor sitting on the x-axis.
    /// A price is non-negative and unbounded, so the floor is 0 and only the
    /// ceiling follows the data; the bounds are printed underneath.</summary>
    private static void Plot(string label, float[] series)
    {
        if (series.Length == 0)
        {
            ImGui.TextUnformatted("no data");
            return;
        }
        PlotDomain domain = TrendAxisModel.PriceDomain(series);
        string overlay = series[^1].ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1f);   // inert, as the trends plot (no hover readout tooltip)
        ImGui.BeginDisabled();
        ImGui.PlotLines(label, ref series[0], series.Length, 0, overlay,
            (float)domain.Floor, (float)domain.Ceiling, new System.Numerics.Vector2(300, 56));
        ImGui.EndDisabled();
        ImGui.PopStyleVar();
        ImGui.TextUnformatted(TrendAxisModel.AxisLabel(domain, ToDoubles(series)));
    }

    private static double[] ToDoubles(float[] series)
    {
        var wide = new double[series.Length];
        for (int i = 0; i < series.Length; i++) wide[i] = series[i];
        return wide;
    }

    /// <summary>Before the ImGui frame starts: the era cross-fade's theme for this frame, written into ImGui's
    /// style while it runs.</summary>
    public void PrepareFrame()
    {
        _frameTheme = _eraFade is { } fade ? fade.Current : _theme;
        if (_eraFade is { } f0) { UiTheme.Apply(_frameTheme, Scale); if (f0.Done) _eraFade = null; }
    }

    /// <summary>
    /// The frame's ImGui content, between the host's <c>ImGui.NewFrame</c> and <c>ImGui.Render</c>: the research
    /// screen alone while it is open; otherwise the world lens, the Age surfaces and the chrome. Button presses
    /// are answered here (ImGui's immediate mode), through the same dispatch as every other control.
    /// </summary>
    public void Draw()
    {
        Controls.BeginFrame();
        if (_fonts is { } fonts) ImGui.PushFont(fonts.For(_frameTheme).Body);

        if (_progressionOpen && _progression is not null)
        {
            DrawProgression();
            if (_fonts is not null) ImGui.PopFont();
            Controls.EndFrame();
            return;
        }

        DrawCompassRose();  // art substrate: §4 item 5 furniture
        DrawWorldLensAndAge();   // ADR-031: zoom lens (background) + Age panel/flow/toast (foreground)
        DrawNameLabels();   // T2.9: background drawlist — under all chrome

        // H1: the ADVANCE AGE flow is MODAL (a full-window backdrop on the foreground list): the chrome under it is
        // drawn but inert, so a click outside the dialog cannot end the turn or open a section beneath it.
        bool modal = _age.FlowOpen;
        if (modal) ImGui.BeginDisabled();
        DrawStatusBand();
        DrawSelectionCard();
        DrawUnitCard();
        DrawContextPanel();
        DrawCommandBar();
        if (modal) ImGui.EndDisabled();

        if (_fonts is not null) ImGui.PopFont();
        Controls.EndFrame();
    }

    /// <summary>
    /// Chrome, not a window: positioned every frame, no title bar, no move, no
    /// resize, no scrollbar. The old panels were FirstUseEver defaults the user
    /// could drag anywhere — which is why panel overlap was something the gate
    /// had to keep re-discovering. A frame that can be lost in the middle of the
    /// map is not a frame.
    /// </summary>
    private static void BeginChrome(in PanelRect rect, ImGuiWindowFlags extra = ImGuiWindowFlags.None)
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(rect.X, rect.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(rect.Width, rect.Height), ImGuiCond.Always);
        ImGui.Begin(rect.Title,
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoBringToFrontOnFocus | extra);
    }

    // ================================================================== UR-1 / UR-3: type roles in the ImGui chrome

    /// <summary>The atlas font of a type role in the frame's era (null without fonts: ImGui's own face).</summary>
    private ImFontPtr? RoleFont(Sim.Ui.Theme.TypeRole role, Sim.Ui.Render.FontRole style = Sim.Ui.Render.FontRole.Body) =>
        _fonts?.Role(_frameTheme, role, style);

    /// <summary>A type role's px at the atlas's scale, in the face the frame's era sets <paramref name="style"/> in.</summary>
    private float RolePx(Sim.Ui.Theme.TypeRole role, Sim.Ui.Render.FontRole style = Sim.Ui.Render.FontRole.Body)
    {
        Sim.Ui.Render.TextStyle st = _frameTheme.Type.For(style);
        return _fonts?.RolePx(st.Face, role, st.Case == Sim.Ui.Render.TextCase.Upper)
            ?? UiTheme.ScaledPx(Sim.Ui.Theme.TypeScale.Px(role, st.Face, st.Case == Sim.Ui.Render.TextCase.Upper), Scale);
    }

    private void PushRole(Sim.Ui.Theme.TypeRole role, Sim.Ui.Render.FontRole style = Sim.Ui.Render.FontRole.Body)
    {
        if (RoleFont(role, style) is { } f) ImGui.PushFont(f);
    }

    private void PopRole() { if (_fonts is not null) ImGui.PopFont(); }

    /// <summary>A run in the era's typesetting for <paramref name="style"/> (case, tracking, the double-struck bold) at
    /// an exact px, set through the DrawList backend into <paramref name="dl"/>; returns its width.</summary>
    private float StyledRun(ImDrawListPtr dl, float x, float y, string text, float px, Sim.Ui.Render.FontRole style,
        ParchmentPalette.Rgba color)
    {
        _drawListBackend ??= new DrawListImGuiBackend(_fonts);
        Sim.Ui.Render.TextStyle st = _frameTheme.Type.For(style);
        var d = new Sim.Ui.Render.DrawList();
        d.Text(x, y, text, px, color, Sim.Ui.Render.TextAlign.Left, style, st);
        _drawListBackend.Render(dl, d);
        return (float)_drawListBackend.Width(text, px, style, st);
    }

    /// <summary>The width a styled run of <paramref name="text"/> would take.</summary>
    private float StyledWidth(string text, float px, Sim.Ui.Render.FontRole style)
    {
        _drawListBackend ??= new DrawListImGuiBackend(_fonts);
        return (float)_drawListBackend.Width(text, px, style, _frameTheme.Type.For(style));
    }

    /// <summary>Truncates <paramref name="text"/> with "..." so its styled run fits <paramref name="width"/>.</summary>
    private string FitStyled(string text, float px, Sim.Ui.Render.FontRole style, float width)
    {
        if (StyledWidth(text, px, style) <= width) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string s = text[..n].TrimEnd() + "...";
            if (StyledWidth(s, px, style) <= width) return s;
        }
        return "...";
    }

    /// <summary>A panel's TITLE (UR-3: the subject — the settlement's name, "Your empire") in the era's title face and
    /// role size, vertically centred in a <paramref name="rowHeight"/> row at the cursor, fitted to
    /// <paramref name="width"/>; in the manuscript era its first letter is the decorated initial (ThemeText.Title).
    /// The row is reserved with a Dummy.</summary>
    private void TitleRow(string text, float width, float rowHeight)
    {
        _drawListBackend ??= new DrawListImGuiBackend(_fonts);
        float px = RolePx(Sim.Ui.Theme.TypeRole.Title, Sim.Ui.Render.FontRole.Title);
        string fit = FitStyled(text, px, Sim.Ui.Render.FontRole.Title, width);
        System.Numerics.Vector2 p = ImGui.GetCursorScreenPos();
        var d = new Sim.Ui.Render.DrawList();
        Sim.Ui.Theme.ThemeText.Title(d, _frameTheme, p.X, p.Y + (rowHeight - px) / 2f - 1f, fit, px / _frameTheme.Type.SizeScale, _frameTheme.Ink.Text);
        _drawListBackend.Render(ImGui.GetWindowDrawList(), d);
        ImGui.Dummy(new System.Numerics.Vector2(width, rowHeight));
    }

    /// <summary>A block heading (UR-3: Heading role, the era's heading style, the accent's TEXT ink) at the cursor.</summary>
    private void HeadingRow(string text)
    {
        float px = RolePx(Sim.Ui.Theme.TypeRole.Heading, Sim.Ui.Render.FontRole.Heading);
        System.Numerics.Vector2 p = ImGui.GetCursorScreenPos();
        float w = StyledRun(ImGui.GetWindowDrawList(), p.X, p.Y, text, px, Sim.Ui.Render.FontRole.Heading, _frameTheme.TextInk.Accent);
        ImGui.Dummy(new System.Numerics.Vector2(Math.Max(1f, w), px));
    }

    /// <summary>The label widths the command bar is laid out from, measured in EVERY era's body face and the widest
    /// kept, so a button never changes size with the Age (era continuity: geometry is era-invariant).</summary>
    private float LabelWidth(string label)
    {
        if (_labelWidths.TryGetValue(label, out float w)) return w;
        w = 0f;
        if (_fonts is { } fonts)
            foreach (Sim.Ui.Render.TypeFace face in new[] { Sim.Ui.Render.TypeFace.Garamond, Sim.Ui.Render.TypeFace.PlexSerif, Sim.Ui.Render.TypeFace.PlexSans })
            {
                float px = fonts.RolePx(face, Sim.Ui.Theme.TypeRole.Body);
                ImFontPtr f = fonts.Face(face, px);
                w = Math.Max(w, f.CalcTextSizeA(f.FontSize, float.MaxValue, 0f, label).X);
            }
        else w = ImGui.CalcTextSize(label).X;
        _labelWidths[label] = w;
        return w;
    }

    /// <summary>The height of <paramref name="text"/> in <paramref name="font"/> wrapped at <paramref name="wrap"/>.</summary>
    private static float WrappedHeight(ImFontPtr? font, string text, float wrap)
    {
        if (font is not { } f) return ImGui.GetTextLineHeight();
        return Math.Max(f.FontSize, f.CalcTextSizeA(f.FontSize, float.MaxValue, Math.Max(1f, wrap), text).Y);
    }

    /// <summary>
    /// THE ALWAYS-TRUE BAND (UR-3): when, how many, how much, and what to do next — in three groups. [Turn · Year] |
    /// [Population · Food], each a caps label over… beside its figure in the KPI face (24 px), the figures clickable
    /// (T4.19 lane B: each routes through ExplainRouting to the account that decomposes it; a dotted underline on
    /// hover says so) | [the research chip · the Age chip]. Idle research — the turn-1 call to action — is a warning
    /// chip in the progress family's TEXT ink with a mark (it measured 2.41:1 as amber text, and read as disabled).
    /// A narrow window drops the settlement count and the Age's name before anything else. Read-only: the band
    /// emits no order.
    /// </summary>
    private void DrawStatusBand()
    {
        PanelRect band = StatusRect;
        BeginChrome(band);
        DrawPanelFurniture(new ChromeElement(band, RulePlacement.BottomEdge));
        float s = Scale, margin = PanelLayout.Margin * s;
        Sim.Ui.Theme.EraTheme t = _frameTheme;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float capPx = RolePx(Sim.Ui.Theme.TypeRole.Caption, Sim.Ui.Render.FontRole.Caps);
        float kpiPx = RolePx(Sim.Ui.Theme.TypeRole.Kpi, Sim.Ui.Render.FontRole.Numeric);
        float detailPx = RolePx(Sim.Ui.Theme.TypeRole.Caption);
        ImFontPtr? kpiFont = RoleFont(Sim.Ui.Theme.TypeRole.Kpi, Sim.Ui.Render.FontRole.Numeric);
        ImFontPtr? capFont = RoleFont(Sim.Ui.Theme.TypeRole.Caption, Sim.Ui.Render.FontRole.Caps);
        // Baselines: the caps label and the detail sit on the figure's baseline.
        float kpiAscent = kpiFont is { } kf ? kf.Ascent * kpiPx / kf.FontSize : kpiPx * 0.8f;
        float capAscent = capFont is { } cf ? cf.Ascent * capPx / cf.FontSize : capPx * 0.8f;
        float top = margin;
        float labelY = top + kpiAscent - capAscent;
        float gap = 8f * s, groupGap = 30f * s, itemGap = 20f * s;
        float frameH = ImGui.GetFrameHeight();

        string research = _researchFigure.Text;
        string age = _ageFigure.Text;
        (string Label, string Value, string Detail, ExplainFigure? Route)[] figures =
        [
            ("Turn", _hud.TurnValue, "", null),
            ("Year", _hud.YearValue, "", null),
            ("Population", _hud.WorldPopulationValue, _hud.SettlementCountDetail, ExplainFigure.WorldPopulation),
            ("Food", _hud.WorldFoodValue, "", ExplainFigure.WorldFood),
        ];
        float FigureWidth(int i, bool details) =>
            StyledWidth(figures[i].Label, capPx, Sim.Ui.Render.FontRole.Caps) + gap
            + StyledWidth(figures[i].Value, kpiPx, Sim.Ui.Render.FontRole.Numeric)
            + (details && figures[i].Detail.Length > 0 ? gap + StyledWidth(figures[i].Detail, detailPx, Sim.Ui.Render.FontRole.Body) : 0f);
        float chipPad = 2f * (12f * s);
        float markW = 22f * s;
        float ChipWidth(string text, bool mark) => ImGui.CalcTextSize(text).X + chipPad + (mark ? markW : 0f);
        float Total(bool details, string ageText) =>
            FigureWidth(0, details) + itemGap + FigureWidth(1, details) + groupGap + FigureWidth(2, details) + itemGap + FigureWidth(3, details)
            + groupGap + ChipWidth(research, _researchFigure.Idle) + (ageText.Length > 0 ? itemGap + ChipWidth(ageText, false) : 0f);
        float room = band.Width - 2f * margin;
        bool details = true;
        if (Total(true, age) > room) { details = false; age = _ageFigure.Short; }

        float x = band.X + margin + 4f * s;
        for (int i = 0; i < figures.Length; i++)
        {
            (string label, string value, string detail, ExplainFigure? route) = figures[i];
            float x0 = x;
            x += StyledRun(dl, x, band.Y + labelY, label, capPx, Sim.Ui.Render.FontRole.Caps, t.Ink.TextSoft) + gap;
            x += StyledRun(dl, x, band.Y + top, value, kpiPx, Sim.Ui.Render.FontRole.Numeric, t.Ink.Text);
            if (details && detail.Length > 0)
                x += gap + StyledRun(dl, x + gap, band.Y + labelY, detail, detailPx, Sim.Ui.Render.FontRole.Body, t.Ink.TextSoft);
            if (route is ExplainFigure fig)
            {
                // The figure is a control: an invisible button over label and value, routed to its explanation.
                ImGui.SetCursorScreenPos(new System.Numerics.Vector2(x0, band.Y + top));
                bool clicked = ImGui.InvisibleButton("##fig-" + fig.ToString(), new System.Numerics.Vector2(Math.Max(1f, x - x0), kpiPx));
                Controls.Record("fig:" + fig.ToString());
                if (ImGui.IsItemHovered()) DottedUnderline(dl, x0, x, band.Y + top + kpiPx + 1f * s, t.Ink.TextSoft);
                if (clicked) Route(ExplainRouting.For(fig));
            }
            x += i == 1 || i == figures.Length - 1 ? groupGap : itemGap;
            if (i == 1 || i == figures.Length - 1) Separator(dl, x - groupGap / 2f, band.Y + top + 2f * s, band.Y + top + kpiPx - 2f * s);
        }

        // The research chip — the band's way into the trees.
        float chipY = band.Y + top + (kpiPx - frameH) / 2f;
        float rw = Math.Min(ChipWidth(research, _researchFigure.Idle), Math.Max(80f * s, band.X + band.Width - margin - x));
        ImGui.SetCursorScreenPos(new System.Numerics.Vector2(x, chipY));
        ParchmentPalette.Rgba researchInk = _researchFigure.Idle ? t.TextInk.Progress : t.TextInk.Active;
        ImGui.PushStyleColor(ImGuiCol.Text, Col(researchInk));
        ImGui.PushStyleColor(ImGuiCol.Button, Col(t.Material.PanelRaised));
        ImGui.PushStyleColor(ImGuiCol.Border, Col(_researchFigure.Idle ? t.Semantic.Progress : t.Material.Border));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, (_researchFigure.Idle ? 2f : 1f) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new System.Numerics.Vector2(_researchFigure.Idle ? 1f : 0.5f, 0.5f));
        bool researchClicked = ImGui.Button(research + "##band-research", new System.Numerics.Vector2(rw, frameH));
        Controls.Record("band-research");
        System.Numerics.Vector2 rmin = ImGui.GetItemRectMin();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(3);
        if (_researchFigure.Idle) AttentionMark(dl, rmin.X + 12f * s + 8f * s, rmin.Y + frameH / 2f, 8f * s, t);
        if (researchClicked) ToggleProgression();
        x += rw + itemGap;

        // The compact Age indicator: opens (or closes) the capital's Age panel on demand.
        if (age.Length > 0)
        {
            float aw = Math.Min(ChipWidth(age, false), Math.Max(60f * s, band.X + band.Width - margin - x));
            ImGui.SetCursorScreenPos(new System.Numerics.Vector2(x, chipY));
            ImGui.PushStyleColor(ImGuiCol.Text, Col(_ageFigure.Eligible ? t.TextInk.Accent : t.Ink.Text));
            ImGui.PushStyleColor(ImGuiCol.Button, Col(_ageFigure.Eligible ? Sim.Ui.Theme.ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.16) : t.Material.PanelRaised));
            bool ageClicked = ImGui.Button(age + "##band-age", new System.Numerics.Vector2(aw, frameH));
            Controls.Record("band-age");
            ImGui.PopStyleColor(2);
            if (ageClicked)
            {
                if (_agePanelOpen) _agePanelOpen = false; else OpenAgePanel(flow: false);
            }
            x += aw + itemGap;
        }
        if (_developer && _turnAudit is { } audit)
        {
            ImGui.SetCursorScreenPos(new System.Numerics.Vector2(x, band.Y + top));
            PushDataFont();
            ImGui.TextUnformatted("last turn: " + audit.Digest);
            PopDataFont();
        }
        ImGui.End();
    }

    /// <summary>A short vertical hairline between the band's groups.</summary>
    private void Separator(ImDrawListPtr dl, float x, float y0, float y1) =>
        dl.AddLine(new System.Numerics.Vector2(x, y0), new System.Numerics.Vector2(x, y1), Col(Sim.Ui.Theme.ThemeColor.Alpha(_frameTheme.Material.Hairline, 0.9)), Math.Max(1f, Scale));

    /// <summary>A dotted underline: the hover affordance of a clickable figure.</summary>
    private void DottedUnderline(ImDrawListPtr dl, float x0, float x1, float y, ParchmentPalette.Rgba ink)
    {
        float step = 4f * Scale;
        for (float x = x0; x < x1; x += step)
            dl.AddLine(new System.Numerics.Vector2(x, y), new System.Numerics.Vector2(Math.Min(x1, x + step * 0.5f), y), Col(ink), Math.Max(1f, Scale));
    }

    /// <summary>The attention mark of a warning chip: a disc in the progress pigment with an exclamation in the ink on
    /// the accent (a non-colour cue, UR-3).</summary>
    private void AttentionMark(ImDrawListPtr dl, float cx, float cy, float r, Sim.Ui.Theme.EraTheme t)
    {
        dl.AddCircleFilled(new System.Numerics.Vector2(cx, cy), r, Col(t.Semantic.Progress), 0);
        float w = Math.Max(1.6f, r * 0.26f);
        dl.AddLine(new System.Numerics.Vector2(cx, cy - r * 0.55f), new System.Numerics.Vector2(cx, cy + r * 0.12f), Col(t.Ink.OnAccent), w);
        dl.AddCircleFilled(new System.Numerics.Vector2(cx, cy + r * 0.48f), w * 0.62f, Col(t.Ink.OnAccent), 0);
    }

    /// <summary>An era colour as ImGui's packed ABGR.</summary>
    private static uint Col(ParchmentPalette.Rgba c) => ((uint)c.A << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;

    /// <summary>A clickable figure line (UR-3: wrapped to the card, in the current font): the text, and an invisible
    /// button over it that opens the surface that explains it — so it reads as the number it was.</summary>
    private void Figure(string text, ExplainFigure figure)
    {
        System.Numerics.Vector2 at = ImGui.GetCursorScreenPos();
        ImGui.TextUnformatted(text);
        System.Numerics.Vector2 min = ImGui.GetItemRectMin(), max = ImGui.GetItemRectMax();
        // The button covers the text exactly, so the cursor ends where the text left it.
        ImGui.SetCursorScreenPos(at);
        bool clicked = ImGui.InvisibleButton("##fig-" + figure.ToString(), new System.Numerics.Vector2(Math.Max(1f, max.X - min.X), Math.Max(1f, max.Y - min.Y)));
        Controls.Record("fig:" + figure.ToString());
        if (ImGui.IsItemHovered()) DottedUnderline(ImGui.GetWindowDrawList(), min.X, max.X, max.Y, _frameTheme.Ink.TextSoft);
        if (clicked) Route(ExplainRouting.For(figure));
    }

    /// <summary>Applies a click-to-explain route: opens the section (and tab),
    /// unfolds the TURN account it names, unfolds the happiness factors when
    /// the route asks for them. Pure UI state.</summary>
    private void Route(ExplainRoute route)
    {
        // ADR-033 D9: the route names the explanation; the mode decides where it is shown.
        (_openSection, _developerTab) = GameSections.Resolve(route.Section, _developer, _developerTab);
        if (route.Section == Section.Settlement) _settlementTab = route.Tab;
        if (route.Expand == AuditExpand.Population) _auditExpanded[0] = true;
        if (route.Expand == AuditExpand.Grain) _auditExpanded[1] = true;
        if (route.ShowHappinessFactors) _showHappinessFactors = true;
    }

    /// <summary>The rows the selection card shows: the HUD's figure lines (the food/population stream owns their
    /// wording), each routed to the panel that decomposes it.</summary>
    private (string Text, ExplainFigure Route)[] SelectionRows() =>
    [
        (_hud.PopulationLine, ExplainFigure.SettlementPopulation),
        (_hud.FoodLine, ExplainFigure.SettlementFood),
        (_hud.HappinessLine, ExplainFigure.SettlementHappiness),
        (_hud.GrievanceLine, ExplainFigure.SettlementGrievance),
    ];

    /// <summary>
    /// The selected settlement, floating over the map: selection is how every
    /// section is aimed, so losing sight of it while looking at the world would
    /// make the world view useless for deciding anything.
    ///
    /// T4.19 lane B: population, food, happiness and grievance are clickable
    /// figures routed to the SETTLEMENT tab that decomposes each. UR-3: 320 px × s wide, its TITLE the settlement's
    /// name in the title face over the era's rule, its rows in the data face wrapped to the card, and its height the
    /// MEASURED content (the population line once ran through the frame of a fixed 268 × 160 card).
    /// </summary>
    private void DrawSelectionCard()
    {
        float s = Scale;
        float frameH = ImGui.GetFrameHeight();
        float padX = ImGui.GetStyle().WindowPadding.X, width = PanelLayout.SelectionWidth * s;
        float rowGap = 6f * s, wrap = width - 2f * padX;
        var element = new ChromeElement(_selectionRect with { Width = width }, RulePlacement.UnderHeaderRow);
        float content = ChromeGeometry.ContentTop(element, frameH, s) - element.Panel.Y;
        (string Text, ExplainFigure Route)[] rows = SelectionRows();
        ImFontPtr? data = RoleFont(Sim.Ui.Theme.TypeRole.Data, Sim.Ui.Render.FontRole.Numeric);
        float h = content;
        foreach ((string text, _) in rows) h += WrappedHeight(data, text, wrap) + rowGap;
        h += PanelLayout.Margin * s - rowGap + 2f * s;
        _selectionRect = new PanelRect(PanelLayout.Selection.Title, PanelLayout.Margin * s,
            StatusRect.Height + PanelLayout.Margin * s, width, (float)Math.Ceiling(h));
        element = new ChromeElement(_selectionRect, RulePlacement.UnderHeaderRow);
        // The left column follows the card: the formation card (and the Age panel) start under it.
        _unitCardRect = _unitCardRect with { Y = _selectionRect.Y + _selectionRect.Height + PanelLayout.Margin * s };

        BeginChrome(_selectionRect);
        DrawPanelFurniture(element);
        ScreenRect head = ChromeGeometry.HeaderRow(element, frameH, s);
        ImGui.SetCursorScreenPos(new System.Numerics.Vector2(_selectionRect.X + padX, head.Y));
        TitleRow(_hud.TitleLine, wrap, frameH);
        ImGui.SetCursorPosY(content);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(ImGui.GetStyle().ItemSpacing.X, rowGap));
        ImGui.PushTextWrapPos(width - padX);
        PushRole(Sim.Ui.Theme.TypeRole.Data, Sim.Ui.Render.FontRole.Numeric);
        foreach ((string text, ExplainFigure route) in rows) Figure(text, route);
        PopRole();
        ImGui.PopTextWrapPos();
        ImGui.PopStyleVar();
        ImGui.End();
    }

    /// <summary>Selects formation <paramref name="unit"/> (or none, with -1) and opens (closes) its card. Opening
    /// the card closes the Age panel: they share the left column.</summary>
    private void SelectUnit(int unit)
    {
        if (unit == _selectedUnit) return;
        _selectedUnit = unit;
        _unitCard = unit >= 0
            ? UnitCardModel.Build(_world, _session.Config, unit, UiPlayer.Empire, id => _session.Names.Name(id)) : null;
        if (_unitCard is null) _selectedUnit = -1;
        else _agePanelOpen = false;
    }

    /// <summary>
    /// THE FORMATION CARD (H1, Director §3): name, owner, family line, current Age form, station, what the next
    /// Age does to it — and, in the card's own words, that it cannot be moved or ordered until the Battle Layer
    /// (M7). Its only control is the close button: nothing on it pretends to move the formation. UR-3: its height is
    /// its measured content (the M7 limitation — its most important sentence — was clipped by a fixed 268 px), and
    /// the limitation is a callout in the progress family's text ink with an attention mark.
    /// </summary>
    private void DrawUnitCard()
    {
        if (_unitCard is not { } card) return;
        float s = Scale;
        float frameH = ImGui.GetFrameHeight();
        float padX = ImGui.GetStyle().WindowPadding.X, width = UnitCardWidth * s, wrap = width - 2f * padX;
        float markW = 22f * s;
        var lines = new List<(string Text, ParchmentPalette.Rgba Ink, bool Callout)>
        {
            (card.Owner + " - " + card.Station, _frameTheme.Ink.Text, false),
            (card.Family + (card.Line.Length > 0 ? ": " + card.Line : ""), _frameTheme.TextInk.Accent, false),
            (card.AgeForm, _frameTheme.Ink.Text, false),
        };
        if (card.NextAge.Length > 0) lines.Add((card.NextAge, _frameTheme.Ink.TextSoft, false));
        lines.Add((card.Limitation, _frameTheme.TextInk.Progress, true));
        ImFontPtr? body = RoleFont(Sim.Ui.Theme.TypeRole.Body);
        float spacing = ImGui.GetStyle().ItemSpacing.Y;
        var probe = new ChromeElement(new PanelRect("##unit-card", 0, 0, width, 100), RulePlacement.UnderHeaderRow);
        float h = ChromeGeometry.ContentTop(probe, frameH, s);
        foreach ((string text, _, bool callout) in lines)
            h += WrappedHeight(body, text, callout ? wrap - markW : wrap) + spacing + (callout ? 6f * s : 0f);
        h += PanelLayout.Margin * s;
        float y = _selectionRect.Y + _selectionRect.Height + PanelLayout.Margin * s;
        _unitCardRect = new PanelRect("##unit-card", PanelLayout.Margin * s, y, width,
            (float)Math.Ceiling(Math.Min(h, Math.Max(120f, _viewportHeight - y - CommandRect.Height - PanelLayout.Margin * s))));
        var element = new ChromeElement(_unitCardRect, RulePlacement.UnderHeaderRow);

        BeginChrome(_unitCardRect);
        DrawPanelFurniture(element);
        ScreenRect close = ChromeGeometry.CloseButton(element, frameH, s);
        ScreenRect head = ChromeGeometry.HeaderRow(element, frameH, s);
        ImGui.SetCursorScreenPos(new System.Numerics.Vector2(head.X + padX - PanelLayout.Margin * s, head.Y));
        TitleRow(card.Title, close.X - head.X - 8f * s, frameH);
        PlaceCursor(_unitCardRect, close);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign,
            new System.Numerics.Vector2(ChromeGeometry.CloseGlyphAlign, ChromeGeometry.CloseGlyphAlign));
        bool closeClicked = ImGui.Button(ChromeGeometry.CloseGlyph + "##unit-close", Size(close));
        Controls.Record("unit-close");
        ImGui.PopStyleVar();
        if (closeClicked) SelectUnit(-1);
        ImGui.SetCursorPosY(ChromeGeometry.ContentTop(element, frameH, s) - _unitCardRect.Y);
        foreach ((string text, ParchmentPalette.Rgba ink, bool callout) in lines)
        {
            if (callout)
            {
                ImGui.Dummy(new System.Numerics.Vector2(1f, 6f * s));
                System.Numerics.Vector2 at = ImGui.GetCursorScreenPos();
                AttentionMark(ImGui.GetWindowDrawList(), at.X + 8f * s, at.Y + ImGui.GetTextLineHeight() / 2f, 8f * s, _frameTheme);
                ImGui.SetCursorScreenPos(new System.Numerics.Vector2(at.X + markW, at.Y));
            }
            ImGui.PushTextWrapPos(width - padX);
            ImGui.PushStyleColor(ImGuiCol.Text, Col(ink));
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();
            ImGui.PopTextWrapPos();
        }
        ImGui.End();
    }

    /// <summary>
    /// THE VERBS AND THE WAYS OF LOOKING, on one row. End Turn sits apart from
    /// the section navigation because it is the only control here that changes
    /// the world; the rest change only what is on screen. UR-3: End Turn is the PRIMARY action — the row's height,
    /// accent-filled with an accent edge, its label emboldened — and each section button is as wide as its measured
    /// label (ChromeGeometry.CommandRow; "INSTITUTIONS" and "SETTLEMENT" were clipped); the open section reads as
    /// SELECTED (a tinted fill and an underline bar), not as pressed.
    /// </summary>
    private void DrawCommandBar()
    {
        PanelRect bar = CommandRect;
        BeginChrome(bar);
        DrawPanelFurniture(new ChromeElement(bar, RulePlacement.TopEdge));   // rule along the TOP edge: world | controls
        float s = Scale;
        Sim.Ui.Theme.EraTheme t = _frameTheme;
        IReadOnlyList<Section> roster = GameSections.Roster(_developer);
        var widths = new float[roster.Count];
        for (int i = 0; i < widths.Length; i++) widths[i] = LabelWidth(GameSections.Label(roster[i]));
        float territoryW = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + LabelWidth("territory");
        CommandRowLayout row = ChromeGeometry.CommandRow(bar, widths, territoryW, s);

        // T3.9a-b item 1 discoverability: the binding is shown ON the button. Both paths (click here, Space in
        // Update) call EndTurn().
        PlaceCursor(bar, row.EndTurn);
        ImGui.PushStyleColor(ImGuiCol.Button, Col(Sim.Ui.Theme.ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.30)));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Col(Sim.Ui.Theme.ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.42)));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Col(Sim.Ui.Theme.ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.52)));
        ImGui.PushStyleColor(ImGuiCol.Border, Col(t.Material.Accent));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f * s);
        bool endTurn = ImGui.Button("##end-turn", Size(row.EndTurn));
        Controls.Record("end-turn");
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        {
            // The label: "End Turn" emboldened in the body face and its key in the caption face, centred as one.
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float px = RolePx(Sim.Ui.Theme.TypeRole.Body, Sim.Ui.Render.FontRole.Body), kpx = RolePx(Sim.Ui.Theme.TypeRole.Caption);
            const string verb = "End Turn", key = "Space";
            float bold = Math.Max(0.6f, px / 30f);
            float vw = (ImGui.CalcTextSize(verb).X) + bold, kw = (RoleFont(Sim.Ui.Theme.TypeRole.Caption) is { } kf ? kf.CalcTextSizeA(kf.FontSize, float.MaxValue, 0f, key).X : 0f);
            float gap = 8f * s, total = vw + gap + kw;
            float x0 = row.EndTurn.X + (row.EndTurn.Width - total) / 2f, cy = row.EndTurn.Y + row.EndTurn.Height / 2f;
            ImFontPtr font = ImGui.GetFont();
            float fs = ImGui.GetFontSize();
            dl.AddText(font, fs, new System.Numerics.Vector2(x0, cy - fs / 2f), Col(t.Ink.Text), verb);
            dl.AddText(font, fs, new System.Numerics.Vector2(x0 + bold, cy - fs / 2f), Col(t.Ink.Text), verb);
            if (RoleFont(Sim.Ui.Theme.TypeRole.Caption) is { } cap)
                dl.AddText(cap, cap.FontSize, new System.Numerics.Vector2(x0 + vw + gap, cy - cap.FontSize / 2f + 1f * s), Col(t.Ink.TextSoft), key);
            _ = kpx;
        }
        if (endTurn) EndTurn();

        for (int i = 0; i < roster.Count; i++)
        {
            Section section = roster[i];
            ScreenRect slot = row.Nav[i];
            PlaceCursor(bar, slot);
            bool open = _openSection == section;
            if (open) ImGui.PushStyleColor(ImGuiCol.Button, Col(Sim.Ui.Theme.ThemeColor.Mix(t.Material.PanelRaised, t.Material.Accent, 0.14)));
            bool navClicked = ImGui.Button(GameSections.Label(section) + "##nav", Size(slot));
            Controls.Record("nav:" + section.ToString());
            if (open)
            {
                ImGui.PopStyleColor();
                // Selected: an underline bar in the accent's soft pigment along the button's foot.
                float bh = 3f * s;
                ImGui.GetWindowDrawList().AddRectFilled(new System.Numerics.Vector2(slot.X + 6f * s, slot.Bottom - bh - 2f * s),
                    new System.Numerics.Vector2(slot.Right - 6f * s, slot.Bottom - 2f * s), Col(t.Material.AccentSoft));
            }
            if (navClicked) _openSection = GameSections.Toggle(_openSection, section);
        }

        ImGui.SetCursorPos(new System.Numerics.Vector2(row.TerritoryX - bar.X,
            row.EndTurn.Y + (row.EndTurn.Height - ImGui.GetFrameHeight()) / 2f - bar.Y));
        ImGui.Checkbox("territory", ref _showCatchment);
        Controls.Record("territory");
        ImGui.End();
    }

    /// <summary>Moves the ImGui cursor to a ChromeGeometry rect's top-left.
    /// SetCursorPos is window-local, so the screen rect is re-based on the
    /// panel it was computed against; scroll offsets are ImGui's to apply.</summary>
    private static void PlaceCursor(in PanelRect panel, in ScreenRect rect) =>
        ImGui.SetCursorPos(new System.Numerics.Vector2(rect.X - panel.X, rect.Y - panel.Y));

    private static System.Numerics.Vector2 Size(in ScreenRect rect) => new(rect.Width, rect.Height);

    /// <summary>UR-3: the context panel's title names its SUBJECT — the settlement's name, "Your empire", the
    /// institutions of the empire, the settlement whose labour the policy panel sets.</summary>
    private string ContextTitle() => _openSection switch
    {
        Section.Place when _placeView is { } v => v.Title,
        Section.Empire when _empireView is { } v => v.Title,
        Section.Institutions when _institutionsView is { } v => v.Title,
        Section.Policy when _actionModel?.Labour is { } l => GameSections.Title(Section.Policy) + " - " + l.Name,
        _ => GameSections.Title(_openSection),
    };

    /// <summary>
    /// The one contextual surface. Nothing is drawn at all when no section is
    /// open — that is the state the whole redesign exists to make reachable.
    /// </summary>
    private void DrawContextPanel()
    {
        if (_openSection == Section.None) return;

        PanelRect panel = ContextRect;
        float s = Scale;
        BeginChrome(panel);
        var element = new ChromeElement(panel, RulePlacement.UnderHeaderRow);
        DrawPanelFurniture(element,   // rule under the header row
            _openSection == Section.Annals ? _annalsId : default);

        // T4.19 lane D — the header row: title at the left, close button
        // flush right, both a frame height tall. The rect is the view-model's:
        // square, frame-height, Margin from the panel edge, and the text
        // alignment is pushed explicitly so the glyph's anchor is the rect's
        // centre by construction. UR-3: the title is the panel's SUBJECT in the
        // era's title face.
        float frameHeight = ImGui.GetFrameHeight();
        ScreenRect close = ChromeGeometry.CloseButton(element, frameHeight, s);
        ScreenRect head = ChromeGeometry.HeaderRow(element, frameHeight, s);
        ImGui.SetCursorScreenPos(new System.Numerics.Vector2(head.X + 2f * s, head.Y));
        TitleRow(ContextTitle(), close.X - head.X - 10f * s, frameHeight);
        PlaceCursor(panel, close);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign,
            new System.Numerics.Vector2(ChromeGeometry.CloseGlyphAlign, ChromeGeometry.CloseGlyphAlign));
        bool closeClicked = ImGui.Button(ChromeGeometry.CloseGlyph + "##close", Size(close));
        Controls.Record("close");
        if (closeClicked) _openSection = Section.None;
        ImGui.PopStyleVar();

        // The header rule IS the separator now; content starts under it. The
        // sections scroll inside a child so the header row is chrome that
        // stays put, and so a vertical scrollbar — ImGui hangs it on the
        // window's right edge — cannot land on the close button.
        ImGui.SetCursorPosY(ChromeGeometry.ContentTop(element, frameHeight, s) - panel.Y);
        // NoBackground: a child window paints ImGuiCol_ChildBg unless told not
        // to, and UiTheme sets ChildBg to a paper tint - so without this flag
        // every section's content region would be washed lighter than its
        // header row, with a hard edge at the child's bounds. The panel plate
        // DrawPanelFurniture already painted is the background; the child
        // exists only to scroll, and must be invisible as a surface.
        // H1: the body is ONE child window for every section, so its scroll used to carry over — POLICY scrolled
        // to its end opened the next section scrolled past its own top. A different section, developer tab or
        // settlement tab opens at the top.
        var bodyKey = (_openSection, _developerTab, _settlementTab);
        if (bodyKey != _contextBodyKey) ImGui.SetNextWindowScroll(System.Numerics.Vector2.Zero);
        _contextBodyKey = bodyKey;
        ImGui.BeginChild("context-body", System.Numerics.Vector2.Zero,
            ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoBackground);
        switch (_openSection)
        {
            case Section.Place: DrawPlayerView(_placeView, "Select a settlement on the map."); break;
            case Section.Empire: DrawPlayerView(_empireView, ""); break;
            case Section.Institutions: DrawPlayerView(_institutionsView, ""); break;
            case Section.Policy: DrawPolicySection(); break;
            case Section.Annals: DrawAnnalsSection(); break;
            case Section.Trends: DrawTrendsSection(); break;
            case Section.Developer: DrawDeveloperSection(); break;
            // A developer surface opened directly (a route resolved with the toggle on lands on DEV, so these
            // are reached only through DrawDeveloperSection's tabs).
            case Section.Turn: case Section.Settlement: case Section.Economy: case Section.More:
                DrawDeveloperTab(_openSection); break;
        }
        ImGui.EndChild();

        ImGui.End();
    }

    /// <summary>
    /// ADR-033 D9 — a PLAYER VIEW (UR-3): the subject is the panel's title (the header row); under it whose it is,
    /// then each block's heading in the heading face and the accent's text ink, and its rows — the statement wrapped
    /// at the left, its FIGURE in a right-aligned value column in the data face (a figure too wide for the column
    /// takes its own line, right-aligned, under the statement). The block gap follows the era's density token.
    /// </summary>
    private void DrawPlayerView(PlayerView? view, string empty)
    {
        if (view is null) { ImGui.TextUnformatted(empty); return; }
        float s = Scale;
        float avail = ImGui.GetContentRegionAvail().X;
        float colGap = 12f * s;
        ImFontPtr? data = RoleFont(Sim.Ui.Theme.TypeRole.Data, Sim.Ui.Render.FontRole.Numeric);
        if (view.Subtitle.Length > 0)
        {
            PushRole(Sim.Ui.Theme.TypeRole.Secondary);
            ImGui.PushStyleColor(ImGuiCol.Text, Col(_frameTheme.Ink.TextSoft));
            ImGui.TextUnformatted(view.Subtitle);
            ImGui.PopStyleColor();
            PopRole();
        }
        bool first = true;
        foreach (ViewBlock block in view.Blocks)
        {
            if (!first || view.Subtitle.Length > 0)
            {
                ImGui.Dummy(new System.Numerics.Vector2(1f, (float)(_frameTheme.Density.Gap * 0.5) * s));
                ImGui.Separator();
                ImGui.Dummy(new System.Numerics.Vector2(1f, 2f * s));
            }
            first = false;
            if (block.Heading.Length > 0) HeadingRow(block.Heading);
            foreach (ViewLine row in block.Rows)
            {
                if (row.Figure.Length == 0 || data is not { } df)
                {
                    ImGui.PushTextWrapPos(0f);
                    ImGui.TextUnformatted(row.ToString());
                    ImGui.PopTextWrapPos();
                    continue;
                }
                float fw = df.CalcTextSizeA(df.FontSize, float.MaxValue, 0f, row.Figure).X;
                bool column = fw <= avail * 0.42f;
                System.Numerics.Vector2 at = ImGui.GetCursorScreenPos();
                float textW = column ? avail - fw - colGap : avail;
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textW);
                ImGui.TextUnformatted(row.Text);
                ImGui.PopTextWrapPos();
                System.Numerics.Vector2 after = ImGui.GetCursorScreenPos();
                ImDrawListPtr dl = ImGui.GetWindowDrawList();
                if (column)
                {
                    // On the first line's baseline: the data face's line is a little shorter than the body's.
                    float dy = (ImGui.GetTextLineHeight() - df.FontSize) * 0.75f;
                    dl.AddText(df, df.FontSize, new System.Numerics.Vector2(at.X + avail - fw, at.Y + dy), Col(_frameTheme.Ink.Text), row.Figure);
                }
                else
                {
                    ImGui.SetCursorScreenPos(after);
                    ImGui.Dummy(new System.Numerics.Vector2(1f, df.FontSize));
                    dl.AddText(df, df.FontSize, new System.Numerics.Vector2(at.X + avail - fw, after.Y), Col(_frameTheme.Ink.Text), row.Figure);
                }
            }
        }
    }

    /// <summary>
    /// ADR-033 D9 — DEV: the audit and debug surfaces, intact, behind the developer toggle. A tab row (TURN,
    /// RECORDS, ECONOMY, BUILD) and the chosen surface exactly as it was drawn as a section.
    /// </summary>
    private void DrawDeveloperSection()
    {
        float rowX = 0f;
        foreach (Section tab in GameSections.DeveloperTabs)
        {
            if (WrapButton(GameSections.Label(tab), "dev-" + tab.ToString(), _developerTab == tab, ref rowX))
                _developerTab = tab;
        }
        ImGui.Separator();
        DrawDeveloperTab(_developerTab);
    }

    private void DrawDeveloperTab(Section tab)
    {
        switch (tab)
        {
            case Section.Turn: DrawTurnSection(); break;
            case Section.Settlement: DrawSettlementSection(); break;
            case Section.Economy: DrawEconomySection(); break;
            case Section.More: DrawBuildSection(); break;
        }
    }

    // --- the shared furniture of the glass-box panels ------------------------

    /// <summary>Data lines in the numeric face, one block.</summary>
    private void DataLines(IReadOnlyList<string> lines)
    {
        PushDataFont();
        foreach (string line in lines) ImGui.TextUnformatted(line);
        PopDataFont();
    }

    /// <summary>A row of small toggle buttons that WRAPS inside the panel
    /// rather than scrolling off it: the tab row and the trend metrics. The
    /// pressed one reads as pressed (the command bar's convention).</summary>
    private bool WrapButton(string label, string id, bool on, ref float rowX)
    {
        float width = ImGui.CalcTextSize(label).X + 2f * ImGui.GetStyle().FramePadding.X;
        float available = ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX();
        if (rowX > 0f && rowX + width > available) rowX = 0f;   // next row
        if (rowX > 0f) ImGui.SameLine();
        if (on) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
        bool clicked = ImGui.Button(label + "##" + id);
        Controls.Record(id);
        if (on) ImGui.PopStyleColor();
        rowX += width + ImGui.GetStyle().ItemSpacing.X;
        return clicked;
    }

    /// <summary>Selects a settlement and centres the camera on it — the WHERE
    /// rows' click. Same RefreshHud path as a map click.</summary>
    private void SelectAndCentre(int settlementId)
    {
        if (settlementId != _selected)
        {
            _selected = settlementId;
            RefreshHud();
        }
        Rectangle viewport = Viewport();
        CameraFocus.CenterOn(_camera!, _world, settlementId, viewport.Width, viewport.Height);
    }

    /// <summary>
    /// TURN — the audit of the last End Turn (B3): WHAT CHANGED, each line
    /// unfolding to its account's legs; WHERE, each row a click that selects
    /// and centres; WHY, the causes identities and the reconcile flags as the
    /// record states them.
    /// </summary>
    private void DrawTurnSection()
    {
        if (_turnAudit is not { } audit)
        {
            ImGui.TextUnformatted("No turn played yet. End a turn to audit it.");
            return;
        }
        PushDataFont();
        ImGui.TextUnformatted(audit.HeaderLine);
        PopDataFont();
        ImGui.Separator();

        ImGui.TextUnformatted("WHAT CHANGED  (click a line for its account)");
        PushDataFont();
        for (int i = 0; i < audit.Changed.Count; i++)
        {
            AuditLine line = audit.Changed[i];
            bool auditClicked = ImGui.Selectable(line.Line + "##audit-" + line.Key, _auditExpanded[i]);
            Controls.Record("audit-" + line.Key);
            if (auditClicked) _auditExpanded[i] = !_auditExpanded[i];
            if (_auditExpanded[i])
                foreach (string detail in line.Detail) ImGui.TextUnformatted(detail);
        }
        PopDataFont();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("WHERE  (click a row to select and centre)");
        ImGui.TextUnformatted("largest population change");
        PushDataFont();
        foreach (WhereRow row in audit.LargestPopulationDelta)
        {
            string whereId = "where-pop-" + row.Settlement.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bool whereClicked = ImGui.Selectable(row.Line + "##" + whereId, row.Settlement == _selected);
            Controls.Record(whereId);
            if (whereClicked) SelectAndCentre(row.Settlement);
        }
        PopDataFont();
        ImGui.TextUnformatted("largest deficit");
        PushDataFont();
        foreach (WhereRow row in audit.LargestDeficit)
        {
            string whereId = "where-def-" + row.Settlement.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bool whereClicked = ImGui.Selectable(row.Line + "##" + whereId, row.Settlement == _selected);
            Controls.Record(whereId);
            if (whereClicked) SelectAndCentre(row.Settlement);
        }
        PopDataFont();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("WHY");
        DataLines(audit.Why);
    }

    /// <summary>
    /// SETTLEMENT — the selected settlement, tabbed (B4). The former
    /// POPULATION and MARKET sections live in the Population and Economy
    /// tabs; every line they drew is still drawn there.
    /// </summary>
    private void DrawSettlementSection()
    {
        if (_selected < 0)
        {
            ImGui.TextUnformatted("Select a settlement on the map.");
            return;
        }
        ImGui.TextUnformatted(_hud.TitleLine);

        float rowX = 0f;
        foreach (SettlementTab tab in GameSections.Tabs)
        {
            if (WrapButton(GameSections.TabLabel(tab), "tab-" + tab.ToString(), _settlementTab == tab, ref rowX))
                _settlementTab = tab;
        }
        ImGui.Separator();

        switch (_settlementTab)
        {
            case SettlementTab.Overview: DrawSettlementOverview(); break;
            case SettlementTab.Population: DrawSettlementPopulation(); break;
            case SettlementTab.Food: DrawSettlementFood(); break;
            case SettlementTab.Economy: DrawSettlementEconomy(); break;
            case SettlementTab.Grievance: DrawSettlementGrievance(); break;
            case SettlementTab.Migration: DrawRecordTab(_settlementView?.Migration); break;
            case SettlementTab.Orders: DrawRecordTab(_settlementView?.Orders); break;
        }
    }

    /// <summary>A tab that is only record lines, or the no-record line.</summary>
    private void DrawRecordTab(IReadOnlyList<string>? lines)
    {
        if (lines is null) { ImGui.TextUnformatted(SettlementInspectorModel.NoRecord); return; }
        DataLines(lines);
    }

    private void DrawSettlementOverview()
    {
        // The T4.18 card figures first (live world), then the record's headline.
        DataLines([_hud.PopulationLine, _hud.FoodLine, _hud.HappinessLine, _hud.GrievanceLine]);
        ImGui.Separator();
        DrawRecordTab(_settlementView?.Overview);
    }

    private void DrawSettlementPopulation()
    {
        DrawRecordTab(_settlementView?.Population);
        ImGui.Separator();
        // T3.9a item 3 (the former POPULATION section): needs PER CLASS.
        foreach (NeedsClassBlock block in _needsBlocks)
        {
            ImGui.TextUnformatted(block.HeaderLine);   // class header: body face
            DataLines(block.NeedLines);
            ImGui.Spacing();
        }
    }

    private void DrawSettlementFood()
    {
        DataLines([_hud.FoodLine]);
        DrawRecordTab(_settlementView?.Food);
        // T4.20: the food-flow block, ADDITIVE and below the store account.
        ImGui.Separator();
        DrawRecordTab(_settlementView?.FoodFlow);
    }

    /// <summary>The former MARKET section (T3.9a items 1+2) plus the record's
    /// economy lines. READ-ONLY: no widget here emits an order.</summary>
    private void DrawSettlementEconomy()
    {
        DrawRecordTab(_settlementView?.Economy);
        ImGui.Separator();
        PushDataFont();
        foreach (MarketGoodRow row in _marketRows)
        {
            string goodId = "good" + row.GoodId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bool goodClicked = ImGui.Selectable(row.Line + "##" + goodId, _selectedGood == row.GoodId);
            Controls.Record(goodId);
            if (goodClicked) _selectedGood = row.GoodId;
        }
        ImGui.Separator();
        PriceBreakdown? breakdown = _selected >= 0
            ? MarketModel.Breakdown(_world, _selected, _selectedGood, _displayCfg.Goods!)
            : null;
        if (breakdown is null)
        {
            ImGui.TextUnformatted("price decomposition: not yet measured");
        }
        else
        {
            ImGui.TextUnformatted(breakdown.HeaderLine);
            foreach (string line in breakdown.TermLines) ImGui.TextUnformatted(line);
            ImGui.TextUnformatted(breakdown.DriverLine);
        }
        ImGui.Separator();
        // Item 2 gate criterion: scarcity READS as a rising line.
        Plot("price##sel-good", _session.History.Price(_selected, _selectedGood));
        PopDataFont();
    }

    /// <summary>
    /// GRIEVANCE — the centre of the packet (A6-A9, B6): happiness and its
    /// factors, then per class the total, the accrual against the decay, the
    /// PRIMARY first, every bound need as a contributor; a contributor unfolds
    /// to its causal chain and ends at its lever with a button that opens
    /// POLICY. Summary → decomposition → cause → lever, and nothing else.
    /// </summary>
    private void DrawSettlementGrievance()
    {
        if (_settlementView?.Grievance is not { } view)
        {
            ImGui.TextUnformatted(_settlementView is null
                ? SettlementInspectorModel.NoRecord
                : "the explanation needs the previous world - available from the next End Turn");
            return;
        }

        PushDataFont();
        bool happinessClicked = ImGui.Selectable(view.HappinessLine + "##happiness", _showHappinessFactors);
        Controls.Record("happiness");
        if (happinessClicked) _showHappinessFactors = !_showHappinessFactors;
        PopDataFont();
        if (_showHappinessFactors)
        {
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(view.ScopeNote);
            ImGui.PopTextWrapPos();
            PushDataFont();
            for (int f = 0; f < view.Factors.Count; f++)
            {
                HappinessFactorRow factor = view.Factors[f];
                string factorId = "factor-" + f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                bool factorClicked = ImGui.Selectable("  " + factor.Line + "##" + factorId, _expandedFactor == f);
                Controls.Record(factorId);
                if (factorClicked) _expandedFactor = _expandedFactor == f ? -1 : f;
                if (_expandedFactor == f) DrawChain(factor.Chain, factor.Lever, "factor-" + f.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            PopDataFont();
        }

        ImGui.Separator();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted(view.AttributionNote);
        ImGui.PopTextWrapPos();

        foreach (ClassGrievanceBlock block in view.Classes)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted(block.HeaderLine);   // class header: body face
            PushDataFont();
            ImGui.TextUnformatted(block.AccrualLine);
            ImGui.TextUnformatted(block.PrimaryLine);
            foreach (ContributorRow row in block.Contributors)
            {
                bool open = _expandedClass == block.ClassId && _expandedNeed == row.NeedId;
                string id = block.ClassId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "-" + row.NeedId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                bool needClicked = ImGui.Selectable("  " + row.Line + "##need-" + id, open);
                Controls.Record("need-" + id);
                if (needClicked)
                {
                    _expandedClass = open ? -1 : block.ClassId;
                    _expandedNeed = open ? -1 : row.NeedId;
                }
                if (open) DrawChain(row.Chain, row.Lever, "need-" + id);
            }
            if (block.NotSimulated.Count > 0)
            {
                ImGui.TextUnformatted("  not yet simulated:");
                foreach (string line in block.NotSimulated) ImGui.TextUnformatted("  " + line);
            }
            PopDataFont();
        }
    }

    /// <summary>A chain's links, each with ITS OWN node's lever indented under
    /// it (a weather link reads "condition, no lever", a share link names its
    /// slider), then the head-node lever summary whose button opens POLICY for
    /// this settlement. GAP links are already worded "not recorded" by the
    /// view model; nothing here reinterprets them.</summary>
    private void DrawChain(IReadOnlyList<ChainLine> chain, LeverLine lever, string id)
    {
        ImGui.PushTextWrapPos(0f);
        foreach (ChainLine link in chain)
        {
            ImGui.TextUnformatted("    " + link.Text);
            ImGui.TextUnformatted("      -> " + link.Lever.Text);
        }
        ImGui.TextUnformatted("    " + lever.Text);
        ImGui.PopTextWrapPos();
        if (!lever.IsNone)
        {
            ImGui.SameLine();
            bool leverClicked = ImGui.Button("open POLICY##lever-" + id);
            Controls.Record("lever-" + id);
            if (leverClicked) _openSection = Section.Policy;
        }
    }

    /// <summary>
    /// POLICY — the only section the director ACTS in: THE ACTION SURFACE (ADR-033 D1/D2; directive "THE
    /// CENTRAL GAMEPLAY PRINCIPLE", "NO MODERN DASHBOARD AT TURN 1"). What the civilization can do now, built
    /// only from AvailableActionsQuery / LabourActivities and painted by the same ActionSurfaceScreen the
    /// headless preview paints: at turn 1 the five baseline activities of the selected (or capital)
    /// settlement as pebbles, learning, the baseline projects with their blockers, basic fighting and what
    /// the people do on their own — and more only as the query lists more. It replaces the five static %
    /// sliders and "Apply labour split" (the M3/M4 dashboard). The labour RECORD (declared vs effective and
    /// the history of changes, T4.19 B5) is kept, folded, under it: a glass-box record, not a control.
    /// </summary>
    private void DrawPolicySection()
    {
        if (_actionModel is null) return;
        _drawListBackend ??= new DrawListImGuiBackend(_fonts);
        _actions.Theme = _frameTheme;
        _actions.Scale = Scale;
        // UR-6: the surface's controls answer hover — the pointer, while it is over this panel's scrolling body.
        System.Numerics.Vector2 mouse = ImGui.GetIO().MousePos;
        if (ImGui.IsWindowHovered()) _actions.PointerMove(mouse.X, mouse.Y); else _actions.PointerMove(null, null);
        System.Numerics.Vector2 origin = ImGui.GetCursorScreenPos();
        float width = Math.Max(1f, ImGui.GetContentRegionAvail().X - 4f);
        var surface = new Sim.Ui.Render.DrawList();
        double height = _actions.Paint(surface, _drawListBackend, origin.X, origin.Y, width);
        // One invisible item the size of the painted surface: it reserves the scroll extent and takes the
        // pointer; the click is answered by the screen's own hit regions (painted this frame).
        ImGui.InvisibleButton("##action-surface", new System.Numerics.Vector2(width, (float)Math.Max(1.0, height)));
        Controls.Record("action-surface");
        if (ImGui.IsItemActivated())
        {
            System.Numerics.Vector2 p = ImGui.GetIO().MousePos;
            DispatchAction(_actions.Click(p.X, p.Y));
        }
        else if (ImGui.IsItemActive()) _actions.Drag(ImGui.GetIO().MousePos.X);
        if (ImGui.IsItemDeactivated()) _actions.Release();
        _drawListBackend.Render(ImGui.GetWindowDrawList(), surface);

        ImGui.Spacing();
        ImGui.Separator();
        if (_selected < 0 || _policyView is not { } view) return;
        // UR-6: the record is a glass-box fold, not a decision — set at the secondary role, not as the panel's
        // largest text.
        PushRole(Sim.Ui.Theme.TypeRole.Secondary);
        ImGui.PushStyleColor(ImGuiCol.Text, Col(_frameTheme.Ink.TextSoft));
        ImGui.Checkbox("labour record (declared vs effective, history)", ref _policyRecord);
        Controls.Record("labour-record");
        ImGui.PopStyleColor();
        PopRole();
        if (!_policyRecord) return;
        ImGui.TextUnformatted(_hud.TitleLine);
        foreach (PolicyEntry policy in PolicyHistoryModel.Policies)
            ImGui.TextUnformatted(policy.Name + " - " + policy.Note);
        ImGui.TextUnformatted("CURRENT  (declared vs effective, currently running)");
        DataLines(view.Current);
        ImGui.Spacing();
        ImGui.TextUnformatted("HISTORY  (newest first)");
        PushDataFont();
        if (view.History.Count == 0) ImGui.TextUnformatted("no change yet for this settlement");
        foreach (PolicyChangeView change in view.History)
        {
            ImGui.TextUnformatted(change.Line);
            foreach (string line in change.Consequences) ImGui.TextUnformatted(line);
        }
        PopDataFont();
        // ADR-033 D9: the per-turn policy table is a record dump — developer only.
        if (!_developer) return;
        ImGui.Spacing();
        ImGui.Checkbox("per-turn policy table", ref _policyShowStates);
        Controls.Record("per-turn-table");
        if (_policyShowStates) DataLines(view.States);
    }

    /// <summary>
    /// ECONOMY — T3.9b's trade panel. Its hard case: in the opening turns of the
    /// canonical world nothing trades yet (trade begins once settlements' prices
    /// diverge), and an empty panel reads as a broken one, so the summary states
    /// "no trade" as a counted fact and every good carries its own reason. T4.19 lane B: plus the world GoodAccount table from the
    /// latest TurnRecord — every non-grain good's account and whether it closes.
    /// </summary>
    private void DrawEconomySection()
    {
        PushDataFont();
        ImGui.TextUnformatted(_tradeSummary);
        ImGui.Separator();
        foreach (TradeFlowLine flow in _tradeFlows) ImGui.TextUnformatted(flow.Line);
        if (_tradeFlows.Count > 0) ImGui.Separator();
        foreach (TradeGoodRow row in _tradeRows) ImGui.TextUnformatted(row.Line);
        PopDataFont();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("WORLD GOOD ACCOUNTS  (last turn, from the ledger's flow rows)");
        if (_turnAudit is { } audit) DataLines(audit.GoodAccounts);
        else ImGui.TextUnformatted("no turn played yet");
    }

    /// <summary>
    /// ANNALS — a history log, so it opens on the RECENT end rather than
    /// dumping six thousand years at a reader who wanted to know what just
    /// happened. The full chronicle is one click away and nothing is dropped.
    /// </summary>
    private void DrawAnnalsSection()
    {
        IReadOnlyList<string> lines = _session.AnnalLines;
        const int recent = 12;
        int from = _annalsShowAll ? 0 : Math.Max(0, lines.Count - recent);

        if (lines.Count > recent)
        {
            ImGui.Checkbox($"all {lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} entries##annals-all",
                ref _annalsShowAll);
            Controls.Record("annals-all");
            ImGui.Spacing();
        }

        ImGui.BeginChild("annal-scroll");
        ImGui.PushTextWrapPos(0f);
        for (int i = from; i < lines.Count; i++) ImGui.TextUnformatted(lines[i]);
        ImGui.PopTextWrapPos();
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f) ImGui.SetScrollHereY(1f);
        ImGui.EndChild();
    }

    /// <summary>
    /// TRENDS — ONE graph, with the metric and the scope chosen rather than six
    /// thumbnails competing. T4.19 lane B (B2): the metrics are every SeriesKey
    /// of the observation history (grievance per class) with world or
    /// settlement scope, plus the price series the T3.9a HistoryBuffer alone
    /// carries. Same graph, one implementation.
    /// </summary>
    private void DrawTrendsSection()
    {
        float rowX = 0f;
        for (int i = 0; i < _trendMetrics.Count; i++)
        {
            if (WrapButton(_trendMetrics[i].ButtonLabel, "trend-" + i.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), _trendIndex == i, ref rowX))
                _trendIndex = i;
        }

        ImGui.Spacing();
        bool worldScope = ImGui.RadioButton("world##trend-world", _trendWorldScope);
        Controls.Record("trend-world");
        if (worldScope) _trendWorldScope = true;
        ImGui.SameLine();
        bool placeScope = ImGui.RadioButton((_selected >= 0 ? _session.Names.Name(_selected) : "(no selection)") + "##trend-place",
                !_trendWorldScope);
        Controls.Record("trend-place");
        if (placeScope)
        {
            _trendWorldScope = false;
        }

        ImGui.Spacing();
        if (_trendMetrics.Count == 0) { ImGui.TextUnformatted("no data"); return; }
        TrendMetric metric = _trendMetrics[_trendIndex];
        if (metric.IsPrice)
        {
            // Prices: the observation history has no price series (§7), so the
            // T3.9a buffer keeps them — per (settlement, good), never world.
            ImGui.TextUnformatted("price of the good selected under SETTLEMENT / Economy, this settlement");
            float[] prices = _session.History.Price(_selected, _selectedGood);
            PlotDomain priceDomain = TrendAxisModel.PriceDomain(prices);
            PushDataFont();
            ImGui.TextUnformatted(TrendAxisModel.AxisLabel(priceDomain, ToDoubles(prices)));
            ImGui.TextUnformatted(TrendAxisModel.BandNote(PlotBand.Neutral));
            PopDataFont();
            PlotLarge("##trend", prices, priceDomain, PlotBand.Neutral);
            return;
        }
        double[] series = _trendWorldScope
            ? TrendsModel.World(_session.Observations, metric.Key, metric.ClassId)
            : TrendsModel.Settlement(_session.Observations, _selected, metric.Key, metric.ClassId);
        PlotDomain domain = TrendAxisModel.Domain(metric.Key, series);
        PlotBand band = TrendAxisModel.Band(
            metric.Key, series.Length == 0 ? double.NaN : series[^1]);
        PushDataFont();
        ImGui.TextUnformatted(TrendsModel.LastValueLine(series));
        ImGui.TextUnformatted(TrendAxisModel.AxisLabel(domain, series));
        ImGui.TextUnformatted(TrendAxisModel.BandNote(band));
        PopDataFont();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted(TrendsModel.ScopeNote(metric.Key, _trendWorldScope));
        ImGui.PopTextWrapPos();
        PlotLarge("##trend", TrendsModel.ForPlot(series), domain, band);
    }

    /// <summary>BUILD — the glass-box footer. Diagnostic rather than play
    /// information, so it no longer costs screen space during play; every line
    /// it ever carried is still here, plus (T4.19 lane B) the session's five
    /// files so the director can find them.</summary>
    private void DrawBuildSection()
    {
        PushDataFont();
        ImGui.TextUnformatted(BuildInfo.Describe()); // same identity as the title
        ImGui.TextUnformatted(HudModel.StatusLine(_world.Seed, Fps));
        ImGui.TextUnformatted(HudModel.CameraLine(_camera!.CenterX, _camera.CenterY, _camera.Zoom));
        // Art-substrate provenance: which assets are real and which are
        // stand-ins, plus the bake cost — the glass-box habit applied to art.
        ImGui.TextUnformatted(_art.SummaryLine());
        ImGui.TextUnformatted(BakeNote);
        if (_fonts is { } f) ImGui.TextUnformatted(f.Note);
        ImGui.Separator();
        foreach (string line in SessionFilesModel.Lines(_sessionLogPath)) ImGui.TextUnformatted(line);
        PopDataFont();
    }

    /// <summary>The trends plot: as wide as the panel and tall enough to read,
    /// which the old 300×56 thumbnails were not. <paramref name="domain"/> is the axis
    /// the reader is told about; <paramref name="band"/> colours the line by
    /// the latest reading against that domain (Neutral pushes no colour, so an
    /// unbounded metric keeps the theme's own plot colour — see
    /// TrendAxisModel for why an arbitrary colour would be a lie).</summary>
    private static void PlotLarge(string label, float[] series, PlotDomain domain, PlotBand band)
    {
        if (series.Length == 0)
        {
            ImGui.TextUnformatted("no data");
            return;
        }
        string overlay = series[^1].ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        uint? colour = TrendAxisModel.BandColour(band);
        if (colour is { } c) ImGui.PushStyleColor(ImGuiCol.PlotLines, c);
        // The plot is a picture, not a control: drawn inert (full opacity), so ImGui's hover readout — a tooltip the
        // game otherwise never shows, and which the headless gate rightly treats as its error tooltip — cannot appear.
        // The values it would read out are the lines above the plot (UR-3: the panel grew, and the gate's pointer
        // started landing on it).
        ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1f);
        ImGui.BeginDisabled();
        ImGui.PlotLines(label, ref series[0], series.Length, 0, overlay,
            (float)domain.Floor, (float)domain.Ceiling,
            new System.Numerics.Vector2(Math.Max(120f, ImGui.GetContentRegionAvail().X - 4f), 220f * (ImGui.GetFontSize() / UiTheme.BodyFontPx)));
        ImGui.EndDisabled();
        ImGui.PopStyleVar();
        if (colour is not null) ImGui.PopStyleColor();
    }
}