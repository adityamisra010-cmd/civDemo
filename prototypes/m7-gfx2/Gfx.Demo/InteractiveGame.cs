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
/// Render A/B (render1): G toggles the G1 preset and the G2 switches; 1 sky, 2 aerial perspective, 3 far field,
/// 4 PBR, 5 IBL (none, sky, HDRI), 6 shadows (cascaded, single), 7 tonemapper (cycle), 8 bloom, 9 FXAA; the title
/// bar shows the active switches.
/// </summary>
public sealed class InteractiveGame : Game
{
    readonly IReadOnlyList<SceneRegistry.Entry> _scenes;
    readonly string? _initial;
    EffectLibrary _fx = null!; ForwardRenderer _renderer = null!;
    DemoScene? _scene; TacticalCamera _cam = new(Vector3.Zero);
    float _hour = 10f, _t; int _lastWheel;
    KeyboardState _prevKeys;
    RenderFeatures _g2 = new(); bool _g1Preset;

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
        _scene = _scenes[i].Create(); _scene.Load(GraphicsDevice, _fx);
        _renderer.Settings.Features = new RenderFeatures();
        _scene.Configure(_renderer.Settings);
        // stage render2: interactive frames move, so they use temporal anti-aliasing (key 9 cycles TAA, FXAA, none)
        _renderer.Settings.Features.Aa = AntiAliasing.Taa;
        _g2 = _renderer.Settings.Features.Clone();
        if (_g1Preset) _renderer.Settings.Features = RenderFeatures.G1();
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
        RenderSwitches(k);
        base.Update(gameTime);
    }

    void RenderSwitches(KeyboardState k)
    {
        bool Pressed(Keys key) => k.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);
        var f = _g2; bool changed = false;
        if (Pressed(Keys.G)) { _g1Preset = !_g1Preset; changed = true; }
        if (Pressed(Keys.D1)) { f.Sky = f.Sky == SkyMode.Analytic ? SkyMode.Legacy : SkyMode.Analytic; changed = true; }
        if (Pressed(Keys.D2)) { f.AerialPerspective = !f.AerialPerspective; changed = true; }
        if (Pressed(Keys.D3)) { f.FarField = !f.FarField; changed = true; }
        if (Pressed(Keys.D4)) { f.Pbr = !f.Pbr; changed = true; }
        if (Pressed(Keys.D5)) { f.Ibl = f.Ibl switch { IblSource.None => IblSource.Sky, IblSource.Sky => IblSource.Hdri, _ => IblSource.None }; changed = true; }
        if (Pressed(Keys.D6)) { f.ShadowTechnique = f.ShadowTechnique == ShadowTechnique.Cascaded ? ShadowTechnique.Single : ShadowTechnique.Cascaded; changed = true; }
        if (Pressed(Keys.D7)) { f.ToneMapper = f.ToneMapper switch { ToneMapper.PbrNeutral => ToneMapper.AcesHill, ToneMapper.AcesHill => ToneMapper.AgX, ToneMapper.AgX => ToneMapper.AcesFit, _ => ToneMapper.PbrNeutral }; changed = true; }
        if (Pressed(Keys.D8)) { f.Bloom = !f.Bloom; changed = true; }
        if (Pressed(Keys.D9)) { f.Aa = f.Aa switch { AntiAliasing.Taa => AntiAliasing.Fxaa, AntiAliasing.Fxaa => AntiAliasing.None, _ => AntiAliasing.Taa }; changed = true; }
        _prevKeys = k;
        if (!changed) return;
        _renderer.Settings.Features = _g1Preset ? RenderFeatures.G1() : _g2.Clone();
        Window.Title = "M7 Gfx prototype — " + (_g1Preset ? "G1 preset" : _g2.Describe());
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_scene != null) _renderer.Render(_scene, _cam.Pose, SunModel.At(_hour), null, _t);
        base.Draw(gameTime);
    }

    protected override void UnloadContent() { _scene?.Dispose(); _renderer.Dispose(); _fx.Dispose(); }
}
