using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.Observability.Forensic;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Export;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// D-086 DE1 (the Director, 2026-11-11: "it should record which node was researched and how much and which turn also") —
/// THE RESEARCH SHEET of the session workbook. A session written to disk as the game writes it (seed 42, one AI empire, the
/// player researching): every telemetry research row is a coded Research row with its exact numbers; the player's completion
/// rows fall on the turns of the chronicle's own "research completed" events (an independent writer); every node code is
/// named in the legend; the sheet is coded (no word, every code defined); and sessions written before telemetry/v6 export
/// with the sheet saying NOT RECORDED, never inventing a row.
/// </summary>
public class RunRecordResearchTests
{
    private static int Sheet(string name) => Array.IndexOf(SessionWorkbook.SheetNames, name);

    /// <summary>The player's research orders: in each lane without a target, the cheapest available node of that tree
    /// (composite key (effective cost, node key): a stable integer tie-break) — through the UI's own emitter.</summary>
    internal static void PlayerResearch(UiSession s, int turn)
    {
        if (s.Config.Research is not { } content) return;
        bool[] available = ResearchQuery.AvailableMask(s.World, content, UiPlayer.Empire);
        foreach (ResearchTree tree in new[] { ResearchTree.Technology, ResearchTree.Civics })
        {
            if (ResearchQuery.TryGetTarget(s.World, content, UiPlayer.Empire, tree, out _)) continue;
            int best = -1;
            double bestCost = 0.0;
            for (int i = 0; i < available.Length; i++)
            {
                if (!available[i] || content.Nodes[i].Tree != tree) continue;
                double cost = ResearchQuery.EffectiveCost(s.World, content, UiPlayer.Empire, i);
                if (best < 0 || cost < bestCost || (cost == bestCost && content.Nodes[i].Key.Value < content.Nodes[best].Key.Value))
                {
                    best = i;
                    bestCost = cost;
                }
            }
            if (best >= 0) s.EmitResearchOrder(content.Nodes[best].Key);
        }
    }

    private static string TempDir(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), name + "-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return dir;
    }

    /// <summary>The Civics Points text of the sheet back to integer milli-CP (exact: at most three decimals).</summary>
    private static long Milli(string text)
    {
        bool negative = text.StartsWith('-');
        string t = negative ? text[1..] : text;
        int dot = t.IndexOf('.');
        string whole = dot < 0 ? t : t[..dot], frac = dot < 0 ? "" : t[(dot + 1)..];
        Assert.True(frac.Length <= 3, "at most three decimals: " + text);
        long v = (whole.Length == 0 ? 0L : long.Parse(whole, CultureInfo.InvariantCulture)) * 1000L
            + (frac.Length == 0 ? 0L : long.Parse(frac.PadRight(3, '0'), CultureInfo.InvariantCulture));
        return negative ? -v : v;
    }

    private static string Holder(int h) => h >= 0 ? "PL" + h.ToString(CultureInfo.InvariantCulture) : "ST" + (-1 - h).ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void TheResearchSheet_IsEveryTelemetryResearchRow_CodedAndExact_ThePlayerAndTheAiIncluded()
    {
        string dir = TempDir("civsim-research-sheet");
        try
        {
            UiSession session = UiSession.Start(42, aiEmpiresOverride: 1);
            string log = ExportFixtures.PlayToDisk(session, dir, "20261111-120000-a1", turns: 40, PlayerResearch);
            (string path, SessionExportResult r) = SessionWorkbook.ExportBeside(UiSession.ManifestPath(log));
            Assert.Equal(["v6"], r.Codebooks);
            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(File.ReadAllBytes(path));
            Assert.Equal(SessionWorkbook.SheetNames, back.SheetNames);
            IReadOnlyList<string[]> sheet = back.Sheets[Sheet("Research")];
            Assert.Equal(["T", "HD", "KD", "RN", "LN", "PR", "AC", "PG", "CS", "LV", "GN", "DR", "BL"], sheet[0]);

            // the telemetry's research rows, as the forensic reader reads them, are the sheet's rows in order
            TelemetryRecordFile telemetry = TelemetryRecordFile.Read(UiSession.TelemetryPath(log));
            int at = 1, completions1 = 0, completions2 = 0;
            var rows1 = 0;
            var rows2 = 0;
            foreach (TelemetryTurn turn in telemetry.Turns)
            {
                Assert.True(turn.Research!.Recorded);
                string t = turn.Turn.ToString(CultureInfo.InvariantCulture);
                foreach (ResearchStep step in turn.Research.Steps)
                {
                    string[] row = sheet[at++];
                    Assert.Equal(t, row[0]);
                    Assert.Equal(Holder(step.Holder), row[1]);
                    Assert.Equal(step.Completed ? "RC" : "RP", row[2]);
                    Assert.Equal("RN" + step.Node.ToString(CultureInfo.InvariantCulture), row[3]);
                    Assert.Equal(step.Tree == "Civics" ? "LC" : "LT", row[4]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(step.Received), BitConverter.DoubleToInt64Bits(CellNumber.Parse(row[5])));
                    Assert.Equal(BitConverter.DoubleToInt64Bits(step.Credit), BitConverter.DoubleToInt64Bits(CellNumber.Parse(row[6])));
                    Assert.Equal(BitConverter.DoubleToInt64Bits(step.Progress), BitConverter.DoubleToInt64Bits(CellNumber.Parse(row[7])));
                    Assert.Equal(BitConverter.DoubleToInt64Bits(step.Cost), BitConverter.DoubleToInt64Bits(CellNumber.Parse(row[8])));
                    Assert.Equal(step.Level.ToString(CultureInfo.InvariantCulture), row[9]);
                    if (step.Holder == 1) { rows1++; if (step.Completed) completions1++; }
                    if (step.Holder == 2) { rows2++; if (step.Completed) completions2++; }
                }
                foreach (CivicsBankStep b in turn.Research.Bank)
                {
                    string[] row = sheet[at++];
                    Assert.Equal([t, Holder(b.Holder), "BK", "", "", "", "", "", "", ""], row[..10]);
                    Assert.Equal(b.GeneratedMilli, Milli(row[10]));
                    Assert.Equal(b.DrawnMilli, Milli(row[11]));
                    Assert.Equal(b.BalanceMilli, Milli(row[12]));
                }
            }
            Assert.Equal(sheet.Count, at);
            Assert.True(rows1 > 0 && rows2 > 0 && completions1 > 0 && completions2 > 0,
                $"the player and the AI both research and complete: {rows1}/{completions1}, {rows2}/{completions2}");

            // TURN-EXACT, BY AN INDEPENDENT WRITER: each "research completed (the player's)" chronicle event (StateChronicle,
            // year Y) is the player's RC row whose turn has year Y in the trace, and every player RC row is such an event.
            string[] chronicle = ExportFixtures.Lines(File.ReadAllBytes(UiSession.ChroniclePath(log)));
            var events = new List<(string Year, string Node)>();
            foreach (string line in chronicle)
            {
                string[] f = line.Split(' ');
                if (f.Length == 4 && f[1] == "RM" && f[2] == "PL1") events.Add((f[0][1..], f[3]));
            }
            string[] trace = ExportFixtures.Lines(File.ReadAllBytes(UiSession.TracePath(log)));
            var yearOfTurn = new Dictionary<string, string>(StringComparer.Ordinal);   // test lookup only, never iterated
            foreach (string line in trace[1..])   // the chronicle prints the year rounded to a whole year (ChronicleProse.FormatYear)
                yearOfTurn[line.Split(',')[0]] = ((long)Math.Round(double.Parse(line.Split(',')[1], CultureInfo.InvariantCulture)))
                    .ToString(CultureInfo.InvariantCulture);
            var rc = new List<(string Year, string Node)>();
            foreach (string[] row in sheet)   // a first completion (a repeatable's later levels add no chronicle event)
                if (row.Length > 9 && row[1] == "PL1" && row[2] == "RC" && row[9] is "0" or "1") rc.Add((yearOfTurn[row[0]], row[3]));
            events.Sort((x, y) => string.CompareOrdinal(x.Year + " " + x.Node, y.Year + " " + y.Node));
            rc.Sort((x, y) => string.CompareOrdinal(x.Year + " " + x.Node, y.Year + " " + y.Node));
            Assert.Equal(events, rc);
            Assert.NotEmpty(rc);

            // NAMED: every node code of the sheet has its name in the legend (the chronicle's name lines)
            List<(string Section, string Code, string Meaning)> legend = SessionExportCodesTests.Legend(back.Sheets[Sheet("Codebook")]);
            var named = new HashSet<string>(StringComparer.Ordinal);   // test lookup only
            foreach ((string section, string code, string _) in legend) if (section == "Chronicle:names") named.Add(code);
            for (int i = 1; i < sheet.Count; i++)   // row 1 is the header (its RN is the column's code)
                if (sheet[i].Length > 3 && sheet[i][3].StartsWith("RN", StringComparison.Ordinal)) Assert.Contains(sheet[i][3], named);

            // CODED: no word outside the legend sheets, every code defined
            Assert.Empty(SessionExportCodesTests.Words(back));
            Assert.Empty(SessionExportCodesTests.UndefinedCodes(back));
            Assert.Contains("node row(s)", ReadmeValue(back, "sheet Research"), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>OLDER VINTAGES STILL EXPORT, AND NOTHING IS INVENTED: the Director's 68-turn session (telemetry/v4) and the
    /// 16-turn current-v5 session export with a Research sheet that holds its header and one NR row (the number of telemetry
    /// lines without research rows), and a README that says research was not recorded. A session whose telemetry file is
    /// missing says NP on the Research sheet, as on the Telemetry sheet.</summary>
    [Theory]
    [InlineData("director-68", 68)]
    [InlineData("current-v5", 16)]
    public void ASessionWrittenBeforeV6_ExportsWithTheResearchSheetMarkedNotRecorded(string fixture, int turns)
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files(fixture);
        OoxmlWorkbook.Content back = OoxmlWorkbook.Read(SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files)).Bytes);
        IReadOnlyList<string[]> sheet = back.Sheets[Sheet("Research")];
        Assert.Equal(2, sheet.Count);
        Assert.Equal(["NR", turns.ToString(CultureInfo.InvariantCulture)], sheet[1]);
        Assert.Contains("written before research rows were recorded", ReadmeValue(back, "sheet Research"), StringComparison.Ordinal);

        files.RemoveAll(f => f.Name.StartsWith("telemetry-", StringComparison.Ordinal));
        OoxmlWorkbook.Content without = OoxmlWorkbook.Read(SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files)).Bytes);
        IReadOnlyList<string[]> missing = without.Sheets[Sheet("Research")];
        Assert.Equal(2, missing.Count);
        Assert.Equal("NP", missing[1][0]);
        Assert.Equal(without.Sheets[Sheet("Telemetry")][1], missing[1]);   // the same NP row the Telemetry sheet holds
    }

    /// <summary>
    /// RECORDED BUT NOTHING RESEARCHED IS NOT "NOT RECORDED" (verification round 1, RR-R1-03 (a)): a telemetry/v6 session
    /// in which no line holds a research or bank row — the current-v6 session with both arrays emptied on all 16 lines, its
    /// close record listing the emptied file — exports with a Research sheet of its header and ONE NX row counting those 16
    /// lines: never the NR row of a session written before telemetry/v6, never an empty sheet. The README counts the turns,
    /// NX is a code the Codebook defines, and the workbook stays coded.
    /// </summary>
    [Fact]
    public void AV6SessionWithNoResearchRowOnAnyLine_ExportsOneNxRow_NotTheNotRecordedRow()
    {
        List<(string Name, byte[] Bytes)> files = ExportFixtures.Files("current-v6");
        int t = files.FindIndex(f => f.Name.StartsWith("telemetry-", StringComparison.Ordinal));
        int fr = files.FindIndex(f => f.Name.StartsWith("forensic-", StringComparison.Ordinal));
        string written = System.Text.Encoding.UTF8.GetString(files[t].Bytes);
        string emptied = System.Text.RegularExpressions.Regex.Replace(written, "\"research\":\\[[^\\]]*\\]", "\"research\":[]");
        emptied = System.Text.RegularExpressions.Regex.Replace(emptied, "\"civicsBank\":\\[[^\\]]*\\]", "\"civicsBank\":[]");
        Assert.NotEqual(written, emptied);   // the fixture's lines do hold research rows
        string[] lines = emptied.TrimEnd('\n').Split('\n');
        Assert.Equal(16, lines.Length);
        foreach (string line in lines)
        {
            Assert.Contains("\"schema\":\"telemetry/v6\"", line, StringComparison.Ordinal);
            Assert.Contains("\"research\":[],\"civicsBank\":[]", line, StringComparison.Ordinal);
        }
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(emptied);
        string forensic = System.Text.Encoding.UTF8.GetString(files[fr].Bytes), listed = ForensicIdentity.Sha256Hex(files[t].Bytes);
        Assert.Contains(listed, forensic, StringComparison.Ordinal);
        files[t] = (files[t].Name, bytes);
        files[fr] = (files[fr].Name, System.Text.Encoding.UTF8.GetBytes(
            forensic.Replace(listed, ForensicIdentity.Sha256Hex(bytes), StringComparison.Ordinal)));

        OoxmlWorkbook.Content back = OoxmlWorkbook.Read(SessionWorkbook.Build(ExportFixtures.Manifest(files), ExportFixtures.Reader(files)).Bytes);
        IReadOnlyList<string[]> sheet = back.Sheets[Sheet("Research")];
        Assert.Equal(2, sheet.Count);
        Assert.Equal(["NX", "16"], sheet[1]);
        Assert.EndsWith("This workbook has 0 node row(s), 0 of them completions, and 0 bank row(s), from 16 turn(s).",
            ReadmeValue(back, "sheet Research"), StringComparison.Ordinal);
        Assert.Contains(SessionExportCodesTests.Legend(back.Sheets[Sheet("Codebook")]), e => e.Section == "Research:kind" && e.Code == "NX");
        Assert.Empty(SessionExportCodesTests.Words(back));
        Assert.Empty(SessionExportCodesTests.UndefinedCodes(back));
    }

    /// <summary>
    /// THE RUN RECORD IS THE STATE'S RESEARCH, TURN BY TURN, THROUGH THE GAME'S OWN SESSION PATH (D-086 DE1; the brief's
    /// "for a known short headless session"). A session played as the game plays it (seed 42, one AI empire, the player
    /// researching, every End Turn saved), its telemetry FILE read back and compared with the session's own worlds by
    /// <see cref="AssertTheRowsAreTheWorldsResearch"/>, for every turn, every holder and every node. The holders this session
    /// has are the player PL1 and the AI empire PL2. A founded game has no city-state: founding gives every settlement to an
    /// empire, a colony inherits its founder's control, and a revolted place passes to a new polity (R3). Verification round
    /// 2 (RR-R2-01) corrected this text, which listed the city-states among this session's holders. A city-state's rows are
    /// tested end to end by <see cref="ACityStatesResearch_IsInTheRunRecord_AfterTheRosterPolities_CodedStByItsSettlement"/>.
    /// </summary>
    [Fact]
    public void TheTelemetryFilesResearchRows_AreTheWorldsResearchTurnByTurn_EveryHolder()
    {
        string dir = TempDir("civsim-research-state");
        try
        {
            UiSession session = UiSession.Start(42, aiEmpiresOverride: 1);
            var worlds = new List<IReadOnlyWorldState> { session.World };
            string log = ExportFixtures.PlayToDisk(session, dir, "20261111-130000-a1", turns: 30, (s, t) =>
            {
                if (t > 0) worlds.Add(s.World);   // the world the previous End Turn produced
                PlayerResearch(s, t);
            });
            worlds.Add(session.World);
            TelemetryRecordFile telemetry = TelemetryRecordFile.Read(UiSession.TelemetryPath(log));
            Assert.Equal(30, telemetry.Turns.Count);
            (List<int> holders, int rows, int completions) = AssertTheRowsAreTheWorldsResearch(telemetry, worlds);
            Assert.True(rows > 50 && completions > 0, $"rows {rows}, completions {completions}");
            Assert.Contains(1, holders);   // the player's empire
            Assert.Contains(2, holders);   // the AI empire
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// A CITY-STATE'S RESEARCH IS IN THE RUN RECORD, END TO END (verification round 2, RR-R2-01). A game founded on this tree
    /// never holds a city-state: founding gives every settlement to an empire (WorldFounding), a colony inherits its
    /// founder's control (ColonizationSystem), and a revolted place passes to a new polity (RevoltSystem, R3). So this
    /// session releases one settlement from the realm before the first turn, as the R2a preview does (R2aPreview.CityState):
    /// seed 42, one AI empire, the last settlement (11) with no controller. It researches on its own under its local key
    /// −1 − its id (−12), at the content's city-state pace. The session is played through the game's own session path for 64
    /// turns and written to disk for the export only: the export reads files, never a replay, and the record's seed alone
    /// would not rebuild the released settlement. Then:
    /// <list type="bullet">
    /// <item>the telemetry rows are the worlds' research turn by turn, for every holder
    /// (<see cref="AssertTheRowsAreTheWorldsResearch"/>). The city-state has research rows, bank rows and its first
    /// completion row among them (MEASURED: on turn 63);</item>
    /// <item>in every turn's record the roster polities' rows come first, then the city-state's: research rows and bank rows
    /// alike. For the bank rows this is the record's own order, not the accounts table's: that table keeps the city-state's
    /// account first (ascending key, ADR-043), which is checked as the premise;</item>
    /// <item>the Research sheet codes every city-state row ST and the released settlement's id, read from the world, and
    /// every other row PL and the polity's id, row for row with the telemetry. The workbook stays coded.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void ACityStatesResearch_IsInTheRunRecord_AfterTheRosterPolities_CodedStByItsSettlement()
    {
        const int turns = 64;   // the city-state's first completion is on turn 63 (MEASURED)
        string dir = TempDir("civsim-research-city");
        try
        {
            Sim.Core.State.WorldState founded = UiFounding.Found(42, aiEmpiresOverride: 1);
            SettlementId city = founded.Settlements[founded.Settlements.Count - 1].Id;
            var kept = new List<ControlRow>();
            for (int i = 0; i < founded.Controls.Count; i++) if (founded.Controls[i].Place != city) kept.Add(founded.Controls[i]);
            Assert.Equal(founded.Controls.Count - 1, kept.Count);   // the settlement had its one controller
            founded.Controls.Clear();
            foreach (ControlRow c in kept) founded.Controls.Add(c);
            int local = SettlementKnowledge.LocalHolder(city).Value;
            Assert.Equal(-1 - city.Value, local);

            UiSession session = UiSession.StartFrom(founded, 42);
            var worlds = new List<IReadOnlyWorldState> { session.World };
            string log = ExportFixtures.PlayToDisk(session, dir, "20261112-120000-a1", turns, (s, t) =>
            {
                if (t > 0) worlds.Add(s.World);   // the world the previous End Turn produced
                PlayerResearch(s, t);
            });
            worlds.Add(session.World);
            TelemetryRecordFile telemetry = TelemetryRecordFile.Read(UiSession.TelemetryPath(log));
            Assert.Equal(turns, telemetry.Turns.Count);
            var (holders, _, _) = AssertTheRowsAreTheWorldsResearch(telemetry, worlds);
            Assert.Contains(1, holders);
            Assert.Contains(2, holders);
            Assert.Contains(local, holders);

            // the record's order in every turn: the roster polities by id, then the city-states by settlement id
            int cityRows = 0, cityCompletions = 0, cityBank = 0;
            foreach (TelemetryTurn turn in telemetry.Turns)
            {
                ResearchStep[] steps = turn.Research!.Steps;
                for (int i = 1; i < steps.Length; i++)
                    Assert.True(Before(steps[i - 1].Holder, steps[i - 1].Node, steps[i].Holder, steps[i].Node),
                        $"turn {turn.Turn}: research row {steps[i - 1]} before {steps[i]}");
                CivicsBankStep[] bank = turn.Research.Bank;
                for (int i = 1; i < bank.Length; i++)
                    Assert.True(Before(bank[i - 1].Holder, 0, bank[i].Holder, 0), $"turn {turn.Turn}: bank row {bank[i - 1]} before {bank[i]}");
                foreach (ResearchStep r in steps)
                    if (r.Holder == local) { cityRows++; if (r.Completed) cityCompletions++; }
                foreach (CivicsBankStep b in bank)
                    if (b.Holder == local) cityBank++;
            }
            Assert.True(cityRows > 0 && cityCompletions > 0 && cityBank > 0,
                $"the city-state's rows: research {cityRows}, completions {cityCompletions}, bank {cityBank}");
            Assert.Equal(local, worlds[^1].CivicsPoints[0].Polity.Value);   // the premise: the table holds its account first

            // the Research sheet: the city-state is ST and the released settlement's id, row for row with the telemetry
            (string path, SessionExportResult _) = SessionWorkbook.ExportBeside(UiSession.ManifestPath(log));
            OoxmlWorkbook.Content back = OoxmlWorkbook.Read(File.ReadAllBytes(path));
            IReadOnlyList<string[]> sheet = back.Sheets[Sheet("Research")];
            string st = "ST" + city.Value.ToString(CultureInfo.InvariantCulture);
            string Code(int holder) => holder == local ? st : "PL" + holder.ToString(CultureInfo.InvariantCulture);
            int at = 1, stRows = 0;
            foreach (TelemetryTurn turn in telemetry.Turns)
            {
                string t = turn.Turn.ToString(CultureInfo.InvariantCulture);
                foreach (ResearchStep step in turn.Research!.Steps)
                {
                    string[] row = sheet[at++];
                    Assert.Equal([t, Code(step.Holder), step.Completed ? "RC" : "RP", "RN" + step.Node.ToString(CultureInfo.InvariantCulture)], row[..4]);
                    if (row[1] == st) stRows++;
                }
                foreach (CivicsBankStep b in turn.Research.Bank)
                {
                    string[] row = sheet[at++];
                    Assert.Equal([t, Code(b.Holder), "BK"], row[..3]);
                    Assert.Equal(b.GeneratedMilli, Milli(row[10]));
                    if (row[1] == st) stRows++;
                }
            }
            Assert.Equal(sheet.Count, at);
            Assert.Equal(cityRows + cityBank, stRows);
            Assert.Empty(SessionExportCodesTests.Words(back));
            Assert.Empty(SessionExportCodesTests.UndefinedCodes(back));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The record's documented row order, written out here: a roster polity (key ≥ 0, by id) before a city-state
    /// (key below 0, by its settlement id −1 − key), then the node.</summary>
    private static bool Before(int holderA, int nodeA, int holderB, int nodeB)
    {
        (int Group, int Ordinal) a = holderA >= 0 ? (0, holderA) : (1, -1 - holderA), b = holderB >= 0 ? (0, holderB) : (1, -1 - holderB);
        return a.Group != b.Group ? a.Group < b.Group : a.Ordinal != b.Ordinal ? a.Ordinal < b.Ordinal : nodeA < nodeB;
    }

    /// <summary>
    /// The telemetry file's research rows against the session's own worlds — <paramref name="worlds"/>[t] before turn t + 1's
    /// End Turn, [t + 1] after it — for every turn, every holder and every node:
    /// <list type="bullet">
    /// <item>a node whose stored progress moved has exactly one row, and the row's progress is, bit for bit, the progress the
    /// world stores after the turn, its points received the difference from the world before;</item>
    /// <item>a node that completed (its completed row appeared, or its level rose) has exactly one row, a completion row, on
    /// that very turn and no other; a row on a turn says completed only if the world completed the node in that step;</item>
    /// <item>no row names a node whose progress did not move and that did not complete;</item>
    /// <item>each bank row is the CivicsPoints account's change, exactly, and every account that changed has its row.</item>
    /// </list>
    /// Nothing here re-derives research. Returns the holders that have rows (first appearance order), the rows and the
    /// completion rows.
    /// </summary>
    private static (List<int> Holders, int Rows, int Completions) AssertTheRowsAreTheWorldsResearch(
        TelemetryRecordFile telemetry, List<IReadOnlyWorldState> worlds)
    {
        Assert.Equal(telemetry.Turns.Count + 1, worlds.Count);
        int rows = 0, completions = 0;
        var holders = new List<int>();
        for (int t = 0; t < telemetry.Turns.Count; t++)
        {
            IReadOnlyWorldState prev = worlds[t], next = worlds[t + 1];
            TelemetryTurn turn = telemetry.Turns[t];
            Assert.Equal(next.Clock.Turn, turn.Turn);
            Assert.True(turn.Research!.Recorded);
            var seen = new List<(int Holder, int Node)>();
            foreach (ResearchStep r in turn.Research.Steps)
            {
                Assert.DoesNotContain((r.Holder, r.Node), seen);   // one row per holder and node
                seen.Add((r.Holder, r.Node));
                if (!holders.Contains(r.Holder)) holders.Add(r.Holder);
                double? before = ProgressOf(prev, r.Holder, r.Node), after = ProgressOf(next, r.Holder, r.Node);
                bool completed = (Done(next, r.Holder, r.Node) && !Done(prev, r.Holder, r.Node))
                    || LevelOf(next, r.Holder, r.Node) > LevelOf(prev, r.Holder, r.Node);
                Assert.True(completed == r.Completed, $"turn {turn.Turn}: {r} completed {completed}");
                if (completed)
                {
                    completions++;
                    Assert.Null(after);   // the world removes a completed node's progress row
                }
                else
                {
                    Assert.Equal(Bits(after ?? double.NaN), Bits(r.Progress));
                    Assert.Equal(Bits(r.Progress - (before ?? 0.0)), Bits(r.Received));
                }
                rows++;
            }
            // every node whose stored progress moved, and every completion, has its row
            for (int i = 0; i < next.ResearchProgress.Count; i++)
            {
                ResearchProgressRow p = next.ResearchProgress[i];
                double? before = ProgressOf(prev, p.Polity.Value, p.Node.Value);
                if (before is null || Bits(before.Value) != Bits(p.Progress))
                    Assert.Contains((p.Polity.Value, p.Node.Value), seen);
            }
            for (int i = 0; i < next.ResearchCompleted.Count; i++)
            {
                ResearchCompletedRow c = next.ResearchCompleted[i];
                if (!Done(prev, c.Polity.Value, c.Node.Value)) Assert.Contains((c.Polity.Value, c.Node.Value), seen);
            }
            // the bank rows: each account's change, exactly; every changed account has its row
            var banked = new List<int>();
            foreach (CivicsBankStep b in turn.Research.Bank)
            {
                CivicsPointsRow now = Account(next, b.Holder), then = Account(prev, b.Holder);
                Assert.Equal(now.GeneratedMilli - then.GeneratedMilli, b.GeneratedMilli);
                Assert.Equal(now.SpentMilli - then.SpentMilli, b.DrawnMilli);
                Assert.Equal(now.BalanceMilli, b.BalanceMilli);
                banked.Add(b.Holder);
            }
            for (int i = 0; i < next.CivicsPoints.Count; i++)
            {
                CivicsPointsRow now = next.CivicsPoints[i], then = Account(prev, now.Polity.Value);
                if (now != then with { Polity = now.Polity }) Assert.Contains(now.Polity.Value, banked);
            }
        }
        return (holders, rows, completions);
    }

    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    private static double? ProgressOf(IReadOnlyWorldState w, int holder, int node)
    {
        for (int i = 0; i < w.ResearchProgress.Count; i++)
            if (w.ResearchProgress[i].Polity.Value == holder && w.ResearchProgress[i].Node.Value == node) return w.ResearchProgress[i].Progress;
        return null;
    }

    private static bool Done(IReadOnlyWorldState w, int holder, int node)
    {
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (w.ResearchCompleted[i].Polity.Value == holder && w.ResearchCompleted[i].Node.Value == node) return true;
        return false;
    }

    private static int LevelOf(IReadOnlyWorldState w, int holder, int node)
    {
        for (int i = 0; i < w.ResearchLevels.Count; i++)
            if (w.ResearchLevels[i].Polity.Value == holder && w.ResearchLevels[i].Node.Value == node) return w.ResearchLevels[i].Level;
        return 0;
    }

    private static CivicsPointsRow Account(IReadOnlyWorldState w, int holder)
    {
        for (int i = 0; i < w.CivicsPoints.Count; i++)
            if (w.CivicsPoints[i].Polity.Value == holder) return w.CivicsPoints[i];
        return new CivicsPointsRow(new PolityId(holder), 0L, 0L, 0L);
    }

    private static string ReadmeValue(OoxmlWorkbook.Content back, string key)
    {
        foreach (string[] row in back.Sheets[0])
            if (row.Length > 1 && row[0] == key) return row[1];
        throw new InvalidOperationException("no README row " + key);
    }
}
