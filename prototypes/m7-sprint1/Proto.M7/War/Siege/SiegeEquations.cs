using Proto.M7.Fixed;
using Proto.M7.Rng;
using Proto.M7.Time;
using Proto.M7.WarEcon;

namespace Proto.M7.War.Siege;

/// <summary>The defending side as the equations see it (aggregated from garrisoned formations; people by arm). The ranged arm is
/// split by what it shoots: <c>Bow*</c> = non-gunpowder weapons (missiles, shooter-days), <c>Gun*</c> = firearms and rifles
/// (lane E munitions, kg); manned fortress guns also fire munitions.</summary>
/// <param name="Bowmen">Shooters with a non-gunpowder weapon (RangedKind.Bow).</param>
/// <param name="BowValueMilli">Σ bowmen × RangedKindFactor‰ (people-equivalents × 1000).</param>
/// <param name="BowLethalityMilliPerYear">Σ bowmen × FireLethality‰/yr.</param>
/// <param name="Gunmen">Shooters with a gunpowder weapon (Firearm, Rifle).</param>
/// <param name="GunmenValueMilli">Σ gunmen × RangedKindFactor‰.</param>
/// <param name="GunmenLethalityMilliPerYear">Σ gunmen × FireLethality‰/yr.</param>
/// <param name="GunsManned">Installation guns that the garrison's artillery crews can man: min(guns, crews / crew per gun).</param>
public readonly record struct DefenderForce(long Melee, long Cavalry, long Engineers, long Bowmen, long BowValueMilli, long BowLethalityMilliPerYear,
                                            long Gunmen, long GunmenValueMilli, long GunmenLethalityMilliPerYear, long GunsManned)
{
    public long Ranged => checked(Bowmen + Gunmen);
    public long Fighting => checked(Melee + Ranged + Cavalry + Engineers);
}

/// <summary>Inputs and outputs of one assault, relief battle, sally or raid (the land-module contract seam).</summary>
public readonly record struct AssaultInputs(long AttackPowerMilli, long DefencePowerMilli, ulong Seed, long SiegeId, long AtMs, int Purpose);

public readonly record struct AssaultResult(bool Success, Q32 Odds, Q32 Fortune, Q32 AttackerLossShare, Q32 DefenderLossShare);

/// <summary>
/// The battle-resolution seam. Siege never duplicates combat resolution (C15, ADR-060 §2.5): an assault is a land battle.
/// In integration this is the land module (BattleResolution.Resolve on a BattleSetup built from these inputs). The prototype
/// land module is entity-capped (50-60 per formation, m7-spec.md:395) and has no wall/breach terrain, so the prototype ships
/// <see cref="FormationAssaultStandIn"/>: a formation-level odds model, labelled as a STAND-IN.
/// </summary>
public interface IAssaultResolver
{
    AssaultResult Resolve(in AssaultInputs input, SiegeCoefficients k);
}

/// <summary>
/// STAND-IN for the land module (see <see cref="IAssaultResolver"/>). Odds R = attack / defence; fortune F ~ U[1 − f, 1 + f] by
/// counter RNG keyed on (siege, instant, purpose) — never on a sequence (EventRules R5); success with probability
/// p = (R F)² / (1 + (R F)²) (second counter draw). Loss shares: attacker min(cap_a, c_a / (R F)), defender min(cap_d, c_d × R F).
/// PLACEHOLDER coefficients (SiegeCoefficients).
/// </summary>
public sealed class FormationAssaultStandIn : IAssaultResolver
{
    public static readonly FormationAssaultStandIn Instance = new();
    public const uint Stream = 0x5E6E_0001;

    public AssaultResult Resolve(in AssaultInputs x, SiegeCoefficients k)
    {
        Q32 fortune = Fortune(x.Seed, x.SiegeId, x.AtMs, x.Purpose, k.FortunePermille);
        if (x.AttackPowerMilli <= 0)
            return new AssaultResult(false, Q32.Zero, fortune, Q.Permille(k.AssaultAttackerLossCapPermille), Q32.Zero);
        if (x.DefencePowerMilli <= 0)
            return new AssaultResult(true, Q32.FromRatio(1000, 1), fortune, Q32.Zero, Q.Permille(k.AssaultDefenderLossCapPermille));
        // Odds are capped at 1000:1 so every product below stays inside long.
        long a = Math.Min(x.AttackPowerMilli, checked(x.DefencePowerMilli * 1000));
        Q32 odds = Q32.FromRatio(a, x.DefencePowerMilli);
        Q32 rf = odds.Mul(fortune);
        if (rf.Raw <= 0) rf = new Q32(1);
        // Success is uncertain even at good odds: p = R²/(1 + R²) (R = odds × fortune), drawn by counter RNG (index 1).
        Q32 r2 = rf.Mul(rf);
        Q32 p = new(FixedMath.MulDivFloor(r2.Raw, Q32.OneRaw, checked(Q32.OneRaw + r2.Raw)));
        bool success = CounterRng.Chance(x.Seed, Stream, x.SiegeId, x.AtMs, x.Purpose, 1, Q.Clamp01(p));
        Q32 lossA = Q.Min(Q.Permille(k.AssaultAttackerLossCapPermille), new Q32(FixedMath.MulDivFloor(Q.Permille(k.AssaultAttackerLossPermille).Raw, Q32.OneRaw, rf.Raw)));
        Q32 lossD = Q.Min(Q.Permille(k.AssaultDefenderLossCapPermille), Q.Permille(k.AssaultDefenderLossPermille).Mul(rf));
        return new AssaultResult(success, odds, fortune, lossA, lossD);
    }

    /// <summary>F = (1 − f) + 2f × u, u = top 32 bits of a counter draw / 2^32. In [1 − f, 1 + f).</summary>
    public static Q32 Fortune(ulong seed, long siegeId, long atMs, int purpose, long fortunePermille)
    {
        ulong u = CounterRng.Draw(seed, Stream, siegeId, atMs, purpose, 0) >> 32;
        long raw = checked(Q.Permille(1000 - fortunePermille).Raw + FixedMath.MulDivFloor(2 * fortunePermille, (long)u, 1000));
        return new Q32(raw);
    }
}

/// <summary>
/// The per-pulse siege equations (DIR5 §2.2; C15 delegated). Pure integer functions of explicit inputs; every rate is per
/// sim-year and is integrated over the pulse length dt (Law 3) with an unconditionally stable implicit or semi-implicit
/// step, so the result does not blow up for long pulses (21-day War Pulses) and converges as the pulse shrinks (60-second
/// battle pulses; re-measured in fix r1 once the head-loss carry was made exact, reports/H-S.md §5). Nothing here reads a date or an era label (Law 4): the gunpowder transition is carried entirely by the
/// engine mix (EngineDamage × Vulnerability) and the fortification kind, which are computed state.
/// </summary>
public static class SiegeEquations
{
    // ------------------------------------------------------------------ fortification

    /// <summary>
    /// fortFactor = 1 + wallValue(kind) × W² + fieldValue × E (a coefficient on the defence, Law 2). Always ≥ 1. The masonry term
    /// is convex in integrity: an intact circuit forces an escalade (near hopeless against a manned wall), while the first breaches
    /// already open it to assault — most of the value is lost in the first half of the damage.
    /// </summary>
    public static Q32 FortFactor(SiegeCoefficients k, FortKind kind, Q32 wall, Q32 fieldWorks)
    {
        Q32 wc = Q.Clamp01(wall);
        Q32 w = Q.Permille(k.WallValuePermille[(int)kind]).Mul(wc.Mul(wc));
        Q32 e = Q.Permille(k.FieldWorksValuePermille).Mul(Q.Clamp01(fieldWorks));
        return Q.Add(Q.Add(Q32.One, w), e);
    }

    /// <summary>True for the engine classes that fire gunpowder (they draw lane E munitions; Gunpowder capability).</summary>
    public static bool IsGunpowder(EngineClass e) => e is EngineClass.Bombard or EngineClass.ModernGun;

    /// <summary>True for the ranged weapons that fire gunpowder.</summary>
    public static bool IsGunpowder(RangedKind r) => r is RangedKind.Firearm or RangedKind.Rifle;

    /// <summary>Wall damage per sim-year (Q32 integrity/yr) from the engine mix: Σ engines × damage × vulnerability(fort, engine)
    /// × (powder and shot met, for gunpowder engines), all × the crews' fighting state (hunger, sickness). Rams and towers count
    /// only once the approach works are complete.</summary>
    public static Q32 WallDamagePerYear(SiegeCoefficients k, FortKind kind, long[] engines, bool approachComplete, Q32 crews, Q32 munitionsMet)
    {
        if (kind == FortKind.None) return Q32.Zero;
        long other = 0, powder = 0;
        for (int e = 0; e < 4; e++)
        {
            if (e == (int)EngineClass.RamTower && !approachComplete) continue;
            long d = checked(engines[e] * k.EngineDamagePermillePerYear[e] * k.VulnerabilityPermille[(int)kind * 4 + e] / 1000);
            if (IsGunpowder((EngineClass)e)) powder = checked(powder + d); else other = checked(other + d);
        }
        return Q.Add(Q.Permille(other), Q.Permille(powder).Mul(Q.Clamp01(munitionsMet))).Mul(Q.Clamp01(crews));
    }

    /// <summary>Damage rate to earthworks per sim-year (Σ engines × EarthDamage, gunpowder engines × munitions met) × crews.</summary>
    public static Q32 EarthDamagePerYear(SiegeCoefficients k, long[] engines, Q32 crews, Q32 munitionsMet)
    {
        long other = 0, powder = 0;
        for (int e = 1; e < 4; e++)
        {
            long d = checked(engines[e] * k.EarthDamagePermillePerYear[e]);
            if (IsGunpowder((EngineClass)e)) powder = checked(powder + d); else other = checked(other + d);
        }
        return Q.Add(Q.Permille(other), Q.Permille(powder).Mul(Q.Clamp01(munitionsMet))).Mul(Q.Clamp01(crews));
    }

    /// <summary>
    /// Masonry over one step: dW/dt = −d + ρL(1 − W), repair implicit: W' = (W − d·dt + ρL·dt) / (1 + ρL·dt), clamped to [0, 1].
    /// d = WallDamagePerYear; ρ = RepairMax(kind); L = defender labour factor in [0, 1].
    /// </summary>
    public static Q32 WallStep(SiegeCoefficients k, FortKind kind, Q32 wall, Q32 damagePerYear, Q32 labour, long dtMs)
    {
        if (kind == FortKind.None) return Q32.Zero;
        Q32 dDt = new(FixedMath.MulDivFloor(damagePerYear.Raw, dtMs, SimInstant.MsPerYear));
        Q32 rDt = Q.OverDt(k.RepairMaxPermillePerYear[(int)kind], dtMs).Mul(Q.Clamp01(labour));
        long num = checked(wall.Raw - dDt.Raw + rDt.Raw);
        if (num <= 0) return Q32.Zero;
        return Q.Clamp01(new Q32(FixedMath.MulDivFloor(num, Q32.OneRaw, checked(Q32.OneRaw + rDt.Raw))));
    }

    /// <summary>Field works: dE/dt = b·L·(1 − E) − e·E (both implicit): E' = (E + bL·dt) / (1 + bL·dt + e·dt).</summary>
    public static Q32 FieldWorksStep(SiegeCoefficients k, Q32 works, Q32 earthDamagePerYear, Q32 labour, long dtMs)
    {
        Q32 bDt = Q.OverDt(k.FieldWorksBuildPermillePerYear, dtMs).Mul(Q.Clamp01(labour));
        Q32 eDt = new(FixedMath.MulDivFloor(earthDamagePerYear.Raw, dtMs, SimInstant.MsPerYear));
        return Q.Clamp01(new Q32(FixedMath.MulDivFloor(checked(works.Raw + bDt.Raw), Q32.OneRaw, checked(Q32.OneRaw + bDt.Raw + eDt.Raw))));
    }

    /// <summary>Mining progress per step (one unit = one successful mine): MineMax × engA/(engA + half) × (1 − counter) × capability × dt,
    /// counter = CounterMax × engD/(engD + counterHalf). Saturating in both: frontage limits the galleries; listening posts need few men.</summary>
    public static Q32 MiningProgress(SiegeCoefficients k, long engineersA, long engineersD, Q32 attackerCapability, long dtMs)
    {
        if (engineersA <= 0) return Q32.Zero;
        Q32 sat = Q32.FromRatio(engineersA, checked(engineersA + k.MineHalfEngineers));
        Q32 counter = Q.Permille(k.CounterMineMaxPermille).Mul(Q.Ratio(Math.Max(0, engineersD), checked(Math.Max(0, engineersD) + k.CounterMineHalfEngineers)));
        return Q.OverDt(k.MineMaxPermillePerYear, dtMs).Mul(sat).Mul(Q.Sub(Q32.One, counter)).Mul(Q.Clamp01(attackerCapability));
    }

    /// <summary>Approach works progress per step: (engineers + infantry × share) × dt / work (engineer-years).</summary>
    public static Q32 ApproachProgress(SiegeCoefficients k, long engineersA, long meleeA, Q32 attackerCapability, long workEngineerYears, long dtMs)
    {
        if (workEngineerYears <= 0) return Q32.One;
        long workers = checked(engineersA + meleeA * k.InfantryWorkPermille / 1000);
        Q32 p = new(FixedMath.MulDivFloor(checked(workers * dtMs), Q32.OneRaw, checked(SimInstant.MsPerYear * workEngineerYears)));
        return p.Mul(Q.Clamp01(attackerCapability));
    }

    // ------------------------------------------------------------------ nutrition, disease, morale

    /// <summary>ν' = (ν + κ·n) / (1 + κ), κ = dt / τ: body reserves relax toward the ration actually eaten.</summary>
    public static Q32 NutritionStep(SiegeCoefficients k, Q32 nutrition, Q32 eaten, long dtMs)
        => Q.Clamp01(Q.Relax(nutrition, Q.Clamp01(eaten), Q32.FromRatio(dtMs, checked(k.NutritionTauDays * SimInstant.MsPerDay))));

    /// <summary>
    /// Disease prevalence I (share sick), logistic SIS with crowding and malnutrition: dI/dt = β·I·(1 − I) − γ·I, β = β0 × crowding ×
    /// (1 + κ(1 − ν)). Semi-implicit step I' = I(1 + β dt) / (1 + dt(β I + γ)) — stays in [0, 1] for any dt, equilibrium 1 − γ/β.
    /// </summary>
    public static Q32 DiseaseStep(SiegeCoefficients k, Q32 prevalence, Q32 crowding, Q32 nutrition, long dtMs)
    {
        Q32 malnut = Q.Add(Q32.One, Q.Permille(k.DiseaseMalnutritionGainPermille).Mul(Q.Sub(Q32.One, Q.Clamp01(nutrition))));
        Q32 bDt = Q.OverDt(k.DiseaseBetaPermillePerYear, dtMs).Mul(Q.Min(crowding, new Q32(8 * Q32.OneRaw))).Mul(malnut);
        Q32 gDt = Q.OverDt(k.DiseaseRecoveryPermillePerYear, dtMs);
        Q32 i = Q.Clamp01(prevalence);
        long num = i.Mul(Q.Add(Q32.One, bDt)).Raw;
        long den = checked(Q32.OneRaw + bDt.Mul(i).Raw + gDt.Raw);
        Q32 next = new(FixedMath.MulDivFloor(num, Q32.OneRaw, den));
        return Q.Max(Q.Permille(k.DiseaseFloorPermille), Q.Clamp01(next));
    }

    /// <summary>Morale relaxes toward its target: M' = (M + λdt·T) / (1 + λdt).</summary>
    public static Q32 MoraleStep(SiegeCoefficients k, Q32 morale, Q32 target, long dtMs)
        => Q.Clamp01(Q.Relax(morale, Q.Clamp01(target), Q.OverDt(k.MoraleRatePermillePerYear, dtMs)));

    /// <summary>Defender morale target: loyalty × hunger × disease × breach × casualties × hopelessness × unpaid upkeep.</summary>
    public static Q32 DefenderMoraleTarget(SiegeCoefficients k, Q32 loyalty, Q32 nutrition, Q32 disease, bool breach, Q32 garrisonShare,
                                           Q32 enemyShareOfPower, Q32 unpaidShare)
    {
        Q32 t = Q.Clamp01(loyalty);
        t = t.Mul(Q.Penalty(Q.Permille(k.MoraleHungerPermille), nutrition));
        t = t.Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleDiseasePermille).Mul(Q.Clamp01(disease))));
        if (breach) t = t.Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleBreachPermille)));
        t = t.Mul(Q.Penalty(Q.Permille(k.MoraleCasualtyPermille), garrisonShare));
        t = t.Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleOddsPermille).Mul(Q.Clamp01(enemyShareOfPower))));
        t = t.Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleUnpaidPermille).Mul(Q.Clamp01(unpaidShare))));
        return Q.Clamp01(t);
    }

    /// <summary>Besieger morale target: supply × camp disease × casualties × hopelessness.</summary>
    public static Q32 AttackerMoraleTarget(SiegeCoefficients k, Q32 supply, Q32 disease, Q32 forceShare, Q32 enemyShareOfPower)
    {
        Q32 t = Q.Penalty(Q.Permille(k.MoraleSupplyPermille), supply);
        t = t.Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleDiseasePermille).Mul(Q.Clamp01(disease))));
        t = t.Mul(Q.Penalty(Q.Permille(k.MoraleCasualtyPermille), forceShare));
        t = t.Mul(Q.Sub(Q32.One, Q.Permille(k.MoraleOddsPermille).Mul(Q.Clamp01(enemyShareOfPower))));
        return Q.Clamp01(t);
    }

    /// <summary>Capability lost to hunger: f_n = 1 − a_n (1 − ν). Rationing lowers ν toward the ration, so it lowers the defence
    /// coefficient "to some extent" (DIR5 §2.2) through this one mechanism.</summary>
    public static Q32 HungerFactor(SiegeCoefficients k, Q32 nutrition) => Q.Penalty(Q.Permille(k.HungerCapabilityPermille), nutrition);

    /// <summary>Fighting value of morale in a battle: 0.5 + 0.5 M.</summary>
    public static Q32 MoraleFight(Q32 morale) => Q.Add(new Q32(Q32.OneRaw / 2), new Q32(Q.Clamp01(morale).Raw / 2));

    // ------------------------------------------------------------------ per-head loss RATES (‰ per sim-year; integrated by lane E's
    // HeadcountLoss, per elapsed day on the declining strength, competing flows sharing one survival product)

    /// <summary>One flow's share of the loss over a pulse, in units of 1/<see cref="HeadcountLoss.One"/> of a head (10⁻¹⁸):
    /// heads × (1 − survival) × rate / totalRate (floor), on lane E's survival product (<see cref="HeadcountLoss.Survival"/>). The
    /// world adds it to a carry in the same unit and moves the whole heads, so losses smaller than a thousandth of a head per pulse
    /// (60-second pulses) accumulate instead of being floored away (fix r1, S-V1; lane E's <see cref="HeadcountLoss.LossMilli"/>
    /// floors to thousandths, which is exact enough on War Pulses but not on battle pulses). The flows of one survival product never
    /// sum to more than heads × One.</summary>
    public static Int128 LossAtto(long heads, long permilleYear, long totalPermilleYear, Int128 survival)
    {
        if (heads < 0 || permilleYear < 0 || permilleYear > totalPermilleYear || survival < 0 || survival > HeadcountLoss.One)
            throw new ArgumentOutOfRangeException(nameof(heads), "invalid loss input");
        if (heads == 0 || permilleYear == 0) return 0;
        return checked((Int128)heads * (HeadcountLoss.One - survival) * permilleYear / totalPermilleYear);
    }

    /// <summary>Starvation (the food-system stand-in, D-039 D5): μ = μmax × max(0, ν_t − ν)/ν_t ‰/yr on the body-reserve state ν.
    /// NOT lane E's immediate-shortfall rate: a besieged city rations deliberately and lives on its reserves for weeks (τ).</summary>
    public static long StarvationPermilleYear(SiegeCoefficients k, Q32 nutrition)
        => DeficitRate(k.StarvationMaxPermillePerYear, k.StarvationThresholdPermille, nutrition);

    /// <summary>Dehydration: as starvation, on the water actually drunk this pulse (no reserve smoothing: thirst is acute).</summary>
    public static long ThirstPermilleYear(SiegeCoefficients k, Q32 waterMet)
        => DeficitRate(k.ThirstMaxPermillePerYear, k.ThirstThresholdPermille, waterMet);

    private static long DeficitRate(long maxPermillePerYear, long thresholdPermille, Q32 level)
    {
        Q32 th = Q.Permille(thresholdPermille);
        if (level >= th) return 0;
        Q32 deficit = new(FixedMath.MulDivFloor(Q.Sub(th, Q.Clamp01(level)).Raw, Q32.OneRaw, th.Raw));
        return deficit.Scale(maxPermillePerYear);
    }

    /// <summary>The share of the full ration not met, in ‰, from the body-reserve state (lane E's "hunger" input), rounded to the
    /// nearest ‰ (a floor would bias every non-dyadic state toward hunger).</summary>
    public static long HungerPermille(Q32 nutrition) => 1000 - RoundTo(nutrition, 1000);

    /// <summary>A fraction in [0, 1] on an integer scale, rounded to the nearest unit (half up).</summary>
    public static long RoundTo(Q32 fraction, long scale)
        => (long)(((Int128)Q.Clamp01(fraction).Raw * scale + Q32.OneRaw / 2) / Q32.OneRaw);

    /// <summary>
    /// Disease deaths ‰/yr: lane E's camp-disease mortality at the background prevalence (<see cref="TroopLossRates.DiseasePermilleYear"/>:
    /// base × (1 + hunger factor × hunger), × Sanitation) scaled by the prevalence over the background: m = m_E × I / I_floor.
    /// At the floor the siege kills exactly what lane E's army economy kills; crowding and hunger raise I and so the deaths.
    /// </summary>
    public static long DiseaseDeathPermilleYear(SiegeCoefficients k, LogisticsRates r, Q32 prevalence, Q32 nutrition, bool sanitation)
    {
        long baseRate = TroopLossRates.DiseasePermilleYear(r, HungerPermille(nutrition), 0, sanitation);
        return FixedMath.MulDivFloor(baseRate, Q.Clamp01(prevalence).Raw, Q.Permille(k.DiseaseFloorPermille).Raw);
    }

    /// <summary>Desertion ‰/yr: lane E's rate (<see cref="TroopLossRates.DesertionPermilleYear"/>) on this side's morale, the age of its
    /// pay arrears and its hunger.</summary>
    public static long DesertionPermilleYear(LogisticsRates r, Q32 morale, long arrearsMonths, Q32 nutrition)
        => TroopLossRates.DesertionPermilleYear(r, RoundTo(morale, 1_000_000), arrearsMonths, HungerPermille(nutrition), false);

    /// <summary>Aimed-fire attrition rate ‰/yr per person: (casualties/yr) / (population × protection), protection ≥ 1.
    /// <paramref name="casualtiesMilliPerYear"/> is casualties per sim-year × 1000.</summary>
    public static long FirePermilleYear(long casualtiesMilliPerYear, Q32 protection, long population)
    {
        if (casualtiesMilliPerYear <= 0 || population <= 0) return 0;
        Q32 prot = protection.Raw < Q32.OneRaw ? Q32.One : protection;
        return (long)((Int128)casualtiesMilliPerYear * Q32.OneRaw / ((Int128)population * prot.Raw));
    }

    /// <summary>Attrition of a stock of machines (not people) over one step: x = expected losses / (count × protection), share x/(1+x).</summary>
    public static Q32 FireShare(long casualtiesMilliPerYear, Q32 protection, long population, long dtMs)
    {
        if (casualtiesMilliPerYear <= 0 || population <= 0) return Q32.Zero;
        Q32 expected = new(FixedMath.MulDivFloor(checked(casualtiesMilliPerYear * dtMs), Q32.OneRaw, 1000 * SimInstant.MsPerYear));
        Q32 prot = protection.Raw < Q32.OneRaw ? Q32.One : protection;
        long denom = checked(population * prot.Raw);
        Q32 x = new(FixedMath.MulDivFloor(expected.Raw, Q32.OneRaw, denom));
        return Q.ImplicitShare(x);
    }

    // ------------------------------------------------------------------ strength

    /// <summary>
    /// Defensive capability D (people-equivalents × 1000): [melee, cavalry, engineers by DefenceValue + bowmen by weapon factor ×
    /// missiles met + gunmen by weapon factor and manned guns × gun value, × munitions met] × (1 − sick) × hunger factor ×
    /// fortFactor. A shooter without ammunition still fights hand to hand (DryRanged). Morale is NOT in D (it is the other
    /// factor of siege health and enters battles separately), so nothing is counted twice.
    /// </summary>
    public static long Capability(SiegeCoefficients k, in DefenderForce d, Q32 missilesMet, Q32 munitionsMet, Q32 disease, Q32 nutrition, Q32 fortFactor)
    {
        long baseMilli = checked(d.Melee * k.DefenceValuePermille[0] + d.Cavalry * k.DefenceValuePermille[2] + d.Engineers * k.DefenceValuePermille[3]);
        Q32 ms = Q.Clamp01(missilesMet), mu = Q.Clamp01(munitionsMet);
        long bowFire = checked(d.BowValueMilli * k.DefenceValuePermille[1] / 1000);
        long gunFire = checked(d.GunmenValueMilli * k.DefenceValuePermille[1] / 1000 + d.GunsManned * k.DefenderGunValuePermille);
        long bowDry = checked(d.Bowmen * k.DryRangedPermille), gunDry = checked(d.Gunmen * k.DryRangedPermille);
        long total = checked(baseMilli + ms.Scale(bowFire) + Q.Sub(Q32.One, ms).Scale(bowDry) + mu.Scale(gunFire) + Q.Sub(Q32.One, mu).Scale(gunDry));
        Q32 f = Q.Sub(Q32.One, Q.Clamp01(disease)).Mul(HungerFactor(k, nutrition)).Mul(fortFactor);
        return f.Scale(total);
    }

    /// <summary>
    /// Assault power of the besiegers (people-equivalents × 1000) with a share of the melee arm committed: storming party
    /// (committed melee, engineers, cavalry and ranged at their AssaultValue) × (1 + s_r × min(ranged:melee ratio, cap)) — the
    /// ranged-to-melee ratio enters as suppressive support that saturates — plus engine support, × supply × (1 − sick) × morale.
    /// </summary>
    public static long AssaultPower(SiegeCoefficients k, long[] people, RangedKind ranged, long[] engines, bool approachComplete, Q32 commit,
                                    Q32 supply, Q32 disease, Q32 morale)
    {
        long melee = Q.Clamp01(commit).Scale(people[0]);
        long party = checked(melee * k.AssaultValuePermille[0] + people[1] * k.AssaultValuePermille[1]
                             + people[2] * k.AssaultValuePermille[2] + people[3] * k.AssaultValuePermille[3]);
        long rangedEq = checked(people[1] * k.RangedKindFactorPermille[(int)ranged] / 1000);
        Q32 ratio = melee > 0 ? Q32.FromRatio(rangedEq, melee) : Q.Permille(k.RangedSupportCapRatioPermille);
        ratio = Q.Min(ratio, Q.Permille(k.RangedSupportCapRatioPermille));
        Q32 support = Q.Add(Q32.One, Q.Permille(k.RangedSupportPermille).Mul(ratio));
        long eng = 0;
        for (int e = 0; e < 4; e++)
        {
            if (e == (int)EngineClass.RamTower && !approachComplete) continue;
            eng = checked(eng + engines[e] * k.EngineSupportPermille[e]);
        }
        long total = checked(support.Scale(party) + eng);
        Q32 f = Q.Clamp01(supply).Mul(Q.Sub(Q32.One, Q.Clamp01(disease))).Mul(MoraleFight(morale));
        return f.Scale(total);
    }

    /// <summary>The besiegers as a field force defending their lines (relief battles and sallies) at field values, ranged by weapon,
    /// × (1 + fieldValue × works facing outward).</summary>
    public static long LinePower(SiegeCoefficients k, long[] people, RangedKind ranged, Q32 works, Q32 supply, Q32 disease, Q32 morale)
    {
        long b = 0;
        for (int a = 0; a < 4; a++)
        {
            long v = checked(people[a] * k.FieldValuePermille[a]);
            if (a == (int)Arm.Ranged) v = checked(v * k.RangedKindFactorPermille[(int)ranged] / 1000);
            b = checked(b + v);
        }
        Q32 f = Q.Add(Q32.One, Q.Permille(k.FieldWorksValuePermille).Mul(Q.Clamp01(works)));
        f = f.Mul(Q.Clamp01(supply)).Mul(Q.Sub(Q32.One, Q.Clamp01(disease))).Mul(MoraleFight(morale));
        return f.Scale(b);
    }

    /// <summary>A field force attacking (relief army, sally party or raiders) at field values, ranged by weapon (no engines).</summary>
    public static long FieldAttackPower(SiegeCoefficients k, long[] people, RangedKind ranged)
    {
        long b = 0;
        for (int a = 0; a < 4; a++)
        {
            long v = checked(people[a] * k.FieldValuePermille[a]);
            if (a == (int)Arm.Ranged) v = checked(v * k.RangedKindFactorPermille[(int)ranged] / 1000);
            b = checked(b + v);
        }
        return b;
    }

    /// <summary>Siege health = will × capability = M × clamp(D / D_ref) (DIR5 §2.2 "siege-health calculation"; C15 "Q32 fraction").</summary>
    public static Q32 Health(Q32 morale, long capability, long capabilityRef)
        => Q.Clamp01(morale).Mul(capabilityRef <= 0 ? Q32.Zero : Q.Clamp01(Q.Ratio(capability, capabilityRef)));

    /// <summary>Regime label from the besiegers' machines (computed capability; Law 4). Reporting and tests only.</summary>
    public static SiegeRegime RegimeOf(long[] engines)
        => engines[(int)EngineClass.ModernGun] > 0 ? SiegeRegime.Modern
         : engines[(int)EngineClass.Bombard] > 0 ? SiegeRegime.Gunpowder
         : SiegeRegime.Classical;

    /// <summary>Endurance in ms of a store at a ration (full need per sim-year × ration), or long.MaxValue for no consumers.
    /// Strictly longer for a strictly lower ration while the store is non-empty (the rationing trade-off's endurance side). A
    /// closed-form helper for tests and reports: the world integrates consumption pulse by pulse and does not call it (S-V10).</summary>
    public static long EnduranceMs(long store, long fullNeedPerYear, Q32 ration)
    {
        long ratePerYear = Q.Clamp01(ration).Scale(fullNeedPerYear);
        if (ratePerYear <= 0) return long.MaxValue;
        return FixedMath.MulDivFloor(store, SimInstant.MsPerYear, ratePerYear);
    }
}
