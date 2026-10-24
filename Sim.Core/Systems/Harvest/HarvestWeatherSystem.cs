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
/// THE MODEL, per settlement i, in LOG space, stepped in s = max(1, round(dt)) sub-steps of
/// h = dt / s years inside each turn (D-051 ruling 1(b), 2026-10-10):
///   rho      = exp(−h / CorrelationTimeYears)
///   x_i(k)   = rho × x_i(k−1) + Sigma × sqrt(1 − rho²) × e_i(k)
///   mult_i   = (1/s) × Σ_k exp(x_i(k) − Sigma²/2)     — the TIME-AVERAGE over the turn
/// The row carries the end-of-turn x; s exact steps compose to one exact step of length dt, so the
/// carried state's turn-to-turn law is exactly the pre-1(b) AR(1) with rho = exp(−dt/tau).
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
/// settlement's local draw over the GEOGRAPHIC straight-line distances between
/// site cells (M5 R2b — never the road-aware travel costs; see Kernel), normalised
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

        // The spatial kernel, ONCE per turn (it reads only PREV geometry, which no sub-step changes).
        // w[i·n + j] is the T3.4b/R2b exponential-distance weight with the unconditional self-weight
        // 1.0; sumSqOf[i] = Σ_j w², carried to the variance-corrected blend (T3.4c, see Innovations).
        double[] w = new double[n * n];
        Span<double> sumSqOf = n <= 256 ? stackalloc double[n] : new double[n];
        double kmPerCostUnit = TransportQuery.KmPerCostUnit(prev);
        for (int i = 0; i < n; i++)
        {
            SettlementId from = prev.Settlements[i].Id;
            double sumSq = 0.0;
            for (int j = 0; j < n; j++)
            {
                SettlementId to = prev.Settlements[j].Id;
                double wij = i == j ? 1.0 : Kernel(prev, i, j, from, to, kmPerCostUnit, h.SpatialRangeCostUnits, h.LegacyTravelCostKernel);
                if (wij <= 0.0) wij = 0.0;
                w[i * n + j] = wij;
                sumSq += wij * wij;
            }
            sumSqOf[i] = sumSq;
        }

        double k = Math.Clamp(h.SpatialSharedFraction, 0.0, 1.0);
        double wRegional = Math.Sqrt(k), wLocal = Math.Sqrt(1.0 - k);
        double meanCorrection = 0.5 * h.SigmaLogYield * h.SigmaLogYield;
        Span<double> local = n <= 256 ? stackalloc double[n] : new double[n];
        Span<double> innovation = n <= 256 ? stackalloc double[n] : new double[n];
        Span<double> x = n <= 256 ? stackalloc double[n] : new double[n];
        Span<double> sumMult = n <= 256 ? stackalloc double[n] : new double[n];

        // The AR(1) state each settlement carries in from PREV. A settlement with NO row (the founding
        // turn, a colony's first turn) joins the weather process AT ITS STATIONARY DISTRIBUTION,
        // x ~ N(0, σ²), drawn through the same spatially blended pipeline (D-051 ruling 1(b), M5 polish
        // 2026-10-10). Before 1(b) such a row started at x = 0; the −σ²/2 mean correction assumes a
        // stationary x, so x = 0 is a below-mean year (exp(−σ²/2) = 0.958), and averaged over the
        // founding turn's yearly sub-steps that start would cost ≈ 0.45 % of expected harvest. The draw
        // pass runs only on turns where some settlement lacks a row, so a world with no new settlement
        // consumes exactly the sub-step draws below.
        //
        // harvestVariance.timeAveragedHarvest = false (the GOLDEN-ATTRIBUTION CONTROL ONLY, never a gameplay
        // setting) restores the pre-1(b) mechanic bit-exactly: x = 0 for a row-less settlement, no extra draw
        // pass, ONE sub-step of the whole dt (ρ = exp(−dt/τ)), Multiplier = exp(x − σ²/2) — the same arithmetic
        // in the same order, and the same RNG consumption (IntegratedPinAttributionTests returns the pre-1(b)
        // pins byte for byte through it).
        bool timeAveraged = h.TimeAveragedHarvest;
        bool anyNew = false;
        for (int i = 0; i < n; i++)
        {
            SettlementId id = prev.Settlements[i].Id;
            bool found = false;
            for (int r = 0; r < prev.HarvestWeather.Count; r++)
            {
                if (prev.HarvestWeather[r].Settlement != id) continue;
                x[i] = prev.HarvestWeather[r].LogDeviation;
                found = true;
                break;
            }
            if (!found) { x[i] = timeAveraged ? double.NaN : 0.0; anyNew |= timeAveraged; }
        }
        if (anyNew)
        {
            Innovations(ctx, prev, n, w, sumSqOf, wRegional, wLocal, local, innovation);
            for (int i = 0; i < n; i++)
                if (double.IsNaN(x[i])) x[i] = h.SigmaLogYield * innovation[i];
        }

        // D-051 RULING 1(b) — THE TURN'S HARVEST IS THE TIME-AVERAGE OF THE YEARLY WEATHER PROCESS.
        //
        // The process is unchanged: the same log-space AR(1) (an Ornstein–Uhlenbeck process with
        // correlation time τ and stationary SD σ), the same spatial blend, the same mean-one map
        // m = exp(x − σ²/2). What changes is what one turn's harvest READS. Before 1(b) one point draw
        // of x multiplied the whole turn's harvest — at dt = 10 a single year's weather stood for a
        // decade, so the decade harvest's log-spread was the full yearly σ ≈ 0.29 (measured: a 4 %
        // shortfall drove a ~6 % exodus, and 11/20 order-free canonical seeds starved by turn 650).
        // Now the turn is divided into s = max(1, round(dt)) equal sub-steps of h = dt / s years (one
        // YEAR at every integral dt ≥ 1; a sub-year turn is one sub-step, the point value), the process
        // is stepped EXACTLY across each (ρ_h = exp(−h/τ), innovation σ·√(1 − ρ_h²)), and the turn's
        // multiplier is the MEAN of the sub-steps' multipliers.
        //
        //   - MEAN UNCHANGED: each sub-step multiplier is mean-one at stationarity, so their average
        //     is too (the stationary start above makes that hold from a settlement's first turn).
        //   - SPREAD REDUCED as ruled: at dt = 10, τ = 3 the log-spread of the decade average is
        //     σ·√(1/s · [1 + 2·Σ_{m=1}^{s−1} (1 − m/s)·ρ₁^m]) = 0.2936 × √0.4335 = 0.193
        //     (D-051: "~0.30 to ~0.19").
        //   - THE STATE IS UNCHANGED IN LAW: s exact OU steps compose to ONE exact step of length dt
        //     (Π ρ_h = exp(−dt/τ); the stationary variance σ² is preserved at every h), so LogDeviation,
        //     the row's carried state, has exactly the turn-resolution AR(1) law it had before — its
        //     lag-1 autocorrelation is still exp(−dt/τ) and its SD still σ.
        //   - dt-CORRECT (law 3): the sub-step count follows from dt in YEARS, never from a turn count.
        //   - DETERMINISTIC: one local draw per settlement per sub-step, sub-steps in order, settlements
        //     in table row order inside each — the same stream discipline as before, s times.
        //   - σ = 0: every x is 0 and every multiplier exactly 1, while consuming the SAME draws (the
        //     matched no-weather control the tests rely on).
        // ADR-047 — harvestVariance.sigmaLogYield, per settlement HOLDER ("steadier harvests"). The weather process itself
        // (x, its draws and its spatial blend) is shared and unchanged; what a holder's knowledge lowers is how strongly
        // its HARVEST follows that weather: σᵢ = the holder's σ, sensitivity sᵢ = σᵢ/σ, and the sub-step multiplier reads
        // exp(sᵢ·x − σᵢ²/2) — still mean-one at stationarity (sᵢ·x ~ N(0, σᵢ²)). A settlement whose σᵢ is the base σ takes the
        // unchanged expression below, so with no effect the step is the pre-ADR-047 instruction sequence bit for bit.
        KnowledgeEffectsSnapshot fx = KnowledgeEffects.Snapshot(prev, _cfg.Research);
        Span<double> sensitivity = n <= 256 ? stackalloc double[n] : new double[n];
        Span<double> sensitiveCorrection = n <= 256 ? stackalloc double[n] : new double[n];
        bool anySensitivity = false;
        for (int i = 0; i < n; i++)
        {
            double sigmaI = fx.ApplyAt(prev.Settlements[i].Id, KnowledgeOperand.HarvestVarianceSigmaLogYield, h.SigmaLogYield);
            bool own = sigmaI != h.SigmaLogYield && h.SigmaLogYield > 0.0;
            sensitivity[i] = own ? sigmaI / h.SigmaLogYield : 1.0;
            sensitiveCorrection[i] = own ? 0.5 * sigmaI * sigmaI : meanCorrection;
            anySensitivity |= own;
        }

        int subSteps = timeAveraged ? Math.Max(1, (int)Math.Round(ctx.DtYears, MidpointRounding.AwayFromZero)) : 1;
        double subDt = ctx.DtYears / subSteps;
        double rho = Math.Exp(-subDt / h.CorrelationTimeYears);
        double innovationScale = h.SigmaLogYield * Math.Sqrt(Math.Max(0.0, 1.0 - rho * rho));
        for (int i = 0; i < n; i++) sumMult[i] = 0.0;
        for (int step = 0; step < subSteps; step++)
        {
            Innovations(ctx, prev, n, w, sumSqOf, wRegional, wLocal, local, innovation);
            for (int i = 0; i < n; i++)
            {
                x[i] = rho * x[i] + innovationScale * innovation[i];
                sumMult[i] += anySensitivity && sensitivity[i] != 1.0
                    ? Math.Exp(sensitivity[i] * x[i] - sensitiveCorrection[i])
                    : Math.Exp(x[i] - meanCorrection);
            }
        }

        for (int i = 0; i < n; i++)
        {
            SettlementId id = prev.Settlements[i].Id;
            var row = new HarvestWeatherRow(id, x[i], sumMult[i] / subSteps);
            int existing = -1;
            for (int r = 0; r < weather.Count; r++)
                if (weather[r].Settlement == id) { existing = r; break; }
            if (existing >= 0) weather[existing] = row; else weather.Add(row);
        }
    }

    /// <summary>
    /// One spatially blended, unit-variance innovation per settlement (PASSES 1–2 of T3.4b, with the
    /// T3.4c variance correction), written into <paramref name="innovation"/> in table row order.
    ///
    /// PASS 1 — one local unit-normal draw per settlement, in table row order, taken BEFORE any
    /// smoothing so the draw SEQUENCE can never depend on distances or kernel weights.
    ///
    /// PASS 2 — the regional field: the exponential-distance-kernel smoothing of the local draws,
    /// divided by sqrt(Σw²) so it has unit variance INDIVIDUALLY.
    ///
    /// THE BLEND, VARIANCE-CORRECTED (T3.4c, fixing a T3.4b defect). regional_i and local_i are NOT
    /// independent: the regional field has self-weight 1.0, so regional_i contains local_i at
    /// coefficient 1/sqrt(sumSq_i), which is exactly Cov(regional_i, local_i). Blending them as though
    /// independent gives Var(√k·regional_i + √(1−k)·local_i) = 1 + 2√(k(1−k))/√sumSq_i > 1 for every
    /// 0 &lt; k &lt; 1 — T3.4b did that, so realised sigma ran 1.10–1.41× the derived 0.2936 and the
    /// multiplier's mean ran 1.013–1.043 (a silent ~3.2 % lift on expected yield, which CR-003 §5.2(b)
    /// forbids in substance). Dividing by sqrt of that variance restores BOTH properties exactly, at
    /// every geometry. An isolated settlement needs no branch: S_i = 1, regional_i = local_i, and the
    /// general formula gives the fully-correlated (√k+√(1−k))². REJECTED ALTERNATIVE, recorded so it
    /// is not re-proposed: excluding self (w_ii = 0) makes them independent, but then an isolated
    /// settlement divides by zero, and a region's weather would no longer include its own site.
    /// </summary>
    private static void Innovations(
        SimContext<HarvestWeatherTables> ctx, IReadOnlyWorldState prev, int n, double[] w,
        ReadOnlySpan<double> sumSqOf, double wRegional, double wLocal, Span<double> local, Span<double> innovation)
    {
        for (int i = 0; i < n; i++)
        {
            SettlementId id = prev.Settlements[i].Id;
            local[i] = NextStandardNormal(ctx.Rng(new RegionId(id.Value)));
        }
        for (int i = 0; i < n; i++)
        {
            double acc = 0.0;
            for (int j = 0; j < n; j++)
            {
                double wij = w[i * n + j];
                if (wij <= 0.0) continue;
                acc += wij * local[j];
            }
            double regional = acc / Math.Sqrt(sumSqOf[i]);
            double varInn = 1.0 + 2.0 * wRegional * wLocal / Math.Sqrt(sumSqOf[i]);
            innovation[i] = (wRegional * regional + wLocal * local[i]) / Math.Sqrt(varInn);
        }
    }

    /// <summary>
    /// Exponential distance kernel over GEOGRAPHIC distance (M5 integration R2b, resolving
    /// docs/m5-governing-loop-port.md §5 B4): exp(−d / range), with d the straight-line distance
    /// between the two site cells in ideal-ground cost units (<see cref="GeographicDistance"/>).
    /// The kernel is a proxy for PHYSICAL PROXIMITY, and weather does not follow roads: before this
    /// it read the road-aware Pathfinder costs in SettlementDistances (ADR-032 §10.5), so building a
    /// road changed which settlements shared a drought, and a pair with no land route (across a
    /// strait) shared no weather at all however close. Now no road, route class or reachability can
    /// move it (pinned by HarvestWeatherGeographyTests). A terrain-less world (a hand-built toy,
    /// whose distance rows are written by hand and never road-aware) keeps the hand-written table;
    /// there an absent pair shares no weather. The config value harvestVariance.spatialDistance
    /// "travelCost" restores the pre-R2b reading; it exists only as the golden-attribution control.
    /// </summary>
    private static double Kernel(
        IReadOnlyWorldState prev, int fromRow, int toRow, SettlementId from, SettlementId to,
        double kmPerCostUnit, double range, bool legacyTravelCost)
    {
        if (!(range > 0.0)) return 0.0;
        if (!legacyTravelCost && GeographicDistance.TryIdealGroundCostUnits(
                prev, prev.Settlements[fromRow].SiteCell, prev.Settlements[toRow].SiteCell, kmPerCostUnit, out double geo))
            return Math.Exp(-geo / range);
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
