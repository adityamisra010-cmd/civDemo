using Proto.M7.Events;
using Proto.M7.Tactical;
using Proto.M7.Time;
using Proto.M7.War;
using Proto.M7.War.Outcomes;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>
/// POPULATED-table round trips for every lane W row type (CLAUDE.md): exact length, bit-exact round trip, hash equality, refusal of a
/// truncated table and of a wrong version; the M8 boundary (no treaty or relation field); the record agrees with the world.
/// </summary>
public class OutcomeCodecTests
{
    private static readonly long T1 = B(1) + Day, TB = B(2) + Day, T3 = B(3) + Day, T4 = B(4) + Day;

    /// <summary>A world with every kind of record: a captive leader, an execution, a sack, a raze, a field battle with prisoners both
    /// ways, a release, standing terms, a ransom, a standing exchange offer, rations split at a turn start, a war ended in conquest.</summary>
    internal static WarOutcomeWorld Populated()
    {
        var battle = new FieldBattle(0, TB - Day, 1, 0, BattleMechanism.Tactical, (byte)BattleEndReason.SideEliminated, 0, new[]
        {
            new CollapseInput(0, 1, new long[] { 25, 5, 10, 5, 5 }, 300, 1_000),
            new CollapseInput(2, 0, new long[] { 40, 2, 0, 3, 5 }, 900, 1_000),
        });
        var b = Build((s, w) =>
        {
            Capture(s, T1, 2, 0, 9_000);            // polity 1's seat: its leader becomes polity 0's captive
            w.ScheduleBattle(s, TB, battle);
            Capture(s, T3, 0, 2, 4_000);            // polity 0's seat: its leader becomes polity 2's captive
            Capture(s, T4, 1, 2, 4_000);            // polity 0's last two cities fall at one instant: executed
            Capture(s, T4, 2, 1, 4_000);            // (polity 1 retakes its seat city; its leader stays polity 0's captive)
        }, stops: true);
        long turn = Cal.TurnOf(new SimInstant(T4)) + 1;
        long after = Cal.TurnStart(turn).Ms + 3 * Day;
        Drive(b, after, at =>
            at == T1 ? new[] { (OutcomeKinds.OrderDispose, Pack(0, 2), (long)Disposition.Sack) }
            : at == TB ? new[]
            {
                (OutcomeKinds.OrderPrisoners, Pack(1, 0), Value((long)PrisonerAction.Release, 100)),
                (OutcomeKinds.OrderRansomTerms, Pack(0, 0), 200L),
                (OutcomeKinds.OrderRansom, Pack(1, 0), 150L),
                (OutcomeKinds.OrderExchange, Pack(0, 1), 40L),
            }
            : at == T4 ? new[] { (OutcomeKinds.OrderDispose, Pack(2, 1), (long)Disposition.Raze) }
            : None);
        return b.W;
    }

    private static void RoundTrip<T>(IReadOnlyList<T> rows, int rowBytes, OutcomeCodec.SpanEncoder<T> enc, OutcomeCodec.SpanDecoder<T> dec)
    {
        Assert.NotEmpty(rows);
        byte[] a = OutcomeCodec.EncodeTable(rows, rowBytes, enc);
        Assert.Equal(OutcomeCodec.HeaderBytes + rows.Count * rowBytes, a.Length);                // exact expected length
        var back = OutcomeCodec.DecodeTable(a, rowBytes, dec);
        Assert.Equal(rows.Count, back.Count);
        byte[] b = OutcomeCodec.EncodeTable(back, rowBytes, enc);
        Assert.Equal(a, b);                                                                       // bit-exact
        Assert.Equal(OutcomeCodec.HashHex(a), OutcomeCodec.HashHex(b));                           // hash equality
        Assert.Throws<FormatException>(() => OutcomeCodec.DecodeTable(a.AsSpan(0, a.Length - 1), rowBytes, dec));
        var bad = (byte[])a.Clone(); bad[0] = 99;
        Assert.Throws<FormatException>(() => OutcomeCodec.DecodeTable(bad, rowBytes, dec));
    }

    [Fact]
    public void PopulatedTables_RoundTripBitExact_WithExactLengths()
    {
        var w = Populated();
        // populated, not empty or default
        Assert.Contains(w.Fates, f => f.To == (byte)LeaderFate.Executed && f.From == (byte)LeaderFate.Captive);
        Assert.Contains(w.Fates, f => f.To == (byte)LeaderFate.Captive && f.Polity == 1);
        Assert.Contains(w.SettlementOutcomes, o => o.Disposition == (byte)Disposition.Sack && o.LootMoney > 0 && o.Killed > 0 && o.GrievancePermilleYear > 1_000);
        Assert.Contains(w.SettlementOutcomes, o => o.Disposition == (byte)Disposition.Raze && o.GrainBurnt > 0 && o.Burnt > 0);
        foreach (var a in new[] { PrisonerAction.Intake, PrisonerAction.Release, PrisonerAction.Ransom })
            Assert.Contains(w.PrisonerRows, p => p.Action == (byte)a);
        Assert.Contains(w.PrisonerRows, p => p.Money > 0);
        foreach (var c in new[] { CasualtyCause.Killed, CasualtyCause.Wounded, CasualtyCause.Captured, CasualtyCause.Deserted, CasualtyCause.CiviliansKilled, CasualtyCause.CiviliansFled })
            Assert.Contains(w.Casualties, x => x.Cause == (byte)c);
        var custody = w.CustodyRows();
        Assert.Contains(custody, c => c.CarryDen > 1 && c.SettledMs > TB);
        Assert.Contains(custody, c => c.ExchangeOffer == 40);
        Assert.Contains(w.PolityRows(), p => p.MenRansomTerms == 200 && p.Fate == (byte)LeaderFate.Executed);
        Assert.Contains(w.PolityRows(), p => p.Fate == (byte)LeaderFate.Captive && p.HeldBy == 0);
        Assert.Contains(w.SettlementRows(), s => s.Razed && s.Disposed);
        Assert.Contains(w.SettlementRows(), s => s.TakenBy == 1 && s.TakenFrom == 0 && s.GrievancePermilleYear == 0);   // liberated
        Assert.Equal((byte)WarOutcome.Conquest, w.WarRows()[0].Outcome);
        Assert.Contains(w.ParticipantRows(), p => p.Exit == (byte)ParticipantExit.Eliminated);
        Assert.Contains(w.BattleSides, s => s.Formations > 0 && s.PersonnelStart > 0);

        RoundTrip(w.WarRows(), OutcomeCodec.WarRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeWar);
        RoundTrip(w.ParticipantRows(), OutcomeCodec.ParticipantRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeParticipant);
        RoundTrip(w.Battles, OutcomeCodec.BattleRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeBattle);
        RoundTrip(w.BattleSides, OutcomeCodec.BattleSideRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeBattleSide);
        RoundTrip(w.Casualties, OutcomeCodec.CasualtyRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeCasualty);
        RoundTrip(w.Fates, OutcomeCodec.LeaderFateRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeLeaderFate);
        RoundTrip(w.SettlementOutcomes, OutcomeCodec.SettlementOutcomeRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeSettlementOutcome);
        RoundTrip(w.PrisonerRows, OutcomeCodec.PrisonerRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodePrisoner);
        RoundTrip(w.SettlementRows(), OutcomeCodec.SettlementStateRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeSettlementState);
        RoundTrip(w.PolityRows(), OutcomeCodec.PolityStateRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodePolityState);
        RoundTrip(custody, OutcomeCodec.CustodyRowBytes, OutcomeCodec.Encode, OutcomeCodec.DecodeCustody);
    }

    [Fact]
    public void DecodedRows_EqualTheOriginals_FieldByField()
    {
        var w = Populated();
        var fates = OutcomeCodec.DecodeTable(OutcomeCodec.EncodeTable(w.Fates, OutcomeCodec.LeaderFateRowBytes, OutcomeCodec.Encode),
                                             OutcomeCodec.LeaderFateRowBytes, OutcomeCodec.DecodeLeaderFate);
        Assert.Equal(w.Fates, fates);
        var pol = OutcomeCodec.DecodeTable(OutcomeCodec.EncodeTable(w.PolityRows(), OutcomeCodec.PolityStateRowBytes, OutcomeCodec.Encode),
                                           OutcomeCodec.PolityStateRowBytes, OutcomeCodec.DecodePolityState);
        for (int i = 0; i < pol.Count; i++)
        {
            Assert.Equal(w.PolityRows()[i].Accounts, pol[i].Accounts);
            Assert.Equal(w.PolityRows()[i] with { Accounts = pol[i].Accounts }, pol[i]);
        }
        Assert.Equal(w.SettlementOutcomes, OutcomeCodec.DecodeTable(OutcomeCodec.EncodeTable(w.SettlementOutcomes, OutcomeCodec.SettlementOutcomeRowBytes,
            OutcomeCodec.Encode), OutcomeCodec.SettlementOutcomeRowBytes, OutcomeCodec.DecodeSettlementOutcome));
        // fix r1: the two fields added to the state rows survive the round trip in their own slots (populated, not default)
        var set = OutcomeCodec.DecodeTable(OutcomeCodec.EncodeTable(w.SettlementRows(), OutcomeCodec.SettlementStateRowBytes, OutcomeCodec.Encode),
                                           OutcomeCodec.SettlementStateRowBytes, OutcomeCodec.DecodeSettlementState);
        Assert.Equal(w.SettlementRows(), set);
        Assert.Contains(set, x => x.Sackers == 4_000 && x.TakenAtMs == T4);
        Assert.Contains(pol, p => p.LastLossWar == 0 && p.LastLossBy >= 0);
    }

    [Fact]
    public void RowLengths_ArePinned()
        => Assert.Equal(new[] { 25, 26, 51, 21, 18, 55, 81, 41, 75, 89, 52 }, new[]   // fix r1: settlement state +Sackers, polity state +LastLossWar
        {
            OutcomeCodec.WarRowBytes, OutcomeCodec.ParticipantRowBytes, OutcomeCodec.BattleRowBytes, OutcomeCodec.BattleSideRowBytes, OutcomeCodec.CasualtyRowBytes,
            OutcomeCodec.LeaderFateRowBytes, OutcomeCodec.SettlementOutcomeRowBytes, OutcomeCodec.PrisonerRowBytes, OutcomeCodec.SettlementStateRowBytes,
            OutcomeCodec.PolityStateRowBytes, OutcomeCodec.CustodyRowBytes,
        });

    [Fact]
    public void TheRecord_HasNoTreatyOrDiplomaticField_M8OwnsThose()
    {
        var rowTypes = typeof(WarOutcomeWorld).Assembly.GetTypes().Where(t => t.Namespace == "Proto.M7.War.Outcomes" && t.Name.EndsWith("Row")).ToArray();
        Assert.Equal(11, rowTypes.Length);
        foreach (var t in rowTypes)
            foreach (var p in t.GetProperties())
                foreach (var banned in new[] { "Treaty", "Alliance", "Relation", "Diplomac", "Truce", "Tribute", "Vassal", "Peace" })
                    Assert.DoesNotContain(banned, p.Name);
    }

    [Fact]
    public void ThePopulatedWorld_IsReproducible_AndItsHashIsBuiltFromTheTables()
    {
        var a = Populated();
        var b = Populated();
        Assert.Equal(a.StateHashHex(), b.StateHashHex());
        Assert.Equal(a.RecordHashHex(), b.RecordHashHex());
        Assert.Equal(11, a.EncodedTables().Count);
        Assert.NotEqual(Build().W.RecordHashHex(), a.RecordHashHex());
    }
}
