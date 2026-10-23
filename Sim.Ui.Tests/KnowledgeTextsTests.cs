using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;
using Sim.Ui.Info;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>
/// M6 review item 4 (2026-10-23) — THE EDUCATIONAL TEXT ON THE SCREEN: every research node with a corpus entry shows its
/// text as plain prose in the research detail panel ("IN HISTORY") and on its info card, and nothing a reader would take
/// for a citation, a source badge or a verification label reaches the screen. Provenance stays in the repository.
/// </summary>
public class KnowledgeTextsTests
{
    private static readonly PolityId Me = UiPlayer.Empire;

    // Markers of citation / bibliography / verification UI. Plain prose such as "tin sources" or "about 1880" is allowed.
    private static readonly string[] Forbidden =
        ["http", "www.", "[", "]", "doi:", "Source:", "Sources:", "source-checked", "search-corroborated", "unverified",
         "partially-checked", "fact-check", "verified", "citation", "Ibid"];

    [Fact]
    public void EveryCorpusNodeHasItsText_AndNoTextCarriesACitationOrAVerificationLabel()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent r = s.Config.Research!;
        Assert.Equal(561, KnowledgeTexts.Count);
        int withText = 0;
        foreach (ResearchNode n in r.Nodes)
        {
            string? text = KnowledgeTexts.For(n.Key);
            if (text is null)
            {
                Assert.Contains(n.Id, new[] { "siege_tower", "crossbow_repeating" });   // the two merges deferred by ADR-045
                continue;
            }
            withText++;
            Assert.InRange(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, 40, 70);
            foreach (string bad in Forbidden) Assert.DoesNotContain(bad, text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(561, withText);
    }

    [Fact]
    public void TheDetailPanelModel_CarriesTheText_AndTheInfoCardPaintsIt()
    {
        UiSession s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        ResearchContent r = s.Config.Research!;
        ResearchNode node = r.Nodes[r.IndexOfId("cereal_cultivation")];
        string text = KnowledgeTexts.For(node.Key)!;
        Assert.NotNull(text);
        ResearchSnapshot snap = ResearchSnapshot.Build(s.World, r, Me);
        Assert.Equal(text, snap.Nodes[r.IndexOf(node.Key)].History);

        var panel = new InfoPanel();
        panel.Open(InfoSubject.Node(node.Key));
        panel.Ensure(s.World, s.Config, Me, ActionQueryContext.None, id => s.Names.Name(id), 0);
        var d = new DrawList();
        panel.Paint(d, ApproxTextMeasure.Instance, EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)), 0, 0, 900, false);
        var painted = new System.Text.StringBuilder();
        foreach (DrawCmd c in d.Commands) if (c is TextCmd t) painted.Append(t.Text).Append(' ');
        string all = string.Join(' ', painted.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string firstWords = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6));
        Assert.Contains(firstWords, all);
        foreach (string bad in Forbidden) Assert.DoesNotContain(bad, all, StringComparison.OrdinalIgnoreCase);
    }
}
