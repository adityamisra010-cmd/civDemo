using Sim.Core.Kernel;
using Xunit;

namespace Sim.Tests.Kernel;

/// <summary>
/// T4.1e FOLLOW-UP — THE DUPLICATED PIN GUARD.
///
/// The founded golden is pinned in TWO places: <see cref="SnapshotTests"/> and
/// `.github/workflows/ci.yml`'s FOUNDED_GOLDEN, which the cross-process
/// determinism job compares against. T4.1e re-pinned the suite's copy and not
/// the yaml's, and `determinism-xproc` went red on main while a full local
/// suite stayed green — a local green CANNOT see a constant that lives in CI
/// yaml.
///
/// TWO FIXES WERE AVAILABLE AND THIS IS THE CHEAPER ONE, DELIBERATELY:
///   (a) make ci.yml read the value from the suite's source — REMOVES the
///       possibility, but the golden's home is a C# const inside the test that
///       computes the hash, and yaml cannot read C#. Sharing it means moving a
///       TEST PIN out of the tests into a data file, which trades one
///       duplication for a worse one: a golden nobody reads beside its assert.
///   (b) THIS TEST — assert the two copies agree. It does not prevent the
///       duplication; it makes drift FAIL LOUDLY IN THE SUITE, on the machine
///       of whoever re-pins, before the push. Cheaper, and it catches the exact
///       failure that occurred.
/// If a third copy of this constant is ever added, extend this test rather than
/// trusting the reviewer to notice.
/// </summary>
public class CiPinAgreementTests
{
    [Fact]
    public void CiFoundedGolden_AgreesWithTheSuitePin()
    {
        string root = RepoRoot();
        string ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        string suite = File.ReadAllText(Path.Combine(root, "Sim.Tests", "Kernel", "SnapshotTests.cs"));

        string ciPin = Extract(ci, "FOUNDED_GOLDEN=");
        Assert.False(string.IsNullOrEmpty(ciPin),
            "ci.yml no longer defines FOUNDED_GOLDEN — if the xproc job stopped pinning the " +
            "founded golden, delete this test deliberately; do not let it pass vacuously.");

        Assert.Contains(ciPin, suite);
    }

    /// <summary>The 64-hex value following <paramref name="marker"/>.</summary>
    private static string Extract(string text, string marker)
    {
        int i = text.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return "";
        int start = i + marker.Length, end = start;
        while (end < text.Length && Uri.IsHexDigit(text[end])) end++;
        return end - start == 64 ? text[start..end] : "";
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
