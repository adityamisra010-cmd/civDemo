using Xunit;
using Sim.Core;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.Progression;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-057 (M6 CIV, P1/P5/P6) — THE CIVICS PRESENTATION. D-D (RULED): Available and Adopted are distinguished and adoption is
/// M8, so no surface says "Adopted": completed civics are RESEARCHED. The INSTITUTIONS lens and Progression › Civics show the
/// law court Unresearched → Available → Active with its locked reason; the lens shows the regime module, the DERIVED label and
/// the government forms "Available - adoption in M8" (D-066 A10). Every line is InstitutionsQuery's or RegimeQuery's answer.
/// </summary>
public class CivicsPresentationTests
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static (SimConfig Cfg, WorldState World) Founded()
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig wg = WorldgenConfigLoader.Load(stream);
        wg = wg with { SizePx = 256, Siting = wg.Siting with { SettlementCount = 4 } };
        return (cfg, WorldFounding.Found(wg, cfg, 42));
    }

    private static IEnumerable<string> AllText(LensPage page)
    {
        yield return page.Title;
        yield return page.Purpose;
        yield return page.StatusNote;
        foreach (LensSection s in page.Sections)
        {
            yield return s.Heading;
            yield return s.Note;
            foreach (string item in s.Items) yield return item;
        }
    }

    private static void Grant(WorldState w, SimConfig cfg, string id)
    {
        var key = cfg.Research!.Nodes[cfg.Research.IndexOfId(id)].Key;
        if (!ResearchQuery.IsCompleted(w, Me, key)) w.ResearchCompleted.Add(new ResearchCompletedRow(Me, key));
    }

    /// <summary>P1 — the defect fixed: no lens, with or without the session config, labels a civic "Adopted" (the only
    /// "adoption" words left say it is M8's), and the INSTITUTIONS lens's first section is the researched civics.</summary>
    [Fact]
    public void NoLensSaysAdopted_CompletedCivicsAreResearched()
    {
        (SimConfig cfg, WorldState w) = Founded();
        Grant(w, cfg, "law_code");
        foreach (Lens lens in Lenses.All)
            foreach (SimConfig? c in new[] { null, cfg })
                foreach (string text in AllText(Lenses.Page(lens, w, cfg.Research!, Me, c)))
                {
                    Assert.DoesNotContain("Adopted", text, StringComparison.Ordinal);
                    Assert.DoesNotContain("adopted civic", text, StringComparison.OrdinalIgnoreCase);
                }
        LensPage inst = Lenses.Page(Lens.Institutions, w, cfg.Research!, Me, cfg);
        Assert.Equal("Researched civics", inst.Sections[0].Heading);
        Assert.Contains("Written law", inst.Sections[0].Items);
    }

    /// <summary>P6 — the INSTITUTIONS lens shows the court Unresearched (with the locked reason) → Available → Active, each
    /// line InstitutionsQuery.TypeReading's; P5 — the regime module, the derived label and the 33 forms.</summary>
    [Fact]
    public void InstitutionsLens_ShowsTheCourtUnresearchedAvailableActive_TheModule_TheLabel_AndTheForms()
    {
        (SimConfig cfg, WorldState w) = Founded();
        const int court = 6;
        string Line(WorldState world) =>
            Assert.Single(Lenses.Page(Lens.Institutions, world, cfg.Research!, Me, cfg).Sections, s => s.Heading == "Institutions")
                .Items.Single(i => i.StartsWith("Law Court: ", StringComparison.Ordinal));

        Assert.Equal("Law Court: Unresearched - needs Written law", Line(w));
        Grant(w, cfg, "law_code");
        Assert.Equal("Law Court: " + InstitutionsQuery.TypeReading(w, cfg, Me, court)!.Line, Line(w));
        Assert.StartsWith("Law Court: Available", Line(w));
        SettlementId s = LabourActivities.ControlledSettlements(w, Me)[0];
        w.Institutions.Add(new InstitutionRow(1, Me, s, court, w.Clock.Turn, 0.25));
        Assert.Equal("Law Court: Active - 1 founded (maturity 0.25)", Line(w));

        LensPage page = Lenses.Page(Lens.Institutions, w, cfg.Research!, Me, cfg);
        LensSection regime = Assert.Single(page.Sections, x => x.Heading == "Regime modules");
        Assert.Equal("Codified law: present: 1 active law court", regime.Items[0]);
        Assert.StartsWith("Government label: law-giving chiefdom", regime.Items[1]);
        LensSection forms = Assert.Single(page.Sections, x => x.Heading == "Government forms");
        Assert.Equal(33, forms.Items.Count);
        Assert.Equal(forms.Items.Count, forms.Subjects!.Count);
        Assert.DoesNotContain(forms.Items, i => i.EndsWith("Available - adoption in M8", StringComparison.Ordinal));   // none researched
        Grant(w, cfg, "chiefdom");
        LensSection after = Assert.Single(Lenses.Page(Lens.Institutions, w, cfg.Research!, Me, cfg).Sections, x => x.Heading == "Government forms");
        Assert.Contains(cfg.Research!.Nodes[cfg.Research.IndexOfId("chiefdom")].Name + ": Available - adoption in M8", after.Items);
        // researching a form adopted nothing: the label is the court's alone
        Assert.StartsWith("Government label: law-giving chiefdom",
            Assert.Single(Lenses.Page(Lens.Institutions, w, cfg.Research!, Me, cfg).Sections, x => x.Heading == "Regime modules").Items[1]);
    }

    /// <summary>P6 — Progression › Civics: the law_code node's detail names the court with the same reading the lens shows,
    /// and the screen (with the session config) paints the INSTITUTION heading for it.</summary>
    [Fact]
    public void ProgressionCivics_TheLawCodeNode_ShowsTheCourtsStage()
    {
        (SimConfig cfg, WorldState w) = Founded();
        var screen = new ProgressionScreen(cfg.Research!, Me) { Config = cfg };
        screen.Refresh(w);
        int lawCode = cfg.Research!.IndexOfId("law_code");
        Assert.Equal(["Law Court: Unresearched - needs Written law"], screen.InstitutionLines(lawCode));
        Grant(w, cfg, "law_code");
        var after = new ProgressionScreen(cfg.Research!, Me) { Config = cfg };
        after.Refresh(w);
        Assert.Equal(["Law Court: " + InstitutionsQuery.TypeReading(w, cfg, Me, 6)!.Line], after.InstitutionLines(lawCode));
        // the detail paints: focus the node and paint the drawer
        after.Focus(lawCode, jump: true);
        after.Paint(1600, 1000, ApproxTextMeasure.Instance);
        // a node no type realizes has no INSTITUTION lines; without the config there are none (a content preview)
        Assert.Empty(after.InstitutionLines(cfg.Research.IndexOfId("chiefdom")));
        Assert.Empty(new ProgressionScreen(cfg.Research, Me).InstitutionLines(lawCode));
    }
}
