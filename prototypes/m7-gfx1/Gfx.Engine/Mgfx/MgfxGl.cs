// ATTRIBUTION: adapted from the M7 recon probe writer
//   scratchpad/m7gfx/probe-mono-probe/src/MgfxGl.cs (agent "mono-probe", 2026-11-04),
// which derived the MGFX v11 / profile 0 (OpenGL) layout from the decompiled MonoGame.Framework 3.8.5
// runtime (Effect.ReadEffect, Shader..ctor). Changes here: nullable annotations, namespace, effect
// source-name field. The byte layout is unchanged. If MonoGame bumps MGFXVersion past 11 this breaks at
// RUNTIME: the EffectLibrary load test (Gfx.Tests) is the tripwire.
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Mgfx;

/// <summary>Writes a MonoGame MGFX (v11, OpenGL) effect binary from hand-written GLSL 1.20.</summary>
public sealed class MgfxGl
{
    public sealed class Param { public string Name = ""; public EffectParameterClass Class; public EffectParameterType Type; public int Rows, Cols; }
    public sealed class CBuf { public string Name = ""; public int SizeBytes; public List<(int param, int offset)> Members = new(); }
    public sealed class Sampler { public string GlslName = ""; public int Param; public int TextureSlot; }
    public sealed class Attr { public string Name = ""; public VertexElementUsage Usage; public int Index; }
    public sealed class Sh { public bool Vertex; public string Glsl = ""; public List<int> CBufs = new(); public List<Sampler> Samplers = new(); public List<Attr> Attrs = new(); }
    public sealed class Pass { public string Name = ""; public int Vs, Ps; }

    public readonly List<Param> Params = new();
    public readonly List<CBuf> CBufs = new();
    public readonly List<Sh> Shaders = new();
    public readonly List<(string name, List<Pass> passes)> Techniques = new();
    public string SourceName = "gfx.glsl";

    public int AddParam(string name, EffectParameterClass c, EffectParameterType t, int rows, int cols)
    { Params.Add(new Param { Name = name, Class = c, Type = t, Rows = rows, Cols = cols }); return Params.Count - 1; }

    /// <summary>Lays out params into a vec4-array constant buffer; returns the cbuffer index and the vec4 count.</summary>
    public (int index, int vec4s) AddCBuffer(string glslArrayName, IReadOnlyList<int> members)
    {
        var cb = new CBuf { Name = glslArrayName };
        int slot = 0;
        foreach (int p in members)
        {
            cb.Members.Add((p, slot * 16));
            var pr = Params[p];
            slot += pr.Class == EffectParameterClass.Matrix ? pr.Cols : pr.Rows; // matrix uses ColumnCount vec4s (ConstantBuffer.SetParameter)
        }
        cb.SizeBytes = slot * 16;
        CBufs.Add(cb);
        return (CBufs.Count - 1, slot);
    }

    public byte[] Build(int effectKey)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms, Encoding.UTF8);
        w.Write(Encoding.ASCII.GetBytes("MGFX"));
        w.Write((byte)11); // version
        w.Write((byte)0);  // profile: OpenGL
        w.Write(effectKey);
        w.Write(CBufs.Count);
        foreach (var cb in CBufs)
        {
            w.Write(cb.Name); w.Write((short)cb.SizeBytes); w.Write(cb.Members.Count);
            foreach (var (p, off) in cb.Members) { w.Write(p); w.Write((ushort)off); }
        }
        w.Write(Shaders.Count);
        foreach (var s in Shaders)
        {
            w.Write(s.Vertex); w.Write(SourceName); w.Write(s.Vertex ? "main_vs" : "main_ps");
            byte[] code = Encoding.ASCII.GetBytes(s.Glsl);
            w.Write(code.Length); w.Write(code);
            w.Write((byte)s.Samplers.Count);
            foreach (var sm in s.Samplers)
            {
                w.Write((byte)0); w.Write((byte)sm.TextureSlot); w.Write((byte)sm.TextureSlot); w.Write(false);
                w.Write(sm.GlslName); w.Write((byte)sm.Param);
            }
            w.Write((byte)s.CBufs.Count); foreach (int c in s.CBufs) w.Write((byte)c);
            w.Write((byte)s.Attrs.Count);
            foreach (var a in s.Attrs) { w.Write(a.Name); w.Write((byte)a.Usage); w.Write((byte)a.Index); w.Write((short)0); }
        }
        w.Write(Params.Count);
        foreach (var p in Params)
        {
            w.Write((byte)p.Class); w.Write((byte)p.Type); w.Write(p.Name); w.Write(""); w.Write(0);
            w.Write((byte)p.Rows); w.Write((byte)p.Cols); w.Write(0); w.Write(0);
            if (p.Type is EffectParameterType.Bool or EffectParameterType.Int32 or EffectParameterType.Single)
                for (int i = 0; i < p.Rows * p.Cols; i++) w.Write(0f);
        }
        w.Write(Techniques.Count);
        foreach (var (name, passes) in Techniques)
        {
            w.Write(name); w.Write(0); w.Write(passes.Count);
            foreach (var ps in passes) { w.Write(ps.Name); w.Write(0); w.Write(ps.Vs); w.Write(ps.Ps); w.Write(false); w.Write(false); w.Write(false); }
        }
        w.Write(Encoding.ASCII.GetBytes("MGFX"));
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>MojoShader-equivalent epilogue: D3D clip-space -> GL (y flip on render targets, z [0,1] -> [-1,1]).</summary>
    public const string VsEpilogue = @"
    gl_Position.y = gl_Position.y * posFixup.y;
    gl_Position.xy += posFixup.zw * gl_Position.ww;
    gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;";
}
