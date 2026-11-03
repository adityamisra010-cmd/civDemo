using Xunit;
using Sim.Core;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Worldgen;
using Sim.Ui.Actions;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// D-074 (Director playtest 2026-11-03): "once it was built, i couldnt do anything about it. i thought it would open a policies
/// window"; and its panel said "Its effect is not measured", while in a game with no tax the court had no effect at all. The
/// court's effect is now said honestly in the INSTITUTIONS panel (no tax: no effect yet; under a tax: the felt-burden factor
/// the levy reads), and the End Turn that founds an institution announces it with what it does.
/// </summary>
public class LawCourtClarityTests
{
    private static readonly PolityId Me = UiPlayer.Empire;
    private const int Court = 6;   // sim.json institutions.types law_court

    private static (SimConfig Cfg, WorldState World, SettlementId Site) WithCourt(double maturity)
    {
        SimConfig cfg = UiFounding.ProductionConfig();
        using var stream = Sim.Data.DataFiles.OpenWorldgen();
        WorldgenConfig wg = WorldgenConfigLoader.Load(stream);
        wg = wg with { SizePx = 256, Siting = wg.Siting with { SettlementCount = 4 } };
        WorldState w = WorldFounding.Found(wg, cfg, 42);
        SettlementId s = LabourActivities.ControlledSettlements(w, Me)[0];
        w.Institutions.Add(new InstitutionRow(1, Me, s, Court, w.Clock.Turn, maturity));
        return (cfg, w, s);
    }

    private static InstitutionInstanceView TheCourt(IReadOnlyWorldState w, SimConfig cfg) =>
        Assert.Single(InstitutionsQuery.Instances(w, cfg, Me), v => v.TypeKey == Court);

    [Fact]
    public void TheCourtsEffectLine_SaysNoEffectWithoutATax_AndTheFeltFactorUnderOne()
    {
        (SimConfig cfg, WorldState w, SettlementId s) = WithCourt(0.6);
        double serviceMax = cfg.Needs!.Unrest!.TaxServiceOffsetMax;
        Assert.True(serviceMax > 0.0, "anti-vacuity: the shipped levy reads public services");
        InstitutionInstanceView v = TheCourt(w, cfg);
        Assert.False(v.Heals);
        Assert.Equal("", v.Branch);

        // No tax declared: the honest line, never "not measured".
        Assert.Equal(0.0, Governance.NominalTaxRate(w, Me));
        ViewLine none = PlayerViews.EffectNow(w, cfg, v, PlayerViews.FiguresFrom);
        Assert.Equal("It lightens how heavily your tax is felt here; you levy no tax, so it has no effect yet.", none.Text);
        Assert.Equal("", none.Figure);

        // Under a tax: the factor the felt burden is multiplied by there (Unrest.FeltBurden's service factor).
        w.TaxPolicies.Add(new TaxPolicyRow(Me, 0.1));
        double v0 = Unrest.ServiceOffset(w, s, cfg);
        Assert.True(v0 > 0.0, "anti-vacuity: the court's maturity counts in the town's services");
        ViewLine taxed = PlayerViews.EffectNow(w, cfg, v, PlayerViews.FiguresFrom);
        Assert.Equal("Your tax is felt more lightly here (public services, this one among them)", taxed.Text);
        Assert.Equal("felt x" + (1.0 - serviceMax * Math.Clamp(v0, 0.0, 1.0)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), taxed.Figure);

        // The factor is the levy's own: the felt burden with the court is the one without it times a smaller factor.
        double felt = Unrest.FeltBurden(w, s, cfg, 0.0);
        WorldState bare = w.Clone();
        bare.Institutions.Clear();
        double v1 = Unrest.ServiceOffset(bare, s, cfg);
        Assert.True(v1 < v0, "the court adds to the services");
        Assert.True(felt < Unrest.FeltBurden(bare, s, cfg, 0.0), "under a tax the court lightens the felt burden");
    }

    [Fact]
    public void NoInstitutionPanelLine_SaysItsEffectIsNotMeasured()
    {
        (SimConfig cfg, WorldState w, SettlementId _) = WithCourt(0.3);
        foreach (int density in new[] { 0, 1, 3, 5 })
        {
            int lines = 0;
            foreach (ViewBlock b in PlayerViews.Institutions(w, cfg, Me, id => "Bigen", density).Blocks)
                foreach (ViewLine line in b.Rows) { lines++; Assert.DoesNotContain("not measured", line.Text, StringComparison.Ordinal); }
            Assert.True(lines > 0, "anti-vacuity: the panel has lines");
        }
    }

    [Fact]
    public void TheEndTurnThatFoundsTheCourt_AnnouncesIt_WithWhatItDoes_AndOnlyThatTurn()
    {
        (SimConfig cfg, WorldState w, SettlementId s) = WithCourt(0.0);
        WorldState prev = w.Clone();
        prev.Institutions.Clear();
        ActionSurfaceModel Model(IReadOnlyWorldState now, IReadOnlyWorldState? before) => ActionSurface.Build(new ActionSurfaceInput(
            now, before, cfg, UiSession.ProductionEra(), Me, s.Value, [], id => id == s.Value ? "Bigen" : "Elsewhere",
            EraThemes.For(UiEras.Of(now, cfg.Ages, Me))));

        ActionSurfaceModel founded = Model(w, prev);
        int at = founded.Notices.IndexOf("founded: Law Court at Bigen");
        Assert.True(at >= 0, "the founding is announced: " + string.Join(" | ", founded.Notices));
        Assert.Equal("  it lightens how heavily a tax is felt there - you levy none yet; choosing laws is not yet part of the game",
            founded.Notices[at + 1]);

        w.TaxPolicies.Add(new TaxPolicyRow(Me, 0.1));
        ActionSurfaceModel taxed = Model(w, prev);
        Assert.Contains("  as it matures, your tax is felt more lightly there; choosing laws is not yet part of the game", taxed.Notices);

        // The next End Turn (the court already there before it): not announced again.
        ActionSurfaceModel later = Model(w, w.Clone());
        Assert.DoesNotContain(later.Notices, n => n.StartsWith("founded: ", StringComparison.Ordinal));
    }
}
