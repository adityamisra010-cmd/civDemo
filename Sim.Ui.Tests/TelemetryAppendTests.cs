using System.Text.Json;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// P0 — THE TELEMETRY WRITE PATH. Before this packet
/// <c>UiSession.ExportTelemetry</c> was File.Create + write-all: every End Turn
/// truncated the artifact and re-serialized every turn played so far, so the
/// bytes written over a session were quadratic in its length and the file was
/// INVALID (truncated to nothing) for the whole duration of every save.
///
/// These tests pin the three properties the replacement must have and the old
/// path did not: bytes written per turn is O(the new record) and not O(the
/// file); a record already on disk survives a process death mid-write; and the
/// resulting file is still exactly the file the write-all path produced, line
/// for line and byte for byte, so no reader of telemetry/v2 can tell.
/// </summary>
public class TelemetryAppendTests
{
    private static Sim.Ui.UiSession Started() =>
        Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);

    private static string Dir(string tag)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"civsim-p0-{tag}");
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void AppendingEachTurnProducesEXACTLYTheFileTheWriteAllPathProduced()
    {
        // The compatibility pin. If this ever fails the artifact contract moved,
        // and telemetry/v2 promises it did not.
        string dir = Dir("identical");
        try
        {
            string incremental = Path.Combine(dir, "telemetry-incremental.jsonl");
            Sim.Ui.UiSession session = Started();
            for (int t = 0; t < 8; t++)
            {
                session.EndTurn();
                session.ExportTelemetry(incremental);   // one save per End Turn
            }
            session.FinalizeTelemetry(incremental);

            string whole = Path.Combine(dir, "telemetry-whole.jsonl");
            using (FileStream file = File.Create(whole))
            {
                Sim.Core.Observability.TelemetryWriter.WriteAll(file, session.Observations);
            }

            Assert.Equal(File.ReadAllBytes(whole), File.ReadAllBytes(incremental));
            Assert.Equal(8, File.ReadAllLines(incremental).Length);
            Assert.Equal(8, session.TelemetryRecordsWritten);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void WriteAmplification_BytesWrittenPerTurnIsTheSizeOfTheNEWRecordNotOfTheFile()
    {
        // THE DEFECT, MEASURED. Under write-all the total bytes written after N
        // saves is Σ(file size after each save) ≈ N/2 × final size. Under append
        // it is the final size exactly — every byte is written once and never
        // rewritten. The assertion is EXACT equality, not a ratio: the append
        // path has no second writer, so there is nothing to round.
        string dir = Dir("amplification");
        try
        {
            string path = Path.Combine(dir, "telemetry.jsonl");
            Sim.Ui.UiSession session = Started();
            var perSave = new List<long>();
            long previous = 0;
            for (int t = 0; t < 12; t++)
            {
                session.EndTurn();
                session.ExportTelemetry(path);
                perSave.Add(session.TelemetryBytesWritten - previous);
                previous = session.TelemetryBytesWritten;
            }

            long fileSize = new FileInfo(path).Length;
            Assert.Equal(fileSize, session.TelemetryBytesWritten);

            // Each save wrote exactly one record, and no save wrote the file.
            string[] lines = File.ReadAllLines(path);
            Assert.Equal(12, lines.Length);
            for (int i = 0; i < perSave.Count; i++)
            {
                long lineBytes = System.Text.Encoding.UTF8.GetByteCount(lines[i]) + 1;
                Assert.Equal(lineBytes, perSave[i]);
                // and, the shape of the claim: the 12th save is not 12× the 1st.
                Assert.True(perSave[i] < fileSize,
                    $"save {i} wrote {perSave[i]} bytes into a {fileSize}-byte file — that is a rewrite.");
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SavingTwiceWithNoNewTurnWritesNOTHING()
    {
        string dir = Dir("idempotent");
        try
        {
            string path = Path.Combine(dir, "telemetry.jsonl");
            Sim.Ui.UiSession session = Started();
            session.EndTurn();
            session.ExportTelemetry(path);
            long after = session.TelemetryBytesWritten;
            session.ExportTelemetry(path);
            session.ExportTelemetry(path);
            Assert.Equal(after, session.TelemetryBytesWritten);
            Assert.Single(File.ReadAllLines(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void KilledMidWrite_EveryCOMPLETEDRecordBeforeTheDeathIsStillValid()
    {
        // A process death is simulated by truncating the artifact at an
        // arbitrary byte — the state a half-written record leaves behind. The
        // property: every line that had already been completed (every '\n'
        // before the cut) still parses as a whole telemetry/v2 record. Under the
        // old File.Create path the equivalent cut left a file that was missing
        // EVERY turn, because the truncation happened before any byte was
        // rewritten.
        string dir = Dir("killed");
        try
        {
            string path = Path.Combine(dir, "telemetry.jsonl");
            Sim.Ui.UiSession session = Started();
            for (int t = 0; t < 6; t++)
            {
                session.EndTurn();
                session.ExportTelemetry(path);
            }
            byte[] full = File.ReadAllBytes(path);

            // Cut at eleven points across the file, including inside records.
            for (int k = 1; k <= 11; k++)
            {
                int cut = (int)((long)full.Length * k / 12);
                string wounded = Path.Combine(dir, $"wounded-{k}.jsonl");
                File.WriteAllBytes(wounded, full.AsSpan(0, cut).ToArray());

                int completed = 0;
                for (int i = 0; i < cut; i++) if (full[i] == (byte)'\n') completed++;

                string[] lines = File.ReadAllText(wounded).Split('\n');
                Assert.True(lines.Length - 1 >= completed);
                for (int i = 0; i < completed; i++)
                {
                    using JsonDocument doc = JsonDocument.Parse(lines[i]);
                    Assert.Equal("telemetry/v2", doc.RootElement.GetProperty("schema").GetString());
                    Assert.Equal(i + 1, doc.RootElement.GetProperty("turn").GetProperty("turn").GetInt64());
                }
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void AFreshPathIsWrittenWHOLE_soAnExportIsStillAnExport()
    {
        // Resume is scoped to the file this session has been appending to. Asked
        // for a path it has never written, the session writes the complete
        // artifact rather than a suffix of it.
        string dir = Dir("fresh");
        try
        {
            string first = Path.Combine(dir, "a.jsonl");
            string second = Path.Combine(dir, "b.jsonl");
            Sim.Ui.UiSession session = Started();
            for (int t = 0; t < 5; t++) { session.EndTurn(); session.ExportTelemetry(first); }
            session.ExportTelemetry(second);
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
