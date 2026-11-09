using System.Security.Cryptography;
using System.Text;

namespace Gfx.Cinematics;

public enum OutcomeKind { DecisiveVictory, Surrender, Capture, Execution, NegotiatedPeace, Inconclusive, NavalVictory, SiegeVictory, Defeat }
/// <summary>Fate of the OPPOSING leader after the war (post-war state, not a cinematic choice).</summary>
public enum LeaderFate { Alive, Surrendered, Captured, Executed, Fled, Unknown }
public enum WarDomain { Land, Naval, Siege }
public enum Side { Player, Opponent, None }

/// <summary>War record shown on the closing card. Integers only.</summary>
public sealed record WarRecord(int DurationDays, int Battles, long CasualtiesPlayer, long CasualtiesOpponent, int TerritoryCellsChanged);

/// <summary>
/// LABELLED FIXTURE. No war, peace, surrender, leader or leader-fate state exists in Sim.Core (recon-governance
/// §1.4: diplomacy is M8, leaders are open O-34). This DTO is what a future authoritative war-result record would
/// hand the presentation layer; the cinematic reads it and must never write it. Integer/enum fields only, so
/// <see cref="Hash"/> is exact. Execution as a SIM OUTCOME needs a Director ruling (it is a Ledger flow, Law 1).
/// </summary>
public sealed record WarOutcome(
    string FixtureLabel,
    OutcomeKind Kind,
    Side Victor,
    LeaderFate OpponentLeaderFate,
    WarDomain Domain,
    int EraCapabilityTier,
    int PlayerCultureId,
    int OpponentCultureId,
    WarRecord Record,
    ulong WeatherTimeSeed)
{
    public const string Label = "LABELLED FIXTURE - no war state exists in Sim.Core";

    public byte[] CanonicalBytes()
    {
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms, Encoding.UTF8);
        var lb = Encoding.UTF8.GetBytes(FixtureLabel); w.Write(lb.Length); w.Write(lb);
        w.Write((int)Kind); w.Write((int)Victor); w.Write((int)OpponentLeaderFate); w.Write((int)Domain);
        w.Write(EraCapabilityTier); w.Write(PlayerCultureId); w.Write(OpponentCultureId);
        w.Write(Record.DurationDays); w.Write(Record.Battles); w.Write(Record.CasualtiesPlayer); w.Write(Record.CasualtiesOpponent); w.Write(Record.TerritoryCellsChanged);
        w.Write(WeatherTimeSeed);
        w.Flush(); return ms.ToArray();
    }

    public string Hash() => Convert.ToHexString(SHA256.HashData(CanonicalBytes()));
}

/// <summary>The fixture outcomes used by the demo scenes and tests (all labelled).</summary>
public static class WarOutcomeFixtures
{
    static WarOutcome Make(OutcomeKind k, Side victor, LeaderFate fate, WarDomain d, int tier, ulong seed, WarRecord r) =>
        new(WarOutcome.Label, k, victor, fate, d, tier, PlayerCultureId: 1, OpponentCultureId: 2, r, seed);

    public static WarOutcome Execution => Make(OutcomeKind.Execution, Side.Player, LeaderFate.Executed, WarDomain.Land, 3, 11, new(412, 9, 18_400, 42_100, 6));
    public static WarOutcome Surrender => Make(OutcomeKind.Surrender, Side.Player, LeaderFate.Surrendered, WarDomain.Land, 1, 12, new(230, 5, 7_900, 15_300, 3));
    public static WarOutcome Capture => Make(OutcomeKind.Capture, Side.Player, LeaderFate.Captured, WarDomain.Land, 4, 13, new(655, 14, 31_000, 52_700, 9));
    public static WarOutcome Peace => Make(OutcomeKind.NegotiatedPeace, Side.Player, LeaderFate.Alive, WarDomain.Land, 5, 14, new(1_480, 21, 88_000, 91_500, 2));
    public static WarOutcome Inconclusive => Make(OutcomeKind.Inconclusive, Side.None, LeaderFate.Alive, WarDomain.Land, 6, 15, new(2_190, 17, 120_400, 118_900, 0));
    public static WarOutcome Naval => Make(OutcomeKind.NavalVictory, Side.Player, LeaderFate.Fled, WarDomain.Naval, 5, 16, new(300, 4, 6_200, 11_800, 1));
    public static WarOutcome Siege => Make(OutcomeKind.SiegeVictory, Side.Player, LeaderFate.Captured, WarDomain.Siege, 3, 17, new(540, 3, 12_500, 20_100, 4));
    public static WarOutcome Defeat => Make(OutcomeKind.Defeat, Side.Opponent, LeaderFate.Alive, WarDomain.Land, 6, 18, new(820, 11, 64_000, 30_200, -5));

    public static IReadOnlyList<(string name, WarOutcome outcome)> All => new[]
    {
        ("execution", Execution), ("surrender", Surrender), ("capture", Capture), ("peace", Peace),
        ("inconclusive", Inconclusive), ("naval", Naval), ("siege", Siege), ("defeat", Defeat),
    };
}
