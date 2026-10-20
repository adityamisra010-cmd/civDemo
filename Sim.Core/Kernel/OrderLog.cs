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
    /// Whether the issuer can levy a tax at all — the tax gate: the Taxation knowledge (sim.json
    /// governance.taxationRequires, R5) AND the minimum Age (governance.taxationMinAge, Age III,
    /// H2) — is state-dependent and checked where the order is consumed
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

    /// <summary>
    /// D-051 §5 / ADR-036 (PROPOSED — the legality terms await the Director's confirmation): the issuing
    /// Empire's FOUND SETTLEMENT order. A party of colonists leaves one of the issuer's settlements with
    /// provisions and founds a settlement at a chosen site, through the EXISTING colonization founding path
    /// (ColonizationSystem.Found) — there is no settler unit and no movement system; founding is instantaneous.
    /// TargetId = the SOURCE settlement's id; Amount = the SITE CELL (row-major terrain cell index), a whole
    /// number carried exactly by the double (cells are &lt; 2^31, far inside the 2^53 exact range). The party
    /// size is not carried: it is the content fraction sim.json colonization.orderPartyFraction of the
    /// source's population (ADR-036 §3). The five-field OrderRecord already encodes this, so the wire format is
    /// untouched (IoVersion stays 1).
    ///
    /// Range-validated at LOAD only for what needs no world: the source id is ≥ 0 and the cell is a
    /// non-negative whole number (NaN and ±∞ are rejected). That the cell is on the map (&lt; Size²) needs the
    /// terrain and is checked in OrderValidation.ValidateAgainstWorld, with the source's existence and control
    /// (ADR-034 deferral for a source founded mid-game). Whether the site is LEGAL — land, scored, spaced,
    /// unclaimed, reachable, with enough people and food at the source — is state-dependent and decided where
    /// the order is consumed (ColonizationSystem, via <see cref="State.FoundingQuery"/>.Check):
    /// an order failing it changes nothing. The first LEGAL order per source per turn founds; later orders from
    /// that source in the same turn change nothing. Found Settlement orders resolve in log order BEFORE the
    /// emergent (D-037 B1) foundings of the same step.
    /// DELIVERY: an order stamped turn t founds in the step t → t+1 — the SettlementRow first appears in the
    /// state of turn t+1 with FoundedTurn t+1; its catchment first appears in the state of t+2 (CatchmentSystem
    /// recomputes on the next step), and GovernanceSystem's reach for it is 0.0 in t+1 and t+2 (no distance row
    /// in PREV yet) and real from t+3.
    /// </summary>
    FoundSettlement = 9,

    // Kind 10 is RESERVED for MoveFormation (M7; D-052 §4, the m5w plan): never assign it here.

    /// <summary>
    /// WS1 (D-054 — the Director's 2026-10-14 instruction, workstream 1, item 9; ADR-039): CANCEL one entry of a
    /// settlement's construction queue. TargetId = the settlement id; Amount = the entry's SLOT, a whole number
    /// &gt;= 0 carried exactly by the double. NO REFUND: what has gone into the entry — its delivered materials and
    /// the labour spent on it — was sunk as the work progressed and is forfeited (a salvage refund is an open Director
    /// choice, D-054). The next slot becomes the head with its own progress. The five-field OrderRecord already encodes
    /// this, so the wire format is untouched (IoVersion stays 1).
    ///
    /// Range-validated at LOAD (TargetId &gt;= 0; Amount finite, whole, in [0, int.MaxValue]). The settlement's
    /// existence and the issuer's CONTROL of it are checked in OrderValidation (the EnqueueConstruction rule) — and
    /// AGAIN WHERE IT IS CONSUMED, because that pass sees only the turn-0 world and only loaded logs
    /// (ConstructionQuery.CancelOrderValid on PREV and the step's NEXT queue: a roster actor, control, the payload
    /// re-checked, the slot present). An order failing it changes nothing — never floored, never clamped.
    /// DELIVERY: an order stamped turn t removes the entry in the step t → t+1, BEFORE that step's enqueues and building
    /// work (ConstructionSystem's fixed order), so the state of turn t+1 no longer holds it and nothing more goes in.
    ///
    /// LEGACY (D-056): no longer emitted. A slot is reused once its entry is gone, so a recorded slot-only cancel can name a
    /// DIFFERENT entry in a world that has diverged from the recorded one. Recorded kind-11 orders are still read and
    /// replayed — but only where a session trace proves, before each batch that carries one, that the world is the
    /// recorded one (Sim.Core.Kernel.ReplayGuard); a bare log carrying one is refused. Every emitter issues
    /// <see cref="CancelConstructionEntry"/>.
    /// </summary>
    CancelConstruction = 11,

    /// <summary>
    /// WS2 (D-054; ADR-039): set the issuing Empire's standing BUILDING/ROADS MATERIAL SPLIT. TargetId = the issuing
    /// Empire's own PolityId (the SetTaxRate convention); Amount = BuildingsPercent, a WHOLE number in [0, 100] (roads
    /// get the rest). Range-validated at LOAD; that the target is the issuer and the issuer a roster Empire is checked
    /// in OrderValidation and again at delivery (ConstructionQuery.AllocationOrderValid, which also needs sim.json
    /// construction.allocationRatios on). DELIVERY: an order stamped turn t upserts its Empire's ResourceAllocationRow (v33)
    /// in the step t → t+1, BEFORE that step's building work (ConstructionSystem's step 2), so it governs that very step's
    /// split; the last valid order of an Empire in a step wins.
    /// </summary>
    SetResourceAllocation = 12,

    /// <summary>
    /// D-056 (supersedes kind 11 as the emitted cancel; the Director's 2026-10-18 mandate) — CANCEL ONE QUEUE ENTRY BY
    /// IDENTITY. Kind 11 named only (settlement, slot); slots are reused once a queue empties or its top slot leaves, so a
    /// recorded kind 11 replayed under rules whose completion timing differs could remove a DIFFERENT entry and forfeit
    /// its progress (the legacy-session defect: a completed granary's 500,000 lost while the cancelled one kept building).
    /// Kind 13 carries what makes the entry the entry: TargetId = the settlement id; Amount = the packed
    /// <see cref="CancelTarget"/> (slot, project id, enqueue turn), a whole number below 2^53 carried exactly by the double
    /// (<see cref="OrderLog.PackCancelTarget"/>). The wire format is untouched (IoVersion stays 1).
    ///
    /// RESOLUTION (ConstructionQuery.CancelTargetIndex, one function for the system, the delivery check and the UI): the
    /// entry of the settlement whose ProjectId and EnqueuedTurn equal the recorded ones — the one at the recorded slot when
    /// several do. So a cancel follows the entry it was issued for and never lands on another one. No entry with that
    /// identity: the order changes nothing (the entry is already gone — built or cancelled). Two or more with that identity
    /// and none at the recorded slot: AMBIGUOUS, and a replayed log is refused at delivery
    /// (OrderValidation.ValidateAtDelivery) rather than guessed at. Load, control and delivery rules are kind 11's.
    /// </summary>
    CancelConstructionEntry = 13,
}

/// <summary>
/// D-056 — the identity a <see cref="OrderKind.CancelConstructionEntry"/> carries: the entry's slot when the order was
/// issued (a hint that disambiguates duplicates), its project and the turn its enqueue order was accepted (−1 for an
/// entry migrated from a v32 save, which has no progress row).
/// </summary>
public readonly record struct CancelTarget(int Slot, int ProjectId, long EnqueuedTurn);

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

    /// <summary>A CancelConstruction slot payload: finite, a whole number, in [0, int.MaxValue] (NaN and ±∞ fail).
    /// The ONE payload rule, applied at load here and again by the consumer at delivery
    /// (ConstructionQuery.CancelOrderValid) — an in-memory OrderLog.Append never validates.</summary>
    public static bool ConstructionSlotValid(double amount) =>
        amount >= 0.0 && amount <= int.MaxValue && amount == Math.Floor(amount);

    /// <summary>D-056 — the field widths of a packed <see cref="CancelTarget"/>: Amount = slot + 2^18 × (projectId +
    /// 2^16 × (enqueuedTurn + 1)); slot in [0, 2^18), project in [0, 2^16), enqueue turn in [−1, 2^19 − 1) — 524,286
    /// turns, far past a 6,000-year game at any era pace. 53 bits, so the double carries it exactly.</summary>
    public const int CancelSlotBits = 18, CancelProjectBits = 16, CancelTurnBits = 19;

    /// <summary>D-056 — whether every field of <paramref name="target"/> fits its width (<see cref="PackCancelTarget"/>).</summary>
    public static bool CanPackCancelTarget(CancelTarget target) =>
        target.Slot >= 0 && target.Slot < 1 << CancelSlotBits
        && target.ProjectId >= 0 && target.ProjectId < 1 << CancelProjectBits
        && target.EnqueuedTurn >= -1 && target.EnqueuedTurn + 1 < 1L << CancelTurnBits;

    /// <summary>D-056 — packs <paramref name="target"/> into a CancelConstructionEntry Amount. Throws when a field is out
    /// of range: an order that cannot carry its target's identity is never emitted.</summary>
    public static double PackCancelTarget(CancelTarget target)
    {
        if (!CanPackCancelTarget(target))
            throw new ArgumentOutOfRangeException(nameof(target),
                $"cannot encode cancel target (slot {target.Slot}, project {target.ProjectId}, enqueued turn " +
                $"{target.EnqueuedTurn}): needs slot < 2^{CancelSlotBits}, project < 2^{CancelProjectBits}, " +
                $"turn + 1 < 2^{CancelTurnBits}.");
        return target.Slot + (1L << CancelSlotBits) * (target.ProjectId + (1L << CancelProjectBits) * (target.EnqueuedTurn + 1));
    }

    /// <summary>D-056 — the inverse of <see cref="PackCancelTarget"/>. False unless <paramref name="amount"/> is a whole
    /// number in [0, 2^53) (NaN and ±∞ fail); every such number decodes to fields inside their ranges.</summary>
    public static bool TryUnpackCancelTarget(double amount, out CancelTarget target)
    {
        target = default;
        if (!(amount >= 0.0) || amount >= 9007199254740992.0 || amount != Math.Floor(amount)) return false;
        long packed = (long)amount;
        int slot = (int)(packed & ((1L << CancelSlotBits) - 1));
        long rest = packed >> CancelSlotBits;
        int project = (int)(rest & ((1L << CancelProjectBits) - 1));
        long turn = (rest >> CancelProjectBits) - 1;
        target = new CancelTarget(slot, project, turn);
        return true;
    }

    /// <summary>A SetResourceAllocation payload: a whole number in [0, 100] (NaN fails).</summary>
    public static bool AllocationPercentValid(double amount) =>
        amount >= 0.0 && amount <= 100.0 && amount == Math.Floor(amount);

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
            case OrderKind.FoundSettlement:
                // ADR-036: only the world-free checks live here (the critic correction to the S1b plan). The cell
                // rides in a double: insist it is a non-negative WHOLE number rather than truncating later — a
                // fractional cell is not a place, and flooring it would found somewhere the issuer did not choose.
                // "On the map" (< Size²) needs the terrain: OrderValidation.ValidateAgainstWorld checks it.
                if (record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): FoundSettlement source settlement id must be " +
                        $">= 0, got {record.TargetId}.");
                if (!(record.Amount >= 0.0) || record.Amount != Math.Floor(record.Amount)
                    || record.Amount > int.MaxValue)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): FoundSettlement site cell must be a non-negative " +
                        "whole number carried exactly by Amount, got " +
                        $"{record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            case OrderKind.CancelConstruction:
                // WS1: the slot rides in a double. A non-negative WHOLE number or the log is refused — 2.5 is not a slot,
                // and a NaN saturating to 0 would cancel the head (the reason the consumer re-checks it too).
                if (record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): CancelConstruction settlement id must be " +
                        $">= 0, got {record.TargetId}.");
                if (!ConstructionSlotValid(record.Amount))
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): CancelConstruction slot must be a non-negative whole " +
                        "number carried exactly by Amount, got " +
                        $"{record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            case OrderKind.CancelConstructionEntry:
                if (record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): CancelConstructionEntry settlement id must be " +
                        $">= 0, got {record.TargetId}.");
                if (!TryUnpackCancelTarget(record.Amount, out _))
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): CancelConstructionEntry target must be a packed whole " +
                        "number in [0, 2^53) carried exactly by Amount, got " +
                        $"{record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            case OrderKind.SetResourceAllocation:
                if (record.TargetId < 0)
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetResourceAllocation target polity id must be " +
                        $">= 0, got {record.TargetId}.");
                if (!AllocationPercentValid(record.Amount))
                    throw new SnapshotFormatException(
                        $"order[{index}] (turn {record.Turn}): SetResourceAllocation building share must be a whole " +
                        "number in [0,100], got " +
                        $"{record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                break;
            default:
                throw new SnapshotFormatException(
                    $"order[{index}] (turn {record.Turn}): unknown order kind {(int)record.Kind}; " +
                    "this build understands kinds 1 (SetRainBias), 2 (LaborAllocation), 3 (SectorAllocation), " +
                    "4 (EnqueueConstruction), 5 (SetTaxRate), 6 (SetResearchTarget), 7 (AdvanceAge), 8 (DevelopRoads), " +
                    "9 (FoundSettlement), 11 (CancelConstruction), 12 (SetResourceAllocation) and " +
                    "13 (CancelConstructionEntry); kind 10 is reserved for MoveFormation (M7), and kind 11 is legacy " +
                    "(read, no longer written: D-056).");
        }
    }
}
