#!/usr/bin/env python3
"""IP name-overlap report, first pass (D-069 R3; docs/ip-originality.md §3 item 2, §4).

Compares the names this game SHIPS (Sim.Data/content/ and the Sim.Ui era enum) with the third-party reference name lists
that ALREADY exist in this repository's design research under docs/. Nothing is fetched from the internet. Writes
docs/m6-plan/ip-name-overlap-report.md.

The report is an INPUT to the release-gate review, not a legal verdict. The classification column is a curated
judgement recorded in CLASSIFY below; a match with no curated entry is printed as "unclassified - review".

Usage:  python3 scripts/ip-name-overlap-report.py            (write the report)
        python3 scripts/ip-name-overlap-report.py --check    (exit 1 if the report is stale)
"""
import hashlib
import json
import re
import sys
import unicodedata
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPORT = ROOT / "docs" / "m6-plan" / "ip-name-overlap-report.md"
TOOL = "scripts/ip-name-overlap-report.py"
KR = "docs/m6-plan/knowledge-redesign"


def rel(p):
    return str(Path(p).relative_to(ROOT)).replace("\\", "/")


def load(p):
    with open(ROOT / p, encoding="utf-8") as f:
        return json.load(f)


# ---------------------------------------------------------------------------------------------- our shipped names

def our_names():
    """(category, id, name, source) for every shipped name the release gate reviews."""
    out = []
    research = load("Sim.Data/content/research.json")
    sim = load("Sim.Data/content/sim.json")
    forms = set(sim["regime"]["forms"])
    for n in research["technologies"]:
        out.append(("Technology", n["id"], n["name"], "Sim.Data/content/research.json"))
    for n in research["civics"]:
        cat = "Government form" if n["id"] in forms else "Civic"
        out.append((cat, n["id"], n["name"], "Sim.Data/content/research.json"))
    kinds = {"building": "Building / structure", "infrastructure": "Building / structure",
             "institution": "Institution", "project": "Project", "unit": "Unit (research entity)",
             "activity": "Activity / recipe", "recipe": "Activity / recipe"}
    for e in research["entities"]:
        out.append((kinds.get(e["kind"], "Other entity"), e["id"], e["name"], "Sim.Data/content/research.json"))
    uf = load("Sim.Data/content/unit-families.json")
    for f in uf["families"]:
        out.append(("Unit family", f["id"], f["name"], "Sim.Data/content/unit-families.json"))
        for ij in f["line"]:
            out.append(("Unit identity", ij["id"], ij["name"], "Sim.Data/content/unit-families.json"))
    up = load("Sim.Data/content/unit-progression.json")
    for t in up["trees"]:
        out.append(("Promotion", t["id"], t["name"], "Sim.Data/content/unit-progression.json"))
        for p in t.get("promotions", []):
            out.append(("Promotion", p["id"], p["name"], "Sim.Data/content/unit-progression.json"))
    ages = load("Sim.Data/content/ages.json")
    for a in ages["ages"]:
        out.append(("Age / era", a["id"], a["name"], "Sim.Data/content/ages.json"))
        for part in re.split(r"\s*/\s*", a["name"]):
            if part != a["name"]:
                out.append(("Age / era", a["id"], part, "Sim.Data/content/ages.json (name part)"))
    for s in ages["surges"]:
        out.append(("Age surge", s["id"], s["name"], "Sim.Data/content/ages.json"))
    enum = (ROOT / "Sim.Ui/Theme/UiEra.cs").read_text(encoding="utf-8")
    body = enum[enum.index("public enum UiEra"):]
    body = body[:body.index("}")]
    for m in re.finditer(r"^\s*([A-Za-z]+)\s*=\s*\d+", body, re.M):
        spaced = re.sub(r"(?<=[a-z])(?=[A-Z])", " ", m.group(1))
        out.append(("UI era theme", m.group(1), spaced, "Sim.Ui/Theme/UiEra.cs"))
    # player-facing mechanic terms named in the Sim.Ui research views (the research panel's acceleration lines).
    # D-069 R4 (2026-11-02): the player-facing name is "Idea"; "eureka" stays only as an internal code and data key.
    out.append(("Player-facing term", "idea", "Idea", "Sim.Ui (research views; internal key: eureka)"))
    return out


# ---------------------------------------------------------------------------------------------- reference lists

PARENS_DROP = re.compile(
    r"\b(?:policy|policies|tradition|government|ideology|civic|civics|technology|tech|unique civic|luxury resource|"
    r"bonus resource|strategic resource|resource|harvest|action|wonder|building|improvement|unit|district|project|"
    r"great person|era|age)\b", re.I)


def split_analog(s):
    """An analog string such as "Engineering (Aqueduct)" or "Monarchy / Democracy" -> its candidate names."""
    names = []
    main = re.sub(r"\([^)]*\)", "", s)
    for part in re.split(r"\s*(?:/|\+|;|,| or )\s*", main):
        part = part.strip(" .:-")
        if part:
            names.append(part)
    for inner in re.findall(r"\(([^)]*)\)", s):
        for piece in re.split(r"\s*[;,]\s*", inner):
            m = re.match(r"unlocks the (.+?) government", piece, re.I)
            if m:
                names.append(m.group(1))
                continue
            piece = re.sub(r"^[^:]*:\s*", "", piece)                     # "Inca civic: Terrace Farm" -> "Terrace Farm"
            piece = PARENS_DROP.sub("", piece).strip(" .:-")
            piece = re.sub(r"\s+", " ", piece)
            if piece and piece[0].isupper() and len(piece.split()) <= 4:
                names.append(piece)
    return names


def humanize_id(i):
    """BUILDING_GREAT_STELE -> Great Stele (a game id turned into its likely display name)."""
    rest = i.split("_", 1)[1] if "_" in i else i
    return " ".join(w.capitalize() for w in rest.split("_"))


ID_KIND = OrderedDict([("NODE_CIVIC", "civic"), ("NODE_TECH", "technology"), ("TRADITION", "policy / tradition"),
                       ("POLICY", "policy / tradition"), ("GOVERNMENT", "government"), ("IDEOLOGY", "ideology"),
                       ("BUILDING", "building"), ("WONDER", "wonder"), ("IMPROVEMENT", "improvement"),
                       ("UNIT", "unit"), ("DISTRICT", "district"), ("QUARTER", "quarter"), ("CIVIC", "civic")])


def kind_of(i):
    for p, k in ID_KIND.items():
        if i.startswith(p + "_"):
            return k
    return None


def reference_names():
    """(game, kind, name, source file) for every third-party name found in the repo's design research."""
    refs = []

    def add(game, kind, name, src):
        name = re.sub(r"\s+", " ", name).strip()
        if name:
            refs.append((game, kind, name, src))

    # 1. Civilization VI base-game civics (a local cache of the game's civic list)
    p = f"{KR}/data/civ6-civics-base.json"
    for c in load(p)["civics"]:
        add("Civilization VI", "civic", c["name"], p)

    # 2. Civilization VII civics data (civics, masteries, policies/traditions, governments, ideologies, unlocked
    #    buildings/wonders/units, unique civics, ages)
    p = f"{KR}/data/civ7-civics.json"
    d7 = load(p)
    for age in d7["ages"]:
        add("Civilization VII", "age", age.capitalize() + " Age", p)

    def walk7(o):
        if isinstance(o, dict):
            i = o.get("id")
            if isinstance(i, str) and isinstance(o.get("name"), str) and kind_of(i):
                add("Civilization VII", kind_of(i), o["name"], p)
            for k, v in o.items():
                if k in ("traditions", "civics") and isinstance(v, list):
                    for x in v:
                        if isinstance(x, str):
                            add("Civilization VII", "policy / tradition" if k == "traditions" else "civic", x, p)
                if k in ("governmentGatedPolicies", "nodes") and isinstance(v, list):
                    for x in v:
                        if isinstance(x, list) and len(x) == 2 and isinstance(x[1], str) and kind_of(x[0]):
                            add("Civilization VII", kind_of(x[0]), x[1], p)
                if k == "otherUnlocks" and isinstance(v, list):
                    for x in v:
                        if isinstance(x, str) and kind_of(x) in ("building", "wonder", "improvement", "unit", "district", "quarter"):
                            add("Civilization VII", kind_of(x) + " (name from game id)", humanize_id(x), p)
                walk7(v)
        elif isinstance(o, list):
            for x in o:
                walk7(x)
    walk7(d7)

    # 3. Civ VI / Civ VII analogs recorded per node (tranche-2 node audit), per civic (civics proposal) and per
    #    government form (government packages)
    for p, tag in ((f"{KR}/data/tranche2/node-audit.json", None),
                   (f"{KR}/civics-tree-proposal.json", None),
                   (f"{KR}/data/government-packages.json", "government")):
        def walk(o, tree=None):
            if isinstance(o, dict):
                t = o.get("tree", tree)
                for key, game in (("civ6", "Civilization VI"), ("civ7", "Civilization VII")):
                    if key in o:
                        v = o[key]
                        if isinstance(v, dict):
                            v = v.get("analog")
                        vals = v if isinstance(v, list) else [v]
                        for s in vals:
                            if isinstance(s, str):
                                for n in split_analog(s):
                                    add(game, tag or (t or "analog") + " analog", n, p)
                for v in o.values():
                    walk(v, t)
            elif isinstance(o, list):
                for x in o:
                    walk(x, tree)
        walk(load(p))

    # 4. unit references moved out of unit-families.json by D-069 R2 (Civ VI and AoE II unit names)
    p = "docs/m7-plan/unit-design-references.md"
    for line in (ROOT / p).read_text(encoding="utf-8").splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) == 5 and cells[0].startswith("`") and re.match(r"\d+$", cells[1]):
            for piece in cells[4].split(";"):
                piece = piece.strip()
                m = re.match(r"(Civ VI|AoE II)\s+(.+)$", piece)
                if not m:
                    continue
                game = "Civilization VI" if m.group(1) == "Civ VI" else "Age of Empires II"
                for n in re.split(r"\s*/\s*", m.group(2)):
                    add(game, "unit", n, p)

    # 5. names in prose that directly follow a game's name ("Civ VI's Great People", "Civilization VII Test of Time")
    cue = (r"(Civ(?:ilization)?\s*(?:VII|VI|IV|V|7|6|4|5)|AoE\s*(?:II|2)|Age of Empires(?: II)?|Old World|Humankind|"
           r"Victoria 3|Europa Universalis(?: IV)?|EU4|Crusader Kings(?: III)?|Hearts of Iron(?: IV)?|Stellaris)")
    name = r"((?:[A-Z][A-Za-z\-']+)(?:\s+(?:of|the|and|&|[A-Z][A-Za-z\-']+)){0,3})"
    rx = re.compile(cue + r"(?:'s|’s)?:?\s+(?:(?:base|the|its|a|an)\s+)?" + name)
    skip = ("ip-name-overlap-report.md", "unit-design-references.md", "d069-", "ip-originality.md")
    for f in sorted((ROOT / "docs").rglob("*.md")):
        if any(s in f.name for s in skip):
            continue
        text = f.read_text(encoding="utf-8")
        for m in rx.finditer(text):
            n = re.sub(r"\s+(?:of|the|and|&)$", "", m.group(2))
            add(GAME_OF_CUE(m.group(1)), "prose mention", n, rel(f))
    return refs


def GAME_OF_CUE(c):  # noqa: N802 - reads as a table lookup
    c = re.sub(r"\s+", " ", c)
    if re.match(r"Civ(?:ilization)?\s*(?:VII|7)$", c):
        return "Civilization VII"
    if re.match(r"Civ(?:ilization)?\s*(?:VI|6)$", c):
        return "Civilization VI"
    if re.match(r"Civ(?:ilization)?\s*(?:IV|4)$", c):
        return "Civilization IV"
    if re.match(r"Civ(?:ilization)?\s*(?:V|5)$", c):
        return "Civilization V"
    if c.startswith("AoE") or c.startswith("Age of Empires"):
        return "Age of Empires II"
    if c.startswith("Europa") or c == "EU4":
        return "Europa Universalis IV"
    return c


# ---------------------------------------------------------------------------------------------- matching

def fold(s):
    return "".join(ch for ch in unicodedata.normalize("NFKD", s) if not unicodedata.combining(ch))


def singular(w):
    if len(w) <= 3:
        return w
    if w.endswith("men"):
        return w[:-3] + "man"
    if w.endswith("ies") and len(w) > 4:
        return w[:-3] + "y"
    if w.endswith(("ches", "shes", "sses", "xes")):
        return w[:-2]
    if w.endswith("s") and not w.endswith(("ss", "us", "is")):
        return w[:-1]
    return w


def norm(s):
    s = fold(s).lower().replace("&", " and ")
    s = re.sub(r"[^a-z0-9]+", " ", s).strip()
    words = [singular(w) for w in s.split() if w not in ("the", "a", "an")]
    return " ".join(words)


def edit(a, b, cap):
    if abs(len(a) - len(b)) > cap:
        return cap + 1
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i] + [0] * len(b)
        best = i
        for j, cb in enumerate(b, 1):
            cur[j] = min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb))
            best = min(best, cur[j])
        if best > cap:
            return cap + 1
        prev = cur
    return prev[-1]


STOP = {"of", "and", "the", "age", "era"}


def match(ours, ref):
    """'exact' (case-insensitive equality), 'near: <how>' or None."""
    if ours.strip().lower() == ref.strip().lower():
        return "exact"
    a, b = norm(ours), norm(ref)
    if not a or not b:
        return None
    if a == b:
        return "near: same after normalization"
    n = min(len(a), len(b))
    cap = 0 if n < 5 else (1 if n < 8 else 2)
    if cap and edit(a, b, cap) <= cap:
        return f"near: edit distance <= {cap}"
    ta, tb = [t for t in a.split() if t not in STOP], [t for t in b.split() if t not in STOP]
    short, long_ = (ta, tb) if len(ta) <= len(tb) else (tb, ta)
    if len(short) >= 2 and all(t in long_ for t in short):
        return "near: every word of the shorter name is in the other"
    return None


# ---------------------------------------------------------------------------------------------- classification

# Curated judgement (D-069 R3 first pass, 2026-11-01). Keyed by our name, case-insensitive. "common" = a standard
# historical, technical or generic term any work on the subject would use; "review" = distinctive or coined, or the
# same name in a closely similar role, for the Director's release-gate review (ip-originality.md §3 item 2).
COMMON = "common historical term"
REVIEW = "distinctive/coined - review"
SPURIOUS = "no real overlap (spurious near match)"
_COMMON_NAMES = """
coinage; cavalry; battering ram; water mill; stirrup; alchemy; gunpowder; celestial navigation with instruments;
atmospheric steam engine; rotary steam engine; tank; tanks; controlled nuclear fission; laser; computation; machine gun;
chiefdom; municipal charters; civil service examinations; feudal monarchy; constitutional monarchy; political philosophy;
common law; divine right of kings; enlightenment; humanism; mercantilism; stock exchange; civil code; liberalism;
merit civil service; nationalism; professional sport; socialism; revolutionary republic; fascism; hospitals; hospital;
corps of engineers; progressivism; public works relief; social media; think tanks; granary; workshop; printing house;
castle; aqueduct; power station; spearmen; archers; slingers; crossbowmen; artillery; mining; toolmaking; skirmishers;
rangers; pikemen; pike and shot; heavy infantry / line infantry; men-at-arms; rifle line infantry; mechanized infantry;
machine-gun teams; mounted scout / light cavalry; hussars; knights; cuirassiers; trebuchets; bombards; rocket artillery;
destroyers; scouts; line infantry drill; modern; agricultural; sericulture; war bands; conservatism; holy war; paper mill
"""
CLASSIFY = {n.strip(): COMMON for n in _COMMON_NAMES.replace("\n", " ").split(";") if n.strip()}
# The first pass (2026-11-01) sent "Eureka" to review: it is the name another game gives the very mechanic ours uses it
# for, and it was player-facing. D-069 R4 (2026-11-02) renamed the player-facing term to "Idea", an everyday word.
CLASSIFY["idea"] = COMMON
# near matches that share letters, not a name: (our name, reference name), case-insensitive
SPURIOUS_PAIRS = {("sericulture", "agriculture"), ("war bands", "mār-banê"), ("conservatism", "conservation"),
                  ("holy war", "cold war"), ("paper mill", "water mill"), ("castle", "castes"), ("castle", "cattle"),
                  ("nationalism", "rationalism"), ("agricultural", "agriculture"), ("archers", "archery"),
                  ("power station", "battersea power station")}


def classify(name, ref=None):
    if ref is not None and (name.lower(), ref.lower()) in SPURIOUS_PAIRS:
        return SPURIOUS
    return CLASSIFY.get(name.lower(), "unclassified - review")


def flagged(name, ref):
    return classify(name, ref) not in (COMMON, SPURIOUS)


# ---------------------------------------------------------------------------------------------- report

def build():
    ours = our_names()
    refs = reference_names()
    # de-duplicate reference names per (game, name) keeping every kind and source
    refmap = OrderedDict()
    for game, kind, name, src in refs:
        key = (game, name.lower())
        e = refmap.setdefault(key, {"game": game, "name": name, "kinds": [], "sources": []})
        if kind not in e["kinds"]:
            e["kinds"].append(kind)
        if src not in e["sources"]:
            e["sources"].append(src)
    refl = list(refmap.values())
    rows = []
    seen = set()
    for cat, oid, oname, osrc in ours:
        for r in refl:
            how = match(oname, r["name"])
            if how is None:
                continue
            k = (cat, oid, oname, r["game"], r["name"].lower())
            if k in seen:
                continue
            seen.add(k)
            rows.append((cat, oid, oname, how, r))
    return ours, refl, rows


CATEGORY_ORDER = ["Technology", "Civic", "Government form", "Unit family", "Unit identity", "Unit (research entity)",
                  "Promotion", "Age / era", "Age surge", "UI era theme", "Building / structure", "Institution",
                  "Project", "Activity / recipe", "Other entity", "Player-facing term"]


def sources_cell(r):
    s = r["sources"]
    shown = ", ".join(f"`{x}`" for x in s[:2])
    return shown + (f" (+{len(s) - 2} more)" if len(s) > 2 else "")


def render(ours, refl, rows):
    w = []
    out = w.append
    digest = hashlib.sha256()
    for p in ("Sim.Data/content/research.json", "Sim.Data/content/unit-families.json", "Sim.Data/content/ages.json",
              "Sim.Data/content/unit-progression.json", "Sim.Data/content/sim.json"):
        digest.update((ROOT / p).read_bytes())
    exact = [r for r in rows if r[3] == "exact"]
    near = [r for r in rows if r[3] != "exact"]
    flagged_names = {r[2].lower() for r in rows if flagged(r[2], r[4]["name"])}
    spurious = [r for r in rows if classify(r[2], r[4]["name"]) == SPURIOUS]
    out("# IP name-overlap report: first pass (D-069 R3)")
    out("")
    out(f"**Generated** by `{TOOL}` from the shipped content (sha256 of research, unit-families, ages, unit-progression"
        f" and sim.json together: `{digest.hexdigest()[:16]}…`) and the reference lists already in `docs/`. Do not edit by"
        " hand; regenerate with `python3 scripts/ip-name-overlap-report.py` (`--check` reports staleness). The"
        " classification column is the curated table in that script.")
    out("")
    out("**Authority:** [D-069](../d069-originality-and-third-party-game-references.md) R3; rule and release gate:"
        " [ip-originality.md](../ip-originality.md) §3 item 2 and §4.")
    out("")
    out("> **This is an input to the release-gate review, not a legal verdict.** A match says only that two names are"
        " equal or similar. It says nothing about whether a name is protectable, and a name with no match here is not"
        " thereby cleared. Common historical terms (\"Bronze Working\", \"Feudalism\", \"Skirmisher\") are fine under"
        " ip-originality.md §3 item 2; the review is about distinctive coined names, distinctive combinations, and the"
        " same names in the same tree structure.")
    out("")
    out("## 1. Headline")
    out("")
    out(f"- Shipped names compared: **{len(ours)}** (each category below).")
    out(f"- Distinct third-party reference names in the repository's research: **{len(refl)}**.")
    out(f"- **Exact matches** (case-insensitive equality): **{len(exact)}** rows, **{len({r[2].lower() for r in exact})}**"
        " distinct names of ours.")
    out(f"- **Near matches** (equal after normalization, small edit distance, or word containment): **{len(near)}** rows,"
        f" **{len({r[2].lower() for r in near})}** distinct names of ours.")
    out(f"- Of those rows, **{len(spurious)}** are spurious near matches (shared letters, not a shared name).")
    out(f"- **Flagged for review** (classified \"distinctive/coined - review\" or not yet classified): **{len(flagged_names)}**"
        " distinct name(s) of ours. Every other match is a common historical term; §5 adds the same-role clusters the"
        " name-by-name view cannot show.")
    out("")
    out("| Category | Shipped names | Exact rows | Near rows | Names flagged for review |")
    out("|---|---:|---:|---:|---:|")
    for cat in CATEGORY_ORDER:
        n = sum(1 for o in ours if o[0] == cat)
        if not n:
            continue
        ce = sum(1 for r in exact if r[0] == cat)
        cn = sum(1 for r in near if r[0] == cat)
        cf = len({r[2].lower() for r in rows if r[0] == cat and flagged(r[2], r[4]["name"])})
        out(f"| {cat} | {n} | {ce} | {cn} | {cf} |")
    out("")
    out("## 2. Coverage and limits (read before relying on this)")
    out("")
    out("- **Only lists already in this repository were used.** Nothing was fetched. The reference lists are:")
    out(f"  1. Civilization VI base-game civics: `{KR}/data/civ6-civics-base.json` (the full civic list).")
    out(f"  2. Civilization VII: `{KR}/data/civ7-civics.json` (civics and masteries of the three Ages, unique civics,"
        " policies and traditions, governments, ideologies, and the buildings, wonders and units the civics unlock;"
        " names taken from game ids are marked \"name from game id\").")
    out(f"  3. The Civilization VI and VII analog recorded for each of our nodes: `{KR}/data/tranche2/node-audit.json`,"
        f" `{KR}/civics-tree-proposal.json` and `{KR}/data/government-packages.json`. These list only the third-party"
        " nodes someone mapped to ours, not those games' full technology trees.")
    out("  4. Civilization VI and Age of Empires II unit names: the `reference` values moved out of `unit-families.json`"
        " by D-069 R2 (`docs/m7-plan/unit-design-references.md` §1).")
    out("  5. Capitalized names that directly follow a game's name in prose anywhere in `docs/**/*.md` (for example"
        " \"Civ VI's Great People\"). This is low-precision extraction; most of it is noise that matches nothing.")
    out("- **Not covered:** the full Civilization VI technology tree, unit roster, building, wonder, district and"
        " policy-card lists; Civilization VII technologies and units beyond those named above; every other game of the"
        " genre (Civilization I–V, Age of Empires I–IV, Europa Universalis, Victoria, Humankind, Old World, Millennia,"
        " Ara and others), whose lists are not in the repository. Those need a list before the release gate can rely"
        " on this check (ip-originality.md §3 item 2).")
    out("- **Not covered by name matching at all:** tree structure (§3 item 3), text (§3 item 4), art and audio"
        " (§3 item 5) and store text (§3 item 6).")
    out("- **Shipped names not in the comparison:** policy names (no policy content ships yet), place names (generated"
        " procedurally), good and need names (`goods.json`, `needs.json`), and UI labels other than the era enum and the"
        " Idea term (renamed from Eureka under D-069 R4).")
    out("- **Method.** Exact: case-insensitive equality of the trimmed names. Near: (a) equal after normalization"
        " (accents folded, lower case, punctuation removed, a leading article dropped, each word made singular by simple"
        " rules: \"-men\" → \"-man\", \"-ies\" → \"-y\", trailing \"s\"); or (b) edit distance ≤ 1 on normalized names of"
        " 5–7 characters and ≤ 2 on names of 8 or more (no fuzzy match below 5 characters); or (c) every word of the"
        " shorter name (at least two words, ignoring \"of\", \"and\", \"the\", \"age\", \"era\") occurs in the other."
        " A one-word reference name inside a longer name of ours (\"Pottery\" in \"Open-fired pottery\") is deliberately"
        " not matched, because it would bury the list in generic words; the reviewer should keep that gap in mind.")
    out("- **Not in CI.** The generator has a `--check` mode, but no CI step runs it; whether it joins CI is the"
        " Director's call.")
    out("- **Classification** is a first-pass judgement: \"common historical term\" (a standard historical, technical or"
        " generic term any work on the subject would use) or \"distinctive/coined - review\" (a coined game term, a"
        " distinctive combination, or the same name in a closely similar role). It is a triage aid for the Director's"
        " review, not a clearance.")
    out("")
    out("## 3. Matches by category")
    out("")
    out("Columns: our category, our id, our name, the match kind, the matched reference name (game; kind), where the"
        " reference name was found, and the classification.")
    for cat in CATEGORY_ORDER:
        crow = [r for r in rows if r[0] == cat]
        if not crow:
            continue
        out("")
        out(f"### {cat}")
        out("")
        out("| Our id | Our name | Match | Reference name (game; kind) | Source | Classification |")
        out("|---|---|---|---|---|---|")
        for _, oid, oname, how, r in sorted(crow, key=lambda x: (x[3] != "exact", x[2].lower(), x[1], x[4]["game"], x[4]["name"])):
            kinds = ", ".join(r["kinds"])
            out(f"| `{oid}` | {oname} | {how} | {r['name']} ({r['game']}; {kinds}) | {sources_cell(r)} | {classify(oname, r['name'])} |")
    out("")
    out("## 4. Names flagged for review")
    out("")
    flagged_rows = sorted({(r[0], r[1], r[2]) for r in rows if flagged(r[2], r[4]["name"])}, key=lambda x: (CATEGORY_ORDER.index(x[0]), x[2].lower(), x[1]))
    if flagged_rows:
        out("| Category | Our id | Our name | Classification |")
        out("|---|---|---|---|")
        for cat, oid, oname in flagged_rows:
            out(f"| {cat} | `{oid}` | {oname} | {classify(oname)} |")
    else:
        out("None.")
    out("")
    out("## 5. Same-role clusters (input to ip-originality.md §3 items 2 and 3)")
    out("")
    out("Each name above may be common, yet the gate also asks whether the *same names in the same tree structure*"
        " recur (§3 item 2) and whether a tree mirrors another game's position for position (§3 item 3). Name matching"
        " cannot answer item 3; these clusters show where to look first.")
    out("")

    def cluster(title, cats, game, kinds, note):
        hit = sorted({(r[2], r[4]["name"]) for r in rows
                      if r[0] in cats and r[4]["game"] == game and any(k in kinds for k in r[4]["kinds"])
                      and classify(r[2], r[4]["name"]) != SPURIOUS}, key=lambda x: x[0].lower())
        total = sum(1 for o in ours if o[0] in cats)
        out(f"- **{title}:** {len({h[0].lower() for h in hit})} of our {total} names match a {'/'.join(kinds)} name of {game}"
            + (": " + "; ".join(f"{a} ~ {b}" if a.lower() != b.lower() else a for a, b in hit) + "." if hit else ".") + " " + note)

    cluster("Civics vs Civilization VI civics", ("Civic", "Government form"), "Civilization VI", ("civic",),
            "All are standard terms of political, legal or social history. Our civics tree has 480 nodes against that"
            " game's 50-odd, so the overlap is a small share of ours, but these nodes should be checked for position-"
            "for-position placement (§3 item 3).")
    cluster("Government forms vs Civilization VII governments", ("Government form", "Civic"), "Civilization VII", ("government",),
            "Standard constitutional terms; the set of 33 forms is our own (tranche-2 regime-forms lane).")
    cluster("Unit identities and unit entities vs Civilization VI units", ("Unit identity", "Unit (research entity)"), "Civilization VI", ("unit",),
            "Standard military-history terms. Several of our upgrade lines follow the same historical sequence as that"
            " game's (spear to pike to pike-and-shot; knight to cuirassier to tank); the sequence is history, but the"
            " line-by-line correspondence is the place to apply §3 item 3. The former per-identity references are in"
            " `docs/m7-plan/unit-design-references.md`.")
    cluster("Unit identities and unit entities vs Age of Empires II units", ("Unit identity", "Unit (research entity)"), "Age of Empires II", ("unit",),
            "Standard military-history terms.")
    out("")
    return "\n".join(w)


def main():
    ours, refl, rows = build()
    text = render(ours, refl, rows)
    if "--check" in sys.argv:
        cur = REPORT.read_text(encoding="utf-8") if REPORT.exists() else ""
        if cur != text:
            print(f"{TOOL}: {rel(REPORT)} is stale - run python3 {TOOL}")
            return 1
        print(f"{TOOL}: {rel(REPORT)} is current")
        return 0
    REPORT.write_text(text, encoding="utf-8")
    print(f"{TOOL}: wrote {rel(REPORT)} ({len(rows)} match rows)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
