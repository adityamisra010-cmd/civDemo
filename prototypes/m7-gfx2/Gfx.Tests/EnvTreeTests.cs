using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Gfx.Proc.Environment.EzTree;
using Gfx.Units;

namespace Gfx.Tests;

/// <summary>
/// Stage env: the C# port of ez-tree (MIT). The random-number generator must match an independent Python oracle bit
/// for bit; the generator must reproduce the original JavaScript's output (run under Node with three r180 while
/// porting; compact reference in fixtures/ez-tree-js-reference.json).
/// </summary>
[Collection(EnvSerialCollection.Name)]
public class EnvTreeTests
{
    static string Fixture(string name) => BattleTrace.FindFixture(name);

    [Fact]
    public void EzRngMatchesThePythonOracleBitForBit()
    {
        int lines = 0;
        EzRng? a = null, b = null; long seed = long.MinValue;
        foreach (var line in File.ReadLines(Fixture("ez-rng-oracle.txt")))
        {
            if (line.StartsWith('#')) continue;
            var f = line.Split(' ');
            long s = long.Parse(f[0], CultureInfo.InvariantCulture);
            int i = int.Parse(f[1], CultureInfo.InvariantCulture);
            if (s != seed || i == 0) { seed = s; a = new EzRng(s); b = new EzRng(s); Assert.Equal(0, i); }
            uint raw = a!.NextUInt32();
            Assert.Equal(uint.Parse(f[2], CultureInfo.InvariantCulture), raw);
            Assert.Equal(f[3], Bits(raw / 4294967296.0));
            Assert.Equal(f[4], Bits(b!.Random(0.3, -0.3)));
            lines++;
        }
        Assert.Equal(15 * 128, lines);
    }

    static string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("x16", CultureInfo.InvariantCulture);

    [Fact]
    public void EzRngRandomUsesTheJavaScriptRangeFormula()
    {
        var r1 = new EzRng(23399); var r2 = new EzRng(23399);
        for (int i = 0; i < 64; i++)
        {
            double u = r1.NextUInt32() / 4294967296.0;
            Assert.Equal(BitConverter.DoubleToInt64Bits((2.5 - -1.5) * u + -1.5), BitConverter.DoubleToInt64Bits(r2.Random(2.5, -1.5)));
        }
    }

    [Fact]
    public void JsRoundRoundsHalvesUpLikeJavaScript()
    {
        Assert.Equal(3, EzTreeGenerator.JsRound(2.5));
        Assert.Equal(-2, EzTreeGenerator.JsRound(-2.5));
        Assert.Equal(4, EzTreeGenerator.JsRound(3.5));
        Assert.Equal(2, EzTreeGenerator.JsRound(2.4999));
        Assert.Equal(0, EzTreeGenerator.JsRound(0.49999999999999994)); // JavaScript gives 0; floor(x + 0.5) would give 1
    }

    [Fact]
    public void EzTreePortMatchesTheJavaScriptOriginal()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Fixture("ez-tree-js-reference.json")));
        int step = doc.RootElement.GetProperty("step").GetInt32();
        int checkedPresets = 0; double worst = 0;
        foreach (var p in doc.RootElement.GetProperty("presets").EnumerateObject())
        {
            var o = EzTreePresets.Load(p.Name);
            var (b, l) = EzTreeGenerator.Mesh(EzTreeGenerator.GenerateSkeleton(o), o);
            var r = p.Value;
            Assert.Equal(r.GetProperty("branchVertexCount").GetInt32(), b.VertexCount);
            Assert.Equal(r.GetProperty("leafVertexCount").GetInt32(), l.VertexCount);
            Assert.Equal(r.GetProperty("branchIndexCount").GetInt32(), b.Indices.Count);
            Assert.Equal(r.GetProperty("leafIndexCount").GetInt32(), l.Indices.Count);
            Assert.Equal(r.GetProperty("branchIndexSha256").GetString(), IndexHash(b.Indices));
            Assert.Equal(r.GetProperty("leafIndexSha256").GetString(), IndexHash(l.Indices));
            worst = Math.Max(worst, Compare(b.Verts, r.GetProperty("branchVerts"), step));
            worst = Math.Max(worst, Compare(b.Normals, r.GetProperty("branchNormals"), step));
            worst = Math.Max(worst, Compare(b.Uvs, r.GetProperty("branchUvs"), step));
            worst = Math.Max(worst, Compare(l.Verts, r.GetProperty("leafVerts"), step));
            worst = Math.Max(worst, Compare(l.Normals, r.GetProperty("leafNormals"), step));
            worst = Math.Max(worst, Compare(l.Uvs, r.GetProperty("leafUvs"), step));
            checkedPresets++;
        }
        Assert.Equal(4, checkedPresets);
        // measured worst while porting: 3.7e-11 relative over all fifteen presets (sin/cos/atan2 of V8 vs the C runtime)
        Assert.True(worst < 1e-9, $"worst relative difference {worst:E3}");
    }

    static double Compare(List<double> mine, JsonElement sampled, int step)
    {
        double worst = 0; int k = 0;
        foreach (var e in sampled.EnumerateArray())
        {
            double x = e.GetDouble(), a = mine[k * step];
            worst = Math.Max(worst, Math.Abs(a - x) / Math.Max(1, Math.Abs(x)));
            k++;
        }
        Assert.Equal((mine.Count + step - 1) / step, k);
        return worst;
    }

    static string IndexHash(List<int> idx)
    {
        var bytes = new byte[idx.Count * 4];
        for (int i = 0; i < idx.Count; i++) BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), idx[i]);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    [Fact]
    public void EmbeddedPresetsAreTheUnmodifiedPinnedFiles()
    {
        var manifest = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(Fixture("ez-rng-oracle.txt"))!, "..", "assets", "manifest.tsv"));
        foreach (var name in EzTreePresets.Names)
        {
            string dest = $"ez-tree/src/lib/presets/{name}.json";
            var row = manifest.Single(l => l.StartsWith(dest + "\t", StringComparison.Ordinal)).Split('\t');
            Assert.Equal(row[4], Convert.ToHexStringLower(SHA256.HashData(EzTreePresets.Bytes(name))));
            Assert.Equal("MIT", row[6]);
        }
    }

    [Fact]
    public void TreeGenerationIsDeterministicAndSeedSensitive()
    {
        var o = EzTreePresets.Load("ash_medium");
        var (b1, l1) = EzTreeGenerator.Mesh(EzTreeGenerator.GenerateSkeleton(o), o);
        var (b2, l2) = EzTreeGenerator.Mesh(EzTreeGenerator.GenerateSkeleton(o.Clone()), o);
        Assert.Equal(b1.Verts, b2.Verts); Assert.Equal(l1.Verts, l2.Verts);
        var o2 = o.Clone(); o2.Seed += 1;
        var (b3, _) = EzTreeGenerator.Mesh(EzTreeGenerator.GenerateSkeleton(o2), o2);
        Assert.NotEqual(b1.Verts, b3.Verts);
        // the clone owns its tables
        o2.Length[0] = 99; Assert.NotEqual(99, o.Length[0]);
    }
}
