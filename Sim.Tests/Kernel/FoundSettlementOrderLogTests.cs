using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Tests.TestUtil;
using Xunit;

namespace Sim.Tests.Kernel;

/// <summary>
/// M5 S1b (ADR-036) — OrderKind 9 FoundSettlement on the fixed five-field record: the wire format and its load-time
/// range checks, the world-dependent checks (OrderValidation, with the ADR-034 deferral for a source founded
/// mid-game), and a PLAYED log that founds — a colony founded by the order and then a colony founded FROM that
/// colony — replayed, saved and loaded at every turn across issue and effect, hash for hash.
/// </summary>
public class FoundSettlementOrderLogTests
{
    private static readonly PolityId Player = new(1);
    private static SimConfig Cfg => FoundingRigs.Cfg();

    private static OrderLog RoundTrip(OrderLog log)
    {
        using var ms = new MemoryStream();
        log.Save(ms);
        ms.Position = 0;
        return OrderLog.Load(ms);
    }

    private static OrderLog One(OrderRecord r)
    {
        var log = new OrderLog();
        log.Append(r);
        return log;
    }

    // ================================================================== WIRE FORMAT + LOAD

    [Fact]
    public void FoundSettlement_IsKindNine_AndRoundTripsBitExactThroughTheUnchangedWireFormat()
    {
        Assert.Equal(9, (int)OrderKind.FoundSettlement);
        Assert.Equal(1, OrderLog.IoVersion);
        var log = new OrderLog();
        log.Append(OrderRecord.From(4, Player, OrderKind.FoundSettlement, 0, 0.0));
        log.Append(OrderRecord.From(4, Player, OrderKind.FoundSettlement, 12, 1024.0 * 1024.0 - 1.0));
        log.Append(OrderRecord.From(9, Player, OrderKind.FoundSettlement, int.MaxValue, int.MaxValue));
        OrderLog back = RoundTrip(log);
        Assert.Equal(log.Count, back.Count);
        for (int i = 0; i < log.Count; i++)
        {
            Assert.Equal(log[i], back[i]);
            Assert.Equal(BitConverter.DoubleToInt64Bits(log[i].Amount), BitConverter.DoubleToInt64Bits(back[i].Amount));
        }
    }

    [Theory]
    [InlineData(-1, 5.0, "source settlement id must be >= 0")]
    [InlineData(int.MinValue, 5.0, "source settlement id must be >= 0")]
    [InlineData(0, 1.5, "site cell must be a non-negative whole number")]
    [InlineData(0, -1.0, "site cell must be a non-negative whole number")]
    [InlineData(0, -0.5, "site cell must be a non-negative whole number")]
    [InlineData(0, double.NaN, "site cell must be a non-negative whole number")]
    [InlineData(0, double.PositiveInfinity, "site cell must be a non-negative whole number")]
    [InlineData(0, double.NegativeInfinity, "site cell must be a non-negative whole number")]
    [InlineData(0, 2147483648.0, "site cell must be a non-negative whole number")]
    public void AMalformedFoundSettlement_IsRejectedAtLoad(int source, double cell, string fragment)
    {
        var e = Assert.Throws<SnapshotFormatException>(() =>
            RoundTrip(One(OrderRecord.From(3, Player, OrderKind.FoundSettlement, source, cell))));
        Assert.Contains(fragment, e.Message);
    }

    [Fact]
    public void AnOffMapCell_IsAWorldCheck_RejectedUpFront_ForEveryActorAndEveryTurn()
    {
        // The critic correction: load sees no terrain, so "< Size²" is checked by ValidateAgainstWorld — and, the
        // terrain being static, even for a source id the ADR-034 deferral would otherwise wave through.
        WorldState w = FoundingRigs.Dev(42UL);
        int size = w.Terrain!.Size;
        OrderValidation.ValidateAgainstWorld(One(OrderRecord.From(0, Player, OrderKind.FoundSettlement, 0, size * size - 1)), w);
        var e = Assert.Throws<OrderValidationException>(() =>
            OrderValidation.ValidateAgainstWorld(One(OrderRecord.From(0, Player, OrderKind.FoundSettlement, 0, size * size)), w));
        Assert.Contains("off the map", e.Message);
        int deferred = FoundingRigs.MaxSettlementId(w) + 1;
        Assert.Throws<OrderValidationException>(() =>
            OrderValidation.ValidateAgainstWorld(One(OrderRecord.From(5, Player, OrderKind.FoundSettlement, deferred, size * size)), w));
    }

    [Fact]
    public void TheSourceIsATurnZeroSettlement_ItMustExistAndBeTheIssuersUpFront()
    {
        WorldState w = FoundingRigs.Dev(42UL, aiEmpires: 1);
        // Settlement 1 is the rival's (round-robin founding control).
        var e = Assert.Throws<OrderValidationException>(() =>
            OrderValidation.ValidateAgainstWorld(One(OrderRecord.From(0, Player, OrderKind.FoundSettlement, 1, 100)), w));
        Assert.Contains("does not control", e.Message);
        // A missing source at turn 0 is never deferred (the turn-0 batch reads exactly this world).
        var missing = Assert.Throws<OrderValidationException>(() =>
            OrderValidation.ValidateAgainstWorld(One(OrderRecord.From(0, Player, OrderKind.FoundSettlement, 99, 100)), w));
        Assert.Contains("does not exist", missing.Message);
        OrderValidation.ValidateAgainstWorld(One(OrderRecord.From(0, Player, OrderKind.FoundSettlement, 0, 100)), w);
    }

    [Fact]
    public void ASourceAboveTheTurnZeroMaximum_IsDeferredUpFront_AndMustExistAtDelivery()
    {
        // ADR-034: a colony takes maxId + 1, so a source above every turn-0 id may be founded by delivery.
        WorldState w0 = FoundingRigs.Dev(42UL);
        int colony = FoundingRigs.MaxSettlementId(w0) + 1;
        OrderBatch Batch(OrderLog l, long t) => l.BatchFor(t);
        OrderLog log = One(OrderRecord.From(3, Player, OrderKind.FoundSettlement, colony, 100));
        OrderValidation.ValidateAgainstWorld(log, w0);                       // deferred
        var e = Assert.Throws<OrderValidationException>(() => OrderValidation.ValidateAtDelivery(Batch(log, 3), w0, w0));
        Assert.Contains("does not exist when the order is delivered", e.Message);
    }

    // ================================================================== A PLAYED LOG THAT FOUNDS

    private const int Horizon = 7;
    private const long FoundTurn = 1, FromColonyTurn = 3;

    /// <summary>The colony founded by the turn-1 order (the dev world's turn-0 maximum + 1).</summary>
    private static int Colony => FoundingRigs.MaxSettlementId(FoundingRigs.Dev(42UL)) + 1;

    /// <summary>The player's script, a pure function of (turn, the world the player sees): at turn 1 found from
    /// settlement 0; at turn 3 found FROM THE COLONY (the ADR-034 case) — each at the nearest legal site.</summary>
    private static void Script(OrderLog log, IReadOnlyWorldState w)
    {
        long t = w.Clock.Turn;
        if (t != FoundTurn && t != FromColonyTurn) return;
        SettlementId src = t == FoundTurn ? w.Settlements[0].Id : new SettlementId(Colony);
        int site = FoundingRigs.NearestLegalSite(w, Cfg, FoundingRigs.Frontier(w), Player, src);
        if (site < 0) throw new InvalidOperationException($"turn {t}: no legal site from settlement {src.Value}");
        log.Append(FoundingQuery.FoundOrder(w, Player, src, site));
    }

    internal sealed record Played(List<WorldState> Worlds, List<string> Hashes, OrderLog Log);

    private static readonly Lazy<Played> Run = new(() =>
    {
        var log = new OrderLog();
        TurnExecutor exec = FoundingRigs.Pipeline(Cfg, log);
        WorldState w = FoundingRigs.Dev(42UL);
        var worlds = new List<WorldState> { w.Clone() };
        var hashes = new List<string> { WorldHash.ComputeHex(w) };
        for (int t = 0; t < Horizon; t++)
        {
            Script(log, w);
            w = exec.Step(w);
            worlds.Add(w.Clone());
            hashes.Add(WorldHash.ComputeHex(w));
        }
        return new Played(worlds, hashes, log);
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    [Fact]
    public void ThePlayedLog_FoundsAColony_ThenFoundsFromThatColony_AndValidatesUpFrontAndAtDelivery()
    {
        Played p = Run.Value;
        Assert.Equal(2, p.Log.Count);
        Assert.Equal(Colony, p.Log[1].TargetId);                      // the second order's source IS the colony

        // Turn-exact: the colony stands from state 2 (stamped 1), the grand-colony from state 4 (stamped 3).
        Assert.False(FoundingRigs.TryGetSettlement(p.Worlds[(int)FoundTurn], Colony, out _));
        Assert.True(FoundingRigs.TryGetSettlement(p.Worlds[(int)FoundTurn + 1], Colony, out SettlementRow c));
        Assert.Equal(FoundTurn + 1, c.FoundedTurn);
        Assert.False(FoundingRigs.TryGetSettlement(p.Worlds[(int)FromColonyTurn], Colony + 1, out _));
        Assert.True(FoundingRigs.TryGetSettlement(p.Worlds[(int)FromColonyTurn + 1], Colony + 1, out SettlementRow g));
        Assert.Equal(FromColonyTurn + 1, g.FoundedTurn);
        Assert.True(EmpireQuery.ControlsSettlement(p.Worlds[^1], Player, new SettlementId(Colony + 1)));
        Assert.True(FoundingRigs.People(p.Worlds[(int)FromColonyTurn + 1], new SettlementId(Colony))
                    < FoundingRigs.People(p.Worlds[(int)FromColonyTurn], new SettlementId(Colony)));

        // What `sim run --orders`, `sim replay` and `sim inspect` run: up front against turn 0, then each batch at
        // delivery against the world it is delivered to.
        WorldState turnZero = p.Worlds[0];
        OrderValidation.ValidateAgainstWorld(p.Log, turnZero);
        for (int t = 0; t < Horizon; t++) OrderValidation.ValidateAtDelivery(p.Log.BatchFor(t), turnZero, p.Worlds[t]);

        // A FORGED source above the turn-0 maximum passes up front and is rejected where it is delivered.
        var forged = new OrderLog();
        forged.Append(p.Log[0]);
        forged.Append(OrderRecord.From(FromColonyTurn, Player, OrderKind.FoundSettlement, Colony + 5, p.Log[1].Amount));
        OrderValidation.ValidateAgainstWorld(forged, turnZero);
        Assert.Throws<OrderValidationException>(() =>
            OrderValidation.ValidateAtDelivery(forged.BatchFor(FromColonyTurn), turnZero, p.Worlds[(int)FromColonyTurn]));
    }

    [Fact]
    public void TheReplayOfThePlayedLog_IsHashEqualEveryTurn()
    {
        Played p = Run.Value;
        OrderLog loaded = RoundTrip(p.Log);
        TurnExecutor exec = FoundingRigs.Pipeline(Cfg, loaded);
        WorldState w = FoundingRigs.Dev(42UL);                         // founded anew
        Assert.Equal(p.Hashes[0], WorldHash.ComputeHex(w));
        for (int t = 0; t < Horizon; t++)
        {
            w = exec.Step(w);
            Assert.True(p.Hashes[t + 1] == WorldHash.ComputeHex(w), $"replay diverged at turn {t + 1}");
        }
    }

    public static TheoryData<int, bool> SavePoints()
    {
        var d = new TheoryData<int, bool>();
        for (int k = 1; k < Horizon; k++) d.Add(k, false);
        d.Add((int)FoundTurn, true);          // issued, not yet delivered
        d.Add((int)FromColonyTurn, true);
        return d;
    }

    [Theory]
    [MemberData(nameof(SavePoints))]
    public void SaveAndLoad_AtEveryTurnAcrossIssueAndEffect_ContinuesHashEqual(int k, bool issued)
    {
        // Saved at k (world snapshot + the log as it stood: every order stamped before k, plus — issued — the
        // player's turn-k order), loaded, and continued with a FRESH executor re-running the script.
        Played p = Run.Value;
        var partial = new OrderLog();
        for (int i = 0; i < p.Log.Count; i++)
            if (p.Log[i].Turn < k || (issued && p.Log[i].Turn == k)) partial.Append(p.Log[i]);
        if (issued) Assert.Contains(Enumerable.Range(0, partial.Count), i => partial[i].Turn == k);

        using var ms = new MemoryStream();
        Snapshot.Save(p.Worlds[k], ms);
        ms.Position = 0;
        WorldState w = Snapshot.Load(ms, p.Worlds[k].Terrain);
        Assert.Equal(p.Hashes[k], WorldHash.ComputeHex(w));
        OrderLog log = RoundTrip(partial);

        TurnExecutor exec = FoundingRigs.Pipeline(Cfg, log);
        for (int t = k; t < Horizon; t++)
        {
            if (!(issued && t == k)) Script(log, w);
            w = exec.Step(w);
            Assert.True(p.Hashes[t + 1] == WorldHash.ComputeHex(w), $"saved at {k} (issued={issued}): diverged at turn {t + 1}");
        }
        Assert.Equal(p.Log.Count, log.Count);
        for (int i = 0; i < log.Count; i++) Assert.Equal(p.Log[i], log[i]);
    }

    [Fact]
    public void AWorldHoldingOrderFoundedColonies_SerializesAtExactLength_AndRoundTripsBitExact()
    {
        // No new row type ships (the order rides existing tables), but the colonies' rows are POPULATED here:
        // exact schema length, bit-exact round trip, equal hash.
        WorldState w = Run.Value.Worlds[^1];
        Assert.True(FoundingRigs.TryGetSettlement(w, Colony + 1, out _));
        using var raw = new MemoryStream();
        using (var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true)) CanonicalSchema.Write(w, writer);
        Assert.Equal(CanonicalSchema.ExpectedLength(w), raw.Length);

        using var a = new MemoryStream();
        Snapshot.Save(w, a);
        a.Position = 0;
        WorldState back = Snapshot.Load(a, w.Terrain);
        using var b = new MemoryStream();
        Snapshot.Save(back, b);
        Assert.Equal(a.ToArray(), b.ToArray());
        Assert.Equal(WorldHash.ComputeHex(w), WorldHash.ComputeHex(back));
    }
}
