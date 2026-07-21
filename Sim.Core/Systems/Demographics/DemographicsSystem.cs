using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Core.Systems.Demographics;

/// <summary>Writable handles to DemographicsSystem's own tables (built by SystemCatalog only).</summary>
public readonly record struct DemographicsTables(Table<PopBandRow> PopBands);

/// <summary>
/// Demographics (T1.5): births, mortality, starvation, and band aging — every
/// person moving exclusively through the Ledger (law 1). All rates are per-sim-
/// year, computed from PREV counts (§3.2), integrated linearly with dtYears
/// (law 3; the dt-halving test characterizes the integration error), and
/// converted to whole people through per-row D-004 remainder accumulators.
///
/// OPERATION ORDER IS PINNED (and is a semantics surface — under famine the
/// ClampToAvailable floors make outcomes depend on it): per settlement,
///   1. Births      — source into children (reason Births), rate × PREV adults.
///   2. Base deaths — sink per band (reason Deaths), band rate × PREV count.
///   3. Starvation  — sink per band (reason Starvation), max rate × PREV
///      deficit ratio × PREV count. The deficit is LAST turn's Consumption
///      output (one-turn lag, §3.2 — documented and accepted).
///   4. Aging       — Ledger.Transfer children→adults (rate 1/15·yr⁻¹) and
///      adults→elders (1/45·yr⁻¹); Transfer conserves by construction, which
///      is why the person-exact reconciliation needs no Aging reason.
///
/// FLOORS (documented): every sink and transfer uses ClampToAvailable — a
/// starved settlement bottoms out at zero people in a band, never negative.
/// A clamp shortfall is NOT banked in the remainder (people who do not exist
/// cannot die later); remainders carry only sub-person fractions.
/// STATELESS: config is immutable tuning, not state.
/// </summary>
public sealed class DemographicsSystem(SimConfig cfg) : ISimSystem<DemographicsTables>
{
    public static readonly SystemId WellKnownId = new(7);
    public const string Name = "demographics";

    private readonly SimConfig _cfg = cfg;

    public SystemId Id => WellKnownId;

    public void Step(SimContext<DemographicsTables> ctx)
    {
        IReadOnlyWorldState prev = ctx.Prev;
        Table<PopBandRow> bands = ctx.Owned.PopBands;
        DemographicsConfig d = _cfg.Demographics;

        // Ascending settlement-row order — the fixed iteration order (law 5).
        for (int s = 0; s < prev.Settlements.Count; s++)
        {
            SettlementId settlement = prev.Settlements[s].Id;

            // Locate this settlement's three band rows (indices in Next == Prev:
            // the clone preserves layout and this system never adds rows here).
            int childIdx = FindBand(bands, settlement, PopBands.Children);
            int adultIdx = FindBand(bands, settlement, PopBands.Adults);
            int elderIdx = FindBand(bands, settlement, PopBands.Elders);
            if (childIdx < 0 || adultIdx < 0 || elderIdx < 0) continue; // never founded → nothing to do

            long prevChildren = prev.PopBands[childIdx].Count.Value;
            long prevAdults = prev.PopBands[adultIdx].Count.Value;
            long prevElders = prev.PopBands[elderIdx].Count.Value;

            // PREV turn's deficit ratio (absent before the first consumption turn → 0).
            double deficit = 0.0;
            for (int i = 0; i < prev.ConsumptionDeficits.Count; i++)
            {
                if (prev.ConsumptionDeficits[i].Settlement == settlement)
                {
                    deficit = prev.ConsumptionDeficits[i].DeficitRatio;
                    break;
                }
            }

            // 1. Births — from PREV adults, credited to children.
            {
                ref PopBandRow child = ref bands.Ref(childIdx);
                double exact = d.BirthsPerAdultPerYear * prevAdults * ctx.DtYears + child.BirthRemainder;
                long births = (long)Math.Floor(exact);
                ctx.Ledger.Flow(
                    ref child.Count, ConservedQuantityIds.Population, ReasonIds.Births,
                    births, FlowDirection.Source, OverdrawPolicy.Throw);
                child.BirthRemainder = exact - births;
            }

            // 2. Base deaths — per band, band-specific rate × PREV count.
            SinkDeaths(ctx, bands, childIdx, d.ChildMortalityPerYear, prevChildren, ReasonIds.Deaths);
            SinkDeaths(ctx, bands, adultIdx, d.AdultMortalityPerYear, prevAdults, ReasonIds.Deaths);
            SinkDeaths(ctx, bands, elderIdx, d.ElderMortalityPerYear, prevElders, ReasonIds.Deaths);

            // 3. Starvation — uniform max rate scaled by PREV deficit, per band.
            double starvationRate = d.StarvationMortalityMaxPerYear * deficit;
            SinkStarvation(ctx, bands, childIdx, starvationRate, prevChildren);
            SinkStarvation(ctx, bands, adultIdx, starvationRate, prevAdults);
            SinkStarvation(ctx, bands, elderIdx, starvationRate, prevElders);

            // 4. Aging — conserving transfers between bands, rates × PREV counts.
            Age(ctx, bands, childIdx, adultIdx, d.AgingChildToAdultPerYear, prevChildren);
            Age(ctx, bands, adultIdx, elderIdx, d.AgingAdultToElderPerYear, prevAdults);
        }
    }

    private static void SinkDeaths(
        SimContext<DemographicsTables> ctx, Table<PopBandRow> bands,
        int index, double ratePerYear, long prevCount, ReasonId reason)
    {
        ref PopBandRow row = ref bands.Ref(index);
        double exact = ratePerYear * prevCount * ctx.DtYears + row.DeathRemainder;
        long deaths = (long)Math.Floor(exact);
        ctx.Ledger.Flow(
            ref row.Count, ConservedQuantityIds.Population, reason,
            deaths, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
        row.DeathRemainder = exact - deaths; // sub-person fraction only (see header)
    }

    private static void SinkStarvation(
        SimContext<DemographicsTables> ctx, Table<PopBandRow> bands,
        int index, double ratePerYear, long prevCount)
    {
        ref PopBandRow row = ref bands.Ref(index);
        double exact = ratePerYear * prevCount * ctx.DtYears + row.StarvationRemainder;
        long deaths = (long)Math.Floor(exact);
        ctx.Ledger.Flow(
            ref row.Count, ConservedQuantityIds.Population, ReasonIds.Starvation,
            deaths, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
        row.StarvationRemainder = exact - deaths;
    }

    private static void Age(
        SimContext<DemographicsTables> ctx, Table<PopBandRow> bands,
        int fromIndex, int toIndex, double ratePerYear, long prevCount)
    {
        double exact = ratePerYear * prevCount * ctx.DtYears + bands.Ref(fromIndex).AgingRemainder;
        long aging = (long)Math.Floor(exact);
        ctx.Ledger.Transfer(
            ref bands.Ref(fromIndex).Count, ref bands.Ref(toIndex).Count,
            aging, OverdrawPolicy.ClampToAvailable);
        bands.Ref(fromIndex).AgingRemainder = exact - aging;
    }

    private static int FindBand(Table<PopBandRow> bands, SettlementId settlement, int band)
    {
        for (int i = 0; i < bands.Count; i++)
            if (bands[i].Settlement == settlement && bands[i].Band == band) return i;
        return -1;
    }
}
