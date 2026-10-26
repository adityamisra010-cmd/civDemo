using System.Globalization;
using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>
/// D-056 — THE LEGACY-CANCEL GUARD, one mechanism for every runner that replays a recorded order log (UiSession.ReplayTo,
/// `sim inspect`, and — through Cli.LoadOrders — `sim run --orders`, `sim run --from-save --orders`, `sim replay`,
/// `sim research --orders`).
///
/// THE DEFECT IT CLOSES. A legacy cancel (<see cref="OrderKind.CancelConstruction"/>, kind 11) names its entry by
/// (settlement, slot) alone, and a slot is reused once its entry is gone (NextSlot = one past the highest slot queued).
/// Replayed into a world that has drifted from the recorded one — another build, another schema, another platform — the
/// same slot can hold a DIFFERENT project, and the cancel removed it without a word. Kind 13
/// (<see cref="OrderKind.CancelConstructionEntry"/>) carries the entry's identity and can no longer do that; every emitter
/// now issues it. Kind 11 orders already recorded are kept, but replayed only where this guard can prove the world they
/// are delivered to is the one they were recorded in.
///
/// THE RULE. Before a batch carrying a kind-11 order is delivered, the world it is delivered to (PREV, turn t) must equal
/// the session trace's row for turn t in some readable schema form (<see cref="MatchesRecorded"/>): every table that
/// schema serialized byte-identical, which includes the construction queue and progress the slot is resolved against. A
/// batch with no kind-11 order is not checked here (identity-carrying orders need no proof). The check runs BEFORE the
/// batch is appended or delivered, so a refusal mutates no world and the caller has written nothing.
/// A log with no trace — a bare log handed to the CLI, or a session whose trace file is missing — has nothing to prove
/// the world with, so a kind-11 order in it is refused (<see cref="RefuseUnverifiable"/>).
///
/// Every refusal is an <see cref="OrderValidationException"/>: the type every runner already reports (Cli.Guard: exit 2;
/// ResumeLaunch: exit 2 and the refusal dialog), with a message that says what was found, why it is not applied, that
/// nothing was changed, and what to do.
/// </summary>
public static class ReplayGuard
{
    /// <summary>The readable schemas, newest first: the forms <see cref="MatchesRecorded"/> tries.</summary>
    private static readonly int[] ReadableNewestFirst = [CanonicalSchema.Version, 36, 35, 34, 33, CanonicalSchema.OldestReadableVersion];

    /// <summary>The kind-11 orders in <paramref name="batch"/>.</summary>
    public static int LegacyCancels(OrderBatch batch)
    {
        int n = 0;
        for (int i = 0; i < batch.Count; i++) if (batch[i].Kind == OrderKind.CancelConstruction) n++;
        return n;
    }

    /// <summary>The kind-11 orders in <paramref name="log"/>.</summary>
    public static int LegacyCancels(OrderLog log)
    {
        int n = 0;
        for (int i = 0; i < log.Count; i++) if (log[i].Kind == OrderKind.CancelConstruction) n++;
        return n;
    }

    /// <summary>The first turn of <paramref name="log"/> carrying a kind-11 order; −1 when none does.</summary>
    public static long FirstLegacyCancelTurn(OrderLog log)
    {
        long first = -1;
        for (int i = 0; i < log.Count; i++)
            if (log[i].Kind == OrderKind.CancelConstruction && (first < 0 || log[i].Turn < first)) first = log[i].Turn;
        return first;
    }

    /// <summary>
    /// True when <paramref name="world"/> is the world <paramref name="recordedHash"/> was computed from, in any readable
    /// schema's form (<see cref="WorldHash.ComputeHexAsSchema"/>: this build's v34 hash, the v33 hash, the v32 hash). The
    /// trace a v33 build wrote matches the v33 form exactly when every table v33 serialized is identical — the queue and
    /// progress among them; a mismatch in every form means the replay is not the recorded world.
    /// </summary>
    public static bool MatchesRecorded(WorldState world, string recordedHash)
    {
        ArgumentNullException.ThrowIfNull(recordedHash);
        foreach (int version in ReadableNewestFirst)
            if (string.Equals(WorldHash.ComputeHexAsSchema(world, version), recordedHash, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>
    /// Refuses a log that carries kind-11 orders and no trace to verify them with (a bare CLI log; a session whose trace
    /// is missing). <paramref name="describedAs"/> names the log; <paramref name="remedy"/> is the last sentence (what
    /// to do). No-op when the log has no kind-11 order.
    /// </summary>
    public static void RefuseUnverifiable(OrderLog log, string describedAs, string remedy)
    {
        ArgumentNullException.ThrowIfNull(log);
        int n = LegacyCancels(log);
        if (n == 0) return;
        throw new OrderValidationException(
            $"{describedAs} holds {Count(n, "legacy slot-only cancel order")} (kind 11, CancelConstruction; the first on " +
            $"turn {Inv(FirstLegacyCancelTurn(log))}). A kind-11 cancel names its entry by slot alone, and a slot is reused " +
            "once its entry is gone, so replayed into any world but the one it was recorded in it can cancel a different " +
            "project. With no session trace there is no way to check that world, so the log is refused: nothing was " +
            $"replayed and no file was written. {remedy}");
    }

    /// <summary>
    /// THE CHECK, before <paramref name="batch"/> is delivered to <paramref name="prev"/> (its turn t = prev's turn): when
    /// the batch carries a kind-11 order, <paramref name="trace"/> must hold a row for turn t that
    /// <see cref="MatchesRecorded"/> accepts, or an <see cref="OrderValidationException"/> says why the session is refused.
    /// <paramref name="describedAs"/> names the session; <paramref name="recordedBy"/> the build that recorded it, when known.
    /// No-op for a batch with no kind-11 order. Reads only.
    /// </summary>
    public static void CheckBeforeDelivery(
        OrderBatch batch, WorldState prev, IReadOnlyList<SessionTrace.Row>? trace, string describedAs, string? recordedBy)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(prev);
        int n = LegacyCancels(batch);
        if (n == 0) return;
        long turn = prev.Clock.Turn;
        string what = $"{describedAs}: the orders delivered on turn {Inv(turn)} include {Count(n, "legacy slot-only cancel order")} " +
            "(kind 11, CancelConstruction), which name their entry by slot alone — a slot that holds the recorded project only " +
            "in the world the order was recorded in.";
        string remedy = "Nothing was applied and no session file was changed. To continue this session, open it with the build " +
            "that recorded it" + (string.IsNullOrEmpty(recordedBy) ? "" : $" ({recordedBy})") + "; to play this build, start a new game.";
        SessionTrace.Row? row = null;
        if (trace is not null)
            for (int r = 0; r < trace.Count; r++)
                if (trace[r].Turn == turn) { row = trace[r]; break; }
        if (row is null)
            throw new OrderValidationException(what + " " + (trace is null
                ? "The session has no trace to check that world against, so the cancel cannot be applied safely. "
                : $"The session's trace has no row for turn {Inv(turn)} to check that world against, so the cancel cannot be applied safely. ")
                + remedy);
        if (!MatchesRecorded(prev, row.Value.Hash))
            throw new OrderValidationException(what + $" Replaying the session here does not reproduce that world at turn " +
                $"{Inv(turn)} (replay hash {WorldHash.ComputeHex(prev)}, saved trace {row.Value.Hash}), so the cancel could " +
                "remove a different project and the session is refused. " + remedy);
    }

    private static string Count(int n, string noun) => Inv(n) + " " + noun + (n == 1 ? "" : "s");

    private static string Inv(long value) => value.ToString(CultureInfo.InvariantCulture);
}
