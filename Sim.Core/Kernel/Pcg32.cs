namespace Sim.Core.Kernel;

/// <summary>
/// Canonical PCG32 XSH-RR (kernel contract §3.5), implemented from the reference
/// constants and operation sequence of pcg-c-basic (pcg_basic.c, Apache-2.0,
/// M.E. O'Neill, pcg-random.org). State advances by an LCG with the canonical
/// multiplier and a per-stream odd increment; output is xorshift-high then a
/// state-derived random rotation. Pure static functions over (state, inc) — the
/// state itself lives in WorldState rows (RngStreamRow), never here.
/// </summary>
public static class Pcg32
{
    public const ulong Multiplier = 6364136223846793005UL;

    /// <summary>
    /// Canonical pcg32_srandom_r seeding: state = 0, inc = (initSeq &lt;&lt; 1) | 1
    /// (always odd), advance, add initState, advance.
    /// </summary>
    public static (ulong State, ulong Inc) Seed(ulong initState, ulong initSeq)
    {
        ulong inc = (initSeq << 1) | 1UL;
        ulong state = 0UL;
        Next(ref state, inc);
        state += initState;
        Next(ref state, inc);
        return (state, inc);
    }

    /// <summary>Canonical pcg32_random_r: one 32-bit draw; advances state.</summary>
    public static uint Next(ref ulong state, ulong inc)
    {
        ulong oldState = state;
        state = oldState * Multiplier + inc;
        uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rot = (int)(oldState >> 59);
        return (xorShifted >> rot) | (xorShifted << (-rot & 31));
    }
}
