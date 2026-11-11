namespace Proto.M7.Tactical;

/// <summary>
/// [CB] D-082 DA5 ("Battlefield units"; READING DA5-R (a), applied): a campaign formation of P persons fights as n battle entities,
/// n ≤ its DR24 cap, each standing for the equal share P ÷ n (docs/m7-plan/battle-entity-proposal.md §2). This is the ONE place
/// both resolvers read a formation's persons from (<see cref="FormationSpec.Persons"/>), so the TacticalResolver and the AutoResolver
/// cannot read it differently (D-079 D11: a one-sided term opens a parity gap). Integer only.
///
/// What the resolvers do with it today: nothing but the formation's weight in lane MOR's "fight it won" rule
/// (<see cref="LocalWinMorale"/>, coefficient 0 by default), whose rise is floor(c × B ÷ (B + W)) over the two formations' sizes: with
/// entities standing for different numbers of persons, the entity counts would weigh a broken formation of 5,000 persons fielded as
/// 50 entities like one of 50 persons. Scaling an entity's hit points and output by its share is a PROPOSAL (§3), not built here.
/// </summary>
public static class EntityShare
{
    /// <summary>The persons a formation stands for: <see cref="FormationSpec.Persons"/>, or its entity count when that is null
    /// (one person per entity: the identity for every battle built before D-082 DA5).</summary>
    public static long PersonsOf(FormationSpec s) => s.Persons ?? s.Count;

    /// <summary>Refuses a formation whose persons cannot stand behind its entities: Persons set on a vessel or aircraft formation
    /// (their crews are lane N's personnel ledger), or fewer persons than entities (every entity stands for at least one person).
    /// Called by both resolvers at construction, beside the cap check of the entity count.
    /// [CB, verification round 1, CB-R1-05] It does NOT check that the entity count is the one rule's for those persons
    /// (n = Caps.DirectorCaps.EntitiesOf(class, P)): the battle accepts any n ≤ P within the cap, for example 10 heavy-infantry entities
    /// standing for 40 persons, which the rule never fields (40 persons fight as 40 entities). The one rule holds where formations are
    /// fielded (the roster and both War Sandbox paths); the campaign → battle hand-off of M7.15 must apply it, or this check must be
    /// extended to it (docs/m7-plan/battle-entity-proposal.md §9, PROPOSAL, not built).</summary>
    public static void Validate(FormationSpec s)
    {
        if (s.Persons is not { } p) return;
        if (s.Domain != Domain.Land) throw new ArgumentException($"formation {s.Id}: Persons is for land formations (a vessel's or an aircraft's crew is in the personnel ledger)");
        if (p < s.Count) throw new ArgumentException($"formation {s.Id}: {p} persons cannot stand behind {s.Count} entities (at least one person per entity)");
    }
}
