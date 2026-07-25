using Sim.Ui.Art;
using Xunit;

namespace Sim.Ui.Tests;

public class AssetAuditTests
{
    [Fact]
    public void OrphanScan_FlagsAnyUnreferencedFile_IncludingMisExtensionedDrops()
    {
        // THE LESSON THIS PINS (real-art drop 2): the director's header rule
        // arrived as header-rule.png.jpg — a JPEG export landing BESIDE the
        // placeholder it meant to replace. The audit's original *.png orphan
        // glob was blind to it, so the drop looked complete while the rule
        // silently kept rendering as a stand-in. The orphan scan must flag
        // EVERY unreferenced file whatever its extension; only fonts/ (loaded
        // by UiTheme, not the manifest) and drop-point *.md docs are exempt.
        string root = Path.Combine(Path.GetTempPath(), $"art-audit-{Guid.NewGuid():N}");
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
            // And a complete placeholder set has no OTHER orphans.
            Assert.Equal(2, report.OrphanFiles.Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
