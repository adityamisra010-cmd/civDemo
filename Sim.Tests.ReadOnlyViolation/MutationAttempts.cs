using Sim.Core.State;

namespace Sim.Tests.ReadOnlyViolation;

/// <summary>
/// T0.2 acceptance proof (m0-kernel-spec §4): mutation through
/// <see cref="IReadOnlyWorldState"/> does not compile. Every statement below is a
/// mutation attempt an agent might plausibly write; each fails with the compiler
/// error noted. scripts/check-readonly-proof.sh asserts the build fails with
/// exactly these diagnostics — in CI, a green run means the read-only contract
/// still holds by construction.
/// </summary>
public static class MutationAttempts
{
    public static void Attempt(IReadOnlyWorldState world)
    {
        // CS0200: Property or indexer 'IReadOnlyTable<RegionRow>.this[int]' cannot
        // be assigned to -- it is read only.
        world.Regions[0] = new RegionRow(new RegionId(99));

        // CS1061: 'IReadOnlyTable<RegionRow>' contains no definition for 'Add' —
        // the mutation surface simply does not exist on the view.
        world.Regions.Add(new RegionRow(new RegionId(99)));

        // CS1061: 'IReadOnlyTable<RegionRow>' contains no definition for 'Ref' —
        // no writable references are reachable from a read-only view.
        world.Regions.Ref(0).Id = new RegionId(99);
    }
}
