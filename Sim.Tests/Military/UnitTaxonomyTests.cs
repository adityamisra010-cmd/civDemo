using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 §1 — THE TAXONOMY (unit charter §4.5, §5, Q2 resolved; D-065 R8–R14 (Addendum 3); aviation-progression §3, §9.3):
/// twenty families, keys 13, 15 and 16 withdrawn (abstract military support, category B) and unallocated; append-only keys, the
/// five rehomings with the §5.3 branch-flag edits, the staged aviation identities, a knowledge gate and recruitment data on
/// every identity, the plannedGoods map with its owning packets, the loader's refusals (a good that is neither real nor
/// planned can never silently block), and ADR-031's modernization contract kept.
/// </summary>
public class UnitTaxonomyTests
{
    /// <summary>Every identity key of the 12-family file (ADR-031) — still present, never renumbered.</summary>
    private static readonly int[] OldKeys =
    [
        101, 102, 103, 104, 105, 201, 202, 203, 204, 301, 309, 302, 303, 304, 305, 306, 307, 308,
        401, 402, 403, 404, 405, 406, 407, 408, 501, 502, 503, 504, 505, 506, 601, 602, 603, 604, 605,
        701, 702, 703, 801, 802, 803, 804, 805, 806, 901, 902, 903, 904, 905, 906, 907, 908,
        1001, 1002, 1003, 1004, 1005, 1006, 1101, 1102, 1103, 1104, 1201, 1202,
    ];

    [Fact]
    public void TwentyFamilies_KeysAppendOnly_13_15_16Unallocated_NewIdentitiesFollowFamilyTimes100()
    {
        Assert.Equal(66, OldKeys.Length);
        Assert.Equal(20, Families.Families.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 17, 18, 19, 20, 21, 22, 23], Families.Families.Select(f => f.Key));
        Assert.Equal(["air_defence", "naval_raiders", "submarines", "carriers",
            "rotary_wing", "unmanned_aircraft", "reconnaissance", "strike_aviation"],
            Families.Families.Skip(12).Select(f => f.Id));
        // D-065 R9–R10 (Addendum 3): combat engineers, military supply and field medical are abstract support capabilities —
        // no family, no identity under their withdrawn keys; R12–R14: no prospector, sniper or trade/shipping family.
        foreach (int withdrawn in new[] { 13, 15, 16 }) Assert.Null(Families.FamilyByKey(withdrawn));
        foreach (string absent in new[] { "combat_engineers", "military_supply", "field_medical", "prospectors", "snipers", "merchant_shipping" })
            Assert.Null(Families.FamilyById(absent));
        Assert.Equal(6, Families.FamilyById("naval_transport")!.Line.Count);   // R13: family 10 unchanged (ADR-031 amendment pending)
        foreach (int key in OldKeys) Assert.NotNull(Families.IdentityByKey(key));
        var keys = new List<int>();
        foreach (UnitFamily f in Families.Families)
            foreach (UnitIdentity i in f.Line)
            {
                Assert.DoesNotContain(i.Key, keys);
                keys.Add(i.Key);
                if (Array.IndexOf(OldKeys, i.Key) < 0) Assert.Equal(f.Key, i.Key / 100);   // a new key is family × 100 + n
            }
        Assert.Equal(80, keys.Count);
    }

    [Fact]
    public void TheFiveRehomings_CarryTheSection53BranchEdits()
    {
        Assert.Equal(("reconnaissance", false), (Families.FamilyByKey(Id("scouts").FamilyKey)!.Id, Id("scouts").Branch));
        Assert.Equal(("submarines", true), (Families.FamilyByKey(Id("nuclear_submarines").FamilyKey)!.Id, Id("nuclear_submarines").Branch));
        Assert.Equal(("carriers", false), (Families.FamilyByKey(Id("aircraft_carriers").FamilyKey)!.Id, Id("aircraft_carriers").Branch));
        Assert.Equal(("rotary_wing", false), (Families.FamilyByKey(Id("helicopters").FamilyKey)!.Id, Id("helicopters").Branch));
        Assert.Equal(("unmanned_aircraft", true), (Families.FamilyByKey(Id("combat_drones").FamilyKey)!.Id, Id("combat_drones").Branch));
        Assert.Equal(UnitComposition.MobileAgent, Families.FamilyById("reconnaissance")!.Composition);
        Assert.Empty(Families.FamilyById("naval_combat")!.Branches);
        foreach (UnitFamilyBranch b in Families.FamilyById("air")!.Branches)
            Assert.DoesNotContain("helicopters", b.Identities);
        Assert.Equal("skirmishers", Families.FamilyById("light_infantry")!.Line[0].Id);   // the family now begins at A3
        Assert.Equal(3, Families.FamilyById("light_infantry")!.FirstAge);
    }

    [Fact]
    public void Aviation_IsStagedFromA8_WithBranchesNeverModernizationTargets()
    {
        UnitFamily air = Families.FamilyById("air")!;
        Assert.Equal(8, Id("military_aircraft").Age);
        Assert.Equal(("military_aircraft", "advanced_piston_aircraft"), (air.RealizationAt(8)!.Id, air.RealizationAt(9)!.Id));
        Assert.Equal("aircraft_metal", Id("advanced_piston_aircraft").RequiresSource);
        Assert.Equal(("jet_engine", true), (Id("jet_fighters").RequiresSource, Id("jet_fighters").Branch));
        Assert.True(Id("stealth_aircraft").Branch);
        UnitFamily strike = Families.FamilyById("strike_aviation")!;
        Assert.Equal(("light_bombers", "strategic_bombers"), (strike.RealizationAt(8)!.Id, strike.RealizationAt(9)!.Id));
        Assert.Equal(("aircraft", "aircraft_metal", "stealth AND aircraft_metal"),
            (Id("light_bombers").RequiresSource, Id("strategic_bombers").RequiresSource, Id("stealth_bombers").RequiresSource));
        Assert.Equal("rifled_artillery AND aircraft", Id("anti_aircraft_artillery").RequiresSource);
        Assert.Equal(("guided_missile AND radar", true), (Id("sam_batteries").RequiresSource, Id("sam_batteries").Branch));
        Assert.Equal(("jet_engine AND steam_turbine", true), (Id("jet_capable_carriers").RequiresSource, Id("jet_capable_carriers").Branch));
        Assert.Equal("aerial_reconnaissance AND computer", Id("reconnaissance_drones").RequiresSource);
        foreach (string a in new[] { "military_aircraft", "advanced_piston_aircraft", "jet_fighters", "stealth_aircraft", "light_bombers", "strategic_bombers", "stealth_bombers", "helicopters", "reconnaissance_drones", "combat_drones" })
            Assert.Equal("building.airfield", Id(a).Recruitment!.Building);
        // Flight I needs timber, cloth and tools (existing) plus engines and fuel (EC-4a) — no aluminium.
        Assert.Equal(["timber", "cloth", "tools", "engines", "fuel"], Id("military_aircraft").Recruitment!.Inputs.Select(i => i.Good));
        // ADR-031 with the new content: modernization follows the mainline (bombers stay bombers) and never targets a branch.
        Assert.Equal(Id("advanced_piston_aircraft").Key, MilitaryQuery.PlanFor(Families, Row("military_aircraft"), 9).ToIdentity);
        Assert.Equal(Id("strategic_bombers").Key, MilitaryQuery.PlanFor(Families, Row("light_bombers"), 9).ToIdentity);
        foreach (UnitFamily f in Families.Families)
            for (int age = 1; age <= 9; age++)
                Assert.False(f.RealizationAt(age) is { Branch: true });
        // An unknown identity is preserved.
        var unknown = new MilitaryUnitRow(1, Player, 99, 9999, new SettlementId(0), 0, 0, 0, 0);
        Assert.Equal(ModernizationOutcome.PreservedNoSuccessor, MilitaryQuery.PlanFor(Families, unknown, 9).Outcome);
    }

    [Fact]
    public void TheEmbarkPredicate_IsCarrierClassTimesIdentity()
    {
        UnitIdentity carrier = Id("aircraft_carriers"), jetDeck = Id("jet_capable_carriers");
        Assert.True(RecruitmentQuery.CanEmbark(Families, Id("military_aircraft"), carrier));
        Assert.True(RecruitmentQuery.CanEmbark(Families, Id("advanced_piston_aircraft"), carrier));
        Assert.True(RecruitmentQuery.CanEmbark(Families, Id("light_bombers"), carrier));
        Assert.False(RecruitmentQuery.CanEmbark(Families, Id("strategic_bombers"), carrier));
        Assert.False(RecruitmentQuery.CanEmbark(Families, Id("jet_fighters"), carrier));
        Assert.True(RecruitmentQuery.CanEmbark(Families, Id("jet_fighters"), jetDeck));
        Assert.False(RecruitmentQuery.CanEmbark(Families, Id("military_aircraft"), Id("destroyers")));
    }

    [Fact]
    public void EveryIdentity_HasAKnowledgeGateAndRecruitmentData_AndEveryGoodIsRealOrPlanned()
    {
        GoodsConfig goods = Cfg.Goods!;
        foreach (UnitFamily f in Families.Families)
            foreach (UnitIdentity i in f.Line)
            {
                Assert.True(i.Baseline is not null || i.RealizedBy is not null || i.Requires is not null, i.Id);
                Assert.NotNull(i.Recruitment);
                Assert.True(i.Recruitment!.Personnel >= 1 && i.Recruitment.Years > 0, i.Id);
                foreach (UnitRecruitInput x in i.Recruitment.Inputs)
                    Assert.True(x.IsPlanned ? goods.IdOf(x.Good) < 0 && x.PlannedPacket is not null : goods.IdOf(x.Good) == x.GoodId, i.Id + " " + x.Good);
            }
        Assert.Equal(
            [("iron-ore", "EC-1"), ("coal", "EC-1"), ("iron", "EC-1"), ("steel", "EC-1"), ("horses", "EC-2"), ("saltpetre", "EC-3"),
             ("sulphur", "EC-3"), ("gunpowder", "EC-3"), ("explosives", "EC-3"), ("engines", "EC-4a"), ("fuel", "EC-4a"),
             ("aluminium", "EC-5a"), ("electronics", "EC-5b"), ("composites", "EC-5c"), ("uranium", "EC-6")],
            Families.PlannedGoods.Select(p => (p.Good, p.Packet)));
        // The A1–A3 land identities the M7.3 slice names are recruitable from existing goods alone.
        foreach (string id in new[] { "warband", "axe_warriors", "bronze_swordsmen", "spearmen", "slingers", "archers", "skirmishers" })
            Assert.All(Id(id).Recruitment!.Inputs, x => Assert.False(x.IsPlanned, id + " " + x.Good));
    }

    [Theory]
    [InlineData("\"good\": \"bronze\", \"qty\": 10", "\"good\": \"bronz\", \"qty\": 10", "neither a goods.json good nor in plannedGoods")]
    [InlineData("\"requires\": \"hafting\"", "\"requires\": \"haftingg\"", "haftingg")]
    [InlineData("\"baseline\": \"baseline.basic_military\"", "\"baseline\": \"baseline.basic_militar\"", "not a research.json baseline capability")]
    [InlineData("\"building\": \"building.airfield\"", "\"building\": \"unit.tank\"", "not a research.json building entity")]
    [InlineData("{ \"good\": \"iron\", \"packet\": \"EC-1\"", "{ \"good\": \"iron\", \"packet\": \"\"", "packet must name")]
    public void TheLoader_RefusesABrokenGate_OrAGoodThatIsNeitherRealNorPlanned(string from, string to, string message)
    {
        string json = FamiliesJson();
        Assert.Contains(from, json);
        var e = Assert.Throws<UnitFamilyContentException>(() =>
            UnitFamilyContentLoader.Load(json.Replace(from, to), Research, Cfg.Goods));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void TheLoader_UnderSchema2_RequiresAGateAndRecruitmentOnEveryIdentity()
    {
        string json = FamiliesJson();
        string noGate = json.Replace("\"requires\": \"hafting\", ", "");
        Assert.Contains("declares a knowledge gate", Assert.Throws<UnitFamilyContentException>(() => UnitFamilyContentLoader.Load(noGate, Research, Cfg.Goods)).Message);
        int at = json.IndexOf("\"id\": \"axe_warriors\"", StringComparison.Ordinal);
        int rec = json.IndexOf(", \"recruitment\": ", at, StringComparison.Ordinal);
        int end = json.IndexOf("} }", rec, StringComparison.Ordinal) + 3;
        string noRecruit = json[..rec] + " }" + json[end..];
        Assert.Contains("carries recruitment data", Assert.Throws<UnitFamilyContentException>(() => UnitFamilyContentLoader.Load(noRecruit, Research, Cfg.Goods)).Message);
    }

    private static MilitaryUnitRow Row(string identity) =>
        new(1, Player, Id(identity).FamilyKey, Id(identity).Key, new SettlementId(0), 0, 0, 0, 0);

    private static string FamiliesJson()
    {
        using var s = Sim.Data.DataFiles.OpenUnitFamilies();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
