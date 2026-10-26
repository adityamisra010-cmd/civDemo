using System.Globalization;
using Xunit.Abstractions;
using static Sim.Tests.Kernel.ResearchPacingRunner;

namespace Sim.Tests.Kernel;

/// <summary>
/// D-064 evidence — MEASUREMENTS, not gates (run with SIM_MEASURE=1). Both use <see cref="ResearchPacingRunner"/> on the
/// canonical world, seeds 1, 2, 42 and 7, 1,630 turns. Optional environment: CIVSIM_MEASURE_OUT (a directory for the per-turn,
/// per-node and milestone CSVs), CIVSIM_TURNS (a shorter run, for smoke checks), CIVSIM_PAR (parallel runs, default 4).
/// Results: docs/m6-plan/knowledge-redesign/research-cost-pass/overflow-trial.md and empty-lane-reserve-proposal.md.
/// </summary>
public class ResearchEconomyMeasurement(ITestOutputHelper output)
{
    private static ulong[] Seeds => Environment.GetEnvironmentVariable("CIVSIM_SEEDS") is { Length: > 0 } list
        ? Array.ConvertAll(list.Split(','), x => ulong.Parse(x, CultureInfo.InvariantCulture))
        : [1, 2, 42, 7];

    private static int Turns => int.TryParse(Environment.GetEnvironmentVariable("CIVSIM_TURNS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int t) ? t : 1630;
    private static int Par => int.TryParse(Environment.GetEnvironmentVariable("CIVSIM_PAR"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : 4;
    private static string? OutDir => Environment.GetEnvironmentVariable("CIVSIM_MEASURE_OUT");

    private void RunAll(RunOptions[] all)
    {
        // CIVSIM_JOBS: a comma list of labels to run (default all).
        string? only = Environment.GetEnvironmentVariable("CIVSIM_JOBS");
        RunOptions[] jobs = only is { Length: > 0 } ? Array.FindAll(all, j => Array.IndexOf(only.Split(','), j.Label) >= 0) : all;
        var results = new string[jobs.Length];
        System.Threading.Tasks.Parallel.For(0, jobs.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Par },
            i => results[i] = Run(jobs[i], OutDir).Summary);
        foreach (string r in results) output.WriteLine(r);
    }

    /// <summary>The route-overflow trial: the shipped engine (OFF) against tuning.researchOverflow ON — nothing else differs
    /// (the lane variant is not used). Every ON run is replayed from its order log in a fresh executor, with a save/load
    /// round trip at turn 815, and the final hashes compared.</summary>
    [Sim.Tests.Systems.MeasurementFact("route overflow trial; research-cost-pass/overflow-trial.md")]
    public void RouteOverflowTrial_FourSeeds()
    {
        var jobs = new List<RunOptions>();
        foreach (ulong s in Seeds)
        {
            jobs.Add(new RunOptions(s, false, "off", Turns));
            jobs.Add(new RunOptions(s, true, "route", Turns, Replay: true, SaveLoadAt: Turns / 2));
        }
        RunAll([.. jobs]);
    }

    /// <summary>ADR-052 — gate-aware AI technology goals (research.json tuning.aiGateAwareGoals): overflow ON with the switch
    /// absent ("route", the trial's ON run) and with it on ("route-gate", replayed from its order log with a save/load round
    /// trip at mid-run); "off-gate" (overflow OFF, switch on) is optional context. Results:
    /// docs/adr/adr-052-ai-gate-aware-goals.md §4 (raw summaries: research-cost-pass/data/gate-aware/summaries.txt).</summary>
    [Sim.Tests.Systems.MeasurementFact("gate-aware AI technology goals; docs/adr/adr-052-ai-gate-aware-goals.md")]
    public void GateAwareGoals_FourSeeds()
    {
        var jobs = new List<RunOptions>();
        foreach (ulong s in Seeds)
        {
            jobs.Add(new RunOptions(s, true, "route", Turns));
            jobs.Add(new RunOptions(s, true, "route-gate", Turns, Replay: true, SaveLoadAt: Turns / 2, GateAware: true));
            jobs.Add(new RunOptions(s, false, "off-gate", Turns, GateAware: true));
        }
        RunAll([.. jobs]);
    }

    /// <summary>Director ruling 2026-10-26 — the COMBINED trial: the shipped defaults ("off") against the trial configuration
    /// ("trial": tuning.researchOverflow + the engine reserve tuning.researchReserve {capTurns 3, drawTurns 1} +
    /// tuning.aiGateAwareGoals), the trial without the gate rule (diagnostic), and the human proxy (re-target d turns after a
    /// lane empties) under both, d = 6 standing in for deliberate idling. Every trial run is replayed from its order log with
    /// a save/load round trip at mid-run. Node costs and repeatables are the shipped content's.</summary>
    [Sim.Tests.Systems.MeasurementFact("combined overflow + reserve + gate-aware trial; research-cost-pass/combined-trial.md")]
    public void CombinedTrial_FourSeeds()
    {
        var jobs = new List<RunOptions>();
        foreach (ulong s in Seeds)
        {
            jobs.Add(new RunOptions(s, false, "off", Turns));
            jobs.Add(new RunOptions(s, true, "trial", Turns, Replay: true, SaveLoadAt: Turns / 2, GateAware: true, EngineReserveCapTurns: 3.0));
            jobs.Add(new RunOptions(s, true, "trial-nogate", Turns, EngineReserveCapTurns: 3.0));
            jobs.Add(new RunOptions(s, false, "d1-off", Turns, HumanDelay: 1));
            jobs.Add(new RunOptions(s, false, "d3-off", Turns, HumanDelay: 3));
            jobs.Add(new RunOptions(s, true, "d1-trial", Turns, HumanDelay: 1, GateAware: true, EngineReserveCapTurns: 3.0));
            jobs.Add(new RunOptions(s, true, "d3-trial", Turns, HumanDelay: 3, GateAware: true, EngineReserveCapTurns: 3.0));
            jobs.Add(new RunOptions(s, true, "d6-trial", Turns, HumanDelay: 6, GateAware: true, EngineReserveCapTurns: 3.0));
        }
        RunAll([.. jobs]);
    }

    /// <summary>The empty-lane reserve study (a measurement model; the engine is unchanged): a human proxy that re-targets a
    /// lane d turns after it empties, with the RP of a technology lane that funds nothing discarded (today) or kept in a reserve
    /// capped at k turns of RP (d = 1: discard or uncapped; d = 3: discard, k = 1, 3, uncapped, and uncapped with a draw limit of one turn's RP). The AI
    /// reference (no delay, discard) is the trial's OFF run. Overflow OFF, so the
    /// reserve's effect is its own; one ON pairing (cap 3) shows how the two rules compose.</summary>
    [Sim.Tests.Systems.MeasurementFact("empty-lane reserve study; research-cost-pass/empty-lane-reserve-proposal.md")]
    public void EmptyLaneReserveStudy_FourSeeds()
    {
        var jobs = new List<RunOptions>();
        foreach (ulong s in Seeds)
        {
            // The rule would apply to every polity: the AI (no delay) under the same reserve is the fair reference.
            jobs.Add(new RunOptions(s, false, "ai-cap3", Turns, ReserveCapTurns: 3.0));
            jobs.Add(new RunOptions(s, false, "ai-uncapped", Turns, ReserveCapTurns: double.PositiveInfinity));
            jobs.Add(new RunOptions(s, true, "ai-cap3-route", Turns, ReserveCapTurns: 3.0));
            // LOSSY draws (overflow OFF): banked RP joins the step's points and obeys the same surplus rule, so banking is
            // never more efficient than spending — the AI and the delayed proxy under the same rule.
            jobs.Add(new RunOptions(s, false, "ai-cap3-lossy", Turns, ReserveCapTurns: 3.0, DrawLimitTurns: 1.0, DrawLosesSurplus: true));
            jobs.Add(new RunOptions(s, false, "d3-cap3-lossy", Turns, HumanDelay: 3, ReserveCapTurns: 3.0, DrawLimitTurns: 1.0, DrawLosesSurplus: true));
            jobs.Add(new RunOptions(s, false, "d1-cap3-lossy", Turns, HumanDelay: 1, ReserveCapTurns: 3.0, DrawLimitTurns: 1.0, DrawLosesSurplus: true));
            jobs.Add(new RunOptions(s, false, "ai-uncapped-lossy", Turns, ReserveCapTurns: double.PositiveInfinity, DrawLimitTurns: 1.0, DrawLosesSurplus: true));
            jobs.Add(new RunOptions(s, false, "d3-uncapped-lossy", Turns, HumanDelay: 3, ReserveCapTurns: double.PositiveInfinity, DrawLimitTurns: 1.0, DrawLosesSurplus: true));
            jobs.Add(new RunOptions(s, false, "d1-discard", Turns, HumanDelay: 1));
            jobs.Add(new RunOptions(s, false, "d1-uncapped", Turns, HumanDelay: 1, ReserveCapTurns: double.PositiveInfinity));
            jobs.Add(new RunOptions(s, false, "d3-discard", Turns, HumanDelay: 3));
            jobs.Add(new RunOptions(s, false, "d3-cap1", Turns, HumanDelay: 3, ReserveCapTurns: 1.0));
            jobs.Add(new RunOptions(s, false, "d3-cap3", Turns, HumanDelay: 3, ReserveCapTurns: 3.0));
            jobs.Add(new RunOptions(s, false, "d3-uncapped", Turns, HumanDelay: 3, ReserveCapTurns: double.PositiveInfinity));
            jobs.Add(new RunOptions(s, false, "d3-uncapped-draw1", Turns, HumanDelay: 3, ReserveCapTurns: double.PositiveInfinity, DrawLimitTurns: 1.0));
            jobs.Add(new RunOptions(s, true, "d3-cap3-route", Turns, HumanDelay: 3, ReserveCapTurns: 3.0));
            jobs.Add(new RunOptions(s, true, "d3-discard-route", Turns, HumanDelay: 3));
        }
        RunAll([.. jobs]);
    }
}
