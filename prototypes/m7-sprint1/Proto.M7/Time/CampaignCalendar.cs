using System.Text.Json;
using Proto.M7.Fixed;

namespace Proto.M7.Time;

/// <summary>One era band in integer units: [StartDay, EndDay) with turn length DtDays.</summary>
public readonly record struct EraBand(string Name, long StartYear, long EndYear, long DtDays, long StartDay, long EndDay, long FirstTurn, long TurnCount)
{
    public long DtMs => DtDays * SimInstant.MsPerDay;
    public long StartMs => StartDay * SimInstant.MsPerDay;
    public long EndMs => EndDay * SimInstant.MsPerDay;
}

/// <summary>Position of an instant inside its campaign turn as an exact fraction Num/Den of the turn.</summary>
public readonly record struct TurnPosition(long Turn, long Num, long Den);

/// <summary>
/// The campaign calendar built from the era bands of era-pacing.json (spec §2.2). Mirrors the kernel rules
/// (Sim.Core/Kernel/EraTableLoader.cs: dtYears*360 integral, band span a multiple of dt, contiguous bands;
/// EraTable.DtDaysAt: dt keyed on the turn-START date) WITHOUT referencing Sim.*. dtYears is read from the
/// JSON number token's raw text with exact rational arithmetic (ExactNumberText) — no IEEE type.
/// Turn t spans the half-open interval [TurnStart(t), TurnStart(t+1)).
/// </summary>
public sealed class CampaignCalendar
{
    private readonly EraBand[] _bands;

    public CampaignCalendar(EraBand[] bands)
    {
        if (bands.Length == 0) throw new ArgumentException("no bands");
        _bands = bands;
        TotalTurns = bands[^1].FirstTurn + bands[^1].TurnCount;
    }

    public IReadOnlyList<EraBand> Bands => _bands;
    public long TotalTurns { get; }
    public SimInstant CampaignEnd => new(_bands[^1].EndMs);

    public static CampaignCalendar FromJsonFile(string path) => FromJson(File.ReadAllBytes(path));

    public static CampaignCalendar FromJson(ReadOnlySpan<byte> utf8)
    {
        var raw = new List<(string name, ExactNumber start, ExactNumber end, ExactNumber dt)>();
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
        Expect(ref reader, JsonTokenType.StartObject);
        bool sawBands = false;
        while (true)
        {
            Read(ref reader);
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) throw new FormatException("expected property");
            string prop = reader.GetString()!;
            if (prop != "bands") { reader.Skip(); continue; }
            sawBands = true;
            Expect(ref reader, JsonTokenType.StartArray);
            while (true)
            {
                Read(ref reader);
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.StartObject) throw new FormatException("expected band object");
                string? name = null; ExactNumber? s = null, e = null, d = null;
                while (true)
                {
                    Read(ref reader);
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    string key = reader.GetString()!;
                    Read(ref reader);
                    switch (key)
                    {
                        case "name": name = reader.GetString(); break;
                        case "startYear": s = Num(ref reader); break;
                        case "endYear": e = Num(ref reader); break;
                        case "dtYears": d = Num(ref reader); break;
                        default: reader.Skip(); break;
                    }
                }
                if (name is null || s is null || e is null || d is null) throw new FormatException("band missing a field");
                raw.Add((name, s.Value, e.Value, d.Value));
            }
        }
        if (!sawBands) throw new FormatException("no 'bands' property");
        return Build(raw);
    }

    private static ExactNumber Num(ref Utf8JsonReader r)
    {
        if (r.TokenType != JsonTokenType.Number) throw new FormatException("expected number");
        // ValueSpan is the token's raw UTF-8 text: the exact digits written in the file.
        if (r.HasValueSequence) throw new FormatException("segmented input is not supported");
        return ExactNumberText.Parse(r.ValueSpan);
    }

    private static void Read(ref Utf8JsonReader r) { if (!r.Read()) throw new FormatException("unexpected end of JSON"); }
    private static void Expect(ref Utf8JsonReader r, JsonTokenType t) { Read(ref r); if (r.TokenType != t) throw new FormatException($"expected {t}"); }

    private static CampaignCalendar Build(List<(string name, ExactNumber start, ExactNumber end, ExactNumber dt)> raw)
    {
        if (raw.Count == 0) throw new FormatException("'bands' is empty");
        var bands = new EraBand[raw.Count];
        long originYear = raw[0].start.AsExactInteger();
        long turn = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            var (name, s, e, d) = raw[i];
            long sy = s.AsExactInteger(), ey = e.AsExactInteger();
            if (ey <= sy) throw new FormatException($"band '{name}' is empty or reversed");
            long dtDays = d.TimesExactInteger(SimInstant.YearDays); // throws unless dtYears*360 is integral
            if (dtDays <= 0) throw new FormatException($"band '{name}' dt must be positive");
            long startDay = (sy - originYear) * SimInstant.YearDays;
            long endDay = (ey - originYear) * SimInstant.YearDays;
            if (i > 0 && startDay != bands[i - 1].EndDay) throw new FormatException($"band '{name}' is not contiguous");
            long span = endDay - startDay;
            if (span % dtDays != 0) throw new FormatException($"band '{name}' span is not a whole number of turns");
            long count = span / dtDays;
            bands[i] = new EraBand(name, sy, ey, dtDays, startDay, endDay, turn, count);
            turn += count;
        }
        return new CampaignCalendar(bands);
    }

    /// <summary>Index of the band containing turn (binary search over FirstTurn).</summary>
    public int BandIndexOfTurn(long turn)
    {
        if (turn < 0 || turn >= TotalTurns) throw new ArgumentOutOfRangeException(nameof(turn));
        int lo = 0, hi = _bands.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_bands[mid].FirstTurn <= turn) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>Index of the band whose [StartMs, EndMs) contains the instant (computed state, Law 4 — not a label).</summary>
    public int BandIndexAt(SimInstant t)
    {
        if (t.Ms < _bands[0].StartMs || t.Ms >= _bands[^1].EndMs) throw new ArgumentOutOfRangeException(nameof(t));
        int lo = 0, hi = _bands.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_bands[mid].StartMs <= t.Ms) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>Start instant of a turn; TurnStart(TotalTurns) is the campaign end.</summary>
    public SimInstant TurnStart(long turn)
    {
        if (turn == TotalTurns) return CampaignEnd;
        var b = _bands[BandIndexOfTurn(turn)];
        return new SimInstant(b.StartMs + (turn - b.FirstTurn) * b.DtMs);
    }

    public SimInstant TurnEnd(long turn) => TurnStart(turn + 1);

    public long TurnLengthMs(long turn) => _bands[BandIndexOfTurn(turn)].DtMs;
    public long TurnLengthDays(long turn) => _bands[BandIndexOfTurn(turn)].DtDays;

    public long TurnOf(SimInstant t)
    {
        var b = _bands[BandIndexAt(t)];
        return b.FirstTurn + (t.Ms - b.StartMs) / b.DtMs; // non-negative operands: truncation == floor
    }

    /// <summary>The instant's exact position in its turn: Num = offset ms from turn start, Den = turn length ms.</summary>
    public TurnPosition PositionInTurn(SimInstant t)
    {
        long turn = TurnOf(t);
        return new TurnPosition(turn, t.Ms - TurnStart(turn).Ms, TurnLengthMs(turn));
    }

    /// <summary>TurnStart(turn) + floor(lengthMs * num / den); 0 &lt;= num &lt;= den, den &gt; 0.</summary>
    public SimInstant At(long turn, long num, long den)
    {
        if (den <= 0 || num < 0 || num > den) throw new ArgumentOutOfRangeException(nameof(num), "need 0 <= num <= den, den > 0");
        return new SimInstant(TurnStart(turn).Ms + FixedMath.MulDivFloor(TurnLengthMs(turn), num, den));
    }
}
