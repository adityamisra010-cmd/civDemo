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

    /// <summary>ADR-055: the formation's people as its FormationPersonnelRow records them ("Personnel: 20 adults"), or the
    /// token line when it holds no one; a rival's reads as not known (D-060). Formatting a row field only.</summary>
    public string Personnel { get; init; } = "";

    /// <summary>ADR-055: its pending re-equipment as its ReequipRow records it ("Re-equipping to X: 90 of 180 days"), or "".</summary>
    public string Reequip { get; init; } = "";

    /// <summary>M6.12 (D-F; D-066 A2 item 4): the INERT planned-for-M7 line — the training placeholder and the family's first
    /// promotion "earned in M7" (<see cref="UnitTrainingPlaceholder.Line"/>). Text only: no control, no order, no world read.</summary>
    public string Planned { get; init; } = "";

    /// <summary>M6 E1.5 (D-052 §7: "selecting it opens a combined army detail panel showing the composition"): when the
    /// map's token for this formation stands for more than one — the formations of one owner, one place and one identity the
    /// player knows — the line naming how many and which ("Grouped here: 3 formations of Spearmen (ids 4, 9, 12) - one
    /// token on the map"); "" when it stands alone. A count of formations, never of people.</summary>
    public string Composition { get; init; } = "";
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
    ];

    /// <summary>ADR-055: the headcount line of a formation with no personnel row (the military layer off, or a formation
    /// saved before v39): a token that holds no one. No number.</summary>
    public const string TokenPersonnelText = "Personnel: none - a token formation that holds no one";

    /// <summary>P11: the locked movement line. It is text only: the card offers no Move control and the map reads no
    /// right-click. M6.12 (2026-10-28): "awaiting the Director's ruling" removed — stale wording recorded in
    /// m5-playtest-known-issues.md and DW-018 (movement and Action Points were RULED M7 work, D-052 §4); the shorter line
    /// is also what keeps the card unscrolled at the 1280x800 design window with the training placeholder line added.</summary>
    public const string MovementText = "Movement: not simulated — Battle Layer (M7)";

    /// <summary>D-060: the recorded-fields line of a rival's formation (in place of its experience and army lines).</summary>
    public const string RivalRecordText = "Experience and army: not known to you - it is not your formation";

    /// <summary>The card's statement of the limitation (the same words for every formation).</summary>
    public const string LimitationText =
        "Movement and battle arrive with the Battle Layer (milestone M7). This formation cannot be moved or given "
        + "orders yet: it stays where it is stationed, and it modernizes for free when its civilization enters a new Age.";

    /// <summary>ADR-055: the limitation with the military layer on — it still cannot move, but at its station it can be
    /// re-equipped or disbanded (from the Policy surface's roster).</summary>
    public const string LimitationWithOrdersText =
        "Movement and battle arrive with the Battle Layer (milestone M7). This formation cannot be moved yet: at its "
        + "station it can be re-equipped or disbanded (Policy), and it modernizes for free at each new Age.";

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
        bool orders = RecruitmentQuery.Enabled(cfg);
        string personnel = !mine ? "Personnel: not known to you - it is not your formation"
            : RecruitmentQuery.PersonnelIndex(world.FormationPersonnel, unit.Id) is int pi and >= 0
                ? "Personnel: " + world.FormationPersonnel[pi].Count.Value.ToString(CultureInfo.InvariantCulture) + " adults"
                : TokenPersonnelText;
        string reequip = "";
        if (mine && RecruitmentQuery.PendingReequip(world, unit.Id) is { } pending)
            reequip = "Re-equipping to " + (families?.IdentityByKey(pending.TargetIdentity)?.Name ?? "identity " + pending.TargetIdentity.ToString(CultureInfo.InvariantCulture))
                + ": " + pending.ElapsedDays.ToString(CultureInfo.InvariantCulture) + " of " + pending.RequiredDays.ToString(CultureInfo.InvariantCulture)
                + " days (it fights as " + title + " until then)";
        return new UnitCard(unit.Id, title, owner, mine, familyName, line, ageForm, station, next, orders ? LimitationWithOrdersText : LimitationText)
        {
            Composition = CompositionText(world, unit, title, knowledge ?? PlayerKnowledge.Of(world, player)),
            Personnel = personnel,
            Reequip = reequip,
            Position = PositionText(unit),
            PositionAddsToStation = !AtStationSite(world, unit),
            // D-060: a formation's recorded experience and army membership are its owner's books — a rival's card names the
            // formation, its owner, its line and Age form and where it stands (all of it drawn on the map), never those.
            Experience = mine ? ExperienceText(unit.Experience) : RivalRecordText,
            Army = mine ? ArmyText(unit.Army) : "",
            NotSimulated = NotSimulatedLines,
            Planned = UnitTrainingPlaceholder.Line(family),
            Movement = MovementText,
        };
    }

    /// <summary>M6 E1.5 (D-052 §7): the composition line of <paramref name="unit"/>'s token — the known formations of its
    /// owner at its place (the same settlement, or in the field exactly its position) with its identity, ids ascending;
    /// "" when it is alone (the lens's grouping rule, <see cref="WorldLens.Groups(WorldProjection)"/>, read from the rows).</summary>
    public static string CompositionText(IReadOnlyWorldState world, in MilitaryUnitRow unit, string title, PlayerKnowledge known)
    {
        var ids = new List<int>();
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = world.MilitaryUnits[i];
            if (u.Owner.Value != unit.Owner.Value || u.Identity != unit.Identity || !known.KnowsUnit(u)) continue;
            bool here = unit.Location.Value >= 0 || u.Location.Value >= 0
                ? u.Location.Value == unit.Location.Value
                : u.X == unit.X && u.Y == unit.Y;
            if (here) ids.Add(u.Id);
        }
        if (ids.Count <= 1) return "";
        ids.Sort();
        var list = new System.Text.StringBuilder();
        for (int i = 0; i < ids.Count; i++) list.Append(i == 0 ? "" : ", ").Append(ids[i].ToString(CultureInfo.InvariantCulture));
        return "Grouped here: " + ids.Count.ToString(CultureInfo.InvariantCulture) + " formations of " + title + " (ids " + list
            + ") - one token on the map";
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
