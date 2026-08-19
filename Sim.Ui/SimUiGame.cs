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
    private HudModel _hud = null!;

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
            _selected >= 0 ? _session.Names.Name(_selected) : null);


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
        if (syncSlider)
            for (int s = 0; s < Sectors.Count; s++)
                _sectorWeights[s] = (int)Math.Round(Sectors.Share(allocation, s) * 100.0);

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

        if (keyboard.IsKeyDown(Keys.Escape)) Exit();

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
    /// ImGui window plus a 9-sliced inked border and a header rule under the
    /// title bar. Drawn on the WINDOW draw list at Begin time, so every
    /// widget added afterwards sits on top of it.
    /// </summary>
    private void DrawPanelFurniture(IntPtr backgroundId = default)
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

        NineSlice(list, _panelId, min, max, 12f, 64f);

        if (_headerRuleId != IntPtr.Zero)
        {
            // D-A1 fix (docs/art-gate-defects.md): native dimensions from the
            // LOADED asset, uv from the pure view-model — uniform scale set by
            // the vertical mapping, horizontal overflow tiled, so the rule's
            // ink weight is identical at every panel width. The same treatment
            // the parchment background above gets, applied horizontally.
            float y = min.Y + ImGui.GetFrameHeight() + 2f;
            float h = ViewModel.PanelFurniture.HeaderRuleScreenHeightPx;
            float w = (max.X - min.X) - 12f;
            (float u, float v) = ViewModel.PanelFurniture.HeaderRuleUv(
                _headerRuleTexture!.Width, _headerRuleTexture.Height, w, h);
            list.AddImage(_headerRuleId,
                new System.Numerics.Vector2(min.X + 6f, y),
                new System.Numerics.Vector2(min.X + 6f + w, y + h),
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
    private static void ApplyPanelDefaults(in PanelRect rect)
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(rect.X, rect.Y), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(rect.Width, rect.Height), ImGuiCond.FirstUseEver);
    }

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

    /// <summary>T2.10: the graphs — world totals + the selected settlement,
    /// straight off the D-028 ring buffer (see HistoryBuffer for the replay/
    /// mid-game-load semantics). PlotLines autoscales per series; the overlay
    /// text carries the latest value so the line is readable as a number too.</summary>
    private void DrawGraphs()
    {
        ApplyPanelDefaults(PanelLayout.Graphs);
        ImGui.Begin(PanelLayout.Graphs.Title);
        DrawPanelFurniture();
        ViewModel.HistoryBuffer history = _session.History;
        ImGui.TextUnformatted("world");
        Plot("pop##world", history.World(HistoryBuffer.Metric.Population));
        Plot("food##world", history.World(HistoryBuffer.Metric.Food));
        Plot("grievance##world", history.World(HistoryBuffer.Metric.Grievance));
        ImGui.Separator();
        ImGui.TextUnformatted(_selected >= 0 ? _session.Names.Name(_selected) : "(no selection)");
        Plot("pop##sel", history.Settlement(_selected, HistoryBuffer.Metric.Population));
        Plot("food##sel", history.Settlement(_selected, HistoryBuffer.Metric.Food));
        Plot("grievance##sel", history.Settlement(_selected, HistoryBuffer.Metric.Grievance));
        ImGui.End();
    }

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

    /// <summary>T3.9a items 1+2: the market panel — the selected settlement's
    /// goods with stock, price and last move; the PriceTerms decomposition for
    /// the selected good (the glass-box artifact, surfaced); and the per-good
    /// price time series in the same PlotLines idiom as the T2.10 graphs.
    /// READ-ONLY: no widget in this window emits an order.</summary>
    private void DrawMarket()
    {
        ApplyPanelDefaults(PanelLayout.Market);
        ImGui.Begin(PanelLayout.Market.Title);
        DrawPanelFurniture();
        ImGui.TextUnformatted(_selected >= 0 ? _session.Names.Name(_selected) : "(no selection)");
        ImGui.Separator();
        PushDataFont();   // §3 rule (see PushDataFont): market rows are data lines
        foreach (MarketGoodRow row in _marketRows)
        {
            // Selectable rows pick the good whose decomposition + series show
            // below. TextUnformatted doctrine does not apply to Selectable
            // labels via printf — Selectable takes the label verbatim, but the
            // lines contain no '%' anyway (chg is a signed decimal).
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
        // Item 2 gate criterion: scarcity READS as a rising line. PlotLines
        // autoscales per series (the T2.10 idiom), so a flat price is a
        // boring flat line and a spike is unmissable.
        Plot("price##sel-good", _session.History.Price(_selected, _selectedGood));
        PopDataFont();
        ImGui.End();
    }

    /// <summary>
    /// T3.9b: the trade panel. Its hard case is that on the canonical world
    /// NOTHING TRADES (T3.6 measured zero flow), and an empty panel reads as a
    /// broken one. So the panel never renders emptiness: the summary line
    /// states "no trade" as a counted, deliberate fact, and every good carries
    /// its own reason — no spread at all (the escalation-2 band-edge pegging,
    /// with the shared price printed) or a spread under the deadband
    /// (escalation 1). Both escalations are M4 material; this panel makes the
    /// measured state legible and changes nothing about it.
    /// </summary>
    private void DrawTrade()
    {
        ApplyPanelDefaults(PanelLayout.Trade);
        ImGui.Begin(PanelLayout.Trade.Title);
        DrawPanelFurniture();
        PushDataFont();   // §3 rule (see PushDataFont): trade rows are data lines
        ImGui.TextUnformatted(_tradeSummary);
        ImGui.Separator();
        foreach (TradeFlowLine flow in _tradeFlows) ImGui.TextUnformatted(flow.Line);
        if (_tradeFlows.Count > 0) ImGui.Separator();
        foreach (TradeGoodRow row in _tradeRows) ImGui.TextUnformatted(row.Line);
        PopDataFont();
        ImGui.End();
    }

    /// <summary>T2.9: the annals — scrollable, newest LAST, auto-scrolled to
    /// the tail when new lines arrive while the reader is at the tail.</summary>
    private void DrawAnnals()
    {
        ApplyPanelDefaults(PanelLayout.Annals);
        ImGui.Begin(PanelLayout.Annals.Title);
        DrawPanelFurniture(_annalsId);   // the scholar's sheet behind the text
        ImGui.BeginChild("annal-scroll");
        // TextUnformatted under a wrap pos (T1.8 re-gate doctrine: never the
        // printf-parsing Text/TextWrapped for content strings).
        ImGui.PushTextWrapPos(0f);
        IReadOnlyList<string> lines = _session.AnnalLines;
        for (int i = 0; i < lines.Count; i++) ImGui.TextUnformatted(lines[i]);
        ImGui.PopTextWrapPos();
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f)
            ImGui.SetScrollHereY(1f);
        ImGui.EndChild();
        ImGui.End();
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
        DrawNameLabels();   // T2.9: background drawlist — under all panels
        DrawAnnals();       // T2.9
        DrawGraphs();       // T2.10
        DrawMarket();       // T3.9a: goods, prices, decomposition, series
        DrawTrade();        // T3.9b: flows, or WHY nothing flowed
        ApplyPanelDefaults(PanelLayout.Hud);
        // T3.9a-b item 4: the HUD has a FIXED default size and scrolls when
        // content exceeds it (AlwaysAutoResize had no height cap, so the
        // panel grew past the 800 px window bottom and under the Annals —
        // the gate's overlap). HorizontalScrollbar so an over-long footer
        // line scrolls instead of silently clipping at the fixed width.
        ImGui.Begin(PanelLayout.Hud.Title, ImGuiWindowFlags.HorizontalScrollbar);
        DrawPanelFurniture();

        // TextUnformatted ONLY (T1.8 re-gate finding 2): ImGui.Text runs printf
        // parsing, and the SplitLine's '%' rendered as garbage. Every HUD string
        // is pre-formatted by HudModel and passed through unparsed.
        PushDataFont();   // §3 rule (see PushDataFont): clock/world are data lines
        ImGui.TextUnformatted(_hud.ClockLine);
        ImGui.TextUnformatted(_hud.WorldLine);      // T2.4: the world summary
        PopDataFont();
        ImGui.Separator();
        ImGui.TextUnformatted(_hud.TitleLine);      // T2.4: the selected settlement
        PushDataFont();   // §3 rule: the settlement data block, one face throughout
        ImGui.TextUnformatted(_hud.PopulationLine);
        ImGui.TextUnformatted(_hud.FoodLine);
        // T3.9a item 4: the five-sector split as five labelled bar rows
        // (DISPLAY ONLY — the slider below stays the only control until T3.9b).
        // T3.9a-b item 2: row height is the MEASURED frame height of the
        // active font (line height + vertical frame padding), never a
        // literal — the literal 14 sat under the font's line height and
        // clipped every label top and bottom (gate Q2).
        foreach (SectorBarRow sector in _sectorRows)
            ImGui.ProgressBar((float)sector.Fraction,
                new System.Numerics.Vector2(220, ImGui.GetFrameHeight()), sector.Label);
        ImGui.TextUnformatted(_hud.GrievanceLine);          // T2.6: display only
        PopDataFont();
        ImGui.Separator();
        // T3.9a item 3: needs PER CLASS (T3.5 baskets differ by class — the
        // panel shows the difference; unbound needs stay honestly labelled).
        foreach (NeedsClassBlock block in _needsBlocks)
        {
            ImGui.TextUnformatted(block.HeaderLine);   // class header: body face
            PushDataFont();   // §3 rule: per-need values are data lines
            foreach (string line in block.NeedLines) ImGui.TextUnformatted(line);
            PopDataFont();
        }
        ImGui.Separator();

        // T3.9b: REAL PER-SECTOR CONTROL, replacing the farm-% slider.
        // Five raw weights, submitted AS TYPED — normalization happens in the
        // consumer (Sectors.Share), and the preview line below shows exactly
        // what the sim will run, so normalization is never invisible. Emitted
        // on an explicit Apply rather than per-slider-release: a five-way
        // allocation is one decision, and one decision is one batch in the
        // log (§3.9 log hygiene).
        for (int s = 0; s < Sectors.Count; s++)
            ImGui.SliderInt(SectorBarModel.SectorNames[s], ref _sectorWeights[s], 0, 100);
        PushDataFont();
        ImGui.TextUnformatted(_selected >= 0
            ? SectorOrderFactory.PreviewLine(new SettlementId(_selected), _sectorWeights)
            : "applies as —");
        PopDataFont();
        bool canSubmit = _selected >= 0 && SectorOrderFactory.CanSubmit(_sectorWeights);
        ImGui.BeginDisabled(!canSubmit);
        if (ImGui.Button("Apply labor split", new System.Numerics.Vector2(180, 28)))
            SubmitSectorOrders();
        ImGui.EndDisabled();
        if (!canSubmit && _selected >= 0)
            ImGui.TextUnformatted("give at least one sector a positive weight");

        ImGui.Checkbox("territory overlay", ref _showCatchment);

        // T3.9a-b item 1 discoverability: the binding is shown ON the button
        // itself. Both paths (click here, Space in Update) call EndTurn().
        if (ImGui.Button("End Turn [Space]", new System.Numerics.Vector2(180, 32)))
            EndTurn();

        ImGui.Separator();
        PushDataFont();   // §3 rule: the debug footer is data lines
        ImGui.TextUnformatted(BuildInfo.Describe()); // same identity as the title
        ImGui.TextUnformatted(HudModel.StatusLine(_world.Seed, _fps));
        ImGui.TextUnformatted(HudModel.CameraLine(_camera!.CenterX, _camera.CenterY, _camera.Zoom));
        // Art-substrate provenance: which assets are real and which are
        // stand-ins, plus the bake cost — the glass-box habit applied to art.
        ImGui.TextUnformatted(_art.SummaryLine());
        ImGui.TextUnformatted(_bakeNote);
        if (_fonts is { } f) ImGui.TextUnformatted(f.Note);
        PopDataFont();
        ImGui.End();
        if (_fonts is not null) ImGui.PopFont();
        _imgui.AfterLayout();
    }
}
