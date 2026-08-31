using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.State;

/// <summary>
/// M4 packet §14 items 7 and 8 — the two requirements the Empire foundation did
/// NOT ship a test for. Both are about what must NOT happen.
///
/// Item 7 is the load-bearing one: player and AI are supposed to be the same
/// simulation, distinguished only by who issues the orders. A test that merely
/// reads back the CommandSource field would prove nothing about that — the claim
/// is about the SIMULATION, so it has to be measured on the simulation's own
/// output, and the only observable strong enough is the canonical world hash.
/// </summary>
public class EmpireOrderSeamTests
{
    private const int Turns = 20;

    /// <summary>
    /// Runs the identical order log through the identical pipeline with the
    /// issuing polity registered under <paramref name="source"/>, then NORMALISES
    /// the roster row to a fixed value before hashing.
    ///
    /// The normalisation is the whole trick, and it is what an earlier version of
    /// this test got wrong. The command source is itself a serialized field, so
    /// hashing the two runs raw compares "did the simulation diverge?" TOGETHER
    /// with "does the roster byte differ?" — and the second is true by
    /// construction, so the comparison can never fail for the reason we care
    /// about. Stamping both rosters to the same value removes the byte from the
    /// comparison and leaves exactly the question worth asking: given the same
    /// orders, does the WORLD come out the same?
    /// </summary>
    private static string RunUnderCommandSource(CommandSource source)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);

        // The Empire that issues the orders, and the settlements it controls.
        var polity = new PolityId(1);
        world.Polities.Add(new PolityRow(polity, source));
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            world.Controls.Add(new ControlRow(polity, world.Settlements[s].Id, 1.0));
        }

        world.Capitals.Add(new CapitalRow(polity, world.Settlements[0].Id));

        OrderLog orders = Sim.Tests.Kernel.DrivenGoldenTests.DrivingOrders(world.Settlements.Count);
        OrderValidation.ValidateAgainstWorld(orders, world);

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())),
            orders);

        WorldState final = exec.Run(world, Turns);

        // Normalise the one field that differs BY CONSTRUCTION between the two
        // runs, so the hash compares the simulated world and nothing else.
        for (int i = 0; i < final.Polities.Count; i++)
        {
            final.Polities[i] = new PolityRow(final.Polities[i].Id, CommandSource.Ai);
        }

        return WorldHash.ComputeHex(final);
    }

    [Fact]
    public void AnOrderEntersThePipelineIdentically_WhetherThePlayerOrTheAiIssuedIt()
    {
        // §14.7. The ONLY difference between the two runs is the CommandSource
        // byte on the roster row. Everything downstream — the order log, the
        // pipeline, the seed, the controlled set, the capital — is identical.
        //
        // The hashes must be identical too. If any system ever branches on human
        // vs AI ownership, or routes player orders down a second path, the
        // trajectories separate and this fails. That is the whole point: the
        // command source says WHO decided, never WHAT the world does.
        //
        // The roster byte is normalised away before hashing (see the helper), so
        // this compares the SIMULATED WORLD and nothing else. The companion test
        // below proves the byte really is in the stream, which is what stops that
        // normalisation from quietly turning this into a tautology.
        string asPlayer = RunUnderCommandSource(CommandSource.Player);
        string asAi = RunUnderCommandSource(CommandSource.Ai);

        Assert.Equal(asPlayer, asAi);
    }

    [Fact]
    public void TheCommandSourceByteIsGenuinelyInTheStream_SoTheEqualityAboveIsNotVacuous()
    {
        // The control for the test above. Two worlds differing ONLY in the
        // command source must hash DIFFERENTLY when nothing else runs — proving
        // the field is serialized and observable. Take that together with the
        // equality above and the conclusion is forced: the byte is visible to the
        // hash, and yet 20 turns of simulation produce the same world, so no
        // system consumed it.
        var player = new WorldState();
        player.Polities.Add(new PolityRow(new PolityId(1), CommandSource.Player));

        var ai = new WorldState();
        ai.Polities.Add(new PolityRow(new PolityId(1), CommandSource.Ai));

        Assert.NotEqual(WorldHash.ComputeHex(player), WorldHash.ComputeHex(ai));
    }

    [Fact]
    public void SettlementControlSemanticsAreDeterministic_AcrossRepeatedRuns()
    {
        // §14.8. Same seed, same orders, same control set — twice. Both the
        // canonical hash and the derived Empire answers must agree exactly.
        string first = RunUnderCommandSource(CommandSource.Ai);
        string second = RunUnderCommandSource(CommandSource.Ai);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EmpireDerivationDoesNotDependOnControlRowInsertionOrder()
    {
        // §14.8, the sharper half. Membership is DERIVED by scanning Controls, so
        // the derivation must be a property of the SET, not of the order rows
        // happened to be appended in. (The canonical stream is deliberately
        // order-sensitive — that is serialization, not semantics — so this
        // asserts on the derived answers, which are what systems will consume.)
        var ascending = new WorldState();
        var descending = new WorldState();
        var polity = new PolityId(4);
        ascending.Polities.Add(new PolityRow(polity, CommandSource.Ai));
        descending.Polities.Add(new PolityRow(polity, CommandSource.Ai));

        int[] places = [10, 11, 12, 13];
        for (int i = 0; i < places.Length; i++)
        {
            ascending.Controls.Add(new ControlRow(polity, new SettlementId(places[i]), 1.0));
            descending.Controls.Add(new ControlRow(polity, new SettlementId(places[places.Length - 1 - i]), 1.0));
        }

        Assert.Equal(
            EmpireQuery.ControlledCount(ascending, polity),
            EmpireQuery.ControlledCount(descending, polity));
        Assert.Equal(EmpireQuery.IsExtinct(ascending, polity), EmpireQuery.IsExtinct(descending, polity));
        Assert.Equal(
            EmpireQuery.TryGetCommandSource(ascending, polity, out CommandSource a),
            EmpireQuery.TryGetCommandSource(descending, polity, out CommandSource d));
        Assert.Equal(a, d);
    }
}
