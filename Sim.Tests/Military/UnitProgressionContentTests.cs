using System.Text.Json.Nodes;
using Sim.Core.Systems.Ages;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Military;

/// <summary>
/// M6.9 / M6.10 (T6.12; D-E and D-F RULED option 1; D-066 R2, A2 item 4) — THE UNIT-PROGRESSION CONTENT AND ITS VALIDATOR.
/// The shipped file is loaded POPULATED and read back field by field (three trees, twelve promotions, two training profiles,
/// seven rejections — never an empty-table pass); then every loader rule (R1–R7, T1–T3) is broken by one mutation of the
/// shipped JSON, and each mutant must be refused with that rule's message. Inertness (no system reads the content) is
/// UnitProgressionInertTests.
/// </summary>
public class UnitProgressionContentTests
{
    private static readonly UnitFamilyContent Families = TestConfigs.UnitFamilies();

    private static string ShippedJson()
    {
        using var reader = new StreamReader(Sim.Data.DataFiles.OpenUnitProgression());
        return reader.ReadToEnd();
    }

    private static UnitProgressionContent Shipped() => UnitProgressionContentLoader.Load(ShippedJson(), Families);

    // ------------------------------------------------------------------ populated

    [Fact]
    public void Shipped_LoadsPopulated_ThreeTreesTwelvePromotions_OverLandArmyFormationsOnly()
    {
        UnitProgressionContent c = Shipped();
        Assert.Equal("INERT", c.Status);
        Assert.Equal(3, c.RepeatCap);
        Assert.Equal(["tree.line_infantry", "tree.ranged_infantry", "tree.mounted"], c.Trees.Select(t => t.Id));
        Assert.Equal([4, 4, 4], c.Trees.Select(t => t.Promotions.Count));
        Assert.Equal(["heavy_infantry", "spear_anti_cavalry", "light_infantry"], c.Trees[0].Families);
        Assert.Equal(["ranged_infantry"], c.Trees[1].Families);
        Assert.Equal(["light_cavalry", "heavy_cavalry", "ranged_cavalry"], c.Trees[2].Families);
        foreach (PromotionTree t in c.Trees)
            foreach (string f in t.Families)
                Assert.Equal(UnitComposition.ArmyFormation, Families.FamilyById(f)!.Composition);
        // The founding warband's family (heavy_infantry, identity 301) has a tree: the slice's inert promotion entry (D-P item 4).
        Assert.Equal("heavy_infantry", Families.FamilyByKey(Families.FoundingIdentity.FamilyKey)!.Id);
        Assert.Equal("promo.inf.drilled", c.TreeForFamily("heavy_infantry")!.FirstRoot()!.Id);
        Assert.Null(c.TreeForFamily("siege"));
        Assert.Null(c.TreeForFamily("naval_combat"));
    }

    [Fact]
    public void Shipped_EveryContractTermIsGrantedExactlyOnce_AndEachPromotionReadsBack()
    {
        UnitProgressionContent c = Shipped();
        var granted = new List<(string, string)>();
        foreach (PromotionTree t in c.Trees)
            foreach (Promotion p in t.Promotions)
                foreach (PromotionEffect e in p.Effects) granted.Add((e.Field, e.Term));
        Assert.Equal(UnitProgressionContract.Terms.Count, granted.Count);
        Assert.Equal(UnitProgressionContract.Terms.OrderBy(x => x.Field + "." + x.Term, StringComparer.Ordinal),
            granted.OrderBy(x => x.Item1 + "." + x.Item2, StringComparer.Ordinal));

        Promotion shield = c.PromotionById("promo.inf.shield_wall")!;
        Assert.Equal(("strength", "losses_taken", 0.9), (shield.Effects[0].Field, shield.Effects[0].Term, shield.Effects[0].Scale));
        Assert.Equal(["promo.inf.steady_ranks"], shield.Requires);
        Assert.Equal(("inf.doctrine", 1, 0), (shield.ExclusiveGroup, shield.MaxTimes, shield.TreeIndex));
        Promotion volleys = c.PromotionById("promo.rng.practised_volleys")!;
        Assert.Equal((2, 1.05), (volleys.MaxTimes, volleys.Effects[0].Scale));
        Promotion last = c.PromotionById("promo.cav.last_to_break")!;
        Assert.Equal(["promo.cav.rally_drill", "promo.cav.hardened_riders"], last.Requires);
        Assert.Null(last.ExclusiveGroup);
        // Two exclusive groups, two members each, each in one tree.
        Assert.Equal(["promo.inf.shock_tactics", "promo.inf.shield_wall"],
            c.Trees[0].Promotions.Where(p => p.ExclusiveGroup == "inf.doctrine").Select(p => p.Id));
        Assert.Equal(["promo.rng.full_pouches", "promo.rng.screening_drill"],
            c.Trees[1].Promotions.Where(p => p.ExclusiveGroup == "rng.kit").Select(p => p.Id));
    }

    [Fact]
    public void Shipped_TrainingContract_IsExplicitAndDormant_AndReadsBackFieldByField()
    {
        TrainingContract t = Shipped().Training;
        Assert.Equal(("DORMANT", "explicit", 0.0, "idle"), (t.Status, t.Mode, t.PassiveGainPerYear, t.DormantState));
        Assert.Equal(["train.land_formation", "train.reconnaissance"], t.Profiles.Select(p => p.Id));
        TrainingProfile land = t.Profiles[0];
        Assert.Equal(8, land.Families.Count);
        Assert.Equal(UnitProgressionContract.TrainingConditions, land.Eligibility);
        Assert.Equal((1.0, 0.25, 1, 2, "escalating_years", 1.5),
            (land.ActionCapacityShare, land.Years, land.Increment, land.Cap, land.DiminishingRule, land.Factor));
        // Diminishing returns as data: the k-th training lasts years × factor^(k−1) — exact in binary for these bands.
        Assert.Equal(0.25, land.YearsOfTraining(1));
        Assert.Equal(0.375, land.YearsOfTraining(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => land.YearsOfTraining(3));   // past the cap
        TrainingProfile recon = t.Profiles[1];
        Assert.Equal(["reconnaissance"], recon.Families);
        Assert.Equal((1, 1.0, 0.1), (recon.Cap, recon.Factor, recon.Years));
        Assert.Equal(UnitComposition.MobileAgent, Families.FamilyById("reconnaissance")!.Composition);
        Assert.Equal(UnitProgressionContract.TrainingConditions, t.Rejections.Select(r => r.Condition));
        Assert.Equal(["not_owner", "not_stationed", "reequipping", "engaged", "unsupplied", "insufficient_action_capacity", "at_cap"],
            t.Rejections.Select(r => r.Code));
        Assert.All(t.Rejections, r => Assert.False(string.IsNullOrWhiteSpace(r.Text)));
    }

    [Fact]
    public void Shipped_LoadsTheSameWithoutTheFamilyCrossCheck_AsTheUiLoadsIt()
    {
        UnitProgressionContent withFamilies = Shipped();
        UnitProgressionContent without = UnitProgressionContentLoader.Load(ShippedJson(), families: null);
        Assert.Equal(withFamilies.Trees.SelectMany(t => t.Promotions).Select(p => p.Id), without.Trees.SelectMany(t => t.Promotions).Select(p => p.Id));
        Assert.Equal(withFamilies.Training.Profiles.Select(p => p.Id), without.Training.Profiles.Select(p => p.Id));
    }

    // ------------------------------------------------------------------ mutants: each rule's violation is refused

    private static JsonObject Tree(JsonObject root, int t) => (JsonObject)root["trees"]![t]!;
    private static JsonObject Promo(JsonObject root, int t, int p) => (JsonObject)Tree(root, t)["promotions"]![p]!;
    private static JsonObject Effect(JsonObject root, int t, int p) => (JsonObject)Promo(root, t, p)["effects"]![0]!;
    private static JsonObject Training(JsonObject root) => (JsonObject)root["training"]!;
    private static JsonObject Profile(JsonObject root, int p) => (JsonObject)Training(root)["profiles"]![p]!;
    private static JsonObject Rejection(JsonObject root, int r) => (JsonObject)Training(root)["rejections"]![r]!;

    /// <summary>Each mutant: a name, the one change it makes to the shipped JSON, and the text the refusal must carry.</summary>
    private static readonly (string Name, Action<JsonObject> Mutate, string Expect)[] Mutants =
    [
        ("schema", r => r["schema"] = 2, "schema is 2"),
        ("R1 tree id prefix", r => Tree(r, 0)["id"] = "line_infantry", "R1:"),
        ("R1 promotion id prefix", r => Promo(r, 0, 0)["id"] = "drilled", "R1:"),
        ("R1 duplicate id across trees", r => Promo(r, 1, 0)["id"] = "promo.inf.drilled", "R1:"),
        ("R1 duplicate profile id", r => Profile(r, 1)["id"] = "train.land_formation", "R1:"),
        ("R2 unknown family", r => ((JsonArray)Tree(r, 1)["families"]!)[0] = "longbowmen", "R2:"),
        ("R2 non-formation family in a tree", r => ((JsonArray)Tree(r, 1)["families"]!).Add("naval_combat"), "R2:"),
        ("R2 family in two trees", r => ((JsonArray)Tree(r, 1)["families"]!).Add("heavy_infantry"), "R2:"),
        ("R2 family in two profiles", r => ((JsonArray)Profile(r, 1)["families"]!).Add("siege"), "R2:"),
        ("R2 unknown profile family", r => ((JsonArray)Profile(r, 1)["families"]!)[0] = "scouts", "R2:"),
        ("R3 refused field", r => Effect(r, 0, 0)["field"] = "action_capacity", "is refused"),
        ("R3 refused experience", r => Effect(r, 0, 0)["field"] = "experience", "is refused"),
        ("R3 unknown field", r => Effect(r, 0, 0)["field"] = "attack", "R3:"),
        ("R3 unknown term", r => Effect(r, 0, 0)["term"] = "bonus", "R3:"),
        ("R4 stat duplicated across trees", r => { Effect(r, 1, 0)["field"] = "cohesion"; Effect(r, 1, 0)["term"] = "loss_rate"; }, "R4:"),
        ("R4 stat duplicated within a tree", r => { Effect(r, 0, 3)["field"] = "strength"; Effect(r, 0, 3)["term"] = "melee_effect"; }, "R4:"),
        ("R5 missing prerequisite", r => ((JsonArray)Promo(r, 0, 1)["requires"]!)[0] = "promo.inf.nothing", "R5:"),
        ("R5 prerequisite in another tree", r => ((JsonArray)Promo(r, 0, 1)["requires"]!)[0] = "promo.rng.practised_volleys", "R5:"),
        ("R5 self prerequisite", r => ((JsonArray)Promo(r, 0, 0)["requires"]!).Add("promo.inf.drilled"), "R5:"),
        ("R5 duplicate prerequisite", r => ((JsonArray)Promo(r, 0, 1)["requires"]!).Add("promo.inf.drilled"), "R5:"),
        ("R5 cycle", r => ((JsonArray)Promo(r, 0, 0)["requires"]!).Add("promo.inf.steady_ranks"), "cycle"),
        ("R6 single-member group", r => Promo(r, 0, 0)["exclusiveGroup"] = "inf.alone", "only one member"),
        ("R6 group in two trees", r => { Promo(r, 1, 0)["exclusiveGroup"] = "inf.doctrine"; Promo(r, 1, 1)["exclusiveGroup"] = "inf.doctrine"; }, "another tree"),
        ("R6 member requires its own group", r => ((JsonArray)Promo(r, 0, 3)["requires"]!).Add("promo.inf.shock_tactics"), "its own exclusive group"),
        // Rally drill and hardened riders become one exclusive group: last-to-break needs both, so it could never be earned.
        ("R6 requires two members of one group", r => { Promo(r, 2, 1)["exclusiveGroup"] = "cav.x"; Promo(r, 2, 2)["exclusiveGroup"] = "cav.x"; },
            "it requires both"),
        ("R7 maxTimes zero", r => Promo(r, 0, 0)["maxTimes"] = 0, "R7:"),
        ("R7 maxTimes over repeatCap", r => Promo(r, 1, 0)["maxTimes"] = 4, "R7:"),
        ("R7 repeatCap zero", r => r["repeatCap"] = 0, "R7:"),
        ("R7 scale one", r => Effect(r, 0, 0)["scale"] = 1.0, "R7:"),
        ("R7 scale non-positive", r => Effect(r, 0, 0)["scale"] = -0.5, "R7:"),
        ("R7 compound out of bounds", r => { Effect(r, 1, 0)["scale"] = 1.3; Promo(r, 1, 0)["maxTimes"] = 3; }, "compounded coefficient"),
        ("T1 passive mode", r => Training(r)["mode"] = "passive", "T1:"),
        ("T1 passive gain", r => Training(r)["passiveGainPerYear"] = 0.01, "T1:"),
        ("T1 dormant state", r => Training(r)["dormantState"] = "resting", "T1:"),
        ("T2 unknown condition", r => ((JsonArray)Profile(r, 0)["eligibility"]!).Add("veteran"), "T2:"),
        ("T2 duplicate condition", r => ((JsonArray)Profile(r, 0)["eligibility"]!).Add("owned"), "T2:"),
        ("T2 cap condition missing", r => ((JsonArray)Profile(r, 1)["eligibility"]!).RemoveAt(4), "must include 'below_cap'"),
        ("T2 cost share zero", r => ((JsonObject)Profile(r, 0)["cost"]!)["actionCapacityShare"] = 0.0, "actionCapacityShare"),
        ("T2 cost share over one", r => ((JsonObject)Profile(r, 0)["cost"]!)["actionCapacityShare"] = 1.5, "actionCapacityShare"),
        ("T2 years zero", r => ((JsonObject)Profile(r, 0)["cost"]!)["years"] = 0.0, "cost.years"),
        ("T2 increment zero", r => Profile(r, 0)["increment"] = 0, "increment"),
        ("T2 cap zero", r => Profile(r, 0)["cap"] = 0, "cap must be"),
        ("T2 factor below one", r => ((JsonObject)Profile(r, 0)["diminishing"]!)["factor"] = 0.9, "diminishing.factor"),
        ("T2 unknown rule", r => ((JsonObject)Profile(r, 0)["diminishing"]!)["rule"] = "decay", "diminishing.rule"),
        ("T3 condition without rejection", r => ((JsonArray)Training(r)["rejections"]!).RemoveAt(3), "has no rejection"),
        ("T3 rejection condition twice", r => Rejection(r, 1)["condition"] = "owned", "already has a rejection"),
        ("T3 duplicate code", r => Rejection(r, 1)["code"] = "not_owner", "unique"),
        ("T3 empty text", r => Rejection(r, 0)["text"] = " ", "must not be empty"),
        ("T3 unknown rejection condition", r => Rejection(r, 0)["condition"] = "veteran", "T3:"),
    ];

    public static TheoryData<string> MutantNames()
    {
        var data = new TheoryData<string>();
        foreach ((string name, _, _) in Mutants) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(MutantNames))]
    public void EachRuleViolation_IsRefused_WithThatRulesMessage(string mutant)
    {
        (string _, Action<JsonObject> mutate, string expect) = Mutants.Single(m => m.Name == mutant);
        JsonObject root = JsonNode.Parse(ShippedJson())!.AsObject();
        mutate(root);
        var e = Assert.Throws<UnitProgressionContentException>(() => UnitProgressionContentLoader.Load(root.ToJsonString(), Families));
        Assert.Contains(expect, e.Message, StringComparison.Ordinal);
    }

    /// <summary>The control for the mutants: the shipped JSON round-tripped through the same JsonNode path loads unchanged.</summary>
    [Fact]
    public void TheMutantHarness_UnmutatedRoundTrip_Loads()
    {
        JsonObject root = JsonNode.Parse(ShippedJson())!.AsObject();
        UnitProgressionContent c = UnitProgressionContentLoader.Load(root.ToJsonString(), Families);
        Assert.Equal(12, c.Trees.Sum(t => t.Promotions.Count));
    }
}
