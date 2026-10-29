using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.Observability.Forensic;
using Sim.Ui.Export;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>The session-export fixtures (Fixtures/session-export/README.md) and a session written to disk exactly as the
/// game writes it.</summary>
internal static class ExportFixtures
{
    public static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "session-export", name);

    /// <summary>A fixture session as (file name, bytes) pairs, the .gz files un-gzipped IN MEMORY under their own name.</summary>
    public static List<(string Name, byte[] Bytes)> Files(string name)
    {
        var files = new List<(string, byte[])>();
        string[] paths = Directory.GetFiles(Dir(name));
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (string p in paths)
        {
            string f = Path.GetFileName(p);
            if (f.StartsWith("prototype-", StringComparison.Ordinal) || f.EndsWith(".md", StringComparison.Ordinal)) continue;
            if (f.EndsWith(".gz", StringComparison.Ordinal)) files.Add((f[..^3], Gunzip(File.ReadAllBytes(p))));
            else files.Add((f, File.ReadAllBytes(p)));
        }
        return files;
    }

    public static Func<string, byte[]?> Reader(List<(string Name, byte[] Bytes)> files) => name =>
    {
        foreach ((string n, byte[] b) in files)
            if (n == name) return b;
        return null;
    };

    public static string Manifest(List<(string Name, byte[] Bytes)> files)
    {
        foreach ((string n, byte[] _) in files)
            if (n.StartsWith("session-", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)) return n;
        throw new InvalidOperationException("no manifest");
    }

    public static byte[] Bytes(List<(string Name, byte[] Bytes)> files, string prefix)
    {
        foreach ((string n, byte[] b) in files)
            if (n.StartsWith(prefix, StringComparison.Ordinal)) return b;
        throw new InvalidOperationException("no " + prefix);
    }

    public static string[] Lines(byte[] utf8)
    {
        string t = new UTF8Encoding(false, true).GetString(utf8);
        string[] parts = t.Split('\n');
        return parts[^1].Length == 0 ? parts[..^1] : parts;
    }

    /// <summary>The prototype's cells for a fixture (prototype/encode.py output), one per line.</summary>
    public static string[] PrototypeCells(string name) =>
        Lines(Gunzip(File.ReadAllBytes(Path.Combine(Dir(name), "prototype-cells.txt.gz"))));

    public static byte[] Gunzip(byte[] gz)
    {
        using var input = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Plays <paramref name="turns"/> End Turns and writes the session's files as GameUi does — the manifest and the
    /// forensic run record at launch, SaveSession after every End Turn (log, chronicle, trace, telemetry), then the close:
    /// FinalizeTelemetry and ExportForensicClose. Returns the order-log path.</summary>
    public static string PlayToDisk(UiSession session, string dir, string stamp, int turns, Action<UiSession, int>? orders = null)
    {
        Directory.CreateDirectory(dir);
        string log = Path.Combine(dir, "orders-" + stamp + ".bin");
        session.ExportManifest("2026-10-28 12:00:00", log);
        session.ExportForensicRun("2026-10-28 12:00:00+00:00", log);
        for (int t = 0; t < turns; t++)
        {
            orders?.Invoke(session, t);
            session.EndTurn();
            session.Save(log);
            session.ExportChronicle(UiSession.ChroniclePath(log));
            session.ExportTrace(UiSession.TracePath(log));
            session.ExportTelemetry(UiSession.TelemetryPath(log));
        }
        session.FinalizeTelemetry(UiSession.TelemetryPath(log));
        session.ExportForensicClose(log);
        return log;
    }

    /// <summary>The player's orders of the current-tree fixture: a research target, then two labour splits.</summary>
    public static void FixtureOrders(UiSession s, int t)
    {
        Sim.Core.Systems.Research.ResearchContent research = s.Config.Research!;
        if (t == 0) s.EmitResearchOrder(research.Nodes[research.IndexOfId("cordage")].Key);
        if (t == 1) s.EmitSectorOrders([45, 20, 15, 12, 8], s.World.Settlements[0].Id.Value);
        if (t == 2) s.EmitSectorOrders([50, 10, 20, 10, 10], s.World.Settlements[1].Id.Value);
    }

    public static string Sha(string path) => ForensicIdentity.Sha256Hex(File.ReadAllBytes(path));
}

/// <summary>
/// ADR-059 (D-066 A14–A17) — THE ONE-FILE SESSION EXPORT. The codec against the prototype's cells (the README §5.5
/// goldens), its adversarial cases, the A17 continuation cells on a 64-settlement world, the deterministic OOXML writer,
/// the workbook goldens of the Director's 68-turn session and of a current-tree session, the headless command and the
/// session-close hook.
/// </summary>
public class SessionExportTests
{
    /// <summary>sha256 of the Director's 68-turn session's workbook (Fixtures/session-export/director-68).</summary>
    internal const string DirectorWorkbookSha = "136e853c72d8596cb81785dd438dfeb6dbe527ca0e1ce53fd8532e8bf0c365e5";

    /// <summary>sha256 of the current-tree (telemetry/v5) session's workbook (Fixtures/session-export/current-v5).</summary>
    internal const string CurrentWorkbookSha = "6d488a2c97bcbb143f274ddf97784cf4daf170d301b3e1e340718b532139560d";

    // ------------------------------------------------------------------------------------- codec goldens --

    [Theory]
    [InlineData("director-68", "v4", 68)]
    [InlineData("current-v5", "v5", 16)]
    public void TheCodecsCellsAreThePrototypes_AndEveryCellDecodesToItsLineByteForByte(string fixture, string codebook, int turns)
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files(fixture);
        string[] lines = ExportFixtures.Lines(ExportFixtures.Bytes(files, "telemetry-"));
        string[] proto = ExportFixtures.PrototypeCells(fixture);
        Assert.Equal(turns, lines.Length);
        Assert.Equal(lines.Length, proto.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            Assert.Equal(codebook, TelemetryCellCodec.CodebookOf(lines[i]).Version);
            string cell = TelemetryCellCodec.Encode(lines[i]);
            Assert.True(proto[i] == cell, $"line {i + 1}: the C# cell differs from the prototype's at character {FirstDiff(proto[i], cell)}");
            Assert.True(lines[i] == TelemetryCellCodec.Decode(cell), $"line {i + 1}: the cell does not decode to its line");
            Assert.StartsWith(codebook + " ", cell, StringComparison.Ordinal);
        }
    }

    private static int FirstDiff(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
            if (a[i] != b[i]) return i;
        return n;
    }

    // --------------------------------------------------------------------------------- adversarial codec --

    /// <summary>A real v5 line of the current-tree fixture, as a mutable JSON tree.</summary>
    private static JObj RealLine(int index = 3)
    {
        string[] lines = ExportFixtures.Lines(ExportFixtures.Bytes(ExportFixtures.Files("current-v5"), "telemetry-"));
        return (JObj)JsonText.Parse(lines[index]);
    }

    private static JObj Obj(JVal v, params string[] path)
    {
        JVal cur = v;
        foreach (string p in path)
            cur = cur is JArr a ? a.Items[int.Parse(p, System.Globalization.CultureInfo.InvariantCulture)] : ((JObj)cur).Get(p)!;
        return (JObj)cur;
    }

    private static string RoundTrip(JObj doc)
    {
        string line = JsonText.Serialize(doc);
        string cell = TelemetryCellCodec.EncodeChecked(line, "adversarial");
        Assert.Equal(line, TelemetryCellCodec.Decode(cell));
        return cell;
    }

    [Fact]
    public void JsonTextReprintsTheWritersLinesExactly()
    {
        // The codec's printer is the writer's printer: every real line of both fixtures re-prints byte for byte.
        foreach (string fixture in new[] { "director-68", "current-v5" })
            foreach (string line in ExportFixtures.Lines(ExportFixtures.Bytes(ExportFixtures.Files(fixture), "telemetry-")))
                Assert.Equal(line, JsonText.Serialize(JsonText.Parse(line)));
    }

    [Fact]
    public void NegativeZero_IsKeptDistinctFromZero_DefaultsAreComparedAsText()
    {
        JObj doc = RealLine();
        JObj food = Obj(doc, "settlements", "0", "food");
        food.Set("deficitRatio", new JNum("0"));
        string zero = RoundTrip(doc);
        food.Set("deficitRatio", new JNum("-0"));
        string negZero = RoundTrip(doc);
        Assert.NotEqual(zero, negZero);                                     // −0 is written, 0 is the default
        Assert.Contains(",-0,", negZero.Split('|')[1], StringComparison.Ordinal);
        food.Set("deficitRatio", new JNum("0.0"));                          // text the writer never prints still survives
        RoundTrip(doc);
        food.Set("deficitRatio", new JNum("-0.5"));
        Assert.Contains("-.5", RoundTrip(doc).Split('|')[1], StringComparison.Ordinal);
        food.Set("deficitRatio", new JNum("1E-05"));
        Assert.Contains("1E-05", RoundTrip(doc).Split('|')[1], StringComparison.Ordinal);
    }

    [Fact]
    public void NaNAndInfinities_AreTheirCodes_InFieldsAndInDoubleArrays()
    {
        JObj doc = RealLine();
        JObj s0 = Obj(doc, "settlements", "0");
        Obj(s0, "food").Set("deficitRatio", new JStr("NaN"));
        Obj(s0, "housing").Set("capacity", new JStr("Infinity"));
        Obj(s0, "housing").Set("need", new JStr("-Infinity"));
        ((JArr)Obj(s0, "economy").Get("sectorShares")!).Items[1] = new JStr("NaN");
        string seg = RoundTrip(doc).Split('|')[1];
        Assert.Contains(",N,", seg, StringComparison.Ordinal);
        Assert.Contains("H:", seg, StringComparison.Ordinal);
        Assert.Contains(",I,-I", seg, StringComparison.Ordinal);
        Assert.Contains(";N;", seg, StringComparison.Ordinal);
        // A string that is NOT one of the three is not a number: it takes the ! escape, and still round-trips.
        Obj(s0, "food").Set("deficitRatio", new JStr("nan"));
        Assert.Contains("!\"nan\"", RoundTrip(doc).Split('|')[1], StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyTablesAndLines_RoundTrip()
    {
        JObj doc = RealLine();
        // an empty attractiveness list where the line-level default is not empty → Ma~
        JObj mig = Obj(doc, "settlements", "1", "migration");
        mig.Set("allAttractiveness", new JArr());
        Assert.Contains(" Ma~", RoundTrip(doc).Split('|')[2], StringComparison.Ordinal);
        // no orders, no policy rows
        Obj(doc, "turn").Set("orders", new JArr());
        Obj(doc, "turn").Set("policy", new JArr());
        foreach (JVal s in ((JArr)doc.Get("settlements")!).Items) ((JObj)s).Set("orders", new JArr());
        RoundTrip(doc);
        // no settlements at all: the cell is the turn segment alone
        doc.Set("settlements", new JArr());
        string cell = RoundTrip(doc);
        Assert.DoesNotContain("|", cell, StringComparison.Ordinal);
    }

    [Fact]
    public void Escapes_EverySeparatorAndControlCharacterInAStringRoundTrips()
    {
        JObj doc = RealLine();
        const string nasty = "a,b;c/d|e f~g*h_i!j:k\"l\\m\nn\to<p>q&r'+`sét\U0001F600u\u0001v_x0041_";
        Obj(doc, "settlements", "0", "population").Set("colonistsDepartedIdentity", new JStr(nasty));
        Obj(doc, "settlements", "2", "housing").Set("builtDecayedSplit", new JStr(""));
        string cell = RoundTrip(doc);
        string[] segs = cell.Split('|');
        Assert.Equal(((JArr)doc.Get("settlements")!).Items.Count + 1, segs.Length);   // no raw | leaked
        foreach (string seg in segs) Assert.DoesNotContain("  ", seg, StringComparison.Ordinal);
        Assert.Contains("!\"a\\u002cb\\u003bc\\u002fd\\u007ce\\u0020f\\u007eg\\u002ah\\u005fi", segs[1], StringComparison.Ordinal);
        foreach (char ch in cell) Assert.True(ch >= 0x20 && ch < 0x7F, "a coded cell is printable ASCII");
    }

    [Fact]
    public void AnOrderKindTheCodebookDoesNotName_TakesTheEscape_AndOptionalKeysKeepPresenceExactly()
    {
        JObj doc = RealLine();
        JObj grain = Obj(doc, "turn", "grain");
        // kind 10 is reserved (M7 MoveFormation): its name prints as "10" — no code, so the ! escape
        var order = new JObj();
        foreach ((string k, JVal v) in new (string, JVal)[]
                 {
                     ("index", new JNum("0")), ("turn", new JNum("3")), ("actor", new JNum("1")), ("kind", new JStr("10")),
                     ("targetId", new JNum("4")), ("settlement", new JNum("-1")), ("sector", new JNum("-1")), ("amount", new JNum("2.5")),
                 })
            order.Set(k, v);
        ((JArr)Obj(doc, "turn").Get("orders")!).Items.Add(order);
        Assert.Contains("!\"10\"", RoundTrip(doc), StringComparison.Ordinal);

        // v5's OPTIONAL keys: absent, present, present as an explicit 0 — three different lines, three different cells
        RemoveKey(grain, "military");
        RemoveKey(grain, "fodder");
        string absent = RoundTrip(doc);
        InsertAfter(grain, "overflow", "military", new JNum("12"));
        string military = RoundTrip(doc);
        InsertAfter(grain, "military", "fodder", new JNum("7"));
        string both = RoundTrip(doc);
        RemoveKey(grain, "military");
        RemoveKey(grain, "fodder");
        InsertAfter(grain, "overflow", "military", new JNum("0"));
        string explicitZero = RoundTrip(doc);
        Assert.Equal(4, new HashSet<string>([absent, military, both, explicitZero], StringComparer.Ordinal).Count);
    }

    private static void RemoveKey(JObj o, string key)
    {
        int i = o.IndexOf(key);
        if (i >= 0) o.RemoveAt(i);
    }

    private static void InsertAfter(JObj o, string after, string key, JVal value)
    {
        int at = o.IndexOf(after) + 1;
        o.Keys.Insert(at, key);
        o.Values.Insert(at, value);
    }

    /// <summary>Tie-dense: the line-level attractiveness table A is the most common per-record list, the FIRST SEEN
    /// winning a tie (a stable, order-based tie-break — no hash, no dictionary order).</summary>
    [Fact]
    public void TheLineLevelAttractivenessTable_TieBreaksOnFirstSeen()
    {
        JObj doc = RealLine();
        var setts = (JArr)doc.Get("settlements")!;
        Assert.True(setts.Items.Count >= 4);
        JArr listA = AttrList(("0", "1.5"), ("1", "2.5"));
        JArr listB = AttrList(("0", "3.5"), ("1", "4.5"));
        for (int i = 0; i < setts.Items.Count; i++)
            Obj(setts.Items[i], "migration").Set("allAttractiveness", i % 2 == 0 ? listA : listB);   // an exact tie when even
        int n = setts.Items.Count;
        string first = RoundTrip(doc);
        Assert.Contains(" A:1.5/2.5", first.Split('|')[0], StringComparison.Ordinal);   // listA was seen first (settlement = row: default)
        for (int i = 0; i < n; i++)
            Obj(setts.Items[i], "migration").Set("allAttractiveness", i % 2 == 0 ? listB : listA);
        string swapped = RoundTrip(doc);
        Assert.Contains(" A:3.5/4.5", swapped.Split('|')[0], StringComparison.Ordinal);  // now listB is first
        Assert.Equal(0, n % 2);   // the fixture's 12 settlements: an exact 6/6 tie, broken by first sight
    }

    private static JArr AttrList(params (string Settlement, string Value)[] rows)
    {
        var a = new JArr();
        foreach ((string s, string v) in rows)
        {
            var o = new JObj();
            o.Set("settlement", new JNum(s));
            o.Set("value", new JNum(v));
            a.Items.Add(o);
        }
        return a;
    }

    [Fact]
    public void RefusalsAreNamed_UnknownSchema_ChangedShape_BadCell()
    {
        JObj doc = RealLine();
        doc.Set("schema", new JStr("telemetry/v6"));
        Assert.Equal(SessionExportException.TelemetrySchemaUnknown,
            Assert.Throws<SessionExportException>(() => TelemetryCellCodec.Encode(JsonText.Serialize(doc))).Code);
        doc.Set("schema", new JStr("telemetry/v5"));
        JObj grain = Obj(doc, "turn", "grain");
        (grain.Keys[0], grain.Keys[1]) = (grain.Keys[1], grain.Keys[0]);   // key order differs from the codebook
        Assert.Equal(SessionExportException.TelemetryShape,
            Assert.Throws<SessionExportException>(() => TelemetryCellCodec.Encode(JsonText.Serialize(doc))).Code);
        // a v4-shaped codebook cannot read the v5 shape: the old tag on a new line is refused, not mis-encoded
        (grain.Keys[0], grain.Keys[1]) = (grain.Keys[1], grain.Keys[0]);
        doc.Set("schema", new JStr("telemetry/v4"));
        if (grain.IndexOf("military") >= 0 || grain.IndexOf("fodder") >= 0)
            Assert.Equal(SessionExportException.TelemetryShape,
                Assert.Throws<SessionExportException>(() => TelemetryCellCodec.Encode(JsonText.Serialize(doc))).Code);
        foreach (string bad in new[] { "v9 t:1,2,3", "v5 t:x,1,1", "v5 t:1,2,3 Q:1", "v5 t:1,2,3|#1 P:a", "v5 t:1,2,3 k:*z", "" })
            Assert.Equal(SessionExportException.CellUndecodable,
                Assert.Throws<SessionExportException>(() => TelemetryCellCodec.Decode(bad)).Code);
    }

    // ------------------------------------------------------------------------------------- number text --

    [Fact]
    public void CellNumberText_IsTheWritersShortestText_AndParsesBackBitForBit()
    {
        var values = new List<double>
        {
            0.0, -0.0, 1.0, -1.0, 0.5, -0.5, 0.1, 1.0 / 3.0, 2.0 / 3.0, 1e-5, 1e-7, 1.5e-300, double.Epsilon, -double.Epsilon,
            double.MaxValue, double.MinValue, 1e15, 1e16, 1e17, 1e21, 123456789012345680.0, 0.30000000000000004, 100.0, 20.0, 0.05,
            BitConverter.Int64BitsToDouble(0x000FFFFFFFFFFFFF), 9007199254740993.0, 4.35, 2.675,
        };
        for (int i = 1; i < 400; i++) values.Add(i / 7.0 - 20.0);
        foreach (double d in values)
        {
            string cell = CellNumber.Format(d);
            Assert.Equal(BitConverter.DoubleToInt64Bits(d), BitConverter.DoubleToInt64Bits(CellNumber.Parse(cell)));
            // The same text the telemetry writer (Utf8JsonWriter) prints, with the grammar's 0. → . shortening.
            using var ms = new MemoryStream();
            using (var w = new System.Text.Json.Utf8JsonWriter(ms)) w.WriteNumberValue(d);
            string json = Encoding.UTF8.GetString(ms.ToArray());
            string expected = json.StartsWith("0.", StringComparison.Ordinal) ? json[1..]
                : json.StartsWith("-0.", StringComparison.Ordinal) ? "-" + json[2..] : json;
            Assert.Equal(expected, cell);
        }
        Assert.Equal("-0", CellNumber.Format(-0.0));
        Assert.Equal("N", CellNumber.Format(double.NaN));
        Assert.Equal("I", CellNumber.Format(double.PositiveInfinity));
        Assert.Equal("-I", CellNumber.Format(double.NegativeInfinity));
        Assert.True(double.IsNaN(CellNumber.Parse("N")));
    }

    // ------------------------------------------------------------------------------- continuation (A17) --

    [Fact]
    public void Split_CutsOnlyAtSegmentBoundaries_NeverTruncates_AndJoinRestoresTheCell()
    {
        string[] proto = ExportFixtures.PrototypeCells("director-68");
        foreach (int limit in new[] { 900, 1500, 4000, 10096 })
            foreach (string cell in proto)
            {
                IReadOnlyList<string> parts = TelemetryCellCodec.Split(cell, limit);
                foreach (string p in parts) Assert.True(p.Length <= limit);
                for (int k = 1; k < parts.Count; k++) Assert.StartsWith(">|#", parts[k], StringComparison.Ordinal);
                Assert.Equal(cell, TelemetryCellCodec.Join(parts));
                if (cell.Length <= limit) Assert.Single(parts);
            }
        // a single settlement segment over the limit cannot be split: a named refusal, never a truncated cell
        Assert.Equal(SessionExportException.CellTooLarge,
            Assert.Throws<SessionExportException>(() => TelemetryCellCodec.Split(proto[0], 300)).Code);
    }

    /// <summary>D-066 A17 on a synthetic world of 64 settlements (≈650 coded characters each, so a turn overflows one
    /// cell): the workbook holds no cell over 32,767 characters, has continuation rows, and every turn joins and decodes
    /// back to its line exactly.</summary>
    [Fact]
    public void A64SettlementWorld_OverflowsIntoContinuationRows_AndRoundTripsExactly()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-export-64-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            UiSession session = UiSession.Start(42, settlementsOverride: 64);
            string log = ExportFixtures.PlayToDisk(session, dir, "20261028-120000-n64", turns: 3);
            Assert.True(session.World.Settlements.Count >= 50, "the synthetic world has " + session.World.Settlements.Count + " settlements");
            (string path, SessionExportResult r) = SessionWorkbook.ExportBeside(UiSession.ManifestPath(log));
            Assert.True(r.ContinuationRows >= 2, "64 settlements must overflow a cell");
            Assert.True(r.LongestCell <= OoxmlWorkbook.MaxCellChars);

            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(File.ReadAllBytes(path));
            foreach (IReadOnlyList<string[]> sheet in back.Sheets)
                foreach (string[] row in sheet)
                    foreach (string c in row) Assert.True(c.Length <= OoxmlWorkbook.MaxCellChars);
            IReadOnlyList<string[]> tel = back.Sheets[Array.IndexOf(SessionWorkbook.SheetNames, "Telemetry")];
            int cont = 0;
            foreach (string[] row in tel)
                if (row.Length > 1 && row[0].Length == 0 && row[1].StartsWith('>')) cont++;
            Assert.Equal(r.ContinuationRows, cont);
            string[] lines = ExportFixtures.Lines(File.ReadAllBytes(UiSession.TelemetryPath(log)));
            IReadOnlyList<string> cells = SessionWorkbook.JoinTelemetry(tel);
            Assert.Equal(lines.Length, cells.Count);
            int over = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (cells[i].Length > OoxmlWorkbook.MaxCellChars) over++;
                Assert.Equal(lines[i], TelemetryCellCodec.Decode(cells[i]));
            }
            Assert.True(over >= 2, "turns 2 and 3 of this world are each over one cell (measured: 24,068 / 44,524 / 45,905 characters)");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ALoweredCellLimit_SplitsTheDirectorsSession_AndTheSelfCheckStillPasses()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("director-68");
        SessionExportResult r = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files), cellLimit: 2000);
        Assert.True(r.ContinuationRows > 68 * 3);
        Assert.True(r.LongestCell <= 2000);
        // the value sheets split too (a forensic value over the limit): continuation rows with an empty column A
        IReadOnlyList<string[]> forensic = OoxmlWorkbook.Read(r.Bytes).Sheets[Array.IndexOf(SessionWorkbook.SheetNames, "Forensic")];
        foreach (string[] row in forensic) Assert.True(row.Length == 0 || row[0].Length > 0 || row[1].StartsWith('>'));
    }

    // ----------------------------------------------------------------------------------- the OOXML writer --

    private static List<SheetModel> SmallSheets()
    {
        var a = new SheetModel("README", [10, 40]);
        a.Rows.Add(["key", "value"]);
        a.Rows.Add(["plain", "text"]);
        a.Rows.Add(["xml", "<a href=\"x\">&amp;</a> & ' \""]);
        a.Rows.Add(["controls", "tab\there, line\nfeed, cr\rlf\r\n, nul\u0000, bel\u0007, ￾￿"]);
        a.Rows.Add(["ooxml escape", "_x0041_ stays literal, _x00 is not one, __x1234__"]);
        a.Rows.Add(["spaces", "  leading and trailing  "]);
        a.Rows.Add(["unicode", "é中\U0001F600 and a lone \uD800 surrogate"]);
        a.Rows.Add(["", ">a continuation-looking cell"]);
        a.Rows.Add(["gap", "", "", "D only"]);
        var b = new SheetModel("Second", []);
        b.Rows.Add([]);
        b.Rows.Add(["after an empty row"]);
        return [a, b];
    }

    /// <summary>The OOXML writer's byte golden: a fixed two-sheet workbook (escapes, gaps, an empty row).</summary>
    internal const string SmallWorkbookSha = "e65373b07c7eee9160c39bcbdad022f7c13d306fd399715be01cb883118a82b5";

    [Fact]
    public void TheWriterIsByteIdenticalAcrossRuns_AndMatchesItsGolden()
    {
        byte[] one = OoxmlWorkbook.Write(SmallSheets());
        byte[] two = OoxmlWorkbook.Write(SmallSheets());
        Assert.Equal(one, two);
        string sha = ForensicIdentity.Sha256Hex(one);
        Assert.True(SmallWorkbookSha == sha, "the small workbook's sha256 is " + sha);
    }

    [Fact]
    public void TheWorkbookReadsBackExactly_ThroughZipArchiveAndXmlReader()
    {
        List<SheetModel> sheets = SmallSheets();
        OoxmlWorkbook.Content back = OoxmlWorkbook.Read(OoxmlWorkbook.Write(sheets));
        Assert.Equal(["README", "Second"], back.SheetNames);
        for (int s = 0; s < sheets.Count; s++)
        {
            Assert.Equal(sheets[s].Rows.Count, back.Sheets[s].Count);
            for (int r = 0; r < sheets[s].Rows.Count; r++)
            {
                string[] want = sheets[s].Rows[r];
                int n = want.Length;
                while (n > 0 && want[n - 1].Length == 0) n--;
                Assert.Equal(want[..n], back.Sheets[s][r]);
            }
        }
    }

    /// <summary>The ZIP container, walked by hand: every entry STORED, the fixed entry order, timestamp, version and
    /// attributes, no extra fields, a correct CRC, and a central directory that agrees with the local headers.</summary>
    [Fact]
    public void TheZipIsStoredWithFixedHeaders_AndEveryXmlPartIsWellFormed()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("director-68");
        byte[] z = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files)).Bytes;
        string[] order =
        [
            "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml",
            "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", "xl/worksheets/sheet3.xml", "xl/worksheets/sheet4.xml",
            "xl/worksheets/sheet5.xml", "xl/worksheets/sheet6.xml", "xl/worksheets/sheet7.xml", "xl/worksheets/sheet8.xml",
        ];
        int pos = 0;
        var local = new List<(string Name, int Offset, uint Crc, int Size)>();
        while (BinaryPrimitives.ReadUInt32LittleEndian(z.AsSpan(pos)) == 0x04034b50)
        {
            ReadOnlySpan<byte> h = z.AsSpan(pos, 30);
            Assert.Equal(20, BinaryPrimitives.ReadUInt16LittleEndian(h[4..]));
            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(h[6..]));        // no flags, no data descriptor
            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(h[8..]));        // STORED
            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(h[10..]));       // 00:00:00
            Assert.Equal(0x21, BinaryPrimitives.ReadUInt16LittleEndian(h[12..]));    // 1980-01-01
            uint crc = BinaryPrimitives.ReadUInt32LittleEndian(h[14..]);
            int csize = (int)BinaryPrimitives.ReadUInt32LittleEndian(h[18..]);
            int usize = (int)BinaryPrimitives.ReadUInt32LittleEndian(h[22..]);
            int nlen = BinaryPrimitives.ReadUInt16LittleEndian(h[26..]);
            Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(h[28..]));       // no extra field
            Assert.Equal(csize, usize);
            string name = Encoding.ASCII.GetString(z, pos + 30, nlen);
            Assert.Equal(crc, StoredZip.Crc32(z.AsSpan(pos + 30 + nlen, usize)));
            local.Add((name, pos, crc, usize));
            pos += 30 + nlen + usize;
        }
        Assert.Equal(order, local.ConvertAll(l => l.Name).ToArray());
        for (int i = 0; i < local.Count; i++)
        {
            ReadOnlySpan<byte> c = z.AsSpan(pos, 46);
            Assert.Equal(0x02014b50u, BinaryPrimitives.ReadUInt32LittleEndian(c));
            Assert.Equal(20, BinaryPrimitives.ReadUInt16LittleEndian(c[4..]));       // version made by: 2.0 / MS-DOS, not the host
            Assert.Equal(local[i].Crc, BinaryPrimitives.ReadUInt32LittleEndian(c[16..]));
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(c[38..]));      // external attributes
            Assert.Equal((uint)local[i].Offset, BinaryPrimitives.ReadUInt32LittleEndian(c[42..]));
            pos += 46 + BinaryPrimitives.ReadUInt16LittleEndian(c[28..]);
        }
        Assert.Equal(0x06054b50u, BinaryPrimitives.ReadUInt32LittleEndian(z.AsSpan(pos)));
        Assert.Equal(z.Length, pos + 22);

        // well-formed, required parts present, every sheet related and typed (Read throws otherwise)
        OoxmlWorkbook.Content back = OoxmlWorkbook.Read(z);
        Assert.Equal(SessionWorkbook.SheetNames, back.SheetNames);
        using var zip = new ZipArchive(new MemoryStream(z), ZipArchiveMode.Read);
        foreach (ZipArchiveEntry e in zip.Entries)
        {
            using var r = new StreamReader(e.Open());
            var xml = new System.Xml.XmlDocument { XmlResolver = null };
            xml.LoadXml(r.ReadToEnd());
        }
    }

    [Fact]
    public void ExcelsLimitsAreNamedRefusals_NeverTruncation()
    {
        var tall = new SheetModel("Tall", []);
        for (int i = 0; i <= OoxmlWorkbook.MaxRows; i++) tall.Rows.Add([]);
        Assert.Equal(SessionExportException.RowLimit, Assert.Throws<SessionExportException>(() => OoxmlWorkbook.Write([tall])).Code);
        var wide = new SheetModel("Wide", []);
        wide.Rows.Add(["a", "b", new string('x', OoxmlWorkbook.MaxCellChars + 1)]);
        Assert.Equal(SessionExportException.CellTooLarge, Assert.Throws<SessionExportException>(() => OoxmlWorkbook.Write([wide])).Code);
        var exact = new SheetModel("Exact", []);
        exact.Rows.Add(["a", new string('x', OoxmlWorkbook.MaxCellChars)]);
        Assert.Equal(OoxmlWorkbook.MaxCellChars, OoxmlWorkbook.Read(OoxmlWorkbook.Write([exact])).Sheets[0][0][1].Length);
    }

    // ---------------------------------------------------------------------------------- workbook goldens --

    [Fact]
    public void TheDirectors68TurnSession_ExportsByteIdentically_AndEveryCellDecodes()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("director-68");
        Assert.Equal(5, files.Count);   // the Director's set: no chronicle file was supplied
        SessionExportResult r = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files));
        SessionExportResult again = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files));
        Assert.Equal(r.Bytes, again.Bytes);
        Assert.True(DirectorWorkbookSha == r.Sha256, "the Director's workbook's sha256 is " + r.Sha256);
        Assert.Equal(["v4"], r.Codebooks);
        Assert.Equal(0, r.ContinuationRows);
        Assert.Equal(10096, r.LongestCell);   // README §3: the Director's session, max 10,096 coded characters

        OoxmlWorkbook.Content back = OoxmlWorkbook.Read(r.Bytes);
        Assert.Equal(SessionWorkbook.SheetNames, back.SheetNames);
        string[] lines = ExportFixtures.Lines(ExportFixtures.Bytes(files, "telemetry-"));
        IReadOnlyList<string> cells = SessionWorkbook.JoinTelemetry(back.Sheets[5]);
        string[] proto = ExportFixtures.PrototypeCells("director-68");
        Assert.Equal(lines.Length, cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            Assert.Equal(proto[i], cells[i]);
            Assert.Equal(lines[i], TelemetryCellCodec.Decode(cells[i]));
            Assert.Equal((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), back.Sheets[5][i + 1][0]);
        }

        // The chronicle is not in the set: the sheet says so — and what the close record says of it — and invents nothing.
        IReadOnlyList<string[]> chron = back.Sheets[4];
        Assert.Equal(2, chron.Count);
        Assert.Equal("FILE NOT PRESENT", chron[1][0]);
        Assert.Contains("3b0b4eba20940f64e7a26fddb0d360252c782079a54d05242f3fe63c143cc38a", chron[1][1], StringComparison.Ordinal);

        // Orders: the 27 records of the binary log, decoded
        IReadOnlyList<string[]> orders = back.Sheets[2];
        OrderLog log = OrderLog.Load(new MemoryStream(ExportFixtures.Bytes(files, "orders-")));
        Assert.Equal(log.Count + 1, orders.Count);
        for (int i = 0; i < log.Count; i++)
        {
            Assert.Equal(log[i].Turn.ToString(System.Globalization.CultureInfo.InvariantCulture), orders[i + 1][1]);
            Assert.Equal(((int)log[i].Kind).ToString(System.Globalization.CultureInfo.InvariantCulture), orders[i + 1][3]);
            Assert.Equal(BitConverter.DoubleToInt64Bits(log[i].Amount), BitConverter.DoubleToInt64Bits(CellNumber.Parse(orders[i + 1][7])));
        }

        // Trace as is; Forensic with every file's sha256 now and as the close record lists it
        string[] trace = ExportFixtures.Lines(ExportFixtures.Bytes(files, "trace-"));
        for (int i = 0; i < trace.Length; i++) Assert.Equal(trace[i], string.Join(",", back.Sheets[3][i]));
        IReadOnlyList<string[]> forensic = back.Sheets[7];
        foreach ((string Name, byte[] Bytes) f in files)
        {
            string[]? row = null;
            foreach (string[] x in forensic)
                if (x.Length > 1 && x[0].StartsWith("export.file.", StringComparison.Ordinal) && x[1] == f.Name) row = x;
            Assert.NotNull(row);
            Assert.Equal(ForensicIdentity.Sha256Hex(f.Bytes), row![4]);
            if (!f.Name.StartsWith("forensic-", StringComparison.Ordinal)) Assert.Equal("same", row[6]);
        }
    }

    [Fact]
    public void ACurrentTreeSession_v5_ExportsByteIdentically_AndEveryCellDecodes()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("current-v5");
        Assert.Equal(6, files.Count);
        SessionExportResult r = SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files));
        Assert.Equal(r.Bytes, SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files)).Bytes);
        Assert.True(CurrentWorkbookSha == r.Sha256, "the current-tree workbook's sha256 is " + r.Sha256);
        Assert.Equal(["v5"], r.Codebooks);
        OoxmlWorkbook.Content back = OoxmlWorkbook.Read(r.Bytes);
        string[] lines = ExportFixtures.Lines(ExportFixtures.Bytes(files, "telemetry-"));
        IReadOnlyList<string> cells = SessionWorkbook.JoinTelemetry(back.Sheets[5]);
        string[] proto = ExportFixtures.PrototypeCells("current-v5");
        for (int i = 0; i < cells.Count; i++)
        {
            Assert.Equal(proto[i], cells[i]);
            Assert.Equal(lines[i], TelemetryCellCodec.Decode(cells[i]));
        }
        string[] chronicle = ExportFixtures.Lines(ExportFixtures.Bytes(files, "chronicle-"));
        Assert.Equal(chronicle.Length + 1, back.Sheets[4].Count);
        for (int i = 0; i < chronicle.Length; i++) Assert.Equal(chronicle[i], back.Sheets[4][i + 1].Length > 1 ? back.Sheets[4][i + 1][1] : "");
        Assert.Equal("v5", back.Sheets[6][0][1]);   // Codebook sheet: codebook v5
    }

    // ------------------------------------------------------------------- the headless command and the hook --

    [Fact]
    public void TheHeadlessCommand_WritesTheSameWorkbook_AndRefusesByName()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-export-cmd-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
            List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("director-68");
            foreach ((string n, byte[] b) in files) File.WriteAllBytes(Path.Combine(dir, n), b);
            var before = new List<string>();
            foreach ((string n, byte[] _) in files) before.Add(ExportFixtures.Sha(Path.Combine(dir, n)));

            var o = new StringWriter();
            var e = new StringWriter();
            Assert.Equal(0, SessionExportCommand.Run(["--export-session", dir], o, e));
            string xlsx = Path.Combine(dir, "session-20261019-144516-a1.xlsx");
            Assert.Equal(DirectorWorkbookSha, ExportFixtures.Sha(xlsx));
            Assert.Contains("68 turn(s), codebook v4", o.ToString(), StringComparison.Ordinal);
            for (int i = 0; i < files.Count; i++) Assert.Equal(before[i], ExportFixtures.Sha(Path.Combine(dir, files[i].Name)));   // read, never written

            // a damaged telemetry line: refused by name, and no workbook is written
            File.Delete(xlsx);
            string tel = Path.Combine(dir, "telemetry-20261019-144516-a1.jsonl");
            string text = File.ReadAllText(tel);
            File.WriteAllText(tel, text.Replace("\"schema\":\"telemetry/v4\",\"turn\":{\"turn\":7,", "\"schema\":\"telemetry/v4\",\"turn\":{\"turn\":7,\"x\":1,", StringComparison.Ordinal));
            e = new StringWriter();
            Assert.Equal(2, SessionExportCommand.Run(["--export-session", dir], new StringWriter(), e));
            Assert.Contains(SessionExportException.TelemetryShape, e.ToString(), StringComparison.Ordinal);
            Assert.Contains("line 7", e.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(xlsx));

            Assert.Equal(2, SessionExportCommand.Run(["--export-session", Path.Combine(dir, "nowhere")], new StringWriter(), new StringWriter()));
            Assert.Equal(2, SessionExportCommand.Run(["--export-session"], new StringWriter(), new StringWriter()));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The session-close hook, through the real GameUi (the headless harness): SaveOnExit writes the six files,
    /// the forensic close record, and THEN the workbook. The workbook's Forensic sheet holds the close record (so the
    /// export ran after it); the close record does not list the workbook; and every file the close record hashed still
    /// has that hash after the export.</summary>
    [Fact]
    public void AtSessionClose_TheWorkbookIsWrittenAfterTheForensicClose_AndTheSixFilesKeepTheirHashes()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-export-close-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
            using Sim.Ui.Headless.UiFrameHarness h = Sim.Ui.Headless.UiFrameHarness.Start(session, dir, Path.Combine(AppContext.BaseDirectory, "assets"));
            session.ExportManifest("2026-10-28 12:00:00", h.SessionLogPath);
            session.ExportForensicRun("2026-10-28 12:00:00+00:00", h.SessionLogPath);
            for (int t = 0; t < 3; t++) session.EndTurn();
            string xlsx = SessionWorkbook.WorkbookPathFor(UiSession.ManifestPath(h.SessionLogPath));
            Assert.False(File.Exists(xlsx));

            h.Ui.SaveOnExit();

            Assert.True(File.Exists(xlsx));
            ForensicRecordFile fr = ForensicRecordFile.Read(UiSession.ForensicPath(h.SessionLogPath));
            Assert.NotNull(fr.Close);
            Assert.Equal(5, fr.Close!.Artifacts.Length);
            foreach (ArtifactRef a in fr.Close.Artifacts)
            {
                Assert.False(a.File.EndsWith(".xlsx", StringComparison.Ordinal));
                Assert.Equal(a.Sha256, ExportFixtures.Sha(Path.Combine(dir, a.File)));   // unchanged by the export
            }
            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(File.ReadAllBytes(xlsx));
            bool closeInWorkbook = false;
            foreach (string[] row in back.Sheets[7])
                if (row.Length > 1 && row[0] == "close.turnsReached" && row[1] == "3") closeInWorkbook = true;
            Assert.True(closeInWorkbook, "the workbook's Forensic sheet holds the close record: the export ran after the close");
            string[] lines = ExportFixtures.Lines(File.ReadAllBytes(UiSession.TelemetryPath(h.SessionLogPath)));
            Assert.Equal(3, lines.Length);
            IReadOnlyList<string> cells = SessionWorkbook.JoinTelemetry(back.Sheets[5]);
            for (int i = 0; i < lines.Length; i++) Assert.Equal(lines[i], TelemetryCellCodec.Decode(cells[i]));
            Assert.StartsWith("v5 ", cells[0], StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>NOT A CHECK — the generator of Fixtures/session-export/current-v5 (README there). Runs only with
    /// CIVSIM_WRITE_EXPORT_FIXTURE=DIR; without it the method returns at once and asserts nothing.</summary>
    [Fact]
    public void Tool_WritesTheCurrentTreeFixture_OnlyWhenAsked()
    {
        string? dir = Environment.GetEnvironmentVariable("CIVSIM_WRITE_EXPORT_FIXTURE");
        if (dir is null) return;
        UiSession session = UiSession.Start(42, aiEmpiresOverride: 1);
        ExportFixtures.PlayToDisk(session, dir, "20261028-120000-a1", turns: 16, ExportFixtures.FixtureOrders);
    }
}
