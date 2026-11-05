using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Gfx.Avatar;

/// <summary>Face blend-shape channels. Order is the serialized order of <see cref="AvatarProfile.FaceMorphs"/>; append only.</summary>
public enum FaceMorph
{
    JawWidth, Cheekbones, BrowRidge, NoseLength, NoseWidth, NoseBridge, EyeSize,
    EyeSpacing, LipFullness, ChinLength, EarSize, AgeLines, ForeheadHeight, HeadWidth,
}

public enum HairStyle { Shaved, Short, Long, TiedBack, Curly }
public enum FacialHair { None, Stubble, Moustache, FullBeard }
public enum AvatarRole { Military, Civil, Royal }
/// <summary>Banner / heraldry division (presentation).</summary>
public enum HeraldryPattern { PerFess, PerPale, Cross, Chevron, Saltire }

/// <summary>Body proportions. All integers so the profile hash is platform-exact.</summary>
public sealed class BodyParams
{
    /// <summary>Standing height, mm (1500..2050).</summary>
    public int HeightMm { get; set; } = 1760;
    /// <summary>Slim (0) → heavy (1000).</summary>
    public int BuildPermille { get; set; } = 500;
    /// <summary>Narrow (0) → broad (1000) shoulders.</summary>
    public int ShoulderPermille { get; set; } = 500;
}

public sealed class Heraldry
{
    public string Primary { get; set; } = "#8C1C1C";
    public string Secondary { get; set; } = "#E6C85A";
    public HeraldryPattern Pattern { get; set; } = HeraldryPattern.PerPale;
}

/// <summary>A clothing PREFERENCE for one role. Era is never stored: the era outfit is computed at render time.</summary>
public sealed class RolePreference
{
    /// <summary>Style variant within the era's garment set (0..2).</summary>
    public int Variant { get; set; }
    /// <summary>Dress in heraldry colours (else <see cref="Accent"/> as the main colour).</summary>
    public bool HeraldicColours { get; set; } = true;
    public string Accent { get; set; } = "#3A4A6B";
}

public sealed class ClothingPreferences
{
    public RolePreference Military { get; set; } = new();
    public RolePreference Civil { get; set; } = new() { HeraldicColours = false };
    public RolePreference Royal { get; set; } = new();
    public RolePreference For(AvatarRole r) => r switch { AvatarRole.Military => Military, AvatarRole.Civil => Civil, _ => Royal };
}

/// <summary>
/// The player's persistent identity (VB A.2). PRESENTATION-ONLY: lives in its own versioned JSON file next to the
/// session files, never in WorldState or the world hash (recon-governance §1.3: no Action Capacity, no orders,
/// no table rows → not a gameplay object). Every field is an integer, enum or string, so <see cref="Hash"/> is exact.
/// Face morph weights are signed permille: −1000..1000 = normalized −1..1 (0 = neutral).
/// </summary>
public sealed class AvatarProfile
{
    public const int CurrentSchemaVersion = 1;
    public const int MorphCount = 14;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    /// <summary>Stable identity (GUID, "D" format). Never regenerated on dynasty/ruler change.</summary>
    public string Id { get; set; } = "00000000-0000-0000-0000-000000000000";
    public string DisplayName { get; set; } = "";
    public BodyParams Body { get; set; } = new();
    public int[] FaceMorphs { get; set; } = new int[MorphCount];
    public string SkinTone { get; set; } = "#C68E6A";
    public string EyeColour { get; set; } = "#4A3A28";
    public HairStyle Hair { get; set; } = HairStyle.Short;
    public string HairColour { get; set; } = "#3B2A1E";
    public FacialHair FacialHair { get; set; } = FacialHair.None;
    public Heraldry Heraldry { get; set; } = new();
    public ClothingPreferences Clothing { get; set; } = new();

    public int Morph(FaceMorph m) => FaceMorphs[(int)m];

    // ------------------------------------------------------------------ validation

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new FormatException($"schemaVersion {SchemaVersion} (expected {CurrentSchemaVersion}; migrate first)");
        if (!Guid.TryParseExact(Id, "D", out _)) throw new FormatException("id must be a GUID in D format");
        if (FaceMorphs is null || FaceMorphs.Length != MorphCount) throw new FormatException($"faceMorphs must have {MorphCount} entries");
        foreach (var w in FaceMorphs) if (w < -1000 || w > 1000) throw new FormatException($"face morph {w} outside -1000..1000");
        if (Body.HeightMm < 1500 || Body.HeightMm > 2050) throw new FormatException("heightMm outside 1500..2050");
        if (Body.BuildPermille is < 0 or > 1000 || Body.ShoulderPermille is < 0 or > 1000) throw new FormatException("body permille outside 0..1000");
        foreach (var c in new[] { SkinTone, EyeColour, HairColour, Heraldry.Primary, Heraldry.Secondary, Clothing.Military.Accent, Clothing.Civil.Accent, Clothing.Royal.Accent })
            Colour.Parse(c);
    }

    // ------------------------------------------------------------------ JSON (versioned)

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Parses, migrates older schema versions, validates.</summary>
    public static AvatarProfile FromJson(string json)
    {
        var node = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("profile is not a JSON object");
        Migrate(node);
        var p = node.Deserialize<AvatarProfile>(Json) ?? throw new FormatException("empty profile");
        p.Validate();
        return p;
    }

    /// <summary>
    /// Version migration (stub with one real step). v0 (pre-release draft) stored <c>name</c> and a 12-entry
    /// <c>morphs</c> array; v1 renames to <c>displayName</c>/<c>faceMorphs</c> and appends ForeheadHeight/HeadWidth = 0.
    /// </summary>
    public static void Migrate(JsonObject o)
    {
        int v = o["schemaVersion"]?.GetValue<int>() ?? 0;
        if (v == 0)
        {
            if (o["name"] is { } n) { o.Remove("name"); o["displayName"] = n.GetValue<string>(); }
            if (o["morphs"] is JsonArray a)
            {
                var arr = new JsonArray();
                foreach (var x in a) arr.Add(x!.GetValue<int>());
                while (arr.Count < MorphCount) arr.Add(0);
                o.Remove("morphs"); o["faceMorphs"] = arr;
            }
            o["schemaVersion"] = v = 1;
        }
        if (v > CurrentSchemaVersion) throw new FormatException($"profile schemaVersion {v} is newer than this build ({CurrentSchemaVersion})");
    }

    public void Save(string path) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path, ToJson()); }
    public static AvatarProfile Load(string path) => FromJson(File.ReadAllText(path));

    // ------------------------------------------------------------------ canonical hash

    /// <summary>Canonical little-endian bytes of every field in declaration order (strings: int32 length + UTF-8).</summary>
    public byte[] CanonicalBytes(bool identityOnly = false)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);
        void S(string s) { var b = Encoding.UTF8.GetBytes(s); w.Write(b.Length); w.Write(b); }
        w.Write(SchemaVersion); S(Id);
        w.Write(Body.HeightMm); w.Write(Body.BuildPermille); w.Write(Body.ShoulderPermille);
        w.Write(FaceMorphs.Length); foreach (var m in FaceMorphs) w.Write(m);
        S(SkinTone); S(EyeColour); w.Write((int)Hair); S(HairColour); w.Write((int)FacialHair);
        if (!identityOnly)
        {
            S(DisplayName);
            S(Heraldry.Primary); S(Heraldry.Secondary); w.Write((int)Heraldry.Pattern);
            foreach (var r in new[] { Clothing.Military, Clothing.Civil, Clothing.Royal }) { w.Write(r.Variant); w.Write(r.HeraldicColours); S(r.Accent); }
        }
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>SHA-256 (hex) of the whole profile.</summary>
    public string Hash() => Convert.ToHexString(SHA256.HashData(CanonicalBytes()));
    /// <summary>SHA-256 of the physical identity only (face, body, skin, eyes, hair) — what must never change across eras.</summary>
    public string IdentityHash() => Convert.ToHexString(SHA256.HashData(CanonicalBytes(identityOnly: true)));

    public AvatarProfile Clone() => FromJson(ToJson());

    // ------------------------------------------------------------------ fixtures

    static ulong Mix(ulong z) { unchecked { z += 0x9E3779B97F4A7C15UL; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }

    static readonly string[] SkinTones = { "#F1C9A5", "#E0AC88", "#C68E6A", "#A8714F", "#8D5A3B", "#5E3A26" };
    static readonly string[] HairColours = { "#1C1410", "#3B2A1E", "#6A4A2E", "#A67C4E", "#C9A66B", "#8E8A84", "#7A2E1A" };
    static readonly string[] EyeColours = { "#4A3A28", "#2E5A7A", "#4F6B3A", "#6B5A3A", "#2A2A2A" };
    static readonly string[] Banners = { "#8C1C1C", "#1C3F8C", "#1E6B3A", "#D9B23A", "#4A1E6B", "#E8E4D8", "#1A1A1A" };

    /// <summary>
    /// Deterministic sample profile from an integer seed (face sheet, opponent fixtures). Integer SplitMix64 only.
    /// LABELLED FIXTURE: a real profile is player-authored in a customization screen (not built here).
    /// </summary>
    public static AvatarProfile Sample(ulong seed, string name)
    {
        ulong s = seed; ulong Next() => s = Mix(s);
        int R(int lo, int hi) => lo + (int)(Next() % (ulong)(hi - lo + 1));
        var p = new AvatarProfile
        {
            Id = new Guid(BitConverter.GetBytes(Next()).Concat(BitConverter.GetBytes(Next())).ToArray()).ToString("D"),
            DisplayName = name,
            Body = new BodyParams { HeightMm = R(1620, 1920), BuildPermille = R(150, 900), ShoulderPermille = R(200, 900) },
            SkinTone = SkinTones[R(0, SkinTones.Length - 1)],
            EyeColour = EyeColours[R(0, EyeColours.Length - 1)],
            Hair = (HairStyle)R(0, 4),
            HairColour = HairColours[R(0, HairColours.Length - 1)],
            FacialHair = (FacialHair)R(0, 3),
        };
        for (int i = 0; i < MorphCount; i++) p.FaceMorphs[i] = R(-900, 900);
        p.FaceMorphs[(int)FaceMorph.AgeLines] = R(0, 1000);
        int b1 = R(0, Banners.Length - 1), b2 = (b1 + R(1, Banners.Length - 1)) % Banners.Length;
        p.Heraldry = new Heraldry { Primary = Banners[b1], Secondary = Banners[b2], Pattern = (HeraldryPattern)R(0, 4) };
        p.Clothing.Military.Variant = R(0, 2); p.Clothing.Civil.Variant = R(0, 2); p.Clothing.Royal.Variant = R(0, 2);
        return p;
    }

    /// <summary>The prototype's "player" (fixture written to the scratchpad by the demo, never into the repo).</summary>
    public static AvatarProfile PlayerFixture()
    {
        var p = new AvatarProfile
        {
            Id = "6f1d2c7a-3b8e-4c55-9a10-2e7b5d4c9f01",
            DisplayName = "The Founder",
            Body = new BodyParams { HeightMm = 1800, BuildPermille = 560, ShoulderPermille = 650 },
            SkinTone = "#C68E6A", EyeColour = "#2E5A7A", Hair = HairStyle.TiedBack, HairColour = "#3B2A1E",
            FacialHair = FacialHair.FullBeard,
            Heraldry = new Heraldry { Primary = "#1C3F8C", Secondary = "#D9B23A", Pattern = HeraldryPattern.Chevron },
        };
        int[] m = { 300, 450, 350, 150, -100, 250, 0, -50, 200, 300, 0, 350, 100, 50 };
        Array.Copy(m, p.FaceMorphs, MorphCount);
        p.Clothing.Military.Variant = 1; p.Clothing.Royal.Variant = 0;
        return p;
    }
}

/// <summary>"#RRGGBB" ↔ XNA colour.</summary>
public static class Colour
{
    public static Microsoft.Xna.Framework.Color Parse(string hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#') throw new FormatException($"colour '{hex}' must be #RRGGBB");
        uint v = uint.Parse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        return new Microsoft.Xna.Framework.Color((int)(v >> 16) & 255, (int)(v >> 8) & 255, (int)v & 255);
    }
}
