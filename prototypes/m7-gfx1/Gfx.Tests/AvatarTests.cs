using Gfx.Avatar;
using Gfx.Avatar.Demo;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Units;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>Lane A: persistent avatar profile, era dress, parametric mesh.</summary>
public class AvatarTests
{
    static string OutDir => Path.Combine(AppContext.BaseDirectory, "test-output");

    /// <summary>Pinned canonical hash of the populated player fixture: catches any drift in the canonical encoding.</summary>
    const string PlayerFixtureHash = "925A4FE19B36D6ACCE4BD927E8E60C7B56787F8BE52CAA34BD5167DD4E15A53D";

    [Fact]
    public void ProfileRoundTripsBitExactThroughJsonAndFile()
    {
        var p = AvatarProfile.PlayerFixture();
        Assert.All(p.FaceMorphs.Where((_, i) => i != (int)FaceMorph.EyeSize && i != (int)FaceMorph.EarSize), w => Assert.NotEqual(0, w)); // populated
        string json = p.ToJson();
        var q = AvatarProfile.FromJson(json);
        Assert.Equal(p.CanonicalBytes(), q.CanonicalBytes());
        Assert.Equal(json, q.ToJson());
        Assert.Equal(p.Hash(), q.Hash());
        string path = Path.Combine(OutDir, "profile-player.json");
        p.Save(path);
        Assert.Equal(p.Hash(), AvatarProfile.Load(path).Hash());
    }

    [Fact]
    public void ProfileHashIsPinned() { string h = AvatarProfile.PlayerFixture().Hash(); Assert.True(h == PlayerFixtureHash, "hash " + h); }

    [Fact]
    public void HashSeesEveryMorphAndIdentityHashIgnoresClothingPreferences()
    {
        var p = AvatarProfile.PlayerFixture(); var seen = new HashSet<string> { p.Hash() };
        for (int i = 0; i < AvatarProfile.MorphCount; i++)
        {
            var q = p.Clone(); q.FaceMorphs[i] += q.FaceMorphs[i] < 1000 ? 1 : -1;
            Assert.True(seen.Add(q.Hash()), $"morph {(FaceMorph)i} not hashed");
            Assert.NotEqual(p.IdentityHash(), q.IdentityHash());
        }
        var c = p.Clone(); c.Clothing.Royal.Variant = 2; c.Heraldry.Primary = "#000000"; c.DisplayName = "Renamed";
        Assert.NotEqual(p.Hash(), c.Hash());
        Assert.Equal(p.IdentityHash(), c.IdentityHash());
    }

    [Fact]
    public void V0ProfileMigratesToV1()
    {
        const string v0 = """{ "id": "6f1d2c7a-3b8e-4c55-9a10-2e7b5d4c9f01", "name": "Old Draft", "morphs": [1,2,3,4,5,6,7,8,9,10,11,12] }""";
        var p = AvatarProfile.FromJson(v0);
        Assert.Equal(1, p.SchemaVersion); Assert.Equal("Old Draft", p.DisplayName);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 0, 0 }, p.FaceMorphs);
    }

    [Fact]
    public void ValidationRejectsBadProfiles()
    {
        var good = AvatarProfile.PlayerFixture().ToJson();
        Assert.Throws<FormatException>(() => AvatarProfile.FromJson(good.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9")));
        Assert.Throws<FormatException>(() => AvatarProfile.FromJson(good.Replace("6f1d2c7a-3b8e-4c55-9a10-2e7b5d4c9f01", "not-a-guid")));
        var p = AvatarProfile.PlayerFixture(); p.FaceMorphs[3] = 1001; Assert.Throws<FormatException>(p.Validate);
        p = AvatarProfile.PlayerFixture(); p.FaceMorphs = new int[12]; Assert.Throws<FormatException>(p.Validate);
        p = AvatarProfile.PlayerFixture(); p.SkinTone = "C68E6A"; Assert.Throws<FormatException>(p.Validate);
    }

    static Vector3[] HeadPositions(AvatarProfile p)
    {
        var m = AvatarMesh.Build(p, null, new AvatarBuildOptions(HeadOnly: true));
        return m.Mesh.Vertices.Take(m.HeadVertexEnd).Select(v => v.Position).ToArray();
    }

    [Fact]
    public void AtLeastTwelveFaceMorphsEachDeformTheHeadDistinctly()
    {
        Assert.True(Enum.GetValues<FaceMorph>().Length >= 12);
        var neutral = AvatarProfile.PlayerFixture(); Array.Clear(neutral.FaceMorphs); neutral.FacialHair = FacialHair.None; neutral.Hair = HairStyle.Shaved;
        var basePos = HeadPositions(neutral);
        var fields = new List<Vector3[]>();
        foreach (var m in Enum.GetValues<FaceMorph>())
        {
            var p = neutral.Clone(); p.FaceMorphs[(int)m] = 800;
            var pos = HeadPositions(p);
            Assert.Equal(basePos.Length, pos.Length);
            var d = pos.Select((x, i) => x - basePos[i]).ToArray();
            float max = d.Max(v => v.Length());
            Assert.True(max > 0.001f, $"{m} moves the head by only {max * 1000:F3} mm");
            fields.Add(d);
        }
        for (int a = 0; a < fields.Count; a++)
            for (int b = a + 1; b < fields.Count; b++)
                Assert.True(fields[a].Where((v, i) => (v - fields[b][i]).Length() > 1e-4f).Any(), $"{(FaceMorph)a} and {(FaceMorph)b} deform identically");
        // linear blend shapes: weight −w moves by exactly the negated delta (within float rounding)
        var plus = neutral.Clone(); plus.FaceMorphs[(int)FaceMorph.JawWidth] = 600;
        var minus = neutral.Clone(); minus.FaceMorphs[(int)FaceMorph.JawWidth] = -600;
        var pp = HeadPositions(plus); var mm = HeadPositions(minus);
        for (int i = 0; i < basePos.Length; i++) Assert.True(((pp[i] - basePos[i]) + (mm[i] - basePos[i])).Length() < 1e-5f);
    }

    [Theory]
    [InlineData(0, Era.Ancient)] [InlineData(2, Era.Ancient)] [InlineData(3, Era.Medieval)] [InlineData(4, Era.Medieval)]
    [InlineData(5, Era.EarlyModern)] [InlineData(6, Era.Modern)] [InlineData(40, Era.Modern)]
    public void EraIsComputedFromTheCapabilityTier(int tier, Era expected) => Assert.Equal(expected, EraDress.EraFromCapabilityTier(tier));

    [Fact]
    public void EraTierRejectsNegativeAndProfileStoresNoEra()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EraDress.EraFromCapabilityTier(-1));
        string json = AvatarProfile.PlayerFixture().ToJson();
        Assert.DoesNotContain("\"era", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tier", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(typeof(AvatarProfile).GetProperties(), pr => pr.PropertyType == typeof(Era) || pr.PropertyType == typeof(Outfit));
    }

    [Fact]
    public void IdentityIsInvariantAcrossEveryEraAndRoleOutfit()
    {
        var p = AvatarProfile.PlayerFixture(); string hash = p.Hash();
        var reference = AvatarMesh.Build(p, EraDress.Select(p, 0, AvatarRole.Military));
        var outfits = new HashSet<(Garment, Headgear)>();
        for (int tier = 0; tier <= 7; tier++)
            foreach (var role in Enum.GetValues<AvatarRole>())
            {
                var o = EraDress.Select(p, tier, role); outfits.Add((o.Garment, o.Headgear));
                var m = AvatarMesh.Build(p, o);
                Assert.Equal(reference.IdentityVertexEnd, m.IdentityVertexEnd);
                Assert.Equal(reference.Scale, m.Scale);
                for (int i = 0; i < m.IdentityVertexEnd; i++)
                    Assert.True(reference.Mesh.Vertices[i].Position == m.Mesh.Vertices[i].Position, $"tier {tier} {role}: identity vertex {i} moved");
                for (int i = m.HeadVertexStart; i < m.HeadVertexEnd; i++)
                    Assert.Equal(reference.Mesh.Vertices[i].Color, m.Mesh.Vertices[i].Color);
            }
        Assert.Equal(hash, p.Hash()); // building/outfitting never writes the profile
        Assert.True(outfits.Count >= 8, $"only {outfits.Count} distinct garment/headgear outfits");
        var eras = Enum.GetValues<Era>().Select(e => EraDress.Select(p, e, AvatarRole.Military)).Select(o => (o.Garment, o.Headgear)).Distinct().Count();
        Assert.Equal(4, eras);
    }

    [Fact]
    public void OutfitFollowsRolePreference()
    {
        var p = AvatarProfile.PlayerFixture();
        var a = EraDress.Select(p, Era.Ancient, AvatarRole.Military);
        p.Clothing.Military.Variant = 2;
        Assert.NotEqual(a.Cloak, EraDress.Select(p, Era.Ancient, AvatarRole.Military).Cloak);
        p.Clothing.Military.HeraldicColours = false; p.Clothing.Military.Accent = "#112233";
        Assert.Equal(new Color(0x11, 0x22, 0x33), EraDress.Select(p, Era.Ancient, AvatarRole.Military).Main);
    }

    [Fact]
    public void MeshIsValidAndCpuSkinningMatchesTheBoneTextureReference()
    {
        var p = AvatarProfile.PlayerFixture();
        var m = AvatarMesh.Build(p, EraDress.Select(p, 4, AvatarRole.Royal), new AvatarBuildOptions(Sword: true));
        var v = m.Mesh.Vertices;
        Assert.True(m.Mesh.Indices.Length % 3 == 0 && m.Mesh.Indices.All(i => i >= 0 && i < v.Length));
        Assert.All(v, x => { Assert.InRange(x.Joints.ToVector4().X, 0, Skeleton.JointCount - 1); Assert.Equal(1f, x.Weights.X + x.Weights.Y + x.Weights.Z + x.Weights.W, 5); });
        var pose = Pose.Rest(); pose.Local[Skeleton.RUpperArm] = Matrix.CreateRotationX(-1.2f); pose.Local[Skeleton.Spine] = Matrix.CreateRotationX(0.3f);
        var skin = Skeleton.SkinMatrices(pose);
        var dst = new VertexPNC[v.Length];
        PosedAvatar.Skin(v, skin, dst);
        float maxErr = 0;
        for (int i = 0; i < v.Length; i++) { var (rp, _) = BoneTexture.SkinCpu(v[i], skin, skin, 0); maxErr = MathF.Max(maxErr, (rp - dst[i].Position).Length()); }
        Assert.True(maxErr < 1e-5f, $"max error {maxErr}");
        // the posed right arm actually moved (sword is on the right hand)
        Assert.Contains(Enumerable.Range(0, v.Length), i => (dst[i].Position - v[i].Position).Length() > 0.3f);
    }

    [GfxFact]
    public void EraLineupRendersFiguresAndNeverWritesTheProfile()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var profile = AvatarProfile.PlayerFixture();
            string before = profile.Hash();
            using var scene = new AvatarErasScene(profile);
            scene.Load(gd, TestGfx.Host.Effects);
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shot = scene.Shots[0];
            scene.Update(shot.SceneTime);
            r.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt);
            var px = Capture.ReadPixels(rt);
            Capture.SavePng(rt, Path.Combine(OutDir, "avatar-eras-test.png"));
            // figures stand in the middle band: compare against the same frame with nothing but sky+ground
            int figure = 0;
            for (int y = 40; y < 150; y++) for (int x = 60; x < 260; x++) { var c = px[y * 320 + x]; var g = px[y * 320 + 10]; if (Math.Abs(c.R - g.R) + Math.Abs(c.G - g.G) + Math.Abs(c.B - g.B) > 40) figure++; }
            Assert.True(figure > 1500, $"figure pixels {figure}");
            Assert.Equal(before, profile.Hash()); // the instance the scene rendered, not a fresh fixture
        });
    }
}
