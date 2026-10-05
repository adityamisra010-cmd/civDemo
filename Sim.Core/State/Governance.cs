using Sim.Core.Kernel;
using Sim.Core.Systems;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Research;

namespace Sim.Core.State;

/// <summary>
/// ADR-033 D4 — THE GOVERNING LOOP'S READERS (ported from <c>m5-full-build</c>). Pure derived
/// queries over authoritative state: nothing here mutates, nothing here is stored, and no system
/// calls another system to get an answer (law 6 — systems share these statics through State).
///
/// THE LOOP THESE READERS CLOSE, every arrow a real consumer:
///
///   tax policy (TaxPolicyRow — an Empire's standing decision, order-set, research-gated)
///     → administrative REACH (ControlRow.Strength, computed by GovernanceSystem from the
///       capital over the road-aware SettlementDistances) decides what is actually collected
///     → EFFECTIVE rate = nominal × Strength
///     → raises realised production (ProductionSystem, <see cref="ExtractionMultiplier"/>)
///     → is FELT by each population segment, offset by its provision and the settlement's services
///       (<see cref="Unrest.FeltBurden"/>; H2) → injures DIGNITY (D-035-D) → the segment's LEVY GRIEVANCE
///       accumulates (a stock, dt-integrated; H2)
///       → lowers HAPPINESS as it accumulates (<see cref="SettlementHappiness.TaxSufficiency"/>), never at the edict
///       → PROTEST (<see cref="Unrest"/>): output drag and discharge; past the segment's own TIPPING POINT a
///         growing portion of it in revolt; the settlement UPRISING only when the rebels carry it (D-021
///         unrest-lite, H2 — the negative loop that grows with the levy; no tax → revolt rule anywhere)
///     → happiness drives migration's destination weight (D-021 valves)
///     → LEGITIMACY reads the condition of what the Empire still holds
///     → the AI tax valve (AiGovernance) answers it.
///
/// ONE FACT, ONE PLACE. Reach is computed in exactly one function (<see cref="AdministrativeReach"/>),
/// called by exactly one writer (GovernanceSystem), and stored in exactly one field
/// (<see cref="ControlRow.Strength"/>). Every consumer — production, happiness, legitimacy, the
/// UI — reads the stored value through <see cref="ControlStrength"/> and
/// <see cref="EffectiveTaxRate"/>; none recomputes reach beside it (the defect M5B shipped:
/// Strength written, never read, while production and happiness recomputed reach).
///
/// WHAT THE TAX IS NOT (CR-008: "tax is a policy on flows; no treasury"). It moves no goods,
/// holds no stock and has no recipient; goods stay in <see cref="GoodStockRow"/> and economic
/// ownership stays derived through <see cref="ControlRow"/>.
///
/// INERT WITHOUT CONFIG. With no <c>governance</c> section every reader returns the neutral
/// value — effective rate 0, extraction ×1, no levy possible — so a rig that runs the full
/// catalog on a hand-written config behaves exactly as before the port.
/// </summary>
public static class Governance
{
    /// <summary>
    /// What an Empire has DECLARED it will take, a fraction in [0, 1]. Absence of a row is the
    /// never-legislated default of ZERO (the <see cref="SectorAllocationRow"/> convention); a
    /// NaN rate reads 0 (an unmeasurable policy is not a levy). First row by table order — the
    /// system keeps one row per Empire.
    /// </summary>
    public static double NominalTaxRate(IReadOnlyWorldState world, PolityId polity)
    {
        for (int i = 0; i < world.TaxPolicies.Count; i++)
        {
            if (world.TaxPolicies[i].Polity.Value != polity.Value) continue;
            double rate = world.TaxPolicies[i].Rate;
            if (double.IsNaN(rate)) return 0.0;
            return Math.Clamp(rate, 0.0, 1.0);
        }

        return 0.0;
    }

    /// <summary>Whether <paramref name="polity"/> has a policy row at all (a declared rate,
    /// possibly zero) — the difference between "levies 0 %" and "never legislated".</summary>
    public static bool HasPolicy(IReadOnlyWorldState world, PolityId polity)
    {
        for (int i = 0; i < world.TaxPolicies.Count; i++)
            if (world.TaxPolicies[i].Polity.Value == polity.Value) return true;
        return false;
    }

    /// <summary>
    /// ADMINISTRATIVE REACH of <paramref name="polity"/> at <paramref name="place"/>, in [0, 1] —
    /// THE WRITER'S COMPUTATION: GovernanceSystem evaluates it on PREV and stores the result in
    /// <see cref="ControlRow.Strength"/>. Nothing else in the simulation calls it; consumers read
    /// the stored value (<see cref="ControlStrength"/>).
    ///
    /// <c>exp(−travelCost / authorityDecayCostUnits)</c> from the polity's capital over
    /// <see cref="SettlementDistanceRow"/> — the same table, functional form and default e-fold
    /// migration's damping uses — so it is a distance term over the NETWORK GRAPH (D-040 C3), and
    /// because SettlementDistances are the authoritative Pathfinder's costs over the built roads,
    /// a road that cuts travel cost raises reach (D-040 C6's road–control coupling, realized).
    ///
    /// The capital is the origin and administers itself in full (1.0). An Empire with NO capital,
    /// or whose capital it no longer controls, has no seat and reaches nothing (0.0). A place with
    /// no distance row from the seat is UNADMINISTERED (0.0), never adjacent — missing data must not
    /// read as perfect administration; an unreachable place (+∞ cost) reaches 0.0 by construction.
    /// </summary>
    public static double AdministrativeReach(
        IReadOnlyWorldState world, PolityId polity, SettlementId place, GovernanceConfig governance)
    {
        ArgumentNullException.ThrowIfNull(governance);
        if (!EmpireQuery.TryGetCapital(world, polity, out SettlementId seat)) return 0.0;
        if (!EmpireQuery.ControlsSettlement(world, polity, seat)) return 0.0;   // a lost seat administers nothing
        if (seat.Value == place.Value) return 1.0;                             // the seat administers itself in full

        double decay = governance.AuthorityDecayCostUnits;
        if (!(decay > 0.0)) return 0.0;

        for (int i = 0; i < world.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = world.SettlementDistances[i];
            if (row.From.Value != seat.Value || row.To.Value != place.Value) continue;
            double cost = row.TravelCost;
            if (double.IsNaN(cost)) return 0.0;
            return Math.Clamp(Math.Exp(-cost / decay), 0.0, 1.0);
        }

        return 0.0;   // no route on record: unadministered, not adjacent
    }

    /// <summary>
    /// THE ONE STORED REACH: <see cref="ControlRow.Strength"/> of the (polity, place) control
    /// relation, clamped to [0, 1] (NaN reads 0). No relation reads 0. Should a hand-built world
    /// carry two rows for one relation, the MINIMUM is taken — a function of the row set, never of
    /// row order (law 5; the <see cref="EmpireQuery.TryGetController"/> precedent).
    /// </summary>
    public static double ControlStrength(IReadOnlyWorldState world, PolityId polity, SettlementId place)
    {
        bool found = false;
        double strength = 0.0;
        for (int i = 0; i < world.Controls.Count; i++)
        {
            ControlRow row = world.Controls[i];
            if (row.Polity.Value != polity.Value || row.Place.Value != place.Value) continue;
            double s = double.IsNaN(row.Strength) ? 0.0 : Math.Clamp(row.Strength, 0.0, 1.0);
            if (!found || s < strength) strength = s;
            found = true;
        }

        return found ? strength : 0.0;
    }

    /// <summary>
    /// What the state ACTUALLY extracts at <paramref name="settlement"/>: the controller's declared
    /// rate scaled by the stored reach, nominal × <see cref="ControlRow.Strength"/>, in [0, 1].
    /// The single number every downstream consumer uses — production reads it as effort
    /// compelled, happiness as burden borne — so the gain and the cost can never drift apart.
    /// Zero for an uncontrolled settlement, an untaxed Empire, and whenever the config carries no
    /// governance section (the loop is inert).
    /// </summary>
    public static double EffectiveTaxRate(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (cfg.Governance is null) return 0.0;
        if (!EmpireQuery.TryGetController(world, settlement, out PolityId polity)) return 0.0;
        double nominal = NominalTaxRate(world, polity);
        if (nominal <= 0.0) return 0.0;   // the common case, and it costs nothing
        return Math.Clamp(nominal * ControlStrength(world, polity, settlement), 0.0, 1.0);
    }

    /// <summary>
    /// The multiplier on a settlement's realised production: <c>1 + taxExtractionResponseMax ×
    /// effectiveRate</c>. A state that taxes harder works its realm harder (corvée, quotas, levied
    /// labour), so extraction RAISES output while costing the people. Untaxed is EXACTLY 1.0, so an
    /// untaxed world produces bit-identically to the tree before the port.
    /// </summary>
    public static double ExtractionMultiplier(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        double rate = EffectiveTaxRate(world, settlement, cfg);
        if (rate <= 0.0) return 1.0;
        return 1.0 + cfg.Governance!.TaxExtractionResponseMax * rate;
    }

    /// <summary>
    /// M5 R2b — WHAT REALISED PRODUCTION IS MULTIPLIED BY: the levy's
    /// <see cref="ExtractionMultiplier"/> times protest's drag (<see cref="Unrest.OutputFactor"/>,
    /// D-021 unrest-lite: discontent → protest → production drag). The extraction a heavy levy buys is
    /// therefore paid back in disorder once the grievance it breeds passes the protest onset — the
    /// brake strengthens with the amplitude of the exaction. A quiet settlement's drag is EXACTLY 1.0,
    /// so the product is the extraction multiplier bit for bit (and 1.0 untaxed and quiet).
    /// </summary>
    public static double OutputMultiplier(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        double extraction = ExtractionMultiplier(world, settlement, cfg);
        double drag = Unrest.OutputFactor(world, settlement, cfg);
        return drag == 1.0 ? extraction : extraction * drag;
    }

    /// <summary>
    /// F1 (2026-10-05, d049 §13) — WHAT REALISED FOOD PRODUCTION (farming, gathering, herding and fishing) IS
    /// MULTIPLIED BY. Protesters and rebels withhold the work the LEVY compels, never the work that feeds themselves:
    /// the drag (<see cref="Unrest.OutputFactor"/>) can take back the levy's extraction gain on food, but never pushes
    /// food output below what the people would produce untaxed (1.0). Non-food output keeps the full
    /// <see cref="OutputMultiplier"/> (a strike of the levied crafts, ore and stone). Without this floor a final
    /// settlement (which cannot revolt away, D-048 ruling 5) under a sustained 99–100 % levy starved to extinction
    /// through its own rebels. EXACTLY <see cref="ExtractionMultiplier"/> when there is no drag, so every quiet or
    /// untaxed world produces bit-identically.
    /// </summary>
    public static double FoodOutputMultiplier(IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        double extraction = ExtractionMultiplier(world, settlement, cfg);
        double drag = Unrest.OutputFactor(world, settlement, cfg);
        if (drag == 1.0) return extraction;
        return Math.Max(extraction * drag, Math.Min(extraction, 1.0));
    }

    /// <summary>
    /// LEGITIMACY — how well an Empire is regarded by the people it actually holds, on the 0..100
    /// happiness scale: the POPULATION-WEIGHTED mean <see cref="SettlementHappiness.Of"/> of the
    /// settlements it controls. Derived, never stored. An Empire that holds no one has no standing
    /// (0.0), not a vacuous perfect score. Not a second happiness and not a mood aura: happiness is
    /// a settlement's material condition, legitimacy an Empire's standing.
    /// </summary>
    public static double Legitimacy(IReadOnlyWorldState world, PolityId polity, SimConfig cfg)
    {
        double weighted = 0.0;
        long people = 0;

        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId place = world.Settlements[s].Id;
            if (!EmpireQuery.ControlsSettlement(world, polity, place)) continue;

            long pop = 0;
            for (int b = 0; b < world.Buckets.Count; b++)
                if (world.Buckets[b].Settlement.Value == place.Value) pop += world.Buckets[b].Count.Value;
            if (pop <= 0) continue;

            weighted += SettlementHappiness.Of(world, place, cfg) * pop;
            people += pop;
        }

        if (people <= 0) return 0.0;
        return Math.Clamp(weighted / people, 0.0, SettlementHappiness.Max);
    }

    /// <summary>
    /// THE TAX EDICT'S AVAILABILITY PREDICATE (ADR-033 D4: taxation is research-gated by the content; H2, Director
    /// 2026-10-05 §7: the capability is operational only from its Age). True iff <see cref="GateOf"/> is
    /// <see cref="TaxGate.Open"/> — the config carries a governance section AND research content, the polity's
    /// completed knowledge satisfies sim.json <c>governance.taxationRequires</c> (R5: the single Civics node
    /// <c>taxation</c>, key 1007), AND the polity's CURRENT Age is at least sim.json <c>governance.taxationMinAge</c>
    /// (A3, the Bronze Age). Both halves live in content; no node id and no Age number is named in C#.
    ///
    /// RESEARCH IS NOT AGE-GATED, THE CAPABILITY IS. A polity still in A1 or A2 may complete the Taxation civic (the
    /// graph reaches it) and holds that knowledge for ever, but the edict is refused until the polity ENTERS the
    /// minimum Age — and an Age is entered only by the AdvanceAge order on eligibility (ADR-031), so this is computed
    /// state, never a date (law 4).
    ///
    /// ONE PREDICATE, EVERY CALLER: GovernanceSystem applies a SetTaxRate only when this holds on PREV (so a
    /// hand-built order, a replayed log and a loaded save meet it exactly as a live click does); the AI valve acts
    /// only when it holds; the UI emitter refuses when it does not; and the available-actions query asks this same
    /// function. The AI's RESEARCH goal reads only the knowledge half (<see cref="KnowsTaxation"/>), because the Age
    /// half is not researchable — chasing the other OR-branches of a requirement it already meets would waste research.
    /// </summary>
    public static bool CanLevyTax(IReadOnlyWorldState world, SimConfig cfg, PolityId polity) =>
        GateOf(world, cfg, polity) == TaxGate.Open;

    /// <summary>Where <paramref name="polity"/> stands at the tax gate, first unmet condition first: the loop is
    /// inert (no governance or research content), the Taxation knowledge is missing, the minimum Age is not yet
    /// entered, or the edict is open. The UI states the reason with it; <see cref="CanLevyTax"/> is "== Open".</summary>
    public static TaxGate GateOf(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Governance is null || cfg.Research is null) return TaxGate.Inert;
        if (!KnowsTaxation(world, cfg, polity)) return TaxGate.NeedsKnowledge;
        if (!MeetsTaxationAge(world, cfg, polity)) return TaxGate.NeedsAge;
        return TaxGate.Open;
    }

    /// <summary>
    /// THE KNOWLEDGE HALF of the gate: the polity's completed knowledge satisfies sim.json
    /// <c>governance.taxationRequires</c>, parsed against the attached research content
    /// (<see cref="ResearchContentLoader.ParseRequirement"/>; the four-stream load validates it once) and evaluated by
    /// the existing knowledge evaluator (<see cref="ResearchQuery.RequirementMet"/>). False without governance or
    /// research content.
    /// </summary>
    public static bool KnowsTaxation(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Governance is not { } governance || cfg.Research is not { } research) return false;
        Predicate requirement = ResearchContentLoader.ParseRequirement(
            research, governance.TaxationRequires, "sim.json governance.taxationRequires");
        return ResearchQuery.RequirementMet(research, requirement, ResearchQuery.CompletedMask(world, research, polity));
    }

    /// <summary>
    /// THE AGE HALF of the gate: true when sim.json <c>governance.taxationMinAge</c> is absent (no Age requirement),
    /// else when the polity's CURRENT Age (<see cref="AgeQuery.CurrentAge"/>; no Age row = the founding Age) is at
    /// least it. A declared minimum with NO Age content attached cannot be shown to hold and FAILS CLOSED — a world
    /// without Ages has no Bronze Age to have entered.
    /// </summary>
    public static bool MeetsTaxationAge(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Governance?.TaxationMinAge is not { } minAge) return true;
        if (cfg.Ages is not { } ages) return false;
        return AgeQuery.CurrentAge(world, ages, polity) >= minAge;
    }

    /// <summary>
    /// THE ONE CONSTRUCTOR of the tax edict, used by the player's factory and the AI valve alike:
    /// the issuing Empire legislates ITS OWN rate (TargetId = issuer, which OrderValidation
    /// requires), Amount = the percentage as given (the system converts it to a fraction).
    /// </summary>
    public static OrderRecord TaxOrder(long turn, PolityId issuer, double percent)
        => OrderRecord.From(turn, issuer, OrderKind.SetTaxRate, issuer.Value, percent);
}

/// <summary>Where a polity stands at the tax gate (<see cref="Governance.GateOf"/>), first unmet condition first.</summary>
public enum TaxGate
{
    /// <summary>The config carries no governance or no research content: the governing loop is inert.</summary>
    Inert = 0,
    /// <summary>The Taxation knowledge (sim.json governance.taxationRequires) is not complete.</summary>
    NeedsKnowledge = 1,
    /// <summary>The knowledge is complete but the polity has not entered sim.json governance.taxationMinAge.</summary>
    NeedsAge = 2,
    /// <summary>The edict is operational.</summary>
    Open = 3,
}
