using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Mgfx;

/// <summary>Which shader stage(s) a uniform lives in.</summary>
[Flags]
public enum Stage { Vertex = 1, Pixel = 2, Both = 3 }

/// <summary>
/// Small DSL over <see cref="MgfxGl"/>: declare parameters, textures, attributes and varyings, write the
/// two <c>main()</c> bodies, get an MGFX <see cref="Effect"/>. GLSL 1.20, one technique, one pass.
/// <para>Generated names you can use in GLSL:</para>
/// <list type="bullet">
/// <item>vec4 parameter <c>Foo</c> → macro <c>Foo</c> (a vec4) in every stage it is declared in.</item>
/// <item>matrix parameter <c>Bar</c> → function <c>vec4 Bar_mul(vec4 v)</c> (row vector times matrix, XNA convention).</item>
/// <item>texture <c>Tex</c> → <c>uniform sampler2D Tex;</c> in BOTH stages. It is listed only on the pixel stage in
/// MGFX (critic's vertex-texture-fetch workaround, scratchpad/m7gfx/probe-critic/vtf/): read it in the vertex stage
/// with <c>texture2DLod</c>. The pixel shader must reference the sampler (the builder adds a no-op read).</item>
/// </list>
/// The vertex stage gets the MojoShader posFixup epilogue appended automatically: write D3D-style clip space.
/// </summary>
public sealed class EffectBuilder
{
    readonly record struct P(string Name, bool Matrix, Stage Stage);
    readonly List<P> _params = new();
    readonly List<(string name, int slot)> _textures = new();
    readonly List<(string decl, VertexElementUsage usage, int index)> _attrs = new();
    readonly StringBuilder _varyings = new(), _vsLib = new(), _psLib = new();
    string _vsMain = "", _psMain = "";

    public string Name { get; }
    public EffectBuilder(string name) { Name = name; }

    public EffectBuilder Vec4(string name, Stage stage) { _params.Add(new P(name, false, stage)); return this; }
    public EffectBuilder Matrix(string name, Stage stage) { _params.Add(new P(name, true, stage)); return this; }
    /// <summary>Declares a 2D texture bound to sampler <paramref name="slot"/> (gd.Textures[slot]/gd.SamplerStates[slot]).</summary>
    public EffectBuilder Texture(string name, int slot) { _textures.Add((name, slot)); return this; }
    /// <summary><paramref name="glslDecl"/> like <c>"vec4 a_pos"</c>; bound to vertex element (usage, index).</summary>
    public EffectBuilder Attribute(string glslDecl, VertexElementUsage usage, int index) { _attrs.Add((glslDecl, usage, index)); return this; }
    /// <summary>Varying declarations without the keyword, e.g. <c>"vec3 v_wpos; vec4 v_col;"</c>.</summary>
    public EffectBuilder Varyings(string decls) { _varyings.Append(decls).Append(' '); return this; }
    public EffectBuilder VertexLib(string glsl) { _vsLib.AppendLine(glsl); return this; }
    public EffectBuilder PixelLib(string glsl) { _psLib.AppendLine(glsl); return this; }
    public EffectBuilder VertexMain(string body) { _vsMain = body; return this; }
    public EffectBuilder PixelMain(string body) { _psMain = body; return this; }

    public bool HasParam(string name) { foreach (var p in _params) if (p.Name == name) return true; return false; }

    public string VertexGlsl { get; private set; } = "";
    public string PixelGlsl { get; private set; } = "";

    public byte[] BuildBytes()
    {
        var m = new MgfxGl { SourceName = Name + ".glsl" };
        var vsMembers = new List<int>(); var psMembers = new List<int>();
        var paramIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in _params)
        {
            if (paramIndex.ContainsKey(p.Name)) throw new InvalidOperationException($"{Name}: duplicate parameter {p.Name}");
            int ix = p.Matrix
                ? m.AddParam(p.Name, EffectParameterClass.Matrix, EffectParameterType.Single, 4, 4)
                : m.AddParam(p.Name, EffectParameterClass.Vector, EffectParameterType.Single, 1, 4);
            paramIndex[p.Name] = ix;
            if ((p.Stage & Stage.Vertex) != 0) vsMembers.Add(ix);
            if ((p.Stage & Stage.Pixel) != 0) psMembers.Add(ix);
        }
        var texParams = new List<(string name, int slot, int param)>();
        foreach (var (name, slot) in _textures)
            texParams.Add((name, slot, m.AddParam(name, EffectParameterClass.Object, EffectParameterType.Texture2D, 0, 0)));

        var vs = new MgfxGl.Sh { Vertex = true };
        var ps = new MgfxGl.Sh { Vertex = false };
        int vsN = 0, psN = 0;
        if (vsMembers.Count > 0) { var (ci, n) = m.AddCBuffer("vs_uniforms_vec4", vsMembers); vs.CBufs.Add(ci); vsN = n; }
        if (psMembers.Count > 0) { var (ci, n) = m.AddCBuffer("ps_uniforms_vec4", psMembers); ps.CBufs.Add(ci); psN = n; }
        foreach (var (name, slot, param) in texParams) ps.Samplers.Add(new MgfxGl.Sampler { GlslName = name, Param = param, TextureSlot = slot });
        foreach (var (decl, usage, index) in _attrs)
            vs.Attrs.Add(new MgfxGl.Attr { Name = decl.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1], Usage = usage, Index = index });

        string Header(bool vertex)
        {
            var sb = new StringBuilder("#version 120\n");
            string arr = vertex ? "vs_uniforms_vec4" : "ps_uniforms_vec4";
            int n = vertex ? vsN : psN;
            if (n > 0) sb.Append("uniform vec4 ").Append(arr).Append('[').Append(n).Append("];\n");
            if (vertex) sb.Append("uniform vec4 posFixup;\n");
            foreach (var (name, _) in _textures) sb.Append("uniform sampler2D ").Append(name).Append(";\n");
            if (vertex) foreach (var (decl, _, _) in _attrs) sb.Append("attribute ").Append(decl).Append(";\n");
            foreach (var v in _varyings.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                sb.Append("varying ").Append(v).Append(";\n");
            int slot = 0;
            foreach (var p in _params)
            {
                if ((p.Stage & (vertex ? Stage.Vertex : Stage.Pixel)) == 0) continue;
                if (p.Matrix)
                {
                    sb.Append($"vec4 {p.Name}_mul(vec4 v) {{ return vec4(dot(v, {arr}[{slot}]), dot(v, {arr}[{slot + 1}]), dot(v, {arr}[{slot + 2}]), dot(v, {arr}[{slot + 3}])); }}\n");
                    slot += 4;
                }
                else { sb.Append($"#define {p.Name} {arr}[{slot}]\n"); slot += 1; }
            }
            return sb.ToString();
        }

        // No-op reads keep every sampler referenced in the pixel program (the VTF workaround binds through it).
        var touch = new StringBuilder();
        foreach (var (name, _) in _textures) touch.Append($"    gl_FragColor += 0.0 * texture2D({name}, vec2(0.5));\n");

        VertexGlsl = Header(true) + _vsLib + "void main() {\n" + _vsMain + "\n" + MgfxGl.VsEpilogue + "\n}\n";
        PixelGlsl = Header(false) + _psLib + "void main() {\n" + _psMain + "\n" + touch + "}\n";
        vs.Glsl = VertexGlsl; ps.Glsl = PixelGlsl;
        m.Shaders.Add(vs); m.Shaders.Add(ps);
        m.Techniques.Add(("Main", new List<MgfxGl.Pass> { new() { Name = "P0", Vs = 0, Ps = 1 } }));
        return m.Build(StableKey(Name + "\n" + VertexGlsl + "\n" + PixelGlsl));
    }

    public Effect Build(GraphicsDevice gd) => new(gd, BuildBytes()) { Name = Name };

    /// <summary>FNV-1a over the text: the per-device EffectCache key (must differ between distinct effects).</summary>
    static int StableKey(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) { h ^= c; h *= 16777619; }
        return unchecked((int)h);
    }
}
