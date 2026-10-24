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
/// With the switch off (the shipped value) the same world shows the node complete, as before.
/// </summary>
public class RepeatableLevelsUiTests(SteppedWorldFixture fx) : IClassFixture<SteppedWorldFixture>
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static ResearchContent LevelsOn(SteppedWorldFixture fx)
    {
        using var stream = Sim.Data.DataFiles.OpenResearch();
        string text = new StreamReader(stream).ReadToEnd();
        const string off = "\"repeatableLevels\": false,";
        Assert.Contains(off, text);   // the shipped switch: off on this branch
        return ResearchContentLoader.Load(text.Replace(off, "\"repeatableLevels\": true,"), fx.Session.Config.Goods);
    }

    private static WorldState WithCrops(IReadOnlyWorldState world, ResearchContent c, int level)
    {
        WorldState w = ((WorldState)world).Clone();
        var crops = c.Nodes[c.IndexOfId("frontier_crops")].Key;
        // Every technology known (so the agriculture subtree is exhausted and crops is open), crops among them.
        for (int n = 0; n < c.TechnologyCount; n++)
            if (!ResearchQuery.IsCompleted(w, Me, c.Nodes[n].Key)) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, c.Nodes[n].Key));
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
        ResearchContent c = fx.Content;
        Assert.False(c.Tuning.RepeatableLevels);
        WorldState w = WithCrops(fx.Session.World, c, 3);
        int i = c.IndexOfId("frontier_crops");
        ResearchNodeView v = ResearchSnapshot.Build(w, c, Me).Nodes[i];
        Assert.Equal(NodeState.Completed, v.State);
        Assert.Null(v.Repeat);
    }
}
