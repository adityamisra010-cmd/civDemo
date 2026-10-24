"""Import tranche 2 into the runtime research content (D-063; tranche2-contract.md).

Builds Sim.Data/content/research.json from:
  - data/tranche2/research.pre-tranche2.json  the pristine pre-tranche node arrays (496 technologies, 67 civics)
  - Sim.Data/content/research.json             current file: every section other than the node arrays is kept as is
                                               (engine tracks own tuning switches); nodes it holds that are neither
                                               pristine nor tranche-2 imports (e.g. engine-added repeatables) are kept
  - civics-tree-proposal.json                  the 121 D13 civics (keys 1068-1188 kept)
  - data/tranche2/out/*.json                   lane-group audit records (effects, dependency changes) and new nodes
  - data/tranche2/decisions.json               integrator decisions: which proposed dependency changes are applied
Re-runnable: it always starts from the pristine arrays. Writes data/tranche2/import-report.json.

Usage: python3 -I tools/import_tranche2.py [--dry-run]
"""
import json
import sys
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent
REPO = PKG.parents[2]
sys.path.insert(0, str(PKG / "tools"))
import kr_lib as kr  # noqa: E402

RESEARCH = REPO / "Sim.Data" / "content" / "research.json"
T2 = PKG / "data" / "tranche2"
U, K = 92.4, 2.0
AGES = [f"A{i}" for i in range(1, 10)]
FACTORS = ["novelty", "difficulty", "material", "institutional", "breadth", "prereq_complexity"]
RANGES = {"novelty": (0.5, 2.0), "difficulty": (0.0, 3.5), "material": (0.0, 2.0), "institutional": (0.0, 3.0),
          "breadth": (0.0, 1.5), "prereq_complexity": (0.0, 1.0)}
UNIV_BY_DOMAIN = {"military": 1, "naval": 1, "medicine": 2, "engineering": 3, "energy": 3, "industry": 3,
                  "construction": 3, "infrastructure": 3, "transport": 3, "communication": 3, "materials": 3,
                  "science": 4, "environment": 4, "agriculture": 5, "food": 5}
UNIV_NAMES = {1: "Military", 2: "Medical", 3: "Engineering", 4: "Natural Science", 5: "Agricultural"}
BRANCH_UNIV = {"military": 1, "medicine": 2, "engineering": 3, "natural_science": 4, "agriculture": 5}


def cost_of(m):
    return round(U * K ** m / 10.0) * 10.0


def snap_factors(f):
    out = {}
    for k in FACTORS:
        v = float(f.get(k, 0.0))
        lo, hi = RANGES[k]
        v = min(max(round(v * 4) / 4, lo), hi)
        out[k] = v
    return out


def unlocks_block(caps):
    return {"capabilities": caps, "units": [], "buildings": [], "institutions": [], "infrastructure": [],
            "activities": [], "projects": [], "techniques": [], "applications": []}


def main(argv):
    dry = "--dry-run" in argv
    pristine = kr.load_json(T2 / "research.pre-tranche2.json")
    current = kr.load_json(RESEARCH)
    groups = sorted((T2 / "out").glob("*.json"))
    outs = [kr.load_json(p) for p in groups]
    decisions = kr.load_json(T2 / "decisions.json") if (T2 / "decisions.json").exists() else {}
    civp = {n["id"]: n for n in kr.load_json(PKG / "civics-tree-proposal.json")["nodes"]}
    report = {"applied": [], "rejected": [], "warnings": [], "review": [], "saturation": {}}
    review = kr.load_json(T2 / "review-new-nodes.json")["verdicts"] if (T2 / "review-new-nodes.json").exists() else []
    removed = {v["id"]: v for v in review if v["verdict"] in ("drop", "merge-into")}
    alias = {v["id"]: v["target"] for v in review if v["verdict"] == "merge-into" and v.get("target")}
    fixes = {v["id"]: v for v in review if v["verdict"] == "fix-effect" and v.get("newEffects")}

    tech = [dict(n) for n in pristine["technologies"]]
    civ = [dict(n) for n in pristine["civics"]]
    pristine_ids = {n["id"] for n in tech + civ}
    imported_ids = set()

    # 1. D13 civics (proposal keys kept)
    for n in sorted((x for x in civp.values() if x.get("lane") == "D13-civics-expansion"), key=lambda x: x["key"]):
        c = n.get("civic") or {}
        f = snap_factors(n["factors"])
        m = sum(f.values())
        civ.append({
            "key": n["key"], "id": n["id"], "name": n["name"], "desc": c.get("concept") or n["name"],
            "age": n["age"], "frontier": False, "emerged": n.get("emerged"), "domain": "governance",
            "secondaryDomains": [], "depth": 0, "cost": cost_of(m), "prereq": n.get("prereq"), "eurekas": [],
            "unlocks": unlocks_block([((n.get("unlocks") or {}).get("capabilities") or [n["name"].lower()])[0]]),
            "family": None, "generation": None, "effects": {"immediate": []}, "repeatable": None,
            "universityRelevance": [],
            "costRationale": {"tier": "T2", "factors": f, "magnitude": m, "content_cost": int(cost_of(m)),
                              "calibration_adjustment": 0, "note": "tranche 2: D13 civics expansion (D-063 R5)"}})
        imported_ids.add(n["id"])

    by_id = {n["id"]: n for n in tech + civ}
    lane_of = {}
    for o in outs:
        for r in o.get("audit", []):
            lane_of[r["id"]] = r.get("lane")

    # 2. New nodes from the lane groups (keys appended deterministically)
    new_tech, new_civ = [], []
    for o in outs:
        for n in o.get("newNodes", []):
            if n["id"] in removed:
                report["review"].append({"id": n["id"], "verdict": removed[n["id"]]["verdict"],
                                         "target": removed[n["id"]].get("target"), "why": removed[n["id"]].get("why")})
                continue
            if n["id"] in by_id or n["id"] in imported_ids:
                report["rejected"].append({"id": n["id"], "why": "id collides"})
                continue
            (new_civ if n.get("tree") == "civics" else new_tech).append(n)
            imported_ids.add(n["id"])
    def nkey(n):
        return (AGES.index(n["age"]), n.get("lane") or "", n["id"])
    next_tech = max(x["key"] for x in tech) + 1
    for n in sorted(new_tech, key=nkey):
        f = snap_factors(n.get("factors") or {})
        m = sum(f.values())
        dom = n.get("lane") or "science"
        branch = n.get("branch")
        univ = BRANCH_UNIV.get(branch) or UNIV_BY_DOMAIN.get(dom, 4)
        tech.append({
            "key": next_tech, "id": n["id"], "name": n["name"], "desc": n.get("desc") or n["name"], "age": n["age"],
            "frontier": False, "emerged": n.get("emerged"), "branch": branch, "domain": dom, "secondaryDomains": [],
            "depth": 0, "cost": cost_of(m), "prereq": n.get("prereq"), "eurekas": [],
            "unlocks": unlocks_block([n.get("desc") or n["name"]]), "family": None, "generation": None,
            "effects": {"immediate": []}, "repeatable": None,
            "universityRelevance": [{"type": univ, "name": UNIV_NAMES[univ], "role": "primary"}],
            "costRationale": {"tier": "T2", "factors": f, "magnitude": m, "content_cost": int(cost_of(m)),
                              "calibration_adjustment": 0, "note": "tranche 2: gap fill (D-063 R5)"}})
        next_tech += 1
    next_civ = max(x["key"] for x in civ if x["key"] < 1990) + 1
    for n in sorted(new_civ, key=nkey):
        f = snap_factors(n.get("factors") or {})
        f["material"] = 0.0
        m = sum(f.values())
        civ.append({
            "key": next_civ, "id": n["id"], "name": n["name"], "desc": n.get("desc") or n["name"], "age": n["age"],
            "frontier": False, "emerged": n.get("emerged"), "domain": "governance", "secondaryDomains": [],
            "depth": 0, "cost": cost_of(m), "prereq": n.get("prereq"), "eurekas": [],
            "unlocks": unlocks_block([n.get("desc") or n["name"]]), "family": None, "generation": None,
            "effects": {"immediate": []}, "repeatable": None, "universityRelevance": [],
            "costRationale": {"tier": "T2", "factors": f, "magnitude": m, "content_cost": int(cost_of(m)),
                              "calibration_adjustment": 0, "note": "tranche 2: civics expansion (D-063 R5)"}})
        next_civ += 1
    if next_civ > 1989:
        raise SystemExit(f"civic key range exhausted ({next_civ})")
    by_id = {n["id"]: n for n in tech + civ}
    import re as _re
    def rewrite(expr):
        if not expr:
            return expr
        return _re.sub(r"[a-z][a-z0-9_]*", lambda m: alias.get(m.group(0), m.group(0)), expr)
    for n in tech + civ:
        n["prereq"] = rewrite(n.get("prereq"))

    # 3a. Engine-owned fields of pristine nodes are taken from the current file (ADR-049 levels descriptors)
    cur_by_id = {n["id"]: n for n in current["technologies"] + current["civics"]}
    for n in tech + civ:
        if n["id"] in pristine_ids and n["id"] in cur_by_id and "repeatable" in cur_by_id[n["id"]]:
            n["repeatable"] = cur_by_id[n["id"]]["repeatable"]
    # 3. Engine-added nodes present in the current file (e.g. civics repeatables 1990+) are kept as they are
    for arr_name, arr in (("technologies", tech), ("civics", civ)):
        for n in current[arr_name]:
            if n["id"] not in pristine_ids and n["id"] not in by_id:
                arr.append(n)
                by_id[n["id"]] = n
    tech.sort(key=lambda x: x["key"])
    civ.sort(key=lambda x: x["key"])

    # 4. Effects for every node (audit records and new-node records)
    effect_src = {}
    for o in outs:
        for r in o.get("audit", []) + o.get("newNodes", []):
            effect_src[r["id"]] = r
    for nid, r in effect_src.items():
        n = by_id.get(nid)
        if n is None:
            continue
        effs = r.get("effects") or []
        if nid in fixes:
            effs = [{"operand": o, "delta": d} for o, d in fixes[nid]["newEffects"]]
        realized = [{"operand": e["operand"], "delta": round(float(e["delta"]), 4)} for e in effs]
        n["effects"] = {"immediate": [], "realized": realized} if realized else {"immediate": []}
    # merged nodes hand their effects to the surviving node (an operand already present keeps the larger delta)
    for src, dst in alias.items():
        r, n = effect_src.get(src), by_id.get(dst)
        if r is None or n is None:
            continue
        cur = {e["operand"]: e for e in (n.get("effects") or {}).get("realized", [])}
        for e in r.get("effects") or []:
            d = round(float(e["delta"]), 4)
            if e["operand"] in cur:
                cur[e["operand"]]["delta"] = max(cur[e["operand"]]["delta"], d)
            else:
                cur[e["operand"]] = {"operand": e["operand"], "delta": d}
        n["effects"] = {"immediate": [], "realized": list(cur.values())}
    # integrator edits on nodes outside a group's own lane (decisions.extraEdits)
    for ed in decisions.get("extraEdits", []):
        n = by_id.get(ed["id"])
        if n is None:
            report["rejected"].append({"id": ed["id"], "why": "extraEdit target missing"})
            continue
        old = n.get("prereq")
        n["prereq"] = ed["newPrereq"]
        report["applied"].append({"id": ed["id"], "old": {"prereq": old}, "new": {"prereq": ed["newPrereq"]},
                                  "action": "integrator cross-lane edge", "rationale": ed.get("why")})
    # saturation: an operand whose summed deltas exceed 90 % of its cap is scaled down proportionally, so every
    # node's effect still counts (otherwise the clamp would make the last nodes inert)
    caps = decisions.get("caps", {})
    sums = {}
    for n in tech + civ:
        if isinstance(n.get("repeatable"), dict):
            continue
        for e in (n.get("effects") or {}).get("realized", []):
            sums[e["operand"]] = sums.get(e["operand"], 0.0) + e["delta"]
    for op, total in sorted(sums.items()):
        cap = caps.get(op)
        if cap is None:
            continue
        if total > 0.9 * cap:
            f = 0.9 * cap / total
            for n in tech + civ:
                for e in (n.get("effects") or {}).get("realized", []):
                    if e["operand"] == op:
                        e["delta"] = max(round(e["delta"] * f, 4), 0.001)
            report["saturation"][op] = {"sum": round(total, 4), "cap": cap, "scaledBy": round(f, 4)}

    # 5. Dependency / Age changes proposed by the audit, applied only where the integrator approved them
    approved = set(decisions.get("applyPrereq", [])) | ({"*"} if decisions.get("applyAllValid") else set())
    for o in outs:
        for r in o.get("audit", []):
            if r.get("newPrereq") is None and r.get("newAge") is None:
                continue
            if "*" not in approved and r["id"] not in approved:
                report["rejected"].append({"id": r["id"], "why": "not approved by the integrator", "action": r.get("action")})
                continue
            n = by_id.get(r["id"])
            if n is None:
                continue
            old = {"prereq": n.get("prereq"), "age": n.get("age")}
            if r.get("newPrereq") is not None:
                n["prereq"] = r["newPrereq"] or None
            if r.get("newAge"):
                n["age"] = r["newAge"]
            report["applied"].append({"id": r["id"], "old": old, "new": {"prereq": n.get("prereq"), "age": n.get("age")},
                                      "action": r.get("action"), "rationale": r.get("rationale")})

    # 6. Derived fields: depth (longest prerequisite path, union over OR), recursive set
    nodes = tech + civ
    ast = {n["id"]: kr.parse(n.get("prereq")) for n in nodes}

    # 6a. Subtree placement (loader rule, architecture 8.4.5): a Main-tree technology may never require subtree knowledge.
    # A NEW technology that does is placed in the subtree of its subtree prerequisites (the most common one; ties by
    # subtree order), with that subtree's university as its primary; repeated to a fixpoint because a moved node's
    # Main-tree dependents may then need moving too. A pristine node in that position is an error (an audit edge).
    order = list(BRANCH_UNIV)
    moved = True
    while moved:
        moved = False
        for n in tech:
            if n.get("branch") is not None:
                continue
            subs = [by_id[a]["branch"] for a in kr.atoms(ast[n["id"]]) if a in by_id and by_id[a].get("branch")] \
                if ast[n["id"]] else []
            if not subs:
                continue
            if n["id"] not in imported_ids:
                raise SystemExit(f"pristine Main-tree technology {n['id']} now requires subtree knowledge ({subs})")
            b = min(set(subs), key=lambda x: (-subs.count(x), order.index(x)))
            n["branch"] = b
            u = BRANCH_UNIV[b]
            n["universityRelevance"] = [{"type": u, "name": UNIV_NAMES[u], "role": "primary"}]
            report.setdefault("subtreePlacement", []).append({"id": n["id"], "branch": b, "because": sorted(set(subs))})
            moved = True
    depth = {}

    def dep(i, stack=()):
        if i in depth:
            return depth[i]
        if i in stack:
            raise SystemExit(f"cycle at {i}: {' -> '.join(stack + (i,))}")
        a = ast[i]
        d = 0 if a is None else max((dep(x, stack + (i,)) + 1 for x in kr.atoms(a) if x in by_id), default=0)
        depth[i] = d
        return d
    missing = sorted({x for i in ast if ast[i] for x in kr.atoms(ast[i]) if x not in by_id})
    if missing:
        raise SystemExit(f"unknown prerequisite atoms: {missing[:20]}")
    for n in nodes:
        n["depth"] = dep(n["id"])

    # 6a'. A subtree repeatable states how many finite nodes of its subtree it waits for (loader rule) — recounted
    for n in tech:
        rep = n.get("repeatable")
        av = rep.get("availability") if isinstance(rep, dict) else None
        if isinstance(av, dict) and av.get("subtree"):
            av["finite_nodes_to_exhaust"] = sum(1 for x in tech if x.get("branch") == av["subtree"]
                                                and not isinstance(x.get("repeatable"), dict))

    # 6b. Implied Eurekas (loader rule): a Eureka whose condition is guaranteed by the node's own prerequisites would fire
    # the moment the node became available. An audit edge that made the precursor a hard prerequisite turns its Eureka
    # into exactly that, so the Eureka is dropped (the knowledge is now required, not a circumstance).
    guaranteed = {}

    def guar(i):
        if i in guaranteed:
            return guaranteed[i]
        def g(a):
            if a is None:
                return set()
            if a[0] == "atom":
                return {a[1]} | guar(a[1]) if a[1] in by_id else set()
            if a[0] == "and":
                out = set()
                for x in a[1]:
                    out |= g(x)
                return out
            if a[0] == "or":
                sets = [g(x) for x in a[1]]
                return set.intersection(*sets) if sets else set()
            return set()
        guaranteed[i] = g(ast[i])
        return guaranteed[i]
    for n in nodes:
        kept = []
        for e in n.get("eurekas") or []:
            w = e.get("when")
            try:
                wa = kr.parse(w) if isinstance(w, str) else None
            except ValueError:
                wa = None
            if wa is not None and kr.atoms(wa) and all(a in by_id for a in kr.atoms(wa)) and kr.holds(wa, guar(n["id"])):
                report.setdefault("impliedEurekasDropped", []).append({"id": n["id"], "when": w})
                continue
            kept.append(e)
        if len(kept) != len(n.get("eurekas") or []):
            n["eurekas"] = kept
    out = dict(current)
    out["technologies"] = tech
    out["civics"] = civ
    rec = sorted(n["id"] for n in nodes if isinstance(n.get("repeatable"), dict))
    rs = dict(out["researchSets"])
    rs["recursive"] = rec
    out["researchSets"] = rs
    report["counts"] = {
        "technologies": len(tech), "civics": len(civ),
        "technologiesFinite": sum(1 for n in tech if not isinstance(n.get("repeatable"), dict)),
        "civicsFinite": sum(1 for n in civ if not isinstance(n.get("repeatable"), dict)),
        "imported": len(imported_ids), "withRealizedEffects": sum(1 for n in nodes if (n.get("effects") or {}).get("realized"))}
    kr.dump_json(report, T2 / "import-report.json")
    if not dry:
        with open(RESEARCH, "w", encoding="utf-8") as fh:
            json.dump(out, fh, indent=1, ensure_ascii=True)
            fh.write("\n")
    print(json.dumps(report["counts"]))
    print(f"applied {len(report['applied'])} dependency/Age changes; rejected {len(report['rejected'])}")


if __name__ == "__main__":
    main(sys.argv[1:])
