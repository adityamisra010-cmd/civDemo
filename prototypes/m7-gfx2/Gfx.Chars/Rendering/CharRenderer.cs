using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Rendering;

/// <summary>
/// Draws character instances: fills the bone palette once per frame, then per part sets the material's effect and
/// textures, binds the vertex stream plus the morph stream and issues one indexed draw. Shadow passes use the depth
/// variant per cascade (frustum-culled per cascade).
/// </summary>
public sealed class CharRenderer : IDisposable
{
    readonly GraphicsDevice _gd;
    BonePalette? _palette;
    Texture2D? _flatNormal, _black, _white;
    public CharMaterials Materials { get; } = new();

    public CharRenderer(GraphicsDevice gd) { _gd = gd; }

    Texture2D Solid(ref Texture2D? t, Color c)
    {
        if (t == null) { t = new Texture2D(_gd, 1, 1, false, SurfaceFormat.Color); t.SetData(new[] { c }); }
        return t;
    }
    public Texture2D FlatNormal => Solid(ref _flatNormal, new Color(128, 128, 255, 255));
    public Texture2D Black => Solid(ref _black, new Color(0, 0, 0, 0));
    public Texture2D White => Solid(ref _white, Color.White);

    /// <summary>Solves every instance's skin and morphs and uploads the palette. Call once per frame before drawing.</summary>
    public void Prepare(IReadOnlyList<CharInstance> instances)
    {
        int bones = instances.Count > 0 ? instances[0].Asset.Skeleton.Count : 1;
        if (_palette == null || _palette.Bones != bones) { _palette?.Dispose(); _palette = new BonePalette(_gd, bones, Math.Max(4, instances.Count)); }
        _palette.Reset();
        foreach (var inst in instances)
        {
            inst.SolvePose();
            inst.UpdateMorphs(_gd);
            inst.PaletteRow = _palette.Add(inst.Skin);
        }
        _palette.Upload();
    }

    /// <summary>Changes every opaque pass (each accumulated still sample): seeds the hashed alpha test.</summary>
    public float FrameSeed { get; private set; }
    int _frame;

    public void DrawShadows(RenderContext rc, IReadOnlyList<CharInstance> instances)
    {
        if (_palette == null) return;
        var fx = rc.Effects.Get(CharEffectNames.Depth);
        var fxa = rc.Effects.Get(CharEffectNames.DepthAlpha);
        var frustum = rc.ShadowFrustum ?? rc.Frustum;
        foreach (var inst in instances)
        {
            { var bs = inst.Bounds; if (!frustum.IntersectsSphere(bs.Center, bs.Radius)) continue; }
            foreach (var part in inst.AllParts())
            {
                if (inst.HiddenParts.Contains(part.Name) || !Materials.CastsShadow(part.Material)) continue;
                if (CharMaterials.AlphaTested(part.Material))
                {
                    fxa.Parameters["HairTex"].SetValue(inst.Asset.Textures.TryGetValue("hair", out var ht) ? ht : White);
                    Draw(rc, fxa, inst, part, depth: true);
                }
                else Draw(rc, fx, inst, part, depth: true);
            }
        }
    }

    public void DrawOpaque(RenderContext rc, IReadOnlyList<CharInstance> instances)
    {
        if (_palette == null) return;
        _late.Clear();
        _frame++;
        FrameSeed = (_frame * 0.618034f) % 1f * 97f;
        foreach (var inst in instances)
        {
            { var bs = inst.Bounds; if (!rc.Frustum.IntersectsSphere(bs.Center, bs.Radius)) continue; }
            foreach (var part in inst.AllParts())
            {
                if (inst.HiddenParts.Contains(part.Name)) continue;
                if (CharMaterials.IsAdditive(part.Material)) { _late.Add((inst, part)); continue; }
                var fx = Materials.Setup(this, rc, inst, part);
                if (fx == null) continue;
                Draw(rc, fx, inst, part, depth: false);
            }
        }
        // additive specular layers (cornea): after every opaque part, depth-tested, no depth writes
        if (_late.Count == 0) return;
        var bsPrev = _gd.BlendState; var dsPrev = _gd.DepthStencilState;
        _gd.BlendState = BlendState.Additive; _gd.DepthStencilState = DepthStencilState.DepthRead;
        foreach (var (inst, part) in _late)
        {
            var fx = Materials.Setup(this, rc, inst, part);
            if (fx != null) Draw(rc, fx, inst, part, depth: false);
        }
        _gd.BlendState = bsPrev; _gd.DepthStencilState = dsPrev;
    }

    readonly List<(CharInstance, GpuPart)> _late = new();

    void Draw(RenderContext rc, Effect fx, CharInstance inst, GpuPart part, bool depth)
    {
        var owner = inst.OwnerOf(part);
        fx.Parameters["CharBones"].SetValue(_palette!.Texture);
        fx.Parameters["CharBoneSize"].SetValue(_palette.SizeParam);
        fx.Parameters["BoneRow"].SetValue(new Vector4(inst.PaletteRow, 0, 0, 0));
        rc.Apply(fx);
        _gd.SamplerStates[1] = SamplerState.PointClamp;
        _gd.SetVertexBuffers(new VertexBufferBinding(part.Vertices), new VertexBufferBinding(inst.MorphBuffer(part, owner)));
        IndexBuffer ib = part.Indices; int tris = part.TriangleCount;
        if (inst.PartViews.TryGetValue(part.Name, out var view) && part.Views.TryGetValue(view, out var v)) { ib = v.ib; tris = v.tris; }
        if (tris == 0) return;
        _gd.Indices = ib;
        _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, tris);
        rc.Stats.RecordDraw(tris, 1);
    }

    public void Dispose() { _palette?.Dispose(); _flatNormal?.Dispose(); _black?.Dispose(); _white?.Dispose(); }
}
