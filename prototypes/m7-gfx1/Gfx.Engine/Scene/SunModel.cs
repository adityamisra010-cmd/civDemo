using Microsoft.Xna.Framework;

namespace Gfx.Engine.Scene;

/// <summary>Lighting state for one frame. All colours are LINEAR (HDR intensities, not 0..255).</summary>
public readonly record struct SunState(
    Vector3 DirectionToLight, Vector3 LightColor, Vector3 AmbientSky, Vector3 AmbientGround,
    Vector3 SkyZenith, Vector3 SkyHorizon, Vector3 FogColor, float SunElevationRadians)
{
    public bool IsNight => SunElevationRadians < -0.05f;
}

/// <summary>
/// Time of day → sun direction/colour/ambient. PRESENTATION ONLY: the hour is a visual parameter (never a sim
/// gate, Law 4). Axes: +X east, −Z north, +Y up. Below the horizon the key light switches to a dim moon.
/// </summary>
public static class SunModel
{
    public static SunState At(float hourOfDay, float latitudeDeg = 40f, float declinationDeg = 12f)
    {
        float H = MathHelper.ToRadians((hourOfDay - 12f) * 15f);
        float lat = MathHelper.ToRadians(latitudeDeg), dec = MathHelper.ToRadians(declinationDeg);
        float sinE = MathF.Sin(lat) * MathF.Sin(dec) + MathF.Cos(lat) * MathF.Cos(dec) * MathF.Cos(H);
        float elev = MathF.Asin(Math.Clamp(sinE, -1f, 1f));
        // azimuth measured from north, clockwise (east positive)
        float az = MathF.Atan2(MathF.Sin(H), MathF.Cos(H) * MathF.Sin(lat) - MathF.Tan(dec) * MathF.Cos(lat)) + MathF.PI;
        var toSun = Vector3.Normalize(new Vector3(MathF.Cos(elev) * MathF.Sin(az), MathF.Sin(elev), -MathF.Cos(elev) * MathF.Cos(az)));

        float day = Smooth(-0.05f, 0.25f, sinE);    // 0 night → 1 full day
        float low = 1f - Smooth(0.0f, 0.45f, sinE);  // golden-hour warmth
        var warm = new Vector3(1.0f, 0.42f, 0.16f); var white = new Vector3(1.0f, 0.95f, 0.86f);
        Vector3 sunCol = Vector3.Lerp(white, warm, low) * (3.2f * Smooth(-0.02f, 0.12f, sinE));

        var zenithDay = new Vector3(0.10f, 0.25f, 0.62f); var horizonDay = new Vector3(0.55f, 0.68f, 0.85f);
        var horizonDusk = new Vector3(0.95f, 0.45f, 0.22f); var zenithNight = new Vector3(0.004f, 0.008f, 0.025f);
        var horizonNight = new Vector3(0.015f, 0.02f, 0.045f);
        Vector3 zenith = Vector3.Lerp(zenithNight, zenithDay, day);
        Vector3 horizon = Vector3.Lerp(horizonNight, Vector3.Lerp(horizonDay, horizonDusk, low * day), Smooth(-0.1f, 0.1f, sinE));
        Vector3 ambSky = Vector3.Lerp(new Vector3(0.02f, 0.03f, 0.06f), new Vector3(0.20f, 0.27f, 0.40f), day);
        Vector3 ambGround = ambSky * new Vector3(0.45f, 0.40f, 0.32f);

        Vector3 dir = toSun, col = sunCol;
        if (sinE < -0.02f)
        {   // moonlight: opposite the sun, held at least 20° up
            var m = -toSun; float me = MathF.Max(MathF.Asin(Math.Clamp(m.Y, -1f, 1f)), MathHelper.ToRadians(20f));
            var flat = Vector3.Normalize(new Vector3(m.X, 0, m.Z) + new Vector3(1e-4f, 0, 0));
            dir = Vector3.Normalize(flat * MathF.Cos(me) + Vector3.Up * MathF.Sin(me));
            col = new Vector3(0.10f, 0.13f, 0.22f) * (1f - day);
        }
        return new SunState(dir, col, ambSky, ambGround, zenith, horizon, Vector3.Lerp(horizon, zenith, 0.25f), elev);
    }

    static float Smooth(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3 - 2 * t); }
}
