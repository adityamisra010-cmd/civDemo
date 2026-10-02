using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.Observability;
using Sim.Core.Observability.Explain;
using Sim.Core.Observability.Forensic;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.GovernanceRigs;

namespace Sim.Tests.Observability;

/// <summary>
/// ADR-033 D4 — GLASS BOX TRUTHFULNESS FOR THE GOVERNING LOOP. Once the tax multiplies happiness,
/// "happiness reads Food and Housing only" is false; these pin that the burden is an EXPLAINED cause
/// (the explanation and the settlement record carry it through one constructor, recomputed by the
/// simulation's own readers), that telemetry/v4 records it and reads it back exactly, and that the tax
/// enters the policy history as the second policy, attributed to the order that set it.
/// </summary>
public class TaxGlassBoxTests
{
    private static TurnExecutor Full(OrderLog orders)
    {
        using var era = Sim.Data.DataFiles.OpenEraPacing();
        using var pipe = Sim.Data.DataFiles.OpenPipeline();
        return new TurnExecutor(EraTableLoader.Load(era),
            PipelineLoader.Load(pipe, SystemCatalog.All(Cfg(), TestConfigs.DevWorldgen())), orders);
    }

    [Fact]
    public void TheHappinessExplanation_CarriesTheBurdenAsACause_AndTheScoreIsTheProvisionReadingTimesIt()
    {
        (WorldState w, PolityId p) = Founded();
        SettlementId seat = Seat(w, p);
        WellProvided(w, seat);

        HappinessExplanation untaxed = HappinessExplanation.For(w, Cfg(), seat);
        Assert.Equal(BitConverter.DoubleToInt64Bits(1.0), BitConverter.DoubleToInt64Bits(untaxed.Burden.Scale));
        Assert.Equal(p.Value, untaxed.Burden.Controller);
        Assert.False(untaxed.Burden.PolicyRowPresent);
        Assert.Equal(0.0, untaxed.Burden.EffectiveRate);

        w.TaxPolicies.Add(new TaxPolicyRow(p, 0.6));
        HappinessExplanation taxed = HappinessExplanation.For(w, Cfg(), seat);
        Assert.Equal(TaxBurdenReading.Of(w, Cfg(), seat), taxed.Burden);
        Assert.True(taxed.Burden.PolicyRowPresent);
        Assert.Equal(0.6, taxed.Burden.NominalRate);
        Assert.Equal(1.0, taxed.Burden.ControlStrength);   // the capital
        Assert.Equal(Governance.EffectiveTaxRate(w, seat, Cfg()), taxed.Burden.EffectiveRate);
        Assert.Equal(SettlementHappiness.TaxSufficiency(w, seat, Cfg()), taxed.Burden.Scale);
        Assert.Equal(SettlementHappiness.Of(w, seat, Cfg()), taxed.Happiness);
        // The two provision factors did not move; the score did, by exactly the burden.
        Assert.Equal(untaxed.Factors[0].Value, taxed.Factors[0].Value);
        Assert.Equal(untaxed.Factors[1].Value, taxed.Factors[1].Value);
        Assert.Equal(untaxed.Happiness * taxed.Burden.Scale, taxed.Happiness, 9);
        Assert.DoesNotContain(" only.", HappinessExplanation.ScopeNote);
        Assert.Contains("tax burden", HappinessExplanation.ScopeNote);
    }

    [Fact]
    public void AnUncontrolledSettlement_ReadsNoController_AndNoBurden()
    {
        (WorldState w, PolityId p) = Founded();
        w.TaxPolicies.Add(new TaxPolicyRow(p, 1.0));
        var lost = new SettlementId(1);
        var kept = new List<ControlRow>();
        for (int i = 0; i < w.Controls.Count; i++) if (w.Controls[i].Place != lost) kept.Add(w.Controls[i]);
        w.Controls.Clear();
        foreach (ControlRow r in kept) w.Controls.Add(r);

        TaxBurdenReading b = TaxBurdenReading.Of(w, Cfg(), lost);
        Assert.Equal(-1, b.Controller);
        Assert.False(b.PolicyRowPresent);
        Assert.Equal(0.0, b.EffectiveRate);
        Assert.Equal(1.0, b.Scale);
    }

    [Fact]
    public void TheSettlementRecordCarriesTheBurden_AndTelemetryV4WritesAndReadsItBackExactly()
    {
        (WorldState genesis, PolityId p) = Founded();
        Grant(genesis, p);
        var orders = new OrderLog();
        orders.Append(SetTax(0, p, 45.0));
        TurnExecutor exec = Full(orders);
        WorldState prev = exec.Run(genesis, 2);
        WorldState next = exec.Step(prev);

        TurnObservation obs = Observer.Observe(prev, next, Cfg(), OrderApplied.For(orders, prev.Clock.Turn));
        bool anyTaxed = false;
        foreach (SettlementRecord r in obs.Settlements)
        {
            TaxBurdenReading expected = TaxBurdenReading.Of(next, Cfg(), new SettlementId(r.Settlement));
            Assert.Equal(expected, r.Social.Tax);
            anyTaxed |= r.Social.Tax!.Value.EffectiveRate > 0.0;
        }
        Assert.True(anyTaxed, "no settlement bears the levy — the record is not exercised");

        using var line = new MemoryStream();
        TelemetryWriter.WriteTurn(line, obs);
        string text = System.Text.Encoding.UTF8.GetString(line.ToArray());
        Assert.Contains("\"schema\":\"telemetry/v4\"", text);
        Assert.Contains("\"tax\":{", text);
        TelemetryTurn read = Assert.Single(TelemetryRecordFile.Parse([text.Trim()], "v4-line").Turns);
        for (int i = 0; i < obs.Settlements.Length; i++)
        {
            TaxBurdenReading want = obs.Settlements[i].Social.Tax!.Value;
            TelemetryTax got = read.Settlements[i].Tax;
            Assert.True(got.Recorded);
            Assert.Equal(want.Controller, got.Controller);
            Assert.Equal(want.PolicyRowPresent, got.PolicyRowPresent);
            Assert.Equal(BitConverter.DoubleToInt64Bits(want.NominalRate), BitConverter.DoubleToInt64Bits(got.NominalRate));
            Assert.Equal(BitConverter.DoubleToInt64Bits(want.ControlStrength), BitConverter.DoubleToInt64Bits(got.ControlStrength));
            Assert.Equal(BitConverter.DoubleToInt64Bits(want.EffectiveRate), BitConverter.DoubleToInt64Bits(got.EffectiveRate));
            Assert.Equal(BitConverter.DoubleToInt64Bits(want.Scale), BitConverter.DoubleToInt64Bits(got.Scale));
        }
    }

    [Fact]
    public void TaxEntersThePolicyHistory_AsTheSecondPolicy_AttributedToTheOrderThatSetIt()
    {
        (WorldState genesis, PolityId p) = Founded(aiEmpires: 1);
        var ai = new PolityId(2);
        Grant(genesis, p);   // the AI cannot tax: its edict must leave no trace in the history
        var orders = new OrderLog();
        orders.Append(SetTax(1, p, 20.0));    // log index 0
        orders.Append(SetTax(1, p, 35.0));    // index 1 — same turn, later: it stands
        orders.Append(SetTax(1, ai, 50.0));   // index 2 — refused (no taxation knowledge)
        orders.Append(SetTax(3, p, 0.0));     // index 3 — repeal
        TurnExecutor exec = Full(orders);
        var log = new ObservationLog();
        WorldState w = genesis;
        for (int t = 0; t < 5; t++)
        {
            WorldState next = exec.Step(w);
            log.Observe(w, next, Cfg(), OrderApplied.For(orders, w.Clock.Turn));
            w = next;
        }

        IObservationHistory history = log;
        Assert.Equal(2, history.TaxPolicyChanges.Count);
        TaxPolicyChange set = history.TaxPolicyChanges[0];
        Assert.Equal((2L, p.Value, 0.0, false, 0.35, p.Value, 1),
            (set.Turn, set.Polity, set.OldRate, set.OldRowPresent, set.NewRate, set.Actor, set.OrderIndex));
        TaxPolicyChange repeal = history.TaxPolicyChanges[1];
        Assert.Equal((4L, p.Value, 0.35, true, 0.0, p.Value, 3),
            (repeal.Turn, repeal.Polity, repeal.OldRate, repeal.OldRowPresent, repeal.NewRate, repeal.Actor, repeal.OrderIndex));

        // One state per roster Empire per observed turn, including turns with no change.
        Assert.Equal(5 * 2, history.TaxPolicyStates.Count);
        double[] playerDeclared = [0.0, 0.35, 0.35, 0.0, 0.0];
        int k = 0;
        foreach (TaxPolicyState s in history.TaxPolicyStates)
        {
            if (s.Polity == ai.Value) { Assert.False(s.RowPresent); Assert.Equal(0.0, s.Declared); continue; }
            Assert.Equal(playerDeclared[k++], s.Declared);
        }
        Assert.Equal(5, k);
    }
}
