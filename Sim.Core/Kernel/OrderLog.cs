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
    /// ADR-033 D4 (the M5 governing loop, ported from `m5-full-build`, which held kind 5 since
    /// ADR-029 §4 reserved it): set the issuing Empire's standing TAX POLICY. TargetId = the
    /// issuing Empire's own PolityId; Amount = the nominal rate as a PERCENTAGE in [0, 100] (the
    /// convention LaborAllocation and SectorAllocation use). The five-field OrderRecord already
    /// encodes this, so the wire format is untouched. Player and AI issue the SAME order
    /// (<see cref="State.Governance.TaxOrder"/>).
    ///
    /// Range-validated at LOAD (Amount in [0, 100], TargetId ≥ 0). That the target is the issuing
    /// Empire and the issuer is a roster Empire is world-dependent and checked in OrderValidation.
    /// Whether the issuer can levy a tax at all — the research gate, sim.json
    /// governance.taxationRequires — is state-dependent and checked where the order is consumed
    /// (GovernanceSystem, via <see cref="State.Governance.CanLevyTax"/> on PREV, the ResearchSystem
    /// precedent): an order failing it changes nothing, and the last valid order of a turn wins.
    /// DELIVERY: an order stamped turn t writes the policy row in the step t → t+1 (first visible
    /// in the state of turn t+1); production and every system that reads happiness read it from
    /// PREV, so its first effect on output, migration and revolt lands in the step t+1 → t+2.
    ///
    /// A POLICY, NOT A TRANSACTION (CR-008): this order moves no goods and creates no stock.
    /// </summary>
    SetTaxRate = 5,

    /// <summary>
    /// ADR-029 / D-044 R9: set, change or clear the issuing Empire's ONE active
    /// research target. TargetId = the node's STABLE key from research.json (a
    /// Technology or a Civics node; one RP pool serves both trees, R2 and R12), or
    /// -1 to clear the target. Amount is reserved and must be exactly 0. A
    /// persistent directive (D-042 §6.2): the target stays until it completes or
    /// another order changes it.
    ///
    /// Range-validated at LOAD (TargetId is a key ≥ 1 or exactly -1; Amount == 0). Whether the node exists
    /// and is AVAILABLE to this Empire is content- and state-dependent, so it is
    /// checked where the order is consumed (ResearchSystem, the ConstructionSystem
    /// precedent); an order naming an unavailable node changes nothing. DELIVERY: an
    /// order stamped turn t retargets the step t → t+1, and that step's RP already
    /// goes to the new target.
    /// </summary>
    SetResearchTarget = 6,

    /// <summary>
    /// ADR-031 / D-047 ruling 13: the issuing Empire's explicit decision to ADVANCE to its
    /// next Age. TargetId = the Age being entered (2..9, which must be the issuer's current
    /// Age + 1); Amount = the chosen Age-surge emphasis key (ages.json surges[].key), a whole
    /// number >= 1 carried exactly by the double. Player and AI issue the same order.
    ///
    /// Range-validated at LOAD. Whether the Age is the issuer's NEXT Age, the surge exists and
    /// the issuer is ELIGIBLE (milestones) is state-dependent and checked where it is consumed
    /// (AgeTransitionSystem, via AgeQuery.CheckAdvance); an order failing it changes nothing.
    /// DELIVERY: an order stamped turn t is applied by the step t → t+1 — every other system of
    /// that step still reads the old Age (nothing retroactive) and the state of turn t+1 is the
    /// first under the new Age.
    /// </summary>
    AdvanceAge = 7,

    /// <summary>
    /// ADR-032 (the Director's transport rulings 1, 2, 9, 13): the issuing Empire's road-
    /// development ACTION — "modernize X% of eligible inter-city transport demand". TargetId is
    /// reserved and must be 0; Amount = the percentage in (0, 100] (the slider). Player and AI
    /// issue the SAME order (RoadDevelopmentQuery.DevelopOrder) and it goes through the SAME
    /// selection (RoadDevelopmentQuery.Plan) — there is no player-only road system.
    ///
    /// Range-validated at LOAD. Which routes are eligible (research, existing classes, control
    /// of an endpoint) and what the issuer can afford are state-dependent and resolved where the
    /// order is consumed (RoadDevelopmentSystem). One DevelopRoads per Empire per turn takes
    /// effect — the first in log order; later ones that turn change nothing.
    /// DELIVERY: an order stamped turn t is applied by the step t → t+1, against the state of
    /// turn t (usage = turn t's realised trade, research = turn t's completed knowledge); the
    /// built edges and the consumed materials first appear in the state of turn t+1.
    /// </summary>
    DevelopRoads = 8,
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
            case OrderKind.SetTaxRate:
                if (!(record.Amount >= 0.0 && record.Amount <= 100.0)) // NaN fails this too
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetTaxRate percentage must be in " +
                        $"[0,100], got {record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                if (record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetTaxRate target polity id must be " +
                        $">= 0, got {record.TargetId}.");
                break;
            case OrderKind.SetResearchTarget:
                if (record.TargetId != -1 && record.TargetId < 1)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetResearchTarget node key must be a " +
                        $"research.json key (>= 1) or -1 to clear the target, got {record.TargetId}.");
                if (record.Amount != 0.0) // NaN fails this too
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetResearchTarget Amount is reserved and must be 0, got " +
                        $"{record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            case OrderKind.AdvanceAge:
                if (record.TargetId < 2 || record.TargetId > 9)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): AdvanceAge target Age must be in 2..9 " +
                        $"(the Age being entered), got {record.TargetId}.");
                if (!(record.Amount >= 1.0) || record.Amount != Math.Floor(record.Amount) || record.Amount > int.MaxValue)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): AdvanceAge surge key must be a whole number >= 1 " +
                        $"carried exactly by Amount, got {record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            case OrderKind.DevelopRoads:
                if (record.TargetId != 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): DevelopRoads TargetId is reserved and must be 0, got {record.TargetId}.");
                if (!(record.Amount > 0.0 && record.Amount <= 100.0)) // NaN fails this too
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): DevelopRoads percentage must be in (0,100], got " +
                        $"{record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            default:
                throw new SnapshotFormatException(
                    $"order[{index}] (turn {record.Turn}): unknown order kind {(int)record.Kind}; " +
                    "this build understands kinds 1 (SetRainBias), 2 (LaborAllocation), 3 (SectorAllocation), " +
                    "4 (EnqueueConstruction), 5 (SetTaxRate), 6 (SetResearchTarget), 7 (AdvanceAge) and 8 (DevelopRoads).");
        }
    }
}
