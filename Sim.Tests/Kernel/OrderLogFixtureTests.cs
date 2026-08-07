using Sim.Core.Kernel;

namespace Sim.Tests.Kernel;

/// <summary>
/// T3.9b: THE SHIPPED ORDER-LOG FIXTURES STILL LOAD.
///
/// WHY THIS EXISTS. T3.9b's packet anticipated an IoVersion bump ("the first
/// new order since M1's LaborAllocationOrder") and asked what would happen to
/// the director's session logs — several of which are load-bearing regression
/// evidence, not archives. Measured, the premise did not hold: the five-sector
/// order is OrderKind.SectorAllocation, shipped by T3.3 (D-032), so T3.9b adds
/// no kind, bumps no version, and the logs were never at risk.
///
/// The RISK the packet named is real even though this packet did not carry it,
/// and it was guarded by nothing: no test loaded these files. The measurement
/// that answered the question therefore becomes a permanent test, so the next
/// vocabulary or version change CANNOT break the M1/M2 regression fixtures
/// silently — it breaks here, by name, with the file listed.
///
/// The two docs/ logs are the director's own played sessions (the M2 gate and
/// the HELD-exit reproduction). They are not test content and are not copied
/// to the output directory, so this test resolves them from the repository
/// root and FAILS LOUDLY if it cannot — a fixture that quietly stopped being
/// checked would be the exact failure this file exists to prevent.
/// </summary>
public class OrderLogFixtureTests
{
    /// <summary>Every shipped order log, with the role that makes it
    /// load-bearing. Paths are repo-relative.</summary>
    public static TheoryData<string, string> ShippedLogs() => new()
    {
        { "Sim.Tests/Fixtures/first-reign-orders.bin", "the director's first reign — T1.9 trajectory pin" },
        { "Sim.Tests/Fixtures/t38-director-orders.bin", "the T3.9a gate session — T3.8 before/after column" },
        { "docs/orders-20260722-153834.bin", "the M2 gate session" },
        { "docs/orders-20260724-164734-held-exit.bin", "the M2 HELD-exit reproduction" },
        // T4.1b: the director's M3-exit session, committed at main 3185a6b. It
        // landed in docs/session-logs/ rather than docs/ — the newer layout —
        // and this test resolves specific filenames, so it did NOT cover the new
        // folder until this line existed. The log was committed to be GUARDED;
        // committed-but-unguarded is the state this entry closes.
        //
        // The two older logs are deliberately LEFT where they are: moving them
        // would be a rename in the same commit as a coverage fix, and the two
        // failures look identical in the log (a missing file). Consolidating the
        // layout is a separate, trivial commit if the director wants it.
        { "docs/session-logs/orders-20260807-145349.bin", "the director's M3-exit session (30 records, 6 sector-allocation events, settlements 2 and 11)" },
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sim.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir); // no root => the test cannot do its job; fail, never skip
        return dir!.FullName;
    }

    [Theory]
    [MemberData(nameof(ShippedLogs))]
    public void ShippedOrderLog_StillLoadsAtTheCurrentIoVersion(string relativePath, string role)
    {
        string path = Path.Combine(RepoRoot(), relativePath);
        Assert.True(File.Exists(path),
            $"{relativePath} is missing ({role}) — a load-bearing order log has been moved or deleted.");

        using FileStream stream = File.OpenRead(path);
        // Load performs the header check, the version check and per-record
        // payload validation — the whole compatibility surface in one call.
        OrderLog log = OrderLog.Load(stream);
        Assert.True(log.Count > 0, $"{relativePath} loaded but is EMPTY ({role}) — vacuous evidence.");

        // Every record's kind must be one this build understands. Load already
        // refuses unknown kinds; asserting it here names the file when a future
        // vocabulary change makes an old log unreadable.
        for (int i = 0; i < log.Count; i++)
        {
            OrderKind kind = log[i].Kind;
            Assert.True(
                kind is OrderKind.SetRainBias or OrderKind.LaborAllocation or OrderKind.SectorAllocation,
                $"{relativePath} record {i} carries kind {(int)kind}, which this build does not understand.");
        }
    }

    [Fact]
    public void OrderLogIoVersion_IsPinned_SoABumpIsADeliberateAct()
    {
        // A bare pin, and the comment is the point: bumping IoVersion makes
        // every shipped log above unreadable unless a migration path ships with
        // it. The bump is legitimate — it is simply never accidental. T3.9b
        // did NOT bump it: the five-sector control reuses T3.3's
        // OrderKind.SectorAllocation, so version 1 still reads every log.
        Assert.Equal(1, OrderLog.IoVersion);
    }
}
