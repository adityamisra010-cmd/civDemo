namespace Sim.Core.Kernel;

/// <summary>
/// The order vocabulary. Payload mapping is per-kind and documented here — the
/// OrderRecord shape {Turn, ActorId, Kind, TargetId, Amount} is fixed.
/// </summary>
public enum OrderKind
{
    /// <summary>Adds a flat mm/year bias to one region's rainfall draw this turn
    /// (consumed by the retired toy WeatherSystem; kept for the toy preset and
    /// the kernel-invariant order-pipe tests). TargetId = region id.</summary>
    SetRainBias = 1,

    /// <summary>
    /// The first REAL order (T1.6, m1 spec §3): sets a settlement's labor split.
    /// TargetId = settlement id; Amount = farm percentage in [0,100] (100 = all
    /// labor farms, 0 = all labor builds paths). Consumed by PathBuildSystem
    /// into the LaborAllocations row; Farming and PathBuild read the row from
    /// Prev the following turn. Range-validated at LOAD time.
    /// </summary>
    LaborAllocation = 2,

    /// <summary>
    /// T3.3 (D-032): sets ONE sector's raw labor weight for a settlement.
    /// The fixed OrderRecord shape carries one double, so a full five-way
    /// allocation is issued as a BATCH of these (one per sector, same turn) —
    /// TargetId = settlementId × 8 + sectorId (Sectors.Farming..Construction,
    /// 0..4; ×8 leaves headroom and decodes with shift/mask), Amount = weight
    /// percentage in [0,100]. Consumed by PathBuildSystem into the
    /// SectorAllocations row (raw weights; consumers read NORMALIZED shares,
    /// so a partial batch is well-defined). The legacy LaborAllocation order
    /// stays valid and maps onto the same row: farming = pct, construction =
    /// 100 − pct, other sectors zeroed — the M1/M2 fixtures replay unchanged
    /// in meaning.
    /// </summary>
    SectorAllocation = 3,

    /// <summary>
    /// M4-D: enqueue a construction project in a settlement the issuing Empire
    /// controls. TargetId = settlement id; Amount = the project id, a whole
    /// number carried exactly by the double (project ids are small integers, far
    /// inside the 2^53 exact range). The five-field OrderRecord already encodes
    /// this, so the wire format is untouched.
    ///
    /// Range-validated at LOAD (a project id must be a non-negative integer);
    /// the settlement's existence AND the issuing Empire's CONTROL of it are
    /// world-dependent and checked in OrderValidation.
    /// </summary>
    EnqueueConstruction = 4,

    /// <summary>
    /// M5: set an Empire's standing TAX POLICY. TargetId = the issuing Empire's
    /// PolityId; Amount = the nominal rate as a PERCENTAGE in [0,100], matching
    /// the convention LaborAllocation and SectorAllocation already use so the
    /// order corpus reads consistently.
    ///
    /// The five-field OrderRecord already encodes this, so the WIRE FORMAT IS
    /// UNTOUCHED. Range-validated at LOAD; that the target polity is the issuing
    /// Empire is world-dependent and checked in OrderValidation.
    ///
    /// A POLICY, NOT A TRANSACTION: this order moves no goods and creates no
    /// stock. It records what the state asks for; what it actually collects is
    /// that rate scaled by administrative reach, computed each turn.
    /// </summary>
    SetTaxRate = 5,
}

/// <summary>
/// One external input to the sim (§3.9): {turn, actorId, payload}. Turn semantics:
/// an order with Turn = t is delivered to the step that transforms turn-t state
/// into turn-(t+1) state (i.e. delivered when Prev.Clock.Turn == t).
///
/// M4-B — WHAT <see cref="ActorId"/> MEANS. It is the STRATEGIC ACTOR issuing the
/// order: the <see cref="State.PolityId"/> of the Empire, read through
/// <see cref="Actor"/>. §3.9 already defined this field as the actor of a
/// "player/AI order", and <see cref="State.PolityId"/> is a one-int identity, so
/// the binding needs no new field, no new identity type and NO CHANGE TO THE WIRE
/// FORMAT — the int on disk was always this id.
///
/// It is NOT a command source. Who DECIDED (a human or the AI) is
/// <see cref="State.CommandSource"/> on the polity's roster row; who is ACTING is
/// this id. Never encode "the player" as an actor id — under D-042 a human and an
/// AI commanding the same Empire issue orders as the SAME actor, and one human
/// switching Empires changes the actor while the command source is unmoved.
/// </summary>
public readonly record struct OrderRecord(long Turn, int ActorId, OrderKind Kind, int TargetId, double Amount)
{
    /// <summary>
    /// The issuing Empire, typed. A projection of <see cref="ActorId"/>, never a
    /// second identity: <c>Actor.Value == ActorId</c> always, so nothing can drift
    /// between the serialized form and the strategic one.
    /// </summary>
    public State.PolityId Actor => new(ActorId);

    /// <summary>Builds a record from a typed issuer — the preferred constructor.</summary>
    public static OrderRecord From(
        long turn, State.PolityId actor, OrderKind kind, int targetId, double amount)
        => new(turn, actor.Value, kind, targetId, amount);
}

/// <summary>
/// Append-only order log — the second half of determinism (§3.9) and the save
/// recovery path (D-008): replay(seed, orderLog) must reproduce the run
/// hash-for-hash. A separate artifact from snapshots, with its own IO.
/// </summary>
public sealed class OrderLog
{
    private readonly List<OrderRecord> _records = [];

    public int Count => _records.Count;

    public OrderRecord this[int index] => _records[index];

    /// <summary>Append-only: records may only be added, in nondecreasing turn order.</summary>
    public void Append(OrderRecord record)
    {
        if (_records.Count > 0 && record.Turn < _records[^1].Turn)
            throw new ArgumentException(
                $"order log is append-only in turn order: cannot append turn {record.Turn} " +
                $"after turn {_records[^1].Turn}.");
        _records.Add(record);
    }

    /// <summary>All orders addressed to the step executing from turn-<paramref name="turn"/> state.</summary>
    public OrderBatch BatchFor(long turn)
    {
        int count = 0;
        for (int i = 0; i < _records.Count; i++)
            if (_records[i].Turn == turn) count++;
        if (count == 0) return OrderBatch.Empty;

        var orders = new OrderRecord[count];
        int j = 0;
        for (int i = 0; i < _records.Count; i++)
            if (_records[i].Turn == turn) orders[j++] = _records[i];
        return new OrderBatch(orders);
    }

    // --- IO: separate artifact, own header, field-by-field like the schema -----

    public const int IoVersion = 1;
    private static ReadOnlySpan<byte> Magic => "CIVORDR\0"u8;

    public void Save(Stream destination)
    {
        using var writer = new BinaryWriter(destination, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(IoVersion);
        writer.Write(_records.Count);
        for (int i = 0; i < _records.Count; i++)
        {
            OrderRecord r = _records[i];
            writer.Write(r.Turn);
            writer.Write(r.ActorId);
            writer.Write((int)r.Kind);
            writer.Write(r.TargetId);
            writer.Write(BitConverter.DoubleToInt64Bits(r.Amount));
        }
    }

    public static OrderLog Load(Stream source)
    {
        using var reader = new BinaryReader(source, System.Text.Encoding.UTF8, leaveOpen: true);

        Span<byte> magic = stackalloc byte[8];
        if (reader.Read(magic) != 8 || !magic.SequenceEqual(Magic))
            throw new SnapshotFormatException("not a civ-sim order log: bad magic (expected CIVORDR header).");

        int version = reader.ReadInt32();
        if (version != IoVersion)
            throw new SnapshotFormatException(
                $"order log is version {version}, this build reads only version {IoVersion}.");

        var log = new OrderLog();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var record = new OrderRecord(
                reader.ReadInt64(), reader.ReadInt32(), (OrderKind)reader.ReadInt32(),
                reader.ReadInt32(), BitConverter.Int64BitsToDouble(reader.ReadInt64()));
            ValidateRecord(record, i);
            log.Append(record);
        }
        return log;
    }

    /// <summary>
    /// Per-kind payload validation at LOAD time (T1.6): a malformed order is
    /// rejected here, actionably, before the sim ever runs — never mid-turn.
    /// (Settlement EXISTENCE needs a world and is checked by
    /// <see cref="OrderValidation.ValidateAgainstWorld"/> before turn 1.)
    /// </summary>
    private static void ValidateRecord(in OrderRecord record, int index)
    {
        switch (record.Kind)
        {
            case OrderKind.SetRainBias:
                break; // any bias amount is legal (the draw floors at zero)
            case OrderKind.LaborAllocation:
                if (!(record.Amount >= 0.0 && record.Amount <= 100.0)) // NaN fails this too
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): LaborAllocation farm percentage " +
                        $"must be in [0,100], got {record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            case OrderKind.SectorAllocation:
                if (!(record.Amount >= 0.0 && record.Amount <= 100.0)) // NaN fails this too
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SectorAllocation weight percentage " +
                        $"must be in [0,100], got {record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                if ((record.TargetId & 7) >= Sim.Core.State.Sectors.Count || record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SectorAllocation sector id " +
                        $"{record.TargetId & 7} unknown — sectors are 0..{Sim.Core.State.Sectors.Count - 1} " +
                        "(farming, herding, extraction, crafting, construction).");
                break;
            case OrderKind.SetTaxRate:
                if (!(record.Amount >= 0.0 && record.Amount <= 100.0))   // NaN fails this too
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetTaxRate percentage must be in " +
                        $"[0,100], got {record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                if (record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetTaxRate target polity id must be " +
                        $">= 0, got {record.TargetId}.");
                break;
            case OrderKind.EnqueueConstruction:
                // The project id rides in a double. Insist it is a non-negative
                // WHOLE number here rather than truncating later: 1.5 is not a
                // project, and silently flooring it would enqueue the wrong one.
                if (!(record.Amount >= 0.0) || record.Amount != Math.Floor(record.Amount)
                    || record.Amount > int.MaxValue)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): EnqueueConstruction project id must be a " +
                        "non-negative whole number carried exactly by Amount, got " +
                        $"{record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                if (record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): EnqueueConstruction settlement id must be " +
                        $">= 0, got {record.TargetId}.");
                break;
            default:
                throw new SnapshotFormatException(
                    $"order[{index}] (turn {record.Turn}): unknown order kind {(int)record.Kind}; " +
                    "this build understands kinds 1 (SetRainBias), 2 (LaborAllocation), 3 (SectorAllocation), " +
                    "4 (EnqueueConstruction) and 5 (SetTaxRate).");
        }
    }
}
