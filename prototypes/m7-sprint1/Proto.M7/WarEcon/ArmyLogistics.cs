using Proto.M7.Spatial;
using Proto.M7.Tactical;
using Proto.M7.Time;

namespace Proto.M7.WarEcon;

/// <summary>
/// What an army is made of (DIR5 §2.11 "unit composition"). Men by arm (foot, mounted, gun crews); riding horses; draft
/// animals (oxen, mules, draft horses); wagons and trucks of its train; guns; whether the foot carry firearms. Proportions
/// between arms are fixed for the army's life: losses (desertion, disease) are spread pro rata, so per-man rates are the
/// exact weighted means over the initial composition.
/// </summary>
public sealed record ArmyComposition(long Foot, long Mounted, long GunCrew, long Horses, long DraftAnimals, long Wagons,
                                     long Trucks, long Guns, bool Firearms)
{
    /// <summary>Naval force: the lane N vessel class of its hulls (null = a land army). See <see cref="NavalLogistics"/>.</summary>
    public VesselClass? Vessel { get; init; }
    /// <summary>Number of hulls of <see cref="Vessel"/>.</summary>
    public long Hulls { get; init; }
    /// <summary>The ships' companies: hulls x the class's crew (lane N <see cref="VesselClass.Crew"/>).</summary>
    public long Seamen => Vessel is null ? 0 : checked(Hulls * Vessel.Crew);
    public bool Naval => Vessel is not null;

    public long Men => checked(Foot + Mounted + GunCrew + Seamen);

    /// <summary>Null when the composition is legal for the capability; otherwise the reason. Campaign availability is gated
    /// by technology (DIR5 §2.15), so firearms and guns need Gunpowder and trucks need MotorTransport: an army without them
    /// can never consume munitions or fuel ("ancient infantry must not consume oil", DIR5 §2.11).</summary>
    public string? Validate(PolityCapability cap)
    {
        if (Foot < 0 || Mounted < 0 || GunCrew < 0 || Horses < 0 || DraftAnimals < 0 || Wagons < 0 || Trucks < 0 || Guns < 0)
            return "negative count";
        if (Vessel is not null || Hulls != 0)
        {
            string? naval = NavalLogistics.Validate(this, cap);
            if (naval is not null) return naval;
        }
        if (Men <= 0) return "an army needs men";
        if ((Firearms || Guns > 0 || GunCrew > 0) && !cap.Has(WarTech.Gunpowder)) return "firearms, guns and gun crews need Gunpowder";
        if (Trucks > 0 && !cap.Has(WarTech.MotorTransport)) return "trucks need MotorTransport";
        return null;
    }

    /// <summary>Motorised: every man rides (20 per truck) and no animals march with it.</summary>
    public bool Motorised => Trucks > 0 && Trucks * 20 >= Men && Horses == 0 && DraftAnimals == 0;
}

/// <summary>A stretch of an army's line of advance: length in metres, terrain of the ground, the road on it, and whether a
/// navigable waterway or a railway runs along it. Locality is the settlement the army forages from while on it (-1: none).</summary>
public readonly record struct RouteSegment(long LengthM, TerrainClass Terrain, RoadClass Road, bool Waterway, bool Rail, int Locality);

/// <summary>An army's route from its depot (position 0) forward. Positions are metres along the route.</summary>
public sealed class Route
{
    private readonly RouteSegment[] _seg;
    private readonly long[] _start;

    public Route(RouteSegment[] segments)
    {
        if (segments.Length == 0) throw new ArgumentException("a route needs a segment");
        _seg = (RouteSegment[])segments.Clone();
        _start = new long[_seg.Length];
        long at = 0;
        for (int i = 0; i < _seg.Length; i++)
        {
            var s = _seg[i];
            if (s.LengthM <= 0) throw new ArgumentOutOfRangeException(nameof(segments), "segment length must be positive");
            // Water is a sea or deep-river passage (only a naval force moves on it; supply crosses it by boat).
            if (s.Terrain is TerrainClass.Road or TerrainClass.Wall or TerrainClass.Building)
                throw new ArgumentOutOfRangeException(nameof(segments), "segment terrain is the ground (Open, Forest, Jungle, Hills, Mountain, Marsh, Ford) or Water (roads are RoadClass)");
            _start[i] = at;
            at = checked(at + s.LengthM);
        }
        Length = at;
    }

    public long Length { get; }
    public int Count => _seg.Length;
    public RouteSegment this[int i] => _seg[i];
    public long StartOf(int i) => _start[i];

    /// <summary>Index of the segment containing position p (the last one at the route's end).</summary>
    public int SegmentAt(long p)
    {
        if (p < 0 || p > Length) throw new ArgumentOutOfRangeException(nameof(p));
        int i = _seg.Length - 1;
        while (i > 0 && _start[i] > p) i--;
        return i;
    }
}

/// <summary>A march plan for one pulse: metres advanced if unrestricted, the ms spent marching, and wear-weighted metres.</summary>
public readonly record struct MarchPlan(long DistanceM, long MarchMs, long WearM);

/// <summary>The supply line from the depot (position 0) to the army: carriage loss fraction of every shipped unit,
/// throughput per pulse (infrastructure and fleet), animal-hauled and motor-hauled distance, and fuel per shipped gm.</summary>
public readonly record struct SupplyLine(ExactFraction Loss, long CapacityPerPulse, long AnimalHaulM, long MotorHaulM);

/// <summary>Logistics arithmetic (pure integer functions). Every rate is per sim-year and is integrated with the exact
/// dtYears = dtMs / MsPerYear (Law 3); distances in metres; nothing reads a date (Law 4).</summary>
public static class ArmyLogistics
{
    private static readonly long Y = SimInstant.MsPerYear;
    private const long D = SimInstant.MsPerDay;

    /// <summary>Base march speed (m/day on open ground) of the slowest element.</summary>
    public static long BaseSpeed(ArmyComposition c, LogisticsRates r)
    {
        if (c.Naval) return NavalLogistics.PassageSpeed(c.Vessel!, r);
        if (c.Motorised) return r.MarchMotorMPerDay;
        if (c.Wagons > 0 && c.DraftAnimals > 0) return r.MarchOxTrainMPerDay;
        if (c.Foot > 0 || c.GunCrew > 0) return r.MarchFootMPerDay;
        return r.MarchMountedMPerDay;
    }

    /// <summary>Cost (x1000, inverse speed) of marching over a segment: terrain, softened by a road, and for a motorised
    /// army trebled off the road. Roads reduce costs and jungle and mountains increase them (DIR5 §2.11).</summary>
    public static long SegmentCost(RouteSegment s, ArmyComposition c, TerrainLogistics t)
    {
        long terrain = t.MarchCost(s.Terrain);
        if (s.Road == RoadClass.None) return c.Motorised ? checked(terrain * 3) : terrain;
        return GroundCost(s, t);
    }

    /// <summary>Cost (x1000, inverse speed) of animal-drawn movement over a segment: the terrain, softened by its road.
    /// The march (non-motorised) and the supply line's pack and wagon carriage both use it, so a road cuts the carriers'
    /// time on the way and with it the cargo they eat (lane E fix round 1, E-V5; DIR5 §2.11 "roads reduce costs").</summary>
    public static long GroundCost(RouteSegment s, TerrainLogistics t)
    {
        long terrain = t.MarchCost(s.Terrain);
        if (s.Road == RoadClass.None) return terrain;
        // sim.json roads.classes speedFactor CITED (DirtPath 1.0, Track 0.85, Built 0.7); the share of the terrain's excess
        // over open ground that the road leaves is PROPOSAL (a path keeps most of it, an engineered road a quarter).
        (long speedFactor, long keep) = s.Road switch
        {
            RoadClass.DirtPath => (1000L, 800L),
            RoadClass.Track => (850L, 500L),
            _ => (700L, 250L),
        };
        long ground = 1000 + (terrain - 1000) * keep / 1000;
        return Math.Max(1, speedFactor * ground / 1000);
    }

    /// <summary>The march over one pulse of length W from position p, unrestricted: segments are crossed piecewise at their
    /// own speed (rail segments at rail speed when the polity holds Railways); the army halts at the route's end. A land
    /// army halts at the edge of a Water segment and a naval force at the edge of a land segment (embarkation is not
    /// modelled here); a naval force moves at its passage speed with no terrain cost and no wear by distance.</summary>
    public static MarchPlan PlanMarch(Route route, long p, long w, ArmyComposition c, PolityCapability cap, LogisticsRates r, TerrainLogistics t)
    {
        long baseSpeed = BaseSpeed(c, r);
        long left = w, dist = 0;
        Int128 wear = 0;
        int i = route.SegmentAt(p);
        while (left > 0 && p < route.Length)
        {
            var s = route[i];
            if (c.Naval != (s.Terrain == TerrainClass.Water)) break;
            long segEnd = route.StartOf(i) + s.LengthM, rem = segEnd - p;
            bool byRail = !c.Naval && s.Rail && cap.Has(WarTech.Railways);
            long speed = byRail ? r.MarchRailMPerDay : baseSpeed;
            long cost = byRail || c.Naval ? 1000 : SegmentCost(s, c, t);
            // ms to cross rem metres: rem * cost * D / (speed * 1000)
            Int128 need = (Int128)rem * cost * D;
            Int128 per = (Int128)speed * 1000;
            Int128 tRem = (need + per - 1) / per;
            long adv;
            if (tRem <= left) { adv = rem; left -= (long)tRem; }
            else { adv = (long)((Int128)left * per / ((Int128)cost * D)); left = 0; }
            dist += adv;
            wear += (Int128)adv * (byRail || c.Naval ? 0 : t.WearFactor(s.Terrain)) / 1000;
            p += adv;
            if (p >= segEnd && i < route.Count - 1) i++;
            if (adv == 0) break;
        }
        return new MarchPlan(dist, w - left, (long)wear);
    }

    /// <summary>How a supply-line segment carries freight: the best mode its infrastructure AND the polity's capability
    /// allow, and that the army's train can provide (wagons, trucks).</summary>
    public static CarriageMode ModeOf(RouteSegment s, PolityCapability cap, ArmyComposition c)
    {
        if (s.Rail && cap.Has(WarTech.Railways)) return CarriageMode.Rail;
        if (s.Waterway || s.Terrain == TerrainClass.Water) return CarriageMode.Water;
        if (s.Road != RoadClass.None && c.Trucks > 0 && cap.Has(WarTech.MotorTransport)) return CarriageMode.Motor;
        if (s.Road != RoadClass.None && c.Wagons > 0) return CarriageMode.Wagon;
        return CarriageMode.Pack;
    }

    /// <summary>
    /// The supply line over [0, p) for a pulse of length W. Loss: animal carriage eats its cargo at distance/radius on open
    /// ground, scaled by the ground's cost with its road (<see cref="GroundCost"/>: rough ground costs more, a road less), cut
    /// by Magazines; water and rail lose
    /// distance/radius; motor loses no cargo but burns fuel. Capacity per pulse = the least of every crossed segment's
    /// throughput (sim.json road capacities CITED) and the train's fleet: carriers × payload × round trips in W.
    /// </summary>
    public static SupplyLine Evaluate(Route route, long p, long w, ArmyComposition c, PolityCapability cap, LogisticsRates r, TerrainLogistics t)
    {
        var loss = ExactFraction.Zero;
        Int128 capYear = Int128.MaxValue;
        long animalM = 0, motorM = 0;
        for (int i = 0; i < route.Count && route.StartOf(i) < p; i++)
        {
            var s = route[i];
            long len = Math.Min(s.LengthM, p - route.StartOf(i));
            var mode = ModeOf(s, cap, c);
            long terrain = GroundCost(s, t);
            long mag = cap.Has(WarTech.Magazines) ? r.MagazineLossPermille : 1000;
            switch (mode)
            {
                case CarriageMode.Pack:
                    loss += ExactFraction.Ratio((Int128)len * terrain * mag, (Int128)1_000_000_000 * r.PackRadiusKm); animalM += len; break;
                case CarriageMode.Wagon:
                    loss += ExactFraction.Ratio((Int128)len * terrain * mag, (Int128)1_000_000_000 * r.WagonRadiusKm); animalM += len; break;
                case CarriageMode.Water:
                    loss += ExactFraction.Ratio(len, (Int128)1000 * r.WaterRadiusKm); break;
                case CarriageMode.Rail:
                    loss += ExactFraction.Ratio(len, (Int128)1000 * r.RailRadiusKm); break;
                case CarriageMode.Motor:
                    motorM += len; break;
            }
            long segCap = mode switch
            {
                CarriageMode.Rail => r.CapacityRailGmPerYear,
                CarriageMode.Water => r.CapacityWaterGmPerYear,
                _ => s.Road switch
                {
                    RoadClass.None => r.CapacityNoneGmPerYear,
                    RoadClass.DirtPath => r.CapacityDirtGmPerYear,
                    RoadClass.Track => r.CapacityTrackGmPerYear,
                    _ => r.CapacityBuiltGmPerYear,
                },
            };
            if (segCap < capYear) capYear = segCap;
        }
        if (loss > ExactFraction.One) loss = ExactFraction.One;
        Int128 capPulse = capYear == Int128.MaxValue ? Int128.MaxValue : capYear * w / Y;
        if (animalM > 0)
        {
            // Carriers: wagons, plus draft animals not hitched (two per wagon) as pack animals (about 90 kg = 357 gm each).
            long spare = Math.Max(0, c.DraftAnimals - 2 * c.Wagons);
            Int128 perTrip = (Int128)c.Wagons * r.WagonPayloadGm + (Int128)spare * 357;
            Int128 fleet = perTrip * w * r.MarchOxTrainMPerDay / ((Int128)2 * animalM * D);
            if (fleet < capPulse) capPulse = fleet;
        }
        if (motorM > 0)
        {
            Int128 fleet = (Int128)c.Trucks * r.TruckPayloadGm * w * r.MarchMotorMPerDay / ((Int128)2 * motorM * D);
            if (fleet < capPulse) capPulse = fleet;
        }
        long cp = capPulse > long.MaxValue / 4 ? long.MaxValue / 4 : (long)capPulse;
        return new SupplyLine(loss, cp, animalM, motorM);
    }

    /// <summary>Litres of fuel the trucks burn to deliver gm over the motor-hauled distance (round trip), rounded up.</summary>
    public static long CarriageFuel(long gm, SupplyLine line, LogisticsRates r)
    {
        if (gm == 0 || line.MotorHaulM == 0) return 0;
        Int128 num = (Int128)gm * 2 * line.MotorHaulM * r.FuelPerTruck1000Km;
        Int128 den = (Int128)r.TruckPayloadGm * 1_000_000;
        return (long)((num + den - 1) / den);
    }

    /// <summary>Depot quantity needed so that, after the carriage loss, at least <paramref name="atArmy"/> arrives.
    /// Returns -1 when nothing can arrive (loss is total).</summary>
    public static long Gross(long atArmy, ExactFraction loss)
    {
        if (atArmy == 0) return 0;
        if (loss >= ExactFraction.One) return -1;
        Int128 keepNum = (Int128)loss.Den - loss.Num;
        return (long)(((Int128)atArmy * loss.Den + keepNum - 1) / keepNum);
    }

    /// <summary>What arrives of a shipped quantity: floor(shipped × (1 − loss)).</summary>
    public static long Delivered(long shipped, ExactFraction loss)
        => loss >= ExactFraction.One ? 0 : (long)((Int128)shipped * (loss.Den - loss.Num) / loss.Den);
}
