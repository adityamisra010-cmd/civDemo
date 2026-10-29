using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-058 (D-066 A7) — CONTACT-CARRIED FOREIGN EXPOSURE: DiffusionSystem, the one writer of the ResearchExposures seam.
///
/// The rig: the standard research rig (every node 2,500 RP, the pool ceiling 0.40 × 2,500 = 1,000) with
/// tuning.foreignExposure {enabled, ratePerYear 0.01} at dt 10 — so a step of contact offers 0.01 × 10 × 2,500 = 250 RP on
/// every node the partner knows and the receiver may research now. Polity 1 (the player) controls settlement 0 and knows
/// only <c>a</c>; polity 2 (an AI) controls settlement 1 and knows <c>a b c w</c>; one grain flow joins the two settlements
/// (TradeFlows carries over from PREV each step: no trade system runs here). Polity 1 may research b, d, w and law; the
/// partner knows b and w — c is known too but polity 1 lacks its prerequisite b, so it is never offered.
/// </summary>
public class DiffusionExposureTests
{
    private static readonly PolityId P1 = new(Player);
    private static readonly PolityId P2 = new(2);
    private const int B = 2, C = 3, D = 4, W = 11;

    private static ResearchContent Content(bool enabled = true, double rate = 0.01, bool absent = false)
    {
        Spec spec = Standard();
        if (!absent) spec.ForeignExposure = (enabled, rate);
        return spec.Load();
    }

    private static readonly ResearchContent On = Content();

    private static SimConfig Cfg(ResearchContent content) => TestConfigs.Sim() with { Research = content };

    /// <summary>Diffusion and research only (both read PREV, so their order is legibility).</summary>
    private static TurnExecutor Executor(ResearchContent content) =>
        new(FlatEra(10.0), [SystemCatalog.Diffusion(Cfg(content)), SystemCatalog.Research(Cfg(content))]);

    /// <summary>The rig world (see the type's remarks). <paramref name="flow"/> false: no trade, so no contact.</summary>
    private static WorldState Rig(bool flow = true, int[]? partnerKnows = null)
    {
        WorldState w = World([Player, 2], [Player, 2], 10_000);
        w.ResearchCompleted.Add(new ResearchCompletedRow(P1, Key(1)));
        foreach (int k in partnerKnows ?? [1, B, C, W]) w.ResearchCompleted.Add(new ResearchCompletedRow(P2, Key(k)));
        if (flow) w.TradeFlows.Add(new TradeFlowRow(new SettlementId(0), new SettlementId(1), new GoodId(1), 5));
        return w;
    }

    private static double Offered(IReadOnlyWorldState w, PolityId p, int key) => ResearchQuery.ExposureOffered(w, p, Key(key));

    private static double Credited(IReadOnlyWorldState w, PolityId p, int key, AccelerationSource s = AccelerationSource.ForeignExposure) =>
        ResearchQuery.CreditedBySource(w, p, Key(key), s);

    // ------------------------------------------------------------------ the credit, turn by turn

    /// <summary>THE DETERMINISTIC CREDIT AND THE TURN-EXACT PIN: the offer of step t→t+1 is in state t+1 and is paid in step
    /// t+1→t+2 (research reads the seam from PREV) — 250 a step, capped at the ceiling of 1,000, never lowered.</summary>
    [Fact]
    public void Contact_OffersRateTimesDtTimesBaseCost_PaidOneStepLater_UpToTheCeiling()
    {
        TurnExecutor ex = Executor(On);
        WorldState w = Rig();
        double[] offered = [250.0, 500.0, 750.0, 1000.0, 1000.0, 1000.0];
        double[] paid = [0.0, 250.0, 500.0, 750.0, 1000.0, 1000.0];
        for (int t = 0; t < offered.Length; t++)
        {
            w = ex.Step(w);
            foreach (int k in new[] { B, W })
            {
                Assert.Equal(offered[t], Offered(w, P1, k));
                Assert.Equal(paid[t], Credited(w, P1, k));
                Assert.Equal(paid[t], Progress(w, k));   // no target: the progress row is the credit alone
            }
            // c is known by the partner but not researchable by polity 1 (it lacks b); d and law are researchable but the
            // partner does not know them; the partner learns nothing from polity 1 (it knows all polity 1 knows).
            foreach (int k in new[] { C, D, 1001 }) Assert.Equal(0.0, Offered(w, P1, k));
            Assert.Equal(0, RowsOf(w, P2));
        }
        Assert.Equal(2, RowsOf(w, P1));   // one row per (polity, node), updated in place
        Assert.False(Done(w, B));          // a bounded advance, never a completion (1,000 of 2,500)
    }

    [Fact]
    public void Offered_IsMonotone_StepByStep_AndAHandWrittenOfferAboveTheCapIsNeverLowered()
    {
        TurnExecutor ex = Executor(On);
        WorldState w = Rig();
        w.ResearchExposures.Add(new ResearchExposureRow(P1, Key(W), 1500.0));   // above the 1,000 cap (a hand-written seam row)
        double before = Offered(w, P1, B);
        for (int t = 0; t < 8; t++)
        {
            WorldState next = ex.Step(w);
            for (int r = 0; r < w.ResearchExposures.Count; r++)
            {
                ResearchExposureRow row = w.ResearchExposures[r];
                Assert.True(Offered(next, row.Polity, row.Node.Value) >= Offered(w, row.Polity, row.Node.Value));
            }
            Assert.True(Offered(next, P1, B) >= before);
            before = Offered(next, P1, B);
            w = next;
        }
        Assert.Equal(1000.0, Offered(w, P1, B));
        Assert.Equal(1500.0, Offered(w, P1, W));
        Assert.Equal(1000.0, Credited(w, P1, W));   // the pool still pays only up to the ceiling
    }

    /// <summary>THE CEILING IS ONE POOL WITH EUREKA: w's timber Eureka (0.20 × 2,500 = 500) fires in step 1; exposure is
    /// then paid only into the pool's remaining 500 — Eureka + exposure = 1,000 = the ceiling exactly, never above.</summary>
    [Fact]
    public void Ceiling_IsSharedWithEureka_TheirSumNeverExceedsIt()
    {
        TurnExecutor ex = Executor(On);
        WorldState w = Stock(Rig(), 0, 4, 10);   // timber in polity 1's settlement: w's first Eureka holds
        for (int t = 0; t < 8; t++)
        {
            w = ex.Step(w);
            double eureka = Credited(w, P1, W, AccelerationSource.Eureka);
            double exposure = Credited(w, P1, W);
            Assert.True(eureka + exposure <= 1000.0, $"step {t + 1}: {eureka} + {exposure}");
        }
        Assert.Equal(500.0, Credited(w, P1, W, AccelerationSource.Eureka));
        Assert.Equal(500.0, Credited(w, P1, W));
        Assert.Equal(1000.0, Offered(w, P1, W));   // offered in full; the pool paid only its headroom
        Assert.Equal(1000.0, ResearchQuery.AccelerationPoolOf(w, On, P1, Key(W)).Credited);
        Assert.Equal(0.0, ResearchQuery.AccelerationPoolOf(w, On, P1, Key(W)).Headroom);
    }

    // ------------------------------------------------------------------ who is exposed

    [Fact]
    public void NoTrade_NoExposure_AndDomesticTradeCarriesNothing()
    {
        Assert.Equal(0, Run(Executor(On), Rig(flow: false), 4).ResearchExposures.Count);

        // Both settlements polity 1's: the flow is domestic, whatever settlement 1's own (dormant) knowledge holds.
        WorldState w = World([Player], [Player, Player], 10_000);
        w.ResearchCompleted.Add(new ResearchCompletedRow(P1, Key(1)));
        w.ResearchCompleted.Add(new ResearchCompletedRow(SettlementKnowledge.LocalHolder(new SettlementId(1)), Key(B)));
        w.TradeFlows.Add(new TradeFlowRow(new SettlementId(0), new SettlementId(1), new GoodId(1), 5));
        Assert.Equal(0, Run(Executor(On), w, 4).ResearchExposures.Count);
    }

    /// <summary>A city state is a CARRIER: an Empire trading with an uncontrolled settlement is offered what that
    /// settlement's own knowledge holds. City states are not receivers in M6.</summary>
    [Fact]
    public void CityStatePartner_CarriesItsOwnKnowledge_ButReceivesNone()
    {
        WorldState w = World([Player], [Player], 10_000);
        w.Settlements.Add(new SettlementRow(new SettlementId(1), SiteCell: 1, FoundedTurn: 0));   // no control row: a city state
        w.ResearchCompleted.Add(new ResearchCompletedRow(P1, Key(1)));
        PolityId local = SettlementKnowledge.LocalHolder(new SettlementId(1));
        foreach (int k in new[] { 1, B }) w.ResearchCompleted.Add(new ResearchCompletedRow(local, Key(k)));
        w.TradeFlows.Add(new TradeFlowRow(new SettlementId(1), new SettlementId(0), new GoodId(1), 5));
        WorldState w1 = Executor(On).Step(w);
        Assert.Equal(250.0, Offered(w1, P1, B));
        Assert.Equal(0, RowsOf(w1, local));
        Assert.Equal(1, w1.ResearchExposures.Count);
    }

    /// <summary>THE FOG-LEAK TEST: on a terrain world with the fog on, polity 1's seed disc does not reach settlement 1 and it
    /// has no sighting of it, so a trade flow with it carries NOTHING — and what that unknown partner knows cannot change
    /// polity 1's world (metamorphic: two partners with different knowledge, byte-identical receivers). Once polity 1 has
    /// sighted the settlement (rule E5), the same flow carries.</summary>
    [Fact]
    public void FogLeak_NoExposureFromAnUnsightedPartner_WhateverItKnows_ThenExposureOnceSighted()
    {
        Assert.True(DiscoveryQuery.FogOn(Cfg(On)));
        WorldState Fogged(int[] partnerKnows, bool sighted)
        {
            WorldState w = Rig(partnerKnows: partnerKnows);
            w.Terrain = LandTerrain();
            w.Settlements[0] = w.Settlements[0] with { SiteCell = 1 * Size + 1 };
            w.Settlements[1] = w.Settlements[1] with { SiteCell = 30 * Size + 30 };
            if (sighted) w.SettlementSightings.Add(new SettlementSightingRow(P1, new SettlementId(1), 0));
            return w;
        }
        Assert.False(PolityKnowledge.Of(Fogged([1, B], false), P1, Cfg(On)).KnowsSettlement(new SettlementId(1)));
        Assert.True(PolityKnowledge.Of(Fogged([1, B], true), P1, Cfg(On)).KnowsSettlement(new SettlementId(1)));

        WorldState little = Run(Executor(On), Fogged([1, B], false), 3);
        WorldState much = Run(Executor(On), Fogged([1, B, C, W], false), 3);
        Assert.Equal(0, little.ResearchExposures.Count);
        Assert.Equal(0, much.ResearchExposures.Count);
        Assert.Equal(Fingerprint(little, P1), Fingerprint(much, P1));

        WorldState seen = Executor(On).Step(Fogged([1, B], true));
        Assert.Equal(250.0, Offered(seen, P1, B));
        Assert.Equal(0.0, Offered(seen, P1, W));   // this partner does not know w
    }

    // ------------------------------------------------------------------ parity, replay, persistence, the switch

    /// <summary>AI PARITY (metamorphic): the two Empires know complementary nodes and trade with each other; swapping
    /// which one is the player changes no exposure row — one rule, no command-source branch.</summary>
    [Fact]
    public void AiParity_SwappingThePlayerAndTheAi_ChangesNoExposureRow()
    {
        WorldState Arm(bool swapped)
        {
            WorldState w = World([Player, 2], [Player, 2], 10_000);
            foreach (int k in new[] { 1, B }) w.ResearchCompleted.Add(new ResearchCompletedRow(P1, Key(k)));
            foreach (int k in new[] { 1, W }) w.ResearchCompleted.Add(new ResearchCompletedRow(P2, Key(k)));
            w.TradeFlows.Add(new TradeFlowRow(new SettlementId(1), new SettlementId(0), new GoodId(1), 5));
            if (swapped)
                for (int i = 0; i < w.Polities.Count; i++)
                    w.Polities[i] = w.Polities[i] with { Source = w.Polities[i].Id == P1 ? CommandSource.Ai : CommandSource.Player };
            return Run(Executor(On), w, 3);
        }
        WorldState a = Arm(false), b = Arm(true);
        Assert.Equal(750.0, Offered(a, P1, W));   // polity 1 learns w from polity 2 …
        Assert.Equal(750.0, Offered(a, P2, B));   // … and polity 2 learns b from polity 1, by the same rule
        Assert.Equal(a.ResearchExposures.Count, b.ResearchExposures.Count);
        for (int i = 0; i < a.ResearchExposures.Count; i++) Assert.Equal(a.ResearchExposures[i], b.ResearchExposures[i]);
        for (int i = 0; i < a.ResearchCredits.Count; i++) Assert.Equal(a.ResearchCredits[i], b.ResearchCredits[i]);
    }

    /// <summary>REPLAY AND SAVE/LOAD: two runs from the same state are hash-equal, and a world saved mid-exposure (rows
    /// populated, offered not yet fully paid) loads bit-exactly and continues hash-equal with the live one.</summary>
    [Fact]
    public void Replay_And_SaveLoad_MidExposure_AreHashEqual()
    {
        WorldState live = Run(Executor(On), Rig(), 6);
        Assert.Equal(WorldHash.ComputeHex(live), WorldHash.ComputeHex(Run(Executor(On), Rig(), 6)));

        WorldState mid = Run(Executor(On), Rig(), 2);
        Assert.Equal(500.0, Offered(mid, P1, B));
        Assert.Equal(250.0, Credited(mid, P1, B));
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(mid, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(mid), ms.Length);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(mid, back));
        Assert.Equal(WorldHash.ComputeHex(mid), WorldHash.ComputeHex(back));
        Assert.Equal(WorldHash.ComputeHex(live), WorldHash.ComputeHex(Run(Executor(On), back, 4)));
    }

    /// <summary>NO SCHEMA BUMP: the writer fills the v26 section-47 table that already exists — the schema version is the
    /// base tree's 39, and a world the writer populated round-trips at exactly ExpectedLength (the populated-table rule).</summary>
    [Fact]
    public void NoSchemaBump_TheWriterFillsTheExistingSection_PopulatedRoundTripIsExact()
    {
        Assert.Equal(39, CanonicalSchema.Version);
        WorldState w = Run(Executor(On), Rig(), 3);
        Assert.Equal(2, w.ResearchExposures.Count);
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(w, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(w), ms.Length);
        // The same world with the rows stripped is shorter by exactly two section-47 rows (4 + 4 + 8 bytes each).
        WorldState stripped = w.Clone();
        stripped.ResearchExposures.Clear();
        Assert.Equal(2L * 16L, CanonicalSchema.ExpectedLength(w) - CanonicalSchema.ExpectedLength(stripped));
    }

    /// <summary>THE SWITCH-OFF TWIN: enabled false writes no row, and its world is the world of the block ABSENT byte for
    /// byte (the pre-ADR-058 engine); with the switch on the same rig differs.</summary>
    [Fact]
    public void SwitchOff_WritesNothing_AndEqualsTheBlockAbsent()
    {
        WorldState off = Run(Executor(Content(enabled: false)), Rig(), 5);
        WorldState absent = Run(Executor(Content(absent: true)), Rig(), 5);
        Assert.Equal(0, off.ResearchExposures.Count);
        Assert.Equal(0, absent.ResearchExposures.Count);
        Assert.Equal(WorldHash.ComputeHex(absent), WorldHash.ComputeHex(off));
        Assert.NotEqual(WorldHash.ComputeHex(off), WorldHash.ComputeHex(Run(Executor(On), Rig(), 5)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.01)]
    [InlineData(1.5)]
    public void Loader_RefusesARateOutsideZeroToOne(double rate)
    {
        var ex = Assert.Throws<ResearchContentException>(() => Content(rate: rate));
        Assert.Contains("tuning.foreignExposure.ratePerYear", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedContent_HasTheWriterOn_AtTheChosenRate()
    {
        ForeignExposureTuning? fe = TestConfigs.Research().Tuning.ForeignExposure;
        Assert.NotNull(fe);
        Assert.True(fe!.Enabled);
        Assert.Equal(0.01, fe.RatePerYear);
    }

    /// <summary>dt-CORRECTNESS (law 3): the same ten years of contact offer the same amount at dt 10 (one step) and at dt 1
    /// (ten steps) — the rate is per sim-year.</summary>
    [Fact]
    public void Offer_IsPerSimYear_TenYearsOfferTheSameAtDt10AndDt1()
    {
        WorldState coarse = new TurnExecutor(FlatEra(10.0), [SystemCatalog.Diffusion(Cfg(On))]).Step(Rig());
        var fine = new TurnExecutor(FlatEra(1.0), [SystemCatalog.Diffusion(Cfg(On))]);
        WorldState w = Rig();
        for (int t = 0; t < 10; t++) w = fine.Step(w);
        Assert.Equal(250.0, Offered(coarse, P1, B));
        Assert.Equal(250.0, Offered(w, P1, B), 9);   // ten 25-RP increments (doubles; exact to 1e-9 RP)
    }

    // ------------------------------------------------------------------ helpers

    private const int Size = 32;   // 8 × 8 knowledge-lattice nodes of 4 px

    /// <summary>A 32 × 32 all-land terrain (no water, no river).</summary>
    internal static TerrainSet LandTerrain(Func<int, int, double>? water = null, Func<int, int, double>? rivers = null)
    {
        int n = Size * Size;
        var zero = new double[n];
        var one = new double[n];
        var wat = new double[n];
        var riv = new double[n];
        for (int c = 0; c < n; c++)
        {
            one[c] = 1.0;
            wat[c] = water?.Invoke(c % Size, c / Size) ?? 0.0;
            riv[c] = rivers?.Invoke(c % Size, c / Size) ?? 0.0;
        }
        return TerrainSet.FromLayers(Size, 4.0, 0.0, zero, wat, zero, one, one, one, riv, [], wrapX: false);
    }

    private static int RowsOf(IReadOnlyWorldState w, PolityId p)
    {
        int n = 0;
        for (int i = 0; i < w.ResearchExposures.Count; i++) if (w.ResearchExposures[i].Polity == p) n++;
        return n;
    }

    /// <summary>Polity <paramref name="p"/>'s research rows as text (its view of the world's research state).</summary>
    private static string Fingerprint(IReadOnlyWorldState w, PolityId p)
    {
        var parts = new List<string>();
        for (int i = 0; i < w.ResearchProgress.Count; i++) if (w.ResearchProgress[i].Polity == p) parts.Add(w.ResearchProgress[i].ToString());
        for (int i = 0; i < w.ResearchCredits.Count; i++) if (w.ResearchCredits[i].Polity == p) parts.Add(w.ResearchCredits[i].ToString());
        for (int i = 0; i < w.ResearchCompleted.Count; i++) if (w.ResearchCompleted[i].Polity == p) parts.Add(w.ResearchCompleted[i].ToString());
        for (int i = 0; i < w.ResearchExposures.Count; i++) if (w.ResearchExposures[i].Polity == p) parts.Add(w.ResearchExposures[i].ToString());
        return string.Join("|", parts);
    }
}
