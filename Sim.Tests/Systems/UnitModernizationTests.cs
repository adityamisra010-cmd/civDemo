using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-031 — the unit-family graph (D-047 Part 2) and automatic, free modernization at Age
/// transition (ruling 18, Part 3): per-family conversion, no-successor preservation, the
/// explicit generic successor, preserved fields, and preview == applied.
/// </summary>
public class UnitModernizationTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly AgeContent Ages = TestConfigs.Ages();
    private static readonly UnitFamilyContent Families = TestConfigs.UnitFamilies();
    private static readonly ResearchContent Research = TestConfigs.Research();
    private static readonly PolityId Player = new(1);

    private static UnitIdentity Id(string id) => Families.IdentityById(id) ?? throw new ArgumentException(id);

    private static MilitaryUnitRow Unit(int id, string identity, double experience = 0.0, int army = 0, int location = 0) =>
        new(id, Player, Id(identity).FamilyKey, Id(identity).Key, new SettlementId(location), 12.25, 7.5, experience, army);

    private static (string To, ModernizationOutcome Outcome) Plan(string identity, int toAge)
    {
        UnitConversionPlan p = MilitaryQuery.PlanFor(Families, Unit(1, identity), toAge);
        return (Families.IdentityByKey(p.ToIdentity)!.Id, p.Outcome);
    }

    // ------------------------------------------------------------------ the family graph

    [Fact]
    public void Content_TwelveFamilies_EveryRequiredRole_EachWithSchemaFields()
    {
        string[] expected =
        [
            "light_infantry", "spear_anti_cavalry", "heavy_infantry", "ranged_infantry", "light_cavalry", "heavy_cavalry",
            "ranged_cavalry", "siege", "naval_combat", "naval_transport", "air", "strategic_missile",
        ];
        Assert.Equal(expected, Families.Families.Select(f => f.Id));
        foreach (UnitFamily f in Families.Families)
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Role));
            Assert.False(string.IsNullOrWhiteSpace(f.HistoricalEmergence));
            Assert.NotEmpty(f.Line);
            Assert.True(f.FirstAge >= 1 && f.FirstAge <= 9);
            foreach (UnitIdentity i in f.Line) Assert.Equal(f.Key, i.FamilyKey);
        }
        Assert.Equal(UnitComposition.MobileAgent, Families.FamilyById("naval_combat")!.Composition);
        Assert.Equal(UnitComposition.ArmyFormation, Families.FamilyById("heavy_infantry")!.Composition);
        Assert.Equal("warband", Families.FoundingIdentity.Id);
        Assert.Equal(1, Families.FormationsPerPolity);
    }

    [Fact]
    public void Content_RealizedBy_NamesRealResearchUnitEntities()
    {
        foreach (UnitFamily f in Families.Families)
            foreach (UnitIdentity i in f.Line)
                if (i.RealizedBy is { } e)
                {
                    int index = Research.EntityIndexOf(e);
                    Assert.True(index >= 0, e);
                    Assert.Equal(ResearchEntityKind.Unit, Research.Entities[index].Kind);
                }
    }

    [Fact]
    public void Graph_PredecessorAndSuccessor_FollowTheMainline()
    {
        UnitFamily heavy = Families.FamilyById("heavy_infantry")!;
        Assert.Null(heavy.Predecessor(Id("warband")));
        Assert.Equal("axe_warriors", heavy.Successor(Id("warband"))!.Id);
        Assert.Equal("bronze_swordsmen", heavy.Successor(Id("axe_warriors"))!.Id);
        for (int age = 1; age <= 9; age++) Assert.NotNull(heavy.RealizationAt(age)); // no Age gap in the main infantry line
        Assert.Equal("men_at_arms", heavy.Predecessor(Id("musketeers"))!.Id);
        Assert.Null(heavy.Successor(Id("mechanized_infantry")));
        UnitFamily ranged = Families.FamilyById("ranged_infantry")!;
        Assert.Equal("arquebusiers", ranged.Successor(Id("longbowmen"))!.Id); // a branch rejoins the mainline
        Assert.Equal("crossbowmen", ranged.RealizationAt(6)!.Id);             // the mainline, never the branch
        Assert.Null(Families.FamilyById("air")!.RealizationAt(8));            // not yet realized
        Assert.Null(Families.FamilyById("spear_anti_cavalry")!.RealizationAt(8)); // line ended
    }

    [Fact]
    public void LoaderRejects_NonIncreasingMainline_AndUnknownGenericFamily()
    {
        string json = FamiliesJson();
        var e1 = Assert.Throws<UnitFamilyContentException>(() => UnitFamilyContentLoader.Load(
            json.Replace("\"id\": \"skirmishers\", \"name\": \"Skirmishers\", \"age\": 3", "\"id\": \"skirmishers\", \"name\": \"Skirmishers\", \"age\": 1"),
            Research));
        Assert.Contains("strictly increasing", e1.Message);
        var e2 = Assert.Throws<UnitFamilyContentException>(() => UnitFamilyContentLoader.Load(
            json.Replace("\"rule\": \"generic\", \"family\": \"heavy_infantry\"", "\"rule\": \"generic\", \"family\": \"hoplites\""),
            Research));
        Assert.Contains("hoplites", e2.Message);
        var e3 = Assert.Throws<UnitFamilyContentException>(() => UnitFamilyContentLoader.Load(
            json.Replace("\"realizedBy\": \"unit.spearmen\"", "\"realizedBy\": \"unit.spearman\""), Research));
        Assert.Contains("unit.spearman", e3.Message);
    }

    // ------------------------------------------------------------------ the conversion rule, per family

    [Theory]
    [InlineData("warband", 2, "axe_warriors", ModernizationOutcome.Converted)]        // the founding warband's Neolithic successor
    [InlineData("axe_warriors", 3, "bronze_swordsmen", ModernizationOutcome.Converted)]
    [InlineData("warband", 3, "bronze_swordsmen", ModernizationOutcome.Converted)]
    [InlineData("warband", 5, "legion", ModernizationOutcome.Converted)]             // several Ages at once: the realization AT the new Age
    [InlineData("men_at_arms", 7, "musketeers", ModernizationOutcome.Converted)]
    [InlineData("musketeers", 8, "line_infantry", ModernizationOutcome.Converted)]
    [InlineData("scouts", 3, "skirmishers", ModernizationOutcome.Converted)]
    [InlineData("spearmen", 4, "phalanx", ModernizationOutcome.Converted)]
    [InlineData("phalanx", 6, "pikemen", ModernizationOutcome.Converted)]
    [InlineData("slingers", 2, "archers", ModernizationOutcome.Converted)]
    [InlineData("crossbowmen", 7, "arquebusiers", ModernizationOutcome.Converted)]
    [InlineData("longbowmen", 7, "arquebusiers", ModernizationOutcome.Converted)]   // a branch converts to the mainline successor
    [InlineData("horse_scouts", 4, "light_horse", ModernizationOutcome.Converted)]
    [InlineData("knights", 7, "cuirassiers", ModernizationOutcome.Converted)]
    [InlineData("horse_archers", 6, "mounted_crossbowmen", ModernizationOutcome.Converted)]
    [InlineData("siege_rams", 5, "torsion_engines", ModernizationOutcome.Converted)]
    [InlineData("bombards", 8, "field_artillery", ModernizationOutcome.Converted)]
    [InlineData("war_galleys", 5, "quinqueremes", ModernizationOutcome.Converted)]
    [InlineData("ironclads", 9, "destroyers", ModernizationOutcome.Converted)]
    [InlineData("shore_craft", 3, "plank_boats", ModernizationOutcome.Converted)]
    [InlineData("steam_transports", 9, "amphibious_transports", ModernizationOutcome.Converted)]
    public void Modernization_ConvertsToTheFamilysRealizationAtTheNewAge(string from, int toAge, string to, ModernizationOutcome outcome)
    {
        Assert.Equal((to, outcome), Plan(from, toAge));
    }

    [Theory]
    [InlineData("scouts", 2)]             // documented gap: scouts remain the Neolithic light-infantry realization
    [InlineData("shore_craft", 2)]        // documented gap: dugouts and hide boats remain Neolithic water transport
    [InlineData("cuirassiers", 8)]        // no Industrial heavy cavalry: kept until tanks exist at A9
    [InlineData("archers", 3)]            // archers persist through the Bronze Age
    [InlineData("military_aircraft", 9)]  // already the newest realization
    [InlineData("nuclear_submarines", 9)] // a branch is never converted onto the mainline at its own Age
    public void Modernization_NoSuccessorYet_PreservesTheUnitUnchanged(string identity, int toAge)
    {
        Assert.Equal((identity, ModernizationOutcome.PreservedNoSuccessor), Plan(identity, toAge));
    }

    [Fact]
    public void Modernization_EndedLine_ConvertsToTheExplicitGenericSuccessor()
    {
        // Pike made obsolete by the bayonet: the spear line ends after A7 and folds into line infantry.
        Assert.Equal(("line_infantry", ModernizationOutcome.ConvertedGeneric), Plan("pike_and_shot", 8));
        Assert.Equal(("mechanized_infantry", ModernizationOutcome.ConvertedGeneric), Plan("pikemen", 9));
        // Mounted missile troops become carbine cavalry.
        Assert.Equal(("carbine_cavalry", ModernizationOutcome.ConvertedGeneric), Plan("dragoons", 8));
        UnitConversionPlan p = MilitaryQuery.PlanFor(Families, Unit(1, "pike_and_shot"), 8);
        Assert.Equal(Families.FamilyById("heavy_infantry")!.Key, p.ToFamily);
        Assert.Equal(Families.FamilyById("spear_anti_cavalry")!.Key, p.FromFamily);
    }

    [Fact]
    public void Modernization_UnknownIdentity_IsPreserved_NeverDestroyed()
    {
        var ghost = new MilitaryUnitRow(4, Player, 77, 7777, new SettlementId(0), 0, 0, 2.0, 1);
        UnitConversionPlan p = MilitaryQuery.PlanFor(Families, ghost, 5);
        Assert.Equal((7777, ModernizationOutcome.PreservedNoSuccessor, false), (p.ToIdentity, p.Outcome, p.Changes));
    }

    [Fact]
    public void FamilyLineChanges_AreTheGraphTheUiDraws_ComputedByTheSameRule()
    {
        FamilyLineChange[] a6to7 = MilitaryQuery.FamilyLineChanges(Families, 6, 7);
        Assert.Equal(12, a6to7.Length);
        FamilyLineChange spear = a6to7.Single(c => c.Family.Id == "spear_anti_cavalry");
        Assert.Equal(("pikemen", "pike_and_shot"), (spear.From!.Id, spear.To!.Id));
        FamilyLineChange air = a6to7.Single(c => c.Family.Id == "air");
        Assert.Null(air.From);
        Assert.Null(air.To);
        FamilyLineChange spear78 = MilitaryQuery.FamilyLineChanges(Families, 7, 8).Single(c => c.Family.Id == "spear_anti_cavalry");
        Assert.Equal(("pike_and_shot", "line_infantry", ModernizationOutcome.ConvertedGeneric),
            (spear78.From!.Id, spear78.To!.Id, spear78.Outcome));
    }

    // ------------------------------------------------------------------ applied at the Age transition

    private static TurnExecutor AgeOnly(OrderLog orders) =>
        new(ResearchRigs.FlatEra(10.0), [SystemCatalog.AgeEligibility(Cfg), SystemCatalog.AgeTransition(Cfg)], orders);

    /// <summary>A world of one polity at Age 2, eligible for A3 (core: bronze; supporting: proto_writing
    /// (Institutional-Social), wheel_solid (Technological), formations (Military)), owning a mixed army.</summary>
    private static WorldState BronzeReady()
    {
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42);
        w.AgeStates.Add(new AgeStateRow(Player, 2, 5, 1, 5));
        foreach (string id in new[] { "arsenical_bronze", "proto_writing", "wheel_solid" })
            w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId(id)].Key));
        w.MilitaryUnits.Clear();
        w.MilitaryUnits.Add(Unit(10, "warband", experience: 3.5, army: 7, location: 2));
        w.MilitaryUnits.Add(Unit(11, "spearmen", experience: 0.25, army: 7, location: 2));
        w.MilitaryUnits.Add(Unit(12, "archers", experience: 1.0, army: 0, location: -1));
        w.MilitaryUnits.Add(Unit(13, "shore_craft"));
        w.MilitaryUnits.Add(new MilitaryUnitRow(14, new PolityId(9), Id("warband").FamilyKey, Id("warband").Key,
            new SettlementId(3), 1, 1, 0, 0)); // another owner: untouched
        return w;
    }

    [Fact]
    public void AgeTransition_ModernizesEveryFormation_PreservingLocationOwnerExperienceAndArmy()
    {
        WorldState w0 = BronzeReady();
        Assert.True(AgeQuery.IsEligible(w0, Ages, Player));
        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, Player, OrderKind.AdvanceAge, 3, 1));
        WorldState w1 = AgeOnly(orders).Step(w0);
        Assert.Equal(3, AgeQuery.CurrentAge(w1, Ages, Player));

        string[] identities = new string[5];
        for (int i = 0; i < 5; i++) identities[i] = Families.IdentityByKey(w1.MilitaryUnits[i].Identity)!.Id;
        Assert.Equal(["bronze_swordsmen", "spearmen", "archers", "plank_boats", "warband"], identities);
        for (int i = 0; i < 5; i++)
        {
            MilitaryUnitRow before = w0.MilitaryUnits[i], after = w1.MilitaryUnits[i];
            Assert.Equal((before.Id, before.Owner, before.Location, before.Army), (after.Id, after.Owner, after.Location, after.Army));
            Assert.Equal(BitConverter.DoubleToInt64Bits(before.Experience), BitConverter.DoubleToInt64Bits(after.Experience));
            Assert.Equal(BitConverter.DoubleToInt64Bits(before.X), BitConverter.DoubleToInt64Bits(after.X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(before.Y), BitConverter.DoubleToInt64Bits(after.Y));
        }
        Assert.Equal(w0.MilitaryUnits.Count, w1.MilitaryUnits.Count); // free and lossless: nothing destroyed or created
    }

    [Fact]
    public void AgeTransition_PreviewEqualsTheAppliedConversion_AndEveryOutcomeIsLogged()
    {
        WorldState w0 = BronzeReady();
        UnitConversionPlan[] preview = MilitaryQuery.ModernizationPreview(w0, Ages, Families, Player);
        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, Player, OrderKind.AdvanceAge, 3, 2));
        WorldState w1 = AgeOnly(orders).Step(w0);

        UnitConversionRow[] log = MilitaryQuery.Conversions(w1, Player);
        Assert.Equal(preview.Length, log.Length);
        Assert.Equal(4, log.Length); // the player's four formations; the other owner's is not touched
        for (int i = 0; i < log.Length; i++)
        {
            Assert.Equal((preview[i].Unit, preview[i].FromIdentity, preview[i].ToIdentity, (int)preview[i].Outcome),
                (log[i].Unit, log[i].FromIdentity, log[i].ToIdentity, log[i].Outcome));
            Assert.Equal((1L, 2, 3), (log[i].Turn, log[i].FromAge, log[i].ToAge));
            Assert.Equal(log[i].ToIdentity, w1.MilitaryUnits[i].Identity);
        }
        Assert.Equal([ModernizationOutcome.Converted, ModernizationOutcome.PreservedNoSuccessor,
            ModernizationOutcome.PreservedNoSuccessor, ModernizationOutcome.Converted], preview.Select(p => p.Outcome));

        ConversionSummary[] summary = MilitaryQuery.Summarize(Families, preview);
        Assert.Equal(("warband", "bronze_swordsmen", 1), (summary[0].From.Id, summary[0].To.Id, summary[0].Count));
        Assert.Equal(4, summary.Length);
    }

    [Fact]
    public void AgeTransition_WithoutAnOrder_ModernizesNothing()
    {
        WorldState w0 = BronzeReady();
        WorldState w1 = AgeOnly(new OrderLog()).Step(w0);
        Assert.True(WorldStates.TableEquals(w0.MilitaryUnits, w1.MilitaryUnits));
        Assert.Equal(0, w1.UnitConversions.Count);
    }

    [Fact]
    public void AgeTransition_FromTheFoundingWorld_TheWarbandBecomesAxeWarriorsInTheNeolithic_ThenBronzeInfantry()
    {
        // The canonical founding warband converts at A2 to its Neolithic successor (axe warriors),
        // and at A3 to bronze-armed infantry. Preview == applied at each step.
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), Cfg, 42);
        foreach (string id in new[] { "cereal_cultivation", "pottery_open_fired", "arsenical_bronze", "proto_writing", "wheel_solid" })
            w.ResearchCompleted.Add(new ResearchCompletedRow(Player, Research.Nodes[Research.IndexOfId(id)].Key));
        var orders = new OrderLog();
        orders.Append(OrderRecord.From(0, Player, OrderKind.AdvanceAge, 2, 1));
        orders.Append(OrderRecord.From(1, Player, OrderKind.AdvanceAge, 3, 1));
        TurnExecutor ex = AgeOnly(orders);
        UnitConversionPlan[] preview1 = MilitaryQuery.ModernizationPreview(w, Ages, Families, Player);
        WorldState w1 = ex.Step(w);
        Assert.Equal(2, AgeQuery.CurrentAge(w1, Ages, Player));
        Assert.Equal("axe_warriors", Families.IdentityByKey(w1.MilitaryUnits[0].Identity)!.Id);
        Assert.Equal((int)ModernizationOutcome.Converted, w1.UnitConversions[0].Outcome);
        Assert.Single(preview1);
        Assert.Equal((preview1[0].Unit, preview1[0].FromIdentity, preview1[0].ToIdentity, (int)preview1[0].Outcome),
            (w1.UnitConversions[0].Unit, w1.UnitConversions[0].FromIdentity, w1.UnitConversions[0].ToIdentity, w1.UnitConversions[0].Outcome));
        Assert.Equal(Id("axe_warriors").Key, preview1[0].ToIdentity);
        WorldState w2 = ex.Step(w1);
        Assert.Equal(3, AgeQuery.CurrentAge(w2, Ages, Player));
        Assert.Equal("bronze_swordsmen", Families.IdentityByKey(w2.MilitaryUnits[0].Identity)!.Id);
        Assert.Equal((int)ModernizationOutcome.Converted, w2.UnitConversions[1].Outcome);
        Assert.Equal([1L, 2L], new[] { w2.AgeTransitions[0].EffectiveTurn, w2.AgeTransitions[1].EffectiveTurn });
    }

    private static string FamiliesJson()
    {
        using var s = Sim.Data.DataFiles.OpenUnitFamilies();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
