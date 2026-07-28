using Sim.Ui.Art;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// THE DROP AUDIT'S TEETH. Two failure classes have now cost this project a
/// gate round each, and both share a shape: the drop LOOKS delivered and
/// BEHAVES missing.
///   1. terrain/deep.png arrived as deepsea.png — a name the manifest did not
///      know, so the wash silently rendered as a stand-in (fixed by aliases).
///   2. ui/header-rule.png arrived as header-rule.png.jpg — a doubled
///      extension AND a JPEG, which cannot carry an alpha channel at all, so
///      the rule would have drawn as an opaque box over the panel.
/// These tests plant both failures deliberately and require the audit to name
/// them. A silently-ignored orphan is the expensive failure mode.
/// </summary>
public class AssetAuditTests
{
    [Fact]
    public void OrphanScan_FlagsAnyUnreferencedFile_IncludingMisExtensionedDrops()
    {
        // The orphan scan must cover EVERY extension, not just *.png: the
        // header-rule.png.jpg drop was invisible to a *.png glob. Only fonts/
        // (loaded by UiTheme, not the manifest) and drop-point *.md docs are
        // exempt.
        string root = Root();
        try
        {
            PlaceholderArt.GenerateMissing(root);
            File.WriteAllBytes(Path.Combine(root, "ui", "header-rule.png.jpg"), [0xFF, 0xD8, 0xFF]);
            File.WriteAllText(Path.Combine(root, "notes.txt"), "stray");
            Directory.CreateDirectory(Path.Combine(root, "fonts"));
            File.WriteAllText(Path.Combine(root, "fonts", "OFL.txt"), "license");
            File.WriteAllText(Path.Combine(root, "README.md"), "drop point");

            AssetAudit.Report report = AssetAudit.Run(root);

            Assert.Contains("ui/header-rule.png.jpg", report.OrphanFiles);
            Assert.Contains("notes.txt", report.OrphanFiles);
            Assert.DoesNotContain(report.OrphanFiles, o => o.StartsWith("fonts/"));
            Assert.DoesNotContain(report.OrphanFiles, o => o.EndsWith(".md"));
            Assert.Equal(2, report.OrphanFiles.Count);
            // And it must be LOUD in the rendered report, with the diagnosis.
            string text = report.Render();
            Assert.Contains("ORPHANED FILES", text);
            Assert.Contains("header-rule.png.jpg", text);
            Assert.Contains("doubled extension", text);
        }
        finally { Clean(root); }
    }

    [Fact]
    public void TransparencyCheck_FlagsArtThatCannotBeTransparent()
    {
        // A key the renderer composites over the map (settlement marker) is
        // replaced by REAL art with NO alpha channel — an 8-bit RGB PNG, which
        // is what a JPEG re-exported under a .png name decodes to. The audit
        // must say so by name; the eye would only notice the opaque box later.
        string root = Root();
        try
        {
            PlaceholderArt.GenerateMissing(root);
            AssetManifest.Entry marker = AssetManifest.Require("ui/settlement-marker");
            WriteRgbNoAlpha(Path.Combine(root, marker.RelativePath), 32, 32);

            AssetAudit.Report report = AssetAudit.Run(root);
            AssetAudit.KeyStatus status = report.Keys.Single(k => k.Key == marker.Key);

            Assert.False(status.IsPlaceholder);              // it IS real art...
            Assert.NotNull(status.AlphaFault);               // ...and it is unusable
            Assert.Contains("NO alpha channel", status.AlphaFault);
            string text = report.Render();
            Assert.Contains("TRANSPARENCY FAULTS", text);
            Assert.Contains(marker.Key, text);
        }
        finally { Clean(root); }
    }

    [Fact]
    public void TransparencyCheck_FlagsAnOpaqueBoxThatMerelyHasAnAlphaChannel()
    {
        // The subtler version: a real RGBA PNG whose alpha is all 255. It
        // satisfies "is a PNG with an alpha channel" and still draws as a box.
        string root = Root();
        try
        {
            PlaceholderArt.GenerateMissing(root);
            AssetManifest.Entry rose = AssetManifest.Require("ui/compass-rose");
            WriteOpaqueRgba(Path.Combine(root, rose.RelativePath), 32, 32);

            AssetAudit.KeyStatus status = AssetAudit.Run(root).Keys.Single(k => k.Key == rose.Key);
            Assert.NotNull(status.AlphaFault);
            Assert.Contains("corners are OPAQUE", status.AlphaFault);
        }
        finally { Clean(root); }
    }

    [Fact]
    public void TransparencyCheck_DoesNotFlagSheetsMeantToBeOpaque()
    {
        // The inverse, so the check cannot become a nuisance: full-bleed sheets
        // (parchment, grain, terrain washes, the Annals background) are opaque
        // BY DESIGN and must never be reported as faults.
        string root = Root();
        try
        {
            PlaceholderArt.GenerateMissing(root);
            // Includes ui/panel and ui/button-plate: parchment PLATES drawn
            // behind a window and a button, opaque on purpose.
            foreach (string key in new[]
                     { "parchment/base-0", "parchment/grain", "ui/annals-bg", "ui/panel", "ui/button-plate" })
            {
                AssetManifest.Entry e = AssetManifest.Require(key);
                Assert.False(e.RequiresAlpha, $"{key} must not require transparency");
                WriteRgbNoAlpha(Path.Combine(root, e.RelativePath), 16, 16);
            }
            Assert.Empty(AssetAudit.Run(root).AlphaFaults);
        }
        finally { Clean(root); }
    }

    [Fact]
    public void TheRealDrop_HasNoOrphansAndNoTransparencyFaults()
    {
        // The live gate on whatever is in assets/ right now.
        AssetAudit.Report report = AssetAudit.Run();
        Assert.True(report.OrphanFiles.Count == 0,
            "orphaned files in assets/: " + string.Join(", ", report.OrphanFiles));
        Assert.True(!report.AlphaFaults.Any(),
            "transparency faults: " +
            string.Join("; ", report.AlphaFaults.Select(k => $"{k.Key}: {k.AlphaFault}")));
    }

    private static string Root() =>
        Path.Combine(Path.GetTempPath(), $"art-audit-{Guid.NewGuid():N}");

    private static void Clean(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    /// <summary>An 8-bit RGB PNG (colour type 2) — what a JPEG re-export
    /// becomes. PngCodec.Write emits RGBA, so this is written by hand.</summary>
    private static void WriteRgbNoAlpha(string path, int w, int h)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var raw = new byte[h * (1 + w * 3)];
        for (int y = 0; y < h; y++)
        {
            int row = y * (1 + w * 3);
            raw[row] = 0; // filter: none
            for (int x = 0; x < w; x++)
            {
                raw[row + 1 + x * 3] = 200;
                raw[row + 2 + x * 3] = 180;
                raw[row + 3 + x * 3] = 140;
            }
        }
        File.WriteAllBytes(path, BuildPng(w, h, colorType: 2, raw));
    }

    /// <summary>An RGBA PNG whose alpha is uniformly 255 — the opaque box.</summary>
    private static void WriteOpaqueRgba(string path, int w, int h)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var img = new ArtImage(w, h, new byte[w * h * 4]);
        for (int i = 0; i < w * h; i++)
        {
            img.Rgba[i * 4] = 200; img.Rgba[i * 4 + 1] = 180;
            img.Rgba[i * 4 + 2] = 140; img.Rgba[i * 4 + 3] = 255;
        }
        PngCodec.Write(path, img);
    }

    private static byte[] BuildPng(int w, int h, byte colorType, byte[] raw)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, w); WriteBe(ihdr, 4, h);
        ihdr[8] = 8; ihdr[9] = colorType;
        Chunk(ms, "IHDR", ihdr);
        using (var deflated = new MemoryStream())
        {
            using (var z = new System.IO.Compression.ZLibStream(
                deflated, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                z.Write(raw);
            Chunk(ms, "IDAT", deflated.ToArray());
        }
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void WriteBe(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBe(len, 0, data.Length); s.Write(len);
        byte[] t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(t); s.Write(data);
        var crcInput = new byte[t.Length + data.Length];
        t.CopyTo(crcInput, 0); data.CopyTo(crcInput, t.Length);
        var crc = new byte[4]; WriteBe(crc, 0, unchecked((int)Crc32(crcInput)));
        s.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for (int b = 0; b < 8; b++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
