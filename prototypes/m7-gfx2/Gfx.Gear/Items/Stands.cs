using Gfx.Gear.Geometry;
using Gfx.Gear.Maps;
using Microsoft.Xna.Framework;

namespace Gfx.Gear.Items;

/// <summary>Display furniture for the kit gallery (not kit): a wooden post with a carved head form, and a weapon rack.</summary>
public static class Stands
{
    public static KitItem HeadPost(uint seed, double headY, Rig.BodyMeasure m)
    {
        var mesh = new KitMesh("post");
        var path = new List<Vector3d> { new(0, 0, 0), new(0, headY - 0.12, 0) };
        mesh.Append(Shapes.Tube(path, 0.025, 10, new UvRect(0, 0, 0.5f, 1), Color.White, up: new Vector3d(0, 0, 1), caps: true));
        // the head form: an egg sized from the measured head (no face)
        var prof = new List<Vector2d>();
        for (int i = 0; i <= 16; i++) { double a = Math.PI * (0.5 - i / 16.0); prof.Add(new Vector2d(Math.Cos(a), Math.Sin(a))); }
        var head = Shapes.Lathe(prof, 32, new UvRect(0.5f, 0, 1, 1), Color.White);
        float hw = m.TempleHalfWidth, hl = 0.5f * (m.BrowZ - m.SkullBackZ), hh = (float)(m.CrownY - m.HeadCentre.Y);
        head.Transform(Matrix.CreateScale(hw, Math.Max(hh, 0.09f), hl) * Matrix.CreateTranslation(0, (float)headY, 0));
        mesh.Append(head);
        mesh.Finish(50f);
        var item = new KitItem { Id = $"post#{seed}", Kind = "stand.post", Label = "Display post" };
        item.Parts.Add(new KitPart { Name = "post", Mesh = mesh, Maps = "wood" });
        item.Maps["wood"] = Wood(seed, mesh);
        return item;
    }

    public static KitItem Rack(uint seed, double width, int pegs)
    {
        var mesh = new KitMesh("rack");
        foreach (double x in new[] { -width / 2, width / 2 })
            mesh.Append(Shapes.Tube(new List<Vector3d> { new(x, 0, 0), new(x, 1.35, 0) }, 0.035, 10, new UvRect(0, 0, 0.5f, 1), Color.White, up: new Vector3d(0, 0, 1), caps: true));
        foreach (double y in new[] { 0.35, 1.25 })
            mesh.Append(Shapes.Tube(new List<Vector3d> { new(-width / 2, y, 0), new(width / 2, y, 0) }, 0.028, 10, new UvRect(0.5f, 0, 1, 1), Color.White, up: new Vector3d(0, 1, 0), caps: true));
        mesh.Finish(50f);
        var item = new KitItem { Id = $"rack#{seed}", Kind = "stand.rack", Label = "Weapon rack" };
        item.Parts.Add(new KitPart { Name = "rack", Mesh = mesh, Maps = "wood" });
        item.Maps["wood"] = Wood(seed, mesh);
        _ = pegs;
        return item;
    }

    static MapSet Wood(uint seed, KitMesh mesh)
    {
        var maps = new MapSet("wood", 512, 512);
        maps.PaintMesh(mesh, 1, s => Materials.Wood(s.Position.X + s.Position.Z, s.Position.Y, seed, new Vector3(0.36f, 0.25f, 0.14f), new Vector3(0.17f, 0.11f, 0.06f)));
        return maps.Finish(4);
    }
}
