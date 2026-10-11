using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.World;

namespace Sim.Ui.ViewModel;

/// <summary>What the unit card shows for one military formation. Every line is read from the world and the
/// unit-family content; <see cref="Limitation"/> states what the formation cannot do in this build.</summary>
public sealed record UnitCard(
    int UnitId, string Title, string Owner, bool Mine, string Family, string Line, string AgeForm, string Station,
    string NextAge, string Limitation)
{
    /// <summary>P11: where the row says it stands — its <c>X</c>, <c>Y</c> (terrain px, the D-047 ruling 17 frame).</summary>
    public string Position { get; init; } = "";

    /// <summary>P11: whether <see cref="Position"/> says more than <see cref="UnitCard.Station"/> — false only for a
    /// formation stationed exactly on its settlement's site cell (every founded Warband), whose station names the place.
    /// The window draws the position line only when it does.</summary>
    public bool PositionAddsToStation { get; init; } = true;

    /// <summary>P11: the row's <c>Experience</c>, as recorded, with what changes it in this build (nothing).</summary>
    public string Experience { get; init; } = "";

    /// <summary>P11: the row's <c>Army</c> membership (0 = none), as recorded, with what forms armies (nothing yet).</summary>
    public string Army { get; init; } = "";

    /// <summary>P11: the figures a player expects of a fighting unit that this build does NOT simulate — each its own
    /// line, never a number (<see cref="UnitCardModel.NotSimulatedLines"/>, the same for every formation).</summary>
    public IReadOnlyList<string> NotSimulated { get; init; } = [];

    /// <summary>P11: the locked movement line (<see cref="UnitCardModel.MovementText"/>): non-interactive — no control on
    /// the card moves the formation, and no right-click on the map orders it.</summary>
    public string Movement { get; init; } = "";
}

/// <summary>
/// THE WARBAND AS AN HONEST UI OBJECT (M5 hardening H1, Director §3). Determined from the code at this commit:
/// <list type="bullet">
/// <item>Formations are rows of <c>MilitaryUnits</c>; worldgen founds one per Empire with a capital
/// (<c>WorldFounding.FoundInitialFormations</c>) and the ONLY system that writes the table afterwards is
/// <c>AgeTransitionSystem</c>, which changes a formation's identity at an Age entry (ADR-031 modernization).
/// Nothing writes a formation's position.</item>
/// <item>There is no movement order: <c>OrderKind</c> is 1..8 (rain bias, labour, sector, construction, tax,
/// research target, Age advance, roads). D-043 B1's universal MobileAgent (position, movement capability,
/// Action Capacity) is ratified but unbuilt; the 2026-10-03 roadmap places movement and battle in M7 (Battle
/// Layer).</item>
/// <item><c>AvailableActionsQuery</c> lists military as a STANDING capability ("Basic fighting") with no order.</item>
/// </list>
/// So a formation is selectable and explained, and the card says plainly that it cannot be moved yet; no
/// control on it pretends otherwise.
/// <para>M5 map workstream P11 — THE DETAIL WINDOW. Beyond H1's lines the card carries the row's recorded position,
/// experience and army membership, one "not simulated" line each for strength, health, attack, Action Points and the
/// headcount (the row holds none of them; no number is ever shown for one), and the LOCKED movement line — text only,
/// pending the Director's ruling (M7, CR-017). Nothing on it is computed beyond formatting a row field or reading the
/// unit-family content (the card field provenance test pins each line to its field).</para>
/// </summary>
public static class UnitCardModel
{
    /// <summary>
    /// P11: THE HONEST STAT BLOCK. <c>MilitaryUnitRow</c> holds no strength, health, attack, Action Points or headcount
    /// (unit-families.json: "NO COMBAT STATISTICS"; a formation "holds no people and no goods"; D-043 A8 defers the
    /// military-stat model; CR-017 §3, Action Capacity, is OPEN), so the card names each and says it is not simulated —
    /// it never shows a number for one. The headcount line is why no token on the map carries a count label.
    /// </summary>
    public static readonly IReadOnlyList<string> NotSimulatedLines =
    [
        "Strength: not simulated",
        "Health: not simulated",
        "Attack: not simulated",
        "Action points: not simulated",
        "Headcount: not simulated - no count",
    ];

    /// <summary>P11: the locked movement line (pending the Director's ruling on the workstream's movement issue: M7 and
    /// CR-017). It is text only: the card offers no Move control and the map reads no right-click.</summary>
    public const string MovementText =
        "Movement: not simulated in this build — scheduled for the Battle Layer (M7), awaiting the Director's ruling";

    /// <summary>The card's statement of the limitation (the same words for every formation).</summary>
    public const string LimitationText =
        "Movement and battle arrive with the Battle Layer (milestone M7). This formation cannot be moved or given "
        + "orders yet: it stays where it is stationed, and it modernizes for free when its civilization enters a new Age.";

    /// <summary>The card for formation <paramref name="unitId"/>, or null when no such formation exists — or (P7) when the
    /// player does not know it (<paramref name="knowledge"/>; null = <see cref="PlayerKnowledge.Of(IReadOnlyWorldState, PolityId)"/>,
    /// all-known until S2a): an unknown formation reads exactly as an id that names nothing.</summary>
    public static UnitCard? Build(IReadOnlyWorldState world, SimConfig cfg, int unitId, PolityId player, Func<int, string> settlementName,
        PlayerKnowledge? knowledge = null)
    {
        MilitaryUnitRow? found = null;
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
            if (world.MilitaryUnits[i].Id == unitId) { found = world.MilitaryUnits[i]; break; }
        if (found is not { } unit) return null;
        if (!(knowledge ?? PlayerKnowledge.Of(world, player)).KnowsUnit(unit)) return null;
        UnitFamilyContent? families = cfg.UnitFamilies;
        UnitIdentity? identity = families?.IdentityByKey(unit.Identity);
        UnitFamily? family = families?.FamilyByKey(unit.Family);
        bool mine = unit.Owner.Value == player.Value;
        string title = identity?.Name ?? "Formation";
        string owner = mine ? "Your formation" : "A rival Empire's formation (polity " + unit.Owner.Value.ToString(CultureInfo.InvariantCulture) + ")";
        string familyName = family?.Name ?? "family " + unit.Family.ToString(CultureInfo.InvariantCulture);
        string line = Line(family, identity);
        string ageForm = identity is null ? "Age form: unknown to this content"
            : "Its Age " + Sim.Ui.Ages.AgePanelModel.Numeral(identity.Age) + " form: " + identity.Name;
        string station = unit.Location.Value >= 0 ? "Stationed at " + settlementName(unit.Location.Value) : "In the field";

        string next = "";
        if (cfg.Ages is { } ages && families is not null)
        {
            if (AgeQuery.NextAge(world, ages, unit.Owner) is { } nextAge)
            {
                UnitConversionPlan plan = MilitaryQuery.PlanFor(families, unit, nextAge.Key);
                string to = families.IdentityByKey(plan.ToIdentity)?.Name ?? title;
                next = "At the next Age (" + nextAge.Name + "): "
                    + (plan.Changes ? "becomes " + to : "stays " + title + " - " + Sim.Ui.Ages.AdvanceFlowModel.OutcomeText(plan.Outcome));
            }
            else next = "Final Age: no further modernization.";
        }
        return new UnitCard(unit.Id, title, owner, mine, familyName, line, ageForm, station, next, LimitationText)
        {
            Position = PositionText(unit),
            PositionAddsToStation = !AtStationSite(world, unit),
            Experience = ExperienceText(unit.Experience),
            Army = ArmyText(unit.Army),
            NotSimulated = NotSimulatedLines,
            Movement = MovementText,
        };
    }

    /// <summary>P11: whether <paramref name="unit"/> is stationed in a settlement and stands exactly on its site cell.</summary>
    private static bool AtStationSite(IReadOnlyWorldState world, in MilitaryUnitRow unit)
    {
        if (unit.Location.Value < 0 || world.Terrain is not { } terrain) return false;
        for (int i = 0; i < world.Settlements.Count; i++)
        {
            if (world.Settlements[i].Id.Value != unit.Location.Value) continue;
            int cell = world.Settlements[i].SiteCell, size = terrain.Size;
            return unit.X == cell % size && unit.Y == cell / size;
        }
        return false;
    }

    /// <summary>P11: the row's position as recorded — its <c>X</c>, <c>Y</c> in map (terrain) px, invariant culture, up to
    /// two decimals.</summary>
    public static string PositionText(in MilitaryUnitRow unit) =>
        "Position: " + unit.X.ToString("0.##", CultureInfo.InvariantCulture) + ", " + unit.Y.ToString("0.##", CultureInfo.InvariantCulture)
        + " on the map";

    /// <summary>P11: the row's experience as recorded (one decimal, as the settlement view shows it). Worldgen founds it at
    /// 0 and the one writer of <c>MilitaryUnits</c> (AgeTransitionSystem) preserves it: nothing in this build earns it.</summary>
    public static string ExperienceText(double experience) =>
        "Experience: " + experience.ToString("0.0", CultureInfo.InvariantCulture) + " - nothing earns it yet";

    /// <summary>P11: the row's army membership as recorded (0 = none); nothing in this build forms an army (D-043 B5's Army
    /// MobileAgent is unbuilt), so a membership other than none is shown as the bare number the row holds.</summary>
    public static string ArmyText(int army) =>
        army == 0 ? "Army: none - no army is formed yet" : "Army: number " + army.ToString(CultureInfo.InvariantCulture);

    /// <summary>The family's mainline from the formation's current identity: "Warband -> Axe warriors -> Bronze swordsmen".</summary>
    public static string Line(UnitFamily? family, UnitIdentity? current)
    {
        if (family is null || current is null) return "";
        var names = new List<string>();
        bool from = false;
        foreach (UnitIdentity i in family.Line)
        {
            if (i.Branch) continue;
            if (i.Key == current.Key) from = true;
            if (from) names.Add(i.Name);
            if (names.Count == 3) break;
        }
        return string.Join(" -> ", names);
    }
}

/// <summary>Picking a formation token on the map: the token rect the world lens drew (<see cref="WorldLens.UnitToken"/>:
/// 2s × 1.5s about its centre, s = 9 px at World zoom, 12 px closer — P2: blended between them across the World ↔
/// Regional cross-fade, as <see cref="UnitPlacement.Size"/> records) grown by a small grip margin.</summary>
public static class UnitSelection
{
    public const double GripPx = 3;

    /// <summary>The token half-width the lens uses at <paramref name="zoom"/> (WorldLens.Paint).</summary>
    public static double TokenSize(WorldZoom zoom) => zoom == WorldZoom.World ? 9 : 12;

    /// <summary>The formation whose token contains (x, y) in the last painted lens frame, or -1.</summary>
    public static int HitTest(LensFrame? frame, double x, double y) =>
        frame is null ? -1 : HitTest(frame.UnitPlacements, frame.Zoom, x, y);

    /// <summary>
    /// P11: THE FORMATION UNDER A SCREEN CLICK, or -1. ADMISSION is the token's plate as drawn (2s × 1.5s about its
    /// centre, s = <see cref="UnitPlacement.Size"/>, or <see cref="TokenSize"/> of <paramref name="zoom"/> for a placement
    /// no paint made) grown by <see cref="GripPx"/>. RANKING is the composite key (token-centre screen distance ASC,
    /// unit id ASC) — the settlement pick's rule (<see cref="SettlementSelection.HitTest(IReadOnlyList{SettlementHit}, double, double)"/>),
    /// never the order the placements were drawn in: tokens sharing an anchor fan out without overlapping
    /// (<see cref="WorldLens.FanSlots"/>), and where two tokens of different anchors overlap (two field formations a hair
    /// apart, a field token beside a station's fan) a click picks the one whose centre is nearer, a bit-exact tie the lower
    /// id — so every row is picked at its own centre whatever the table order (pinned by a tie-dense test). (Before P11 the
    /// last-drawn token won, so an overlapped formation could be unreachable in one table order and not in another.)
    /// </summary>
    public static int HitTest(IReadOnlyList<UnitPlacement> placements, WorldZoom zoom, double x, double y)
    {
        bool found = false;
        int bestId = -1;
        double bestDistSq = double.PositiveInfinity;
        for (int i = 0; i < placements.Count; i++)
        {
            UnitPlacement p = placements[i];
            double s = p.Size > 0 ? p.Size : TokenSize(zoom);   // P2: the size the token was drawn at
            if (!(x >= p.X - s - GripPx && x <= p.X + s + GripPx && y >= p.Y - s * 0.75 - GripPx && y <= p.Y + s * 0.75 + GripPx))
                continue;
            double dx = p.X - x, dy = p.Y - y;
            double distSq = dx * dx + dy * dy;
            if (!found || distSq < bestDistSq || (distSq == bestDistSq && p.Id < bestId))
            {
                found = true;
                bestDistSq = distSq;
                bestId = p.Id;
            }
        }
        return bestId;
    }
}
