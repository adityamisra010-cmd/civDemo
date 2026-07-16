namespace Sim.Core.State;

/// <summary>
/// A conserved stock (law 1, ADR-004): a `long` in base units whose public surface
/// is GET-ONLY. The single mutation path is <see cref="UNSAFE_LedgerSet"/>, called
/// exclusively by Ledger.cs — enforced by the CI grep gate (any occurrence of the
/// method name outside Ledger.cs / this declaration fails the build). Stocks are
/// born at zero; all value enters and leaves the world through Ledger.Flow, so the
/// conservation identity (Σ stocks + Σ sunk − Σ sourced = 0) is checkable exactly.
/// </summary>
public readonly record struct Conserved
{
    public long Value { get; }

    private Conserved(long value) => Value = value;

    public static readonly Conserved Zero = default;

    /// <summary>
    /// LEDGER-ONLY mutation bypass. Never call this outside Sim.Core/Kernel/Ledger.cs —
    /// the banned-constructs gate greps for this name and fails CI on any other use.
    /// </summary>
    internal static Conserved UNSAFE_LedgerSet(long value) => new(value);

    /// <summary>
    /// SNAPSHOT-ONLY reconstitution (not mutation: the stock's flow counterweights
    /// load in the same stream, so the conservation identity is preserved). Never
    /// call this outside Sim.Core/Kernel/CanonicalSchema.cs — grep-gated like
    /// UNSAFE_LedgerSet.
    /// </summary>
    internal static Conserved FromSnapshot(long value) => new(value);
}
