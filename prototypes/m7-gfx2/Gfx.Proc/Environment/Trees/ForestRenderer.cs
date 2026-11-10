using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Trees;

/// <summary>Bark and leaf textures by set, from the env bake (see <see cref="EnvTextures"/>).</summary>
public sealed class TreeMaterials
{
    readonly EnvTextures _tex;
    public TreeMaterials(EnvTextures tex) { _tex = tex; }
    static string B(BarkSet b) => b.ToString().ToLowerInvariant();
    static string L(LeafSet l) => l.ToString().ToLowerInvariant();
    public Texture2D BarkAlbedo(BarkSet b) => _tex.Get($"bark_{B(b)}_alb_rgh.r1tex");
    public Texture2D BarkNormal(BarkSet b) => _tex.Get($"bark_{B(b)}_nrm_ao.r1tex", normalMap: true);
    public Texture2D LeafAlbedo(LeafSet l) => _tex.Get($"leaf_{L(l)}_alb.r1tex");
    public Texture2D LeafNormal(LeafSet l) => _tex.Get($"leaf_{L(l)}_nrm_tr.r1tex", normalMap: true);
}

/// <summary>Distances (m) at which a 20 m tree changes detail level; scaled by the tree's height.</summary>
public sealed record TreeLodSettings(float Lod1At = 42f, float ImpostorAt = 135f, float FadeBand = 0.14f, float CullAt = 6000f)
{
    /// <summary>Scale of the switch distances for a tree of height <paramref name="h"/> m.</summary>
    public static float SizeFactor(float h) => Math.Clamp(h / 20f, 0.3f, 1.35f);
}

/// <summary>
/// Draws placed trees (stage env; PRESENTATION ONLY). Every frame each instance gets its detail level by camera
/// distance: LOD0 (full ez-tree mesh), LOD1 (ez-tree's reduced mesh), or the impostor. Within a band around each
/// switch distance the instance is drawn in both levels with complementary screen-space dithering (an exact
/// cross-fade: each pixel shows exactly one of the two), so levels never pop; jittered stills average the dither into
/// a smooth blend. Shadow passes use one level per tree (by camera distance) and cull by each cascade's light frustum.
/// </summary>
public sealed class ForestRenderer : IDisposable
{
    sealed class Batch
    {
        public TreeInstance[] Items = new TreeInstance[16]; public int Count;
        public InstanceBuffer<TreeInstance> Buffer = null!;
        public void Add(in TreeInstance t) { if (Count == Items.Length) Array.Resize(ref Items, Count * 2); Items[Count++] = t; }
    }

    sealed class Slot
    {
        public TreeVariantGpu Gpu = null!;
        public TreeInstance[] Instances = Array.Empty<TreeInstance>();
        public Vector4[] Spheres = Array.Empty<Vector4>();
        public int[] Visible = Array.Empty<int>();
        public readonly Batch[] Batches = new Batch[3];
    }

    readonly GraphicsDevice _gd;
    readonly List<Slot> _slots = new();
    readonly TreeMaterials _mats;
    public TreeLodSettings Lod { get; set; } = new();
    /// <summary>Wind: x = strength 0..1, direction in xz.</summary>
    public float WindStrength { get; set; } = 0.35f;
    public Vector3 WindDirection { get; set; } = Vector3.Normalize(new Vector3(0.8f, 0, 0.6f));
    public int LastInstances { get; private set; }
    public int[] LastPerLod { get; } = new int[3];
    public int LastTriangles { get; private set; }
    public int TotalInstances { get; private set; }
    public IReadOnlyList<TreeVariantGpu> Variants => _slots.Select(s => s.Gpu).ToArray();

    public ForestRenderer(GraphicsDevice gd, TreeMaterials mats) { _gd = gd; _mats = mats; }

    /// <summary>Adds a variant with its (static) instances; returns its index.</summary>
    public int Add(TreeVariantGpu v, IReadOnlyList<TreeInstance> instances)
    {
        var s = new Slot { Gpu = v, Instances = instances.ToArray() };
        s.Spheres = new Vector4[s.Instances.Length];
        var c = v.Model.Sphere;
        for (int i = 0; i < s.Instances.Length; i++)
        {
            var t = s.Instances[i]; float sc = t.Lod.Z;
            var p = t.Position + Vector3.Transform(c.Center * sc, Matrix.CreateRotationY(t.Lod.W));
            s.Spheres[i] = new Vector4(p, c.Radius * sc);
        }
        s.Visible = new int[s.Instances.Length];
        for (int k = 0; k < 3; k++) s.Batches[k] = new Batch { Buffer = new InstanceBuffer<TreeInstance>(_gd, Math.Max(1, Math.Min(4096, s.Instances.Length))) };
        _slots.Add(s);
        TotalInstances += s.Instances.Length;
        return _slots.Count - 1;
    }

    /// <summary>Level weights for distance <paramref name="d"/>: (level, fade, sign) pairs, at most two.</summary>
    public static int Assign(float d, float factor, TreeLodSettings lod, bool fade, bool impostors, Span<(int level, float fade, float sign)> outp)
    {
        float d0 = lod.Lod1At * factor, d1 = lod.ImpostorAt * factor;
        int far = impostors ? 2 : 1;
        if (!fade)
        {
            outp[0] = (d < d0 ? 0 : d < d1 || !impostors ? 1 : 2, 0, 0);
            if (outp[0].level > far) outp[0].level = far;
            return 1;
        }
        float b0 = d0 * lod.FadeBand, b1 = d1 * lod.FadeBand;
        if (d < d0 - b0) { outp[0] = (0, 0, 0); return 1; }
        if (d < d0 + b0) { float f = Smooth((d - (d0 - b0)) / (2 * b0)); outp[0] = (0, f, +1); outp[1] = (1, f, -1); return 2; }
        if (!impostors || d < d1 - b1) { outp[0] = (1, 0, 0); return 1; }
        if (d < d1 + b1) { float f = Smooth((d - (d1 - b1)) / (2 * b1)); outp[0] = (1, f, +1); outp[1] = (far, f, -1); return 2; }
        outp[0] = (far, 0, 0); return 1;
    }

    static float Smooth(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3 - 2 * t); }

    void Fill(RenderContext rc, bool shadow)
    {
        var f = rc.Features;
        var cam = rc.Camera.Position;
        bool reflection = rc.IsReflectionPass;
        Span<(int level, float fade, float sign)> a = stackalloc (int, float, float)[2];
        LastInstances = 0; Array.Clear(LastPerLod);
        foreach (var s in _slots)
        {
            for (int k = 0; k < 3; k++) s.Batches[k].Count = 0;
            if (s.Instances.Length == 0) continue;
            int vis = shadow ? (rc.ShadowFrustum is { } sf ? sf.CullSpheres(s.Spheres, s.Visible) : All(s)) : rc.Frustum.CullSpheres(s.Spheres, s.Visible);
            float factor = TreeLodSettings.SizeFactor(s.Gpu.Model.Height);
            bool imp = f.EnvImpostors && s.Gpu.ImpostorAlbedo != null;
            for (int q = 0; q < vis; q++)
            {
                int i = s.Visible[q];
                var sp = s.Spheres[i];
                float d = Vector3.Distance(cam, new Vector3(sp.X, sp.Y, sp.Z));
                if (d > Lod.CullAt) continue;
                int n;
                if (shadow || reflection)
                {
                    // one level per tree: shadows and reflections never cross-fade (coarser in reflections)
                    n = Assign(reflection ? d * 2.5f : d, factor, Lod, false, imp, a);
                }
                else n = Assign(d, factor, Lod, f.EnvLodFade, imp, a);
                for (int k = 0; k < n; k++)
                {
                    var t = s.Instances[i];
                    t.Lod.X = a[k].fade; t.Lod.Y = a[k].sign;
                    s.Batches[a[k].level].Add(t);
                }
                LastInstances++;
            }
            for (int k = 0; k < 3; k++)
            {
                var bt = s.Batches[k];
                if (bt.Count > 0) bt.Buffer.Set(bt.Items, bt.Count);
                LastPerLod[k] += bt.Count;
            }
        }
    }

    static int All(Slot s) { for (int i = 0; i < s.Instances.Length; i++) s.Visible[i] = i; return s.Instances.Length; }

    Vector4 EnvParams(RenderContext rc, float cut) =>
        new(rc.SampleIndex, rc.SampleCount, rc.SampleCount > 1 && !rc.IsShadowPass ? 1 : 0, cut);

    void SetWind(RenderContext rc, Effect e)
    {
        bool on = rc.Features.EnvWind;
        e.Parameters["WindParams"].SetValue(new Vector4(rc.TimeSeconds, WindStrength, on ? 1 : 0, 0.7f));
        e.Parameters["WindDir"].SetValue(new Vector4(WindDirection, 0));
    }

    public void DrawShadowCasters(RenderContext rc)
    {
        Fill(rc, shadow: true);
        Draw(rc, depth: true);
    }

    public void DrawOpaque(RenderContext rc)
    {
        Fill(rc, shadow: false);
        Draw(rc, depth: false);
    }

    void Draw(RenderContext rc, bool depth)
    {
        int tris = 0;
        foreach (var s in _slots)
        {
            var v = s.Gpu; var sp = v.Model.Species;
            for (int k = 0; k < 2; k++)
            {
                var bt = s.Batches[k];
                if (bt.Count == 0) continue;
                var lod = k == 0 ? v.Lod0 : v.Lod1;
                tris += DrawMesh(rc, depth, true, lod, bt, sp, v.Model);
                tris += DrawMesh(rc, depth, false, lod, bt, sp, v.Model);
            }
            var bi = s.Batches[2];
            if (bi.Count > 0 && v.ImpostorAlbedo != null) tris += DrawImpostors(rc, depth, v, bi);
        }
        LastTriangles = tris;
    }

    int DrawMesh(RenderContext rc, bool depth, bool bark, TreeLodGpu lod, Batch bt, TreeSpecies sp, TreeModel model)
    {
        int tris = bark ? lod.BarkTris : lod.LeafTris;
        if (tris == 0) return 0;
        var e = rc.Effects.Get(bark ? (depth ? EnvEffectNames.BarkDepth : EnvEffectNames.Bark) : (depth ? EnvEffectNames.LeafDepth : EnvEffectNames.Leaf));
        var p = e.Parameters;
        p["EnvParams"]?.SetValue(EnvParams(rc, 0.5f));
        SetWind(rc, e);
        if (!bark)
        {
            p["TreeParams"].SetValue(new Vector4(model.Height, 0, 0, 0));
            p["LeafAlb"].SetValue(_mats.LeafAlbedo(sp.Leaf));
        }
        if (!depth)
        {
            p["BakeMode"].SetValue(Vector4.Zero);
            p["SpeciesTint"].SetValue(new Vector4(bark ? sp.BarkTint : sp.LeafTint, 1));
            if (bark) { p["BarkAlb"].SetValue(_mats.BarkAlbedo(sp.Bark)); p["BarkNrm"].SetValue(_mats.BarkNormal(sp.Bark)); }
            else { p["LeafNrm"].SetValue(_mats.LeafNormal(sp.Leaf)); p["Transmission"].SetValue(new Vector4(sp.Transmission, 0)); }
        }
        _gd.SetVertexBuffers(new VertexBufferBinding(bark ? lod.BarkVb : lod.LeafVb, 0, 0), new VertexBufferBinding(bt.Buffer.Buffer, 0, 1));
        _gd.Indices = bark ? lod.BarkIb : lod.LeafIb;
        rc.Apply(e);
        _gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, tris, bt.Count);
        rc.Stats.RecordDraw(tris * bt.Count, bt.Count);
        return tris * bt.Count;
    }

    VertexBuffer? _quadVb; IndexBuffer? _quadIb;

    int DrawImpostors(RenderContext rc, bool depth, TreeVariantGpu v, Batch bt)
    {
        if (_quadVb == null)
        {
            _quadVb = new VertexBuffer(_gd, VertexPosition.VertexDeclaration, 4, BufferUsage.WriteOnly);
            _quadVb.SetData(new[] { new VertexPosition(new Vector3(-1, -1, 0)), new VertexPosition(new Vector3(1, -1, 0)), new VertexPosition(new Vector3(1, 1, 0)), new VertexPosition(new Vector3(-1, 1, 0)) });
            _quadIb = new IndexBuffer(_gd, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
            _quadIb.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
        }
        var e = rc.Effects.Get(depth ? EnvEffectNames.ImpostorDepth : EnvEffectNames.Impostor);
        var p = e.Parameters;
        var m = v.Model;
        p["EnvParams"].SetValue(EnvParams(rc, 0.4f));
        SetWind(rc, e);
        p["ImpParams"].SetValue(new Vector4(v.ImpostorFrames, v.FrameSize, v.ImpostorFrames * v.FrameSize, m.Sphere.Radius * 1.02f));
        p["ImpCenter"].SetValue(new Vector4(m.Sphere.Center, 0));
        // the shadow pass faces the light: a viewer far towards the sun
        var eye = depth ? rc.Camera.Target + rc.Sun.DirectionToLight * 20000f : rc.Camera.Position;
        p["ImpEye"].SetValue(new Vector4(eye, 1));
        p["ImpViewProj"].SetValue(depth ? rc.LightViewProj : rc.ViewProj);
        p["ImpAlb"].SetValue(v.ImpostorAlbedo); p["ImpNrm"].SetValue(v.ImpostorNormal);
        if (depth)
        {
            // orthographic cascade: one shadow texel spans (2 / M11) / tile size world units
            float texel = 2f / MathF.Max(MathF.Abs(rc.LightViewProj.M11), 1e-6f) / MathF.Max(rc.Features.CascadeSize, 1);
            p["ImpLod"].SetValue(new Vector4(0, texel, 0, 0));
        }
        else p["ImpLod"].SetValue(new Vector4(2f * MathF.Tan(rc.Camera.FovYRadians * 0.5f) * rc.ViewportSize.W, 0, 0, 0));
        if (!depth)
        {
            p["ImpLightViewProj"].SetValue(rc.LightViewProj);
            // w: shrub (a small, dense crown whose baked occlusion understates the sun reaching its outer leaves)
            p["Transmission"].SetValue(new Vector4(m.Species.Transmission, m.Species.IsShrub ? 1 : 0));
        }
        _gd.SetVertexBuffers(new VertexBufferBinding(_quadVb, 0, 0), new VertexBufferBinding(bt.Buffer.Buffer, 0, 1));
        _gd.Indices = _quadIb;
        rc.Apply(e);
        // impostor atlases: trilinear, clamped (frames are padded by the bleed)
        _gd.SamplerStates[ImpostorEffects.AlbSlot] = SamplerState.LinearClamp; _gd.SamplerStates[ImpostorEffects.NrmSlot] = SamplerState.LinearClamp;
        _gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 2, bt.Count);
        rc.Stats.RecordDraw(2 * bt.Count, bt.Count);
        return 2 * bt.Count;
    }

    public void Dispose()
    {
        foreach (var s in _slots) { s.Gpu.Dispose(); foreach (var b in s.Batches) b?.Buffer.Dispose(); }
        _quadVb?.Dispose(); _quadIb?.Dispose();
    }
}
