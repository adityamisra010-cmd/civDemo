using Gfx.Chars.Rendering;
using Gfx.Gear.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Gear.Runtime;

/// <summary>
/// The era kit inside a character stage: registers the kit material with the stage's renderer, owns the GPU textures
/// of generated map sets and the assets built from items, and adds props (items placed by a world transform) to the
/// stage's instances so they are drawn, shadowed and culled with the characters. PRESENTATION ONLY.
/// </summary>
public sealed class KitWorld : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly List<CharAsset> _assets = new();
    public CharStage Stage { get; }
    public KitTextureCache Cache { get; }
    /// <summary>The stage-chars hair atlas (horsehair crests use the hair-card shader).</summary>
    public Texture2D? HairAtlas { get; }

    public KitWorld(GraphicsDevice gd, CharStage stage)
    {
        _gd = gd; Stage = stage;
        Cache = new KitTextureCache(gd);
        KitMaterials.Register(stage.Renderer.Materials);
        string atlas = Path.Combine(stage.Folder, "tex", "hair_atlas.r1tx");
        if (File.Exists(atlas)) HairAtlas = stage.Textures.Get(atlas);
    }

    /// <summary>Adds an item as a prop instance (world transform set by the caller each frame).</summary>
    public CharInstance AddProp(KitItem item, Matrix world)
    {
        var a = KitAssets.Prop(_gd, item, Cache, HairAtlas);
        _assets.Add(a);
        var inst = KitAssets.PropInstance(a, item);
        inst.World = world;
        Stage.Instances.Add(inst);
        Stage.Invalidate();
        return inst;
    }

    /// <summary>Adds a prop from a prepared character file (a rigged item such as the bow) with the item's map sets.</summary>
    public CharInstance AddAsset(Gfx.Chars.Format.CharacterFile file, KitItem item, float radius)
    {
        var a = new CharAsset(_gd, file, "");
        foreach (var (k, m) in item.Maps.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var t = Cache.Get(item.Id + "/" + k, m);
            a.Textures[k + ":base"] = t.Base; a.Textures[k + ":normal"] = t.Normal; a.Textures[k + ":orm"] = t.Orm;
        }
        _assets.Add(a);
        var inst = new CharInstance(a) { BoundsRadius = radius };
        Stage.Instances.Add(inst);
        Stage.Invalidate();
        return inst;
    }

    /// <summary>Adds a prop made of baked stage-chars outfit parts (BEFORE shots).</summary>
    public CharInstance AddBakedProp(CharAsset outfit, IEnumerable<string> parts, Matrix toProp, Matrix world)
    {
        var a = KitAssets.PropFromBaked(_gd, outfit, parts, toProp);
        _assets.Add(a);
        var inst = new CharInstance(a) { World = world, BoundsRadius = 0.8f };
        Stage.Instances.Add(inst);
        Stage.Invalidate();
        return inst;
    }

    /// <summary>Dresses an identity instance in worn kit (items fixed to its sockets), trimming hair under covering items.</summary>
    public CharAsset Wear(CharInstance inst, string id, IEnumerable<(KitItem item, string frame, Rig.KitSocket socket)> items, Func<KitItem, Vector3, bool>? covers = null)
    {
        var a = KitAssets.Worn(_gd, inst.Asset, id, items, Cache, covers);
        _assets.Add(a);
        inst.Wear(_gd, a);
        Stage.Invalidate();
        return a;
    }

    public void Dispose()
    {
        foreach (var a in _assets) a.Dispose();
        _assets.Clear();
        Cache.Dispose();
    }
}
