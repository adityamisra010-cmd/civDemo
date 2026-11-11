using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Engine.Assets;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Crowd;

/// <summary>One drawable look of the crowd: an identity in one outfit, with its three LOD meshes and textures.</summary>
public sealed class CrowdVariant
{
    public required string Identity { get; init; }
    public required string Outfit { get; init; }
    /// <summary>Index of the identity in the animation bank.</summary>
    public required int BankIdentity { get; init; }
    /// <summary>LOD meshes: 0 = lod1, 1 = lod2, 2 = lod3 (lod2 again when the crowd bake is missing).</summary>
    public required GpuPart[] Lods { get; init; }
    public required Texture2D Skin { get; init; }
    public required Texture2D Emblem { get; init; }
    /// <summary>Standing height of the identity (m), for the projected-size LOD rule.</summary>
    public required float Height { get; init; }
    internal readonly CrowdBatch[] Main = new CrowdBatch[3], Shadow = new CrowdBatch[3];
    public string Key => Identity + "__" + Outfit;
}

internal sealed class CrowdBatch(GraphicsDevice gd) : IDisposable
{
    public CrowdInstance[] Items = new CrowdInstance[64];
    public int Count;
    public readonly InstanceBuffer<CrowdInstance> Buffer = new(gd, 64);
    public void Add(in CrowdInstance v)
    {
        if (Count == Items.Length) Array.Resize(ref Items, Items.Length * 2);
        Items[Count++] = v;
    }
    public void Dispose() => Buffer.Dispose();
}

/// <summary>What the crowd drew in the last pass of each kind (diagnostics, costs and tests).</summary>
public sealed class CrowdStats
{
    public int Agents;
    /// <summary>Instances drawn per level in the last main pass (a soldier in a cross-fade counts in both levels).</summary>
    public readonly int[] PerLevel = new int[3];
    public int Soldiers, CulledFrustum, CulledOcclusion, InFade, Draws;
    public long Triangles;
    public int ShadowInstances, ShadowDraws, ShadowCulledCascade;
    public long ShadowTriangles;
    /// <summary>CPU wall time (ms) of the last <see cref="CrowdRenderer.SetAgents"/>, of the last main pass's culling and
    /// level choice (before any draw is issued), and of the shadow passes' selection summed over the frame's cascades.</summary>
    public double SetAgentsMs, CullMs, ShadowSelectMs;
    public void ResetMain() { Array.Clear(PerLevel); Soldiers = CulledFrustum = CulledOcclusion = InFade = Draws = 0; Triangles = 0; CullMs = 0; }
}

/// <summary>
/// Instanced, GPU-skinned crowd of stage-chars soldiers (stage render2; PRESENTATION ONLY). Every visible soldier is one
/// instance of its variant's LOD mesh; one draw per variant and level and pass. Per frame: <see cref="SetAgents"/> once
/// (world transforms, bank rows, looks), then the renderer's passes call <see cref="DrawOpaque"/> (frustum and
/// occlusion culling, level choice with the dithered cross-fade) and <see cref="DrawShadows"/> (per cascade: the
/// cascade's frustum, one coarser level, no fade). Agents come from a scripted scene or from published state; nothing is
/// written back.
/// </summary>
public sealed class CrowdRenderer : IDisposable
{
    readonly GraphicsDevice _gd;
    readonly List<CrowdVariant> _variants = new();
    readonly List<CharAsset> _ownAssets = new();
    CrowdAgent[] _agents = Array.Empty<CrowdAgent>();
    CrowdInstance[] _base = Array.Empty<CrowdInstance>();
    float[] _height = Array.Empty<float>();
    int _count;
    // the previous SetAgents (object motion for TAA): bank rows and positions per agent index
    Vector4[] _prevA = Array.Empty<Vector4>(), _prevB = Array.Empty<Vector4>();
    Vector3[] _prevPos = Array.Empty<Vector3>();
    int _prevCount = -1;

    public CrowdAnimationBank Bank { get; }
    public IReadOnlyList<CrowdVariant> Variants => _variants;
    public CrowdLodSettings Lod { get; set; } = CrowdLodSettings.Default;
    /// <summary>Dithered cross-fade between levels (off = hard switches).</summary>
    public bool LodFade { get; set; } = true;
    /// <summary>Forces one level for every soldier (−1 = by projected size). For the LOD sweep.</summary>
    public int ForceLevel { get; set; } = -1;
    /// <summary>Per-soldier looks; off = every soldier is drawn with <see cref="CrowdLook.Neutral"/> (no size variation either).</summary>
    public bool Looks { get; set; } = true;
    /// <summary>Two-clip blending; off = each soldier is drawn with the heavier of its two clips only.</summary>
    public bool ClipBlend { get; set; } = true;
    public bool FrustumCulling { get; set; } = true;
    /// <summary>Optional occlusion test of a soldier's bounding sphere (true = hidden); see <see cref="OcclusionBuffer"/>.</summary>
    public OcclusionBuffer? Occlusion { get; set; }
    /// <summary>Soldiers cast shadows up to this distance from the camera (m).</summary>
    public float ShadowDistance { get; set; } = 320f;
    public CrowdStats Stats { get; } = new();
    /// <summary>True when the crowd LOD3 bake was found; otherwise level 2 draws lod2.</summary>
    public bool HasLod3 { get; }

    /// <summary>Bounding sphere of a soldier relative to its feet: centre 1.2 m up, radius 1.7 m (head, raised arms, upright spear).</summary>
    public const float BoundsUp = 1.2f, BoundsRadius = 1.7f;

    public CrowdRenderer(GraphicsDevice gd, CharStage loader, IReadOnlyList<(string identity, string outfit)> variants, string? crowdFolder = null)
    {
        _gd = gd;
        crowdFolder ??= AssetRoot.Baked("crowd");
        var identities = new List<string>();
        var bankInput = new List<(Gfx.Chars.Animation.Skeleton, CharacterFile)>();
        bool lod3 = true;
        foreach (var (id, outfit) in variants)
        {
            var ident = loader.Asset(id);
            var dress = loader.Asset(id + "__" + outfit);
            int bi = identities.IndexOf(id);
            if (bi < 0) { bi = identities.Count; identities.Add(id); bankInput.Add((ident.Skeleton, ident.ClipFile)); }
            var l1 = dress.Part("lod1") ?? throw new InvalidDataException($"{id}__{outfit}: no lod1");
            var l2 = dress.Part("lod2") ?? throw new InvalidDataException($"{id}__{outfit}: no lod2");
            GpuPart l3 = l2;
            string p3 = Path.Combine(crowdFolder, $"{id}__{outfit}__lod3.g2ch");
            if (File.Exists(p3))
            {
                var a3 = CharAsset.Load(gd, p3);
                _ownAssets.Add(a3);
                l3 = a3.Part("lod3") ?? l2;
            }
            else lod3 = false;
            string? em = dress.File.Info["lod_emblem"]?.GetValue<string>();
            var white = loader.Renderer.White;
            var skin = ident.Textures.TryGetValue("skin", out var st) ? st : white;
            var emblem = em != null && dress.Textures.TryGetValue("emblem:" + em, out var et) ? et : white;
            float height = 1.75f;
            if (ident.File.Info["stature_m"] is { } sm) height = (float)sm.GetValue<double>();
            else if (ident.Skeleton.TryIndex("head", out int hb)) height = ident.Skeleton.Tail[hb].Y + 0.05f;
            var v = new CrowdVariant
            {
                Identity = id, Outfit = outfit, BankIdentity = bi, Lods = new[] { l1, l2, l3 }, Skin = skin, Emblem = emblem, Height = height,
            };
            for (int k = 0; k < 3; k++) { v.Main[k] = new CrowdBatch(gd); v.Shadow[k] = new CrowdBatch(gd); }
            _variants.Add(v);
        }
        HasLod3 = lod3;
        Bank = CrowdAnimationBank.Build(gd, bankInput);
    }

    /// <summary>Bank index of a clip name (see <see cref="CrowdAnimationBank.ClipNames"/>).</summary>
    public static int Clip(string name) => CrowdAnimationBank.ClipIndex(name) is int i and >= 0 ? i : throw new KeyNotFoundException(name);

    /// <summary>Takes this frame's agents: world transforms, bank rows and looks are computed once here.</summary>
    public void SetAgents(ReadOnlySpan<CrowdAgent> agents)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        // keep this frame's rows and positions as the next frame's previous ones (same agent order between frames)
        bool hasPrev = _prevCount == agents.Length && _prevCount == _count;
        if (hasPrev)
        {
            if (_prevA.Length < _count) { _prevA = new Vector4[_count]; _prevB = new Vector4[_count]; _prevPos = new Vector3[_count]; }
            for (int i = 0; i < _count; i++) { _prevA[i] = _base[i].AnimA; _prevB[i] = _base[i].AnimB; _prevPos[i] = _agents[i].Position; }
        }
        _count = agents.Length;
        if (_agents.Length < _count) { _agents = new CrowdAgent[_count]; _base = new CrowdInstance[_count]; _height = new float[_count]; }
        agents.CopyTo(_agents);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var a = ref _agents[i];
            var v = _variants[a.Variant];
            var look = a.Look;
            var world = Matrix.CreateScale(look.Width, look.Height, look.Width) * Matrix.CreateRotationY(a.Yaw) * Matrix.CreateTranslation(a.Position);
            ref var b = ref _base[i];
            b.SetWorld(world);
            var (a0, a1, fa) = Bank.Sample(v.BankIdentity, a.ClipA, a.TimeA);
            var (b0, b1, fb) = Bank.Sample(v.BankIdentity, a.ClipB, a.TimeB);
            float w = Math.Clamp(a.Blend, 0f, 1f);
            b.AnimA = new Vector4(a0, a1, fa, w);
            b.AnimB = new Vector4(b0, b1, fb, 0);
            b.Look = look.Packed;
            b.Lod = new Vector4(0, 0, look.Tarnish, look.Seed);
            if (hasPrev) { b.PrevAnimA = _prevA[i]; b.PrevAnimB = _prevB[i]; b.PrevDelta = new Vector4(a.Position - _prevPos[i], 0); }
            else { b.PrevAnimA = b.AnimA; b.PrevAnimB = b.AnimB; b.PrevDelta = Vector4.Zero; }
            _height[i] = v.Height * look.Height;
        }
        _prevCount = _count;
        Stats.Agents = _count;
        Stats.SetAgentsMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    /// <summary>The agents of the current frame (read only).</summary>
    public ReadOnlySpan<CrowdAgent> Agents => _agents.AsSpan(0, _count);

    /// <summary>
    /// Agent <paramref name="i"/>'s instance as the passes draw it: <see cref="Looks"/> off replaces its look with the
    /// neutral one (and its world transform with the unscaled one), <see cref="ClipBlend"/> off keeps only the heavier
    /// clip (for this frame and the previous one). With both on it is the instance <see cref="SetAgents"/> made.
    /// </summary>
    CrowdInstance Drawn(int i)
    {
        var inst = _base[i];
        if (!Looks)
        {
            ref readonly var a = ref _agents[i];
            inst.SetWorld(Matrix.CreateRotationY(a.Yaw) * Matrix.CreateTranslation(a.Position));
            inst.Look = CrowdLook.Neutral.Packed;
            inst.Lod = new Vector4(0, 0, CrowdLook.Neutral.Tarnish, CrowdLook.Neutral.Seed);
        }
        if (!ClipBlend)
        {
            inst.AnimA = Heavier(inst.AnimA, inst.AnimB);
            inst.PrevAnimA = Heavier(inst.PrevAnimA, inst.PrevAnimB);
        }
        return inst;
    }

    /// <summary>Clip A's rows when B weighs under a half, else clip B's rows, as an unblended clip A sample.</summary>
    static Vector4 Heavier(Vector4 a, Vector4 b) => a.W >= 0.5f ? new Vector4(b.X, b.Y, b.Z, 0) : new Vector4(a.X, a.Y, a.Z, 0);

    /// <summary>Animation level of detail for the coarsest level: one clip (the heavier one), nearest frame.</summary>
    static CrowdInstance Coarse(in CrowdInstance src)
    {
        var c = src;
        bool useB = src.AnimA.W >= 0.5f;
        var s = useB ? src.AnimB : src.AnimA;
        float row = s.Z < 0.5f ? s.X : s.Y;
        c.AnimA = new Vector4(row, row, 0, 0);
        bool useBp = src.PrevAnimA.W >= 0.5f;
        var sp = useBp ? src.PrevAnimB : src.PrevAnimA;
        float prow = sp.Z < 0.5f ? sp.X : sp.Y;
        c.PrevAnimA = new Vector4(prow, prow, 0, 0);
        return c;
    }

    public void DrawOpaque(RenderContext rc)
    {
        if (_count == 0) return;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Stats.ResetMain();
        if (!rc.Features.Crowd) return;
        foreach (var v in _variants) for (int k = 0; k < 3; k++) v.Main[k].Count = 0;
        var cam = rc.Camera.Position;
        float m22 = rc.Projection.M22, vh = rc.ViewportSize.Y;
        bool reflection = rc.IsReflectionPass;
        Span<(int level, float fade, float sign)> lv = stackalloc (int, float, float)[2];
        for (int i = 0; i < _count; i++)
        {
            ref readonly var a = ref _agents[i];
            if (a.Hidden) continue;
            var c = a.Position + new Vector3(0, BoundsUp, 0);
            if (FrustumCulling && !rc.Frustum.IntersectsSphere(c, BoundsRadius)) { Stats.CulledFrustum++; continue; }
            if (!reflection && Occlusion != null && Occlusion.IsSphereOccluded(c, BoundsRadius)) { Stats.CulledOcclusion++; continue; }
            float px = CrowdLodSettings.ProjectedPx(_height[i], Vector3.Distance(cam, c), m22, vh);
            int n;
            if (reflection) { lv[0] = (Lod.ShadowLevel(px, ForceLevel), 0, 0); n = 1; }
            else n = Lod.Assign(px, LodFade, ForceLevel, lv);
            var v = _variants[a.Variant];
            var drawn = Drawn(i);
            for (int k = 0; k < n; k++)
            {
                var inst = lv[k].level == 2 ? Coarse(drawn) : drawn;
                inst.Lod.X = lv[k].fade; inst.Lod.Y = lv[k].sign;
                v.Main[lv[k].level].Add(inst);
                Stats.PerLevel[lv[k].level]++;
            }
            if (n == 2) Stats.InFade++;
            Stats.Soldiers++;
        }
        Stats.CullMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        var fx = rc.Effects.Get(CrowdEffectNames.Lit);
        fx.Parameters["CrowdParams"].SetValue(new Vector4(rc.SampleIndex, rc.SampleCount, 0, 0));
        foreach (var v in _variants)
        {
            fx.Parameters["SkinTex"].SetValue(v.Skin);
            fx.Parameters["Emblem"].SetValue(v.Emblem);
            for (int k = 0; k < 3; k++)
            {
                var (d, t) = DrawBatch(rc, fx, v.Lods[k], v.Main[k]);
                Stats.Draws += d; Stats.Triangles += t;
            }
        }
    }

    public void DrawShadows(RenderContext rc)
    {
        if (_count == 0) return;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var v in _variants) for (int k = 0; k < 3; k++) v.Shadow[k].Count = 0;
        if (rc.ShadowCascade <= 0) { Stats.ShadowInstances = 0; Stats.ShadowDraws = 0; Stats.ShadowTriangles = 0; Stats.ShadowCulledCascade = 0; Stats.ShadowSelectMs = 0; }
        if (!rc.Features.Crowd) return;
        var frustum = rc.ShadowFrustum ?? rc.Frustum;
        var cam = rc.Camera.Position;
        float m22 = rc.Projection.M22, vh = rc.ViewportSize.Y;
        float maxD2 = ShadowDistance * ShadowDistance;
        for (int i = 0; i < _count; i++)
        {
            ref readonly var a = ref _agents[i];
            if (a.Hidden) continue;
            var c = a.Position + new Vector3(0, BoundsUp, 0);
            float d2 = Vector3.DistanceSquared(cam, c);
            if (d2 > maxD2) continue;
            if (FrustumCulling && !frustum.IntersectsSphere(c, BoundsRadius)) continue;
            if (rc.CoveredByFinerCascade(c, BoundsRadius)) { Stats.ShadowCulledCascade++; continue; }
            int level = Lod.ShadowLevel(CrowdLodSettings.ProjectedPx(_height[i], MathF.Sqrt(d2), m22, vh), ForceLevel);
            var drawn = Drawn(i);
            var inst = level == 2 ? Coarse(drawn) : drawn;
            inst.Lod.X = 0; inst.Lod.Y = 0;
            _variants[a.Variant].Shadow[level].Add(inst);
        }
        Stats.ShadowSelectMs += System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        var fx = rc.Effects.Get(CrowdEffectNames.Depth);
        foreach (var v in _variants)
            for (int k = 0; k < 3; k++)
            {
                var (d, t) = DrawBatch(rc, fx, v.Lods[k], v.Shadow[k]);
                Stats.ShadowDraws += d; Stats.ShadowTriangles += t; Stats.ShadowInstances += v.Shadow[k].Count;
            }
    }

    CrowdBatch? _raw;
    /// <summary>
    /// Draws the given instances of one variant at one level as they are (no culling, no level choice): the low-level
    /// entry the tests use to pin the dither, blending and skinning, and that custom content may use.
    /// </summary>
    public void DrawRaw(RenderContext rc, int variant, int level, ReadOnlySpan<CrowdInstance> items)
    {
        _raw ??= new CrowdBatch(_gd);
        _raw.Count = 0;
        foreach (ref readonly var it in items) _raw.Add(it);
        var v = _variants[variant];
        var fx = rc.Effects.Get(rc.IsShadowPass ? CrowdEffectNames.Depth : CrowdEffectNames.Lit);
        if (!rc.IsShadowPass)
        {
            fx.Parameters["CrowdParams"].SetValue(new Vector4(rc.SampleIndex, rc.SampleCount, 0, 0));
            fx.Parameters["SkinTex"].SetValue(v.Skin);
            fx.Parameters["Emblem"].SetValue(v.Emblem);
        }
        DrawBatch(rc, fx, v.Lods[level], _raw);
    }

    /// <summary>An instance of agent-independent data for <see cref="DrawRaw"/>: world transform, two clips and a look.</summary>
    public CrowdInstance Instance(int variant, in Matrix world, int clipA, float timeA, int clipB, float timeB, float blend, CrowdLook look)
    {
        var v = _variants[variant];
        var inst = new CrowdInstance();
        inst.SetWorld(world);
        var (a0, a1, fa) = Bank.Sample(v.BankIdentity, clipA, timeA);
        var (b0, b1, fb) = Bank.Sample(v.BankIdentity, clipB, timeB);
        inst.AnimA = new Vector4(a0, a1, fa, Math.Clamp(blend, 0f, 1f));
        inst.AnimB = new Vector4(b0, b1, fb, 0);
        inst.Look = look.Packed;
        inst.Lod = new Vector4(0, 0, look.Tarnish, look.Seed);
        inst.PrevAnimA = inst.AnimA; inst.PrevAnimB = inst.AnimB;
        return inst;
    }

    (int draws, long tris) DrawBatch(RenderContext rc, Effect fx, GpuPart mesh, CrowdBatch b)
    {
        if (b.Count == 0) return (0, 0);
        b.Buffer.Set(b.Items, b.Count);
        fx.Parameters["CrowdBank"].SetValue(Bank.Texture);
        fx.Parameters["BankInfo"].SetValue(Bank.Info);
        fx.Parameters["BankSize"].SetValue(Bank.SizeParam);
        _gd.SetVertexBuffers(new VertexBufferBinding(mesh.Vertices, 0, 0), new VertexBufferBinding(b.Buffer.Buffer, 0, 1));
        _gd.Indices = mesh.Indices;
        rc.Apply(fx);
        _gd.SamplerStates[Gfx.Engine.Effects.GlslLib.BoneSlot] = SamplerState.PointClamp;
        _gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.TriangleCount, b.Count);
        long tris = (long)mesh.TriangleCount * b.Count;
        rc.Stats.RecordDraw((int)Math.Min(int.MaxValue, tris), b.Count);
        return (1, tris);
    }

    public void Dispose()
    {
        foreach (var v in _variants) for (int k = 0; k < 3; k++) { v.Main[k].Dispose(); v.Shadow[k].Dispose(); }
        _raw?.Dispose();
        foreach (var a in _ownAssets) a.Dispose();
        Bank.Dispose();
    }
}
