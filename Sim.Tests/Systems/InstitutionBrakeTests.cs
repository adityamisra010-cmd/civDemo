using System.Globalization;
using System.Text;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit.Abstractions;
using static Sim.Tests.TestUtil.UniversityRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-033 D6 — THE D-021 BRAKE, MEASURED (docs/institutions-universities.md §7.2). D-021's paired-feedback
/// rule: the positive loop research → universities → cheaper research must ship with a negative loop that
/// STRENGTHENS WITH AMPLITUDE. The amplitude is the institutional investment (how many universities, how
/// mature); the brake is the labour they withdraw: staff = staffShare × adultsPerUniversity × M per university,
/// taken out of EVERY sector pool, which lowers labour-bound output and the published food surplus the
/// sustaining viability test reads, so maturity DECAYS where the institutions outgrow what the host can feed.
///
/// The rig hosts k = 0…K fully mature universities in ONE settlement of the dev world (after three ordinary
/// turns, so a real food surplus is published) and plays the PRODUCTION PIPELINE from twin states that differ
/// only by k — identical RNG draws, identical orders (none). The market is set to adults / 16, so every k ≤ K
/// = 14 holds it with headroom and the FOOD term alone is measured; the staff share is raised to 0.8 so each
/// instance withdraws 0.8 / 16 = 5 % of the host's adults. (At the canonical 0.05 a host at its market's
/// capacity loses at most 5 % IN TOTAL — the second test — so the food term binds only where the surplus is
/// already within that margin of the sustaining threshold; the brake's SHAPE does not depend on σ, its
/// magnitude scales with it.) Measured: (1) the labour withdrawn is exactly linear in k; (2) the host's
/// labour-bound output falls monotonically and its LOSS grows with k (the restoring force grows with the
/// amplitude); (3) the published food surplus falls monotonically with k; (4) a finite k* exists past which
/// the sustaining test fails and InstitutionsSystem decays maturity — while below it maturity holds.
/// </summary>
public class InstitutionBrakeTests(ITestOutputHelper output)
{
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);
    private const int K = 14;

    [Fact]
    public void Brake_LabourWithdrawn_OutputLost_AndSurplusFall_GrowWithTheNumberOfUniversities_UntilViabilityBinds()
    {
        // Three ordinary turns (no institution exists, so the institutions tuning cannot matter yet).
        WorldState start = Production(Cfg()).Run(WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg(), 42), 3);
        SettlementId s = start.Settlements[0].Id;
        long adults = BandViews.Adults(start.Buckets, s);
        long market = adults / 16;
        SimConfig cfg = Cfg(adultsPerUniversity: market, tune: u => u with { StaffShareAtMaturity = 0.8 });
        TurnExecutor exec = Production(cfg);
        double perInstance = 0.8 * market;
        int type = TypeKey(Research, "engineering_university");
        int project = InstitutionContent.ProjectOfType(cfg, type)!.Id;

        var staff = new double[K + 1];
        var labourOutput = new long[K + 1];
        var surplus = new double[K + 1];
        var maturityAfter = new double[K + 1];
        var table = new StringBuilder("k | staff | labour-bound output | food surplus ratio | maturity after one more step\n");
        for (int k = 0; k <= K; k++)
        {
            WorldState w = start.Clone();
            if (k > 0) w.Structures.Add(new StructureRow(s, project, k));
            for (int i = 0; i < k; i++) Found(w, Player, s, type, 1.0);
            staff[k] = InstitutionStaffing.Staff(w, cfg, s);

            WorldState one = exec.Step(w);            // production with staff(k) withdrawn
            WorldState two = exec.Step(one);          // ...and the surplus ClassMobility publishes from it
            labourOutput[k] = LabourBoundOutput(one, s, cfg);
            surplus[k] = InstitutionViability.FoodSurplusRatio(two, s);
            WorldState three = exec.Step(two);        // InstitutionsSystem reads that surplus
            maturityAfter[k] = k == 0 ? double.NaN : Mean(three, s);
            table.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{k} | {staff[k]:F1} | {labourOutput[k]} | {surplus[k]:F4} | {maturityAfter[k]:F4}"));
        }
        output.WriteLine(table.ToString());

        // (1) the force: labour withdrawn is exactly linear in the amplitude (5 % of the host per instance here).
        for (int k = 0; k <= K; k++) Assert.Equal(Math.Min(adults, k * perInstance), staff[k], 9);
        Assert.True(market * K <= adults, "the market must hold for every k, so the food term alone is measured");
        // (2) labour-bound output falls monotonically, and the loss grows with k (strengthens with amplitude).
        for (int k = 1; k <= K; k++)
        {
            Assert.True(labourOutput[k] < labourOutput[k - 1], $"output must fall as university {k} withdraws labour");
            Assert.True(labourOutput[0] - labourOutput[k] > labourOutput[0] - labourOutput[k - 1]);
        }
        // (3) the food surplus the viability test reads falls monotonically with k.
        for (int k = 1; k <= K; k++)
            Assert.True(surplus[k] <= surplus[k - 1], $"surplus must not rise with university {k}: {surplus[k]} > {surplus[k - 1]}");
        Assert.True(surplus[K] < surplus[0]);
        // (4) the brake BINDS at a finite amplitude: below k* the institutions hold full maturity, from k* on the
        // host cannot sustain them and their maturity decays (the loop is closed by real state, not a cap).
        double sustain = cfg.Institutions!.Universities.SustainingFoodSurplusRatio;
        int kStar = -1;
        for (int k = 1; k <= K && kStar < 0; k++) if (surplus[k] < sustain) kStar = k;
        Assert.True(kStar > 1, $"the brake must bind at a finite count above one university (k* = {kStar}; surpluses {string.Join(", ", surplus)})");
        for (int k = 1; k < kStar; k++) Assert.Equal(1.0, maturityAfter[k]);
        for (int k = kStar; k <= K; k++) Assert.True(maturityAfter[k] < 1.0, $"at k = {k} the host cannot feed its scholars and maturity must decay");
        output.WriteLine($"k* = {kStar} (adults {adults}, sustaining threshold {sustain})");
    }

    [Fact]
    public void AtTheCanonicalStaffShare_AHostAtItsMarketCapacity_LosesAtMostStaffShareOfItsAdults()
    {
        // Staffing is per INSTANCE (σ × adultsPerUniversity × M), so however large the host, n ≤ adults / A_u
        // instances (the market) employ at most σ of its adults — the bound host-proportional staffing broke.
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg(), 42);
        SettlementId s = w.Settlements[0].Id;
        long adults = BandViews.Adults(w.Buckets, s);
        SimConfig cfg = Cfg(adultsPerUniversity: adults / 7);
        double sigma = cfg.Institutions!.Universities.StaffShareAtMaturity;
        int type = TypeKey(Research, "engineering_university");
        long capacity = adults / cfg.Institutions.Universities.AdultsPerUniversity;   // the market's limit (7)
        for (long k = 0; k < capacity; k++) Found(w, Player, s, type, 1.0);
        double staff = InstitutionStaffing.Staff(w, cfg, s);
        Assert.Equal(sigma * cfg.Institutions.Universities.AdultsPerUniversity * capacity, staff, 9);
        Assert.True(staff <= sigma * adults, $"{staff} staff exceed {sigma} of {adults} adults");
        Assert.Equal(adults - staff, InstitutionStaffing.LabourAdults(w, cfg, s), 9);
    }

    /// <summary>The settlement's output of the deposit sectors this step — herding/fishing food and extraction,
    /// both linear in their labour pools (farming may be land-bound and so insensitive to labour).</summary>
    private static long LabourBoundOutput(WorldState w, SettlementId s, SimConfig cfg)
    {
        long total = 0;
        foreach (string good in new[] { "livestock", "fish", "timber", "stone", "clay", "fiber", "hides", "copper-ore", "tin-ore" })
        {
            int id = cfg.Goods!.IdOf(good);
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Settlement == s && w.GoodStocks[i].Good.Value == id) total += w.GoodStocks[i].LastProducedUnits;
        }
        return total;
    }

    private static double Mean(WorldState w, SettlementId s)
    {
        double sum = 0.0;
        int n = 0;
        for (int i = 0; i < w.Institutions.Count; i++)
            if (w.Institutions[i].Settlement == s) { sum += w.Institutions[i].Maturity; n++; }
        return n == 0 ? double.NaN : sum / n;
    }
}
