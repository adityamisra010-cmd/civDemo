using Proto.M7.Fixed;

namespace Proto.M7.Tactical;

/// <summary>
/// Battle-condition inputs of a formation that BOTH resolvers consume through the SAME equations (Slice E, spec §5:
/// "experience ... leadership (input), supply (input fraction Q32)"). Without them in the tactical engine the parity battery's
/// low-supply and leadership scenarios would compare the AutoResolver against a tactical battle that ignores the input.
///
/// Every mapping below is a PLACEHOLDER mechanism (invented for the prototype; no ratified value exists):
///  - Experience: integer percent bonus in [0, MaxExperience] on melee, ranged and defence:
///    stat' = floor(stat * (100 + Experience) / 100). The campaign carries experience as `MilitaryUnitRow.Experience`
///    (an IEEE floating-point field, recon-military); the mapping to this integer is an OPEN integration item.
///  - Supply: Q32 fraction in [0, 1] of the formation's supply need met. Attack stats (melee, ranged) are scaled by
///    SupplyFloor + (1 - SupplyFloor) * Supply with SupplyFloor = 1/2 (PLACEHOLDER: an unsupplied formation still fights at
///    half strength). Defence is unaffected. The campaign-side source of the fraction is the Slice F logistics lane (OPEN).
///  - Leadership: integer in [0, MaxLeadership]; morale lost per casualty is scaled by 100 / (100 + Leadership).
/// These are coefficients INSIDE the resolution equations (hit chance, damage, morale loss), not free-floating buffs
/// (CLAUDE.md law 2). At the defaults (0, 0, 1) every equation is the exact identity, so Slice D goldens do not move.
/// </summary>
public static class ConditionMath
{
    public const long PercentDen = 100;
    public const long MaxExperience = 100;
    public const long MaxLeadership = 100;
    /// <summary>Supply floor = 1/2. PLACEHOLDER.</summary>
    public static readonly Q32 SupplyFloor = Q32.FromRatio(1, 2);

    public static void Validate(long experience, long leadership, Q32 supply)
    {
        if (experience < 0 || experience > MaxExperience) throw new ArgumentOutOfRangeException(nameof(experience), $"experience must be in [0, {MaxExperience}]");
        if (leadership < 0 || leadership > MaxLeadership) throw new ArgumentOutOfRangeException(nameof(leadership), $"leadership must be in [0, {MaxLeadership}]");
        if (!supply.IsProbability) throw new ArgumentOutOfRangeException(nameof(supply), "supply must be a fraction in [0, 1]");
    }

    /// <summary>Supply multiplier on attack: SupplyFloor + (1 - SupplyFloor) * supply (Q32, exact floor). Equals One at supply One.</summary>
    public static Q32 SupplyFactor(Q32 supply) => new(SupplyFloor.Raw + SupplyFloor.Complement().Mul(supply).Raw);

    /// <summary>Effective attack stat (melee or ranged): experience bonus, then supply factor; identity at (0, One).</summary>
    public static long Attack(long stat, long experience, Q32 supply)
    {
        long x = FixedMath.MulDivFloor(stat, PercentDen + experience, PercentDen);
        return supply == Q32.One ? x : SupplyFactor(supply).Scale(x);
    }

    /// <summary>Effective defence stat: experience bonus only; identity at 0.</summary>
    public static long Defence(long stat, long experience) => FixedMath.MulDivFloor(stat, PercentDen + experience, PercentDen);

    /// <summary>
    /// Morale lost when <paramref name="killed"/> of <paramref name="start"/> entities die:
    /// floor(killed * fullLoss * 100 / (start * (100 + leadership))). At leadership 0 this is exactly
    /// floor(killed * fullLoss / start), the Slice D rule.
    /// </summary>
    public static long MoraleLoss(long killed, long fullLoss, long start, long leadership)
        => FixedMath.MulDivFloor(killed, checked(fullLoss * PercentDen), checked(start * (PercentDen + leadership)));
}
