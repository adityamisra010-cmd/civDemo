using Sim.Core.Systems.Ages;

namespace Sim.Ui.World;

/// <summary>
/// M5 map workstream P11 — THE FORMATION TOKEN'S EMBLEM, KEYED BY THE FORMATION'S CURRENT IDENTITY (the Director's
/// default for the unit-representation issue: one non-anatomical emblem token per formation, provisional per D-043 B1;
/// D-038 C1: no faces, limbs or animation — the token is a plate with strokes on it, never a figure).
///
/// <para>Every part is read from the unit-family content for the identity the row holds NOW (the last Age transition's
/// automatic modernization wrote it, ADR-031), so a modernized formation's token changes with it:</para>
/// <list type="bullet">
/// <item><see cref="Glyph"/> — the ROLE mark: the family the identity belongs to (content's <c>UnitIdentity.FamilyKey</c>),
/// one of the twelve family emblems the lens has drawn since H1. An identity the content does not know falls back to the
/// row's own family (<see cref="Unkeyed"/>).</item>
/// <item><see cref="Form"/> — the FORM pips: the identity's place in its family's line, 1-based, in content order (Age
/// ascending, branch identities where the content lists them), so two identities of one family never share an emblem;
/// 0 when the content does not know the identity (no pip is drawn — nothing is guessed).</item>
/// <item><see cref="Identity"/> — the key the form pips' hand is seeded from: every token of an identity is drawn alike.</item>
/// </list>
///
/// <para>NO COUNT. The emblem carries no number of anything: a formation holds no people (<c>MilitaryUnitRow</c> has no
/// headcount, manpower or strength — WorldState's row docs; unit-families.json "NO COMBAT STATISTICS"), so ledger §24's
/// "internal composition may appear as labels/counts" has nothing to show until recruitment exists (M7). The absence is
/// stated in the legend (<see cref="WorldProjection.Absent"/>) and on the formation's card, never drawn as a guess.</para>
/// </summary>
public readonly record struct UnitEmblem(int Identity, int Glyph, int Form)
{
    /// <summary>The most form pips a token draws: the longest family line in the content (heavy infantry, nine forms).
    /// A longer line would draw this many (the card still names the form).</summary>
    public const int MaxPips = 9;

    /// <summary>The emblem of a formation whose row holds family <paramref name="family"/> and identity
    /// <paramref name="identity"/>, read from <paramref name="content"/> (null content: <see cref="Unkeyed"/>).</summary>
    public static UnitEmblem Of(UnitFamilyContent? content, int family, int identity)
    {
        if (content?.IdentityByKey(identity) is not { } id) return Unkeyed(family) with { Identity = identity };
        UnitFamily? owner = content.FamilyByKey(id.FamilyKey);
        int form = 0;
        if (owner is not null)
            for (int i = 0; i < owner.Line.Count; i++)
                if (owner.Line[i].Key == id.Key) { form = i + 1; break; }
        return new UnitEmblem(id.Key, id.FamilyKey, form);
    }

    /// <summary>The family mark alone (no form pips) — an identity the content does not know, or a caller that has only
    /// the family (the H1 token, command for command).</summary>
    public static UnitEmblem Unkeyed(int family) => new(-1, family, 0);
}
