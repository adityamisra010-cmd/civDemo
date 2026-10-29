using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;

namespace Sim.Core.Systems.Military;

/// <summary>
/// ADR-055 §7 — THE AI'S LEGAL-ONLY RECRUITMENT (unit charter §6.8, §15.7; behind sim.json military.aiRecruitment). Not a
/// system: it returns at most ONE RecruitFormation order per AI polity per turn, through the player's verb and the player's
/// predicate (RecruitmentQuery.CheckRecruit on the world's own published stocks), so an attempted illegal order cannot be
/// produced and the log replays.
///
/// THE RULE: an AI polity recruits when its NEXT Age has a Military Realization milestone (an ages.json <c>formations</c>
/// fact — aN_formation, a5_heavy_infantry) that is UNMET and some identity that fact counts (realized at minIdentityAge or
/// later, of the fact's family if it names one) is legal at one of its settlements now. The pick is the composite integer
/// key (identity Age ascending — the least advanced identity that satisfies the milestone, so the policy never spends more
/// than the milestone asks; identity key ascending; settlement id ascending): total, tie-free, no double compared. Reading
/// the next Age here is AI GOAL-SETTING (the AgeAdvancePolicy precedent), never an input of the recruitment predicate,
/// which does not read the Age (Law 4). It reads only the polity's own settlements, stocks, people and completed
/// knowledge — no rival's state (the P7 leak gate).
/// </summary>
public static class AiMilitaryPolicy
{
    /// <summary>The recruitment the AI issues for <paramref name="polity"/> this turn, or null.</summary>
    public static OrderRecord? Decide(IReadOnlyWorldState world, SimConfig cfg, PolityId polity)
    {
        if (cfg.Military is not { Enabled: true, AiRecruitment: true } || cfg.Ages is not { } ages || cfg.UnitFamilies is not { } families) return null;
        if (!EmpireQuery.TryGetCommandSource(world, polity, out CommandSource source) || source != CommandSource.Ai) return null;
        if (AgeQuery.NextAge(world, ages, polity) is not { Entry: { } entry }) return null;
        AgeMilestone? wanted = UnmetFormationMilestone(world, polity, entry);
        if (wanted is null) return null;
        return Choose(world, cfg, families, polity, wanted.Fact);
    }

    /// <summary>The first unmet formations milestone of the entry requirements (core first, then supporting, content order).</summary>
    public static AgeMilestone? UnmetFormationMilestone(IReadOnlyWorldState world, PolityId polity, AgeEntryRequirements entry)
    {
        foreach (IReadOnlyList<AgeMilestone> list in new[] { entry.Core, entry.Supporting })
            for (int i = 0; i < list.Count; i++)
                if (list[i].Fact.Kind == MilestoneFactKind.Formations && !AgeQuery.Milestone(world, polity, list[i]).Met) return list[i];
        return null;
    }

    /// <summary>The legal candidate with the least composite key (identity Age, identity key, settlement id), or null.</summary>
    public static OrderRecord? Choose(IReadOnlyWorldState world, SimConfig cfg, UnitFamilyContent families, PolityId polity, AgeMilestoneFact fact)
    {
        MilitaryView view = RecruitmentQuery.ViewOf(world);
        bool[]? completed = cfg.Research is { } research ? ResearchQuery.CompletedMask(world, research, polity) : null;
        (int Age, int Key, int Settlement)? best = null;
        for (int f = 0; f < families.Families.Count; f++)
        {
            UnitFamily family = families.Families[f];
            for (int i = 0; i < family.Line.Count; i++)
            {
                UnitIdentity identity = family.Line[i];
                // The settlement-independent items first (cheap, and they refuse most identities for most of the game):
                // counted by the milestone, recruitable, known, a live consumer, no named missing system or planned good.
                if (identity.Recruitment is not { } r || !Counts(fact, identity.Key)) continue;
                if (family.Composition != UnitComposition.ArmyFormation || r.BlockedBy is not null || HasPlanned(r)) continue;
                if (!RecruitmentQuery.KnowledgeMet(cfg.Research, completed, identity)) continue;
                for (int s = 0; s < world.Settlements.Count; s++)
                {
                    SettlementId settlement = world.Settlements[s].Id;
                    var key = (identity.Age, identity.Key, settlement.Value);
                    if (best is { } b && Compare(key, b) >= 0) continue;
                    if (!EmpireQuery.ControlsSettlement(world, polity, settlement)) continue;
                    if (!RecruitmentQuery.CheckRecruit(world, cfg, view, polity, settlement, identity.Key, completed).Ok) continue;
                    best = key;
                }
            }
        }
        return best is { } pick
            ? OrderRecord.From(world.Clock.Turn, polity, OrderKind.RecruitFormation, pick.Settlement, pick.Key)
            : null;
    }

    /// <summary>The composite key order: identity Age, then identity key, then settlement id — integers only.</summary>
    public static int Compare((int Age, int Key, int Settlement) a, (int Age, int Key, int Settlement) b)
    {
        int c = a.Age.CompareTo(b.Age);
        if (c != 0) return c;
        c = a.Key.CompareTo(b.Key);
        return c != 0 ? c : a.Settlement.CompareTo(b.Settlement);
    }

    private static bool HasPlanned(UnitRecruitment r)
    {
        for (int i = 0; i < r.Inputs.Count; i++) if (r.Inputs[i].IsPlanned) return true;
        return false;
    }

    private static bool Counts(AgeMilestoneFact fact, int identity) =>
        fact.IdentityKeys is { } keys && Array.BinarySearch(keys, identity) >= 0;
}
