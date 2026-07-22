using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.NeedsGrievance;

/// <summary>Writable handles to NeedsGrievanceSystem's own tables (built by
/// SystemCatalog only).</summary>
public readonly record struct NeedsGrievanceTables(
    Table<NeedSatisfactionRow> Satisfactions, Table<GrievanceRow> Grievances);

/// <summary>
/// Needs + grievance (T2.6, m2 spec §3/§4; D-018 needs ladder, D-021 decay
/// doctrine). Everything reads Prev (§3.2). Slots after Demographics, before
/// PathBuild.
///
/// SATISFACTION (rebuilt each turn, per settlement × class × BOUND need):
/// M2 binds Sustenance only — s = 1 − PREV consumption deficit, clamped to
/// [0,1] at both ends. Classes carry EQUAL values at M2 because consumption
/// clamping is settlement-wide; the rows are per-class because M3's class
/// consumption baskets will differentiate them — that is an honesty note
/// about today, not a mechanism. UNBOUND needs get no rows and contribute
/// EXACTLY nothing anywhere (their weights are dormant data until the
/// milestone that binds them; the HUD renders them "not yet simulated").
///
/// GRIEVANCE (persistent per-(settlement, class) DOUBLE stock): deliberately
/// NOT conserved and NEVER Ledger — law 1 governs people/money/goods, and
/// grievance is manufactured by deprivation and destroyed by decay. Explicit
/// dt-correct integration, per year, integrated with dtYears:
///
///   G_next = G_prev + Σ_bound wₙ × (expectationₙ − sₙ)⁺ × dt
///                   − decayRate(t) × G_prev × dt,   floored at 0
///
/// expectationₙ is FIXED at 1.0 for M2 — the D-018 §4 habituation ratchet
/// (expectations drifting toward recent consumption) is deferred to the
/// milestone that gives needs real supply curves to habituate to.
///
/// GENERATIONAL DECAY (D-021 §8 — "children inherit a fraction of their
/// parents' grudges"): decayRate = BaseDecayPerYear +
/// (1 − InheritFraction) × turnoverRate, where turnoverRate = (PREV births +
/// deaths) / PREV population per settlement per year — read from the PREV
/// SettlementVitals chronicle row (Demographics writes it; communication is
/// through tables, law 6), whose own DtYears field keeps the rate per-year
/// across era-pacing transitions. All three knobs are TUNE in needs.json.
///
/// READ ISOLATION (the packet's teeth): NeedSatisfactions and Grievances are
/// referenced ONLY by this system, serialization, StateEquals, tests, and
/// Sim.Ui — enforced by the CI read-isolation grep (allowlisted); grievance
/// drives NO behavior until M5 ships the unrest valves with the gas pedal.
/// STATELESS: config is immutable tuning, not state. No RNG.
/// </summary>
public sealed class NeedsGrievanceSystem : ISimSystem<NeedsGrievanceTables>
{
    public static readonly SystemId WellKnownId = new(11);
    public const string Name = "needsgrievance";

    /// <summary>M2 expectation baseline (see header: habituation deferred, D-018 §4).</summary>
    private const double Expectation = 1.0;

    /// <summary>The Sustenance need id in the D-018 registry — the one need M2
    /// binds. Data-checked at construction, not assumed.</summary>
    private const int SustenanceId = 1;

    private readonly NeedsConfig _needs;

    public NeedsGrievanceSystem(SimConfig cfg)
    {
        _needs = cfg.Needs ?? throw new NeedsConfigException(
            "SimConfig.Needs is not loaded — construct configs via " +
            "SimConfigLoader.Load(simStream, needsStream) so needs.json travels with sim.json.");
    }

    public SystemId Id => WellKnownId;

    public void Step(SimContext<NeedsGrievanceTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        double dt = ctx.DtYears;
        GrievanceTuning tuning = _needs.Grievance;

        // --- satisfaction: rebuilt from PREV supply signals ------------------
        Table<NeedSatisfactionRow> satisfactions = ctx.Owned.Satisfactions;
        satisfactions.Clear();

        // Grievance rows persist (cloned): founded settlement-major in class
        // order; iterate rows and derive everything per row's settlement.
        Table<GrievanceRow> grievances = ctx.Owned.Grievances;

        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // PREV deficit (absent before the first consumption turn → 0).
            double deficit = 0.0;
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
            {
                if (prev.ConsumptionDeficits[i].Settlement == settlement)
                {
                    deficit = prev.ConsumptionDeficits[i].DeficitRatio;
                    break;
                }
            }
            // Sustenance satisfaction, clamped at BOTH ends (a deficit outside
            // [0,1] must not mint satisfaction above 1 or below 0).
            double sustenance = Math.Clamp(1.0 - deficit, 0.0, 1.0);

            // D-021 generational turnover from the PREV vitals chronicle row
            // (absent on the first turn → 0): per-year via the ROW's dt.
            double turnoverPerYear = 0.0;
            for (int i = 0; i < prev.SettlementVitals.Count; i++)
            {
                SettlementVitalsRow v = prev.SettlementVitals[i];
                if (v.Settlement != settlement) continue;
                long pop = 0;
                for (int b = 0; b < prev.Buckets.Count; b++)
                    if (prev.Buckets[b].Settlement == settlement) pop += prev.Buckets[b].Count.Value;
                if (pop > 0 && v.DtYears > 0.0)
                    turnoverPerYear = (v.Births + v.Deaths) / (double)pop / v.DtYears;
                break;
            }
            double decayRate = tuning.BaseDecayPerYear
                               + (1.0 - tuning.InheritFraction) * turnoverPerYear;

            // Accrual from BOUND needs only — M2: Sustenance. An unbound need
            // is skipped ENTIRELY (zero effect, whatever its weight says).
            double accrualPerYear = 0.0;
            for (int n = 0; n < _needs.Needs.Length; n++)
            {
                NeedEntry need = _needs.Needs[n];
                if (!need.Bound) continue;
                double satisfaction = need.Id == SustenanceId ? sustenance : 1.0;
                accrualPerYear += need.Weight * Math.Max(0.0, Expectation - satisfaction);
            }

            // Per-class rows: satisfaction rows for bound needs, grievance
            // integration on the persistent stock. Values are identical across
            // classes at M2 (settlement-wide inputs — see header).
            for (int g = 0; g < grievances.Count; g++)
            {
                if (grievances[g].Settlement != settlement) continue;
                ClassId cls = grievances[g].Class;
                for (int n = 0; n < _needs.Needs.Length; n++)
                {
                    NeedEntry need = _needs.Needs[n];
                    if (!need.Bound) continue;
                    satisfactions.Add(new NeedSatisfactionRow(
                        settlement, cls, need.Id, need.Id == SustenanceId ? sustenance : 1.0));
                }

                double gPrev = 0.0;
                for (int i = 0; i < prev.Grievances.Count; i++)
                {
                    if (prev.Grievances[i].Settlement == settlement
                        && prev.Grievances[i].Class == cls)
                    { gPrev = prev.Grievances[i].Value; break; }
                }
                // Explicit Euler, floored at 0: a decayRate × dt > 1 step (huge
                // turnover at Neolithic dt) must bottom out, not oscillate
                // negative. The floor is the stock's own domain bound, not a
                // conservation clamp.
                double gNext = gPrev + accrualPerYear * dt - decayRate * gPrev * dt;
                grievances[g] = grievances[g] with { Value = Math.Max(0.0, gNext) };
            }
        }
    }
}
