#!/usr/bin/env python3
"""Integrate the design lanes' part files into the canonical proposal files.

DESIGN-PACKAGE TOOLING ONLY. Reads data/current-tree-inventory.json,
data/partition.json and data/proposal-parts/*.json; writes
technology-tree-proposal.json and civics-tree-proposal.json (PROPOSED data;
never runtime content). Deterministic: the same inputs give byte-identical
outputs (sorted iteration everywhere, no clocks, no randomness).

Usage: python3 -I tools/integrate.py [--allow-incomplete]
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

PARTS = kr.DATA / "proposal-parts"
U, K = 92.4, 2.0
FACTORS = ["novelty", "difficulty", "material", "institutional", "breadth", "prereq_complexity"]
BRANCH_ORDER = {b: i for i, b in enumerate(kr.BRANCHES)}
AGE_ORDER = {a: i for i, a in enumerate(kr.AGES)}
TECH_KEY_START, TECH_KEY_MAX = 427, 999
CIVIC_KEY_START, CIVIC_KEY_MAX = 1008, 1999
RETIRING = {"retire", "merge", "replace"}
UNIVERSITY_TYPE = {"military": 1, "medicine": 2, "engineering": 3, "natural_science": 4, "agriculture": 5}

OWNER_BY_CLASS = {
    "B": ("realized-today", "existing"), "C": ("knowledge-only", "existing"),
    "D": ("realized-today", "existing"), "E": ("realized-today", "existing"),
    "F": ("deferred", "M7"), "H": ("knowledge-only", "existing"), "J": ("deferred", "M6"),
}


def cost_of(magnitude):
    return float(round(U * (K ** magnitude) / 10.0) * 10)


def magnitude_of(factors):
    return round(sum(float(factors.get(f, 0.0)) for f in FACTORS), 4)


def rewrite_atoms(expr, mapping):
    if not expr:
        return expr
    return re.sub(r"[A-Za-z_][A-Za-z0-9_.]*",
                  lambda m: mapping.get(m.group(0), m.group(0)) if m.group(0) not in ("AND", "OR", "NOT") else m.group(0),
                  expr)


def auto_capability(cur):
    """Capability block for an existing node from the CI-checked unlock audit and its unlocks."""
    ua = cur.get("unlockAudit") or {}
    cls = (ua.get("class") or "C")[:1]
    status, milestone = OWNER_BY_CLASS.get(cls, ("knowledge-only", "existing"))
    unl = cur.get("unlocks") or {}
    grants = []
    for c in unl.get("capabilities", []):
        grants.append(f"knowledge:{c}")
    for k, prefix in (("units", "unit"), ("buildings", "building"), ("institutions", "institution"),
                      ("infrastructure", "infrastructure"), ("activities", "production"),
                      ("projects", "building"), ("techniques", "knowledge"), ("applications", "knowledge")):
        for v in unl.get(k, []):
            grants.append(f"{prefix}:{v}")
    if cur.get("researchStageAtom"):
        grants.append("stage:research_stage")
    if cur.get("taxGateAtom"):
        grants.append("tax:tax_edict")
    for m in cur.get("ageMilestones") or []:
        grants.append(f"age:{m['milestone']}")
    cons = ua.get("consequences") or ""
    owner = "none"
    for needle, own in (("recipe", "ProductionSystem"), ("activity", "ProductionSystem (labour activity)"),
                        ("trade", "TradeArbitrageSystem"), ("tax", "GovernanceSystem"),
                        ("road", "RoadDevelopmentSystem"), ("inst.", "InstitutionsSystem"),
                        ("building.", "ConstructionSystem"), ("unit.", "Battle layer (M7)"),
                        ("stage", "ResearchSystem (research stage)"), ("Age", "AgeTransitionSystem")):
        if needle in cons:
            owner = own
            break
    if cls == "F":
        owner = "Battle layer (M7)"
    if cls == "H":
        owner = "ResearchSystem (repeatable)"
    if owner == "none" and any(m for m in cur.get("ageMilestones") or []):
        owner = "AgeTransitionSystem (milestone fact)"
    return {
        "grants": grants, "realizationOwner": owner, "status": status, "milestone": milestone,
        "currentEffect": ("unlock-audit class " + (ua.get("class") or "?") + "; consequences: " + (cons or "—")),
        "deferredEffect": None, "inputs": {"resources": [], "geography": [], "infrastructure": [], "institutions": []},
        "missingInputs": "default", "unknownResource": "default", "aiValuation": "default",
        "playerCommunication": "default", "autoFilled": True,
    }


def load_parts():
    parts = {}
    if PARTS.exists():
        for p in sorted(PARTS.glob("*.json")):
            part = kr.load_json(p)
            parts[part["lane"]] = part
    return parts


def main(argv):
    allow_incomplete = "--allow-incomplete" in argv
    inv = kr.load_json(kr.DATA / "current-tree-inventory.json")
    partition = kr.load_json(kr.DATA / "partition.json")["lanes"]
    owner_of = {nid: lane for lane, ids in partition.items() for nid in ids}
    parts = load_parts()
    problems = []
    cur = {n["id"]: n for n in inv["nodes"]}

    # 1. existing records, lane by lane
    reviewed, overrides = {}, {}
    for lane in sorted(parts):
        for rec in parts[lane].get("existing", []):
            nid = rec["id"]
            if nid not in cur:
                problems.append(f"{lane}: existing id {nid!r} is not a current node")
                continue
            if owner_of.get(nid) != lane:
                problems.append(f"{lane}: {nid!r} is owned by {owner_of.get(nid)}")
                continue
            if nid in reviewed:
                problems.append(f"{lane}: {nid!r} listed twice")
                continue
            reviewed[nid] = lane
            overrides[nid] = rec
    unreviewed = sorted(set(cur) - set(reviewed))
    if unreviewed:
        msg = f"{len(unreviewed)} existing node(s) not reviewed by their lane (kept unchanged): " + \
              ", ".join(unreviewed[:12]) + ("…" if len(unreviewed) > 12 else "")
        problems.append(msg)

    # 2. new records
    new_recs, new_ids = [], set()
    for lane in sorted(parts):
        for rec in parts[lane].get("new", []):
            nid = rec["id"]
            if nid in cur or nid in new_ids:
                problems.append(f"{lane}: new id {nid!r} collides")
                continue
            new_ids.add(nid)
            new_recs.append((lane, rec))

    # 3. mapping for retired nodes
    mapping = {}
    for nid, rec in overrides.items():
        d = set(rec.get("disposition") or [])
        if "merge" in d and rec.get("mergeInto"):
            mapping[nid] = rec["mergeInto"]
        elif "replace" in d and rec.get("replacedBy"):
            mapping[nid] = rec["replacedBy"]

    tech, civ = [], []
    for n in inv["nodes"]:
        nid = n["id"]
        rec = overrides.get(nid, {"disposition": ["keep"], "rationale": "not reviewed", "changes": "none"})
        disp = list(rec.get("disposition") or ["keep"])
        retired = bool(set(disp) & RETIRING)
        factors = rec.get("factors") or n.get("factors") or {}
        rebalanced = "factors" in rec
        mag = magnitude_of(factors) if rebalanced else n.get("magnitude")
        cost = cost_of(mag) if rebalanced else n.get("cost")
        out = {
            "key": n["key"], "id": nid, "status": "retired" if retired else "existing", "disposition": disp,
            "lane": owner_of.get(nid), "name": rec.get("name", n["name"]), "tree": n["tree"],
            "branch": rec.get("branch", n["branch"]), "domain": rec.get("domain", n.get("domain")),
            "secondaryDomains": rec.get("secondaryDomains", n.get("secondaryDomains") or []),
            "age": rec.get("age", n["age"]), "emerged": rec.get("emerged", n.get("emerged")),
            "prereq": rec.get("prereq", rewrite_atoms(n.get("prereq"), mapping)),
            "prereqChanged": "prereq" in rec, "prereqRationale": rec.get("prereqRationale"),
            "oldPrereq": n.get("prereq"),
            "cost": cost, "oldCost": n.get("cost"), "magnitude": mag, "oldMagnitude": n.get("magnitude"),
            "factors": factors, "oldFactors": n.get("factors"), "costRationale": rec.get("costRationale"),
            "nodeKind": rec.get("nodeKind"),
            "capability": rec.get("capability") or auto_capability(n),
            "eurekas": rec.get("eurekas", n.get("eurekas") or []),
            "family": rec.get("family", n.get("family")), "generation": rec.get("generation", n.get("generation")),
            "repeatable": n.get("repeatable"), "set": n.get("set"),
            "unlocks": n.get("unlocks") or {},
            "universityRelevance": rec.get("universityRelevance", n.get("universityRelevance") or []),
            "splitInto": rec.get("splitInto"), "mergedInto": rec.get("mergeInto"), "replacedBy": rec.get("replacedBy"),
            "retireReason": rec.get("retireReason"),
            "rationale": rec.get("rationale"), "changes": rec.get("changes", "none"),
            "compatibility": rec.get("compatibility"),
            "oldDesc": n.get("desc"),
        }
        if n["tree"] == "civics":
            out["civic"] = rec.get("civic")
            civ.append(out)
        else:
            tech.append(out)

    # 4. new nodes (keys assigned deterministically)
    def new_sort(item):
        lane, r = item
        return (AGE_ORDER.get(r.get("age"), 99), BRANCH_ORDER.get(r.get("branch") or "trunk", 99), r["id"])

    def civic_sort(item):
        lane, r = item
        cat = (r.get("civic") or {}).get("category", "micro")
        return (AGE_ORDER.get(r.get("age"), 99), 0 if cat == "micro" else 1, r["id"])

    tech_new = sorted([x for x in new_recs if x[0] != "D12-civics"], key=new_sort)
    civ_new = sorted([x for x in new_recs if x[0] == "D12-civics"], key=civic_sort)
    for i, (lane, r) in enumerate(tech_new):
        key = TECH_KEY_START + i
        if key > TECH_KEY_MAX:
            problems.append(f"technology key range exhausted at {r['id']}")
        tech.append(new_record(lane, r, key, "technology", mapping))
    for i, (lane, r) in enumerate(civ_new):
        key = CIVIC_KEY_START + i
        if key > CIVIC_KEY_MAX:
            problems.append(f"civic key range exhausted at {r['id']}")
        civ.append(new_record(lane, r, key, "civics", mapping))

    # 4b. integration overrides (cross-lane requests adjudicated by the integrator; each with a reason)
    ov_path = kr.DATA / "integration-overrides.json"
    applied = []
    if ov_path.exists():
        recs = {n["id"]: n for n in tech + civ}
        for o in kr.load_json(ov_path).get("overrides", []):
            n = recs.get(o["id"])
            if n is None:
                problems.append(f"override for unknown id {o['id']!r}")
                continue
            for k, v in (o.get("set") or {}).items():
                if k == "factors":
                    n["factors"] = v
                    n["magnitude"] = magnitude_of(v)
                    n["cost"] = cost_of(n["magnitude"])
                elif k == "prereq":
                    n["prereq"] = v
                    n["prereqChanged"] = True
                else:
                    n[k] = v
            for k, v in (o.get("capabilityPatch") or {}).items():
                n.setdefault("capability", {})[k] = v
            if o.get("dispositionAdd"):
                for d in o["dispositionAdd"]:
                    if d not in n["disposition"]:
                        n["disposition"].append(d)
            n.setdefault("integrationNotes", []).append({"reason": o.get("reason"), "source": o.get("source"),
                                                         "fields": sorted(list((o.get("set") or {}).keys()) +
                                                                          list((o.get("capabilityPatch") or {}).keys()))})
            applied.append(o["id"])

    # 5. derived metrics on the active proposed graph (both trees, one key space)
    active = [n for n in tech + civ if n["status"] != "retired"]
    g = kr.Graph(active)
    missing = g.missing_refs()
    for a, b in missing:
        problems.append(f"{a}: prerequisite {b!r} is not an active proposed node")
    cyc = g.find_cycle()
    if cyc:
        problems.append("cycle: " + " -> ".join(cyc))
    stage_expr = rewrite_atoms(inv["summary"]["researchStage"]["requires"], mapping)
    stage_override = None
    for lane in sorted(parts):
        if parts[lane].get("researchStage"):
            stage_override = parts[lane]["researchStage"]
    if stage_override:
        stage_expr = stage_override["requires"]
    age_facts = []
    for f in inv["summary"]["ageFacts"]:
        f2 = dict(f)
        fact = dict(f["fact"])
        if fact.get("kind") == "research":
            fact["nodes"] = [mapping.get(x, x) for x in fact["nodes"]]
        f2["fact"] = fact
        age_facts.append(f2)
    for lane in sorted(parts):
        for ch in parts[lane].get("ageFactChanges", []):
            for f2 in age_facts:
                if f2["milestone"] == ch["milestone"]:
                    f2["fact"] = ch["fact"]
                    f2["changed"] = ch.get("rationale")
    gate_exprs = [stage_expr] + ["(" + " OR ".join(f["fact"]["nodes"]) + ")" for f in age_facts
                                  if f["role"] == "core" and f["fact"].get("kind") == "research"]
    gate_ancestors = set()
    if not missing and not cyc:
        for e in gate_exprs:
            for a in kr.atoms(kr.parse(e)):
                if a in g.nodes:
                    gate_ancestors |= g.all_ancestors(a) | {a}
        for n in active:
            nid = n["id"]
            n["depth"] = g.depth_longest(nid)
            n["derived"] = {"depthMin": g.depth(nid), "depthLongest": g.depth_longest(nid),
                            "closureCount": len(g.closure(nid)),
                            "closureCost": round(g.closure_cost(nid), 1), "dependants": sorted(g.deps[nid]),
                            "optional": nid not in gate_ancestors}

    # 6. entity-requirement changes (lane parts' entityRequirementChanges + overrides' entityChanges)
    entity_changes = []
    for lane in sorted(parts):
        for ch in parts[lane].get("entityRequirementChanges") or []:
            entity_changes.append(dict(ch, lane=lane))
    if ov_path.exists():
        for ch in kr.load_json(ov_path).get("entityChanges", []):
            entity_changes = [c for c in entity_changes if c["entity"] != ch["entity"]] + [dict(ch, lane="integration")]
    entity_changes.sort(key=lambda c: c["entity"])

    retired = [{"key": n["key"], "id": n["id"], "disposition": n["disposition"],
                "mappedTo": mapping.get(n["id"]), "reason": n.get("retireReason") or n.get("rationale")}
               for n in tech + civ if n["status"] == "retired"]
    common = {"status": "PROPOSED — design data for Director review; not ratified; not runtime content",
              "provenance": {"baselineHead": inv["provenance"]["gitHead"],
                             "inventory": "data/current-tree-inventory.json",
                             "lanes": sorted(parts)},
              "conventions": {"keyRanges": {"technology": [1, TECH_KEY_MAX], "civics": [1000, CIVIC_KEY_MAX],
                                            "newTechnologyFrom": TECH_KEY_START, "newCivicFrom": CIVIC_KEY_START},
                              "costModel": "BaseCost = 92.4 x 2^magnitude, rounded to 10; magnitude = sum of six factors"},
              "problems": problems}
    tech_doc = dict(common, **{
        "_schema": "kr/technology-tree-proposal@1",
        "_doc": ["Canonical PROPOSED technology graph (see data-schema.md). Generated by tools/integrate.py from the lane "
                 "part files; edit the parts, not this file."],
        "researchStage": {"requires": stage_expr, "override": stage_override},
        "entityChanges": entity_changes,
        "ageFacts": age_facts, "retiredKeys": [r for r in retired if r["key"] < 1000],
        "nodes": sorted(tech, key=lambda n: n["key"])})
    civ_doc = dict(common, **{
        "_schema": "kr/civics-tree-proposal@1",
        "_doc": ["Canonical PROPOSED civics graph (see data-schema.md). Generated by tools/integrate.py."],
        "civicsMeta": (parts.get("D12-civics") or {}).get("meta"),
        "retiredKeys": [r for r in retired if r["key"] >= 1000],
        "nodes": sorted(civ, key=lambda n: n["key"])})
    kr.dump_json(tech_doc, kr.PKG / "technology-tree-proposal.json")
    kr.dump_json(civ_doc, kr.PKG / "civics-tree-proposal.json")
    print(f"integration overrides applied: {len(applied)}")
    print(f"technology: {len(tech)} records ({sum(1 for n in tech if n['status']=='new')} new, "
          f"{sum(1 for n in tech if n['status']=='retired')} retired); civics: {len(civ)} records "
          f"({sum(1 for n in civ if n['status']=='new')} new)")
    for p in problems:
        print("PROBLEM:", p)
    if problems and not allow_incomplete:
        return 1
    return 0


def new_record(lane, r, key, tree, mapping):
    factors = r.get("factors") or {}
    mag = magnitude_of(factors)
    return {
        "key": key, "id": r["id"], "status": "new", "disposition": ["new"], "lane": lane,
        "name": r["name"], "tree": tree, "branch": r.get("branch") or "trunk", "domain": r.get("domain"),
        "secondaryDomains": r.get("secondaryDomains") or [], "age": r.get("age"), "emerged": r.get("emerged"),
        "prereq": rewrite_atoms(r.get("prereq"), mapping), "prereqChanged": True,
        "prereqRationale": r.get("prereqRationale"), "oldPrereq": None,
        "cost": cost_of(mag), "oldCost": None, "magnitude": mag, "oldMagnitude": None, "factors": factors,
        "oldFactors": None, "costRationale": r.get("costRationale"), "costAnalog": r.get("costAnalog"),
        "nodeKind": r.get("nodeKind"), "capability": r.get("capability"), "eurekas": r.get("eurekas") or [],
        "family": r.get("family"), "generation": r.get("generation"), "repeatable": None, "set": "finite",
        "unlocks": r.get("unlocks") or {}, "splitFrom": r.get("splitFrom"), "whyDistinct": r.get("whyDistinct"),
        "universityRelevance": r.get("universityRelevance") or (
            [{"type": UNIVERSITY_TYPE[r["primaryUniversity"]], "role": "primary"}] if r.get("primaryUniversity") else []),
        "acceptanceTest": r.get("acceptanceTest"), "rationale": r.get("rationale"), "changes": "new node",
        "compatibility": r.get("compatibility"), "civic": r.get("civic"),
    }


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
