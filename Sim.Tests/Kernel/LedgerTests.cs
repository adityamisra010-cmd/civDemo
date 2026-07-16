using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;

namespace Sim.Tests.Kernel;

// T0.6 acceptance: exact conservation under arbitrary op sequences (FsCheck),
// truthful clamping, named overflow exception (never wrap), auditor teeth.
public class LedgerTests
{
    // --- deterministic example tests -------------------------------------------

    private static (WorldState World, Ledger Ledger) NewWorldWithGoods(params long[] endowments)
    {
        var world = new WorldState(seed: 1);
        var ledger = new Ledger(world.LedgerFlows);
        for (int i = 0; i < endowments.Length; i++)
        {
            world.Regions.Add(new RegionRow(new RegionId(i)));
            int idx = world.Goods.Add(new GoodsRow(new RegionId(i), Conserved.Zero));
            ledger.Flow(ref world.Goods.Ref(idx).Amount, ConservedQuantityIds.ToyGood,
                ReasonIds.InitialEndowment, endowments[i], FlowDirection.Source, OverdrawPolicy.Throw);
        }
        return (world, ledger);
    }

    [Fact]
    public void NegativeAmount_AlwaysThrows_BothOps()
    {
        var (world, ledger) = NewWorldWithGoods(100, 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Transfer(
            ref world.Goods.Ref(0).Amount, ref world.Goods.Ref(1).Amount, -1, OverdrawPolicy.ClampToAvailable));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Flow(
            ref world.Goods.Ref(0).Amount, ConservedQuantityIds.ToyGood, ReasonIds.Growth,
            -1, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable));
    }

    [Fact]
    public void ClampedTransfer_ConservesAndReportsTruthfully()
    {
        var (world, ledger) = NewWorldWithGoods(30, 0);
        long moved = ledger.Transfer(
            ref world.Goods.Ref(0).Amount, ref world.Goods.Ref(1).Amount, 100, OverdrawPolicy.ClampToAvailable);
        Assert.Equal(30, moved);                          // truthful report
        Assert.Equal(0, world.Goods[0].Amount.Value);
        Assert.Equal(30, world.Goods[1].Amount.Value);
        Assert.True(ConservationAuditor.IsConserved(world, out _));
    }

    [Fact]
    public void OverdrawUnderThrowPolicy_Throws_AndMutatesNothing()
    {
        var (world, ledger) = NewWorldWithGoods(30, 5);
        Assert.Throws<LedgerOverdrawException>(() => ledger.Transfer(
            ref world.Goods.Ref(0).Amount, ref world.Goods.Ref(1).Amount, 100, OverdrawPolicy.Throw));
        Assert.Equal(30, world.Goods[0].Amount.Value);
        Assert.Equal(5, world.Goods[1].Amount.Value);
        Assert.True(ConservationAuditor.IsConserved(world, out _));
    }

    [Fact]
    public void ExactBalanceTransfer_MovesEverything()
    {
        var (world, ledger) = NewWorldWithGoods(42, 0);
        long moved = ledger.Transfer(
            ref world.Goods.Ref(0).Amount, ref world.Goods.Ref(1).Amount, 42, OverdrawPolicy.Throw);
        Assert.Equal(42, moved);
        Assert.Equal(0, world.Goods[0].Amount.Value);
    }

    [Fact]
    public void NearInt64Max_TransferOverflow_ThrowsNamedException_NeverWraps()
    {
        // Note: within one (quantity, reason), the cumulative counterweight caps
        // total endowment at Int64.Max — endowing Max-1 then 10 under the SAME
        // reason throws at the endowment (also covered below). To reach a
        // transfer-overflow state we endow the second stock under a different
        // reason; the world total then legitimately exceeds Int64, so the audit
        // (whose checked sum would overflow by design) is not run here.
        var (world, ledger) = NewWorldWithGoods(long.MaxValue - 1);
        world.Regions.Add(new RegionRow(new RegionId(1)));
        int idx = world.Goods.Add(new GoodsRow(new RegionId(1), Conserved.Zero));
        ledger.Flow(ref world.Goods.Ref(idx).Amount, ConservedQuantityIds.ToyGood,
            ReasonIds.Growth, 10, FlowDirection.Source, OverdrawPolicy.Throw);

        // Moving 10 into a stock holding Max-1 must throw LedgerOverflowException
        // and mutate NOTHING (atomicity).
        Assert.Throws<LedgerOverflowException>(() => ledger.Transfer(
            ref world.Goods.Ref(1).Amount, ref world.Goods.Ref(0).Amount, 10, OverdrawPolicy.Throw));
        Assert.Equal(long.MaxValue - 1, world.Goods[0].Amount.Value);
        Assert.Equal(10, world.Goods[1].Amount.Value);
    }

    [Fact]
    public void NearInt64Max_CounterweightOverflow_ThrowsNamedException_NeverWraps()
    {
        // The cumulative TotalSourced itself is checked: a second endowment under
        // the same reason that would push it past Int64.Max throws and mutates
        // neither the stock nor the counterweight.
        var (world, ledger) = NewWorldWithGoods(long.MaxValue - 1);
        world.Regions.Add(new RegionRow(new RegionId(1)));
        int idx = world.Goods.Add(new GoodsRow(new RegionId(1), Conserved.Zero));
        Assert.Throws<LedgerOverflowException>(() => ledger.Flow(
            ref world.Goods.Ref(idx).Amount, ConservedQuantityIds.ToyGood,
            ReasonIds.InitialEndowment, 10, FlowDirection.Source, OverdrawPolicy.Throw));
        Assert.Equal(0, world.Goods[1].Amount.Value);
        Assert.True(ConservationAuditor.IsConserved(world, out _));
    }

    [Fact]
    public void NearInt64Max_SourceFlowOverflow_ThrowsNamedException_NeverWraps()
    {
        var (world, ledger) = NewWorldWithGoods(long.MaxValue - 1);
        Assert.Throws<LedgerOverflowException>(() => ledger.Flow(
            ref world.Goods.Ref(0).Amount, ConservedQuantityIds.ToyGood, ReasonIds.Growth,
            10, FlowDirection.Source, OverdrawPolicy.Throw));
        Assert.Equal(long.MaxValue - 1, world.Goods[0].Amount.Value);
        Assert.True(ConservationAuditor.IsConserved(world, out _));
    }

    [Fact]
    public void SinkFlow_ClampAndThrowPolicies_BehaveAndConserve()
    {
        var (world, ledger) = NewWorldWithGoods(50);
        long sunk = ledger.Flow(ref world.Goods.Ref(0).Amount, ConservedQuantityIds.ToyGood,
            ReasonIds.Growth, 80, FlowDirection.Sink, OverdrawPolicy.ClampToAvailable);
        Assert.Equal(50, sunk);
        Assert.Equal(0, world.Goods[0].Amount.Value);
        Assert.Throws<LedgerOverdrawException>(() => ledger.Flow(
            ref world.Goods.Ref(0).Amount, ConservedQuantityIds.ToyGood, ReasonIds.Growth,
            1, FlowDirection.Sink, OverdrawPolicy.Throw));
        Assert.True(ConservationAuditor.IsConserved(world, out _));
    }

    [Fact]
    public void SelfTransfer_IsNetZero_AndConserves()
    {
        var (world, ledger) = NewWorldWithGoods(70);
        long moved = ledger.Transfer(
            ref world.Goods.Ref(0).Amount, ref world.Goods.Ref(0).Amount, 20, OverdrawPolicy.Throw);
        Assert.Equal(20, moved);
        Assert.Equal(70, world.Goods[0].Amount.Value);
        Assert.True(ConservationAuditor.IsConserved(world, out _));
    }

    [Fact]
    public void FailedFlow_OnFirstUseReason_LeavesNoPhantomRow()
    {
        // Atomicity includes table LAYOUT: a failed Flow on a new (quantity,
        // reason) pair must not add a zero-total row (it would change future
        // canonical hashes for callers that catch and continue).
        var (world, ledger) = NewWorldWithGoods(0);
        int rowsBefore = world.LedgerFlows.Count;

        Assert.Throws<LedgerOverdrawException>(() => ledger.Flow(
            ref world.Goods.Ref(0).Amount, ConservedQuantityIds.ToyGood, ReasonIds.Growth,
            5, FlowDirection.Sink, OverdrawPolicy.Throw));
        Assert.Equal(rowsBefore, world.LedgerFlows.Count);

        // Overflow path, same guarantee: source overflow on a fresh reason.
        var (world2, ledger2) = NewWorldWithGoods(long.MaxValue - 1);
        int rows2Before = world2.LedgerFlows.Count;
        Assert.Throws<LedgerOverflowException>(() => ledger2.Flow(
            ref world2.Goods.Ref(0).Amount, ConservedQuantityIds.ToyGood, ReasonIds.Growth,
            10, FlowDirection.Source, OverdrawPolicy.Throw));
        Assert.Equal(rows2Before, world2.LedgerFlows.Count);
    }

    [Fact]
    public void Audit_IdentityArithmeticOverflow_ThrowsRatherThanWraps()
    {
        // "All audit arithmetic is checked": if the identity sum itself cannot
        // fit Int64, the audit fails loudly instead of printing wrapped numbers.
        var audit = new ConservationAuditor.QuantityAudit(
            ConservedQuantityIds.ToyGood, long.MaxValue - 1, TotalSourced: 0, TotalSunk: 10);
        Assert.Throws<OverflowException>(() => audit.IsConserved);
    }

    [Fact]
    public void Auditor_HasTeeth_DroppedLocalTransferBreaksIt()
    {
        // Corrupt a world using only the public API: transfer into a local stock
        // that is then dropped — value leaves the audited world (ADR-004 known
        // escape, used here deliberately as the corruption vector).
        var (world, ledger) = NewWorldWithGoods(100, 100);
        Assert.True(ConservationAuditor.IsConserved(world, out _));

        Conserved blackHole = Conserved.Zero;
        ledger.Transfer(ref world.Goods.Ref(0).Amount, ref blackHole, 25, OverdrawPolicy.Throw);

        Assert.False(ConservationAuditor.IsConserved(world, out string report));
        Assert.Contains("CONSERVATION VIOLATION", report);
    }

    // --- FsCheck: arbitrary op sequences conserve EXACTLY ------------------------

    public record LedgerOp(byte Kind, byte From, byte To, long Amount, bool Clamp);

    private static Arbitrary<LedgerOp> LedgerOpArb() =>
        (from kind in Gen.Choose(0, 2)
         from fromIdx in Gen.Choose(0, 3)
         from toIdx in Gen.Choose(0, 3)
         from amount in Gen.OneOf(
             Gen.Choose(0, 100).Select(i => (long)i),          // small, incl. 0
             Gen.Constant(0L),                                  // explicit zero
             Gen.Choose(0, 1000).Select(i => (long)i * 37L),    // mid-size
             Gen.Constant(250L))                                // exact-balance candidates
         from clamp in ArbMap.Default.GeneratorFor<bool>()
         select new LedgerOp((byte)kind, (byte)fromIdx, (byte)toIdx, amount, clamp)).ToArbitrary();

    [Property(MaxTest = 200)]
    public Property ArbitraryOpSequences_ConserveExactly()
    {
        Gen<LedgerOp[]> opsGen = Gen.ArrayOf(LedgerOpArb().Generator, 50);
        return Prop.ForAll(opsGen.ToArbitrary(), ops =>
        {
            var (world, ledger) = NewWorldWithGoods(250, 250, 250, 250);

            foreach (LedgerOp op in ops)
            {
                OverdrawPolicy policy = op.Clamp ? OverdrawPolicy.ClampToAvailable : OverdrawPolicy.Throw;
                try
                {
                    switch (op.Kind)
                    {
                        case 0:
                            ledger.Transfer(ref world.Goods.Ref(op.From).Amount,
                                ref world.Goods.Ref(op.To).Amount, op.Amount, policy);
                            break;
                        case 1:
                            ledger.Flow(ref world.Goods.Ref(op.From).Amount, ConservedQuantityIds.ToyGood,
                                ReasonIds.Growth, op.Amount, FlowDirection.Source, policy);
                            break;
                        default:
                            ledger.Flow(ref world.Goods.Ref(op.From).Amount, ConservedQuantityIds.ToyGood,
                                ReasonIds.Growth, op.Amount, FlowDirection.Sink, policy);
                            break;
                    }
                }
                catch (LedgerOverdrawException)
                {
                    // Throw-policy overdraws are expected members of arbitrary
                    // sequences; the invariant below must hold regardless.
                }

                // EXACT conservation after EVERY op — no epsilon (law 1).
                if (!ConservationAuditor.IsConserved(world, out string report))
                    return false.Label(report);
            }
            return true.ToProperty();
        });
    }
}
