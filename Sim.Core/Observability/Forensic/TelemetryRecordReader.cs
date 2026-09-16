using System.Text.Json;

namespace Sim.Core.Observability.Forensic;

/// <summary>One class headcount in a settlement on a turn (READ).</summary>
public readonly record struct ClassCountRow(int Class, string Name, long Count);

/// <summary>One class's activation latch in a settlement on a turn (READ
/// ClassStateRow.Active).</summary>
public readonly record struct ClassActiveRow(int Class, string Name, int Active);

/// <summary>One order delivered to a step, as the record carries it (READ).</summary>
public readonly record struct OrderRow(int Index, long Turn, int Actor, string Kind, int TargetId, int Settlement, int Sector, double Amount);

/// <summary>The world grain account for a step: every term READ from the record,
/// which DIFFERENCED them from the ledger's own cumulative rows.</summary>
public readonly record struct GrainTotals(
    long Opening, long Endowment, long Harvest, long Eaten, long Spoilage, long Overflow, long Closing,
    bool Reconciles, long Discrepancy);

/// <summary>The step's cross-settlement movement totals (READ).</summary>
public readonly record struct FlowsTotals(
    long MigrantsMoved, int SettlementsFounded, int ControlLost, long TradeUnits, int TradeFlowCount,
    bool UnattributedGrainTransfer);

/// <summary>One settlement on one turn, as the saved telemetry record carries
/// it. Every field here is READ from the file — nothing is recomputed, and
/// nothing the file does not contain appears.</summary>
public sealed record TelemetrySettlement(
    int Settlement, long FoundedTurn, int Controller, bool Founded,
    long PopOpening, long PopClosing, long Births, long Deaths, long Inflow, long Outflow,
    long ColonistsDeparted, string ColonistsDepartedIdentity, ClassCountRow[] ClassCounts,
    long GrainOpening, long GrainClosing, long Harvest, long Eaten,
    long StoreLosses, string StoreLossesIdentity, long DemandUnits, double DeficitRatio,
    bool HousingHasRow, long DwellingsOpening, long DwellingsClosing,
    double Happiness, double[] HappinessFactors,
    ClassActiveRow[] ClassActive);

/// <summary>One observed step, as the saved telemetry record carries it.</summary>
public sealed record TelemetryTurn(
    long Turn, double Year, double DtYears,
    long PopOpening, long Births, long NaturalDeaths, long Starvation, long PopClosing,
    bool PopReconciles, long PopDiscrepancy,
    GrainTotals Grain, FlowsTotals Flows, OrderRow[] Orders, TelemetrySettlement[] Settlements);

/// <summary>
/// THE TELEMETRY READER. Until this packet the telemetry file was WRITE-ONLY —
/// there was no reader anywhere in the repository, so every question about a
/// played session was answered by REPLAYING it, and a file found on its own
/// could be read only by eye. Adding forensic fields without adding a reader
/// would have added data no tool can read.
///
/// STRICT ON THE TAG, TOLERANT OF KEYS: a line whose schema is not
/// <see cref="TelemetryWriter.Schema"/> is refused with the file and line named,
/// because a reader that silently accepts an unknown vintage would report the
/// difference as a finding. Unknown keys are ignored.
///
/// IT RECOMPUTES NOTHING. Every value it returns was written by the observer
/// that watched the step. Where the record cannot answer a question, the
/// inspector above it says so rather than deriving one here.
/// </summary>
public sealed class TelemetryRecordFile
{
    public IReadOnlyList<TelemetryTurn> Turns { get; }

    private TelemetryRecordFile(IReadOnlyList<TelemetryTurn> turns) => Turns = turns;

    public static TelemetryRecordFile Read(string path) => Parse(File.ReadLines(path), path);

    public static TelemetryRecordFile Parse(IEnumerable<string> lines, string describedAs)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var turns = new List<TelemetryTurn>();
        int number = 0;
        foreach (string raw in lines)
        {
            number++;
            string line = raw.Trim();
            if (line.Length == 0) continue;

            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            string? schema = Str(root, "schema");
            if (schema != TelemetryWriter.Schema)
            {
                throw new InvalidDataException(
                    $"{describedAs} line {number}: not a {TelemetryWriter.Schema} record "
                    + $"(found '{schema ?? "no schema tag"}').");
            }
            turns.Add(ReadTurn(root));
        }
        return new TelemetryRecordFile(turns);
    }

    /// <summary>The record for a turn, or null when the file does not cover it.</summary>
    public TelemetryTurn? At(long turn)
    {
        for (int i = 0; i < Turns.Count; i++) if (Turns[i].Turn == turn) return Turns[i];
        return null;
    }

    /// <summary>One settlement's record on one turn, or null.</summary>
    public TelemetrySettlement? Settlement(long turn, int settlement)
    {
        TelemetryTurn? t = At(turn);
        if (t is null) return null;
        for (int i = 0; i < t.Settlements.Length; i++)
            if (t.Settlements[i].Settlement == settlement) return t.Settlements[i];
        return null;
    }

    /// <summary>Every settlement id the file mentions, ascending.</summary>
    public int[] SettlementIds()
    {
        var ids = new List<int>();
        for (int i = 0; i < Turns.Count; i++)
        {
            TelemetrySettlement[] rows = Turns[i].Settlements;
            for (int j = 0; j < rows.Length; j++) if (!ids.Contains(rows[j].Settlement)) ids.Add(rows[j].Settlement);
        }
        int[] array = [.. ids];
        Array.Sort(array);
        return array;
    }

    private static TelemetryTurn ReadTurn(JsonElement root)
    {
        JsonElement t = Obj(root, "turn");
        JsonElement pop = Obj(t, "population");
        JsonElement grain = Obj(t, "grain");
        JsonElement flows = Obj(t, "flows");

        var orders = new List<OrderRow>();
        foreach (JsonElement o in Arr(t, "orders"))
            orders.Add(new OrderRow(
                (int)Lng(o, "index"), Lng(o, "turn"), (int)Lng(o, "actor"), Str(o, "kind") ?? "",
                (int)Lng(o, "targetId"), (int)Lng(o, "settlement"), (int)Lng(o, "sector"), Dbl(o, "amount")));

        var settlements = new List<TelemetrySettlement>();
        foreach (JsonElement s in Arr(root, "settlements")) settlements.Add(ReadSettlement(s));

        return new TelemetryTurn(
            Turn: Lng(t, "turn"),
            Year: Dbl(t, "year"),
            DtYears: Dbl(t, "dtYears"),
            PopOpening: Lng(pop, "opening"),
            Births: Lng(pop, "births"),
            NaturalDeaths: Lng(pop, "naturalDeaths"),
            Starvation: Lng(pop, "starvation"),
            PopClosing: Lng(pop, "closing"),
            PopReconciles: Bln(pop, "reconciles"),
            PopDiscrepancy: Lng(pop, "discrepancy"),
            Grain: new GrainTotals(
                Lng(grain, "opening"), Lng(grain, "endowment"), Lng(grain, "harvest"), Lng(grain, "eaten"),
                Lng(grain, "spoilage"), Lng(grain, "overflow"), Lng(grain, "closing"),
                Bln(grain, "reconciles"), Lng(grain, "discrepancy")),
            Flows: new FlowsTotals(
                Lng(flows, "migrantsMoved"), (int)Lng(flows, "settlementsFounded"), (int)Lng(flows, "controlLost"),
                Lng(flows, "tradeUnits"), (int)Lng(flows, "tradeFlowCount"), Bln(flows, "unattributedGrainTransfer")),
            Orders: [.. orders],
            Settlements: [.. settlements]);
    }

    private static TelemetrySettlement ReadSettlement(JsonElement s)
    {
        JsonElement pop = Obj(s, "population");
        JsonElement food = Obj(s, "food");
        JsonElement housing = Obj(s, "housing");
        JsonElement economy = Obj(s, "economy");
        JsonElement social = Obj(s, "social");

        var counts = new List<ClassCountRow>();
        foreach (JsonElement c in Arr(pop, "classCounts"))
            counts.Add(new ClassCountRow((int)Lng(c, "class"), Str(c, "name") ?? "", Lng(c, "count")));

        var active = new List<ClassActiveRow>();
        foreach (JsonElement c in Arr(economy, "classActive"))
            active.Add(new ClassActiveRow((int)Lng(c, "class"), Str(c, "name") ?? "", (int)Lng(c, "active")));

        var factors = new List<double>();
        foreach (JsonElement f in Arr(social, "happinessFactors")) factors.Add(Value(f));

        return new TelemetrySettlement(
            Settlement: (int)Lng(s, "settlement"),
            FoundedTurn: Lng(s, "foundedTurn"),
            Controller: (int)Lng(s, "controller"),
            Founded: Bln(s, "founded"),
            PopOpening: Lng(pop, "opening"),
            PopClosing: Lng(pop, "closing"),
            Births: Lng(pop, "births"),
            Deaths: Lng(pop, "deaths"),
            Inflow: Lng(pop, "inflow"),
            Outflow: Lng(pop, "outflow"),
            ColonistsDeparted: Lng(pop, "colonistsDeparted"),
            ColonistsDepartedIdentity: Str(pop, "colonistsDepartedIdentity") ?? "",
            ClassCounts: [.. counts],
            GrainOpening: Lng(food, "grainOpening"),
            GrainClosing: Lng(food, "grainClosing"),
            Harvest: Lng(food, "harvest"),
            Eaten: Lng(food, "eaten"),
            StoreLosses: Lng(food, "storeLosses"),
            StoreLossesIdentity: Str(food, "storeLossesIdentity") ?? "",
            DemandUnits: Lng(food, "demandUnits"),
            DeficitRatio: Dbl(food, "deficitRatio"),
            HousingHasRow: Bln(housing, "hasRow"),
            DwellingsOpening: Lng(housing, "dwellingsOpening"),
            DwellingsClosing: Lng(housing, "dwellingsClosing"),
            Happiness: Dbl(social, "happiness"),
            HappinessFactors: [.. factors],
            ClassActive: [.. active]);
    }

    // --- JSON primitives -----------------------------------------------------
    //
    // telemetry/v2 writes a non-finite double as the STRING "NaN" / "Infinity"
    // (TelemetryWriter, deliberately, so an absent reading stays distinguishable
    // from a reading of zero). The reader therefore has to accept both shapes.
    // That convention is NOT changed here: moving it would move the telemetry
    // tag and break the byte-identity that tag promises — a separate ruling.

    private static double Value(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.String => e.GetString() switch
        {
            "NaN" => double.NaN,
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            _ => double.NaN,
        },
        _ => double.NaN,
    };

    private static JsonElement Obj(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) ? v : default;

    private static IEnumerable<JsonElement> Arr(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object) yield break;
        if (!e.TryGetProperty(name, out JsonElement v) || v.ValueKind != JsonValueKind.Array) yield break;
        foreach (JsonElement item in v.EnumerateArray()) yield return item;
    }

    private static string? Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v)
            && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Lng(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v)
            && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0L;

    private static double Dbl(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v)
            ? Value(v) : double.NaN;

    private static bool Bln(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v)
            && v.ValueKind == JsonValueKind.True;
}
