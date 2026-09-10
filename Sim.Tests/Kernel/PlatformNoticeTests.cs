using Sim.Cli;
using Sim.Core.Kernel;

namespace Sim.Tests.Kernel;

/// <summary>
/// ADR-022 — the platform line `sim inspect` prints before its verdict, pinned
/// headless in all four (manifest, running) × (reference, not) cases plus the
/// two the four do not cover: the same non-reference platform on both sides,
/// and a pre-v2 manifest with no platform on record.
///
/// What each case must SAY is the point, not that it says something: the
/// defect this closes is a true "REPRODUCTION FAILED at turn 2" read as a
/// determinism finding when it was a cross-platform one (CR-013 §3), so each
/// assertion below is a sentence a reader has to be able to act on.
/// </summary>
public class PlatformNoticeTests
{
    private const string Ref = SessionManifest.ReferencePlatform;   // linux-x64

    [Fact]
    public void ReferenceOnBothSides_SaysSoPlainly_AndRaisesNoNotice()
    {
        string text = PlatformNotice.For(Ref, Ref);

        Assert.Contains("played on linux-x64 (reference platform)", text);
        Assert.Contains("inspecting on linux-x64 (reference platform)", text);
        Assert.Contains("a hash mismatch below is a determinism finding", text);
        Assert.DoesNotContain("NOTICE", text);
    }

    [Fact]
    public void TheMeasuredContainerRid_CountsAsReference()
    {
        // ubuntu.24.04-x64 is the RID this project's remote sessions run under
        // (CR-013 §8.2) and it reproduces the linux-x64 runner byte for byte
        // (§8.4). A notice here would tell every agent its own replays are
        // suspect, which is false.
        string text = PlatformNotice.For("ubuntu.24.04-x64", "ubuntu.24.04-x64");
        Assert.Contains("played on ubuntu.24.04-x64 (reference platform)", text);
        Assert.DoesNotContain("NOTICE", text);
    }

    [Fact]
    public void PlayedOffReference_InspectedOnReference_ExpectsDivergenceFromTurn2_AndSaysWhereToJudge()
    {
        // The director's case: a Windows session inspected on CI or in a
        // container. The hash check WILL fail from turn 2; the notice must say
        // that before the verdict prints, and say what remains comparable.
        string text = PlatformNotice.For("win-x64", Ref);

        Assert.Contains("played on win-x64 (NOT the reference platform)", text);
        Assert.Contains("inspecting on linux-x64 (reference platform)", text);
        Assert.Contains("NOTICE: this session was played on win-x64, not the reference platform (linux-x64)", text);
        Assert.Contains("EXPECTED to report REPRODUCTION FAILED from", text);
        Assert.Contains("turn 2", text);
        Assert.Contains("CR-013 §8", text);
        Assert.Contains("not a determinism defect", text);
        Assert.Contains("population, food and settlements columns remain comparable", text);
        Assert.Contains("on the machine that", text);
        Assert.Contains("played it", text);
        // Exactly one notice: the inspecting side is the reference and needs none.
        Assert.Equal(1, Count(text, "NOTICE:"));
        Assert.DoesNotContain("sim inspect itself is running off", text);
    }

    [Fact]
    public void PlayedOnReference_InspectedOffReference_WarnsAboutTheInspectingMachine()
    {
        // The mirror: a reference-platform trace (CI, a container, a Linux
        // player) inspected on a Windows machine. The trace is canonical; the
        // replay is the one that will diverge.
        string text = PlatformNotice.For(Ref, "win-x64");

        Assert.Contains("played on linux-x64 (reference platform)", text);
        Assert.Contains("inspecting on win-x64 (NOT the reference platform)", text);
        Assert.Contains("NOTICE: sim inspect itself is running off the reference platform (linux-x64)", text);
        Assert.Contains("EXPECTED to fail the hash cross-check here from turn 2", text);
        Assert.Contains("population, food and settlements columns stay comparable", text);
        Assert.Contains("judged on the", text);
        Assert.Equal(1, Count(text, "NOTICE:"));
        Assert.DoesNotContain("this session was played on", text);
    }

    [Fact]
    public void OffReferenceOnBothSides_DifferentPlatforms_RaisesBothNotices()
    {
        string text = PlatformNotice.For("win-x64", "osx-arm64");

        Assert.Contains("NOTICE: this session was played on win-x64, not the reference platform", text);
        Assert.Contains("NOTICE: sim inspect itself is running off the reference platform", text);
        Assert.Equal(2, Count(text, "NOTICE:"));
    }

    [Fact]
    public void OffReferenceOnBothSides_SamePlatform_ExpectsReproductionHERE_ButNotTheCanonicalArtifact()
    {
        // The Windows player inspecting his own session on his own machine —
        // the case ADR-022 says a Windows build is FOR. CR-013 §8.4 measured
        // the Windows runner reproducing the director's Windows trace exactly,
        // so a mismatch here IS a finding; the notice must not wave it away as
        // "expected", and must still say these are not the reference hashes.
        string text = PlatformNotice.For("win-x64", "win-x64");

        Assert.Contains("played on win-x64 and is being inspected on the same", text);
        Assert.Contains("expected to", text);
        Assert.Contains("reproduce HERE", text);
        Assert.Contains("CR-013 §8.4", text);
        Assert.Contains("a mismatch below is a determinism finding for this platform", text);
        Assert.Contains("not the canonical artifact", text);
        Assert.Contains("diverge from", text);
        Assert.Equal(1, Count(text, "NOTICE:"));
        Assert.DoesNotContain("EXPECTED to report REPRODUCTION FAILED", text);
    }

    [Fact]
    public void APreV2Manifest_SaysThePlatformIsUnknown_AndGivesTheSignatureToLookFor()
    {
        // Every session played before ADR-022. The notice cannot classify, so
        // it must say so and hand the reader the measured signature instead of
        // a guess.
        string text = PlatformNotice.For(SessionManifest.PlatformNotRecorded, Ref);

        Assert.Contains("played on not recorded (pre-v2 session); inspecting on linux-x64 (reference platform)", text);
        Assert.Contains("NOTICE: this session's manifest predates v2", text);
        Assert.Contains("cannot be classified from the manifest", text);
        Assert.Contains("REPRODUCTION FAILED at turn 2", text);
        Assert.Contains("population/food/settlements columns agreeing to the unit", text);
        Assert.Equal(1, Count(text, "NOTICE:"));

        // ...and inspected off the reference it ALSO warns about the machine.
        string off = PlatformNotice.For(SessionManifest.PlatformNotRecorded, "win-x64");
        Assert.Equal(2, Count(off, "NOTICE:"));
        Assert.Contains("sim inspect itself is running off the reference platform", off);
    }

    [Fact]
    public void EveryCaseNamesTheReferencePlatformAndTheADR_AndEndsWithoutATrailingNewline()
    {
        // The line is printed with Console.WriteLine; a trailing newline would
        // double-space it under the world line.
        foreach ((string m, string r) in new[]
        {
            (Ref, Ref), ("win-x64", Ref), (Ref, "win-x64"), ("win-x64", "win-x64"),
            ("win-x64", "osx-arm64"), (SessionManifest.PlatformNotRecorded, Ref),
        })
        {
            string text = PlatformNotice.For(m, r);
            Assert.Contains("reference is linux-x64 (ADR-022)", text);
            Assert.False(text.EndsWith('\n'), $"trailing newline for ({m}, {r})");
            Assert.StartsWith("  platform  ", text);
        }
    }

    private static int Count(string text, string needle)
    {
        int n = 0;
        for (int i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
