using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Rendering;

/// <summary>
/// Per-frame bone palette: one texture row per drawn character, 3 RGBA32F texels per bone holding the columns of
/// the bone's skin matrix (world folded in), read in the vertex shaders by vertex texture fetch (slot 1).
/// </summary>
public sealed class BonePalette : IDisposable
{
    readonly GraphicsDevice _gd;
    Texture2D _tex;
    Vector4[] _data;
    public int Bones { get; }
    public int Capacity { get; private set; }
    public int Used { get; private set; }
    public Texture2D Texture => _tex;
    public Vector4 SizeParam => new(_tex.Width, _tex.Height, 1f / _tex.Width, 1f / _tex.Height);

    public BonePalette(GraphicsDevice gd, int bones, int capacity = 16)
    {
        _gd = gd; Bones = bones; Capacity = Math.Max(1, capacity);
        _tex = new Texture2D(gd, bones * 3, Capacity, false, SurfaceFormat.Vector4);
        _data = new Vector4[bones * 3 * Capacity];
    }

    public void Reset() => Used = 0;

    public int Add(Matrix[] skin)
    {
        if (Used == Capacity) Grow(Capacity * 2);
        int row = Used++;
        int o = row * Bones * 3;
        int count = Math.Min(Bones, skin.Length);
        for (int j = count; j < Bones; j++) { _data[o + 3 * j] = _data[o + 3 * j + 1] = _data[o + 3 * j + 2] = Vector4.Zero; }
        for (int j = 0; j < count; j++)
        {
            ref readonly var m = ref skin[j];
            _data[o + 3 * j] = new Vector4(m.M11, m.M21, m.M31, m.M41);
            _data[o + 3 * j + 1] = new Vector4(m.M12, m.M22, m.M32, m.M42);
            _data[o + 3 * j + 2] = new Vector4(m.M13, m.M23, m.M33, m.M43);
        }
        return row;
    }

    void Grow(int cap)
    {
        var d = new Vector4[Bones * 3 * cap];
        Array.Copy(_data, d, _data.Length);
        _data = d; Capacity = cap;
        _tex.Dispose();
        _tex = new Texture2D(_gd, Bones * 3, Capacity, false, SurfaceFormat.Vector4);
    }

    public void Upload()
    {
        if (Used == 0) return;
        _tex.SetData(0, new Rectangle(0, 0, Bones * 3, Used), _data, 0, Bones * 3 * Used);
    }

    public void Dispose() => _tex.Dispose();
}
