using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Gfx.Gear.Geometry;
using Gfx.Gear.Items;
using Gfx.Gear.Maps;

namespace Gfx.Gear.Bake;

/// <summary>
/// The hero asset's bake (DR49 item 2, "from source through rendering"): the aspis generator (source: this repository's
/// code, no external input) is run with fixed parameters into three LODs and one 2048-texel PBR map set, written as a
/// standard glTF 2.0 binary with the maps embedded as PNG (any glTF viewer or engine can open it), plus a manifest of
/// sha256 values and a provenance record. The renderer then loads the .glb, not the generator's in-memory output, so
/// what is drawn is what was delivered. Deterministic: the same code and parameters give the same bytes.
/// </summary>
public static class HeroBake
{
    public const string Version = "aspis-1";
    public const string FileName = "aspis.glb";
    public static readonly AspisSpec Spec = new() { Seed = 1, MapSize = 2048, Device = "sun", Paint = "#5C1A12", Wear = 0.55, BronzeFace = true };

    public sealed record Result(byte[] Glb, string Sha256, int[] Triangles, int[] Vertices, int MapSize, double GeometryMs, double PaintMs, double EncodeMs,
        long PngBytes, string MeshHash0, string MapHash);

    public static Result Bake(AspisSpec? spec = null)
    {
        spec ??= Spec;
        var sw = Stopwatch.StartNew();
        var lods = new List<KitMesh>();
        for (int l = 0; l < 3; l++)
        {
            var m = Aspis.BuildMesh(spec with { Lod = l });
            m.Name = $"aspis_LOD{l}";
            lods.Add(m);
        }
        double geo = sw.Elapsed.TotalMilliseconds; sw.Restart();
        var maps = Aspis.Paint(spec, lods[0]);
        double paint = sw.Elapsed.TotalMilliseconds; sw.Restart();
        var bas = Png.Encode(maps.Width, maps.Height, maps.BaseRgba, 3);
        var nrm = Png.Encode(maps.Width, maps.Height, maps.NormalRgba, 3);
        var orm = Png.Encode(maps.Width, maps.Height, maps.OrmRgba, 3);
        var extras = new JsonObject
        {
            ["item"] = "aspis (bronze-faced hoplite shield)",
            ["generator"] = "prototypes/m7-gfx2/Gfx.Gear/Items/Aspis.cs",
            ["bake"] = "prototypes/m7-gfx2/Gfx.Gear/Bake/HeroBake.cs",
            ["version"] = Version,
            ["licence"] = "project-owned: generated in code, no external input",
            ["spec"] = new JsonObject
            {
                ["seed"] = (long)spec.Seed, ["map_size"] = spec.MapSize, ["device"] = spec.Device, ["paint"] = spec.Paint, ["wear"] = spec.Wear,
                ["bronze_face"] = spec.BronzeFace,
            },
            ["lod_triangles"] = new JsonArray(lods.Select(m => (JsonNode)m.TriangleCount).ToArray()),
            ["units"] = "metres, +Y up, the shield's face along +Z",
        };
        var glb = Glb.Write(lods, new GlbMaterial("aspis_bronze_wood", bas, nrm, orm), extras);
        double enc = sw.Elapsed.TotalMilliseconds;
        return new Result(glb, Convert.ToHexString(SHA256.HashData(glb)).ToLowerInvariant(), lods.Select(m => m.TriangleCount).ToArray(),
            lods.Select(m => m.VertexCount).ToArray(), maps.Width, geo, paint, enc, bas.LongLength + nrm.LongLength + orm.LongLength, lods[0].Hash(), maps.Hash());
    }

    /// <summary>Writes the .glb, MANIFEST.tsv (sha256 and bytes of every output and of each embedded image) and a provenance record.</summary>
    public static void WriteTo(string dir, Result r, string? commit)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, FileName), r.Glb);
        var (_, images, _) = Glb.Read(r.Glb);
        string[] names = { "basecolor.png", "normal.png", "occlusion_roughness_metallic.png" };
        var prov = new JsonObject
        {
            ["asset"] = FileName, ["sha256"] = r.Sha256, ["bytes"] = r.Glb.Length, ["version"] = Version, ["commit"] = commit ?? "(uncommitted working tree)",
            ["source"] = "generated in code: prototypes/m7-gfx2/Gfx.Gear/Items/Aspis.cs (geometry and maps), Gfx.Gear/Geometry, Gfx.Gear/Maps",
            ["external_inputs"] = "none",
            ["licence"] = "project-owned (original work of this project); SHIP-OK",
            ["historical_form"] = "Greek hoplite aspis, about 500-350 BC: deep wooden bowl, offset flat rim (itys), bronze facing, porpax arm band, antilabe hand grip, carrying cord. Form only; no asset or likeness of any game.",
            ["lod_triangles"] = new JsonArray(r.Triangles.Select(t => (JsonNode)t).ToArray()),
            ["map_size"] = r.MapSize,
            ["timings_ms_this_container"] = new JsonObject { ["geometry"] = Math.Round(r.GeometryMs), ["paint"] = Math.Round(r.PaintMs), ["encode"] = Math.Round(r.EncodeMs) },
        };
        File.WriteAllText(Path.Combine(dir, "aspis.provenance.json"), prov.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
        var lines = new List<string> { "# path\tsha256\tbytes", $"{FileName}\t{r.Sha256}\t{r.Glb.Length}" };
        for (int i = 0; i < images.Count; i++)
            lines.Add($"{FileName}#{names[i]}\t{Convert.ToHexString(SHA256.HashData(images[i])).ToLowerInvariant()}\t{images[i].Length}");
        File.WriteAllText(Path.Combine(dir, "MANIFEST.tsv"), string.Join("\n", lines) + "\n");
    }

    /// <summary>Loads the baked aspis as an item: LOD0 mesh, the embedded maps, the generator's frames.</summary>
    public static KitItem Load(string glbPath, int lod = 0)
    {
        var (meshes, images, _) = Glb.Read(File.ReadAllBytes(glbPath));
        var (w, h, bas) = Png.Decode(images[0]);
        var (_, _, nrm) = Png.Decode(images[1]);
        var (_, _, orm) = Png.Decode(images[2]);
        var item = new KitItem { Id = $"aspis-baked-L{lod}", Kind = "shield.aspis", Era = "ancient", Label = "Aspis (baked hero asset)" };
        item.Parts.Add(new KitPart { Name = "aspis", Mesh = meshes[lod], Maps = "aspis" });
        item.Maps["aspis"] = MapSet.FromBytes("aspis", w, h, bas, nrm, orm);
        Aspis.Frames(item);
        item.GripRadius = 0.006f;
        return item;
    }
}
