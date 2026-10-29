using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// ADR-055 — the player's MILITARY orders (RecruitFormation 16, ReequipFormation 17, DisbandFormation 18). Pure. Each order
/// is built only when THE predicate MilitarySystem applies admits it on this world (RecruitmentQuery.CheckRecruit /
/// CheckReequip / CheckCancelReequip / CheckDisband — the predicates the action surface lists by and the AI uses), so the UI
/// never appends an order the simulation would refuse. No rule lives here: this file only asks the predicates and stamps the
/// current turn.
/// P7 (read isolation): every order is issued AS the player of <paramref name="knowledge"/> (PlayerKnowledge — the same seam
/// FoundSettlementOrderFactory is handed), and this file reads NO world table itself. A settlement the player does not know
/// is refused before any predicate is asked; a formation is found only through RecruitmentQuery.IsOwnFormation, which
/// answers "not your formation" alike for a formation that does not exist and for one another polity owns — so neither a
/// hidden settlement nor a hidden formation can change what this surface offers or says.
/// </summary>
public static class MilitaryOrderFactory
{
    /// <summary>The refusal of an order naming a settlement the player does not know — the same words as the predicate's
    /// refusal of a settlement the player does not control, so a hidden settlement reads as any other foreign one.</summary>
    public const string NotYourSettlement = "not a settlement you control";

    /// <summary>RecruitFormation of <paramref name="identityKey"/> at <paramref name="settlement"/>, or null when refused (the
    /// reason is <paramref name="refusal"/>).</summary>
    public static OrderRecord? Recruit(IReadOnlyWorldState world, SimConfig cfg, PlayerKnowledge knowledge, SettlementId settlement, int identityKey, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        if (!knowledge.KnowsSettlement(settlement.Value)) { refusal = NotYourSettlement; return null; }
        RecruitCheck check = RecruitmentQuery.CheckRecruit(world, cfg, RecruitmentQuery.ViewOf(world), knowledge.Player, settlement, identityKey);
        refusal = check.Ok ? null : check.Reason;
        return check.Ok ? OrderRecord.From(world.Clock.Turn, knowledge.Player, OrderKind.RecruitFormation, settlement.Value, identityKey) : null;
    }

    /// <summary>ReequipFormation of formation <paramref name="unitId"/> to <paramref name="targetKey"/>, or null when refused.</summary>
    public static OrderRecord? Reequip(IReadOnlyWorldState world, SimConfig cfg, PlayerKnowledge knowledge, int unitId, int targetKey, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        RecruitCheck check = RecruitmentQuery.CheckReequip(world, cfg, RecruitmentQuery.ViewOf(world), knowledge.Player, unitId, targetKey, out _);
        refusal = check.Ok ? null : check.Reason;
        return check.Ok ? OrderRecord.From(world.Clock.Turn, knowledge.Player, OrderKind.ReequipFormation, unitId, targetKey) : null;
    }

    /// <summary>The cancel of formation <paramref name="unitId"/>'s pending re-equipment (ReequipFormation, Amount 0), or null
    /// when refused (not the player's formation, or nothing pending).</summary>
    public static OrderRecord? CancelReequip(IReadOnlyWorldState world, SimConfig cfg, PlayerKnowledge knowledge, int unitId, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        RecruitCheck check = RecruitmentQuery.CheckCancelReequip(world, cfg, knowledge.Player, unitId);
        refusal = check.Ok ? null : check.Reason;
        return check.Ok ? OrderRecord.From(world.Clock.Turn, knowledge.Player, OrderKind.ReequipFormation, unitId, 0.0) : null;
    }

    /// <summary>DisbandFormation of formation <paramref name="unitId"/>, or null when refused (not the player's, or not stationed).</summary>
    public static OrderRecord? Disband(IReadOnlyWorldState world, SimConfig cfg, PlayerKnowledge knowledge, int unitId, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        RecruitCheck check = RecruitmentQuery.CheckDisband(world, cfg, knowledge.Player, unitId);
        refusal = check.Ok ? null : check.Reason;
        return check.Ok ? OrderRecord.From(world.Clock.Turn, knowledge.Player, OrderKind.DisbandFormation, unitId, 0.0) : null;
    }
}
