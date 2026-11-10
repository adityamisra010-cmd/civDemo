using Gfx.Proc.Authoritative;
using Gfx.Proc.Environment.Trees;
using Gfx.Proc.Presentation;
using Microsoft.Xna.Framework;

namespace Gfx.Proc.Environment.Structures;

/// <summary>Where a settlement stands and what the camera may want from it.</summary>
public sealed record SettlementPlan(string Kind, Vector2 Centre, float Radius, Vector2 Gate, Vector2 GateOut, IReadOnlyList<Vector2> Outline,
    IReadOnlyList<Exclusion> Exclusions, float WalkY = 0)
{
    /// <summary>A point inside, on the main street, for interior shots.</summary>
    public Vector2 Inside { get; init; }
}

/// <summary>
/// Settlement planning (stage env; PRESENTATION ONLY: the battle grid has no settlement cells, so these are set
/// dressing placed on it by a read-only search; in the full game the campaign settlement would place them). The site
/// search scores flatness, dry open ground and the distance to water; the layout is a seeded, parametric plan.
/// </summary>
public static class Settlements
{
    static float H01(uint seed, int a, int b) => PNoise.U01(PNoise.Hash(a, b, seed));

    /// <summary>The flattest dry, open disc of radius r, preferring ground 60–200 m from water and near the centre.</summary>
    public static Vector2 FindSite(TerrainMeshData m, WaterField water, float r, float maxFromCentre, IReadOnlyList<Exclusion> avoid, float flatness = 22f)
    {
        float best = float.MaxValue; var bc = Vector2.Zero;
        for (float cz = -maxFromCentre; cz <= maxFromCentre; cz += 12)
            for (float cx = -maxFromCentre; cx <= maxFromCentre; cx += 12)
            {
                var c = new Vector2(cx, cz);
                if (MathF.Abs(cx) > m.HalfExtentX - r - 30 || MathF.Abs(cz) > m.HalfExtentZ - r - 30) continue;
                bool bad = false;
                foreach (var e in avoid) if (Vector2.Distance(e.Centre, c) < e.Radius + r + 20) bad = true;
                if (bad) continue;
                float lo = float.MaxValue, hi = float.MinValue, pen = 0;
                for (float dz = -r; dz <= r; dz += 8)
                    for (float dx = -r; dx <= r; dx += 8)
                    {
                        if (dx * dx + dz * dz > r * r) continue;
                        float x = cx + dx, z = cz + dz;
                        if (m.IsWet(x, z)) { pen += 400; continue; }
                        var cls = m.ClassAt(x, z);
                        pen += cls switch { TerrainClass.Mountain => 40, TerrainClass.Forest or TerrainClass.Jungle => 6, TerrainClass.Water => 400, _ => 0 };
                        float h = m.HeightAt(x, z); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
                    }
                // distance to the nearest water (coarse)
                float wd = 400;
                for (float a = 0; a < MathHelper.TwoPi; a += MathHelper.TwoPi / 16)
                    for (float d = r; d < 400; d += 16)
                    {
                        float x = cx + MathF.Cos(a) * d, z = cz + MathF.Sin(a) * d;
                        if (m.IsWet(x, z)) { wd = MathF.Min(wd, d); break; }
                    }
                float waterPen = wd < r + 40 ? (r + 40 - wd) * 4 : wd > 220 ? (wd - 220) * 0.4f : 0;
                float sc = pen + (hi - lo) * flatness + waterPen + c.Length() * 0.12f;
                if (sc < best) { best = sc; bc = c; }
            }
        return bc;
    }

    /// <summary>
    /// An Iron Age palisaded village (temperate): an irregular ring palisade of about 46 m radius with a timber gate
    /// tower facing the nearest road (or the map centre), roundhouses of 4–7 m radius around an open yard with their
    /// doors to the yard, granaries, fenced pens and haystacks.
    /// </summary>
    public static SettlementPlan IronAgeVillage(TerrainMeshData m, WaterField water, uint seed, StructureKit? kit)
    {
        const float R = 46f;
        var c = FindSite(m, water, R + 8, 300, Array.Empty<Exclusion>());
        // gate towards the nearest road cell, else towards the map centre
        Vector2 toward = -c;
        float bestRoad = float.MaxValue;
        for (float a = 0; a < MathHelper.TwoPi; a += MathHelper.TwoPi / 48)
            for (float d = R; d < 260; d += 6)
            {
                float x = c.X + MathF.Cos(a) * d, z = c.Y + MathF.Sin(a) * d;
                if (m.ClassAt(x, z) == TerrainClass.Road) { if (d < bestRoad) { bestRoad = d; toward = new Vector2(MathF.Cos(a), MathF.Sin(a)); } break; }
            }
        var gateDir = toward.LengthSquared() > 1e-6f ? Vector2.Normalize(toward) : Vector2.UnitX;
        float gateAng = MathF.Atan2(gateDir.Y, gateDir.X);
        var ring = new List<Vector2>();
        int n = 64;
        for (int i = 0; i < n; i++)
        {
            float a = gateAng + i * MathHelper.TwoPi / n;
            float rr = R * (1f + 0.07f * (PNoise.Fbm(MathF.Cos(a) * 1.3f + 5f, MathF.Sin(a) * 1.3f, seed ^ 0x11u, 2) - 0.5f) * 2f);
            ring.Add(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rr);
        }
        var gate = ring[0];
        var plan = new SettlementPlan("iron-age-village", c, R, gate, gateDir, ring,
            new[] { new Exclusion(c, R + 4f) }) { Inside = c + gateDir * (R * 0.35f) };
        if (kit == null) return plan;

        kit.Palisade(ring, 3.4f, gate, 3.6f);
        kit.IronAgeGate(gate, gateDir, 3.6f);
        // roundhouses around the yard, doors facing the centre (or the morning sun side)
        var houses = new List<(Vector2 p, float r)>();
        int count = 11;
        for (int i = 0; i < count; i++)
        {
            float a = gateAng + 0.55f + i * (MathHelper.TwoPi - 1.1f) / (count - 1) + (H01(seed, i, 1) - 0.5f) * 0.18f;
            float rr = 4.2f + 2.6f * H01(seed, i, 2);
            float dist = R - rr - 6.5f - 6f * H01(seed, i, 3);
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * dist;
            bool clash = false;
            foreach (var (q, qr) in houses) if (Vector2.Distance(p, q) < rr + qr + 2.2f) clash = true;
            if (clash) continue;
            houses.Add((p, rr));
            var door = Vector2.Normalize(c - p + new Vector2(0.4f, 0.6f) * 6f);
            var daub = new Vector3(0.95f + 0.1f * H01(seed, i, 4), 0.95f, 0.9f + 0.1f * H01(seed, i, 5));
            kit.Roundhouse(p, rr, door, daub, new Vector3(1f, 0.97f, 0.92f));
        }
        // a great house near the centre
        var hall = c - gateDir * 9f;
        kit.Roundhouse(hall, 8.2f, gateDir, new Vector3(1.0f, 0.97f, 0.92f), new Vector3(1f));
        houses.Add((hall, 8.2f));
        // granaries, pens, haystacks in the gaps
        for (int i = 0; i < 4; i++)
        {
            float a = gateAng + 1.2f + i * 1.45f;
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (R * 0.55f);
            bool clash = false;
            foreach (var (q, qr) in houses) if (Vector2.Distance(p, q) < qr + 3.5f) clash = true;
            if (clash) continue;
            var ax = Vector2.Normalize(new Vector2(MathF.Cos(a + 0.7f), MathF.Sin(a + 0.7f)));
            if (i % 2 == 0) kit.Granary(p, ax, new Vector3(0.95f));
            else
            {
                var pen = new List<Vector2>();
                for (int k = 0; k <= 6; k++) { float b = a + k * 0.5f; pen.Add(p + new Vector2(MathF.Cos(b), MathF.Sin(b)) * 4.5f); }
                kit.Fence(pen);
                kit.Haystack(p + new Vector2(1.5f, 0.5f), 1.3f, new Vector3(1.05f, 1.0f, 0.9f));
            }
            houses.Add((p, 3.5f));
        }
        return plan;
    }

    /// <summary>
    /// A Classical walled town (arid): a rectangular circuit of about 128 × 96 m with chamfered corners, curtain walls
    /// 7 m to the wall walk with crenellated parapets, roofed square towers every 32 m or so, a main gate with two
    /// towers and a round-arched gateway; inside an orthogonal street grid with paved streets, plastered houses with
    /// tile roofs, and a temple on a podium facing the agora.
    /// </summary>
    public static SettlementPlan ClassicalTown(TerrainMeshData m, WaterField water, uint seed, StructureKit? kit)
    {
        var half = new Vector2(64f, 48f);
        var c = FindSite(m, water, 70f, 330, Array.Empty<Exclusion>(), 60f);
        // orientation: the gate side faces the map centre; town axes aligned with that direction
        var toCentre = -c; if (toCentre.LengthSquared() < 1) toCentre = Vector2.UnitY;
        var fwd = Vector2.Normalize(toCentre);            // +z of the town (gate side)
        var ax = new Vector2(fwd.Y, -fwd.X);              // +x of the town
        Vector2 W(float x, float z) => c + ax * x + fwd * z;
        var (lo, hi) = (float.MaxValue, float.MinValue); float sum = 0; int cnt = 0;
        for (float z = -half.Y; z <= half.Y; z += 8) for (float x = -half.X; x <= half.X; x += 8) { float h = m.HeightAt(W(x, z).X, W(x, z).Y); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h); sum += h; cnt++; }
        // the wall walk about 6.5 m above the mean ground of the circuit, never less than 3 m above its highest ground
        float walk = MathF.Max(sum / cnt + 6.5f, hi + 3f);
        // circuit (counter-clockwise seen from above: outer face on the left of each edge)
        float ch = 9f;
        var outline = new List<Vector2>
        {
            W(-half.X + ch, half.Y), W(-half.X, half.Y - ch), W(-half.X, -half.Y + ch), W(-half.X + ch, -half.Y),
            W(half.X - ch, -half.Y), W(half.X, -half.Y + ch), W(half.X, half.Y - ch), W(half.X - ch, half.Y),
        };
        var gate = W(0, half.Y);
        var plan = new SettlementPlan("classical-town", c, half.Length(), gate, fwd, outline,
            new[] { new Exclusion(c, half.Length() + 6f) }, walk) { Inside = W(0, 8) };
        if (kit == null) return plan;
        var stone = new Vector3(1.0f, 0.97f, 0.9f);
        // walls between the outline points; the gate gap on the front edge (last → first point)
        for (int i = 0; i < outline.Count; i++)
        {
            var a = outline[i]; var b = outline[(i + 1) % outline.Count];
            // the outline runs so that the outer face (see StructureKit.Wall) of each edge a→b faces outwards
            if (i == outline.Count - 1)
            {
                // front edge with the gate: two pieces either side of the gatehouse
                kit.CurtainWall(a, gate + ax * 7.5f, walk, 3.0f, stone);
                kit.CurtainWall(gate - ax * 7.5f, b, walk, 3.0f, stone);
                continue;
            }
            kit.CurtainWall(a, b, walk, 3.0f, stone);
        }
        kit.TownGate(gate, fwd, 4.4f, walk, 3.0f, stone);
        // towers along the long sides and at the chamfers
        foreach (var (p, o) in new[]
        {
            (W(-half.X, 0), -ax), (W(half.X, 0), ax), (W(-half.X * 0.5f, -half.Y), -fwd), (W(half.X * 0.5f, -half.Y), -fwd), (W(0, -half.Y), -fwd),
            (W(-half.X * 0.55f, half.Y), fwd), (W(half.X * 0.55f, half.Y), fwd),
            (W(-half.X + ch * 0.5f, half.Y - ch * 0.5f), Vector2.Normalize(fwd - ax)), (W(half.X - ch * 0.5f, half.Y - ch * 0.5f), Vector2.Normalize(fwd + ax)),
            (W(-half.X + ch * 0.5f, -half.Y + ch * 0.5f), Vector2.Normalize(-fwd - ax)), (W(half.X - ch * 0.5f, -half.Y + ch * 0.5f), Vector2.Normalize(-fwd + ax)),
        })
            kit.Tower(p + o * 2.2f, o, 7.0f, walk, stone);
        // streets: the main street from the gate, two cross streets
        var street = new Vector3(1.0f);
        kit.Street(W(0, half.Y - 2), W(0, -half.Y + 4), 6f, street);
        kit.Street(W(-half.X + 4, 14), W(half.X - 4, 14), 5f, street);
        kit.Street(W(-half.X + 4, -18), W(half.X - 4, -18), 5f, street);
        // temple on the agora (west of the main street, facing the street)
        kit.Temple(W(-26, -2), Vector2.Normalize(fwd), 6, 9);
        // houses in insulae, east side and the blocks behind the walls
        // lime washes: white, ochre, pale rose, buff (multiplying the plaster set's 0.53 mean albedo)
        var plaster = new[] { new Vector3(1.0f, 0.98f, 0.92f), new Vector3(1.0f, 0.86f, 0.62f), new Vector3(1.0f, 0.84f, 0.76f), new Vector3(0.96f, 0.95f, 0.90f), new Vector3(1.0f, 0.92f, 0.78f) };
        int k = 0;
        foreach (var (x0, x1, z0, z1) in new[] { (6f, 58f, 19f, 42f), (6f, 58f, -13f, 9f), (6f, 58f, -42f, -23f), (-58f, -6f, 19f, 42f), (-58f, -6f, -42f, -23f) })
        {
            float x = x0;
            while (x < x1 - 6)
            {
                float w = 7f + 5f * H01(seed, k, 1);
                if (x + w > x1) w = x1 - x;
                float d = MathF.Min(z1 - z0, 9f + 6f * H01(seed, k, 2));
                bool front = H01(seed, k, 3) < 0.5f;
                float zc = front ? z1 - d / 2 : z0 + d / 2;
                var hc = W(x + w / 2, zc);
                int floors = H01(seed, k, 4) < 0.45f ? 2 : 1;
                kit.House(hc, ax, new Vector2(w / 2 - 0.4f, d / 2 - 0.4f), floors, plaster[k % plaster.Length], new Vector3(0.95f + 0.1f * H01(seed, k, 5), 0.95f, 0.95f), front ? 2 : 0, H01(seed, k, 6) < 0.5f);
                // a second house behind on deep blocks
                if (z1 - z0 - d > 9)
                {
                    float d2 = MathF.Min(z1 - z0 - d - 1.5f, 10f);
                    var hc2 = W(x + w / 2, front ? z0 + d2 / 2 : z1 - d2 / 2);
                    kit.House(hc2, ax, new Vector2(w / 2 - 0.4f, d2 / 2 - 0.4f), 1, plaster[(k + 2) % plaster.Length], new Vector3(1f), front ? 0 : 2, true);
                }
                x += w + 1.2f + 2.5f * H01(seed, k, 7);
                k++;
            }
        }
        return plan;
    }
}
