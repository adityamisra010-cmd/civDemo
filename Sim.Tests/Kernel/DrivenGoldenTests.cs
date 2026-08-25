using System.Globalization;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// T3.11 ITEM 1 — THE DRIVEN GOLDEN.
///
/// THE GAP THIS CLOSES (m3 spec, blocking): every pinned golden ran the
/// all-farming default, so no good but grain flowed and the price step's
/// exponent was exactly 0 — and ADR-016 changed the solver's MATHEMATICS while
/// leaving all three goldens BYTE-IDENTICAL. The project's strongest
/// regression instrument was blind to the goods economy.
///
/// WHAT DRIVES IT: SectorAllocation orders IN THE LOG (D-032 / OrderKind 3,
/// shipped T3.3, turn-exact delivery pinned there and again at T3.9b). Never a
/// changed default — T3.5b's mix is derived from a reference class and CR-003
/// §5.1 governs it.
///
/// WHY THE MIXES ARE ASYMMETRIC (T3.6's queue line, made operational): a
/// uniform mix produces no comparative advantage — every settlement prices
/// every good alike, every gap is identically zero, and a "driven" golden
/// drives nothing. Settlements specialise by settlementIndex % 3, a fixed
/// integer rule with no RNG and no ordering over doubles:
///
///   GROUP A — GRANARY  (0,3,6,9)   70/10/ 5/ 5/10  food surplus, thin
///                                  non-food stocks -> non-food prices HIGH
///   GROUP B — QUARRY   (1,4,7,10)  30/10/45/ 5/10  deep raw stocks -> raw
///                                  prices LOW, crafted prices HIGH
///   GROUP C — WORKSHOP (2,5,8,11)  30/10/10/40/10  crafted goods accumulate
///                                  -> crafted LOW, raw HIGH. B's mirror,
///                                  which is what a price spread needs.
///
/// Farming stays >= 30 and construction at 10 in every group ON PURPOSE: a
/// golden that starved or stopped maintaining housing would re-measure T3.8's
/// collapse instead of the goods economy. An instrument should exercise ONE
/// thing loudly. Weights are raw and normalise in the consumer
/// (Sectors.Share); each row sums to 100, so the stated numbers are the
/// applied shares TO WITHIN A ULP — not bit-exactly, because 0.3 + 0.1 +
/// 0.45 + 0.05 + 0.1 is not exactly 1.0 in doubles. The vacuity test below
/// therefore compares through the SAME operations in the same order rather
/// than against a naive weight/100 (it failed that way first, at
/// 0.29999999999999993 vs ...99 — the test was wrong, not the system).
/// </summary>
public class DrivenGoldenTests
{
    /// <summary>The three specialisations, indexed by settlementIndex % 3.
    /// Order within a row is Sectors.Farming..Construction.</summary>
    private static readonly double[][] GroupMixes =
    [
        [70.0, 10.0,  5.0,  5.0, 10.0],   // A — granary
        [30.0, 10.0, 45.0,  5.0, 10.0],   // B — quarry
        [30.0, 10.0, 10.0, 40.0, 10.0],   // C — workshop
    ];

    private static readonly string[] GroupNames = ["granary", "quarry", "workshop"];

    /// <summary>The order log that drives the world: one batch at turn 2 —
    /// after founding settles, before the economy develops — so the whole
    /// 300-turn trajectory runs driven. Ascending settlement, then ascending
    /// sector: a fixed integer order, which is also the order PathBuildSystem
    /// applies them in.</summary>
    public static OrderLog DrivingOrders(int settlementCount)
    {
        var log = new OrderLog();
        for (int s = 0; s < settlementCount; s++)
        {
            double[] mix = GroupMixes[s % GroupMixes.Length];
            for (int sector = 0; sector < Sectors.Count; sector++)
            {
                log.Append(new OrderRecord(
                    Turn: 2, ActorId: 1, OrderKind.SectorAllocation,
                    TargetId: s * 8 + sector, Amount: mix[sector]));
            }
        }
        return log;
    }

    public static (WorldState World, SimConfig Cfg) RunDriven(int turns)
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = WorldFounding.Found(TestConfigs.Worldgen(), cfg, 42);
        OrderLog orders = DrivingOrders(world.Settlements.Count);
        OrderValidation.ValidateAgainstWorld(orders, world);

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var exec = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg)), orders);
        return (exec.Run(world, turns), cfg);
    }

    [Fact]
    public void DrivenGolden_Seed42Turn300_MatchesPinnedConstant()
    {
        // THE PIN. Unlike the three existing goldens this world is DRIVEN, so
        // its hash is sensitive to the goods economy: production differs by
        // group, stocks diverge, and the price solver runs on a non-zero
        // exponent instead of the all-farming default's exactly-zero one.
        //
        // RED-PROVEN per §7.4 / the packet's acceptance clause, two arms, each
        // measured and reverted, neither ever committed — see
        // docs/t3.11-review-record.md for the transcribed hashes:
        //   P1  raw = prevPrice * exponent            (drop the exponential)
        //       = the ADR-016 REGRESSION ITSELF, the exact class of change
        //         that left all three existing goldens byte-identical.
        //   P2  exponent drops stockReleaseRate       (drop one input)
        //       = a TERM-ATTRIBUTION regression: the solver still
        //         exponentiates, but one measured input stops driving price.
        //
        // §7.5: grain is pinned at 1.0 as the numeraire, so the proof is
        // evidenced on NON-GRAIN prices, not on the hash alone.
        //
        // HISTORY (a golden is read by people who were not there):
        //   v1 (T3.11, 2026-08-05): first pin. Schema v19, canonical 1024²
        //   N = 12, seed 42, 300 turns, driven by the 60-order batch above.
        // T4.1e RE-PIN (defect repair, code-only, ONE cause): deposits moved
        // from a point sample at the site cell to the area-weighted mean over
        // the 50 km hinterland (land cells only). Every founded world's
        // endowments change, so this world hash re-mints. Spacing is UNTOUCHED
        // at its shipped 480 km — this packet carries one change and this pin
        // moves for that one cause.
                // T4.1b RE-PIN (ADR-018, ONE cause): the same re-sited world under the
        // order log — minSpacingKm 480 -> 95.2.
        // T4.2 RE-PIN (VALUE, itemized, ONE cause): store bounding
        // (grainSpoilagePerYear = 0.08, granaryYearsOfDemand = 1.5) changes the
        // driven world's grain stock trajectory over the 300-turn horizon —
        // spoilage and the granary ceiling both apply from the first turn a
        // store exists. No other mechanism, constant, or ordering changed.
        // T4.3 RE-PIN (VALUE, SCHEMA-ONLY, ONE cause): the Claims, Controls
        // and Recognitions tables joined the stream (three zero count
        // prefixes, 12 bytes). No polity/claim/control system exists yet.
        // T4.8 RE-PIN (SCHEMA-ONLY, ONE cause — the v21 Notables table).
        //   OLD   75b5bbbf85fbc6262253b51ca01f2bc6d5323df0b7b983d873dd7fc6f896f61d
        //   NEW   74d072c8add4ccae0fe83ed4c4eb3c92242632e271bbe9103401b795de221f63
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
        //   OLD   75b5bbbf85fbc6262253b51ca01f2bc6d5323df0b7b983d873dd7fc6f896f61d
        //   NEW   7f32248ba34ef8cf4ffd7ce56c76695495f5615f263fea2f80e78a91c62c3c9f
        //   CAUSE `transport.riverCostFactor` now reaches TraversalLattice.Build,
        //         so river-threaded blocks price below the land mean. SettlementSiting
        //         enforces `minSpacingKm` as a TRAVEL-COST distance (D-025: "minimum
        //         travel-time spacing"), and rivers shorten travel cost, so the
        //         spacing test rejects different candidates: 3 of 9 dev sites move
        //         and the pick order shifts. Every seeded world therefore differs
        //         from founding onward. Candidate SCORES are untouched — the lattice
        //         enters siting only through the spacing constraint.
        //   NOT A SCHEMA CHANGE: no table joined or left the stream.
        // T4.7 RE-DERIVED ON REBASE onto main-with-T4.8 (v21 schema). The value
        // pinned pre-rebase was measured against ba96b1c and is void here: the
        // cumulative main differs by BOTH T4.8's empty-Notables count prefix and
        // T4.7's own behaviour, so the hash was re-measured rather than carried.
        //   OLD (on main, T4.8's pin)  74d072c8add4ccae0fe83ed4c4eb3c92242632e271bbe9103401b795de221f63
        //   NEW (T4.7 rebased)         35c90bd1c2f0fef3ec34ae66bc3469fbeb7619da99cf5fb2c7e0054379ac89a0
        //   CAUSE, behavioural and unchanged from T4.7's original attribution:
        //         `transport.riverCostFactor` reaches TraversalLattice.Build, so
        //         river-threaded blocks price below the land mean. SettlementSiting
        //         enforces `minSpacingKm` as a TRAVEL-COST distance (D-025: "minimum
        //         travel-time spacing"), and rivers shorten travel cost, so different
        //         candidates fail the spacing test: 3 of 9 dev sites move and the
        //         pick order shifts. Candidate SCORES are untouched.
        const string golden = "414a32f44d2d49d81fd8c2085c7f7612e7e4418fd6f9bca1ac77d36db393fb61";
        // GRANARY CAPACITY-FLOOR RE-PIN (VALUE, ONE cause — a sub-unit granary
        // ceiling no longer destroys an existing store). DIRECTOR-RULED.
        //   OLD (main, T4.5's pin)  aae82e388697663fe1c9430283257aa6892cdce16e5d26c2a738dd7736258e66
        //   NEW                     414a32f44d2d49d81fd8c2085c7f7612e7e4418fd6f9bca1ac77d36db393fb61
        //   CAUSE `ConsumptionSystem.BoundStore` sized the granary with
        //         `ConservedMath.WholeUnits`, which FLOORS — it is the D-004
        //         converter for a FLOW, under a remainder convention that banks
        //         the fraction elsewhere. A capacity is a THRESHOLD with no
        //         remainder bank, so at `1.5 × annualGrainDemand < 1` a genuinely
        //         POSITIVE capacity floored to 0, `over` became the entire store,
        //         and the settlement's whole grain holding was destroyed in one
        //         turn — while a settlement at literally ZERO demand fails the
        //         `annualGrainDemand > 0` guard and keeps its grain forever. A
        //         settlement down to its last people was treated worse than a
        //         dead one. The ceiling is now enforced only when representable:
        //         `if (capacity > 0 && over > 0)`.
        //   WHERE THE DIVERGENCE BEGINS, and the control that proves the cause.
        //         Measured per turn with the fix ON and OFF over this same driven
        //         world (`DrivenGoldenAttributionProbe`):
        //           turn 152 — capacity floors to 0, but the store is ALREADY 0,
        //                      so there is nothing to destroy: hashes IDENTICAL
        //                      (`4cea9af9f980`), world grain 162 both sides.
        //           turn 153 — no capacity floors to 0: hashes IDENTICAL.
        //           turn 154 — capacity floors to 0 WITH STOCK PRESENT: hashes
        //                      DIVERGE, world grain 127 → 136. The 9 units are a
        //                      4-person settlement's entire granary, previously
        //                      destroyed. Recurs at turns 223 and 241.
        //         The divergence begins exactly when capacity floors to zero
        //         while stock is positive, and NOT when capacity floors to zero
        //         against an empty store — which is what attributes the movement
        //         to this defect and to nothing else.
        //   BLAST RADIUS: this golden only. `FoundedGolden`, `GoldenHash`, both
        //         `FirstReign` tests, `CiPinAgreement`, every `SnapshotTests`,
        //         `EquilibriumInvariantTests` and `PopulationExactnessTests` pass
        //         UNCHANGED; calibration is 4 failed / 2 passed on both sides with
        //         the identical test set. `ci.yml` pins FOUNDED_GOLDEN only and is
        //         untouched. No data file, band, quarantine or schema changed.
        // T4.5 RE-PIN (VALUE, ONE cause — herding now responds to weather).
        //   OLD (main, T4.7's pin)  35c90bd1c2f0fef3ec34ae66bc3469fbeb7619da99cf5fb2c7e0054379ac89a0
        //   NEW (T4.5 rebased)      aae82e388697663fe1c9430283257aa6892cdce16e5d26c2a738dd7736258e66
        //   RE-DERIVED ON REBASE onto the cumulative main (T4.8 v21 + T4.7 rivers).
        //         The value measured on T4.5's pre-rebase base (ef42b770…, taken
        //         against a tree with neither) is VOID here and was re-measured,
        //         never carried through the conflict resolution.
        //   CAUSE T4.5 gives the HERDING food pathway the same HarvestWeatherRow
        //         multiplier farming already had (D-037 B3). The never-ordered
        //         default sector mix HERDS: Sectors.Default is Farming 0.55,
        //         *Herding 0.15*, Extraction 0.10, Crafting 0.12, Construction
        //         0.08 — so every settlement in every weather-bearing world now
        //         has 15% of its labour producing food that varies with the year.
        //         The Herding sector is the FOOD-FROM-DEPOSITS sector and covers
        //         BOTH livestock and fish (ProductionSystem.InSector), and
        //         WorldFounding gives every site a fish deposit, so the catch moves
        //         with the year too. Food moves, and the hash follows.
        //   NOT THE RAID: appropriation cannot fire in this world. It requires a
        //         HERDING-DOMINANT settlement, and RE-MEASURED on the rebased tree
        //         by counting inside AppropriationSystem over all 300 turns of this
        //         exact run: ZERO herding-dominant settlement-turns and ZERO grain
        //         transfers (the default mix is farming-dominant). The raid
        //         contributes nothing to this hash; the weather coupling is the
        //         whole of the movement.
        //   NOT A SCHEMA CHANGE: no table joined or left the stream.
        (WorldState world, _) = RunDriven(300);
        Assert.Equal(golden, WorldHash.ComputeHex(world));
    }

    [Fact]
    public void DrivenGolden_ActuallyDrivesTheSettlements_NotVacuous()
    {
        // The anti-vacuity guard the golden itself cannot provide: a hash pin
        // passes just as happily over a world where the orders never landed.
        // Asserts the driving actually took, and that the three groups really
        // are different — if a future change dropped the order path, the
        // golden would simply be re-pinned and nobody would notice.
        (WorldState world, _) = RunDriven(12);

        Assert.Equal(12, world.Settlements.Count);
        int checkedCount = 0;
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            double[] want = GroupMixes[s % GroupMixes.Length];
            SectorAllocationRow row = default;
            bool found = false;
            for (int i = 0; i < world.SectorAllocations.Count; i++)
                if (world.SectorAllocations[i].Settlement == world.Settlements[s].Id)
                { row = world.SectorAllocations[i]; found = true; break; }
            Assert.True(found, $"settlement {s} has no allocation row — the batch never landed");

            // Compared through the SAME operations in the same order the sim
            // uses: build the row the ordered weights produce (weight/100 via
            // Sectors.With, exactly as PathBuildSystem stores them) and
            // compare Sectors.Share of both. Bit-exact by construction. A
            // naive want/100 comparison is NOT equivalent and fails here —
            // the weights sum to 1.0 only to within a ulp.
            SectorAllocationRow expected = Sectors.Default(world.Settlements[s].Id);
            for (int sector = 0; sector < Sectors.Count; sector++)
                expected = Sectors.With(expected, sector, want[sector] / 100.0);
            for (int sector = 0; sector < Sectors.Count; sector++)
            {
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(Sectors.Share(expected, sector)),
                    BitConverter.DoubleToInt64Bits(Sectors.Share(row, sector)));
            }
            checkedCount++;
        }
        Assert.Equal(12, checkedCount);

        // The groups are genuinely different — the precondition for any
        // comparative advantage at all.
        Assert.NotEqual(GroupMixes[0][2], GroupMixes[1][2]);   // extraction
        Assert.NotEqual(GroupMixes[1][3], GroupMixes[2][3]);   // crafting
    }

    [Fact]
    public void D1_FlowAndItsDecomposition_Measured()
    {
        // D1's PRE-COMMITTED DECOMPOSITION (§7.15, docs/t3.11-spec.md).
        // "Trade volume" is composite — it moves because gaps opened OR
        // because stocks changed, and "zero volume" is likewise composite: no
        // spread at all versus a real spread under the deadband. So all three
        // factors are read SEPARATELY and printed, whatever the volume.
        //
        // NOTE (spec correction, recorded rather than silently diverged): the
        // spec said this reuses T3.9b's TradeModel classification. It cannot —
        // TradeModel lives in Sim.Ui and NOTHING references Sim.Ui (ADR-009).
        // The same classification is therefore computed here independently.
        (WorldState world, SimConfig cfg) = RunDriven(300);
        var inv = CultureInfo.InvariantCulture;
        int grain = cfg.Goods!.GrainId;

        long totalFlow = 0;
        for (int i = 0; i < world.TradeFlows.Count; i++) totalFlow += world.TradeFlows[i].Quantity;

        // The minimum path cost over reachable pairs — the deadband's other
        // factor. Anything unreachable is skipped by the trade system itself.
        double minPathCost = double.PositiveInfinity;
        for (int i = 0; i < world.SettlementDistances.Count; i++)
        {
            double c = world.SettlementDistances[i].TravelCost;
            if (double.IsFinite(c) && c < minPathCost) minPathCost = c;
        }

        Console.WriteLine($"T311_D1 totalFlow={totalFlow} rows={world.TradeFlows.Count} " +
            $"minPathCost={minPathCost.ToString("F4", inv)} pairs={world.SettlementDistances.Count}");

        foreach (GoodEntry g in cfg.Goods.Goods)
        {
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            for (int i = 0; i < world.Prices.Count; i++)
            {
                if (world.Prices[i].Good.Value != g.Id) continue;
                double p = world.Prices[i].Price;
                if (p < lo) lo = p;
                if (p > hi) hi = p;
            }
            long moved = 0, maxStock = 0;
            for (int i = 0; i < world.TradeFlows.Count; i++)
                if (world.TradeFlows[i].Good.Value == g.Id) moved += world.TradeFlows[i].Quantity;
            for (int i = 0; i < world.GoodStocks.Count; i++)
                if (world.GoodStocks[i].Good.Value == g.Id && world.GoodStocks[i].Amount.Value > maxStock)
                    maxStock = world.GoodStocks[i].Amount.Value;

            double spread = double.IsFinite(lo) && double.IsFinite(hi) ? hi - lo : double.NaN;
            double deadband = g.BulkPerUnit * minPathCost * cfg.Trade.CostPerBulkCostUnit;
            string state = g.Id == grain ? "NUMERAIRE"
                : moved > 0 ? "FLOWED"
                : spread > deadband ? "GAP>DEADBAND(!)"
                : spread > 0.0 ? "GapUnderDeadband"
                : "GapZero";

            Console.WriteLine(string.Create(inv,
                $"T311_D1 {g.Name,-11} spread={spread,9:F4} deadband={deadband,9:F4} " +
                $"maxStock={maxStock,10} moved={moved,8}  {state}"));
        }

        // NOT an assertion about the VALUE of flow — D1 pre-commits that zero
        // is an expected outcome given T3.6's escalations. What is asserted is
        // that the instrument is not vacuous: prices exist to compare.
        Assert.True(world.Prices.Count > 0, "no prices published — the decomposition is vacuous");
    }
}
