using Sim.Core.Chronicle;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

/// <summary>Integration items 3 (state-derived annal events), 4 (colony names) and 6 (resume by replay).</summary>
public class ChronicleAndResumeTests
{
    private static UiSession Played(int turns, int? ai = null)
    {
        var s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: ai);
        for (int t = 0; t < turns; t++) s.EndTurn();
        return s;
    }

    private static List<string> Diff(UiSession s, WorldState prev, WorldState next)
    {
        var lines = new List<string>();
        new StateChronicle().Observe(prev, next, s.Config, s.Names.Name, lines);
        return lines;
    }

    [Fact]
    public void AStepThatChangesNothingWritesNoEvent()
    {
        UiSession s = Played(2);
        Assert.Empty(Diff(s, s.World, s.World.Clone()));
    }

    [Fact]
    public void ConstructionInstitutionsControlAndTax_EachBecomeOneAnnalLine()
    {
        UiSession s = Played(1);
        WorldState prev = s.World;
        WorldState next = prev.Clone();
        SettlementId site = next.Settlements[0].Id;
        string where = s.Names.Name(site.Value);
        var project = s.Config.Goods!.Projects![0];
        next.Structures.Add(new StructureRow(site, project.Id, ConstructionQuery.Built(prev, site, project.Id) + 1));
        int type = s.Config.Research!.UniversityTypes[0].Key;
        next.Institutions.Add(new InstitutionRow(77, UiPlayer.Empire, site, type, next.Clock.Turn, 0.1));
        next.TaxPolicies.Add(new TaxPolicyRow(UiPlayer.Empire, 0.1));
        // Control lost: drop every control row of the second settlement (it was the player's).
        SettlementId lost = next.Settlements[1].Id;
        Assert.True(EmpireQuery.TryGetController(prev, lost, out _));
        var keep = new List<ControlRow>();
        for (int i = 0; i < next.Controls.Count; i++) if (next.Controls[i].Place != lost) keep.Add(next.Controls[i]);
        next.Controls.Clear();
        foreach (ControlRow c in keep) next.Controls.Add(c);

        List<string> lines = Diff(s, prev, next);
        Assert.Contains(lines, l => l.EndsWith(where + " completed a " + project.Name.ToLowerInvariant() + ".", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(" was founded at " + where + ".", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith("Your empire levied a tax of 10%.", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(s.Names.Name(lost.Value) + " rose up and threw off the rule of your empire.", StringComparison.Ordinal));
        foreach (string l in lines) Assert.StartsWith("In the year ", l, StringComparison.Ordinal);

        // Maturity: the same institution crossing matureAt is one "came to maturity" line, and only then.
        WorldState grown = next.Clone();
        for (int i = 0; i < grown.Institutions.Count; i++)
            if (grown.Institutions[i].Id == 77) { InstitutionRow r = grown.Institutions[i]; grown.Institutions[i] = r with { Maturity = 0.999 }; }
        List<string> later = Diff(s, next, grown);
        Assert.Single(later);
        Assert.EndsWith(" at " + where + " came to maturity.", later[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ColoniesFoundedMidGame_AreNamed_AndExistingNamesNeverChange()
    {
        // The registry is a pure function of (seed, ids): rebuilding over a world that gained ids keeps the
        // earlier names — what UiSession relies on when it rebuilds after a founding step.
        UiSession s = Played(1);
        WorldState grown = s.World.Clone();
        int newId = 0;
        for (int i = 0; i < grown.Settlements.Count; i++) newId = Math.Max(newId, grown.Settlements[i].Id.Value + 1);
        grown.Settlements.Add(grown.Settlements[0] with { Id = new SettlementId(newId) });
        using var stream = Sim.Data.DataFiles.OpenChronicle();
        ChronicleConfig cc = ChronicleConfigLoader.Load(stream);
        NameRegistry before = NameRegistry.Build(cc, s.World.Seed, s.World);
        NameRegistry after = NameRegistry.Build(cc, s.World.Seed, grown);
        for (int i = 0; i < s.World.Settlements.Count; i++)
            Assert.Equal(before.Name(s.World.Settlements[i].Id.Value), after.Name(s.World.Settlements[i].Id.Value));
        Assert.DoesNotContain("settlement", after.Name(newId), StringComparison.Ordinal);
        // And in a played session no settlement — colonies included — falls back to "settlement N".
        UiSession played = Played(60);
        for (int i = 0; i < played.World.Settlements.Count; i++)
            Assert.DoesNotContain("settlement ", played.Names.Name(played.World.Settlements[i].Id.Value), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlayerTitleCarriesTheNameOnly()
    {
        UiSession s = Played(0);
        int id = s.World.Settlements[0].Id.Value;
        HudModel hud = HudModel.From(s.World, id, null, s.Names.Name(id));
        Assert.Equal(s.Names.Name(id), hud.TitleLine);
    }

    [Fact]
    public void Resume_RebuildsTheWorldAndTheUiRecords_AndContinuedPlayIsIdentical()
    {
        string dir = Path.Combine(Path.GetTempPath(), "u2b-resume-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        string log = Path.Combine(dir, "orders-20260101-120000-s256-n4-a1.bin");

        UiSession live = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4, aiEmpiresOverride: 1);
        live.ExportManifest("2026-01-01 12:00:00", log);
        int capital = live.World.Settlements[0].Id.Value;
        for (int t = 0; t < 8; t++)
        {
            if (t == 2) Assert.True(live.EmitSectorOrders([40, 20, 10, 10, 20], capital));
            if (t == 3 && live.Config.Research is { } rc)
            {
                ResearchNodeId[] avail = ResearchQuery.AvailableNodes(live.World, rc, UiPlayer.Empire);
                if (avail.Length > 0) Assert.True(live.EmitResearchOrder(avail[0]));
            }
            live.EndTurn();
        }
        Assert.True(live.EmitSectorOrders([30, 30, 10, 10, 20], capital));   // queued, not yet played
        int aiOrders = 0;
        for (int i = 0; i < live.Orders.Count; i++) if (live.Orders[i].ActorId != UiPlayer.ActorId) aiOrders++;
        live.Save(log);
        live.ExportTrace(UiSession.TracePath(log));

        UiSession resumed = UiSession.Resume(UiArgs.ResolveManifest(dir), out string resumedLog);
        Assert.Equal(Path.GetFullPath(log), Path.GetFullPath(resumedLog));
        Assert.Equal(live.World.Clock.Turn, resumed.World.Clock.Turn);
        Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
        Assert.Equal(live.Orders.Count, resumed.Orders.Count);
        Assert.Equal(live.AnnalLines.ToArray(), resumed.AnnalLines.ToArray());
        Assert.Equal(live.TraceLines.ToArray(), resumed.TraceLines.ToArray());
        Assert.Equal(live.Observations.Observations.Count, resumed.Observations.Observations.Count);
        Assert.Equal(live.Observations.PolicyChanges.Count, resumed.Observations.PolicyChanges.Count);
        for (int i = 0; i < live.World.Settlements.Count; i++)
        {
            int id = live.World.Settlements[i].Id.Value;
            Assert.Equal(live.Names.Name(id), resumed.Names.Name(id));
        }

        for (int t = 0; t < 4; t++) { live.EndTurn(); resumed.EndTurn(); }
        Assert.Equal(WorldHash.ComputeHex(live.World), WorldHash.ComputeHex(resumed.World));
        Assert.Equal(live.Orders.Count, resumed.Orders.Count);
        for (int i = 0; i < live.Orders.Count; i++) Assert.Equal(live.Orders[i], resumed.Orders[i]);
        Assert.True(aiOrders > 0, "the AI empire issued no order - the AI part of the replay was not exercised");
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Resume_RefusesATraceItCannotReproduce()
    {
        UiSession live = Played(3);
        UiSession fresh = Played(0);
        var bad = new List<SessionTrace.Row> { new(1, 0, 0, 0, 0, "deadbeef") };
        Assert.Throws<InvalidOperationException>(() => fresh.ReplayTo(live.Orders, 3, bad));
    }
}

/// <summary>Integration item 5 — what the text says is simulated is what is simulated.</summary>
public class TruthInTextTests
{
    [Fact]
    public void TheLenses_NoLongerDenyRoadsUniversitiesOrCrafting()
    {
        var s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        var content = s.Config.Research!;
        foreach (Sim.Ui.Progression.Lens lens in new[] { Sim.Ui.Progression.Lens.Infrastructure, Sim.Ui.Progression.Lens.Institutions, Sim.Ui.Progression.Lens.Industry })
        {
            Sim.Ui.Progression.LensPage page = Sim.Ui.Progression.Lenses.Page(lens, s.World, content, UiPlayer.Empire);
            Assert.DoesNotContain("construction of these is not yet simulated", page.StatusNote);
            Assert.DoesNotContain("No system produces industry state", page.StatusNote);
            Assert.DoesNotContain("Establishing an institution belongs to its owning system, not yet simulated", page.StatusNote);
        }
    }

    [Fact]
    public void TheWorldCaption_NamesTheTopSectorByItsKnowledgeLabel()
    {
        var s = UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        int capital = s.World.Settlements[0].Id.Value;
        Assert.True(s.EmitSectorOrders([60, 10, 10, 10, 10], capital));
        s.EndTurn();
        Sim.Ui.World.WorldProjection p = Sim.Ui.World.WorldProjection.Build(s.World, s.Config, s.Names.Name, UiPlayer.Empire);
        Sim.Ui.World.SettlementLensView v = p.Settlements.Single(x => x.Id == capital);
        string expected = LabourActivities.For(s.World, s.Config, UiPlayer.Empire)
            .Single(a => a.Settlement.Value == capital && a.Sector == Sectors.Farming).Label;
        Assert.Equal(expected, v.TopSectorLabel);
    }
}
