namespace Sim.Core.Kernel;

/// <summary>What a corridor's measured envelope says about that corridor.</summary>
public enum CorridorVerdict
{
    /// <summary>Not quarantined, and the whole measured envelope sits inside the band.</summary>
    InBand = 0,

    /// <summary>Not quarantined, and some seed left the band. THIS is the only gating verdict.</summary>
    OutOfBand = 1,

    /// <summary>Quarantined, and the measured envelope still sits inside the recorded window —
    /// the deviation is behaving exactly as the quarantine recorded it.</summary>
    QuarantinedInsideWindow = 2,

    /// <summary>Quarantined, and the measured envelope has LEFT the recorded window. Reported
    /// loudly; does not gate (T3.12 — a quarantined corridor reports, it does not gate).</summary>
    QuarantinedOutsideWindow = 3,

    /// <summary>Quarantined with no usable window recorded — the drift cannot be judged at all.
    /// Reported as loudly as a breach, because an unjudgeable quarantine is the silence
    /// T3.12 exists to prevent.</summary>
    QuarantinedWindowUnknown = 4,
}

/// <summary>
/// M4 closure — THE CHECKER, CHECKED. One definition of "what does this corridor's
/// measured envelope mean", shared by the nightly sweep and by the tests, so the two
/// instruments cannot diverge in judgement the way they diverged in T3.12.
///
/// WHY THIS TYPE EXISTS. T3.12 closed one half of a real defect: the nightly read only
/// the BAND, so a corridor the project had deliberately quarantined gated anyway, and the
/// nightly was red for eleven consecutive runs while nobody read it. The fix taught the
/// nightly about `quarantine.active` and had it PRINT the measured range beside the
/// recorded window. It did not teach it to COMPARE them. The status line therefore prints
/// two intervals side by side and says nothing about the relationship between them, so a
/// quarantined corridor drifting clean out of its own recorded window prints exactly like
/// one sitting comfortably inside it. That residual is what the M4 closure audit's B5 is:
/// `canonical.densityPerArableKm2` at seed 3 measures 0.35415668759623087 against a
/// recorded window floor of 0.3685744951368359 — 3.91 % below — and no shipped instrument
/// can see it. The battery cannot either: its canonical theory runs seeds 1 and 2, and
/// both are comfortably inside.
///
/// WHAT THE DATA FILE ALREADY PROMISED. This is not a new requirement invented here.
/// `corridors.json` states it twice in its own words: *"Both the nightly sweep and
/// CalibrationBatteryTests read THIS field, so the two instruments cannot disagree again"*
/// and *"the battery keeps its per-seed teeth in BOTH directions against 'window'"*. The
/// comparison below is that promise, executed.
///
/// WHAT THIS DELIBERATELY DOES NOT DO. It moves no band, no window, no quarantine flag and
/// no threshold, and it changes no simulation behaviour — CR-002 and CR-003 both forbid
/// fitting the instrument to the artifact, and a corridor's disposition stays the
/// director's. It also does not make a quarantined corridor gate: T3.12's mechanism is that
/// such a corridor is REPORTED with its measured range rather than gating, and
/// <see cref="Gates"/> keeps that exactly. The change is from silence to a statement.
///
/// PURE AND DETERMINISTIC: no I/O, no clock, no randomness, no collection iteration order —
/// the caller supplies the numbers and the verdict is a total function of them.
/// </summary>
public static class CorridorStatus
{
    /// <summary>
    /// Classify one corridor from its measured envelope.
    /// </summary>
    /// <param name="measuredLo">Minimum across the swept seeds.</param>
    /// <param name="measuredHi">Maximum across the swept seeds.</param>
    /// <param name="bandLo">The TARGET band's floor.</param>
    /// <param name="bandHi">The TARGET band's ceiling.</param>
    /// <param name="quarantineActive">Whether the corridor declares an ACTIVE quarantine.</param>
    /// <param name="windowLo">The quarantine's recorded envelope floor; NaN when absent.</param>
    /// <param name="windowHi">The quarantine's recorded envelope ceiling; NaN when absent.</param>
    public static CorridorVerdict Classify(
        double measuredLo, double measuredHi,
        double bandLo, double bandHi,
        bool quarantineActive, double windowLo, double windowHi)
    {
        if (!quarantineActive)
        {
            return measuredLo >= bandLo && measuredHi <= bandHi
                ? CorridorVerdict.InBand
                : CorridorVerdict.OutOfBand;
        }

        // A quarantine with no usable window cannot be judged. That is itself a finding:
        // it is the silence T3.12 closed, re-opened by an incomplete quarantine record.
        if (double.IsNaN(windowLo) || double.IsNaN(windowHi) || !(windowHi >= windowLo))
            return CorridorVerdict.QuarantinedWindowUnknown;

        return measuredLo >= windowLo && measuredHi <= windowHi
            ? CorridorVerdict.QuarantinedInsideWindow
            : CorridorVerdict.QuarantinedOutsideWindow;
    }

    /// <summary>
    /// Does this verdict fail the sweep? ONLY <see cref="CorridorVerdict.OutOfBand"/> does.
    /// A quarantined corridor reports and does not gate, whatever its window says — that is
    /// T3.12's mechanism and this method is where it is enforced rather than remembered.
    /// </summary>
    public static bool Gates(CorridorVerdict verdict) => verdict == CorridorVerdict.OutOfBand;

    /// <summary>
    /// Does this verdict need a human to read it? Every quarantined verdict except
    /// "inside its window" does. This is the predicate the nightly prints on.
    /// </summary>
    public static bool NeedsReading(CorridorVerdict verdict) =>
        verdict == CorridorVerdict.QuarantinedOutsideWindow
        || verdict == CorridorVerdict.QuarantinedWindowUnknown;

    /// <summary>The fixed report word for a verdict. Ordinal, culture-free, stable.</summary>
    public static string Word(CorridorVerdict verdict) => verdict switch
    {
        CorridorVerdict.InBand => "GATED",
        CorridorVerdict.OutOfBand => "BREACH",
        CorridorVerdict.QuarantinedInsideWindow => "QUARANTINED",
        CorridorVerdict.QuarantinedOutsideWindow => "WINDOW BREACH",
        CorridorVerdict.QuarantinedWindowUnknown => "WINDOW UNKNOWN",
        _ => "UNCLASSIFIED",
    };

    /// <summary>
    /// The signed fraction by which the measured envelope overshoots the recorded window,
    /// as a positive number, or 0.0 when it does not. Reported so the drift's SIZE is on the
    /// run log rather than only its existence: B5 is 3.91 % and a reader needs to know that
    /// rather than re-deriving it. Returns NaN when there is no window to measure against.
    /// </summary>
    public static double WindowOvershootFraction(
        double measuredLo, double measuredHi, double windowLo, double windowHi)
    {
        if (double.IsNaN(windowLo) || double.IsNaN(windowHi) || !(windowHi >= windowLo))
            return double.NaN;

        double below = windowLo == 0.0 ? 0.0 : (windowLo - measuredLo) / Math.Abs(windowLo);
        double above = windowHi == 0.0 ? 0.0 : (measuredHi - windowHi) / Math.Abs(windowHi);
        double worst = Math.Max(below, above);
        return worst > 0.0 ? worst : 0.0;
    }
}
