using System.Globalization;
using Sim.Core.Kernel;

namespace Sim.Cli;

/// <summary>
/// Fix round 1 (D-054, verifier F1) — what `sim inspect` says instead of a hash verdict for a session recorded under
/// another canonical schema.
/// </summary>
public static class SessionSchemaNotice
{
    /// <summary>
    /// Final nits (D-054, verifier RC2-1, 2026-10-15) — THE ONE CONDITION under which `sim inspect` compares a trace's
    /// hashes with its replay's: the session was recorded under THIS build's canonical schema. A hash covers the canonical
    /// stream of the schema that wrote it, so under any other schema — older or newer — none can equal a replayed one.
    /// The platform block (PlatformNotice.For), the recipe line (<see cref="Reproduce"/>) and the verdict all read it.
    /// </summary>
    public static bool HashesChecked(int recordedSchema) => recordedSchema == CanonicalSchema.Version;

    /// <summary>
    /// D-057 — the verdict line for a session recorded under ANOTHER READABLE schema (<see cref="CanonicalSchema.IsReadable"/>,
    /// not this build's): `sim inspect` hashes each replayed world as that schema's build hashed it
    /// (<see cref="WorldHash.ComputeHexAsSchema"/>, through <see cref="ReplayProof"/>) and compares it with the trace, up to
    /// the first turn that differs. <paramref name="provenTurn"/> is the last turn found equal (−1: none);
    /// <paramref name="firstDifference"/> the first found to differ (−1: none); <paramref name="lastTurn"/> the trace's last.
    /// It replaces <see cref="NotChecked"/>'s "no replayed hash can equal a recorded one", which stopped being true when
    /// D-056 gave this build the older schemas' hash forms.
    /// </summary>
    public static string CheckedInRecordedForm(int recorded, long provenTurn, long firstDifference, long lastTurn)
    {
        string r = recorded.ToString(CultureInfo.InvariantCulture);
        string verdict = firstDifference < 0
            ? (provenTurn == lastTurn
                ? "the replay reproduces the recorded game through turn " + Inv(lastTurn)
                : "the replay matches the recorded game through turn " + Inv(provenTurn) + " (the trace holds no row after it)")
            : firstDifference == 0
                ? "the replay differs from the recorded game from its founding (turn 0) on"
                : "the replay matches the recorded game through turn " + Inv(provenTurn) + " and differs from turn "
                  + Inv(firstDifference) + " on (another build's rules, data or platform: D-008 replays an older session, "
                  + "it does not promise to reproduce it)";
        return "trace checked in the v" + r + " form (D-057): the session was recorded under save schema v" + r
            + " and this build is v" + CanonicalSchema.Version.ToString(CultureInfo.InvariantCulture)
            + ", so each replayed turn is hashed as a v" + r + " build hashed it; " + verdict
            + ". Replayed from its seed and orders"
            + (TopologyFlags.RecordedFlat(recorded) ? " on the flat map v" + r + " used" : "")
            + "; the rows below are this replay's.";
    }

    private static string Inv(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The verdict line for a session whose manifest records schema <paramref name="recorded"/>, when this build
    /// cannot hash a world in that schema's form (a schema outside the readable set: a newer build's, or one older than
    /// v32). For another readable schema the verdict is <see cref="CheckedInRecordedForm"/>.</summary>
    public static string NotChecked(int recorded) =>
        "trace not checked: the session was recorded under save schema v" + recorded.ToString(CultureInfo.InvariantCulture)
        + " and this build is v" + CanonicalSchema.Version.ToString(CultureInfo.InvariantCulture)
        + ", so no replayed hash can equal a recorded one (D-008). Replayed from its seed and orders"
        + (TopologyFlags.RecordedFlat(recorded)
            ? " on the flat map v" + recorded.ToString(CultureInfo.InvariantCulture) + " used"
            : "")
        + "; the rows below are this replay's.";

    /// <summary>
    /// Fix round 2 (D-054, verifier RC1-1, 2026-10-15) — `sim inspect`'s "reproduce with:" line for the session
    /// <paramref name="manifest"/> records (<paramref name="manifestFile"/>: the manifest's file name, as the session
    /// line prints it), replayed to <paramref name="turns"/>.
    ///
    /// For a session this build can found as it was founded, the line is the <see cref="SessionManifest.ReplayCommand"/>
    /// recipe, as before. For a session recorded before the cylinder (<see cref="TopologyFlags.RecordedFlat"/>) that
    /// recipe does not reproduce it here: this build's `sim replay --founded` founds the shipped cylinder, not the flat
    /// map the session was played on. MEASURED on fix round 1's tree with Fixtures/v32-session-found: the printed recipe
    /// exited 2, "error: order (turn 5): FoundSettlement targets settlement 12, which does not exist when the order is
    /// delivered (turn 5, 12 settlement(s)). …"; for a 250 px batch-2 session it was refused by the cylinder's size rule
    /// ("… the nearest sizes the cylinder takes are 248 px and 252 px."). The same recipes on the batch-2 build exited 0
    /// with the recorded hashes. So the line names `sim inspect` — which rebuilds such a session flat — and gives the
    /// recipe as the recording build's.
    ///
    /// Final nits (verifier RC2-1, 2026-10-15): the this-build branch is keyed on <see cref="HashesChecked"/> — the
    /// condition `sim inspect`'s platform block and verdict use — not on RecordedFlat. MEASURED on fix round 2's tree: a
    /// session whose manifest says v34 got "  reproduce with: sim replay --founded --seed 42 --orders
    /// orders-7d8bfccc29cce001.bin --turns 3" above "trace not checked: the session was recorded under save schema v34 …".
    /// A session recorded under a NEWER schema is now given only the recording build's recipe, and this build's
    /// `sim replay` is disowned. `sim inspect` prints no recipe for an empty trace (RC2-2) and does not call this then.
    /// </summary>
    public static string Reproduce(SessionManifest manifest, string manifestFile, long turns)
    {
        if (HashesChecked(manifest.SchemaVersion))
            return "  reproduce with: " + manifest.ReplayCommand(turns);

        string recorded = manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture);
        string current = CanonicalSchema.Version.ToString(CultureInfo.InvariantCulture);
        if (!TopologyFlags.RecordedFlat(manifest.SchemaVersion))
            return "  reproduce with: the build that recorded the session (" + manifest.BuildSha + ", " + manifest.BuildDate
                + "): " + manifest.ReplayCommand(turns) + "\n"
                + "          not with this build's sim replay: this build is schema v" + current
                + " and the session was recorded under v" + recorded + ", so no replay here reproduces its hashes (D-008).";

        return "  reproduce with: sim inspect --manifest " + manifestFile
            + " (it rebuilds a v" + recorded + " session on the flat map v" + recorded + " used)\n"
            + "          not with this build's sim replay: it founds the cylinder this build ships (schema v"
            + CanonicalSchema.Version.ToString(CultureInfo.InvariantCulture) + "), not that flat map.\n"
            + "          The build that recorded the session (" + manifest.BuildSha + ", " + manifest.BuildDate
            + ") replays it with: " + manifest.ReplayCommand(turns);
    }
}
