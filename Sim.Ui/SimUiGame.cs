using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Sim.Core.State;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// The window (T1.7, D-023): renders a founded world's terrain — smooth-shaded,
/// no visible tiles (bilinear sampling over the once-baked texture) — with
/// pan/zoom camera and a single ImGui debug panel. VIEW ONLY this packet:
/// End Turn, overlays and orders arrive at T1.8. The UI holds the WorldState
/// and reads it; the sim never reads UI state, and nothing references Sim.Ui.
/// </summary>
public sealed class SimUiGame : Game
{
    private readonly WorldState _world;
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _terrainTexture;
    private ImGuiRenderer? _imgui;
    private Camera? _camera;

    private MouseState _lastMouse;
    private double _fps;

    public SimUiGame(WorldState world)
    {
        _world = world;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 800,
            SynchronizeWithVerticalRetrace = true, // 60 fps target: vsync
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "civ-sim — M1 walking skeleton";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _imgui = new ImGuiRenderer(this);

        // Bake ONCE (ADR-008: terrain is immutable in M1).
        int size = _world.Terrain!.Size;
        _terrainTexture = new Texture2D(GraphicsDevice, size, size, false, SurfaceFormat.Color);
        _terrainTexture.SetData(TerrainBaker.Bake(_world.Terrain));

        _camera = new Camera(size);
        _camera.Clamp(Viewport().Width, Viewport().Height);
    }

    private Rectangle Viewport() => GraphicsDevice.Viewport.Bounds;

    protected override void Update(GameTime gameTime)
    {
        MouseState mouse = Mouse.GetState();
        KeyboardState keyboard = Keyboard.GetState();
        Rectangle viewport = Viewport();
        ImGuiIOPtr io = ImGui.GetIO();

        if (keyboard.IsKeyDown(Keys.Escape)) Exit();

        // Camera input — skipped while ImGui wants the mouse/keyboard.
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
            double panPx = 600.0 * gameTime.ElapsedGameTime.TotalSeconds; // screen px/s
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
        if (dt > 0) _fps = _fps * 0.95 + (1.0 / dt) * 0.05; // smoothed display fps

        GraphicsDevice.Clear(new Color(10, 14, 20));
        Rectangle viewport = Viewport();
        Camera cam = _camera!;

        // World transform from the pure camera: scale then translate (floats
        // only HERE, at the render boundary).
        var transform =
            Matrix.CreateTranslation((float)-cam.CenterX, (float)-cam.CenterY, 0f)
            * Matrix.CreateScale((float)cam.Zoom)
            * Matrix.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f);

        // LinearClamp = bilinear sampling: the D-023 "no visible tiles" mandate.
        _spriteBatch!.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        _spriteBatch.Draw(_terrainTexture, Vector2.Zero, Color.White);
        _spriteBatch.End();

        // Debug panel (the single T1.7 panel).
        _imgui!.BeforeLayout(gameTime);
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(12, 12), ImGuiCond.FirstUseEver);
        ImGui.Begin("debug", ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.Text($"seed: {_world.Seed}");
        long year = -4000 + _world.Clock.SimDays / 360; // presentation-only calendar
        ImGui.Text($"turn: {_world.Clock.Turn}   year: {year}");
        ImGui.Text($"camera: ({cam.CenterX:F0}, {cam.CenterY:F0})  zoom: {cam.Zoom:F2}x");
        ImGui.Text($"fps: {_fps:F0}");
        ImGui.End();
        _imgui.AfterLayout();

        base.Draw(gameTime);
    }
}
