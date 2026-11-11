using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Engine.Assets;
using Gfx.Gear.Anim;
using Gfx.Gear.Bake;
using Gfx.Gear.Geometry;
using Gfx.Gear.Items;
using Gfx.Gear.Maps;
using Gfx.Gear.Rig;
using Gfx.Gear.Runtime;
using Microsoft.Xna.Framework;

namespace Gfx.Tests;

/// <summary>
/// Stage kit: sockets on the rig, grip poses (finger wraps, strap and central shield holds), the generators'
/// determinism and mesh and map validity, the hero bake (glTF and PNG round trips, byte-identical re-bakes), the
/// authored clips (hand targets reached, determinism, deaths on the ground), helmets hiding hair, carried items, the
/// bow rig, and the presentation-only boundary.
/// </summary>
public sealed class KitTests
{
    static readonly string Dir = AssetRoot.Baked("chars");
    static readonly Dictionary<string, (CharacterFile f, Skeleton s)> Cache = new(StringComparer.Ordinal);
    static readonly object Lock = new();

    static (CharacterFile f, Skeleton s) Id(string id)
    {
        lock (Lock)
        {
            if (!Cache.TryGetValue(id, out var v)) { var f = CharacterFile.Load(Path.Combine(Dir, id + ".g2ch")); v = (f, new Skeleton(f.Bones)); Cache[id] = v; }
            return v;
        }
    }

    static readonly string[] Identities = CharsTests.Identities;

    // ------------------------------------------------------------------ sockets

    [CharsBakeFact]
    public void SocketsSitOnTheBodyNotOnTheHands()
    {
        foreach (var id in Identities)
        {
            var (f, s) = Id(id);
            var k = KitSockets.Compute(f, s);
            foreach (var side in new[] { "l", "r" })
            {
                var hip = k["hip_" + side].Origin;
                var thigh = s.Head[s["thigh_" + side]];
                var hand = s.Head[s["hand_" + side]];
                // the lost attempt's hip socket sat on the A-pose hand, 0.6 m out
                Assert.True((hip - thigh).Length() < 0.20f, $"{id} hip_{side} {(hip - thigh).Length():F3} m from the hip joint");
                Assert.True((hip - hand).Length() > 0.25f, $"{id} hip_{side} only {(hip - hand).Length():F3} m from the hand");
                Assert.Equal(s["pelvis"], k["hip_" + side].Bone);
                Assert.True(MathF.Abs(hip.X) > MathF.Abs(thigh.X), $"{id} hip_{side} is not outside the hip joint");
                // forearm socket: on the forearm's back, between elbow and wrist
                var fo = k["forearm_" + side];
                var el = s.Head[s["lowerarm_" + side]]; var wr = s.Head[s["hand_" + side]];
                var ax = Vector3.Normalize(wr - el);
                var q = fo.Origin - el;
                float along = Vector3.Dot(q, ax), off = (q - ax * along).Length();
                Assert.InRange(along, 0.03f, 0.12f);
                Assert.InRange(off, 0.02f, 0.06f);
                Assert.Equal(s["lowerarm_" + side], fo.Bone);
            }
            var back = k["back"].Origin;
            Assert.True(back.Z < s.Head[s["spine_03"]].Z - 0.05f, $"{id} back socket is not behind the chest");
            var head = k["head"].Origin;
            Assert.True((head - s.Head[s["head"]]).Length() < 0.12f, $"{id} head socket {(head - s.Head[s["head"]]).Length():F3} m from the head bone");
        }
    }

    [CharsBakeFact]
    public void StrapShieldFitBringsTheFistToTheHandGrip()
    {
        var aspis = Aspis.Build(new AspisSpec { Seed = 1, MapSize = 0, Lod = 2 });
        var kite = Shields.Build(new ShieldSpec { Kind = "kite", MapSize = 0, Lod = 1 });
        foreach (var id in Identities)
        {
            var (f, s) = Id(id);
            var k = KitSockets.Compute(f, s);
            foreach (var (shield, standoff) in new[] { (aspis, 0f), (kite, 0.028f) })
            {
                var (wrist, slide, residual) = KitHold.FitStrap(s, k, shield, standoff);
                Assert.True(residual < 0.030f, $"{id} {shield.Kind}: fist {residual * 1000:F1} mm from the hand grip");
                Assert.InRange(slide, -0.12f, 0.12f);
                Assert.True(MathF.Abs(wrist.Length() - 1f) < 1e-4f);
            }
        }
    }

    // ------------------------------------------------------------------ grip poses

    static (CharRig rig, Pose p, Quaternion[] g, Vector3[] h) RestPose(string id)
    {
        var (_, s) = Id(id);
        var rig = new CharRig(s); var p = new Pose(s.Count); var g = new Quaternion[s.Count]; var h = new Vector3[s.Count];
        Fk.Solve(s, p, g, h, rig.Pelvis);
        return (rig, p, g, h);
    }

    [CharsBakeFact]
    public void GripWrapClosesTheFingersOnTheShaftWithoutPassingThrough()
    {
        foreach (var id in new[] { "avatar", "a1", "b4" })
        {
            var (f, s) = Id(id);
            var k = KitSockets.Compute(f, s);
            float CurlSum(float radius, out float err)
            {
                var (rig, p, g, h) = RestPose(id);
                var sk = k["grip_r"].Local(s, g, h);
                err = Grip.Wrap(rig, p, g, h, false, sk.Translation, sk.Up, radius);
                float sum = 0;
                foreach (var (b, axis, _) in rig.FingersR) { p.Local[b].Normalize(); sum += 2 * MathF.Acos(Math.Clamp(MathF.Abs(p.Local[b].W), 0f, 1f)); }
                // no finger segment end inside the shaft
                foreach (var (b, _, _) in rig.FingersR)
                {
                    var q = h[b] - sk.Translation; var d = (q - sk.Up * Vector3.Dot(q, sk.Up)).Length();
                    Assert.True(d > radius - 0.002f, $"{id}: finger joint {s.Names[b]} {d * 1000:F1} mm from the axis, inside a {radius * 1000:F0} mm shaft");
                }
                return sum;
            }
            float thin = CurlSum(0.004f, out float e1), mid = CurlSum(0.014f, out float e2), thick = CurlSum(0.022f, out float e3);
            Assert.True(e2 < 0.015f, $"{id}: spear-shaft wrap error {e2 * 1000:F1} mm");
            Assert.True(thin > mid && mid > thick, $"{id}: curl {thin:F2} / {mid:F2} / {thick:F2} rad should shrink as the grip thickens");
        }
    }

    [CharsBakeFact]
    public void CentralGripShieldTurnsAboutItsBarToFaceTheWantedWay()
    {
        var thureos = Shields.Build(new ShieldSpec { Kind = "thureos", MapSize = 0, Lod = 1 });
        var grip = thureos.Frame("grip");
        // the board turned about its bar: for any socket frame, the face normal ends up as close to the wanted direction as
        // a turn about the bar allows (the component along the bar cannot change)
        foreach (var yaw in new[] { 0f, 0.7f, 2.0f })
        {
            var sock = Matrix.CreateRotationY(yaw) * Matrix.CreateRotationZ(MathHelper.PiOver2) * Matrix.CreateTranslation(1, 1, 1);
            var bar = Vector3.Normalize(sock.Up);
            var want = Vector3.Normalize(new Vector3(0.3f, 0.1f, 1f));
            var face0 = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, Matrix.Invert(grip) * sock));
            // search the spin numerically and compare with the hold's closed form (via ItemWorlds math, reproduced here)
            float best = -2;
            for (int i = 0; i < 3600; i++)
            {
                var w = Matrix.Invert(grip) * Matrix.CreateFromAxisAngle(Vector3.UnitY, i * MathF.Tau / 3600) * sock;
                best = MathF.Max(best, Vector3.Dot(Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, w)), want));
            }
            var vp = face0 - bar * Vector3.Dot(face0, bar); var dp = want - bar * Vector3.Dot(want, bar);
            float spin = MathF.Atan2(Vector3.Dot(Vector3.Cross(Vector3.Normalize(vp), Vector3.Normalize(dp)), bar), Vector3.Dot(Vector3.Normalize(vp), Vector3.Normalize(dp)));
            var wSolved = Matrix.Invert(grip) * Matrix.CreateFromAxisAngle(Vector3.UnitY, spin) * sock;
            float got = Vector3.Dot(Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, wSolved)), want);
            Assert.True(got > best - 2e-3f, $"spin {spin:F3}: face . wanted {got:F4} < best {best:F4}");
        }
    }

    // ------------------------------------------------------------------ generators

    static IEnumerable<Func<KitItem>> Generators(BodyMeasure m, int map)
    {
        yield return () => Aspis.Build(new AspisSpec { Seed = 3, MapSize = map, Lod = 1 });
        foreach (var k in new[] { "thureos", "round", "kite", "heater" }) yield return () => Shields.Build(new ShieldSpec { Kind = k, Seed = 3, MapSize = map, Lod = 1 });
        foreach (var k in new[] { "corinthian", "montefortino", "nasal", "kettle", "greathelm" }) yield return () => Helmets.Build(new HelmetSpec { Kind = k, Seed = 3, MapSize = map, Lod = 1 }, m);
        foreach (var k in new[] { "spear", "xiphos", "longsword", "arming", "axe", "bow", "arrow", "quiver", "scabbard", "sling", "musket" })
            yield return () => Weapons.Build(new WeaponSpec { Kind = k, Seed = 3, MapSize = map, Lod = 1 });
        yield return () => Banner.Build(new BannerSpec { Seed = 3, MapSize = map });
    }

    [CharsBakeFact]
    public void GeneratorsAreDeterministicAndSeedsVary()
    {
        var (f, s) = Id("a1");
        var m = KitSockets.Measure(f, s);
        foreach (var gen in Generators(m, 64))
        {
            var a = gen(); var b = gen();
            Assert.Equal(a.Hash(), b.Hash());
        }
        Assert.NotEqual(Aspis.Build(new AspisSpec { Seed = 3, MapSize = 64, Lod = 1 }).Hash(), Aspis.Build(new AspisSpec { Seed = 4, MapSize = 64, Lod = 1 }).Hash());
        Assert.NotEqual(Shields.Build(new ShieldSpec { Kind = "kite", Seed = 3, MapSize = 64, Lod = 1 }).Hash(), Shields.Build(new ShieldSpec { Kind = "kite", Seed = 4, MapSize = 64, Lod = 1 }).Hash());
        // helmets fit the head they are made for: two identities give two shells
        var (f2, s2) = Id("b4");
        Assert.NotEqual(Helmets.Build(new HelmetSpec { Kind = "nasal", MapSize = 0, Lod = 1 }, m).Hash(), Helmets.Build(new HelmetSpec { Kind = "nasal", MapSize = 0, Lod = 1 }, KitSockets.Measure(f2, s2)).Hash());
    }

    [CharsBakeFact]
    public void GeneratedMeshesAndMapsAreWellFormed()
    {
        var (f, s) = Id("avatar");
        var m = KitSockets.Measure(f, s);
        foreach (var gen in Generators(m, 64))
        {
            var item = gen();
            foreach (var part in item.Parts)
            {
                var errs = part.Mesh.Validate(1e-14f);
                Assert.True(errs.Count == 0, $"{item.Id}/{part.Name}: {string.Join("; ", errs)}");
                foreach (var uv in part.Mesh.Uvs) Assert.True(uv.X >= -1e-4f && uv.X <= 1.0001f && uv.Y >= -1e-4f && uv.Y <= 1.0001f, $"{item.Id}: UV {uv} outside the atlas");
                Assert.True(part.Mesh.TriangleCount > 10);
            }
            foreach (var (k, map) in item.Maps)
            {
                Assert.Equal(map.Width * map.Height * 4, map.NormalRgba.Length);
                for (int i = 0; i < map.Width * map.Height; i += 7)
                {
                    float nx = map.NormalRgba[4 * i] / 127.5f - 1, ny = map.NormalRgba[4 * i + 1] / 127.5f - 1, nz = map.NormalRgba[4 * i + 2] / 127.5f - 1;
                    float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    Assert.True(len > 0.95f && len < 1.05f && nz > 0, $"{item.Id}/{k}: normal texel {i} length {len:F3}, z {nz:F3}");
                }
            }
            Assert.True(item.Frames.Count > 0, item.Id + " has no frames");
        }
    }

    [Fact]
    public void HeightBumpsBecomeNormalsTiltingAwayFromTheirTop()
    {
        // a dome painted in the middle of a map: on its right side the normal tilts towards +u (x > 0), above its
        // top (smaller v, the image's up) towards +y: the convention the tangent frames use (OpenGL, green up)
        var maps = new MapSet("t", 64, 64) { CavityStrength = 0 };
        maps.PaintRect(UvRect.Full, 0.1, 0.1, 1, (u, v) =>
        {
            double d = Math.Sqrt((u - 0.5) * (u - 0.5) + (v - 0.5) * (v - 0.5));
            return Texel.Of(new Vector3(0.5f), 0.5f, 0f, (float)(0.01 * Math.Max(0, 1 - d / 0.3)));
        });
        maps.Finish();
        int I(int x, int y) => (y * 64 + x) * 4;
        Assert.True(maps.NormalRgba[I(44, 32)] > 140, "right of the dome: normal x should be positive");
        Assert.True(maps.NormalRgba[I(20, 32)] < 115, "left of the dome: normal x should be negative");
        Assert.True(maps.NormalRgba[I(32, 20) + 1] > 140, "above the dome (image up): normal y should be positive");
        Assert.True(maps.NormalRgba[I(32, 44) + 1] < 115, "below the dome: normal y should be negative");
    }

    [Fact]
    public void TangentFramesMatchTheNormalMapConvention()
    {
        // a quad facing +Z with u along +X and v increasing downwards (-Y): the stored bitangent (green up) must point +Y
        var m = new KitMesh("q");
        int a = m.Add(new Vector3(0, 1, 0), new Vector2(0, 0), Color.White), b = m.Add(new Vector3(1, 1, 0), new Vector2(1, 0), Color.White);
        int c = m.Add(new Vector3(1, 0, 0), new Vector2(1, 1), Color.White), d = m.Add(new Vector3(0, 0, 0), new Vector2(0, 1), Color.White);
        m.Tri(a, d, c); m.Tri(a, c, b);
        m.Finish();
        var n = m.Normals[0]; var t = m.Tangents[0];
        var bit = Vector3.Cross(n, new Vector3(t.X, t.Y, t.Z)) * t.W;
        Assert.True(Vector3.Dot(new Vector3(t.X, t.Y, t.Z), Vector3.UnitX) > 0.99f);
        Assert.True(Vector3.Dot(bit, Vector3.UnitY) > 0.99f, $"bitangent {bit}");
    }

    // ------------------------------------------------------------------ the hero bake

    [Fact]
    public void PngRoundTripsBitExactly()
    {
        var px = new byte[37 * 23 * 4];
        for (int i = 0; i < px.Length; i++) px[i] = (byte)(Noise.Hash(i, 7) & 255);
        for (int i = 3; i < px.Length; i += 4) px[i] = 255;
        var (w, h, back) = Png.Decode(Png.Encode(37, 23, px, 3));
        Assert.Equal((37, 23), (w, h));
        Assert.Equal(px, back);
        var (_, _, back4) = Png.Decode(Png.Encode(37, 23, px, 4));
        Assert.Equal(px, back4);
    }

    [Fact]
    public void HeroBakeIsByteIdenticalAndRoundTrips()
    {
        var spec = HeroBake.Spec with { MapSize = 256 };
        var r1 = HeroBake.Bake(spec); var r2 = HeroBake.Bake(spec);
        Assert.Equal(r1.Sha256, r2.Sha256);
        Assert.True(r1.Glb.AsSpan().SequenceEqual(r2.Glb));
        var (meshes, images, json) = Glb.Read(r1.Glb);
        Assert.Equal(3, meshes.Count);
        Assert.Equal("2.0", json["asset"]!["version"]!.GetValue<string>());
        var lod0 = Aspis.BuildMesh(spec with { Lod = 0 });
        Assert.Equal(lod0.VertexCount, meshes[0].VertexCount);
        Assert.Equal(lod0.Indices, meshes[0].Indices);
        for (int i = 0; i < lod0.VertexCount; i += 97) { Assert.Equal(lod0.Positions[i], meshes[0].Positions[i]); Assert.Equal(lod0.Uvs[i], meshes[0].Uvs[i]); }
        var maps = Aspis.Paint(spec, lod0);
        var (w, h, bas) = Png.Decode(images[0]);
        Assert.Equal((256, 256), (w, h));
        for (int i = 0; i < bas.Length; i += 4) { Assert.Equal(maps.BaseRgba[i], bas[i]); Assert.Equal(maps.BaseRgba[i + 2], bas[i + 2]); }
        Assert.Equal(maps.OrmRgba.Where((_, i) => i % 4 != 3), Png.Decode(images[2]).rgba.Where((_, i) => i % 4 != 3));
        Assert.True(r1.Triangles[0] > r1.Triangles[1] && r1.Triangles[1] > r1.Triangles[2], "LODs must decrease");
    }

    // ------------------------------------------------------------------ authored animation

    [CharsBakeFact]
    public void AuthoredClipsReachTheirHandTargets()
    {
        foreach (var id in new[] { "avatar", "a1", "b4", "commander_a" })
        {
            var (f, s) = Id(id);
            var k = KitSockets.Compute(f, s);
            var d = Dims.Of(s);
            var guard = CombatClips.HopliteGuardLoop(d);
            var clip = ClipBaker.Bake(guard, s, k);
            var rig = new CharRig(s); var p = new Pose(s.Count); var g = new Quaternion[s.Count]; var h = new Vector3[s.Count];
            ClipSampler.Sample(clip, 0f, p);
            Fk.Solve(s, p, g, h, rig.Pelvis);
            var key = guard.Keys[0];
            var sk = k["grip_r"].Local(s, g, h);
            Assert.True((sk.Translation - key.R!.Pos).Length() < 0.02f, $"{id}: right grip {(sk.Translation - key.R.Pos).Length() * 1000:F1} mm from its target");
            Assert.True(Vector3.Dot(Vector3.Normalize(sk.Up), Vector3.Normalize(key.R.Dir)) > MathF.Cos(MathHelper.ToRadians(5)), $"{id}: grip axis off its target direction");
            // feet on their targets, nothing below the ground
            Assert.True((h[rig.FootL] - key.FL!.Pos).Length() < 0.02f, $"{id}: left ankle {(h[rig.FootL] - key.FL.Pos).Length() * 1000:F1} mm off");
            foreach (var x in h) Assert.True(x.Y > -0.01f, $"{id}: a joint below the ground");
        }
    }

    [CharsBakeFact]
    public void AuthoredClipsAreDeterministic()
    {
        var (f, s) = Id("a2");
        var k = KitSockets.Compute(f, s);
        var d = Dims.Of(s);
        foreach (var a in new[] { CombatClips.HopliteThrust(d), CombatClips.OverheadCut(d, true), CombatClips.BowShoot(d) })
        {
            var c1 = ClipBaker.Bake(a, s, k); var c2 = ClipBaker.Bake(a, s, k);
            Assert.Equal(c1.Rotations, c2.Rotations);
            Assert.Equal(c1.Pelvis, c2.Pelvis);
            Assert.All(c1.Rotations, q => Assert.True(float.IsFinite(q.X + q.Y + q.Z + q.W) && MathF.Abs(q.Length() - 1) < 1e-3f));
        }
    }

    [CharsBakeFact]
    public void DeathsEndLyingOnTheGroundAndSurrenderKneels()
    {
        var (f, s) = Id("b2");
        var k = KitSockets.Compute(f, s);
        var d = Dims.Of(s);
        var rig = new CharRig(s);
        foreach (var a in new[] { CombatClips.DeathBack(d, "hoplite", false), CombatClips.DeathForward(d, "sword", true), CombatClips.Surrender(d, "hoplite", false) })
        {
            var clip = ClipBaker.Bake(a, s, k);
            var p = new Pose(s.Count); var g = new Quaternion[s.Count]; var h = new Vector3[s.Count];
            ClipSampler.Sample(clip, clip.Duration, p);
            Fk.Solve(s, p, g, h, rig.Pelvis);
            foreach (var x in h) Assert.True(x.Y > -0.01f, $"{a.Name}: a joint {x.Y:F3} m below the ground");
            if (a.Name.Contains("death")) Assert.True(h[rig.Pelvis].Y < 0.30f && h[rig.Head].Y < 0.35f, $"{a.Name}: pelvis {h[rig.Pelvis].Y:F2} m, head {h[rig.Head].Y:F2} m: not lying down");
            else Assert.True(h[rig.Pelvis].Y < 0.70f && h[rig.HandR].Y > h[rig.Head].Y - 0.25f, $"{a.Name}: pelvis {h[rig.Pelvis].Y:F2} m, hands not raised");
        }
    }

    // ------------------------------------------------------------------ worn and carried kit

    [CharsBakeFact]
    public void HelmetsHideTheHairUnderThemButNotTheFace()
    {
        var (f, s) = Id("avatar");
        var k = KitSockets.Compute(f, s);
        var m = KitSockets.Measure(f, s);
        var hair = f.Mesh("hair")!;
        foreach (var kind in new[] { "corinthian", "kettle", "nasal" })
        {
            var helm = Helmets.Build(new HelmetSpec { Kind = kind, MapSize = 0, Lod = 1 }, m);
            var kept = KitAssets.HairView(hair, new[] { (helm, "head", k["head"]) }, Helmets.Covers);
            Assert.True(kept.Length < hair.Indices.Length * 0.6, $"{kind}: kept {kept.Length / 3} of {hair.TriangleCount} hair triangles");
            // the crown is covered; a point in front of the eyes is not (eye openings, or no face guard at all)
            var head = k["head"].Bind.Translation;
            Assert.True(Helmets.Covers(helm, new Vector3(0, m.CrownY - head.Y - 0.01f, 0)), kind + ": the crown is not covered");
            Assert.False(Helmets.Covers(helm, new Vector3(0.034f, m.EyeY - head.Y, m.BrowZ - head.Z + 0.01f)), kind + ": the eye is covered");
        }
    }

    [CharsBakeFact]
    public void CarriedItemsHangFromTheirSockets()
    {
        var (f, s) = Id("a3");
        var k = KitSockets.Compute(f, s);
        var scabbard = Weapons.Build(new WeaponSpec { Kind = "scabbard", MapSize = 0, Lod = 1 });
        var w = Matrix.Invert(scabbard.Frame("hang")) * k["hip_l"].Bind;
        var throat = Vector3.Transform(Vector3.Zero, w);
        var tip = Vector3.Transform(new Vector3(0, -0.66f, 0), w);
        Assert.True((throat - k["hip_l"].Origin).Length() < 0.06f);
        Assert.True(tip.Y < throat.Y - 0.5f, "the scabbard should hang down from the hip");
        Assert.True(throat.X > 0, "hip_l is on the left (+x)");
    }

    [Fact]
    public void BowLimbsBendSoTheStringReachesTheHand()
    {
        foreach (float draw in new[] { 0.25f, 0.45f, 0.62f })
        {
            var nock = new Vector3(0, 0, BowRig.Brace.Z - draw);
            float bend = BowRig.SolveBend(nock);
            var tip = Vector3.Transform(BowRig.TipU, Matrix.CreateRotationX(-bend));
            Assert.True(MathF.Abs((tip - nock).Length() - BowRig.HalfString) < 0.002f, $"draw {draw}: string half {(tip - nock).Length():F3} vs {BowRig.HalfString:F3}");
            Assert.InRange(bend, 0.01f, 0.6f);
        }
        Assert.True(BowRig.SolveBend(new Vector3(0, 0, -0.80f)) > BowRig.SolveBend(new Vector3(0, 0, -0.45f)), "a longer draw bends the limbs more");
    }

    [Fact]
    public void KitLayerReferencesNoSimulationAssembly()
    {
        var refs = typeof(KitItem).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();
        Assert.DoesNotContain(refs, n => n.StartsWith("Sim.", StringComparison.Ordinal) || n.StartsWith("Proto", StringComparison.Ordinal));
        Assert.Contains("Gfx.Chars", refs);
    }

    // ------------------------------------------------------------------ rendering

    [CharsGfxFact]
    public void HeroSceneRendersTheAspis() => TestGfx.Host.Invoke(gd =>
    {
        var e = Gfx.Engine.Scene.SceneRegistry.Discover(typeof(Gfx.Demo.Program).Assembly).First(x => x.Name == "kit-hero");
        using var scene = e.Create();
        scene.Load(gd, TestGfx.Host.Effects);
        using var renderer = new Gfx.Engine.Rendering.ForwardRenderer(gd, TestGfx.Host.Effects);
        scene.Configure(renderer.Settings);
        var shot = scene.Shots.First(x => x.Name == "face");
        scene.Update(shot.SceneTime);
        using var rt = new Microsoft.Xna.Framework.Graphics.RenderTarget2D(gd, 320, 180, false, Microsoft.Xna.Framework.Graphics.SurfaceFormat.Color, Microsoft.Xna.Framework.Graphics.DepthFormat.None);
        renderer.Stats.BeginFrame();
        renderer.RenderStill(scene, shot.Camera, Gfx.Engine.Scene.SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
        renderer.Stats.EndFrame();
        var px = new Color[320 * 180];
        rt.GetData(px);
        // the middle of the frame is the bronze face and its painted device: warm (red over blue) and lit
        double r = 0, b = 0; int n = 0;
        for (int y = 60; y < 120; y++) for (int x = 110; x < 210; x++) { var c = px[y * 320 + x]; r += c.R; b += c.B; n++; }
        r /= n; b /= n;
        Assert.True(r > b + 15 && r > 50, $"mean centre colour R {r:F0} B {b:F0}: not the bronze aspis");
        Assert.True(renderer.Stats.LastDrawCalls > 0);
        return 0;
    });

    /// <summary>Draws a character stage (no terrain): the sky, then the stage's instances.</summary>
    sealed class StageProbe(Gfx.Chars.Rendering.CharStage stage) : Gfx.Engine.Rendering.IRenderContent
    {
        public void DrawShadowCasters(Gfx.Engine.Rendering.RenderContext rc) => stage.DrawShadows(rc);
        public void DrawOpaque(Gfx.Engine.Rendering.RenderContext rc) => stage.DrawOpaque(rc);
    }

    /// <summary>
    /// G2 integration of kit and render2: kit items are skinned character parts, so under TAA they write their object
    /// motion into the G-buffer like the body that wears or holds them. Without it the resolve reprojects them by depth
    /// alone (camera motion only), and the helmet, shield and spear of a walking soldier smear while his body stays sharp.
    /// A prop moved 10 cm along +x between two frames writes the flag that the resolve must use its motion, and that
    /// motion equals the projected move at the depth the G-buffer holds (within 5 %, no vertical motion).
    /// </summary>
    [CharsGfxFact]
    public void MovingKitItemsWriteTheirScreenMotion() => TestGfx.Host.Invoke(gd =>
    {
        const int W = 256, H = 256;
        using var stage = new Gfx.Chars.Rendering.CharStage(gd);
        using var kit = new KitWorld(gd, stage);
        var shield = Shields.Build(new ShieldSpec { Kind = "round", Seed = 3, MapSize = 64, Lod = 1 });
        var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
        foreach (var p in shield.Parts.SelectMany(q => q.Mesh.Positions)) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
        var world0 = Matrix.CreateTranslation(new Vector3(0, 1, 0) - (lo + hi) * 0.5f);
        var prop = kit.AddProp(shield, world0);
        using var r = new Gfx.Engine.Rendering.ForwardRenderer(gd, TestGfx.Host.Effects);
        r.Settings.Shadows = false;
        r.Settings.Features.SetMany("autoexposure=0,bloom=0,grade=0,aa=taa,farfield=0,ssao=0,contact=0");
        var cam = new Gfx.Engine.Cameras.CameraPose(new Vector3(0, 1f, 3f), new Vector3(0, 1f, 0), 0.7f, 0.1f, 200f);
        using var rt = new Microsoft.Xna.Framework.Graphics.RenderTarget2D(gd, W, H, false, Microsoft.Xna.Framework.Graphics.SurfaceFormat.Color, Microsoft.Xna.Framework.Graphics.DepthFormat.None);
        var probe = new StageProbe(stage);
        r.Render(probe, cam, Gfx.Engine.Scene.SunModel.At(11f), rt, 0f);   // first frame: no history, no motion
        prop.World = world0 * Matrix.CreateTranslation(0.1f, 0, 0);
        stage.Invalidate();
        r.Render(probe, cam, Gfx.Engine.Scene.SunModel.At(11f), rt, 1f / 30f);
        Assert.True(r.LastTaaHistoryKept, "the second frame keeps the TAA history (static camera)");
        var vel = new Microsoft.Xna.Framework.Graphics.PackedVector.HalfVector4[W * H];
        r.ScreenSpace!.Velocity!.GetData(vel);
        var depth = new float[W * H];
        r.ScreenSpace.Depth!.GetData(depth);
        int at = (H / 2) * W + W / 2;
        float d = depth[at];
        Assert.InRange(d, 2.5f, 3.5f);   // the shield covers the centre
        var c = vel[at].ToVector4();
        var sky = vel[3 * W + 3].ToVector4();
        Assert.Equal(0f, sky.Z);
        Assert.Equal(1f, c.Z);           // the resolve uses this pixel's object motion
        var vp = cam.View * cam.Projection(1f);
        var fwd = Vector3.Normalize(cam.Target - cam.Position);
        var pNow = cam.Position + fwd * d;
        var a0 = Vector4.Transform(new Vector4(pNow, 1), vp); var a1 = Vector4.Transform(new Vector4(pNow - new Vector3(0.1f, 0, 0), 1), vp);
        float expect = a0.X / a0.W - a1.X / a1.W;
        Assert.True(expect > 0.05f, $"expected motion {expect}");
        Assert.InRange(c.X, expect * 0.95f, expect * 1.05f);
        Assert.InRange(c.Y, -0.01f * expect, 0.01f * expect);
        return 0;
    });
}
