using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;

namespace Gfx.Tests;

public class EffectLoadTests
{
    [GfxFact]
    public void EveryRegisteredEffectCompilesAndLinks()
    {
        var results = TestGfx.Host.Invoke(_ => TestGfx.Host.Effects.WarmupAll());
        Assert.NotEmpty(results);
        foreach (var (name, err) in results) Assert.True(err == null, $"{name}: {err}");
    }

    [GfxFact]
    public void EngineShipsItsBuiltinSet()
    {
        var names = TestGfx.Host.Effects.Names;
        foreach (var n in new[] { EffectNames.InstancedLit, EffectNames.InstancedDepth, EffectNames.SkinnedLit, EffectNames.SkinnedDepth, EffectNames.Sky, EffectNames.Post })
            Assert.Contains(n, names);
    }

    [Fact]
    public void MgfxBytesHaveV11GlHeaderAndTrailer()
    {
        var bytes = new BuiltinEffects().CreateEffects().First().BuildBytes();
        Assert.Equal("MGFX"u8.ToArray(), bytes[..4]);
        Assert.Equal(11, bytes[4]); Assert.Equal(0, bytes[5]);
        Assert.Equal("MGFX"u8.ToArray(), bytes[^4..]);
    }

    [Fact]
    public void BuilderMapsParametersToStageArrays()
    {
        var b = new EffectBuilder("t").Matrix("M", Stage.Vertex).Vec4("A", Stage.Both).Vec4("B", Stage.Pixel)
            .Attribute("vec4 a_pos", Microsoft.Xna.Framework.Graphics.VertexElementUsage.Position, 0)
            .VertexMain("gl_Position = M_mul(a_pos) + A;").PixelMain("gl_FragColor = A + B;");
        b.BuildBytes();
        Assert.Contains("uniform vec4 vs_uniforms_vec4[5];", b.VertexGlsl);
        Assert.Contains("#define A vs_uniforms_vec4[4]", b.VertexGlsl);
        Assert.Contains("uniform vec4 ps_uniforms_vec4[2];", b.PixelGlsl);
        Assert.Contains("#define B ps_uniforms_vec4[1]", b.PixelGlsl);
        Assert.Contains("posFixup", b.VertexGlsl);
    }
}
