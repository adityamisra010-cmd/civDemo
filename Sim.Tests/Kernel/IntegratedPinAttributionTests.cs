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
/// The M4 v23 tables are EMPTY in every world — nothing writes them — so their
/// entire contribution to the canonical stream is the two four-byte zero count
/// prefixes appended at the very end. Removing exactly those eight bytes yields
/// the stream this tree WOULD have produced at schema v22. If that stripped
/// stream hashes to main's pinned value, the pin moved for the M4 schema and for
/// nothing else, and the world is otherwise bit-identical to main's.
/// </summary>
public class IntegratedPinAttributionTests
{
    /// <summary>The two empty v23 count prefixes: Polities and Capitals.</summary>
    private const int V23TrailerBytes = 8;

    /// <summary>
    /// The driven world at T4.4's schema v22 WITH the capacity-floor fix — i.e.
    /// main's tree plus the behavioural fix and nothing else. Measured on the
    /// integrated tree by <see cref="HashAtV22"/>; it is the midpoint that lets
    /// the two causes of the driven pin's movement be reported separately.
    /// </summary>
    internal const string CapacityFloorFixAtSchemaV22 =
        "9432d39f5a1618eead13115c889dd77748c118e4699310576704802aa2d0c621";

    private static string HashAtV22(WorldState world)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }

        byte[] full = buffer.ToArray();

        // The trailer must be exactly two empty tables, or this control is
        // measuring something else — assert that before trusting the result.
        Assert.Equal(0, world.Polities.Count);
        Assert.Equal(0, world.Capitals.Count);
        for (int i = full.Length - V23TrailerBytes; i < full.Length; i++)
        {
            Assert.Equal(0, full[i]);
        }

        return Convert.ToHexStringLower(SHA256.HashData(full.AsSpan(0, full.Length - V23TrailerBytes).ToArray()));
    }

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheM4SchemaAlone()
    {
        // main (070f05b) carried this value, and T4.4 deliberately left it
        // UNMOVED as its own no-unrelated-movement control. If it reappears with
        // the v23 trailer removed, M4's tables are the entire cause here too.
        const string mainValue = "0f94b4ad95b8821d19b24d208d56ecc1d2be755ced2d89c539249855ebc23745";

        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(mainValue, HashAtV22(world));
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheM4SchemaAlone()
    {
        // main's post-T4.4 pin. Reappearing under the control proves the
        // capacity-floor fix does NOT reach this world — consistent with the
        // pre-integration measurement, which found the fix's blast radius to be
        // the driven golden only.
        const string mainValue = "87bba71338596b6c179e6c0f5f738e731382e3f877ca4389ef578e517b34990b";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                TestUtil.TestConfigs.Sim(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), TestUtil.TestConfigs.Sim(), 42), 300);

        Assert.Equal(mainValue, HashAtV22(world));
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheM4SchemaAlone()
    {
        // The fourth pinned world, and the one that most needs a control: T4.4's
        // own history records an earlier revision of it moving this pin
        // BEHAVIOURALLY (the lone settlement colonising its way out of the
        // director's 0%-farm order). So "schema only" here is exactly the claim
        // that must not be taken on trust.
        const string mainValue = "bf9312a259fd45d018d93d308fda1ac7d5d5b4ee55203a5526a6ac1939581a5c";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _);
        Assert.Equal(mainValue, HashAtV22(world));
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
        string atV22 = HashAtV22(world);

        Assert.NotEqual(mainPinBeforeTheFix, atV22);
        Assert.Equal(CapacityFloorFixAtSchemaV22, atV22);
    }
}
