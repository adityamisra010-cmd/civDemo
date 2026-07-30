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

    internal static TurnExecutor CanonicalExecutor(OrderLog? orders = null)
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
    internal static WorldState Genesis(ulong seed)
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
        //   v6 (T1.6): schema gained the empty SectorAllocations + PathProgress
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
        //   v9 value: 87b9600ee4b717a13b0af627fb053f43677056e4466ec7fc355937a6e838ded0
        //   T2.7 note (deliberate): schema v10 widened BucketRow
        //   (ReboundReservoir), which serializes no rows on this toy world,
        //   and the demographic retune touches systems outside the toy preset
        //   — the v9 value STOOD through T2.7 unchanged.
        //   v10 (T2.6): schema v11 gained the empty SettlementVitals +
        //   NeedSatisfactions + Grievances tables (three zero count prefixes,
        //   12 bytes); needsgrievance is not in the toy preset. Only the
        //   stream grew.
        //   v10 value: 6ba9b770735a289cd49dff990d0c6e518afa91d336f8372073adcbd40018ecd2
        //   v11 (T2.8): schema v12 gained the empty SmoothedAttractiveness
        //   table (one zero count prefix, 4 bytes); migration is not in the
        //   toy preset. Only the stream grew.
        //   v11 value: ff9519a151bb4b3b348aa289b7555c221834e4ce297d989cb038860c2c07d420
        //   v12 (T3.2, D-031 — SCHEMA ONLY on this world): schema v13 replaced
        //   the empty FoodStores table with the empty GoodStocks + Deposits
        //   tables (net one more zero count prefix, 4 bytes). No goods system
        //   runs in the toy preset and this terrain-less world founds no
        //   stocks; only the stream grew.
        //   v13 (T3.4, D-033 — SCHEMA ONLY on this world): schema v15 gained
        //   two long fields on GoodStockRow (empty here) and the empty Prices +
        //   PriceTerms tables (two zero count prefixes, 8 bytes). The price
        //   system is not in the toy preset and this world has no settlements,
        //   so no price is ever computed; only the stream grew.
        //   v12 value: d0767e5126acbb3f9af220f373fc3dca37a8b8c80d35aad0e993294ea1da8dbd
        //   v14 (T3.4b, CR-003 §3 — SCHEMA ONLY on this world): schema v16
        //   appended the empty HarvestWeather table (one zero count prefix, 4
        //   bytes). The harvest-weather system is not in the toy preset and this
        //   world has no settlements, so no weather is ever drawn; only the
        //   stream grew.
        //   v13 value: 8287c70cf0c0baecdfe01d7eab709f4056edaf26eee4666f7826b215f5a2dc1c
        //   v18 (T3.6, D-034 — SCHEMA-ONLY for this preset): the TradeFlows
        //   table joined the stream. The TOY pipeline does not run the trade
        //   system, so the table is empty and only the byte stream grew (one
        //   zero count prefix); the trajectory is unchanged.
        //   v17 value: 1195124da9977df75052efe24f7b3fbe6a42122cf470e166cfe989dcd436653e
        const string golden = "bbb0929b414fc50502f142ca9e2bfd45d9d7fb4982a46742efcc97f522a7a718";

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
            new SettlementId(0), NodeCount: 1, EffectiveArableKm2: 3.5,
            NetworkRevision: 5, LastRecomputeTurn: 42));
        world.CatchmentSummaries.Add(new CatchmentSummaryRow(
            new SettlementId(1), NodeCount: 1, EffectiveArableKm2: -0.0,
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
        Assert.Equal(3.5, loaded.CatchmentSummaries[0].EffectiveArableKm2);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.CatchmentSummaries[1].EffectiveArableKm2));
        Assert.Equal(42, loaded.CatchmentSummaries[0].LastRecomputeTurn);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void FoundedGolden_Seed42Turn300_MatchesPinnedConstant()
    {
        // T1.9: THE founded-world golden — the production preset on the
        // canonical 1024² N = 12 world, 300 no-order turns (the same horizon
        // as the founded harness legs; since T2.11 the horizon CROSSES the
        // Neolithic→Bronze era gate at turn 250, so the pin covers the dt
        // transition too). FROZEN like its toy sibling above: breaks loudly
        // on ANY founded-behavior change — that is its job. Update
        // deliberately, with a history line, never casually.
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
        //   v5 value: 112d2c77fbd11029aad3bf8109db3f2f516e823184040aacb218fa0e328bc032
        //   v6 (T2.7, historical demographic retune — DELIBERATE): schema v10
        //   (BucketRow +ReboundReservoir) AND behavior — the pre-modern
        //   fertility/mortality profiles (CBR ≈ 37, CDR ≈ 36.5, fed growth
        //   ≈ 0.07 %/yr) plus famine fertility suppression and the deferred-
        //   conception rebound reshape every trajectory. Update ci.yml's
        //   FOUNDED_GOLDEN together with this constant.
        //   v6 value: cb3e43959c467a57e57c1f4dbeafc266550a2c471aa905add90d8733650c15bb
        //   v7 (T2.6 — OBSERVATIONAL TABLES ONLY): schema v11 + the
        //   needsgrievance system in the pipeline. Population/food TRAJECTORIES
        //   are unchanged (needs/grievance reads Prev and writes only its own
        //   tables; the vitals chronicle is bookkeeping of flows that already
        //   ran) — every trajectory-derived test passed across the re-pin; the
        //   stream gained vitals/satisfaction/grievance rows. Update ci.yml's
        //   FOUNDED_GOLDEN together with this constant.
        //   v7 value: bfd44b872a6938d8787ff877e8e165acee981b0ad0c487a7bbdf7cc1513b43b5
        //   v8 (T2.8, migration stabilization — DELIBERATE, behavior + schema
        //   v12): the gap-closing flow cap + EMA-smoothed attractiveness
        //   change every migration flow, and CRUCIALLY end the mortality
        //   dodging the old ping-pong enabled (people shuttling between
        //   settlements evaded Prev-sized death sinks via ClampToAvailable) —
        //   trajectories change massively and HONESTLY (see docs/adr/cr-001).
        //   Update ci.yml's FOUNDED_GOLDEN together with this constant.
        //   v8 value: 8daa4c17dbfb4e0c1ea43aa95a2a227c03b5adb33d13ffc759b6f0082a94db14
        //   v9 (T2.7b, ADR-011 exponential-survival micro-step kernel —
        //   DELIBERATE, behavior only, schema unchanged): mortality/fertility/
        //   aging now integrate in half-year micro-steps with exact
        //   exponential survival fractions on PRESENT counts (dt-invariant by
        //   construction; fed growth 0.761/1000·yr at every dt), and the
        //   vital-rate profiles re-tuned to the honest dynamics (CBR ≈ 41.4,
        //   CDR ≈ 40.7). Every demographic trajectory changes. Update ci.yml's
        //   FOUNDED_GOLDEN together with this constant.
        //   v9 value (turn 200): 72fd2f00eac5d633b3a36140142aba1fc461ade2cfe682b110a70d96e00ed13c
        //   v10 (T2.11 — HORIZON EXTENSION ONLY, no behavior change): the
        //   golden horizon moves 200 → 300 turns to match the extended
        //   harness legs and cross the era-pacing gate at turn 250 (dt
        //   10 → 5). The hash changes because the WORLD IS OLDER, not because
        //   any trajectory drifted — the v9 value above still reproduces at
        //   turn 200 on this same code. Update ci.yml's FOUNDED_GOLDEN (and
        //   its --turns) together with this constant.
        //   v10 value: a5959cdc117ed5cb66f7ee6128d0ff81e66a04feb806e24f0558cadfdc65f2bf
        //   v11 (T3.1+T3.2 paired re-pin — DELIBERATE, worldgen + schema +
        //   founding together, moved ONCE per the pairing rule): T3.1 changed
        //   the WORLD (river-seeded moisture/access, edge taper, region-scored
        //   jittered siting, jittered endowments — every field and every site
        //   moves) and the emergence predicate gained the population term;
        //   T3.2 migrated FoodStores into per-good GoodStocks (schema v13,
        //   grain carries the M2 role) and founds 14 stock rows + deposit
        //   rolls per settlement. Trajectory semantics are pinned by the
        //   worldgen-refresh battery, the goods migration tests, and the
        //   recalibrated corridors (bands re-measured, notes in-file).
        //   v12 (T3.2b, CR-002 recalibration — DELIBERATE, moved ONCE for the
        //   whole packet): CatchmentSummaryRow.EffectiveFarmland became
        //   EffectiveArableKm2 and now carries fertility-weighted km² instead of
        //   fertility-weighted NODES (schema WIDTH and version unchanged — same
        //   double, different denomination), the catchment became a 50 km
        //   economic hinterland from TUNE data instead of a 15-cost-unit code
        //   constant, and farming.yieldPerArableKm2PerYear replaced
        //   yieldPerFarmlandPerYear with a derived value. Every catchment, every
        //   harvest and every trajectory moves by design. Update ci.yml's
        //   FOUNDED_GOLDEN together with this constant.
        //   v11 value: c219cdcc251c903de8ef9240fa839f279ca729d02d14ef70a49f744cca20b173
        //   v13 (T3.3, D-032 — DELIBERATE, behavior + schema v14): the M2
        //   grain monoculture becomes five-sector production over the D-031
        //   roster (ProductionSystem replaces FarmingSystem in the pipeline),
        //   the mandated M2 scaffolding is demolished (artisan tool multiplier
        //   and weighted construction labor DELETED; tools are a real good the
        //   farmers consume), and LaborAllocationRow widens to
        //   SectorAllocationRow. Every trajectory moves by design. Update
        //   ci.yml's FOUNDED_GOLDEN together with this constant.
        //   v12 value: 8aa163701c02d52441dc7cc4efd1c1bd45cad01ca821cad0c88aeb75755374a0
        // T3.4 RE-MINT (D-033). The founded world DOES run the price system,
        // so this hash moves for two reasons, both intended and both itemized:
        //   1. schema v15 — GoodStockRow gains LastInputDemandUnits and
        //      LastConsumptionDemandUnits, and the Prices + PriceTerms tables
        //      are appended;
        //   2. those tables are POPULATED on this world — the founded run
        //      prices every (settlement, good) every turn, and grain is pinned
        //      at 1.0 while the rest move.
        // No existing system's behaviour changed: production, consumption,
        // migration, demographics and pathbuild are byte-identical, and the
        // T3.3 value below was verified unchanged immediately before the price
        // system was added to the pipeline.
        //   T3.3 value: 3a73f1a7df18091da43e542f48669996b01a46675f1b77782bdbf4a7892999ff
        // T3.4b RE-MINT (CR-003 §3). The founded world DOES run harvest
        // weather, so this moves for two reasons, both intended and itemized:
        //   1. schema v16 — the HarvestWeather table is appended and POPULATED;
        //   2. BEHAVIOUR — realised farm output is now multiplied by a mean-one
        //      stochastic factor, so every downstream trajectory (stores,
        //      population, migration, prices) differs from the deterministic
        //      run. This is the packet's whole purpose, not a side effect.
        // Mean is exactly 1 by construction, so EXPECTED yield is unchanged and
        // the derived 26.0 is untouched; what moved is the realised path.
        //   T3.4 value: aebac29c9ac5c7a2321e0be7a4126869526ed556000869ccc92d6937176880dc
        //   SECOND re-mint in this packet, and the reason is a CONSTANT becoming
        //   DERIVED rather than any code change: sigmaLogYield 0.18 (chosen) ->
        //   0.2936 (derived from a rain-fed-cereal CV of 0.30). Weather
        //   amplitude sets the realised harvest path, so the hash moves.
        //   post-weather, pre-derivation value:
        //   305e3bf1a5df12d7b6061d1da431024486c2d340e6382de45b6195d8fe33eab8
//   T3.4c (variance fix - DELIBERATE): the weather blend's cross-term is
        //   corrected, so every weather multiplier changes and with it every
        //   harvest. NOT a schema change; behaviour only. Realised CV moves from
        //   0.38-0.43 to 0.295-0.308, inside the reference band the derived sigma
        //   came from.
        //   previous value: 38f371b2f711514ab1eaa733808f1705443e56176c2b7d5a5d849e61c790e207
        //   T3.5 (D-035 consumption baskets + needs — DELIBERATE, re-minted on
        //   the REBASED substrate, i.e. on top of the T3.4c variance fix):
        //   schema v17 (GoodStockRow.LastConsumptionEatenUnits) plus a real
        //   behaviour change — consumption is now a class-weighted basket over
        //   six goods rather than a single grain flow. Per-person nutrition is
        //   unchanged by construction (the food basket sums to 1.0 and unmet
        //   non-staple demand substitutes into the staple). Update ci.yml's
        //   FOUNDED_GOLDEN together with this constant.
        //   T3.4c-only value: ed26139ba58e6fb22ddcd36f4b1abf0a407f8468f0cd001d28623c725570fda3
        //   T3.5b (2026-07-28, DELIBERATE): the never-ordered default becomes
        //   the derived subsistence mix (0.55/0.15/0.10/0.12/0.08 —
        //   docs/t3.5b-derivations.md §1), the variety reference becomes the
        //   fixed nutritional standard (H* = 0.54), and empty classes accrue
        //   no grievance. All three move every founded world: production
        //   spreads across five sectors, satisfaction re-bases, ghost stocks
        //   zero. Semantic shape asserts unchanged and passing.
        //   T3.5 value: d4e5150a607d9f9ffaf90128433ebec32bb86f0cc2c98abdb8680bd51e3ae945
        //   T3.5b SECOND RE-PIN, stated with its reason (director injection 4:
        //   never folded in quietly): the variety standard's H* is now
        //   normalised exactly as VarietyFactor normalises the obtained diet
        //   — without this, 0.70+0.20+0.10 = 1−1ulp left a diet exactly AT the
        //   standard one ulp above it and the exact-saturation branch was dead
        //   by a rounding error. A mechanism-correctness fix, not a test
        //   addition; it moves every variety-weighted satisfaction by ≤1 ulp
        //   and the hash with it. FirstReign did NOT move, which corroborates
        //   the diagnosis: its pure-grain diets sit at H = 1, where the excess
        //   is (1−H*)/(1−H*) = 1 for ANY standard.
        //   pre-normalisation value: ea965151f84539806b9bc8ca7ffe378f27ab0978e4347d99f06f39d03c598054
        //   v18 (T3.6, D-034 — DELIBERATE, schema + pipeline): the TradeFlows
        //   table joined the stream AND the trade system joined the production
        //   pipeline after price. Whether the no-order founded world actually
        //   TRADES on this horizon is measured and reported by
        //   TradeReadingsTests (the T3.11 blocking-gap question), not assumed
        //   from this hash moving — the hash moves for the schema alone.
        //   v17 value: 724c5e3e7d5bbb59234e480e7f91e13d6b27a321cce2d3455e0ae8400a9d4023
        //   T3.6b (ADR-017 — DELIBERATE, data-only, 2026-07-29): founding
        //   endowmentJitter 0.25 → 0.69 (RC-1 reference-band floor). Every
        //   founded endowment redraws, so the founded trajectory moves; the
        //   schema is untouched (still v18) and the toy golden did not move.
        //   pre-T3.6b value: 3d0e3706e41e9dd8aa21131c285f2a65693c8be988f9aae21309c834f545ab54
        const string golden = "469e38b06e9e3947081acf7304572e9830a16159740139e323ef3651798dfbf0";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(TestUtil.TestConfigs.Sim())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), TestUtil.TestConfigs.Sim(), 42), 300);
        Assert.Equal(golden, WorldHash.ComputeHex(world));
    }

    [Fact]
    public void SchemaV13_PopulatedGoodStockAndDepositTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test — exact ExpectedLength, bit-exact round trip, hash
        // equality. v13 (T3.2): GoodStockRow (five-field payload with the
        // GOOD id distinct from the settlement id — a swapped write order
        // round-trips wrong here) and DepositRow (negative-zero abundance).
        WorldState world = Genesis(23);
        var ledger = new Sim.Core.Kernel.Ledger(world.LedgerFlows);
        world.GoodStocks.Add(new GoodStockRow(new SettlementId(2), new GoodId(7),
            Conserved.Zero, produceRemainder: 0.375, consumeRemainder: -0.0,
            lastProducedUnits: 4242));
        world.GoodStocks.Add(new GoodStockRow(new SettlementId(3), new GoodId(11),
            Conserved.Zero, produceRemainder: -0.0, consumeRemainder: 0.625));
        ledger.Flow(ref world.GoodStocks.Ref(0).Amount,
            ConservedQuantityIds.OfGood(new GoodId(7)),
            ReasonIds.InitialEndowment, 987654321, FlowDirection.Source, OverdrawPolicy.Throw);
        world.Deposits.Add(new DepositRow(new SettlementId(2), new GoodId(5), 1.75));
        world.Deposits.Add(new DepositRow(new SettlementId(3), new GoodId(8), -0.0));

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(world), ms.Length);

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(TestUtil.WorldStates.StateEquals(world, back), "round-trip drifted");
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(back));
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
        world.GoodStocks.Add(new GoodStockRow(new SettlementId(0), new GoodId(1),
            Conserved.Zero, produceRemainder: 0.375, consumeRemainder: -0.0));
        ledger.Flow(ref world.GoodStocks.Ref(0).Amount, ConservedQuantityIds.OfGood(new GoodId(1)),
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
        Assert.Equal(6000, loaded.GoodStocks[0].Amount.Value);
        Assert.Equal(0.375, loaded.GoodStocks[0].ProduceRemainder);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.GoodStocks[0].ConsumeRemainder));
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
        world.GoodStocks.Add(new GoodStockRow(new SettlementId(0), new GoodId(1),
            Conserved.Zero, 0.0, -0.0, lastProducedUnits: 31700));
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
        Assert.Equal(31700, loaded.GoodStocks[0].LastProducedUnits);
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
    public void SchemaV10_PopulatedReboundReservoir_ExactLengthRoundTripHash()
    {
        // Constitution rule: every widened row type ships a POPULATED-table
        // test (T1.1/T1.3 precedent: empty tables prove nothing). v10 widens
        // BucketRow with ReboundReservoir (T2.7's deferred-conception bank) —
        // pinned here with a nonzero, non-round value: exact ExpectedLength,
        // bit-exact round trip, hash equality.
        WorldState world = Genesis(32);
        world.Buckets.Add(new BucketRow(new SettlementId(0), new CultureId(1),
            new ReligionId(1), new ClassId(1), cohortIdx: 0,
            Conserved.Zero, 0.125, 0.0, 0.0, 0.0,
            mobilityRemainder: 0.25, migrationRemainder: 0.875,
            reboundReservoir: 137.4375));

        // Anti-padding: exact schema width sum with the widened row PRESENT.
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
        Assert.Equal(137.4375, loaded.Buckets[0].ReboundReservoir);
        Assert.Equal(0.125, loaded.Buckets[0].BirthRemainder);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV11_PopulatedVitalsSatisfactionGrievance_ExactLengthRoundTripHash()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test. v11 adds SettlementVitalsRow (long counts + a double dt),
        // NeedSatisfactionRow and GrievanceRow — non-round doubles and a
        // negative zero exercise bit-exactness.
        WorldState world = Genesis(33);
        world.SettlementVitals.Add(new SettlementVitalsRow(new SettlementId(0), 123, 456, 10.0));
        world.SettlementVitals.Add(new SettlementVitalsRow(new SettlementId(1), 0, 0, 2.5));
        world.NeedSatisfactions.Add(new NeedSatisfactionRow(new SettlementId(0), new ClassId(1), 1, 0.8125));
        world.NeedSatisfactions.Add(new NeedSatisfactionRow(new SettlementId(0), new ClassId(2), 1, -0.0));
        world.Grievances.Add(new GrievanceRow(new SettlementId(0), new ClassId(1), 42.640625));
        world.Grievances.Add(new GrievanceRow(new SettlementId(1), new ClassId(1), 0.0));

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
        Assert.Equal(456, loaded.SettlementVitals[0].Deaths);
        Assert.Equal(2.5, loaded.SettlementVitals[1].DtYears);
        Assert.Equal(0.8125, loaded.NeedSatisfactions[0].Value);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.NeedSatisfactions[1].Value));
        Assert.Equal(42.640625, loaded.Grievances[0].Value);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV12_PopulatedSmoothedAttractiveness_ExactLengthRoundTripHash()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test. v12 adds SmoothedAttractivenessRow (T2.8's EMA filter
        // state) — non-round double and negative zero pin bit-exactness.
        WorldState world = Genesis(34);
        world.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(new SettlementId(0), 12.359375));
        world.SmoothedAttractiveness.Add(new SmoothedAttractivenessRow(new SettlementId(1), -0.0));

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
        Assert.Equal(12.359375, loaded.SmoothedAttractiveness[0].Value);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.SmoothedAttractiveness[1].Value));
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV6_PopulatedLaborTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test — exact ExpectedLength, bit-exact round trip, hash equality.
        WorldState world = Genesis(23);
        world.SectorAllocations.Add(new SectorAllocationRow(
            new SettlementId(0), 0.35, 0.15, 0.2, 0.25, 0.05));
        world.SectorAllocations.Add(new SectorAllocationRow(
            new SettlementId(1), -0.0, 0.0, 0.0, 0.0, 1.0));
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
        Assert.Equal(0.35, loaded.SectorAllocations[0].Farming);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.SectorAllocations[1].Farming));
        Assert.Equal(123.456, loaded.PathProgress[0].Banked);
        Assert.Equal(4321, loaded.PathProgress[0].FrontierNode);
        Assert.Equal(-1, loaded.PathProgress[1].FrontierNode);
        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV15_PopulatedPriceTables_LengthAndRoundTripExact()
    {
        // Constitution rule: every new serialized row type ships a POPULATED-
        // table test — exact ExpectedLength, bit-exact round trip, hash
        // equality. Empty-table coverage proves nothing (T1.1/T1.3 precedent),
        // and DISTINCT values in every field are what makes a transposition
        // detectable (T3.3 precedent: the SectorAllocation write path).
        WorldState world = Genesis(41);
        world.Prices.Add(new PriceRow(new SettlementId(0), new GoodId(3), 2.5));
        world.Prices.Add(new PriceRow(new SettlementId(1), new GoodId(7), -0.0));
        // Seven distinct term fields, so a permuted write order cannot pass.
        world.PriceTerms.Add(new PriceTermRow(
            new SettlementId(0), new GoodId(3),
            PrevPrice: 1.5, Consumption: 0.25, InputDemand: 0.125,
            Production: -0.0625, StockRelease: -0.03125, Clamp: 0.015625, Delta: 0.296875));
        world.PriceTerms.Add(new PriceTermRow(
            new SettlementId(1), new GoodId(7),
            PrevPrice: -0.0, Consumption: 0.0, InputDemand: 0.0,
            Production: 0.0, StockRelease: 0.0, Clamp: 0.0, Delta: 0.0));

        // GoodStockRow gained two fields at v15 — populate them distinctly too.
        world.GoodStocks.Add(new GoodStockRow(
            new SettlementId(0), new GoodId(3), Conserved.Zero, 0.5, 0.25,
            lastProducedUnits: 11, lastInputDemandUnits: 22, lastConsumptionDemandUnits: 33,
            lastConsumptionEatenUnits: 44));   // v17 (T3.5) — DISTINCT from the other three

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

        Assert.Equal(2.5, loaded.Prices[0].Price);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.Prices[1].Price));
        PriceTermRow t = loaded.PriceTerms[0];
        Assert.Equal(1.5, t.PrevPrice);
        Assert.Equal(0.25, t.Consumption);
        Assert.Equal(0.125, t.InputDemand);
        Assert.Equal(-0.0625, t.Production);
        Assert.Equal(-0.03125, t.StockRelease);
        Assert.Equal(0.015625, t.Clamp);
        Assert.Equal(0.296875, t.Delta);
        GoodStockRow g = loaded.GoodStocks[^1];
        Assert.Equal(11, g.LastProducedUnits);
        Assert.Equal(22, g.LastInputDemandUnits);
        Assert.Equal(33, g.LastConsumptionDemandUnits);
        Assert.Equal(44, g.LastConsumptionEatenUnits);   // v17 (T3.5)

        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV16_PopulatedHarvestWeatherTable()
    {
        // THE POPULATED-TABLE TEST THE CONSTITUTION REQUIRES FOR EVERY NEW
        // SERIALIZED ROW TYPE, missing since T3.4b introduced schema v16. The
        // V-series ran V3, V4, V6-V13, V15 and then stopped: nothing anywhere in
        // Sim.Tests constructed a HarvestWeatherRow, so a read/write
        // transposition of its two doubles was caught only incidentally, by one
        // leg of the founded harness — the exact T1.1/T1.3 precedent this rule
        // exists for.
        //
        // DISTINCT values in every field, and a NEGATIVE LogDeviation, because
        // that is the half of the range a bad year lives in and an all-positive
        // fixture would not notice a sign error. -0.0 is included for the same
        // reason it is elsewhere in this file: it is bit-distinct from 0.0 and a
        // normalising serializer would silently eat it.
        WorldState world = CanonicalExecutor().Run(Genesis(42), 2);
        world.HarvestWeather.Add(new HarvestWeatherRow(new SettlementId(0), -0.4375, 0.6455078125));
        world.HarvestWeather.Add(new HarvestWeatherRow(new SettlementId(1), 0.28125, 1.32470703125));
        world.HarvestWeather.Add(new HarvestWeatherRow(new SettlementId(2), -0.0, 1.0));

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

        Assert.Equal(3, loaded.HarvestWeather.Count);
        // Field-by-field, and by BITS on the doubles — the fields are the same
        // type and adjacent, so a transposition round-trips cleanly under an
        // equality that only checks values are present.
        Assert.Equal(0, loaded.HarvestWeather[0].Settlement.Value);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.4375),
            BitConverter.DoubleToInt64Bits(loaded.HarvestWeather[0].LogDeviation));
        Assert.Equal(BitConverter.DoubleToInt64Bits(0.6455078125),
            BitConverter.DoubleToInt64Bits(loaded.HarvestWeather[0].Multiplier));
        Assert.Equal(BitConverter.DoubleToInt64Bits(0.28125),
            BitConverter.DoubleToInt64Bits(loaded.HarvestWeather[1].LogDeviation));
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0),
            BitConverter.DoubleToInt64Bits(loaded.HarvestWeather[2].LogDeviation));

        Assert.Equal(WorldHash.ComputeHex(world), WorldHash.ComputeHex(loaded));
    }

    [Fact]
    public void SchemaV18_PopulatedTradeFlowsTable()
    {
        // T3.6 (D-034): the constitution's populated-table test for the new
        // row type — exact ExpectedLength, bit-exact round trip, hash
        // equality. DISTINCT values in every field (From ≠ To ≠ Good ≠
        // Quantity), so a transposition of the three int32 id fields — same
        // width, adjacent — cannot round-trip cleanly. Quantity includes a
        // value above Int32.MaxValue so a 4-byte write of the 8-byte field
        // breaks the length equation loudly.
        WorldState world = CanonicalExecutor().Run(Genesis(42), 2);
        world.TradeFlows.Add(new TradeFlowRow(
            new SettlementId(3), new SettlementId(7), new GoodId(11), 5_000_000_017L));
        world.TradeFlows.Add(new TradeFlowRow(
            new SettlementId(7), new SettlementId(3), new GoodId(2), 1L));

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

        Assert.Equal(2, loaded.TradeFlows.Count);
        Assert.Equal(3, loaded.TradeFlows[0].From.Value);
        Assert.Equal(7, loaded.TradeFlows[0].To.Value);
        Assert.Equal(11, loaded.TradeFlows[0].Good.Value);
        Assert.Equal(5_000_000_017L, loaded.TradeFlows[0].Quantity);
        Assert.Equal(7, loaded.TradeFlows[1].From.Value);
        Assert.Equal(3, loaded.TradeFlows[1].To.Value);
        Assert.Equal(2, loaded.TradeFlows[1].Good.Value);
        Assert.Equal(1L, loaded.TradeFlows[1].Quantity);

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
