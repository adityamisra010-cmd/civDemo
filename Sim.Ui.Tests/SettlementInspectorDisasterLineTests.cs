using System;
using System.Collections.Generic;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// T4.21-6 — THE INSPECTOR MAY NOT CONTRADICT ITS OWN FOOD-STATE LINE.
///
/// <see cref="SettlementInspectorModel.FoodStateLines"/> prints the
/// classification and, beneath it, whether a disaster applied. Those two lines
/// are decided by the SAME fact — <c>FoodState.IsStruck</c>, i.e. prev
/// <c>DisasterRow.AppliedMultiplier &lt; 1</c> — so if the disaster line is
/// keyed on any other field the panel can say "FAMINE - a ruined harvest" and
/// "disaster: none applied to this harvest" one under the other. It did: the
/// line was keyed on <c>DisasterRow.Multiplier</c>, THIS step's forward-looking
/// factor, which on the dominant canonical shape (dt 10, durationYears 5, the
/// event already over) is exactly 1.0.
///
/// There was no test on this branch at all before T4.21-6, which is how it
/// shipped. These two cases are the two directions of the disagreement.
/// </summary>
public class SettlementInspectorDisasterLineTests
{
    /// <summary>The TAIL row — the ordinary canonical case. FAMINE by
    /// AppliedMultiplier 0.30, while this step's rates are untouched.</summary>
    [Fact]
    public void TheTailRow_ShowsTheDisasterThatCausedTheFamine()
    {
        FoodStateSection fs = RecordSections.Famine(
            FamineReason.Disaster, nominalDeficit: 0.4, severity: 0.0,
            appliedMultiplier: 0.30, multiplierThisStep: 1.0);

        IReadOnlyList<string> lines = SettlementInspectorModel.FoodStateLines(fs);

        Assert.Contains(lines, l => l.Contains("FAMINE", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("none applied", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("x 0.300", StringComparison.Ordinal));
        // ... and THIS step's factor is still shown, as its own clause, because
        // it is a different and also-true fact about the same row.
        Assert.Contains(lines, l => l.Contains("this turn's rates x 1.000", StringComparison.Ordinal));
    }

    /// <summary>The other direction: a row whose strike is still to come. THIS
    /// step's rates are cut, but the harvest that produced the observed deficit
    /// was not — so the state is not Famine and the panel must not claim the
    /// deficit was caused by a disaster.</summary>
    [Fact]
    public void TheOnsetRow_DoesNotClaimTheDeficitWasStruck()
    {
        FoodStateSection fs = RecordSections.Normal(nominalDeficit: 0.4) with
        {
            DisasterRowPresent = true,
            DisasterKind = 1,
            DisasterSeverity = 0.80,
            DisasterMultiplierThisStep = 0.20,
            DisasterAppliedMultiplier = 1.0,
        };

        IReadOnlyList<string> lines = SettlementInspectorModel.FoodStateLines(fs);

        Assert.DoesNotContain(lines, l => l.Contains("FAMINE", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("none applied to this harvest", StringComparison.Ordinal));
    }
}
