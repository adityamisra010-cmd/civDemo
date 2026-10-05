using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Sim.Ui.ImGuiIntegration;

/// <summary>
/// ImGui.NET ⇄ MonoGame renderer binding — the standard community renderer
/// (ImGuiNET.SampleProgram.XNA lineage), vendored per ADR-009: implement or
/// vendor was sanctioned, and vendoring ~250 reviewable lines beats a third
/// package. Rendering only; sits OUTSIDE the determinism surface (floats,
/// Dictionary iteration and wall-clock timing are all legal in Sim.Ui).
/// </summary>
public sealed class ImGuiRenderer
{
    private readonly Game _game;
    private readonly GraphicsDevice _device;
    private readonly Dictionary<IntPtr, Texture2D> _boundTextures = [];
    private int _nextTextureId = 1;

    /// <summary>
    /// Sampler for EVERY ImGui draw call (D-A1 gate round 2). This renderer
    /// never set one, so ImGui geometry sampled under whatever state the last
    /// SpriteBatch pass left on the device — LinearClamp from the settlement
    /// marker pass that runs immediately before DrawHud. Under clamp, uv &gt; 1
    /// re-samples the final texel column forever, so the panel furniture's
    /// tiled draws (parchment background uv = size/128, header rule u &gt; 1 on
    /// wide panels) showed exactly one period and streaked the edge column.
    /// Wrap matches the reference ImGui backends (imgui_impl_dx11 creates its
    /// sampler with D3D11_TEXTURE_ADDRESS_WRAP) and is safe for the font atlas
    /// and 9-slice, whose uv stay inside [0,1]. Public and static so headless
    /// tests can pin the address mode the composed furniture draw depends on.
    /// </summary>
    public static readonly SamplerState TextureSampler = SamplerState.LinearWrap;

    private BasicEffect? _effect;
    private readonly RasterizerState _rasterizer = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true,
    };

    private byte[] _vertexData = new byte[8192];
    private VertexBuffer? _vertexBuffer;
    private int _vertexBufferSize;
    private byte[] _indexData = new byte[2048];
    private IndexBuffer? _indexBuffer;
    private int _indexBufferSize;

    private static readonly VertexDeclaration ImGuiVertexDeclaration = new(
        ImGuiVertexSize,
        new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
        new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    private const int ImGuiVertexSize = 20; // Vector2 pos + Vector2 uv + uint color

    /// <param name="ownsContext">False when the caller already created the
    /// ImGui context (the art substrate does, so fonts can join the atlas
    /// BEFORE this renderer uploads it).</param>
    public ImGuiRenderer(Game game, bool ownsContext = true)
    {
        _game = game;
        _device = game.GraphicsDevice;
        PrepareContext(ownsContext);
        RebuildFontAtlas();
        game.Window.TextInput += (_, e) =>
        {
            if (e.Character != '\t') ImGui.GetIO().AddInputCharacter(e.Character);
        };
    }

    /// <summary>The device-free half of construction: creates the ImGui context when the renderer owns it,
    /// then declares this renderer's capabilities on the CURRENT context. M5 hardening H1 (the Research-screen
    /// crash): the declaration includes <c>RendererHasVtxOffset</c>, so ImGui may let a draw list grow past
    /// 65,535 vertices by rebasing its 16-bit indices instead of asserting; RenderDrawData applies the offset
    /// (ImGuiDrawData.Plan). Factored out so the headless harness runs the SAME setup the windowed game does and
    /// tests can pin it without a GPU (ImGuiRendererSetupTests).</summary>
    public static void PrepareContext(bool ownsContext)
    {
        if (ownsContext) ImGui.CreateContext();
        ImGuiDrawData.Configure(ImGui.GetIO());
    }

    /// <summary>Uploads the ImGui font atlas as a Texture2D and binds it.</summary>
    public unsafe void RebuildFontAtlas()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int _);
        var pixels = new byte[width * height * 4];
        new ReadOnlySpan<byte>(pixelData, pixels.Length).CopyTo(pixels);

        var fontTexture = new Texture2D(_device, width, height, false, SurfaceFormat.Color);
        fontTexture.SetData(pixels);
        io.Fonts.SetTexID(BindTexture(fontTexture));
        io.Fonts.ClearTexData();
    }

    public IntPtr BindTexture(Texture2D texture)
    {
        var id = new IntPtr(_nextTextureId++);
        _boundTextures[id] = texture;
        return id;
    }

    /// <summary>Starts an ImGui frame: pumps input state and DeltaTime.</summary>
    public void BeforeLayout(GameTime gameTime)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        io.DisplaySize = new System.Numerics.Vector2(
            _device.PresentationParameters.BackBufferWidth,
            _device.PresentationParameters.BackBufferHeight);
        io.DisplayFramebufferScale = System.Numerics.Vector2.One;

        if (_game.IsActive)
            ImGuiInput.Feed(io, Mouse.GetState(), Keyboard.GetState(), ref _lastScroll);

        ImGui.NewFrame();
    }

    private int _lastScroll;

    /// <summary>Renders the ImGui draw data accumulated since BeforeLayout.</summary>
    public void AfterLayout()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0) return;

        // Preserve device state we stomp on.
        Viewport lastViewport = _device.Viewport;
        Rectangle lastScissor = _device.ScissorRectangle;
        BlendState lastBlend = _device.BlendState;
        DepthStencilState lastDepth = _device.DepthStencilState;
        RasterizerState lastRasterizer = _device.RasterizerState;
        SamplerState lastSampler = _device.SamplerStates[0];

        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);
        UpdateBuffers(drawData);

        _effect ??= new BasicEffect(_device)
        {
            World = Matrix.Identity,
            View = Matrix.Identity,
            TextureEnabled = true,
            VertexColorEnabled = true,
        };
        _effect.Projection = Matrix.CreateOrthographicOffCenter(
            0f, ImGui.GetIO().DisplaySize.X, ImGui.GetIO().DisplaySize.Y, 0f, -1f, 1f);

        _device.BlendState = BlendState.NonPremultiplied;
        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = _rasterizer;
        _device.SamplerStates[0] = TextureSampler;
        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        // One draw call per ImGui command, base vertex = list start + cmd.VtxOffset (ImGuiDrawData.Plan — the
        // same plan the headless harness validates index by index).
        foreach (ImGuiDrawCall call in ImGuiDrawData.Plan(drawData))
        {
            if (!_boundTextures.TryGetValue(call.TextureId, out Texture2D? texture)) continue;

            _device.ScissorRectangle = new Rectangle(
                (int)call.ClipX0, (int)call.ClipY0,
                (int)(call.ClipX1 - call.ClipX0), (int)(call.ClipY1 - call.ClipY0));
            _effect!.Texture = texture;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    baseVertex: call.BaseVertex,
                    startIndex: call.StartIndex,
                    primitiveCount: call.PrimitiveCount);
            }
        }

        _device.Viewport = lastViewport;
        _device.ScissorRectangle = lastScissor;
        _device.BlendState = lastBlend;
        _device.DepthStencilState = lastDepth;
        _device.RasterizerState = lastRasterizer;
        _device.SamplerStates[0] = lastSampler;
    }

    private unsafe void UpdateBuffers(ImDrawDataPtr drawData)
    {
        if (drawData.TotalVtxCount > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();
            _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5);
            _vertexBuffer = new VertexBuffer(
                _device, ImGuiVertexDeclaration, _vertexBufferSize, BufferUsage.None);
            _vertexData = new byte[_vertexBufferSize * ImGuiVertexSize];
        }
        if (drawData.TotalIdxCount > _indexBufferSize)
        {
            _indexBuffer?.Dispose();
            _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5);
            _indexBuffer = new IndexBuffer(
                _device, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.None);
            _indexData = new byte[_indexBufferSize * sizeof(ushort)];
        }

        int vtxBytes = 0, idxBytes = 0;
        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr drawList = drawData.CmdLists[i];
            int listVtxBytes = drawList.VtxBuffer.Size * ImGuiVertexSize;
            int listIdxBytes = drawList.IdxBuffer.Size * sizeof(ushort);
            new ReadOnlySpan<byte>((void*)drawList.VtxBuffer.Data, listVtxBytes)
                .CopyTo(_vertexData.AsSpan(vtxBytes));
            new ReadOnlySpan<byte>((void*)drawList.IdxBuffer.Data, listIdxBytes)
                .CopyTo(_indexData.AsSpan(idxBytes));
            vtxBytes += listVtxBytes;
            idxBytes += listIdxBytes;
        }
        _vertexBuffer!.SetData(_vertexData, 0, vtxBytes);
        _indexBuffer!.SetData(_indexData, 0, idxBytes);
    }
}
