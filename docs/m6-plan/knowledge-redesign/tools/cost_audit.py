"""Research cost audit (D-064 cost pass): every node of both trees, valued by its LIVE gameplay content.

For each node: key, id, name, tree, Age, content factors and magnitude, content cost (U x K^magnitude), pacing scale and
BaseCost, prerequisites (atoms, OR alternatives, cross-tree atoms), dependents (direct, transitive), role flags (Age
milestone fact, live gate, stage), live value (operand effects weighted by operand importance and share of cap; live
unlocks; order gates), placeholder content (unlocks no system realizes yet), the Age-sweep burden of its Age, and a
disposition (keep / reduce / increase) with the factor change and the reason. Repeatables get a per-level table.

The disposition is a SCREEN, not a formula that sets costs by effect count: it compares a node's magnitude rank with its
live-value rank inside its (tree, Age) cohort and flags only large disagreements; tools/apply_cost_pass.py applies the
reviewed decisions (decisions.json), each with its own reason, through the content factors (finalization ruling 1: a
cost is always U x K^magnitude of its factors; no calibration adjustment).

Usage: python3 -I tools/cost_audit.py [--research PATH] [--out-prefix NAME] [--live live-map.json]
"""
import csv
import json
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

OUT = kr.PKG / "research-cost-pass"
FACTORS = ["novelty", "difficulty", "material", "institutional", "breadth", "prereq_complexity"]

# Operand importance (gameplay reach of the owning system, judged per system — the report states the basis):
# 3 = drives food, people, knowledge or revenue for the whole polity; 2 = a real but narrower economy or society lever;
# 1 = convenience or reach (exploration, sight, founding party size, river haulage).
WEIGHT = {
    "farming.yieldPerArableKm2PerYear": 3, "farming.outputPerFarmerPerYear": 3, "production.outputPerExtractorPerYear": 3,
    "research.researchPointsCoefficient": 3, "research.civicsPointsCoefficient": 3, "demographics.mortalityPerYear": 3,
    "governance.taxExtractionResponseMax": 3,
    "fog.exploreStepNodes": 1, "fog.exploreMaxRadiusNodes": 1, "fog.formationSightNodes": 1,
    "colonization.orderPartyFraction": 1, "transport.riverCostFactor": 1,
}
DEFAULT_WEIGHT = 2
LIVE_PREFIX = ("building.", "infra.", "recipe.", "activity.", "inst.")


def caps():
    import re
    reg = (kr.ROOT / "Sim.Core" / "State" / "KnowledgeOperands.cs").read_text(encoding="utf-8")
    out = {}
    for m in re.finditer(r'new\(KnowledgeOperand\.\w+, "([^"]+)", OperandDirection\.(\w+), ([\d.]+),', reg):
        out[m.group(1)] = float(m.group(3))
    return out


def main(argv):
    research = Path(argv[argv.index("--research") + 1]) if "--research" in argv else kr.CONTENT / "research.json"
    prefix = argv[argv.index("--out-prefix") + 1] if "--out-prefix" in argv else "audit"
    live_path = Path(argv[argv.index("--live") + 1]) if "--live" in argv else None
    d = kr.load_json(research)
    live = kr.load_json(live_path) if live_path and live_path.exists() else None
    live_ops = None
    live_entities = None
    if live:
        live_ops = {o["id"] for o in live.get("operands", []) if o.get("live")}
        live_entities = set(live.get("liveEntities", []))
    cap = caps()
    t = d["tuning"]
    pace = t.get("pacingScale") or {}
    nodes = [("technology", n) for n in d["technologies"]] + [("civics", n) for n in d["civics"]]
    tree_of = {n["id"]: tree for tree, n in nodes}
    gates = {a for _, expr in kr.live_gates() for a in kr.atoms(kr.parse(expr))}
    ages = kr.load_json(kr.CONTENT / "ages.json")
    milestone = defaultdict(list)
    for ag in ages["ages"]:
        for kind in ("core", "supporting"):
            for m in (ag.get("entry") or {}).get(kind, []):
                f = m.get("fact") or {}
                if f.get("kind") == "research":
                    for nid in f.get("nodes", []):
                        milestone[nid].append(f"{ag['id']}:{kind}")
    stage = set(kr.atoms(kr.parse(d.get("stage", {}).get("requires")))) if isinstance(d.get("stage"), dict) and d["stage"].get("requires") else set()
    ents = {e["id"]: e for e in d.get("entities", [])}
    asts = {n["id"]: kr.parse(n.get("prereq")) for _, n in nodes}
    deps = defaultdict(set)
    for nid, a in asts.items():
        for x in (kr.atoms(a) if a else []):
            deps[x].add(nid)

    def descendants(nid):
        seen, stack = set(), [nid]
        while stack:
            for c in deps[stack.pop()]:
                if c not in seen:
                    seen.add(c)
                    stack.append(c)
        return seen

    rows = []
    for tree, n in nodes:
        cr = n["costRationale"]
        age = int(n["age"][1])
        scale = (pace.get(tree) or [1.0] * 9)[age - 1]
        rep = n.get("repeatable") if isinstance(n.get("repeatable"), dict) else None
        a = asts[n["id"]]
        atoms = sorted(kr.atoms(a)) if a else []
        expr = n.get("prereq") or ""
        realized = (n.get("effects") or {}).get("realized", [])
        eff_value, eff_desc = 0.0, []
        for e in realized:
            op = e["operand"]
            w = WEIGHT.get(op, DEFAULT_WEIGHT)
            is_live = live_ops is None or op in live_ops
            v = (w * e["delta"] / cap.get(op, 1.0) * 10.0) if is_live else 0.0
            eff_value += v
            eff_desc.append(f"{op} +{e['delta']:g}{'' if is_live else ' (NOT LIVE)'}")
        unl = [x for k in ("units", "buildings", "institutions", "infrastructure", "activities", "projects")
               for x in (n["unlocks"].get(k) or [])]
        if live_entities is not None:
            live_unl = [x for x in unl if x in live_entities]
        else:
            live_unl = [x for x in unl if x.startswith(LIVE_PREFIX)]
        placeholder = [x for x in unl if x not in live_unl]
        gate = n["id"] in gates
        unlock_value = min(6.0, 2.0 * len(live_unl)) + (4.0 if gate else 0.0)
        direct, desc = len(deps[n["id"]]), len(descendants(n["id"]))
        struct_value = 0.5 * min(direct, 6) + (2.0 if milestone.get(n["id"]) else 0.0) + (2.0 if n["id"] in stage else 0.0)
        value = round(eff_value + unlock_value + struct_value, 3)
        rows.append({
            "key": n["key"], "id": n["id"], "name": n["name"], "tree": tree, "age": age,
            "repeatable": bool(rep), "levels": bool(rep and rep.get("levels")),
            "costGrowth": rep.get("costGrowth") if rep else None,
            **{f: cr["factors"][f] for f in FACTORS}, "magnitude": cr["magnitude"],
            "contentCost": n["cost"], "pacingScale": scale, "baseCost": round(n["cost"] * scale, 3),
            "prereq": expr, "prereqAtoms": len(atoms), "hasOr": " OR " in expr,
            "crossTreeAtoms": sum(1 for x in atoms if tree_of.get(x) != tree),
            "directDependents": direct, "allDescendants": desc,
            "milestone": ";".join(milestone.get(n["id"], [])), "liveGate": gate, "stage": n["id"] in stage,
            "effects": "; ".join(eff_desc), "effectValue": round(eff_value, 3),
            "liveUnlocks": " ".join(live_unl), "placeholderUnlocks": " ".join(placeholder),
            "value": value,
        })

    # Sweep burden: with ageSweep on, a node of Age a >= 3 also needs every finite node of Age a-2 in BOTH trees.
    sweep = bool(t.get("ageSweep"))
    cohort = defaultdict(float)
    for r in rows:
        if not r["repeatable"]:
            cohort[r["age"]] += r["baseCost"]
    for r in rows:
        r["sweepBurdenBaseCost"] = round(cohort[r["age"] - 2], 1) if sweep and r["age"] >= 3 and not r["repeatable"] else 0.0
        r["sweepMandatory"] = sweep and not r["repeatable"] and r["age"] <= 7

    # The screen: magnitude rank vs value rank inside each (tree, Age) cohort of finite nodes.
    groups = defaultdict(list)
    for r in rows:
        if not r["repeatable"]:
            groups[(r["tree"], r["age"])].append(r)
    for g in groups.values():
        k = len(g)

        def pct(key):
            order = sorted(g, key=lambda r: (r[key], r["key"]))
            return {r["id"]: (i / (k - 1) if k > 1 else 0.5) for i, r in enumerate(order)}
        for r in g:
            r["power"] = round(r["effectValue"] + (min(6.0, 2.0 * len(r["liveUnlocks"].split())) if r["liveUnlocks"] else 0.0)
                               + (4.0 if r["liveGate"] else 0.0), 3)
        mp, vp, pp = pct("magnitude"), pct("value"), pct("power")
        vmed = sorted(r["value"] for r in g)[k // 2]
        for r in g:
            r["magnitudePct"], r["valuePct"] = round(mp[r["id"]], 3), round(vp[r["id"]], 3)
            gap = mp[r["id"]] - vp[r["id"]]
            disp, step, why = "keep", 0.0, "magnitude and live value agree within its cohort"
            if r["value"] == 0.0 or (r["effectValue"] == 0.0 and not r["liveUnlocks"] and not r["liveGate"]
                                     and r["directDependents"] == 0 and not r["milestone"]):
                disp, step = "reduce", (-0.5 if mp[r["id"]] > 0.5 else -0.25)
                why = "no live effect, unlock, gate or dependent today (placeholder content only): priced for its live value"
            elif gap > 0.5 and r["value"] <= vmed:
                disp, step = "reduce", (-0.5 if gap > 0.7 else -0.25)
                why = f"priced in the top {100 - round(100 * mp[r['id']])}% of its cohort but delivers bottom-half live value ({r['value']})"
            elif r["age"] >= 3 and pp[r["id"]] >= 0.85 and mp[r["id"]] <= 0.3:
                disp, step = "increase", (0.5 if mp[r["id"]] <= 0.15 else 0.25)
                why = f"top-15% live power ({r['power']}) priced in the bottom {round(100 * mp[r['id']])}% of its cohort"
            r["screen"], r["screenStep"], r["screenReason"] = disp, step, why
    for r in rows:
        if r["repeatable"]:
            r["magnitudePct"] = r["valuePct"] = None
            r["screen"], r["screenStep"], r["screenReason"] = "repeatable", 0.0, "priced by the repeatable level rule (separate table)"

    OUT.mkdir(parents=True, exist_ok=True)
    cols = list(rows[0].keys())
    with open(OUT / f"{prefix}.csv", "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=cols)
        w.writeheader()
        for r in rows:
            w.writerow(r)
    summary = defaultdict(lambda: defaultdict(int))
    for r in rows:
        summary[f"{r['tree']} A{r['age']}"][r["screen"]] += 1
    print(json.dumps({k: dict(v) for k, v in sorted(summary.items())}, indent=None))
    print(f"{len(rows)} nodes -> {OUT / (prefix + '.csv')}")


if __name__ == "__main__":
    main(sys.argv[1:])
