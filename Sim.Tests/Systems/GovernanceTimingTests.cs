using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.GovernanceRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D4 — THE GOVERNING LOOP'S TIMING, pinned turn-exact (the T1.9 precedent: replay equality
/// proves reproducibility, not WHEN an order lands), plus the defects M5B shipped unprotected: no test
/// ran ProductionSystem with a tax in place, and no test pinned when a road change reaches the stored
/// reach or what a new colony reads.
///
///   SetTaxRate stamped t ─step t→t+1─▶ TaxPolicies row in state t+1 (its ONLY effect by then)
///     ─step t+1→t+2─▶ first change in production, and (H2) the first levy-grievance rows and the first
///       fall in the happiness READING of state t+2 ─step t+2→t+3─▶ migration and the AI valve answer it
///   DevelopRoads stamped t ─▶ TransportEdges in t+1 ─▶ SettlementDistances in t+2 (CatchmentSystem
///       reads PREV) ─▶ ControlRow.Strength in t+3 (GovernanceSystem reads PREV) ─▶ production in the
///       step t+3→t+4 (ProductionSystem reads PREV Strength)
///   colony founded by the step t→t+1 ─▶ Strength 0.0 in t+1 and t+2 (no route on record) ─▶ its reach in t+3
///   founded world (no distances at turn 0) ─▶ every non-capital Strength 0.0 in turn 1 ─▶ its reach in turn 2
/// </summary>
public class GovernanceTimingTests
{
    private const int IssueTurn = 3;

    private static TurnExecutor Full(SimConfig cfg, WorldgenConfig worldgen, OrderLog? orders = null)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(cfg, worldgen)), orders);
    }

    private static List<WorldState> Run(WorldState genesis, OrderLog orders, int turns)
    {
        TurnExecutor exec = Full(Cfg(), TestConfigs.DevWorldgen(), orders);
        var worlds = new List<WorldState> { genesis };
        WorldState w = genesis;
        for (int t = 0; t < turns; t++) worlds.Add(w = exec.Step(w));
        return worlds;
    }

    private static long Produced(IReadOnlyWorldState w, SettlementId s, GoodId good)
    {
        int i = GoodStockIndex.IndexOf(w.GoodStocks, s, good);
        return i < 0 ? 0 : w.GoodStocks[i].LastProducedUnits;
    }

    private static string HashWithoutTaxPolicies(WorldState w)
    {
        WorldState c = w.Clone();
        c.TaxPolicies.Clear();
        return WorldHash.ComputeHex(c);
    }

    // ---- 1. THE ORDER-DELIVERY PIN ------------------------------------------------------------

    [Fact]
    public void ATaxIssuedAtTurnT_WritesThePolicyInStateTPlus1_AndFirstMovesProductionInTheStepTPlus1ToTPlus2()
    {
        (WorldState genesis, PolityId p) = Founded();
        Grant(genesis, p);
        SettlementId seat = Seat(genesis, p);
        var grain = new GoodId(Cfg().Goods!.GrainId);

        var taxed = new OrderLog();
        taxed.Append(SetTax(IssueTurn, p, 40.0));
        List<WorldState> t = Run(genesis.Clone(), taxed, IssueTurn + 4);
        List<WorldState> u = Run(genesis.Clone(), new OrderLog(), IssueTurn + 4);

        // Up to and including the issuing turn's state, the order has done nothing.
        for (int k = 0; k <= IssueTurn; k++)
            Assert.Equal(WorldHash.ComputeHex(u[k]), WorldHash.ComputeHex(t[k]));

        // State t+1: the policy row exists — and it is the ONLY difference. Production of the step
        // t → t+1 read PREV (state t, untaxed), so every stock, flow and Strength is the twin's.
        Assert.Equal(0, u[IssueTurn + 1].TaxPolicies.Count);
        Assert.Equal(0.40, Governance.NominalTaxRate(t[IssueTurn + 1], p), 9);
        Assert.Equal(0.0, Governance.NominalTaxRate(t[IssueTurn], p));
        Assert.Equal(WorldHash.ComputeHex(u[IssueTurn + 1]), HashWithoutTaxPolicies(t[IssueTurn + 1]));
        Assert.Equal(Produced(u[IssueTurn + 1], seat, grain), Produced(t[IssueTurn + 1], seat, grain));

        // H2 (2026-10-05, Director §4: the burden ACCUMULATES): the happiness READING of state t+1 is UNCHANGED —
        // the levy reaches welfare only through the segments' accumulated levy grievance, which NeedsGrievanceSystem
        // first accrues in the step t+1 → t+2 (reading the policy from PREV). State t+2 holds the first TaxGrievance
        // rows and the first fall in happiness; migration and the AI valve read it in the step t+2 → t+3. (ADR-033
        // D4's first form scaled happiness by 1 − r already in state t+1 — superseded.)
        Assert.Equal(SettlementHappiness.Of(u[IssueTurn], seat, Cfg()), SettlementHappiness.Of(t[IssueTurn], seat, Cfg()));
        Assert.Equal(SettlementHappiness.Of(u[IssueTurn + 1], seat, Cfg()), SettlementHappiness.Of(t[IssueTurn + 1], seat, Cfg()));
        Assert.Equal(0, t[IssueTurn + 1].TaxGrievances.Count);
        Assert.True(t[IssueTurn + 2].TaxGrievances.Count > 0, "no levy grievance accrued in the step t+1 → t+2");
        Assert.True(Unrest.LevyPressure(t[IssueTurn + 2], seat, Cfg()) > 0.0);
        Assert.True(SettlementHappiness.Of(t[IssueTurn + 2], seat, Cfg()) < SettlementHappiness.Of(u[IssueTurn + 2], seat, Cfg()));

        // State t+2: the FIRST production change — the capital's harvest is raised by the extraction
        // multiplier 1 + 0.3 × 0.40 × Strength(seat = 1).
        long untaxedHarvest = Produced(u[IssueTurn + 2], seat, grain);
        long taxedHarvest = Produced(t[IssueTurn + 2], seat, grain);
        Assert.True(untaxedHarvest > 0, "the capital harvested nothing — the pin would be vacuous");
        Assert.True(taxedHarvest > untaxedHarvest, $"the levy did not reach production in step t+1→t+2 ({untaxedHarvest} → {taxedHarvest})");
        Assert.NotEqual(WorldHash.ComputeHex(u[IssueTurn + 2]), HashWithoutTaxPolicies(t[IssueTurn + 2]));
    }

    [Fact]
    public void SaveBetweenIssueAndEffect_LoadAndContinue_EqualsTheUninterruptedRun_HashForHash()
    {
        (WorldState genesis, PolityId p) = Founded();
        Grant(genesis, p);
        var orders = new OrderLog();
        orders.Append(SetTax(IssueTurn, p, 40.0));
        orders.Append(SetTax(IssueTurn + 2, p, 75.0));   // a second edict inside the continued window
        const int horizon = IssueTurn + 7;

        List<WorldState> straight = Run(genesis.Clone(), orders, horizon);

        // Save the state of t+1 — the policy row written, its effect not yet produced — and resume.
        WorldState saved = straight[IssueTurn + 1];
        Assert.True(Governance.HasPolicy(saved, p));
        using var ms = new MemoryStream();
        Snapshot.Save(saved, ms);
        ms.Position = 0;
        WorldState loaded = Snapshot.Load(ms, saved.Terrain);
        Assert.Equal(WorldHash.ComputeHex(saved), WorldHash.ComputeHex(loaded));

        TurnExecutor resumed = Full(Cfg(), TestConfigs.DevWorldgen(), orders);
        WorldState w = loaded;
        for (int k = IssueTurn + 2; k <= horizon; k++)
        {
            w = resumed.Step(w);
            Assert.Equal(WorldHash.ComputeHex(straight[k]), WorldHash.ComputeHex(w));
        }
        Assert.Equal(0.75, Governance.NominalTaxRate(w, p), 9);
    }

    // ---- 2. PRODUCTION WITH A TAX IN PLACE: ALL THREE APPLICATION SITES -----------------------

    /// <summary>A founded dev world stepped twice through the full pipeline, so catchments, distances
    /// and the stored reach exist (a turn-0 world has no arable land on record yet).</summary>
    private static (WorldState World, PolityId Player) Warmed()
    {
        (WorldState w, PolityId p) = Founded();
        return (Run(w, new OrderLog(), 2)[2], p);
    }

    [Fact]
    public void ProductionWithATaxInPlace_RaisesFarmingDepositAndCraftOutput_ByExactlyTheExtractionMultiplier()
    {
        (WorldState w, PolityId p) = Warmed();
        // R1: pottery firing is research-gated; the subject here is the extraction multiplier at the crafting
        // site, so the realm knows its crafts (TestConfigs.KnowRecipes — knowledge rows only).
        TestConfigs.KnowRecipes(w, Cfg());
        SettlementId seat = Seat(w, p);
        Assert.Equal(1.0, Governance.ControlStrength(w, p, seat));
        GoodsConfig goods = Cfg().Goods!;
        // Abundant craft inputs, so the LABOUR cap binds and the multiplier is visible in crafted output.
        foreach (string input in new[] { "clay", "timber", "fiber" })
            AddStock(w, seat, new GoodId(goods.IdOf(input)), 1_000_000);

        WorldState taxedWorld = w.Clone();
        taxedWorld.TaxPolicies.Add(new TaxPolicyRow(p, 0.5));   // Strength(seat) = 1: effective 0.5
        double e = Governance.ExtractionMultiplier(taxedWorld, seat, Cfg());
        Assert.Equal(1.15, e, 12);

        var production = new TurnExecutor(FlatEra(), [SystemCatalog.Production(Cfg())]);
        WorldState u = production.Step(w);
        WorldState t = production.Step(taxedWorld);

        // The realised RATE × dt of a produced row = produced + new remainder − old remainder.
        double Realised(WorldState before, WorldState after, GoodId good)
        {
            int b = GoodStockIndex.IndexOf(before.GoodStocks, seat, good);
            int a = GoodStockIndex.IndexOf(after.GoodStocks, seat, good);
            return after.GoodStocks[a].LastProducedUnits + after.GoodStocks[a].ProduceRemainder - before.GoodStocks[b].ProduceRemainder;
        }

        int sites = 0;
        // Site 1 — farming (grain).
        var grain = new GoodId(goods.GrainId);
        Assert.True(Realised(w, u, grain) > 0.0);
        Assert.Equal(e, Realised(taxedWorld, t, grain) / Realised(w, u, grain), 9);
        sites++;

        // Site 2 — the deposit sectors: herding/fishing (food) AND extraction (raw goods).
        bool food = false, raw = false;
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow d = w.Deposits[i];
            if (d.Settlement != seat || d.Abundance <= 0.0) continue;
            double before = Realised(w, u, d.Good);
            if (!(before > 0.0)) continue;
            Assert.Equal(e, Realised(taxedWorld, t, d.Good) / before, 9);
            if (d.Good.Value == goods.IdOf("fish") || d.Good.Value == goods.IdOf("livestock")) food = true; else raw = true;
        }
        Assert.True(food, "no herding/fishing output at the capital — site 2 (food) unexercised");
        Assert.True(raw, "no extraction output at the capital — site 2 (raw) unexercised");
        sites++;

        // Site 3 — crafting, on the labour-allowed output (inputs abundant, so it is what is made).
        foreach (string crafted in new[] { "pottery", "cloth" })
        {
            var good = new GoodId(goods.IdOf(crafted));
            Assert.True(Realised(w, u, good) > 0.0, $"{crafted} was not crafted — site 3 unexercised");
            Assert.Equal(e, Realised(taxedWorld, t, good) / Realised(w, u, good), 9);
        }
        sites++;
        Assert.Equal(3, sites);

        // Untaxed is BIT-IDENTICAL to a config with no governance at all (the multiplier is exactly 1).
        WorldState bare = new TurnExecutor(FlatEra(), [SystemCatalog.Production(Cfg() with { Governance = null })]).Step(w);
        Assert.Equal(WorldHash.ComputeHex(u), WorldHash.ComputeHex(bare));
    }

    [Fact]
    public void ALevyCannotConjureCraftOutputTheMaterialsDoNotSupport()
    {
        // The extraction multiplier rides the LABOUR-allowed output; the Leontief input caps still
        // bind. With fiber scarce AND none extracted this step (the settlement's extraction share is
        // zero — Craft reads the live stocks, so same-step extraction would otherwise feed it, and the
        // levy raises that extraction too), weaving is input-capped, so a 100 % levy (×1.3) makes NOT
        // ONE more cloth than the untaxed step — it only raises the demand the market sees.
        (WorldState w, PolityId p) = Warmed();
        SettlementId seat = Seat(w, p);
        GoodsConfig goods = Cfg().Goods!;
        var fiber = new GoodId(goods.IdOf("fiber"));
        var cloth = new GoodId(goods.IdOf("cloth"));
        bool replaced = false;
        var crafting = new SectorAllocationRow(seat, 0.5, 0.0, 0.0, 0.5, 0.0);
        for (int i = 0; i < w.SectorAllocations.Count; i++)
            if (w.SectorAllocations[i].Settlement == seat) { w.SectorAllocations[i] = crafting; replaced = true; }
        if (!replaced) w.SectorAllocations.Add(crafting);
        int fi0 = GoodStockIndex.IndexOf(w.GoodStocks, seat, fiber);
        long have = w.GoodStocks[fi0].Amount.Value;
        if (have > 30)
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(fi0).Amount, ConservedQuantityIds.OfGood(fiber),
                ReasonIds.InitialEndowment, have - 30, FlowDirection.Sink, OverdrawPolicy.Throw);
        else
            AddStock(w, seat, fiber, 30 - have);
        long fiberStock = w.GoodStocks[GoodStockIndex.IndexOf(w.GoodStocks, seat, fiber)].Amount.Value;
        WorldState taxedWorld = w.Clone();
        taxedWorld.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));
        var production = new TurnExecutor(FlatEra(), [SystemCatalog.Production(Cfg())]);
        WorldState u = production.Step(w);
        WorldState t = production.Step(taxedWorld);
        long untaxedCloth = Produced(u, seat, cloth);
        long taxedCloth = Produced(t, seat, cloth);
        Assert.True(taxedCloth > 0, "nothing was woven — the cap is not exercised");
        Assert.True(untaxedCloth == taxedCloth,
            $"untaxed {untaxedCloth} vs taxed {taxedCloth} cloth from {fiberStock} fiber (remainder {w.GoodStocks[GoodStockIndex.IndexOf(w.GoodStocks, seat, fiber)].ConsumeRemainder})");
        Assert.True(taxedCloth <= fiberStock / 3);   // 3 fiber per cloth
        long untaxedDemand = u.GoodStocks[GoodStockIndex.IndexOf(u.GoodStocks, seat, fiber)].LastInputDemandUnits;
        long taxedDemand = t.GoodStocks[GoodStockIndex.IndexOf(t.GoodStocks, seat, fiber)].LastInputDemandUnits;
        Assert.True(taxedDemand > untaxedDemand, "the levy's labour-side effect is invisible in the published demand");
    }

    // ---- 3. ROADS → STORED REACH → PRODUCTION --------------------------------------------------

    [Fact]
    public void ARoadBuiltByStepT_ReachesDistancesInTPlus2_StrengthInTPlus3_AndProductionInTheStepTPlus3ToTPlus4()
    {
        (WorldState genesis, PolityId p) = Founded();
        Grant(genesis, p);                                   // taxation
        Grant(genesis, p, "track_road");                     // the trackway (infra.road_track)
        genesis.TaxPolicies.Add(new TaxPolicyRow(p, 0.5));   // a standing levy, so reach has a consumer

        // The road order is built from the world it is issued in (the player's slider path).
        List<WorldState> warm = Run(genesis.Clone(), new OrderLog(), IssueTurn);
        var roads = new OrderLog();
        roads.Append(RoadDevelopmentQuery.DevelopOrder(warm[IssueTurn], p, 100.0));
        List<WorldState> r = Run(genesis.Clone(), roads, IssueTurn + 4);
        List<WorldState> u = Run(genesis.Clone(), new OrderLog(), IssueTurn + 4);
        const int t = IssueTurn;

        static bool SameDistances(IReadOnlyWorldState a, IReadOnlyWorldState b)
        {
            if (a.SettlementDistances.Count != b.SettlementDistances.Count) return false;
            for (int i = 0; i < a.SettlementDistances.Count; i++)
                if (!a.SettlementDistances[i].Equals(b.SettlementDistances[i])) return false;
            return true;
        }
        static bool SameStrengths(IReadOnlyWorldState a, IReadOnlyWorldState b)
        {
            if (a.Controls.Count != b.Controls.Count) return false;
            for (int i = 0; i < a.Controls.Count; i++)
                if (BitConverter.DoubleToInt64Bits(a.Controls[i].Strength) != BitConverter.DoubleToInt64Bits(b.Controls[i].Strength)) return false;
            return true;
        }

        Assert.Equal(0, r[t].TransportEdges.Count);
        Assert.True(r[t + 1].TransportEdges.Count > 0, "the order built no road — the pin would be vacuous");
        Assert.True(SameDistances(u[t + 1], r[t + 1]));      // the step that built it routed over the old network
        Assert.False(SameDistances(u[t + 2], r[t + 2]));     // catchment re-routes on the NEXT step (reads PREV)
        Assert.True(SameStrengths(u[t + 2], r[t + 2]));      // governance has not seen the new distances yet
        Assert.False(SameStrengths(u[t + 3], r[t + 3]));     // ...and writes the new reach one step later

        // Strength in t+3 IS the reach over the road-aware distances of t+2 — the ONE computer, on PREV.
        SettlementId moved = default;
        bool found = false;
        for (int i = 0; i < r[t + 3].Controls.Count; i++)
        {
            ControlRow row = r[t + 3].Controls[i];
            Assert.Equal(BitConverter.DoubleToInt64Bits(Governance.AdministrativeReach(r[t + 2], row.Polity, row.Place, Cfg().Governance!)),
                BitConverter.DoubleToInt64Bits(row.Strength));
            if (!found && BitConverter.DoubleToInt64Bits(row.Strength) != BitConverter.DoubleToInt64Bits(u[t + 3].Controls[i].Strength))
            { moved = row.Place; found = true; }
        }
        Assert.True(found);

        // Production reads PREV Strength: the step t+3 → t+4 is the first whose extraction multiplier
        // moved (state t+2 still carries the twin's Strength). Isolate the Strength channel from the
        // road's catchment effect by stepping production on state t+3 with and without the new reach.
        Assert.Equal(Governance.ExtractionMultiplier(u[t + 2], moved, Cfg()), Governance.ExtractionMultiplier(r[t + 2], moved, Cfg()));
        Assert.NotEqual(Governance.ExtractionMultiplier(u[t + 3], moved, Cfg()), Governance.ExtractionMultiplier(r[t + 3], moved, Cfg()));
        WorldState oldReach = r[t + 3].Clone();
        for (int i = 0; i < oldReach.Controls.Count; i++)
            if (oldReach.Controls[i].Place == moved)
                oldReach.Controls[i] = oldReach.Controls[i] with { Strength = u[t + 3].Controls[i].Strength };
        var production = new TurnExecutor(FlatEra(), [SystemCatalog.Production(Cfg())]);
        var grain = new GoodId(Cfg().Goods!.GrainId);
        Assert.NotEqual(Produced(production.Step(oldReach), moved, grain), Produced(production.Step(r[t + 3]), moved, grain));
    }

    // ---- 4. A NEW COLONY'S FIRST TURNS ---------------------------------------------------------

    [Fact]
    public void ANewColony_ReadsStrengthZero_UntaxedAndUnburdened_UntilItsRouteIsOnRecord()
    {
        // ColonizationSystem writes the colony's control row at 1.0; GovernanceSystem, later in the
        // SAME step, rewrites it from PREV — where the colony has no distance row — as 0.0
        // (unadministered, never "perfectly administered"). Catchment computes the colony's distances
        // in the next step (state t+2), and governance reads them one step after that (state t+3).
        WorldState w = Founded(settlements: 4).World;
        PolityId p = new(1);
        var handoff = new TurnExecutor(FlatEra(), [
            SystemCatalog.Catchment(Cfg()),
            SystemCatalog.Migration(Cfg()),
            SystemCatalog.Colonization(Cfg(), TestConfigs.DevWorldgen()),
            SystemCatalog.Governance(Cfg())]);
        w = handoff.Step(w);   // warm: distances on record
        SettlementId src = Seat(w, p);
        // Every other settlement a food-less ruin (ADR-012's "no viable destination"), the source hungry.
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            if (w.Settlements[s].Id == src) continue;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Settlement == w.Settlements[s].Id)
                    w.GoodStocks[i] = w.GoodStocks[i] with { Amount = Conserved.Zero, LastProducedUnits = 0 };
        }
        SetDeficit(w, src, 0.40);
        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.5));
        int before = w.Settlements.Count;

        WorldState t1 = handoff.Step(w);
        Assert.Equal(before + 1, t1.Settlements.Count);
        SettlementId colony = t1.Settlements[before].Id;
        Assert.True(EmpireQuery.ControlsSettlement(t1, p, colony));   // control inherited from the parent
        Assert.Equal(0.0, Governance.ControlStrength(t1, p, colony));
        Assert.Equal(0.0, Governance.EffectiveTaxRate(t1, colony, Cfg()));
        Assert.Equal(1.0, SettlementHappiness.TaxSufficiency(t1, colony, Cfg()));
        Assert.Equal(1.0, Governance.ExtractionMultiplier(t1, colony, Cfg()));

        WorldState t2 = handoff.Step(t1);
        Assert.True(HasRoute(t2, src, colony), "catchment did not put the colony on record in t+2");
        Assert.False(HasRoute(t1, src, colony));
        Assert.Equal(0.0, Governance.ControlStrength(t2, p, colony));   // governance read t+1: still no route

        WorldState t3 = handoff.Step(t2);
        double reach = Governance.ControlStrength(t3, p, colony);
        Assert.Equal(Governance.AdministrativeReach(t2, p, colony, Cfg().Governance!), reach);
        Assert.True(reach > 0.0, "the colony is on the network but reads no reach");
        Assert.Equal(0.5 * reach, Governance.EffectiveTaxRate(t3, colony, Cfg()), 12);
    }

    [Fact]
    public void AFoundedWorld_ReadsEveryNonCapitalStrengthZeroInTurn1_AndItsReachFromTurn2()
    {
        // The colony case at founding. WorldFounding writes the control rows at 1.0 and NO
        // SettlementDistances (CatchmentSystem computes them inside a step), so the step 0 → 1
        // rewrites every non-capital Strength from an empty PREV table as 0.0 and the capital's as
        // 1.0; the step 1 → 2 reads turn 1's distances and writes the reach. An edict stamped turn 0
        // DOES meet the zero rows: it is policy in turn 1, and production in the step 1 → 2 reads
        // turn 1's Strength, so for that one step only the capital is taxed.
        (WorldState genesis, PolityId p) = Founded();
        Grant(genesis, p);
        Assert.Equal(0, genesis.SettlementDistances.Count);
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 50.0));
        List<WorldState> worlds = Run(genesis, orders, 2);
        WorldState t1 = worlds[1], t2 = worlds[2];
        SettlementId seat = Seat(t1, p);
        Assert.True(t1.SettlementDistances.Count > 0, "catchment wrote no distances in the step 0 → 1");
        Assert.Equal(0.5, Governance.NominalTaxRate(t1, p));

        int others = 0;
        for (int i = 0; i < t1.Controls.Count; i++)
        {
            ControlRow row = t1.Controls[i];
            if (row.Place == seat) { Assert.Equal(1.0, row.Strength); continue; }
            others++;
            Assert.Equal(0.0, row.Strength);
            Assert.Equal(1.0, Governance.ExtractionMultiplier(t1, row.Place, Cfg()));
        }
        Assert.True(others > 0, "the founded rig holds no settlement besides the capital");
        Assert.Equal(1.0 + 0.3 * 0.5, Governance.ExtractionMultiplier(t1, seat, Cfg()));

        int reached = 0;
        for (int i = 0; i < t2.Controls.Count; i++)
        {
            ControlRow row = t2.Controls[i];
            double expected = Governance.AdministrativeReach(t1, row.Polity, row.Place, Cfg().Governance!);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(row.Strength));
            if (row.Place != seat && row.Strength > 0.0) reached++;
        }
        Assert.True(reached > 0, "no non-capital settlement reads a reach in turn 2");
    }

    private static bool HasRoute(IReadOnlyWorldState w, SettlementId from, SettlementId to)
    {
        for (int i = 0; i < w.SettlementDistances.Count; i++)
            if (w.SettlementDistances[i].From == from && w.SettlementDistances[i].To == to) return true;
        return false;
    }
}
