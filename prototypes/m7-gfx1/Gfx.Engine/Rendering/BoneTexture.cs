using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

/// <summary>
/// Baked bone-matrix texture for instanced GPU skinning (<c>SkinnedLit</c>/<c>SkinnedDepth</c>).
/// Layout: SurfaceFormat.Vector4 (RGBA32F), width = joints × 3, height = pose rows (all clips' frames stacked).
/// Texel (3j+k, row) = column k (k=0..2) of joint j's SKIN matrix (inverse-bind × joint model transform, XNA
/// row-vector convention): (M1k, M2k, M3k, M4k). Lane U owns clip baking; this type only packs and uploads.
/// Bind with <c>RenderContext.BindBones(tex)</c> before drawing (slot 1, point-clamp, VTF workaround).
/// </summary>
public sealed class BoneTexture : IDisposable
{
    public Texture2D Texture { get; }
    public int Joints { get; }
    public int Rows { get; }
    public Vector4 SizeParam => new(Texture.Width, Texture.Height, 1f / Texture.Width, 1f / Texture.Height);

    BoneTexture(Texture2D t, int joints, int rows) { Texture = t; Joints = joints; Rows = rows; }

    /// <summary>poses[row][joint] → texel data (row-major, width = joints*3).</summary>
    public static Vector4[] Pack(IReadOnlyList<Matrix[]> poses, int joints)
    {
        var data = new Vector4[joints * 3 * poses.Count];
        for (int r = 0; r < poses.Count; r++)
        {
            var pose = poses[r];
            if (pose.Length != joints) throw new ArgumentException($"pose row {r} has {pose.Length} joints, expected {joints}");
            for (int j = 0; j < joints; j++)
            {
                ref readonly var m = ref pose[j]; int o = r * joints * 3 + j * 3;
                data[o] = new Vector4(m.M11, m.M21, m.M31, m.M41);
                data[o + 1] = new Vector4(m.M12, m.M22, m.M32, m.M42);
                data[o + 2] = new Vector4(m.M13, m.M23, m.M33, m.M43);
            }
        }
        return data;
    }

    public static BoneTexture Create(GraphicsDevice gd, IReadOnlyList<Matrix[]> poses, int joints)
    {
        var tex = new Texture2D(gd, joints * 3, poses.Count, false, SurfaceFormat.Vector4);
        tex.SetData(Pack(poses, joints));
        return new BoneTexture(tex, joints, poses.Count);
    }

    /// <summary>CPU reference of exactly what the shader does (matrix lerp between rows, then weighted blend).</summary>
    public static (Vector3 pos, Vector3 nrm) SkinCpu(in VertexSkinned v, Matrix[] poseA, Matrix[] poseB, float blend)
    {
        Vector3 p = Vector3.Zero, n = Vector3.Zero;
        var jv = v.Joints.ToVector4();
        ReadOnlySpan<int> j = stackalloc int[] { (int)jv.X, (int)jv.Y, (int)jv.Z, (int)jv.W };
        ReadOnlySpan<float> w = stackalloc float[] { v.Weights.X, v.Weights.Y, v.Weights.Z, v.Weights.W };
        for (int i = 0; i < 4; i++)
        {
            if (w[i] <= 0) continue;
            var m = Matrix.Lerp(poseA[j[i]], poseB[j[i]], blend);
            p += w[i] * Vector3.Transform(v.Position, m);
            n += w[i] * Vector3.TransformNormal(v.Normal, m);
        }
        return (p, n);
    }

    public void Dispose() => Texture.Dispose();
}
