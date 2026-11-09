using Gfx.Engine.Hosting;

namespace Gfx.Tests;

/// <summary>
/// [GfxFact] = a Fact that SKIPS (not fails) when no GL device can be created. The probe creates the process-wide
/// headless host (SDL offscreen + Mesa software GL on Linux without a display). Use for every test that renders:
/// <code>[GfxFact] public void X() => TestGfx.Host.Invoke(gd => { ... all GPU work here ... });</code>
/// </summary>
public sealed class GfxFactAttribute : FactAttribute
{
    public GfxFactAttribute() { if (!TestGfx.Available) Skip = "no graphics device: " + TestGfx.Reason; }
}

public static class TestGfx
{
    static readonly Lazy<(GfxHost? host, string? reason)> State = new(() =>
    {
        GfxHost.TryGetShared(out var h, out var r, typeof(TestGfx).Assembly);
        return (h, r);
    });
    public static bool Available => State.Value.host != null;
    public static string? Reason => State.Value.reason;
    public static GfxHost Host => State.Value.host ?? throw new InvalidOperationException("no device: " + Reason);
}
