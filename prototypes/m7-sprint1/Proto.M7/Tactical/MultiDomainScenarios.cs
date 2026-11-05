using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>Keeps only the orders of the listed formations from an inner controller (e.g. a land AI for some formations).</summary>
public sealed class OnlyFormations : IFormationController
{
    private readonly IFormationController _inner;
    private readonly long[] _ids;
    private readonly List<OrderRequest> _scratch = new();
    public OnlyFormations(IFormationController inner, params long[] ids) { _inner = inner; _ids = (long[])ids.Clone(); Array.Sort(_ids); }
    public void Decide(SideView view, List<OrderRequest> orders)
    {
        _scratch.Clear();
        _inner.Decide(view, _scratch);
        foreach (var o in _scratch) if (Array.BinarySearch(_ids, o.FormationId) >= 0) orders.Add(o);
    }
}

/// <summary>
/// Synthetic naval / air / combined-arms scenarios (lane N) shared by tests and the bench. Every geometry and force mix is a
/// TEST INPUT, not a claim; vessel / airframe / weapon numbers are the PLACEHOLDERS of <see cref="DomainProfiles"/>.
/// Side 0 exits West, side 1 exits East (as the land scenarios).
/// </summary>
public static class MultiDomainScenarios
{
    /// <summary>A crew profile (personal stats of the people aboard; family key 9 = naval, 11 = aviation — prototype keys).</summary>
    public static UnitProfile Crew(int family) => new(family, family * 100 + 99, "crew(prototype)", CapFamily.Ships,
        Integrity: 30, Melee: 25, Ranged: 0, Defence: 25, RangeCells: 0, SightCells: 10, SpeedNum: 1, SpeedDen: 1, Citation: "PLACEHOLDER (lane N)");

    public static readonly UnitProfile NavalCrew = Crew(9);
    public static readonly UnitProfile AirCrew = Crew(11);

    public static Vec2L Cell(long cx, long cy, long cell) => new(cx * cell + cell / 2, cy * cell + cell / 2);

    public static SideSpec[] Sides(Vec2L[]? obj0 = null, Vec2L[]? obj1 = null) => new[]
    {
        new SideSpec(ExitEdge.West) { Objectives = obj0 ?? Array.Empty<Vec2L>() },
        new SideSpec(ExitEdge.East) { Objectives = obj1 ?? Array.Empty<Vec2L>() },
    };

    public static FormationSpec Fleet(long id, int side, VesselClass v, int hulls, Vec2L anchor)
        => new(id, side, NavalCrew, hulls, anchor) { Domain = Domain.Naval, Vessel = v };

    public static FormationSpec Flight(long id, int side, AirframeClass a, int count)
        => new(id, side, AirCrew, count, default) { Domain = Domain.Air, Airframe = a };

    /// <summary>All-water battle area: 64 x 40 cells of cellMm.</summary>
    public static TerrainGrid OpenSea(long cellMm) => new(64, 40, cellMm, TerrainClass.Water);

    /// <summary>
    /// Two lines of five hulls of one class meeting head-on on open water (cells of cellMm), both sides ordered to Attack at
    /// pulse 0 by the caller. Anchors 32 cells apart, mirror images about the centre line.
    /// </summary>
    public static BattleSetup LineBattle(VesselClass v, long cellMm, ulong seed, DomainRules? rules = null, int hulls = 5)
        => new(OpenSea(cellMm), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), Sides(),
               new[] { Fleet(1, 0, v, hulls, Cell(16, 20, cellMm)), Fleet(2, 1, v, hulls, Cell(47, 20, cellMm)) }, seed)
        { MultiDomain = new MultiDomainSetup { Rules = rules ?? new DomainRules() } };

    public const long AmphibCell = 50_000;
    public static Vec2L A(long cx, long cy) => Cell(cx, cy, AmphibCell);

    /// <summary>
    /// The amphibious combined-arms scenario (64 x 40 cells of 50 m = 3.2 x 2.0 km). Sea for x &lt;= 27; a headland of hills
    /// at x 28-33, rows 3-9 (20 m high); beach (open) x 28-31 below it; forest x 32-37, rows 12-31; open ground beyond.
    /// Side 0 (from the sea): 10 = 2 turret warships; 11 = 2 engine transports carrying 12 = 50 riflemen; 13 = 1 strike
    /// aircraft; 14 = 1 fighter. Side 1 (ashore): 20 = 50 riflemen at the forest edge; 21 = coastal battery (5 gun teams) on
    /// the headland; 22 = air-defence battery (5 teams) behind the forest; 23 = 2 snipers in the forest; 24 = 1 fighter.
    /// Objective of side 0 = the forest behind the beach; of side 1 = its own position (it holds).
    /// </summary>
    public static BattleSetup Amphibious(ulong seed, DomainRules? rules = null)
    {
        var g = new TerrainGrid(64, 40, AmphibCell, TerrainClass.Open);
        for (int y = 0; y < 40; y++)
            for (int x = 0; x < 64; x++)
            {
                if (x <= 27) g.Set(x, y, TerrainClass.Water);
                else if (x <= 33 && y >= 3 && y <= 9) g.Set(x, y, TerrainClass.Hills, 20_000);
                else if (x >= 32 && x <= 37 && y >= 12 && y <= 31) g.Set(x, y, TerrainClass.Forest);
            }
        var forms = new[]
        {
            Fleet(10, 0, DomainProfiles.TurretWarship, 2, A(12, 14)),
            Fleet(11, 0, DomainProfiles.SteamTransport with { Capacity = 30 }, 2, A(6, 22)),
            new FormationSpec(12, 0, Rifles50m, 50, A(6, 22)) { EmbarkedOn = 11 },
            Flight(13, 0, DomainProfiles.StrikeAircraft, 1),
            Flight(14, 0, DomainProfiles.Fighter, 1),
            new FormationSpec(20, 1, Rifles50m, 50, A(34, 20)),
            new FormationSpec(21, 1, DomainProfiles.GunCrew, 5, A(31, 6)) { Weapons = new[] { DomainProfiles.CoastalGun } },
            new FormationSpec(22, 1, DomainProfiles.GunCrew, 5, A(40, 18)) { Weapons = new[] { DomainProfiles.AaGun } },
            new FormationSpec(23, 1, DomainProfiles.Sniper with { RangeCells = 14, SightCells = 16 }, 2, A(33, 26)),
            Flight(24, 1, DomainProfiles.Fighter, 1),
        };
        return new BattleSetup(g, TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(),
            Sides(new[] { A(35, 20) }, new[] { A(34, 20) }), forms, seed)
        {
            MultiDomain = new MultiDomainSetup { Rules = rules ?? new DomainRules() },
            // Firearm-era dispersion: 5 m between soldiers / gun teams instead of the close-order 1.5 m default (TEST INPUT).
            Rules = new TacticalRules { SlotSpacingMm = 5_000 },
        };
    }

    /// <summary>Riflemen with ranges re-expressed for 50 m cells (300 m fire, 700 m sight). TEST INPUT.</summary>
    public static readonly UnitProfile Rifles50m = DomainProfiles.Riflemen with { RangeCells = 6, SightCells = 14 };

    /// <summary>The attacker's scripted operation plan for <see cref="Amphibious"/> (a player script, recorded in the order log).</summary>
    public static ScriptedController AmphibiousPlan() => new ScriptedController()
        .Add(0, new OrderRequest(10, OrderKind.Attack, 0, 21))                  // warships engage the headland battery
        .Add(0, new OrderRequest(13, OrderKind.Attack, 0, 21))                  // strike aircraft bombs it
        .Add(0, new OrderRequest(14, OrderKind.Escort, 0, 13))                  // fighter escorts the strike
        .Add(1, new OrderRequest(11, OrderKind.MoveTo, 0, -1, A(27, 20)))       // transports run in to the beach
        .Add(2, new OrderRequest(10, OrderKind.Bombard, 0, -1, A(33, 18)))      // shift fire to the forest edge, clear of the landing
        .Add(3, new OrderRequest(12, OrderKind.Disembark, 0, -1, A(31, 20)))    // land on the beach
        .Add(6, new OrderRequest(12, OrderKind.Attack, 0, 20))                  // assault the defenders
        .Add(8, new OrderRequest(12, OrderKind.Attack, 0, 23));                 // then go after the snipers

    /// <summary>The defender: land AI for the riflemen only; guns and snipers hold (weapons free); the fighter intercepts.</summary>
    public static IFormationController AmphibiousDefence() => new CompositeController(
        new OnlyFormations(new KnowledgeAi(), 20),
        new DomainAi { AirWeapons = id => id == 24 ? TargetDomains.Air : TargetDomains.None });

    /// <summary>One-hull-per-side helper battle on open sea with explicit hull placements (tests).</summary>
    public static BattleSetup Duel(VesselClass a, Vec2L pa, VesselClass b, Vec2L pb, long cellMm, ulong seed, DomainRules? rules = null)
        => new(OpenSea(cellMm), TerrainCostTable.PlaceholderTestTable(), ConcealmentTable.PlaceholderTestTable(), Sides(),
               new[]
               {
                   Fleet(1, 0, a, 1, pa) with { Entities = new[] { new EntitySpec(1000, pa) } },
                   Fleet(2, 1, b, 1, pb) with { Entities = new[] { new EntitySpec(2000, pb) } },
               }, seed)
        { MultiDomain = new MultiDomainSetup { Rules = rules ?? new DomainRules() } };

    /// <summary>Full-turn helper: an angle in degrees as binary angle units.</summary>
    public static int Deg(long d) => FixedTrig.FromDegrees(d);
}
