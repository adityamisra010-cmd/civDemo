using System.Text.RegularExpressions;
using Sim.Core;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using Xunit;
using static Sim.Tests.TestUtil.UniversityRigs;

namespace Sim.Tests.State;

/// <summary>
/// ADR-057 (M6 CIV, P5; D-D option 1 RULED, D-066 A2 item 2, A10, A13) — THE REGIME-MODULE DATA SHAPE, read-only: one module
/// ("codified law") PRESENT exactly while its owner holds an ACTIVE law court; the government label DERIVED from the base
/// label and the present modules' qualifiers ("chiefdom" → "law-giving chiefdom"), never stored; the 33 government forms
/// shown "Available - adoption in M8" once researched, with their locked reasons before.
/// </summary>
public sealed class RegimeQueryTests
{
    private static readonly PolityId Player = new(1);
    private static readonly PolityId Rival = new(2);

    [Fact]
    public void ShippedShape_OneModule_CodifiedLaw_OnTheLawCourt_BaseLabelChiefdom()
    {
        SimConfig cfg = TestConfigs.Sim();
        RegimeConfig r = cfg.Regime!;
        Assert.Equal("chiefdom", r.BaseLabel);
        RegimeModuleConfig m = Assert.Single(r.Modules);
        Assert.Equal(("codified_law", "Codified law", "law_court", "law-giving"), (m.Id, m.Name, m.InstitutionType, m.Qualifier));
        Assert.Equal(33, r.Forms.Length);
    }

    [Fact]
    public void Module_IsPresentExactlyWhileTheOwnerHoldsAnActiveCourt_AndTheLabelIsDerived()
    {
        SimConfig cfg = TestConfigs.Sim();
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, cfg, 42);
        SettlementId s = LabourActivities.ControlledSettlements(w, Player)[0];
        int court = InstitutionContent.TypeKeyOf(cfg, Sim.Tests.Systems.LawCourtTests.Court(cfg));
        Assert.Equal("chiefdom", RegimeQuery.Label(w, cfg, Player));
        Assert.Equal("absent: needs an active law court", Assert.Single(RegimeQuery.Modules(w, cfg, Player)).Reason);

        // a university is no court: still absent
        WorldState uni = w.Clone();
        Found(uni, Player, s, 1, 1.0);
        Assert.Equal("chiefdom", RegimeQuery.Label(uni, cfg, Player));
        // the RIVAL's court gives the player nothing
        WorldState rivals = w.Clone();
        Found(rivals, Rival, LabourActivities.ControlledSettlements(w, Rival)[0], court, 0.5);
        Assert.Equal("chiefdom", RegimeQuery.Label(rivals, cfg, Player));
        Assert.Equal("law-giving chiefdom", RegimeQuery.Label(rivals, cfg, Rival));
        // the player's court at maturity 0 (just founded: ACTIVE) is enough
        WorldState own = w.Clone();
        Found(own, Player, s, court, 0.0);
        RegimeModuleView present = Assert.Single(RegimeQuery.Modules(own, cfg, Player));
        Assert.True(present.Present);
        Assert.Equal("present: 1 active law court", present.Reason);
        Assert.Equal("law-giving chiefdom", RegimeQuery.Label(own, cfg, Player));
        // DERIVED, never stored: the label is a pure function of the world — removing the row removes it
        own.Institutions.Clear();
        Assert.Equal("chiefdom", RegimeQuery.Label(own, cfg, Player));
    }

    /// <summary>D-066 A10: a researched form reads "Available - adoption in M8"; a researchable one says researching it is not
    /// adopting it; a locked one names its missing prerequisites.</summary>
    [Fact]
    public void Forms_AreAvailableForAdoptionInM8_OnceResearched_WithLockedReasonsBefore()
    {
        SimConfig cfg = TestConfigs.Sim();
        ResearchContent rc = cfg.Research!;
        WorldState w = WorldFounding.Found(TestConfigs.DevWorldgen(), cfg, 42);
        GovernmentFormView[] before = RegimeQuery.Forms(w, cfg, Player);
        Assert.Equal(33, before.Length);
        Assert.DoesNotContain(before, f => f.State == CivicState.Researched);
        GovernmentFormView chiefdom = Assert.Single(before, f => f.Id == "chiefdom");
        int ci = rc.IndexOfId("chiefdom");
        if (chiefdom.State == CivicState.Unresearched)
        {
            Assert.StartsWith("Locked - needs ", chiefdom.Line);
            foreach (int a in rc.Nodes[ci].PrerequisiteNodes) Assert.Contains(rc.Nodes[a].Name, chiefdom.Line);
        }
        foreach (int a in rc.Nodes[ci].PrerequisiteNodes) Grant(w, rc, Player, rc.Nodes[a].Id);
        GovernmentFormView open = Assert.Single(RegimeQuery.Forms(w, cfg, Player), f => f.Id == "chiefdom");
        Assert.Equal(CivicState.Available, open.State);
        Assert.Contains("not adopting it", open.Line);
        Grant(w, rc, Player, "chiefdom");
        GovernmentFormView done = Assert.Single(RegimeQuery.Forms(w, cfg, Player), f => f.Id == "chiefdom");
        Assert.Equal((CivicState.Researched, "Available - adoption in M8"), (done.State, done.Line));
        // researching a form changes no module and no label (no adoption in M6)
        Assert.Equal("chiefdom", RegimeQuery.Label(w, cfg, Player));
    }
}

/// <summary>
/// ADR-057 — THE SOURCE GUARD: the derived government label and the regime-module reading are NEVER READ BY ANY MECHANISM
/// (arch-FGH: "DERIVED … never read by any mechanism"; D-D: no active political effect in M6). No Sim.Core file other than
/// the query itself names RegimeQuery, and no Sim.Core file other than the query, the loader and the optionality
/// validator's home reads the regime section (<c>.Regime</c>) — so no system, policy or query can branch on a module or the
/// label. The presentation (Sim.Ui) is the only reader.
/// </summary>
public sealed class RegimeLabelSourceGuardTests
{
    [Fact]
    public void NoMechanismReadsTheRegimeLabelOrModules()
    {
        string root = RepoRoot();
        string core = Path.Combine(root, "Sim.Core");
        string[] files = Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        int scanned = 0;
        var section = new Regex(@"\.Regime\b(?!Config|Module|Query|Content)");
        foreach (string f in files)
        {
            string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            if (rel.Contains("/obj/", StringComparison.Ordinal) || rel.Contains("/bin/", StringComparison.Ordinal)) continue;
            scanned++;
            string text = File.ReadAllText(f);
            if (rel == "Sim.Core/State/RegimeQuery.cs") continue;
            Assert.False(text.Contains("RegimeQuery", StringComparison.Ordinal), rel + " names RegimeQuery: a mechanism must not read the derived label or modules");
            if (rel == "Sim.Core/Systems/SimConfig.cs") continue;   // the loader: shape and content validation only
            Assert.False(section.IsMatch(text), rel + " reads the regime section");
        }
        Assert.True(scanned > 100, "the guard scanned too few files to be meaningful");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
