using System.Text.Json.Nodes;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Gear.Items;
using Gfx.Gear.Maps;
using Gfx.Gear.Rig;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Gear.Runtime;

/// <summary>GPU textures of generated map sets, uploaded once per key and disposed with the cache.</summary>
public sealed class KitTextureCache(GraphicsDevice gd) : IDisposable
{
    readonly Dictionary<string, KitTextures> _tex = new(StringComparer.Ordinal);
    /// <summary>CPU time spent building mips and uploading (ms), and texels uploaded (level 0).</summary>
    public double UploadMs { get; private set; }
    public long Texels { get; private set; }

    public KitTextures Get(string key, MapSet m)
    {
        if (_tex.TryGetValue(key, out var t)) return t;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        t = KitGpu.Upload(gd, m);
        UploadMs += sw.Elapsed.TotalMilliseconds; Texels += (long)m.Width * m.Height;
        _tex[key] = t;
        return t;
    }

    public void Dispose() { foreach (var t in _tex.Values) t.Dispose(); _tex.Clear(); }
}

/// <summary>
/// Turns generated items into character-file assets the stage-chars renderer draws: props (an item with its own one-bone
/// skeleton, placed by a world transform each frame: weapons in hand, shields, dropped weapons, items on display) and
/// worn kit (items fixed to a socket of an identity, written in bind space and bound to the socket's bone, plus the
/// hair views that keep hair inside a helmet from showing). PRESENTATION ONLY.
/// </summary>
public static class KitAssets
{
    static JsonObject MaterialJson(KitPart p)
    {
        if (p.Material == "hair") return new JsonObject { ["kind"] = "hair", ["colour"] = p.HairColour ?? "#5A1A14" };
        var o = new JsonObject { ["kind"] = "kit", ["maps"] = p.Maps, ["normal"] = (double)p.NormalStrength, ["sheen"] = (double)p.Sheen };
        if (p.AlphaTest) o["alpha"] = 1.0;
        return o;
    }

    static void AddTextures(CharAsset asset, KitItem item, KitTextureCache cache)
    {
        foreach (var (k, m) in item.Maps.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var t = cache.Get(item.Id + "/" + k, m);
            asset.Textures[k + ":base"] = t.Base; asset.Textures[k + ":normal"] = t.Normal; asset.Textures[k + ":orm"] = t.Orm;
        }
    }

    /// <summary>A prop asset: the item's parts in item space, bound to one root bone.</summary>
    public static CharAsset Prop(GraphicsDevice gd, KitItem item, KitTextureCache cache, Texture2D? hairAtlas = null)
    {
        var f = new CharacterFile();
        f.Info["id"] = "prop:" + item.Id;
        f.Info["kind"] = item.Kind;
        f.Bones.Add(new BoneDef("root", -1, Vector3.Zero, Vector3.UnitY));
        var mats = new JsonObject();
        foreach (var p in item.Parts)
        {
            f.Meshes.Add(KitGpu.ToMeshDef(p.Mesh, p.Name, p.Material, 0));
            mats[p.Name] = MaterialJson(p);
        }
        f.Info["materials"] = mats;
        var asset = new CharAsset(gd, f, "");
        AddTextures(asset, item, cache);
        if (hairAtlas != null) asset.Textures["hair"] = hairAtlas;
        return asset;
    }

    /// <summary>A prop instance of an item (culling sphere sized to the item).</summary>
    public static CharInstance PropInstance(CharAsset prop, KitItem item)
    {
        var inst = new CharInstance(prop) { BoundsRadius = item.Radius };
        return inst;
    }

    /// <summary>Identity hair meshes a helmet can hide (scalp shells, long hair, tails, beards).</summary>
    public static bool IsHair(string mesh) => mesh.StartsWith("hair", StringComparison.Ordinal) || mesh.StartsWith("ponytail", StringComparison.Ordinal)
        || mesh.StartsWith("beard", StringComparison.Ordinal);

    /// <summary>
    /// Worn kit: each item is placed by aligning its frame with a socket of the identity (bind space) and bound rigidly to
    /// the socket's bone. A covering test (a helmet's inner volume, in item space) trims the identity's hair: a hair
    /// triangle whose centroid lies inside the helmet is left out of the view (keyed by <paramref name="id"/>).
    /// </summary>
    public static CharAsset Worn(GraphicsDevice gd, CharAsset identity, string id, IEnumerable<(KitItem item, string frame, KitSocket socket)> items,
        KitTextureCache cache, Func<KitItem, Vector3, bool>? covers = null)
    {
        var f = new CharacterFile();
        f.Info["id"] = id;
        var mats = new JsonObject();
        var list = items.ToList();
        foreach (var (item, frame, socket) in list)
        {
            var toBind = Matrix.Invert(item.Frame(frame)) * socket.Bind;
            foreach (var p in item.Parts)
            {
                var mesh = new Geometry.KitMesh(p.Name);
                mesh.Append(p.Mesh, toBind);
                string name = item.Id + ":" + p.Name;
                f.Meshes.Add(KitGpu.ToMeshDef(mesh, name, p.Material, socket.Bone));
                mats[name] = MaterialJson(p);
            }
        }
        f.Info["materials"] = mats;
        // hair views: hide identity hair inside any covering item
        if (covers != null)
            foreach (var hm in identity.File.Meshes)
                if (IsHair(hm.Name)) f.Views.Add(new ViewDef(hm.Name, id, HairView(hm, list, covers)));
        var asset = new CharAsset(gd, f, "");
        foreach (var (item, _, _) in list)
            foreach (var (k, m) in item.Maps.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var t = cache.Get(item.Id + "/" + k, m);
                // keyed by the part names' map key, prefixed by the item (several items may share a map key name)
                asset.Textures[item.Id + ":" + k + ":base"] = t.Base; asset.Textures[item.Id + ":" + k + ":normal"] = t.Normal; asset.Textures[item.Id + ":" + k + ":orm"] = t.Orm;
            }
        // material map keys point at the prefixed textures
        foreach (var (item, _, _) in list)
            foreach (var p in item.Parts)
                if (p.Material == "kit" && mats[item.Id + ":" + p.Name] is JsonObject mo) mo["maps"] = item.Id + ":" + p.Maps;
        return asset;
    }

    /// <summary>The triangles (index triples) of a hair mesh whose centroid no covering item contains.</summary>
    public static uint[] HairView(MeshDef hm, IReadOnlyList<(KitItem item, string frame, KitSocket socket)> items, Func<KitItem, Vector3, bool> covers)
    {
        var toItem = items.Select(x => Matrix.Invert(x.socket.Bind) * x.item.Frame(x.frame)).ToArray();
        var keep = new List<uint>();
        for (int t = 0; t < hm.Indices.Length; t += 3)
        {
            var c = (hm.Positions[hm.Indices[t]] + hm.Positions[hm.Indices[t + 1]] + hm.Positions[hm.Indices[t + 2]]) / 3f;
            bool hidden = false;
            for (int k = 0; k < items.Count && !hidden; k++) hidden = covers(items[k].item, Vector3.Transform(c, toItem[k]));
            if (!hidden) { keep.Add(hm.Indices[t]); keep.Add(hm.Indices[t + 1]); keep.Add(hm.Indices[t + 2]); }
        }
        return keep.ToArray();
    }

    /// <summary>
    /// A prop made of baked stage-chars outfit parts (for BEFORE shots: the chars bake's own shield on display), moved
    /// from bind space into prop space by <paramref name="toProp"/>; the parts keep their stage-chars materials and
    /// textures, so they render exactly as on the character.
    /// </summary>
    public static CharAsset PropFromBaked(GraphicsDevice gd, CharAsset outfit, IEnumerable<string> parts, Matrix toProp)
    {
        var f = new CharacterFile();
        f.Info["id"] = "prop:" + outfit.Id + ":baked";
        f.Bones.Add(new BoneDef("root", -1, Vector3.Zero, Vector3.UnitY));
        var mats = new JsonObject();
        var src = outfit.File.Info["materials"] as JsonObject;
        foreach (var name in parts)
        {
            var m = outfit.File.Mesh(name);
            if (m == null) continue;
            var km = new Geometry.KitMesh(name);
            for (int i = 0; i < m.VertexCount; i++)
            {
                int k = km.Add(m.Positions[i], m.Normals[i], m.Uvs[i], new Color(m.Colors[4 * i], m.Colors[4 * i + 1], m.Colors[4 * i + 2], m.Colors[4 * i + 3]));
                km.Tangents[k] = m.Tangents[i];
            }
            foreach (var x in m.Indices) km.Indices.Add((int)x);
            km.Transform(toProp);
            f.Meshes.Add(KitGpu.ToMeshDef(km, name, m.Material, 0));
            if (src?[name] is JsonNode mn) mats[name] = mn.DeepClone();
        }
        f.Info["materials"] = mats;
        var asset = new CharAsset(gd, f, "");
        foreach (var (k, t) in outfit.Textures) asset.Textures[k] = t;
        return asset;
    }

    /// <summary>Baked kit parts of a stage-chars outfit (hidden when the era kit dresses the character).</summary>
    public static readonly string[] BakedKitParts = { "helmet", "crest", "shield_face", "shield_back", "shield_rim", "spear_shaft", "spear_head", "sword", "sword_grip" };

    /// <summary>
    /// Hides a stage-chars outfit's baked helmet, shield and weapons and restores the identity's full hair (the outfit's
    /// hair views under its helmet are removed), so generated kit can be worn instead.
    /// </summary>
    public static void StripBakedKit(CharInstance inst)
    {
        foreach (var p in BakedKitParts) inst.HiddenParts.Add(p);
        foreach (var part in inst.Asset.Parts) if (IsHair(part.Name)) inst.PartViews.Remove(part.Name);
    }
}
