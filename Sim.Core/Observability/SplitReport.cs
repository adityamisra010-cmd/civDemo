using System.Text.Json;
using static System.FormattableString;

namespace Sim.Core.Observability;

/// <summary>One good of one Empire's row of the split report (<see cref="SplitReportRow"/>): the D-054 WS2 split as
/// ConstructionSystem computed it — <see cref="S"/> the purse's holding when building work starts, the shares
/// <see cref="Eb"/>/<see cref="Er"/>, building work's CLAIM <see cref="Db"/> (D-055: what its heads would draw AND set aside
/// with no split), road works' demand <see cref="Dr"/>, the cap and the grant <see cref="Gb"/>, <see cref="HoldsBack"/>
/// (Gb &lt; Db) — the step's own FORECAST of what building work draws, sets aside and road works are charged, and what
/// ACTUALLY happened: <see cref="Build"/> and <see cref="Road"/> the purse's fall across the construction and the road
/// development phases, <see cref="Holds"/> the purse's ConstructionHolds rows (schema v34 section 61) as construction
/// published them. <see cref="Start"/> is the holds walk's starting budget for the Empire's purse (min(S, Gb) under a
/// split), null when the purse had no queue head to walk.</summary>
public sealed record SplitGoodFigures(
    string Good, int Id, long S, long? Start, long Eb, long Er, long Db, long Dr, long Cap, long Gb, bool HoldsBack,
    long ForecastBuild, long Build, long ForecastHolds, long Holds, long ForecastRoad, long Road);

/// <summary>A queue entry a head names (its holder).</summary>
public sealed record SplitHeadRef(int Settlement, int Slot, int Project);

/// <summary>One good of one head (<see cref="SplitHeadFigures"/>): <see cref="Need"/> what it still needed when the step
/// began (Σ Remaining(c, p before) over its inputs of the good), <see cref="Budget"/> the budget the holds walk resolved it
/// against (null: unbounded), what it drew and set aside, and — for the head's BINDING good only — why it lacked it
/// (<see cref="Cause"/>, a MaterialCause name; "None" for every other good), the earliest earlier head that sets it aside
/// (<see cref="Holder"/>, for HeldForEarlier) and how many units earlier heads of its purse set aside
/// (<see cref="SetAsideAhead"/>).</summary>
public sealed record SplitHeadGood(
    string Good, int Id, long Need, long? Budget, long Draw, long Hold, string Cause, SplitHeadRef? Holder, long SetAsideAhead);

/// <summary>One queue head of the Empire's purse, in THE BUILDING ORDER, as the step's holds walk resolved it
/// (ConstructionQuery.HoldsWalk): its entry, the order's stamp turn (<see cref="Enqueued"/>, −1 for a migrated entry),
/// progress before and after (parts per 1,000,000), its release (a HoldRelease name), its limit (a HeadLimitKind name) and
/// its goods.</summary>
public sealed record SplitHeadFigures(
    int Settlement, int Slot, int Project, long Enqueued, long Before, long After, string Release, string Limit,
    SplitHeadGood[] Goods)
{
    /// <summary>This head's figures for good id <paramref name="id"/>, or null when the project has no input of it.</summary>
    public SplitHeadGood? GoodOf(int id)
    {
        for (int i = 0; i < Goods.Length; i++) if (Goods[i].Id == id) return Goods[i];
        return null;
    }
}

/// <summary>
/// One line of the split report: one roster Empire in one step — the step from turn <see cref="Turn"/> (PREV's turn, the
/// turn its orders were stamped) to turn <see cref="To"/> = Turn + 1 (the world it produced). <see cref="BuildingsPercent"/>
/// is the Empire's effective building share b, <see cref="RoadPercent"/> the DevelopRoads order road development applied
/// for it (null: none), <see cref="Guarded"/> the starvation guard fired.
/// </summary>
public sealed record SplitReportRow(
    long Turn, int Polity, int BuildingsPercent, double? RoadPercent, bool Guarded, SplitGoodFigures[] Goods,
    SplitHeadFigures[] Heads)
{
    /// <summary>The turn of the world this step produced.</summary>
    public long To => Turn + 1;

    /// <summary>This row's figures for good id <paramref name="id"/>, or null when it is not an eligible good.</summary>
    public SplitGoodFigures? GoodOf(int id)
    {
        for (int i = 0; i < Goods.Length; i++) if (Goods[i].Id == id) return Goods[i];
        return null;
    }
}

/// <summary>
/// D-055 / ADR-040 — THE SPLIT REPORT FILE (`sim run --split-report PATH`, read by `sim split-check`): JSONL, one line per
/// (step, roster Empire), ascending (turn, polity), every line tagged <see cref="Schema"/>. Written by
/// <see cref="Utf8JsonWriter"/> (round-trippable, culture-invariant numbers) from records whose arrays are in a fixed order
/// (goods ascending id; heads in the building order), so the same run written twice is byte-identical — the TelemetryWriter
/// precedent. <see cref="Parse"/> reads it back into the same records, refusing a line it cannot read with its line number,
/// so the checker judges exactly what the run wrote. A report, not state: nothing here is serialized into a WorldState.
/// </summary>
public static class SplitReport
{
    public const string Schema = "split-report/v1";

    /// <summary>Writes <paramref name="row"/> as one JSONL line (the writer is created per line and flushed).</summary>
    public static void Write(Stream output, SplitReportRow row)
    {
        using (var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false }))
        {
            json.WriteStartObject();
            json.WriteString("schema", Schema);
            json.WriteNumber("turn", row.Turn);
            json.WriteNumber("to", row.To);
            json.WriteNumber("polity", row.Polity);
            json.WriteNumber("b", row.BuildingsPercent);
            if (row.RoadPercent is { } road) json.WriteNumber("road", road); else json.WriteNull("road");
            json.WriteBoolean("guarded", row.Guarded);
            json.WriteStartArray("goods");
            for (int i = 0; i < row.Goods.Length; i++)
            {
                SplitGoodFigures g = row.Goods[i];
                json.WriteStartObject();
                json.WriteString("good", g.Good);
                json.WriteNumber("id", g.Id);
                json.WriteNumber("S", g.S);
                if (g.Start is { } start) json.WriteNumber("start", start); else json.WriteNull("start");
                json.WriteNumber("Eb", g.Eb);
                json.WriteNumber("Er", g.Er);
                json.WriteNumber("Db", g.Db);
                json.WriteNumber("Dr", g.Dr);
                json.WriteNumber("cap", g.Cap);
                json.WriteNumber("Gb", g.Gb);
                json.WriteBoolean("holdsBack", g.HoldsBack);
                json.WriteNumber("forecastBuild", g.ForecastBuild);
                json.WriteNumber("build", g.Build);
                json.WriteNumber("forecastHolds", g.ForecastHolds);
                json.WriteNumber("holds", g.Holds);
                json.WriteNumber("forecastRoad", g.ForecastRoad);
                json.WriteNumber("road", g.Road);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("heads");
            for (int h = 0; h < row.Heads.Length; h++)
            {
                SplitHeadFigures head = row.Heads[h];
                json.WriteStartObject();
                json.WriteNumber("settlement", head.Settlement);
                json.WriteNumber("slot", head.Slot);
                json.WriteNumber("project", head.Project);
                json.WriteNumber("enqueued", head.Enqueued);
                json.WriteNumber("before", head.Before);
                json.WriteNumber("after", head.After);
                json.WriteString("release", head.Release);
                json.WriteString("limit", head.Limit);
                json.WriteStartArray("goods");
                for (int i = 0; i < head.Goods.Length; i++)
                {
                    SplitHeadGood g = head.Goods[i];
                    json.WriteStartObject();
                    json.WriteString("good", g.Good);
                    json.WriteNumber("id", g.Id);
                    json.WriteNumber("need", g.Need);
                    if (g.Budget is { } budget) json.WriteNumber("budget", budget); else json.WriteNull("budget");
                    json.WriteNumber("draw", g.Draw);
                    json.WriteNumber("hold", g.Hold);
                    json.WriteString("cause", g.Cause);
                    if (g.Holder is { } holder)
                    {
                        json.WriteStartObject("holder");
                        json.WriteNumber("settlement", holder.Settlement);
                        json.WriteNumber("slot", holder.Slot);
                        json.WriteNumber("project", holder.Project);
                        json.WriteEndObject();
                    }
                    else json.WriteNull("holder");
                    json.WriteNumber("setAsideAhead", g.SetAsideAhead);
                    json.WriteEndObject();
                }
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
            json.Flush();
        }
        output.WriteByte((byte)'\n');
    }

    /// <summary>The report <paramref name="text"/> as rows, in file order. Refuses (FormatException, naming the line) a line
    /// that is not a JSON object of this <see cref="Schema"/>, a missing or mistyped field (named, with where it is: T3 round 3,
    /// finding T3-L2-T3V2-L2-F4), a "to" that is not turn + 1, goods that are not strictly ascending in id — in a row or in a
    /// head, so no good is listed twice (round 3, the same finding) — and rows that are not strictly ascending in (turn,
    /// polity): a report the probe could not have written. The heads' order is NOT refused here: it is P12's building-order
    /// clause, judged with the row named. Empty lines are skipped.</summary>
    public static SplitReportRow[] Parse(string text)
    {
        var rows = new List<SplitReportRow>();
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            if (lines[n].Length == 0) continue;
            SplitReportRow row;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(lines[n]);
                row = ReadRow(doc.RootElement);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                throw new FormatException($"split report line {n + 1}: {e.Message}", e);
            }
            if (rows.Count > 0)
            {
                SplitReportRow last = rows[^1];
                if (row.Turn < last.Turn || (row.Turn == last.Turn && row.Polity <= last.Polity))
                    throw new FormatException(
                        $"split report line {n + 1}: (turn {row.Turn}, polity {row.Polity}) does not follow (turn {last.Turn}, " +
                        $"polity {last.Polity}) — rows are strictly ascending in (turn, polity)");
            }
            rows.Add(row);
        }
        return [.. rows];
    }

    private static SplitReportRow ReadRow(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) throw new FormatException("not a JSON object");
        string schema = Str(e, "schema", "");
        if (schema != Schema) throw new FormatException($"schema \"{schema}\" is not \"{Schema}\"");
        long turn = Long(e, "turn", "");
        long to = Long(e, "to", "");
        if (to != turn + 1) throw new FormatException($"\"to\" {to} is not turn {turn} + 1");
        int polity = Int(e, "polity", "");
        int b = Int(e, "b", "");
        JsonElement road = Field(e, "road", "");
        double? roadPercent = road.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Number => road.GetDouble(),
            _ => throw new FormatException($"field \"road\" is not a number or null ({road.ValueKind})"),
        };
        bool guarded = Bool(e, "guarded", "");
        JsonElement goods = Arr(e, "goods", "");
        var goodRows = new SplitGoodFigures[goods.GetArrayLength()];
        for (int i = 0; i < goodRows.Length; i++)
        {
            goodRows[i] = ReadGood(goods[i], Invariant($"goods[{i}]: "));
            if (i > 0 && goodRows[i].Id <= goodRows[i - 1].Id)
                throw new FormatException(
                    Invariant($"goods[{i}] ({goodRows[i].Good}, id {goodRows[i].Id}) does not follow goods[{i - 1}] ({goodRows[i - 1].Good}, id ") +
                    Invariant($"{goodRows[i - 1].Id}) — a row's goods are strictly ascending in id, each good once"));
        }
        JsonElement heads = Arr(e, "heads", "");
        var headRows = new SplitHeadFigures[heads.GetArrayLength()];
        for (int i = 0; i < headRows.Length; i++) headRows[i] = ReadHead(heads[i], Invariant($"heads[{i}]"));
        return new SplitReportRow(turn, polity, b, roadPercent, guarded, goodRows, headRows);
    }

    private static SplitGoodFigures ReadGood(JsonElement g, string at)
    {
        if (g.ValueKind != JsonValueKind.Object) throw new FormatException($"{at}not a JSON object");
        return new(
            Str(g, "good", at), Int(g, "id", at), Long(g, "S", at), OptionalLong(g, "start", at), Long(g, "Eb", at),
            Long(g, "Er", at), Long(g, "Db", at), Long(g, "Dr", at), Long(g, "cap", at), Long(g, "Gb", at),
            Bool(g, "holdsBack", at), Long(g, "forecastBuild", at), Long(g, "build", at), Long(g, "forecastHolds", at),
            Long(g, "holds", at), Long(g, "forecastRoad", at), Long(g, "road", at));
    }

    private static SplitHeadFigures ReadHead(JsonElement h, string head)
    {
        string at = head + ": ";
        if (h.ValueKind != JsonValueKind.Object) throw new FormatException($"{at}not a JSON object");
        JsonElement goods = Arr(h, "goods", at);
        var rows = new SplitHeadGood[goods.GetArrayLength()];
        for (int i = 0; i < rows.Length; i++)
        {
            JsonElement g = goods[i];
            string gat = Invariant($"{head}.goods[{i}]: ");
            if (g.ValueKind != JsonValueKind.Object) throw new FormatException($"{gat}not a JSON object");
            JsonElement holder = Field(g, "holder", gat);
            SplitHeadRef? holderRef = holder.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Object => new SplitHeadRef(
                    Int(holder, "settlement", gat + "holder: "), Int(holder, "slot", gat + "holder: "), Int(holder, "project", gat + "holder: ")),
                _ => throw new FormatException($"{gat}field \"holder\" is not an object or null ({holder.ValueKind})"),
            };
            rows[i] = new SplitHeadGood(
                Str(g, "good", gat), Int(g, "id", gat), Long(g, "need", gat), OptionalLong(g, "budget", gat), Long(g, "draw", gat),
                Long(g, "hold", gat), Str(g, "cause", gat), holderRef, Long(g, "setAsideAhead", gat));
            if (i > 0 && rows[i].Id <= rows[i - 1].Id)
                throw new FormatException(
                    Invariant($"{head}.goods[{i}] ({rows[i].Good}, id {rows[i].Id}) does not follow {head}.goods[{i - 1}] ({rows[i - 1].Good}, id ") +
                    Invariant($"{rows[i - 1].Id}) — a head's goods are strictly ascending in id, each good once"));
        }
        return new SplitHeadFigures(
            Int(h, "settlement", at), Int(h, "slot", at), Int(h, "project", at), Long(h, "enqueued", at), Long(h, "before", at),
            Long(h, "after", at), Str(h, "release", at), Str(h, "limit", at), rows);
    }

    // The typed field readers: a missing or mistyped field is refused BY NAME, with where it is (round 3).

    private static JsonElement Field(JsonElement e, string name, string at) =>
        e.TryGetProperty(name, out JsonElement v) ? v : throw new FormatException($"{at}missing field \"{name}\"");

    private static long Long(JsonElement e, string name, string at)
    {
        JsonElement v = Field(e, name, at);
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long x)
            ? x : throw new FormatException($"{at}field \"{name}\" is not a whole number ({Shown(v)})");
    }

    private static long? OptionalLong(JsonElement e, string name, string at)
    {
        JsonElement v = Field(e, name, at);
        if (v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long x)
            ? x : throw new FormatException($"{at}field \"{name}\" is not a whole number or null ({Shown(v)})");
    }

    private static int Int(JsonElement e, string name, string at)
    {
        JsonElement v = Field(e, name, at);
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int x)
            ? x : throw new FormatException($"{at}field \"{name}\" is not a whole number in the int range ({Shown(v)})");
    }

    private static string Str(JsonElement e, string name, string at)
    {
        JsonElement v = Field(e, name, at);
        return v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new FormatException($"{at}field \"{name}\" is not a string ({Shown(v)})");
    }

    private static bool Bool(JsonElement e, string name, string at)
    {
        JsonElement v = Field(e, name, at);
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new FormatException($"{at}field \"{name}\" is not true or false ({Shown(v)})"),
        };
    }

    private static JsonElement Arr(JsonElement e, string name, string at)
    {
        JsonElement v = Field(e, name, at);
        return v.ValueKind == JsonValueKind.Array ? v : throw new FormatException($"{at}field \"{name}\" is not an array ({Shown(v)})");
    }

    /// <summary>A mistyped value as the report has it (its JSON text, at most 40 characters).</summary>
    private static string Shown(JsonElement v)
    {
        string raw = v.GetRawText();
        return raw.Length <= 40 ? raw : raw[..40] + "...";
    }
}
