"""Mesh helpers: triangulation, smooth normals, UV-split vertex buffers, tangents, level-set clipping, smoothing."""
import numpy as np


def quads_to_tris(Fq):
    Fq = np.asarray(Fq, dtype=np.int64)
    return np.concatenate([Fq[:, [0, 1, 2]], Fq[:, [0, 2, 3]]])


def face_normals(V, F):
    n = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    return n


def vertex_normals(V, F, nverts=None):
    """Area-weighted smooth normals over triangle list F (indices into V)."""
    nverts = len(V) if nverts is None else nverts
    fn = face_normals(V, F)
    N = np.zeros((nverts, 3))
    for k in range(3):
        np.add.at(N, F[:, k], fn)
    ln = np.linalg.norm(N, axis=1)
    zero = ln < 1e-20
    ln[zero] = 1
    N = N / ln[:, None]
    N[zero] = (0.0, 1.0, 0.0)
    return N


def split_by_uv(Fv, Fuv):
    """Unique (vertex, uv) corners -> new vertex ids. Returns (src_vertex, src_uv, F_new). Order: first use."""
    corners = np.stack([Fv.ravel(), Fuv.ravel()], 1)
    keys = corners[:, 0] * 10_000_000 + corners[:, 1]
    _, first, inv = np.unique(keys, return_index=True, return_inverse=True)
    # renumber in order of first appearance (stable and deterministic)
    order = np.argsort(first, kind="stable")
    rank = np.empty_like(order)
    rank[order] = np.arange(len(order))
    new_ids = rank[inv]
    src = corners[first[order]]
    return src[:, 0], src[:, 1], new_ids.reshape(Fv.shape)


def tangents(P, N, UV, F):
    """Per-vertex tangents (xyz) and handedness (w) from UV derivatives (Lengyel), Gram-Schmidt against N."""
    nv = len(P)
    t1 = np.zeros((nv, 3))
    t2 = np.zeros((nv, 3))
    p0, p1, p2 = P[F[:, 0]], P[F[:, 1]], P[F[:, 2]]
    w0, w1, w2 = UV[F[:, 0]], UV[F[:, 1]], UV[F[:, 2]]
    e1, e2 = p1 - p0, p2 - p0
    d1, d2 = w1 - w0, w2 - w0
    r = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
    r = np.where(np.abs(r) < 1e-14, 1e-14, r)
    sdir = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / r[:, None]
    tdir = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / r[:, None]
    for k in range(3):
        np.add.at(t1, F[:, k], sdir)
        np.add.at(t2, F[:, k], tdir)
    t = t1 - N * (N * t1).sum(1, keepdims=True)
    ln = np.linalg.norm(t, axis=1)
    bad = ln < 1e-12
    # fallback tangent: any vector perpendicular to N
    alt = np.cross(N, np.array([0.0, 1.0, 0.0]))
    alt2 = np.cross(N, np.array([1.0, 0.0, 0.0]))
    alt = np.where((np.linalg.norm(alt, axis=1) < 1e-6)[:, None], alt2, alt)
    t[bad] = alt[bad]
    ln2 = np.linalg.norm(t, axis=1, keepdims=True)
    t = np.where(ln2 > 1e-12, t / np.maximum(ln2, 1e-12), np.array([1.0, 0.0, 0.0]))
    w = np.where((np.cross(N, t) * t2).sum(1) < 0, -1.0, 1.0)
    return np.concatenate([t, w[:, None]], 1)


def adjacency(F, nverts):
    """Sparse vertex adjacency as (neighbour lists flattened): returns (rows, cols) unique undirected edges."""
    e = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    e = np.sort(e, 1)
    e = np.unique(e, axis=0)
    return e


def laplacian_smooth(V, F, iters=10, lam=0.5, mu=-0.53, fixed=None):
    """Taubin smoothing (lambda/mu) over the triangle mesh F. fixed: boolean mask of vertices not to move."""
    V = V.copy()
    E = adjacency(F, len(V))
    deg = np.zeros(len(V))
    np.add.at(deg, E[:, 0], 1)
    np.add.at(deg, E[:, 1], 1)
    deg[deg == 0] = 1
    for it in range(iters):
        for f in (lam, mu):
            S = np.zeros_like(V)
            np.add.at(S, E[:, 0], V[E[:, 1]])
            np.add.at(S, E[:, 1], V[E[:, 0]])
            L = S / deg[:, None] - V
            if fixed is not None:
                L[fixed] = 0
            V = V + f * L
    return V


def clip_mesh(F, field, attrs, keep_below=True):
    """Clips triangles F against the level set field == 0, keeping field <= 0 (or >= 0).

    attrs: dict name -> per-vertex array (interpolated linearly at new boundary vertices; integer-like arrays
    should be passed as float). Returns (F_new, attrs_new, boundary_vertex_mask). Every triangle that crosses the
    level set is cut along the straight segment between the edge crossings, so a smooth field gives a smooth hem.
    """
    s = field if keep_below else -field
    nv = len(s)
    new_attrs = {k: [np.asarray(v)] for k, v in attrs.items()}
    extra = []
    edge_cache = {}
    out = []
    boundary = []

    def cross(a, b):
        key = (a, b) if a < b else (b, a)
        if key in edge_cache:
            return edge_cache[key]
        t = s[a] / (s[a] - s[b])
        idx = nv + len(extra)
        extra.append((a, b, t))
        edge_cache[key] = idx
        boundary.append(idx)
        return idx

    inside = s <= 0
    for tri in F:
        a, b, c = int(tri[0]), int(tri[1]), int(tri[2])
        ins = inside[a], inside[b], inside[c]
        k = sum(ins)
        if k == 3:
            out.append((a, b, c))
        elif k == 0:
            continue
        else:
            # rotate so the odd vertex is first
            v = [a, b, c]
            m = [ins[0], ins[1], ins[2]]
            if k == 1:
                i = m.index(True)
            else:
                i = m.index(False)
            v = v[i:] + v[:i]
            p, q, r = v
            pq, pr = cross(p, q), cross(p, r)
            if k == 1:
                out.append((p, pq, pr))
            else:
                out.append((pq, q, r))
                out.append((pq, r, pr))
    for k, arr in new_attrs.items():
        base = arr[0]
        if extra:
            ea = np.array([e[0] for e in extra])
            eb = np.array([e[1] for e in extra])
            et = np.array([e[2] for e in extra])
            shape = (-1,) + (1,) * (base.ndim - 1)
            nvals = base[ea] * (1 - et.reshape(shape)) + base[eb] * et.reshape(shape)
            new_attrs[k] = np.concatenate([base, nvals])
        else:
            new_attrs[k] = base
    bmask = np.zeros(nv + len(extra), bool)
    bmask[boundary] = True
    return np.array(out, dtype=np.int64).reshape(-1, 3), new_attrs, bmask


def compact(F, attrs):
    """Drops unreferenced vertices (order of first use is kept stable by index)."""
    used = np.unique(F.ravel())
    remap = -np.ones(max(len(next(iter(attrs.values()))), 1), np.int64)
    remap[used] = np.arange(len(used))
    return remap[F], {k: np.asarray(v)[used] for k, v in attrs.items()}, used


def boundary_loops(F):
    """Ordered boundary loops (lists of vertex ids) of a manifold triangle mesh."""
    e = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    key = {}
    for a, b in e:
        key[(int(a), int(b))] = key.get((int(a), int(b)), 0) + 1
    border = {}
    for (a, b) in key:
        if (b, a) not in key:
            border[a] = b
    loops = []
    seen = set()
    for start in sorted(border):
        if start in seen:
            continue
        loop = [start]
        seen.add(start)
        cur = border[start]
        while cur != start and cur in border and cur not in seen:
            loop.append(cur)
            seen.add(cur)
            cur = border[cur]
        loops.append(loop)
    return loops
