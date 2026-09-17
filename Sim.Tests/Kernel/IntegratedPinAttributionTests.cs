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
/// T4.21-3 (CR-015 / ADR-026) IS A BEHAVIOURAL PACKET, and — exactly as T4.10
/// did — it changes what the controls can claim. The effective deficit and the
/// headroom growth cap move every founded world (SnapshotTests.FoundedGolden
/// has the cause and the probe), so the stripped values no longer return any
/// pre-T4.21 pin: the T4.21-1 layout controls' reference constants are RE-
/// MEASURED on this tree, and what they prove is the weaker, still-useful thing
/// that the disaster streams and table are separable from the simulation output
/// they sit beside; the v22/v23 constants of the M4 ladder are re-measured the
/// same way. The synthetic terrain-less world (GoldenHashSeed42Turn200, no
/// demand rows — the cap never reads it) is the one that carries the weight
/// now: BOTH its controls are UNMOVED and still return main's pre-M4 and the
/// pre-T4.21 values byte for byte, which is the measurement showing this
/// packet stayed inside the founded worlds' demographics instead of leaking
/// anywhere it had no business being.
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
    /// T4.21-3: re-measured (behaviour — see the header and SnapshotTests
    /// .FoundedGolden). OLD 60bd5b208696a25f93469d58d4a4284d8ae8467107aeee3545d3d0f82b61ba14.
    internal const string CapacityFloorFixAtSchemaV22 =
        "23cdc7042bf2972ba13b52e9e6bb26a8fc526a74ba61284c93208bb73efadffd";

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

        return HashDroppingTrailer(buffer.ToArray(), dropTrailingTables);
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
    /// Five trailing empty tables since v25: Polities, Capitals (v23),
    /// ConstructionQueue, Structures (v24), Disasters (v25).</summary>
    private static string HashAtSchemaV22(WorldState world) => HashWithoutM4(world, 5);

    /// <summary>The stream as v23 — M4-A's tables present but empty, i.e. the
    /// tree exactly as it stood before M4-C's founding wrote them.</summary>
    private static string HashAtSchemaV23(WorldState world) => HashWithoutM4(world, 3);

    /// <summary>
    /// The stream as v24 — the tree exactly as it stood BEFORE T4.21-1: the
    /// disaster streams and table removed, the empty v25 trailer dropped, and
    /// NOTHING ELSE touched (M4's rows stay). The pre-packet pin must return
    /// byte for byte, or the packet changed behaviour.
    /// </summary>
    private static string HashAtSchemaV24(WorldState world, out int disasterStreamsRemoved)
    {
        WorldState stripped = world.Clone();
        disasterStreamsRemoved = StripDisaster(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), 1);
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
        // T4.21-3: re-measured (behaviour; header). OLD f886efbd159f5717848534efe3af61b826fa599d742e5e244d7afafa067bce22.
        const string mainValue = "c870354f478551a199617e7525e51fd06bffa22f9ccaeb2015956f117c6f4828";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                TestUtil.TestConfigs.Sim(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), TestUtil.TestConfigs.Sim(), 42), 300);

        Assert.Equal(mainValue, HashAtSchemaV22(world));

        // M4-C LAYER. This world is FOUNDED, so it now carries real Empire rows.
        // Emptying just those rows — leaving the v23 prefixes in place — must
        // return the pre-M4-C pin byte for byte. That is what proves founding's
        // Empire state is the WHOLE delta: no population, food, terrain, deposit,
        // path, production, demographic, migration or economic state moved with
        // it, because any such drift would survive the strip and break this.
        // T4.19 lane C: OLD 16a1c17150f210b90a8c4d866f16a1767bdc13f218f880304f2449437625e015.
        // T4.21-3: re-measured (behaviour; header). OLD e48d9bcd8883204bb2efa4843923c49a30de86fae9269e7354e6d2018bf8e1f7.
        const string beforeM4C = "bbbb9aece1b981f74fba310d0b0e40a3764bee0192a357018a64f8e7ed1e5e63";
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
        // T4.21-3: re-measured (behaviour; header). OLD 69d6cf178fa536e0582874eacf7adec9fbcbc686c5e14a12292f935aa2694550.
        const string mainValue = "f424daa7da78568c23f475dd0b24dce31ee3b15f8ccf7d50fbf2713e0300d325";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _);
        Assert.Equal(mainValue, HashAtSchemaV22(world));

        // M4-C LAYER — this world is FOUNDED too, so it also carries Empire rows.
        // T4.19 lane C: OLD f79714f955c31cf0f25d323c045a0c1935345e92908fa78758bc8266c6b8ef0b.
        // T4.21-3: re-measured (behaviour; header). OLD 4e7d2e69e7c5ed72444501bed84c341b50a0d77c25d123ead36498ce9b280d7b.
        const string beforeM4C = "89bb4b64a8c7d664c6d89d843c0255463ee49171c4d12707b4cabac181e537ee";
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
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300);
        string atV22 = HashAtSchemaV22(world);

        Assert.NotEqual(mainPinBeforeTheFix, atV22);
        Assert.Equal(CapacityFloorFixAtSchemaV22, atV22);

        // M4-C LAYER — founded world, so Empire rows land here as well. Three
        // causes now compose in this one pin, and each is measured separately.
        // T4.19: OLD e2f3c0426f504077c8536f51f7784a7fa2b5925bc85c95ef715b7931f64851ab.
        // T4.21-3: re-measured (behaviour; header). OLD cf93e0fed26a3e28e9e971498f8240c1534a5a8aa97391fe030adeaf76d4fa75.
        const string beforeM4C = "edb812c3b2940498e779c0f81002f69db74b7b5023e5f78a72cb8e35d152a9c0";
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
        Assert.Equal(25, CanonicalSchema.Version);
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
        // T4.21-3: BEHAVIOUR moved this world (header; SnapshotTests.FoundedGolden),
        // so the stripped value can no longer return the pre-T4.21 pin
        // (917993b2b5367cd6141c46f4b0d2d81bfd74516198b87209a82be6a643637d62);
        // it is re-measured on this tree and the control now proves the disaster
        // rows are separable from the output. The difference between this value
        // and the pre-T4.21 pin IS T4.21-3's behaviour, attributed there.
        const string beforeT421 = "776616ce4e78ca8cff586a891cf495840b9e48df1e9d29bc9b5e5ecede21b566";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                TestUtil.TestConfigs.Sim(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), TestUtil.TestConfigs.Sim(), 42), 300);

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
        // T4.21-3: the classification is now READ (FAMINE/Abandonment feeds the
        // kernel) and the turn-2 hold applies, so this world moved (FirstReignTests
        // has the record); re-measured on this tree — OLD
        // 5ee8119e365ad04dbdfc45f791a8962bb0fb616016ad1616c67b9c74c2d81e9a.
        const string beforeT421 = "77454c587a98f8fadb2ad055ca00509612f32a2a7aa73762e2418e0da30ebdfb";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _);
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.True(removed >= 1, "no disaster stream rows to strip — control vacuous");
        Assert.Equal(0, world.Disasters.Count);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheDisasterLayoutAlone()
    {
        // The pre-packet pin (DrivenGoldenTests at 45046eb).
        // T4.21-3: behaviour moved this world (DrivenGoldenTests has the record);
        // re-measured on this tree — OLD 76f82629abbffbc3c0897d2cfab7933e890a5441697dfdb82a59cd64d74163a6.
        const string beforeT421 = "d6a0554e6c419cc6b8f3bd364b30f9721fb9b1ad1fb00675f265ae309ab1f1a1";

        (WorldState world, _) = DrivenGoldenTests.RunDriven(300);
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.Equal(world.Settlements.Count, removed);
        Assert.Equal(0, world.Disasters.Count);
    }
}
