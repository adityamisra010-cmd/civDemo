using Gfx.Engine.Cameras;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Gfx.Demo;

/// <summary>
/// Windowed explorer (cannot be exercised in the headless container — untested here). WASD pan, Q/E rotate,
/// mouse wheel zoom, F1..F12 switch scene, T advance time of day, Esc quit.
/// </summary>
public sealed class InteractiveGame : Game
{
    readonly IReadOnlyList<SceneRegistry.Entry> _scenes;
    readonly string? _initial;
    EffectLibrary _fx = null!; ForwardRenderer _renderer = null!;
    DemoScene? _scene; TacticalCamera _cam = new(Vector3.Zero);
    float _hour = 10f, _t; int _lastWheel;

    public InteractiveGame(IReadOnlyList<SceneRegistry.Entry> scenes, string? initial)
    {
        _scenes = scenes; _initial = initial;
        _ = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef, PreferredBackBufferWidth = 1600, PreferredBackBufferHeight = 900,
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8, SynchronizeWithVerticalRetrace = true,
        };
        IsMouseVisible = true; Window.AllowUserResizing = true; Window.Title = "M7 Gfx prototype";
    }

    protected override void LoadContent()
    {
        _fx = new EffectLibrary(GraphicsDevice, typeof(InteractiveGame).Assembly);
        _fx.WarmupAll();
        _renderer = new ForwardRenderer(GraphicsDevice, _fx);
        int start = 0;
        for (int i = 0; i < _scenes.Count; i++) if (_scenes[i].Name == _initial) start = i;
        if (_scenes.Count > 0) Switch(start);
    }

    void Switch(int i)
    {
        _scene?.Dispose();
        _scene = _scenes[i].Create(); _scene.Load(GraphicsDevice, _fx); _scene.Configure(_renderer.Settings);
        _cam = new TacticalCamera(_scene.Focus) { Bounds = new BoundingBox(new Vector3(-400), new Vector3(400)) };
        Window.Title = "M7 Gfx prototype — " + _scenes[i].Name;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds; _t += dt;
        var k = Keyboard.GetState(); var m = Mouse.GetState();
        if (k.IsKeyDown(Keys.Escape)) Exit();
        for (int i = 0; i < Math.Min(12, _scenes.Count); i++) if (k.IsKeyDown(Keys.F1 + i)) Switch(i);
        float speed = _cam.Distance * 1.2f * dt;
        _cam.Pan((k.IsKeyDown(Keys.D) ? speed : 0) - (k.IsKeyDown(Keys.A) ? speed : 0), (k.IsKeyDown(Keys.W) ? speed : 0) - (k.IsKeyDown(Keys.S) ? speed : 0));
        if (k.IsKeyDown(Keys.Q)) _cam.Rotate(1.5f * dt);
        if (k.IsKeyDown(Keys.E)) _cam.Rotate(-1.5f * dt);
        if (k.IsKeyDown(Keys.T)) _hour = (_hour + dt * 2f) % 24f;
        int wheel = m.ScrollWheelValue - _lastWheel; _lastWheel = m.ScrollWheelValue;
        if (wheel != 0) _cam.Zoom(MathF.Pow(0.999f, wheel));
        _cam.Update(dt);
        _scene?.Update(_t);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_scene != null) _renderer.Render(_scene, _cam.Pose, SunModel.At(_hour), null, _t);
        base.Draw(gameTime);
    }

    protected override void UnloadContent() { _scene?.Dispose(); _renderer.Dispose(); _fx.Dispose(); }
}
