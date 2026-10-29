using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;
using Sim.Tests.TestUtil;
using static Sim.Tests.TestUtil.ResearchRigs;

namespace Sim.Tests.Systems;

/// <summary>
/// M7 audit (2026-10-27) — THE FOREIGN-EXPOSURE SEAM ACROSS A CONTROL CHANGE, FOR THE AI, AND THROUGH SAVE/LOAD.
///
/// The seam (ADR-029 addendum A; D-046 F§2): <see cref="ResearchExposureRow"/> is a passive INPUT keyed by knowledge
/// holder (no writer exists — M6.21/T6.10 owns one); ResearchSystem pays the offered amount not yet credited from the one
/// shared pool (≤ 0.40 × BaseCost with Eureka, ≤ the remaining EffectiveCost) and records it per source in
/// <see cref="ResearchCreditRow"/>. D-048 ruling 3 (RATIFIED 2026-10-04): "Eureka credit remains with the parent. Fired
/// Eurekas and credit provenance are not copied." KnowledgeMonotonicTests pins the fired-Eureka rows; these tests pin the
/// rest of that ruling for the exposure seam — the credit provenance (both sources) and the offers stay with the parent
/// holder, the new polity starts with none, and an offer keyed to the parent is never paid to the polity that now holds the
/// settlement. They also pin AI parity (the same offer and the same Eureka pay an AI polity exactly what they pay the
/// player — one rule, no command-source branch) and that a world saved mid-exposure continues exactly like the live one.
/// </summary>
public class ResearchExposureControlChangeTests
{
    private static readonly ResearchContent Rig = Standard().Load();
    private static readonly PolityId P1 = new(Player);
    private static readonly PolityId P2 = new(2);   // the polity the revolt founds (one above the roster)
    private const int Mil = 6, Med = 7;

    /// <summary>KnowledgeMonotonicTests' revolt corner: settlement 1 destitute, settlement 0 comfortable, the player knowing
    /// a b c d e + law, with research effort on mil — plus exposure offers and credit provenance of both sources.</summary>
    private static WorldState Split()
    {
        WorldState w = World([Player], [Player, Player], 10_000);
        var ledger = new Ledger(w.LedgerFlows);
        for (int i = 0; i < 2; i++)
        {
            var s = new SettlementId(i);
            w.ConsumptionDeficits.Add(new ConsumptionDeficitRow(s, i == 1 ? 1.0 : 0.0, 10_000));
            w.Housing.Add(new HousingRow(s, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            if (i == 0)
                ledger.Flow(ref w.Housing.Ref(i).Dwellings, ConservedQuantityIds.Dwellings,
                    ReasonIds.InitialEndowment, 5_000, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        WithCompleted(w, 1, 2, 3, 4, 5, 1001);
        w.ResearchProgress.Add(new ResearchProgressRow(P1, Key(Mil), 250.0));
        // mil: 200 already paid by exposure (offer fully credited), 50 by a Eureka; med: an offer of 300 not yet paid.
        w.ResearchCredits.Add(new ResearchCreditRow(P1, Key(Mil), (int)AccelerationSource.Eureka, 50.0));
        w.ResearchCredits.Add(new ResearchCreditRow(P1, Key(Mil), (int)AccelerationSource.ForeignExposure, 200.0));
        w.ResearchExposures.Add(new ResearchExposureRow(P1, Key(Mil), 200.0));
        w.ResearchExposures.Add(new ResearchExposureRow(P1, Key(Med), 300.0));
        return w;
    }

    private static TurnExecutor RevoltOnly()
    {
        SimConfig cfg = TestConfigs.Sim() with { Research = Rig };
        return new(FlatEra(10.0), [SystemCatalog.Revolt(cfg)]);
    }

    private static TurnExecutor RevoltThenResearch()
    {
        SimConfig cfg = TestConfigs.Sim() with { Research = Rig };
        return new(FlatEra(10.0), [SystemCatalog.Revolt(cfg), SystemCatalog.Research(cfg)]);
    }

    private static double Credited(IReadOnlyWorldState w, PolityId p, int key, AccelerationSource source) =>
        ResearchQuery.CreditedBySource(w, p, Key(key), source);

    private static int RowsOf<T>(IReadOnlyTable<T> table, Func<T, PolityId> polity, PolityId p) where T : unmanaged
    {
        int n = 0;
        for (int i = 0; i < table.Count; i++) if (polity(table[i]).Value == p.Value) n++;
        return n;
    }

    /// <summary>Foreign-exposure provenance rows of <paramref name="p"/>. (A revolt-born polity earns its OWN Eurekas from
    /// the knowledge it was copied — the rig's node w fires on "c" — so only the exposure source is asserted absent.)</summary>
    private static int ExposureRowsOf(IReadOnlyWorldState w, PolityId p)
    {
        int n = 0;
        for (int i = 0; i < w.ResearchCredits.Count; i++)
            if (w.ResearchCredits[i].Polity == p && w.ResearchCredits[i].Source == (int)AccelerationSource.ForeignExposure) n++;
        return n;
    }

    [Fact]
    public void Revolt_LeavesTheExposureOffersAndTheCreditProvenanceWithTheParent_TheNewPolityGetsNone_D048R3()
    {
        WorldState start = Split();
        WorldState w = RevoltOnly().Step(start);

        Assert.True(EmpireQuery.TryGetController(w, new SettlementId(1), out PolityId founded));
        Assert.Equal(P2, founded);
        // The parent's rows are untouched, row for row.
        Assert.Equal(start.ResearchCredits.Count, w.ResearchCredits.Count);
        for (int i = 0; i < start.ResearchCredits.Count; i++) Assert.Equal(start.ResearchCredits[i], w.ResearchCredits[i]);
        Assert.Equal(start.ResearchExposures.Count, w.ResearchExposures.Count);
        for (int i = 0; i < start.ResearchExposures.Count; i++) Assert.Equal(start.ResearchExposures[i], w.ResearchExposures[i]);
        // Nothing is copied to the new polity (it does receive the completed knowledge — KnowledgeMonotonicTests).
        Assert.Equal(0, RowsOf(w.ResearchCredits, r => r.Polity, P2));
        Assert.Equal(0, RowsOf(w.ResearchExposures, r => r.Polity, P2));
        Assert.True(ResearchQuery.IsCompleted(w, P2, Key(1)));
    }

    [Fact]
    public void AfterRevolt_AnOfferKeyedToTheParent_PaysOnlyTheParent_OnlyItsUnpaidIncrement_FromTheSharedPool()
    {
        TurnExecutor ex = RevoltThenResearch();
        WorldState w1 = ex.Step(Split());   // the revolt founds P2; research reads PREV (P2 not yet on its roster)
        WorldState w2 = ex.Step(w1);        // both polities research from w1

        // med: the parent's 300 offer is paid once (BaseCost 2,500 → ceiling 1,000; remaining cost far above 300).
        Assert.Equal(300.0, Credited(w2, P1, Med, AccelerationSource.ForeignExposure));
        Assert.Equal(300.0, Progress(w2, Med, Player));
        // mil: the offer was already credited in full before the revolt — nothing more is paid, to anyone.
        Assert.Equal(200.0, Credited(w2, P1, Mil, AccelerationSource.ForeignExposure));
        // The new polity holds the settlement and the same knowledge, but the offers are the parent's: it is paid nothing.
        Assert.True(EmpireQuery.ControlsSettlement(w2, P2, new SettlementId(1)));
        Assert.Equal(0.0, Credited(w2, P2, Med, AccelerationSource.ForeignExposure));
        Assert.Equal(0.0, Credited(w2, P2, Mil, AccelerationSource.ForeignExposure));
        Assert.Equal(0.0, Progress(w2, Med, 2));
        Assert.Equal(0, ExposureRowsOf(w2, P2));
        // A later step pays nothing again (cumulative offer, already credited).
        WorldState w3 = ex.Step(w2);
        Assert.Equal(300.0, Credited(w3, P1, Med, AccelerationSource.ForeignExposure));
    }

    /// <summary>Two polities, one settlement each, identical knowledge, stocks and offers; command sources swapped between
    /// the arms. The acceleration credit each receives is the same number by the same rule (Spine principle 7: the AI uses
    /// the player's verbs and information class).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AiParity_TheSameOfferAndTheSameEureka_CreditAnAiPolityExactlyAsThePlayer(bool swapped)
    {
        Spec spec = Standard();
        spec.Technologies.Add(new Node(12, "x", "a", Cost: 100.0, Eurekas: [new Eu("timber", "stock_timber > 0")]));
        ResearchContent content = spec.Load();
        const int a = Player, b = 2;
        WorldState w = World([a, b], [a, b], 10_000);   // polity 1 the player, polity 2 an AI
        if (swapped)                                     // the metamorphic arm: the AI and the player trade places
            for (int i = 0; i < w.Polities.Count; i++)
                w.Polities[i] = w.Polities[i] with { Source = w.Polities[i].Id.Value == Player ? CommandSource.Ai : CommandSource.Player };
        foreach (int p in new[] { a, b })
        {
            foreach (int k in new[] { 1, 2, 3 })   // a b c: c is the research stage, which opens the med subtree
                w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(p), Key(k)));
            w.ResearchExposures.Add(new ResearchExposureRow(new PolityId(p), Key(12), 12.0));
            w.ResearchExposures.Add(new ResearchExposureRow(new PolityId(p), Key(Med), 77.0));
        }
        Stock(w, 0, 4, 10);   // timber in each polity's own settlement: both Eurekas hold
        Stock(w, 1, 4, 10);
        Assert.NotEqual(EmpireQuery.TryGetCommandSource(w, P1, out CommandSource s1) ? s1 : CommandSource.Ai,
            EmpireQuery.TryGetCommandSource(w, P2, out CommandSource s2) ? s2 : CommandSource.Ai);

        WorldState w1 = Executor(content).Step(w);

        foreach (PolityId p in new[] { P1, P2 })
        {
            // In the step, the Eureka is paid first (40 = the whole pool), so the offer of 12 finds no headroom: one pool.
            Assert.Equal(40.0, ResearchQuery.CreditedBySource(w1, p, Key(12), AccelerationSource.Eureka));
            Assert.Equal(0.0, ResearchQuery.CreditedBySource(w1, p, Key(12), AccelerationSource.ForeignExposure));
            Assert.Equal(40.0, ResearchQuery.Progress(w1, p, Key(12)));
            Assert.Equal(77.0, ResearchQuery.CreditedBySource(w1, p, Key(Med), AccelerationSource.ForeignExposure));
        }
    }

    [Fact]
    public void SaveLoad_MidExposure_AfterTheRevolt_TheLoadedWorldContinuesBitExactlyLikeTheLiveOne()
    {
        TurnExecutor live = RevoltThenResearch();
        WorldState w1 = live.Step(Split());
        // The offer on med grows after the save: the loaded world must pay only the increment, exactly as the live one.
        WorldState grow(WorldState w)
        {
            WorldState c = w.Clone();
            for (int i = 0; i < c.ResearchExposures.Count; i++)
                if (c.ResearchExposures[i].Node == Key(Med)) c.ResearchExposures[i] = c.ResearchExposures[i] with { Offered = 450.0 };
            return c;
        }
        WorldState a = Run(live, grow(Run(live, w1, 1)), 3);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            CanonicalSchema.Write(w1, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(w1), ms.Length);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        WorldState back = CanonicalSchema.Read(reader);
        Assert.True(WorldStates.StateEquals(w1, back));
        TurnExecutor resumed = RevoltThenResearch();
        WorldState b = Run(resumed, grow(Run(resumed, back, 1)), 3);

        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.Equal(450.0, Credited(b, P1, Med, AccelerationSource.ForeignExposure));   // 300, then the 150 increment
        Assert.Equal(0, ExposureRowsOf(b, P2));
    }
}
