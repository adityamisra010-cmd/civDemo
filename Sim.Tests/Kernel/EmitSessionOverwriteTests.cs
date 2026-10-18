using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-057 (2026-10-18) — `--emit-session` NEVER OVERWRITES A SESSION RECORD. The run id is derived from the run's identity
/// at launch, not its length, so two different runs can share it: measured, `run --founded --seed 42 --turns 2` and
/// `--turns 3` both emit run 70ec7bc67542fcda. Into one directory the second run silently overwrote the first's manifest,
/// log, trace, telemetry and forensic record. It is now refused (a usage error, exit 1) before any file is written —
/// `replay`'s `--report-jsonl` included — and the first record stays byte for byte.
/// </summary>
public class EmitSessionOverwriteTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(4);
    private static readonly string[] Founded = ["--founded", "--seed", "42", "--size", "256", "--settlements", "4"];

    private static (string[] Files, byte[][] Bytes) Contents(string dir)
    {
        string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        return (files, files.Select(File.ReadAllBytes).ToArray());
    }

    [Fact]
    public void ASecondRunWithTheSameRunId_IsRefused_BeforeWritingAnything_AndTheFirstRecordStays()
    {
        string root = Path.Combine(Path.GetTempPath(), "d057-emit-" + Guid.NewGuid().ToString("N"));
        string dir = Path.Combine(root, "same");
        Directory.CreateDirectory(root);
        try
        {
            (int first, string out1, string err1) = CliProcess.Run(Limit, ["run", .. Founded, "--turns", "2", "--emit-session", dir]);
            Assert.True(first == 0, err1);
            string runId = out1.Split("(run ")[1].Split(')')[0];
            var before = Contents(root);
            Assert.Contains(Path.Combine(dir, "session-" + runId + ".json"), before.Files);

            string hashes = Path.Combine(root, "h.log"), report = Path.Combine(root, "r.jsonl"), split = Path.Combine(root, "s.rep");
            foreach (string[] again in new[]
                     {
                         new[] { "run", "--turns", "3", "--emit-session", dir, "--hash-log", hashes, "--split-report", split },
                         new[] { "replay", "--turns", "3", "--orders", Path.Combine(dir, "orders-" + runId + ".bin"),
                             "--emit-session", dir, "--hash-log", hashes, "--report-jsonl", report },
                     })
            {
                (int exit, _, string err) = CliProcess.Run(Limit, [again[0], .. Founded, .. again[1..]]);
                Assert.True(exit == 1, $"{again[0]}: expected the usage exit 1, got {exit}: {err}");
                Assert.Contains($"--emit-session {dir}: it already holds a session record for run {runId} (session-{runId}.json), "
                    + "which this run would overwrite.", err);
                var after = Contents(root);
                Assert.Equal(before.Files, after.Files);                     // no hash log, report or split report either
                for (int i = 0; i < after.Files.Length; i++) Assert.Equal(before.Bytes[i], after.Bytes[i]);
            }

            // Another directory: the same run emits as before.
            (int other, _, string err2) = CliProcess.Run(Limit, ["run", .. Founded, "--turns", "3", "--emit-session", Path.Combine(root, "other")]);
            Assert.True(other == 0, err2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
