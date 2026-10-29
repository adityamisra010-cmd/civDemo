using System.Globalization;
using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// D-057 — THE IDENTITY PROOF of a replayed session: before a recorded batch is delivered, every settlement and polity the
/// batch names must be shown to be the entity the order was given for. One instance per replay, fed each turn's batch in
/// turn order (<see cref="BeforeDelivery"/>); used by every runner that continues or inspects a RECORDED session
/// (UiSession.ReplayTo, `sim inspect`).
///
/// THE DEFECT IT CLOSES (D-056 §8's residual, reproduced in D-057 §1). An order names its settlement and its issuing
/// polity by id, and an id allocated after turn 0 is "the next free one" (ColonizationSystem: settlement max + 1;
/// RevoltSystem: roster max + 1). Ids are never reused, so in a replay that IS the recorded game every id means what it
/// meant. In a replay that has drifted from it — another build's rules, another schema — colonies can be founded in
/// another order or not at all, and the same id names a DIFFERENT colony or revolt-born polity: a recorded order for the
/// player's colony was applied to another one without a word.
///
/// WHAT IS PROVEN, AND HOW.
/// <list type="bullet">
/// <item>A replayed world that equals the session trace's row for its turn in some readable schema form
///   (<see cref="ReplayGuard.MatchesRecorded"/>) IS the recorded world, so every settlement and polity it holds is the
///   recorded one — and, ids being allocated in order and never reused, so is every one it held earlier. The proof
///   therefore keeps the bounds of the LAST matching turn: settlement ids up to that world's highest, polity ids up to its
///   highest. Each turn is checked until the first one that differs (that is the drift; nothing later is checked eagerly,
///   since a drifted replay does not reconverge in practice); afterwards a batch naming an id above the bounds is checked
///   against its own turn's row, and refused if that row does not match.</item>
/// <item>The FOUNDED ROSTER — the player (polity 1) and the AI Empires founded with the world (WorldFounding) — are fixed
///   roles that every build founds the same way from the manifest, so their ids are proven by construction. A polity
///   above them was founded by a revolt and is proven only like a colony.</item>
/// <item>An id the delivered-to world does not hold is not this check's business: it names nothing to misapply to, and
///   ADR-034's delivery check (<see cref="OrderValidation.ValidateAtDelivery"/>) refuses it.</item>
/// <item>A legacy slot-only cancel (kind 11) keeps D-056's stricter rule — the world it is delivered to must itself match
///   (<see cref="ReplayGuard.CheckBeforeDelivery"/>), because a slot is reused within a settlement.</item>
/// </list>
///
/// A session with NO trace proves nothing beyond the founded roster: a batch naming any settlement is refused (there is no
/// way to show even the founding reproduced). Every refusal is an <see cref="OrderValidationException"/> raised before the
/// batch is delivered — the type every runner already reports (Cli.Guard and ResumeLaunch: exit 2) — with a message that
/// says which order, why it is not applied, that nothing was changed, and what to do. Reads only: it never writes a world.
/// </summary>
public sealed class ReplayProof
{
    private readonly IReadOnlyList<SessionTrace.Row>? _trace;
    private readonly long[] _turns;      // the trace's turns, ascending (the first row of each turn)
    private readonly string[] _hashes;   // the hash of that row
    private readonly int _recordedSchema;
    private readonly int _rosterMax;
    private readonly string _describedAs;
    private readonly string? _recordedBy;
    private readonly string _remedy;

    private int _settlementMax = -1;   // settlement ids up to this are proven
    private int _polityMax;            // polity ids up to this are proven (at least the founded roster's)
    private long _provenTurn = -1;
    private long _firstDifference = -1;

    /// <param name="trace">The session trace; null when the session has none.</param>
    /// <param name="recordedSchema">The schema the session was recorded under (its manifest): the form tried first.</param>
    /// <param name="foundedRosterMax">The highest polity id the founding gives (1 + the manifest's AI Empires).</param>
    /// <param name="describedAs">Names the session in a refusal.</param>
    /// <param name="recordedBy">The build that recorded it, when known: named in the remedy.</param>
    /// <param name="remedy">The refusal's last sentence; null for the session default (the build that recorded it, or a
    /// new game).</param>
    public ReplayProof(IReadOnlyList<SessionTrace.Row>? trace, int recordedSchema, int foundedRosterMax,
        string describedAs, string? recordedBy, string? remedy = null)
    {
        _trace = trace;
        _recordedSchema = recordedSchema;
        _rosterMax = foundedRosterMax;
        _polityMax = foundedRosterMax;
        _describedAs = describedAs;
        _recordedBy = recordedBy;
        _remedy = remedy ?? "Nothing was applied and no session file was changed. To continue this session, open it with the "
            + "build that recorded it" + (string.IsNullOrEmpty(recordedBy) ? "" : $" ({recordedBy})")
            + "; to play this build, start a new game.";
        int n = trace?.Count ?? 0;
        var turns = new List<long>(n);
        var hashes = new List<string>(n);
        // The rows in turn order, the first row of a turn kept (a trace is written in turn order; a repeated turn is
        // read the way ReplayGuard.CheckBeforeDelivery reads it).
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            int byTurn = trace![a].Turn.CompareTo(trace[b].Turn);
            return byTurn != 0 ? byTurn : a.CompareTo(b);
        });
        for (int i = 0; i < n; i++)
        {
            SessionTrace.Row row = trace![order[i]];
            if (turns.Count > 0 && turns[^1] == row.Turn) continue;
            turns.Add(row.Turn);
            hashes.Add(row.Hash);
        }
        _turns = [.. turns];
        _hashes = [.. hashes];
    }

    /// <summary>The last turn the replay was shown to be the recorded world; −1 when none was.</summary>
    public long ProvenTurn => _provenTurn;

    /// <summary>The first turn the replay was found to differ from the recorded world; −1 when none was found.</summary>
    public long FirstDifference => _firstDifference;

    /// <summary>The highest settlement id shown to name the recorded settlement; −1 when none.</summary>
    public int ProvenSettlementMax => _settlementMax;

    /// <summary>
    /// The settlement <paramref name="order"/> names — its target for the settlement-addressed kinds (2, 4, 9, 11, 13, 14, 15;
    /// kind 3's settlement × 8 + sector decoded) — or −1 for an order that names none (its target is the issuer, a
    /// research node, an Age, or a region).
    /// </summary>
    public static int AddressedSettlement(OrderRecord order) => order.Kind switch
    {
        OrderKind.LaborAllocation or OrderKind.EnqueueConstruction or OrderKind.FoundSettlement
            or OrderKind.CancelConstruction or OrderKind.CancelConstructionEntry
            or OrderKind.Explore or OrderKind.Prospect or OrderKind.RecruitFormation => order.TargetId,
        OrderKind.SectorAllocation => order.TargetId >> 3,
        _ => -1,
    };

    /// <summary>
    /// THE CHECK, before <paramref name="batch"/> is delivered to <paramref name="prev"/> (the world of the batch's turn):
    /// throws <see cref="OrderValidationException"/> unless every settlement and polity the batch names is proven (see the
    /// type's summary), and — for a batch carrying a legacy kind-11 cancel — unless <paramref name="prev"/> itself matches
    /// the trace. Call it once per turn, in turn order, before the batch is appended or delivered. Reads only.
    /// </summary>
    public void BeforeDelivery(OrderBatch batch, WorldState prev)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(prev);
        long turn = prev.Clock.Turn;
        Observe(prev);   // until the first difference: every turn
        if (ReplayGuard.LegacyCancels(batch) > 0 && _provenTurn != turn)
            ReplayGuard.CheckBeforeDelivery(batch, prev, _trace, _describedAs, _recordedBy);   // D-056: refuses here
        for (int i = 0; i < batch.Count; i++)
        {
            OrderRecord order = batch[i];
            int settlement = AddressedSettlement(order);
            bool settlementUnproven = settlement > _settlementMax && Holds(prev, settlement, out _);
            bool actorUnproven = order.ActorId > _polityMax && HoldsPolity(prev, order.ActorId);
            if (!settlementUnproven && !actorUnproven) continue;
            if (_provenTurn != turn && Compare(prev))   // the world of this very turn may still match
            {
                i--;   // the bounds moved: judge the order again
                continue;
            }
            throw new OrderValidationException(Refusal(order, prev, settlementUnproven ? settlement : -1));
        }
    }

    /// <summary>Compares <paramref name="world"/> with the trace's row for its turn, until the first turn found to differ
    /// (<see cref="BeforeDelivery"/> calls it; a runner calls it once more for the replay's last world, which no batch is
    /// delivered to). Reads only.</summary>
    public void Observe(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_firstDifference < 0 && _provenTurn != world.Clock.Turn) Compare(world);
    }

    /// <summary>Compares <paramref name="world"/> with the trace's row for its turn: on a match the bounds become that
    /// world's; on a mismatch the first difference is recorded. True on a match.</summary>
    private bool Compare(WorldState world)
    {
        long turn = world.Clock.Turn;
        int at = Array.BinarySearch(_turns, turn);
        if (at < 0) return false;   // no row for this turn: nothing shown either way
        if (!Matches(world, _hashes[at]))
        {
            if (_firstDifference < 0) _firstDifference = turn;
            return false;
        }
        _provenTurn = turn;
        for (int s = 0; s < world.Settlements.Count; s++)
            if (world.Settlements[s].Id.Value > _settlementMax) _settlementMax = world.Settlements[s].Id.Value;
        for (int p = 0; p < world.Polities.Count; p++)
            if (world.Polities[p].Id.Value > _polityMax) _polityMax = world.Polities[p].Id.Value;
        return true;
    }

    /// <summary><see cref="ReplayGuard.MatchesRecorded"/>, the recorded schema's form tried first (the one its trace
    /// holds; the others cover a trace a later build rewrote).</summary>
    private bool Matches(WorldState world, string hash)
    {
        if (CanonicalSchema.IsReadable(_recordedSchema)
            && string.Equals(WorldHash.ComputeHexAsSchema(world, _recordedSchema), hash, StringComparison.Ordinal))
            return true;
        return ReplayGuard.MatchesRecorded(world, hash);
    }

    private static bool Holds(WorldState world, int settlement, out long foundedTurn)
    {
        for (int s = 0; s < world.Settlements.Count; s++)
            if (world.Settlements[s].Id.Value == settlement)
            {
                foundedTurn = world.Settlements[s].FoundedTurn;
                return true;
            }
        foundedTurn = -1;
        return false;
    }

    private static bool HoldsPolity(WorldState world, int polity)
    {
        for (int p = 0; p < world.Polities.Count; p++)
            if (world.Polities[p].Id.Value == polity) return true;
        return false;
    }

    private string Refusal(OrderRecord order, WorldState prev, int settlement)
    {
        long turn = prev.Clock.Turn;
        string what;
        if (settlement >= 0)
        {
            Holds(prev, settlement, out long founded);
            what = $"{_describedAs}: the orders delivered on turn {Inv(turn)} include {order.Kind} (issued by polity " +
                $"{Inv(order.ActorId)}) for settlement {Inv(settlement)}, founded at turn {Inv(founded)} in this replay. A " +
                $"settlement's id is the next free one when it is founded, so id {Inv(settlement)} names the settlement the " +
                "order was given for only if this replay is the recorded game up to that founding.";
        }
        else
        {
            what = $"{_describedAs}: the orders delivered on turn {Inv(turn)} include {order.Kind} issued by polity " +
                $"{Inv(order.ActorId)}, which a revolt founded (the founding gave polities 1 to {Inv(_rosterMax)}). A " +
                $"polity's id is the next free one when it is founded, so id {Inv(order.ActorId)} names the polity that " +
                "issued the order only if this replay is the recorded game up to that revolt.";
        }
        string evidence;
        if (_trace is null)
            evidence = "The session has no trace to show that with,";
        else if (_provenTurn < 0)
            evidence = _firstDifference == 0
                ? "This replay differs from the recorded game from its founding on (turn 0 does not match the saved trace),"
                : "The saved trace does not show that,";
        else if (_firstDifference >= 0)
            evidence = $"This replay matches the recorded game (its saved trace) through turn {Inv(_provenTurn)} and differs " +
                $"from turn {Inv(_firstDifference)} on,";
        else
            evidence = $"The saved trace shows this replay is the recorded game only through turn {Inv(_provenTurn)},";
        return $"{what} {evidence} so the order could land on a different " +
            (settlement >= 0 ? "settlement" : "polity") + " and the session is refused rather than the order applied. " + _remedy;
    }

    private static string Inv(long value) => value.ToString(CultureInfo.InvariantCulture);
}
