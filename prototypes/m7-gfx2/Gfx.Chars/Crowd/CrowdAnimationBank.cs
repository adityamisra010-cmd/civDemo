using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Chars.Crowd;

/// <summary>
/// Baked crowd animation (stage render2; PRESENTATION ONLY): every frame of every crowd clip of every identity as skin
/// matrices (character space, bind → posed) in one RGBA32F texture that the crowd vertex shader reads (vertex texture
/// fetch, slot 1). Texel (column·3·bones + 3·bone + k, row) holds column k of the bone's skin matrix
/// (M1k, M2k, M3k, M4k); a bank row is one frame. Rows are wrapped into columns of <see cref="RowsPerColumn"/> so the
/// texture stays within the HiDef size limit.
/// <para>The GPU blends two frames of each of two clips per instance (linear blend of the skin matrices, then the
/// weighted vertex blend): <see cref="SkinCpu"/> is the exact CPU reference of the shader.</para>
/// <para>The clips are the identities' CMU retargets (PROTOTYPE-ONLY, stage chars), baked frame by frame at their own
/// 30 fps with the stage-chars forward kinematics; no IK, look or foot locking (those stay with the hero path).</para>
/// </summary>
public sealed class CrowdAnimationBank : IDisposable
{
    public const int RowsPerColumn = 4096;
    /// <summary>The clips the crowd plays (the stage-chars clip set without "march", a 2-frame placeholder, and "bow").</summary>
    public static readonly string[] ClipNames =
    {
        "idle", "idle_proud", "walk", "walk_b", "jog", "run", "thrust", "swing_over", "swing_side", "block", "cower", "kneel",
        "death_fwd", "death_back", "stumble", "wounded_walk",
    };

    public int Bones { get; }
    public int Rows { get; }
    public int Columns { get; }
    public Texture2D Texture { get; }
    /// <summary>x = rows per column, y = texels per column (3 × bones), z/w unused.</summary>
    public Vector4 Info => new(RowsPerColumn, 3 * Bones, 0, 0);
    public Vector4 SizeParam => new(Texture.Width, Texture.Height, 1f / Texture.Width, 1f / Texture.Height);
    /// <summary>The CPU copy of the texture data (row-major per column; see <see cref="Texel"/>).</summary>
    public Vector4[] Data { get; }

    readonly int[][] _base;            // [identity][clip] first row
    readonly ClipDef[][] _clips;       // [identity][clip]
    public int Identities => _base.Length;

    CrowdAnimationBank(GraphicsDevice? gd, IReadOnlyList<(Skeleton skeleton, CharacterFile clips)> identities)
    {
        if (identities.Count == 0) throw new ArgumentException("no identities");
        Bones = identities[0].skeleton.Count;
        _base = new int[identities.Count][];
        _clips = new ClipDef[identities.Count][];
        int rows = 0;
        for (int i = 0; i < identities.Count; i++)
        {
            if (identities[i].skeleton.Count != Bones) throw new InvalidDataException("crowd identities must share one rig");
            _base[i] = new int[ClipNames.Length]; _clips[i] = new ClipDef[ClipNames.Length];
            for (int c = 0; c < ClipNames.Length; c++)
            {
                var clip = identities[i].clips.Clip(ClipNames[c]) ?? throw new InvalidDataException($"identity {i}: clip {ClipNames[c]} missing");
                _clips[i][c] = clip; _base[i][c] = rows; rows += clip.Frames;
            }
        }
        Rows = rows;
        Columns = (rows + RowsPerColumn - 1) / RowsPerColumn;
        int width = Columns * 3 * Bones, height = Math.Min(rows, RowsPerColumn);
        Data = new Vector4[width * height];
        var pose = new Pose(Bones);
        var global = new Quaternion[Bones]; var heads = new Vector3[Bones]; var skin = new Matrix[Bones];
        for (int i = 0; i < identities.Count; i++)
        {
            var sk = identities[i].skeleton;
            int pelvis = sk.TryIndex("pelvis", out int p) ? p : -1;
            for (int c = 0; c < ClipNames.Length; c++)
            {
                var clip = _clips[i][c];
                for (int f = 0; f < clip.Frames; f++)
                {
                    pose.Reset();
                    int nb = Math.Min(clip.Bones, Bones);
                    for (int b = 0; b < nb; b++) pose.Local[b] = clip.Rotations[f * clip.Bones + b];
                    pose.Pelvis = clip.Pelvis[f];
                    Fk.Solve(sk, pose, global, heads, pelvis);
                    Fk.SkinMatrices(sk, global, heads, Matrix.Identity, skin);
                    int row = _base[i][c] + f;
                    for (int b = 0; b < Bones; b++)
                    {
                        ref readonly var m = ref skin[b];
                        Data[Index(row, b, 0, width)] = new Vector4(m.M11, m.M21, m.M31, m.M41);
                        Data[Index(row, b, 1, width)] = new Vector4(m.M12, m.M22, m.M32, m.M42);
                        Data[Index(row, b, 2, width)] = new Vector4(m.M13, m.M23, m.M33, m.M43);
                    }
                }
            }
        }
        if (gd != null)
        {
            Texture = new Texture2D(gd, width, height, false, SurfaceFormat.Vector4);
            Texture.SetData(Data);
        }
        else Texture = null!;
    }

    int Index(int row, int bone, int k, int width)
    {
        int col = row / RowsPerColumn, r = row % RowsPerColumn;
        return r * width + col * 3 * Bones + bone * 3 + k;
    }

    /// <summary>Bakes the bank for the given identities (all must share the rig's bone order) and uploads it.</summary>
    public static CrowdAnimationBank Build(GraphicsDevice gd, IReadOnlyList<(Skeleton skeleton, CharacterFile clips)> identities) => new(gd, identities);

    /// <summary>CPU-only bank (tests): no texture.</summary>
    public static CrowdAnimationBank BuildCpu(IReadOnlyList<(Skeleton skeleton, CharacterFile clips)> identities) => new(null, identities);

    public ClipDef Clip(int identity, int clip) => _clips[identity][clip];
    public int FirstRow(int identity, int clip) => _base[identity][clip];
    public static int ClipIndex(string name) => Array.IndexOf(ClipNames, name);

    /// <summary>
    /// Rows and fraction for clip-local time <paramref name="t"/> (s): looping clips wrap, one-shot clips clamp to their
    /// last frame (the same rule as <see cref="ClipSampler.Frames"/>).
    /// </summary>
    public (int row0, int row1, float frac) Sample(int identity, int clip, float t)
    {
        var c = _clips[identity][clip];
        var (f0, f1, a) = ClipSampler.Frames(c, t);
        int b = _base[identity][clip];
        return (b + f0, b + f1, a);
    }

    /// <summary>Column k (0..2) of the skin matrix of <paramref name="bone"/> in bank row <paramref name="row"/>.</summary>
    public Vector4 Texel(int row, int bone, int k) => Data[Index(row, bone, k, Columns * 3 * Bones)];

    /// <summary>
    /// CPU reference of the crowd vertex shader's skinning: per influence, the skin matrix columns are lerped between the
    /// two frames of clip A (and of clip B when its weight is above 0), clip A and B are lerped by the weight, and the
    /// bind-space position is transformed and weighted. Returns the character-space position.
    /// </summary>
    public Vector3 SkinCpu(Vector3 bindPos, ReadOnlySpan<int> joints, ReadOnlySpan<float> weights, Vector4 animA, Vector4 animB)
    {
        var p = new Vector4(bindPos, 1);
        Vector3 sum = Vector3.Zero;
        Span<Vector4> col = stackalloc Vector4[3];
        for (int i = 0; i < 4; i++)
        {
            float w = weights[i];
            if (w <= 0) continue;
            for (int k = 0; k < 3; k++)
            {
                var a = Vector4.Lerp(Texel((int)animA.X, joints[i], k), Texel((int)animA.Y, joints[i], k), animA.Z);
                if (animA.W > 0)
                {
                    var b = Vector4.Lerp(Texel((int)animB.X, joints[i], k), Texel((int)animB.Y, joints[i], k), animB.Z);
                    a = Vector4.Lerp(a, b, animA.W);
                }
                col[k] = a;
            }
            sum += w * new Vector3(Vector4.Dot(col[0], p), Vector4.Dot(col[1], p), Vector4.Dot(col[2], p));
        }
        return sum;
    }

    public void Dispose() => Texture?.Dispose();
}
