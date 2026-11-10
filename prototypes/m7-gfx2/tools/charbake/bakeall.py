"""Assembles every output file of the chars bake (identities, outfits, clips, textures)."""
import numpy as np

import hashlib

import clips as clipmod
import materials
import outfits
import xform
from rig import INDEX
import hair
import lod
import teeth
import mhcore
import skin
import texio
from g2ch import Writer
from identities import IDENTITIES
from fields import Fields
from identity import Assets, Identity
from retarget import Retargeter


def solve_stature(spec, A):
    """Height macro that gives the requested stature (two bakes, linear interpolation, one refinement)."""
    if "stature" not in spec:
        return spec
    want = spec["stature"]
    s = dict(spec)

    def measure(h):
        s2 = dict(s)
        s2["macro"] = dict(s["macro"], height=h)
        items = mhcore.macro_targets(s2["macro"], A.targets)
        for mod, val in sorted(s.get("modifiers", {}).items()):
            items += mhcore.modifier_targets(mod, val)
        V = mhcore.apply_targets(A.mesh.V, A.targets, items)
        g = V[A.mesh.group_verts("joint-ground")].mean(0)[1]
        return float(V[A.mesh.group_verts("body"), 1].max() - g) / 10.0

    h0, h1 = 0.5, 0.6
    m0, m1 = measure(h0), measure(h1)
    h = h0 + (want - m0) * (h1 - h0) / (m1 - m0)
    mh = measure(h)
    h = h + (want - mh) * (h1 - h0) / (m1 - m0)
    s["macro"] = dict(s["macro"], height=float(np.clip(h, 0.0, 1.0)))
    return s


def masks_for(A, size):
    D = f"{A.raw}/mpfb2/src/mpfb/data/textures"
    m = {k: texio.load_gray(f"{D}/mpfb_{k}.jpg", size) for k in ("face", "lips", "eyelids", "ears", "inside-mouth")}
    m["redness"] = np.clip(m["ears"] * 0.8, 0, 1)
    return m


def bake_identity(spec, A, files):
    spec = solve_stature(spec, A)
    ident = Identity(spec, A)
    w = Writer()
    body = ident.body_mesh()
    names, parents, heads, tails = ident.skeleton()
    size = spec.get("texture", 1024)
    fl = Fields(ident, skin.fields_masks(A.raw, 1024))
    surf = hair.Surface(ident)
    paint = hair.paint_layers(ident, fl, surf, spec, size, np.random.default_rng(2000 + spec["seed"]))
    alb_levels, mask_levels = skin.skin_maps(spec, A.raw, body, fl, size, layers=paint)
    files[f"tex/{spec['id']}_skin.r1tx"] = texio.r1tx_bytes(alb_levels)
    files[f"tex/{spec['id']}_skinmask.r1tx"] = texio.r1tx_bytes(mask_levels)
    if "tex/skin_detail.r1tx" not in files:
        files["tex/skin_detail.r1tx"] = texio.r1tx_bytes(skin.detail_normal())
    eye_rgb, eye_a = skin.eye_texture(A.raw, spec["eyes"])
    files[f"tex/{spec['id']}_eye.r1tx"] = texio.r1tx_bytes(texio.mips_color(eye_rgb, eye_a))
    info = {
        "id": spec["id"], "kind": spec["kind"], "label": spec["label"], "stature_m": round(ident.height, 4),
        "macro": {k: round(v, 6) for k, v in spec["macro"].items()},
        "textures": {"skin": f"tex/{spec['id']}_skin.r1tx", "skin_mask": f"tex/{spec['id']}_skinmask.r1tx",
                     "skin_detail": "tex/skin_detail.r1tx", "eye": f"tex/{spec['id']}_eye.r1tx", "hair": "tex/hair_atlas.r1tx",
                     "fur": "tex/fur.r1tx", "hairshell": "tex/hair_lying.r1tx"},
        "hair_colour": spec["hair"]["colour"],
        "expressions": ["blink", "battlecry", "pain", "grim", "calm"],
        "sources": ["mh-core hm08 base mesh, targets, face pose units, default rig, eyes (CC0)",
                    "mpfb2 game_engine rig and weights, region masks (CC0)", "mhjs-data skin " + spec["skin"]["base"] + " (CC0 badge)",
                    "hair, beard, brows, teeth, garments: generated in code"],
        "clips": f"{spec['id']}__clips.g2ch",
    }
    if "fixture" in spec:
        info["fixture"] = spec["fixture"]
    w.info(info)
    w.skeleton(names, parents, heads, tails)
    socks = []
    for sname, (bone, centre, R) in sorted(outfits.socket_frames(ident).items()):
        bi = INDEX[bone]
        socks.append((sname, bi, centre - ident.heads[bone], xform.mat_to_quat(R)))
    w.sockets(socks)
    w.mesh(body)
    w.morphs("body", ident.body_morphs(body))
    eyes = ident.eye_meshes(eye_a)
    for em in eyes:
        w.mesh(em)
    for mesh, morphs in teeth.build(ident):
        w.mesh(mesh)
        w.morphs(mesh["name"], morphs)
    for grp, nm, col in (("helper-tongue", "tongue", (150, 64, 60, 255)),):
        mesh, morphs = ident.helper_mesh(grp, nm, "mouth", col)
        w.mesh(mesh)
        w.morphs(nm, morphs)
    # hair, beard, brows and lashes: cards generated in code
    rng = np.random.default_rng(1000 + spec["seed"])
    if "tex/hair_atlas.r1tx" not in files:
        files["tex/hair_atlas.r1tx"] = texio.r1tx_bytes(hair.atlas_levels())
    if "tex/fur.r1tx" not in files:
        files["tex/fur.r1tx"] = texio.r1tx_bytes(hair.fur_texture())
    if "tex/hair_lying.r1tx" not in files:
        files["tex/hair_lying.r1tx"] = texio.r1tx_bytes(hair.lying_texture())
    parts = hair.scalp_shells(ident, fl, surf, spec["hair"])
    if spec["hair"]["style"] == "tied_back":
        parts.append(hair.ponytail(ident, surf, hair.tie_point(fl), rng, 0))
    elif spec["hair"]["style"] == "medium":
        for mesh, morphs in hair.scalp_hair(ident, fl, surf, spec["hair"], rng):
            mesh["name"] = "hair_long"
            parts.append((mesh, morphs))
    parts += hair.beard_fur(ident, fl, surf, spec.get("beard"))
    parts += hair.brows(ident, fl, surf, rng, spec.get("brow_density", 1.0))
    parts += hair.lashes(ident)
    built = {"body": body, "eyes": eyes[0]}
    for mesh, morphs in parts:
        w.mesh(mesh)
        built[mesh["name"]] = mesh
        if morphs:
            w.morphs(mesh["name"], morphs)
    data = w.bytes()
    files[f"{spec['id']}.g2ch"] = data
    # the clips (CMU motion capture, PROTOTYPE-ONLY) go to their own file, so the identity file holds only CC0 and
    # generated data and can be swapped or shipped independently of the animation source
    cw = Writer()
    cw.info({"id": spec["id"] + "__clips", "identity": spec["id"], "identity_sha256": hashlib.sha256(data).hexdigest(),
             "status": "PROTOTYPE-ONLY (CMU motion capture, provisional terms)",
             "sources": ["CMU Graphics Lab Motion Capture Database via una-dinosauria/cmu-mocap (B. Hahne BVH conversion)"],
             "acknowledgement": "The data used in this project was obtained from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217."})
    rt = Retargeter(ident.heads, ident.tails, ident.parents)
    for c in clipmod.bake_clips(rt, ident.parents, ident.heads, A.cmu):
        cw.raw("CLIP", clipmod.clip_chunk(c))
    files[f"{spec['id']}__clips.g2ch"] = cw.bytes()
    return ident, built, hashlib.sha256(data).hexdigest()


def emblem_mean_hex(data):
    """Mean sRGB colour of an emblem texture's top level (the painted field dominates): the LOD's paint colour."""
    import struct
    w_, h_ = struct.unpack("<II", data[8:16])
    a = np.frombuffer(data[24:24 + w_ * h_ * 4], np.uint8).reshape(h_, w_, 4)[..., :3].astype(np.float64) / 255.0
    lin = texio.srgb_to_linear(a).reshape(-1, 3).mean(0)
    s = texio.linear_to_srgb(lin)
    return "#" + "".join(f"{int(round(c * 255)):02X}" for c in s)


def bake_outfit(spec, outfit, ident, built, ident_sha, files):
    b = outfits.Builder(ident)
    outfit["recipe"](b)
    w = Writer()
    for tex in outfit.get("textures", []):
        name, fn = tex
        if name not in files:
            files[name] = fn()
    for mname, mat in sorted(b.materials.items()):
        if mat.get("kind") == "paint" and mat.get("emblem") in files:
            mat["lod_colour"] = emblem_mean_hex(files[mat["emblem"]])
    # visible skin: identity body triangles not entirely under a garment (by a margin)
    body = built["body"]
    F = body["F"]
    cov = b.covered[body["src"]][F]
    vis = F[~cov.all(1)]
    # scalp hair under a helmet or hat: drop triangles inside the head covering (a level rim, or a helmet whose lower
    # edge varies with the azimuth, which also covers the beard under its cheek plates)
    rim = getattr(b, "hair_rim", None)
    edge = getattr(b, "helm_edge", None)
    hair_views = {}
    if rim is not None or edge is not None:
        eye_y = 0.5 * (ident.eye_centres[0][1] + ident.eye_centres[1][1])
        for hn in ("hair", "hair_lod", "hair_long") + (("beard",) if edge is not None else ()):
            if hn in built:
                hm = built[hn]
                T = hm["idx"].reshape(-1, 3)
                C = hm["pos"][T].mean(1)
                if edge is None:
                    hair_views[hn] = T[C[:, 1] < eye_y + rim - 0.004]
                else:
                    c, ey, fn = edge
                    a = np.abs(np.arctan2(C[:, 0] - c[0], C[:, 2] - c[2]))
                    hair_views[hn] = T[C[:, 1] < ey + fn(a) - 0.004]
    # LODs: merged single-material meshes at about 3,000 and 1,500 triangles (meshoptimizer)
    hair_lin = skin.hex_lin(spec["hair"]["colour"])
    lods = {}
    for lname, total in (("lod1", 3000), ("lod2", 1500)):
        m = lod.build(ident, built, b.meshes, b.materials, vis.ravel(), hair_views.get("hair"), hair_lin, total)
        m["name"] = lname
        lods[lname] = m
    info = {"id": outfit["id"], "identity": spec["id"], "identity_sha256": ident_sha, "label": outfit["label"],
            "era": outfit.get("era", ""), "force": outfit.get("force", ""),
            "materials": {k: v for k, v in sorted(b.materials.items())},
            "textures": {"fabric": "tex/fabric.r1tx", "wood": "tex/wood.r1tx", "trim": "tex/trim.r1tx"},
            "lod_triangles": {k: int(len(v["idx"]) // 3) for k, v in sorted(lods.items())}}
    shield = b.materials.get("shield_face")
    if shield and shield.get("emblem"):
        info["lod_emblem"] = shield["emblem"]
    for mname, mat in sorted(b.materials.items()):
        if "emblem" in mat:
            info["textures"]["emblem:" + mat["emblem"]] = mat["emblem"]
    w.info(info)
    for mesh, morphs in b.meshes:
        w.mesh(mesh)
        if morphs:
            w.morphs(mesh["name"], morphs)
    for lname in ("lod1", "lod2"):
        w.mesh(lods[lname])
    w.view("body", outfit["id"], vis.ravel())
    for hn, T in sorted(hair_views.items()):
        w.view(hn, outfit["id"], T.ravel())
    files[f"{spec['id']}__{outfit['id']}.g2ch"] = w.bytes()


def bake(raw, only):
    A = Assets(raw)
    files = {}
    files["tex/fabric.r1tx"] = texio.r1tx_bytes(materials.fabric())
    files["tex/wood.r1tx"] = texio.r1tx_bytes(materials.wood())
    files["tex/trim.r1tx"] = texio.r1tx_bytes(materials.trim_patterns())
    for spec in IDENTITIES:
        if only and spec["id"] not in only:
            continue
        ident, built, sha = bake_identity(spec, A, files)
        for outfit in spec.get("outfits", []):
            bake_outfit(spec, outfit, ident, built, sha, files)
    return files
