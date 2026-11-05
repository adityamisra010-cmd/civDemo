using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

public class ActionBudgetTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TotalActionsAtMostN(int n)
    {
        var b = new ActionBudget(n);
        OrderKind[] kinds = { OrderKind.MoveTo, OrderKind.Hold, OrderKind.Retreat, OrderKind.Rally };
        int accepted = 0;
        foreach (var k in kinds)
        {
            var r = b.TryAccept(k);
            if (r == BudgetRefusal.None) accepted++;
            else Assert.Equal(BudgetRefusal.TotalExhausted, r);
        }
        Assert.Equal(n, accepted);
        Assert.Equal(n, b.Used);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void EachKindAtMostOncePerInterval(int n)
    {
        foreach (OrderKind k in Enum.GetValues<OrderKind>())
        {
            var b = new ActionBudget(n);
            Assert.Equal(BudgetRefusal.None, b.TryAccept(k));
            Assert.Equal(BudgetRefusal.KindRepeated, b.TryAccept(k));   // move-move, attack-attack, retreat-retreat, heal-heal ...
            Assert.Equal(1, b.Used);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NoAttackOrReengageAfterRetreat(int n)
    {
        var b = new ActionBudget(n);
        Assert.Equal(BudgetRefusal.None, b.TryAccept(OrderKind.Retreat));
        Assert.Equal(BudgetRefusal.AttackAfterRetreat, b.TryAccept(OrderKind.Attack));
        Assert.Equal(BudgetRefusal.AttackAfterRetreat, b.TryAccept(OrderKind.Reengage));
        Assert.Equal(BudgetRefusal.None, b.TryAccept(OrderKind.MoveTo));            // other kinds still fine
        Assert.Equal(2, b.Used);
        // Attack BEFORE Retreat is allowed (only the retreat-then-reengage chain is banned).
        var c = new ActionBudget(n);
        Assert.Equal(BudgetRefusal.None, c.TryAccept(OrderKind.Attack));
        Assert.Equal(BudgetRefusal.None, c.TryAccept(OrderKind.Retreat));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void HealNeverInAnIntervalWithAttack(int n)
    {
        var a = new ActionBudget(n);
        Assert.Equal(BudgetRefusal.None, a.TryAccept(OrderKind.Attack));
        Assert.Equal(BudgetRefusal.HealWithAttack, a.TryAccept(OrderKind.Rally));
        var b = new ActionBudget(n);
        Assert.Equal(BudgetRefusal.None, b.TryAccept(OrderKind.Rally));
        Assert.Equal(BudgetRefusal.HealWithAttack, b.TryAccept(OrderKind.Attack));
        Assert.Equal(BudgetRefusal.HealWithAttack, b.TryAccept(OrderKind.Reengage));
        var c = new ActionBudget(n);
        Assert.Equal(BudgetRefusal.None, c.TryAccept(OrderKind.Reengage));
        Assert.Equal(BudgetRefusal.HealWithAttack, c.TryAccept(OrderKind.Rally));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void RefreshRestoresTheBudget(int n)
    {
        var b = new ActionBudget(n);
        b.TryAccept(OrderKind.Retreat);
        Assert.Equal(BudgetRefusal.AttackAfterRetreat, b.Check(OrderKind.Attack));
        b.TryAccept(OrderKind.MoveTo);
        Assert.NotEqual(BudgetRefusal.None, b.Check(OrderKind.Attack));
        b.Refresh();
        Assert.Equal(0, b.Used);
        Assert.Equal(BudgetRefusal.None, b.TryAccept(OrderKind.Attack));
        Assert.Equal(BudgetRefusal.None, b.TryAccept(OrderKind.MoveTo));
    }

    [Fact]
    public void N3AllowsAThirdDistinctActionN2DoesNot()
    {
        var two = new ActionBudget(2); var three = new ActionBudget(3);
        foreach (var b in new[] { two, three }) { b.TryAccept(OrderKind.MoveTo); b.TryAccept(OrderKind.Attack); }
        Assert.Equal(BudgetRefusal.TotalExhausted, two.TryAccept(OrderKind.Hold));
        Assert.Equal(BudgetRefusal.None, three.TryAccept(OrderKind.Hold));
    }

    [Fact]
    public void BudgetNIsValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActionBudget(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActionBudget(7));
    }

    /// <summary>In-battle: refused orders are RECORDED in the order log with their reason, per N; the budget refreshes each pulse.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void InBattleRefusalsAreRecorded(int n)
    {
        var rules = new TacticalRules { ActionBudgetN = n };
        var setup = Meeting(UnitProfiles.HeavyInfantry, 10, UnitProfiles.HeavyInfantry, 10, 5, rules);
        var s0 = new ScriptedController()
            .Add(0, new OrderRequest(1, OrderKind.MoveTo, 0, -1, Cell(16, 16)))
            .Add(0, new OrderRequest(1, OrderKind.MoveTo, 1, -1, Cell(17, 16)))     // kind repeated
            .Add(0, new OrderRequest(1, OrderKind.Retreat, 2))
            .Add(0, new OrderRequest(1, OrderKind.Reengage, 3, 2))                  // after retreat / or total exhausted
            .Add(0, new OrderRequest(1, OrderKind.Hold, 4))                         // 3rd action: N=3 only
            .Add(1, new OrderRequest(1, OrderKind.Reengage, 0, 2));                 // next pulse: budget refreshed, retreat directive => OK
        var b = new TacticalBattle(setup, s0, PassiveController.Instance);
        b.RunUntilPulse(2);
        var log = b.Orders.Where(o => o.Side == 0).ToArray();
        Assert.Equal(6, log.Length);
        Assert.True(log[0].Accepted);
        Assert.Equal(BudgetRefusal.KindRepeated, log[1].Budget);
        Assert.True(log[2].Accepted);
        Assert.Equal(n == 2 ? BudgetRefusal.TotalExhausted : BudgetRefusal.AttackAfterRetreat, log[3].Budget);
        if (n == 2) Assert.Equal(BudgetRefusal.TotalExhausted, log[4].Budget); else Assert.True(log[4].Accepted);
        // pulse 1: refreshed budget; with N=3 the pulse-0 Hold replaced the Retreat directive => nothing to re-engage.
        if (n == 2) Assert.True(log[5].Accepted);
        else Assert.Equal(OrderInvalid.NothingToReengage, log[5].Invalid);
        Assert.Equal(1, log[5].Pulse);
    }
}
