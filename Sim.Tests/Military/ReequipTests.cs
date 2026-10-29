using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Worldgen;
using Sim.Tests.TestUtil;
using static Sim.Tests.Military.MilitaryRigs;

namespace Sim.Tests.Military;

/// <summary>
/// ADR-055 §6 — THE RE-EQUIP ORDER (kind 17; unit charter §6.6, D-065 R1), row by row of the charter's specification: the
/// permitted transition classes (a) another family, (b) the same family to a branch, (c) branch to branch; the refused ones
/// (identical, composition change, and OD-2's same-line catch-up, isolated in one policy function); materials reserved IN
/// ESCROW at acceptance and sunk at completion; the personnel difference drawn or returned; experience kept or × the declared
/// transfer fraction; the duration a rate × dtYears at two turn lengths; completion AFTER an Age transition of the same step;
/// cancellation and disbandment release the reservation; the separate re-equipment log; turn-exact delivery; a populated
/// save mid-re-equipment; the switch-off twin.
/// </summary>
public class ReequipTests
{
    private const double Transfer = 0.35;

    /// <summary>Ready() plus spearmen (unit.spearmen: hafting), crossbows and longbows known.</summary>
    private static WorldState Armed()
    {
        WorldState w = Ready();
        Know(w, "crossbow", "longbow", "casting_closed");
        return w;
    }

    /// <summary>Adds a formation of <paramref name="identity"/> with <paramref name="people"/> adults drawn from the capital by
    /// Ledger.Transfer (a rig formation; the system's own recruitment is RecruitmentTests'), at the next id.</summary>
    private static int AddFormation(WorldState w, string identity, long people, double experience = 0.0)
    {
        int id = MaxUnitId(w) + 1;
        SettlementId cap = Capital(w);
        w.MilitaryUnits.Add(new MilitaryUnitRow(id, Player, Id(identity).FamilyKey, Id(identity).Key, cap, 1.0, 1.0, experience, 0));
        int row = w.FormationPersonnel.Add(new FormationPersonnelRow(id, default, 0.0, 0));
        long[] draw = RecruitmentQuery.AllocateDraw(w.Buckets, cap, people)!;
        var ledger = new Ledger(w.LedgerFlows);
        for (int b = 0; b < draw.Length; b++)
            if (draw[b] > 0) ledger.Transfer(ref w.Buckets.Ref(b).Count, ref w.FormationPersonnel.Ref(row).Count, draw[b], OverdrawPolicy.Throw);
        return id;
    }

    private static void SetExperience(WorldState w, int unit, double experience)
    {
        int i = RecruitmentQuery.UnitIndex(w.MilitaryUnits, unit);
        w.MilitaryUnits[i] = w.MilitaryUnits[i] with { Experience = experience };
    }

    private static RecruitCheck CheckReequip(IReadOnlyWorldState w, int unit, string target) =>
        RecruitmentQuery.CheckReequip(w, Cfg, RecruitmentQuery.ViewOf(w), Player, unit, Id(target).Key, out _);

    // ------------------------------------------------------------------ the transition policy (one function)

    [Theory]
    [InlineData("warband", "spearmen", ReequipClass.RoleChange, null)]                         // (a) another family
    [InlineData("crossbowmen", "longbowmen", ReequipClass.ToBranch, null)]                     // (b) mainline to a branch
    [InlineData("advanced_piston_aircraft", "jet_fighters", ReequipClass.ToBranch, null)]      // (b) Flight II branch
    [InlineData("jet_fighters", "stealth_aircraft", ReequipClass.BranchToBranch, null)]        // (c) branch to branch
    [InlineData("warband", "axe_warriors", ReequipClass.None, RecruitmentQuery.OD2Refusal)]    // same family to its mainline: OD-2
    [InlineData("jet_fighters", "advanced_piston_aircraft", ReequipClass.None, RecruitmentQuery.OD2Refusal)]
    [InlineData("military_aircraft", "advanced_piston_aircraft", ReequipClass.None, RecruitmentQuery.OD2Refusal)]
    [InlineData("warband", "warband", ReequipClass.None, "the formation already is Warband")]
    public void TransitionPolicy_PermitsABC_RefusesIdenticalAndOD2(string from, string to, ReequipClass cls, string? refusal)
    {
        (ReequipClass c, string? r) = RecruitmentQuery.TransitionPolicy(Families, Id(from), Id(to));
        Assert.Equal((cls, refusal), (c, r));
    }

    [Fact]
    public void TransitionPolicy_RefusesACompositionChange()
    {
        (ReequipClass c, string? r) = RecruitmentQuery.TransitionPolicy(Families, Id("warband"), Id("shore_craft"));
        Assert.Equal(ReequipClass.None, c);
        Assert.Contains("change of composition", r);
    }

    [Fact]
    public void CheckReequip_RefusesEachExcludedClass_AndANonOwner_AndADoubleOrder()
    {
        WorldState w = Armed();
        int warband = Warband(w);
        Assert.Equal((RecruitVerdict.TransitionRefused, RecruitmentQuery.OD2Refusal),
            (CheckReequip(w, warband, "bronze_swordsmen").Verdict, CheckReequip(w, warband, "bronze_swordsmen").Reason));
        Assert.Equal(RecruitVerdict.TransitionRefused, CheckReequip(w, warband, "warband").Verdict);
        Assert.Equal(RecruitVerdict.TransitionRefused, CheckReequip(w, warband, "shore_craft").Verdict);
        Assert.Equal(RecruitVerdict.NotEmpire, RecruitmentQuery.CheckReequip(w, Cfg, RecruitmentQuery.ViewOf(w), new PolityId(5), warband, Id("spearmen").Key, out _).Verdict);
        WorldState two = World(aiEmpires: 1);
        Assert.Equal(RecruitVerdict.NotOwned, RecruitmentQuery.CheckReequip(two, Cfg, RecruitmentQuery.ViewOf(two), new PolityId(2), Warband(two), Id("spearmen").Key, out _).Verdict);
        Assert.Equal(RecruitVerdict.NotKnown, CheckReequip(w, warband, "phalanx").Verdict);
        Assert.True(CheckReequip(w, warband, "spearmen").Ok);
        // While one is pending, another is refused.
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "spearmen"));
        WorldState w1 = MilitaryOnly(log, dtYears: 0.25).Step(w);
        Assert.Equal(RecruitVerdict.AlreadyReequipping, CheckReequip(w1, warband, "archers").Verdict);
    }

    // ------------------------------------------------------------------ acceptance, escrow, completion

    [Fact]
    public void RoleChange_AcceptsIntoEscrow_CompletesAfterTheDuration_AndKeepsTheTransferFractionOfExperience()
    {
        WorldState w0 = Armed();
        int warband = Warband(w0);
        SetExperience(w0, warband, 2.0);
        SettlementId cap = Capital(w0);
        long timber0 = StockAll(w0, "timber"), stone0 = StockAll(w0, "stone"), people0 = People(w0);
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "spearmen"));
        TurnExecutor ex = MilitaryOnly(log, dtYears: 0.25);   // 90 days a step; spearmen take 0.5 years = 180 days

        WorldState w1 = ex.Step(w0);                           // accepted: escrow holds the bill, no work yet
        ReequipRow pending = Assert.Single(Rows(w1.Reequips));
        Assert.Equal((warband, Id("spearmen").Key, 0L, 1L, 180L, 0L, 0L),
            (pending.Unit, pending.TargetIdentity, pending.IssuedTurn, pending.AcceptedTurn, pending.RequiredDays, pending.ElapsedDays, pending.PersonnelDelta));
        Assert.Equal([(warband, Good("timber"), 10L), (warband, Good("stone"), 5L)], Escrow(w1));   // ascending good id
        Assert.Equal((timber0 - 10, stone0 - 5), (StockAll(w1, "timber"), StockAll(w1, "stone")));
        Assert.Equal(Id("warband").Key, Unit(w1, warband).Identity);         // it is its source until completion
        Conserved(w1);

        WorldState w2 = ex.Step(w1);                           // 90 of 180 days
        Assert.Equal(90L, w2.Reequips[0].ElapsedDays);
        Assert.Equal(Id("warband").Key, Unit(w2, warband).Identity);

        WorldState w3 = ex.Step(w2);                           // 180 of 180: complete in the state of turn 3
        Assert.Equal(0, w3.Reequips.Count);
        Assert.Equal(0, w3.ReequipEscrow.Count);
        MilitaryUnitRow done = Unit(w3, warband);
        Assert.Equal((Id("spearmen").FamilyKey, Id("spearmen").Key, cap), (done.Family, done.Identity, done.Location));
        Assert.Equal(BitConverter.DoubleToInt64Bits(2.0 * Transfer), BitConverter.DoubleToInt64Bits(done.Experience));
        ReequipLogRow entry = Assert.Single(Rows(w3.ReequipLog));
        Assert.Equal((3L, warband, Id("warband").Key, Id("spearmen").Key, 1, 0L),
            (entry.Turn, entry.Unit, entry.FromIdentity, entry.ToIdentity, entry.Cause, entry.PersonnelDelta));
        Assert.Equal((10L, 5L), (Sunk(w3, "timber", ReasonIds.Reequipment), Sunk(w3, "stone", ReasonIds.Reequipment)));
        Assert.Equal(0, w3.UnitConversions.Count);              // never ADR-031's log
        Assert.Equal(people0, People(w3));
        Conserved(w3);
    }

    [Theory]
    [InlineData(0.25, 3)]     // 90-day turns: accepted in the state of 1, 180 days by the state of 3
    [InlineData(0.1, 6)]      // 36-day turns: five steps of work after acceptance
    [InlineData(10.0, 2)]     // decade turns: one step of work completes it
    public void TheDuration_IsARateTimesDtYears_TurnExactAtEveryTurnLength(double dtYears, long completionTurn)
    {
        WorldState w = Armed();
        int warband = Warband(w);
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "spearmen"));
        TurnExecutor ex = MilitaryOnly(log, dtYears);
        for (long t = 1; t <= completionTurn; t++)
        {
            w = ex.Step(w);
            bool done = Unit(w, warband).Identity == Id("spearmen").Key;
            Assert.Equal(t == completionTurn, done && w.Reequips.Count == 0 && t >= completionTurn);
            if (done) break;
        }
        Assert.Equal(completionTurn, w.Clock.Turn);
    }

    [Fact]
    public void ToBranch_KeepsExperience_AndAStrengthDifferenceIsDrawnFromTheSettlementByTransfer()
    {
        WorldState w0 = Armed();
        SettlementId cap = Capital(w0);
        int warband = Warband(w0);
        // (a) the warband (20) re-equips as crossbowmen (40): 20 more adults are drawn at acceptance.
        long adults0 = BandViews.Adults(w0.Buckets, cap);
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "crossbowmen"));
        WorldState w1 = MilitaryOnly(log, dtYears: 10.0).Step(w0);
        Assert.Equal(20L, w1.Reequips[0].PersonnelDelta);
        Assert.Equal(40L, Personnel(w1, warband));
        Assert.Equal(adults0 - 20, BandViews.Adults(w1.Buckets, cap));
        Conserved(w1);

        // (b) crossbowmen (40) to the longbowmen branch (40): no one moves, experience kept in full.
        WorldState b0 = Armed();
        int xbow = AddFormation(b0, "crossbowmen", 40, experience: 1.5);
        var log2 = new OrderLog();
        log2.Append(Reequip(0, xbow, "longbowmen"));
        TurnExecutor ex = MilitaryOnly(log2, dtYears: 10.0);
        WorldState b2 = ex.Step(ex.Step(b0));
        MilitaryUnitRow done = Unit(b2, xbow);
        Assert.Equal(Id("longbowmen").Key, done.Identity);
        Assert.Equal(BitConverter.DoubleToInt64Bits(1.5), BitConverter.DoubleToInt64Bits(done.Experience));
        Assert.Equal(40L, Personnel(b2, xbow));
        Assert.Equal(0L, b2.ReequipLog[0].PersonnelDelta);
        Conserved(b2);
    }

    [Fact]
    public void ASmallerTarget_ReturnsTheSurplusAdultsToTheSettlement()
    {
        WorldState w0 = Armed();
        SettlementId cap = Capital(w0);
        int xbow = AddFormation(w0, "crossbowmen", 40);
        long adults0 = BandViews.Adults(w0.Buckets, cap);
        var log = new OrderLog();
        log.Append(Reequip(0, xbow, "spearmen"));             // (a) 40 -> 20
        WorldState w1 = MilitaryOnly(log).Step(w0);
        Assert.Equal(-20L, w1.Reequips[0].PersonnelDelta);
        Assert.Equal(20L, Personnel(w1, xbow));
        Assert.Equal(adults0 + 20, BandViews.Adults(w1.Buckets, cap));
        Conserved(w1);
    }

    // ------------------------------------------------------------------ cancellation, disbandment, ties

    [Fact]
    public void Cancel_ReturnsTheEscrowExactly_LogsCause2_AndDoesNotReverseThePeople()
    {
        WorldState w0 = Armed();
        SettlementId cap = Capital(w0);
        int warband = Warband(w0);
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "crossbowmen"));       // escrow: 10 timber, 5 bronze; +20 adults
        log.Append(CancelReequip(1, warband));
        TurnExecutor ex = MilitaryOnly(log, dtYears: 0.25);   // 1 year = 360 days: still pending at the cancel
        WorldState w1 = ex.Step(w0);
        long timberAtCap1 = Stock(w1, cap, "timber");
        WorldState w2 = ex.Step(w1);
        Assert.Equal(0, w2.Reequips.Count);
        Assert.Equal(0, w2.ReequipEscrow.Count);
        Assert.Equal(timberAtCap1 + 10, Stock(w2, cap, "timber"));
        Assert.Equal(StockAll(w0, "timber"), StockAll(w2, "timber"));
        Assert.Equal(StockAll(w0, "bronze"), StockAll(w2, "bronze"));
        Assert.Equal(0L, Sunk(w2, "timber", ReasonIds.Reequipment));
        Assert.Equal(40L, Personnel(w2, warband));             // the 20 drawn stay in the formation
        Assert.Equal(Id("warband").Key, Unit(w2, warband).Identity);
        ReequipLogRow entry = Assert.Single(Rows(w2.ReequipLog));
        Assert.Equal((2, Id("crossbowmen").Key), (entry.Cause, entry.ToIdentity));
        Conserved(w2);
    }

    [Fact]
    public void Disband_ReturnsPeopleAndEscrow_LogsCause3_AndTheIdIsNeverReissued()
    {
        WorldState w0 = Armed();
        SettlementId cap = Capital(w0);
        int warband = Warband(w0);
        long adults0 = BandViews.Adults(w0.Buckets, cap), people0 = People(w0);
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "spearmen"));
        log.Append(Disband(1, warband));
        log.Append(Recruit(2, cap, "spearmen"));
        TurnExecutor ex = MilitaryOnly(log, dtYears: 0.25);
        WorldState w1 = ex.Step(w0), w2 = ex.Step(w1);
        Assert.Equal(-1, RecruitmentQuery.UnitIndex(w2.MilitaryUnits, warband));
        Assert.Equal(-1, RecruitmentQuery.PersonnelIndex(w2.FormationPersonnel, warband));
        Assert.Equal(0, w2.Reequips.Count);
        Assert.Equal(0, w2.ReequipEscrow.Count);
        Assert.Equal(adults0 + 20, BandViews.Adults(w2.Buckets, cap));
        Assert.Equal(StockAll(w0, "timber"), StockAll(w2, "timber"));
        ReequipLogRow entry = Assert.Single(Rows(w2.ReequipLog));
        Assert.Equal((3, warband, Id("spearmen").Key), (entry.Cause, entry.Unit, entry.ToIdentity));
        Assert.Equal(people0, People(w2));
        Conserved(w2);
        WorldState w3 = ex.Step(w2);
        int fresh = w3.MilitaryUnits[^1].Id;
        Assert.True(fresh > warband);
        Assert.Equal(Id("spearmen").Key, w3.MilitaryUnits[^1].Identity);
        Conserved(w3);
    }

    [Fact]
    public void TheFirstValidReequipOfAFormationInAStepWins()
    {
        WorldState w0 = Armed();
        int warband = Warband(w0);
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "bronze_swordsmen"));   // refused (OD-2): does not count
        log.Append(Reequip(0, warband, "archers"));            // the first valid one
        log.Append(Reequip(0, warband, "spearmen"));           // a later valid one: ignored
        WorldState w1 = MilitaryOnly(log, dtYears: 0.25).Step(w0);
        Assert.Equal(Id("archers").Key, Assert.Single(Rows(w1.Reequips)).TargetIdentity);
    }

    // ------------------------------------------------------------------ ADR-031 interplay

    [Fact]
    public void AnAgeTransitionInTheCompletionStep_ModernizesTheSourceFirst_ThenTheReequipmentCompletesOntoTheTarget()
    {
        WorldState w0 = World();
        w0.AgeStates.Add(new AgeStateRow(Player, 2, 0, 1, 0));
        Know(w0, "arsenical_bronze", "proto_writing", "wheel_solid", "law_code", "hafting");
        SettlementId cap = Capital(w0);
        foreach (string g in new[] { "timber", "stone" }) Endow(w0, cap, g, 200);
        int warband = Warband(w0);
        SetExperience(w0, warband, 1.0);
        Assert.True(AgeQuery.IsEligible(w0, Cfg.Ages!, Player));
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "spearmen"));                                  // accepted in the state of 1
        log.Append(OrderRecord.From(1, Player, OrderKind.AdvanceAge, 3, 1));          // A3 in the state of 2
        TurnExecutor ex = AgeAndMilitary(log, dtYears: 10.0);                         // completes in the step 1 -> 2
        WorldState w1 = ex.Step(w0);
        Assert.True(AgeQuery.IsEligible(w1, Cfg.Ages!, Player));
        WorldState w2 = ex.Step(w1);

        Assert.Equal(3, AgeQuery.CurrentAge(w2, Cfg.Ages!, Player));
        UnitConversionRow conversion = Assert.Single(Rows(w2.UnitConversions));   // ADR-031 applied on the source, logged
        Assert.Equal((warband, Id("warband").Key, Id("bronze_swordsmen").Key, 2L), (conversion.Unit, conversion.FromIdentity, conversion.ToIdentity, conversion.Turn));
        ReequipLogRow entry = Assert.Single(Rows(w2.ReequipLog));                 // then the re-equipment, from the modernized source
        Assert.Equal((Id("bronze_swordsmen").Key, Id("spearmen").Key, 1, 2L), (entry.FromIdentity, entry.ToIdentity, entry.Cause, entry.Turn));
        Assert.Equal(Id("spearmen").Key, Unit(w2, warband).Identity);
        Assert.Equal(BitConverter.DoubleToInt64Bits(Transfer), BitConverter.DoubleToInt64Bits(Unit(w2, warband).Experience));
        Conserved(w2);
    }

    // ------------------------------------------------------------------ persistence and the switch

    [Fact]
    public void APendingReequipment_SurvivesSaveAndLoad_AndTheResumedRunIsTheUninterruptedRun()
    {
        WorldState w0 = Armed();
        int warband = Warband(w0);
        var log = new OrderLog();
        log.Append(Reequip(0, warband, "crossbowmen"));
        TurnExecutor ex = MilitaryOnly(log, dtYears: 0.25);
        WorldState w1 = ex.Step(w0);
        Assert.Equal(1, w1.Reequips.Count);
        Assert.Equal(2, w1.ReequipEscrow.Count);
        WorldState loaded = RoundTrip(w1);
        Assert.Equal(WorldHash.ComputeHex(w1), WorldHash.ComputeHex(loaded));
        WorldState a = w1, b = loaded;
        for (int i = 0; i < 6; i++) { a = ex.Step(a); b = ex.Step(b); }
        Assert.Equal(WorldHash.ComputeHex(a), WorldHash.ComputeHex(b));
        Assert.Equal(Id("crossbowmen").Key, Unit(a, warband).Identity);
    }

    [Fact]
    public void TheSwitchOff_RefusesReequipment()
    {
        SimConfig off = TestConfigs.PreMilitary(Cfg);
        WorldState w0 = World(off);
        Know(w0, "hafting");
        Endow(w0, Capital(w0), "timber", 100);
        Endow(w0, Capital(w0), "stone", 100);
        var log = new OrderLog();
        log.Append(Reequip(0, Warband(w0), "spearmen"));
        WorldState w1 = MilitaryOnly(log, cfg: off).Step(w0);
        Assert.Equal(0, w1.Reequips.Count);
        Assert.True(WorldStates.StateEquals(w1, MilitaryOnly(null, cfg: off).Step(w0)));
    }

    private static (int Unit, int Good, long Units)[] Escrow(IReadOnlyWorldState w)
    {
        var rows = new (int, int, long)[w.ReequipEscrow.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = (w.ReequipEscrow[i].Unit, w.ReequipEscrow[i].Good, w.ReequipEscrow[i].Units.Value);
        return rows;
    }

    internal static WorldState RoundTrip(WorldState w)
    {
        using var buffer = new MemoryStream();
        Snapshot.Save(w, buffer);
        buffer.Position = 0;
        return Snapshot.Load(buffer, w.Terrain);
    }
}
