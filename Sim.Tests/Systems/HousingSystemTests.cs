using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.8 — the HOUSING MECHANISM's semantic teeth (maintenance-not-abstract-
/// decay, director ruling). Every named mutant in the lens manifest has a
/// kill here that asserts the MEANING, not a golden: τ→∞ (test 2: the exact
/// exponent), degradation-ignores-m (test 3: the Leontief fraction scales the
/// exponent), cap-deleted (test 4: build stops AT the surplus target with
/// materials and labor to spare), labor-split-deleted (test 6: PathBuild's
/// bank moves by exactly the published housing labor). Expected values are
/// computed with the SAME expressions and operand order the system uses, so
/// equality is bit-exact — never approximate (law 1 discipline applied to
/// doubles: same operations, same order, same bits).
/// </summary>
public class HousingSystemTests
{
    /// <summary>The two-material rig: canonical config with the clay draw
    /// rates RESTORED to the pre-correction values. The T3.8 fix pass zeroed
    /// canonical clay (corrected derivation — structural earth is subsoil, a
    /// non-good, not the registry's ceramic clay), but the MECHANISM keeps its
    /// full Leontief generality over any number of drawn materials; these
    /// tests exercise that generality with rigged data (constitution: data
    /// rigging is legal). Tests of the canonical single-material behaviour use
    /// TestConfigs.Sim() directly.</summary>
    private static SimConfig TwoMaterialConfig()
    {
        SimConfig cfg = TestConfigs.Sim();
        return cfg with
        {
            Housing = cfg.Housing with
            {
                UpkeepClayPerDwellingYear = 0.025,
                BuildClayPerDwelling = 1.0,
            },
        };
    }

    private static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } ] }""");

    private static TurnExecutor HousingOnly(SimConfig cfg, double dt = 10.0) =>
        new(FlatEra(dt), [SystemCatalog.Housing(cfg)]);

    /// <summary>One-settlement hand world: a 1-bucket adult population (cohort
    /// 5), a housing row, timber/clay stocks, and an explicit sector row —
    /// all conserved quantities seeded the lawful way (Ledger flows).</summary>
    private static WorldState HousingWorld(
        SimConfig cfg, long pop, long dwellings, long timber, long clay, double constructionShare)
    {
        var world = new WorldState(11);
        var ledger = new Ledger(world.LedgerFlows);
        var id = new SettlementId(0);
        world.Settlements.Add(new SettlementRow(id, SiteCell: 0, FoundedTurn: 0));
        int bucket = world.Buckets.Add(new BucketRow(
            id, new CultureId(1), new ReligionId(1), new ClassId(1),
            cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
        ledger.Flow(ref world.Buckets.Ref(bucket).Count, ConservedQuantityIds.Population,
            ReasonIds.InitialEndowment, pop, FlowDirection.Source, OverdrawPolicy.Throw);
        world.Grievances.Add(new GrievanceRow(id, new ClassId(1), 0.0));
        int h = world.Housing.Add(new HousingRow(id, Conserved.Zero, 0.0, 0.0, 1.0, 0.0));
        if (dwellings > 0)
        {
            ledger.Flow(ref world.Housing.Ref(h).Dwellings, ConservedQuantityIds.Dwellings,
                ReasonIds.InitialEndowment, dwellings, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        AddStock(world, ledger, cfg, "timber", timber);
        AddStock(world, ledger, cfg, "clay", clay);
        world.SectorAllocations.Add(new SectorAllocationRow(
            id, Farming: 1.0 - constructionShare, Herding: 0.0, Extraction: 0.0,
            Crafting: 0.0, Construction: constructionShare));
        return world;
    }

    private static void AddStock(WorldState world, Ledger ledger, SimConfig cfg, string good, long amount)
    {
        int gid = cfg.Goods!.IdOf(good);
        Assert.True(gid > 0, $"unknown good '{good}'");
        int row = world.GoodStocks.Add(new GoodStockRow(
            new SettlementId(0), new GoodId(gid), Conserved.Zero, 0.0, 0.0));
        if (amount > 0)
        {
            ledger.Flow(ref world.GoodStocks.Ref(row).Amount,
                ConservedQuantityIds.OfGood(new GoodId(gid)),
                ReasonIds.InitialEndowment, amount, FlowDirection.Source, OverdrawPolicy.Throw);
        }
    }

    private static HousingRow House(WorldState world)
    {
        for (int i = 0; i < world.Housing.Count; i++)
            if (world.Housing[i].Settlement.Value == 0) return world.Housing[i];
        Assert.Fail("no housing row for settlement 0");
        return default;
    }

    private static long Stock(WorldState world, SimConfig cfg, string good)
    {
        int gid = cfg.Goods!.IdOf(good);
        for (int i = 0; i < world.GoodStocks.Count; i++)
            if (world.GoodStocks[i].Settlement.Value == 0 && world.GoodStocks[i].Good.Value == gid)
                return world.GoodStocks[i].Amount.Value;
        Assert.Fail($"no {good} stock row for settlement 0");
        return 0;
    }

    private static void AssertBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    [Fact]
    public void Maintained_FullUpkeep_NoDecay_DrawsExactUpkeep()
    {
        // The ruling's equivalence: fully maintained housing persists
        // INDEFINITELY, and the upkeep is a real material draw, not a
        // coefficient. Construction share 0 keeps the build arm silent
        // (deficit 20 exists, but zero builder-years cap it at zero).
        SimConfig cfg = TwoMaterialConfig();
        WorldState world = HousingWorld(cfg, pop: 600, dwellings: 100, timber: 1000, clay: 1000, 0.0);
        WorldState next = HousingOnly(cfg).Step(world);

        HousingRow h = House(next);
        Assert.Equal(100L, h.Dwellings.Value);          // nothing decayed
        AssertBits(1.0, h.LastMaintenanceFraction);
        AssertBits(0.0, h.LastLaborUsed);               // and nothing built
        // Upkeep drawn exactly: 100 × 0.05 × 10 = 50 timber, 100 × 0.025 × 10 = 25 clay.
        long timberDraw = (long)Math.Round(100L * cfg.Housing.UpkeepTimberPerDwellingYear * 10.0);
        long clayDraw = (long)Math.Round(100L * cfg.Housing.UpkeepClayPerDwellingYear * 10.0);
        Assert.Equal(1000L - timberDraw, Stock(next, cfg, "timber"));
        Assert.Equal(1000L - clayDraw, Stock(next, cfg, "clay"));
    }

    [Fact]
    public void Unmaintained_DecaysByTheExactExponent_AndBanksTheRemainder()
    {
        // The τ tooth: with NO materials, m = 0 and one 10-year turn loses
        // exactly dwellings × (1 − e^(−dt/τ)) — whole units through the
        // Ledger, fraction banked. A τ→∞ mutant (decay disabled) keeps all
        // 100 and dies here; so does any linear-rate approximation, because
        // the expected value below is the exact closed form, bit for bit.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = HousingWorld(cfg, pop: 600, dwellings: 100, timber: 0, clay: 0, 0.0);
        WorldState next = HousingOnly(cfg).Step(world);

        double lostExact = 100L * (1.0 - Math.Exp(-(1.0 - 0.0) * 10.0 / cfg.Housing.TauYears)) + 0.0;
        long lost = (long)Math.Floor(lostExact);
        HousingRow h = House(next);
        AssertBits(0.0, h.LastMaintenanceFraction);
        Assert.Equal(100L - lost, h.Dwellings.Value);
        AssertBits(lostExact - lost, h.DecayRemainder);
        Assert.True(lost > 0, "rig vacuous: the decay arm never fired");
    }

    [Fact]
    public void Maintenance_IsLeontief_TheScarcerMaterialGoverns()
    {
        // Roof AND walls (two-material rig): timber plentiful, clay at 12 of
        // the 25 demanded.
        // m must be the MIN fill (12/25), not a timber-weighted average, and
        // the decay exponent must scale by (1 − m) — the kill for the
        // degradation-ignores-m mutant (which would lose 22 dwellings here
        // instead of the correct 12) and for any mean-not-min blend.
        SimConfig cfg = TwoMaterialConfig();
        WorldState world = HousingWorld(cfg, pop: 600, dwellings: 100, timber: 1000, clay: 12, 0.0);
        WorldState next = HousingOnly(cfg).Step(world);

        double demandT = 100L * cfg.Housing.UpkeepTimberPerDwellingYear * 10.0;
        double demandC = 100L * cfg.Housing.UpkeepClayPerDwellingYear * 10.0;
        double m = Math.Min(Math.Min(1.0, 1000.0 / demandT), Math.Min(1.0, 12.0 / demandC));
        HousingRow h = House(next);
        AssertBits(m, h.LastMaintenanceFraction);
        Assert.True(m < 0.5, "rig vacuous: clay was not the binding material");

        double lostExact = 100L * (1.0 - Math.Exp(-(1.0 - m) * 10.0 / cfg.Housing.TauYears)) + 0.0;
        long lost = (long)Math.Floor(lostExact);
        Assert.Equal(100L - lost, h.Dwellings.Value);
        AssertBits(lostExact - lost, h.DecayRemainder);
        // Materials used = demand × m, rounded: 24 timber, 12 clay (all of it).
        Assert.Equal(1000L - (long)Math.Round(demandT * m), Stock(next, cfg, "timber"));
        Assert.Equal(12L - (long)Math.Round(demandC * m), Stock(next, cfg, "clay"));
    }

    [Fact]
    public void Build_StopsExactlyAtTheSurplusTarget_NeverAStockpile()
    {
        SimConfig cfg = TwoMaterialConfig();
        // Arm 1 — AT the target (600 people / 6 per dwelling × 1.2 = 120):
        // labor and materials abundant, and construction must still build
        // NOTHING. The cap-deleted mutant builds to its material cap here.
        WorldState atTarget = HousingWorld(cfg, pop: 600, dwellings: 120, timber: 10_000, clay: 10_000, 1.0);
        WorldState next1 = HousingOnly(cfg).Step(atTarget);
        Assert.Equal(120L, House(next1).Dwellings.Value);
        AssertBits(0.0, House(next1).LastLaborUsed);

        // Arm 2 — from NOTHING with everything abundant: one turn builds the
        // deficit exactly — 120 dwellings, not the 5,000 the timber pile
        // could finance. Materials sink at build cost; labor use is published.
        WorldState fromZero = HousingWorld(cfg, pop: 600, dwellings: 0, timber: 10_000, clay: 10_000, 1.0);
        WorldState next2 = HousingOnly(cfg).Step(fromZero);
        HousingRow h = House(next2);
        Assert.Equal(120L, h.Dwellings.Value);
        Assert.Equal(10_000L - (long)(120 * cfg.Housing.BuildTimberPerDwelling), Stock(next2, cfg, "timber"));
        Assert.Equal(10_000L - (long)(120 * cfg.Housing.BuildClayPerDwelling), Stock(next2, cfg, "clay"));
        AssertBits(Math.Min(120 * cfg.Housing.BuildLaborAdultYearsPerDwelling, 600L * 10.0), h.LastLaborUsed);
    }

    [Fact]
    public void Build_EachCapBindsAlone()
    {
        SimConfig cfg = TwoMaterialConfig();

        // Labor-bound: a sliver of construction labor against a 1,200-deficit
        // and mountains of material. Expected built = floor(builderYears /
        // laborPerDwelling), computed through the SAME Share() the system
        // calls so the rig never argues with normalization arithmetic.
        WorldState laborBound = HousingWorld(cfg, pop: 6000, dwellings: 0, timber: 100_000, clay: 100_000, 0.0000875);
        double builderYears = Sectors.Share(laborBound.SectorAllocations[0], Sectors.Construction) * 6000L * 10.0;
        double laborCap = builderYears / cfg.Housing.BuildLaborAdultYearsPerDwelling;
        long expectLabor = (long)Math.Floor(laborCap);
        Assert.True(expectLabor >= 1 && expectLabor < 100, "rig premise: labor is the scarce input");
        WorldState nextL = HousingOnly(cfg).Step(laborBound);
        Assert.Equal(expectLabor, House(nextL).Dwellings.Value);
        AssertBits(Math.Min(expectLabor * cfg.Housing.BuildLaborAdultYearsPerDwelling, builderYears),
            House(nextL).LastLaborUsed);

        // Timber-bound: 21 timber at 2 per dwelling caps at 10 despite labor
        // and clay to spare.
        WorldState timberBound = HousingWorld(cfg, pop: 6000, dwellings: 0, timber: 21, clay: 100_000, 1.0);
        WorldState nextT = HousingOnly(cfg).Step(timberBound);
        Assert.Equal((long)Math.Floor(21.0 / cfg.Housing.BuildTimberPerDwelling), House(nextT).Dwellings.Value);

        // Clay-bound: 7 clay at 1 per dwelling caps at 7.
        WorldState clayBound = HousingWorld(cfg, pop: 6000, dwellings: 0, timber: 100_000, clay: 7, 1.0);
        WorldState nextC = HousingOnly(cfg).Step(clayBound);
        Assert.Equal((long)Math.Floor(7.0 / cfg.Housing.BuildClayPerDwelling), House(nextC).Dwellings.Value);
    }

    [Fact]
    public void LaborSplit_PathBuildDeductsPrevHousingLabor_FlooredAtZero()
    {
        // The law-6 split: PathBuild subtracts Prev.Housing.LastLaborUsed
        // from its construction pool — a table read at the §3.2 one-turn lag,
        // never a system reference. Three founded twins differing ONLY in the
        // published housing labor; construction share is a sliver so the bank
        // never covers a segment and Banked equals the accrual expression
        // bit-for-bit. The labor-split-deleted mutant gives twin B twin A's
        // bank and dies on the bit assert.
        SimConfig sim = TestConfigs.Sim();
        WorldgenConfig wg = TestConfigs.DevWorldgen();

        WorldState Founded()
        {
            WorldState w = WorldFounding.Found(wg, sim, 42);
            for (int s = 0; s < w.Settlements.Count; s++)
            {
                w.SectorAllocations.Add(new SectorAllocationRow(
                    w.Settlements[s].Id, Farming: 1.0 - 1e-8, Herding: 0.0,
                    Extraction: 0.0, Crafting: 0.0, Construction: 1e-8));
            }
            return w;
        }

        WorldState a = Founded();
        SettlementId id0 = a.Settlements[0].Id;
        double builders = Sectors.Share(a.SectorAllocations[0], Sectors.Construction)
            * BandViews.Adults(a.Buckets, id0);
        double byA = builders * 10.0;
        double half = byA / 2.0;
        Assert.True(sim.PathBuild.LaborPerAdultPerYear * byA < 1e-3,
            "rig premise: accrual must stay far below any segment cost");

        static void SetHousingLabor(WorldState w, SettlementId id, double labor)
        {
            for (int i = 0; i < w.Housing.Count; i++)
                if (w.Housing[i].Settlement == id) { w.Housing.Ref(i).LastLaborUsed = labor; return; }
            Assert.Fail("no founded housing row to rig");
        }

        WorldState b = Founded();
        SetHousingLabor(b, id0, half);
        WorldState c = Founded();
        SetHousingLabor(c, id0, byA * 2.0);

        var exec = new TurnExecutor(FlatEra(10.0), [SystemCatalog.PathBuild(sim)]);
        WorldState nextA = exec.Step(a);
        WorldState nextB = exec.Step(b);
        WorldState nextC = exec.Step(c);

        static double Banked(WorldState w, SettlementId id)
        {
            for (int i = 0; i < w.PathProgress.Count; i++)
                if (w.PathProgress[i].Settlement == id) return w.PathProgress[i].Banked;
            return double.NaN;
        }

        double k = sim.PathBuild.LaborPerAdultPerYear;
        AssertBits(k * byA, Banked(nextA, id0));
        AssertBits(k * Math.Max(0.0, byA - half), Banked(nextB, id0));
        Assert.True(Banked(nextB, id0) < Banked(nextA, id0), "the deduction did not bite");
        // Floor at zero: housing labor exceeding the pool leaves NOTHING to
        // accrue — no progress row is even created for the settlement.
        Assert.True(double.IsNaN(Banked(nextC, id0)),
            "over-drawn pool still accrued path labor — the floor is broken");
    }

    [Fact]
    public void OneTurnStop_ShelterFollowsTheStock_NeverCollapsesToZero()
    {
        // THE PACKET'S MOTIVATING ASSERTION, permanent (manifest lens 4: this
        // — not a golden — is the required killer for the rebind-reverted
        // mutant). Under the T3.5 flow stand-in, the before-column MEASURED a
        // settlement that stopped obtaining materials reading Shelter 0.0000
        // the very next reading — instantly and completely homeless. Under
        // the stock, a full stop (no materials, no construction labor) decays
        // the housing by one exact exponent step per turn, and Shelter reads
        // the SURVIVING stock: high and nonzero after a stopped turn.
        //
        // Two steps because satisfaction reads PREV housing (§3.2 lag): step
        // 1 decays the stock; step 2's satisfaction row reads it.
        SimConfig cfg = TestConfigs.Sim();
        WorldState world = HousingWorld(cfg, pop: 600, dwellings: 100, timber: 0, clay: 0, 0.0);
        var exec = new TurnExecutor(FlatEra(10.0),
            [SystemCatalog.Housing(cfg), SystemCatalog.NeedsGrievance(cfg)]);
        WorldState next = exec.Step(exec.Step(world));

        long afterOneTurn = 100L - (long)Math.Floor(
            100L * (1.0 - Math.Exp(-(1.0 - 0.0) * 10.0 / cfg.Housing.TauYears)));
        double expected = Math.Min(1.0, afterOneTurn * cfg.Housing.PersonsPerDwelling / 600L);

        double shelter = double.NaN;
        for (int i = 0; i < next.NeedSatisfactions.Count; i++)
        {
            NeedSatisfactionRow r = next.NeedSatisfactions[i];
            if (r.Settlement.Value == 0 && r.Class.Value == 1 && r.NeedId == 2) shelter = r.Value;
        }
        Assert.False(double.IsNaN(shelter), "no Shelter satisfaction row — the stock is not being read");
        AssertBits(expected, shelter);
        Assert.True(shelter > 0.5,
            $"one stopped turn collapsed Shelter to {shelter:F4} — the flow stand-in's signature, not the stock's");
    }

    [Fact]
    public void Dwellings_ConservationIdentity_LedgerRowsExact()
    {
        // Law 1 directly on the new conserved quantity: after three mixed
        // turns (build + partial maintenance + decay all active), the stock
        // equals endowment + built − decayed, from the LEDGER's own aggregate
        // rows, exactly — no epsilon. Two-material rig keeps the original
        // measured mixed dynamics (build + partial maintenance + decay).
        SimConfig cfg = TwoMaterialConfig();
        WorldState world = HousingWorld(cfg, pop: 600, dwellings: 100, timber: 260, clay: 40, 0.3);
        WorldState end = HousingOnly(cfg).Run(world, 3);

        long sourced = 0, sunk = 0;
        for (int i = 0; i < end.LedgerFlows.Count; i++)
        {
            LedgerFlowRow row = end.LedgerFlows[i];
            if (row.Quantity != ConservedQuantityIds.Dwellings) continue;
            sourced += row.TotalSourced;
            sunk += row.TotalSunk;
        }
        long stock = 0;
        for (int i = 0; i < end.Housing.Count; i++) stock += end.Housing[i].Dwellings.Value;
        Assert.Equal(sourced - sunk, stock);
        Assert.True(sunk > 0, "rig vacuous: no decay ever fired across the horizon");
    }

    [Fact]
    public void Decay_DtSensitivity_IsBoundedAndByDesign_NotAccidental()
    {
        // Law 3 at the mechanism's quantization boundary (the ToolWear
        // precedent's shape): the exponent integrates exactly, so the ONLY
        // dt-sensitivity is whole-unit rounding drift — one 10-year turn vs
        // ten 1-year turns on a 100,000-dwelling unmaintained stock must land
        // within a few units of each other. A mutant that hardcodes a
        // per-turn loss (ignoring dt) loses 10× more on the fine path and
        // fails by tens of thousands.
        SimConfig cfg = TestConfigs.Sim();
        WorldState coarse = HousingOnly(cfg, dt: 10.0)
            .Step(HousingWorld(cfg, pop: 600, dwellings: 100_000, timber: 0, clay: 0, 0.0));
        WorldState fine = HousingOnly(cfg, dt: 1.0)
            .Run(HousingWorld(cfg, pop: 600, dwellings: 100_000, timber: 0, clay: 0, 0.0), 10);

        long coarseD = House(coarse).Dwellings.Value;
        long fineD = House(fine).Dwellings.Value;
        Assert.True(Math.Abs(coarseD - fineD) <= 5,
            $"dt drift exceeded the quantization bound: {coarseD} (dt=10) vs {fineD} (10×dt=1)");
        Assert.True(coarseD < 100_000, "rig vacuous: nothing decayed");
    }
}
