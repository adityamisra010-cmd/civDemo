using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gfx.Units;

/// <summary>Entity state codes of the battle prototype (Proto.M7.Tactical.EntityState; copied as numbers, not referenced).</summary>
public enum TraceState : byte { Active = 0, Routing = 1, Surrendered = 2, Dead = 3, Escaped = 4 }

public enum TraceEventKind : byte { Fire = 1, Damage = 2 }

public readonly record struct TraceFormation(long Id, int Side, string Kind, bool Ranged, int Count);
public readonly record struct TraceEntity(long Id, long Formation, int Side);
/// <summary>Fire: A = 1 melee / 2 ranged. Damage: A = hit points.</summary>
public readonly record struct TraceEvent(long TimeMs, TraceEventKind Kind, long Source, long Target, long A);

/// <summary>
/// FROZEN INPUT: a battle-trace fixture (format <c>m7gfx-battle-trace/1</c>) exported from the battle prototype
/// (positions in integer mm per 2 s step, entity states, Fire/Damage events). The renderer only READS it: all data is
/// exposed read-only and <see cref="Hash"/> (SHA-256 over a canonical little-endian encoding) proves invariance.
/// </summary>
public sealed class BattleTrace
{
    public const string Format = "m7gfx-battle-trace/1";
    readonly long[][] _pos; readonly byte[][] _state; readonly long[] _times;
    readonly TraceFormation[] _formations; readonly TraceEntity[] _entities; readonly TraceEvent[] _events;

    public string Label { get; }
    public ulong Seed { get; }
    public long StepMs { get; }
    public bool Over { get; }
    public IReadOnlyList<TraceFormation> Formations => _formations;
    public IReadOnlyList<TraceEntity> Entities => _entities;
    public IReadOnlyList<TraceEvent> Events => _events;
    public int StepCount => _times.Length;
    public long StepTimeMs(int step) => _times[step];
    /// <summary>Position (mm) of entity index e at step s.</summary>
    public (long x, long y) PosMm(int step, int e) => (_pos[step][2 * e], _pos[step][2 * e + 1]);
    public TraceState State(int step, int e) => (TraceState)_state[step][e];

    BattleTrace(string label, ulong seed, long stepMs, bool over, TraceFormation[] f, TraceEntity[] e, long[] t, long[][] p, byte[][] s, TraceEvent[] ev)
    { Label = label; Seed = seed; StepMs = stepMs; Over = over; _formations = f; _entities = e; _times = t; _pos = p; _state = s; _events = ev; }

    public static BattleTrace Load(string path) => Parse(File.ReadAllBytes(path));

    public static BattleTrace Parse(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        using var doc = JsonDocument.ParseValue(ref reader);
        var r = doc.RootElement;
        if (r.GetProperty("format").GetString() != Format) throw new InvalidDataException("not an " + Format + " fixture");
        var f = r.GetProperty("formations").EnumerateArray().Select(x => new TraceFormation(
            x.GetProperty("id").GetInt64(), x.GetProperty("side").GetInt32(), x.GetProperty("kind").GetString() ?? "",
            x.GetProperty("ranged").GetBoolean(), x.GetProperty("count").GetInt32())).ToArray();
        var e = r.GetProperty("entities").EnumerateArray().Select(x => new TraceEntity(x[0].GetInt64(), x[1].GetInt64(), x[2].GetInt32())).ToArray();
        var steps = r.GetProperty("steps").EnumerateArray().ToArray();
        var t = new long[steps.Length]; var p = new long[steps.Length][]; var s = new byte[steps.Length][];
        for (int i = 0; i < steps.Length; i++)
        {
            t[i] = steps[i].GetProperty("t").GetInt64();
            p[i] = steps[i].GetProperty("pos").EnumerateArray().Select(v => v.GetInt64()).ToArray();
            s[i] = steps[i].GetProperty("state").EnumerateArray().Select(v => checked((byte)v.GetInt32())).ToArray();
            if (p[i].Length != 2 * e.Length || s[i].Length != e.Length) throw new InvalidDataException($"step {i}: entity count mismatch");
            if (i > 0 && t[i] <= t[i - 1]) throw new InvalidDataException($"step {i}: time not increasing");
        }
        var ev = r.GetProperty("events").EnumerateArray().Select(x => new TraceEvent(
            x.GetProperty("t").GetInt64(),
            x.GetProperty("k").GetString() switch { "Fire" => TraceEventKind.Fire, "Damage" => TraceEventKind.Damage, var k => throw new InvalidDataException("event kind " + k) },
            x.GetProperty("src").GetInt64(), x.GetProperty("tgt").GetInt64(), x.GetProperty("a").GetInt64())).ToArray();
        return new BattleTrace(r.GetProperty("label").GetString() ?? "", r.GetProperty("seed").GetUInt64(), r.GetProperty("stepMs").GetInt64(),
            r.GetProperty("over").GetBoolean(), f, e, t, p, s, ev);
    }

    /// <summary>Index of entity id (entities are listed in ascending id; binary search).</summary>
    public int IndexOf(long entityId)
    {
        int lo = 0, hi = _entities.Length - 1;
        while (lo <= hi)
        {
            int m = (lo + hi) >> 1; long id = _entities[m].Id;
            if (id == entityId) return m;
            if (id < entityId) lo = m + 1; else hi = m - 1;
        }
        return -1;
    }

    /// <summary>SHA-256 (hex) over a canonical little-endian encoding of every field.</summary>
    public string Hash()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        void S(string s) { var u = Encoding.UTF8.GetBytes(s); L(u.Length); h.AppendData(u); }
        S(Format); S(Label); L(unchecked((long)Seed)); L(StepMs); L(Over ? 1 : 0);
        L(_formations.Length); foreach (var f in _formations) { L(f.Id); L(f.Side); S(f.Kind); L(f.Ranged ? 1 : 0); L(f.Count); }
        L(_entities.Length); foreach (var e in _entities) { L(e.Id); L(e.Formation); L(e.Side); }
        L(_times.Length);
        for (int i = 0; i < _times.Length; i++)
        {
            L(_times[i]);
            foreach (var v in _pos[i]) L(v);
            h.AppendData(_state[i]);
        }
        L(_events.Length); foreach (var e in _events) { L(e.TimeMs); L((long)e.Kind); L(e.Source); L(e.Target); L(e.A); }
        return Convert.ToHexString(h.GetHashAndReset());
    }

    /// <summary>Walks up from <paramref name="start"/> to find <c>fixtures/&lt;name&gt;</c> (repo-relative fixture lookup).</summary>
    public static string FindFixture(string name, string? start = null)
    {
        for (var d = new DirectoryInfo(start ?? AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "fixtures", name);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("fixture not found above " + (start ?? AppContext.BaseDirectory), name);
    }

    public const string DefaultFixture = "battle-trace-combinedarms-s7.json";
}
