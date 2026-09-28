using System.Security.Cryptography;
using Sim.Ui.Art;
using Sim.Ui.Art.Glyphs;
using Xunit;

namespace Sim.Ui.Tests.World;

/// <summary>
/// Task Part 13: the world foundation added four object marks (Lyre, Compass, Anchor, Drop)
/// APPENDED to GlyphDomain. This pins that every glyph that could be baked BEFORE them bakes
/// byte-identically after: one SHA-256 over the pixel buffers of a sweep of the pre-existing
/// spec space (every base, state, size and pre-existing domain, plus the era, veterancy,
/// strength, placement, maturity, progress and stage axes), measured on commit 78e6b58 (the
/// last commit before the additions) and asserted here. Reference platform only (ADR-022):
/// the baker uses Math.Sin/Cos, whose last ulp may differ on other platforms.
/// </summary>
public class GlyphByteIdentityTests
{
    // Measured on 78e6b58 (linux-x64 / ubuntu.24.04-x64) with exactly this sweep.
    private const int ExpectedCount = 25578;
    private const string ExpectedSha256 = "584A32A84226490FCB8DDC94DC3A741910B5C3629670D76470A73839F6D4B657";

    public static (int Count, string Hex) Sweep()
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int n = 0;
        SizeClass[] sizes = [SizeClass.Px16, SizeClass.Px24, SizeClass.Px32, SizeClass.Px48];
        void Add(GlyphSpec s) { ArtImage img = GlyphBaker.Bake(s); sha.AppendData(img.Rgba); n++; }
        for (int b = 0; b <= 17; b++)            // Hall .. Emblem: every pre-existing base
            for (int st = 0; st <= 6; st++)      // Locked .. Discovered
                foreach (SizeClass z in sizes)
                {
                    var baseSpec = new GlyphSpec((GlyphBase)b, (GlyphState)st, z, Progress: 0.5, Maturity: 0.6);
                    Add(baseSpec);
                    Add(GlyphSpec.Exemplar((GlyphBase)b, (GlyphState)st, z));
                    if (z >= SizeClass.Px24)
                        for (int d = 0; d <= 32; d++) Add(baseSpec with { Domain = (GlyphDomain)d });   // None .. Flask
                    for (int e = 0; e < 5; e++) Add(baseSpec with { Era = (EraRegister)e, Domain = GlyphDomain.Craft });
                    for (int v = 0; v < 4; v++) { Add(baseSpec with { Veterancy = (Veterancy)v }); Add(baseSpec with { Veterancy = (Veterancy)v, Strength = 0.35 }); }
                    Add(baseSpec with { Placement = Placement.Map });
                    foreach (double m in new[] { 0.0, 0.49, 0.5, 1.0 }) Add(baseSpec with { Maturity = m });
                    foreach (double p in new[] { 0.0, 1.0 }) Add(baseSpec with { Progress = p });
                    for (int k = 1; k <= 4; k++) Add(baseSpec with { Stage = k });
                }
        return (n, Convert.ToHexString(sha.GetHashAndReset()));
    }

    [Fact]
    public void EveryPreExistingGlyph_BakesByteIdentically()
    {
        if (!Sim.Core.Kernel.SessionManifest.IsReferencePlatform(System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier))
            return;   // ADR-022: the pin is defined on the reference platform only
        (int count, string hex) = Sweep();
        Assert.Equal(ExpectedCount, count);
        Assert.Equal(ExpectedSha256, hex);
    }
}
