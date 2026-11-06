namespace Proto.M7.WarEcon;

/// <summary>
/// The per-head loss RATES of soldiers (and, in a besieged city, civilians) shared by lane E's army economy
/// (<see cref="WarEconomyWorld"/>) and lane S's siege world (Proto.M7.War.Siege). Extracted by lane S from lane E's
/// <c>WarEconomyWorld.OnPulseStart</c> without changing a single operation or its order, so both worlds use ONE formula with
/// ONE coefficient table (<see cref="LogisticsRates"/>, lane E's labelled PROPOSAL). Rates are NOMINAL per-sim-year ‰ of the
/// heads still there (Law 3); <see cref="HeadcountLoss"/> integrates them over a pulse. Pure integer arithmetic.
/// </summary>
public static class TroopLossRates
{
    /// <summary>
    /// Desertion ‰/yr: base + low morale × (1 − morale) + arrears (per month in arrears, capped) + hunger × hunger share
    /// + mutiny. <paramref name="moralePpm"/> is morale in millionths (0..1,000,000); <paramref name="arrearsMonths"/> is the
    /// age of the oldest unpaid arrears in whole 30-day months; <paramref name="hungerPm"/> is the share of the standing ration
    /// not eaten, in ‰ (0..1000).
    /// </summary>
    public static long DesertionPermilleYear(LogisticsRates r, long moralePpm, long arrearsMonths, long hungerPm, bool mutinous)
        => r.DesertionBasePermilleYear
           + r.DesertionLowMoralePermilleYear * (1000 * 1000 - moralePpm) / (1000 * 1000)
           + Math.Min(r.DesertionArrearsCapPermilleYear, arrearsMonths * r.DesertionPerArrearsMonthPermilleYear)
           + r.DesertionHungerPermilleYear * hungerPm / 1000
           + (mutinous ? r.DesertionMutinyPermilleYear : 0);

    /// <summary>
    /// Disease mortality ‰/yr of a body of men at the background level of camp disease: base × (1 + hunger factor × hunger +
    /// exposure factor × equipment deficit), × the Sanitation factor when the polity holds Sanitation. Starvation itself is
    /// NOT included (<see cref="StarvationPermilleYear"/>).
    /// </summary>
    public static long DiseasePermilleYear(LogisticsRates r, long hungerPm, long kitDeficitPm, bool sanitation)
    {
        long dis = r.DiseaseBasePermilleYear * (1000 + r.DiseaseHungerFactorPermille * hungerPm / 1000 + r.DiseaseExposureFactorPermille * kitDeficitPm / 1000) / 1000;
        if (sanitation) dis = dis * r.DiseaseSanitationPermille / 1000;
        return dis;
    }

    /// <summary>Deaths from starvation itself ‰/yr at a ration shortfall of <paramref name="hungerPm"/> ‰ (lane E's immediate
    /// shortfall model of a field army; not reduced by Sanitation).</summary>
    public static long StarvationPermilleYear(LogisticsRates r, long hungerPm) => r.StarvationDeathPermilleYear * hungerPm / 1000;
}
