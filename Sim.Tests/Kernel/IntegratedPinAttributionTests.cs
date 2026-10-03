using System.Security.Cryptography;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Disaster;

namespace Sim.Tests.Kernel;

/// <summary>
/// M4 INTEGRATION — THE CONTROL THAT ATTRIBUTES EVERY MOVED PIN.
///
/// Three goldens move on the integrated tree (T4.4 schema v22 + M4 schema v23 +
/// the granary capacity-floor fix), and "schema only" is a claim that has to be
/// MEASURED, not asserted. This is the same control T4.4 used for its own
/// widening, run the other way round.
///
/// The control REMOVES the Empire state from a finished world and re-hashes. If
/// what comes back is the earlier pinned value byte for byte, then the Empire
/// state is the entire delta and no simulation state moved with it.
///
/// TWO LAYERS are separated, because M4 moved these pins twice for different
/// reasons: M4-A's schema v23 LAYOUT (two count prefixes, present even when the
/// tables are empty) and M4-C's founding CONTENT (the rows themselves).
///
/// T4.10 CHANGED WHAT THIS CONTROL CAN CLAIM, and the honesty of that matters more
/// than the convenience of leaving the old numbers in place. Until T4.10 these
/// stripped values equalled main's PRE-M4 pins, which is what licensed the phrase
/// "moved for the M4 schema alone". T4.10 removes the food term from migration
/// attractiveness — a genuine BEHAVIOURAL change — so the three worlds it reaches
/// no longer reduce to any pre-M4 value, and the reference constants below are
/// re-measured on this tree. The control therefore no longer proves "M4 layout is
/// the only delta since pre-M4"; it proves the WEAKER and still-useful thing, that
/// the M4 rows are separable from the simulation output they sit beside.
///
/// THE NO-UNRELATED-MOVEMENT CONTROL MOVED WITH IT, and it is the one that carries
/// the weight now: GoldenHashSeed42Turn200 below is SYNTHETIC and terrain-less, so
/// migration cannot reach it. Its stripped value is still main's pre-M4 pin,
/// untouched by T4.10 — which is the measurement showing this change stayed inside
/// migration instead of leaking somewhere it had no business being.
///
/// M4-C GENERALISED THIS. The original control stripped the two four-byte zero
/// count prefixes off the end of the stream, which was exact while nothing wrote
/// the tables — but M4-C's founding DOES write them, and `Controls` sits
/// mid-stream rather than in the trailer, so byte-stripping no longer expresses
/// the question. Clearing the three tables does, for empty and populated worlds
/// alike, and it degenerates to exactly the old trailer strip when they are empty.
///
/// T4.21-1 (CR-015) ADDED A THIRD LAYER, and it is the one this packet is
/// judged on: schema v25 appends the Disasters table (EMPTY in every canonical
/// world — the packet ships hazardPerYear = 0, so no row is ever written) and
/// DisasterSystem draws two uniforms per settlement per turn UNCONDITIONALLY, so
/// every founded world gains one RngStreamRow per settlement. Those two are the
/// ENTIRE delta the packet may make: <see cref="StripDisaster"/> removes the
/// disaster streams and clears the table, <see cref="HashAtSchemaV24"/> also
/// drops the empty v25 trailer, and the PRE-PACKET pins must return BYTE FOR
/// BYTE on every pinned world. FoodState and FoodHeadroom are statics nothing
/// calls yet, and ProductionSystem multiplies by exactly 1.0 without a strike —
/// this control is the measurement that says so, on the tree, not the argument.
/// The v22/v23 controls below strip the disaster layer too, so every constant
/// they carry is UNMOVED by this packet.
///
/// T4.21-2 (ADR-025, bounded migration) AND T4.21-3 (CR-015 / ADR-026, the
/// effective deficit and the headroom growth cap) ARE BOTH BEHAVIOURAL, and —
/// exactly as T4.10 did — they change what the controls can claim. Both move
/// every founded world (SnapshotTests.FoundedGolden has the joint cause), so
/// the stripped values no longer return any pre-T4.21 pin: every founded /
/// FirstReign / driven constant in this file is RE-MEASURED ON THE MERGED TREE
/// by the agent writing this line (ADR-015 §6), and what the strips prove is
/// the weaker, still-useful thing that the disaster streams and table, and the
/// M4 ladder's tables, are separable from the simulation output they sit
/// beside; "moved for the layout alone" is a historical fact about commit
/// 1735d41, not a property of this tree. The synthetic terrain-less world
/// (GoldenHashSeed42Turn200 — no distances, so no migration; no demand rows, so
/// the growth cap never reads it) is the one that carries the weight now: BOTH
/// its controls are UNMOVED and still return main's pre-M4 (0f94b4ad…) and the
/// pre-T4.21 (eec82711…) values byte for byte, re-measured on the merged tree,
/// which is the measurement showing the two packets stayed inside the founded
/// worlds' migration and demographics instead of leaking anywhere they had no
/// business being.
///
/// ADR-029 (D-044) ADDS A FOURTH LAYER: schema v26 appends the seven research
/// tables — ResearchTargets, ResearchProgress, ResearchCompleted, ResearchEurekas,
/// ResearchCostModifiers and (addendum A) ResearchCredits and ResearchExposures —
/// AFTER Disasters, and the production pipeline gains
/// the ResearchSystem as its last entry. The system writes ONLY those tables. No
/// other system reads them, and the system draws no RNG and records no ledger flow.
/// Under the curated Eurekas of addendum A an order-less world writes NO research
/// row in 300 turns (measured), so each research control also runs a research-DRIVEN
/// arm, a target chosen whenever one is free, which does. <see cref="StripResearch"/> clears
/// the seven tables, and <see cref="HashAtSchemaV25"/> also drops their seven empty
/// count prefixes. The PRE-PACKET pins — the values on main at 93270cd — must
/// return BYTE FOR BYTE on every pinned world. Any leak of research into
/// population, food, trade, migration or anything else would survive the strip and
/// break them. Every older control in this file strips the research layer too and
/// drops its seven prefixes, so every constant it carries is UNMOVED by this packet.
/// </summary>
public class IntegratedPinAttributionTests
{
    /// <summary>
    /// The driven world at T4.4's schema v22 WITH the capacity-floor fix — i.e.
    /// main's tree plus the behavioural fix and nothing else. Measured on the
    /// integrated tree by <see cref="HashAtSchemaV22"/>; the midpoint that lets
    /// the two causes of the driven pin's movement be reported separately.
    /// </summary>
    /// T4.19 (CR-014 ruled): re-measured on this tree with HashAtSchemaV22 —
    /// the driven world moved for lane C's founding vector (the cap change alone
    /// returns the old vector's pin byte for byte; DrivenGoldenTests has the arms).
    /// OLD 611a1508e650c9b897e3ec3ec0884969ae3add4d8de520fa5a126efbb71926ea.
    /// T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour, both
    /// packets — see the header and SnapshotTests.FoundedGolden).
    /// OLD (pre-packet) 60bd5b208696a25f93469d58d4a4284d8ae8467107aeee3545d3d0f82b61ba14;
    /// OLD (T4.21-2 alone) 789585bed857ae94888ee8edbdc18b1ee5c9222c23695109b86e7002271b194d;
    /// OLD (T4.21-3 alone) 23cdc7042bf2972ba13b52e9e6bb26a8fc526a74ba61284c93208bb73efadffd.
    internal const string CapacityFloorFixAtSchemaV22 =
        "e3d472c6de20e7032a71e78133c7108d34340f70f7f8c1fc036420c8bf6d04b2";

    /// <summary>Bytes one empty table contributes to the stream: its count prefix.</summary>
    private const int EmptyTableBytes = 4;

    /// <summary>
    /// The finished world with EVERY M4 row removed — Polities, Controls,
    /// Capitals (v23) and the construction queue and structures (v24) — then
    /// re-serialized with <paramref name="dropTrailingTables"/> of the trailing
    /// empty count prefixes also removed. Operates on a deep Clone.
    ///
    /// That parameter is what lets one control answer three different questions:
    /// drop 4 tables and the stream is T4.4's v22, before M4 existed; drop the 2
    /// v24 tables and it is v23, the tree as it stood before M4-C; drop none and
    /// it is v24 with M4's content removed but its layout intact. Each is a real
    /// earlier pin, so each comparison is against a measured value rather than a
    /// recomputed one.
    /// </summary>
    private static string HashWithoutM4(WorldState world, int dropTrailingTables)
    {
        WorldState stripped = world.Clone();
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        StripAges(stripped);
        StripResearch(stripped);
        StripDisaster(stripped);
        stripped.Polities.Clear();
        stripped.Controls.Clear();
        stripped.Capitals.Clear();
        stripped.ConstructionQueue.Clear();
        stripped.Structures.Clear();

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }

        return HashDroppingTrailer(buffer.ToArray(), dropTrailingTables + AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount);
    }

    private static string HashDroppingTrailer(byte[] full, int dropTrailingTables)
    {
        int drop = dropTrailingTables * EmptyTableBytes;
        for (int i = full.Length - drop; i < full.Length; i++)
        {
            Assert.Equal(0, full[i]);   // what is dropped really is empty tables
        }

        return Convert.ToHexStringLower(SHA256.HashData(full.AsSpan(0, full.Length - drop).ToArray()));
    }

    /// <summary>
    /// T4.21-1: remove the packet's two layout contributions IN PLACE — the
    /// Disasters rows (none exist at hazard 0; cleared regardless so a populated
    /// world strips the same way) and DisasterSystem's RngStreams rows, which
    /// the registry appended lazily on turn 1 AFTER HarvestWeather's and which
    /// therefore sit in the stream exactly where removing them restores the
    /// pre-packet row sequence. Returns how many stream rows were removed, so a
    /// caller can assert the strip was not vacuous.
    /// </summary>
    private static int StripDisaster(WorldState stripped)
    {
        stripped.Disasters.Clear();
        var kept = new List<RngStreamRow>(stripped.RngStreams.Count);
        int removed = 0;
        for (int i = 0; i < stripped.RngStreams.Count; i++)
        {
            RngStreamRow row = stripped.RngStreams[i];
            if (row.System == DisasterSystem.WellKnownId) { removed++; continue; }
            kept.Add(row);
        }
        stripped.RngStreams.Clear();
        for (int i = 0; i < kept.Count; i++) stripped.RngStreams.Add(kept[i]);
        return removed;
    }

    /// <summary>The stream as T4.4's v22 — before M4 touched the schema at all.
    /// Ten trailing empty tables since v26: Polities, Capitals (v23),
    /// ConstructionQueue, Structures (v24), Disasters (v25) and the seven research
    /// tables (v26).</summary>
    private static string HashAtSchemaV22(WorldState world) => HashWithoutM4(world, 5 + ResearchTableCount);

    /// <summary>The stream as v23 — M4-A's tables present but empty, i.e. the
    /// tree exactly as it stood before M4-C's founding wrote them.</summary>
    private static string HashAtSchemaV23(WorldState world) => HashWithoutM4(world, 3 + ResearchTableCount);

    /// <summary>ADR-029: the seven v26 research tables, appended after Disasters (two of
    /// them, ResearchCredits and ResearchExposures, by addendum A).</summary>
    private const int ResearchTableCount = 7;

    /// <summary>ADR-031: the five v28 Age/military tables, appended after ResearchExposures.
    /// Every pin in this file predates v28, so every control strips them and drops their
    /// five count prefixes first (the two Age systems draw no RNG and write no ledger flow;
    /// founding formations are tokens), then asks its original question unchanged.</summary>
    private const int AgeTableCount = 5;

    private static int StripAges(WorldState stripped)
    {
        int removed = stripped.AgeStates.Count + stripped.AgeEligibility.Count + stripped.AgeTransitions.Count
            + stripped.MilitaryUnits.Count + stripped.UnitConversions.Count;
        stripped.AgeStates.Clear();
        stripped.AgeEligibility.Clear();
        stripped.AgeTransitions.Clear();
        stripped.MilitaryUnits.Clear();
        stripped.UnitConversions.Clear();
        return removed;
    }

    /// <summary>The stream as v26 — the tree exactly as it stood BEFORE ADR-031: the Age/military
    /// rows removed, the five empty v28 prefixes dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV26(WorldState world, out int ageRowsRemoved)
    {
        WorldState stripped = world.Clone();
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        ageRowsRemoved = StripAges(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount);
    }

    /// <summary>ADR-032: the two v29 transport tables, appended after UnitConversions. Every pin in
    /// this file predates v29, so every control strips them and drops their two count prefixes
    /// first, then asks its original question unchanged.</summary>
    private const int RoadTableCount = 2;

    private static int StripRoads(WorldState stripped)
    {
        int removed = stripped.TransportEdges.Count + stripped.RoadDevelopments.Count;
        stripped.TransportEdges.Clear();
        stripped.RoadDevelopments.Clear();
        return removed;
    }

    /// <summary>The stream as v28 — the tree exactly as it stood BEFORE ADR-032: the transport rows
    /// removed, the two empty v29 prefixes dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV28(WorldState world, out int roadRowsRemoved)
    {
        WorldState stripped = world.Clone();
        StripInstitutions(stripped);
        StripGovernance(stripped);
        roadRowsRemoved = StripRoads(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), RoadTableCount + GovernanceTableCount + InstitutionTableCount);
    }

    /// <summary>ADR-033 D4: the one v30 governance table (TaxPolicies), appended after
    /// RoadDevelopments. Every pin in this file predates v30, so every control strips the
    /// governance layer and drops its count prefix first, then asks its original question
    /// unchanged.</summary>
    private const int GovernanceTableCount = 1;

    /// <summary>
    /// ADR-033 D4: remove the governing loop's ENTIRE footprint in the stream, IN PLACE — the
    /// TaxPolicies rows (none exist in an order-less or tax-less world; cleared regardless so a
    /// populated world strips the same way) and the reach GovernanceSystem writes into
    /// ControlRow.Strength, restored to the 1.0 every control row carried before v30 (founding and
    /// colonization write exactly 1.0 and, before the port, nothing else ever wrote the field).
    /// GovernanceSystem draws no RNG and writes no ledger flow, and Strength is read only by the
    /// tax readers, so with no tax levied this IS the whole delta. Returns how many Strength
    /// fields were restored, so a caller can tell a populated strip from a vacuous one.
    /// </summary>
    private static int StripGovernance(WorldState stripped)
    {
        stripped.TaxPolicies.Clear();
        int restored = 0;
        for (int i = 0; i < stripped.Controls.Count; i++)
        {
            ControlRow row = stripped.Controls[i];
            if (BitConverter.DoubleToInt64Bits(row.Strength) == BitConverter.DoubleToInt64Bits(1.0)) continue;
            stripped.Controls[i] = row with { Strength = 1.0 };
            restored++;
        }
        return restored;
    }

    /// <summary>The stream as v29 — the tree exactly as it stood BEFORE ADR-033 D4: the tax rows
    /// removed, every Strength back at 1.0, the empty v30 prefix dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV29(WorldState world, out int strengthsRestored)
    {
        WorldState stripped = world.Clone();
        StripInstitutions(stripped);
        strengthsRestored = StripGovernance(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), GovernanceTableCount + InstitutionTableCount);
    }

    /// <summary>ADR-033 D6 + D10: the two v31 tables (Institutions, ConstructionLabor), appended after
    /// TaxPolicies. Every pin in this file predates v31, so every control strips the institutions layer and drops
    /// its two count prefixes first, then asks its original question unchanged.</summary>
    private const int InstitutionTableCount = 2;

    /// <summary>
    /// ADR-033 D6 + D10: remove the institutions layer's ENTIRE footprint in the stream, IN PLACE — the
    /// Institutions rows, the ConstructionLabor rows (D10, rebuilt every step; a row only where a project was
    /// built) and the ResearchCostModifiers rows InstitutionsSystem now rebuilds every step (empty in any world
    /// with no university, exactly as the table was empty before it had a writer). InstitutionsSystem draws no
    /// RNG and writes no ledger flow; with no institution the labour reader returns the raw adult count and the
    /// mortality seam the literal 1.0, and with no completed project PathBuild subtracts nothing — so in a world
    /// with neither, these rows (none) and two count prefixes are the whole delta. Returns how many rows were
    /// removed.
    /// </summary>
    private static int StripInstitutions(WorldState stripped)
    {
        int removed = stripped.Institutions.Count + stripped.ConstructionLabor.Count + stripped.ResearchCostModifiers.Count;
        stripped.Institutions.Clear();
        stripped.ConstructionLabor.Clear();
        stripped.ResearchCostModifiers.Clear();
        return removed;
    }

    /// <summary>The stream as v30 — the tree exactly as it stood BEFORE ADR-033 D6/D10: the institutions layer
    /// removed, the two empty v31 prefixes dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV30(WorldState world, out int institutionRowsRemoved)
    {
        WorldState stripped = world.Clone();
        institutionRowsRemoved = StripInstitutions(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), InstitutionTableCount);
    }

    /// <summary>
    /// ADR-029: clear the seven research tables IN PLACE. It returns how many rows
    /// were removed, so a caller can tell a populated strip from a vacuous one. The
    /// research system owns nothing else, draws no RNG and writes no ledger flow, so
    /// these tables are its entire footprint in the stream.
    /// </summary>
    private static int StripResearch(WorldState stripped)
    {
        int removed = stripped.ResearchTargets.Count + stripped.ResearchProgress.Count
            + stripped.ResearchCompleted.Count + stripped.ResearchEurekas.Count
            + stripped.ResearchCostModifiers.Count + stripped.ResearchCredits.Count
            + stripped.ResearchExposures.Count;
        stripped.ResearchTargets.Clear();
        stripped.ResearchProgress.Clear();
        stripped.ResearchCompleted.Clear();
        stripped.ResearchEurekas.Clear();
        stripped.ResearchCostModifiers.Clear();
        stripped.ResearchCredits.Clear();
        stripped.ResearchExposures.Clear();
        return removed;
    }

    /// <summary>
    /// The stream as v25 — the tree exactly as it stood BEFORE the research packet
    /// (main at 93270cd): the research rows removed, the seven empty v26 prefixes
    /// dropped, and NOTHING ELSE touched. The pre-packet pin must return byte for
    /// byte, or research changed something outside its own tables.
    /// </summary>
    private static string HashAtSchemaV25(WorldState world, out int researchRowsRemoved)
    {
        WorldState stripped = world.Clone();
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        StripAges(stripped);
        researchRowsRemoved = StripResearch(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), ResearchTableCount + AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount);
    }

    /// <summary>
    /// The stream as v24 — the tree exactly as it stood BEFORE T4.21-1: the
    /// disaster streams and table removed, the empty v25 trailer dropped, and
    /// NOTHING ELSE touched (M4's rows stay). The pre-packet pin must return
    /// byte for byte, or the packet changed behaviour.
    /// </summary>
    private static string HashAtSchemaV24(WorldState world, out int disasterStreamsRemoved)
    {
        WorldState stripped = world.Clone();
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        StripAges(stripped);
        StripResearch(stripped);
        disasterStreamsRemoved = StripDisaster(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), 1 + ResearchTableCount + AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount);
    }

    /// <summary>
    /// T4.21-4 — THE λ = 0 TWIN THE LAYOUT CONTROLS RUN ON, and why; KEPT BY
    /// T4.21-7 NOW THAT λ = 0 IS THE SHIPPED VALUE AGAIN.
    ///
    /// T4.21-4 armed the famine-class disaster (sim.json
    /// disaster.hazardPerYear 0.0 -> 0.01). That is a BEHAVIOUR change and it
    /// moved the behavioural goldens, which were re-pinned where they live
    /// (SnapshotTests.FoundedGolden, DrivenGoldenTests.DrivenGolden). The
    /// controls in this file are LAYOUT controls: their question is whether a
    /// stream layer is SEPARABLE from the stream, not what the world did.
    /// Re-measuring them against the armed world would answer a different
    /// question and would quietly drop the attribution.
    ///
    /// So they run the λ = 0 twin, which — because DisasterSystem draws both
    /// its uniforms UNCONDITIONALLY (the stated RNG contract) — is bit-identical
    /// to the tree as it stood before the arming commit. Every constant in this
    /// file therefore returns BYTE FOR BYTE, and that was itself the
    /// attribution T4.21-4 owed: the arming was the ENTIRE cause of the
    /// behavioural goldens' movement, because nothing else in that packet
    /// touched code that runs.
    ///
    /// T4.21-7 DISARMS the shipped value (CR-016: the mechanism ships complete
    /// and tested but INERT; the RATE is the director's). The twin is therefore
    /// now equal to the shipped config — and is KEPT EXPLICIT anyway, for the
    /// same reason it was introduced: these controls must answer the LAYOUT
    /// question whatever λ the director rules, and a control that silently
    /// inherits the shipped value would stop being one the moment the rate is
    /// ruled non-zero. The shipped value is asserted alongside, so a silent
    /// RE-ARMING fails here too — and if it is ruled, these constants stay put
    /// while the behavioural goldens move, which is the whole point.
    /// </summary>
    /// <summary>R1: the canonical config with the recipe-knowledge links removed (TestConfigs.PreRecipeKnowledge).
    /// EVERY layer control in this file runs on it, so each constant it carries is UNMOVED by R1 (the T4.21-4
    /// precedent: a layout control must not be asked to absorb a behaviour change); R1's own controls at the
    /// end of the file prove the twin returns the pre-R1 pins byte for byte.
    /// R2a: the twin is also PRE-R2a (TestConfigs.PreTradeKnowledge: no trade node/entity, trade ungated, no
    /// city-state research), and M5 R2b: with every R2b layer stripped too (TestConfigs.PreR2b — weather
    /// geography, unrest/Dignity, Age military realization), so every older control stays unmoved; the R2a, R2b
    /// and R2c (merged) controls are at the end of the file.</summary>
    private static Sim.Core.Systems.SimConfig PreR1() =>
        TestUtil.TestConfigs.PreRecipeKnowledge(TestUtil.TestConfigs.PreTradeKnowledge(TestUtil.TestConfigs.PreR2b(TestUtil.TestConfigs.Sim())));

    /// <summary>M5 R2b: the canonical config with only the R2b layers stripped — on the merged tree, the R2a world.</summary>
    private static Sim.Core.Systems.SimConfig PreR2b() => TestUtil.TestConfigs.PreR2b(TestUtil.TestConfigs.Sim());

    /// <summary>R2a: the canonical config with only the R2a trade-knowledge layer stripped — on the merged tree, the R2b world.</summary>
    private static Sim.Core.Systems.SimConfig PreR2a() => TestUtil.TestConfigs.PreTradeKnowledge(TestUtil.TestConfigs.Sim());

    /// <summary>R2c: both R2 layers stripped — the R1 world (m5-integration @ f1fe76f).</summary>
    private static Sim.Core.Systems.SimConfig PreR2() =>
        TestUtil.TestConfigs.PreTradeKnowledge(TestUtil.TestConfigs.PreR2b(TestUtil.TestConfigs.Sim()));

    private static Sim.Core.Systems.SimConfig Unarmed()
    {
        Sim.Core.Systems.SimConfig shipped = PreR1();
        Assert.Equal(0.0, shipped.Disaster.HazardPerYear);   // inert, T4.21-7 / CR-016
        return shipped with { Disaster = shipped.Disaster with { HazardPerYear = 0.0 } };
    }

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheM4SchemaAlone()
    {
        // main (070f05b) carried this value, and T4.4 deliberately left it
        // UNMOVED as its own no-unrelated-movement control. If it reappears with
        // the v23 trailer removed, M4's tables are the entire cause here too.
        const string mainValue = "0f94b4ad95b8821d19b24d208d56ecc1d2be755ced2d89c539249855ebc23745";

        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(mainValue, HashAtSchemaV22(world));
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheM4SchemaAlone()
    {
        // T4.21-2 (ADR-025): re-measured — OLD f886efbd159f5717848534efe3af61b8
        // 26fa599d742e5e244d7afafa067bce22 (v22), e48d9bcd8883204bb2efa4843923c49a
        // 30de86fae9269e7354e6d2018bf8e1f7 (v23); bounded migration moved this world.
        // main's post-T4.4 pin. Reappearing under the control proves the
        // capacity-floor fix does NOT reach this world — consistent with the
        // pre-integration measurement, which found the fix's blast radius to be
        // the driven golden only.
        // T4.19 lane C: re-measured on the corrected founding vector (tuning
        // data; SnapshotTests.FoundedGolden carries the record and the 41-table
        // turn-0 control). OLD f25c5dd3947a53827c1d9615a7e351108c05258bb0ffe0b1ab1a269e9a4626c6.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) f886efbd159f5717848534efe3af61b826fa599d742e5e244d7afafa067bce22;
        // OLD (T4.21-2 alone) 05eb271cfec6226716d58eb6ca10859ae2f7a91273b0978574509d96fbb806c3;
        // OLD (T4.21-3 alone) c870354f478551a199617e7525e51fd06bffa22f9ccaeb2015956f117c6f4828.
        const string mainValue = "364760291d6dd6917cd38ad79dd10da8d23255bbe2280da3b91f0e4eab194100";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                Unarmed(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), Unarmed(), 42), 300);

        Assert.Equal(mainValue, HashAtSchemaV22(world));

        // M4-C LAYER. This world is FOUNDED, so it now carries real Empire rows.
        // Emptying just those rows — leaving the v23 prefixes in place — must
        // return the pre-M4-C pin byte for byte. That is what proves founding's
        // Empire state is the WHOLE delta: no population, food, terrain, deposit,
        // path, production, demographic, migration or economic state moved with
        // it, because any such drift would survive the strip and break this.
        // T4.19 lane C: OLD 16a1c17150f210b90a8c4d866f16a1767bdc13f218f880304f2449437625e015.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) e48d9bcd8883204bb2efa4843923c49a30de86fae9269e7354e6d2018bf8e1f7;
        // OLD (T4.21-2 alone) f49815f0d222a8a269a042611bc110486527d5112e3262fe591f7524266872ab;
        // OLD (T4.21-3 alone) bbbb9aece1b981f74fba310d0b0e40a3764bee0192a357018a64f8e7ed1e5e63.
        const string beforeM4C = "97d89a9afd9ed784e0162b63614d62300a29b046ae61b7017f67b2076121f433";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));

        // ...and the rows really are there, so the strip is not vacuous.
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheM4SchemaAlone()
    {
        // T4.21-2 (ADR-025): re-measured — OLD 69d6cf178fa536e0582874eacf7adec9
        // fbcbc686c5e14a12292f935aa2694550 (v22), 4e7d2e69e7c5ed72444501bed84c341b
        // 50a0d77c25d123ead36498ce9b280d7b (v23); bounded migration moved this world.
        // The fourth pinned world, and the one that most needs a control: T4.4's
        // own history records an earlier revision of it moving this pin
        // BEHAVIOURALLY (the lone settlement colonising its way out of the
        // director's 0%-farm order). So "schema only" here is exactly the claim
        // that must not be taken on trust.
        // T4.19 lane C: re-measured on the corrected founding vector (tuning
        // data; FirstReignTests carries the record). OLD a64a6cf62eb63a4e5c46297fca4e146a543e13cb0f49a53c3687b47da63001e6.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) 69d6cf178fa536e0582874eacf7adec9fbcbc686c5e14a12292f935aa2694550;
        // OLD (T4.21-2 alone) 7cb35cbdc92e3bf0419dcd091343126acb801eb0a7bc0576627c561af47ca2e1;
        // OLD (T4.21-3 alone) f424daa7da78568c23f475dd0b24dce31ee3b15f8ccf7d50fbf2713e0300d325.
        const string mainValue = "23b0db02a8e2cfef199598bde7b69ebc1e5df09b7516dab5903df3784aacf43c";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(mainValue, HashAtSchemaV22(world));

        // M4-C LAYER — this world is FOUNDED too, so it also carries Empire rows.
        // T4.19 lane C: OLD f79714f955c31cf0f25d323c045a0c1935345e92908fa78758bc8266c6b8ef0b.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) 4e7d2e69e7c5ed72444501bed84c341b50a0d77c25d123ead36498ce9b280d7b;
        // OLD (T4.21-2 alone) e877c79683265a6dd09055861541c731ecfdfed2eee0188cb6939974541cf6f5;
        // OLD (T4.21-3 alone) 89bb4b64a8c7d664c6d89d843c0255463ee49171c4d12707b4cabac181e537ee.
        const string beforeM4C = "fc5d2646bdb8bf1831376eb09b9389bdf8ea059def5dab3ed67b8357d65d6fe9";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_SeparatesTheSchemaMoveFromTheBehaviouralOne()
    {
        // This is the one golden with TWO causes, so the control has to show them
        // apart. With the v23 trailer removed the stream is at T4.4's v22 — and it
        // must NOT equal main's pin, because the capacity-floor fix genuinely
        // changes this world. What the control establishes is that the difference
        // between the integrated pin and this value is the M4 schema alone, and
        // the difference between this value and main's pin is the behavioural fix
        // alone.
        const string mainPinBeforeTheFix = "5b204b455cc5d0ef03031f7b0606af9d491ecc3d2d2c0d68bdb60a3bbd0b69cb";

        // T4.19 — RE-PINNED under the CR-014 ruling. Lane C's founding vector
        // moved this world AND carried it into a latent Craft overdraw at turn
        // 213 (weaving, fiber, settlement 10: stock 66, cap 22 x 3 = 66, banked
        // ConsumeRemainder 0.9999999999999929, exactIn 67.0, Throw). The cap now
        // includes the bank (option 1) and the world completes. The two causes
        // are measured apart in DrivenGoldenTests: the cap change on the OLD
        // vector returns the old pin byte for byte at turn 300 (a three-turn
        // transient at 273-275, closed by 276), so the movement of every
        // constant here is the founding vector; the cap change is what lets it
        // be measured at all. Both constants below re-measured on this tree via
        // HashAtSchemaV22 / HashAtSchemaV23; the founded, FirstReign and
        // synthetic controls in this file are UNMOVED (run, not assumed).
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, Unarmed());
        string atV22 = HashAtSchemaV22(world);

        Assert.NotEqual(mainPinBeforeTheFix, atV22);
        Assert.Equal(CapacityFloorFixAtSchemaV22, atV22);

        // M4-C LAYER — founded world, so Empire rows land here as well. Three
        // causes now compose in this one pin, and each is measured separately.
        // T4.19: OLD e2f3c0426f504077c8536f51f7784a7fa2b5925bc85c95ef715b7931f64851ab.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) cf93e0fed26a3e28e9e971498f8240c1534a5a8aa97391fe030adeaf76d4fa75;
        // OLD (T4.21-2 alone) bdf29a88dcec3385d9bd6ccb8938877ef88459629b10eccf33707e45dcdb0c15;
        // OLD (T4.21-3 alone) edb812c3b2940498e779c0f81002f69db74b7b5023e5f78a72cb8e35d152a9c0.
        const string beforeM4C = "6c901bab51b34819c5a961a48d9d18daa36c334419711c3a1ec35af85d99c445";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    // ======================================================================
    // T4.21-1 — THE LAYOUT-ONLY CONTROL FOR SCHEMA v25 + THE DISASTER STREAMS
    // ======================================================================

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV25TrailerAlone()
    {
        // The toy pipeline has no disaster system, so this synthetic world gains
        // NO stream rows — its whole movement is the empty Disasters prefix.
        // The pre-packet pin (SnapshotTests.GoldenHash at 45046eb). UNMOVED by
        // T4.21-2 (bounded migration): no distances, no migration — this is the
        // no-unrelated-movement control for that packet too.
        const string beforeT421 = "eec82711bbb257ea4ad2a6537ae31945cede7008f1c512b99af936831e3afe69";

        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.Equal(0, removed);
        Assert.Equal(0, world.Disasters.Count);
        Assert.Equal(31, CanonicalSchema.Version);   // v31: ADR-033 D6/D10 Institutions + ConstructionLabor
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheDisasterLayoutAlone()
    {
        // T4.21-2 (ADR-025): the pre-packet pin no longer returns — bounded
        // migration moved this world; re-measured on this tree (OLD
        // 917993b2b5367cd6141c46f4b0d2d81bfd74516198b87209a82be6a643637d62 —
        // the pre-T4.21 pin, which this control returned byte for byte at
        // 1735d41). What survives is separability: the strip is non-vacuous.
        // The pre-packet pin (SnapshotTests.FoundedGolden and ci.yml's
        // FOUNDED_GOLDEN at 45046eb). hazardPerYear = 0 ⇒ no row is ever
        // written, ProductionSystem multiplies by 1.0 exactly, and the ONLY
        // thing in the stream that is not in the pre-packet stream is one
        // RngStreamRow per settlement plus the empty v25 prefix.
        // T4.21-2 ∥ T4.21-3 MERGE: BEHAVIOUR moved this world twice (header;
        // SnapshotTests.FoundedGolden), so the stripped value can no longer
        // return the pre-T4.21 pin
        // (917993b2b5367cd6141c46f4b0d2d81bfd74516198b87209a82be6a643637d62);
        // it is re-measured ON THE MERGED TREE and the control now proves the
        // disaster rows are separable from the output. The difference between
        // this value and the pre-T4.21 pin IS the two packets' behaviour,
        // attributed there.
        // OLD (T4.21-2 alone) 7ee73714b054cd3250a0c9a37a59677581cb74a0469e20b29e9bcde65fc05ed6;
        // OLD (T4.21-3 alone) 776616ce4e78ca8cff586a891cf495840b9e48df1e9d29bc9b5e5ecede21b566.
        const string beforeT421 = "1588d1c4a9597a5c69291908236a48869fe8b4bf1ccc4d6b0ee4d639def41372";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                Unarmed(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), Unarmed(), 42), 300);

        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.Equal(world.Settlements.Count, removed);   // one disaster stream per settlement — not vacuous
        Assert.Equal(0, world.Disasters.Count);           // hazard 0: no row, ever
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheDisasterLayoutAlone()
    {
        // T4.21-2 (ADR-025): re-measured on this tree (OLD 5ee8119e365ad04bdfc45f
        // 791a8962bb0fb616016ad1616c67b9c74c2d81e9a returned byte for byte at
        // 1735d41); bounded migration moved this world.
        // The pre-packet pin (FirstReignTests at 45046eb). This is the world
        // whose lone settlement dies under the director's 0%-farm order — an
        // ABANDONMENT famine under the new classification, and this control is
        // what proves the classification's existence moved nothing: FoodState
        // is a static nothing in the pipeline calls yet.
        // T4.21-2 ∥ T4.21-3 MERGE: both packets moved this world (FirstReignTests
        // has the joint record) — the classification is now READ (FAMINE/
        // Abandonment feeds the kernel), the turn-2 headroom hold applies, and the
        // flight is the bounded hazard. Re-measured ON THE MERGED TREE.
        // OLD (pre-packet) 5ee8119e365ad04bdfc45f791a8962bb0fb616016ad1616c67b9c74c2d81e9a;
        // OLD (T4.21-2 alone) 8e23b4b32e3a1e0257df6af8fd482d8f5cb177345bb3378f2b4061ac8a561e72;
        // OLD (T4.21-3 alone) 77454c587a98f8fadb2ad055ca00509612f32a2a7aa73762e2418e0da30ebdfb.
        const string beforeT421 = "c20bb5a688c132ad050a741d495bfc7497c0c9772f4aeaf79b37cd33ecee1109";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.True(removed >= 1, "no disaster stream rows to strip — control vacuous");
        Assert.Equal(0, world.Disasters.Count);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheDisasterLayoutAlone()
    {
        // The pre-packet pin (DrivenGoldenTests at 45046eb).
        // T4.21-2 ∥ T4.21-3 MERGE: both packets moved this world (DrivenGoldenTests
        // has the joint record); re-measured ON THE MERGED TREE.
        // OLD (pre-packet) 76f82629abbffbc3c0897d2cfab7933e890a5441697dfdb82a59cd64d74163a6;
        // OLD (T4.21-2 alone) cee0c2c0… (see docs/t4.21-2-record.md §2);
        // OLD (T4.21-3 alone) d6a0554e6c419cc6b8f3bd364b30f9721fb9b1ad1fb00675f265ae309ab1f1a1.
        const string beforeT421 = "38de16bab469505e4a4b1966681da8bfd9103a4a96d3bb7c09951496aa0479fe";

        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, Unarmed());
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.Equal(world.Settlements.Count, removed);
        Assert.Equal(0, world.Disasters.Count);
    }

    // ======================================================================
    // ADR-029 — THE LAYOUT CONTROL FOR SCHEMA v26 + THE RESEARCH ROWS
    // ======================================================================
    // Each constant below is the value the world's own golden carried on main at
    // 93270cd, before the research packet. The strip returning it BYTE FOR BYTE is
    // the measurement that the research layer is the ENTIRE delta.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV26ResearchTrailerAlone()
    {
        // The toy pipeline runs no research system: its whole movement is seven empty
        // count prefixes (SnapshotTests.GoldenHash on main).
        const string beforeResearch = "b6df7edd362e15de908526c6343f50f920f3a344b7dac703aaad7671c41adaa1";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out int removed));
        Assert.Equal(0, removed);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheResearchLayerAlone()
    {
        // SnapshotTests.FoundedGolden and ci.yml's FOUNDED_GOLDEN on main.
        const string beforeResearch = "db7c7a0907ad43353b1a44f1a957a407c0ce89ecbb2bf105b170b4b316cc82d9";
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                Unarmed(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), Unarmed(), 42), 300);
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out _));
        Assert.Equal(0, world.ResearchTargets.Count);    // nobody chose a target
        Assert.Equal(0, world.ResearchCompleted.Count);
        // MEASURED (research finalization, addendum A): with no order, none of the 18 evaluable
        // curated Eurekas holds on an available node within 300 turns, so the order-less arm
        // writes NO research row and its strip is vacuous on its own. The research-DRIVEN arm
        // below supplies the teeth: the same world with a target chosen every time one is free.
        WorldState researched = RunWithResearchDriver(
            Sim.Core.Worldgen.WorldFounding.Found(TestUtil.TestConfigs.Worldgen(), Unarmed(), 42),
            new OrderLog(), Unarmed(), 300, researchFrom: 0);
        Assert.Equal(beforeResearch, HashAtSchemaV25(researched, out int removed));
        Assert.True(removed > 0, "no research rows to strip — control vacuous");
        Assert.True(researched.ResearchCompleted.Count > 0, "research completed nothing — control vacuous");
    }

    /// <summary>
    /// Runs a founded world with the measurement driver of ResearchDeterminismTests appending a
    /// SetResearchTarget for the cheapest available node whenever the player has no target,
    /// from turn <paramref name="researchFrom"/> on, on top of whatever <paramref name="orders"/>
    /// already holds. Research writes only its own
    /// tables, so the stripped stream must equal the pre-research pin byte for byte.
    /// </summary>
    private static WorldState RunWithResearchDriver(WorldState world, OrderLog orders, Sim.Core.Systems.SimConfig cfg, int turns, int researchFrom)
    {
        Sim.Core.Systems.Research.ResearchContent content = TestUtil.TestConfigs.Research();
        var player = new PolityId(1);
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestUtil.TestConfigs.Worldgen())), orders);
        for (int t = 0; t < turns; t++)
        {
            if (world.Clock.Turn >= researchFrom && !ResearchQuery.TryGetTarget(world, player, out _)
                && ResearchQuery.CheapestAvailable(world, content, player) is { } pick)
                orders.Append(OrderRecord.From(world.Clock.Turn, player, OrderKind.SetResearchTarget, pick.Value, 0.0));
            world = executor.Step(world);
        }
        return world;
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheResearchLayerAlone()
    {
        // FirstReignTests' golden on main.
        const string beforeResearch = "dacf3c34824a866726861be64480da4b7fe913a8a80bd4a72f51bc282ec1fe3e";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out _));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheResearchLayerAlone()
    {
        // DrivenGoldenTests' golden on main.
        const string beforeResearch = "98ee3a7acdcad9a9cb93870ec3d66d80c4559f8c430ce9d5329b251f010f5cdb";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, Unarmed());
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out _));
        // The order-less research arm writes no research row (measured; see the founded control),
        // so the teeth are the same driven world with the research driver added to its orders.
        WorldState founded = Sim.Core.Worldgen.WorldFounding.Found(TestUtil.TestConfigs.Worldgen(), Unarmed(), 42);
        WorldState researched = RunWithResearchDriver(founded,
            DrivenGoldenTests.DrivingOrders(founded.Settlements.Count), Unarmed(), 300,
            researchFrom: 2); // the log is append-only in turn order, and its batch is stamped turn 2
        Assert.Equal(beforeResearch, HashAtSchemaV25(researched, out int removed));
        Assert.True(removed > 0, "no research rows to strip — control vacuous");
        Assert.True(researched.ResearchCompleted.Count > 0, "research completed nothing — control vacuous");
    }
    // ======================================================================
    // ADR-031 — THE LAYOUT CONTROL FOR SCHEMA v28 (Ages and military formations)
    // ======================================================================
    // Each constant is the pin as it stood on this branch at 3e5647f, BEFORE ADR-031.
    // Stripping the five Age/military tables and dropping their count prefixes must return
    // it BYTE FOR BYTE: the Age systems draw no RNG, write no ledger flow, and the founding
    // formations are tokens — so the v28 tables are the ENTIRE cause of every re-pin.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV28AgeTrailerAlone()
    {
        const string beforeAges = "c7bb78dc2164b335c87a819e6e2084ab4d0ddc5f4932597cece5eaac9c6052e8";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.Equal(0, removed); // the toy pipeline runs no Age system and founds no formation
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheAgeLayerAlone()
    {
        const string beforeAges = "e4279f655c5c25d8de3652d37d966984f9ee1611736bf0c546d85fb77fdfbb18";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.True(removed > 0, "no Age/military rows to strip — control vacuous");
        Assert.Equal(1, world.MilitaryUnits.Count); // the founding warband, unconverted: nobody advanced
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheAgeLayerAlone()
    {
        const string beforeAges = "4291b3e15006e3f110cba311114326bc811c0c7de4295ec2db27184f51d62db7";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.True(removed > 0, "no Age/military rows to strip — control vacuous");
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheAgeLayerAlone()
    {
        const string beforeAges = "464aac3d5ece3ea9e78e8a1034731467b9b155f5d5199c264db114f65d70ae47";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.True(removed > 0, "no Age/military rows to strip — control vacuous");
    }

    // ======================================================================
    // ADR-032 — THE LAYOUT CONTROL FOR SCHEMA v29 (the inter-city transport graph)
    // ======================================================================
    // Each constant is the pin as it stood on this branch at c8ceb5f, BEFORE ADR-032.
    // Stripping the two transport tables and dropping their count prefixes must return it
    // BYTE FOR BYTE. None of these runs issues a DevelopRoads order, so the tables are EMPTY
    // (removed == 0 is the point, not a vacuity): RoadDevelopmentSystem returns at its first
    // loop, draws no RNG and writes no ledger flow, and the research content change (motor_road,
    // the dry-dock requirement, the track_road wording) moved no research choice either — the
    // two empty prefixes are the ENTIRE cause of every re-pin.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV29TransportTrailerAlone()
    {
        const string beforeRoads = "498635bf3c2673b9b544582e17774381b7a785903d7ed320e2dd8e44ddfd4296";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheTransportLayoutAlone()
    {
        const string beforeRoads = "15c63d6564ff8092cae67bc90518b525655a6a38f67723beb44930aa8af83fcd";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed); // no order log: no road is ever developed
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheTransportLayoutAlone()
    {
        const string beforeRoads = "259c13cf27ea3bed62cd8a0018469a85858b51f546486acb8046dc3577014050";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed); // the first-reign log carries no DevelopRoads order
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheTransportLayoutAlone()
    {
        const string beforeRoads = "f94b01eb509853e252a399e103c8d82d7939013d3cca78419b090cc085e2083f";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed); // the driven log carries no DevelopRoads order
    }

    // ======================================================================
    // ADR-033 D4 — THE LAYER CONTROL FOR SCHEMA v30 (the M5 governing loop)
    // ======================================================================
    // Each constant is the pin as it stood on m5-integration at ffbb6f8, BEFORE the governing loop
    // was ported (schema v29). Stripping the governance layer — the TaxPolicies rows, the reach
    // GovernanceSystem writes into ControlRow.Strength (restored to the 1.0 founding and colonization
    // wrote), and the empty v30 count prefix — must return it BYTE FOR BYTE. None of these runs
    // levies a tax (no SetTaxRate order; and none could pass the research gate), so the effective tax
    // rate is exactly 0 everywhere, production multiplies by exactly 1.0, happiness by exactly 1.0,
    // and the new table plus the Strength writes are the ENTIRE cause of every re-pin. GovernanceSystem
    // draws no RNG and writes no ledger flow. A leak of the governing loop into population, food,
    // migration, trade or anything else would survive the strip and break these.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV30GovernanceTrailerAlone()
    {
        // The toy pipeline runs no governance system and the synthetic world has no control rows:
        // its whole movement is one empty count prefix.
        const string beforeGovernance = "4c051fd40e9b86610ea7e2245daaff55d6503074ace491a986b41959f4f73151";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out int restored));
        Assert.Equal(0, restored);
        Assert.Equal(0, world.TaxPolicies.Count);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone()
    {
        const string beforeGovernance = "b2c0032f9e0a726627e85e6b4856ff454624d7f89d11492ae8cf963ae2a50ea0";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out int restored));
        // Non-vacuous: the founded world's non-capital settlements carry a computed reach < 1.
        Assert.True(restored > 0, "no Strength differed from 1.0 — the reach computation is invisible and the control vacuous");
        Assert.Equal(0, world.TaxPolicies.Count);   // no order log: no tax is ever levied
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheGovernanceLayerAlone()
    {
        const string beforeGovernance = "c805ca10e1e24ae686f5a0d59564e77ceecb9fd50de5aad51489887fc61455c3";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out _));
        Assert.Equal(0, world.TaxPolicies.Count);   // the first-reign log carries no SetTaxRate order
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone()
    {
        const string beforeGovernance = "0460e6e916d1b2bb0d39595c1daa5772879ca3d39d90334474b32da003aeee3a";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out int restored));
        Assert.True(restored > 0, "no Strength differed from 1.0 — control vacuous");
        Assert.Equal(0, world.TaxPolicies.Count);   // the driven log carries no SetTaxRate order
    }

    // ======================================================================
    // ADR-033 D6 + D10 — THE LAYER CONTROL FOR SCHEMA v31 (institutions; construction spent once)
    // ======================================================================
    // Each constant is the pin as it stood on m5-integration at 5e7fa35, BEFORE the institutions layer (schema
    // v30). Stripping it — the Institutions and ConstructionLabor rows, the ResearchCostModifiers rows its system
    // now rebuilds, and the two empty v31 count prefixes — must return it BYTE FOR BYTE. None of these runs
    // founds a university (no EnqueueConstruction order of a university project; none could pass the knowledge
    // gate) and none completes any construction project (no EnqueueConstruction at all), so: the labour reader
    // returns the raw adult count (production, housing, paths and capacity unchanged bit for bit), the mortality
    // seam returns the literal 1.0 (demographics unchanged), PathBuild subtracts no construction labour (D10 null
    // arm), and the cost-modifier table stays empty. removed == 0 is the point, not a vacuity (the ADR-032
    // precedent): a leak of the layer into population, food, paths, research or anything else would survive the
    // strip and break these.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV31InstitutionsTrailerAlone()
    {
        // The toy pipeline runs no institutions or construction system: its whole movement is two empty prefixes.
        const string beforeInstitutions = "bbcac0469b61ff494fee410179f937e62505fa23ad183afce00d89b8c3f8333c";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheInstitutionsLayoutAlone()
    {
        const string beforeInstitutions = "64820f83239f005e84ef2965a5564ff46a513434d550ad43a449a58fff5f17ce";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);   // no order log: nothing is built, no university is founded
        Assert.Equal(0, world.Structures.Count);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheInstitutionsLayoutAlone()
    {
        const string beforeInstitutions = "3613dcc4aa059755fc9eb4ab8b353879c93d428d7915bac673b44f4a83366ba3";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);   // the first-reign log carries no EnqueueConstruction order
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheInstitutionsLayoutAlone()
    {
        const string beforeInstitutions = "638d7a0914f0475f539354e47c99673b615ca52f4e353b03bec724c77392cb7a";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);   // the driven log carries only SectorAllocation orders
    }
    // ======================================================================
    // R1 (research → gameplay unlock pipeline, 2026-10-03) — THE RECIPE-KNOWLEDGE LAYER
    // ======================================================================
    // R1 links each goods.json recipe to a research.json recipe entity, and ProductionSystem runs a recipe only
    // when its entity is knowledge-eligible for the settlement's controller (CraftingQuery). pottery-firing
    // requires pottery_open_fired and bronze-casting tin_bronze; none of these runs completes either node, so
    // from turn 1 no settlement fires pottery or casts bronze (and toolmaking, which reads bronze, has none).
    // That is BEHAVIOUR, so no strip of state can return the old pins: the control is the CONTENT twin. Removing
    // the four recipe links (TestConfigs.PreRecipeKnowledge — a pure data change, the T4.21-4 precedent) must
    // return each pre-R1 pin BYTE FOR BYTE: the links are the entire delta, and nothing else in the R1 commit
    // set moves simulation output. Each OLD constant is the pin on m5-integration at ac13c3d.

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheRecipeKnowledgeLayerAlone()
    {
        const string preR1 = "74306d6a574b6a680e454eb385f88e9df2d6a74c5c16fd9af64930c3cdc55c1c";
        Assert.Equal(preR1, WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden(PreR1())));
        string now = WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden(PreR2()));
        Assert.True(now == PostR1FoundedGolden, "R1 founded hash " + now);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheRecipeKnowledgeLayerAlone()
    {
        const string preR1 = "481d37170d7f70f35950a358cbb831c2c78f668642cdd529f7dbfd806853ef87";
        Assert.Equal(preR1, WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1())));
        string now = WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2()));
        Assert.True(now == PostR1FirstReignGolden, "R1 first-reign hash " + now);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheRecipeKnowledgeLayerAlone()
    {
        const string preR1 = "65d53a01ffe1b3e9065cd48100698ac909e3e5b44e1c96f0f32dd5d6c6dbd651";
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(preR1, WorldHash.ComputeHex(twin));
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR2());
        string now = WorldHash.ComputeHex(world);
        Assert.True(now == PostR1DrivenGolden, "R1 driven hash " + now);
    }

    // ======================================================================
    // M5 R2b (governance and weather fixes, 2026-10-03) — THREE BEHAVIOURAL LAYERS
    // ======================================================================
    // (1) WEATHER GEOGRAPHY: the harvest-weather kernel reads straight-line site distance, not road-aware travel
    //     cost; (2) UNREST: Dignity bound to the tax burden (D-035-D) and grievance → protest/discharge/uprising
    //     (D-021 unrest-lite); (3) AGE MILITARY REALIZATION: formations facts count only identities realized at the
    //     Age being entered. All three are BEHAVIOUR (or a published eligibility row), so the control is the
    //     CONTENT/CONFIG twin (TestConfigs.PreR2b), which must return each R1 pin BYTE FOR BYTE: the three layers
    //     are the entire delta. Each OLD constant is the pin on m5-integration at f1fe76f.

    internal const string PostR1FoundedGolden = "68c629b66badfa717514e91c6efe9aaa2f8874d272d21afcee283fe1d85ce655";
    internal const string PostR1FirstReignGolden = "158bdd4cd2ee9cc363a3ae423bec0c16fa3ba92b4e4459c390eeeac58f2e6465";
    internal const string PostR1DrivenGolden = "7aa20e40f4d3ba9fbc060aa510b0e6868893d34c122b07060ee84407f01f5372";

    // R2c (merge of R2a + R2b, 2026-10-03): on the merged tree each stream's layer is stripped separately and
    // jointly. MEASURED: R2a moves only the driven pin; R2b moves all three; the two layers compose without
    // interaction on these pins (stripping one returns exactly the other stream's own pin).
    internal const string R2aOnlyDrivenGolden = "75124c12e61f8999d9746f213255b29199f89cc669608644f42845e100ce1dfe";
    internal const string R2bOnlyFoundedGolden = "1368df9fb3d233b12263cabcfd6291dcd3fad8295f0a645a070813dea6aa467b";
    internal const string R2bOnlyFirstReignGolden = "680a20c5616520f2fbc3b2df97bb99f49ad2adf86edf3f45b0fe7beefaff2b8d";
    internal const string R2bOnlyDrivenGolden = "58e4c47cca90b2e46d6fb3961ddf2034bf5bfb57dcbd5998bc3a488c4a5952f2";

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheR2bLayersAlone()
    {
        Assert.Equal(PostR1FoundedGolden, WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden(PreR2b())));
        Assert.Equal(PostR1FoundedGolden, WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden(PreR2())));
        Assert.Equal(SnapshotTests.FoundedGoldenHash, WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden()));
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheR2bLayersAlone()
    {
        Assert.Equal(PostR1FirstReignGolden, WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2b())));
        Assert.Equal(PostR1FirstReignGolden, WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2())));
        Assert.Equal(Sim.Tests.Systems.FirstReignTests.PostR1Golden, WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _)));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheR2bLayersAlone()
    {
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, PreR2b());
        Assert.Equal(R2aOnlyDrivenGolden, WorldHash.ComputeHex(twin));
        (WorldState both, _) = DrivenGoldenTests.RunDriven(300, PreR2());
        Assert.Equal(PostR1DrivenGolden, WorldHash.ComputeHex(both));
    }

    [Fact]
    public void FoundedAndFirstReign_UnmovedByTheTradeKnowledgeLayer()
    {
        Assert.Equal(R2bOnlyFoundedGolden, WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden(PreR2a())));
        Assert.Equal(R2bOnlyFirstReignGolden, WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2a())));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheTradeKnowledgeLayerAlone()
    {
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, PreR2a());
        Assert.Equal(R2bOnlyDrivenGolden, WorldHash.ComputeHex(twin));
        // Which part of the layer: the TRADE GATE alone (node, entity and city-state research kept; only
        // sim.json trade.entity removed) returns the R2b-only pin too — the gate is the entire R2a cause here.
        Sim.Core.Systems.SimConfig ungated = TestUtil.TestConfigs.Sim();
        (WorldState gateOnly, _) = DrivenGoldenTests.RunDriven(300, ungated with { Trade = ungated.Trade with { Entity = null } });
        Assert.Equal(R2bOnlyDrivenGolden, WorldHash.ComputeHex(gateOnly));
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300);
        Assert.Equal(DrivenGoldenTests.Golden, WorldHash.ComputeHex(world));
    }
}
