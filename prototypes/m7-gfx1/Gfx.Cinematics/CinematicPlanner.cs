using System.Security.Cryptography;
using System.Text;
using Gfx.Avatar;

namespace Gfx.Cinematics;

public enum Beat { Aftermath, AvatarIntro, OpponentIntro, Confrontation, OutcomeBeat, Banner, WarRecordCard }

/// <summary>What the outcome beat depicts, resolved from the recorded outcome by <see cref="CinematicPlanner"/> rules.</summary>
public enum OutcomeBeatKind
{
    Victory, Submission, Capture, Execution, Treaty, Standoff, DefeatYield, RepresentativeYields,
    /// <summary>Execution recorded but not depicted (content switch off, or inconsistent with a surrender): cut to aftermath.</summary>
    AftermathCut,
    /// <summary>Opponent leader's fate unknown: no opponent figure.</summary>
    AftermathOnly,
}

public enum CameraMove { Crane, Dolly, Orbit, TwoShotPush, LowAngle, OverShoulder, Static }
public enum SetKind { Field, Naval, Fortress }
public enum Weather { Clear, Haze, Overcast, Mist }
public enum OpponentPresence { Leader, Representative, Absent }
public enum BannerSide { None, Player, Opponent, Both }

/// <summary>Content settings (player options). Execution is depicted only when this is on AND the fate is Executed.</summary>
public sealed record CinematicSettings(bool AllowExecutionContent = true);

/// <summary>One shot. An empty clip id means the character is not on screen in this shot.</summary>
public sealed record PlannedShot(Beat Beat, CameraMove Move, int DurationMs, string AvatarClip, string OpponentClip, int CameraSide, int Framing);

/// <summary>The planner's output: pure data (integers, enums, strings). Hash is exact.</summary>
public sealed record CinematicPlan(
    OutcomeBeatKind OutcomeBeat, SetKind Set, int HourTenths, Weather Weather, OpponentPresence Opponent,
    BannerSide Banner, AvatarRole AvatarRole, AvatarRole OpponentRole, bool AvatarArmed, int EraCapabilityTier,
    IReadOnlyList<PlannedShot> Shots, IReadOnlyList<string> Notes)
{
    public int TotalMs { get { int s = 0; foreach (var x in Shots) s += x.DurationMs; return s; } }

    public byte[] CanonicalBytes()
    {
        using var ms = new MemoryStream(); using var w = new BinaryWriter(ms, Encoding.UTF8);
        void S(string s) { var b = Encoding.UTF8.GetBytes(s); w.Write(b.Length); w.Write(b); }
        w.Write((int)OutcomeBeat); w.Write((int)Set); w.Write(HourTenths); w.Write((int)Weather); w.Write((int)Opponent);
        w.Write((int)Banner); w.Write((int)AvatarRole); w.Write((int)OpponentRole); w.Write(AvatarArmed); w.Write(EraCapabilityTier);
        w.Write(Shots.Count);
        foreach (var s in Shots) { w.Write((int)s.Beat); w.Write((int)s.Move); w.Write(s.DurationMs); S(s.AvatarClip); S(s.OpponentClip); w.Write(s.CameraSide); w.Write(s.Framing); }
        w.Write(Notes.Count); foreach (var n in Notes) S(n);
        w.Flush(); return ms.ToArray();
    }

    public string Hash() => Convert.ToHexString(SHA256.HashData(CanonicalBytes()));

    /// <summary>Human-readable dump (plan files for naval/siege etc.).</summary>
    public string Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"outcomeBeat={OutcomeBeat} set={Set} hour={HourTenths / 10}.{HourTenths % 10} weather={Weather} opponent={Opponent} banner={Banner}");
        sb.AppendLine($"avatarRole={AvatarRole} opponentRole={OpponentRole} armed={AvatarArmed} eraTier={EraCapabilityTier} totalMs={TotalMs} hash={Hash()}");
        foreach (var s in Shots)
            sb.AppendLine($"  {s.Beat,-14} {s.Move,-12} {s.DurationMs,5} ms  avatar={(s.AvatarClip == "" ? "-" : s.AvatarClip),-20} opponent={(s.OpponentClip == "" ? "-" : s.OpponentClip),-20} side={s.CameraSide,2} framing={s.Framing}");
        foreach (var n in Notes) sb.AppendLine("  note: " + n);
        return sb.ToString();
    }
}

/// <summary>Clip catalogue: ids and tags only (pure data the planner selects from). Poses live in <see cref="CineClips"/>.</summary>
public static class ClipCatalog
{
    public const string Stand = "stand", Approach = "approach", Submit = "submit", Captive = "captive", Escort = "escort",
        Treaty = "treaty", Execute = "execute", Fall = "fall", Victory = "victory", Yield = "yield", Standoff = "standoff";

    public static readonly (string Id, string[] Tags)[] Clips =
    {
        ("stand-proud", new[] { Stand }),
        ("stand-wary", new[] { Stand }),
        ("walk-in", new[] { Approach }),
        ("kneel-one", new[] { Submit }),
        ("kneel-both", new[] { Submit }),
        ("bound-walk", new[] { Captive }),
        ("led-captive", new[] { Captive }),
        ("escort-walk", new[] { Escort }),
        ("handshake", new[] { Treaty }),
        ("clasp-bow", new[] { Treaty }),
        ("strike-overhead", new[] { Execute }),
        ("strike-side", new[] { Execute }),
        ("kneel-fall", new[] { Fall }),
        ("victory-raise-arm", new[] { Victory }),
        ("victory-raise-both", new[] { Victory }),
        ("bow-deep", new[] { Yield }),
        ("bow-head", new[] { Yield }),
        ("standoff-arms-crossed", new[] { Standoff }),
        ("standoff-hold", new[] { Standoff }),
    };

    public static bool HasTag(string clipId, string tag)
    {
        foreach (var (id, tags) in Clips) if (id == clipId) return Array.IndexOf(tags, tag) >= 0;
        return false;
    }

    public static string[] WithTag(string tag)
    {
        var l = new List<string>();
        foreach (var (id, tags) in Clips) if (Array.IndexOf(tags, tag) >= 0) l.Add(id);
        return l.ToArray();
    }
}

/// <summary>
/// PURE function (outcome, seed, settings) → <see cref="CinematicPlan"/>. Integer-only (SplitMix64); reads the
/// outcome, never writes it. The renderer never decides what happened: every beat follows from the recorded
/// outcome (R24, ledger §24). Validity RULES (each covered by the truth-table test):
/// <list type="number">
/// <item>R1 fate governs: the outcome beat follows the opponent leader's recorded fate; a conflicting kind is noted, never acted out.</item>
/// <item>R2 never Execution if the leader surrendered (fate Surrendered, or kind Surrender with any fate).</item>
/// <item>R3 Execution only if fate == Executed AND <see cref="CinematicSettings.AllowExecutionContent"/>; otherwise cut to aftermath.</item>
/// <item>R4 Inconclusive (or no victor) → Standoff: no victory clip, no banner beat.</item>
/// <item>R5 Defeat (victor = opponent) → defeat sequence: the player's avatar yields on the losing side; opponent's banner. The avatar is never killed or captured (presentation-only identity).</item>
/// <item>R6 Naval domain → naval set; Siege domain → fallen-fortress set (the recorded domain governs; a Naval/SiegeVictory kind picks the set only for a Land-domain record).</item>
/// <item>R7 Mandatory beats in fixed order: Aftermath, AvatarIntro, [OpponentIntro], Confrontation, OutcomeBeat, [Banner], WarRecordCard.
/// Opponent intro is dropped only when no opponent figure exists; Banner only when there is no victor.</item>
/// <item>R8 Variation (seed): camera moves/sides/framing, hour, weather, clip variants, durations ±0.5 s — never the beats.</item>
/// </list>
/// </summary>
public static class CinematicPlanner
{
    static ulong Mix(ulong z) { unchecked { z += 0x9E3779B97F4A7C15UL; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }

    sealed class Rng(ulong s)
    {
        ulong _s = s;
        public int Next(int n) { _s = Mix(_s); return (int)(_s % (ulong)n); }
        public T Pick<T>(T[] a) => a[Next(a.Length)];
    }

    public static CinematicPlan Plan(WarOutcome o, ulong seed, CinematicSettings? settings = null)
    {
        settings ??= new CinematicSettings();
        var rng = new Rng(Mix(o.WeatherTimeSeed) ^ Mix(seed * 0x2545F4914F6CDD1DUL + 1));
        var notes = new List<string>();
        var (beat, presence) = ResolveBeat(o, settings, notes);

        var set = o.Domain switch { WarDomain.Naval => SetKind.Naval, WarDomain.Siege => SetKind.Fortress, _ => SetKind.Field };
        // the recorded domain governs; the kind only chooses the set for a Land-domain record
        if (o.Domain == WarDomain.Land && o.Kind == OutcomeKind.NavalVictory) { set = SetKind.Naval; notes.Add("R6: kind NavalVictory on a land-domain record → naval set"); }
        if (o.Domain == WarDomain.Land && o.Kind == OutcomeKind.SiegeVictory) { set = SetKind.Fortress; notes.Add("R6: kind SiegeVictory on a land-domain record → fortress set"); }

        bool sombre = beat is OutcomeBeatKind.Standoff or OutcomeBeatKind.DefeatYield or OutcomeBeatKind.AftermathCut;
        int hour = sombre ? rng.Pick(new[] { 63, 68, 172, 178, 183 }) : rng.Pick(new[] { 85, 98, 110, 125, 140, 155, 166 });
        var weather = sombre ? rng.Pick(new[] { Weather.Overcast, Weather.Mist, Weather.Haze }) : rng.Pick(new[] { Weather.Clear, Weather.Clear, Weather.Haze, Weather.Overcast, Weather.Mist });

        var banner = beat switch
        {
            OutcomeBeatKind.Standoff => BannerSide.None,
            OutcomeBeatKind.DefeatYield => BannerSide.Opponent,
            OutcomeBeatKind.Treaty => BannerSide.Both,
            _ => BannerSide.Player,
        };
        var avatarRole = beat is OutcomeBeatKind.Treaty or OutcomeBeatKind.Submission or OutcomeBeatKind.RepresentativeYields ? AvatarRole.Royal : AvatarRole.Military;
        var oppRole = beat switch
        {
            OutcomeBeatKind.Capture or OutcomeBeatKind.Execution => AvatarRole.Civil,
            OutcomeBeatKind.RepresentativeYields => AvatarRole.Civil,
            OutcomeBeatKind.Treaty or OutcomeBeatKind.Submission => AvatarRole.Royal,
            _ => AvatarRole.Military,
        };

        string Clip(string tag) => rng.Pick(ClipCatalog.WithTag(tag));
        string stand = Clip(ClipCatalog.Stand), oppStand = presence == OpponentPresence.Absent ? "" : Clip(ClipCatalog.Stand);
        string avOut, opOut;
        switch (beat)
        {
            case OutcomeBeatKind.Victory: avOut = Clip(ClipCatalog.Victory); opOut = Clip(ClipCatalog.Yield); break;
            case OutcomeBeatKind.Submission: avOut = "stand-proud"; opOut = Clip(ClipCatalog.Submit); break;
            case OutcomeBeatKind.Capture: avOut = Clip(ClipCatalog.Escort); opOut = Clip(ClipCatalog.Captive); break;
            case OutcomeBeatKind.Execution: avOut = Clip(ClipCatalog.Execute); opOut = Clip(ClipCatalog.Fall); break;
            case OutcomeBeatKind.Treaty: avOut = Clip(ClipCatalog.Treaty); opOut = avOut; break;
            case OutcomeBeatKind.Standoff: avOut = Clip(ClipCatalog.Standoff); opOut = Clip(ClipCatalog.Standoff); break;
            case OutcomeBeatKind.DefeatYield: avOut = Clip(ClipCatalog.Yield); opOut = Clip(ClipCatalog.Victory); break;
            case OutcomeBeatKind.RepresentativeYields: avOut = "stand-proud"; opOut = Clip(ClipCatalog.Yield); break;
            case OutcomeBeatKind.AftermathOnly: avOut = Clip(ClipCatalog.Victory); opOut = ""; break;
            default /* AftermathCut */: avOut = ""; opOut = ""; break;
        }

        int Dur(int baseMs) => baseMs + 100 * (rng.Next(11) - 5);
        int SideR() => rng.Next(2) == 0 ? -1 : 1;
        var shots = new List<PlannedShot>
        {
            new(Beat.Aftermath, rng.Pick(new[] { CameraMove.Crane, CameraMove.Dolly }), Dur(4000), "", "", SideR(), rng.Next(3)),
            new(Beat.AvatarIntro, rng.Pick(new[] { CameraMove.Dolly, CameraMove.Orbit, CameraMove.LowAngle }), Dur(3500), "walk-in", oppStand, SideR(), rng.Next(3)),
        };
        if (presence != OpponentPresence.Absent)
            shots.Add(new(Beat.OpponentIntro, rng.Pick(new[] { CameraMove.Dolly, CameraMove.Orbit, CameraMove.OverShoulder }), Dur(3000), stand, oppStand, SideR(), rng.Next(3)));
        shots.Add(new(Beat.Confrontation, rng.Pick(new[] { CameraMove.TwoShotPush, CameraMove.Orbit, CameraMove.Static }), Dur(3000), stand, oppStand, SideR(), rng.Next(3)));
        shots.Add(new(Beat.OutcomeBeat, beat == OutcomeBeatKind.AftermathCut ? CameraMove.Crane : rng.Pick(new[] { CameraMove.TwoShotPush, CameraMove.LowAngle, CameraMove.OverShoulder, CameraMove.Orbit }),
            Dur(4500), avOut, opOut, SideR(), rng.Next(3)));
        if (banner != BannerSide.None)
            shots.Add(new(Beat.Banner, rng.Pick(new[] { CameraMove.LowAngle, CameraMove.Orbit }), Dur(3000), avOut == "" ? "" : stand, opOut == "" || beat == OutcomeBeatKind.Capture || beat == OutcomeBeatKind.Execution ? "" : oppStand, SideR(), rng.Next(3)));
        shots.Add(new(Beat.WarRecordCard, rng.Pick(new[] { CameraMove.Static, CameraMove.Crane }), Dur(4000), "", "", SideR(), 0));

        return new CinematicPlan(beat, set, hour, weather, presence, banner, avatarRole, oppRole,
            AvatarArmed: beat == OutcomeBeatKind.Execution, o.EraCapabilityTier, shots, notes);
    }

    /// <summary>Rules R1–R5: the depicted outcome beat from the recorded outcome.</summary>
    static (OutcomeBeatKind, OpponentPresence) ResolveBeat(WarOutcome o, CinematicSettings settings, List<string> notes)
    {
        if (o.Kind == OutcomeKind.Inconclusive || o.Victor == Side.None)
        {
            if (o.Kind != OutcomeKind.Inconclusive) notes.Add($"R4: no victor recorded for kind {o.Kind} → standoff");
            return (OutcomeBeatKind.Standoff, OpponentPresence.Leader);
        }
        if (o.Kind == OutcomeKind.Defeat || o.Victor == Side.Opponent)
        {
            if (o.Kind != OutcomeKind.Defeat) notes.Add($"R5: victor is the opponent for kind {o.Kind} → defeat sequence");
            return (OutcomeBeatKind.DefeatYield, OpponentPresence.Leader);
        }
        if (o.Kind == OutcomeKind.Defeat) notes.Add("R5: kind Defeat with player victor is inconsistent");

        bool surrenderContext = o.Kind == OutcomeKind.Surrender || o.OpponentLeaderFate == LeaderFate.Surrendered;
        void Conflict(string depicted) { notes.Add($"R1: kind {o.Kind} conflicts with fate {o.OpponentLeaderFate}; fate governs → {depicted}"); }
        switch (o.OpponentLeaderFate)
        {
            case LeaderFate.Executed:
                if (surrenderContext) { notes.Add("R2: execution after surrender is never depicted → aftermath cut"); return (OutcomeBeatKind.AftermathCut, OpponentPresence.Leader); }
                if (!settings.AllowExecutionContent) { notes.Add("R3: execution content disabled → aftermath cut"); return (OutcomeBeatKind.AftermathCut, OpponentPresence.Leader); }
                if (o.Kind != OutcomeKind.Execution) Conflict("execution");
                return (OutcomeBeatKind.Execution, OpponentPresence.Leader);
            case LeaderFate.Surrendered:
                if (o.Kind is not (OutcomeKind.Surrender or OutcomeKind.DecisiveVictory or OutcomeKind.SiegeVictory or OutcomeKind.NavalVictory)) Conflict("submission");
                return (OutcomeBeatKind.Submission, OpponentPresence.Leader);
            case LeaderFate.Captured:
                if (o.Kind is not (OutcomeKind.Capture or OutcomeKind.DecisiveVictory or OutcomeKind.SiegeVictory or OutcomeKind.NavalVictory)) Conflict("capture");
                return (OutcomeBeatKind.Capture, OpponentPresence.Leader);
            case LeaderFate.Fled:
                notes.Add("leader fled: a representative stands in");
                return (OutcomeBeatKind.RepresentativeYields, OpponentPresence.Representative);
            case LeaderFate.Unknown:
                notes.Add("leader fate unknown: no opponent figure");
                return (OutcomeBeatKind.AftermathOnly, OpponentPresence.Absent);
            default: // Alive
                if (o.Kind == OutcomeKind.NegotiatedPeace) return (OutcomeBeatKind.Treaty, OpponentPresence.Leader);
                if (o.Kind == OutcomeKind.Surrender) return (OutcomeBeatKind.Submission, OpponentPresence.Leader);
                if (o.Kind is OutcomeKind.Execution or OutcomeKind.Capture) Conflict("victory");
                return (OutcomeBeatKind.Victory, OpponentPresence.Leader);
        }
    }
}
