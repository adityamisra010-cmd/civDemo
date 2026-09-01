using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M4-D — the settlement construction queue. Every test drives the REAL founding
/// path and the REAL order pipeline; none hand-assembles a world, because what is
/// under test is precisely that a founded, player-controlled settlement can be
/// ordered to build something.
/// </summary>
public class ConstructionTests
{
    private const int Granary = 1;
    private const int Workshop = 2;

    private static WorldState Found(int settlements = 1, ulong seed = 42)
        => WorldFounding.Found(TestConfigs.Worldgen(), TestConfigs.Sim(), seed, settlements);

    private static PolityId PlayerOf(WorldState w)
    {
        for (int i = 0; i < w.Polities.Count; i++)
            if (w.Polities[i].Source == CommandSource.Player) return w.Polities[i].Id;
        throw new InvalidOperationException("founded world has no player Empire");
    }

    private static OrderRecord Enqueue(long turn, PolityId actor, SettlementId place, int project)
        => OrderRecord.From(turn, actor, OrderKind.EnqueueConstruction, place.Value, project);

    private static TurnExecutor Executor(OrderLog orders)
    {
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream,
                SystemCatalog.All(TestConfigs.Sim(), TestConfigs.Worldgen())),
            orders);
    }

    /// <summary>Stock the settlement so materials are never the binding constraint.</summary>
    private static void Endow(WorldState w, SettlementId s, long each = 10_000)
    {
        foreach (string name in new[] { "timber", "stone", "tools" }) TopUp(w, s, name, each);
    }

    /// <summary>
    /// Raise one good's stock to at least <paramref name="target"/>. FIND-OR-ADD,
    /// never a bare Add: a second GoodStockRow for the same (settlement, good) is
    /// invisible to every reader — they all take the FIRST match — so an
    /// endowment written into a duplicate row silently does nothing. That bug
    /// made an earlier version of the queue-blocking test pass for the wrong
    /// reason, which is why this helper exists at all.
    /// </summary>
    private static void TopUp(WorldState w, SettlementId s, string good, long target)
    {
        var id = new GoodId(TestConfigs.Sim().Goods!.IdOf(good));
        int idx = -1;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good == id) { idx = i; break; }
        if (idx < 0) idx = w.GoodStocks.Add(new GoodStockRow(s, id, Conserved.Zero, 0.0, 0.0));

        long have = w.GoodStocks[idx].Amount.Value;
        if (have >= target) return;
        new Ledger(w.LedgerFlows).Flow(
            ref w.GoodStocks.Ref(idx).Amount, ConservedQuantityIds.OfGood(id),
            ReasonIds.InitialEndowment, target - have, FlowDirection.Source, OverdrawPolicy.Throw);
    }

    /// <summary>Put all labour in construction so capacity is never the constraint.</summary>
    private static void AllBuilders(WorldState w, SettlementId s)
        => SetConstructionShare(w, s, 1.0);

    private static void SetConstructionShare(WorldState w, SettlementId s, double share)
    {
        var row = new SectorAllocationRow(s, Farming: 1.0 - share, Herding: 0.0,
            Extraction: 0.0, Crafting: 0.0, Construction: share);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == s) { w.SectorAllocations[i] = row; return; }
        w.SectorAllocations.Add(row);
    }

    private static long StockOf(IReadOnlyWorldState w, SettlementId s, string good)
    {
        var id = new GoodId(TestConfigs.Sim().Goods!.IdOf(good));
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good == id)
                return w.GoodStocks[i].Amount.Value;
        return 0;
    }

    private static long StructureCount(IReadOnlyWorldState w, SettlementId s, int project)
    {
        for (int i = 0; i < w.Structures.Count; i++)
            if (w.Structures[i].Settlement == s && w.Structures[i].ProjectId == project)
                return w.Structures[i].Count;
        return 0;
    }

    // ---- 1. enqueue -------------------------------------------------------

    [Fact]
    public void AnAcceptedOrderPutsTheProjectAtTheHeadOfTheSettlementsQueue()
    {
        WorldState world = Found();
        PolityId player = PlayerOf(world);
        SettlementId place = world.Settlements[0].Id;

        var orders = new OrderLog();
        orders.Append(Enqueue(0, player, place, Granary));
        OrderValidation.ValidateAgainstWorld(orders, world);   // accepted

        // Nothing can be built: no materials, no builders. So the enqueue is
        // observable on its own rather than confounded with completion.
        SetConstructionShare(world, place, 0.0);
        WorldState next = Executor(orders).Run(world, 1);

        Assert.Equal(1, next.ConstructionQueue.Count);
        Assert.Equal(place, next.ConstructionQueue[0].Settlement);
        Assert.Equal(Granary, next.ConstructionQueue[0].ProjectId);
        Assert.Equal(0, next.ConstructionQueue[0].Slot);
    }

    // ---- 2. queue order ---------------------------------------------------

    [Fact]
    public void TheHeadBlocksTheQueue_ALaterProjectIsNotBuiltPastAStalledOne()
    {
        WorldState world = Found();
        PolityId player = PlayerOf(world);
        SettlementId place = world.Settlements[0].Id;
        AllBuilders(world, place);

        // Workshop first and it CANNOT be supplied (tools are never stocked);
        // granary behind it and it demonstrably COULD be. If the head did not
        // block, the granary would appear.
        //
        // The endowment is deliberately enormous. Housing draws on timber too, so
        // a modest stock is drained by turn 3 and the granary then fails for lack
        // of materials rather than for being behind the head — which would make
        // this test pass for the wrong reason and kill nothing. A mutant that
        // reaches past the head was measured to survive the earlier version;
        // this one kills it.
        TopUp(world, place, "timber", 5_000_000);
        TopUp(world, place, "stone", 5_000_000);

        var orders = new OrderLog();
        orders.Append(Enqueue(0, player, place, Workshop));
        orders.Append(Enqueue(0, player, place, Granary));
        WorldState next = Executor(orders).Run(world, 3);

        // THE ANTI-VACUITY GUARD: the granary's materials are still there in
        // quantity, so "not built" can only mean "not reached".
        Assert.True(StockOf(next, place, "timber") > 1_000_000, "timber ran out — test is vacuous");
        Assert.True(StockOf(next, place, "stone") > 1_000_000, "stone ran out — test is vacuous");

        Assert.Equal(0, StructureCount(next, place, Granary));   // NOT built past the head
        Assert.Equal(0, StructureCount(next, place, Workshop));
        Assert.Equal(2, next.ConstructionQueue.Count);
        Assert.Equal(Workshop, next.ConstructionQueue[0].ProjectId);
        Assert.True(next.ConstructionQueue[0].Slot < next.ConstructionQueue[1].Slot);
    }

    // ---- 3. success -------------------------------------------------------

    [Fact]
    public void WithMaterialsAndBuilders_TheProjectCompletesAndLeavesTheQueue()
    {
        WorldState world = Found();
        PolityId player = PlayerOf(world);
        SettlementId place = world.Settlements[0].Id;
        Endow(world, place);
        AllBuilders(world, place);
        long timber0 = StockOf(world, place, "timber");
        long stone0 = StockOf(world, place, "stone");

        var orders = new OrderLog();
        orders.Append(Enqueue(0, player, place, Granary));
        WorldState next = Executor(orders).Run(world, 2);

        Assert.Equal(1, StructureCount(next, place, Granary));
        Assert.Equal(0, next.ConstructionQueue.Count);

        // §23 CONSERVATION: exactly the declared quantities, and no more. Other
        // systems also touch timber and stone, so this asserts the DELTA
        // attributable to construction by re-running with the order withheld.
        WorldState control = Found();
        Endow(control, place);
        AllBuilders(control, place);
        WorldState idle = Executor(new OrderLog()).Run(control, 2);

        Assert.Equal(40, StockOf(idle, place, "timber") - StockOf(next, place, "timber"));
        Assert.Equal(20, StockOf(idle, place, "stone") - StockOf(next, place, "stone"));
        Assert.True(timber0 > 0 && stone0 > 0);
    }

    // ---- 4. insufficient material ----------------------------------------

    [Fact]
    public void MissingOneMaterial_BuildsNothingAndConsumesNothing()
    {
        // Timber in abundance, stone entirely absent. The atomic rule says the
        // timber must be untouched — a partial draw here is the leak this test
        // exists for. Housing also spends timber, so the control is a MATCHED
        // world: identical in every way except that no construction was ordered.
        // Comparing against an un-endowed world instead would measure housing.
        static WorldState RunWithTimberOnly(bool ordered)
        {
            WorldState w = Found();
            PolityId player = PlayerOf(w);
            SettlementId place = w.Settlements[0].Id;
            AllBuilders(w, place);

            TopUp(w, place, "timber", 5_000);

            var orders = new OrderLog();
            if (ordered) orders.Append(Enqueue(0, player, place, Granary));
            return Executor(orders).Run(w, 2);
        }

        WorldState next = RunWithTimberOnly(ordered: true);
        WorldState control = RunWithTimberOnly(ordered: false);
        SettlementId place = next.Settlements[0].Id;

        Assert.Equal(0, StructureCount(next, place, Granary));
        Assert.Equal(1, next.ConstructionQueue.Count);
        Assert.Equal(0, StockOf(next, place, "stone"));

        // The whole point: the blocked project drew NOTHING, so the two worlds
        // hold identical timber despite one of them having been told to build.
        Assert.Equal(StockOf(control, place, "timber"), StockOf(next, place, "timber"));
    }

    // ---- 5 & 6. capacity and its opportunity cost -------------------------

    [Fact]
    public void CapacityIsTheGate_AndConstructionLabourIsWhatBuysIt()
    {
        // The SAME project, the SAME materials, two labour allocations. The only
        // difference is where the settlement's adults are put — which is the
        // opportunity cost the packet asks to be made real rather than stored.
        static WorldState RunAt(double constructionShare)
        {
            WorldState w = Found();
            PolityId player = PlayerOf(w);
            SettlementId place = w.Settlements[0].Id;
            Endow(w, place);
            SetConstructionShare(w, place, constructionShare);

            var orders = new OrderLog();
            orders.Append(Enqueue(0, player, place, Workshop));
            return Executor(orders).Run(w, 2);
        }

        WorldState farmers = RunAt(0.0);    // every adult farming
        WorldState builders = RunAt(1.0);   // every adult building
        SettlementId s = farmers.Settlements[0].Id;

        Assert.Equal(0, StructureCount(farmers, s, Workshop));   // no builders, no workshop
        Assert.Equal(1, farmers.ConstructionQueue.Count);        // still waiting

        Assert.Equal(1, StructureCount(builders, s, Workshop));  // builders, workshop
        Assert.Equal(0, builders.ConstructionQueue.Count);

        // ...and the blocked run consumed nothing.
        Assert.True(StockOf(farmers, s, "timber") > StockOf(builders, s, "timber"));
    }

    // ---- 7. control -------------------------------------------------------

    [Fact]
    public void AnEmpireMayOnlyBuildWhereItRules()
    {
        WorldState world = Found(settlements: 2);
        PolityId mine = PlayerOf(world);
        SettlementId ours = world.Settlements[0].Id;
        SettlementId theirs = world.Settlements[1].Id;

        // Hand settlement 1 to a second Empire.
        var rival = new PolityId(2);
        world.Polities.Add(new PolityRow(rival, CommandSource.Ai));
        for (int i = 0; i < world.Controls.Count; i++)
            if (world.Controls[i].Place == theirs)
                world.Controls[i] = new ControlRow(rival, theirs, 1.0);

        var trespass = new OrderLog();
        trespass.Append(Enqueue(0, mine, theirs, Granary));
        OrderValidationException ex = Assert.Throws<OrderValidationException>(
            () => OrderValidation.ValidateAgainstWorld(trespass, world));
        Assert.Contains("does not control", ex.Message);

        var lawful = new OrderLog();
        lawful.Append(Enqueue(0, mine, ours, Granary));
        OrderValidation.ValidateAgainstWorld(lawful, world);   // must not throw

        // ...and the rival may build in its own.
        var theirOrder = new OrderLog();
        theirOrder.Append(Enqueue(0, rival, theirs, Granary));
        OrderValidation.ValidateAgainstWorld(theirOrder, world);
    }

    // ---- 8. actor ---------------------------------------------------------

    [Fact]
    public void TheOrdersActorIsTheFoundedPolityId()
    {
        WorldState world = Found();
        PolityId player = PlayerOf(world);
        OrderRecord order = Enqueue(0, player, world.Settlements[0].Id, Granary);

        Assert.Equal(player, order.Actor);
        Assert.Equal(player.Value, order.ActorId);
    }

    // ---- 9. player/AI symmetry -------------------------------------------

    [Fact]
    public void ConstructionIsIdenticalWhetherThePlayerOrTheAiOrderedIt()
    {
        static string RunAs(CommandSource source)
        {
            WorldState w = Found();
            PolityId player = PlayerOf(w);
            SettlementId place = w.Settlements[0].Id;
            Endow(w, place);
            AllBuilders(w, place);
            for (int i = 0; i < w.Polities.Count; i++)
                if (w.Polities[i].Id == player) w.Polities[i] = new PolityRow(player, source);

            var orders = new OrderLog();
            orders.Append(Enqueue(0, player, place, Granary));
            WorldState final = Executor(orders).Run(w, 2);

            // Normalise the one field that differs by construction, so the hash
            // compares the SIMULATED WORLD and not the roster byte.
            for (int i = 0; i < final.Polities.Count; i++)
                final.Polities[i] = new PolityRow(final.Polities[i].Id, CommandSource.Ai);
            return WorldHash.ComputeHex(final);
        }

        Assert.Equal(RunAs(CommandSource.Player), RunAs(CommandSource.Ai));
    }

    // ---- 10. multi-settlement --------------------------------------------

    [Fact]
    public void ConstructionAffectsOnlyTheTargetedSettlement()
    {
        WorldState world = Found(settlements: 3);
        PolityId player = PlayerOf(world);
        SettlementId target = world.Settlements[2].Id;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            Endow(world, world.Settlements[s].Id);
            AllBuilders(world, world.Settlements[s].Id);
        }

        var orders = new OrderLog();
        orders.Append(Enqueue(0, player, target, Granary));
        WorldState next = Executor(orders).Run(world, 2);

        Assert.Equal(1, StructureCount(next, target, Granary));
        Assert.Equal(0, StructureCount(next, world.Settlements[0].Id, Granary));
        Assert.Equal(0, StructureCount(next, world.Settlements[1].Id, Granary));
        Assert.Equal(1, next.Structures.Count);
    }

    // ---- 11. save / load --------------------------------------------------

    [Fact]
    public void AQueuedProjectSurvivesSaveAndLoad_AndCompletesTheSameAfterwards()
    {
        WorldState world = Found();
        PolityId player = PlayerOf(world);
        SettlementId place = world.Settlements[0].Id;
        Endow(world, place);
        SetConstructionShare(world, place, 0.0);   // queued, not buildable yet

        var enqueue = new OrderLog();
        enqueue.Append(Enqueue(0, player, place, Granary));
        WorldState queued = Executor(enqueue).Run(world, 1);
        Assert.Equal(1, queued.ConstructionQueue.Count);

        using var ms = new MemoryStream();
        Snapshot.Save(queued, ms);
        ms.Position = 0;
        WorldState back = Snapshot.Load(ms, Sim.Core.Worldgen.Worldgen.Generate(TestConfigs.Worldgen(), 42));

        Assert.True(WorldStates.StateEquals(queued, back), "save/load drifted");
        Assert.Equal(1, back.ConstructionQueue.Count);
        Assert.Equal(Granary, back.ConstructionQueue[0].ProjectId);
        Assert.Equal(queued.ConstructionQueue[0].Slot, back.ConstructionQueue[0].Slot);

        // Finish it on BOTH and compare: the reloaded queue behaves identically.
        AllBuilders(queued, place);
        AllBuilders(back, place);
        WorldState a = Executor(new OrderLog()).Run(queued, 2);
        WorldState b = Executor(new OrderLog()).Run(back, 2);
        Assert.Equal(1, StructureCount(a, place, Granary));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    // ---- 12 & 13. replay and determinism ---------------------------------

    [Fact]
    public void TheSameFoundingAndOrderStreamReplaysToTheSameWorld()
    {
        static string RunOnce()
        {
            WorldState w = Found(settlements: 2);
            PolityId player = PlayerOf(w);
            SettlementId place = w.Settlements[0].Id;
            Endow(w, place);
            AllBuilders(w, place);

            var orders = new OrderLog();
            orders.Append(Enqueue(0, player, place, Granary));
            orders.Append(Enqueue(0, player, place, Workshop));
            OrderValidation.ValidateAgainstWorld(orders, w);
            return WorldHash.ComputeHex(Executor(orders).Run(w, 6));
        }

        Assert.Equal(RunOnce(), RunOnce());
    }

    [Fact]
    public void AProjectIdThatIsNotAWholeNumberIsRejectedAtLoad()
    {
        var log = new OrderLog();
        log.Append(new OrderRecord(0, 1, OrderKind.EnqueueConstruction, 0, 1.5));
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        Assert.Throws<SnapshotFormatException>(() => OrderLog.Load(ms));
    }
}
