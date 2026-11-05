using System.Security.Cryptography;
using System.Text;
using Proto.M7.Tactical;

namespace Proto.M7.Tests.Tactical;

/// <summary>
/// Slice E: integer statistics, tolerance derivation, the calibration procedure, and the REDUCED deterministic battery subset
/// (the full battery — 10 scenarios x 40 seeds x 2 orientations — is the Bench `parity` mode, reports/parity-report.md).
/// </summary>
public class ParityBatteryTests
{
    // ------------------------------------------------------------------ statistics

    [Fact]
    public void NearestRankQuantilesHandComputed()
    {
        var d = Dist.Of(new long[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 });     // input order irrelevant
        Assert.Equal(new Dist(1, 3, 5, 8, 9), d);                          // ranks ceil(q n) - 1: 0, 2, 4, 7, 8
        Assert.Equal(3, d.IqrHalfCeil);                                     // ceil((8 - 3) / 2)
        Assert.Equal(new Dist(7, 7, 7, 7, 7), Dist.Of(new long[] { 7 }));
        Assert.Equal(2, Dist.Of(new long[] { 1, 2, 3, 4 }).P50);            // even n: the LOWER middle value
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void QuantilesOfATieDenseSampleIgnoreInputOrder(ulong perm)
    {
        long[] v = { 36, 36, 36, 38, 38, 0, 0, 36, 40, 36, 36, 38, 0, 36, 40, 40 };
        var p = TestSupport.Permutation(v.Length, perm);
        Assert.Equal(Dist.Of(v), Dist.Of(p.Select(i => v[i]).ToArray()));
        Assert.Equal(new Dist(0, 36, 36, 38, 40), Dist.Of(v));
    }

    [Fact]
    public void ModalWinnerTieBreaksAThenBThenDraw()
    {
        ScenarioStats S(int a, int b, int d) => new(a + b + d, a, b, d, default, default, default, 50, 50);
        Assert.Equal(0, S(5, 5, 5).ModalWinner);
        Assert.Equal(1, S(4, 5, 5).ModalWinner);
        Assert.Equal(-1, S(4, 4, 5).ModalWinner);
        Assert.Equal(333, S(5, 5, 5).ModalPermille);
    }

    [Fact]
    public void OutcomeCasualtyFractionsArePersonnelLossPermilleByRole()
    {
        var r = new BattleResult(ResolverKind.Auto, BattleEndReason.SideEliminated, 1, 0, 3, 0, 0, 0, "", "", new[]
        {
            new FormationResult(1, 0, Domain.Land, 3, 50, 41, 9, 0, 41, 0, 0, 0, 900, 0, 0, 0),
            new FormationResult(2, 0, Domain.Land, 4, 60, 59, 1, 0, 59, 0, 0, 0, 900, 0, 0, 0),
            new FormationResult(3, 1, Domain.Land, 3, 50, 30, 18, 2, 0, 30, 30, 0, 200, 0, 0, 0),
        });
        var o = ParityOutcome.Of(r);
        Assert.Equal(0, o.Winner);
        Assert.Equal(90, o.CasA);      // (9 + 1) / 110 = 90.9 permille, floor
        Assert.Equal(400, o.CasB);     // (18 + 2) / 50
        Assert.Equal(3, o.Pulses);
    }

    // ------------------------------------------------------------------ tolerance

    [Fact]
    public void ToleranceDerivationHandComputed()
    {
        var tac = new ScenarioStats(40, 20, 20, 0, new Dist(20, 30, 34, 38, 40), new Dist(26, 28, 34, 36, 40), new Dist(3, 3, 3, 4, 9), 50, 50);
        var t = ParityTolerance.Derive(tac, 20);
        // p = (2*20+1)/(2*41) = 500 permille; ISqrt(18 * 500 * 500 / 20) = ISqrt(225000) = 474
        Assert.Equal(474, t.WinRate);
        // cas A: IQR 8 -> half 4; sampling floor ceil(39417*8 / ISqrt(2e9)=44721) = 8; resolution 2*20 = 40 -> 40
        Assert.Equal(8, ParityTolerance.MedianSamplingFloor(8, 20));
        Assert.Equal(40, t.CasA);
        Assert.Equal(40, t.CasB);
        Assert.Equal(1, t.Pulses);                 // IQR 1: half 1, floor ceil(39417/44721) = 1
        Assert.False(t.ModalRequired);             // modal 500 < 600
        var wide = new ScenarioStats(40, 40, 0, 0, new Dist(0, 100, 200, 300, 400), new Dist(300, 360, 380, 400, 420), new Dist(3, 3, 5, 9, 11), 50, 50);
        var tw = ParityTolerance.Derive(wide, 20);
        Assert.Equal(177, ParityTolerance.MedianSamplingFloor(200, 20));   // ceil(39417*200/44721) = ceil(176.3)
        Assert.Equal(177, tw.CasA);                // IQR 200: half 100 < sampling floor 177 (> resolution 40)
    }

    [Fact]
    public void ToleranceWidensWithFewerSeedsAndModalRuleBindsClearFavourites()
    {
        var tac = new ScenarioStats(80, 60, 20, 0, new Dist(20, 30, 34, 38, 40), new Dist(26, 28, 34, 36, 40), new Dist(3, 3, 3, 3, 3), 50, 50);
        Assert.True(ParityTolerance.Derive(tac, 5).WinRate > ParityTolerance.Derive(tac, 40).WinRate);
        var t = ParityTolerance.Derive(tac, 40);
        Assert.True(t.ModalRequired);
        Assert.Equal(0, t.ModalWinner);
        var flipped = tac with { WinA = 20, WinB = 60 };
        var c = ParityCheck.Of(tac, flipped, t);
        Assert.False(c.VictorOk);
        Assert.False(c.Pass);
        Assert.True(ParityCheck.Of(tac, tac, t).Pass);
    }

    [Fact]
    public void ParityCheckFlagsEachCriterionSeparately()
    {
        var tac = new ScenarioStats(40, 20, 20, 0, new Dist(20, 30, 340, 38, 40), new Dist(26, 28, 340, 36, 40), new Dist(3, 3, 3, 4, 9), 50, 50);
        var tol = new ParityTolerance(50, 40, 40, 1, 0, false);
        Assert.True(ParityCheck.Of(tac, tac with { CasA = tac.CasA with { P50 = 380 } }, tol).CasAOk);
        Assert.False(ParityCheck.Of(tac, tac with { CasA = tac.CasA with { P50 = 381 } }, tol).CasAOk);
        Assert.False(ParityCheck.Of(tac, tac with { CasB = tac.CasB with { P50 = 299 } }, tol).CasBOk);
        Assert.False(ParityCheck.Of(tac, tac with { Pulses = tac.Pulses with { P50 = 5 } }, tol).PulsesOk);
        Assert.True(ParityCheck.Of(tac, tac with { Pulses = tac.Pulses with { P50 = 4 } }, tol).PulsesOk);
        Assert.False(ParityCheck.Of(tac, tac with { WinA = 23, WinB = 17 }, tol).WinOk);   // 575 vs 500: gap 75 > 50
        Assert.True(ParityCheck.Of(tac, tac with { WinA = 22, WinB = 18 }, tol).WinOk);    // gap 50
    }

    // ------------------------------------------------------------------ the reduced deterministic battery subset

    private static readonly ParityScenarioId[] SubsetScenarios =
        { ParityScenarioId.EqualInfantry, ParityScenarioId.ArchersVsInfantryOpen, ParityScenarioId.LowSupply };

    private static (ParityCase[] Cases, ParityOutcome[] Tac, ParityOutcome[] Auto) RunSubset()
    {
        var cases = ParityBattery.Cases(SubsetScenarios, 1, 3);
        var tac = cases.Select(c => ParityBattery.RunTactical(c)).ToArray();
        var auto = cases.Select(c => ParityBattery.RunAuto(c, new AutoCoefficients())).ToArray();
        return (cases, tac, auto);
    }

    private static string Digest(ParityCase[] cases, ParityOutcome[] o)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cases.Length; i++)
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"{(int)cases[i].Scenario},{cases[i].Seed},{(cases[i].Mirrored ? 1 : 0)},{o[i].Winner},{o[i].CasA},{o[i].CasB},{o[i].Pulses},{(int)o[i].Reason};");
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(sb.ToString())));
    }

    /// <summary>
    /// GOLDENS (Linux x64, .NET 10, measured 2026-11-04): digests of the reduced subset (3 scenarios x seeds 1-3 x 2
    /// orientations) under each resolver with the shipped (fitted) coefficients and PLACEHOLDER tactical rules. They move
    /// whenever a tactical rule, an auto coefficient or the battery geometry changes (re-pin with attribution). Cross-platform
    /// equality is UNTESTED (O-17). The semantic assertions below carry the meaning; the digests only pin reproducibility.
    /// </summary>
    public const string GoldenTacticalSubset = "1148277797655A6E7BAF45B24E5F16880CA577B9813E81ED1F5E53E57B5A40E7";
    public const string GoldenAutoSubset = "87BB6323547EA6F2982DBA71A9B8DDEB93A8709C8292CA0D3BCE94017574DD77";

    [Fact]
    public void ReducedBatterySubsetIsDeterministicAndHasTheExpectedShape()
    {
        var (cases, tac, auto) = RunSubset();
        var (cases2, tac2, auto2) = RunSubset();
        Assert.Equal(cases, cases2);
        Assert.Equal(tac, tac2);
        Assert.Equal(auto, auto2);
        Assert.Equal(18, cases.Length);
        for (int i = 0; i < cases.Length; i++)
        {
            var c = cases[i];
            switch (c.Scenario)
            {
                case ParityScenarioId.ArchersVsInfantryOpen:
                    // archers 60 v HI 50: the archers win without losses under both resolvers (measured 80/80 tactical)
                    Assert.Equal(0, tac[i].Winner); Assert.Equal(0, auto[i].Winner);
                    Assert.Equal(0, tac[i].CasA); Assert.Equal(0, auto[i].CasA);
                    Assert.True(tac[i].CasB >= 300 && auto[i].CasB >= 300);   // routed after >= 30 % losses
                    break;
                case ParityScenarioId.LowSupply:
                    Assert.Equal(1, tac[i].Winner); Assert.Equal(1, auto[i].Winner);
                    Assert.True(tac[i].CasA > tac[i].CasB && auto[i].CasA > auto[i].CasB);
                    break;
            }
            Assert.True(tac[i].Pulses >= 1 && auto[i].Pulses >= 1);
        }
        Assert.Equal(GoldenTacticalSubset, Digest(cases, tac));
        Assert.Equal(GoldenAutoSubset, Digest(cases, auto));
    }

    [Fact]
    public void BatteryCasesCoverBothOrientationsOfEverySeed()
    {
        var cases = ParityBattery.Cases(ParityBattery.AllScenarioIds(), 1, 40);
        Assert.Equal(800, cases.Length);
        Assert.Equal(10, ParityScenarios.All.Count);
        foreach (var id in ParityBattery.AllScenarioIds())
            for (ulong s = 1; s <= 40; s++)
            {
                Assert.Contains(new ParityCase(id, s, false), cases);
                Assert.Contains(new ParityCase(id, s, true), cases);
            }
    }

    [Fact]
    public void MirroredSetupIsTheExactMirrorImage()
    {
        foreach (var id in ParityBattery.AllScenarioIds())
        {
            var a = ParityScenarios.Build(id, 1, false);
            var m = ParityScenarios.Build(id, 1, true);
            long W = TacticalScenarios.FieldW * TacticalScenarios.CellMm;
            for (int i = 0; i < a.Formations.Length; i++)
            {
                Assert.Equal(W - a.Formations[i].Anchor.X, m.Formations[i].Anchor.X);
                Assert.Equal(a.Formations[i].Anchor.Y, m.Formations[i].Anchor.Y);
            }
            Assert.NotEqual(a.Sides[0].Exit, m.Sides[0].Exit);
            for (int y = 0; y < TacticalScenarios.FieldH; y++)
                for (int x = 0; x < TacticalScenarios.FieldW; x++)
                    Assert.Equal(a.Grid.ClassAt(x, y), m.Grid.ClassAt(TacticalScenarios.FieldW - 1 - x, y));
        }
    }

    // ------------------------------------------------------------------ calibration

    [Fact]
    public void CalibrationIsDeterministicNeverWorsensTheLossAndUsesOnlyTheFitCases()
    {
        var (cases, tac, _) = RunSubset();
        var fit = Enumerable.Range(0, cases.Length).Where(i => cases[i].Seed <= 2).ToArray();
        var fc = fit.Select(i => cases[i]).ToArray();
        var ft = fit.Select(i => tac[i]).ToArray();
        var axes = AutoCalibration.DefaultAxes().Where(a => a.Name is "MeleeEngagePermille" or "FortunePermille").ToArray();
        var start = new AutoCoefficients { MeleeEngagePermille = 1000, FortunePermille = 0 };
        var r1 = AutoCalibration.Fit(start, axes, fc, ft, SubsetScenarios, 3);
        var r2 = AutoCalibration.Fit(start, axes, fc, ft, SubsetScenarios, 3);
        Assert.Equal(r1.Fitted, r2.Fitted);
        Assert.Equal(r1.Loss, r2.Loss);
        Assert.Equal(r1.Steps, r2.Steps);
        long startLoss = ParityBattery.TotalLoss(start, fc, ft, SubsetScenarios);
        Assert.True(r1.Loss <= startLoss);
        Assert.Equal(r1.Loss, ParityBattery.TotalLoss(r1.Fitted, fc, ft, SubsetScenarios));
        long prev = startLoss;
        foreach (var s in r1.Steps) { Assert.True(s.Loss < prev); prev = s.Loss; }   // every accepted move strictly improves
        // Fit-set isolation (verify-E E-V6: the former check filtered the corrupted entries out itself, so it could not fail).
        // The split under test is ParityBattery.FitHalf, the one the bench uses, applied to the FULL arrays.
        var bad = new ParityOutcome(-1, 999, 999, 99, BattleEndReason.BothDisengaged);
        var (sc, st) = ParityBattery.FitHalf(cases, tac, 2);
        Assert.Equal(fc, sc);                                   // exactly the fit cases, in case order (oracle: the index filter above)
        Assert.Equal(ft, st);
        Assert.All(sc, c => Assert.True(c.Seed <= 2));
        // (a) corrupting every HOLDOUT outcome changes neither the fit inputs nor the fit
        var corrupted = (ParityOutcome[])tac.Clone();
        for (int i = 0; i < cases.Length; i++) if (cases[i].Seed > 2) corrupted[i] = bad;
        var (hc, ht) = ParityBattery.FitHalf(cases, corrupted, 2);
        Assert.Equal(fc, hc);
        Assert.Equal(ft, ht);
        var r3 = AutoCalibration.Fit(start, axes, hc, ht, SubsetScenarios, 3);
        Assert.Equal(r1.Fitted, r3.Fitted);
        Assert.Equal(r1.Loss, r3.Loss);
        // (b) teeth: corrupting ONE fit outcome does reach the fit inputs and moves the loss (the check can fail)
        var fitCorrupted = (ParityOutcome[])tac.Clone();
        fitCorrupted[fit[0]] = bad;
        var (_, ft4) = ParityBattery.FitHalf(cases, fitCorrupted, 2);
        Assert.NotEqual(ft, ft4);
        Assert.NotEqual(r1.Loss, ParityBattery.TotalLoss(r1.Fitted, fc, ft4, SubsetScenarios));
    }

    [Fact]
    public void CalibrationKeepsTheCurrentValueOnTies()
    {
        var (cases, tac, _) = RunSubset();
        // An axis whose setter ignores the value: every candidate has the same loss -> no move is ever accepted.
        var inert = new CoefficientAxis("Inert", new long[] { 1, 2, 3 }, c => 0, (c, v) => c);
        var r = AutoCalibration.Fit(new AutoCoefficients(), new[] { inert }, cases, tac, SubsetScenarios, 2);
        Assert.Empty(r.Steps);
        Assert.Equal(new AutoCoefficients(), r.Fitted);
    }

    [Fact]
    public void LossIsZeroForIdenticalStatsAndCountsEveryComponent()
    {
        var t = new ScenarioStats(40, 20, 20, 0, new Dist(20, 30, 340, 38, 400), new Dist(26, 28, 340, 36, 400), new Dist(3, 3, 3, 4, 9), 50, 50);
        Assert.Equal(0, ParityBattery.Loss(t, t));
        Assert.Equal(2 * (25 + 25), ParityBattery.Loss(t, t with { WinA = 21, WinB = 19 }));
        Assert.Equal(10, ParityBattery.Loss(t, t with { CasA = t.CasA with { P50 = 350 } }));
        Assert.Equal(5, ParityBattery.Loss(t, t with { CasB = t.CasB with { P90 = 410 } }));
        Assert.Equal(100, ParityBattery.Loss(t, t with { Pulses = t.Pulses with { P50 = 4 } }));
    }
}
