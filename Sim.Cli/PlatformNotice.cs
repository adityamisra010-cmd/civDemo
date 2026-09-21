using Sim.Core.Kernel;

namespace Sim.Cli;

/// <summary>
/// `sim inspect`'s platform line (ADR-022, the CR-013 ruling): what a reader
/// should expect from the hash cross-check, given where the trace was
/// recorded and where the replay is running.
///
/// A PURE FUNCTION of the two platform strings, so the four cases are pinned
/// by headless tests and the wording cannot drift between the notice a
/// Windows player reads and the one the tests assert. Nothing here reads the
/// runtime; the caller passes `RuntimeInformation.RuntimeIdentifier` in.
///
/// WHY IT EXISTS. The determinism promise is defined on the reference
/// platform (Linux x64). Off it, the same seed and order log produce a world
/// that agrees to the unit in every integer column measured (population, food,
/// settlements — CR-013 §2, turns 0–5) but hashes differently from turn 2
/// (CR-013 §8.5: a −2 ulp `PriceTerms[43].Consumption` and two
/// `HarvestWeather` rows, both downstream of libm `Exp`/`Sqrt`). Without this
/// line, `sim inspect` reported the director's first real Windows session as
/// `REPRODUCTION FAILED at turn 2` — a true statement about the hashes that
/// read as a determinism defect it was not. The tool must say which of the two
/// it is looking at BEFORE it prints the verdict.
/// </summary>
public static class PlatformNotice
{
    private const string Ref = SessionManifest.ReferencePlatform;

    /// <summary>
    /// The lines `sim inspect` prints under the world line: a plain platform
    /// line, then zero, one or two NOTICE paragraphs. Joined with '\n', no
    /// trailing newline.
    /// </summary>
    public static string For(string manifestPlatform, string runningPlatform)
    {
        bool recorded = SessionManifest.IsPlatformRecorded(manifestPlatform);
        bool playedOnRef = recorded && SessionManifest.IsReferencePlatform(manifestPlatform);
        bool runningOnRef = SessionManifest.IsReferencePlatform(runningPlatform);

        var lines = new List<string>(8)
        {
            $"  platform  played on {manifestPlatform}"
                + (recorded ? (playedOnRef ? " (reference platform)" : " (NOT the reference platform)") : "")
                + $"; inspecting on {runningPlatform}"
                + (runningOnRef ? " (reference platform)" : " (NOT the reference platform)")
                + $"; reference is {Ref} (ADR-022)",
        };

        if (!recorded)
        {
            lines.Add("");
            lines.Add("  NOTICE: this session's manifest predates v2 and does not say which platform played it,");
            lines.Add("          so the hash cross-check below cannot be classified from the manifest. The");
            lines.Add("          CR-013 cross-platform signature is REPRODUCTION FAILED at turn 2 with the");
            lines.Add("          population/food/settlements columns agreeing to the unit; a mismatch on the");
            lines.Add("          machine that played the session is a determinism finding.");
            if (!runningOnRef) AddRunningOffReference(lines);
            return string.Join("\n", lines);
        }

        if (playedOnRef && runningOnRef)
        {
            lines.Add("  platform  reference platform on both sides: a hash mismatch below is a determinism finding.");
            return string.Join("\n", lines);
        }

        if (!playedOnRef && !runningOnRef && manifestPlatform == runningPlatform)
        {
            lines.Add("");
            lines.Add($"  NOTICE: this session was played on {manifestPlatform} and is being inspected on the same");
            lines.Add($"          platform, which is not the reference ({Ref}). Its hashes are expected to");
            lines.Add("          reproduce HERE — CR-013 §8.4: the Windows runner reproduced the director's Windows");
            lines.Add("          trace exactly — so a mismatch below is a determinism finding for this platform. They");
            lines.Add("          are not the canonical artifact: against a reference replay they would diverge from");
            lines.Add("          turn 2 (CR-013 §8), and the goldens are defined on the reference platform (ADR-022).");
            return string.Join("\n", lines);
        }

        if (!playedOnRef)
        {
            lines.Add("");
            lines.Add($"  NOTICE: this session was played on {manifestPlatform}, not the reference platform ({Ref}).");
            lines.Add("          A replay on the reference platform is EXPECTED to report REPRODUCTION FAILED from");
            lines.Add("          turn 2: the two platforms' Exp/Sqrt differ in the last ulp (CR-013 §8, measured on");
            lines.Add("          seed 42). That is a cross-platform divergence, not a determinism defect. The");
            lines.Add("          population, food and settlements columns remain comparable — they agreed to the");
            lines.Add("          unit on every measured turn (CR-013 §2) — so the turns reported below are still");
            lines.Add("          the session's turns. Judge whether the session REPRODUCES on the machine that");
            lines.Add("          played it: run sim inspect there.");
        }

        if (!runningOnRef) AddRunningOffReference(lines);
        return string.Join("\n", lines);
    }

    private static void AddRunningOffReference(List<string> lines)
    {
        lines.Add("");
        lines.Add($"  NOTICE: sim inspect itself is running off the reference platform ({Ref}). A trace recorded");
        lines.Add("          on the reference platform is EXPECTED to fail the hash cross-check here from turn 2");
        lines.Add("          (CR-013 §8) while the population, food and settlements columns stay comparable to");
        lines.Add("          the unit (CR-013 §2). Reproduction of a reference-platform session is judged on the");
        lines.Add("          reference platform; reproduction of a session played on THIS machine is judged here.");
    }
}
