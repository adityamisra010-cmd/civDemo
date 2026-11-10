"""Outfit recipes (labelled design choices). Colours are albedos given as sRGB hex. Every recipe builds layers on an
outfits.Builder; nothing here reads or writes simulation state.

Palette READING (labelled): the G1 heraldry colours (#1C3F8C blue, #D9B23A gold, #8C1C1C red) are display colours for
flat-shaded placeholders. Dyed wool and linen of the periods shown are less saturated and darker than display
colours (woad and indigo blues, weld and ochre yellows, madder reds, undyed linen and wool), so cloth uses the
dyed equivalents below, in the same hue family as each force's heraldry; paint (shields) is a little brighter.
"""
import numpy as np

import kit
from fields import smoothstep

# cloth (dyed and undyed) -- albedos given as sRGB hex
LINEN = "#C2B391"
UNDYED = "#8E8068"
BONE = "#CFC5AE"
LEATHER = "#5A3C26"
DARK_LEATHER = "#2E2219"
WOOL_BROWN = "#5B4632"
WOAD = "#35476B"          # force A / the player's blue, as a woad-dyed wool
WOAD_DARK = "#273350"
OCHRE = "#A6823F"         # the player's gold, as a weld/ochre-dyed band
MADDER = "#7B2C24"        # force B red, as a madder-dyed wool
MADDER_DARK = "#56201B"
HOSE_DARK = "#36312C"
HOSE_BROWN = "#4B3D31"

# paint (shields), slightly brighter than dyed cloth
PAINT = {"blue": "#2B4373", "gold": "#C29A45", "red": "#93301F", "white": "#DAD3C1", "black": "#2A2622"}

FORCE_A = {"id": "A", "main": WOAD, "dark": WOAD_DARK, "trim": OCHRE, "paint": (PAINT["blue"], PAINT["gold"])}
FORCE_B = {"id": "B", "main": MADDER, "dark": MADDER_DARK, "trim": BONE, "paint": (PAINT["red"], PAINT["white"])}


def cloth(fabric, colour, trim=None, rough=0.85, pattern=0, emblem=None, emblem_box=None, sole=None):
    m = {"kind": "cloth", "fabric": fabric, "colour": colour, "rough": rough}
    if trim:
        m["colour2"] = trim
        m["pattern"] = pattern
    if emblem:
        m["emblem"] = emblem
        m["emblem_box"] = emblem_box
    if sole:
        m["colour3"] = sole
    return m


def metal(kind, rough=0.38, wear=0.4):
    return {"kind": "metal", "metal": kind, "rough": rough, "wear": wear}


WOOD = {"kind": "wood", "colour": "#7A5A3A", "rough": 0.7}
DARK_WOOD = {"kind": "wood", "colour": "#4A3524", "rough": 0.75}


def max_(a, b_):
    return np.maximum(a, b_)


def base_layers(b, legs="hose", legs_colour=HOSE_DARK, footwear="boots", foot_colour=DARK_LEATHER):
    """Undergarment (always: nothing anatomical is ever shown), legwear and footwear."""
    bc = b.bc
    b.trousers("braies", bc.waist_y + 0.02, b.thigh_len * 0.55, 0.003, cloth("linen", LINEN, rough=0.9))
    if legs == "hose":
        b.trousers("hose", bc.waist_y - 0.02, b.thigh_len + b.shin_len - 0.02, 0.0045, cloth("wool", legs_colour, rough=0.92))
    ank = bc.ankle_y
    shoe = cloth("leather", foot_colour, rough=0.55, sole="#1C1612")
    over = 0.0045 if legs == "hose" else None
    if footwear == "boots":
        b.footwear("boots", ank + 0.03, shoe, shaft_top=b.thigh_len + b.shin_len * 0.45, over=over)
    elif footwear == "tall_boots":
        b.footwear("boots", ank + 0.03, dict(shoe, rough=0.4), shaft_top=b.thigh_len - 0.03, over=over)
    elif footwear == "shoes":
        b.footwear("shoes", ank + 0.012, shoe)


def tunic(b, name, colour, sleeve, hem_y, fabric="linen", offset=0.005, trim=None, trim_w=0.018, pattern=0, neck=0.078,
          front_drop=0.0, emblem=None, skirt_clear=0.010, flare=0.03, skirt=True, armholes=False, smooth=2, folds=1.0,
          front_slit=0.0, rough=0.85, seed=0, gather=0.0):
    """A torso garment (shell of the skin, with sleeves or armholes) plus its fitted skirt from the hips down.
    gather: depth (m) of the folds the belt gathers in soft cloth between the chest and the waist (0 = none)."""
    bc = b.bc
    top_hem = bc.hip_y - 0.03
    region = b.f_neck(neck, front_drop)
    region = max_(region, b.f_noarms(0.3) if armholes else b.f_sleeve(sleeve))
    region = max_(region, b.f_above(top_hem))
    region = max_(region, b.f_torso(0.5))
    box = [0.0, bc.shoulder_y - 0.17, 0.20] if emblem else None
    mat = cloth(fabric, colour, trim, rough=rough, pattern=pattern, emblem=emblem, emblem_box=box)
    g = (bc.shoulder_y - 0.16, bc.waist_y - 0.035, gather, seed) if gather > 0 else None
    b.body_shell(name, region, offset, mat, smooth=smooth, trim=trim_w if trim else 0.0, gather=g)
    if skirt:
        b.skirt(name + "_skirt", top_hem + 0.06, hem_y, skirt_clear, flare, cloth(fabric, colour, trim, rough=rough, pattern=pattern),
                trim=trim_w if trim else 0.0, folds=folds, seed=seed, top_clear=offset + 0.006, front_slit=front_slit)


def belt(b, y, colour=LEATHER, width=0.042, offset=0.016):
    region = np.maximum(np.abs(b.V[:, 1] - y) - width / 2, b.f_torso(0.5))
    region = np.maximum(region, b.f_noarms(0.4))
    b.body_shell("belt", region, offset, cloth("leather", colour, rough=0.5), smooth=2, hide_margin=1.0, hem=0.003)


def mail_shirt(b, sleeve, hem_y, rough=0.42, wear=0.5):
    tunic(b, "mail", "#8A8C8E", sleeve, hem_y, fabric="mail", offset=0.017, skirt_clear=0.019, flare=0.03, smooth=3, folds=0.35)
    for m, _ in b.meshes:
        if m["name"] in ("mail", "mail_skirt"):
            m["material"] = "metal"
    b.materials["mail"] = metal("mail", rough, wear)
    b.materials["mail_skirt"] = metal("mail", rough, wear)


# ------------------------------------------------------------------ outfits
def soldier_medieval(b, force, kind, emblem_tex, seed=0):
    """kind: gambeson_spear, mail_spear, surcoat_sword, levy_spear."""
    bc = b.bc
    legs = HOSE_DARK if force["id"] == "A" else HOSE_BROWN
    base_layers(b, legs="hose", legs_colour=legs, footwear="boots" if kind != "levy_spear" else "shoes")
    if kind == "gambeson_spear":
        tunic(b, "gambeson", force["main"], b.arm_len + b.fore_len * 0.85, bc.knee_y + 0.12, fabric="quilted", offset=0.011,
              skirt_clear=0.014, flare=0.035, smooth=3, trim=force["dark"], trim_w=0.012, folds=0.7, seed=seed)
    elif kind == "levy_spear":
        tunic(b, "tunic", UNDYED, b.arm_len + b.fore_len * 0.9, bc.knee_y + 0.06, fabric="wool", offset=0.006, skirt_clear=0.010,
              flare=0.05, trim=force["main"], trim_w=0.016, pattern=1, folds=1.1, seed=seed, gather=0.005)
        # a padded jacket over the tunic (undyed, quilted), shorter
        tunic(b, "jack", "#7E6B4E", b.arm_len + b.fore_len * 0.6, bc.hip_y - 0.06, fabric="quilted", offset=0.014,
              skirt_clear=0.016, flare=0.02, smooth=3, folds=0.5, seed=seed + 1)
    else:
        tunic(b, "gambeson", "#7C6A4E", b.arm_len + b.fore_len * 0.85, bc.knee_y + 0.13, fabric="quilted", offset=0.011,
              skirt_clear=0.013, flare=0.035, smooth=3, folds=0.6, seed=seed)
        mail_shirt(b, b.arm_len + 0.04, bc.knee_y + 0.17)
    if kind == "surcoat_sword":
        tunic(b, "surcoat", force["main"], 0, bc.knee_y + 0.02, fabric="wool", offset=0.024, skirt_clear=0.028, flare=0.06,
              armholes=True, neck=0.09, emblem=emblem_tex, trim=force["trim"], trim_w=0.016, pattern=2, folds=1.2,
              front_slit=0.35, seed=seed, gather=0.006)
    belt(b, bc.waist_y - 0.035, offset=0.032 if kind in ("mail_spear", "surcoat_sword") else 0.022)
    helm = {"gambeson_spear": "kettle", "mail_spear": "nasal", "surcoat_sword": "nasal", "levy_spear": "cap"}[kind]
    kit.helmet(b, helm, metal("iron", 0.40, 0.5) if helm != "cap" else cloth("felt", "#4A3F33", rough=0.95))
    if kind == "surcoat_sword":
        kit.sword(b, metal("steel", 0.26, 0.3), cloth("leather", DARK_LEATHER, rough=0.6))
        kit.shield(b, "heater", emblem_tex, metal("iron", 0.45, 0.6), DARK_WOOD)
    else:
        kit.spear(b, metal("iron", 0.36, 0.5), WOOD)
        kit.shield(b, "round" if kind != "mail_spear" else "kite", emblem_tex, metal("iron", 0.45, 0.6), DARK_WOOD)


def commander(b, force, emblem_tex):
    bc = b.bc
    base_layers(b, legs="hose", legs_colour="#2B2B30", footwear="tall_boots", foot_colour="#231A14")
    tunic(b, "gambeson", "#5E4B36", b.arm_len + b.fore_len * 0.9, bc.knee_y + 0.12, fabric="quilted", offset=0.011,
          skirt_clear=0.013, smooth=3, folds=0.6, seed=5)
    mail_shirt(b, b.arm_len + b.fore_len * 0.6, bc.knee_y + 0.15, rough=0.30, wear=0.25)
    tunic(b, "surcoat", force["main"], 0, bc.knee_y - 0.08, fabric="wool", offset=0.024, skirt_clear=0.028, flare=0.08,
          armholes=True, neck=0.09, emblem=emblem_tex, trim=force["trim"], trim_w=0.026, pattern=3, folds=1.3, front_slit=0.3,
          seed=6, gather=0.006)
    belt(b, bc.waist_y - 0.035, colour="#3A2416", offset=0.032)
    kit.helmet(b, "kettle", metal("steel", 0.24, 0.15), plume=None)
    kit.sword(b, metal("steel", 0.2, 0.2), cloth("leather", "#1E1612", rough=0.5))
    kit.shield(b, "heater", emblem_tex, metal("gold", 0.35, 0.3), DARK_WOOD)


def avatar_era(b, era, emblem_tex):
    bc = b.bc
    if era == "ancient":
        base_layers(b, legs="none", footwear="shoes", foot_colour=LEATHER)
        # the linen tunic runs whole under the cuirass (no belt folds: they would only lie under the bronze); its
        # bordered neckline shows inside the cuirass's neck opening, its sleeves at the armholes
        tunic(b, "tunic", WOAD, 0.13, bc.knee_y + 0.10, fabric="linen", offset=0.0045, skirt_clear=0.008, flare=0.05,
              trim=OCHRE, trim_w=0.016, pattern=1, folds=1.2, seed=11)
        # bronze muscle cuirass: a smoothed shell of the torso 14 mm over the skin (at least 8 mm over the tunic) with
        # the skin's own weights, so the two layers move together. Its edges are single clean curves: the neck opening
        # (an ellipsoid dipping 2.5 cm at the front) and the hip line (a plane) are cut on a smoothed copy of the shell
        # itself, the armholes by the arm weight smoothed over the skin (40 passes, at 0.5, so the shoulders keep a
        # 10 cm strap); stray islands are dropped and the edge loops faired before the 6 mm rolled hem.
        nc = b.h["neck_01"] + np.array([0.0, -0.015, 0.0])
        y_hip = bc.waist_y - 0.09

        def plate_edges(Pf):
            d = Pf - nc
            dist = np.linalg.norm(d * np.array([1.0, 1.25, 1.1]), axis=1)
            front = smoothstep(-0.02, 0.06, d[:, 2])
            return np.maximum.reduce([(0.085 + 0.025 * front) - dist, d[:, 1] - 0.05, y_hip - Pf[:, 1]])
        region = np.maximum.reduce([b.f_armhole(0.5, iters=40), bc.headness - 0.3, bc.legness - 0.75])
        b.body_shell("cuirass", region, 0.014, metal("bronze", 0.32, 0.45), smooth=6, hide_margin=0.01, hem=0.006,
                     field=plate_edges, field_smooth=30, islands=True, fair=6)
        b.skirt("pteruges", bc.waist_y - 0.05, bc.knee_y + 0.16, 0.030, 0.05, cloth("leather", "#6B4A2E", rough=0.6),
                pteruges=14, folds=0.2, seed=12, top_clear=0.026)
        # greaves over the shins
        s0, s1 = b.thigh_len + 0.03, b.thigh_len + b.shin_len - 0.03
        legs = b.bc.leg_s()
        region = np.where(b.bc.legness > 0.3, np.maximum(s0 - legs, legs - s1), 1.0)
        region = np.maximum(region, -(b.N[:, 2] + 0.25) * 0.1)
        b.body_shell("greaves", region, 0.008, metal("bronze", 0.30, 0.4), smooth=2, hide_margin=1.0)
        kit.helmet(b, "crested", metal("bronze", 0.30, 0.5), plume="#7E2A20")
        kit.spear(b, metal("bronze", 0.35, 0.4), WOOD)
        kit.shield(b, "aspis", emblem_tex, metal("bronze", 0.30, 0.45), WOOD)
    elif era == "medieval":
        base_layers(b, legs="hose", legs_colour="#2F2B2A", footwear="boots")
        tunic(b, "gambeson", "#6E5B42", b.arm_len + b.fore_len * 0.9, bc.knee_y + 0.12, fabric="quilted", offset=0.011,
              skirt_clear=0.013, smooth=3, folds=0.6, seed=21)
        mail_shirt(b, b.arm_len + 0.05, bc.knee_y + 0.16, rough=0.36, wear=0.35)
        tunic(b, "surcoat", WOAD, 0, bc.knee_y + 0.0, fabric="wool", offset=0.024, skirt_clear=0.028, flare=0.06,
              armholes=True, neck=0.09, emblem=emblem_tex, trim=OCHRE, trim_w=0.016, pattern=2, folds=1.2, front_slit=0.3,
              seed=22, gather=0.006)
        belt(b, bc.waist_y - 0.035, offset=0.032)
        kit.helmet(b, "kettle", metal("iron", 0.36, 0.4))
        kit.sword(b, metal("steel", 0.24, 0.3), cloth("leather", DARK_LEATHER, rough=0.6))
        kit.shield(b, "heater", emblem_tex, metal("iron", 0.45, 0.5), DARK_WOOD)
    elif era == "early_modern":
        base_layers(b, legs="hose", legs_colour="#CFC6B0", footwear="tall_boots", foot_colour="#171311")
        tunic(b, "shirt", "#DCD6C8", b.arm_len + b.fore_len * 0.97, bc.hip_y - 0.05, fabric="linen", offset=0.004, skirt=False,
              neck=0.07)
        # long coat: sleeves to the wrist, skirts to the knee, open front below the chest, cuffs and edges trimmed
        V = b.V
        front = V[:, 2] > b.h["spine_03"][2] - 0.01
        slit = np.where(front & (V[:, 1] < bc.shoulder_y - 0.30), 0.03 - np.abs(V[:, 0]), -1.0)
        region = max_(b.f_neck(0.085, 0.05), b.f_sleeve(b.arm_len + b.fore_len * 0.92))
        region = max_(region, b.f_above(bc.hip_y - 0.03))
        region = max_(region, b.f_torso(0.5))
        region = max_(region, slit)
        coat = cloth("wool", WOAD_DARK, OCHRE, rough=0.9, pattern=3)
        b.body_shell("coat", region, 0.012, coat, smooth=3, trim=0.02)
        b.skirt("coat_skirt", bc.hip_y + 0.03, bc.knee_y - 0.02, 0.018, 0.09, coat, trim=0.02, folds=1.0, seed=31,
                front_slit=0.32, top_clear=0.018)
        belt(b, bc.waist_y - 0.02, colour="#1A1412", offset=0.024)
        kit.helmet(b, "tricorne", cloth("felt", "#1B1A1E", OCHRE, rough=0.95))
        kit.sword(b, metal("steel", 0.2, 0.2), cloth("leather", "#1A1412", rough=0.5), length=0.92)
