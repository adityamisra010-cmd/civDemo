using System.Globalization;
using Gfx.Gear.Bake;

namespace Gfx.Demo;

/// <summary>Stage kit: <c>--kit-bake</c> writes the hero aspis (glTF 2.0 + manifest + provenance); <c>--check</c> compares a fresh bake with the files.</summary>
public static class KitBakeCli
{
    public static int Run(string outDir, string commit, bool check)
    {
        var r = HeroBake.Bake();
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "[kit-bake] aspis {0}: LOD triangles {1}, vertices {2}, maps {3}x{3}; geometry {4:F0} ms, paint {5:F0} ms, encode {6:F0} ms (this container's CPUs; not Victus evidence)",
            HeroBake.Version, string.Join("/", r.Triangles), string.Join("/", r.Vertices), r.MapSize, r.GeometryMs, r.PaintMs, r.EncodeMs));
        Console.WriteLine($"[kit-bake] glb {r.Glb.Length} bytes sha256 {r.Sha256}; embedded PNG {r.PngBytes} bytes; mesh LOD0 {r.MeshHash0[..16]}; maps {r.MapHash[..16]}");
        string path = Path.Combine(outDir, HeroBake.FileName);
        if (check)
        {
            if (!File.Exists(path)) { Console.WriteLine($"[kit-bake] MISSING {path}"); return 1; }
            var old = File.ReadAllBytes(path);
            bool same = old.AsSpan().SequenceEqual(r.Glb);
            Console.WriteLine($"[kit-bake] --check: {(same ? "identical" : "DIFFERS")} ({old.Length} vs {r.Glb.Length} bytes)");
            return same ? 0 : 1;
        }
        HeroBake.WriteTo(outDir, r, string.IsNullOrEmpty(commit) ? null : commit);
        Console.WriteLine($"[kit-bake] wrote {path}, MANIFEST.tsv, aspis.provenance.json");
        return 0;
    }
}
