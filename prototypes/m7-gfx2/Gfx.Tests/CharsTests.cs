using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Gfx.Avatar;
using Gfx.Chars.Animation;
using Gfx.Chars.Format;
using Gfx.Chars.Rendering;
using Gfx.Engine.Assets;
using Microsoft.Xna.Framework;

namespace Gfx.Tests;

/// <summary>A Fact that SKIPS (not fails) when the stage-chars bake is missing (scripts/bake-chars.sh has not run).</summary>
public sealed class CharsBakeFactAttribute : FactAttribute
{
    public CharsBakeFactAttribute() { if (!CharStage.BakeExists()) Skip = "no character bake: run scripts/bake-chars.sh"; }
}

/// <summary>A device test that also needs the character bake: SKIPS without either.</summary>
public sealed class CharsGfxFactAttribute : FactAttribute
{
    public CharsGfxFactAttribute()
    {
        if (!TestGfx.Available) Skip = "no graphics device: " + TestGfx.Reason;
        else if (!CharStage.BakeExists()) Skip = "no character bake: run scripts/bake-chars.sh";
    }
}

/// <summary>Stage chars: the G2CH format, the baked identities and outfits, and the cosmetic animation layer.</summary>
public sealed class CharsTests
{
    static readonly string Dir = AssetRoot.Baked("chars");
    static readonly Dictionary<string, CharacterFile> Cache = new(StringComparer.Ordinal);
    static readonly object Lock = new();

    static CharacterFile Load(string id)
    {
        lock (Lock)
        {
            if (!Cache.TryGetValue(id, out var f)) { f = CharacterFile.Load(Path.Combine(Dir, id + ".g2ch")); Cache[id] = f; }
            return f;
        }
    }

    public static readonly string[] Identities = { "avatar", "commander_a", "a1", "a2", "a3", "a4", "b1", "b2", "b3", "b4" };
    static readonly string[] Eras = { "ancient", "medieval", "early_modern" };

    // ------------------------------------------------------------------ the G2CH container (populated round trip)

    /// <summary>Test-side writer mirroring tools/charbake/g2ch.py (little-endian chunks).</summary>
    sealed class Writer
    {
        readonly List<byte[]> _chunks = new();
        static byte[] Str(string s) { var b = Encoding.UTF8.GetBytes(s); var o = new byte[2 + b.Length]; BinaryPrimitives.WriteUInt16LittleEndian(o, (ushort)b.Length); b.CopyTo(o, 2); return o; }
        static byte[] U32(uint v) { var o = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(o, v); return o; }
        static byte[] I32(int v) { var o = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(o, v); return o; }
        static byte[] F(params float[] v) { var o = new byte[4 * v.Length]; for (int i = 0; i < v.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(o.AsSpan(4 * i), v[i]); return o; }
        void Chunk(string tag, params byte[][] parts)
        {
            var payload = parts.SelectMany(p => p).ToArray();
            _chunks.Add(Encoding.ASCII.GetBytes(tag).Concat(U32((uint)payload.Length)).Concat(payload).ToArray());
        }
        public void Info(string json) => Chunk("INFO", Encoding.UTF8.GetBytes(json));
        public void Skel((string n, int p, Vector3 h, Vector3 t)[] b) =>
            Chunk("SKEL", new[] { U32((uint)b.Length) }.Concat(b.Select(x => Str(x.n).Concat(I32(x.p)).Concat(F(x.h.X, x.h.Y, x.h.Z, x.t.X, x.t.Y, x.t.Z)).ToArray())).ToArray());
        public void Sock(string n, int bone, Vector3 p, Quaternion q) =>
            Chunk("SOCK", U32(1), Str(n), I32(bone), F(p.X, p.Y, p.Z, q.W, q.X, q.Y, q.Z));
        public void Mesh(MeshDef m)
        {
            var parts = new List<byte[]> { Str(m.Name), Str(m.Material), U32(m.Flags), U32((uint)m.VertexCount), U32((uint)m.Indices.Length) };
            parts.Add(m.Positions.SelectMany(v => F(v.X, v.Y, v.Z)).ToArray());
            parts.Add(m.Normals.SelectMany(v => F(v.X, v.Y, v.Z)).ToArray());
            parts.Add(m.Tangents.SelectMany(v => F(v.X, v.Y, v.Z, v.W)).ToArray());
            parts.Add(m.Uvs.SelectMany(v => F(v.X, v.Y)).ToArray());
            parts.Add(m.Joints); parts.Add(m.Weights); parts.Add(m.Colors);
            parts.Add(m.Indices.SelectMany(U32).ToArray());
            Chunk("MESH", parts.ToArray());
        }
        public void Morph(string mesh, MorphDef d) => Chunk("MRPH", Str(mesh), U32(1), Str(d.Name), U32((uint)d.Indices.Length),
            d.Indices.SelectMany(U32).ToArray(), d.DeltaPositions.SelectMany(v => F(v.X, v.Y, v.Z)).ToArray(), d.DeltaNormals.SelectMany(v => F(v.X, v.Y, v.Z)).ToArray());
        public void View(string mesh, string view, uint[] idx) => Chunk("VIEW", Str(mesh), Str(view), U32((uint)idx.Length), idx.SelectMany(U32).ToArray());
        public void Clip(ClipDef c) => Chunk("CLIP", Str(c.Name), U32(c.Loop ? 1u : 0u), F(c.Fps), U32((uint)c.Frames), U32((uint)c.Bones), F(c.Speed, 0f),
            c.Rotations.SelectMany(q => F(q.W, q.X, q.Y, q.Z)).ToArray(), c.Pelvis.SelectMany(v => F(v.X, v.Y, v.Z)).ToArray(), c.Contacts, Str(c.Source));
        public byte[] Bytes() => Encoding.ASCII.GetBytes("G2CH").Concat(U32(1)).Concat(U32((uint)_chunks.Count)).Concat(_chunks.SelectMany(c => c)).ToArray();
    }

    static MeshDef SampleMesh() => new()
    {
        Name = "body", Material = "skin", Flags = 0,
        Positions = new[] { new Vector3(0, 0, 0), new Vector3(0.1f, 0, 0), new Vector3(0, 0.2f, 0.05f) },
        Normals = new[] { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitY },
        Tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, -1), new Vector4(0, 0, 1, 1) },
        Uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.25f, 0.75f) },
        Joints = new byte[] { 0, 1, 0, 0, 1, 0, 0, 0, 1, 2, 3, 0 }, Weights = new byte[] { 200, 55, 0, 0, 255, 0, 0, 0, 100, 100, 55, 0 },
        Colors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128, 1, 2, 3, 4 }, Indices = new uint[] { 0, 1, 2 },
    };

    static byte[] SampleFile()
    {
        var w = new Writer();
        w.Info("{\"id\":\"t\",\"kind\":\"test\"}");
        w.Skel(new[] { ("Root", -1, Vector3.Zero, Vector3.UnitY), ("pelvis", 0, new Vector3(0, 1, 0), new Vector3(0, 1.1f, 0)), ("head", 1, new Vector3(0, 1.6f, 0), new Vector3(0, 1.8f, 0)), ("eye_l", 2, new Vector3(0.03f, 1.65f, 0.08f), new Vector3(0.03f, 1.65f, 0.1f)) });
        w.Sock("grip_r", 1, new Vector3(0.1f, -0.2f, 0.05f), Quaternion.Normalize(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f)));
        var m = SampleMesh();
        w.Mesh(m);
        w.Morph("body", new MorphDef("blink", new uint[] { 2 }, new[] { new Vector3(0, -0.004f, 0) }, new[] { new Vector3(0, 0, 0.01f) }));
        w.View("body", "kit", new uint[] { 0, 1, 2 });
        var rot = Enumerable.Range(0, 2 * 4).Select(i => Quaternion.Normalize(new Quaternion(0.01f * i, 0, 0, 1))).ToArray();
        w.Clip(new ClipDef { Name = "walk", Loop = true, Fps = 30, Frames = 2, Bones = 4, Speed = 1.4f, Rotations = rot, Pelvis = new[] { Vector3.Zero, new Vector3(0, 0.01f, 0) }, Contacts = new byte[] { 1, 2 }, Source = "test" });
        return w.Bytes();
    }

    [Fact]
    public void G2chPopulatedRoundTripIsBitExact()
    {
        var bytes = SampleFile();
        // exact length: header 12 + chunk headers 8 x 7 + payloads
        int expected = 12 + 7 * 8
            + Encoding.UTF8.GetByteCount("{\"id\":\"t\",\"kind\":\"test\"}")
            + 4 + (2 + 4 + 4 + 24) + (2 + 6 + 4 + 24) + (2 + 4 + 4 + 24) + (2 + 5 + 4 + 24)
            + 4 + 2 + 6 + 4 + 12 + 16
            + (2 + 4) + (2 + 4) + 12 + 3 * (12 + 12 + 16 + 8 + 4 + 4 + 4) + 12
            + (2 + 4) + 4 + (2 + 5) + 4 + 4 + 12 + 12
            + (2 + 4) + (2 + 3) + 4 + 12
            + (2 + 4) + 4 + 4 + 4 + 4 + 8 + 8 * 16 + 2 * 12 + 2 + (2 + 4);
        Assert.Equal(expected, bytes.Length);
        var f = CharacterFile.Parse(bytes);
        Assert.Equal(4, f.Bones.Count);
        Assert.Single(f.Sockets); Assert.Single(f.Meshes); Assert.Single(f.Views); Assert.Single(f.Clips); Assert.Empty(f.Unknown);
        Assert.Equal("blink", f.Morphs["body"][0].Name);
        // re-serialise what was parsed: identical bytes and hash
        var w = new Writer();
        w.Info(f.Info.ToJsonString());
        w.Skel(f.Bones.Select(b => (b.Name, b.Parent, b.Head, b.Tail)).ToArray());
        w.Sock(f.Sockets[0].Name, f.Sockets[0].Bone, f.Sockets[0].Position, f.Sockets[0].Rotation);
        w.Mesh(f.Meshes[0]);
        w.Morph("body", f.Morphs["body"][0]);
        w.View(f.Views[0].Mesh, f.Views[0].View, f.Views[0].Indices);
        w.Clip(f.Clips[0]);
        var again = w.Bytes();
        Assert.Equal(bytes, again);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), CharacterFile.Parse(again).Sha256);
    }

    [Fact]
    public void G2chRejectsTruncatedAndCorruptData()
    {
        var bytes = SampleFile();
        Assert.Throws<InvalidDataException>(() => CharacterFile.Parse(bytes.AsSpan(0, bytes.Length - 3).ToArray()));
        var bad = (byte[])bytes.Clone(); bad[0] = (byte)'X';
        Assert.Throws<InvalidDataException>(() => CharacterFile.Parse(bad));
        var extra = bytes.Concat(new byte[] { 0 }).ToArray();
        Assert.Throws<InvalidDataException>(() => CharacterFile.Parse(extra));
    }

    // ------------------------------------------------------------------ the bake

    [CharsBakeFact]
    public void BakeManifestMatchesTheFiles()
    {
        var lines = File.ReadAllLines(Path.Combine(Dir, "MANIFEST.tsv")).Where(l => !l.StartsWith('#')).ToArray();
        Assert.True(lines.Length >= 40, $"{lines.Length} manifest rows");
        foreach (var l in lines)
        {
            var c = l.Split('\t');
            var data = File.ReadAllBytes(Path.Combine(Dir, c[0]));
            Assert.Equal(long.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture), data.LongLength);
            Assert.Equal(c[1], Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant());
        }
    }

    [CharsBakeFact]
    public void EveryBakedFileParsesCompletely()
    {
        foreach (var path in Directory.GetFiles(Dir, "*.g2ch").OrderBy(p => p, StringComparer.Ordinal))
        {
            var f = CharacterFile.Load(path);
            Assert.Empty(f.Unknown);
            if (path.EndsWith("__clips.g2ch", StringComparison.Ordinal)) Assert.True(f.Clips.Count > 0, path);
            else Assert.True(f.Meshes.Count > 0, path);
        }
    }

    /// <summary>Provenance: identity and outfit files hold only CC0 and generated data; the CMU clips are in their own
    /// PROTOTYPE-ONLY files, which name the identity they were retargeted to.</summary>
    [CharsBakeFact]
    public void ClipsLiveInTheirOwnPrototypeOnlyFiles()
    {
        foreach (var id in Identities)
        {
            var f = Load(id);
            Assert.Empty(f.Clips);
            var c = Load(id + "__clips");
            Assert.True(c.Clips.Count >= 18, id);
            Assert.StartsWith("PROTOTYPE-ONLY", c.Info["status"]!.GetValue<string>());
            Assert.Equal(f.Sha256.ToLowerInvariant(), c.Info["identity_sha256"]!.GetValue<string>());
            foreach (var clip in c.Clips) Assert.Equal(f.Bones.Count, clip.Bones);
        }
    }

    [CharsBakeFact]
    public void AvatarStartsFromTheG1PlayerFixture()
    {
        var fx = AvatarProfile.PlayerFixture();
        var info = Load("avatar").Info["fixture"]!.AsObject();
        Assert.Equal(fx.Id, info["id"]!.GetValue<string>());
        Assert.Equal(fx.DisplayName, info["displayName"]!.GetValue<string>());
        Assert.Equal(fx.Body.HeightMm, info["body"]!["heightMm"]!.GetValue<int>());
        Assert.Equal(fx.Body.BuildPermille, info["body"]!["buildPermille"]!.GetValue<int>());
        Assert.Equal(fx.Body.ShoulderPermille, info["body"]!["shoulderPermille"]!.GetValue<int>());
        Assert.Equal(fx.FaceMorphs, info["faceMorphs"]!.AsArray().Select(n => n!.GetValue<int>()).ToArray());
        Assert.Equal(fx.SkinTone, info["skinTone"]!.GetValue<string>());
        Assert.Equal(fx.EyeColour, info["eyeColour"]!.GetValue<string>());
        Assert.Equal(fx.HairColour, info["hairColour"]!.GetValue<string>());
        Assert.Equal(fx.Hair.ToString(), info["hair"]!.GetValue<string>());
        Assert.Equal(fx.FacialHair.ToString(), info["facialHair"]!.GetValue<string>());
        // the stature the fixture asks for, within a centimetre
        Assert.InRange(Load("avatar").Info["stature_m"]!.GetValue<double>(), 1.79, 1.81);
    }

    /// <summary>
    /// The avatar's identity persists across eras: the three era outfits name the one identity file by its sha256 and
    /// carry no face or body data of their own (garments, armour, equipment, LODs and index views only); every face
    /// triangle the identity has is visible in every era (views only hide skin under clothing).
    /// </summary>
    [CharsBakeFact]
    public void EraOutfitsShareTheAvatarsFaceAndBody()
    {
        var id = Load("avatar");
        var identityParts = new[] { "body", "eyes", "cornea", "teeth_upper", "teeth_lower", "tongue", "hair", "beard", "brows", "lashes", "ponytail" };
        var body = id.Mesh("body")!;
        int head = id.BoneIndex("head");
        float neckY = id.Bones[id.BoneIndex("neck_01")].Head.Y;
        var faceTris = new HashSet<(uint, uint, uint)>();
        for (int i = 0; i < body.Indices.Length; i += 3)
        {
            uint a = body.Indices[i], b = body.Indices[i + 1], c = body.Indices[i + 2];
            if (body.Positions[a].Y > neckY + 0.04f && body.Positions[b].Y > neckY + 0.04f && body.Positions[c].Y > neckY + 0.04f) faceTris.Add((a, b, c));
        }
        Assert.True(faceTris.Count > 2000, $"{faceTris.Count} head triangles");
        foreach (var era in Eras)
        {
            var o = Load("avatar__era_" + era);
            Assert.Equal(id.Sha256.ToLowerInvariant(), o.Info["identity_sha256"]!.GetValue<string>());
            Assert.Equal("avatar", o.Info["identity"]!.GetValue<string>());
            foreach (var m in o.Meshes) Assert.DoesNotContain(m.Name, identityParts);
            Assert.Empty(o.Bones);
            var view = o.Views.Single(v => v.Mesh == "body").Indices;
            var vis = new HashSet<(uint, uint, uint)>();
            for (int i = 0; i < view.Length; i += 3) vis.Add((view[i], view[i + 1], view[i + 2]));
            Assert.True(vis.IsSubsetOf(Enumerable.Range(0, body.Indices.Length / 3).Select(t => (body.Indices[3 * t], body.Indices[3 * t + 1], body.Indices[3 * t + 2]))));
            Assert.True(faceTris.IsSubsetOf(vis), $"{era}: a face triangle is hidden");
        }
        Assert.True(head >= 0);
    }

    [CharsBakeFact]
    public void TwoForcesOfDistinctIdentities()
    {
        var shas = new HashSet<string>();
        var faces = new List<Vector3[]>();
        foreach (var id in Identities)
        {
            var f = Load(id);
            Assert.True(shas.Add(f.Sha256), id + " duplicates another identity file");
            faces.Add(f.Mesh("body")!.Positions);
            Assert.Equal(new[] { "battlecry", "blink", "calm", "grim", "pain" }, f.Morphs["body"].Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }
        // every pair of bodies differs by more than 1.5 cm somewhere, and every pair of faces by more than 3 mm once the
        // heads are aligned at the eyes (no cloned geometry: body proportions and face modifiers both vary)
        for (int i = 0; i < faces.Count; i++)
            for (int j = i + 1; j < faces.Count; j++)
            {
                float max = 0;
                for (int k = 0; k < faces[i].Length; k++) max = MathF.Max(max, (faces[i][k] - faces[j][k]).Length());
                Assert.True(max > 0.015f, $"{Identities[i]} vs {Identities[j]}: max body difference {max} m");
                var si = Load(Identities[i]); var sj = Load(Identities[j]);
                Vector3 Eyes(CharacterFile f) => 0.5f * (f.Bones[f.BoneIndex("eye_l")].Head + f.Bones[f.BoneIndex("eye_r")].Head);
                var ei = Eyes(si); var ej = Eyes(sj);
                float face = 0;
                for (int k = 0; k < faces[i].Length; k++)
                    if (faces[i][k].Y > ei.Y - 0.12f && faces[i][k].Z > ei.Z - 0.06f)
                        face = MathF.Max(face, ((faces[i][k] - ei) - (faces[j][k] - ej)).Length());
                Assert.True(face > 0.003f, $"{Identities[i]} vs {Identities[j]}: max face difference {face} m");
            }
        foreach (var id in new[] { "a1", "a2", "a3", "a4" }) Assert.Equal("A", Load(id + "__kit_a").Info["force"]!.GetValue<string>());
        foreach (var id in new[] { "b1", "b2", "b3", "b4" }) Assert.Equal("B", Load(id + "__kit_b").Info["force"]!.GetValue<string>());
        Assert.Equal("A", Load("commander_a__kit_a").Info["force"]!.GetValue<string>());
    }

    [CharsBakeFact]
    public void LodsMeetTheirTriangleBudgets()
    {
        foreach (var path in Directory.GetFiles(Dir, "*__*.g2ch").Where(p => !p.EndsWith("__clips.g2ch", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal))
        {
            var f = CharacterFile.Load(path);
            int l1 = f.Mesh("lod1")!.TriangleCount, l2 = f.Mesh("lod2")!.TriangleCount;
            Assert.InRange(l1, 2400, 3600);
            Assert.InRange(l2, 1100, 1800);
            Assert.Equal("lod", f.Mesh("lod1")!.Material);
        }
    }

    /// <summary>
    /// Fur shells (scalp hair and beards) are stacked offset copies of a skin region: every triangle lies within one
    /// layer (its three vertices carry the same layer fraction in vertex colour A), each layer has the same triangle
    /// count, and the layers rise monotonically from the skin.
    /// </summary>
    [CharsBakeFact]
    public void FurShellTrianglesStayWithinTheirLayer()
    {
        int checkedMeshes = 0;
        foreach (var id in Identities)
            foreach (var name in new[] { "hair", "beard" })
            {
                var m = Load(id).Mesh(name);
                if (m == null || (m.Material != "fur" && m.Material != "hairshell")) continue;
                checkedMeshes++;
                var perLayer = new SortedDictionary<byte, int>();
                for (int i = 0; i < m.Indices.Length; i += 3)
                {
                    byte a = m.Colors[4 * m.Indices[i] + 3], b = m.Colors[4 * m.Indices[i + 1] + 3], c = m.Colors[4 * m.Indices[i + 2] + 3];
                    Assert.True(a == b && b == c, $"{id}/{name}: triangle {i / 3} spans layers {a}, {b}, {c}");
                    perLayer[a] = perLayer.GetValueOrDefault(a) + 1;
                }
                Assert.True(perLayer.Count >= 3, $"{id}/{name}: {perLayer.Count} layers");
                Assert.Single(perLayer.Values.Distinct());
            }
        Assert.True(checkedMeshes >= 12, $"{checkedMeshes} shell meshes checked");
    }

    [CharsBakeFact]
    public void ExpressionsAreBoundedAndBlinkClosesTheLids()
    {
        var f = Load("avatar");
        var body = f.Mesh("body")!;
        foreach (var m in f.Morphs["body"])
        {
            Assert.True(m.Indices.Length > 50, m.Name);
            // the largest motion is the jaw in the battle cry (about 3 cm); nothing may jump further
            Assert.True(m.DeltaPositions.Max(d => d.Length()) < 0.05f, m.Name + " moves a vertex more than 5 cm");
        }
        // blink: the vertices it moves most go down (upper lids close)
        var blink = f.Morphs["body"].Single(m => m.Name == "blink");
        var top = blink.DeltaPositions.Select((d, i) => (d, i)).OrderByDescending(x => x.d.Length()).Take(20).ToArray();
        Assert.All(top, x => Assert.True(x.d.Y < 0, "an upper-lid vertex moves up"));
        // the battle cry opens the jaw: the lower teeth move down
        var cry = f.Morphs["teeth_lower"].Single(m => m.Name == "battlecry");
        Assert.True(cry.DeltaPositions.Average(d => d.Y) < -0.005f);
    }

    // ------------------------------------------------------------------ animation (cosmetic, presentation only)

    /// <summary>An identity's clip file and its skeleton (the clips live in a separate PROTOTYPE-ONLY file).</summary>
    static (CharacterFile f, Skeleton s) Rig(string id = "a1") => (Load(id + "__clips"), new Skeleton(Load(id).Bones));

    static Matrix MovingRoot(float speed, float t) => Matrix.CreateTranslation(0, 0, speed * t);

    [CharsBakeFact]
    public void CrossfadeTakesTwoTenthsOfASecond()
    {
        var (f, s) = Rig();
        var a = new CharAnimator(f, s);
        const float dt = 1f / 60f;
        float t = 0;
        var idle = new AnimInput { Speed = 0 };
        var act = new AnimInput { Action = "thrust", ActionStart = 1.0f };
        for (; t < 1.0f - 1e-4f; t += dt) a.Step(t, dt, t >= 1.0f ? act : idle, Matrix.Identity);
        var weights = new List<(float t, float w)>();
        for (int k = 0; k <= 15; k++) { t = 1.0f + k * dt; a.Step(t, dt, act, Matrix.Identity); weights.Add((t - 1.0f, a.LastCrossfadeWeight)); }
        Assert.True(weights.First(x => x.t > 0.09f && x.t < 0.11f).w is > 0.2f and < 0.8f);
        Assert.Equal(1f, weights.First(x => x.t >= CharAnimator.CrossfadeSeconds - 1e-4f).w, 5);
        Assert.True(weights.Where(x => x.t < CharAnimator.CrossfadeSeconds - dt).All(x => x.w < 1f));
    }

    /// <summary>
    /// Stride sync and foot locking on a moving root: the clip is in place and the root carries the travel, so a
    /// planted foot should stay still in the world. Without the root moving (in-place playback, a static instance)
    /// the planted foot slides back at the walking speed, which is what a slide measurement reports when the root
    /// does not travel.
    /// </summary>
    [CharsBakeFact]
    public void PlantedFeetStayStillOnAMovingRoot()
    {
        var (f, s) = Rig();
        const float dt = 1f / 60f, v = 1.45f;
        SlideStats Run(bool lockFeet, bool moving)
        {
            var a = new CharAnimator(f, s);
            var inp = new AnimInput { Speed = v, FootLock = lockFeet };
            for (int i = 0; i < 360; i++) { float t = i * dt; a.Step(t, dt, inp, moving ? MovingRoot(v, t) : Matrix.Identity); }
            return a.Slide;
        }
        var unlocked = Run(false, true);
        var locked = Run(true, true);
        var inPlace = Run(false, false);
        Assert.True(unlocked.Samples > 100);
        Assert.True(unlocked.MeanBefore < 0.25f, $"stride-synchronised planted foot slides {unlocked.MeanBefore:F3} m/s");
        Assert.True(locked.MeanAfter < 0.02f, $"locked planted foot slides {locked.MeanAfter:F4} m/s");
        Assert.InRange(inPlace.MeanBefore, 0.7f * v, 1.3f * v);
    }

    [CharsBakeFact]
    public void HitReactionIsAdditiveAndDecaysToTheBasePose()
    {
        var (f, s) = Rig();
        const float dt = 1f / 60f;
        var a = new CharAnimator(f, s); var b = new CharAnimator(f, s);
        var plain = new AnimInput { Speed = 0 };
        var hit = new AnimInput { Speed = 0, HitTime = 1.0f, HitYaw = 0.4f };
        float maxDiff = 0, endDiff = 0;
        int spine = s["spine_03"];
        for (int i = 0; i < 180; i++)
        {
            float t = i * dt;
            a.Step(t, dt, plain, Matrix.Identity); b.Step(t, dt, hit, Matrix.Identity);
            float d = 2 * MathF.Acos(Math.Clamp(MathF.Abs(Quaternion.Dot(a.Pose.Local[spine], b.Pose.Local[spine])), 0f, 1f));
            if (t >= 1.0f && t < 1.3f) maxDiff = MathF.Max(maxDiff, d);
            if (t > 2.9f) endDiff = MathF.Max(endDiff, d);
            if (t < 1.0f) Assert.True(d < 1e-5f, "the hit layer acts before its start time");
        }
        Assert.True(maxDiff > MathHelper.ToRadians(5), $"peak hit offset {MathHelper.ToDegrees(maxDiff):F1} deg");
        Assert.True(endDiff < MathHelper.ToRadians(0.05f), $"residual {MathHelper.ToDegrees(endDiff):F3} deg");
    }

    [CharsBakeFact]
    public void UpperBodyMaskLeavesTheLegsAlone()
    {
        var (f, s) = Rig();
        const float dt = 1f / 60f;
        var a = new CharAnimator(f, s); var b = new CharAnimator(f, s);
        var walk = new AnimInput { Speed = 1.4f, FootLock = false };
        var layered = new AnimInput { Speed = 1.4f, FootLock = false, UpperClip = "idle", UpperClipTime = 1.0f, UpperWeight = 1f };
        for (int i = 0; i < 90; i++) { float t = i * dt; a.Step(t, dt, walk, Matrix.Identity); b.Step(t, dt, layered, Matrix.Identity); }
        foreach (var leg in new[] { "pelvis", "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r" })
            Assert.True(MathF.Abs(MathF.Abs(Quaternion.Dot(a.Pose.Local[s[leg]], b.Pose.Local[s[leg]])) - 1f) < 1e-6f, leg + " changed");
        float arm = MathF.Abs(Quaternion.Dot(a.Pose.Local[s["upperarm_l"]], b.Pose.Local[s["upperarm_l"]]));
        Assert.True(arm < 0.9999f, "the arm layer did nothing");
    }

    [CharsBakeFact]
    public void HeadLookIsClampedToNeckLimits()
    {
        var (f, s) = Rig();
        const float dt = 1f / 60f;
        var a = new CharAnimator(f, s);
        // a target far behind the right shoulder (about 150 degrees round)
        var inp = new AnimInput { Speed = 0, LookAt = new Vector3(-2.0f, 1.6f, -3.5f) };
        for (int i = 0; i < 60; i++) a.Step(i * dt, dt, inp, Matrix.Identity);
        var g = new Quaternion[s.Count]; var h = new Vector3[s.Count];
        Fk.Solve(s, a.Pose, g, h, s["pelvis"]);
        var fwd = Vector3.Transform(Vector3.UnitZ, g[s["head"]]);
        float yaw = MathF.Atan2(fwd.X, fwd.Z);
        // the target is about 150 degrees round; the chain's limit is 75 degrees in total (spine, neck and head)
        Assert.InRange(MathHelper.ToDegrees(yaw), -80f, -60f);
    }

    [Fact]
    public void TwoBoneIkReachesAReachableTarget()
    {
        var bones = new List<BoneDef>
        {
            new("Root", -1, Vector3.Zero, Vector3.UnitY), new("thigh", 0, new Vector3(0, 1, 0), new Vector3(0, 0.55f, 0)),
            new("calf", 1, new Vector3(0, 0.55f, 0.02f), new Vector3(0, 0.1f, 0)), new("foot", 2, new Vector3(0, 0.1f, 0), new Vector3(0, 0, 0.15f)),
        };
        var s = new Skeleton(bones); var p = new Pose(4);
        var g = new Quaternion[4]; var h = new Vector3[4];
        Fk.Solve(s, p, g, h, -1);
        var target = new Vector3(0.1f, 0.25f, 0.2f);
        float err = Ik.TwoBone(s, p, g, h, 1, 2, 3, target);
        Assert.True(err < 1e-3f, $"reach error {err} m");
        // lengths are kept
        Assert.Equal((bones[2].Head - bones[1].Head).Length(), (h[2] - h[1]).Length(), 4);
        Assert.Equal((bones[3].Head - bones[2].Head).Length(), (h[3] - h[2]).Length(), 4);
    }

    /// <summary>
    /// Sockets: the grip socket's world transform, posed, carries the weapon. The sword grip is baked rigid to the
    /// socket's bone around the socket origin, so after any pose its skinned centroid must sit at SocketWorld's
    /// translation (within the grip's own offset from the socket at rest).
    /// </summary>
    [CharsGfxFact]
    public void SocketWorldCarriesTheSwordGrip()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var stage = new CharStage(gd);
            var inst = stage.Spawn("avatar", "era_medieval");
            var outfit = inst.Outfits[0].File;
            var grip = outfit.Mesh("sword_grip")!;
            var sock = inst.Asset.File.Sockets.Single(x => x.Name == "grip_r");
            Assert.All(Enumerable.Range(0, grip.VertexCount), i => { Assert.Equal(sock.Bone, grip.Joints[4 * i]); Assert.Equal(255, grip.Weights[4 * i]); });
            var c = Vector3.Zero; foreach (var p in grip.Positions) c += p; c /= grip.VertexCount;
            var sockBind = inst.Asset.Skeleton.Head[sock.Bone] + sock.Position;
            float restOffset = (c - sockBind).Length();
            Assert.True(restOffset < 0.12f, $"grip centroid {restOffset} m from the socket at rest");
            // pose: raise and twist the right arm, move the instance
            var s = inst.Asset.Skeleton;
            inst.Pose.Local[s["upperarm_r"]] = Quaternion.CreateFromYawPitchRoll(0.4f, -1.1f, 0.3f);
            inst.Pose.Local[s["lowerarm_r"]] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.9f);
            inst.World = Matrix.CreateRotationY(0.7f) * Matrix.CreateTranslation(3, 0.2f, -2);
            inst.SolvePose();
            var skinned = Vector3.Transform(c, inst.Skin[sock.Bone]);
            var sw = inst.SocketWorld("grip_r");
            // the grip in the socket's own frame is the same at rest and posed: the weapon rides on the socket
            var rest = Matrix.CreateFromQuaternion(sock.Rotation) * Matrix.CreateTranslation(sockBind);
            var localRest = Vector3.Transform(c, Matrix.Invert(rest));
            var localPosed = Vector3.Transform(skinned, Matrix.Invert(sw));
            Assert.True((localRest - localPosed).Length() < 1e-3f, $"rest {localRest} posed {localPosed}");
            // the pose moved it (the check is not trivially at rest), and the socket axes stay orthonormal
            Assert.True((skinned - c).Length() > 0.5f);
            Assert.Equal(1f, sw.Up.Length(), 3); Assert.Equal(0f, Vector3.Dot(sw.Up, sw.Forward), 3);
        });
    }

    /// <summary>At runtime the three era instances draw one identity asset: the same skeleton, body vertex buffer, skin
    /// and eye textures; only the outfit assets differ, and each era only hides skin under its own clothes.</summary>
    [CharsGfxFact]
    public void ErasShareOneIdentityAssetAtRuntime()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var stage = new CharStage(gd);
            var inst = Eras.Select(e => stage.Spawn("avatar", "era_" + e)).ToArray();
            Assert.Same(inst[0].Asset, inst[1].Asset);
            Assert.Same(inst[0].Asset, inst[2].Asset);
            var body = inst[0].Asset.Part("body")!;
            Assert.All(inst, i => Assert.Same(body.Vertices, i.Asset.Part("body")!.Vertices));
            Assert.All(inst, i => Assert.Same(inst[0].Asset.Textures["skin"], i.Asset.Textures["skin"]));
            Assert.All(inst, i => Assert.Same(inst[0].Asset.Textures["eye"], i.Asset.Textures["eye"]));
            Assert.Equal(3, inst.Select(i => i.Outfits[0]).Distinct().Count());
            Assert.Equal(3, inst.Select(i => i.PartViews["body"]).Distinct().Count());
        });
    }

    /// <summary>
    /// The ancient helmet's cheek plates trim the beard under them only while it is worn: the outfit carries a beard
    /// view with fewer triangles than the full beard (and more than none: the chin stays bearded); bare-headed, the
    /// full beard and scalp hair come back and the helmet is hidden. The medieval kettle hat (a level rim) trims the
    /// scalp hair and leaves the beard whole.
    /// </summary>
    [CharsGfxFact]
    public void CheekPlatesTrimTheBeardOnlyWhileWorn()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var stage = new CharStage(gd);
            var inst = stage.Spawn("avatar", "era_ancient");
            var beard = inst.Asset.Part("beard")!;
            Assert.True(inst.PartViews.TryGetValue("beard", out var view), "the ancient outfit trims the beard");
            Assert.InRange(beard.Views[view!].tris, 1, beard.TriangleCount - 1);
            Assert.True(inst.PartViews.ContainsKey("hair"));
            inst.SetHeadgear(false);
            Assert.False(inst.PartViews.ContainsKey("beard"));
            Assert.False(inst.PartViews.ContainsKey("hair"));
            Assert.Contains("helmet", inst.HiddenParts);
            inst.SetHeadgear(true);
            Assert.Equal(view, inst.PartViews["beard"]);
            Assert.DoesNotContain("helmet", inst.HiddenParts);
            var med = stage.Spawn("avatar", "era_medieval");
            Assert.True(med.PartViews.ContainsKey("hair"));
            Assert.False(med.PartViews.ContainsKey("beard"));
        });
    }

    /// <summary>
    /// Layered skirts are pushed out of the legs in layers, innermost first: the commander's under-gambeson, mail and
    /// surcoat skirts and the ancient tunic and leather strips get increasing push-out radii, so in a stride an inner
    /// skirt cannot land on an outer one (one shared capsule put the mail skirt through the commander's surcoat).
    /// </summary>
    [CharsGfxFact]
    public void LayeredSkirtsArePushedOutInLayerOrder()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var stage = new CharStage(gd);
            foreach (var (id, outfit, order) in new[] {
                ("commander_a", "kit_a", new[] { "gambeson_skirt", "mail_skirt", "surcoat_skirt" }),
                ("avatar", "era_ancient", new[] { "tunic_skirt", "pteruges" }) })
            {
                var inst = stage.Spawn(id, outfit);
                var skirts = inst.Outfits[0].Parts.Where(p => (p.Def.Flags & 1) != 0).ToArray();
                Assert.Equal(order, skirts.Select(p => p.Name).ToArray());
                Assert.Equal(Enumerable.Range(0, order.Length), skirts.Select(p => CharMaterials.SkirtLayer(inst, p)));
            }
        });
    }

    /// <summary>
    /// Skirt push-out capsules clear the legwear: for every soldier and the commander (all in hose), the thigh capsule
    /// is at least 3 mm wider than the hose's outermost point around the thigh bone; and for at least one of them that
    /// point lies outside the old capsule (the skin's 90th-percentile radius plus 10 mm), which let the hose through a
    /// skirt (b4's levy tunic showed two dark tongues of hose).
    /// </summary>
    [CharsGfxFact]
    public void SkirtCapsulesClearTheLegwear()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var stage = new CharStage(gd);
            var mats = new CharMaterials();
            float worstGap = 0f;
            foreach (var (id, outfit) in new[] { ("a1", "kit_a"), ("a2", "kit_a"), ("a3", "kit_a"), ("a4", "kit_a"), ("b1", "kit_b"),
                         ("b2", "kit_b"), ("b3", "kit_b"), ("b4", "kit_b"), ("commander_a", "kit_a") })
            {
                var inst = stage.Spawn(id, outfit);
                var o = inst.Outfits[0];
                var s = inst.Asset.Skeleton;
                var (thigh, _) = mats.LegRadii(inst.Asset, o);
                float hose = Radii(o.Part("hose")!.Def, s, "thigh_l", "calf_l").Max();
                var skin = Radii(inst.Asset.File.Mesh("body")!, s, "thigh_l", "calf_l");
                Assert.True(thigh >= hose + 0.003f, $"{id}: thigh capsule {thigh:F4} m, hose outermost {hose:F4} m");
                float oldCapsule = skin[(int)(skin.Count * 0.9f)] + 0.010f;
                worstGap = Math.Max(worstGap, hose - oldCapsule);
            }
            Assert.True(worstGap > 0f, $"hose outermost point beyond the old skin capsule by {worstGap:F4} m at most");
        });

        static List<float> Radii(MeshDef m, Skeleton s, string bone, string child)
        {
            int b = s[bone]; var A = s.Head[b]; var ab = s.Head[s[child]] - A;
            var d = new List<float>();
            for (int i = 0; i < m.VertexCount; i++)
            {
                if (m.Joints[4 * i] != b || m.Weights[4 * i] < 180) continue;
                float t = Math.Clamp(Vector3.Dot(m.Positions[i] - A, ab) / ab.LengthSquared(), 0f, 1f);
                if (t < 0.15f || t > 0.85f) continue;
                d.Add((m.Positions[i] - (A + ab * t)).Length());
            }
            d.Sort();
            return d;
        }
    }

    /// <summary>The hero scene renders a dressed character on the meadow (skips without a device or a bake).</summary>
    [CharsGfxFact]
    public void HeroSceneRendersADressedCharacter()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new Gfx.Demo.Scenes.CharsHeroScene();
            scene.Load(gd, TestGfx.Host.Effects);
            using var r = new Gfx.Engine.Rendering.ForwardRenderer(gd, TestGfx.Host.Effects);
            scene.Configure(r.Settings);
            using var rt = new Microsoft.Xna.Framework.Graphics.RenderTarget2D(gd, 320, 180, false, Microsoft.Xna.Framework.Graphics.SurfaceFormat.Color, Microsoft.Xna.Framework.Graphics.DepthFormat.None);
            var shot = scene.Shots[0];
            scene.Update(shot.SceneTime);
            r.Stats.BeginFrame();
            r.Render(scene, shot.Camera, Gfx.Engine.Scene.SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
            r.Stats.EndFrame();
            var px = Gfx.Engine.Diagnostics.Capture.ReadPixels(rt);
            Assert.True(Gfx.Engine.Diagnostics.Capture.DistinctColors(px) > 2000);
            Assert.True(r.Stats.LastTriangles > 100_000, $"{r.Stats.LastTriangles} triangles");
            // the centre column (the character) differs from the sky and the ground around it
            int centre = 0;
            for (int y = 40; y < 150; y++) { var p = px[y * 320 + 160]; var g = px[y * 320 + 20]; if (Math.Abs(p.R - g.R) + Math.Abs(p.G - g.G) + Math.Abs(p.B - g.B) > 40) centre++; }
            Assert.True(centre > 60, $"{centre} character pixels in the centre column");
        });
    }

    /// <summary>Rendering never influences the simulation (DR42): the character layer references no simulation assembly.</summary>
    [Fact]
    public void CharacterLayerReferencesNoSimulationAssembly()
    {
        var refs = typeof(CharAnimator).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();
        Assert.DoesNotContain(refs, n => n.StartsWith("Sim.", StringComparison.Ordinal));
        Assert.Contains("Gfx.Engine", refs);
    }
}
