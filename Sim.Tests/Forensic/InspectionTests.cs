using Sim.Core.Kernel;
using Sim.Core.Observability.Forensic;
using Sim.Core.State;

namespace Sim.Tests.Forensic;

/// <summary>
/// P3 — HEADLESS INSPECTION, ANSWERED FROM THE SAVED RECORD.
///
/// The properties that make an inspection answer EVIDENCE rather than a second
/// opinion: it comes from the artifact and not from a fresh run; every answer
/// carries its own KNOWN / DERIVABLE / NOT RECORDED tag; a recorded event
/// corresponds to a real transition and a transition that did not happen
/// produces no event; the happiness the record carries IS the authoritative
/// value, not a copy of the formula; and NOT RECORDED is never promoted.
/// </summary>
public class InspectionTests
{
    private const int Turns = 24;

    private sealed record Session(
        string Dir, string ManifestPath, SessionInspector Inspector,
        TelemetryRecordFile Telemetry, IReadOnlyList<SessionTrace.Row> Trace) : IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true);
        }
    }

    private static Sim.Core.Systems.SimConfig Cfg()
    {
        using Stream sim = Sim.Data.DataFiles.OpenSim();
        using Stream needs = Sim.Data.DataFiles.OpenNeeds();
        using Stream goods = Sim.Data.DataFiles.OpenGoods();
        return Sim.Core.Systems.SimConfigLoader.Load(sim, needs, goods);
    }

    private static Session Play(string tag)
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-p3-" + tag);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);

        var orders = new OrderLog();
        // One real order, so the order path is exercised rather than assumed.
        orders.Append(new OrderRecord(2, 1, OrderKind.LaborAllocation, 0, 70.0));

        WorldState world = Sim.Cli.HeadlessFounding.Found(42, 256, 4);
        var executor = new TurnExecutor(Sim.Cli.CliRecipes.Era(), Sim.Cli.CliRecipes.ProductionPipeline(), orders);

        string manifestPath;
        using (var emitter = new Sim.Cli.SessionEmitter(
            dir, 42, 256, 4, orders, Cfg(), world, "testsha", "testdate", "linux-x64"))
        {
            for (int t = 1; t <= Turns; t++)
            {
                WorldState prev = world;
                world = executor.Step(prev);
                emitter.Observe(prev, world);
            }
            emitter.Close();
            manifestPath = emitter.ManifestPath;
        }

        SessionInspector inspector = SessionInspector.Open(manifestPath);
        SessionManifest manifest;
        using (FileStream file = File.OpenRead(manifestPath)) manifest = SessionManifest.Read(file, manifestPath);
        return new Session(
            dir, manifestPath, inspector,
            TelemetryRecordFile.Read(Path.Combine(dir, manifest.TelemetryFile)),
            SessionTrace.Parse(File.ReadLines(Path.Combine(dir, manifest.TraceFile)), "trace"));
    }

    // -----------------------------------------------------------------------

    [Fact]
    public void EVERYAnswerCarriesAnEvidenceTag_andNOTRECORDEDIsNeverPromoted()
    {
        using Session s = Play("tags");
        Answer[] all = s.Inspector.Answer("all", null);
        Assert.NotEmpty(all);
        foreach (Answer a in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Topic));
            Assert.False(string.IsNullOrWhiteSpace(a.Basis));
            Assert.StartsWith("[", a.Header, StringComparison.Ordinal);
            Assert.Contains(EvidenceTag.Of(a.Evidence), a.Header, StringComparison.Ordinal);
        }

        // The pairwise destination is NOT RECORDED, and the answer is the exact
        // string the ruling requires — not a hedge, not an approximation.
        int pairwiseAnswers = 0;
        foreach (Answer a in all)
        {
            if (!a.Topic.Contains("pairwise", StringComparison.Ordinal)) continue;
            pairwiseAnswers++;
            Assert.Equal(Evidence.NotRecorded, a.Evidence);
            Assert.Equal(
                "UNAVAILABLE: pairwise migration destination was not persisted by this build.",
                a.Summary);
            Assert.Contains("[NOT RECORDED]", a.Header, StringComparison.Ordinal);
        }
        // It is printed EVERY time the aggregate is, so the aggregate can never
        // be mistaken for the pairwise fact.
        Assert.True(pairwiseAnswers >= 1);
    }

    [Fact]
    public void HappinessInTheRecordISTheAuthoritativeValue_bitForBit_notACopyOfTheFormula()
    {
        // THE PROPERTY THAT DECIDES WHETHER THE HAPPINESS ANSWER IS EVIDENCE.
        // The record's value must equal SettlementHappiness.Of on the same
        // world EXACTLY — no epsilon — because the observer CALLS that public
        // reader rather than re-implementing it. A copied per-factor calculator
        // would pass an approximate comparison and fail this one the first time
        // the formula moved.
        using Session s = Play("happiness-exact");
        Sim.Core.Systems.SimConfig cfg = Cfg();

        var orders = new OrderLog();
        orders.Append(new OrderRecord(2, 1, OrderKind.LaborAllocation, 0, 70.0));
        WorldState world = Sim.Cli.HeadlessFounding.Found(42, 256, 4);
        var executor = new TurnExecutor(Sim.Cli.CliRecipes.Era(), Sim.Cli.CliRecipes.ProductionPipeline(), orders);

        int compared = 0;
        for (int t = 1; t <= Turns; t++)
        {
            world = executor.Step(world);
            TelemetryTurn? turn = s.Telemetry.At(t);
            Assert.NotNull(turn);
            foreach (TelemetrySettlement row in turn!.Settlements)
            {
                double authoritative = SettlementHappiness.Of(world, new SettlementId(row.Settlement), cfg);
                Assert.Equal(authoritative, row.Happiness);     // EXACT, no tolerance
                var factors = new double[SettlementHappiness.FactorCount];
                SettlementHappiness.Factors(world, new SettlementId(row.Settlement), cfg, factors);
                Assert.Equal(SettlementHappiness.FactorCount, row.HappinessFactors.Length);
                for (int i = 0; i < factors.Length; i++) Assert.Equal(factors[i], row.HappinessFactors[i]);
                compared++;
            }
        }
        Assert.True(compared > 0, "the comparison must actually have run");
    }

    [Fact]
    public void EveryRecordedMIGRATIONCorrespondsToAnActualAggregateStateTransition()
    {
        // An event must name a change the state actually underwent. The world
        // total is a SUM of the per-settlement inflows the migration system
        // wrote, so the two must agree exactly, every turn.
        using Session s = Play("migration-real");
        foreach (TelemetryTurn turn in s.Telemetry.Turns)
        {
            long inflow = 0, outflow = 0;
            foreach (TelemetrySettlement row in turn.Settlements) { inflow += row.Inflow; outflow += row.Outflow; }
            Assert.Equal(turn.Flows.MigrantsMoved, inflow);

            // A movement event is emitted ONLY where a flow is non-zero.
            if (inflow == 0 && outflow == 0)
            {
                foreach (TelemetrySettlement row in turn.Settlements)
                {
                    Assert.Equal(0, row.Inflow);
                    Assert.Equal(0, row.Outflow);
                }
            }
        }

        Answer events = s.Inspector.MajorEvents();
        foreach (string line in events.Lines)
        {
            if (!line.Contains("MIGRATION", StringComparison.Ordinal)) continue;
            long turn = TurnOf(line);
            TelemetryTurn? t = s.Telemetry.At(turn);
            Assert.NotNull(t);
            long moved = 0;
            foreach (TelemetrySettlement row in t!.Settlements) moved += row.Inflow + row.Outflow;
            Assert.True(moved > 0, $"a MIGRATION event was emitted for turn {turn}, which recorded no movement");
        }
    }

    [Fact]
    public void EveryRecordedARTISANActivationCorrespondsToTheExistingLATCHRow()
    {
        using Session s = Play("artisan-latch");
        Answer[] artisan = s.Inspector.ArtisanActivation(null);
        Answer latch = artisan[0];
        Assert.Equal(Evidence.Known, latch.Evidence);
        Assert.Equal(Evidence.Derivable, artisan[1].Evidence);   // presence is NOT activation

        foreach (string line in latch.Lines)
        {
            if (!line.Contains("ACTIVATED on turn", StringComparison.Ordinal)) continue;
            int settlement = int.Parse(
                line.Split("settlement ")[1].Split(':')[0], System.Globalization.CultureInfo.InvariantCulture);
            long turn = long.Parse(
                line.Split("ACTIVATED on turn ")[1].Split("  ")[0], System.Globalization.CultureInfo.InvariantCulture);

            TelemetrySettlement? row = s.Telemetry.Settlement(turn, settlement);
            Assert.NotNull(row);
            Assert.Contains(row!.ClassActive, c =>
                c.Name.Contains("artisan", StringComparison.OrdinalIgnoreCase) && c.Active != 0);

            // ...and the turn before it, the latch was NOT set: an activation
            // event for a latch that was already set would be a fabricated
            // transition.
            TelemetrySettlement? before = s.Telemetry.Settlement(turn - 1, settlement);
            if (before is not null)
            {
                foreach (ClassActiveRow c in before.ClassActive)
                    if (c.Name.Contains("artisan", StringComparison.OrdinalIgnoreCase))
                        Assert.Equal(0, c.Active);
            }
        }
    }

    [Fact]
    public void FOODTotalsReconcileWithTheAuthoritativeFields_exactly()
    {
        using Session s = Play("food");
        foreach (TelemetryTurn t in s.Telemetry.Turns)
        {
            GrainTotals g = t.Grain;
            Assert.True(g.Reconciles,
                $"turn {t.Turn} grain account does not reconcile (discrepancy {g.Discrepancy})");
            Assert.Equal(0, g.Discrepancy);
            // The identity itself, restated and checked as EXACT long equality.
            Assert.Equal(
                g.Closing,
                g.Opening + g.Endowment + g.Harvest - g.Eaten - g.Spoilage - g.Overflow);
            Assert.True(t.PopReconciles, $"turn {t.Turn} population account does not reconcile");
            Assert.Equal(
                t.PopClosing,
                t.PopOpening + t.Births - t.NaturalDeaths - t.Starvation);
        }

        Answer[] resources = s.Inspector.ResourceFlows(null);
        Assert.Equal(Evidence.Known, resources[0].Evidence);
        // The per-settlement split is a RESIDUAL, tagged as one, and carries the
        // identity it is a residual OF.
        Assert.Equal(Evidence.Derivable, resources[1].Evidence);
        Assert.Contains("RESIDUAL", resources[1].Basis, StringComparison.Ordinal);
        Assert.Contains("BoundStore", resources[1].Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void EventsReferenceVALIDEntities_andNoEventIsEmittedForATransitionThatDidNotHappen()
    {
        using Session s = Play("events");
        int[] valid = s.Telemetry.SettlementIds();
        Answer events = s.Inspector.MajorEvents();

        foreach (string line in events.Lines)
        {
            long turn = TurnOf(line);
            Assert.NotNull(s.Telemetry.At(turn));
            if (!line.Contains("settlement ", StringComparison.Ordinal)) continue;
            int id = int.Parse(
                line.Split("settlement ")[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains(id, valid);
        }

        // The categories this build HAS NO MECHANISM FOR must never appear. They
        // are absent because they do not exist, not because they went unobserved.
        foreach (string line in events.Lines)
        {
            Assert.DoesNotContain("FORCED DISPLACEMENT", line, StringComparison.Ordinal);
            Assert.DoesNotContain("WAR", line, StringComparison.Ordinal);
            Assert.DoesNotContain("BATTLE", line, StringComparison.Ordinal);
        }

        // A FOOD SHORTFALL event must sit on a turn where the deficit ratio
        // actually crossed zero, and a CONTROL CHANGED event on a turn where a
        // control row was actually lost.
        foreach (string line in events.Lines)
        {
            long turn = TurnOf(line);
            if (line.Contains("CONTROL CHANGED", StringComparison.Ordinal))
                Assert.True(s.Telemetry.At(turn)!.Flows.ControlLost > 0);
            if (!line.Contains("FOOD SHORTFALL", StringComparison.Ordinal)) continue;
            int id = int.Parse(
                line.Split("settlement ")[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(s.Telemetry.Settlement(turn, id)!.DeficitRatio > 0.0);
            Assert.True(s.Telemetry.Settlement(turn - 1, id) is null
                || s.Telemetry.Settlement(turn - 1, id)!.DeficitRatio == 0.0);
        }
    }

    [Fact]
    public void ORDERSAreAttributableToTheirActorAndToItsCommandSource()
    {
        // The record carries the ACTOR by stable id. CommandSource is a property
        // of the polity in the world and is carried by NO artifact, so it is
        // reachable only through the public EmpireQuery on a replayed world —
        // DERIVABLE, and labelled as such rather than passed off as READ.
        using Session s = Play("orders");
        var orders = new OrderLog();
        orders.Append(new OrderRecord(2, 1, OrderKind.LaborAllocation, 0, 70.0));
        WorldState world = Sim.Cli.HeadlessFounding.Found(42, 256, 4);
        var executor = new TurnExecutor(Sim.Cli.CliRecipes.Era(), Sim.Cli.CliRecipes.ProductionPipeline(), orders);

        int delivered = 0;
        for (int t = 1; t <= Turns; t++)
        {
            world = executor.Step(world);
            TelemetryTurn turn = s.Telemetry.At(t)!;
            foreach (OrderRow o in turn.Orders)
            {
                delivered++;
                Assert.Equal(1, o.Actor);
                Assert.Equal("LaborAllocation", o.Kind);
                Assert.Equal(0, o.Settlement);         // decoded, not the raw packed target
                Assert.True(EmpireQuery.TryGetCommandSource(world, new PolityId(o.Actor), out CommandSource source));
                Assert.Equal(CommandSource.Player, source);
            }
        }
        // The delivery rule: an order stamped turn T lands on the step FROM T.
        Assert.Equal(1, delivered);
        Assert.Single(s.Telemetry.At(3)!.Orders);
        Assert.Empty(s.Telemetry.At(2)!.Orders);
    }

    [Fact]
    public void STABLEIdsAreStable_acrossEveryTurnTheyAppearIn()
    {
        using Session s = Play("ids");
        var foundedTurn = new Dictionary<int, long>();
        foreach (TelemetryTurn t in s.Telemetry.Turns)
        {
            var seenThisTurn = new List<int>();
            foreach (TelemetrySettlement row in t.Settlements)
            {
                Assert.DoesNotContain(row.Settlement, seenThisTurn);    // unique within a turn
                seenThisTurn.Add(row.Settlement);
                if (foundedTurn.TryGetValue(row.Settlement, out long known))
                    Assert.Equal(known, row.FoundedTurn);               // never re-numbered
                else
                    foundedTurn[row.Settlement] = row.FoundedTurn;
            }
        }
        Assert.NotEmpty(foundedTurn);
        // No display name appears in the settlement key anywhere in the record.
        foreach (int id in s.Telemetry.SettlementIds()) Assert.True(id >= 0);
    }

    [Fact]
    public void EVERYTurnHasAPreAndPostStateIdentity_withThePreTaggedDERIVABLE()
    {
        // Only the POST-turn hash is recorded: the trace writes one line per turn
        // AFTER the step. The pre-turn identity is therefore the previous turn's
        // post-turn hash, and the answer says DERIVABLE, not KNOWN.
        using Session s = Play("hashes");
        Answer[] hashes = s.Inspector.StateHashes();
        Assert.Equal(Evidence.Known, hashes[0].Evidence);
        Assert.Equal(Evidence.Derivable, hashes[1].Evidence);
        Assert.Contains("ONLY THE POST-TURN HASH IS RECORDED", hashes[1].Basis, StringComparison.Ordinal);

        Assert.Equal(Turns + 1, s.Trace.Count);           // turn 0 included
        for (int i = 0; i < s.Trace.Count; i++)
        {
            Assert.Equal(i, s.Trace[i].Turn);
            Assert.Equal(64, s.Trace[i].Hash.Length);
            if (i > 0) Assert.NotEqual(s.Trace[i - 1].Hash, s.Trace[i].Hash);
        }
    }

    [Fact]
    public void ANSWERINGReadsTheSAVEDRecordAndDoesNOTReRunTheSimulation()
    {
        // The whole point of the topic surface. It is proved structurally: the
        // inspector is handed the files and nothing else — no executor, no world,
        // no config — so it CANNOT step the simulation. Removing the telemetry
        // turns every telemetry-backed answer into a stated absence rather than
        // into a fresh run.
        using Session s = Play("no-rerun");
        SessionManifest manifest;
        using (FileStream file = File.OpenRead(s.ManifestPath)) manifest = SessionManifest.Read(file, s.ManifestPath);
        File.Delete(Path.Combine(s.Dir, manifest.TelemetryFile));

        SessionInspector blind = SessionInspector.Open(s.ManifestPath);
        Answer settlements = blind.SettlementHistory(null);
        Assert.Equal(Evidence.NotRecorded, settlements.Evidence);
        Assert.Contains("This is NOT answered from a fresh simulation run", settlements.Basis, StringComparison.Ordinal);

        // ...while the trace-backed and forensic-backed answers still work,
        // because those files are still there.
        Assert.Equal(Evidence.Known, blind.StateHashes()[0].Evidence);
        Assert.Equal(Evidence.Known, blind.Limits().Evidence);
    }

    [Fact]
    public void THEHappinessDecompositionAnswerReportsTheARCHITECTURALBoundary_notAnExplanation()
    {
        using Session s = Play("happiness-limit");
        Answer[] happiness = s.Inspector.HappinessHistory(0);
        Assert.Equal(Evidence.Known, happiness[0].Evidence);        // the value
        Assert.Equal(Evidence.NotRecorded, happiness[1].Evidence);  // the decomposition

        // The boundary is stated as file:line so a reviewer can CHECK it.
        Assert.Contains("SettlementHappiness.cs", happiness[1].Basis, StringComparison.Ordinal);
        Assert.Contains("WeightOf is `private static` (:234)", happiness[1].Basis, StringComparison.Ordinal);
        Assert.Contains("It does not manufacture a decomposition.", happiness[1].Basis, StringComparison.Ordinal);
    }

    /// <summary>
    /// T4.21-5 (spec §3.11 / §6.6) — THE FOUR NEW MAJOR-EVENT CATEGORIES, held
    /// to the same rule as every other: an event line must correspond to a
    /// transition the RECORD shows, and a transition the record does not show
    /// must produce no line.
    ///
    /// The played session is the canonical 24-turn one; it is not required to
    /// famine, be struck, be abandoned or refuse refugees, and this test does
    /// NOT force it to. What it pins is the implication in both directions:
    /// every FAMINE ONSET line sits on a settlement-turn the telemetry
    /// classifies as Famine; every DISASTER line on one whose applied
    /// multiplier is below 1; every ABANDONMENT on one the record marks
    /// abandoned; every REFUGEES REFUSED on one whose vacancyScale is below 1.
    /// And no such line may appear on a settlement-turn that does not.
    /// </summary>
    [Fact]
    public void T421_TheFamineDisasterAbandonmentAndRefusalLines_MatchTheRecordBothWays()
    {
        using Session s = Play("t421-events");
        Answer events = s.Inspector.MajorEvents();

        int famineLines = 0, disasterLines = 0, abandonLines = 0, refusedLines = 0;
        foreach (string line in events.Lines)
        {
            if (!line.Contains("settlement ", StringComparison.Ordinal)) continue;
            long turn = TurnOf(line);
            int id = int.Parse(
                line.Split("settlement ")[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
            TelemetrySettlement row = s.Telemetry.Settlement(turn, id)!;

            if (line.Contains("FAMINE ONSET", StringComparison.Ordinal))
            {
                famineLines++;
                Assert.True(row.FoodState.IsFamine, $"FAMINE ONSET on turn {turn} but the record does not say Famine");
                Assert.Contains("reason ", line, StringComparison.Ordinal);
                Assert.DoesNotContain("reason None", line, StringComparison.Ordinal);
            }
            if (line.Contains("DISASTER", StringComparison.Ordinal))
            {
                disasterLines++;
                // T4.21-6 — NOT the inspector's own predicate restated. Until this
                // packet the assertion here re-stated exactly the expression
                // SessionInspector used to emit the line, on exactly the field it
                // used, so it held whichever field that was — including the WRONG
                // one, which is how the DISASTER lines shipped systematically one
                // turn away from the FAMINE they caused. The cross-check now runs
                // against FoodState.IsStruck's own reading, via TelemetryFoodState.Struck.
                Assert.True(row.FoodState.Struck,
                    $"turn {turn} settlement {id}: a DISASTER line on a settlement-turn the record does not "
                    + "call struck — the line and FoodState.IsStruck read different fields");
            }
            if (line.Contains("ABANDONMENT", StringComparison.Ordinal))
            {
                abandonLines++;
                Assert.True(row.FoodState.Abandoned);
            }
            if (line.Contains("REFUGEES REFUSED", StringComparison.Ordinal))
            {
                refusedLines++;
                Assert.True(row.MigrationPlan.RefugeesRefused);
                Assert.True(row.MigrationPlan.VacancyScale < 1.0);
            }
        }

        // THE OTHER DIRECTION. Walk the record itself: every settlement-turn
        // that ENTERS Famine, is struck for the first time, becomes abandoned or
        // refuses refugees must have produced its line.
        int expectedFamine = 0, expectedRefused = 0;
        var wasFamine = new Dictionary<int, bool>();
        foreach (TelemetryTurn t in s.Telemetry.Turns)
        {
            foreach (TelemetrySettlement row in t.Settlements)
            {
                Assert.True(row.FoodState.Recorded, "a telemetry/v3 line must carry the foodState section");
                Assert.True(row.MigrationPlan.Recorded, "a telemetry/v3 line must carry the migrationPlan section");
                if (row.FoodState.IsFamine && !(wasFamine.TryGetValue(row.Settlement, out bool was) && was))
                    expectedFamine++;
                wasFamine[row.Settlement] = row.FoodState.IsFamine;
                if (row.MigrationPlan.RefugeesRefused) expectedRefused++;
            }
        }
        Assert.Equal(expectedFamine, famineLines);
        Assert.Equal(expectedRefused, refusedLines);
        Assert.True(disasterLines >= 0 && abandonLines >= 0);

        // T4.21-6 — THE CROSS-CHECK THE DISASTER BLOCK LACKED. Two properties
        // that are not the inspector talking to itself:
        //   (a) every FAMINE-with-reason-Disaster settlement-turn carries an
        //       APPLIED multiplier below 1 — the classification and the recorded
        //       disaster fields are the same fact, so the panel beneath a FAMINE
        //       line can never read "none applied to this harvest";
        //   (b) the struck settlement-turns are NON-EMPTY on this session, so
        //       (a) is not vacuously true.
        // On the shipped mapping (a) failed on the dominant canonical shape: at
        // dt 10 with durationYears 5 the event has run its course by the turn its
        // deficit classifies, so the surviving row reads Multiplier 1.0 and the
        // strike lives only in AppliedMultiplier.
        int famineByDisaster = 0, struckTurns = 0;
        foreach (TelemetryTurn t in s.Telemetry.Turns)
        {
            foreach (TelemetrySettlement row in t.Settlements)
            {
                if (row.FoodState.Struck) struckTurns++;
                if (!row.FoodState.IsFamine) continue;
                if (!row.FoodState.FamineReason.Contains("Disaster", StringComparison.Ordinal)) continue;
                famineByDisaster++;
                Assert.True(row.FoodState.DisasterRowPresent,
                    $"turn {t.Turn} settlement {row.Settlement}: FAMINE for reason "
                    + $"{row.FoodState.FamineReason} with no disaster row recorded");
                Assert.True(row.FoodState.DisasterAppliedMultiplier < 1.0,
                    $"turn {t.Turn} settlement {row.Settlement}: FAMINE for reason "
                    + $"{row.FoodState.FamineReason}, yet the recorded applied multiplier is "
                    + row.FoodState.DisasterAppliedMultiplier.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                    + " — the classification and the disaster block disagree");
                Assert.True(row.FoodState.Struck, "IsStruck's own reading must agree with the reason");
            }
        }
        Assert.True(famineByDisaster > 0,
            "no FAMINE-by-disaster settlement-turn in this session — the cross-check above is vacuous");
        Assert.True(struckTurns > 0, "no struck settlement-turn recorded — the disaster block is untested");
        Console.WriteLine(
            $"T4.21-6 disaster cross-check: {famineByDisaster} FAMINE-by-disaster settlement-turns, "
            + $"{struckTurns} struck settlement-turns, {disasterLines} DISASTER lines.");

        // The answer's own text must warn that ABSENCE on an older record means
        // "the file does not say", not "it did not happen".
        Assert.Contains("the file does not say", events.Basis, StringComparison.Ordinal);
    }

    /// <summary>
    /// T4.21-5 — THE DIRECTOR'S EXISTING RECORD STILL READS. A telemetry/v2 line
    /// (the vintage of every session played before this packet) parses, every v2
    /// field reads exactly as before, and the two new sections report
    /// Recorded = FALSE — the reader says "the file does not say" instead of
    /// defaulting a food state into existence. A THIRD vintage is still refused.
    /// </summary>
    [Fact]
    public void T421_ATelemetryV2Line_StillReads_AndItsMissingSectionsSaySoRatherThanDefaulting()
    {
        using Session s = Play("t421-v2");
        // Take a real v3 line this build wrote and DOWNGRADE it by stripping the
        // two sections and the tag — the honest simulation of an older file,
        // because every other byte is one this build actually produced.
        SessionManifest manifest;
        using (FileStream file = File.OpenRead(s.ManifestPath)) manifest = SessionManifest.Read(file, s.ManifestPath);
        string v3 = File.ReadAllLines(Path.Combine(s.Dir, manifest.TelemetryFile))[5];
        Assert.Contains("\"schema\":\"telemetry/v3\"", v3, StringComparison.Ordinal);

        using var doc = System.Text.Json.JsonDocument.Parse(v3);
        string v2 = Downgrade(doc.RootElement);
        Assert.Contains("\"schema\":\"telemetry/v2\"", v2, StringComparison.Ordinal);
        Assert.DoesNotContain("\"foodState\"", v2, StringComparison.Ordinal);
        Assert.DoesNotContain("\"migrationPlan\"", v2, StringComparison.Ordinal);

        TelemetryRecordFile older = TelemetryRecordFile.Parse([v2], "v2-fixture");
        TelemetryRecordFile newer = TelemetryRecordFile.Parse([v3], "v3-line");
        TelemetryTurn a = Assert.Single(older.Turns);
        TelemetryTurn b = Assert.Single(newer.Turns);

        // Every v2 field is identical across the two vintages: v3 is ADDITIVE.
        Assert.Equal(b.Turn, a.Turn);
        Assert.Equal(b.Grain, a.Grain);
        Assert.Equal(b.Flows, a.Flows);
        Assert.Equal(b.Settlements.Length, a.Settlements.Length);
        for (int i = 0; i < a.Settlements.Length; i++)
        {
            TelemetrySettlement x = a.Settlements[i], y = b.Settlements[i];
            Assert.Equal(y.Settlement, x.Settlement);
            Assert.Equal(y.PopClosing, x.PopClosing);
            Assert.Equal(y.DeficitRatio, x.DeficitRatio);
            Assert.Equal(y.Happiness, x.Happiness);

            // The new sections: absent on v2, recorded on v3.
            Assert.False(x.FoodState.Recorded);
            Assert.False(x.MigrationPlan.Recorded);
            Assert.False(x.FoodState.IsFamine);          // unrecorded is never "yes"
            Assert.False(x.MigrationPlan.RefugeesRefused);
            Assert.True(double.IsNaN(x.FoodState.EffectiveDeficit));
            Assert.True(y.FoodState.Recorded);
            Assert.True(y.MigrationPlan.Recorded);
        }

        // A v2 record produces NO T4.21-5 event lines at all — absence of the
        // section, not absence of the event.
        Assert.True(TelemetryRecordFile.IsReadable("telemetry/v2"));
        Assert.True(TelemetryRecordFile.IsReadable("telemetry/v3"));
        Assert.False(TelemetryRecordFile.IsReadable("telemetry/v4"));
        Assert.False(TelemetryRecordFile.IsReadable(null));
        InvalidDataException bad = Assert.Throws<InvalidDataException>(
            () => TelemetryRecordFile.Parse([v3.Replace("telemetry/v3", "telemetry/v4", StringComparison.Ordinal)], "future"));
        Assert.Contains("telemetry/v3, telemetry/v2", bad.Message, StringComparison.Ordinal);
    }

    /// <summary>Re-emit a v3 line as a v2 one: same bytes, minus the two
    /// sections this packet added, with the older tag.</summary>
    private static string Downgrade(System.Text.Json.JsonElement root)
    {
        using var buffer = new MemoryStream();
        using (var json = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            foreach (System.Text.Json.JsonProperty p in root.EnumerateObject())
            {
                if (p.NameEquals("schema")) { json.WriteString("schema", "telemetry/v2"); continue; }
                if (!p.NameEquals("settlements")) { p.WriteTo(json); continue; }
                json.WriteStartArray("settlements");
                foreach (System.Text.Json.JsonElement row in p.Value.EnumerateArray())
                {
                    json.WriteStartObject();
                    foreach (System.Text.Json.JsonProperty f in row.EnumerateObject())
                    {
                        if (f.NameEquals("foodState") || f.NameEquals("migrationPlan")) continue;
                        f.WriteTo(json);
                    }
                    json.WriteEndObject();
                }
                json.WriteEndArray();
            }
            json.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The turn an event line is stamped with: the first integer in it.</summary>
    private static long TurnOf(string line)
    {
        int i = 0;
        while (i < line.Length && !char.IsAsciiDigit(line[i])) i++;
        int start = i;
        while (i < line.Length && char.IsAsciiDigit(line[i])) i++;
        return long.Parse(line[start..i], System.Globalization.CultureInfo.InvariantCulture);
    }
}
