using System.Globalization;
using System.Text.RegularExpressions;
using Sim.Core.Chronicle;
using Sim.Core.Kernel;
using Sim.Ui.Export;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-059 amendment 2026-10-29 (Director: "the workbook should have 0 real words in anything that is not a key or a
/// legend") — THE CODED SHEETS. The Director's acceptance criterion as a strict guard over every non-legend sheet; every
/// code a sheet uses defined in the Codebook legend for that sheet; the coded chronicle record built at the source and
/// paired one for one with the prose record (P7's filter unchanged); the in-game annals still prose; and the coded
/// Orders rows keeping every amount's exact bits.
/// </summary>
public class SessionExportCodesTests
{
    /// <summary>The two legend sheets: the only sheets allowed words.</summary>
    private static readonly string[] LegendSheets = ["README", "Codebook"];

    private static readonly Regex Word = new("[A-Za-z]{3,}", RegexOptions.CultureInvariant);
    private static readonly Regex Letters = new("[A-Za-z]+", RegexOptions.CultureInvariant);
    // Identifiers kept verbatim (defined independently of the exporter): hashes / hex, file names, version tags.
    private static readonly Regex Hex = new("^(?:[0-9a-f]{7,}|[0-9A-F]{7,})$", RegexOptions.CultureInvariant);
    private static readonly Regex FileName = new(@"^[a-z0-9]+(?:[.-][a-z0-9]+)*\.(?:json|jsonl|bin|csv|txt|xlsx)$", RegexOptions.CultureInvariant);
    private static readonly Regex VersionTag = new("^[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9]+(?:-[a-z0-9]+)*)*/v[0-9]+$", RegexOptions.CultureInvariant);
    // A JSON key path (Manifest / Forensic column KY): keys joined by '.', [i] items, #n for a second record of a kind.
    private static readonly Regex KeyPath = new(@"^(?:\$|[A-Za-z_][A-Za-z0-9_]*(?:#\d+)?(?:\[\d+\])*(?:\.[A-Za-z_][A-Za-z0-9_]*(?:\[\d+\])*)*)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex Number = new(@"^(?:-?(?:\d+\.?\d*|\.\d+)(?:[Ee][+-]?\d+)?|N|I|-I)$", RegexOptions.CultureInvariant);
    private static readonly string[] TypeCodes = ["S", "N", "B", "O", "A", "Z", "X"];

    private static bool IsIdentifier(string c) => Hex.IsMatch(c) || FileName.IsMatch(c) || VersionTag.IsMatch(c);

    /// <summary>A KEY cell: column A of a key / value / type row of Manifest or Forensic (its type a type code).</summary>
    private static bool IsKey(string sheet, IReadOnlyList<string[]> rows, int r, int c) =>
        (sheet == "Manifest" || sheet == "Forensic") && r > 0 && c == 0 && rows[r].Length >= 3
        && Array.IndexOf(TypeCodes, rows[r][2]) >= 0 && KeyPath.IsMatch(rows[r][0]);

    /// <summary>THE DIRECTOR'S CRITERION: every cell of a non-legend sheet holding a word (three or more ASCII letters in
    /// a row) that is neither an identifier nor a key — as "sheet!row,col: text".</summary>
    internal static List<string> Words(OoxmlWorkbook.Content back)
    {
        var bad = new List<string>();
        for (int s = 0; s < back.SheetNames.Count; s++)
        {
            string sheet = back.SheetNames[s];
            if (Array.IndexOf(LegendSheets, sheet) >= 0) continue;
            IReadOnlyList<string[]> rows = back.Sheets[s];
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    string cell = rows[r][c];
                    if (!Word.IsMatch(cell) || IsIdentifier(cell) || IsKey(sheet, rows, r, c)) continue;
                    bad.Add(sheet + "!" + (r + 1).ToString(CultureInfo.InvariantCulture) + "," + (c + 1).ToString(CultureInfo.InvariantCulture)
                        + ": " + (cell.Length > 80 ? cell[..80] : cell));
                }
        }
        return bad;
    }

    /// <summary>The Codebook legend: (section, code, meaning) of every row after the 'legend' header row.</summary>
    internal static List<(string Section, string Code, string Meaning)> Legend(IReadOnlyList<string[]> codebook)
    {
        var o = new List<(string, string, string)>();
        bool on = false;
        foreach (string[] row in codebook)
        {
            if (row.Length > 0 && row[0] == "legend" && row.Length > 1 && row[1] == "code") { on = true; continue; }
            if (!on || row.Length < 2 || row[0].Length == 0) continue;
            o.Add((row[0], row[1], row.Length > 3 ? row[3] : ""));
        }
        return o;
    }

    internal static string[] LegendRow(IReadOnlyList<string[]> codebook, string section, string code)
    {
        bool on = false;
        foreach (string[] row in codebook)
        {
            if (row.Length > 1 && row[0] == "legend" && row[1] == "code") on = true;
            else if (on && row.Length > 3 && row[0] == section && row[1] == code) return row;
        }
        throw new InvalidOperationException("no legend row " + section + " " + code);
    }

    /// <summary>EVERY CODE DEFINED: each letter run of every non-legend cell (identifiers, keys, numbers and the telemetry
    /// cells — read with their own codebook and decoded byte for byte elsewhere — aside) must be a code the legend
    /// defines in a section for that sheet (<c>Sheet:part</c>) or for every sheet (all, type, QT:texts; and the
    /// Chronicle sheet's Chronicle:names). Returns the undefined uses.</summary>
    internal static List<string> UndefinedCodes(OoxmlWorkbook.Content back)
    {
        List<(string Section, string Code, string Meaning)> legend = Legend(back.Sheets[Array.IndexOf(SessionWorkbook.SheetNames, "Codebook")]);
        var bad = new List<string>();
        for (int s = 0; s < back.SheetNames.Count; s++)
        {
            string sheet = back.SheetNames[s];
            if (Array.IndexOf(LegendSheets, sheet) >= 0) continue;
            var defined = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string section, string code, string _) in legend)
            {
                bool applies = section.StartsWith(sheet + ":", StringComparison.Ordinal) || section is "all" or "type" or "QT:texts"
                    || (sheet == "Forensic" && section == "Manifest:col");   // Forensic's KY / VL / TY are Manifest's
                if (!applies) continue;
                Match m = Letters.Match(code);
                if (m.Success && m.Index == 0) defined.Add(m.Value);
            }
            IReadOnlyList<string[]> rows = back.Sheets[s];
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    string cell = rows[r][c];
                    if (cell.Length == 0 || IsIdentifier(cell) || IsKey(sheet, rows, r, c) || Number.IsMatch(cell)) continue;
                    if (sheet == "Telemetry" && r > 0 && c == 1) continue;
                    foreach (Match m in Letters.Matches(cell))
                        if (!defined.Contains(m.Value))
                            bad.Add(sheet + "!" + (r + 1).ToString(CultureInfo.InvariantCulture) + "," + (c + 1).ToString(CultureInfo.InvariantCulture)
                                + ": " + m.Value + " in " + cell);
                }
        }
        return bad;
    }

    private static OoxmlWorkbook.Content Fixture(string name)
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files(name);
        return OoxmlWorkbook.Read(SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files)).Bytes);
    }

    // ------------------------------------------------------------------------------------------- the guards --

    [Theory]
    [InlineData("director-68")]
    [InlineData("current-v5")]
    public void NoWordOutsideTheLegendSheets_AndEveryCodeIsDefined_OnTheFixtures(string fixture)
    {
        OoxmlWorkbook.Content back = Fixture(fixture);
        Assert.Equal(SessionWorkbook.SheetNames, back.SheetNames);
        List<string> words = Words(back);
        Assert.True(words.Count == 0, "words outside README / Codebook:\n" + string.Join("\n", words));
        List<string> undefined = UndefinedCodes(back);
        Assert.True(undefined.Count == 0, "codes the Codebook does not define:\n" + string.Join("\n", undefined));
    }

    /// <summary>The guard has teeth: a prose word in a data cell, an undefined code, and a word in a column header are each
    /// caught; an identifier, a JSON key and a number are not.</summary>
    [Fact]
    public void TheGuardsCatchAWordAndAnUndefinedCode_AndPassIdentifiersKeysAndNumbers()
    {
        OoxmlWorkbook.Content back = Fixture("current-v5");
        var sheets = new List<IReadOnlyList<string[]>>(back.Sheets);
        int orders = Array.IndexOf(SessionWorkbook.SheetNames, "Orders");
        var o = new List<string[]>(back.Sheets[orders]);
        o[1] = (string[])o[1].Clone();
        o[1][6] = "settlement #0";
        sheets[orders] = o;
        var withWord = new OoxmlWorkbook.Content(back.SheetNames, sheets, back.EntryNames);
        Assert.Contains(Words(withWord), w => w.StartsWith("Orders!2,7: settlement #0", StringComparison.Ordinal));

        o[1] = (string[])o[1].Clone();
        o[1][6] = "ZQ0";   // two letters, no word — but not a code the legend defines
        Assert.Empty(Words(withWord));
        Assert.Contains(UndefinedCodes(withWord), w => w.StartsWith("Orders!2,7: ZQ in ZQ0", StringComparison.Ordinal));

        o[0] = (string[])o[0].Clone();
        o[0][0] = "index";
        Assert.Contains(Words(withWord), w => w.StartsWith("Orders!1,1: index", StringComparison.Ordinal));

        Assert.True(IsIdentifier("session-20261028-120000-a1.json") && IsIdentifier("telemetry/v5") && IsIdentifier("3FF0000000000000"));
        Assert.True(IsIdentifier("55e9b1cba3f7a6f3") && IsIdentifier("pipeline.toy.json"));
        Assert.False(IsIdentifier("dev") || IsIdentifier("Early Modern") || IsIdentifier("ubuntu.24.04-x64") || IsIdentifier("deadBEEF00"));
    }

    // ---------------------------------------------------------------------------- the coded chronicle record --

    /// <summary>
    /// A session written to disk as the game writes it: its chronicle FILE is the coded record — the format tag, the name
    /// lines (ordinal by code), one coded line per prose record line — and its workbook's Chronicle sheet shows exactly
    /// those event lines (numbered), the names going to the legend's Chronicle:names rows; no word anywhere outside the
    /// legend sheets, every code defined.
    /// </summary>
    [Fact]
    public void ANewSession_WritesTheCodedChronicle_AndItsWorkbookShowsItCoded()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-export-codes-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
            string log = ExportFixtures.PlayToDisk(session, dir, "20261029-120000-c1", turns: 8);
            string[] file = ExportFixtures.Lines(File.ReadAllBytes(UiSession.ChroniclePath(log)));
            Assert.Equal(ChronicleCode.FormatTag, file[0]);
            IReadOnlyList<(string Code, string Name)> names = session.RecordNames;
            IReadOnlyList<string> codes = session.RecordCodeLines;
            Assert.Equal(1 + names.Count + codes.Count, file.Length);
            Assert.Equal(session.RecordAnnalLines.Count, codes.Count);
            for (int i = 0; i < names.Count; i++)
            {
                Assert.Equal(names[i].Code + "=" + names[i].Name, file[1 + i]);
                if (i > 0) Assert.True(string.CompareOrdinal(names[i - 1].Code, names[i].Code) < 0, "name lines in ordinal order");
            }
            for (int i = 0; i < codes.Count; i++) Assert.Equal(codes[i], file[1 + names.Count + i]);
            // every settlement of the 4-settlement world founded: FN for each, named by id, its name in the name table
            int founded = 0;
            foreach (string c in codes)
                if (c.StartsWith("Y0 FN ST", StringComparison.Ordinal)) founded++;
            Assert.Equal(4, founded);

            (string _, SessionExportResult r) = SessionWorkbook.ExportBeside(UiSession.ManifestPath(log));
            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(r.Bytes);
            IReadOnlyList<string[]> chron = back.Sheets[Array.IndexOf(SessionWorkbook.SheetNames, "Chronicle")];
            Assert.Equal(["L", "EV"], chron[0]);
            Assert.Equal(codes.Count + 1, chron.Count);
            for (int i = 0; i < codes.Count; i++)
                Assert.Equal([(i + 1).ToString(CultureInfo.InvariantCulture), codes[i]], chron[i + 1]);
            IReadOnlyList<string[]> codebook = back.Sheets[Array.IndexOf(SessionWorkbook.SheetNames, "Codebook")];
            foreach ((string code, string name) in names) Assert.Equal(name, LegendRow(codebook, "Chronicle:names", code)[3]);
            Assert.Contains("This file is coded (" + ChronicleCode.FormatTag + ")", ReadmeRow(back, "sheet Chronicle"), StringComparison.Ordinal);
            List<string> words = Words(back);
            Assert.True(words.Count == 0, "words outside README / Codebook:\n" + string.Join("\n", words));
            List<string> undefined = UndefinedCodes(back);
            Assert.True(undefined.Count == 0, "codes the Codebook does not define:\n" + string.Join("\n", undefined));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    private static string ReadmeRow(OoxmlWorkbook.Content back, string key)
    {
        foreach (string[] row in back.Sheets[0])
            if (row.Length > 1 && row[0] == key) return row[1];
        throw new InvalidOperationException("no README row " + key);
    }

    /// <summary>
    /// THE CODED RECORD IS THE PROSE RECORD, ONE FOR ONE — and the in-game annals are unchanged. The current-tree fixture's
    /// session (seed 42, canonical world, one AI empire, fog: the player does not know every settlement) replayed through
    /// the game's own session: the PROSE record (RecordAnnalLines) is byte for byte the fixture's frozen chronicle file,
    /// written by the M6 closure tree before this amendment, so the prose did not move; the in-game ANNALS panel is that
    /// record with D-060's books filter only (every panel line is a record line or a founding told without its figure);
    /// the CODED record has one line per prose line, in order, with the same year, and names by id exactly the
    /// settlements the prose line names — never a settlement P7 hides.
    /// </summary>
    [Fact]
    public void TheCodedRecordPairsTheProseRecord_AndTheInGameAnnalsAreUnchanged()
    {
        UiSession session = UiSession.Start(42, aiEmpiresOverride: 1);
        for (int t = 0; t < 16; t++)
        {
            ExportFixtures.FixtureOrders(session, t);
            session.EndTurn();
        }
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("current-v5");
        string[] frozen = ExportFixtures.Lines(ExportFixtures.Bytes(files, "chronicle-"));
        Assert.Equal(frozen, session.RecordAnnalLines);

        int record = 0;
        foreach (string panel in session.AnnalLines)
        {
            while (record < session.RecordAnnalLines.Count && session.RecordAnnalLines[record] != panel
                   && !(panel.EndsWith(" was founded.", StringComparison.Ordinal)
                        && session.RecordAnnalLines[record].StartsWith(panel[..^1], StringComparison.Ordinal)))
                record++;
            Assert.True(record < session.RecordAnnalLines.Count, "panel line not in the record: " + panel);
            record++;
        }

        IReadOnlyList<string> codes = session.RecordCodeLines;
        Assert.Equal(session.RecordAnnalLines.Count, codes.Count);
        bool anyHidden = false;
        for (int s = 0; s < session.World.Settlements.Count; s++)
            anyHidden |= session.Knowledge.IsHiddenSettlement(session.World.Settlements[s].Id.Value);
        Assert.True(anyHidden, "the fixture session has settlements the player does not know (P7 is exercised)");
        for (int i = 0; i < codes.Count; i++)
        {
            string prose = session.RecordAnnalLines[i];
            string[] f = codes[i].Split(' ');
            Assert.Equal("In " + (prose.StartsWith("In the year ", StringComparison.Ordinal) ? "the year " : "") + f[0][1..] + ",",
                prose[..(prose.IndexOf(',') + 1)]);
            foreach (string field in f)
                if (field.StartsWith("ST", StringComparison.Ordinal))
                {
                    int id = int.Parse(field[2..], CultureInfo.InvariantCulture);
                    Assert.Contains(session.Names.Name(id), prose, StringComparison.Ordinal);
                    Assert.False(session.Knowledge.IsHiddenSettlement(id), "a hidden settlement is never in the record");
                }
        }
    }

    /// <summary>
    /// THE UI-SIDE EVENTS CODED AT THE SOURCE, field for field: the state chronicle (the events the core collector does
    /// not detect), given the session record, appends one coded line per prose line, in the same order — a structure
    /// completed (SC), an institution founded (IF) and matured (IM), a tax levied (TL), a revolt (RV), a settlement
    /// changing hands (CP), an Age entered (AE), a class rising and subsiding (SR / SS) — each content thing by its key
    /// and named once in the record's name table; without the record, the prose is the same lines.
    /// </summary>
    [Fact]
    public void TheStateChroniclesEvents_AreCodedFieldForField_OneLinePerProseLine()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        s.EndTurn();
        Sim.Core.State.WorldState prev = s.World;
        Sim.Core.State.WorldState next = prev.Clone();
        string y = "Y" + ((long)Math.Round(next.Clock.WorldDateYears)).ToString(CultureInfo.InvariantCulture) + " ";
        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        Sim.Core.State.SettlementId site = next.Settlements[0].Id, lost = next.Settlements[1].Id, moved = next.Settlements[2].Id;
        Sim.Core.Systems.ConstructionProjectEntry project = s.Config.Goods!.Projects![0];
        next.Structures.Add(new Sim.Core.State.StructureRow(site, project.Id, Sim.Core.State.ConstructionQuery.Built(prev, site, project.Id) + 1));
        int type = s.Config.Research!.UniversityTypes[0].Key;
        next.Institutions.Add(new Sim.Core.State.InstitutionRow(77, Sim.Ui.ViewModel.UiPlayer.Empire, site, type, next.Clock.Turn, 0.1));
        next.TaxPolicies.Add(new Sim.Core.State.TaxPolicyRow(Sim.Ui.ViewModel.UiPlayer.Empire, 0.1));
        var keep = new List<Sim.Core.State.ControlRow>();
        for (int i = 0; i < next.Controls.Count; i++)
            if (next.Controls[i].Place == moved) keep.Add(next.Controls[i] with { Polity = new Sim.Core.State.PolityId(99) });
            else if (next.Controls[i].Place != lost) keep.Add(next.Controls[i]);
        next.Controls.Clear();
        foreach (Sim.Core.State.ControlRow c in keep) next.Controls.Add(c);
        Sim.Core.Systems.Ages.AgeContent ages = s.Config.Ages!;
        int a0 = Sim.Core.State.AgeQuery.CurrentAge(prev, ages, Sim.Ui.ViewModel.UiPlayer.Empire);
        int at = -1;
        for (int i = 0; i < next.AgeStates.Count; i++) if (next.AgeStates[i].Polity == Sim.Ui.ViewModel.UiPlayer.Empire) at = i;
        if (at >= 0) next.AgeStates[at] = next.AgeStates[at] with { Age = a0 + 1 };
        else next.AgeStates.Add(new Sim.Core.State.AgeStateRow(Sim.Ui.ViewModel.UiPlayer.Empire, a0 + 1, next.Clock.Turn, 0, 0));

        var prose = new List<string>();
        var record = new ChronicleCode.Record();
        new Sim.Ui.ViewModel.StateChronicle().Observe(prev, next, s.Config, s.Names.Name, prose, coded: record);
        var plain = new List<string>();
        new Sim.Ui.ViewModel.StateChronicle().Observe(prev, next, s.Config, s.Names.Name, plain);
        Assert.Equal(plain, prose);   // the record changes no prose line
        string institution = Sim.Core.State.InstitutionContent.Info(s.Config, type)!.Name;
        string[] expected =
        [
            y + "AE PL1 AG" + I(a0 + 1),
            y + "TL PL1 TB0 TN10",
            y + "SC ST" + I(site.Value) + " PJ" + I(project.Id),
            y + "IF IN" + I(type) + " ST" + I(site.Value),
            y + "RV ST" + I(lost.Value) + " PF1",
            y + "CP ST" + I(moved.Value) + " PF1 PT99",
        ];
        Assert.Equal(expected, record.Lines);
        Assert.Equal(prose.Count, record.Lines.Count);
        string[] fragments = [" entered the ", " levied a tax of 10%.", " completed a ", " was founded at ", " rose up and threw off ", " passed from "];
        for (int i = 0; i < prose.Count; i++) Assert.Contains(fragments[i], prose[i], StringComparison.Ordinal);
        Assert.Equal(
            new (string, string)[]
            {
                ("AG" + I(a0 + 1), Sim.Ui.ViewModel.StateChronicle.AgeName(ages, a0 + 1)), ("IN" + I(type), institution),
                ("PJ" + I(project.Id), project.Name), ("ST" + I(site.Value), s.Names.Name(site.Value)),
                ("ST" + I(lost.Value), s.Names.Name(lost.Value)), ("ST" + I(moved.Value), s.Names.Name(moved.Value)),
            }.OrderBy(n => n.Item1, StringComparer.Ordinal).ToArray(),
            record.Names.ToArray());

        // maturity, and a class rising against the levy and subsiding: one coded line each
        Sim.Core.State.WorldState grown = next.Clone();
        for (int i = 0; i < grown.Institutions.Count; i++)
            if (grown.Institutions[i].Id == 77) grown.Institutions[i] = grown.Institutions[i] with { Maturity = 0.999 };
        var r2 = new ChronicleCode.Record();
        new Sim.Ui.ViewModel.StateChronicle().Observe(next, grown, s.Config, s.Names.Name, new List<string>(), coded: r2);
        Assert.Equal([y + "IM IN" + I(type) + " ST" + I(site.Value)], r2.Lines);

        Sim.Core.State.ClassId cls = default;
        for (int i = 0; i < s.World.Buckets.Count; i++)
            if (s.World.Buckets[i].Settlement == site && s.World.Buckets[i].Count.Value > 0) { cls = s.World.Buckets[i].Class; break; }
        double tip = s.Config.Needs!.Unrest!.UprisingGrievance;
        Sim.Core.State.WorldState calm = s.World.Clone(), risen = s.World.Clone();
        calm.TaxGrievances.Add(new Sim.Core.State.TaxGrievanceRow(site, cls, tip * 0.9));
        risen.TaxGrievances.Add(new Sim.Core.State.TaxGrievanceRow(site, cls, tip));
        var r3 = new ChronicleCode.Record();
        var p3 = new List<string>();
        new Sim.Ui.ViewModel.StateChronicle().Observe(calm, risen, s.Config, s.Names.Name, p3, coded: r3);
        new Sim.Ui.ViewModel.StateChronicle().Observe(risen, calm, s.Config, s.Names.Name, p3, coded: r3);
        string yc = "Y" + ((long)Math.Round(calm.Clock.WorldDateYears)).ToString(CultureInfo.InvariantCulture) + " ";
        Assert.Equal([yc + "SR CL" + I(cls.Value) + " ST" + I(site.Value), yc + "SS CL" + I(cls.Value) + " ST" + I(site.Value)], r3.Lines);
        Assert.Equal(2, p3.Count);
        Assert.Contains(("CL" + I(cls.Value), Array.Find(s.Config.Registries.Classes, e => e.Id == cls.Value)!.Name), r3.Names);
    }

    // ------------------------------------------------------------------------------------- the coded orders --

    /// <summary>
    /// THE CODED ORDERS ROWS KEEP EVERY AMOUNT EXACTLY: on both fixtures each row's AB is the amount's 64-bit pattern, AM
    /// parses back to the same bits, TG is the target id and KC the kind's code; and every kind's coded reading is pinned,
    /// edge amounts included (−0, NaN, a packed queue entry, an unpackable one).
    /// </summary>
    [Fact]
    public void TheCodedOrdersRows_KeepTheExactAmountBits_AndEveryKindReadsAsItsCode()
    {
        foreach (string fixture in new[] { "director-68", "current-v5" })
        {
            List<(string Name, byte[] Bytes)> files = ExportFixtures.Files(fixture);
            OrderLog log = OrderLog.Load(new MemoryStream(ExportFixtures.Bytes(files, "orders-")));
            IReadOnlyList<string[]> rows = Fixture(fixture).Sheets[Array.IndexOf(SessionWorkbook.SheetNames, "Orders")];
            Assert.Equal(["I", "T", "PL", "K", "KC", "TG", "TD", "AM", "AD", "AB"], rows[0]);
            Assert.Equal(log.Count + 1, rows.Count);
            for (int i = 0; i < log.Count; i++)
            {
                string[] row = rows[i + 1];
                long bits = BitConverter.DoubleToInt64Bits(log[i].Amount);
                Assert.Equal(bits.ToString("X16", CultureInfo.InvariantCulture), row[9]);
                Assert.Equal(bits, long.Parse(row[9], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                Assert.Equal(bits, BitConverter.DoubleToInt64Bits(CellNumber.Parse(row[7])));
                Assert.Equal(log[i].TargetId.ToString(CultureInfo.InvariantCulture), row[5]);
                Assert.Equal(WorkbookCodes.KindCode((int)log[i].Kind), row[4]);
                (string k, string td, string ad) = SessionWorkbook.Code(log[i]);
                Assert.Equal(new[] { k, td, ad }, new[] { row[4], row[6], row[8] });
            }
        }

        static OrderRecord R(OrderKind k, int target, double amount) => new(3, 1, k, target, amount);
        (OrderRecord Order, string Kind, string Target, string Amount)[] cases =
        [
            (R(OrderKind.SetRainBias, 4, -12.5), "RB", "RG4", "MM-12.5"),
            (R(OrderKind.LaborAllocation, 7, 60), "LA", "ST7", "PC60"),
            (R(OrderKind.SectorAllocation, 12 * 8 + 3, 12.25), "SA", "ST12/S3", "PC12.25"),
            (R(OrderKind.EnqueueConstruction, 5, 2), "EC", "ST5", "PJ2"),
            (R(OrderKind.SetTaxRate, 1, 15), "TX", "PL1", "PC15"),
            (R(OrderKind.SetResearchTarget, 1069, 0), "RT", "RN1069", "ZR0"),
            (R(OrderKind.SetResearchTarget, -1, 0), "RT", "LT-1", "ZR0"),
            (R(OrderKind.SetResearchTarget, -2, 0), "RT", "LC-2", "ZR0"),
            (R(OrderKind.AdvanceAge, 3, 7), "AA", "AG3", "SK7"),
            (R(OrderKind.DevelopRoads, 0, 40), "DR", "ZR0", "PC40"),
            (R(OrderKind.FoundSettlement, 9, 462102), "FS", "ST9", "CI462102"),
            (R(OrderKind.CancelConstruction, 2, 1), "CC", "ST2", "QS1"),
            (R(OrderKind.SetResourceAllocation, 2, 0.5), "RA", "PL2", "PC.5"),
            (R(OrderKind.CancelConstructionEntry, 2, 1.5), "CE", "ST2", "NV1.5"),
            (R(OrderKind.Explore, 6, 0), "EX", "ST6", "ZR0"),
            (R(OrderKind.Prospect, 5, 0), "PR", "ST5", "SV"),
            (R(OrderKind.Prospect, 5, 1), "PR", "ST5", "AS"),
            (R(OrderKind.Prospect, 5, -0.0), "PR", "ST5", "MT-0"),
            (R(OrderKind.Prospect, 5, 2), "PR", "ST5", "MT2"),
            (R(OrderKind.RecruitFormation, 8, 301), "RF", "ST8", "ID301"),
            (R(OrderKind.ReequipFormation, 40, 0), "RE", "FM40", "CN"),
            (R(OrderKind.ReequipFormation, 40, 302), "RE", "FM40", "ID302"),
            (R(OrderKind.DisbandFormation, 40, 0), "DF", "FM40", "ZR0"),
            (R(OrderKind.SetTaxRate, 1, double.NaN), "TX", "PL1", ""),
            (R((OrderKind)10, 1, 1), "MF", "", ""),
            (R((OrderKind)99, 1, 1), "UK", "", ""),
        ];
        foreach ((OrderRecord o, string kind, string target, string amount) in cases)
            Assert.Equal((kind, target, amount), SessionWorkbook.Code(o));
        double packed = OrderLog.PackCancelTarget(new CancelTarget(2, 1, 7));
        Assert.Equal(("CE", "ST2", "QS2/PJ1/T7"), SessionWorkbook.Code(R(OrderKind.CancelConstructionEntry, 2, packed)));
    }

    /// <summary>Every order kind the build knows has its own code, distinct, and each is in the legend (Orders:kind) with
    /// its kind number; the legend lists no code twice within a section.</summary>
    [Fact]
    public void EveryOrderKindHasADistinctLegendCode_AndNoSectionDefinesACodeTwice()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (OrderKind k in Enum.GetValues<OrderKind>())
        {
            string code = WorkbookCodes.KindCode((int)k);
            Assert.True(seen.Add(code), "kind code " + code + " twice");
            Assert.Contains(WorkbookCodes.Legend, e => e.Section == "Orders:kind" && e.Code == code
                && e.Meaning.StartsWith(((int)k).ToString(CultureInfo.InvariantCulture) + " " + k.ToString(), StringComparison.Ordinal));
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkbookCodes.Entry e in WorkbookCodes.Legend) Assert.True(keys.Add(e.Section + "\t" + e.Code), "twice: " + e.Section + " " + e.Code);
    }
}
