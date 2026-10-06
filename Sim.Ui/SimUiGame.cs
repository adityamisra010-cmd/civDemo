using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.Art;
using Sim.Ui.ImGuiIntegration;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// The window (T1.7/T1.8, D-023): a THIN MonoGame host (M5 hardening H1). It owns what needs a graphics device —
/// the parchment terrain bake, the vector rivers, the grain overlay, the ImGui renderer and the art textures — and
/// hands everything else to <see cref="GameUi"/>: each Update polls the mouse and keyboard ONCE and passes them to
/// <see cref="GameUi.Update"/>; each Draw paints the map and then <see cref="GameUi.Draw"/> between the ImGui
/// renderer's BeforeLayout and AfterLayout. The headless playability harness drives the same GameUi without this
/// class, so the gate exercises the live UI. The UI is a VIEW + ORDER SOURCE, single-threaded (m1 spec §3); the
/// sim never reads UI state; nothing references Sim.Ui.
/// </summary>
public sealed class SimUiGame : Game
{
    private readonly UiSession _session;
    private readonly string _sessionLogPath;
    private readonly bool _developer;
    private GameUi? _ui;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _terrainTexture;
    private float _terrainDrawScale = 1f;
    private ImGuiRenderer? _imgui;

    // --- art substrate (style-bible parchment atlas) ----------------------
    private readonly AssetLibrary _art = AssetLibrary.Load();
    // UR-2: the fibre overlay in two passes (FibreOverlay) — the map's remainder before the interface, the
    // interface's soft share after it.
    private Texture2D? _fibreRestTexture, _fibreSoftTexture;
    private Texture2D? _panelTexture, _headerRuleTexture, _buttonPlateTexture,
                       _annalsTexture, _compassTexture;
    private UiTheme.Fonts? _fonts;


    /// <summary>Multiply blend for the §4 grain overlay: dst × src, so a
    /// near-white grain darkens everything faintly — map AND UI alike.</summary>
    private static readonly BlendState MultiplyBlend = new()
    {
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.SourceAlpha,
    };


    private VertexBuffer? _riverVertices;
    private BasicEffect? _worldEffect;
    private static readonly RasterizerState WorldRasterizer = new()
    {
        CullMode = CullMode.None,
        MultiSampleAntiAlias = true, // ADR-009: MSAA is the anti-aliasing choice
    };


    private double _fps;
    private MouseState _mouse;
    private KeyboardState _keyboard;

    public SimUiGame(UiSession session, string sessionLogPath, bool developer = false, double userScale = UiScale.DefaultUser)
    {
        _developer = developer;
        _userScale = UiScale.NearestStep(userScale);
        _session = session;
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
        // F3: a floor under the user-resizable window (PanelLayout.MinWindowWidth/Height) — below it the command
        // bar's fixed row ran past the right edge. MonoGame has no native minimum, so a resize below the floor is
        // snapped back to it (the playability gate plays the game at exactly this size).
        Window.ClientSizeChanged += (_, _) => EnforceMinimumWindowSize();
        Window.Title = BuildInfo.Describe(); // build identity: sha + date (T1.10)
    }

    private bool _enforcingMinimum;

    private void EnforceMinimumWindowSize()
    {
        if (_enforcingMinimum) return;
        Rectangle b = Window.ClientBounds;
        if (b.Width <= 0 || b.Height <= 0) return; // minimised
        int w = Math.Max(b.Width, PanelLayout.MinWindowWidth), h = Math.Max(b.Height, PanelLayout.MinWindowHeight);
        if (w == b.Width && h == b.Height) return;
        _enforcingMinimum = true;
        try
        {
            _graphics.PreferredBackBufferWidth = w;
            _graphics.PreferredBackBufferHeight = h;
            _graphics.ApplyChanges();
        }
        finally { _enforcingMinimum = false; }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Fonts must join the atlas BEFORE the ImGui renderer uploads it (§3).
        ImGui.CreateContext();
        // T3.9a-b item 4: window state is session-scoped and in-memory ONLY — a stale imgui.ini from an older
        // build would override the PanelLayout defaults, so the ini is disabled.
        unsafe { ImGui.GetIO().NativePtr->IniFilename = null; }
        // UR-1: the atlas is rasterised at the UI scale of the opening window (and rebuilt when it changes).
        _uiScale = UiScale.Effective(UiScale.Auto(_graphics.PreferredBackBufferHeight), _userScale);
        _fonts = UiTheme.LoadFonts(_art.Root, _uiScale);
        _imgui = new ImGuiRenderer(this, ownsContext: false);   // declares RendererHasVtxOffset (H1)
        _worldEffect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        WorldState world = _session.World;

        // THE PARCHMENT BAKE (§4 items 1–4): terrain wash tiles splatted by the worldgen fields onto the paper,
        // with inked coasts — one supersampled texture, baked once, drawn with bilinear filtering.
        ParchmentBaker.Result bake = ParchmentBaker.Bake(world.Terrain!, _art, world.Seed);
        _terrainTexture = new Texture2D(GraphicsDevice, bake.Size, bake.Size, false, SurfaceFormat.Color);
        _terrainTexture.SetData(bake.Rgba);
        // The atlas is SUPERSAMPLED (Size = worldSize × ss): it is drawn scaled to span the WORLD (the
        // coastal-flooding defect; pinned headless by CoastlineRenderTests via DisplayedTexel).
        _terrainDrawScale = (float)bake.WorldDrawScale;
        string bakeNote = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"map {bake.Size}² {bake.MegabytesResident:F0} MB baked in {bake.BakeMilliseconds:F0} ms");

        ArtImage fibre = _art.Get("parchment/grain");
        _fibreRestTexture = UploadArt(FibreOverlay.Rest(fibre));
        _fibreSoftTexture = UploadArt(FibreOverlay.Soft(fibre));
        _panelTexture = UploadArt(_art.Get("ui/panel"));
        // D-A1: the header rule is PROCEDURAL (HeaderRuleBaker), drawn with an instrument, in code.
        _headerRuleTexture = UploadArt(Art.HeaderRuleBaker.Bake());
        _buttonPlateTexture = UploadArt(_art.Get("ui/button-plate"));
        _annalsTexture = UploadArt(_art.Get("ui/annals-bg"));
        _compassTexture = UploadArt(_art.Get("ui/compass-rose"));
        _imgui.BindTexture(_panelTexture);
        _imgui.BindTexture(_headerRuleTexture);
        _imgui.BindTexture(_buttonPlateTexture);
        IntPtr annalsId = _imgui.BindTexture(_annalsTexture);
        IntPtr compassId = _imgui.BindTexture(_compassTexture);

        RebuildRiverBuffer(1.0);

        Rectangle v = Viewport();
        _ui = new GameUi(_session, _sessionLogPath, _developer, _art, _fonts, new UiTextureIds(annalsId, compassId), v.Width, v.Height,
            _userScale)
        {
            BakeNote = bakeNote,
        };
    }

    private double _uiScale = 1.0;
    private readonly double _userScale;

    /// <summary>UR-1: when the UI asks for another scale (the window crossed a scale step, or the player pressed
    /// Ctrl+= / Ctrl+- / Ctrl+0), the atlas is rebuilt at the new sizes between frames and handed to the UI.</summary>
    private void FollowUiScale()
    {
        if (_ui is not { } ui || _imgui is null) return;
        double wanted = ui.WantedScale;
        if (Math.Abs(wanted - _uiScale) < 1e-9) return;
        _uiScale = wanted;
        ImGui.GetIO().Fonts.Clear();
        _fonts = UiTheme.LoadFonts(_art.Root, _uiScale);
        _imgui.RebuildFontAtlas();
        ui.SetFonts(_fonts);
    }

    // D-A3: rivers hold a CLAMPED screen width (see RiverMesh.ScreenWidthForRank),
    // so the world-space strip is rebuilt when zoom changes — zoom moves on
    // wheel events only, never per frame. Measured rebuild cost on the
    // canonical 1024² terrain: see the D-A3 commit message.
    private double _riverBuiltZoom;

    private void RebuildRiverBuffer(double zoom)
    {
        _riverVertices?.Dispose();
        _riverVertices = MakeBuffer(RiverMeshToLine(RiverMesh.Build(_session.World.Terrain!, zoom)),
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

    /// <summary>Rebuilds the cached HUD snapshot for the current selection, the action surface and the

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        // A final save, the telemetry flushed past the OS buffers, then the forensic close record (P0/P1).
        _ui?.SaveOnExit();
        base.OnExiting(sender, args);
    }

    private Rectangle Viewport() => GraphicsDevice.Viewport.Bounds;

    protected override void Update(GameTime gameTime)
    {
        _mouse = Mouse.GetState();
        _keyboard = Keyboard.GetState();
        if (_ui is { } ui)
        {
            Rectangle v = Viewport();
            ui.SetViewport(v.Width, v.Height);
            ui.Update(_mouse, _keyboard, gameTime.ElapsedGameTime.TotalSeconds, IsActive);
            FollowUiScale();
            if (ui.ExitRequested) Exit();
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        double dt = gameTime.ElapsedGameTime.TotalSeconds;
        if (dt > 0) _fps = _fps * 0.95 + (1.0 / dt) * 0.05;

        GraphicsDevice.Clear(new Color(
            ParchmentPalette.PaperShade.R, ParchmentPalette.PaperShade.G, ParchmentPalette.PaperShade.B));
        if (_ui is not { } ui) { base.Draw(gameTime); return; }
        Rectangle viewport = Viewport();
        Camera cam = ui.Camera;
        if (cam.Zoom != _riverBuiltZoom) RebuildRiverBuffer(cam.Zoom);   // D-A3

        var transform =
            Matrix.CreateTranslation((float)-cam.CenterX, (float)-cam.CenterY, 0f)
            * Matrix.CreateScale((float)cam.Zoom)
            * Matrix.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f);

        _spriteBatch!.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        _spriteBatch.Draw(_terrainTexture, Vector2.Zero, null, Color.White,
            0f, Vector2.Zero, _terrainDrawScale, SpriteEffects.None, 0f);
        _spriteBatch.End();

        // World-space vector layers. Layer ownership (Sim.Ui/World/MapLayers.cs): this GPU pass owns ONLY the
        // terrain bake (above) and the vector rivers; everything else on the map is the WorldLens's (GameUi).
        _worldEffect!.World = transform;
        _worldEffect.Projection = Matrix.CreateOrthographicOffCenter(
            0f, viewport.Width, viewport.Height, 0f, -1f, 1f);
        GraphicsDevice.RasterizerState = WorldRasterizer;
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        DrawWorldBuffer(_riverVertices);
        DrawFibreOverlay(_fibreRestTexture);   // UR-2 pass 1: the map's remainder of the fibre (FibreOverlay.Rest)

        // The UI: one ImGui frame — GameUi draws it between the renderer's BeforeLayout and AfterLayout.
        ui.Fps = _fps;
        ui.PrepareFrame();
        _imgui!.BeforeLayout(gameTime);
        ui.Draw();
        _imgui.AfterLayout();

        DrawFibreOverlay(_fibreSoftTexture);   // §4 item 2 (amended 2026-10-06): over EVERYTHING, the interface's soft share
        base.Draw(gameTime);
    }


    /// <summary>The fibre/age overlay (style-bible §4 item 2, amended 2026-10-06 by UR-2): one screen-filling quad of
    /// a tiling texture MULTIPLIED over the frame, so the whole window reads as one sheet of paper rather than a map
    /// with widgets floating above it. Drawn twice per frame (<see cref="FibreOverlay"/>): the map's remainder before
    /// the interface, the soft share over everything after it — the map keeps the full fibre, the words get a calm
    /// ground.</summary>
    private void DrawFibreOverlay(Texture2D? texture)
    {
        if (texture is null) return;
        Rectangle viewport = Viewport();
        _spriteBatch!.Begin(samplerState: SamplerState.LinearWrap, blendState: MultiplyBlend);
        _spriteBatch.Draw(texture, viewport,
            new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);
        _spriteBatch.End();
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
}