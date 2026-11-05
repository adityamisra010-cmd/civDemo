namespace Proto.M7.Tactical;

/// <summary>Why an order was refused by the action budget (None = accepted). Stable codes.</summary>
public enum BudgetRefusal : byte
{
    None = 0,
    /// <summary>The order's kind was already accepted in this interval ("each kind at most once per interval").</summary>
    KindRepeated = 1,
    /// <summary>N actions already spent in this interval ("total actions &lt;= N").</summary>
    TotalExhausted = 2,
    /// <summary>Attack/Reengage after a Retreat in the same interval.</summary>
    AttackAfterRetreat = 3,
    /// <summary>Heal (Rally) in an interval containing an attack (Attack or Reengage), in either order.</summary>
    HealWithAttack = 4,
}

/// <summary>
/// Strict per-formation action budget for ONE decision interval = one tactical pulse (spec §4; brief §2 "Action economy").
/// N is an INPUT (the Director said "approximately two or three" and did NOT decide which; tests run N = 2 and N = 3).
///
/// Rules (checked in this order; a refused order is recorded by the caller and consumes nothing):
///   1. each order kind at most once per interval (prevents move-move, attack-attack ... chains);
///   2. total accepted actions &lt;= N;
///   3. after a Retreat in the interval, no Attack and no Reengage in that interval (no retreat-then-re-engage chain);
///   4. Heal (Rally) is not allowed in an interval that contains an attack, and an attack is not allowed in an interval that
///      contains a Heal. "Attack" here means Attack OR Reengage (Reengage resumes an attack; treating it as an attack for
///      rule 4 is a design choice under the delegated action-budget prototype authority, reported).
/// Every order kind costs exactly one action (Hold included when explicitly issued; a standing directive costs nothing).
/// The budget is REFRESHED at pulse start (ledger §19: "involved military agents receive refreshed Action Capacity").
///
/// Mapping onto the ratified Action Capacity framework is OPEN (reported, not decided): ledger §17.2 makes AC
/// agent-specific; §19 refreshes it per War Pulse; the M6 training contract's `actionCapacityShare` in (0, 1]
/// (UnitProgressionContent.cs) needs a share -&gt; slot rounding rule; the proposed charter's "1–3 whole actions per
/// refresh" (unit-catalogue…md:228) contains N in {2, 3}; whether this per-interval agency budget is CR-017 §3 option B
/// (exempt from Law 3) is O-8 (open).
/// </summary>
public sealed class ActionBudget
{
    public ActionBudget(int n)
    {
        if (n < 1 || n > 6) throw new ArgumentOutOfRangeException(nameof(n), "budget N must be in [1, 6] (6 = number of order kinds)");
        N = n;
    }

    public int N { get; }
    public int Used { get; private set; }
    private int _kindMask;

    /// <summary>Refresh at the start of a decision interval (pulse start).</summary>
    public void Refresh()
    {
        Used = 0;
        _kindMask = 0;
    }

    public bool HasUsed(OrderKind k) => (_kindMask & (1 << (int)k)) != 0;

    private bool AttackUsed => HasUsed(OrderKind.Attack) || HasUsed(OrderKind.Reengage);

    /// <summary>The refusal an order of this kind WOULD get now (None = acceptable). Pure.</summary>
    public BudgetRefusal Check(OrderKind k)
    {
        if ((byte)k > (byte)OrderKind.Reengage) throw new ArgumentOutOfRangeException(nameof(k));
        if (HasUsed(k)) return BudgetRefusal.KindRepeated;
        if (Used >= N) return BudgetRefusal.TotalExhausted;
        bool isAttack = k == OrderKind.Attack || k == OrderKind.Reengage;
        if (isAttack && HasUsed(OrderKind.Retreat)) return BudgetRefusal.AttackAfterRetreat;
        if (k == OrderKind.Rally && AttackUsed) return BudgetRefusal.HealWithAttack;
        if (isAttack && HasUsed(OrderKind.Rally)) return BudgetRefusal.HealWithAttack;
        return BudgetRefusal.None;
    }

    /// <summary>Check and, when acceptable, spend one action. Returns the refusal (None = accepted).</summary>
    public BudgetRefusal TryAccept(OrderKind k)
    {
        var r = Check(k);
        if (r != BudgetRefusal.None) return r;
        Used++;
        _kindMask |= 1 << (int)k;
        return BudgetRefusal.None;
    }
}
