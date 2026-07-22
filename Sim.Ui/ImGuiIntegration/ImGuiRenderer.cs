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

    public ImGuiRenderer(Game game)
    {
        _game = game;
        _device = game.GraphicsDevice;
        ImGui.CreateContext();
        RebuildFontAtlas();
        game.Window.TextInput += (_, e) =>
        {
            if (e.Character != '\t') ImGui.GetIO().AddInputCharacter(e.Character);
        };
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
        {
            MouseState mouse = Mouse.GetState();
            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
            io.AddMouseWheelEvent(0f, (mouse.ScrollWheelValue - _lastScroll) / 120f);
            _lastScroll = mouse.ScrollWheelValue;

            KeyboardState keyboard = Keyboard.GetState();
            foreach ((Keys key, ImGuiKey imguiKey) in KeyMap)
                io.AddKeyEvent(imguiKey, keyboard.IsKeyDown(key));
            io.AddKeyEvent(ImGuiKey.ModCtrl,
                keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
            io.AddKeyEvent(ImGuiKey.ModShift,
                keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
            io.AddKeyEvent(ImGuiKey.ModAlt,
                keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
        }

        ImGui.NewFrame();
    }

    private int _lastScroll;

    private static readonly (Keys, ImGuiKey)[] KeyMap =
    [
        (Keys.Tab, ImGuiKey.Tab), (Keys.Left, ImGuiKey.LeftArrow), (Keys.Right, ImGuiKey.RightArrow),
        (Keys.Up, ImGuiKey.UpArrow), (Keys.Down, ImGuiKey.DownArrow), (Keys.PageUp, ImGuiKey.PageUp),
        (Keys.PageDown, ImGuiKey.PageDown), (Keys.Home, ImGuiKey.Home), (Keys.End, ImGuiKey.End),
        (Keys.Delete, ImGuiKey.Delete), (Keys.Back, ImGuiKey.Backspace), (Keys.Enter, ImGuiKey.Enter),
        (Keys.Escape, ImGuiKey.Escape), (Keys.Space, ImGuiKey.Space), (Keys.A, ImGuiKey.A),
        (Keys.C, ImGuiKey.C), (Keys.V, ImGuiKey.V), (Keys.X, ImGuiKey.X), (Keys.Y, ImGuiKey.Y),
        (Keys.Z, ImGuiKey.Z),
    ];

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
        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        int vtxOffset = 0, idxOffset = 0;
        for (int listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
        {
            ImDrawListPtr drawList = drawData.CmdLists[listIndex];
            for (int cmdIndex = 0; cmdIndex < drawList.CmdBuffer.Size; cmdIndex++)
            {
                ImDrawCmdPtr cmd = drawList.CmdBuffer[cmdIndex];
                if (cmd.ElemCount == 0) continue;
                if (!_boundTextures.TryGetValue(cmd.TextureId, out Texture2D? texture)) continue;

                _device.ScissorRectangle = new Rectangle(
                    (int)cmd.ClipRect.X, (int)cmd.ClipRect.Y,
                    (int)(cmd.ClipRect.Z - cmd.ClipRect.X), (int)(cmd.ClipRect.W - cmd.ClipRect.Y));
                _effect!.Texture = texture;

                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        baseVertex: vtxOffset + (int)cmd.VtxOffset,
                        startIndex: idxOffset + (int)cmd.IdxOffset,
                        primitiveCount: (int)cmd.ElemCount / 3);
                }
            }
            vtxOffset += drawList.VtxBuffer.Size;
            idxOffset += drawList.IdxBuffer.Size;
        }

        _device.Viewport = lastViewport;
        _device.ScissorRectangle = lastScissor;
        _device.BlendState = lastBlend;
        _device.DepthStencilState = lastDepth;
        _device.RasterizerState = lastRasterizer;
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
