using System.Globalization;
using Sim.Core.Observability;
using Sim.Core.Observability.Explain;
using Sim.Core.State;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T4.19 lane B (B4) — THE SETTLEMENT TABS from the SettlementRecord
/// (docs/observability-architecture.md §3) and, for Migration, the on-demand
/// MigrationExplanation (§6). Pure: takes records, returns lines. Every
/// figure is a READ/SUMMED/RESIDUAL/RECOMPUTED field of the record and the
/// two residuals are printed WITH their identity strings, verbatim, so a
/// reader sees what each absorbs rather than a number that pretends to be
/// measured. GAP fields are printed as "not recorded" — never as 0.
/// </summary>
public static class SettlementInspectorModel
{
    private static string N(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string Signed(long v) => v.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);
    private static string R(double v) => double.IsNaN(v) ? "not recorded" : v.ToString("F3", CultureInfo.InvariantCulture);

    /// <summary>A record GAP string ("GAP: ...", never a number) worded the way
    /// the packet asks: "not recorded: ...".</summary>
    public static string Gap(string recordGap)
    {
        const string prefix = "GAP: ";
        string body = recordGap.StartsWith(prefix, StringComparison.Ordinal) ? recordGap[prefix.Length..] : recordGap;
        return "not recorded: " + body;
    }

    /// <summary>The line shown on every tab when the settlement has no record
    /// yet (no End Turn played, or founded after the last observation).</summary>
    public const string NoRecord = "no turn observed yet for this settlement - end a turn to see its record";

    public static IReadOnlyList<string> OverviewLines(SettlementRecord r, string name)
    {
        ArgumentNullException.ThrowIfNull(r);
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture,
                $"{name}  (settlement {r.Settlement}, founded turn {r.FoundedTurn}, {(r.Controller >= 0 ? "polity " + r.Controller.ToString(CultureInfo.InvariantCulture) : "no controller")})"),
            string.Create(CultureInfo.InvariantCulture,
                $"population {N(r.Population.Closing)}  ({Signed(r.Population.Closing - r.Population.Opening)} this turn)"),
            string.Create(CultureInfo.InvariantCulture,
                $"grain {N(r.Food.GrainClosing)}  ({Signed(r.Food.GrainClosing - r.Food.GrainOpening)} this turn)  deficit {r.Food.DeficitRatio:F2}"),
            string.Create(CultureInfo.InvariantCulture,
                $"happiness {r.Social.Happiness:F1}  (food {r.Social.HappinessFactors[0]:F2}, housing {r.Social.HappinessFactors[1]:F2})"),
            string.Create(CultureInfo.InvariantCulture,
                $"dwellings {N(r.Housing.DwellingsClosing)}  sufficiency {r.Housing.Sufficiency:F2}"),
        };
        for (int i = 0; i < r.Social.Grievance.Length; i++)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"grievance {r.Social.Grievance[i].Name} {r.Social.Grievance[i].Value:F2}"));
        if (r.Founded) lines.Add("founded THIS turn: openings are 0 and no system wrote a row for it yet");
        return lines;
    }

    public static IReadOnlyList<string> PopulationLines(SettlementRecord r)
    {
        ArgumentNullException.ThrowIfNull(r);
        PopulationSection p = r.Population;
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"opening {N(p.Opening)} -> closing {N(p.Closing)}  ({Signed(p.Closing - p.Opening)})"),
            string.Create(CultureInfo.InvariantCulture, $"  children {N(p.Children)}  adults {N(p.Adults)}  elders {N(p.Elders)}  notables {N(p.Notables)}"),
            string.Create(CultureInfo.InvariantCulture, $"  births {Signed(p.Births)}"),
            string.Create(CultureInfo.InvariantCulture, $"  deaths {Signed(-p.Deaths)}  (natural + starvation, UNSPLIT per settlement - section 8 gap 1)"),
            string.Create(CultureInfo.InvariantCulture, $"  inflow {Signed(p.Inflow)}  outflow {Signed(-p.Outflow)}"),
            string.Create(CultureInfo.InvariantCulture, $"  colonists departed {Signed(-p.ColonistsDeparted)}  (residual)"),
            "  " + p.ColonistsDepartedIdentity,
        };
        for (int i = 0; i < p.ClassCounts.Length; i++)
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"  {p.ClassCounts[i].Name} {N(p.ClassCounts[i].Count)}"));
        return lines;
    }

    public static IReadOnlyList<string> FoodLines(SettlementRecord r)
    {
        ArgumentNullException.ThrowIfNull(r);
        FoodSection f = r.Food;
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture,
                $"grain {N(f.GrainOpening)} + harvest {N(f.Harvest)} - eaten {N(f.Eaten)} - store losses {N(f.StoreLosses)} = {N(f.GrainClosing)}"),
            "  " + f.StoreLossesIdentity,
            string.Create(CultureInfo.InvariantCulture, $"demand {N(f.DemandUnits)} units  obtained {N(f.FoodObtained)}  deficit {f.DeficitRatio:F3}"),
        };
        lines.AddRange(FoodStateLines(r.FoodState));
        for (int i = 0; i < f.FoodGoods.Length; i++)
        {
            FoodGood g = f.FoodGoods[i];
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  {g.Name,-10} produced {N(g.Produced)}  demand {N(g.Demand)}  eaten {N(g.Eaten)}"));
        }
        HousingSection h = r.Housing;
        lines.Add(h.HasRow
            ? string.Create(CultureInfo.InvariantCulture,
                $"dwellings {N(h.DwellingsOpening)} -> {N(h.DwellingsClosing)}  capacity {h.Capacity:F0}  need {h.Need:F1}  sufficiency {h.Sufficiency:F2}  upkeep {h.LastMaintenanceFraction:F2}")
            : "dwellings: no housing row yet (a colony before its first housing turn)");
        lines.Add("  built vs decayed: " + Gap(h.BuiltDecayedSplit));
        return lines;
    }

    /// <summary>
    /// T4.21-5 (spec §3.11, CR-015) — WHAT KIND OF SHORTFALL, beside the deficit
    /// that has always been shown. READ-ONLY and non-authoritative: every value
    /// is a field of the record the observer built by calling the simulation's
    /// own statics, and this method neither classifies nor thresholds anything.
    ///
    /// The deficit line above answers "how much"; these answer "what kind", and
    /// the distinction is the whole of CR-015: a 25% shortfall in a bad decade
    /// is STRESS and nobody dies of it, the same 25% after a crop failure is
    /// FAMINE. Without the state word beside the number the two look identical.
    ///
    /// A record whose prev rows are absent (a founding turn) says so rather than
    /// rendering NORMAL as if the settlement had been observed and found well.
    /// </summary>
    public static IReadOnlyList<string> FoodStateLines(FoodStateSection fs)
    {
        ArgumentNullException.ThrowIfNull(fs);
        if (!fs.PrevRowsPresent)
            return ["food state: not classified - this settlement did not exist in the previous world"];

        var lines = new List<string>(3)
        {
            string.Create(CultureInfo.InvariantCulture,
                $"food state: {StateWord(fs.State)}{ReasonSuffix(fs)}  (nominal {fs.NominalDeficit:F3}, effective {fs.EffectiveDeficit:F3})"),
        };
        lines.Add(fs.DisasterRowPresent && fs.DisasterMultiplierApplied < 1.0
            ? string.Create(CultureInfo.InvariantCulture,
                $"  disaster: severity {fs.DisasterSeverity:F2}, food rates x {fs.DisasterMultiplierApplied:F3} this turn, {fs.DisasterRemainingYears:F1} years still to run")
            : "  disaster: none applied to this harvest");
        if (fs.DisasterPendingRowPresent && fs.DisasterPendingMultiplier < 1.0)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  disaster PENDING for next turn: severity {fs.DisasterPendingSeverity:F2}, food rates x {fs.DisasterPendingMultiplier:F3}"));
        return lines;
    }

    private static string StateWord(FoodStateKind state) => state switch
    {
        FoodStateKind.Normal => "NORMAL - no shortfall",
        FoodStateKind.Stress => "STRESS - the cut is absorbed, nobody starves",
        FoodStateKind.Severe => "SEVERE - adaptation exhausted, starvation begins",
        FoodStateKind.Famine => "FAMINE",
        _ => state.ToString(),
    };

    /// <summary>The cause, from the RECORDED FamineReason and nothing else.</summary>
    private static string ReasonSuffix(FoodStateSection fs) => fs.Reason switch
    {
        FamineReason.Disaster => " - a ruined harvest",
        FamineReason.Abandonment => " - fields left untilled",
        FamineReason.Both => " - a ruined harvest AND fields left untilled",
        _ => fs.Abandoned ? " (food labour is zero, but the store still covers it)" : "",
    };

    /// <summary>The one-line reason the three per-settlement store quantities
    /// are not shown. They are NOT zero and NOT unknown-in-principle: the
    /// simulation computes them, and then keeps them. Recovering them here
    /// would mean an observer-side copy of ConsumptionSystem's private store
    /// bounding, which docs/observability-architecture.md §0 forbids even when
    /// the copy would be numerically exact.</summary>
    public const string StoreGapReason =
        "not recorded: ConsumptionSystem writes no per-settlement capacity, spoilage or "
        + "overflow row; the only honest source would be a copy of its private store-bounding "
        + "arithmetic, which the observability contract forbids. The WORLD totals for spoilage "
        + "and overflow are differenced ledger legs and are in the turn record.";

    /// <summary>The lag warning carried with the published surplus ratio.</summary>
    public const string SurplusRatioLagNote =
        "  (LAST TURN'S: ClassMobilitySystem publishes it from the PREVIOUS world's "
        + "production and requirement, so it will not agree with the balance above - that is not a defect)";

    /// <summary>
    /// T4.20 — THE FOOD FLOW BLOCK, additive and separate from
    /// <see cref="FoodLines"/> (which keeps the grain STORE account untouched).
    /// It answers one question: did this settlement, this turn, grow more food
    /// than it needed? Every figure is a field of the record:
    /// <c>FoodProduced</c> SUMMED, <c>DemandUnits</c> READ, <c>FoodBalance</c>
    /// DIFFERENCED, per-good produced/eaten READ, reserve READ from the
    /// economy section's stock, store losses the existing RESIDUAL printed with
    /// its own identity. Nothing is divided by the turn length: these are
    /// whole-turn totals and the header says so. Nothing is recomputed.
    ///
    /// The surplus / balanced / deficit word comes from the SIGN of the balance
    /// and nothing else — no threshold, no new constant. The zero case is
    /// "balanced" BY DEFINITION (produced == required exactly), not because it
    /// falls inside a band.
    ///
    /// Pure and allocation-light: plain index loops, no LINQ, InvariantCulture
    /// on every conversion. It runs once per selection rebuild, not per frame,
    /// but obeys the per-frame rule anyway.
    /// </summary>
    public static IReadOnlyList<string> FoodFlowLines(SettlementRecord r, double dtYears)
    {
        ArgumentNullException.ThrowIfNull(r);
        FoodSection f = r.Food;
        long balance = f.FoodBalance;
        string verdict = balance > 0 ? "surplus" : balance < 0 ? "deficit" : "balanced";
        var lines = new List<string>(12)
        {
            string.Create(CultureInfo.InvariantCulture,
                $"FOOD THIS TURN ({dtYears:F1} sim-years) - whole-turn totals, not per-year"),
            string.Create(CultureInfo.InvariantCulture, $"  produced   {Signed(f.FoodProduced)}"),
            string.Create(CultureInfo.InvariantCulture, $"  required   {Signed(-f.DemandUnits)}"),
            string.Create(CultureInfo.InvariantCulture, $"  balance    {Signed(balance)}        {verdict}"),
        };
        for (int i = 0; i < f.FoodGoods.Length; i++)
        {
            FoodGood g = f.FoodGoods[i];
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  {g.Name,-10} produced {N(g.Produced)}  eaten {N(g.Eaten)}"));
        }
        if (f.FoodGoods.Length == 0) lines.Add("  no food good has a stock row for this settlement yet");
        for (int i = 0; i < f.FoodGoods.Length; i++)
        {
            FoodGood g = f.FoodGoods[i];
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  reserve {g.Name,-10} {N(Reserve(r.Economy, g.Good))}"));
        }
        lines.Add(string.Create(CultureInfo.InvariantCulture,
            $"  store losses {Signed(-f.StoreLosses)}  (residual, grain only)"));
        lines.Add("  " + f.StoreLossesIdentity);
        lines.Add(string.Create(CultureInfo.InvariantCulture,
            $"  food surplus ratio: {R(r.Economy.FoodSurplusRatio)}"));
        lines.Add(SurplusRatioLagNote);
        lines.Add("  granary capacity / spoilage / overflow, per settlement: " + StoreGapReason);
        return lines;
    }

    /// <summary>The good's closing stock, READ from the economy section's own
    /// GoodReading (EconomySection.Goods is next's GoodStockRow.Amount). 0 when
    /// the settlement carries no row for that good. Index loop, no LINQ.</summary>
    private static long Reserve(EconomySection e, int good)
    {
        for (int i = 0; i < e.Goods.Length; i++)
            if (e.Goods[i].Good == good) return e.Goods[i].Stock;
        return 0;
    }

    /// <summary>ECONOMY tab, record half (the MarketModel rows and price plot
    /// are the other half): shares in force, variables, class latches, trade legs.</summary>
    public static IReadOnlyList<string> EconomyLines(SettlementRecord r, Func<int, string> name)
    {
        ArgumentNullException.ThrowIfNull(r);
        EconomySection e = r.Economy;
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture,
                $"shares in force: farm {e.SectorShares[0] * 100:F0}% herd {e.SectorShares[1] * 100:F0}% mine {e.SectorShares[2] * 100:F0}% craft {e.SectorShares[3] * 100:F0}% build {e.SectorShares[4] * 100:F0}%{(e.SectorRowPresent ? "" : "  (default, never ordered)")}"),
            string.Create(CultureInfo.InvariantCulture,
                $"food surplus ratio {R(e.FoodSurplusRatio)}  artisan share {R(e.ArtisanShare)}  trade volume {R(e.TradeVolume)}"),
        };
        var latches = new System.Text.StringBuilder("classes active: ");
        for (int i = 0; i < e.ClassActive.Length; i++)
        {
            if (i > 0) latches.Append(", ");
            latches.Append(e.ClassActive[i].Name).Append(' ').Append(e.ClassActive[i].Active.ToString(CultureInfo.InvariantCulture));
        }
        lines.Add(latches.ToString());
        for (int i = 0; i < e.TradeIn.Length; i++)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  trade in   {e.TradeIn[i].Name,-10} {N(e.TradeIn[i].Quantity)} from {name(e.TradeIn[i].Other)}"));
        for (int i = 0; i < e.TradeOut.Length; i++)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  trade out  {e.TradeOut[i].Name,-10} {N(e.TradeOut[i].Quantity)} to {name(e.TradeOut[i].Other)}"));
        if (e.TradeIn.Length == 0 && e.TradeOut.Length == 0) lines.Add("  no trade legs this turn");
        return lines;
    }

    /// <summary>MIGRATION: the record's flows, the explanation's push / pull /
    /// others sorted (SmoothedAttractiveness DESC, id ASC — the query's own
    /// order), and every GAP as "not recorded". <paramref name="explanation"/>
    /// is null when the session holds no previous world yet.</summary>
    public static IReadOnlyList<string> MigrationLines(
        SettlementRecord r, MigrationExplanation? explanation, Func<int, string> name)
    {
        ArgumentNullException.ThrowIfNull(r);
        MigrationSection m = r.Migration;
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture,
                $"inflow {Signed(r.Population.Inflow)}  outflow {Signed(-r.Population.Outflow)}"),
            string.Create(CultureInfo.InvariantCulture,
                $"push: source deficit {m.PushDeficitRatio:F3} (prev turn - famine flight driver)"),
            string.Create(CultureInfo.InvariantCulture,
                $"pull: smoothed attractiveness {R(m.PullAttractiveness)} (prev turn)"),
            string.Create(CultureInfo.InvariantCulture,
                $"food gate inputs: prev grain store {N(m.PrevGrainStock)}, prev harvest {N(m.PrevGrainHarvest)}"),
            string.Create(CultureInfo.InvariantCulture,
                $"unplaced departure {m.UnplacedDeparture:F1}  remainder {m.UnplacedRemainder:F1}"),
        };
        if (explanation is null)
        {
            lines.Add("destinations: the explanation needs the previous world - available from the next End Turn");
        }
        else
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"self as destination: deficit {explanation.Self.DestinationDeficit:F3}  grain {(explanation.Self.GrainPresent ? "present" : "ABSENT")}  happiness {R(explanation.Self.Happiness)}"));
            lines.Add("other destinations (attractiveness DESC, id ASC):");
            for (int i = 0; i < explanation.Others.Length; i++)
            {
                Destination d = explanation.Others[i];
                lines.Add(string.Create(CultureInfo.InvariantCulture,
                    $"  {name(d.Id.Value),-14} pull {R(d.SmoothedAttractiveness)}  deficit {d.DestinationDeficit:F3}  grain {(d.GrainPresent ? "present" : "ABSENT")}  happiness {R(d.Happiness)}"));
                // T4.21-5: what that destination could ACCEPT this turn, and
                // whether it turned anyone away. RECOMPUTED via MigrationSystem
                // .Plan by the explanation; printed, never re-derived.
                if (d.PlanRecorded)
                    lines.Add(string.Create(CultureInfo.InvariantCulture,
                        $"                 vacancy {R(d.Vacancy)}  cap {R(d.VacancyCap)}  wanted {d.DesiredInflowAe:F1} ae  vacScale {d.VacancyScale:F3}{(d.VacancyScale < 1.0 ? "  <- REFUGEES REFUSED" : "")}"));
            }
            if (explanation.PlanRecorded)
                lines.Add(string.Create(CultureInfo.InvariantCulture,
                    $"source bound: exit openness {explanation.ExitOpenness:F4}  flight fraction at profile 1 {explanation.FlightFractionPrime:F4}  bound {explanation.FlightBound:F1} heads  (dt {explanation.DtYears:F1}y)"));
            lines.Add("  " + MigrationExplanation.PushReading);
        }
        lines.Add("pairwise flows: " + Gap(m.PairwiseFlows));
        lines.Add("damping: " + MigrationExplanation.Damping);
        lines.Add("viability products: " + MigrationExplanation.ViabilityProducts);
        lines.Add("gap scale: " + MigrationExplanation.GapScale);
        return lines;
    }

    public static IReadOnlyList<string> OrderLines(SettlementRecord r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.Orders.Length == 0) return ["no orders applied to this settlement this turn"];
        var lines = new List<string>(r.Orders.Length + 1)
        {
            "orders applied this step (delivered from the previous turn's log batch):",
        };
        for (int i = 0; i < r.Orders.Length; i++)
        {
            OrderApplied o = r.Orders[i];
            string target = o.Sector >= 0 ? SectorBarModel.SectorNames[o.Sector] : o.Kind.ToString();
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"  #{o.Index}  actor {o.Actor}  {o.Kind}  {target}  amount {o.Amount:F0}"));
        }
        return lines;
    }
}
