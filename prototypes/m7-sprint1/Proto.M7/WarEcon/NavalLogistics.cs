using Proto.M7.Fixed;
using Proto.M7.Tactical;
using Proto.M7.Time;

namespace Proto.M7.WarEcon;

/// <summary>
/// Ships' companies at sea (Phase C lane E; DIR5 §2.11 composition, D-080 WA4 realism). A naval force is an
/// <see cref="ArmyComposition"/> with a lane N <see cref="VesselClass"/> and a number of hulls: its company is hulls x the
/// class's <see cref="VesselClass.Crew"/> (lane N's vessel model, not a parallel one), plus any embarked foot up to the
/// hulls' <see cref="VesselClass.Capacity"/>. The class is the one lane N's <see cref="NavalDesign.Derive"/> gives for the
/// polity's naval capability; nothing reads a date (Law 4).
///
/// What differs from a land army (every number a PROPOSAL in <see cref="LogisticsRates.Proposal"/>, reports/H-E.md §3a):
/// - ration: the sea ration replaces the field extra (heavier, preserved food);
/// - exertion: only oared vessels have one — the rowers' extra ration while under way;
/// - stores: a hull stows days of its company's ration by propulsion (galleys a few days, sailing ships months);
/// - movement: passage speed (engines: the class's sustained speed; oar and sail: by propulsion), on Water segments only; steam movement burns bunker coal (and only steam);
/// - pay: seamen's wages; munitions: powder and shot for gun-armed hulls on days of action (Gunpowder only);
/// - scurvy: deaths begin after ScurvyOnsetDays without fresh food and rise with the time beyond; a port call that yields
///   fresh food resets the clock; Antiscorbutics cut the rate. The rate is latched at the pulse start (Law 3 integration).
/// </summary>
public static class NavalLogistics
{
    private const long Day = SimInstant.MsPerDay;

    /// <summary>True when any of the class's weapons is a gun (every lane N naval weapon except the deck archers).</summary>
    public static bool GunArmed(VesselClass v)
    {
        foreach (var w in v.Weapons) if (!ReferenceEquals(w, DomainProfiles.DeckArchers)) return true;
        return false;
    }

    /// <summary>The naval capability a propulsion needs (lane N: oars, sails, steam power).</summary>
    public static NavalCapability Needs(Propulsion p) => p switch
    {
        Propulsion.Oar => NavalCapability.Oars,
        Propulsion.Sail => NavalCapability.Sails,
        _ => NavalCapability.SteamPower,
    };

    /// <summary>Passage speed (m/day) of a naval force. Engine-driven: the lane N class's MaxSpeedMmPerS at the sustained
    /// ‰ (mm/s × 86,400 s/day / 1000 = m/day; lane E fix round 1, E-V7), so a turret warship outsteams a steam frigate.
    /// Oared and sailing: the propulsion's passage table (night halts and the wind govern a passage, not the class's
    /// sprint or best-point-of-sailing speed).</summary>
    public static long PassageSpeed(VesselClass v, LogisticsRates r) => v.Propulsion switch
    {
        Propulsion.Oar => r.PassageOarMPerDay,
        Propulsion.Sail => r.PassageSailMPerDay,
        _ => (long)((Int128)v.MaxSpeedMmPerS * 86_400 / 1000 * r.PassageEngineSustainedPermille / 1000),
    };

    /// <summary>Days of the company's sea ration one hull stows, by propulsion.</summary>
    public static long StoresDays(VesselClass v, LogisticsRates r) => v.Propulsion switch
    {
        Propulsion.Oar => r.StoresDaysOar,
        Propulsion.Sail => r.StoresDaysSail,
        _ => r.StoresDaysEngine,
    };

    /// <summary>Rowers among <paramref name="men"/> present (losses are pro rata over the initial composition): the
    /// seamen's share of the men, times the rowers' share of an oared crew. Zero unless the vessels are oared.</summary>
    public static long Rowers(ArmyComposition c, long men, LogisticsRates r)
    {
        if (!c.Naval || c.Vessel!.Propulsion != Propulsion.Oar) return 0;
        return (long)((Int128)men * c.Seamen * r.RowersPermille / ((Int128)c.Men * 1000));
    }

    /// <summary>Seamen among <paramref name="men"/> present (pro rata), for pay.</summary>
    public static long SeamenPresent(ArmyComposition c, long men)
        => c.Naval ? (long)((Int128)men * c.Seamen / c.Men) : 0;

    /// <summary>Kg of bunker coal to steam <paramref name="distanceM"/> metres (rounded up); zero unless steam-powered.</summary>
    public static long CoalFor(ArmyComposition c, long distanceM, LogisticsRates r)
    {
        if (!c.Naval || c.Vessel!.Propulsion != Propulsion.Engine || distanceM == 0) return 0;
        Int128 n = (Int128)c.Hulls * r.CoalPerHull1000Km * distanceM;
        return (long)((n + 999_999) / 1_000_000);
    }

    /// <summary>Scurvy deaths, ‰ per year, after <paramref name="msWithoutFresh"/> ms without fresh food: zero up to the
    /// onset, then ScurvyPermilleYearPerMonth per 30 days beyond it (exact, floor), capped; Antiscorbutics multiply it by
    /// ScurvyAntiscorbuticPermille ‰.</summary>
    public static long ScurvyPermilleYear(long msWithoutFresh, PolityCapability cap, LogisticsRates r)
    {
        long beyond = msWithoutFresh - r.ScurvyOnsetDays * Day;
        if (beyond <= 0) return 0;
        Int128 raw = (Int128)r.ScurvyPermilleYearPerMonth * beyond / (30 * Day);
        long rate = raw > r.ScurvyMaxPermilleYear ? r.ScurvyMaxPermilleYear : (long)raw;
        if (cap.Has(WarTech.Antiscorbutics)) rate = FixedMath.MulDivFloor(rate, r.ScurvyAntiscorbuticPermille, 1000);
        return rate;
    }

    /// <summary>Null when a naval composition is legal for the capability; otherwise the reason.</summary>
    public static string? Validate(ArmyComposition c, PolityCapability cap)
    {
        if (c.Vessel is null || c.Hulls <= 0) return "a naval force needs a vessel class and at least one hull";
        if (c.Mounted != 0 || c.GunCrew != 0 || c.Horses != 0 || c.DraftAnimals != 0 || c.Wagons != 0 || c.Trucks != 0 || c.Guns != 0)
            return "a naval force carries only its company and embarked foot";
        if (c.Foot > checked(c.Hulls * c.Vessel.Capacity)) return "embarked foot exceed the hulls' capacity";
        if (!cap.HasNaval(Needs(c.Vessel.Propulsion))) return $"{c.Vessel.Propulsion} vessels need the {Needs(c.Vessel.Propulsion)} capability";
        if (GunArmed(c.Vessel) && !cap.Has(WarTech.Gunpowder)) return "gun-armed vessels need Gunpowder";
        return null;
    }
}
