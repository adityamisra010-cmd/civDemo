using System.Text.Json.Nodes;
using Sim.Ui.World;
using Sim.Ui.World.Content;
using Sim.Ui.World.View;
using Xunit;
using static Sim.Ui.Tests.World.WorldTestKit;

namespace Sim.Ui.Tests.World;

/// <summary>Task Part 4: deterministic, data-driven visual morphology. An institution GROWS
/// (stage k contains stage k-1 unchanged), stages follow a content-chosen driver, and a
/// driver that is not reported draws the base stage — never a substitute.</summary>
public class WorldMorphologyTests
{
    [Fact]
    public void EveryVisualType_GrowsCumulatively_EarlierPartsStayPut()
    {
        foreach (VisualType t in Morph().VisualTypes)
            for (int k = 1; k < t.Stages.Count; k++)
            {
                IReadOnlyList<MorphPart> prev = t.PartsAt(k - 1), now = t.PartsAt(k);
                Assert.True(now.Count > prev.Count, $"{t.Id} stage {k} adds nothing");
                for (int i = 0; i < prev.Count; i++) Assert.Equal(prev[i], now[i]);   // same part, same local coordinates
            }
    }

    [Fact]
    public void Thresholds_AreInclusive_AtEveryBoundary()
    {
        WorldMorphology m = Morph();
        foreach (VisualType t in m.VisualTypes)
        {
            if (t.StageBy.Input is not string input) continue;
            for (int k = 1; k < t.Stages.Count; k++)
            {
                double min = t.Stages[k].Min;
                StructureReport at = StructureOf("x", t.Id, "s", 0, input == WorldViewConstants.MultiplicityInput ? (long)min : 1,
                    ReportedVisibility.Visible, input == WorldViewConstants.MultiplicityInput ? [] : [(input, min)]);
                Assert.Equal(k, Morphology.Structure(at, t).Index);
                if (input == WorldViewConstants.MultiplicityInput) continue;
                StructureReport below = StructureOf("x", t.Id, "s", 0, 1, ReportedVisibility.Visible, (input, Math.BitDecrement(min)));
                Assert.Equal(k - 1, Morphology.Structure(below, t).Index);
            }
        }
        foreach (StageDriver d in m.Settlements.Drivers)
            for (int k = 1; k < d.Thresholds.Count; k++)
            {
                Assert.Equal(k, Morphology.Settlement(SettlementAt("s", 0, 0, (d.Input, d.Thresholds[k])), m.Settlements, allowDemonstration: true).Index);
                Assert.Equal(k - 1, Morphology.Settlement(SettlementAt("s", 0, 0, (d.Input, Math.BitDecrement(d.Thresholds[k]))), m.Settlements, allowDemonstration: true).Index);
            }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-5.0)]
    public void AnUnusableDriver_DrawsTheBaseStage_AndSaysSo(double value)
    {
        VisualType uni = Morph().Visual("university")!;
        StageChoice c = Morphology.Structure(StructureOf("x", "university", "s", 0, 1, ReportedVisibility.Visible, ("capacity", value)), uni);
        Assert.Equal(0, c.Index);
        Assert.False(c.Reported);
        Assert.Contains("not reported", c.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingDriver_IsNeverSubstituted()
    {
        // A report with every OTHER input huge and a late establishment still draws the base stage.
        VisualType uni = Morph().Visual("university")!;
        StageChoice c = Morphology.Structure(StructureOf("x", "university", "s", 999, 50, ReportedVisibility.Visible,
            ("staff", 1e9), ("served", 1e9)), uni);
        Assert.Equal(0, c.Index);
        Assert.Contains("capacity not reported", c.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ALabelDriver_MapsThroughTheOrdinalTable()
    {
        VisualType academy = Morph().Visual("military-academy")!;
        StructureReport Of(string maturity) => new("x", "x", "military-academy", "s", null, PolityRelation.None, 0, 1, [],
            [new ReportedLabel("maturity", maturity)], null, null, ReportedVisibility.Visible, "t");
        Assert.Equal(0, Morphology.Structure(Of("NEW"), academy).Index);
        Assert.Equal(1, Morphology.Structure(Of("DEVELOPING"), academy).Index);
        Assert.Equal(2, Morphology.Structure(Of("ESTABLISHED"), academy).Index);
        Assert.Equal(2, Morphology.Structure(Of("MATURE"), academy).Index);
        Assert.False(Morphology.Structure(Of("LEGENDARY"), academy).Reported);   // unknown label: base, flagged
    }

    /// <summary>Changing WHAT drives a type's growth is a content edit — capacity, a maturity
    /// label, the report's multiplicity — with no renderer change.</summary>
    [Fact]
    public void TheDriver_IsChosenByContentAlone()
    {
        JsonObject Edit(JsonObject rule)
        {
            JsonObject o = JsonNode.Parse(MorphologyJson)!.AsObject();
            JsonObject uni = o["visualTypes"]!.AsArray().Select(n => n!.AsObject()).First(v => (string?)v["id"] == "university");
            uni["stageBy"] = rule;
            return o;
        }
        WorldMorphology Load(JsonObject o) => WorldContentLoader.LoadMorphology(o.ToJsonString()).Morphology!;

        var report = new StructureReport("x", "x", "university", "s", null, PolityRelation.None, 0, 3,
            [new ReportedInput("capacity", 2500)], [new ReportedLabel("maturity", "DEVELOPING")], null, null, ReportedVisibility.Visible, "t");

        WorldMorphology byCapacity = Load(Edit(new JsonObject { ["input"] = "capacity" }));
        WorldMorphology byLabel = Load(Edit(new JsonObject { ["label"] = "maturity", ["ordinals"] = new JsonObject { ["NEW"] = 0, ["DEVELOPING"] = 800 } }));
        WorldMorphology byCount = Load(Edit(new JsonObject { ["input"] = "multiplicity" }));
        Assert.Equal(2, Morphology.Structure(report, byCapacity.Visual("university")!).Index);   // 2500 >= 2000
        Assert.Equal(1, Morphology.Structure(report, byLabel.Visual("university")!).Index);      // DEVELOPING = 800
        Assert.Equal(0, Morphology.Structure(report, byCount.Visual("university")!).Index);      // 3 < 800
    }

    [Fact]
    public void SettlementStage_PrefersTheSimulationsSizeTier_AndNeverNamesIt()
    {
        WorldMorphology m = Morph();
        StageChoice live = Morphology.Settlement(SettlementAt("s", 0, 0, ("sizeTier", 2), ("population", 900000)), m.Settlements, allowDemonstration: false);
        Assert.Equal(2, live.Index);
        Assert.DoesNotContain("Town", live.Name, StringComparison.Ordinal);
        Assert.DoesNotContain("City", live.Name, StringComparison.Ordinal);
        Assert.StartsWith("sizeTier 2", live.Name, StringComparison.Ordinal);

        StageChoice demo = Morphology.Settlement(SettlementAt("s", 0, 0, ("population", 12000)), m.Settlements, allowDemonstration: true);
        Assert.Equal("Town", demo.Name);
        Assert.Contains("DEMONSTRATION", demo.Explanation, StringComparison.Ordinal);

        StageChoice none = Morphology.Settlement(SettlementAt("s", 0, 0), m.Settlements, allowDemonstration: true);
        Assert.Equal(0, none.Index);
        Assert.False(none.Reported);

        // A LIVE settlement with only a population is never staged by the demonstration thresholds.
        StageChoice livePopOnly = Morphology.Settlement(SettlementAt("s", 0, 0, ("population", 900000)), m.Settlements, allowDemonstration: false);
        Assert.Equal(0, livePopOnly.Index);
        Assert.False(livePopOnly.Reported);
        Assert.DoesNotContain("DEMONSTRATION", livePopOnly.Explanation, StringComparison.Ordinal);
        Assert.StartsWith("base footprint", livePopOnly.Name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(270.0)]
    [InlineData(-90.0)]
    [InlineData(630.0)]
    public void Headings_Normalise_ToOneValue(double deg) => Assert.Equal(270.0, WorldViewBuilder.NormalizeHeading(deg));

    /// <summary>Task Parts 4/5/14 against the shipped demo: the featured city's institutions
    /// appear and grow step by step (visual stages chosen from the reported values).</summary>
    [Fact]
    public void DemoTimeline_InstitutionsAppearAndGrow()
    {
        WorldMorphology m = Morph();
        int? Stage(int step, string key) => DemoView(m, step).FindStructure(Structure(key))?.Stage.Index;
        Assert.Null(Stage(0, "u-123"));                      // State A: no university
        Assert.Null(Stage(0, "h-7"));                        // State A: no hospital
        Assert.Equal(0, Stage(2, "u-123"));                  // State B: first university, main building
        Assert.Equal(0, Stage(2, "h-7"));                    // State B: first hospital
        Assert.Equal(1, Stage(3, "u-123"));                  // expanded wing
        Assert.Equal(1, Stage(3, "h-7"));                    // State C: hospital expansion
        Assert.Equal(0, Stage(3, "f-40"));                   // State C: factories
        Assert.Equal(3, Stage(4, "u-123"));                  // State D: campus
        Assert.Equal(2, Stage(4, "h-7"));                    // medical complex
        Assert.Equal(2, Stage(4, "f-40"));                   // factory growth
        int stageA = DemoView(m, 0).Settlement("s-veyra")!.Stage.Index, stageD = DemoView(m, 4).Settlement("s-veyra")!.Stage.Index;
        Assert.True(stageD > stageA);
    }

    /// <summary>Quantity to visual at civilization scale: the summary counts REPORTS (sum of
    /// multiplicity) — 1, 10, 20, 30 universities at steps B, C, D, E — never an inference.</summary>
    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 10)]
    [InlineData(4, 20)]
    [InlineData(5, 30)]
    public void Summary_CountsReportedUniversities(int step, long expected)
    {
        WorldView v = DemoView(Morph(), step);
        Assert.Equal(expected, v.Summary.Single(l => l.VisualType == "university").Reported);
        long direct = v.Structures.Where(s => s.Drawable && s.Type.Id == "university").Sum(s => Math.Max(1, s.Report.Multiplicity));
        Assert.Equal(expected, direct);
    }

    /// <summary>Past the (provisional, D-038 H8) per-settlement limit, the rest share ONE cluster
    /// token — its members are exactly the reports beyond the limit, in priority order.</summary>
    [Fact]
    public void Aggregation_ClustersOnlyWhatIsReportedBeyondTheLimit()
    {
        WorldMorphology m = Morph();
        WorldView v = DemoView(m, 5);
        ClusterView k = v.Clusters.Single(c => c.SettlementKey == "s-veyra" && c.Type.Id == "university");
        Assert.Equal([Structure("u-162")], k.Members);
        Assert.Equal(m.Aggregation.IndividualUpTo, v.Structures.Count(s => s.Report.SettlementKey == "s-veyra" && s.Type.Id == "university" && s.Slot is not null));
        // A single report with multiplicity is one token, never expanded.
        StructureView colleges = v.FindStructure(Structure("u-mora-colleges"))!;
        Assert.Equal(4, colleges.Report.Multiplicity);
        Assert.NotNull(colleges.Slot);
    }
}
