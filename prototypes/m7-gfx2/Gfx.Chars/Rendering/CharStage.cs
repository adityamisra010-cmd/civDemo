using Gfx.Engine.Assets;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Rendering;

/// <summary>
/// A set of drawn characters for one scene: loads baked identities and outfits from <c>$G2_ASSETS/baked/chars</c>
/// (tools/charbake), owns the renderer and the shared texture cache, and prepares the bone palette and morph streams
/// once per scene update (not once per pass). PRESENTATION ONLY: it reads baked files and the instances' cosmetic
/// state; nothing here is written back to any simulation.
/// </summary>
public sealed class CharStage : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly Dictionary<string, CharAsset> _assets = new(StringComparer.Ordinal);
    int _version, _prepared = -1;

    public CharRenderer Renderer { get; }
    public TextureCache Textures { get; }
    public List<CharInstance> Instances { get; } = new();
    public string Folder { get; }

    public CharStage(GraphicsDevice gd, string? folder = null)
    {
        _gd = gd;
        Folder = folder ?? AssetRoot.Baked("chars");
        Renderer = new CharRenderer(gd);
        Textures = new TextureCache(gd);
    }

    /// <summary>True when the bake exists (scripts/bake-chars.sh has run).</summary>
    public static bool BakeExists(string? folder = null) => File.Exists(Path.Combine(folder ?? AssetRoot.Baked("chars"), "avatar.g2ch"));

    /// <summary>A baked identity or outfit file by id (e.g. "avatar", "avatar__era_medieval"), loaded once.</summary>
    public CharAsset Asset(string id)
    {
        if (!_assets.TryGetValue(id, out var a))
        {
            string path = Path.Combine(Folder, id + ".g2ch");
            if (!File.Exists(path)) throw new FileNotFoundException($"character bake missing: {path} (run scripts/bake-chars.sh)");
            a = CharAsset.Load(_gd, path, Textures.Get);
            _assets[id] = a;
        }
        return a;
    }

    /// <summary>A new instance of an identity wearing one outfit (every capture dresses its characters).</summary>
    public CharInstance Spawn(string identity, string outfit)
    {
        var inst = new CharInstance(Asset(identity));
        inst.Wear(_gd, Asset(identity + "__" + outfit));
        Instances.Add(inst);
        Invalidate();
        return inst;
    }

    /// <summary>Marks the poses and expressions as changed (call after every scene update).</summary>
    public void Invalidate() => _version++;

    void Prepare()
    {
        if (_prepared == _version) return;
        Renderer.Prepare(Instances);
        _prepared = _version;
    }

    public void DrawShadows(RenderContext rc) { Prepare(); Renderer.DrawShadows(rc, Instances); }
    public void DrawOpaque(RenderContext rc) { Prepare(); Renderer.DrawOpaque(rc, Instances); }

    public void Dispose()
    {
        foreach (var i in Instances) i.Dispose();
        Instances.Clear();
        foreach (var a in _assets.Values) a.Dispose();
        _assets.Clear();
        Renderer.Dispose();
        Textures.Dispose();
    }
}
