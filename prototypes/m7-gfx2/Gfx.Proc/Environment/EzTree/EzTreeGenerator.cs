// C# port of ez-tree's generator (src/lib/tree.js: skeleton growth and meshing), MIT,
// Copyright (c) 2024 Daniel Greenheck (github.com/dgreenheck/ez-tree @ dcf309bd86bd521083d9c70f01f2de45fdc7c457).
// Ported: #generateSkeleton, #growBranch, generateChildBranches, generateLeaves, #recordLeaf, shuffledIndices,
// #meshSkeleton, #meshBranch, #meshLeaf, in the original's order of random-number use. Not ported: the trellis force
// (no preset enables it), materials, three.js meshes and LOD objects (the G2 renderer has its own). The port adds
// per-vertex extras (branch level, position along the branch, bark tangent, leaf anchor) in separate lists; they do
// not change the ported outputs. PRESENTATION ONLY: tree shapes are decoration and never reach simulation state.
namespace Gfx.Proc.Environment.EzTree;

public struct EzSection
{
    public Vec3d Origin;
    public EulerXyz Orientation;
    public double Radius;
}

/// <summary>One grown branch: its section frames (ring centres), segment count and base radius.</summary>
public sealed class EzSkeletonBranch
{
    public required List<EzSection> Sections { get; init; }
    public required double SegmentCount { get; init; }
    public required double BaseRadius { get; init; }
    /// <summary>Branch level (0 = trunk). Extra: not part of the JavaScript skeleton record.</summary>
    public required int Level { get; init; }
}

public readonly record struct EzLeaf(Vec3d Origin, EulerXyz Orientation, double Size);

public sealed class EzSkeleton
{
    public List<EzSkeletonBranch> Branches { get; } = new();
    public List<EzLeaf> Leaves { get; } = new();
}

/// <summary>
/// Meshing detail (ez-tree's LODDetail): sample every Nth section ring, scale the radial segment count, keep every Nth
/// leaf and scale the kept leaves, override the billboard mode.
/// </summary>
public sealed record EzDetail(int SectionStride = 1, double SegmentFactor = 1, int LeafStride = 1, double LeafScale = 1, EzBillboard? Billboard = null)
{
    public static readonly EzDetail Full = new();
    /// <summary>ez-tree's <c>defaultLODLevels[1]</c> (≈40 % of the full triangle count).</summary>
    public static readonly EzDetail Lod1 = new(3, 0.75, 2, 1.25);
    /// <summary>ez-tree's <c>defaultLODLevels[2]</c> (≈20 %).</summary>
    public static readonly EzDetail Lod2 = new(6, 0.4, 2, 1.3, EzBillboard.Single);
}

/// <summary>Raw geometry buffers, laid out exactly as ez-tree's (xyz triples, uv pairs, triangle indices).</summary>
public sealed class EzBuffers
{
    public List<double> Verts { get; } = new();
    public List<double> Normals { get; } = new();
    public List<double> Uvs { get; } = new();
    public List<int> Indices { get; } = new();
    // ---- extras (not in the JavaScript buffers)
    /// <summary>Branches: level of the branch the vertex belongs to. Leaves: level of the leaf's branch.</summary>
    public List<int> Level { get; } = new();
    /// <summary>Branches: 0..1 along the branch. Leaves: 0 at the stem end, 1 at the tip of the card.</summary>
    public List<double> Along { get; } = new();
    /// <summary>Branches: unit tangent around the ring (direction of increasing u), xyz triples.</summary>
    public List<double> Tangents { get; } = new();
    /// <summary>Leaves: the leaf's attachment point (xyz triples, one per vertex).</summary>
    public List<double> Anchors { get; } = new();
    /// <summary>Branches: distance along the branch from its base (model units). Leaves: unused.</summary>
    public List<double> AlongDistance { get; } = new();
    /// <summary>Branches: index of the skeleton branch the vertex belongs to. Leaves: index of the leaf.</summary>
    public List<int> Owner { get; } = new();
    public int VertexCount => Verts.Count / 3;
    public int TriangleCount => Indices.Count / 3;
}

public static class EzTreeGenerator
{
    sealed class PendingBranch
    {
        public Vec3d Origin; public EulerXyz Orientation; public double Length, Radius; public int Level; public double SectionCount, SegmentCount;
    }

    /// <summary>JavaScript <c>Math.round</c>: nearest integer, halves towards +infinity (C#'s default rounds halves to even).</summary>
    public static double JsRound(double x)
    {
        double f = Math.Floor(x);
        return x - f >= 0.5 ? f + 1 : f;
    }

    /// <summary>Grows the skeleton; every random number is drawn here, in the original's order.</summary>
    public static EzSkeleton GenerateSkeleton(EzTreeOptions o)
    {
        if (o.TrellisEnabled) throw new NotSupportedException("ez-tree trellis growth is not ported");
        var sk = new EzSkeleton();
        var rng = new EzRng(o.Seed);
        var queue = new Queue<PendingBranch>();
        queue.Enqueue(new PendingBranch
        {
            Origin = new Vec3d(0, 0, 0), Orientation = new EulerXyz(0, 0, 0), Length = o.Length[0], Radius = o.Radius[0],
            Level = 0, SectionCount = o.Sections[0], SegmentCount = o.Segments[0],
        });
        while (queue.Count > 0) GrowBranch(queue.Dequeue(), o, rng, queue, sk);
        return sk;
    }

    static void GrowBranch(PendingBranch branch, EzTreeOptions o, EzRng rng, Queue<PendingBranch> queue, EzSkeleton sk)
    {
        var sectionOrientation = branch.Orientation;
        var sectionOrigin = branch.Origin;
        // The original compares the type with 'Deciduous' (capital D) while the type values are lower case, so this
        // divisor is always 1. Kept as in the original so seeds grow identical trees.
        double sectionLength = branch.Length / branch.SectionCount / (o.Type == "Deciduous" ? o.Levels - 1 : 1);
        var sections = new List<EzSection>();
        for (int i = 0; i <= branch.SectionCount; i++)
        {
            double sectionRadius = branch.Radius;
            if (i == branch.SectionCount && branch.Level == o.Levels) sectionRadius = 0.001;
            else if (o.Type == "deciduous") sectionRadius *= 1 - o.Taper[branch.Level] * (i / branch.SectionCount);
            else if (o.Type == "evergreen") sectionRadius *= 1 - (i / branch.SectionCount);

            sections.Add(new EzSection { Origin = sectionOrigin, Orientation = sectionOrientation, Radius = sectionRadius });
            sectionOrigin = sectionOrigin + new Vec3d(0, sectionLength, 0).ApplyEuler(sectionOrientation);

            double gnarliness = Math.Max(1, 1 / Math.Sqrt(sectionRadius)) * o.Gnarliness[branch.Level];
            sectionOrientation.X += rng.Random(gnarliness, -gnarliness);
            sectionOrientation.Z += rng.Random(gnarliness, -gnarliness);

            var qSection = Quatd.FromEuler(sectionOrientation);
            var qTwist = Quatd.FromAxisAngle(new Vec3d(0, 1, 0), o.Twist[branch.Level]);
            qSection = Quatd.Multiply(qSection, qTwist);

            var sectionUp = new Vec3d(0, 1, 0).ApplyQuaternion(qSection);
            var target = o.ForceDirection.Normalize();
            var axis = Vec3d.Cross(sectionUp, target);
            double sinFull = axis.Length();
            if (sinFull > 1e-6)
            {
                axis = axis.Div(sinFull);
                double fullAngle = Math.Atan2(sinFull, sectionUp.Dot(target));
                double step = o.ForceStrength / sectionRadius;
                double clamped = Math.Max(-fullAngle, Math.Min(fullAngle, step));
                qSection = Quatd.Multiply(Quatd.FromAxisAngle(axis, clamped), qSection); // premultiply
            }
            sectionOrientation = EulerXyz.FromQuaternion(qSection);
        }

        sk.Branches.Add(new EzSkeletonBranch { Sections = sections, SegmentCount = branch.SegmentCount, BaseRadius = branch.Radius, Level = branch.Level });

        if (o.Type == "deciduous")
        {
            var last = sections[^1];
            if (branch.Level < o.Levels)
                queue.Enqueue(new PendingBranch
                {
                    Origin = last.Origin, Orientation = last.Orientation, Length = o.Length[branch.Level + 1], Radius = last.Radius,
                    Level = branch.Level + 1, SectionCount = branch.SectionCount, SegmentCount = branch.SegmentCount,
                });
            else RecordLeaf(last.Origin, last.Orientation, o, rng, sk);
        }

        if (branch.Level == o.Levels) GenerateLeaves(sections, o, rng, sk);
        else if (branch.Level < o.Levels) GenerateChildBranches(o.Children[branch.Level], branch.Level + 1, sections, o, rng, queue);
    }

    static void GenerateChildBranches(double count, int level, List<EzSection> sections, EzTreeOptions o, EzRng rng, Queue<PendingBranch> queue)
    {
        double radialOffset = rng.Random();
        double startMin = o.Start[level];
        double heightStep = (1.0 - startMin) / count;
        var angleSlots = ShuffledIndices(count, rng);
        for (int i = 0; i < count; i++)
        {
            double childBranchStart = startMin + (i + rng.Random()) * heightStep;
            var (origin, parentOrientation, alpha, a, b) = Interpolate(sections, childBranchStart);
            double childBranchRadius = o.Radius[level] * ((1 - alpha) * a.Radius + alpha * b.Radius);
            double radialJitter = rng.Random(0.5, -0.5);
            double radialAngle = 2.0 * Math.PI * (radialOffset + (angleSlots[i] + radialJitter) / count);
            var q1 = Quatd.FromAxisAngle(new Vec3d(1, 0, 0), o.Angle[level] / (180 / Math.PI));
            var q2 = Quatd.FromAxisAngle(new Vec3d(0, 1, 0), radialAngle);
            var q3 = Quatd.FromEuler(parentOrientation);
            q2 = Quatd.Multiply(q2, q1);
            q3 = Quatd.Multiply(q3, q2);
            var childOrientation = EulerXyz.FromQuaternion(q3);
            double childLength = o.Length[level] * (o.Type == "evergreen" ? 1.0 - childBranchStart : 1.0);
            queue.Enqueue(new PendingBranch
            {
                Origin = origin, Orientation = childOrientation, Length = childLength, Radius = childBranchRadius,
                Level = level, SectionCount = o.Sections[level], SegmentCount = o.Segments[level],
            });
        }
    }

    static void GenerateLeaves(List<EzSection> sections, EzTreeOptions o, EzRng rng, EzSkeleton sk)
    {
        double radialOffset = rng.Random();
        double count = o.LeafCount;
        double startMin = o.LeafStart;
        double heightStep = (1.0 - startMin) / count;
        var angleSlots = ShuffledIndices(count, rng);
        for (int i = 0; i < count; i++)
        {
            double leafStart = startMin + (i + rng.Random()) * heightStep;
            var (origin, parentOrientation, _, _, _) = Interpolate(sections, leafStart);
            double radialJitter = rng.Random(0.5, -0.5);
            double radialAngle = 2.0 * Math.PI * (radialOffset + (angleSlots[i] + radialJitter) / count);
            var q1 = Quatd.FromAxisAngle(new Vec3d(1, 0, 0), o.LeafAngle / (180 / Math.PI));
            var q2 = Quatd.FromAxisAngle(new Vec3d(0, 1, 0), radialAngle);
            var q3 = Quatd.FromEuler(parentOrientation);
            q2 = Quatd.Multiply(q2, q1);
            q3 = Quatd.Multiply(q3, q2);
            RecordLeaf(origin, EulerXyz.FromQuaternion(q3), o, rng, sk);
        }
    }

    /// <summary>
    /// The sections on either side of the normalised position <paramref name="t"/> along the parent: lerped origin,
    /// slerped orientation (<c>qB.slerp(qA, alpha)</c>, as in the original), alpha and the two sections.
    /// </summary>
    static (Vec3d origin, EulerXyz orientation, double alpha, EzSection a, EzSection b) Interpolate(List<EzSection> sections, double t)
    {
        int sectionIndex = (int)Math.Floor(t * (sections.Count - 1));
        var a = sections[sectionIndex];
        var b = sectionIndex == sections.Count - 1 ? a : sections[sectionIndex + 1];
        double alpha = (t - sectionIndex / (double)(sections.Count - 1)) / (1 / (double)(sections.Count - 1));
        var origin = Vec3d.Lerp(a.Origin, b.Origin, alpha);
        var qA = Quatd.FromEuler(a.Orientation);
        var qB = Quatd.FromEuler(b.Orientation);
        return (origin, EulerXyz.FromQuaternion(qB.Slerp(qA, alpha)), alpha, a, b);
    }

    static void RecordLeaf(Vec3d origin, EulerXyz orientation, EzTreeOptions o, EzRng rng, EzSkeleton sk)
    {
        double size = o.LeafSize * (1 + rng.Random(o.LeafSizeVariance, -o.LeafSizeVariance));
        sk.Leaves.Add(new EzLeaf(origin, orientation, size));
    }

    /// <summary>Fisher-Yates shuffle of [0..count-1] with the tree's generator (as in the original).</summary>
    public static int[] ShuffledIndices(double count, EzRng rng)
    {
        int n = (int)count;
        var arr = new int[n];
        for (int k = 0; k < n; k++) arr[k] = k;
        for (int k = n - 1; k > 0; k--)
        {
            int r = (int)Math.Floor(rng.Random() * (k + 1));
            (arr[k], arr[r]) = (arr[r], arr[k]);
        }
        return arr;
    }

    // ------------------------------------------------------------------------------------------------ meshing

    /// <summary>ez-tree's <c>#meshSkeleton(detail)</c>: branch rings and leaf quads; uses no random numbers.</summary>
    public static (EzBuffers branches, EzBuffers leaves) Mesh(EzSkeleton sk, EzTreeOptions o, EzDetail? detail = null)
    {
        detail ??= EzDetail.Full;
        int sectionStride = Math.Max(1, detail.SectionStride);
        int leafStride = Math.Max(1, detail.LeafStride);
        var billboard = detail.Billboard ?? o.Billboard;
        var branches = new EzBuffers();
        var leaves = new EzBuffers();
        for (int bi = 0; bi < sk.Branches.Count; bi++) MeshBranch(branches, sk.Branches[bi], bi, sectionStride, detail.SegmentFactor, o);
        for (int i = 0; i < sk.Leaves.Count; i += leafStride) MeshLeaf(leaves, sk.Leaves[i], i, detail.LeafScale, billboard, o, LeafLevel(o));
        return (branches, leaves);
    }

    static int LeafLevel(EzTreeOptions o) => (int)o.Levels;

    static void MeshBranch(EzBuffers buf, EzSkeletonBranch sb, int owner, int sectionStride, double segmentFactor, EzTreeOptions o)
    {
        var sections = sb.Sections;
        var cum = new double[sections.Count];
        for (int i = 1; i < sections.Count; i++) cum[i] = cum[i - 1] + (sections[i].Origin - sections[i - 1].Origin).Length();
        int segments = (int)Math.Max(3, JsRound(sb.SegmentCount * segmentFactor));
        double wrapsX = Math.Max(1, JsRound(sb.BaseRadius * o.BarkTextureScaleX));
        var sampled = new List<EzSection>();
        var sampledAlong = new List<double>();
        var sampledDist = new List<double>();
        for (int i = 0; i < sections.Count; i += sectionStride) { sampled.Add(sections[i]); sampledAlong.Add(i / (double)Math.Max(1, sections.Count - 1)); sampledDist.Add(cum[i]); }
        if ((sections.Count - 1) % sectionStride != 0) { sampled.Add(sections[^1]); sampledAlong.Add(1.0); sampledDist.Add(cum[^1]); }

        int indexOffset = buf.Verts.Count / 3;
        for (int k = 0; k < sampled.Count; k++)
        {
            var section = sampled[k];
            Vec3d firstV = default, firstN = default, firstT = default; double firstUvY = 0;
            var q = Quatd.FromEuler(section.Orientation);
            for (int j = 0; j < segments; j++)
            {
                double angle = 2.0 * Math.PI * j / segments;
                var vertex = new Vec3d(Math.Cos(angle), 0, Math.Sin(angle)).Mul(section.Radius).ApplyEuler(section.Orientation) + section.Origin;
                var normal = new Vec3d(Math.Cos(angle), 0, Math.Sin(angle)).ApplyEuler(section.Orientation).Normalize();
                double uvx = j / (double)segments * wrapsX, uvy = k % 2 == 0 ? 0 : 1;
                Push(buf.Verts, vertex); Push(buf.Normals, normal); buf.Uvs.Add(uvx); buf.Uvs.Add(uvy);
                var tangent = new Vec3d(-Math.Sin(angle), 0, Math.Cos(angle)).ApplyQuaternion(q);
                Push(buf.Tangents, tangent); buf.Level.Add(sb.Level); buf.Along.Add(sampledAlong[k]);
                buf.AlongDistance.Add(sampledDist[k]); buf.Owner.Add(owner);
                if (j == 0) { firstV = vertex; firstN = normal; firstT = tangent; firstUvY = uvy; }
            }
            // duplicate the first vertex so the UV seam is continuous (u = wrapsX)
            Push(buf.Verts, firstV); Push(buf.Normals, firstN); buf.Uvs.Add(wrapsX); buf.Uvs.Add(firstUvY);
            Push(buf.Tangents, firstT); buf.Level.Add(sb.Level); buf.Along.Add(sampledAlong[k]);
            buf.AlongDistance.Add(sampledDist[k]); buf.Owner.Add(owner);
        }
        int N = segments + 1;
        for (int i = 0; i < sampled.Count - 1; i++)
            for (int j = 0; j < segments; j++)
            {
                int v1 = indexOffset + i * N + j, v2 = indexOffset + i * N + (j + 1), v3 = v1 + N, v4 = v2 + N;
                buf.Indices.Add(v1); buf.Indices.Add(v3); buf.Indices.Add(v2);
                buf.Indices.Add(v2); buf.Indices.Add(v3); buf.Indices.Add(v4);
            }
    }

    static void MeshLeaf(EzBuffers buf, EzLeaf leaf, int owner, double scale, EzBillboard billboard, EzTreeOptions o, int level)
    {
        double leafSize = leaf.Size * scale;
        double W = leafSize, L = leafSize;
        void CreateLeaf(double rotation)
        {
            int i = buf.Verts.Count / 3;
            var rot = new EulerXyz(0, rotation, 0);
            Span<Vec3d> v = stackalloc Vec3d[4];
            v[0] = new Vec3d(-W / 2, L, 0); v[1] = new Vec3d(-W / 2, 0, 0); v[2] = new Vec3d(W / 2, 0, 0); v[3] = new Vec3d(W / 2, L, 0);
            for (int k = 0; k < 4; k++) v[k] = v[k].ApplyEuler(rot).ApplyEuler(leaf.Orientation) + leaf.Origin;
            for (int k = 0; k < 4; k++) Push(buf.Verts, v[k]);
            var n = new Vec3d(0, 0, 1).ApplyEuler(leaf.Orientation);
            for (int k = 0; k < 4; k++)
            {
                var nk = o.RoundedNormals ? (n + v[k] - leaf.Origin).Normalize() : n;
                Push(buf.Normals, nk);
            }
            buf.Uvs.Add(0); buf.Uvs.Add(1); buf.Uvs.Add(0); buf.Uvs.Add(0); buf.Uvs.Add(1); buf.Uvs.Add(0); buf.Uvs.Add(1); buf.Uvs.Add(1);
            buf.Indices.Add(i); buf.Indices.Add(i + 1); buf.Indices.Add(i + 2);
            buf.Indices.Add(i); buf.Indices.Add(i + 2); buf.Indices.Add(i + 3);
            for (int k = 0; k < 4; k++)
            {
                Push(buf.Anchors, leaf.Origin); buf.Level.Add(level); buf.Along.Add(k == 0 || k == 3 ? 1.0 : 0.0); buf.Owner.Add(owner);
            }
        }
        CreateLeaf(0);
        if (billboard == EzBillboard.Double) CreateLeaf(Math.PI / 2);
    }

    static void Push(List<double> l, Vec3d v) { l.Add(v.X); l.Add(v.Y); l.Add(v.Z); }
}
