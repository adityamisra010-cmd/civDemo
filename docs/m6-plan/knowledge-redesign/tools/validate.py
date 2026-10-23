#!/usr/bin/env python3
"""Validate the PROPOSED graphs, the educational corpus and the source register.

DESIGN-PACKAGE TOOLING ONLY. Structural validity is NOT historical verification:
this tool checks shape, references, counts and wording rules; whether a claim is
true is recorded per entry in the corpus factCheck block and the editorial
checklist, never inferred from a passing run.

Writes validation-report.md. Exit 1 if any ERROR is found.
Usage: python3 -I tools/validate.py [--no-corpus]
"""
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

FACTORS = ["novelty", "difficulty", "material", "institutional", "breadth", "prereq_complexity"]
# MEASURED loader ranges, Sim.Core/Systems/Research/ResearchContent.cs:808-837
FACTOR_RANGE = {"novelty": (0.5, 2.0), "difficulty": (0.0, 3.5), "material": (0.0, 2.0),
                "institutional": (0.0, 3.0), "breadth": (0.0, 1.5), "prereq_complexity": (0.0, 1.0)}
UNIV_BRANCH = {1: "military", 2: "medicine", 3: "engineering", 4: "natural_science", 5: "agriculture"}
GRANT_PREFIXES = {"knowledge", "identification", "access", "production", "building", "infrastructure",
                  "institution", "unit", "policy", "regime", "trade", "tax", "stage", "age"}
STATUSES = {"realized-today", "knowledge-only", "deferred"}
MILESTONES = {"existing", "M6", "M7", "M8", "M9", "M10", "M10+", "M11+"}
NODE_KINDS = {"science", "application", "technique", "resource", "infrastructure", "institutional", "military",
              "medical", "civic-micro", "civic-macro"}
INEVITABLE = re.compile(r"\b(inevitabl\w*|destined|bound to happen|every (?:civilization|society|people) "
                        r"(?:must|eventually)|all societies|naturally led|the natural next step|was only a matter of time)\b",
                        re.I)
GAMEY = re.compile(r"(\+\s?\d+\s?%|\bbonus\b|\bunlocks?\b|\bboosts?\b|\bresearch points?\b|\bthe player\b|\byou\b)", re.I)
BRANCHES = set(kr.BRANCHES)
AGE_N = {a: i + 1 for i, a in enumerate(kr.AGES)}
# V21: the era-marked year heuristic of gap audit G-34, MEASURED from
# Sim.Tests/Systems/ResearchContentTests.cs:466-483 (EarliestMarkedYear) - the same pattern, character for character.
ERA_MARKED = re.compile(r"(\d[\d,]*(?:\.\d+)?)(?:\s*[-–/]\s*(\d[\d,]*(?:\.\d+)?))?\s*(BCE|CE|kya|Mya)\b")
# The test's own examples (ResearchContentTests.cs:493-497); V21 refuses to run silently if the port drifts from them.
ERA_EXAMPLES = [("1895 CE", 1895.0), ("~100 CE Alexandria; ~200 BCE China", -200.0), ("1860-1880 CE", 1860.0),
                ("~44 kya Lebombo; ~20 kya Ishango", -44000.0), ("1977 Sanger; 2001 human genome", None)]
UNDATED_PIN_AT_HEAD = 83   # ResearchContentTests.cs:513 pins Nodes.Count - 83 dated nodes at HEAD


class Report:
    def __init__(self):
        self.rows = []

    def add(self, level, code, msg):
        self.rows.append((level, code, msg))

    def count(self, level):
        return sum(1 for r in self.rows if r[0] == level)


def earliest_marked_year(emerged):
    """EarliestMarkedYear (ResearchContentTests.cs:469-483): the earliest year (CE positive, BCE negative, kya and Mya
    scaled) carrying an explicit era marker in an `emerged` text; a range "1860-1880 CE" counts both ends; None when
    no year carries a marker (bare years such as "1977 Sanger" are undated and not tested)."""
    if emerged is None:
        return None
    best = None
    for m in ERA_MARKED.finditer(emerged):
        for grp in (1, 2):
            if m.group(grp) is None:
                continue
            v = float(m.group(grp).replace(",", ""))
            era = m.group(3)
            y = v if era == "CE" else (-v if era == "BCE" else (-v * 1000.0 if era == "kya" else -v * 1e6))
            if best is None or y < best:
                best = y
    return best


def must_hold_atoms(ast):
    """Predicate.MustHoldAtoms (Sim.Core/Systems/ClassMobility/Predicate.cs:107-138): the atoms true in EVERY satisfying
    assignment - AND is the union of its sides, OR the intersection, a comparison or a NOT contributes nothing."""
    if ast is None:
        return set()
    kind = ast[0]
    if kind == "atom":
        return {ast[1]}
    if kind == "and":
        out = set()
        for x in ast[1]:
            out |= must_hold_atoms(x)
        return out
    if kind == "or":
        sets = [must_hold_atoms(x) for x in ast[1]]
        return set.intersection(*sets) if sets else set()
    return set()


def must_closure(i, ast):
    """MustClosure (ResearchContentTests.cs:275-286) and the loader's must[i] (ResearchContent.cs:654-663): every node
    complete WHENEVER i's prerequisites hold - the must-held atoms, closed transitively (i itself excluded)."""
    seen, todo = set(), [i]
    while todo:
        x = todo.pop()
        for a in must_hold_atoms(ast.get(x)):
            if a not in seen:
                seen.add(a)
                todo.append(a)
    return seen


def check_chronology(rep, active, cur):
    """V21 - D-047 ruling 1 ('a prerequisite must be causally and historically possible before the node it gates'),
    reproducing Sim.Tests/Systems/ResearchContentTests.cs:485-518 (D047_Causal_NoRequiredPrerequisiteEmergedAfterItsDependent)
    on the proposed graph: for every node dated after 3000 BCE, no must-held prerequisite may carry an era-marked
    emergence later than the node's own. ERROR rows are exactly the rows that test would fail on."""
    for text, want in ERA_EXAMPLES:
        got = earliest_marked_year(text)
        if got != want:
            rep.add("ERROR", "V21", f"the EarliestMarkedYear port gives {got} for {text!r}, the test expects {want} "
                                    "(ResearchContentTests.cs:493-497) - V21 results are void until fixed")
    nodes = sorted(active, key=lambda n: n["key"])
    by_id = {n["id"]: n for n in nodes}
    ast = {n["id"]: kr.parse(n.get("prereq")) for n in nodes}
    dated = edges = 0
    for n in nodes:
        y = earliest_marked_year(n.get("emerged"))
        if y is None:
            old = cur.get(n["id"])
            if n["status"] == "new" or (old is not None and old.get("emerged") != n.get("emerged")):
                rep.add("WARN", "V21", f"{n['id']}: no era-marked year in emerged {n.get('emerged')!r} - the chronology "
                                       "test cannot check its edges and the marker-free pin moves (lane brief §5)")
            continue
        dated += 1
        if y <= -3000.0 or ast[n["id"]] is None:
            continue
        for a in sorted(must_hold_atoms(ast[n["id"]])):
            p = by_id.get(a)
            ya = earliest_marked_year(p.get("emerged")) if p else None
            if ya is None:
                continue
            edges += 1
            if ya > y:
                rep.add("ERROR", "V21", f"{n['id']} ({y:g}) <- {a} ({ya:g}): a must-held prerequisite is dated later than "
                                        "its dependant (D-047 r1; ResearchContentTests.cs:500-512)")
    if edges <= 300:
        rep.add("ERROR", "V21", f"only {edges} dated must-hold edges checked (the test requires > 300, "
                                "ResearchContentTests.cs:514)")
    for node, forbidden in (("xray", "vacuum_tube"), ("canning", "germ_theory")):     # :515-517, the Director examples
        if node in by_id and forbidden in must_closure(node, ast):
            rep.add("ERROR", "V21", f"{forbidden} is in the must-closure of {node} (Director example, "
                                    "ResearchContentTests.cs:516-517)")
    undated = len(nodes) - dated
    rep.add("INFO", "V21", f"chronology: {dated} of {len(nodes)} active nodes dated, {edges} dated must-hold edges checked; "
                           f"{undated} marker-free texts (pinned at {UNDATED_PIN_AT_HEAD} on HEAD content, "
                           "ResearchContentTests.cs:513 - a count pin that moves with the content)")
    return {"dated": dated, "undated": undated, "edges": edges}


def _can_hold(ast, reached):
    """Predicate.CanHold (Predicate.cs:105, :140-147): NOT-free conditions are evaluated with every comparison taken
    as true; any NOT makes the answer 'can hold' (undecided, never a rejection)."""
    def uses_not(x):
        return x is not None and (x[0] == "not" or (x[0] in ("and", "or") and any(uses_not(y) for y in x[1])))

    def optimistic(x):
        kind = x[0]
        if kind == "atom":
            return x[1] in reached
        if kind == "and":
            return all(optimistic(y) for y in x[1])
        if kind == "or":
            return any(optimistic(y) for y in x[1])
        return True
    return ast is None or uses_not(ast) or optimistic(ast)


def _has_comparison(ast):
    if ast is None:
        return False
    if ast[0] == "cmp":
        return True
    if ast[0] in ("and", "or"):
        return any(_has_comparison(x) for x in ast[1])
    if ast[0] == "not":
        return _has_comparison(ast[1])
    return False


def _reach(order, ast, subtree, reached, stage_open, excluded=None):
    """ResearchContentLoader.Reach (ResearchContent.cs:1328-1346): the completion fixpoint; before the stage opens only
    Main-tree technologies and Civics can complete; `excluded` is held incomplete."""
    changed = True
    while changed:
        changed = False
        for i in order:
            if i in reached or i == excluded or (not stage_open and subtree[i]):
                continue
            if kr.holds(ast[i], reached):
                reached.add(i)
                changed = True


def check_eurekas(rep, active, stage_expr):
    """V22 - the loader's Eureka refusals on the proposed graph, reproducing ResearchContentLoader:
    shape (ResearchContent.cs:566-572: evaluable_now and class agree with `when`), reference (:573-579: every condition
    atom is a node), DEAD Eurekas (:620-647: the condition cannot hold while the node is still researchable - reached
    without the node, comparisons optimistic, NOT undecided) and IMPLIED Eurekas (:649-682: a knowledge-only condition
    every path to the node already satisfies - the node's must-closure, plus the research stage's for a subtree node).
    Conditions: a record's own `when`, else - for an existing node's append-only list - the production condition at the
    same index (read-only from Sim.Data/content/research.json; the inventory does not carry `when`)."""
    prod = {}
    prod_path = kr.CONTENT / "research.json"
    if prod_path.exists():
        r = kr.load_json(prod_path)
        prod = {x["id"]: x.get("eurekas") or [] for x in r.get("technologies", []) + r.get("civics", [])}
    else:
        rep.add("WARN", "V22", "Sim.Data/content/research.json not found - existing nodes' Eureka conditions not checked")
    nodes = sorted(active, key=lambda n: n["key"])
    order = [n["id"] for n in nodes]
    ast = {n["id"]: kr.parse(n.get("prereq")) for n in nodes}
    subtree = {n["id"]: n["tree"] == "technology" and n.get("branch") not in (None, "trunk") for n in nodes}
    conds = {}
    for n in nodes:
        for idx, e in enumerate(n.get("eurekas") or []):
            when = e.get("when")
            if when is None and n["status"] == "existing" and idx < len(prod.get(n["id"], [])):
                when = prod[n["id"]][idx].get("when")
            if e.get("evaluable_now") is not None and bool(e["evaluable_now"]) != (when is not None):
                rep.add("ERROR", "V22", f"{n['id']}.eurekas[{idx}]: evaluable_now must be true exactly when 'when' carries "
                                        "a condition (ResearchContent.cs:566-567)")
            if e.get("class") and (e["class"] == "machine-evaluable") != (when is not None):
                rep.add("ERROR", "V22", f"{n['id']}.eurekas[{idx}]: class {e['class']!r} disagrees with the condition "
                                        "(machine-evaluable exactly when 'when' is present, ResearchContent.cs:568-572)")
            if when is None:
                continue
            try:
                cast = kr.parse(when)
            except ValueError as ex:
                rep.add("ERROR", "V22", f"{n['id']}.eurekas[{idx}]: invalid Eureka reference - {ex}")
                continue
            bad = [a for a in kr.atoms(cast) if a not in ast]
            if bad:
                rep.add("ERROR", "V22", f"{n['id']}.eurekas[{idx}]: invalid Eureka reference - {', '.join(bad)} is not an "
                                        "active node (ResearchContent.cs:573-579)")
                continue
            conds.setdefault(n["id"], []).append((idx, when, cast))
    stage = kr.parse(stage_expr)
    # dead: reached without i (stage opening only if it can without i)
    for i in order:
        without = None
        for idx, when, cast in conds.get(i, []):
            if not kr.atoms(cast):
                continue
            if without is None:
                without = set()
                _reach(order, ast, subtree, without, False, excluded=i)
                if kr.holds(stage, without):
                    _reach(order, ast, subtree, without, True, excluded=i)
            if not _can_hold(cast, without):
                rep.add("ERROR", "V22", f"{i}.eurekas[{idx}]: dead Eureka - the condition {when!r} cannot hold while {i} "
                                        "is still researchable: the knowledge it names completes only after it "
                                        "(ResearchContent.cs:620-647)")
    # implied: a knowledge-only condition already guaranteed by the node's own prerequisites (and the stage, in a subtree)
    stage_must = set()
    for a in must_hold_atoms(stage):
        stage_must |= {a} | must_closure(a, ast)
    n_checked = 0
    for i in order:
        for idx, when, cast in conds.get(i, []):
            if _has_comparison(cast):
                continue
            n_checked += 1
            guaranteed = must_closure(i, ast) | (stage_must if subtree[i] else set())
            if kr.holds(cast, guaranteed):
                rep.add("ERROR", "V22", f"{i}.eurekas[{idx}]: implied Eureka - the condition {when!r} is satisfied on every "
                                        f"path to {i} (its own prerequisites guarantee it): a flat cost cut, not a "
                                        "circumstance (ResearchContent.cs:649-682)")
    total = sum(len(v) for v in conds.values())
    rep.add("INFO", "V22", f"Eurekas: {total} evaluable conditions checked for dead Eurekas, {n_checked} knowledge-only "
                           "conditions for implied Eurekas")
    return {"conditions": total, "knowledgeOnly": n_checked}


def check_graphs(rep, tech, civ, inv):
    stats = {}
    all_nodes = tech["nodes"] + civ["nodes"]
    active = [n for n in all_nodes if n["status"] != "retired"]
    by_id = {n["id"]: n for n in active}
    # V01 identity
    ids = Counter(n["id"] for n in all_nodes)
    keys = Counter(n["key"] for n in all_nodes)
    for i, c in ids.items():
        if c > 1:
            rep.add("ERROR", "V01", f"duplicate id {i}")
    for k, c in keys.items():
        if c > 1:
            rep.add("ERROR", "V01", f"duplicate key {k}")
    for n in tech["nodes"]:
        if not (1 <= n["key"] <= 999):
            rep.add("ERROR", "V01", f"technology key out of range: {n['id']} {n['key']}")
    for n in civ["nodes"]:
        if not (1000 <= n["key"] <= 1999):
            rep.add("ERROR", "V01", f"civic key out of range: {n['id']} {n['key']}")
    # V14 old-to-new mapping completeness and key stability
    cur = {n["id"]: n for n in inv["nodes"]}
    prop_by_id = {n["id"]: n for n in all_nodes}
    for i, n in cur.items():
        if i not in prop_by_id:
            rep.add("ERROR", "V14", f"existing node {i} missing from the proposal (no mapping)")
        elif prop_by_id[i]["key"] != n["key"]:
            rep.add("ERROR", "V14", f"existing node {i} changed key {n['key']} -> {prop_by_id[i]['key']}")
    # V02/V03 references and cycles
    g = kr.Graph(active)
    for a, b in g.missing_refs():
        lvl = "ERROR"
        rep.add(lvl, "V02", f"{a}: prerequisite {b} is not an active node")
    cyc = g.find_cycle()
    if cyc:
        rep.add("ERROR", "V03", "cycle: " + " -> ".join(cyc))
        return stats
    # V04 trunk closure and the cross-tree fence
    for n in tech["nodes"]:
        if n["status"] == "retired":
            continue
        for a in kr.atoms(g.ast[n["id"]]):
            p = by_id.get(a)
            if not p:
                continue
            if p["tree"] == "civics":
                rep.add("WARN", "V04", f"technology {n['id']} requires civic {a} (cross-tree edge — needs a fence ruling)")
            elif n["branch"] == "trunk" and p["branch"] != "trunk":
                rep.add("ERROR", "V04", f"trunk node {n['id']} requires subtree node {a} (trunk must be closed)")
    # V05 Age monotonicity
    for n in active:
        for a in kr.atoms(g.ast[n["id"]]):
            p = by_id.get(a)
            if p and AGE_N.get(p["age"], 0) > AGE_N.get(n["age"], 0):
                rep.add("ERROR", "V05", f"{n['id']} ({n['age']}) requires later-Age {a} ({p['age']})")
    # V06 research stage
    stage = kr.parse(tech["researchStage"]["requires"])
    for a in kr.atoms(stage):
        p = by_id.get(a)
        if not p:
            rep.add("ERROR", "V06", f"research stage atom {a} is not an active node")
        elif p["tree"] == "technology" and p["branch"] != "trunk":
            rep.add("ERROR", "V06", f"research stage atom {a} is a subtree node (stage unreachable)")
        elif p["tree"] == "civics":
            rep.add("WARN", "V06", f"research stage atom {a} is a civic — CP pacing gates all five subtrees (docket C.1)")
    # V07 Age facts
    for f in tech["ageFacts"]:
        fact = f["fact"]
        if fact.get("kind") != "research":
            continue
        for x in fact["nodes"]:
            p = by_id.get(x)
            if not p:
                rep.add("ERROR", "V07", f"Age fact {f['milestone']} names {x}, not an active node")
            elif f["role"] == "core" and p["tree"] == "civics" and ((p.get("civic") or {}).get("category") == "macro"):
                rep.add("ERROR", "V07", f"Age CORE fact {f['milestone']} names macro civic {x} (Age must not grant a government form)")
            elif f["role"] == "core" and p["tree"] == "civics":
                rep.add("WARN", "V07", f"Age CORE fact {f['milestone']} names civic {x} (CP pacing gates Age entry)")
    # V08 node completeness and cost model
    for n in active:
        nid = n["id"]
        if n["tree"] == "technology" and n["branch"] not in BRANCHES:
            rep.add("ERROR", "V08", f"{nid}: bad branch {n['branch']}")
        if n["age"] not in AGE_N:
            rep.add("ERROR", "V08", f"{nid}: bad age {n['age']}")
        if not n.get("cost") or n["cost"] <= 0:
            rep.add("ERROR", "V08", f"{nid}: no cost")
        f = n.get("factors") or {}
        if set(f) != set(FACTORS):
            rep.add("ERROR", "V08", f"{nid}: factors must be exactly {FACTORS}")
        else:
            for k in FACTORS:
                v = float(f[k])
                lo, hi = FACTOR_RANGE[k]
                if v < lo or v > hi or abs(v * 4 - round(v * 4)) > 1e-9:
                    rep.add("ERROR", "V08", f"{nid}: factor {k}={v} outside the loader range [{lo},{hi}] or not a quarter step")
            mag = sum(float(f[k]) for k in FACTORS)
            if n.get("magnitude") is not None and abs(mag - float(n["magnitude"])) > 1e-6:
                rep.add("ERROR", "V08", f"{nid}: magnitude {n['magnitude']} != sum of factors {mag}")
            expect = kr.base_cost(mag)
            if n.get("cost") and abs(n["cost"] - expect) > max(5.0, 0.001 * expect):
                rep.add("ERROR", "V08", f"{nid}: cost {n['cost']} off the model {expect:.1f}")
        if n["status"] == "new" and not n.get("nodeKind"):
            rep.add("ERROR", "V08", f"{nid}: new node without nodeKind")
        if n.get("nodeKind") and n["nodeKind"] not in NODE_KINDS:
            rep.add("ERROR", "V08", f"{nid}: unknown nodeKind {n['nodeKind']}")
        # V09 capability
        cap = n.get("capability") or {}
        if not cap:
            rep.add("ERROR", "V09", f"{nid}: no capability block")
            continue
        if cap.get("status") not in STATUSES:
            rep.add("ERROR", "V09", f"{nid}: capability.status {cap.get('status')!r}")
        if cap.get("milestone") not in MILESTONES:
            rep.add("ERROR", "V09", f"{nid}: capability.milestone {cap.get('milestone')!r}")
        if not cap.get("realizationOwner"):
            rep.add("ERROR", "V09", f"{nid}: no realizationOwner")
        for gr in cap.get("grants") or []:
            if gr.split(":", 1)[0] not in GRANT_PREFIXES:
                rep.add("ERROR", "V09", f"{nid}: grant prefix in {gr!r}")
        deps = g.deps.get(nid, [])
        if cap.get("status") == "knowledge-only" and not deps and not cap.get("deferredEffect") \
                and not [x for x in cap.get("grants") or [] if not x.startswith("knowledge:")]:
            rep.add("WARN", "V09", f"{nid}: knowledge-only dead end (no dependants, no assigned later realization)")
        # V18 loader rules: orphan, one primary university (technology), subtree primary serves its subtree
        entity_grants = [x for x in cap.get("grants") or [] if x.split(":", 1)[0] in
                         ("institution", "unit", "building", "infrastructure", "production")]
        caps_text = [x for x in cap.get("grants") or [] if x.startswith("knowledge:")] or \
            ((n.get("unlocks") or {}).get("capabilities") or [])
        if not deps and not entity_grants and not caps_text:
            rep.add("ERROR", "V18", f"{nid}: orphan (no dependant, no entity unlock, no capability string) — the loader refuses it")
        if n["tree"] == "technology":
            prim = [u for u in n.get("universityRelevance") or [] if u.get("role") == "primary"]
            if len(prim) != 1:
                rep.add("ERROR", "V18", f"{nid}: a technology needs exactly one primary university ({len(prim)} given)")
            elif n["branch"] != "trunk" and UNIV_BRANCH.get(prim[0].get("type")) != n["branch"]:
                rep.add("ERROR", "V18", f"{nid}: subtree node's primary university must serve {n['branch']}")
        if n["tree"] == "civics":
            for gr in cap.get("grants") or []:
                pre = gr.split(":", 1)[0]
                if pre in ("regime", "policy"):
                    rep.add("WARN", "V18", f"{nid}: grant {gr} needs a new entity kind (loader allows civics to unlock only "
                                           "institutions — ResearchContent.cs:1240-1242); IMPLEMENTATION DEFERRED (T6.5/D-V)")
        # V19 prereq_complexity convention (ledger :425-426; D-047 :467-470): top-level AND conjuncts,
        # an OR group counts once: 1 -> 0.0, 2 -> 0.5, >=3 -> 1.0 (one documented exception: taxation)
        ast = g.ast[nid]
        conj = 0 if ast is None else (len(ast[1]) if ast[0] == "and" else 1)
        expect_pc = 0.0 if conj <= 1 else (0.5 if conj == 2 else 1.0)
        if f and set(f) == set(FACTORS) and abs(float(f["prereq_complexity"]) - expect_pc) > 1e-9 and nid != "taxation":
            lvl = "WARN" if n["status"] == "existing" and not n.get("prereqChanged") else "ERROR"
            rep.add(lvl, "V19", f"{nid}: prereq_complexity {f['prereq_complexity']} but {conj} top-level requirement(s) "
                                f"→ {expect_pc} by convention")
        # V10 Eureka ceiling
        w = sum(float(e.get("weight") or 0) for e in n.get("eurekas") or [])
        if w > 0.4 + 1e-9:
            rep.add("ERROR", "V10", f"{nid}: Eureka weights sum {w} > 0.40")
    # V12 civics
    for i in ("law_code", "legal_code_roman", "census", "coined_wage", "patent", "joint_stock", "taxation"):
        n = prop_by_id.get(i)
        if not n:
            rep.add("ERROR", "V12", f"existing civic {i} missing")
        elif n["tree"] != "civics":
            rep.add("ERROR", "V12", f"existing civic {i} moved out of the civics tree")
    for n in civ["nodes"]:
        if n["status"] == "retired":
            continue
        c = n.get("civic") or {}
        if n["status"] == "new" and c.get("category") not in ("micro", "macro"):
            rep.add("ERROR", "V12", f"civic {n['id']}: category must be micro or macro")
        for gr in (n.get("capability") or {}).get("grants") or []:
            if gr.split(":", 1)[0] in ("unit", "production", "building", "identification", "access"):
                rep.add("WARN", "V12", f"civic {n['id']} grants {gr} (civics should unlock institutions/policies/regimes)")
    # V13 lineage
    for n in all_nodes:
        for s in n.get("splitInto") or []:
            p = prop_by_id.get(s)
            if not p or p.get("splitFrom") != n["id"]:
                rep.add("ERROR", "V13", f"{n['id']} split into {s}, which does not name it as splitFrom")
        for fld in ("mergedInto", "replacedBy"):
            t = n.get(fld)
            if t and (t not in by_id):
                rep.add("ERROR", "V13", f"{n['id']} {fld} {t}, not an active node")
    # V20 entity-requirement changes resolve (node id, then inst.<name> short id — ResearchContent.cs:1119-1160)
    inst_short = {e["id"].split(".", 1)[1] for e in inv["entities"] if e["id"].startswith("inst.")}
    ent_ids = {e["id"] for e in inv["entities"]}
    for ch in tech.get("entityChanges") or []:
        if ch["entity"] not in ent_ids:
            rep.add("WARN", "V20", f"entity change for {ch['entity']} — not an existing entity (proposed new entity)")
        for a in kr.atoms(kr.parse(ch.get("newRequires"))):
            if a not in by_id and a not in inst_short:
                rep.add("ERROR", "V20", f"entity {ch['entity']} requires {a!r}, neither an active node nor an institution")
    # V16 duplicate names
    names = Counter(n["name"].lower() for n in active)
    for nm, c in names.items():
        if c > 1:
            rep.add("WARN", "V16", f"duplicate display name: {nm}")
    # V17 trunk after A6
    for n in tech["nodes"]:
        if n["status"] != "retired" and n["branch"] == "trunk" and AGE_N.get(n["age"], 0) >= 7:
            rep.add("WARN", "V17", f"trunk node {n['id']} at {n['age']} (current content keeps A7+ in subtrees)")
    # V21 chronology (the D-047 test) and V22 Eurekas (the loader's dead/implied refusals): ERROR = would fail there
    stats["chronology"] = check_chronology(rep, active, cur)
    stats["eurekas"] = check_eurekas(rep, active, tech["researchStage"]["requires"])
    stats["active"] = len(active)
    stats["byStatus"] = dict(Counter(n["status"] for n in all_nodes))
    stats["byTree"] = dict(Counter(n["tree"] for n in active))
    stats["dispositions"] = dict(Counter(d for n in all_nodes for d in n["disposition"]))
    return stats


LEGACY_CIVICS = ("law_code", "legal_code_roman", "census", "coined_wage", "patent", "joint_stock", "taxation")


def check_gates(rep, tech, civ, inv):
    """V23-V26 (added 2026-10-22, Director-intent refinement): key ranges for new records, the mandatory closure of
    the research stage and the Age CORE facts, no regime form made mandatory or Age-granted, and the consumers of
    every legacy civic (its gates). Must-closure follows the loader's MustHoldAtoms rule (see must_closure)."""
    stats = {}
    active = [n for n in tech["nodes"] + civ["nodes"] if n["status"] != "retired"]
    by_id = {n["id"]: n for n in active}
    ast = {n["id"]: kr.parse(n.get("prereq")) for n in active}
    # V23 key ranges: legacy keys never renumbered (V14); new technology keys append from 427, new civics from 1008
    for n in tech["nodes"]:
        if n["status"] == "new" and n["key"] < 427:
            rep.add("ERROR", "V23", f"new technology {n['id']} has key {n['key']} (< 427, a legacy range)")
    for n in civ["nodes"]:
        if n["status"] == "new" and n["key"] < 1008:
            rep.add("ERROR", "V23", f"new civic {n['id']} has key {n['key']} (< 1008, the legacy civic range)")
    cur_keys = {c["id"]: c["key"] for c in inv["nodes"] if c["id"] in LEGACY_CIVICS}
    for i in LEGACY_CIVICS:
        if i in by_id and i in cur_keys and by_id[i]["key"] != cur_keys[i]:
            rep.add("ERROR", "V23", f"legacy civic {i} key {cur_keys[i]} -> {by_id[i]['key']}")

    def closure_of(roots):
        out = set()
        for r in roots:
            if r in by_id:
                out.add(r)
                out |= must_closure(r, ast)
        return out
    stage_atoms = must_hold_atoms(kr.parse(tech["researchStage"]["requires"]))
    mandatory = closure_of(stage_atoms)
    # An Age research fact holds when ANY listed node is complete (Sim.Core/Systems/Ages/AgeContent.cs, 'research'
    # case), so only what every alternative must hold is mandatory: the intersection of the alternatives' closures.
    # (Before 2026-10-23 this took the union, which marked one branch's private ancestors, e.g. ore_prospecting under
    # tin_bronze, as mandatory although arsenical_bronze satisfies a3_bronze without it.)
    for f in tech.get("ageFacts") or []:
        if f["role"] == "core" and f["fact"]["kind"] == "research":
            alts = [closure_of({x}) for x in f["fact"].get("nodes") or [] if x in by_id]
            if alts:
                mandatory |= set.intersection(*alts)
    # live gates outside the stage and the Age facts, READ FROM CONTENT (kr.live_gates: sim.json
    # governance.taxationRequires, research.json inst.university with institutions expanded) — never literals
    live = kr.live_gates(tech)
    for _, expr in live:
        mandatory |= closure_of(must_hold_atoms(kr.parse(expr)))
    stats["liveGates"] = [f"{label}: {expr}" for label, expr in live]
    macro = {n["id"] for n in civ["nodes"] if n["status"] != "retired" and (n.get("civic") or {}).get("category") == "macro"}
    # the D11 case study is optional except its declared trunk geology path (technology-tree-proposal.md §4:
    # "ore_prospecting (A3, trunk)"): the branch field is the declaration, so no id is listed here
    nuclear_new = {n["id"] for n in tech["nodes"] if n["status"] == "new" and n.get("lane", "").startswith("D11")
                   and n.get("branch") != "trunk"}
    # declared optional: a civic whose civic.required is false (every micro / regime-form civic but the gate civics)
    optional_civ = {n["id"] for n in civ["nodes"] if n["status"] != "retired" and (n.get("civic") or {}).get("required") is False}
    # V24 optional content must not become mandatory through prerequisite closure
    for i in sorted(mandatory & macro):
        rep.add("ERROR", "V24", f"regime form {i} is in the mandatory closure of the research stage, an Age CORE fact or a live gate")
    for i in sorted((mandatory & optional_civ) - macro):
        rep.add("ERROR", "V24", f"civic {i} is declared optional (civic.required false) but is in the mandatory closure of "
                                "the research stage, an Age CORE fact or a live gate")
    for i in sorted(mandatory & nuclear_new):
        rep.add("ERROR", "V24", f"case-study node {i} (lane D11, knowledge only — D-062 R10) is in the mandatory closure of "
                                "the research stage, an Age CORE fact or a live gate")
    for n in civ["nodes"]:
        if n["status"] != "retired" and (n.get("civic") or {}).get("required") is True and n["id"] not in mandatory:
            rep.add("WARN", "V24", f"civic {n['id']} is declared required but no stage, Age CORE fact or live gate needs it")
    # V25 no Age fact (core or supporting) requires a regime form: Age transitions never grant or demand a government
    for f in tech.get("ageFacts") or []:
        for i in f["fact"].get("nodes") or []:
            if i in macro:
                rep.add("ERROR", "V25", f"Age fact {f['milestone']} names regime form {i}")
    # V26 civic gate consumers (INFO): who waits on each legacy civic and which civics sit in mandatory closures
    ent_req = defaultdict(list)
    changed = {ch["entity"]: ch.get("newRequires") for ch in tech.get("entityChanges") or []}
    for e in inv["entities"]:
        req = changed.get(e["id"], e.get("requires"))
        for a in kr.atoms(kr.parse(req)):
            ent_req[a].append(e["id"])
    gates = {}
    for i in LEGACY_CIVICS:
        if i not in by_id:
            continue
        direct = sorted(n["id"] for n in active if i in kr.atoms(ast[n["id"]]))
        must = sorted(n["id"] for n in active if n["id"] != i and i in must_closure(n["id"], ast))
        facts = sorted(f["milestone"] for f in tech.get("ageFacts") or []
                       if i in (f["fact"].get("nodes") or []) or any(i in must_closure(x, ast) | {x}
                                                                   for x in f["fact"].get("nodes") or [] if x in by_id))
        gates[i] = {"directDependants": len(direct), "mustDependants": len(must), "ageFacts": facts,
                    "researchStage": i in mandatory and i in closure_of(stage_atoms),
                    "entities": sorted(ent_req.get(i, []))}
        rep.add("INFO", "V26", f"{i}: {len(direct)} direct / {len(must)} must-closure dependants; Age facts {facts or '-'}; "
                               f"research stage {'YES' if gates[i]['researchStage'] else 'no'}; entities {gates[i]['entities'] or '-'}")
    # V27 every capability promise names its class, owner and milestone; nothing deferred claims to work today
    classes = Counter()
    for n in active:
        c = n.get("capability") or {}
        rc = c.get("realizationClass")
        classes[rc] += 1
        if rc is None or not c.get("realizationOwner") or not c.get("milestone"):
            rep.add("ERROR", "V27", f"{n['id']}: capability lacks realization class, owner or milestone")
        elif rc == "EXISTING" and c.get("status") != "realized-today":
            rep.add("ERROR", "V27", f"{n['id']}: EXISTING but its capability is not realized today")
        elif rc in ("DEFERRED", "NEW MECHANIC REQUIRED") and c.get("milestone") == "existing":
            rep.add("ERROR", "V27", f"{n['id']}: {rc} but its milestone says 'existing'")
    stats["realizationClass"] = dict(classes)
    civ_mand = sorted(mandatory & {n["id"] for n in civ["nodes"]})
    rep.add("INFO", "V24", f"mandatory closure (research stage, Age CORE research facts, live gates {[l for l, _ in live]}): {len(mandatory)} nodes, of which "
                           f"civics {civ_mand}, regime forms {len(mandatory & macro)}, case-study nodes {len(mandatory & nuclear_new)}")
    stats.update({"mandatoryClosure": len(mandatory), "civicsInMandatory": civ_mand,
                  "regimeFormsInMandatory": len(mandatory & macro), "caseStudyInMandatory": len(mandatory & nuclear_new),
                  "legacyCivicGates": {k: {"must": v["mustDependants"], "stage": v["researchStage"],
                                           "ageFacts": len(v["ageFacts"]), "entities": len(v["entities"])}
                                       for k, v in gates.items()}})
    return stats


PAGE_READ = {"page-read", "document-read"}


def shingles(text, k=5):
    w = re.findall(r"[a-z0-9]+", (text or "").lower())
    return {" ".join(w[i:i + k]) for i in range(max(0, len(w) - k + 1))}


def check_corpus(rep, tech, civ):
    stats = {}
    index_path = kr.PKG / "corpus" / "index.json"
    files = sorted((kr.PKG / "corpus").glob("*.json"))
    files = [f for f in files if f.name not in ("index.json", "plan.json")]
    entries = []
    for f in files:
        for e in kr.load_json(f):
            e["_file"] = f.name
            entries.append(e)
    active = {n["id"]: n for n in tech["nodes"] + civ["nodes"] if n["status"] != "retired"}
    seen = Counter(e["id"] for e in entries)
    for i, c in seen.items():
        if c > 1:
            rep.add("ERROR", "C01", f"{i}: {c} corpus entries")
        if i not in active:
            rep.add("ERROR", "C01", f"corpus entry for {i}, which is not an active proposed node")
    missing = sorted(set(active) - set(seen))
    for i in missing:
        rep.add("ERROR", "C01", f"{i}: no corpus entry")
    reg = {}
    reg_path = kr.PKG / "sources" / "register.json"
    if reg_path.exists():
        for s in kr.load_json(reg_path):
            reg[s["id"]] = s
    else:
        rep.add("ERROR", "C03", "sources/register.json missing")
    wc = []
    band = Counter()
    status = Counter()
    ship = Counter()
    queue = defaultdict(list)
    sh = {}
    for e in entries:
        n = kr.word_count(e.get("description"))
        wc.append(n)
        if 50 <= n <= 60:
            band["50-60"] += 1
        elif 45 <= n <= 65:
            band["45-49/61-65"] += 1
            rep.add("INFO", "C02", f"{e['id']}: {n} words (inside the 45–65 allowance)")
        else:
            band["outside"] += 1
            rep.add("ERROR", "C02", f"{e['id']}: {n} words (outside 45–65)")
        if e.get("wordCount") is not None and e["wordCount"] != n:
            rep.add("WARN", "C02", f"{e['id']}: stored wordCount {e['wordCount']} != counted {n}")
        srcs = e.get("sources") or []
        if not srcs:
            if (e.get("factCheck") or {}).get("status") == "unverified":
                rep.add("WARN", "C03", f"{e['id']}: BLOCKED, sourcing pending — unverified entry with no retrieved "
                                       "source (never a substitute for a source)")
            else:
                rep.add("ERROR", "C03", f"{e['id']}: no sources but status is not 'unverified'")
        for s in srcs:
            if s.get("sourceId") not in reg:
                rep.add("ERROR", "C03", f"{e['id']}: source {s.get('sourceId')} not in the register")
        st = (e.get("factCheck") or {}).get("status", "missing")
        status[st] += 1
        # C11 evidence agreement (2026-10-22): a status may never claim more than its cited evidence shows
        retrievals = {(reg.get(s.get("sourceId")) or {}).get("retrieval", "web-search-result") for s in srcs}
        results = [c.get("result") for c in ((e.get("factCheck") or {}).get("claims") or [])]
        if st == "source-checked" and not (retrievals & PAGE_READ):
            rep.add("ERROR", "C11", f"{e['id']}: 'source-checked' but no cited source was read as a page or document")
        # 2026-10-23 (Director review item 3): a claim is source-checked only against a page someone opened — EACH
        # supported or disputed claim must cite a page-read / document-read source; one opened page cannot certify
        # claims that rest on search results
        if st == "source-checked":
            claims = (e.get("factCheck") or {}).get("claims") or []
            if not claims:
                rep.add("ERROR", "C11", f"{e['id']}: 'source-checked' but lists no checked claim")
            for c in claims:
                if c.get("result") in ("supported", "disputed") and not any(
                        (reg.get(s) or {}).get("retrieval") in PAGE_READ for s in c.get("sources") or []):
                    rep.add("ERROR", "C11", f"{e['id']}: 'source-checked' but the claim {c.get('claim', '')[:60]!r} "
                                            "rests on no page or document that was read")
        # 'disputed' = the disagreement itself is sourced and the text presents the point as disputed
        if st == "search-corroborated" and results and any(r not in ("supported", "corrected", "disputed") for r in results):
            rep.add("ERROR", "C11", f"{e['id']}: 'search-corroborated' but a listed claim is {sorted(set(results))}")
        if st == "partially-checked" and results and not any(r in ("supported", "corrected") for r in results):
            rep.add("ERROR", "C11", f"{e['id']}: 'partially-checked' but no listed claim is supported")
        if st == "unverified" and any(r in ("supported", "corrected") for r in results):
            rep.add("WARN", "C11", f"{e['id']}: 'unverified' but a listed claim is supported (status may be understated)")
        if st in ("partially-checked", "search-corroborated") and not results:
            ship["notItemised"] += 1
        confs = {(reg.get(s.get("sourceId")) or {}).get("confidence") for s in srcs}
        if st == "search-corroborated" and srcs and confs <= {"low", None}:
            rep.add("ERROR", "C11", f"{e['id']}: 'search-corroborated' rests only on low-confidence sources")
        for s in srcs:
            if s.get("sourceId") in reg and not s.get("claims"):
                rep.add("WARN", "C11", f"{e['id']}: cites {s.get('sourceId')} without naming the claim it supports")
        # C10 shipping gate: only source-checked text with every listed claim supported may ship as fact
        if st == "source-checked" and all(r in ("supported", "corrected", "disputed") for r in results):
            ship["ready"] += 1
        else:
            ship["blocked"] += 1
            queue[st].append(e["id"])
        if st == "missing":
            rep.add("ERROR", "C07", f"{e['id']}: no factCheck status")
        if INEVITABLE.search(e.get("description") or ""):
            rep.add("WARN", "C05", f"{e['id']}: inevitability wording: {INEVITABLE.search(e['description']).group(0)!r}")
        if GAMEY.search(e.get("description") or ""):
            rep.add("WARN", "C06", f"{e['id']}: game-mechanic wording in an educational text: "
                                   f"{GAMEY.search(e['description']).group(0)!r}")
        for q in re.findall(r"[\"“]([^\"”]+)[\"”]", e.get("description") or ""):
            if kr.word_count(q) > 10:
                rep.add("WARN", "C08", f"{e['id']}: quotation of {kr.word_count(q)} words")
        sh[e["id"]] = shingles(e.get("description"))
    texts = Counter((e.get("description") or "").strip().lower() for e in entries)
    for t, c in texts.items():
        if c > 1 and t:
            rep.add("ERROR", "C04", f"exact duplicate description used {c} times: {t[:60]}…")
    ids = sorted(sh)
    near = 0
    for i in range(len(ids)):
        a = sh[ids[i]]
        if not a:
            continue
        for j in range(i + 1, len(ids)):
            b = sh[ids[j]]
            if not b:
                continue
            jac = len(a & b) / len(a | b)
            if jac >= 0.25:
                near += 1
                rep.add("WARN", "C04", f"near-duplicate {ids[i]} ~ {ids[j]} (5-gram Jaccard {jac:.2f})")
    rep.add("WARN" if ship["blocked"] else "INFO", "C10",
            f"SHIPPING GATE: {ship['ready']} of {len(entries)} entries may ship as fact; {ship['blocked']} are BLOCKED "
            f"(not source-checked: {', '.join(f'{k} {len(v)}' for k, v in sorted(queue.items()))}). Zero structural errors "
            "is not a fact-check; the mechanic may ship without its educational text, unsourced text may not.")
    if ship["notItemised"]:
        rep.add("WARN", "C11", f"{ship['notItemised']} entries above 'unverified' list no per-claim results: their status "
                               "rests on the claims recorded per source, so C11 cannot check them claim by claim "
                               "(per node: corpus/unresolved-claims.md)")
    used = defaultdict(set)
    for e in entries:
        for s in e.get("sources") or []:
            used[s.get("sourceId")].add(e["id"])
    stats.update({"entries": len(entries), "files": len(files), "missing": len(missing),
                  "wordBands": dict(band), "wordMin": min(wc) if wc else None, "wordMax": max(wc) if wc else None,
                  "wordMean": round(sum(wc) / len(wc), 1) if wc else None, "factCheck": dict(status),
                  "registerSources": len(reg), "sourcesUsed": len(used), "nearDuplicatePairs": near,
                  "sourceTypes": dict(Counter(s.get("type") for s in reg.values())),
                  "shipGate": {"ready": ship["ready"], "blocked": ship["blocked"]}, "notItemised": ship["notItemised"],
                  "retrieval": dict(Counter(s.get("retrieval", "web-search-result") for s in reg.values()))})
    if index_path.exists():
        idx = kr.load_json(index_path)
        listed = {x["file"] for x in idx.get("files", [])}
        for f in files:
            if f.name not in listed:
                rep.add("ERROR", "C09", f"corpus file {f.name} not listed in corpus/index.json")
    else:
        rep.add("ERROR", "C09", "corpus/index.json missing")
    return stats


def main(argv):
    rep = Report()
    tech = kr.load_json(kr.PKG / "technology-tree-proposal.json")
    civ = kr.load_json(kr.PKG / "civics-tree-proposal.json")
    inv = kr.load_json(kr.DATA / "current-tree-inventory.json")
    gstats = check_graphs(rep, tech, civ, inv)
    gstats["gates"] = check_gates(rep, tech, civ, inv)
    cstats = {} if "--no-corpus" in argv else check_corpus(rep, tech, civ)
    lines = ["# Validation report — knowledge redesign package", "",
             "Generated by `tools/validate.py`. **Structural validity is not historical verification**: this report "
             "checks shape, references, counts and wording rules. Fact-check status is recorded per corpus entry.", "",
             f"- ERROR: {rep.count('ERROR')} · WARN: {rep.count('WARN')} · INFO: {rep.count('INFO')}",
             f"- **Shipping gate (C10): {(cstats.get('shipGate') or {}).get('ready', 'n/a')} entries may ship as fact; "
             f"{(cstats.get('shipGate') or {}).get('blocked', 'n/a')} are blocked** until source-checked "
             "(remaining queue: `corpus/sourcing-queue.md`).",
             f"- graph: {gstats}", f"- corpus: {cstats}", "", "| level | code | finding |", "|---|---|---|"]
    order = {"ERROR": 0, "WARN": 1, "INFO": 2}
    for lvl, code, msg in sorted(rep.rows, key=lambda r: (order[r[0]], r[1], r[2])):
        lines.append(f"| {lvl} | {code} | {msg.replace('|', '/')} |")
    (kr.PKG / "validation-report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"ERROR {rep.count('ERROR')} WARN {rep.count('WARN')} INFO {rep.count('INFO')}")
    print("graph", gstats)
    print("corpus", cstats)
    return 1 if rep.count("ERROR") else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
