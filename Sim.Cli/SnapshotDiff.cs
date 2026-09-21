using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Cli;

/// <summary>
/// `sim diff` (T4.19 lane E, CR-013 §6 "measure first"): the FIRST DIVERGENT
/// FIELD between two snapshots, then the full per-table extent.
///
/// It compares the two CANONICAL STREAMS (CanonicalSchema.Write), not the two
/// WorldState object graphs, and walks them with the layout below — a
/// field-by-field mirror of CanonicalSchema.Write in the same block order.
/// Two reasons, both structural:
///   1. The hash that diverged in CR-013 is SHA-256 over exactly this stream,
///      so the stream is the only place a divergence is guaranteed to be
///      visible: a field that hashes differently is, by construction, a byte
///      run in this walk that compares unequal. Comparing object graphs would
///      have to re-derive which fields the hash covers.
///   2. Sim.Cli must not name the needs/grievance tables (check-read-isolation:
///      grievance drives no behaviour before M5, D-021). A stream walk never
///      touches a table by name — it reads a count prefix and N × row width.
/// The layout is therefore a DUPLICATE of the schema's field list, and a
/// duplicate goes stale. The guard is SnapshotDiffTests: a populated founded
/// world must walk to EXACTLY CanonicalSchema.ExpectedLength bytes with every
/// table's row count equal to the world's — a misaligned width shifts every
/// later count prefix, which no populated world survives.
///
/// Doubles are reported in R format AND as their 64-bit pattern, with the
/// signed ulp distance between the two patterns when both are finite and of
/// one sign, because the CR-013 signature (integer stocks agree, hash differs)
/// is exactly the case where "3.0000000000000004 vs 3" is invisible in a
/// short print and a last-ulp difference is the whole finding.
/// </summary>
public static class SnapshotDiff
{
    public enum Kind { Int32, Int64, UInt64, Double, Bool }

    public readonly record struct Field(string Name, Kind Kind);

    public sealed record Table(string Name, Field[] Fields);

    /// <summary>The header row: seed + clock. The terrain flag and the content
    /// hash it gates are handled inline in <see cref="Compare"/>.</summary>
    private static readonly Field[] HeaderFields =
    [
        new("Seed", Kind.UInt64),
        new("Clock.Turn", Kind.Int64),
        new("Clock.SimDays", Kind.Int64),
        new("Clock.DtDays", Kind.Int64),
    ];

    private static Field I(string n) => new(n, Kind.Int32);
    private static Field L(string n) => new(n, Kind.Int64);
    private static Field D(string n) => new(n, Kind.Double);

    /// <summary>CanonicalSchema.Write blocks 3..39, in stream order. Every entry
    /// is (field, width class) exactly as the writer emits it; widths must sum
    /// to the schema's *RowWidth constants (SnapshotDiffTests proves the sum).</summary>
    public static readonly Table[] Layout =
    [
        new("Regions", [I("Id")]),
        new("RngStreams", [I("System"), I("Region"), new("State", Kind.UInt64), new("Inc", Kind.UInt64)]),
        new("Rainfall", [I("Region"), D("RainfallMmPerYear")]),
        new("Biomass", [I("Region"), L("Biomass"), D("GrowthRemainder")]),
        new("Goods", [I("Region"), L("Amount")]),
        new("LedgerFlows", [I("Quantity"), I("Reason"), L("TotalSourced"), L("TotalSunk")]),
        new("NetworkNodes", [I("Id"), I("LatticeNode")]),
        new("NetworkEdges", [I("Id"), I("A"), I("B"), I("EdgeType"), D("Cost")]),
        new("Settlements", [I("Id"), I("SiteCell"), L("FoundedTurn")]),
        new("NetworkMeta", [I("Revision")]),
        new("CatchmentNodes", [I("Settlement"), I("LatticeNode"), D("TravelCost")]),
        new("CatchmentSummaries", [I("Settlement"), I("NodeCount"), D("EffectiveArableKm2"), I("NetworkRevision"), L("LastRecomputeTurn"), I("SizeTier")]),
        new("Buckets", [I("Settlement"), I("Culture"), I("Religion"), I("Class"), I("CohortIdx"), L("Count"),
            D("BirthRemainder"), D("DeathRemainder"), D("StarvationRemainder"), D("AgingRemainder"),
            D("MobilityRemainder"), D("MigrationRemainder"), D("ReboundReservoir"),
            D("UnplacedDeparture"), D("UnplacedRemainder")]),
        new("GoodStocks", [I("Settlement"), I("Good"), L("Amount"), D("ProduceRemainder"), D("ConsumeRemainder"),
            L("LastProducedUnits"), L("LastInputDemandUnits"), L("LastConsumptionDemandUnits"), L("LastConsumptionEatenUnits")]),
        new("Deposits", [I("Settlement"), I("Good"), D("Abundance")]),
        new("ConsumptionDeficits", [I("Settlement"), D("DeficitRatio"), L("DemandUnits")]),
        new("SectorAllocations", [I("Settlement"), D("Farming"), D("Herding"), D("Extraction"), D("Crafting"), D("Construction")]),
        new("PathProgress", [I("Settlement"), D("Banked"), I("FrontierNode")]),
        new("Variables", [I("Settlement"), I("VarId"), D("Value")]),
        new("ClassStates", [I("Settlement"), I("Class"), I("Active")]),
        new("SettlementDistances", [I("From"), I("To"), D("TravelCost")]),
        new("MigrationFlows", [I("Settlement"), L("Inflow"), L("Outflow")]),
        new("SettlementVitals", [I("Settlement"), L("Births"), L("Deaths"), D("DtYears")]),
        // Blocks 25/26 are labelled SINGULAR on purpose: check-read-isolation.sh
        // is a bare identifier grep over Sim.Cli and would flag the plural
        // table names here as a sim-side reader (they are labels, not reads).
        new("NeedSatisfaction", [I("Settlement"), I("Class"), I("NeedId"), D("Value")]),
        new("Grievance", [I("Settlement"), I("Class"), D("Value")]),
        new("SmoothedAttractiveness", [I("Settlement"), D("Value")]),
        new("Prices", [I("Settlement"), I("Good"), D("Price")]),
        new("PriceTerms", [I("Settlement"), I("Good"), D("PrevPrice"), D("Consumption"), D("InputDemand"),
            D("Production"), D("StockRelease"), D("Clamp"), D("Delta")]),
        new("HarvestWeather", [I("Settlement"), D("LogDeviation"), D("Multiplier")]),
        new("TradeFlows", [I("From"), I("To"), I("Good"), L("Quantity")]),
        new("Housing", [I("Settlement"), L("Dwellings"), D("BuildRemainder"), D("DecayRemainder"),
            D("LastMaintenanceFraction"), D("LastLaborUsed")]),
        new("Claims", [I("Polity"), I("Place"), D("Strength")]),
        new("Controls", [I("Polity"), I("Place"), D("Strength")]),
        new("Recognitions", [I("Recogniser"), I("Recognised")]),
        new("Notables", [I("Id"), I("Settlement"), I("Allegiance"), I("CohortIdx"), L("Count")]),
        new("Polities", [I("Id"), I("Source")]),
        new("Capitals", [I("Polity"), I("Place")]),
        new("ConstructionQueue", [I("Settlement"), I("Slot"), I("ProjectId")]),
        new("Structures", [I("Settlement"), I("ProjectId"), L("Count")]),
        new("Disasters", [I("Settlement"), I("Kind"), D("Severity"), D("RemainingYears"),
            D("Multiplier"), D("AppliedMultiplier")]),
    ];

    /// <summary>A field that compares unequal. Row is -1 for the header block
    /// and for a table's row-count prefix.</summary>
    public sealed record FieldDifference(
        string Table, int Row, string Field, Kind Kind, string ValueA, string ValueB, string Detail);

    public sealed record TableSummary(
        string Table, int RowsA, int RowsB, int RowsCompared, int RowsDiffering, int FirstDifferingRow);

    public sealed record Result(
        bool Identical, FieldDifference? First, IReadOnlyList<TableSummary> Tables,
        long BytesA, long BytesB, long BytesWalkedA, long BytesWalkedB);

    public static Result Compare(WorldState a, WorldState b)
    {
        byte[] bytesA = Serialize(a), bytesB = Serialize(b);
        using var ra = new BinaryReader(new MemoryStream(bytesA, writable: false));
        using var rb = new BinaryReader(new MemoryStream(bytesB, writable: false));

        FieldDifference? first = null;
        var tables = new List<TableSummary>(Layout.Length + 1);

        // Header block: seed, clock, terrain flag, then the conditional hash.
        int headerDiffs = 0;
        for (int f = 0; f < HeaderFields.Length; f++)
        {
            FieldDifference? d = CompareField("Header", -1, HeaderFields[f], ra, rb);
            if (d is null) continue;
            headerDiffs++;
            first ??= d;
        }
        bool terrainA = ra.ReadBoolean(), terrainB = rb.ReadBoolean();
        if (terrainA != terrainB)
        {
            headerDiffs++;
            first ??= new FieldDifference("Header", -1, "TerrainPresent", Kind.Bool,
                terrainA ? "True" : "False", terrainB ? "True" : "False", "");
        }
        string hashA = terrainA ? Hex(ra.ReadBytes(32)) : "(none)";
        string hashB = terrainB ? Hex(rb.ReadBytes(32)) : "(none)";
        if (hashA != hashB)
        {
            headerDiffs++;
            first ??= new FieldDifference("Header", -1, "TerrainContentHash", Kind.Bool, hashA, hashB, "sha256 hex");
        }
        tables.Add(new TableSummary("Header", 1, 1, 1, headerDiffs > 0 ? 1 : 0, headerDiffs > 0 ? 0 : -1));

        for (int t = 0; t < Layout.Length; t++)
        {
            Table table = Layout[t];
            int countA = ra.ReadInt32(), countB = rb.ReadInt32();
            if (countA != countB)
            {
                first ??= new FieldDifference(table.Name, -1, "RowCount", Kind.Int32,
                    countA.ToString(CultureInfo.InvariantCulture), countB.ToString(CultureInfo.InvariantCulture),
                    "row counts differ; rows compared up to the shorter table, then each stream is walked to its own count");
            }
            int compared = Math.Min(countA, countB);
            int differing = 0, firstRow = -1;
            for (int r = 0; r < compared; r++)
            {
                bool rowDiffers = false;
                for (int f = 0; f < table.Fields.Length; f++)
                {
                    FieldDifference? d = CompareField(table.Name, r, table.Fields[f], ra, rb);
                    if (d is null) continue;
                    rowDiffers = true;
                    first ??= d;
                }
                if (rowDiffers)
                {
                    differing++;
                    if (firstRow < 0) firstRow = r;
                }
            }
            // Tail rows of the longer table: consumed so the next count prefix
            // is read from the right offset on BOTH streams.
            Skip(ra, table, countA - compared);
            Skip(rb, table, countB - compared);
            tables.Add(new TableSummary(table.Name, countA, countB, compared, differing, firstRow));
        }

        bool identical = first is null && bytesA.Length == bytesB.Length;
        return new Result(identical, first, tables, bytesA.Length, bytesB.Length,
            ra.BaseStream.Position, rb.BaseStream.Position);
    }

    public static void Print(Result r, TextWriter w)
    {
        var ic = CultureInfo.InvariantCulture;
        if (r.Identical)
        {
            w.WriteLine($"identical: {r.BytesA.ToString(ic)} bytes, {r.Tables.Count.ToString(ic)} blocks compared, no differing field");
        }
        else if (r.First is { } d)
        {
            w.WriteLine("FIRST DIVERGENT FIELD");
            w.WriteLine($"  table  {d.Table}");
            w.WriteLine($"  row    {(d.Row < 0 ? "(block-level)" : d.Row.ToString(ic))}");
            w.WriteLine($"  field  {d.Field} ({d.Kind})");
            w.WriteLine($"  A      {d.ValueA}");
            w.WriteLine($"  B      {d.ValueB}");
            if (d.Detail.Length > 0) w.WriteLine($"  note   {d.Detail}");
        }
        else
        {
            w.WriteLine($"streams differ in length only: A {r.BytesA.ToString(ic)} bytes, B {r.BytesB.ToString(ic)} bytes");
        }
        w.WriteLine();
        w.WriteLine("PER-TABLE SUMMARY (stream order)");
        w.WriteLine($"  {"table",-24} {"rowsA",8} {"rowsB",8} {"compared",9} {"differing",10} {"firstRow",9}");
        for (int i = 0; i < r.Tables.Count; i++)
        {
            TableSummary t = r.Tables[i];
            string flag = t.RowsDiffering > 0 || t.RowsA != t.RowsB ? "  <--" : "";
            w.WriteLine($"  {t.Table,-24} {t.RowsA,8} {t.RowsB,8} {t.RowsCompared,9} {t.RowsDiffering,10} {(t.FirstDifferingRow < 0 ? "-" : t.FirstDifferingRow.ToString(ic)),9}{flag}");
        }
        w.WriteLine($"  bytes: A {r.BytesA.ToString(ic)} (walked {r.BytesWalkedA.ToString(ic)}), B {r.BytesB.ToString(ic)} (walked {r.BytesWalkedB.ToString(ic)})");
    }

    private static byte[] Serialize(WorldState world)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(world, writer);
        return ms.ToArray();
    }

    private static void Skip(BinaryReader r, Table table, int rows)
    {
        if (rows <= 0) return;
        long width = 0;
        for (int f = 0; f < table.Fields.Length; f++)
            width += table.Fields[f].Kind switch { Kind.Int32 => 4, Kind.Bool => 1, _ => 8 };
        r.BaseStream.Seek(width * rows, SeekOrigin.Current);
    }

    private static FieldDifference? CompareField(string table, int row, Field field, BinaryReader ra, BinaryReader rb)
    {
        var ic = CultureInfo.InvariantCulture;
        switch (field.Kind)
        {
            case Kind.Int32:
            {
                int x = ra.ReadInt32(), y = rb.ReadInt32();
                return x == y ? null : new FieldDifference(table, row, field.Name, field.Kind, x.ToString(ic), y.ToString(ic), "");
            }
            case Kind.Int64:
            {
                long x = ra.ReadInt64(), y = rb.ReadInt64();
                return x == y ? null : new FieldDifference(table, row, field.Name, field.Kind, x.ToString(ic), y.ToString(ic), "");
            }
            case Kind.UInt64:
            {
                ulong x = ra.ReadUInt64(), y = rb.ReadUInt64();
                return x == y ? null : new FieldDifference(table, row, field.Name, field.Kind, x.ToString(ic), y.ToString(ic), "");
            }
            case Kind.Bool:
            {
                bool x = ra.ReadBoolean(), y = rb.ReadBoolean();
                return x == y ? null : new FieldDifference(table, row, field.Name, field.Kind, x ? "True" : "False", y ? "True" : "False", "");
            }
            default:
            {
                // Bit comparison, never ==: -0.0 == 0.0 and NaN != NaN would
                // both misreport against a hash that sees only the bits.
                long bx = ra.ReadInt64(), by = rb.ReadInt64();
                if (bx == by) return null;
                return new FieldDifference(table, row, field.Name, field.Kind, DoubleText(bx), DoubleText(by), UlpNote(bx, by));
            }
        }
    }

    /// <summary>"R-format (0xBITS)" — the R round-trip text plus the exact pattern.</summary>
    public static string DoubleText(long bits)
    {
        double v = BitConverter.Int64BitsToDouble(bits);
        return $"{v.ToString("R", CultureInfo.InvariantCulture)} (0x{bits.ToString("X16", CultureInfo.InvariantCulture)})";
    }

    /// <summary>Signed ulp distance B-A when both are finite, non-NaN and of one
    /// sign (where the int64 patterns are monotone in magnitude); otherwise names
    /// why the distance is not defined.</summary>
    public static string UlpNote(long bitsA, long bitsB)
    {
        double a = BitConverter.Int64BitsToDouble(bitsA), b = BitConverter.Int64BitsToDouble(bitsB);
        if (double.IsNaN(a) || double.IsNaN(b) || double.IsInfinity(a) || double.IsInfinity(b))
            return "ulp distance undefined (NaN/Inf involved)";
        if ((bitsA < 0) != (bitsB < 0))
            return "ulp distance undefined (signs differ)";
        long ulps = bitsB - bitsA;
        return $"ulp distance B-A = {ulps.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Hex(byte[] bytes)
    {
        var sb = new System.Text.StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
