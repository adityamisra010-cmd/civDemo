using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Engine.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Rendering;

/// <summary>One uploaded mesh part of a character file.</summary>
public sealed class GpuPart : IDisposable
{
    public required MeshDef Def { get; init; }
    public required VertexBuffer Vertices { get; init; }
    public required IndexBuffer Indices { get; init; }
    public IReadOnlyList<MorphDef> Morphs { get; init; } = Array.Empty<MorphDef>();
    public string Name => Def.Name;
    public string Material => Def.Material;
    public int TriangleCount => Def.TriangleCount;
    /// <summary>Alternative index buffers by view name (an outfit's visible subset).</summary>
    public Dictionary<string, (IndexBuffer ib, int tris)> Views { get; } = new(StringComparer.Ordinal);
    public void Dispose() { Vertices.Dispose(); Indices.Dispose(); foreach (var v in Views.Values) v.ib.Dispose(); }
}

/// <summary>
/// A baked character file uploaded to the GPU: parts, textures, skeleton and clips. Several instances (and several
/// outfits) share one asset; nothing here changes after load.
/// </summary>
public sealed class CharAsset : IDisposable
{
    public CharacterFile File { get; }
    public Skeleton Skeleton { get; }
    public List<GpuPart> Parts { get; } = new();
    public Dictionary<string, Texture2D> Textures { get; } = new(StringComparer.Ordinal);
    public VertexBuffer ZeroMorphs { get; }
    public string Id => File.Info["id"]?.GetValue<string>() ?? "?";
    public string BakedDir { get; }
    /// <summary>The identity's clip file (CMU retargets, PROTOTYPE-ONLY, kept out of the identity file), or the identity
    /// file itself when it carries its own clips.</summary>
    public CharacterFile ClipFile { get; }

    public CharAsset(GraphicsDevice gd, CharacterFile file, string bakedDir, Func<string, Texture2D>? textureCache = null)
    {
        File = file; BakedDir = bakedDir;
        string? clips = file.Info["clips"]?.GetValue<string>();
        ClipFile = clips != null && System.IO.File.Exists(Path.Combine(bakedDir, clips)) ? CharacterFile.Load(Path.Combine(bakedDir, clips)) : file;
        Skeleton = file.Bones.Count > 0 ? new Skeleton(file.Bones) : new Skeleton(Array.Empty<BoneDef>());
        int maxV = 1;
        foreach (var m in file.Meshes)
        {
            var verts = new CharVertex[m.VertexCount];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = new CharVertex
                {
                    Position = m.Positions[i], Normal = m.Normals[i], Tangent = m.Tangents[i], Uv = m.Uvs[i],
                    Joints = new Microsoft.Xna.Framework.Graphics.PackedVector.Byte4(m.Joints[4 * i], m.Joints[4 * i + 1], m.Joints[4 * i + 2], m.Joints[4 * i + 3]),
                    Weights = new Color(m.Weights[4 * i], m.Weights[4 * i + 1], m.Weights[4 * i + 2], m.Weights[4 * i + 3]),
                    Color = new Color(m.Colors[4 * i], m.Colors[4 * i + 1], m.Colors[4 * i + 2], m.Colors[4 * i + 3]),
                };
            }
            var vb = new VertexBuffer(gd, CharVertex.Decl, Math.Max(1, verts.Length), BufferUsage.WriteOnly);
            if (verts.Length > 0) vb.SetData(verts);
            var ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, Math.Max(3, m.Indices.Length), BufferUsage.WriteOnly);
            if (m.Indices.Length > 0) ib.SetData(m.Indices.Select(x => (int)x).ToArray());
            var part = new GpuPart { Def = m, Vertices = vb, Indices = ib, Morphs = file.Morphs.TryGetValue(m.Name, out var ms) ? ms : Array.Empty<MorphDef>() };
            Parts.Add(part);
            maxV = Math.Max(maxV, m.VertexCount);
        }
        ZeroMorphs = new VertexBuffer(gd, MorphVertex.Decl, maxV, BufferUsage.WriteOnly);
        ZeroMorphs.SetData(new MorphVertex[maxV]);
        if (file.Info["textures"] is System.Text.Json.Nodes.JsonObject tx)
            foreach (var (key, val) in tx.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string rel = val!.GetValue<string>();
                string path = Path.Combine(bakedDir, rel);
                Textures[key] = textureCache != null ? textureCache(path) : BakedTexture.Load(gd, path);
            }
    }

    public static CharAsset Load(GraphicsDevice gd, string path, Func<string, Texture2D>? textureCache = null)
        => new(gd, CharacterFile.Load(path), Path.GetDirectoryName(Path.GetFullPath(path))!, textureCache);

    public GpuPart? Part(string name) => Parts.Find(p => p.Name == name);

    public void Dispose()
    {
        foreach (var p in Parts) p.Dispose();
        ZeroMorphs.Dispose();
    }
}

/// <summary>Shared texture cache (textures are disposed with the cache, not with the assets).</summary>
public sealed class TextureCache(GraphicsDevice gd) : IDisposable
{
    readonly Dictionary<string, Texture2D> _tex = new(StringComparer.Ordinal);
    public Texture2D Get(string path)
    {
        if (!_tex.TryGetValue(path, out var t)) { t = BakedTexture.Load(gd, path); _tex[path] = t; }
        return t;
    }
    public void Dispose() { foreach (var t in _tex.Values) t.Dispose(); _tex.Clear(); }
}
