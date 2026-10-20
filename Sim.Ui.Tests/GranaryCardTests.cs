using Xunit;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Actions;
using Sim.Ui.Theme;
using Sim.Ui.ViewModel;

namespace Sim.Ui.Tests;

/// <summary>
/// M5 item 5 — THE GRANARY SAYS WHAT IT REALLY DOES. Its info card (InfoQuery) and its build button's "does:" line
/// (ActionSurfaceModel.ProjectEffects) state the two effects the simulation applies — LOCAL farm output on cultivated
/// grain (+3.2% for the first, strictly less for each further one below about 35, never more than +5%) and the
/// EMPIRE-WIDE famine shortfall (-9.8% for the first, strictly less below about 69, never more than -25%; organised
/// relief, no grain moves, and no promise that lost births fall by the same percentage) — from the very functions
/// ProductionSystem and DemographicsSystem multiply by (State.GranaryEffects), beside the Age II milestone and the levy
/// relief. The build button states the RESULTING TOTAL with one more STANDING granary (queued ones do not count). They
/// never claim storage. With the effects switched off the card is the pre-granary-effects card.
/// </summary>
[Collection("ImGui context")]
public class GranaryCardTests
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private static UiSession Small(int? ai = null) => UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: ai);

    /// <summary>C1: the farm factor acts only on CULTIVATED grain (it is the literal 1.0 while a settlement gathers wild
    /// food), so every rig that states the live farm effect runs on a CULTIVATING world: the small world with the player's
    /// polity knowing root_crop (a depth-0 node that makes activity.farming knowledge-eligible), so its settlements grow
    /// cultivated grain (LabourActivities.HarvestsWildFood false — asserted).</summary>
    private static UiSession Cultivating()
    {
        UiSession s0 = Small();
        WorldState w = s0.World.Clone();
        var research = s0.Config.Research!;
        w.ResearchCompleted.Add(new ResearchCompletedRow(Me, research.Nodes[research.IndexOfId("root_crop")].Key));
        UiSession s = UiSession.StartFrom(w, s0.World.Seed, sizeOverridePx: 256, settlementsOverride: 4);
        Assert.False(LabourActivities.HarvestsWildFood(s.World, s.Config, new SettlementId(Capital(s.World))), "the rig's capital must cultivate");
        return s;
    }

    private static int Capital(IReadOnlyWorldState w) => EmpireQuery.TryGetCapital(w, Me, out SettlementId c) ? c.Value : -1;

    private static int GranaryId(SimConfig cfg) => GranaryEffects.ProjectId(cfg);

    private static InfoCard Card(IReadOnlyWorldState w, SimConfig cfg, int settlement) =>
        InfoQuery.Card(w, cfg, Me, InfoSubject.OfProject(GranaryId(cfg), settlement));

    private static InfoLink Line(InfoCard card, string label) => Assert.Single(card.Enables, l => l.Label == label);

    private static ActionSurfaceModel Model(UiSession s) =>
        ActionSurface.ForSession(s, UiSession.ProductionEra(), Capital(s.World), EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me)));

    private static void NoStorageClaim(string text)
    {
        foreach (string word in new[] { "store", "storage", "capacity", "stock", "holds", "keeps grain" })
            Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheCard_StatesTheLocalFarmAndTheEmpireFamineEffects_WithTheirFirstStepAndCeiling_AndNoStorage()
    {
        UiSession s = Cultivating();
        InfoCard card = Card(s.World, s.Config, Capital(s.World));
        InfoLink farm = Line(card, InfoQuery.GranaryFarmLabel);
        InfoLink famine = Line(card, InfoQuery.GranaryFamineLabel);
        Assert.Single(card.Enables, l => l.Label == InfoQuery.LevyServicesLabel);
        Assert.Contains(card.Enables, l => l.Subject is { Kind: InfoKind.AgeMilestone });

        Assert.Equal("farm output here +3.2% with a first standing granary - it saves part of the cultivated crop lost after the harvest (never wild food); each further one adds strictly less below about 35 here, nothing that shows after; never more than +5.0% in all", farm.Note);
        Assert.Equal("the shortfall behind starvation and birth suppression in a severe shortage or a famine, in every settlement of the civilization: -9.8% with a first standing granary anywhere in it; each further one adds strictly less below about 69, nothing that shows after; never more than -25.0% in all - organised relief, no grain moves; births recover only where the eased shortfall falls below the depth that halts them", famine.Note);
        // C4(b): the percentage is a cut to the SHORTFALL; the card never says lost births fall by it.
        Assert.DoesNotContain("lost births", famine.Note);
        NoStorageClaim(farm.Note!);
        NoStorageClaim(famine.Note!);
        Assert.Contains("holds no goods", card.RealizedBy);   // the card's own statement: a structure is counted, not stocked
    }

    [Fact]
    public void WithGranariesStanding_TheCardReadsTheCurrentCountsAndTheNextDiminishingStep()
    {
        UiSession s0 = Cultivating();
        WorldState w = s0.World.Clone();
        int capital = Capital(w);
        w.Structures.Add(new StructureRow(new SettlementId(capital), GranaryId(s0.Config), 1));
        InfoCard card = Card(w, s0.Config, capital);
        Assert.Equal("farm output here +3.2% now from 1 standing granary; one more adds +1.2% - each further one adds strictly less below about 35 here, nothing that shows after; never more than +5.0% in all",
            Line(card, InfoQuery.GranaryFarmLabel).Note);
        Assert.Equal("the shortfall behind starvation and birth suppression in a severe shortage or a famine, in every settlement of the civilization: -9.8% now from 1 standing granary; one more adds -6.0% - each further one adds strictly less below about 69, nothing that shows after; never more than -25.0% in all - organised relief, no grain moves; births recover only where the eased shortfall falls below the depth that halts them",
            Line(card, InfoQuery.GranaryFamineLabel).Note);

        // The famine count is the CIVILIZATION's: a second granary in another of the player's settlements counts there,
        // the farm count is the settlement's own.
        int other = -1;
        for (int i = 0; i < w.Settlements.Count && other < 0; i++)
            if (w.Settlements[i].Id.Value != capital && EmpireQuery.ControlsSettlement(w, Me, w.Settlements[i].Id)) other = w.Settlements[i].Id.Value;
        Assert.True(other >= 0, "the rig needs a second player settlement");
        w.Structures.Add(new StructureRow(new SettlementId(other), GranaryId(s0.Config), 1));
        InfoCard there = Card(w, s0.Config, other);
        Assert.StartsWith("farm output here +3.2% now from 1 standing granary", Line(there, InfoQuery.GranaryFarmLabel).Note);
        Assert.Contains("-15.8% now from 2 standing granaries", Line(there, InfoQuery.GranaryFamineLabel).Note);
    }

    [Fact]
    public void TheBuildButton_SaysBothEffects_AsMultipliers_BesideTheMilestoneAndTheLevy()
    {
        UiSession s = Cultivating();
        ActionSurfaceModel m = Model(s);
        ProjectEntry granary = m.Construction!.Projects.Single(p => p.Name.Contains("ranary", StringComparison.Ordinal));
        string effects = granary.Effects!;
        Assert.Contains("counts toward Age II", effects);
        Assert.Contains("with one more standing granary here: cultivated harvest x1.03 in total (never above x1.05)", effects);
        Assert.Contains("with one more standing granary in the civilization: famine shortfall x0.90 in total (never below x0.75)", effects);
        Assert.Contains("once a levy exists, eases how heavily it is felt here", effects);
        Assert.DoesNotContain("%", effects);                                   // the action surface states no percentages
        Assert.DoesNotContain("food", effects, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storage", effects, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capacity", effects, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheBriefForms_ReadTheResultingTotalWithOneMoreStandingGranary()
    {
        // C4(a): cumulative - the TOTAL factor once one more granary stands (m standing now), never a per-granary multiplier.
        GranariesConfig g = Small().Config.Granaries!;
        Assert.Equal("with one more standing granary here: cultivated harvest x1.03 in total (never above x1.05)", InfoQuery.GranaryFarmBrief(g, 0));
        Assert.Equal("with one more standing granary here: cultivated harvest x1.04 in total (never above x1.05)", InfoQuery.GranaryFarmBrief(g, 1));
        Assert.Equal("with one more standing granary in the civilization: famine shortfall x0.90 in total (never below x0.75)", InfoQuery.GranaryFamineBrief(g, 0));
        Assert.Equal("with one more standing granary in the civilization: famine shortfall x0.84 in total (never below x0.75)", InfoQuery.GranaryFamineBrief(g, 1));
        Assert.Equal("with one more standing granary in the civilization: famine shortfall x0.76 in total (never below x0.75)", InfoQuery.GranaryFamineBrief(g, 5));
    }

    [Fact]
    public void TheBuildButton_CountsStandingGranariesOnly_AQueuedOneChangesNothing_ABuiltOneMovesTheTotal()
    {
        // C4(a): "standing" is literal - the button's totals read built Structures, never the construction queue.
        UiSession s = Cultivating();
        int capital = Capital(s.World);
        int granary = GranaryId(s.Config);
        EraTheme theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
        string Effects(IReadOnlyWorldState w) =>
            ActionSurface.Build(new ActionSurfaceInput(w, null, s.Config, UiSession.ProductionEra(), Me, capital, [],
                    id => s.Names.Name(id), theme))
                .Construction!.Projects.Single(p => p.ProjectId == granary).Effects!;
        string before = Effects(s.World);
        Assert.Contains("with one more standing granary here: cultivated harvest x1.03 in total", before);
        Assert.Contains("with one more standing granary in the civilization: famine shortfall x0.90 in total", before);

        WorldState queued = s.World.Clone();
        queued.ConstructionQueue.Add(new ConstructionQueueRow(new SettlementId(capital), 0, granary));
        Assert.Equal(before, Effects(queued));

        WorldState built = s.World.Clone();
        built.Structures.Add(new StructureRow(new SettlementId(capital), granary, 1));
        string after = Effects(built);
        Assert.Contains("with one more standing granary here: cultivated harvest x1.04 in total", after);
        Assert.Contains("with one more standing granary in the civilization: famine shortfall x0.84 in total", after);
    }

    [Fact]
    public void AWildFoodSettlement_IsToldTheFarmEffectIsNoneUntilItCultivates_OnTheCardAndTheButton()
    {
        // C1: while the settlement gathers wild food the farm factor is the literal 1.0 (ProductionSystem.Farm skips it), so
        // neither the card nor the button may claim a farm effect now; they say what it becomes once the place cultivates.
        // The famine line is untouched (it acts on the effective deficit, wild food or not).
        UiSession s = Small();
        int capital = Capital(s.World);
        int granary = GranaryId(s.Config);
        Assert.True(LabourActivities.HarvestsWildFood(s.World, s.Config, new SettlementId(capital)), "the turn-one capital gathers wild food");
        InfoCard card = Card(s.World, s.Config, capital);
        Assert.Equal("no effect while this settlement gathers wild food; once it cultivates: farm output here +3.2% with a first standing granary - it saves part of the cultivated crop lost after the harvest (never wild food); each further one adds strictly less below about 35 here, nothing that shows after; never more than +5.0% in all",
            Line(card, InfoQuery.GranaryFarmLabel).Note);
        Assert.Equal(Line(Card(Cultivating().World, s.Config, capital), InfoQuery.GranaryFamineLabel).Note, Line(card, InfoQuery.GranaryFamineLabel).Note);

        // One granary standing at a wild-food settlement: the count is shown, never "now".
        WorldState w = s.World.Clone();
        w.Structures.Add(new StructureRow(new SettlementId(capital), granary, 1));
        string farm = Line(Card(w, s.Config, capital), InfoQuery.GranaryFarmLabel).Note!;
        Assert.Equal("no effect while this settlement gathers wild food; once it cultivates: farm output here +3.2% from 1 standing granary; one more adds +1.2% - each further one adds strictly less below about 35 here, nothing that shows after; never more than +5.0% in all", farm);
        Assert.DoesNotContain(" now ", farm);
        Assert.Equal(1.0, GranaryEffects.FarmYieldFactor(w, s.Config, new SettlementId(capital)));   // the claim it makes: none

        // The build button says the same, in its multiplier form.
        EraTheme theme = EraThemes.For(UiEras.Of(s.World, s.Config.Ages, Me));
        string effects = ActionSurface.Build(new ActionSurfaceInput(s.World, null, s.Config, UiSession.ProductionEra(), Me, capital, [],
                id => s.Names.Name(id), theme))
            .Construction!.Projects.Single(p => p.ProjectId == granary).Effects!;
        Assert.Contains("no effect while this settlement gathers wild food; once it cultivates, with one more standing granary here: cultivated harvest x1.03 in total (never above x1.05)", effects);
        Assert.Contains("with one more standing granary in the civilization: famine shortfall x0.90 in total (never below x0.75)", effects);
        Assert.Equal("no effect while this settlement gathers wild food; once it cultivates, with one more standing granary here: cultivated harvest x1.03 in total (never above x1.05)",
            InfoQuery.GranaryFarmBrief(s.Config.Granaries!, 0, harvestsWildFood: true));
    }

    [Fact]
    public void WithTheEffectsSwitchedOff_TheCardIsThePreGranaryCard()
    {
        UiSession s = Small();
        SimConfig off = s.Config with { Granaries = s.Config.Granaries! with { Enabled = false } };
        InfoCard card = Card(s.World, off, Capital(s.World));
        Assert.DoesNotContain(card.Enables, l => l.Label == InfoQuery.GranaryFarmLabel || l.Label == InfoQuery.GranaryFamineLabel);
        InfoLink levy = Assert.Single(card.Enables, l => l.Label == InfoQuery.LevyServicesLabel);
        Assert.Equal("once a tax levy exists, each one built eases how heavily the levy is felt here, as far as your administration reaches", levy.Note);
    }

    [Fact]
    public void AForeignSettlementsCard_ShowsNoCount()
    {
        UiSession s = Small(ai: 1);
        int foreign = -1;
        for (int i = 0; i < s.World.Settlements.Count && foreign < 0; i++)
            if (!EmpireQuery.ControlsSettlement(s.World, Me, s.World.Settlements[i].Id)) foreign = s.World.Settlements[i].Id.Value;
        Assert.True(foreign >= 0, "the rig needs a foreign settlement");
        WorldState w = s.World.Clone();
        w.Structures.Add(new StructureRow(new SettlementId(foreign), GranaryId(s.Config), 3));
        InfoCard card = Card(w, s.Config, foreign);
        Assert.DoesNotContain("now from", Line(card, InfoQuery.GranaryFarmLabel).Note);
        Assert.DoesNotContain("now from", Line(card, InfoQuery.GranaryFamineLabel).Note);
        // ...and the foreign granaries do not change the card a player reads there.
        Assert.Equal(Line(Card(s.World, s.Config, foreign), InfoQuery.GranaryFamineLabel).Note, Line(card, InfoQuery.GranaryFamineLabel).Note);
    }
}
