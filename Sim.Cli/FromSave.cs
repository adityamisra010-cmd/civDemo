using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Worldgen;

namespace Sim.Cli;

/// <summary>
/// D-055 / ADR-040 (batch 4, item 3) — `sim run --from-save SAVE`: CONTINUE A SAVED WORLD IN A NEW PROCESS. Named so as not
/// to collide with Sim.Ui's `--resume`, which replays a session record; this loads the snapshot itself and steps it, so a
/// save written by one process is the starting state of the next (the cross-process allocation leg, design §3).
/// <list type="bullet">
/// <item>The save is loaded by the CLI's one loader (<see cref="SaveFiles.Load(string, long, out string?)"/>): the terrain
///   regenerated and checked against the save's own hash; a schema-v32 or v33 save MIGRATED, with its one-line note on
///   STDERR. A save made at a non-canonical map size needs `--size PX` — without it the regenerated terrain does not match
///   and the load is refused, with that hint.</item>
/// <item>`--turns N` is N MORE steps; `--save-at K'` must lie in (K, K + N], an ABSOLUTE turn, K the save's turn.</item>
/// <item>The seed, and whether the world is founded, are the save's: `--seed` and `--founded` are refused when they
///   contradict it. `--settlements` and `--emit-session` are refused (the save holds its settlements; a session record
///   starts at founding, so a continued run writes none). `--ai-empires`, if given, must equal the FOUNDING AI Empire
///   count, as in a run from founding (T3 round 3, finding T3-L2-T3V2-L2-F3): AI polities a revolt founded later are not
///   counted, and the refusal says so.</item>
/// <item>TURN ZERO — the world ADR-034's order validation reads (the up-front check and the deferral maxima at delivery) — is
///   the REGENERATED FOUNDING WORLD (<see cref="Cli.StartWorld"/> from the save's seed, map size, topology, founding
///   settlement count — its settlements founded at turn 0 — and FOUNDING AI Empire count, <see cref="FoundingAiEmpires"/>),
///   so validation is exactly the uninterrupted run's. The regenerated world is CHECKED against the save (its settlements,
///   roster prefix and capitals — capitals are written only at founding); a save it does not match is refused rather than
///   validated against the wrong world. A save in which a revolt founded an AI polity before K continues (round 3: the
///   revolt-born polity is not a founding Empire — it holds no capital — and follows the founding roster), except where
///   founding may have left an Empire without a capital (more Empires than founding settlements), where the founding count
///   cannot be told from the save and such a save is still refused. A toy save's turn zero is its genesis.</item>
/// <item>`--orders` must be a PLAYER input log: its records stamped before K are dropped unread (they were delivered
///   before the save, or never), and every record stamped AT OR AFTER K must be issued by the save's player polity — a log
///   with any later record by another polity (an emitted run log whose AI orders reach K) is REFUSED, because the
///   continuation produces the AI's orders itself (it would deliver them twice). An emitted run log whose AI records all
///   precede K is accepted: those records are dropped with the rest.</item>
/// <item>The AI's worldgen comes from <see cref="SaveFiles.WorldgenFor"/> (the save's version and topology trailer, and
///   `--size`); the AI producer and the pipeline are otherwise exactly the uninterrupted run's.</item>
/// </list>
/// </summary>
internal static class FromSave
{
    /// <summary>Snapshot.Save's header: magic 8 | version 4 (SaveFiles' header layout).</summary>
    private const int HeaderVersionOffset = 8;

    internal static int Run(Options opts, string path)
    {
        if (opts.Get("--emit-session") is not null)
            throw new CliUsageException(
                "--from-save writes no session record (a session record starts at founding): --emit-session is refused");
        if (opts.Get("--settlements") is not null)
            throw new CliUsageException("--from-save takes the settlements from the save: --settlements is refused");
        int turns = opts.Turns();
        long sizeArg = opts.LongOr("--size", -1);
        long saveAt = opts.LongOr("--save-at", -1);
        string? savePath = opts.Get("--save");
        if (saveAt >= 0 && savePath is null) throw new CliUsageException("--save-at requires --save PATH");
        if (savePath is not null && saveAt < 0) throw new CliUsageException("--save requires --save-at K");
        // D-056 (verifier finding 26): --save naming the save being continued would overwrite it — the only copy of the
        // world the run starts from — so it is refused before anything is read or written.
        if (savePath is not null && string.Equals(Path.GetFullPath(savePath), Path.GetFullPath(path), StringComparison.Ordinal))
            throw new CliUsageException(
                $"--save {savePath} is the save being continued (--from-save): it would be overwritten. Write the continued save to another path.");

        byte[] bytes = File.ReadAllBytes(path);
        WorldState world = Load(path, sizeArg);
        long k = world.Clock.Turn;
        ulong seed = world.Seed;
        bool founded = world.Terrain is not null;

        if (opts.Get("--seed") is not null && opts.Seed() != seed)
            throw new CliUsageException(
                $"--seed {opts.Seed()} contradicts the save, whose seed is {seed} (--from-save takes the seed from the save)");
        if (opts.Has("--founded") && !founded)
            throw new CliUsageException("--founded contradicts the save, which is of the M0 toy world (it has no terrain)");
        if (!founded && sizeArg >= 0) throw new CliUsageException("--size requires a founded save");
        int ai = FoundingAiEmpires(world);
        int revoltBorn = AiPolityCount(world) - ai;
        long aiArg = opts.LongOr("--ai-empires", -1);
        if (aiArg >= 0 && !founded) throw new CliUsageException("--ai-empires requires a founded save");
        if (aiArg >= 0 && aiArg != ai)
            throw new CliUsageException(
                $"--ai-empires {aiArg} contradicts the save, which was founded with {ai} AI Empire{(ai == 1 ? "" : "s")}" +
                (revoltBorn > 0
                    ? $" (--ai-empires is the founding count: the save's {revoltBorn} revolt-born AI polit{(revoltBorn == 1 ? "y is" : "ies are")} not counted)"
                    : ""));
        if (saveAt >= 0 && (saveAt <= k || saveAt > k + turns))
            throw new CliUsageException(
                $"--save-at must be in {k + 1}..{k + turns} (the save's turn {k} plus 1..{turns}), got {saveAt}");
        if (opts.Get("--split-report") is not null && !founded)
            throw new CliUsageException("--split-report requires a founded world (the save is of the M0 toy world)");

        OrderLog? loaded = opts.Get("--orders") is { } op ? PlayerOrdersFrom(Cli.LoadOrders(op), k, world) : null;

        // Turn zero: the regenerated founding world (founded) or the toy genesis, checked against the save.
        int? sizePx = sizeArg >= 0 ? (int)sizeArg : null;
        WorldState turnZero = founded
            ? Cli.StartWorld(seed, true, sizePx, FoundingSettlements(world), ai, world.Terrain!.WrapX)
            : Cli.StartWorld(seed, false);
        if (founded && FoundingMismatch(turnZero, world) is { } mismatch)
            throw new InvalidOperationException(
                $"--from-save cannot regenerate the founding world of {path} from its seed: {mismatch}. Turn zero is the " +
                "world ADR-034's order validation reads; a save whose founding cannot be regenerated (e.g. one founded with more " +
                "Empires than settlements in which a revolt then founded an AI polity) is refused rather than validated against a " +
                "different world.");
        if (loaded is not null) OrderValidation.ValidateAgainstWorld(loaded, turnZero);

        Sim.Core.Systems.SimConfig? aiCfg = founded ? Cli.SimCfg() : null;
        WorldgenConfig? aiWorldgen = founded ? SaveFiles.WorldgenFor(Version(bytes), CliRecipes.Worldgen(), sizeArg, Trailer(bytes)) : null;
        OrderLog? orders = aiCfg is not null ? new OrderLog() : loaded;
        var executor = Cli.Executor(orders, founded);

        var hashLog = opts.Get("--hash-log") is not null ? new List<string>(turns) : null;
        using Stream? report = opts.Get("--split-report") is { } rp ? File.Create(rp) : null;
        Sim.Core.Observability.SplitProbe? probe = report is not null ? new(report, Cli.SimCfg()) : null;

        world = Cli.Steps(turns, world, turnZero, executor, loaded, orders, aiCfg, aiWorldgen, hashLog, session: null, probe,
            saveAt, savePath);

        if (hashLog is not null) Cli.WriteHashLog(opts.Get("--hash-log")!, hashLog);
        Console.WriteLine(
            $"run complete: seed {seed}, {turns} turns from turn {k.ToString(CultureInfo.InvariantCulture)}, hash {WorldHash.ComputeHex(world)}");
        if (opts.Has("--report")) Cli.Report(world);
        return 0;
    }

    /// <summary>The save, through the CLI's one loader; a migrated save's note goes to STDERR. A terrain mismatch without
    /// `--size` gets the hint that a save made at a non-canonical size needs it.</summary>
    private static WorldState Load(string path, long sizeArg)
    {
        WorldState world;
        try
        {
            world = SaveFiles.Load(path, sizeArg, out string? note);
            if (note is not null) Console.Error.WriteLine(note);
        }
        catch (SnapshotFormatException e) when (sizeArg < 0 && e.Message.StartsWith("terrain mismatch", StringComparison.Ordinal))
        {
            throw new SnapshotFormatException(
                e.Message + " --from-save regenerates the terrain at the canonical map size; a save made at another size needs "
                + "--size PX (the size it was founded at).");
        }
        return world;
    }

    /// <summary>The records of <paramref name="log"/> stamped at or after the save's turn <paramref name="k"/>, in log order
    /// (earlier ones are dropped unread). In a founded save every kept record must be issued by the save's player polity: a
    /// record by any other actor means an emitted run log, whose AI orders the continuation would deliver twice.</summary>
    internal static OrderLog PlayerOrdersFrom(OrderLog log, long k, IReadOnlyWorldState save)
    {
        int player = -1;
        for (int p = 0; p < save.Polities.Count; p++)
            if (save.Polities[p].Source == CommandSource.Player) { player = save.Polities[p].Id.Value; break; }
        var kept = new OrderLog();
        for (int i = 0; i < log.Count; i++)
        {
            OrderRecord record = log[i];
            if (record.Turn < k) continue;
            if (save.Terrain is not null && save.Polities.Count > 0 && record.ActorId != player)
                throw new OrderValidationException(
                    $"--from-save takes a PLAYER input log: record {i} (turn {record.Turn}, {record.Kind}) is issued by polity " +
                    $"{record.ActorId}, not the player (polity {player}). An emitted run log carries the AI's orders, which the " +
                    "continued run produces itself — it would deliver them twice. Give the player's own orders.");
            kept.Append(record);
        }
        return kept;
    }

    /// <summary>
    /// THE FOUNDING AI EMPIRE COUNT of the save (T3 round 3, finding T3-L2-T3V2-L2-F3) — what `--ai-empires` was when the run
    /// was founded, which <see cref="AiPolityCount"/> overstates once a revolt has founded an AI polity. Capitals are written
    /// only at founding (WorldFounding: each Empire's first settlement) and never removed; polities are never removed; a
    /// revolt-born polity (RevoltSystem) gets no capital. Founding gives every Empire a capital exactly when the Empires
    /// (the player and the AI) number no more than the founding settlements. So when the AI polities holding a Capitals row
    /// number fewer than the founding settlements less the player's one, they ARE the founding AI Empires. Otherwise every
    /// founding settlement is a capital and founding may have left Empires with none, indistinguishable from revolt-born
    /// polities: the count is then the roster's AI polity count (exact when no revolt happened), and a save with both stays
    /// refused by the founding check.
    /// </summary>
    internal static int FoundingAiEmpires(IReadOnlyWorldState world)
    {
        int withCapital = 0;
        for (int p = 0; p < world.Polities.Count; p++)
        {
            if (world.Polities[p].Source != CommandSource.Ai) continue;
            for (int c = 0; c < world.Capitals.Count; c++)
                if (world.Capitals[c].Polity == world.Polities[p].Id) { withCapital++; break; }
        }
        return withCapital + 1 < FoundingSettlements(world) ? withCapital : AiPolityCount(world);
    }

    /// <summary>The number of AI-commanded polities on the save's roster.</summary>
    internal static int AiPolityCount(IReadOnlyWorldState world)
    {
        int n = 0;
        for (int p = 0; p < world.Polities.Count; p++)
            if (world.Polities[p].Source == CommandSource.Ai) n++;
        return n;
    }

    /// <summary>The number of the save's settlements founded at turn 0 (colonies carry the turn they were founded).</summary>
    internal static int FoundingSettlements(IReadOnlyWorldState world)
    {
        int n = 0;
        for (int s = 0; s < world.Settlements.Count; s++)
            if (world.Settlements[s].FoundedTurn == 0) n++;
        return n;
    }

    /// <summary>Why <paramref name="zero"/> (the regenerated founding world) is not the founding of <paramref name="save"/>, or
    /// null: every founding settlement must be in the save with its site, the founding roster must be the save's roster's
    /// prefix, and the capitals (written only at founding) must be the save's exactly.</summary>
    internal static string? FoundingMismatch(IReadOnlyWorldState zero, IReadOnlyWorldState save)
    {
        for (int s = 0; s < zero.Settlements.Count; s++)
        {
            SettlementRow founded = zero.Settlements[s];
            int at = -1;
            for (int i = 0; i < save.Settlements.Count; i++)
                if (save.Settlements[i].Id == founded.Id) { at = i; break; }
            if (at < 0 || save.Settlements[at].SiteCell != founded.SiteCell || save.Settlements[at].FoundedTurn != 0)
                return $"its founding settlement {founded.Id.Value} (cell {founded.SiteCell}) is not in the save as founded";
        }
        if (zero.Polities.Count > save.Polities.Count)
            return $"the founding roster has {zero.Polities.Count} polities, the save {save.Polities.Count}";
        for (int p = 0; p < zero.Polities.Count; p++)
            if (zero.Polities[p] != save.Polities[p])
                return $"founding polity {zero.Polities[p].Id.Value} ({zero.Polities[p].Source}) is not the save's roster row {p}";
        if (zero.Capitals.Count != save.Capitals.Count)
            return $"the founding world has {zero.Capitals.Count} capitals, the save {save.Capitals.Count}";
        for (int c = 0; c < zero.Capitals.Count; c++)
            if (zero.Capitals[c] != save.Capitals[c])
                return $"founding capital {c} (polity {zero.Capitals[c].Polity.Value} at settlement {zero.Capitals[c].Place.Value}) " +
                       "is not the save's";
        return null;
    }

    private static int Version(byte[] bytes) => BitConverter.ToInt32(bytes, HeaderVersionOffset);

    /// <summary>The topology trailer (the file's last int32) of a save from v33 on; none before (a v32 world is flat).</summary>
    private static int? Trailer(byte[] bytes) =>
        TopologyFlags.RecordedFlat(Version(bytes)) ? null : BitConverter.ToInt32(bytes, bytes.Length - 4);
}
