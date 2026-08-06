namespace Sim.Tests.TestUtil;

/// <summary>
/// Resolves the repository root from the test binary's location by walking up
/// to the `Sim.slnx` marker — the same technique T3.9b's OrderLogFixtureTests
/// uses to reach `docs/*.bin`, lifted here so the next test that needs a repo
/// path does not invent a third copy.
///
/// OrderLogFixtureTests was deliberately NOT refactored onto this: it is a
/// shipped guard, and ADR-015 §7.17 re-opens a modified guard's red proof.
/// Re-proving a passing guard to remove six duplicated lines is a worse trade
/// than the duplication. Whoever next touches that file for its own reasons
/// can fold it in then.
/// </summary>
public static class RepoPaths
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sim.slnx")))
            dir = dir.Parent;
        // No root means the test cannot do its job. FAIL — never skip, never
        // silently pass: a green test that could not find what it asserts on
        // is worse than no test (the T1.1/T1.3 empty-table precedent).
        if (dir is null)
            throw new InvalidOperationException(
                "repository root not found: no Sim.slnx above " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}
