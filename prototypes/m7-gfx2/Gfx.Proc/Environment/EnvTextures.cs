using Gfx.Engine.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment;

/// <summary>
/// The environment stage's baked textures (<c>scripts/bake-env.sh</c> → <c>$G2_ASSETS/baked/env</c>, R1TX container),
/// loaded on first use on the GL thread. A missing or unreadable file is not an error: the caller gets a neutral
/// stand-in (white albedo, flat normal) and <see cref="Failures"/> records why, so captures can say what was missing.
/// PRESENTATION ONLY.
/// </summary>
public sealed class EnvTextures : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly Dictionary<string, Texture2D> _loaded = new(StringComparer.Ordinal);
    readonly List<string> _failures = new();
    Texture2D? _white, _flat;

    public EnvTextures(GraphicsDevice gd) { _gd = gd; }

    public static string Dir => AssetRoot.Baked("env");
    public IReadOnlyList<string> Failures => _failures;
    public bool AllLoaded => _failures.Count == 0;

    /// <summary>The baked texture <paramref name="file"/> (e.g. <c>bark_oak_alb_rgh.r1tex</c>), or a stand-in.</summary>
    public Texture2D Get(string file, bool normalMap = false)
    {
        if (_loaded.TryGetValue(file, out var t)) return t;
        string path = Path.Combine(Dir, file);
        try
        {
            t = BakedTexture.Load(_gd, path);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _failures.Add($"{file}: {e.Message}");
            t = normalMap ? Flat() : White();
        }
        // texture creation mid-frame re-binds GL units behind MonoGame's cache: force every slot to re-bind
        for (int i = 0; i < 16; i++) _gd.Textures[i] = null;
        _loaded[file] = t;
        return t;
    }

    Texture2D White() => _white ??= Solid(255, 255, 255, 255);
    Texture2D Flat() => _flat ??= Solid(128, 128, 255, 255);

    Texture2D Solid(byte r, byte g, byte b, byte a)
    {
        var tex = new Texture2D(_gd, 1, 1, false, SurfaceFormat.Color);
        tex.SetData(new[] { new Microsoft.Xna.Framework.Color(r, g, b, a) });
        return tex;
    }

    public void Dispose()
    {
        foreach (var t in _loaded.Values) if (t != _white && t != _flat) t.Dispose();
        _loaded.Clear();
        _white?.Dispose(); _flat?.Dispose();
    }
}
