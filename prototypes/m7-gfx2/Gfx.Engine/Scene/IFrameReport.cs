namespace Gfx.Engine.Scene;

/// <summary>Optional (stage render2): a scene that can describe what its last frame drew (crowd levels, culling) for
/// <c>Gfx.Demo --bench</c> and <c>--capture</c> logs. PRESENTATION ONLY.</summary>
public interface IFrameReport
{
    string FrameReport();
}
