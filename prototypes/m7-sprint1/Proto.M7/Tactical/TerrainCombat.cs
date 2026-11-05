using Proto.M7.Fixed;
using Proto.M7.Spatial;

namespace Proto.M7.Tactical;

/// <summary>
/// [TR] Lane TR: the terrain coefficients INSIDE the tactical hit-chance equations (DIR5 §2.7; CLAUDE.md law 2 — mechanisms
/// and coefficients inside resolution equations, never free-floating buffs). Pure functions, Q32 / integer only.
///
/// MELEE blow of attacker a (formation F_a) on target t (formation F_t):
///   A = EffMelee(F_a), D = EffDefence(F_t)                               (Slice D/E effective stats)
///   g  = Gradient(cell(a) -&gt; cell(t)) in [-cap, cap]                    (&gt; 0: attacking UPHILL)
///   S  = 1 - beta * g                                                   (slope: the higher side has the advantage)
///   Ia = 1 + chi_a * min(1, delta_a / deltaRef_a)                       (charge impetus: chi = profile Impetus, delta = the
///                                                                       distance a actually covered in this step, deltaRef =
///                                                                       its open-ground sprint per step; terrain slows the
///                                                                       run-up, so it shrinks the impetus by MECHANISM)
///   Fa = Footing(class cell(a)),  Ft = Footing(class cell(t))           (mud / water deny footing, both ways)
///   Ka = c0 + (1 - c0) * Cohesion(F_a),  Kt = c0 + (1 - c0) * Cohesion(F_t)
///   A' = A * (S * Ia * Fa * Ka),  D' = D * (Ft * Kt)                     (Q32 products, floor at each step)
///   p  = clamp(A' / (A' + D'), HitMin, HitMax)
/// MISSILE shot of shooter a that geometrically struck entity s (Slice C DirectFire):
///   A = EffRanged(F_a), D = EffDefence(F_s)
///   g_s = Gradient(cell(s) -&gt; cell(a)) in [-cap, cap]                  (&gt; 0: shooting DOWNHILL)
///   E  = 1 + gamma * g_s
///   p  = clamp(A*E / (A*E + D), HitMin, HitMax) * T                     (T = TerrainSight.FirePath transmission: the cover
///                                                                       the missile must pass, incl. the target's own cell)
/// GRADIENT between two cells: 0 for the same cell; else (h_to - h_from) / |centre_to - centre_from| clamped to [-cap, cap]
///   (cell-centre slope of the flat-topped cells; exact integers, Q32 floor).
/// IDENTITY: on flat Road/Open ground with Impetus 0 every factor is exactly One and p equals the Slice D value bit for bit
///   (the A/(A+D) ratio is evaluated as (A*2^32*f)/((A+D)*2^32...) only when some factor differs from One).
/// TERMINATION (D-tactical.md §4 argument): every factor is &gt; 0 (validated: beta*cap &lt; 1, gamma*cap &lt; 1, footing, order,
///   floor, transmission &gt; 0), so p &gt;= HitMin * T &gt; 0 for every attack: unbounded attacking still consumes HP a.s.
/// </summary>
public static class TerrainCombat
{
    /// <summary>Gradient from cell(from) to cell(to) as a signed Q32 raw value in [-cap, cap] (One = 45 degrees).</summary>
    public static long GradientRaw(TerrainGrid grid, Vec2L from, Vec2L to, Q32 cap)
    {
        CellCoord a = grid.CellOf(from), b = grid.CellOf(to);
        if (a == b || !grid.InBounds(a.X, a.Y) || !grid.InBounds(b.X, b.Y)) return 0;
        long dh = grid.HeightAt(b.X, b.Y) - grid.HeightAt(a.X, a.Y);
        if (dh == 0) return 0;
        long run = Vec2L.Distance(grid.CellCenter(a.X, a.Y), grid.CellCenter(b.X, b.Y));
        long g = FixedMath.MulDivFloor(dh, Q32.OneRaw, run);
        if (g > cap.Raw) g = cap.Raw;
        if (g < -cap.Raw) g = -cap.Raw;
        return g;
    }

    /// <summary>1 - coef * g (g signed Q32 raw). Positive by validation (coef*cap &lt; 1).</summary>
    public static Q32 OneMinus(Q32 coef, long gRaw) => new(Q32.OneRaw - FixedMath.MulDivFloor(coef.Raw, gRaw, Q32.OneRaw));

    /// <summary>1 + coef * g (g signed Q32 raw). Positive by validation (coef*cap &lt; 1).</summary>
    public static Q32 OnePlus(Q32 coef, long gRaw) => new(Q32.OneRaw + FixedMath.MulDivFloor(coef.Raw, gRaw, Q32.OneRaw));

    /// <summary>Cohesion factor c0 + (1 - c0) * cohesion.</summary>
    public static Q32 CohesionFactor(TerrainEffects fx, Q32 cohesion)
        => new(fx.CohesionFloor.Raw + fx.CohesionFloor.Complement().Mul(cohesion).Raw);

    /// <summary>Charge impetus factor 1 + chi * min(1, moved / reference); One when chi = 0 or the entity did not move.</summary>
    public static Q32 ImpetusFactor(Q32 chi, long movedMm, long referenceMm)
    {
        if (chi.Raw == 0 || movedMm <= 0 || referenceMm <= 0) return Q32.One;
        Q32 frac = movedMm >= referenceMm ? Q32.One : Q32.FromRatio(movedMm, referenceMm);
        return new(Q32.OneRaw + chi.Mul(frac).Raw);
    }

    /// <summary>
    /// p = clamp(A*fa / (A*fa + D*fd), min, max), with the Slice D special case A + D &lt;= 0 =&gt; 0 (then clamped). When both
    /// factors are One this is exactly Q32.FromRatio(A, A + D) clamped — the Slice D hit chance.
    /// </summary>
    public static Q32 HitChance(long attack, Q32 fa, long defence, Q32 fd, Q32 hitMin, Q32 hitMax)
    {
        Q32 p;
        if (attack + defence <= 0) p = Q32.Zero;
        else if (fa == Q32.One && fd == Q32.One) p = Q32.FromRatio(attack, attack + defence);
        else
        {
            Int128 an = (Int128)attack * fa.Raw, dn = (Int128)defence * fd.Raw;
            Int128 den = an + dn;
            p = den <= 0 ? Q32.Zero : new Q32((long)(an * Q32.OneRaw / den));   // an, dn >= 0: truncation = floor
        }
        if (p < hitMin) p = hitMin;
        if (p > hitMax) p = hitMax;
        return p;
    }

    /// <summary>The attacker-side melee factor S * Ia * Fa * Ka (each Q32, product floored left to right).</summary>
    public static Q32 MeleeAttackFactor(TerrainEffects fx, long gradientUphillRaw, Q32 impetus, TerrainClass attackerCell, Q32 attackerCohesion)
        => OneMinus(fx.MeleeSlope, gradientUphillRaw).Mul(impetus).Mul(fx.Footing(attackerCell)).Mul(CohesionFactor(fx, attackerCohesion));

    /// <summary>The defender-side melee factor Ft * Kt.</summary>
    public static Q32 MeleeDefenceFactor(TerrainEffects fx, TerrainClass defenderCell, Q32 defenderCohesion)
        => fx.Footing(defenderCell).Mul(CohesionFactor(fx, defenderCohesion));

    /// <summary>The missile factor E = 1 + gamma * g_s (g_s &gt; 0 when shooting downhill).</summary>
    public static Q32 MissileElevationFactor(TerrainEffects fx, long gradientDownhillRaw) => OnePlus(fx.MissileElevation, gradientDownhillRaw);
}
