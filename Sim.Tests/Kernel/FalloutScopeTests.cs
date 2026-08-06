using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// T3.11 ITEM 4a — THE PER-SOLUTION FALLOUT RULE, mechanically enforced as far
/// as it can be.
///
/// THE DEFECT IT ANSWERS (T3.8 certification fix pass, director ruling): T3.8's
/// fallout enumeration ran Sim.Tests ONLY. A fourteenth real fallout item — the
/// HUD needs-block pin, "Shelter: 0.00" asserted against a founded world that
/// now arrives HOUSED — was invisible to that enumeration and surfaced only
/// when Sim.Ui.Tests ran at handback. The rule: any packet changing sim state
/// the HUD displays enumerates fallout across BOTH test projects.
///
/// WHAT A TEST CAN AND CANNOT ENFORCE — stated plainly rather than implied.
/// It CANNOT enforce the rule itself: no assertion can tell whether an agent
/// enumerated one project or two, because enumeration is something a person
/// does before writing code. What it CAN do is guarantee the rule remains
/// PERFORMABLE — that both test projects exist in the solution and that CI's
/// test invocation is solution-wide, so a run cannot silently narrow to
/// Sim.Tests and re-create the exact blind spot T3.8 fell into. That is the
/// mechanical half; the judgement half is the queue rule, and it stays there.
///
/// If CI is ever deliberately narrowed, this test fails and names the reason —
/// which is the point. Re-scope it in the same commit, on purpose.
/// </summary>
public class FalloutScopeTests
{
    [Fact]
    public void BothTestProjects_AreInTheSolution()
    {
        string slnx = File.ReadAllText(Path.Combine(RepoPaths.Root(), "Sim.slnx"));
        Assert.Contains("Sim.Tests/Sim.Tests.csproj", slnx);
        Assert.Contains("Sim.Ui.Tests/Sim.Ui.Tests.csproj", slnx);
    }

    [Fact]
    public void TheMainCiTestStep_RunsTheWholeSolution_NotOneProject()
    {
        // The load-bearing line: `dotnet test Sim.slnx`. A future edit to
        // `dotnet test Sim.Tests` would still be green locally and would still
        // look like "the tests run" — and would restore the T3.8 blind spot in
        // CI itself. The calibration job legitimately targets Sim.Tests alone
        // (it filters one class), so this asserts the SOLUTION-WIDE step
        // exists, not that no per-project invocation may appear anywhere.
        //
        // ANCHORED ON THE STEP, NOT THE SUBSTRING — and that is a red-proof
        // finding against this test's own first draft. Written as a bare
        // Assert.Contains on "dotnet test Sim.slnx --no-build --configuration
        // Release", the guard stayed GREEN when the main step was narrowed to
        // `dotnet test Sim.Tests`, because the DETERMINISM job further down
        // contains the same substring inside its own `run: |` block. A guard
        // satisfied by an unrelated line elsewhere in the file is not a guard.
        // The `run: ` prefix and the terminating newline pin it to the main
        // step: the determinism job's line has no `run: ` prefix (it sits in a
        // block scalar) and ends with a ` \` continuation, so it cannot
        // satisfy this. Both arms are proven red below at T3.11 Item 4a.
        string ci = File.ReadAllText(
            Path.Combine(RepoPaths.Root(), ".github", "workflows", "ci.yml"))
            .Replace("\r\n", "\n");
        Assert.Contains("run: dotnet test Sim.slnx --no-build --configuration Release\n", ci);
    }
}
