using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Harvest;

/// <summary>Tables owned by <see cref="HarvestWeatherSystem"/> (T3.4b).</summary>
public sealed record HarvestWeatherTables(Table<HarvestWeatherRow> Weather);

/// <summary>
/// T3.4b — HARVEST VARIANCE (CR-003 ruling §3). The first stochastic driver in
/// the simulation.
///
/// WHY IT EXISTS. The corrected constants leave a pre-Malthusian FRONTIER world
/// (CR-003 §5.1): land is abundant, so without weather there is no scarcity
/// driver at all except player error. The ruling's framing is the governing
/// one — "a frontier society is not Malthusian", the trap must EMERGE when land
/// fills, and it must never be restored by choosing a constant that reproduces
/// the old crash. Weather supplies the year-to-year shocks that make stores
/// matter; it does not supply famine.
///
/// FAMINE IS NOT TRIGGERED HERE, AND CANNOT BE. This system computes exactly one
/// thing: a mean-one multiplier on realised farm output. It has no threshold, no
/// event, no famine flag, and no read of population, stores or deficits. A bad
/// year is a small number multiplying a harvest; whether that becomes famine is
/// decided downstream by the food balance alone — thin stores plus consecutive
/// bad years. That is the ruling's constraint ("no famine trigger, threshold or
/// event that fires independently of the food balance") satisfied by
/// CONSTRUCTION rather than by discipline.
///
/// THE MODEL, per settlement i, in LOG space:
///   rho    = exp(−dtYears / CorrelationTimeYears)
///   x_i(t) = rho × x_i(t−1) + Sigma × sqrt(1 − rho²) × e_i
///   mult_i = exp(x_i − Sigma²/2)
///
/// TEMPORAL CORRELATION (required by the ruling). rho is the AR(1) memory. A bad
/// year raises the odds of the next year being bad, so MULTI-YEAR DROUGHTS are
/// reachable — which is the point: one bad year is survivable from stores,
/// consecutive bad years are what kill. rho = exp(−dt/tau) makes that memory a
/// property of YEARS rather than of turn count (law 3, and the ADR-016 family:
/// a continuous decay integrated exactly, not a per-turn constant).
///
/// The sqrt(1 − rho²) factor is not decoration: it holds the STATIONARY variance
/// at Sigma² for every dt. Without it a campaign that shrinks dt across eras
/// would silently change how variable the weather is, which would be the era
/// table altering the climate.
///
/// SPATIAL CORRELATION (required by the ruling). The innovation e_i is a blend
/// of a REGIONAL field and a LOCAL draw:
///   e_i = sqrt(k) × regional_i + sqrt(1−k) × local_i
/// with regional_i an exponential-distance-kernel smoothing of every
/// settlement's local draw over the SettlementDistances travel costs, normalised
/// to unit variance. Neighbours therefore share weather strongly and distant
/// settlements weakly. The ruling is explicit about why this matters:
/// uncorrelated rolls let trade and migration trivially average away all risk,
/// and regional bad years are the point.
///
/// MEAN EXACTLY ONE. E[exp(x)] = exp(Sigma²/2) for x ~ N(0, Sigma²), so the
/// −Sigma²/2 correction makes E[mult] = 1. Weather changes the VARIANCE of
/// yield, never its expectation. This is what keeps the derived
/// YieldPerArableKm2PerYear = 26.0 a statement about land quality under normal
/// conditions — the ruling requires the multiplier "never alters the derived
/// 26.0", and without this correction merely switching weather on would raise
/// mean yield by exp(Sigma²/2) and quietly re-open a constant the director
/// derived.
///
/// DETERMINISM (law 5). One RNG stream per settlement from the registry, keyed
/// (SystemId × settlement id); state lives in WorldState, so same seed → same
/// weather, forever, live and on replay. Settlements are visited in table row
/// order; the local draws are taken in that same fixed order BEFORE any
/// smoothing, so the kernel cannot make the draw sequence depend on distances.
///
/// STATELESS: config is immutable tuning; the AR(1) state is in the row.
/// </summary>
public sealed class HarvestWeatherSystem(SimConfig cfg) : ISimSystem<HarvestWeatherTables>
{
    public static readonly SystemId WellKnownId = new(13);
    public const string Name = "harvestweather";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<HarvestWeatherTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<HarvestWeatherRow> weather = ctx.Owned.Weather;
        HarvestVarianceConfig h = _cfg.HarvestVariance;

        int n = prev.Settlements.Count;
        if (n == 0) return;

        // PASS 1 — one local unit-normal draw per settlement, in table row order.
        // Taken BEFORE any smoothing so the draw SEQUENCE can never depend on
        // distances, kernel weights, or anything a later packet might change.
        Span<double> local = n <= 256 ? stackalloc double[n] : new double[n];
        for (int i = 0; i < n; i++)
        {
            SettlementId id = prev.Settlements[i].Id;
            local[i] = NextStandardNormal(ctx.Rng(new RegionId(id.Value)));
        }

        // PASS 2 — the regional field: an exponential-distance-kernel smoothing
        // of the local draws, normalised to unit variance. A sum of independent
        // unit normals with weights w has variance Σw², so dividing by sqrt(Σw²)
        // restores unit variance exactly — INDIVIDUALLY. That is all it does, and
        // T3.4b's error was to read it as more (see PASS 3).
        //
        // sumSq is CARRIED OUT of this pass because PASS 3 needs it: the self
        // weight is 1.0, so regional[i] CONTAINS local[i] with coefficient
        // 1/sqrt(sumSq), and that coefficient IS the covariance the blend has to
        // divide out.
        Span<double> regional = n <= 256 ? stackalloc double[n] : new double[n];
        Span<double> sumSqOf = n <= 256 ? stackalloc double[n] : new double[n];
        for (int i = 0; i < n; i++)
        {
            SettlementId from = prev.Settlements[i].Id;
            double acc = 0.0, sumSq = 0.0;
            for (int j = 0; j < n; j++)
            {
                SettlementId to = prev.Settlements[j].Id;
                double w = i == j ? 1.0 : Kernel(prev, from, to, h.SpatialRangeCostUnits);
                if (w <= 0.0) continue;
                acc += w * local[j];
                sumSq += w * w;
            }
            sumSqOf[i] = sumSq;
            regional[i] = sumSq > 0.0 ? acc / Math.Sqrt(sumSq) : local[i];
        }

        // PASS 3 — the AR(1) step and the mean-one multiplier.
        double rho = Math.Exp(-ctx.DtYears / h.CorrelationTimeYears);
        double innovationScale = h.SigmaLogYield * Math.Sqrt(Math.Max(0.0, 1.0 - rho * rho));
        double k = Math.Clamp(h.SpatialSharedFraction, 0.0, 1.0);
        double wRegional = Math.Sqrt(k), wLocal = Math.Sqrt(1.0 - k);
        double meanCorrection = 0.5 * h.SigmaLogYield * h.SigmaLogYield;

        for (int i = 0; i < n; i++)
        {
            SettlementId id = prev.Settlements[i].Id;
            double previous = 0.0;
            for (int r = 0; r < prev.HarvestWeather.Count; r++)
            {
                if (prev.HarvestWeather[r].Settlement != id) continue;
                previous = prev.HarvestWeather[r].LogDeviation;
                break;
            }

            // THE BLEND, VARIANCE-CORRECTED (T3.4c, fixing a T3.4b defect).
            //
            // regional_i and local_i are NOT independent: PASS 2 builds the
            // regional field with self-weight 1.0, so regional_i contains
            // local_i at coefficient 1/sqrt(sumSq_i), which is exactly
            // Cov(regional_i, local_i). Blending them as though independent
            // gives
            //
            //   Var(√k·regional_i + √(1−k)·local_i) = 1 + 2√(k(1−k))/√sumSq_i
            //
            // which is strictly greater than 1 for every 0 < k < 1. T3.4b did
            // that, so realised sigma ran 1.10–1.41× the DERIVED 0.2936 and the
            // multiplier's mean ran 1.013–1.043 instead of exactly 1 — a silent
            // ~3.2% lift on expected yield (t = 7.98 over 32 seeds), which is
            // observationally identical to running the derived 26.0 at 26.9 and
            // is what CR-003 §5.2(b) forbids in substance.
            //
            // Dividing by sqrt of that variance restores BOTH properties exactly,
            // at every geometry, and touches nothing else: the spatial structure,
            // the AR(1) memory, the draw order and the RNG consumption are all
            // unchanged, and SIGMA STAYS 0.2936 — it simply becomes true.
            //
            // The k-sweep is the proof this is the only defect: variance is
            // exactly correct at k=0 and k=1 (no blending happens, so there is no
            // cross-term) and maximal in between, with the shipped k=0.6 sitting
            // essentially at the maximum.
            //
            // THE FALLBACK BRANCH. When sumSq_i is 0 — a settlement with no
            // reachable neighbour, where PASS 2 sets regional_i = local_i — the
            // two are the SAME variable, correlation 1, and the variance is
            // (√k+√(1−k))². That is the worst case (1.9798 at k=0.5), so it must
            // be handled and not left to the general formula.
            //
            // REJECTED ALTERNATIVE, recorded so it is not re-proposed: excluding
            // self (w_ii = 0) does make them independent, but then EVERY isolated
            // settlement falls into that same fallback, and it changes what
            // "regional" means — a region's weather would no longer include its
            // own site.
            double varInn = sumSqOf[i] > 0.0
                ? 1.0 + 2.0 * wRegional * wLocal / Math.Sqrt(sumSqOf[i])
                : (wRegional + wLocal) * (wRegional + wLocal);
            double innovation =
                (wRegional * regional[i] + wLocal * local[i]) / Math.Sqrt(varInn);
            double x = rho * previous + innovationScale * innovation;
            double multiplier = Math.Exp(x - meanCorrection);

            var row = new HarvestWeatherRow(id, x, multiplier);
            int existing = -1;
            for (int r = 0; r < weather.Count; r++)
                if (weather[r].Settlement == id) { existing = r; break; }
            if (existing >= 0) weather[existing] = row; else weather.Add(row);
        }
    }

    /// <summary>Exponential distance kernel over the T2.5 travel-cost table.
    /// Absent pairs (no route) share no weather — correctly, since the kernel is
    /// a proxy for physical proximity and an unreachable settlement is not a
    /// neighbour.</summary>
    private static double Kernel(
        IReadOnlyWorldState prev, SettlementId from, SettlementId to, double range)
    {
        if (!(range > 0.0)) return 0.0;
        for (int i = 0; i < prev.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow d = prev.SettlementDistances[i];
            if (d.From != from || d.To != to) continue;
            if (!double.IsFinite(d.TravelCost)) return 0.0;
            return Math.Exp(-d.TravelCost / range);
        }
        return 0.0;
    }

    /// <summary>
    /// Box–Muller from the registry's uniform stream. Deterministic and
    /// allocation-free; the u1 floor keeps log() finite when a draw returns
    /// exactly 0. Only the first of the two Box–Muller outputs is used —
    /// discarding the second costs one extra draw and keeps the mapping from
    /// "nth call" to "nth normal" trivial, which is what makes a reordered or
    /// inserted draw show up as a hash change rather than a silent reshuffle.
    /// </summary>
    private static double NextStandardNormal(RngStream rng)
    {
        double u1 = rng.NextDouble();
        double u2 = rng.NextDouble();
        if (u1 < 1e-300) u1 = 1e-300;
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
