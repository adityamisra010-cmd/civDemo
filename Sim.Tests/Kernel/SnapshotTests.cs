using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

// T0.7 acceptance: save→load→continue equals the uninterrupted run hash-for-hash
// at every subsequent turn; structural anti-padding proof; pinned golden hash;
// version-mismatch rejection.
public class SnapshotTests
{
    private static EraTable CanonicalEra()
    {
        using var stream = Sim.Data.DataFiles.OpenEraPacing();
        return EraTableLoader.Load(stream);
    }

    private static TurnExecutor CanonicalExecutor(OrderLog? orders = null)
    {
        // Toy preset (T1.5): the golden lineage lives on the toy world + toy
        // systems; the production preset's behavior is pinned by the T1.5
        // population tests and gets its own golden at T1.9.
        using var stream = Sim.Data.DataFiles.OpenPipelineToy();
        var pipeline = PipelineLoader.Load(stream, SystemCatalog.All(TestUtil.TestConfigs.Sim()));
        return new TurnExecutor(CanonicalEra(), pipeline, orders);
    }

    // The canonical M0 test world: seed + two regions (world genesis, shared by
    // the golden-hash and replay tests — changing it changes the golden hash).
    private static WorldState Genesis(ulong seed)
    {
        var world = new WorldState(seed);
        world.Regions.Add(new RegionRow(new RegionId(0)));
        world.Regions.Add(new RegionRow(new RegionId(1)));
        return world;
    }

    [Fact]
    public void SaveLoadContinue_HashEqualsUninterruptedRun_AtEveryTurn()
    {
        const int k = 25, n = 75;
        var executor = CanonicalExecutor();

        // Uninterrupted run to turn k…
        WorldState uninterrupted = executor.Run(Genesis(42), k);

        // …saved and reloaded at turn k…
        using var buffer = new MemoryStream();
        Snapshot.Save(uninterrupted, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);

        Assert.True(WorldStates.StateEquals(uninterrupted, loaded));
        Assert.Equal(WorldHash.ComputeHex(uninterrupted), WorldHash.ComputeHex(loaded));

        // …must continue identically at EVERY turn k..N (RNG continuation
        // included by construction: stream states live in the state stream).
        for (int turn = k; turn < n; turn++)
        {
            uninterrupted = executor.Step(uninterrupted);
            loaded = executor.Step(loaded);
            Assert.Equal(WorldHash.ComputeHex(uninterrupted), WorldHash.ComputeHex(loaded));
        }
    }

    [Fact]
    public void CanonicalStream_LengthEqualsSchemaWidthSum_AntiPaddingProof()
    {
        // Any raw-memory shortcut in the serializer writes padded struct layouts
        // and fails this exact-length equality (e.g. BiomassRow pads 4→24 bytes in
        // memory; the schema writes exactly 20).
        var executor = CanonicalExecutor();
        WorldState world = executor.Run(Genesis(42), 10);

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }

        Assert.True(world.Biomass.Count > 0 && world.RngStreams.Count > 0); // non-vacuous
        Assert.Equal(CanonicalSchema.ExpectedLength(world), buffer.Length);
    }

    [Fact]
    public void GoldenHash_Seed42Turn200_MatchesPinnedConstant()
    {
        // FROZEN CONSTANT. This test breaks LOUDLY on ANY change to world state
        // content, schema order, field widths, RNG, system behavior, or the
        // canonical era/pipeline data — that is its job. Update it deliberately,
        // with a schema Version bump where appropriate; never casually.
        //
        // Update history:
        //   v1 (T0.7): 4cba3e716e5d770a93b13beb4ef7c44baaefaa36c83c94c3f85ef48285f47ce9
        //   v2 (T1.1, ADR-008): schema gained the terrain presence flag + hash
        //   after the clock; sim behavior unchanged (this world has no terrain —
        //   the stream grew exactly one 0x00 byte).
        //   v2 value: 34ad6f01a9b8aaa05eccc7f1265457bf6811a26e5760f4791c1ecf0d7ccea060
        //   v3 (T1.3): schema gained the empty NetworkNodes/NetworkEdges tables
        //   (two zero count prefixes, 8 bytes); sim behavior unchanged.
        //   v3 value: 1884a60b2b66e106503291131b91e9254e7ddf20b6e6a9926fddeedd1cf62e9e
        //   v4 (T1.4): schema gained four empty tables (Settlements, NetworkMeta,
        //   CatchmentNodes, CatchmentSummaries — four zero count prefixes, 16 bytes).
        //   The catchment system leads the pipeline but no-ops on this terrain-less
        //   genesis world, so sim behavior is unchanged; only the stream grew.
        //   v4 value: 64dff09f5e58a95966f9e7c6b2d921048d8595ad9d3183e9e5dc1152c9d235e2
        //   v5 (T1.5): schema gained three empty tables (PopBands, FoodStores,
        //   ConsumptionDeficits — three zero count prefixes, 12 bytes). This test
        //   also moved from the production pipeline to the retired-toy preset —
        //   behavior-identical here: the toy preset is exactly the systems that
        //   acted on this terrain-less world (catchment always no-oped, drew no
        //   RNG). Only the stream grew.
        //   v5 value: abf1ef9357f7cd7599895743e2687c31cb003d616bbb396b4e3de206ba05121c
        //   v6 (T1.6): schema gained the empty LaborAllocations + PathProgress
        //   tables (two zero count prefixes, 8 bytes) — forced by the labor
        //   order's persistent allocation state. Sim behavior on this toy world
        //   is unchanged (pathbuild is not in the toy preset); only the stream grew.
        //   T1.8 note (deliberate): the Leontief farming amendment CHANGED sim
        //   behavior on founded worlds (that was the point — ghost harvest
        //   fixed), but this golden runs the retired-toy preset on a terrain-
        //   less world where farming never executes, so the v6 value STANDS.
        //   Founded-world behavior is pinned by the T1.5/T1.6/T1.8 tests and
        //   the first-reign fixture; its own golden lands at T1.9.
        //   T2.1 note (deliberate): schema v7 replaced PopBands with Buckets,
        //   but the canonical stream carries no version constant and both
        //   tables serialize EMPTY on this toy world (same zero count prefix),
        //   so the v6 value STANDS. Cohort-model behavior is pinned by the
        //   founded golden and the first-reign fixture, both re-pinned at T2.1.
        //   v6 value: 8f3a1986afe9f6fd076e082c868ca36bd171c9da5932fb34c0975de0f38390e1
        //   v8 (T2.2, D-020): schema gained the empty Variables + ClassStates
        //   tables (two zero count prefixes, 8 bytes). Sim behavior on this toy
        //   world is unchanged (classmobility is not in the toy preset; the
        //   Bucket/FoodStore/Deficit row widenings serialize no rows here);
        //   only the stream grew.
        //   v8 value: 539ec6f830644903ee82a19d6ab03079977ead838047869edcc8a2fb20364b23
        //   v9 (T2.5): schema gained the empty SettlementDistances +
        //   MigrationFlows tables (two zero count prefixes, 8 bytes); the
        //   BucketRow widening serializes no rows here. Only the stream grew.
        const string golden = "87b9600ee4b717a13b0af627fb053f43677056e4466ec7fc355937a6e838ded0";

        WorldState world = CanonicalExecutor().Run(Genesis(42), 200);
        Assert.Equal(golden, WorldHash.ComputeHex(world));
    }

    [Fact]
    public void SchemaV3_PopulatedNetworkTables_LengthAndRoundTripExact()
    {
        // Adversarial-review finding (T1.3, the T1.1 precedent repeated): the v3
        // network row widths and (de)serializers were only ever exercised with
        // EMPTY tables — a destroyed edge-cost write passed the whole suite.
        // This test pins the populated branch: exact length, bit-exact round
        // trip (including a negative-zero Cost), and hash equality.
        WorldState world = Genesis(7);
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(0), LatticeNode: 1234));
        world.NetworkNodes.Add(new NetworkNodeRow(new NetworkNodeId(1), LatticeNode: 987));
        world.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(0), new NetworkNodeId(0), new NetworkNodeId(1),
            EdgeTypes.DirtPath, Cost: 123.456));
        world.NetworkEdges.Add(new NetworkEdgeRow(
            new NetworkEdgeId(1), new NetworkNodeId(1), new NetworkNodeId(0),
            EdgeTypes.DirtPath, Cost: -0.0));

        // Anti-padding: exact schema width sum with rows PRESENT.
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        // Round trip: every field survives bit-exactly; hashes agree.
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(1234, loaded.NetworkNodes[0].LatticeNode);
        Assert.Equal(123.456, loaded.NetworkEdges[0].Cost);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.NetworkEdges[1].Cost));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV4_PopulatedSettlementAndCatchmentTables_LengthAndRoundTripExact()
    {
        // Constitution rule (T1.4): every new serialized row type ships a
        // POPULATED-table test — exact ExpectedLength, bit-exact round trip, hash
        // equality. Empty-table coverage proves nothing (T1.1/T1.3 precedent).
        // Exercises all four v4 row types with negative-zero doubles present.
        WorldState world = Genesis(11);
        world.Settlements.Add(new SettlementRow(new SettlementId(0), SiteCell: 4242, FoundedTurn: 7));
        world.Settlements.Add(new SettlementRow(new SettlementId(1), SiteCell: 99, FoundedTurn: 13));
        world.NetworkMeta.Add(new NetworkMetaRow(Revision: 5));
        world.CatchmentNodes.Add(new CatchmentNodeRow(new SettlementId(0), LatticeNode: 321, TravelCost: 8.75));
        world.CatchmentNodes.Add(new CatchmentNodeRow(new SettlementId(1), LatticeNode: 654, TravelCost: -0.0));
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(0), NodeCount: 1, EffectiveFarmland: 3.5,
            NetworkRevision: 5, LastRecomputeTurn: 42));
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(1), NodeCount: 1, EffectiveFarmland: -0.0,
            NetworkRevision: 5, LastRecomputeTurn: 42));

        // Anti-padding: exact schema width sum with rows PRESENT.
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        // Round trip: every field survives bit-exactly; hashes agree.
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(4242, loaded.Settlements[0].SiteCell);
        Assert.Equal(13, loaded.Settlements[1].FoundedTurn);
        Assert.Equal(5, loaded.NetworkMeta[0].Revision);
        Assert.Equal(8.75, loaded.CatchmentNodes[0].TravelCost);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.CatchmentNodes[1].TravelCost));
        Assert.Equal(3.5, loaded.CatchmentSummaries[0].EffectiveFarmland);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.CatchmentSummaries[1].EffectiveFarmland));
        Assert.Equal(42, loaded.CatchmentSummaries[0].LastRecomputeTurn);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void FoundedGolden_Seed42Turn200_MatchesPinnedConstant()
    {
        // T1.9: THE founded-world golden — the M1 production preset on the
        // canonical 1024² world, 200 no-order turns (the same horizon as the
        // founded harness legs; ≥5 Malthus cycles). FROZEN like its toy
        // sibling above: breaks loudly on ANY founded-behavior change — that
        // is its job. Update deliberately, with a history line, never casually.
        //
        // Update history:
        //   v1 (T1.9, post-Leontief farming):
        //   a9ae0ba00a8750a55c103a8c245ecbca4bd87d6ee5851e2a040a974974d34e6e
        //   v2 (T2.1, D-026 cohort buckets — DELIBERATE): PopBands → Buckets
        //   (schema v7) and the cohort demographic profiles replaced the band
        //   rates; behavior changes by design (slot-advance aging, newborn
        //   cohort spread, famine age multipliers). Update ci.yml's
        //   FOUNDED_GOLDEN together with this constant.
        //   v2 value: 1446f99105bf0b2fd457bbc278e156eafaad7cfd246a1ef695209200771d7cb0
        //   v3 (T2.2, D-020 class system — DELIBERATE): schema v8, classmobility
        //   in the pipeline, artisans emerge/mobilize, peasant-labor Leontief
        //   with the scaffolded tool multiplier. Behavior changes by design.
        //   v3 value: 5139a54ddb77ff46b2eb69e04815bc397da31dd6db5da9a977ef89dec4320347
        //   v4 (T2.3, D-025 — DELIBERATE): the canonical founded world is now
        //   the PLURAL N = 12 world (spacing siting + partitioned catchments).
        //   The first-reign golden did NOT re-pin: at --settlements 1 the
        //   partition is bit-identical to the old single-source isochrone.
        //   v4 value: a91c7588f3f428a3c7dc3a1f7f7bd635d3167e4c9ae3c9b27df96964430684cb
        //   v5 (T2.5, D-021 migration — DELIBERATE): schema v9 + the migration
        //   system in the pipeline; trajectories move (people flow between the
        //   twelve settlements). Update ci.yml's FOUNDED_GOLDEN together.
        const string golden = "112d2c77fbd11029aad3bf8109db3f2f516e823184040aacb218fa0e328bc032";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(TestUtil.TestConfigs.Sim())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), TestUtil.TestConfigs.Sim(), 42), 200);
        Assert.Equal(golden, WorldHash.ComputeHex(world));
    }

    [Fact]
    public void SchemaV7_PopulatedBucketAndFoodTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-table
        // test — exact ExpectedLength, bit-exact round trip, hash equality.
        // v7 (T2.1): BucketRow replaces PopBandRow — the full five-part key is
        // exercised with DISTINCT values per field (a swapped Culture/Religion/
        // Class write order round-trips wrong here, not in an empty table).
        // Negative-zero doubles present.
        WorldState world = Genesis(17);
        var ledger = new Sim.Core.Kernel.Ledger(world.LedgerFlows);
        world.Buckets.Add(new BucketRow(new SettlementId(0), new CultureId(3),
            new ReligionId(5), new ClassId(7), cohortIdx: 0,
            Conserved.Zero, birthRemainder: 0.25, deathRemainder: -0.0,
            starvationRemainder: 0.5, agingRemainder: 0.125));
        world.Buckets.Add(new BucketRow(new SettlementId(0), new CultureId(4),
            new ReligionId(6), new ClassId(8), cohortIdx: 9,
            Conserved.Zero, birthRemainder: 0.0, deathRemainder: 0.75,
            starvationRemainder: -0.0, agingRemainder: 0.9));
        ledger.Flow(ref world.Buckets.Ref(0).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 130, FlowDirection.Source, OverdrawPolicy.Throw);
        ledger.Flow(ref world.Buckets.Ref(1).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, 200, FlowDirection.Source, OverdrawPolicy.Throw);
        world.FoodStores.Add(new FoodStoreRow(new SettlementId(0),
            Conserved.Zero, harvestRemainder: 0.375, eatenRemainder: -0.0));
        ledger.Flow(ref world.FoodStores.Ref(0).Store, ConservedQuantityIds.Food,
            ReasonIds.InitialEndowment, 6000, FlowDirection.Source, OverdrawPolicy.Throw);
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 0.613));
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(1), -0.0));

        // Anti-padding: exact schema width sum with rows PRESENT.
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        // Round trip: every field survives bit-exactly; hashes agree.
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(130, loaded.Buckets[0].Count.Value);
        Assert.Equal(new CultureId(3), loaded.Buckets[0].Culture);
        Assert.Equal(new ReligionId(5), loaded.Buckets[0].Religion);
        Assert.Equal(new ClassId(7), loaded.Buckets[0].Class);
        Assert.Equal(9, loaded.Buckets[1].CohortIdx);
        Assert.Equal(0.25, loaded.Buckets[0].BirthRemainder);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.Buckets[0].DeathRemainder));
        Assert.Equal(0.9, loaded.Buckets[1].AgingRemainder);
        Assert.Equal(6000, loaded.FoodStores[0].Store.Value);
        Assert.Equal(0.375, loaded.FoodStores[0].HarvestRemainder);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.FoodStores[0].EatenRemainder));
        Assert.Equal(0.613, loaded.ConsumptionDeficits[0].DeficitRatio);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.ConsumptionDeficits[1].DeficitRatio));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV8_PopulatedVariableAndClassStateTables_AndWidenedRows_Exact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test — exact ExpectedLength, bit-exact round trip, hash
        // equality. v8 adds VariableRow + ClassStateRow and WIDENS three rows;
        // the widened fields are populated with nonzero (and negative-zero)
        // values so a dropped write is visible, not hidden by defaults.
        WorldState world = Genesis(29);
        world.Buckets.Add(new BucketRow(new SettlementId(0), new CultureId(1),
            new ReligionId(1), new ClassId(2), cohortIdx: 7,
            Conserved.Zero, 0.125, 0.25, 0.375, 0.5, mobilityRemainder: 0.625));
        world.FoodStores.Add(new FoodStoreRow(new SettlementId(0),
            Conserved.Zero, 0.0, -0.0, lastHarvestUnits: 31700));
        world.ConsumptionDeficits.Add(new ConsumptionDeficitRow(new SettlementId(0), 0.42, DemandUnits: 4096));
        world.Variables.Add(new VariableRow(new SettlementId(0), Sim.Core.State.Variables.FoodSurplusRatio, 1.375));
        world.Variables.Add(new VariableRow(new SettlementId(1), Sim.Core.State.Variables.ArtisanShare, -0.0));
        world.ClassStates.Add(new ClassStateRow(new SettlementId(0), new ClassId(1), Active: 1));
        world.ClassStates.Add(new ClassStateRow(new SettlementId(0), new ClassId(2), Active: 0));

        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(0.625, loaded.Buckets[0].MobilityRemainder);
        Assert.Equal(31700, loaded.FoodStores[0].LastHarvestUnits);
        Assert.Equal(4096, loaded.ConsumptionDeficits[0].DemandUnits);
        Assert.Equal(1.375, loaded.Variables[0].Value);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.Variables[1].Value));
        Assert.Equal(Sim.Core.State.Variables.ArtisanShare, loaded.Variables[1].VarId);
        Assert.Equal(1, loaded.ClassStates[0].Active);
        Assert.Equal(new ClassId(2), loaded.ClassStates[1].Class);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV9_PopulatedDistanceAndMigrationTables_AndWidenedBucket_Exact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test. v9 adds SettlementDistanceRow (incl. the +∞ unreachable
        // sentinel — its IEEE bits must survive the round trip bit-exactly)
        // and MigrationFlowRow, and widens BucketRow with MigrationRemainder.
        WorldState world = Genesis(31);
        world.Buckets.Add(new BucketRow(new SettlementId(0), new CultureId(1),
            new ReligionId(1), new ClassId(1), cohortIdx: 4,
            Conserved.Zero, 0.0, 0.0, 0.0, 0.0,
            mobilityRemainder: 0.25, migrationRemainder: 0.875));
        world.SettlementDistances.Add(new SettlementDistanceRow(
            new SettlementId(0), new SettlementId(1), 42.125));
        world.SettlementDistances.Add(new SettlementDistanceRow(
            new SettlementId(1), new SettlementId(0), double.PositiveInfinity));
        world.MigrationFlows.Add(new MigrationFlowRow(new SettlementId(0), 123, 456));
        world.MigrationFlows.Add(new MigrationFlowRow(new SettlementId(1), 0, 789));

        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(0.875, loaded.Buckets[0].MigrationRemainder);
        Assert.Equal(42.125, loaded.SettlementDistances[0].TravelCost);
        Assert.True(double.IsPositiveInfinity(loaded.SettlementDistances[1].TravelCost));
        Assert.Equal(123, loaded.MigrationFlows[0].Inflow);
        Assert.Equal(789, loaded.MigrationFlows[1].Outflow);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV6_PopulatedLaborTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test — exact ExpectedLength, bit-exact round trip, hash equality.
        WorldState world = Genesis(23);
        world.LaborAllocations.Add(new LaborAllocationRow(new SettlementId(0), 0.35));
        world.LaborAllocations.Add(new LaborAllocationRow(new SettlementId(1), -0.0));
        world.PathProgress.Add(new PathProgressRow(new SettlementId(0), Banked: 123.456, FrontierNode: 4321));
        world.PathProgress.Add(new PathProgressRow(new SettlementId(1), Banked: -0.0, FrontierNode: -1));

        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(world, writer);
        }
        Assert.Equal(CanonicalSchema.ExpectedLength(world), raw.Length);

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);
        Assert.True(WorldStates.StateEquals(world, loaded));
        Assert.Equal(0.35, loaded.LaborAllocations[0].FarmShare);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.LaborAllocations[1].FarmShare));
        Assert.Equal(123.456, loaded.PathProgress[0].Banked);
        Assert.Equal(4321, loaded.PathProgress[0].FrontierNode);
        Assert.Equal(-1, loaded.PathProgress[1].FrontierNode);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void VersionMismatch_FailsWithActionableMessage()
    {
        WorldState world = CanonicalExecutor().Run(Genesis(42), 3);
        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);

        // Corrupt the version field (bytes 8..12, after the 8-byte magic).
        byte[] bytes = buffer.ToArray();
        bytes[8] = 99;
        using var corrupted = new MemoryStream(bytes);

        var e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(corrupted));
        Assert.Contains("schema version 99", e.Message);
        Assert.Contains("saves break between milestones by design (D-008)", e.Message);
        Assert.Contains("replay", e.Message);
    }

    [Fact]
    public void BadMagic_FailsActionably()
    {
        using var junk = new MemoryStream("not a save file at all"u8.ToArray());
        var e = Assert.Throws<SnapshotFormatException>(() => Snapshot.Load(junk));
        Assert.Contains("bad magic", e.Message);
    }

    [Fact]
    public void NegativeZeroAndNaN_SurviveBitExactly()
    {
        // The schema must NOT normalize special doubles — bit-exactness detects
        // divergence that value-equality would mask.
        var world = Genesis(1);
        world.Rainfall.Add(new RainfallRow(new RegionId(0), -0.0));
        world.Rainfall.Add(new RainfallRow(new RegionId(1), double.NaN));

        using var buffer = new MemoryStream();
        Snapshot.Save(world, buffer);
        buffer.Position = 0;
        WorldState loaded = Snapshot.Load(buffer);

        Assert.Equal(
            BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.Rainfall[0].RainfallMmPerYear));
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(double.NaN),
            BitConverter.DoubleToInt64Bits(loaded.Rainfall[1].RainfallMmPerYear));
    }
}
