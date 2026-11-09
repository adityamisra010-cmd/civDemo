using Gfx.Engine.Cameras;
using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;

namespace Gfx.Demo.Scenes;

/// <summary>
/// Lane E acceptance scene: terrain-less lit test scene — ground plane, ~3k instanced boxes in shadowed clusters,
/// and a row of instanced GPU-skinned two-joint bars bending via the bone texture (VTF path in the real pipeline).
/// </summary>
[DemoScene("engine-instanced", "Lane E: instanced shadowed boxes + GPU-skinned bars on a ground plane")]
public sealed class EngineInstancedScene : DemoScene
{
    GpuMesh _ground = null!, _box = null!, _bar = null!;
    InstanceBuffer<InstanceTransform> _groundInst = null!, _boxAll = null!, _boxVisible = null!;
    InstanceBuffer<SkinnedInstance> _bars = null!;
    InstanceTransform[] _boxes = Array.Empty<InstanceTransform>(), _visibleScratch = Array.Empty<InstanceTransform>();
    Vector4[] _spheres = Array.Empty<Vector4>(); int[] _visibleIdx = Array.Empty<int>();
    SkinnedInstance[] _barData = Array.Empty<SkinnedInstance>();
    BoneTexture _bones = null!;
    public const int PoseRows = 16;
    public int BoxCount => _boxes.Length;
    public int LastVisibleBoxes { get; private set; }

    static uint Hash(uint x) { unchecked { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; } }
    static float U(uint i, uint salt) => (Hash(i * 0x9E3779B9u + salt) & 0xFFFFFF) / 16777215f;

    protected override void OnLoad()
    {
        var gd = Device;
        _ground = GpuMesh.Create(gd, Primitives.Plane(600, 32, new Color(118, 128, 92)));
        _box = GpuMesh.Create(gd, Primitives.Box(new Vector3(0, 1, 0), Vector3.One, new Color(200, 200, 200)));
        _bar = GpuMesh.Create(gd, Primitives.SkinnedBar(new Color(225, 205, 160)));
        _groundInst = new InstanceBuffer<InstanceTransform>(gd, 1);
        _groundInst.Set(new[] { InstanceTransform.From(Matrix.Identity) });

        // clusters of boxes ("buildings"/"crates") — deterministic hash placement, presentation only
        var list = new List<InstanceTransform>(); var spheres = new List<Vector4>();
        var palette = new[] { new Vector4(0.85f, 0.35f, 0.25f, 1), new Vector4(0.30f, 0.45f, 0.80f, 1), new Vector4(0.90f, 0.80f, 0.45f, 1), new Vector4(0.55f, 0.75f, 0.45f, 1), new Vector4(0.8f, 0.8f, 0.8f, 1) };
        for (uint c = 0; c < 24; c++)
        {
            float cx = (U(c, 1) - 0.5f) * 420f, cz = (U(c, 2) - 0.5f) * 420f;
            for (uint k = 0; k < 120; k++)
            {
                uint id = c * 1000 + k;
                float r = 4f + U(id, 3) * 26f, a = U(id, 4) * MathHelper.TwoPi;
                float sx = 0.6f + U(id, 5) * 2.4f, sz = 0.6f + U(id, 6) * 2.4f, sy = 0.5f + U(id, 7) * U(id, 8) * 9f;
                var pos = new Vector3(cx + MathF.Cos(a) * r, 0, cz + MathF.Sin(a) * r);
                var m = Matrix.CreateScale(sx, sy, sz) * Matrix.CreateRotationY(U(id, 9) * MathHelper.Pi) * Matrix.CreateTranslation(pos);
                list.Add(InstanceTransform.From(m, palette[(int)(Hash(id) % (uint)palette.Length)] * (0.8f + 0.2f * U(id, 10))));
                spheres.Add(new Vector4(pos + new Vector3(0, sy, 0), new Vector3(sx, sy, sz).Length() * 1.0f));
            }
        }
        _boxes = list.ToArray(); _spheres = spheres.ToArray();
        _visibleIdx = new int[_boxes.Length]; _visibleScratch = new InstanceTransform[_boxes.Length];
        _boxAll = new InstanceBuffer<InstanceTransform>(gd, _boxes.Length); _boxAll.Set(_boxes);
        _boxVisible = new InstanceBuffer<InstanceTransform>(gd, _boxes.Length);

        // two-joint bone texture: row r bends joint 1 by angle(r) about Z around the pivot y=1
        var poses = new List<Matrix[]>();
        for (int r = 0; r < PoseRows; r++)
        {
            float ang = MathHelper.ToRadians(75f) * MathF.Sin(r / (float)PoseRows * MathHelper.TwoPi);
            poses.Add(new[] { Matrix.Identity, Matrix.CreateTranslation(0, -1, 0) * Matrix.CreateRotationZ(ang) * Matrix.CreateTranslation(0, 1, 0) });
        }
        _bones = BoneTexture.Create(gd, poses, 2);
        _barData = new SkinnedInstance[40];
        _bars = new InstanceBuffer<SkinnedInstance>(gd, _barData.Length);
        Update(0);
    }

    public override void Update(float t)
    {
        for (int i = 0; i < _barData.Length; i++)
        {
            float phase = t * 6f + i * 0.9f;
            int row = (int)MathF.Floor(phase) % PoseRows; float blend = phase - MathF.Floor(phase);
            var m = Matrix.CreateScale(3f) * Matrix.CreateTranslation(-60 + i * 3f, 0, 40);
            _barData[i] = SkinnedInstance.From(m, new Vector4(0.9f, 0.9f - i * 0.01f, 0.8f, 1), row, (row + 1) % PoseRows, blend);
        }
        _bars.Set(_barData);
    }

    public override IReadOnlyList<CaptureShot> Shots => new[]
    {
        new CaptureShot("day", new CameraPose(new Vector3(-150, 95, 190), new Vector3(-20, 0, 20)), 10.5f, 0.4f),
        new CaptureShot("dusk", new CameraPose(new Vector3(140, 60, 160), new Vector3(-10, 0, 0)), 18.4f, 1.1f),
        new CaptureShot("closeup", new CameraPose(new Vector3(-34, 8, 62), new Vector3(-14, 3, 40)), 15f, 0.7f),
    };

    public override Vector3 Focus => new(-20, 0, 20);
    public override float ShadowRadius => 220f;

    public override void DrawShadowCasters(RenderContext rc)
    {
        var depth = rc.Effects.Get(EffectNames.InstancedDepth);
        _box.DrawInstanced(rc, depth, _boxAll);
        rc.BindBones(_bones);
        _bar.DrawInstanced(rc, rc.Effects.Get(EffectNames.SkinnedDepth), _bars);
    }

    public override void DrawOpaque(RenderContext rc)
    {
        var lit = rc.Effects.Get(EffectNames.InstancedLit);
        _ground.DrawInstanced(rc, lit, _groundInst);
        int n = rc.Frustum.CullSpheres(_spheres, _visibleIdx);
        for (int i = 0; i < n; i++) _visibleScratch[i] = _boxes[_visibleIdx[i]];
        _boxVisible.Set(_visibleScratch, n);
        LastVisibleBoxes = n;
        _box.DrawInstanced(rc, lit, _boxVisible);
        rc.BindBones(_bones);
        _bar.DrawInstanced(rc, rc.Effects.Get(EffectNames.SkinnedLit), _bars);
    }

    public override void Dispose()
    {
        foreach (var d in new IDisposable[] { _ground, _box, _bar, _groundInst, _boxAll, _boxVisible, _bars, _bones }) d?.Dispose();
    }
}
