using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Revolt;

/// <summary>Tables owned by <see cref="RevoltSystem"/>. `Controls` is a SANCTIONED
/// SHARED table — Colonization appends to it (inheriting a founder's control) and
/// this system removes from it. They never touch the same row in the same turn:
/// colonization writes rows for settlements created THIS turn, which cannot yet
/// have a Prev happiness reading.</summary>
public readonly record struct RevoltTables(Table<ControlRow> Controls);

/// <summary>
/// M4 — REVOLT: A SETTLEMENT AT ZERO HAPPINESS STOPS OBEYING.
///
/// THE RULE, and it is one line: a settlement whose derived happiness is exactly
/// zero loses its control relation. No new table, no ownership flag, no second
/// identity — the settlement simply stops appearing in `Controls`, which is what
/// "no polity commands this place" already means under D-037 A3 ("exactly one
/// state control row, OR none").
///
/// WHY THIS SYSTEM EXISTS AT ALL, stated plainly because it is the only thing in
/// M4 that can produce an uncontrolled settlement in a live world. Before it,
/// `WorldFounding` wrote a control row for every settlement it founded and
/// nothing ever removed one, so a founded world had zero stateless settlements
/// for its whole 6,000 years — which silently made T4.5's appropriation
/// mechanism unreachable (its raider must be stateless) and left D-037 B3's
/// non-state peoples with no way to come into being. This closes that loop from
/// the SIMULATION side rather than by seeding fake uncontrolled settlements into
/// worldgen: statelessness is now something a world can ARRIVE at, by governing
/// a place so badly that it stops being governed.
///
/// ZERO IS TOTAL DEPRIVATION, NOT A MOOD. <see cref="SettlementHappiness"/> is
/// anchored so that zero is reachable only when every measured condition is at
/// zero — an unfed AND unhoused population. This is deliberately not a "low
/// happiness" band with a tunable threshold: a band would be a policy knob
/// inviting tuning, while the ruled condition is a corner of the state space.
///
/// WHAT IT DOES NOT DO. It does not transfer control to another polity, does not
/// create a rebel polity, does not fight, and does not touch population, goods or
/// any other stock — a revolt here is the LOSS of a relation and nothing else.
/// Who, if anyone, picks the place up afterwards is M5's politics and M6's war;
/// this is the smallest mechanism that makes the state reachable, which is what
/// the M4 boundary allows.
///
/// SIGNALS ARE ALL FROM PREV (§3.2), so the reading that condemns a settlement is
/// the one every other system saw this turn, and the outcome cannot depend on
/// where this system sits in the pipeline.
/// </summary>
public sealed class RevoltSystem(SimConfig cfg) : ISimSystem<RevoltTables>
{
    public static readonly SystemId WellKnownId = new(21);
    public const string Name = "revolt";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<RevoltTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<ControlRow> controls = ctx.Owned.Controls;
        if (controls.Count == 0) return;

        // Pass 1: WHICH settlements revolt. Iterated in Settlements table order —
        // an array walk, never a set (law 5) — so the answer is a function of the
        // world and not of hash order.
        Span<bool> revolts = prev.Settlements.Count <= 64
            ? stackalloc bool[prev.Settlements.Count]
            : new bool[prev.Settlements.Count];

        bool any = false;
        for (int i = 0; i < prev.Settlements.Count; i++)
        {
            SettlementId place = prev.Settlements[i].Id;
            if (!EmpireQuery.TryGetController(prev, place, out _)) continue;  // already stateless
            if (!SettlementHappiness.IsRevoltReady(prev, place, _cfg)) continue;
            revolts[i] = true;
            any = true;
        }

        if (!any) return;   // the common case writes NOTHING, so no world moves

        // Pass 2: rebuild without the revolted places. `Table` is Add/Clear only,
        // and rebuilding preserves the relative order of every surviving row —
        // which matters because the canonical stream serializes this table in
        // row order and a reshuffle would move every world hash for no reason.
        var kept = new List<ControlRow>(controls.Count);
        for (int i = 0; i < controls.Count; i++)
        {
            ControlRow row = controls[i];
            if (IsRevolting(prev, revolts, row.Place)) continue;
            kept.Add(row);
        }

        controls.Clear();
        for (int i = 0; i < kept.Count; i++) controls.Add(kept[i]);
    }

    private static bool IsRevolting(IReadOnlyWorldState prev, ReadOnlySpan<bool> revolts, SettlementId place)
    {
        for (int i = 0; i < prev.Settlements.Count; i++)
            if (prev.Settlements[i].Id.Value == place.Value) return revolts[i];
        return false;
    }
}
