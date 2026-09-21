using System.Text.Json;
using Sim.Core.Kernel;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Kernel;

/// <summary>
/// M4 closure, blocker B5 — THE CHECKER, CHECKED.
///
/// The defect these tests close is not a simulation defect. It is that the nightly
/// sweep PRINTED a quarantined corridor's measured range beside its recorded window
/// and never COMPARED them, so a corridor drifting clean out of its own window printed
/// exactly like one sitting inside it, and the gate short-circuited on
/// `quarantine.active` before anything looked. The battery could not see it either:
/// its canonical theory runs seeds 1 and 2 (CalibrationBatteryTests) and both are
/// comfortably inside the window, while the drift is at seed 3.
///
/// So the comparison moved out of an untested `jq` expression in `ci.yml` and into
/// <see cref="CorridorStatus"/>, and this file is the teeth on it. T3.12's own lesson,
/// applied one level up: nothing checked the checker.
///
/// NOTHING HERE MOVES A BAND, A WINDOW, A QUARANTINE FLAG OR ANY SIMULATION BEHAVIOUR.
/// The B5 numbers below are the MEASURED ones from
/// docs/design/density-window-verification.md (verdict CONFIRMED) and are used as the
/// fixture the classifier must be able to see; the corridor file itself is asserted
/// UNCHANGED by <see cref="TheShippedDensityQuarantine_StillDeclaresTheWindowTheVerifierMeasured"/>.
/// </summary>
public class CorridorStatusTests
{
    // The recorded window of canonical.densityPerArableKm2, at full measured precision,
    // exactly as corridors.json carries it.
    private const double WindowLo = 0.3685744951368359;
    private const double WindowHi = 0.7421101248166698;

    // The B5 measurement: seed 3 on the T4.21 candidate, 3.9117757 % below the floor.
    private const double B5Seed3 = 0.35415668759623087;

    // The density TARGET band, which is not this test's business to move.
    private const double BandLo = 0.15;
    private const double BandHi = 0.6;

    [Fact]
    public void B5_AQuarantinedCorridorOutsideItsWindow_IsSeen()
    {
        // The whole point. Before this classifier existed, this case was indistinguishable
        // from a healthy quarantine at every shipped instrument.
        CorridorVerdict v = CorridorStatus.Classify(
            measuredLo: B5Seed3, measuredHi: WindowHi,
            bandLo: BandLo, bandHi: BandHi,
            quarantineActive: true, windowLo: WindowLo, windowHi: WindowHi);

        Assert.Equal(CorridorVerdict.QuarantinedOutsideWindow, v);
        Assert.True(CorridorStatus.NeedsReading(v), "a drifted quarantine must be read");
    }

    [Fact]
    public void B5_TheDriftIsReportedAtItsMeasuredSize()
    {
        double overshoot = CorridorStatus.WindowOvershootFraction(
            B5Seed3, WindowHi, WindowLo, WindowHi);

        // 3.9117757 % below the floor — the same figure the verification lane measured,
        // and the same figure as the population move, to the digits both share.
        Assert.Equal(0.039117757009345686, overshoot, 12);
    }

    [Fact]
    public void AQuarantinedCorridorInsideItsWindow_IsNotFlagged()
    {
        // The anti-vacuity arm: if this also reported a breach, the test above would be
        // proving nothing but that the classifier always complains.
        CorridorVerdict v = CorridorStatus.Classify(
            measuredLo: WindowLo, measuredHi: WindowHi,
            bandLo: BandLo, bandHi: BandHi,
            quarantineActive: true, windowLo: WindowLo, windowHi: WindowHi);

        Assert.Equal(CorridorVerdict.QuarantinedInsideWindow, v);
        Assert.False(CorridorStatus.NeedsReading(v));
        Assert.Equal(0.0, CorridorStatus.WindowOvershootFraction(WindowLo, WindowHi, WindowLo, WindowHi));
    }

    [Fact]
    public void T3_12_MECHANISM_AQuarantinedCorridorNeverGates_EvenWhenItHasDrifted()
    {
        // T3.12 ruled that a quarantined corridor is REPORTED with its measured range
        // rather than gating. Closing the blind spot must not quietly convert the
        // quarantine back into a gate — that would re-fight a settled ruling by accident.
        foreach (CorridorVerdict v in new[]
                 {
                     CorridorVerdict.QuarantinedInsideWindow,
                     CorridorVerdict.QuarantinedOutsideWindow,
                     CorridorVerdict.QuarantinedWindowUnknown,
                 })
        {
            Assert.False(CorridorStatus.Gates(v), $"{v} must report, not gate");
        }

        // And the one verdict that DOES gate still does, or the sweep has no teeth at all.
        Assert.True(CorridorStatus.Gates(CorridorVerdict.OutOfBand));
        Assert.False(CorridorStatus.Gates(CorridorVerdict.InBand));
    }

    [Fact]
    public void ANonQuarantinedCorridorOutOfBand_StillGates()
    {
        CorridorVerdict v = CorridorStatus.Classify(
            measuredLo: 0.10, measuredHi: 0.80,
            bandLo: BandLo, bandHi: BandHi,
            quarantineActive: false, windowLo: double.NaN, windowHi: double.NaN);

        Assert.Equal(CorridorVerdict.OutOfBand, v);
        Assert.True(CorridorStatus.Gates(v));
    }

    [Fact]
    public void ANonQuarantinedCorridorInBand_Passes()
    {
        CorridorVerdict v = CorridorStatus.Classify(
            measuredLo: 0.20, measuredHi: 0.55,
            bandLo: BandLo, bandHi: BandHi,
            quarantineActive: false, windowLo: double.NaN, windowHi: double.NaN);

        Assert.Equal(CorridorVerdict.InBand, v);
        Assert.False(CorridorStatus.Gates(v));
    }

    [Fact]
    public void AQuarantineWithNoUsableWindow_IsItselfAFinding()
    {
        // An unjudgeable quarantine is the silence T3.12 closed, re-opened by an
        // incomplete record. It must not read as "fine".
        CorridorVerdict v = CorridorStatus.Classify(
            measuredLo: 0.1, measuredHi: 0.9,
            bandLo: BandLo, bandHi: BandHi,
            quarantineActive: true, windowLo: double.NaN, windowHi: double.NaN);

        Assert.Equal(CorridorVerdict.QuarantinedWindowUnknown, v);
        Assert.True(CorridorStatus.NeedsReading(v));
        Assert.False(CorridorStatus.Gates(v));
        Assert.True(double.IsNaN(
            CorridorStatus.WindowOvershootFraction(0.1, 0.9, double.NaN, double.NaN)));
    }

    [Fact]
    public void TheBoundaryIsInclusive_SoASeedExactlyOnTheWindowEdgeIsInside()
    {
        // The window is stated as the MEASURED ENVELOPE at full precision, with the
        // extremes deliberately NOT rounded outward (corridors.json says so in terms).
        // An exclusive comparison would therefore report the very seeds that DEFINE the
        // window as outside it.
        Assert.Equal(
            CorridorVerdict.QuarantinedInsideWindow,
            CorridorStatus.Classify(WindowLo, WindowLo, BandLo, BandHi, true, WindowLo, WindowHi));
        Assert.Equal(
            CorridorVerdict.QuarantinedInsideWindow,
            CorridorStatus.Classify(WindowHi, WindowHi, BandLo, BandHi, true, WindowLo, WindowHi));
    }

    [Fact]
    public void ADriftAboveTheCeiling_IsSeenToo_NotJustBelowTheFloor()
    {
        // corridors.json states the battery keeps teeth "in BOTH directions against
        // 'window'". A one-sided check would pass the mirror image of B5 in silence.
        CorridorVerdict v = CorridorStatus.Classify(
            measuredLo: WindowLo, measuredHi: WindowHi * 1.05,
            bandLo: BandLo, bandHi: BandHi,
            quarantineActive: true, windowLo: WindowLo, windowHi: WindowHi);

        Assert.Equal(CorridorVerdict.QuarantinedOutsideWindow, v);
        Assert.Equal(0.05, CorridorStatus.WindowOvershootFraction(
            WindowLo, WindowHi * 1.05, WindowLo, WindowHi), 12);
    }

    [Fact]
    public void TheShippedDensityQuarantine_StillDeclaresTheWindowTheVerifierMeasured()
    {
        // GUARD, not a re-pin. If someone re-pins the window to absorb the B5 drift, the
        // literals above stop describing the shipped file and this fails by name — which
        // is the point: re-deriving a corridor is a director ruling (the quarantine's own
        // liftCondition says so), and CR-002/CR-003 forbid fitting the instrument to the
        // artifact. This test does not care what the right answer is. It cares that the
        // answer is not changed quietly.
        using Stream s = Sim.Data.DataFiles.OpenCorridors();
        using JsonDocument doc = JsonDocument.Parse(s);
        JsonElement q = doc.RootElement
            .GetProperty("canonical").GetProperty("densityPerArableKm2").GetProperty("quarantine");

        Assert.True(q.GetProperty("active").GetBoolean(), "the density quarantine is still active");
        JsonElement w = q.GetProperty("window");
        Assert.Equal(WindowLo, w[0].GetDouble());
        Assert.Equal(WindowHi, w[1].GetDouble());

        // And the TARGET band is untouched, which is the thing CR-002/CR-003 actually fence.
        JsonElement band = doc.RootElement
            .GetProperty("canonical").GetProperty("densityPerArableKm2").GetProperty("band");
        Assert.Equal(BandLo, band[0].GetDouble());
        Assert.Equal(BandHi, band[1].GetDouble());
    }

    [Fact]
    public void EveryQuarantinedCorridorInTheShippedFile_DeclaresAUsableWindow()
    {
        // Sweeps the file rather than naming corridors, so a corridor quarantined later
        // without a window cannot slip in un-judgeable.
        using Stream s = Sim.Data.DataFiles.OpenCorridors();
        using JsonDocument doc = JsonDocument.Parse(s);

        int checkedCount = 0;
        foreach (JsonProperty group in doc.RootElement.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (JsonProperty corridor in group.Value.EnumerateObject())
            {
                if (corridor.Value.ValueKind != JsonValueKind.Object) continue;
                if (!corridor.Value.TryGetProperty("quarantine", out JsonElement q)) continue;
                if (!q.TryGetProperty("active", out JsonElement a) || !a.GetBoolean()) continue;

                checkedCount++;
                string name = $"{group.Name}.{corridor.Name}";
                Assert.True(q.TryGetProperty("window", out JsonElement w),
                    $"{name} is quarantined with no window — unjudgeable");
                Assert.Equal(2, w.GetArrayLength());
                Assert.True(w[1].GetDouble() >= w[0].GetDouble(), $"{name} window is inverted");
            }
        }

        Assert.True(checkedCount > 0, "no active quarantine found — this sweep is vacuous");
    }
}
