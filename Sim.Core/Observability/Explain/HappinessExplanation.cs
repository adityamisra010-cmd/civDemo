using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Core.Observability.Explain;

/// <summary>One happiness factor with the §5 chain under it.</summary>
public readonly record struct HappinessFactor(
    SettlementHappiness.Factor Factor, string Name, double Value, Link[] Chain);

/// <summary>
/// T4.19 — WHY THIS SETTLEMENT IS (UN)HAPPY (docs/observability-architecture.md §6).
///
/// Happiness is a DERIVED reading, never a stock (SettlementHappiness.cs:6-25),
/// so the explanation asks the same public reader the migration system asks —
/// <see cref="SettlementHappiness.Of"/> for the score and
/// <see cref="SettlementHappiness.Factors"/> for its two inputs — and then
/// hangs the §5 cause chains under each factor: the food-supply block under
/// Food (1 − DeficitRatio, SettlementHappiness.cs:118-129) and the
/// housing-supply block under Housing (dwellings × PersonsPerDwelling /
/// population, SettlementHappiness.cs:138-159). Same blocks, same world, so
/// happiness and the needs explanation cannot name different causes for the
/// same shortfall. The blocks are told that world's identity —
/// <see cref="SourceWorld.Next"/>, the one world this query was asked about —
/// and stamp it on every link, so a reader sees "next ConsumptionDeficits"
/// here and "prev ConsumptionDeficits" under a grievance for the same
/// settlement and turn: two different values, each labelled with the table it
/// was actually read from (A2-LABEL).
///
/// WHAT IT DELIBERATELY IS NOT: the needs aggregate. Happiness omits Comfort
/// and applies no Tier-A gate, BY DESIGN — it must not read the needs tables
/// because it feeds migration, a behaviour, and D-021 defers needs-driven
/// behaviour to M5 (SettlementHappiness.cs:27-38). The explanation states this
/// (<see cref="ScopeNote"/>) so the two numbers are never read as one.
///
/// ADR-033 D4 — THE TAX BURDEN IS AN EXPLAINED CAUSE. The M5 governing loop multiplies
/// the normalised CES reading by <see cref="SettlementHappiness.TaxSufficiency"/>
/// (1 − the effective tax rate), so the two provision factors no longer explain the
/// score on their own. <see cref="Burden"/> carries that multiplier and the two stored
/// facts it is computed from (the declared rate and ControlRow.Strength, the stored
/// reach), through the SAME constructor the settlement record uses
/// (<see cref="TaxBurdenReading.Of"/>). It is not a CES factor and is not in
/// <see cref="Factors"/>: a burden on provision, not a provision.
/// </summary>
public sealed class HappinessExplanation
{
    public const string ScopeNote =
        "Happiness reads Food (1 − DeficitRatio) and Housing (dwellings × PersonsPerDwelling / population) as its two "
        + "provision factors, then MULTIPLIES the normalised reading by the M5 tax burden (TaxSufficiency = 1 − declared "
        + "tax rate × ControlRow.Strength, the stored administrative reach; exactly 1 when untaxed). "
        + "Comfort and the Tier-A gate are ABSENT by design: happiness feeds migration, and D-021 forbids the needs "
        + "tables from driving behaviour before M5 (SettlementHappiness.cs:27-38). It is deliberately not the same "
        + "number as the needs aggregate that accrues grievance.";

    /// <summary>How the burden line reads, and what moves it.</summary>
    public const string BurdenNote =
        "The burden multiplies the whole reading (it is not a third factor, so total deprivation still reads 0 at any "
        + "rate). Effective rate = the controller's declared rate (SetTaxRate, the lever) × the stored reach "
        + "(ControlRow.Strength = exp(−travel cost from the capital / authorityDecayCostUnits), written by "
        + "GovernanceSystem from the previous turn's road-aware distances). Untaxed, uncontrolled, or no governance "
        + "section: scale 1.";

    public SettlementId Settlement { get; }
    /// <summary>RECOMPUTED SettlementHappiness.Of, on [0, 100].</summary>
    public double Happiness { get; }
    /// <summary>RECOMPUTED SettlementHappiness.Factors, in Factor order, each with its chain.</summary>
    public HappinessFactor[] Factors { get; }
    /// <summary>ADR-033 D4: RECOMPUTED tax burden — the multiplier on the reading, with its two
    /// stored inputs (<see cref="TaxBurdenReading.Of"/>, the settlement record's constructor).</summary>
    public TaxBurdenReading Burden { get; }

    private HappinessExplanation(SettlementId settlement, double happiness, HappinessFactor[] factors, TaxBurdenReading burden)
    {
        Settlement = settlement;
        Happiness = happiness;
        Factors = factors;
        Burden = burden;
    }

    public static HappinessExplanation For(IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);

        double happiness = SettlementHappiness.Of(world, settlement, cfg);
        Span<double> values = stackalloc double[SettlementHappiness.FactorCount];
        SettlementHappiness.Factors(world, settlement, cfg, values);

        var food = new List<Link>();
        CausalChain.FoodSupply(world, SourceWorld.Next, cfg, settlement, food);
        var housing = new List<Link>();
        CausalChain.HousingSupply(world, SourceWorld.Next, null, cfg, settlement, housing);

        var factors = new HappinessFactor[SettlementHappiness.FactorCount];
        factors[(int)SettlementHappiness.Factor.Food] = new HappinessFactor(
            SettlementHappiness.Factor.Food, "Food", values[(int)SettlementHappiness.Factor.Food], [.. food]);
        factors[(int)SettlementHappiness.Factor.Housing] = new HappinessFactor(
            SettlementHappiness.Factor.Housing, "Housing", values[(int)SettlementHappiness.Factor.Housing], [.. housing]);

        return new HappinessExplanation(settlement, happiness, factors, TaxBurdenReading.Of(world, cfg, settlement));
    }
}
