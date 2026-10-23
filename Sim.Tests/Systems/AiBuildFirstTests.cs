using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Discovery;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-046 §7 (M6 review batch 3, 2026-10-23) — THE AI'S BUILD-FIRST PRIORITY (AiDiscoveryPolicy.DefersToBuilding, sim.json
/// discovery.aiBuildFirst). The survey's 10 stone competed with the first granary's 20; the AI now holds a Prospect back while
/// paying it would leave its purse unable to cover its essential construction (each idle settlement's next ordinary project,
/// counted only when it needs no crafted good), unless a concealed resource is strategically required (a known recipe needs a
/// concealable good it sees nowhere). Pinned: the exact reserve boundary, the strategic exception, that neither weakens the
/// bill or its affordability check, and that the AI still surveys and still finds concealed copper and tin.
/// </summary>
public class AiBuildFirstTests
{
    private static readonly SimConfig Cfg = TestConfigs.Sim();
    private static readonly PolityId Ai = new(2);

    private static readonly Lazy<WorldState> DevDuo = new(() =>
        WorldFounding.Found(TestConfigs.DevWorldgen() with { AiEmpires = 1 }, Cfg, 42));

    private static GoodId Good(string name) => new(Cfg.Goods!.IdOf(name));

    private static SettlementId[] Own(IReadOnlyWorldState w)
    {
        var own = new List<SettlementId>();
        for (int i = 0; i < w.Settlements.Count; i++)
            if (EmpireQuery.ControlsSettlement(w, Ai, w.Settlements[i].Id)) own.Add(w.Settlements[i].Id);
        return [.. own];
    }

    private static void Endow(WorldState w, SettlementId s, GoodId g, long amount)
    {
        if (amount == 0) return;
        for (int i = 0; i < w.GoodStocks.Count; i++)
        {
            if (w.GoodStocks[i].Settlement != s || w.GoodStocks[i].Good != g) continue;
            new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(i).Amount, ConservedQuantityIds.OfGood(g),
                amount > 0 ? ReasonIds.InitialEndowment : ReasonIds.Spoilage, Math.Abs(amount),
                amount > 0 ? FlowDirection.Source : FlowDirection.Sink, OverdrawPolicy.Throw);
            return;
        }
        throw new InvalidOperationException("no stock row");
    }

    private static long Unclaimed(IReadOnlyWorldState w, SettlementId s, GoodId g) =>
        ConstructionQuery.Unclaimed(w.GoodStocks, w.ConstructionQueue, w.ConstructionProgress, Cfg.Goods!,
            ConstructionQuery.Holders(w, Cfg, s), g);

    private static long Reserve(IReadOnlyWorldState w, SettlementId s, GoodId g) =>
        AiDiscoveryPolicy.EssentialReserve(w, Cfg, Cfg.Goods!, Cfg.Goods!.Projects!, Ai, ConstructionQuery.Holders(w, Cfg, s), g);

    /// <summary>The founding AI world with its purse set to exactly <paramref name="extra"/> stone beyond reserve + bill (and
    /// timber and grain ample), so stone alone decides.</summary>
    private static WorldState Purse(long extraStone)
    {
        WorldState w = DevDuo.Value.Clone();
        SettlementId s0 = Own(w)[0];
        GoodId stone = Good("stone"), timber = Good("timber");
        long bill = 0;
        foreach ((GoodId g, long q) in DiscoveryQuery.ProspectBill(Cfg, ProspectMethod.Survey)) if (g == stone) bill = q;
        Endow(w, s0, timber, Reserve(w, s0, timber) + 1000);
        Endow(w, s0, stone, Reserve(w, s0, stone) + bill + extraStone - Unclaimed(w, s0, stone));
        Assert.Equal(Reserve(w, s0, stone) + bill + extraStone, Unclaimed(w, s0, stone));
        return w;
    }

    [Fact]
    public void TheReserve_IsEachIdleSettlementsGranary_AndNotTheWorkshop()
    {
        WorldState w = DevDuo.Value.Clone();
        SettlementId[] own = Own(w);
        ConstructionProjectEntry granary = Cfg.Goods!.ProjectById(1)!;
        Assert.Equal("granary", granary.Name);
        GoodId stone = Good("stone"), tools = Good("tools");
        // every AI settlement idle, labour present, no granary: the reserve is one granary's stone per settlement
        Assert.Equal(own.Length * ConstructionQuery.InputOf(Cfg.Goods!, granary, stone), Reserve(w, own[0], stone));
        // the workshop (tools — a crafted good) is never reserved, so it never holds a survey back
        Assert.Equal(0, Reserve(w, own[0], tools));
    }

    [Fact]
    public void AConstrainedPurse_TheGranaryComesFirst_ExactBoundary()
    {
        // One stone short of reserve + bill: the survey waits; exactly reserve + bill: the survey goes; the bill itself is
        // affordable in both (the affordability check is unchanged — only the AI's priority holds it back).
        WorldState shortByOne = Purse(-1), exact = Purse(0);
        SettlementId s0 = Own(exact)[0];
        Assert.Equal(ProspectVerdict.Ok, DiscoveryQuery.CheckProspect(shortByOne, Cfg, Ai, s0));
        Assert.True(AiDiscoveryPolicy.DefersToBuilding(shortByOne, Cfg, Ai, s0, ProspectMethod.Survey));
        Assert.Null(AiDiscoveryPolicy.Prospect(shortByOne, Cfg, Ai));
        Assert.False(AiDiscoveryPolicy.DefersToBuilding(exact, Cfg, Ai, s0, ProspectMethod.Survey));
        Assert.Equal(0.0, AiDiscoveryPolicy.Prospect(exact, Cfg, Ai)!.Value.Amount);
        // the switch off (TestConfigs.PreAiBuildFirst): the e91d10b AI surveys from the short purse
        Assert.NotNull(AiDiscoveryPolicy.Prospect(shortByOne, TestConfigs.PreAiBuildFirst(Cfg), Ai));
    }

    [Fact]
    public void AQueuedGranary_ReleasesItsReserve_TheClaimAlreadyCountsIt()
    {
        WorldState w = Purse(-1);
        SettlementId[] own = Own(w);
        GoodId stone = Good("stone");
        long before = Reserve(w, own[0], stone);
        w.ConstructionQueue.Add(new ConstructionQueueRow(own[0], 0, 1));   // the granary queued at the first settlement
        Assert.Equal(before - 20, Reserve(w, own[0], stone));               // no longer a wish there…
        Assert.Equal(Unclaimed(Purse(-1), own[0], stone) - 20, Unclaimed(w, own[0], stone));   // …it is a claim instead
        Assert.True(AiDiscoveryPolicy.DefersToBuilding(w, Cfg, Ai, own[0], ProspectMethod.Survey));   // the same total need
    }

    [Fact]
    public void TheStrategicException_AKnownRecipesConcealedInputSeenNowhere_SurveysDespiteTheReserve()
    {
        WorldState w = Purse(-1);
        ResearchContent research = Cfg.Research!;
        Assert.Equal(-1, AiDiscoveryPolicy.StrategicNeed(w, Cfg, Ai));   // no recipe consuming an ore is known at founding
        // Learn bronze casting (its entity's cheapest route), and see no copper or tin anywhere the AI rules.
        var routes = new AiResearchPolicy.Routes(w, research, Ai);
        foreach (int i in routes.Entity(research.EntityIndexOf("recipe.bronze_casting")))
            w.ResearchCompleted.Add(new ResearchCompletedRow(Ai, research.Nodes[i].Key));
        GoodId copper = Good("copper-ore"), tin = Good("tin-ore");
        var keep = new List<DepositRow>();
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow d = w.Deposits[i];
            if ((d.Good == copper || d.Good == tin) && Own(w).Contains(d.Settlement)) continue;
            keep.Add(d);
        }
        w.Deposits.Clear();
        foreach (DepositRow d in keep) w.Deposits.Add(d);
        Assert.Equal(Math.Min(copper.Value, tin.Value), AiDiscoveryPolicy.StrategicNeed(w, Cfg, Ai));
        Assert.True(AiDiscoveryPolicy.DefersToBuilding(w, Cfg, Ai, Own(w)[0], ProspectMethod.Survey));   // the reserve still binds…
        Assert.NotNull(AiDiscoveryPolicy.Prospect(w, Cfg, Ai));                                         // …but the need overrides it

        // The exception never bypasses affordability: a purse short of the bill itself — no order.
        WorldState poor = w.Clone();
        SettlementId s0 = Own(poor)[0];
        Endow(poor, s0, Good("stone"), -Unclaimed(poor, s0, Good("stone")));
        Assert.Equal(ProspectVerdict.Unaffordable, DiscoveryQuery.CheckProspect(poor, Cfg, Ai, s0));
        Assert.Null(AiDiscoveryPolicy.Prospect(poor, Cfg, Ai));
    }

    [Fact]
    public void TheBillAndItsCheck_AreUnchanged()
    {
        Assert.Equal([(Good("grain"), 40L), (Good("timber"), 20L), (Good("stone"), 10L)], DiscoveryQuery.ProspectBill(Cfg, ProspectMethod.Survey));
        Assert.Equal(DiscoveryQuery.ProspectBill(Cfg, ProspectMethod.Assay), DiscoveryQuery.ProspectBill(TestConfigs.PreAiBuildFirst(Cfg), ProspectMethod.Assay));
        WorldState w = Purse(-1);
        foreach (SettlementId s in Own(w))   // the player's predicate reads the switch nowhere
            Assert.Equal(DiscoveryQuery.CheckProspect(w, TestConfigs.PreAiBuildFirst(Cfg), Ai, s), DiscoveryQuery.CheckProspect(w, Cfg, Ai, s));
    }

    /// <summary>The canonical AI world, 300 turns: the AI still surveys, and its surveys reveal concealed copper and tin
    /// (rows it could not see at founding read above 0 after their settlement is surveyed).</summary>
    [Fact]
    public void TheAi_StillSurveys_AndFindsConcealedCopperAndTin()
    {
        WorldgenConfig worldgen = TestConfigs.Worldgen();
        var log = new OrderLog();
        TurnExecutor exec;
        using (var era = Sim.Data.DataFiles.OpenEraPacing())
        using (var pipe = Sim.Data.DataFiles.OpenPipeline())
            exec = new TurnExecutor(EraTableLoader.Load(era), PipelineLoader.Load(pipe, SystemCatalog.All(Cfg, worldgen)), log);
        WorldState w = WorldFounding.Found(worldgen with { AiEmpires = 1 }, Cfg, 42);
        GoodId copper = Good("copper-ore"), tin = Good("tin-ore");
        var hidden = new List<DepositRow>();
        for (int i = 0; i < w.Deposits.Count; i++)
        {
            DepositRow d = w.Deposits[i];
            if ((d.Good == copper || d.Good == tin) && d.Abundance > 0.0 && EmpireQuery.ControlsSettlement(w, Ai, d.Settlement)
                && DepositConcealment.KnownAbundance(w, Cfg, d.Settlement, d.Good.Value) == 0.0) hidden.Add(d);
        }
        Assert.NotEmpty(hidden);
        for (int t = 0; t < 300; t++)
        {
            AiOrders.Append(log, w, Cfg, worldgen);
            w = exec.Step(w);
        }
        int surveys = 0;
        for (int i = 0; i < log.Count; i++) if (log[i].ActorId == Ai.Value && log[i].Kind == OrderKind.Prospect) surveys++;
        Assert.True(surveys > 0, "the AI never surveyed");
        int found = 0;
        foreach (DepositRow d in hidden)
            if (EmpireQuery.ControlsSettlement(w, Ai, d.Settlement) && DepositConcealment.KnownAbundance(w, Cfg, d.Settlement, d.Good.Value) > 0.0) found++;
        Assert.True(found > 0, $"none of the {hidden.Count} concealed copper/tin rows was found in 300 turns");
    }
}
