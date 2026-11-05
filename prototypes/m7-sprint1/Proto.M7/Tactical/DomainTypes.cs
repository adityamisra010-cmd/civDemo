using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>How a vessel moves. Stable codes. Sail speed depends on the wind (see <see cref="DomainRules.SailFactor"/>).</summary>
public enum Propulsion : byte
{
    Oar = 0,
    Sail = 1,
    Engine = 2,
}

/// <summary>Where a weapon mount can bear, relative to the carrier's heading. Stable codes.</summary>
public enum MountArc : byte
{
    /// <summary>Within +-ArcHalf of the heading (bow chasers, fixed forward guns, forward-firing aircraft guns).</summary>
    Bow = 0,
    /// <summary>Within +-ArcHalf of EITHER beam (heading +-90 degrees): broadside batteries.</summary>
    Broadside = 1,
    /// <summary>Any bearing (turrets, land guns, bombs released over the target).</summary>
    Turret = 2,
}

/// <summary>Target classes a weapon is compatible with (engagement eligibility, m7-spec REV:430-433).</summary>
[Flags]
public enum TargetDomains : byte
{
    None = 0,
    Land = 1,
    Surface = 2,
    Air = 4,
}

public enum AirKind : byte
{
    FixedWing = 0,
    Rotary = 1,
}

/// <summary>Flight phase of an aircraft entity (orthogonal to its <see cref="EntityState"/> life state).</summary>
public enum AirPhase : byte
{
    /// <summary>On the ground at its side's air base: not on the battlefield, cannot be seen or engaged.</summary>
    Based = 0,
    Airborne = 1,
    /// <summary>Returning to base (bingo fuel, empty racks, damage, or a Retreat order). Cannot attack.</summary>
    Returning = 2,
}

/// <summary>
/// One weapon system of a vessel, an aircraft or a land formation with domain weapons (artillery, coastal guns, air defence).
/// Engagement sequence (m7-spec REV:432): the shooter's side must TRACK the target (detect) -&gt; the target's domain must be in
/// <see cref="Targets"/> (classify + eligibility) -&gt; range and arc -&gt; LOS for direct fire -&gt; hit roll or area impact
/// (interception/defence = the target's evasion or defence in the hit roll) -&gt; damage -&gt; knowledge and inspection (trace).
/// Every number on every instance in <see cref="DomainProfiles"/> is a PLACEHOLDER.
/// </summary>
public sealed record WeaponSpec(string Name, TargetDomains Targets, MountArc Arc, long MaxRangeMm, long ReloadMs, long Accuracy)
{
    public long MinRangeMm { get; init; }
    /// <summary>Angular half-width of the arc (Bow: around the heading; Broadside: around each beam). Ignored for Turret.</summary>
    public int ArcHalf { get; init; } = FixedTrig.FromDegrees(30);
    /// <summary>HP per hit (or per blast) on a land entity or an airframe.</summary>
    public long Damage { get; init; }
    /// <summary>Hull points per hit on a vessel.</summary>
    public long HullDamage { get; init; }
    /// <summary>Personnel casualties per hit on a vessel, split between crew and embarked troops in proportion.</summary>
    public long CrewDamage { get; init; }
    /// <summary>&gt; 0: area weapon against land targets (indirect fire at a point; every land entity of EITHER side inside the
    /// radius of the impact takes <see cref="Damage"/>). 0: direct fire with a hit roll, line of sight required.</summary>
    public long BlastRadiusMm { get; init; }
    /// <summary>Area fire: impact = aim + uniform offset in a square of half-width range * permille / 1000.</summary>
    public long DispersionPermille { get; init; }
    /// <summary>Rounds per sortie (aircraft) or per battle (vessels, land); -1 = unlimited.</summary>
    public int Ammo { get; init; } = -1;

    public bool Indirect => BlastRadiusMm > 0;

    public void Validate()
    {
        if (Targets == TargetDomains.None) throw new ArgumentException($"weapon {Name}: no target class");
        if (MaxRangeMm <= 0 || MinRangeMm < 0 || MinRangeMm >= MaxRangeMm) throw new ArgumentException($"weapon {Name}: range");
        if (ReloadMs <= 0) throw new ArgumentException($"weapon {Name}: reload must be positive");
        if (Accuracy <= 0) throw new ArgumentException($"weapon {Name}: accuracy must be positive");
        if (Damage < 0 || HullDamage < 0 || CrewDamage < 0 || BlastRadiusMm < 0 || DispersionPermille < 0) throw new ArgumentException($"weapon {Name}: negative effect");
        if ((Targets & TargetDomains.Land) != 0 && Damage <= 0) throw new ArgumentException($"weapon {Name}: land-capable weapon needs Damage > 0 (termination: every attack must be able to consume HP)");
        if ((Targets & TargetDomains.Air) != 0 && Damage <= 0) throw new ArgumentException($"weapon {Name}: air-capable weapon needs Damage > 0");
        if ((Targets & TargetDomains.Surface) != 0 && HullDamage <= 0 && CrewDamage <= 0) throw new ArgumentException($"weapon {Name}: surface-capable weapon needs HullDamage > 0 or CrewDamage > 0");
        if (ArcHalf < 0 || ArcHalf > FixedTrig.HalfTurn) throw new ArgumentException($"weapon {Name}: arc");
        if (Ammo < -1 || Ammo == 0) throw new ArgumentException($"weapon {Name}: ammo must be -1 (unlimited) or positive");
    }
}

/// <summary>
/// A vessel class: what one hull can do. A class is chosen from CAPABILITIES (propulsion, armament), never from a date or an
/// era label (CLAUDE.md law 4; <see cref="NavalCapability"/>). One vessel entity = one hull (CAP-3 counted object: hulls;
/// "Ships 5" covers every vessel family, transports included — READING CAP-5, m7-2-m7-3 revision §5).
/// Hull points are structural integrity (not conserved); Crew is PERSONNEL (conserved through the battle ledger).
/// </summary>
public sealed record VesselClass(
    int ClassKey, string Name, Propulsion Propulsion, long RadiusMm, long MaxSpeedMmPerS, long AccelMmPerS2,
    long TurnRatePerS, long Hull, long Crew, string Citation)
{
    /// <summary>Embarked personnel slots (one land entity = one person = one slot). 0 = cannot carry troops.</summary>
    public long Capacity { get; init; }
    public WeaponSpec[] Weapons { get; init; } = Array.Empty<WeaponSpec>();
    /// <summary>Mast / superstructure height: observer eye height and the height at which the vessel is seen.</summary>
    public long HeightMm { get; init; } = 8_000;
    public long SightMm { get; init; } = 3_000_000;
    /// <summary>Defence term against direct fire (hit chance = accuracy / (accuracy + evasion), clamped).</summary>
    public long Evasion { get; init; } = 20;
    /// <summary>Detection-range multiplier (percent) for observers looking at this vessel: hulls are large.</summary>
    public long SignaturePercent { get; init; } = 300;
    // ---- [RAM] D-081 ramming (lane RAM). Lane N's 30 s RamCooldownMs is REMOVED (superseded per D-081 RM-R: the run-up is
    // the physical mechanism a timer only approximated). Every value is a PLACEHOLDER; the rationale is on DomainProfiles.Galley.
    /// <summary>Ram: hull damage of a PERPENDICULAR strike whose normal closing speed equals MaxSpeedMmPerS; every ram effect
    /// scales with the kinetic energy of the normal component, (v_n / MaxSpeed)^2 (<see cref="RamPhysics"/>). 0 = no ram.</summary>
    public long RamDamage { get; init; }
    /// <summary>Ramming speed: the closing speed a contact needs to be a ram (D-081 RM3), and the normal impact speed a ram
    /// needs to breach a side. Below it a bow contact is a collision. Must be in (0, MaxSpeed] for a ram class.</summary>
    public long RamSpeedMmPerS { get; init; }
    /// <summary>People killed aboard the struck hull by a perpendicular breach at MaxSpeed (energy-scaled like the hull damage).</summary>
    public long RamCrewDamage { get; init; }
    /// <summary>Share (permille) of the ram's energy-scaled damage taken by the rammer's own bow, on every bow contact.</summary>
    public long RamBowDamagePermille { get; init; }
    /// <summary>Reach of the beak beyond the hull disc (contact distance) and its full embedding depth in a breach, mm.</summary>
    public long RamLengthMm { get; init; }
    // ---- [RAM] end
    /// <summary>Per-person melee value of the people aboard in a boarding fight.</summary>
    public long BoardingMelee { get; init; } = 30;
    /// <summary>Per-person defence value in a boarding fight.</summary>
    public long BoardingDefence { get; init; } = 30;
    /// <summary>Time per person to embark / disembark (one boat lane per hull).</summary>
    public long EmbarkMsPerPerson { get; init; } = 1_000;
    public long DisembarkMsPerPerson { get; init; } = 2_000;
    /// <summary>Damage control (Rally): hull repaired per second from a bounded pool of RepairPoolPercent of Hull.</summary>
    public long RepairPerS { get; init; } = 1;
    public long RepairPoolPercent { get; init; } = 20;

    public CapFamily Cap => CapFamily.Ships;

    public void Validate()
    {
        if (RadiusMm <= 0 || RadiusMm > Disc.MaxRadiusMm) throw new ArgumentException($"vessel {Name}: radius");
        if (MaxSpeedMmPerS <= 0 || AccelMmPerS2 <= 0 || TurnRatePerS <= 0) throw new ArgumentException($"vessel {Name}: kinematics");
        if (Hull <= 0 || Crew <= 0 || Capacity < 0) throw new ArgumentException($"vessel {Name}: hull/crew/capacity");
        if (HeightMm <= 0 || SightMm <= 0 || Evasion < 0 || SignaturePercent <= 0) throw new ArgumentException($"vessel {Name}: sensors");
        if (RamDamage < 0 || BoardingMelee < 0 || BoardingDefence < 0) throw new ArgumentException($"vessel {Name}: ram/boarding");
        if (RamDamage > 0 && (RamSpeedMmPerS <= 0 || RamSpeedMmPerS > MaxSpeedMmPerS || RamCrewDamage < 0 || RamBowDamagePermille < 0
                              || RamBowDamagePermille >= 1000 || RamLengthMm <= 0))
            throw new ArgumentException($"vessel {Name}: ram (ramming speed in (0, MaxSpeed], crew damage >= 0, bow share in [0, 1000), beak > 0)");
        if (EmbarkMsPerPerson <= 0 || DisembarkMsPerPerson <= 0 || RepairPerS < 0 || RepairPoolPercent < 0) throw new ArgumentException($"vessel {Name}: transport/repair");
        foreach (var w in Weapons) w.Validate();
    }
}

/// <summary>
/// An airframe class. One aircraft entity = one airframe (CAP-3 counted object). Fixed-wing aircraft cannot fly below
/// <see cref="MinSpeedMmPerS"/> (no hovering); rotary-wing aircraft may hover but burn fuel doing so. Fuel is flight time
/// (ms at cruise); every airborne step burns at least one unit, so every sortie is bounded.
/// </summary>
public sealed record AirframeClass(
    int ClassKey, string Name, AirKind Kind, long MinSpeedMmPerS, long CruiseMmPerS, long MaxSpeedMmPerS, long TurnRatePerS,
    long AltitudeMm, long EnduranceMs, int Sorties, long TurnaroundMs, long Hp, long Crew, string Citation)
{
    public WeaponSpec[] Weapons { get; init; } = Array.Empty<WeaponSpec>();
    public long AccelMmPerS2 { get; init; } = 20_000;
    public long SightMm { get; init; } = 3_000_000;
    public long Evasion { get; init; } = 40;
    public long SignaturePercent { get; init; } = 200;
    /// <summary>Rotary wing: burn rate while hovering, percent of the cruise burn.</summary>
    public long HoverBurnPercent { get; init; } = 120;

    public CapFamily Cap => Kind == AirKind.FixedWing ? CapFamily.FixedWing : CapFamily.Helicopters;

    public void Validate()
    {
        if (Kind == AirKind.FixedWing && MinSpeedMmPerS <= 0) throw new ArgumentException($"airframe {Name}: fixed wing needs a positive minimum speed (no hovering)");
        if (Kind == AirKind.Rotary && MinSpeedMmPerS != 0) throw new ArgumentException($"airframe {Name}: rotary minimum speed is 0");
        if (CruiseMmPerS <= 0 || CruiseMmPerS < MinSpeedMmPerS || MaxSpeedMmPerS < CruiseMmPerS) throw new ArgumentException($"airframe {Name}: speeds");
        if (TurnRatePerS <= 0 || AltitudeMm <= 0 || EnduranceMs <= 0 || Sorties <= 0 || TurnaroundMs < 0) throw new ArgumentException($"airframe {Name}: flight");
        if (Hp <= 0 || Crew <= 0 || AccelMmPerS2 <= 0 || SightMm <= 0 || Evasion < 0 || SignaturePercent <= 0 || HoverBurnPercent <= 0) throw new ArgumentException($"airframe {Name}: values");
        foreach (var w in Weapons) w.Validate();
    }
}

/// <summary>
/// Numeric rules of the naval / air / combined-arms modules. Every value is a PLACEHOLDER (invented for the prototype;
/// no ratified number exists). Integer only; rates are per second and integrated over the step (law 3).
/// </summary>
public sealed record DomainRules
{
    /// <summary>Grappling reach beyond touching hulls, mm.</summary>
    public long BoardingReachMm { get; init; } = 5_000;
    /// <summary>A boarder must have slowed to at most this speed to grapple.</summary>
    public long BoardingSpeedMmPerS { get; init; } = 2_500;
    /// <summary>Boarding fight: expected casualties inflicted per second = fighters * hitChance * this / 1000.</summary>
    public long BoardingLethalityPermillePerS { get; init; } = 20;
    /// <summary>A boarded vessel is captured when the people aboard fall to at most StrikeNum/StrikeDen of those aboard when
    /// the fight began, and the boarders outnumber them.</summary>
    public long StrikeNum { get; init; } = 1;
    public long StrikeDen { get; init; } = 4;
    /// <summary>Fraction of the people aboard a sinking vessel / downed aircraft who survive (counted Escaped). 0 = all lost.</summary>
    public long ShipwreckSurvivalNum { get; init; } = 0;
    public long ShipwreckSurvivalDen { get; init; } = 1;
    public long BailoutSurvivalNum { get; init; } = 0;
    public long BailoutSurvivalDen { get; init; } = 1;
    /// <summary>A land entity boards a stopped transport within this distance of the hull centre.</summary>
    public long EmbarkRangeMm { get; init; } = 60_000;
    /// <summary>A transport lands troops onto passable land cells within this distance of the hull centre.</summary>
    public long LandingRangeMm { get; init; } = 80_000;
    /// <summary>Embarking / landing needs the hull at most this fast ("stopped").</summary>
    public long StoppedSpeedMmPerS { get; init; } = 500;
    /// <summary>A freshly landed entity counts as exposed (fully visible) for this long.</summary>
    public long LandingExposureMs { get; init; } = 20_000;
    /// <summary>Direct-fire hit chance falls linearly with range to this fraction (permille) at maximum range.</summary>
    public long RangeFalloffFloorPermille { get; init; } = 500;
    /// <summary>Bingo reserve: an aircraft turns for base when its fuel would leave less than this after the flight home.</summary>
    public long AirReserveMs { get; init; } = 60_000;
    /// <summary>An aircraft lands when it is within this distance of its base.</summary>
    public long AirLandRadiusMm { get; init; } = 100_000;
    /// <summary>An aircraft aborts to base at or below this fraction (permille) of its airframe HP.</summary>
    public long DamagedAbortPermille { get; init; } = 500;
    /// <summary>Vessels keep station this many hull radii apart in line ahead.</summary>
    public long LineSpacingRadii { get; init; } = 8;
    /// <summary>Preferred engagement range as a fraction (permille) of the main battery's maximum range.</summary>
    public long PreferredRangePermille { get; init; } = 700;

    // ---- [RAM] D-081 ramming rules (lane RAM). PLACEHOLDER values, rationale in reports/H-RAM.md.
    /// <summary>Below this impact angle (between the closing velocity and the struck hull's keel line) a ram glances off
    /// whatever its speed: the beak skids along the planking. PLACEHOLDER 20 deg.</summary>
    public int RamGlanceAngle { get; init; } = FixedTrig.FromDegrees(20);
    /// <summary>The ram directive starts an attack run only when the rammer lies at least this far off the target's keel line
    /// (it manoeuvres for the beam); a run is aborted below <see cref="RamGlanceAngle"/>. PLACEHOLDER 45 deg.</summary>
    public int RamAttackAngle { get; init; } = FixedTrig.FromDegrees(45);
    /// <summary>Backing water (astern) acceleration as a fraction (permille) of the forward acceleration: the time to back an
    /// embedded beak out of a breach. PLACEHOLDER 500.</summary>
    public long BackingAccelPermille { get; init; } = 500;
    // ---- [RAM] end

    /// <summary>Wind on/off. Off = sail behaves like any other propulsion (factor 1).</summary>
    public bool Wind { get; init; }
    /// <summary>Direction the wind blows FROM (angle units).</summary>
    public int WindFrom { get; init; }
    /// <summary>Sail no-go zone half-width around the wind: inside it a sailing vessel makes no headway.</summary>
    public int SailNoGo { get; init; } = FixedTrig.FromDegrees(60);

    /// <summary>
    /// Sail speed factor (permille) for a heading: 0 inside the no-go zone; 500 close-hauled just outside it, rising linearly
    /// to 1000 on a beam reach (90 degrees off the wind); 1000 to 135 degrees; falling linearly to 750 dead downwind (180).
    /// PLACEHOLDER polar (shape after square-rigger polars: no headway inside ~60°, about half speed close-hauled).
    /// </summary>
    public long SailFactor(int heading)
    {
        if (!Wind) return 1000;
        long off = FixedTrig.Separation(heading, WindFrom);              // 0 = heading straight into the wind
        long beam = FixedTrig.Quarter, broad = FixedTrig.Quarter + FixedTrig.Eighth;
        if (off <= SailNoGo) return 0;
        if (off < beam) return 500 + FixedMath.MulDivFloor(off - SailNoGo, 500, beam - SailNoGo);
        if (off <= broad) return 1000;
        return 1000 - FixedMath.MulDivFloor(off - broad, 250, FixedTrig.HalfTurn - broad);
    }

    public void Validate()
    {
        if (BoardingReachMm < 0 || BoardingSpeedMmPerS <= 0 || BoardingLethalityPermillePerS <= 0) throw new ArgumentException("boarding");
        if (StrikeNum < 0 || StrikeDen <= 0 || StrikeNum >= StrikeDen) throw new ArgumentException("strike colours fraction");
        if (ShipwreckSurvivalNum < 0 || ShipwreckSurvivalDen <= 0 || ShipwreckSurvivalNum > ShipwreckSurvivalDen) throw new ArgumentException("shipwreck survival");
        if (BailoutSurvivalNum < 0 || BailoutSurvivalDen <= 0 || BailoutSurvivalNum > BailoutSurvivalDen) throw new ArgumentException("bailout survival");
        if (EmbarkRangeMm <= 0 || LandingRangeMm <= 0 || StoppedSpeedMmPerS < 0 || LandingExposureMs < 0) throw new ArgumentException("transport");
        if (RangeFalloffFloorPermille < 0 || RangeFalloffFloorPermille > 1000) throw new ArgumentException("falloff");
        if (AirReserveMs < 0 || AirLandRadiusMm <= 0 || DamagedAbortPermille < 0 || DamagedAbortPermille > 1000) throw new ArgumentException("air");
        if (LineSpacingRadii < 3 || PreferredRangePermille <= 0 || PreferredRangePermille > 1000) throw new ArgumentException("formation");
        if (SailNoGo < 0 || SailNoGo >= FixedTrig.Quarter) throw new ArgumentException("sail no-go zone");
        if (RamGlanceAngle < 0 || RamGlanceAngle > FixedTrig.Quarter || RamAttackAngle < RamGlanceAngle || RamAttackAngle > FixedTrig.Quarter
            || BackingAccelPermille <= 0 || BackingAccelPermille > 1000) throw new ArgumentException("ram rules");   // [RAM]
    }
}

/// <summary>Per-battle configuration of the naval / air / combined-arms modules (optional; null = defaults).</summary>
public sealed record MultiDomainSetup
{
    public DomainRules Rules { get; init; } = new();
    /// <summary>Air base point per side (null = the centre of the side's exit edge). Aircraft take off from and land at it.</summary>
    public Vec2L[]? AirBases { get; init; }
}

/// <summary>Naval capabilities a polity has (COMPUTED state in the campaign: technologies/resources), never a date.</summary>
[Flags]
public enum NavalCapability
{
    None = 0,
    Oars = 1,
    Sails = 2,
    Gunpowder = 4,
    SteamPower = 8,
    Turrets = 16,
    Aviation = 32,
}

public enum VesselRole : byte
{
    Warship = 0,
    Transport = 1,
}
