using Proto.M7.Fixed;

namespace Proto.M7.Tactical;

/// <summary>
/// PLACEHOLDER vessel classes, airframes, weapons and land profiles for the naval / air / combined-arms foundation (lane N).
/// NOTHING here is a ratified number: m7-spec.md:395-404 gives caps only, and no vessel, airframe or weapon statistics are
/// ratified anywhere. Values are plausible orders of magnitude (speeds from knots and km/h, crews and ranges from the
/// general historical record) chosen so the mechanisms can be exercised and distinguished by era; they are inputs to be
/// calibrated (TUNE), not claims. Every record carries the label in its Citation.
/// Unit conversions used: 1 knot = 514 mm/s; 1 km/h = 278 mm/s.
/// Gunfire accuracies are deliberately LOW for long-range guns and air defence (per-shot hit rates of a few percent at
/// range, as for real naval and anti-aircraft fire); the hit chance never drops below TacticalRules.HitMin (1/20).
/// </summary>
public static class DomainProfiles
{
    private const string Ph = "PLACEHOLDER (lane N; no ratified value exists)";

    // ------------------------------------------------------------------ weapons

    /// <summary>Archers / javelins on deck (oared and early sailing warships): kill people, cannot hole a hull.</summary>
    public static WeaponSpec DeckArchers { get; } = new("deck_archers", TargetDomains.Surface | TargetDomains.Land, MountArc.Turret,
        MaxRangeMm: 150_000, ReloadMs: 10_000, Accuracy: 30) { Damage = 10, CrewDamage = 2 };

    /// <summary>Muzzle-loading broadside battery (sail era): one salvo per side about once a minute.</summary>
    public static WeaponSpec Broadside { get; } = new("broadside_battery", TargetDomains.Surface | TargetDomains.Land, MountArc.Broadside,
        MaxRangeMm: 1_000_000, ReloadMs: 60_000, Accuracy: 20)
        { Damage = 25, HullDamage = 30, CrewDamage = 12, BlastRadiusMm = 15_000, DispersionPermille = 60 };

    /// <summary>Bow guns of a gun galley: fire only ahead.</summary>
    public static WeaponSpec BowGuns { get; } = new("bow_guns", TargetDomains.Surface | TargetDomains.Land, MountArc.Bow,
        MaxRangeMm: 800_000, ReloadMs: 60_000, Accuracy: 15)
        { ArcHalf = FixedTrig.FromDegrees(15), Damage = 25, HullDamage = 20, CrewDamage = 8, BlastRadiusMm = 12_000, DispersionPermille = 60 };

    /// <summary>Shell-firing broadside of a steam frigate.</summary>
    public static WeaponSpec SteamBroadside { get; } = new("steam_broadside", TargetDomains.Surface | TargetDomains.Land, MountArc.Broadside,
        MaxRangeMm: 1_500_000, ReloadMs: 45_000, Accuracy: 15)
        { Damage = 35, HullDamage = 40, CrewDamage = 12, BlastRadiusMm = 15_000, DispersionPermille = 50 };

    /// <summary>Breech-loading turret gun (engine-powered warship).</summary>
    public static WeaponSpec TurretGun { get; } = new("turret_gun", TargetDomains.Surface | TargetDomains.Land, MountArc.Turret,
        MaxRangeMm: 8_000_000, ReloadMs: 6_000, Accuracy: 8)
        { Damage = 60, HullDamage = 25, CrewDamage = 4, BlastRadiusMm = 15_000, DispersionPermille = 20 };

    public static WeaponSpec ShipAa { get; } = new("ship_aa", TargetDomains.Air, MountArc.Turret,
        MaxRangeMm: 2_500_000, ReloadMs: 4_000, Accuracy: 2) { Damage = 5 };

    public static WeaponSpec LightAa { get; } = new("light_aa", TargetDomains.Air, MountArc.Turret,
        MaxRangeMm: 1_500_000, ReloadMs: 6_000, Accuracy: 2) { Damage = 4 };

    public static WeaponSpec AircraftGuns { get; } = new("aircraft_guns", TargetDomains.Air, MountArc.Bow,
        MaxRangeMm: 800_000, ReloadMs: 1_000, Accuracy: 15) { ArcHalf = FixedTrig.FromDegrees(10), Damage = 6 };

    public static WeaponSpec DefensiveGun { get; } = new("defensive_gun", TargetDomains.Air, MountArc.Turret,
        MaxRangeMm: 500_000, ReloadMs: 2_000, Accuracy: 5) { Damage = 4 };

    /// <summary>Free-fall bombs, released over the target (horizontal range 300 m); two per sortie.</summary>
    public static WeaponSpec Bombs { get; } = new("bombs", TargetDomains.Land | TargetDomains.Surface, MountArc.Turret,
        MaxRangeMm: 300_000, ReloadMs: 2_000, Accuracy: 15)
        { Damage = 60, HullDamage = 80, CrewDamage = 10, BlastRadiusMm = 25_000, DispersionPermille = 100, Ammo = 2 };

    /// <summary>Guided rockets of an attack helicopter: direct fire, forward arc, eight per sortie.</summary>
    public static WeaponSpec Rockets { get; } = new("rockets", TargetDomains.Land | TargetDomains.Surface, MountArc.Bow,
        MaxRangeMm: 3_000_000, ReloadMs: 5_000, Accuracy: 20)
        { ArcHalf = FixedTrig.FromDegrees(20), Damage = 40, HullDamage = 40, CrewDamage = 5, Ammo = 8 };

    /// <summary>Coastal gun (land, crew-served): engages vessels and land targets.</summary>
    public static WeaponSpec CoastalGun { get; } = new("coastal_gun", TargetDomains.Surface | TargetDomains.Land, MountArc.Turret,
        MaxRangeMm: 3_000_000, ReloadMs: 10_000, Accuracy: 8)
        { Damage = 50, HullDamage = 30, CrewDamage = 5, BlastRadiusMm = 20_000, DispersionPermille = 25 };

    /// <summary>Field artillery (land): engages land targets and vessels near the shore.</summary>
    public static WeaponSpec FieldGun { get; } = new("field_gun", TargetDomains.Surface | TargetDomains.Land, MountArc.Turret,
        MaxRangeMm: 3_000_000, ReloadMs: 8_000, Accuracy: 10)
        { Damage = 40, HullDamage = 15, CrewDamage = 3, BlastRadiusMm = 15_000, DispersionPermille = 30 };

    public static WeaponSpec AaGun { get; } = new("aa_gun", TargetDomains.Air, MountArc.Turret,
        MaxRangeMm: 2_500_000, ReloadMs: 4_000, Accuracy: 2) { Damage = 5 };

    // ------------------------------------------------------------------ vessels (capability-defined; see NavalDesign)

    /// <summary>Oared warship with a ram (trireme-like): ~9 knots sprint, rams and boards; deck archers.</summary>
    public static VesselClass Galley { get; } = new(101, "oared_ram_galley", Propulsion.Oar, RadiusMm: 8_000, MaxSpeedMmPerS: 4_600,
        AccelMmPerS2: 500, TurnRatePerS: FixedTrig.FromDegrees(10), Hull: 120, Crew: 200, Citation: Ph)
        { Weapons = new[] { DeckArchers }, RamDamage = 90, HeightMm = 5_000, SightMm = 2_000_000, Evasion = 25, BoardingMelee = 25, BoardingDefence = 25 };

    /// <summary>Sailing warship without guns (cog-like): boards; deck archers.</summary>
    public static VesselClass Cog { get; } = new(102, "sailing_boarding_ship", Propulsion.Sail, RadiusMm: 8_000, MaxSpeedMmPerS: 3_000,
        AccelMmPerS2: 100, TurnRatePerS: FixedTrig.FromDegrees(4), Hull: 150, Crew: 80, Citation: Ph)
        { Weapons = new[] { DeckArchers }, HeightMm = 15_000, SightMm = 2_500_000, Evasion = 20, BoardingMelee = 35, BoardingDefence = 40 };

    /// <summary>Oared gun galley: bow guns only (must point at the target).</summary>
    public static VesselClass GunGalley { get; } = new(103, "oared_gun_galley", Propulsion.Oar, RadiusMm: 10_000, MaxSpeedMmPerS: 4_000,
        AccelMmPerS2: 400, TurnRatePerS: FixedTrig.FromDegrees(8), Hull: 200, Crew: 300, Citation: Ph)
        { Weapons = new[] { BowGuns, DeckArchers }, HeightMm = 6_000, SightMm = 2_500_000, Evasion = 25 };

    /// <summary>Sailing line-of-battle ship: broadsides only (no bow fire), slow to turn, wind-dependent.</summary>
    public static VesselClass ShipOfTheLine { get; } = new(104, "sailing_line_ship", Propulsion.Sail, RadiusMm: 15_000, MaxSpeedMmPerS: 4_100,
        AccelMmPerS2: 100, TurnRatePerS: FixedTrig.FromDegrees(3), Hull: 600, Crew: 600, Citation: Ph)
        { Weapons = new[] { Broadside }, HeightMm = 50_000, SightMm = 4_000_000, Evasion = 40, SignaturePercent = 400, BoardingMelee = 30, BoardingDefence = 35 };

    /// <summary>Steam frigate: engine power with a shell broadside (no wind dependence).</summary>
    public static VesselClass SteamFrigate { get; } = new(105, "steam_broadside_frigate", Propulsion.Engine, RadiusMm: 15_000, MaxSpeedMmPerS: 5_700,
        AccelMmPerS2: 200, TurnRatePerS: FixedTrig.FromDegrees(4), Hull: 500, Crew: 400, Citation: Ph)
        { Weapons = new[] { SteamBroadside }, HeightMm = 30_000, SightMm = 5_000_000, Evasion = 60, SignaturePercent = 400 };

    /// <summary>Turret warship (destroyer-like): fast, long-range turret guns, air defence.</summary>
    public static VesselClass TurretWarship { get; } = new(106, "turret_warship", Propulsion.Engine, RadiusMm: 12_000, MaxSpeedMmPerS: 15_400,
        AccelMmPerS2: 400, TurnRatePerS: FixedTrig.FromDegrees(6), Hull: 300, Crew: 150, Citation: Ph)
        { Weapons = new[] { TurretGun, ShipAa }, HeightMm = 25_000, SightMm = 12_000_000, Evasion = 100 };

    /// <summary>Engine-powered vessel without guns (boarding only) — exists so the capability mapping is total.</summary>
    public static VesselClass EngineBoardingShip { get; } = Cog with { ClassKey = 107, Name = "engine_boarding_ship", Propulsion = Propulsion.Engine, MaxSpeedMmPerS = 5_000, AccelMmPerS2 = 200 };

    public static VesselClass OaredTransport { get; } = new(201, "oared_transport", Propulsion.Oar, RadiusMm: 9_000, MaxSpeedMmPerS: 3_000,
        AccelMmPerS2: 300, TurnRatePerS: FixedTrig.FromDegrees(6), Hull: 80, Crew: 40, Citation: Ph)
        { Capacity = 30, HeightMm = 4_000, SightMm = 2_000_000, Evasion = 20, EmbarkMsPerPerson = 2_000, DisembarkMsPerPerson = 2_000 };

    public static VesselClass SailTransport { get; } = new(202, "sailing_transport", Propulsion.Sail, RadiusMm: 10_000, MaxSpeedMmPerS: 3_000,
        AccelMmPerS2: 100, TurnRatePerS: FixedTrig.FromDegrees(3), Hull: 150, Crew: 60, Citation: Ph)
        { Capacity = 40, HeightMm = 20_000, SightMm = 2_500_000, Evasion = 30, EmbarkMsPerPerson = 3_000, DisembarkMsPerPerson = 4_000 };

    public static VesselClass SteamTransport { get; } = new(203, "engine_transport", Propulsion.Engine, RadiusMm: 10_000, MaxSpeedMmPerS: 7_700,
        AccelMmPerS2: 250, TurnRatePerS: FixedTrig.FromDegrees(5), Hull: 200, Crew: 50, Citation: Ph)
        { Capacity = 50, Weapons = new[] { LightAa }, HeightMm = 15_000, SightMm = 6_000_000, Evasion = 60, EmbarkMsPerPerson = 1_000, DisembarkMsPerPerson = 1_000 };

    // ------------------------------------------------------------------ airframes

    public static AirframeClass Fighter { get; } = new(301, "fighter", AirKind.FixedWing, MinSpeedMmPerS: 60_000, CruiseMmPerS: 120_000,
        MaxSpeedMmPerS: 180_000, TurnRatePerS: FixedTrig.FromDegrees(20), AltitudeMm: 1_500_000, EnduranceMs: 1_800_000, Sorties: 2,
        TurnaroundMs: 600_000, Hp: 30, Crew: 1, Citation: Ph)
        { Weapons = new[] { AircraftGuns }, SightMm = 5_000_000, Evasion = 60, AccelMmPerS2 = 15_000 };

    public static AirframeClass StrikeAircraft { get; } = new(302, "strike_aircraft", AirKind.FixedWing, MinSpeedMmPerS: 50_000, CruiseMmPerS: 100_000,
        MaxSpeedMmPerS: 140_000, TurnRatePerS: FixedTrig.FromDegrees(12), AltitudeMm: 1_000_000, EnduranceMs: 1_800_000, Sorties: 2,
        TurnaroundMs: 900_000, Hp: 40, Crew: 2, Citation: Ph)
        { Weapons = new[] { Bombs, DefensiveGun }, SightMm = 4_000_000, Evasion = 25, AccelMmPerS2 = 10_000 };

    public static AirframeClass AttackHelicopter { get; } = new(303, "attack_helicopter", AirKind.Rotary, MinSpeedMmPerS: 0, CruiseMmPerS: 60_000,
        MaxSpeedMmPerS: 80_000, TurnRatePerS: FixedTrig.FromDegrees(30), AltitudeMm: 100_000, EnduranceMs: 2_400_000, Sorties: 2,
        TurnaroundMs: 600_000, Hp: 30, Crew: 2, Citation: Ph)
        { Weapons = new[] { Rockets }, SightMm = 4_000_000, Evasion = 30, AccelMmPerS2 = 8_000 };

    public static AirframeClass ReconAircraft { get; } = new(304, "recon_aircraft", AirKind.FixedWing, MinSpeedMmPerS: 40_000, CruiseMmPerS: 70_000,
        MaxSpeedMmPerS: 90_000, TurnRatePerS: FixedTrig.FromDegrees(15), AltitudeMm: 2_000_000, EnduranceMs: 3_600_000, Sorties: 2,
        TurnaroundMs: 600_000, Hp: 20, Crew: 2, Citation: Ph)
        { SightMm = 6_000_000, Evasion = 30, AccelMmPerS2 = 8_000 };

    // ------------------------------------------------------------------ land profiles used with the domain modules

    /// <summary>
    /// Sniper team member. PROTOTYPE TEST IDENTITY: no sniper identity exists in the data (CAP-4 / C36: "created only in an
    /// authorized packet"); IdentityKey 4990 is a prototype-local key. Cap family Snipers (cap 2, m7-spec.md:401). Values
    /// PLACEHOLDER: very high ranged skill, long range and sight (optics), low fire rate (one aimed shot per 5 steps),
    /// personal concealment 1/8 (camouflage: detection chance x 1/8 unless it fired within the last step).
    /// </summary>
    public static UnitProfile Sniper { get; } = new(
        FamilyKey: 4, IdentityKey: 4990, Name: "sniper_team(prototype)", CapFamily: CapFamily.Snipers,
        Integrity: 30, Melee: 10, Ranged: 90, Defence: 20, RangeCells: 30, SightCells: 35, SpeedNum: 1, SpeedDen: 1,
        Citation: Ph + "; cap m7-spec.md:401")
    { FireIntervalSteps = 5, PersonalConcealment = Q32.FromRatio(1, 8) };

    /// <summary>Rifle infantry (firearm era): legacy ranged fire every step. PROTOTYPE TEST IDENTITY 4991.</summary>
    public static UnitProfile Riflemen { get; } = new(
        FamilyKey: 4, IdentityKey: 4991, Name: "riflemen(prototype)", CapFamily: CapFamily.ArchersLightInfantry,
        Integrity: 40, Melee: 20, Ranged: 50, Defence: 30, RangeCells: 6, SightCells: 14, SpeedNum: 1, SpeedDen: 1, Citation: Ph);

    /// <summary>
    /// Crew of a crew-served gun (coastal, field or air-defence gun). Ranged 0: the gun is a domain weapon given on the
    /// formation (<see cref="FormationSpec.Weapons"/>), resolved by the combined-arms layer, not the legacy land fire. One
    /// entity = one weapon team (gun + crew). Family key 8 = siege (bombards, field artillery; unit-families.json:152-164);
    /// coastal guns are fortress artillery of the same family and anti-aircraft guns belong to air defence (key 14). Neither
    /// family has a Director cap (D-076 §5 CAP-5; D-079 R6 keeps siege and air defence uncapped), so the profile is
    /// <see cref="CapFamily.Uncapped"/>: no cap is invented (the Mortars cap of 5 is for mortars only). PROTOTYPE TEST
    /// IDENTITY 4992.
    /// </summary>
    public static UnitProfile GunCrew { get; } = new(
        FamilyKey: 8, IdentityKey: 4992, Name: "gun_crew(prototype)", CapFamily: CapFamily.Uncapped,
        Integrity: 40, Melee: 10, Ranged: 0, Defence: 25, RangeCells: 0, SightCells: 20, SpeedNum: 1, SpeedDen: 2, Citation: Ph);
}

/// <summary>
/// Vessel class from CAPABILITIES (state-derived; law 4 — no date, era label or turn number is an input). Propulsion is the
/// best the capability set supports (steam power &gt; sails &gt; oars); a warship's armament follows gunpowder and turrets
/// (turrets need steam power to traverse). The mapping is total over every capability set that includes some propulsion.
/// </summary>
public static class NavalDesign
{
    public static Propulsion PropulsionOf(NavalCapability caps)
    {
        if ((caps & NavalCapability.SteamPower) != 0) return Propulsion.Engine;
        if ((caps & NavalCapability.Sails) != 0) return Propulsion.Sail;
        if ((caps & NavalCapability.Oars) != 0) return Propulsion.Oar;
        throw new ArgumentException("no seagoing propulsion in the capability set", nameof(caps));
    }

    public static VesselClass Derive(NavalCapability caps, VesselRole role)
    {
        var p = PropulsionOf(caps);
        if (role == VesselRole.Transport)
            return p switch { Propulsion.Oar => DomainProfiles.OaredTransport, Propulsion.Sail => DomainProfiles.SailTransport, _ => DomainProfiles.SteamTransport };
        bool guns = (caps & NavalCapability.Gunpowder) != 0;
        bool turrets = (caps & NavalCapability.Turrets) != 0 && guns && p == Propulsion.Engine;
        if (turrets) return DomainProfiles.TurretWarship;
        if (guns) return p switch { Propulsion.Oar => DomainProfiles.GunGalley, Propulsion.Sail => DomainProfiles.ShipOfTheLine, _ => DomainProfiles.SteamFrigate };
        return p switch { Propulsion.Oar => DomainProfiles.Galley, Propulsion.Sail => DomainProfiles.Cog, _ => DomainProfiles.EngineBoardingShip };
    }
}
