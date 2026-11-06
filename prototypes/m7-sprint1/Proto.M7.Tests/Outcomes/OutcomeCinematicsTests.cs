using System.Reflection;
using Proto.M7.Tactical;
using Proto.M7.War.Outcomes;
using Proto.M7.War.Outcomes.Presentation;
using Proto.M7.War.Siege;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// DIR5 §5.3 (C35) and D-079 Q12: the presentation reads the authoritative record; an execution is depicted only when the fate is
/// Executed and the content setting shows it; the setting (whose default is the Director's, Q12 OPEN) is presentation-only.
/// </summary>
public class OutcomeCinematicsTests
{
    [Fact]
    public void AnExecutionCue_OnlyForAnExecutedFate_AndTheSettingDecidesShowOrCutAway()
    {
        var w = OutcomeCodecTests.Populated();
        Assert.Contains(w.Fates, f => f.To == (byte)LeaderFate.Executed);
        foreach (var f in w.Fates)
        {
            var show = CinematicSelector.ForLeaderFate(f, ExecutionContent.Show);
            var cut = CinematicSelector.ForLeaderFate(f, ExecutionContent.CutAway);
            bool executed = f.To == (byte)LeaderFate.Executed;
            Assert.Equal(executed, show == CinematicCue.ExecutionShown);
            Assert.Equal(executed, cut == CinematicCue.ExecutionCutAway);
            Assert.NotEqual(CinematicCue.ExecutionShown, cut);
            if (f.To == (byte)LeaderFate.Captive) Assert.Equal(CinematicCue.LeaderTakenCaptive, show);
        }
    }

    [Fact]
    public void TheContentSetting_HasNoDefault_TheSelectorRefusesUnset()
    {
        Assert.Equal(0, (int)default(ExecutionContent));
        Assert.Equal(ExecutionContent.Unset, default(ExecutionContent));
        var f = OutcomeCodecTests.Populated().Fates[^1];
        Assert.Throws<InvalidOperationException>(() => CinematicSelector.ForLeaderFate(f, default));
    }

    [Fact]
    public void TheContentSetting_IsPresentationOnly_NoSimulationTypeReadsIt()
    {
        var sim = typeof(WarOutcomeWorld).Assembly.GetTypes().Where(t => t.Namespace == "Proto.M7.War.Outcomes").ToArray();
        Assert.NotEmpty(sim);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var t in sim)
        {
            foreach (var f in t.GetFields(all)) Assert.NotEqual(typeof(ExecutionContent), f.FieldType);
            foreach (var p in t.GetProperties(all)) Assert.NotEqual(typeof(ExecutionContent), p.PropertyType);
            foreach (var m in t.GetMethods(all).Cast<MethodBase>().Concat(t.GetConstructors(all)))
                foreach (var p in m.GetParameters()) Assert.NotEqual(typeof(ExecutionContent), p.ParameterType);
        }
    }

    [Fact]
    public void Battles_KeepTheOutcomeDistinctions()
    {
        BattleRow Field(BattleEndReason r, int winner) => new(0, 0, (byte)BattleKind.Field, 0, (byte)r, 0, 1, 0, 0, -1, winner);
        BattleRow Siege(SiegeOutcome o) => new(0, 0, (byte)BattleKind.Siege, (byte)BattleMechanism.SiegeHealth, (byte)o, 0, 1, 0, 0, 0, 0);
        Assert.Equal(CinematicCue.Victory, CinematicSelector.ForBattle(Field(BattleEndReason.SideEliminated, 0), 0));
        Assert.Equal(CinematicCue.Defeat, CinematicSelector.ForBattle(Field(BattleEndReason.SideEliminated, 0), 1));
        Assert.Equal(CinematicCue.Inconclusive, CinematicSelector.ForBattle(Field(BattleEndReason.Standoff, -1), 0));      // never a false victory
        Assert.Equal(CinematicCue.Inconclusive, CinematicSelector.ForBattle(Field(BattleEndReason.BothEliminated, -1), 1));
        Assert.Equal(CinematicCue.Surrender, CinematicSelector.ForBattle(Siege(SiegeOutcome.Capitulated), 0));
        Assert.Equal(CinematicCue.Surrender, CinematicSelector.ForBattle(Siege(SiegeOutcome.CapCapitulation), 0));
        Assert.Equal(CinematicCue.Stormed, CinematicSelector.ForBattle(Siege(SiegeOutcome.Stormed), 0));
        Assert.Equal(CinematicCue.SiegeRaised, CinematicSelector.ForBattle(Siege(SiegeOutcome.Relieved), 0));
        Assert.Equal(CinematicCue.SiegeRaised, CinematicSelector.ForBattle(Siege(SiegeOutcome.CapLifted), 0));
        Assert.Equal(CinematicCue.Peace, CinematicSelector.ForWar(new WarRow(0, 0, 9, (byte)WarOutcome.NegotiatedPeace, -1), 0));
        Assert.Equal(CinematicCue.None, CinematicSelector.ForWar(new WarRow(0, 0, -1, (byte)WarOutcome.Ongoing, -1), 0));
        Assert.Equal(CinematicCue.Victory, CinematicSelector.ForWar(new WarRow(0, 0, 9, (byte)WarOutcome.Conquest, 1), 1));
        Assert.Equal(CinematicCue.Defeat, CinematicSelector.ForWar(new WarRow(0, 0, 9, (byte)WarOutcome.Conquest, 1), 0));
    }

    [Fact]
    public void SettlementCues_FollowTheRecordedDisposition()
    {
        var w = OutcomeCodecTests.Populated();
        foreach (var o in w.SettlementOutcomes)
            Assert.Equal((Disposition)o.Disposition switch
            {
                Disposition.Sack => CinematicCue.SettlementSacked,
                Disposition.Raze => CinematicCue.SettlementRazed,
                _ => CinematicCue.SettlementOccupied,
            }, CinematicSelector.ForSettlement(o));
        Assert.Contains(w.SettlementOutcomes, o => CinematicSelector.ForSettlement(o) == CinematicCue.SettlementRazed);
    }
}
