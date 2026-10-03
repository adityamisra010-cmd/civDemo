using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.NeedsGrievance;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// M5 integration R2b — D-021 UNREST-LITE, the tax brake (Director decision 9, item 15 GOVERNANCE):
/// tax burden → Dignity (D-035-D) → grievance → protest (output drag, discharge) → uprising (revolt).
/// High tax produces pressure; the pressure has a deterministic gameplay consequence; normal tax remains
/// viable; revolt is deterministic.
/// </summary>
public class UnrestTests
{
    private static SimConfig Cfg => GovernanceRigs.Cfg();
    private static UnrestTuning Tuning => Cfg.Needs!.Unrest!;

    private static void Levy(WorldState w, PolityId p, double rate)
    {
        for (int i = 0; i < w.TaxPolicies.Count; i++)
            if (w.TaxPolicies[i].Polity == p) { w.TaxPolicies[i] = new TaxPolicyRow(p, rate); return; }
        w.TaxPolicies.Add(new TaxPolicyRow(p, rate));
    }

    /// <summary>Sets every class's grievance at <paramref name="s"/> and, unless <paramref name="levyFelt"/> is
    /// false, publishes a felt levy (Dignity 0.5) as the ONLY satisfaction row of each class — so the whole
    /// stock is the levy's (TaxGrievance == G exactly: lifting the levy would close the entire shortfall).</summary>
    private static void SetGrievance(WorldState w, SettlementId s, double value, bool levyFelt = true)
    {
        var keep = new List<NeedSatisfactionRow>();
        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
            if (w.NeedSatisfactions[i].Settlement != s) keep.Add(w.NeedSatisfactions[i]);
        w.NeedSatisfactions.Clear();
        foreach (NeedSatisfactionRow r in keep) w.NeedSatisfactions.Add(r);
        for (int i = 0; i < w.Grievances.Count; i++)
        {
            if (w.Grievances[i].Settlement != s) continue;
            w.Grievances[i] = w.Grievances[i] with { Value = value };
            if (levyFelt) w.NeedSatisfactions.Add(new NeedSatisfactionRow(s, w.Grievances[i].Class, 7, 0.5));
        }
    }

    private static double DignityRow(IReadOnlyWorldState w, SettlementId s)
    {
        for (int i = 0; i < w.NeedSatisfactions.Count; i++)
            if (w.NeedSatisfactions[i].Settlement == s && w.NeedSatisfactions[i].NeedId == 7) return w.NeedSatisfactions[i].Value;
        return double.NaN;
    }

    // ------------------------------------------------------------------ the content

    [Fact]
    public void TheShippedContent_BindsDignityToTheTaxBurden_AndShipsTheUnrestSection()
    {
        NeedEntry dignity = Cfg.Needs!.Needs.Single(n => n.Id == 7);
        Assert.True(dignity.Bound);
        Assert.True(dignity.FromTaxBurden);
        Assert.NotNull(Cfg.Needs.Unrest);
        Assert.True(Tuning.UprisingGrievance > Tuning.ProtestOnsetGrievance);
    }

    [Fact]
    public void ALoaderRefuses_ABasketForTheTaxBurdenNeed_AndAnUprisingBelowTheOnset()
    {
        string json;
        using (var reader = new StreamReader(Sim.Data.DataFiles.OpenNeeds())) json = reader.ReadToEnd();
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            System.Text.RegularExpressions.Regex.Replace(json, "\"uprisingGrievance\": [0-9.]+", "\"uprisingGrievance\": 1.0")));
        Assert.Throws<NeedsConfigException>(() => NeedsConfigLoader.Load(
            json.Replace("\"source\": \"taxBurden\"", "\"source\": \"taxBurdn\"", StringComparison.Ordinal)));
    }

    // ------------------------------------------------------------------ high tax → pressure

    [Fact]
    public void HighTax_InjuresDignityDirectly_AndGrievanceRisesAboveTheUntaxedTwin()
    {
        (WorldState taxed, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(taxed, player);
        WorldState untaxed = taxed.Clone();
        Levy(taxed, player, 0.9);

        TurnExecutor ex = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(Cfg));
        WorldState t1 = ex.Step(taxed), u1 = ex.Step(untaxed);

        // D-035-D: the satisfaction IS one minus the effective rate (declared × reach; the seat's reach is 1.0).
        Assert.Equal(1.0 - Governance.EffectiveTaxRate(taxed, seat, Cfg), DignityRow(t1, seat));
        Assert.Equal(1.0 - 0.9, DignityRow(t1, seat), 12);
        Assert.Equal(1.0, DignityRow(u1, seat));
        Assert.True(Unrest.Grievance(t1, seat) > Unrest.Grievance(u1, seat));

        // ...and keeps rising under a sustained levy until protest's discharge and decay hold it.
        WorldState t = t1, u = u1;
        for (int i = 0; i < 6; i++) { t = ex.Step(t); u = ex.Step(u); }
        Assert.True(Unrest.Grievance(t, seat) > Unrest.Grievance(t1, seat));
        Assert.True(Unrest.Grievance(t, seat) - Unrest.Grievance(u, seat) > Unrest.Grievance(t1, seat) - Unrest.Grievance(u1, seat));
    }

    [Fact]
    public void WithoutAGovernanceSection_DignityHasNoCarrier_AndPublishesNothing()
    {
        SimConfig bare = Cfg with { Governance = null };
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        WorldState next = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.NeedsGrievance(bare)).Step(w);
        Assert.True(double.IsNaN(DignityRow(next, seat)));
    }

    // ------------------------------------------------------------------ the protest law and its consequences

    [Fact]
    public void Protest_IsZeroAtOrBelowTheOnset_LinearBetween_OneAtTheUprising()
    {
        UnrestTuning u = Tuning;
        Assert.Equal(0.0, Unrest.ProtestOf(0.0, u));
        Assert.Equal(0.0, Unrest.ProtestOf(u.ProtestOnsetGrievance, u));
        Assert.Equal(0.5, Unrest.ProtestOf((u.ProtestOnsetGrievance + u.UprisingGrievance) / 2.0, u), 12);
        Assert.Equal(1.0, Unrest.ProtestOf(u.UprisingGrievance, u));
        Assert.Equal(1.0, Unrest.ProtestOf(u.UprisingGrievance * 3.0, u));
        Assert.Equal(0.0, Unrest.ProtestOf(double.NaN, u));
    }

    [Fact]
    public void Protest_DragsRealisedOutput_AndTheDragGrowsWithItsAmplitude()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        UnrestTuning u = Tuning;

        // Quiet and untaxed: EXACTLY 1.0 (a quiet world produces bit-identically).
        SetGrievance(w, seat, u.ProtestOnsetGrievance);
        Assert.Equal(1.0, Governance.OutputMultiplier(w, seat, Cfg));

        Levy(w, player, 0.5);
        double extraction = Governance.ExtractionMultiplier(w, seat, Cfg);
        Assert.Equal(extraction, Governance.OutputMultiplier(w, seat, Cfg));   // quiet: the extraction alone

        double previous = extraction;
        foreach (double fraction in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            SetGrievance(w, seat, u.ProtestOnsetGrievance + fraction * (u.UprisingGrievance - u.ProtestOnsetGrievance));
            double expected = extraction * (1.0 - u.ProtestOutputDragMax * fraction * Governance.EffectiveTaxRate(w, seat, Cfg));
            Assert.Equal(expected, Governance.OutputMultiplier(w, seat, Cfg), 12);
            Assert.True(Governance.OutputMultiplier(w, seat, Cfg) < previous);
            previous = Governance.OutputMultiplier(w, seat, Cfg);
        }
        // At full protest the drag takes back more than full extraction gives (taxExtractionResponseMax 0.3).
        Assert.True(previous < 1.0);
    }

    [Fact]
    public void GrievanceTheLevyDoesNotExplain_IgnitesNothing_SoHungerOrMissingPotteryCannotRaiseARuler()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SetGrievance(w, seat, Tuning.UprisingGrievance * 4.0, levyFelt: false);
        Assert.Equal(0.0, Unrest.TaxGrievance(w, seat, Cfg));
        Assert.Equal(0.0, Unrest.Protest(w, seat, Cfg));
        Assert.Equal(1.0, Unrest.OutputFactor(w, seat, Cfg));
        Assert.Equal(0.0, Unrest.DischargePerYear(w, seat, Cfg));
        Assert.False(Unrest.IsUprising(w, seat, Cfg));
    }

    [Fact]
    public void TheLevysGrievance_IsTheShareOfTheShortfallLiftingTheLevyWouldClose()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SetGrievance(w, seat, 40.0);
        Assert.Equal(40.0, Unrest.TaxGrievance(w, seat, Cfg), 9);    // Dignity the only shortfall: all of it
        // Add an equally weighted second shortfall the levy does not cause: the levy's share falls below 1.
        for (int i = 0; i < w.Grievances.Count; i++)
            if (w.Grievances[i].Settlement == seat) w.NeedSatisfactions.Add(new NeedSatisfactionRow(seat, w.Grievances[i].Class, 6, 0.2));
        double share = Unrest.TaxGrievance(w, seat, Cfg) / 40.0;
        Assert.InRange(share, 0.01, 0.99);
    }

    [Fact]
    public void Protest_Discharges_TheDecayRateGainsDischargeTimesIntensity_AndQuietIsBitIdentical()
    {
        GrievanceTuning g = Cfg.Needs!.Grievance;
        Assert.Equal(NeedsGrievanceSystem.DecayRatePerYear(g, 0.07), NeedsGrievanceSystem.DecayRatePerYear(g, 0.07, 0.0));
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SetGrievance(w, seat, Tuning.UprisingGrievance);
        Assert.Equal(Tuning.ProtestDischargePerYear, Unrest.DischargePerYear(w, seat, Cfg));
        SetGrievance(w, seat, Tuning.ProtestOnsetGrievance - 1.0);
        Assert.Equal(0.0, Unrest.DischargePerYear(w, seat, Cfg));
    }

    [Fact]
    public void WithoutAnUnrestSection_EveryReaderIsNeutral()
    {
        SimConfig inert = Cfg with { Needs = Cfg.Needs! with { Unrest = null } };
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(w, player);
        SetGrievance(w, seat, 1000.0);
        Assert.Equal(0.0, Unrest.Protest(w, seat, inert));
        Assert.Equal(1.0, Unrest.OutputFactor(w, seat, inert));
        Assert.Equal(0.0, Unrest.DischargePerYear(w, seat, inert));
        Assert.False(Unrest.IsUprising(w, seat, inert));
    }

    // ------------------------------------------------------------------ uprising: deterministic revolt

    [Fact]
    public void AnUprising_RevoltsAWellProvidedSettlement_BeforeTotalDeprivation_AndOnlyAtTheThreshold()
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        SettlementId seatId = GovernanceRigs.Seat(w, player), place = seatId;
        for (int i = 0; i < w.Settlements.Count; i++)
            if (w.Settlements[i].Id != seatId) { place = w.Settlements[i].Id; break; }
        Assert.NotEqual(seatId, place);
        GovernanceRigs.WellProvided(w, place);
        Assert.True(SettlementHappiness.Of(w, place, Cfg) > 50.0);   // nowhere near the zero corner

        TurnExecutor revolt = UniversityRigs.Only(new OrderLog(), 10.0, SystemCatalog.Revolt(Cfg));
        SetGrievance(w, place, Math.BitDecrement(Tuning.UprisingGrievance));
        Assert.True(EmpireQuery.ControlsSettlement(revolt.Step(w), player, place));

        SetGrievance(w, place, Tuning.UprisingGrievance);
        WorldState a = revolt.Step(w), b = revolt.Step(w.Clone());
        Assert.False(EmpireQuery.ControlsSettlement(a, player, place));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));   // deterministic
        // Only the relation is lost: nobody is moved, nothing is destroyed.
        Assert.True(WorldStates.TableEquals(w.Buckets, a.Buckets));
        Assert.True(WorldStates.TableEquals(w.GoodStocks, a.GoodStocks));
    }

    // ------------------------------------------------------------------ the integrated loop

    private static (WorldState World, int RevoltTurn, double PeakCapitalProtest, int QuietAgainTurn) RunLevy(double percent, int turns)
    {
        (WorldState w, PolityId player) = GovernanceRigs.Founded();
        GovernanceRigs.Grant(w, player);
        SettlementId seat = GovernanceRigs.Seat(w, player);
        var orders = new OrderLog();
        if (percent > 0.0) orders.Append(Governance.TaxOrder(0, player, percent));
        TurnExecutor ex = UniversityRigs.Production(Cfg, orders);
        int revoltTurn = -1, quietTurn = -1;
        double peak = 0.0;
        for (int t = 1; t <= turns; t++)
        {
            w = ex.Step(w);
            double p = Unrest.Protest(w, seat, Cfg);
            peak = Math.Max(peak, p);
            if (revoltTurn < 0 && !EmpireQuery.ControlsSettlement(w, player, seat)) revoltTurn = t;
            if (revoltTurn > 0 && quietTurn < 0 && p == 0.0) quietTurn = t;
        }
        return (w, revoltTurn, peak, quietTurn);
    }

    [Fact]
    public void NormalTax_AtTheAiCeiling_RemainsViable_NoProtestNoRevolt_AndOutputGains()
    {
        const int Turns = 40;
        (WorldState taxed, int revolt, double peak, _) = RunLevy(40.0, Turns);
        (WorldState untaxed, _, _, _) = RunLevy(0.0, Turns);
        Assert.Equal(-1, revolt);
        Assert.Equal(0.0, peak);
        for (int s = 0; s < taxed.Settlements.Count; s++)
            Assert.Equal(0.0, Unrest.Protest(taxed, taxed.Settlements[s].Id, Cfg));
        Assert.Equal(untaxed.Controls.Count, taxed.Controls.Count);
        long Grain(WorldState w)
        {
            long total = 0;
            for (int i = 0; i < w.GoodStocks.Count; i++)
                if (w.GoodStocks[i].Good.Value == Cfg.Goods!.GrainId) total += w.GoodStocks[i].LastProducedUnits;
            return total;
        }
        Assert.True(Grain(taxed) > Grain(untaxed));
    }

    [Fact]
    public void ExtremeTax_IgnitesProtest_RaisesTheSeat_AndTheEpisodeBurnsOut_Deterministically()
    {
        const int Turns = 40;
        (WorldState a, int revoltA, double peakA, int quietA) = RunLevy(99.0, Turns);
        (WorldState b, int revoltB, double peakB, int quietB) = RunLevy(99.0, Turns);
        Assert.True(revoltA > 0, "a 99 % levy at full reach must raise the seat within the horizon");
        Assert.True(peakA > 0.0);
        Assert.True(quietA > revoltA, "the episode must end: protest returns to zero after the uprising");
        Assert.Equal((revoltA, peakA, quietA), (revoltB, peakB, quietB));
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
    }

    // ------------------------------------------------------------------ R2c: the uprising meets city-state research

    /// <summary>R2c interaction (R2a × R2b): a settlement thrown off by an UPRISING has no controller, so it is a
    /// city-state and researches on its own under its local holder (SettlementKnowledge.LocalHolder) — no extra
    /// mechanism. Decision (R2c, INFERRED, recorded in ADR-033 R2c): its local record starts EMPTY; it is not seeded
    /// with the former ruler's knowledge (that would be a knowledge-diffusion mechanic no ruling provides).</summary>
    [Fact]
    public void ARevoltedSeat_BecomesACityState_AndResearchesFromAnEmptyLocalRecord()
    {
        (WorldState w, int revolt, _, _) = RunLevy(99.0, 40);
        Assert.True(revolt > 0);
        (WorldState fresh, PolityId player) = GovernanceRigs.Founded();
        SettlementId seat = GovernanceRigs.Seat(fresh, player);
        PolityId local = SettlementKnowledge.LocalHolder(seat);
        Assert.False(EmpireQuery.TryGetController(w, seat, out _));
        double progress = 0.0;
        for (int i = 0; i < w.ResearchProgress.Count; i++)
            if (w.ResearchProgress[i].Polity == local) progress += w.ResearchProgress[i].Progress;
        int completed = 0;
        for (int i = 0; i < w.ResearchCompleted.Count; i++)
            if (w.ResearchCompleted[i].Polity == local) completed++;
        Assert.True(progress > 0.0 || completed > 0, "the uncontrolled seat must accumulate its own research");
        // Started empty: the grant given to the ruler (taxation) is not in the city-state's own record.
        bool[] own = ResearchQuery.CompletedMask(w, TestConfigs.Research(), local);
        bool[] ruler = ResearchQuery.CompletedMask(w, TestConfigs.Research(), player);
        int inherited = 0;
        for (int i = 0; i < own.Length; i++) if (own[i] && ruler[i]) inherited++;
        Assert.Equal(0, inherited);
    }
}
