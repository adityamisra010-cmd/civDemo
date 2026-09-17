using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Disaster;

/// <summary>Tables owned by <see cref="DisasterSystem"/> (T4.21-1).</summary>
public sealed record DisasterTables(Table<DisasterRow> Disasters);

/// <summary>
/// T4.21-1 (CR-015 §3.3) — THE EXPLICIT FAMINE-CLASS PRODUCTION SHOCK. The
/// second stochastic driver in the simulation, and the first EVENT.
///
/// WHY IT EXISTS. The director's mandate: famine is exceptional. Ordinary
/// weather — a mean-one lognormal with sigma 0.2936 — must never be famine, and
/// yet a settlement must be able to lose its harvest for reasons no granary
/// absorbs. This system supplies that cause: a rare, multi-year, famine-CLASS
/// crop failure with a magnitude derived so that one event ALONE can exhaust a
/// typical settlement's effective food buffer at the coarsest era dt
/// (DisasterConfig: s·D ≥ 3.46 production-years at ρ_ship = 1.3, G = 1.5 y,
/// dt_max = 10). Weather cannot reach that band at the year scale (a 0.25 yearly
/// multiplier sits at z = −4.57), so "not every extreme draw is a disaster"
/// holds by construction.
///
/// WHAT IT IS NOT. It does not decide famine — FoodState does, from the food
/// balance (CR-003 §3 with CR-015's cause qualifier): a strike a rich granary
/// absorbs is not a famine. It reads no population, no stores, no deficits and
/// no weather. It multiplies exactly the two FOOD paths harvest weather
/// multiplies (farming, herding/fishing), leaves the derived 26.0 a statement
/// about land, touches no store (a "granary loss" kind is queued), and carries a
/// single kind. HarvestWeatherSystem's "famine is not triggered here" stands for
/// WEATHER; the amendment that lets THIS system exist is CR-015 (N1).
///
/// THE STEP, per settlement in table row order, all from PREV:
///   p = 1 − exp(−λ dt)                                   exact integration of a per-year hazard (law 3)
///   uH = rng(s).NextDouble(); uS = rng(s).NextDouble()    ALWAYS both — see DETERMINISM
///   applied = prev row's Multiplier (absent → 1.0)       the factor Production used THIS step
///   active  (prev RemainingYears > 0): severity and remaining carry (renewal — no new onset while active)
///   onset   (uH < p):                  severity = s_min + (s_max − s_min)·uS; remaining = D
///   neither:                           write (s, 0, 0, 0, 1.0, applied) iff applied < 1, then continue
///   overlap = min(remaining, dt); multiplier = 1 − severity·overlap/dt; remaining −= overlap
///   write (s, 1, severity, remaining, multiplier, applied)
/// The table is REBUILT every step (TradeFlows precedent), rows only where
/// Multiplier &lt; 1 ∨ AppliedMultiplier &lt; 1 ∨ RemainingYears &gt; 0.
///
/// dt-CORRECTNESS. P composes exactly across dts up to the stated onset biases;
/// severity × overlap integrates the loss against the years actually inside
/// the turn, so a 5-year failure costs 5·s years of food at dt 10 (one turn,
/// multiplier 1 − 0.5 s) and at dt 0.5 (ten turns at 1 − s) alike — pinned by
/// DisasterSystemTests.D_DtExact_FiveYearFailure. RemainingYears persists BY
/// DIMENSION (an event longer than a late-era turn), not by observation of a
/// regime. ACCEPTED BIASES (CR-015 §3.3, at λ = 0.01): "no new onset while
/// active" plus once-per-turn booking gives at most ONE onset per turn at dt 10
/// (≈ 4.7% of onsets truncated) and a 5-year refractory at dt 0.5 (effective
/// rate λ/(1 + λD) = 0.952 λ); the exact renewal is queued.
///
/// DETERMINISM (law 5). One RNG stream per settlement from the registry, keyed
/// (SystemId × settlement id) exactly as HarvestWeather keys its own; state in
/// WorldState. BOTH uniforms are drawn unconditionally, in that fixed order, so
/// RNG consumption is a constant 2 NextDouble per settlement-turn whatever the
/// hazard or the outcome: λ = 0 leaves the RngStreams table bit-identical to a
/// λ &gt; 0 run with no strike, which is what makes the layout-only attribution
/// control of the shipped (λ = 0) packet exact. An inserted or reordered draw
/// shows up as a hash change rather than a silent reshuffle.
///
/// PIPELINE SLOT: immediately after "harvestweather", written for NEXT turn's
/// PREV read — the same one-turn lag as weather. The Spine's "events/crises"
/// slot is late in the turn; under the one-turn lag the two positions are
/// equivalent for everything that reads the row, and the early position fixes
/// the turn-1 RngStreams creation order. Stated so nobody reads it as the Spine
/// slot being moved.
///
/// SystemId 23: 17 is held by the unmerged t4.13 branch, 22 by GovernanceSystem
/// on the unmerged m5-full-build branch (which also carries a "v25" schema —
/// see CanonicalSchema's collision note). PipelineLoader refuses duplicate ids
/// and names at load, so a merge collision is loud, never silent.
///
/// STATELESS: config is immutable tuning; every carried quantity is in the row.
/// </summary>
public sealed class DisasterSystem(SimConfig cfg) : ISimSystem<DisasterTables>
{
    public static readonly SystemId WellKnownId = new(23);
    public const string Name = "disaster";

    /// <summary>The single shipped kind.</summary>
    public const int KindCropFailure = 1;

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<DisasterTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<DisasterRow> disasters = ctx.Owned.Disasters;
        DisasterConfig c = _cfg.Disaster;
        double dt = ctx.DtYears;

        // Rebuilt every step: rows persist only through the carry below.
        disasters.Clear();

        int n = prev.Settlements.Count;
        if (n == 0) return;

        double p = 1.0 - Math.Exp(-c.HazardPerYear * dt);

        for (int i = 0; i < n; i++)
        {
            SettlementId id = prev.Settlements[i].Id;
            RngStream rng = ctx.Rng(new RegionId(id.Value));
            double uHazard = rng.NextDouble();
            double uSeverity = rng.NextDouble();   // ALWAYS taken, even when unused

            double applied = 1.0, severity = 0.0, remaining = 0.0;
            for (int r = 0; r < prev.Disasters.Count; r++)
            {
                if (prev.Disasters[r].Settlement != id) continue;
                DisasterRow prevRow = prev.Disasters[r];
                applied = prevRow.Multiplier;
                severity = prevRow.Severity;
                remaining = prevRow.RemainingYears;
                break;
            }

            if (remaining > 0.0)
            {
                // An active disaster continues; no new onset while active.
            }
            else if (uHazard < p)
            {
                severity = c.SeverityMin + (c.SeverityMax - c.SeverityMin) * uSeverity;
                remaining = c.DurationYears;
            }
            else
            {
                // Carry the Applied fact one turn, nothing else.
                if (applied < 1.0)
                    disasters.Add(new DisasterRow(id, 0, 0.0, 0.0, 1.0, applied));
                continue;
            }

            double overlap = Math.Min(remaining, dt);
            double multiplier = 1.0 - severity * overlap / dt;
            remaining -= overlap;
            if (multiplier < 1.0 || applied < 1.0 || remaining > 0.0)
                disasters.Add(new DisasterRow(id, KindCropFailure, severity, remaining, multiplier, applied));
        }
    }
}
