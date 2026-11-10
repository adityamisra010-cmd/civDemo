using System.Diagnostics;
using Gfx.Chars.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Rendering;

/// <summary>
/// One drawn character: an asset (identity), optional outfit assets, a world transform, a pose and expression
/// weights. Holds its own morph stream (blended on the CPU when the weights change) and skin matrices.
/// PRESENTATION ONLY: a cosmetic view of published state; nothing here feeds back into any simulation.
/// </summary>
public sealed class CharInstance : IDisposable
{
    public CharAsset Asset { get; }
    public List<CharAsset> Outfits { get; } = new();
    public Matrix World = Matrix.Identity;
    public Pose Pose { get; }
    /// <summary>Expression weights by name (blink, battlecry, pain, grim, calm), 0..1.</summary>
    public SortedDictionary<string, float> Expression { get; } = new(StringComparer.Ordinal);
    public Quaternion[] Global { get; }
    public Vector3[] Heads { get; }
    public Matrix[] Skin { get; }
    public int PaletteRow { get; internal set; } = -1;
    /// <summary>Visible-index views to use per part name (an outfit's hidden-skin view, hair under a helmet).</summary>
    public Dictionary<string, string> PartViews { get; } = new(StringComparer.Ordinal);
    public HashSet<string> HiddenParts { get; } = new(StringComparer.Ordinal);
    public Vector3 Tint = Vector3.One;
    /// <summary>CPU time of the last morph blend + upload (ms) — the "expression blend cost".</summary>
    public double LastMorphMs { get; private set; }
    public int LastMorphVertices { get; private set; }

    readonly Dictionary<GpuPart, (DynamicVertexBuffer vb, MorphVertex[] data, int[] touched)> _morph = new();
    string _morphKey = "";
    readonly int _pelvis;

    public CharInstance(CharAsset asset)
    {
        Asset = asset;
        int n = asset.Skeleton.Count;
        Pose = new Pose(n);
        Global = new Quaternion[n]; Heads = new Vector3[n]; Skin = new Matrix[n];
        _pelvis = asset.Skeleton.TryIndex("pelvis", out int p) ? p : -1;
    }

    public BoundingSphere Bounds
    {
        get
        {
            var c = _pelvis >= 0 ? Heads[_pelvis] : Vector3.Zero;
            return new BoundingSphere(Vector3.Transform(c, World), 1.35f);
        }
    }

    public void SolvePose()
    {
        Fk.Solve(Asset.Skeleton, Pose, Global, Heads, _pelvis);
        Fk.SkinMatrices(Asset.Skeleton, Global, Heads, World, Skin);
    }

    /// <summary>World-space transform of a bone's rest frame after posing (for sockets and cameras).</summary>
    public Matrix BoneWorld(int bone) => Matrix.CreateFromQuaternion(Global[bone]) * Matrix.CreateTranslation(Heads[bone]) * World;

    /// <summary>
    /// World transform of an attachment socket (grip_r, grip_l, shield_l) after posing: the socket's bone-local frame
    /// (SOCK chunk; position relative to the bone head, rotation in the bone's world-aligned rest frame) carried by the
    /// bone. Weapons and shields are baked rigid to the same frame, so they follow it exactly.
    /// </summary>
    public Matrix SocketWorld(string name)
    {
        var sock = Asset.File.Sockets.Find(x => x.Name == name) ?? throw new KeyNotFoundException("socket " + name);
        var local = Matrix.CreateFromQuaternion(sock.Rotation) * Matrix.CreateTranslation(sock.Position + Asset.Skeleton.Head[sock.Bone]);
        // bind space -> posed character space for the socket's bone, then world
        var skin = Matrix.CreateTranslation(-Asset.Skeleton.Head[sock.Bone]) * Matrix.CreateFromQuaternion(Global[sock.Bone]) * Matrix.CreateTranslation(Heads[sock.Bone]);
        return local * skin * World;
    }

    /// <summary>Blends the morph stream of every morphable part when the expression weights changed.</summary>
    public void UpdateMorphs(GraphicsDevice gd)
    {
        string key = string.Join(";", Expression.Select(kv => kv.Key + "=" + kv.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        if (key == _morphKey) { LastMorphMs = 0; return; }
        _morphKey = key;
        var sw = Stopwatch.StartNew();
        int verts = 0;
        foreach (var part in AllParts())
        {
            if (part.Morphs.Count == 0) continue;
            if (!_morph.TryGetValue(part, out var st))
            {
                var vb = new DynamicVertexBuffer(gd, MorphVertex.Decl, part.Def.VertexCount, BufferUsage.WriteOnly);
                var touched = part.Morphs.SelectMany(m => m.Indices).Distinct().Select(x => (int)x).OrderBy(x => x).ToArray();
                st = (vb, new MorphVertex[part.Def.VertexCount], touched);
                _morph[part] = st;
            }
            foreach (int i in st.touched) st.data[i] = default;
            foreach (var m in part.Morphs)
            {
                if (!Expression.TryGetValue(m.Name, out float w) || w == 0) continue;
                var idx = m.Indices; var dp = m.DeltaPositions; var dn = m.DeltaNormals;
                for (int k = 0; k < idx.Length; k++)
                {
                    ref var v = ref st.data[idx[k]];
                    v.DeltaPosition += dp[k] * w; v.DeltaNormal += dn[k] * w;
                }
            }
            st.vb.SetData(st.data, 0, st.data.Length, SetDataOptions.Discard);
            verts += st.touched.Length;
        }
        LastMorphMs = sw.Elapsed.TotalMilliseconds;
        LastMorphVertices = verts;
    }

    /// <summary>The asset (identity or one of the worn outfits) that owns a part.</summary>
    public CharAsset OwnerOf(GpuPart part)
    {
        foreach (var o in Outfits) if (o.Parts.Contains(part)) return o;
        return Asset;
    }

    /// <summary>
    /// Puts on an outfit: its parts are drawn with the identity, and its views (visible skin, hair under a helmet)
    /// replace the identity's full index lists. The identity's own data is shared, never copied.
    /// </summary>
    public void Wear(GraphicsDevice gd, CharAsset outfit)
    {
        Outfits.Add(outfit);
        string id = outfit.File.Info["id"]?.GetValue<string>() ?? "outfit";
        foreach (var v in outfit.File.Views)
        {
            var part = Asset.Part(v.Mesh);
            if (part == null) continue;
            if (!part.Views.ContainsKey(id))
            {
                var ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, Math.Max(3, v.Indices.Length), BufferUsage.WriteOnly);
                if (v.Indices.Length > 0) ib.SetData(v.Indices.Select(x => (int)x).ToArray());
                part.Views[id] = (ib, v.Indices.Length / 3);
            }
            PartViews[v.Mesh] = id;
            _outfitViews[v.Mesh] = id;
        }
    }

    readonly Dictionary<string, string> _outfitViews = new(StringComparer.Ordinal);
    /// <summary>Parts that belong to headgear (hidden when bare-headed) and identity parts an outfit trims under it.</summary>
    public static bool IsHeadgear(string part) => part is "helmet" or "crest" or "hat" || part.StartsWith("helmet_", StringComparison.Ordinal);
    /// <summary>Identity hair a helmet trims: scalp hair, tails and (under cheek plates) the beard.</summary>
    static bool IsHair(string mesh) => mesh.StartsWith("hair", StringComparison.Ordinal) || mesh.StartsWith("ponytail", StringComparison.Ordinal)
        || mesh.StartsWith("beard", StringComparison.Ordinal);

    /// <summary>Headgear on (the outfit's helmet or hat, hair and beard trimmed under it) or off (bare-headed, full hair).</summary>
    public void SetHeadgear(bool on)
    {
        foreach (var o in Outfits)
            foreach (var p in o.Parts)
                if (IsHeadgear(p.Name)) { if (on) HiddenParts.Remove(p.Name); else HiddenParts.Add(p.Name); }
        foreach (var (mesh, view) in _outfitViews)
            if (IsHair(mesh)) { if (on) PartViews[mesh] = view; else PartViews.Remove(mesh); }
    }

    internal VertexBuffer MorphBuffer(GpuPart part, CharAsset owner) => _morph.TryGetValue(part, out var st) ? st.vb : owner.ZeroMorphs;

    /// <summary>
    /// Level of detail: 0 = every part (full detail), 1 or 2 = only the outfit's merged "lod1"/"lod2" mesh (about 3,000
    /// and 1,500 triangles, one draw). Chosen by the caller (distance or projected size); presentation only.
    /// </summary>
    public int Lod { get; set; }

    public static bool IsLodPart(string name) => name is "lod1" or "lod2";

    public IEnumerable<GpuPart> AllParts()
    {
        if (Lod > 0)
        {
            string want = Lod == 1 ? "lod1" : "lod2";
            foreach (var o in Outfits) foreach (var p in o.Parts) if (p.Name == want) yield return p;
            yield break;
        }
        foreach (var p in Asset.Parts) yield return p;
        foreach (var o in Outfits) foreach (var p in o.Parts) if (!IsLodPart(p.Name)) yield return p;
    }

    public void Dispose() { foreach (var st in _morph.Values) st.vb.Dispose(); _morph.Clear(); }
}
