using FsCheck.Xunit;

namespace Sim.Tests;

// T0.1 scaffold smoke tests: prove the xUnit + FsCheck stack (D-003) is wired.
// Real acceptance tests arrive with their packets (T0.2+).
public class ScaffoldSmokeTests
{
    [Fact]
    public void XunitIsWired() => Assert.True(true);

    [Property]
    public bool FsCheckIsWired(long a, long b) => a + b == b + a;
}
