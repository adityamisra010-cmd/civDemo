using Sim.Core.Observability;
using Sim.Core.State;

namespace Sim.Ui.Tests;

/// <summary>
/// T4.21-5 — the two APPENDED settlement-record sections (spec §3.11), built by
/// hand for the view-model tests. These rigs are NOT the observer: they carry
/// whatever the test needs the UI to render, which is exactly why the UI tests
/// can pin the rendering of a FAMINE row without stepping a world into one.
/// The values here are therefore FIXTURES, never evidence about the simulation.
/// </summary>
internal static class RecordSections
{
    /// <summary>A settlement with nothing exceptional: NORMAL, no disaster, no
    /// weather row, unbounded headroom (FoodHeadroom's own null-arm value).</summary>
    internal static FoodStateSection Normal(double nominalDeficit = 0.0) => new(
        PrevRowsPresent: true,
        State: nominalDeficit > 0.0 ? FoodStateKind.Stress : FoodStateKind.Normal,
        Reason: FamineReason.None,
        NominalDeficit: nominalDeficit,
        EffectiveDeficit: 0.0,
        Abandoned: false,
        DisasterRowPresent: false, DisasterKind: 0, DisasterSeverity: 0.0,
        DisasterMultiplierApplied: 1.0, DisasterRemainingYears: 0.0,
        DisasterPendingRowPresent: false, DisasterPendingKind: 0, DisasterPendingSeverity: 0.0,
        DisasterPendingMultiplier: 1.0, DisasterPendingRemainingYears: 0.0,
        HarvestWeatherRowPresent: false, HarvestWeatherApplied: double.NaN,
        FoodLimit: double.PositiveInfinity, Vacancy: double.PositiveInfinity,
        SurplusRatio: double.NaN, Headroom: double.PositiveInfinity);

    /// <summary>A settlement in FAMINE for the named reason, struck by a
    /// disaster of the given severity and applied multiplier.</summary>
    internal static FoodStateSection Famine(
        FamineReason reason, double nominalDeficit, double severity, double multiplier,
        double remainingYears = 0.0, bool abandoned = false) => Normal() with
    {
        State = FoodStateKind.Famine,
        Reason = reason,
        NominalDeficit = nominalDeficit,
        EffectiveDeficit = nominalDeficit,
        Abandoned = abandoned,
        DisasterRowPresent = multiplier < 1.0,
        DisasterKind = multiplier < 1.0 ? 1 : 0,
        DisasterSeverity = severity,
        DisasterMultiplierApplied = multiplier,
        DisasterRemainingYears = remainingYears,
    };

    /// <summary>A planner readout with nothing moving.</summary>
    internal static MigrationPlanSection NoPlan(double dtYears = 10.0) => new(
        PlanRecorded: true, DtYears: dtYears,
        ExitOpenness: 0.0, FlightFractionPrime: 0.0, FlightBound: 0.0, FlightOut: 0.0,
        GapOut: 0.0, GapOutflowCap: double.PositiveInfinity, SrcScale: 1.0,
        FlightIn: 0.0, GapIn: 0.0, GapInflowCap: double.PositiveInfinity, DestScale: 1.0,
        VacancyCap: double.PositiveInfinity, DesiredInflowAe: 0.0, VacancyScale: 1.0,
        InflowAeByChannel: "GAP: none");
}
