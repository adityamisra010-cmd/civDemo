using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// T3.6 R3 — the trade→stocks→prices→trade coupling under DRIVEN stress
/// (spec pre-commitment, docs/t3.6-spec.md). A two-settlement rig with a
/// forced, sustained production asymmetry: S0 publishes a constant cloth
/// production signal, S1 a constant consumption-demand signal, so the price
/// solver is pushed apart every turn while trade pulls stocks (and therefore
/// stockRelease, and therefore prices) back together — the D-021 positive
/// loop at its maximum plausible drive. The oscillation detector (proven in
/// OscillationDetectorTests BEFORE this file) watches every price series.
///
/// THE ABLATION IS RIG-ONLY (the spec's explicit fence): the damper variants
/// are constructed with record `with` on the in-memory config — shipped data
/// files are not touched, and the loader's (0,1) guard still protects every
/// real load path. Dampers named in the spec: (1) f &lt; 1, (2) the price
/// step's rail + band, (3) the transport-cost deadband.
///
/// Pre-committed readings: stable with margin → report the margin and which
/// damper binds. Unstable or marginal → report and STOP (never retune).
/// </summary>
public class TradeStressSoakTests
{
    private static EraTable FlatEra() => EraTableLoader.Load(
        """{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": 10.0 } ] }""");

    /// <summary>Runs the driven rig and returns the worst sign-flip count over
    /// both cloth price series plus the trade-volume series, with the series
    /// lengths the detector contract expects.</summary>
    private static (int WorstPriceFlips, int VolumeFlips, long TotalTraded, bool EverTraded)
        RunSoak(SimConfig cfg, int turns = 300)
    {
        // S0: producer (constant production signal, deep stock so the drive
        // cannot exhaust it inside the horizon). S1: consumer (constant
        // consumption-demand signal, modest stock so its market has scale).
        WorldState w = TradeArbitrageTests.TradeWorld(cfg, 2,
            stocks: [(0, "cloth", 2_000_000, 1000), (1, "cloth", 10_000, 0)],
            prices: [(0, "cloth", 1.0), (1, "cloth", 1.0)],
            edges: [(0, 1, 1.0)]);
        // The forced demand asymmetry: S1 wants 100,000 cloth per turn,
        // forever. The number is chosen against the solver's own arithmetic:
        // stockRelease = 0.5 · stock · dt = 5 × stock at dt 10, so a demand
        // signal below ~50,000 is swallowed by S1's own release term and both
        // prices simply decay in step (measured — the first version of this
        // rig used 1,000 and NEVER TRADED; the vacuity guard below caught it).
        // At 100,000 the excess is genuinely positive at S1 and negative at
        // S0: the maximum plausible sustained gap drive.
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            GoodStockRow row = w.GoodStocks[i];
            if (row.Settlement.Value == 1 && row.Good.Value == cfg.Goods!.IdOf("cloth"))
                w.GoodStocks[i] = row with { LastConsumptionDemandUnits = 100_000 };
        }

        var exec = new TurnExecutor(FlatEra(),
            [SystemCatalog.Price(cfg), SystemCatalog.TradeArbitrage(cfg)]);
        var p0 = new List<double>();
        var p1 = new List<double>();
        var volume = new List<double>();
        long total = 0;
        for (int t = 0; t < turns; t++)
        {
            w = exec.Step(w);
            double a = 1.0, b = 1.0;
            int clothId = cfg.Goods!.IdOf("cloth");
            for (int i = 0; i < w.Prices.Count; i++)
            {
                if (w.Prices[i].Good.Value != clothId) continue;
                if (w.Prices[i].Settlement.Value == 0) a = w.Prices[i].Price;
                if (w.Prices[i].Settlement.Value == 1) b = w.Prices[i].Price;
            }
            p0.Add(a);
            p1.Add(b);
            long moved = 0;
            for (int i = 0; i < w.TradeFlows.Count; i++)
                if (w.TradeFlows[i].Good.Value == clothId) moved += w.TradeFlows[i].Quantity;
            volume.Add(moved);
            total += moved;
            Assert.True(double.IsFinite(a) && double.IsFinite(b), "a price went non-finite");
        }
        const double tol = 1e-9;
        int worst = Math.Max(
            OscillationDetector.CountFlips(p0, tol), OscillationDetector.CountFlips(p1, tol));
        return (worst, OscillationDetector.CountFlips(volume, 0.5), total, total > 0);
    }

    [Fact]
    public void DrivenStress_Shipped_IsStable_AndTheAblationNamesTheBindingDamper()
    {
        SimConfig cfg = TestConfigs.Sim();

        (int shippedFlips, int shippedVolFlips, long traded, bool everTraded) = RunSoak(cfg);
        Assert.True(everTraded, "VACUOUS: the driven rig never traded — nothing was stressed");
        // The stability claim, via the proven detector: sustained oscillation
        // is >= 3 flips (its contract); the shipped margin is asserted at the
        // detector's own bar so a marginal system fails here rather than
        // being averaged away. (MEASURED values are recorded in the review
        // record and the assert message keeps them honest on re-run.)
        Assert.True(shippedFlips < OscillationDetector.MinFlips,
            $"REPORT AND STOP (spec R3): shipped dampers gave {shippedFlips} price sign-flips "
            + $"under driven stress — at or above the detector's oscillation bar. Do not retune.");

        // --- ablation, one damper at a time, rig-only ---------------------
        // (1) the cap: f 0.25 → 0.9 (still < 1 — the mandate's own edge).
        SimConfig fWide = cfg with { Trade = cfg.Trade with { GapClosingFraction = 0.9 } };
        (int fFlips, _, _, _) = RunSoak(fWide);

        // (2) the price rail widened out of the way (band left intact — it is
        // the divergence backstop, and removing both at once would conflate
        // two dampers).
        SimConfig railWide = cfg with { Price = cfg.Price with { MaxRelativeChangePerYear = 100.0 } };
        (int railFlips, _, _, _) = RunSoak(railWide);

        // (3) the deadband to (effectively) zero — the loader refuses 0, and
        // the rig honours the SAME contract by ablating within the legal
        // domain instead of bypassing it: 1e-9 is a dead deadband.
        SimConfig noDeadband = cfg with { Trade = cfg.Trade with { CostPerBulkCostUnit = 1e-9 } };
        (int deadbandFlips, _, _, _) = RunSoak(noDeadband);

        // MEASURED 2026-07-28 (300 turns, dt 10, demand drive 100k/turn):
        //   shipped: 1 price flip, 1 volume flip, 2,000,000 units traded —
        //     the seller's ENTIRE stock, i.e. under sustained maximum drive
        //     the mechanism drains a granary to zero rather than oscillating
        //     (reported to R2/B-2, not a stability fault);
        //   f→0.9: 1 flip. rail widened ×100: 1 flip. deadband→~0: 1 flip.
        // READING (spec R3, applied without deliberation): STABLE WITH
        // MARGIN — 1 flip against the detector's bar of 3, and NO SINGLE
        // damper carries the margin: each ablated alone leaves the rig at
        // 1 flip. The margin lives in the combination (chiefly f < 1 damping
        // each step toward parity while the rail bounds the price response);
        // the pins below keep every one of those measurements honest.
        Assert.True(fFlips < OscillationDetector.MinFlips,
            $"f→0.9 alone destabilized the rig ({fFlips} flips): the cap was the sole binding damper");
        Assert.True(railFlips < OscillationDetector.MinFlips,
            $"rail-widening alone destabilized the rig ({railFlips} flips): the rail was the sole binding damper");
        Assert.True(deadbandFlips < OscillationDetector.MinFlips,
            $"deadband removal alone destabilized the rig ({deadbandFlips} flips): the deadband was the sole binding damper");
        Assert.True(shippedVolFlips < OscillationDetector.MinFlips,
            $"trade VOLUME oscillated ({shippedVolFlips} flips) even though prices did not");
    }
}
