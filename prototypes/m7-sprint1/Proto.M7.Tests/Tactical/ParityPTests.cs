using System.Security.Cryptography;
using System.Text;
using Proto.M7.Fixed;
using Proto.M7.Spatial;
using Proto.M7.Tactical;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// [P] Lane P: the pre-committed parity acceptance rule P1 (hand-computed values and exact boundaries), the structure of the
/// lane-P battery (deployment legality, exact mirror images, disjoint seed sets, held-out isolation of the calibration).
/// </summary>
public class ParityPTests
{
    // ------------------------------------------------------------------ rule P1: bands

    [Fact]
    public void WinBandHandComputed()
    {
        // p~ = 81/162 = 1/2; 3.6 * sqrt(1/4 * 2/80) = 0.28460 -> 285 permille (num/den = 80,999.7 -> ceil 81,000 -> ceil sqrt 285)
        Assert.Equal(285, ParityCriteriaP1.WinBand(40, 80, 80));
        // p~ = 160.5/161; 3.6 * sqrt(p(1-p) 2/80) = 44.6 permille < the substantive 100
        Assert.Equal(100, ParityCriteriaP1.WinBand(80, 80, 80));
        Assert.Equal(100, ParityCriteriaP1.WinBand(0, 80, 80));
        // n = 160 each: 3.6 * sqrt(1/4 * 2/160) = 0.20125 -> 202 (ceil of the exact integer chain)
        Assert.Equal(202, ParityCriteriaP1.WinBand(80, 160, 160));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParityCriteriaP1.WinBand(81, 80, 80));
    }

    [Fact]
    public void MeanSamplingFloorHandComputed()
    {
        // sample {0, 1000}: s^2 = 500,000; 3.6 * sqrt(500,000 * (1/2 + 1/2)) = 2,545.58 -> 2,546
        Assert.Equal(2546, ParityCriteriaP1.MeanSamplingFloor(1000, 1_000_000, 2, 2));
        // a constant reference has no spread: 0 (the substantive band then decides)
        Assert.Equal(0, ParityCriteriaP1.MeanSamplingFloor(400 * 80, 400L * 400 * 80, 80, 80));
        Assert.Equal(0, ParityCriteriaP1.MeanSamplingFloor(400, 160_000, 1, 80));
        // {300, 400} x 40 each (n = 80): s^2 = 2,500 * 80/79; 3.6 * sqrt(2,531.6 * 2/80) = 28.64 -> 29
        long sum = 40 * 300 + 40 * 400, sq = 40L * 90_000 + 40L * 160_000;
        Assert.Equal(29, ParityCriteriaP1.MeanSamplingFloor(sum, sq, 80, 80));
    }

    [Fact]
    public void MeanComparisonIsExactAtTheBoundary()
    {
        // means 350 and 380 (n 80 / 80): gap exactly 30 -> within a band of 30, not within 29
        Assert.True(ParityCriteriaP1.MeanWithin(380 * 80, 80, 350 * 80, 80, 30));
        Assert.False(ParityCriteriaP1.MeanWithin(380 * 80, 80, 350 * 80, 80, 29));
        // unequal n: 380.0125 vs 350 -> gap 30.0125 > 30 (no rounding helps it)
        Assert.False(ParityCriteriaP1.MeanWithin(380 * 80 + 1, 80, 350 * 160, 160, 30));
        Assert.Equal(30, ParityCriteriaP1.MeanGap(380 * 80 + 1, 80, 350 * 160, 160));
    }

    private static POutcome O(int w, long ca, long cb, long pulses, long ms) => new(w, ca, cb, pulses, ms, BattleEndReason.SideEliminated);

    private static PStats Sample(int winA, int winB, int draw, long ca, long cb, long pulses, long ms)
    {
        var l = new List<POutcome>();
        for (int i = 0; i < winA; i++) l.Add(O(0, ca, cb, pulses, ms));
        for (int i = 0; i < winB; i++) l.Add(O(1, ca, cb, pulses, ms));
        for (int i = 0; i < draw; i++) l.Add(O(-1, ca, cb, pulses, ms));
        return PStats.Of(l);
    }

    [Fact]
    public void CheckFlagsEveryCriterionSeparatelyAtItsBoundary()
    {
        var r = Sample(80, 0, 0, 0, 400, 3, 200_000);          // decisive reference: A always wins, constant outcomes
        Assert.True(ParityCriteriaP1.Check(r, r, 60, 50, 2_000).Pass);
        // V: modal victor
        var flip = Sample(0, 80, 0, 0, 400, 3, 200_000);
        var cf = ParityCriteriaP1.Check(r, flip, 60, 50, 2_000);
        Assert.True(cf.ModalRequired); Assert.False(cf.ModalOk); Assert.False(cf.WinOk); Assert.False(cf.Pass);
        // W: 72/80 = 900 permille A wins: gap 100 = the substantive band -> ok; 71/80: gap 112.5 -> fail
        Assert.True(ParityCriteriaP1.Check(r, Sample(72, 8, 0, 0, 400, 3, 200_000), 60, 50, 2_000).WinOk);
        Assert.False(ParityCriteriaP1.Check(r, Sample(71, 9, 0, 0, 400, 3, 200_000), 60, 50, 2_000).WinOk);
        // CA / CB: substantive 30 permille (one entity of 60 = 17, of 50 = 20)
        Assert.True(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 30, 400, 3, 200_000), 60, 50, 2_000).CasAOk);
        Assert.False(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 31, 400, 3, 200_000), 60, 50, 2_000).CasAOk);
        Assert.True(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 0, 370, 3, 200_000), 60, 50, 2_000).CasBOk);
        Assert.False(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 0, 369, 3, 200_000), 60, 50, 2_000).CasBOk);
        // resolution floor: a role of 10 entities -> one entity = 100 permille
        Assert.True(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 100, 400, 3, 200_000), 10, 50, 2_000).CasAOk);
        // D: 15 % of 200 s = 30 s
        Assert.True(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 0, 400, 3, 230_000), 60, 50, 2_000).DurOk);
        Assert.False(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 0, 400, 3, 230_001), 60, 50, 2_000).DurOk);
        // P: median pulses within 1
        Assert.True(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 0, 400, 4, 200_000), 60, 50, 2_000).PulsesOk);
        Assert.False(ParityCriteriaP1.Check(r, Sample(80, 0, 0, 0, 400, 5, 200_000), 60, 50, 2_000).PulsesOk);
    }

    [Fact]
    public void ModalRuleBindsOnlyAtTheThreshold()
    {
        // 48/80 = 600 permille: required; 47/80 = 587: not required
        var at = Sample(48, 32, 0, 0, 400, 3, 200_000);
        var below = Sample(47, 33, 0, 0, 400, 3, 200_000);
        var cand = Sample(30, 50, 0, 0, 400, 3, 200_000);
        Assert.True(ParityCriteriaP1.Check(at, cand, 50, 50, 2_000).ModalRequired);
        Assert.False(ParityCriteriaP1.Check(at, cand, 50, 50, 2_000).ModalOk);
        Assert.False(ParityCriteriaP1.Check(below, cand, 50, 50, 2_000).ModalRequired);
        Assert.True(ParityCriteriaP1.Check(below, cand, 50, 50, 2_000).ModalOk);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void StatsOfATieDenseSampleIgnoreInputOrder(ulong perm)
    {
        var v = new[]
        {
            O(0, 300, 400, 3, 168_000), O(1, 400, 300, 3, 168_000), O(-1, 400, 400, 4, 200_000), O(0, 300, 400, 3, 168_000),
            O(1, 400, 300, 9, 490_000), O(0, 280, 400, 3, 168_000), O(1, 400, 300, 3, 168_000), O(-1, 400, 400, 4, 200_000),
        };
        var p = TestSupport.Permutation(v.Length, perm);
        var a = PStats.Of(v); var b = PStats.Of(p.Select(i => v[i]).ToArray());
        Assert.Equal(a, b);
        Assert.Equal(0, a.Modal);                 // 3 / 3 / 2: tie A = B broken toward A
        Assert.Equal(375, a.ModalPermille);
        Assert.Equal(2880, a.CasASum);
    }

    [Fact]
    public void OutcomeHandComputedFromAResult()
    {
        var r = new BattleResult(ResolverKind.Auto, BattleEndReason.SideEliminated, 1, 0, 4, 206_000, 0, 0, "", "", new[]
        {
            new FormationResult(1, 0, Domain.Land, 3, 50, 41, 9, 0, 41, 0, 0, 0, 900, 0, 0, 0),
            new FormationResult(2, 0, Domain.Land, 4, 60, 59, 1, 0, 59, 0, 0, 0, 900, 0, 0, 0),
            new FormationResult(3, 1, Domain.Land, 3, 50, 30, 18, 2, 0, 30, 30, 0, 200, 0, 0, 0),
        });
        var o = POutcome.Of(r, 6_000);
        Assert.Equal(new POutcome(0, 90, 400, 4, 200_000, BattleEndReason.SideEliminated), o);
    }

    // ------------------------------------------------------------------ battery structure

    [Fact]
    public void SeedSetsAreDisjointAndSizedAsCommitted()
    {
        Assert.True(ParityBatteryP.LastTuningSeed < ParityBatteryP.FirstAcceptanceSeed);
        Assert.True(ParityBatteryP.LastAcceptanceSeed < ParityBatteryP.FirstControlSeed);
        Assert.Equal((ulong)ParityCriteriaP1.MinAcceptanceN, ParityBatteryP.LastAcceptanceSeed - ParityBatteryP.FirstAcceptanceSeed + 1);
        Assert.Equal((ulong)ParityCriteriaP1.MinAcceptanceN, ParityBatteryP.LastControlSeed - ParityBatteryP.FirstControlSeed + 1);
        Assert.Equal(20, ParityBatteryP.TuningScenarios().Length);
        Assert.Equal(9, ParityBatteryP.HeldOutScenarios().Length);
        Assert.Equal(1600, ParityBatteryP.TuningCases().Length);
        foreach (var c in ParityBatteryP.TuningCases()) Assert.True(ParityBatteryP.IsTuningCase(c));
        foreach (var id in ParityBatteryP.AllScenarios())
        {
            Assert.False(ParityBatteryP.IsTuningCase(new PCase(id, ParityBatteryP.FirstAcceptanceSeed)));
            Assert.False(ParityBatteryP.IsTuningCase(new PCase(id, ParityBatteryP.FirstControlSeed)));
        }
        foreach (var id in ParityBatteryP.HeldOutScenarios()) Assert.False(ParityBatteryP.IsTuningCase(new PCase(id, 1)));
        // orientation alternates with the seed: half of every seed set is mirrored
        Assert.False(ParityBatteryP.Mirrored(5001)); Assert.True(ParityBatteryP.Mirrored(5002));
    }

    [Fact]
    public void EveryFamilyHasHeldOutScenarios()
    {
        foreach (ParityFamily f in new[] { ParityFamily.OpenConditions, ParityFamily.Composition, ParityFamily.Concealment, ParityFamily.Terrain })
        {
            Assert.Contains(ParityBatteryP.All, s => s.Family == f && s.HeldOut);
            Assert.Contains(ParityBatteryP.All, s => s.Family == f && !s.HeldOut);
        }
        Assert.Equal(ParityBatteryP.All.Count, ParityBatteryP.All.Select(s => s.Id).Distinct().Count());
        Assert.Equal(ParityBatteryP.All.Count, ParityBatteryP.All.Select(s => s.Name).Distinct().Count());
    }

    [Fact]
    public void CalibrationRefusesEveryNonTuningCase()
    {
        var tac = new[] { O(0, 0, 400, 3, 200_000) };
        Assert.Throws<InvalidOperationException>(() => ParityCalibrationP.TotalLoss(new AutoCoefficients(), new[] { new PCase(PScenario.EqualInfantry, ParityBatteryP.FirstAcceptanceSeed) }, tac));
        Assert.Throws<InvalidOperationException>(() => ParityCalibrationP.TotalLoss(new AutoCoefficients(), new[] { new PCase(PScenario.HighRidge, 1) }, tac));
        Assert.Throws<InvalidOperationException>(() => ParityCalibrationP.TotalLoss(new AutoCoefficients(), new[] { new PCase(PScenario.EqualInfantry, 81) }, tac));
    }

    [Fact]
    public void EveryNewScenarioIsDeploymentLegalInBothOrientations()
    {
        foreach (var id in ParityBatteryP.AllScenarios())
            foreach (ulong seed in new ulong[] { 1, 2 })
            {
                var s = ParityBatteryP.Build(id, seed);
                Assert.True(DeploymentZones.Check(s).Accepted, $"{id} seed {seed}: {DeploymentZones.Check(s)}");
                if ((int)id > 10) Assert.Equal(DeploymentRule.Thirds, s.Deployment);
            }
    }

    [Fact]
    public void MirroredSeedBuildsTheExactMirrorImage()
    {
        long Wmm = TacticalScenarios.FieldW * TacticalScenarios.CellMm;
        foreach (var id in ParityBatteryP.AllScenarios())
        {
            var a = ParityBatteryP.Build(id, 1);
            var m = ParityBatteryP.Build(id, 2);
            Assert.Equal(a.Formations.Length, m.Formations.Length);
            for (int i = 0; i < a.Formations.Length; i++)
            {
                Assert.Equal(Wmm - a.Formations[i].Anchor.X, m.Formations[i].Anchor.X);
                Assert.Equal(a.Formations[i].Anchor.Y, m.Formations[i].Anchor.Y);
                Assert.Equal(a.Formations[i].Posture, m.Formations[i].Posture);
                Assert.Equal(a.Formations[i].Count, m.Formations[i].Count);
            }
            for (long y = 0; y < TacticalScenarios.FieldH; y++)
                for (long x = 0; x < TacticalScenarios.FieldW; x++)
                {
                    Assert.Equal(a.Grid.ClassAt(x, y), m.Grid.ClassAt(TacticalScenarios.MirrorColumn(x), y));
                    Assert.Equal(a.Grid.HeightAt(x, y), m.Grid.HeightAt(TacticalScenarios.MirrorColumn(x), y));
                }
            for (int side = 0; side < 2; side++)
            {
                Assert.NotEqual(a.Sides[side].Exit, m.Sides[side].Exit);
                Assert.Equal(a.Sides[side].Objectives.Length, m.Sides[side].Objectives.Length);
                for (int k = 0; k < a.Sides[side].Objectives.Length; k++)
                    Assert.Equal(Wmm - a.Sides[side].Objectives[k].X, m.Sides[side].Objectives[k].X);
            }
        }
    }

    [Fact]
    public void DefenderFrontRankStandsJustInsideItsThirdSoAnAttackerInContactIsOneCellOut()
    {
        foreach (var id in new[] { PScenario.HillCrest, PScenario.FordCrossing, PScenario.MarshFront, PScenario.Defile, PScenario.ArchersOnHill, PScenario.HoldLineOpen,
                                   PScenario.HighRidge, PScenario.NarrowFordArchers, PScenario.UrbanStreet })
        {
            var s = ParityBatteryP.Build(id, 1);
            var d = s.Formations.Single(f => f.Side == 1);
            Assert.Equal(Posture.Defend, d.Posture);
            Assert.Equal(new Vec2L(640_400, 330_000), d.Anchor);
            Assert.Equal(32, s.Grid.CellOf(d.Anchor).X);
            Assert.Equal(31, s.Grid.CellOf(d.Anchor - new Vec2L(1_200, 0)).X);   // attacker in contact (1,200 mm) stands one cell out
        }
    }
    // ------------------------------------------------------------------ reduced deterministic subset (goldens)

    private static readonly PScenario[] Subset =
        { PScenario.EqualInfantry, PScenario.HillCrest, PScenario.FordCrossing, PScenario.SniperScreen, PScenario.ForestHoldArchers };

    private static string Digest(PCase[] cases, POutcome[] o)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cases.Length; i++)
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"{(int)cases[i].Scenario},{cases[i].Seed},{o[i].Winner},{o[i].CasA},{o[i].CasB},{o[i].Pulses},{o[i].DurationMs},{(int)o[i].Reason};");
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(sb.ToString())));
    }

    /// <summary>GOLDENS (Linux x64, .NET 10, measured 2026-11-06, lane P): digests of a reduced lane-P subset (5 scenarios x
    /// TUNING seeds 1-4) under the TacticalResolver (default rules) and the AutoResolver's FROZEN model-2 defaults. They move
    /// whenever a tactical rule, a model-2 coefficient or a battery geometry changes (re-pin with attribution). The
    /// semantic assertions carry the meaning; the digests pin reproducibility. Cross-platform equality is UNTESTED (CR-013).</summary>
    public const string GoldenTacticalSubsetP = "F8E2634E3FAEBA2CE2FD8DAD4B4D0FF2B472144E6AAE33EE55146D308C05837E";
    public const string GoldenAutoSubsetP = "D10B68EB25D86447BFC86A6A0BDBB0EBE446EC196473EF6DDA2616A1AD1A6415";

    [Fact]
    public void ReducedLanePSubsetIsDeterministicAndHasTheExpectedShape()
    {
        var cases = ParityBatteryP.Cases(Subset, 1, 4);
        var tac = cases.Select(ParityBatteryP.RunTactical).ToArray();
        var auto = cases.Select(c => ParityBatteryP.RunAuto(c, new AutoCoefficients())).ToArray();
        Assert.Equal(auto, cases.Select(c => ParityBatteryP.RunAuto(c, new AutoCoefficients())).ToArray());
        Assert.Equal(20, cases.Length);
        for (int i = 0; i < cases.Length; i++)
        {
            if (cases[i].Scenario == PScenario.ForestHoldArchers)
            {
                // archers vs a line holding in the forest: the archers win without losses in both resolvers
                Assert.Equal(0, tac[i].Winner); Assert.Equal(0, auto[i].Winner);
                Assert.Equal(0, auto[i].CasA);
            }
            if (cases[i].Scenario == PScenario.SniperScreen) { Assert.Equal(0, tac[i].Winner); Assert.Equal(0, auto[i].Winner); }
            Assert.True(tac[i].DurationMs > 0 && auto[i].DurationMs > 0);
        }
        Assert.Equal(GoldenTacticalSubsetP, Digest(cases, tac));
        Assert.Equal(GoldenAutoSubsetP, Digest(cases, auto));
    }

    // ------------------------------------------------------------------ [P fix r1] P-V6 and P-V9

    /// <summary>[P fix r1, P-V6] The win-rate band rule P1 actually applies at the acceptance sample sizes (n_R = n_C = 160): the
    /// sampling floor, not the 100 ‰ substantive band, binds for every reference rate from 11 / 160 to 149 / 160 — 175 ‰ at a
    /// 25 % or 75 % rate and 202 ‰ at 50 % (hand: 3.6 x sqrt(p(1-p) x 2/160) with p~ = (k + 1/2)/161). The substantive 100 ‰ binds
    /// only for k &lt;= 10 or k &gt;= 150 (rates below ~6.6 % or above ~93 %). A tuning scenario's self-check against its n = 80
    /// tuning sample uses the wider 247 ‰ (n_C = 80). These are the frozen rule's numbers (H-P.md §2), stated, not changed.</summary>
    [Fact]
    public void WinBandsAtTheAcceptanceSampleSizesAreTheSamplingFloors()
    {
        Assert.Equal(100, ParityCriteriaP1.WinBand(10, 160, 160));
        Assert.Equal(104, ParityCriteriaP1.WinBand(11, 160, 160));
        Assert.Equal(123, ParityCriteriaP1.WinBand(16, 160, 160));
        Assert.Equal(175, ParityCriteriaP1.WinBand(40, 160, 160));
        Assert.Equal(202, ParityCriteriaP1.WinBand(80, 160, 160));
        Assert.Equal(175, ParityCriteriaP1.WinBand(120, 160, 160));
        Assert.Equal(100, ParityCriteriaP1.WinBand(150, 160, 160));
        Assert.Equal(247, ParityCriteriaP1.WinBand(80, 160, 80));
        for (int k = 11; k <= 149; k++) Assert.True(ParityCriteriaP1.WinBand(k, 160, 160) > ParityCriteriaP1.WinSubstantivePermille, $"k = {k}");
    }

    /// <summary>[P fix r1, P-V9] The tactical-outcome CSV cache (re-read to compute the acceptance verdict and the battery hash)
    /// round-trips a POPULATED table exactly: every battery scenario in both orientations (model-2 outcomes) plus one row per
    /// end reason with winner -1; exact text and length of a hand row; bit-exact re-serialisation; digest equality.</summary>
    [Fact]
    public void TacticalOutcomeCsvRoundTripsAPopulatedTableExactly()
    {
        var cases = new List<PCase>(); var outs = new List<POutcome>();
        foreach (var d in ParityBatteryP.All)
            for (ulong seed = 1; seed <= 2; seed++) { var c = new PCase(d.Id, seed); cases.Add(c); outs.Add(ParityBatteryP.RunAuto(c, new AutoCoefficients())); }
        foreach (var reason in Enum.GetValues<BattleEndReason>()) { cases.Add(new PCase(PScenario.HighRidge, 9160)); outs.Add(new POutcome(-1, 0, 1000, 123, 9_876_000, reason)); }
        Assert.Equal(2 * ParityBatteryP.All.Count + Enum.GetValues<BattleEndReason>().Length, cases.Count);
        Assert.Contains(outs, o => o.Winner == 0); Assert.Contains(outs, o => o.Winner == 1); Assert.Contains(outs, o => o.CasA > 0 && o.CasB > 0);

        const string hand = "107,9160,-1,0,1000,123,9876000,5,7,0.0";
        Assert.Equal(hand, ParityPCsv.Row(new PCase(PScenario.HighRidge, 9160), new POutcome(-1, 0, 1000, 123, 9_876_000, BattleEndReason.Standoff), 7, "0.0"));
        Assert.Equal(38, hand.Length);

        string Write(IReadOnlyList<PCase> c, IReadOnlyList<POutcome> o)
        {
            var sb = new StringBuilder(ParityPCsv.Header).Append('\n');
            for (int i = 0; i < c.Count; i++) sb.Append(ParityPCsv.Row(c[i], o[i], 1000 + i, "12.5")).Append('\n');
            return sb.ToString();
        }
        string text = Write(cases, outs);
        var (pc, po) = ParityPCsv.ParseAll(text.Split('\n'));
        Assert.Equal(cases, pc);
        Assert.Equal(outs, po);
        Assert.Equal(text, Write(pc, po));                                                    // bit-exact re-serialisation
        Assert.Equal(ParityPCsv.Digest(cases, outs), ParityPCsv.Digest(pc, po));
        Assert.Throws<FormatException>(() => ParityPCsv.ParseAll(new[] { "scenario,seed" }));  // wrong header refused
    }
}
