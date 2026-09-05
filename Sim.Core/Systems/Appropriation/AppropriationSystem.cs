using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Appropriation;

/// <summary>Owned tables: none. Appropriation moves grain that already exists
/// between two settlements' stock rows and publishes no table of its own.</summary>
public readonly record struct AppropriationTables(Table<GoodStockRow> Stocks);

/// <summary>
/// T4.5 (D-037 B3): NON-STATE PEOPLES TAKE GRAIN WHEN THEIR OWN SUBSISTENCE FAILS.
///
/// D-037 B3 is explicit about the shape and about what must NOT exist: "RAIDING
/// EMERGES FROM THEIR SUBSISTENCE FAILING — no raid timer, no aggression stat."
/// This system therefore has no timer, no cooldown, no aggression stat, no
/// randomness and no state. It is a pure function of the PREV world.
///
/// THE CAUSAL CHAIN, end to end, entirely out of mechanisms that already existed:
///
///   bad harvest weather (HarvestWeatherRow, T3.4b)
///     -> reduced HERDING subsistence (ProductionSystem.FromDeposits, coupled at T4.5)
///     -> less food obtained than the nutritional requirement
///     -> ConsumptionSystem publishes ConsumptionDeficitRow (ratio, DemandUnits)
///     -> THIS system reads that deficit and appropriates the shortfall
///     -> Ledger.Transfer, which conserves by construction
///
/// D-037 B3's own sentence is the design: "Steppe raiding historically correlates
/// with drought, which is exactly the T3.4b harvest-variance driver: the same bad
/// year that starves villages sends herders after grain."
///
/// HOW MUCH OF THAT SENTENCE THIS PACKET ACTUALLY DELIVERS, measured rather than
/// asserted, because an independent review found the obvious reading too strong.
/// The chain above is real link by link, but the WEATHER link only reaches the
/// deficit where food-from-deposits output is BINDING. It is not binding for the
/// settlements this system selects: the consumption basket is grain-dominant
/// (needs.json: grain 0.9, livestock 0.06, fish 0.04) and a surplus in one good
/// does not cover a shortfall in another, so a herding-dominant settlement is
/// short by roughly the grain share in EVERY year, drought or not, while its
/// livestock output sits far above the 0.06 it needs. Consequently:
///   - a pure pastoralist's DeficitRatio is a near-constant of the basket, not a
///     weather signal, and it appropriates in a self-correcting alternation
///     (take, eat, be fed, take again) that owes nothing to the year;
///   - the drought sensitivity that IS observable end to end runs through the
///     FARMING half of a mixed settlement, which had its weather multiplier
///     before T4.5.
/// What T4.5's coupling changes for certain is total food OUTPUT in every
/// weather-bearing world — enough to move both behavioural goldens and to trip
/// the CR-003 famine tripwire in ClassSystemTests. Closing the gap between "the
/// raid responds to the year" and "the raid responds to the basket" needs diet
/// substitution or a pastoralist grain trade, neither of which is T4.5's design.
/// It is written down in docs/queue.md and docs/t4.5-review-record.md rather
/// than left implied by this comment.
///
/// NO SECOND DEFINITION OF HUNGER. The trigger is ConsumptionSystem's existing
/// deficit row, read from PREV like every other cross-system signal (§3.2's
/// one-turn lag). Nothing here re-derives whether a settlement is short of food.
///
/// NO NEW CONSTANT. The amount taken is the settlement's OWN measured shortfall,
/// `DeficitRatio x DemandUnits` — both already on the row. There is no raid size,
/// no greed factor and no threshold to tune: a settlement that is 1% short takes
/// 1% of its requirement, and a settlement that is not short takes nothing.
///
/// DIMENSIONS, and the one seam in them (law 3). `DemandUnits` is not a rate: it
/// is already dt-integrated where it is published (ConsumptionSystem computes
/// `persons x perPersonPerYear x dtYears`), so the amount taken scales linearly
/// with dt in steady state — measured 3000 units at dt=10 against 1500 at dt=5,
/// ratio exactly 2. There is nothing here to integrate again, and re-deriving
/// the requirement locally would be a SECOND definition of hunger.
/// The seam is at the one-turn lag: the row read from PREV was integrated under
/// the PREVIOUS turn's dt, so on the single turn where era pacing changes dt
/// (era-pacing.json steps 10 -> 5 -> 3 -> 2 -> 1 -> 0.5) a raider takes
/// old_dt/new_dt times the shortfall of the turn it is now in — 2x at the first
/// boundary. It is one turn per band, it is not a compounding error, and every
/// fix costs more than it buys at M4: publishing a per-year demand rate widens a
/// serialized row, and carrying the previous dt on the row does the same. It is
/// recorded in docs/queue.md rather than papered over here.
///
/// WHO RAIDS: a settlement with NO ControlRow — the stateless case that
/// ControlRow's own contract already provides for ("exactly one state control
/// row, OR none"). No new table, no new population carrier, no new polity type,
/// no schema change.
///
/// NOTE FOR THE READER, because it is load-bearing AND because the ground moved
/// under it. When T4.5 shipped, nothing in Sim.Core wrote Controls (T4.3 shipped
/// the table as schema only), so statelessness alone was true of EVERY settlement
/// and the herding-dominance condition below was the only thing narrowing the
/// trigger. **M4-C changed that fact.** `WorldFounding.FoundInitialEmpire` now
/// writes one ControlRow per founded settlement, so in a founded world NO
/// worldgen settlement is stateless and `IsStateless` is false for all of them.
///
/// The consequence is worth stating plainly rather than leaving to be
/// rediscovered: this system now has TWO independent blockers in a founded world,
/// not one. Even if worldgen later placed herding-dominant pastoralists exactly as
/// D-037 B3 requires, every settlement worldgen founds also carries a control row,
/// so no raid could fire. The queued worldgen work therefore NO LONGER SUFFICES to
/// bring this mechanism to life; whether colonised or worldgen-placed settlements
/// should be stateless is a D-037 B1/B3-vs-D-042 question that is not this
/// system's to answer. Recorded in docs/queue.md.
///
/// The colonization path is the one route that still produces a stateless
/// settlement, because `ColonizationSystem` writes no control row.
///
/// WHO IS RAIDED: the OTHER settlement holding the most grain, tie-broken to the
/// LOWEST settlement id by the strictly-greater scan over ascending rows — the
/// composite (stock desc, id asc) key the constitution requires of every argmax.
/// Wealth, not distance: this system introduces no travel model.
///
/// RESISTANCE IS OUT OF SCOPE (T4.8 owns conflict). The taking is unopposed, and
/// the victim's only protection is arithmetic: the transfer clamps to what is
/// actually there, so a stock can reach zero but never go negative.
/// </summary>
public sealed class AppropriationSystem(SimConfig cfg) : ISimSystem<AppropriationTables>
{
    public static readonly SystemId WellKnownId = new(16);
    public const string Name = "appropriation";

    private readonly GoodId _grain = new(cfg.Goods?.GrainId
        ?? throw new ArgumentNullException(nameof(cfg), "goods config is required for appropriation"));

    public SystemId Id => WellKnownId;

    public void Step(SimContext<AppropriationTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<GoodStockRow> stocks = ctx.Owned.Stocks;

        // Fewer than two settlements: nobody to take from. Also the M0/toy worlds.
        if (prev.Settlements.Count < 2) return;

        // Settlement-major ascending: the iteration order IS the tie-break order,
        // and it is an array scan, never a dictionary walk (law 5).
        for (int r = 0; r < prev.Settlements.Count; r++)
        {
            SettlementId raider = prev.Settlements[r].Id;
            if (!IsStateless(prev, raider)) continue;
            if (!IsHerdingDominant(prev, raider)) continue;

            double ratio = 0.0;
            long demandUnits = 0;
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
            {
                if (prev.ConsumptionDeficits[i].Settlement != raider) continue;
                ratio = prev.ConsumptionDeficits[i].DeficitRatio;
                demandUnits = prev.ConsumptionDeficits[i].DemandUnits;
                break;
            }
            if (ratio <= 0.0 || demandUnits <= 0) continue; // fed: no raid, by construction

            long wanted = ConservedMath.WholeUnits(
                ratio * demandUnits, $"appropriation by settlement {raider.Value}");
            if (wanted <= 0) continue; // a shortfall under one whole unit takes nothing

            int raiderRow = GoodStockIndex.IndexOf(stocks, raider, _grain);
            if (raiderRow < 0) continue; // nowhere to put it

            int victimRow = RichestOtherGrainRow(stocks, prev, raider);
            if (victimRow < 0) continue; // nobody held any grain when the turn began

            // ClampToAvailable, never Throw: the victim can be emptied but can
            // never be overdrawn, and the return value reports what truly moved.
            ctx.Ledger.Transfer(
                ref stocks.Ref(victimRow).Amount, ref stocks.Ref(raiderRow).Amount,
                wanted, OverdrawPolicy.ClampToAvailable);
        }
    }

    /// <summary>
    /// D-037 B3's carrier: a settlement no polity controls. ControlRow's contract
    /// already admits "or none"; statelessness is that absence, not a new field.
    /// </summary>
    private static bool IsStateless(IReadOnlyWorldState prev, SettlementId settlement)
    {
        for (int i = 0; i < prev.Controls.Count; i++)
            if (prev.Controls[i].Place == settlement) return false;
        return true;
    }

    /// <summary>
    /// D-037 B3's SUBJECT: "Pastoralists, hunter-gatherers and other stateless
    /// populations occupy marginal terrain that farming settlements do not claim."
    /// Non-state peoples are not merely uncontrolled — they live off herds, which
    /// is why a drought reaches them at all. A settlement whose largest sector is
    /// HERDING is that people; a village that happens to have no control row is not.
    ///
    /// WHY THIS CONDITION IS HERE, stated because it narrows the trigger. When this
    /// was written nothing in Sim.Core wrote Controls, so statelessness ALONE was
    /// true of every settlement (M4-C has since changed that — see the type header);
    /// raiding on statelessness alone made every village in
    /// the world take grain from its hungriest neighbour — measured, not supposed:
    /// it emptied CollapseStabilityTests' food-less-death precondition, turning an
    /// ADR-012 resurrection guard vacuous. That is famine relief for everyone, not
    /// D-037 B3's herders, and it is not what the decision describes.
    ///
    /// It introduces NO constant and NO new state: `Sectors.Raw` already exists and
    /// "largest sector" is a comparison, not a threshold. `Sectors.Default` is
    /// Farming 0.55 / Herding 0.15 / Extraction 0.10 / Crafting 0.12 /
    /// Construction 0.08 — farming-dominant — so an ordinary village is excluded by
    /// the same data that already governs its labour, and a settlement with NO
    /// allocation row at all is excluded too.
    /// </summary>
    private static bool IsHerdingDominant(IReadOnlyWorldState prev, SettlementId settlement)
    {
        for (int i = 0; i < prev.SectorAllocations.Count; i++)
        {
            if (prev.SectorAllocations[i].Settlement != settlement) continue;
            SectorAllocationRow row = prev.SectorAllocations[i];
            double herding = Sectors.Raw(row, Sectors.Herding);
            for (int sector = 0; sector < Sectors.Count; sector++)
            {
                if (sector == Sectors.Herding) continue;
                // Strictly-greater: a tie is NOT herding-dominant, so a settlement
                // split evenly between plough and herd stays a farming village.
                if (Sectors.Raw(row, sector) >= herding) return false;
            }
            return herding > 0.0;
        }
        // No allocation row at all. ProductionSystem substitutes Sectors.Default
        // for a missing row, and that default is farming-DOMINANT (not, as an
        // earlier version of this comment said, all-farming — it has herded since
        // T3.5b), so the settlement is not herding-dominant either way.
        return false;
    }

    /// <summary>
    /// The other settlement holding the most grain, under the EXPLICIT composite
    /// key (stock DESC, settlement id ASC) the constitution requires of every
    /// argmax. No floating-point comparison anywhere — grain stocks are longs.
    ///
    /// THE KEY IS COMPARED, NOT ASSUMED. An earlier version relied on a
    /// strictly-greater scan and called that the composite key, which is only
    /// true while `Settlements` happens to be stored in ascending id order: with
    /// rows in any other order a tie went to the FIRST ROW SCANNED instead of the
    /// lowest id, and the tie-dense test could not see it because it built its
    /// rows ascending too. Independent review caught it; the test now builds
    /// descending and the id is compared here outright, so the outcome no longer
    /// depends on the table's storage order at all.
    /// </summary>
    private int RichestOtherGrainRow(
        Table<GoodStockRow> stocks, IReadOnlyWorldState prev, SettlementId raider)
    {
        int best = -1;
        long bestAmount = 0;
        int bestId = 0;
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId victim = prev.Settlements[s].Id;
            if (victim == raider) continue;
            // WEALTH IS READ FROM **PREV**, the amount held when the turn began —
            // the row it is MOVED FROM is the live one. Selecting on the live
            // table let two hungry herders rob each other inside a single pass:
            // the first emptied the second, and the second, now finding the first
            // rich, took it straight back, leaving the world bit-identical, nobody
            // relieved, and the outcome decided by settlement row order. Reading
            // PREV kills that: a settlement that began the turn with nothing is
            // never chosen, however much it has just been handed.
            int prevRow = GoodStockIndex.IndexOf(prev.GoodStocks, victim, _grain);
            if (prevRow < 0) continue;
            long amount = prev.GoodStocks[prevRow].Amount.Value;
            if (amount <= 0) continue;
            // (stock DESC, id ASC), both halves compared explicitly.
            if (best >= 0 && (amount < bestAmount
                || (amount == bestAmount && victim.Value > bestId))) continue;
            int liveRow = GoodStockIndex.IndexOf(stocks, victim, _grain);
            if (liveRow < 0) continue; // nowhere to take it from
            bestAmount = amount; bestId = victim.Value; best = liveRow;
        }
        return best;
    }
}
