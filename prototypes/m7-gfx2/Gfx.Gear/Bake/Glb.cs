using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gfx.Gear.Geometry;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Bake;

/// <summary>A glTF 2.0 metallic-roughness material with three embedded PNG images.</summary>
public sealed record GlbMaterial(string Name, byte[] BaseColorPng, byte[] NormalPng, byte[] OrmPng);

/// <summary>
/// Minimal glTF 2.0 binary (.glb) writer and reader for the hero bake (Khronos glTF 2.0 specification): one buffer,
/// meshes with POSITION, NORMAL, TANGENT, TEXCOORD_0 and 32-bit indices, one node per mesh, one metallic-roughness
/// material whose base colour, normal and occlusion-roughness-metallic textures are PNG images embedded in the buffer
/// (glTF's channel packing: occlusion R, roughness G, metallic B). Canonical output: properties in a fixed order,
/// no timestamps, so the same input always gives the same bytes. Units metres, +Y up, the item's face along +Z.
/// </summary>
public static class Glb
{
    const uint Magic = 0x46546C67, JsonChunk = 0x4E4F534A, BinChunk = 0x004E4942;
    const int Float = 5126, UInt = 5125, ArrayBuffer = 34962, ElementArray = 34963;

    public static byte[] Write(IReadOnlyList<KitMesh> meshes, GlbMaterial mat, JsonObject? extras = null, string generator = "Gfx.Gear HeroBake")
    {
        var bin = new MemoryStream();
        var views = new JsonArray(); var accessors = new JsonArray();
        int View(byte[] data, int? target)
        {
            while (bin.Length % 4 != 0) bin.WriteByte(0);
            var v = new JsonObject { ["buffer"] = 0, ["byteOffset"] = (int)bin.Length, ["byteLength"] = data.Length };
            if (target is { } t) v["target"] = t;
            bin.Write(data);
            views.Add(v);
            return views.Count - 1;
        }
        int Accessor(int view, int componentType, int count, string type, JsonArray? min = null, JsonArray? max = null)
        {
            var a = new JsonObject { ["bufferView"] = view, ["componentType"] = componentType, ["count"] = count, ["type"] = type };
            if (min != null) a["min"] = min;
            if (max != null) a["max"] = max;
            accessors.Add(a);
            return accessors.Count - 1;
        }
        static byte[] Floats(IEnumerable<float> f) { var l = f.ToArray(); var b = new byte[l.Length * 4]; for (int i = 0; i < l.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(4 * i), l[i]); return b; }
        var meshArr = new JsonArray(); var nodes = new JsonArray(); var sceneNodes = new JsonArray();
        foreach (var m in meshes)
        {
            var bb = m.Bounds();
            int pos = Accessor(View(Floats(m.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z })), ArrayBuffer), Float, m.VertexCount, "VEC3",
                new JsonArray(bb.Min.X, bb.Min.Y, bb.Min.Z), new JsonArray(bb.Max.X, bb.Max.Y, bb.Max.Z));
            int nrm = Accessor(View(Floats(m.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z })), ArrayBuffer), Float, m.VertexCount, "VEC3");
            int tan = Accessor(View(Floats(m.Tangents.SelectMany(p => new[] { p.X, p.Y, p.Z, p.W })), ArrayBuffer), Float, m.VertexCount, "VEC4");
            int uv = Accessor(View(Floats(m.Uvs.SelectMany(p => new[] { p.X, p.Y })), ArrayBuffer), Float, m.VertexCount, "VEC2");
            var ib = new byte[m.Indices.Count * 4];
            for (int i = 0; i < m.Indices.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(ib.AsSpan(4 * i), (uint)m.Indices[i]);
            int idx = Accessor(View(ib, ElementArray), UInt, m.Indices.Count, "SCALAR");
            var prim = new JsonObject
            {
                ["attributes"] = new JsonObject { ["POSITION"] = pos, ["NORMAL"] = nrm, ["TANGENT"] = tan, ["TEXCOORD_0"] = uv },
                ["indices"] = idx, ["material"] = 0, ["mode"] = 4,
            };
            meshArr.Add(new JsonObject { ["name"] = m.Name, ["primitives"] = new JsonArray(prim) });
            nodes.Add(new JsonObject { ["name"] = m.Name, ["mesh"] = meshArr.Count - 1 });
            sceneNodes.Add(nodes.Count - 1);
        }
        var images = new JsonArray();
        foreach (var (png, name) in new[] { (mat.BaseColorPng, "basecolor"), (mat.NormalPng, "normal"), (mat.OrmPng, "occlusion_roughness_metallic") })
            images.Add(new JsonObject { ["name"] = name, ["bufferView"] = View(png, null), ["mimeType"] = "image/png" });
        while (bin.Length % 4 != 0) bin.WriteByte(0);
        var root = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = generator },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = sceneNodes }),
            ["nodes"] = nodes,
            ["meshes"] = meshArr,
            ["materials"] = new JsonArray(new JsonObject
            {
                ["name"] = mat.Name,
                ["pbrMetallicRoughness"] = new JsonObject
                {
                    ["baseColorTexture"] = new JsonObject { ["index"] = 0 },
                    ["metallicRoughnessTexture"] = new JsonObject { ["index"] = 2 },
                    ["metallicFactor"] = 1.0, ["roughnessFactor"] = 1.0,
                },
                ["normalTexture"] = new JsonObject { ["index"] = 1 },
                ["occlusionTexture"] = new JsonObject { ["index"] = 2 },
            }),
            ["textures"] = new JsonArray(new JsonObject { ["sampler"] = 0, ["source"] = 0 }, new JsonObject { ["sampler"] = 0, ["source"] = 1 }, new JsonObject { ["sampler"] = 0, ["source"] = 2 }),
            ["samplers"] = new JsonArray(new JsonObject { ["magFilter"] = 9729, ["minFilter"] = 9987, ["wrapS"] = 33071, ["wrapT"] = 33071 }),
            ["images"] = images,
            ["accessors"] = accessors,
            ["bufferViews"] = views,
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = (int)bin.Length }),
        };
        if (extras != null) root["extras"] = extras;
        var json = Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        int jsonLen = (json.Length + 3) & ~3;
        var binBytes = bin.ToArray();
        int total = 12 + 8 + jsonLen + 8 + binBytes.Length;
        var o = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(o, Magic); BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(4), 2); BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(8), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(12), (uint)jsonLen); BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(16), JsonChunk);
        json.CopyTo(o, 20);
        for (int i = 20 + json.Length; i < 20 + jsonLen; i++) o[i] = 0x20;
        int b0 = 20 + jsonLen;
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(b0), (uint)binBytes.Length); BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(b0 + 4), BinChunk);
        binBytes.CopyTo(o, b0 + 8);
        return o;
    }

    /// <summary>Reads a .glb written by <see cref="Write"/> (or any glTF 2.0 binary using the same subset).</summary>
    public static (List<KitMesh> meshes, List<byte[]> images, JsonObject json) Read(byte[] glb)
    {
        if (glb.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(glb) != Magic) throw new InvalidDataException("not a glTF binary");
        if (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(4)) != 2) throw new InvalidDataException("glTF version is not 2");
        if (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(8)) != glb.Length) throw new InvalidDataException("glb length mismatch");
        int jl = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        if (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(16)) != JsonChunk) throw new InvalidDataException("first chunk is not JSON");
        var root = JsonNode.Parse(Encoding.UTF8.GetString(glb, 20, jl).TrimEnd(' ', '\0'))!.AsObject();
        int b0 = 20 + jl;
        int bl = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(b0));
        if (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(b0 + 4)) != BinChunk) throw new InvalidDataException("second chunk is not BIN");
        var bin = glb.AsSpan(b0 + 8, bl).ToArray();
        var views = root["bufferViews"]!.AsArray(); var acc = root["accessors"]!.AsArray();
        byte[] ViewBytes(int v)
        {
            var o = views[v]!.AsObject();
            int off = o["byteOffset"]?.GetValue<int>() ?? 0, len = o["byteLength"]!.GetValue<int>();
            return bin.AsSpan(off, len).ToArray();
        }
        float[] Floats(int a)
        {
            var o = acc[a]!.AsObject();
            if (o["componentType"]!.GetValue<int>() != Float) throw new InvalidDataException("expected float accessor");
            int n = o["count"]!.GetValue<int>() * (o["type"]!.GetValue<string>() switch { "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => 1 });
            var b = ViewBytes(o["bufferView"]!.GetValue<int>());
            var f = new float[n];
            for (int i = 0; i < n; i++) f[i] = BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(4 * i));
            return f;
        }
        var meshes = new List<KitMesh>();
        foreach (var mn in root["meshes"]!.AsArray())
        {
            var prim = mn!["primitives"]![0]!.AsObject();
            var at = prim["attributes"]!.AsObject();
            var p = Floats(at["POSITION"]!.GetValue<int>()); var n = Floats(at["NORMAL"]!.GetValue<int>());
            var t = Floats(at["TANGENT"]!.GetValue<int>()); var uv = Floats(at["TEXCOORD_0"]!.GetValue<int>());
            var m = new KitMesh(mn["name"]?.GetValue<string>() ?? "mesh");
            int nv = p.Length / 3;
            for (int i = 0; i < nv; i++)
            {
                int k = m.Add(new Vector3(p[3 * i], p[3 * i + 1], p[3 * i + 2]), new Vector3(n[3 * i], n[3 * i + 1], n[3 * i + 2]), new Vector2(uv[2 * i], uv[2 * i + 1]), Color.White);
                m.Tangents[k] = new Vector4(t[4 * i], t[4 * i + 1], t[4 * i + 2], t[4 * i + 3]);
            }
            var ia = acc[prim["indices"]!.GetValue<int>()]!.AsObject();
            if (ia["componentType"]!.GetValue<int>() != UInt) throw new InvalidDataException("expected 32-bit indices");
            var ib = ViewBytes(ia["bufferView"]!.GetValue<int>());
            for (int i = 0; i < ia["count"]!.GetValue<int>(); i++) m.Indices.Add((int)BinaryPrimitives.ReadUInt32LittleEndian(ib.AsSpan(4 * i)));
            meshes.Add(m);
        }
        var images = new List<byte[]>();
        foreach (var im in root["images"]!.AsArray()) images.Add(ViewBytes(im!["bufferView"]!.GetValue<int>()));
        return (meshes, images, root);
    }
}
