using System.Security.Cryptography;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;

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
/// </summary>
public class IntegratedPinAttributionTests
{
    /// <summary>
    /// The driven world at T4.4's schema v22 WITH the capacity-floor fix — i.e.
    /// main's tree plus the behavioural fix and nothing else. Measured on the
    /// integrated tree by <see cref="HashAtSchemaV22"/>; the midpoint that lets
    /// the two causes of the driven pin's movement be reported separately.
    /// </summary>
    internal const string CapacityFloorFixAtSchemaV22 =
        "611a1508e650c9b897e3ec3ec0884969ae3add4d8de520fa5a126efbb71926ea";

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
        stripped.Polities.Clear();
        stripped.Controls.Clear();
        stripped.Capitals.Clear();
        stripped.ConstructionQueue.Clear();
        stripped.Structures.Clear();
        stripped.TaxPolicies.Clear();

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }

        byte[] full = buffer.ToArray();
        int drop = dropTrailingTables * EmptyTableBytes;
        for (int i = full.Length - drop; i < full.Length; i++)
        {
            Assert.Equal(0, full[i]);   // what is dropped really is empty tables
        }

        return Convert.ToHexStringLower(SHA256.HashData(full.AsSpan(0, full.Length - drop).ToArray()));
    }

    /// <summary>The stream as T4.4's v22 — before M4 touched the schema at all.</summary>
    private static string HashAtSchemaV22(WorldState world) => HashWithoutM4(world, 5);

    /// <summary>The stream as v23 — M4-A's tables present but empty, i.e. the
    /// tree exactly as it stood before M4-C's founding wrote them.</summary>
    private static string HashAtSchemaV23(WorldState world) => HashWithoutM4(world, 3);

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
        // main's post-T4.4 pin. Reappearing under the control proves the
        // capacity-floor fix does NOT reach this world — consistent with the
        // pre-integration measurement, which found the fix's blast radius to be
        // the driven golden only.
        const string mainValue = "f25c5dd3947a53827c1d9615a7e351108c05258bb0ffe0b1ab1a269e9a4626c6";

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
        const string beforeM4C = "16a1c17150f210b90a8c4d866f16a1767bdc13f218f880304f2449437625e015";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));

        // ...and the rows really are there, so the strip is not vacuous.
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheM4SchemaAlone()
    {
        // The fourth pinned world, and the one that most needs a control: T4.4's
        // own history records an earlier revision of it moving this pin
        // BEHAVIOURALLY (the lone settlement colonising its way out of the
        // director's 0%-farm order). So "schema only" here is exactly the claim
        // that must not be taken on trust.
        const string mainValue = "a64a6cf62eb63a4e5c46297fca4e146a543e13cb0f49a53c3687b47da63001e6";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _);
        Assert.Equal(mainValue, HashAtSchemaV22(world));

        // M4-C LAYER — this world is FOUNDED too, so it also carries Empire rows.
        const string beforeM4C = "f79714f955c31cf0f25d323c045a0c1935345e92908fa78758bc8266c6b8ef0b";
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

        (WorldState world, _) = DrivenGoldenTests.RunDriven(300);
        string atV22 = HashAtSchemaV22(world);

        Assert.NotEqual(mainPinBeforeTheFix, atV22);
        Assert.Equal(CapacityFloorFixAtSchemaV22, atV22);

        // M4-C LAYER — founded world, so Empire rows land here as well. Three
        // causes now compose in this one pin, and each is measured separately.
        const string beforeM4C = "e2f3c0426f504077c8536f51f7784a7fa2b5925bc85c95ef715b7931f64851ab";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    // ---------------------------------------------------------------------
    // M5 LAYER — the governing loop.
    //
    // M5 moves three pinned worlds, and the packet's rule is the same one M4
    // worked under: a moved golden is repinned only with a measured cause. The
    // causes here are THREE, and they are not the same three for every world:
    //
    //   (1) LAYOUT. Schema v25 appends TaxPolicies. Nothing writes it without a
    //       SetTaxRate order, so on every pinned world its whole contribution is
    //       one four-byte zero count prefix.
    //   (2) CONTROL STRENGTH. GovernanceSystem now writes ControlRow.Strength =
    //       administrative reach, where founding and colonization used to leave a
    //       placeholder 1.0. Reach is 1.0 at the capital and decays with travel
    //       cost, so this reaches only worlds holding NON-CAPITAL settlements.
    //   (3) HAPPINESS — RULED OUT BY CONSTRUCTION, and it was not always so. The
    //       first implementation made taxation a third CES factor, which moved
    //       every world with settlements even when untaxed, and disarmed the
    //       ruled revolt condition as well. Taxation now MULTIPLIES the aggregate
    //       instead, and an untaxed realm multiplies by exactly 1.0, so pre-M5
    //       happiness is bit-identical. That is what lets the M4 layer above keep
    //       its original constants rather than being re-measured under M5.
    //
    // So the control below is exact again: strip (1) and (2) and the pre-M5 pin
    // must come back byte for byte, on every pinned world.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The finished world with M5's state removed but M4's left intact: the
    /// TaxPolicies table cleared (and its trailing empty prefix dropped), and
    /// every ControlRow.Strength restored to the placeholder 1.0 that founding
    /// and colonization wrote before GovernanceSystem existed. What survives is
    /// the schema v24 stream, so the comparison is against a real earlier pin.
    /// </summary>
    private static string HashAtSchemaV24(WorldState world, bool restoreUnitStrength)
    {
        WorldState stripped = world.Clone();
        stripped.TaxPolicies.Clear();
        if (restoreUnitStrength)
        {
            for (int i = 0; i < stripped.Controls.Count; i++)
            {
                stripped.Controls[i] = stripped.Controls[i] with { Strength = 1.0 };
            }
        }

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }

        byte[] full = buffer.ToArray();
        for (int i = full.Length - EmptyTableBytes; i < full.Length; i++)
        {
            Assert.Equal(0, full[i]);   // the dropped prefix really is an empty table
        }

        return Convert.ToHexStringLower(
            SHA256.HashData(full.AsSpan(0, full.Length - EmptyTableBytes).ToArray()));
    }

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheM5LayoutAlone()
    {
        // The synthetic terrain-less world: no settlements, so no controls and no
        // happiness. Cause (2) and cause (3) cannot reach it, and the prediction
        // that follows is exact — dropping the one empty count prefix must return
        // the pre-M5 pin byte for byte. This is M5's no-unrelated-movement
        // control, and it is the reason the other two pins can be read as
        // "the governing loop moved them" rather than "something drifted".
        const string beforeM5 = "eec82711bbb257ea4ad2a6537ae31945cede7008f1c512b99af936831e3afe69";

        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(0, world.Controls.Count);
        Assert.Equal(0, world.TaxPolicies.Count);
        Assert.Equal(beforeM5, HashAtSchemaV24(world, restoreUnitStrength: false));
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheM5LayoutAlone()
    {
        // One settlement, and it is the capital — so its administrative reach is
        // exactly 1.0, the same value founding wrote. Cause (2) is therefore
        // NUMERICALLY absent here rather than merely small, and this world tests
        // that claim: the strip returns the pre-M5 pin, which it could not do if
        // the happiness factor had moved this world's migration either.
        const string beforeM5 = "7a9c3de745eac824c5c1b5783d527cf959ada9c558e7012423bea5f92a6361a3";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _);
        Assert.Equal(1, world.Controls.Count);
        Assert.Equal(1.0, world.Controls[0].Strength);
        Assert.Equal(beforeM5, HashAtSchemaV24(world, restoreUnitStrength: false));
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForM5LayoutAndControlStrengthAlone()
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                TestUtil.TestConfigs.Sim(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), TestUtil.TestConfigs.Sim(), 42), 300);

        // Twelve settlements under one polity, so this is the ONE pinned world
        // that cause (2) can reach — and it does: eleven non-capital settlements
        // now carry reach below 1.0 where the placeholder used to sit.
        Assert.Equal(12, world.Controls.Count);
        Assert.Equal(1.0, world.Controls[0].Strength);
        Assert.True(world.Controls[1].Strength < 1.0);

        // LAYOUT + CONTROL STRENGTH REMOVED must return the PRE-M5 PIN itself,
        // byte for byte. Nothing is left over: happiness cannot move an untaxed
        // world, and with no policy row the extraction multiplier is exactly 1.0,
        // so production output is bit-identical too.
        const string layoutAndStrengthRemoved =
            "98a89d18b014fa1726ab3ee611a8662b2982bf4fbac0b10ada00718e4eebd983";
        Assert.Equal(layoutAndStrengthRemoved, HashAtSchemaV24(world, restoreUnitStrength: true));

        // ...and with strength LEFT ALONE the value differs, so the restore above
        // is doing real work rather than passing vacuously.
        Assert.NotEqual(layoutAndStrengthRemoved, HashAtSchemaV24(world, restoreUnitStrength: false));
    }
}
