namespace Sim.Ui.ViewModel;

/// <summary>
/// Shared world-space line→quad geometry (pure view-model; extracted from
/// RiverMesh at T1.8 so path polylines reuse it). Each segment becomes a quad
/// (two triangles, six vertices) around its axis, extended by half a width at
/// both ends so consecutive quads overlap and cover joints. MSAA smooths the
/// solid geometry at draw time (ADR-009).
/// </summary>
public static class LineGeometry
{
    public readonly record struct Vertex(double X, double Y);

    public static void AppendSegmentQuad(List<Vertex> vertices, Vertex a, Vertex b, double halfWidth)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0.0) return;
        double ux = dx / length, uy = dy / length; // unit axis
        double px = -uy, py = ux;                  // unit perpendicular

        double ax = a.X - ux * halfWidth, ay = a.Y - uy * halfWidth;
        double bx = b.X + ux * halfWidth, by = b.Y + uy * halfWidth;

        var a1 = new Vertex(ax + px * halfWidth, ay + py * halfWidth);
        var a2 = new Vertex(ax - px * halfWidth, ay - py * halfWidth);
        var b1 = new Vertex(bx + px * halfWidth, by + py * halfWidth);
        var b2 = new Vertex(bx - px * halfWidth, by - py * halfWidth);

        vertices.Add(a1); vertices.Add(b1); vertices.Add(a2); // triangle 1
        vertices.Add(a2); vertices.Add(b1); vertices.Add(b2); // triangle 2
    }
}
