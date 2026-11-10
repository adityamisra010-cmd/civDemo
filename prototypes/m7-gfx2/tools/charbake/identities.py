"""The identities of stage chars. Values are deliberate, documented choices (LABELLED FIXTURES), not random.

The avatar starts from G1's AvatarProfile.PlayerFixture() (Gfx.Avatar/AvatarProfile.cs), copied verbatim into
PLAYER_FIXTURE; Gfx.Tests checks that the copy still equals the C# fixture's JSON. The mapping from the fixture's
14 face channels and body integers to MakeHuman modifiers is a READING made here (G1's channels were invented for
G1's procedural head), documented in FACE_MAP and avatar_spec().
"""
import mhcore

# Verbatim copy of AvatarProfile.PlayerFixture() (identity part); checked by Gfx.Tests (CharsTests).
PLAYER_FIXTURE = {
    "id": "6f1d2c7a-3b8e-4c55-9a10-2e7b5d4c9f01",
    "displayName": "The Founder",
    "body": {"heightMm": 1800, "buildPermille": 560, "shoulderPermille": 650},
    "faceMorphs": [300, 450, 350, 150, -100, 250, 0, -50, 200, 300, 0, 350, 100, 50],
    "skinTone": "#C68E6A",
    "eyeColour": "#2E5A7A",
    "hair": "TiedBack",
    "hairColour": "#3B2A1E",
    "facialHair": "FullBeard",
}

# G1 face channel -> MakeHuman regional modifiers (each value -1..1 is scaled by the gain)
FACE_MAP = [
    ("JawWidth", ["chin/chin-width:decr|incr", "head/head-square"]),
    ("Cheekbones", ["cheek/l-cheek-bones:decr|incr", "cheek/r-cheek-bones:decr|incr"]),
    ("BrowRidge", ["eyebrows/eyebrows-trans:backward|forward", "forehead/forehead-nubian:decr|incr"]),
    ("NoseLength", ["nose/nose-scale-vert:decr|incr"]),
    ("NoseWidth", ["nose/nose-scale-horiz:decr|incr"]),
    ("NoseBridge", ["nose/nose-hump:decr|incr"]),
    ("EyeSize", ["eyes/l-eye-scale:decr|incr", "eyes/r-eye-scale:decr|incr"]),
    ("EyeSpacing", ["eyes/l-eye-trans:in|out", "eyes/r-eye-trans:in|out"]),
    ("LipFullness", ["mouth/mouth-lowerlip-volume:decr|incr", "mouth/mouth-upperlip-volume:decr|incr"]),
    ("ChinLength", ["chin/chin-height:decr|incr"]),
    ("EarSize", ["ears/l-ear-scale:decr|incr", "ears/r-ear-scale:decr|incr"]),
    ("AgeLines", ["head/head-age:decr|incr"]),
    ("ForeheadHeight", ["forehead/forehead-scale-vert:decr|incr"]),
    ("HeadWidth", ["head/head-scale-horiz:decr|incr"]),
]
FACE_GAIN = 0.7


def macro(gender=1.0, years=30.0, muscle=0.55, weight=0.5, height=0.5, proportions=0.6, african=0.0, asian=0.0, caucasian=1.0):
    return {"gender": gender, "age": mhcore.age_slider(years), "muscle": muscle, "weight": weight, "height": height,
            "proportions": proportions, "african": african, "asian": asian, "caucasian": caucasian}


def avatar_spec():
    p = PLAYER_FIXTURE
    mods = {}
    for k, (name, targets) in enumerate(FACE_MAP):
        v = p["faceMorphs"][k] / 1000.0 * FACE_GAIN
        for t in targets:
            if ":" not in t and v < 0:
                continue  # single-ended modifiers only take positive values
            mods[t] = mods.get(t, 0.0) + v
    build = p["body"]["buildPermille"] / 1000.0
    mods["torso/torso-vshape:decr|incr"] = (p["body"]["shoulderPermille"] - 500) / 500.0
    years = 30.0 + 25.0 * p["faceMorphs"][11] / 1000.0  # AgeLines 350 -> 38.75 y
    return {
        "id": "avatar", "seed": 1, "kind": "avatar", "label": "Player avatar (G1 PlayerFixture identity)",
        "fixture": p,
        "macro": macro(years=years, muscle=0.5 + 0.6 * (build - 0.5), weight=0.5 + (build - 0.5), proportions=0.65,
                       african=0.1, asian=0.2, caucasian=0.7),
        "stature": p["body"]["heightMm"] / 1000.0,
        "modifiers": mods,
        "skin": {"base": "middleage_caucasian_male", "tone": p["skinTone"]},
        "eyes": p["eyeColour"],
        "hair": {"style": "tied_back", "colour": p["hairColour"]},
        "beard": {"style": "full", "length": 0.035},
        "beard_shadow": 1.0,
        "texture": 2048,
    }


import materials  # noqa: E402
import recipes  # noqa: E402
import texio  # noqa: E402

# Heraldry (original geometric designs, painted). Force A follows the player's fixture heraldry (blue field, gold
# chevron); force B is red and white. Paint colours: recipes.PAINT (a READING of the G1 display colours).
FORCE_A = recipes.FORCE_A
FORCE_B = recipes.FORCE_B


def emblem_tex(name, field, charge, kind):
    return (f"tex/emblem_{name}.r1tx", lambda: texio.r1tx_bytes(materials.emblem(field, charge, kind)))


EMB_A = emblem_tex("force_a", recipes.PAINT["blue"], recipes.PAINT["gold"], "chevron")
EMB_A_CMD = emblem_tex("force_a_cmd", recipes.PAINT["blue"], recipes.PAINT["gold"], "sun")
EMB_B = emblem_tex("force_b", recipes.PAINT["red"], recipes.PAINT["white"], "wheel")
EMB_B2 = emblem_tex("force_b2", recipes.PAINT["white"], recipes.PAINT["red"], "saltire")


def soldier(id_, seed, years, race, muscle, weight, stature, skin_base, tone, hair_style, hair_colour, eyes, beard,
            mods, force, kind, emblem, label):
    afr, asi, cau = race
    pal = FORCE_A if force == "A" else FORCE_B
    return {
        "id": id_, "seed": seed, "kind": "soldier", "label": label,
        "macro": macro(years=years, muscle=muscle, weight=weight, proportions=0.6, african=afr, asian=asi, caucasian=cau),
        "stature": stature, "modifiers": mods,
        "skin": {"base": skin_base, "tone": tone}, "eyes": eyes,
        "hair": {"style": hair_style, "colour": hair_colour},
        "beard": beard, "beard_shadow": 0.0 if beard["style"] == "none" else (1.0 if beard["style"] != "stubble" else 0.75),
        "texture": 1024,
        "outfits": [{"id": f"kit_{force.lower()}", "label": f"Force {force}: {kind}", "force": force, "era": "medieval",
                     "textures": [emblem],
                     "recipe": (lambda b, pal=pal, kind=kind, em=emblem, sd=seed: recipes.soldier_medieval(b, pal, kind, em[0], seed=sd))}],
    }


def all_identities():
    av = avatar_spec()
    av["outfits"] = [
        {"id": f"era_{era}", "label": f"Avatar, {era.replace('_', ' ')} (military)", "era": era, "force": "player",
         "textures": [EMB_A], "recipe": (lambda b, era=era: recipes.avatar_era(b, era, EMB_A[0]))}
        for era in ("ancient", "medieval", "early_modern")]
    cmd = {
        "id": "commander_a", "seed": 21, "kind": "commander", "label": "Force A commander",
        "macro": macro(years=44, muscle=0.72, weight=0.56, proportions=0.75, african=0.0, asian=0.1, caucasian=0.9),
        "stature": 1.84,
        "modifiers": {"nose/nose-hump:decr|incr": 0.45, "chin/chin-prominent:decr|incr": 0.35, "chin/chin-width:decr|incr": 0.3,
                      "eyebrows/eyebrows-trans:backward|forward": 0.4, "cheek/l-cheek-bones:decr|incr": 0.3, "cheek/r-cheek-bones:decr|incr": 0.3,
                      "head/head-square": 0.3, "torso/torso-vshape:decr|incr": 0.35},
        "skin": {"base": "middleage_caucasian_male", "tone": "#D9A07E"}, "eyes": "#6B5A3A",
        "hair": {"style": "short", "colour": "#7A5A3A"}, "beard": {"style": "short", "length": 0.018}, "beard_shadow": 1.0,
        "texture": 2048,
        "outfits": [{"id": "kit_a", "label": "Force A commander: mail, surcoat, gilt-rim shield", "force": "A", "era": "medieval",
                     "textures": [EMB_A_CMD], "recipe": lambda b: recipes.commander(b, FORCE_A, EMB_A_CMD[0])}],
    }
    soldiers = [
        soldier("a1", 31, 22, (0.1, 0.1, 0.8), 0.60, 0.45, 1.74, "young_caucasian_male", "#E0AC88", "cropped", "#6A4A2E", "#4F6B3A",
                {"style": "stubble"}, {"nose/nose-scale-horiz:decr|incr": -0.2, "chin/chin-width:decr|incr": 0.25, "ears/l-ear-scale:decr|incr": 0.2,
                                       "ears/r-ear-scale:decr|incr": 0.2}, "A", "gambeson_spear", EMB_A, "Force A spearman (gambeson)"),
        soldier("a2", 32, 38, (0.0, 0.3, 0.7), 0.62, 0.78, 1.70, "middleage_caucasian_male", "#C68E6A", "short", "#1C1410", "#4A3A28",
                {"style": "moustache"}, {"nose/nose-scale-vert:decr|incr": 0.3, "nose/nose-hump:decr|incr": 0.5, "head/head-fat:decr|incr": 0.4,
                                         "mouth/mouth-scale-horiz:decr|incr": 0.2}, "A", "mail_spear", EMB_A, "Force A spearman (mail)"),
        soldier("a3", 33, 28, (0.85, 0.0, 0.15), 0.72, 0.5, 1.82, "young_african_male", "#5E3A26", "cropped", "#1C1410", "#2A2A2A",
                {"style": "short", "length": 0.010}, {"nose/nose-scale-horiz:decr|incr": 0.3, "mouth/mouth-lowerlip-volume:decr|incr": 0.3,
                                                       "head/head-oval": 0.4, "chin/chin-height:decr|incr": 0.2}, "A", "surcoat_sword", EMB_A, "Force A swordsman (surcoat)"),
        soldier("a4", 34, 46, (0.0, 0.0, 1.0), 0.42, 0.30, 1.67, "old_caucasian_male", "#E0AC88", "medium", "#8E8A84", "#2E5A7A",
                {"style": "full", "length": 0.040}, {"nose/nose-scale-vert:decr|incr": 0.35, "eyes/l-eye-bag:decr|incr": 0.5, "eyes/r-eye-bag:decr|incr": 0.5,
                                                      "cheek/l-cheek-volume:decr|incr": -0.4, "cheek/r-cheek-volume:decr|incr": -0.4}, "A", "levy_spear", EMB_A, "Force A levy (padded jacket)"),
        soldier("b1", 41, 24, (0.0, 0.6, 0.4), 0.68, 0.55, 1.72, "young_asian_male", "#E0AC88", "short", "#1C1410", "#4A3A28",
                {"style": "none"}, {"nose/nose-scale-horiz:decr|incr": 0.15, "head/head-round": 0.4, "eyes/l-eye-height2:decr|incr": -0.3,
                                    "eyes/r-eye-height2:decr|incr": -0.3}, "B", "gambeson_spear", EMB_B, "Force B spearman (gambeson)"),
        soldier("b2", 42, 33, (0.4, 0.0, 0.6), 0.68, 0.55, 1.78, "middleage_african_male", "#8D5A3B", "cropped", "#1C1410", "#4A3A28",
                {"style": "full", "length": 0.030}, {"nose/nose-flaring:decr|incr": 0.4, "chin/chin-prominent:decr|incr": 0.3, "head/head-square": 0.5},
                "B", "mail_spear", EMB_B2, "Force B spearman (mail)"),
        soldier("b3", 43, 52, (0.0, 0.0, 1.0), 0.48, 0.6, 1.73, "old_caucasian_male", "#F1C9A5", "short", "#8E8A84", "#4F6B3A",
                {"style": "moustache"}, {"nose/nose-scale-vert:decr|incr": 0.4, "nose/nose-point-width:decr|incr": 0.4, "head/head-age:decr|incr": 0.6,
                                         "ears/l-ear-lobe:decr|incr": 0.5, "ears/r-ear-lobe:decr|incr": 0.5}, "B", "surcoat_sword", EMB_B, "Force B swordsman (surcoat)"),
        soldier("b4", 44, 19, (0.0, 0.2, 0.8), 0.52, 0.42, 1.76, "young_caucasian_male", "#F1C9A5", "medium", "#7A2E1A", "#2E5A7A",
                {"style": "stubble"}, {"nose/nose-scale-vert:decr|incr": -0.2, "chin/chin-height:decr|incr": -0.2, "head/head-triangular": 0.3},
                "B", "levy_spear", EMB_B, "Force B levy (padded jacket)"),
    ]
    return [av, cmd] + soldiers


IDENTITIES = all_identities()
