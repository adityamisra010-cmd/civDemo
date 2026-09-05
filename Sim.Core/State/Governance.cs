using Sim.Core.Systems;

namespace Sim.Core.State;

/// <summary>
/// M5 — THE GOVERNING LOOP'S READERS. Pure derived queries over authoritative
/// state: nothing here mutates, nothing here is stored, and no system calls
/// another system to get an answer.
///
/// THE LOOP THESE READERS CLOSE, and every arrow is a real consumer rather than a
/// displayed number:
///
///   tax policy (an Empire's standing decision, order-set)
///     → administrative REACH decides what the state can actually collect
///     → EFFECTIVE extraction raises production output           (economic effect)
///     → the same effective burden lowers HAPPINESS               (social cost)
///     → happiness drives migration and, at zero, revolt          (D-021 valves)
///     → losing settlements changes what the Empire controls
///     → LEGITIMACY, read off the condition of what it still holds
///     → which feeds back into what the next policy can achieve
///
/// WHAT M5's TAX IS NOT. It moves no goods, holds no stock, and has no recipient.
/// There is no treasury, no receipt and no transfer: goods stay physically
/// localized in <see cref="GoodStockRow"/>, and economic ownership stays derived
/// through <see cref="ControlRow"/>. A tax here is a POLICY that changes how hard
/// a realm is worked and what that costs the people working it.
/// </summary>
public static class Governance
{
    /// <summary>
    /// What an Empire has DECLARED it will take, as a fraction in [0,1].
    ///
    /// Absence of a row is the never-ordered default of ZERO — an Empire that has
    /// never legislated a tax levies none. That is the same convention
    /// <see cref="SectorAllocationRow"/> uses, and it is what lets a world founded
    /// before M5 behave exactly as it did.
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

    /// <summary>
    /// ADMINISTRATIVE REACH — how much of its declared tax the state can actually
    /// bring in from this settlement, in (0, 1].
    ///
    /// THIS IS AUTHORITY, AND IT IS THE REASON TAXATION IS NOT A SLIDER. A rate is
    /// what the state asks for; reach is what its officials, roads and garrisons
    /// can actually enforce at that distance. A capital collects in full from its
    /// own streets and poorly from the far frontier, so an Empire that sprawls
    /// discovers that declaring a higher rate does not straightforwardly collect
    /// more — which is the governing constraint the milestone exists to model.
    ///
    /// DENOMINATED ON THE SHIPPED NETWORK, NOT ON A NEW GEOMETRY. It is
    /// <c>exp(−travelCost / decay)</c> over the SAME `SettlementDistances` travel
    /// cost migration already uses, with the SAME functional form migration's
    /// damping uses (`MigrationSystem`: <c>exp(−travelCost / DampingDecayCostUnits)</c>)
    /// and, by default, the same 25-cost-unit e-fold. That is deliberate and is
    /// the constant's whole warrant: both quantities answer "how far does
    /// influence travel over this network", so inventing a second, unrelated
    /// distance scale for administration would be asserting a magnitude nobody
    /// measured. This also satisfies D-040 C3 — control carries a distance term
    /// over the NETWORK GRAPH, travel cost and not Euclidean distance.
    ///
    /// THE CAPITAL IS THE ORIGIN, and that is the only governance privilege it
    /// gets: it is the seat administration radiates from, not a store, not a sink,
    /// and not an owner. Reach at the capital itself is exactly 1.0 (cost 0).
    ///
    /// EDGE CASES, all meaningful rather than defensive: an Empire with NO capital
    /// has no seat to administer from and reaches nothing (0.0) — capital loss has
    /// a real fiscal consequence. An UNREACHABLE settlement (infinite travel cost)
    /// reaches 0.0 by construction, not by branch, exactly as migration's damping
    /// does. A settlement with no distance row to the capital is treated as
    /// unreachable rather than adjacent, because missing data must not read as
    /// perfect administration.
    /// </summary>
    public static double AdministrativeReach(
        IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (!EmpireQuery.TryGetController(world, settlement, out PolityId polity)) return 0.0;
        if (!EmpireQuery.TryGetCapital(world, polity, out SettlementId seat)) return 0.0;
        if (seat.Value == settlement.Value) return 1.0;   // the seat administers itself in full

        double decay = cfg.Governance?.AuthorityDecayCostUnits
            ?? throw new ArgumentException(
                "SimConfig.Governance is not loaded — administrative reach reads its decay scale.",
                nameof(cfg));
        if (!(decay > 0.0)) return 0.0;

        for (int i = 0; i < world.SettlementDistances.Count; i++)
        {
            SettlementDistanceRow row = world.SettlementDistances[i];
            if (row.From.Value != seat.Value || row.To.Value != settlement.Value) continue;
            double cost = row.TravelCost;
            if (double.IsNaN(cost)) return 0.0;
            return Math.Clamp(Math.Exp(-cost / decay), 0.0, 1.0);
        }

        return 0.0;   // no route on record: unadministered, not adjacent
    }

    /// <summary>
    /// What the state ACTUALLY extracts here: the declared rate scaled by what its
    /// administration can reach. This is the single number every downstream
    /// consumer uses — production reads it as effort compelled, happiness reads it
    /// as burden borne — so the economic gain and the social cost can never drift
    /// apart or be tuned against each other.
    /// </summary>
    public static double EffectiveTaxRate(
        IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        if (!EmpireQuery.TryGetController(world, settlement, out PolityId polity)) return 0.0;
        double nominal = NominalTaxRate(world, polity);
        if (nominal <= 0.0) return 0.0;   // the common case, and it costs nothing
        return Math.Clamp(nominal * AdministrativeReach(world, settlement, cfg), 0.0, 1.0);
    }

    /// <summary>
    /// The multiplier M5 applies to a settlement's realised production.
    ///
    /// <c>1 + response × effectiveRate</c>: a state that taxes harder also works
    /// its realm harder — corvée, tribute quotas and levied labour — so extraction
    /// RAISES output while costing the population. The ceiling is denominated
    /// against the shipped tool bonus (`farming.toolYieldBonusMax`), which is the
    /// tree's existing statement of how much a multiplier may add to production;
    /// full extraction is worth at most what fully equipping the settlement is
    /// worth. Untaxed is exactly 1.0, so an untaxed world produces bit-identically
    /// to M4.
    /// </summary>
    public static double ExtractionMultiplier(
        IReadOnlyWorldState world, SettlementId settlement, SimConfig cfg)
    {
        double rate = EffectiveTaxRate(world, settlement, cfg);
        if (rate <= 0.0) return 1.0;
        double response = cfg.Governance?.TaxExtractionResponseMax ?? 0.0;
        return 1.0 + response * rate;
    }

    /// <summary>
    /// LEGITIMACY — how well an Empire is regarded by the people it actually
    /// holds, on the same 0..100 scale as happiness so the two are comparable at
    /// a glance.
    ///
    /// DERIVED, NOT STORED, and deliberately so: legitimacy is a reading of the
    /// realm's present condition, not a stock that can be spent, granted or
    /// decayed independently of it. It is the POPULATION-WEIGHTED mean happiness
    /// of the settlements the Empire controls — a state is judged by how its
    /// subjects actually live, and a large miserable city counts for more than a
    /// contented hamlet.
    ///
    /// AN EMPIRE THAT HOLDS NOTHING HAS NO STANDING (0.0) rather than a vacuous
    /// perfect score, which matters because <see cref="EmpireQuery.IsExtinct"/>
    /// makes that state reachable.
    ///
    /// It is NOT a second happiness and NOT a mood aura. Happiness is a
    /// SETTLEMENT's material condition; legitimacy is an EMPIRE's standing, and it
    /// exists because governance decisions are taken at the Empire level and must
    /// be answerable at that level.
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
}
