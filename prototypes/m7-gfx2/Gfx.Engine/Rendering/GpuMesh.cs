using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Engine.Rendering;

/// <summary>CPU mesh (any vertex type) with 32-bit indices.</summary>
public sealed class MeshData<TVertex> where TVertex : struct, IVertexType
{
    public TVertex[] Vertices { get; }
    public int[] Indices { get; }
    public MeshData(TVertex[] vertices, int[] indices) { Vertices = vertices; Indices = indices; }
    public int TriangleCount => Indices.Length / 3;
}

/// <summary>Uploaded mesh. Draw with <see cref="DrawInstanced{TInstance}"/> (all engine effects are instanced).</summary>
public sealed class GpuMesh : IDisposable
{
    public VertexBuffer Vertices { get; }
    public IndexBuffer Indices { get; }
    public int TriangleCount { get; }
    public BoundingSphere LocalBounds { get; }

    GpuMesh(VertexBuffer vb, IndexBuffer ib, int tris, BoundingSphere b) { Vertices = vb; Indices = ib; TriangleCount = tris; LocalBounds = b; }

    public static GpuMesh Create<TVertex>(GraphicsDevice gd, MeshData<TVertex> m, Func<TVertex, Vector3> position) where TVertex : struct, IVertexType
    {
        var vb = new VertexBuffer(gd, typeof(TVertex), m.Vertices.Length, BufferUsage.WriteOnly); vb.SetData(m.Vertices);
        var ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, m.Indices.Length, BufferUsage.WriteOnly); ib.SetData(m.Indices);
        var pts = new Vector3[m.Vertices.Length]; for (int i = 0; i < pts.Length; i++) pts[i] = position(m.Vertices[i]);
        return new GpuMesh(vb, ib, m.TriangleCount, pts.Length > 0 ? BoundingSphere.CreateFromPoints(pts) : default);
    }
    public static GpuMesh Create(GraphicsDevice gd, MeshData<VertexPNC> m) => Create(gd, m, v => v.Position);
    public static GpuMesh Create(GraphicsDevice gd, MeshData<VertexSkinned> m) => Create(gd, m, v => v.Position);

    /// <summary>
    /// Binds mesh (stream 0) + instances (stream 1, frequency 1), applies <paramref name="effect"/> via
    /// <see cref="RenderContext.Apply"/> (standard params + samplers) and issues one instanced draw.
    /// </summary>
    public void DrawInstanced<TInstance>(RenderContext rc, Effect effect, InstanceBuffer<TInstance> instances, int count = -1)
        where TInstance : struct, IVertexType
    {
        if (count < 0) count = instances.Count;
        if (count == 0) return;
        var gd = rc.Device;
        gd.SetVertexBuffers(new VertexBufferBinding(Vertices, 0, 0), new VertexBufferBinding(instances.Buffer, 0, 1));
        gd.Indices = Indices;
        rc.Apply(effect);
        gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, TriangleCount, count);
        rc.Stats.RecordDraw(TriangleCount * count, count);
    }

    public void Dispose() { Vertices.Dispose(); Indices.Dispose(); }
}

/// <summary>Growable dynamic per-instance vertex buffer.</summary>
public sealed class InstanceBuffer<T> : IDisposable where T : struct, IVertexType
{
    readonly GraphicsDevice _gd;
    public DynamicVertexBuffer Buffer { get; private set; }
    public int Count { get; private set; }

    public InstanceBuffer(GraphicsDevice gd, int capacity = 256)
    {
        _gd = gd;
        Buffer = new DynamicVertexBuffer(gd, typeof(T), Math.Max(1, capacity), BufferUsage.WriteOnly);
    }

    /// <summary>Uploads the first <paramref name="count"/> elements (all when negative).</summary>
    public void Set(T[] data, int count = -1)
    {
        if (count < 0) count = data.Length;
        if (count > Buffer.VertexCount)
        {
            int cap = Math.Max(count, Buffer.VertexCount * 2);
            Buffer.Dispose();
            Buffer = new DynamicVertexBuffer(_gd, typeof(T), cap, BufferUsage.WriteOnly);
        }
        Count = count;
        if (count > 0) Buffer.SetData(data, 0, count, SetDataOptions.Discard);
    }

    public void Dispose() => Buffer.Dispose();
}
