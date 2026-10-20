using System.Globalization;
using Xunit;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// ADR-033 D6 — the world map's institution seam (stream U3 left it returning nothing) now reads the REAL
/// Institutions table: a settlement shows a university because an institution row was FOUNDED there (D-038 H5),
/// one entry per type, ascending key, counted, named from research.json; a university BUILDING is drawn once —
/// as the institution — never also as a generic structure glyph; and ResearchCostModifiers still imply nothing.
/// </summary>
public class InstitutionMarkerSourceTests
{
    private static readonly PolityId Me = UiPlayer.Empire;

    [Fact]
    public void InstitutionsAt_GroupsTheFoundedRowsByType_AscendingAndCounted_FromTheRealTable()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        WorldState w = session.World.Clone();
        SettlementId a = w.Settlements[0].Id, b = w.Settlements[1].Id;
        SimConfig cfg = session.Config;
        Assert.Empty(InstitutionMarkerSource.InstitutionsAt(w, cfg, a));          // nothing founded: nothing

        // Rows out of type order, two of type 3 at a, one of type 2 at b; a polity-level cost factor (an EFFECT).
        w.Institutions.Add(new InstitutionRow(1, Me, a, 3, 5, 0.4));
        w.Institutions.Add(new InstitutionRow(2, Me, b, 2, 6, 0.1));
        w.Institutions.Add(new InstitutionRow(3, Me, a, 1, 7, 0.0));
        w.Institutions.Add(new InstitutionRow(4, new PolityId(2), a, 3, 8, 0.9));
        w.ResearchCostModifiers.Add(new ResearchCostModifierRow(Me, 5, 0.8));

        IReadOnlyList<InstitutionView> atA = InstitutionMarkerSource.InstitutionsAt(w, cfg, a);
        Assert.Equal([new InstitutionView(1, "Military University", 1), new InstitutionView(3, "Engineering University", 2)], atA);
        Assert.Equal([new InstitutionView(2, "Medical University", 1)], InstitutionMarkerSource.InstitutionsAt(w, cfg, b));
        Assert.Empty(InstitutionMarkerSource.InstitutionsAt(w, cfg, w.Settlements[2].Id));   // the factor row is no university
    }

    [Fact]
    public void TheLens_DrawsAUniversityOnce_AsTheInstitution_NotAlsoAsAGenericStructure()
    {
        UiSession session = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        WorldState w = session.World.Clone();
        SimConfig cfg = session.Config;
        SettlementId a = w.Settlements[0].Id;
        int granary = -1, engineering = -1;
        foreach (ConstructionProjectEntry p in cfg.Goods!.Projects!)
        {
            if (p.Name == "granary") granary = p.Id;
            if (p.Founds?.UniversityType == "engineering_university") engineering = p.Id;
        }
        w.Structures.Add(new StructureRow(a, granary, 1));
        w.Structures.Add(new StructureRow(a, engineering, 1));                  // the university building...
        w.Institutions.Add(new InstitutionRow(1, Me, a, 3, 5, 0.5));            // ...and the institution it founded

        WorldProjection p2 = WorldProjection.Build(w, cfg, id => "S" + id.ToString(CultureInfo.InvariantCulture), Me);
        SettlementLensView view = p2.Settlements.First(s => s.Id == a.Value);
        Assert.Equal([granary], view.Structures.Select(st => st.ProjectId));     // the generic glyphs: the granary only
        Assert.Equal([new InstitutionView(3, "Engineering University", 1)], view.Institutions);
        Assert.DoesNotContain(p2.Absent, x => x.StartsWith("Institutions", StringComparison.Ordinal));

        LensFrame f = WorldLens.Paint(new DrawList(), ApproxTextMeasure.Instance, p2, WorldZoom.Settlement, (x, y) => (x * 4, y * 4), 4,
            new RectD(0, 0, 1024, 1024));
        var markers = f.Institutions.Where(m => m.Settlement == a.Value).Select(m => m.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(["institution:3", "structure:" + granary.ToString(CultureInfo.InvariantCulture)], markers);
    }
}
