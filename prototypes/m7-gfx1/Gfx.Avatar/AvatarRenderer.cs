using Gfx.Engine.Effects;
using Gfx.Engine.Rendering;
using Gfx.Units;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Avatar;

/// <summary>
/// CPU-updated VertexPNC mesh drawn through the engine's instanced effects with ONE instance (world + tint).
/// Used for the hero characters (CPU skinning: two characters per scene, ~5k vertices each — GPU instanced
/// skinning is lane U's path for crowds), the banner cloth and the war-record card.
/// </summary>
public sealed class DynamicMesh : IDisposable
{
    readonly DynamicVertexBuffer _vb; readonly IndexBuffer _ib; readonly InstanceBuffer<InstanceTransform> _inst;
    readonly InstanceTransform[] _one = new InstanceTransform[1];
    public VertexPNC[] Vertices { get; }
    public int TriangleCount { get; }

    public DynamicMesh(GraphicsDevice gd, int vertexCount, int[] indices)
    {
        Vertices = new VertexPNC[vertexCount];
        _vb = new DynamicVertexBuffer(gd, typeof(VertexPNC), vertexCount, BufferUsage.WriteOnly);
        _ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly); _ib.SetData(indices);
        _inst = new InstanceBuffer<InstanceTransform>(gd, 1);
        TriangleCount = indices.Length / 3;
        SetWorld(Matrix.Identity, Vector4.One);
    }

    public void Upload() => _vb.SetData(Vertices, 0, Vertices.Length, SetDataOptions.Discard);
    public void SetWorld(in Matrix world, Vector4 tint) { _one[0] = InstanceTransform.From(world, tint); _inst.Set(_one); }

    public void Draw(RenderContext rc, Effect effect)
    {
        var gd = rc.Device;
        gd.SetVertexBuffers(new VertexBufferBinding(_vb, 0, 0), new VertexBufferBinding(_inst.Buffer, 0, 1));
        gd.Indices = _ib;
        rc.Apply(effect);
        gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, TriangleCount, 1);
        rc.Stats.RecordDraw(TriangleCount, 1);
    }

    public void Dispose() { _vb.Dispose(); _ib.Dispose(); _inst.Dispose(); }
}

/// <summary>An <see cref="AvatarModel"/> posed on the CPU (skin matrices from <see cref="Skeleton.SkinMatrices"/>).</summary>
public sealed class PosedAvatar : IDisposable
{
    readonly DynamicMesh _mesh;
    public AvatarModel Model { get; }

    public PosedAvatar(GraphicsDevice gd, AvatarModel model)
    {
        Model = model;
        _mesh = new DynamicMesh(gd, model.Mesh.Vertices.Length, model.Mesh.Indices);
        SetPose(Pose.Rest(), Matrix.Identity);
    }

    /// <summary>Skins every vertex with <paramref name="pose"/> and places the figure (model scale applied first).</summary>
    public void SetPose(in Pose pose, in Matrix world, float groundWorldY = 0f)
    {
        var placed = SkinPlaced(Model, pose, world, _mesh.Vertices, groundWorldY);
        _mesh.Upload();
        _mesh.SetWorld(placed, Vector4.One);
    }

    /// <summary>
    /// Skins <paramref name="model"/> into <paramref name="dst"/> (model space) and returns its model-to-world matrix.
    /// Outfit vertices (index ≥ <see cref="AvatarModel.IdentityVertexEnd"/>) are draped on the ground plane y = <paramref name="groundWorldY"/> (default 0): a
    /// rigid skirt or robe skinned to the pelvis would otherwise pass through the ground when the wearer kneels or
    /// falls. PLACEHOLDER for cloth simulation. The body itself is never clamped — clips must keep it above ground.
    /// Assumes the world transform keeps +Y up (yaw + translation, as staged characters are placed).
    /// </summary>
    public static Matrix SkinPlaced(AvatarModel model, in Pose pose, in Matrix world, VertexPNC[] dst, float groundWorldY = 0f)
    {
        Skin(model.Mesh.Vertices, Skeleton.SkinMatrices(pose), dst);
        var m = Matrix.CreateScale(model.Scale) * world;
        float groundY = (groundWorldY + 0.005f - m.M42) / m.M22;
        for (int i = model.IdentityVertexEnd; i < dst.Length; i++)
            if (dst[i].Position.Y < groundY) dst[i].Position.Y = groundY;
        return m;
    }

    /// <summary>CPU linear-blend skinning (same maths as <c>BoneTexture.SkinCpu</c> at blend 0).</summary>
    public static void Skin(VertexSkinned[] src, Matrix[] skin, VertexPNC[] dst)
    {
        for (int i = 0; i < src.Length; i++)
        {
            ref readonly var v = ref src[i];
            var j = v.Joints.ToVector4(); var w = v.Weights;
            Vector3 p, n;
            if (w.X >= 1f) { ref readonly var m = ref skin[(int)j.X]; p = Vector3.Transform(v.Position, m); n = Vector3.TransformNormal(v.Normal, m); }
            else
            {
                p = Vector3.Zero; n = Vector3.Zero;
                Acc(skin, (int)j.X, w.X, v, ref p, ref n); Acc(skin, (int)j.Y, w.Y, v, ref p, ref n);
                Acc(skin, (int)j.Z, w.Z, v, ref p, ref n); Acc(skin, (int)j.W, w.W, v, ref p, ref n);
            }
            dst[i] = new VertexPNC(p, n, v.Color);
        }
    }

    static void Acc(Matrix[] skin, int j, float w, in VertexSkinned v, ref Vector3 p, ref Vector3 n)
    {
        if (w <= 0) return;
        p += w * Vector3.Transform(v.Position, skin[j]); n += w * Vector3.TransformNormal(v.Normal, skin[j]);
    }

    public void DrawShadow(RenderContext rc) => _mesh.Draw(rc, rc.Effects.Get(EffectNames.InstancedDepth));
    public void Draw(RenderContext rc) => _mesh.Draw(rc, rc.Effects.Get(EffectNames.InstancedLit));
    public void Dispose() => _mesh.Dispose();
}
