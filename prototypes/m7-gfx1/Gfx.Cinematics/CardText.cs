using Gfx.Engine.Effects;
using Gfx.Engine.Mgfx;
using Gfx.Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Cinematics;

/// <summary>Effect names shipped by this lane.</summary>
public static class CineEffectNames
{
    /// <summary>VertexPNC + InstanceTransform, UNLIT emissive (sRGB vertex colour × tint.rgb), no fog. For the war-record card.</summary>
    public const string Unlit = "CineUnlit";
}

public sealed class CineEffects : IEffectSource
{
    public IEnumerable<EffectBuilder> CreateEffects()
    {
        yield return new EffectBuilder(CineEffectNames.Unlit)
            .Attribute("vec4 a_pos", VertexElementUsage.Position, 0)
            .Attribute("vec4 a_col", VertexElementUsage.Color, 0)
            .AddInstanceTransform()
            .Matrix("ViewProj", Stage.Vertex)
            .Varyings("vec4 v_col; vec3 v_tint;")
            .VertexMain(@"
    vec3 wp = instPos(vec4(a_pos.xyz, 1.0));
    v_col = a_col; v_tint = i_tint.rgb;
    gl_Position = ViewProj_mul(vec4(wp, 1.0));")
            .PixelMain(@"
    gl_FragColor = vec4(pow(max(v_col.rgb, vec3(0.0)), vec3(2.2)) * v_tint, 1.0);");
    }
}

/// <summary>
/// SpriteFont-free text: a 5×7 bitmap font emitted as one quad per lit cell (VertexPNC, panel space x right,
/// y up, z = 0). Placeholder for the war-record card; the production path is a proper font atlas.
/// </summary>
public static class GlyphText
{
    static readonly Dictionary<char, string[]> Font = new()
    {
        ['A'] = new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        ['B'] = new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
        ['C'] = new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " },
        ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
        ['E'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
        ['F'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
        ['G'] = new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ####" },
        ['H'] = new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        ['I'] = new[] { " ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        ['J'] = new[] { "  ###", "   # ", "   # ", "   # ", "   # ", "#  # ", " ##  " },
        ['K'] = new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
        ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
        ['M'] = new[] { "#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #" },
        ['N'] = new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" },
        ['O'] = new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
        ['Q'] = new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #" },
        ['R'] = new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
        ['S'] = new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
        ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
        ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        ['V'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
        ['W'] = new[] { "#   #", "#   #", "#   #", "# # #", "# # #", "## ##", "#   #" },
        ['X'] = new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" },
        ['Y'] = new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " },
        ['Z'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },
        ['0'] = new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
        ['1'] = new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        ['2'] = new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" },
        ['3'] = new[] { "#####", "   # ", "  #  ", "   # ", "    #", "#   #", " ### " },
        ['4'] = new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
        ['5'] = new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
        ['6'] = new[] { "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### " },
        ['7'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
        ['8'] = new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
        ['9'] = new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
        ['-'] = new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
        ['+'] = new[] { "     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     " },
        ['/'] = new[] { "     ", "    #", "   # ", "  #  ", " #   ", "#    ", "     " },
        [':'] = new[] { "     ", "  #  ", "     ", "     ", "     ", "  #  ", "     " },
        ['.'] = new[] { "     ", "     ", "     ", "     ", "     ", "     ", "  #  " },
        [','] = new[] { "     ", "     ", "     ", "     ", "     ", "  #  ", " #   " },
        [' '] = new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " },
    };

    public static bool Supports(char c) => Font.ContainsKey(char.ToUpperInvariant(c));

    /// <summary>Appends quads for <paramref name="text"/> with its top-left at (x, y), cell size <paramref name="cell"/>.</summary>
    public static void Add(List<VertexPNC> v, List<int> ix, string text, float x, float y, float cell, Color col, float z = 0.001f)
    {
        float cx = x;
        foreach (char ch in text.ToUpperInvariant())
        {
            if (!Font.TryGetValue(ch, out var rows)) rows = Font[' '];
            for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                    if (rows[r][c] == '#') Quad(v, ix, cx + c * cell, y - r * cell, cell * 0.92f, cell * 0.92f, col, z);
            cx += 6 * cell;
        }
    }

    public static void Quad(List<VertexPNC> v, List<int> ix, float x, float y, float w, float h, Color col, float z)
    {
        int b = v.Count; var n = Vector3.Backward;
        v.Add(new VertexPNC(new Vector3(x, y, z), n, col)); v.Add(new VertexPNC(new Vector3(x + w, y, z), n, col));
        v.Add(new VertexPNC(new Vector3(x + w, y - h, z), n, col)); v.Add(new VertexPNC(new Vector3(x, y - h, z), n, col));
        ix.Add(b); ix.Add(b + 1); ix.Add(b + 2); ix.Add(b); ix.Add(b + 2); ix.Add(b + 3);
    }

    public static float Width(string text, float cell) => text.Length * 6 * cell - cell;
}
