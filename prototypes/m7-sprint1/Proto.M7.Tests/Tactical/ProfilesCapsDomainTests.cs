using Proto.M7.Spatial;
using Proto.M7.Tactical;
using static Proto.M7.Tactical.TacticalScenarios;

namespace Proto.M7.Tests.Tactical;

public class ProfilesCapsDomainTests
{
    [Fact]
    public void ProposedCapsAreTheDirectorRevisionTable()
    {
        var c = ProposedCaps.Proposed;
        // docs/m7-spec.md:395-402, verbatim values.
        Assert.Equal(50, c.CapOf(CapFamily.HeavyInfantry));
        Assert.Equal(60, c.CapOf(CapFamily.ArchersLightInfantry));
        Assert.Equal(5, c.CapOf(CapFamily.Tanks));
        Assert.Equal(3, c.CapOf(CapFamily.Helicopters));
        Assert.Equal(5, c.CapOf(CapFamily.Ships));
        Assert.Equal(1, c.CapOf(CapFamily.FixedWing));
        Assert.Equal(2, c.CapOf(CapFamily.Snipers));
        Assert.Equal(5, c.CapOf(CapFamily.Mortars));
        Assert.Contains("m7-spec.md:391-404", ProposedCaps.Citation);
    }

    [Fact]
    public void CapsTableMatchesTheRepoSpecWhenPresent()
    {
        string? spec = FindRepoFile("docs/m7-spec.md");
        if (spec is null) return;   // prototype copied outside the repo: nothing to compare against
        var lines = File.ReadAllLines(spec);
        Assert.Contains("| Heavy/aggregated infantry | 50 soldiers |", lines[394]);
        Assert.Contains("| Archers and lighter infantry | 60 soldiers |", lines[395]);
        Assert.Contains("| Mortars | 5 weapon systems/crews", lines[401]);
    }

    [Fact]
    public void ProfilesCarryTheCitedCharterValues()
    {
        var hi = UnitProfiles.HeavyInfantry;
        Assert.Equal(50, hi.Integrity);     // unit-catalogue…md:202 anchor
        Assert.Equal(50, hi.Melee);         // :202 anchor
        Assert.Equal(0, hi.Ranged);         // :664 "Ranged — until A7"
        Assert.Equal(CapFamily.HeavyInfantry, hi.CapFamily);
        var ar = UnitProfiles.Archers;
        Assert.Equal(2, ar.RangeCells);     // :223 short 1–2
        Assert.True(ar.IsRanged);
        Assert.Equal(CapFamily.ArchersLightInfantry, ar.CapFamily);
        foreach (var p in new[] { hi, ar }) Assert.Contains("unit-catalogue-and-progression.md", p.Citation);
        // band sanity: every stat inside the charter's 0..95 scale (unit-catalogue…md:209-213)
        foreach (var p in new[] { hi, ar })
            foreach (long v in new[] { p.Integrity, p.Melee, p.Ranged, p.Defence }) Assert.InRange(v, 0, 95);
    }

    [Fact]
    public void CharterAnchorLineSaysIntegrity50Melee50WhenPresent()
    {
        string? doc = FindRepoFile("docs/m7-plan/unit-catalogue-and-progression.md");
        if (doc is null) return;
        var lines = File.ReadAllLines(doc);
        Assert.Contains("Integrity 50 and Melee 50", lines[201]);
        Assert.Contains("short 1–2", lines[222]);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void HeavyInfantryAboveCapIsRefused(int n, bool ok)
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, n, UnitProfiles.HeavyInfantry, 10, 1);
        if (ok) _ = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        else Assert.Contains("cap 50", Assert.Throws<ArgumentException>(() => new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance)).Message);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void ArchersAboveCapAreRefused(int n, bool ok)
    {
        var s = Meeting(UnitProfiles.Archers, n, UnitProfiles.HeavyInfantry, 10, 1);
        if (ok) _ = new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance);
        else Assert.Throws<ArgumentException>(() => new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance));
    }

    [Fact]
    public void CapsAreConfigurableWithoutChangingTheProposal()
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, 40, UnitProfiles.HeavyInfantry, 10, 1)
            with { Caps = ProposedCaps.Proposed.WithOverride(CapFamily.HeavyInfantry, 30) };
        Assert.Throws<ArgumentException>(() => new TacticalBattle(s, PassiveController.Instance, PassiveController.Instance));
        Assert.Equal(50, ProposedCaps.Proposed.CapOf(CapFamily.HeavyInfantry));
    }

    // ---------------- Domain tag + explicit resolver choice (O-1)

    private sealed class StubAuto : IBattleResolver
    {
        public int Calls;
        public ResolverKind Kind => ResolverKind.Auto;
        public BattleResult Resolve(BattleSetup setup, IFormationController a, IFormationController b)
        {
            Calls++;
            return new BattleResult(ResolverKind.Auto, BattleEndReason.None, -1, -1, 0, 0, 0, 0, "", "", Array.Empty<FormationResult>());
        }
    }

    [Fact]
    public void ResolverDefaultsToTacticalAndAutoIsNeverImplicit()
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, 10, UnitProfiles.HeavyInfantry, 10, 3);
        var auto = new StubAuto();
        var r = BattleResolution.Resolve(s, new KnowledgeAi(), new KnowledgeAi(), auto: auto);   // resolver omitted
        Assert.Equal(ResolverKind.Tactical, r.Resolver);
        Assert.Equal(0, auto.Calls);
        Assert.Throws<InvalidOperationException>(() => BattleResolution.Resolve(s, new KnowledgeAi(), new KnowledgeAi(), ResolverKind.Auto));
        var ra = BattleResolution.Resolve(s, new KnowledgeAi(), new KnowledgeAi(), ResolverKind.Auto, auto);
        Assert.Equal(ResolverKind.Auto, ra.Resolver);
        Assert.Equal(1, auto.Calls);
    }

    [Fact]
    public void NavalDomainIsNeverSilentlyAutoResolved()
    {
        var s = Meeting(UnitProfiles.HeavyInfantry, 10, UnitProfiles.HeavyInfantry, 10, 3);
        var naval = s with { Formations = new[] { s.Formations[0] with { Domain = Domain.Naval }, s.Formations[1] } };
        var auto = new StubAuto();
        var ex = Assert.Throws<NotSupportedException>(() => BattleResolution.Resolve(naval, new KnowledgeAi(), new KnowledgeAi(), auto: auto));
        Assert.Contains("Naval", ex.Message);
        Assert.Equal(0, auto.Calls);   // default = Tactical even for naval; no fallback
    }

    [Fact]
    public void OrderAndBudgetInterfacesAreDomainAgnostic()
    {
        // The order request, the order log entry and the budget carry no domain field: the same values are valid for any
        // domain tag. The tag lives on the formation and flows into the result.
        foreach (var t in new[] { typeof(OrderRequest), typeof(OrderLogEntry), typeof(ActionBudget), typeof(EntityView) })
            foreach (var p in t.GetProperties()) Assert.NotEqual(typeof(Domain), p.PropertyType);
        Assert.Contains(typeof(FormationSpec).GetProperties(), p => p.PropertyType == typeof(Domain));
        Assert.Contains(typeof(FormationResult).GetProperties(), p => p.PropertyType == typeof(Domain));
        var r = new TacticalBattle(Meeting(UnitProfiles.HeavyInfantry, 10, UnitProfiles.HeavyInfantry, 10, 3), new KnowledgeAi(), new KnowledgeAi()).Run();
        Assert.All(r.Formations, f => Assert.Equal(Domain.Land, f.Domain));
    }

    internal static string? FindRepoFile(string rel)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(dir.FullName, rel);
            if (File.Exists(c) && Directory.Exists(Path.Combine(dir.FullName, "prototypes"))) return c;
            dir = dir.Parent;
        }
        return null;
    }
}
