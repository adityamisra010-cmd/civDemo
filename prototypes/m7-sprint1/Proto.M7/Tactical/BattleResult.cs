using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.Tactical;

/// <summary>
/// Aggregation of one formation after the battle (O-10 a: entities collapse back into formation counts).
/// Exact conservation (no epsilon): Start = Survivors + Killed + Surrendered, where
/// Survivors = ActiveAtEnd + Escaped (Escaped = RoutedEscaped + DisengagedEscaped + orderly-retreat escapes).
/// Killed and Surrendered are the personnel deltas a future Ledger.Transfer would move (integration is a dependency:
/// ADR-055 tables, O-35 immortality) — nothing is transferred here.
/// </summary>
public sealed record FormationResult(
    long FormationId, int Side, Domain Domain, int FamilyKey,
    long Start, long Survivors, long Killed, long Surrendered,
    long ActiveAtEnd, long Escaped, long RoutedEscaped, long DisengagedEscaped,
    long MoraleEnd, long DamageDealt, long DamageTaken, long AttacksMade)
{
    public bool Conserved => Start == Survivors + Killed + Surrendered;
    /// <summary>Personnel leaving the formation's count (killed + captured).</summary>
    public long PersonnelLoss => Killed + Surrendered;
}

/// <summary>Battle outcome — the shape AutoResolver (Slice E) also produces.</summary>
/// <param name="Side">The eliminated / disengaged side, or -1 when both (or none).</param>
/// <param name="HoldsField">The side left with Active entities on the field, or -1.</param>
public sealed record BattleResult(
    ResolverKind Resolver, BattleEndReason Reason, int Side, int HoldsField,
    long Pulses, long EndMs, long EventsExecuted, long EventsDropped,
    string TraceHash, string StateHash, FormationResult[] Formations)
{
    /// <summary>Canonical hash of the result (no trace/state hash inside; used for equality across resolvers' runs).</summary>
    public string ResultHash()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L((long)Resolver); L((long)Reason); L(Side); L(HoldsField); L(Pulses); L(EndMs);
        L(Formations.Length);
        foreach (var f in Formations)
        {
            L(f.FormationId); L(f.Side); L((long)f.Domain); L(f.FamilyKey); L(f.Start); L(f.Survivors); L(f.Killed); L(f.Surrendered);
            L(f.ActiveAtEnd); L(f.Escaped); L(f.RoutedEscaped); L(f.DisengagedEscaped); L(f.MoraleEnd); L(f.DamageDealt); L(f.DamageTaken); L(f.AttacksMade);
        }
        return Convert.ToHexString(h.GetHashAndReset());
    }
}

/// <summary>A battle resolver. The choice between resolvers is explicit (O-1: auto optional, never the default).</summary>
public interface IBattleResolver
{
    ResolverKind Kind { get; }
    BattleResult Resolve(BattleSetup setup, IFormationController side0, IFormationController side1);
}

public sealed class TacticalResolver : IBattleResolver
{
    public static readonly TacticalResolver Instance = new();
    public ResolverKind Kind => ResolverKind.Tactical;
    public BattleResult Resolve(BattleSetup setup, IFormationController side0, IFormationController side1)
        => new TacticalBattle(setup, side0, side1).Run();
}

/// <summary>
/// Resolver selection (spec §4 Naval / O-1): the resolver is an EXPLICIT parameter that defaults to Tactical. AutoResolver is
/// used only when explicitly requested AND supplied; there is no implicit fallback to auto for any domain (a naval battle
/// with the default resolver goes to the tactical engine, which reports that the naval module is not built — it is never
/// silently auto-resolved).
/// </summary>
public static class BattleResolution
{
    public static BattleResult Resolve(BattleSetup setup, IFormationController side0, IFormationController side1,
                                       ResolverKind resolver = ResolverKind.Tactical, IBattleResolver? auto = null)
    {
        switch (resolver)
        {
            case ResolverKind.Tactical:
                return TacticalResolver.Instance.Resolve(setup, side0, side1);
            case ResolverKind.Auto:
                if (auto is null) throw new InvalidOperationException("AutoResolver requested but none supplied (it is never implicit)");
                if (auto.Kind != ResolverKind.Auto) throw new ArgumentException("supplied resolver is not an AutoResolver", nameof(auto));
                return auto.Resolve(setup, side0, side1);
            default:
                throw new ArgumentOutOfRangeException(nameof(resolver));
        }
    }
}
