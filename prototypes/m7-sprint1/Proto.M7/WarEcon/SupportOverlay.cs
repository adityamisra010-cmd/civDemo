namespace Proto.M7.WarEcon;

/// <summary>
/// [D] The panel-managed support attached to one army for one War Pulse (lane D; DIR5 §2.12, D-079 DR29: "Support units are
/// primarily panel-managed capabilities, not physical battlefield entities"; category B, D-065 R10). Counts only: the support
/// layer owns the accounts. <see cref="Staff"/> are the support personnel with the army (medical staff and train drivers):
/// they eat the field ration (and the march extra when it marches) and draw a foot soldier's pay. <see cref="Wagons"/> and
/// <see cref="DraftAnimals"/> join the army's own train for the pulse (train capacity, supply-line fleet, march speed, fodder).
/// <see cref="Patients"/> are its wounded in care: they eat the base ration only.
/// </summary>
public readonly record struct SupportComposition(long Staff, long Wagons, long DraftAnimals, long Patients)
{
    public static readonly SupportComposition None = default;
    public bool IsNone => Staff == 0 && Wagons == 0 && DraftAnimals == 0 && Patients == 0;
}

/// <summary>
/// [D] The seam through which a support-capability layer attaches support to the war economy WITHOUT a second supply model:
/// the war economy calls <see cref="Latch"/> once per army at the army's pulse start, before planning the pulse, in its
/// (Priority, Id) order, and plans the pulse with the returned support (lane E's own logistics arithmetic). Absent
/// (<see cref="WarEconSetup.Support"/> null) the war economy is byte-for-byte what it was (tested).
/// </summary>
public interface ISupportOverlay
{
    /// <summary>The support in force for army <paramref name="armyId"/> over the pulse starting at <paramref name="pulseStartMs"/>.
    /// The overlay may move its OWN assets between its own accounts of the same ledger at this instant.</summary>
    SupportComposition Latch(int armyId, int owner, int warId, long pulseIndex, long pulseStartMs, long menAtStart, bool naval);
}
