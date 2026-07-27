using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

// THE FIRST REIGN (T1.8 re-gate): the director's actual played session,
// replayed headless as a permanent fixture. This log exposed the ghost-harvest
// spec defect — 0% farm at turn 3 starved the settlement to extinction, then a
// 38% order at turn 18 harvested +15,858 food/turn into a dead world (333,018
// banked by turn 40, unbounded). Post-Leontief, this test pins the fixed
// trajectory: harvest collapses WITH the population and the dead world stays
// dead. Runs at the full 1024² canonical config, seed 42 — exactly the world
// the UI founds.
public class FirstReignTests
{
    private static OrderLog Fixture()
    {
        using var stream = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "first-reign-orders.bin"));
        return OrderLog.Load(stream);
    }

    private static WorldState Replay(int turns, out List<(long Pop, long Food, long Harvest)> trajectory)
    {
        SimConfig cfg = TestConfigs.Sim();
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), Fixture());
        // FULL 1024², N = 1 via the D-029 flag (T2.3): the fixture is a
        // single-settlement director session and replays at --settlements 1.
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42, settlementsOverride: 1);
        OrderValidation.ValidateAgainstWorld(Fixture(), world);

        trajectory = [];
        for (int t = 1; t <= turns; t++)
        {
            world = exec.Step(world);
            long pop = 0;
            for (int i = 0; i < world.Buckets.Count; i++) pop += world.Buckets[i].Count.Value;
            long harvest = 0;
            for (int i = 0; i < world.LedgerFlows.Count; i++)
            {
                LedgerFlowRow row = world.LedgerFlows[i];
                if (row.Quantity == ConservedQuantityIds.OfGood(new GoodId(1)) && row.Reason == ReasonIds.Harvest)
                    harvest = row.TotalSourced;
            }
            trajectory.Add((pop, world.GoodStocks[0].Amount.Value, harvest));
            Assert.True(ConservationAuditor.IsConserved(world, out string report), $"turn {t}: {report}");
        }
        return world;
    }

    [Fact]
    public void FirstReign_PostFix_HarvestDiesWithThePeople_NoFoodMountain()
    {
        WorldState final = Replay(40, out var trajectory);

        // T1.9 PIN — the director's first reign guards the Leontief fix
        // forever: the full 40-turn ordered trajectory is hash-pinned (a
        // founded-world ORDERED golden). Breaks loudly on any sim-behavior
        // change; update deliberately with a history line, never casually.
        //   v1 (T1.9, post-Leontief):
        //   6c32ed53d2d0a1d19753847ea23cd3c92b9d02ce51f32a6f3eea63e66627e246
        //   v2 (T2.1, D-026 cohort buckets — DELIBERATE, D-029): trajectory
        //   re-pins under the cohort demographics; the shape asserts below
        //   remain the load-bearing guard.
        //   v2 value: b4af3b3dff1cc62cf0b55f7a7234f2af4cc7c64bc91b98ca0b7a3107f53df504
        //   v3 (T2.2, D-020 class system — DELIBERATE): schema v8 + the class
        //   pipeline; the famine still extinguishes the settlement and the
        //   shape asserts stand unchanged.
        //   v3 value: 1a377e2e26cf5f0b83f75b3a8e509c290e385d33958711746247fee6d48fac44
        //   v4 (T2.5 — SCHEMA-ONLY): BucketRow gained MigrationRemainder and
        //   two empty tables joined the stream. At N = 1 migration NO-OPS (a
        //   flow needs a second settlement), so the TRAJECTORY is unchanged —
        //   the shape asserts below pass untouched; only the byte stream grew.
        //   v4 value: 8a108763b1476489895a5c34ff63ad2060d65ee8d476a6cf8df49c528f7c709c
        //   v5 (T2.7, historical demographic retune — DELIBERATE, behavior +
        //   schema v10): the pre-modern vital rates change every count on the
        //   trajectory, and famine fertility suppression halts conceptions
        //   during the director's engineered starvation. The SHAPE of the
        //   played session survives the retune — extinction still lands inside
        //   (5, 25], the dead world stays frozen, no food mountain — and the
        //   shape asserts below re-verified against the new trajectory.
        //   v5 value: d457c2042bdd462ce1f8f7ee432fb264607ec55e8ea5373a6969c7e7fd48fb2c
        //   v6 (T2.6 — OBSERVATIONAL TABLES ONLY): schema v11 + needsgrievance
        //   in the pipeline; the trajectory is unchanged (grievance accrues
        //   during the director's famine and is read by nothing) — the shape
        //   asserts below pass untouched; the byte stream gained the
        //   vitals/satisfaction/grievance rows.
        //   v6 value: fd02c400127ea8972ac271721637538fee371265ec31c38131963a36d87ef17e
        //   v7 (T2.8, migration stabilization): at N = 1 migration still moves
        //   nobody, but the system now WRITES its EMA filter row every turn
        //   (schema v12 state) — the byte stream changes; the trajectory does
        //   not, and the shape asserts below stand untouched.
        //   v7 value: 15f44bd9ac90febda378db5eb4299843da81e9fbcdc31b061853bc31448a6f6b
        //   v8 (T2.7b, ADR-011 exponential-survival micro-step kernel —
        //   DELIBERATE, behavior only): the micro-step integration changes
        //   every count on the director's famine trajectory. The SHAPE of the
        //   played session survives — extinction still lands inside (5, 25],
        //   the dead world stays frozen, no food mountain — and the shape
        //   asserts below re-verified against the new trajectory.
        //   v8 value: e5c9df592206c2da34cd17f7dbdc5e046819bcf01102df3b4c79b713f97149c5
        //   v9 (T2.13 ghost-grievance fix — DELIBERATE, dead-world state only):
        //   an extinct settlement's grievance stock is now zeroed by
        //   NeedsGrievance (grievance is held by people) instead of decaying
        //   in the ruin forever. The director's famine extinguishes this
        //   settlement, so the frozen dead-world bytes change; the living
        //   trajectory to extinction is untouched and the shape asserts below
        //   re-verified unchanged.
        //   v9 value: c35a88a837f102780342992e1f0db24d45e71eb020da3e1fba33b85d2a28b3ee
        //   v10 (T3.1+T3.2 paired re-pin — DELIBERATE): the worldgen refresh
        //   re-sites and re-endows even this N = 1 replay world (river-seeded
        //   moisture, edge taper, jittered siting/endowment) and schema v13
        //   carries the grain-migrated goods tables. The SHAPE of the played
        //   session survives — extinction inside (5, 25], the dead world
        //   frozen, no food mountain — and the shape asserts below were
        //   re-verified against the new trajectory (they run on every build;
        //   a blind re-pin cannot satisfy them).
        //   v11 (T3.2b, CR-002 recalibration — DELIBERATE, tuning + denomination
        //   together, moved ONCE): the catchment became a 50 km economic
        //   hinterland instead of a ~205 km isochrone and the yield constant was
        //   re-derived and re-denominated per fertility-weighted km², so this
        //   settlement's land, harvest and trajectory all move. The SHAPE of the
        //   played session survives and the shape asserts below were re-verified
        //   against the new trajectory — extinction still lands inside (5, 25],
        //   the dead world is still frozen, and there is still no food mountain
        //   (they run on every build; a blind re-pin cannot satisfy them).
        //   v10 value: 3a6d296f117cbb339969a9ad261f5b685b27adcae4bcac55a016fe70a7d7e72c
        //   v12 (T3.3, D-032 production + scaffolding demolition — DELIBERATE):
        //   FarmingSystem is REPLACED by ProductionSystem (five sectors over the
        //   D-031 roster), the M2 artisan tool-multiplier and weighted
        //   construction-labor scaffolds are DELETED, tools become a real good
        //   consumed by farmers, and schema v14 widens the labor row to five
        //   sector weights. This N=1 fixture's own harvest changes because the
        //   yield's tool factor is now a real stock (zero at founding) rather
        //   than an artisan-share multiplier. The SHAPE of the played session
        //   survives and the shape asserts below were re-verified against the
        //   new trajectory — extinction still inside (5, 25], the dead world
        //   frozen, still no food mountain.
        //   v11 value: db653fd2b3615bcbeea94fefac870a227c9e49e92b28af4618da17489653a9f0
        //   v8 (T3.4, D-033): the price system joins the pipeline and
        //   populates the Prices + PriceTerms tables on this founded world, and
        //   schema v15 adds two long fields to GoodStockRow. SCHEMA + NEW STATE,
        //   no behaviour change to any existing system — the semantic
        //   assertions below (extinction window, flat post-extinction
        //   trajectory, no food mountain) are unchanged and still pass, which
        //   is what distinguishes this re-mint from a regression.
        //   v7 value: 7c0671a31557e0842668d0995d9af0fd20530fcf11dea3df963d9f86a92a3ae7
        //   v9 (T3.4b, CR-003 §3): harvest weather multiplies realised farm
        //   output, so this collapsing world's trajectory differs — SCHEMA v16
        //   plus real behaviour change. The semantic assertions below are
        //   unchanged and still pass (extinction window, flat post-extinction
        //   trajectory, no food mountain), which is what separates this re-mint
        //   from a regression.
        //   v8 value: b6e16c1edf39ef0585eaef800b4b00d7f10a82ced12316349a2344965cb31c7b
        //   SECOND re-mint in this packet — sigmaLogYield became DERIVED (0.18 ->
        //   0.2936). Semantic assertions below unchanged and still passing.
        //   post-weather, pre-derivation value:
        //   58a0aec6747ce1ee7fc7625fbc4dccb1627906d8c2b67f0f3c8a431eb2d4c1a2
        //   v10 (T3.5, D-035): consumption became a class-weighted BASKET over
        //   six goods instead of a single grain flow, GoodStockRow gained
        //   LastConsumptionEatenUnits (SCHEMA v17), and needs aggregate by CES.
        //   Both a schema change and a real behaviour change — the nutritional
        //   total per person is unchanged by construction (food basket weights
        //   sum to 1.0 and unmet non-staple demand substitutes into grain), so
        //   the demographic trajectory moves only through integer rounding of
        //   the split flows. The semantic assertions below are unchanged and
        //   still pass, which is what separates this re-mint from a regression.
        //   v9 value: 8f7e93be5bf3e28acc03dbe786aac038a7df0ecea4cfd9843be19212c0a5f0a4
        const string golden = "97adbeb9dfbfcef446204c124ed3242aed42a81bc03ce7062adf24a40db7c45f";
        Assert.Equal(golden, WorldHash.ComputeHex(final));

        // SHAPE ASSERTS — the anti-blind-repin guard (adversarial pass): they
        // assert trajectory SEMANTICS, so a ghost-harvest revert plus a
        // mechanical golden re-pin still fails here. Never delete these as
        // "redundant with the golden".
        // The famine plays out (the director's 0%-farm order really starves).
        int extinctionTurn = trajectory.FindIndex(x => x.Pop == 0) + 1;
        Assert.True(extinctionTurn is > 5 and <= 25,
            $"extinction at turn {extinctionTurn} — outside the played session's shape");

        // Post-extinction, FOREVER: harvest total static (the turn-18 order
        // resurrects nothing), food static, population stays zero.
        long harvestAtDeath = trajectory[extinctionTurn - 1].Harvest;
        long foodAtDeath = trajectory[extinctionTurn - 1].Food;
        for (int t = extinctionTurn; t < trajectory.Count; t++)
        {
            Assert.Equal(0, trajectory[t].Pop);
            Assert.Equal(harvestAtDeath, trajectory[t].Harvest);
            Assert.Equal(foodAtDeath, trajectory[t].Food);
        }

        // The ghost food mountain never forms (pre-fix: 333,018 by turn 40).
        long maxFood = trajectory.Max(x => x.Food);
        Assert.True(maxFood < 100_000, $"food mountain formed: peak {maxFood}");
    }

    [Fact]
    public void FirstReign_ReplayIsTwinDeterministic()
    {
        WorldState a = Replay(25, out _);
        WorldState b = Replay(25, out _);
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }
}
