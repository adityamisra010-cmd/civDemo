#!/usr/bin/env python3
"""Extract the CURRENT research tree into a design-package inventory.

Reads (read-only): Sim.Data/content/research.json, ages.json, sim.json,
era-pacing.json and docs/research-gameplay-unlock-audit.md (section 5, the
CI-checked per-node consumer table). Writes:
data/current-tree-inventory.json - one record per technology and civic node
with its prerequisites, dependants, consumers, Age-milestone links, cost
rationale, closure cost and depth (all MEASURED from the files; closure cost
is a greedy cheapest-route figure, labelled as such).

Usage: python3 -I docs/m6-plan/knowledge-redesign/tools/extract_current.py
"""
import re
import subprocess
import sys
from collections import Counter, defaultdict

sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402


def git_head():
    try:
        return subprocess.check_output(["git", "-C", str(kr.ROOT), "rev-parse", "HEAD"], text=True).strip()
    except Exception:  # pragma: no cover - provenance only
        return None


def unlock_audit_rows():
    """node id -> {class, consequences, canonical, enforced, ui} from section 5."""
    path = kr.ROOT / "docs" / "research-gameplay-unlock-audit.md"
    rows, in_table = {}, False
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("## 5. Every node"):
            in_table = True
            continue
        if in_table and line.startswith("## "):
            break
        if not in_table or not line.startswith("| `"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 10:
            continue
        nid = cells[0].strip("`")
        rows[nid] = {
            "class": cells[3], "consequences": cells[6], "canonical": cells[7],
            "enforced": cells[8], "ui": cells[9],
        }
    return rows


def main():
    research = kr.load_json(kr.CONTENT / "research.json")
    ages = kr.load_json(kr.CONTENT / "ages.json")
    sim = kr.load_json(kr.CONTENT / "sim.json")
    pacing = kr.load_json(kr.CONTENT / "era-pacing.json")
    techs, civics = research["technologies"], research["civics"]
    allnodes = [dict(n, tree="technology") for n in techs] + [dict(n, tree="civics") for n in civics]
    g = kr.Graph(allnodes)
    assert not g.missing_refs(), g.missing_refs()
    assert g.find_cycle() is None

    # consumers: entities whose `requires` names the node
    ent_consumers = defaultdict(list)
    for e in research["entities"]:
        for a in kr.atoms(kr.parse(e.get("requires"))):
            ent_consumers[a].append(e["id"])
    stage_atoms = kr.atoms(kr.parse(research["researchStage"]["requires"]))
    tax_atoms = []
    for sec in sim.values():
        if isinstance(sec, dict) and isinstance(sec.get("taxationRequires"), str):
            tax_atoms = kr.atoms(kr.parse(sec["taxationRequires"]))
    # Age milestone links
    age_links = defaultdict(list)
    age_facts = []
    for age in ages["ages"]:
        entry = age.get("entry") or {}
        for role in ("core", "supporting"):
            for m in entry.get(role, []):
                fact = m.get("fact", {})
                rec = {"age_key": age["key"], "age_id": age["id"], "role": role, "milestone": m["id"],
                       "category": m.get("category"), "fact": fact}
                age_facts.append(rec)
                if fact.get("kind") == "research":
                    for nid in fact.get("nodes", []):
                        age_links[nid].append({"age_key": age["key"], "role": role, "milestone": m["id"],
                                               "any_of": fact.get("nodes", [])})
    sets = research.get("researchSets", {})
    recursive = set(sets.get("recursive", []))
    speculative = set(sets.get("speculative_finite", []))
    audit = unlock_audit_rows()

    records = []
    for n in allnodes:
        i = n["id"]
        cr = n.get("costRationale") or {}
        unl = n.get("unlocks") or {}
        clos = g.closure(i)
        rec = {
            "key": n["key"], "id": i, "name": n["name"], "tree": n["tree"],
            "branch": n.get("branch") or "trunk", "domain": n.get("domain"),
            "secondaryDomains": n.get("secondaryDomains") or [], "age": n.get("age"),
            "frontier": bool(n.get("frontier")), "emerged": n.get("emerged"),
            "cost": n.get("cost"), "magnitude": cr.get("magnitude"), "tier": cr.get("tier"),
            "factors": cr.get("factors"), "costNote": cr.get("note"),
            "prereq": n.get("prereq"), "prereqAtoms": kr.atoms(g.ast[i]),
            "dependants": sorted(g.deps[i]),
            "descendantCount": len(g.descendants(i)),
            "entityConsumers": sorted(ent_consumers.get(i, [])),
            "researchStageAtom": i in stage_atoms, "taxGateAtom": i in tax_atoms,
            "ageMilestones": age_links.get(i, []),
            "unlocks": {k: v for k, v in unl.items() if v},
            "family": n.get("family"), "generation": n.get("generation"),
            "effects": (n.get("effects") or {}).get("immediate") or [],
            "repeatable": n.get("repeatable"),
            "set": "recursive" if i in recursive else ("speculative_finite" if i in speculative else "finite"),
            "eurekas": [{"text": e.get("text"), "kind": e.get("kind"), "weight": e.get("weight"),
                         "evaluable_now": e.get("evaluable_now"), "class": e.get("class")}
                        for e in (n.get("eurekas") or [])],
            "universityRelevance": n.get("universityRelevance") or [],
            "depthField": n.get("depth"), "depthMin": g.depth(i),
            "closureCount": len(clos), "closureCost": round(g.closure_cost(i), 1),
            "allAncestorCount": len(g.all_ancestors(i)),
            "desc": n.get("desc"), "descWords": kr.word_count(n.get("desc")),
            "unlockAudit": audit.get(i),
        }
        records.append(rec)

    # summaries
    by = lambda f: dict(sorted(Counter(f(r) for r in records).items(), key=lambda kv: str(kv[0])))
    tot = lambda rs: round(sum(r["cost"] or 0 for r in rs), 1)
    summary = {
        "counts": {"technology": len(techs), "civics": len(civics), "total": len(allnodes),
                   "byTreeBranch": by(lambda r: f"{r['tree']}/{r['branch']}"),
                   "byAge": by(lambda r: r["age"]),
                   "bySet": by(lambda r: r["set"]),
                   "byUnlockClass": by(lambda r: (r["unlockAudit"] or {}).get("class", "?")[:1]),
                   "entities": by(lambda r: None) and dict(Counter(e["kind"] for e in research["entities"]))},
        "totalCost": {"all": tot(records),
                      "finite": tot([r for r in records if r["set"] != "recursive"]),
                      "byAge": {a: tot([r for r in records if r["age"] == a]) for a in kr.AGES},
                      "byTreeBranch": {k: tot([r for r in records if f"{r['tree']}/{r['branch']}" == k])
                                       for k in sorted({f"{r['tree']}/{r['branch']}" for r in records})}},
        "researchStage": {"requires": research["researchStage"]["requires"], "atoms": stage_atoms,
                          "closureCost": round(sum(g.cost(j) for j in g._expr_closure(kr.parse(research["researchStage"]["requires"]))), 1),
                          "closureCount": len(g._expr_closure(kr.parse(research["researchStage"]["requires"])))},
        "taxGate": {"requires": tax_atoms},
        "ageFacts": age_facts,
        "tuning": research["tuning"],
        "eraPacing": pacing,
        "descWordStats": {"min": min(r["descWords"] for r in records), "max": max(r["descWords"] for r in records),
                          "mean": round(sum(r["descWords"] for r in records) / len(records), 1)},
    }
    out = {"_doc": ["MEASURED inventory of the CURRENT research content, generated by tools/extract_current.py.",
                    "closureCount/closureCost: the node plus a cheapest satisfying prerequisite set (greedy over OR "
                    "alternatives; an upper bound on the true minimum). depthMin: shortest prerequisite chain "
                    "(roots = 1). unlockAudit: copied from docs/research-gameplay-unlock-audit.md section 5."],
           "provenance": {"gitHead": git_head(), "researchSchema": research.get("schema"),
                          "corpusSha256": research.get("source", {}).get("corpusSha256")},
           "summary": summary, "nodes": records,
           "entities": research["entities"], "baseline": research["baseline"],
           "sectorActivities": research["sectorActivities"], "universityTypes": research["universityTypes"]}
    kr.dump_json(out, kr.DATA / "current-tree-inventory.json")
    print(f"wrote {kr.DATA / 'current-tree-inventory.json'}: {len(records)} nodes")
    print("counts", summary["counts"]["byTreeBranch"])
    print("total cost", summary["totalCost"]["all"], "finite", summary["totalCost"]["finite"])
    print("stage", summary["researchStage"]["closureCount"], summary["researchStage"]["closureCost"])
    print("desc words", summary["descWordStats"])


if __name__ == "__main__":
    main()
