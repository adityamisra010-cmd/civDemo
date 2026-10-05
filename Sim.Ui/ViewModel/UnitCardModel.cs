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
    string NextAge, string Limitation);

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
/// </summary>
public static class UnitCardModel
{
    /// <summary>The card's statement of the limitation (the same words for every formation).</summary>
    public const string LimitationText =
        "Movement and battle arrive with the Battle Layer (milestone M7). This formation cannot be moved or given "
        + "orders yet: it stays where it is stationed, and it modernizes for free when its civilization enters a new Age.";

    /// <summary>The card for formation <paramref name="unitId"/>, or null when no such formation exists.</summary>
    public static UnitCard? Build(IReadOnlyWorldState world, SimConfig cfg, int unitId, PolityId player, Func<int, string> settlementName)
    {
        MilitaryUnitRow? found = null;
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
            if (world.MilitaryUnits[i].Id == unitId) { found = world.MilitaryUnits[i]; break; }
        if (found is not { } unit) return null;
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
        return new UnitCard(unit.Id, title, owner, mine, familyName, line, ageForm, station, next, LimitationText);
    }

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
/// 2s × 1.5s about its centre, s = 9 px at World zoom, 12 px closer) grown by a small grip margin.</summary>
public static class UnitSelection
{
    public const double GripPx = 3;

    /// <summary>The token half-width the lens uses at <paramref name="zoom"/> (WorldLens.Paint).</summary>
    public static double TokenSize(WorldZoom zoom) => zoom == WorldZoom.World ? 9 : 12;

    /// <summary>The formation whose token contains (x, y) in the last painted lens frame, or -1. Tokens fan out
    /// without overlapping; the last drawn (topmost) wins a tie.</summary>
    public static int HitTest(LensFrame? frame, double x, double y)
    {
        if (frame is null) return -1;
        double s = TokenSize(frame.Zoom);
        for (int i = frame.UnitPlacements.Count - 1; i >= 0; i--)
        {
            UnitPlacement p = frame.UnitPlacements[i];
            if (x >= p.X - s - GripPx && x <= p.X + s + GripPx && y >= p.Y - s * 0.75 - GripPx && y <= p.Y + s * 0.75 + GripPx)
                return p.Id;
        }
        return -1;
    }
}
