using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Revolt;

/// <summary>Tables owned by <see cref="RevoltSystem"/>. `Controls` is a SANCTIONED
/// SHARED table — Colonization appends to it (inheriting a founder's control) and
/// this system removes from it. They never touch the same row in the same turn:
/// colonization writes rows for settlements created THIS turn, which cannot yet
/// have a Prev happiness reading.
///
/// R3 (Director R2-final §2): a revolt also FOUNDS the new polity — it appends one
/// <see cref="PolityRow"/> (CommandSource.Ai) per revolted settlement (Polities has no
/// other writer after founding), and copies the former polity's knowledge into it by
/// APPENDING <see cref="ResearchCompletedRow"/>s (<see cref="KnowledgeTransfer"/>).
/// ResearchCompleted is a SANCTIONED SHARED table split by pipeline order and row key:
/// revolt (before research) appends rows ONLY for a polity it creates this turn, a key
/// absent from Prev, so ResearchSystem — which works from Prev's roster and appends its
/// own completions — never reads or writes the same row.</summary>
public readonly record struct RevoltTables(
    Table<ControlRow> Controls, Table<PolityRow> Polities, Table<ResearchCompletedRow> ResearchCompleted);

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
/// ADR-033 D4 adds the second corner: the M5 tax burden multiplies the reading
/// (SettlementHappiness.TaxSufficiency), so a declared 100 % levy at full reach
/// (the capital, Strength 1.0) also reads exactly zero — total extraction.
///
/// M5 R2b — THE THIRD PATH, UPRISING (D-021 unrest-lite, D-009/D-010's
/// "discontent → protest → uprising"): a controlled settlement whose PREV grievance
/// stands at or above needs.json <c>unrest.uprisingGrievance</c>
/// (<see cref="Unrest.IsUprising"/>) also throws off its ruler. This is the "revolt
/// reachable before total deprivation" the Director asked for, and it is NOT a
/// happiness band: it reads the grievance MEMORY stock, which has to be accrued over
/// years of unmet needs (since R2b, Dignity injured by the levy, D-035-D) against
/// generational decay and protest's own discharge — so it is history, not a mood
/// reading that flicks with one turn's policy. The two zero corners above stand
/// unchanged. Inert without the unrest section.
///
/// IS LOSING LABOUR ORDERS THE INTENDED CONSEQUENCE? Yes (R2b decision, documented in
/// docs/m5-integration-coherence-matrix.md §6): every order domain asks the D-037
/// control relation (LabourActivities.CanAllocate, ConstructionQuery, OrderValidation),
/// so a revolted settlement refuses its former ruler's labour and construction orders.
/// It is not a deadlock of the WORLD: the settlement keeps its people, stocks, standing
/// allocation and production, grows, and — once its grievance discharges — is quiet; it
/// is simply nobody's to command. Re-annexation, reconquest and capital succession have
/// no ratified mechanism in M5 (M6 war / later politics) and are not invented here.
///
/// R3 — THE REVOLTED SETTLEMENT BECOMES A NEW AI-CONTROLLED POLITY (Director R2-final §2,
/// RATIFIED; the Singapore/Malaysia model). At the instant of separation the settlement's
/// control row passes to a NEW polity (id = one above every id in the roster, settlement-table
/// order when several revolt at once; CommandSource.Ai), and that polity receives a COMPLETE
/// copy of its former polity's completed knowledge (<see cref="KnowledgeTransfer.MergeInto"/>).
/// Nothing is filtered and nothing is removed from the parent; afterwards the two research
/// independently (no link is kept). The new polity has NO capital row — a capital-less Empire
/// is representable (M4-A) and designating a seat would be a succession rule nobody ratified
/// (§5, DEFERRED). INFERRED (R3): a settlement whose controller holds no OTHER place does not
/// revolt — a polity that is only that place has no ruler for it to throw off (without this a
/// grievance still above the uprising line would split the same settlement again every turn).
///
/// WHAT IT DOES NOT DO. It does not fight, does not move control to an EXISTING polity, and
/// does not touch population, goods or any other stock.
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
            if (!EmpireQuery.TryGetController(prev, place, out PolityId ruler)) continue;  // already stateless
            if (EmpireQuery.ControlledCount(prev, ruler) <= 1) continue;  // it IS its polity (D-048 ruling 5, RATIFIED)
            if (!SettlementHappiness.IsRevoltReady(prev, place, _cfg)
                && !Unrest.IsUprising(prev, place, _cfg)) continue;
            revolts[i] = true;
            any = true;
        }

        if (!any) return;   // the common case writes NOTHING, so no world moves

        // D-048 ruling 5 (RATIFIED 2026-10-04): a civilization's FINAL settlement cannot revolt away — and that
        // holds when all of a ruler's places rise on the SAME turn, which the per-place guard above cannot see.
        // A ruler every one of whose places is marked keeps one: its capital if it has one among them, else the
        // first of them in settlement-table order (an array walk, law 5).
        for (int i = 0; i < prev.Settlements.Count; i++)
        {
            if (!revolts[i]) continue;
            EmpireQuery.TryGetController(prev, prev.Settlements[i].Id, out PolityId ruler);
            if (!LosesEveryPlace(prev, revolts, ruler)) continue;
            int keep = i;
            if (EmpireQuery.TryGetCapital(prev, ruler, out SettlementId seat))
                for (int j = 0; j < prev.Settlements.Count; j++)
                    if (revolts[j] && prev.Settlements[j].Id.Value == seat.Value) { keep = j; break; }
            revolts[keep] = false;
        }

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

        // Pass 3 (R3): each revolted place, in settlement-table order, becomes a new AI polity
        // holding a complete copy of its former ruler's knowledge at this instant.
        int nextId = 0;
        for (int i = 0; i < ctx.Owned.Polities.Count; i++)
            nextId = Math.Max(nextId, ctx.Owned.Polities[i].Id.Value);
        for (int i = 0; i < prev.Settlements.Count; i++)
        {
            if (!revolts[i]) continue;
            SettlementId place = prev.Settlements[i].Id;
            EmpireQuery.TryGetController(prev, place, out PolityId former);
            var founded = new PolityId(checked(++nextId));
            ctx.Owned.Polities.Add(new PolityRow(founded, CommandSource.Ai));
            controls.Add(new ControlRow(founded, place, 1.0));
            KnowledgeTransfer.MergeInto(ctx.Owned.ResearchCompleted, former, founded);
        }
    }

    /// <summary>Whether every place <paramref name="ruler"/> controls is marked to revolt.</summary>
    private static bool LosesEveryPlace(IReadOnlyWorldState prev, ReadOnlySpan<bool> revolts, PolityId ruler)
    {
        for (int j = 0; j < prev.Settlements.Count; j++)
        {
            if (revolts[j]) continue;
            if (EmpireQuery.TryGetController(prev, prev.Settlements[j].Id, out PolityId other)
                && other.Value == ruler.Value) return false;
        }
        return true;
    }

    private static bool IsRevolting(IReadOnlyWorldState prev, ReadOnlySpan<bool> revolts, SettlementId place)
    {
        for (int i = 0; i < prev.Settlements.Count; i++)
            if (prev.Settlements[i].Id.Value == place.Value) return revolts[i];
        return false;
    }
}
