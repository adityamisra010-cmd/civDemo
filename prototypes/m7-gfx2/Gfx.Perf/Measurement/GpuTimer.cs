namespace Gfx.Perf.Measurement;

/// <summary>GPU timestamps of one frame: marker names in issue order and their times (ns) relative to the first.</summary>
public sealed record GpuFrame(long FrameId, string[] Names, long[] TimesNs, bool Monotonic)
{
    /// <summary>Time (ns since frame start) of the first marker with this name, or -1.</summary>
    public long At(string name)
    {
        for (int i = 0; i < Names.Length; i++) if (Names[i] == name) return TimesNs[i];
        return -1;
    }
    public long TotalNs => TimesNs.Length == 0 ? 0 : TimesNs[^1];
}

/// <summary>
/// Ring of GL timestamp queries (<c>glQueryCounter(GL_TIMESTAMP)</c>, never nested TIME_ELAPSED). Each frame
/// owns one of <see cref="Slots"/> query sets; results are read back several frames later and only when
/// <c>GL_QUERY_RESULT_AVAILABLE</c> says so, so the CPU never waits for the GPU. A slot that is still not
/// available when the ring comes round again is DROPPED (counted, never waited for). <see cref="FinishAll"/>
/// (after the measured frames) waits with glFinish and collects the rest.
/// </summary>
public sealed class GpuTimer : IDisposable
{
    public const int Slots = 8, MaxMarkers = 48;
    readonly GlApi _gl;
    readonly uint[] _ids = new uint[Slots * MaxMarkers];
    readonly string[][] _names = new string[Slots][];
    readonly int[] _count = new int[Slots];
    readonly long[] _frame = new long[Slots];
    readonly bool[] _pending = new bool[Slots];
    int _cur = -1;
    readonly Queue<GpuFrame> _done = new();
    bool _disposed;

    /// <summary>Frames whose results were not available in time (ring wrapped) or never became available.</summary>
    public long Dropped { get; private set; }
    /// <summary>Markers that did not fit into a slot (more than <see cref="MaxMarkers"/> in one frame).</summary>
    public long Overflow { get; private set; }
    public int TimestampBits { get; }

    public GpuTimer(GlApi gl)
    {
        if (!gl.HasTimerQueries) throw new NotSupportedException("GL timer queries (GL_ARB_timer_query / GL 3.3) are not available");
        _gl = gl;
        _gl.GenQueries(_ids);
        for (int s = 0; s < Slots; s++) { _names[s] = new string[MaxMarkers]; _frame[s] = -1; }
        TimestampBits = gl.TimestampBits();
        // Prime every query object once so that RESULT_AVAILABLE is defined for each id.
        for (int i = 0; i < _ids.Length; i++) _gl.Timestamp(_ids[i]);
        _gl.Finish();
        _gl.DrainErrors();
    }

    public bool InFrame => _cur >= 0;

    /// <summary>Starts frame <paramref name="frameId"/> (strictly increasing) and records its "frame.begin" marker.</summary>
    public void BeginFrame(long frameId)
    {
        if (_cur >= 0) EndFrame();
        Poll();
        int s = (int)(frameId % Slots);
        if (_pending[s] && !TryHarvest(s)) { Dropped++; _pending[s] = false; }
        _cur = s; _count[s] = 0; _frame[s] = frameId; _pending[s] = true;
        Mark("frame.begin");
    }

    /// <summary>Records a timestamp marker in the current frame (no-op outside a frame).</summary>
    public void Mark(string name)
    {
        if (_cur < 0) return;
        int n = _count[_cur];
        if (n >= MaxMarkers) { Overflow++; return; }
        _names[_cur][n] = name;
        _gl.Timestamp(_ids[_cur * MaxMarkers + n]);
        _count[_cur] = n + 1;
    }

    /// <summary>Closes the current frame. <paramref name="flush"/> submits the queued commands (needed on drivers whose swap does not flush).</summary>
    public void EndFrame(bool flush = true)
    {
        if (_cur < 0) return;
        Mark("frame.end");
        _cur = -1;
        if (flush) _gl.Flush();
    }

    /// <summary>Collects every completed slot without waiting.</summary>
    public void Poll()
    {
        for (int k = 0; k < Slots; k++)
        {
            if (!_pending[k] || k == _cur) continue;
            TryHarvest(k);
        }
    }

    /// <summary>glFinish, then collect every pending slot (anything still unavailable is dropped).</summary>
    public void FinishAll()
    {
        if (_cur >= 0) EndFrame(flush: false);
        _gl.Finish();
        for (int k = 0; k < Slots; k++)
            if (_pending[k] && !TryHarvest(k)) { Dropped++; _pending[k] = false; }
    }

    bool TryHarvest(int s)
    {
        int n = _count[s];
        if (n == 0) { _pending[s] = false; return true; }
        int b = s * MaxMarkers;
        if (!_gl.ResultAvailable(_ids[b + n - 1])) return false; // timestamps complete in order: last ⇒ all
        var names = new string[n]; var t = new long[n];
        ulong t0 = _gl.Result(_ids[b]); bool mono = true; long prev = 0;
        for (int i = 0; i < n; i++)
        {
            ulong v = i == 0 ? t0 : _gl.Result(_ids[b + i]);
            long rel = unchecked((long)(v - t0));
            if (rel < prev) mono = false;
            prev = rel; names[i] = _names[s][i]; t[i] = rel;
        }
        _done.Enqueue(new GpuFrame(_frame[s], names, t, mono));
        _pending[s] = false;
        return true;
    }

    /// <summary>Completed frames in completion order.</summary>
    public bool TryDequeue(out GpuFrame frame)
    {
        if (_done.Count > 0) { frame = _done.Dequeue(); return true; }
        frame = null!; return false;
    }

    public void ResetCounters() { Dropped = 0; Overflow = 0; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _gl.DeleteQueries(_ids); } catch (Exception) { /* context already gone */ }
    }
}
