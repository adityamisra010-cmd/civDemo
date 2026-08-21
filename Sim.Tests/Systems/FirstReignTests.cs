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
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestConfigs.Worldgen())), Fixture());
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
        //
        // NUMBERING REPAIR (T3.11 Item 2, 2026-08-06 — heading numbers only; no
        // hash, no entry text and no pin was altered). This history had TWO
        // spliced numbering series: after the T3.3 entry the count RESTARTED at
        // v8, so v8-v13 each named two different pins and v13 named two on its
        // own. The splice point is exactly where the T3.5 entry records being
        // "re-minted on the REBASED substrate" — the rebase carried a second
        // series in beside the first. Headings are now MONOTONIC v1..v22 in
        // file order, which is chronological because each entry names its
        // packet and the packet order is independently checkable against main.
        //
        // WHAT WAS NOT REPAIRED, DELIBERATELY: the trailing "vN value:" labels
        // inside each entry are left EXACTLY as written, and they are known
        // inconsistent — early entries label their OWN pin, entries from the
        // T3.2b one onward label the SUPERSEDED pin. Re-attributing a hash
        // would mean guessing which convention an author meant, and a wrong
        // guess here silently rewrites the project's record of what a world
        // once hashed to. The hashes themselves are unique and were never in
        // doubt; only their labels are. Read a value line as "a pin from around
        // this entry", and read the ENTRY TEXT for which.
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
        //   v13 (T3.4, D-033): the price system joins the pipeline and
        //   populates the Prices + PriceTerms tables on this founded world, and
        //   schema v15 adds two long fields to GoodStockRow. SCHEMA + NEW STATE,
        //   no behaviour change to any existing system — the semantic
        //   assertions below (extinction window, flat post-extinction
        //   trajectory, no food mountain) are unchanged and still pass, which
        //   is what distinguishes this re-mint from a regression.
        //   v7 value: 7c0671a31557e0842668d0995d9af0fd20530fcf11dea3df963d9f86a92a3ae7
        //   v14 (T3.4b, CR-003 §3): harvest weather multiplies realised farm
        //   output, so this collapsing world's trajectory differs — SCHEMA v16
        //   plus real behaviour change. The semantic assertions below are
        //   unchanged and still pass (extinction window, flat post-extinction
        //   trajectory, no food mountain), which is what separates this re-mint
        //   from a regression.
        //   v8 value: b6e16c1edf39ef0585eaef800b4b00d7f10a82ced12316349a2344965cb31c7b
        //   v15 (T3.4b) SECOND re-mint in this packet — sigmaLogYield became DERIVED (0.18 ->
        //   0.2936). Semantic assertions below unchanged and still passing.
        //   post-weather, pre-derivation value:
        //   58a0aec6747ce1ee7fc7625fbc4dccb1627906d8c2b67f0f3c8a431eb2d4c1a2
        //   v16 (T3.4c): THE VARIANCE FIX. The weather blend treated a regional
        //   field that CONTAINS the local draw as independent of it, so realised
        //   sigma ran 1.10-1.41x the derived 0.2936 and the multiplier's mean ran
        //   1.013-1.043 instead of 1. Correcting it changes every weather value
        //   and therefore this collapsing world's whole trajectory.
        //   v9 value: 8f7e93be5bf3e28acc03dbe786aac038a7df0ecea4cfd9843be19212c0a5f0a4
        //   v17 (T3.5, D-035, re-minted on the REBASED substrate — on top of the
        //   T3.4c variance fix): consumption became a class-weighted BASKET over
        //   six goods instead of a single grain flow, GoodStockRow gained
        //   LastConsumptionEatenUnits (SCHEMA v17), and needs aggregate by CES.
        //   The nutritional total per person is unchanged by construction, so
        //   the demographic trajectory moves only through integer rounding of
        //   the split flows. The semantic assertions below are unchanged and
        //   still pass, which is what separates this re-mint from a regression.
        //   v10 value: 19c55dd9f2b509762495c352ef5a3491d03d9ccb658475cc1d23a9e31eb17668
        //   v18 (T3.5b): the derived subsistence default + the fixed variety
        //   standard + the empty-class grievance fix — every founded world's
        //   trajectory moves (production spreads across sectors, satisfaction
        //   re-bases, ghost stocks zero). Semantic assertions below unchanged
        //   and still passing, which is what separates a re-mint from a
        //   regression.
        //   v11 value: fe6b0287592acf5a2c79c57e9b882c790a4466a4a07cefe9b5bb6b584577e40f
        //   v19 (T3.6, D-034 — SCHEMA-ONLY): TradeFlows joined the stream and
        //   trade joined the pipeline, but at N = 1 trade NO-OPS structurally
        //   (a pair needs two settlements), so the trajectory is unchanged —
        //   the shape asserts below pass untouched; the byte stream gained
        //   one empty table count per snapshot.
        //   v12 value: 4427d965f08fc7f59b44a51b57fd4ca3e1187436e381222abdc8247b057fe293
        //   v20 (T3.6b, ADR-017 — DELIBERATE, data-only): endowmentJitter
        //   0.25 → 0.69 redraws even this N = 1 world's founding endowment
        //   (slot-0 jitter), so the director's famine trajectory re-pins; the
        //   shape asserts below run on every build and re-verified unchanged
        //   (extinction inside the window, dead world frozen, no food
        //   mountain — a blind re-pin cannot satisfy them).
        //   v12 value: 9bc1e06f605de359257836c016bd0082341e454c8001e5424a02e892b21ba071
        //   v21 (T3.8 — DELIBERATE, schema v19 + behavior): the housing stock.
        //   Even this N = 1 world founds HOUSED (dwellings via
        //   InitialEndowment), maintenance draws timber/clay while people
        //   live, and the construction pool splits between dwellings and
        //   paths — the director's famine trajectory re-pins. The shape
        //   asserts below re-verified unchanged: extinction inside (5, 25],
        //   dead world frozen, no food mountain (a blind re-pin cannot
        //   satisfy them).
        //   v13 value: 66c3a94522c0c580b3c200efe69cef7e480b312ef0fafc9c421a29f0a4682195
        //   v22 (T3.8 certification fix pass — DELIBERATE, DATA-ONLY; the
        //   SECOND re-pin in this packet, authorized by the certification
        //   ruling on the T3.5b precedent): v14 CAPTURED A WORLD LATER FOUND
        //   DEFECTIVE — housing collapsed from turn 1 (the maintenance
        //   derivation named the registry's ceramic clay, which crafting
        //   consumes to exhaustion before housing's draw; review record, fix
        //   pass). Clay draw rates corrected to 0 (structural earth is
        //   subsoil, a non-good); even this N = 1 world's housing now
        //   maintains from its timber store until the famine takes the people.
        //   Do not read v14 as a good state. Shape asserts re-verified:
        //   extinction inside (5, 25], dead world frozen, no food mountain.
        //   defective v14 value: 2d6f9f45f9ec5097efbca0063284f0325352dbea40dd3df4b246a0c316f7b4e5
        //   v14 value: 144d7e5d89b9ff99085eda559e105c712880064e5c14d8d626bf5df36c913bff
        //   v23 (T4.1e — DEFECT REPAIR, code-only): deposits are now the
        //   AREA-WEIGHTED MEAN of their terrain channel over the settlement's
        //   50 km hinterland, land cells only, instead of a POINT SAMPLE at the
        //   site cell. The site reading was measured saturated: moisture read
        //   EXACTLY 1.0000 at eleven of twelve settlements on the canonical
        //   world at the SHIPPED 480 km spacing, because siting selects for
        //   water access and that channel is "1 at the shore". Even this N = 1
        //   world's founding deposits therefore move. Shape asserts below
        //   RE-VERIFIED against the new trajectory and passing — extinction
        //   inside (5, 25], dead world frozen, no food mountain — which is the
        //   re-mint/regression separator, not a formality.
        //   v24 (T4.2 — B-2 STORE BOUNDING, ONE cause): grainSpoilagePerYear =
        //   0.08 and granaryYearsOfDemand = 1.5 change the director's famine
        //   trajectory's grain stock from the first turn the store exists.
        //   Shape asserts below RE-RUN (not carried) against the new
        //   trajectory and passing — extinction inside (5, 25], dead world
        //   frozen, no food mountain.
        //   v25 (T4.3, D-037 A3 — SCHEMA-ONLY): the Claims, Controls and
        //   Recognitions tables joined the stream (three zero count
        //   prefixes, 12 bytes, N = 1). No polity/claim/control system
        //   exists yet; the trajectory is unchanged — shape asserts below
        //   RE-VERIFIED against the new stream and passing.
        // T4.8 RE-PIN (SCHEMA-ONLY, ONE cause — the v21 Notables table).
        //   OLD   3fd26370340ac7521e371328dd72d8ac3aa3407ba14c4b39afc9713200dc989a
        //   NEW   aa122530e71b22ec65a050c30e6e96b64a185e5975049b9421f2861f05123ad6
        //   CAUSE CanonicalSchema v21 appends the Notables table (R-1: a notable
        //         is a PERSON, so the row carries a conserved Population count).
        //         NO SYSTEM WRITES IT, so the table is EMPTY in every world and
        //         the ONLY change to the stream is its 4-byte count prefix —
        //         MEASURED on the founded seed-42 world: notableRows=0,
        //         notableBytes=4. Every hash moves; no behaviour does.
        //   NOT A BEHAVIOUR CHANGE: no pipeline slot was added, no existing
        //         system was touched, and the targeted suite proves the world is
        //         otherwise identical.
        // T4.7 RE-PIN (VALUE, ONE cause — the river-aware traversal lattice).
        //   OLD   3fd26370340ac7521e371328dd72d8ac3aa3407ba14c4b39afc9713200dc989a
        //   NEW   50bf1298523eeb5e69423e63886ce1e7b6e67bc39a21d59d580508af7198ec4f
        //   CAUSE `transport.riverCostFactor` now reaches TraversalLattice.Build.
        //         SettlementSiting enforces `minSpacingKm` as a TRAVEL-COST distance
        //         (D-025: "minimum travel-time spacing"); rivers shorten travel cost,
        //         so different candidates fail the spacing test — 3 of 9 dev sites
        //         move and the pick order shifts. Candidate SCORES are untouched.
        //   NOT A SCHEMA CHANGE: no table joined or left the stream.
        // T4.7 RE-DERIVED ON REBASE onto main-with-T4.8 (v21 schema). The value
        // pinned pre-rebase was measured against ba96b1c and is void here: the
        // cumulative main differs by BOTH T4.8's empty-Notables count prefix and
        // T4.7's own behaviour, so the hash was re-measured rather than carried.
        //   OLD (on main, T4.8's pin)  aa122530e71b22ec65a050c30e6e96b64a185e5975049b9421f2861f05123ad6
        //   NEW (T4.7 rebased)         dfd14560d94f44c1774d6e75298dc0a37a202ddc198fde343c0af12c5c6e0cca
        //   CAUSE, behavioural and unchanged from T4.7's original attribution:
        //         `transport.riverCostFactor` reaches TraversalLattice.Build, so
        //         river-threaded blocks price below the land mean. SettlementSiting
        //         enforces `minSpacingKm` as a TRAVEL-COST distance (D-025: "minimum
        //         travel-time spacing"), and rivers shorten travel cost, so different
        //         candidates fail the spacing test: 3 of 9 dev sites move and the
        //         pick order shifts. Candidate SCORES are untouched.
        const string golden = "dfd14560d94f44c1774d6e75298dc0a37a202ddc198fde343c0af12c5c6e0cca";
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
