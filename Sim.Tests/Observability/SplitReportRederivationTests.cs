using Sim.Core.Observability;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Observability;

/// <summary>
/// T3 ROUND 2 (2026-10-17; D-055 / ADR-040, batch 4 item 3 — "the leg must PROVE BEHAVIOUR") — THE CHECKER'S NEW PROPERTIES,
/// TESTED ON REAL REPORTS (Fixtures/split-report): P11 re-derives every row's split from S, b, D_b and D_r (finding F2: a
/// share rounded up, mutant V2-CEIL, passed every earlier property); P12 re-walks every row's heads in THE BUILDING ORDER
/// against the grant (finding F3: a reversed settlement tie-break, mutant CM06c, passed them too); PT judges a kind-12
/// order against its twin (finding F1: an order delivered where no head drew proved nothing, and a split applied to
/// spending a step late, mutant CM04b, passed byte for byte). AIM: the real reports pass — a.jsonl and its control, and
/// k.jsonl (the ci.yml leg's continuation from save_K, b = 70 delivered at 136 with three granaries) with its kind-12 twin
/// k-no12.jsonl. TEETH: one mutated report per property — each shaped like the mutant it stands for and otherwise
/// consistent, so that property alone fails — plus a tie-dense building order.
/// </summary>
public class SplitReportRederivationTests
{
    private static SplitReportRow[] Read(string name) =>
        SplitReport.Parse(File.ReadAllText(SplitReportCheckTests.FixturePath(name)));

    private static SplitReportRow[] A() => SplitReportCheckTests.A();
    private static SplitReportRow[] Control() => SplitReportCheckTests.Control();
    private static SplitReportRow[] K() => Read("k.jsonl");
    private static SplitReportRow[] KNo12() => Read("k-no12.jsonl");

    private const long Seventy = 136;

    private static SplitTwin Twin(SplitReportRow[] rows) => new(rows, Seventy, 70);

    /// <summary>The row of <paramref name="rows"/> at <paramref name="turn"/> replaced by <paramref name="change"/>.</summary>
    private static SplitReportRow[] AtRow(SplitReportRow[] rows, long turn, Func<SplitReportRow, SplitReportRow> change) =>
        rows.Select(r => r.Turn == turn ? change(r) : r).ToArray();

    /// <summary>Good <paramref name="good"/> of <paramref name="row"/> replaced by <paramref name="change"/>.</summary>
    private static SplitReportRow Good(SplitReportRow row, string good, Func<SplitGoodFigures, SplitGoodFigures> change) =>
        row with { Goods = row.Goods.Select(g => g.Good == good ? change(g) : g).ToArray() };

    /// <summary>Good <paramref name="good"/> of head <paramref name="index"/> of <paramref name="row"/> replaced.</summary>
    private static SplitReportRow HeadGood(SplitReportRow row, int index, string good, Func<SplitHeadGood, SplitHeadGood> change) =>
        row with
        {
            Heads = row.Heads.Select((h, i) => i != index ? h : h with
            {
                Goods = h.Goods.Select(g => g.Good == good ? change(g) : g).ToArray(),
            }).ToArray(),
        };

    private static SplitGoodFigures StoneOf(SplitReportRow r) => r.Goods.Single(g => g.Good == "stone");

    private static SplitReportRow RowAt(SplitReportRow[] rows, long turn) => rows.Single(r => r.Turn == turn);

    // ================================================================== aim: the real reports pass P11, P12 and PT

    [Fact]
    public void TheRealReports_PassP11AndP12_AndTheKind12PairPassesPT_WithTheDrawsPinned()
    {
        foreach ((string name, SplitCheckResult result) in new[]
        {
            ("a.jsonl", SplitReportCheck.Check(A(), Control(), 120)),
            ("control.jsonl", SplitReportCheck.Check(Control())),
            ("k.jsonl", SplitReportCheck.Check(K(), expectHoldAt: 131)),
            ("k-no12.jsonl", SplitReportCheck.Check(KNo12())),
        })
            Assert.False(result.Failed("P11") || result.Failed("P12"), name + ": " + string.Join(" | ", result.Failures));
        // P12 is not vacuous on them: rows with two or more heads, and b = 100 rows with heads (the control).
        Assert.Contains(A(), r => r.Heads.Length >= 2);
        Assert.Contains(Control(), r => r.Heads.Length >= 2 && r.BuildingsPercent == 100);
        Assert.Equal(3, RowAt(K(), Seventy).Heads.Length);

        // k.jsonl is the ci.yml leg's report B (the continuation from save_K) and passes as the leg runs it; with its twin
        // it passes PT, which names the draw the order changed IN ITS OWN STEP: stone 51 (70 % of 73) against 21 (30 %).
        SplitCheckResult k = SplitReportCheck.Check(K(), expectHoldAt: 131, twin: Twin(KNo12()));
        Assert.True(k.Passed, string.Join(" | ", k.Failures));
        Assert.Equal("the kind-12 order at 136 (PT): b 30 -> 70 in step 136->137; building work drew, against the twin without it: " +
                     "stone 51 vs 21 (E_b 51 vs 21, G_b 51 vs 21), timber 102 vs 42 (E_b 310121 vs 132909, G_b 120 vs 120)", k.Findings[^1]);
        SplitGoodFigures a = StoneOf(RowAt(K(), Seventy)), t = StoneOf(RowAt(KNo12(), Seventy));
        Assert.Equal((70, 73L, 51L, 51L, 60L), (RowAt(K(), Seventy).BuildingsPercent, a.S, a.Eb, a.Build, a.Db));
        Assert.Equal((30, 73L, 21L, 21L, 60L), (RowAt(KNo12(), Seventy).BuildingsPercent, t.S, t.Eb, t.Build, t.Db));
        // The twin's rows before 136 are k.jsonl's, byte for byte (the same orders until the dropped one).
        string[] kLines = File.ReadAllLines(SplitReportCheckTests.FixturePath("k.jsonl"));
        string[] twinLines = File.ReadAllLines(SplitReportCheckTests.FixturePath("k-no12.jsonl"));
        Assert.Equal(kLines[..6], twinLines[..6]);
        Assert.NotEqual(kLines[6], twinLines[6]);
    }

    // ================================================================== P11: the split's arithmetic, re-derived

    /// <summary>
    /// V2-CEIL'S SHAPE: building work's share rounded UP (E_b = ceil(S × b / 100)). Row 117 → 118's stone as the mutant
    /// writes it — E_b 20, E_r 44, cap 20, G_b 20, the walk started at 20, settlement 0's head drew 20 and completed, road
    /// works 44: the real V2-CEIL leg's report has exactly these stone figures at 117 (t3-out/r2/legs/V2-CEIL) — is
    /// consistent with every other property (the forecast, the grant spent, the walk, the guarantee), so only P11 sees it:
    /// floor(64 × 30 / 100) = 19.
    /// </summary>
    [Fact]
    public void P11_AShareRoundedUp_FailsP11Alone_NamingTheRow()
    {
        SplitReportRow r117 = RowAt(A(), 117);
        Assert.Equal((30, 64L, 19L, 45L, 40L, 63L, 19L, 19L, 19L, 45L), (r117.BuildingsPercent, StoneOf(r117).S, StoneOf(r117).Eb,
            StoneOf(r117).Er, StoneOf(r117).Db, StoneOf(r117).Dr, StoneOf(r117).Cap, StoneOf(r117).Gb, StoneOf(r117).Build, StoneOf(r117).Road));
        SplitReportRow[] ceil = AtRow(A(), 117, r =>
        {
            SplitReportRow m = HeadGood(
                Good(r, "stone", g => g with
                {
                    Eb = 20, Er = 44, Cap = 20, Gb = 20, Start = 20, Build = 20, ForecastBuild = 20, Road = 44, ForecastRoad = 44,
                }),
                0, "stone", h => h with { Budget = 20, Draw = 20, Cause = "None" });
            return m with { Heads = m.Heads.Select((h, i) => i == 0 ? h with { After = 1_000_000, Limit = "None" } : h).ToArray() };
        });
        SplitCheckResult result = SplitReportCheck.Check(ceil, Control(), 120);
        Assert.Equal(["P11: step 117->118 (polity 1) stone: E_b = 20, but floor(S x b / 100) = floor(64 x 30 / 100) = 19"], result.Failures);
        Assert.False(SplitReportCheck.Check(A(), Control(), 120).Failed("P11"));
    }

    [Theory]
    [InlineData("E_r is not S - E_b")]
    [InlineData("the cap ignores the road surplus")]
    [InlineData("the grant is not min(D_b, cap)")]
    [InlineData("b is not a percentage")]
    [InlineData("a negative claim")]
    public void P11_EveryOtherFigureOfTheSplit_IsReDerived(string clause)
    {
        SplitReportRow[] mutant = clause switch
        {
            "E_r is not S - E_b" => AtRow(A(), 117, r => Good(r, "stone", g => g with { Er = 46 })),
            // Row 117 -> 118 timber: cap = 105140 + max(0, 245328 - 15) = 350453; one more is a cap computed otherwise, and
            // the grant (80 = D_b) is unaffected, so P2 still holds.
            "the cap ignores the road surplus" => AtRow(A(), 117, r => Good(r, "timber", g => g with { Cap = g.Cap + 1 })),
            // Row 117 -> 118 stone with a grant of 18 where min(40, 19) = 19 — the walk, the draws and the road share all
            // consistent with 18, so P2 and P12 hold and only the grant's derivation is wrong.
            "the grant is not min(D_b, cap)" => AtRow(A(), 117, r => HeadGood(
                Good(r, "stone", g => g with { Gb = 18, Start = 18, Build = 18, ForecastBuild = 18 }),
                0, "stone", h => h with { Budget = 18, Draw = 18 })),
            "b is not a percentage" => AtRow(A(), 117, r => r with { BuildingsPercent = 101 }),
            "a negative claim" => AtRow(A(), 116, r => Good(r, "stone", g => g with { Db = -1 })),
            _ => throw new ArgumentOutOfRangeException(nameof(clause)),
        };
        SplitCheckResult result = SplitReportCheck.Check(mutant, Control(), 120);
        Assert.True(result.Failed("P11"), string.Join(" | ", result.Failures));
        string p11 = result.Failures.Single(f => f.StartsWith("P11:", StringComparison.Ordinal));
        Assert.Contains(clause switch
        {
            "E_r is not S - E_b" => "step 117->118 (polity 1) stone: E_r = 46, but S - E_b = 64 - 19 = 45",
            "the cap ignores the road surplus" => "step 117->118 (polity 1) timber: the cap is 350454, but E_b + max(0, E_r - D_r) = 105140 + max(0, 245328 - 15) = 350453",
            "the grant is not min(D_b, cap)" => "step 117->118 (polity 1) stone: G_b = 18, but min(D_b, cap) = min(40, 19) = 19",
            "b is not a percentage" => "step 117->118 (polity 1) timber: the building share b = 101 is not a whole percentage in [0, 100]",
            _ => "step 116->117 (polity 1) stone: the purse S = 4913, the claim D_b = -1 and the road demand D_r = 4913 must not be negative",
        }, p11);
        if (clause is "the cap ignores the road surplus" or "the grant is not min(D_b, cap)")
            Assert.Equal([p11], result.Failures);   // that clause alone
    }

    /// <summary>The starvation guard: a guarded row's split is taken with D_r = 0 (ResourceAllocation's header). Row 117 → 118
    /// timber recorded as guarded the way the rule records it (D_r 0, cap = S − 0 surplus kept: 350468, G_b = D_b) passes P11;
    /// the same row marked guarded but with the cap of D_r = 15 fails, and says why.</summary>
    [Fact]
    public void P11_AGuardedRow_TakesDrAsZero()
    {
        SplitReportRow[] honest = AtRow(A(), 117, r => Good(r with { Guarded = true }, "timber", g => g with { Dr = 0, Cap = 350468 }));
        Assert.DoesNotContain(SplitReportCheck.Check(honest).Failures, f => f.StartsWith("P11:", StringComparison.Ordinal) && f.Contains("timber", StringComparison.Ordinal));
        SplitReportRow[] stale = AtRow(A(), 117, r => r with { Guarded = true });
        string p11 = SplitReportCheck.Check(stale).Failures.Single(f => f.StartsWith("P11:", StringComparison.Ordinal));
        Assert.StartsWith("P11: step 117->118 (polity 1) timber: the cap is 350453, but E_b + max(0, E_r - D_r) = 105140 + max(0, 245328 - 0) = 350468 " +
                          "(D_r taken as 0: the starvation guard fired)", p11);
    }

    // ================================================================== P12: the walk, re-derived

    /// <summary>
    /// CM06c'S SHAPE: the building order's settlement tie-break reversed. At 117 → 118 both heads are fresh granaries ordered
    /// at 117 (the same need), so the mutant's row is the real one with the two settlements' labels exchanged: settlement 2
    /// served first — field for field the row the real CM06c leg's report holds (t3-out/r2/legs/CM06c). Every sum, the grant,
    /// the walk's budgets and P7's witness are unchanged — only the ORDER is wrong.
    /// </summary>
    [Fact]
    public void P12_AWalkInAnotherOrder_FailsP12Alone_NamingTheTwoHeads()
    {
        SplitReportRow r117 = RowAt(A(), 117);
        Assert.Equal(new[] { (117L, 0, 0, 0L), (117L, 2, 0, 0L) }, r117.Heads.Select(h => (h.Enqueued, h.Settlement, h.Slot, h.Before)).ToArray());
        Assert.Equal((20L, 20L), (r117.Heads[0].GoodOf(5)!.Need, r117.Heads[1].GoodOf(5)!.Need));
        SplitReportRow[] reversed = AtRow(A(), 117, r => r with
        {
            Heads = [r.Heads[0] with { Settlement = 2 }, r.Heads[1] with { Settlement = 0 }],
        });
        SplitCheckResult result = SplitReportCheck.Check(reversed, Control(), 120);
        Assert.Equal(["P12: step 117->118 (polity 1): head s2:0 (enqueued 117) is walked before s0:0 (enqueued 117), out of THE BUILDING " +
                      "ORDER (EnqueuedTurn, SettlementId, Slot strictly ascending)"], result.Failures);
        // The same mutant on the control's row (b = 100): P9 now requires the control's rows to be the rule's as well.
        SplitReportRow[] control = AtRow(Control(), 117, r => r with
        {
            Heads = [r.Heads[0] with { Settlement = 2 }, r.Heads[1] with { Settlement = 0 }],
        });
        Assert.StartsWith("P9: the control's step 117->118 (polity 1) breaks P12: step 117->118 (polity 1): head s2:0",
            SplitReportCheck.Check(A(), control, 120).Failures.Single());
    }

    [Theory]
    [InlineData("the walk started above the grant")]
    [InlineData("the walk started at the grant at b = 100")]
    [InlineData("a head resolved against a budget the heads ahead did not leave")]
    [InlineData("a later head took a unit the earlier head needed")]
    [InlineData("a released head set some aside")]
    public void P12_AWalkThatSpentWhatTheRuleDoesNotGive_FailsP12Alone(string clause)
    {
        (SplitReportRow[] report, SplitReportRow[]? control, string expected) = clause switch
        {
            "the walk started above the grant" => (AtRow(A(), 117, r => Good(r, "stone", g => g with { Start = 20 })), Control(),
                "P12: step 117->118 (polity 1) stone: the walk started at 20, not at G_b = 19 (the split's grant)"),
            // The control's 117 -> 118 (b = 100): its walk starts at the whole purse S = 64, not at G_b = 40.
            "the walk started at the grant at b = 100" => (AtRow(Control(), 117, r => Good(r, "stone", g => g with { Start = 40 })), null,
                "P12: step 117->118 (polity 1) stone: the walk started at 40, not at S = 64 (b = 100: the split never binds)"),
            "a head resolved against a budget the heads ahead did not leave" =>
                (AtRow(A(), 117, r => HeadGood(r, 1, "stone", h => h with { Budget = 5 })), Control(),
                 "P12: step 117->118 (polity 1) stone: head s2:0 (enqueued 117) was resolved against 5, but the heads ahead of it in the " +
                 "building order left 0 of the walk's 19"),
            // One unit moved from settlement 0's head to settlement 2's: the sums (P1, P2) unchanged, the earlier head short.
            "a later head took a unit the earlier head needed" =>
                (AtRow(A(), 117, r => HeadGood(HeadGood(r, 0, "stone", h => h with { Draw = 18 }), 1, "stone", h => h with { Draw = 1 })), Control(),
                 "P12: step 117->118 (polity 1) stone: head s0:0 (enqueued 117) drew 18 and set aside 0, but the D-055 walk gives an " +
                 "unreleased head min(its need, what the heads ahead left) = min(20, 19) = 19"),
            // Step 119 -> 120: settlement 0's promoted head (no builders) sets aside 19 stone — marked released, it may not.
            "a released head set some aside" => (AtRow(A(), 119, r => r with
                {
                    Heads = r.Heads.Select((h, i) => i == 0 ? h with { Release = "NoLabourAdults" } : h).ToArray(),
                }), Control(),
                "P12: step 119->120 (polity 1) timber: head s0:1 (enqueued 117) is released (NoLabourAdults) but drew 0 and set aside 40: " +
                "a released head takes nothing (2 violations in all)"),
            _ => throw new ArgumentOutOfRangeException(nameof(clause)),
        };
        SplitCheckResult result = control is null ? SplitReportCheck.Check(report) : SplitReportCheck.Check(report, control, 120);
        // P12 alone among the every-row properties (a control checked on its own also fails the existence properties).
        Assert.Equal([expected], result.Failures.Where(f => Array.Exists(["P1:", "P2:", "P3:", "P11:", "P12:"], p => f.StartsWith(p, StringComparison.Ordinal))).ToArray());
        if (control is not null) Assert.Equal([expected], result.Failures);
    }

    /// <summary>
    /// TIE-DENSE BUILDING ORDER (CLAUDE.md: every ordering ships a tie-dense test): 24 heads — three migrated entries
    /// (EnqueuedTurn −1), twelve ordered at turn 5 and nine at turn 7, settlements inserted out of id order — sort by
    /// <see cref="SplitReportCheck.CompareBuildingOrder"/> into EnqueuedTurn, then SettlementId, then Slot, whatever the
    /// insertion order; a row walked in that order (24 granary-like heads needing 3 stone each, a grant of 60: the first
    /// twenty served) passes P12; and exchanging the labels of ANY adjacent pair — across a turn boundary or inside a tie —
    /// fails P12 naming exactly that pair. A tie on (EnqueuedTurn, SettlementId) — never two heads of one settlement in a
    /// real report — falls to the Slot.
    /// </summary>
    [Fact]
    public void P12_TieDense_TheBuildingOrderIsTotal_AndEveryAdjacentSwapIsNamed()
    {
        (long Turn, int Settlement)[] keys =
        [
            (5, 7), (-1, 21), (7, 15), (5, 0), (5, 11), (7, 12), (-1, 4), (5, 3), (7, 20), (5, 9), (7, 13), (5, 1),
            (7, 19), (5, 10), (-1, 9), (5, 2), (7, 14), (5, 8), (7, 18), (5, 4), (7, 16), (5, 6), (7, 17), (5, 5),
        ];
        SplitHeadFigures Head((long Turn, int Settlement) k) => new(k.Settlement, 0, 1, k.Turn, 0, 0, "None", "Materials",
            [new SplitHeadGood("stone", 5, 3, 0, 0, 0, "None", null, 0)]);
        var expected = keys.OrderBy(k => k.Turn).ThenBy(k => k.Settlement).Select(Head).ToArray();
        foreach (IEnumerable<(long, int)> insertion in new[] { keys.AsEnumerable(), keys.AsEnumerable().Reverse(), keys.Skip(11).Concat(keys.Take(11)) })
        {
            var heads = insertion.Select(Head).ToList();
            heads.Sort(SplitReportCheck.CompareBuildingOrder);
            Assert.Equal(expected.Select(h => (h.Enqueued, h.Settlement)), heads.Select(h => (h.Enqueued, h.Settlement)));
        }
        Assert.Equal((-1L, 4), (expected[0].Enqueued, expected[0].Settlement));
        Assert.Equal((5L, 0), (expected[3].Enqueued, expected[3].Settlement));
        Assert.Equal((7L, 20), (expected[^1].Enqueued, expected[^1].Settlement));

        // The walk on a grant of 60: each head needs 3, so the first twenty draw 3 and the last four nothing.
        SplitHeadFigures[] walked = expected.Select((h, i) => h with
        {
            After = i < 20 ? 1_000_000 : 0, Limit = i < 20 ? "None" : "Materials",
            Goods = [h.Goods[0] with { Budget = 60 - 3L * Math.Min(i, 20), Draw = i < 20 ? 3 : 0 }],
        }).ToArray();
        var stone = new SplitGoodFigures("stone", 5, 200, 60, 60, 140, 72, 200, 60, 60, true, 60, 60, 0, 0, 140, 140);
        var row = new SplitReportRow(4, 1, 30, 100.0, false, [stone], walked);
        SplitCheckResult clean = SplitReportCheck.Check([row]);
        Assert.False(clean.Failed("P12") || clean.Failed("P11") || clean.Failed("P1") || clean.Failed("P2"), string.Join(" | ", clean.Failures));

        for (int i = 1; i < walked.Length; i++)
        {
            // Exchange the labels (turn and settlement) of heads i − 1 and i; their figures stay where the walk spent them.
            SplitHeadFigures[] swapped = (SplitHeadFigures[])walked.Clone();
            swapped[i - 1] = walked[i - 1] with { Enqueued = walked[i].Enqueued, Settlement = walked[i].Settlement };
            swapped[i] = walked[i] with { Enqueued = walked[i - 1].Enqueued, Settlement = walked[i - 1].Settlement };
            string p12 = Assert.Single(SplitReportCheck.Check([row with { Heads = swapped }]).Failures, f => f.StartsWith("P12:", StringComparison.Ordinal));
            Assert.StartsWith($"P12: step 4->5 (polity 1): head s{walked[i].Settlement}:0 (enqueued {walked[i].Enqueued}) is walked before " +
                              $"s{walked[i - 1].Settlement}:0 (enqueued {walked[i - 1].Enqueued}), out of THE BUILDING ORDER", p12);
        }

        // (EnqueuedTurn, SettlementId) tied — the Slot decides; equal keys are not "strictly ascending".
        SplitHeadFigures s1 = expected[0] with { Slot = 1 }, s0 = expected[0];
        Assert.True(SplitReportCheck.CompareBuildingOrder(s0, s1) < 0 && SplitReportCheck.CompareBuildingOrder(s1, s0) > 0);
        Assert.Equal(0, SplitReportCheck.CompareBuildingOrder(s0, s0 with { Project = 9, Before = 5 }));
    }

    // ================================================================== P12: the division of a claim into draw and set-aside

    /// <summary>
    /// T3 ROUND 3 (finding T3-L1-T3-V1L1-N1) — THE DRAW/SET-ASIDE DIVISION. Row 130 → 131 of a.jsonl: two granaries ordered at
    /// 130 (settlements 0 and 2), each resolved against a budget covering its whole need (s0:0 timber 80 / stone 40, s2:0
    /// timber 40 / stone 20, for 40 / 20 each), each drew its need and completed. Rewritten as two shared-walk mutants write
    /// it (verify-1 v3l1, legs MW1-heads-never-draw and MW2-younger-never-draw: ResolveHead handed an empty budget — always, or
    /// once the purse has spent) — the head draws nothing, sets its need aside, does not move and is limited by
    /// <paramref name="limit"/>, timber its binding good (HeldForEarlier), and the purse's totals move with it (build falls by
    /// the draws, holds rise by them, forecasts too). The walk's equality (draw + hold = min(need, budget left)), the grant
    /// spent, the forecast, the guarantee and every existence property still hold — the leg passed both mutants on report A —
    /// so only P12's division clause sees it: a head limited by materials whose budget covered its whole need. MW2's shape
    /// (the younger head only) has budget = need exactly, so the clause is strict (budget &lt; need). AIM: the same rewrite
    /// limited by Labour — a head with no builders, which legitimately sets its need aside without drawing (a.jsonl 119–124)
    /// — passes.
    /// </summary>
    [Theory]
    [InlineData("MW1", "Materials", true)]
    [InlineData("MW1", "Unobtainable", true)]
    [InlineData("MW1", "Labour", false)]
    [InlineData("MW2", "Materials", true)]
    [InlineData("MW2", "Labour", false)]
    public void P12_AHeadWhoseBudgetCoveredItsWholeNeed_ReportedMaterialsLimited_FailsP12Alone(string shape, string limit, bool fails)
    {
        SplitReportRow r130 = RowAt(A(), 130);
        Assert.Equal((30, 2), (r130.BuildingsPercent, r130.Heads.Length));
        Assert.All(r130.Heads, h => Assert.Equal(("None", 130L, 0L, 1_000_000L), (h.Limit, h.Enqueued, h.Before, h.After)));
        Assert.Equal([(40L, 80L, 40L, 0L), (20L, 40L, 20L, 0L)], r130.Heads[0].Goods.Select(g => (g.Need, g.Budget!.Value, g.Draw, g.Hold)));
        Assert.Equal([(40L, 40L, 40L, 0L), (20L, 20L, 20L, 0L)], r130.Heads[1].Goods.Select(g => (g.Need, g.Budget!.Value, g.Draw, g.Hold)));

        int from = shape == "MW1" ? 0 : 1;   // MW1: every head; MW2: the younger head only
        SplitReportRow[] mutant = AtRow(A(), 130, r =>
        {
            var heads = r.Heads.Select((h, i) => i < from ? h : h with
            {
                After = h.Before,
                Limit = limit,
                Goods = h.Goods.Select(g => g with
                {
                    Draw = 0,
                    Hold = g.Hold + g.Draw,
                    Cause = g.Good == "timber" ? "HeldForEarlier" : "None",
                    Holder = g.Good == "timber" && i == 1 && shape == "MW1" ? new SplitHeadRef(0, 0, r.Heads[0].Project) : null,
                    SetAsideAhead = g.Good == "timber" && i == 1 && shape == "MW1" ? 40 : 0,
                }).ToArray(),
            }).ToArray();
            var goods = r.Goods.Select(g =>
            {
                long moved = 0;
                for (int i = from; i < r.Heads.Length; i++) moved += r.Heads[i].GoodOf(g.Id)?.Draw ?? 0;
                return g with { Build = g.Build - moved, ForecastBuild = g.ForecastBuild - moved, Holds = g.Holds + moved, ForecastHolds = g.ForecastHolds + moved };
            }).ToArray();
            return r with { Goods = goods, Heads = heads };
        });
        SplitReportRow m = RowAt(mutant, 130);
        Assert.Equal(shape == "MW1" ? (0L, 40L) : (20L, 20L), (StoneOf(m).Build, StoneOf(m).Holds));

        SplitCheckResult result = SplitReportCheck.Check(mutant, Control(), 120);
        if (!fails)
        {
            Assert.True(result.Passed, string.Join(" | ", result.Failures));
            return;
        }
        string who = shape == "MW1" ? "s0:0" : "s2:0";
        string covered = shape == "MW1" ? "timber budget 80 >= need 40, stone budget 40 >= need 20" : "timber budget 40 >= need 40, stone budget 20 >= need 20";
        Assert.Equal([$"P12: step 130->131 (polity 1): head {who} (enqueued 130) is reported limited by {limit}, but its budget covered its whole " +
                      $"need of every good ({covered}) and it drew 0 and set aside 60: the resolver binds a head on materials only where some " +
                      "input's budget falls short of what it still needs"], result.Failures);
    }

    /// <summary>T3 round 3 (finding T3-L1-T3-V1L1-N1), AIM ON REAL DATA: the fixtures' materials-limited heads — 9 in a.jsonl,
    /// 3 in k.jsonl, 4 in its twin — are each short of some good (budget below need), so the clause holds on them; and the
    /// fixtures pass P12 (TheRealReports_PassP11AndP12_…).</summary>
    [Fact]
    public void P12_TheRealMaterialsLimitedHeads_AreEachShortOfSomeGood()
    {
        foreach ((SplitReportRow[] rows, int expected) in new[] { (A(), 9), (K(), 3), (KNo12(), 4) })
        {
            SplitHeadFigures[] limited = rows.SelectMany(r => r.Heads).Where(h => h.Limit == "Materials").ToArray();
            Assert.Equal(expected, limited.Length);
            Assert.All(limited, h => Assert.Contains(h.Goods, g => g.Budget is { } b && b < g.Need));
        }
    }

    // ================================================================== PT: the kind-12 order changed spending in its own step

    /// <summary>
    /// THE OLD LEG'S SHAPE, on real data: the fixture pair a.jsonl / control.jsonl IS a kind-12 pair — the control is the same
    /// run without the b = 30 order stamped 116 — but nothing draws in the order's delivery step (the granaries are ordered
    /// at 117), so the order cannot have changed spending there: PT fails, and says so. This is why the scenario's b = 70
    /// order now shares its batch with three granaries.
    /// </summary>
    [Fact]
    public void PT_AnOrderDeliveredWhereNoHeadDraws_ProvesNothing()
    {
        Assert.Empty(RowAt(A(), 116).Heads);
        SplitCheckResult result = SplitReportCheck.Check(A(), twin: new SplitTwin(Control(), 116, 30));
        Assert.Equal("PT: the order changed b from 100 to 30 at step 116->117, but building work drew the same of every good as in the " +
                     "twin: the new split did not change spending in its delivery step",
            result.Failures.Single(f => f.StartsWith("PT:", StringComparison.Ordinal)));
    }

    /// <summary>
    /// CM04b'S SHAPE: the split applied to spending ONE STEP LATE — construction reads PREV's split rows, so in step 136 → 137
    /// building work spends under b = 30 while the order's row says 70. The report's step-136 row is the real one with its
    /// MEASURED columns (build, holds, road) as the twin's (what the b = 30 walk spent) and everything the probe computes
    /// (b, the split, the forecast, the heads) as the b = 70 row: P1 sees the step not doing what it forecast, P2 the grant
    /// unspent, and PT the draw unchanged by the order. The row so built is field for field the real CM04b leg's report row
    /// (t3-out/r2/legs/CM04b/alloc-b.rep).
    /// </summary>
    [Fact]
    public void PT_ASplitAppliedAStepLate_FailsPT_AndP1()
    {
        SplitReportRow twin136 = RowAt(KNo12(), Seventy);
        SplitReportRow[] late = AtRow(K(), Seventy, r => r with
        {
            Goods = r.Goods.Select(g => twin136.GoodOf(g.Id) is { } t ? g with { Build = t.Build, Holds = t.Holds, Road = t.Road } : g).ToArray(),
        });
        SplitCheckResult result = SplitReportCheck.Check(late, expectHoldAt: 131, twin: Twin(KNo12()));
        Assert.Equal(new[] { "P1", "P2", "PT" }, result.Failures.Select(f => f[..f.IndexOf(':', StringComparison.Ordinal)]).ToArray());
        Assert.StartsWith("P1: step 136->137 (polity 1) timber: building work drew 42 but the split forecast 102", result.Failures[0]);
        Assert.Equal("PT: the order changed b from 30 to 70 at step 136->137, but building work drew the same of every good as in the " +
                     "twin: the new split did not change spending in its delivery step", result.Failures[2]);
    }

    [Theory]
    [InlineData("no step at T runs at B")]
    [InlineData("the order turn is not the delivery step")]
    [InlineData("the twin has no step at T")]
    [InlineData("the twin ran B too")]
    [InlineData("the twin differs before T")]
    [InlineData("the twin's claim differs at T")]
    [InlineData("the twin's own step is not the rule's")]
    public void PT_ATwinThatIsNotTheRunWithoutTheOrder_Fails(string clause)
    {
        SplitReportRow[] k = K(), twin = KNo12();
        SplitTwin pair = clause switch
        {
            "no step at T runs at B" => new SplitTwin(twin, Seventy, 60),
            "the order turn is not the delivery step" => new SplitTwin(twin, Seventy - 1, 70),
            "the twin has no step at T" => Twin(twin.Where(r => r.Turn != Seventy).ToArray()),
            "the twin ran B too" => Twin(k),
            "the twin differs before T" => Twin(AtRow(twin, 133, r => Good(r, "stone", g => g with { S = g.S + 1 }))),
            "the twin's claim differs at T" => Twin(AtRow(twin, Seventy, r => Good(r, "stone", g => g with { Db = 59 }))),
            "the twin's own step is not the rule's" => Twin(AtRow(twin, Seventy, r => Good(r, "stone", g => g with { Eb = 22, Er = 51 }))),
            _ => throw new ArgumentOutOfRangeException(nameof(clause)),
        };
        string pt = SplitReportCheck.Check(k, expectHoldAt: 131, twin: pair).Failures.Single();
        Assert.Equal(clause switch
        {
            "no step at T runs at B" => "PT: the report has no step 136->137 run at b = 60: the order stamped 136 did not set the split in its delivery step",
            "the order turn is not the delivery step" => "PT: the report has no step 135->136 run at b = 70: the order stamped 135 did not set the split in its delivery step",
            "the twin has no step at T" => "PT: the twin has no step 136->137 for polity 1, the step that delivers the order",
            "the twin ran B too" => "PT: the twin ran b = 70 at step 136->137 too: it is not the run without the order",
            "the twin differs before T" => "PT: the twin and the report differ at step 133->134 (polity 1), before the order's step 136",
            "the twin's claim differs at T" => "PT: at step 136->137 the twin's stone differs before the split applies (the report's S 73, D_b 60, D_r 73): " +
                                              "the twins were not delivered the order in the same world",
            _ => "PT: the twin's step 136->137 (b = 30) breaks P11: step 136->137 (polity 1) stone: E_b = 22, but floor(S x b / 100) = floor(73 x 30 / 100) = 21",
        }, pt);
    }

    // ================================================================== the verb, as the ci.yml step runs it

    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);

    [Fact]
    public void SplitCheck_Twin_ExitsZeroOnTheRealPair_OneOnALateSplit_AndRefusesHalfAnOption()
    {
        (int exit, string stdout, string stderr) = CliProcess.Run(Limit, "split-check", SplitReportCheckTests.FixturePath("k.jsonl"),
            "--twin", SplitReportCheckTests.FixturePath("k-no12.jsonl"), "--order-at", "136:70");
        Assert.True(exit == 0, stdout + stderr);
        Assert.Equal("PASS", CliProcess.Lines(stdout)[^1]);
        Assert.Contains("the kind-12 order at 136 (PT): b 30 -> 70 in step 136->137; building work drew, against the twin without it: stone 51 vs 21", stdout);

        string dir = Path.Combine(Path.GetTempPath(), "civsim-splittwin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            SplitReportRow twin136 = RowAt(KNo12(), Seventy);
            string late = Path.Combine(dir, "late.jsonl");
            using (FileStream f = File.Create(late))
                foreach (SplitReportRow row in AtRow(K(), Seventy, r => r with
                {
                    Goods = r.Goods.Select(g => twin136.GoodOf(g.Id) is { } t ? g with { Build = t.Build, Holds = t.Holds, Road = t.Road } : g).ToArray(),
                })) SplitReport.Write(f, row);
            (exit, stdout, stderr) = CliProcess.Run(Limit, "split-check", late, "--twin", SplitReportCheckTests.FixturePath("k-no12.jsonl"), "--order-at", "136:70");
            Assert.Equal(1, exit);
            Assert.Contains("FAIL PT: the order changed b from 30 to 70 at step 136->137", stdout);
            Assert.Equal("split-check: FAILED - P1, P2, PT", CliProcess.Lines(stderr).Single());

            (exit, _, stderr) = CliProcess.Run(Limit, "split-check", SplitReportCheckTests.FixturePath("k.jsonl"), "--twin", late);
            Assert.Equal(1, exit);
            Assert.StartsWith("error: --twin TWIN and --order-at T:B go together", CliProcess.Lines(stderr)[0]);
            (exit, _, stderr) = CliProcess.Run(Limit, "split-check", SplitReportCheckTests.FixturePath("k.jsonl"), "--twin", late, "--order-at", "136:x");
            Assert.Equal(1, exit);
            Assert.StartsWith("error: --order-at takes T:B", CliProcess.Lines(stderr)[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
