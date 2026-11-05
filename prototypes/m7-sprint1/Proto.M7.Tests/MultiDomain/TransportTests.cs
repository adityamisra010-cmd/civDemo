using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tests.MultiDomain.DT;

namespace Proto.M7.Tests.MultiDomain;

/// <summary>Transports: embark / disembark with capacity and time cost, amphibious landing, loss of troops with a sinking
/// hull through ledger flows (never vanishing), and termination (a reversible move is not activity).</summary>
public class TransportTests
{
    private const long Cell = 50_000;
    // Coast: water for x <= 19 (cells of 50 m). Transport hull centred on water cell (19, 20), 50 m from land cell (20, 20).
    private static Vec2L C(long cx, long cy) => MultiDomainScenarios.Cell(cx, cy, Cell);
    private static readonly VesselClass T30 = DomainProfiles.SteamTransport with { Capacity = 30 };

    private static BattleSetup Shore(int troops, ulong seed = 1, VesselClass? t = null, bool aboard = false, DomainRules? rules = null)
    {
        var forms = new List<FormationSpec>
        {
            Ship(1, 0, t ?? T30, C(19, 20)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, troops, C(20, 20)) { EmbarkedOn = aboard ? 1 : null },
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)),       // a distant, unseen enemy
        };
        return OnGrid(Coast(Cell, 20), seed, rules, null, forms.ToArray());
    }

    private static int CountState(TacticalBattle b, long formation, EntityState s) => b.Entities().Count(e => e.FormationId == formation && e.State == s);

    [Fact]
    public void EmbarkRefusedWithoutCapacityAndByTheWrongDomain()
    {
        var b = new TacticalBattle(Shore(40), Script((0, new OrderRequest(2, OrderKind.Embark, 0, 1)), (0, new OrderRequest(1, OrderKind.Embark, 1, 1))), PassiveController.Instance);
        b.RunUntilPulse(1);
        Assert.Equal(OrderInvalid.NoCapacity, b.Orders[0].Invalid);
        Assert.Equal(OrderInvalid.NotCapable, b.Orders[1].Invalid);               // a vessel cannot embark
        var b2 = new TacticalBattle(Shore(20), Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(25, 20)))), PassiveController.Instance);
        b2.RunUntilPulse(1);
        Assert.Equal(OrderInvalid.NotEmbarked, b2.Orders[0].Invalid);
        var b3 = new TacticalBattle(Shore(20, aboard: true), Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(5, 20)))), PassiveController.Instance);
        b3.RunUntilPulse(1);
        Assert.Equal(OrderInvalid.BadTarget, b3.Orders[0].Invalid);                // landing point at sea
    }

    /// <summary>
    /// ORDER-DELIVERY PIN (turn-exact, with ±1 neighbours): an Embark order delivered at step offset k takes effect at
    /// pulseStart + k * StepMs — nobody is aboard one step earlier; at that instant exactly floor(StepMs / EmbarkMsPerPerson)
    /// people have boarded (the time cost), and boarding continues at that rate.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EmbarkTakesEffectAtTheDeliveredStepAndCostsTime(int k)
    {
        var setup = Shore(20);
        long step = setup.Rules.StepMs, perStep = step / T30.EmbarkMsPerPerson;
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(2, OrderKind.Embark, k, 1))), PassiveController.Instance);
        long tk = setup.StartMs + k * step;
        var aboardAt = new SortedDictionary<long, int>();
        RunUntil(b, () => { aboardAt[b.Scheduler.NowMs] = CountState(b, 2, EntityState.Embarked); return b.Scheduler.NowMs >= tk + 10 * step; });
        Assert.Equal(0, aboardAt[tk - step]);
        Assert.Equal(perStep, aboardAt[tk]);
        Assert.Equal(2 * perStep, aboardAt[tk + step]);
        Assert.Equal(20, aboardAt[tk + 9 * step]);
        Assert.Null(b.PersonnelConsistency());
        Assert.Equal(20, b.Personnel.Balance(2, PersonnelPlace.Aboard));
        Assert.Equal(0, b.Personnel.Balance(2, PersonnelPlace.Field));
    }

    [Fact]
    public void AmphibiousLandingPutsTroopsOnlyOnPassableLandAtTheLandingRate()
    {
        var setup = Shore(20, aboard: true);
        long step = setup.Rules.StepMs, perStep = step / T30.DisembarkMsPerPerson;
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(25, 20)))), PassiveController.Instance);
        int prev = 0; long steps = 0;
        RunUntil(b, () =>
        {
            int onField = CountState(b, 2, EntityState.Active);
            Assert.True(onField - prev <= perStep, $"landed {onField - prev} in one step > {perStep}");
            prev = onField;
            steps++;
            foreach (var e in b.Entities())
                if (e.FormationId == 2 && e.State == EntityState.Active)
                {
                    var c = setup.Grid.CellOf(e.Pos);
                    Assert.True(setup.Grid.IsPassable(setup.Costs, c.X, c.Y), $"entity {e.Id} landed on an impassable cell");
                }
            return onField == 20;
        }, 5);
        Assert.Equal(0, CountState(b, 2, EntityState.Embarked));
        Assert.Equal(20, b.Personnel.Balance(2, PersonnelPlace.Field));
        Assert.Equal(20, b.Personnel.Flows.Where(f => f.Reason == PersonnelFlowReason.Disembark).Sum(f => f.Amount));
        Assert.Equal(-1, b.CarrierOf(2000));
    }

    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(1L, 2L)]
    public void SunkTransportsTroopsAreLostThroughLedgerFlowsNeverVanish(long num, long den)
    {
        var rules = new DomainRules { ShipwreckSurvivalNum = num, ShipwreckSurvivalDen = den };
        var frail = T30 with { Hull = 1 };
        var s = Sea(100_000, 12, rules,
            Ship(1, 0, frail, M(3000, 2000)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, M(3000, 2000)) { EmbarkedOn = 1 },
            Ship(5, 1, DomainProfiles.TurretWarship, M(5000, 2000)));
        var b = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        var r = RunChecked(b);
        Assert.Equal(EntityState.Dead, D(b, 1000).State);
        var f2 = r.Formations.Single(f => f.FormationId == 2);
        Assert.Equal(20, f2.Start);
        Assert.Equal(f2.Start, f2.Survivors + f2.Killed + f2.Surrendered);
        Assert.Equal(0, f2.EmbarkedAtEnd);
        Assert.DoesNotContain(b.Entities(), e => e.FormationId == 2 && e.State == EntityState.Embarked);
        var p = r.Personnel!;
        long lost = p.Count(2, PersonnelPlace.LostWithVessel), kia = p.Count(2, PersonnelPlace.Killed), saved = p.Count(2, PersonnelPlace.Escaped);
        Assert.Equal(20, lost + kia + saved);
        Assert.Equal(Proto.M7.Fixed.FixedMath.MulDivFloor(20 - kia, num, den), saved);
        // Every lost soldier moved by a LostWithVessel transfer out of the transport's Aboard account (one flow per person).
        long flowed = 0;
        foreach (var f in b.Personnel.Flows)
        {
            if (f.Reason != PersonnelFlowReason.LostWithVessel) continue;
            var from = b.Personnel.AccountInfo(f.From);
            if (from.FormationId == 2) { Assert.Equal(PersonnelPlace.Aboard, from.Place); Assert.Equal(1000, from.CarrierId); flowed += f.Amount; }
        }
        Assert.Equal(lost, flowed);
        Assert.True(lost > 0);
        Assert.Equal(p.OpeningTotal, p.Total);
    }

    [Fact]
    public void EmbarkDisembarkLoopIsNotActivitySoTheBattleStillEnds()
    {
        // On EACH side two formations share a transport and swap places every pulse (one lands while the other boards), so
        // at every pulse end one formation is ashore having changed state. Nobody ever sees anybody. Embarking / landing is
        // a reversible move, not primary activity: both sides must be found inactive after five pulses (BothDisengaged).
        // If it counted as activity, neither side would ever disengage and the battle would never end (harness guard).
        var t = T30 with { SightMm = 500_000 };          // TEST INPUT: the transports must not see the distant enemy
        var forms = new[]
        {
            Ship(1, 0, t, C(19, 30)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 5, C(20, 30)),
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 5, C(20, 30)) { EmbarkedOn = 1 },
            Ship(4, 1, t, C(19, 6)),
            new FormationSpec(5, 1, DomainProfiles.Riflemen, 5, C(20, 6)),
            new FormationSpec(6, 1, DomainProfiles.Riflemen, 5, C(20, 6)) { EmbarkedOn = 4 },
        };
        // [TR] BothDisengaged is a LEGACY end: this test pins lane N's activity definition under the legacy inactivity model.
        // The DIR5 §2.6 counterpart is EmbarkDisembarkLoopIsNotProgressUnderDir5 below.
        var setup = OnGrid(Coast(Cell, 20), 1, null, new TacticalRules { Inactivity = InactivityModel.LegacySideDisengage }, forms);
        ScriptedController Swap(long a, long b, long ship, long y)
        {
            var c = new ScriptedController();
            for (int p = 0; p < 40; p++)
            {
                long boarding = p % 2 == 0 ? a : b, landing = p % 2 == 0 ? b : a;
                c.Add(p, new OrderRequest(boarding, OrderKind.Embark, 0, ship));
                c.Add(p, new OrderRequest(landing, OrderKind.Disembark, 0, -1, C(21, y)));
            }
            return c;
        }
        var b = new TacticalBattle(setup, Swap(2, 3, 1, 30), Swap(5, 6, 4, 6));
        var r = RunChecked(b, 40);
        Assert.Equal(BattleEndReason.BothDisengaged, r.Reason);
        Assert.True(r.Pulses <= 7);
        // the swapping really happened: people moved both ways on both sides
        Assert.Contains(b.Personnel.Flows, f => f.Reason == PersonnelFlowReason.Embark && b.Personnel.AccountInfo(f.From).FormationId == 2);
        Assert.Contains(b.Personnel.Flows, f => f.Reason == PersonnelFlowReason.Disembark && b.Personnel.AccountInfo(f.To).FormationId == 3);
        Assert.Contains(b.Personnel.Flows, f => f.Reason == PersonnelFlowReason.Embark && b.Personnel.AccountInfo(f.From).FormationId == 5);
    }

    /// <summary>
    /// [TR] DIR5 §2.6 counterpart of <see cref="EmbarkDisembarkLoopIsNotActivitySoTheBattleStillEnds"/>: under the default
    /// hidden-stationary rule the swap loop is not progress either (embarking / landing is a reversible move, not a state
    /// change of the formation): the whole-field idle counter reaches five and the battle ends as a Standoff (measured: 6
    /// pulses). No formation is ever judged — each formation spends a pulse end aboard (no Active member) at least every
    /// other pulse, which resets its own counter — so nobody routs. If the loop counted as progress the battle would never end
    /// (the 40-pulse harness guard would fail).
    /// </summary>
    [Fact]
    public void EmbarkDisembarkLoopIsNotProgressUnderDir5()
    {
        var t = T30 with { SightMm = 500_000 };          // TEST INPUT: the transports must not see the distant enemy
        var forms = new[]
        {
            Ship(1, 0, t, C(19, 30)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 5, C(20, 30)),
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 5, C(20, 30)) { EmbarkedOn = 1 },
            Ship(4, 1, t, C(19, 6)),
            new FormationSpec(5, 1, DomainProfiles.Riflemen, 5, C(20, 6)),
            new FormationSpec(6, 1, DomainProfiles.Riflemen, 5, C(20, 6)) { EmbarkedOn = 4 },
        };
        var setup = OnGrid(Coast(Cell, 20), 1, null, null, forms);
        ScriptedController Swap(long a, long b, long ship, long y)
        {
            var c = new ScriptedController();
            for (int p = 0; p < 40; p++)
            {
                long boarding = p % 2 == 0 ? a : b, landing = p % 2 == 0 ? b : a;
                c.Add(p, new OrderRequest(boarding, OrderKind.Embark, 0, ship));
                c.Add(p, new OrderRequest(landing, OrderKind.Disembark, 0, -1, C(21, y)));
            }
            return c;
        }
        var b = new TacticalBattle(setup, Swap(2, 3, 1, 30), Swap(5, 6, 4, 6));
        var r = RunChecked(b, 40);
        Assert.Equal(BattleEndReason.Standoff, r.Reason);           // measured: 6 pulses
        Assert.Equal(6, r.Pulses);
        Assert.Empty(b.InactivityVerdicts);                          // nobody is ever judged: no ashore stretch reaches five
        Assert.All(r.Formations, f => Assert.Equal(0, f.Killed + f.Surrendered + f.RoutedEscaped));
        Assert.Contains(b.Personnel.Flows, f => f.Reason == PersonnelFlowReason.Embark && b.Personnel.AccountInfo(f.From).FormationId == 2);
        Assert.Contains(b.Personnel.Flows, f => f.Reason == PersonnelFlowReason.Disembark && b.Personnel.AccountInfo(f.To).FormationId == 3);
    }

    // ---------------------------------------------------------------- verification round 1 (N-R1-01, -02b, -03a, -04)

    /// <summary>Landed / boarded people of a formation per completed instant, from the ledger flows (exact).</summary>
    private static long Ops(TacticalBattle b, PersonnelFlowReason reason, long atMs)
    {
        long n = 0;
        foreach (var f in b.Personnel.Flows) if (f.Reason == reason && f.AtMs == atMs) n += f.Amount;
        return n;
    }

    /// <summary>
    /// N-R1-01: a hull's loading time is ONE budget per step, shared by every formation it serves and by both operations.
    /// (a) The same 20 men landing as one formation or split into two formations on the same hull land at exactly the same
    /// per-instant rate (metamorphic); (b) two formations embarking onto one hull board at most floor(StepMs / EmbarkMs) per
    /// step between them; (c) a hull that lands one formation while embarking another spends one budget on both:
    /// boarded * EmbarkMs + landed * DisembarkMs never exceeds one step's time, cumulatively over the operation (exact carry).
    /// Before the fix each (formation, operation) pair earned a full step of time from the same hull (measured: 2x rate).
    /// </summary>
    [Fact]
    public void HullLoadingTimeIsSharedByFormationsLanding()
    {
        long step = new TacticalRules().StepMs;
        // one formation of 20 vs the same men as two formations of 10 on the same hull
        var t = T30 with { DisembarkMsPerPerson = 1_000 };
        long[] Landing(bool split)
        {
            var forms = new List<FormationSpec> { Ship(1, 0, t, C(19, 20)) };
            if (split)
            {
                forms.Add(new FormationSpec(2, 0, DomainProfiles.Riflemen, 10, C(20, 20)) { EmbarkedOn = 1 });
                forms.Add(new FormationSpec(3, 0, DomainProfiles.Riflemen, 10, C(20, 22)) { EmbarkedOn = 1 });
            }
            else forms.Add(new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, C(20, 20)) { EmbarkedOn = 1 });
            forms.Add(new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));
            var setup = OnGrid(Coast(Cell, 20), 1, null, null, forms.ToArray());
            var orders = split
                ? Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(25, 20))), (0, new OrderRequest(3, OrderKind.Disembark, 0, -1, C(25, 22))))
                : Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(25, 20))));
            var b = new TacticalBattle(setup, orders, PassiveController.Instance);
            var series = new List<long>();
            RunUntil(b, () =>
            {
                long now = b.Scheduler.NowMs;
                if ((now - setup.StartMs) % step == 0) series.Add(b.Personnel.Flows.Where(f => f.Reason == PersonnelFlowReason.Disembark).Sum(f => f.Amount));
                return series.Count > 0 && series[^1] == 20;
            }, 5);
            Assert.Null(b.PersonnelConsistency());
            return series.ToArray();
        }
        var one = Landing(false);
        var two = Landing(true);
        Assert.Equal(one, two);                                                   // splitting a force does not speed a hull up
        long perStep = step / t.DisembarkMsPerPerson;
        for (int i = 0; i < one.Length; i++) Assert.Equal(Math.Min(20, (i + 1) * perStep), one[i]);
    }

    /// <summary>N-R1-01 (b): two formations embarking onto one hull board at most floor(StepMs / EmbarkMsPerPerson) per step
    /// between them.</summary>
    [Fact]
    public void HullLoadingTimeIsSharedByFormationsEmbarking()
    {
        long step = new TacticalRules().StepMs;
        var setup = OnGrid(Coast(Cell, 20), 1, null, null,
            Ship(1, 0, T30, C(19, 20)),                                                // hull centre (975 m, 1025 m)
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 10, M(1030, 1005)),      // both ~58 m from the hull
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 10, M(1030, 1045)),
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(2, OrderKind.Embark, 0, 1)), (0, new OrderRequest(3, OrderKind.Embark, 0, 1))),
                                   PassiveController.Instance);
        long cap = step / T30.EmbarkMsPerPerson; int contested = 0; bool f3InRange = false;
        Int128 r2 = (Int128)new DomainRules().EmbarkRangeMm * new DomainRules().EmbarkRangeMm;
        RunUntil(b, () =>
        {
            long now = b.Scheduler.NowMs;
            long n2 = 0, n3 = 0;
            foreach (var f in b.Personnel.Flows)
                if (f.Reason == PersonnelFlowReason.Embark && f.AtMs == now)
                {
                    if (b.Personnel.AccountInfo(f.From).FormationId == 2) n2 += f.Amount; else n3 += f.Amount;
                }
            Assert.True(n2 + n3 <= cap, $"boarded {n2}+{n3} onto one hull in one step > {cap}");
            if (n2 > 0 && f3InRange) contested++;            // formation 3 was waiting in range while formation 2 boarded
            var hull = D(b, 1000).Pos;
            f3InRange = b.Entities().Any(e => e.FormationId == 3 && e.State == EntityState.Active && Vec2L.DistanceSq(e.Pos, hull) <= r2);
            return CountState(b, 2, EntityState.Embarked) + CountState(b, 3, EntityState.Embarked) == 20;
        }, 6);
        Assert.True(contested > 0, "formation 3 never waited in range while formation 2 boarded: the shared case was not exercised");
        Assert.Null(b.PersonnelConsistency());
    }

    /// <summary>N-R1-01 (c): a hull that lands one formation while embarking another spends ONE budget on both:
    /// boarded * EmbarkMs + landed * DisembarkMs never exceeds the time of the steps elapsed (exact carry).</summary>
    [Fact]
    public void HullLoadingTimeIsSharedByLandingAndEmbarking()
    {
        long step = new TacticalRules().StepMs;
        var t2 = T30 with { EmbarkMsPerPerson = 1_500, DisembarkMsPerPerson = 1_000 };
        var setup = OnGrid(Coast(Cell, 20), 1, null, null,
            Ship(1, 0, t2, C(19, 20)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 10, C(20, 20)) { EmbarkedOn = 1 },
            new FormationSpec(3, 0, DomainProfiles.Riflemen, 10, M(1030, 1045)),
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(25, 20))), (0, new OrderRequest(3, OrderKind.Embark, 0, 1))),
                                   PassiveController.Instance);
        long used = 0, steps = 0; bool mixed = false;
        RunUntil(b, () =>
        {
            long now = b.Scheduler.NowMs;
            if ((now - setup.StartMs) % step != 0) return false;
            long e = Ops(b, PersonnelFlowReason.Embark, now), l = Ops(b, PersonnelFlowReason.Disembark, now);
            steps++;                                         // every step since the orders (delivered at the first one)
            used += e * t2.EmbarkMsPerPerson + l * t2.DisembarkMsPerPerson;
            if (e > 0 && l > 0) mixed = true;
            Assert.True(used <= steps * step, $"hull worked {used} ms in {steps} steps of {step} ms");
            return CountState(b, 2, EntityState.Embarked) == 0 && CountState(b, 3, EntityState.Embarked) == 10;
        }, 6);
        Assert.True(mixed, "landing and embarking never shared a step: the shared case was not exercised");
        Assert.Null(b.PersonnelConsistency());
    }

    /// <summary>
    /// N-R1-03a: embarking needs a STOPPED hull (speed &lt;= StoppedSpeed). A transport steaming along the coast passes
    /// within embark range of a formation ordered to embark onto it: nobody boards while the hull is under way.
    /// </summary>
    [Fact]
    public void EmbarkNeedsAStoppedHull()
    {
        var rules = new DomainRules();
        var setup = OnGrid(Coast(Cell, 20), 1, rules, null,
            Ship(1, 0, T30, C(19, 10)),
            new FormationSpec(2, 0, DomainProfiles.Riflemen, 20, C(20, 20)),
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(62, 2)));
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, C(19, 34))), (0, new OrderRequest(2, OrderKind.Embark, 0, 1))),
                                   PassiveController.Instance);
        Int128 r2 = (Int128)rules.EmbarkRangeMm * rules.EmbarkRangeMm;
        int passes = 0, prevAboard = 0;
        RunUntil(b, () =>
        {
            var v = D(b, 1000);
            int aboard = CountState(b, 2, EntityState.Embarked);
            if (aboard > prevAboard) Assert.True(v.Speed <= rules.StoppedSpeedMmPerS, $"boarded at {v.Speed} mm/s");
            prevAboard = aboard;
            if (v.Speed > rules.StoppedSpeedMmPerS && b.Entities().Any(e => e.FormationId == 2 && e.State == EntityState.Active && Vec2L.DistanceSq(e.Pos, v.Pos) <= r2))
                passes++;
            return b.CurrentPulse >= 3;
        }, 4);
        Assert.True(passes > 0, "the moving hull never came within embark range of the troops: the case was not exercised");
        Assert.Equal(0, CountState(b, 2, EntityState.Embarked));
    }

    /// <summary>
    /// N-R1-02b: landed troops are EXPOSED for LandingExposureMs. Camouflaged troops (personal concealment 1/8, TEST INPUT)
    /// land on an open beach watched by an enemy 500 m inland (in sight, out of either side's range). Exposure lifts the
    /// camouflage, so every landed soldier is acquired with certainty (open ground: clarity 1) at the first detection after
    /// landing; and each landed soldier's exposure ends exactly LandingExposureMs after the instant it landed.
    /// </summary>
    [Fact]
    public void LandedTroopsAreExposedForTheLandingWindow()
    {
        var rules = new DomainRules();
        var camo = DomainProfiles.Riflemen with { PersonalConcealment = Proto.M7.Fixed.Q32.FromRatio(1, 8) };
        var setup = OnGrid(Coast(Cell, 20), 3, rules, null,
            Ship(1, 0, T30, C(19, 20)),
            new FormationSpec(2, 0, camo, 10, C(20, 20)) { EmbarkedOn = 1 },
            new FormationSpec(9, 1, DomainProfiles.Riflemen, 5, C(30, 20)));
        long step = setup.Rules.StepMs;
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(2, OrderKind.Disembark, 0, -1, C(21, 20)))), PassiveController.Instance);
        var landedAt = new SortedDictionary<long, long>();
        RunUntil(b, () =>
        {
            long now = b.Scheduler.NowMs;
            foreach (var e in b.Entities())
                if (e.FormationId == 2 && e.State == EntityState.Active && !landedAt.ContainsKey(e.Id)) landedAt[e.Id] = now;
            foreach (var (id, at) in landedAt)
                if (now >= at + step) Assert.True(b.EverDetected(1, id), $"landed soldier {id} (at {at}) not acquired by {now}");
            return landedAt.Count == 10 && now >= landedAt.Values.Max() + step;
        }, 6);
        foreach (var (id, at) in landedAt) Assert.Equal(at + rules.LandingExposureMs, b.RevealedUntilMs(id));
    }

    /// <summary>
    /// ORDER-DELIVERY PIN (N-R1-04, turn-exact with ±1 neighbours): a Disembark order delivered at step offset k starts the
    /// landing at pulseStart + k * StepMs — nobody ashore one step earlier; at that instant exactly
    /// floor(StepMs / DisembarkMsPerPerson) people have landed, and landing continues at that rate.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DisembarkTakesEffectAtTheDeliveredStep(int k)
    {
        var setup = Shore(20, aboard: true);
        long step = setup.Rules.StepMs, perStep = step / T30.DisembarkMsPerPerson;
        var b = new TacticalBattle(setup, Script((0, new OrderRequest(2, OrderKind.Disembark, k, -1, C(25, 20)))), PassiveController.Instance);
        long tk = setup.StartMs + k * step;
        var ashoreAt = new SortedDictionary<long, int>();
        RunUntil(b, () => { ashoreAt[b.Scheduler.NowMs] = CountState(b, 2, EntityState.Active); return b.Scheduler.NowMs >= tk + 3 * step; });
        Assert.Equal(0, ashoreAt[tk - step]);
        Assert.Equal(perStep, ashoreAt[tk]);
        Assert.Equal(2 * perStep, ashoreAt[tk + step]);
        Assert.Null(b.PersonnelConsistency());
    }
}
