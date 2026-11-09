using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Diagnostics;

/// <summary>Machine + GL facts for reports. GL strings via MonoGame's internal GL binding (reflection, as in the recon probe).</summary>
public sealed record SysInfo(
    string GlVendor, string GlRenderer, string GlVersion, string GlslVersion, string MonoGameVersion,
    GraphicsProfile Profile, int CpuCount, long TotalMemoryBytes, string Os, string Runtime)
{
    public bool IsSoftware => GlRenderer.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase)
                              || GlRenderer.Contains("softpipe", StringComparison.OrdinalIgnoreCase)
                              || GlRenderer.Contains("swrast", StringComparison.OrdinalIgnoreCase);
    public bool IsIntelIGpu => GlRenderer.Contains("Intel", StringComparison.OrdinalIgnoreCase) || GlVendor.Contains("Intel", StringComparison.OrdinalIgnoreCase);
    public bool IsNvidia => GlRenderer.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || GlVendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);

    /// <summary>Label to put on every timing this process measures.</summary>
    public string TimingLabel => IsSoftware ? "SOFTWARE RENDERING (llvmpipe) - not Victus performance" : $"GPU: {GlRenderer}";

    public IReadOnlyList<string> Warnings
    {
        get
        {
            var w = new List<string>();
            if (IsIntelIGpu) w.Add("IntelIGPU: rendering on the Intel integrated GPU — the RTX dGPU is NOT in use (Optimus). Set Windows Graphics settings → High performance, or NVIDIA Control Panel → preferred GPU.");
            if (IsSoftware) w.Add("SOFTWARE RENDERING: Mesa software rasterizer; timings say nothing about real GPU performance.");
            if (Profile != GraphicsProfile.HiDef) w.Add("GraphicsProfile is not HiDef.");
            return w;
        }
    }

    public static SysInfo Collect(GraphicsDevice gd)
    {
        string Gl(int name)
        {
            try
            {
                var asm = typeof(Game).Assembly;
                var glT = asm.GetType("MonoGame.OpenGL.GL"); var snT = asm.GetType("MonoGame.OpenGL.StringName");
                var gs = glT?.GetMethod("GetString", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
                if (gs == null || snT == null) return "unknown";
                return gs.Invoke(null, new[] { Enum.ToObject(snT, name) }) as string ?? "unknown";
            }
            catch (Exception) { return "unknown"; }
        }
        var mg = typeof(Game).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                 ?? typeof(Game).Assembly.GetName().Version?.ToString() ?? "?";
        return new SysInfo(Gl(0x1F00), Gl(0x1F01), Gl(0x1F02), Gl(0x8B8C), mg, gd.GraphicsProfile,
            Environment.ProcessorCount, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription);
    }

    public string ToText()
    {
        var sb = new StringBuilder();
        void L(string k, object v) => sb.Append(k.PadRight(14)).Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\n');
        L("GL_VENDOR", GlVendor); L("GL_RENDERER", GlRenderer); L("GL_VERSION", GlVersion); L("GLSL", GlslVersion);
        L("MonoGame", MonoGameVersion); L("Profile", Profile); L("CPUs", CpuCount);
        L("RAM", string.Create(CultureInfo.InvariantCulture, $"{TotalMemoryBytes / (1024.0 * 1024 * 1024):F1} GiB visible to .NET"));
        L("OS", Os); L("Runtime", Runtime);
        L("Flags", (IsSoftware ? "Software " : "") + (IsIntelIGpu ? "IntelIGPU " : "") + (IsNvidia ? "NVIDIA" : ""));
        foreach (var w in Warnings) sb.Append("WARNING: ").Append(w).Append('\n');
        return sb.ToString();
    }
}
