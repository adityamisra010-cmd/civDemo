using System.Text.Json;

namespace Sim.Core.Observability;

/// <summary>
/// §7 — THE TELEMETRY FILE: JSONL, one line per observed turn, holding the
/// <see cref="TurnRecord"/> and every <see cref="SettlementRecord"/> of that
/// step. Written by <see cref="Utf8JsonWriter"/>, whose number formatting is
/// round-trippable ("R") and culture-invariant, from records whose arrays are in
/// index/registry order — so the same session written twice is byte-identical
/// (asserted in Sim.Tests/Observability, not assumed).
///
/// JSONL for ReplayReport's reason: the data is RAGGED (goods × settlements ×
/// classes × needs, plus a variable number of orders and trade legs), and a
/// self-describing object per turn adds a good by changing the data, not the
/// header. <see cref="Schema"/> tags every line so a reader can tell which
/// vintage produced a file it did not write — so the tag MOVES whenever the
/// emitted field set does. T4.20 added <c>food.foodProduced</c> and
/// <c>food.foodBalance</c>: that is a new field set, hence v2. This is the
/// TELEMETRY vintage only; it is not <c>CanonicalSchema</c> (still v24) and
/// nothing here is serialized into <see cref="Sim.Core.State.WorldState"/>.
///
/// NaN is written as the string "NaN": System.Text.Json refuses non-finite
/// doubles by default, and a reading that is absent (no price row yet, no
/// attractiveness row yet) must stay distinguishable from a reading of zero.
/// </summary>
public static class TelemetryWriter
{
    public const string Schema = "telemetry/v2";

    /// <summary>Every observation in the history, one line each.</summary>
    public static void WriteAll(Stream output, IObservationHistory history)
    {
        IReadOnlyList<TurnObservation> all = history.Observations;
        for (int i = 0; i < all.Count; i++) WriteTurn(output, all[i]);
    }

    /// <summary>One JSONL line. The writer is created per line and flushed; the
    /// caller owns the stream (ReplayReport precedent: a failure to write cannot
    /// leave a half-stepped world behind, because the world is not here).</summary>
    public static void WriteTurn(Stream output, TurnObservation observation)
    {
        using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false });
        json.WriteStartObject();
        json.WriteString("schema", Schema);
        WriteTurnRecord(json, "turn", observation.Turn);
        json.WriteStartArray("settlements");
        for (int i = 0; i < observation.Settlements.Length; i++)
            WriteSettlementRecord(json, null, observation.Settlements[i]);
        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        output.WriteByte((byte)'\n');
    }

    /// <summary>One settlement record, indented, in full — what
    /// <c>sim inspect --settlement ID --turn N</c> prints so the director test
    /// can be answered headlessly. Same writer as the JSONL, so "in full" is
    /// guaranteed by construction rather than by a second field list.</summary>
    public static void WriteSettlement(Stream output, SettlementRecord record, TurnRecord turn)
    {
        using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteString("schema", Schema);
        json.WriteNumber("turn", turn.Turn);
        Num(json, "year", turn.Year);
        Num(json, "dtYears", turn.DtYears);
        WriteSettlementRecord(json, "settlement", record);
        json.WriteEndObject();
        json.Flush();
        output.WriteByte((byte)'\n');
    }

    // ------------------------------------------------------------- turn --

    private static void WriteTurnRecord(Utf8JsonWriter json, string name, TurnRecord t)
    {
        json.WriteStartObject(name);
        json.WriteNumber("turn", t.Turn);
        Num(json, "year", t.Year);
        Num(json, "dtYears", t.DtYears);

        json.WriteStartArray("stocks");
        for (int i = 0; i < t.Stocks.Length; i++)
        {
            StockRecord s = t.Stocks[i];
            json.WriteStartObject();
            json.WriteNumber("quantity", s.Quantity);
            json.WriteString("name", s.Name);
            json.WriteNumber("opening", s.Opening);
            json.WriteNumber("closing", s.Closing);
            Legs(json, "sources", s.Sources);
            Legs(json, "sinks", s.Sinks);
            json.WriteBoolean("reconciles", s.Reconciles);
            json.WriteNumber("discrepancy", s.Discrepancy);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartObject("population");
        json.WriteNumber("opening", t.Population.Opening);
        json.WriteNumber("births", t.Population.Births);
        json.WriteNumber("naturalDeaths", t.Population.NaturalDeaths);
        json.WriteNumber("starvation", t.Population.Starvation);
        json.WriteNumber("closing", t.Population.Closing);
        json.WriteBoolean("reconciles", t.Population.Reconciles);
        json.WriteNumber("discrepancy", t.Population.Discrepancy);
        json.WriteEndObject();

        json.WriteStartObject("grain");
        json.WriteNumber("opening", t.Grain.Opening);
        json.WriteNumber("endowment", t.Grain.Endowment);
        json.WriteNumber("harvest", t.Grain.Harvest);
        json.WriteNumber("eaten", t.Grain.Eaten);
        json.WriteNumber("spoilage", t.Grain.Spoilage);
        json.WriteNumber("overflow", t.Grain.Overflow);
        json.WriteNumber("closing", t.Grain.Closing);
        json.WriteBoolean("reconciles", t.Grain.Reconciles);
        json.WriteNumber("discrepancy", t.Grain.Discrepancy);
        json.WriteEndObject();

        json.WriteStartObject("dwellings");
        json.WriteNumber("opening", t.Dwellings.Opening);
        json.WriteNumber("built", t.Dwellings.Built);
        json.WriteNumber("decayed", t.Dwellings.Decayed);
        json.WriteNumber("closing", t.Dwellings.Closing);
        json.WriteBoolean("reconciles", t.Dwellings.Reconciles);
        json.WriteNumber("discrepancy", t.Dwellings.Discrepancy);
        json.WriteEndObject();

        json.WriteStartArray("goods");
        for (int i = 0; i < t.Goods.Length; i++)
        {
            GoodAccount g = t.Goods[i];
            json.WriteStartObject();
            json.WriteNumber("good", g.Good);
            json.WriteString("name", g.Name);
            json.WriteNumber("opening", g.Opening);
            json.WriteNumber("produced", g.Produced);
            json.WriteNumber("inputsConsumed", g.InputsConsumed);
            json.WriteNumber("toolWear", g.ToolWear);
            json.WriteNumber("eaten", g.Eaten);
            json.WriteNumber("housingMaterials", g.HousingMaterials);
            json.WriteNumber("constructionMaterials", g.ConstructionMaterials);
            json.WriteNumber("closing", g.Closing);
            json.WriteBoolean("reconciles", g.Reconciles);
            json.WriteNumber("discrepancy", g.Discrepancy);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartObject("flows");
        json.WriteNumber("migrantsMoved", t.Flows.MigrantsMoved);
        json.WriteNumber("settlementsFounded", t.Flows.SettlementsFounded);
        json.WriteNumber("controlLost", t.Flows.ControlLost);
        json.WriteNumber("tradeUnits", t.Flows.TradeUnits);
        json.WriteNumber("tradeFlowCount", t.Flows.TradeFlowCount);
        json.WriteBoolean("unattributedGrainTransfer", t.Flows.UnattributedGrainTransfer);
        json.WriteEndObject();

        Orders(json, t.Orders);

        json.WriteStartArray("policy");
        for (int i = 0; i < t.Policy.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("settlement", t.Policy[i].Settlement);
            Doubles(json, "shares", t.Policy[i].Shares);
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartObject("causes");
        json.WriteNumber("populationDelta", t.Causes.PopulationDelta);
        json.WriteNumber("populationExplained", t.Causes.PopulationExplained);
        json.WriteNumber("grainDelta", t.Causes.GrainDelta);
        json.WriteNumber("grainExplained", t.Causes.GrainExplained);
        json.WriteEndObject();

        json.WriteEndObject();
    }

    // ------------------------------------------------------- settlement --

    private static void WriteSettlementRecord(Utf8JsonWriter json, string? name, SettlementRecord r)
    {
        if (name is null) json.WriteStartObject(); else json.WriteStartObject(name);
        json.WriteNumber("settlement", r.Settlement);
        json.WriteNumber("foundedTurn", r.FoundedTurn);
        json.WriteNumber("controller", r.Controller);
        json.WriteBoolean("founded", r.Founded);

        PopulationSection p = r.Population;
        json.WriteStartObject("population");
        json.WriteNumber("opening", p.Opening);
        json.WriteNumber("closing", p.Closing);
        json.WriteNumber("children", p.Children);
        json.WriteNumber("adults", p.Adults);
        json.WriteNumber("elders", p.Elders);
        json.WriteNumber("notables", p.Notables);
        json.WriteNumber("births", p.Births);
        json.WriteNumber("deaths", p.Deaths);
        json.WriteNumber("inflow", p.Inflow);
        json.WriteNumber("outflow", p.Outflow);
        json.WriteNumber("colonistsDeparted", p.ColonistsDeparted);
        json.WriteString("colonistsDepartedIdentity", p.ColonistsDepartedIdentity);
        json.WriteStartArray("classCounts");
        for (int i = 0; i < p.ClassCounts.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("class", p.ClassCounts[i].Class);
            json.WriteString("name", p.ClassCounts[i].Name);
            json.WriteNumber("count", p.ClassCounts[i].Count);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();

        FoodSection f = r.Food;
        json.WriteStartObject("food");
        json.WriteNumber("grainOpening", f.GrainOpening);
        json.WriteNumber("grainClosing", f.GrainClosing);
        json.WriteNumber("harvest", f.Harvest);
        json.WriteNumber("eaten", f.Eaten);
        json.WriteNumber("storeLosses", f.StoreLosses);
        json.WriteString("storeLossesIdentity", f.StoreLossesIdentity);
        json.WriteNumber("demandUnits", f.DemandUnits);
        Num(json, "deficitRatio", f.DeficitRatio);
        json.WriteStartArray("foodGoods");
        for (int i = 0; i < f.FoodGoods.Length; i++)
        {
            FoodGood g = f.FoodGoods[i];
            json.WriteStartObject();
            json.WriteNumber("good", g.Good);
            json.WriteString("name", g.Name);
            json.WriteNumber("produced", g.Produced);
            json.WriteNumber("demand", g.Demand);
            json.WriteNumber("eaten", g.Eaten);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteNumber("foodObtained", f.FoodObtained);
        json.WriteNumber("foodProduced", f.FoodProduced);
        json.WriteNumber("foodBalance", f.FoodBalance);
        json.WriteEndObject();

        HousingSection h = r.Housing;
        json.WriteStartObject("housing");
        json.WriteBoolean("hasRow", h.HasRow);
        json.WriteNumber("dwellingsOpening", h.DwellingsOpening);
        json.WriteNumber("dwellingsClosing", h.DwellingsClosing);
        Num(json, "capacity", h.Capacity);
        Num(json, "need", h.Need);
        Num(json, "sufficiency", h.Sufficiency);
        Num(json, "lastMaintenanceFraction", h.LastMaintenanceFraction);
        Num(json, "lastLaborUsed", h.LastLaborUsed);
        json.WriteString("builtDecayedSplit", h.BuiltDecayedSplit);
        json.WriteEndObject();

        EconomySection e = r.Economy;
        json.WriteStartObject("economy");
        json.WriteStartArray("goods");
        for (int i = 0; i < e.Goods.Length; i++)
        {
            GoodReading g = e.Goods[i];
            json.WriteStartObject();
            json.WriteNumber("good", g.Good);
            json.WriteString("name", g.Name);
            json.WriteNumber("stock", g.Stock);
            json.WriteNumber("produced", g.Produced);
            json.WriteNumber("inputDemand", g.InputDemand);
            json.WriteNumber("consumptionDemand", g.ConsumptionDemand);
            json.WriteNumber("eaten", g.Eaten);
            Num(json, "price", g.Price);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        Trade(json, "tradeIn", e.TradeIn);
        Trade(json, "tradeOut", e.TradeOut);
        Doubles(json, "sectorShares", e.SectorShares);
        json.WriteBoolean("sectorRowPresent", e.SectorRowPresent);
        Num(json, "foodSurplusRatio", e.FoodSurplusRatio);
        Num(json, "artisanShare", e.ArtisanShare);
        Num(json, "tradeVolume", e.TradeVolume);
        json.WriteStartArray("classActive");
        for (int i = 0; i < e.ClassActive.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("class", e.ClassActive[i].Class);
            json.WriteString("name", e.ClassActive[i].Name);
            json.WriteNumber("active", e.ClassActive[i].Active);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();

        SocialSection s = r.Social;
        json.WriteStartObject("social");
        Num(json, "happiness", s.Happiness);
        Doubles(json, "happinessFactors", s.HappinessFactors);
        json.WriteStartArray("grievance");
        for (int i = 0; i < s.Grievance.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("class", s.Grievance[i].Class);
            json.WriteString("name", s.Grievance[i].Name);
            Num(json, "value", s.Grievance[i].Value);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("needSatisfaction");
        for (int i = 0; i < s.NeedSatisfaction.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("class", s.NeedSatisfaction[i].Class);
            json.WriteNumber("need", s.NeedSatisfaction[i].NeedId);
            json.WriteString("name", s.NeedSatisfaction[i].NeedName);
            Num(json, "value", s.NeedSatisfaction[i].Value);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();

        MigrationSection m = r.Migration;
        json.WriteStartObject("migration");
        Num(json, "pushDeficitRatio", m.PushDeficitRatio);
        Num(json, "pullAttractiveness", m.PullAttractiveness);
        json.WriteStartArray("allAttractiveness");
        for (int i = 0; i < m.AllAttractiveness.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("settlement", m.AllAttractiveness[i].Settlement);
            Num(json, "value", m.AllAttractiveness[i].Value);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteNumber("prevGrainStock", m.PrevGrainStock);
        json.WriteNumber("prevGrainHarvest", m.PrevGrainHarvest);
        Num(json, "unplacedDeparture", m.UnplacedDeparture);
        Num(json, "unplacedRemainder", m.UnplacedRemainder);
        json.WriteString("pairwiseFlows", m.PairwiseFlows);
        json.WriteEndObject();

        PolicySection pol = r.Policy;
        json.WriteStartObject("policy");
        Doubles(json, "declaredWeights", pol.DeclaredWeights);
        json.WriteBoolean("declaredRowPresent", pol.DeclaredRowPresent);
        Doubles(json, "effectiveShares", pol.EffectiveShares);
        json.WriteEndObject();

        Orders(json, r.Orders);
        json.WriteEndObject();
    }

    // ---------------------------------------------------------- pieces --

    private static void Orders(Utf8JsonWriter json, OrderApplied[] orders)
    {
        json.WriteStartArray("orders");
        for (int i = 0; i < orders.Length; i++)
        {
            OrderApplied o = orders[i];
            json.WriteStartObject();
            json.WriteNumber("index", o.Index);
            json.WriteNumber("turn", o.Turn);
            json.WriteNumber("actor", o.Actor);
            json.WriteString("kind", o.Kind.ToString());
            json.WriteNumber("targetId", o.TargetId);
            json.WriteNumber("settlement", o.Settlement);
            json.WriteNumber("sector", o.Sector);
            Num(json, "amount", o.Amount);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void Trade(Utf8JsonWriter json, string name, TradeLeg[] legs)
    {
        json.WriteStartArray(name);
        for (int i = 0; i < legs.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("other", legs[i].Other);
            json.WriteNumber("good", legs[i].Good);
            json.WriteString("name", legs[i].Name);
            json.WriteNumber("quantity", legs[i].Quantity);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void Legs(Utf8JsonWriter json, string name, FlowLeg[] legs)
    {
        json.WriteStartArray(name);
        for (int i = 0; i < legs.Length; i++)
        {
            json.WriteStartObject();
            json.WriteNumber("reason", legs[i].Reason);
            json.WriteString("name", legs[i].Name);
            json.WriteNumber("units", legs[i].Units);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void Doubles(Utf8JsonWriter json, string name, double[] values)
    {
        json.WriteStartArray(name);
        for (int i = 0; i < values.Length; i++)
        {
            if (double.IsFinite(values[i])) json.WriteNumberValue(values[i]);
            else json.WriteStringValue(NonFinite(values[i]));
        }
        json.WriteEndArray();
    }

    private static void Num(Utf8JsonWriter json, string name, double value)
    {
        if (double.IsFinite(value)) json.WriteNumber(name, value);
        else json.WriteString(name, NonFinite(value));
    }

    private static string NonFinite(double value) =>
        double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity";
}
