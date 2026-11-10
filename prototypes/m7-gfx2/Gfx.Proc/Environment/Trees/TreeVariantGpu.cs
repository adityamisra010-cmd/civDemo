using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Proc.Environment.Trees;

/// <summary>GPU buffers of one tree detail level (bark rings and leaf cards).</summary>
public sealed class TreeLodGpu : IDisposable
{
    public VertexBuffer BarkVb { get; }
    public IndexBuffer BarkIb { get; }
    public VertexBuffer LeafVb { get; }
    public IndexBuffer LeafIb { get; }
    public int BarkTris { get; }
    public int LeafTris { get; }

    public TreeLodGpu(GraphicsDevice gd, TreeLodMesh m)
    {
        BarkVb = new VertexBuffer(gd, VertexBark.Decl, Math.Max(1, m.BarkVertices.Length), BufferUsage.WriteOnly);
        if (m.BarkVertices.Length > 0) BarkVb.SetData(m.BarkVertices);
        BarkIb = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, Math.Max(3, m.BarkIndices.Length), BufferUsage.WriteOnly);
        if (m.BarkIndices.Length > 0) BarkIb.SetData(m.BarkIndices);
        LeafVb = new VertexBuffer(gd, VertexLeaf.Decl, Math.Max(1, m.LeafVertices.Length), BufferUsage.WriteOnly);
        if (m.LeafVertices.Length > 0) LeafVb.SetData(m.LeafVertices);
        LeafIb = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, Math.Max(3, m.LeafIndices.Length), BufferUsage.WriteOnly);
        if (m.LeafIndices.Length > 0) LeafIb.SetData(m.LeafIndices);
        BarkTris = m.BarkIndices.Length / 3; LeafTris = m.LeafIndices.Length / 3;
    }

    public void Dispose() { BarkVb.Dispose(); BarkIb.Dispose(); LeafVb.Dispose(); LeafIb.Dispose(); }
}

/// <summary>
/// One tree variant on the GPU: LOD0, LOD1 and the hemi-octahedral impostor atlases (albedo × canopy occlusion +
/// coverage; local normal + depth), rendered from LOD0 at load (2× supersampled, read back, downsampled with
/// premultiplied coverage, colour bled into empty texels, coverage-preserving alpha mips). PRESENTATION ONLY.
/// </summary>
public sealed class TreeVariantGpu : IDisposable
{
    public TreeModel Model { get; }
    public TreeLodGpu Lod0 { get; }
    public TreeLodGpu Lod1 { get; }
    public Texture2D? ImpostorAlbedo { get; private set; }
    public Texture2D? ImpostorNormal { get; private set; }
    public int ImpostorFrames { get; }
    public int FrameSize { get; }
    /// <summary>Wall time of the impostor bake (ms, llvmpipe here).</summary>
    public double ImpostorBakeMs { get; private set; }

    public TreeVariantGpu(GraphicsDevice gd, TreeModel model, int frames = 8, int frameSize = 128)
    {
        Model = model;
        Lod0 = new TreeLodGpu(gd, model.Lod0);
        Lod1 = new TreeLodGpu(gd, model.Lod1);
        ImpostorFrames = frames; FrameSize = frameSize;
    }

    /// <summary>Renders the impostor atlases. Call outside a frame (it binds its own targets and restores none).</summary>
    public void BakeImpostors(GraphicsDevice gd, EffectLibrary fx, TreeMaterials mats)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int ss = 2;
        int n = ImpostorFrames, f = FrameSize, big = n * f * ss;
        var sphere = Model.Sphere;
        float R = sphere.Radius * 1.02f;
        using var rt = new RenderTarget2D(gd, big, big, false, SurfaceFormat.Color, DepthFormat.Depth24);
        using var inst = new InstanceBuffer<TreeInstance>(gd, 1);
        inst.Set(new[] { TreeInstance.From(Vector3.Zero, 0, 1, Vector3.One, 0) });
        var albedo = new Color[big * big];
        var normal = new Color[big * big];
        for (int mode = 1; mode <= 2; mode++)
        {
            gd.SetRenderTarget(rt);
            gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(0, 0, 0, 0), 1f, 0);
            gd.BlendState = BlendState.Opaque; gd.DepthStencilState = DepthStencilState.Default; gd.RasterizerState = RasterizerState.CullNone;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var d = ImpostorMath.FrameDirection(i, j, n);
                    var (_, up) = ImpostorMath.Basis(d);
                    var view = Matrix.CreateLookAt(sphere.Center + d * (3 * R), sphere.Center, up);
                    var proj = Matrix.CreateOrthographic(2 * R, 2 * R, R * 0.5f, R * 5.5f);
                    gd.Viewport = new Viewport(i * f * ss, j * f * ss, f * ss, f * ss);
                    var vp = view * proj;
                    DrawBake(gd, fx, mats, Lod0, inst, vp, mode, d, R, sphere.Center, bark: true);
                    DrawBake(gd, fx, mats, Lod0, inst, vp, mode, d, R, sphere.Center, bark: false);
                }
            gd.SetRenderTarget(null);
            rt.GetData(mode == 1 ? albedo : normal);
        }
        for (int i = 0; i < 16; i++) gd.Textures[i] = null;
        var (alb, nrm) = ImpostorAtlas.Build(albedo, normal, big, ss);
        ImpostorAlbedo = alb.Create(gd); ImpostorNormal = nrm.Create(gd);
        for (int i = 0; i < 16; i++) gd.Textures[i] = null;
        ImpostorBakeMs = sw.Elapsed.TotalMilliseconds;
    }

    void DrawBake(GraphicsDevice gd, EffectLibrary fx, TreeMaterials mats, TreeLodGpu lod, InstanceBuffer<TreeInstance> inst, Matrix vp,
        int mode, Vector3 dir, float radius, Vector3 centre, bool bark)
    {
        int tris = bark ? lod.BarkTris : lod.LeafTris;
        if (tris == 0) return;
        var e = fx.Get(bark ? EnvEffectNames.Bark : EnvEffectNames.Leaf);
        var p = e.Parameters;
        p["ViewProj"].SetValue(vp);
        p["BakeMode"].SetValue(new Vector4(mode, 0, 0, 0));
        p["BakeFrame"].SetValue(new Vector4(dir, radius));
        p["BakeCenter"].SetValue(new Vector4(centre, 0));
        p["EnvParams"].SetValue(new Vector4(0, 1, 0, 0.5f));
        p["WindParams"].SetValue(Vector4.Zero);
        p["WindDir"].SetValue(new Vector4(1, 0, 0, 0));
        var sp = Model.Species;
        p["SpeciesTint"].SetValue(new Vector4(bark ? sp.BarkTint : sp.LeafTint, 1));
        // lighting uniforms are unused in bake mode, but must not be NaN
        p["LightModel"]?.SetValue(Vector4.Zero);
        p["ShadowParams"]?.SetValue(Vector4.Zero);
        if (bark) { p["BarkAlb"].SetValue(mats.BarkAlbedo(sp.Bark)); p["BarkNrm"].SetValue(mats.BarkNormal(sp.Bark)); }
        else
        {
            p["LeafAlb"].SetValue(mats.LeafAlbedo(sp.Leaf)); p["LeafNrm"].SetValue(mats.LeafNormal(sp.Leaf));
            p["TreeParams"].SetValue(new Vector4(Model.Height, 0, 0, 0));
            p["Transmission"].SetValue(new Vector4(sp.Transmission, 0));
        }
        gd.SetVertexBuffers(new VertexBufferBinding(bark ? lod.BarkVb : lod.LeafVb, 0, 0), new VertexBufferBinding(inst.Buffer, 0, 1));
        gd.Indices = bark ? lod.BarkIb : lod.LeafIb;
        e.CurrentTechnique.Passes[0].Apply();
        for (int s = 0; s < 16; s++) gd.SamplerStates[s] = SamplerState.LinearClamp;
        gd.SamplerStates[TreeEffects.AlbSlot] = SamplerState.LinearWrap; gd.SamplerStates[TreeEffects.NrmSlot] = SamplerState.LinearWrap;
        gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, tris, 1);
    }

    public void Dispose()
    {
        Lod0.Dispose(); Lod1.Dispose(); ImpostorAlbedo?.Dispose(); ImpostorNormal?.Dispose();
    }
}

/// <summary>CPU post-processing of a supersampled impostor atlas into mipmapped textures.</summary>
public static class ImpostorAtlas
{
    public sealed class Mipped
    {
        public required int Size { get; init; }
        public required List<Color[]> Levels { get; init; }
        public Texture2D Create(GraphicsDevice gd)
        {
            var t = new Texture2D(gd, Size, Size, true, SurfaceFormat.Color);
            for (int l = 0; l < t.LevelCount && l < Levels.Count; l++) t.SetData(l, null, Levels[l], 0, Levels[l].Length);
            return t;
        }
    }

    /// <summary>Octahedral normal packing, y up (the same as the GLSL <c>octEnc</c>/<c>octDec</c>).</summary>
    public static Vector2 OctEncode(Vector3 n)
    {
        n /= MathF.Abs(n.X) + MathF.Abs(n.Y) + MathF.Abs(n.Z);
        var p = new Vector2(n.X, n.Z);
        if (n.Y < 0) p = new Vector2((1 - MathF.Abs(p.Y)) * (p.X >= 0 ? 1 : -1), (1 - MathF.Abs(p.X)) * (p.Y >= 0 ? 1 : -1));
        return p * 0.5f + new Vector2(0.5f);
    }

    public static Vector3 OctDecode(Vector2 e)
    {
        var f = e * 2f - Vector2.One;
        var n = new Vector3(f.X, 1 - MathF.Abs(f.X) - MathF.Abs(f.Y), f.Y);
        if (n.Y < 0) { float tx = n.X, tz = n.Z; n.X = (1 - MathF.Abs(tz)) * (tx >= 0 ? 1 : -1); n.Z = (1 - MathF.Abs(tx)) * (tz >= 0 ? 1 : -1); }
        return Vector3.Normalize(n);
    }

    /// <summary>
    /// Downsamples the ss× atlas: albedo averaged with coverage weights, coverage = mean alpha; normal (decoded), depth
    /// and occlusion averaged with coverage weights. Empty texels take the mean of their covered neighbours (8 dilation
    /// rounds), so filtering never pulls in black. Each albedo mip rescales alpha so the share of texels passing the
    /// 0.5 test stays that of level 0.
    /// </summary>
    public static (Mipped albedo, Mipped normal) Build(Color[] albedo, Color[] normal, int big, int ss)
    {
        int size = big / ss, n = size * size;
        var col = new Vector3[n]; var cov = new float[n]; var nrm = new Vector3[n]; var dep = new float[n]; var occ = new float[n];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float c = 0, d = 0, o = 0; var rgb = Vector3.Zero; var nn = Vector3.Zero;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        int i = (y * ss + sy) * big + x * ss + sx;
                        var a = albedo[i]; var b = normal[i];
                        float w = a.A / 255f;
                        if (w <= 0) continue;
                        c += w; rgb += w * new Vector3(a.R, a.G, a.B);
                        nn += w * OctDecode(new Vector2(b.R / 255f, b.G / 255f)); d += w * b.B; o += w * b.A;
                    }
                int k = y * size + x;
                cov[k] = c / (ss * ss);
                if (c > 0) { col[k] = rgb / c; nrm[k] = nn; dep[k] = d / c; occ[k] = o / c; }
            }
        Dilate(col, nrm, dep, occ, cov, size, 8);
        return (AlbedoMips(col, cov, size), NormalMips(nrm, dep, occ, size));
    }

    static void Dilate(Vector3[] col, Vector3[] nrm, float[] dep, float[] occ, float[] cov, int size, int rounds)
    {
        var filled = new bool[cov.Length];
        for (int i = 0; i < filled.Length; i++) filled[i] = cov[i] > 0;
        var next = new bool[filled.Length];
        for (int r = 0; r < rounds; r++)
        {
            Array.Copy(filled, next, filled.Length);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (filled[i]) continue;
                    var c = Vector3.Zero; var nn = Vector3.Zero; float d = 0, o = 0; int cnt = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                            int j = yy * size + xx;
                            if (!filled[j]) continue;
                            c += col[j]; nn += nrm[j]; d += dep[j]; o += occ[j]; cnt++;
                        }
                    if (cnt == 0) continue;
                    col[i] = c / cnt; nrm[i] = nn; dep[i] = d / cnt; occ[i] = o / cnt; next[i] = true;
                }
            Array.Copy(next, filled, filled.Length);
        }
    }

    static byte B(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);

    static Mipped AlbedoMips(Vector3[] col, float[] cov, int size)
    {
        var levels = new List<Color[]>();
        int passing = 0; foreach (var c in cov) if (c > 0.5f) passing++;
        float baseCov = passing / (float)cov.Length;
        var c0 = col; var a0 = cov; int s = size;
        while (true)
        {
            float scale = s == size ? 1f : CoverageScale(a0, baseCov);
            var lv = new Color[s * s];
            for (int i = 0; i < lv.Length; i++) lv[i] = new Color(B(c0[i].X), B(c0[i].Y), B(c0[i].Z), B(MathF.Min(1f, a0[i] * scale) * 255f));
            levels.Add(lv);
            if (s == 1) break;
            int h = s / 2;
            var c1 = new Vector3[h * h]; var a1 = new float[h * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < h; x++)
                {
                    var sum = Vector3.Zero; float wsum = 0, asum = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int i = (2 * y + (k >> 1)) * s + 2 * x + (k & 1);
                        float w = MathF.Max(a0[i], 0.02f);
                        sum += c0[i] * w; wsum += w; asum += a0[i];
                    }
                    c1[y * h + x] = sum / wsum; a1[y * h + x] = asum * 0.25f;
                }
            c0 = c1; a0 = a1; s = h;
        }
        return new Mipped { Size = size, Levels = levels };
    }

    static Mipped NormalMips(Vector3[] nrm, float[] dep, float[] occ, int size)
    {
        var levels = new List<Color[]>();
        var n0 = nrm; var d0 = dep; var o0 = occ; int s = size;
        while (true)
        {
            var lv = new Color[s * s];
            for (int i = 0; i < lv.Length; i++)
            {
                var v = n0[i].LengthSquared() > 1e-10f ? Vector3.Normalize(n0[i]) : Vector3.Up;
                var e = OctEncode(v);
                lv[i] = new Color(B(e.X * 255f), B(e.Y * 255f), B(d0[i]), B(o0[i]));
            }
            levels.Add(lv);
            if (s == 1) break;
            int h = s / 2;
            var n1 = new Vector3[h * h]; var d1 = new float[h * h]; var o1 = new float[h * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < h; x++)
                {
                    var nn = Vector3.Zero; float d = 0, o = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int i = (2 * y + (k >> 1)) * s + 2 * x + (k & 1);
                        nn += n0[i].LengthSquared() > 1e-10f ? Vector3.Normalize(n0[i]) : Vector3.Zero; d += d0[i]; o += o0[i];
                    }
                    n1[y * h + x] = nn; d1[y * h + x] = d * 0.25f; o1[y * h + x] = o * 0.25f;
                }
            n0 = n1; d0 = d1; o0 = o1; s = h;
        }
        return new Mipped { Size = size, Levels = levels };
    }

    /// <summary>Scale for this level's coverage so the share of texels passing the 0.5 test equals level 0's.</summary>
    static float CoverageScale(float[] a, float target)
    {
        float lo = 0.5f, hi = 6f;
        for (int it = 0; it < 18; it++)
        {
            float mid = 0.5f * (lo + hi); int c = 0;
            foreach (var v in a) if (v * mid > 0.5f) c++;
            if (c / (float)a.Length < target) lo = mid; else hi = mid;
        }
        return hi;
    }
}
