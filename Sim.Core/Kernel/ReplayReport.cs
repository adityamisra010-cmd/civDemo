using System.Text.Json;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Kernel;

/// <summary>
/// T3.12a — THE REPLAY DIAGNOSTIC REPORTER.
///
/// THE GAP IT CLOSES: an orders `.bin` plus a chronicle `.txt` carry almost no
/// state. The chronicle records emergence and migration events only; the orders
/// log records INPUTS, not outcomes. Everything between — stocks, prices, needs,
/// grievance, class counts, sector mixes — was visible only to whoever sat at the
/// machine, which made the DIRECTOR the measuring instrument: expensive, lossy,
/// and unable to scale to an M4 exit. This turns any played session into a
/// reproducible dataset.
///
/// IT IS AN OBSERVER, AND THAT IS A HARD CONSTRAINT. It is handed a WorldState
/// AFTER the step and READS it. It never writes world state, never returns a
/// world, and is never consulted by any system. Determinism is asserted, not
/// assumed — see ReplayReportTests.
///
/// NO SECOND IMPLEMENTATION OF ANY FORMULA. Every number below is READ from the
/// table the owning system already wrote: satisfaction from NeedSatisfactions,
/// grievance from Grievances, price from Prices, stock from GoodStocks, dwellings
/// from Housing, tier from CatchmentSummaries, shares through Sectors.Share.
/// Population and the cohort split are the ONLY aggregates, and they are plain
/// sums over BucketRow.Count — the same conserved longs the Ledger moves, summed,
/// not re-derived. A reporter carrying its own copy of a formula is a second
/// implementation that will drift away from the sim silently.
///
/// WHY JSONL AND NOT CSV (the packet asked for the choice and its reason): the
/// data is RAGGED. A settlement has a variable number of active classes, each
/// with a variable number of bound needs, alongside ~13 goods each carrying two
/// numbers. Flattening that into CSV forces either a column explosion
/// (goods × fields × classes × needs, mostly empty) or several files joined on a
/// composite key — and both bake the registry sizes into a header contract that
/// breaks whenever a good or a class is added. JSONL is one self-describing
/// object per reported turn: it streams, it appends without a header, `jq` reads
/// it directly, and adding a good changes the data rather than the schema.
///
/// DETERMINISM OF THE FILE ITSELF: tables are walked in index order (never a
/// Dictionary), and Utf8JsonWriter emits round-trippable, culture-invariant
/// numbers. The same log replayed twice produces byte-identical report files —
/// asserted, not assumed.
/// </summary>
public static class ReplayReport
{
    /// <summary>The schema tag written on every line, so a reader can tell
    /// which vintage of the reporter produced a file it did not generate.</summary>
    public const string Schema = "replay-report/v1";

    /// <summary>
    /// Writes ONE JSONL line describing the world at this turn. Called after the
    /// step, with the post-step state. `writer` is the caller's open stream — the
    /// reporter opens and closes nothing, so a failure to write cannot leave a
    /// half-stepped world behind.
    /// </summary>
    public static void WriteTurn(Stream output, WorldState w, SimConfig cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg.Goods);
        ArgumentNullException.ThrowIfNull(cfg.Needs);

        using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false });
        json.WriteStartObject();
        json.WriteString("schema", Schema);
        json.WriteNumber("turn", w.Clock.Turn);
        json.WriteNumber("year", w.Clock.WorldDateYears);
        json.WriteNumber("dtYears", w.Clock.DtYears);
        json.WriteString("hash", WorldHash.ComputeHex(w));

        // WORLD ROW. totalFood is the numeraire good's stock summed over
        // settlements — grain, cfg.Goods.GrainId, the same id the price system
        // pins at 1.0.
        long totalPop = 0;
        for (int i = 0; i < w.Buckets.Count; i++) totalPop += w.Buckets[i].Count.Value;
        long totalFood = 0;
        int grain = cfg.Goods.GrainId;
        for (int i = 0; i < w.GoodStocks.Count; i++)
            if (w.GoodStocks[i].Good.Value == grain) totalFood += w.GoodStocks[i].Amount.Value;
        long totalFlow = 0;
        for (int i = 0; i < w.TradeFlows.Count; i++) totalFlow += w.TradeFlows[i].Quantity;
        json.WriteNumber("totalPopulation", totalPop);
        json.WriteNumber("totalFood", totalFood);
        json.WriteNumber("totalTradeFlow", totalFlow);

        // Allocated ONCE outside the settlement loop (CA2014) and cleared per
        // settlement — a stackalloc inside the loop would grow the frame per
        // settlement and overflow on a large world.
        Span<long> cohorts = stackalloc long[Cohorts.Count];
        json.WriteStartArray("settlements");
        for (int s = 0; s < w.Settlements.Count; s++)
        {
            SettlementId id = w.Settlements[s].Id;
            json.WriteStartObject();
            json.WriteNumber("id", id.Value);

            // POPULATION + COHORT SPLIT — plain sums over the conserved stock.
            cohorts.Clear();
            long pop = 0;
            for (int i = 0; i < w.Buckets.Count; i++)
            {
                BucketRow b = w.Buckets[i];
                if (b.Settlement != id) continue;
                pop += b.Count.Value;
                cohorts[b.CohortIdx] += b.Count.Value;
            }
            json.WriteNumber("population", pop);
            json.WriteStartArray("cohorts");
            for (int c = 0; c < Cohorts.Count; c++) json.WriteNumberValue(cohorts[c]);
            json.WriteEndArray();

            WriteClasses(json, w, cfg, id);
            WriteSectors(json, w, id);
            WriteGoods(json, w, cfg, id);
            WriteHousing(json, w, id);
            WriteTrade(json, w, cfg, id);

            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        output.WriteByte((byte)'\n');
    }

    /// <summary>Every class PRESENT in this settlement, with its count, its bound
    /// needs' satisfaction and its grievance. "Present" = it has a bucket row —
    /// the dense founding instantiates every registered class, so a class at zero
    /// people is reported at zero rather than omitted, which is what makes a
    /// class appearing or vanishing visible in a diff.</summary>
    private static void WriteClasses(Utf8JsonWriter json, WorldState w, SimConfig cfg, SettlementId id)
    {
        json.WriteStartArray("classes");
        for (int ci = 0; ci < cfg.Registries.Classes.Length; ci++)
        {
            ClassEntry entry = cfg.Registries.Classes[ci];
            long count = 0;
            bool present = false;
            for (int i = 0; i < w.Buckets.Count; i++)
            {
                BucketRow b = w.Buckets[i];
                if (b.Settlement != id || b.Class.Value != entry.Id) continue;
                present = true;
                count += b.Count.Value;
            }
            if (!present) continue;

            json.WriteStartObject();
            json.WriteNumber("id", entry.Id);
            json.WriteString("name", entry.Name);
            json.WriteNumber("count", count);

            int active = 0;
            for (int i = 0; i < w.ClassStates.Count; i++)
                if (w.ClassStates[i].Settlement == id && w.ClassStates[i].Class.Value == entry.Id)
                { active = w.ClassStates[i].Active; break; }
            json.WriteNumber("active", active);

            // BOUND needs only — an unbound need has no satisfaction to report,
            // and reporting 0.0 for it would read as deprivation.
            json.WriteStartObject("needs");
            for (int n = 0; n < cfg.Needs!.Needs.Length; n++)
            {
                NeedEntry need = cfg.Needs.Needs[n];
                if (!need.Bound) continue;
                for (int i = 0; i < w.NeedSatisfactions.Count; i++)
                {
                    NeedSatisfactionRow row = w.NeedSatisfactions[i];
                    if (row.Settlement != id || row.Class.Value != entry.Id || row.NeedId != need.Id) continue;
                    json.WriteNumber(need.Name, row.Value);
                    break;
                }
            }
            json.WriteEndObject();

            for (int i = 0; i < w.Grievances.Count; i++)
                if (w.Grievances[i].Settlement == id && w.Grievances[i].Class.Value == entry.Id)
                { json.WriteNumber("grievance", w.Grievances[i].Value); break; }

            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    /// <summary>The allocation IN FORCE, read through Sectors.Share — the same
    /// accessor every consumer uses, so a normalisation change cannot make the
    /// report disagree with the sim.</summary>
    private static void WriteSectors(Utf8JsonWriter json, WorldState w, SettlementId id)
    {
        for (int i = 0; i < w.SectorAllocations.Count; i++)
        {
            if (w.SectorAllocations[i].Settlement != id) continue;
            SectorAllocationRow row = w.SectorAllocations[i];
            json.WriteStartObject("sectors");
            json.WriteNumber("farming", Sectors.Share(row, Sectors.Farming));
            json.WriteNumber("herding", Sectors.Share(row, Sectors.Herding));
            json.WriteNumber("extraction", Sectors.Share(row, Sectors.Extraction));
            json.WriteNumber("crafting", Sectors.Share(row, Sectors.Crafting));
            json.WriteNumber("construction", Sectors.Share(row, Sectors.Construction));
            json.WriteEndObject();
            return;
        }
    }

    /// <summary>Every good's stock and price, in registry order.</summary>
    private static void WriteGoods(Utf8JsonWriter json, WorldState w, SimConfig cfg, SettlementId id)
    {
        json.WriteStartArray("goods");
        for (int g = 0; g < cfg.Goods!.Goods.Length; g++)
        {
            GoodEntry good = cfg.Goods.Goods[g];
            json.WriteStartObject();
            json.WriteString("name", good.Name);
            for (int i = 0; i < w.GoodStocks.Count; i++)
            {
                GoodStockRow row = w.GoodStocks[i];
                if (row.Settlement != id || row.Good.Value != good.Id) continue;
                json.WriteNumber("stock", row.Amount.Value);
                // The consumption pair the needs fill is computed from — reported
                // because "Comfort is low" and "nobody wanted any" look identical
                // without it (the T3.9b Q5 decomposition, made routine).
                json.WriteNumber("demanded", row.LastConsumptionDemandUnits);
                json.WriteNumber("eaten", row.LastConsumptionEatenUnits);
                json.WriteNumber("produced", row.LastProducedUnits);
                break;
            }
            for (int i = 0; i < w.Prices.Count; i++)
            {
                if (w.Prices[i].Settlement != id || w.Prices[i].Good.Value != good.Id) continue;
                json.WriteNumber("price", w.Prices[i].Price);
                break;
            }
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteHousing(Utf8JsonWriter json, WorldState w, SettlementId id)
    {
        json.WriteStartObject("housing");
        for (int i = 0; i < w.Housing.Count; i++)
        {
            if (w.Housing[i].Settlement != id) continue;
            json.WriteNumber("dwellings", w.Housing[i].Dwellings.Value);
            json.WriteNumber("maintenanceFraction", w.Housing[i].LastMaintenanceFraction);
            break;
        }
        for (int i = 0; i < w.CatchmentSummaries.Count; i++)
        {
            if (w.CatchmentSummaries[i].Settlement != id) continue;
            json.WriteNumber("sizeTier", w.CatchmentSummaries[i].SizeTier);
            json.WriteNumber("arableKm2", w.CatchmentSummaries[i].EffectiveArableKm2);
            break;
        }
        json.WriteEndObject();
    }

    /// <summary>Flows touching this settlement, split by direction. Omitted
    /// entirely when nothing moved — which is the canonical world's state today
    /// (both trade escalations), so an empty array every turn would be noise.</summary>
    private static void WriteTrade(Utf8JsonWriter json, WorldState w, SimConfig cfg, SettlementId id)
    {
        bool any = false;
        for (int i = 0; i < w.TradeFlows.Count; i++)
            if (w.TradeFlows[i].From == id || w.TradeFlows[i].To == id) { any = true; break; }
        if (!any) return;

        json.WriteStartArray("trade");
        for (int i = 0; i < w.TradeFlows.Count; i++)
        {
            TradeFlowRow row = w.TradeFlows[i];
            if (row.From != id && row.To != id) continue;
            json.WriteStartObject();
            json.WriteString("dir", row.From == id ? "out" : "in");
            json.WriteNumber("other", row.From == id ? row.To.Value : row.From.Value);
            json.WriteString("good", cfg.Goods!.Goods[row.Good.Value - 1].Name);
            json.WriteNumber("quantity", row.Quantity);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }
}
