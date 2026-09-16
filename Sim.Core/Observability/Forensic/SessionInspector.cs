using System.Globalization;
using Sim.Core.Kernel;

namespace Sim.Core.Observability.Forensic;

/// <summary>
/// ONE ANSWER, WITH ITS EVIDENCE TAG ATTACHED.
///
/// The tag is not decoration and not a convention a caller may forget: it is a
/// field of the answer, so nothing in this layer can print a value without
/// printing how that value was obtained. <see cref="Basis"/> is the rule — the
/// table it was READ from, the identity it is a RESIDUAL of, or the reason it is
/// NOT RECORDED — stated so a reviewer can check it rather than trust it.
/// </summary>
public sealed record Answer(string Topic, Evidence Evidence, string Summary, string Basis, string[] Lines)
{
    public Answer(string topic, Evidence evidence, string summary, string basis)
        : this(topic, evidence, summary, basis, []) { }

    /// <summary>The one-line header a CLI prints, tag first.</summary>
    public string Header =>
        "[" + EvidenceTag.Of(Evidence) + "] " + Topic + ": " + Summary;
}

/// <summary>
/// HEADLESS INSPECTION, ANSWERED FROM THE SAVED RECORD.
///
/// Every query here reads artifacts on disk — the telemetry JSONL, the trace
/// CSV, the forensic record, the manifest — and NEVER re-runs the simulation.
/// That is the point: a reviewer's question must be answered by the evidence the
/// session left behind, not by a fresh run that could differ from it. Where the
/// record cannot answer, the answer is the limitation, tagged
/// <see cref="Evidence.NotRecorded"/>, and nothing in this class promotes that
/// tag to <see cref="Evidence.Derivable"/>.
///
/// WHY IT LIVES IN Sim.Core/Observability. `scripts/check-read-isolation.sh`
/// greps Sim.Cli as well as Sim.Core for the needs/grievance table names and
/// matches PROSE as well as code, allowlisting `Sim.Core/Observability/` by path
/// prefix. The reader and every query primitive therefore live here, and Sim.Cli
/// stays a thin flag-parsing shell.
/// </summary>
public sealed class SessionInspector
{
    private readonly SessionManifest _manifest;
    private readonly TelemetryRecordFile? _telemetry;
    private readonly string _telemetrySource;
    private readonly IReadOnlyList<SessionTrace.Row> _trace;
    private readonly ForensicRecordFile? _forensic;

    public SessionInspector(
        SessionManifest manifest,
        TelemetryRecordFile? telemetry,
        string telemetrySource,
        IReadOnlyList<SessionTrace.Row> trace,
        ForensicRecordFile? forensic)
    {
        _manifest = manifest;
        _telemetry = telemetry;
        _telemetrySource = telemetrySource;
        _trace = trace;
        _forensic = forensic;
    }

    /// <summary>Loads whatever of the set is on disk beside the manifest. A file
    /// that is missing is ABSENT, never invented: the queries that needed it say
    /// so instead of answering from something else.</summary>
    public static SessionInspector Open(string manifestPath, TelemetryRecordFile? reconstructed = null)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";
        SessionManifest manifest;
        using (FileStream file = File.OpenRead(manifestPath)) manifest = SessionManifest.Read(file, manifestPath);

        string telemetryPath = Path.Combine(dir, manifest.TelemetryFile);
        TelemetryRecordFile? telemetry = reconstructed;
        string source = reconstructed is null ? "" : "RECONSTRUCTED BY REPLAY — the saved record did not cover it";
        if (telemetry is null && manifest.TelemetryFile.Length > 0 && File.Exists(telemetryPath))
        {
            telemetry = TelemetryRecordFile.Read(telemetryPath);
            source = manifest.TelemetryFile;
        }

        string tracePath = Path.Combine(dir, manifest.TraceFile);
        IReadOnlyList<SessionTrace.Row> trace = File.Exists(tracePath)
            ? SessionTrace.Parse(File.ReadLines(tracePath), tracePath)
            : [];

        string forensicPath = Path.Combine(dir, manifest.ForensicFile);
        ForensicRecordFile? forensic = manifest.ForensicFile.Length > 0 && File.Exists(forensicPath)
            ? ForensicRecordFile.Read(forensicPath)
            : null;

        return new SessionInspector(manifest, telemetry, source, trace, forensic);
    }

    /// <summary>The topics `--answer` accepts, in print order.</summary>
    public static string[] Topics =>
    [
        "world", "settlements", "polities", "movements", "resources",
        "happiness", "migration", "artisan", "events", "hashes", "limits",
    ];

    /// <summary>Answers one topic, or every topic for "all".</summary>
    public Answer[] Answer(string topic, int? settlement)
    {
        if (topic == "all")
        {
            var all = new List<Answer>();
            foreach (string t in Topics) all.AddRange(Answer(t, settlement));
            return [.. all];
        }
        return topic switch
        {
            "world" => [WorldSummary()],
            "settlements" => [SettlementHistory(settlement)],
            "polities" => [PolityHistory()],
            "movements" => PopulationMovements(settlement),
            "resources" => ResourceFlows(settlement),
            "happiness" => HappinessHistory(settlement),
            "migration" => MigrationHistory(settlement),
            "artisan" => ArtisanActivation(settlement),
            "events" => [MajorEvents()],
            "hashes" => StateHashes(),
            "limits" => [Limits()],
            _ => throw new ArgumentException(
                $"unknown topic '{topic}' — one of: {string.Join(", ", Topics)}, all", nameof(topic)),
        };
    }

    private Answer NoTelemetry(string topic) => new(
        topic, Evidence.NotRecorded,
        "no telemetry record for this session",
        $"the manifest names '{_manifest.TelemetryFile}' and it is not beside the manifest. This is NOT "
        + "answered from a fresh simulation run: a question about what happened is answered by the evidence "
        + "the session left behind, or it is not answered at all.");

    // -------------------------------------------------------------- world --

    public Answer WorldSummary()
    {
        var lines = new List<string>
        {
            $"seed                  {_manifest.Seed.ToString(CultureInfo.InvariantCulture)}   [KNOWN — manifest]",
            $"sizePx                {(_manifest.SizePx is { } px ? px.ToString(CultureInfo.InvariantCulture) : "default (not overridden)")}   [KNOWN — manifest]",
            $"settlements override  {(_manifest.Settlements is { } n ? n.ToString(CultureInfo.InvariantCulture) : "default (not overridden)")}   [KNOWN — manifest]",
            $"canonical schema      v{_manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture)}   [KNOWN — manifest]",
            $"build                 {_manifest.BuildSha} ({_manifest.BuildDate})   [KNOWN — manifest]",
            $"platform played on    {_manifest.Platform}   [KNOWN — manifest]",
        };

        if (_forensic is { } f)
        {
            lines.Add($"run id                {f.RunId}   [KNOWN — forensic run record, content-derived]");
            lines.Add($"config digest         {f.Run.ConfigDigest}   [KNOWN — forensic run record]");
            lines.Add($"config resources      {f.Run.Config.Length.ToString(CultureInfo.InvariantCulture)} embedded files, each by sha256   [KNOWN]");
            lines.Add($"pipeline              {f.Run.Pipeline.Length.ToString(CultureInfo.InvariantCulture)} systems, in order   [KNOWN — forensic run record]");
            lines.Add($"hash algorithm        {f.Run.HashAlgorithm}; self-identifies its schema: {(f.Run.HashCoversSchemaVersion ? "yes" : "NO")}   [KNOWN]");
            lines.Add($"terrain content hash  {f.Run.TerrainContentHash ?? "null — " + f.Run.TerrainState}   [{(f.Run.TerrainContentHash is null ? "NOT RECORDED" : "KNOWN")}]");
            lines.Add($"ai empires configured {f.Run.AiEmpiresConfigured.ToString(CultureInfo.InvariantCulture)}   [KNOWN — forensic run record]");
            lines.Add(f.Close is { } c
                ? $"closed cleanly        yes, at turn {c.TurnsReached.ToString(CultureInfo.InvariantCulture)}, final hash {Short(c.FinalWorldHash)}   [KNOWN — forensic close record]"
                : "closed cleanly        NO — the forensic record has no close line   [KNOWN by ABSENCE]");
        }
        else
        {
            lines.Add("run id                null — this session wrote no forensic record   [NOT RECORDED]");
        }

        if (_telemetry is { } t)
        {
            lines.Add($"turns observed        {t.Turns.Count.ToString(CultureInfo.InvariantCulture)}   [KNOWN — {_telemetrySource}]");
            lines.Add($"settlements seen      {t.SettlementIds().Length.ToString(CultureInfo.InvariantCulture)}   [KNOWN — {_telemetrySource}]");
        }
        lines.Add($"trace rows            {_trace.Count.ToString(CultureInfo.InvariantCulture)} (turn 0 included)   [KNOWN — {_manifest.TraceFile}]");

        return new Answer("world summary", Evidence.Known,
            $"seed {_manifest.Seed.ToString(CultureInfo.InvariantCulture)}, schema v{_manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture)}, "
                + $"{_trace.Count.ToString(CultureInfo.InvariantCulture)} trace rows",
            "READ from the manifest, the forensic run/close records and the trace — no simulation was run.",
            [.. lines]);
    }

    // -------------------------------------------------------- settlements --

    public Answer SettlementHistory(int? settlement)
    {
        if (_telemetry is not { } t) return NoTelemetry("settlement history");

        var lines = new List<string>
        {
            "  turn        pop   births   deaths   inflow  outflow    grain  dwellings  controller",
        };
        foreach (TelemetryTurn turn in t.Turns)
        {
            for (int i = 0; i < turn.Settlements.Length; i++)
            {
                TelemetrySettlement s = turn.Settlements[i];
                if (settlement is { } want && s.Settlement != want) continue;
                lines.Add(
                    Num(turn.Turn, 6) + Num(s.PopClosing, 11) + Num(s.Births, 9) + Num(s.Deaths, 9)
                    + Num(s.Inflow, 9) + Num(s.Outflow, 9) + Num(s.GrainClosing, 9)
                    + (s.HousingHasRow ? Num(s.DwellingsClosing, 11) : "        n/a")
                    + (s.Controller >= 0 ? Num(s.Controller, 12) : "     none")
                    + (settlement is null ? "   settlement " + s.Settlement.ToString(CultureInfo.InvariantCulture) : ""));
            }
        }

        return new Answer("settlement history", Evidence.Known,
            settlement is { } id
                ? $"settlement {id.ToString(CultureInfo.InvariantCulture)} across {t.Turns.Count.ToString(CultureInfo.InvariantCulture)} observed turns"
                : $"every settlement across {t.Turns.Count.ToString(CultureInfo.InvariantCulture)} observed turns",
            $"READ per (turn, settlement) from {_telemetrySource}: population, vitals, migration flows, grain "
                + "stock, dwellings and controller are all fields the observer wrote from the owning systems' "
                + "own rows. A dwellings column of 'n/a' is housing.hasRow=false — the settlement has no "
                + "HousingRow yet — and NOT a dwelling count of zero.",
            [.. lines]);
    }

    // ------------------------------------------------------------ polity --

    public Answer PolityHistory()
    {
        if (_telemetry is not { } t) return NoTelemetry("polity history");

        var lines = new List<string>();
        long controlChanges = 0;
        foreach (TelemetryTurn turn in t.Turns)
        {
            if (turn.Flows.ControlLost > 0)
            {
                controlChanges += turn.Flows.ControlLost;
                lines.Add($"turn {Num(turn.Turn, 6).Trim()}: {turn.Flows.ControlLost.ToString(CultureInfo.InvariantCulture)} control row(s) LOST "
                    + "[KNOWN — turn.flows.controlLost, DIFFERENCED by the observer from prev/next ControlRows]");
            }
        }

        // Which settlement each controller held, per turn, is READ per record.
        TelemetryTurn last = t.Turns[^1];
        for (int i = 0; i < last.Settlements.Length; i++)
        {
            TelemetrySettlement s = last.Settlements[i];
            lines.Add($"final turn {last.Turn.ToString(CultureInfo.InvariantCulture)}: settlement "
                + $"{s.Settlement.ToString(CultureInfo.InvariantCulture)} controlled by polity "
                + (s.Controller >= 0 ? s.Controller.ToString(CultureInfo.InvariantCulture) : "NONE")
                + "   [KNOWN — settlements[].controller]");
        }

        lines.Add("");
        lines.Add("WHY a control change happened is NOT RECORDED: the FACT of the change is a clean difference "
            + "of ControlRows, but no row carries a reason or a category for it. Closing that needs new "
            + "authoritative state, not an observer.");

        return new Answer("polity history", Evidence.Known,
            $"{controlChanges.ToString(CultureInfo.InvariantCulture)} control-row loss(es) across the session",
            $"READ from {_telemetrySource}: settlements[].controller per turn (a READ of ControlRow.Polity on "
                + "next, -1 when none) and turn.flows.controlLost (DIFFERENCED). The REASON for a change is not "
                + "recorded by this build.",
            [.. lines]);
    }

    // --------------------------------------------------------- movements --

    public Answer[] PopulationMovements(int? settlement)
    {
        if (_telemetry is not { } t) return [NoTelemetry("population movements")];

        var lines = new List<string> { "  turn   settlement    inflow   outflow   births   deaths   colonistsDeparted" };
        long inflow = 0, outflow = 0;
        string identity = "";
        foreach (TelemetryTurn turn in t.Turns)
        {
            for (int i = 0; i < turn.Settlements.Length; i++)
            {
                TelemetrySettlement s = turn.Settlements[i];
                if (settlement is { } want && s.Settlement != want) continue;
                if (s.Inflow == 0 && s.Outflow == 0 && s.ColonistsDeparted == 0) continue;
                inflow += s.Inflow;
                outflow += s.Outflow;
                if (identity.Length == 0) identity = s.ColonistsDepartedIdentity;
                lines.Add(Num(turn.Turn, 6) + Num(s.Settlement, 13) + Num(s.Inflow, 10) + Num(s.Outflow, 10)
                    + Num(s.Births, 9) + Num(s.Deaths, 9) + Num(s.ColonistsDeparted, 20));
            }
        }

        var aggregate = new Answer(
            "population movements (aggregate)", Evidence.Known,
            $"inflow {inflow.ToString(CultureInfo.InvariantCulture)}, outflow {outflow.ToString(CultureInfo.InvariantCulture)}, "
                + "per settlement per turn",
            $"READ from {_telemetrySource}: MigrationFlowRow.Inflow and .Outflow as the migration system wrote "
                + "them. These are AGGREGATES — netted across every partner, every cohort, every class and both "
                + "migration channels. ColonistsDeparted is a RESIDUAL of a stated identity: "
                + (identity.Length > 0 ? identity : "(no colony movement in this session)"),
            [.. lines]);

        // THE LIMITATION, printed beside the aggregate every time it is asked
        // for — so a reader can never take the aggregate for the pairwise fact.
        var pairwise = new Answer(
            "population movements (pairwise destination)", Evidence.NotRecorded,
            ForensicSchema.MigrationPairwiseAnswer,
            ForensicSchema.MigrationPairwiseWhy,
            [
                "This layer does NOT infer destinations from attractiveness.",
                "This layer does NOT rank destinations as historical fact.",
                "This layer does NOT reconstruct the split probabilistically.",
                "This layer does NOT add a synthetic category to stand in for it.",
                "Aggregate inflow beside aggregate outflow is NOT proof of pairwise movement, and is not",
                "presented as any.",
            ]);

        return [aggregate, pairwise];
    }

    // --------------------------------------------------------- resources --

    public Answer[] ResourceFlows(int? settlement)
    {
        if (_telemetry is not { } t) return [NoTelemetry("resource flows")];

        var world = new List<string>
        {
            "  turn   opening   harvest     eaten  spoilage  overflow   closing  reconciles",
        };
        long harvest = 0, eaten = 0, spoilage = 0, overflow = 0;
        bool allReconcile = true;
        foreach (TelemetryTurn turn in t.Turns)
        {
            GrainTotals g = turn.Grain;
            harvest += g.Harvest; eaten += g.Eaten; spoilage += g.Spoilage; overflow += g.Overflow;
            allReconcile &= g.Reconciles;
            world.Add(Num(turn.Turn, 6) + Num(g.Opening, 10) + Num(g.Harvest, 10) + Num(g.Eaten, 10)
                + Num(g.Spoilage, 10) + Num(g.Overflow, 10) + Num(g.Closing, 10)
                + (g.Reconciles ? "        yes" : "   NO (" + g.Discrepancy.ToString(CultureInfo.InvariantCulture) + ")"));
        }

        var worldAnswer = new Answer(
            "resource flows (world level)", Evidence.Known,
            $"grain: harvest {harvest.ToString(CultureInfo.InvariantCulture)}, eaten {eaten.ToString(CultureInfo.InvariantCulture)}, "
                + $"spoilage {spoilage.ToString(CultureInfo.InvariantCulture)}, overflow {overflow.ToString(CultureInfo.InvariantCulture)}"
                + (allReconcile ? " — every turn reconciles EXACTLY" : " — AT LEAST ONE TURN DOES NOT RECONCILE"),
            $"READ from {_telemetrySource}: the world grain account, whose every term the observer DIFFERENCED "
                + "from the ledger's own cumulative rows. The identity Opening + Endowment + Harvest - Eaten - "
                + "Spoilage - Overflow == Closing is checked as an EXACT long equality, never an approximate one; "
                + "a turn that does not reconcile is a simulation defect and is printed as one. At WORLD level "
                + "the split into spoilage and overflow IS recorded, by ledger reason.",
            [.. world]);

        var perSettlement = new List<string>
        {
            "  turn   settlement   opening   harvest     eaten   closing   storeLosses",
        };
        string identity = "";
        long losses = 0;
        foreach (TelemetryTurn turn in t.Turns)
        {
            for (int i = 0; i < turn.Settlements.Length; i++)
            {
                TelemetrySettlement s = turn.Settlements[i];
                if (settlement is { } want && s.Settlement != want) continue;
                losses += s.StoreLosses;
                if (identity.Length == 0) identity = s.StoreLossesIdentity;
                perSettlement.Add(Num(turn.Turn, 6) + Num(s.Settlement, 13) + Num(s.GrainOpening, 10)
                    + Num(s.Harvest, 10) + Num(s.Eaten, 10) + Num(s.GrainClosing, 10) + Num(s.StoreLosses, 14));
            }
        }

        var settlementAnswer = new Answer(
            "resource flows (per settlement)", Evidence.Derivable,
            $"StoreLosses total {losses.ToString(CultureInfo.InvariantCulture)} — ONE residual, FOUR mechanisms",
            "There is no settlement dimension on the ledger, so the per-settlement loss is a RESIDUAL of a "
                + "stated identity, printed here verbatim as the record carries it: "
                + (identity.Length > 0 ? identity : "(no settlement records in this session)")
                + "  ||  " + ForensicSchema.StoreLossesWhy,
            [.. perSettlement]);

        return [worldAnswer, settlementAnswer];
    }

    // --------------------------------------------------------- happiness --

    public Answer[] HappinessHistory(int? settlement)
    {
        if (_telemetry is not { } t) return [NoTelemetry("happiness history")];

        var lines = new List<string> { "  turn   settlement   happiness   factor[Food]   factor[Housing]   branch labels" };
        foreach (TelemetryTurn turn in t.Turns)
        {
            for (int i = 0; i < turn.Settlements.Length; i++)
            {
                TelemetrySettlement s = turn.Settlements[i];
                if (settlement is { } want && s.Settlement != want) continue;
                double food = s.HappinessFactors.Length > 0 ? s.HappinessFactors[0] : double.NaN;
                double housing = s.HappinessFactors.Length > 1 ? s.HappinessFactors[1] : double.NaN;
                lines.Add(Num(turn.Turn, 6) + Num(s.Settlement, 13) + Dec(s.Happiness, 12)
                    + Dec(food, 15) + Dec(housing, 18) + "   " + BranchLabels(s));
            }
        }

        var value = new Answer(
            "happiness history (authoritative value)", Evidence.Known,
            settlement is { } id
                ? $"settlement {id.ToString(CultureInfo.InvariantCulture)}: final value "
                    + Final(t, id, s => s.Happiness)
                : "every settlement, per turn",
            $"READ from {_telemetrySource}. The stored value is itself a RECOMPUTED reading — the observer "
                + "called the PUBLIC SettlementHappiness.Of on the post-step world, the same reader the "
                + "migration system asks — so it is the authoritative number and not a copy of the formula. "
                + "The two factor values are the PUBLIC SettlementHappiness.Factors, in Factor order "
                + "[Food, Housing].",
            [.. lines]);

        var decomposition = new Answer(
            "happiness decomposition (raw factor / normalised / weight / contribution / aggregate)",
            Evidence.NotRecorded,
            "not available from this build without changing its architecture",
            ForensicSchema.HappinessDecompositionWhy,
            [
                "What IS available, and is printed above: the authoritative value (SettlementHappiness.Of),",
                "the two factor values (SettlementHappiness.Factors), and the branch labels that follow from",
                "which ROWS ARE PRESENT.",
                "",
                "\"Why was happiness 100?\" is therefore answered exactly as far as the simulation exposes it,",
                "and no further. No explanation is manufactured to fill the gap.",
            ]);

        return [value, decomposition];
    }

    /// <summary>
    /// THE BRANCH LABELS — and only the ones ROW PRESENCE actually determines.
    ///
    /// Housing is honest: the record carries housing.hasRow, so "no housing row"
    /// and "a housing row reading zero" are distinguishable. Food is NOT: the
    /// observer writes deficitRatio 0.0 when the ConsumptionDeficitRow is ABSENT
    /// and carries no presence flag, so a zero is ambiguous between the two, and
    /// this layer says AMBIGUOUS rather than picking one. Fixing that flag is an
    /// artifact-contract change to the shipped telemetry, not an inspection.
    /// </summary>
    private static string BranchLabels(TelemetrySettlement s)
    {
        string housing = s.HousingHasRow
            ? "housing=ROW PRESENT [KNOWN]"
            : "housing=NO ROW [KNOWN — housing.hasRow=false]";
        string food = s.DeficitRatio == 0.0 && s.DemandUnits == 0
            ? "food=AMBIGUOUS ZERO [NOT RECORDED — no deficit-row presence flag: 'no row' and 'a row reading zero' are the same bytes]"
            : "food=DEFICIT ROW PRESENT [DERIVABLE — a non-zero deficitRatio or demandUnits can only come from a row]";
        return food + "; " + housing;
    }

    // --------------------------------------------------------- migration --

    public Answer[] MigrationHistory(int? settlement)
    {
        if (_telemetry is not { } t) return [NoTelemetry("migration history")];

        var lines = new List<string> { "  turn   migrantsMoved   founded   unattributedGrainTransfer" };
        long moved = 0;
        foreach (TelemetryTurn turn in t.Turns)
        {
            if (turn.Flows.MigrantsMoved == 0 && turn.Flows.SettlementsFounded == 0) continue;
            moved += turn.Flows.MigrantsMoved;
            lines.Add(Num(turn.Turn, 6) + Num(turn.Flows.MigrantsMoved, 16) + Num(turn.Flows.SettlementsFounded, 10)
                + (turn.Flows.UnattributedGrainTransfer ? "   yes" : "   no"));
        }

        Answer[] movements = PopulationMovements(settlement);
        var summary = new Answer(
            "migration history", Evidence.Known,
            $"{moved.ToString(CultureInfo.InvariantCulture)} migrants moved across the session (world total)",
            $"READ from {_telemetrySource}: turn.flows.migrantsMoved is a SUM of next.MigrationFlows inflow — "
                + "the movement the system actually performed. The per-settlement halves are printed beside it.",
            [.. lines]);

        return [summary, .. movements];
    }

    // ----------------------------------------------------------- artisan --

    public Answer[] ArtisanActivation(int? settlement)
    {
        if (_telemetry is not { } t) return [NoTelemetry("artisan activation history")];

        var latch = new List<string>();
        var presence = new List<string>();
        int[] ids = settlement is { } want ? [want] : t.SettlementIds();

        for (int k = 0; k < ids.Length; k++)
        {
            int id = ids[k];
            bool latched = false, present = false;
            foreach (TelemetryTurn turn in t.Turns)
            {
                TelemetrySettlement? row = Find(turn, id);
                if (row is null) continue;

                if (!latched)
                {
                    for (int i = 0; i < row.ClassActive.Length; i++)
                    {
                        if (!IsArtisan(row.ClassActive[i].Name) || row.ClassActive[i].Active == 0) continue;
                        latch.Add($"settlement {id.ToString(CultureInfo.InvariantCulture)}: ACTIVATED on turn "
                            + turn.Turn.ToString(CultureInfo.InvariantCulture)
                            + "  [KNOWN — economy.classActive, a READ of the ClassStateRow latch]");
                        latched = true;
                        break;
                    }
                }
                if (!present)
                {
                    for (int i = 0; i < row.ClassCounts.Length; i++)
                    {
                        if (!IsArtisan(row.ClassCounts[i].Name) || row.ClassCounts[i].Count <= 0) continue;
                        presence.Add($"settlement {id.ToString(CultureInfo.InvariantCulture)}: FIRST PRESENT on turn "
                            + turn.Turn.ToString(CultureInfo.InvariantCulture) + " with "
                            + row.ClassCounts[i].Count.ToString(CultureInfo.InvariantCulture) + " people"
                            + "  [DERIVABLE — first turn population.classCounts for the class exceeds zero]");
                        present = true;
                        break;
                    }
                }
                if (latched && present) break;
            }
            if (!latched) latch.Add($"settlement {id.ToString(CultureInfo.InvariantCulture)}: never activated in the observed turns  [KNOWN by absence]");
            if (!present) presence.Add($"settlement {id.ToString(CultureInfo.InvariantCulture)}: never present in the observed turns  [KNOWN by absence]");
        }

        return
        [
            new Answer("artisan activation (latch turn)", Evidence.Known,
                $"{latch.Count.ToString(CultureInfo.InvariantCulture)} settlement(s) examined",
                $"READ from {_telemetrySource}: economy.classActive[].active is a READ of the class's own "
                    + "ClassStateRow — the LATCH the simulation itself set. This is the activation fact, not an "
                    + "inference from headcount.",
                [.. latch]),
            new Answer("artisan first presence (headcount)", Evidence.Derivable,
                $"{presence.Count.ToString(CultureInfo.InvariantCulture)} settlement(s) examined",
                "DERIVED by scanning population.classCounts, which is a SUM of the settlement's bucket rows for "
                    + "that class, for the first turn it exceeds zero. PRESENCE IS NOT ACTIVATION: the latch above "
                    + "is the authoritative fact and this is a different question about the same class.",
                [.. presence]),
        ];
    }

    private static bool IsArtisan(string name)
        => name.Contains("artisan", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------ events --

    public Answer MajorEvents()
    {
        if (_telemetry is not { } t) return NoTelemetry("major events");

        var lines = new List<string>();
        var deficit = new Dictionary<int, bool>();
        var seen = new List<int>();

        foreach (TelemetryTurn turn in t.Turns)
        {
            for (int i = 0; i < turn.Settlements.Length; i++)
            {
                TelemetrySettlement s = turn.Settlements[i];
                if (!seen.Contains(s.Settlement)) seen.Add(s.Settlement);

                if (s.Founded)
                {
                    lines.Add(Event(turn.Turn, "SETTLEMENT FOUNDED", s.Settlement,
                        $"first appears in this step; foundedTurn {s.FoundedTurn.ToString(CultureInfo.InvariantCulture)}",
                        "KNOWN — settlements[].founded"));
                }

                bool inDeficit = s.DeficitRatio > 0.0;
                if (deficit.TryGetValue(s.Settlement, out bool was))
                {
                    if (!was && inDeficit)
                        lines.Add(Event(turn.Turn, "FOOD SHORTFALL", s.Settlement,
                            $"deficitRatio rose to {s.DeficitRatio.ToString("0.####", CultureInfo.InvariantCulture)}",
                            "DERIVABLE — a transition of the READ deficitRatio across zero"));
                    else if (was && !inDeficit)
                        lines.Add(Event(turn.Turn, "FOOD RECOVERY", s.Settlement,
                            "deficitRatio returned to zero",
                            "DERIVABLE — a transition of the READ deficitRatio across zero; note that a zero "
                                + "deficitRatio is AMBIGUOUS between 'no row' and 'a row reading zero'"));
                }
                deficit[s.Settlement] = inDeficit;

                if (s.Inflow > 0 || s.Outflow > 0)
                {
                    lines.Add(Event(turn.Turn, "MIGRATION", s.Settlement,
                        $"inflow {s.Inflow.ToString(CultureInfo.InvariantCulture)}, outflow {s.Outflow.ToString(CultureInfo.InvariantCulture)} "
                            + "(AGGREGATE — no destination is recorded)",
                        "KNOWN — MigrationFlowRow inflow/outflow"));
                }

                for (int c = 0; c < s.ClassActive.Length; c++)
                {
                    if (!IsArtisan(s.ClassActive[c].Name) || s.ClassActive[c].Active == 0) continue;
                    if (turn.Turn == FirstLatchTurn(t, s.Settlement))
                        lines.Add(Event(turn.Turn, "ARTISAN ACTIVATED", s.Settlement,
                            "class latch set", "KNOWN — economy.classActive"));
                }
            }

            if (turn.Flows.ControlLost > 0)
            {
                lines.Add(Event(turn.Turn, "CONTROL CHANGED", -1,
                    $"{turn.Flows.ControlLost.ToString(CultureInfo.InvariantCulture)} control row(s) lost; WHICH settlement and WHY are not recorded",
                    "KNOWN — turn.flows.controlLost (a count only)"));
            }
        }

        return new Answer("major events", Evidence.Derivable,
            $"{lines.Count.ToString(CultureInfo.InvariantCulture)} event(s) derivable from the record",
            "Each event is a READ field or a transition of one, and carries its own tag. NO EVENT IS EMITTED "
                + "FOR A TRANSITION THE RECORD DOES NOT SHOW: there is no forced displacement, no war and no "
                + "battle in this build, so those categories are absent because they do not exist, not because "
                + "they went unobserved.",
            [.. lines]);
    }

    private static long FirstLatchTurn(TelemetryRecordFile t, int settlement)
    {
        foreach (TelemetryTurn turn in t.Turns)
        {
            TelemetrySettlement? row = Find(turn, settlement);
            if (row is null) continue;
            for (int i = 0; i < row.ClassActive.Length; i++)
                if (IsArtisan(row.ClassActive[i].Name) && row.ClassActive[i].Active != 0) return turn.Turn;
        }
        return -1;
    }

    private static string Event(long turn, string kind, int settlement, string detail, string tag) =>
        "turn " + turn.ToString(CultureInfo.InvariantCulture).PadLeft(5) + "  " + kind.PadRight(20)
        + (settlement >= 0 ? "settlement " + settlement.ToString(CultureInfo.InvariantCulture) : "(world)").PadRight(16)
        + detail + "   [" + tag + "]";

    // ------------------------------------------------------------ hashes --

    public Answer[] StateHashes()
    {
        if (_trace.Count == 0)
        {
            return [new Answer("state hashes", Evidence.NotRecorded,
                "no trace beside the manifest",
                $"the manifest names '{_manifest.TraceFile}' and it is not there; the per-turn hashes live only "
                    + "in the trace, so without it the session's state identity is unrecoverable from the set.")];
        }

        var lines = new List<string> { "  turn   hash BEFORE the step            hash AFTER the step" };
        for (int i = 0; i < _trace.Count; i++)
        {
            string after = _trace[i].Hash;
            string before = i == 0 ? "(no prior turn — this IS the founding state)" : Short(_trace[i - 1].Hash);
            lines.Add(Num(_trace[i].Turn, 6) + "   " + before.PadRight(32) + Short(after));
        }

        var post = new Answer("state hashes (post-turn)", Evidence.Known,
            $"{_trace.Count.ToString(CultureInfo.InvariantCulture)} hashes, turn 0 included",
            $"READ from {_manifest.TraceFile}: each line ends in the world hash the session's own machine "
                + "computed for that turn. The algorithm is sha256 over the canonical stream, and the stream "
                + "BEGINS AT THE SEED — so a world hash does NOT self-identify the schema version that produced "
                + "it. The schema version is carried beside it, on the manifest and the forensic run record.",
            [.. lines]);

        var pre = new Answer("state hashes (pre-turn)", Evidence.Derivable,
            "pre(N) = post(N-1) for every N > 0; pre(0) does not exist",
            "ONLY THE POST-TURN HASH IS RECORDED. The trace writes one line per turn AFTER the step, so a "
                + "pre-turn identity is the previous turn's post-turn hash and is tagged DERIVABLE, never KNOWN. "
                + "The chain is self-verifying: any turn whose pre-hash does not equal the previous post-hash "
                + "would mean a line is missing from the trace.",
            []);

        return [post, pre];
    }

    // ------------------------------------------------------------ limits --

    public Answer Limits()
    {
        if (_forensic is not { } f)
        {
            return new Answer("limitations", Evidence.NotRecorded,
                "this session wrote no forensic record, so it carries no limitation catalogue",
                "A session recorded before this packet has no forensic file. The limitations still apply to it; "
                    + "they are simply not carried in its own artifact.");
        }

        var lines = new List<string>();
        for (int i = 0; i < f.Run.Limitations.Length; i++)
        {
            ForensicLimitation l = f.Run.Limitations[i];
            lines.Add("");
            lines.Add(l.Token);
            lines.Add("  WHAT: " + l.What);
            lines.Add("  WHY : " + l.Why);
            if (l.Answer is { } a) lines.Add("  THE ANSWER TO ANY SUCH QUERY IS, LITERALLY: " + a);
        }

        return new Answer("limitations", Evidence.Known,
            $"{f.Run.Limitations.Length.ToString(CultureInfo.InvariantCulture)} stated limitation(s) — what this evidence CANNOT establish",
            "READ from the forensic run record's own catalogue. A reviewer's first question is what the record "
                + "is unable to tell him; before this it could be answered only by discovering silence.",
            [.. lines]);
    }

    // ------------------------------------------------------------ pieces --

    private static TelemetrySettlement? Find(TelemetryTurn turn, int settlement)
    {
        for (int i = 0; i < turn.Settlements.Length; i++)
            if (turn.Settlements[i].Settlement == settlement) return turn.Settlements[i];
        return null;
    }

    private static string Final(TelemetryRecordFile t, int settlement, Func<TelemetrySettlement, double> pick)
    {
        for (int i = t.Turns.Count - 1; i >= 0; i--)
        {
            TelemetrySettlement? row = Find(t.Turns[i], settlement);
            if (row is not null) return pick(row).ToString("0.####", CultureInfo.InvariantCulture)
                + " on turn " + t.Turns[i].Turn.ToString(CultureInfo.InvariantCulture);
        }
        return "no record";
    }

    private static string Num(long value, int width)
        => value.ToString(CultureInfo.InvariantCulture).PadLeft(width);

    private static string Dec(double value, int width)
        => (double.IsFinite(value) ? value.ToString("0.####", CultureInfo.InvariantCulture) : "n/a").PadLeft(width);

    private static string Short(string? hash)
        => hash is { Length: > 12 } ? hash[..12] + "…" : hash ?? "null";
}
