using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.ClassMobility;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-058 §B (D-066 track KNOW) — EUREKA EVALUABILITY: seven Eurekas whose circumstance existing computed state already
/// holds are made machine-evaluable over three new quantity families of the research dialect (EurekaQuantities):
/// deposit_&lt;good&gt; (the deposit as its holder KNOWS it — concealed ores count only once prospected), site_river and
/// site_coast (the terrain of the settlement's own knowledge-lattice node). Each newly evaluable Eureka is shown to hold on
/// a rig that has its circumstance and NOT on rigs that lack it; the engine fires one on a rig; the counts of the shipped
/// content are pinned (21 evaluable, 65 future-system), and the register docs/m6-plan/eureka-evaluability-register.md
/// lists every one of the 86 exactly once with the status the content has.
/// </summary>
public class EurekaEvaluabilityTests
{
    private static readonly ResearchContent Canon = TestConfigs.Research();
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly PolityId P1 = new(Player);

    private static int Good(string name) => Cfg.Goods!.IdOf(name);

    private static Predicate Condition(string node)
    {
        ResearchEureka e = Canon.Nodes[Canon.IndexOfId(node)].Eurekas[0];
        Assert.True(e.EvaluableNow, node);
        return e.Condition!;
    }

    private static bool Holds(IReadOnlyWorldState w, string node, SimConfig? cfg = null) =>
        ResearchQuery.EurekaHolds(w, Canon, P1, Condition(node), ResearchQuery.CompletedMask(w, Canon, P1), cfg ?? Cfg);

    /// <summary>Polity 1 controls settlement 0 (and polity 2 settlement 1, when asked for).</summary>
    private static WorldState Base(bool rival = false) =>
        rival ? World([Player, 2], [Player, 2], 1_000) : World([Player], [Player], 1_000);

    private static WorldState Deposit(WorldState w, int settlement, string good, double abundance = 0.5)
    {
        w.Deposits.Add(new DepositRow(new SettlementId(settlement), new GoodId(Good(good)), abundance));
        return w;
    }

    /// <summary>Settlement 0 at cell (1, 1) of the 32 px all-land rig terrain — its lattice node is the block x, y in 0..3 —
    /// with a river cell and/or an open-water cell inside that block, or open water far away (outside the block).</summary>
    private static WorldState Sited(WorldState w, bool river = false, bool coast = false, bool farWater = false)
    {
        w.Terrain = DiffusionExposureTests.LandTerrain(
            water: (x, y) => (coast && x == 3 && y == 0) || (farWater && x == 20 && y == 20) ? 1.0 : 0.0,
            rivers: (x, y) => river && x == 2 && y == 2 ? 1.0 : 0.0);
        w.Settlements[0] = w.Settlements[0] with { SiteCell = 1 * 32 + 1 };
        return w;
    }

    // ------------------------------------------------------------------ each newly evaluable Eureka: holds iff its circumstance

    [Fact]
    public void FishingHook_HoldsWithAFishDeposit_NotWithoutOrAtARivalsSettlement()
    {
        Assert.Equal("deposit_fish > 0", Condition("fishing_hook").Source);
        Assert.True(Holds(Deposit(Base(), 0, "fish"), "fishing_hook"));
        Assert.False(Holds(Base(), "fishing_hook"));
        Assert.False(Holds(Deposit(Base(), 0, "fish", 0.0), "fishing_hook"));
        Assert.False(Holds(Deposit(Base(rival: true), 1, "fish"), "fishing_hook"));   // the rival's waters are not ours
    }

    [Fact]
    public void Coke_HoldsWithAKnownCoalSeam_AConcealedOneOnlyOnceProspected()
    {
        Assert.Equal("deposit_coal > 0", Condition("coke").Source);
        (int concealed, int open) = ConcealmentSplit("coal");
        Assert.True(Holds(Only(open, "coal"), "coke"));
        WorldState hidden = Only(concealed, "coal");
        Assert.False(Holds(hidden, "coke"));                              // concealed and unsurveyed: no leak
        Assert.True(Holds(hidden, "coke", Cfg with { Discovery = null }));  // concealment off: the raw deposit
        hidden.SettlementSurveys.Add(new SettlementSurveyRow(P1, new SettlementId(concealed), 0));
        Assert.True(Holds(hidden, "coke"));                               // prospected: known
        Assert.False(Holds(Base(), "coke"));
    }

    [Fact]
    public void Gunpowder_HoldsWithSaltpetreHeldOrAKnownDeposit_NotOtherwise()
    {
        Assert.Equal("deposit_saltpetre > 0 OR stock_saltpetre > 0", Condition("gunpowder").Source);
        Assert.True(Holds(Stock(Base(), 0, Good("saltpetre"), 3), "gunpowder"));
        (int concealed, int open) = ConcealmentSplit("saltpetre");
        Assert.True(Holds(Only(open, "saltpetre"), "gunpowder"));
        Assert.False(Holds(Only(concealed, "saltpetre"), "gunpowder"));
        Assert.False(Holds(Stock(Base(), 0, Good("sulphur"), 3), "gunpowder"));
        Assert.False(Holds(Base(), "gunpowder"));
    }

    [Fact]
    public void SulphuricAcid_HoldsWithSulphurAndSaltpetreHeld_NotWithEitherAlone()
    {
        Assert.Equal("stock_sulphur > 0 AND stock_saltpetre > 0", Condition("sulphuric_acid").Source);
        Assert.True(Holds(Stock(Stock(Base(), 0, Good("sulphur"), 2), 0, Good("saltpetre"), 2), "sulphuric_acid"));
        Assert.False(Holds(Stock(Base(), 0, Good("sulphur"), 2), "sulphuric_acid"));
        Assert.False(Holds(Stock(Base(), 0, Good("saltpetre"), 2), "sulphuric_acid"));
    }

    [Fact]
    public void Dugout_HoldsWithTimberBesideARiverOrCoast_NotWithEitherAlone()
    {
        Assert.Equal("deposit_timber > 0 AND (site_river > 0 OR site_coast > 0)", Condition("dugout").Source);
        Assert.True(Holds(Sited(Deposit(Base(), 0, "timber"), river: true), "dugout"));
        Assert.True(Holds(Sited(Deposit(Base(), 0, "timber"), coast: true), "dugout"));
        Assert.False(Holds(Sited(Deposit(Base(), 0, "timber")), "dugout"));
        Assert.False(Holds(Sited(Deposit(Base(), 0, "timber"), farWater: true), "dugout"));
        Assert.False(Holds(Sited(Base(), river: true, coast: true), "dugout"));
        Assert.False(Holds(Deposit(Base(), 0, "timber"), "dugout"));   // no terrain: no site fact
    }

    [Fact]
    public void SaltExtraction_HoldsOnACoast_NotBesideARiverAlone()
    {
        Assert.Equal("site_coast > 0", Condition("salt_extraction").Source);
        Assert.True(Holds(Sited(Base(), coast: true), "salt_extraction"));
        Assert.False(Holds(Sited(Base(), river: true), "salt_extraction"));   // a river cell is not open water
        Assert.False(Holds(Sited(Base(), farWater: true), "salt_extraction"));
    }

    [Fact]
    public void SailSquare_HoldsBesideARiverOrCoast_NotInland()
    {
        Assert.Equal("site_river > 0 OR site_coast > 0", Condition("sail_square").Source);
        Assert.True(Holds(Sited(Base(), river: true), "sail_square"));
        Assert.True(Holds(Sited(Base(), coast: true), "sail_square"));
        Assert.False(Holds(Sited(Base()), "sail_square"));
        Assert.False(Holds(Sited(Base(), farWater: true), "sail_square"));
    }

    // ------------------------------------------------------------------ the engine fires them

    /// <summary>The engine (ResearchSystem, through SystemCatalog with the step's config) fires a deposit and a site Eureka
    /// on a rig that holds the circumstance — once, 0.40 × BaseCost — and fires nothing on the rig that lacks it.</summary>
    [Fact]
    public void Engine_FiresDepositAndSiteEurekas_OnTheRigThatHoldsThem_Only()
    {
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "x", "a", Eurekas: [new Eu("fish", "deposit_fish > 0")]));
        spec.Technologies.Add(new Node(13, "y", "a", Eurekas: [new Eu("coast", "site_coast > 0")]));
        ResearchContent content = spec.Load();
        WorldState Make(bool fish, bool coast)
        {
            WorldState w = WithCompleted(PlayerWorld(), 1);
            if (fish) w.Deposits.Add(new DepositRow(new SettlementId(0), new GoodId(Good("fish")), 0.5));
            return Sited(w, coast: coast);
        }
        WorldState yes = Executor(content).Step(Make(fish: true, coast: true));
        WorldState no = Executor(content).Step(Make(fish: false, coast: false));
        foreach (int k in new[] { 12, 13 })
        {
            Assert.True(ResearchQuery.EurekaFired(yes, P1, Key(k), 0));
            Assert.Equal(1000.0, ResearchQuery.CreditedBySource(yes, P1, Key(k), AccelerationSource.Eureka));
            Assert.False(ResearchQuery.EurekaFired(no, P1, Key(k), 0));
            Assert.Equal(0.0, Progress(no, k));
        }
    }

    /// <summary>A good its goods.json packet removed keeps its quantity names, bound to 0 — the packet-off config loads the
    /// same research.json (the sim.json concealableGoods precedent), and its circumstance cannot hold.</summary>
    [Fact]
    public void PacketOffGoods_StillParse_AndReadZero()
    {
        GoodsConfig off = Cfg.Goods!.WithoutPacket("ec3");
        Assert.Contains("saltpetre", off.DisabledGoodNames);
        string json = TestConfigs.ResearchJson();
        ResearchContent content = ResearchContentLoader.Load(json, off);
        Predicate gunpowder = content.Nodes[content.IndexOfId("gunpowder")].Eurekas[0].Condition!;
        WorldState w = Base();
        Assert.False(ResearchQuery.EurekaHolds(w, content, P1, gunpowder, ResearchQuery.CompletedMask(w, content, P1), Cfg));
    }

    // ------------------------------------------------------------------ the shipped content and the register

    [Fact]
    public void ShippedEurekas_86_21Evaluable_65FutureSystem_ByOwningSystem()
    {
        int total = 0, evaluable = 0;
        var future = new SortedDictionary<string, int>(StringComparer.Ordinal);   // test-side tally, not sim logic
        foreach (ResearchNode n in Canon.Nodes)
            foreach (ResearchEureka e in n.Eurekas)
            {
                total++;
                if (e.EvaluableNow) { evaluable++; Assert.Equal("machine-evaluable", e.Class); }
                else
                {
                    Assert.Equal("future-system:" + e.System, e.Class);
                    future[e.System] = future.GetValueOrDefault(e.System) + 1;
                }
            }
        Assert.Equal(86, total);
        Assert.Equal(21, evaluable);   // 14 before ADR-058 + 6 resources/terrain + 1 goods state
        Assert.Equal(
            "construction state=1; goods state=1; institutions=8; resources/terrain=30; world events=24; world state=1",
            string.Join("; ", future.Select(kv => kv.Key + "=" + kv.Value)));
    }

    /// <summary>docs/m6-plan/eureka-evaluability-register.md lists every shipped Eureka exactly once (node id, index), with
    /// the status the content has: "evaluable" rows carry the content's condition verbatim; "future" rows name an owner.</summary>
    [Fact]
    public void Register_ListsEveryEurekaExactlyOnce_WithTheContentsStatus()
    {
        string path = Path.Combine(RepoPaths.Root(), "docs", "m6-plan", "eureka-evaluability-register.md");
        var rows = new List<string[]>();
        bool register = false;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal)) register = line.StartsWith("## 2. Register", StringComparison.Ordinal);
            if (!register || !line.StartsWith("| `", StringComparison.Ordinal)) continue;
            string[] cells = line.Split('|');
            rows.Add([.. cells.Skip(1).Take(cells.Length - 2).Select(c => c.Trim())]);
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] r in rows)
        {
            string node = r[0].Trim('`');
            int index = int.Parse(r[1], System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(seen.Add(node + "#" + index), $"{node}#{index} listed twice");
            ResearchEureka e = Canon.Nodes[Canon.IndexOfId(node)].Eurekas[index];
            string status = r[3];
            if (e.EvaluableNow)
            {
                Assert.StartsWith("evaluable", status, StringComparison.Ordinal);
                Assert.Equal("`" + e.Condition!.Source + "`", r[4]);
            }
            else
            {
                Assert.StartsWith("future", status, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(r[4]), $"{node}: a future Eureka names its owning system and milestone");
            }
        }
        int total = 0;
        foreach (ResearchNode n in Canon.Nodes) total += n.Eurekas.Count;
        Assert.Equal(total, seen.Count);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Two of polity 1's settlements 0..7 on which a lone deposit of <paramref name="good"/> is, respectively,
    /// concealed and not (the classification is per settlement — its own rows and its low-discrepancy offset).</summary>
    private static (int Concealed, int Open) ConcealmentSplit(string good)
    {
        WorldState w = World([Player], [Player, Player, Player, Player, Player, Player, Player, Player], 100);
        int concealed = -1, open = -1;
        for (int s = 0; s < 8; s++)
        {
            Deposit(w, s, good);
            if (DepositConcealment.IsConcealed(w, Cfg, new SettlementId(s), new GoodId(Good(good)))) { if (concealed < 0) concealed = s; }
            else if (open < 0) open = s;
        }
        Assert.True(concealed >= 0 && open >= 0, $"{good}: settlements 0..7 hold no concealed/open split");
        return (concealed, open);
    }

    /// <summary>The 8-settlement world with one deposit of <paramref name="good"/>, at <paramref name="settlement"/>.</summary>
    private static WorldState Only(int settlement, string good) =>
        Deposit(World([Player], [Player, Player, Player, Player, Player, Player, Player, Player], 100), settlement, good);
}
