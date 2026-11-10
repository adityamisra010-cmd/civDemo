using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;

namespace Gfx.Chars.Format;

/// <summary>One bone of the baked skeleton. Rest frames are world-aligned: a bone is a head position and a parent.</summary>
public sealed record BoneDef(string Name, int Parent, Vector3 Head, Vector3 Tail);

/// <summary>An attachment point (weapon grip, shield strap) in a bone's rest frame.</summary>
public sealed record SocketDef(string Name, int Bone, Vector3 Position, Quaternion Rotation);

/// <summary>A baked mesh part (CPU arrays). Weights are bytes summing to 255.</summary>
public sealed class MeshDef
{
    public required string Name { get; init; }
    public required string Material { get; init; }
    public uint Flags { get; init; }
    public required Vector3[] Positions { get; init; }
    public required Vector3[] Normals { get; init; }
    public required Vector4[] Tangents { get; init; }
    public required Vector2[] Uvs { get; init; }
    public required byte[] Joints { get; init; }   // 4 per vertex
    public required byte[] Weights { get; init; }  // 4 per vertex, sum 255
    public required byte[] Colors { get; init; }   // 4 per vertex (RGBA)
    public required uint[] Indices { get; init; }
    public int VertexCount => Positions.Length;
    public int TriangleCount => Indices.Length / 3;
}

/// <summary>A sparse blend shape over one mesh.</summary>
public sealed record MorphDef(string Name, uint[] Indices, Vector3[] DeltaPositions, Vector3[] DeltaNormals);

/// <summary>An outfit's index subset of an identity mesh (visible skin under garments, hair under a helmet).</summary>
public sealed record ViewDef(string Mesh, string View, uint[] Indices);

/// <summary>A baked clip: local rotations per frame and bone, pelvis offset per frame, planted-foot bits.</summary>
public sealed class ClipDef
{
    public required string Name { get; init; }
    public bool Loop { get; init; }
    public float Fps { get; init; }
    public int Frames { get; init; }
    public int Bones { get; init; }
    /// <summary>Natural travel speed of a locomotion cycle (m/s); 0 otherwise.</summary>
    public float Speed { get; init; }
    public required Quaternion[] Rotations { get; init; } // frame-major: [frame * Bones + bone]
    public required Vector3[] Pelvis { get; init; }
    public required byte[] Contacts { get; init; }        // bit 1 left planted, bit 2 right planted
    public required string Source { get; init; }
    public float Duration => Frames / Fps;
}

/// <summary>
/// Reader of the G2CH container written by <c>tools/charbake/g2ch.py</c>. Little-endian; unknown chunks are kept as
/// raw bytes (<see cref="Unknown"/>), so readers and writers can evolve separately.
/// </summary>
public sealed class CharacterFile
{
    public const uint Magic = 0x48433247; // "G2CH"
    public const int Version = 1;

    public JsonObject Info { get; private set; } = new();
    public List<BoneDef> Bones { get; } = new();
    public List<SocketDef> Sockets { get; } = new();
    public List<MeshDef> Meshes { get; } = new();
    public Dictionary<string, List<MorphDef>> Morphs { get; } = new(StringComparer.Ordinal);
    public List<ViewDef> Views { get; } = new();
    public List<ClipDef> Clips { get; } = new();
    public List<(string tag, byte[] data)> Unknown { get; } = new();
    /// <summary>SHA-256 of the file bytes (hex), for identity checks across outfits.</summary>
    public string Sha256 { get; private set; } = "";

    public static CharacterFile Load(string path) => Parse(File.ReadAllBytes(path));

    public static CharacterFile Parse(byte[] bytes)
    {
        var f = new CharacterFile { Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) };
        var r = new Reader(bytes);
        if (r.U32() != Magic) throw new InvalidDataException("not a G2CH file");
        int ver = (int)r.U32();
        if (ver != Version) throw new InvalidDataException($"G2CH version {ver} (expected {Version})");
        int chunks = (int)r.U32();
        for (int c = 0; c < chunks; c++)
        {
            string tag = Encoding.ASCII.GetString(r.Bytes(4));
            int len = (int)r.U32();
            var sub = new Reader(r.Bytes(len));
            switch (tag)
            {
                case "INFO": f.Info = JsonNode.Parse(Encoding.UTF8.GetString(sub.Rest()))!.AsObject(); break;
                case "SKEL":
                    {
                        int n = (int)sub.U32();
                        for (int i = 0; i < n; i++) f.Bones.Add(new BoneDef(sub.Str(), sub.I32(), sub.V3(), sub.V3()));
                        break;
                    }
                case "SOCK":
                    {
                        int n = (int)sub.U32();
                        for (int i = 0; i < n; i++)
                        {
                            string name = sub.Str(); int bone = sub.I32(); var p = sub.V3();
                            float w = sub.F32(), x = sub.F32(), y = sub.F32(), z = sub.F32();
                            f.Sockets.Add(new SocketDef(name, bone, p, new Quaternion(x, y, z, w)));
                        }
                        break;
                    }
                case "MESH": f.Meshes.Add(ReadMesh(sub)); break;
                case "MRPH":
                    {
                        string mesh = sub.Str(); int n = (int)sub.U32(); var list = new List<MorphDef>(n);
                        for (int i = 0; i < n; i++)
                        {
                            string name = sub.Str(); int cnt = (int)sub.U32();
                            var idx = sub.U32s(cnt); var dp = sub.V3s(cnt); var dn = sub.V3s(cnt);
                            list.Add(new MorphDef(name, idx, dp, dn));
                        }
                        f.Morphs[mesh] = list;
                        break;
                    }
                case "VIEW":
                    {
                        string mesh = sub.Str(), view = sub.Str(); int n = (int)sub.U32();
                        f.Views.Add(new ViewDef(mesh, view, sub.U32s(n)));
                        break;
                    }
                case "CLIP": f.Clips.Add(ReadClip(sub)); break;
                default: f.Unknown.Add((tag, sub.Rest())); break;
            }
        }
        if (!r.AtEnd) throw new InvalidDataException("trailing bytes after the last chunk");
        return f;
    }

    static MeshDef ReadMesh(Reader r)
    {
        string name = r.Str(), mat = r.Str();
        uint flags = r.U32(); int nv = (int)r.U32(), ni = (int)r.U32();
        var pos = r.V3s(nv); var nrm = r.V3s(nv);
        var tan = new Vector4[nv]; for (int i = 0; i < nv; i++) tan[i] = new Vector4(r.F32(), r.F32(), r.F32(), r.F32());
        var uv = new Vector2[nv]; for (int i = 0; i < nv; i++) uv[i] = new Vector2(r.F32(), r.F32());
        var j = r.Bytes(nv * 4); var w = r.Bytes(nv * 4); var col = r.Bytes(nv * 4);
        var idx = r.U32s(ni);
        if (!r.AtEnd) throw new InvalidDataException($"mesh {name}: {r.Remaining} trailing bytes");
        foreach (var x in idx) if (x >= nv) throw new InvalidDataException($"mesh {name}: index {x} out of range");
        return new MeshDef { Name = name, Material = mat, Flags = flags, Positions = pos, Normals = nrm, Tangents = tan, Uvs = uv, Joints = j, Weights = w, Colors = col, Indices = idx };
    }

    static ClipDef ReadClip(Reader r)
    {
        string name = r.Str();
        uint flags = r.U32(); float fps = r.F32(); int frames = (int)r.U32(), bones = (int)r.U32();
        float speed = r.F32(); r.F32();
        var rot = new Quaternion[frames * bones];
        for (int i = 0; i < rot.Length; i++) { float w = r.F32(), x = r.F32(), y = r.F32(), z = r.F32(); rot[i] = new Quaternion(x, y, z, w); }
        var pel = r.V3s(frames);
        var con = r.Bytes(frames);
        string src = r.Str();
        return new ClipDef { Name = name, Loop = (flags & 1) != 0, Fps = fps, Frames = frames, Bones = bones, Speed = speed, Rotations = rot, Pelvis = pel, Contacts = con, Source = src };
    }

    public MeshDef? Mesh(string name) => Meshes.Find(m => m.Name == name);
    public ClipDef? Clip(string name) => Clips.Find(c => c.Name == name);
    public int BoneIndex(string name) => Bones.FindIndex(b => b.Name == name);

    sealed class Reader(byte[] b)
    {
        int _p;
        public bool AtEnd => _p == b.Length;
        public int Remaining => b.Length - _p;
        void Need(int n) { if (_p + n > b.Length) throw new InvalidDataException("truncated G2CH data"); }
        public uint U32() { Need(4); var v = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(_p)); _p += 4; return v; }
        public int I32() { Need(4); var v = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(_p)); _p += 4; return v; }
        public float F32() { Need(4); var v = BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(_p)); _p += 4; return v; }
        public Vector3 V3() => new(F32(), F32(), F32());
        public Vector3[] V3s(int n) { var a = new Vector3[n]; for (int i = 0; i < n; i++) a[i] = V3(); return a; }
        public uint[] U32s(int n) { var a = new uint[n]; for (int i = 0; i < n; i++) a[i] = U32(); return a; }
        public byte[] Bytes(int n) { Need(n); var a = b.AsSpan(_p, n).ToArray(); _p += n; return a; }
        public byte[] Rest() => Bytes(b.Length - _p);
        public string Str() { Need(2); int n = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(_p)); _p += 2; return Encoding.UTF8.GetString(Bytes(n)); }
    }
}
