namespace Proto.M7.Econ;

/// <summary>
/// How the calibration turns MEASURED numbers into <see cref="LogisticsPriority.Resolve"/> inputs. Three input choices are
/// open modelling questions, and each one can flip the allocator's verdict on the calibration worlds (verification F1:
/// measured on the turn-25 rows of seeds 1, 2, 42, 7). None is a default the repository has ratified; the calibration
/// prints all readings side by side.
/// <list type="bullet">
/// <item><see cref="NetOfSpoilage"/>: false = free is grain stock + last step's grain harvest (PROTOTYPE ASSUMPTION: next
/// harvest ≈ last); true = that minus the last step's grain spoilage (world ledger, reason Spoilage), on the reading that
/// the next step will spoil about as much.</item>
/// <item><see cref="GrainOnlyRequirement"/>: false = R is <c>ConsumptionDeficits.DemandUnits</c>, the ALL-FOOD requirement
/// (grain + livestock + fish) although free is grain only; true = R is the last step's grain actually eaten (world ledger,
/// reason Eaten), a grain-only requirement.</item>
/// <item><see cref="HaulOnlyDraw"/>: false = a move draws its whole bill (<see cref="MovementEstimate.TotalUnits"/>: rations
/// + haul); true = it draws only the haul (<see cref="MovementEstimate.IncrementalUnits"/>), on the reading that the rations
/// are standing upkeep MilitarySystem pays anyway.</item>
/// </list>
/// The world-ledger readings are per WORLD, not per polity: they are the player's only when the player is the world's
/// only polity (the EconWorld calibration world, aiEmpires 0). The caller must check that.
/// </summary>
public readonly record struct AllocatorReading(bool NetOfSpoilage, bool GrainOnlyRequirement, bool HaulOnlyDraw)
{
    /// <summary>The reading the first calibration report used (all three false).</summary>
    public static AllocatorReading AsShipped => default;

    /// <summary>A short label for the report table.</summary>
    public string Label =>
        !NetOfSpoilage && !GrainOnlyRequirement && !HaulOnlyDraw ? "as shipped (free = stock + harvest; R = all-food; draw = rations + haul)"
        : (NetOfSpoilage ? "free − spoilage; " : "") + (GrainOnlyRequirement ? "R = grain eaten; " : "") + (HaulOnlyDraw ? "draw = haul only; " : "");
}

/// <summary>The three scalar allocator inputs (grain units).</summary>
public readonly record struct AllocatorInputs(long Free, long CivilianRequirement, long GrowthDemand);

/// <summary>Pure functions from measurements to allocator inputs (no world access, no state).</summary>
public static class MeasuredAllocation
{
    /// <summary>The allocator inputs for <paramref name="player"/> under <paramref name="reading"/>.
    /// <paramref name="worldLastStep"/> is the world ledger's flows over the step that produced the snapshot.</summary>
    public static AllocatorInputs Inputs(PolitySnapshot player, LedgerTotals worldLastStep, AllocatorReading reading)
    {
        // PROTOTYPE ASSUMPTION: the next step's harvest ≈ the last step's.
        long free = checked(player.GrainStock + player.GrainProducedLastStep);
        if (reading.NetOfSpoilage) free = Math.Max(0, free - worldLastStep.Spoilage);
        long requirement = reading.GrainOnlyRequirement ? worldLastStep.GrainEaten : player.CivilianRequirementLastStep;
        // G = Σ FoodHeadroom.Vacancy × dt (unchanged from the first report; an unbounded headroom is reported as 0 here).
        long growth = player.GrowthHeadroomUnbounded ? 0 : MovementFood.CeilUnits(player.GrowthHeadroomPersonsPerYear * player.DtYears);
        return new AllocatorInputs(free, requirement, growth);
    }

    /// <summary>The move request for a column with bill <paramref name="e"/>: draw per <paramref name="reading"/>, days =
    /// the whole days the march takes (at least 1).</summary>
    public static MoveRequest Move(long id, int priority, MovementEstimate e, AllocatorReading reading) =>
        new(id, priority, reading.HaulOnlyDraw ? e.IncrementalUnits : e.TotalUnits, Math.Max(1, (long)Math.Ceiling(e.TravelDays)));

    /// <summary>Resolve the allocator on measured numbers under one reading.</summary>
    public static LogisticsDecision Resolve(PolitySnapshot player, LedgerTotals worldLastStep, AllocatorReading reading,
        ReadOnlySpan<(long Id, int Priority, MovementEstimate Bill)> columns)
    {
        AllocatorInputs x = Inputs(player, worldLastStep, reading);
        var moves = new MoveRequest[columns.Length];
        for (int i = 0; i < columns.Length; i++) moves[i] = Move(columns[i].Id, columns[i].Priority, columns[i].Bill, reading);
        return LogisticsPriority.Resolve(x.Free, x.CivilianRequirement, x.GrowthDemand, moves);
    }

    /// <summary>The readings the calibration prints: as shipped, each single change, and all three together.</summary>
    public static AllocatorReading[] ReportedReadings =>
    [
        AllocatorReading.AsShipped,
        new(false, true, false),
        new(true, false, false),
        new(false, false, true),
        new(true, true, true),
    ];
}
