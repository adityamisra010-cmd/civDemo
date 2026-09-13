using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
using Sim.Ui.Art;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// The window (T1.7/T1.8, D-023): terrain + vector rivers + overlays
/// (settlement marker, built paths, catchment fill toggle), the HUD panel
/// (population, food, last harvest, labor split + slider, End Turn), and
/// session recording. The UI is a VIEW + ORDER SOURCE, single-threaded (m1
/// spec §3): it holds a founded world, reads it, and feeds the sim exclusively
/// through the T0.7 order log; End Turn runs the executor synchronously. The
/// sim never reads UI state; nothing references Sim.Ui.
/// </summary>
public sealed class SimUiGame : Game
{
    private readonly UiSession _session;
    private readonly string _sessionLogPath;
    private WorldState _world; // convenience alias of _session.World, refreshed each End Turn

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _terrainTexture;
    private float _terrainDrawScale = 1f;
    private Texture2D? _markerTexture;
    private ImGuiRenderer? _imgui;
    private Camera? _camera;

    // --- art substrate (style-bible parchment atlas) ----------------------
    private readonly AssetLibrary _art = AssetLibrary.Load();
    private Texture2D? _grainTexture;
    private Texture2D? _panelTexture, _headerRuleTexture, _buttonPlateTexture,
                       _annalsTexture, _compassTexture;
    private IntPtr _panelId, _headerRuleId, _buttonPlateId, _annalsId, _compassId;
    private UiTheme.Fonts? _fonts;
    private string _bakeNote = "";

    /// <summary>Multiply blend for the §4 grain overlay: dst × src, so a
    /// near-white grain darkens everything faintly — map AND UI alike.</summary>
    private static readonly BlendState MultiplyBlend = new()
    {
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.SourceAlpha,
    };

    private TraversalLattice? _lattice;
    private int _latticeStride;

    private VertexBuffer? _riverVertices;
    private VertexBuffer? _pathVertices;
    private int _pathVersion = -1;      // NetworkEdges.Count when path mesh was built
    private VertexBuffer?[] _territoryVertices = [];  // T2.4: one tinted mesh per settlement
    private long _catchmentVersion = -1; // Σ LastRecomputeTurn when fills were built
    private BasicEffect? _worldEffect;
    private static readonly RasterizerState WorldRasterizer = new()
    {
        CullMode = CullMode.None,
        MultiSampleAntiAlias = true, // ADR-009: MSAA is the anti-aliasing choice
    };

    /// <summary>Built paths draw in INK-SOFT (§2) — a cartographer's road
    /// hatch, not a colored line. Symbology (road tiers, trade) is DEFERRED.</summary>
    private static readonly Color PathColor = new(
        ParchmentPalette.InkSoft.R, ParchmentPalette.InkSoft.G, ParchmentPalette.InkSoft.B, (byte)235);

    /// <summary>Territory fill alpha. Style-bible §2 rule: the twelve political
    /// colors are INK WASHES over parchment, not opaque fills — ~35% strength,
    /// so the paper and its terrain washes read through every territory.</summary>
    private static readonly byte TerritoryAlpha =
        (byte)Math.Round(255 * ParchmentPalette.TerritoryWashStrength);

    private bool _showCatchment = true; // T2.4: political geography on by default
    // T3.9b: the five-sector control's widget state (raw weight percentages,
    // farming..construction). Replaces the retired single farm-% slider, whose
    // 100 setting zeroed herding, extraction, crafting AND construction at
    // once — the gate session's 100/0/0/0/0 and the blunt instrument this
    // packet exists to remove.
    private readonly int[] _sectorWeights = new int[Sectors.Count];

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
        using var sim = Sim.Data.DataFiles.OpenSim();
        using var needs = Sim.Data.DataFiles.OpenNeeds();
        using var goods = Sim.Data.DataFiles.OpenGoods();
        return Sim.Core.Systems.SimConfigLoader.Load(sim, needs, goods);
    }

    /// <summary>T3.9a: the market panel's selected good — PURE UI STATE like
    /// the settlement selection. Starts at the first non-numeraire good (the
    /// numeraire's breakdown is all zeros by definition — nothing to explain).</summary>
    private int _selectedGood;
    private System.Collections.Generic.IReadOnlyList<MarketGoodRow> _marketRows = [];
    private System.Collections.Generic.IReadOnlyList<NeedsClassBlock> _needsBlocks = [];
    private System.Collections.Generic.IReadOnlyList<SectorBarRow> _sectorRows = [];
    private System.Collections.Generic.IReadOnlyList<TradeGoodRow> _tradeRows = [];
    private System.Collections.Generic.IReadOnlyList<TradeFlowLine> _tradeFlows = [];
    private string _tradeSummary = "";

    /// <summary>T2.4: the selected settlement id — PURE UI STATE (never in
    /// WorldState, never serialized). Starts at the first settlement.</summary>
    private int _selected;

    private MouseState _lastMouse;
    private KeyboardState _lastKeyboard;
    private bool _clickCandidate;   // press began on the map (not over ImGui)
    // D-A2: per-settlement label rects, measured each frame by DrawNameLabels
    // (row-aligned with world.Settlements) and consumed by the click hit-test.
    private readonly System.Collections.Generic.List<SettlementSelection.LabelRect> _labelRects = new();
    private int _clickDownX, _clickDownY;
    private double _fps;

    public SimUiGame(UiSession session, string sessionLogPath)
    {
        _session = session;
        _world = session.World;
        _sessionLogPath = sessionLogPath;
        _graphics = new GraphicsDeviceManager(this)
        {
            // T3.9a-b item 4: the default window IS the layout's design
            // resolution (PanelLayout.DesignWidth/Height = 1280×800) — read
            // from the tested view-model so the proven-non-overlapping
            // default layout and the actual window cannot drift apart.
            PreferredBackBufferWidth = PanelLayout.DesignWidth,
            PreferredBackBufferHeight = PanelLayout.DesignHeight,
            SynchronizeWithVerticalRetrace = true,
            PreferMultiSampling = true,
        };
        _graphics.PreparingDeviceSettings += (_, e) =>
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = BuildInfo.Describe(); // build identity: sha + date (T1.10)
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Fonts must join the atlas BEFORE the ImGui renderer uploads it (§3).
        ImGui.CreateContext();
        // T3.9a-b item 4: window state (position/size/COLLAPSED) is session-
        // scoped and in-memory ONLY. ImGui's default imgui.ini would persist
        // layout across runs — and a stale ini from an older build overrides
        // every FirstUseEver default below, resurrecting the pre-polish
        // overlapping layout. With the ini disabled the PanelLayout defaults
        // are authoritative at every launch, while the user's in-session
        // drag/resize/collapse changes stick for the whole session: ImGui
        // keeps per-window state in the live context, and no SetNextWindow*
        // call in this file uses ImGuiCond.Always — first-use only, so
        // nothing re-forces a panel after the user touches it.
        unsafe { ImGui.GetIO().NativePtr->IniFilename = null; }
        _fonts = UiTheme.LoadFonts(_art.Root);
        _imgui = new ImGuiRenderer(this, ownsContext: false);
        UiTheme.Apply();
        _worldEffect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };

        // THE PARCHMENT BAKE (§4 items 1–4): terrain wash tiles splatted by the
        // worldgen fields onto the paper, with inked coasts — one supersampled
        // texture, baked once, drawn with bilinear filtering (no per-frame cost,
        // so the 60 fps budget is untouched).
        ParchmentBaker.Result bake = ParchmentBaker.Bake(_world.Terrain!, _art, _world.Seed);
        _terrainTexture = new Texture2D(GraphicsDevice, bake.Size, bake.Size, false, SurfaceFormat.Color);
        _terrainTexture.SetData(bake.Rgba);
        // The atlas is SUPERSAMPLED (Size = worldSize × ss): it must be drawn
        // scaled to span the WORLD, not its own texel count. Drawn at native
        // size it covers ss× the world and every marker sits over the wash
        // painted for the cell at 1/ss of its coordinates — the coastal-
        // flooding defect (settlements rendered in the sea). The mapping is
        // pinned headless by CoastlineRenderTests via DisplayedTexel.
        _terrainDrawScale = (float)bake.WorldDrawScale;
        _bakeNote = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"map {bake.Size}² {bake.MegabytesResident:F0} MB baked in {bake.BakeMilliseconds:F0} ms");

        _grainTexture = UploadArt(_art.Get("parchment/grain"));
        _panelTexture = UploadArt(_art.Get("ui/panel"));
        // D-A1: the header rule is PROCEDURAL (HeaderRuleBaker) — a generated
        // resource like the parchment atlas, not a manifest drop point. Two
        // generated-asset rounds failed (docs/art-gate-defects.md); a ruled
        // line is drawn with an instrument, in code.
        _headerRuleTexture = UploadArt(Art.HeaderRuleBaker.Bake());
        _buttonPlateTexture = UploadArt(_art.Get("ui/button-plate"));
        _annalsTexture = UploadArt(_art.Get("ui/annals-bg"));
        _compassTexture = UploadArt(_art.Get("ui/compass-rose"));
        _panelId = _imgui.BindTexture(_panelTexture);
        _headerRuleId = _imgui.BindTexture(_headerRuleTexture);
        _buttonPlateId = _imgui.BindTexture(_buttonPlateTexture);
        _annalsId = _imgui.BindTexture(_annalsTexture);
        _compassId = _imgui.BindTexture(_compassTexture);

        int size = _world.Terrain!.Size;
        RebuildRiverBuffer(1.0);

        // The same lattice geometry the M1 systems compute on (pure of terrain).
        _lattice = TraversalLattice.Build(_world.Terrain, _displayCfg.Transport.RiverCostFactor);
        _latticeStride = OverlayMeshes.LatticeStride(_lattice, size);

        _markerTexture = UploadArt(_art.Get("ui/settlement-marker"));
        _camera = new Camera(size);
        _camera.Clamp(Viewport().Width, Viewport().Height);

        _selected = _world.Settlements.Count > 0 ? _world.Settlements[0].Id.Value : -1;
        RefreshHud(syncSlider: true);
        RebuildOverlays();
    }

    // D-A3: rivers hold a CLAMPED screen width (see RiverMesh.ScreenWidthForRank),
    // so the world-space strip is rebuilt when zoom changes — zoom moves on
    // wheel events only, never per frame. Measured rebuild cost on the
    // canonical 1024² terrain: see the D-A3 commit message.
    private double _riverBuiltZoom;

    private void RebuildRiverBuffer(double zoom)
    {
        _riverVertices?.Dispose();
        _riverVertices = MakeBuffer(RiverMeshToLine(RiverMesh.Build(_world.Terrain!, zoom)),
            new Color(ParchmentPalette.River.R, ParchmentPalette.River.G,
                      ParchmentPalette.River.B, (byte)255));
        _riverBuiltZoom = zoom;
    }

    private static LineGeometry.Vertex[] RiverMeshToLine(RiverMesh.Vertex[] mesh)
    {
        var result = new LineGeometry.Vertex[mesh.Length];
        for (int i = 0; i < mesh.Length; i++) result[i] = new(mesh[i].X, mesh[i].Y);
        return result;
    }

    private VertexBuffer? MakeBuffer(LineGeometry.Vertex[] mesh, Color color)
    {
        if (mesh.Length == 0) return null;
        var vertices = new VertexPositionColor[mesh.Length];
        for (int i = 0; i < mesh.Length; i++)
            vertices[i] = new VertexPositionColor(
                new Vector3((float)mesh[i].X, (float)mesh[i].Y, 0f), color);
        var buffer = new VertexBuffer(
            GraphicsDevice, VertexPositionColor.VertexDeclaration, vertices.Length, BufferUsage.None);
        buffer.SetData(vertices);
        return buffer;
    }

    /// <summary>Uploads an ArtImage (manifest asset or its placeholder) as a
    /// texture. Non-premultiplied RGBA straight from the PNG.</summary>
    private Texture2D UploadArt(ArtImage image)
    {
        var texture = new Texture2D(GraphicsDevice, image.Width, image.Height, false, SurfaceFormat.Color);
        texture.SetData(image.Rgba);
        return texture;
    }

    private void RebuildOverlays()
    {
        if (_world.NetworkEdges.Count != _pathVersion)
        {
            _pathVertices?.Dispose();
            _pathVertices = MakeBuffer(
                OverlayMeshes.BuildPaths(_world, _lattice!.Size, _latticeStride), PathColor);
            _pathVersion = _world.NetworkEdges.Count;
        }

        long catchmentVersion = 0;
        for (int i = 0; i < _world.CatchmentSummaries.Count; i++)
            catchmentVersion += _world.CatchmentSummaries[i].LastRecomputeTurn + 1;
        if (catchmentVersion != _catchmentVersion)
        {
            // T2.4: the partition as political geography — one translucent
            // mesh per settlement, tinted from the deterministic palette.
            foreach (VertexBuffer? buffer in _territoryVertices) buffer?.Dispose();
            LineGeometry.Vertex[][] fills =
                OverlayMeshes.BuildTerritoryFills(_world, _lattice!.Size, _latticeStride);
            _territoryVertices = new VertexBuffer?[fills.Length];
            for (int s = 0; s < fills.Length; s++)
            {
                ParchmentPalette.Rgba ink = ParchmentPalette.TerritoryInk(_world.Settlements[s].Id.Value);
                _territoryVertices[s] = MakeBuffer(fills[s], new Color(ink.R, ink.G, ink.B, TerritoryAlpha));
            }
            _catchmentVersion = catchmentVersion;
        }
    }

    /// <summary>Rebuilds the cached HUD snapshot for the current selection;
    /// optionally snaps the slider to the selected settlement's current split
    /// (on selection change and startup — never mid-drag, and not after End
    /// Turn, where a just-emitted order has not applied yet).</summary>
    private void RefreshHud(bool syncSlider)
    {
        _hud = HudModel.From(_world, _selected, _needs,
            _selected >= 0 ? _session.Names.Name(_selected) : null, _session.Config);

        // T4.19 lane B: the glass-box view models, rebuilt on the same cadence
        // (selection change / End Turn) — pure reads over the observation
        // history and the (prev, next) pair the session holds.
        _turnAudit = ScreenModels.TurnAudit(_session);
        _settlementView = ScreenModels.Settlement(_session, _selected);
        _policyView = ScreenModels.Policy(_session, _selected);
        _trendMetrics = TrendsModel.Metrics(_displayCfg.Registries.Classes);
        if (_trendIndex >= _trendMetrics.Count) _trendIndex = 0;


        // T3.9a: the read-only market/needs/sector displays, recomputed on
        // the same cadence as the HUD snapshot (selection change / End Turn).
        Sim.Core.Systems.GoodsConfig goods = _displayCfg.Goods!;
        if (_selectedGood == 0)
            foreach (Sim.Core.Systems.GoodEntry g in goods.Goods)
                if (!g.Numeraire) { _selectedGood = g.Id; break; }
        _marketRows = MarketModel.Rows(_world, _selected, goods);
        _needsBlocks = NeedsPanelModel.Blocks(_world, _selected, _needs, _displayCfg.Registries.Classes);
        SectorAllocationRow allocation = Sectors.Default(new SettlementId(_selected));
        for (int i = 0; i < _world.SectorAllocations.Count; i++)
            if (_world.SectorAllocations[i].Settlement.Value == _selected)
            { allocation = _world.SectorAllocations[i]; break; }
        _sectorRows = SectorBarModel.Rows(allocation);
        // T3.9b: snap the control to the settlement's CURRENT split on
        // selection change and startup — never mid-edit, and not after End
        // Turn, where a just-submitted batch has not applied yet (the same
        // cadence rule the retired farm-% slider followed). Weights are the
        // normalized shares as percentages, so what the control shows on
        // arrival is what the sim is actually running.
        // T4.18: snap through the allocation model, which rounds by largest
        // remainder. Rounding each share independently loses units — an uneven
        // split floors in several places at once — so the old form could open
        // the panel reading 99% before the director had touched anything.
        if (syncSlider) SectorAllocationModel.FromShares(allocation, _sectorWeights);

        // T3.9b: the trade panel's rows — world-level, not per-settlement
        // (trade is a pairwise mechanism over every settlement).
        _tradeRows = TradeModel.Rows(_world, goods);
        _tradeFlows = TradeModel.Flows(_world, goods);
        _tradeSummary = TradeModel.SummaryLine(_tradeRows);
    }

    // --- the player's verbs ---------------------------------------------------

    // Stamping/stepping/persistence all live in UiSession (T1.9 adversarial
    // hardening): the replay-equivalence test drives the SAME code paths.
    // T3.9b: the five-sector submit. One BATCH of five SectorAllocation
    // orders for the SELECTED settlement (T2.4 targeting, unchanged). The
    // session refuses an all-zero allocation and returns false; on refusal
    // nothing is written and nothing is claimed.
    private void SubmitSectorOrders()
    {
        if (!_session.EmitSectorOrders(_sectorWeights, _selected)) return;
        SaveSession();
        RefreshHud(syncSlider: false);
    }

    private void EndTurn()
    {
        _session.EndTurn();
        _world = _session.World;
        RefreshHud(syncSlider: false);
        RebuildOverlays();
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
        // settlement records, the glass box as it was actually played.
        _session.ExportTelemetry(UiSession.TelemetryPath(_sessionLogPath));
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        SaveSession();
        base.OnExiting(sender, args);
    }

    private Rectangle Viewport() => GraphicsDevice.Viewport.Bounds;

    protected override void Update(GameTime gameTime)
    {
        MouseState mouse = Mouse.GetState();
        KeyboardState keyboard = Keyboard.GetState();
        Rectangle viewport = Viewport();
        ImGuiIOPtr io = ImGui.GetIO();

        // T4.19 lane B: Escape CLOSES the open panel; with nothing open it
        // exits, as T4.18 did. Pressed-edge on polled state (a held key fires
        // once), and only when ImGui does not want the keyboard — a text field
        // owns its own Escape. GameSections.OnEscape says which case applied,
        // so one press is never both "close" and "exit".
        if (IsActive && !io.WantCaptureKeyboard
            && keyboard.IsKeyDown(Keys.Escape) && !_lastKeyboard.IsKeyDown(Keys.Escape))
        {
            (Section next, bool closed) = GameSections.OnEscape(_openSection);
            _openSection = next;
            if (!closed) Exit();
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
                int hit = SettlementSelection.HitTest(
                    _world, _camera!, mouse.X, mouse.Y, viewport.Width, viewport.Height,
                    _labelRects);   // D-A2: labels are part of the click target
                if (hit >= 0 && hit != _selected)
                {
                    _selected = hit;
                    RefreshHud(syncSlider: true);
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
            double panPx = 600.0 * gameTime.ElapsedGameTime.TotalSeconds;
            double dx = 0, dy = 0;
            if (keyboard.IsKeyDown(Keys.W)) dy += panPx;
            if (keyboard.IsKeyDown(Keys.S)) dy -= panPx;
            if (keyboard.IsKeyDown(Keys.A)) dx += panPx;
            if (keyboard.IsKeyDown(Keys.D)) dx -= panPx;
            if (dx != 0 || dy != 0) _camera!.Pan(dx, dy, viewport.Width, viewport.Height);

            // T4.19 lane B: digits 1..7 OPEN sections in roster order (key
            // edge; the same GameSections.Order the command bar draws, so the
            // key and the button cannot disagree about which section is which).
            for (int digit = 1; digit <= GameSections.Order.Count; digit++)
            {
                Keys key = Keys.D0 + digit;
                if (keyboard.IsKeyDown(key) && !_lastKeyboard.IsKeyDown(key))
                    _openSection = GameSections.OnDigit(_openSection, digit);
            }

            // T2.4: Tab cycles the selection in settlement-id order (key edge).
            if (keyboard.IsKeyDown(Keys.Tab) && !_lastKeyboard.IsKeyDown(Keys.Tab))
            {
                int next = SettlementSelection.CycleNext(_world, _selected);
                if (next >= 0 && next != _selected)
                {
                    _selected = next;
                    RefreshHud(syncSlider: true);
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
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        double dt = gameTime.ElapsedGameTime.TotalSeconds;
        if (dt > 0) _fps = _fps * 0.95 + (1.0 / dt) * 0.05;

        GraphicsDevice.Clear(new Color(
            ParchmentPalette.PaperShade.R, ParchmentPalette.PaperShade.G, ParchmentPalette.PaperShade.B));
        Rectangle viewport = Viewport();
        Camera cam = _camera!;
        if (cam.Zoom != _riverBuiltZoom) RebuildRiverBuffer(cam.Zoom);   // D-A3

        var transform =
            Matrix.CreateTranslation((float)-cam.CenterX, (float)-cam.CenterY, 0f)
            * Matrix.CreateScale((float)cam.Zoom)
            * Matrix.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f);

        _spriteBatch!.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        _spriteBatch.Draw(_terrainTexture, Vector2.Zero, null, Color.White,
            0f, Vector2.Zero, _terrainDrawScale, SpriteEffects.None, 0f);
        _spriteBatch.End();

        // World-space vector layers: catchment fill (toggle) under paths under rivers.
        _worldEffect!.World = transform;
        _worldEffect.Projection = Matrix.CreateOrthographicOffCenter(
            0f, viewport.Width, viewport.Height, 0f, -1f, 1f);
        GraphicsDevice.RasterizerState = WorldRasterizer;
        GraphicsDevice.BlendState = BlendState.NonPremultiplied; // translucent fill needs alpha
        if (_showCatchment)
        {
            foreach (VertexBuffer? territory in _territoryVertices)
                DrawWorldBuffer(territory);
        }
        DrawWorldBuffer(_pathVertices);
        DrawWorldBuffer(_riverVertices);

        // Settlement markers: world-anchored, constant SCREEN size — readable at
        // every zoom by construction (no transform on the sprite pass).
        // NonPremultiplied: our textures carry straight alpha from the PNG
        // (UploadArt does not premultiply); SpriteBatch's default AlphaBlend
        // assumes premultiplied and would rim the real marker's antialiased
        // edge with a bright halo. The ImGui pass already draws straight alpha.
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.NonPremultiplied);
        for (int i = 0; i < _world.Settlements.Count; i++)
        {
            LineGeometry.Vertex position = OverlayMeshes.SettlementPosition(
                _world.Settlements[i], _world.Terrain!.Size);
            (double sx, double sy) = cam.WorldToScreen(
                position.X, position.Y, viewport.Width, viewport.Height);
            const float markerPx = (float)SettlementSelection.MarkerScreenPx;
            bool isSelected = _world.Settlements[i].Id.Value == _selected;
            if (isSelected)
            {
                // T2.4: the selected marker is unmistakable — a gold halo ring
                // behind an enlarged marker.
                const float haloPx = markerPx + 10f;
                _spriteBatch.Draw(_markerTexture,
                    new Rectangle((int)(sx - haloPx / 2), (int)(sy - haloPx / 2),
                        (int)haloPx, (int)haloPx), new Color(0xFF, 0xD2, 0x5A, 0xFF));
            }
            float drawPx = isSelected ? markerPx + 4f : markerPx;
            _spriteBatch.Draw(_markerTexture,
                new Rectangle((int)(sx - drawPx / 2), (int)(sy - drawPx / 2),
                    (int)drawPx, (int)drawPx), Color.White);
        }
        _spriteBatch.End();

        DrawHud(gameTime);
        DrawGrainOverlay();   // §4 item 2: multiplied over EVERYTHING, UI included
        base.Draw(gameTime);
    }

    /// <summary>The age/grain overlay (style-bible §4 item 2): one screen-filling
    /// quad of the tiling grain texture, MULTIPLIED over the finished frame —
    /// map, panels and text alike — so the whole window reads as one sheet of
    /// paper rather than a map with widgets floating above it. Near-white
    /// texture, so the effect is tooth, not dirt.</summary>
    private void DrawGrainOverlay()
    {
        if (_grainTexture is null) return;
        Rectangle viewport = Viewport();
        _spriteBatch!.Begin(samplerState: SamplerState.LinearWrap, blendState: MultiplyBlend);
        _spriteBatch.Draw(_grainTexture, viewport,
            new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);
        _spriteBatch.End();
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
        ImDrawListPtr drawList = ImGui.GetBackgroundDrawList();
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
            // Shadowed for readability over any terrain color.
            drawList.AddText(pos + new System.Numerics.Vector2(1, 1), 0xE0000000u, name);
            drawList.AddText(pos, 0xFFE8DCC0u, name); // parchment
            // D-A2: the label is part of the click target. The rect is
            // measured HERE (the renderer owns font metrics) and handed to
            // the pure view-model — ImGui never crosses into SettlementSelection.
            System.Numerics.Vector2 extent = ImGui.CalcTextSize(name);
            _labelRects.Add(new SettlementSelection.LabelRect(
                pos.X, pos.Y, pos.X + extent.X, pos.Y + extent.Y));
        }
    }

    /// <summary>
    /// Panel furniture (§4 item 5): a parchment plate behind the current
    /// ImGui window plus a 9-sliced inked border and a header rule. Drawn on
    /// the WINDOW draw list at Begin time, so every widget added afterwards
    /// sits on top of it.
    ///
    /// T4.19 lane D: the rule's placement is the ELEMENT's, from
    /// ChromeGeometry, not "under the title bar" for every window. The
    /// T4.18 chrome has no title bars, and in the 56 px command bar the
    /// under-title-bar formula (y = top + frameHeight + 2 = 31..39) crossed
    /// the button row (12..42). The caller names its element; the rect the
    /// rule is drawn into is the rect the headless test checks.
    /// </summary>
    private void DrawPanelFurniture(in ChromeElement element, IntPtr backgroundId = default)
    {
        if (_panelId == IntPtr.Zero) return;
        ImDrawListPtr list = ImGui.GetWindowDrawList();
        System.Numerics.Vector2 min = ImGui.GetWindowPos();
        System.Numerics.Vector2 max = min + ImGui.GetWindowSize();

        if (backgroundId != default)
        {
            // Tiled parchment sheet: uv spans the window in texture multiples,
            // so the ruled lines keep a constant pitch at any panel size.
            var uv = new System.Numerics.Vector2(
                (max.X - min.X) / 128f, (max.Y - min.Y) / 128f);
            list.AddImage(backgroundId, min, max, System.Numerics.Vector2.Zero, uv, 0xFFFFFFFFu);
        }

        NineSlice(list, _panelId, min, max, ChromeGeometry.FrameBorderPx, 64f);

        if (_headerRuleId != IntPtr.Zero)
        {
            // D-A1 fix (docs/art-gate-defects.md): native dimensions from the
            // LOADED asset, uv from the pure view-model — uniform scale set by
            // the vertical mapping, horizontal overflow tiled, so the rule's
            // ink weight is identical at every panel width. The same treatment
            // the parchment background above gets, applied horizontally.
            ScreenRect rule = ChromeGeometry.HeaderRule(element, ImGui.GetFrameHeight());
            (float u, float v) = ViewModel.PanelFurniture.HeaderRuleUv(
                _headerRuleTexture!.Width, _headerRuleTexture.Height, rule.Width, rule.Height);
            list.AddImage(_headerRuleId,
                new System.Numerics.Vector2(rule.X, rule.Y),
                new System.Numerics.Vector2(rule.Right, rule.Bottom),
                System.Numerics.Vector2.Zero, new System.Numerics.Vector2(u, v), 0xFFFFFFFFu);
        }
    }

    /// <summary>Classic 9-slice: corners at native size, edges stretched along
    /// one axis, centre skipped (the panel's own background shows through).</summary>
    private static void NineSlice(
        ImDrawListPtr list, IntPtr texture,
        System.Numerics.Vector2 min, System.Numerics.Vector2 max, float border, float textureSize)
    {
        float b = border, uvB = border / textureSize;
        void Piece(float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1) =>
            list.AddImage(texture,
                new System.Numerics.Vector2(x0, y0), new System.Numerics.Vector2(x1, y1),
                new System.Numerics.Vector2(u0, v0), new System.Numerics.Vector2(u1, v1), 0xFFFFFFFFu);

        // corners
        Piece(min.X, min.Y, min.X + b, min.Y + b, 0, 0, uvB, uvB);
        Piece(max.X - b, min.Y, max.X, min.Y + b, 1 - uvB, 0, 1, uvB);
        Piece(min.X, max.Y - b, min.X + b, max.Y, 0, 1 - uvB, uvB, 1);
        Piece(max.X - b, max.Y - b, max.X, max.Y, 1 - uvB, 1 - uvB, 1, 1);
        // edges
        Piece(min.X + b, min.Y, max.X - b, min.Y + b, uvB, 0, 1 - uvB, uvB);
        Piece(min.X + b, max.Y - b, max.X - b, max.Y, uvB, 1 - uvB, 1 - uvB, 1);
        Piece(min.X, min.Y + b, min.X + b, max.Y - b, 0, uvB, uvB, 1 - uvB);
        Piece(max.X - b, min.Y + b, max.X, max.Y - b, 1 - uvB, uvB, 1, 1 - uvB);
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
    private void PushDataFont() { if (_fonts is { } f) ImGui.PushFont(f.Numeric); }
    private void PopDataFont() { if (_fonts is not null) ImGui.PopFont(); }

    private static void Plot(string label, float[] series)
    {
        if (series.Length == 0)
        {
            ImGui.TextUnformatted("no data");
            return;
        }
        string overlay = series[^1].ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        ImGui.PlotLines(label, ref series[0], series.Length, 0, overlay,
            float.MaxValue, float.MaxValue, new System.Numerics.Vector2(300, 56));
    }

    private void DrawWorldBuffer(VertexBuffer? buffer)
    {
        if (buffer is null) return;
        GraphicsDevice.SetVertexBuffer(buffer);
        foreach (EffectPass pass in _worldEffect!.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, buffer.VertexCount / 3);
        }
    }

    private void DrawHud(GameTime gameTime)
    {
        _imgui!.BeforeLayout(gameTime);
        if (_fonts is { } fonts) ImGui.PushFont(fonts.Body);

        DrawCompassRose();  // art substrate: §4 item 5 furniture
        DrawNameLabels();   // T2.9: background drawlist — under all chrome

        DrawStatusBand();
        DrawSelectionCard();
        DrawContextPanel();
        DrawCommandBar();

        if (_fonts is not null) ImGui.PopFont();
        _imgui.AfterLayout();
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

    /// <summary>
    /// THE ALWAYS-TRUE BAND: when, how many, how much. Four facts that are
    /// worth screen space on every frame of every turn, laid out horizontally
    /// so they cost 48 pixels of height instead of a column.
    ///
    /// T4.19 lane B: the population and food figures are CLICKABLE — each a
    /// Selectable sized to its own text, routed through ExplainRouting to the
    /// TURN account that decomposes it — and the turn digest from the latest
    /// TurnRecord follows them after the first End Turn. Read-only: the band
    /// still emits no order.
    /// </summary>
    private void DrawStatusBand()
    {
        BeginChrome(PanelLayout.Status);
        DrawPanelFurniture(ChromeGeometry.Status);   // rule along the BOTTOM edge: status | world
        PushDataFont();
        ImGui.TextUnformatted(_hud.ClockLine);
        ImGui.SameLine(0, 28);
        Figure(_hud.WorldPopulationFigure, ExplainFigure.WorldPopulation);
        ImGui.SameLine(0, 8);
        ImGui.TextUnformatted(_hud.SettlementCountFigure);
        ImGui.SameLine(0, 28);
        Figure(_hud.WorldFoodFigure, ExplainFigure.WorldFood);
        if (_turnAudit is { } audit)
        {
            ImGui.SameLine(0, 28);
            ImGui.TextUnformatted("last turn: " + audit.Digest);
        }
        PopDataFont();
        ImGui.End();
    }

    /// <summary>A clickable figure: a Selectable the size of its text, so it
    /// reads as the number it was and opens the surface that explains it.</summary>
    private void Figure(string text, ExplainFigure figure)
    {
        if (ImGui.Selectable(text + "##fig-" + figure.ToString(), false,
                ImGuiSelectableFlags.None, ImGui.CalcTextSize(text)))
        {
            Route(ExplainRouting.For(figure));
        }
    }

    /// <summary>Applies a click-to-explain route: opens the section (and tab),
    /// unfolds the TURN account it names, unfolds the happiness factors when
    /// the route asks for them. Pure UI state.</summary>
    private void Route(ExplainRoute route)
    {
        _openSection = route.Section;
        if (route.Section == Section.Settlement) _settlementTab = route.Tab;
        if (route.Expand == AuditExpand.Population) _auditExpanded[0] = true;
        if (route.Expand == AuditExpand.Grain) _auditExpanded[1] = true;
        if (route.ShowHappinessFactors) _showHappinessFactors = true;
    }

    /// <summary>
    /// The selected settlement, floating over the map: selection is how every
    /// section is aimed, so losing sight of it while looking at the world would
    /// make the world view useless for deciding anything.
    ///
    /// T4.19 lane B: population, food, happiness and grievance are clickable
    /// figures routed to the SETTLEMENT tab that decomposes each.
    /// </summary>
    private void DrawSelectionCard()
    {
        BeginChrome(PanelLayout.Selection);
        DrawPanelFurniture(ChromeGeometry.Selection);   // under the title line, as before
        ImGui.TextUnformatted(_hud.TitleLine);
        PushDataFont();
        Figure(_hud.PopulationLine, ExplainFigure.SettlementPopulation);
        Figure(_hud.FoodLine, ExplainFigure.SettlementFood);
        Figure(_hud.HappinessLine, ExplainFigure.SettlementHappiness);
        ImGui.SameLine(0, 16);
        Figure(_hud.GrievanceLine, ExplainFigure.SettlementGrievance);
        PopDataFont();
        ImGui.End();
    }

    /// <summary>
    /// THE VERBS AND THE WAYS OF LOOKING, on one row. End Turn sits apart from
    /// the section navigation because it is the only control here that changes
    /// the world; the rest change only what is on screen.
    /// </summary>
    private void DrawCommandBar()
    {
        BeginChrome(PanelLayout.Command);
        DrawPanelFurniture(ChromeGeometry.Command);   // rule along the TOP edge: world | controls

        // T4.19 lane D: every control is placed at the rect ChromeGeometry
        // computes — cursor set explicitly, not left to WindowPadding and
        // SameLine spacing — so the row the headless test proves disjoint
        // from the rule is the row that is drawn. (WindowPadding.x is 14 and
        // Margin is 12; the old row started at 14 by accident of the style.)
        //
        // T3.9a-b item 1 discoverability: the binding is shown ON the button.
        // Both paths (click here, Space in Update) call EndTurn().
        PlaceCursor(PanelLayout.Command, ChromeGeometry.EndTurnButton);
        if (ImGui.Button("End Turn [Space]", Size(ChromeGeometry.EndTurnButton)))
            EndTurn();

        for (int i = 0; i < GameSections.Order.Count; i++)
        {
            Section section = GameSections.Order[i];
            ScreenRect slot = ChromeGeometry.NavButton(i);
            PlaceCursor(PanelLayout.Command, slot);

            // The open section reads as pressed, so the row says where you are
            // as well as where you can go.
            bool open = _openSection == section;
            if (open) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
            if (ImGui.Button(GameSections.Label(section) + "##nav", Size(slot)))
                _openSection = GameSections.Toggle(_openSection, section);
            if (open) ImGui.PopStyleColor();
        }

        ImGui.SetCursorPos(new System.Numerics.Vector2(
            ChromeGeometry.TerritoryToggleX - PanelLayout.Command.X,
            ChromeGeometry.ButtonRow.Y - PanelLayout.Command.Y));
        ImGui.Checkbox("territory", ref _showCatchment);
        ImGui.End();
    }

    /// <summary>Moves the ImGui cursor to a ChromeGeometry rect's top-left.
    /// SetCursorPos is window-local, so the screen rect is re-based on the
    /// panel it was computed against; scroll offsets are ImGui's to apply.</summary>
    private static void PlaceCursor(in PanelRect panel, in ScreenRect rect) =>
        ImGui.SetCursorPos(new System.Numerics.Vector2(rect.X - panel.X, rect.Y - panel.Y));

    private static System.Numerics.Vector2 Size(in ScreenRect rect) => new(rect.Width, rect.Height);

    /// <summary>
    /// The one contextual surface. Nothing is drawn at all when no section is
    /// open — that is the state the whole redesign exists to make reachable.
    /// </summary>
    private void DrawContextPanel()
    {
        if (_openSection == Section.None) return;

        BeginChrome(PanelLayout.Context);
        DrawPanelFurniture(ChromeGeometry.Context,   // rule under the header row
            _openSection == Section.Annals ? _annalsId : default);

        // T4.19 lane D — the header row: title at the left, close button
        // flush right, both a frame height tall. The old button was 24×20
        // under FramePadding (8,5) and a 19 px face — an 8×10 interior for a
        // 19 px glyph, which ImGui pins to the interior's top-left rather
        // than centring (ChromeGeometry.LabelAnchor models the clamp). The
        // rect is now the view-model's: square, frame-height, Margin from
        // the panel edge, and the text alignment is pushed explicitly so the
        // glyph's anchor is the rect's centre by construction. Whether the
        // GPU draws it there is the one hop no headless test can see.
        float frameHeight = ImGui.GetFrameHeight();
        ScreenRect close = ChromeGeometry.CloseButton(ChromeGeometry.Context, frameHeight);
        ImGui.AlignTextToFramePadding();   // title baseline against the frame-height row
        ImGui.TextUnformatted(GameSections.Title(_openSection));
        PlaceCursor(PanelLayout.Context, close);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign,
            new System.Numerics.Vector2(ChromeGeometry.CloseGlyphAlign, ChromeGeometry.CloseGlyphAlign));
        if (ImGui.Button(ChromeGeometry.CloseGlyph + "##close", Size(close)))
            _openSection = Section.None;
        ImGui.PopStyleVar();

        // The header rule IS the separator now; content starts under it. The
        // sections scroll inside a child so the header row is chrome that
        // stays put, and so a vertical scrollbar — ImGui hangs it on the
        // window's right edge, x = 381..395 in this 396 px panel — cannot
        // land on the close button (x = 355..384). Section content overflows
        // the 607 px below the header routinely now (a Grievance tab with three
        // classes and an open chain is several hundred lines), so the case
        // occurs on every tab.
        ImGui.SetCursorPosY(ChromeGeometry.ContentTop(ChromeGeometry.Context, frameHeight) - PanelLayout.Context.Y);
        // NoBackground: a child window paints ImGuiCol_ChildBg unless told not
        // to, and UiTheme sets ChildBg to a 0.55-alpha paper tint - so without
        // this flag every section's content region would be washed lighter than
        // its header row, with a hard edge at the child's bounds. The parchment
        // plate DrawPanelFurniture already painted is the background; the child
        // exists only to scroll, and must be invisible as a surface.
        ImGui.BeginChild("context-body", System.Numerics.Vector2.Zero,
            ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoBackground);
        switch (_openSection)
        {
            case Section.Turn: DrawTurnSection(); break;
            case Section.Settlement: DrawSettlementSection(); break;
            case Section.Policy: DrawPolicySection(); break;
            case Section.Economy: DrawEconomySection(); break;
            case Section.Annals: DrawAnnalsSection(); break;
            case Section.Trends: DrawTrendsSection(); break;
            case Section.More: DrawBuildSection(); break;
        }
        ImGui.EndChild();

        ImGui.End();
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
            RefreshHud(syncSlider: true);
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
            if (ImGui.Selectable(line.Line + "##audit-" + line.Key, _auditExpanded[i]))
                _auditExpanded[i] = !_auditExpanded[i];
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
            if (ImGui.Selectable(row.Line + "##where-pop-" + row.Settlement.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), row.Settlement == _selected))
                SelectAndCentre(row.Settlement);
        }
        PopDataFont();
        ImGui.TextUnformatted("largest deficit");
        PushDataFont();
        foreach (WhereRow row in audit.LargestDeficit)
        {
            if (ImGui.Selectable(row.Line + "##where-def-" + row.Settlement.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), row.Settlement == _selected))
                SelectAndCentre(row.Settlement);
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
            if (ImGui.Selectable(row.Line + "##good" + row.GoodId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                _selectedGood == row.GoodId))
            {
                _selectedGood = row.GoodId;
            }
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
        if (ImGui.Selectable(view.HappinessLine + "##happiness", _showHappinessFactors))
            _showHappinessFactors = !_showHappinessFactors;
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
                if (ImGui.Selectable("  " + factor.Line + "##factor-" + f.ToString(
                        System.Globalization.CultureInfo.InvariantCulture), _expandedFactor == f))
                    _expandedFactor = _expandedFactor == f ? -1 : f;
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
                if (ImGui.Selectable("  " + row.Line + "##need-" + id, open))
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
            if (ImGui.Button("open POLICY##lever-" + id)) _openSection = Section.Policy;
        }
    }

    /// <summary>
    /// POLICY — the only section the director ACTS in.
    ///
    /// T4.18 workstream B: the five sliders are a FIXED-SUM allocation of 100,
    /// not five independent numbers with a caption predicting what they would
    /// mean once divided through. Moving one rebalances the others
    /// (SectorAllocationModel, deterministic, tested), so the number on the
    /// slider IS the share the sim will run and the old "applies as …" preview
    /// has nothing left to say. The order payload is unchanged: D-032 weights,
    /// submitted as typed, normalized by the consumer.
    ///
    /// T4.19 lane B (B5): under the sliders, CURRENT shows declared beside
    /// effective; HISTORY lists every PolicyChange for this settlement newest
    /// first with its order number and, under each, the settlement's record
    /// on the turns after — labelled observed, never attributed; the per-turn
    /// PolicyState table is one checkbox away. The list of policies has one
    /// entry; M5's taxation is the second in the same shape.
    /// </summary>
    private void DrawPolicySection()
    {
        if (_selected < 0)
        {
            ImGui.TextUnformatted("Select a settlement on the map to set its labour.");
            return;
        }

        ImGui.TextUnformatted(_hud.TitleLine);
        foreach (PolicyEntry policy in PolicyHistoryModel.Policies)
            ImGui.TextUnformatted(policy.Name + " - " + policy.Note);
        ImGui.Separator();

        ImGui.TextUnformatted("labour allocation — always 100%");
        for (int s = 0; s < Sectors.Count; s++)
        {
            int before = _sectorWeights[s];
            ImGui.SetNextItemWidth(PanelLayout.Context.Width - 150);
            if (ImGui.SliderInt(SectorBarModel.SectorNames[s], ref _sectorWeights[s], 0, 100)
                && _sectorWeights[s] != before)
            {
                // The others absorb the difference immediately, so the panel is
                // never in a state that sums to 97 or 104 — not even mid-drag.
                SectorAllocationModel.Rebalance(_sectorWeights, s, _sectorWeights[s]);
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Apply labour split", new System.Numerics.Vector2(170, 28)))
            SubmitSectorOrders();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("CURRENT  (declared vs effective, currently running)");
        PushDataFont();
        foreach (SectorBarRow sector in _sectorRows)
        {
            ImGui.ProgressBar((float)sector.Fraction,
                new System.Numerics.Vector2(PanelLayout.Context.Width - 40, ImGui.GetFrameHeight()),
                sector.Label);
        }
        if (_policyView is { } current) foreach (string line in current.Current) ImGui.TextUnformatted(line);
        ImGui.Spacing();
        ImGui.TextUnformatted(_hud.FoodLine);
        ImGui.TextUnformatted(_hud.GrievanceLine);   // T2.6: display only
        PopDataFont();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("HISTORY  (newest first)");
        if (_policyView is { } history)
        {
            PushDataFont();
            if (history.History.Count == 0) ImGui.TextUnformatted("no change yet for this settlement");
            foreach (PolicyChangeView change in history.History)
            {
                ImGui.TextUnformatted(change.Line);
                foreach (string line in change.Consequences) ImGui.TextUnformatted(line);
            }
            PopDataFont();
            ImGui.Spacing();
            ImGui.Checkbox("per-turn policy table", ref _policyShowStates);
            if (_policyShowStates) DataLines(history.States);
        }
    }

    /// <summary>
    /// ECONOMY — T3.9b's trade panel. Its hard case is unchanged: on the
    /// canonical world nothing trades, and an empty panel reads as a broken one,
    /// so the summary states "no trade" as a counted fact and every good carries
    /// its own reason. T4.19 lane B: plus the world GoodAccount table from the
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
            ImGui.Checkbox($"all {lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} entries",
                ref _annalsShowAll);
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
        if (ImGui.RadioButton("world", _trendWorldScope)) _trendWorldScope = true;
        ImGui.SameLine();
        if (ImGui.RadioButton(_selected >= 0 ? _session.Names.Name(_selected) : "(no selection)",
                !_trendWorldScope))
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
            PlotLarge("##trend", _session.History.Price(_selected, _selectedGood));
            return;
        }
        double[] series = _trendWorldScope
            ? TrendsModel.World(_session.Observations, metric.Key, metric.ClassId)
            : TrendsModel.Settlement(_session.Observations, _selected, metric.Key, metric.ClassId);
        PushDataFont();
        ImGui.TextUnformatted(TrendsModel.LastValueLine(series));
        PopDataFont();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted(TrendsModel.ScopeNote(metric.Key, _trendWorldScope));
        ImGui.PopTextWrapPos();
        PlotLarge("##trend", TrendsModel.ForPlot(series));
    }

    /// <summary>BUILD — the glass-box footer. Diagnostic rather than play
    /// information, so it no longer costs screen space during play; every line
    /// it ever carried is still here, plus (T4.19 lane B) the session's five
    /// files so the director can find them.</summary>
    private void DrawBuildSection()
    {
        PushDataFont();
        ImGui.TextUnformatted(BuildInfo.Describe()); // same identity as the title
        ImGui.TextUnformatted(HudModel.StatusLine(_world.Seed, _fps));
        ImGui.TextUnformatted(HudModel.CameraLine(_camera!.CenterX, _camera.CenterY, _camera.Zoom));
        // Art-substrate provenance: which assets are real and which are
        // stand-ins, plus the bake cost — the glass-box habit applied to art.
        ImGui.TextUnformatted(_art.SummaryLine());
        ImGui.TextUnformatted(_bakeNote);
        if (_fonts is { } f) ImGui.TextUnformatted(f.Note);
        ImGui.Separator();
        foreach (string line in SessionFilesModel.Lines(_sessionLogPath)) ImGui.TextUnformatted(line);
        PopDataFont();
    }

    /// <summary>The trends plot: as wide as the panel and tall enough to read,
    /// which the old 300×56 thumbnails were not.</summary>
    private static void PlotLarge(string label, float[] series)
    {
        if (series.Length == 0)
        {
            ImGui.TextUnformatted("no data");
            return;
        }
        string overlay = series[^1].ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        ImGui.PlotLines(label, ref series[0], series.Length, 0, overlay,
            float.MaxValue, float.MaxValue,
            new System.Numerics.Vector2(PanelLayout.Context.Width - 40, 220));
    }
}
