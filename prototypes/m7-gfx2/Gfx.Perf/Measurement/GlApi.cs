using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;

namespace Gfx.Perf.Measurement;

/// <summary>
/// The few raw GL entry points the harness needs (timer queries, memory info, strings), resolved through
/// <c>SDL_GL_GetProcAddress</c> from the SDL2 library MonoGame already loaded. Must be created and used on the
/// thread that owns the current GL context (the game thread). None of these calls change GL state that MonoGame
/// caches (queries and reads only).
/// </summary>
public sealed unsafe class GlApi
{
    public const uint GL_TIMESTAMP = 0x8E28, GL_QUERY_RESULT = 0x8866, GL_QUERY_RESULT_AVAILABLE = 0x8867, GL_QUERY_COUNTER_BITS = 0x8864;
    public const uint GL_VENDOR = 0x1F00, GL_RENDERER = 0x1F01, GL_VERSION = 0x1F02, GL_EXTENSIONS = 0x1F03, GL_SHADING_LANGUAGE_VERSION = 0x8B8C;
    public const uint GL_NUM_EXTENSIONS = 0x821D;
    // GL_NVX_gpu_memory_info (values in KiB)
    public const uint GL_GPU_MEMORY_INFO_DEDICATED_VIDMEM_NVX = 0x9047, GL_GPU_MEMORY_INFO_TOTAL_AVAILABLE_MEMORY_NVX = 0x9048,
        GL_GPU_MEMORY_INFO_CURRENT_AVAILABLE_VIDMEM_NVX = 0x9049, GL_GPU_MEMORY_INFO_EVICTION_COUNT_NVX = 0x904A,
        GL_GPU_MEMORY_INFO_EVICTED_MEMORY_NVX = 0x904B;
    // GL_ATI_meminfo (4 ints each, KiB: total free, largest free block, total aux free, largest aux free)
    public const uint GL_VBO_FREE_MEMORY_ATI = 0x87FB, GL_TEXTURE_FREE_MEMORY_ATI = 0x87FC, GL_RENDERBUFFER_FREE_MEMORY_ATI = 0x87FD;

    readonly delegate* unmanaged<int, uint*, void> _genQueries;
    readonly delegate* unmanaged<int, uint*, void> _deleteQueries;
    readonly delegate* unmanaged<uint, uint, void> _queryCounter;
    readonly delegate* unmanaged<uint, uint, int*, void> _getQueryObjectiv;
    readonly delegate* unmanaged<uint, uint, ulong*, void> _getQueryObjectui64v;
    readonly delegate* unmanaged<uint, uint, int*, void> _getQueryiv;
    readonly delegate* unmanaged<uint, int*, void> _getIntegerv;
    readonly delegate* unmanaged<uint, byte*> _getString;
    readonly delegate* unmanaged<uint, uint, byte*> _getStringi;
    readonly delegate* unmanaged<uint> _getError;
    readonly delegate* unmanaged<void> _flush;
    readonly delegate* unmanaged<void> _finish;
    readonly delegate* unmanaged<int, uint*, void> _deleteFramebuffers;
    readonly delegate* unmanaged<int, uint*, void> _deleteRenderbuffers;

    public bool HasTimerQueries { get; }
    public IReadOnlyList<string> Extensions { get; }
    public bool HasExtension(string name) => Extensions.Contains(name, StringComparer.Ordinal);

    GlApi(IntPtr getProcAddress)
    {
        var gp = (delegate* unmanaged<byte*, IntPtr>)getProcAddress;
        IntPtr P(string name)
        {
            var b = Encoding.ASCII.GetBytes(name + "\0");
            fixed (byte* p = b) return gp(p);
        }
        _genQueries = (delegate* unmanaged<int, uint*, void>)P("glGenQueries");
        _deleteQueries = (delegate* unmanaged<int, uint*, void>)P("glDeleteQueries");
        _queryCounter = (delegate* unmanaged<uint, uint, void>)P("glQueryCounter");
        _getQueryObjectiv = (delegate* unmanaged<uint, uint, int*, void>)P("glGetQueryObjectiv");
        _getQueryObjectui64v = (delegate* unmanaged<uint, uint, ulong*, void>)P("glGetQueryObjectui64v");
        _getQueryiv = (delegate* unmanaged<uint, uint, int*, void>)P("glGetQueryiv");
        _getIntegerv = (delegate* unmanaged<uint, int*, void>)P("glGetIntegerv");
        _getString = (delegate* unmanaged<uint, byte*>)P("glGetString");
        _getStringi = (delegate* unmanaged<uint, uint, byte*>)P("glGetStringi");
        _getError = (delegate* unmanaged<uint>)P("glGetError");
        _flush = (delegate* unmanaged<void>)P("glFlush");
        _finish = (delegate* unmanaged<void>)P("glFinish");
        _deleteFramebuffers = (delegate* unmanaged<int, uint*, void>)P("glDeleteFramebuffers");
        _deleteRenderbuffers = (delegate* unmanaged<int, uint*, void>)P("glDeleteRenderbuffers");
        if (_getIntegerv == null || _getString == null || _getError == null || _flush == null || _finish == null)
            throw new InvalidOperationException("core GL 1.1 entry points not resolvable through SDL_GL_GetProcAddress");
        Extensions = ReadExtensions();
        bool fns = _genQueries != null && _deleteQueries != null && _queryCounter != null && _getQueryObjectiv != null && _getQueryObjectui64v != null;
        HasTimerQueries = fns && (HasExtension("GL_ARB_timer_query") || VersionAtLeast(3, 3)) && TimestampBits() > 0;
        DrainErrors();
    }

    /// <summary>Resolves the entry points on the current GL thread. Returns null (with a reason) when that is impossible.</summary>
    public static GlApi? TryCreate(out string? failure)
    {
        try
        {
            var gpa = SdlProc("SDL_GL_GetProcAddress");
            if (gpa == IntPtr.Zero) { failure = "SDL_GL_GetProcAddress not found in the SDL2 library MonoGame loaded"; return null; }
            failure = null;
            return new GlApi(gpa);
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; return null; }
    }

    /// <summary>An export of the SDL2 library MonoGame loaded (handle from MonoGame's internal <c>Sdl.NativeLibrary</c>).</summary>
    public static IntPtr SdlProc(string name)
    {
        var sdl = typeof(Game).Assembly.GetType("Sdl");
        var f = sdl?.GetField("NativeLibrary", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (f?.GetValue(null) is not IntPtr h || h == IntPtr.Zero) return IntPtr.Zero;
        return NativeLibrary.TryGetExport(h, name, out var p) ? p : IntPtr.Zero;
    }

    public string GetString(uint name)
    {
        var p = _getString(name);
        return p == null ? "" : Marshal.PtrToStringAnsi((IntPtr)p) ?? "";
    }

    List<string> ReadExtensions()
    {
        var list = new List<string>();
        int n = 0;
        if (_getStringi != null)
        {
            _getIntegerv(GL_NUM_EXTENSIONS, &n);
            if (_getError() == 0)
                for (uint i = 0; i < (uint)n; i++)
                {
                    var p = _getStringi(GL_EXTENSIONS, i);
                    if (p != null) list.Add(Marshal.PtrToStringAnsi((IntPtr)p) ?? "");
                }
        }
        if (list.Count == 0) // legacy path: one space-separated string
            list.AddRange(GetString(GL_EXTENSIONS).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        DrainErrors();
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    bool VersionAtLeast(int major, int minor)
    {
        var v = GetString(GL_VERSION);
        int i = 0; while (i < v.Length && !char.IsDigit(v[i])) i++;
        int ma = 0, mi = 0;
        while (i < v.Length && char.IsDigit(v[i])) ma = ma * 10 + (v[i++] - '0');
        if (i < v.Length && v[i] == '.') { i++; while (i < v.Length && char.IsDigit(v[i])) mi = mi * 10 + (v[i++] - '0'); }
        return ma > major || (ma == major && mi >= minor);
    }

    /// <summary>GL_QUERY_COUNTER_BITS for GL_TIMESTAMP (0 = timestamps unsupported).</summary>
    public int TimestampBits()
    {
        if (_getQueryiv == null) return 0;
        int bits = 0;
        _getQueryiv(GL_TIMESTAMP, GL_QUERY_COUNTER_BITS, &bits);
        if (_getError() != 0) return 0;
        return bits;
    }

    public void DrainErrors() { for (int i = 0; i < 16 && _getError() != 0; i++) { } }
    public uint GetError() => _getError();
    public void Flush() => _flush();
    public void Finish() => _finish();

    /// <summary>glDeleteFramebuffers for one name (no-op when the entry point is missing).</summary>
    public void DeleteFramebuffer(int id) { if (_deleteFramebuffers == null || id == 0) return; uint u = (uint)id; _deleteFramebuffers(1, &u); }
    /// <summary>glDeleteRenderbuffers for one name (no-op when the entry point is missing).</summary>
    public void DeleteRenderbuffer(int id) { if (_deleteRenderbuffers == null || id == 0) return; uint u = (uint)id; _deleteRenderbuffers(1, &u); }

    public void GenQueries(Span<uint> ids) { fixed (uint* p = ids) _genQueries(ids.Length, p); }
    public void DeleteQueries(ReadOnlySpan<uint> ids) { fixed (uint* p = ids) _deleteQueries(ids.Length, p); }
    public void Timestamp(uint id) => _queryCounter(id, GL_TIMESTAMP);
    public bool ResultAvailable(uint id) { int a = 0; _getQueryObjectiv(id, GL_QUERY_RESULT_AVAILABLE, &a); return a != 0; }
    public ulong Result(uint id) { ulong r = 0; _getQueryObjectui64v(id, GL_QUERY_RESULT, &r); return r; }

    /// <summary>
    /// A memory-info query (GL_NVX_gpu_memory_info / GL_ATI_meminfo, KiB) read as UNSIGNED 32-bit, so a value above
    /// 2 TiB (software rasterizers report system-memory figures) is not shown as a negative number. -1 = error.
    /// </summary>
    public long GetKb(uint pname, int index = 0) => GetIntegers(pname, Math.Max(1, index + 1)) is { } v ? (long)(uint)v[index] : -1;

    /// <summary>glGetIntegerv for <paramref name="count"/> ints; null when the query raised a GL error.</summary>
    public int[]? GetIntegers(uint pname, int count = 1)
    {
        DrainErrors();
        var r = new int[Math.Max(4, count)];
        fixed (int* p = r) _getIntegerv(pname, p);
        if (_getError() != 0) { DrainErrors(); return null; }
        return r[..count];
    }
}
