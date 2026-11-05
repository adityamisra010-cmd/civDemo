using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

public class ReplayAndMetamorphicTests
{
    /// <summary>
    /// GOLDEN (Linux-measured, x64, .NET 10, 2026-11-04): trace hash of the combined-arms battle, seed 42, both sides
    /// KnowledgeAi, default PLACEHOLDER rules. Integer-only code => expected platform-independent, but cross-platform equality
    /// is UNTESTED (O-17). Moves whenever a tactical rule, placeholder or event encoding changes (re-pin with attribution).
    /// </summary>
    public const string GoldenTraceHash = "E53AA685CACD9B206B777DB77F23BE72934DA8FC9B1BEFEDAB23454BF2AC44E3";
    public const string GoldenStateHash = "88F6322C135B38F0E75858F29E2214D43C5A614B65E0FC58A586ED5A1FFE67E1";

    [Fact]
    public void ReplayFromRecordedOrdersReproducesTraceAndResult()
    {
        var live = new TacticalBattle(CombinedArms(42), new KnowledgeAi(), new KnowledgeAi());
        var r1 = TT.RunGuarded(live);
        Assert.NotEmpty(live.Orders);
        var replay = new TacticalBattle(CombinedArms(42), ScriptedController.FromLog(live.Orders, 0), ScriptedController.FromLog(live.Orders, 1));
        var r2 = TT.RunGuarded(replay);
        Assert.Equal(r1.TraceHash, r2.TraceHash);
        Assert.Equal(r1.StateHash, r2.StateHash);
        Assert.Equal(r1.ResultHash(), r2.ResultHash());
        Assert.Equal(live.Orders, replay.Orders);
        Assert.Equal(GoldenTraceHash, r1.TraceHash);
        Assert.Equal(GoldenStateHash, r1.StateHash);
    }

    [Fact]
    public void SameSeedSameBattleDifferentSeedDifferentTrace()
    {
        var a = TT.RunGuarded(new TacticalBattle(CombinedArms(7), new KnowledgeAi(), new KnowledgeAi()));
        var b = TT.RunGuarded(new TacticalBattle(CombinedArms(7), new KnowledgeAi(), new KnowledgeAi()));
        var c = TT.RunGuarded(new TacticalBattle(CombinedArms(8), new KnowledgeAi(), new KnowledgeAi()));
        Assert.Equal(a.TraceHash, b.TraceHash);
        Assert.NotEqual(a.TraceHash, c.TraceHash);
    }

    /// <summary>Metamorphic: permuting the formation input order and every formation's entity input order changes nothing.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void PermutingEntityInputOrderGivesTheIdenticalBattle(ulong perm)
    {
        BattleSetup Explicit(bool permute)
        {
            var baseSetup = CombinedArms(5);
            var probe = new TacticalBattle(baseSetup, PassiveController.Instance, PassiveController.Instance);
            var ents = probe.Entities();
            var forms = baseSetup.Formations.Select(f =>
            {
                var mine = ents.Where(e => e.FormationId == f.Id).Select(e => new EntitySpec(e.Id, e.Pos)).ToArray();
                if (permute)
                {
                    var p = TestSupport.Permutation(mine.Length, perm * 31 + (ulong)f.Id);
                    mine = p.Select(i => mine[i]).ToArray();
                }
                return f with { Entities = mine };
            }).ToArray();
            if (permute)
            {
                var p = TestSupport.Permutation(forms.Length, perm);
                forms = p.Select(i => forms[i]).ToArray();
            }
            return baseSetup with { Formations = forms };
        }
        var a = TT.RunGuarded(new TacticalBattle(Explicit(false), new KnowledgeAi(), new KnowledgeAi()));
        var b = TT.RunGuarded(new TacticalBattle(Explicit(true), new KnowledgeAi(), new KnowledgeAi()));
        Assert.Equal(a.TraceHash, b.TraceHash);
        Assert.Equal(a.StateHash, b.StateHash);
        Assert.Equal(a.ResultHash(), b.ResultHash());
        // and explicit placements identical to the generated slots reproduce the generated battle
        var g = TT.RunGuarded(new TacticalBattle(CombinedArms(5), new KnowledgeAi(), new KnowledgeAi()));
        Assert.Equal(g.TraceHash, a.TraceHash);
    }
}
