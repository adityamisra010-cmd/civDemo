using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.Pathing;
using Sim.Core.State;
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
    private Texture2D? _markerTexture;
    private ImGuiRenderer? _imgui;
    private Camera? _camera;

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

    private static readonly Color PathColor = new(0x8B, 0x62, 0x3B, 0xFF);      // earth brown — never river blue

    /// <summary>T2.4: territory fill alpha — translucent enough for terrain to
    /// read through, opaque enough that twelve tints stay tellable apart.</summary>
    private const byte TerritoryAlpha = 0x50;

    private bool _showCatchment = true; // T2.4: political geography on by default
    private int _sliderFarmPct = 100;
    private HudModel _hud = null!;

    /// <summary>T2.4: the selected settlement id — PURE UI STATE (never in
    /// WorldState, never serialized). Starts at the first settlement.</summary>
    private int _selected;

    private MouseState _lastMouse;
    private KeyboardState _lastKeyboard;
    private bool _clickCandidate;   // press began on the map (not over ImGui)
    private int _clickDownX, _clickDownY;
    private double _fps;

    public SimUiGame(UiSession session, string sessionLogPath)
    {
        _session = session;
        _world = session.World;
        _sessionLogPath = sessionLogPath;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 800,
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
        _imgui = new ImGuiRenderer(this);
        _worldEffect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };

        int size = _world.Terrain!.Size;
        _terrainTexture = new Texture2D(GraphicsDevice, size, size, false, SurfaceFormat.Color);
        _terrainTexture.SetData(TerrainBaker.Bake(_world.Terrain));
        _riverVertices = MakeBuffer(RiverMeshToLine(RiverMesh.Build(_world.Terrain)),
            new Color(TerrainPalette.RiverColor.R, TerrainPalette.RiverColor.G,
                      TerrainPalette.RiverColor.B, TerrainPalette.RiverColor.A));

        // The same lattice geometry the M1 systems compute on (pure of terrain).
        _lattice = TraversalLattice.Build(_world.Terrain);
        _latticeStride = OverlayMeshes.LatticeStride(_lattice, size);

        _markerTexture = MakeMarkerTexture(32);
        _camera = new Camera(size);
        _camera.Clamp(Viewport().Width, Viewport().Height);

        _selected = _world.Settlements.Count > 0 ? _world.Settlements[0].Id.Value : -1;
        RefreshHud(syncSlider: true);
        RebuildOverlays();
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

    /// <summary>Filled circle with a dark rim — the settlement marker sprite.</summary>
    private Texture2D MakeMarkerTexture(int size)
    {
        var pixels = new Color[size * size];
        double r = size / 2.0 - 1.0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x + 0.5 - size / 2.0, dy = y + 0.5 - size / 2.0;
                double d = Math.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] =
                    d > r ? Color.Transparent
                    : d > r - 3.0 ? new Color(30, 24, 18, 255)      // rim
                    : new Color(0xF0, 0xE6, 0xC8, 0xFF);            // parchment fill
            }
        }
        var texture = new Texture2D(GraphicsDevice, size, size, false, SurfaceFormat.Color);
        texture.SetData(pixels);
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
                (byte r, byte g, byte b) = SettlementPalette.Color(_world.Settlements[s].Id.Value);
                _territoryVertices[s] = MakeBuffer(fills[s], new Color(r, g, b, TerritoryAlpha));
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
        _hud = HudModel.From(_world, _selected);
        if (syncSlider) _sliderFarmPct = (int)Math.Round(_hud.FarmSharePct);
    }

    // --- the player's verbs ---------------------------------------------------

    // Stamping/stepping/persistence all live in UiSession (T1.9 adversarial
    // hardening): the replay-equivalence test drives the SAME code paths.
    private void EmitLaborOrder(int farmPct)
    {
        _session.EmitLaborOrder(farmPct, _selected); // T2.4: orders the SELECTION
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

    private void SaveSession() => _session.Save(_sessionLogPath);

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
                    _world, _camera!, mouse.X, mouse.Y, viewport.Width, viewport.Height);
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

        _lastMouse = mouse;
        _lastKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        double dt = gameTime.ElapsedGameTime.TotalSeconds;
        if (dt > 0) _fps = _fps * 0.95 + (1.0 / dt) * 0.05;

        GraphicsDevice.Clear(new Color(10, 14, 20));
        Rectangle viewport = Viewport();
        Camera cam = _camera!;

        var transform =
            Matrix.CreateTranslation((float)-cam.CenterX, (float)-cam.CenterY, 0f)
            * Matrix.CreateScale((float)cam.Zoom)
            * Matrix.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f);

        _spriteBatch!.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        _spriteBatch.Draw(_terrainTexture, Vector2.Zero, Color.White);
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
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
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
        base.Draw(gameTime);
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
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(12, 12), ImGuiCond.FirstUseEver);
        ImGui.Begin("civ-sim", ImGuiWindowFlags.AlwaysAutoResize);

        // TextUnformatted ONLY (T1.8 re-gate finding 2): ImGui.Text runs printf
        // parsing, and the SplitLine's '%' rendered as garbage. Every HUD string
        // is pre-formatted by HudModel and passed through unparsed.
        ImGui.TextUnformatted(_hud.ClockLine);
        ImGui.TextUnformatted(_hud.WorldLine);      // T2.4: the world summary
        ImGui.Separator();
        ImGui.TextUnformatted(_hud.TitleLine);      // T2.4: the selected settlement
        ImGui.TextUnformatted(_hud.PopulationLine);
        ImGui.TextUnformatted(_hud.FoodLine);
        ImGui.TextUnformatted(_hud.SplitLine);
        ImGui.Separator();

        // The labor slider: emits ONE order, on release only (§3.9 log
        // hygiene), targeting the SELECTED settlement (T2.4).
        ImGui.SliderInt("farm %", ref _sliderFarmPct, 0, 100);
        if (ImGui.IsItemDeactivatedAfterEdit() && _selected >= 0)
            EmitLaborOrder(_sliderFarmPct);

        ImGui.Checkbox("territory overlay", ref _showCatchment);

        if (ImGui.Button("End Turn", new System.Numerics.Vector2(180, 32)))
            EndTurn();

        ImGui.Separator();
        ImGui.TextUnformatted(BuildInfo.Describe()); // same identity as the title
        ImGui.TextUnformatted(HudModel.StatusLine(_world.Seed, _fps));
        ImGui.TextUnformatted(HudModel.CameraLine(_camera!.CenterX, _camera.CenterY, _camera.Zoom));
        ImGui.End();
        _imgui.AfterLayout();
    }
}
