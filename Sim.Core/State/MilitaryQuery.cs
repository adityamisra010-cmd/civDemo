using Sim.Core.Systems.Ages;

namespace Sim.Core.State;

/// <summary>The outcome of modernizing one formation at an Age transition (D-047 Part 3). The value is
/// <see cref="UnitConversionRow.Outcome"/>.</summary>
public enum ModernizationOutcome
{
    /// <summary>Converted to the family's realization at the new Age.</summary>
    Converted = 1,
    /// <summary>The family's line has ended; converted to the explicitly defined generic successor family.</summary>
    ConvertedGeneric = 2,
    /// <summary>No later realization exists yet (or the line ended with a preserve rule): kept unchanged.</summary>
    PreservedNoSuccessor = 3,
    /// <summary>The family is declared not to auto-modernize: kept unchanged.</summary>
    PreservedNotAutoModernize = 4,
}

/// <summary>What happens to one formation if its owner enters <c>ToAge</c>. Identity keys are
/// unit-families.json keys; a preserved formation has From == To.</summary>
public readonly record struct UnitConversionPlan(
    int Unit, PolityId Owner, int FromFamily, int FromIdentity, int ToFamily, int ToIdentity, int ToAge,
    ModernizationOutcome Outcome)
{
    public bool Changes => FromIdentity != ToIdentity;
}

/// <summary>One line of "Your military will modernize": how many formations make the same change.</summary>
public sealed record ConversionSummary(
    UnitIdentity From, UnitIdentity To, ModernizationOutcome Outcome, int Count);

/// <summary>A family-level change between two Ages, computed from the unit-family graph alone
/// (e.g. Spearmen → Pikemen), independent of which formations exist.</summary>
public sealed record FamilyLineChange(
    UnitFamily Family, UnitIdentity? From, UnitIdentity? To, ModernizationOutcome Outcome);

/// <summary>
/// ADR-031 — the military read seam. Pure, read-only. <b>The modernization the
/// AgeTransitionSystem applies is <see cref="PlanFor"/></b>, the same function the preview
/// calls, so the preview the player sees before advancing is exactly what happens
/// (Glass Box; D-047 Part 4 F: "use actual unit-family data").
/// Deterministic: formations are visited in table order; no dictionary, no LINQ, no RNG.
/// </summary>
public static class MilitaryQuery
{
    /// <summary>The polity's formations, in table order.</summary>
    public static MilitaryUnitRow[] Units(IReadOnlyWorldState world, PolityId polity)
    {
        var result = new List<MilitaryUnitRow>();
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
            if (world.MilitaryUnits[i].Owner.Value == polity.Value) result.Add(world.MilitaryUnits[i]);
        return [.. result];
    }

    /// <summary>The polity's modernization log, oldest first.</summary>
    public static UnitConversionRow[] Conversions(IReadOnlyWorldState world, PolityId polity)
    {
        var result = new List<UnitConversionRow>();
        for (int i = 0; i < world.UnitConversions.Count; i++)
            if (world.UnitConversions[i].Owner.Value == polity.Value) result.Add(world.UnitConversions[i]);
        return [.. result];
    }

    /// <summary>
    /// THE MODERNIZATION RULE for one formation entering <paramref name="toAge"/> (ruling 18:
    /// automatic and free). The family's mainline realization at the new Age replaces the
    /// formation's identity when it is a LATER identity. With no later realization the formation is
    /// preserved. When the family's line has ENDED before the new Age, its noSuccessor rule
    /// applies: preserve, or convert to the generic successor family's realization at the new Age
    /// (followed along the generic chain until a realization exists; none found → preserved).
    /// A formation whose identity is unknown to the content (content changed under a save) is
    /// preserved, never destroyed (D-047 Part 3: "do not destroy units merely because their Age
    /// has changed").
    /// </summary>
    public static UnitConversionPlan PlanFor(UnitFamilyContent content, in MilitaryUnitRow unit, int toAge)
    {
        UnitIdentity? identity = content.IdentityByKey(unit.Identity);
        UnitFamily? family = identity is null ? null : content.FamilyByKey(identity.FamilyKey);
        if (identity is null || family is null)
            return Keep(unit, toAge, ModernizationOutcome.PreservedNoSuccessor);
        if (!family.AutoModernize)
            return Keep(unit, toAge, ModernizationOutcome.PreservedNotAutoModernize);

        if (family.RealizationAt(toAge) is { } realization)
        {
            return realization.Age > identity.Age
                ? new UnitConversionPlan(unit.Id, unit.Owner, unit.Family, unit.Identity, family.Key, realization.Key, toAge,
                    ModernizationOutcome.Converted)
                : Keep(unit, toAge, ModernizationOutcome.PreservedNoSuccessor);
        }

        bool ended = family.LineEndsAfterAge is int end && toAge > end;
        UnitFamily current = family;
        while (ended && current.NoSuccessor == NoSuccessorRule.Generic)
        {
            UnitFamily? next = content.FamilyByKey(current.GenericSuccessorFamily);
            if (next is null) break;
            if (next.RealizationAt(toAge) is { } generic)
                return new UnitConversionPlan(unit.Id, unit.Owner, unit.Family, unit.Identity, next.Key, generic.Key, toAge,
                    ModernizationOutcome.ConvertedGeneric);
            current = next;
            ended = current.LineEndsAfterAge is int e2 && toAge > e2;
        }
        return Keep(unit, toAge, ModernizationOutcome.PreservedNoSuccessor);
    }

    /// <summary>The modernization of every formation the polity owns, if it entered
    /// <paramref name="toAge"/>, in table order.</summary>
    public static UnitConversionPlan[] Plan(IReadOnlyWorldState world, UnitFamilyContent content, PolityId polity, int toAge)
    {
        var result = new List<UnitConversionPlan>();
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow unit = world.MilitaryUnits[i];
            if (unit.Owner.Value == polity.Value) result.Add(PlanFor(content, unit, toAge));
        }
        return [.. result];
    }

    /// <summary>"Your military will modernize": the plan for the polity's NEXT Age (empty at the final
    /// Age). Identical to what the transition applies if the polity advances from this state.</summary>
    public static UnitConversionPlan[] ModernizationPreview(
        IReadOnlyWorldState world, AgeContent ages, UnitFamilyContent content, PolityId polity) =>
        AgeQuery.NextAge(world, ages, polity) is { } next ? Plan(world, content, polity, next.Key) : [];

    /// <summary>The preview grouped by (from, to, outcome), in order of first appearance.</summary>
    public static ConversionSummary[] Summarize(UnitFamilyContent content, UnitConversionPlan[] plans)
    {
        var keys = new List<(int From, int To, ModernizationOutcome Outcome)>();
        var counts = new List<int>();
        for (int i = 0; i < plans.Length; i++)
        {
            var key = (plans[i].FromIdentity, plans[i].ToIdentity, plans[i].Outcome);
            int at = keys.IndexOf(key);
            if (at < 0) { keys.Add(key); counts.Add(1); }
            else counts[at]++;
        }
        var result = new List<ConversionSummary>();
        for (int i = 0; i < keys.Count; i++)
        {
            UnitIdentity? from = content.IdentityByKey(keys[i].From);
            UnitIdentity? to = content.IdentityByKey(keys[i].To);
            if (from is null || to is null) continue; // an identity the content no longer knows: nothing to name
            result.Add(new ConversionSummary(from, to, keys[i].Outcome, counts[i]));
        }
        return [.. result];
    }

    /// <summary>For every family, in content order, what its realization at <paramref name="fromAge"/>
    /// becomes at <paramref name="toAge"/> under the modernization rule — the unit-progression graph
    /// the UI draws, computed by <see cref="PlanFor"/> on a notional formation of each family.
    /// A family not yet realized at <paramref name="fromAge"/> reports From = null and the
    /// realization it first gains (To), or null when it still has none.</summary>
    public static FamilyLineChange[] FamilyLineChanges(UnitFamilyContent content, int fromAge, int toAge)
    {
        var result = new FamilyLineChange[content.Families.Count];
        for (int f = 0; f < result.Length; f++)
        {
            UnitFamily family = content.Families[f];
            UnitIdentity? from = family.RealizationAt(fromAge);
            if (from is null)
            {
                result[f] = new FamilyLineChange(family, null, family.RealizationAt(toAge), ModernizationOutcome.PreservedNoSuccessor);
                continue;
            }
            var notional = new MilitaryUnitRow(0, new PolityId(0), family.Key, from.Key, new SettlementId(-1), 0.0, 0.0, 0.0, 0);
            UnitConversionPlan plan = PlanFor(content, notional, toAge);
            result[f] = new FamilyLineChange(family, from, content.IdentityByKey(plan.ToIdentity), plan.Outcome);
        }
        return result;
    }

    private static UnitConversionPlan Keep(in MilitaryUnitRow unit, int toAge, ModernizationOutcome outcome) =>
        new(unit.Id, unit.Owner, unit.Family, unit.Identity, unit.Family, unit.Identity, toAge, outcome);
}
