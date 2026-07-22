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
    private VertexBuffer? _catchmentVertices;
    private long _catchmentVersion = -1; // Σ LastRecomputeTurn when fill was built
    private BasicEffect? _worldEffect;
    private static readonly RasterizerState WorldRasterizer = new()
    {
        CullMode = CullMode.None,
        MultiSampleAntiAlias = true, // ADR-009: MSAA is the anti-aliasing choice
    };

    private static readonly Color PathColor = new(0x8B, 0x62, 0x3B, 0xFF);      // earth brown — never river blue
    private static readonly Color CatchmentFill = new(0xE8, 0xD9, 0x6A, 0x46);  // translucent field-yellow

    private bool _showCatchment;
    private int _sliderFarmPct = 100;
    private long _previousHarvestTotal;
    private HudModel _hud = null!;

    private MouseState _lastMouse;
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

        _hud = HudModel.From(_world, previousHarvestTotal: 0);
        _previousHarvestTotal = _hud.HarvestTotal;
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
            _catchmentVertices?.Dispose();
            _catchmentVertices = MakeBuffer(
                OverlayMeshes.BuildCatchmentFill(_world, _lattice!.Size, _latticeStride), CatchmentFill);
            _catchmentVersion = catchmentVersion;
        }
    }

    // --- the player's verbs ---------------------------------------------------

    // Stamping/stepping/persistence all live in UiSession (T1.9 adversarial
    // hardening): the replay-equivalence test drives the SAME code paths.
    private void EmitLaborOrder(int farmPct)
    {
        _session.EmitLaborOrder(farmPct);
        SaveSession();
    }

    private void EndTurn()
    {
        _session.EndTurn();
        _world = _session.World;
        _hud = HudModel.From(_world, _previousHarvestTotal);
        _previousHarvestTotal = _hud.HarvestTotal;
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
        }

        _lastMouse = mouse;
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
        if (_showCatchment) DrawWorldBuffer(_catchmentVertices);
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
            const float markerPx = 14f;
            _spriteBatch.Draw(_markerTexture,
                new Rectangle((int)(sx - markerPx / 2), (int)(sy - markerPx / 2),
                    (int)markerPx, (int)markerPx), Color.White);
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
        ImGui.TextUnformatted(_hud.PopulationLine);
        ImGui.TextUnformatted(_hud.FoodLine);
        ImGui.TextUnformatted(_hud.SplitLine);
        ImGui.Separator();

        // The labor slider: emits ONE order, on release only (§3.9 log hygiene).
        ImGui.SliderInt("farm %", ref _sliderFarmPct, 0, 100);
        if (ImGui.IsItemDeactivatedAfterEdit() && _world.Settlements.Count > 0)
            EmitLaborOrder(_sliderFarmPct);

        ImGui.Checkbox("catchment overlay", ref _showCatchment);

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
