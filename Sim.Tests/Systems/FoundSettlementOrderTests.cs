using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 S1b — THE FOUND SETTLEMENT ORDER (D-051 §5; ADR-036, PROPOSED). OrderKind 9 founds a settlement through the
/// EXISTING colonization path (ColonizationSystem.Found: the settlement row, inherited control, goods, deposits,
/// Class-A rows, the people and provisions by Ledger.Transfer, the ADR-012 seed). Legality is ONE predicate,
/// FoundingQuery.Check, judged on PREV — the world the order was issued from; the party departs live.
///
/// Every rig is order-driven: emergent B1 founding is near-dormant on the shipped weather (CR-020 §B.2), so a
/// test waiting for a natural colony would be vacuous. The worlds are the dev preset (256², 4 settlements, seed
/// 42) after ONE orderless production step (turn 1), so PREV carries catchments and settlement distances.
/// </summary>
public class FoundSettlementOrderTests
{
    private static readonly PolityId Player = new(1);
    private static SimConfig Cfg => FoundingRigs.Cfg();

    /// <summary>Dev seed 42 after one orderless production step: the state of turn 1.</summary>
    private static readonly Lazy<WorldState> Turn1 = new(
        () => FoundingRigs.Pipeline(FoundingRigs.Cfg(), null).Step(FoundingRigs.Dev(42UL)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static WorldState W1() => Turn1.Value.Clone();

    private static OrderLog Log(params OrderRecord[] orders)
    {
        var log = new OrderLog();
        foreach (OrderRecord o in orders) log.Append(o);
        return log;
    }

    private static ControlRow ControlOf(IReadOnlyWorldState w, int settlement)
    {
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place.Value == settlement) return w.Controls[i];
        throw new InvalidOperationException($"no control row for settlement {settlement}");
    }

    private static int CatchmentRowsOf(IReadOnlyWorldState w, int settlement)
    {
        int n = 0;
        for (int i = 0; i < w.CatchmentNodes.Count; i++) if (w.CatchmentNodes[i].Settlement.Value == settlement) n++;
        return n;
    }

    // ================================================================== DELIVERY (turn-exact)

    [Fact]
    public void AnOrderStampedT_FoundsInStateTPlus1_ItsCatchmentAppearsInTPlus2_AndReachIsZeroUntilTPlus3()
    {
        // CLAUDE.md: every order-delivery semantic gets its own turn-exact pin — replay equality cannot see a
        // stamping drift (T1.9). The colony's whole first three states are pinned.
        WorldState w = W1();
        long t = w.Clock.Turn;
        SettlementId src = w.Settlements[0].Id;
        int site = FoundingRigs.NearestLegalSite(w, Cfg, FoundingRigs.Frontier(w), Player, src);
        Assert.True(site >= 0, "no legal site — the rig is void");
        int colony = FoundingRigs.MaxSettlementId(w) + 1;

        OrderRecord order = FoundingQuery.FoundOrder(w, Player, src, site);
        Assert.Equal((t, OrderKind.FoundSettlement, src.Value, (double)site), (order.Turn, order.Kind, order.TargetId, order.Amount));
        TurnExecutor exec = FoundingRigs.Pipeline(Cfg, Log(order));

        Assert.False(FoundingRigs.TryGetSettlement(w, colony, out _));
        WorldState s1 = exec.Step(w);                                    // state t+1
        Assert.True(FoundingRigs.TryGetSettlement(s1, colony, out SettlementRow row), "not founded in state t+1");
        Assert.Equal(site, row.SiteCell);
        Assert.Equal(t + 1, row.FoundedTurn);
        Assert.Equal(Player, ControlOf(s1, colony).Polity);              // a city the issuing civilization controls
        Assert.Equal(0.0, ControlOf(s1, colony).Strength);               // GovernanceSystem: no distance row in PREV
        Assert.Equal(0, CatchmentRowsOf(s1, colony));                    // CatchmentSystem runs FIRST: next step

        WorldState s2 = exec.Step(s1);                                   // state t+2
        Assert.True(CatchmentRowsOf(s2, colony) > 0, "no catchment in state t+2");
        Assert.Equal(0.0, ControlOf(s2, colony).Strength);               // PREV (t+1) still has no distance row

        WorldState s3 = exec.Step(s2);                                   // state t+3
        Assert.True(ControlOf(s3, colony).Strength > 0.0, "no administrative reach in state t+3");
    }

    [Fact]
    public void TheStampIsTheDeliveryKey_AnOrderStampedTPlus1_FoundsOneStepLater()
    {
        // The same order stamped one turn later leaves state t+1 bit-identical to the orderless step and founds in
        // t+2 with FoundedTurn t+2 — so FoundedTurn is the delivery step's, not a constant offset from issue.
        WorldState w = W1();
        long t = w.Clock.Turn;
        SettlementId src = w.Settlements[0].Id;
        WorldState plain1 = FoundingRigs.Pipeline(Cfg, null).Step(w.Clone());
        int site = FoundingRigs.NearestLegalSite(plain1, Cfg, FoundingRigs.Frontier(plain1), Player, src);
        int colony = FoundingRigs.MaxSettlementId(w) + 1;

        TurnExecutor exec = FoundingRigs.Pipeline(Cfg, Log(FoundingQuery.FoundOrder(plain1, Player, src, site)));
        WorldState s1 = exec.Step(w.Clone());
        Assert.Equal(WorldHash.ComputeHex(plain1), WorldHash.ComputeHex(s1));
        Assert.False(FoundingRigs.TryGetSettlement(s1, colony, out _));
        WorldState s2 = exec.Step(s1);
        Assert.True(FoundingRigs.TryGetSettlement(s2, colony, out SettlementRow row));
        Assert.Equal(t + 2, row.FoundedTurn);
        Assert.Equal(site, row.SiteCell);
    }

    // ================================================================== CONSERVATION

    [Fact]
    public void PeopleAndGrainMoveOnlyByTheLedger_Exactly_AndNoSourceBucketIsEmptied()
    {
        // Colonization alone, so every change in the step is the founding's. The party and provisions are
        // re-derived independently from PREV with the two public arms and compared with exact equality.
        WorldState prev = FoundingRigs.Dev(42UL);
        SettlementId src = prev.Settlements[0].Id;
        int site = FoundingRigs.NearestLegalSite(prev, Cfg, FoundingRigs.Frontier(prev), Player, src);
        int colony = FoundingRigs.MaxSettlementId(prev) + 1;
        var grain = new GoodId(Cfg.Goods!.GrainId);

        var plan = new long[prev.Buckets.Count];
        long party = FoundingQuery.AllocateParty(prev.Buckets, src, FoundingQuery.PartyFraction(Cfg), plan);
        long provisions = FoundingQuery.ProvisionsShare(prev.GoodStocks, prev.Buckets, src, grain, party);
        Assert.True(party >= 1 && provisions >= 1, $"vacuous rig: party {party}, provisions {provisions}");
        Assert.Equal((long)Math.Floor(FoundingQuery.PartyFraction(Cfg) * FoundingRigs.People(prev, src)), party);

        // THE PROPORTIONAL FLOOR (ADR-036 §3), checked independently of the allocator: every non-empty source
        // bucket gives floor(T x c / N) or one more (the remainder) — never fewer, never the row-order remainder
        // alone. Without this the key-for-key check below compares the plan with itself, and a round-robin
        // allocator (no floor step) took 5 of the 6 people of a 60-64 cohort here and passed.
        long sourcePeople = FoundingRigs.People(prev, src);
        int nonEmpty = 0;
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            BucketRow b = prev.Buckets[i];
            if (b.Settlement != src || b.Count.Value <= 0) continue;
            nonEmpty++;
            long floor = party * b.Count.Value / sourcePeople;
            Assert.True(plan[i] == floor || plan[i] == floor + 1, $"bucket row {i} (count {b.Count.Value}): gave {plan[i]}, proportional floor {floor}");
        }
        Assert.True(nonEmpty > 2, $"vacuous rig: {nonEmpty} non-empty source buckets");

        WorldState next = FoundingRigs.ColonizationOnly(Cfg, Log(FoundingQuery.FoundOrder(prev, Player, src, site))).Step(prev.Clone());
        var colonyId = new SettlementId(colony);

        Assert.Equal(FoundingRigs.People(prev), FoundingRigs.People(next));
        Assert.Equal(FoundingRigs.Grain(prev), FoundingRigs.Grain(next));
        Assert.Equal(party, FoundingRigs.People(next, colonyId));
        Assert.Equal(FoundingRigs.People(prev, src) - party, FoundingRigs.People(next, src));
        Assert.Equal(provisions, FoundingRigs.Grain(next, colonyId));
        Assert.Equal(FoundingRigs.Grain(prev, src) - provisions, FoundingRigs.Grain(next, src));
        Assert.True(ConservationAuditor.IsConserved(next, Cfg.Goods, out string report), report);

        // Key for key: each source bucket gave exactly its planned share and kept at least one person.
        int moved = 0;
        for (int i = 0; i < prev.Buckets.Count; i++)
        {
            BucketRow b = prev.Buckets[i];
            if (b.Settlement != src) continue;
            Assert.Equal(b.Count.Value - plan[i], next.Buckets[i].Count.Value);
            if (b.Count.Value > 0) Assert.True(next.Buckets[i].Count.Value >= 1, $"bucket row {i} emptied");
            long arrived = -1;
            for (int j = prev.Buckets.Count; j < next.Buckets.Count; j++)
            {
                BucketRow c = next.Buckets[j];
                if (c.Settlement == colonyId && c.Culture == b.Culture && c.Religion == b.Religion
                    && c.Class == b.Class && c.CohortIdx == b.CohortIdx) { arrived = c.Count.Value; break; }
            }
            Assert.Equal(plan[i], arrived);
            if (plan[i] > 0) moved++;
        }
        Assert.True(moved > 1, "the party came from a single bucket — the allocation is not exercised");

        // Every other good is untouched (only grain travels as provisions); the colony's goods rows start empty.
        for (int i = 0; i < prev.GoodStocks.Count; i++)
            if (prev.GoodStocks[i].Good != grain)
                Assert.Equal(prev.GoodStocks[i].Amount.Value, next.GoodStocks[i].Amount.Value);
    }

    [Fact]
    public void TheFullPipelineStepThatFoundsBalancesTheLedgerExactly()
    {
        WorldState w = W1();
        SettlementId src = w.Settlements[0].Id;
        int site = FoundingRigs.NearestLegalSite(w, Cfg, FoundingRigs.Frontier(w), Player, src);
        WorldState next = FoundingRigs.Pipeline(Cfg, Log(FoundingQuery.FoundOrder(w, Player, src, site))).Step(w);
        Assert.Equal(w.Settlements.Count + 1, next.Settlements.Count);
        Assert.True(ConservationAuditor.IsConserved(next, Cfg.Goods, out string report), report);
    }

    // ================================================================== REJECTION ARMS

    public static TheoryData<string> Arms => new()
    {
        "UnknownSource", "NotControlled-rival", "NotControlled-stateless", "OffMap", "Water", "PoorSite",
        "Occupied", "TooClose", "Claimed", "Unreachable", "NoProvisions", "NoParty",
    };

    private sealed record ArmCase(WorldState Prev, SimConfig Cfg, WorldgenConfig Worldgen, OrderRecord Order, FoundingVerdict Want);

    private static ArmCase Build(string arm)
    {
        WorldgenConfig wg = TestConfigs.Worldgen();
        WorldState w = W1();
        SettlementId src = w.Settlements[0].Id;
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        int legal = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, src);
        int Cell(FoundingVerdict v) => FoundingRigs.FirstCell(w, Cfg, f, Player, src, v, 0, 80);

        switch (arm)
        {
            case "UnknownSource":
                return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, new SettlementId(99), legal), FoundingVerdict.UnknownSource);
            case "NotControlled-rival":
            {
                // aiEmpires 1: settlements alternate player / rival. The site is LEGAL for the rival from its own
                // settlement, so the player's order fails on control and nothing else.
                WorldState r = FoundingRigs.Pipeline(Cfg, null).Step(FoundingRigs.Dev(42UL, aiEmpires: 1));
                SettlementId rivals = r.Settlements[1].Id;
                PolityId rival = ControlOf(r, rivals.Value).Polity;
                Assert.NotEqual(Player, rival);
                int site = FoundingRigs.NearestLegalSite(r, Cfg, FoundingRigs.Frontier(r), rival, rivals);
                return new(r, Cfg, wg, FoundingQuery.FoundOrder(r, Player, rivals, site), FoundingVerdict.NotControlled);
            }
            case "NotControlled-stateless":
            {
                // The source's control row removed (a settlement that revolted away and was not re-founded).
                var kept = new List<ControlRow>();
                for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != src) kept.Add(w.Controls[i]);
                w.Controls.Clear();
                foreach (ControlRow c in kept) w.Controls.Add(c);
                return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, legal), FoundingVerdict.NotControlled);
            }
            case "OffMap":
                return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, f.Size * f.Size), FoundingVerdict.OffMap);
            case "Water": return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, Cell(FoundingVerdict.Water)), FoundingVerdict.Water);
            case "PoorSite": return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, Cell(FoundingVerdict.PoorSite)), FoundingVerdict.PoorSite);
            case "Occupied": return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, Cell(FoundingVerdict.Occupied)), FoundingVerdict.Occupied);
            case "TooClose": return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, Cell(FoundingVerdict.TooClose)), FoundingVerdict.TooClose);
            case "Claimed":
            {
                // With the shipped spacing (95.2 km) every catchment (50 km budget) lies inside a spacing
                // exclusion, so "claimed" is only distinguishable from "too close" when spacing is smaller than
                // the catchment. Rig the WORLDGEN siting spacing (system and predicate alike) to 10 km.
                WorldgenConfig small = wg with { Siting = wg.Siting with { MinSpacingKm = 10.0 } };
                SettlementSiting.FrontierSiting fs = FoundingRigs.Frontier(w, Cfg, small);
                int cell = FoundingRigs.FirstCell(w, Cfg, fs, Player, src, FoundingVerdict.Claimed, 0, 80);
                return new(w, Cfg, small, FoundingQuery.FoundOrder(w, Player, src, cell), FoundingVerdict.Claimed);
            }
            case "Unreachable":
                return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, FoundingRigs.UnreachableCandidate(w, f, src)), FoundingVerdict.Unreachable);
            case "NoProvisions":
            {
                // An empty granary: the source's grain sunk through the Ledger in PREV (both twins).
                var grain = new GoodId(Cfg.Goods!.GrainId);
                FoundingRigs.SinkGood(w, src, grain, FoundingRigs.Grain(w, src));
                return new(w, Cfg, wg, FoundingQuery.FoundOrder(w, Player, src, legal), FoundingVerdict.NoProvisions);
            }
            case "NoParty":
            {
                // A fraction that floors to nobody (party 0). The keep-one cap's own zero is pinned in the allocator tests.
                SimConfig tiny = Cfg with { Colonization = new ColonizationConfig(1e-6) };
                return new(w, tiny, wg, FoundingQuery.FoundOrder(w, Player, src, legal), FoundingVerdict.NoParty);
            }
            default: throw new ArgumentException(arm);
        }
    }

    [Theory]
    [MemberData(nameof(Arms))]
    public void EveryRejectionArm_IsThatArm_AndTheStepIsHashEqualToTheOrderlessStep(string arm)
    {
        ArmCase c = Build(arm);
        int cell = FoundingQuery.SiteCellOf(c.Order);
        Assert.True(cell >= 0, $"{arm}: no witness cell found — the rig is void");
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(c.Prev, c.Cfg, c.Worldgen);
        Assert.Equal(c.Want, FoundingQuery.Check(c.Prev, c.Cfg, f, c.Order.Actor, new SettlementId(c.Order.TargetId), cell));

        WorldState without = FoundingRigs.Pipeline(c.Cfg, null, c.Worldgen).Step(c.Prev.Clone());
        WorldState with = FoundingRigs.Pipeline(c.Cfg, Log(c.Order), c.Worldgen).Step(c.Prev.Clone());
        Assert.Equal(without.Settlements.Count, with.Settlements.Count);
        Assert.Equal(WorldHash.ComputeHex(without), WorldHash.ComputeHex(with));
    }

    [Fact]
    public void TheClaimedRig_FoundsOnceTheSiteLiesOutsideEveryCatchment()
    {
        // Control for the Claimed arm: under the same 10 km spacing rig a site OUTSIDE every catchment founds, so
        // the Claimed rejection above is the catchment rule and not the rig breaking founding altogether.
        WorldgenConfig small = TestConfigs.Worldgen() with { Siting = TestConfigs.Worldgen().Siting with { MinSpacingKm = 10.0 } };
        WorldState w = W1();
        SettlementId src = w.Settlements[0].Id;
        int site = FoundingRigs.NearestLegalSite(w, Cfg, FoundingRigs.Frontier(w, Cfg, small), Player, src);
        WorldState next = FoundingRigs.Pipeline(Cfg, Log(FoundingQuery.FoundOrder(w, Player, src, site)), small).Step(w);
        Assert.Equal(w.Settlements.Count + 1, next.Settlements.Count);
    }

    [Fact]
    public void DepartureIsLive_AnOrderLegalOnPrev_FoundsNothingWhenTheStepEmptiesTheGranaryFirst()
    {
        // The one place PREV-legal and founded can differ, pinned: the source's granary is cut (through the Ledger)
        // to a few grain — enough for a per-capita share of 1 on PREV — and this step's meals (Consumption runs
        // before Colonization) eat it. The party may not leave without provisions; nothing is founded.
        WorldState w = W1();
        SettlementId src = w.Settlements[0].Id;
        var grain = new GoodId(Cfg.Goods!.GrainId);
        int site = FoundingRigs.NearestLegalSite(w, Cfg, FoundingRigs.Frontier(w), Player, src);
        long people = FoundingRigs.People(w, src);
        long party = FoundingQuery.AllocateParty(w.Buckets, src, FoundingQuery.PartyFraction(Cfg), null);
        long keep = (people + party - 1) / party;                     // the smallest store whose share is >= 1
        FoundingRigs.SinkGood(w, src, grain, FoundingRigs.Grain(w, src) - keep);
        Assert.Equal(FoundingVerdict.Legal, FoundingQuery.Check(w, Cfg, FoundingRigs.Frontier(w), Player, src, site));

        TurnExecutor Exec(OrderLog? log) => new(GovernanceRigs.FlatEra(),
            [SystemCatalog.Consumption(Cfg), SystemCatalog.Colonization(Cfg, TestConfigs.Worldgen())], log);
        WorldState without = Exec(null).Step(w.Clone());
        Assert.Equal(0, FoundingRigs.Grain(without, src));            // non-vacuity: the meals took it all
        WorldState with = Exec(Log(FoundingQuery.FoundOrder(w, Player, src, site))).Step(w.Clone());
        Assert.Equal(w.Settlements.Count, with.Settlements.Count);
        Assert.Equal(WorldHash.ComputeHex(without), WorldHash.ComputeHex(with));
    }

    // ================================================================== ONE PREDICATE: offered <=> founded

    [Fact]
    public void AnOrderThePredicateCallsLegalIsExactlyAnOrderTheStepFounds()
    {
        // The listing predicate a UI or AI asks (FoundingQuery.Check on the world it sees) against what the
        // production step does with the same order: founded <=> Legal, over every source's nearest legal site and
        // a witness of every rejection arm the rig world holds, plus a sweep of one ring.
        WorldState w = W1();
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        var cases = new List<(SettlementId Source, int Cell)>();
        for (int s = 0; s < w.Settlements.Count; s++)
            cases.Add((w.Settlements[s].Id, FoundingRigs.NearestLegalSite(w, Cfg, f, Player, w.Settlements[s].Id)));
        SettlementId src = w.Settlements[0].Id;
        foreach (FoundingVerdict v in new[] { FoundingVerdict.Water, FoundingVerdict.PoorSite, FoundingVerdict.Occupied, FoundingVerdict.TooClose })
            cases.Add((src, FoundingRigs.FirstCell(w, Cfg, f, Player, src, v, 0, 80)));
        cases.Add((src, FoundingRigs.UnreachableCandidate(w, f, src)));
        cases.Add((src, f.Size * f.Size));
        int k = 0;
        foreach (int cell in FoundingRigs.Ring(f.Size, FoundingRigs.SiteCellOf(w, src), 40))
            if (k++ % 23 == 0) cases.Add((src, cell));

        WorldState without = FoundingRigs.Pipeline(Cfg, null).Step(w.Clone());
        string plain = WorldHash.ComputeHex(without);
        int legal = 0, refused = 0;
        var verdicts = new HashSet<FoundingVerdict>();
        foreach ((SettlementId source, int cell) in cases)
        {
            Assert.True(cell >= 0, "a witness cell is missing — the rig is void");
            FoundingVerdict v = FoundingQuery.Check(w, Cfg, f, Player, source, cell);
            verdicts.Add(v);
            WorldState with = FoundingRigs.Pipeline(Cfg, Log(FoundingQuery.FoundOrder(w, Player, source, cell))).Step(w.Clone());
            bool founded = with.Settlements.Count == without.Settlements.Count + 1;
            Assert.True(founded == (v == FoundingVerdict.Legal), $"source {source.Value} cell {cell}: verdict {v} but founded={founded}");
            if (founded) { legal++; Assert.Equal(cell, with.Settlements[^1].SiteCell); }
            else { refused++; Assert.Equal(plain, WorldHash.ComputeHex(with)); }
        }
        Assert.True(legal >= 4 && refused >= 6 && verdicts.Count >= 6, $"legal {legal}, refused {refused}, verdicts {verdicts.Count}");
    }

    // ================================================================== SAME-TURN MULTIPLICITY

    /// <summary>A second legal site for <paramref name="src"/> that stays legal once <paramref name="first"/> is
    /// founded (spacing grown exactly as the system grows it).</summary>
    private static int SecondSite(WorldState w, SettlementSiting.FrontierSiting f, SettlementId src, int first)
    {
        var occupied = new int[w.Settlements.Count + 1];
        for (int i = 0; i < w.Settlements.Count; i++) occupied[i] = w.Settlements[i].SiteCell;
        occupied[^1] = first;
        double[] spacing = SettlementSiting.SeedSpacing(f, occupied);
        for (int r = 1; r < f.Size; r++)
            foreach (int cell in FoundingRigs.Ring(f.Size, FoundingRigs.SiteCellOf(w, src), r))
                if (FoundingQuery.Check(w, Cfg, f, spacing, occupied, Player, src, cell) == FoundingVerdict.Legal) return cell;
        return -1;
    }

    [Fact]
    public void TheFirstLegalOrderPerSourcePerTurnFounds_InLogOrder_NotTheBestSite()
    {
        WorldState w = FoundingRigs.Dev(42UL);
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        SettlementId src = w.Settlements[0].Id;
        int a = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, src);
        int b = SecondSite(w, f, src, a);
        Assert.True(a >= 0 && b >= 0 && a != b);

        WorldState ab = FoundingRigs.ColonizationOnly(Cfg, Log(FoundingQuery.FoundOrder(w, Player, src, a), FoundingQuery.FoundOrder(w, Player, src, b))).Step(w.Clone());
        WorldState ba = FoundingRigs.ColonizationOnly(Cfg, Log(FoundingQuery.FoundOrder(w, Player, src, b), FoundingQuery.FoundOrder(w, Player, src, a))).Step(w.Clone());
        Assert.Equal(w.Settlements.Count + 1, ab.Settlements.Count);
        Assert.Equal(a, ab.Settlements[^1].SiteCell);
        Assert.Equal(w.Settlements.Count + 1, ba.Settlements.Count);
        Assert.Equal(b, ba.Settlements[^1].SiteCell);
    }

    [Fact]
    public void AnIllegalFirstOrder_DoesNotSpendTheSourcesTurn()
    {
        WorldState w = FoundingRigs.Dev(42UL);
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        SettlementId src = w.Settlements[0].Id;
        int water = FoundingRigs.FirstCell(w, Cfg, f, Player, src, FoundingVerdict.Water, 0, 80);
        int a = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, src);
        WorldState next = FoundingRigs.ColonizationOnly(Cfg, Log(FoundingQuery.FoundOrder(w, Player, src, water), FoundingQuery.FoundOrder(w, Player, src, a))).Step(w.Clone());
        Assert.Equal(w.Settlements.Count + 1, next.Settlements.Count);
        Assert.Equal(a, next.Settlements[^1].SiteCell);
    }

    [Fact]
    public void TwoSourcesOrderingOneSite_TheFirstFounds_TheSecondIsOccupiedAndMovesNobody()
    {
        WorldState w = FoundingRigs.Dev(42UL);
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        SettlementId first = w.Settlements[0].Id;
        int site = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, first);
        SettlementId second = default;
        for (int s = 1; s < w.Settlements.Count; s++)
            if (FoundingQuery.Check(w, Cfg, f, Player, w.Settlements[s].Id, site) == FoundingVerdict.Legal) { second = w.Settlements[s].Id; break; }
        Assert.NotEqual(first, second);   // a second source for which the same site is legal on its own

        WorldState next = FoundingRigs.ColonizationOnly(Cfg, Log(FoundingQuery.FoundOrder(w, Player, first, site), FoundingQuery.FoundOrder(w, Player, second, site))).Step(w.Clone());
        Assert.Equal(w.Settlements.Count + 1, next.Settlements.Count);
        Assert.True(FoundingRigs.People(next, first) < FoundingRigs.People(w, first));
        Assert.Equal(FoundingRigs.People(w, second), FoundingRigs.People(next, second));
        Assert.Equal(FoundingRigs.Grain(w, second), FoundingRigs.Grain(next, second));
    }

    [Fact]
    public void TwoSourcesOfOneIssuer_EachFoundTheirOwnSite_InOneTurn_IdsInLogOrder()
    {
        // The rule is one founding per SOURCE per turn (ADR-036 §4), not per issuer: a civilization with several
        // cities may found from each of them in the same step. Two distant settlements of the same issuer each order
        // a legal site that stays legal once the other order has founded, so nothing but the per-turn key could
        // refuse the second order. Both found, ids max+1 and max+2 follow the LOG order, each at its own cell, each
        // peopled from its own source. Keyed on the issuer, the second order would be skipped.
        WorldState w = FoundingRigs.Dev(42UL);
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        SettlementId first = w.Settlements[0].Id;
        int a = FoundingRigs.NearestLegalSite(w, Cfg, f, Player, first);
        SettlementId second = default;
        int b = -1;
        for (int s = 1; s < w.Settlements.Count && b < 0; s++)
        {
            second = w.Settlements[s].Id;
            b = SecondSite(w, f, second, a);                          // legal for `second` with `a` already founded
        }
        Assert.True(a >= 0 && b >= 0 && a != b && first != second, $"no non-conflicting pair: a {a}, b {b} — the rig is void");
        Assert.Equal(Player, ControlOf(w, first.Value).Polity);       // the same issuer controls both sources
        Assert.Equal(Player, ControlOf(w, second.Value).Polity);
        // Reversed log order needs `a` legal once `b` stands: checked, not assumed.
        int[] withB = new int[w.Settlements.Count + 1];
        for (int i = 0; i < w.Settlements.Count; i++) withB[i] = w.Settlements[i].SiteCell;
        withB[^1] = b;
        Assert.Equal(FoundingVerdict.Legal,
            FoundingQuery.Check(w, Cfg, f, SettlementSiting.SeedSpacing(f, withB), withB, Player, first, a));

        int max = FoundingRigs.MaxSettlementId(w);
        double fraction = FoundingQuery.PartyFraction(Cfg);
        long partyA = FoundingQuery.AllocateParty(w.Buckets, first, fraction, null);
        long partyB = FoundingQuery.AllocateParty(w.Buckets, second, fraction, null);
        Assert.True(partyA >= 1 && partyB >= 1);
        OrderRecord fromFirst = FoundingQuery.FoundOrder(w, Player, first, a);
        OrderRecord fromSecond = FoundingQuery.FoundOrder(w, Player, second, b);

        foreach ((OrderRecord one, OrderRecord two, SettlementId s1, int c1, long p1, SettlementId s2, int c2, long p2) in new[]
                 {
                     (fromFirst, fromSecond, first, a, partyA, second, b, partyB),
                     (fromSecond, fromFirst, second, b, partyB, first, a, partyA),
                 })
        {
            WorldState next = FoundingRigs.ColonizationOnly(Cfg, Log(one, two)).Step(w.Clone());
            Assert.Equal(w.Settlements.Count + 2, next.Settlements.Count);
            Assert.True(FoundingRigs.TryGetSettlement(next, max + 1, out SettlementRow r1), "the first order founded nothing");
            Assert.True(FoundingRigs.TryGetSettlement(next, max + 2, out SettlementRow r2), "the second order founded nothing");
            Assert.Equal(c1, r1.SiteCell);
            Assert.Equal(c2, r2.SiteCell);
            Assert.Equal(p1, FoundingRigs.People(next, r1.Id));
            Assert.Equal(p2, FoundingRigs.People(next, r2.Id));
            Assert.Equal(FoundingRigs.People(w, s1) - p1, FoundingRigs.People(next, s1));
            Assert.Equal(FoundingRigs.People(w, s2) - p2, FoundingRigs.People(next, s2));
            Assert.Equal(FoundingRigs.People(w), FoundingRigs.People(next));
        }
    }

    [Fact]
    public void OrdersResolveBeforeTheEmergentFounding_AnOrderForTheEmergentPickTakesIt()
    {
        // B1 demand on settlement 0 AND an order for the very cell B1 would pick. Orders first (ADR-036 §4): the
        // order founds there with id max+1 and B1 founds elsewhere with id max+2, spaced from the order's colony.
        // Emergent first would hand the cell to B1 and refuse the order — the player's explicit choice would lose
        // to the frontier drift of the same step.
        WorldState w = FoundingRigs.Dev(42UL);
        SettlementSiting.FrontierSiting f = FoundingRigs.Frontier(w);
        SettlementId emergent = w.Settlements[0].Id;
        for (int i = 0; i < w.Buckets.Count; i++)
            if (w.Buckets[i].Settlement == emergent) w.Buckets.Ref(i).UnplacedDeparture = 3.0;

        var occupied = new int[w.Settlements.Count];
        for (int i = 0; i < occupied.Length; i++) occupied[i] = w.Settlements[i].SiteCell;
        int pick = SettlementSiting.ChooseFrontierSite(f, w.Terrain!, SettlementSiting.SeedSpacing(f, occupied), occupied,
            TestConfigs.Worldgen().Siting.ScoreJitter, w.Seed);
        SettlementId orderer = default;
        bool any = false;
        for (int s = 0; s < w.Settlements.Count && !any; s++)
            if (FoundingQuery.Check(w, Cfg, f, Player, w.Settlements[s].Id, pick) == FoundingVerdict.Legal) { orderer = w.Settlements[s].Id; any = true; }
        Assert.True(any, $"B1's pick {pick} is not a legal order site from any settlement — the rig is void");
        int max = FoundingRigs.MaxSettlementId(w);

        WorldState alone = FoundingRigs.ColonizationOnly(Cfg, null).Step(w.Clone());
        Assert.True(FoundingRigs.TryGetSettlement(alone, max + 1, out SettlementRow b1Alone));
        Assert.Equal(pick, b1Alone.SiteCell);                       // non-vacuity: B1 really takes that cell

        WorldState both = FoundingRigs.ColonizationOnly(Cfg, Log(FoundingQuery.FoundOrder(w, Player, orderer, pick))).Step(w.Clone());
        Assert.True(FoundingRigs.TryGetSettlement(both, max + 1, out SettlementRow ordered));
        Assert.Equal(pick, ordered.SiteCell);
        Assert.True(FoundingRigs.People(both, orderer) < FoundingRigs.People(w, orderer) || orderer == emergent);
        Assert.True(FoundingRigs.TryGetSettlement(both, max + 2, out SettlementRow b1));
        Assert.NotEqual(pick, b1.SiteCell);
        var spacingFromOrdered = SettlementSiting.SeedSpacing(f, [pick]);
        Assert.False(spacingFromOrdered[Sim.Core.Pathing.LatticeMap.OriginLatticeNode(f.Lattice, f.Size, b1.SiteCell)] < f.MinSpacingCostUnits,
            "B1 crowded the ordered colony");
    }

    // ================================================================== THE PARTY ALLOCATOR

    private static Table<BucketRow> Buckets(params (int Settlement, long Count)[] rows)
    {
        var flows = new Table<LedgerFlowRow>();
        var ledger = new Ledger(flows);
        var t = new Table<BucketRow>();
        for (int i = 0; i < rows.Length; i++)
        {
            int r = t.Add(new BucketRow(new SettlementId(rows[i].Settlement), new CultureId(1), new ReligionId(1),
                new ClassId(1), cohortIdx: i, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (rows[i].Count > 0)
                ledger.Flow(ref t.Ref(r).Count, ConservedQuantityIds.Population, ReasonIds.InitialEndowment,
                    rows[i].Count, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return t;
    }

    private static long[] Allocate(Table<BucketRow> t, int source, double fraction, out long total)
    {
        var per = new long[t.Count];
        total = FoundingQuery.AllocateParty(t, new SettlementId(source), fraction, per);
        return per;
    }

    [Fact]
    public void Allocator_TieDense_EqualBucketsGiveTheRemainderToTheLowestRows()
    {
        // Ten EQUAL buckets of 5 (another settlement's rows interleaved): N = 50, T = floor(0.23 x 50) = 11, each
        // floor(11 x 5 / 50) = 1, so one person is owed — it comes from the LOWEST source row, never from a tie
        // broken by anything else. Interleaved foreign rows give nothing.
        var rows = new List<(int, long)>();
        for (int i = 0; i < 10; i++) { rows.Add((7, 5)); rows.Add((3, 9)); }
        Table<BucketRow> t = Buckets(rows.ToArray());
        long[] per = Allocate(t, 7, 0.23, out long total);
        Assert.Equal(11, total);
        var expected = new long[20];
        for (int i = 0; i < 20; i += 2) expected[i] = 1;
        expected[0] = 2;
        Assert.Equal(expected, per);

        // Remainder of three over equal buckets: rows 0, 2, 4 (the three lowest source rows) take one more each.
        long[] per3 = Allocate(t, 7, 0.26, out long total3);              // T = 13 = 10 floors + 3 owed
        Assert.Equal(13, total3);
        for (int i = 0; i < 20; i += 2) Assert.Equal(i <= 4 ? 2 : 1, per3[i]);

        // Twin calls are identical (no hidden state).
        Assert.Equal(per3, Allocate(t, 7, 0.26, out _));
    }

    [Fact]
    public void Allocator_UnequalBuckets_GiveTheirProportionalFloor_ThenTheRemainderByAscendingRow()
    {
        // Unequal buckets, exact vectors. Equal or tiny buckets cannot tell the proportional floor from a pure
        // row-order round robin (both give the same vector there); these can. The round robin's vectors are in the
        // comments so the distinction is visible.
        // [10, 30, 60] at 0.2 (one foreign row interleaved): N = 100, T = 20; floors 20x10/100 = 2, 6, 12 sum to T,
        // so nothing is owed. Round robin would give [7, 0, 7, 6] (measured against that mutant).
        long[] exact = Allocate(Buckets((1, 10), (2, 50), (1, 30), (1, 60)), 1, 0.2, out long t0);
        Assert.Equal(20, t0);
        Assert.Equal(new long[] { 2, 0, 6, 12 }, exact);

        // [7, 13, 29, 51] at 0.25: N = 100, T = 25; floors 1, 3, 7, 12 (fractions .75, .25, .25, .75) sum to 23, so
        // two are owed and they go to rows 0 and 1 — the lowest rows, not the largest fractions (that rule would
        // give [2, 3, 7, 13]). Round robin would give [6, 7, 6, 6].
        Assert.Equal(new long[] { 2, 4, 7, 12 }, Allocate(Buckets((1, 7), (1, 13), (1, 29), (1, 51)), 1, 0.25, out long t1));
        Assert.Equal(25, t1);

        // The same counts in reverse row order: floors 12, 7, 3, 1; the two owed go to rows 0 and 1 again — the
        // remainder follows the row, not the size. Round robin would give [7, 6, 6, 6].
        Assert.Equal(new long[] { 13, 8, 3, 1 }, Allocate(Buckets((1, 51), (1, 29), (1, 13), (1, 7)), 1, 0.25, out long t2));
        Assert.Equal(25, t2);
    }

    [Fact]
    public void Allocator_KeepsOnePersonInEveryNonEmptyBucket_AndCyclesTheRemainderPastFullRows()
    {
        // Single-person buckets can give nobody: even a 99% fraction forms no party.
        Assert.Equal(0, FoundingQuery.AllocateParty(Buckets((1, 1), (1, 1), (1, 1)), new SettlementId(1), 0.99, null));
        // [1, 2, 1, 2] at 0.9: T = min(floor(5.4), capacity 2) = 2; floors are 0, the two spare people come from the
        // two-person rows (the one-person rows are at their floor of one).
        Assert.Equal(new long[] { 0, 1, 0, 1 }, Allocate(Buckets((1, 1), (1, 2), (1, 1), (1, 2)), 1, 0.9, out long t1));
        Assert.Equal(2, t1);
        // [10, 1, 1, 1] at 0.5: T = 6, floors 4, 0, 0, 0; the two owed people cycle past the full rows back to row 0.
        Assert.Equal(new long[] { 6, 0, 0, 0 }, Allocate(Buckets((1, 10), (1, 1), (1, 1), (1, 1)), 1, 0.5, out long t2));
        Assert.Equal(6, t2);
        // Empty buckets are skipped and keep nothing; an empty or absent source forms no party.
        Assert.Equal(new long[] { 0, 2, 0 }, Allocate(Buckets((1, 0), (1, 10), (2, 10)), 1, 0.25, out long t3));
        Assert.Equal(2, t3);
        Assert.Equal(0, FoundingQuery.AllocateParty(Buckets((2, 10)), new SettlementId(1), 0.5, null));
        Assert.Equal(0, FoundingQuery.AllocateParty(Buckets((1, 10)), new SettlementId(1), 0.0, null));
    }

    [Fact]
    public void Allocator_TotalIsExactAndWithinEveryBucket_OverADenseSweep()
    {
        // Exhaustive small sweep: every allocation sums to T = min(floor(f N), sum(c - 1)), never takes a bucket's
        // last person, and the null-array total equals the allocated total.
        long[][] shapes = [[3, 3, 3], [1, 7, 2, 5], [12, 1, 12, 1, 12], [2, 2, 2, 2, 2, 2, 2], [40, 3], [5]];
        foreach (long[] shape in shapes)
            for (int pct = 1; pct < 100; pct += 7)
            {
                double fraction = pct / 100.0;
                var rows = new (int, long)[shape.Length];
                for (int i = 0; i < shape.Length; i++) rows[i] = (1, shape[i]);
                Table<BucketRow> t = Buckets(rows);
                long[] per = Allocate(t, 1, fraction, out long total);
                long n = 0, cap = 0, sum = 0;
                for (int i = 0; i < shape.Length; i++) { n += shape[i]; cap += Math.Max(0, shape[i] - 1); sum += per[i]; }
                Assert.Equal(Math.Min((long)Math.Floor(fraction * n), cap), total);
                Assert.Equal(total, sum);
                Assert.Equal(total, FoundingQuery.AllocateParty(t, new SettlementId(1), fraction, null));
                for (int i = 0; i < shape.Length; i++) Assert.True(per[i] >= 0 && per[i] <= Math.Max(0, shape[i] - 1));
            }
    }

    // ================================================================== CONFIG

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public void ThePartyFraction_MustLieStrictlyBetweenZeroAndOne(double bad)
    {
        string json = TestConfigs.SimJson().Replace("\"orderPartyFraction\": 0.2",
            "\"orderPartyFraction\": " + (double.IsNaN(bad) ? "\"NaN\"" : bad.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Throws<SimConfigException>(() => SimConfigLoader.Load(json));
    }

    [Fact]
    public void TheCanonicalPartyFraction_IsTheDocumentedContentValue()
    {
        Assert.Equal(0.2, Cfg.Colonization!.OrderPartyFraction);
        Assert.Equal(0.0, FoundingQuery.PartyFraction(Cfg with { Colonization = null }));
    }
}
