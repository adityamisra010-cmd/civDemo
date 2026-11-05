using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Units;

/// <summary>
/// Instanced, GPU-skinned crowd renderer with animation LOD (thresholds are camera distances, metres):
/// <list type="bullet">
/// <item>LOD0 &lt; <see cref="Lod0Distance"/>: 6-sided body + every equipment part; animation blended between baked
/// frames every frame (full rate).</item>
/// <item>LOD1 &lt; <see cref="Lod1Distance"/>: 4-sided body + silhouette parts only (shields, spear, bow, musket);
/// animation snapped to baked frames and advanced at <see cref="Lod1Hz"/> Hz (reduced rate).</item>
/// <item>LOD2 beyond: rigid 3-box proxy through the engine's <c>InstancedLit</c> (no skinning; dead soldiers lie flat,
/// standing otherwise) — the impostor slot; real impostor cards are a follow-up.</item>
/// </list>
/// Frustum culling per soldier (bounding sphere 1.3 m). The shadow pass reuses the camera-culled set (soldiers off
/// screen cast no shadow — accepted for the prototype). Draw calls: ≤ 1 + parts per LOD per pass.
/// </summary>
public sealed class SoldierCrowd : IDisposable
{
    public float Lod0Distance { get; set; } = 40f;
    public float Lod1Distance { get; set; } = 120f;
    public float Lod1Hz { get; set; } = 10f;

    readonly GpuMesh _body0, _body1, _proxy;
    readonly GpuMesh[] _parts;
    readonly BoneTexture _bones;
    readonly InstanceBuffer<SkinnedInstance> _ib0, _ib1;
    readonly InstanceBuffer<SkinnedInstance>[] _partIb0, _partIb1;
    readonly InstanceBuffer<InstanceTransform> _ibProxy;
    SkinnedInstance[] _l0 = Array.Empty<SkinnedInstance>(), _l1 = Array.Empty<SkinnedInstance>();
    InstanceTransform[] _l2 = Array.Empty<InstanceTransform>();
    SkinnedInstance[][] _p0, _p1;
    int _n0, _n1, _n2; int[] _np0, _np1;

    public int Lod0Count => _n0;
    public int Lod1Count => _n1;
    public int Lod2Count => _n2;
    public int Culled { get; private set; }
    public BoneTexture Bones => _bones;
    public int Lod0BodyTriangles => _body0.TriangleCount;

    public SoldierCrowd(GraphicsDevice gd)
    {
        _body0 = GpuMesh.Create(gd, SoldierMeshes.Body(6));
        _body1 = GpuMesh.Create(gd, SoldierMeshes.Body(4));
        _proxy = GpuMesh.Create(gd, SoldierMeshes.Proxy());
        _parts = new GpuMesh[SoldierMeshes.PartCount];
        for (int i = 0; i < _parts.Length; i++) _parts[i] = GpuMesh.Create(gd, SoldierMeshes.Equipment((Part)i));
        _bones = ClipLibrary.Bake(gd);
        _ib0 = new(gd); _ib1 = new(gd); _ibProxy = new(gd);
        _partIb0 = new InstanceBuffer<SkinnedInstance>[_parts.Length]; _partIb1 = new InstanceBuffer<SkinnedInstance>[_parts.Length];
        _p0 = new SkinnedInstance[_parts.Length][]; _p1 = new SkinnedInstance[_parts.Length][];
        _np0 = new int[_parts.Length]; _np1 = new int[_parts.Length];
        for (int i = 0; i < _parts.Length; i++)
        {
            _partIb0[i] = new(gd); _partIb1[i] = new(gd);
            _p0[i] = Array.Empty<SkinnedInstance>(); _p1[i] = Array.Empty<SkinnedInstance>();
        }
    }

    static void Push<T>(ref T[] a, ref int n, in T v)
    {
        if (n == a.Length) Array.Resize(ref a, Math.Max(64, a.Length * 2));
        a[n++] = v;
    }

    /// <summary>Selects LOD, culls and uploads instances for this frame. Call once per frame before drawing.</summary>
    public void Prepare(ReadOnlySpan<SoldierFrame> soldiers, Vector3 cameraPos, in Gfx.Engine.Scene.Frustum frustum)
    {
        _n0 = _n1 = _n2 = 0; Culled = 0;
        Array.Clear(_np0); Array.Clear(_np1);
        float d0 = Lod0Distance * Lod0Distance, d1 = Lod1Distance * Lod1Distance;
        foreach (ref readonly var s in soldiers)
        {
            if (!s.Visible) continue;
            if (!frustum.IntersectsSphere(s.Position + new Vector3(0, 0.9f, 0), 1.3f)) { Culled++; continue; }
            float dsq = Vector3.DistanceSquared(s.Position, cameraPos);
            var world = s.Look.Scale * Matrix.CreateRotationY(s.Yaw) * Matrix.CreateTranslation(s.Position);
            var tint = s.Look.Tint(s.Side);
            if (dsq >= d1)
            {
                var m = s.Clip == ClipId.Death && ClipLibrary.Phase(ClipId.Death, s.ClipTime) > 0.6f
                    ? s.Look.Scale * Matrix.CreateRotationX(-MathF.PI / 2) * Matrix.CreateTranslation(0, 0.12f, 0) * Matrix.CreateRotationY(s.Yaw) * Matrix.CreateTranslation(s.Position)
                    : world;
                // InstancedLit multiplies a LINEAR tint: convert the sRGB cloth colour (proxy vertex colour ≈ 0.83 linear)
                var lin = new Vector3(MathF.Pow(tint.X, 2.2f), MathF.Pow(tint.Y, 2.2f), MathF.Pow(tint.Z, 2.2f)) * 1.2f;
                Push(ref _l2, ref _n2, InstanceTransform.From(m, new Vector4(lin, 1)));
                continue;
            }
            bool near = dsq < d0;
            var (ra, rb, w) = near ? ClipLibrary.Sample(s.Clip, s.ClipTime)
                                   : ClipLibrary.SampleStepped(s.Clip, MathF.Floor(s.ClipTime * Lod1Hz) / Lod1Hz);
            var inst = SkinnedInstance.From(world, tint, ra, rb, w);
            if (near) Push(ref _l0, ref _n0, inst); else Push(ref _l1, ref _n1, inst);
            if (s.Look.Helmet >= 0 && near) Push(ref _p0[s.Look.Helmet], ref _np0[s.Look.Helmet], inst);
            foreach (var p in SoldierLook.KitParts(s.Look.Kit))
            {
                if (near) Push(ref _p0[(int)p], ref _np0[(int)p], inst);
                else if (SoldierLook.InLod1(p)) Push(ref _p1[(int)p], ref _np1[(int)p], inst);
            }
        }
        _ib0.Set(_l0, _n0); _ib1.Set(_l1, _n1); _ibProxy.Set(_l2, _n2);
        for (int i = 0; i < _parts.Length; i++) { _partIb0[i].Set(_p0[i], _np0[i]); _partIb1[i].Set(_p1[i], _np1[i]); }
    }

    public void Draw(RenderContext rc)
    {
        var skinned = rc.Effects.Get(rc.IsShadowPass ? EffectNames.SkinnedDepth : UnitEffects.UnitSkinnedLit);
        rc.BindBones(_bones);
        _body0.DrawInstanced(rc, skinned, _ib0);
        _body1.DrawInstanced(rc, skinned, _ib1);
        for (int i = 0; i < _parts.Length; i++)
        {
            _parts[i].DrawInstanced(rc, skinned, _partIb0[i]);
            _parts[i].DrawInstanced(rc, skinned, _partIb1[i]);
        }
        _proxy.DrawInstanced(rc, rc.Effects.Get(rc.IsShadowPass ? EffectNames.InstancedDepth : EffectNames.InstancedLit), _ibProxy);
    }

    public void Dispose()
    {
        _body0.Dispose(); _body1.Dispose(); _proxy.Dispose(); _bones.Dispose();
        _ib0.Dispose(); _ib1.Dispose(); _ibProxy.Dispose();
        foreach (var p in _parts) p.Dispose();
        foreach (var b in _partIb0) b.Dispose();
        foreach (var b in _partIb1) b.Dispose();
    }
}
