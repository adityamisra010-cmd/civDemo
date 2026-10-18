using Sim.Core.Chronicle;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>Integration items 3 (state-derived annal events), 4 (colony names) and 6 (resume by replay).</summary>
public class ChronicleAndResumeTests
{
    private static UiSession Played(int turns, int? ai = null)
    {
        var s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: ai);
        for (int t = 0; t < turns; t++) s.EndTurn();
        return s;
    }

    private static List<string> Diff(UiSession s, WorldState prev, WorldState next)
    {
        var lines = new List<string>();
        new StateChronicle().Observe(prev, next, s.Config, s.Names.Name, lines);
        return lines;
    }

    [Fact]
    public void AStepThatChangesNothingWritesNoEvent()
    {
        UiSession s = Played(2);
        Assert.Empty(Diff(s, s.World, s.World.Clone()));
    }

    [Fact]
    public void ConstructionInstitutionsControlAndTax_EachBecomeOneAnnalLine()
    {
        UiSession s = Played(1);
        WorldState prev = s.World;
        WorldState next = prev.Clone();
        SettlementId site = next.Settlements[0].Id;
        string where = s.Names.Name(site.Value);
        var project = s.Config.Goods!.Projects![0];
        next.Structures.Add(new StructureRow(site, project.Id, ConstructionQuery.Built(prev, site, project.Id) + 1));
        int type = s.Config.Research!.UniversityTypes[0].Key;
        next.Institutions.Add(new InstitutionRow(77, UiPlayer.Empire, site, type, next.Clock.Turn, 0.1));
        next.TaxPolicies.Add(new TaxPolicyRow(UiPlayer.Empire, 0.1));
        // Control lost: drop every control row of the second settlement (it was the player's).
        SettlementId lost = next.Settlements[1].Id;
        Assert.True(EmpireQuery.TryGetController(prev, lost, out _));
        var keep = new List<ControlRow>();
        for (int i = 0; i < next.Controls.Count; i++) if (next.Controls[i].Place != lost) keep.Add(next.Controls[i]);
        next.Controls.Clear();
        foreach (ControlRow c in keep) next.Controls.Add(c);

        List<string> lines = Diff(s, prev, next);
        Assert.Contains(lines, l => l.EndsWith(where + " completed a " + project.Name.ToLowerInvariant() + ".", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(" was founded at " + where + ".", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith("Your empire levied a tax of 10%.", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(s.Names.Name(lost.Value) + " rose up and threw off the rule of your empire.", StringComparison.Ordinal));
        foreach (string l in lines) Assert.StartsWith("In the year ", l, StringComparison.Ordinal);

        // Maturity: the same institution crossing matureAt is one "came to maturity" line, and only then.
        WorldState grown = next.Clone();
        for (int i = 0; i < grown.Institutions.Count; i++)
            if (grown.Institutions[i].Id == 77) { InstitutionRow r = grown.Institutions[i]; grown.Institutions[i] = r with { Maturity = 0.999 }; }
        List<string> later = Diff(s, next, grown);
        Assert.Single(later);
        Assert.EndsWith(" at " + where + " came to maturity.", later[0], StringComparison.Ordinal);
    }

    /// <summary>H2 (Director 2026-10-05 §4/§18): a population segment crossing its own tipping point is one annal
    /// line ("rose against the levy"), its rising subsiding another — read on both worlds through the public
    /// Unrest reader.</summary>
    [Fact]
    public void ASegmentRisingAgainstTheLevy_AndItsSubsiding_EachBecomeOneAnnalLine()
    {
        UiSession s = Played(1);
        SettlementId site = s.World.Settlements[0].Id;
        ClassId cls = default;
        for (int i = 0; i < s.World.Buckets.Count; i++)
            if (s.World.Buckets[i].Settlement == site && s.World.Buckets[i].Count.Value > 0) { cls = s.World.Buckets[i].Class; break; }
        string name = Array.Find(s.Config.Registries.Classes, e => e.Id == cls.Value)!.Name.ToLowerInvariant();
        double tip = s.Config.Needs!.Unrest!.UprisingGrievance;

        WorldState calm = s.World.Clone();
        calm.TaxGrievances.Add(new TaxGrievanceRow(site, cls, tip * 0.9));
        WorldState risen = s.World.Clone();
        risen.TaxGrievances.Add(new TaxGrievanceRow(site, cls, tip));

        List<string> rose = Diff(s, calm, risen);
        Assert.Single(rose);
        Assert.StartsWith("In the year ", rose[0], StringComparison.Ordinal);
        Assert.EndsWith(", the " + name + " of " + s.Names.Name(site.Value) + " rose against the levy.", rose[0], StringComparison.Ordinal);
        List<string> fell = Diff(s, risen, calm);
        Assert.Single(fell);
        Assert.EndsWith("the rising of the " + name + " of " + s.Names.Name(site.Value) + " subsided.", fell[0], StringComparison.Ordinal);
        Assert.Empty(Diff(s, risen, risen.Clone()));
    }

    [Fact]
    public void ColoniesFoundedMidGame_AreNamed_AndExistingNamesNeverChange()
    {
        // The registry is a pure function of (seed, ids): rebuilding over a world that gained ids keeps the
        // earlier names — what UiSession relies on when it rebuilds after a founding step.
        UiSession s = Played(1);
        WorldState grown = s.World.Clone();
        int newId = 0;
        for (int i = 0; i < grown.Settlements.Count; i++) newId = Math.Max(newId, grown.Settlements[i].Id.Value + 1);
        grown.Settlements.Add(grown.Settlements[0] with { Id = new SettlementId(newId) });
        using var stream = Sim.Data.DataFiles.OpenChronicle();
        ChronicleConfig cc = ChronicleConfigLoader.Load(stream);
        NameRegistry before = NameRegistry.Build(cc, s.World.Seed, s.World);
        NameRegistry after = NameRegistry.Build(cc, s.World.Seed, grown);
        for (int i = 0; i < s.World.Settlements.Count; i++)
            Assert.Equal(before.Name(s.World.Settlements[i].Id.Value), after.Name(s.World.Settlements[i].Id.Value));
        Assert.DoesNotContain("settlement", after.Name(newId), StringComparison.Ordinal);
        // And in a played session no settlement — colonies included — falls back to "settlement N".
        UiSession played = Played(60);
        for (int i = 0; i < played.World.Settlements.Count; i++)
            Assert.DoesNotContain("settlement ", played.Names.Name(played.World.Settlements[i].Id.Value), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlayerTitleCarriesTheNameOnly()
    {
        UiSession s = Played(0);
        int id = s.World.Settlements[0].Id.Value;
        HudModel hud = HudModel.From(s.World, id, null, s.Names.Name(id));
        Assert.Equal(s.Names.Name(id), hud.TitleLine);
    }

    [Fact]
    public void Resume_RebuildsTheWorldAndTheUiRecords_AndContinuedPlayIsIdentical()
    {
        string dir = Path.Combine(Path.GetTempPath(), "u2b-resume-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        string log = Path.Combine(dir, "orders-20260101-120000-s256-n4-a1.bin");

        UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        live.ExportManifest("2026-01-01 12:00:00", log);
        int capital = live.World.Settlements[0].Id.Value;
        for (int t = 0; t < 8; t++)
        {
            if (t == 2) Assert.True(live.EmitSectorOrders([40, 20, 10, 10, 20], capital));
            if (t == 3 && live.Config.Research is { } rc)
            {
                ResearchNodeId[] avail = ResearchQuery.AvailableNodes(live.World, rc, UiPlayer.Empire);
                if (avail.Length > 0) Assert.True(live.EmitResearchOrder(avail[0]));
            }
            live.EndTurn();
        }
        Assert.True(live.EmitSectorOrders([30, 30, 10, 10, 20], capital));   // queued, not yet played
        int aiOrders = 0;
        for (int i = 0; i < live.Orders.Count; i++) if (live.Orders[i].ActorId != UiPlayer.ActorId) aiOrders++;
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));

        UiSession resumed = UiSession.Resume(UiArgs.ResolveManifest(dir), out string resumedLog);
        Assert.Equal(Path.GetFullPath(log), Path.GetFullPath(resumedLog));
        Assert.Equal(live.World.Clock.Turn, resumed.World.Clock.Turn);
        Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
        Assert.Equal(live.Orders.Count, resumed.Orders.Count);
        Assert.Equal(live.AnnalLines.ToArray(), resumed.AnnalLines.ToArray());
        Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
        Assert.Equal(live.Observations.Observations.Count, resumed.Observations.Observations.Count);
        Assert.Equal(live.Observations.PolicyChanges.Count, resumed.Observations.PolicyChanges.Count);
        for (int i = 0; i < live.World.Settlements.Count; i++)
        {
            int id = live.World.Settlements[i].Id.Value;
            Assert.Equal(live.Names.Name(id), resumed.Names.Name(id));
        }

        for (int t = 0; t < 4; t++) { live.EndTurn(); resumed.EndTurn(); }
        Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
        Assert.Equal(live.Orders.Count, resumed.Orders.Count);
        for (int i = 0; i < live.Orders.Count; i++) Assert.Equal(live.Orders[i], resumed.Orders[i]);
        Assert.True(aiOrders > 0, "the AI empire issued no order - the AI part of the replay was not exercised");
        Directory.Delete(dir, recursive: true);
    }

    /// <summary>M5 playtest blocker (D-052): the trace check's refusal is a typed <see cref="ResumeDivergedException"/>
    /// (it was a plain InvalidOperationException) carrying the turn and both hashes, its message the check's own text.
    /// The owner asked for this change of type; the refusal itself is unchanged.</summary>
    [Fact]
    public void Resume_RefusesATraceItCannotReproduce()
    {
        UiSession live = Played(3);
        UiSession fresh = Played(0);
        var bad = new List<SessionTrace.Row> { new(1, 0, 0, 0, 0, "deadbeef") };
        ResumeDivergedException e = Assert.Throws<ResumeDivergedException>(() => fresh.ReplayTo(live.Orders, 3, bad));
        Assert.Equal(1, e.Turn);
        Assert.Equal("deadbeef", e.SavedHash);
        Assert.Equal(64, e.ReplayHash.Length);
        Assert.Equal("resume diverged at turn 1: replay hash " + e.ReplayHash + ", saved trace deadbeef", e.Message);
    }

    /// <summary>Rewrites a session manifest so it records <paramref name="schemaVersion"/> — what an older build
    /// wrote into it (every other field is the session's own).</summary>
    internal static void RecordSchema(string manifestPath, int schemaVersion)
    {
        SessionManifest m;
        using (FileStream f = File.OpenRead(manifestPath)) m = SessionManifest.Read(f, manifestPath);
        using FileStream o = File.Create(manifestPath);
        (m with { SchemaVersion = schemaVersion }).Write(o);
    }

    /// <summary>M5 playtest blocker (D-052): rewrites a session manifest so it records another build's identity —
    /// what that build wrote into it (every other field is the session's own).</summary>
    internal static void RecordBuild(string manifestPath, string buildSha, string buildDate, string platform)
    {
        SessionManifest m;
        using (FileStream f = File.OpenRead(manifestPath)) m = SessionManifest.Read(f, manifestPath);
        using FileStream o = File.Create(manifestPath);
        (m with { BuildSha = buildSha, BuildDate = buildDate, Platform = platform }).Write(o);
    }

    /// <summary>Every file in <paramref name="dir"/> as "name sha256", ordinal by name — to prove a refused resume
    /// wrote, changed and created nothing there.</summary>
    internal static string[] Fingerprint(string dir)
    {
        string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        var lines = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
            lines[i] = Path.GetRelativePath(dir, files[i]) + " "
                + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(files[i])));
        return lines;
    }

    /// <summary>Rewrites every trace line's hash as a build with another canonical stream would have computed it:
    /// WorldHash covers the whole stream, so after a schema bump no line can equal this build's replay. The
    /// stand-in value (64 zeros) is a fixture, not a measured older-schema hash.</summary>
    internal static void RecordTraceUnderAnotherSchema(string tracePath)
    {
        string[] lines = File.ReadAllLines(tracePath);
        for (int i = 1; i < lines.Length; i++)
            if (lines[i].Length > 0) lines[i] = lines[i][..(lines[i].LastIndexOf(',') + 1)] + new string('0', 64);
        File.WriteAllText(tracePath, string.Join("\n", lines) + "\n");
    }

    /// <summary>
    /// S0 (2026-10-10, D-008) — A SESSION RECORDED UNDER AN OLDER SCHEMA. Before, Resume never read the manifest's
    /// SchemaVersion and checked the old trace's hashes, which cover the whole canonical stream — so every session
    /// saved before any schema bump failed with "resume diverged at turn 1". Now it resumes by the D-008 recovery
    /// path: re-founded from the seed and replayed from the order log without the trace-hash check (the trace still
    /// gives the turn), with an explicit notice; and it continues hash-identically to a fresh replay of the same
    /// seed and log. The control proves the check is skipped BECAUSE of the mismatch: the same doctored trace under
    /// this build's schema is still refused.
    /// </summary>
    [Fact]
    public void Resume_OfASessionRecordedUnderAnOlderSchema_ReplaysWithTheNotice_AndContinuesLikeAFreshReplay()
    {
        string dir = Path.Combine(Path.GetTempPath(), "s0-schema-resume-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        string log = Path.Combine(dir, "orders-20261010-120000-s256-n4-a1.bin");
        try
        {
            // Fix round 1 (D-054, 2026-10-15): the session is recorded FLAT, as every session recorded under the previous
            // schema (v32) was — Resume now re-founds a pre-v33 session flat (TopologyFlags.RecordedFlat). MEASURED with
            // the cylinder rig after that fix: the resumed (flat) world no longer equalled the fresh cylinder replay
            // ("Expected: 0d5c72e1…, Actual: 71883a5e…" at the fresh-replay comparison) — the rig, a cylinder session
            // relabelled v32, is a session no v32 build could have written. Re-rigged, not re-pinned: every assertion stands.
            UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1, wrapXOverride: false);
            live.ExportManifest("2026-10-10 12:00:00", log);
            int capital = live.World.Settlements[0].Id.Value;
            for (int t = 0; t < 8; t++)
            {
                if (t == 2) Assert.True(live.EmitSectorOrders([40, 20, 10, 10, 20], capital));
                live.EndTurn();
            }
            Assert.True(live.EmitSectorOrders([30, 30, 10, 10, 20], capital));   // queued, not yet played
            live.Save(log);
            live.ExportTrace(UiSession.TracePath(log));
            string manifestPath = UiSession.ManifestPath(log);

            // D-055 (2026-10-16) CAUSE BLOCK (X1 (h)): was `older = CanonicalSchema.Version - 1`, which meant v32 — the flat
            // schema — only while this build was v33. Under v34 it would mean v33, a CYLINDER schema (TopologyFlags
            // .RecordedFlat(33) is false), and this flat arm would silently change meaning. It is the explicit 32 now; never
            // redefine Version − 1 as flat. The v33 arm is its own test below (a cylinder rebuild).
            int older = 32;
            RecordTraceUnderAnotherSchema(UiSession.TracePath(log));

            // Control: under THIS build's schema the doctored trace is refused at the first replayed turn — as an
            // incompatible session since D-052 (the owner's change), the check's divergence its inner exception.
            RecordSchema(manifestPath, CanonicalSchema.Version);
            SessionIncompatibleException refused = Assert.Throws<SessionIncompatibleException>(() => UiSession.Resume(manifestPath, out _));
            ResumeDivergedException diverged = Assert.IsType<ResumeDivergedException>(refused.InnerException);
            Assert.StartsWith("resume diverged at turn 1:", diverged.Message, StringComparison.Ordinal);

            // Recorded under the previous schema: resumed by replay, with the notice.
            RecordSchema(manifestPath, older);
            UiSession resumed = UiSession.Resume(manifestPath, out string resumedLog);
            Assert.Equal(Path.GetFullPath(log), Path.GetFullPath(resumedLog));
            Assert.Equal(UiSession.SchemaMismatchNotice(older, CanonicalSchema.Version), resumed.ResumeNotice);
            Assert.Equal("Recorded under save schema v" + older.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "; this build is v" + CanonicalSchema.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".",
                resumed.ResumeNotice[0]);
            Assert.Contains("D-008", resumed.ResumeNotice[1], StringComparison.Ordinal);
            Assert.Empty(live.ResumeNotice);   // a session started here carries none
            Assert.Equal(8, resumed.World.Clock.Turn);   // the trace's turn column still says how far it got

            // A fresh replay of the same seed and log, with no trace at all.
            OrderLog saved;
            using (FileStream f = File.OpenRead(log)) saved = OrderLog.Load(f);
            UiSession fresh = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1, wrapXOverride: false);
            fresh.ReplayTo(saved, 8);
            Assert.False(resumed.World.Terrain!.WrapX);                                // rebuilt on the flat map it was played on
            Assert.Equal(WorldHash.ComputeHex(fresh.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(fresh.TraceLines.ToArray(), resumed.TraceLines.ToArray());   // rebuilt with this build's hashes
            Assert.Equal(fresh.Orders.Count, resumed.Orders.Count);                    // the queued split is re-queued

            // ...and continues hash-identically to it, turn by turn (the AI producer runs live in all three).
            for (int t = 0; t < 4; t++)
            {
                live.EndTurn();
                resumed.EndTurn();
                fresh.EndTurn();
                Assert.Equal(WorldHash.ComputeHex(fresh.World), WorldHash.ComputeHex(resumed.World));
                Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            }
            Assert.Equal(fresh.Orders.Count, resumed.Orders.Count);
            for (int i = 0; i < fresh.Orders.Count; i++) Assert.Equal(fresh.Orders[i], resumed.Orders[i]);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// M5 playtest blocker (D-052) — A SESSION ANOTHER BUILD RECORDED UNDER THIS SCHEMA. The audit's case: a session
    /// from playtest build 2dc6af2 (schema v32, like this build) resumed here replays into a different world from turn
    /// 1, so the trace check refuses it — and that refusal ended the launch as an unhandled exception. Now Resume
    /// refuses it with a <see cref="SessionIncompatibleException"/> that tells the player which build recorded it,
    /// which build this is, from which turn the worlds differ and what to do, the check's
    /// <see cref="ResumeDivergedException"/> kept as the inner exception. Nothing in the session directory is
    /// written, changed or created (every file hashed before and after). The fixture: this build's own session,
    /// its manifest rewritten to name build 2dc6af2 on win-x64 and its trace's hashes replaced (the stand-in for a
    /// world another build computed — <see cref="RecordTraceUnderAnotherSchema"/>, under the SAME schema here).
    /// </summary>
    [Fact]
    public void Resume_OfASessionAnotherBuildRecordedUnderThisSchema_IsRefusedAsIncompatible_AndItsFilesAreUntouched()
    {
        string dir = Path.Combine(Path.GetTempPath(), "d052-cross-build-resume-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        string log = Path.Combine(dir, "orders-20261011-120000-s256-n4.bin");
        try
        {
            UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
            live.ExportManifest("2026-10-11 12:00:00", log);
            live.ExportForensicRun("2026-10-11 12:00:00+00:00", log);
            int capital = live.World.Settlements[0].Id.Value;
            for (int t = 0; t < 3; t++)
            {
                if (t == 1) Assert.True(live.EmitSectorOrders([40, 20, 15, 15, 10], capital));
                live.EndTurn();
            }
            live.Save(log);
            live.ExportTrace(UiSession.TracePath(log));
            live.ExportChronicle(UiSession.ChroniclePath(log));
            live.FinalizeTelemetry(UiSession.TelemetryPath(log));
            string manifestPath = UiSession.ManifestPath(log);
            RecordBuild(manifestPath, "2dc6af2", "2026-10-11", "win-x64");
            RecordTraceUnderAnotherSchema(UiSession.TracePath(log));
            string[] before = Fingerprint(dir);
            Assert.Equal(6, before.Length);   // manifest, forensic, orders, trace, chronicle, telemetry

            SessionIncompatibleException e = Assert.Throws<SessionIncompatibleException>(() => UiSession.Resume(manifestPath, out _));
            Assert.Equal(before, Fingerprint(dir));   // nothing written, changed or created

            // The facts, typed...
            Assert.Equal(Path.GetFullPath(manifestPath), e.ManifestPath);
            Assert.Equal(("2dc6af2", "2026-10-11", "win-x64"), (e.RecordedBuildSha, e.RecordedBuildDate, e.RecordedPlatform));
            Assert.Equal((BuildInfo.Sha, BuildInfo.Date, System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier),
                (e.CurrentBuildSha, e.CurrentBuildDate, e.CurrentPlatform));
            Assert.Equal(1, e.Turn);
            ResumeDivergedException inner = Assert.IsType<ResumeDivergedException>(e.InnerException);
            Assert.StartsWith("resume diverged at turn 1: replay hash ", inner.Message, StringComparison.Ordinal);
            Assert.Equal(new string('0', 64), inner.SavedHash);
            Assert.Equal(1, inner.Turn);

            // ...and the player's message: what happened, between which builds, what to do, and that nothing changed.
            Assert.Equal(string.Join("\n", e.Lines), e.Message);
            Assert.Equal(
                SessionIncompatibleException.Describe("2dc6af2", "2026-10-11", "win-x64", BuildInfo.Sha, BuildInfo.Date,
                    System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, 1),
                e.Lines);
            Assert.Equal("This saved session cannot be resumed with this build.", e.Lines[0]);
            Assert.Contains("recorded by build 2dc6af2 (2026-10-11, win-x64)", e.Message, StringComparison.Ordinal);
            Assert.Contains("this is build " + BuildInfo.Sha + " (" + BuildInfo.Date + ", ", e.Message, StringComparison.Ordinal);
            Assert.Contains("a different world from turn 1,", e.Message, StringComparison.Ordinal);
            Assert.Contains("start a new game", e.Message, StringComparison.Ordinal);
            Assert.Contains("The session files were not changed.", e.Lines);
            Assert.DoesNotContain("determinism defect", e.Message, StringComparison.Ordinal);   // another build's id
            Assert.DoesNotContain("resume diverged", e.Message, StringComparison.Ordinal);      // the jargon stays inside

            // Control: the very same files under this build's identity, the trace undoctored, resume.
            live.ExportTrace(UiSession.TracePath(log));
            RecordBuild(manifestPath, BuildInfo.Sha, BuildInfo.Date, System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);
            UiSession resumed = UiSession.Resume(manifestPath, out _);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Empty(resumed.ResumeNotice);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>M5 playtest blocker (D-052): the incompatibility message adds a line about what a divergence MEANS
    /// only when the recorded and current build ids are equal — a determinism defect to report if this build
    /// recorded it, unless it was recorded on another platform (ADR-022: a session replays exactly only where it was
    /// recorded), and saying so when the shared id is the unstamped "dev". Under another build's id there is no such
    /// line; the turn is written culture-invariantly.</summary>
    [Fact]
    public void TheIncompatibilityMessage_NamesADeterminismDefect_OnlyUnderTheSameBuildId_AndThePlatformWhenItDiffers()
    {
        IReadOnlyList<string> other = SessionIncompatibleException.Describe("2dc6af2", "2026-10-11", "win-x64", "abc1234", "2026-10-12", "win-x64", 12345);
        Assert.Equal(5, other.Count);
        Assert.Equal("Replaying its orders here produces a different world from turn 12345, so the saved game cannot be rebuilt.", other[2]);
        Assert.Equal("To continue it, run the build that recorded it. To play this build, start a new game.", other[3]);
        Assert.Equal("The session files were not changed.", other[4]);

        IReadOnlyList<string> same = SessionIncompatibleException.Describe("abc1234", "2026-10-12", "win-x64", "abc1234", "2026-10-12", "win-x64", 1);
        Assert.Equal(6, same.Count);
        Assert.Equal(other[0], same[0]);
        Assert.Equal("If this build recorded it, that is a determinism defect: please report it with the session folder.", same[5]);

        // A pre-v2 manifest records no platform: nothing to blame but the build.
        IReadOnlyList<string> unrecorded = SessionIncompatibleException.Describe(
            "abc1234", "2026-10-12", SessionManifest.PlatformNotRecorded, "abc1234", "2026-10-12", "linux-x64", 1);
        Assert.Equal(same[5], unrecorded[5]);

        // "dev" is every unstamped local build's id: the line does not let it pass for an identity.
        IReadOnlyList<string> local = SessionIncompatibleException.Describe("dev", "local", "linux-x64", "dev", "local", "linux-x64", 1);
        Assert.Equal(6, local.Count);
        Assert.StartsWith("Both are unstamped local builds (\"dev\"), so which build recorded it is not known;", local[5], StringComparison.Ordinal);
        Assert.Contains("determinism defect", local[5], StringComparison.Ordinal);

        IReadOnlyList<string> elsewhere = SessionIncompatibleException.Describe("abc1234", "2026-10-12", "win-x64", "abc1234", "2026-10-12", "linux-x64", 2);
        Assert.Equal(6, elsewhere.Count);
        Assert.Contains("the platform", elsewhere[5], StringComparison.Ordinal);
        Assert.Contains("ADR-022", elsewhere[5], StringComparison.Ordinal);
        Assert.DoesNotContain("determinism defect", string.Join("\n", elsewhere), StringComparison.Ordinal);

        // D-052 review nit: the two RIDs CR-013 §8.2 measured equal to the reference ("linux-x64" and
        // "ubuntu.24.04-x64", SessionManifest.IsReferencePlatform) are ONE platform for the replay promise, so a
        // same-build divergence between them is a determinism defect, not a platform one.
        IReadOnlyList<string> reference = SessionIncompatibleException.Describe(
            "abc1234", "2026-10-12", SessionManifest.MeasuredUbuntuRid, "abc1234", "2026-10-12", SessionManifest.ReferencePlatform, 3);
        Assert.Equal(same[5], reference[5]);
    }

    /// <summary>D-052 review finding: Program.cs hands <see cref="ResumeLaunch.NativeDialog"/> to the launch, and the
    /// process tests suppress the dialog through <see cref="ResumeLaunch.NoDialogVariable"/> — so pin the selector itself:
    /// with the variable unset (this test process never sets it; RunSimUi sets it only on child processes) it is the
    /// native dialog, and with it set it is null. The native call itself is not exercised here (it would block on a
    /// display) — its evidence is manual.</summary>
    [Fact]
    public void TheNativeDialogSelector_IsTheNativeDialog_UnlessSuppressed()
    {
        string? saved = Environment.GetEnvironmentVariable(ResumeLaunch.NoDialogVariable);
        try
        {
            Environment.SetEnvironmentVariable(ResumeLaunch.NoDialogVariable, null);
            Action<string, string>? live = ResumeLaunch.NativeDialog();
            Assert.NotNull(live);
            Assert.Equal(typeof(NativeErrorDialog), live!.Method.DeclaringType);
            Assert.Equal(nameof(NativeErrorDialog.Show), live.Method.Name);

            Environment.SetEnvironmentVariable(ResumeLaunch.NoDialogVariable, "1");
            Assert.Null(ResumeLaunch.NativeDialog());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ResumeLaunch.NoDialogVariable, saved);
        }
    }
}

/// <summary>S0 (D-008): the game OPENS a session resumed under another schema with the explicit notice — the toast,
/// under its own kicker — and a session resumed under its own schema with none. Through the live UI
/// (<see cref="Sim.Ui.Headless.UiFrameHarness"/>, a real ImGui frame), so the GameUi wiring is what is pinned.
/// Opening the UI never touches the world (hash before and after).</summary>
[Collection("ImGui context")]
public class ResumeNoticeUiTests
{
    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    /// <summary>A session recorded 2 turns into <paramref name="dir"/> (256 px, 4 settlements): flat when
    /// <paramref name="flat"/>, as every session recorded under a schema before v33 was, else the shipped cylinder.
    /// Returns the live session and its manifest path.</summary>
    private static (UiSession Live, string Manifest) Record(string dir, bool flat)
    {
        string log = Path.Combine(dir, "orders-20261010-120000-s256-n4.bin");
        UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, wrapXOverride: flat ? false : null);
        live.ExportManifest("2026-10-10 12:00:00", log);
        for (int t = 0; t < 2; t++) live.EndTurn();
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));
        return (live, UiSession.ManifestPath(log));
    }

    /// <summary>
    /// Final nits (verifier RC1-5, 2026-10-15): the older-schema half is recorded FLAT, like its sibling
    /// ChronicleAndResumeTests.Resume_OfASessionRecordedUnderAnOlderSchema… — every session recorded under v32 was flat,
    /// and Resume rebuilds a pre-v33 session flat (fix round 1). The rig recorded a CYLINDER session and relabelled it v32,
    /// a session no v32 build could have written; it passed only because it asserted the notice. MEASURED on fix round 2's
    /// tree with a world-identity assertion added to that rig: "Expected: 96f7adfa4cb7d662… Actual: d82b01914521b012…"
    /// (the live cylinder world vs the flat world Resume rebuilt). Now the resumed world is asserted to BE the recorded one
    /// as well as the notice; the own-schema half keeps a session this build recorded (the shipped cylinder).
    /// </summary>
    [Fact]
    public void AGameOpenedOnASessionResumedUnderAnotherSchema_ShowsTheNotice_AndOneUnderItsOwnSchemaDoesNot()
    {
        string dir = Path.Combine(Path.GetTempPath(), "s0-schema-notice-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            (UiSession live, string manifestPath) = Record(Path.Combine(dir, "older"), flat: true);

            // D-055 (2026-10-16) CAUSE BLOCK (X1 (h)): was `older = CanonicalSchema.Version - 1` (v32 while this build was
            // v33); the flat arm is the explicit 32 now, and the v33 (cylinder) arm is its own test below.
            int older = 32;
            ChronicleAndResumeTests.RecordSchema(manifestPath, older);
            ChronicleAndResumeTests.RecordTraceUnderAnotherSchema(UiSession.TracePath(Path.Combine(dir, "older", "orders-20261010-120000-s256-n4.bin")));
            UiSession resumed = UiSession.Resume(manifestPath, out _);
            // The world the session was played in, rebuilt — not only the notice.
            Assert.False(resumed.World.Terrain!.WrapX);
            Assert.Equal(live.World.Clock.Turn, resumed.World.Clock.Turn);
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            string before = WorldHash.ComputeHex(resumed.World);
            using (var h = Sim.Ui.Headless.UiFrameHarness.Start(resumed, Path.Combine(dir, "ui-older"), Assets()))
            {
                Assert.True(h.Ui.Notices.Visible);
                Assert.Equal(UiSession.ResumeNoticeKicker, h.Ui.Notices.Kicker);
                Assert.Equal(UiSession.SchemaMismatchNotice(older, CanonicalSchema.Version), h.Ui.Notices.Lines);
                Assert.Empty(h.Problems);
            }
            Assert.Equal(before, WorldHash.ComputeHex(resumed.World));

            // A session recorded by THIS build (the shipped cylinder) under its own schema and its own, undoctored trace:
            // resumed with the trace checked, into the world it was played in, and opened with no notice.
            (UiSession ownLive, string ownManifest) = Record(Path.Combine(dir, "own"), flat: false);
            UiSession own = UiSession.Resume(ownManifest, out _);
            Assert.Empty(own.ResumeNotice);
            Assert.True(own.World.Terrain!.WrapX);
            Assert.Equal(WorldHash.ComputeHex(ownLive.World), WorldHash.ComputeHex(own.World));
            using (var h = Sim.Ui.Headless.UiFrameHarness.Start(own, Path.Combine(dir, "ui-own"), Assets()))
            {
                Assert.False(h.Ui.Notices.Visible);
                Assert.Empty(h.Problems);
            }
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// D-055 (2026-10-16; X1 (h)) — THE v33 ARM: a session recorded under schema v33 (the batch-3 schema, the first
    /// cylinder one) and resumed by this v34 build. It is a CYLINDER rebuild (TopologyFlags.RecordedFlat(33) is false, so
    /// the shipped worldgen founds it, as it was founded), its notice's second line is the cylinder one ("Resumed by
    /// replaying its seed and orders (D-008).", not the flat-map line), and its trace is NOT checked: the trace's hashes
    /// are doctored (every one replaced) and the resume still succeeds. The session is recorded by this build on the
    /// cylinder and relabelled v33 — its world is this build's, so the resumed world equals the live one. (The real
    /// batch-3 session is Sim.Ui.Tests' V33SessionResumeTests.)
    /// </summary>
    [Fact]
    public void ASessionResumedUnderSchemaV33_IsACylinderRebuild_WithTheCylinderNoticeLine_AndItsTraceIsNotChecked()
    {
        string dir = Path.Combine(Path.GetTempPath(), "s0-schema-v33-"
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            (UiSession live, string manifestPath) = Record(Path.Combine(dir, "v33"), flat: false);
            Assert.True(live.World.Terrain!.WrapX);
            ChronicleAndResumeTests.RecordSchema(manifestPath, 33);
            ChronicleAndResumeTests.RecordTraceUnderAnotherSchema(UiSession.TracePath(Path.Combine(dir, "v33", "orders-20261010-120000-s256-n4.bin")));
            UiSession resumed = UiSession.Resume(manifestPath, out _);
            Assert.True(resumed.World.Terrain!.WrapX);                                   // a cylinder rebuild
            Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
            Assert.Equal(UiSession.SchemaMismatchNotice(33, CanonicalSchema.Version), resumed.ResumeNotice);
            Assert.Equal(4, resumed.ResumeNotice.Count);
            Assert.Equal("Recorded under save schema v33; this build is v34.", resumed.ResumeNotice[0]);
            Assert.Equal("Resumed by replaying its seed and orders (D-008).", resumed.ResumeNotice[1]);
            Assert.Equal("The saved trace was not checked against this replay.", resumed.ResumeNotice[2]);
            using (var h = Sim.Ui.Headless.UiFrameHarness.Start(resumed, Path.Combine(dir, "ui-v33"), Assets()))
            {
                Assert.True(h.Ui.Notices.Visible);
                Assert.Equal(UiSession.SchemaMismatchNotice(33, CanonicalSchema.Version), h.Ui.Notices.Lines);
                Assert.Empty(h.Problems);
            }
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}

/// <summary>Integration item 5 — what the text says is simulated is what is simulated.</summary>
public class TruthInTextTests
{
    [Fact]
    public void TheLenses_NoLongerDenyRoadsUniversitiesOrCrafting()
    {
        var s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        var content = s.Config.Research!;
        foreach (Sim.Ui.Progression.Lens lens in new[] { Sim.Ui.Progression.Lens.Infrastructure, Sim.Ui.Progression.Lens.Institutions, Sim.Ui.Progression.Lens.Industry })
        {
            Sim.Ui.Progression.LensPage page = Sim.Ui.Progression.Lenses.Page(lens, s.World, content, UiPlayer.Empire);
            Assert.DoesNotContain("construction of these is not yet simulated", page.StatusNote);
            Assert.DoesNotContain("No system produces industry state", page.StatusNote);
            Assert.DoesNotContain("Establishing an institution belongs to its owning system, not yet simulated", page.StatusNote);
        }
    }

    [Fact]
    public void TheWorldCaption_NamesTheTopSectorByItsKnowledgeLabel()
    {
        var s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int capital = s.World.Settlements[0].Id.Value;
        Assert.True(s.EmitSectorOrders([60, 10, 10, 10, 10], capital));
        s.EndTurn();
        Sim.Ui.World.WorldProjection p = Sim.Ui.World.WorldProjection.Build(s.World, s.Config, s.Names.Name, UiPlayer.Empire);
        Sim.Ui.World.SettlementLensView v = p.Settlements.Single(x => x.Id == capital);
        string expected = LabourActivities.For(s.World, s.Config, UiPlayer.Empire)
            .Single(a => a.Settlement.Value == capital && a.Sector == Sectors.Farming).Label;
        Assert.Equal(expected, v.TopSectorLabel);
    }
}
