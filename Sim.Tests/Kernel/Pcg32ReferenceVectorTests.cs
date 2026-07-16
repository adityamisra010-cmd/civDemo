using Sim.Core.Kernel;

namespace Sim.Tests.Kernel;

// T0.3 acceptance: known seed → known outputs. These are the PUBLISHED reference
// vectors of canonical PCG32 XSH-RR: the first six 32-bit values printed by the
// pcg-c-basic demo (pcg32-demo.c, deterministic mode) after
// pcg32_srandom_r(&rng, 42u, 54u) — "Round 1, Make some 32-bit numbers".
// Independently cross-checked against a from-scratch Python replication of the
// reference algorithm before pinning. These vectors freeze the stream: any future
// refactor of Pcg32 that changes outputs fails here.
public class Pcg32ReferenceVectorTests
{
    [Fact]
    public void Seed42Seq54_ProducesPublishedDemoOutputs()
    {
        (ulong state, ulong inc) = Pcg32.Seed(42UL, 54UL);

        uint[] expected =
        [
            0xa15c02b7u, 0x7b47f409u, 0xba1d3330u,
            0x83d2f293u, 0xbfa4784bu, 0xcbed606eu,
        ];

        foreach (uint want in expected)
            Assert.Equal(want, Pcg32.Next(ref state, inc));
    }

    [Fact]
    public void Seed_ProducesOddIncrement_Always()
    {
        // The increment must be odd for the LCG to be full-period (§3.5 / packet
        // constraint). (initSeq << 1) | 1 guarantees it; pin a few spot checks.
        foreach (ulong seq in new ulong[] { 0, 1, 54, ulong.MaxValue })
        {
            (_, ulong inc) = Pcg32.Seed(42UL, seq);
            Assert.Equal(1UL, inc & 1UL);
        }
    }
}
