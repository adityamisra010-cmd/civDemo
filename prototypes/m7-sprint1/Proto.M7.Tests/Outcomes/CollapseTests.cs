using Proto.M7.Events;
using Proto.M7.Tactical;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using Xunit.Abstractions;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// DIR5 §2.9: battle-transient entities collapse into formation counts after battle (no veteran table); dead, wounded, captured and
/// deserted are explicit Ledger flows and the people of every formation are conserved exactly.
/// </summary>
public class CollapseTests
{
    private readonly ITestOutputHelper _out;
    public CollapseTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Apportion_OnePersonPerEntity_GivesEachCategoryExactlyItsCount()
        => Assert.Equal(new long[] { 30, 5, 5, 7, 3 }, BattleCollapse.Apportion(50, new long[] { 30, 5, 5, 7, 3 }));

    [Fact]
    public void Apportion_AnExactMultiple_ScalesEveryCategory()
        => Assert.Equal(new long[] { 1_800, 300, 300, 420, 180 }, BattleCollapse.Apportion(3_000, new long[] { 30, 5, 5, 7, 3 }));

    [Fact]
    public void Apportion_TieDense_EqualRemaindersGoToTheLowestCategory()
    {
        Assert.Equal(new long[] { 34, 33, 33, 0, 0 }, BattleCollapse.Apportion(100, new long[] { 1, 1, 1, 0, 0 }));
        Assert.Equal(new long[] { 1, 1, 0, 0, 0 }, BattleCollapse.Apportion(2, new long[] { 1, 1, 1, 1, 1 }));
        Assert.Equal(new long[] { 1, 1, 1, 1, 1, 1, 1, 0, 0, 0 }, BattleCollapse.Apportion(7, new long[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
        // largest remainder first: 10 × (1, 2, 4) / 7 = 1.43, 2.86, 5.71 → floors 1, 2, 5; the two left go to remainders 6/7 and 5/7
        Assert.Equal(new long[] { 1, 3, 0, 0, 6 }, BattleCollapse.Apportion(10, new long[] { 1, 2, 0, 0, 4 }));
        Assert.Equal(new long[] { 9, 0, 0, 0, 0 }, BattleCollapse.Apportion(9, new long[] { 0, 0, 0, 0, 0 }));   // no entity fielded: all stay fit
    }

    [Fact]
    public void Apportion_AlwaysSumsExactly_AndEachShareIsWithinOneOfItsQuota()
    {
        for (int c = 0; c < 2_000; c++)
        {
            var w = new long[5];
            for (int i = 0; i < 5; i++) w[i] = (long)Rng.CounterRng.NextBelow(0xC011_A95EUL, (uint)c, i, 0, 0, 0, 61);
            long persons = (long)Rng.CounterRng.NextBelow(0xC011_A95EUL, (uint)c, 9, 0, 0, 0, 1_000_000);
            var a = BattleCollapse.Apportion(persons, w);
            long n = w.Sum();
            Assert.Equal(persons, a.Sum());
            for (int i = 0; i < 5; i++)
            {
                if (n == 0) { Assert.Equal(i == 0 ? persons : 0, a[i]); continue; }
                Int128 lo = (Int128)persons * w[i] / n;
                Assert.True(a[i] == lo || a[i] == lo + 1, $"case {c}: share {a[i]} vs quota floor {lo}");
            }
        }
    }

    [Theory]
    [InlineData(true, 250L, 0L)]
    [InlineData(false, 250L, 75L)]
    [InlineData(false, 1_000L, 0L)]
    [InlineData(false, 0L, 100L)]
    public void Split_RoutedMenDesertOnlyOnTheSideThatLostTheField_InProportionToItsLostMorale(bool held, long morale, long deserted)
    {
        var s = BattleCollapse.Split(100, new CollapseInput(0, 0, new long[] { 0, 0, 1, 0, 0 }, morale, 1_000), held);
        Assert.Equal((100L, deserted, 100 - deserted), (s.Persons, s.Deserted, s.RoutedReturned));
        Assert.True(s.Conserved);
    }

    private static (TacticalBattle Battle, BattleSetup Setup) Fought(ulong seed)
    {
        var setup = TacticalScenarios.CombinedArms(seed);
        var b = new TacticalBattle(setup, new KnowledgeAi(), new KnowledgeAi());
        b.Run();
        return (b, setup);
    }

    private static readonly (long, int)[] Map = { (1, 0), (2, 1), (3, 2), (4, 3) };

    private static OutcomeFormation[] Forms(long scale) => new[]
    {
        new OutcomeFormation(0, 0, 50 * scale), new OutcomeFormation(1, 0, 60 * scale), new OutcomeFormation(2, 1, 50 * scale), new OutcomeFormation(3, 1, 60 * scale),
    };

    private static Built Collapsed(TacticalBattle battle, BattleSetup setup, long scale)
    {
        var r = battle.Result!;
        var inputs = BattleCollapse.FromTactical(battle, setup, Map);
        var fb = new FieldBattle(0, B(2), 0, 1, BattleMechanism.Tactical, (byte)r.Reason, r.HoldsField, inputs);
        var b = Build((s, w) => w.ScheduleBattle(s, B(2) + Day, fb), formations: Forms(scale));
        b.R.RunUntil(B(3));
        return b;
    }

    [Theory]
    [InlineData(42UL, 1L)]
    [InlineData(42UL, 100L)]
    [InlineData(7UL, 1L)]
    public void ATacticalBattle_CollapsesIntoFormationCounts_WithExactFlows(ulong seed, long scale)
    {
        var (battle, setup) = Fought(seed);
        var r = battle.Result!;
        var b = Collapsed(battle, setup, scale);
        var w = b.W;
        // oracle straight from the engine's end state: per formation, category counts of its entities
        var views = battle.Entities();
        long killedAll = 0, woundedAll = 0, desertedAll = 0;
        foreach (var (tid, camp) in Map)
        {
            var spec = setup.Formations.Single(f => f.Id == tid);
            var mine = views.Where(v => v.FormationId == tid).ToArray();
            long dead = mine.Count(v => v.State == EntityState.Dead), sur = mine.Count(v => v.State == EntityState.Surrendered);
            long wounded = mine.Count(v => v.State != EntityState.Dead && v.State != EntityState.Surrendered && v.Hp < spec.Profile.MaxHp);
            long routed = mine.Count(v => v.State != EntityState.Dead && v.State != EntityState.Surrendered && v.Hp == spec.Profile.MaxHp && v.RoutedEscape);
            var fr = r.Formations.Single(f => f.FormationId == tid);
            Assert.Equal((fr.Killed, fr.Surrendered), (dead, sur));            // the engine's own aggregation agrees
            long morale = fr.MoraleEnd;
            long des = r.HoldsField == spec.Side ? 0 : routed * scale * (1_000 - morale) / 1_000;
            long persons = mine.Length * scale;
            Assert.Equal(persons - dead * scale - wounded * scale - sur * scale - des, w.Ledger.Balance(w.FormationAccount(camp)));
            Assert.Equal(wounded * scale, w.Ledger.Balance(w.WoundedAccount(camp)));
            killedAll += dead * scale; woundedAll += wounded * scale; desertedAll += des;
            _out.WriteLine($"seed {seed} scale {scale} formation {tid}: {mine.Length} entities, dead {dead}, wounded {wounded}, routed {routed}, surrendered {sur}, morale {morale}, deserted {des}");
        }
        Assert.Equal(killedAll, w.Ledger.Balance(w.KilledSink));
        Assert.Equal(desertedAll, w.Ledger.Balance(w.DesertedSink));
        // the record: casualties by side and cause sum to the flows
        long Cause(CasualtyCause c) => w.Casualties.Where(x => x.Cause == (byte)c).Sum(x => x.Count);
        Assert.Equal((killedAll, woundedAll, desertedAll), (Cause(CasualtyCause.Killed), Cause(CasualtyCause.Wounded), Cause(CasualtyCause.Deserted)));
        Assert.Equal(w.Ledger.Balance(w.CustodyOf(0, 1)) + w.Ledger.Balance(w.CustodyOf(1, 0)), Cause(CasualtyCause.Captured));
        var row = Assert.Single(w.Battles);
        Assert.Equal(((byte)BattleKind.Field, (byte)r.Reason, r.HoldsField, B(2) + Day), (row.Kind, row.EndCode, row.WinnerSide, row.EndMs));
        Assert.Equal(new[] { (0, 2, 110 * scale), (1, 2, 110 * scale) }, w.BattleSides.Select(x => ((int)x.Side, x.Formations, x.PersonnelStart)).ToArray());
    }

    [Fact]
    public void AMultiDomainBattle_VesselsAndAircraftCollapseFromThePersonnelLedger_Exactly()
    {
        // lane N's amphibious combined-arms battle: warships, transports with troops aboard, aircraft, guns, snipers, riflemen
        var setup = MultiDomainScenarios.Amphibious(42);
        var battle = new TacticalBattle(setup, MultiDomainScenarios.AmphibiousPlan(), MultiDomainScenarios.AmphibiousDefence());
        battle.Run();
        var r = battle.Result!;
        var p = r.Personnel!;
        var specs = setup.Formations.OrderBy(f => f.Id).ToArray();
        var views = battle.Entities();
        long Persons(FormationSpec f) => f.Domain == Domain.Land ? views.Count(v => v.FormationId == f.Id) : p.Lines.Where(l => l.FormationId == f.Id).Sum(l => l.Count);
        var map = specs.Select((f, i) => (f.Id, i)).ToArray();
        var forms = specs.Select((f, i) => new OutcomeFormation(i, f.Side, Persons(f))).ToArray();
        var fb = new FieldBattle(0, B(2), 0, 1, BattleMechanism.Tactical, (byte)r.Reason, r.HoldsField, BattleCollapse.FromTactical(battle, setup, map));
        var b = Build((s, w) => w.ScheduleBattle(s, B(2) + Day, fb), formations: forms);
        var before = Totals(b.W.Ledger);
        b.R.RunUntil(B(3));
        Assert.Equal(before, Totals(b.W.Ledger));
        var crews = specs.Select((f, i) => (f, i)).Where(x => x.f.Domain != Domain.Land).ToArray();
        Assert.Contains(crews, x => x.f.Domain == Domain.Naval);
        Assert.Contains(crews, x => x.f.Domain == Domain.Air);
        long lost = 0;
        foreach (var (f, i) in crews)
        {
            long dead = p.Count(f.Id, PersonnelPlace.Killed) + p.Count(f.Id, PersonnelPlace.LostWithVessel), captured = p.Count(f.Id, PersonnelPlace.Captured);
            Assert.Equal(Persons(f) - dead - captured, b.W.Ledger.Balance(b.W.FormationAccount(i)));   // no per-person wounds or routs aboard a hull
            Assert.Equal(0, b.W.Ledger.Balance(b.W.WoundedAccount(i)));
            lost += dead;
            _out.WriteLine($"formation {f.Id} {f.Domain}: persons {Persons(f)}, dead {dead}, captured {captured}");
        }
        Assert.True(lost > 0, "the battle sank or downed something");
    }

    [Fact]
    public void TheCollapse_ConservesEveryPerson_AndIsDeterministic()
    {
        var (b1, s1) = Fought(42);
        var (b2, s2) = Fought(42);
        var x = Collapsed(b1, s1, 37);    // 37 persons per entity: remainders everywhere
        var y = Collapsed(b2, s2, 37);
        Assert.Equal(x.W.StateHashHex(), y.W.StateHashHex());
        var t = Build(formations: Forms(37));
        Assert.Equal(Totals(t.W.Ledger), Totals(x.W.Ledger));
    }

    [Fact]
    public void NoVeteranTable_NoRecordRowIsKeyedByABattleEntity()
    {
        var rowTypes = typeof(WarOutcomeWorld).Assembly.GetTypes().Where(t => t.Namespace == "Proto.M7.War.Outcomes" && t.Name.EndsWith("Row")).ToArray();
        Assert.True(rowTypes.Length >= 11);
        foreach (var t in rowTypes)
            foreach (var p in t.GetProperties())
                Assert.DoesNotContain("Entit", p.Name);
    }

    [Fact]
    public void ScheduleBattle_RefusesAFormationOnTheWrongSide()
    {
        var b = Build();
        var bad = new FieldBattle(0, B(1), 0, 1, BattleMechanism.Tactical, 1, 0, new[] { new CollapseInput(2, 0, new long[] { 1, 0, 0, 0, 0 }, 1_000, 1_000) });
        Assert.Throws<ArgumentException>(() => b.W.ScheduleBattle(b.S, B(1) + Day, bad));
    }
}
