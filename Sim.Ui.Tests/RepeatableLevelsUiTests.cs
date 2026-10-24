using Xunit;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-049 (D-063 R4) — THE RESEARCH PANEL FOR A LEVELLED REPEATABLE: the shipped research.json with tuning.repeatableLevels
/// turned on, a stepped real world, and frontier_crops researched three times. The node is never shown complete; it shows
/// "Researched 3×", the next level's cost (BaseCost × 1.2^3), and per operand the next level's benefit and the cumulative
/// benefit — both ResearchQuery's own numbers (RepeatableLevelBenefit / RepeatableContribution) with the operand's meaning.
/// With the switch off the same world shows the node complete, as before. D-063 (tranche 2, 2026-10-24): the shipped
/// research.json now carries the switch ON, so the levelled panel reads the shipped content as it ships and the switch-off
/// case is the same file with only tuning.repeatableLevels turned off (the ADR-049 attribution switch).
/// </summary>
public class RepeatableLevelsUiTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static ResearchContent LevelsOn(SteppedWorldFixture fx)
    {
        Assert.True(fx.Content.Tuning.RepeatableLevels);   // D-063: the shipped switch is on (tranche 2)
        return fx.Content;
    }

    private static ResearchContent LevelsOff(SteppedWorldFixture fx)
    {
        using var stream = Sim.Data.DataFiles.OpenResearch();
        string text = new StreamReader(stream).ReadToEnd();
        const string on = "\"repeatableLevels\": true,";
        Assert.Contains(on, text);   // the shipped switch: on since tranche 2 (D-063)
        return ResearchContentLoader.Load(text.Replace(on, "\"repeatableLevels\": false,"), fx.Session.Config.Goods);
    }

    private static WorldState WithCrops(IReadOnlyWorldState world, ResearchContent c, int level)
    {
        WorldState w = ((WorldState)world).Clone();
        var crops = c.Nodes[c.IndexOfId("frontier_crops")].Key;
        // Every technology known (so the agriculture subtree is exhausted and crops is open), crops among them.
        for (int n = 0; n < c.TechnologyCount; n++)
            if (!ResearchQuery.IsCompleted(w, Me, c.Nodes[n].Key)) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[n].Key));
        // D-063 (tranche 2 ships tuning.ageSweep on): an Age-9 node also waits for every Age-7 node of BOTH trees (ADR-048),
        // so the civics up to Age 7 are known too.
        for (int n = c.TechnologyCount; n < c.Nodes.Count; n++)
            if (c.Nodes[n].AgeNumber <= 7 && !ResearchQuery.IsCompleted(w, Me, c.Nodes[n].Key))
                w.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[n].Key));
        Assert.False(ResearchQuery.SweepBlock(w, c, Me, crops).Blocked);
        w.ResearchLevels.Add(new ResearchLevelRow(Me, crops, level));
        return w;
    }

    [Fact]
    public void ALevelledRepeatable_ShowsResearchedNTimes_NextCost_AndPerLevelAndCumulativeBenefit_NeverComplete()
    {
        ResearchContent c = LevelsOn(fx);
        WorldState w = WithCrops(fx.Session.World, c, 3);
        int i = c.IndexOfId("frontier_crops");
        ResearchSnapshot snap = ResearchSnapshot.Build(w, c, Me);
        ResearchNodeView v = snap.Nodes[i];
        Assert.NotEqual(NodeState.Completed, v.State);
        Assert.True(v.Available);
        RepeatView rep = Assert.IsType<RepeatView>(v.Repeat);
        Assert.Equal(3, rep.Level);
        Assert.Equal("Researched 3×", rep.LevelText);
        Assert.Equal(1.2, rep.CostGrowth);
        Assert.Equal(ResearchQuery.EffectiveCost(w, c, Me, i), rep.NextLevelCost);
        Assert.Equal(c.Nodes[i].BaseCost * Math.Pow(1.2, 3), ResearchQuery.LevelBaseCost(c, i, 4));
        RepeatEffectView e = Assert.Single(rep.Effects);
        Assert.Equal("farming.yieldPerArableKm2PerYear", e.Operand);
        Assert.Equal("land yields more grain", e.Meaning);
        Assert.Equal(3.0, e.Cap);
        Assert.Equal(ResearchQuery.RepeatableLevelBenefit(4, 0.1, 3.0), e.NextLevelBenefit);
        Assert.Equal(ResearchQuery.RepeatableContribution(3, 0.1, 3.0), e.Cumulative);
        Assert.Equal("Land yields more grain: next level +21.87%; all 3 levels +81.3% (cap +300%)",
            ProgressionScreen.LevelEffectText(e, rep.Level));

        // The card's state line names the level count and the next level, never "Known".
        var screen = new ProgressionScreen(c, Me);
        screen.Refresh(w);
        screen.Paint(1600, 1000, ApproxTextMeasure.Instance);
        string line = screen.StateLine(i);
        Assert.DoesNotContain("Known", line);
        Assert.Contains("Researched 3×", line);
        Assert.Contains("level 4", line);

        // The detail panel paints the LEVELS section with the same words.
        screen.Focus(i, jump: true);
        DrawList list = screen.Paint(1600, 1000, ApproxTextMeasure.Instance);
        var texts = new List<string>();
        foreach (DrawCmd cmd in list.Commands) if (cmd is TextCmd t) texts.Add(t.Text);
        string all = string.Join("\n", texts);
        Assert.Contains("LEVELS", all);
        Assert.Contains("Next level cost", all);
        Assert.Contains("next level +21.87%", all);
    }

    [Fact]
    public void WithTheSwitchOff_TheSameWorldShowsTheRepeatableCompleteAsBefore()
    {
        ResearchContent c = LevelsOff(fx);
        Assert.False(c.Tuning.RepeatableLevels);
        WorldState w = WithCrops(fx.Session.World, c, 3);
        int i = c.IndexOfId("frontier_crops");
        ResearchNodeView v = ResearchSnapshot.Build(w, c, Me).Nodes[i];
        Assert.Equal(NodeState.Completed, v.State);
        Assert.Null(v.Repeat);
    }
}
