using Gfx.Engine.Cameras;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework;

namespace Gfx.Tests;

public class FrustumTests
{
    static readonly CameraPose Cam = new(new Vector3(0, 10, 50), new Vector3(0, 0, 0), 0.8f, 0.5f, 500f);
    static Matrix VP => Cam.ViewProjection(16f / 9f);

    static float U(int i, int salt) { unchecked { uint h = (uint)(i * 374761393 + salt * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f; } }

    [Fact]
    public void ObviousCasesInsideAndOutside()
    {
        var f = new Frustum(VP);
        Assert.True(f.IntersectsSphere(Vector3.Zero, 1));
        Assert.False(f.IntersectsSphere(new Vector3(0, 10, 80), 1));    // behind the camera
        Assert.False(f.IntersectsSphere(new Vector3(0, 0, -600), 5));   // beyond far plane
        Assert.False(f.IntersectsSphere(new Vector3(400, 0, 0), 5));    // far to the side
        Assert.True(f.IntersectsSphere(new Vector3(0, 10, 80), 40));    // big sphere reaching in
        Assert.True(f.IntersectsBox(new Vector3(-1), new Vector3(1)));
        Assert.False(f.IntersectsBox(new Vector3(300, -1, -1), new Vector3(302, 1, 1)));
    }

    [Fact]
    public void AgreesWithMonoGameBoundingFrustumOnCenters()
    {
        // point (r=0) classification must match the reference exactly away from the boundary
        var f = new Frustum(VP); var reference = new BoundingFrustum(VP);
        int checkedN = 0;
        for (int i = 0; i < 5000; i++)
        {
            var p = new Vector3((U(i, 1) - 0.5f) * 600, (U(i, 2) - 0.5f) * 200, (U(i, 3) - 0.5f) * 1200);
            var refIn = reference.Contains(new BoundingSphere(p, 0.05f));
            var refStrictIn = reference.Contains(new BoundingSphere(p, 1e-3f)) == ContainmentType.Contains;
            bool ours = f.IntersectsSphere(p, 0);
            if (refStrictIn) { Assert.True(ours, $"visible point culled: {p}"); checkedN++; }
            else if (refIn == ContainmentType.Disjoint) { Assert.False(ours, $"invisible point kept: {p}"); checkedN++; }
        }
        Assert.True(checkedN > 4900);
    }

    [Fact]
    public void CullSpheresReturnsVisibleIndicesInOrder()
    {
        var f = new Frustum(VP);
        var s = new[] { new Vector4(0, 0, 0, 1), new Vector4(0, 10, 90, 1), new Vector4(5, 0, -20, 1), new Vector4(900, 0, 0, 1) };
        var idx = new int[s.Length];
        int n = f.CullSpheres(s, idx);
        Assert.Equal(new[] { 0, 2 }, idx[..n]);
    }

    [Fact]
    public void ChunkLodSelectsByDistanceWithHysteresis()
    {
        var lod = new ChunkLod(new[] { 50f, 150f, 400f }, 0.1f);
        Assert.Equal(0, lod.Select(10)); Assert.Equal(1, lod.Select(51)); Assert.Equal(2, lod.Select(399)); Assert.Equal(-1, lod.Select(401));
        Assert.Equal(0, lod.Select(54, previous: 0));   // within 10% band: stays fine
        Assert.Equal(1, lod.Select(56, previous: 0));   // past the band: coarser
        Assert.Equal(0, lod.Select(40, previous: 1));   // going finer is immediate
        Assert.Equal(2, lod.Select(430, previous: 2));  // culling is delayed by the band too
    }

    [Fact]
    public void ChunkGridVisibleListsFrustumChunksWithLod()
    {
        var f = new Frustum(VP); var lod = new ChunkLod(new[] { 60f, 200f, 450f });
        var chunks = new List<BoundingBox>();
        for (int z = -8; z < 8; z++) for (int x = -8; x < 8; x++)
            chunks.Add(new BoundingBox(new Vector3(x * 64, -5, z * 64), new Vector3(x * 64 + 64, 5, z * 64 + 64)));
        var outp = new (int, int)[chunks.Count];
        int n = lod.Visible(f, Cam.Position, chunks.ToArray(), outp);
        Assert.InRange(n, 1, chunks.Count - 1);
        for (int i = 0; i < n; i++)
        {
            var (c, l) = outp[i];
            Assert.True(f.IntersectsBox(chunks[c].Min, chunks[c].Max));
            if (i > 0) Assert.True(outp[i - 1].Item1 < c);
            Assert.InRange(l, 0, 2);
        }
        // the chunk under the camera is LOD 0
        int under = chunks.FindIndex(b => b.Min.X <= 0 && b.Max.X > 0 && b.Min.Z <= 0 && b.Max.Z > 0);
        Assert.Contains((under, 0), outp[..n]);
    }
}
