#!/usr/bin/env python3
"""Migrate the Director's technology corpus (tech-graph-v0.6.json) into the
research engine's content file (Sim.Data/content/research.json) and write the
per-node audit (docs/research-corpus-audit.md).

ADR-029 §2 is the contract this script implements; D-044 is the ruling record.
The corpus file is READ ONLY — it is never modified. Every derived value is
computed deterministically from it (sorted iteration, no hash order), so
running the script twice produces byte-identical output.

Usage:  python3 scripts/migrate-research-corpus.py            (write both files)
        python3 scripts/migrate-research-corpus.py --check    (exit 1 if either file is stale)

What is migrated verbatim: ids, names, descriptions, ages, prerequisite
expressions, Eureka prose, capability / technique / application text,
families, generations, repeatable descriptors, registry requirement
expressions.

What is DERIVED here, with the rule stated at the derivation:
  * integer keys (stable ids for rows and orders)        -> assign_keys
  * the Civics tree (six arch §5.7 candidates)            -> build_civics
  * the research-stage trigger                           -> stage_expression
  * trunk / subtree placement (rule R1)                   -> classify
  * BaseCost (TUNE, chosen, not derived)                  -> cost_of
  * machine-evaluable Eureka conditions                  -> map_eureka
  * entity unlock lists (reverse index of the registry)   -> build_entities
"""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CORPUS = ROOT / "tech-graph-v0.6.json"
OUT = ROOT / "Sim.Data" / "content" / "research.json"
AUDIT = ROOT / "docs" / "research-corpus-audit.md"

# --------------------------------------------------------------------------
# TUNE — chosen, never derived (S8 §4.1(c)). Edit here and regenerate, or edit
# research.json directly; the loader validates either way.
# --------------------------------------------------------------------------
TUNE = {
    # CLP per sim-year = clpCoefficient * adults ^ clpAdultExponent (ADR-029 §6).
    # PROVISIONAL: population is the only §8.1 input that exists in state.
    "clpCoefficient": 1.0,
    "clpAdultExponent": 0.5,
    # One fired Eureka credits this fraction of the node's EffectiveCost,
    # capped at the remaining cost (D-044 R10).
    "eurekaCreditFraction": 0.25,
}
# BaseCost = round to 10 of COST_BASE * COST_GROWTH ** depth, where depth is the
# node's longest prerequisite path (union over OR alternatives). Monotone along
# every edge by construction, so a dependent always costs more than each of its
# prerequisites.
COST_BASE = 1000.0
COST_GROWTH = 1.12

AGES = ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9"]
TRUNK_AGES = {"A1", "A2", "A3", "A4", "A5"}   # rule R1 stage cut (content metadata, never a runtime gate)

BRANCHES = [  # key, id, number, name — D-044 R3
    (1, "military", "1.1", "Military"),
    (2, "medicine", "1.2", "Medicine"),
    (3, "engineering", "1.3", "Engineering"),
    (4, "natural_science", "1.4", "Natural Science"),
    (5, "agriculture", "1.5", "Agriculture"),
]
UNIVERSITY_TYPES = [  # key, id, name, branch — D-044 R5
    (1, "military_university", "Military University", "military"),
    (2, "medical_university", "Medical University", "medicine"),
    (3, "engineering_university", "Engineering University", "engineering"),
    (4, "natural_science_university", "Natural Science University", "natural_science"),
    (5, "agricultural_university", "Agricultural University", "agriculture"),
]
# Rule R1 domain map (primary domain -> subtree) for nodes outside the trunk.
# naval -> Military follows architecture §8.4.1 ("military and naval knowledge");
# industry and energy -> Engineering follows D-044 R20-F (Industry & Energy removed).
DOMAIN_TO_BRANCH = {
    "military": "military", "naval": "military",
    "medicine": "medicine",
    "science": "natural_science", "environment": "natural_science",
    "agriculture": "agriculture", "food": "agriculture",
    "engineering": "engineering", "materials": "engineering", "construction": "engineering",
    "transport": "engineering", "communication": "engineering", "infrastructure": "engineering",
    "industry": "engineering", "energy": "engineering",
}
# The six architecture §5.7 Civics candidates, in that section's order.
CIVICS = ["law_code", "legal_code_roman", "census", "coined_wage", "patent", "joint_stock"]
CIVIC_KEY_BASE = 1001

# Registry entity kinds, in the order they are emitted.
ENTITY_KINDS = [("buildings", "building"), ("infrastructure", "infrastructure"),
                ("institutions", "institution"), ("units", "unit"),
                ("activities", "activity"), ("projects", "project")]
UNLOCK_KINDS = ["units", "buildings", "institutions", "infrastructure", "activities", "projects"]

# --------------------------------------------------------------------------
# Eureka mapping (ADR-029 §7). Only FAITHFUL mappings; everything else keeps its
# prose and an explicit status. Quantities are stock_<good> (any controlled
# settlement holds a positive stock of that shipped good, PREV turn).
# --------------------------------------------------------------------------
GOOD_SYNONYMS = {  # exact whole-string synonyms -> goods.json name
    "grain": "grain", "livestock": "livestock", "fish": "fish",
    "timber": "timber", "wood": "timber", "stone": "stone", "clay": "clay",
    "copper": "copper-ore", "copper ore": "copper-ore", "tin": "tin-ore", "tin ore": "tin-ore",
    "fibre": "fiber", "fiber": "fiber", "hides": "hides", "bronze": "bronze",
    "tools": "tools", "pottery": "pottery", "cloth": "cloth",
}
# Hand-reviewed circumstance strings whose material is plainly a shipped good.
# Reviewed one by one; strings that name a PRODUCT of a technique (molten copper,
# copper wire, heat-treated stone, ground-stone axes), a negation (daub subsoil,
# "not potter's clay"), an unstated quantity ("in quantity", "at scale") or an
# inseparable list (stone, papyrus, ink) are deliberately NOT here.
CURATED_GOODS = {
    "knappable stone — quartzite, basalt, chert": ["stone"],
    "fine-grained knappable stone": ["stone"],
    "fine-grained stone, ideally obsidian or flint": ["stone"],
    "hard fine-grained stone": ["stone"],
    "bast fibre, sinew, hair, or gut": ["fiber"],
    "antler or wood thrower": ["timber"],
    "flexible wood (yew, elm, ash)": ["timber"],
    "sinew or fibre string": ["fiber"],
    "smooth stones": ["stone"],
    "mammoth bone or timber": ["timber"],
    "logs or reed bundles": ["timber"],
    "large straight log": ["timber"],
    "coarse stone slab": ["stone"],
    "stone bowl": ["stone"],
    "stone or bone blade": ["stone"],
    "lime or wood ash": ["timber"],
    "levigated clay": ["clay"],
    "felled timber": ["timber"],
    "workable stone in catchment": ["stone"],
    "digging tools": ["tools"],
    "stone or timber lining": ["stone", "timber"],
    "clay or stone whorl": ["clay", "stone"],
    "native copper deposit": ["copper-ore"],
    "copper ore (elevation channel)": ["copper-ore"],
    "carved stone mould": ["stone"],
    "stone or earth retaining walls": ["stone"],
    "timber runners": ["timber"],
    "planked timber": ["timber"],
    "timber ard": ["timber"],
    "carved stone": ["stone"],
    "clay sealings": ["clay"],
    "arsenical copper ore (fahlore)": ["copper-ore"],
    "tin — from cornwall, erzgebirge, afghanistan, anatolian taurus, southeast asia": ["tin-ore"],
    "long-distance exchange reaching a tin source": ["tin-ore"],
    "stone or clay bivalve mould": ["stone", "clay"],
    "fine clay": ["clay"],
    "picks (antler, bronze)": ["bronze"],
    "clay tablet": ["clay"],
    "bronze stylus": ["bronze"],
    "bent timber": ["timber"],
    "bronze fittings": ["bronze"],
    "stone weights": ["stone"],
    "clay or straw hives": ["clay"],
    "tuyère (clay nozzle)": ["clay"],
    "stone, birch bark, palm leaf": ["stone"],
    "precious metal or bronze": ["bronze"],
    "timber frame": ["timber"],
    "hide covering": ["hides"],
    "quarried stone": ["stone"],
    "bronze ram": ["bronze"],
    "bricks or cut stone": ["stone"],
    "two dressed stones": ["stone"],
    "cut stone": ["stone"],
    "bast fibre, rags, bark": ["fiber"],
    "glass or clay still": ["clay"],
    "wood ash": ["timber"],
    "cast bronze or iron": ["bronze"],
    "clay type": ["clay"],
    "timber gates": ["timber"],
    "iron or bronze screw": ["bronze"],
    "crushed stone": ["stone"],
}
# Circumstances that name a body of knowledge in the graph under another word.
TECH_ALIASES = {
    "fire": "fire_making",
    "sustained fire": "fire_making",
    "adhesive": "adhesive_natural",
    "alphabetic script (makes movable type economic)": "abjad OR alphabet_vowels",
}
EUREKA_STATUSES = ["evaluable", "no-state-carrier", "institution-state-absent", "contact-state-absent"]

TOKEN = re.compile(r"[a-z_0-9]+|AND|OR|\(|\)")


def fail(msg):
    sys.exit(f"migrate-research-corpus: {msg}")


# ---------------------------------------------------------------- expressions
def tokens(expr):
    if expr is None:
        return []
    toks = TOKEN.findall(expr)
    if "".join(toks) != re.sub(r"\s+", "", expr):
        fail(f"expression {expr!r} has characters outside the corpus grammar")
    return toks


def atoms(expr):
    return [t for t in tokens(expr) if t not in ("AND", "OR", "(", ")")]


def parse(expr):
    """Parse the corpus grammar into ('atom', id) / ('and'|'or', [children])."""
    toks = tokens(expr)
    pos = 0

    def parse_or():
        nonlocal pos
        left = parse_and()
        items = [left]
        while pos < len(toks) and toks[pos] == "OR":
            pos += 1
            items.append(parse_and())
        return items[0] if len(items) == 1 else ("or", items)

    def parse_and():
        nonlocal pos
        items = [parse_unary()]
        while pos < len(toks) and toks[pos] == "AND":
            pos += 1
            items.append(parse_unary())
        return items[0] if len(items) == 1 else ("and", items)

    def parse_unary():
        nonlocal pos
        if pos >= len(toks):
            fail(f"expression {expr!r} ends early")
        t = toks[pos]
        if t == "(":
            pos += 1
            inner = parse_or()
            if pos >= len(toks) or toks[pos] != ")":
                fail(f"expression {expr!r}: unclosed parenthesis")
            pos += 1
            return inner
        if t in ("AND", "OR", ")"):
            fail(f"expression {expr!r}: unexpected {t}")
        pos += 1
        return ("atom", t)

    node = parse_or()
    if pos != len(toks):
        fail(f"expression {expr!r}: trailing tokens")
    return node


def render(node, top=True):
    if node[0] == "atom":
        return node[1]
    op = " AND " if node[0] == "and" else " OR "
    text = op.join(render(c, False) for c in node[1])
    return text if top else f"({text})"


def substitute(node, fn):
    """Replace atoms by fn(atom) -> node (or the atom itself)."""
    if node[0] == "atom":
        return fn(node[1])
    return (node[0], [substitute(c, fn) for c in node[1]])


# ---------------------------------------------------------------- corpus checks
def load_corpus():
    raw = CORPUS.read_bytes()
    corpus = json.loads(raw)
    techs = corpus["technologies"]
    ids = [t["id"] for t in techs]
    if len(ids) != len(set(ids)):
        fail("duplicate technology id in the corpus")
    idset = set(ids)
    for t in techs:
        for a in atoms(t["research"]["prereq"]):
            if a not in idset:
                fail(f"{t['id']}: prerequisite {a!r} is not a technology")
        parse(t["research"]["prereq"]) if t["research"]["prereq"] else None
    # effects.enables must be the exact reverse index of prereq (measured true for v0.6).
    dependents = {i: [] for i in ids}
    for t in techs:
        for a in dict.fromkeys(atoms(t["research"]["prereq"])):
            dependents[a].append(t["id"])
    for t in techs:
        if t["effects"]["enables"] != dependents[t["id"]]:
            fail(f"{t['id']}: effects.enables is not the reverse index of prereq")
        if t["effects"]["immediate"]:
            fail(f"{t['id']}: effects.immediate is non-empty; no immediate-effect kind is ratified")
    return raw, corpus


def acyclic_order(nodes, prereq_atoms):
    """Kahn's algorithm over ids in KEY order; returns a topological order or fails naming a cycle."""
    indeg = {n: 0 for n in nodes}
    deps = {n: [] for n in nodes}
    for n in nodes:
        for a in dict.fromkeys(prereq_atoms[n]):
            indeg[n] += 1
            deps[a].append(n)
    ready = [n for n in nodes if indeg[n] == 0]
    order = []
    while ready:
        n = ready.pop(0)
        order.append(n)
        for d in deps[n]:
            indeg[d] -= 1
            if indeg[d] == 0:
                ready.append(d)
    if len(order) != len(nodes):
        fail("prerequisite cycle among: " + ", ".join(n for n in nodes if indeg[n] > 0))
    return order


# ---------------------------------------------------------------- derivations
def build_civics(corpus):
    """Reclassify the six arch §5.7 candidates as Civics nodes (ADR-029 §2.3).

    The civic takes the institution's short id and its knowledge half: its
    prerequisite is the institution's requirement expression, with every
    reference to ANOTHER institution replaced by that institution's own
    knowledge requirement (recursively) — an institution is established, not
    researched, so it cannot be a research prerequisite. The registry
    institution stays an institution whose requirement becomes the civic."""
    insts = {e["id"].split(".", 1)[1]: e for e in corpus["registry"]["institutions"]}
    techids = {t["id"] for t in corpus["technologies"]}

    def knowledge(short, seen=()):
        if short in seen:
            fail(f"institution cycle through {short}")
        expr = insts[short]["requirements"]["expression"]
        if expr is None:
            return None
        return substitute(parse(expr), lambda a: expand(a, seen + (short,)))

    def expand(a, seen):
        if a in techids or a in CIVICS:
            return ("atom", a)
        if a in insts:
            k = knowledge(a, seen)
            if k is None:
                fail(f"civic prerequisite reaches institution {a} with no requirement")
            return k
        fail(f"civic prerequisite atom {a!r} is neither a technology, a civic nor an institution")

    civics = []
    for i, short in enumerate(CIVICS):
        e = insts[short]
        node = parse(e["requirements"]["expression"])
        prereq = render(substitute(node, lambda a: expand(a, (short,))))
        civics.append({"key": CIVIC_KEY_BASE + i, "id": short, "institution": e, "prereq": prereq})
    return civics


def stage_expression(corpus):
    """The university / research institutional stage (D-044 R4, ADR-029 §8).

    Knowledge-level reading of "the civilization can establish a university":
      building.university's technology requirement
      AND building.library's technology requirement (the university building requires a library)
      AND inst.university's requirement, with institution references expanded to their
          knowledge requirement (civic ids stay civic atoms).
    Institutional PRESENCE is not evaluable: no institutions system exists."""
    b = {e["id"]: e for e in corpus["registry"]["buildings"]}
    insts = {e["id"].split(".", 1)[1]: e for e in corpus["registry"]["institutions"]}
    techids = {t["id"] for t in corpus["technologies"]}
    uni = b["building.university"]
    if uni["requirements"]["buildings"] != ["building.library"]:
        fail("building.university no longer requires exactly building.library; re-derive the stage")

    def expand(a, seen=()):
        if a in techids or a in CIVICS:
            return ("atom", a)
        if a in seen:
            fail(f"institution cycle through {a}")
        e = insts[a]["requirements"]["expression"]
        return substitute(parse(e), lambda x: expand(x, seen + (a,)))

    parts = [parse(uni["requirements"]["technologies"]),
             parse(b["building.library"]["requirements"]["technologies"]),
             substitute(parse(insts["university"]["requirements"]["expression"]), expand)]
    return render(("and", parts))


def depth_of(order, prereq_atoms):
    depth = {}
    for n in order:
        ps = prereq_atoms[n]
        depth[n] = 0 if not ps else 1 + max(depth[p] for p in ps)
    return depth


def closure(roots, prereq_atoms):
    seen, stack = set(), list(roots)
    while stack:
        n = stack.pop()
        if n in seen:
            continue
        seen.add(n)
        stack.extend(prereq_atoms[n])
    return seen


def classify(tech, age, forced_trunk):
    """Rule R1 (ADR-029 §8): trunk if in the stage-trigger closure or aged A1-A5,
    else the primary domain's subtree."""
    if tech["id"] in forced_trunk or age in TRUNK_AGES:
        return None
    dom = tech["tags"]["primary_domains"][0]
    if dom not in DOMAIN_TO_BRANCH:
        fail(f"{tech['id']}: primary domain {dom!r} has no subtree in the R1 map")
    return DOMAIN_TO_BRANCH[dom]


def cost_of(depth):
    return float(int(round(COST_BASE * COST_GROWTH ** depth / 10.0)) * 10)


def good_quantity(name):
    return "stock_" + name.replace("-", "_")


def map_eureka(text, node_ids, node_names):
    """-> (when | None, status). Faithful mappings only (ADR-029 §7)."""
    if text.startswith("institution present:"):
        return None, "institution-state-absent"
    if text.startswith("contact with a civilization"):
        return None, "contact-state-absent"
    if not text.startswith("circumstance:"):
        fail(f"unknown Eureka form {text!r}")
    s = text[len("circumstance:"):].strip().lower()
    if s in node_ids:
        return s, "evaluable"
    if s in node_names:
        return node_names[s], "evaluable"
    if s in TECH_ALIASES:
        return TECH_ALIASES[s], "evaluable"
    goods = [GOOD_SYNONYMS[s]] if s in GOOD_SYNONYMS else CURATED_GOODS.get(s)
    if goods:
        return " OR ".join(f"{good_quantity(g)} > 0" for g in goods), "evaluable"
    return None, "no-state-carrier"


def build_entities(corpus, civic_ids):
    """Registry entities with their knowledge requirement (ADR-029 §10).

    Atoms resolve to technology or civic nodes first, then to institution short
    ids. The six civic-backed institutions now require their civic. An atom that
    resolves to nothing is declared in `unresolved` — never silently dropped."""
    techids = {t["id"] for t in corpus["technologies"]}
    inst_short = {e["id"].split(".", 1)[1] for e in corpus["registry"]["institutions"]}
    out = []
    for key, kind in ENTITY_KINDS:
        for e in corpus["registry"][key]:
            if kind == "activity":
                expr = e["requires"]
                name = None
            elif kind == "institution":
                expr = e["requirements"]["expression"]
                name = e["name"]
            else:
                expr = e["requirements"]["technologies"]
                name = e["name"]
            short = e["id"].split(".", 1)[1]
            if kind == "institution" and short in civic_ids:
                expr = short   # the knowledge half moved to the Civics node of the same id
            unresolved = sorted({a for a in atoms(expr)
                                 if a not in techids and a not in civic_ids and a not in inst_short})
            out.append({"id": e["id"], "kind": kind, "name": name,
                        "requires": render(parse(expr)) if expr else None,
                        "unresolved": unresolved})
    return out


# ---------------------------------------------------------------- main
def build():
    raw, corpus = load_corpus()
    sha = hashlib.sha256(raw).hexdigest()
    techs = corpus["technologies"]
    techids = [t["id"] for t in techs]
    if set(CIVICS) & set(techids):
        fail("a Civics id collides with a technology id")

    civics = build_civics(corpus)
    civic_ids = set(CIVICS)
    all_ids = techids + CIVICS
    prereq_expr = {t["id"]: t["research"]["prereq"] for t in techs}
    prereq_expr.update({c["id"]: c["prereq"] for c in civics})
    prereq_atoms = {n: list(dict.fromkeys(atoms(prereq_expr[n]))) for n in all_ids}
    order = acyclic_order(all_ids, prereq_atoms)
    depth = depth_of(order, prereq_atoms)

    stage = stage_expression(corpus)
    stage_closure = closure(atoms(stage), prereq_atoms)
    ages = {t["id"]: ("A9" if t["age"] == "F" else t["age"]) for t in techs}
    for t in techs:
        if ages[t["id"]] not in AGES:
            fail(f"{t['id']}: age {t['age']!r} is not one of the nine Ages or F")

    branch = {t["id"]: classify(t, ages[t["id"]], stage_closure) for t in techs}
    for n in stage_closure:
        if n in branch and branch[n] is not None:
            fail(f"stage-trigger node {n} landed in a subtree")
    for t in techs:
        if branch[t["id"]] is None:
            for a in prereq_atoms[t["id"]]:
                if a in branch and branch[a] is not None:
                    fail(f"trunk node {t['id']} requires subtree node {a}")

    names = {t["name"].lower(): t["id"] for t in techs}
    for c in civics:
        names.setdefault(c["institution"]["name"].lower(), c["id"])
    entities = build_entities(corpus, civic_ids)

    # Unlock lists are regenerated from the (authoritative) entity requirements.
    unlocks = {n: {k: [] for k in UNLOCK_KINDS} for n in all_ids}
    kind_to_list = {"unit": "units", "building": "buildings", "institution": "institutions",
                    "infrastructure": "infrastructure", "activity": "activities", "project": "projects"}
    for e in entities:
        for a in dict.fromkeys(atoms(e["requires"])):
            if a in unlocks:
                unlocks[a][kind_to_list[e["kind"]]].append(e["id"])

    # Before regeneration, prove the corpus's own reverse index agreed with its registry.
    for t in techs:
        corpus_list = {k: list(t["unlocks"].get(k, [])) for k in ("units", "buildings", "institutions", "infrastructure")}
        corpus_list["activities"] = list(t["world"].get("activities", []))
        derived = {k: [] for k in corpus_list}
        for key, kind in ENTITY_KINDS:
            if kind == "project":
                continue
            for e in corpus["registry"][key]:
                expr = e["requires"] if kind == "activity" else (
                    e["requirements"]["expression"] if kind == "institution" else e["requirements"]["technologies"])
                if t["id"] in atoms(expr):
                    derived[kind_to_list[kind]].append(e["id"])
        for k in corpus_list:
            if sorted(corpus_list[k]) != sorted(derived[k]):
                fail(f"{t['id']}: corpus unlocks.{k} disagrees with the registry requirements")

    def node_record(key, n, tree, name, desc, age, frontier, emerged, dom, sec, eurekas,
                    caps, techniques, applications, family, gen, repeatable):
        u = {"capabilities": caps}
        for k in UNLOCK_KINDS:
            u[k] = unlocks[n][k]
        u["techniques"] = techniques
        u["applications"] = applications
        rec = {"key": key, "id": n, "name": name, "desc": desc, "age": age, "frontier": frontier,
               "emerged": emerged}
        if tree == "technology":
            rec["branch"] = branch[n]
        rec.update({"domain": dom, "secondaryDomains": sec, "depth": depth[n], "cost": cost_of(depth[n]),
                    "prereq": prereq_expr[n], "eurekas": eurekas, "unlocks": u,
                    "family": family, "generation": gen, "effects": {"immediate": []},
                    "repeatable": repeatable})
        return rec

    tech_records = []
    for i, t in enumerate(techs):
        eus = []
        for text in t["research"]["eureka"]:
            when, status = map_eureka(text, set(all_ids), names)
            eus.append({"text": text, "when": when, "status": status})
        rep = t["research"].get("repeatable")
        if rep is not None:
            rep = dict(rep)
            rep["engine"] = ("NOT IMPLEMENTED — completes once (D-044 R11 idempotent completion); "
                             "levels are an open question (D-044 Part D T6)")
        tech_records.append(node_record(
            i + 1, t["id"], "technology", t["name"], t["desc"], ages[t["id"]], t["age"] == "F",
            t["evidence"]["emerged"], t["tags"]["primary_domains"][0], t["tags"]["secondary_domains"],
            eus, t["unlocks"]["capabilities"], t["unlocks"].get("techniques", []),
            t["unlocks"].get("applications", []), t["progression"]["family"], t["progression"]["gen"], rep))

    civic_records = []
    for c in civics:
        inst = c["institution"]
        tech_ages = [ages[a] for a in closure(prereq_atoms[c["id"]], prereq_atoms) if a in ages]
        age = max(tech_ages, key=AGES.index)
        civic_records.append(node_record(
            c["key"], c["id"], "civics", inst["name"],
            (f"{inst['name']}: the civic knowledge of this form of organization. Reclassified from registry "
             f"institution {inst['id']} (architecture §5.7); the institution itself is established by its owning "
             f"system once this civic is complete."),
            age, False, None, "governance", [d for d in inst["tags"]["domains"]], [],
            [], [], [], None, None, None))

    content = {
        "schema": "civ-sim/research@1",
        "_doc": [
            "GENERATED by scripts/migrate-research-corpus.py from tech-graph-v0.6.json (read-only). ADR-029 is the contract; D-044 the ruling.",
            "key: the STABLE integer id carried by save rows and orders. Never renumber a key; append new nodes with new keys.",
            "prereq / researchStage.requires / eurekas[].when / entities[].requires: D-020 predicate dialect with AND/OR/NOT keywords and boolean atoms (ADR-029 §11).",
            "tuning: TUNE, chosen not derived. cost: TUNE, chosen not derived (depth-based; see the generator).",
        ],
        "source": {"corpus": "tech-graph-v0.6.json", "corpusVersion": corpus["version"], "corpusSha256": sha,
                   "generator": "scripts/migrate-research-corpus.py"},
        "tuning": dict(TUNE),
        "trees": [{"id": "technology", "number": "1", "name": "Technology"},
                  {"id": "civics", "number": "2", "name": "Civics"}],
        "branches": [{"key": k, "id": i, "number": num, "name": nm} for k, i, num, nm in BRANCHES],
        "researchStage": {
            "requires": stage,
            "_doc": ("Knowledge-level reading of the university / research institutional stage (D-044 R4): "
                     "building.university's technology requirement AND building.library's AND inst.university's "
                     "requirement with institution references expanded to their knowledge requirements. "
                     "Institutional presence is not evaluable (no institutions system); ADR-029 §8."),
        },
        "universityTypes": [{"key": k, "id": i, "name": nm, "branch": b} for k, i, nm, b in UNIVERSITY_TYPES],
        "technologies": tech_records,
        "civics": civic_records,
        "entities": entities,
    }
    stats = {"sha": sha, "order": order, "depth": depth, "branch": branch, "stage": stage,
             "stage_closure": stage_closure, "prereq_atoms": prereq_atoms}
    return content, stats, corpus


def render_audit(content, stats, corpus):
    techs = content["technologies"]
    civs = content["civics"]
    L = []
    L.append("# Research corpus audit — tech-graph-v0.6 → research.json")
    L.append("")
    L.append("**GENERATED** by `scripts/migrate-research-corpus.py` (do not edit by hand). "
             f"Corpus SHA-256 `{stats['sha']}`. Contract: `docs/adr/adr-029-research-engine.md`; rulings: D-044.")
    L.append("")
    counts = {}
    for t in techs:
        counts[t["branch"] or "trunk"] = counts.get(t["branch"] or "trunk", 0) + 1
    L.append("## Summary")
    L.append("")
    L.append(f"- Technology nodes integrated: **{len(techs)}** (every corpus technology; none dropped).")
    L.append(f"- Civics nodes integrated: **{len(civs)}** (the architecture §5.7 candidates).")
    L.append("- Tree 1 placement (rule R1, ADR-029 §8): " + ", ".join(
        f"{k} {counts.get(k, 0)}" for k in ["trunk", "military", "medicine", "engineering", "natural_science", "agriculture"]))
    ev = sum(1 for t in techs for e in t["eurekas"] if e["status"] == "evaluable")
    tot = sum(len(t["eurekas"]) for t in techs)
    by = {}
    for t in techs:
        for e in t["eurekas"]:
            by[e["status"]] = by.get(e["status"], 0) + 1
    L.append(f"- Eureka strings: {tot}; machine-evaluable {ev}; " + ", ".join(f"{k} {v}" for k, v in sorted(by.items())))
    nodes_with = sum(1 for t in techs if any(e["status"] == "evaluable" for e in t["eurekas"]))
    L.append(f"- Technology nodes with at least one evaluable Eureka: {nodes_with}")
    L.append(f"- Research stage trigger: `{stats['stage']}`")
    L.append(f"- Stage-trigger prerequisite closure: {len(stats['stage_closure'])} nodes (all forced into the trunk)")
    unres = [e for e in content["entities"] if e["unresolved"]]
    L.append(f"- Registry entities: {len(content['entities'])}; with unresolved corpus references: "
             + (", ".join(f"`{e['id']}` ({', '.join(e['unresolved'])})" for e in unres) or "none"))
    L.append(f"- Frontier (age F) nodes normalized to A9 + frontier: {sum(1 for t in techs if t['frontier'])}")
    L.append(f"- Repeatable descriptors kept as data (levels not implemented): {sum(1 for t in techs if t['repeatable'])}")
    L.append("")
    L.append("## Corpus problems found (none repaired silently)")
    L.append("")
    L.append("1. **No research cost in the corpus.** BaseCost is derived from prerequisite depth (TUNE, chosen).")
    L.append("2. **Eurekas are prose.** Only faithful mappings are machine-evaluable; the rest keep their text with a status.")
    L.append("3. **`inst.newspaper` references `postal_imperial`,** which is not a technology, civic or institution "
             "(it survives only as a `reclass` entry). Declared unresolved; `inst.newspaper`, and everything that "
             "requires it, is never knowledge-eligible.")
    L.append("4. **Age `F`** (16 nodes) is not one of the nine Ages; normalized to A9 with `frontier: true` (architecture §17.5).")
    L.append("5. **`research.repeatable`** on 10 frontier nodes conflicts with idempotent completion; the nodes complete once (D-044 Part D T6).")
    L.append("6. **Generation numbers are display lineage, not a dependency rule.** 13 nodes break 'gen N requires gen N-1'; not validated (D-044 R7).")
    L.append("7. **No Civics nodes existed.** The six architecture §5.7 candidates were reclassified; no Civics content was invented.")
    L.append("8. **`effects.immediate` is empty on every node.** No immediate-effect kind is ratified (law 2), so the loader requires it empty.")
    L.append("")
    L.append("## Per-node audit")
    L.append("")
    L.append("Columns: key · id · tree/branch · primary domain · age · depth · BaseCost · prerequisites · Eurekas (evaluable/total) · family/gen · entity unlocks · capabilities · emerged")
    L.append("")
    L.append("| key | id | tree / branch | domain | age | depth | cost | prerequisites | eureka | family / gen | entities | caps | emerged |")
    L.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|")

    def esc(x):
        return (x or "").replace("|", "\\|")
    for t in techs:
        ent = sum(len(t["unlocks"][k]) for k in UNLOCK_KINDS)
        evn = sum(1 for e in t["eurekas"] if e["status"] == "evaluable")
        fam = f"{t['family']} / {t['generation']}" if t["family"] else ""
        L.append(f"| {t['key']} | `{t['id']}` | technology / {t['branch'] or 'main'} | {t['domain']} | {t['age']}{' (F)' if t['frontier'] else ''} "
                 f"| {t['depth']} | {int(t['cost'])} | {esc(t['prereq'])} | {evn}/{len(t['eurekas'])} | {esc(fam)} | {ent} | {len(t['unlocks']['capabilities'])} | {esc(t['emerged'])} |")
    for c in civs:
        ent = sum(len(c["unlocks"][k]) for k in UNLOCK_KINDS)
        L.append(f"| {c['key']} | `{c['id']}` | civics | {c['domain']} | {c['age']} (derived) | {c['depth']} | {int(c['cost'])} "
                 f"| {esc(c['prereq'])} | 0/0 | | {ent} | 0 | |")
    L.append("")
    L.append("## Eureka mapping (every evaluable Eureka)")
    L.append("")
    L.append("| node | corpus text | condition |")
    L.append("|---|---|---|")
    for t in techs:
        for e in t["eurekas"]:
            if e["status"] == "evaluable":
                L.append(f"| `{t['id']}` | {esc(e['text'])} | `{esc(e['when'])}` |")
    L.append("")
    return "\n".join(L) + "\n"


def main():
    content, stats, corpus = build()
    text = json.dumps(content, indent=1, ensure_ascii=True) + "\n"
    audit = render_audit(content, stats, corpus)
    if "--check" in sys.argv[1:]:
        stale = [p for p, want in ((OUT, text), (AUDIT, audit))
                 if not p.exists() or p.read_text(encoding="utf-8") != want]
        if stale:
            sys.exit("stale: " + ", ".join(str(p.relative_to(ROOT)) for p in stale))
        print("research.json and the audit are up to date")
        return
    OUT.write_text(text, encoding="utf-8")
    AUDIT.write_text(audit, encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)} ({len(text)} bytes) and {AUDIT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
